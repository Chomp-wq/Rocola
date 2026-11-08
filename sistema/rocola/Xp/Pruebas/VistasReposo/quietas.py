# quietas.py A A2 B [B2...]: las texturas QUIETAS (las que salen en las dos corridas de la referencia, A y A2) como CONJUNTO (sin contar
# cuantas veces se subieron): cuantas distintas hay y cuales no salen en B (ni una vez); y las de B que no salen ni en A ni en A2.
# (Copia de la del grupo «suena», Pruebas/VistasSuena/quietas.py, con solo esta nota de mas; al juntar las ramas sobra una de las dos.)
import collections, os, sys
a, a2 = sys.argv[1], sys.argv[2]; bs = sys.argv[3:]
def lee(d):
    r = []
    p = os.path.join(d, 'huellas.tsv')
    if not os.path.isfile(p): return r
    for l in open(p):
        c = l.rstrip('\n').split('\t')
        if len(c) >= 3 and c[0] == 'textura': r.append((c[1], c[2], c[3] if len(c) > 3 else ''))
    return r
tq = tf = 0
for rec in sorted(os.listdir(a)):
    if not os.path.isdir(os.path.join(a, rec)) or not all(os.path.isdir(os.path.join(b, rec)) for b in bs): continue
    A, A2 = lee(os.path.join(a, rec)), lee(os.path.join(a2, rec))
    ha = set(h for _, h, _ in A); ha2 = set(h for _, h, _ in A2); q = ha & ha2
    nom = {}
    for n, h, t in A + A2: nom.setdefault(h, []).append(n + '@' + t)
    hb = set()
    for b in bs: hb |= set(h for _, h, _ in lee(os.path.join(b, rec)))
    falta = sorted(q - hb); nuevas = hb - ha - ha2
    tq += len(q); tf += len(falta)
    print('%-10s quietas distintas %d, en B %d%s | de B ni en A ni en A2: %d distintas' % (rec, len(q), len(q) - len(falta),
          (' FALTAN ' + '; '.join(h + ' ' + ','.join(sorted(set(nom[h]))[:3]) for h in falta)) if falta else '', len(nuevas)))
print('TOTAL quietas distintas %d, en B %d' % (tq, tq - tf))
