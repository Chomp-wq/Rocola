#!/usr/bin/env python3
# Comprueba la lista de una letra que armo la rocola (ROCOLA_PARED_VOLCADO: casilla, genero, xp|catalogo, artista, disco, clave) contra el
# catalogo de prueba y las reglas de la pared por generos (FASE11_pared-generos.md).
#   verifica.py VOLCADO CATALOGO.tsv.gz LETRA ORDEN(generos separados por comas) TUYOS(cuantos discos de XP tiene la letra)
import gzip, sys, unicodedata

vol, catgz, letra, orden, tuyos = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4].split(","), int(sys.argv[5])
mal = []
def norma(s):   # como MotorBusqueda.Normaliza: sin acentos ni mayusculas, solo letras y cifras, un espacio entre palabras
    s = unicodedata.normalize("NFD", s.lower())
    out, esp = [], False
    for ch in s:
        if unicodedata.category(ch) == "Mn" or ch in "'’‘ʼ": continue
        if ch.isalnum():
            if esp and out: out.append(" ")
            out.append(ch); esp = False
        else: esp = True
    return "".join(out)
filas = [l.rstrip("\n").split("\t") for l in open(vol, encoding="utf-8") if l.strip()]
rango = {g: i for i, g in enumerate(orden)}
print("filas: %d (%d paginas); tuyas: %d; generos en orden: %s" % (len(filas), (len(filas) + 17) // 18, sum(1 for f in filas if f[2] == "xp"), ",".join(orden[:8]) + "..."))
if len(filas) != 500 * 18: mal.append("no son 500 paginas de 18: %d discos" % len(filas))
if sum(1 for f in filas if f[2] == "xp") != tuyos: mal.append("no estan tus %d discos" % tuyos)
# el catalogo, en el orden del archivo
cat, art = [], None
dentro = False
for l in gzip.open(catgz, "rt", encoding="utf-8"):
    c = l.rstrip("\n").split("\t")
    if c[0] == "L": dentro = c[1] == letra; continue
    if not dentro: continue
    if c[0] == "A": art = c[1]
    elif c[0] == "D": cat.append((art, c[1], c[4] if c[4] else ("sp-" + c[3] if c[3] else None)))
tuyas = set((norma(f[3]), norma(f[4])) for f in filas if f[2] == "xp")
quedan = [x for x in cat if (norma(x[0]), norma(x[1])) not in tuyas]
puestas = [(f[3], f[4]) for f in filas if f[2] == "catalogo"]
esperadas = [(x[0], x[1]) for x in quedan[:500 * 18 - tuyos]]
if set(puestas) != set(esperadas): mal.append("los del catalogo no son los primeros %d del archivo sin los tuyos (sobran %d, faltan %d)" % (len(esperadas), len(set(puestas) - set(esperadas)), len(set(esperadas) - set(puestas))))
fuera = quedan[500 * 18 - tuyos:]
print("del catalogo: %d puestos; fuera por las 500 paginas: %d (el ultimo del archivo: %s)" % (len(puestas), len(fuera), fuera[-1][1] if fuera else "-"))
for x in cat:
    if (norma(x[0]), norma(x[1])) in tuyas and (x[0], x[1]) in set(puestas): mal.append("repetido: %s - %s" % x)
# el orden: genero (por su lugar), artista (sin acentos ni mayusculas), lo tuyo antes, lo del catalogo en el orden del archivo
pos = {(x[0], x[1]): i for i, x in enumerate(cat)}
antes = None
for i, f in enumerate(filas):
    k = (rango.get(f[1], len(orden)), norma(f[3]))
    if antes is not None:
        if k < antes[0]: mal.append("casilla %d fuera de orden: %s / %s despues de %s" % (i, f[1], f[3], antes[1])); break
        if k == antes[0]:
            if antes[2] == "catalogo" and f[2] == "xp": mal.append("casilla %d: lo tuyo despues de lo del catalogo (%s)" % (i, f[3])); break
            if antes[2] == "catalogo" and f[2] == "catalogo" and pos.get((f[3], f[4]), -1) < pos.get((antes[3], antes[4]), -1): mal.append("casilla %d: del catalogo fuera del orden del archivo" % i); break
    antes = (k, f[3], f[2], f[3], f[4])
gens = []
for f in filas:
    if not gens or gens[-1] != f[1]: gens.append(f[1])
if len(gens) != len(set(gens)): mal.append("un genero sale en dos tramos: %s" % gens)
print("generos de la letra, en orden: %s" % ", ".join(gens))
if letra == "A":
    # tus discos y los del catalogo del mismo artista, juntos (Alma Nortena / Alma Norteña, Angeles / Ángeles), con el genero de XP de los tuyos
    # (Regional: «Corridos», «Cumbias»; el catalogo dice Tropical de Ángeles) y sin repetir los que ya tienes
    def tramo(art):
        k = norma(art); return [(i, f[1], f[2], f[4]) for i, f in enumerate(filas) if norma(f[3]) == k]
    for art, esp in (("Alma Nortena", [("Regional", "xp", "Corridos Bravos"), ("Regional", "catalogo", "Otros Corridos Bravos")]),
                     ("Angeles Ficticios", [("Regional", "xp", "Cumbias del Recuerdo"), ("Regional", "catalogo", "Más Cumbias Ficticias"), ("Regional", "catalogo", "Éxitos Ficticios")])):
        t = tramo(art)
        print("%s: %s" % (art, " | ".join("%d %s %s %s" % x for x in t)))
        if [(x[1], x[2], x[3]) for x in t] != esp: mal.append("%s: no estan juntos, en su orden y sin repetir: %s" % (art, t))
        elif t[-1][0] - t[0][0] != len(t) - 1: mal.append("%s: no estan seguidos" % art)
for m in mal: print("MAL: " + m)
sys.exit(1 if mal else 0)
