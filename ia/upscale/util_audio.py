# -*- coding: utf-8 -*-
# Utilerias de audio del kit de upscaling. NUMPY PURO: estas funciones se prueban sin GPU
# (test_util.py), asi el unico riesgo queda en la parte del modelo (la valida 00_verificar.py).
import numpy as np, wave, subprocess, struct, os, json, math, tempfile, threading

FS = 44100
# ffmpeg y ffprobe son programas de consola: lanzados desde pythonw (el motor) Windows les abriria
# una ventana por cada llamada. Fuera de Windows vale 0.
NOWIN = getattr(subprocess, "CREATE_NO_WINDOW", 0)

# =============================================================================================
#  REMUESTREO A 44,1 kHz PARA APOLLO (el modelo solo trabaja a 44,1 kHz)
#
#  Una cancion que ya viene a 44,1 kHz NO se remuestrea: sale igual bit a bit con cualquiera de
#  estas opciones (comprobado con FFmpeg 6.1 y 7.0 en WAV, MP3, 16 bits, mono y 5.1). Las demas si,
#  y el remuestreador que FFmpeg usa por defecto (swr) hace dano MEDIDO donde mas importa:
#    - bajando de 48/96 kHz: -1,28 dB a 20 kHz, y lo que hay entre 22 y 24 kHz vuelve plegado
#      a 20-22 kHz a solo -14..-36 dB;
#    - subiendo de 22,05/24/32 kHz (los archivos flojos, que son los que Apollo repara): deja
#      IMAGENES justo encima del corte original (un tono de 10 kHz a 22,05 kHz sale reflejado en
#      12,05 kHz a -25 dB). El detector del corte las toma por musica: a 32 kHz con musica hasta
#      15 kHz mide 17,4 kHz y la mezcla sube de 0,82 a 1,00.
#  SoXR de alta precision (28 bits) queda plano hasta 20,5 kHz y sin plegados ni imagenes medibles
#  (-158 dB o menos). Si el FFmpeg instalado no lo trae, un swr reforzado (filtro de 256 puntos)
#  deja los plegados en -125 dB o menos. Detalles y mediciones: MEJORAS_AUDIO_Y_PENDIENTES, 1.16.
# =============================================================================================
REMUESTREO_HEREDADO = "swr-defecto"    # lo de siempre: '-ar 44100' sin filtro propio
REMUESTREOS = (
    ("soxr-p28", "aresample=resampler=soxr:precision=28"),
    ("swr-f256-c092-k12", "aresample=filter_size=256:cutoff=0.92:kaiser_beta=12"),
)
_CAPACIDAD = {}                        # firma del ffmpeg -> (id, filtro)
_CAPACIDAD_CERROJO = threading.Lock()


def args_decodificar(filtro=None):
    """Argumentos de SALIDA de FFmpeg para Apollo: estereo, 44,1 kHz, float32 sin cuantizar.

    Con ``filtro=None`` son exactamente los de antes (el remuestreador por defecto)."""
    salida = ["-ac", "2"]
    if filtro:
        salida += ["-af", filtro]
    return salida + ["-ar", str(FS), "-c:a", "pcm_f32le"]


def firma_herramienta(ruta):
    """Identidad de un ejecutable: si cambia (otro FFmpeg), hay que volver a probarlo."""
    st = os.stat(ruta)
    return (os.path.abspath(ruta), st.st_size, st.st_mtime_ns)


