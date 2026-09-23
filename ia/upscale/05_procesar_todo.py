# -*- coding: utf-8 -*-
"""Procesa una biblioteca completa sin tocar ni escapar de sus dos raices."""
import argparse
import csv
import hashlib
import json
import math
import os
import shutil
import subprocess
import sys
import tempfile
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from util_audio import FS, leer_wav, escribir_wav, acantilado_hz, por_trozos, mezcla_auto
from util_audio import REMUESTREO_HEREDADO, remuestreador, args_decodificar, sondeo_audio, sin_remuestreo
from memoria_audio import MemoriaAudio

UMBRAL_BUENA = 18500.0
EXTS = (".mp3", ".wav", ".flac", ".m4a", ".ogg", ".wma")
FORMATOS_ROCOLA = ("mp3", "wav")
MANIFEST_VERSION = 3
TIMEOUT_FFMPEG = 2 * 60 * 60


def _real(path):
    return os.path.realpath(os.path.abspath(path))


def _canon(path):
    """Clave de comparacion; _real conserva las mayusculas para nombres de salida."""
    return os.path.normcase(_real(path))


def _dentro(path, raiz):
    try:
        return os.path.commonpath((_canon(path), _canon(raiz))) == _canon(raiz)
    except ValueError:
        return False


def validar_raices(raiz, salida):
    raiz, salida = _real(raiz), _real(salida)
    if not os.path.isdir(raiz):
        raise ValueError("La carpeta de musica no existe: " + raiz)
    # Cualquier solape puede provocar recursion o que una salida sustituya al original.
    if _dentro(salida, raiz) or _dentro(raiz, salida):
        raise ValueError("Musica y salida deben ser carpetas separadas, sin estar una dentro de la otra")
    return raiz, salida


def _es_audio_seguro(path, raiz):
    return (_dentro(path, raiz) and os.path.isfile(path) and
            os.path.splitext(path)[1].lower() in EXTS)


def _escanear(raiz):
    rutas = []
    dirs_vistos = {_canon(raiz)}
    def error_walk(e):
        raise e
    for base, dirs, files in os.walk(raiz, onerror=error_walk):
        # No atravesar enlaces/junctions ni volver a una carpeta ya vista: los junctions de
        # Windows no siempre son reportados por islink y pueden formar ciclos infinitos.
        seguros = []
        for d in sorted(dirs, key=str.lower):
            candidato = os.path.join(base, d)
            real = _canon(candidato)
            if os.path.islink(candidato) or not _dentro(real, raiz) or real in dirs_vistos:
                continue
            dirs_vistos.add(real)
            seguros.append(d)
        dirs[:] = seguros
        for nombre in sorted(files, key=str.lower):
            ruta = os.path.join(base, nombre)
            if _es_audio_seguro(ruta, raiz):
                rutas.append(_real(ruta))
    return rutas


def orden_desde_csv(csv_path, raiz):
    """Prioriza el CSV, pero siempre agrega canciones nuevas encontradas en la raiz."""
    raiz = _canon(raiz)
    todas = _escanear(raiz)
    prioridad = {}
    if csv_path and os.path.isfile(csv_path):
        with open(csv_path, newline="", encoding="utf-8-sig") as f:
            lector = csv.reader(f, delimiter=";")
            next(lector, None)
            for fila in lector:
                if len(fila) < 3:
                    continue
                try:
                    hz = float(fila[0])
                except (TypeError, ValueError):
                    continue
                ruta = fila[2]
                if not os.path.isabs(ruta):
                    ruta = os.path.join(raiz, ruta)
                ruta = _canon(ruta)
                if _es_audio_seguro(ruta, raiz):
                    prioridad[ruta] = hz if hz > 0 else 99999.0
    return sorted(todas, key=lambda r: ((0, prioridad[_canon(r)], _canon(r))
                                        if _canon(r) in prioridad else (1, 0, _canon(r))))


