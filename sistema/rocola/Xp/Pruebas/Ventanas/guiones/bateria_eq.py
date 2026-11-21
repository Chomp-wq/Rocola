#!/usr/bin/env python3
# bateria_eq.py DIR -- lo que mira la escena f3ecualizador (escenas.sh; los pasos en vistas_f3.py «ecualizador») en la hoja «Ecualizador...»
# de Ctrl+F3 (el ecualizador del dueno de XP del 17-11-2026: Form1.Ajustes.cs, Form1.EcualizadorDueno.cs). La referencia congelada
# (aef2211) no tiene esa hoja, asi que no se compara punto por punto: se mira lo que fija la bateria de XP (ventanaf3.cs no se puede correr:
# es de WinForms) con el volcado de cada pieza (estado-*.txt) y las capturas de la hoja (*.png.capa4-hoja-29,2-966x764.png):
#   - el boton «Ecualizador...» en Ajustes del sonido, y que abre la hoja: los 10 deslizadores (31 Hz a 16 kHz) con su nombre y su valor,
#     «Todo en 0», «Sin ecualizador», el letrero de la memoria en uso, el margen y el dibujo de la curva (CurvaEqView) a la vista;
#   - mover una banda con el raton cambia su valor, su letrero, el margen y EL DIBUJO de la curva (puntos de su caja); «Sin ecualizador»
#     pasa a «Ponerlo (N s)» y otra vez vuelve; «Todo en 0» lo deja como al abrir (el mismo dibujo, punto por punto); volver la esconde;
#   - lo guardado: eq_dueno= con la banda de 500 Hz en +6 y eq_dueno_billar= en 0 (rocola_config.txt, al cerrar);
#   - las tres comprobaciones de sitio de ventanaf3.cs en las vistas de la hoja (bateria_f3.py: nada fuera, nada bajo «Listo», ningun
#     letrero sobre un mando).
# Sale con 1 si algo falla.
import os, re, sys
import numpy as np
from PIL import Image, ImageChops
d = sys.argv[1]
import subprocess
NOMBRES = ["31 Hz", "63 Hz", "125 Hz", "250 Hz", "500 Hz", "1 kHz", "2 kHz", "4 kHz", "8 kHz", "16 kHz"]
CURVA = (507, 227, 507 + 424, 227 + 168)   # la caja de CurvaEqView en la captura de la hoja: (48+472+16-29, 131+98-2)
bien = mal = 0
def si(q, ok):
    global bien, mal
    print(("BIEN  " if ok else "MAL   ") + q)
    if ok: bien += 1
    else: mal += 1

def lee(n):
    t = open(os.path.join(d, "estado-%s.txt" % n), encoding="utf-8").read().split("\n")
    v = {"des": [], "ve": False, "botones": {}, "rot": [], "curva": None, "foco": ""}
    for i, l in enumerate(t):
        if l.startswith("foco="): v["foco"] = l[5:]
        m = re.match(r"^\s+Deslizador @84,(\d+),270x22 (\S)(\S) t\d+ value=(\d+)", l)
        if m and (int(m.group(1)) - 40) % 30 == 0 and len(v["des"]) < 10:
            k = (int(m.group(1)) - 40) // 30
            nombre = re.match(r"^\s+Rotulo'(.*)' @16,", t[i - 1]); valor = re.match(r"^\s+Rotulo'(.*)' @362,", t[i + 1])
            v["des"].append((k, m.group(2) == "v", int(m.group(4)), nombre.group(1) if nombre else None, valor.group(1) if valor else None))
        m = re.match(r"^\s+BotonPlano'(Ecualizador\.\.\.|Todo en 0|Sin ecualizador|Ponerlo \(\d+ s\))' @\S+ (\S)(\S)", l)
        if m: v["botones"][m.group(1)] = m.group(2) == "v"
        m = re.match(r"^\s+CurvaEqView @16,98,424x168 (\S)(\S)", l)
        if m: v["curva"] = m.group(1) == "v"
        m = re.match(r"^\s+Rotulo'((?:Usando|Margen|Todo baja|Con la que suena).*)' @16,\d+,424x\d+ (\S)", l)
        if m: v["rot"].append((m.group(1), m.group(2) == "v"))
    return v

def hoja(n):
    return Image.open(os.path.join(d, "%s.png.capa4-hoja-29,2-966x764.png" % n)).convert("RGB")
def distintos(a, b, caja):
    return int((np.asarray(ImageChops.difference(a.crop(caja), b.crop(caja))).max(axis=2) > 0).sum())
def colores(a, caja): return len(np.unique(np.asarray(a.crop(caja)).reshape(-1, 3), axis=0))
def db(medios):   # el texto de su letrero (eqTxt)
    x = (medios - 24) * 0.5
    if x == 0: return "0 dB"
    s = ("%+.1f" % x).replace(".", ",")
    return (s[:-2] if s.endswith(",0") else s) + " dB"