def _prueba_remuestreo(ffmpeg, filtro, carpeta, timeout):
    """Convierte 0,1 s de dos tonos a 48 kHz y comprueba formato, largo y nivel de lo que sale."""
    src = os.path.join(carpeta, "prueba_48k.wav")
    dst = os.path.join(carpeta, "prueba_441.wav")
    if not os.path.exists(src):
        t = np.arange(4800) / 48000.0
        escribir_wav_float(src, np.stack((0.25 * np.sin(2 * np.pi * 1000 * t),
                                          0.25 * np.sin(2 * np.pi * 1500 * t)), axis=1), 48000)
    try:
        subprocess.run([ffmpeg, "-nostdin", "-y", "-v", "error", "-i", src] +
                       args_decodificar(filtro) + [dst], check=True, stdin=subprocess.DEVNULL,
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=timeout,
                       creationflags=NOWIN)
        y, fs = leer_wav(dst)
    except Exception:
        return False
    finally:
        try: os.remove(dst)
        except OSError: pass
    if fs != FS or y.ndim != 2 or y.shape[1] != 2 or abs(len(y) - 4410) > 1:
        return False
    centro = y[441:-441]
    # 0,25 de pico es 0,1768 eficaz; se tolera 0,1 dB (los extremos llevan el arranque del filtro).
    rms = np.sqrt((centro ** 2).mean(axis=0))
    return bool(np.all(np.abs(20 * np.log10(np.maximum(rms, 1e-12) / (0.25 / np.sqrt(2)))) < 0.1))


def remuestreador(ffmpeg, timeout=30):
    """El mejor remuestreador que ESTE FFmpeg ejecuta de verdad: ``(id, filtro)``.

    No se da por hecho que un FFmpeg traiga SoXR: se prueba convirtiendo de verdad. Si ninguna
    opcion funciona queda el de siempre (``filtro=None``). Se recuerda por ejecutable (ruta,
    tamano y fecha): otro FFmpeg en la misma ruta se vuelve a probar."""
    try:
        clave = firma_herramienta(ffmpeg)
    except OSError:
        return (REMUESTREO_HEREDADO, None)
    with _CAPACIDAD_CERROJO:
        if clave in _CAPACIDAD:
            return _CAPACIDAD[clave]
        elegido = (REMUESTREO_HEREDADO, None)
        with tempfile.TemporaryDirectory(prefix="rocola_remuestreo_") as carpeta:
            for ident, filtro in REMUESTREOS:
                if _prueba_remuestreo(ffmpeg, filtro, carpeta, timeout):
                    elegido = (ident, filtro)
                    break
        _CAPACIDAD[clave] = elegido
        return elegido


def sondeo_audio(ffprobe, ruta, timeout=30):
    """``(duracion, frecuencias)`` de TODAS las pistas de audio con una sola llamada a ffprobe.

    ``(None, [])`` si no se pudo leer. Una frecuencia que no se sabe cuenta como 0."""
    try:
        r = subprocess.run([ffprobe, "-v", "error", "-select_streams", "a",
                            "-show_entries", "stream=index,sample_rate:format=duration",
                            "-of", "json", ruta], capture_output=True, text=True,
                           stdin=subprocess.DEVNULL, timeout=timeout, creationflags=NOWIN)
        if r.returncode != 0:
            return (None, [])
        dato = json.loads(r.stdout)
        pistas = dato.get("streams") or []
        frecuencias = []
        for p in pistas:
            try:
                frecuencias.append(int(p.get("sample_rate", 0)))
            except (TypeError, ValueError):
                frecuencias.append(0)
        dur = float(dato.get("format", {}).get("duration", 0))
        return ((dur if pistas and math.isfinite(dur) and dur > 0 else None), frecuencias)
    except Exception:
        return (None, [])


def sin_remuestreo(frecuencias):
    """True solo si se SABE que todas las pistas vienen ya a 44,1 kHz: ahi el remuestreador no
    interviene y el resultado es el mismo con cualquiera."""
    return bool(frecuencias) and all(f == FS for f in frecuencias)

def leer_wav(path, reservar=None):
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
        x = np.empty((n, ch), dtype=np.float64) if reservar is None else reservar((n, ch), False)
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

def decodificar_a_wav(ffmpeg, origen, destino, timeout=20 * 60, filtro=False):
    """Cualquier formato -> WAV float32 44.1k estereo, sin cuantizacion intermedia.

    Por defecto usa el mejor remuestreador que tenga este FFmpeg (ver ``remuestreador``);
    ``filtro=None`` fuerza el de siempre."""
    if filtro is False:
        filtro = remuestreador(ffmpeg)[1]
    subprocess.run([ffmpeg, '-nostdin', '-y', '-v', 'error', '-i', origen] +
                   args_decodificar(filtro) + [destino], check=True,
                   stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                   stderr=subprocess.PIPE, timeout=timeout, creationflags=NOWIN)


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

