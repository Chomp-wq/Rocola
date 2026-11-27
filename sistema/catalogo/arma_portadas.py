#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""EL PAQUETE DE PORTADAS DE LA PARED (sistema/catalogo/LEEME.md, «Las portadas por adelantado»; la rocola lo lee en
sistema/rocola/Xp/RocolaOs/RocolaOs.ParedPortadas.cs).

Peticion del dueno del 27-11-2026: la pared de discos con musica de Spotify se quito porque «cargan muy lento las caratulas»; al devolverla, un
disco del catalogo solo entra en la pared cuando su portada ya esta a la mano. Este programa baja de antemano del Cover Art Archive la miniatura
(front-250) de los discos que van a la pared, empezando por los de los artistas mas famosos del catalogo, y las junta en PARTES de unos 150 MB
(el limite de GitHub es por archivo y por publicacion) para publicarlas junto al catalogo. Solo biblioteca estandar de Python 3.

Ordenes:
  arma    --catalogo ARCHIVO.tsv.gz --salida DIR   baja las portadas y escribe rocola_portadas.txt (el manifiesto) y rocola_portadas_NN.pak
          [--txt rocola_catalogo.txt]              (la version del catalogo con que se armo va en el manifiesto)
          [--max-discos 60000] [--max-mb 900] [--parte-mb 150] [--hilos 16] [--base URL] [--cache DIR]
  valida  --salida DIR                             comprueba el manifiesto (sha256, bytes) y el indice de cada parte; sale con 1 si algo falla
  lista   --catalogo ARCHIVO.tsv.gz [--max-discos N]   dice cuantos discos entran en el paquete y no baja nada

QUE ENTRA: los discos del catalogo con portada (columna 6 = 1) y sin marcas (columna 7: bandas sonoras, videojuegos... no van en la pared: ver
arma_catalogo.py, LETRAS_MARCA), y de artistas que no sean de esos generos (Soundtrack, Karaoke, Infantil, Hablado), del artista mas famoso al menos
(la fama del catalogo, columna 4 de «A») y, de cada artista, sus discos en el orden del archivo; hasta --max-discos o --max-mb, lo que llegue
primero. Una portada que el Cover Art Archive no da (404) o que no es un JPEG de verdad se salta (se cuenta en el informe). Con --cache, lo ya
bajado en otra corrida no se vuelve a pedir (y los 404 tampoco).

