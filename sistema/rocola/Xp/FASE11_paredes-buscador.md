# Fase 11, Spotify: las paredes del buscador (grupo «paredes-buscador»)

Junta de Spotify para el cumpleaños (15-11-2026). Hace los puntos 3 y 4 de `SPOTIFY_JUNTA_PENDIENTES.md`: el buscador con Spotify como
lo pidió el dueño el 7-11-2026 y **las paredes del buscador**, la decisión del dueño del 15-11-2026 («crear paredes de discos que solo son
accesibles desde el buscador, para que todo el catálogo de Spotify sea accesible de la manera más cómoda»), con sus ideas aprobadas el
mismo día («Me gustan tus ideas, agrégalas»).

Rama `paredes-buscador` (base `junta-spotify` en 4fb1656, con el catálogo y el núcleo ya juntados). Archivos:
`RocolaOs/RocolaOs.SpotifyCatalogoParedes.cs` (nuevo), cambios en `RocolaOs/RocolaOs.SpotifyCatalogo.cs` y
`RocolaOs/RocolaOs.SpotifyCatalogoApi.cs`, los ganchos g53-g56 en `Codigo/` (con sus líneas en `RocolaOs/GANCHOS.tsv`), la API falsa y
las escenas de `Pruebas/RocolaOs/catalogo/`.

## 1. Lo que hace (todo con lo que XP ya dibuja)

| Qué | Cómo se ve y cómo se usa |
|---|---|
| **Artistas de Spotify en el buscador** | Solo los que **no** están en la rocola: se compara su nombre, sin acentos ni mayúsculas (`MotorBusqueda.Normaliza` de XP), con el artista de cada disco tuyo (`Album.Artist`). Hasta 3. El primero que da Spotify sale si su nombre se parece a lo escrito (lo contiene, está contenido o está a dos letras: la distancia de `BuscadorFlexible.Distancia` del programa anterior, copiada); los demás, solo si lo contienen. Van justo después de tus artistas que salgan primero en los resultados de XP (si XP no da ninguno al principio, arriba de todo). Su foto es la portada de la tarjeta ARTISTA de XP. Sin «N seguidores». |
| **La pared de un artista** (al elegir uno de esos) | La rejilla de discos de XP (18 por página; IZQ/DER mueven, ARRIBA/ABAJO cambian de página como en XP —ARRIBA es la siguiente—, PONER entra al disco), **fuera de la barra de letras**: es una «letra» propia (`♪`, U+266A) que no está en `LetrasOrden` ni en la barra, así que la barra no la marca y REGRESAR+izq/der no sale de ella (g46 da el orden vacío). La cabecera dice el nombre del artista en mayúsculas, como «VIDEOS» o «SPOTIFY» (g47). Casilla 1: **«Canciones de ARTISTA»**; luego sus discos y sencillos. |
| **«Canciones de ARTISTA»** | Un disco de XP (su foto de portada, la lista «NN Título» de XP, PONER pide por I6) con las canciones de `search?type=track&q=artist:"NOMBRE"` en el orden en que las da Spotify. Se piden **al entrar**, de 10 en 10 mientras alguien está dentro, hasta 50 (5 peticiones); si sale antes, lo que falta se pide la siguiente vez que entre. Si la casilla se queda elegida 0,6 s en la pared (lo de g52 del catálogo), se pide solo su primera página, para que la ficha y el letrero de XP digan «10 canciones» y no «0 canciones». La misma canción puede estar en su disco con otro número: aquí va con la ruta `spotify:track:ID#artista` (su `Uri` de I6 no cambia) para que la lista diga su lugar. |
| **«ARTISTA · más en Spotify»** | Justo detrás de la tarjeta de XP de un artista **tuyo** que Spotify también tiene (tu tarjeta sigue igual y sigue llevando a tu pared, como en XP). Abre su pared **sin tus discos**: se comparan nombres de disco sin acentos ni mayúsculas; una carpeta tuya sin « - » es para XP un disco sin nombre de ese artista, y cuenta como el disco que se llama como él. Si ya se saben todos sus discos (una página contestada antes) y los tienes todos, la tarjeta no sale; si no se saben, sale (saberlo costaría una petición por búsqueda) y su pared dice cuáles faltan. |
| **«Todos los discos de «lo escrito»»** | La última tarjeta de los discos de Spotify del buscador (una tarjeta ÁLBUM de XP, con la portada del primer disco). Abre una pared con `search?type=album`, de 10 en 10 (offset hasta 990: Spotify no da más de 1000). La cabecera dice «TODOS LOS DISCOS DE «LO ESCRITO»». |
| **REGRESAR** (la pila) | Un disco abierto desde una pared → la pared, **en la misma página y casilla** (lo hace XP: `BackToAlbums`). En una pared → **el buscador con lo escrito y la misma elección** (g53; lo contestado sale de memoria: 0 peticiones), y debajo la pared de XP como estaba antes de abrir el buscador (al cerrarlo se vuelve allí). Si el buscador se había abierto desde otra pared del buscador, debajo queda esa (como mucho 4 niveles: disco, pared, buscador y la pared de debajo; lo más hondo se suelta). |
| **Un disco o lista de Spotify abierto desde el buscador** | Si **no** estaba en la letra ♫: REGRESAR vuelve **al buscador** con lo escrito y la misma elección (g54). Si ya estaba: la pared se queda en él, como XP (va a la letra ♫). Al abrirlo entra a los recientes de la letra ♫, como antes; la siguiente vez ya «está en la pared». |
| **SP9: los atajos** | Con `refresh_token`: «Tus más escuchadas» (`/v1/me/top/tracks`) y «Escuchado hace poco» (`/v1/me/player/recently-played`, sin repetidas) como atajos de XP (los de «Buscaste:», «Videos»...), **detrás** de los de XP; si no caben, se quedan fuera como los de XP (g55). Pulsarlos pone sus canciones como resultados, como «Lo más pedido hoy» (`MuestraAtajo`); elegir una la pide por I6 (g56). Una petición cada uno **por sesión**, la primera vez que se abre el buscador; en memoria. Sin `refresh_token` no salen ni se pide nada. |