def _destino_base(raiz, salida, src, formato):
    if formato not in FORMATOS_ROCOLA:
        raise ValueError("Formato no compatible con Rocola: " + str(formato))
    rel = os.path.relpath(_real(src), _real(raiz))
    if rel == os.pardir or rel.startswith(os.pardir + os.sep) or os.path.isabs(rel):
        raise ValueError("La entrada esta fuera de la biblioteca: " + src)
    return os.path.join(_real(salida), os.path.splitext(rel)[0] + "." + formato)


def mapa_destinos(raiz, salida, rutas, formato):
    """Resuelve colisiones de igual basename (mp3/flac/etc.) de forma determinista."""
    deseados = {}
    conteos = {}
    for src in rutas:
        dst = _destino_base(raiz, salida, src, formato)
        clave = os.path.normcase(dst).replace("\\", "/").casefold()
        deseados[_canon(src)] = dst
        conteos[clave] = conteos.get(clave, 0) + 1
    mapa = {}
    usados = set()
    for src in sorted(deseados):
        dst = deseados[src]
        clave = os.path.normcase(dst).replace("\\", "/").casefold()
        if conteos[clave] > 1 or clave in usados:
            rel = os.path.relpath(src, _canon(raiz)).replace("\\", "/")
            digest = hashlib.sha256(rel.encode("utf-8")).hexdigest()
            base = os.path.splitext(dst)[0]
            for ancho in range(10, 65, 2):
                candidato = base + "__" + digest[:ancho] + "." + formato
                clave_cand = os.path.normcase(candidato).replace("\\", "/").casefold()
                if clave_cand not in usados:
                    dst, clave = candidato, clave_cand
                    break
            else:  # practicamente imposible, pero no se permite pisar ni con colision SHA-256.
                n = 2
                while True:
                    candidato = base + "__" + digest + "_%d.%s" % (n, formato)
                    clave_cand = os.path.normcase(candidato).replace("\\", "/").casefold()
                    if clave_cand not in usados:
                        dst, clave = candidato, clave_cand
                        break
                    n += 1
        mapa[src] = dst
        usados.add(clave)
    for dst in mapa.values():
        if not _dentro(dst, salida):
            raise ValueError("Destino fuera de la salida: " + dst)
    return mapa


def _ffprobe_de(ffmpeg):
    nombre = "ffprobe.exe" if os.name == "nt" else "ffprobe"
    junto = os.path.join(os.path.dirname(ffmpeg), nombre)
    return junto if os.path.isfile(junto) else shutil.which("ffprobe")


def _run(cmd, timeout=TIMEOUT_FFMPEG):
    try:
        return subprocess.run(cmd, check=True, stdin=subprocess.DEVNULL,
                              stdout=subprocess.DEVNULL, stderr=subprocess.PIPE,
                              timeout=timeout)
    except subprocess.TimeoutExpired:
        raise RuntimeError("el proceso externo excedio el tiempo limite de %ds" % timeout)
    except subprocess.CalledProcessError as e:
        detalle = (e.stderr or b"").decode("utf-8", "replace").strip()[-2000:]
        raise RuntimeError("fallo el proceso externo%s" % ((": " + detalle) if detalle else ""))


def _verificar_herramienta(path, nombre):
    try:
        subprocess.run([path, "-version"], check=True, stdin=subprocess.DEVNULL,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=15)
    except Exception as e:
        raise RuntimeError("%s no funciona: %s" % (nombre, e))


def _salida_valida(ffprobe, path):
    if not os.path.isfile(path) or os.path.getsize(path) <= 44:
        return False
    try:
        r = subprocess.run([ffprobe, "-v", "error", "-select_streams", "a:0",
                            "-show_entries", "stream=index:format=duration", "-of", "json", path],
                           check=True, stdin=subprocess.DEVNULL, capture_output=True,
                           text=True, timeout=30)
        dato = json.loads(r.stdout)
        dur = float(dato.get("format", {}).get("duration", 0))
        return bool(dato.get("streams")) and math.isfinite(dur) and dur > 0
    except Exception:
        return False


