# -*- coding: utf-8 -*-
# ============================================================================================
#  MOTOR DE IA EN TIEMPO REAL (rocola-hd): mantiene Apollo cargado en la GPU y procesa canciones
#  BAJO DEMANDA. La rocola (C#) pide trabajos dejando un archivo .job; el motor los procesa y
#  deja el resultado en cache/listos/<hash>.wav float32. La rocola reproduce ese WAV si existe; si no,
#  reproduce el original (nunca se bloquea). Con cache: cada cancion se procesa UNA vez.
#  La MUSICA ORIGINAL NUNCA SE TOCA: todo vive en la carpeta cache del motor, con tope de tamano.
#
#  Protocolo (carpeta, sin sockets ni dependencias raras -> robusto y facil de depurar):
#    cache/pendientes/<hash>.job   JSON con src, mezcla, mezcla_key, pipeline y cache_key
#    cache/listos/<hash>.wav       version mejorada float32, sin nueva compresion con perdida <- motor
#    cache/listos/<hash>.orig      marca "ya era de calidad completa; usa el ORIGINAL"     <- motor
#    cache/listos/<hash>.error                                                             <- si algo fallo
#    cache/modelo.fingerprint     huella que la rocola copia al job y usa en el hash
#  El HASH lo calculan IGUAL la rocola y el motor: SHA-256 de ruta, tamano, fecha UTC,
#  mezcla y huella exacta del modelo/proceso. Cambiar una cancion o el modelo invalida la cache.
#
#    python daemon_ia.py [--cache <carpeta>] [--tope_gb 20]
# ============================================================================================
import sys, os, json, time, fnmatch, hashlib, subprocess, argparse, shutil, math, stat
# Bajo pythonw.exe (la rocola lo lanza SIN consola) sys.stdout y sys.stderr son None: cualquier
# print() nuestro o barra de progreso de una libreria (huggingface/tqdm) reventaria con
# "'NoneType' object has no attribute 'write'". Los redirigimos a la nada para que nada truene;
# nuestro registro real va aparte a cache/motor_log.txt.
if sys.stdout is None: sys.stdout = open(os.devnull, "w")
if sys.stderr is None: sys.stderr = open(os.devnull, "w")
# ffmpeg es un programa de CONSOLA: lanzado desde pythonw (que no tiene consola) Windows le creaba
# una ventana CMD nueva por cada cancion (el parpadeo molesto). CREATE_NO_WINDOW la suprime.
NOWIN = getattr(subprocess, "CREATE_NO_WINDOW", 0)   # 0 fuera de Windows
# BLINDAJE ANTI-CONGELON: un archivo corrupto (tipico de discos rescatados) puede decodificar a
# HORAS de audio basura -> gigas y gigas en RAM -> Windows se ahoga en swap y la maquina entera se
# congela. Tope duro: canciones de mas de 15 min (o que ffprobe no puede leer) NO pasan por la IA;
# se marcan .orig y la rocola reproduce el original tal cual. Ademas la decodificacion lleva -t de
# red de seguridad, para que ffmpeg no pueda escribir un wav sin fin ni aunque mienta la cabecera.
MAX_SEG = 15 * 60
TIMEOUT_PROBE = 30
TIMEOUT_DECODE = 20 * 60
TIMEOUT_ENCODE = 20 * 60
MAX_REINTENTOS = 3
BACKOFF = (60, 5 * 60, 30 * 60)
PIPELINE_VERSION = "apollo-safe-v4-float-decode"
CACHE_FORMAT_VERSION = "wav-float32-v4-sin-recompresion"
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "upscale"))
from util_audio import FS, leer_wav, escribir_wav_float, acantilado_hz, por_trozos, mezclar, mezcla_auto

def _ticks_desde_stat(st):
    # DateTime.UnixEpoch.Ticks: reproduce File.GetLastWriteTimeUtc(path).Ticks de C#.
    return st.st_mtime_ns // 100 + 621355968000000000


