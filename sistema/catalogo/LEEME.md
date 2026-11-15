# El catálogo de la rocola (`rocola_catalogo.tsv.gz`)

Petición del dueño del 14-11-2026 (plan: `sistema/rocola/Xp/PLAN_PARED_GENEROS.md`): con el modo Spotify, cada letra de la pared llega
hasta 500 páginas (18 discos cada una = 9 000 discos) juntando la música del dueño y la de Spotify, «poniendo los artistas y discos más
populares de cada artista por género y ordenado por el abecedario». La API de Spotify de la app del dueño (modo desarrollo, 2026) ya no da
popularidad ni géneros, así que «lo más popular por género» sale de **un catálogo propio armado con datos abiertos**. La pared no gasta
ni una petición de Spotify para enseñarlo: Spotify solo toca, busca y abre un disco.

Aquí está **el armador** (`arma_catalogo.py`, Python 3 solo con la biblioteca estándar), su prueba sin red (`prueba/`) y su compilación a
pedido (`.github/workflows/rocola-catalogo.yml`), que publica el catálogo en la versión de GitHub con etiqueta fija `catalogo`. La parte
de la rocola (bajarlo, leerlo y la pared) es de otro grupo: lee el formato de abajo, que es un **contrato** («I8»).

El dueño decidió el 15-11-2026 que **no hay salto de página por género**: la pared se mueve como la de XP. El orden dentro de cada letra
sigue siendo por género (eso lo hace la rocola con este archivo) y el contrato no cambia.

## El formato (contrato «I8», formato 1)

`rocola_catalogo.tsv.gz`: gzip (sin nombre y con fecha 0) de un texto UTF-8, saltos de línea LF, campos separados por tabulador. Ningún
campo lleva tabuladores ni saltos de línea (los nombres se limpian: cualquier espacio raro queda como un espacio, sin espacios a los
lados).

```
#rocola-catalogo	1	<AAAA-MM-DD de la copia de MusicBrainz>	<artistas>	<discos>
L	<letra>	<artistas de la letra>	<discos de la letra>
A	<nombre>	<género (etiqueta de XP)>	<fama>	<id de artista de Spotify o vacío>	<país ISO de 2 letras o vacío>	<w|m>
D	<nombre del disco>	<año o vacío>	<id de disco de Spotify o vacío>	<MBID del grupo de lanzamiento>	<1 si tiene portada, si no 0>
D	...
A	...
L	...
```

- **Las 27 letras van siempre**, en el orden `#ABCDEFGHIJKLMNOPQRSTUVWXYZ`, aunque una tenga 0 artistas (`L	X	0	0`).
- La **letra** de un artista es la de XP (`FirstLetter`, `Form1.Biblioteca.cs`): el primer carácter del nombre sin espacios, en
  mayúscula y sin acento (forma D); si no es de la A a la Z, `#`. «Ángeles…» va en la A, «Ñandú…» en la N, «¡Ay…» y «3 Gallos…» en
  `#`. El armador y `valida` usan la misma regla (`letra_xp`).
- Dentro de cada letra, los artistas del **más al menos famoso** (para poder cortar por el final); a igual fama, por nombre (sin
  mayúsculas ni acentos) y por MBID. La rocola los reordena como quiera (género, abecedario); el orden del archivo solo dice quién va
  antes si hay que cortar.
- **Género**: siempre una etiqueta de XP de `GenreLabel` (`Form1.Fondo.cs`), nunca «Otros».
- **Fama**: entero de 0 a 1 000 000.
- **País**: código ISO 3166-1 de 2 letras en mayúsculas, o vacío si no se sabe.
- `w` = el género salió de Wikidata (CC0); `m` = de las etiquetas de MusicBrainz (CC BY-NC-SA 3.0).
- Los `D` de un artista van justo después de su `A`, del más al menos escuchado: **de 1 a 6** por artista.
- **Año**: 4 cifras (la primera edición del disco) o vacío. **Ids de Spotify**: 22 caracteres `[0-9A-Za-z]`, o vacío. **MBID**: el
  del grupo de lanzamiento (`release-group`) de MusicBrainz, en minúsculas con guiones; sirve para la portada del Cover Art Archive
  (`https://coverartarchive.org/release-group/<MBID>/front-250`). Portada: `1` si el Cover Art Archive tiene una de frente para ese
  disco, si no `0` (entonces la casilla de XP de los discos sin carátula).
