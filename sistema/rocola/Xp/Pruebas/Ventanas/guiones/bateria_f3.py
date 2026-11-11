#!/usr/bin/env python3
# bateria_f3.py DIR [DIR ...]
# LO QUE MIRA LA BATERIA DE XP «ventanaf3.cs» (Rocola/pruebas: Ctrl+F3 de verdad, pagina por pagina y hoja por hoja) SOBRE EL PROGRAMA
# TRADUCIDO, con lo que este deja en la escena f3vistas: el volcado de cada pieza (clase, texto, sitio, si se ve) en cada una de sus 25
# vistas (estado-*.txt, ROCOLA_ESTADO_EN). La bateria de XP no se puede correr tal cual contra la traduccion (es un programa de Mono que
# usa Form, Control, Controls.Find, RectangleToScreen y Application.OpenForms de WinForms; las piezas nativas no tienen esa API, a
# proposito): aqui van las mismas tres comprobaciones de sitio, con los mismos criterios, leidas del volcado en vez de por Control:
#   - nada visible queda fuera de la ventana (el cliente de 960x732);
#   - ningun texto ni mando queda debajo del boton Listo;
#   - ningun letrero pisa un mando (un letrero que va antes en el orden de dibujo y cruza un boton, un interruptor o un deslizador).
# Lo que la bateria mide con las letras (que cada texto quepa en su caja) no esta aqui: lo fija la comparacion punto por punto de cada vista
# con la referencia. Sale con 1 si algo falla.
import sys, os, re

PIEZA = re.compile(r"^( +)(\S+?)(?:'(.*)')? @(-?\d+),(-?\d+),(\d+)x(\d+) (\S)(\S)(.*)$", re.S)
HOJA = re.compile(r"^hoja (\S+?)'(.*)' @(-?\d+),(-?\d+),(\d+)x(\d+)$", re.S)
MANDOS = ("BotonPlano", "Interruptor", "Deslizador")
LETREROS = ("Rotulo", "Label")

def cruza(a, b):   # Rectangle.IntersectsWith
    return a[0] < b[0] + b[2] and b[0] < a[0] + a[2] and a[1] < b[1] + b[3] and b[1] < a[1] + a[3]

def dentro(v, r):  # Rectangle.Contains(Rectangle)
    return v[0] <= r[0] and r[0] + r[2] <= v[0] + v[2] and v[1] <= r[1] and r[1] + r[3] <= v[1] + v[3]

def arbol(ruta):
    """la primera hoja con piezas (Ctrl+F3) del volcado: lista de nodos {clase, texto, caja absoluta en el cliente, ve, hijos, padre, indice}"""
    raiz = None; pila = []
    for l in open(ruta, encoding="utf-8").read().split("\n"):
        h = HOJA.match(l)
        if h:
            if raiz is not None: break   # (otra hoja encima: el historial de caratulas; aqui solo Ctrl+F3)
            raiz = {"clase": h.group(1), "texto": h.group(2), "caja": (0, 0, int(h.group(5)) - 6, int(h.group(6)) - 32), "ve": True, "hijos": [], "padre": None, "s": 0}
            pila = [raiz]; continue
        m = PIEZA.match(l)
        if not m or raiz is None: continue
        s = len(m.group(1)); x, y, w, hh = (int(v) for v in m.groups()[3:7])
        while pila[-1]["s"] >= s: pila.pop()
        p = pila[-1]
        n = {"clase": m.group(2), "texto": m.group(3) or "", "local": (x, y, w, hh), "caja": (p["caja"][0] + x, p["caja"][1] + y, w, hh) if p is not raiz else (x, y, w, hh),
             "ve": m.group(8) == "v", "hijos": [], "padre": p, "s": s, "indice": len(p["hijos"])}
        p["hijos"].append(n); pila.append(n)
    return raiz

def corto(s):
    s = s.replace("\\n", " | "); return s if len(s) <= 60 else s[:60] + "..."

def busca_listo(n):
    for h in n["hijos"]:
        if not h["ve"]: continue
        if h["clase"] == "BotonPlano" and h["texto"] == "Listo": return h
        r = busca_listo(h)
        if r: return r
    return None

def revisa(p, listo, ventana, donde, mal, cuenta):
    for c in p["hijos"]:
        if not c["ve"]: continue
        r = c["caja"]
        if r[2] > 0 and r[3] > 0 and not dentro(ventana, r): mal.append("%s: fuera de la ventana: %s «%s» %s" % (donde, c["clase"], corto(c["texto"]), r))
        else: cuenta[0] += 1
        if c["hijos"]: revisa(c, listo, ventana, donde, mal, cuenta); continue
        if c["clase"] in MANDOS:
            for s in p["hijos"]:
                if s is not c and s["ve"] and s["clase"] in LETREROS and s["indice"] < c["indice"] and cruza(s["local"], c["local"]):
                    mal.append("%s: un letrero pisa un mando: «%s» sobre %s «%s»" % (donde, corto(s["texto"]), c["clase"], corto(c["texto"])))
            cuenta[0] += 1
        if c is not listo and cruza(r, listo["caja"]): mal.append("%s: debajo de Listo: %s «%s»" % (donde, c["clase"], corto(c["texto"])))
        else: cuenta[0] += 1

total_mal = 0
for d in sys.argv[1:]:
    mal, cuenta, vistas = [], [0], 0
    for f in sorted(os.listdir(d)):
        if not f.startswith("estado-") or f == "estado-fin.txt": continue
        raiz = arbol(os.path.join(d, f))
        if raiz is None: mal.append(f + ": no hay ninguna hoja abierta"); continue
        listo = busca_listo(raiz)
        if listo is None: mal.append(f + ": no hay boton Listo"); continue
        vistas += 1
        revisa(raiz, listo, raiz["caja"], f[7:-4], mal, cuenta)
    for m in mal: print("  FALLO " + m)
    print("%s: %s" % (d, ("OK: %d comprobaciones en %d vistas" % (cuenta[0], vistas)) if not mal else ("FALLO: %d problemas" % len(mal))))
    total_mal += len(mal)
sys.exit(1 if total_mal else 0)
