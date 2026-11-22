#!/usr/bin/env python3
# musica.py SALIDA.wav [SEGUNDOS]: MUSICA SINTETICA para la prueba del relleno de los huecos (R1; prueba.sh), como la de octubre
# (AUDIO_P1_D1_D2_Y_TECNICAS_MODEST_2026-10-30.md): acordes con armonicos y vibrato, una «voz» (formantes que cambian), bombo, charles
# (ruido agudo en golpes), platillo y un fondo de ruido rosa suave; estereo (cada parte en su lado), 44,1 kHz, 16 bits. Siempre la misma
# (semilla fija). Solo numpy.
import sys, wave
import numpy as np

sal = sys.argv[1]
seg = float(sys.argv[2]) if len(sys.argv) > 2 else 20.0
fs = 44100
n = int(seg * fs)
t = np.arange(n) / fs
rng = np.random.default_rng(20261122)
L = np.zeros(n); R = np.zeros(n)

# acordes (cambian cada 2 s): fundamental y 14 armonicos que caen 1/k, con vibrato lento
acordes = [[220.0, 277.18, 329.63], [196.0, 246.94, 293.66], [174.61, 220.0, 261.63], [164.81, 207.65, 246.94]]
for i in range(int(seg // 2) + 1):
    a, b = int(i * 2 * fs), min(n, int((i + 1) * 2 * fs))
    if a >= n: break
    tt = t[a:b] - t[a]
    env = np.minimum(1, tt / 0.02) * np.exp(-tt * 0.6)
    for j, f0 in enumerate(acordes[i % 4]):
        vib = 1 + 0.003 * np.sin(2 * np.pi * 5.5 * tt + j)
        for k in range(1, 16):
            f = f0 * k
            if f > 18000: break
            s = np.sin(2 * np.pi * f * np.cumsum(vib) / fs + rng.uniform(0, 6.28)) * env / k * 0.05
            pan = 0.5 + 0.35 * np.sin(j + k)
            L[a:b] += s * (1 - pan); R[a:b] += s * pan
# la «voz»: diente de sierra filtrado por tres formantes que se mueven
f0 = 180 * (1 + 0.08 * np.sin(2 * np.pi * 0.3 * t)) * (1 + 0.004 * np.sin(2 * np.pi * 6 * t))
ph = np.cumsum(f0) / fs
voz = np.zeros(n)
for k in range(1, 60):
    fk = f0 * k
    amp = np.zeros(n)
    for fc, bw in ((700 + 300 * np.sin(2 * np.pi * 0.25 * t), 120), (1200 + 500 * np.sin(2 * np.pi * 0.17 * t), 150), (2600, 250), (3400, 300)):
        amp += np.exp(-0.5 * ((fk - fc) / bw) ** 2)
    amp *= (fk < 9000)
    voz += np.sin(2 * np.pi * k * ph) * amp / np.sqrt(k)
voz *= 0.04 * (0.6 + 0.4 * np.sin(2 * np.pi * 0.5 * t) ** 2)
L += voz; R += voz
# bombo (cada 0,5 s), charles (cada 0,25 s) y platillo (cada 4 s)
for g in np.arange(0, seg, 0.5):
    a = int(g * fs); m = min(n - a, int(0.3 * fs)); tt = np.arange(m) / fs
    s = np.sin(2 * np.pi * (50 + 90 * np.exp(-tt * 30)) * tt) * np.exp(-tt * 9) * 0.35
    L[a:a + m] += s; R[a:a + m] += s
def ruido_agudo(m, desde_hz):
    x = rng.standard_normal(m)
    X = np.fft.rfft(x); f = np.fft.rfftfreq(m, 1 / fs)
    X *= (f > desde_hz) * (1 / np.sqrt(np.maximum(f, 1) / 1000))
    return np.fft.irfft(X, m)
for g in np.arange(0.125, seg, 0.25):
    a = int(g * fs); m = min(n - a, int(0.08 * fs)); tt = np.arange(m) / fs
    s = ruido_agudo(m, 6000) * np.exp(-tt * 45) * 0.06
    L[a:a + m] += s * 1.2; R[a:a + m] += s * 0.7
for g in np.arange(1.0, seg, 4.0):
    a = int(g * fs); m = min(n - a, int(1.5 * fs)); tt = np.arange(m) / fs
    s = ruido_agudo(m, 3000) * np.exp(-tt * 2.5) * 0.05
    L[a:a + m] += s * 0.7; R[a:a + m] += s * 1.2
# fondo de ruido rosa suave (un poco de todo, como la reverberacion de una sala)
X = np.fft.rfft(rng.standard_normal(n)); f = np.fft.rfftfreq(n, 1 / fs)
X /= np.sqrt(np.maximum(f, 20)); rosa = np.fft.irfft(X, n); rosa *= 0.004 / np.std(rosa)
L += rosa; R += np.roll(rosa, 337)

x = np.stack([L, R], 1)
x *= 0.7 / np.max(np.abs(x))
s16 = np.round(x * 32767).astype('<i2')
with wave.open(sal, 'wb') as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(fs); w.writeframes(s16.tobytes())
