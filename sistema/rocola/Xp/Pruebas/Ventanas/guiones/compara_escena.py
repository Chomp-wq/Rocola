#!/usr/bin/env python3
# compara_escena.py DIR_A DIR_B [--ruido RUIDO.json]
# Compara lo que dejaron dos corridas de la misma escena (corre.sh): la traza (compara_trazas.py), los estados de XP (iguales salvo el
# milisegundo en que se escribieron y la fecha del programa), que rocola_estado.txt este o falte en las dos, rocola_config.txt y
# rocola_teclas.txt byte a byte y las capturas (las de cada capa de Dibujo2D -las hojas- byte a byte en los pixeles; la pantalla entera y la de la tarjeta se
# mueven solas -el cielo-: se dice cuantos pixeles cambian).
import sys, os, re, subprocess, json

def png_igual(a, b):
    # la ventana de prueba F12 (GpuTestWindow: 1030x732) lleva en su titulo los cuadros por segundo, que son de la maquina de ese momento: se
    # tapa el texto de la barra (de la X de cerrar para atras) en las dos antes de comparar; el marco, los bordes y la «X» si cuentan
    if re.search(r"-1030x732\.png$", a):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            ra, rb = os.path.join(d, "a.png"), os.path.join(d, "b.png")
            for o, x in ((a, ra), (b, rb)): subprocess.run(["convert", o, "-fill", "black", "-draw", "rectangle 8,3 1000,28", x], check=True)
            return png_igual(ra, rb)
    r = subprocess.run(["compare", "-metric", "AE", a, b, "null:"], capture_output=True, text=True)
    try: return int(float(r.stderr.split()[0]))
    except Exception: return -1

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from compara_trazas import equivalentes   # (la tabla de equivalencias de las ventanas: las piezas nativas con su nombre de WinForms)

def transparente(png):
    r = subprocess.run(["convert", png, "-alpha", "extract", "-format", "%[fx:maxima]", "info:"], capture_output=True, text=True)
    return r.stdout.strip() == "0"

def norm_estado(t):
    t = re.sub(r"^t=\d+\n", "", t)
    t = "\n".join(equivalentes(l) for l in t.split("\n"))
    t = re.sub(r" *\[\d+ FPS, \d+ estrellas\]", "", t)   # (los cuadros por segundo del titulo de la ventana F12: los de la maquina de ese momento)
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
    # la capa de una hoja nativa se guarda como «-hoja-»; la de la referencia, como la de Dibujo2D: se comparan igual (punto por punto)
    nb = {f.replace("-hoja-", "-dibujo2d-"): f for f in os.listdir(b)}
    # (f12-como-hoja) la referencia pone, encima de la tarjeta de la ventana F12, otro tramo de Dibujo2D para los controles que lleva por encima
    # (ninguno): transparente del todo, y la hoja nativa no lo arma. Con ella detras de la rocola, ese tramo corre la cuenta de las capas: se
    # dejan de lado, en las dos, las capas de dibujo transparentes del todo y las demas se emparejan por orden (la capa N de la captura)
    for cap in sorted({re.sub(r"\.capa\d+-.*$", "", f) for f in os.listdir(a) if ".capa" in f}):
        def capas(d):
            r = []
            for f in os.listdir(d):
                m = re.match(re.escape(cap) + r"\.capa(\d+)-(.*)$", f)
                if m: r.append((int(m.group(1)), f))
            r.sort()
            return [f for _, f in r if not ("-dibujo2d-" in f.replace("-hoja-", "-dibujo2d-") and transparente(os.path.join(d, f)))]
        la, lb = capas(a), capas(b)
        for fa, fb in zip(la, lb):
            if fa.replace("-hoja-", "-dibujo2d-") != fb.replace("-hoja-", "-dibujo2d-"):
                nb[fa] = fb   # (el mismo tamano y sitio en el nombre: si no, se vera como diferente)
    for f in sorted(os.listdir(a)):
        pa, pb = os.path.join(a, f), os.path.join(b, nb.get(f, f))
        if f.endswith(".png") and not os.path.exists(pb) and transparente(pa):
            # la referencia pone, encima de la tarjeta de la ventana F12, otro tramo de Dibujo2D para los controles que lleva por encima (ninguno):
            # transparente del todo; la hoja nativa no lo arma (no hay nada que ver)
            res.append(f + ": transparente del todo en la referencia (la hoja no lo arma)"); continue
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
    emparejadas = set(nb[f] for f in os.listdir(a) if f in nb)
    for f in sorted(os.listdir(b)):
        if f.endswith(".png") and f not in emparejadas and ".capa" in f and "-dibujo2d-" in f.replace("-hoja-", "-dibujo2d-") and transparente(os.path.join(b, f)): continue   # (transparente: nada que ver)
        if (f.startswith("estado-") or f.endswith(".png")) and f not in emparejadas and not os.path.exists(os.path.join(a, f.replace("-hoja-", "-dibujo2d-"))): res.append("sobra " + f); malo = True
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
