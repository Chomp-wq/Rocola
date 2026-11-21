#!/usr/bin/env python3
# LA REGLA 3 EN UNA CAPTURA: la luminancia relativa (sRGB) del cielo hasta 0,848 del alto (o hasta --hasta), donde no hay interfaz. Por cada
# fila, el percentil 60 de las columnas que quedan (sin las cajas dadas, o solo dentro de --solo): asi no cuentan las estrellas sueltas ni
# los textos finos. Da lo mas claro de esas filas, en que fila, y el contraste del texto blanco con eso ((1+0,05)/(L+0,05)).
#   contraste.py CAPTURA.png [--hasta 0.848] [--solo x0,y0,x1,y1] [x0,y0,x1,y1 ...]   (cajas a quitar, en pixeles)
import sys
import numpy as np
from PIL import Image
a = sys.argv[1:]; ruta = a.pop(0); hasta = 0.848; solo = None; quitar = []
while a:
    x = a.pop(0)
    if x == '--hasta': hasta = float(a.pop(0))
    elif x == '--solo': solo = tuple(map(int, a.pop(0).split(',')))
    else: quitar.append(tuple(map(int, x.split(','))))
im = np.asarray(Image.open(ruta).convert('RGB'), np.float64) / 255.0
H, W, _ = im.shape
lin = np.where(im <= 0.04045, im / 12.92, ((im + 0.055) / 1.055) ** 2.4)
L = 0.2126 * lin[..., 0] + 0.7152 * lin[..., 1] + 0.0722 * lin[..., 2]
mask = np.zeros((H, W), bool) if solo else np.ones((H, W), bool)
if solo: mask[solo[1]:solo[3], solo[0]:solo[2]] = True
for (x0, y0, x1, y1) in quitar: mask[max(0, y0):min(H, y1), max(0, x0):min(W, x1)] = False
filas = []
for y in range(0, int(hasta * H)):
    v = L[y][mask[y]]
    if v.size < 20 or (not solo and v.size < W * 0.25): continue
    filas.append((np.percentile(v, 60), y))
if not filas: print('sin cielo que medir'); sys.exit(2)
mx, ymx = max(filas)
print(f"cielo hasta {hasta:.3f} del alto{' (solo ' + ','.join(map(str, solo)) + ')' if solo else ''}: luz maxima {mx:.3f} (fila {ymx}); contraste del blanco {1.05/(mx+0.05):.1f}:1; regla 3 (<= 0,17): {'BIEN' if mx <= 0.17 else 'MAL'}")