def _encode_desde_wav(ffmpeg, wav, original, dst, formato, calidad):
    tmp = dst + ".tmp." + formato
    cmd = [ffmpeg, "-nostdin", "-y", "-v", "error", "-i", wav, "-i", original,
           "-map", "0:a:0", "-map_metadata", "1"]
    if formato == "mp3":
        cmd += ["-map", "1:v?", "-c:v", "copy", "-c:a", "libmp3lame",
                "-b:a", "%dk" % calidad, "-id3v2_version", "3"]
    else:
        cmd += ["-vn", "-c:a", "pcm_s16le"]
    cmd.append(tmp)
    try:
        _run(cmd)
        os.replace(tmp, dst)
    finally:
        try:
            os.remove(tmp)
        except OSError:
            pass


def _copiar_o_convertir_buena(ffmpeg, src, dst, formato, calidad):
    tmp = dst + ".tmp." + formato
    try:
        if os.path.splitext(src)[1].lower() == "." + formato:
            shutil.copy2(src, tmp)
        else:
            cmd = [ffmpeg, "-nostdin", "-y", "-v", "error", "-i", src,
                   "-map", "0:a:0", "-map_metadata", "0"]
            if formato == "mp3":
                cmd += ["-map", "0:v?", "-c:v", "copy", "-c:a", "libmp3lame",
                        "-b:a", "%dk" % calidad, "-id3v2_version", "3"]
            else:
                cmd += ["-vn", "-c:a", "pcm_s16le"]
            cmd.append(tmp)
            _run(cmd)
        os.replace(tmp, dst)
    finally:
        try:
            os.remove(tmp)
        except OSError:
            pass


def _identidad_entrada(path):
    st = os.stat(path)
    return {"path": _canon(path), "size": st.st_size, "mtime_ns": st.st_mtime_ns}


def _identidad_salida(path):
    st = os.stat(path)
    return {"path": _canon(path), "size": st.st_size, "mtime_ns": st.st_mtime_ns}


def cargar_manifest(path):
    entradas = {}
    if not os.path.isfile(path):
        return entradas
    with open(path, encoding="utf-8", errors="replace") as f:
        for linea in f:
            try:
                dato = json.loads(linea)
                if (isinstance(dato, dict) and dato.get("version") == MANIFEST_VERSION and
                        isinstance(dato.get("input"), dict)):
                    entradas[dato["input"]["path"]] = dato
            except (ValueError, KeyError, TypeError):
                continue
    return entradas


def entrada_completa(registro, src, dst, parametros):
    try:
        return (registro["input"] == _identidad_entrada(src) and
                registro["params"] == parametros and
                registro["output"] == _identidad_salida(dst) and
                registro["output"]["size"] > 44)
    except (KeyError, OSError, TypeError, ValueError):
        return False


SIN_REMUESTREO = "ninguno"


def remuestreo_de(frecuencias, remuestreo):
    """Lo que se apunta de ESTA cancion: el remuestreador solo cuenta si de verdad interviene."""
    return SIN_REMUESTREO if sin_remuestreo(frecuencias) else remuestreo


