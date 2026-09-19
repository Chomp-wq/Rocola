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
    b0;b1;b2;b3;b4;b5;b6;b7;corteHz;recorte;corteGrave|ruta
con las 8 bandas en dB (relativas a la mediana de su cuerpo) y despues los datos de calidad. Esa
ficha ha ido CRECIENDO con las versiones -al principio eran solo las 8 bandas, luego se le anadio
el corte de agudos, luego el recorte y el corte de abajo- y siempre anadiendo AL FINAL. Por eso
aqui se leen los 8 primeros y, si esta, el 9o; lo que venga detras se ignora sin quejarse, y un
archivo de cualquier version de la rocola se puede leer. No hay que cosechar nada aparte.

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
    """Devuelve (curvas Nx8, cortes N). Vale para el archivo de cualquier version de la rocola: se piden
    los 8 primeros numeros y se coge el 9o si esta (corte 0 = esa version no lo apuntaba). Lo que venga
    detras -recorte, corte de graves, o lo que se anada manana- se ignora sin dar la linea por mala."""
    curvas, cortes = [], []
    malas = 0
    with open(ruta, "r", encoding="utf-8", errors="replace") as f:
        for linea in f:
            bar = linea.find("|")
            if bar <= 0:
                continue
            partes = linea[:bar].split(";")
            if len(partes) < N_BANDAS:
                malas += 1; continue
            try:
                v = [float(x) for x in partes[:IDX_CORTE + 1]]
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

def error_medio(model, X, Y, lote):
    """El error cuadratico medio, SUMADO POR LOTES.

    Antes esto era ((model.predict(X) - Y)**2).mean(), y se hacia con el conjunto ENTERO de una vez:
    en cada epoca para el de validacion, y cada veinte para el de entrenamiento. Predecir de una arma
    la pila de activaciones de TODAS las canciones a la vez -entradas, capa oculta y salida-, y con una
    biblioteca grande eso es el pico de memoria de todo el entrenamiento, pagado una y otra vez solo
    para imprimir un numero.

    Se suma lote a lote, del mismo tamano con el que ya se entrena. El resultado NO es identico al
    ultimo bit -sumar en trozos no da exactamente lo mismo que sumar de una-, pero la diferencia es del
    orden del redondeo y no cambia cual es la mejor epoca. Lo comprueba ia/test_entrenar_eq.py.
    """
    if len(X) == 0:
        return 0.0
    suma = 0.0
    for i in range(0, len(X), lote):
        d = model.predict(X[i:i + lote]) - Y[i:i + lote]
        suma += float((d * d).sum())
    return suma / (len(X) * Y.shape[1])


def entrenar(model, X, Y, Xv, Yv, epocas, lr=3e-3, lote=256, semilla=3, l2=1e-5):
    if epocas <= 0 or lote <= 0 or len(X) == 0 or len(Xv) == 0:
        raise ValueError("Se requieren epocas/lote positivos y ejemplos de entrenamiento y validacion")
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
            c1, c2 = 1-b1**paso, 1-b2**paso
            for L in reversed(range(len(model.W))):
                gW = g.T @ acts[L] + l2 * model.W[L]; gB = g.sum(axis=0)
                if L > 0: g = (g @ model.W[L]) * (1.0 - acts[L]**2)
                mW[L] = b1*mW[L] + (1-b1)*gW; vW[L] = b2*vW[L] + (1-b2)*gW**2
                mB[L] = b1*mB[L] + (1-b1)*gB; vB[L] = b2*vB[L] + (1-b2)*gB**2
                model.W[L] -= lr * (mW[L]/c1) / (np.sqrt(vW[L]/c2) + eps)
                model.B[L] -= lr * (mB[L]/c1) / (np.sqrt(vB[L]/c2) + eps)
        vl = error_medio(model, Xv, Yv, lote)
        if vl < mejor:   # se guarda el MEJOR punto, no el ultimo: asi no se pasa de entrenamiento
            mejor = vl
            mejorW = [w.copy() for w in model.W]; mejorB = [b.copy() for b in model.B]
        if (ep+1) % 20 == 0 or ep == 0:
            tr = error_medio(model, X, Y, lote)
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
    medido = cortes > 0                      # canciones cuyo corte de agudos SI se sabe
    print("  %d canciones en total, %d sanas (con sus agudos completos)" % (len(curvas), int(sanas.sum())))

    # DE QUE CANCIONES SE APRENDE. Lo ideal es aprender solo de las sanas: en las danadas los agudos ya
    # no existen y ensenarian a la red el dano del codec en vez de como suena una grabacion buena. Pero
    # si no hay bastantes, plantarse seria tirar meses de pre-analisis: se entrena con TODAS, que es
    # exactamente lo que la rocola hace con su promedio cuando no tiene bastantes sanas medidas. Y quien
    # decide de verdad si el modelo vale es la prueba del final, no esto.
    if sanas.sum() >= 200:
        usa = sanas
        print("  se aprende SOLO de las sanas: la red vera como suena lo que esta bien grabado")
    else:
        usa = np.ones(len(curvas), dtype=bool)
        print()
        if medido.sum() == 0:
            print("  ESTE ARCHIVO NO TRAE EL DATO DEL CORTE DE AGUDOS.")
            print("  Lo escribio una VERSION VIEJA de la rocola, que todavia no lo apuntaba, asi que no hay")
            print("  forma de saber cuales de tus canciones conservan sus agudos. Copia otra vez rocola_eq.txt")
            print("  de junto a Rocola.exe -el de ahora si lo trae- y vuelve a entrenar: el modelo saldra")
            print("  mejor, porque aprendera solo de tus grabaciones completas.")
        else:
            print("  Solo hay %d canciones sanas de %d (para aprender solo de ellas harian falta 200)."
                  % (int(sanas.sum()), len(curvas)))
            c = cortes[medido]
            print("  El corte de agudos mas alto que hay es %.1f kHz y la mitad de la biblioteca esta por"
                  % (c.max() / 1000.0))
            print("  debajo de %.1f kHz: es una biblioteca de copias comprimidas." % (np.median(c) / 1000.0))
        print()
        print("  Se entrena con TODAS las canciones, igual que hace la rocola con su promedio. Si aun asi")
        print("  la red no le gana al promedio, no se guarda nada y la rocola sigue como esta.")

    D = curvas[usa]
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
    # El promedio contra el que hay que ganar es EL QUE USA LA ROCOLA, no uno comodo: la rocola saca su
    # objetivo de las canciones sanas si tiene al menos diez medidas, y si no, de todas. Compararse
    # contra el de todas teniendo sanas seria hacerse trampas al solitario.
    if sanas.sum() >= 10:
        base = curvas[sanas][:, SALIDAS].mean(axis=0)
        de_donde = "el promedio de tus %d canciones sanas" % int(sanas.sum())
    else:
        base = Y[tr].mean(axis=0)
        de_donde = "el promedio de toda tu biblioteca"
    print("\n  Se compara contra %s, que es lo que la rocola usa hoy." % de_donde)
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