- Topes: como mucho **9 000 discos por letra** y ningún género más del **35 % del tope** (3 150 discos) en una letra.
- Las cuentas de `L` y de la cabecera cuadran con las líneas que siguen.

`rocola_catalogo.txt` (líneas `clave=valor`): `formato=1`, `version=<AAAAMMDD de la copia>.<número de corrida>`, `fecha=` (el día en
que se armó), `sha256=` y `bytes=` del `.tsv.gz`, `artistas=`, `discos=`, `fuentes=` y `licencia=`. La rocola baja el `.txt`, y solo si
la `version` cambió baja el `.tsv.gz` y lo comprueba con `sha256` y `bytes`.

`rocola_catalogo_informe.txt`: los números por letra y por género (lo que entró y lo que quedó fuera), de dónde salió el género, los
tiempos de cada paso y una muestra de unos 40 artistas conocidos para revisar a ojo (`muestra.txt`).

**Para el grupo de la pared**: `prueba/rocola_catalogo_prueba.tsv.gz` es un catálogo chico (176 artistas, 652 discos), **todo
inventado**, armado por el mismo programa con la copia inventada de `prueba/`; tiene las 27 letras, varios géneros por letra, nombres
con acento, «Ñ», minúsculas y `#`, discos sin año, sin id de Spotify y sin portada. `sh sistema/catalogo/prueba/prueba.sh` comprueba
que sigue saliendo igual byte a byte; si alguna vez cambia (`ACTUALIZA=1`), se dice.

## Las fuentes y sus licencias

| Fuente | Qué se toma | Licencia |
|---|---|---|
| MusicBrainz, copia completa `mbdump.tar.bz2` (`data.metabrainz.org/pub/musicbrainz/data/fullexport/LATEST`) | artistas, alias, áreas y países, discos (grupos de lanzamiento, ediciones, fechas, tipos), créditos, enlaces a Spotify | CC0 |
| MusicBrainz `mbdump-derived.tar.bz2` | `tag` y `artist_tag`: etiquetas de los artistas, **solo de respaldo** del género y solo las que son géneros de MusicBrainz (tabla `genre`) | **CC BY-NC-SA 3.0** (no comercial, compartir igual) |
| MusicBrainz `mbdump-cover-art-archive.tar.bz2` | si un disco tiene portada de frente (no se baja ninguna imagen al armar) | CC0 (las tablas) |
| Wikidata (SPARQL, `query.wikidata.org`) | P1902 (id de Spotify), P136 (género, con su nombre en inglés y español), `wikibase:sitelinks` (en cuántas Wikipedias está) | CC0 |
| ListenBrainz (`api.listenbrainz.org/1/popularity/artist` y `/release-group`) | escuchas y oyentes de cada artista y de cada disco | CC0 |

Con `--solo-cc0` el armador no usa las etiquetas de MusicBrainz: todo el catálogo es CC0 (y salen menos artistas: los que solo tienen
género por etiquetas). El `.txt` y el informe dicen cuántos artistas salieron de cada forma. El uso de la rocola es privado (reuniones
familiares); si algún día fuera comercial, conviene armar con `--solo-cc0`. Ningún dato de Spotify se copia: solo los ids que ya
están en MusicBrainz y Wikidata.

## Las reglas

1. **Quién entra**: artistas de MusicBrainz con (a) enlace a Spotify (`open.spotify.com/artist/ID` en sus enlaces de MusicBrainz, o
   P1902 en Wikidata; si hay varios, el que coincide en los dos, si no el menor), (b) género y (c) al menos un disco que valga.
2. **Nombre**: el `name` del artista. Si no se puede leer en la rocola (menos de la mitad de sus letras son latinas), su alias en
   español o inglés (primero los principales, `primary_for_locale`; a igualdad el español); si no tiene, fuera. Los discos sin letras
   latinas, fuera.
3. **Género**: las reglas de `GenreLabel` de XP, **leídas del archivo** `sistema/rocola/Xp/Codigo/Form1.Fondo.cs` cada vez (el
   armador se para si salen menos de 30): una sola lista. Cada género de Wikidata se pasa entero por `GenreLabel` («nombre en inglés /
   nombre en español», así manda el orden de las reglas de XP); cada uno pesa 1. Si Wikidata no da ninguna etiqueta, las etiquetas de
   MusicBrainz que son géneros, cada una con sus votos. Gana la etiqueta con más peso; a igualdad, la regla que va antes en
   `GenreLabel`. «Otros» no cuenta; sin género, el artista no entra.
