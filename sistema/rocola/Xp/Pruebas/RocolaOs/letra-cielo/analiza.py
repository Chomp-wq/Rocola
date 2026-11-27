#!/usr/bin/env python3
# analiza.py: mide las capturas de la letra en el cielo (Pruebas/RocolaOs/letra-cielo/prueba.sh). Solo numeros; nunca letras de verdad.
#   analiza.py relleno PNG          -> «frac=0.512 x0=.. x1=.. xa=..»: lo llenado de ambar del renglon que se canta (de la banda de la letra)
#   analiza.py tinta PNG Y0 Y1      -> cuantos pixeles de letra (blanco, ambar o lavanda) hay entre las filas Y0 y Y1
#   analiza.py contraste PNG        -> el peor contraste (WCAG) del texto de la letra con lo que tiene detras (la sombra incluida)
#   analiza.py igual PNG_A PNG_B    -> cuantos pixeles de la banda de la letra cambian entre una captura y otra
import sys
from PIL import Image
import numpy as np

BANDA = (370, 515)   # filas del cielo donde va la letra en 1024x768 (el panel del reposo empieza en 523)

def carga(p):
    return np.asarray(Image.open(p).convert("RGB")).astype(np.float32)

def lum(a):
    c = a / 255.0
    c = np.where(c <= 0.03928, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return 0.2126 * c[..., 0] + 0.7152 * c[..., 1] + 0.0722 * c[..., 2]

def mascaras(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    ambar = (r > 225) & (g > 170) & (g < 235) & (b < 175) & (r - b > 70)
    blanco = (r > 238) & (g > 238) & (b > 238)
    lavanda = (r > 190) & (g > 180) & (b > 235) & (r - g < 40) & (r < 235)
    return ambar, blanco, lavanda

def renglon_de_arriba(a):
    # el renglon que se canta: de los grupos de filas con letra (blanca o de ambar; con huecos de hasta 4 filas) el que mas tinta tiene
    amb, bla, lav = mascaras(a)
    t = (amb | bla)[BANDA[0]:BANDA[1]]
    filas = t.sum(axis=1)
    grupos = []; i = 0
    while i < len(filas):
        if filas[i] > 3:
            j = i; k = i
            while k < len(filas) and k - j <= 4:
                if filas[k] > 3: j = k
                k += 1
            grupos.append((i, j, int(filas[i:j + 1].sum()))); i = j + 1
        else: i += 1
    if not grupos: return None
    g = max(grupos, key=lambda x: x[2])
    return BANDA[0] + g[0], BANDA[0] + g[1]

def relleno(p):
    a = carga(p); r = renglon_de_arriba(a)
    if r is None: print("nada"); return 1
    y0, y1 = r
    amb, bla, lav = mascaras(a)
    zona = (amb | bla)[y0:y1 + 1]
    cols = np.nonzero(zona.sum(axis=0) > 0)[0]
    x0, x1 = int(cols[0]), int(cols[-1])
    za = amb[y0:y1 + 1]
    ca = np.nonzero(za.sum(axis=0) > 0)[0]
    if len(ca) == 0: print("frac=0.000 x0=%d x1=%d xa=%d y0=%d y1=%d" % (x0, x1, x0, y0, y1)); return 0
    xa = int(ca[-1])
    print("frac=%.3f x0=%d x1=%d xa=%d y0=%d y1=%d" % ((xa - x0) / max(1, x1 - x0), x0, x1, xa, y0, y1))
    return 0

def tinta(p, y0, y1):
    a = carga(p); amb, bla, lav = mascaras(a)
    print(int((amb | bla | lav)[int(y0):int(y1)].sum()))

def contraste(p):
    a = carga(p); L = lum(a)
    amb, bla, lav = mascaras(a)
    txt = amb | bla | lav
    ys, xs = np.nonzero(txt[BANDA[0]:BANDA[1]])
    if len(ys) == 0: print("nada"); return 1
    y0, y1, x0, x1 = ys.min() + BANDA[0], ys.max() + BANDA[0], xs.min(), xs.max()
    reg = (slice(max(0, y0 - 6), y1 + 7), slice(max(0, x0 - 6), x1 + 7))
    # el fondo: lo que no es letra (ni su borde de 2 px) en esa caja; el peor caso: su percentil 90 (con la sombra puesta)
    cerca = txt[reg].copy()
    for _ in range(2):   # (dilatar 2 px)
        c2 = cerca.copy()
        c2[1:] |= cerca[:-1]; c2[:-1] |= cerca[1:]; c2[:, 1:] |= cerca[:, :-1]; c2[:, :-1] |= cerca[:, 1:]
        cerca = c2
    fondo = L[reg][~cerca]
    p90 = float(np.percentile(fondo, 90)); med = float(np.median(fondo))
    out = []
    for nombre, m in (("blanco", bla), ("ambar", amb), ("lavanda", lav)):
        mm = m[reg]
        if mm.sum() < 30: continue
        lt = float(np.percentile(L[reg][mm], 50))
        out.append("%s %.2f" % (nombre, (lt + 0.05) / (p90 + 0.05)))
    print("fondo p90=%.3f mediana=%.3f; contraste: %s" % (p90, med, ", ".join(out)))
    return 0

def claro(p, y0, y1):
    # los pixeles claros (luminancia > 0,22: el cielo de la rocola no pasa de 0,17) entre las filas Y0 y Y1: lo que se ve de la letra aunque este a medio apagar
    a = carga(p); print(int((lum(a)[int(y0):int(y1)] > 0.22).sum()))

def centroide(p):
    # la altura media (ponderada por lo claro que es, sobre 0,2) de la tinta de la letra en la banda; sube cuando los renglones suben
    a = carga(p); L = lum(a)[BANDA[0]:BANDA[1], 60:960]
    w = np.maximum(0, L - 0.2)
    if w.sum() < 1: print("0"); return
    ys = np.arange(BANDA[0], BANDA[1])[:, None]
    print("%.1f" % ((w * ys).sum() / w.sum()))

def ambar(p):
    a = carga(p); print(int(mascaras(a)[0][BANDA[0]:BANDA[1]].sum()))

def igual(pa, pb):
    a, b = carga(pa), carga(pb)
    d = np.abs(a - b).max(axis=2)[BANDA[0]:BANDA[1]]
    print(int((d > 6).sum()))

if __name__ == "__main__":
    c = sys.argv[1]
    sys.exit({"relleno": lambda: relleno(sys.argv[2]), "tinta": lambda: tinta(*sys.argv[2:5]), "contraste": lambda: contraste(sys.argv[2]), "claro": lambda: claro(*sys.argv[2:5]), "centroide": lambda: centroide(sys.argv[2]), "ambar": lambda: ambar(sys.argv[2]), "igual": lambda: igual(sys.argv[2], sys.argv[3])}[c]() or 0)
