#!/usr/bin/env python3
# frecuencias.py SONADO.raw [HZ_MUESTREO]: lo que de verdad sono (16 bits estereo, de la tarjeta de prueba con reloj), en tramos de 50 ms:
# la frecuencia dominante redondeada a 10 Hz (0 = silencio) y, juntando los tramos iguales, «desde hasta hz» por renglon. Asi se sabe que
# cancion se oyo (cada una de Spotify lleva su tono: Pruebas/RocolaOs/spotify-xp/SoloistFalso), cuanto, en que orden y si se corto.
import sys, numpy as np
a = np.fromfile(sys.argv[1], dtype='<i2').astype(np.float64) / 32768.0
fs = int(sys.argv[2]) if len(sys.argv) > 2 else 44100
x = a[0::2]
v = int(fs * 0.05)
seg = []
for i in range(0, len(x) - v + 1, v):
    w = x[i:i+v]
    if np.sqrt(np.mean(w*w)) < 0.003: f = 0
    else:
        s = np.abs(np.fft.rfft(w * np.hanning(v), n=v*4)); f = int(round(np.argmax(s) * fs / (v*4) / 10.0) * 10)
    t = i / fs
    if seg and abs(seg[-1][2] - f) <= 10: seg[-1][1] = t + 0.05
    else: seg.append([t, t + 0.05, f])
for d, h, f in seg: print("%.2f %.2f %d" % (d, h, f))
