#!/usr/bin/env python3
# estados.py REF1 REF2 [OTRA...] -- por pantalla, el conjunto de estados de dibujo (mezcla, etapas, medida de la textura, destino; sin n
# ni nombres) de cada corrida de $TARJETA/trazas/NOMBRE (pantallas.sh), y si los de cada otra corrida estan dentro de la union de las dos
# de referencia («fuera») y cuales de los que tienen las dos de referencia le faltan («faltan»). Las corridas de la rocola entera no se
# repiten igual (azar sin semilla, la hora): lo que falta en una corrida puede salir en otra; lo que sale fuera, no deberia.
import os, re, sys
trazas = os.path.join(os.environ.get("TARJETA", "/root/rocola-trabajo/port/tarjeta"), "trazas")
pantallas = ["pared", "disco", "buscador", "asistente", "video", "perf-todo", "perf-grano", "perf-media", "ctrlf3", "atraccion"]
def conj(n, p):
    s = set()
    f = os.path.join(trazas, n, p + ".txt")
    if not os.path.exists(f): return None
    for l in open(f):
        l = re.sub(r"^dibujo n=\d+ ", "", l.strip()); l = re.sub(r"#\d+", "", l); s.add(l)
    return s
refs, otras = sys.argv[1:3], sys.argv[3:]
for p in pantallas:
    a, b = conj(refs[0], p), conj(refs[1], p)
    if a is None or b is None: continue
    u = a | b; inter = a & b
    linea = "%-10s ref: %2d estados (%2d en las dos)" % (p, len(u), len(inter))
    for o in otras:
        c = conj(o, p)
        if c is None: linea += " | %s: no esta" % o; continue
        fuera = c - u; falta = inter - c
        linea += " | %s: %2d, fuera %d, faltan %d" % (o, len(c), len(fuera), len(falta))
        for f in sorted(fuera): linea += "\n      fuera (" + o + "): " + f
        for f in sorted(falta): linea += "\n      falta (" + o + "): " + f
    print(linea)
