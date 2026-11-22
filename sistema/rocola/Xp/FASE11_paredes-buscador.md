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
| g53 | `Form1.cs` 2185 (ProcessCmdKey, en la pared: `if (IsBack(kc)) { return true; }`) | REGRESAR en una pared del buscador vuelve al buscador (su repetición no: revisión, abajo) |
| g54 | `Form1.cs` 2174 (ProcessCmdKey, en un disco: `if (IsBack(kc)) { BackToAlbums(); return true; }`) | Un disco de Spotify abierto desde el buscador que no estaba en la letra vuelve al buscador |
| g55 | `BuscadorPortadasGpu.cs` 146 (ArmaAtajos) | Los atajos de SP9 detrás de los de XP |
| g56 | `BuscadorPortadasGpu.cs` 198 (UsaAtajo) | Pulsar uno de esos pone sus canciones como resultados |
| g57 | `Form1.cs` 2152 (ProcessCmdKey, en el buscador: `if (IsBack(kc)) { searchOverlay.Backspace(); return true; }`) | (revisión) La repetición del rojo que volvió al buscador no borra lo escrito |

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
| Los atajos de SP9 | 2 por sesión (con `refresh_token`); 0 sin él. Si no llegaron por la red (sin red, 5xx, 429), otra vez al abrir el buscador pasado un minuto (revisión cuota-hilos-y-fallos) |
| Hojear deprisa (mantener ARRIBA/ABAJO) | solo las páginas que se están mirando cuando les toca en la cola; las que ya pasaron se dejan sin pedir (revisión cuota-hilos-y-fallos) |

La página de al lado **no** se pide por adelantado (la idea del pendiente decía «y si acaso la siguiente»): así cada página cuesta 1 o 2
peticiones y solo si alguien la mira; la casilla que aún no llega se ve vacía unos cientos de milisegundos.

El estado (`/run/rocola-xp/spotify/catalogo`) suma `paredes=` (páginas de «Todos los discos» y de «Canciones de»), `atajos=` y `dejadas=`
(páginas que se dejaron en la cola sin pedir porque ya nadie las miraba).

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

## Revisión (funcionamiento-y-reglas)

Revisor independiente, 15-11-2026. Enfoque: cada punto (a)-(g) contra la decisión del dueño del 15-11-2026 y el pedido del 7-11-2026; XP
intacto sin el modo o sin llave; nada de dibujo nuevo; la pila de REGRESAR en todos los caminos (también con el buscador cerrado por
tiempo, una moneda, la atracción o el asistente en medio). Se leyó el código (no el informe) y se corrieron sus pruebas y dos escenas nuevas.

**Lo que se comprobó y está bien** (en el código y con la prueba):

- (a) Disco o lista de Spotify abierto desde el buscador que no estaba en la letra ♫: `enPared` se mira **antes** de meterlo a los
  recientes; g54 solo actúa si el disco de dentro es ese (`ReferenceEquals(currentAlbum, rocolaOsSpVueltaAlbum)`) y cualquier otra entrada a
  un disco (g49, `EnterAlbum`) lo olvida. Ya en la letra: `BackToAlbums` de XP.
- (b) Artistas: los tuyos se comparan con `MotorBusqueda.Normaliza` de XP contra el artista de tus discos; hasta 3; el primero que da
  Spotify con la regla de «a dos letras», los demás si lo contienen (la regla escrita del 7-11-2026; el programa anterior además aceptaba
  «está contenido» en todos: aquí solo en el primero, como dice la regla). Justo detrás de tus artistas que salen primero, o arriba.
- (c)-(f) La pared es la rejilla de XP con una «letra» fuera de la barra (g46 da el orden vacío: `GotoLetter` con `n = 0` no indexa nada);
  la cabecera por g47; las casillas que no llegan son discos de XP vacíos; «Canciones de», «Todos los discos» y «más en Spotify» son
  tarjetas de XP (`SearchItem` con su `Display`). **Nada de dibujo nuevo** (vistas las capturas: cabecera, rejilla, ficha, buscador).
- (g) Los atajos: `RocolaOs.Activo`, el modo y `HayCuenta` antes de pedir nada; una petición cada uno por sesión.
- **Sin el modo** (y sin `ROCOLA_OS`): g53 no hace nada (la letra nunca es ♪), g54 da `false` (BackToAlbums), g55 sale al principio, g56
  solo mira los tipos 100 y 101, g57 (nuevo, abajo) da `false`. Escenas `apagado` y `cero`: 0 peticiones, nada en /run. **Con el modo y
  sin la llave de la API**: no hay resultados de Spotify, así que ninguna tarjeta abre una pared ni marca una vuelta; los atajos piden
  `HayCuenta`.