**Una casilla cuya página aún no llega** es un disco de XP sin nombre ni portada: XP no le pinta nada (la casilla queda vacía) y se llena al
llegar. Si se entra a una así, el disco se abre al llegar su página. Con 429 o sin red se queda vacía y sale el aviso de XP «Espera un
momento y vuelve a intentarlo».

**Lo que NO se hizo (es visual: para la presentación del final)**: el zoom desde la casilla, cualquier marca de «Spotify», un aviso propio
(«Spotify frena»). La tarjeta ARTISTA de Spotify dice «1 disco» (lo de XP con el disco de su `AlbumRef`; ya estaba así en el catálogo,
§11 de `FASE11_catalogo.md`).

**Sin el modo, sin la llave de la API, con 429 o sin red**: nada de esto sale. Sin el modo cada gancho devuelve lo de XP (g53: REGRESAR en
la pared no hace nada, como XP; g54: `BackToAlbums`; g55: nada; g56: false). Sin la llave no hay resultados de Spotify, así que no hay
tarjetas que abran paredes. Con 429 el cliente del catálogo no pregunta nada hasta su `Retry-After`: lo que no llega no se enseña y sale
el aviso de XP.

## 2. Los ganchos (g53-g56)

| Id | Archivo:línea | Para qué |
|---|---|---|
| g53 | `Form1.cs` 2185 (ProcessCmdKey, en la pared: `if (IsBack(kc)) { return true; }`) | REGRESAR en una pared del buscador vuelve al buscador |
| g54 | `Form1.cs` 2174 (ProcessCmdKey, en un disco: `if (IsBack(kc)) { BackToAlbums(); return true; }`) | Un disco de Spotify abierto desde el buscador que no estaba en la letra vuelve al buscador |
| g55 | `BuscadorPortadasGpu.cs` 146 (ArmaAtajos) | Los atajos de SP9 detrás de los de XP |
| g56 | `BuscadorPortadasGpu.cs` 198 (UsaAtajo) | Pulsar uno de esos pone sus canciones como resultados |

