#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""EL CATALOGO DE LA ROCOLA (sistema/catalogo/LEEME.md; plan en sistema/rocola/Xp/PLAN_PARED_GENEROS.md).

Arma «rocola_catalogo.tsv.gz»: por letra de XP, los artistas mas famosos (para un bar de Mexico) con su genero (las etiquetas de XP,
leidas de GenreLabel en Form1.Fondo.cs: una sola lista) y sus discos mas escuchados, con datos abiertos: MusicBrainz (copia completa,
CC0; sus etiquetas de genero, CC BY-NC-SA, solo de respaldo), Wikidata (CC0) y ListenBrainz (CC0). Solo biblioteca estandar de Python 3.

Ordenes:
  extrae  --salida DIR                 lee un tar de la copia de MusicBrainz por la entrada estandar (ya descomprimido) y deja en DIR solo
                                       las tablas que se usan (algunas ya filtradas: url solo de Spotify, artist_credit de un artista,
                                       release_group solo Album)
  arma    --tablas DIR --salida DIR    arma el catalogo; pregunta a Wikidata y ListenBrainz lo que no este en la carpeta de --cache
          [--sin-red] [--solo-cc0]     (--sin-red: solo lo de la cache; para la prueba); escribe el .tsv.gz, el .txt y el informe
  valida  ARCHIVO.tsv.gz               comprueba el contrato «I8» (formato, orden, topes); sale con 1 si algo falla
  esquema CreateTables.sql [caa.sql]   comprueba que el orden de columnas que usa este programa es el de MusicBrainz
"""

import argparse
import gzip
import hashlib
import io
import json
import math
import os
import re
import sys
import tarfile
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request

# =====================================================================================================================================
# PERILLAS (sus valores se explican en el LEEME; cambiarlas cambia el catalogo, no el formato)
# =====================================================================================================================================
TOPE_DISCOS_LETRA = 9000          # 500 paginas de 18 discos (la pared de XP: 6 x 3)
DISCOS_POR_ARTISTA = 6            # como mucho, los mas escuchados
MAX_GENERO_LETRA = 0.35           # ningun genero pasa del 35 % del tope de una letra (3 150 discos)

# Fama = mezcla en escala logaritmica (cada parte va de 0 a 1 y se corta en 1), por los factores de pais y de genero
PESO_ESCUCHAS = 0.45              # escuchas de ListenBrainz (total_listen_count)
PESO_OYENTES = 0.30               # oyentes de ListenBrainz (total_user_count)
PESO_WIKIPEDIAS = 0.25            # en cuantas Wikipedias esta (wikibase:sitelinks)
LOG_ESCUCHAS_TOPE = 8.0           # log10: 100 millones de escuchas = 1
LOG_OYENTES_TOPE = 6.0            # log10: 1 millon de oyentes = 1
LOG_WIKIPEDIAS_TOPE = 2.5         # log10: ~316 Wikipedias = 1

FACTOR_MEXICO = 3.0
FACTOR_HISPANO = 2.0              # paises de habla hispana y Puerto Rico
PAISES_HISPANOS = frozenset('AR BO CL CO CR CU DO EC ES GQ GT HN NI PA PE PR PY SV UY VE'.split())
FACTOR_GENERO = {'Regional': 3.0, 'Tropical': 2.5, 'Bolero': 2.5, 'Latino': 2.2, 'Reggaeton': 2.0, 'Flamenco': 1.5}

# Discos: tipo principal Album y sin estos tipos secundarios (los nombres de release_group_secondary_type)
TIPOS_SECUNDARIOS_FUERA = frozenset(['Spokenword', 'Interview', 'Audiobook', 'Audio drama', 'Demo', 'DJ-mix', 'Mixtape/Street',
                                     'Remix', 'Field recording'])
TIPO_PRINCIPAL = 'Album'

# Red
AGENTE = 'rocola-catalogo/1'      # User-Agent generico: nada del dueno
WIKIDATA = 'https://query.wikidata.org/sparql'
LISTENBRAINZ = 'https://api.listenbrainz.org/1/popularity/'
LOTE_WIKIDATA = 1500              # MBID por consulta (~4 s con 2 000; el tope de Wikidata es 60 s)
LOTE_LISTENBRAINZ = 1000          # MBID por peticion (1 001 ya lo corta el servidor; se parte a la mitad si falla)

LETRAS = '#ABCDEFGHIJKLMNOPQRSTUVWXYZ'
FORMATO = 1

# =====================================================================================================================================
# LAS TABLAS DE MUSICBRAINZ QUE SE USAN, CON SU ORDEN DE COLUMNAS (de admin/sql/CreateTables.sql y caa/CreateTables.sql de
# metabrainz/musicbrainz-server, comprobado contra la copia del 14-11-2026; «esquema» lo vuelve a comprobar). Cada fila se comprueba
# al leerla: si una tabla cambia de columnas, el programa se para en vez de leer otra cosa.
# =====================================================================================================================================
ESQUEMA = {
    'artist': 'id gid name sort_name begin_date_year begin_date_month begin_date_day end_date_year end_date_month end_date_day type '
              'area gender comment edits_pending last_updated ended begin_area end_area',
    'artist_alias': 'id artist name locale edits_pending last_updated type sort_name begin_date_year begin_date_month begin_date_day '
                    'end_date_year end_date_month end_date_day primary_for_locale ended',
    'artist_credit': 'id name artist_count ref_count created edits_pending gid',
    'artist_credit_name': 'artist_credit position artist name join_phrase',
    'release_group': 'id gid name artist_credit type comment edits_pending last_updated',
    'release_group_primary_type': 'id name parent child_order description gid',
    'release_group_secondary_type': 'id name parent child_order description gid',
    'release_group_secondary_type_join': 'release_group secondary_type created',
    'release': 'id gid name artist_credit release_group status packaging language script barcode comment edits_pending quality '
               'last_updated',
    'release_status': 'id name parent child_order description gid',
    'release_country': 'release country date_year date_month date_day',
    'release_unknown_country': 'release date_year date_month date_day',
    'url': 'id gid url edits_pending last_updated',
    'l_artist_url': 'id link entity0 entity1 edits_pending last_updated link_order entity0_credit entity1_credit',
    'l_release_url': 'id link entity0 entity1 edits_pending last_updated link_order entity0_credit entity1_credit',
    'area': 'id gid name type edits_pending last_updated begin_date_year begin_date_month begin_date_day end_date_year end_date_month '
            'end_date_day ended comment',
    'iso_3166_1': 'area code',
    'iso_3166_2': 'area code',
    'l_area_area': 'id link entity0 entity1 edits_pending last_updated link_order entity0_credit entity1_credit',
    'link': 'id link_type begin_date_year begin_date_month begin_date_day end_date_year end_date_month end_date_day attribute_count '
            'created ended',
    'link_type': 'id parent child_order gid entity_type0 entity_type1 name description link_phrase reverse_link_phrase '
                 'long_link_phrase last_updated is_deprecated has_dates entity0_cardinality entity1_cardinality',
    'genre': 'id gid name comment edits_pending last_updated',
    # mbdump-derived (CC BY-NC-SA 3.0): solo de respaldo para el genero; --solo-cc0 no las lee
    'tag': 'id name ref_count',
    'artist_tag': 'artist tag count last_updated',
    # mbdump-cover-art-archive (en el tar se llaman «cover_art_archive.TABLA»)
    'cover_art_archive.art_type': 'id name parent child_order description gid',
    'cover_art_archive.cover_art': 'id release comment edit ordering date_uploaded edits_pending mime_type filesize thumb_250_filesize '
                                   'thumb_500_filesize thumb_1200_filesize',
    'cover_art_archive.cover_art_type': 'id type_id',
    'cover_art_archive.release_group_cover_art': 'release_group release',
}
ESQUEMA = {t: c.split() for t, c in ESQUEMA.items()}
TABLAS_CC_BY_NC_SA = ('tag', 'artist_tag')
# «part of» entre areas (link_type.gid): el pais de una ciudad o un estado
PARTE_DE_GID = 'de7cc874-8b1b-3a05-8272-f3834c968fb7'

ES_SPOTIFY = re.compile(r'open\.spotify\.com/(?:intl-[a-zA-Z-]+/)?(artist|album)/([0-9A-Za-z]{22})(?![0-9A-Za-z])')
ES_MBID = re.compile(r'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')
ES_SPOTIFY_ID = re.compile(r'^[0-9A-Za-z]{22}$')


def dice(*a):
    print(time.strftime('%H:%M:%S'), *a, file=sys.stderr, flush=True)


# =====================================================================================================================================
# LO DE XP: el genero (GenreLabel) y la letra (FirstLetter), leidos/traducidos del codigo de la rocola
# =====================================================================================================================================
def ruta_xp_por_defecto():
    aqui = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(aqui, '..', 'rocola', 'Xp', 'Codigo', 'Form1.Fondo.cs')


def _cadena_cs(lit):
    """El texto de un literal de C# entre comillas (solo los escapes que usa GenreLabel)."""
    return re.sub(r'\\u([0-9a-fA-F]{4})|\\(.)', lambda m: chr(int(m.group(1), 16)) if m.group(1) else
                  {'n': '\n', 't': '\t', '\\': '\\', '"': '"', "'": "'", '0': '\0'}.get(m.group(2), m.group(2)), lit)


