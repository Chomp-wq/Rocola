#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""LA COPIA CHICA INVENTADA para probar el armador sin red (sistema/catalogo/LEEME.md, «La prueba»).

Escribe en la carpeta dada las tablas de MusicBrainz que usa arma_catalogo.py, con sus columnas de verdad (las del ESQUEMA del
armador) y nombres INVENTADOS, y la cache de Wikidata y ListenBrainz (cache/). Todo sale igual en cada corrida (sin azar).
Cubre los casos raros: acentos y «Ñ», nombres que van a «#», nombres sin letras latinas con y sin alias, artistas sin Spotify, con
Spotify solo en Wikidata, sin genero, con genero solo por etiquetas de MusicBrainz, sin discos que valgan, discos de dos artistas,
tipos secundarios fuera, nombres repetidos, mas de 6 discos, el id de Spotify de la edicion oficial mas vieja, portadas de frente, de
grupo y solo de atras, y paises directos, por subdivision y subiendo por «part of».
"""
import hashlib
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, '..'))
import arma_catalogo as ac  # noqa: E402

N = '\\N'


def mbid(tipo, n):
    h = hashlib.sha256(('%s-%d' % (tipo, n)).encode()).hexdigest()
    return '%s-%s-4%s-8%s-%s' % (h[:8], h[8:12], h[13:16], h[17:20], h[20:32])


B62 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz'


def spid(tipo, n):
    v = int(hashlib.sha256(('sp-%s-%d' % (tipo, n)).encode()).hexdigest(), 16)
    s = ''
    for _ in range(22):
        v, r = divmod(v, 62)
        s += B62[r]
    return s


class Tablas:
    def __init__(self):
        self.t = {k: [] for k in ac.ESQUEMA}

    def fila(self, tabla, **v):
        cols = ac.ESQUEMA[tabla]
        malas = set(v) - set(cols)
        assert not malas, (tabla, malas)
        self.t[tabla].append([str(v[c]) if c in v and v[c] is not None else N for c in cols])

    def escribe(self, carpeta):
        os.makedirs(carpeta, exist_ok=True)
        for tabla, filas in self.t.items():
            with open(os.path.join(carpeta, tabla), 'w', encoding='utf-8', newline='\n') as f:
                for c in filas:
                    f.write('\t'.join(c) + '\n')
        with open(os.path.join(carpeta, 'TIMESTAMP'), 'w') as f:
            f.write('2026-11-14 00:22:30.889344+00\n')


# Generos de Wikidata inventados (QID propios, nombres de genero comunes en ingles y en español)
GENEROS_WD = {
    'Q900001': ('regional Mexican', 'regional mexicano'), 'Q900002': ('norteño', 'norteño'), 'Q900003': ('banda', 'banda sinaloense'),
    'Q900004': ('cumbia', 'cumbia'), 'Q900005': ('salsa', 'salsa'), 'Q900006': ('bolero', 'bolero'),
    'Q900007': ('Latin pop', 'pop latino'), 'Q900008': ('reggaeton', 'reguetón'), 'Q900009': ('rock music', 'rock'),
    'Q900010': ('pop music', 'pop'), 'Q900011': ('heavy metal', 'heavy metal'), 'Q900012': ('jazz', 'jazz'),
    'Q900013': ('hip hop music', 'hip hop'), 'Q900014': ('mariachi', 'mariachi'), 'Q900015': ('bachata', 'bachata'),
    'Q900016': ('Latin ballad', 'balada romántica'), 'Q900017': ('art song', 'canción artística'),  # -> Otros
    'Q900018': ('electronic music', 'música electrónica'), 'Q900019': ('country music', 'country'),
}
G = {'regional': 'Q900001', 'norteno': 'Q900002', 'banda': 'Q900003', 'cumbia': 'Q900004', 'salsa': 'Q900005',
     'bolero': 'Q900006', 'latinpop': 'Q900007', 'reggaeton': 'Q900008', 'rock': 'Q900009', 'pop': 'Q900010',
     'metal': 'Q900011', 'jazz': 'Q900012', 'hiphop': 'Q900013', 'mariachi': 'Q900014', 'bachata': 'Q900015',
     'balada': 'Q900016', 'otros': 'Q900017', 'electro': 'Q900018', 'country': 'Q900019'}

SILABAS_A = ['Tol', 'Mira', 'Cara', 'Bel', 'Quin', 'Ros', 'Dal', 'Fer', 'Gua', 'Hor', 'Ixta', 'Jaru', 'Kel', 'Lun', 'Mor', 'Nava',
             'Ore', 'Pal', 'Rival', 'Sol', 'Tama', 'Ulu', 'Vel', 'Wal', 'Xala', 'Yuri', 'Zam']
SILABAS_B = ['var', 'sol', 'mina', 'quete', 'dera', 'lito', 'ranca', 'ves', 'neta', 'lazo']
PRIMERO = ['Los', 'Banda', 'Grupo', 'Sonora', 'Trio', 'Mariachi', '', '', '', '']
DISCOS = ['Corazón de {x}', 'Noches de {x}', 'El Camino', 'Lo Mejor de {x}', 'En Vivo desde {x}', 'Amor Eterno', 'Sin Fronteras',
          'Tierra Mía', 'Cielo Abierto', 'Raíces', 'Volver a {x}', 'Siempre Contigo']


def main():
    if len(sys.argv) != 2:
        raise SystemExit('uso: inventa.py CARPETA')
    salida = sys.argv[1]
    T = Tablas()
    # ---- tipos fijos (como los de MusicBrainz)
    for i, n in ((1, 'Album'), (2, 'Single'), (3, 'EP'), (11, 'Other'), (12, 'Broadcast')):
        T.fila('release_group_primary_type', id=i, name=n, child_order=i, description='', gid=mbid('pt', i))
    for i, n in ((1, 'Compilation'), (2, 'Soundtrack'), (3, 'Spokenword'), (4, 'Interview'), (5, 'Audiobook'), (6, 'Live'),
                 (7, 'Remix'), (8, 'DJ-mix'), (9, 'Mixtape/Street'), (10, 'Demo'), (11, 'Audio drama'), (12, 'Field recording')):
        T.fila('release_group_secondary_type', id=i, name=n, child_order=i, description='', gid=mbid('st', i))
    for i, n in ((1, 'Official'), (2, 'Promotion'), (3, 'Bootleg'), (4, 'Pseudo-Release')):
        T.fila('release_status', id=i, name=n, child_order=i, description='', gid=mbid('rs', i))
    for i, n in ((1, 'Front'), (2, 'Back'), (3, 'Booklet')):
        T.fila('cover_art_archive.art_type', id=i, name=n, child_order=i, description='', gid=mbid('at', i))
    # ---- areas: paises, una subdivision, una ciudad (de una subdivision sin codigo) y un enlace que no es «part of»
    paises = {'MX': 1, 'US': 2, 'CO': 3, 'ES': 4, 'GB': 5, 'PR': 6, 'JP': 7}
    for c, i in paises.items():
        T.fila('area', id=i, gid=mbid('area', i), name='Pais %s' % c, type=1, edits_pending=0, ended='f', comment='')
        T.fila('iso_3166_1', area=i, code=c)
    T.fila('area', id=20, gid=mbid('area', 20), name='Estado Inventado', type=2, edits_pending=0, ended='f', comment='')
    T.fila('iso_3166_2', area=20, code='MX-INV')
    T.fila('area', id=21, gid=mbid('area', 21), name='Region sin codigo', type=2, edits_pending=0, ended='f', comment='')
    T.fila('area', id=22, gid=mbid('area', 22), name='Ciudad Inventada', type=3, edits_pending=0, ended='f', comment='')
    T.fila('area', id=23, gid=mbid('area', 23), name='Ciudad Hermanada', type=3, edits_pending=0, ended='f', comment='')
    T.fila('link_type', id=356, gid=ac.PARTE_DE_GID, entity_type0='area', entity_type1='area', name='part of', description='',
           link_phrase='parts', reverse_link_phrase='part of', long_link_phrase='', is_deprecated='f', has_dates='t',
           entity0_cardinality=0, entity1_cardinality=0, child_order=0)
    T.fila('link_type', id=999, gid=mbid('lt', 999), entity_type0='area', entity_type1='area', name='twinned with', description='',
           link_phrase='', reverse_link_phrase='', long_link_phrase='', is_deprecated='f', has_dates='t', entity0_cardinality=0,
           entity1_cardinality=0, child_order=0)
    T.fila('link_type', id=194, gid=mbid('lt', 194), entity_type0='artist', entity_type1='url', name='free streaming',
           description='', link_phrase='', reverse_link_phrase='', long_link_phrase='', is_deprecated='f', has_dates='t',
           entity0_cardinality=0, entity1_cardinality=0, child_order=0)
    T.fila('link', id=1, link_type=356, attribute_count=0, ended='f')
    T.fila('link', id=2, link_type=999, attribute_count=0, ended='f')
    T.fila('link', id=3, link_type=194, attribute_count=0, ended='f')
    for k, (padre, hijo, enlace) in enumerate(((1, 21, 1), (21, 22, 1), (7, 23, 2))):   # 23 «hermanada» con JP: no es «part of»
        T.fila('l_area_area', id=k + 1, link=enlace, entity0=padre, entity1=hijo, edits_pending=0, link_order=0,
               entity0_credit='', entity1_credit='')
    # ---- generos de MusicBrainz y etiquetas (las de respaldo)
    for i, n in enumerate(['cumbia', 'salsa', 'rock', 'pop', 'regional mexican', 'jazz'], 1):
        T.fila('genre', id=i, gid=mbid('genre', i), name=n, comment='', edits_pending=0)
    for i, n in enumerate(['cumbia', 'salsa', 'rock', 'pop', 'regional mexican', 'jazz', 'seen live', 'mexican'], 1):
        T.fila('tag', id=i, name=n, ref_count=1)
    tag_id = {'cumbia': 1, 'salsa': 2, 'rock': 3, 'pop': 4, 'regional mexican': 5, 'jazz': 6, 'seen live': 7, 'mexican': 8}

    estado = {'artist': 0, 'credit': 0, 'rg': 0, 'release': 0, 'url': 0, 'lau': 0, 'lru': 0, 'caa': 0}
    wd_sp, wd_art, lb_art, lb_rg = [], [], [], []

    def nuevo(clave):
        estado[clave] += 1
        return estado[clave]

    def credito(*artistas_ids):
        k = nuevo('credit')
        T.fila('artist_credit', id=k, name='x', artist_count=len(artistas_ids), ref_count=1, created='2020-01-01', edits_pending=0,
               gid=mbid('ac', k))
        for p, a in enumerate(artistas_ids):
            T.fila('artist_credit_name', artist_credit=k, position=p, artist=a, name='x', join_phrase='')
        return k

    def url(texto):
        k = nuevo('url')
        T.fila('url', id=k, gid=mbid('url', k), url=texto, edits_pending=0)
        return k

    def artista(nombre, area=None, sp_mb=True, sp_wd=False, generos=(), etiquetas=(), wikipedias=0, escuchas=0, oyentes=0,
                begin_area=None, alias=()):
        k = nuevo('artist')
        g = mbid('artist', k)
        T.fila('artist', id=k, gid=g, name=nombre, sort_name=nombre, type=1, area=area, comment='', edits_pending=0, ended='f',
               begin_area=begin_area)
        if sp_mb:
            u = url('https://open.spotify.com/artist/%s' % spid('a', k))
            T.fila('l_artist_url', id=nuevo('lau'), link=3, entity0=k, entity1=u, edits_pending=0, link_order=0, entity0_credit='',
                   entity1_credit='')
            u = url('https://www.discogs.com/artist/%d' % k)   # otro enlace que no es de Spotify
            T.fila('l_artist_url', id=nuevo('lau'), link=3, entity0=k, entity1=u, edits_pending=0, link_order=0, entity0_credit='',
                   entity1_credit='')
        if sp_wd:
            wd_sp.append((g, spid('a', k) if not sp_mb else spid('a', k)))
        wd_art.append((g, str(wikipedias), ','.join(sorted({G[x] for x in generos}, key=lambda q: int(q[1:])))))
        lb_art.append((g, str(escuchas), str(oyentes)))
        for n, votos in etiquetas:
            T.fila('artist_tag', artist=k, tag=tag_id[n], count=votos)
        for i, (an, loc, prim, tipo) in enumerate(alias):
            T.fila('artist_alias', id=k * 10 + i, artist=k, name=an, locale=loc, edits_pending=0, type=tipo, sort_name=an,
                   primary_for_locale=prim, ended='f')
        return k

    def disco(artista_id, nombre, ano=None, escuchas=0, tipo=1, secundarios=(), ediciones=1, sp=None, portada=None,
              credito_id=None, oficial=True):
        """sp: None, 'uno' (una edicion con Spotify), 'dos' (dos ediciones, gana la oficial mas vieja); portada: None, 'frente',
        'grupo', 'atras'."""
        k = nuevo('rg')
        g = mbid('rg', k)
        T.fila('release_group', id=k, gid=g, name=nombre, artist_credit=credito_id or credito(artista_id), type=tipo, comment='',
               edits_pending=0)
        for s in secundarios:
            T.fila('release_group_secondary_type_join', release_group=k, secondary_type=s, created='2020-01-01')
        lb_rg.append((g, str(escuchas), str(escuchas // 10)))
        eds = []
        for e in range(ediciones):
            r = nuevo('release')
            eds.append(r)
            T.fila('release', id=r, gid=mbid('release', r), name=nombre, artist_credit=1, release_group=k,
                   status=1 if (oficial or e > 0) else 3, language=N, script=N, barcode='', comment='', edits_pending=0, quality=-1)
            if ano:
                if e % 2 == 0:
                    T.fila('release_country', release=r, country=1, date_year=ano + e, date_month=5 if e else None)
                else:
                    T.fila('release_unknown_country', release=r, date_year=ano + e)
        if sp:
            objetivo = eds[:1] if sp == 'uno' else eds[:2]
            for i, r in enumerate(objetivo):
                u = url('https://open.spotify.com/intl-es/album/%s?si=abc' % spid('album', r) if i == 0 else
                        'https://open.spotify.com/album/%s' % spid('album', r))
                T.fila('l_release_url', id=nuevo('lru'), link=3, entity0=r, entity1=u, edits_pending=0, link_order=0,
                       entity0_credit='', entity1_credit='')
        if portada in ('frente', 'atras'):
            c = nuevo('caa')
            T.fila('cover_art_archive.cover_art', id=c, release=eds[-1], comment='', edit=1, ordering=1, date_uploaded='2020-01-01',
                   edits_pending=0, mime_type='image/jpeg')
            T.fila('cover_art_archive.cover_art_type', id=c, type_id=1 if portada == 'frente' else 2)
        if portada == 'grupo':
            T.fila('cover_art_archive.release_group_cover_art', release_group=k, release=eds[0])
        return k

    def discos_varios(a, nombre, n, base_escuchas, ano=1990):
        for i in range(n):
            disco(a, DISCOS[i % len(DISCOS)].format(x=nombre.split()[-1]), ano=ano + i, escuchas=base_escuchas // (i + 1),
                  sp='uno' if i % 2 == 0 else None, portada='frente' if i % 3 != 2 else None, ediciones=1 + i % 3)

    # ---- los casos a proposito
    a = artista('Los Ángeles del Desierto', area=1, generos=['cumbia', 'regional'], wikipedias=25, escuchas=4000000, oyentes=60000)
    discos_varios(a, 'Desierto', 8, 300000)                                   # mas de 6: solo 6
    disco(a, 'CORAZÓN DE DESIERTO', ano=2001, escuchas=1)                     # repetido sin mayusculas ni acentos: fuera
    disco(a, 'Remezclas del Desierto', ano=2002, escuchas=900000, secundarios=[7])       # Remix: fuera
    disco(a, 'Desierto en Vivo', ano=2003, escuchas=5, secundarios=[6, 1])              # Live + Compilation: vale
    disco(a, 'Sencillo del Desierto', ano=2004, escuchas=800000, tipo=2)                 # Single: fuera
    b = artista('Ángeles de Plata', area=20, generos=['bolero'], wikipedias=8, escuchas=90000, oyentes=3000)  # MX por la subdivision
    disco(b, 'Plata Pura', ano=1975, escuchas=0, sp='dos', ediciones=3, oficial=False)  # dos con Spotify: la oficial mas vieja
    disco(b, 'Plata Fina', ano=1972, escuchas=0, portada='grupo', ediciones=1)          # empate a 0 escuchas
    disco(b, 'Plata Vieja', ano=1970, escuchas=0, portada='atras', ediciones=5)
    disco(b, 'Plata Sin Fecha', escuchas=0)
    c = artista('Ñandú Sonoro', area=22, generos=['salsa'], wikipedias=3, escuchas=20000, oyentes=900)   # MX subiendo dos saltos
    disco(c, 'Ñandú Salsero', ano=1988, escuchas=1000, sp='uno', portada='frente')
    d = artista('¡Ay Caramba Banda!', area=1, generos=['banda'], wikipedias=0, escuchas=500, oyentes=20)        # va a «#»
    disco(d, 'Caramba', ano=2015, escuchas=10)
    e = artista('3 Gallos del Valle', area=6, generos=['norteno'], wikipedias=1, escuchas=7000, oyentes=300)    # «#», Puerto Rico
    disco(e, 'Tres Gallos', ano=2010, escuchas=70)
    f = artista('Сокол Ветра', area=7, generos=['rock'], wikipedias=4, escuchas=3000, oyentes=100,
                alias=[('Sokol Vetra', 'en', 't', 1), ('Sokol del Viento', 'es', 'f', 1), ('Сокол', 'es', 't', 1)])
    disco(f, 'Viento Norte', ano=2000, escuchas=30)
    disco(f, 'Ветер', ano=2001, escuchas=3000)                                 # sin letras latinas: fuera
    h = artista('Звезда Без Имени', area=7, generos=['rock'], escuchas=999999999, oyentes=999999)       # sin alias: fuera
    disco(h, 'Estrella', ano=2000, escuchas=10)
    i = artista('Sin Spotify Ninguno', area=1, sp_mb=False, generos=['regional'], escuchas=99999999)  # fuera: sin Spotify
    disco(i, 'Nada', ano=2000)
    j = artista('Solo en Wikidata', area=23, sp_mb=False, sp_wd=True, generos=['pop', 'latinpop'],         # empate 1 a 1: gana la regla
                # que va antes en GenreLabel (Latino)
                wikipedias=12, escuchas=150000, oyentes=5000)              # area 23 hermanada con JP: no es «part of», sin pais
    disco(j, 'Wiki Pop', ano=2019, escuchas=500, sp='uno', portada='grupo')
    k = artista('Etiquetas Nomas', area=3, generos=['otros'], etiquetas=[('cumbia', 3), ('salsa', 5), ('seen live', 50)],
                escuchas=40000, oyentes=1500)                               # genero por etiquetas: Tropical (salsa 5 > cumbia 3)
    disco(k, 'Puras Etiquetas', ano=1999, escuchas=40)
    m = artista('Sin Genero Alguno', area=2, generos=['otros'], etiquetas=[('seen live', 9), ('mexican', 4)], escuchas=10 ** 7)
    disco(m, 'Sin Genero', ano=2000)                                            # fuera: sin genero
    n = artista('Solo Sencillos', area=1, generos=['regional'], escuchas=10 ** 7)
    disco(n, 'Un Sencillo', ano=2000, tipo=2)
    disco(n, 'Un EP', ano=2001, tipo=3)                                        # fuera: sin discos que valgan
    o = artista('Dueto Uno', area=1, generos=['bolero'], escuchas=60000, oyentes=2000)
    p = artista('Dueto Dos', area=1, generos=['bolero'], escuchas=50000, oyentes=1800)
    juntos = credito(o, p)
    disco(o, 'Juntos', ano=1980, escuchas=999999, credito_id=juntos)           # de dos artistas: no cuenta para ninguno
    disco(o, 'Solo Uno', ano=1981, escuchas=10)
    disco(p, 'Solo Dos', ano=1982, escuchas=10)
    q = artista('Empate Alfa', area=5, generos=['jazz'], escuchas=1000, oyentes=10)
    r = artista('empate alfa', area=5, generos=['jazz'], escuchas=1000, oyentes=10)    # misma fama: orden por nombre y MBID
    disco(q, 'Alfa Uno', ano=1960, escuchas=1)
    disco(r, 'Alfa Dos', ano=1961, escuchas=1)
    s = artista('Reggaetonero Inventado', area=6, generos=['reggaeton', 'hiphop'], wikipedias=30, escuchas=2 * 10 ** 7, oyentes=10 ** 5)
    discos_varios(s, 'Perreo', 4, 2000000, 2015)
    t = artista('Mariachi Sol de Inventado', area=1, begin_area=None, generos=['mariachi', 'regional'], wikipedias=15,
                escuchas=3 * 10 ** 6, oyentes=4 * 10 ** 4)
    discos_varios(t, 'Jalisco', 6, 100000, 1965)
    u = artista('Sin Area Ninguna', area=None, begin_area=4, generos=['balada'], escuchas=8000, oyentes=200)  # pais por begin_area
    disco(u, 'Baladas Sueltas', ano=1985, escuchas=8)
    v = artista('Rockeros Ingleses', area=5, generos=['rock', 'metal'],  # empate: Metal (antes que Rock en GenreLabel)
                 wikipedias=80, escuchas=10 ** 8, oyentes=10 ** 6)
    discos_varios(v, 'Londres', 7, 10 ** 7, 1970)

    # ---- muchos artistas de relleno para que cada letra traiga varios generos (y para probar los topes)
    gens = ['regional', 'cumbia', 'salsa', 'bolero', 'latinpop', 'rock', 'pop', 'reggaeton', 'jazz', 'bachata', 'balada', 'metal',
            'electro', 'country', 'norteno', 'hiphop']
    areas = [1, 1, 1, 2, 3, 4, 5, 6, 7, None]
    for idx in range(160):
        nombre = '%s %s%s' % (PRIMERO[idx % len(PRIMERO)], SILABAS_A[(idx * 7) % len(SILABAS_A)], SILABAS_B[idx % len(SILABAS_B)])
        nombre = nombre.strip()
        if idx % 9 == 0:
            nombre = nombre.lower()        # en minusculas: la letra sale igual
        gg = [gens[idx % len(gens)]] + ([gens[(idx * 3) % len(gens)]] if idx % 4 == 0 else [])
        x = artista(nombre, area=areas[idx % len(areas)], generos=gg, wikipedias=(idx * 13) % 60,
                    escuchas=(idx * 7919) % 5000000, oyentes=(idx * 104729) % 90000)
        discos_varios(x, nombre, 1 + idx % 7, (idx * 31337) % 800000 + 10, 1960 + idx % 50)

    T.escribe(salida)
    cache = os.path.join(salida, 'cache')
    os.makedirs(cache, exist_ok=True)

    def tsv(nombre, filas):
        with open(os.path.join(cache, nombre), 'w', encoding='utf-8', newline='\n') as f:
            for c in filas:
                f.write('\t'.join(c) + '\n')

    tsv('wd_spotify.tsv', [(g, s) for g, s in wd_sp])
    tsv('wd_artistas.tsv', wd_art)
    tsv('wd_generos.tsv', [(q, en, es) for q, (en, es) in sorted(GENEROS_WD.items())])
    tsv('lb_artistas.tsv', lb_art)
    tsv('lb_discos.tsv', lb_rg)
    print('inventado en %s: %d artistas, %d grupos de lanzamiento, %d ediciones' % (salida, estado['artist'], estado['rg'],
                                                                                  estado['release']))


if __name__ == '__main__':
    main()