- La pila: disco → pared (misma página y casilla: XP) → buscador (lo escrito y la elección) → la pared de debajo. Con el buscador
  cerrado por el minuto de XP queda la pared de debajo; con la atracción, XP cierra el disco (`BackToAlbums`) y la pared sigue ahí al
  despertar; la moneda no toca la vista; el disco que se cierra solo a los 30 s vuelve a su pared (XP) y la vuelta vieja se olvida al
  entrar a otro disco. Si la página no llega mientras el buscador sigue abierto, el aviso de XP y el buscador se queda (`!searchOpen` al
  llegar: no se abre nada encima de otra cosa).

**Lo que estaba mal y se arregló** (código y prueba):

1. **El rojo mantenido se saltaba la pila y borraba lo escrito.** Los botones de la rocola repiten como un teclado de XP (a los 500 ms,
   unas 30 por segundo: `TeclasLinux`). Dentro de un disco de una pared, mantener el rojo 1,5 s: la pulsación llevaba a la pared (XP),
   la 1.ª repetición pasaba por g53 al buscador y las demás eran «borrar letra» (`searchOverlay.Backspace`, lo de XP en el buscador): el
   cliente acababa en el buscador **vacío**. Comprobado con el programa de 80fb9b5 (NativeAOT, la escena nueva `mantenido`: 3 MAL de 5;
   la captura muestra el buscador sin «interminable»). Arreglo: g53 no atiende las repeticiones (`RocolaOsSpotifyParedRegresa(msg.Repetida)`,
   como XP en un disco, cuya pulsación ya sacó del disco y cuyas repeticiones no hacen nada en la pared); y un gancho nuevo, **g57**
   (`Form1.cs` 2152, el REGRESAR del buscador: `if (!RocolaOsSpotifyTragaRegresar(msg.Repetida)) searchOverlay.Backspace();`), que no
   borra con las repeticiones de la pulsación que volvió al buscador (una pulsación nueva, o una repetición más de 1 s después de la
   anterior —el soltar se perdió—, lo olvida). Sin el modo nunca se marca: siempre `Backspace`, como XP.
2. **ROJO+ARRIBA en una pared dejaba la pared de XP de antes.** En la pared el rojo es REGRESAR (vuelve al buscador al apretarlo), y
   ARRIBA con el rojo apretado es la combinación de XP de abrir/cerrar el buscador: lo cerraba enseguida. Arreglo, sin gancho nuevo: al
   volver al buscador con el rojo apretado (`BackHeld()`), el disparo del buscador de XP (`UnDisparo`, «una vez por pulsación») queda
   tomado hasta soltar, como si la combinación ya hubiera hecho lo suyo: el buscador se queda abierto con lo escrito (en XP, ROJO+ARRIBA
   desde un disco también acaba en el buscador). Para cerrarlo, soltar y volver a apretar (la regla de XP del 14-10-2026).
3. (menor) Tus artistas se recordaban por el número de discos; un re-escaneo con el mismo número pero otra música los dejaba viejos.
   Ahora también por la lista (`albums` es otra tras re-escanear).

Ganchos: g53 cambia su línea (pasa `msg.Repetida`) y **g57** es nuevo (`Form1.cs` 2152), los dos en `GANCHOS.tsv`, CRLF conservado.
`ganchos.py`: **TODO BIEN** (31 ganchos en 13 archivos; Codigo/ vuelve byte a byte a fc7b667). `diff-xp.py --sin-ganchos` (109
archivos): 5 095 / 4 828 / 229, las de la base. Si la junta ya usó g57, se renumera.

**Escenas nuevas** en `Pruebas/RocolaOs/catalogo/prueba.sh`:

| Escena | Qué hace | Comprueba |
|---|---|---|
| `mantenido` | pared de Banda; 2.ª página; entra a un disco; **rojo mantenido 1,5 s**; PONER, PONER (pide); REGRESAR; **rojo mantenido 1,5 s** en la pared; PONER; **ROJO (1,2 s) + ARRIBA** en la pared; PONER | la 1.ª pedida es can5181 (el rojo mantenido se quedó en la pared) y es la única; la pared de Banda se abrió 3 veces (lo escrito no se borró y ROJO+ARRIBA no cerró el buscador); de memoria (4 peticiones de artista) y nada de Orquesta |
| `tiempo` | pared de Banda y nada más: **atracción** a los 40 s; **moneda** (la despierta y acredita); REGRESAR (buscador) y PONER (la pared otra vez); REGRESAR; **un minuto** sin tocar el buscador (solo **dos monedas**, que no cuentan como uso del buscador y alejan la atracción): **se cierra solo**; REGRESAR (nada, XP); PONER; REGRESAR | la pared de Banda abierta 2 veces y la 2.ª de memoria (2 peticiones de artista); nada pedido de Spotify; tras cerrarse por tiempo quedó la pared de XP de antes (PONER entró a un disco de XP: ningún `/v1/albums/` ni la pared otra vez; capturas: «LETRA L», «Los Ejemplos», 16 créditos) |