def lee_reglas_genero(ruta=None):
    """Las reglas de GenreLabel (Form1.Fondo.cs), en su orden: [(etiqueta, (palabra, ...)), ...]. Has(g, ...) es g.Contains(palabra)
    con g = genero.ToLowerInvariant(): gana la primera regla con alguna palabra dentro. Sin genero o sin regla: «Otros»."""
    ruta = ruta or ruta_xp_por_defecto()
    with open(ruta, encoding='utf-8-sig') as f:
        texto = f.read()
    m = re.search(r'static string GenreLabel\(string genre\)\s*\{(.*?)\n\s*\}\s*\n', texto, re.S)
    if not m:
        raise SystemExit('No encuentro GenreLabel en %s' % ruta)
    cuerpo = m.group(1)
    reglas = []
    for r in re.finditer(r'if\s*\(\s*Has\(\s*g\s*,(.*?)\)\s*\)\s*return\s*"([^"]+)"\s*;', cuerpo, re.S):
        palabras = tuple(_cadena_cs(x) for x in re.findall(r'"((?:[^"\\]|\\.)*)"', r.group(1)))
        reglas.append((r.group(2), palabras))
    if not re.search(r'return\s*"Otros"\s*;\s*$', cuerpo.strip()):
        raise SystemExit('GenreLabel ya no acaba en «Otros»: revisar el armador')
    if len(reglas) < 30:
        raise SystemExit('GenreLabel dio solo %d reglas (se esperan 30 o mas): revisar el armador' % len(reglas))
    etiquetas = {e for e, _ in reglas} | {'Otros'}
    malas = sorted({e for d in (PREMAPEO_EXACTO, PREMAPEO_FRASE) for e in d.values()} - etiquetas)
    if malas:
        raise SystemExit('El pre-mapeo de generos usa etiquetas que GenreLabel ya no tiene: %s' % ', '.join(malas))
    return reglas


def etiqueta_genero(texto, reglas):
    if not texto:
        return 'Otros'
    g = texto.lower()
    for etiqueta, palabras in reglas:
        for p in palabras:
            if p in g:
                return etiqueta
    return 'Otros'


# ---- EL PRE-MAPEO de los nombres de genero (en ingles y en español, de Wikidata y MusicBrainz) ANTES de GenreLabel ----------------
# GenreLabel busca PEDAZOS de palabra (Has = Contains) pensados para las etiquetas ID3 de los MP3 de la rocola; con los nombres de
# Wikidata y MusicBrainz algunos caen mal: «new romantic» lleva «romantic» (Bolero), «classic rock» lleva «classic» (Clasica),
# «garage rock» lleva «garage» (DnB), «dubstep» lleva «dub» (Reggae), «soundtrack / banda sonora» lleva «banda» (Regional),
# «folk-pop» lleva «k-pop», «worship» y «chiptune» llevan «hip», «pornographic» y «photography» llevan «rap»... Aqui se corrigen SIN
# tocar GenreLabel ni su lista: cada nombre se compara (en minusculas, sin acentos, con «-», «_» y «/» como espacio, sin lo que va entre
# parentesis y sin « music» al final) con PREMAPEO_EXACTO (el nombre entero) y con PREMAPEO_FRASE (palabras enteras dentro del
# nombre); gana la coincidencia mas larga (de los dos nombres de Wikidata, el ingles y el español). Sin coincidencia, el nombre va a
# GenreLabel como siempre. «Otros» = no da genero (nombres que no son de musica). El LEEME («El genero») lo explica; la prueba
# (comprueba.py) tiene un caso por trampa.
PREMAPEO_EXACTO = {
    # Pop: new wave, new romantic, synth-pop y parientes (GenreLabel: Otros, Bolero o Electronica)
    'new wave': 'Pop', 'nueva ola': 'Pop', 'new romantic': 'Pop', 'nuevo romanticismo': 'Pop',
    'synth pop': 'Pop', 'synthpop': 'Pop', 'tecnopop': 'Pop', 'techno pop': 'Pop', 'tecno pop': 'Pop', 'technopop': 'Pop',
    'electropop': 'Pop', 'electronic pop': 'Pop', 'pop electronico': 'Pop', 'dance pop': 'Pop', 'disco pop': 'Pop',
    'baroque pop': 'Pop', 'pop barroco': 'Pop', 'orchestral pop': 'Pop', 'pop orquestal': 'Pop', 'hypnagogic pop': 'Pop',
    'pop hipnagogico': 'Pop', 'garage pop': 'Pop', 'adult contemporary': 'Pop', 'carnavalskraker': 'Pop', 'doskpop': 'Pop',
    'folk pop': 'Folk', 'punk pop': 'Punk', 'rock pop': 'Pop',
    # Rock: classic rock, garage rock, hard rock, album rock y los que GenreLabel manda a Clasica, DnB, Soul o Electronica
    'classic rock': 'Rock', 'rock clasico': 'Rock', 'garage rock': 'Rock', 'garage rock revival': 'Rock', 'garage psych': 'Rock',
    'hard rock': 'Rock', 'album rock': 'Rock', 'album oriented rock': 'Rock', 'blues rock': 'Rock', 'rock blues': 'Rock',
    'electronic rock': 'Rock', 'rock electronico': 'Rock', 'dance rock': 'Rock', 'piano rock': 'Rock', 'instrumental rock': 'Rock',
    'rock instrumental': 'Rock', 'symphonic rock': 'Rock', 'rock sinfonico': 'Rock', 'symphonic prog': 'Rock', 'rock opera': 'Rock',
    'opera rock': 'Rock', 'heavy psych': 'Rock', 'shoegaze': 'Rock', 'shoegazing': 'Rock', 'emo': 'Punk', 'screamo': 'Punk',
    'oi!': 'Punk', 'sasscore': 'Punk',
    # Reggae: dancehall, rocksteady, lovers rock (GenreLabel: Electronica o Rock)
    'dancehall': 'Reggae', 'rocksteady': 'Reggae', 'rock steady': 'Reggae', 'lovers rock': 'Reggae', 'crack rock steady': 'Punk',
    # Electronica: dubstep (GenreLabel: Reggae por «dub»), bass music (DnB por «bass»), hardcore de baile (Metal por «hardcore»)...
    'dubstep': 'Electronica', 'post dubstep': 'Electronica', 'melodic dubstep': 'Electronica', 'riddim dubstep': 'Electronica',
    'dubstyle': 'Electronica', 'deathstep': 'Electronica', 'dub techno': 'Electronica', 'dubtronica': 'Electronica',
    'tropical house': 'Electronica', 'house tropical': 'Electronica', 'latin house': 'Electronica', 'garage house': 'Electronica',
    'bass house': 'Electronica', 'future bass': 'Electronica', 'kawaii future bass': 'Electronica', 'bass music': 'Electronica',
    'uk bass': 'Electronica', 'brazilian bass': 'Electronica', 'bubblegum bass': 'Electronica', 'colour bass': 'Electronica',
    'melodic bass': 'Electronica', 'midtempo bass': 'Electronica', 'techno bass': 'Electronica', 'hardbass': 'Electronica',
    'happy hardcore': 'Electronica', 'hardcore techno': 'Electronica', 'uk hardcore': 'Electronica', 'hardcore breaks': 'Electronica',
    'breakbeat hardcore': 'Electronica', 'freeform hardcore': 'Electronica', 'uptempo hardcore': 'Electronica',
    'industrial hardcore': 'Electronica', 'digital hardcore': 'Electronica', 'mainstream hardcore': 'Electronica',
    'amapiano': 'Electronica', 'afropiano': 'Electronica', 'chiptune': 'Electronica', 'dungeon chip': 'Electronica',
    'doomcore': 'Electronica', 'uk funky': 'Electronica', 'funkot': 'Electronica',
    # DnB: los subgeneros con «funk» (GenreLabel: Soul)
    'neurofunk': 'DnB', 'liquid funk': 'DnB', 'drumfunk': 'DnB',
    # Punk: el hardcore punk y sus hijos (GenreLabel: Metal por «hardcore»; el resto de los «hardcore» va en la frase)
    'hardcore punk': 'Punk', 'post hardcore': 'Punk', 'melodic hardcore': 'Punk', 'hardcore melodico': 'Punk',
    # Hip-Hop: hardcore hip hop (Metal), drill, grime y otros que GenreLabel deja en Otros; Afroswing (Jazz por «swing»)
    'hardcore hip hop': 'Hip-Hop', 'hip hop hardcore': 'Hip-Hop', 'drill': 'Hip-Hop', 'grime': 'Hip-Hop', 'horrorcore': 'Hip-Hop',
    'dirty south': 'Hip-Hop', 'miami bass': 'Hip-Hop', 'afroswing': 'Hip-Hop',
    # Reggaeton: lo urbano latino (GenreLabel: Hip-Hop por «trap» o Latino por «latin»); romantic flow (Bolero por «romantic»)
    'latin trap': 'Reggaeton', 'trap latino': 'Reggaeton', 'urbano': 'Reggaeton', 'urbano latino': 'Reggaeton',
    'musica urbana latina': 'Reggaeton', 'trapeton': 'Reggaeton', 'romantic flow': 'Reggaeton',
    # Latino: el funk de Brasil (GenreLabel: Soul por «funk») y el tecnobrega (Electronica por «tecno»)
    'funk carioca': 'Latino', 'baile funk': 'Latino', 'funk brasileiro': 'Latino', 'brazilian funk': 'Latino',
    'funk ostentacao': 'Latino', 'funk automotivo': 'Latino', 'funk consciente': 'Latino', 'funk de bh': 'Latino',
    'funk mandelao': 'Latino', 'funk melody': 'Latino', 'funk proibidao': 'Latino', 'brega funk': 'Latino', 'arrocha funk': 'Latino',
    'funknejo': 'Latino', 'tecnobrega': 'Latino', 'tecno brega': 'Latino',
    # Tropical: la rumba de Cuba y de salon (GenreLabel: Flamenco); merenhouse (Electronica por «house»)
    'rumba cubana': 'Tropical', 'cuban rumba': 'Tropical', 'rhumba': 'Tropical', 'rumba de salon': 'Tropical',
    'merenhouse': 'Tropical', 'merenrap': 'Tropical',
    # Jazz: latin jazz (Latino), post-bop y big band (Otros), ragtime clasico (Clasica)
    'latin jazz': 'Jazz', 'jazz latino': 'Jazz', 'post bop': 'Jazz', 'hard bop': 'Jazz', 'big band': 'Jazz', 'classic ragtime': 'Jazz',
    # Soul: new jack swing (Jazz por «swing»), urban contemporary (Otros), piedmont blues (Electronica por «edm»)
    'new jack swing': 'Soul', 'urban contemporary': 'Soul', 'piedmont blues': 'Soul',
    # Country: classic country (Clasica), western swing (Jazz)
    'classic country': 'Country', 'western swing': 'Country',
    # Bolero: la balada latina (MusicBrainz «latin ballad»: Latino; la de Wikidata ya era Bolero por «balada romantica»)
    'latin ballad': 'Bolero',
    # Clasica: el Romanticismo de la musica clasica (GenreLabel: Bolero por «romantic»)
    'romantic': 'Clasica', 'romantic classical': 'Clasica', 'romantic era': 'Clasica', 'musica del romanticismo': 'Clasica',
    'romanticismo': 'Clasica', 'romanticism': 'Clasica', 'neoromanticism': 'Clasica', 'romantische oper': 'Clasica',
    # Gotico: dark wave con espacio (GenreLabel: «darkwave» junto), deathrock (Metal por «death»)
    'dark wave': 'Gotico', 'neoclassical dark wave': 'Gotico', 'neoclassical darkwave': 'Gotico', 'deathrock': 'Gotico',
    # Industrial: death industrial (Metal por «death»)
    'death industrial': 'Industrial',
    # Metal: doomgaze (Oldies por «doo»; el resto de los «doom» va en la frase)
    'doomgaze': 'Metal',
    # Soundtrack: GenreLabel lo manda a Regional por «banda» (de «banda sonora»)
    'banda sonora': 'Soundtrack', 'bandas sonoras': 'Soundtrack', 'film score composer': 'Soundtrack', 'video game music': 'Soundtrack',
    'musica de videojuegos': 'Soundtrack',
    # Folk: lo que cae en Reggae por «ska», en Regional por «tejan», en Electronica por «dance»...
    'kan ha diskan': 'Folk', 'avtorskaya pesnya': 'Folk', 'piosenka aktorska': 'Folk', 'izvorna bosanska muzika': 'Folk',
    'euskal kantagintza berria': 'Folk', 'cante alentejano': 'Folk', 'scottish country dance': 'Folk', 'contra dance': 'Folk',
    'fife and drum': 'Folk', 'fife and drum blues': 'Folk',
    # World: Africa y Asia que caen en Reggae, Hip-Hop, Soul, Regional o Flamenco
    'maskanda': 'World', 'maskandi': 'World', 'boduberu': 'World', 'township jive': 'World', 'township bubblegum': 'World',
    'township': 'World', 'wassoulou': 'World', 'congolese rumba': 'World', 'rumba congolena': 'World', 'la rumba congolena': 'World',
    'afrobeat': 'World', 'afrobeats': 'World', 'bandari': 'World', 'haitian vodou drumming': 'World',
    'indigenous music of north america': 'World', 'musica indigena de america del norte': 'World',
    # Otros (no dan genero): nombres que no son de musica o de banda militar, y los que GenreLabel atrapa por un pedazo de palabra
    'steampunk': 'Otros', 'drumline': 'Otros', 'drum and bugle corps': 'Otros', 'military band': 'Otros',
    'banda de musica militar': 'Otros', 'brass band': 'Otros', 'banda de viento metal': 'Otros', 'bass instrument': 'Otros',
    'therapy': 'Otros', 'travesti': 'Otros', 'transvestism': 'Otros', 'travestismo': 'Otros', 'soap opera': 'Otros',
    'tap dance': 'Otros', 'multi instrumentalist': 'Otros', 'multiinstrumentista': 'Otros', 'graphical sound': 'Otros',
    'parnassianism': 'Otros', 'parnasianismo': 'Otros', 'environmentalist': 'Otros', 'ambientalista': 'Otros',
    'dark romanticism': 'Otros', 'romanticismo oscuro': 'Otros', 'romance novel': 'Otros', 'novela romantica': 'Otros',
    'romantic poetry': 'Otros', 'poesia romantica': 'Otros',
}
PREMAPEO_FRASE = {
    # (palabras enteras dentro del nombre) las familias con muchos nombres; el nombre exacto de arriba gana si es mas largo
    'hardcore': 'Punk',                    # hardcore punk y sus hijos; los de baile y el hip hop van arriba por nombre
    'doom': 'Metal',                       # «stoner doom», «black doom» (Oldies por «doo»)
    'worship': 'Cristiana',                # «contemporary worship», «praise & worship» (Hip-Hop por «hip»)
    'township': 'World',                   # «township music» (Hip-Hop por «hip»)
    'banda sonora': 'Soundtrack', 'bandas sonoras': 'Soundtrack',
    'new romantic': 'Pop', 'nuevo romanticismo': 'Pop', 'synth pop': 'Pop', 'dance pop': 'Pop', 'dancehall': 'Reggae',
    'classic rock': 'Rock', 'garage rock': 'Rock', 'tropical house': 'Electronica', 'dubstep': 'Electronica',
    'funk carioca': 'Latino', 'latin jazz': 'Jazz', 'jazz latino': 'Jazz', 'latin trap': 'Reggaeton', 'trap latino': 'Reggaeton',
    'hardcore hip hop': 'Hip-Hop', 'hip hop hardcore': 'Hip-Hop', 'hardcore techno': 'Electronica', 'happy hardcore': 'Electronica',
    'photography': 'Otros', 'fotografia': 'Otros', 'biographical': 'Otros', 'biografico': 'Otros', 'pornographic': 'Otros',
    'pornografico': 'Otros',
}


