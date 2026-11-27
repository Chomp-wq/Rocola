#!/usr/bin/env python3
# La letra A que armo la rocola con el catalogo de especial.py (escena «exclusiones»): solo entran los discos que van a la pared.
#   verifica_excl.py VOLCADO MBIDS.txt     (el volcado: casilla, pastilla, xp|catalogo, artista, disco, clave, el artista con que se ordena)
import sys
filas = [l.rstrip("\n").split("\t") for l in open(sys.argv[1], encoding="utf-8") if l.strip()]
info = [l.rstrip("\n").split("\t") for l in open(sys.argv[2], encoding="utf-8") if l.strip()]
cat = sorted((f[3], f[4]) for f in filas if f[2] == "catalogo")
deben = sorted((i[1], i[2]) for i in info if i[3] == "1")
xp = sorted(f[4] for f in filas if f[2] == "xp")
mal = 0
print("del catalogo en la pared: %s" % [d for _, d in cat])
for i in info:
    esta = (i[1], i[2]) in cat
    if esta != (i[3] == "1"):
        mal += 1
        print("MAL: «%s - %s» %s (%s)" % (i[1], i[2], "esta y no debe" if esta else "falta", i[4] or "entra"))
print("tuyos: %s" % xp)
if "Cumbias del Recuerdo" not in xp:
    mal += 1
    print("MAL: falta tu «Cumbias del Recuerdo»")
if sum(1 for _, d in cat if d.startswith("Cumbias del Recuerdo") or d.startswith("Corridos Bravos")):
    mal += 1
    print("MAL: se repitio un disco que ya tienes (con otra edicion)")
if len(filas) != len(xp) + len(deben):
    mal += 1
    print("MAL: son %d discos (deben ser %d)" % (len(filas), len(xp) + len(deben)))
# el de «Alma Norteña feat. Invitado» se ordena con tu nombre («Alma Nortena»), junto a tu «Corridos Bravos»
for f in filas:
    if f[2] == "catalogo" and f[4] == "Corridos Nuevos" and f[6] != "Alma Nortena":
        mal += 1
        print("MAL: «Corridos Nuevos» se ordena como «%s» (debe ser el nombre del artista que tienes)" % f[6])
sys.exit(1 if mal else 0)
