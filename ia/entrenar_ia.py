#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# ============================================================================================
#  ENTRENADOR DE LA IA PROPIA DE LA ROCOLA (rama rocola-hd)
# --------------------------------------------------------------------------------------------
#  Entrena el MLP (28 -> 48 -> 48 -> 12, tanh) que predice la ENVOLVENTE de la banda de agudos
#  que el codec corto, a partir de la forma del espectro que sobrevivio. El reconstructor
#  espectral de la rocola (SpectralTap.RegenNeural) usa esa envolvente en vez de la caida fija
#  de -9 dB/octava. El CONTRATO de entrada/salida (RIA1) debe coincidir EXACTO con Form1.cs.
#
#  Uso:
#    python3 entrenar_ia.py                        # entrena con audio SINTETICO (prueba de concepto)
#    python3 entrenar_ia.py --musica /ruta/wavs    # entrena con TU musica (archivos .wav 44.1k)
#      --clips N        cuantos trozos de 2s usar en total (defecto 400; para produccion 6000+)
#      --por_archivo M  tope de trozos por cancion (defecto sin tope; 12 = mas VARIEDAD de canciones)
#  Para preparar los wav desde tu biblioteca mp3 usa entrenar_con_mi_musica.bat (hace todo solo).
#
#  Salidas (en esta carpeta):
#    rocola_ia.bin          -> copialo JUNTO AL Rocola.exe para activar la IA
#    ia_vectores_prueba.txt -> vectores de paridad (los usa la bateria de pruebas en C#)
#
#  Requisitos: python3 + numpy. Para MP3, convierte antes a WAV (ffmpeg -i cancion.mp3 cancion.wav).
# ============================================================================================
import sys, os, wave, struct
import numpy as np

FS = 44100
EPS = 1e-12

# ---------------- contrato RIA1 (identico a Form1.cs) ----------------
def hz_bin(hz, frame):
    ny = frame // 2
    b = int(hz / (FS / frame))
    return min(max(b, 1), ny - 1)

def features_and_target(mag2n, frame, fc_hz):
    """mag2n = |X[k]/N|^2 del cuadro COMPLETO (con banda alta real). Devuelve (x28, y12) o None."""
    ny = frame // 2
    x = np.zeros(28, dtype=np.float64)
    # 24 bandas log de 200 Hz al corte (energia media por bin)
    for i in range(24):
        a = hz_bin(200.0 * (fc_hz / 200.0) ** (i / 24.0), frame)
        b = hz_bin(200.0 * (fc_hz / 200.0) ** ((i + 1) / 24.0), frame)
        if b <= a: b = a + 1
        x[i] = np.log10(mag2n[a:b].mean() + EPS)
    mean = x[:24].mean()
    x[:24] -= mean
    x[24] = mean
    x[25] = fc_hz / 20000.0
    e = x[16:24] + mean                                  # planitud de las ultimas 8 bandas bajo el corte
    x[26] = e.mean() - np.log10((10.0 ** e).mean() + EPS)
    x[27] = 0.0
    if mean < -8.0:                                      # cuadro casi mudo: no sirve para aprender
        return None
    # costura: banda 0.92*corte .. corte
    cut = hz_bin(fc_hz, frame)
    sa = hz_bin(fc_hz * 0.92, frame)
    if sa >= cut: sa = cut - 1
    seam = mag2n[sa:cut].mean() + EPS
    # objetivo: 12 sub-bandas log del corte a min(2*corte, 20k, ny*0.95) (energia REAL de la banda alta)
    top = min(2.0 * fc_hz, 20000.0, ny * (FS / frame) * 0.95)
    if top <= fc_hz * 1.1:
        return None
    y = np.zeros(12, dtype=np.float64)
    for j in range(12):
        a = hz_bin(fc_hz * (top / fc_hz) ** (j / 12.0), frame)
        b = hz_bin(fc_hz * (top / fc_hz) ** ((j + 1) / 12.0), frame)
        if a < cut: a = cut
        if b <= a: b = a + 1
        y[j] = np.log10(mag2n[a:b].mean() + EPS) - np.log10(seam)
    return x, np.clip(y, -4.0, 0.5)

def frames_of(sig, frame):
    """Cuadros con ventana raiz-de-Hann (la MISMA del SpectralTap) -> |X/N|^2."""
    win = np.sqrt(0.5 - 0.5 * np.cos(2.0 * np.pi * np.arange(frame) / frame))
    hop = frame // 2
    out = []
    for p in range(0, len(sig) - frame, hop):
        X = np.fft.rfft(sig[p:p + frame] * win)
        m = (np.abs(X) / frame) ** 2
        out.append(m)
    return out

