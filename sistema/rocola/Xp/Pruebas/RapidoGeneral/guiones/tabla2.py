# tabla2.py DIR...: como tabla.py, pero con la dispersion: por escena y programa, la mediana y [minimo-maximo] de las vueltas
import sys, os, glob, statistics as st, collections
filas = collections.defaultdict(list)
for d in sys.argv[1:]:
    esc = os.path.basename(d.rstrip('/')).split('-', 1)[-1]
    for r in sorted(glob.glob(d + '/*/mide.tsv')):
        prog = os.path.basename(os.path.dirname(r)).rsplit('-', 1)[0]
        carga = 0.0
        try: carga = float(open(os.path.dirname(r) + '/salida.txt').readline().split()[3])
        except Exception: pass
        for l in open(r):
            c = l.rstrip('\n').split('\t')
            if c[0] == 'tramo' and c[1] == 'escena':
                filas[(esc, prog)].append([float(c[2]), float(c[3]), float(c[4]), float(c[5]), float(c[6]), float(c[15]), float(c[16]), carga])
nom = ["cuadros", "p50", "p95", "p99", "max", "MB", "KB/c", "carga"]
print("escena\tprograma\tn\t" + "\t".join(nom))
for (e, p), v in sorted(filas.items()):
    out = []
    for i in range(len(nom)):
        xs = [x[i] for x in v]
        out.append("%.1f [%.1f-%.1f]" % (st.median(xs), min(xs), max(xs)))
    print("%s\t%s\t%d\t%s" % (e, p, len(v), "\t".join(out)))
