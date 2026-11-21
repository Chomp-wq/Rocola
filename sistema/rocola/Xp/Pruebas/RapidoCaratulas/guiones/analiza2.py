# analiza2.py MIDE.tsv... : por corrida y en total, lo que tardo cada pagina (sin la primera, la del arranque) en tener sus portadas en memoria
# (decodas) y subidas (lista). Una pagina que no llego antes de la siguiente cuenta con lo que estuvo a la vista (censurada, marcada con >).
# Y los cuadros (medianas de los renglones de 5 s).
import sys, statistics as st
def pct(l, p): l = sorted(l); return l[min(len(l) - 1, int(len(l) * p))] if l else float('nan')
def resumen(l, nc):
    if not l: return "n=0"
    return "n=%d p50=%.0f p90=%.0f max=%.0f sin_llegar=%d" % (len(l), pct(l, .5), pct(l, .9), max(l), nc)
tot = {"decodas": [], "lista": []}; totc = {"decodas": 0, "lista": 0}; tcuad = []
for ruta in sys.argv[1:]:
    ev = [l.rstrip("\n").split("\t") for l in open(ruta)]
    pags = []  # [clave, t0, tdec, tlis]
    cuad = []
    for c in ev:
        if c[0] == "pagina": pags.append([c[1], float(c[2]), None, None])
        elif c[0] == "decodas" and pags and c[1] == pags[-1][0] and pags[-1][2] is None: pags[-1][2] = float(c[2]) - pags[-1][1]
        elif c[0] == "lista" and pags and c[1] == pags[-1][0] and pags[-1][3] is None: pags[-1][3] = float(c[2]) - pags[-1][1]
        elif c[0] == "cuadros": cuad.append([float(x) for x in c[2:6]])
    loc = {"decodas": [], "lista": []}; locc = {"decodas": 0, "lista": 0}
    for i, p in enumerate(pags):
        if i == 0 or p[0].startswith("|"): continue   # la del arranque
        fin = pags[i + 1][1] if i + 1 < len(pags) else None
        for j, k in ((2, "decodas"), (3, "lista")):
            if p[j] is not None: loc[k].append(p[j])
            elif fin is not None: loc[k].append(fin - p[1]); locc[k] += 1
    for k in loc: tot[k] += loc[k]; totc[k] += locc[k]
    tcuad += cuad
    cs = lambda i: st.median([x[i] for x in cuad]) if cuad else float('nan')
    print("%-28s decodas %s | lista %s | cuadros p50 %.1f p95 %.1f p99 %.1f max %.1f" % (ruta.split("/")[-2], resumen(loc["decodas"], locc["decodas"]), resumen(loc["lista"], locc["lista"]), cs(0), cs(1), cs(2), max([x[3] for x in cuad] or [0])))
cs = lambda i: st.median([x[i] for x in tcuad]) if tcuad else float('nan')
print("%-28s decodas %s | lista %s | cuadros p50 %.1f p95 %.1f p99 %.1f" % ("TOTAL", resumen(tot["decodas"], totc["decodas"]), resumen(tot["lista"], totc["lista"]), cs(0), cs(1), cs(2)))