EL FORMATO DE UNA PARTE (.pak, enteros de 32 bits little-endian): «RPK1», cuantos, y por cada portada 16 bytes del MBID, desplazamiento y
largo (desde el principio de la parte); despues, los JPEG uno tras otro. EL MANIFIESTO (claves=valor): formato=1, catalogo, discos, bytes, partes y
parteN=NOMBRE,SHA256,BYTES.
"""
import argparse
import concurrent.futures
import gzip
import hashlib
import io
import os
import re
import struct
import sys
import time
import urllib.error
import urllib.request

AGENTE = 'rocola-catalogo/1'          # generico: nada del dueno
BASE_CAA = 'https://coverartarchive.org'
MAX_DISCOS = 60000
MAX_MB = 900
PARTE_MB = 150
HILOS = 16
MAX_PORTADA = 2 * 1024 * 1024          # como la rocola: una portada de mas de 2 MB no vale
ES_MBID = re.compile(r'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')
GENEROS_FUERA = frozenset(['Soundtrack', 'Karaoke', 'Infantil', 'Hablado'])   # los de arma_catalogo.MARCA_POR_GENERO


def dice(*a):
    print(*a, file=sys.stderr, flush=True)


def lee_catalogo(ruta):
    """[(fama, orden, mbid)] de los discos que van a la pared, en el orden de entrada al paquete (artista mas famoso primero)."""
    artistas = []     # (fama, n, [mbid])
    n = 0
    fama, genero, discos = 0, '', None
    with gzip.open(ruta, 'rt', encoding='utf-8', newline='\n') as f:
        for l in f:
            c = l.rstrip('\n').split('\t')
            if c[0] == 'A':
                if discos:
                    artistas.append((fama, n, discos))
                n += 1
                fama, genero, discos = int(c[3]), c[2], []
            elif c[0] == 'D' and discos is not None and genero not in GENEROS_FUERA:
                mbid = c[4]
                if len(c) >= 6 and c[5] == '1' and not (len(c) >= 7 and c[6]) and ES_MBID.match(mbid):
                    discos.append(mbid)
        if discos:
            artistas.append((fama, n, discos))
    artistas.sort(key=lambda a: (-a[0], a[1]))
    vistos, salida = set(), []
    for fama, n, discos in artistas:
        for m in discos:
            if m not in vistos:
                vistos.add(m)
                salida.append(m)
    return salida


class Pide:
    """Un GET con reintentos: (estado, bytes). estado: 'ok', '404', 'mal' (no es un JPEG o pesa de mas) o 'error' (la red)."""

    def __init__(self, base, agente=AGENTE, reintentos=4):
        self.base, self.agente, self.reintentos = base.rstrip('/'), agente, reintentos

    def __call__(self, mbid):
        url = '%s/release-group/%s/front-250' % (self.base, mbid)
        for i in range(self.reintentos):
            try:
                req = urllib.request.Request(url, headers={'User-Agent': self.agente})
                with urllib.request.urlopen(req, timeout=30) as r:
                    b = r.read(MAX_PORTADA + 1)
                if len(b) > MAX_PORTADA or b[:2] != b'\xff\xd8':
                    return 'mal', b''
                return 'ok', b
            except urllib.error.HTTPError as e:
                if e.code == 404:
                    return '404', b''
                if e.code in (429, 500, 502, 503, 504):
                    time.sleep(min(30, 1.5 * (i + 1) ** 2))
                    continue
                return 'error', b''
            except (urllib.error.URLError, OSError, TimeoutError):
                time.sleep(min(30, 1.5 * (i + 1) ** 2))
        return 'error', b''


def escribe_parte(ruta, entradas):
    """entradas: [(mbid, bytes)] -> el .pak; devuelve (sha256, bytes)."""
    cab = io.BytesIO()
    cab.write(b'RPK1' + struct.pack('<I', len(entradas)))
    off = 8 + 24 * len(entradas)
    for mbid, b in entradas:
        cab.write(bytes.fromhex(mbid.replace('-', '')) + struct.pack('<II', off, len(b)))
        off += len(b)
    h = hashlib.sha256()
    tmp = ruta + '.tmp'
    with open(tmp, 'wb') as f:
        for trozo in [cab.getvalue()] + [b for _, b in entradas]:
            f.write(trozo)
            h.update(trozo)
    os.replace(tmp, ruta)
    return h.hexdigest(), os.path.getsize(ruta)


def orden_arma(a):
    t0 = time.time()
    os.makedirs(a.salida, exist_ok=True)
    cache = a.cache
    if cache:
        os.makedirs(cache, exist_ok=True)
    lista = lee_catalogo(a.catalogo)
    dice('catalogo: %d discos con portada y sin marcas' % len(lista))
    lista = lista[:a.max_discos]
    pide = Pide(a.base, reintentos=a.reintentos)
    tope_bytes = a.max_mb * 1024 * 1024
    parte_bytes = a.parte_mb * 1024 * 1024

    def trae(mbid):
        if cache:
            pj, p4 = os.path.join(cache, mbid + '.jpg'), os.path.join(cache, mbid + '.404')
            if os.path.exists(pj):
                with open(pj, 'rb') as f:
                    return 'ok', f.read()
            if os.path.exists(p4):
                return '404', b''
        est, b = pide(mbid)
        if cache and est == 'ok':
            with open(os.path.join(cache, mbid + '.jpg.t'), 'wb') as f:
                f.write(b)
            os.replace(os.path.join(cache, mbid + '.jpg.t'), os.path.join(cache, mbid + '.jpg'))
        elif cache and est in ('404', 'mal'):      # sin portada o no es un JPEG: no se vuelve a pedir
            open(os.path.join(cache, mbid + '.404'), 'wb').close()
        return est, b

    cuentas = {'ok': 0, '404': 0, 'mal': 0, 'error': 0}
    partes, actual, bytes_actual, total, n_ok = [], [], 0, 0, 0
    nombre_n = [0]

    def cierra():
        nonlocal actual, bytes_actual
        if not actual:
            return
        nombre_n[0] += 1
        nombre = 'rocola_portadas_%02d.pak' % nombre_n[0]
        sha, tam = escribe_parte(os.path.join(a.salida, nombre), actual)
        partes.append((nombre, sha, tam))
        dice('  parte %s: %d portadas, %.1f MB' % (nombre, len(actual), tam / 1048576))
        actual, bytes_actual = [], 0

    # en bloques (en el orden de fama), con varios hilos dentro de cada bloque: el paquete queda en el orden del catalogo
    BLOQUE = max(a.hilos * 8, 64)
    parar = False
    with concurrent.futures.ThreadPoolExecutor(max_workers=a.hilos) as ex:
        for ini in range(0, len(lista), BLOQUE):
            bloque = lista[ini:ini + BLOQUE]
            for mbid, (est, b) in zip(bloque, ex.map(trae, bloque)):
                cuentas[est] += 1
                if est != 'ok':
                    continue
                if total + len(b) > tope_bytes:
                    parar = True
                    break
                actual.append((mbid, b))
                bytes_actual += len(b)
                total += len(b)
                n_ok += 1
                if bytes_actual >= parte_bytes:
                    cierra()
            if parar:
                break
            if (ini // BLOQUE) % 50 == 49:
                dice('  %d de %d, %d portadas (%.0f MB), %d sin portada, %d fallidas (%.0f s)'
                     % (ini + len(bloque), len(lista), n_ok, total / 1048576, cuentas['404'] + cuentas['mal'], cuentas['error'],
                        time.time() - t0))
    cierra()
    version = ''
    if a.txt and os.path.exists(a.txt):
        for l in open(a.txt, encoding='utf-8'):
            if l.startswith('version='):
                version = l.strip().split('=', 1)[1]
    elif a.version:
        version = a.version
    tmp = os.path.join(a.salida, 'rocola_portadas.txt.tmp')
    with open(tmp, 'w', encoding='utf-8', newline='\n') as f:
        f.write('formato=1\ncatalogo=%s\ndiscos=%d\nbytes=%d\npartes=%d\n' % (version, n_ok, sum(p[2] for p in partes), len(partes)))
        for i, (nombre, sha, tam) in enumerate(partes, 1):
            f.write('parte%d=%s,%s,%d\n' % (i, nombre, sha, tam))
    os.replace(tmp, os.path.join(a.salida, 'rocola_portadas.txt'))
    # las partes de una corrida de antes que ya no estan en el manifiesto
    for n in os.listdir(a.salida):
        if re.match(r'^rocola_portadas_\d+\.pak$', n) and n not in {p[0] for p in partes}:
            os.remove(os.path.join(a.salida, n))
    dice('Listo: %d portadas en %d partes, %.1f MB (pedidas %d: %d bien, %d sin portada en el Cover Art Archive, %d no eran un JPEG, '
         '%d con la red mal) en %.0f s' % (n_ok, len(partes), total / 1048576, len(lista), cuentas['ok'], cuentas['404'], cuentas['mal'],
                                           cuentas['error'], time.time() - t0))
    if n_ok == 0:
        raise SystemExit('Ninguna portada: no se publica un paquete vacio')
    if len(lista) and cuentas['error'] * 10 > len(lista):
        raise SystemExit('Mas del 10 %% de las portadas fallaron por la red (%d de %d): no se publica' % (cuentas['error'], len(lista)))


def lee_manifiesto(ruta):
    d, partes = {}, []
    for l in open(ruta, encoding='utf-8'):
        l = l.strip()
        if '=' not in l:
            continue
        k, v = l.split('=', 1)
        if re.match(r'^parte\d+$', k):
            nombre, sha, tam = v.split(',')
            partes.append((nombre, sha, int(tam)))
        else:
            d[k] = v
    return d, partes


def valida(salida):
    """Los errores (lista) del paquete de SALIDA."""
    errores = []
    ruta = os.path.join(salida, 'rocola_portadas.txt')
    if not os.path.exists(ruta):
        return ['no esta rocola_portadas.txt']
    d, partes = lee_manifiesto(ruta)
    if d.get('formato') != '1':
        errores.append('formato=%r' % d.get('formato'))
    if int(d.get('partes', -1)) != len(partes) or not partes:
        errores.append('partes=%s y el manifiesto trae %d' % (d.get('partes'), len(partes)))
    total, discos, vistos = 0, 0, set()
    for nombre, sha, tam in partes:
        if '/' in nombre or '\\' in nombre or not re.match(r'^rocola_portadas_\d+\.pak$', nombre):
            errores.append('nombre de parte raro: %r' % nombre)
            continue
        p = os.path.join(salida, nombre)
        if not os.path.exists(p):
            errores.append('falta %s' % nombre)
            continue
        b = open(p, 'rb').read()
        if len(b) != tam:
            errores.append('%s mide %d y el manifiesto dice %d' % (nombre, len(b), tam))
        if hashlib.sha256(b).hexdigest() != sha:
            errores.append('%s: el sha256 no es el del manifiesto' % nombre)
        if b[:4] != b'RPK1':
            errores.append('%s: no empieza con RPK1' % nombre)
            continue
        n = struct.unpack('<I', b[4:8])[0]
        total += len(b)
        for i in range(n):
            mb, off, ln = b[8 + 24 * i:24 + 24 * i], *struct.unpack('<II', b[24 + 24 * i:32 + 24 * i])
            if off + ln > len(b) or ln < 2 or ln > MAX_PORTADA:
                errores.append('%s: portada %d fuera de la parte' % (nombre, i))
            elif b[off:off + 2] != b'\xff\xd8':
                errores.append('%s: portada %d no es un JPEG' % (nombre, i))
            if mb in vistos:
                errores.append('%s: portada %d repetida' % (nombre, i))
            vistos.add(mb)
        discos += n
    if str(discos) != d.get('discos'):
        errores.append('discos=%s y las partes traen %d' % (d.get('discos'), discos))
    if str(total) != d.get('bytes'):
        errores.append('bytes=%s y las partes miden %d' % (d.get('bytes'), total))
    return errores


def orden_valida(a):
    errores = valida(a.salida)
    for e in errores[:50]:
        dice('NO VALE: ' + e)
    if errores:
        raise SystemExit(1)
    dice('vale: ' + a.salida)


def orden_lista(a):
    lista = lee_catalogo(a.catalogo)
    print('%d discos van a la pared; entrarian %d' % (len(lista), min(len(lista), a.max_discos or MAX_DISCOS)))


def main(argv=None):
    p = argparse.ArgumentParser(description='El paquete de portadas de la pared (sistema/catalogo/LEEME.md)')
    s = p.add_subparsers(dest='orden', required=True)
    r = s.add_parser('arma')
    r.add_argument('--catalogo', required=True)
    r.add_argument('--salida', required=True)
    r.add_argument('--txt', help='rocola_catalogo.txt (de ahi sale la version del catalogo que va en el manifiesto)')
    r.add_argument('--version')
    r.add_argument('--max-discos', type=int, default=MAX_DISCOS)
    r.add_argument('--max-mb', type=int, default=MAX_MB)
    r.add_argument('--parte-mb', type=int, default=PARTE_MB)
    r.add_argument('--hilos', type=int, default=HILOS)
    r.add_argument('--base', default=BASE_CAA, help='el Cover Art Archive (otro solo para probar)')
    r.add_argument('--cache', help='carpeta donde guardar lo bajado (otra corrida lo reusa)')
    r.add_argument('--reintentos', type=int, default=4)
    v = s.add_parser('valida')
    v.add_argument('--salida', required=True)
    l = s.add_parser('lista')
    l.add_argument('--catalogo', required=True)
    l.add_argument('--max-discos', type=int, default=MAX_DISCOS)
    a = p.parse_args(argv)
    {'arma': orden_arma, 'valida': orden_valida, 'lista': orden_lista}[a.orden](a)


if __name__ == '__main__':
    main()
