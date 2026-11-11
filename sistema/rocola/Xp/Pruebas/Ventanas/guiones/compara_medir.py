#!/usr/bin/env python3
# compara_medir.py DIR_A DIR_B
# La escena f3medir («Medir este equipo» y el historial de caratulas) de dos corridas. Lo que se mide (milisegundos por cuadro, cuadros por
# segundo, cual es la peor pantalla, la hora, cuanto tarda en volver) sale distinto en dos corridas del MISMO programa, asi que aqui no se
# comparan las cuentas por segundo ni los textos medidos, sino:
#  - la SECUENCIA de la traza (teclas, raton, foco, hojas que se abren y se cierran, dialogos): igual;
#  - cada ESTADO: las mismas hojas y las mismas piezas, cada una con su clase, su sitio, si se ve, si esta encendida, su orden y el foco;
#    el texto puede cambiar solo en las piezas de lo medido (se listan cuantas), y «escrito=» (Medir deja o no una letra en el buscador
#    segun cual fue la peor pantalla);
#  - cada CAPA de una hoja: punto por punto igual FUERA de las cajas de las piezas cuyo texto cambio (lo medido).
import sys, os, re, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from compara_trazas import equivalentes
from compara_escena import norm_estado
import numpy as np
from PIL import Image

PIEZA = re.compile(r"^( +)(\S+?)(?:'(.*)')? @(-?\d+),(-?\d+),(\d+)x(\d+) (.*)$", re.S)
HOJA = re.compile(r"^hoja (\S+?)'(.*)' @(-?\d+),(-?\d+),(\d+)x(\d+)$", re.S)

def lineas(ruta):
    # (un texto con saltos va en una sola linea del volcado, con \n escrito)
    return norm_estado(open(ruta, encoding="utf-8").read()).split("\n")

def compara_estado(la, lb, cajas):
    """devuelve (estructura_distinta, textos_distintos); apunta en 'cajas' {(x, y, w, h) de la hoja: [cajas de piezas con otro texto]}"""
    mal, textos = [], 0
    if len(la) != len(lb): return ["%d renglones frente a %d" % (len(la), len(lb))], 0
    hoja = None; pila = []
    for a, b in zip(la, lb):
        ha, hb = HOJA.match(a), HOJA.match(b)
        if ha or hb:
            if not (ha and hb) or ha.groups()[2:] != hb.groups()[2:] or ha.group(1) != hb.group(1): mal.append(a + " | " + b); continue
            hoja = tuple(int(v) for v in ha.groups()[2:]); pila = []; cajas.setdefault(hoja, []); continue
        pa, pb = PIEZA.match(a), PIEZA.match(b)
        if pa and pb:
            ga, gb = pa.groups(), pb.groups()
            if ga[:2] + ga[3:] != gb[:2] + gb[3:]: mal.append(a.strip() + " | " + b.strip()); continue
            s = len(ga[0]); x, y, w, h = (int(v) for v in ga[3:7])
            while pila and pila[-1][0] >= s: pila.pop()
            ox, oy = (pila[-1][1], pila[-1][2]) if pila else (3, 29)   # el cliente de la hoja dentro de su capa: borde de 3 y barra de 26
            pila.append((s, ox + x, oy + y))
            if ga[2] != gb[2]:
                textos += 1
                if hoja is not None: cajas[hoja].append((ox + x, oy + y, w, h))
        elif a != b:
            if a.startswith("buscador=") and b.startswith("buscador=") and a.split(" ")[0] == b.split(" ")[0]: textos += 1
            else: mal.append(a + " | " + b)
    return mal, textos

def main(a, b):
    malo = False
    r = subprocess.run([sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), "compara_trazas.py"),
                        os.path.join(a, "traza.txt"), os.path.join(b, "traza.txt"), "--holgura", "1000000"], capture_output=True, text=True)
    sec = [l for l in r.stdout.split("\n") if "secuencia" in l]
    print("traza: " + sec[0].strip() if sec else "traza: ?")
    dif = [l for l in sec[1:] if "DIFERENTE" in l]
    if dif: malo = True; print("   " + "\n   ".join(l.strip() for l in dif[:40]))
    else: print("   secuencia IGUAL (las cuentas por segundo no se comparan: Medir tarda lo que tarda)")
    cajas = {}
    for f in sorted(os.listdir(a)):
        if not f.startswith("estado-"): continue
        pb = os.path.join(b, f)
        if not os.path.exists(pb): print("falta " + f); malo = True; continue
        mal, textos = compara_estado(lineas(os.path.join(a, f)), lineas(pb), cajas)
        if mal: malo = True; print("%s: DIFERENTE en la estructura\n   %s" % (f, "\n   ".join(mal[:20])))
        else: print("%s: las mismas piezas, sitios y foco; %d textos de lo medido distintos" % (f, textos))
    nb = {f.replace("-hoja-", "-dibujo2d-"): f for f in os.listdir(b)}
    for f in sorted(os.listdir(a)):
        m = re.search(r"\.capa\d+-dibujo2d-(-?\d+),(-?\d+)-(\d+)x(\d+)\.png$", f)
        if not m: continue
        hoja = tuple(int(v) for v in m.groups())
        if hoja not in cajas: continue   # (las capas de la pantalla de la rocola, no de una hoja)
        pb = os.path.join(b, nb.get(f, f))
        if not os.path.exists(pb): print("falta " + f); malo = True; continue
        ia = np.asarray(Image.open(os.path.join(a, f)).convert("RGBA")).astype(np.int16); ib = np.asarray(Image.open(pb).convert("RGBA")).astype(np.int16)
        if ia.shape != ib.shape: print(f + ": otra medida"); malo = True; continue
        d = (ia != ib).any(axis=2); total = int(d.sum())
        for (x, y, w, h) in cajas[hoja]: d[max(0, y):max(0, y + h), max(0, x):max(0, x + w)] = False
        fuera = int(d.sum())
        print("%s: %d puntos distintos fuera de lo medido (%d dentro, en %d cajas)%s" % (f, fuera, total - fuera, len(set(cajas[hoja])), "" if fuera == 0 else " (DEBE SER 0)"))
        if fuera: malo = True
    for f in ("rocola_config.txt", "rocola_teclas.txt"):
        pa, pb = os.path.join(a, f), os.path.join(b, f)
        if os.path.exists(pa) != os.path.exists(pb) or (os.path.exists(pa) and open(pa, "rb").read() != open(pb, "rb").read()): print(f + " DIFERENTE"); malo = True
        elif os.path.exists(pa): print(f + ": igual")
    ea, eb = os.path.exists(os.path.join(a, "rocola_estado.txt")), os.path.exists(os.path.join(b, "rocola_estado.txt"))
    print("rocola_estado.txt: %s / %s" % ("esta" if ea else "falta", "esta" if eb else "falta"))
    if ea != eb: malo = True
    print("RESULTADO: " + ("DIFERENTE" if malo else "IGUAL"))
    return 1 if malo else 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))