def clave_premapeo(nombre):
    """El nombre como lo compara el pre-mapeo: minusculas, sin acentos, sin «(...)», «-», «_», «/» y «&» como espacio, sin « music»
    ni «musica » de mas."""
    s = sin_acentos(re.sub(r'\([^)]*\)', ' ', nombre or ''))
    s = ' '.join(re.sub(r'[-_/&,]+', ' ', s).split())
    s = re.sub(r' music$', '', s)
    return s


def premapeo(nombre):
    """(largo, etiqueta) de la coincidencia mas larga del pre-mapeo, o None."""
    s = clave_premapeo(nombre)
    if not s:
        return None
    mejor = None
    if s in PREMAPEO_EXACTO:
        mejor = (len(s), PREMAPEO_EXACTO[s])
    for f, e in PREMAPEO_FRASE.items():
        if len(f) > (mejor[0] if mejor else 0) and re.search(r'(^| )' + re.escape(f) + r'( |$)', s):
            mejor = (len(f), e)
    return mejor


def _arregla_para_genrelabel(texto):
    """Lo que GenreLabel lee: «folk-pop», «punk-pop», «dark-pop»... no son K-Pop ni J-Pop (el «k-»/«j-» va pegado a otra palabra)."""
    return re.sub(r'(?<=[^\W\d_])([kj])-pop', r'\1 pop', texto, flags=re.I)


def genero_de(nombres, reglas):
    """La etiqueta de XP de un genero de Wikidata (nombres = (ingles, español)) o de una etiqueta de MusicBrainz (un nombre):
    primero el pre-mapeo (la coincidencia mas larga de todos los nombres), si no GenreLabel con los nombres juntos («ingles / español»,
    como siempre)."""
    mejor = None
    for n in nombres:
        p = premapeo(n)
        if p and (mejor is None or p[0] > mejor[0]):
            mejor = p
    if mejor:
        return mejor[1]
    return etiqueta_genero(_arregla_para_genrelabel(' / '.join(n for n in nombres if n).strip(' /')), reglas)


def letra_xp(nombre):
    """FirstLetter de XP (Form1.Biblioteca.cs): primer caracter sin espacios, en mayuscula, sin acento (FormD); A-Z o «#»."""
    if not nombre:
        return '#'
    n = nombre.strip()
    if not n:
        return '#'
    c = n[0]
    if ord(c) > 0xFFFF:             # en C# n[0] seria medio par sustituto: no es letra
        return '#'
    u = c.upper()
    c = u if len(u) == 1 else c
    for d in unicodedata.normalize('NFD', c):
        if unicodedata.category(d) != 'Mn':
            u = d.upper()
            c = u if len(u) == 1 else d
            break
    return c if 'A' <= c <= 'Z' else '#'


def es_latino(nombre):
    """Que se pueda leer en la rocola: al menos la mitad de sus letras son latinas (y tiene alguna)."""
    letras = [c for c in nombre if c.isalpha()]
    if not letras:
        return False
    lat = sum(1 for c in letras if unicodedata.name(c, '').startswith('LATIN'))
    return lat * 2 >= len(letras)


def sin_acentos(s):
    return ' '.join(''.join(c for c in unicodedata.normalize('NFD', s) if unicodedata.category(c) != 'Mn').casefold().split())


def limpio(s):
    """Un nombre sin tabuladores ni saltos de linea (el formato los usa)."""
    return ' '.join(s.replace('\t', ' ').replace('\r', ' ').replace('\n', ' ').split())


# =====================================================================================================================================
# LEER LAS TABLAS (texto de COPY de PostgreSQL: tabuladores, \N = nulo, barras invertidas escapadas)
# =====================================================================================================================================
_ESC = {'t': '\t', 'n': '\n', 'r': '\r', 'b': '\b', 'f': '\f', 'v': '\v', '\\': '\\'}


def _desescapa(v):
    if v == '\\N':
        return None
    if '\\' not in v:
        return v
    return re.sub(r'\\(.)', lambda m: _ESC.get(m.group(1), m.group(1)), v)


def filas(carpeta, tabla, crudas=False, opcional=False):
    """Las filas de una tabla como listas de campos. crudas=True no desescapa (para columnas numericas: mas rapido)."""
    ruta = os.path.join(carpeta, tabla)
    if not os.path.exists(ruta):
        if opcional:
            return
        raise SystemExit('Falta la tabla %s en %s' % (tabla, carpeta))
    n = len(ESQUEMA[tabla])
    with open(ruta, encoding='utf-8', newline='\n') as f:
        for i, linea in enumerate(f):
            c = linea.rstrip('\n').split('\t')
            if len(c) != n:
                raise SystemExit('%s, fila %d: %d columnas y se esperan %d (%s): ¿cambio la tabla en MusicBrainz?'
                                 % (tabla, i + 1, len(c), n, ' '.join(ESQUEMA[tabla])))
            yield c if crudas else [_desescapa(v) for v in c]


