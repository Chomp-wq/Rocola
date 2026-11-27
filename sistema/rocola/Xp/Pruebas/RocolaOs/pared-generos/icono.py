#!/usr/bin/env python3
# La casilla de la letra ♫ de la barra de letras (1024x768, escena «letra»): lleva el icono de Spotify (el circulo verde #1DB954 con tres ondas negras),
# no el texto. Mide en cada captura, en la franja de la barra de la derecha (donde va la ultima letra): el circulo verde (alto y ancho de 15 puntos o mas,
# unos 300 de area) y las ondas negras dentro (de 25 a 250 puntos oscuros, en tres tiras). Con -v guarda un recorte ampliado junto a la captura.
#   icono.py CAPTURA.png [CAPTURA2.png ...]
#   icono.py --ausente CAPTURA.png [...]   (27-11-2026: el dueno quito la letra ♫, RocolaOsSpotifyLetraEnLaPared = false) lo contrario: en esa
#                                          franja NO hay circulo verde de Spotify (ni 30 puntos de su verde), sin elegir y elegida
import sys
from PIL import Image

mal = 0
ausente = "--ausente" in sys.argv[1:]
for ruta in [x for x in sys.argv[1:] if x != "--ausente"]:
    a = Image.open(ruta).convert("RGB")
    if a.size != (1024, 768):
        print("MAL: %s no es de 1024x768 (%s)" % (ruta, a.size))
        mal += 1
        continue
    X0, Y0, X1, Y1 = 860, 52, 1000, 96            # la ultima casilla de la barra (la ♫ es la ultima: la barra llega hasta x=990)
    verde = []
    for y in range(Y0, Y1):
        for x in range(X0, X1):
            r, g, b = a.getpixel((x, y))
            if abs(r - 0x1D) <= 40 and abs(g - 0xB9) <= 40 and abs(b - 0x54) <= 50 and g > r + 60:
                verde.append((x, y))
    if ausente:
        print("%s: %d puntos del verde de Spotify en la franja de la ultima letra (debe haber menos de 30)" % (ruta.rsplit("/", 1)[-1], len(verde)))
        if len(verde) >= 30:
            print("MAL: sale el icono de Spotify al final del abecedario y ya no debe salir")
            mal += 1
        continue
    if not verde:
        print("MAL: %s: ningun punto del verde de Spotify en la casilla" % ruta)
        mal += 1
        continue
    xs, ys = [p[0] for p in verde], [p[1] for p in verde]
    an, al = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
    oscuros = 0
    for y in range(min(ys), max(ys) + 1):
        for x in range(min(xs), max(xs) + 1):
            r, g, b = a.getpixel((x, y))
            if r < 60 and g < 90 and b < 80:
                oscuros += 1
    # tres ondas = tres bajadas del verde al negro a lo largo de la columna del centro (y las dos de al lado)
    cx = (min(xs) + max(xs)) // 2
    tiras = 0
    for dx in (-2, 0, 2):
        n, previa = 0, False
        for y in range(min(ys), max(ys) + 1):
            r, g, b = a.getpixel((cx + dx, y))
            d = g < 120 and r < 60          # (el verde mide G=185; la onda, G de 0 a 100: la mezcla del borde cuenta como onda)
            if d and not previa:
                n += 1
            previa = d
        tiras = max(tiras, n)
    print("%s: icono de %dx%d puntos (%d verdes), %d oscuros, %d tiras de ondas, en x %d-%d y %d-%d" % (
        ruta.rsplit("/", 1)[-1], an, al, len(verde), oscuros, tiras, min(xs), max(xs), min(ys), max(ys)))
    if an < 15 or al < 15:
        print("MAL: el icono mide menos de 15 puntos")
        mal += 1
    if an > 27 or al > 27:
        print("MAL: el icono mide mas de 27 puntos: se sale de su casilla o de la capsula de la letra elegida")
        mal += 1
    if len(verde) < 180:
        print("MAL: poco verde (%d): no se lee" % len(verde))
        mal += 1
    if not (25 <= oscuros <= 250) or tiras != 3:
        print("MAL: las ondas negras no se ven bien (%d puntos oscuros en %d tiras: deben ser 3 tiras)" % (oscuros, tiras))
        mal += 1
sys.exit(1 if mal else 0)