def estado_entrada(registro, src, dst, parametros, remuestreo, frecuencias_de):
    """``(completa, refresco)`` de una entrada del manifiesto con el remuestreador de hoy.

    ``frecuencias_de(src)`` solo se llama para entradas de antes de apuntar el remuestreo; las que
    siguen valiendo se devuelven con ``refresco`` (los parametros que conviene reescribir para no
    volver a preguntar). Una cancion a 44,1 kHz no cambia con ningun remuestreador; una a otra
    frecuencia hecha con otro remuestreador se vuelve a procesar."""
    try:
        previos = registro["params"]
    except (KeyError, TypeError):
        return (False, None)
    if not isinstance(previos, dict):
        return (False, None)
    if "remuestreo" in previos:
        usado = previos["remuestreo"]
        vale = usado == SIN_REMUESTREO or usado == remuestreo
        return (vale and entrada_completa(registro, src, dst, dict(parametros, remuestreo=usado)), None)
    if not entrada_completa(registro, src, dst, parametros):
        return (False, None)
    usado = remuestreo_de(frecuencias_de(src), REMUESTREO_HEREDADO)
    if usado == SIN_REMUESTREO or remuestreo == REMUESTREO_HEREDADO:
        return (True, dict(parametros, remuestreo=usado))
    return (False, None)


def _anota_manifest(f, src, dst, parametros, identidad_entrada=None):
    dato = {"version": MANIFEST_VERSION, "input": identidad_entrada or _identidad_entrada(src),
            "params": parametros, "output": _identidad_salida(dst),
            "completed_at": int(time.time())}
    f.write(json.dumps(dato, ensure_ascii=False, sort_keys=True) + "\n")
    f.flush()
    os.fsync(f.fileno())


def _repara_ultima_linea(path):
    """Descarta solo el ultimo JSON incompleto dejado por un corte de luz."""
    if not os.path.isfile(path) or os.path.getsize(path) == 0:
        return
    with open(path, "rb+") as f:
        f.seek(-1, os.SEEK_END)
        if f.read(1) == b"\n":
            return
        pos = f.tell() - 1
        while pos > 0:
            paso = min(64 * 1024, pos)
            pos -= paso
            f.seek(pos)
            bloque = f.read(paso)
            ultimo = bloque.rfind(b"\n")
            if ultimo >= 0:
                f.truncate(pos + ultimo + 1)
                f.flush(); os.fsync(f.fileno())
                return
        f.seek(0); f.truncate(0); f.flush(); os.fsync(f.fileno())


def _candado_salida(salida):
    """Impide que dos conversiones escriban el mismo manifiesto/salida a la vez."""
    p = os.path.join(salida, "_procesando.lock")
    f = open(p, "a+")
    try:
        if os.path.getsize(p) == 0:
            f.write("0"); f.flush()
        if os.name == "nt":
            import msvcrt
            f.seek(0); msvcrt.locking(f.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(f, fcntl.LOCK_EX | fcntl.LOCK_NB)
        return f
    except OSError:
        f.close()
        return None


def _elige_carpeta(titulo):
    try:
        import tkinter
        from tkinter import filedialog
        root = tkinter.Tk(); root.withdraw()
        ruta = filedialog.askdirectory(title=titulo, mustexist=True)
        root.destroy()
        return ruta
    except Exception as e:
        raise ValueError("No pude abrir el selector de carpetas: %s" % e)


def resumen_final(salida, fallas):
    if fallas:
        print("\nINCOMPLETO: %d archivos fallaron. Revisa _errores.txt y vuelve a ejecutar para reintentar." % fallas)
        print("Salidas verificadas disponibles en:", salida)
        return 1
    print("\nLISTO. Todas las salidas de esta tanda fueron verificadas en:", salida)
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--musica")
    ap.add_argument("--salida")
    ap.add_argument("--csv", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "calidad_biblioteca.csv"))
    ap.add_argument("--formato", choices=FORMATOS_ROCOLA, default="mp3")
    ap.add_argument("--calidad", type=int, default=320)
    ap.add_argument("--seg", type=float, default=10.0)
    ap.add_argument("--mezcla", default="auto")
    ap.add_argument("--apollo", default=os.environ.get("APOLLO_DIR"))
    ap.add_argument("--interactivo", action="store_true")
    a = ap.parse_args()

    try:
        if not a.musica and a.interactivo:
            a.musica = _elige_carpeta("Selecciona la carpeta de musica ORIGINAL")
        if not a.salida and a.interactivo:
            a.salida = _elige_carpeta("Selecciona la carpeta VACIA de salida")
        if not a.musica or not a.salida:
            raise ValueError("Se requieren --musica y --salida")
        raiz, salida = validar_raices(a.musica, a.salida)
        if not math.isfinite(a.seg) or a.seg <= 0.5:
            raise ValueError("--seg debe ser mayor que el solape de 0.5 segundos")
        if not 64 <= a.calidad <= 320:
            raise ValueError("--calidad debe estar entre 64 y 320 kbps")
        if a.mezcla != "auto":
            mezcla_num = float(a.mezcla)
            if not math.isfinite(mezcla_num) or not 0 <= mezcla_num <= 100:
                raise ValueError("--mezcla debe ser auto o un porcentaje entre 0 y 100")
    except ValueError as e:
        print("[X]", e)
        return 2

    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        print("[X] Falta ffmpeg (corre 1_instalar.bat)")
        return 1
    ffprobe = _ffprobe_de(ffmpeg)
    if not ffprobe:
        print("[X] Falta ffprobe junto a ffmpeg; no se puede verificar la salida")
        return 1
    try:
        _verificar_herramienta(ffmpeg, "ffmpeg")
        _verificar_herramienta(ffprobe, "ffprobe")
    except RuntimeError as e:
        print("[X]", e)
        return 1

    os.makedirs(salida, exist_ok=True)
    candado = _candado_salida(salida)
    if candado is None:
        print("[X] Ya hay otro proceso trabajando en esta misma carpeta de salida")
        return 1
    try:
        return _procesar_carpeta(a, raiz, salida, ffmpeg, ffprobe)
    finally:
        candado.close()


