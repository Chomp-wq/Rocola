# resume.py MUESTRAS [HILO]: por hilo, las funciones mas vistas arriba (propias) y en la pila (incluidas), sin las de esperar
import sys, collections
path = sys.argv[1]; filtro = sys.argv[2] if len(sys.argv) > 2 else None
ESPERA = ("futex", "poll", "epoll", "nanosleep", "pthread_cond", "__GI___", "clock_nanosleep", "read", "syscall", "WaitOne", "Wait", "sem_", "__lll")
top = collections.defaultdict(collections.Counter); inc = collections.defaultdict(collections.Counter); tot = collections.Counter(); ocup = collections.Counter()
for l in open(path, errors="replace"):
    if "|" not in l: continue
    th, st = l.rstrip("\n").split("|", 1); fr = st.split(";")
    if filtro and filtro not in th: continue
    tot[th] += 1
    if fr and any(fr[0].startswith(e) or e in fr[0] for e in ESPERA): continue
    ocup[th] += 1; top[th][fr[0]] += 1
    for x in set(fr): inc[th][x] += 1
for th, n in ocup.most_common(12):
    print("%-30s ocupado %d de %d" % (th, n, tot[th]))
    for f, c in top[th].most_common(6): print("     arriba %3d %s" % (c, f[:150]))
    for f, c in inc[th].most_common(14): print("     pila   %3d %s" % (c, f[:150]))
