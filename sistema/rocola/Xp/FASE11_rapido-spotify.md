# Fase 11, rendimiento: Spotify lo más rápido posible (grupo «rapido-spotify»)

20-11-2026. Rama `rapido-spotify`, sobre `junta-2` (`c90945e`). Es la tarea pendiente #133 del dueño («busca todas las maneras posibles
de que todo cargue lo más rápido que se pueda»), en su parte de Spotify; la petición de hoy: «muchas mejoras, priorizando el Spotify y la
carga de las carátulas en general para que sean instantáneas». Las carátulas de la música local son de otro grupo (`rapido-caratulas`);
aquí, las de Spotify (buscador, paredes del buscador, letra ♫) y las del catálogo abierto (Cover Art Archive) en las letras.

**Todo es de Rocola OS** (`RocolaOs/`): ni `Codigo/` ni `Nativo/` cambian, ningún gancho nuevo (los g235-g249 de este grupo quedan sin
usar). Sin `ROCOLA_OS`, o con el modo Spotify apagado, o sin la llave de la API, nada de esto corre: `rocola-xp` es la de XP. Lo que se ve
y lo que suena es lo mismo que antes, solo antes: ninguna de estas mejoras cambia el aspecto, así que son automáticas, sin interruptor
(decisión del 15-9-2026). **Nada se probó en el A4, ni con Soloist de verdad, ni con la API de Spotify ni el Cover Art Archive de verdad**:
todo con los falsos de las pruebas (con latencia puesta a propósito) y la máquina de pruebas compartida.

## 1. Cómo se midió

`Pruebas/RocolaOs/rapido-spotify/mide.sh` (y `mide.py`): la rocola entera en NativeAOT, fuera de pantalla, con datos inventados (los de
partida de las escenas de ventanas y seis discos de prueba), la tarjeta de prueba con reloj (lo que de verdad sonó), el **Soloist falso**
del núcleo (`spotify-xp/SoloistFalso`, que tarda `LENTO` s en empezar cada canción) y la **API falsa** del catálogo (`catalogo/ApiFalsa`)
haciendo de API web de Spotify, de servidor de sus portadas y de Cover Art Archive, con latencia a propósito:

| | de fábrica en la medida |
|---|---|
| cada petición a la API y cada portada de Spotify (`LAT`) | 120 ms |
| abrir una conexión nueva (TCP y TLS: `CONEX`) | 150 ms |
| cada portada del Cover Art Archive (sus dos redirecciones: `CAA`) | 400 ms |
| lo que tarda Soloist en empezar una canción (`LENTO`) | 0,6 s |

La API falsa apunta cuándo llegó y cuándo contestó cada petición (`tiempos.txt`) y la rocola, con `ROCOLA_TRAZA_VENTANAS`, cuándo llegó
cada tecla y cada encargo a la pantalla (el de XP que pone en pantalla las portadas decodificadas, `Form1.Portadas.cs:653`, es el «ya se
ve»). Escenas: `busca` (dos búsquedas con Spotify), `disco` (abrir un disco de Spotify del buscador tras quedarse 3 s en su tarjeta),
`artista` (la pared de un artista que solo está en Spotify y su 2.a página), `pared` (la letra A con el catálogo: 5 páginas hacia
adelante, una cada 4 s, y 2 de vuelta), `suena` (dos de Spotify seguidas), `mezcla` (una de XP y detrás una de Spotify) y `arranque`.
`compara.sh` alterna los dos programas (base y nuevo) con las mismas escenas, N vueltas; `resume.py` da mediana [mín-máx]. La base es
`junta-2` (`c90945e`) compilada igual (NativeAOT), con un solo cambio de prueba: la hora en que el doble del catálogo atendió cada pedido
(`RocolaOs.SpotifyPrueba.cs`, `t=`), para medir del pedido al sonido.

