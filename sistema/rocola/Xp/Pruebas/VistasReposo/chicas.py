# chicas.py A B: las texturas subidas de menos de 64 (zonas del reloj, del recuadro de la franja, cintas) de dos corridas, por su huella:
# cuantas hay, cuantas distintas, las de A que no salen en B y al reves (con su nombre)
import collections, hashlib, os, re, sys
def lee(d):
    r = collections.defaultdict(list)
    for f in sorted(os.listdir(d)):
        m = re.match(r'(\d+)_([\d.]+)s_(\d+x\d+)_n\d+_en([\d,]+)_(\d+)x(\d+)\.png$', f)
        if not m: continue
        w, h = int(m.group(5)), int(m.group(6))
        if w >= 64 and h >= 64: continue
        hh = hashlib.md5(open(os.path.join(d, f), 'rb').read()).hexdigest()[:16]
        r[hh].append(m.group(2) + 's ' + m.group(3) + ' en' + m.group(4) + ' ' + m.group(5) + 'x' + m.group(6))
    return r
a, b = lee(sys.argv[1]), lee(sys.argv[2])
print('A: %d subidas, %d distintas | B: %d subidas, %d distintas | comunes distintas %d' % (sum(len(v) for v in a.values()), len(a), sum(len(v) for v in b.values()), len(b), len(set(a) & set(b))))
for h in sorted(set(a) - set(b), key=lambda h: a[h][0]): print('  solo A', h, a[h])
for h in sorted(set(b) - set(a), key=lambda h: b[h][0]): print('  solo B', h, b[h])
