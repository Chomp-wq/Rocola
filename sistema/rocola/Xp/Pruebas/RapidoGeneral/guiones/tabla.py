# tabla.py DIR...: por escena y programa, las medianas de las vueltas del tramo «escena»: cuadros, p50, p95, p99, max, gc0, MB nuevos de la pantalla, KB/cuadro
import sys, os, glob, statistics as st, collections
filas = collections.defaultdict(list)
for d in sys.argv[1:]:
    esc = os.path.basename(d.rstrip('/'))
    for r in sorted(glob.glob(d + '/*/mide.tsv')):
        prog = os.path.basename(os.path.dirname(r)).rsplit('-', 1)[0]
        carga = ''
        try: carga = open(os.path.dirname(r) + '/salida.txt').readline().split()[3]
        except Exception: pass
        for l in open(r):
            c = l.rstrip('\n').split('\t')
            if c[0] == 'tramo' and c[1] == 'escena':
                filas[(esc, prog)].append([float(c[2]), float(c[3]), float(c[4]), float(c[5]), float(c[6]), float(c[12]), float(c[15]), float(c[16]), float(carga or 0)])
print("escena\tprograma\tvueltas\tcuadros\tp50\tp95\tp99\tmax\tgc0\tMB_pantalla\tKB/cuadro\tcarga")
for (e, p), v in sorted(filas.items()):
    m = [st.median(x[i] for x in v) for i in range(9)]
    print("%s\t%s\t%d\t%.0f\t%.1f\t%.1f\t%.1f\t%.1f\t%.0f\t%.2f\t%.1f\t%.1f" % (e, p, len(v), *m))
