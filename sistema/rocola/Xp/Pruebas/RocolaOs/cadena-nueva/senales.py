#!/usr/bin/env python3
# senales.py CARPETA: las senales INVENTADAS de la prueba de la cadena nueva (nada de musica de verdad), en WAV de 16 bits a 44,1 kHz:
#   fuerte.wav   «musica» aplastada como un master moderno (ruido rosa, bajo, acordes, bombo y caja, recortada): unos -9 LUFS; en MP3 sus picos
#                pasan de 0 dBFS al decodificar (lo que hoy se recorta a 16 bits)
#   medio.wav    la misma sin aplastar, con el pico a -0,01 dBFS: unos -16 LUFS
#   rango.wav    mucho rango: ruido rosa bajito (unos -36 dBFS) con golpes cortos casi a 0 dBFS: no cabe a -18 LUFS bajo -1 dBTP (queda mas bajo)
#   disco.wav    30 s seguidos (la misma «musica» media) para partir en dos pistas de un disco continuo
#   corto.wav    12 s de la media y corto2.wav 12 s de la fuerte, para la escena con la rocola
import sys, wave
import numpy as np
fs = 44100
out = sys.argv[1]
rng = np.random.default_rng(1)

def rosa(n, ch=2):
    w = rng.standard_normal((n, ch))
    f = np.fft.rfftfreq(n, 1 / fs); f[0] = 1
    for c in range(ch):
        X = np.fft.rfft(w[:, c]); X /= np.sqrt(f); X[f < 30] = 0; X[f > 16000] *= 0.3; w[:, c] = np.fft.irfft(X, n)
    return w / np.max(np.abs(w))