aju = lee("q1aju")
si("Ajustes del sonido: el boton «Ecualizador...» a la vista", aju["botones"].get("Ecualizador...") is True)
si("Ajustes del sonido: la hoja del ecualizador aun escondida", len(aju["des"]) == 10 and not any(x[1] for x in aju["des"]))
eq = lee("q2eq")
si("la hoja abre: 10 deslizadores a la vista, en orden", [x[0] for x in eq["des"]] == list(range(10)) and all(x[1] for x in eq["des"]))
si("cada uno con su nombre (31 Hz ... 16 kHz)", [x[3] for x in eq["des"]] == NOMBRES)
si("todos en 0 dB al abrir (valor 24 de 0..48) con su letrero", all(x[2] == 24 and x[4] == "0 dB" for x in eq["des"]))
si("«Todo en 0» y «Sin ecualizador» a la vista", eq["botones"].get("Todo en 0") is True and eq["botones"].get("Sin ecualizador") is True)
si("el dibujo de la curva (CurvaEqView) a la vista", eq["curva"] is True)
si("el letrero de la memoria en uso (la normal) y el margen («nada»)", ("Usando: la normal (modo billar apagado)", True) in eq["rot"] and any(r[0].startswith("Margen: nada") and r[1] for r in eq["rot"]))
h2 = hoja("q2eq")
si("el dibujo no esta vacio (%d colores en su caja)" % colores(h2, CURVA), colores(h2, CURVA) >= 4)
b0 = lee("q3banda0")
si("la banda de 31 Hz con el raton al maximo: +12 dB (%s, valor %d) y las demas igual" % (b0["des"][0][4], b0["des"][0][2]),
   b0["des"][0][2] == 48 and b0["des"][0][4] == "+12 dB" and all(x[2] == 24 for x in b0["des"][1:]))
si("y el margen ya no es «nada»", any(r[0].startswith("Todo baja") or r[0].startswith("Con la que suena") for r in b0["rot"]))
h3 = hoja("q3banda0"); n3 = distintos(h2, h3, CURVA)
si("y el dibujo de la curva cambia (%d puntos de su caja)" % n3, n3 >= 300)
b9 = lee("q4banda9")
si("la de 16 kHz al minimo: -12 dB (%s, valor %d)" % (b9["des"][9][4], b9["des"][9][2]), b9["des"][9][2] == 0 and b9["des"][9][4] == "-12 dB" and b9["des"][0][2] == 48)
h4 = hoja("q4banda9"); n4 = distintos(h3, h4, CURVA)
si("y el dibujo cambia otra vez (%d puntos)" % n4, n4 >= 100)
s5 = lee("q5sin")
pon = [k for k in s5["botones"] if k.startswith("Ponerlo")]
si("«Sin ecualizador» pasa a «%s» (E1) y no toca las bandas" % (pon[0] if pon else "?"), len(pon) == 1 and s5["botones"][pon[0]] and s5["des"][0][2] == 48 and s5["des"][9][2] == 0)
s6 = lee("q6con")
si("otro toque: vuelve a «Sin ecualizador»", s6["botones"].get("Sin ecualizador") is True and not any(k.startswith("Ponerlo") for k in s6["botones"]))
s7 = lee("q7cero")
si("«Todo en 0»: las diez en 0 dB", all(x[2] == 24 and x[4] == "0 dB" for x in s7["des"]) and any(r[0].startswith("Margen: nada") for r in s7["rot"]))
h7 = hoja("q7cero"); n7 = distintos(h2, h7, CURVA)
si("y el dibujo, como al abrir (%d puntos distintos de su caja)" % n7, n7 == 0)
s8 = lee("q8banda4")
si("la de 500 Hz a +6 dB (%s, valor %d)" % (s8["des"][4][4], s8["des"][4][2]), s8["des"][4][2] == 36 and s8["des"][4][4] == "+6 dB")
s9 = lee("q9vuelta")
si("volver: la hoja se esconde", not any(x[1] for x in s9["des"]) and s9["curva"] is False)
cfg = open(os.path.join(d, "rocola_config.txt"), encoding="utf-8").read().replace("\r", "").split("\n")
si("guardado: eq_dueno= con 500 Hz en +6", "eq_dueno=0.0;0.0;0.0;0.0;6.0;0.0;0.0;0.0;0.0;0.0" in cfg)
si("guardado: eq_dueno_billar= en 0", "eq_dueno_billar=0.0;0.0;0.0;0.0;0.0;0.0;0.0;0.0;0.0;0.0" in cfg)
# las tres de sitio de ventanaf3.cs en las nueve vistas (bateria_f3.py, sobre los mismos volcados)
r = subprocess.run([sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), "bateria_f3.py"), d], capture_output=True, text=True)
print("   " + r.stdout.strip().replace("\n", "\n   "))
si("las tres de sitio de ventanaf3.cs (bateria_f3.py) en las vistas de la hoja", r.returncode == 0)
print()
print("TODO BIEN (%d comprobaciones)" % bien if mal == 0 else "%d MAL de %d" % (mal, bien + mal))
sys.exit(0 if mal == 0 else 1)
