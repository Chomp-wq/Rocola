#!/usr/bin/env python3
# mundo.py DISCOS PREP XP REPO: la carpeta de datos de una rocola de XP FALSA (todo inventado) a partir de lo que midio rocola-xp en PREP
# (las tablas con rutas de Linux de los discos falsos), como la habria dejado XP en Windows: las rutas de DISCOS/sda1 como «C:\...» y las de
# DISCOS/sdb1 como «D:\...», una cancion con otras mayusculas, una que ya no esta, una ultima linea a medias, la fecha de creacion de
# Windows en rocola_fuentes.txt (un dia despues de la de escritura: la copia), y todo lo demas que deja XP en su carpeta (lo que se trae y
# lo que no). Los modelos de IA son los del repositorio (ia/rocola_ia.bin y pruebas/datos/rocola_eq_ia.bin: no son de nadie).
import os, shutil, sys
D, PREP, XP, REPO = sys.argv[1:5]
os.makedirs(XP, exist_ok=True)
PREF = [(D + "/sda1/", "C:\\"), (D + "/sdb1/", "D:\\")]
BORRADO = "C:\\Rocola\\musica\\Borrado\\No Esta.mp3"

def win(linea):
    for p, w in PREF:
        i = linea.find(p)
        if i >= 0:
            r = w + linea[i + len(p):].replace("/", "\\")
            r = r.replace("Los Inventados\\Disco Uno\\01 Tono La.mp3", "LOS INVENTADOS\\disco uno\\01 tono la.mp3")   # otras mayusculas
            return linea[:i] + r
    return linea

def escribe(nombre, lineas, final="\r\n"):
    with open(os.path.join(XP, nombre), "w", encoding="utf-8", newline="") as f:
        f.write("\r\n".join(lineas) + final)

for t in ["rocola_sonoridad.txt", "rocola_volumen.txt", "rocola_eq.txt", "rocola_fuentes.txt", "rocola_duracion.txt"]:
    ls = open(os.path.join(PREP, t), encoding="utf-8").read().replace("\r\n", "\n").rstrip("\n").split("\n")
    sale, primera = [], None
    for l in ls:
        w = win(l)
        if w != l and t == "rocola_fuentes.txt":
            c = w.split("|", 5); c[1] = str(int(c[2]) + 864000000000); w = "|".join(c)   # la creada de Windows: la copia, un dia despues
        if w != l and primera is None: primera = w
        sale.append(w)
    i = primera.find("C:\\") if primera.find("C:\\") >= 0 else primera.find("D:\\")
    sale.append(primera[:i] + BORRADO)   # una cancion que ya no esta en ningun disco
    if t == "rocola_volumen.txt":
        escribe(t, sale, "\r\n0.0123")   # y una ultima linea a medias (un apagon al anadir): no cuenta
    else:
        escribe(t, sale)

C = "C:\\Rocola\\musica\\"
la, mi, pulso = C + "LOS INVENTADOS\\disco uno\\01 tono la.mp3", C + "Los Inventados\\Disco Uno\\02 Tono Mi.mp3", C + "Banda Ficticia\\Primer Disco\\01 Pulso.mp3"
alto, silencio = "D:\\Musica Extra\\Grupo Prueba\\Disco Dos\\02 Tono Alto.mp3", "D:\\Musica Extra\\Grupo Prueba\\Disco Dos\\01 Silencio.mp3"
escribe("rocola_afondo.txt", [la, pulso, BORRADO])
escribe("rocola_ia_hechas.txt", ["2|" + la, mi, "3|" + mi, "2|" + BORRADO])
escribe("rocola_stats.txt", ["ROCOLA-STATS v1", "total-reproducciones=9", "total-canciones=8", "total-videos=1", "monedas=12", "creditos-cargados=20",
                             "[CANCIONES] count|ruta", "3|" + la, "2|" + alto, "1|" + BORRADO, "2|" + pulso,
                             "[ARTISTAS] count|artista", "4|Los Inventados", "2|Grupo Prueba", "[VIDEOS] count|ruta",
                             "[GENEROS] genero|ruta", "Cumbia|" + la, "Rock|" + BORRADO])