La máquina tiene 4 núcleos y la compartían otros agentes compilando: la carga (1 min) va con cada tabla. Los números son de aquí, **no del
A4** (2 núcleos más lentos, la red del bar): sirven para comparar antes y después, no para prometer tiempos allá.


## 2. Los cuellos de botella y la tabla antes/después

Base = `junta-2` (`c90945e`) con la hora del doble del catálogo; fin = esta rama (el último programa medido, `fin3`, es `4492c4a`; lo que
viene después es solo de las medidas). Mediana [mín-máx] y cuántas vueltas (n); en ms salvo donde se dice. Medidas alternando los dos
programas con `compara.sh` el 20-11-2026 (`medidas-214939.txt` y `medidas-221405.txt` en `/root/rocola-trabajo/fase11/rapido-spotify`),
carga de la máquina (1 min) **entre 5 y 15** con otros agentes compilando: los dos programas sufrieron la misma.

| escena: lo medido | base | fin | qué lo frenaba |
|---|---|---|---|
| arranque: Soloist escucha → la rocola conectada | 1008 [980-1016] n3 | 240 [184-296] n3 | `ws.port` se miraba cada 1 s |
| arranque: la 1.a página de la A con portadas, desde que arranca | 4704 [4561-4909] n3 | 3725 [3585-3849] n3 | 4 hilos para el Cover Art Archive (5 vueltas de red por página) |
| busca: 1.a búsqueda, resultados (desde la última letra) | 777 [766-809] n3 | 493 [467-554] n3 | la llave y la conexión se pedían con la búsqueda (DNS, TCP, TLS) |
| busca: 1.a búsqueda, su última portada en pantalla | 1371 [1328-1427] n3 | 898 [863-956] n3 | lo mismo, y las portadas de una en una |
| disco: abrir un disco de Spotify del buscador (PONER → se ve) | 178 n1 (abrir_disco 162 [149-447] n3) | 1 n1 | la petición de sus canciones salía al pulsar PONER |
| artista: su pared a la vista (PONER → se ve) | 297 n1 | 35 [30-40] n2 | sus dos bloques de discos se pedían al pulsar |
| artista: la última portada de su 1.a página en pantalla | 2795 [2496-2819] n3 | 620 [611-1532] n3 | 17 portadas de una en una (120 ms cada una) |
| artista: la última portada de su 2.a página en pantalla | 2789 [2769-3564] n3 | 820 [791-1451] n3 | lo mismo |
| pared (A con catálogo): cada página, su última portada en pantalla (mediana de 5 páginas) | 2119 [2061-2207] n6 | 213 [183-322] n6 (`fin`); 309 [285-334] n2 (`fin3`) | 4 a la vez y nada por adelantado: cada página esperaba sus 15-17 portadas (400 ms cada una) |
| pared: portadas pedidas al Cover Art Archive en la escena | 99 | 159 | (lo adelantado: las páginas de al lado; dentro de la cota de la batería) |
| pared: cuadros p50/p95/p99/máx | 32/53/66/108 n6 | 34/55/72/110 n6 (`fin`); 41/66/85/147 n2 (`fin3`, carga 7-8) | (sin cambio claro: la carga manda) |
| suena: hueco entre dos de Spotify seguidas (silencio oído) | 1895 [1821-1898] n3 | 1378 [1360-1401] n3 | el «play» de la siguiente salía cuando XP abría su fuente (~1,2 s después) más lo que tarda Soloist |
| suena: de pedirla a que suena | — (la base no lo apunta) | 1838 [1797-1935] n3 | (la latencia del Soloist falso, 0,6 s, más la tarjeta) |
| mezcla: hueco de una de XP a una de Spotify | 2191 [2092-2219] n3 | 1627 [1550-1680] n3 | lo mismo, desde una de XP |
| suena/mezcla: lo oído de cada canción | 8000 y 6000 ms enteras | 8000 y 6000 ms enteras | (nada se pierde: el play adelantado no se come el principio) |

