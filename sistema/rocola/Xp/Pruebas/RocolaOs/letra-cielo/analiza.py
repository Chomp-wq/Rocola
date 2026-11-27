#!/usr/bin/env python3
# analiza.py: mide las capturas de la letra en el cielo (Pruebas/RocolaOs/letra-cielo/prueba.sh). Solo numeros; nunca letras de verdad.
# La geometria de la columna (izquierda, derecha, arriba, abajo, sitio del verso que se canta) la dice la primera linea del log de la rocola
# (ROCOLA_LETRA_LOG: «geo L=.. R=.. T=.. B=.. A=..»); se pasa con la variable GEO (la ruta del log).
#   analiza.py geo                     -> «L R T B A lh nom»
#   analiza.py grupos PNG              -> un grupo de renglones de tinta clara por linea: «y0 y1 n xmin xmax lum» (lum: la luminancia mediana de la tinta)
#   analiza.py blanco PNG              -> cuantos pixeles de blanco opaco hay en la columna (el verso que se canta; la translucida no llega)
#   analiza.py contraste PNG           -> el contraste (WCAG) del blanco opaco con lo que tiene detras (la sombra incluida), en los renglones de ese verso
#   analiza.py lineas PNG              -> cuantos renglones tiene el verso opaco y sus filas (el verso largo se parte)
#   analiza.py tinta PNG              -> cuantos pixeles de tinta clara hay en la columna (a la derecha de la estrella en su extremo)
#   analiza.py puntos PNG              -> cuantas manchas de tinta clara de tamano de punto hay en el renglon del verso que se canta, y donde empiezan
#   analiza.py bajo PNG                -> tinta clara en la franja entre el final de la columna y el panel (debe ser casi nada: el desvanecido)
#   analiza.py centro PNG              -> la altura media (ponderada por lo claro) de la tinta de la columna
#   analiza.py resorte LOG             -> «eventos=N mono=0|1 sobre=PX t95=S»: los cambios de verso del log (la cosa sube de una en una): el verso nuevo baja de su sitio
#                                         al del que se canta sin retroceder (mono), sin pasarse (sobre: lo que pasa del sitio, 0 = sin rebote) y t95: segundos hasta llegar al 95 %
#   analiza.py suave LOG               -> «max=PX n=N»: lo mas que se mueve el verso de referencia entre dos lineas seguidas del log dentro de la misma cosa (la letra sin tiempos)
#   analiza.py igual PNG_A PNG_B       -> cuantos pixeles de la columna cambian entre una captura y otra
import os, sys
from PIL import Image
import numpy as np

def geo():
    for l in open(os.environ["GEO"]):
        if l.startswith("geo "):
            d = dict(kv.split("=") for kv in l.split()[1:])
            return {k: float(v) for k, v in d.items()}
    raise SystemExit("sin linea geo en el log")

def carga(p):
    return np.asarray(Image.open(p).convert("RGB")).astype(np.float32)

