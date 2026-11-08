#!/usr/bin/env python3
# compara_escena.py DIR_A DIR_B [--ruido RUIDO.json]
# Compara lo que dejaron dos corridas de la misma escena (corre.sh): la traza (compara_trazas.py), los estados de XP (iguales salvo el
# milisegundo en que se escribieron y la fecha del programa), que rocola_estado.txt este o falte en las dos, rocola_config.txt y
# rocola_teclas.txt byte a byte y las capturas (las de cada capa de Dibujo2D -las hojas- byte a byte en los pixeles; la pantalla entera y la de la tarjeta se
# mueven solas -el cielo-: se dice cuantos pixeles cambian).
import sys, os, re, subprocess, json

def png_igual(a, b):
    r = subprocess.run(["compare", "-metric", "AE", a, b, "null:"], capture_output=True, text=True)
    try: return int(float(r.stderr.split()[0]))
    except Exception: return -1

def norm_estado(t):
    t = re.sub(r"^t=\d+\n", "", t)
    return re.sub(r"Esta rocola es del \d\d/\d\d/\d{4} \d\d:\d\d", "Esta rocola es del FECHA", t)

def compara(a, b, ruido_ruta=None):
    res, malo = [], False
    ta, tb = os.path.join(a, "traza.txt"), os.path.join(b, "traza.txt")
    if os.path.exists(ta) and os.path.exists(tb):
        cmd = [sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), "compara_trazas.py"), ta, tb]
        if ruido_ruta: cmd += ["--ruido", ruido_ruta]
        r = subprocess.run(cmd, capture_output=True, text=True)
        res.append("traza: " + r.stdout.strip().replace("\n", "\n   "))
        malo |= r.returncode != 0
    for f in sorted(os.listdir(a)):
        pa, pb = os.path.join(a, f), os.path.join(b, f)
        if f.startswith("estado-"):
            if not os.path.exists(pb): res.append("falta " + f); malo = True; continue
            ea, eb = norm_estado(open(pa).read()), norm_estado(open(pb).read())
            if ea == eb: res.append(f + ": igual")
            else:
                malo = True
                import difflib
                res.append(f + ": DIFERENTE\n   " + "\n   ".join(difflib.unified_diff(ea.split("\n"), eb.split("\n"), lineterm="", n=0)))
        elif f in ("rocola_config.txt", "rocola_teclas.txt"):
            if not os.path.exists(pb) or open(pa, "rb").read() != open(pb, "rb").read(): res.append(f + " DIFERENTE"); malo = True
            else: res.append(f + ": igual")
        elif f.endswith(".png"):
            if not os.path.exists(pb): res.append("falta " + f); malo = True; continue
            n = png_igual(pa, pb)
            exacta = "dibujo2d" in f   # las hojas y lo de Dibujo2D: igual byte a byte
            res.append("%s: %d pixeles distintos%s" % (f, n, "" if n == 0 else (" (DEBE SER 0)" if exacta else " (pantalla: el cielo se mueve)")))
            if exacta and n != 0: malo = True
    for f in sorted(os.listdir(b)):
        if (f.startswith("estado-") or f.endswith(".png")) and not os.path.exists(os.path.join(a, f)): res.append("sobra " + f); malo = True
    ea, eb = os.path.exists(os.path.join(a, "rocola_estado.txt")), os.path.exists(os.path.join(b, "rocola_estado.txt"))
    res.append("rocola_estado.txt: %s / %s" % ("esta" if ea else "falta", "esta" if eb else "falta"))
    if ea != eb: malo = True
    return malo, res

if __name__ == "__main__":
    args = sys.argv[1:]
    ruido = None
    if "--ruido" in args: i = args.index("--ruido"); ruido = args[i + 1]; del args[i:i + 2]
    malo, res = compara(args[0], args[1], ruido)
    print("\n".join(res))
    print("RESULTADO: " + ("DIFERENTE" if malo else "IGUAL"))
    sys.exit(1 if malo else 0)