escribe("rocola_billar_nivel.txt", ["1.5|" + alto, "-0.75|" + la, "2|" + BORRADO])
escribe("rocola_billar_bitacora.txt", ["fecha\tarchivo\tentera\tsegundos", "2026-11-01 22:10:00\t02 Tono Alto.mp3\tsi\t6"])
escribe("rocola_teclas.txt", ["arriba=38", "abajo=40", "izquierda=37", "derecha=39", "poner=13", "regresar=27", "moneda=77", "saltar=83"])
escribe("rocola_config.txt", ["gpu=1", "diagnostico=1", "noche=1", "noche_pct=80", "dir=D:\\MUSICA EXTRA", "dir=E:\\Nada", "respaldo_dir=D:\\Respaldos", "ia_datos=1"])
# lo que NO se trae
escribe("rocola_estado.txt", ["ROCOLA-ESTADO v1", "ts=639296995223737626", "creditos=5"])
escribe("rocola_noche.txt", ["noche=2026-11-13", "2|" + la])
escribe("rocola_error.txt", ["2026-11-13 22:00:00  [Algo de XP]  (rocola del 26/10/2026)"])
escribe("rocola_calidad.txt", ["CALIDAD DE LA BIBLIOTECA"])
escribe("rocola_incompatibles.txt", ["2026-11-01 10:00  |  " + silencio + "  |  ACM"])
escribe("rocola_biblioteca.txt", ["ROCOLA-LIB v2", "roots=C:\\Rocola\\musica|D:\\MUSICA EXTRA", "sig=5:3:0|2:1:0", "dirs=0", "A1", la, "V0", "C0"])
escribe("rocola_letras.txt", ["ROCOLA-LETRAS v3", "n=1", "sig=5:3:0|2:1:0"])
escribe("rocola_diagnostico.txt", ["22:00:00.000  arranque"])
escribe("rocola_diagnostico_2026-11-01_1200.txt", ["DIAGNOSTICO COMPLETO"])
os.makedirs(os.path.join(XP, "rocola_miniaturas"), exist_ok=True)
open(os.path.join(XP, "rocola_miniaturas", "0123456789abcdef_256_v2.png"), "wb").write(b"\x89PNG falsa")
os.makedirs(os.path.join(XP, "respaldo", "2026-11-13_070000000_x"), exist_ok=True)
escribe(os.path.join("respaldo", "2026-11-13_070000000_x", "rocola_volumen.txt"), ["0.5|6|" + la])
open(os.path.join(XP, "rocola_ia_datos.bin"), "wb").write(b"RIAD" + bytes(12))
open(os.path.join(XP, "rocola_ia_datos.idx"), "wb").write(bytes(64))
# los modelos de IA: uno en IAS (con su copia anterior) y uno suelto con otras mayusculas (en Windows es el mismo nombre)
os.makedirs(os.path.join(XP, "IAS"), exist_ok=True)
shutil.copyfile(os.path.join(REPO, "ia", "rocola_ia.bin"), os.path.join(XP, "IAS", "rocola_ia.bin"))
shutil.copyfile(os.path.join(REPO, "ia", "rocola_ia.bin"), os.path.join(XP, "IAS", "rocola_ia.bin.anterior-1"))
shutil.copyfile(os.path.join(REPO, "pruebas", "datos", "rocola_eq_ia.bin"), os.path.join(XP, "Rocola_EQ_IA.bin"))
# el programa de XP (solo cuenta su largo y su fecha: la identidad de la carpeta)
open(os.path.join(XP, "Rocola.exe"), "wb").write(b"MZ" + b"rocola de XP falsa" * 100)
os.utime(os.path.join(XP, "Rocola.exe"), ns=(1761500000000000000, 1761500000123456700))
print("carpeta de XP falsa en", XP)
