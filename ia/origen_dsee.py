#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""DE DONDE VIENE CADA EJEMPLO DEL DSEE, Y PARTICIONES QUE NO SE MEZCLAN.

La rocola junta su material en rocola_ia_datos.bin (RIAD v1): cabecera de 16 bytes y registros de
40 float32. Ese formato NO dice de que cancion viene cada registro, y no se puede adivinar por su
posicion. Por eso, desde la version que escribe el indice, la rocola deja al lado
rocola_ia_datos.idx con UNA entrada de 64 bytes por cancion cosechada:

  cabecera (16 bytes): "RIAX", version=1, tamano de entrada=64, 0
  entrada (64 bytes, little-endian):
     0 int64   primer registro          28 uint32 CRC32 de los bytes de esos registros
     8 int32   cuantos registros        32 uint64 huella de la cancion (nombre de archivo + duracion)
    12 int32   version del extractor    40 uint64 huella de la carpeta (el "album" de la rocola)
    16 int32   frecuencia de muestreo   48 uint64 huella del artista (0 = desconocido)
    20 int32   cuadro (FFT)             56 uint32 reservado (0)
    24 int32   canal                    60 uint32 CRC32 de los bytes 0..59 de la entrada

Solo huellas: ni rutas ni nombres en claro. Reglas de lectura (nunca atribuir mal):
  - entrada a medias al final, o con su CRC malo      -> se ignora
  - rango fuera del archivo de datos o que pisa otra  -> se ignora
  - CRC de los datos distinto (archivo cambiado)      -> se ignora
  - registros sin entrada valida                      -> "sin origen": nunca van a validacion ni prueba
