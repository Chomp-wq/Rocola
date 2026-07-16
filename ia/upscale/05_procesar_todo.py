# -*- coding: utf-8 -*-
# ============================================================================================
#  FASE 3 - PROCESADOR MASIVO: mejora TODA la biblioteca y arma una copia nueva "HD".
#  - Prioriza las MAS danadas (del CSV): las mejores ganancias caen primero.
#  - Las "ya buenas" se COPIAN tal cual (no se tocan; instantaneo).
#  - Conserva etiquetas y portada (ID3) via ffmpeg -> la rocola sigue leyendo genero/artista.
#  - REANUDABLE: si lo cierras y lo vuelves a abrir, sigue donde iba (_procesadas.txt).
#  - La carpeta de salida ES una copia completa de tu musica -> tambien te sirve de RESPALDO.
#
#    python 05_procesar_todo.py --musica E:\musica --salida D:\musica_HD [--formato mp3|flac|wav] [--calidad 320]
# ============================================================================================
import sys, os, csv, time, shutil, tempfile, argparse, subprocess, threading
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from util_audio import FS, leer_wav, escribir_wav, acantilado_hz, igualar_rms, por_trozos, mezclar, mezcla_auto

UMBRAL_BUENA = 18500.0   # arriba de esto: se copia tal cual (no hay casi nada que ganar)
EXTS = (".mp3", ".wav", ".flac", ".m4a", ".ogg", ".wma")

def orden_desde_csv(csv_path, raiz):
    """Rutas ordenadas de PEOR a mejor segun el CSV; si no hay CSV, camina la carpeta."""
    if csv_path and os.path.exists(csv_path):
        filas = []
        with open(csv_path, encoding="utf-8") as f:
            r = csv.reader(f, delimiter=";"); next(r, None)
            for row in r:
                if len(row) >= 3:
                    try: hz = float(row[0])
                    except ValueError: continue
                    if os.path.exists(row[2]): filas.append((hz if hz > 0 else 99999, row[2]))
        filas.sort(key=lambda t: t[0])
        return [r for _, r in filas]
    rutas = []
    for base, _, files in os.walk(raiz):
        for f in files:
            if f.lower().endswith(EXTS): rutas.append(os.path.join(base, f))
    return rutas

def destino(raiz, salida, src, formato):
    rel = os.path.relpath(src, raiz)
    dst = os.path.join(salida, rel)
    if formato != "copia":
        dst = os.path.splitext(dst)[0] + "." + formato
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    return dst

def encode(ffmpeg, audio_wav, original, dst, formato, calidad):
    """Codifica el audio mejorado (wav temporal) al formato final, copiando etiquetas + portada
    del ORIGINAL. -map ...? = la portada es opcional (no truena si no hay)."""
    cmd = [ffmpeg, "-y", "-v", "error", "-i", audio_wav, "-i", original,
           "-map", "0:a", "-map", "1:v?", "-map_metadata", "1", "-c:v", "copy"]
    if formato == "mp3":  cmd += ["-c:a", "libmp3lame", "-b:a", "%dk" % calidad, "-id3v2_version", "3"]
    elif formato == "flac": cmd += ["-c:a", "flac"]
    else:                 cmd += ["-c:a", "pcm_s16le"]
    cmd.append(dst)
    subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--musica", required=True)
    ap.add_argument("--salida", required=True)
    ap.add_argument("--csv", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "calidad_biblioteca.csv"))
    ap.add_argument("--formato", choices=["mp3", "flac", "wav"], default="mp3")
    ap.add_argument("--calidad", type=int, default=320)
    ap.add_argument("--seg", type=float, default=10.0)
    ap.add_argument("--mezcla", default="auto", help="cuanta IA: auto (segun dano) o un numero 0..100")
    a = ap.parse_args()

    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg: print("[X] Falta ffmpeg (corre 1_instalar.bat)"); return 1
    os.makedirs(a.salida, exist_ok=True)
    reg = os.path.join(a.salida, "_procesadas.txt")
    hechas = set()
    if os.path.exists(reg):
        hechas = set(l.strip() for l in open(reg, encoding="utf-8") if l.strip())
    log = open(os.path.join(a.salida, "_errores.txt"), "a", encoding="utf-8")

    rutas = orden_desde_csv(a.csv, a.musica)
    rutas = [r for r in rutas if r not in hechas]
    if not rutas: print("Nada pendiente: todo ya estaba procesado."); return 0
    print("Por procesar: %d canciones (las mas danadas primero)." % len(rutas))
    print("Cargando Apollo...")
    import modelo_apollo
    proc = modelo_apollo.cargar()

    t0 = time.time(); n = 0; mejoradas = 0; copiadas = 0
    regf = open(reg, "a", encoding="utf-8")
    for src in rutas:
        n += 1
        try:
            tmp = os.path.join(tempfile.gettempdir(), "hd_dec_%d.wav" % threading.get_ident())
            subprocess.run([ffmpeg, "-y", "-v", "error", "-i", src, "-ac", "2", "-ar", str(FS),
                            "-sample_fmt", "s16", tmp], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            x, _ = leer_wav(tmp)
            cliff = acantilado_hz(x)
            if cliff >= UMBRAL_BUENA:                         # ya buena -> copia tal cual (rapidisimo)
                dst = destino(a.musica, a.salida, src, "copia")
                shutil.copyfile(src, dst); copiadas += 1
            else:                                             # danada -> restaurar con Apollo
                y = por_trozos(x, proc, seg=a.seg)
                m = mezcla_auto(cliff) if a.mezcla == "auto" else max(0.0, min(1.0, float(a.mezcla) / 100.0))
                y = mezclar(y, x, m)                          # wet/dry: suaviza el vaiven de la IA en lo muy danado
                out_wav = os.path.join(tempfile.gettempdir(), "hd_out_%d.wav" % threading.get_ident())
                escribir_wav(out_wav, y)
                dst = destino(a.musica, a.salida, src, a.formato)
                encode(ffmpeg, out_wav, src, dst, a.formato, a.calidad)
                mejoradas += 1
            regf.write(src + "\n"); regf.flush()
        except Exception as e:
            log.write("%s | %s\n" % (src, e)); log.flush()
        if n % 10 == 0 or n == len(rutas):
            paso = (time.time() - t0) / n
            faltan = (len(rutas) - n) * paso
            print("  %d/%d  (mejoradas %d, copiadas %d)  ~%.1f h restantes" %
                  (n, len(rutas), mejoradas, copiadas, faltan / 3600.0))
    regf.close(); log.close()
    print("\nLISTO. Biblioteca HD en:", a.salida)
    print("Apunta la carpeta 'musica' de la rocola a esa carpeta (o copia su contenido).")
    return 0

if __name__ == "__main__":
    sys.exit(main())
