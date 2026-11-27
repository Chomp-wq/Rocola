#!/usr/bin/env python3
# Comprueba la lista de una letra que armo la rocola (ROCOLA_PARED_VOLCADO: casilla, pastilla o «-», xp|catalogo, artista, disco, clave, el
# artista con que se ordena) contra el catalogo y las reglas de la pared con el catalogo (FASE11_pared-generos.md; decision del dueno del
# 20-11-2026: el orden de XP, los discos del catalogo intercalados, los de los artistas mas famosos primero, la pastilla en cada disco).
#   verifica.py VOLCADO CATALOGO.tsv.gz LETRA TUYOS(cuantos discos de XP tiene la letra) [prueba]
# Con «prueba» (el catalogo inventado de catalogo.py) comprueba ademas los casos puestos a proposito en la letra A.
# (27-11-2026) Solo entran los discos del catalogo que van a la pared: con portada (columna 6), sin marcas (columna 7: bandas sonoras, videojuegos...),
# de un artista que no sea de esos generos (Soundtrack, Karaoke, Infantil, Hablado) y con la portada a la mano; en la prueba el paquete de portadas lo arma
# arma_portadas.py contra el Cover Art Archive FALSO, que da 404 a los mbid que acaban en 0 (PARED_MANO=falso, de fabrica): esos tampoco entran.
import gzip, os, sys, unicodedata

vol, catgz, letra, tuyos = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
deprueba = len(sys.argv) > 5 and sys.argv[5] == "prueba"
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
def up(s):      # string.Compare(..., OrdinalIgnoreCase) de .NET: cada caracter (UTF-16) a su mayuscula invariante, y por su numero
    r = []
    for ch in s:
        u = ch.upper()
        # (.NET no pasa la i turca sin punto a la I en su caja invariante, ni la de la I con punto: se quedan como estan)
        r.append(u if len(u) == 1 and ch not in "\u0131\u0130" else ch)
    return "".join(r).encode("utf-16-be")
def clave(art, dis): return (up(art), up(dis))

