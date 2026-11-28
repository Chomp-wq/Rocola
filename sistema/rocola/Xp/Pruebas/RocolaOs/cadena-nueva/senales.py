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
print("SENALES listas en", out)
