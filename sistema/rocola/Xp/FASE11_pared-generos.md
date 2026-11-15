# Fase 11, Spotify: las letras por género, hasta 500 páginas por letra (grupo «pared-generos»)

15-11-2026. Rama `pared-generos`, sobre `paredes-buscador` (c7f73b7: el catálogo de Spotify, sus paredes del buscador y el núcleo ya
juntados). Es el punto 3 del orden de `PLAN_PARED_GENEROS.md` (el grupo «pared»): la petición del dueño del 14-11-2026 («que cada letra
tuviera 500 páginas juntando mi música local y la de Spotify, pero poniendo los artistas y discos más populares de cada artista por género y
ordenado por el abecedario»), con sus decisiones del mismo día (el género, con **la pastilla de XP** en la cabecera) y del 15-11-2026 (**no
hay salto de género**: la pared se mueve como en XP). **No entra en la compilación del cumpleaños**: se junta después. Nada se probó en el
A4 ni con el catálogo de verdad (aún no existe: lo arma el grupo catalogo-datos), ni con la API de verdad.

Archivos: `RocolaOs/RocolaOs.ParedGeneros.cs` y `RocolaOs/RocolaOs.ParedGenerosCatalogo.cs` (nuevos), cambios chicos en
`RocolaOs/RocolaOs.SpotifyCatalogo.cs` y `RocolaOs/RocolaOs.SpotifyCatalogoApi.cs`, los ganchos **g90-g94** en `Codigo/` (con sus líneas
en `RocolaOs/GANCHOS.tsv`), la API falsa del catálogo ampliada y la prueba `Pruebas/RocolaOs/pared-generos/` (`prueba.sh`, `catalogo.py`,
`verifica.py`).

## 1. Lo que hace

**Cuándo va**: la función «pared» de Rocola OS (encendida de fábrica, clave `pared=` de `rocola_os.txt`) solo hace algo con **el modo
Spotify encendido, la llave de la API y el catálogo presente**. Sin cualquiera de los tres, la pared y la cabecera son las de XP tal cual
(cada gancho devuelve lo de XP). Sin la llave (decisión del 15-11-2026, «encendido sin llaves la rocola debe quedar como XP en lo que se ve
y suena») ni siquiera se baja el catálogo.