def _ticks_mtime(path):
    return _ticks_desde_stat(os.stat(path))


def _lower_invariant_dotnet(texto):
    """Equivalente practico de ``String.ToLowerInvariant`` para nombres de archivo.

    ``str.lower`` aplica mapeos Unicode expansivos (por ejemplo, U+0130 pasa a dos
    puntos de codigo), mientras .NET conserva ese caracter. Esa diferencia hacia que
    el motor rechazara trabajos validos de canciones con ciertos nombres Unicode.
    """
    salida = []
    for caracter in texto:
        bajo = caracter.lower()
        salida.append(bajo if len(bajo) == 1 else caracter)
    return "".join(salida)


def hash_trabajo(src, mezcla_clave, pipeline):
    st = os.stat(src)
    clave = (_lower_invariant_dotnet(os.path.abspath(src)) + "|" + str(st.st_size) + "|" +
             str(_ticks_desde_stat(st)) + "|" + str(mezcla_clave) + "|" + str(pipeline))
    return hashlib.sha256(clave.encode("utf-8", "replace")).hexdigest()[:16]


def huella_pipeline(model_fingerprint, seg):
    identidad = "%s|modelo=%s|seg=%.9g|fs=%d|cache=%s" % (
        PIPELINE_VERSION, model_fingerprint, seg, FS, CACHE_FORMAT_VERSION)
    return hashlib.sha256(identidad.encode("utf-8")).hexdigest()

_JOB_HANDLE = None
def _activar_job_hijos():
    """En Windows, mata tambien ffmpeg si el proceso Python es terminado a la fuerza."""
    global _JOB_HANDLE
    if os.name != "nt": return
    try:
        import ctypes
        from ctypes import wintypes
        class BASIC(ctypes.Structure):
            _fields_ = [("PerProcessUserTimeLimit", ctypes.c_int64), ("PerJobUserTimeLimit", ctypes.c_int64),
                        ("LimitFlags", wintypes.DWORD), ("MinimumWorkingSetSize", ctypes.c_size_t),
                        ("MaximumWorkingSetSize", ctypes.c_size_t), ("ActiveProcessLimit", wintypes.DWORD),
                        ("Affinity", ctypes.c_size_t), ("PriorityClass", wintypes.DWORD),
                        ("SchedulingClass", wintypes.DWORD)]
        class IO(ctypes.Structure):
            _fields_ = [("ReadOperationCount", ctypes.c_uint64), ("WriteOperationCount", ctypes.c_uint64),
                        ("OtherOperationCount", ctypes.c_uint64), ("ReadTransferCount", ctypes.c_uint64),
                        ("WriteTransferCount", ctypes.c_uint64), ("OtherTransferCount", ctypes.c_uint64)]
        class EXT(ctypes.Structure):
            _fields_ = [("BasicLimitInformation", BASIC), ("IoInfo", IO),
                        ("ProcessMemoryLimit", ctypes.c_size_t), ("JobMemoryLimit", ctypes.c_size_t),
                        ("PeakProcessMemoryUsed", ctypes.c_size_t), ("PeakJobMemoryUsed", ctypes.c_size_t)]
        k = ctypes.windll.kernel32
        k.CreateJobObjectW.restype = wintypes.HANDLE
        k.CreateJobObjectW.argtypes = [ctypes.c_void_p, wintypes.LPCWSTR]
        k.SetInformationJobObject.argtypes = [wintypes.HANDLE, ctypes.c_int,
                                              ctypes.c_void_p, wintypes.DWORD]
        k.AssignProcessToJobObject.argtypes = [wintypes.HANDLE, wintypes.HANDLE]
        k.GetCurrentProcess.restype = wintypes.HANDLE
        k.CloseHandle.argtypes = [wintypes.HANDLE]
        h = k.CreateJobObjectW(None, None)
        if not h: return
        info = EXT(); info.BasicLimitInformation.LimitFlags = 0x00002000  # KILL_ON_JOB_CLOSE
        if not k.SetInformationJobObject(h, 9, ctypes.byref(info), ctypes.sizeof(info)):
            k.CloseHandle(h); return
        if not k.AssignProcessToJobObject(h, k.GetCurrentProcess()):
            k.CloseHandle(h); return
        _JOB_HANDLE = h
    except Exception:
        _JOB_HANDLE = None

