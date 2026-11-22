#!/usr/bin/env python3
# compara.py ORIGINAL.wav BASE: LO QUE SE OYE FRENTE AL ORIGINAL, sin y con el relleno de los huecos (R1; prueba.sh). Con la misma cuenta
# que la tabla de octubre (AUDIO_P1_D1_D2_Y_TECNICAS_MODEST_2026-10-30.md, «parte de lo que se oye que el MP3 deja 15 dB o mas por
# debajo, por bandas»): un espectrograma (1024 puntos, salto 512, Hann) del original y de lo descomprimido (BASE.sin.f32 y BASE.con.f32 de
# «rocola-xp --prueba-huecos decodifica», alineados con el original por correlacion); una celda «se oye» si el original esta a menos de
# 50 dB de lo mas fuerte de su cuadro; es HUECO si lo descomprimido queda 15 dB o mas por debajo del original, y SOBRA si queda 6 dB o mas
# por encima (ruido que el relleno puso donde no habia). Una linea por banda y el total de 3 a 16 kHz.
import sys
import wave
import numpy as np

orig_ruta, base = sys.argv[1], sys.argv[2]
with wave.open(orig_ruta) as w:
    ch = w.getnchannels(); o = np.frombuffer(w.readframes(w.getnframes()), '<i2').astype(np.float64) / 32768.0
o = o.reshape(-1, ch)
sin = np.fromfile(base + '.sin.f32', '<f4').astype(np.float64).reshape(-1, ch)
con = np.fromfile(base + '.con.f32', '<f4').astype(np.float64).reshape(-1, ch)

# el retardo del codificador y del descompresor: por correlacion del canal izquierdo (los primeros 10 s)
m = min(len(o), len(sin), 441000)
a = o[:m, 0]; b = sin[:m, 0]
n2 = 1 << int(np.ceil(np.log2(2 * m)))
cc = np.fft.irfft(np.fft.rfft(b, n2) * np.conj(np.fft.rfft(a, n2)), n2)
lag = int(np.argmax(cc[:4000]))   # lo descomprimido va detras del original
L = min(len(o), len(sin) - lag, len(con) - lag)
o = o[:L]; sin = sin[lag:lag + L]; con = con[lag:lag + L]

def espectro(x):
    N, H = 1024, 512
    w = np.hanning(N)
    k = (len(x) - N) // H
    idx = np.arange(N)[None, :] + H * np.arange(k)[:, None]
    return np.abs(np.fft.rfft(x[idx] * w, axis=1)) ** 2 + 1e-20

bandas = [(3000, 5000), (5000, 10000), (10000, 14000), (14000, 16000), (3000, 16000)]
f = np.fft.rfftfreq(1024, 1 / 44100)
res = {b: [0, 0, 0, 0] for b in bandas}   # se oyen, huecos sin, huecos con, sobra con
for c in range(ch):
    O, S, C = espectro(o[:, c]), espectro(sin[:, c]), espectro(con[:, c])
    oy = O > (O.max(axis=1, keepdims=True) * 1e-5)   # a menos de 50 dB de lo mas fuerte del cuadro
    hs = S < O * 10 ** (-15 / 10); hc = C < O * 10 ** (-15 / 10); so = (C > O * 10 ** (6 / 10)) & ~(S > O * 10 ** (6 / 10))
    for (lo, hi) in bandas:
        sel = (f >= lo) & (f < hi)
        m_ = oy[:, sel]
        r = res[(lo, hi)]
        r[0] += m_.sum(); r[1] += (hs[:, sel] & m_).sum(); r[2] += (hc[:, sel] & m_).sum(); r[3] += (so[:, sel] & m_).sum()
for (lo, hi) in bandas:
    r = res[(lo, hi)]
    t = max(1, r[0])
    print("BANDA %d-%d huecos_sin=%.2f huecos_con=%.2f sobra_con=%.2f" % (lo, hi, 100.0 * r[1] / t, 100.0 * r[2] / t, 100.0 * r[3] / t))
print("RETARDO %d" % lag)