**Resultados** (el programa con estos cambios; JIT y NativeAOT compilados en `/root/rocola-trabajo/fase11/paredes-rev-fr`, fuera de
la copia de trabajo):

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release` (JIT) | 0 errores, 13 avisos (los de la base; ninguno en `RocolaOs/`) |
| `dotnet publish` NativeAOT (linux-x64) | sale con 0, **0 avisos IL/AOT** |
| `prueba.sh` en JIT (las 21 escenas: las 19 de antes, `mantenido` y `tiempo`) | **TODO BIEN, 126 de 126** |
| `prueba.sh` en NativeAOT (las 21) | **TODO BIEN, 126 de 126** |
| `mantenido` con el programa de 80fb9b5 (NativeAOT, antes del arreglo) | **3 MAL de 5**: ninguna pedida (el rojo mantenido acabó en el buscador vacío) y la pared abierta 1 vez en vez de 3 |
| Cuota de las escenas nuevas (JIT / NativeAOT) | `mantenido` 7 / 7 a /v1 (1 búsqueda, 4 de artista, 1 de «Canciones de», 1 disco); `tiempo` 5 / 4 (1-2 búsquedas, 2 de artista, 1 de «Canciones de»); las 19 de antes, las del informe del grupo |
| `ganchos.py` | **TODO BIEN**: 31 ganchos en 13 archivos; Codigo/ vuelve byte a byte a fc7b667 |
| `diff-xp.py --sin-ganchos` (109 archivos) | 5 095 / 4 828 / 229, las de la base |
| Nivel 0 (`nivel0.sh` de fase11-nivel0, sin `ROCOLA_OS`, alternando con la referencia) | sale con 0: previo, código, ganchos y diff-xp **BIEN**; arranque, moneda, tab, buscador, captura y asistente **IGUAL**; sigterm **ACEPTADO** (el estado se borra); NativeAOT arranque y buscador **IGUAL** (13 BIEN, 1 ACEPTADO, 0 MAL) |
| Nivel 1 (`NIVEL=1`, `ROCOLA_OS=1`) | sale con 0: arranque, moneda, tab, buscador, captura (a la segunda: la primera, solo cuentas del reloj de 42 ms, 323 frente a 338) y asistente **IGUAL**; sigterm **ACEPTADO**; NativeAOT arranque y buscador **IGUAL** (10 BIEN, 1 ACEPTADO, 0 MAL). El modo Spotify va apagado, como en la base de la rama (antes del encendido de fábrica de la junta) |

**Lo que queda abierto** (no se arregló; ninguno rompe la pila):

- Volver al buscador desde una pared no pasa por el tope de aperturas de XP (`TopeBuscadorDeja`: no dos en 0,7 s ni más de seis en 30 s).
  Cada vuelta cuesta un PONER y un REGRESAR (no una combinación que se pueda dejar apretada), así que no se abre «cientos de veces» por
  accidente; si en el A4 se ve que alguien lo machaca, se le pone el tope.
- Con 9 o más de tus artistas al principio, las de Spotify (canciones y discos, a la 8.ª de XP: regla del catálogo) quedan en medio de
  tus artistas, y los artistas de Spotify detrás de todos los tuyos. Raro (una búsqueda de dos letras); no se tocó la regla del catálogo.
- Si los atajos de SP9 fallan (403 sin los permisos, 429, sin red), no se vuelven a pedir en la sesión (por diseño: una por sesión).
- «spotify apagar» con alguien dentro de una pared: se queda en una «letra ♪» vacía (cabecera «LETRA ♪») hasta REGRESAR (el buscador) o
  REGRESAR+izq/der (las letras de XP). Lo mismo le pasa hoy a la letra ♫ del catálogo. Solo pasa si el dueño lo apaga a distancia en ese
  momento.
- Si la página de una pared llega cuando ya se cerró y se volvió a abrir el buscador (sin elegir otra cosa que abra), la pared se abre
  igual: el mismo patrón que los discos del catálogo (`rocolaOsSpAbriendo` y `searchOpen`).
- El asistente: con su menú a la vista se queda con los botones (lo de XP), así que no llega nada a g53/g54/g57; ROJO+ABAJO en una pared
  hace lo mismo que en un disco de XP (primero la vuelta, luego la ayuda encima). No hay escena automática del asistente en la pared.
- Nada de esto se probó con la API de verdad, la cuenta del dueño, Soloist de verdad ni en el A4; la repetición de los botones del A4
  (la placa de botones USB) es la que hay que ver allí: mantener el rojo en un disco de una pared y en la pared.

### Segunda pasada (funcionamiento-y-reglas, después de c0df5ad)

La revisión de arriba dejó dos huecos de este enfoque sin prueba automática: el modo **encendido sin llaves** (como queda de fábrica
tras la junta: decisión del 15-11-2026, «Encendido sin llaves la rocola debe quedar como XP en lo que se ve y suena») y **el asistente en
medio de la pila**. Se comprobó sobre el programa con los cambios de las dos revisiones (c0df5ad; sin cambios de código en esta pasada:
`Codigo/` y `RocolaOs/` no se tocan; `ganchos.py` **TODO BIEN**, 31 ganchos). Dos escenas nuevas en `prueba.sh`:

| Escena | Qué hace | Comprueba |
|---|---|---|
| `sinllave` | `ROCOLA_OS=1`, `spotify=1` y **sin** `rocola_spotify_api.txt` (ni la llave de la API ni la cuenta); las mismas teclas que `apagado`/`cero` (buscar «inventad», elegir, la pared de XP, un disco de XP, REGRESAR, ARRIBA, REGRESAR) | ni una petición (ni la llave); ninguna pared ni vuelta del buscador en la bitácora; nada cobrado ni un `play` al Soloist falso; la traza igual a la de `apagado` (sin `ROCOLA_OS`) quitando lo de `RocolaOs:` (`compara_trazas.py --ignora`, holgura 80) |
| `ayuda` | pared de Banda; **ROJO (mantenido 1 s) y ABAJO 0,3 s después**: el rojo vuelve al buscador (g53) y ABAJO abre la ayuda encima; el rojo la cierra; PONER (la pared otra vez); ARRIBA; PONER (disco de la casilla 19); **ROJO+ABAJO** igual: la pared (XP) y la ayuda encima; el rojo la cierra; PONER, PONER (pide); REGRESAR, REGRESAR, PONER | la ayuda se ve abierta en las dos capturas y cerrada en las dos de después (el círculo verde del globo, píxel 948,369); la pared de Banda abierta 3 veces (el buscador conservó lo escrito y la elección tras la ayuda); la única pedida es can5181 (la pared conservó página y casilla tras la ayuda); 4 peticiones de artista (de memoria) y nada de Orquesta |

Resultados (programas JIT y NativeAOT de c0df5ad, compilados por la revisión de cuota en `/root/rocola-trabajo/fase11/paredes-rev-ch`,
usados sin tocar; la prueba corre en `/root/rocola-trabajo/fase11/paredes-rev-fr`):

| Comprobación | Resultado |
|---|---|
| `apagado sinllave ayuda` en JIT | **TODO BIEN, 14 de 14** (en la primera corrida, 1 MAL: la comparación de trazas de `sinllave`, porque `apagado` arrancó en frío y su SIGTERM llegó a los 22,1 s de su reloj, antes de sus tres últimas teclas; es el mismo margen de tiempo de `cero`, no del programa) |
| `apagado sinllave ayuda` en NativeAOT, dos veces | **TODO BIEN, 14 de 14** las dos (`sinllave`: «IGUALES con la holgura del ruido»; `ayuda`: 7 a /v1 = 1 búsqueda, 1 página de búsqueda, 4 de artista, 1 disco) |

**Lo que enseñó la escena `ayuda`** (no es un fallo; es XP): con `27+40` (los dos en el mismo milisegundo, como lo mandó una vez el
arnés en NativeAOT) el rojo ya cuenta como el atajo de la ayuda (`EsAtajoAyuda` mira también ABAJO apretado), así que la ayuda se abre
encima de la pared y el cliente se queda en la pared, sin volver al buscador; con el rojo primero, vuelve al buscador y la ayuda se abre
encima. Es lo mismo que hace XP desde un disco (con el rojo primero, la pared y la ayuda; a la vez, la ayuda encima del disco). Por eso la
escena aprieta el rojo primero y ABAJO 0,3 s después; las dos formas dejan la pila entera (la pared o el buscador siguen debajo, con su
página, casilla y lo escrito).

Sigue abierto lo de la lista de arriba (el tope de aperturas al volver al buscador, «spotify apagar» dentro de una pared, la repetición
real de la placa de botones del A4). Ni esto ni nada de arriba se probó con la API de verdad, la cuenta del dueño, Soloist de verdad ni en
el A4.

## Revisión (cuota-hilos-y-fallos)

Revisor independiente, con este enfoque: peticiones contadas por acción (nada de fondo, nada por cuadro ni por tecla mantenida), 429 con
Retry-After, 401, 5xx y sin red a media pared, que el hilo de la pantalla nunca espere a la red, listas que llegan mientras se mira,
memoria y portadas al hojear deprisa, NativeAOT. Se leyó el código (`RocolaOs.SpotifyCatalogoParedes.cs` entero, lo nuevo del cliente en
`RocolaOs.SpotifyCatalogoApi.cs` y los ganchos del catálogo que lo llaman) y se corrieron sus pruebas y dos escenas nuevas.

**Lo que se encontró y se arregló:**

1. **Mantener ARRIBA/ABAJO en una pared pedía TODAS las páginas por las que pasaba** (el fallo de cuota de esta revisión). Cada página
   que la repetición de XP pasaba (unas 30 por segundo) encolaba sus 1 o 2 bloques; la cola los pedía todos, uno tras otro, aunque ya
   nadie los mirara, y la página donde uno se quedaba esperaba detrás de ellos. Reproducido con la escena nueva `hojea` (300 discos, 17
   páginas, la red 400 ms más lenta, ARRIBA mantenido 2,5 s) con el programa de 653cefe: **31 peticiones de la pared** (todas las
   páginas, de off=0 a off=290). Arreglo: cada petición de una página de pared lleva un «sigue» (`Pedido.Sigue`, `PidePagina(..., sigue)`)
   que el hilo del catálogo pregunta cuando le toca: la pared publica la página que se mira en un campo volátil (`PaginaPub`, en cada
   `RefreshAlbums`; -1 al dejar la pared) y cada bloque sabe en qué páginas caen sus casillas (`Inicio`/`Fin`, de `Recompone`). Si ya
   no se mira, se deja **sin preguntar a la red** y se contesta `Dejada` («ya no se mira»): no es un fallo (ni bitácora, ni aviso, ni
   `Fallados`), y si entre tanto se volvió a esa página, se pide otra vez. Con el arreglo: **11 o 12 peticiones** (off=0..30 al abrir y en
   la 2.ª página, 1-3 que estaban en camino mientras se hojeaba, la última, off=280 y 290, una vez cada una, y la 16.ª con su 503);
   `dejadas=22`-`23` en el estado. Las portadas siguen a los bloques que llegan: 93-103 portadas (4,3 MB en `/run`) en vez de unas 300.
2. **Los atajos de SP9 no volvían en toda la sesión si la primera vez no había red** (5xx, 429 o sin red al abrir el buscador: el
   router del bar que tarda en prender). Ahora, con un error pasajero (`Pasajero`), se vuelven a pedir al abrir el buscador pasado un
   minuto (lo que sí llegó sale de memoria, 0 peticiones); un 403 (faltan los permisos `user-top-read`/`user-read-recently-played`) o
   sin la cuenta, no: una vez por sesión, como antes. El renglón «un atajo no llego» va por `ApuntaCallado` (como mucho uno cada 10
   min). Sustituye, para los errores pasajeros, el punto «Si los atajos de SP9 fallan [...] no se vuelven a pedir en la sesión» de la
   revisión de funcionamiento-y-reglas. Escena nueva `atajos503`.
3. **La mezcla del buscador recorría toda la biblioteca en el hilo de la pantalla** dos veces por cada artista tuyo que Spotify también
   da (`RocolaOsSpotifyHayMas` y `RocolaOsSpotifyMasTarjeta` llamaban a `RocolaOsSpotifyTusDiscos`, que normalizaba el artista y el
   disco de cada `Album`). La mezcla corre con cada tecla cuando la búsqueda de Spotify ya está en memoria: con 10 artistas tuyos y 1 500
   discos, unas 30 000 normalizaciones por tecla (cuenta hecha, no medida). Ahora los discos de cada artista salen de la misma pasada que «tus artistas»
   (`rocolaOsSpTusDiscos`, rehecha solo si cambia la biblioteca): una búsqueda en un diccionario.
4. **Un bloque que llega con menos de 10** (en «más en Spotify» se quitan tus discos; Spotify a veces da menos) corre las casillas de
   detrás: si la elegida ya era un disco y sigue en la misma página, la elección se queda en SU disco en vez de saltar al de al lado.
   Solo pasa con bloques que llegan fuera de orden (volver a una página tras hojear); no hay escena que lo fuerce.

**Lo que se miró y está bien** (sin cambios):

- El hilo de la pantalla no espera a la red: todo lo que pide va por la cola del catálogo (su hilo) o por el hilo de las portadas, y lo que
  vuelve, por `RocolaOs.Encarga`; lo que la pantalla lee del cliente (`PaginaYa`, `AtajoYa`, `BuscaYa`) toma el cerrojo solo para mirar
  un diccionario (el hilo del catálogo nunca lo tiene durante una petición); `HayLlave`/`HayCuenta` leen el archivo como mucho cada 5 s.
- Nada por cuadro: g52 (en cada cuadro) pide un disco una sola vez por disco y sesión tras 0,6 s elegido (`rocolaOsSpPrevistos`); una
  casilla vacía (hueco) no pide nada; g47 corre en `RefreshAlbums`, no por cuadro; lo que falló en una página no se repite hasta volver a
  mirarla (`Fallados`), así que con 429 o sin red no hay tormenta.
- 429 con Retry-After (segundos o fecha), 401 (una llave nueva y otra vez) y 5xx (un reintento a 1 s) son los del cliente del catálogo;
  la escena `pared429` y ahora `hojea` (503 a media pared: la página falla, el aviso de XP, y al volver a mirarla llega) lo cubren.
- Índices: la casilla que llega a un hueco donde ya se entró comprueba el índice contra la lista nueva; `RocolaOsRestaura` acota fila,
  columna y tarjeta; XP acota página y casilla en `RefreshAlbums`.
- «Canciones de ARTISTA» sigue pidiendo solo mientras alguien está dentro (si sale, la cadena se para; al volver sigue donde iba).

**Pruebas** (programa de esta revisión, en `/root/rocola-trabajo/fase11/paredes-rev-ch`):

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release` (JIT) | 0 errores, 13 avisos (los de la base; ninguno en `RocolaOs/`) |
| `dotnet publish` NativeAOT (linux-x64) | sale con 0, **0 avisos IL/AOT** |
| `hojea` con el programa de 653cefe (JIT, antes del arreglo) | **3 MAL de 6**: **31 peticiones** de la pared (off=0 a 290, todas las páginas), `dejadas` no existe, y el 503 cayó en la cola de lo atrasado |
| `hojea` con el arreglo | JIT: 12, 11 y 11 peticiones de la pared en tres corridas (las 4 de abrir y la 2.ª página, 3-5 que estaban en camino al hojear, la última página y la 16.ª), `dejadas=22`-`23`, 93-103 portadas (4,3 MB en `/run`) en vez de unas 300; NativeAOT: 11 |
| `prueba.sh` en JIT (las 23 escenas: las 21 de antes, `hojea` y `atajos503`) | **133 de 138**: los 5 MAL, todos de `fallos`, por la carga de la máquina (load 13 con otras tres baterías a la vez): las letras se escribieron con más de 300 ms entre ellas, salió una búsqueda intermedia («inventa») y los cambios de humor de la API falsa llegaron antes de tiempo. `fallos` repetida sola (load 6): **7 de 7**. Nada de eso pasa por lo cambiado |
| `prueba.sh` en NativeAOT (las 23) | **137 de 138**: el MAL, la comprobación del 503 de `hojea`, que pedía off=260 y esa vez el bloque que faltaba fue off=270 (el otro llegó al pasar por la página). La comprobación se corrigió (el bloque que dio 503, sea cual sea) y se repitió: **6 de 6** en NativeAOT (y en JIT, con `fallos`, **13 de 13**) |
| `atajos503` | JIT y NativeAOT **6 de 6**: cada atajo un 503 y, pasado el minuto, un 200 (2 peticiones cada uno); abrir el buscador antes del minuto no pide nada; PONER pide la 1.ª de «Tus más escuchadas» (can0012) |
| Cuota por escena (JIT / NativeAOT, a /v1) | disco 3/2, artista 5/5, pared 10/8, canciones 10/8, todos 13/11, mas 4/4, atajos 5/5, pared429 5/6, mantenido 7/8, tiempo 7/5, hojea 16/15, atajos503 7/7; las diferencias son búsquedas intermedias al escribir (la carga), lo de las paredes es lo mismo que en el informe del grupo |
| `ganchos.py` | **TODO BIEN** (31 ganchos; Codigo/ no cambia en esta revisión) |
| `diff-xp.py --sin-ganchos $(ls Codigo)` | 5 095 / 4 828 / 229, las de la base |

