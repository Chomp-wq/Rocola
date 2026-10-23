"""Referencia INDEPENDIENTE del rango de sonoridad (LRA) de EBU Tech 3342 (solo numpy, y la lectura y la ponderacion K de ref_bs1770.py).

No comparte codigo ni forma de calcular con Rocola/Sonoridad.cs (MedidorBs1770.Lra), y a proposito:
  - la ponderacion K sale de la tabla de la norma (ref_bs1770.py), no de los parametros analogicos de la rocola;
  - las ventanas de 3 s se sacan de una suma acumulada, UNA CADA 100 ms (10 por segundo, solape de 2,9 s; los limites floor(k*fs/10), como en ref_bs1770.py);
  - nada de histograma: las sonoridades se ordenan y los percentiles se interpolan (numpy.percentile, lineal);
  - las puertas se comparan en LUFS, con logaritmos.
La rocola usa un histograma de 0,1 LU, asi que las dos cifras coinciden dentro de ~0,2 LU.

Uso:  python3 ref_lra.py archivo.wav --pesos 1,1          -> imprime "LRA <valor>" (o "LRA none" si no hay ventana de 3 s)
"""
import sys

import numpy as np

import ref_bs1770 as bs

PUERTA_RELATIVA_LRA = -20.0


def lra(fs, x, pesos):
    n, canales = x.shape
    limites = np.floor(np.arange(0, n * 10 // fs + 2) * fs / 10.0).astype(np.int64)
    limites = limites[limites <= n]
    if len(limites) < 31:
        return None                                      # menos de 3 s
    ini, fin = limites[:-30], limites[30:]
    potencia = None
    for c in range(canales):
        if pesos[c] == 0:
            continue
        y = bs.pondera_k(x[:, c], fs)
        acum = np.concatenate(([0.0], np.cumsum(y * y)))
        z = (acum[fin] - acum[ini]) / (fin - ini)
        potencia = pesos[c] * z if potencia is None else potencia + pesos[c] * z
    with np.errstate(divide="ignore"):
        l = -0.691 + 10.0 * np.log10(potencia)
    pasa = l > bs.PUERTA_ABSOLUTA
    if not pasa.any():
        return None
    gamma = -0.691 + 10.0 * np.log10(potencia[pasa].mean()) + PUERTA_RELATIVA_LRA
    vale = pasa & (l > gamma)
    if not vale.any():
        return None
    v = np.sort(l[vale])
    return float(np.percentile(v, 95) - np.percentile(v, 10))


def main(argv):
    ruta = argv[1]
    pesos = [float(v) for v in argv[argv.index("--pesos") + 1].split(",")]
    fs, x = bs.lee_wav(ruta)
    r = lra(fs, x, pesos)
    print("LRA none" if r is None else "LRA %.6f" % r)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