Y, sin línea nueva, se reusan los del catálogo: g40 (la mezcla: artistas, «más en Spotify», «Todos los discos»), g41 (elegir: abre la
pared), g44 (la lista de la pared), g46 (dentro de una pared, ni otras letras ni la vuelta), g47 (la cabecera; y pide lo que le falta a la
página que se mira), g49 (entrar a «Canciones de» o a una casilla que aún no llega; y deja de volver al buscador al entrar a otro disco) y
g52 (la primera página de «Canciones de» al quedarse elegida). `ganchos.py`: **TODO BIEN** (los 109 archivos de `Codigo/` vuelven byte a
byte a fc7b667 devolviendo los ganchos; 11 archivos con ganchos). Los ids siguen al último de la rama (g52); si la junta ya usó g53-g56,
se renumeran.

Lo que se quitó: el artista de Spotify que llevaba sus discos a la letra ♫ (`RocolaOsSpotifyCatalogoAbreArtista`, y en el cliente el pedido
«artista» con su `deArtista`): ahora abre su pared. El contador `artistas=` del estado cuenta las páginas de las paredes de artista.

## 3. Peticiones por acción (la cuota)

Todo por el cliente del catálogo (`RocolaOs.SpotifyCatalogoApi.cs`): su cola de una petición a la vez, lo que alguien mira antes que lo de
la cuenta, el freno de 429 (también guardado en `/run`), todo lo contestado en memoria la sesión; **nada de fondo**. Portadas y fotos, como
las del catálogo, a `/run/rocola-xp/spotify/portadas` (del servidor de imágenes: no gastan la cuota de la API).

| Acción | Peticiones a la API |
|---|---|
| Abrir la pared de un artista | 1 (`/artists/{id}/albums?include_groups=album,single&limit=10&offset=0`) y, si tiene más de 10 discos, 1 más (offset=10): la primera página de 18 casillas. Se abre cuando llegan. |
| Pasar a otra página de una pared | 1 o 2 (los bloques de 10 que le faltan a esas 18 casillas); una página ya vista, 0 |
| Abrir «Todos los discos de «lo escrito»» | 1 o 2 (`/search?type=album&limit=10&offset=..`), igual por página |
| Entrar a «Canciones de ARTISTA» | 1 por cada 10, hasta 5 (solo mientras alguien está dentro); quedarse elegida en la pared: 1 (la primera página) |
| Un disco de una pared elegido 0,6 s, o al entrar | 1 (el del catálogo, g52/g49; 2 si tiene más de 50 canciones) |
| REGRESAR al buscador, volver a abrir una pared ya vista | 0 (de memoria) |

La página de al lado **no** se pide por adelantado (la idea del pendiente decía «y si acaso la siguiente»): así cada página cuesta 1 o 2
peticiones y solo si alguien la mira; la casilla que aún no llega se ve vacía unos cientos de milisegundos.
| Los atajos de SP9 | 2 por sesión (con `refresh_token`); 0 sin él |

El estado (`/run/rocola-xp/spotify/catalogo`) suma `paredes=` (páginas de «Todos los discos» y de «Canciones de») y `atajos=`.

## 4. Las pruebas (con números)

`Pruebas/RocolaOs/catalogo/prueba.sh` (rocola-xp de verdad, fuera de pantalla, datos inventados, la API web FALSA y el Soloist FALSO del
núcleo; nunca el de verdad). La API falsa ahora entiende `offset` y `artist:"..."` en search, da los artistas que contienen lo escrito o
están a dos letras y detrás uno que no tiene que ver (como Spotify), `/me/top/tracks` y `/me/player/recently-played`, un artista con 40
discos (Banda Interminable: 41 casillas, 3 páginas, 120 canciones) y «Otra Banda» (la de los datos de prueba de XP) con 3 discos; su registro
apunta `t=tipo off=N` en las peticiones con offset. Cada escena imprime su cuota.

Escenas nuevas:

| Escena | Qué hace | Comprueba (y cuota medida) |
|---|---|---|
| `pared` | «interminable»; elige Banda Interminable; ARRIBA a la 2.ª página; entra al 1.er disco y pide; REGRESAR; PONER entra otra vez y pide la 2.ª; REGRESAR; REGRESAR (buscador); PONER (la misma pared); REGRESAR y cierra el buscador | 1.ª página = 2 peticiones (off=0, off=10), 2.ª = 2 (off=20, off=30), una vez cada una aunque se abrió dos veces; la 3.ª nunca; 1 del disco; misma página y casilla (la 2.ª pedida es la 2.ª del mismo disco: can5181, can5182); el buscador volvió con la misma elección (de Orquesta nada); de «Canciones de» solo off=0; sin cuenta, nada de `/me`; `artistas=4`; nada de Spotify en los datos. **Cuota: 8 (NativeAOT) y 9 (JIT) a /v1** (2-3 búsquedas, según cuántas veces se para de escribir 300 ms; 4 de la pared; 1 de «Canciones de»; 1 disco) |
| `canciones` | entra a «Canciones de Banda Interminable», pide la 1.ª, ARRIBA (la última) y la pide | 5 peticiones (off=0..40), ni una 6.ª; la 1.ª es can5011 y la 2.ª la 50.ª (can5172). **Cuota: 8 (NativeAOT) y 10 (JIT)** (1-3 búsquedas + 2 de la pared + 5) |
| `todos` | baja a «Todos los discos de «interminable»» (la 14.ª tarjeta), la abre, ARRIBA ×3 (la última no hace nada), ABAJO ×2, REGRESAR | off=0..40 una vez cada una (5), ninguna más; como mucho un disco por página mirada (g52); `paredes=5`. **Cuota: 10** (1 + 5 + 4 de discos: el de 60 canciones son 2) |
| `mas` | «otra»: tu artista Otra Banda y detrás «Otra Banda · más en Spotify»; su pared; dos a la derecha y PONER; REGRESAR ×2; tu tarjeta de XP | 1 página (3 discos), la pared con 3 casillas (sin el tuyo), la 3.ª casilla es el Tercero (dis651) y del tuyo (dis649) nada; tu tarjeta lleva a tu pared sin pedir nada. **Cuota: 4** |
| `atajos` | con la cuenta: los atajos, «Tus más escuchadas» y pide la 1.ª; sale; «Escuchado hace poco» y pide la 1.ª; cierra y abre el buscador | 1 petición de cada atajo con la llave de la cuenta, aunque se abrió dos veces; can0012 y can0002; `atajos=2`; sin secretos. **Cuota: 5** (3 de la cuenta + 2) |
| `pared429` | abre la pared; Spotify frena (429, 60 s); ARRIBA, ABAJO, ARRIBA; REGRESAR; elige Orquesta | 1 solo 429 y después **ninguna** petición a /v1; la 2.ª página nunca llega (casillas vacías y el aviso de XP); de Orquesta nada; la bitácora y el estado dicen que frena. **Cuota: 4 (NativeAOT) y 5 (JIT)** |

Escenas del catálogo que cambian por lo pedido: `busca`, `suena` y `sinsesion` (el artista de Spotify va arriba: las canciones son la 2.ª y
3.ª tarjeta), `artista` (abre su pared: 1 página, 3 casillas, «Grupo Ficticio» no sale) y `disco` (REGRESAR vuelve al buscador, PONER abre el
mismo disco de memoria y se pide la 3.ª; después ya está en la letra y REGRESAR va a ella). `apagado` y `cero` además pulsan REGRESAR en la
pared de XP y dentro de un disco de XP (g53 y g54 sin el modo): 0 peticiones, y la traza de `cero` igual a la de `apagado`.

