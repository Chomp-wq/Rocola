# -*- coding: utf-8 -*-
# Para unas canciones danadas, genera el ORIGINAL y VARIOS niveles de "cuanta IA" (mezcla),
# para que elijas cual te gusta. Apollo corre UNA vez por cancion; las mezclas son instantaneas.
#   python comparar_mezcla.py --entrada muestras_danadas --salida comparar_mezcla --niveles 0,40,55,70,100
import sys, os, glob, shutil, tempfile, argparse, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from util_audio import FS, leer_wav, escribir_wav, acantilado_hz, por_trozos, mezclar, mezcla_auto

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--entrada", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "muestras_danadas"))
    ap.add_argument("--salida", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "comparar_mezcla"))
    ap.add_argument("--niveles", default="0,40,55,70,100", help="porcentajes de IA a generar")
    ap.add_argument("--max", type=int, default=6)
    ap.add_argument("--seg", type=float, default=10.0)
    a = ap.parse_args()
    ff = shutil.which("ffmpeg")
    if not ff: print("[X] Falta ffmpeg (corre 1_instalar.bat)"); return 1
    arch = sorted(sum([glob.glob(os.path.join(a.entrada, "*" + e)) for e in (".mp3",".wav",".flac",".m4a")], []))[: a.max]
    if not arch: print("[X] No hay canciones en", a.entrada, "- corre 4_muestras.bat primero"); return 1
    niveles = [int(x) for x in a.niveles.split(",")]
    os.makedirs(a.salida, exist_ok=True)
    print("Cargando Apollo...")
    import modelo_apollo
    proc = modelo_apollo.cargar()
    rep = []
    for i, ruta in enumerate(arch, 1):
        nombre = os.path.splitext(os.path.basename(ruta))[0][:50]
        print("[%d/%d] %s" % (i, len(arch), nombre))
        tmp = os.path.join(tempfile.gettempdir(), "cmp_dec.wav")
        try:
            subprocess.run([ff, "-y", "-v", "error", "-i", ruta, "-ac", "2", "-ar", str(FS),
                            "-sample_fmt", "s16", tmp], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        except Exception as e:
            print("   no se pudo leer:", e); continue
        x, _ = leer_wav(tmp)
        cliff = acantilado_hz(x)
        auto = int(round(mezcla_auto(cliff) * 100))
        y = por_trozos(x, proc, seg=a.seg)                       # Apollo UNA vez
        for niv in niveles:
            etiqueta = "%03d" % niv + ("_AUTO" if niv == auto else "")
            escribir_wav(os.path.join(a.salida, "%02d_%s__IA%s.wav" % (i, nombre, etiqueta)), mezclar(y, x, niv / 100.0))
        rep.append("%s | corte %.0f Hz | IA recomendada (auto) = %d%%" % (nombre, cliff, auto))
    open(os.path.join(a.salida, "recomendado.txt"), "w", encoding="utf-8").write("\n".join(rep) + "\n")
    print("\nLISTO en:", a.salida)
    print("Escucha cada cancion en sus niveles IA000/IA040/... y decide cuanto te gusta.")
    print("El nivel con _AUTO es el que el sistema pondria solo segun el dano. Ver recomendado.txt.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