"""
import os
import struct
import zlib

import numpy as np

CABECERA_IDX = b"RIAX"
VERSION_IDX = 1
TAM_ENTRADA = 64
TAM_REGISTRO = 160          # 40 float32
CABECERA_DATOS = 16
_ENTRADA = struct.Struct("<qiiiiiIQQQII")
assert _ENTRADA.size == TAM_ENTRADA


# ---------------------------------------------------------------------------------------------
#  Huellas: FNV-1a de 64 bits sobre UTF-8 en minusculas (la rocola hace lo mismo en C#)
# ---------------------------------------------------------------------------------------------
def huella(texto):
    h = 0xCBF29CE484222325
    for b in texto.lower().encode("utf-8"):
        h ^= b
        h = (h * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
    return h if h else 1          # 0 queda reservado para "desconocido"


def huella_cancion(nombre_archivo, duracion_seg):
    """El nombre del archivo (sin carpeta) y la duracion redondeada. Mover la biblioteca de sitio no
    cambia esta huella: la misma cancion cosechada dos veces sigue siendo UN grupo."""
    return huella("%s|%d" % (os.path.basename(nombre_archivo), int(round(duracion_seg))))


def entrada_bytes(primero, cuenta, extractor, rate, frame, canal, crc_datos, cancion, carpeta, artista):
    cuerpo = _ENTRADA.pack(primero, cuenta, extractor, rate, frame, canal, crc_datos,
                           cancion, carpeta, artista, 0, 0)[:60]
    return cuerpo + struct.pack("<I", zlib.crc32(cuerpo) & 0xFFFFFFFF)


def cabecera_idx():
    return CABECERA_IDX + struct.pack("<iii", VERSION_IDX, TAM_ENTRADA, 0)


# ---------------------------------------------------------------------------------------------
#  Lectura del indice
# ---------------------------------------------------------------------------------------------
def lee_indice(ruta_idx, ruta_datos, verificar_datos=True, bloque=4096, avance=None, cada=1 << 30):
    """Devuelve (entradas, informe). entradas es un arreglo estructurado ordenado por primer registro,
    solo con las validas. El recorrido es por bloques: memoria acotada aunque el indice sea enorme, y
    la comprobacion de los datos lee los rangos en orden (lectura secuencial del archivo de datos).
    Comprobar los CRC lee el material ENTERO: con gigas tarda lo que tarde el disco en leerlos, asi que
    avance(leidos, total) se llama cada GiB para que se vea que no esta colgado."""
    informe = {"indice": False, "entradas_leidas": 0, "validas": 0, "crc_entrada_malo": 0,
               "cola_incompleta": 0, "fuera_de_rango": 0, "solapadas": 0, "crc_datos_malo": 0,
               "cabecera_mala": False, "registros_totales": 0, "registros_con_origen": 0}
    tipo = np.dtype([("primero", "<i8"), ("cuenta", "<i4"), ("extractor", "<i4"), ("rate", "<i4"),
                     ("frame", "<i4"), ("canal", "<i4"), ("crc", "<u4"), ("cancion", "<u8"),
                     ("carpeta", "<u8"), ("artista", "<u8")])
    try:
        tam_datos = os.path.getsize(ruta_datos)
    except OSError:
        return np.zeros(0, dtype=tipo), informe
    n_reg = max(0, (tam_datos - CABECERA_DATOS) // TAM_REGISTRO)
    informe["registros_totales"] = int(n_reg)
    if not ruta_idx or not os.path.exists(ruta_idx):
        return np.zeros(0, dtype=tipo), informe
    filas = []
    with open(ruta_idx, "rb") as f:
        cab = f.read(16)
        if len(cab) < 16 or cab[:4] != CABECERA_IDX or struct.unpack("<iii", cab[4:16])[:2] != (VERSION_IDX, TAM_ENTRADA):
            informe["cabecera_mala"] = True
            return np.zeros(0, dtype=tipo), informe
        informe["indice"] = True
        while True:
            crudo = f.read(TAM_ENTRADA * bloque)
            if not crudo:
                break
            completas = len(crudo) // TAM_ENTRADA
            if len(crudo) % TAM_ENTRADA:
                informe["cola_incompleta"] += 1
            for k in range(completas):
                e = crudo[k * TAM_ENTRADA:(k + 1) * TAM_ENTRADA]
                informe["entradas_leidas"] += 1
                if zlib.crc32(e[:60]) & 0xFFFFFFFF != struct.unpack("<I", e[60:])[0]:
                    informe["crc_entrada_malo"] += 1
                    continue
                v = _ENTRADA.unpack(e)
                primero, cuenta = v[0], v[1]
                if primero < 0 or cuenta <= 0 or primero + cuenta > n_reg:
                    informe["fuera_de_rango"] += 1
                    continue
                filas.append(v[:10])
    ent = np.array(filas, dtype=tipo) if filas else np.zeros(0, dtype=tipo)
    if len(ent):
        ent = ent[np.argsort(ent["primero"], kind="stable")]
        # Una entrada que pisa a la anterior no se puede atribuir: se descartan las dos partes dudosas.
        fin = ent["primero"] + ent["cuenta"]
        pisa = np.zeros(len(ent), dtype=bool)
        pisa[1:] = ent["primero"][1:] < np.maximum.accumulate(fin)[:-1]
        pisa[:-1] |= pisa[1:]
        informe["solapadas"] = int(pisa.sum())
        ent = ent[~pisa]
    if verificar_datos and len(ent):
        buenas = np.ones(len(ent), dtype=bool)
        total = int(ent["cuenta"].sum()) * TAM_REGISTRO
        leidos, aviso = 0, cada
        with open(ruta_datos, "rb") as f:
            for i in range(len(ent)):
                f.seek(CABECERA_DATOS + int(ent["primero"][i]) * TAM_REGISTRO)
                crc, falta = 0, int(ent["cuenta"][i]) * TAM_REGISTRO
                while falta > 0:
                    trozo = f.read(min(falta, 1 << 20))
                    if not trozo:
                        break
                    crc = zlib.crc32(trozo, crc)
                    falta -= len(trozo)
                    leidos += len(trozo)
                    if avance is not None and leidos >= aviso:
                        avance(leidos, total)
                        aviso += cada
                if falta or (crc & 0xFFFFFFFF) != int(ent["crc"][i]):
                    buenas[i] = False
        informe["crc_datos_malo"] = int((~buenas).sum())
        ent = ent[buenas]
    informe["validas"] = int(len(ent))
    informe["registros_con_origen"] = int(ent["cuenta"].sum()) if len(ent) else 0
    return ent, informe


# ---------------------------------------------------------------------------------------------
#  Grupos: union de cancion, carpeta y artista
# ---------------------------------------------------------------------------------------------
def _componentes(claves_por_elemento):
    """Union-find: elementos que comparten cualquier clave no nula quedan en el mismo grupo.
    Devuelve un arreglo con la etiqueta de grupo (la clave mas pequena del componente, estable)."""
    padre = {}

    def raiz(x):
        while padre[x] != x:
            padre[x] = padre[padre[x]]
            x = padre[x]
        return x

    for claves in claves_por_elemento:
        claves = [c for c in claves if c]
        for c in claves:
            padre.setdefault(c, c)
        for c in claves[1:]:
            a, b = raiz(claves[0]), raiz(c)
            if a != b:
                if a < b:
                    padre[b] = a
                else:
                    padre[a] = b
    return np.array([raiz([c for c in claves if c][0]) for claves in claves_por_elemento], dtype=np.uint64)


NIVELES = ("artista", "carpeta", "cancion")


def pesos_equilibrados(canciones, grupos=None, limite=4.0):
    """Solo identidades de TRAIN: igual masa por grupo y por cancion dentro del grupo.

    Recosechas/canales de la misma cancion comparten masa. No consulta X, Y ni otras
    particiones. Identidad 0 no se inventa: esas filas conservan peso 1. El recorte
    limita extremos a [1/limite, limite], con media 1 tambien despues de recortar.
    """
    c = np.asarray(canciones)
    g = c if grupos is None else np.asarray(grupos)
    if (c.ndim != 1 or g.shape != c.shape or c.dtype.kind not in "ui" or
            g.dtype.kind not in "ui" or np.any(c < 0) or np.any(g < 0) or
            not np.isfinite(limite) or not 1 <= limite <= 8):
        raise ValueError("Identidades/presupuesto de equilibrio invalidos")
    c, g = c.astype(np.uint64), g.astype(np.uint64)
    conocido = c != 0
    pesos = np.ones(len(c), dtype=np.float64)
    if conocido.any():
        cc, gg = c[conocido], g[conocido].copy()
        gg[gg == 0] = cc[gg == 0]
        canciones_unicas, primero, inv, cuenta = np.unique(cc, return_index=True, return_inverse=True, return_counts=True)
        grupo_cancion = gg[primero]
        if np.any(gg != grupo_cancion[inv]):
            raise ValueError("Una cancion no puede pertenecer a grupos distintos")
        grupos_unicos, grupo_inv = np.unique(grupo_cancion, return_inverse=True)
        canciones_grupo = np.bincount(grupo_inv)
        bruto = len(cc) / (len(grupos_unicos) * canciones_grupo[grupo_inv[inv]] * cuenta[inv])
        # Buscar una escala sin perder la media 1 ni romper los limites por normalizar.
        abajo, arriba = 0.0, limite / float(bruto.min())
        for _ in range(48):
            escala = (abajo + arriba) / 2
            if np.clip(bruto * escala, 1 / limite, limite).mean() < 1:
                abajo = escala
            else:
                arriba = escala
        pesos[conocido] = np.clip(bruto * ((abajo + arriba) / 2), 1 / limite, limite)
        nc, ng = len(canciones_unicas), len(grupos_unicos)
    else:
        nc = ng = 0
    return pesos, {"metodo": "grupo-cancion-acotado-v1", "filas": len(c),
                   "canciones": nc, "grupos": ng, "sin_identidad": int((~conocido).sum()),
                   "limite_factor": float(limite), "peso_min": float(pesos.min()) if len(c) else 1.0,
                   "peso_max": float(pesos.max()) if len(c) else 1.0,
                   "peso_medio": float(pesos.mean()) if len(c) else 1.0}


def grupos(entradas, nivel="artista", tope_fraccion=0.5):
    """Etiqueta de grupo por entrada. nivel='artista' une cancion+carpeta+artista; 'carpeta' une
    cancion+carpeta; 'cancion' solo la cancion. Si un grupo se come mas de tope_fraccion de los
    ejemplos (p. ej. una biblioteca entera en una sola carpeta), se baja un nivel y se informa.
    Devuelve (etiquetas, nivel_usado)."""
    if len(entradas) == 0:
        return np.zeros(0, dtype=np.uint64), nivel
    niveles = NIVELES[NIVELES.index(nivel):]
    peso = entradas["cuenta"].astype(np.int64)
    for nv in niveles:
        claves = []
        for e in entradas:
            k = [int(e["cancion"])]
            if nv in ("artista", "carpeta"):
                k.append(int(e["carpeta"]) ^ 0x9E3779B97F4A7C15 if e["carpeta"] else 0)
            if nv == "artista":
                k.append(int(e["artista"]) ^ 0x7F4A7C159E3779B9 if e["artista"] else 0)
            claves.append(k)
        et = _componentes(claves)
        _, inv = np.unique(et, return_inverse=True)
        mayor = np.bincount(inv, weights=peso).max() / max(1, peso.sum())
        if mayor <= tope_fraccion or nv == "cancion":
            return et, nv
    return et, "cancion"


# ---------------------------------------------------------------------------------------------
#  Particiones por grupo: entrenamiento / validacion (elige checkpoint) / prueba (solo decide)
# ---------------------------------------------------------------------------------------------
PARTES = ("entrenamiento", "validacion", "prueba")


def _mezcla64(x, semilla):
    x = (int(x) ^ (semilla * 0x9E3779B97F4A7C15)) & 0xFFFFFFFFFFFFFFFF
    x ^= x >> 33; x = (x * 0xFF51AFD7ED558CCD) & 0xFFFFFFFFFFFFFFFF
    x ^= x >> 33; x = (x * 0xC4CEB9FE1A85EC53) & 0xFFFFFFFFFFFFFFFF
    x ^= x >> 33
    return x


def reparte(etiquetas, pesos, fracciones=(0.8, 0.1, 0.1), semilla=20260922,
            min_grupos=(1, 3, 3), min_ejemplos=(500, 200, 200)):
    """Asigna cada GRUPO entero a una particion (0 entrenamiento, 1 validacion, 2 prueba).

    Los grupos se recorren en un orden mezclado de forma determinista (depende solo de su etiqueta y
    de la semilla, no de su contenido) y se van dando a prueba y luego a validacion hasta cubrir su
    fraccion de ejemplos Y su minimo de grupos; el resto, a entrenamiento. Con pocos grupos un sorteo
    por grupo dejaba particiones de uno solo; asi no. Devuelve (particion_por_elemento, motivo);
    motivo != "" significa que no hay reparto independiente suficiente (y la particion es None)."""
    etiquetas = np.asarray(etiquetas, dtype=np.uint64)
    pesos = np.asarray(pesos, dtype=np.int64)
    if len(etiquetas) == 0:
        return None, "no hay ejemplos con origen conocido"
    unicos, inv = np.unique(etiquetas, return_inverse=True)
    if len(unicos) < sum(min_grupos):
        return None, "solo hay %d grupos independientes; hacen falta al menos %d" % (len(unicos), sum(min_grupos))
    peso_g = np.bincount(inv, weights=pesos, minlength=len(unicos))
    total = float(peso_g.sum())
    orden = sorted(range(len(unicos)), key=lambda g: (_mezcla64(unicos[g], semilla), int(unicos[g])))
    fr = np.asarray(fracciones, dtype=np.float64) / float(sum(fracciones))
    parte_grupo = np.zeros(len(unicos), dtype=np.int64)
    cursor = 0
    for p in (2, 1):
        acum, n = 0.0, 0
        while cursor < len(orden) and (acum < fr[p] * total or n < min_grupos[p]):
            g = orden[cursor]
            parte_grupo[g] = p
            acum += peso_g[g]; n += 1; cursor += 1
    parte = parte_grupo[inv]
    for p in range(3):
        ng = int(np.sum(parte_grupo == p))
        ne = int(pesos[parte == p].sum())
        if ng < min_grupos[p] or ne < min_ejemplos[p]:
            return None, ("la particion de %s se queda con %d grupos y %d ejemplos (minimo %d y %d)"
                          % (PARTES[p], ng, ne, min_grupos[p], min_ejemplos[p]))
    return parte, ""


def filas_de(entradas, parte_por_entrada, filas):
    """Dadas las filas (indices de registro) que se van a usar, devuelve una lista de 3 arreglos con las
    filas de cada particion y otro con las filas SIN ORIGEN. Busqueda binaria: memoria O(filas)."""
    filas = np.asarray(filas, dtype=np.int64)
    if len(entradas) == 0:
        return [filas[:0]] * 3, filas
    ini = entradas["primero"].astype(np.int64)
    fin = ini + entradas["cuenta"].astype(np.int64)
    k = np.searchsorted(ini, filas, side="right") - 1
    dentro = (k >= 0) & (filas < fin[np.maximum(k, 0)])
    partes = []
    for p in range(3):
        sel = dentro & (parte_por_entrada[np.maximum(k, 0)] == p)
        partes.append(filas[sel])
    return partes, filas[~dentro]


def huella_datos(ruta_datos, ruta_idx=None):
    """Huella reproducible del material SIN leer gigas: tamano, cabecera, primer y ultimo MiB, e indice."""
    import hashlib
    h = hashlib.sha256()
    if not os.path.exists(ruta_datos):
        return None
    tam = os.path.getsize(ruta_datos)
    h.update(struct.pack("<q", tam))
    with open(ruta_datos, "rb") as f:
        h.update(f.read(1 << 20))
        if tam > (1 << 20):
            f.seek(max(0, tam - (1 << 20)))
            h.update(f.read(1 << 20))
    if ruta_idx and os.path.exists(ruta_idx):
        with open(ruta_idx, "rb") as f:
            while True:
                b = f.read(1 << 20)
                if not b:
                    break
                h.update(b)
    return h.hexdigest()


def ruta_indice(ruta_datos):
    base = ruta_datos[:-4] if ruta_datos.lower().endswith(".bin") else ruta_datos
    return base + ".idx"
