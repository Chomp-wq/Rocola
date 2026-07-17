# -*- coding: utf-8 -*-
# ============================================================================================
#  MOTOR DE IA EN TIEMPO REAL (rocola-hd): mantiene Apollo cargado en la GPU y procesa canciones
#  BAJO DEMANDA. La rocola (C#) pide trabajos dejando un archivo .job; el motor los procesa y
#  deja el resultado en cache/listos/<hash>.mp3. La rocola reproduce ese mp3 si existe; si no,
#  reproduce el original (nunca se bloquea). Con cache: cada cancion se procesa UNA vez.
#  La MUSICA ORIGINAL NUNCA SE TOCA: todo vive en la carpeta cache del motor, con tope de tamano.
#
#  Protocolo (carpeta, sin sockets ni dependencias raras -> robusto y facil de depurar):
#    cache/pendientes/<hash>.job   (JSON: {"src": ruta, "mezcla": "auto"|"0".."100"})  <- lo deja la rocola
#    cache/listos/<hash>.mp3       version mejorada (MP3 320: pesa ~5 veces menos que WAV) <- motor
#    cache/listos/<hash>.orig      marca "ya era de calidad completa; usa el ORIGINAL"     <- motor
#    cache/listos/<hash>.error                                                             <- si algo fallo
#  El HASH lo calculan IGUAL la rocola y el motor: sha1( ruta_minusculas | tamano_bytes | mezcla )[:16].
#  Usa el TAMANO del archivo (no la fecha) para que C# y Python coincidan exacto.
#
#    python daemon_ia.py [--cache <carpeta>] [--tope_gb 20]
# ============================================================================================
import sys, os, json, time, glob, hashlib, subprocess, argparse, shutil
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
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "..", "upscale"))
from util_audio import FS, leer_wav, escribir_wav, acantilado_hz, por_trozos, mezclar, mezcla_auto

def hash_trabajo(src, mezcla):
    try: size = os.path.getsize(src)
    except OSError: size = 0
    clave = (os.path.abspath(src).lower() + "|" + str(size) + "|" + str(mezcla)).encode("utf-8", "replace")
    return hashlib.sha1(clave).hexdigest()[:16]

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
        r = subprocess.run([ffprobe, "-v", "error", "-show_entries", "format=duration",
                            "-of", "csv=p=0", src], capture_output=True, text=True,
                           timeout=30, creationflags=NOWIN)
        if r.returncode != 0: return None
        return float(r.stdout.strip())
    except Exception:
        return None

def _marca_original(listos, h, motivo):
    """Deja la marca <hash>.orig: la rocola reproduce el ORIGINAL y no vuelve a pedir el trabajo.
    (La misma marca que 'ya era de calidad completa'; aqui el motivo es que no conviene procesarla.)"""
    dst = os.path.join(listos, h + ".orig")
    try:
        with open(dst + ".tmp", "w", encoding="utf-8") as f: f.write(motivo)
        os.replace(dst + ".tmp", dst)
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
            try: open(vivo, "w").write(str(int(time.time())))
            except OSError: pass
            time.sleep(3.0)
    threading.Thread(target=bomba, daemon=True).start()

def _dirs(cache):
    p = os.path.join(cache, "pendientes"); l = os.path.join(cache, "listos")
    os.makedirs(p, exist_ok=True); os.makedirs(l, exist_ok=True)
    return p, l

def _en_cache(listos, h):
    """Lo que ya haya en cache para este hash: .mp3 (nuevo), .wav (cache vieja) o .orig (marca)."""
    for ext in (".mp3", ".wav", ".orig"):
        p = os.path.join(listos, h + ext)
        if os.path.exists(p): return p
    return None

def _a_cache(listos, h, wav, ffmpeg):
    """Guarda el resultado como MP3 320 kbps: a esa tasa el oido no lo distingue del WAV y pesa
    ~5 veces menos (caben ~5x mas canciones en el mismo tope). Si el encode fallara (ffmpeg sin
    libmp3lame, raro), cae a copiar el WAV tal cual. Aparicion atomica en ambos casos."""
    dst = os.path.join(listos, h + ".mp3"); t = dst + ".t.mp3"
    try:
        subprocess.run([ffmpeg, "-y", "-v", "error", "-i", wav, "-codec:a", "libmp3lame",
                        "-b:a", "320k", t], check=True, stdout=subprocess.DEVNULL,
                       stderr=subprocess.DEVNULL, creationflags=NOWIN)
        os.replace(t, dst)
        return dst
    except Exception:
        try: os.remove(t)
        except OSError: pass
        dst = os.path.join(listos, h + ".wav")
        shutil.copyfile(wav, dst + ".tmp"); os.replace(dst + ".tmp", dst)
        return dst