# ---------------- datos: sintetico "musical" o carpeta de WAVs ----------------
def synth_clip(rng, secs=1.2):
    """Musica de mentiras pero VARIADA: voces armonicas con distinto brillo + percusion ruidosa.
    La variedad de brillo (tilt) es lo que ensena a la red la relacion forma->envolvente."""
    n = int(FS * secs)
    t = np.arange(n) / FS
    x = np.zeros(n)
    for _ in range(rng.integers(2, 6)):
        f0 = rng.uniform(80, 900)
        nh = int(min(20000 / f0, rng.uniform(10, 70)))
        if nh < 3: continue
        tilt = rng.uniform(0.4, 2.2)
        h = np.arange(1, nh + 1)
        amps = h ** (-tilt) * rng.uniform(0.6, 1.4, nh)
        ph = rng.uniform(0, 2 * np.pi, nh)
        env = np.minimum(1.0, t * rng.uniform(2, 30)) * np.exp(-t * rng.uniform(0.2, 2.5))
        x += env * (amps[None, :] * np.sin(2 * np.pi * t[:, None] * (f0 * h)[None, :] + ph[None, :])).sum(axis=1) * 0.12
    for _ in range(rng.integers(2, 7)):                   # "platillos": ruido con paso-altas y decaimiento
        p0 = rng.integers(0, n - FS // 5)
        ln = rng.integers(FS // 40, FS // 6)
        burst = rng.normal(0, 1, ln)
        B = np.fft.rfft(burst)
        f = np.fft.rfftfreq(ln, 1 / FS)
        fcut = rng.uniform(1500, 9000)
        B *= 1.0 / (1.0 + (fcut / (f + 1)) ** 4)          # paso-altas suave
        burst = np.fft.irfft(B, ln) * np.exp(-np.arange(ln) / (ln * rng.uniform(0.15, 0.6)))
        x[p0:p0 + ln] += burst * rng.uniform(0.02, 0.15)
    m = np.abs(x).max()
    return (x / m * 0.5) if m > 0 else x

def load_wavs(folder, rng, max_clips=400, max_per_file=0):
    clips = []
    files = [f for f in sorted(os.listdir(folder)) if f.lower().endswith(".wav")]
    rng.shuffle(files)
    for fn in files:
        try:
            w = wave.open(os.path.join(folder, fn), "rb")
            if w.getframerate() != FS or w.getsampwidth() != 2:
                w.close(); continue
            nch = w.getnchannels()
            nfr = min(w.getnframes(), FS * 60)
            raw = np.frombuffer(w.readframes(nfr), dtype=np.int16).astype(np.float64) / 32768.0
            w.close()
            mono = raw.reshape(-1, nch).mean(axis=1) if nch > 1 else raw
            propios = 0
            for p in range(0, len(mono) - FS * 2, FS * 2):     # trozos de 2s
                clips.append(mono[p:p + FS * 2]); propios += 1
                if len(clips) >= max_clips: return clips
                if max_per_file and propios >= max_per_file: break   # variedad: mejor MAS canciones que mas trozos de una
        except Exception:
            continue
    return clips

def build_dataset(clips, rng):
    X, Y = [], []
    for ci, clip in enumerate(clips):
        fc = rng.uniform(10500, 17500)                     # el "corte del MP3" simulado, por clip
        for frame in (2048, 4096):                          # ambos tamanos de ventana de la rocola
            for m in frames_of(clip, frame):
                r = features_and_target(m, frame, fc)
                if r is not None:
                    X.append(r[0]); Y.append(r[1])
    return np.array(X, dtype=np.float64), np.array(Y, dtype=np.float64)

# ---------------- MLP + Adam (backprop a mano: sin dependencias pesadas) ----------------
class Mlp:
    def __init__(self, dims, rng):
        self.W = [rng.normal(0, np.sqrt(2.0 / dims[i]), (dims[i + 1], dims[i])) for i in range(len(dims) - 1)]
        self.B = [np.zeros(dims[i + 1]) for i in range(len(dims) - 1)]
    def forward(self, X):
        acts = [X]
        for L in range(len(self.W)):
            z = acts[-1] @ self.W[L].T + self.B[L]
            acts.append(np.tanh(z) if L < len(self.W) - 1 else z)
        return acts
    def predict(self, X): return self.forward(X)[-1]

def train(model, X, Y, Xv, Yv, epochs=60, lr=2e-3, batch=256, seed=3):
    rng = np.random.default_rng(seed)
    mW = [np.zeros_like(w) for w in model.W]; vW = [np.zeros_like(w) for w in model.W]
    mB = [np.zeros_like(b) for b in model.B]; vB = [np.zeros_like(b) for b in model.B]
    b1, b2, eps, tstep = 0.9, 0.999, 1e-8, 0
    for ep in range(epochs):
        idx = rng.permutation(len(X))
        for s in range(0, len(X), batch):
            bi = idx[s:s + batch]
            acts = model.forward(X[bi])
            g = 2.0 * (acts[-1] - Y[bi]) / len(bi)          # dMSE
            tstep += 1
            for L in reversed(range(len(model.W))):
                gW = g.T @ acts[L]; gB = g.sum(axis=0)
                if L > 0: g = (g @ model.W[L]) * (1.0 - acts[L] ** 2)
                mW[L] = b1 * mW[L] + (1 - b1) * gW; vW[L] = b2 * vW[L] + (1 - b2) * gW ** 2
                mB[L] = b1 * mB[L] + (1 - b1) * gB; vB[L] = b2 * vB[L] + (1 - b2) * gB ** 2
                c1, c2 = 1 - b1 ** tstep, 1 - b2 ** tstep
                model.W[L] -= lr * (mW[L] / c1) / (np.sqrt(vW[L] / c2) + eps)
                model.B[L] -= lr * (mB[L] / c1) / (np.sqrt(vB[L] / c2) + eps)
        if (ep + 1) % 10 == 0 or ep == 0:
            tr = float(((model.predict(X) - Y) ** 2).mean())
            vl = float(((model.predict(Xv) - Yv) ** 2).mean())
            print("  epoca %3d  mse-entren %.4f  mse-valid %.4f" % (ep + 1, tr, vl))
    return model

def export_ria1(model, path):
    with open(path, "wb") as f:
        f.write(b"RIA1")
        f.write(struct.pack("<i", len(model.W)))
        for W, B in zip(model.W, model.B):
            f.write(struct.pack("<ii", W.shape[0], W.shape[1]))
            f.write(W.astype(np.float32).tobytes())        # fila-mayor (out, in): igual que la rocola
            f.write(B.astype(np.float32).tobytes())

def main():
    rng = np.random.default_rng(20260715)
    if "--musica" in sys.argv:
        folder = sys.argv[sys.argv.index("--musica") + 1]
        nclips = int(sys.argv[sys.argv.index("--clips") + 1]) if "--clips" in sys.argv else 400
        porarch = int(sys.argv[sys.argv.index("--por_archivo") + 1]) if "--por_archivo" in sys.argv else 0
        print("Cargando WAVs de:", folder)
        clips = load_wavs(folder, rng, max_clips=nclips, max_per_file=porarch)
        if len(clips) < 20:
            print("Muy pocos WAV 44.1k/16-bit utilizables (%d). Convierte con ffmpeg y reintenta." % len(clips)); return 1
        print("  %d trozos de musica real" % len(clips))
    else:
        print("Generando audio SINTETICO variado (prueba de concepto; para produccion usa --musica)...")
        clips = [synth_clip(rng) for _ in range(320)]
    print("Extrayendo pares (forma espectral -> envolvente real)...")
    X, Y = build_dataset(clips, rng)
    print("  %d ejemplos de %d entradas -> %d salidas" % (X.shape[0], X.shape[1], Y.shape[1]))
    idx = rng.permutation(len(X)); X, Y = X[idx], Y[idx]
    nv = max(500, len(X) // 10)
    Xv, Yv, X, Y = X[:nv], Y[:nv], X[nv:], Y[nv:]

    # linea base: la curva FIJA del reconstructor clasico (-9 dB/oct de amplitud = -1.8 log10/oct de energia)
    fc = Xv[:, 25] * 20000.0
    top = np.minimum(2.0 * fc, np.minimum(20000.0, np.full_like(fc, FS / 2 * 0.95)))
    ybase = np.zeros_like(Yv)
    for j in range(12):
        fmid = fc * (top / fc) ** ((j + 0.5) / 12.0)
        ybase[:, j] = np.clip(-1.8 * np.log2(fmid / fc), -4.0, 0.5)
    mse_base = float(((ybase - Yv) ** 2).mean())

    print("Entrenando el MLP 28->48->48->12 ...")
    model = train(Mlp([28, 48, 48, 12], rng), X, Y, Xv, Yv)
    mse_net = float(((model.predict(Xv) - Yv) ** 2).mean())
    print("RESULTADO  mse-valid: red = %.4f  |  curva fija = %.4f  (mejora %.1fx)" % (mse_net, mse_base, mse_base / max(mse_net, 1e-9)))

    here = os.path.dirname(os.path.abspath(__file__))
    export_ria1(model, os.path.join(here, "rocola_ia.bin"))
    with open(os.path.join(here, "ia_vectores_prueba.txt"), "w") as f:   # paridad python <-> C#
        P = model.predict(Xv[:24])
        for i in range(24):
            f.write(" ".join("%.9g" % v for v in Xv[i]) + " | " + " ".join("%.9g" % v for v in P[i]) + "\n")
    print("Listo: rocola_ia.bin (copialo junto a Rocola.exe) + ia_vectores_prueba.txt")
    return 0

if __name__ == "__main__":
    sys.exit(main())
