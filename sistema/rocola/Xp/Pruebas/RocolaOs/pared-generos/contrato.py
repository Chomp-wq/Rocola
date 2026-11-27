#!/usr/bin/env python3
# El contrato «I8» con el grupo catalogo-datos: la letra que armo la rocola (ROCOLA_PARED_VOLCADO: casilla, pastilla, xp|catalogo, artista,
# disco, clave, el artista con que se ordena) con SU archivo de prueba (contrato/rocola_catalogo_prueba.tsv.gz) trae los discos del archivo,
# con sus nombres (acentos) y la pastilla del genero de su artista (o ninguna si es «Otros»), en el orden de XP. El resto lo mira verifica.py.
#   contrato.py VOLCADO CATALOGO.tsv.gz LETRA TUYOS
import gzip, os, sys, unicodedata

vol, catgz, letra, tuyos = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
def norma(s):
    s = unicodedata.normalize("NFD", s.lower()); return " ".join("".join(c if c.isalnum() else " " for c in s if unicodedata.category(c) != "Mn").split())
filas = [l.rstrip("\n").split("\t") for l in open(vol, encoding="utf-8") if l.strip()]
cat, dentro, art = [], False, None
for l in gzip.open(catgz, "rt", encoding="utf-8"):
    c = l.rstrip("\n").split("\t")
    if c[0] == "L": dentro = c[1] == letra
    elif dentro and c[0] == "A": art = c
    elif dentro and c[0] == "D":
        # (27-11-2026) solo los que van a la pared: con portada, sin marcas, de un artista que no sea de cine, karaoke, infantil u hablado y con la portada
        # a la mano (en la prueba, los mbid que acaban en 0 no tienen: el Cover Art Archive falso les da 404; ver verifica.py)
        if art[2] in ("Soundtrack", "Karaoke", "Infantil", "Hablado") or c[5] != "1" or (len(c) > 6 and c[6]) or (os.environ.get("PARED_MANO", "falso") == "falso" and c[4].endswith("0")): continue
        cat.append((art[1], "-" if art[2] == "Otros" else art[2], c[1]))
tuyas = [f for f in filas if f[2] == "xp"]
tuyo = set(norma(f[3]) for f in tuyas)
suyo = [(f[3], f[1], f[4]) for f in filas if f[2] == "catalogo"]
print("letra %s: %d filas (%d tuyas, %d del catalogo); el archivo trae %d en la letra" % (letra, len(filas), len(tuyas), len(suyo), len(cat)))
mal = []
if len(tuyas) != tuyos: mal.append("no estan tus %d discos" % tuyos)
# (los de artistas que tambien tienes llevan la pastilla de tus discos: aqui solo se comparan los demas)
a = sorted(x for x in suyo if norma(x[0]) not in tuyo); b = sorted(x for x in cat if norma(x[0]) not in tuyo)
if a != b: mal.append("los del catalogo no son los del archivo (artista, pastilla, disco): %r / %r" % (a[:3], b[:3]))
if len(suyo) != len([x for x in cat if norma(x[0]) not in tuyo]) + len([x for x in suyo if norma(x[0]) in tuyo]): mal.append("cuenta")
for m in mal: print("MAL: " + m)
sys.exit(1 if mal else 0)
