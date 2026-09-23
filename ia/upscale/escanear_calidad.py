#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# ============================================================================================
#  ESCANER DE CALIDAD DE LA BIBLIOTECA: mide el "acantilado" (donde el codec corto los agudos)
#  de CADA cancion y te dice DONDE el upscaling va a brillar y donde no vale la pena.
#
#    python escanear_calidad.py --musica D:\musica [--hilos 12] [--max 0]
#
#  Salidas (junto a este script):
#    calidad_biblioteca.csv   -> todas las canciones, de PEOR a mejor (acantilado;veredicto;ruta)
#    peores_canciones.txt     -> el TOP 200 de las mas danadas: usa ESTAS para el piloto A/B
# ============================================================================================
import sys, os, csv, argparse, tempfile, subprocess, shutil, threading, math, json
from concurrent.futures import ThreadPoolExecutor
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from util_audio import leer_wav, acantilado_hz, remuestreador, args_decodificar

EXTS = (".mp3", ".wav", ".flac", ".m4a", ".ogg", ".wma")

TIMEOUT_FFMPEG = 2 * 60
TIMEOUT_FFPROBE = 30
NOWIN = getattr(subprocess, "CREATE_NO_WINDOW", 0)


def _real(path):
    return os.path.realpath(os.path.abspath(path))


def _canon(path):
    return os.path.normcase(_real(path))


def _dentro(path, raiz):
    try:
        return os.path.commonpath((_canon(path), _canon(raiz))) == _canon(raiz)
    except ValueError:
        return False


def _duracion(ffprobe, ruta):
    try:
        r = subprocess.run([ffprobe, "-v", "error", "-select_streams", "a:0",
                            "-show_entries", "stream=index:format=duration",
                            "-of", "json", ruta], capture_output=True, text=True,
                           stdin=subprocess.DEVNULL, timeout=TIMEOUT_FFPROBE,
                           creationflags=NOWIN)
        dato = json.loads(r.stdout) if r.returncode == 0 else {}
        dur = float(dato.get("format", {}).get("duration", 0))
        return dur if dato.get("streams") and math.isfinite(dur) and dur > 0 else None
    except Exception:
        return None


def medir(ffmpeg, ffprobe, ruta, filtro=False):
    """Acantilado (Hz) de ~24s del medio de la cancion; 0 si no se pudo leer.

    Se pasa a 44,1 kHz con el mismo remuestreador que el motor (``filtro``; por defecto el mejor
    de este ffmpeg): el de siempre deja imagenes que el detector toma por agudos (una cancion a
    32 kHz con musica hasta 15 kHz median 17,4 kHz)."""
    if ruta.lower().endswith(".wav") and ffmpeg is None:
        try:
            x, _ = leer_wav(ruta)
            return acantilado_hz(x)
        except Exception:
            return 0.0
    if filtro is False:
        filtro = remuestreador(ffmpeg)[1]
    dur = _duracion(ffprobe, ruta)
    posiciones = ([max(0.0, (dur - 24.0) / 2.0), 0.0] if dur is not None else [30.0, 0.0])
    with tempfile.TemporaryDirectory(prefix="rocola_scan_") as td:
        tmp = os.path.join(td, "muestra.wav")
        for ss in dict.fromkeys("%.3f" % p for p in posiciones):
            try:
                subprocess.run([ffmpeg, "-nostdin", "-y", "-v", "error", "-ss", ss,
                                "-t", "24", "-i", ruta] + args_decodificar(filtro) + [tmp], check=True,
                               stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                               stderr=subprocess.DEVNULL, timeout=TIMEOUT_FFMPEG,
                               creationflags=NOWIN)
                x, _ = leer_wav(tmp)
                if len(x) > 44100:
                    return acantilado_hz(x)
            except Exception:
                pass
    return 0.0


def _escanear(raiz):
    raiz = _canon(raiz)
    rutas = []
    dirs_vistos = {raiz}
    def error_walk(error):
        raise error
    for base, dirs, files in os.walk(raiz, onerror=error_walk):
        seguros = []
        for nombre in sorted(dirs, key=str.lower):
            candidato = os.path.join(base, nombre)
            real = _canon(candidato)
            if os.path.islink(candidato) or not _dentro(real, raiz) or real in dirs_vistos:
                continue
            dirs_vistos.add(real)
            seguros.append(nombre)
        dirs[:] = seguros
        for nombre in sorted(files, key=str.lower):
            ruta = os.path.join(base, nombre)
            if nombre.lower().endswith(EXTS) and os.path.isfile(ruta) and _dentro(ruta, raiz):
                rutas.append(_real(ruta))
    return rutas


def _herramienta_funciona(path):
    try:
        subprocess.run([path, "-version"], check=True, stdin=subprocess.DEVNULL,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=15,
                       creationflags=NOWIN)
        return True
    except Exception:
        return False

