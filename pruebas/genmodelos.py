#!/usr/bin/env python3
# Genera modelos RIA1 de varios tamaños con pesos al azar (solo para MEDIR lo que cuestan:
# el tiempo de una red no depende de sus numeros, solo de su forma).
import sys, struct
import numpy as np
rng = np.random.default_rng(7)
tamanos = {
    "lineal":  [28, 12],
    "chico":   [28, 16, 12],
    "actual":  [28, 48, 48, 12],
    "doble":   [28, 96, 96, 12],
    "cuadru":  [28, 192, 192, 12],
    "grande":  [28, 256, 256, 256, 12],
    "enorme":  [28, 384, 384, 384, 12],
    "tope":    [28, 512, 512, 512, 12],
}
for nombre, capas in tamanos.items():
    pesos = sum(capas[i]*capas[i+1] + capas[i+1] for i in range(len(capas)-1))
    with open("modelo_%s.bin" % nombre, "wb") as f:
        f.write(b"RIA1"); f.write(struct.pack("<i", len(capas)-1))
        for i in range(len(capas)-1):
            r, c = capas[i+1], capas[i]
            f.write(struct.pack("<ii", r, c))
            f.write((rng.normal(0, 0.1, (r, c))).astype(np.float32).tobytes())
            f.write((rng.normal(0, 0.1, r)).astype(np.float32).tobytes())
    print("%-8s %-26s %7d pesos  %6.0f KB" % (nombre, "->".join(map(str,capas)), pesos, (pesos*4)/1024))
