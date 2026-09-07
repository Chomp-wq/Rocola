# -*- coding: utf-8 -*-
# Utilerias de audio del kit de upscaling. NUMPY PURO: estas funciones se prueban sin GPU
# (test_util.py), asi el unico riesgo queda en la parte del modelo (la valida 00_verificar.py).
import numpy as np, wave, subprocess

FS = 44100

def leer_wav(path):
    """WAV 16/32 bits -> (float64 [n, canales] en -1..1, fs)."""
    with wave.open(path, 'rb') as w:
        fs, ch, sw, n = w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
        raw = w.readframes(n)
    if sw == 2: x = np.frombuffer(raw, '<i2').astype(np.float64) / 32768.0
    elif sw == 4: x = np.frombuffer(raw, '<i4').astype(np.float64) / 2147483648.0
    else: raise ValueError('solo WAV de 16 o 32 bits')
    return x.reshape(-1, ch), fs

def escribir_wav(path, x, fs=FS):
    """float [n, canales] -1..1 -> WAV 16 bits."""
    if x.ndim != 2 or x.shape[1] < 1 or fs <= 0:
        raise ValueError("se esperaba audio [muestras, canales] y fs > 0")
    if not np.issubdtype(x.dtype, np.number) or not np.isrealobj(x):
        raise ValueError("el audio contiene valores no numericos o no finitos")
    bloque = max(1, 65536 // x.shape[1])
    # Validar todo ANTES de abrir: una muestra no finita no debe truncar una salida existente.
    for p in range(0, len(x), bloque):
        if not np.isfinite(x[p:p + bloque]).all():
            raise ValueError("el audio contiene valores no numericos o no finitos")
    with wave.open(path, 'wb') as w:
        w.setnchannels(x.shape[1]); w.setsampwidth(2); w.setframerate(fs)
        w.setnframes(len(x))
        for p in range(0, len(x), bloque):
            y = (np.clip(x[p:p + bloque], -1.0, 1.0) * 32767.0).astype('<i2')
            w.writeframesraw(y.tobytes())

def decodificar_a_wav(ffmpeg, origen, destino, timeout=20 * 60):
    """Cualquier formato (mp3/flac/etc) -> WAV 44.1k estereo 16 bits, via ffmpeg."""
    subprocess.run([ffmpeg, '-nostdin', '-y', '-v', 'error', '-i', origen, '-ac', '2',
                    '-ar', str(FS), '-sample_fmt', 's16', destino], check=True,
                   stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                   stderr=subprocess.PIPE, timeout=timeout)

def acantilado_hz(x, fs=FS):
    """Corte del codec (Hz) por espectro promedio: la frecuencia mas alta con energia
    comparable a la banda media (mismo criterio que el detector de la rocola)."""
    # No mezclar L/R antes de medir: una grabacion con agudos en contrafase se cancelaria y
    # pareceria mutilada. Promediar la POTENCIA espectral de los canales conserva esa energia.
    if x.ndim not in (1, 2) or x.size == 0:
        return 0.0
    canales = x.T if x.ndim == 2 else x.reshape(1, -1)
    if canales.shape[0] == 0:
        return 0.0
    N = 4096; win = np.hanning(N)
    acc = None; cnt = 0
    for p in range(0, canales.shape[1] - N + 1, N):
        X = np.abs(np.fft.rfft(canales[:, p:p+N] * win, axis=1)) ** 2
        X = X.mean(axis=0)
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
    if fs <= 0 or seg <= 0 or sol < 0 or seg <= sol:
        raise ValueError("se requiere fs > 0 y seg > sol >= 0")
    if x.ndim != 2:
        raise ValueError("se esperaba audio [muestras, canales]")
    n = len(x); L = int(seg * fs); O = int(sol * fs)
    if L <= O:
        raise ValueError("el trozo debe ser mayor que el solape")
    if n == 0:
        return x.copy()
    if n <= L:
        y = np.asarray(proc(x))
        if y.ndim != 2 or y.shape[0] < n or y.shape[1] != x.shape[1]:
            raise ValueError("el modelo devolvio forma/longitud incompatible")
        if not np.issubdtype(y.dtype, np.number) or not np.isrealobj(y) or not np.isfinite(y[:n]).all():
            raise ValueError("el modelo devolvio valores no numericos o no finitos")
        return y[:n]
    out = np.zeros_like(x); wsum = np.zeros((n, 1))
    p = 0
    while p < n:
        q = min(p + L, n)
        y = np.asarray(proc(x[p:q]))
        if y.ndim != 2 or y.shape[0] < q - p or y.shape[1] != x.shape[1]:
            raise ValueError("el modelo devolvio forma/longitud incompatible")
        y = y[: q - p]
        if not np.issubdtype(y.dtype, np.number) or not np.isrealobj(y) or not np.isfinite(y).all():
            raise ValueError("el modelo devolvio valores no numericos o no finitos")
        w = np.ones((q - p, 1))
        if O and p > 0: w[:min(O, q - p), 0] = np.linspace(0.0, 1.0, min(O, q - p))
        if O and q < n: w[-min(O, q - p):, 0] = np.minimum(w[-min(O, q - p):, 0], np.linspace(1.0, 0.0, min(O, q - p)))
        out[p:q] += y * w; wsum[p:q] += w
        if q >= n: break
        p = q - O
    # Ambos buffers son locales. Evita otro array del tamano de la cancion y otro
    # de pesos sin cambiar el orden aritmetico ni la precision float64 de Apollo.
    np.maximum(wsum, 1e-9, out=wsum)
    if out.dtype == np.float64:
        np.divide(out, wsum, out=out)
        return out
    # Con float32 la division original PROMUEVE a float64; no redondear in-place.
    return out / wsum

def mezclar(mejorada, original, m):
    """Wet/dry: combina 'mejorada' (Apollo) con 'original' en proporcion m (0..1).
    m=0 -> original; m=1 -> puro Apollo. Se igualan longitud y RMS para que sea justo y
    la mezcla no cambie el volumen. Suaviza el vaiven de la IA en material muy danado."""
    n = min(len(mejorada), len(original))
    a = igualar_rms(mejorada[:n], original[:n])          # Apollo al volumen del original
    b = original[:n]
    if m <= 0.0: return b.copy()
    if m >= 1.0: return a
    return igualar_rms(m * a + (1.0 - m) * b, b)          # mezcla, re-igualada (volumen estable entre niveles)

def mezcla_auto(cliff_hz):
    """Cuanta IA aplicar segun que tan danada esta: mientras MENOS sobrevivio (corte bajo),
    mas suave (la IA inventa mas y se nota mas el vaiven)."""
    if cliff_hz <= 0:     return 0.60
    if cliff_hz <= 9000:  return 0.55     # brutal: el modelo alucina mucho -> suave
    if cliff_hz <= 12500: return 0.70
    if cliff_hz <= 15500: return 0.82
    return 1.00                            # poco danada: la IA acierta -> a fondo