def veredicto(hz):
    if hz <= 0: return "ilegible"
    if hz < 15500: return "MUY MEJORABLE"
    if hz < 18500: return "mejorable"
    return "ya buena"

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--musica")
    ap.add_argument("--hilos", type=int, default=12)
    ap.add_argument("--max", type=int, default=0, help="0 = todas")
    ap.add_argument("--interactivo", action="store_true")
    a = ap.parse_args()
    if not a.musica and a.interactivo:
        try:
            import tkinter
            from tkinter import filedialog
            root = tkinter.Tk(); root.withdraw()
            a.musica = filedialog.askdirectory(title="Selecciona la biblioteca de musica", mustexist=True)
            root.destroy()
        except Exception as e:
            print("[X] No pude abrir el selector de carpetas:", e); return 2
    if not a.musica or not os.path.isdir(a.musica):
        print("[X] La carpeta de musica no existe:", a.musica); return 2
    if a.hilos < 1:
        print("[X] --hilos debe ser al menos 1"); return 2
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        print("[X] Falta ffmpeg (corre 1_instalar.bat)"); return 1
    ffprobe = shutil.which("ffprobe")
    if not ffprobe:
        junto = os.path.join(os.path.dirname(ffmpeg), "ffprobe.exe" if os.name == "nt" else "ffprobe")
        ffprobe = junto if os.path.isfile(junto) else None
    if not ffprobe:
        print("[X] Falta ffprobe junto a ffmpeg"); return 1
    if not _herramienta_funciona(ffmpeg) or not _herramienta_funciona(ffprobe):
        print("[X] ffmpeg o ffprobe existe pero no se puede ejecutar"); return 1
    try:
        rutas = _escanear(a.musica)
    except OSError as e:
        print("[X] No se pudo recorrer toda la biblioteca:", e); return 1
    if a.max > 0: rutas = rutas[: a.max]
    if not rutas: print("No hay canciones en", a.musica); return 1
    remuestreo, filtro = remuestreador(ffmpeg)   # una vez, antes de repartir el trabajo en hilos
    print("Remuestreo a 44,1 kHz:", remuestreo)
    print("Midiendo %d canciones con %d hilos... (esto se puede dejar corriendo solo)" % (len(rutas), a.hilos))
    res = []
    hecho = [0]
    progreso_lock = threading.Lock()
    def uno(r):
        hz = medir(ffmpeg, ffprobe, r, filtro)
        with progreso_lock:
            res.append((hz, r))
            hecho[0] += 1
            if hecho[0] % 200 == 0: print("  %d / %d ..." % (hecho[0], len(rutas)))
    with ThreadPoolExecutor(max_workers=a.hilos) as ex:
        list(ex.map(uno, rutas))
    res.sort(key=lambda t: (t[0] <= 0, t[0]))   # peor acantilado primero; ilegibles al final
    here = os.path.dirname(os.path.abspath(__file__))
    csv_final = os.path.join(here, "calidad_biblioteca.csv")
    csv_tmp = csv_final + ".%d.tmp" % os.getpid()
    with open(csv_tmp, "w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f, delimiter=";", quoting=csv.QUOTE_MINIMAL, lineterminator="\n")
        w.writerow(("acantilado_hz", "veredicto", "ruta"))
        for hz, r in res: w.writerow(("%.0f" % hz, veredicto(hz), os.path.abspath(r)))
        f.flush(); os.fsync(f.fileno())
    os.replace(csv_tmp, csv_final)
    top = [r for hz, r in res if 0 < hz < 15500][:200]
    top_final = os.path.join(here, "peores_canciones.txt")
    top_tmp = top_final + ".%d.tmp" % os.getpid()
    with open(top_tmp, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(top) + "\n")
        f.flush(); os.fsync(f.fileno())
    os.replace(top_tmp, top_final)
    n_muy = sum(1 for hz, _ in res if 0 < hz < 15500)
    n_mej = sum(1 for hz, _ in res if 15500 <= hz < 18500)
    n_ok  = sum(1 for hz, _ in res if hz >= 18500)
    n_bad = sum(1 for hz, _ in res if hz <= 0)
    print("\nRESUMEN de %d canciones:" % len(res))
    print("  MUY MEJORABLES  con acantilado < 15.5 kHz : %d   <- aqui la IA brilla" % n_muy)
    print("  mejorables      15.5 - 18.5 kHz           : %d" % n_mej)
    print("  ya buenas       > 18.5 kHz                : %d   <- casi nada que ganar" % n_ok)
    print("  ilegibles                                  : %d" % n_bad)
    print("\nArchivos: calidad_biblioteca.csv  y  peores_canciones.txt (usa esas 200 para el piloto)")
    return 0

if __name__ == "__main__":
    sys.exit(main())