Lo que más se nota: las portadas del catálogo al hojear (de ~2,1 s a ~0,2-0,3 s por página), la pared de un artista de Spotify (de ~2,8 s a
~0,6-0,8 s), abrir un disco o una pared desde la tarjeta que se estaba mirando (de 0,2-0,3 s a casi 0), el hueco entre canciones de Spotify
(medio segundo menos) y la primera búsqueda (0,3-0,5 s menos).

## 3. Lo hecho (de lo que más se nota a lo que menos)

Todo en `RocolaOs/` (nada en `Codigo/` ni en `Nativo/`, ningún gancho nuevo).

1. **Portadas del catálogo (Cover Art Archive) 12 a la vez y por adelantado** (`6a49a70`, `4492c4a`; `RocolaOs.ParedGeneros.cs`,
   `RocolaOs.ParedGenerosCatalogo.cs`). Primero la página que se mira; cuando ya no le queda nada, las de las páginas de al lado, la que
   sigue hacia donde se va y la 1.a de las letras de al lado (las de REGRESAR+izq/der), solo al disco (XP las lee como cualquier portada que
   ya estaba). 12 hilos y 12 conexiones por servidor (antes 4), 20 s como mucho por portada. Al hojear deprisa nada se asienta y nada se pide
   (como antes), y **el adelanto espera a que la página esté quieta** (6 cuadros pintados y 600 ms, por la firma de cada cuadro de g93): un
   tirón de la pantalla ya no cuenta como «se quedó en la página» (la batería `pared-generos hojea` pasó de 64-148 portadas pedidas mientras
   se hojea a 15, como la base: 16). No es la API de Spotify: no gasta cuota.
2. **Portadas de Spotify 6 a la vez y lo último pedido primero** (`8eb392f`; `RocolaOs.SpotifyCatalogo.cs`): buscador, paredes del
   buscador y letra ♫. Antes una a la vez y en el orden en que se pidieron (lo de una búsqueda vieja delante de lo que se mira). Del servidor
   de imágenes, no de la API: no gastan cuota. Mismo archivo en `/run`, mismo resultado.
3. **Adelantar la tarjeta mirada en el buscador** (`8eb392f`, `b3ffba8`): la tarjeta de Spotify en que se queda el buscador dos latidos
   (1-2 s), como g52 en la pared: un disco pide ya sus canciones (1 petición, la misma que haría PONER), un artista, «ARTISTA · más en
   Spotify» o «Todos los discos de…» la 1.a página de su pared (1 o 2 peticiones, las mismas). Cuota: solo lo que alguien mira, una tarjeta
   cada 3 s como mucho, cada una una vez, nada con 429 ni sin la llave; lo contestado queda en la memoria de la sesión (abrirlo: 0).
4. **El «play» adelantado** (`2a740cb`, `b3ffba8`; `RocolaOs.Spotify.cs`, `RocolaOs.SpotifyFuente.cs`, `RocolaOs.SpotifyPide.cs`): entre
   dos de Spotify, en cuanto la de antes acaba bien (Remata), si la siguiente de la cola de XP (`queue[0]`, publicada por la pantalla en el
   latido y al pedir) es de Spotify, su «play» se manda ya en vez de la pausa; de una de XP a una de Spotify, cuando a la de XP le quedan
   2,5 s por leer. Sin lector Soloist no puede escribir en el anillo: nada se oye hasta que XP abre su fuente, que la toma
   (`TomaAdelantada`) sin tirar lo que ya llegó. Si XP abre otra, se deja; si nadie la toma en 30 s, se deja y se pausa. El crédito y lo que
   se oye no cambian (las canciones se oyen enteras en `suena` y `mezcla`; batería `spotify-xp`, escenas `adelanto` y `adelantoxp`).
