#!/usr/bin/env python3
# compara.py DIR_REF DIR_MIO : los rocola_*.txt (sin sellos de hora) y las capturas (pixeles distintos y el rectangulo que los contiene).
import sys, os, re, glob
from PIL import Image, ImageChops
a, b = sys.argv[1], sys.argv[2]
def limpio(p):
    try: t = open(p, encoding='utf-8', errors='replace').read()
    except FileNotFoundError: return None
    t = re.sub(r'^ts=\d+$', 'ts=*', t, flags=re.M)
    # lo que depende de la hora de los archivos copiados (cambia en cada corrida, tambien entre dos de la referencia)
    t = re.sub(r'\b6\d{17}\b', 'HORA', t)
    t = re.sub(r'^sig=\S+$', 'sig=*', t, flags=re.M)
    t = re.sub(r'^n=-?\d+$', 'n=*', t, flags=re.M)
    t = re.sub(r'^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d', 'FECHA', t, flags=re.M)
    return t
for f in sorted(set(os.path.basename(x) for x in glob.glob(a + '/rocola_*.txt') + glob.glob(b + '/rocola_*.txt'))):
    x, y = limpio(os.path.join(a, f)), limpio(os.path.join(b, f))
    print(('IGUAL   ' if x == y else 'DISTINTO') + ' ' + f)
for f in sorted(set(os.path.basename(x) for x in glob.glob(a + '/*.png'))):
    pa, pb = os.path.join(a, f), os.path.join(b, f)
    if not os.path.exists(pb): print('FALTA   ', f); continue
    ia, ib = Image.open(pa).convert('RGBA'), Image.open(pb).convert('RGBA')
    if ia.size != ib.size: print('MEDIDA  ', f, ia.size, ib.size); continue
    d = ImageChops.difference(ia, ib)
    caja = d.getbbox()
    n = sum(1 for p in d.get_flattened_data() if p != (0, 0, 0, 0)) if hasattr(d, 'get_flattened_data') else sum(1 for p in d.getdata() if p != (0, 0, 0, 0))
    print(('IGUAL   ' if n == 0 else 'DISTINTO') + ' %s: %d pixeles distintos%s' % (f, n, (' en ' + str(caja)) if caja else ''))
for f in ['video.wav']:
    pa, pb = os.path.join(a, f), os.path.join(b, f)
    if os.path.exists(pa) and os.path.exists(pb):
        print(('IGUAL   ' if open(pa,'rb').read() == open(pb,'rb').read() else 'DISTINTO') + ' ' + f + ' (%d y %d bytes)' % (os.path.getsize(pa), os.path.getsize(pb)))
