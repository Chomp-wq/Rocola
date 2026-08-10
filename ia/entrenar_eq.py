#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Entrena el modelo del EQ AUTOMATICO de la rocola.

QUE APRENDE
-----------
El EQ automatico corrige cada cancion hacia un objetivo. Ese objetivo era UNO SOLO para toda la
biblioteca: el promedio. El problema es que una balada de piano y un corrido de banda no deberian
tener el mismo espectro, y empujarlos a los dos hacia la misma curva les quita caracter.

Esta red aprende otra cosa: mirando el CUERPO del espectro de una cancion -las bandas de 158 Hz a
5.6 kHz, que son las que definen de que tipo de musica se trata y ademas las que los codecs NO
destrozan- predice cuanto GRAVE (50 Hz) y cuanto BRILLO y AIRE (10 kHz y 15.8 kHz) tendria esa
cancion si estuviera bien grabada.

Se entrena SOLO con las canciones sanas de tu biblioteca (las que conservan sus agudos, que es lo
que el DSEE ya detecta y la rocola ya guarda). O sea: aprende de tus mejores grabaciones y aplica
esa leccion a las demas.

DE DONDE SALEN LOS DATOS
------------------------
De "rocola_eq.txt", que la rocola escribe sola mientras pre-analiza la biblioteca. Cada linea es
    b0;b1;b2;b3;b4;b5;b6;b7;corteHz|ruta
con las 8 bandas en dB (relativas a la mediana de su cuerpo) y el corte donde el codec talo los
agudos. No hay que cosechar nada aparte: ese archivo ya existe.

EL MODELO NO SE GUARDA SI NO SIRVE
----------------------------------
Antes de escribir nada se compara contra lo que habria pasado usando el promedio de siempre, sobre
canciones que la red NO vio al entrenar. Si no gana por un margen claro, NO se guarda el archivo y
la rocola sigue con el promedio. Es la unica forma honesta de saber si la red aporta algo o solo
esta memorizando.

USO
---
    python3 entrenar_eq.py --datos rocola_eq.txt --salida rocola_eq_ia.bin

