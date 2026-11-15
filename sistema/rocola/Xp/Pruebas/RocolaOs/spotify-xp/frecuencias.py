#!/usr/bin/env python3
# frecuencias.py SONADO.raw [HZ_MUESTREO | REGISTRO]: lo que de verdad sono (16 bits estereo, de la tarjeta de prueba con reloj), en tramos de 50 ms:
# la frecuencia dominante redondeada a 10 Hz (0 = silencio) y, juntando los tramos iguales, «desde hasta hz» por renglon. Asi se sabe que
# cancion se oyo (cada una de Spotify lleva su tono: Pruebas/RocolaOs/spotify-xp/SoloistFalso), cuanto, en que orden y si se corto.
# Con el REGISTRO de la tarjeta (registro.txt) en vez de una frecuencia, cada apertura de la salida con la suya (revision «sonido-hilos-y-
# fallos»: si XP abre la salida a 48 kHz para una y a 44,1 kHz para otra, sonado.raw las trae seguidas, cada una a su ritmo).
import sys, numpy as np

def tramos(arg, n):
    # [(cuadro de inicio, cuadros, hz)] de sonado.raw
    if arg is None or arg.isdigit(): return [(0, n, int(arg) if arg else 44100)]
    ses = []   # [hz, total]
    for l in open(arg):
        c = dict(p.split('=', 1) for p in l.split()[2:] if '=' in p)
        if 'rate' not in c: continue
        r, t = int(c['rate']), int(c['total'])
        if not ses or (l.split()[1] == 'PREPARA' and t == 0 and ses[-1][1] > 0) or ses[-1][0] != r: ses.append([r, t])
        else: ses[-1][1] = max(ses[-1][1], t)
    out, pos = [], 0
    for i, (r, t) in enumerate(ses):
        if t <= 0 and i < len(ses) - 1: continue
        k = (n - pos) if i == len(ses) - 1 else min(t, n - pos)
        if k > 0: out.append((pos, k, r)); pos += k
    return out or [(0, n, 44100)]

def principal():
  a = np.fromfile(sys.argv[1], dtype='<i2').astype(np.float64) / 32768.0
  x = a[0::2]
  seg = []
  t0 = 0.0
  for ini, k, fs in tramos(sys.argv[2] if len(sys.argv) > 2 else None, len(x)):
      v = int(fs * 0.05)
      y = x[ini:ini + k]
      for i in range(0, len(y) - v + 1, v):
          w = y[i:i+v]
          if np.sqrt(np.mean(w*w)) < 0.003: f = 0
          else:
              s = np.abs(np.fft.rfft(w * np.hanning(v), n=v*4)); f = int(round(np.argmax(s) * fs / (v*4) / 10.0) * 10)
          t = t0 + i / fs
          if seg and abs(seg[-1][2] - f) <= 10: seg[-1][1] = t + 0.05
          else: seg.append([t, t + 0.05, f])
      t0 += k / fs
  for d, h, f in seg: print("%.2f %.2f %d" % (d, h, f))

if __name__ == "__main__": principal()