Nivel 0 y nivel 1 no se repitieron: esta revisión no toca `Codigo/` ni nada que corra sin el modo Spotify (todo lo cambiado está detrás
de `RocolaOsSpotifyCatalogoVa` o en el hilo del catálogo, que sin el modo no se crea).

**Lo que queda abierto** (de este enfoque):

- Las portadas de Spotify en `/run` (tmpfs, en la memoria) no tienen tope en la sesión: cada disco que llega baja la suya (unos 30-40 KB
  a 300 px). Hojear ya no las multiplica, pero una fiesta larga buscando mucho puede juntar miles (decenas de MB). Es del catálogo, no de
  las paredes; si en el A4 se ve `/run` crecer, se le pone un tope con borrado de las más viejas.
- Abrir una pared y cerrar el buscador antes de que llegue: sus 1 o 2 peticiones siguen (son de una acción de alguien; no se dejan).
- Ni la red lenta del bar, ni la API de verdad, ni la repetición de la placa de botones del A4: `hojea` simula 400 ms por petición y la
  repetición de XP del arnés (500 ms y ~30/s). En el A4: mantener ARRIBA en la pared de un artista grande y contar en
  `/run/rocola-xp/spotify/catalogo` `artistas=` y `dejadas=`.

### Segunda pasada (cuota-hilos-y-fallos, después de e944058)