def lum(a):
    c = a / 255.0
    c = np.where(c <= 0.03928, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return 0.2126 * c[..., 0] + 0.7152 * c[..., 1] + 0.0722 * c[..., 2]

def caja(a):
    g = geo()
    return int(g["L"]) - 14, int(g["R"]), int(g["T"]) - 12, int(g["B"]) + 12

def claro(a):
    # tinta clara: lo que pasa de luminancia 0,22 (el cielo de la rocola no pasa de 0,17)
    return lum(a) > 0.22

def grupos(p):
    a = carga(p); x0, x1, y0, y1 = caja(a)
    m = claro(a)[y0:y1, x0:x1]; L = lum(a)[y0:y1, x0:x1]
    filas = m.sum(axis=1)
    i = 0; out = []
    while i < len(filas):
        if filas[i] > 4:
            j = i; k = i
            while k < len(filas) and k - j <= 6:
                if filas[k] > 4: j = k
                k += 1
            sub = m[i:j + 1]; n = int(sub.sum())
            cols = np.nonzero(sub.sum(axis=0) > 0)[0]
            out.append((y0 + i, y0 + j, n, x0 + int(cols[0]), x0 + int(cols[-1]), float(np.median(L[i:j + 1][sub]))))
            i = j + 1
        else:
            i += 1
    return out

def lineas(p):
    # los renglones del verso opaco: grupos de filas con blanco opaco (min canal >= 242)
    a = carga(p); x0, x1, y0, y1 = caja(a)
    m = (a.min(axis=2) >= 242)[y0:y1, x0:x1]
    filas = m.sum(axis=1); out = []; i = 0
    while i < len(filas):
        if filas[i] > 6:
            j = i; k = i
            while k < len(filas) and k - j <= 6:
                if filas[k] > 6: j = k
                k += 1
            if int(filas[i:j + 1].sum()) > 300: out.append((y0 + i, y0 + j))
            i = j + 1
        else: i += 1
    return out

def blanco(p):
    a = carga(p); x0, x1, y0, y1 = caja(a)
    return int((a[y0:y1, x0:x1].min(axis=2) >= 242).sum())

def contraste(p):
    a = carga(p); L = lum(a); x0, x1, y0, y1 = caja(a)
    t = (a.min(axis=2) >= 242)
    sub = t[y0:y1, x0:x1]
    ys, xs = np.nonzero(sub)
    if len(ys) < 200: print("nada"); return 1
    # los renglones del verso opaco: el grupo de filas con mas blanco opaco
    filas = sub.sum(axis=1); mejor = None; i = 0
    while i < len(filas):
        if filas[i] > 3:
            j = i; k = i
            while k < len(filas) and k - j <= 6:
                if filas[k] > 3: j = k
                k += 1
            n = int(filas[i:j + 1].sum())
            if mejor is None or n > mejor[2]: mejor = (i, j, n)
            i = j + 1
        else: i += 1
    ya, yb = y0 + mejor[0], y0 + mejor[1]
    cols = np.nonzero(t[ya:yb + 1, x0:x1].sum(axis=0) > 0)[0]
    xa, xb = x0 + int(cols[0]), x0 + int(cols[-1])
    reg = (slice(max(0, ya - 4), yb + 5), slice(max(0, xa - 6), xb + 7))
    cerca = t[reg].copy()
    for _ in range(2):
        c2 = cerca.copy()
        c2[1:] |= cerca[:-1]; c2[:-1] |= cerca[1:]; c2[:, 1:] |= cerca[:, :-1]; c2[:, :-1] |= cerca[:, 1:]
        cerca = c2
    fondo = L[reg][~cerca]
    p90 = float(np.percentile(fondo, 90)); med = float(np.median(fondo))
    lt = float(np.percentile(L[reg][t[reg]], 50))
    print("fondo p90=%.3f mediana=%.3f; contraste blanco %.2f" % (p90, med, (lt + 0.05) / (p90 + 0.05)))
    return 0

def tinta(p):
    a = carga(p); g = geo(); x0, x1, y0, y1 = caja(a)
    print(int(claro(a)[y0:y1, x0:x1].sum()))

def puntos(p):
    # las manchas de tinta clara (cuatro vecinos) en el renglon del sitio del verso que se canta, a la izquierda de la columna: tres puntos del mismo tamano
    a = carga(p); g = geo()
    ya, yb = int(g["A"]) - 4, int(g["A"] + g["lh"] * 1.1)
    xa, xb = int(g["L"]) - 14, int(g["L"]) + 85
    m = (lum(a) > 0.14)[ya:yb, xa:xb]
    lab = np.zeros(m.shape, dtype=np.int32); n = 0; tam = []; xs0 = []
    for y in range(m.shape[0]):
        for x in range(m.shape[1]):
            if m[y, x] and lab[y, x] == 0:
                n += 1; pila = [(y, x)]; lab[y, x] = n; c = 0; mnx = x
                while pila:
                    cy, cx = pila.pop(); c += 1; mnx = min(mnx, cx)
                    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        ny, nx = cy + dy, cx + dx
                        if 0 <= ny < m.shape[0] and 0 <= nx < m.shape[1] and m[ny, nx] and lab[ny, nx] == 0:
                            lab[ny, nx] = n; pila.append((ny, nx))
                if c >= 12: tam.append(c); xs0.append(xa + mnx)
    g2 = sorted((x, t) for x, t in zip(xs0, tam) if t >= 100)
    tres = 0
    # (tres manchas seguidas, con 24 a 48 px entre una y otra y de tamanos parecidos; con la estrella cerca puede haber una mancha de mas o la primera pegada a su brillo)
    for i in range(len(g2)):
        for j in range(i + 1, len(g2)):
            for k in range(j + 1, len(g2)):
                sp = [g2[j][0] - g2[i][0], g2[k][0] - g2[j][0]]; tz = [g2[i][1], g2[j][1], g2[k][1]]
                if all(24 <= d <= 48 for d in sp) and max(tz) <= 3 * min(tz): tres = 1
    print("manchas=%d tam=%s x=%s tres=%d" % (len(tam), ",".join(map(str, tam)), ",".join(map(str, xs0)), tres))

def bajo(p):
    a = carga(p); g = geo()
    y0, y1 = int(g["B"]) + 4, int(g["B"]) + 22
    x0, x1 = int(g["L"]), int(g["R"]) - 160   # (el «Letra: LRCLIB» va a la derecha, abajo)
    print(int(claro(a)[y0:y1, x0:x1].sum()))

def centro(p):
    a = carga(p); x0, x1, y0, y1 = caja(a)
    w = np.maximum(0, lum(a)[y0:y1, x0:x1] - 0.2)
    if w.sum() < 1: print("0"); return
    ys = np.arange(y0, y1)[:, None]
    print("%.1f" % ((w * ys).sum() / w.sum()))

def log_filas(ruta):
    r = []
    for l in open(ruta):
        if l.startswith("geo "): continue
        d = dict(kv.split("=") for kv in l.split()[1:] if "=" in kv)
        try: r.append((int(l.split()[0]), float(d["pos"]), int(d["cosa"]), float(d["y"]), float(d["mov"])))
        except (KeyError, ValueError): pass
    return r

def resorte(ruta):
    g = geo(); A = g["A"]; f = log_filas(ruta)
    ev = 0; mono = 1; sobre = 0.0; t95s = []
    i = 1
    while i < len(f):
        if f[i][2] == f[i - 1][2] + 1 and f[i][3] == f[i][3]:
            c = f[i][2]; t0 = f[i][0]; j = i; y0 = f[i][3]
            if y0 - A < 10: i += 1; continue
            ev += 1; t95 = None
            while j < len(f) and f[j][2] == c and f[j][0] - t0 < 8000:
                if j > i and f[j][3] > f[j - 1][3] + 0.3: mono = 0
                sobre = max(sobre, A - f[j][3])
                if t95 is None and (f[j][3] - A) <= 0.05 * (y0 - A): t95 = (f[j][0] - t0) / 1000.0
                j += 1
            if t95 is not None: t95s.append(t95)
            i = j
        else:
            i += 1
    print("eventos=%d mono=%d sobre=%.2f t95=%s" % (ev, mono, max(sobre, 0), "%.2f" % (sum(t95s) / len(t95s)) if t95s else "-"))

def suave(ruta):
    f = log_filas(ruta); m = 0.0; n = 0
    for a, b in zip(f, f[1:]):
        if a[2] == b[2] and b[1] - a[1] > 0.04:
            m = max(m, abs(b[3] - a[3]) / (b[1] - a[1])); n += 1
    print("max=%.2f n=%d" % (m, n))   # px por segundo de cancion

def igual(pa, pb):
    a, b = carga(pa), carga(pb); x0, x1, y0, y1 = caja(a)
    d = np.abs(a - b).max(axis=2)[y0:y1, x0:x1]
    print(int((d > 6).sum()))

if __name__ == "__main__":
    c = sys.argv[1]
    if c == "geo":
        g = geo(); print("%d %d %d %d %d %.1f %.1f" % (g["L"], g["R"], g["T"], g["B"], g["A"], g["lh"], g["nom"])); sys.exit(0)
    if c == "cesa":
        # cuantos cuadros anoto la letra en su log (solo anota con la letra a la vista) en los 4 s antes de que se escribiera la captura PNG
        m = os.stat(sys.argv[2]).st_mtime * 1000; n = 0
        for l in open(os.environ["GEO"]):
            if l.split() and l.split()[0].isdigit() and m - 4000 <= int(l.split()[0]) <= m: n += 1
        print("%d" % n); sys.exit(0)
    if c == "lineas":
        l = lineas(sys.argv[2]); print("%d %s" % (len(l), " ".join("%d-%d" % x for x in l))); sys.exit(0)
    if c == "grupos":
        for g in grupos(sys.argv[2]): print("%d %d %d %d %d %.3f" % g)
        sys.exit(0)
    r = {"blanco": lambda: print(blanco(sys.argv[2])), "contraste": lambda: contraste(sys.argv[2]), "tinta": lambda: tinta(sys.argv[2]),
         "puntos": lambda: puntos(sys.argv[2]), "bajo": lambda: bajo(sys.argv[2]), "resorte": lambda: resorte(sys.argv[2]), "suave": lambda: suave(sys.argv[2]), "centro": lambda: centro(sys.argv[2]), "igual": lambda: igual(sys.argv[2], sys.argv[3])}[c]()
    sys.exit(r or 0)