def analizar_corte(x, fs=FS):
    """Confianza de corte: exige caida abrupta, energia util y concordancia de ventanas.

    No identifica un master sin perdida. Material oscuro/incierto conserva el original en automatico.
    """
    desconocido = {"corte_hz": 0.0, "confianza": 0.0, "reconstruir": False, "motivo": "sin evidencia"}
    if x.ndim not in (1, 2) or x.size == 0 or not np.isfinite(x).all(): return desconocido
    canales = x.T if x.ndim == 2 else x.reshape(1, -1)
    n = 4096
    if canales.shape[1] < n: return desconocido
    # Hasta 96 ventanas distribuidas: coste acotado y sin favorecer intro/final.
    posiciones = np.unique(np.linspace(0, canales.shape[1] - n, min(96, canales.shape[1] // n), dtype=int))
    win = np.hanning(n); bhz = fs / n; espectros = []
    for p in posiciones:
        potencia = np.abs(np.fft.rfft(canales[:, p:p+n] * win, axis=1)) ** 2
        m = potencia.mean(axis=0)
        cuerpo = m[int(100/bhz):int(4000/bhz)].mean()
        ref = m[int(4000/bhz):int(8000/bhz)].mean()
        if ref > 1e-10 and ref > cuerpo * 3e-5: espectros.append(m)
    if not espectros: return desconocido
    espectros = np.asarray(espectros); m = espectros.mean(axis=0)
    ref = m[int(4000/bhz):int(8000/bhz)].mean()
    maxhz = min(20500, fs * .47)
    encontrados = []
    for hz in np.arange(9000, maxhz - 750, 150):
        abajo = slice(int((hz-600)/bhz), int(hz/bhz))
        arriba = slice(int((hz+150)/bhz), int((hz+750)/bhz))
        previo = m[abajo].mean(); posterior = m[arriba].mean()
        if previo <= ref * .003 or posterior >= previo * .03: continue
        concordancia = np.mean(espectros[:, arriba].mean(axis=1) < espectros[:, abajo].mean(axis=1) * .06)
        if concordancia >= .7:
            encontrados.append((hz, float(concordancia)))
    if not encontrados:
        return {"corte_hz": float(acantilado_hz(x, fs)), "confianza": 0.0, "reconstruir": False, "motivo": "sin caida abrupta fiable"}
    # Primera caida, no el borde superior de una ventana que ya pisa el silencio.
    hz, confianza = min(encontrados, key=lambda p: p[0])
    return {"corte_hz": float(hz), "confianza": confianza, "reconstruir": bool(hz < 18500), "motivo": "caida abrupta consistente"}


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

def por_trozos(x, proc, fs=FS, seg=10.0, sol=0.5, reservar=None):
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
    if reservar is not None and x.dtype != np.float64:
        raise ValueError("el almacenamiento externo requiere float64")
    out = np.zeros_like(x) if reservar is None else reservar(x.shape, True)
    # NO SE GUARDA UN MAPA DE PESOS DEL LARGO DE LA CANCION.
    #
    # Antes habia un wsum de n muestras -ochenta megas en una cancion de cuatro minutos, medido- que solo
    # servia para dividir al final. No hace falta: con trozos que avanzan mas de lo que se solapan, cada
    # muestra la tocan COMO MUCHO DOS trozos, y donde se tocan los dos pesos suman siempre LO MISMO (una
    # rampa que sube mas la que baja). Fuera de esos solapes el peso es exactamente 1, y dividir por 1 no
    # cambia ni un bit.
    #
    # Asi que se apuntan solo los solapes -unos pocos numeros- y se divide ahi. El resultado sale IDENTICO
    # byte a byte al de antes; lo comprueba test_mezcla_memoria.py.
    #
    # Si alguien pide trozos que se pisan de tres en tres (seg apenas mayor que sol), esa cuenta corta ya
    # no vale y se hace como siempre, con el mapa entero. Vale mas gastar memoria que devolver otra cosa.
    acotado = (L - O >= O)
    wsum = None if acotado else (np.zeros((n, 1)) if reservar is None else reservar((n, 1), True))
    solapes = []
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
        out[p:q] += y * w
        if acotado:
            if O and q < n: solapes.append(q)
        else: wsum[p:q] += w
        if q >= n: break
        p = q - O
    # Los buffers son locales. Se conserva el orden aritmetico y la precision float64 de Apollo.
    if acotado:
        # Lo que suman los dos pesos en un solape: siempre el mismo vector, y siempre de O muestras.
        comb = np.maximum((np.linspace(1.0, 0.0, O) + np.linspace(0.0, 1.0, O)).reshape(O, 1), 1e-9) if O else None
        if out.dtype == np.float64:
            for q in solapes: np.divide(out[q - O:q], comb, out=out[q - O:q])
            return out
        # Con float32 la division original PROMUEVE a float64; fuera del solape, dividir por 1.0 es
        # exactamente pasar a float64, asi que se hace de una y solo se divide en los solapes.
        res = out.astype(np.float64)
        for q in solapes: np.divide(out[q - O:q], comb, out=res[q - O:q])
        return res
    np.maximum(wsum, 1e-9, out=wsum)
    if out.dtype == np.float64:
        np.divide(out, wsum, out=out)
        return out
    # Con float32 la division original PROMUEVE a float64; no redondear in-place.
    return out / wsum

def mezclar(mejorada, original, m, optimizar=True):
    """Wet/dry: combina 'mejorada' (Apollo) con 'original' en proporcion m (0..1).
    m=0 -> original; m=1 -> puro Apollo. Se igualan longitud y RMS para que sea justo y
    la mezcla no cambie el volumen. Suaviza el vaiven de la IA en material muy danado."""
    n = min(len(mejorada), len(original))
    a = igualar_rms(mejorada[:n], original[:n])          # Apollo al volumen del original
    b = original[:n]
    if m <= 0.0: return b.copy()
    if m >= 1.0: return a
    if optimizar and a.dtype == np.float64 and b.dtype == np.float64 and 0.0 < m < 1.0:
        # Mismas operaciones, precision y reducciones globales. Reusar solo memoria
        # propia: igualar_rms devuelve la entrada cuando la senal es casi muda.
        # Mantener tambien el layout de arrays no contiguos/Fortran: su reduccion
        # puede seguir otro orden. Esos casos conservan la expresion original.
        if a.flags.c_contiguous and b.flags.c_contiguous:
            if np.shares_memory(a, mejorada) or np.shares_memory(a, original) or not a.flags.writeable:
                a = a.copy()
            np.multiply(m, a, out=a)
            dry = (1.0 - m) * b
            np.add(a, dry, out=a)
            del dry
            ro = np.sqrt((b ** 2).mean()); rm = np.sqrt((a ** 2).mean())
            if not rm < 1e-9:  # conservar tambien la propagacion de NaN del camino original
                np.multiply(a, ro / rm, out=a)
            return a
    return igualar_rms(m * a + (1.0 - m) * b, b)          # mezcla, re-igualada (volumen estable entre niveles)

def mezcla_auto(cliff_hz, confianza=None):
    """Cuanta IA aplicar segun que tan danada esta: mientras MENOS sobrevivio (corte bajo),
    mas suave (la IA inventa mas y se nota mas el vaiven)."""
    # Un corte aislado no demuestra dano: llamadas antiguas siguen conservadoras.
    if confianza is None or not np.isfinite(confianza) or confianza < .7 or cliff_hz <= 0 or cliff_hz >= 18500:
        return 0.0
    if cliff_hz <= 9000: return .35
    if cliff_hz <= 12500: return .45
    if cliff_hz <= 15500: return .55
    return .60
