#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Lo que tiene que salir del catalogo de la copia inventada (lo llama prueba.sh). Uso: comprueba.py MODO CATALOGO.tsv.gz TABLAS
MODO: normal, cc0 o tope (arma con --tope 12)."""
import gzip
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, '..'))
sys.path.insert(0, AQUI)
import arma_catalogo as ac  # noqa: E402
import inventa  # noqa: E402

fallos = []


def ok(cond, que):
    if not cond:
        fallos.append(que)


def lee(ruta):
    letras, art = {}, {}
    letra = None
    with gzip.open(ruta, 'rt', encoding='utf-8', newline='\n') as f:
        for l in f:
            c = l.rstrip('\n').split('\t')
            if c[0] == 'L':
                letra = c[1]
                letras[letra] = []
            elif c[0] == 'A':
                a = {'nombre': c[1], 'genero': c[2], 'fama': int(c[3]), 'sp': c[4], 'pais': c[5], 'fuente': c[6], 'letra': letra,
                     'discos': []}
                letras[letra].append(a)
                art.setdefault(c[1], a)
            elif c[0] == 'D':
                letras[letra][-1]['discos'].append({'nombre': c[1], 'ano': c[2], 'sp': c[3], 'gid': c[4], 'portada': c[5],
                                                    'marcas': c[6] if len(c) > 6 else ''})
    return letras, art


def edicion_de(tablas, nombre_disco):
    """Las ediciones (id, estado) del grupo de lanzamiento con ese nombre, de las tablas inventadas."""
    rg = [c[0] for c in ac.filas(tablas, 'release_group', crudas=True) if c[2] == nombre_disco]
    return [(int(c[0]), c[5]) for c in ac.filas(tablas, 'release', crudas=True) if c[4] in rg]


def main():
    modo, ruta, tablas = sys.argv[1:4]
    letras, art = lee(ruta)
    if modo in ('normal', 'cc0'):
        # letras de XP
        for n, l in (('Los Ángeles del Desierto', 'L'), ('Ángeles de Plata', 'A'), ('Ñandú Sonoro', 'N'), ('¡Ay Caramba Banda!', '#'),
                     ('3 Gallos del Valle', '#'), ('Sokol Vetra', 'S')):
            ok(n in art and art[n]['letra'] == l, '%s en la letra %s' % (n, l))
        # quien no entra
        for n in ('Звезда Без Имени', 'Сокол Ветра', 'Sin Spotify Ninguno', 'Sin Genero Alguno', 'Solo Sencillos'):
            ok(n not in art, '%s no debe entrar' % n)
        ok('Solo en Wikidata' in art, 'Spotify solo por Wikidata (P1902)')
        ok(art.get('Solo en Wikidata', {}).get('genero') == 'Latino', 'empate de generos: la regla de antes en GenreLabel')
        ok(art.get('Rockeros Ingleses', {}).get('genero') == 'Metal', 'empate Rock/Metal: Metal')
        ok(art.get('Reggaetonero Inventado', {}).get('genero') == 'Reggaeton', 'Reggaeton')
        ok(art.get('Los Ángeles del Desierto', {}).get('genero') == 'Regional', 'cumbia + regional = Regional')
        # discos
        des = [d['nombre'] for d in art['Los Ángeles del Desierto']['discos']]
        ok(len(des) == 6, 'como mucho 6 discos (hay %d)' % len(des))
        ok(not any('Remezclas' in d or 'Sencillo' in d for d in des), 'sin Remix ni sencillos')
        ok(des[0] == 'Corazón de Desierto' and 'CORAZÓN DE DESIERTO' not in des, 'el mas escuchado primero; repetidos una vez')
        ok('Juntos' not in [d['nombre'] for a in art.values() for d in a['discos']], 'discos de dos artistas: fuera')
        ok([d['nombre'] for d in art['Dueto Uno']['discos']] == ['Solo Uno'], 'Dueto Uno solo con lo suyo')
        ok([d['nombre'] for d in art['Sokol Vetra']['discos']] == ['Viento Norte'], 'disco sin letras latinas: fuera')
        # lo que no va en la pared: marcados (columna 7) y al final de los discos de su artista
        nd = art['Ñandú Sonoro']['discos']
        ok([(d['nombre'], d['marcas']) for d in nd] == [('Ñandú: la Lost Edition', ''), ('Ñandú Salsero', ''),
                                                          ('Ñandú en el Cine', 's'), ('Ñandú Quest (Original Game Soundtrack)', 'sj'),
                                                          ('Ñandú Karaoke Hits', 'k')],
           'marcas: cine, juego y karaoke al final y marcados (sj: la letra s va antes de la j); «Lost» sin marca (%s)' % [(d['nombre'], d['marcas']) for d in nd])
        ok(all(d['marcas'] == '' for d in art['Los Ángeles del Desierto']['discos']), 'sin marcas donde no hay')
        ok(all(d['marcas'] == 's' for d in art['Bandas Sonoras Inventadas']['discos']), 'el genero Soundtrack del artista marca todos sus discos')
        # las tablas de palabras de la rocola y las de aqui dicen lo mismo (prueba/exclusiones.tsv: disco, artista, genero, marcas)
        with open(os.path.join(AQUI, 'exclusiones.tsv'), encoding='utf-8') as f:
            for l in f:
                if not l.strip() or l.startswith('#'):
                    continue
                dis, aut, gen, esp = (l.rstrip('\n').split('\t') + ['', '', '', ''])[:4]
                ok(ac.marcas_de(dis, aut, gen) == esp, 'exclusiones.tsv: %r de %r (%s) debe dar %r y da %r' % (dis, aut, gen, esp, ac.marcas_de(dis, aut, gen)))
        pl = art['Ángeles de Plata']['discos']
        ok([d['nombre'] for d in pl] == ['Plata Pura', 'Plata Fina', 'Plata Vieja', 'Plata Sin Fecha'],
           'a igualdad de escuchas: Spotify, portada, ediciones (%s)' % [d['nombre'] for d in pl])
        eds = edicion_de(tablas, 'Plata Pura')
        oficial_vieja = [e for e, st in eds if st == '1'][0]
        ok(pl[0]['sp'] == inventa.spid('album', oficial_vieja), 'el disco de Spotify de la edicion oficial mas vieja')
        ok(pl[0]['ano'] == '1975' and pl[3]['ano'] == '', 'año de la primera edicion; sin fecha, vacio')
        ok(pl[1]['portada'] == '1' and pl[2]['portada'] == '0', 'portada de grupo si; solo de atras no')
        # paises
        for n, p in (('Ángeles de Plata', 'MX'), ('Ñandú Sonoro', 'MX'), ('3 Gallos del Valle', 'PR'), ('Solo en Wikidata', ''),
                     ('Sin Area Ninguna', 'ES'), ('Rockeros Ingleses', 'GB')):
            ok(art.get(n, {}).get('pais') == p, 'pais de %s: %s (sale %r)' % (n, p, art.get(n, {}).get('pais')))
        # orden dentro de la letra: fama, luego nombre
        e = [a['nombre'] for a in letras['E'] if a['nombre'].lower() == 'empate alfa']
        ok(e == ['Empate Alfa', 'empate alfa'], 'empate de fama: por nombre')
        # fama: Mexico y lo regional pesan mas (mismos numeros, otro pais u otro genero)
        f1 = ac.fama(10 ** 6, 10 ** 4, 10, 'MX', 'Regional')
        ok(f1 > ac.fama(10 ** 6, 10 ** 4, 10, 'CO', 'Regional') > ac.fama(10 ** 6, 10 ** 4, 10, 'US', 'Regional')
           > ac.fama(10 ** 6, 10 ** 4, 10, 'US', 'Rock'), 'factores de pais y de genero')
        ok(ac.fama(10 ** 12, 10 ** 12, 10 ** 6, 'MX', 'Regional') == 1000000 and ac.fama(0, 0, 0, '', 'Rock') == 0, 'fama de 0 a 1e6')
        # el pre-mapeo de los nombres de genero (las trampas de GenreLabel)
        for n, g in (('A Flock of Inventadas', 'Pop'), ('Clasicos del Garage', 'Rock'), ('Dancehall Inventado', 'Reggae'),
                     ('Bandas Sonoras Inventadas', 'Soundtrack'), ('Hardcore Inventado', 'Punk'), ('Folk Pop Inventado', 'Folk'),
                     ('Dubstep Tropical', 'Electronica'), ('Jazz Latino Inventado', 'Jazz'), ('Alabanza Inventada', 'Cristiana'),
                     ('Gabber Inventado', 'Electronica')):
            ok(art.get(n, {}).get('genero') == g, 'pre-mapeo: %s es %s (sale %r)' % (n, g, art.get(n, {}).get('genero')))
        ok('Cine Raro Nomas' not in art, 'pre-mapeo: «pornographic film» no da genero (no Hip-Hop por «rap»)')
        reglas = ac.lee_reglas_genero()
        for nombres, g in (((('New Romantic', 'nuevo romanticismo')), 'Pop'), (('new wave', 'new wave'), 'Pop'),
                           (('synth-pop', 'synth pop'), 'Pop'), (('dance-pop', 'dance pop'), 'Pop'), (('electropop',), 'Pop'),
                           (('classic rock', ''), 'Rock'), (('garage rock',), 'Rock'), (('hard rock', 'hard rock'), 'Rock'),
                           (('album rock',), 'Rock'), (('blues rock', 'rock blues'), 'Rock'), (('instrumental rock',), 'Rock'),
                           (('baroque pop', 'pop barroco'), 'Pop'), (('dancehall',), 'Reggae'), (('rocksteady',), 'Reggae'),
                           (('lovers rock',), 'Reggae'), (('soundtrack', 'banda sonora'), 'Soundtrack'),
                           (('film score composer', 'compositor de bandas sonoras'), 'Soundtrack'),
                           (('hardcore punk',), 'Punk'), (('post-hardcore', 'Post-hardcore'), 'Punk'),
                           (('New York hardcore', ''), 'Punk'), (('hardcore hip-hop', 'hip hop hardcore'), 'Hip-Hop'),
                           (('hardcore', 'hardcore techno'), 'Electronica'), (('hardcore', 'Hardcore (música)'), 'Punk'),
                           (('happy hardcore',), 'Electronica'), (('folk-pop', 'folk-pop'), 'Folk'), (('punk-pop',), 'Punk'),
                           (('dark-pop',), 'Pop'), (('K-pop', 'K-pop'), 'K-Pop'), (('J-pop',), 'K-Pop'),
                           (('dubstep',), 'Electronica'), (('tropical house', 'house tropical'), 'Electronica'),
                           (('future bass',), 'Electronica'), (('Latin jazz', 'jazz latino'), 'Jazz'), (('Latin pop', 'pop latino'), 'Latino'),
                           (('Latin trap', 'trap latino'), 'Reggaeton'), (('urbano music', 'música urbana latina'), 'Reggaeton'),
                           (('salsa',), 'Tropical'), (('salsa romántica',), 'Tropical'), (('cumbia villera',), 'Regional'),
                           (('cumbia pop',), 'Regional'), (('alternative country', 'country alternativo'), 'Country'),
                           (('classic country',), 'Country'), (('western swing',), 'Country'), (('new jack swing',), 'Soul'),
                           (('funk carioca',), 'Latino'), (('Romantic music', 'música del Romanticismo'), 'Clasica'),
                           (('Latin ballad', 'balada romántica'), 'Bolero'), (('latin ballad',), 'Bolero'), (('bolero',), 'Bolero'),
                           (('contemporary worship music', 'música cristiana contemporania'), 'Cristiana'),
                           (('praise & worship',), 'Cristiana'), (('chiptune',), 'Electronica'), (('township music',), 'World'),
                           (('Piedmont blues',), 'Soul'), (('amapiano',), 'Electronica'), (('kan ha diskan',), 'Folk'),
                           (('stoner doom', 'Stoner doom'), 'Metal'), (('doo-wop',), 'Oldies'), (('dark wave', 'Dark wave'), 'Gotico'),
                           (('Cuban rumba', 'Rumba cubana'), 'Tropical'), (('rumba flamenca',), 'Flamenco'),
                           (('pornographic film', 'cine pornográfico'), 'Otros'), (('street photography', 'fotografía callejera'), 'Otros'),
                           (('military band', 'banda de música militar'), 'Otros'), (('banda music', 'Banda sinaloense'), 'Regional'),
                           (('regional Mexican', 'música regional mexicana'), 'Regional'), (('art song', 'canción artística'), 'Otros')):
            sale = ac.genero_de(nombres, reglas)
            ok(sale == g, 'pre-mapeo de %r: %s (sale %s)' % (nombres, g, sale))
        # genero por etiquetas de MusicBrainz
        if modo == 'normal':
            ok(art.get('Etiquetas Trampa', {}).get('fuente') == 'm' and art['Etiquetas Trampa']['genero'] == 'Pop',
               'etiquetas de MusicBrainz con el pre-mapeo: new romantic 4 + dubstep 2 = Pop')
            ok(art.get('Etiquetas Nomas', {}).get('fuente') == 'm' and art['Etiquetas Nomas']['genero'] == 'Tropical',
               'genero por etiquetas de MusicBrainz (salsa 5 votos > cumbia 3)')
        else:
            ok('Etiquetas Nomas' not in art and 'Etiquetas Trampa' not in art, '--solo-cc0: sin etiquetas de MusicBrainz no hay genero')
            ok(all(a['fuente'] == 'w' for a in art.values()), '--solo-cc0: todo de Wikidata')
    elif modo == 'tope':
        tope, cap = 12, int(ac.MAX_GENERO_LETRA * 12)
        for l, v in letras.items():
            n = sum(len(a['discos']) for a in v)
            ok(n <= tope, 'letra %s: %d discos con tope %d' % (l, n, tope))
            por = {}
            for a in v:
                por[a['genero']] = por.get(a['genero'], 0) + len(a['discos'])
            ok(all(k <= cap for k in por.values()), 'letra %s: un genero pasa de %d: %s' % (l, cap, por))
        ok(sum(len(a['discos']) for a in letras['L']) == tope, 'la letra L se llena hasta el tope')
        ok(art.get('Los Ángeles del Desierto', {}).get('discos') and len(art['Los Ángeles del Desierto']['discos']) == cap,
           'el primero de la L se queda con la parte de su genero')
    if fallos:
        for f in fallos:
            print('FALLA: ' + f)
        sys.exit(1)
    print('comprueba %s: bien' % modo)


if __name__ == '__main__':
    main()