filas = [l.rstrip("\n").split("\t") for l in open(vol, encoding="utf-8") if l.strip()]
xp = [f for f in filas if f[2] == "xp"]
print("filas: %d (%d paginas); tuyas: %d; con pastilla: %d" % (len(filas), (len(filas) + 17) // 18, len(xp), sum(1 for f in filas if f[1] != "-")))
if len(xp) != tuyos: mal.append("no estan tus %d discos" % tuyos)
# el catalogo, en el orden del archivo
cat, art, gen, dentro = [], None, None, False
for l in gzip.open(catgz, "rt", encoding="utf-8"):
    c = l.rstrip("\n").split("\t")
    if c[0] == "L": dentro = c[1] == letra; continue
    if not dentro: continue
    if c[0] == "A": art, gen = c[1], c[2]
    elif c[0] == "D":
        mano = os.environ.get("PARED_MANO", "falso") != "falso" or not c[4].endswith("0")
        if gen in ("Soundtrack", "Karaoke", "Infantil", "Hablado") or len(c) < 6 or c[5] != "1" or (len(c) > 6 and c[6]) or not mano: continue
        cat.append((art, c[1], gen, c[4] if c[4] else ("sp-" + c[3] if c[3] else None)))
tuyas = set((norma(f[3]), norma(f[4])) for f in xp)
for f in xp:
    if f[4] == "": tuyas.add((norma(f[3]), norma(f[3])))
quedan = [x for x in cat if norma(x[0]) and (norma(x[0]), norma(x[1])) not in tuyas]
cupo = 500 * 18 - tuyos
esperadas = quedan[:cupo]
puestas = [(f[3], f[4]) for f in filas if f[2] == "catalogo"]
if len(filas) != min(500 * 18, tuyos + len(quedan)): mal.append("son %d discos (deben ser %d)" % (len(filas), min(500 * 18, tuyos + len(quedan))))
if sorted(puestas) != sorted((x[0], x[1]) for x in esperadas):
    mal.append("los del catalogo no son los primeros %d del archivo (los de los artistas mas famosos) sin los tuyos (sobran %d, faltan %d)"
               % (len(esperadas), len(set(puestas) - set((x[0], x[1]) for x in esperadas)), len(set((x[0], x[1]) for x in esperadas) - set(puestas))))
fuera = quedan[cupo:]
print("del catalogo: %d puestos; fuera por las 500 paginas: %d (el ultimo que entro: %s - %s%s)" % (len(puestas), len(fuera),
      esperadas[-1][0] if esperadas else "-", esperadas[-1][1] if esperadas else "-", "; el primero fuera: %s - %s" % fuera[0][:2] if fuera else ""))
for f in filas:
    if f[2] == "catalogo" and (norma(f[3]), norma(f[4])) in tuyas: mal.append("repetido: %s - %s" % (f[3], f[4])); break
# EL ORDEN DE XP: por el artista con que se ordena y el disco (OrdinalIgnoreCase); a igualdad, lo tuyo antes y lo del catalogo en el orden
# del archivo. Tus discos, en el orden de XP (el mismo).
pos = {}
for i, x in enumerate(cat): pos.setdefault((x[0], x[1]), i)
for i in range(1, len(filas)):
    a, b = filas[i - 1], filas[i]
    ka, kb = clave(a[6], a[4]), clave(b[6], b[4])
    if kb < ka: mal.append("casilla %d fuera del orden de XP: «%s - %s» despues de «%s - %s»" % (i, b[6], b[4], a[6], a[4])); break
    if kb == ka:
        if a[2] == "catalogo" and b[2] == "xp": mal.append("casilla %d: lo tuyo despues de lo del catalogo (%s)" % (i, b[3])); break
        if a[2] == "catalogo" and b[2] == "catalogo" and pos.get((b[3], b[4]), -1) < pos.get((a[3], a[4]), -1): mal.append("casilla %d: a igualdad, fuera del orden del archivo" % i); break
# un artista del catalogo con el nombre de uno tuyo se ordena con el tuyo; si no, con el suyo
tuyo_de = {}
for f in xp: tuyo_de.setdefault(norma(f[3]), f[3])
for f in filas:
    if f[2] != "catalogo": continue
    esp = tuyo_de.get(norma(f[3]), f[3])
    if f[6] != esp: mal.append("«%s - %s» se ordena como «%s» (debe ser «%s»)" % (f[3], f[4], f[6], esp)); break
# LA PASTILLA: en los del catalogo, la de tus discos de ese artista (la que mas se repite) o la del catalogo; «Otros», sin pastilla
votos = {}
for f in xp:
    if f[1] != "-": votos.setdefault(norma(f[3]), {}).setdefault(f[1], 0); votos[norma(f[3])][f[1]] += 1
gen_cat = {}   # el genero del artista de cada disco, por la clave del disco (dos artistas pueden llamarse igual)
for x in cat: gen_cat[x[3]] = x[2]
malp = 0
for f in filas:
    if f[2] != "catalogo": continue
    v = votos.get(norma(f[3]))
    esp = (sorted(v.items(), key=lambda kv: (-kv[1], kv[0]))[0][0]) if v else gen_cat.get(f[5], "Otros")
    if esp == "Otros": esp = "-"
    if f[1] != esp:
        malp += 1
        if malp == 1: mal.append("la pastilla de «%s - %s» es «%s» (debe ser «%s»)" % (f[3], f[4], f[1], esp))
gens = {}
for f in filas: gens[f[1]] = gens.get(f[1], 0) + 1
print("pastillas: %s" % ", ".join("%s %d" % kv for kv in sorted(gens.items(), key=lambda kv: -kv[1])))
print("tus discos: %s" % " | ".join("%s %s - %s (%s)" % (f[0], f[3], f[4], f[1]) for f in xp))
if deprueba and letra == "A":
    # tus discos y los del catalogo del mismo artista, juntos (Alma Nortena / Alma Norteña, Angeles / Ángeles) en el orden de XP por disco, con
    # la pastilla de tus discos (Regional: el catalogo dice Tropical de Ángeles) y sin repetir los que ya tienes
    def tramo(art):
        k = norma(art); return [(int(f[0]), f[1], f[2], f[4]) for f in filas if norma(f[3]) == k]
    for art, esp in (("Alma Nortena", [("Regional", "xp", "Corridos Bravos"), ("Regional", "catalogo", "Otros Corridos Bravos")]),
                     ("Angeles Ficticios", [("Regional", "xp", "Cumbias del Recuerdo"), ("Regional", "catalogo", "Más Cumbias Ficticias"), ("Regional", "catalogo", "Éxitos Ficticios")])):
        t = tramo(art)
        print("%s: %s" % (art, " | ".join("%d %s %s %s" % x for x in t)))
        if [(x[1], x[2], x[3]) for x in t] != esp: mal.append("%s: no estan juntos, en su orden y sin repetir: %s" % (art, t))
        elif t[-1][0] - t[0][0] != len(t) - 1: mal.append("%s: no estan seguidos" % art)
    # lo de atras del archivo no entro (los artistas menos famosos) y todos los de «Otros» van sin pastilla
    if len(fuera) < 1: mal.append("del catalogo de prueba deben quedar fuera los ultimos del archivo (por las 500 paginas): %d" % len(fuera))
for m in mal: print("MAL: " + m)
sys.exit(1 if mal else 0)
