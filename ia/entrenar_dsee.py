#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ENTRENA TODO EL DSEE DE UNA SOLA VEZ, CON LO QUE LA ROCOLA JUNTO SOLA.

Esto es el motor; lo normal es no llamarlo a mano sino ARRASTRAR los archivos sobre
`entrenar_dsee.bat`, que hace lo mismo y ademas busca Python por su cuenta.

QUE HAY QUE TRAERSE DE LA ROCOLA (los dos archivos estan JUNTO a Rocola.exe)

    rocola_ia_datos.bin   el material del reconstructor de agudos (lo mas gordo; puede pesar gigas)
    rocola_eq.txt         la huella tonal de cada cancion, para el EQ automatico (unos pocos MB)

No hace falta traerse nada mas: ni la musica, ni el resto de archivos de la rocola. Se pueden
arrastrar los dos, uno solo, o la CARPETA entera donde esten (se buscan dentro).

QUE SALE

    rocola_ia.bin         el reconstructor de agudos del DSEE, aprendido de TU musica
    rocola_eq_ia.bin      el objetivo del EQ automatico, por cancion en vez de uno para todas

Los dos se dejan en la carpeta `entrenado\\` y solo hay que copiarlos junto a Rocola.exe.

QUE **NO** SE ENTRENA (porque no es una red, son medidas que la rocola ya lleva sola)

    - el volumen parejo (rocola_volumen.txt)
    - la FUERZA del EQ automatico y la cantidad de graves: salen de reglas sobre esas medidas
    - el des-recorte, el realce y el fundido: ajustes fijos, no aprendidos
