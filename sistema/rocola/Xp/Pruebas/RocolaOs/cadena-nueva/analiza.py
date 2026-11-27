#!/usr/bin/env python3
# analiza.py: las cuentas de lo que DE VERDAD sono por la tarjeta de prueba con reloj (sonado.raw: enteros de 16 bits, estereo, 44,1 kHz).
#   analiza.py inicio RAW                       el primer cuadro con sonido (|x| > 0,001) y lo que dura todo, en segundos
#   analiza.py constante RAW REF.f32 DESDE HASTA [DONDE] la ganancia de lo que sono frente a la cancion de referencia (decodificada: coma
#                                               flotante estereo cruda), desde DONDE segundos de lo que sono (si no, su primer sonido), por
#                                               bloque de 10 ms, entre DESDE y HASTA segundos de la cancion: el desfase se busca solo
#                                               (correlacion en el primer segundo); dice min, max y el vaiven (max - min) en dB, y la
#                                               diferencia entre las medianas de cada escalon de 2 s (escalones_db)
import sys, wave
import numpy as np

def raw(p):
    a = np.fromfile(p, dtype='<i2').astype(np.float64) / 32768.0
    return a[: len(a) // 2 * 2].reshape(-1, 2)

def lee_wav(p):
    with wave.open(p, 'rb') as w:
        n = w.getnframes(); ch = w.getnchannels(); sw = w.getsampwidth()
        b = w.readframes(n)
    if sw == 2: a = np.frombuffer(b, dtype='<i2').astype(np.float64) / 32768.0
    elif sw == 4: a = np.frombuffer(b, dtype='<f4').astype(np.float64)
    else: raise SystemExit("formato raro")
    a = a.reshape(-1, ch)
    if ch == 1: a = np.repeat(a, 2, axis=1)
    return a

def inicio(x, umbral=1e-3):
    m = np.max(np.abs(x), axis=1)
    i = np.argmax(m > umbral)
    return int(i) if m[i] > umbral else -1

orden = sys.argv[1]
if orden == "inicio":
    x = raw(sys.argv[2]); i = inicio(x)
    print("INICIO cuadro=%d segundo=%.4f total=%.3f" % (i, i / 44100.0, len(x) / 44100.0))
elif orden == "constante":
    x = raw(sys.argv[2]); r = np.fromfile(sys.argv[3], dtype='<f4').astype(np.float64); r = r[: len(r) // 2 * 2].reshape(-1, 2)
    desde = float(sys.argv[4]); hasta = float(sys.argv[5])
    if len(sys.argv) > 6: x = x[int(float(sys.argv[6]) * 44100):]
    i0 = inicio(x); j0 = inicio(r, 1e-4)
    if i0 < 0: print("CONSTANTE sin_sonido=1"); sys.exit(0)
    # el desfase fino: la correlacion del canal izquierdo en el primer segundo, +-2000 cuadros
    seg = 44100; a = x[i0 + 4410: i0 + 4410 + seg, 0]
    mejor, mk = -1e300, 0
    for k in range(-2000, 2001):
        s = j0 + 4410 + k
        if s < 0 or s + seg > len(r): continue
        c = float(np.dot(a, r[s: s + seg, 0]))
        if c > mejor: mejor, mk = c, k
    off = i0 - (j0 + mk)
    b = 441; g = []; tg = []
    for t in np.arange(desde, hasta, 0.01):
        q = int(t * 44100) + j0 + mk
        if q < 0 or q + b > len(r) or q + off + b > len(x) or q + off < 0: continue
        er = float(np.sum(r[q: q + b] ** 2)); ex = float(np.sum(x[q + off: q + off + b] ** 2))
        if er < 1e-9: continue
        g.append(10 * np.log10(ex / er)); tg.append(t)
    g = np.array(g); tg = np.array(tg)
    # por escalon (de 2 s: 0, +10 y -10 dB): la mediana de sus bloques, sin los 100 ms de cada borde (lo que leen por delante y por detras los filtros de fase lineal); si la cadena no depende del nivel, todas
    # iguales (aunque cada bloque varie por lo que la cadena le hace al tono: la del billar no es plana)
    med = []
    for k in range(int(tg.max() // 2) + 1):
        sel = g[(tg >= 2 * k + 0.1) & (tg < 2 * k + 1.9)]
        if len(sel) > 50: med.append(np.median(sel))
    med = np.array(med)
    print("CONSTANTE bloques=%d min_db=%.3f max_db=%.3f vaiven_db=%.3f mediana_db=%.3f desfase=%d escalones=%d escalones_db=%.3f" % (len(g), g.min(), g.max(), g.max() - g.min(), np.median(g), off, len(med), med.max() - med.min()))
