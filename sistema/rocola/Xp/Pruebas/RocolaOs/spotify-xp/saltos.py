#!/usr/bin/env python3
# saltos.py SONADO.raw HZ_MUESTREO|REGISTRO DESDE HASTA: los SALTOS de la onda (cuadros perdidos o repetidos, chasquidos) en un tramo donde
# sono UN tono puro (las canciones del Soloist falso). Para un seno, x[n+1] + x[n-1] = 2 cos(w) x[n]; w sale de los datos (minimos
# cuadrados) y el resto r[n] es casi 0 (lo que se redondea a 16 bits y lo que se cuela de la interpolacion). Un salto da un resto de varias
# centesimas de la amplitud. Imprime «saltos N en S s (amplitud A)». Con el REGISTRO de la tarjeta, DESDE y HASTA en el tiempo de
# frecuencias.py (cada apertura de la salida a su ritmo). Revision «sonido-hilos-y-fallos» (la conversion de frecuencia de la fuente).
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
a = np.fromfile(sys.argv[1], dtype='<i2').astype(np.float64) / 32768.0
x0 = a[0::2]
d = float(sys.argv[3]); h = float(sys.argv[4])
from frecuencias import tramos
t0 = 0.0
x = None
for ini, k, fs in tramos(sys.argv[2], len(x0)):
    if t0 <= d < t0 + k / fs:
        x = x0[ini + int((d - t0) * fs):ini + min(k, int((h - t0) * fs))]; break
    t0 += k / fs
if x is None or len(x) < fs // 2: print("saltos -1 en 0 s (tramo corto)"); sys.exit(0)
c = np.dot(x[1:-1], x[2:] + x[:-2]) / (2 * np.dot(x[1:-1], x[1:-1]))
r = x[2:] + x[:-2] - 2 * c * x[1:-1]
amp = np.sqrt(2 * np.mean(x * x))
n = int(np.sum(np.abs(r) > max(0.02 * amp, 6 / 32768.0)))
print("saltos %d en %.2f s (amplitud %.4f, %d Hz)" % (n, len(x) / fs, amp, fs))
