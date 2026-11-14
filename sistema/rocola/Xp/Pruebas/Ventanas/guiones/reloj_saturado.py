#!/usr/bin/env python3
# reloj_saturado.py mide TRAZA_O_CARPETA [...]      lo que da el reloj de la ventana de prueba F12 en cada traza
# reloj_saturado.py simula [A B [A2 B2]]            el modelo: cuantos Tick da un reloj de 15 ms segun lo que cuestan los cuadros
#
# POR QUE la cuenta del reloj de la ventana F12 (GpuTestWindow.timer, 15 ms: cada Tick pinta y presenta un cuadro de la tarjeta) no sirve
# para comparar dos programas con una holgura fija (grupo arreglo-f12vigia, PORT_arreglo-f12vigia.md; lo usa compara_trazas.py,
# RELOJES_DE_CUADRO). Fuera de pantalla (llvmpipe) el bucle de la pantalla, con la ventana F12 abierta, hace esto (Nativo/PantallaRocola.cs,
# la regla de WM_TIMER de Windows; la misma en la referencia):
#   - si el reloj vencio, su Tick (cuesta A ms: el cuadro de la ventana F12 y componer la pantalla) y su vencimiento pasa al primero de la
#     rejilla de 15 ms que aun no ha pasado (los que se juntaron mientras tanto salen como UNO; el plazo no se corre);
#   - si no, Application.Idle: un cuadro de la rocola (cuesta B ms: el suyo y componer la pantalla).
# Con A + B cerca de dos plazos (30 ms) que el Tick siguiente caiga antes o despues de un vencimiento de la rejilla cambia con UN
# milisegundo de A o de B, y la cuenta en los 5,25 s que la escena f12vigia tiene la ventana abierta salta de 175 a 349: no es monotona
# (una maquina mas lenta puede dar MAS Tick: con A de 15 ms el reloj siempre esta vencido y la rocola ya no pinta entre dos suyos).
#   «mide» saca de cada traza los Tick del reloj, el intervalo corto (dos Tick seguidos: A) y el largo (un Tick y un cuadro de la rocola:
#   A + B). «simula» da la cuenta del modelo para A y B (ms) a varias velocidades de la maquina; sin argumentos, con lo medido el
#   14-11-2026: la referencia A=9,2 B=21,2 y la traduccion A=7,5 B=20,5 (compone una capa transparente menos en cada cuadro: la
#   que la capa de la referencia arma encima de la tarjeta de la ventana F12; con esa capa puesta a proposito da lo que la referencia).
import sys, os, random, statistics


def tics(ruta):
    if os.path.isdir(ruta):
        ruta = os.path.join(ruta, "traza.txt")
    t = []
    with open(ruta, encoding="utf-8", errors="replace") as f:
        for l in f:
            p = l.rstrip("\n").split("\t", 1)
            if len(p) == 2 and (p[1].startswith("tick D3D9Star.cs:1997 ") or p[1].startswith("tick D3D9Star.cs:1867 ")):
                t.append(int(p[0]))
    return t


def mide(rutas):
    for r in rutas:
        t = tics(r)
        d = [y - x for x, y in zip(t, t[1:])]
        co = [x for x in d if x < 18]
        la = [x for x in d if 18 <= x < 150]
        ab = sum(x for x in d if x < 150)
        print("%-28s Tick=%3d  abierta sin capturas=%4d ms  corto: %3d de %4.1f ms  largo: %3d de %4.1f ms"
              % (os.path.basename(os.path.dirname(r) if r.endswith("traza.txt") else r.rstrip("/")), len(t), ab,
                 len(co), statistics.mean(co) if co else 0, len(la), statistics.mean(la) if la else 0))


def cuenta(a, b, ms=5250.0, baile=0.12, semilla=1):
    # (baile: cada cuadro cuesta hasta un 12 % mas o menos, como en las trazas)
    r = random.Random(semilla)
    t, vence, n = 0.0, 15.0, 0
    while t < ms:
        if vence <= t:
            while vence <= t:
                vence += 15
            n += 1
            t += a * (1 + r.uniform(-baile, baile))
        else:
            t += b * (1 + r.uniform(-baile, baile))
    return n


def simula(args):
    v = [float(x.replace(",", ".")) for x in args] or [9.2, 21.2, 7.5, 20.5]
    pares = [(v[i], v[i + 1]) for i in range(0, len(v) - 1, 2)]
    print("sin baile, A=8: " + "  ".join("B=%d: %d" % (b, cuenta(8, b, baile=0)) for b in (18, 19, 20, 21, 22, 24)))
    print("maquina    " + "    ".join("A=%.1f B=%.1f" % p for p in pares) + "    (Tick en 5,25 s; media de 5 semillas)")
    for k in (0.8, 0.85, 0.9, 0.95, 1.0, 1.05, 1.1, 1.15, 1.2, 1.3, 1.4, 1.6, 1.8):
        print("x%.2f      " % k + "    ".join("%14d" % (sum(cuenta(a * k, b * k, semilla=s) for s in range(5)) // 5) for a, b in pares))


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "mide":
        mide(sys.argv[2:])
    elif len(sys.argv) > 1 and sys.argv[1] == "simula":
        simula(sys.argv[2:])
    else:
        print(__doc__ or "reloj_saturado.py mide TRAZA... | simula [A B [A2 B2]]")
