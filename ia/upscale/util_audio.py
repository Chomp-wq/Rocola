# -*- coding: utf-8 -*-
# Utilerias de audio del kit de upscaling. NUMPY PURO: estas funciones se prueban sin GPU
# (test_util.py), asi el unico riesgo queda en la parte del modelo (la valida 00_verificar.py).
import numpy as np, wave, subprocess, struct, os

FS = 44100

def leer_wav(path):
    """RIFF PCM16/24/32 o IEEE float32 -> float64 [n, canales], sin recortar.

    Lectura por bloques: evita retener ademas una copia completa de bytes PCM.
    Incluye WAVE_FORMAT_EXTENSIBLE de FFmpeg; float puede superar +/-1.
    """
    with open(path, 'rb') as f:
        length = os.fstat(f.fileno()).st_size
        header = f.read(12)
        if len(header) != 12 or header[:4] != b'RIFF' or header[8:] != b'WAVE':
            raise ValueError('se requiere RIFF/WAVE')
        end = struct.unpack_from('<I', header, 4)[0] + 8
        if end > length or end < 12: raise ValueError('RIFF truncado')
        fmt = data = None
        while f.tell() + 8 <= end:
            tag, size = struct.unpack('<4sI', f.read(8))
            start = f.tell()
            if start + size > end: raise ValueError('chunk WAV truncado')
            if tag == b'fmt ':
                if fmt is not None or size < 16 or size > 65536: raise ValueError('fmt WAV invalido')
                fmt = f.read(size)
            elif tag == b'data' and data is None:
                data = (start, size)
            f.seek(start + size + (size & 1))
        if fmt is None or data is None: raise ValueError('WAV sin formato/audio')
        kind, ch, fs, byte_rate, align, bits = struct.unpack_from('<HHIIHH', fmt)
        if kind == 0xfffe:
            if len(fmt) < 40 or struct.unpack_from('<H', fmt, 16)[0] < 22:
                raise ValueError('WAV extensible incompleto')
            valid = struct.unpack_from('<H', fmt, 18)[0]
            guid = fmt[24:40]
            if guid[4:] != bytes.fromhex('00001000800000aa00389b71') or valid not in (0, bits):
                raise ValueError('subformato WAV no admitido')
            kind = struct.unpack_from('<I', guid)[0]
        if (not 1 <= ch <= 32 or fs <= 0 or
                not ((kind == 1 and bits in (16, 24, 32)) or (kind == 3 and bits == 32))):
            raise ValueError('solo PCM16/24/32 o float32')
        sw = bits // 8
        if align != ch * sw or byte_rate != fs * align or data[1] % align:
            raise ValueError('alineacion WAV invalida')
        n = data[1] // align
        x = np.empty((n, ch), dtype=np.float64)
        f.seek(data[0])
        block = max(1, 65536 // ch)
        for p in range(0, n, block):
            frames = min(block, n-p)
            raw = f.read(frames * align)
            if len(raw) != frames * align: raise ValueError('audio WAV truncado')
            if kind == 3:
                values = np.frombuffer(raw, '<f4')
            elif sw == 3:
                octets = np.frombuffer(raw, np.uint8).reshape(-1, 3).astype(np.int32)
                values = octets[:, 0] | (octets[:, 1] << 8) | (octets[:, 2] << 16)
                values = ((values ^ 0x800000) - 0x800000) / 8388608.0
            else:
                values = np.frombuffer(raw, '<i2' if sw == 2 else '<i4') / float(1 << (bits-1))
            if not np.isfinite(values).all(): raise ValueError('WAV no finito')
            x[p:p+frames] = values.reshape(frames, ch)
    return x, fs

def ganancia_wav(x):
    """Ganancia sin amplificar que deja margen PCM16; valida antes de exportar."""
    if x.ndim != 2 or x.shape[1] < 1:
        raise ValueError("se esperaba audio [muestras, canales]")
    if not np.issubdtype(x.dtype, np.number) or not np.isrealobj(x):
        raise ValueError("el audio contiene valores no numericos o no finitos")
    bloque = max(1, 65536 // x.shape[1])
    # Validar todo ANTES de abrir: una muestra no finita no debe truncar una salida existente.
    pico = 0.0
    for p in range(0, len(x), bloque):
        parte = x[p:p + bloque]
        if not np.isfinite(parte).all():
            raise ValueError("el audio contiene valores no numericos o no finitos")
        pico = max(pico, float(np.max(np.abs(parte.astype(np.float64)))))
    return min(1.0, 0.98 / pico) if pico else 1.0


def escribir_wav(path, x, fs=FS):
    """WAV PCM16 con ganancia comun anti-recorte y dither TPDF.

    Conserva la dinamica y el balance L/R; no es un limitador true peak.
    Devuelve la ganancia aplicada para diagnostico. No modifica x.
    """
    if fs <= 0:
        raise ValueError("se esperaba fs > 0")
    ganancia = ganancia_wav(x)
    bloque = max(1, 65536 // x.shape[1])
    # RNG local: reproducible y sin alterar el azar del entrenamiento ni otros trabajos.
    azar = np.random.RandomState(0x524941)
    with wave.open(path, 'wb') as w:
        w.setnchannels(x.shape[1]); w.setsampwidth(2); w.setframerate(fs)
        w.setnframes(len(x))
        for p in range(0, len(x), bloque):
            parte = x[p:p + bloque].astype(np.float64) * ganancia
            ruido = azar.random_sample((parte.size, 2))
            dither = (ruido[:, 0] - ruido[:, 1]).reshape(parte.shape)
            # Silencio digital exacto permanece en cero, incluidos canales mudos.
            dither[parte == 0] = 0
            y = np.floor(parte * 32768.0 + dither + 0.5).astype('<i2')
            w.writeframesraw(y.tobytes())
    return ganancia

def decodificar_a_wav(ffmpeg, origen, destino, timeout=20 * 60):
    """Cualquier formato -> WAV float32 44.1k estereo, sin cuantizacion intermedia."""
    subprocess.run([ffmpeg, '-nostdin', '-y', '-v', 'error', '-i', origen, '-ac', '2',
                    '-ar', str(FS), '-c:a', 'pcm_f32le', destino], check=True,
                   stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                   stderr=subprocess.PIPE, timeout=timeout)


def escribir_wav_float(path, x, fs=FS):
    """Cache IEEE float32: sin limitacion, dither ni nueva compresion con perdida.

    La proteccion de picos se aplica al reproducir, despues del DSP/remuestreo.
    Valida todo antes de abrir el destino; RIFF limitado a menos de 4 GiB.
    """
    ganancia_wav(x)  # validacion de forma y finitos; no aplicar esa ganancia
    ch = x.shape[1]
    size = x.size * 4
    if ch > 32 or not isinstance(fs, (int, np.integer)) or fs <= 0 or size > 0xffffffff-50:
        raise ValueError('formato/longitud excede WAV RIFF')
    block = max(1, 65536 // ch)
    for p in range(0,len(x),block):
        if np.max(np.abs(x[p:p+block])) > np.finfo(np.float32).max:
            raise ValueError('audio excede float32')
    with open(path,'wb') as f:
        f.write(b'RIFF'+struct.pack('<I',size+50)+b'WAVE')
        f.write(b'fmt '+struct.pack('<IHHIIHHH',18,3,ch,fs,fs*ch*4,ch*4,32,0))
        f.write(b'fact'+struct.pack('<II',4,len(x)))
        f.write(b'data'+struct.pack('<I',size))
        for p in range(0,len(x),block):
            f.write(x[p:p+block].astype('<f4').tobytes())

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