def _run(cmd, timeout):
    try:
        return subprocess.run(cmd, check=True, stdin=subprocess.DEVNULL,
                              stdout=subprocess.DEVNULL, stderr=subprocess.PIPE,
                              timeout=timeout, creationflags=NOWIN)
    except subprocess.TimeoutExpired:
        raise RuntimeError("el proceso externo excedio el limite de %ds" % timeout)
    except subprocess.CalledProcessError as e:
        detalle = (e.stderr or b"").decode("utf-8", "replace").strip()[-2000:]
        raise RuntimeError("fallo el proceso externo%s" % ((": " + detalle) if detalle else ""))


def _verificar_herramienta(path, nombre):
    try:
        subprocess.run([path, "-version"], check=True, stdin=subprocess.DEVNULL,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                       timeout=15, creationflags=NOWIN)
    except Exception as e:
        raise RuntimeError("%s no funciona: %s" % (nombre, e))

def _log(cache, msg):
    try:
        p = os.path.join(cache, "motor_log.txt")
        try:
            if os.path.getsize(p) > 512 * 1024: os.remove(p)   # tope ~512KB: la rocola corre meses; el log no crece sin fin
        except OSError: pass
        with open(p, "a", encoding="utf-8") as f:
            f.write(time.strftime("%Y-%m-%d %H:%M:%S  ") + msg + "\n")
    except OSError: pass

def _ffprobe_de(ffmpeg):
    """El ffprobe que acompana al ffmpeg (mismo directorio), o el del PATH, o None."""
    exe = "ffprobe.exe" if os.name == "nt" else "ffprobe"
    p = os.path.join(os.path.dirname(ffmpeg), exe)
    if os.path.isfile(p): return p
    return shutil.which("ffprobe")

def _duracion(ffprobe, src):
    """Duracion en segundos segun ffprobe, o None si no se pudo leer (archivo sospechoso)."""
    try:
        r = subprocess.run([ffprobe, "-v", "error", "-select_streams", "a:0",
                            "-show_entries", "stream=index:format=duration",
                            "-of", "json", src], capture_output=True, text=True,
                           stdin=subprocess.DEVNULL, timeout=TIMEOUT_PROBE, creationflags=NOWIN)
        if r.returncode != 0: return None
        dato = json.loads(r.stdout)
        dur = float(dato.get("format", {}).get("duration", 0))
        return dur if dato.get("streams") and math.isfinite(dur) and dur > 0 else None
    except Exception:
        return None

def _marca_original(listos, h, motivo):
    """Deja la marca <hash>.orig: la rocola reproduce el ORIGINAL y no vuelve a pedir el trabajo.
    (La misma marca que 'ya era de calidad completa'; aqui el motivo es que no conviene procesarla.)"""
    dst = os.path.join(listos, h + ".orig")
    tmp = dst + ".tmp"
    try:
        with open(tmp, "w", encoding="utf-8") as f:
            f.write(motivo); f.flush(); os.fsync(f.fileno())
        os.replace(tmp, dst)
    finally:
        try: os.remove(tmp)
        except OSError: pass
    return dst

