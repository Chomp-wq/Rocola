# repaso.py DIR...: por corrida, canciones medidas en el tramo «repaso» (de las lineas «sonoridad integrada» del diagnostico) y lo que gasto
# el hilo del repaso (% de un nucleo del tramo): ms de procesador por cancion
import sys, glob, os, re, statistics as st, collections
res = collections.defaultdict(list)
for d in sys.argv[1:]:
    for r in sorted(glob.glob(d + '/*/mide.tsv')):
        prog = os.path.basename(os.path.dirname(r)).rsplit('-', 1)[0]
        t = [l.rstrip('\n').split('\t') for l in open(r) if l.startswith('tramo\trepaso')]
        if not t: continue
        c = t[0]; hil = dict(x.rsplit('=', 1) for x in c[-1].replace('hilos:', '').split())
        rp = sum(float(v) for k, v in hil.items() if k.startswith('repaso'))
        diag = open(os.path.dirname(r) + '/rocola_diagnostico.txt', errors='replace').read()
        ns = [int(m) for m in re.findall(r'sonoridad integrada: (\d+) canciones', diag)]
        res[prog].append((rp, ns[-1] if ns else 0))
for p, v in res.items():
    print(p, "corridas", len(v), "repaso %nucleo", [x[0] for x in v], "canciones (diag, cada 50)", [x[1] for x in v])
