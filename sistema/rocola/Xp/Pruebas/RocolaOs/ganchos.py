#!/usr/bin/env python3
# ganchos.py [--base REV] [--manifiesto RUTA] [-v]: comprueba los ganchos de Rocola OS (fase 11; PLAN_FASE11.md §2.3; el manifiesto,
# sistema/rocola/Xp/RocolaOs/GANCHOS.tsv). Un gancho es UNA linea que ya existia, cambiada en su sitio: en Codigo/ nunca se anade, se quita
# ni se mueve una linea (las trazas nombran relojes y encargos por archivo:linea, y la referencia congelada tiene esas lineas). Comprueba:
#   1. el manifiesto: cinco columnas, ids gNN sin repetir, linea > 0, ningun tabulador ni CR dentro;
#   2. cada linea «ahora» esta asi en su archivo: en Codigo/, en SU linea; fuera de Codigo/ (Nativo/, Plataforma/), una sola vez en el archivo
#      (alli si se pueden anadir lineas: si se movio, lo dice);
#   3. cada «ahora» lleva su marca: «// ROCOLA OS gNN: (XP) » en Codigo/ y «// ROCOLA OS gNN: (XP) » o «(antes) » fuera (lo de fuera de
#      Codigo/ no es de XP), y la raya «—» que separa el para que;
#   4. ninguna otra linea de Codigo/ lleva «ROCOLA OS», y ninguna otra de Nativo/ y Plataforma/ lleva la marca «ROCOLA OS gNN»;
#   5. DEVOLVIENDO LAS DE ANTES, Codigo/ queda byte a byte como en la base (los mismos archivos, las mismas lineas y los mismos finales,
#      CRLF o LF); y la linea «antes» de cada gancho de fuera de Codigo/ esta en ese archivo de la base;
#   6. cuantos ganchos hay y en que archivos.
# Sale con 0 solo si todo esta bien. REV: la base de Codigo/ (por defecto ca9802f: la del plan, fc7b667, con los cambios de la rocola de XP
# del 17 y 18-11-2026, PORT_xp-1718.md, el modo noche de 50 a 90 % y el de los vecinos del 20-11-2026, PORT_noche-nv.md; antes ebdb272; si cambia Rocola/ y se pasa a Codigo/, la nueva: un commit con Codigo/ SIN los ganchos). Sin git (o sin la base), los puntos 1 a 4 y lo dice.
import os, re, subprocess, sys, collections

AQUI = os.path.dirname(os.path.realpath(__file__))
XP = os.path.abspath(os.path.join(AQUI, "../.."))                 # sistema/rocola/Xp
RAIZ = os.path.abspath(os.path.join(XP, "../../.."))              # el repositorio
MARCA_CODIGO = re.compile(r"// ROCOLA OS (g\d\d+): \(XP\) ")
MARCA_FUERA = re.compile(r"// ROCOLA OS (g\d\d+): \((XP|antes)\) ")
CUALQUIER_MARCA = re.compile(r"ROCOLA OS g\d\d+")
RAYA = "—"


def lee_manifiesto(ruta, errores):
    filas = []
    vistos = set()
    with open(ruta, encoding="utf-8", newline="") as f:
        for n, l in enumerate(f, 1):
            if l.endswith("\n"): l = l[:-1]
            if not l or l.startswith("#"): continue
            if "\r" in l: errores.append("manifiesto, renglon %d: lleva un CR" % n); continue
            c = l.split("\t")
            if len(c) != 5: errores.append("manifiesto, renglon %d: %d columnas (son 5: id, archivo, linea, antes, ahora)" % (n, len(c))); continue
            id, archivo, linea, antes, ahora = c
            if not re.fullmatch(r"g\d\d+", id): errores.append("manifiesto, renglon %d: el id «%s» no es gNN" % (n, id)); continue
            if id in vistos: errores.append("manifiesto: el id %s esta dos veces" % id); continue
            vistos.add(id)
            if not linea.isdigit() or int(linea) < 1: errores.append("%s: la linea «%s» no es un numero" % (id, linea)); continue
            if archivo.startswith("/") or ".." in archivo.split("/"): errores.append("%s: el archivo «%s» no va desde sistema/rocola/Xp" % (id, archivo)); continue
            filas.append((id, archivo, int(linea), antes, ahora))
    return filas