Mismo enfoque, sobre lo que la primera pasada no cubrió: qué pasa cuando Spotify **dice de más** (el `total`), las **portadas sin red** y
una casilla a la que se entró y que resulta no existir. Se leyó otra vez el cliente (`AtiendePagina`, `Pide`, la cola) y las paredes.

**Lo que se encontró y se arregló:**

1. **Con un `total` de más, la pared pedía sola hasta el final** (el fallo de cuota de esta pasada). La pared se arma con el `total` del
   primer bloque, y el de `search` es una estimación de Spotify que puede quedarse muy por encima de lo que da de verdad (la API falsa
   ahora lo imita: `/control/miente?total=N`). Al llegar a la última página de verdad, los bloques de detrás llegan vacíos: la lista se
   corría, la página que se mira se llenaba de casillas de los bloques siguientes, `RefreshAlbums` las pedía, llegaban vacías, y así
   **sin que nadie tocara nada** hasta `offset=990`. Reproducido con la escena nueva `miente` («Todos los discos de «interminable»», 41
   discos y `total` 1000) con el programa de c0df5ad/e944058: **100 peticiones de la búsqueda de discos** (off=0 a 990) en 8 s.
   Arreglo (`RocolaOsSpotifyLlegoBloque`): un bloque que trae menos de 10 en `items` (contando los `null`, `Pagina.Crudos`) es el final, y
   un `total` más chico que el de antes manda; nunca por debajo de lo que ya llegó (`FinCrudo`; uno vacío no cuenta: puede llegar antes que
   el de delante). Con el arreglo: **6** (off=0 a 50; off=50 sale con la 3.ª página, vacío). «Canciones de» también corta por `Crudos`
   (antes, por las canciones leídas: un `null` a media página lo cortaba antes de tiempo).