def _procesar_carpeta(a, raiz, salida, ffmpeg, ffprobe):
    rutas = orden_desde_csv(a.csv, raiz)
    if not rutas:
        print("No hay canciones compatibles en", raiz)
        return 1
    destinos = mapa_destinos(raiz, salida, rutas, a.formato)

    print("Resolviendo modelo Apollo y su huella reproducible...")
    import modelo_apollo
    try:
        resuelto = modelo_apollo.resolver_modelo()
        fingerprint = modelo_apollo.identidad_modelo(a.apollo, resuelto)
    except Exception as e:
        print("[X] No se pudo identificar Apollo:", e)
        return 1
    parametros = {"formato": a.formato, "calidad": a.calidad, "seg": a.seg,
                  "mezcla": str(a.mezcla), "modelo": fingerprint,
                  "umbral": UMBRAL_BUENA}

    # El remuestreo a 44,1 kHz: el mejor que ESTE ffmpeg ejecuta de verdad (ver util_audio).
    remuestreo, filtro = remuestreador(ffmpeg)
    print("Remuestreo a 44,1 kHz:", remuestreo)

    manifest_path = os.path.join(salida, "_procesadas.jsonl")
    anteriores = cargar_manifest(manifest_path)
    pendientes, refrescos = [], []
    for r in rutas:
        dst = destinos[_canon(r)]
        completa, refresco = estado_entrada(anteriores.get(_canon(r)), r, dst, parametros, remuestreo,
                                            lambda s: sondeo_audio(ffprobe, s)[1])
        if completa and _salida_valida(ffprobe, dst):
            if refresco is not None:
                refrescos.append((r, dst, refresco))
        else:
            pendientes.append(r)
    if refrescos:
        # Las entradas de antes de apuntar el remuestreo que siguen valiendo quedan apuntadas con el
        # suyo: la proxima vez no hace falta volver a preguntarle a ffprobe por su frecuencia.
        _repara_ultima_linea(manifest_path)
        with open(manifest_path, "a", encoding="utf-8", newline="\n") as regf:
            for r, dst, p in refrescos:
                _anota_manifest(regf, r, dst, p)
    if not pendientes:
        print("Nada pendiente: manifiesto y salidas verificados.")
        return 0
    # La verificacion anterior no necesita pesos en RAM/VRAM. Si hay trabajo, se carga una vez
    # y se comprueba de nuevo la identidad antes de tocar ninguna salida.
    try:
        proc = modelo_apollo.cargar(apollo_dir=a.apollo, modelo_resuelto=resuelto)
        if proc.model_fingerprint != fingerprint:
            raise RuntimeError("El codigo/modelo cambio al cargar Apollo; vuelve a iniciar el proceso")
    except Exception as e:
        print("[X] No se pudo cargar Apollo:", e)
        return 1
    print("Por procesar: %d de %d canciones." % (len(pendientes), len(rutas)))
    errores_path = os.path.join(salida, "_errores.txt")
    t0 = time.time(); mejoradas = 0; copiadas = 0; fallas = 0
    _repara_ultima_linea(manifest_path)
    with open(manifest_path, "a", encoding="utf-8", newline="\n") as regf, \
         open(errores_path, "a", encoding="utf-8", newline="\n") as log:
        for n, src in enumerate(pendientes, 1):
            dst = destinos[_canon(src)]
            if not _dentro(dst, salida):
                raise RuntimeError("se rechazo un destino fuera de la salida: " + dst)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            try: os.remove(dst)  # nunca dejar como vigente una salida vieja de otros parametros/bytes
            except OSError: pass
            try:
                entrada_antes = _identidad_entrada(src)
                fue_mejorada = False
                params_src = dict(parametros, remuestreo=remuestreo_de(sondeo_audio(ffprobe, src)[1], remuestreo))
                with tempfile.TemporaryDirectory(prefix="rocola_hd_") as td:
                    dec = os.path.join(td, "dec.wav")
                    _run([ffmpeg, "-nostdin", "-y", "-v", "error", "-i", src] +
                         args_decodificar(filtro) + [dec])
                    with MemoriaAudio(dec) as memoria:
                        x, _ = leer_wav(dec, reservar=memoria.crear)
                        cliff = acantilado_hz(x)
                        if cliff >= UMBRAL_BUENA:
                            _copiar_o_convertir_buena(ffmpeg, src, dst, a.formato, a.calidad)
                        else:
                            y = por_trozos(x, proc, seg=a.seg, reservar=memoria.crear)
                            m = mezcla_auto(cliff) if a.mezcla == "auto" else max(0.0, min(1.0, float(a.mezcla) / 100.0))
                            out_wav = os.path.join(td, "out.wav")
                            escribir_wav(out_wav, memoria.mezclar(y, x, m))
                            _encode_desde_wav(ffmpeg, out_wav, src, dst, a.formato, a.calidad)
                            fue_mejorada = True
                if entrada_antes != _identidad_entrada(src):
                    try: os.remove(dst)
                    except OSError: pass
                    raise RuntimeError("el archivo original cambio mientras se procesaba; se reintentara")
                if not _salida_valida(ffprobe, dst):
                    raise RuntimeError("ffprobe rechazo la salida generada")
                _anota_manifest(regf, src, dst, params_src, entrada_antes)
                if fue_mejorada: mejoradas += 1
                else: copiadas += 1
            except Exception as e:
                try: os.remove(dst)  # una salida no verificada nunca debe quedar visible como valida
                except OSError: pass
                fallas += 1
                log.write("%s | %s\n" % (src, e)); log.flush()
            if n % 10 == 0 or n == len(pendientes):
                paso = (time.time() - t0) / n
                print("  %d/%d (mejoradas %d, compatibles %d, fallas %d) ~%.1f h restantes" %
                      (n, len(pendientes), mejoradas, copiadas, fallas,
                       (len(pendientes) - n) * paso / 3600.0))
    return resumen_final(salida, fallas)


if __name__ == "__main__":
    sys.exit(main())
