#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ENTRENA TODO EL DSEE DE UNA SOLA VEZ, CON LO QUE LA ROCOLA JUNTO SOLA.

Esto es el motor; lo normal es no llamarlo a mano sino ARRASTRAR los archivos sobre
`entrenar_dsee.bat`, que hace lo mismo y ademas busca Python por su cuenta.

QUE HAY QUE TRAERSE DE LA ROCOLA (los dos archivos estan JUNTO a Rocola.exe)

    rocola_ia_datos.bin   el material del reconstructor de agudos (lo mas gordo; puede pesar gigas)
    rocola_ia_datos.idx   de que cancion viene cada ejemplo (pequeno; sin el no se puede validar)
    rocola_eq.txt         la huella tonal de cada cancion, para el EQ automatico (unos pocos MB)

No hace falta traerse nada mas: ni la musica, ni el resto de archivos de la rocola. Se pueden
arrastrar los dos, uno solo, o la CARPETA entera donde esten (se buscan dentro).

QUE SALE

    entrenado/dsee-*/candidato.bin       reconstructor y su informe
    entrenado/eq-*/candidato_eq.bin      objetivo EQ si supera el guardian

Son candidatos aislados: NO sustituir instalados sin validacion PCM y escucha autorizada.
Llevar tambien .idx junto al .bin y el sidecar opcional .generos.tsv, conservando el nombre base.

QUE **NO** SE ENTRENA (porque no es una red, son medidas que la rocola ya lleva sola)

    - el volumen parejo (rocola_volumen.txt)
    - la FUERZA del EQ automatico y la cantidad de graves: salen de reglas sobre esas medidas
    - el des-recorte, el realce y el fundido: ajustes fijos, no aprendidos
