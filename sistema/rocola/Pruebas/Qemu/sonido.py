#!/usr/bin/env python3
# sonido.py WAV [DESDE_S]: que se oyo en lo que grabo QEMU (la tarjeta HDA emulada, -audiodev wav). Por tramos de 1 s: el nivel (dBFS) y la
# frecuencia que domina; y al final un resumen por tramos seguidos con el mismo tono (las canciones de la prueba son tonos distintos:
# mundo.sh). QEMU solo escribe mientras la maquina tiene la salida abierta: los silencios con la salida cerrada no aparecen.
import struct, sys
import numpy as np
# (con QEMU aun encendido la cabecera dice 0 bytes: los tamanos se escriben al cerrar. Se lee la cabecera a mano y todo lo que hay detras)
b = open(sys.argv[1], "rb").read()
nc, fs = struct.unpack("<HI", b[22:28]); sw = struct.unpack("<H", b[34:36])[0] // 8
d = b.find(b"data") + 8
b = b[d:len(b) - (len(b) - d) % (nc * sw)]
datos = np.frombuffer(b, dtype=np.int16 if sw == 2 else np.int32).astype(np.float64)
datos = datos.reshape(-1, nc).mean(axis=1) / (32768.0 if sw == 2 else 2147483648.0)
desde = float(sys.argv[2]) if len(sys.argv) > 2 else 0.0
print("%s: %d Hz, %d canales, %.1f s grabados" % (sys.argv[1], fs, nc, len(datos) / fs))
tramos = []
for i in range(int(desde * fs), len(datos) - fs + 1, fs):
    x = datos[i:i + fs]
    rms = np.sqrt(np.mean(x * x)) + 1e-12
    db = 20 * np.log10(rms)
    if db < -60: f = 0
    else:
        e = np.abs(np.fft.rfft(x * np.hanning(len(x))))
        f = int(round(np.argmax(e[20:]) + 20))   # (1 Hz por casilla: tramo de 1 s)
        if e[20:].max() < 5 * np.median(e[20:]): f = -1   # ruido (sin tono que domine)
    tramos.append((i / fs, db, f))
res = []
for t, db, f in tramos:
    clave = "silencio" if f == 0 else ("ruido" if f == -1 else "%d Hz" % f)
    if res and res[-1][2] == clave: res[-1][1] = t + 1; res[-1][3].append(db)
    else: res.append([t, t + 1, clave, [db]])
for a, b, c, dbs in res:
    print("  %7.1f - %7.1f s  %-9s  %5.1f dBFS" % (a, b, c, max(dbs)))