def partes(b):
    # (las lineas sin su fin, y su fin: b"\r\n", b"\n" o b"" en la ultima sin salto)
    ls = b.split(b"\n")
    out = []
    for i, l in enumerate(ls):
        ultima = i == len(ls) - 1
        if ultima and l == b"": break
        if ultima: out.append((l, b""))
        elif l.endswith(b"\r"): out.append((l[:-1], b"\r\n"))
        else: out.append((l, b"\n"))
    return out


def git_show(rev, ruta):
    r = subprocess.run(["git", "-C", RAIZ, "show", rev + ":" + ruta], capture_output=True)
    return r.stdout if r.returncode == 0 else None


def git_ls(rev, carpeta):
    r = subprocess.run(["git", "-C", RAIZ, "ls-tree", "--name-only", rev, carpeta + "/"], capture_output=True, text=True)
    return None if r.returncode != 0 else [os.path.basename(x) for x in r.stdout.split("\n") if x]


def main():
    args = sys.argv[1:]
    base, manifiesto, ver = "ca9802f", os.path.join(XP, "RocolaOs", "GANCHOS.tsv"), False
    if "--base" in args: i = args.index("--base"); base = args[i + 1]; del args[i:i + 2]
    if "--manifiesto" in args: i = args.index("--manifiesto"); manifiesto = args[i + 1]; del args[i:i + 2]
    if "-v" in args: ver = True; args.remove("-v")
    errores, avisos = [], []
    filas = lee_manifiesto(manifiesto, errores)
    por_archivo = collections.defaultdict(list)
    for f in filas: por_archivo[f[1]].append(f)
    # 2 y 3: cada gancho en su sitio y con su marca
    for archivo, gs in sorted(por_archivo.items()):
        p = os.path.join(XP, archivo)
        if not os.path.isfile(p): errores.append("%s: no esta el archivo %s" % (",".join(g[0] for g in gs), archivo)); continue
        ls = partes(open(p, "rb").read())
        texto = [l.decode("utf-8", "replace") for l, _ in ls]
        en_codigo = archivo.startswith("Codigo/")
        lineas_vistas = set()
        for id, _, n, antes, ahora in gs:
            for nombre, c in (("antes", antes), ("ahora", ahora)):
                if "\t" in c or "\r" in c: errores.append("%s: la linea %s lleva un tabulador o un CR" % (id, nombre))
            m = (MARCA_CODIGO if en_codigo else MARCA_FUERA).search(ahora)
            if not m or m.group(1) != id: errores.append("%s: la linea de ahora no lleva su marca «// ROCOLA OS %s: (%s) »" % (id, id, "XP" if en_codigo else "XP|antes"))
            elif RAYA not in ahora[m.end():]: errores.append("%s: a la marca le falta la raya «%s» antes del para que" % (id, RAYA))
            if antes == ahora: errores.append("%s: la de antes y la de ahora son iguales" % id)
            if en_codigo:
                if n in lineas_vistas: errores.append("%s: dos ganchos en la linea %d de %s" % (id, n, archivo))
                lineas_vistas.add(n)
                if n > len(texto): errores.append("%s: %s tiene %d lineas, no %d" % (id, archivo, len(texto), n)); continue
                if texto[n - 1] != ahora:
                    donde = [i + 1 for i, t in enumerate(texto) if t == ahora]
                    errores.append("%s: %s:%d no es la del manifiesto%s" % (id, archivo, n, (" (esta en la %s: SE MOVIO)" % donde) if donde else ""))
            else:
                donde = [i + 1 for i, t in enumerate(texto) if t == ahora]
                if not donde: errores.append("%s: en %s no esta la linea del manifiesto" % (id, archivo))
                elif len(donde) > 1: errores.append("%s: en %s la linea del manifiesto esta %d veces" % (id, archivo, len(donde)))
                elif donde[0] != n: avisos.append("%s: en %s esta en la linea %d, no en la %d (fuera de Codigo/ se pueden anadir lineas: pon la nueva en el manifiesto)" % (id, archivo, donde[0], n))
    # 4: ninguna otra linea con la marca
    ids_de = collections.defaultdict(set)
    for id, archivo, n, antes, ahora in filas: ids_de[archivo].add(ahora)
    for carpeta, patron in (("Codigo", re.compile(r"ROCOLA OS")), ("Nativo", CUALQUIER_MARCA), ("Plataforma", CUALQUIER_MARCA)):
        d = os.path.join(XP, carpeta)
        for nombre in sorted(os.listdir(d)):
            if not nombre.endswith(".cs"): continue
            rel = carpeta + "/" + nombre
            for i, (l, _) in enumerate(partes(open(os.path.join(d, nombre), "rb").read()), 1):
                t = l.decode("utf-8", "replace")
                if patron.search(t) and t not in ids_de.get(rel, ()):
                    errores.append("%s:%d lleva «%s» y no esta en el manifiesto" % (rel, i, patron.search(t).group(0)))
    # 5: devolviendo las de antes, Codigo/ como en la base
    comprobados, base_ok = 0, True
    lista_base = git_ls(base, "sistema/rocola/Xp/Codigo")
    if lista_base is None:
        avisos.append("sin git o sin la base %s: no se compara Codigo/ byte a byte" % base); base_ok = False
    else:
        ahora_lista = sorted(x for x in os.listdir(os.path.join(XP, "Codigo")))
        if sorted(lista_base) != ahora_lista:
            errores.append("Codigo/ no tiene los mismos archivos que en %s: sobran %s, faltan %s" % (base, sorted(set(ahora_lista) - set(lista_base)), sorted(set(lista_base) - set(ahora_lista))))
        for nombre in sorted(set(lista_base) & set(ahora_lista)):
            rel = "Codigo/" + nombre
            ls = partes(open(os.path.join(XP, rel), "rb").read())
            for id, _, n, antes, ahora in por_archivo.get(rel, []):
                if n <= len(ls) and ls[n - 1][0].decode("utf-8", "replace") == ahora: ls[n - 1] = (antes.encode("utf-8"), ls[n - 1][1])
            devuelto = b"".join(l + f for l, f in ls)
            b = git_show(base, "sistema/rocola/Xp/" + rel)
            if b is None: errores.append("%s: no se pudo leer de %s" % (rel, base)); continue
            if devuelto != b:
                lb = partes(b)
                malas = [i + 1 for i in range(max(len(ls), len(lb))) if i >= len(ls) or i >= len(lb) or ls[i] != lb[i]]
                errores.append("%s: devolviendo los ganchos NO queda como en %s (%d lineas ahora, %d alli; distintas: %s)" % (rel, base, len(ls), len(lb), malas[:8]))
            comprobados += 1
        for archivo, gs in sorted(por_archivo.items()):
            if archivo.startswith("Codigo/"): continue
            b = git_show(base, "sistema/rocola/Xp/" + archivo)
            if b is None: avisos.append("%s no esta en %s (archivo nuevo): no se mira su linea de antes" % (archivo, base)); continue
            tb = [l.decode("utf-8", "replace") for l, _ in partes(b)]
            for id, _, n, antes, ahora in gs:
                if antes not in tb: errores.append("%s: la linea de antes no esta en %s de %s" % (id, archivo, base))
    # 6: el resumen
    print("ganchos: %d en %d archivos" % (len(filas), len(por_archivo)))
    for archivo, gs in sorted(por_archivo.items()):
        print("   %-40s %s" % (archivo, " ".join("%s:%d" % (g[0], g[2]) for g in sorted(gs, key=lambda g: g[2]))))
    if base_ok:
        print("Codigo/ devolviendo los ganchos: %d archivos comparados con %s (%d con ganchos)" % (comprobados, base, sum(1 for a in por_archivo if a.startswith("Codigo/"))))
    for a in avisos: print("AVISO " + a)
    for e in errores: print("MAL   " + e)
    print("TODO BIEN" if not errores else "%d MAL" % len(errores))
    sys.exit(0 if not errores else 1)


if __name__ == "__main__":
    main()