def col(tabla, nombre):
    return ESQUEMA[tabla].index(nombre)


# =====================================================================================================================================
# extrae: las tablas que se usan, de un tar de la copia leido una vez por la entrada estandar
# =====================================================================================================================================
def _filtro(tabla):
    """Filtro de filas al extraer (sobre bytes: la fila entera con su salto de linea)."""
    if tabla == 'url':
        return lambda linea: b'open.spotify.com/' in linea
    if tabla == 'artist_credit':
        i = col('artist_credit', 'artist_count')
        return lambda linea: linea.split(b'\t', i + 1)[i] == b'1'
    if tabla == 'release_group':
        i = col('release_group', 'type')
        return lambda linea: linea.split(b'\t', i + 1)[i] == b'1'   # 1 = Album (arma lo comprueba con release_group_primary_type)
    return None


def _lineas(f, trozo=1 << 22):
    """Las lineas (bytes, con su \\n) de un archivo que solo se puede leer hacia delante."""
    resto = b''
    while True:
        b = f.read(trozo)
        if not b:
            break
        b = resto + b
        corte = b.rfind(b'\n') + 1
        resto = b[corte:]
        for l in b[:corte].split(b'\n')[:-1]:
            yield l + b'\n'
    if resto:
        yield resto + b'\n'


def orden_extrae(a):
    os.makedirs(a.salida, exist_ok=True)
    quiero = set(ESQUEMA)
    if a.solo_cc0:
        quiero -= set(TABLAS_CC_BY_NC_SA)
    t0 = time.time()
    entrada = sys.stdin.buffer
    with tarfile.open(fileobj=entrada, mode='r|') as tar:
        for m in tar:
            base = m.name.split('/')[-1]
            if m.name == 'TIMESTAMP' or base == 'TIMESTAMP':
                f = tar.extractfile(m)
                if f is not None:
                    with open(os.path.join(a.salida, 'TIMESTAMP'), 'wb') as g:
                        g.write(f.read())
                continue
            if not m.isfile() or not m.name.startswith('mbdump/') or base not in quiero:
                continue
            f = tar.extractfile(m)
            filtro = _filtro(base)
            n = guardadas = 0
            t1 = time.time()
            with open(os.path.join(a.salida, base), 'wb') as g:
                for linea in _lineas(f):
                    n += 1
                    if filtro is None or filtro(linea):
                        g.write(linea)
                        guardadas += 1
            dice('extrae %-45s %10d filas, %10d guardadas, %4.0f s' % (base, n, guardadas, time.time() - t1))
    dice('extrae: %.0f s' % (time.time() - t0))


# =====================================================================================================================================
# RED: Wikidata (SPARQL) y ListenBrainz, con reintentos y cache en archivos TSV (una corrida cortada sigue donde iba)
# =====================================================================================================================================
def _pide(url, datos, cabeceras, intentos=8, espera_429=30.0):
    ultimo = None
    for intento in range(intentos):
        req = urllib.request.Request(url, data=datos, headers=dict(cabeceras, **{'User-Agent': AGENTE}))
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                return r.read(), dict(r.headers)
        except urllib.error.HTTPError as e:
            ultimo = e
            if e.code in (429, 500, 502, 503, 504):
                ra = e.headers.get('Retry-After') or e.headers.get('X-RateLimit-Reset-In')
                try:
                    espera = max(1.0, float(ra))
                except (TypeError, ValueError):
                    espera = espera_429 if e.code == 429 else 5.0 * (intento + 1)
                dice('  %s contesto %d: espero %.0f s' % (urllib.parse.urlsplit(url).netloc, e.code, espera))
                time.sleep(espera)
                continue
            raise
        except (urllib.error.URLError, OSError) as e:
            ultimo = e
            time.sleep(5.0 * (intento + 1))
    raise RuntimeError('sin respuesta de %s: %s' % (url, ultimo))


def _sparql(consulta, intentos=4):
    """Una consulta SPARQL en TSV. Una respuesta cortada (sin el ultimo salto de linea o con una fila a medias) se vuelve a pedir."""
    ultimo = None
    for intento in range(intentos):
        cuerpo, _ = _pide(WIKIDATA, urllib.parse.urlencode({'query': consulta}).encode(),
                          {'Accept': 'text/tab-separated-values', 'Content-Type': 'application/x-www-form-urlencoded'})
        texto = cuerpo.decode('utf-8', errors='replace')
        if 'TimeoutException' in texto:
            raise RuntimeError('Wikidata: la consulta paso de 60 s')
        lineas = texto.split('\n')
        cab = [c.lstrip('?') for c in lineas[0].split('\t')]
        out, mala = [], None
        for l in lineas[1:]:
            if not l:
                continue
            c = l.split('\t')
            if len(c) != len(cab) or any(v.startswith('"') and not re.search(r'"(@[\w-]+|\^\^<[^>]*>)?$', v) for v in c):
                mala = l
                break
            out.append({k: _rdf(v) for k, v in zip(cab, c)})
        if mala is None and texto.endswith('\n'):
            return out
        ultimo = 'respuesta cortada (%r)' % (mala or lineas[-1])[:80]
        dice('  Wikidata: %s; otra vez' % ultimo)
        time.sleep(10 * (intento + 1))
    raise RuntimeError('Wikidata: ' + ultimo)


def _rdf(v):
    """Un valor del TSV de SPARQL: "texto"@es, "1"^^<...>, <http://www.wikidata.org/entity/Q1> o nada."""
    if not v:
        return None
    if v.startswith('<'):
        return v[1:-1].rsplit('/', 1)[-1]
    m = re.match(r'^"(.*)"(?:@[\w-]+|\^\^<[^>]*>)?$', v, re.S)
    if m:
        return re.sub(r'\\u([0-9a-fA-F]{4})|\\U([0-9a-fA-F]{8})|\\(.)',
                      lambda x: chr(int(x.group(1) or x.group(2), 16)) if (x.group(1) or x.group(2)) else
                      {'t': '\t', 'n': '\n', 'r': '\r', '"': '"', '\\': '\\'}.get(x.group(3), x.group(3)), m.group(1))
    return v


class Cache:
    """Un TSV de clave -> campos, que se va escribiendo al preguntar (cada linea: clave TAB campo TAB campo...)."""

    def __init__(self, ruta, n):
        self.ruta, self.n, self.d = ruta, n, {}
        if ruta and os.path.exists(ruta):
            with open(ruta, encoding='utf-8') as f:
                for l in f:
                    c = l.rstrip('\n').split('\t')
                    if len(c) == n + 1:
                        self.d[c[0]] = c[1:]

    def pon(self, filas_nuevas):
        if not filas_nuevas:
            return
        for k, v in filas_nuevas:
            self.d[k] = v
        if self.ruta:
            with open(self.ruta, 'a', encoding='utf-8') as f:
                for k, v in filas_nuevas:
                    f.write('\t'.join([k] + [limpio(x) for x in v]) + '\n')


def wikidata_spotify(cache, sin_red):
    """Todos los artistas de Wikidata con P434 (MusicBrainz) y P1902 (Spotify): una consulta (~2 s, 61 514 filas el 14-11-2026)."""
    if cache.d or sin_red:
        return
    dice('Wikidata: P434 + P1902...')
    nuevas = {}
    for r in _sparql('SELECT ?mbid ?sp WHERE { ?item wdt:P1902 ?sp ; wdt:P434 ?mbid . }'):
        if r.get('mbid') and r.get('sp') and ES_MBID.match(r['mbid']) and ES_SPOTIFY_ID.match(r['sp']):
            nuevas.setdefault(r['mbid'], set()).add(r['sp'])
    cache.pon([(k, [','.join(sorted(v))]) for k, v in sorted(nuevas.items())])
    if not cache.d:
        cache.pon([('-', ['-'])])   # marca de «preguntado y vacio»
    dice('Wikidata: %d artistas con Spotify' % len(nuevas))