def procesar_uno(job_path, listos, proc, ffmpeg, seg=10.0):
    """Procesa UN trabajo. El resultado queda en cache/listos con el MISMO hash del .job:
      <hash>.mp3   version mejorada (Apollo + mezcla), en MP3 320
      <hash>.orig  marca de "esta cancion YA es de calidad completa": la rocola usa el ORIGINAL
    Los temporales van a cache/tmp (MISMO disco del release), nunca a C:\Temp."""
    tmpdir = os.path.join(os.path.dirname(listos), "tmp")
    try: os.makedirs(tmpdir, exist_ok=True)
    except OSError: tmpdir = os.path.dirname(listos)
    try:
        with open(job_path, encoding="utf-8") as f: job = json.load(f)
    except Exception:
        try: os.remove(job_path)
        except OSError: pass
        return None
    h = os.path.splitext(os.path.basename(job_path))[0]
    ya = _en_cache(listos, h)
    if ya:                                         # ya en cache: nada que hacer
        _quita(job_path); return ya
    src = job.get("src"); mez = job.get("mezcla", "auto")
    cache = os.path.dirname(listos)
    ffprobe = _ffprobe_de(ffmpeg)
    if ffprobe:
        dur = _duracion(ffprobe, src)
        if dur is None or dur > MAX_SEG:
            motivo = ("no se pudo leer la duracion (archivo sospechoso)" if dur is None
                      else "dura %.0f min; el tope de la IA es %d" % (dur / 60.0, MAX_SEG // 60))
            _log(cache, "SIN IA (queda el original): %s -> %s" % (os.path.basename(src or "?"), motivo))
            dst = _marca_original(listos, h, motivo)
            _quita(job_path)
            return dst
    tmp = os.path.join(tmpdir, "ia_dec_%s.wav" % h)
    out = os.path.join(tmpdir, "ia_out_%s.wav" % h)
    try:
        subprocess.run([ffmpeg, "-y", "-v", "error", "-t", str(MAX_SEG + 30), "-i", src,
                        "-ac", "2", "-ar", str(FS), "-sample_fmt", "s16", tmp],
                       check=True, stdout=subprocess.DEVNULL,
                       stderr=subprocess.DEVNULL, creationflags=NOWIN)
        x, _ = leer_wav(tmp)
        cliff = acantilado_hz(x)
        if cliff >= 18500:                         # ya buena: solo la MARCA (bytes, no megas)
            dst = os.path.join(listos, h + ".orig")
            with open(dst + ".tmp", "w") as f: f.write(str(int(cliff)))
            os.replace(dst + ".tmp", dst)          # atomico: la rocola nunca ve archivos a medias
        else:
            y = por_trozos(x, proc, seg=seg)
            m = mezcla_auto(cliff) if mez == "auto" else max(0.0, min(1.0, float(mez) / 100.0))
            escribir_wav(out, mezclar(y, x, m))
            dst = _a_cache(listos, h, out, ffmpeg)
        _quita(job_path)
        return dst
    except Exception as e:
        try: open(os.path.join(listos, h + ".error"), "w", encoding="utf-8").write(str(e))
        except OSError: pass
        _quita(job_path)
        return None
    finally:
        for basura in (tmp, out):
            try: os.remove(basura)
            except OSError: pass

def _quita(p):
    try: os.remove(p)
    except OSError: pass

def evictar(listos, tope_gb):
    """Si la cache pasa del tope, borra los archivos MENOS usados recientemente (por fecha de
    acceso). Cubre .mp3 (cache nueva) y .wav (cache vieja); las marcas .orig pesan bytes."""
    tope = tope_gb * (1024 ** 3)
    arch = [os.path.join(listos, f) for f in os.listdir(listos)
            if f.endswith(".mp3") or f.endswith(".wav")]
    total = 0
    for a in arch:
        try: total += os.path.getsize(a)
        except OSError: pass
    if total <= tope: return
    arch.sort(key=lambda a: os.path.getatime(a))   # mas viejo (menos usado) primero
    for a in arch:
        if total <= tope: break
        try: total -= os.path.getsize(a); os.remove(a)
        except OSError: pass

def _un_ciclo_log(pend, listos, proc, ffmpeg, tope_gb, cache, seg=10.0):
    jobs = sorted(glob.glob(os.path.join(pend, "*.job")), key=lambda p: os.path.getmtime(p))
    for j in jobs:
        r = procesar_uno(j, listos, proc, ffmpeg, seg)
        _log(cache, ("procesada -> " + os.path.basename(r)) if r else ("fallo un trabajo: " + os.path.basename(j)))
    if jobs: evictar(listos, tope_gb)
    return len(jobs)

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
    a = ap.parse_args()
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg: print("[X] Falta ffmpeg"); return 1
    pend, listos = _dirs(a.cache)
    candado = _candado(a.cache)          # NO se suelta nunca: vive lo que el proceso
    if candado is None:
        _log(a.cache, "ya hay un motor con el candado; salgo para no duplicar")
        print("Ya hay un motor corriendo con esta cache; este se cierra."); return 0
    _vivo_bomba(a.cache)                 # latido desde YA, aun sin Apollo cargado
    _log(a.cache, "arrancando motor; cache=" + a.cache)
    print("Motor de IA: cargando Apollo en la GPU (una vez)...")
    try:
        import modelo_apollo
        proc = modelo_apollo.cargar(gpu=a.gpu)
    except Exception as e:
        _log(a.cache, "ERROR cargando Apollo: " + repr(e))
        print("[X] No se pudo cargar Apollo:", e)
        raise
    try:
        # publica las GPUs detectadas para el selector de la rocola (nombres REALES de torch)
        with open(os.path.join(a.cache, "gpus.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(modelo_apollo.lista_gpus()))
    except Exception:
        pass
    _log(a.cache, "Apollo LISTO (gpu=%s)" % a.gpu)
    print("Motor de IA LISTO. Escuchando trabajos en:", pend)
    print("Deja esta ventana ABIERTA mientras uses la rocola con IA en vivo.")
    while True:
        try:
            hechos = _un_ciclo_log(pend, listos, proc, ffmpeg, a.tope_gb, a.cache, a.seg)
            time.sleep(0.2 if hechos else 0.6)     # atiende rapido si hay cola; descansa si no
        except KeyboardInterrupt:
            print("\nMotor detenido."); return 0
        except Exception as e:
            _log(a.cache, "aviso del ciclo: " + repr(e))   # bajo pythonw print va a la nada; esto SI se ve
            print("aviso:", e); time.sleep(1.0)

if __name__ == "__main__":
    sys.exit(main())
