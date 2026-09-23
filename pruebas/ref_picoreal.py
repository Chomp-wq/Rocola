"""Referencia INDEPENDIENTE del pico real (true-peak) de un WAV, solo con numpy.

No comparte nada con Rocola/PicoReal.cs: en vez de un filtro de interpolacion corto y una parabola, aqui se
reconstruye la onda con la interpolacion de banda limitada EXACTA de la secuencia entera (FFT con relleno de
ceros, 32 veces mas fina) y el maximo se afina con una parabola sobre esa rejilla, que a 32x se equivoca en
menos de una milesima de dB. Es la onda que dibujaria un convertidor ideal con esas muestras (y con silencio
antes y despues del archivo).

Uso:  python3 ref_picoreal.py archivo.wav            -> "PICO <lineal> <dBTP>" (el mayor de los canales)
      python3 ref_picoreal.py archivo.wav --canales  -> una linea por canal
"""
import sys

import numpy as np

from ref_bs1770 import lee_wav

FACTOR = 32
RELLENO = 4096


def pico_real(x):
    """Pico de la reconstruccion de banda limitada de una secuencia (un canal)."""
    x = np.asarray(x, dtype=np.float64)
    if len(x) == 0:
        return 0.0
    xp = np.concatenate([np.zeros(RELLENO), x, np.zeros(RELLENO)])
    n = len(xp)
    if n % 2:
        xp = np.concatenate([xp, [0.0]])
        n += 1
    X = np.fft.rfft(xp)
    Y = np.zeros(FACTOR * n // 2 + 1, dtype=complex)
    Y[: len(X)] = X
    Y[len(X) - 1] *= 0.5   # la componente de Nyquist se reparte entre sus dos imagenes
    y = np.fft.irfft(Y, FACTOR * n) * FACTOR
    a = np.abs(y)
    i = int(np.argmax(a))
    m = a[i]
    if 0 < i < len(a) - 1:
        y0, y1, y2 = a[i - 1], a[i], a[i + 1]
        den = y0 - 2 * y1 + y2
        if den < 0:
            d = 0.5 * (y0 - y2) / den
            m = max(m, y1 - 0.25 * (y0 - y2) * d)
    return float(m)


def main(argv):
    fs, x = lee_wav(argv[1])
    picos = [pico_real(x[:, c]) for c in range(x.shape[1])]
    if "--canales" in argv:
        for p in picos:
            print("PICO %.9f %.6f" % (p, 20 * np.log10(p) if p > 0 else -999.0))
    else:
        p = max(picos)
        print("PICO %.9f %.6f" % (p, 20 * np.log10(p) if p > 0 else -999.0))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