5. **Calentar al abrir el buscador** (`8eb392f`, `b3ffba8`; `RocolaOs.SpotifyCatalogoApi.cs`): la llave de la app si falta o vence en menos
   de 5 min, y un HEAD sin llave a la raíz de la API y del servidor de portadas (abre DNS, TCP y TLS mientras se escribe). Como mucho cada
   20 s; nada con 429 (lee antes el plazo de un 429 guardado en `/run`), sin llave o con la llave rechazada. El HEAD no es de la app: no cuenta
   en la cuota.
6. **Conectar antes con Soloist** (`2a740cb`): sin `ws.port`, se mira cada 250 ms (antes 1 s). Un archivo chico, solo mientras no hay
   conexión.
7. **Despertar al hilo de Spotify al pedir** (`2a740cb`): la fuente lo despierta en vez de esperar su vuelta de 50 ms (25 ms de media).

Y para medir: `Pruebas/RocolaOs/rapido-spotify/mide.sh` y `mide.py` (`ef7d580`, `cc6a598`, `886c5ff`), la API falsa con latencia de
portadas, de llaves y de conexiones nuevas y que contesta HEAD, la hora de cada pedido del doble del catálogo (`RocolaOs.SpotifyPrueba.cs`,
solo con `spotify-prueba`), y las escenas `adelanto` y `adelantoxp` de la batería `spotify-xp` (`2890948`, `130448e`).

## 4. Lo que se probó y no ganó (o se corrigió)

- El adelanto de portadas del catálogo en cuanto la página llevaba 300 ms sin cambiar (la 1.a forma de `6a49a70`): con la máquina
  cargada un tirón de la pantalla parecía una página quieta y se pedían 4 páginas más mientras se hojeaba (64 y 148 portadas en la batería
  `hojea`, frente a 16 de la base). Se cambió a «quieta» por cuadros pintados (`4492c4a`). Con eso la 1.a página de la escena `pared`
  midió 963 ms (n2) frente a 231 (n6) del `fin` anterior: la página 1 se adelantaba antes por ser la «de al lado» del arranque; ahora
  espera a que la pantalla de partida esté quieta. Las demás páginas, igual (141-503 ms). Se queda así: no pedir de más al hojear importa más.
- Con la llave pedida antes (calentar), la primera búsqueda sale con 3 letras («inv») en vez de esperar a la llave y salir con la palabra
  entera: es la regla de XP de buscar a los 300 ms sin teclear, ahora sin la espera. Es una búsqueda más en la batería `catalogo fallos`
  (que sigue verde); con la llave ya pedida en la base pasaría igual.

## 5. Propuestas (cambian el aspecto, gastan cuota o necesitan al dueño)

- **Guardar en el disco las portadas del catálogo** con tope (como la demo de Windows: 600 MB, las menos usadas fuera) para que al
  prender no se vuelvan a bajar: hoy ya van al disco de la rocola por XP; falta el tope y la limpieza. Sin probar.
- **Adelanto hondo de la pared en reposo** (las páginas 3 a 10 de cada letra con la rocola quieta, como la demo): miles de portadas del
  Cover Art Archive (~100 MB una vez); no gasta la cuota de Spotify pero sí red y disco del bar. Necesita que el dueño diga cuánto.
- **La dirección directa de archive.org en el catálogo** (saltarse las dos redirecciones de coverartarchive.org): pide rearmar el catálogo.
- **Buscador local del catálogo al momento** (sin gastar cuota, como la demo): cambia lo que se ve mientras se escribe (aparecerían
  resultados del catálogo antes que los de Spotify): es del dueño.
- **Abrir un disco en UNA petición** (`/v1/albums/ID` con sus primeras 50 canciones) ya lo hace el núcleo; no hizo falta tocarlo.
- **HTTP/2** con `HttpClient` (`DefaultRequestVersion` 2.0 con `RequestVersionOrLower`): el gzip ya va (`AutomaticDecompression` del
  núcleo) y el grupo de conexiones ya las reutiliza (keep-alive, 5 min); HTTP/2 juntaría las 6 portadas de Spotify en una conexión TLS, pero
  no se pudo medir aquí (la API falsa es HTTP/1.1 sin TLS) y en Linux depende de la ALPN de OpenSSL del sistema: sin hacer.

