#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""PUBLICAR EL DSEE SIN DEJAR NUNCA UN CONJUNTO A MEDIAS.

Un modelo del DSEE viaja con sus vectores de paridad (y ahora con su informe). Cada archivo ya se
escribia de forma atomica, pero el CONJUNTO no: un corte de luz entre los dos reemplazos podia dejar
vectores nuevos junto a un modelo viejo. Aqui la publicacion va por versiones:

  modelos/.preparando-XXXX/   se escribe todo, con su manifiesto AL FINAL, y se fuerza a disco
  modelos/dsee-<huella>/      se renombra de una vez: una version completa e inmutable
  modelos/ACTIVO              "dsee-<huella> <sha256 del manifiesto>": se reemplaza de forma atomica
                              y es el UNICO paso que decide que conjunto vale
  modelos/HISTORIAL           las versiones que estuvieron activas, para volver atras
  rocola_ia.bin, ia_vectores_prueba.txt (y la carpeta entrenado/)
                              COPIAS de la version activa. Si una quedo a medias, recuperar() la
                              rehace desde el manifiesto; nunca al reves.

La primera vez, el modelo y los vectores que ya hubiera se ARCHIVAN como una version "previo-..." antes
de tocar nada, para poder volver a ellos. Nada se borra salvo las carpetas .preparando que ningun
puntero nombra.

Uso:   python publicar_dsee.py --estado        que version esta activa y si todo cuadra
       python publicar_dsee.py --recuperar     rehace las copias desde la version activa
       python publicar_dsee.py --activar dsee-XXXX   vuelve a una version guardada
