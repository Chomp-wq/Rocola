#!/usr/bin/env python3
# El contrato «I8» con el grupo catalogo-datos: la letra que armo la rocola (ROCOLA_PARED_VOLCADO: casilla, genero, xp|catalogo, artista,
# disco, clave) con SU archivo de prueba (contrato/rocola_catalogo_prueba.tsv.gz) trae los discos del archivo, con sus nombres (acentos),
# el genero de su artista y, de cada artista, en el orden del archivo.
#   contrato.py VOLCADO CATALOGO.tsv.gz LETRA TUYOS
import gzip, sys

vol, catgz, letra, tuyos = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
filas = [l.rstrip("\n").split("\t") for l in open(vol, encoding="utf-8") if l.strip()]
cat, dentro, art = [], False, None
for l in gzip.open(catgz, "rt", encoding="utf-8"):
    c = l.rstrip("\n").split("\t")
    if c[0] == "L": dentro = c[1] == letra
    elif dentro and c[0] == "A": art = c
    elif dentro and c[0] == "D": cat.append((art[1], art[2], c[1]))
tuyas = [f for f in filas if f[2] == "xp"]
suyo = [(f[3], f[1], f[4]) for f in filas if f[2] == "catalogo"]
print("letra %s: %d filas (%d tuyas, %d del catalogo); el archivo trae %d en la letra" % (letra, len(filas), len(tuyas), len(suyo), len(cat)))
mal = []
if len(tuyas) != tuyos: mal.append("no estan tus %d discos" % tuyos)
if sorted(suyo) != sorted(cat): mal.append("los del catalogo no son los del archivo (artista, genero, disco): %r / %r" % (sorted(suyo)[:3], sorted(cat)[:3]))
por, vis = {}, {}
for a, g, d in cat: por.setdefault(a, []).append(d)
for a, g, d in suyo: vis.setdefault(a, []).append(d)
if por != vis: mal.append("los discos de algun artista no van en el orden del archivo")
for m in mal: print("MAL: " + m)
sys.exit(1 if mal else 0)
