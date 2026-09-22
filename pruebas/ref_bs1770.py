"""Referencia INDEPENDIENTE de la sonoridad integrada ITU-R BS.1770 (solo numpy).

No comparte codigo ni forma de calcular con Rocola/Sonoridad.cs, y a proposito:

  - La ponderacion K sale de la TABLA de coeficientes que publica la norma a 48 kHz, no de los
    parametros analogicos que usa la rocola. Se aplica en el dominio de la frecuencia (FFT con relleno de
    ceros, para que la cola del filtro no se enrosque): a 48 kHz eso es el mismo filtro de la norma; a
    otras frecuencias es su respuesta llevada por frecuencia fisica, que es otra aproximacion del filtro
    analogico, distinta de la bilineal con predistorsion de la rocola.
  - Los bloques se sacan de una suma acumulada (la rocola usa subbloques de 100 ms y un anillo).
  - Las puertas se comparan en LKFS, con logaritmos (la rocola compara potencias).

Uso:  python3 ref_bs1770.py archivo.wav --pesos 1,1          -> imprime "LKFS <valor>" o "LKFS -inf"
      python3 ref_bs1770.py --tabla                            -> imprime la tabla de 48 kHz
Lee WAV PCM de 16/24/32 bits y float de 32/64 bits (tambien WAVE_FORMAT_EXTENSIBLE).
"""
import struct
import sys

import numpy as np

# ITU-R BS.1770-4/-5, tablas 1 y 2: coeficientes a 48 kHz (b0, b1, b2, a1, a2; a0 = 1).
ESTANTE_48K = (1.53512485958697, -2.69169618940638, 1.19839281085285, -1.69065929318241, 0.73248077421585)
RLB_48K = (1.0, -2.0, 1.0, -1.99004745483398, 0.99007225036621)
PUERTA_ABSOLUTA = -70.0
PUERTA_RELATIVA = -10.0


def lee_wav(ruta):
    with open(ruta, "rb") as f:
        datos = f.read()
    if datos[:4] not in (b"RIFF", b"RF64") or datos[8:12] != b"WAVE":
        raise ValueError("no es WAVE")
    pos, fmt, cuerpo = 12, None, None
    while pos + 8 <= len(datos):
        ident, largo = datos[pos:pos + 4], struct.unpack("<I", datos[pos + 4:pos + 8])[0]
        trozo = datos[pos + 8:pos + 8 + largo]
        if ident == b"fmt ":
            fmt = trozo
        elif ident == b"data":
            cuerpo = trozo
        pos += 8 + largo + (largo & 1)
    formato, canales, fs = struct.unpack("<HHI", fmt[:8])
    bits = struct.unpack("<H", fmt[14:16])[0]
    if formato == 0xFFFE:
        formato = struct.unpack("<H", fmt[24:26])[0]   # los dos primeros bytes del GUID del subformato
    if formato == 3:
        x = np.frombuffer(cuerpo, dtype="<f4" if bits == 32 else "<f8").astype(np.float64)
    elif formato == 1 and bits == 16:
        x = np.frombuffer(cuerpo, dtype="<i2").astype(np.float64) / 32768.0
    elif formato == 1 and bits == 24:
        b = np.frombuffer(cuerpo, dtype=np.uint8).reshape(-1, 3).astype(np.int32)
        v = b[:, 0] | (b[:, 1] << 8) | (b[:, 2] << 16)
        v = np.where(v >= 1 << 23, v - (1 << 24), v)
        x = v.astype(np.float64) / 8388608.0
    elif formato == 1 and bits == 32:
        x = np.frombuffer(cuerpo, dtype="<i4").astype(np.float64) / 2147483648.0
    else:
        raise ValueError("formato no soportado")
    x = x[: len(x) // canales * canales]
    return fs, x.reshape(-1, canales)


def respuesta_tabla(frecuencias):
    """Respuesta compleja del filtro K de la TABLA de 48 kHz a esas frecuencias (Hz)."""
    z1 = np.exp(-2j * np.pi * np.minimum(frecuencias, 23999.0) / 48000.0)
    def bq(c):
        b0, b1, b2, a1, a2 = c
        return (b0 + b1 * z1 + b2 * z1 * z1) / (1.0 + a1 * z1 + a2 * z1 * z1)
    return bq(ESTANTE_48K) * bq(RLB_48K)


def pondera_k(x, fs):
    n = len(x)
    relleno = int(2 * fs)                      # 2 s de ceros: la cola del paso-alto cae muy por debajo de 1e-20
    m = 1 << int(np.ceil(np.log2(n + relleno)))
    h = respuesta_tabla(np.fft.rfftfreq(m, 1.0 / fs))
    return np.fft.irfft(np.fft.rfft(x, m) * h, m)[:n]


def integrada(fs, x, pesos):
    n, canales = x.shape
    assert len(pesos) == canales
    potencia = np.zeros(0)
    limites = np.floor(np.arange(0, n * 10 // fs + 2) * fs / 10.0).astype(np.int64)
    limites = limites[limites <= n]
    if len(limites) < 5:
        return None                                  # ni un bloque de 400 ms
    ini, fin = limites[:-4], limites[4:]
    for c in range(canales):
        if pesos[c] == 0:
            continue
        y = pondera_k(x[:, c], fs)
        acum = np.concatenate(([0.0], np.cumsum(y * y)))
        z = (acum[fin] - acum[ini]) / (fin - ini)
        potencia = pesos[c] * z if potencia.size == 0 else potencia + pesos[c] * z
    with np.errstate(divide="ignore"):
        l = -0.691 + 10.0 * np.log10(potencia)
    j_abs = l > PUERTA_ABSOLUTA
    if not j_abs.any():
        return float("-inf")
    gamma_r = -0.691 + 10.0 * np.log10(potencia[j_abs].mean()) + PUERTA_RELATIVA
    j = j_abs & (l > gamma_r)
    return float(-0.691 + 10.0 * np.log10(potencia[j].mean()))


def main(argv):
    if "--tabla" in argv:
        print("estante", " ".join(repr(v) for v in ESTANTE_48K))
        print("rlb", " ".join(repr(v) for v in RLB_48K))
        return 0
    ruta = argv[1]
    pesos = [float(v) for v in argv[argv.index("--pesos") + 1].split(",")]
    fs, x = lee_wav(ruta)
    r = integrada(fs, x, pesos)
    print("LKFS none" if r is None else ("LKFS -inf" if r == float("-inf") else "LKFS %.6f" % r))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
