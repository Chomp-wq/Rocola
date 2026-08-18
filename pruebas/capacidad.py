#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# ============================================================================================
#  ¿VALE LA PENA UN MODELO MAS GRANDE PARA EL RECONSTRUCTOR DE AGUDOS?
# --------------------------------------------------------------------------------------------
#  Se entrenan varios tamaños de red SOBRE LOS MISMOS DATOS, con el mismo entrenamiento y la
#  misma division entrenar/validar, y se compara el error de validacion.
#
#  Lo que hay que mirar no es solo si el error baja, sino la DISTANCIA entre el error de
#  entrenamiento y el de validacion:
#     - si los dos bajan juntos           -> al modelo le faltaba tamaño: crecer AYUDA
#     - si baja el de entrenar y el de    -> el modelo ya se sabe los datos de memoria; lo que
#       validar se queda o empeora           falta no es tamaño, son datos o mejores entradas
# ============================================================================================
import sys, os, time
import numpy as np

sys.path.insert(0, "/home/user/Rocola/ia")
import entrenar_ia as T

FS = T.FS

def main():
    rng = np.random.default_rng(20260715)
    nclips = int(sys.argv[sys.argv.index("--clips") + 1]) if "--clips" in sys.argv else 320
    print("Generando %d trozos de audio sintetico variado..." % nclips)
    clips = [T.synth_clip(rng) for _ in range(nclips)]
    print("Extrayendo pares (forma espectral -> envolvente real de los agudos)...")
    X, Y = T.build_dataset(clips, rng)
    print("  %d ejemplos de %d entradas -> %d salidas" % (X.shape[0], X.shape[1], Y.shape[1]))

    idx = rng.permutation(len(X)); X, Y = X[idx], Y[idx]
    nv = max(500, len(X) // 10)
    Xv, Yv, Xt, Yt = X[:nv], Y[:nv], X[nv:], Y[nv:]
    print("  %d para entrenar, %d para validar\n" % (len(Xt), len(Xv)))

    # linea base: la curva FIJA de -9 dB/octava (lo que hace la rocola SIN modelo)
    fc = Xv[:, 25] * 20000.0
    top = np.minimum(2.0 * fc, np.minimum(20000.0, np.full_like(fc, FS / 2 * 0.95)))
    ybase = np.zeros_like(Yv)
    for j in range(12):
        fmid = fc * (top / fc) ** ((j + 0.5) / 12.0)
        ybase[:, j] = np.clip(-1.8 * np.log2(fmid / fc), -4.0, 0.5)
    mse_base = float(((ybase - Yv) ** 2).mean())

    tamanos = [
        [28, 12],                 # sin capa oculta: una regresion lineal
        [28, 16, 12],
        [28, 48, 48, 12],         # EL DE AHORA
        [28, 96, 96, 12],
        [28, 192, 192, 12],
        [28, 256, 256, 256, 12],
        [28, 384, 384, 384, 12],
    ]

    print("  %-24s %9s %10s %10s %9s %8s" % ("modelo", "pesos", "mse-entr", "mse-valid", "vs curva", "seg"))
    print("  " + "-" * 76)
    print("  %-24s %9s %10s %10.4f %8.1fx" % ("curva fija (sin modelo)", "-", "-", mse_base, 1.0))
    filas = []
    for capas in tamanos:
        pesos = sum(capas[i] * capas[i + 1] + capas[i + 1] for i in range(len(capas) - 1))
        r2 = np.random.default_rng(20260715)
        t0 = time.time()
        m = T.Mlp(capas, r2)
        # mismo entrenamiento para todos; se silencia el detalle por epoca
        _stdout = sys.stdout
        sys.stdout = open(os.devnull, "w")
        try:
            m = T.train(m, Xt, Yt, Xv, Yv)
        finally:
            sys.stdout.close(); sys.stdout = _stdout
        seg = time.time() - t0
        mtr = float(((m.predict(Xt) - Yt) ** 2).mean())
        mvl = float(((m.predict(Xv) - Yv) ** 2).mean())
        nombre = "->".join(str(c) for c in capas)
        marca = "   <- el de ahora" if capas == [28, 48, 48, 12] else ""
        print("  %-24s %9d %10.4f %10.4f %8.1fx %8.0f%s" % (nombre, pesos, mtr, mvl, mse_base / max(mvl, 1e-9), seg, marca))
        filas.append((nombre, pesos, mtr, mvl))

    print("\n  LECTURA:")
    base = [f for f in filas if f[0] == "28->48->48->12"][0]
    mejor = min(filas, key=lambda f: f[3])
    print("    el de ahora        : mse-valid %.4f" % base[3])
    print("    el mejor probado   : %s, mse-valid %.4f  (%.1f%% mejor)"
          % (mejor[0], mejor[3], 100.0 * (base[3] - mejor[3]) / base[3]))
    print("    hueco entrenar/validar del mayor: %.4f -> %.4f" % (filas[-1][2], filas[-1][3]))
    return 0

if __name__ == "__main__":
    sys.exit(main())
