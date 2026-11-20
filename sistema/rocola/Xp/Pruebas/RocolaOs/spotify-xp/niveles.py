#!/usr/bin/env python3
# niveles.py SONADO.raw REGISTRO|HZ_MUESTREO HZ [CAMBIO...]: EL NIVEL DE LO QUE DE VERDAD SONO (la tarjeta de prueba con reloj, 16 bits
# estereo; como frecuencias.py) mientras se oye el tono HZ, para la escena «noche» de la junta de Spotify (el modo noche de XP tambien baja
# lo de Spotify, con su rampa). En tramos de 100 ms: el nivel RMS del canal izquierdo en dB (solo los tramos donde el tono dominante es HZ,
# a +-15 Hz). Dice, en una linea de «clave=valor»:
#   antes      la mediana del nivel en el primer tramo quieto (los 2 s antes del primer cambio que se vea), dB
#   despues    la mediana del nivel quieto despues de bajar (2 s seguidos sin cambiar mas de 0,05 dB), dB
#   baja       despues - antes (lo que bajo), dB
#   rampa      lo que tardo en bajar: del primer tramo 0,1 dB por debajo de «antes» al primero a 0,1 dB de «despues», s
#   abajo      cuanto se quedo abajo: de la mitad de la bajada a la mitad de la subida (si volvio a subir; si no, -1), s
#   subida     lo que tardo en volver a subir (si volvio; si no, -1), s
#   vuelve     el nivel quieto al final, si volvio a subir (si no, -99), dB
#   desde      cuando empezo a bajar (segundos de lo sonado)
#   mediana    la mediana de todo (sin el primer medio segundo), dB (para una que suena quieta: la rocola prendida ya de noche)
# Con --cruce EPOCA (la hora, en segundos de UNIX, en que la rocola cruza las 22 h; 17-11-2026: la noche baja en UNA HORA, pareja en dB),
# ademas: cruce (en segundos de lo sonado, desde el primer ARRANCA del REGISTRO), pend_antes y pend_despues (la pendiente de la recta que
# mejor pasa por el nivel antes y despues del cruce, sin el segundo de alrededor, dB/min) y salto (el mayor cambio entre dos tramos
# seguidos despues del cruce, dB).
# Con --serie, ademas, la serie entera (t dB) en las lineas siguientes.
import sys, numpy as np
sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
from frecuencias import tramos

def niveles(raw, reg, hz):
    a = np.fromfile(raw, dtype='<i2').astype(np.float64) / 32768.0
    x = a[0::2]
    serie, t0 = [], 0.0
    for ini, k, fs in tramos(reg, len(x)):
        v = int(fs * 0.1)
        y = x[ini:ini + k]
        for i in range(0, len(y) - v + 1, v):
            w = y[i:i + v]
            r = np.sqrt(np.mean(w * w))
            if r < 0.001: continue
            s = np.abs(np.fft.rfft(w * np.hanning(v), n=v * 4)); f = np.argmax(s) * fs / (v * 4)
            if abs(f - hz) > 15: continue
            serie.append((t0 + i / fs, 20 * np.log10(r)))
        t0 += k / fs
    return serie

def principal():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    raw, reg, hz = args[0], args[1], float(args[2])
    s = niveles(raw, reg, hz)
    out = dict(antes=-99, despues=-99, baja=0, rampa=-1, abajo=-1, subida=-1, vuelve=-99, desde=-1, tramos=len(s))
    if len(s) < 40:
        print(' '.join('%s=%s' % kv for kv in out.items())); return
    t = np.array([p[0] for p in s]); d = np.array([p[1] for p in s])
    # (se saltan los primeros 0,5 s: la entrada de la cancion)
    i0 = int(np.searchsorted(t, t[0] + 0.5))
    out['mediana'] = round(float(np.median(d[i0:])), 3)
    if '--cruce' in sys.argv:
        epoca = float(sys.argv[sys.argv.index('--cruce') + 1])
        arranca = next(float(l.split()[0]) for l in open(reg) if ' ARRANCA ' in l)
        c = epoca - arranca
        an = (t >= t[i0]) & (t < c - 1.0); de = t > c + 1.0
        pend = lambda m: round(float(np.polyfit(t[m], d[m], 1)[0] * 60.0), 4) if m.sum() >= 30 else -99
        out.update(cruce=round(c, 2), pend_antes=pend(an), pend_despues=pend(de),
                   salto=round(float(np.max(np.abs(np.diff(d[de])))), 4) if de.sum() >= 2 else -99)
        print(' '.join('%s=%s' % kv for kv in out.items()))
        if '--serie' in sys.argv:
            for a_, b_ in s: print('%.2f %.3f' % (a_, b_))
        return
    base = np.median(d[i0:i0 + 20])
    j = i0
    while j < len(d) and d[j] > base - 0.1: j += 1
    if j >= len(d):
        out.update(antes=round(base, 2)); print(' '.join('%s=%s' % kv for kv in out.items())); return
    antes = np.median(d[max(i0, j - 20):j]); desde = t[j]
    # despues: el primer tramo de 2 s seguidos (20) sin moverse mas de 0,05 dB
    k = j
    while k + 20 <= len(d) and (d[k:k + 20].max() - d[k:k + 20].min()) > 0.05: k += 1
    despues = np.median(d[k:k + 20]) if k + 20 <= len(d) else d[-1]
    m = j
    while m < len(d) and d[m] > despues + 0.1: m += 1
    rampa = (t[m] - desde) if m < len(d) else -1
    out.update(antes=round(antes, 2), despues=round(despues, 2), baja=round(despues - antes, 2), rampa=round(rampa, 2), desde=round(desde, 2))
    # ¿vuelve a subir? (la prueba: 15 s abajo y vuelve)
    n = k + 20
    while n < len(d) and d[n] < despues + 0.1: n += 1
    if n < len(d):
        p = n
        while p + 10 <= len(d) and (d[p:p + 10].max() - d[p:p + 10].min()) > 0.05: p += 1
        vuelve = np.median(d[p:p + 10]) if p + 10 <= len(d) else d[-1]
        q = n
        while q < len(d) and d[q] < vuelve - 0.1: q += 1
        mitad = (antes + despues) / 2
        a1 = next((t[i] for i in range(j, len(d)) if d[i] <= mitad), -1)
        a2 = next((t[i] for i in range(n, len(d)) if d[i] >= mitad), -1)
        out.update(vuelve=round(vuelve, 2), subida=round((t[q] - t[n]) if q < len(d) else -1, 2), abajo=round(a2 - a1, 2) if a1 >= 0 and a2 >= 0 else -1)
    print(' '.join('%s=%s' % kv for kv in out.items()))
    if '--serie' in sys.argv:
        for a, b in s: print('%.2f %.2f' % (a, b))

if __name__ == '__main__': principal()