def musica(seg):
    n = int(fs * seg); t = np.arange(n) / fs
    bass = 0.6 * np.sin(2 * np.pi * 55 * t) * (0.6 + 0.4 * np.sin(2 * np.pi * 0.5 * t))
    chord = sum(0.15 * np.sin(2 * np.pi * fr * t) for fr in (220, 277.2, 329.6, 440, 880, 1760))
    kick = np.zeros(n)
    for k in range(0, n, fs // 2):
        L = min(4000, n - k); tt = np.arange(L) / fs
        kick[k:k + L] += 1.2 * np.exp(-tt * 30) * np.sin(2 * np.pi * (50 + 80 * np.exp(-tt * 40)) * tt)
    snare = np.zeros((n, 2))
    for k in range(fs // 4, n, fs // 2):
        L = min(3000, n - k); tt = np.arange(L) / fs
        snare[k:k + L] += 0.8 * np.exp(-tt * 50)[:, None] * rng.standard_normal((L, 2))
    x = 0.5 * rosa(n) + snare + (bass + chord + kick)[:, None]
    return x / np.sqrt(np.mean(x ** 2))

def guarda(nombre, y):
    s = np.round(np.clip(y, -1, 32767 / 32768) * 32767).astype('<i2')
    with wave.open(out + "/" + nombre, "wb") as wf:
        wf.setnchannels(2); wf.setsampwidth(2); wf.setframerate(fs); wf.writeframes(s.tobytes())

x = musica(30)
guarda("fuerte.wav", np.clip(x * 0.30, -1, 1))
medio = x / np.max(np.abs(x)) * 0.999
guarda("medio.wav", medio)
guarda("disco.wav", medio)
guarda("corto.wav", medio[:12 * fs])
guarda("corto2.wav", np.clip(x * 0.30, -1, 1)[12 * fs:24 * fs])
n = 20 * fs
r = 0.016 * rosa(n)
for k in range(fs // 3, n - 2000, fs // 2):
    L = 600; tt = np.arange(L) / fs
    r[k:k + L] += (0.95 * np.exp(-tt * 400) * np.sin(2 * np.pi * 180 * tt))[:, None]
guarda("rango.wav", r)
# (28-11-2026, a1) el ecualizador del dueno sobre una dinamica y una aplastada (las de la revision, s2_picos.py): 20 s, mono en los dos canales
#   aplastada.wav  bombo, bajo de cumbia de 41-82 Hz y ruido rosa, recortado (tanh) como un master de hoy: el grave llega al pico
#   bolero.wav     requinto y voz (ruido rosa sin grave, con frases), un bajo suave y rasgueos que dan los picos: el grave muy lejos del pico
#   corto_baja.wav la media de 12 s, 3 dB mas baja (la tercera pista de un disco que no es recopilatorio)
#   largo_fuerte.wav y largo_suave.wav: 32 s, la fuerte y la media (7 dB entre ellas: un recopilatorio para la regla de XP, que pide 30 s o mas)
r2 = np.random.default_rng(7)
n2 = 20 * fs; t2 = np.arange(n2) / fs
def rosa1(n):
    X = np.fft.rfft(r2.standard_normal(n)); f = np.fft.rfftfreq(n, 1 / fs); f[0] = 1; X = X / np.sqrt(f); X[0] = 0
    return np.fft.irfft(X, n)
def hp150(x):
    w = 2 * np.pi * 150.0 / fs; c = np.cos(w); al = np.sin(w) / (2 * 0.707); a0 = 1 + al
    b = np.array([(1 + c) / 2, -(1 + c), (1 + c) / 2]) / a0; a1, a2 = -2 * c / a0, (1 - al) / a0
    y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        v = x[i]; o = b[0] * v + b[1] * x1 + b[2] * x2 - a1 * y1 - a2 * y2; x2 = x1; x1 = v; y2 = y1; y1 = o; y[i] = o
    return y
x = np.zeros(n2)
for k0 in range(0, n2, int(0.5 * fs)):
    m = min(n2 - k0, int(0.4 * fs)); tt = np.arange(m) / fs
    x[k0:k0 + m] += 0.9 * np.sin(2 * np.pi * np.cumsum(50 + 80 * np.exp(-tt / 0.02)) / fs) * np.exp(-tt / 0.15)
for i, k0 in enumerate(range(0, n2, int(0.25 * fs))):
    m = min(n2 - k0, int(0.24 * fs)); tt = np.arange(m) / fs; f = [41.2, 55.0, 61.7, 82.4][i % 4]
    x[k0:k0 + m] += 0.5 * (np.sin(2 * np.pi * f * tt) + 0.4 * np.sin(4 * np.pi * f * tt)) * np.exp(-tt / 0.3)
rr = rosa1(n2); x = x + rr / np.std(rr) * 0.25
x = np.tanh(1.8 * x) / np.tanh(1.8); x = x / np.max(np.abs(x)) * 0.98
guarda("aplastada.wav", np.stack([x, x], axis=1))
rr = hp150(rosa1(n2))
env = 0.35 + 0.65 * (0.5 + 0.5 * np.sin(2 * np.pi * t2 / 4.0)) ** 2
x = rr / np.std(rr) * 0.08 * env
for i, k0 in enumerate(range(0, n2, int(0.6 * fs))):
    m = min(n2 - k0, int(0.55 * fs)); tt = np.arange(m) / fs
    x[k0:k0 + m] += 0.12 * np.sin(2 * np.pi * [55.0, 73.4, 82.4][i % 3] * tt) * np.exp(-tt / 0.4)
for k0 in range(int(0.3 * fs), n2, int(1.2 * fs)):
    m = int(0.05 * fs); x[k0:k0 + m] += r2.standard_normal(m) * np.exp(-np.arange(m) / (0.006 * fs)) * 0.5
x = x / np.max(np.abs(x)) * 0.98
guarda("bolero.wav", np.stack([x, x], axis=1))
guarda("corto_baja.wav", medio[:12 * fs] * 0.708)
# y dos de 32 s (la regla de los recopilatorios solo cuenta las de 30 s o mas): la fuerte y la media de otra «musica», 7 dB entre ellas
x32 = musica(32)
guarda("largo_fuerte.wav", np.clip(x32 * 0.30, -1, 1))
guarda("largo_suave.wav", x32 / np.max(np.abs(x32)) * 0.999)
print("SENALES listas en", out)
