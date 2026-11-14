#!/usr/bin/env python3
# diff-xp.py [--sin-ganchos] [-v] ARCHIVO... -- el diff de Rocola/ARCHIVO (XP) contra sistema/rocola/Xp/Codigo/ARCHIVO quitando lo mecanico de
# la tarjeta (T1: D3D9.Vtx -> Tarjeta.Vtx, D3D9.Release -> Tarjeta.Suelta, D3D9Device -> DispositivoTarjeta). Cuenta las lineas quitadas y
# puestas, y cuantas de las puestas no llevan «EN LINUX», «Linux» ni la marca de las pruebas (con -v las ensena: las de un comentario de
# varias lineas o las que van bajo su comentario «EN LINUX» son normales).
# --sin-ganchos (fase 11, grupo andamio): antes de comparar devuelve los ganchos de Rocola OS (sistema/rocola/Xp/RocolaOs/GANCHOS.tsv: cada
# linea «ahora» de Codigo/ vuelve a ser la de «antes»), para que las cuentas de la traduccion no se muevan con ellos, y los cuenta aparte; al
# final, el total de todos los archivos y los ganchos devueltos. Si un gancho no esta en su linea, lo dice y sale con 1. Sin la opcion, lo de
# siempre.
import io, os, re, subprocess, sys, tempfile
raiz = os.path.abspath(os.path.join(os.path.dirname(os.path.realpath(__file__)), "../../../../../.."))
ver = "-v" in sys.argv
sin_ganchos = "--sin-ganchos" in sys.argv
ganchos = {}   # archivo de Codigo/ -> [(id, linea, antes, ahora)]
if sin_ganchos:
    with open(os.path.join(raiz, "sistema/rocola/Xp/RocolaOs/GANCHOS.tsv"), encoding="utf-8", newline="") as f:
        for l in f:
            l = l.rstrip("\n")
            if not l or l.startswith("#"): continue
            id, archivo, linea, antes, ahora = l.split("\t")
            if archivo.startswith("Codigo/"): ganchos.setdefault(archivo[len("Codigo/"):], []).append((id, int(linea), antes, ahora))
total_q = total_p = total_s = 0
devueltos = {}
malos = []
for n in [a for a in sys.argv[1:] if a not in ("-v", "--sin-ganchos")]:
    xp = open(os.path.join(raiz, "Rocola", n), encoding="utf-8").read().replace("\r\n", "\n")
    xp = xp.replace("D3D9Device", "DispositivoTarjeta")
    xp = re.sub(r"\bD3D9\.Vtx2Size\b", "Tarjeta.Vtx2Size", xp); xp = re.sub(r"\bD3D9\.Vtx2\b", "Tarjeta.Vtx2", xp)
    xp = re.sub(r"\bD3D9\.VtxSize\b", "Tarjeta.VtxSize", xp); xp = re.sub(r"\bD3D9\.Vtx\b", "Tarjeta.Vtx", xp)
    xp = xp.replace("D3D9.Release(", "Tarjeta.Suelta(")
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", suffix=".cs", delete=False) as t:
        t.write(xp); tmp = t.name
    # (los dos lados sin CR: un archivo de XP con CRLF se compara linea por linea con su traduccion, que conserva sus finales)
    if sin_ganchos and n in ganchos:
        # (los ganchos se devuelven en los bytes, por lineas como las cuenta el manifiesto -cortando solo en «\n», con su CR al final si lo
        # lleva-, y despues se lee igual que sin la opcion: la lectura de texto de Python cambia tambien un CR suelto por un salto de linea,
        # y Form1.Datos.cs tiene dos lineas con CR CR LF)
        crudo = open(os.path.join(raiz, "sistema/rocola/Xp/Codigo", n), "rb").read().split(b"\n")
        for id, linea, antes, ahora in ganchos[n]:
            l = crudo[linea - 1] if linea <= len(crudo) else None
            cr = l is not None and l.endswith(b"\r")
            if l is not None and (l[:-1] if cr else l) == ahora.encode("utf-8"):
                crudo[linea - 1] = antes.encode("utf-8") + (b"\r" if cr else b""); devueltos[n] = devueltos.get(n, 0) + 1
            else:
                malos.append("%s: %s:%d no es la linea del manifiesto (ganchos.py lo explica)" % (id, n, linea))
        cod = io.TextIOWrapper(io.BytesIO(b"\n".join(crudo)), encoding="utf-8").read().replace("\r\n", "\n")
    else:
        cod = open(os.path.join(raiz, "sistema/rocola/Xp/Codigo", n), encoding="utf-8").read().replace("\r\n", "\n")
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", suffix=".cs", delete=False) as t:
        t.write(cod); tmp2 = t.name
    d = subprocess.run(["diff", tmp, tmp2], capture_output=True, text=True).stdout
    os.unlink(tmp); os.unlink(tmp2)
    puestas = [l for l in d.splitlines() if l.startswith(">")]
    quitadas = [l for l in d.splitlines() if l.startswith("<")]
    sin = [l for l in puestas if not ("EN LINUX" in l or "Linux" in l or "(pruebas" in l)]
    print("%s: %d lineas quitadas, %d puestas (%d sin marca)" % (n, len(quitadas), len(puestas), len(sin)))
    total_q += len(quitadas); total_p += len(puestas); total_s += len(sin)
    if ver:
        for l in sin: print("   sin marca: " + l[:200])
if sin_ganchos:
    print("TOTAL: %d lineas quitadas, %d puestas (%d sin marca)" % (total_q, total_p, total_s))
    print("ganchos devueltos antes de comparar: %d en %d archivos%s" % (sum(devueltos.values()), len(devueltos),
          (" (" + ", ".join("%s %d" % (k, v) for k, v in sorted(devueltos.items())) + ")") if devueltos else ""))
    for m in malos: print("MAL   " + m)
    sys.exit(1 if malos else 0)
