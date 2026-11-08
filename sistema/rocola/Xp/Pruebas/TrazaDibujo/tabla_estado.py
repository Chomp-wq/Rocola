# LA TABLA DE ESTADO POR LINEA: junta las trazas de los recorridos (traza.tsv de cada uno, de la copia instrumentada de la referencia) y
# saca, por cada archivo:linea de XP que dibuja, las operaciones, las llamadas, en que recorridos salio, los estados del Graphics que se
# vieron (con sus veces), que parte del estado cambia entre llamadas y unos ejemplos de argumentos.
import sys, os, collections
raiz, salida = sys.argv[1], sys.argv[2]
filas = collections.OrderedDict()
for rec in sorted(os.listdir(raiz)):
    t = os.path.join(raiz, rec, 'traza.tsv')
    if not os.path.isfile(t): continue
    for l in open(t, encoding='utf-8'):
        p = l.rstrip('\n').split('\t')
        if len(p) < 8: continue
        archivo, linea, op, estado, n, ini, fin, args = p[:8]
        k = (archivo, int(linea))
        f = filas.setdefault(k, {'ops': collections.Counter(), 'n': 0, 'rec': set(), 'estados': collections.Counter(), 'ej': []})
        f['ops'][op] += int(n); f['n'] += int(n); f['rec'].add(rec); f['estados'][estado] += int(n)
        for a in args.split(' || '):
            if a and a not in f['ej'] and len(f['ej']) < 3: f['ej'].append(a)
def partes(e):
    return dict(x.split('=', 1) for x in e.split(' ') if '=' in x)
cab = ['archivo', 'linea', 'operaciones', 'llamadas', 'recorridos', 'varia', 'estados', 'ejemplos']
lineas = ['\t'.join(cab)]
varian = 0; porArchivo = collections.Counter()
for (archivo, linea) in sorted(filas, key=lambda k: (k[0], k[1])):
    f = filas[(archivo, linea)]
    ests = [e for e in f['estados'] if e != '-']
    varia = []
    if len(ests) > 1:
        ps = [partes(e) for e in ests]
        for clave in ('s', 't', 'c', 'i', 'p', 'm', 'b', 'r', 'd'):
            if len(set(x.get(clave) for x in ps)) > 1: varia.append(clave)
    if varia: varian += 1
    porArchivo[archivo] += 1
    estados = ' | '.join('%s x%d' % (e, c) for e, c in f['estados'].most_common())
    lineas.append('\t'.join([archivo, str(linea), ', '.join('%s x%d' % (o, c) for o, c in f['ops'].most_common()), str(f['n']), ','.join(sorted(f['rec'])), ','.join(varia) or '-', estados, ' || '.join(f['ej'])]))
open(salida, 'w', encoding='utf-8').write('\n'.join(lineas) + '\n')
print('lineas de XP que tocan GDI+: %d (en %d archivos); con el estado distinto entre llamadas: %d' % (len(filas), len(porArchivo), varian))
for a, c in porArchivo.most_common(): print('  %s: %d' % (a, c))