2. **Las portadas que no se bajaban por la red no volvían en toda la sesión** (del catálogo, pero se nota en las paredes: una pared abierta
   con el router del bar a medio prender se quedaba sin portadas hasta reiniciar). El disco no la vuelve a pedir (queda esperándola), y
   el hilo de las portadas apuntaba **un renglón por portada** en la bitácora. Ahora (`RocolaOsSpotifyCatalogoPortadas`), sin red, con
   5xx o 429 del servidor de imágenes, otra vez a los 15 s y, si tampoco, a los 60 s (no gasta la cuota de la API; un 404 o una de más de
   4 MB, no); y el renglón va por `ApuntaCallado` (como mucho uno igual cada 10 min). Escena nueva `portadas` (`/control/imgfalla?n=N`:
   el servidor de imágenes da 503 a todo hasta los 21 s): con el programa de antes, **23 portadas con 503, 0 llegaron, 23 renglones**; con
   el arreglo, las 23 llegaron (un 503 y un 200 cada una) y **1 renglón**.
3. **Una casilla a la que se entró y que resultó no existir** (el `total` era de más) dejaba un disco vacío abierto para siempre. Ahora,
   al saberse que la lista se acaba antes, se vuelve a la pared (lo de XP al salir de un disco), en la última casilla. No hay escena que
   lo fuerce (hay que entrar justo a una casilla de más antes de que llegue su bloque); se leyó el camino.

