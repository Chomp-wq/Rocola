# Compara las huellas de dos corridas de los recorridos (huellas.py): las texturas que se subieron (como multiconjunto de huellas: cuantas de
# A salen identicas en B y al reves) y las capas de Dibujo2D de cada captura (por su nombre). Uso: compara.py DIR_A DIR_B [DIR_A2]
# Con DIR_A2 (otra corrida del MISMO programa que A: el ruido de la hora y del reloj de la musica) cuenta ademas lo QUIETO, lo que sale
# igual en A y en A2: cuantas de esas texturas y capas salen identicas en B (tienen que ser todas), y las de B que no salen ni en A ni en A2.
import collections, os, sys
a, b = sys.argv[1], sys.argv[2]
a2 = sys.argv[3] if len(sys.argv) > 3 else None
def lee(d):
    r = collections.defaultdict(list)
    p = os.path.join(d, 'huellas.tsv')
    if not os.path.isfile(p): return r
    for l in open(p):
        c = l.rstrip('\n').split('\t')
        if len(c) >= 3: r[c[0]].append((c[1], c[2]))
    return r
tot = collections.Counter()
for rec in sorted(set(os.listdir(a)) & set(os.listdir(b))):
    if not os.path.isdir(os.path.join(a, rec)): continue
    A, B = lee(os.path.join(a, rec)), lee(os.path.join(b, rec))
    ta = collections.Counter(h for _, h in A['textura']); tb = collections.Counter(h for _, h in B['textura'])
    comunes = sum((ta & tb).values())
    da = dict(A['dibujo2d']); db = dict(B['dibujo2d'])
    iguales = sum(1 for k in da if k in db and da[k] == db[k]); distintas = [k for k in da if k in db and da[k] != db[k]]
    print('%-10s texturas: A %d, B %d, identicas %d (solo A %d, solo B %d) | capas de Dibujo2D: %d iguales, %d distintas, %d sin pareja%s' % (
        rec, sum(ta.values()), sum(tb.values()), comunes, sum((ta - tb).values()), sum((tb - ta).values()), iguales, len(distintas),
        len(set(da) ^ set(db)), (' -> ' + ', '.join(distintas[:4])) if distintas else ''))
    tot['ta'] += sum(ta.values()); tot['tb'] += sum(tb.values()); tot['com'] += comunes; tot['ig'] += iguales; tot['dis'] += len(distintas)
    if a2 and os.path.isdir(os.path.join(a2, rec)):
        A2 = lee(os.path.join(a2, rec)); ta2 = collections.Counter(h for _, h in A2['textura']); da2 = dict(A2['dibujo2d'])
        quietas = ta & ta2; enB = quietas & tb
        nom = dict((h, n) for n, h in A['textura'] + A2['textura'] + B['textura'])
        faltan = quietas - tb; nuevas = tb - ta - ta2
        cq = [k for k in da if k in da2 and da[k] == da2[k]]; cqB = [k for k in cq if db.get(k) == da[k]]
        print('           QUIETO: texturas %d, en B %d%s | capas %d, en B %d%s | de B ni en A ni en A2: %d%s' % (
            sum(quietas.values()), sum(enB.values()), (' FALTAN ' + ', '.join(nom.get(h, h) for h in list(faltan)[:4])) if faltan else '',
            len(cq), len(cqB), (' DISTINTAS ' + ', '.join(k for k in cq if k not in cqB)[:300]) if len(cqB) < len(cq) else '',
            sum(nuevas.values()), (' (' + ', '.join(nom.get(h, h) for h in list(nuevas)[:6]) + ')') if nuevas else ''))
        tot['q'] += sum(quietas.values()); tot['qb'] += sum(enB.values()); tot['cq'] += len(cq); tot['cqb'] += len(cqB); tot['nu'] += sum(nuevas.values())
print('TOTAL texturas: A %d, B %d, identicas %d | capas de Dibujo2D: %d iguales, %d distintas' % (tot['ta'], tot['tb'], tot['com'], tot['ig'], tot['dis']))
if a2: print('TOTAL QUIETO: texturas %d, en B %d | capas %d, en B %d | texturas de B ni en A ni en A2: %d' % (tot['q'], tot['qb'], tot['cq'], tot['cqb'], tot['nu']))
