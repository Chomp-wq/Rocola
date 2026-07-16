#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# ============================================================================================
#  FASE 1 - PILOTO: mejora un punado de canciones REALES de tu rocola con Apollo y arma el
#  kit A/B para que TU OIDO decida (antes de invertir en procesar 100 mil canciones).
#
#  Uso (en la maquina de la 4070 Super, con 00_verificar.py todo en OK):
#    python 01_piloto.py --entrada C:\muestras --salida C:\piloto [--max 12] [--seg 10]
#
#  Que produce en --salida:
#    pares/NN_original_<cancion>.wav  y  pares/NN_mejorada_<cancion>.wav   (RMS IGUALADO: A/B justo)
#    ciego/NN_X.wav / NN_Y.wav + clave.txt   (escucha SIN saber cual es cual; luego abre la clave)
#    espectros/NN_<cancion>.png              (antes/despues, si matplotlib esta instalado)
#    reporte.txt                             (acantilado antes/despues, tiempos, velocidad)
# ============================================================================================
import sys, os, time, glob, random, shutil, tempfile, argparse
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from util_audio import FS, leer_wav, escribir_wav, decodificar_a_wav, acantilado_hz, igualar_rms, por_trozos

def cargar_apollo(ruta):
    sys.path.insert(0, ruta)
    import torch, look2hear.models
    m = look2hear.models.BaseModel.from_pretrain("JusperLee/Apollo", sr=44100, win=20, feature_dim=256, layer=6).cuda().eval()
    def proc(x):  # x float64 [n, ch] -> [n, ch]
        t = torch.from_numpy(np.ascontiguousarray(x.T, dtype=np.float32)).unsqueeze(0).cuda()  # [1, ch, n]
        with torch.no_grad():
            try:
                y = m(t)
            except Exception:                       # si el modelo exige mono: canal por canal
                outs = []
                for c in range(t.shape[1]):
                    outs.append(m(t[:, c:c+1, :]))
                y = torch.cat(outs, dim=1)
        return y.squeeze(0).cpu().numpy().T.astype(np.float64)
    return proc

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--entrada", required=True, help="carpeta con canciones REALES de la rocola (mp3/wav/flac)")
    ap.add_argument("--salida", required=True)
    ap.add_argument("--apollo", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "Apollo"))
    ap.add_argument("--max", type=int, default=12)
    ap.add_argument("--seg", type=float, default=10.0, help="segundos por trozo (baja a 6 si la GPU se queda corta)")
    a = ap.parse_args()

    ff = shutil.which("ffmpeg")
    if not ff: print("Falta ffmpeg (corre 00_verificar.py)"); return 1
    archivos = sorted(sum([glob.glob(os.path.join(a.entrada, "*" + e)) for e in (".mp3", ".wav", ".flac", ".m4a")], []))
    if not archivos: print("No hay canciones en", a.entrada); return 1
    rng = random.Random(2026)
    rng.shuffle(archivos); archivos = archivos[: a.max]

    for d in ("pares", "ciego", "espectros"):
        os.makedirs(os.path.join(a.salida, d), exist_ok=True)
    print("Cargando Apollo (primera vez descarga el checkpoint)...")
    proc = cargar_apollo(a.apollo)

    try: import matplotlib; matplotlib.use("Agg"); import matplotlib.pyplot as plt
    except ImportError: plt = None; print("(sin matplotlib: no habra espectrogramas;  pip install matplotlib)")

    rep = ["cancion | acantilado antes -> despues (Hz) | dur (s) | proceso (s) | velocidad (x tiempo real)"]
    clave = []
    for i, ruta in enumerate(archivos, 1):
        nombre = os.path.splitext(os.path.basename(ruta))[0][:60]
        print("[%d/%d] %s" % (i, len(archivos), nombre))
        tmp = os.path.join(tempfile.gettempdir(), "piloto_dec.wav")
        try: decodificar_a_wav(ff, ruta, tmp)
        except Exception as e: print("   no se pudo decodificar:", e); continue
        x, _ = leer_wav(tmp)
        c0 = acantilado_hz(x)
        t0 = time.time()
        y = por_trozos(x, proc, seg=a.seg)
        dt = time.time() - t0
        y = igualar_rms(y, x)
        c1 = acantilado_hz(y)
        dur = len(x) / FS
        rep.append("%s | %.0f -> %.0f | %.0f | %.1f | %.1fx" % (nombre, c0, c1, dur, dt, dur / max(dt, 1e-9)))

        po = os.path.join(a.salida, "pares", "%02d_original_%s.wav" % (i, nombre))
        pm = os.path.join(a.salida, "pares", "%02d_mejorada_%s.wav" % (i, nombre))
        escribir_wav(po, x); escribir_wav(pm, y)
        if rng.random() < 0.5:
            shutil.copyfile(po, os.path.join(a.salida, "ciego", "%02d_X.wav" % i))
            shutil.copyfile(pm, os.path.join(a.salida, "ciego", "%02d_Y.wav" % i))
            clave.append("%02d: X=original  Y=mejorada" % i)
        else:
            shutil.copyfile(pm, os.path.join(a.salida, "ciego", "%02d_X.wav" % i))
            shutil.copyfile(po, os.path.join(a.salida, "ciego", "%02d_Y.wav" % i))
            clave.append("%02d: X=mejorada  Y=original" % i)

        if plt is not None:
            fig, axs = plt.subplots(1, 2, figsize=(14, 5), sharey=True)
            for ax, sig, tit in ((axs[0], x, "original (corte %.0f Hz)" % c0), (axs[1], y, "mejorada (corte %.0f Hz)" % c1)):
                mono = sig.mean(axis=1)[: FS * 30]
                ax.specgram(mono, NFFT=2048, Fs=FS, noverlap=1024, cmap="magma")
                ax.set_title(tit); ax.set_ylim(0, 22050)
            fig.suptitle(nombre); fig.tight_layout()
            fig.savefig(os.path.join(a.salida, "espectros", "%02d_%s.png" % (i, nombre)), dpi=90)
            plt.close(fig)

    open(os.path.join(a.salida, "reporte.txt"), "w", encoding="utf-8").write("\n".join(rep) + "\n")
    open(os.path.join(a.salida, "ciego", "clave.txt"), "w", encoding="utf-8").write("\n".join(clave) + "\n")
    print("\nLISTO. Protocolo de escucha en LEEME.md; el reporte quedo en", os.path.join(a.salida, "reporte.txt"))
    return 0

if __name__ == "__main__":
    sys.exit(main())