def _candado(cache):
    """UN SOLO MOTOR por cache, garantizado por el SISTEMA OPERATIVO: candado de archivo que se
    libera solo cuando el proceso muere (sin ventanas de carrera ni candados huerfanos).
    El chequeo anterior (latido con menos de 12s) fallaba en DOS ventanas: el latido no existia
    mientras Apollo cargaba (~30-90s) y se quedaba viejo durante un trabajo largo -> el vigia de
    la rocola lanzaba OTRO motor, cada motor tomaba el siguiente trabajo pendiente, y N Apollos
    en paralelo se comian toda la RAM y VRAM (el congelon con varias canciones encoladas)."""
    p = os.path.join(cache, "motor.lock")
    try:
        f = open(p, "a+")
        if os.path.getsize(p) == 0:
            f.write("0"); f.flush()
        if os.name == "nt":
            import msvcrt
            f.seek(0); msvcrt.locking(f.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(f, fcntl.LOCK_EX | fcntl.LOCK_NB)
        return f            # mantener VIVA la referencia: el candado dura lo que el proceso
    except OSError:
        return None

def _vivo_bomba(cache):
    """Latido en un HILO propio desde el segundo cero: motor.vivo se refresca cada ~3s SIEMPRE,
    aunque Apollo este cargando o un trabajo tarde minutos. Asi la rocola nunca relanza de mas."""
    import threading
    vivo = os.path.join(cache, "motor.vivo")
    def bomba():
        while True:
            tmp = vivo + ".tmp"
            try:
                with open(tmp, "w", encoding="ascii") as f:
                    f.write(str(int(time.time())))
                os.replace(tmp, vivo)
            except OSError:
                try: os.remove(tmp)
                except OSError: pass
            time.sleep(3.0)
    threading.Thread(target=bomba, daemon=True).start()

def _dirs(cache):
    p = os.path.join(cache, "pendientes"); l = os.path.join(cache, "listos")
    os.makedirs(p, exist_ok=True); os.makedirs(l, exist_ok=True)
    return p, l

def _en_cache(listos, h):
    """Cache de este hash: .mp3 heredado, .wav flotante o .orig (marca)."""
    for ext in (".mp3", ".wav", ".orig"):
        p = os.path.join(listos, h + ext)
        try:
            st = os.stat(p)  # una sola consulta; conserva seguimiento de enlaces
            if stat.S_ISREG(st.st_mode) and (ext == ".orig" or st.st_size > 44):
                return p
        except OSError:
            pass
    return None

def _error_path(listos, h):
    return os.path.join(listos, h + ".error")

_ERRORES_MEM = {}

def _lee_error(listos, h):
    try:
        with open(_error_path(listos, h), encoding="utf-8") as f:
            dato = json.load(f)
        if isinstance(dato, dict):
            _ERRORES_MEM[h] = dato
            return dato
    except Exception:
        pass
    return _ERRORES_MEM.get(h, {})

def _guarda_error(listos, h, intentos, error, terminal=False):
    espera = BACKOFF[min(max(intentos - 1, 0), len(BACKOFF) - 1)]
    dato = {"attempts": intentos, "retry_at": 0 if terminal else time.time() + espera,
            "terminal": bool(terminal), "error": str(error)[:2000],
            "updated_at": int(time.time())}
    dst = _error_path(listos, h); tmp = dst + ".tmp"
    _ERRORES_MEM[h] = dato
    try:
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(dato, f, ensure_ascii=False, sort_keys=True)
            f.flush(); os.fsync(f.fileno())
        os.replace(tmp, dst)
    finally:
        try: os.remove(tmp)
        except OSError: pass

def _quita_error(listos, h):
    if not _quita_requerido(_error_path(listos, h)):
        return False
    _ERRORES_MEM.pop(h, None)
    return True

def _a_cache(listos, h, wav, ffmpeg):
    """Publica WAV flotante sin recodificar. tmp/listos comparten disco/cache.

    El nombre final aparece atomicamente; no hay otra copia completa del audio.
    ffmpeg se conserva en la firma para los llamadores existentes.
    """
    dst = os.path.join(listos, h + ".wav")
    os.replace(wav, dst)
    return dst

DEFERIDO = object()

def _valida_job(job, h, pipeline):
    if not isinstance(job, dict):
        raise ValueError("JSON de trabajo invalido")
    obligatorios = ("src", "mezcla", "mezcla_key", "pipeline", "cache_key")
    if any(k not in job for k in obligatorios):
        raise ValueError("faltan campos obligatorios del trabajo")
    src = job["src"]
    mezcla = job["mezcla"]
    mezcla_clave = job["mezcla_key"]
    cache_key = job["cache_key"]
    if not isinstance(src, str) or not os.path.isabs(src) or not os.path.isfile(src):
        raise ValueError("origen inexistente")
    if not isinstance(mezcla, str) or not isinstance(mezcla_clave, str) or mezcla != mezcla_clave:
        raise ValueError("mezcla y mezcla_key no coinciden")
    if not isinstance(job["pipeline"], str) or job["pipeline"] != pipeline:
        raise ValueError("trabajo creado para otro modelo/pipeline")
    if not isinstance(cache_key, str):
        raise ValueError("cache_key invalido")
    cache_key = cache_key.lower()
    if cache_key != h.lower() or len(cache_key) != 16 or any(c not in "0123456789abcdef" for c in cache_key):
        raise ValueError("cache_key invalido")
    esperado = hash_trabajo(src, mezcla_clave, pipeline)
    if esperado != cache_key:
        raise ValueError("cache_key no coincide con ruta/tamano/fecha/mezcla/modelo")
    if mezcla != "auto":
        try:
            valor = float(mezcla)
        except (TypeError, ValueError):
            raise ValueError("mezcla invalida")
        if not math.isfinite(valor) or not 0 <= valor <= 100:
            raise ValueError("mezcla fuera de 0..100")
    return src, mezcla, mezcla_clave

def _escribe_meta(listos, h, job, pipeline):
    dst = os.path.join(listos, h + ".meta.json"); tmp = dst + ".tmp"
    st = os.stat(job["src"])
    dato = {"pipeline": pipeline, "src": os.path.abspath(job["src"]),
            "src_size": st.st_size, "src_mtime_ticks": _ticks_desde_stat(st),
            "mezcla": job.get("mezcla", "auto"), "completed_at": int(time.time())}
    try:
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(dato, f, ensure_ascii=False, sort_keys=True)
            f.flush(); os.fsync(f.fileno())
        os.replace(tmp, dst)
    finally:
        try: os.remove(tmp)
        except OSError: pass


def _finaliza_cache(job_path, listos, h, job, pipeline, dst, cache):
    """Cierra los tres archivos del protocolo; si el disco falla, conserva el job para reparar."""
    try:
        _escribe_meta(listos, h, job, pipeline)
    except Exception as e:
        _log(cache, "cache lista pero no pude publicar metadata %s: %s" % (h, e))
        return DEFERIDO
    if not _quita_error(listos, h):
        _log(cache, "cache lista pero no pude retirar el error anterior: " + h)
        return DEFERIDO
    if not _quita_requerido(job_path):
        _log(cache, "cache lista pero no pude retirar el trabajo: " + h)
        return DEFERIDO
    return dst

def procesar_uno(job_path, listos, proc, ffmpeg, pipeline, ffprobe, seg=10.0):
    """Procesa UN trabajo. El resultado queda en cache/listos con el MISMO hash del .job:
      <hash>.wav   version mejorada (Apollo + mezcla), IEEE float32
      <hash>.orig  marca de "esta cancion YA es de calidad completa": la rocola usa el ORIGINAL
    Los temporales van a cache/tmp (MISMO disco del release), nunca a C:\Temp."""
    try:
        if os.path.getsize(job_path) > 64 * 1024:
            raise ValueError("archivo .job demasiado grande")
        with open(job_path, encoding="utf-8-sig") as f: job = json.load(f)
    except Exception as e:
        h = os.path.splitext(os.path.basename(job_path))[0].lower()
        persistido = False
        if len(h) == 16 and all(c in "0123456789abcdef" for c in h):
            try:
                _guarda_error(listos, h, MAX_REINTENTOS, "trabajo ilegible: " + str(e), True)
                persistido = True
            except Exception: pass
        if persistido or len(h) != 16 or any(c not in "0123456789abcdef" for c in h):
            _quita(job_path)
            return None
        return DEFERIDO
    h = os.path.splitext(os.path.basename(job_path))[0]
    cache = os.path.dirname(listos)
    try:
        src, mez, _ = _valida_job(job, h, pipeline)
    except Exception as e:
        _log(cache, "trabajo rechazado %s: %s" % (os.path.basename(job_path), e))
        try:
            _guarda_error(listos, h, MAX_REINTENTOS, "trabajo rechazado: " + str(e), True)
        except Exception:
            return DEFERIDO
        _quita(job_path)
        return None
    ya = _en_cache(listos, h)
    if ya:                                         # completa una publicacion interrumpida
        if not ya.endswith(".orig") and _duracion(ffprobe, ya) is None:
            if not _quita_requerido(ya):
                return DEFERIDO
            ya = None
        else:
            return _finaliza_cache(job_path, listos, h, job, pipeline, ya, cache)
    anterior = _lee_error(listos, h)
    if anterior.get("terminal"):
        _quita(job_path)
        return None
    try: retry_at = float(anterior.get("retry_at", 0))
    except (TypeError, ValueError): retry_at = 0
    if retry_at > time.time():
        return DEFERIDO
    # Los trabajos diferidos/listos/rechazados no necesitan tocar la carpeta temporal.
    tmpdir = os.path.join(os.path.dirname(listos), "tmp")
    try: os.makedirs(tmpdir, exist_ok=True)
    except OSError: tmpdir = os.path.dirname(listos)
    tmp = os.path.join(tmpdir, "ia_dec_%s.wav" % h)
    out = os.path.join(tmpdir, "ia_out_%s.wav" % h)
    try:
        dur = _duracion(ffprobe, src)
        if dur is None:
            raise RuntimeError("ffprobe no pudo leer una duracion de audio valida")
        if dur > MAX_SEG:
            motivo = "dura %.0f min; el tope de la IA es %d" % (dur / 60.0, MAX_SEG // 60)
            _log(cache, "SIN IA (queda el original): %s -> %s" % (os.path.basename(src or "?"), motivo))
            dst = _marca_original(listos, h, motivo)
            return _finaliza_cache(job_path, listos, h, job, pipeline, dst, cache)
        _run([ffmpeg, "-nostdin", "-y", "-v", "error", "-t", str(MAX_SEG + 30), "-i", src,
              "-ac", "2", "-ar", str(FS), "-c:a", "pcm_f32le", tmp], TIMEOUT_DECODE)
        x, _ = leer_wav(tmp)
        if len(x) > (MAX_SEG + 31) * FS:
            raise RuntimeError("la decodificacion excedio el limite de duracion")
        cliff = acantilado_hz(x)
        if cliff >= 18500:                         # ya buena: solo la MARCA (bytes, no megas)
            dst = _marca_original(listos, h, str(int(cliff)))
        else:
            y = por_trozos(x, proc, seg=seg)
            m = mezcla_auto(cliff) if mez == "auto" else max(0.0, min(1.0, float(mez) / 100.0))
            escribir_wav_float(out, mezclar(y, x, m, optimizar=True))
            dst = _a_cache(listos, h, out, ffmpeg)
            if _duracion(ffprobe, dst) is None:
                if not _quita_requerido(dst):
                    raise RuntimeError("ffprobe rechazo la salida y no se pudo retirarla")
                raise RuntimeError("ffprobe rechazo la salida generada")
        return _finaliza_cache(job_path, listos, h, job, pipeline, dst, cache)
    except Exception as e:
        try: intentos = int(anterior.get("attempts", 0)) + 1
        except (TypeError, ValueError): intentos = 1
        if intentos >= MAX_REINTENTOS:
            motivo = "IA fallo %d veces; usa original: %s" % (intentos, str(e)[:500])
            _log(cache, motivo + " (" + os.path.basename(src) + ")")
            for ext in (".mp3", ".wav"):
                previo = os.path.join(listos, h + ext)
                if os.path.exists(previo) and not _quita_requerido(previo):
                    _log(cache, "no pude retirar una salida fallida antes de marcar original: " + previo)
                    return DEFERIDO
            dst = _marca_original(listos, h, motivo)
            return _finaliza_cache(job_path, listos, h, job, pipeline, dst, cache)
        try:
            _guarda_error(listos, h, intentos, e)
        except Exception:
            pass
        _log(cache, "fallo %d/%d; reintento con espera: %s" % (intentos, MAX_REINTENTOS, e))
        return DEFERIDO
    finally:
        for basura in (tmp, out):
            try: os.remove(basura)
            except OSError: pass

def _quita(p):
    try: os.remove(p)
    except OSError: pass


def _quita_requerido(p):
    try:
        os.remove(p)
        return True
    except FileNotFoundError:
        return True
    except OSError:
        return False

def evictar(listos, tope_gb):
    """Si la cache pasa del tope, borra los archivos MENOS usados recientemente (por fecha de
    acceso). Cubre .mp3 (cache nueva) y .wav (cache vieja); las marcas .orig pesan bytes."""
    tope = tope_gb * (1024 ** 3)
    arch = []
    total = 0
    # Una sola instantanea de metadatos por archivo. No seguir enlaces ni contar directorios.
    with os.scandir(listos) as entries:
        for entry in entries:
            if not entry.name.endswith((".mp3", ".wav")):
                continue
            try:
                st = entry.stat(follow_symlinks=False)
            except OSError:
                continue   # pudo desaparecer durante la enumeracion
            if not stat.S_ISREG(st.st_mode):
                continue
            arch.append((entry.path, st.st_size, st.st_atime))
            total += st.st_size
    if total <= tope: return
    arch.sort(key=lambda a: a[2])   # mas viejo (menos usado) primero; conserva desempates
    for a, size, _ in arch:
        if total <= tope: break
        try:
            os.remove(a)
        except FileNotFoundError:
            pass   # ya no ocupa espacio aunque lo retirara otro proceso
        except OSError:
            continue   # un archivo bloqueado sigue ocupando su espacio
        total -= size

def _un_ciclo_log(pend, listos, proc, ffmpeg, pipeline, ffprobe, tope_gb, cache, seg=10.0):
    jobs = []
    try:
        with os.scandir(pend) as entries:
            for entry in entries:
                # Mismo patron y orden de antiguedad de glob; no abrir JSON para ordenar.
                if entry.name.startswith(".") or not fnmatch.fnmatch(entry.name, "*.job"):
                    continue
                try:
                    jobs.append((entry.path, entry.stat().st_mtime))
                except OSError:
                    continue  # retirado durante la enumeracion: no bloquea el resto
    except FileNotFoundError:
        return 0
    jobs.sort(key=lambda item: item[1])
    atendidos = 0
    for j, _ in jobs:
        r = procesar_uno(j, listos, proc, ffmpeg, pipeline, ffprobe, seg)
        if r is DEFERIDO:
            continue
        atendidos += 1
        _log(cache, ("procesada -> " + os.path.basename(r)) if r else
             ("trabajo descartado: " + os.path.basename(j)))
    if atendidos: evictar(listos, tope_gb)
    return atendidos


def _publica_atomico(path, contenido):
    tmp = path + ".tmp"
    try:
        with open(tmp, "w", encoding="utf-8", newline="\n") as f:
            f.write(contenido)
            f.flush(); os.fsync(f.fileno())
        os.replace(tmp, path)
    finally:
        try: os.remove(tmp)
        except OSError: pass

def main():
    # El motor corre DEBAJO de todo: si Apollo exprime el CPU/GPU, la rocola y Windows respiran
    # primero (el trabajo de IA solo tarda un poco mas; la maquina nunca se debe sentir tomada).
    if os.name == "nt":
        try:
            import ctypes
            ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x00004000)   # BELOW_NORMAL
        except Exception: pass
    ap = argparse.ArgumentParser()
    ap.add_argument("--cache", default=os.path.join(AQUI, "cache"))
    ap.add_argument("--tope_gb", type=float, default=20.0)
    ap.add_argument("--seg", type=float, default=10.0)
    ap.add_argument("--gpu", default="auto")   # que GPU usa Apollo (auto | 0 | 1 | ...); lo manda la rocola (Ctrl+F3)
    ap.add_argument("--apollo", default=os.environ.get("APOLLO_DIR"))
    a = ap.parse_args()
    if not math.isfinite(a.seg) or a.seg <= 0.5:
        print("[X] --seg debe ser mayor que el solape de 0.5 segundos"); return 2
    if not math.isfinite(a.tope_gb) or a.tope_gb <= 0:
        print("[X] --tope_gb debe ser mayor que cero"); return 2
    a.cache = os.path.abspath(a.cache)
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg: print("[X] Falta ffmpeg"); return 1
    ffprobe = _ffprobe_de(ffmpeg)
    if not ffprobe: print("[X] Falta ffprobe junto a ffmpeg"); return 1
    try:
        _verificar_herramienta(ffmpeg, "ffmpeg")
        _verificar_herramienta(ffprobe, "ffprobe")
    except RuntimeError as e:
        print("[X]", e); return 1
    pend, listos = _dirs(a.cache)
    candado = _candado(a.cache)          # NO se suelta nunca: vive lo que el proceso
    if candado is None:
        _log(a.cache, "ya hay un motor con el candado; salgo para no duplicar")
        print("Ya hay un motor corriendo con esta cache; este se cierra."); return 0
    _activar_job_hijos()
    # Una huella vieja no debe generar trabajos mientras se carga un modelo nuevo.
    _quita(os.path.join(a.cache, "modelo.fingerprint"))
    _vivo_bomba(a.cache)                 # latido desde YA, aun sin Apollo cargado
    _log(a.cache, "arrancando motor; cache=" + a.cache)
    print("Motor de IA: cargando Apollo en la GPU (una vez)...")
    try:
        import modelo_apollo
        proc = modelo_apollo.cargar(gpu=a.gpu, apollo_dir=a.apollo)
    except Exception as e:
        _log(a.cache, "ERROR cargando Apollo: " + repr(e))
        print("[X] No se pudo cargar Apollo:", e)
        return 1
    pipeline = huella_pipeline(proc.model_fingerprint, a.seg)
    try:
        # publica las GPUs detectadas para el selector de la rocola (nombres REALES de torch)
        _publica_atomico(os.path.join(a.cache, "gpus.txt"), "\n".join(modelo_apollo.lista_gpus()))
        _publica_atomico(os.path.join(a.cache, "modelo.fingerprint"), pipeline + "\n")
    except Exception:
        _log(a.cache, "ERROR publicando la huella del modelo")
        return 1
    _log(a.cache, "Apollo LISTO (gpu=%s, pipeline=%s)" % (a.gpu, pipeline))
    print("Motor de IA LISTO. Escuchando trabajos en:", pend)
    print("Deja esta ventana ABIERTA mientras uses la rocola con IA en vivo.")
    while True:
        try:
            hechos = _un_ciclo_log(pend, listos, proc, ffmpeg, pipeline, ffprobe,
                                   a.tope_gb, a.cache, a.seg)
            time.sleep(0.2 if hechos else 0.6)     # atiende rapido si hay cola; descansa si no
        except KeyboardInterrupt:
            print("\nMotor detenido."); return 0
        except Exception as e:
            _log(a.cache, "aviso del ciclo: " + repr(e))   # bajo pythonw print va a la nada; esto SI se ve
            print("aviso:", e); time.sleep(1.0)

if __name__ == "__main__":
    sys.exit(main())
