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

## 2. Los cuellos de botella (antes, base `c90945e`)

PENDIENTE: la tabla final.

## 3. Lo hecho (de lo que más se nota a lo que menos)

PENDIENTE.

## 4. Lo que se probó y no ganó (y se quitó)

PENDIENTE.

## 5. Propuestas (cambian el aspecto, gastan cuota o necesitan al dueño)

PENDIENTE.

## 6. Las comprobaciones

PENDIENTE.

## 7. Lo no probado

PENDIENTE.
