#!/usr/bin/env python3
# mundo_xp.py PREP RAIZ_D XP REPO: la carpeta de datos de una rocola de XP FALSA (todo inventado) a partir de lo que midio rocola-xp en
# PREP (las tablas con las rutas de esta maquina), como la habria dejado XP en Windows: lo que empieza por RAIZ_D (la carpeta que sera la
# particion «D:» del disco de XP falso) pasa a «D:\...» con «\», la fecha de creacion de Windows en rocola_fuentes.txt (un dia despues
# de la de escritura: la copia), y lo demas que XP deja en su carpeta: botones, configuracion, lo mas sonado, el estado del dia (5
# creditos: NO se debe traer), un error viejo, el programa (Rocola.exe falso) y los modelos de IA del repositorio (no son de nadie).
# Grupo qemu, 14-11-2026 (como sistema/rocola/Xp/Pruebas/Importa/mundo.py, con las rutas del disco de la prueba en QEMU).
import os, shutil, sys
PREP, RAIZ, XP, REPO = sys.argv[1:5]
os.makedirs(XP, exist_ok=True)

def win(linea):
    i = linea.find(RAIZ)
    if i < 0: return linea
    return linea[:i] + "D:\\" + linea[i + len(RAIZ):].replace("/", "\\")

def escribe(nombre, lineas):
    with open(os.path.join(XP, nombre), "w", encoding="utf-8", newline="") as f:
        f.write("\r\n".join(lineas) + "\r\n")

for t in ["rocola_sonoridad.txt", "rocola_volumen.txt", "rocola_eq.txt", "rocola_fuentes.txt", "rocola_duracion.txt"]:
    p = os.path.join(PREP, t)
    if not os.path.exists(p): continue
    sale = []
    for l in open(p, encoding="utf-8").read().replace("\r\n", "\n").rstrip("\n").split("\n"):
        w = win(l)
        if w != l and t == "rocola_fuentes.txt":
            c = w.split("|", 5); c[1] = str(int(c[2]) + 864000000000); w = "|".join(c)   # la creada de Windows: la copia, un dia despues
        sale.append(w)
    escribe(t, sale)

M = "D:\\musica\\"
la, mi = M + "Los Inventados - Disco Uno\\01 Tono La.mp3", M + "Los Inventados - Disco Uno\\02 Tono Mi.mp3"
alto, grave = M + "Grupo Prueba - Disco Dos\\01 Tono Alto.mp3", M + "Banda Ficticia - Primer Disco\\01 Pulso Grave.mp3"
video = M + "Videos Inventados\\Barras de Prueba.mp4"
escribe("rocola_stats.txt", ["ROCOLA-STATS v1", "total-reproducciones=9", "total-canciones=8", "total-videos=1", "monedas=12", "creditos-cargados=20",
                             "[CANCIONES] count|ruta", "4|" + la, "2|" + alto, "2|" + grave,
                             "[ARTISTAS] count|artista", "4|Los Inventados", "2|Grupo Prueba", "2|Banda Ficticia",
                             "[VIDEOS] count|ruta", "1|" + video, "[GENEROS] genero|ruta"])
escribe("rocola_teclas.txt", ["arriba=38", "abajo=40", "izquierda=37", "derecha=39", "poner=13", "regresar=27", "moneda=77", "saltar=83"])
escribe("rocola_config.txt", ["noche=1", "noche_pct=80", "dir=D:\\musica"])
# lo que NO se trae: el estado del dia (creditos y cola), un error de XP
escribe("rocola_estado.txt", ["ROCOLA-ESTADO v1", "ts=639296995223737626", "creditos=5"])
escribe("rocola_error.txt", ["2026-11-13 22:00:00  [Algo de XP]  (rocola del 26/10/2026)"])
# los modelos de IA (los del repositorio)
os.makedirs(os.path.join(XP, "IAS"), exist_ok=True)
shutil.copyfile(os.path.join(REPO, "ia", "rocola_ia.bin"), os.path.join(XP, "IAS", "rocola_ia.bin"))
shutil.copyfile(os.path.join(REPO, "pruebas", "datos", "rocola_eq_ia.bin"), os.path.join(XP, "IAS", "rocola_eq_ia.bin"))
# el programa de XP (solo cuenta su largo y su fecha: la identidad de la carpeta)
open(os.path.join(XP, "Rocola.exe"), "wb").write(b"MZ" + b"rocola de XP falsa (prueba en QEMU)" * 100)
os.utime(os.path.join(XP, "Rocola.exe"), ns=(1761500000000000000, 1761500000123456700))
print("carpeta de XP falsa en", XP)
