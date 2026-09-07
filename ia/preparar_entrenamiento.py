# -*- coding: utf-8 -*-
# ============================================================================================
#  PREPARA EL MATERIAL DE ENTRENAMIENTO para la IA propia (el DSEE espectral de la rocola).
#
#  La red aprende "que forma tienen los agudos REALES" -> solo sirven de maestras las canciones
#  de CALIDAD COMPLETA (acantilado >= 18500 Hz). Una cancion danada no puede ensenar lo que no
#  tiene. Este script recorre la biblioteca al azar, mide cada candidata y va guardando UN trozo
#  de 60 segundos (del centro, donde la mezcla esta completa) como WAV 44.1k/16-bit, hasta juntar
#  las que se pidan. Con ~600 canciones el modelo (que es chico) queda saturado de sobra.
#
#    python preparar_entrenamiento.py --musica auto --destino entrenamiento_wavs --canciones 600
#      --musica auto     busca una carpeta llamada "musica" junto a ia\ o hasta 5 niveles arriba
#                        (la MISMA regla con la que la rocola encuentra su musica), o una ruta.
#      --canciones N     cuantas canciones buenas juntar (defecto 600)
#
#  Reanudable: si se corta, vuelve a correrlo y continua (no repite lo ya convertido).
# ============================================================================================
import sys, os, random, argparse, subprocess, shutil, hashlib

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, "upscale"))
from util_audio import FS, leer_wav, acantilado_hz

NOWIN = getattr(subprocess, "CREATE_NO_WINDOW", 0)
EXTS = (".mp3", ".flac", ".wav", ".m4a", ".ogg", ".wma", ".aac")
MIN_HZ = 18500          # mismo umbral de "calidad completa" que usa el motor de IA
SEG = 60                # segundos que se guardan por cancion (del centro)

def carpeta_musica(arg):
    if arg and arg.lower() != "auto":
        return arg if os.path.isdir(arg) else None
    d = AQUI
    for _ in range(6):
        cand = os.path.join(d, "musica")
        if os.path.isdir(cand): return cand
        p = os.path.dirname(d)
        if p == d: break
        d = p
    return None

def candidatas(raiz):
    out = []
    for base, _dirs, files in os.walk(raiz):
        for f in files:
            if f.lower().endswith(EXTS): out.append(os.path.join(base, f))
    return out

def convertir(ffmpeg, src, dst):
    """Decodifica ~60s del centro de la cancion a WAV 44.1k/16-bit estereo. True si quedo bien."""
    try:
        subprocess.run([ffmpeg, "-nostdin", "-y", "-v", "error", "-ss", "45", "-t", str(SEG), "-i", src,
                        "-ac", "2", "-ar", str(FS), "-sample_fmt", "s16", dst],
                       check=True, timeout=120, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                       stderr=subprocess.DEVNULL, creationflags=NOWIN)
        return os.path.getsize(dst) > FS * 2 * 2 * 20     # al menos ~20s utiles (canciones cortas no alcanzan el centro)
    except Exception:
        return False

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--musica", default="auto")
    ap.add_argument("--destino", default=os.path.join(AQUI, "entrenamiento_wavs"))
    ap.add_argument("--canciones", type=int, default=600)
    a = ap.parse_args()

    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        print("[X] Falta ffmpeg en el PATH (lo instala 1_instalar.bat de la carpeta upscale)."); return 1
    raiz = carpeta_musica(a.musica)
    if not raiz:
        print("[X] No encontre la carpeta de musica. Pasa la ruta: --musica D:\\mi\\musica"); return 1
    os.makedirs(a.destino, exist_ok=True)
    hechas = set(f for f in os.listdir(a.destino) if f.startswith("ent_"))   # solo lo ya convertido de verdad
    print("Biblioteca:", raiz)
    print("Buscando archivos de audio...")
    todas = candidatas(raiz)
    print("  %d archivos. Revisando al azar hasta juntar %d de calidad completa..." % (len(todas), a.canciones))
    random.Random(20260717).shuffle(todas)

    buenas = len(hechas); vistas = 0
    tmp = os.path.join(a.destino, "_probando.wav")
    for src in todas:
        if buenas >= a.canciones: break
        nombre = "ent_%s.wav" % hashlib.sha1(os.path.abspath(src).lower().encode("utf-8", "replace")).hexdigest()[:12]   # estable entre corridas (reanudable)
        if nombre in hechas: continue
        vistas += 1
        if not convertir(ffmpeg, src, tmp): continue
        try:
            x, _ = leer_wav(tmp)
            cliff = acantilado_hz(x)
        except Exception:
            continue
        if cliff >= MIN_HZ:
            os.replace(tmp, os.path.join(a.destino, nombre))
            hechas.add(nombre); buenas += 1
        if vistas % 25 == 0:
            print("  revisadas %d  |  buenas juntadas %d / %d" % (vistas, buenas, a.canciones))
    try: os.remove(tmp)
    except OSError: pass
    print("LISTO: %d canciones de calidad completa en %s" % (buenas, a.destino))
    if buenas < 100:
        print("[!] Son pocas; el entrenamiento saldra pobre. Junta mas musica de buena calidad y reintenta.")
        return 1
    return 0

if __name__ == "__main__":
    sys.exit(main())
