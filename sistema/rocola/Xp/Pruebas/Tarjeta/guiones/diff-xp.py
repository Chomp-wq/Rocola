#!/usr/bin/env python3
# diff-xp.py ARCHIVO... -- el diff de Rocola/ARCHIVO (XP) contra sistema/rocola/Xp/Codigo/ARCHIVO quitando lo mecanico de la tarjeta (T1:
# D3D9.Vtx -> Tarjeta.Vtx, D3D9.Release -> Tarjeta.Suelta, D3D9Device -> DispositivoTarjeta). Cuenta las lineas quitadas y puestas, y
# cuantas de las puestas no llevan «EN LINUX», «Linux» ni la marca de las pruebas (con -v las ensena: las de un comentario de varias
# lineas o las que van bajo su comentario «EN LINUX» son normales).
import os, re, subprocess, sys, tempfile
raiz = os.path.abspath(os.path.join(os.path.dirname(os.path.realpath(__file__)), "../../../../../.."))
ver = "-v" in sys.argv
for n in [a for a in sys.argv[1:] if a != "-v"]:
    xp = open(os.path.join(raiz, "Rocola", n), encoding="utf-8").read().replace("\r\n", "\n")
    xp = xp.replace("D3D9Device", "DispositivoTarjeta")
    xp = re.sub(r"\bD3D9\.Vtx2Size\b", "Tarjeta.Vtx2Size", xp); xp = re.sub(r"\bD3D9\.Vtx2\b", "Tarjeta.Vtx2", xp)
    xp = re.sub(r"\bD3D9\.VtxSize\b", "Tarjeta.VtxSize", xp); xp = re.sub(r"\bD3D9\.Vtx\b", "Tarjeta.Vtx", xp)
    xp = xp.replace("D3D9.Release(", "Tarjeta.Suelta(")
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", suffix=".cs", delete=False) as t:
        t.write(xp); tmp = t.name
    d = subprocess.run(["diff", tmp, os.path.join(raiz, "sistema/rocola/Xp/Codigo", n)], capture_output=True, text=True).stdout
    os.unlink(tmp)
    puestas = [l for l in d.splitlines() if l.startswith(">")]
    quitadas = [l for l in d.splitlines() if l.startswith("<")]
    sin = [l for l in puestas if not ("EN LINUX" in l or "Linux" in l or "(pruebas" in l)]
    print("%s: %d lineas quitadas, %d puestas (%d sin marca)" % (n, len(quitadas), len(puestas), len(sin)))
    if ver:
        for l in sin: print("   sin marca: " + l[:200])