**La letra** (# y de la A a la Z; ★, VIDEOS, la ♫ y las paredes del buscador no cambian): sus discos de XP y los del catálogo juntos.

| Regla | Cómo |
|---|---|
| Por **género** | Las etiquetas de XP (`GenreLabel`: la misma lista; el género del catálogo se pasa por `GenreLabel`). Un orden **fijo para la sesión**: primero los géneros de los que el dueño tiene más discos (de todas las letras), luego los demás por cuántos discos trae el catálogo, «Otros» al final. Se fija una vez, en cuanto hay catálogo y biblioteca, y no cambia aunque XP aprenda géneros de fondo. |
| El género de un disco tuyo | El de XP (`GeneroDeCancion(null, disco)`: la tabla de discos, la de artistas, lo que dice el nombre), tomado la primera vez que se arma su letra y **fijo la sesión** (por su carpeta). |
| El género de un artista | Para que un artista tuyo y el del catálogo vayan juntos, el artista entero va en un solo género: el que más se repite entre **tus** discos de él (sin «Otros»); si no tienes ninguno con género, el del catálogo; si no, «Otros». |
| Dentro, por **artista** | Sin acentos ni mayúsculas (`MotorBusqueda.Normaliza`, la de XP, que usa `UnicodeXp.FormaD`). «Angeles Ficticios» (tuyo) y «Ángeles Ficticios» (catálogo) son el mismo. |
| De cada artista | Primero **tus** discos (en el orden de XP) y después **los del catálogo en el orden del archivo** (del más al menos escuchado). Un disco del catálogo que ya tienes (mismo nombre, sin acentos ni mayúsculas; una carpeta de XP sin « - » cuenta como el disco que se llama como el artista) **no se repite**. |
| **500 páginas** como mucho | 18 discos por página = 9 000 por letra. Tus discos entran todos; del catálogo, los primeros del archivo hasta llenar (lo de atrás, lo menos escuchado, no entra). |

**Cómo entra**: la lista de una letra se arma **en un hilo** (`RocolaOs:pared_generos`, nice 19), nunca en la pantalla, y entra **de una
pieza**. Mientras se arma se ve la de XP. Al entrar, **la elección se queda en el mismo disco** (si estaba en la primera casilla, en la
primera). Se arman por adelantado la letra que se mira y sus dos vecinas (las de REGRESAR+izq/der y las de pasar de la última página), así
que casi nunca se ve la de XP. En la memoria, **4 letras como mucho** (la que se mira y las últimas 3). Un catálogo nuevo (una vez al día
como mucho) se ve con la lista de antes hasta que llega la nueva, y la elección se queda en el mismo disco (por su clave).

**Moverse**: lo de XP, sin nada nuevo (decisión del 15-11-2026): ARRIBA/ABAJO de página en página, REGRESAR+izq/der cambia de letra desde
su principio, los controles de arriba igual. Las letras que solo trae el catálogo (en la prueba: C, M, Q, #) salen en la barra en su sitio
del abecedario, sin número de canciones (no se saben).

**La pastilla del género** (decisión del 14-11-2026): en la cabecera, detrás de «LETRA A», **la pastilla de XP** con el género del disco
elegido (el de su sitio en la pared). Es el mismo dibujo que `HorneaGenero` de la lista del disco (relleno tenue del color de
`ColorDeEtiqueta`, borde fino, texto aclarado, texto + 16 de ancho, alto + 5, radio 7), con la letra de la cabecera; «Otros», sin pastilla
(XP no inventa). Cambia al moverse dentro de la página. Sin el modo: nada (ni un píxel distinto: los ganchos suman 0).

**Los discos del catálogo** (una casilla de XP: su portada o la casilla sin carátula, su nombre «Artista - Disco»):

- **No piden nada a la API de Spotify mientras se mira la pared**. Elegido, no sale la ficha ni el letrero de XP (dirían «0 canciones»: no
  se sabe cuántas trae sin preguntar a Spotify): como una casilla sin disco elegido.
- **Al entrar** (PONER): con su id de Spotify, **1 petición** (`/v1/albums/ID`, por la cola del cliente del catálogo); sin id, **1 búsqueda**
  (`album:"NOMBRE" artist:"ARTISTA"`, `type=album`; vale el que se llama igual o lo contiene) **y el disco** (2). Sus canciones entran a la
  lista de XP al llegar, con la pastilla de su género (el de su sitio), y PONER las pide por **I6** como las demás de Spotify (cobra; si no
  suena, se devuelve). Lo pedido va a la letra ♫ como su disco de Spotify (el del catálogo de siempre), no como el de la pared.
- **Si Spotify no lo tiene** (404 o 400, o la búsqueda no da ninguno que se llame así): el aviso de XP («Espera un momento y vuelve a
  intentarlo») si se estaba dentro, se vuelve a la pared y **ese disco se esconde la sesión** (la elección no salta: queda en el de
  después). Sin red o con 429: el aviso de XP y el disco se queda.

## 2. El catálogo y sus portadas

**El formato «I8»** (el contrato con el grupo catalogo-datos; su LEEME aún no está escrito: se usó el formato del encargo): UTF-8 en gzip,
tabuladores; `#rocola-catalogo	1	fecha	artistas	discos`; por letra `L	letra	artistas	discos`; `A	nombre	género	fama	id_spotify	país	w|m` del
más al menos famoso; tras cada A sus `D	nombre	año	id_spotify	mbid_grupo	portada(1|0)`. Al lado, `rocola_catalogo.txt` con `sha256=HEX` y
`bytes=N` (vale también la salida de `sha256sum`). Si el grupo catalogo-datos escribe otra cosa en el .txt, se ajusta `LeeTexto`.

**Bajarlo** (`RocolaOsPgDescarga`, su hilo `pared_catalogo`): de la versión «catalogo» de GitHub del repositorio público
(`https://github.com/Chomp-wq/Rocola/releases/download/catalogo/`), **sin llave**, al disco de la rocola: `ROCOLA_IA_DATOS/../catalogo`
(en la rocola, `/rocola/disco/rocola-os/catalogo`; sin la variable, la carpeta de lo grande de XP). Al arrancar se usa el que ya hay (si su
sha256 es el de su .txt); a los 45 s se mira el .txt de la red **una vez al día como mucho** (`rocola_catalogo_mirado.txt`); si dice otro
sha256, se baja el .tsv.gz, se comprueba (bytes y sha256), se lee, y solo entonces se guarda (escritura atómica) y se cambia. Sin red, con
el que ya tenga, y se vuelve a intentar a la hora. En memoria, los bytes sin comprimir (2,5 MB el de prueba; el de 234 000 discos serían
unos 25 MB) y un índice por letra; los discos de una letra se leen al armarla.

**Las portadas** (`RocolaOsPgPortadas`): del Cover Art Archive (`https://coverartarchive.org/release-group/MBID/front-250`, sin llave; solo
si el catálogo dice portada 1), **al verse**: solo las de la página que se mira y cuando lleva 300 ms a la vista (lo de las páginas que
pasan al hojear se deja sin pedir), **4 a la vez** (4 hilos), a su caché en el disco de la rocola (`catalogo/portadas/`), con **tope de
300 MB** (`ROCOLA_PARED_PORTADAS_MB` para las pruebas), medido cada 15 s con las miniaturas: se borran primero las que no se usaron en la
sesión, de la más vieja a la más nueva, hasta el 85 %. Las de las páginas de al lado que ya están en el disco se dan a XP para que las lea
por adelantado. Sin portada (404), la casilla de XP sin carátula. Las miniaturas que XP hace de estas portadas van a su caché
(`portadas/miniaturas`, g43) y su previa de 32 px no va a `previews32-v1.bin` (g50): son miles y tienen su tope. Nada de esto es de Spotify
ni gasta su cuota; nada se descarga de Spotify.

## 3. Los ganchos (g90-g94)

| Id | Archivo:línea | Para qué (sin el modo, lo de XP) |
|---|---|---|
| g90 | `Form1.Biblioteca.cs` 2388 (`CurrentList`: `return list;`) | La lista de la letra: la armada, o la de XP mientras se arma |
| g91 | `Form1.Vistas.cs` 1083 (`HeaderView`: el ancho del nombre) | La cabecera cuenta el sitio de la pastilla (sin ella, `+ 0f`) |
| g92 | `Form1.Vistas.cs` 1166 (`HeaderView`: `x += anchoSec;`) | Pinta la pastilla detrás de «LETRA A» si cabe entera (sin ella, `x += 0`) |
| g93 | `Form1.Video.cs` 1723 (`UiSig`: la firma de la imagen de arriba) | La tarjeta rehace la cabecera si cambia la pastilla (sin ella, la misma cadena: el mismo número). Aquí se miden también los cuadros (el estado) |
| g94 | `Form1.Biblioteca.cs` 2817 (`SetSelectedLocal`) | Al moverse dentro de la página, la pastilla dice el género del disco elegido (y repinta la cabecera en el dibujo por procesador) |

Y sin línea nueva, en los del catálogo (RocolaOs/): g43 y g50 (las miniaturas y previas de estas portadas), g45 (las letras del catálogo en
la barra; sin la llave o el catálogo después de tenerlos, se quitan), g47 (en cada `RefreshAlbums`: la pastilla y las portadas de la página
que se mira), g49 (entrar a un disco del catálogo) y g52 (sin ficha ni letrero para uno sin abrir). `ganchos.py`: **TODO BIEN** (36 ganchos
en 14 archivos; los 109 de `Codigo/` vuelven byte a byte a fc7b667; CRLF y LF conservados, g93 en LF como su archivo).

**Lo que no se tocó** (y por qué): `HeaderView.AnchoPedido` (lo que la cabecera pide en la fila de arriba). Pedir el sitio de la pastilla
le quitaría la palabra a la casilla de AYUDA (`AcomodaAyuda` la compacta), justo lo contrario de la petición del 15-11-2026 («la casilla del
asistente debe decir AYUDA [...] con Rocola OS activo la cabecera cede ese sitio»). Así, en 1024×768 la pastilla cabe y **el número de
página («1/500») se calla** cuando no cabe entero, con la regla de XP («lo que venga detrás solo se dibuja si cabe entero»). A partir de
1280 caben los dos. Es de la presentación de lo visual del final: el dueño elige.

## 4. Las pruebas (con números)

`Pruebas/RocolaOs/pared-generos/prueba.sh PROGRAMA` (rocola-xp de verdad, fuera de pantalla a 1024×768, datos INVENTADOS: los de partida de
las escenas más seis discos copiados de sus MP3 en carpetas «Artista - Disco» cuyo nombre dice el género, como los lee XP; la API falsa del
catálogo, que además da el catálogo de prueba en `/catalogo/` y las portadas del Cover Art Archive en `/release-group/`; el Soloist FALSO del
núcleo, nunca el de verdad). El catálogo de prueba lo genera `catalogo.py`, inventado y siempre igual: **27 074 discos, 4 527 artistas, 9 000
discos en A, B y C** (las 500 páginas), unos pocos en #, L, M, O y Q; con un artista con acento que el dueño tiene sin él, discos repetidos
con los suyos, discos sin id (cada 7.º), uno que Spotify no tiene (404), uno que la búsqueda no encuentra y portadas que faltan (404).

| Escena | Qué hace | Comprueba |
|---|---|---|
| `orden` | arranca y mira la A | el catálogo se bajó (1 .txt y 1 .tsv.gz) al disco de la rocola; **0 peticiones a /v1**; `verifica.py` sobre la lista armada (`ROCOLA_PARED_VOLCADO`): **9 000 discos = 500 páginas**, tus 5 discos, los 8 995 del catálogo son exactamente los primeros del archivo sin tus 2 repetidos (los 3 últimos del archivo fuera), géneros en el orden del estado (cada uno en un solo tramo), artistas en orden, lo tuyo antes, lo del catálogo en el orden del archivo, «Alma Nortena»/«Alma Norteña» y «Angeles»/«Ángeles Ficticios» juntos, en Regional (el catálogo dice Tropical de Ángeles: manda el de tus discos) y sin «Cumbias del Recuerdo» ni «Corridos Bravos» repetidos; el estado: letra A, 500 páginas, pastilla Regional |
| `pastilla` | a la última de Regional (casilla 904, página 51) y un paso a la derecha (la primera de Bolero) | el estado dice casilla 905 y pastilla Bolero; entre las dos capturas cambian **3 240-3 252 puntos** de la cabecera; 0 a /v1 |
| `hojea` | **ARRIBA mantenido 40 s** desde la primera página de la A (la repetición de XP) | **708 páginas** (las 500 de la A y 208 de la B); **0 a /v1**; 65-71 portadas pedidas (las de la primera página y las de donde se paró), **11 525 dejadas sin pedir**; 4 letras en la memoria como mucho |
| `hojeaxp` | lo mismo con el modo apagado (`ROCOLA_PARED_MIDE=1`: solo se miden los cuadros) | 0 peticiones; nada del catálogo |
| `abre` | entra a un disco del catálogo con id y PONER; REGRESAR; a «Disco Que No Esta» (casilla 2 479, página 138), PONER; PONER (el siguiente, «Disco Sin Spotify»); PONER («Disco Por Nombre»); REGRESAR | 1 petición del disco con id y PONER pidió su 1.ª por I6 (`COBRADA spotify:track:ccat0022411`); el 404 y el sin resultado, escondidos (2) y la elección en la misma casilla (2 479); «Disco Por Nombre», 1 búsqueda y el disco; **en total 5 peticiones a /v1** |
| `portadas` | la página 1 y la 2 de la A, dos veces con el mismo disco | 34 pedidas (las de 2 páginas: 36 casillas, 2 con portada 0), 3 sin portada (404) y no guardadas, 31 en la caché y sus 31 miniaturas allí (no en `rocola_miniaturas`), nada en `previews32`; la 2.ª vez, **0 pedidas** (del disco) |
| `tope` | 10 páginas despacio con tope de 1 MB | 166 bajadas, 184 borradas (portadas y miniaturas), **869 KB** al final |
| `descarga` | 4 arranques con el mismo disco | (1) .txt y .tsv.gz una vez; (2) el mismo día: **ninguna** petición al catálogo, el del disco; (3) «mirado» hace 2 días y otro en la red: lo baja (24 074 discos); (4) uno que no es el de su sha256: no se usa, sigue el de antes |
| `sincatalogo` | la red no tiene catálogo (404) | solo el .txt (404), 0 a /v1; la pared de XP (la A con tus 5), sin pastilla, ninguna letra armada |
| `apagado`, `cero`, `sinllave` | sin `ROCOLA_OS`; con `ROCOLA_OS=1` y `spotify=0`; con el modo encendido y **sin la llave** | **ninguna** petición (ni el catálogo, ni portadas, ni la llave); nada del catálogo en el disco; la traza de `cero` y de `sinllave` **igual** a la de `apagado` quitando lo de `RocolaOs:` (`compara_trazas.py --ignora`, holgura 80) |

**Resultados** (15-11-2026; la máquina compartida con otros 3 agentes compilando y una prueba en QEMU: carga 7-12):

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release` (JIT) | 0 errores, 13 avisos (los de la base; ninguno en `RocolaOs/`) |
| `dotnet publish` NativeAOT (linux-x64) | sale con 0, **0 avisos IL/AOT** |
| `prueba.sh` en JIT (las 12 escenas) | **53 de 53 BIEN** (en una corrida anterior, `cero` dio 1 MAL por la carga: un encargo de XP, `MedidasBitacora.cs:129`, 557 frente a 457 con holgura 80; repetida, IGUAL; no pasa por lo cambiado: con `spotify=0` la función no arranca) |
| `prueba.sh` en NativeAOT (las 12) | **53 de 53 BIEN** |
| Lo que tarda armar una letra de 9 000 | NativeAOT: 28 armadas en la corrida, de 16 a 353 ms, mediana 90 (con la carga de la máquina); JIT hasta 533 ms; siempre en su hilo |
| La prueba del catálogo (`Pruebas/RocolaOs/catalogo/prueba.sh`) con este programa, escenas `apagado cero busca disco artista pared sinllave mas` | **56 de 56 BIEN** (JIT): lo de antes no cambió |
| `ganchos.py` | **TODO BIEN** (36 ganchos en 14 archivos) |
| `diff-xp.py --sin-ganchos $(ls Codigo)` | 5 095 / 4 828 / 229, las de la base |
| Nivel 0 y nivel 1 | ver §6 |

**Los cuadros al hojear** (lo que tarda cada cuadro mientras se pasa de página, medido en el programa: g93; ms; llvmpipe fuera de pantalla,
así que los números absolutos no son los del A4; lo que vale es la comparación con la pared de XP en la misma máquina y la misma carga):

| | cuadros | p50 | p95 | p99 | máx | > 100 ms |
|---|---|---|---|---|---|---|
| NativeAOT, la pared por géneros (`hojea`) | 821 | 46 | 66 | 87 | 650 | 3 |
| NativeAOT, la pared de XP (`hojeaxp`) | 909 | 44 | 64 | 76 | 499 | 3 |
| JIT, la pared por géneros | 815 | 48 | 68 | 84 | 471 | 1 |
| JIT, la pared de XP | 856 | 45 | 68 | 79 | 492 | 2 |

Lo mismo con y sin la pared por géneros (los máximos de ~500 ms salen en las dos: el cambio de letra y la carga de la máquina). **La
memoria**: lo más alto en la memoria (RSS) 276 MB con la pared por géneros frente a 200 MB con la de XP (NativeAOT); los objetos de .NET
(con lo que aún no se recogió) 84-86 MB frente a 19 MB. Con 4 letras de 9 000 discos como mucho, no crece al hojear: es el tope.

**Capturas para el dueño** (NativeAOT, 1024×768, datos y catálogo inventados), en
`/tmp/claude-0/-home-user-Rocola/b54ee812-4417-50ce-9f97-015b8aeef72d/scratchpad/generos-capturas/`: `pared-generos-letra-a`,
`pared-generos-pastilla-regional`, `pared-generos-pastilla-bolero`, `pared-generos-hojeando-hojea`, `pared-generos-tras-hojear-hojea` (y las
de `hojeaxp`, la de XP, para comparar), `pared-generos-disco-del-catalogo`, `pared-generos-disco-que-spotify-no-tiene`,
`pared-generos-disco-buscado-por-nombre`, `pared-generos-sin-catalogo`, `pared-xp-apagado`, `pared-xp-cero`, `pared-xp-sin-llave`.

## 5. Para el A4 (no se pudo probar aquí)

Nada de esto se probó en el A4, ni con el catálogo de verdad (no existe todavía), ni con la API de verdad, ni con el Cover Art Archive de
verdad, ni con la pantalla del bar. Lo que hay que mirar allí:

1. Que el catálogo se baje solo (unos 10 MB) y quede en `/rocola/disco/rocola-os/catalogo`; `/run/rocola-xp/pared/estado` dice `catalogo=`,
   `catalogo_discos=`, el orden de los géneros, lo que tardó en armarse cada letra (`armada_ms_max`) y la memoria.
2. Lo que tarda armar una letra de 9 000 discos en el A4 (aquí 16-353 ms en NativeAOT, mediana 90): mientras tanto se ve la de XP.
3. Mantener ARRIBA en una letra grande: que no haya tirones (comparar con la pared de XP) y contar en el estado `portadas_pedidas` y
   `portadas_dejadas`.
4. El Cover Art Archive de verdad redirige a archive.org (307): las portadas tardan; que la casilla sin carátula se llene sola al llegar.
5. Que el orden de géneros salga con lo que el dueño tiene (Regional, Tropical, Bolero...) y que los artistas que tiene queden junto a los
   del catálogo (los nombres de sus carpetas: «Artista - Disco»).
6. Abrir un disco del catálogo con la red del bar (1 o 2 peticiones) y pedir una canción.
7. El número de página en 1024×768 (§3): con la pastilla no cabe; decide el dueño en la presentación.

## 6. Nivel 0 y nivel 1

PENDIENTE (se rellena al acabar la corrida).

## 7. Lo que queda abierto

- **El catálogo de verdad**: el grupo catalogo-datos aún no escribió su LEEME ni su archivo de prueba; se usó el formato del encargo. Si su
  .txt dice el sha256 de otra forma, `LeeTexto` se ajusta; si sus géneros no son etiquetas de XP, pasan por `GenreLabel` igual.
- **El número de página con la pastilla en 1024×768** (§3): de la presentación del final.
- **La ficha y el letrero de XP** no salen en un disco del catálogo sin abrir (dirían «0 canciones»); saber cuántas trae costaría una
  petición por disco mirado. Si el catálogo trajera el número de canciones, se podría decir sin preguntar.
- **El tope de las portadas** se mide cada 15 s y tras cada 64 bajadas: entre medida y medida puede pasarse un poco (las miniaturas de XP,
  de unos 140 KB, pesan más que la portada de 250 px). Las miniaturas se borran antes que las portadas usadas en la sesión: si el tope se
  alcanza a menudo, XP las vuelve a hacer.
- **Un catálogo nuevo a media sesión** (una vez al día como mucho): la lista de antes se ve hasta que llega la nueva y la elección sigue en
  el mismo disco por su clave (leído; sin escena).
- **«spotify apagar» o quitar la llave a media sesión**: las letras del catálogo se quitan de la barra y la pared vuelve a la de XP al
  siguiente latido (leído; sin escena).
- **El aviso** cuando Spotify no tiene un disco es el de XP de siempre («Espera un momento y vuelve a intentarlo»); el disco desaparece de
  la pared, así que no se vuelve a intentar. Un aviso propio sería visual: de la presentación.
- La memoria: unos 70-80 MB más que la pared de XP con 4 letras de 9 000 (medido con el catálogo de prueba).
