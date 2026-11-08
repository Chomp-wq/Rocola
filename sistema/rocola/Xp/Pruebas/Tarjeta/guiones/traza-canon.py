#!/usr/bin/env python3
# traza-canon.py REGISTRO -- las lineas «dibujo» de ROCOLA_TRAZA_DIBUJOS sin lo que depende del reloj (caja y color) y con las texturas
# renombradas por orden de aparicion (#1, #2...: los nombres de OpenGL dependen de en que orden se crearon).
import re, sys
nombres = {}
for l in open(sys.argv[1], encoding="utf-8", errors="replace"):
    if not l.startswith("dibujo "): continue
    l = re.sub(r" caja=\([^)]*\)-\([^)]*\)", "", l.rstrip("\n"))
    l = re.sub(r" color=[0-9A-F]{8}", "", l)
    def ren(m):
        n = m.group(2)
        if n not in nombres: nombres[n] = len(nombres) + 1
        return m.group(1) + "#" + str(nombres[n])
    l = re.sub(r"(tex=\d+x\d+)#(\d+)", ren, l)
    print(l)
