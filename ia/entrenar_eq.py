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
    b0;b1;b2;b3;b4;b5;b6;b7;corteHz;recorte;corteGrave;versionHuella|ruta
con las 8 bandas en dB (relativas a la mediana de su cuerpo) y despues los datos de calidad. Esa
ficha ha ido CRECIENDO con las versiones -al principio eran solo las 8 bandas, luego se le anadio
el corte de agudos, luego el recorte y el corte de abajo- y siempre anadiendo AL FINAL. Por eso
aqui la entrada CLI exige versionHuella=2 (medida original anterior al DSP). Legacy 0/1 solo
se permite como diagnostico separado no publicable; no se interpreta una version futura.

EL MODELO NO SE GUARDA SI NO SIRVE
----------------------------------
Se delega siempre a entrenar_eq_candidato.py: albumes/copia exacta separados, validacion para
checkpoint y prueba final contra promedio de entrenamiento y modelo instalado actual.
Solo exporta candidato aislado si cumple el guardian; nunca modifica el modelo instalado.

USO
---
    python3 entrenar_eq.py --datos rocola_eq.txt --instalado rocola_eq_ia.bin --destino eq_nuevo

PCM y escucha autorizada pendientes antes de activar. --salida solo orienta el directorio del
candidato; NO sobrescribe ese nombre. Sin modelo instalado legible no se certifica mejora.
"""

import argparse, os, struct, sys
from presupuesto_entrenamiento import configura_blas
HILOS_ENTRENAMIENTO = configura_blas()
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
    # La rocola anade al final lo que cambia (DiarioTablas.cs): una cancion medida otra vez sale repetida y manda la
    # ULTIMA linea, igual que al leerla la rocola. Sin esto, esa cancion contaria doble al entrenar.
    filas = {}
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
            filas[linea[bar + 1:].rstrip("\r\n").lower()] = (v[:N_BANDAS], v[IDX_CORTE] if len(v) > N_BANDAS else 0.0)
    if malas:
        print("  (%d lineas ilegibles, saltadas)" % malas)
    curvas = [c for c, _ in filas.values()]
    cortes = [k for _, k in filas.values()]
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

def normalizar_entrenamiento(x, tr):
    if len(tr) == 0:
        raise ValueError("No hay canciones para entrenamiento")
    mu, sd = x[tr].mean(axis=0), x[tr].std(axis=0) + 1e-6
    return (x - mu) / sd, mu, sd


def main():
    """Entrada publica: siempre candidato agrupado independiente, nunca exportacion directa."""
    import tempfile
    import entrenar_eq_candidato as Q
    ap = argparse.ArgumentParser(description="AutoEQ estricto: candidato aislado, huellas limpias v2")
    ap.add_argument("--datos", default="rocola_eq.txt")
    ap.add_argument("--instalado", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "rocola_eq_ia.bin"))
    ap.add_argument("--destino")
    ap.add_argument("--salida", default="rocola_eq_ia.bin", help="Nombre orientativo; no sustituye el archivo")
    ap.add_argument("--epocas", type=int, default=200)
    ap.add_argument("--diagnostico-legacy", action="store_true")
    args = ap.parse_args()
    destino = args.destino
    if not destino:
        destino = tempfile.mkdtemp(prefix="eq-candidato-", dir=os.path.dirname(os.path.abspath(args.salida)))
        os.rmdir(destino)
    try:
        informe = Q.entrenar(args.datos, args.instalado, destino, args.epocas, args.diagnostico_legacy)
    except (OSError, ValueError) as e:
        print("AutoEQ no verificable; instalado intacto:", e)
        return 3
    print("Candidato aislado en", destino, "(requiere PCM/escucha antes de activar)")
    return 0 if informe["aceptado_numericamente"] else 2



if __name__ == "__main__":
    sys.exit(main())
