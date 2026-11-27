#!/usr/bin/env python3
# montaje.py CAPTURAS SALIDA [calidad]: de las capturas de prueba.sh arma las hojas que se guardan en el repositorio (JPEG, no los 100 PNG de
# 700 KB): una por escena (pared, escaparate, reposo; de noche y de dia), cada fecha del calendario en una miniatura de 512x384 con su nombre,
# y una hoja por cosa suelta (los fuegos, la cuenta, el cumpleanos, la fugaz). Necesita pillow.
import os, sys, glob
from PIL import Image, ImageDraw

cap, sal = sys.argv[1], sys.argv[2]
q = int(sys.argv[3]) if len(sys.argv) > 3 else 82
os.makedirs(sal, exist_ok=True)
ORDEN = ['anio_nuevo', 'reyes', 'candelaria', 'amor', 'bandera', 'mujer', 'nino', 'madres', 'padre', 'independencia', 'muertos', 'revolucion',
         'santa_cecilia', 'guadalupana', 'posadas', 'navidad']

def hoja(nombre, archivos, columnas=4, ancho=512, alto=384):
    archivos = [(t, a) for (t, a) in archivos if os.path.exists(a)]
    if not archivos: return
    filas = (len(archivos) + columnas - 1) // columnas
    h = Image.new('RGB', (columnas * ancho, filas * alto), (0, 0, 0))
    d = ImageDraw.Draw(h)
    for i, (t, a) in enumerate(archivos):
        im = Image.open(a).convert('RGB').resize((ancho, alto), Image.LANCZOS)
        x, y = (i % columnas) * ancho, (i // columnas) * alto
        h.paste(im, (x, y))
        d.rectangle([x, y + alto - 18, x + 8 * len(t) + 10, y + alto], fill=(0, 0, 0))
        d.text((x + 5, y + alto - 15), t, fill=(255, 255, 255))
    h.save(os.path.join(sal, nombre + '.jpg'), quality=q, optimize=True)
    print(nombre, len(archivos), 'miniaturas')

for escena in ('pared', 'escaparate', 'reposo'):
    for hora in ('noche', 'dia'):
        hoja('%s-%s' % (escena, hora), [(k, '%s/%s-%s-%s.png' % (cap, escena, k, hora)) for k in ORDEN])
hoja('fuegos', [(os.path.basename(a)[:-4], a) for a in sorted(glob.glob(cap + '/fuegos-*.png'))], 3)
hoja('cumple', [(os.path.basename(a)[:-4], a) for a in sorted(glob.glob(cap + '/cumple-*.png') + glob.glob(cap + '/propia-*.png'))], 3)
hoja('fugaz', [(os.path.basename(a)[:-4], a) for a in sorted(glob.glob(cap + '/fugaz-*.png'))], 4)