**Resultados (15-11-2026, el programa de 80fb9b5):**

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release` | 0 errores, 13 avisos (los de la base; ninguno en `RocolaOs/`) |
| `dotnet publish` NativeAOT | sale con 0, **0 avisos IL/AOT** |
| `prueba.sh` en JIT (las 19 escenas: 13 del catálogo y 6 nuevas) | **116 de 116** BIEN |
| `prueba.sh` en NativeAOT (las 19) | **116 de 116** BIEN |
| `ganchos.py` | TODO BIEN |
| Nivel 0 y nivel 1 (`nivel0.sh` de fase11-nivel0) | sale con 0 los dos: ver §6 |

Capturas para el dueño (NativeAOT, 1024×768, API falsa, datos inventados), en
`/tmp/claude-0/-home-user-Rocola/b54ee812-4417-50ce-9f97-015b8aeef72d/scratchpad/paredes-capturas/`: `buscador-artistas-de-spotify`,
`pared-artista`, `pared-artista-pagina2`, `disco-de-la-pared`, `pared-al-volver-del-disco`, `buscador-al-volver-de-la-pared`,
`pared-otra-vez`, `pared-xp-al-cerrar-el-buscador`, `disco-canciones-de`, `disco-canciones-de-al-final`, `buscador-todos-los-discos`,
`pared-todos`, `pared-todos-pagina3`, `buscador-al-volver-de-todos`, `buscador-mas-en-spotify`, `pared-mas-en-spotify`,
`disco-mas-en-spotify`, `buscador-al-volver-de-mas`, `pared-xp-del-artista`, `buscador-atajos-spotify`, `buscador-tus-mas-escuchadas`,
`buscador-escuchado-hace-poco`, `pared-429`, `buscador-429-artista`, `buscador-al-volver-del-disco`, `pared-artista-pocos` (y las del
catálogo, rehechas).

## 5. Para el A4 (no se pudo probar aquí)

Nada de esto se probó con la API de verdad, la app del dueño, su cuenta, Soloist de verdad ni en el A4. Lo que hay que mirar allí:

1. Buscar un artista que no tengas: que salga arriba (o detrás de los tuyos) con su foto y que su pared abra en 1-2 s con la red del bar.
2. Que `artist:"NOMBRE"` dé sus canciones (Spotify puede mezclar colaboraciones: van en su orden).
3. Que `/v1/me/top/tracks` y `/v1/me/player/recently-played` contesten con la autorización del dueño: necesitan los permisos
   `user-top-read` y `user-read-recently-played` al autorizar con PKCE; sin ellos dan 403 y los atajos no salen (la bitácora lo dice:
   «un atajo no llego»).
4. Contar en `/run/rocola-xp/spotify/catalogo` (`peticiones`, `artistas`, `paredes`, `atajos`) tras una hora de uso, para la cuota.
5. Cómo están nombradas las carpetas del dueño: «más en Spotify» compara los nombres de disco de XP (`Artista - Disco`); con carpetas sin
   « - » solo reconoce el disco que se llama como el artista.

## 6. Nivel 0 y nivel 1

Con `Pruebas/RocolaOs/nivel0.sh` (de la rama fase11-nivel0), el programa de 80fb9b5 (JIT y la publicación NativeAOT), alternando con la
referencia congelada; las escenas que pueden mover los ganchos nuevos (REGRESAR en la pared y en un disco, el buscador y sus atajos) y las de
siempre:

| Batería | Resultado |
|---|---|
| Nivel 0 (sin `ROCOLA_OS`; pasos previo, código, ganchos, escenas, aot) | previo, código (109 de 109 con las mismas líneas y CRLF que fc7b667), `ganchos.py` (30 ganchos en 13 archivos) y `diff-xp.py --sin-ganchos` (5 095 / 4 828 / 229: las de la base) **BIEN**; escenas arranque, moneda, buscador, captura y asistente **IGUAL**; tab y sigterm **ACEPTADO** (los rótulos del repaso; el estado se borra: lo aceptado de siempre); NativeAOT arranque y buscador **IGUAL**. Sale con 0 |
| Nivel 1 (`NIVEL=1`, `ROCOLA_OS=1` con lo de Rocola OS de fábrica) | las 7 escenas: 6 **IGUAL** y sigterm **ACEPTADO**; NativeAOT arranque y buscador **IGUAL**. Sale con 0 |

(La rama sale de `junta-spotify` en 4fb1656, antes de que la junta encendiera el modo Spotify de fábrica: en el nivel 1 el modo va
apagado. Con el modo encendido de fábrica y sin llaves, los ganchos nuevos no hacen nada: sin la llave de la API no hay resultados de
Spotify ni, por tanto, paredes; REGRESAR en una pared de XP y en un disco de XP va por lo de XP; los atajos de SP9 necesitan la cuenta.)