Luego se copia rocola_eq_ia.bin junto al Rocola.exe. Si no esta, la rocola usa el promedio.
"""

import argparse, os, struct, sys
import numpy as np

# ---------------- contrato (identico a Form1.cs) ----------------
N_BANDAS   = 8
IDX_CORTE  = 8            # el 9o valor de cada linea
ENTRADAS   = [1, 2, 3, 4, 5]        # cuerpo: 158, 400, 1000, 2500, 5600 Hz
SALIDAS    = [0, 6, 7]              # 50 Hz, 10 kHz, 15.8 kHz
SANA_HZ    = 19300.0                # mismo umbral con el que la rocola da una cancion por completa

def leer_huellas(ruta):
    """Devuelve (curvas Nx8, cortes N). Acepta el formato viejo de 8 valores (corte desconocido)."""
    curvas, cortes = [], []
    malas = 0
    with open(ruta, "r", encoding="utf-8", errors="replace") as f:
        for linea in f:
            bar = linea.find("|")
            if bar <= 0:
                continue
            partes = linea[:bar].split(";")
            if len(partes) not in (N_BANDAS, N_BANDAS + 1):
                malas += 1; continue
            try:
                v = [float(x) for x in partes]
            except ValueError:
                malas += 1; continue
            curvas.append(v[:N_BANDAS])
            cortes.append(v[IDX_CORTE] if len(v) > N_BANDAS else 0.0)
    if malas:
        print("  (%d lineas ilegibles, saltadas)" % malas)
    return np.array(curvas, dtype=np.float64), np.array(cortes, dtype=np.float64)

class Mlp:
    def __init__(self, dims, rng):
        self.W = [rng.normal(0, np.sqrt(2.0 / dims[i]), (dims[i+1], dims[i])) for i in range(len(dims)-1)]
        self.B = [np.zeros(dims[i+1]) for i in range(len(dims)-1)]
    def forward(self, X):
        acts = [X]
        for L in range(len(self.W)):
            z = acts[-1] @ self.W[L].T + self.B[L]
            acts.append(np.tanh(z) if L < len(self.W)-1 else z)
        return acts
    def predict(self, X): return self.forward(X)[-1]

def entrenar(model, X, Y, Xv, Yv, epocas, lr=3e-3, lote=256, semilla=3, l2=1e-5):
    rng = np.random.default_rng(semilla)
    mW = [np.zeros_like(w) for w in model.W]; vW = [np.zeros_like(w) for w in model.W]
    mB = [np.zeros_like(b) for b in model.B]; vB = [np.zeros_like(b) for b in model.B]
    b1, b2, eps, paso = 0.9, 0.999, 1e-8, 0
    mejor, mejorW, mejorB = 1e30, None, None
    for ep in range(epocas):
        idx = rng.permutation(len(X))
        for s in range(0, len(X), lote):
            bi = idx[s:s+lote]
            acts = model.forward(X[bi])
            g = 2.0 * (acts[-1] - Y[bi]) / len(bi)
            paso += 1
            for L in reversed(range(len(model.W))):
                gW = g.T @ acts[L] + l2 * model.W[L]; gB = g.sum(axis=0)
                if L > 0: g = (g @ model.W[L]) * (1.0 - acts[L]**2)
                mW[L] = b1*mW[L] + (1-b1)*gW; vW[L] = b2*vW[L] + (1-b2)*gW**2
                mB[L] = b1*mB[L] + (1-b1)*gB; vB[L] = b2*vB[L] + (1-b2)*gB**2
                c1, c2 = 1-b1**paso, 1-b2**paso
                model.W[L] -= lr * (mW[L]/c1) / (np.sqrt(vW[L]/c2) + eps)
                model.B[L] -= lr * (mB[L]/c1) / (np.sqrt(vB[L]/c2) + eps)
        vl = float(((model.predict(Xv) - Yv)**2).mean())
        if vl < mejor:   # se guarda el MEJOR punto, no el ultimo: asi no se pasa de entrenamiento
            mejor = vl
            mejorW = [w.copy() for w in model.W]; mejorB = [b.copy() for b in model.B]
        if (ep+1) % 20 == 0 or ep == 0:
            tr = float(((model.predict(X) - Y)**2).mean())
            print("  epoca %3d   error entrenando %.4f   error validando %.4f" % (ep+1, tr, vl))
    model.W, model.B = mejorW, mejorB
    return model

def exportar(model, path):
    with open(path, "wb") as f:
        f.write(b"RIA1")
        f.write(struct.pack("<i", len(model.W)))
        for W, B in zip(model.W, model.B):
            f.write(struct.pack("<ii", W.shape[0], W.shape[1]))
            f.write(W.astype(np.float32).tobytes())   # fila-mayor (salidas, entradas): igual que la rocola
            f.write(B.astype(np.float32).tobytes())

def main():
    ap = argparse.ArgumentParser(description="Entrena el modelo del EQ automatico de la rocola.")
    ap.add_argument("--datos", default="rocola_eq.txt", help="archivo de huellas que escribe la rocola")
    ap.add_argument("--salida", default="rocola_eq_ia.bin", help="modelo a escribir")
    ap.add_argument("--epocas", type=int, default=200)
    ap.add_argument("--oculta", type=int, default=24, help="neuronas por capa oculta")
    ap.add_argument("--margen", type=float, default=5.0,
                    help="%% minimo que la red debe ganarle al promedio para que valga la pena guardarla")
    ap.add_argument("--forzar", action="store_true", help="guardar aunque no le gane al promedio (para probar)")
    a = ap.parse_args()

    if not os.path.exists(a.datos):
        print("No encuentro '%s'." % a.datos)
        print("Es el archivo que la rocola escribe sola al pre-analizar; copialo aqui desde la carpeta del Rocola.exe.")
        return 1

    print("Leyendo %s ..." % a.datos)
    curvas, cortes = leer_huellas(a.datos)
    if len(curvas) == 0:
        print("El archivo no tiene huellas legibles."); return 1
    sanas = cortes >= SANA_HZ
    print("  %d canciones en total, %d sanas (con sus agudos completos)" % (len(curvas), int(sanas.sum())))
    if sanas.sum() < 200:
        print("\nHacen falta al menos 200 canciones SANAS para entrenar algo de fiar; hay %d." % int(sanas.sum()))
        print("Deja correr el pre-analisis (o el ENTRENAMIENTO A FONDO) mas tiempo y vuelve a intentar.")
        print("Si tu biblioteca entera es de baja calidad, el EQ seguira con el promedio: tampoco pasa nada.")
        return 1

    D = curvas[sanas]
    X = D[:, ENTRADAS]
    Y = D[:, SALIDAS]

    # normalizar entradas (la red trabaja mejor con numeros centrados)
    mu, sd = X.mean(axis=0), X.std(axis=0) + 1e-6
    Xn = (X - mu) / sd

    rng = np.random.default_rng(7)
    idx = rng.permutation(len(Xn))
    corte = int(len(idx) * 0.8)
    tr, va = idx[:corte], idx[corte:]
    print("  entrenando con %d canciones, validando con %d (nunca vistas)" % (len(tr), len(va)))

    modelo = Mlp([len(ENTRADAS), a.oculta, a.oculta, len(SALIDAS)], rng)
    modelo = entrenar(modelo, Xn[tr], Y[tr], Xn[va], Y[va], a.epocas)

    # ---- la prueba que decide: ¿le gana al promedio de siempre? ----
    base = Y[tr].mean(axis=0)                       # lo que hace la rocola hoy: una sola curva para todos
    err_base = np.sqrt(((Y[va] - base) ** 2).mean(axis=0))
    err_red  = np.sqrt(((Y[va] - modelo.predict(Xn[va])) ** 2).mean(axis=0))
    nombres = ["grave 50 Hz", "brillo 10 kHz", "aire 15.8 kHz"]
    print("\n  ERROR AL ADIVINAR (dB, sobre canciones que la red nunca vio)\n")
    print("    banda            promedio    red      mejora")
    for i, n in enumerate(nombres):
        mej = 100.0 * (1.0 - err_red[i] / max(1e-9, err_base[i]))
        print("    %-14s   %6.2f   %6.2f    %+5.1f %%" % (n, err_base[i], err_red[i], mej))
    mejora = 100.0 * (1.0 - err_red.mean() / max(1e-9, err_base.mean()))
    print("\n    MEJORA GLOBAL: %+.1f %%" % mejora)

    if mejora < a.margen and not a.forzar:
        print("\n  La red NO le gana al promedio por un margen que valga la pena (hace falta %.0f%%)." % a.margen)
        print("  NO se guarda el modelo: la rocola seguira usando el promedio, que para tu biblioteca")
        print("  es igual de bueno y mas simple. Prueba con mas canciones analizadas, o con --oculta 48.")
        return 2

    # Los pesos de la primera capa absorben la normalizacion, asi la rocola no necesita saber de esto.
    modelo.W[0] = modelo.W[0] / sd
    modelo.B[0] = modelo.B[0] - modelo.W[0] @ mu
    exportar(modelo, a.salida)
    print("\n  Guardado '%s' (%.1f KB). Copialo junto al Rocola.exe." % (a.salida, os.path.getsize(a.salida)/1024.0))
    print("  La rocola lo toma sola al arrancar; si lo borras, vuelve al promedio.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