def wikidata_artistas(mbids, cache, sin_red):
    """Por artista: Wikipedias (sitelinks, la mayor si hay varios) y generos P136 (QID). Por lotes con VALUES."""
    faltan = sorted(m for m in mbids if m not in cache.d)
    if not faltan or sin_red:
        return
    dice('Wikidata: %d artistas por preguntar (lotes de %d)' % (len(faltan), LOTE_WIKIDATA))
    i, lote = 0, LOTE_WIKIDATA
    while i < len(faltan):
        parte = faltan[i:i + lote]
        q = ('SELECT ?mbid ?sl ?g WHERE { VALUES ?mbid { %s } ?item wdt:P434 ?mbid ; wikibase:sitelinks ?sl . '
             'OPTIONAL { ?item wdt:P136 ?g } }' % ' '.join('"%s"' % m for m in parte))
        try:
            res = _sparql(q)
        except RuntimeError as e:
            if lote > 50:
                lote //= 2
                dice('  Wikidata fallo (%s): lotes de %d' % (e, lote))
                continue
            raise
        sl, gen = {}, {}
        for r in res:
            m = r.get('mbid')
            if not m:
                continue
            try:
                sl[m] = max(sl.get(m, 0), int(r.get('sl') or 0))
            except ValueError:
                pass
            if r.get('g') and re.match(r'^Q\d+$', r['g']):
                gen.setdefault(m, set()).add(r['g'])
        cache.pon([(m, [str(sl.get(m, 0)), ','.join(sorted(gen.get(m, ()), key=lambda q: int(q[1:])))]) for m in parte])
        i += len(parte)
        if (i // lote) % 20 == 0:
            dice('  Wikidata: %d de %d' % (i, len(faltan)))


def wikidata_generos(qids, cache, sin_red):
    """El nombre en ingles y en español de cada genero de Wikidata."""
    faltan = sorted((q for q in qids if q not in cache.d), key=lambda q: int(q[1:]))
    if not faltan or sin_red:
        return
    dice('Wikidata: nombres de %d generos' % len(faltan))
    for i in range(0, len(faltan), 400):
        parte = faltan[i:i + 400]
        q = ('SELECT ?g ?en ?es WHERE { VALUES ?g { %s } OPTIONAL { ?g rdfs:label ?en FILTER(LANG(?en) = "en") } '
             'OPTIONAL { ?g rdfs:label ?es FILTER(LANG(?es) = "es") } }' % ' '.join('wd:' + x for x in parte))
        nombres = {}
        for r in _sparql(q):
            if r.get('g'):
                nombres[r['g']] = [r.get('en') or '', r.get('es') or '']
        cache.pon([(x, nombres.get(x, ['', ''])) for x in parte])


def listenbrainz(tipo, mbids, cache, sin_red):
    """Escuchas y oyentes de ListenBrainz por lotes (POST /1/popularity/artist o release-group). Respeta X-RateLimit-*."""
    faltan = sorted(m for m in mbids if m not in cache.d)
    if not faltan or sin_red:
        return
    clave = {'artist': 'artist_mbids', 'release-group': 'release_group_mbids'}[tipo]
    devuelve = {'artist': 'artist_mbid', 'release-group': 'release_group_mbid'}[tipo]
    dice('ListenBrainz %s: %d por preguntar' % (tipo, len(faltan)))
    i, lote, t0 = 0, LOTE_LISTENBRAINZ, time.time()
    while i < len(faltan):
        parte = faltan[i:i + lote]
        try:
            cuerpo, cab = _pide(LISTENBRAINZ + tipo, json.dumps({clave: parte}).encode(), {'Content-Type': 'application/json'},
                                intentos=4)
            datos = json.loads(cuerpo)
        except (RuntimeError, ValueError, urllib.error.HTTPError) as e:
            if lote > 50:
                lote //= 2
                dice('  ListenBrainz fallo (%s): lotes de %d' % (e, lote))
                continue
            raise
        res = {}
        for r in datos:
            res[r.get(devuelve)] = (r.get('total_listen_count') or 0, r.get('total_user_count') or 0)
        cache.pon([(m, [str(int(res.get(m, (0, 0))[0])), str(int(res.get(m, (0, 0))[1]))]) for m in parte])
        i += len(parte)
        cab = {k.lower(): v for k, v in cab.items()}
        try:
            if int(cab.get('x-ratelimit-remaining', '1')) <= 1:
                time.sleep(max(1.0, float(cab.get('x-ratelimit-reset-in', '10'))))
        except ValueError:
            pass
        if (i // max(lote, 1)) % 50 == 0:
            dice('  ListenBrainz %s: %d de %d (%.0f s)' % (tipo, i, len(faltan), time.time() - t0))


# =====================================================================================================================================
# arma
# =====================================================================================================================================
def fecha_copia(carpeta, forzada=None):
    if forzada:
        return forzada
    try:
        with open(os.path.join(carpeta, 'TIMESTAMP')) as f:
            m = re.match(r'(\d{4}-\d{2}-\d{2})', f.read().strip())
            if m:
                return m.group(1)
    except OSError:
        pass
    raise SystemExit('No se la fecha de la copia: falta TIMESTAMP en %s (o --fecha AAAA-MM-DD)' % carpeta)


def _entero(v, defecto=0):
    try:
        return int(v)
    except (TypeError, ValueError):
        return defecto


def _fecha(y, m, d):
    """AAAAMMDD para ordenar (lo que falta, al final del año o del mes)."""
    y = _entero(y)
    if not y:
        return None
    return y * 10000 + (_entero(m) or 12) * 100 + (_entero(d) or 31)


def fama(escuchas, oyentes, wikipedias, pais, genero):
    base = (PESO_ESCUCHAS * min(1.0, math.log10(1 + escuchas) / LOG_ESCUCHAS_TOPE)
            + PESO_OYENTES * min(1.0, math.log10(1 + oyentes) / LOG_OYENTES_TOPE)
            + PESO_WIKIPEDIAS * min(1.0, math.log10(1 + wikipedias) / LOG_WIKIPEDIAS_TOPE))
    fp = FACTOR_MEXICO if pais == 'MX' else (FACTOR_HISPANO if pais in PAISES_HISPANOS else 1.0)
    fg = FACTOR_GENERO.get(genero, 1.0)
    maximo = max(FACTOR_MEXICO, FACTOR_HISPANO, 1.0) * max(list(FACTOR_GENERO.values()) + [1.0])
    v = int(base * fp * fg / maximo * 1000000 + 0.5)
    return max(0, min(1000000, v))


def orden_arma(a):
    global TOPE_DISCOS_LETRA
    if a.tope:
        TOPE_DISCOS_LETRA = a.tope      # solo para la prueba de los topes
    t0 = time.time()
    tablas = a.tablas
    reglas = lee_reglas_genero(a.xp)
    orden_regla = {e: i for i, (e, _) in enumerate(reglas)}
    dice('GenreLabel: %d reglas, %d etiquetas' % (len(reglas), len(orden_regla)))
    copia = fecha_copia(tablas, a.fecha)
    os.makedirs(a.salida, exist_ok=True)
    cache_dir = a.cache or a.salida
    os.makedirs(cache_dir, exist_ok=True)
    pasos = []

    def paso(nombre, t):
        pasos.append((nombre, time.time() - t))
        dice('%s: %.1f s' % (nombre, time.time() - t))

    # ---- 1. Spotify en MusicBrainz: url -> id; artistas y ediciones con su enlace
    t = time.time()
    url_sp = {}
    iu, iurl = col('url', 'id'), col('url', 'url')
    for c in filas(tablas, 'url'):
        m = ES_SPOTIFY.search(c[iurl] or '')
        if m:
            url_sp[int(c[iu])] = (m.group(1), m.group(2))
    sp_artista_mb, sp_edicion = {}, {}
    for tabla, tipo, destino in (('l_artist_url', 'artist', sp_artista_mb), ('l_release_url', 'album', sp_edicion)):
        i0, i1 = col(tabla, 'entity0'), col(tabla, 'entity1')
        for c in filas(tablas, tabla, crudas=True):
            u = url_sp.get(int(c[i1]))
            if u and u[0] == tipo:
                destino.setdefault(int(c[i0]), set()).add(u[1])
    del url_sp
    paso('Spotify en MusicBrainz (%d artistas, %d ediciones)' % (len(sp_artista_mb), len(sp_edicion)), t)

    # ---- 2. Spotify en Wikidata (P1902)
    t = time.time()
    c_sp = Cache(os.path.join(cache_dir, 'wd_spotify.tsv'), 1)
    wikidata_spotify(c_sp, a.sin_red)
    sp_wd = {k: v[0].split(',') for k, v in c_sp.d.items() if k != '-'}
    paso('Spotify en Wikidata (%d artistas)' % len(sp_wd), t)

    # ---- 3. Los artistas con Spotify
    t = time.time()
    artistas = {}      # id -> dict
    A = ESQUEMA['artist']
    ia, ig, inom, iarea, ibarea = A.index('id'), A.index('gid'), A.index('name'), A.index('area'), A.index('begin_area')
    total_artistas = 0
    for c in filas(tablas, 'artist'):
        total_artistas += 1
        aid = int(c[ia])
        gid = c[ig]
        sp = None
        if aid in sp_artista_mb:
            ids = sorted(sp_artista_mb[aid])
            comun = [x for x in ids if x in sp_wd.get(gid, ())]
            sp = (comun or ids)[0]
        elif gid in sp_wd:
            sp = sorted(sp_wd[gid])[0]
        if sp:
            artistas[aid] = {'gid': gid, 'nombre': limpio(c[inom] or ''), 'area': _entero(c[iarea]) or _entero(c[ibarea]),
                             'sp': sp}
    del sp_artista_mb, sp_wd
    paso('Artistas con Spotify: %d de %d' % (len(artistas), total_artistas), t)

    # ---- 4. Nombre: sin letras latinas, su alias principal (primary_for_locale) en español o ingles; si no, uno no principal
    t = time.time()
    sin_latino = {aid for aid, x in artistas.items() if not es_latino(x['nombre'])}
    mejor_alias = {}
    if sin_latino:
        AA = ESQUEMA['artist_alias']
        iart, inom2, iloc, itip, iprim, iid = (AA.index('artist'), AA.index('name'), AA.index('locale'), AA.index('type'),
                                               AA.index('primary_for_locale'), AA.index('id'))
        for c in filas(tablas, 'artist_alias'):
            aid = int(c[iart])
            if aid not in sin_latino:
                continue
            loc = (c[iloc] or '').lower()
            idioma = loc.split('_')[0]
            if idioma not in ('es', 'en'):
                continue
            nombre = limpio(c[inom2] or '')
            if not nombre or not es_latino(nombre):
                continue
            clave = (0 if c[iprim] == 't' else 1, 0 if idioma == 'es' else 1, 0 if c[itip] == '1' else 1, int(c[iid]))
            if aid not in mejor_alias or clave < mejor_alias[aid][0]:
                mejor_alias[aid] = (clave, nombre)
    fuera_nombre = 0
    for aid in sin_latino:
        if aid in mejor_alias:
            artistas[aid]['nombre'] = mejor_alias[aid][1]
        else:
            del artistas[aid]
            fuera_nombre += 1
    paso('Nombres: %d sin letras latinas, %d con alias, %d fuera' % (len(sin_latino), len(mejor_alias), fuera_nombre), t)

    # ---- 5. Los discos: grupos de lanzamiento Album de un solo artista (el mismo), sin los tipos secundarios de fuera
    t = time.time()
    tipo_album = {c[1]: int(c[0]) for c in filas(tablas, 'release_group_primary_type')}.get(TIPO_PRINCIPAL)
    if tipo_album is None:
        raise SystemExit('No encuentro el tipo principal %s' % TIPO_PRINCIPAL)
    sec_fuera = {int(c[0]) for c in filas(tablas, 'release_group_secondary_type') if c[1] in TIPOS_SECUNDARIOS_FUERA}
    if len(sec_fuera) != len(TIPOS_SECUNDARIOS_FUERA):
        raise SystemExit('Faltan tipos secundarios en MusicBrainz: %d de %d' % (len(sec_fuera), len(TIPOS_SECUNDARIOS_FUERA)))
    credito_uno = set()
    for c in filas(tablas, 'artist_credit', crudas=True):
        if c[2] == '1':
            credito_uno.add(int(c[0]))
    credito = {}
    ic, iar = col('artist_credit_name', 'artist_credit'), col('artist_credit_name', 'artist')
    for c in filas(tablas, 'artist_credit_name', crudas=True):
        k = int(c[ic])
        if k in credito_uno:
            ar = int(c[iar])
            if ar in artistas:
                credito[k] = ar
    del credito_uno
    discos = {}        # id del grupo -> dict
    R = ESQUEMA['release_group']
    irg, irgid, irgn, irgac, irgt = R.index('id'), R.index('gid'), R.index('name'), R.index('artist_credit'), R.index('type')
    fuera_disco_nombre = 0
    for c in filas(tablas, 'release_group'):
        if _entero(c[irgt], -1) != tipo_album:
            continue
        ar = credito.get(int(c[irgac]))
        if ar is None:
            continue
        nombre = limpio(c[irgn] or '')
        if not nombre or not es_latino(nombre):
            fuera_disco_nombre += 1
            continue
        discos[int(c[irg])] = {'gid': c[irgid], 'nombre': nombre, 'artista': ar, 'ediciones': 0, 'fecha': None, 'sp': None,
                               'portada': 0}
    del credito
    quita = 0
    for c in filas(tablas, 'release_group_secondary_type_join', crudas=True):
        if int(c[1]) in sec_fuera and discos.pop(int(c[0]), None) is not None:
            quita += 1
    paso('Discos Album de un artista con Spotify: %d (%d por tipo secundario, %d sin nombre latino)'
         % (len(discos), quita, fuera_disco_nombre), t)

    # ---- 6. Ediciones: cuantas, la primera fecha, el disco de Spotify (la oficial mas vieja) y la portada
    t = time.time()
    oficial = {c[1]: int(c[0]) for c in filas(tablas, 'release_status')}.get('Official')
    edicion = {}       # id de la edicion -> (grupo, oficial?)
    E = ESQUEMA['release']
    ie, irgr, ist = E.index('id'), E.index('release_group'), E.index('status')
    for c in filas(tablas, 'release', crudas=True):
        g = int(c[irgr])
        d = discos.get(g)
        if d is None:
            continue
        d['ediciones'] += 1
        edicion[int(c[ie])] = (g, c[ist] != '\\N' and int(c[ist]) == oficial)
    fecha_ed = {}
    for tabla, iy in (('release_country', 2), ('release_unknown_country', 1)):
        for c in filas(tablas, tabla, crudas=True):
            e = int(c[0])
            if e not in edicion:
                continue
            f = _fecha(*[None if v == '\\N' else v for v in c[iy:iy + 3]])
            if f and (e not in fecha_ed or f < fecha_ed[e]):
                fecha_ed[e] = f
    for e, (g, _) in edicion.items():
        f = fecha_ed.get(e)
        d = discos[g]
        if f and (d['fecha'] is None or f < d['fecha']):
            d['fecha'] = f
    elegido = {}
    for e, ids in sp_edicion.items():
        if e not in edicion:
            continue
        g, ofi = edicion[e]
        clave = (0 if ofi else 1, fecha_ed.get(e) or 99999999, e)
        if g not in elegido or clave < elegido[g][0]:
            elegido[g] = (clave, sorted(ids)[0])
    for g, (_, sp) in elegido.items():
        discos[g]['sp'] = sp
    del sp_edicion, elegido
    # Cover Art Archive: el grupo tiene portada si se le eligio una o si alguna de sus ediciones tiene imagen de frente
    caa = os.path.exists(os.path.join(tablas, 'cover_art_archive.cover_art'))
    if caa:
        frente_tipo = {c[1]: int(c[0]) for c in filas(tablas, 'cover_art_archive.art_type')}.get('Front')
        imagen_ed = {}
        for c in filas(tablas, 'cover_art_archive.cover_art', crudas=True):
            e = int(c[1])
            if e in edicion:
                imagen_ed[int(c[0])] = e
        for c in filas(tablas, 'cover_art_archive.cover_art_type', crudas=True):
            if int(c[1]) == frente_tipo:
                e = imagen_ed.get(int(c[0]))
                if e is not None:
                    discos[edicion[e][0]]['portada'] = 1
        for c in filas(tablas, 'cover_art_archive.release_group_cover_art', crudas=True, opcional=True):
            d = discos.get(int(c[0]))
            if d is not None:
                d['portada'] = 1
        del imagen_ed
    else:
        dice('AVISO: sin las tablas del Cover Art Archive: ningun disco lleva portada')
    sin_ediciones = [g for g, d in discos.items() if d['ediciones'] == 0]
    for g in sin_ediciones:
        del discos[g]
    del edicion, fecha_ed
    paso('Ediciones, fechas, Spotify y portadas (%d discos con id de Spotify, %d con portada)'
         % (sum(1 for d in discos.values() if d['sp']), sum(d['portada'] for d in discos.values())), t)

    # ---- 7. Quien tiene discos
    por_artista = {}
    for g, d in discos.items():
        por_artista.setdefault(d['artista'], []).append(g)
    sin_discos = len(artistas) - len(por_artista)
    for aid in [x for x in artistas if x not in por_artista]:
        del artistas[aid]

    # ---- 8. El genero: Wikidata (P136) y, de respaldo, las etiquetas de MusicBrainz que son generos
    t = time.time()
    c_wd = Cache(os.path.join(cache_dir, 'wd_artistas.tsv'), 2)
    wikidata_artistas([x['gid'] for x in artistas.values()], c_wd, a.sin_red)
    qids = set()
    for x in artistas.values():
        v = c_wd.d.get(x['gid'])
        if v and v[1]:
            qids.update(v[1].split(','))
    c_gen = Cache(os.path.join(cache_dir, 'wd_generos.tsv'), 2)
    wikidata_generos(qids, c_gen, a.sin_red)
    etiqueta_q = {q: genero_de((v[0], v[1]), reglas) for q, v in c_gen.d.items()}
    tags = {}
    if not a.solo_cc0:
        generos_mb = {(c[2] or '').lower() for c in filas(tablas, 'genre')}
        nombre_tag = {}
        for c in filas(tablas, 'tag'):
            n = (c[1] or '').lower()
            if n in generos_mb:
                nombre_tag[int(c[0])] = n
        for c in filas(tablas, 'artist_tag', crudas=True):
            aid = int(c[0])
            if aid in artistas:
                n = nombre_tag.get(int(c[1]))
                votos = _entero(c[2])
                if n and votos > 0:
                    tags.setdefault(aid, []).append((n, votos))
    fuente = {'w': 0, 'm': 0}
    sin_genero = 0
    for aid in sorted(artistas):
        x = artistas[aid]
        peso = {}
        v = c_wd.d.get(x['gid'])
        x['wikipedias'] = _entero(v[0]) if v else 0
        if v and v[1]:
            for q in v[1].split(','):
                e = etiqueta_q.get(q, 'Otros')
                if e != 'Otros':
                    peso[e] = peso.get(e, 0) + 1
        x['fuente'] = 'w'
        if not peso and aid in tags:
            for n, votos in tags[aid]:
                e = genero_de((n,), reglas)
                if e != 'Otros':
                    peso[e] = peso.get(e, 0) + votos
            x['fuente'] = 'm'
        if not peso:
            del artistas[aid]
            sin_genero += 1
            continue
        x['genero'] = min(peso, key=lambda e: (-peso[e], orden_regla[e]))
        fuente[x['fuente']] += 1
    del tags
    paso('Generos: %d por Wikidata, %d por etiquetas de MusicBrainz, %d sin genero' % (fuente['w'], fuente['m'], sin_genero), t)

    # ---- 9. El pais: el area del artista (pais directo, prefijo ISO 3166-2, o subiendo por «part of» hasta 3 saltos)
    t = time.time()
    pais_area = {int(c[0]): c[1] for c in filas(tablas, 'iso_3166_1')}
    sub_area = {int(c[0]): c[1].split('-')[0] for c in filas(tablas, 'iso_3166_2')}
    parte_tipo = {int(c[0]) for c in filas(tablas, 'link_type') if c[3] == PARTE_DE_GID}
    enlace_parte = {int(c[0]) for c in filas(tablas, 'link', crudas=True) if int(c[1]) in parte_tipo}
    padre = {}
    for c in filas(tablas, 'l_area_area', crudas=True):
        if int(c[1]) in enlace_parte:
            hijo, pa = int(c[3]), int(c[2])
            if hijo not in padre or pa < padre[hijo]:
                padre[hijo] = pa

    def pais_de(area):
        for _ in range(4):
            if not area:
                return ''
            if area in pais_area:
                return pais_area[area]
            if area in sub_area:
                return sub_area[area]
            area = padre.get(area)
        return ''

    for x in artistas.values():
        x['pais'] = pais_de(x['area'])
    paso('Paises', t)

    # ---- 10. ListenBrainz: escuchas de artistas y discos
    t = time.time()
    c_lba = Cache(os.path.join(cache_dir, 'lb_artistas.tsv'), 2)
    listenbrainz('artist', [x['gid'] for x in artistas.values()], c_lba, a.sin_red)
    c_lbd = Cache(os.path.join(cache_dir, 'lb_discos.tsv'), 2)
    listenbrainz('release-group', [discos[g]['gid'] for aid in artistas for g in por_artista[aid]], c_lbd, a.sin_red)
    paso('ListenBrainz', t)

    # ---- 11. Los discos de cada artista (hasta 6) y su fama
    t = time.time()
    for aid, x in artistas.items():
        lista = []
        for g in por_artista[aid]:
            d = discos[g]
            v = c_lbd.d.get(d['gid'])
            d['escuchas'] = _entero(v[0]) if v else 0
            lista.append(d)
        lista.sort(key=lambda d: (-d['escuchas'], -(1 if d['sp'] else 0), -d['portada'], -d['ediciones'],
                                  d['fecha'] or 99999999, sin_acentos(d['nombre']), d['nombre'], d['gid']))
        vistos, elegidos = set(), []
        for d in lista:
            k = sin_acentos(d['nombre'])
            if k in vistos:
                continue
            vistos.add(k)
            elegidos.append(d)
            if len(elegidos) == DISCOS_POR_ARTISTA:
                break
        x['discos'] = elegidos
        v = c_lba.d.get(x['gid'])
        x['escuchas'] = _entero(v[0]) if v else 0
        x['oyentes'] = _entero(v[1]) if v else 0
        x['fama'] = fama(x['escuchas'], x['oyentes'], x['wikipedias'], x['pais'], x['genero'])
        x['letra'] = letra_xp(x['nombre'])
    paso('Discos y fama', t)

    # ---- 12. Por letra: del mas al menos famoso hasta el tope, sin que un genero pase de su parte
    t = time.time()
    tope_genero = int(MAX_GENERO_LETRA * TOPE_DISCOS_LETRA)
    por_letra = {l: [] for l in LETRAS}
    for x in artistas.values():
        por_letra[x['letra']].append(x)
    salida = {}
    cuentas = {}       # (letra, genero) -> [artistas dentro, discos dentro, artistas fuera, discos fuera]
    for l in LETRAS:
        lista = sorted(por_letra[l], key=lambda x: (-x['fama'], sin_acentos(x['nombre']), x['nombre'], x['gid']))
        total, por_gen, dentro = 0, {}, []
        for x in lista:
            k = cuentas.setdefault((l, x['genero']), [0, 0, 0, 0])
            hueco = min(TOPE_DISCOS_LETRA - total, tope_genero - por_gen.get(x['genero'], 0))
            if hueco <= 0:
                k[2] += 1
                k[3] += len(x['discos'])
                continue
            toma = x['discos'][:hueco]
            if len(toma) < len(x['discos']):
                k[3] += len(x['discos']) - len(toma)
            x['discos'] = toma
            total += len(toma)
            por_gen[x['genero']] = por_gen.get(x['genero'], 0) + len(toma)
            k[0] += 1
            k[1] += len(toma)
            dentro.append(x)
        salida[l] = dentro
    paso('Topes por letra', t)

    # ---- 13. Escribir
    t = time.time()
    datos = texto_catalogo(salida, copia)
    gz = os.path.join(a.salida, 'rocola_catalogo.tsv.gz')
    escribe_gz(gz, datos)
    n_art = sum(len(v) for v in salida.values())
    n_dis = sum(len(x['discos']) for v in salida.values() for x in v)
    with open(gz, 'rb') as f:
        bruto = f.read()
    fuentes = ('MusicBrainz %s (CC0)' % copia + ('' if a.solo_cc0 else '; etiquetas de MusicBrainz (CC BY-NC-SA 3.0)')
               + '; Wikidata (CC0); ListenBrainz (CC0); Cover Art Archive (solo si hay portada)')
    licencia = ('CC0 lo de MusicBrainz, Wikidata y ListenBrainz' + (
        '' if a.solo_cc0 else '; el genero de los artistas con fuente «m» sale de las etiquetas de MusicBrainz (CC BY-NC-SA 3.0, '
                              'no comercial y compartir igual)'))
    with open(os.path.join(a.salida, 'rocola_catalogo.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('formato=%d\nversion=%s.%d\nfecha=%s\nsha256=%s\nbytes=%d\nartistas=%d\ndiscos=%d\nfuentes=%s\nlicencia=%s\n'
                % (FORMATO, copia.replace('-', ''), a.corrida, a.hoy or time.strftime('%Y-%m-%d', time.gmtime()),
                   hashlib.sha256(bruto).hexdigest(), len(bruto), n_art, n_dis, fuentes, licencia))
    paso('Escribir', t)

    informe = []
    w = informe.append
    w('INFORME DEL CATALOGO DE LA ROCOLA (formato %d)' % FORMATO)
    w('Copia de MusicBrainz: %s; %s' % (copia, 'solo CC0 (--solo-cc0)' if a.solo_cc0 else 'con etiquetas de MusicBrainz de respaldo'))
    w('Artistas: %d; discos: %d; .tsv.gz de %d bytes' % (n_art, n_dis, len(bruto)))
    w('')
    w('DE DONDE SALEN')
    w('  artistas en MusicBrainz: %d; con Spotify (MusicBrainz o Wikidata): %d' % (total_artistas, len(artistas) + sin_genero
                                                                                       + sin_discos + fuera_nombre))
    w('  fuera por el nombre (sin letras latinas ni alias): %d; sin discos que valgan: %d; sin genero: %d'
      % (fuera_nombre, sin_discos, sin_genero))
    w('  genero por Wikidata (w): %d; por etiquetas de MusicBrainz (m): %d' % (fuente['w'], fuente['m']))
    dentro_fuente = {'w': 0, 'm': 0}
    for v in salida.values():
        for x in v:
            dentro_fuente[x['fuente']] += 1
    w('  de los que entraron: w %d, m %d' % (dentro_fuente['w'], dentro_fuente['m']))
    w('  con pais: %d (MX %d; habla hispana %d)' % (sum(1 for x in artistas.values() if x['pais']),
                                                  sum(1 for x in artistas.values() if x['pais'] == 'MX'),
                                                  sum(1 for x in artistas.values() if x['pais'] in PAISES_HISPANOS)))
    w('  discos que entraron con id de Spotify: %d; con portada: %d'
      % (sum(1 for v in salida.values() for x in v for d in x['discos'] if d['sp']),
         sum(d['portada'] for v in salida.values() for x in v for d in x['discos'])))
    w('')
    w('POR LETRA (artistas y discos que entraron / artistas y discos que quedaron fuera por el tope o la parte del genero)')
    for l in LETRAS:
        e = [(g, k) for (ll, g), k in cuentas.items() if ll == l]
        w('  %s  dentro %6d artistas %6d discos   fuera %6d artistas %6d discos' % (
            l, sum(k[0] for _, k in e), sum(k[1] for _, k in e), sum(k[2] for _, k in e), sum(k[3] for _, k in e)))
    w('')
    w('POR GENERO (en todas las letras)')
    gens = sorted({g for (_, g) in cuentas}, key=lambda g: orden_regla.get(g, 99))
    for g in gens:
        e = [k for (_, gg), k in cuentas.items() if gg == g]
        w('  %-12s dentro %6d artistas %6d discos   fuera %6d artistas %6d discos' % (
            g, sum(k[0] for k in e), sum(k[1] for k in e), sum(k[2] for k in e), sum(k[3] for k in e)))
    w('')
    w('POR LETRA Y GENERO (artistas/discos dentro; fuera)')
    for l in LETRAS:
        partes = []
        for g in gens:
            k = cuentas.get((l, g))
            if k:
                partes.append('%s %d/%d%s' % (g, k[0], k[1], (' (fuera %d/%d)' % (k[2], k[3])) if k[2] or k[3] else ''))
        w('  %s: %s' % (l, '; '.join(partes) if partes else '-'))
    if a.muestra:
        w('')
        w('MUESTRA PARA REVISAR A OJO')
        lugar = {}
        for l, v in salida.items():
            for i, x in enumerate(v):
                lugar[id(x)] = (l, i + 1)
        indice = {}
        for x in artistas.values():
            indice.setdefault(sin_acentos(x['nombre']), []).append(x)
        with open(a.muestra, encoding='utf-8') as f:
            nombres = [l.strip() for l in f if l.strip() and not l.startswith('#')]
        for n in nombres:
            xs = sorted(indice.get(sin_acentos(n), []), key=lambda x: -x['fama'])
            if not xs:
                w('  %-28s NO ESTA (sin Spotify, sin genero, sin discos o sin nombre latino en MusicBrainz)' % n)
                continue
            x = xs[0]
            if id(x) in lugar:
                l, pos = lugar[id(x)]
                w('  %-28s letra %s n.%d, %s (%s), fama %d, pais %s, escuchas %d, Wikipedias %d: %s' % (
                    n, l, pos, x['genero'], x['fuente'], x['fama'], x['pais'] or '-', x['escuchas'], x['wikipedias'],
                    ' | '.join('%s (%s%s%s)' % (d['nombre'], str(d['fecha'] // 10000) if d['fecha'] else 's/a',
                                               ', sp' if d['sp'] else '', ', portada' if d['portada'] else '') for d in x['discos'])))
            else:
                w('  %-28s FUERA por el tope: letra %s, %s, fama %d' % (n, x['letra'], x['genero'], x['fama']))
    w('')
    w('TIEMPOS')
    for nombre, s in pasos:
        w('  %7.1f s  %s' % (s, nombre))
    w('  %7.1f s  en total' % (time.time() - t0))
    with open(os.path.join(a.salida, 'rocola_catalogo_informe.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(informe) + '\n')
    dice('Listo: %d artistas, %d discos (%.0f s)' % (n_art, n_dis, time.time() - t0))
    errores = valida(gz, reglas, TOPE_DISCOS_LETRA)
    if errores:
        for e in errores[:50]:
            dice('NO VALE: ' + e)
        raise SystemExit(1)


def texto_catalogo(salida, copia):
    """El contrato «I8» (ver LEEME): salida = {letra: [artista, ...]} ya en orden."""
    n_art = sum(len(v) for v in salida.values())
    n_dis = sum(len(x['discos']) for v in salida.values() for x in v)
    out = ['#rocola-catalogo\t%d\t%s\t%d\t%d' % (FORMATO, copia, n_art, n_dis)]
    for l in LETRAS:
        v = salida.get(l, [])
        out.append('L\t%s\t%d\t%d' % (l, len(v), sum(len(x['discos']) for x in v)))
        for x in v:
            out.append('A\t%s\t%s\t%d\t%s\t%s\t%s' % (limpio(x['nombre']), x['genero'], x['fama'], x['sp'] or '', x['pais'] or '',
                                                     x['fuente']))
            for d in x['discos']:
                out.append('D\t%s\t%s\t%s\t%s\t%d' % (limpio(d['nombre']), str(d['fecha'] // 10000) if d['fecha'] else '',
                                                      d['sp'] or '', d['gid'], d['portada']))
    return ('\n'.join(out) + '\n').encode('utf-8')


def escribe_gz(ruta, datos):
    """gzip determinista: sin nombre de archivo y con fecha 0."""
    tmp = ruta + '.tmp'
    with open(tmp, 'wb') as f:
        with gzip.GzipFile(filename='', mode='wb', fileobj=f, compresslevel=9, mtime=0) as g:
            g.write(datos)
    os.replace(tmp, ruta)


# =====================================================================================================================================
# valida: el contrato «I8»
# =====================================================================================================================================
def valida(ruta, reglas=None, tope=TOPE_DISCOS_LETRA, parte_genero=MAX_GENERO_LETRA):
    reglas = reglas or lee_reglas_genero()
    etiquetas = {e for e, _ in reglas}
    errores = []

    def mal(n, s):
        errores.append('linea %d: %s' % (n, s))

    with open(ruta, 'rb') as f:
        bruto = f.read()
    if bruto[:2] != b'\x1f\x8b':
        return ['no es gzip']
    try:
        texto = gzip.decompress(bruto).decode('utf-8')
    except (OSError, UnicodeDecodeError) as e:
        return ['no se puede leer: %s' % e]
    if '\r' in texto:
        errores.append('lleva CR (debe ser solo LF)')
    if not texto.endswith('\n'):
        errores.append('no acaba en salto de linea')
    lineas = texto.split('\n')[:-1]
    if not lineas:
        return ['vacio']
    c = lineas[0].split('\t')
    if len(c) != 5 or c[0] != '#rocola-catalogo' or c[1] != str(FORMATO) or not re.match(r'^\d{4}-\d{2}-\d{2}$', c[2]) \
            or not c[3].isdigit() or not c[4].isdigit():
        return ['cabecera mala: %r' % lineas[0]]
    tot_a, tot_d = int(c[3]), int(c[4])
    letras_vistas = []
    n_a = n_d = 0
    letra = None
    la = ld = 0
    decl = None
    fama_ant = None
    discos_art = None
    por_gen = {}
    tope_gen = int(parte_genero * tope)

    def cierra_artista(n):
        if discos_art is not None and not 1 <= discos_art <= DISCOS_POR_ARTISTA:
            mal(n, 'artista con %d discos' % discos_art)

    def cierra_letra(n):
        if letra is None:
            return
        if (la, ld) != decl:
            mal(n, 'la letra %s dice %s y trae %d artistas, %d discos' % (letra, decl, la, ld))
        if ld > tope:
            mal(n, 'la letra %s pasa del tope: %d' % (letra, ld))
        for g, k in por_gen.items():
            if k > tope_gen:
                mal(n, 'la letra %s: %s tiene %d discos (tope %d)' % (letra, g, k, tope_gen))

    for n, l in enumerate(lineas[1:], 2):
        c = l.split('\t')
        if c[0] == 'L':
            cierra_artista(n)
            discos_art = None
            cierra_letra(n)
            if len(c) != 4 or not c[2].isdigit() or not c[3].isdigit():
                mal(n, 'L mala: %r' % l)
                continue
            letra = c[1]
            letras_vistas.append(letra)
            decl = (int(c[2]), int(c[3]))
            la = ld = 0
            fama_ant = None
            por_gen = {}
        elif c[0] == 'A':
            cierra_artista(n)
            discos_art = 0
            if letra is None:
                mal(n, 'A antes de la primera L')
                continue
            if len(c) != 7:
                mal(n, 'A con %d campos' % len(c))
                continue
            nombre, gen, fa, sp, pais, fu = c[1:]
            if not nombre or nombre != nombre.strip():
                mal(n, 'nombre vacio o con espacios a los lados: %r' % nombre)
            if letra_xp(nombre) != letra:
                mal(n, '%r va en la letra %s, no en la %s' % (nombre, letra_xp(nombre), letra))
            if gen not in etiquetas:
                mal(n, 'genero que no es de XP: %r' % gen)
            if not fa.isdigit() or not 0 <= int(fa) <= 1000000:
                mal(n, 'fama mala: %r' % fa)
            else:
                if fama_ant is not None and int(fa) > fama_ant:
                    mal(n, 'la fama sube dentro de la letra %s' % letra)
                fama_ant = int(fa)
            if sp and not ES_SPOTIFY_ID.match(sp):
                mal(n, 'id de Spotify malo: %r' % sp)
            if pais and not re.match(r'^[A-Z]{2}$', pais):
                mal(n, 'pais malo: %r' % pais)
            if fu not in ('w', 'm'):
                mal(n, 'fuente mala: %r' % fu)
            la += 1
            n_a += 1
            gen_art = gen
        elif c[0] == 'D':
            if discos_art is None:
                mal(n, 'D sin su A')
                continue
            if len(c) != 6:
                mal(n, 'D con %d campos' % len(c))
                continue
            nombre, ano, sp, gid, por = c[1:]
            if not nombre or nombre != nombre.strip():
                mal(n, 'disco sin nombre o con espacios a los lados')
            if ano and not re.match(r'^\d{4}$', ano):
                mal(n, 'año malo: %r' % ano)
            if sp and not ES_SPOTIFY_ID.match(sp):
                mal(n, 'id de disco de Spotify malo: %r' % sp)
            if not ES_MBID.match(gid):
                mal(n, 'MBID malo: %r' % gid)
            if por not in ('0', '1'):
                mal(n, 'portada mala: %r' % por)
            discos_art += 1
            ld += 1
            n_d += 1
            por_gen[gen_art] = por_gen.get(gen_art, 0) + 1
        else:
            mal(n, 'linea que no es L, A ni D: %r' % l[:80])
        if len(errores) > 200:
            break
    cierra_artista(len(lineas))
    cierra_letra(len(lineas))
    if ''.join(letras_vistas) != LETRAS:
        errores.append('letras %r (se esperan las 27 en el orden %s)' % (''.join(letras_vistas), LETRAS))
    if (n_a, n_d) != (tot_a, tot_d):
        errores.append('la cabecera dice %d artistas y %d discos; hay %d y %d' % (tot_a, tot_d, n_a, n_d))
    return errores


def orden_valida(a):
    errores = valida(a.archivo, lee_reglas_genero(a.xp), a.tope)
    for e in errores[:100]:
        print('NO VALE: ' + e)
    if errores:
        raise SystemExit(1)
    print('vale: %s' % a.archivo)


# =====================================================================================================================================
# esquema: las columnas que usa el programa contra admin/sql/CreateTables.sql (y caa/CreateTables.sql)
# =====================================================================================================================================
def columnas_sql(texto, prefijo=''):
    tablas = {}
    for m in re.finditer(r'CREATE TABLE (\w+) \(([^;]*?)\n\);', texto, re.S):
        cols = []
        for l in m.group(2).split('\n'):
            l = re.sub(r'--.*', '', l)
            r = re.match(r'^\s+([a-z_0-9]+)\s+(SERIAL|INTEGER|UUID|VARCHAR|TEXT|SMALLINT|BOOLEAN|TIMESTAMP|CHAR|BIGINT|DATE|INT|'
                         r'JSONB|REAL|BYTEA|INTERVAL|NUMERIC|POINT)\b', l, re.I)
            if r:
                cols.append(r.group(1))
        tablas[prefijo + m.group(1)] = cols
    return tablas


def orden_esquema(a):
    sql = {}
    with open(a.sql, encoding='utf-8') as f:
        sql.update(columnas_sql(f.read()))
    if a.caa:
        with open(a.caa, encoding='utf-8') as f:
            sql.update(columnas_sql(f.read(), 'cover_art_archive.'))
    mal = 0
    for t, cols in ESQUEMA.items():
        if t not in sql:
            print('%-45s no esta en el SQL dado' % t)
            if not t.startswith('cover_art_archive.') or a.caa:
                mal += 1
            continue
        if sql[t] != cols:
            print('%-45s DISTINTA: SQL %s' % (t, ' '.join(sql[t])))
            mal += 1
        else:
            print('%-45s igual (%d columnas)' % (t, len(cols)))
    if mal:
        raise SystemExit(1)


def main(argv=None):
    p = argparse.ArgumentParser(description='El catalogo de la rocola (sistema/catalogo/LEEME.md)')
    s = p.add_subparsers(dest='orden', required=True)
    e = s.add_parser('extrae')
    e.add_argument('--salida', required=True)
    e.add_argument('--solo-cc0', action='store_true')
    r = s.add_parser('arma')
    r.add_argument('--tablas', required=True, help='carpeta con las tablas (las de «extrae», o la copia inventada de la prueba)')
    r.add_argument('--salida', required=True)
    r.add_argument('--cache', help='carpeta de lo preguntado a Wikidata y ListenBrainz (si no, la de --salida)')
    r.add_argument('--sin-red', action='store_true', help='no preguntar nada: solo lo que haya en la cache')
    r.add_argument('--solo-cc0', action='store_true', help='sin las etiquetas de MusicBrainz (CC BY-NC-SA)')
    r.add_argument('--xp', help='Form1.Fondo.cs (por defecto, el de este repositorio)')
    r.add_argument('--fecha', help='fecha de la copia AAAA-MM-DD (si no, la de TIMESTAMP)')
    r.add_argument('--hoy', help='fecha= del .txt (si no, hoy)')
    r.add_argument('--corrida', type=int, default=0, help='numero de corrida para version=')
    r.add_argument('--tope', type=int, help='discos por letra (por defecto %d; otro solo para probar)' % TOPE_DISCOS_LETRA)
    r.add_argument('--muestra', help='archivo con nombres de artistas para la muestra del informe')
    v = s.add_parser('valida')
    v.add_argument('archivo')
    v.add_argument('--xp')
    v.add_argument('--tope', type=int, default=TOPE_DISCOS_LETRA)
    q = s.add_parser('esquema')
    q.add_argument('sql')
    q.add_argument('caa', nargs='?')
    a = p.parse_args(argv)
    {'extrae': orden_extrae, 'arma': orden_arma, 'valida': orden_valida, 'esquema': orden_esquema}[a.orden](a)


if __name__ == '__main__':
    main()
