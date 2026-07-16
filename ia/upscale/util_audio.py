# -*- coding: utf-8 -*-
# Utilerias de audio del kit de upscaling. NUMPY PURO: estas funciones se prueban sin GPU
# (test_util.py), asi el unico riesgo queda en la parte del modelo (la valida 00_verificar.py).
import numpy as np, wave, subprocess

FS = 44100

def leer_wav(path):
    """WAV 16/32 bits -> (float64 [n, canales] en -1..1, fs)."""
    w = wave.open(path, 'rb')
    fs, ch, sw, n = w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
    raw = w.readframes(n); w.close()
    if sw == 2: x = np.frombuffer(raw, '<i2').astype(np.float64) / 32768.0
    elif sw == 4: x = np.frombuffer(raw, '<i4').astype(np.float64) / 2147483648.0
    else: raise ValueError('solo WAV de 16 o 32 bits')
    return x.reshape(-1, ch), fs

def escribir_wav(path, x, fs=FS):
    """float [n, canales] -1..1 -> WAV 16 bits."""
    y = (np.clip(x, -1.0, 1.0) * 32767.0).astype('<i2')
    w = wave.open(path, 'wb'); w.setnchannels(x.shape[1]); w.setsampwidth(2); w.setframerate(fs)
    w.writeframes(y.tobytes()); w.close()

def decodificar_a_wav(ffmpeg, origen, destino):
    """Cualquier formato (mp3/flac/etc) -> WAV 44.1k estereo 16 bits, via ffmpeg."""
    subprocess.run([ffmpeg, '-y', '-v', 'error', '-i', origen, '-ac', '2', '-ar', str(FS),
                    '-sample_fmt', 's16', destino], check=True)

def acantilado_hz(x, fs=FS):
    """Corte del codec (Hz) por espectro promedio: la frecuencia mas alta con energia
    comparable a la banda media (mismo criterio que el detector de la rocola)."""
    mono = x.mean(axis=1) if x.ndim == 2 else x
    N = 4096; win = np.hanning(N)
    acc = None; cnt = 0
    for p in range(0, len(mono) - N, N):
        X = np.abs(np.fft.rfft(mono[p:p+N] * win)) ** 2
        acc = X if acc is None else acc + X; cnt += 1
    if cnt == 0: return 0.0
    m = acc / cnt
    binhz = fs / N
    ref = m[int(4000/binhz):int(8000/binhz)].mean()
    if ref <= 0: return 0.0
    hz = min(21000.0, fs / 2 * 0.96) - 300.0
    while hz >= 8000.0:
        if m[int(hz/binhz):int((hz+300)/binhz)].mean() > ref * 0.002:
            return hz + 300.0
        hz -= 300.0
    return 8000.0

def igualar_rms(mejorada, original):
    """Escala 'mejorada' al RMS de 'original': el A/B se escucha JUSTO (mas fuerte enganya)."""
    ro = np.sqrt((original ** 2).mean()); rm = np.sqrt((mejorada ** 2).mean())
    return mejorada if rm < 1e-9 else mejorada * (ro / rm)

def por_trozos(x, proc, fs=FS, seg=10.0, sol=0.5):
    """Procesa [n, ch] en trozos de 'seg' segundos con 'sol' de solape y fundido cruzado
    lineal (para que la GPU no se quede sin memoria en canciones largas). Conserva longitud."""
    n = len(x); L = int(seg * fs); O = max(1, int(sol * fs))
    if n <= L: return proc(x)[:n]
    out = np.zeros_like(x); wsum = np.zeros((n, 1))
    p = 0
    while p < n:
        q = min(p + L, n)
        y = proc(x[p:q])[: q - p]
        w = np.ones((q - p, 1))
        if p > 0: w[:min(O, q - p), 0] = np.linspace(0.0, 1.0, min(O, q - p))
        if q < n: w[-min(O, q - p):, 0] = np.minimum(w[-min(O, q - p):, 0], np.linspace(1.0, 0.0, min(O, q - p)))
        out[p:q] += y * w; wsum[p:q] += w
        if q >= n: break
        p = q - O
    return out / np.maximum(wsum, 1e-9)
