# Fase 11, Spotify: las letras con el catálogo, hasta 500 páginas por letra, y la pastilla del género en cada disco (grupo «pared-generos»)

Rama `pared-generos` (el nombre se quedó así), sobre `paredes-buscador` (c7f73b7: el catálogo de Spotify, sus paredes del buscador y el núcleo
ya juntados). Es el punto 3 del orden de `PLAN_PARED_GENEROS.md` (el grupo «pared») con **la decisión del dueño del 20-11-2026, que manda
sobre el orden por género de antes**:

> «El modo Spotify, en la parte de las carátulas, ya no se ordenará por género, creo que sería malo cambiar la distribución actual a la que
> la gente ya está acostumbrada, mejor ahora cada disco tendrá al lado la pastilla que diga de qué género es como un dato extra y ya, y como
> ahora el objetivo por letra de abecedario son 500 páginas, se mostrarán discos de Spotify siguiendo la coherencia que ya hay, y
> priorizando a los mejores artistas y en general, todo esto para tener el mejor catálogo posible.»

Lo del 15-11-2026 (la letra por género con la pastilla en la cabecera, commits 7eea493 y e2124bc) se adaptó a esto: se quitó el orden por
género y la pastilla de la cabecera (sus ganchos g91, g92 y g94 se devolvieron a XP y esos números se usan ahora para lo nuevo); el catálogo,
su descarga, sus portadas, abrir sus discos y la prueba se conservan. **No entra en la compilación del cumpleaños**: se junta después. Nada se
probó en el A4, ni con la API de Spotify de verdad, ni con el Cover Art Archive de verdad. **Sí con el catálogo de verdad** (el publicado en
la versión «catalogo» del repositorio, 44 014 artistas y 182 418 discos), servido por la API falsa.

Archivos: `RocolaOs/RocolaOs.ParedGeneros.cs` (reescrito) y `RocolaOs/RocolaOs.ParedGenerosCatalogo.cs` (sin cambios desde e2124bc), un
comentario en `RocolaOs/RocolaOs.SpotifyCatalogo.cs`, los ganchos **g90-g98** en `Codigo/` (con sus líneas en `RocolaOs/GANCHOS.tsv`) y la
prueba `Pruebas/RocolaOs/pared-generos/` (`prueba.sh`, `catalogo.py`, `verifica.py`, `contrato.py`).

## 1. Lo que hace

**Cuándo va**: la función «pared» de Rocola OS (encendida de fábrica, clave `pared=` de `rocola_os.txt`) solo hace algo con **el modo
Spotify encendido, la llave de la API y el catálogo presente**. Sin cualquiera de los tres, la pared es la de XP tal cual, **sin pastillas**
(cada gancho devuelve lo de XP). Sin la llave (decisión del 15-11-2026, «encendido sin llaves la rocola debe quedar como XP en lo que se ve y
suena») ni siquiera se baja el catálogo.

