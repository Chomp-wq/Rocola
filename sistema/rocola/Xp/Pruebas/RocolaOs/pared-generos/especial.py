#!/usr/bin/env python3
# UN CATALOGO CHICO A PROPOSITO para las escenas «exclusiones», «nombres» y «mano» (27-11-2026): lo que NO va en la pared y lo que ya tienes con otro
# nombre, con el mismo formato «I8» (la columna 7 de «D» son las marcas). INVENTADO. Uso: especial.py CARPETA
# Escribe CARPETA/rocola_catalogo.tsv.gz y .txt, y CARPETA/mbids.txt (una linea por disco: «mbid<TAB>artista<TAB>disco<TAB>entra(1|0)<TAB>por que»).
import gzip, hashlib, os, sys
out = sys.argv[1]
os.makedirs(out, exist_ok=True)
n = [0]


def mbid():
    n[0] += 1
    h = hashlib.sha1(("especial-%d" % n[0]).encode()).hexdigest()
    m = "%s-%s-%s-%s-%s" % (h[0:8], h[8:12], h[12:16], h[16:20], h[20:32])
    return m[:-1] + "7"          # (que no acabe en 0: el Cover Art Archive falso da 404 a esos)


# (artista, genero, fama, [(disco, portada, marcas, entra, por que)])
ARTISTAS = [
    ("Ángeles Ficticios", "Tropical", 99999, [
        ("Cumbias del Recuerdo (Remastered 2015)", 1, "", 0, "es el tuyo con la edicion"),
        ("Cumbias Nuevas", 1, "", 1, ""),
        ("Cumbias (Original Motion Picture Soundtrack)", 1, "", 0, "por el nombre: cine"),
        ("Disco Raro", 1, "j", 0, "por la marca del archivo (videojuego)"),
        ("Sin Portada Alguna", 0, "", 0, "sin portada")]),
    ("Alma Norteña feat. Invitado", "Regional", 99998, [
        ("Corridos Bravos - Edición Especial", 1, "", 0, "es el tuyo con la edicion y el artista principal"),
        ("Corridos Nuevos", 1, "", 1, "")]),
    ("Orquesta del Cine", "Soundtrack", 99997, [("Gran Estreno", 1, "", 0, "genero del artista: Soundtrack")]),
    ("Karaoke Stars", "Pop", 99996, [("Exitos del Momento", 1, "", 0, "nombre del artista: karaoke")]),
    ("Voces Dormidas", "Chill", 99995, [("Meditation Music", 1, "", 0, "por el nombre: para dormir o meditar"), ("Olas Tranquilas", 1, "", 1, "")]),
    ("Normal Banda", "Regional", 99994, [("Disco Bueno", 1, "", 1, ""), ("Disco Bueno Dos", 1, "", 1, "")]),
    ("Niños Cantores", "Infantil", 99993, [("Cantos", 1, "", 0, "genero del artista: Infantil")]),
]
lineas, info, na, nd = [], [], 0, 0
for nom, gen, fama, ds in ARTISTAS:
    lineas.append("A\t%s\t%s\t%d\tcatart%02d\tMX\tw" % (nom, gen, fama, na))
    na += 1
    for d, por, marcas, entra, why in ds:
        m = mbid()
        nd += 1
        lineas.append("D\t%s\t2001\tcat%06d\t%s\t%d%s" % (d, nd, m, por, ("\t" + marcas) if marcas else ""))
        info.append("%s\t%s\t%s\t%d\t%s" % (m, nom, d, entra, why))
texto = ("#rocola-catalogo\t1\t2026-11-27\t%d\t%d\nL\tA\t%d\t%d\n" % (na, nd, na, nd)) + "\n".join(lineas) + "\n"
datos = gzip.compress(texto.encode("utf-8"), mtime=0)
open(os.path.join(out, "rocola_catalogo.tsv.gz"), "wb").write(datos)
open(os.path.join(out, "rocola_catalogo.txt"), "w").write("sha256=%s\nbytes=%d\n" % (hashlib.sha256(datos).hexdigest(), len(datos)))
open(os.path.join(out, "mbids.txt"), "w", encoding="utf-8").write("\n".join(info) + "\n")
print("catalogo especial: %d artistas, %d discos (%d entran)" % (na, nd, sum(1 for l in info if l.split("\t")[3] == "1")))
