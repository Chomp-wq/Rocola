#!/usr/bin/env python3
# Comprobaciones por pixeles de la escena «cargando» de prueba.sh (el anillo de carga del buscador, 23-11-2026; 1024x768).
# uso: anillo.py ESPERA1 ESPERA2 ESPERA3 RESULTADOS   (las capturas); escribe «clave=ok|mal» por comprobacion
import sys
from PIL import Image
w1, w2, w3, res = [Image.open(a).convert('RGB') for a in sys.argv[1:5]]
def lum(p): return (p[0] * 299 + p[1] * 587 + p[2] * 114) // 1000
def caja(im, x, y, w, h):
    c = im.crop((x, y, x + w, y + h))
    return list(c.get_flattened_data() if hasattr(c, 'get_flattened_data') else c.getdata())
def difieren(a, b, x, y, w, h, u):
    return sum(1 for p, q in zip(caja(a, x, y, w, h), caja(b, x, y, w, h)) if max(abs(p[i] - q[i]) for i in range(3)) > u)
def raros(im, x, y, w, h):   # puntos que se apartan de la luminancia mediana de la caja (el anillo sobre el cielo)
    l = sorted(lum(p) for p in caja(im, x, y, w, h)); m = l[len(l) // 2]
    return sum(1 for p in caja(im, x, y, w, h) if abs(lum(p) - m) > 14)
RX, RY, RW, RH = 617, 160, 68, 68   # donde va el anillo (en la tarjeta de carga, centro ~ 651,194)
r1 = raros(w1, RX, RY, RW, RH); r0 = raros(w1, 800, 330, RW, RH)   # y un trozo de cielo vacio igual
print('ring1=%s (%d puntos contra %d del cielo)' % ('ok' if r1 > 3 * r0 + 40 else 'mal', r1, r0))
d = difieren(w1, w2, RX, RY, RW, RH, 25)
print('gira=%s (%d puntos distintos en el anillo entre dos capturas)' % ('ok' if d >= 25 else 'mal', d))
def oscuros(im): return sum(1 for p in caja(im, 525, 250, 475, 370) if lum(p) < 45)
o1, o2, o3, orr = oscuros(w1), oscuros(w2), oscuros(w3), oscuros(res)
print('tarjetas=%s (puntos oscuros de las tarjetas: espera %d %d %d, resultados %d)' % ('ok' if max(o1, o2, o3) * 6 < orr and orr > 20000 else 'mal', o1, o2, o3, orr))
claros = max(sum(1 for p in caja(im, 525, 154, 80, 26) if lum(p) > 150) for im in (w1, w2, w3))
print('aviso=%s (%d puntos claros en el renglon del aviso de XP)' % ('ok' if claros < 40 else 'mal', claros))
c = difieren(w1, res, 860, 36, 130, 26, 40)
print('casilla=%s (%d puntos distintos en la casilla de la cuenta)' % ('ok' if c > 30 else 'mal', c))
