#!/usr/bin/env python3
# EL CATALOGO DE PRUEBA de la pared por generos (FASE11_pared-generos.md): INVENTADO de punta a punta (ningun artista, disco ni id de verdad),
# con el formato «I8» (RocolaOs/RocolaOs.ParedGenerosCatalogo.cs). Lo mismo cada vez (sin azar).
#   catalogo.py CARPETA [DISCOS_POR_LETRA_GRANDE]   ->  CARPETA/rocola_catalogo.tsv.gz y CARPETA/rocola_catalogo.txt (sha256 y bytes)
# Letras A, B y C con DISCOS_POR_LETRA_GRANDE discos cada una (9000 de fabrica: las 500 paginas); L, O, M, Q y # con unos pocos (Q: una letra
# que la musica de prueba no tiene). Lo que comprueba la prueba:
#   - A: lo primero del archivo (lo mas famoso) son «Ángeles Ficticios» (con acento; en la musica de prueba «Angeles Ficticios», con dos de sus
#     discos: «Cumbias del Recuerdo», que el catalogo tambien trae y no debe repetirse, y nada mas) y «Alma Norteña»; luego artistas
#     inventados de 10 generos de XP, 6 discos cada uno, del mas al menos famoso;
#   - discos sin id de Spotify (cada 7.o: se buscan por nombre), uno con id «catno...» (Spotify no lo tiene: 404) y «Disco Sin Spotify»
#     sin id (la busqueda no da nada); mbid que acaban en 0 (el Cover Art Archive falso da 404) y discos con portada 0.
import gzip, hashlib, os, sys

out = sys.argv[1]
grande = int(sys.argv[2]) if len(sys.argv) > 2 else 9000
os.makedirs(out, exist_ok=True)
GEN = ["Regional", "Tropical", "Bolero", "Latino", "Pop", "Rock", "Reggaeton", "Electronica", "Jazz", "Otros"]
SIL = ["ra", "mo", "li", "ta", "ve", "su", "no", "ca", "pe", "di", "lu", "ro", "sa", "mi", "te", "ga"]

def mbid(n):
    h = hashlib.sha1(("mbid-%d" % n).encode()).hexdigest()
    return "%s-%s-%s-%s-%s" % (h[0:8], h[8:12], h[12:16], h[16:20], h[20:32])

def nombre(letra, i):
    a = SIL[i % 16]; b = SIL[(i // 16) % 16]; c = SIL[(i // 256) % 16]
    return "%s%s%s%s Ficticio %04d" % (letra if letra != "#" else "1", a, b, c, i)

lineas = []
na = nd = 0
n = 0   # numerador global de discos (ids, mbid)
def disco(nom, anio, conid=True, idfijo=None, portada=None):
    global n
    n += 1
    m = mbid(n)
    if portada is None: portada = 0 if n % 11 == 0 else 1
    if idfijo is not None: sid = idfijo
    elif conid and n % 7 != 0: sid = "cat%06d" % n
    else: sid = ""
    return "D\t%s\t%d\t%s\t%s\t%d" % (nom, anio, sid, m, portada)

letras = {}
for L in ["#", "A", "B", "C", "L", "M", "O", "Q"]:
    bloque = []; arts = 0; dis = 0
    if L in "ABC":
        if L == "A":
            bloque.append("A\tÁngeles Ficticios\tTropical\t99999\tcatart01\tMX\tw")
            for k, x in enumerate(["Cumbias del Recuerdo", "Más Cumbias Ficticias", "Éxitos Ficticios"]):
                bloque.append(disco(x, 1990 + k)); dis += 1
            bloque.append("A\tAlma Norteña\tRegional\t99998\tcatart02\tMX\tw")
            for k, x in enumerate(["Corridos Bravos", "Otros Corridos Bravos"]):
                bloque.append(disco(x, 1995 + k)); dis += 1
            bloque.append("A\tArtista Raro de Prueba\tRock\t99997\t\tMX\tw")
            bloque.append(disco("Disco Que No Esta", 2001, idfijo="catno0001")); dis += 1
            bloque.append(disco("Disco Sin Spotify", 2002, conid=False)); dis += 1
            bloque.append(disco("Disco Por Nombre", 2003, conid=False)); dis += 1
            arts += 3
        i = 0
        while dis < grande:
            g = GEN[i % len(GEN)]
            bloque.append("A\t%s\t%s\t%d\tcatart%s%05d\tMX\tw" % (nombre(L, i), g, 90000 - i, L, i)); arts += 1
            for k in range(6):
                if dis >= grande: break
                bloque.append(disco("%s Disco %d" % (nombre(L, i), k + 1), 1960 + (i + k) % 60)); dis += 1
            i += 1
    else:
        cuantos = {"#": 4, "L": 6, "M": 5, "O": 4, "Q": 5}[L]
        if L == "O":
            bloque.append("A\tOtra Banda\tPop\t5000\tart60\tMX\tw")
            bloque.append(disco("Otra Banda", 2000)); bloque.append(disco("Disco Lejano", 2001)); dis += 2; arts += 1
        for i in range(cuantos):
            g = GEN[(i * 3) % len(GEN)]
            bloque.append("A\t%s\t%s\t%d\t\tMX\tm" % (nombre(L, i), g, 1000 - i)); arts += 1
            for k in range(3):
                bloque.append(disco("%s Disco %d" % (nombre(L, i), k + 1), 1980 + k)); dis += 1
    lineas.append("L\t%s\t%d\t%d" % (L, arts, dis))
    lineas.extend(bloque)
    na += arts; nd += dis
texto = ("#rocola-catalogo\t1\t2026-11-15\t%d\t%d\n" % (na, nd)) + "\n".join(lineas) + "\n"
datos = gzip.compress(texto.encode("utf-8"), mtime=0)
open(os.path.join(out, "rocola_catalogo.tsv.gz"), "wb").write(datos)
open(os.path.join(out, "rocola_catalogo.txt"), "w").write("sha256=%s\nbytes=%d\n" % (hashlib.sha256(datos).hexdigest(), len(datos)))
print("catalogo de prueba: %d artistas, %d discos, %d bytes (%d sin comprimir)" % (na, nd, len(datos), len(texto.encode("utf-8"))))