**La letra** (# y de la A a la Z; ★, VIDEOS, la ♫ y las paredes del buscador no cambian): sus discos de XP y los del catálogo juntos, **en el
orden de XP**.

| Regla | Cómo |
|---|---|
| **El orden de XP** | El de `BuildLibrary` (`Form1.Biblioteca.cs`, `albums.Sort`): por artista y luego por disco, con `string.Compare(..., OrdinalIgnoreCase)`. Tus discos entran todos y en ese mismo orden: la lista de XP es la de siempre, solo se le meten discos en medio. |
| Los del catálogo, **intercalados** | Cada disco del catálogo va en su sitio de ese orden (artista, disco). A igualdad de artista y disco, lo tuyo antes; entre dos del catálogo iguales, el orden del archivo. |
| Un artista tuyo y el del catálogo | Con el mismo nombre sin acentos ni mayúsculas (`MotorBusqueda.Normaliza`, la de XP), **van juntos**: el del catálogo se ordena con tu nombre («Ángeles Ficticios» del catálogo va con tu «Angeles Ficticios», intercalado por disco). Un disco del catálogo que ya tienes (mismo nombre de disco normalizado; una carpeta de XP sin « - » cuenta como el disco que se llama como el artista) **no se repite**. |
| **500 páginas** como mucho | 18 discos por página = 9 000 por letra, **contando los tuyos**. Tus discos entran siempre; del catálogo, los primeros **del archivo** hasta llenar: el archivo va del artista más famoso al menos famoso (`sistema/catalogo/LEEME.md`), así que **si no caben todos entran primero los de los artistas más famosos** (lo de atrás, lo menos escuchado, se queda fuera). |
| Los acentos en el orden | Como en XP: `OrdinalIgnoreCase` pone las letras con acento después de la Z dentro de la letra («Ángela...» de un artista que no tienes va al final de la A, como iría una carpeta tuya que se llamara así). Es la coherencia que ya hay; no se cambió. |

**Cómo entra** (lo de antes, sin cambios): la lista de una letra se arma **en un hilo** (`RocolaOs:pared_generos`, nice 19), nunca en la
pantalla, y entra **de una pieza**. Mientras se arma se ve la de XP. Al entrar, **la elección se queda en el mismo disco**. Se arman por
adelantado la letra que se mira y sus dos vecinas; en la memoria, **4 letras como mucho**.

**Moverse**: lo de XP, sin nada nuevo. Las letras que solo trae el catálogo salen en la barra en su sitio del abecedario.

**La pastilla del género en cada casilla** (el dato extra): la pastilla de XP (la de `HorneaGenero`/`ColorDeEtiqueta` de la lista del disco:
relleno tenue del color del género, borde fino, texto aclarado, texto + 16 de ancho, alto + 5, radio 7, en negrita como `fCola`), **abajo a
la izquierda de la cara del disco** (la que se ve de frente: ancho lado/√2), encima de su borde de abajo; la letra según el lado de la
portada (8 pt en 1024×768); si no cabe a lo ancho, en chico. Lo único que se le añade es **su misma forma en negro a medias debajo**: en XP
va sobre el fondo oscuro de la lista, aquí encima de una portada, y sin eso no se lee sobre una portada clara.

| Disco | Su pastilla |
|---|---|
| Tuyo | El género que XP ya sabe de él (`GeneroDeCancion(null, disco)`: la tabla de discos, la de artistas, lo que dice el nombre), tomado cada vez que se arma su letra. Si XP no lo sabe («Otros»), **sin pastilla**. |
| Del catálogo, de un artista que tienes | El que más se repite entre tus discos de ese artista (el de XP; a igualdad, el primero por orden alfabético). Si XP no sabe ninguno, el del catálogo. |
| Del catálogo, de otro artista | El del catálogo (ya es una etiqueta de `GenreLabel`). «Otros», sin pastilla. |

Va **horneada dentro del mapa de la casilla**, como el palomito de XP («ya la pusiste»): no cuesta nada por cuadro. En la casilla de la
tarjeta (`CaratulasSeparadasGpu.cs`, `BakeSeparatedCell`) el trozo que va encima de la portada —en XP, solo el del palomito— se agranda para
llevar también la pastilla; en el dibujo por procesador (`Form1.Pared.cs`, `BakeCell`) se pinta en la casilla. La clave de la casilla
(`ClaveCasilla`, la que usan los tres sitios que hornean y la tarjeta) lleva la pastilla: una casilla con pastilla es otro mapa, así que al
llegar la letra armada (o al apagar el modo) las casillas se hornean otra vez con el presupuesto de cada cuadro de XP. **El disco elegido**
es el cubo que gira de XP (se dibuja aparte, con su portada): no lleva pastilla, como tampoco el palomito.

**Los discos del catálogo** (lo de antes, sin cambios): no piden nada a la API de Spotify mientras se mira la pared; al entrar, 1 petición
con su id (sin id, 1 búsqueda y el disco); sus canciones llevan el género de su pastilla y se piden por **I6**; si Spotify no lo tiene, el
aviso de XP y **ese disco se esconde la sesión**.

## 2. El catálogo y sus portadas

Sin cambios desde e2124bc (ver el commit 7eea493): el formato «I8», la descarga de la versión «catalogo» de GitHub (sin llave, una vez al día
como mucho, comprobado con sha256 y bytes) al disco de la rocola (`ROCOLA_IA_DATOS/../catalogo`), las portadas del Cover Art Archive al verse
(4 a la vez, caché con tope de 300 MB). El `.txt` que publica el grupo catalogo-datos (`formato=1`, `version=`, `fecha=`, `sha256=`, `bytes=`,
`artistas=`, `discos=`, `fuentes=`, `licencia=`) se lee bien (escena `contrato`, y el de verdad en la escena `real`). En memoria, el de
verdad ocupa **15,3 MB** sin comprimir (`catalogo_kb=15302`).

## 3. Los ganchos (g90-g98)

| Id | Archivo:línea | Para qué (sin el modo o sin pastilla, lo de XP) |
|---|---|---|
| g90 | `Form1.Biblioteca.cs` 2388 (`CurrentList`: `return list;`) | La lista de la letra: la armada, o la de XP mientras se arma |
| g91 | `Form1.Pared.cs` 1218 (`ClaveCasilla`: `string k = CellKey(a);`) | La clave de la casilla lleva su pastilla (sin pastilla, la misma cadena de XP) |
| g92 | `Form1.Pared.cs` 1254 (`BakeCell`: el palomito) | La pastilla en la casilla por procesador |
| g93 | `Form1.Video.cs` 1723 (`UiSig`) | Devuelve la misma cadena; solo mide lo que tarda cada cuadro con la pared delante (el estado) |
| g94 | `CaratulasSeparadasGpu.cs` 19 (`Rectangle mark = Rectangle.Empty;`) | La pastilla horneada de la casilla (o null) |
| g95 | `CaratulasSeparadasGpu.cs` 28 (`layout.Width = ...`) | El trozo de encima de la portada (el del palomito) se agranda para la pastilla |
| g96 | `CaratulasSeparadasGpu.cs` 39 (`if (queued) y += ...`) | Ese trozo también con la pastilla |
| g97 | `CaratulasSeparadasGpu.cs` 49 (`if (queued)`) | Ese trozo se pinta también con la pastilla |
| g98 | `CaratulasSeparadasGpu.cs` 53 (`g.Translate(-mark.X, 0); Palomito(...)`) | El palomito solo si hay algo pedido, y la pastilla; sin pastilla `mark.Y` es 0: lo de XP |

Y sin línea nueva, en los del catálogo (RocolaOs/): g43, g45, g47, g49, g50 y g52 como antes. `ganchos.py`: **TODO BIEN** (38 ganchos en 14
archivos; los 109 de `Codigo/` vuelven byte a byte a fc7b667; `CaratulasSeparadasGpu.cs` en LF y `Form1.Pared.cs` en CRLF, conservados).
`diff-xp.py --sin-ganchos $(ls Codigo)`: **5 095 / 4 828 / 229**, las de la base.

## 4. Las pruebas (con números)

`Pruebas/RocolaOs/pared-generos/prueba.sh PROGRAMA` (rocola-xp de verdad, fuera de pantalla a 1024×768, llvmpipe, datos INVENTADOS: los de
partida de las escenas más seis discos en carpetas «Artista - Disco»; la API falsa del catálogo, que da también el catálogo en `/catalogo/` y
las portadas en `/release-group/`; el Soloist FALSO, nunca el de verdad). El catálogo de prueba (`catalogo.py`, inventado y siempre igual):
**27 074 discos, 4 527 artistas, 9 000 discos en A, B y C**; en la A, lo primero del archivo son «Ángeles Ficticios» y «Alma Norteña» (que el
dueño tiene sin acento, con un disco repetido cada uno), y «Abeja Rara de Prueba» con un disco que Spotify no tiene (404), uno sin id que la
búsqueda no encuentra y uno sin id que sí; un artista de cada diez es «Otros» (sin pastilla); portadas que faltan (404).

`verifica.py` comprueba sobre la lista armada (`ROCOLA_PARED_VOLCADO`): todos tus discos; el número (9 000 o lo que haya); que los del
catálogo son **exactamente los primeros del archivo** sin los repetidos; **el orden de XP** casilla a casilla (con la caja de .NET: la i
turca sin punto no se pasa a I); a igualdad lo tuyo antes; con qué artista se ordena cada uno; ningún repetido; **la pastilla de cada disco
del catálogo** (la de tus discos del artista o la del suyo por la clave del disco: dos artistas pueden llamarse igual).

| Escena | Qué hace | Comprueba |
|---|---|---|
| `orden` | arranca y mira la A | el catálogo se bajó (1 .txt y 1 .tsv.gz); **0 a /v1**; `verifica.py` de la A: **9 000 discos = 500 páginas**, tus 5 en su sitio (casillas 1689, 2254, 3691, 3938 y 5633), los 8 995 del catálogo los primeros del archivo (los **3 últimos** fuera), «Alma Nortena · Corridos Bravos (tuyo) · Otros Corridos Bravos» y «Angeles Ficticios · Cumbias del Recuerdo (tuyo) · Más Cumbias Ficticias · Éxitos Ficticios» seguidos y con la pastilla Regional de tus discos (el catálogo dice Tropical de Ángeles); y lo mismo de la **B** (9 000, «Banda Ejemplar» tuyo, 1 fuera) y la **#** (12) armadas por adelantado; el estado: 500 páginas y 12-15 de 18 casillas con pastilla en la página que se ve |
| `pastilla` | la 1.ª página de la A dos veces: con las pastillas y con `ROCOLA_PARED_SIN_PASTILLAS=1` (la misma pared sin ellas) | casilla por casilla, en la cara del disco: las que llevan pastilla cambian **708-1 158 puntos**; las de «Otros», **0**; la elegida (el cubo) no cuenta. Y una captura de la B con tu disco «Banda Ejemplar - Banda en Vivo» con su pastilla Regional |
| `hojea` | **ARRIBA mantenido 40 s** desde la 1.ª página de la A | **pasan las 500 páginas** (706-730 vistas: la A y parte de la B); **0 a /v1**; 62 portadas pedidas (NativeAOT; ≤ 90), 11 896 dejadas sin pedir; 4 letras en la memoria como mucho |
| `hojeaxp` | lo mismo con el modo apagado | 0 peticiones; nada del catálogo |
| `abre` | entra a un disco del catálogo con id y PONER; REGRESAR; a «Disco Que No Esta», PONER; PONER («Disco Sin Spotify»); PONER («Disco Uno Por Nombre»); REGRESAR | 1 petición del disco con id y su 1.ª por I6 (`COBRADA`); el 404 y el sin resultado, escondidos (2) y la elección en su casilla; «Disco Uno Por Nombre», 1 búsqueda y el disco; **5 peticiones a /v1 en total** |
| `portadas`, `tope`, `descarga` | como antes | 33 pedidas de 2 páginas (3 sin portada), la 2.ª vez 0; tope de 1 MB: 161 bajadas, 174 borradas, 865 KB; el catálogo una vez al día, el nuevo pasado un día, el que no es el de su sha256 no se usa |
| `contrato` | el archivo de prueba del grupo catalogo-datos y su `.txt` con todas sus claves | se lee entero (652 discos); `contrato.py` (los discos del archivo con sus nombres y su pastilla) y `verifica.py` en la A y la B |
| `real` | **el catálogo de verdad** (versión «catalogo», 182 418 discos), servido por la API falsa | se lee; 0 a /v1; `verifica.py` de la A: **9 000**, tus 5, los 8 995 primeros del archivo (5 fuera: los últimos de «Andrew Manze»), el orden de XP, la pastilla de cada uno; 9 000 de 9 000 con pastilla (el armador deja fuera a los artistas sin género). Revisadas a mano también la B y la # de esa corrida: 0 MAL |
| `sincatalogo` | la red no tiene catálogo (404) | solo el .txt; la pared de XP, sin pastillas (`vista=0`), nada armado |
| `apagado`, `cero`, `sinllave` | sin `ROCOLA_OS`; `spotify=0`; sin la llave | **ninguna** petición; nada del catálogo; la traza de `cero` y `sinllave` **igual** a la de `apagado` quitando lo de `RocolaOs:` (`compara_trazas.py --ignora`, holgura 80) |

**Resultados** (20-11-2026; la máquina compartida con otros agentes compilando y probando: carga 9-20 en 4 núcleos):

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release` (JIT) | 0 errores; ningún aviso en `RocolaOs/` |
| `dotnet publish` NativeAOT (linux-x64) | sale con 0, **0 avisos IL/AOT**, 1 min 38 s |
| `prueba.sh` en NativeAOT (las 14 escenas) | **66 de 66 BIEN** |
| `prueba.sh` en JIT (las 14 escenas, carga 19-20) | **65 de 66 BIEN**: `hojea` pidió 110 portadas (tope 90) con los cuadros lentos por la carga; repetida `hojea hojeaxp`: **9 de 9 BIEN** (59 portadas) |
| `orden pastilla` otra vez en NativeAOT (con la captura de la B) | **12 de 12 BIEN** |
| Lo que tarda armar una letra de 9 000 | NativeAOT: hasta 58 ms con el de prueba y **69 ms con el de verdad**; JIT hasta 208 ms con el de verdad (y hasta 1 s con la máquina a carga 20); siempre en su hilo |
| `ganchos.py` | **TODO BIEN** (38 ganchos en 14 archivos) |
| `diff-xp.py --sin-ganchos` | 5 095 / 4 828 / 229, las de la base |

Antes de esas corridas, con la máquina a carga 20, dos escenas fallaron una vez por el tiempo y no por lo que comprueban: `hojea` pidió 127 portadas (el tope
de la prueba es 90: con cuadros lentos, más páginas se quedan los 300 ms que pide una portada) y `abre` perdió teclas al ir a la casilla 6 190
(343 pulsaciones a 0,3 s). Para `abre` se cambió el catálogo de prueba («Abeja Rara de Prueba» queda al principio de la A: la casilla 0-2) y
ya no depende de cientos de pulsaciones.

**Los cuadros al hojear** (ms por cuadro mientras se pasa de página, medido en el programa: g93; llvmpipe fuera de pantalla, así que los
números absolutos no son los del A4; vale la comparación con la pared de XP en la misma máquina):

| NativeAOT | cuadros | p50 | p95 | p99 | máx | > 100 ms |
|---|---|---|---|---|---|---|
| la pared con el catálogo y las pastillas (`hojea`) | 955 | 42 | 57 | 74 | 460 | 4 |
| la pared de XP (`hojeaxp`) | 998 | 39 | 57 | 71 | 404 | 2 |

**La memoria** (NativeAOT): lo más alto en la memoria (RSS) 306 MB con el catálogo de prueba y **335 MB con el de verdad**, frente a 206 MB
con la pared de XP; los objetos de .NET 117 MB (prueba) y 159 MB (verdad) frente a 22 MB. Con 4 letras de 9 000 discos como mucho, no crece
al hojear: es el tope. El A4 tiene 3,5 GB.

**Capturas para el dueño** (NativeAOT, 1024×768, datos y portadas inventados), en
`/tmp/claude-0/-home-user-Rocola/b54ee812-4417-50ce-9f97-015b8aeef72d/scratchpad/generos-capturas/`: `pared-generos-letra-a` (la 1.ª página
de la A), `pared-generos-pastillas-con` y `-sin` (la misma página con y sin pastillas), `pared-generos-pastillas-tus-discos` (la B con tu
disco), **`pared-generos-catalogo-real`** (la A con el catálogo de verdad: «A Flock of Seagulls», «A Forest of Stars»... con sus pastillas;
las portadas son las inventadas de la API falsa), `pared-generos-hojeando-hojea`, `pared-generos-disco-del-catalogo`,
`pared-generos-disco-que-spotify-no-tiene`, `pared-generos-disco-buscado-por-nombre`, `pared-generos-sin-catalogo`, `pared-xp-apagado`,
`pared-xp-cero`, `pared-xp-sin-llave`.

## 5. Para el A4 (no se pudo probar aquí)

Nada de esto se probó en el A4, ni con la API de Spotify de verdad, ni con el Cover Art Archive de verdad, ni con la pantalla del bar.

1. Que el catálogo se baje solo (9,5 MB) a `/rocola/disco/rocola-os/catalogo`; `/run/rocola-xp/pared/estado` dice `catalogo=`,
   `catalogo_discos=` (182 418), lo que tardó en armarse cada letra (`armada_ms_max`) y la memoria.
2. Lo que tarda armar una letra de 9 000 discos en el A4 (aquí, con el de verdad, 69 ms en NativeAOT): mientras tanto se ve la de XP.
3. Mantener ARRIBA en una letra grande: que no haya tirones (comparar con la pared de XP) y contar en el estado `portadas_pedidas`.
4. **La pastilla en la pantalla del bar**: que se lea a la distancia (8 pt en 1024×768) y que no estorbe; tamaño, sitio y el negro de debajo
   son de la presentación de lo visual del final si el dueño quiere otra cosa.
5. Que tus discos lleven el género que XP sabe de ellos (los que XP no sabe van sin pastilla).
6. Abrir un disco del catálogo con la red del bar (1 o 2 peticiones) y pedir una canción.

## 6. Nivel 0

`nivel0.sh` (de la rama `fase11-nivel0`) con `PASOS="previo codigo ganchos"`: **4 de 4 BIEN** (lo de fuera está; los 109 archivos de
`Codigo/` con las mismas líneas, CRLF y último byte que fc7b667; `ganchos.py` TODO BIEN, 40 ganchos en 16 archivos; `diff-xp.py` 5 095 /
4 828 / 229). **Las escenas del nivel 0 y del nivel 1** (alternadas con la referencia congelada) **no se corrieron** esta vez: la máquina
estaba a carga 19-20 con otros agentes y una prueba en QEMU, y con esa carga sus tiempos no comparan. Sin el modo Spotify, sin la llave o sin
catálogo los ganchos devuelven lo de XP (las escenas `apagado`, `cero` y `sinllave` de arriba lo comprueban con la traza: iguales); quedan
para la junta.

## 7. Lo que queda abierto

- **Los acentos en el orden**: el de XP (`OrdinalIgnoreCase`) pone «Ángela...», «Édgar...» después de la Z de su letra. Es lo que hace XP con
  tus carpetas y se dejó así por coherencia; con el catálogo hay muchos más nombres con acento. Si el dueño prefiere que vayan con su letra
  sin acento, es un cambio chico (en el orden de las dos cosas, las tuyas también).
- **La calidad del género del catálogo**: viene de Wikidata y de MusicBrainz por las reglas de `GenreLabel` (con el de verdad: «A Flock of
  Seagulls» sale Bolero, por «romantic» de «new romantic»). Es del armador del catálogo (grupo catalogo-datos), no de la pared.
- **Una casilla sin portada** (aún no llega o el Cover Art Archive no la tiene) se ve vacía, como en XP, y su pastilla queda sola donde iría
  la portada (igual que el palomito de XP).
- **El disco elegido** no muestra la pastilla (es el cubo que gira de XP).
- **La ficha y el letrero de XP** no salen en un disco del catálogo sin abrir (dirían «0 canciones»).
- **El tope de las portadas** se mide cada 15 s y tras cada 64 bajadas: entre medida y medida puede pasarse un poco.
- **Un catálogo nuevo a media sesión** y **«spotify apagar» a media sesión**: leídos, sin escena (las pastillas se quitan al siguiente latido:
  `RocolaOsPgPublica`).
- La memoria: unos 100-130 MB más que la pared de XP con 4 letras de 9 000 (con el catálogo de verdad).
