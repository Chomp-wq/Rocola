#!/usr/bin/env python3
# analiza.py SONADO.raw FORMATO TASA: lo que de verdad sono en la tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c: crudo, dos
# canales). FORMATO: s32 (S32_LE), s16 (S16_LE) o s24 (S24_3LE). Dice:
#   CEROS  cuanto silencio exacto hay al principio (s)
#   BITS24 si las muestras de S32 llevan los 8 bits de abajo en 0 (24 bits de verdad)
#   PICO   el mayor valor absoluto (dBFS)
#   TRAMO  los tramos con un tono: su frecuencia (Hz), desde y hasta (s) y su nivel (dBFS RMS del tramo)
#   HUECOS cuantos huecos de silencio (mas de 5 ms en 0) hay entre el primer sonido y el ultimo
import sys
import numpy as np

ruta, fmt, tasa = sys.argv[1], sys.argv[2], int(sys.argv[3])
b = open(ruta, 'rb').read()
if fmt == 's32':
    d = np.frombuffer(b[: len(b) // 8 * 8], dtype='<i4').reshape(-1, 2).astype(np.int64)
    esc = 2.0 ** 31
elif fmt == 's16':
    d = np.frombuffer(b[: len(b) // 4 * 4], dtype='<i2').reshape(-1, 2).astype(np.int64)
    esc = 2.0 ** 15
else:
    a = np.frombuffer(b[: len(b) // 6 * 6], dtype=np.uint8).reshape(-1, 6).astype(np.int64)
    v = a[:, 0::3] | (a[:, 1::3] << 8) | (a[:, 2::3] << 16)
    v = np.where(v & 0x800000, v - (1 << 24), v)
    d = v
    esc = 2.0 ** 23
n = len(d)
print("CUADROS %d (%.2f s)" % (n, n / tasa))
if n == 0:
    sys.exit(0)
x = d[:, 0] / esc
nz = np.flatnonzero(np.abs(d[:, 0]) > (512 if fmt == 's32' else 2))
print("CEROS %.3f" % ((nz[0] if len(nz) else n) / tasa))
if fmt == 's32':
    print("BITS24 %s" % ("si" if np.all((d & 0xFF) == 0) else "no"))
print("PICO %.2f" % (20 * np.log10(max(1e-12, np.max(np.abs(d)) / esc))))
# tramos de 0,1 s: frecuencia dominante y nivel
v = int(tasa * 0.1)
ventanas = []
w = np.hanning(v)
for i in range(n // v):
    s = x[i * v:(i + 1) * v]
    r = np.sqrt(np.mean(s * s))
    db = 20 * np.log10(max(1e-12, r))
    if db < -60:
        ventanas.append((i, 0, db)); continue
    f = np.fft.rfft(s * w)
    k = int(np.argmax(np.abs(f[1:]))) + 1
    # parabola
    if 1 <= k < len(f) - 1:
        a1, a2, a3 = np.abs(f[k - 1]), np.abs(f[k]), np.abs(f[k + 1])
        p = 0.5 * (a1 - a3) / (a1 - 2 * a2 + a3) if (a1 - 2 * a2 + a3) != 0 else 0
    else:
        p = 0
    ventanas.append((i, (k + p) * tasa / v, db))
tramos = []
for i, f, db in ventanas:
    if f == 0:
        continue
    if tramos and abs(tramos[-1][0] - f) < 0.03 * f and tramos[-1][2] == i - 1:
        t = tramos[-1]; tramos[-1] = (t[0], t[1], i, t[3] + [db])
    else:
        tramos.append((f, i, i, [db]))
for f, a, z, dbs in tramos:
    if z - a < 2:
        continue
    print("TRAMO f=%.0f desde=%.1f hasta=%.1f db=%.1f" % (f, a * 0.1, (z + 1) * 0.1, float(np.median(dbs))))
# huecos de silencio exacto (mas de 5 ms) entre el primer y el ultimo sonido
if len(nz):
    sonando = np.zeros(n, dtype=bool); sonando[nz] = True
    huecos = 0; corrida = 0
    for i in range(nz[0], nz[-1]):
        if not sonando[i]:
            corrida += 1
        else:
            if corrida > tasa * 0.005:
                huecos += 1
            corrida = 0
    print("HUECOS %d" % huecos)