**Lo que se miró y está bien** (sin cambios): un bloque vacío que llega antes que el de delante no sube el final; los bloques de detrás que ya
iban en camino llegan y se ignoran (`Recompone` solo recorre hasta el `total` nuevo); la página que se mira y la elegida las acota XP en
`RefreshAlbums` al encoger la lista; el «sigue» de la cola (primera pasada) sigue valiendo con el `total` corregido; nada de esto corre en
el hilo de la pantalla salvo lo de memoria (`Recompone`, una pasada por la lista al llegar un bloque).

**Pruebas** (programa de esta pasada en `/root/rocola-trabajo/fase11/paredes-rev-ch`; el de antes, c0df5ad, guardado allí como
`jit-antes`/`aot-antes`):

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release` (JIT) | 0 errores, 13 avisos (los de la base; ninguno en `RocolaOs/`) |
| `dotnet publish` NativeAOT (linux-x64) | sale con 0, **0 avisos IL/AOT** |
| `miente` y `portadas` con el programa de antes (c0df5ad, JIT) | **4 MAL de 8**: `miente` 100 páginas de la búsqueda de discos (off=0 a 990, 107 a /v1); `portadas` 23 con 503 y ninguna llegó, 23 renglones |
| `prueba.sh` en JIT (las 25 escenas: las 23 de antes, `miente` y `portadas`) | **TODO BIEN, 156 de 156** |
| `prueba.sh` en NativeAOT (las 25) | **TODO BIEN, 156 de 156** |
| Cuota de las escenas nuevas (JIT / NativeAOT, a /v1) | `miente` 12 / 11 (1-2 búsquedas, 6 páginas de la búsqueda de discos: off=0 a 50, 4 discos de g52); `portadas` 4 / 5 (1-2 búsquedas, 1 página, 2 de artista) y 46 a `/img/` (23 con 503 y 23 con 200) |
| Cuota de las demás (JIT / NativeAOT) | disco 4/2, artista 4/4, pared 8/8, canciones 8/9, todos 10/10, mas 4/4, atajos 5/5, pared429 4/4, mantenido 9/8, tiempo 4/6, hojea 15/15, atajos503 7/7, ayuda 7/8: lo de la primera pasada (las diferencias son búsquedas intermedias al escribir) |
| `ganchos.py` | **TODO BIEN** (Codigo/ no cambia en esta pasada) |

Nivel 0 y nivel 1 no se repitieron: no se toca `Codigo/` y todo lo cambiado corre solo con el modo Spotify (el hilo de las portadas y las
paredes ni se crean sin él; `sinllave`, `apagado` y `cero` siguen en 0 peticiones y con la traza de XP).

**Lo que queda abierto** (de este enfoque, además de lo de la primera pasada):

- Las portadas de `/run` siguen sin tope en la sesión (lo de la primera pasada): con los reintentos no crecen más (es la misma portada).
- Una casilla de más a la que se entra justo antes de saberse el final (punto 3): leído, sin escena.
- Si Spotify da un `total` de más y además el último bloque llega lleno (10 justos), la pared enseña una página más de casillas vacías
  hasta que alguien la mira: entonces se pide 1 bloque, llega vacío y la lista se acaba. Una petición, no una cadena.
- Nada de esto con la API de verdad (cómo de equivocado viene su `total` de search en 2026), ni con el servidor de imágenes de verdad, ni
  en el A4.

## Sin el abecedario en las paredes del buscador (22-11-2026)

Peticion del dueno: «si nos metiamos a un artista que era de Spotify, se abriria una pared de discos que solo existe dentro del buscador
[...] pero sigue apareciendo la barra de abecedario, eso va a confundir a la gente, debe salir un letrero que diga regresar al buscador y el
boton rojo, y el anuncio Discos-tal artista y al lado un icono de Spotify».

- En una pared del buscador (la letra que la barra no tiene, `RocolaOsSpotifyParedMarca`: la de un artista solo de Spotify, «más en
  Spotify» y «Todos los discos de...») la barra de letras de XP no se dibuja (g240, `LetterBar.RenderContent`), no late el dorado de TOP
  (g241, `GetTopGlow`) ni elige letra con el ratón (g242, `LetterBar.Raton`). En su franja, centrado: el botón rojo como lo dibuja el pie de
  XP (`CtrlDot`, el rojo de `HelpBar`) y «REGRESAR AL BUSCADOR» con la letra del pie (sin remates, negrita) a la medida de la franja (12
  puntos en 1024x768), sobre una cápsula oscura translúcida para que se lea sobre el cielo. Delante, en la misma cápsula, el icono de Spotify y «DISCOS · ARTISTA» al mismo tamaño (en 1024x768 la cabecera de arriba comparte fila con el pie y se encoge; aquí se lee), separados por una rayita; si no cabe todo en la franja, solo el letrero. REGRESAR (el rojo) vuelve al buscador como
  antes (g53).
- La cabecera dice «DISCOS · ARTISTA» (la del artista y la de «más en Spotify») o «DISCOS · «lo escrito»» (todos los discos), y detrás
  del texto el icono de Spotify (círculo verde #1DB954 con las tres ondas negras, dibujado con Skia en `RocolaOsIconoSpotify`; sin archivos
  de imagen) del 62 % del alto de la cabecera: su sitio entra en la medida del nombre (g243) y en el tope del texto (g245), y se dibuja
  detrás del texto (g244). Solo junto a la cabecera de la pared que se mira (`RocolaOsParedCabecera`, la pone `RocolaOsSpotifyParedMira`).
- Todo en `RocolaOs/RocolaOs.ParedLetrero.cs`. Sin Rocola OS o fuera de esas paredes, cada gancho deja lo de XP tal cual. La barra la
  pinta la misma `RenderContent` con tarjeta y sin ella (la textura de la interfaz se rehace al cambiar `Current`, que ya entra en su firma).
- Pruebas: la escena `pared` de `Pruebas/RocolaOs/catalogo` mira en las capturas de 1024x768 el botón rojo en la franja de la barra y que
  la franja difiere de la de XP, el verde del icono en la fila de la cabecera (y ninguno en la pared de XP al cerrar el buscador) y que, al
  volver a XP, el abecedario vuelve sin el letrero.
- No visto en el A4. En 1024x768 la cabecera comparte fila con el pie y BUSCAR/AYUDA: un nombre largo de artista se encoge (como XP
  encoge cualquier cabecera que no cabe).
