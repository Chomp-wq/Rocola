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
import sys, os, argparse, tempfile, subprocess, shutil, threading
from concurrent.futures import ThreadPoolExecutor
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from util_audio import leer_wav, acantilado_hz

EXTS = (".mp3", ".wav", ".flac", ".m4a", ".ogg", ".wma")

def medir(ffmpeg, ruta):
    """Acantilado (Hz) de ~24s del medio de la cancion; 0 si no se pudo leer."""
    if ruta.lower().endswith(".wav") and ffmpeg is None:
        try:
            x, _ = leer_wav(ruta)
            return acantilado_hz(x)
        except Exception:
            return 0.0
    tmp = os.path.join(tempfile.gettempdir(), "esc_%d.wav" % threading.get_ident())
    for ss in ("30", "0"):   # canciones cortas: reintenta desde el inicio
        try:
            subprocess.run([ffmpeg, "-y", "-v", "error", "-ss", ss, "-t", "24", "-i", ruta,
                            "-ac", "1", "-ar", "44100", "-sample_fmt", "s16", tmp],
                           check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            x, _ = leer_wav(tmp)
            if len(x) > 44100:
                return acantilado_hz(x)
        except Exception:
            pass
    return 0.0

def veredicto(hz):
    if hz <= 0: return "ilegible"
    if hz < 15500: return "MUY MEJORABLE"
    if hz < 18500: return "mejorable"
    return "ya buena"

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--musica", required=True)
    ap.add_argument("--hilos", type=int, default=12)
    ap.add_argument("--max", type=int, default=0, help="0 = todas")
    a = ap.parse_args()
    ffmpeg = shutil.which("ffmpeg")
    rutas = []
    for base, _, files in os.walk(a.musica):
        for f in files:
            if f.lower().endswith(EXTS): rutas.append(os.path.join(base, f))
    if a.max > 0: rutas = rutas[: a.max]
    if not rutas: print("No hay canciones en", a.musica); return 1
    print("Midiendo %d canciones con %d hilos... (esto se puede dejar corriendo solo)" % (len(rutas), a.hilos))
    res = []
    hecho = [0]
    def uno(r):
        hz = medir(ffmpeg, r)
        res.append((hz, r))
        hecho[0] += 1
        if hecho[0] % 200 == 0: print("  %d / %d ..." % (hecho[0], len(rutas)))
    with ThreadPoolExecutor(max_workers=a.hilos) as ex:
        list(ex.map(uno, rutas))
    res.sort(key=lambda t: (t[0] <= 0, t[0]))   # peor acantilado primero; ilegibles al final
    here = os.path.dirname(os.path.abspath(__file__))
    with open(os.path.join(here, "calidad_biblioteca.csv"), "w", encoding="utf-8") as f:
        f.write("acantilado_hz;veredicto;ruta\n")
        for hz, r in res: f.write("%.0f;%s;%s\n" % (hz, veredicto(hz), r))
    top = [r for hz, r in res if 0 < hz < 15500][:200]
    with open(os.path.join(here, "peores_canciones.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(top) + "\n")
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