4. **Discos**: grupos de lanzamiento de tipo principal Album, sin los tipos secundarios Spokenword, Interview, Audiobook, Audio drama,
   Demo, DJ-mix, Mixtape/Street, Remix ni Field recording (en vivo, recopilaciones y bandas sonoras sí), acreditados a ese artista solo.
   Orden: escuchas de ListenBrainz; a igualdad, primero con id de Spotify, luego con portada, luego más ediciones, luego el más viejo
   (luego nombre y MBID). Los nombres repetidos (sin mayúsculas ni acentos) cuentan una vez. Hasta 6. Año: la primera fecha de sus
   ediciones. Id de Spotify: el de la edición **oficial** más vieja que lo tenga (si ninguna oficial, la más vieja). Portada: si el
   Cover Art Archive tiene una imagen de frente en alguna edición o una elegida para el grupo.
5. **País**: el área del artista (si no tiene, la de inicio): país directo, o el prefijo de su código ISO 3166-2, o subiendo por
   «part of» (`l_area_area`) hasta 3 saltos.
6. **Fama** (0 a 1 000 000, entera): ver las perillas.
7. **Por letra**: del más al menos famoso, cada artista con sus discos mientras quepan: si al siguiente ya no le caben todos (por el
   tope de 9 000 o por la parte de su género), entra con los que quepan; si no cabe ninguno, se queda fuera y se sigue con el
   siguiente (puede caber uno de otro género).
8. **Determinista**: mismos datos de entrada, mismos bytes (orden total con desempates, gzip con fecha 0).

## Las perillas (arriba de `arma_catalogo.py`)

| Perilla | Valor | Qué hace |
|---|---|---|
| `TOPE_DISCOS_LETRA` | 9 000 | discos por letra (500 páginas de 18) |
| `DISCOS_POR_ARTISTA` | 6 | discos por artista |
| `MAX_GENERO_LETRA` | 0,35 | parte del tope que puede llevarse un género en una letra |
| `PESO_ESCUCHAS`, `PESO_OYENTES`, `PESO_WIKIPEDIAS` | 0,45 / 0,30 / 0,25 | la mezcla de la fama |
| `LOG_ESCUCHAS_TOPE`, `LOG_OYENTES_TOPE`, `LOG_WIKIPEDIAS_TOPE` | 8 / 6 / 2,5 | escala logarítmica: 100 millones de escuchas, 1 millón de oyentes o ~316 Wikipedias valen 1 |
| `FACTOR_MEXICO` | 3 | artistas de México |
| `FACTOR_HISPANO` | 1,3 | AR BO CL CO CR CU DO EC ES GQ GT HN NI PA PE PR PY SV UY VE |
| `FACTOR_GENERO` | Regional 3; Tropical 2,5; Bolero 2,5; Latino 2,2; Reggaeton 2; Flamenco 1,5; el resto 1 | familia de género |

Fama = (0,45·min(1, log10(1+escuchas)/8) + 0,30·min(1, log10(1+oyentes)/6) + 0,25·min(1, log10(1+Wikipedias)/2,5)) · país · género
/ (3 · 3) · 1 000 000, redondeada. Así, a iguales números, un artista regional de México vale 9 veces uno de rock de Inglaterra (subido el 15-11-2026: en la primera corrida real lo regional y lo latino quedaban fuera por el tope en las letras llenas).

## Cómo se corre

En GitHub (lo normal): Actions → «Catálogo de la rocola» → Run workflow (solo a pedido, como las demás compilaciones). Al cambiar el
propio `rocola-catalogo.yml` corre solo la prueba (para que GitHub registre el flujo): ni baja nada grande ni publica.

A mano (hacen falta unos 25 GB de disco libres y buena red):

```sh
D=https://data.metabrainz.org/pub/musicbrainz/data/fullexport/$(curl -s .../fullexport/LATEST)
curl -s $D/mbdump.tar.bz2 | lbzip2 -dc | python3 arma_catalogo.py extrae --salida tablas
curl -s $D/mbdump-derived.tar.bz2 | lbzip2 -dc | python3 arma_catalogo.py extrae --salida tablas
curl -s $D/mbdump-cover-art-archive.tar.bz2 | lbzip2 -dc | python3 arma_catalogo.py extrae --salida tablas
python3 arma_catalogo.py arma --tablas tablas --cache cache --salida salida --muestra muestra.txt --corrida 1
python3 arma_catalogo.py valida salida/rocola_catalogo.tsv.gz
```