"""
import hashlib
import os
import shutil
import sys
import uuid

CARPETA = "modelos"
ACTIVO = "ACTIVO"
HISTORIAL = "HISTORIAL"
MANIFIESTO = "manifiesto.txt"
CABECERA = "ROCOLA-MANIFIESTO-DSEE 1"
DERIVADOS = ("rocola_ia.bin", "ia_vectores_prueba.txt")


def _paso(nombre):
    """Punto de corte: las pruebas lo sustituyen para simular un apagon en cada paso."""


def _sha(b):
    return hashlib.sha256(b).hexdigest()


def _lee(ruta):
    with open(ruta, "rb") as f:
        return f.read()


def _sync_dir(d):
    try:                                   # en Windows no se puede abrir una carpeta asi: no pasa nada
        fd = os.open(d, os.O_RDONLY)
        try:
            os.fsync(fd)
        finally:
            os.close(fd)
    except OSError:
        pass


def _escribe(ruta, datos, nombre_paso):
    with open(ruta, "wb") as f:
        _paso("escribe:" + nombre_paso)
        f.write(datos)
        f.flush()
        _paso("fsync:" + nombre_paso)
        os.fsync(f.fileno())


def _atomico(ruta, datos, nombre_paso):
    tmp = ruta + ".tmp-" + uuid.uuid4().hex[:8]
    try:
        _escribe(tmp, datos, nombre_paso)
        _paso("reemplaza:" + nombre_paso)
        os.replace(tmp, ruta)
        _sync_dir(os.path.dirname(os.path.abspath(ruta)))
    finally:
        if os.path.exists(tmp):
            os.unlink(tmp)


# ---------------------------------------------------------------------------------------------
#  Manifiesto
# ---------------------------------------------------------------------------------------------
def manifiesto(archivos, extractor, notas=()):
    lineas = [CABECERA, "extractor %d" % extractor]
    for n in notas:
        lineas.append("nota " + n.replace("\n", " "))
    for nombre in sorted(archivos):
        datos = archivos[nombre]
        lineas.append("archivo %s %d %s" % (_sha(datos), len(datos), nombre))
    cuerpo = "\n".join(lineas) + "\n"
    return (cuerpo + "fin %s\n" % _sha(cuerpo.encode("utf-8"))).encode("utf-8")


def lee_manifiesto(datos):
    """Devuelve {nombre: (sha, tamano)} y el extractor, o lanza ValueError si esta incompleto o alterado."""
    texto = datos.decode("utf-8")
    if not texto.endswith("\n") or "\nfin " not in texto:
        raise ValueError("manifiesto incompleto")
    cuerpo, fin = texto[:texto.rindex("fin ")], texto[texto.rindex("fin "):].split()
    if len(fin) != 2 or _sha(cuerpo.encode("utf-8")) != fin[1]:
        raise ValueError("manifiesto alterado")
    lineas = cuerpo.splitlines()
    if not lineas or lineas[0] != CABECERA:
        raise ValueError("no es un manifiesto del DSEE")
    archivos, extractor = {}, None
    for l in lineas[1:]:
        t = l.split(" ", 3)
        if t[0] == "archivo" and len(t) == 4:
            archivos[t[3]] = (t[1], int(t[2]))
        elif t[0] == "extractor":
            extractor = int(t[1])
    return archivos, extractor


def verifica_version(dir_version):
    """(True, "") si todos los archivos del manifiesto estan y cuadran byte a byte."""
    try:
        man = _lee(os.path.join(dir_version, MANIFIESTO))
        archivos, _ = lee_manifiesto(man)
        for nombre, (sha, tam) in archivos.items():
            datos = _lee(os.path.join(dir_version, nombre))
            if len(datos) != tam or _sha(datos) != sha:
                return False, "%s no coincide con su manifiesto" % nombre
        return True, _sha(man)
    except (OSError, ValueError) as e:
        return False, str(e)


# ---------------------------------------------------------------------------------------------
#  Estado, activacion y recuperacion
# ---------------------------------------------------------------------------------------------
def _activo(base):
    try:
        t = _lee(os.path.join(base, CARPETA, ACTIVO)).decode("ascii").split()
        return (t[0], t[1]) if len(t) == 2 else (None, None)
    except (OSError, UnicodeDecodeError):
        return None, None


def _historial(base):
    try:
        return [l.split()[0] for l in _lee(os.path.join(base, CARPETA, HISTORIAL)).decode("ascii").splitlines() if l.strip()]
    except OSError:
        return []


def activar(base, version, destinos=()):
    d = os.path.join(base, CARPETA, version)
    ok, sha_man = verifica_version(d)
    if not ok:
        raise ValueError("la version %s no esta completa: %s" % (version, sha_man))
    anterior, _ = _activo(base)
    if anterior and anterior != version:
        with open(os.path.join(base, CARPETA, HISTORIAL), "ab") as f:
            f.write((anterior + "\n").encode("ascii"))
            f.flush()
            os.fsync(f.fileno())
    _paso("activa")
    _atomico(os.path.join(base, CARPETA, ACTIVO), ("%s %s\n" % (version, sha_man)).encode("ascii"), ACTIVO)
    materializa(base, destinos)
    return version


def materializa(base, destinos=()):
    """Hace que las copias de siempre sean exactamente la version activa. Devuelve cuantas rehizo."""
    version, _ = _activo(base)
    if not version:
        return 0
    d = os.path.join(base, CARPETA, version)
    archivos, _ = lee_manifiesto(_lee(os.path.join(d, MANIFIESTO)))
    rehechas = 0
    for destino in (base,) + tuple(destinos):
        os.makedirs(destino, exist_ok=True)
        for nombre in DERIVADOS:
            if nombre not in archivos:
                continue
            ruta = os.path.join(destino, nombre)
            try:
                actual = _lee(ruta)
            except OSError:
                actual = None
            if actual is None or _sha(actual) != archivos[nombre][0]:
                _atomico(ruta, _lee(os.path.join(d, nombre)), "deriva:%s" % nombre)
                rehechas += 1
    return rehechas


def recuperar(base, destinos=()):
    """Lo primero que se hace antes de entrenar o publicar. Quita versiones a medio preparar, comprueba
    la version activa contra su manifiesto (si esta rota, vuelve a la ultima buena del historial) y
    rehace las copias que no cuadren. Devuelve un dict con lo que encontro y lo que hizo."""
    estado = {"activo": None, "limpiadas": 0, "rehechas": 0, "aviso": ""}
    carpeta = os.path.join(base, CARPETA)
    if not os.path.isdir(carpeta):
        return estado
    for n in os.listdir(carpeta):
        if n.startswith(".preparando-") or (n.startswith(ACTIVO + ".tmp-")):
            p = os.path.join(carpeta, n)
            shutil.rmtree(p) if os.path.isdir(p) else os.unlink(p)
            estado["limpiadas"] += 1
    version, sha_man = _activo(base)
    if version:
        ok, sha_real = verifica_version(os.path.join(carpeta, version))
        if not ok or sha_real != sha_man:
            estado["aviso"] = "la version activa %s esta danada (%s)" % (version, sha_real if not ok else "manifiesto distinto")
            for previa in reversed(_historial(base)):
                if verifica_version(os.path.join(carpeta, previa))[0]:
                    activar(base, previa)
                    estado["aviso"] += "; se volvio a %s" % previa
                    break
            else:
                estado["aviso"] += "; no hay ninguna version anterior sana: las copias se dejan como estan"
                estado["activo"] = None
                return estado
    estado["activo"], _ = _activo(base)
    estado["rehechas"] = materializa(base, destinos)
    return estado


# ---------------------------------------------------------------------------------------------
#  Publicar
# ---------------------------------------------------------------------------------------------
def _guarda_version(base, archivos, extractor, prefijo, notas=()):
    carpeta = os.path.join(base, CARPETA)
    os.makedirs(carpeta, exist_ok=True)
    man = manifiesto(archivos, extractor, notas)
    nombre = "%s-%s" % (prefijo, _sha(man)[:16])
    final = os.path.join(carpeta, nombre)
    if os.path.isdir(final) and verifica_version(final)[0]:
        return nombre                                     # ya estaba, entera: no se reescribe
    prep = os.path.join(carpeta, ".preparando-" + uuid.uuid4().hex[:12])
    os.makedirs(prep)
    for n in sorted(archivos):
        _escribe(os.path.join(prep, n), archivos[n], n)
    _escribe(os.path.join(prep, MANIFIESTO), man, MANIFIESTO)     # el manifiesto, LO ULTIMO
    _sync_dir(prep)
    if os.path.isdir(final):                              # una version con ese nombre pero rota: se aparta
        os.rename(final, os.path.join(carpeta, ".rota-" + nombre + "-" + uuid.uuid4().hex[:6]))
    _paso("renombra")
    os.rename(prep, final)
    _sync_dir(carpeta)
    return nombre


def _archiva_lo_que_habia(base):
    """Antes de la PRIMERA publicacion: el par que ya estaba se guarda como version previa y se deja
    activo (es lo que habia puesto). No se toca ni un byte de esos archivos."""
    if _activo(base)[0]:
        return None
    previos = {}
    for n in DERIVADOS:
        p = os.path.join(base, n)
        if os.path.exists(p):
            previos[n] = _lee(p)
    if not previos:
        return None
    nota = "importado: el par que habia antes de publicar por versiones (no se verifico al importarlo)"
    nombre = _guarda_version(base, previos, 0, "previo", (nota,))
    activar(base, nombre)
    return nombre


def publicar(base, archivos, extractor, destinos=()):
    """Publica modelo + vectores (+ informe) como UNA version. Devuelve su nombre."""
    recuperar(base, destinos)
    _archiva_lo_que_habia(base)
    nombre = _guarda_version(base, archivos, extractor, "dsee")
    return activar(base, nombre, destinos)


def main(argv):
    base = os.path.dirname(os.path.abspath(__file__))
    if argv[:1] == ["--estado"]:
        v, _ = _activo(base)
        print("version activa:", v or "ninguna (nunca se publico por versiones)")
        if v:
            ok, info = verifica_version(os.path.join(base, CARPETA, v))
            print("completa y sin alterar:", "si" if ok else "NO (" + info + ")")
        return 0
    if argv[:1] == ["--recuperar"]:
        e = recuperar(base)
        print("activa: %s; carpetas a medias quitadas: %d; copias rehechas: %d %s"
              % (e["activo"], e["limpiadas"], e["rehechas"], e["aviso"]))
        return 0
    if len(argv) == 2 and argv[0] == "--activar":
        print("activa:", activar(base, argv[1]))
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