"""

import os, struct, subprocess, sys

AQUI = os.path.dirname(os.path.abspath(__file__))
SALIDA = os.path.join(AQUI, "entrenado")


def raya(t=""):
    print(t)


def titulo(t):
    raya()
    raya("=" * 74)
    raya("  " + t)
    raya("=" * 74)


def mira_que_es(ruta):
    """Que archivo es este, MIRANDO DENTRO y no fiandose del nombre (se pueden renombrar)."""
    try:
        with open(ruta, "rb") as f:
            cab = f.read(16)
    except Exception:
        return "ilegible"
    if cab[:4] == b"RIAD":
        return "datos_dsee"          # material cosechado del reconstructor
    if cab[:4] == b"RIA1":
        return "modelo"              # un modelo YA entrenado
    # Texto: la huella tonal son lineas "b0;b1;...;corte|ruta"
    try:
        with open(ruta, "r", encoding="utf-8", errors="replace") as f:
            for _ in range(40):
                linea = f.readline()
                if not linea:
                    break
                barra = linea.find("|")
                if barra <= 0:
                    continue
                trozos = linea[:barra].split(";")
                if len(trozos) >= 8:
                    try:
                        for t in trozos[:8]:
                            float(t)
                        return "datos_eq"
                    except ValueError:
                        pass
    except Exception:
        pass
    return "otro"


def junta(paths):
    """Clasifica todo lo que se arrastro. Si es una carpeta, mira dentro (y un nivel mas)."""
    datos_dsee, datos_eq, otros = [], [], []
    pendientes = list(paths)
    vistos = set()
    while pendientes:
        p = os.path.abspath(pendientes.pop(0))
        if p in vistos:
            continue
        vistos.add(p)
        if os.path.isdir(p):
            try:
                for n in sorted(os.listdir(p)):
                    h = os.path.join(p, n)
                    if os.path.isfile(h) or os.path.isdir(h):
                        pendientes.append(h)
            except Exception:
                pass
            continue
        if not os.path.isfile(p):
            continue
        que = mira_que_es(p)
        if que == "datos_dsee":
            datos_dsee.append(p)
        elif que == "datos_eq":
            datos_eq.append(p)
        else:
            otros.append((p, que))
    return datos_dsee, datos_eq, otros


def tam(ruta):
    n = os.path.getsize(ruta)
    if n >= 1024 ** 3:
        return "%.1f GB" % (n / 1024.0 ** 3)
    if n >= 1024 ** 2:
        return "%.0f MB" % (n / 1024.0 ** 2)
    return "%.0f KB" % (n / 1024.0)


def cuantos_ejemplos(ruta):
    try:
        return max(0, (os.path.getsize(ruta) - 16) // (40 * 4))
    except Exception:
        return 0


def cuantas_canciones(ruta):
    n = 0
    try:
        with open(ruta, "r", encoding="utf-8", errors="replace") as f:
            for linea in f:
                if linea.find("|") > 0:
                    n += 1
    except Exception:
        pass
    return n


def corre(argv, titulo_paso):
    """Devuelve el codigo de salida del entrenador. 0 = guardo modelo; 2 = corrio bien pero decidio
    NO guardar (no le gana al promedio, que es la respuesta honesta y no un fallo); otro = fallo."""
    raya()
    raya("--- " + titulo_paso + " ---")
    raya("    " + " ".join(argv[1:]))
    raya()
    try:
        return subprocess.call([sys.executable] + argv, cwd=AQUI)
    except Exception as e:
        raya("[X] No se pudo lanzar: %s" % e)
        return -1


def guarda_copia(nombre):
    """Aparta la version anterior antes de pisarla: entrenar cuesta horas, perderla seria una pena."""
    orig = os.path.join(AQUI, nombre)
    if not os.path.exists(orig):
        return
    viejo = orig + ".anterior"
    try:
        if os.path.exists(viejo):
            os.remove(viejo)
        os.replace(orig, viejo)
    except Exception:
        pass


def devuelve_copia(nombre):
    """Si el entrenamiento no dejo modelo nuevo, se vuelve a poner el de antes. Sin esto, un intento
    fallido -o uno que decide honestamente no guardar- dejaria al dueno SIN el modelo que ya tenia."""
    orig = os.path.join(AQUI, nombre)
    viejo = orig + ".anterior"
    if os.path.exists(orig) or not os.path.exists(viejo):
        return False
    try:
        os.replace(viejo, orig)
        return True
    except Exception:
        return False


def lleva_a_salida(nombre):
    orig = os.path.join(AQUI, nombre)
    if not os.path.exists(orig):
        return None
    try:
        os.makedirs(SALIDA, exist_ok=True)
        destino = os.path.join(SALIDA, nombre)
        with open(orig, "rb") as a, open(destino, "wb") as b:
            b.write(a.read())
        return destino
    except Exception:
        return None


def main(argv):
    titulo("ENTRENAR EL DSEE DE LA ROCOLA")
    try:
        import numpy  # noqa: F401
    except Exception:
        raya("[X] Falta numpy, que es lo unico que hace falta ademas de Python.")
        raya("    Instalalo con:   pip install numpy")
        return 2

    if not argv:
        raya("  Arrastra sobre el .bat los DOS archivos que la rocola deja junto a Rocola.exe:")
        raya()
        raya("     rocola_ia_datos.bin    (el reconstructor de agudos)")
        raya("     rocola_eq.txt          (el EQ automatico)")
        raya()
        raya("  Tambien vale arrastrar la carpeta entera donde esten.")
        raya("  Si solo traes uno, se entrena solo esa mitad y se avisa de lo que falto.")
        return 1

    datos_dsee, datos_eq, otros = junta(argv)

    raya("  LO QUE ME TRAJISTE")
    raya("  " + "-" * 70)
    for p in datos_dsee:
        raya("   [reconstructor] %-34s %10s   %s ejemplos"
             % (os.path.basename(p), tam(p), format(cuantos_ejemplos(p), ",")))
    for p in datos_eq:
        raya("   [EQ automatico] %-34s %10s   %s canciones medidas"
             % (os.path.basename(p), tam(p), format(cuantas_canciones(p), ",")))
    for p, que in otros:
        pista = "ya es un modelo entrenado, no material" if que == "modelo" else "no me sirve para entrenar"
        raya("   [ni idea]       %-34s %10s   (%s)" % (os.path.basename(p), tam(p), pista))
    if not datos_dsee and not datos_eq:
        raya()
        raya("[X] Ninguno de esos archivos es material de entrenamiento.")
        raya("    Busca junto a Rocola.exe:  rocola_ia_datos.bin  y  rocola_eq.txt")
        return 1

    hechos, faltaron = [], []

    # ---------------- 1) EL RECONSTRUCTOR DE AGUDOS (el DSEE propiamente dicho) ----------------
    if datos_dsee:
        mayor = max(datos_dsee, key=os.path.getsize)   # si hay varios, el que mas material trae
        n = cuantos_ejemplos(mayor)
        if n < 100000:
            raya()
            raya("  [!] Solo hay %s ejemplos para el reconstructor. Se puede entrenar, pero con tan poco" % format(n, ","))
            raya("      la red aprende de memoria en vez de aprender de verdad. Deja la rocola encendida")
            raya("      unos dias mas, o enciende el REPASO A FONDO en Ctrl+F3 una noche.")
        guarda_copia("rocola_ia.bin")
        guarda_copia("ia_vectores_prueba.txt")
        cod = corre(["entrenar_ia.py", "--datos", mayor, "--capas", "28,96,96,12"],
                    "PASO 1: el reconstructor de agudos (rocola_ia.bin)")
        if cod == 0 and lleva_a_salida("rocola_ia.bin"):
            hechos.append(("rocola_ia.bin", "reconstruye los agudos que el MP3 corto"))
        else:
            faltaron.append("el reconstructor de agudos no se pudo entrenar (mira el mensaje de arriba)")
            if devuelve_copia("rocola_ia.bin"):
                devuelve_copia("ia_vectores_prueba.txt")
                faltaron.append("se dejo puesto el rocola_ia.bin que ya tenias, sin tocar")
    else:
        faltaron.append("no trajiste rocola_ia_datos.bin: sin el no se puede entrenar el reconstructor")

    # ---------------- 2) EL OBJETIVO DEL EQ AUTOMATICO ----------------
    if datos_eq:
        mayor = max(datos_eq, key=os.path.getsize)
        n = cuantas_canciones(mayor)
        if n < 200:
            raya()
            raya("  [!] Solo hay %s canciones medidas. El entrenador del EQ pide al menos 200 SANAS" % format(n, ","))
            raya("      (con sus agudos completos) y probablemente te dira que no. No pasa nada: la rocola")
            raya("      sigue usando el promedio de la biblioteca, como siempre.")
        guarda_copia("rocola_eq_ia.bin")
        cod = corre(["entrenar_eq.py", "--datos", mayor, "--salida", "rocola_eq_ia.bin"],
                    "PASO 2: el objetivo del EQ automatico (rocola_eq_ia.bin)")
        if cod == 0 and lleva_a_salida("rocola_eq_ia.bin"):
            hechos.append(("rocola_eq_ia.bin", "le pone a cada cancion su propio objetivo de EQ"))
        elif cod == 2:
            # El entrenador compara contra el promedio sobre canciones que la red nunca vio, y si no le
            # gana por un margen claro NO guarda nada. Eso NO es un fallo: es la respuesta honesta.
            faltaron.append("el EQ automatico no le gano al promedio, asi que -bien hecho- no se guardo "
                            "modelo: la rocola sigue con el promedio, que para tu biblioteca es igual de "
                            "bueno. Con mas canciones analizadas puede cambiar; vuelve a intentarlo mas "
                            "adelante")
            devuelve_copia("rocola_eq_ia.bin")
        else:
            faltaron.append("el EQ automatico no se pudo entrenar (mira el mensaje de arriba)")
            if devuelve_copia("rocola_eq_ia.bin"):
                faltaron.append("se dejo puesto el rocola_eq_ia.bin que ya tenias, sin tocar")
    else:
        faltaron.append("no trajiste rocola_eq.txt: sin el no se puede entrenar el EQ automatico")

    # ---------------- resumen ----------------
    titulo("RESUMEN")
    if hechos:
        raya("  LISTO. Copia estos archivos JUNTO a Rocola.exe y reinicia la rocola:")
        raya()
        for nombre, para_que in hechos:
            raya("     %-20s  %s" % (nombre, para_que))
        raya()
        raya("  Estan en:  " + SALIDA)
        raya()
        raya("  Para comprobar que la rocola los tomo: Ctrl+F3, en la tarjeta CALIDAD DEL SONIDO")
        raya("  la nota del reconstructor dira \"con IA PROPIA\".")
    else:
        raya("  No se genero ningun modelo.")
    if faltaron:
        raya()
        raya("  LO QUE NO SALIO")
        for f in faltaron:
            raya("     - " + f)
    raya()
    return 0 if hechos else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
