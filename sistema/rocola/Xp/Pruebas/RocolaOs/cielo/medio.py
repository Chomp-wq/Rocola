#!/usr/bin/env python3
# El cielo directo y el de la imagen intermedia (cielo a media resolucion) dan el mismo degradado: por cada fila del cielo (x 300 a 740, y 120 a
# 640, sin la interfaz), la mediana de cada canal; lo mas que se separan las dos capturas (en niveles de 0 a 255). Las nebulosas y las estrellas
# no van igual (el reloj y la resolucion), por eso la mediana y una holgura de 6 niveles.
#   medio.py DIRECTO.png MEDIO.png
import sys
import numpy as np
from PIL import Image
a = np.asarray(Image.open(sys.argv[1]).convert('RGB'), np.float64)
b = np.asarray(Image.open(sys.argv[2]).convert('RGB'), np.float64)
ra = np.median(a[120:640, 300:740], axis=1); rb = np.median(b[120:640, 300:740], axis=1)
d = np.abs(ra - rb); mx = d.max(); y = int(np.unravel_index(d.argmax(), d.shape)[0]) + 120
print(f"lo mas que se separan las medianas por fila: {mx:.1f} niveles (fila {y}); media {d.mean():.2f}")
sys.exit(0 if mx <= 6 else 1)