## 6. Las comprobaciones (21-11-2026, programa `fin3` = `4492c4a`; después solo cambian las medidas y documentos)

- **Compilación**: JIT y NativeAOT sin errores; 13 avisos, todos en `avisos-base.txt` (ninguno nuevo); 0 avisos IL/AOT.
- **`ganchos.py`**: TODO BIEN (ningún gancho nuevo: g235-g249 sin usar). **`diff-xp.py --sin-ganchos`**: 0 líneas quitadas, 0 puestas.
- **Baterías en NativeAOT** con la máquina muy cargada (carga 11 a 19, otros agentes compilando):
  - `pared-generos`: **69 BIEN, 0 MAL** (antes de `4492c4a`, 2 MAL: `hojea` y `portadas`).
  - `spotify-xp`: 6 MAL de 179; `catalogo`: 3 MAL de 156; `sustitucion`: 1 MAL de 97. Todas las MAL son de tiempos o de sonido bajo
    carga. Para saber si eran de esta rama, esas escenas se repitieron **alternando la base y esta rama, dos vueltas** (`reprueba.sh`, carga
    4 a 16):

    | escenas | base vuelta 1 | base vuelta 2 | rapido-spotify vuelta 1 | rapido-spotify vuelta 2 |
    |---|---|---|---|---|
    | spotify-xp: apagado fabrica celular apagon vigilante noche (80) | 4 MAL | 7 MAL | 1 MAL | 1 MAL |
    | catalogo: cero mas sinllave (14) | 1 MAL | 2 MAL | 1 MAL | 1 MAL |
    | sustitucion: titulo (6) | TODO BIEN | TODO BIEN | TODO BIEN | TODO BIEN |

    Las que quedan, en la base también: `fabrica` (rocola_error.txt con un «Pantalla parada» de más en uno de los dos lados por la
    carga: de la base salió 12 frente a 15 una vez), `probar sp/xp` (el latido de 1 s de la prueba: tolerancia, `FASE11_JUNTAS.md`),
    `cero` (la traza frente a sin ROCOLA_OS, holgura 80 ms, con la máquina cargada), `mas` (la tecla de la 3.a casilla llegó antes que la
    pared: la base también falló). Ninguna es nueva de esta rama.
  - Sueltas: `catalogo fallos` (401, 503 y 429: ninguna petición más tras el 429) TODO BIEN en las dos; `sustitucion titulo` TODO BIEN en
    las dos cuando la máquina estaba tranquila; `spotify-xp` con `dos adelanto adelantoxp` TODO BIEN (30) en esta rama, la base 2 MAL
    (`adelantoxp`: su hueco de 2,46 s no baja de 1,9 s, que es lo que la escena pide).

## 7. Lo no probado

- Nada en el A4, ni con Soloist de verdad, ni con la API de Spotify ni con el Cover Art Archive de verdad (solo los falsos, con la latencia
  puesta a mano). En el bar la latencia de la red y los 2 núcleos lentos cambian los números; el orden de las ganancias debería ser el mismo.
- El «play» adelantado con el Soloist de verdad: si Soloist escribe al anillo por adelantado de otra forma que el falso (o si al recibir
  «play» mientras otra cosa termina de sonar se comporta distinto), hay que mirarlo en el A4 (la bitácora de Spotify dice «el play
  adelantado de … no se uso» si no sirvió).
- HTTP/2 y TLS de verdad (la API falsa es HTTP/1.1 en 127.0.0.1): el HEAD de calentar abre la conexión TLS de verdad en el bar, pero no se
  midió aquí.
- Una medida final completa de las siete escenas con el último programa (`fin3`) no se hizo con la máquina tranquila: la tabla del §2 junta
  las vueltas de `fin` (el programa de antes de `4492c4a`, igual en todo salvo el adelanto de la pared) con dos de `fin3` en `pared`.