"""

import os, struct, subprocess, sys, tempfile
from collections import deque

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
    if cab[:4] == b"RIAX":
        return "indice_dsee"         # de que cancion viene cada ejemplo del material
    if os.path.basename(ruta).lower().endswith(".generos.tsv"):
        return "generos_dsee"        # metadata opcional; nunca son objetivos ni rutas de musica
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


# Musica y video: NUNCA son material de entrenamiento. Se saltan sin abrirlos siquiera, porque si se
# arrastra la carpeta de la rocola entera, dentro esta la biblioteca: abrir decenas de miles de canciones
# una por una para mirarles los primeros bytes tiene al disco moliendo un buen rato y parece que se colgo.
MUSICA = (".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".wma", ".opus",
          ".mp4", ".avi", ".mkv", ".wmv", ".mov", ".mpg", ".mpeg", ".vob", ".flv",
          ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp")

HONDO = 1   # cuantos niveles se baja dentro de una carpeta arrastrada


def junta(paths):
    """Clasifica todo lo que se arrastro. Si es una carpeta, mira dentro (y un nivel mas, no la biblioteca
    entera: bajar sin freno significaba recorrer disco por disco toda la musica)."""
    datos_dsee, datos_eq, otros = [], [], []
    pendientes = deque((p, 0) for p in paths)
    vistos = set()
    saltados = 0
    while pendientes:
        crudo, hondo = pendientes.popleft()
        p = os.path.abspath(crudo)
        if p in vistos:
            continue
        vistos.add(p)
        if os.path.isdir(p):
            if hondo > HONDO:
                continue
            try:
                for n in sorted(os.listdir(p)):
                    h = os.path.join(p, n)
                    if os.path.isdir(h):
                        pendientes.append((h, hondo + 1))
                    elif os.path.isfile(h):
                        pendientes.append((h, hondo))
            except Exception:
                pass
            continue
        if not os.path.isfile(p):
            continue
        if p.lower().endswith(MUSICA):
            saltados += 1
            continue
        que = mira_que_es(p)
        if que == "datos_dsee":
            datos_dsee.append(p)
        elif que == "datos_eq":
            datos_eq.append(p)
        else:
            otros.append((p, que))
    return datos_dsee, datos_eq, otros, saltados


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
    """0/4 = candidato valido aislado; 2 = rechazado; 3 = no verificable; otro = fallo."""
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

    raya("  LO QUE ME TRAJISTE")
    raya("  " + "-" * 70)
    for a in argv:
        raya("   arrastrado:     " + a + ("" if os.path.exists(a) else "   [X] NO EXISTE"))
    raya()
    datos_dsee, datos_eq, otros, saltados = junta(argv)
    otros_todos = list(otros)
    if saltados:
        raya("   (me salte %s archivos de musica, video o imagen: esos nunca son material)" % format(saltados, ","))
    for p in datos_dsee:
        raya("   [reconstructor] %-34s %10s   %s ejemplos"
             % (os.path.basename(p), tam(p), format(cuantos_ejemplos(p), ",")))
    for p in datos_eq:
        raya("   [EQ automatico] %-34s %10s   %s canciones medidas"
             % (os.path.basename(p), tam(p), format(cuantas_canciones(p), ",")))
    for p, que in otros:
        if que == "indice_dsee":
            raya("   [origen]        %-34s %10s   de que cancion viene cada ejemplo" % (os.path.basename(p), tam(p)))
    otros = [(p, q) for p, q in otros if q not in ("indice_dsee", "generos_dsee")]
    for p, que in otros[:12]:
        pista = "ya es un modelo entrenado, no material" if que == "modelo" else "no me sirve para entrenar"
        raya("   [ni idea]       %-34s %10s   (%s)" % (os.path.basename(p), tam(p), pista))
    if len(otros) > 12:
        raya("   [ni idea]       ...y %s archivos mas que tampoco sirven" % format(len(otros) - 12, ","))
    if not datos_dsee and not datos_eq:
        raya()
        raya("[X] Ninguno de esos archivos es material de entrenamiento.")
        raya("    Busca junto a Rocola.exe:  rocola_ia_datos.bin  y  rocola_eq.txt")
        return 1

    candidatos, faltaron = [], []

    # ---------------- 1) EL RECONSTRUCTOR DE AGUDOS (el DSEE propiamente dicho) ----------------
    if datos_dsee:
        mayor = max(datos_dsee, key=os.path.getsize)   # si hay varios, el que mas material trae
        n = cuantos_ejemplos(mayor)
        if n < 100000:
            raya()
            raya("  [!] Solo hay %s ejemplos para el reconstructor. Se puede entrenar, pero con tan poco" % format(n, ","))
            raya("      la red aprende de memoria en vez de aprender de verdad. Deja la rocola encendida")
            raya("      unos dias mas, o enciende el REPASO A FONDO en Ctrl+F3 una noche.")
        idx = mayor[:-4] + ".idx" if mayor.lower().endswith(".bin") else mayor + ".idx"
        # Ningun entrenamiento instala ni recupera modelos: solo crea un candidato aislado.
        # El modelo que el dueno tiene PUESTO: si lo arrastro junto al material, el candidato se compara
        # con ese; si no, con el que haya en esta carpeta. El instalado nunca se reemplaza aqui.
        instalados = [p for p, q in otros_todos if q == "modelo" and os.path.basename(p).lower() == "rocola_ia.bin"]
        extra = ["--instalado", instalados[0]] if instalados else []
        for tipo, extension, opcion in (("indice_dsee", ".idx", "--indice"),
                                       ("generos_dsee", ".generos.tsv", "--generos")):
            metadata = [p for p,q in otros_todos if q == tipo]
            esperada = os.path.splitext(mayor)[0] + extension
            elegida = next((p for p in metadata if os.path.basename(p).lower()==os.path.basename(esperada).lower()),
                          metadata[0] if len(metadata)==1 else None)
            if os.path.exists(esperada): elegida=esperada
            if elegida: extra += [opcion,elegida]
            elif metadata: raya("  [!] Metadata ambigua; conserva nombre base y arrastra el par correcto: "+opcion)
        if not os.path.exists(idx) and "--indice" not in extra:
            raya("  [!] No hay indice junto al .bin ni arrastrado: candidato diagnostico sin validacion independiente.")
            raya("      Las rocolas nuevas escriben .idx al cosechar; trae .bin y .idx juntos.")
        os.makedirs(SALIDA, exist_ok=True)
        destino_dsee = tempfile.mkdtemp(prefix="dsee-", dir=SALIDA)
        cod = corre(["entrenar_ia.py", "--datos", mayor, "--capas", "28,96,96,12",
                     "--candidato", os.path.join(destino_dsee, "candidato.bin"),
                     "--informe", os.path.join(destino_dsee, "informe_dsee.json")] + extra,
                    "PASO 1: el reconstructor de agudos (rocola_ia.bin)")
        if cod in (0, 4):
            candidatos.append(("DSEE", destino_dsee))
        elif cod == 2:
            faltaron.append("el DSEE no supero el control de calidad (curva fija y modelo puesto): se conserva el "
                            "modelo anterior; no es un fallo del entrenamiento. Mira informe_dsee.json")
        elif cod == 3:
            faltaron.append("el DSEE no se pudo VERIFICAR: el material no dice de que cancion viene cada ejemplo "
                            "(falta rocola_ia_datos.idx, o tiene muy pocas canciones), asi que no hay validacion "
                            "independiente. Se conserva el modelo anterior. Deja que la rocola nueva coseche unos "
                            "dias (o una noche de REPASO A FONDO) y trae los DOS archivos: .bin e .idx")
        else:
            faltaron.append("el reconstructor de agudos no se pudo entrenar (mira el mensaje de arriba); "
                            "el rocola_ia.bin que ya tenias sigue puesto, sin tocar")
        # ---------- 1b) EL DSEE POR GENERO (I3, 13-10-2026) ----------
        # Con el sidecar de generos, un candidato por cada genero con material de sobra (al menos 60 canciones; como
        # mucho los 6 con mas). Cada uno se compara contra el modelo general en la prueba de SU genero: solo sale
        # aceptado si le gana ahi. Quedan aislados; se instalan con instalar_dsee_genero.py tras escucharlos.
        sidecar = None
        if "--generos" in extra: sidecar = extra[extra.index("--generos") + 1]
        elif os.path.exists(os.path.splitext(mayor)[0] + ".generos.tsv"): sidecar = os.path.splitext(mayor)[0] + ".generos.tsv"
        if sidecar and cod in (0, 2, 4):
            import entrenar_ia as T
            cuenta = {}
            for g in T.lee_generos(mayor, sidecar).values():
                slug = T.slug_genero(g)
                if slug: cuenta[slug] = cuenta.get(slug, 0) + 1
            elegidos = sorted((g for g in cuenta if cuenta[g] >= 60), key=lambda g: -cuenta[g])[:6]
            if not elegidos:
                mas = sorted(cuenta, key=lambda g: -cuenta[g])[:5]
                faltaron.append("DSEE por genero: ningun genero tiene 60 canciones o mas con material; no se entreno ninguno"
                                + (" (los que mas: " + ", ".join("%s %d" % (g, cuenta[g]) for g in mas) + ")" if mas
                                   else " (ninguna cancion con genero en la etiqueta)"))
            for g in elegidos:
                destino_g = tempfile.mkdtemp(prefix="dsee-genero-%s-" % g, dir=SALIDA)
                cod_g = corre(["entrenar_ia.py", "--datos", mayor, "--capas", "28,96,96,12", "--genero", g,
                               "--candidato", os.path.join(destino_g, "candidato.bin"),
                               "--informe", os.path.join(destino_g, "informe_dsee.json")] + extra,
                              "PASO 1b: el DSEE solo para '%s' (%d canciones)" % (g, cuenta[g]))
                if cod_g in (0, 4):
                    candidatos.append(("DSEE " + g, destino_g))
                else:
                    faltaron.append("DSEE de '%s': no le gano al general en su genero (o falto material); se sigue usando "
                                    "el general para ese genero" % g)
        elif cod in (0, 2, 4):
            # Antes se saltaba sin decir nada: sin el archivo de generos no hay modelos por genero.
            faltaron.append("DSEE por genero: no se entreno ninguno porque no esta rocola_ia_datos.generos.tsv junto al "
                            "material (la rocola lo escribe al juntar material; traelo con el .bin y el .idx)")
    else:
        faltaron.append("no trajiste rocola_ia_datos.bin: sin el no se puede entrenar el reconstructor")

    # ---------------- 2) EL OBJETIVO DEL EQ AUTOMATICO ----------------
    if datos_eq:
        mayor = max(datos_eq, key=os.path.getsize)
        n = cuantas_canciones(mayor)
        if n < 200:
            raya()
            raya("  [!] Solo hay %s canciones medidas. Se entrena igual, pero con tan poco la red no suele" % format(n, ","))
            raya("      ganarle al promedio y lo mas probable es que decida no guardar nada. No pasa nada:")
            raya("      el modelo EQ instalado se conserva; si no existe, sigue el promedio de la biblioteca.")
        instalados = [p for p, q in otros_todos if q == "modelo" and os.path.basename(p).lower() == "rocola_eq_ia.bin"]
        instalado = instalados[0] if instalados else os.path.join(AQUI, "rocola_eq_ia.bin")
        # Cada entrenamiento queda aislado; nunca sustituye modelos sin escucha/PCM final.
        os.makedirs(SALIDA, exist_ok=True)
        destino = tempfile.mkdtemp(prefix="eq-", dir=SALIDA)
        os.rmdir(destino)  # el evaluador exige destino nuevo
        cod = corre(["entrenar_eq_candidato.py", "--datos", mayor, "--instalado", instalado, "--destino", destino],
                    "PASO 2: el objetivo del EQ automatico (rocola_eq_ia.bin)")
        if cod == 0:
            raya("  Candidato EQ validado numericamente en " + destino)
            candidatos.append(("AutoEQ", destino))
        elif cod == 2:
            # Rechazo frente al promedio train/instalado o guard runtime; no se modifica el activo.
            faltaron.append("AutoEQ rechazado por el guardian agrupado frente al promedio y al instalado; "
                            "modelo instalado intacto. Mira informe_eq.json")
        else:
            faltaron.append("el EQ automatico no se pudo entrenar (mira el mensaje de arriba)")
            faltaron.append("modelo EQ instalado intacto; se necesitan huellas limpias v2 y referencia actual")
    else:
        faltaron.append("no trajiste rocola_eq.txt: sin el no se puede entrenar el EQ automatico")

    # ---------------- resumen ----------------
    titulo("RESUMEN")
    if candidatos:
        raya("  CANDIDATOS ENTRENADOS; NO ACTIVADOS:")
        raya()
        for nombre, destino in candidatos:
            raya("     %-12s  %s" % (nombre, destino))
        raya()
        raya("  Estan en:  " + SALIDA)
        raya()
        raya("  Validacion PCM y escucha a nivel igualado pendientes antes de cualquier activacion.")
        if any(n.startswith("DSEE ") for n, _ in candidatos):
            raya("  Los de genero, tras escucharlos: doble clic en instalar_dsee_genero.bat (pregunta uno por uno)")
    else:
        raya("  No hay candidatos aceptados; consulta los informes diagnosticos. Instalados intactos.")
    if faltaron:
        raya()
        raya("  LIMITES Y RESULTADOS DE LAS OTRAS PARTES")
        for f in faltaron:
            raya("     - " + f)
    raya()
    return 0 if candidatos else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
