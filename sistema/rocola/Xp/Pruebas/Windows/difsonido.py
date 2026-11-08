#!/usr/bin/env python3
# difsonido.py A.raw B.raw : compara dos crudos de 16 bits estereo a 44,1 kHz muestra a muestra: cuantas difieren, donde, cuanto, y los tramos
import sys, numpy as np
a = np.fromfile(sys.argv[1], dtype='<i2'); b = np.fromfile(sys.argv[2], dtype='<i2')
print('muestras', len(a), len(b))
n = min(len(a), len(b))
d = a[:n].astype(np.int32) - b[:n].astype(np.int32)
idx = np.nonzero(d)[0]
print('distintas', len(idx), 'max', int(np.abs(d).max()) if len(idx) else 0)
if len(idx):
    cuadros = idx // 2
    tramos = []
    ini = prev = cuadros[0]
    for c in cuadros[1:]:
        if c > prev + 1: tramos.append((ini, prev)); ini = c
        prev = c
    tramos.append((ini, prev))
    print('tramos (cuadros, segundos):', len(tramos))
    for t in tramos[:20]: print('  %d-%d  %.4f-%.4f s  (%d cuadros)' % (t[0], t[1], t[0]/44100, t[1]/44100, t[1]-t[0]+1))