`arma` pregunta a Wikidata y ListenBrainz lo que no esté en `--cache` y lo va guardando ahí (una corrida cortada sigue donde iba);
`--sin-red` no pregunta nada. `esquema CreateTables.sql caa/CreateTables.sql` comprueba que el orden de columnas que usa el programa
es el de MusicBrainz (además, cada fila se comprueba al leerla: si una tabla cambia de columnas, el armador se para).

La prueba, sin red: `sh sistema/catalogo/prueba/prueba.sh`.

## Lo probado (15-11-2026)

**Sin red, con la copia inventada** (`prueba/prueba.sh`, 2 s; también corre en GitHub al cambiar el flujo): 180 artistas, 688 grupos
de lanzamiento y 1 239 ediciones inventados dan **176 artistas y 652 discos** (31 738 bytes); dos corridas dan los mismos bytes y son
iguales a `rocola_catalogo_prueba.tsv.gz`; `valida` pasa; `comprueba.py` mira 30 casos (letras con acento, «Ñ» y `#`; fuera los que no
tienen Spotify, ni género, ni discos, ni nombre latino; alias; Spotify solo por Wikidata; empates de género por el orden de
`GenreLabel`; hasta 6 discos sin repetidos, sin Remix ni sencillos ni discos de dos artistas; el id de Spotify de la edición oficial más
vieja; año, portada de frente o de grupo y no de atrás; país directo, por subdivisión, subiendo dos saltos, por el área de inicio y no
por un enlace que no es «part of»; orden a igual fama; factores de la fama); `--solo-cc0` deja fuera al que solo tenía etiquetas; con
`--tope 12` ninguna letra pasa de 12 discos ni un género de 4; `valida` atrapa 8 archivos malos; `extrae` saca de un tar lo mismo.

**Con datos de verdad, en chico** (no cabe aquí la copia de 7 GB): el orden de columnas de las 28 tablas es el de `CreateTables.sql` y
`caa/CreateTables.sql` de hoy (`esquema`), y cada tabla real que se pudo bajar a trozos (area, iso_3166_1/2, l_area_area, link,
link_type, genre, tag, artist_tag, artist, los tipos de disco, release_status: hasta 1,16 millones de filas) se lee con sus columnas;
filas sueltas de release, release_group, release_country, url, artist_alias y artist_credit tienen las columnas esperadas. `extrae`
leyó el principio real de `mbdump.tar.bz2` (TIMESTAMP, area entera). Wikidata de verdad: P1902 + P434 en una consulta (58 830 artistas,
7 s), géneros y Wikipedias por lotes (2 000 MBID en 4 s) y nombres de género; ListenBrainz de verdad (artistas y discos; lotes de
1 000; 1 001 ya lo corta el servidor; 30 peticiones por ventana de 10 s). Ejemplos: Queen → Rock (5 géneros de rock, 2 de metal, 1 de
pop), Caifanes → Rock, José José → Bolero (MX, 22 Wikipedias).

**No probado aquí**: la corrida entera (va en GitHub Actions). Cálculo: bajar ~8,4 GB (las tres copias), unas 6 GB de tablas extraídas
(el flujo libera unos 20 GB antes), ~150 000 artistas y ~1 millón de discos por preguntar a ListenBrainz (~1 150 peticiones, 30-45
min), ~100 consultas a Wikidata (~10 min); en total entre 1,5 y 2,5 horas (tope del flujo: 5,5 h). El informe dice cuánto tardó cada
paso.

## Lo que hay que mirar en la primera corrida real

- Cuánta música mexicana entra: MusicBrainz y ListenBrainz cubren peor lo regional (bandas, norteño, grupero) que el rock en inglés; la
  muestra del informe (`muestra.txt`) lo dice artista por artista. Si faltan muchos, se puede subir `FACTOR_MEXICO` o el de Regional.
- Las reglas de XP aplicadas a nombres de géneros en inglés tienen sorpresas: «classic rock» da Clásica, «garage rock» da DnB,
  «dancehall» da Electrónica. Con varios géneros por artista casi siempre gana el bueno (pesa 1 cada uno), pero no se cambian las reglas
  aquí: son las de XP (una sola lista).
- Licencias: lo de fuente `m` es CC BY-NC-SA (no comercial).
