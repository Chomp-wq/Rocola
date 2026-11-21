# analiza4.py CARPETA_SERIE : por configuracion (etiqueta-N), con analiza2 para las paginas, y ademas el colchon del sonido (el menor, el p1
# mas bajo y la mediana de las medianas de los renglones de 5 s, y los huecos), los lectores (el mayor y el % con dos) y lo hecho
import sys, os, glob, collections, statistics as st, subprocess
d = sys.argv[1]
por = collections.defaultdict(list)
for c in sorted(glob.glob(d + "/*-[0-9]*")): por[os.path.basename(c).rsplit("-", 1)[0]].append(c)
for et, cs in por.items():
    son = []; lec = []; hechas = []; cargas = []
    for c in cs:
        for l in open(c + "/mide.tsv"):
            p = l.rstrip("\n").split("\t")
            if p[0] == "sonido": son.append((float(p[2]), float(p[3]), float(p[4]), int(p[5])))
            if p[0] == "lectores": lec.append((int(p[1]), float(p[2])))
        for l in open(c + "/salida.txt"):
            if l.startswith("miniaturas hechas"): hechas.append(int(l.split()[-1]))
            if l.startswith("carga al empezar"): cargas.append(l.split()[3])
    a2 = subprocess.run(["python3", "-I", os.path.dirname(os.path.abspath(__file__)) + "/analiza2.py"] + [c + "/mide.tsv" for c in cs], capture_output=True, text=True).stdout.strip().split("\n")
    print("==", et, "cargas", ",".join(cargas), "| hechas", hechas)
    for l in a2: print("   ", l)
    if son: print("    sonido: min %.1f ms, p1 mas bajo %.1f, mediana %.1f, huecos %d (%d renglones)" % (min(x[0] for x in son), min(x[1] for x in son), st.median(x[2] for x in son), sum(x[3] for x in son), len(son)))
    if lec: print("    lectores: max %d, %% con dos: media %.1f, max %.1f" % (max(x[0] for x in lec), st.mean(x[1] for x in lec), max(x[1] for x in lec)))
