# Fase 11, Spotify: la sustitución invisible (función «calidad_spotify»)

15-11-2026. Rama `sustitucion-spotify`, sobre `junta-spotify` (`07f3ae0`). Petición del dueño del 15-11-2026: «Que no se te olvide agregar
la función de que si una canción de mi rocola existe en mejor calidad en Spotify, que tome esa versión». Es **SP2** del programa anterior
(decisiones del 6 y 7-11-2026: SP2 con **A, B y C**, SP3, SP4 y el volumen **V1-V3**; `sistema/rocola/Motor/SustitucionSpotify.cs`,
`SpotifyMas.cs` y `VolumenSpotify.cs`, leídos como idea y reglas probadas) traído a **rocola-xp** por ganchos, con las reglas de la cuota de
la API en modo desarrollo (2026, `FASE11_catalogo.md`): **nada de recorrer la biblioteca de fondo**. **Nada de esto se probó con el Soloist
de verdad, con la cuenta del dueño ni en el A4**: todo con el Soloist falso y la API falsa (abajo).

## 1. Qué hace

Una canción **tuya** que **suena peor** que su versión de Spotify (Ogg Vorbis a 320 kbps) y que **tiene pareja** en Spotify suena desde
Spotify **sin ningún cambio visible**: la cola, lo que suena (título, artista, disco, portada), el crédito, el turno, lo más sonado, el
artista, TOP y `rocola_stats.txt` son los de tu canción, porque el `PlayItem` de XP no cambia: solo cambia de dónde sale el sonido. Si
Spotify no puede, suena tu MP3 tal cual, sin aviso y sin cobrar de más.

| Archivo (en `RocolaOs/`) | Qué lleva |
|---|---|
| `RocolaOs.Calidad.cs` | La función «calidad_spotify» (encendida de fábrica), el latido que mira la cola, el hilo propio (A, la pareja, V1, C), la tabla `rocola_spotify_pares.txt`, el estado en `/run`, los ganchos g60-g62, la apertura (desde g13), volver al MP3 y la forma del MP3 |
| `RocolaOs.CalidadFuente.cs` | La fuente: envuelve la de Spotify del núcleo (sin devolver créditos, los 6 s, lo que dura, V1 y C al leer) |
| `RocolaOs.CalidadReglas.cs` | Las reglas A, B y C sin estado (la cabecera del MP3, el ISRC, títulos, marcas de versión, artistas, la correlación) y la orden `rocola-xp --prueba-calidad` |
| `RocolaOs.CalidadApi.cs` | La búsqueda de la pareja en el cliente del catálogo (su hilo, su cola de una petición, su llave y su freno de 429) |
| `RocolaOs.CalidadPrueba.cs` | El doble de las pruebas («calidad-prueba», `SoloEnLaLista`: con `ROCOLA_OS=1` nunca) |

Y en lo de otros grupos, lo mínimo: `RocolaOs.SpotifyFuente.cs` (núcleo) gana `SinDevolver`, `CuadrosSonido`, `Terminada` y `FinMotivo`;
`RocolaOs.SpotifyPide.cs` (núcleo) llama a la sustitución al principio de `RocolaOsSpotifyAbre` (lo de g13); `RocolaOs.SpotifyCatalogoApi.cs`
(catálogo) pasa a `partial` y atiende el pedido «pareja». En `Pruebas/`: el Soloist falso entiende ids con envolvente (`fFFFdSSSm1…`) y la
API falsa sabe buscar `isrc:` y `track:"…" artist:"…"` (canciones aparte, que no salen en las búsquedas de las escenas del catálogo).

## 2. Las reglas

**A, cuándo suena peor** (`SuenaPeor`), con lo que XP **ya mide** de cada canción —el corte de agudos del repaso (`rocola_eq.txt`, `EqCut`)
y el pre-eco (`rocola_sonoridad.txt`, algoritmo 7, `PreEcoDe`)— y lo que dice la cabecera del MP3 (CBR o VBR por Xing/Info/VBRI, la tasa, la
frecuencia, la duración):
- el corte medido bajo 16 kHz;
- menos de 128 kbps en CBR;
- grabada a menos de 44,1 kHz.

(Decisión del dueño del 20-11-2026, al saberse que Soloist suena en «Alta», Ogg Vorbis 160 kbps, sin poder subirla: «Spotify solo se
tomará si la versión en la rocola es peor». Se quitaron «128 kbps con el corte bajo 17,5 kHz» y «pre-eco con 192 kbps o menos y el corte
bajo 19 kHz».)

En VBR solo cuentan el corte y la frecuencia (revisión «invisible-y-reglas», I1); en lo que no es MP3, solo el corte. **Sin medida no se sustituye**: una regla que pide el corte y no lo hay no cuenta (un
MP3 de 128 kbps aún sin repasar no se sustituye; uno de 96 kbps CBR sí, porque la cabecera ya lo dice).

**B, la pareja, estricta** (`Elige`): con el ISRC del MP3 (marco TSRC) se busca `isrc:XXX` y vale la que trae **ese** ISRC (`external_ids`) y dura lo mismo a 3 s (I2 de la revisión «invisible-y-reglas»);
si no trae ISRC o no salió nada, `track:"TÍTULO" artist:"ARTISTA"` (límite 10) y la **regla 2**: título base igual (sin lo de entre paréntesis
ni la cola « - …» de versión, remasterización o año), las **mismas marcas de versión** (en vivo, remix, acústica, karaoke, instrumental, demo,
cover, a capella, regrabada, acelerada, lenta, sinfónica, «versión banda/norteña/…»; remasterizada o editada para radio son la misma), el
**artista principal igual palabra por palabra** (sin acentos ni mayúsculas con `UnicodeXp.FormaD`: el programa corre con InvariantGlobalization)
y la **duración a 2 s**; a igualdad, el disco que se llama como el tuyo y no un recopilatorio.

**C, la forma**: la primera vez que una pareja suena desde Spotify, un hilo lee tu MP3 entero (el lector del repaso, nice 19; nunca en la
pantalla ni en el hilo del sonido) y su volumen por tramos de 0,5 s se compara con el de lo que sonó (correlación de los dB, corrimiento de
hasta 3 tramos, al menos 40 tramos): **r ≥ 0,80** confirmada; **r < 0,55** otra grabación (se apunta y ya no se sustituye); en medio, o con tu
MP3 casi plano, se vuelve a mirar la próxima vez. XP no guarda nada parecido a una «forma»: se mide en ese momento, en memoria.

**El volumen (SP3, V1, V2)**: lo de Spotify se mide y se nivela al límite como lo tuyo, **bajo su ruta de Spotify** (en la tabla de parejas,
no en las de XP). V1: lo que llega de Spotify (antes de la cadena) pasa por el medidor del repaso (`MedidorBs1770`) y, si sonó casi entera
(97 % y 30 s o más), su pico, P99 y P90 se guardan y desde la próxima vez se usa `GananciaAlLimite` de XP con la fuerza del nivelado. V2: la
primera vez, la mediana de las de Spotify ya medidas si hay 5 o más; si no, 1 (como una de XP sin medir). Siempre con el margen del tono del
local y, en el billar, el volumen parejo (como `GananciaDe`). **Nunca a media canción**: la medida se fija al abrirla. **V3 no**: Soloist es el
cliente oficial y no da la ganancia ni el pico de cada canción (lo daba el librespot del programa anterior).

**Lo que se le aplica a la versión de Spotify** (la cadena entera de XP se arma con una copia de tu canción con la ruta de Spotify, g61): el
nivelado de arriba, el local, el billar, el limitador y la salida, como a una de Spotify pedida (R7). **No** se le aplica nada que XP aprendió
de tu MP3 (su EQ automático, su zumbido, su pre-eco, su des-recorte, el realce de dañada): eso describe a tu archivo, no a la versión de Spotify.
Y de ella XP no aprende nada en sus tablas (g23 y g24 de siempre: la ruta es «spotify:»).

## 3. Cómo va, paso a paso

1. **Al entrar a la cola** (pagada): el latido (1 s, en la pantalla, sin disco) manda al hilo «calidad_spotify» las canciones tuyas de la
   cola y la que suena que aún no miró en la sesión, con el corte y el pre-eco de las tablas de XP. El hilo lee la cabecera y decide A.
2. Si suena peor y la tabla no sabe su pareja (o dice «sin pareja» de hace más de 30 días): **una** petición, dos si la del ISRC no dio nada,
   por la cola del catálogo **con prioridad baja** (detrás de buscar y abrir; con 429 no se pregunta). Sin sesión de Soloist, sin la llave o
   frenando, no se pregunta. Lo encontrado —o «sin pareja», con su fecha— se guarda **de un tirón** con `EscribeSeguro` de XP en
   `/rocola/datos/xp/rocola_spotify_pares.txt`: la ruta del MP3, su tamaño y su fecha, el id de Spotify, el ISRC y números (duraciones, la forma,
   la fecha de la búsqueda, pico, P99, P90, r) y «isrc»/«titulo». Nada más (la prueba lo comprueba). Si XP ya tenía la canción abierta por
   delante como MP3 (el calentador, al entrar a la cola), se suelta.
3. **Al sonar**: `PlayNext` → `PlayTrack` → `OpenAudio` → **g13** → `RocolaOsSpotifyAbre` → `RocolaOsCalidadAbre` (en el hilo que abre): con
   pareja, el archivo con el mismo tamaño y fecha, la forma no descartada, menos de 2 fallos en la sesión y Soloist con sesión, la fuente de
   la sustitución. La de Ctrl+F3 que mide (síncrona, en la pantalla) y una retomada (volvió el aparato de sonido: hay que saltar a media
   canción) van por tu MP3.
4. **Si Spotify no empieza a sonar en 6 s**, o acaba por un fallo (se cortó, Soloist se cayó, otra cosa a media canción): la fuente entrega
   silencio y encarga a la pantalla **el camino de XP que retoma una canción** (`ReintentoSalida_Tick`): `DisposePlaybackNow` y `PlayTrack` de
   la MISMA canción, ahora como MP3. Desde el principio si sonó menos de 10 s; si sonó más, **desde donde iba** (`reanudarEn`, la retomada de
   XP). Ni la cola, ni los créditos, ni lo más sonado cambian; el crédito de Spotify nunca se devuelve (`SinDevolver`: es el de tu canción).
   Dos fallos de la misma en la sesión y esa ya no se sustituye en la sesión.
5. **Al acabar**: lo medido (V1) y la forma (C) van al hilo, que los guarda en la tabla.

## 4. Los ganchos

Los tres, una línea cambiada en su sitio con su marca y su final de línea, en `RocolaOs/GANCHOS.tsv`; `ganchos.py`: TODO BIEN (27 ganchos en
12 archivos de `Codigo/` y los de fuera; `Codigo/` vuelve byte a byte a fc7b667). Ids desde g60 (el grupo de las paredes del buscador usa los
de después de g52).

| Id | Dónde | Antes → ahora | Sin el modo |
|---|---|---|---|
| g60 | `Codigo/AperturaAudio.cs:238` (`RutaQueSonaria`, que XP dejó para «el día que esta rocola tenga otra fuente») | `{ return path; }` → `{ return RocolaOsCalidadRuta(path); }`: la que se va a sustituir se ve por la ruta de Spotify; el calentador y el relevo sin pausa no la abren por delante como MP3 y entra por `PlayNext` | `path` |
| g61 | `Codigo/Form1.Sonido.cs:1288` (`ArmaCadena`) | `CadenaCancion c = new CadenaCancion();` → `… t = RocolaOsCalidadCancion(t, source);`: con la fuente de la sustitución, la cadena se arma con una copia de tu canción con la ruta de Spotify | la misma `t` |
| g62 | `Codigo/NivelDisco.cs:46` (`GananciaDe`) | `if (t == null) return 1f;` → `… { float rocolaOsG; if (RocolaOsCalidadGanancia(t, out rocolaOsG)) return rocolaOsG; }`: el nivelado de la versión de Spotify (también cuando Ctrl+F3 vuelve a nivelar en vivo) | `false`: lo de XP |

**No hizo falta gancho** para abrir (lo hace g13, del núcleo: `RocolaOsSpotifyAbre` pregunta primero a la sustitución), ni para cobrar, ni
para lo más sonado (`RecordPlay` cuenta al pedir, con el `PlayItem` de tu MP3: g20 solo calla las rutas «spotify:»), ni para volver al MP3
(un encargo con el camino de XP).

## 5. Peticiones por acción (contadas por la API falsa)

| Acción | Peticiones a la API |
|---|---|
| Pedir una canción que no suena peor | **0** |
| Pedir una que suena peor, con ISRC y pareja por ISRC | **1** (más la llave de la app una vez por hora, la del catálogo) |
| … sin ISRC: título y artista | **1** |
| … con ISRC que no da nada | **2** (ISRC y título) |
| Pedirla otra vez (en la sesión o en la siguiente), con pareja o «sin pareja» de menos de 30 días | **0** |
| Sonar desde Spotify, volver al MP3, medir, la forma | **0** (es Soloist y el disco) |
| Con 429 (o frenando de antes), sin llave o sin sesión de Soloist | **0** |

Nunca se recorre la biblioteca: solo lo que alguien paga.

## 6. Las comprobaciones (con números)

Todo con el **Soloist falso** del núcleo (nunca el de verdad), la **API falsa** del catálogo y la tarjeta de prueba con reloj
(`Pruebas/Windows/alsa/reloj.c`: lo que de verdad sonó, analizado con `frecuencias.py`). La biblioteca es inventada: MP3 hechos con ffmpeg,
cada uno con su tono (180-390 Hz) y la envolvente de volumen 1; su versión de Spotify con otro tono (440-770 Hz) y la envolvente 1 (la misma)
o la 2 (otra forma): así se sabe de dónde salió lo que se oyó. `Pruebas/RocolaOs/sustitucion/prueba.sh`, rocola-xp de verdad fuera de pantalla:

| Escena | Qué comprueba |
|---|---|
| `reglas` | `--prueba-calidad`: 34 reglas de A, B y C con casos inventados, y la cabecera de los MP3 hechos (96 kbps CBR con su ISRC: peor; 320 kbps: no) |
| `isrc` | Una mala (96 kbps) con ISRC: **1** petición (`isrc:`), la pareja por ISRC, suena desde Spotify (440 Hz, 32 s; **0,00 s** del MP3), lo que suena es tu canción campo por campo, créditos 4→3→2 como XP, la primera vez con la **mediana de 5** (V2), medida al límite (V1) y la forma **misma** (r 1,00, 64 tramos), la segunda vez con **su medida** y **0** peticiones; la tabla solo ids y números; `rocola_stats.txt` cuenta tu MP3; ni COBRADA ni DEVUELTA; ninguna tabla de XP con «spotify:» |
| `titulo` | Sin ISRC: **1** petición (`track:"Vals Sin Codigo" artist:"Trio Calidad"`), entre el recopilatorio, la en vivo y la de otro artista elige la de su disco (con acento en Spotify); se oye solo esa; la buena (320 kbps) suena como siempre y **ni se busca** |
| `sinpareja` | ISRC que no da nada: **2** peticiones y «sin pareja»; suena tu MP3 las dos veces; la sesión siguiente **0** peticiones |
| `nx` | Spotify no empieza: a los **6 s** tu MP3 **entero** (32 s), desde el principio, un solo crédito por canción, ni DEVUELTA ni aviso |
| `corte` | Spotify se corta tras ~15 s: tu MP3 **desde donde iba** (lo que falta de los 32 s), sin devolver ni cobrar |
| `forma` | La de Spotify con otra forma: **r < 0,55**, la tabla la marca −1 y la segunda vez suena tu MP3 con **0** peticiones más |
| `frena` | 429 en la búsqueda: **1** petición (la del 429), nada se apunta, la siguiente mala ni se pregunta, suena tu MP3; y lo que suena con tu MP3 es lo mismo, campo por campo, que con su versión de Spotify (`isrc`) |
| `apagado` | Sin `ROCOLA_OS` y con `ROCOLA_OS=1` y `calidad_spotify=0`, con la pareja ya en la tabla: suena tu MP3, **0** peticiones, nada de la función y la tabla sin tocar |
| `sinllave` | Sin `rocola_spotify_api.txt`: **0** peticiones (ni la llave), tu MP3 |
| `sinsesion` | Soloist no arranca (`ROCOLA_SOLOIST_BIN=/bin/true`): aun con la pareja en la tabla, tu MP3 **al momento** (sin esperar los 6 s), **0** peticiones |

Resultados (programa de esta rama):

| Qué | Resultado |
|---|---|
| `dotnet build -c Release --no-incremental` | 0 errores, **13 avisos**: los de `avisos-base.txt` (ninguno en `RocolaOs/`) |
| `dotnet publish -p:PublishAot=true -r linux-x64` | salida 0, **0 avisos IL/AOT** |
| `sustitucion/prueba.sh`, JIT (11 escenas) | **70 de 70** tras arreglar tres comprobaciones de la prueba misma (no del programa): la 1.ª corrida 68 de 70 (`titulo` contaba 1 buena y el relleno también lo es; `nx` pedía 6,0 s y salió 5,97); la 2.ª 69 de 70 (`forma`: la 2.ª petición llegó cuando la 1.ª aún sonaba, «ya va seguida»); `forma` otra vez, 5 de 5 |
| `sustitucion/prueba.sh`, NativeAOT (11 escenas) | **70 de 70** |
| `ganchos.py` | TODO BIEN (27 ganchos; `Codigo/` vuelve byte a byte a fc7b667) |
| `diff-xp.py --sin-ganchos` (109 archivos) | 5 095 quitadas, 4 828 puestas, 229 sin marca: las de la base (27 ganchos devueltos en 12 archivos) |
| `andamio/prueba.sh` | JIT **101 de 101** |
| `spotify-xp/prueba.sh`, JIT | JIT **122 de 124**: `celular` (no_pedidas=1, el falso a destiempo) salió BIEN al repetirla; `fabrica` («rocola_error.txt igual de largo», 12 y 15 renglones: avisos de «Pantalla parada» con la máquina cargada) falla igual con el programa de la base `junta-spotify` 07f3ae0 (9 y 12), así que no es de este grupo |
| `catalogo/prueba.sh`, JIT | JIT **73 de 74**: `cero` (la cuenta de un encargo de XP, `MedidasBitacora.cs:129`, 488 frente a 364, ruido de la carga) salió BIEN al repetir `apagado cero` (9 de 9) |

## 7. Decisiones (lo que no estaba dicho)

- **Cortada a media canción, desde donde iba**: el encargo pedía «el MP3 tal cual, desde el principio» también si se corta. Desde el principio
  queda para lo que sonó menos de 10 s (el mismo corte que R4 usa para devolver); con más, tu MP3 sigue desde donde iba con la retomada de XP
  (la de cuando vuelve el aparato de sonido): repetir tres minutos ya oídos en el bar se notaría más que el salto. Se cambia en una línea
  (`RocolaOsCalidadVuelve`) si el dueño lo prefiere de la otra forma.
- **El relevo sin pausa**: una que se sustituye entra por `PlayNext` (como una de Spotify): entre la anterior y ella, la salida se cierra y
  se abre (~1 s más lo que tarde Soloist). Si la canción entra a la cola en los últimos 12 s de la que suena, el relevo pudo prepararse ya con
  el MP3 antes de saberse la pareja: esa vez suena tu MP3 (la próxima, desde Spotify).
- **La primera vez que se pide**, si nada sonaba, empieza en seguida y la pareja aún no se sabe: esa vez suena tu MP3; desde la siguiente,
  la de Spotify. Así se cumple «solo cuando entra a la cola» sin buscar nada por adelantado.
- **Lo que dura** mientras espera a Spotify es lo de tu MP3 (la barra de lo que suena no salta ni el relevo se prepara antes de tiempo); ya
  sonando, lo de Spotify más lo que esperó (como una de Spotify).
- **El crédito**: si Spotify falla no se devuelve nada (suena tu MP3, que es lo que se pagó).
- **Dos fallos en la sesión** (no empezó o se cortó): esa canción ya no se sustituye hasta reiniciar (que no espere 6 s en cada vuelta).

## 8. Lo que queda (no arreglado)

- **Ctrl+F3 que vuelve a nivelar en vivo** con una sustituida sonando (`ApplyNormNow`): el nivelado es el bueno (g62 la reconoce), pero XP
  le pone en vivo el EQ automático y el realce de **tu MP3** (`EqCorrFor` y `ExciteAmountFor` con la ruta del MP3) hasta que acaba. Solo pasa
  si el dueño mueve el nivelado justo mientras suena una sustituida; arreglarlo pide otro gancho en esa línea.
- La bitácora del billar (`rocola_billar_bitacora.txt`) apunta la de Spotify con su id, no con el nombre de tu MP3 (como una de Spotify).
- La forma lee tu MP3 entero una vez por pareja (la primera vez que suena desde Spotify): en el A4 son ~1-2 s de procesador a nice 19.
- `AGENTS.md` aún no tiene la línea de esta petición del dueño: la pone la junta (otros grupos tocan el mismo archivo a la vez).

## 9. Para el A4 (solo con el Soloist de verdad y la cuenta del dueño)

1. Que el ISRC de los MP3 del dueño se lea (muchos no lo traen: entonces título y artista) y que la búsqueda `isrc:` de la API de verdad
   (con la app del dueño, `market=MX`) dé la canción con su `external_ids`.
2. Que una canción de 128 kbps (o menos) ya repasada suene desde Spotify **sin que nada cambie en pantalla**, y que se oiga mejor.
3. Que el nivel de la versión de Spotify se parezca al de las tuyas (la primera vez sin medir; después, con su medida): mirar la bitácora
   («nivelado … dB (su medida)»).
4. Que si se desconecta internet a media canción sustituida, siga tu MP3 desde donde iba (en menos de ~15 s: el plazo del núcleo para darla
   por cortada) y que la de después suene bien.
5. Que la forma de la de verdad salga «misma» con las canciones bien emparejadas (`r` en la bitácora) y «otra» con una versión distinta: si el
   real da muchos «no se sabe», ajustar los tramos.
6. Contar en `/run/rocola-xp/spotify/calidad` y en `/run/rocola-xp/spotify/catalogo` las peticiones tras una noche de uso (`peticiones=`).
7. Lo del relinking (FASE11_spotify-xp.md, A4 12): si Soloist toca otra uri, la de la sustitución no empieza y a los 6 s suena tu MP3.

## Revisión (sonido-cuota-y-fallos)

15-11-2026, revisor independiente (enfoque: el paso MP3 ↔ Spotify, la cuota, la tabla de parejas, los hilos y NativeAOT). Comprobado en el
código y corriendo las escenas, no en el informe. Lo que estaba mal y se arregló (solo `RocolaOs/RocolaOs.Calidad.cs`,
`RocolaOs.CalidadFuente.cs`, el Soloist falso y la prueba; **ningún gancho nuevo**, `Codigo/` sin tocar):

| # | Qué pasaba | Arreglo | Escena |
|---|---|---|---|
| R1 | **La cuota, en bucle**: cuando la búsqueda no llegaba (sin red, 5xx, 4xx, la llave mala) se quitaba la canción de `Vistas` y el latido la mandaba otra vez al segundo siguiente: con la API en 503, dos peticiones cada ~3 s **por cada mala de la cola mientras estuviera en la cola o sonando**; con un 403/400, para siempre. Sin sesión, sin llave o frenando (429): un renglón en la bitácora y la cabecera del MP3 leída **cada segundo** | `Vistas` guarda desde cuándo se puede volver a mirar: 1 min sin sesión de Soloist, 5 min sin red / 5xx / 429, 30 min con un 4xx, la llave mala o sin la llave. Un renglón por vez | `caida` (nueva), `frena` y `sinllave` (un renglón por canción) |
| R2 | **Una vuelta al MP3 «desde donde iba» dejaba la marca puesta** (`NoSustituir` solo se quitaba si no había retomada): g60 daba la ruta del MP3 y esa canción **ya no se sustituía en toda la sesión** (con un solo fallo, no dos) | La marca se quita antes de mirar `reanudarEn` | `atasca` (la segunda vez, otra vez desde Spotify) |
| R3 | **Spotify atascado en «buffering» a media canción**: el núcleo solo lo da por cortado con Soloist en «playing» (5 s) o sin WebSocket (10 s); en «buffering» (lo que hace al quedarse sin red) esperaba **30 s de silencio** (`SeguridadMs`) antes de volver a tu MP3 | La fuente de la sustitución cuenta lo entregado sin que llegue nada: **5 s** ya sonando → tu MP3 desde donde iba. Contado en cuadros entregados, no en el reloj. A 5 s o menos del final de la de Spotify se da por acabada (sigue la siguiente) | `atasca` (nueva: el Soloist falso gana el evento `atasca`) |
| R4 | **La apertura por delante**: `RocolaOsCalidadAbre` creaba la fuente de Spotify también cuando el calentador abría el MP3 de la **siguiente** (si la pareja llegaba entre g60 y esa apertura): el relevo sin pausa se la podía llevar y el hilo de la forma leía el MP3 entero para nada | Solo se sustituye la que le toca sonar (`nowPlaying` con esa ruta: PlayNext y la vuelta al MP3 lo ponen antes de abrir) | `seguida` |
| R5 | **El disco que sigue sin pausa**: XP encadena la pista siguiente del mismo disco en la misma lectura (`SiguienteDelDisco`: sin fundidos). Una sustituida entra por PlayNext (la salida se cierra y se abre, y Soloist tarda en empezar): en un disco en vivo o una mezcla, un **corte** que XP no tiene | Si la frontera es **continua** (la que suena acaba con música —`FinUtil` del algoritmo 6 a 1 s o menos del final— y la siguiente no empieza con silencio), g60 da la ruta del MP3 y suena tu MP3 por el relevo de XP. Entre pistas con su silencio (lo normal en estudio) o sin la medida, se sustituye como antes. **Decisión del revisor**: el dueño puede preferir la calidad también ahí (una línea: `RocolaOsCalidadEmpalma`) | `seguida` (nueva: 0,00 s de hueco; el vals con pareja suena de su MP3) |
| R6 | **La tabla crecía sin tope** (y se reescribe entera en cada cambio) | Al leerla se quitan los «sin pareja» de más de 30 días (se volverían a preguntar igual) y, pasadas 20 000, las más viejas («sin pareja» primero); si quitó algo, se guarda una vez | (no hay escena: se ve en la bitácora «renglones viejos fuera») |

Lo que se miró y **está bien** (sin cambios): la cola de una petición del catálogo con prioridad baja, 429 (no pregunta mientras frena),
401 (una llave nueva y otra vez, la de siempre del catálogo); `EscribeSeguro` (temporal, fsync, cambio de nombre y `.bak`) y la lectura con
`LeeLineasSeguro` (cae al `.bak`): un apagón no deja la tabla a medias; solo el hilo «calidad_spotify» la escribe. En la pantalla no hay ni red
ni disco: el latido, g60, g61, g62 y la vuelta al MP3 solo miran tablas en memoria (`eqCache`, `integCache`, `durCache`, la tabla de parejas) y
la bitácora no espera al disco; `FileInfo`, la cabecera, la red y la forma van en el hilo que abre, en el de la función y en el de la forma
(nice 19). `reader.Volume` es siempre 1 (`FixedVolume`), así que V1 mide lo que llega de Spotify sin el nivelado. El modo noche va en la cadena
(`LevelTap.Master`) y le toca igual a la sustituida y a la vuelta al MP3. La fuente no espera ni toma cerrojos al leer; lo único que reserva es en
lo que pasa una vez por canción (al acabar o al volver al MP3). Peticiones por canción pedida: 1 o 2 (y, con 5xx, cada una con su reintento: hasta 4).

**Lo que sigue abierto** (no arreglado): lo de §8; si una canción va dos veces seguidas en la cola y la primera vuelve al MP3, el calentador
de la segunda podría quitarle la marca a la primera (sonaría otra vez desde Spotify y, si falla, otra vez al MP3: un fallo más; caso raro);
si la siguiente empieza con silencio pero aún no estaba medida cuando el calentador la abrió, suena tu MP3 (como XP, con su hueco).

Pruebas de esta revisión (rocola-xp de esta rama con lo del otro revisor que estaba en la copia de trabajo; Soloist falso, API falsa, tarjeta
con reloj; nunca el Soloist de verdad):

| Qué | Resultado |
|---|---|
| `dotnet build -c Release --no-incremental` (JIT) | 0 errores, **13 avisos**, los de siempre (ninguno en `RocolaOs/`) |
| `dotnet publish -p:PublishAot=true -r linux-x64` | sale con 0, **0 avisos IL/AOT** |
| `sustitucion/prueba.sh`, JIT, las 14 escenas | **85 de 85** (las 70 de antes más 15: `caida` 4, `atasca` 6, `seguida` 4 y una en `frena`; `sinllave` cuenta un renglón) |
| `sustitucion/prueba.sh`, NativeAOT, las 14 escenas | **85 de 85** |
| `caida`, `atasca`, `seguida` con el programa de la base (222fe30, compilado aparte) | **9 MAL de 14**: `caida` **52 peticiones** a `/v1` en 51 s (con el arreglo, 2) y un renglón cada vez; `atasca` 13,8 s de silencio sin volver al MP3 (hasta que el falso volvió a mandar; sin eso, 30 s), con el arreglo 3,8 s y tu MP3 desde los 12,1 s; `seguida` el vals desde Spotify con hueco, con el arreglo de su MP3 con 0,00 s de hueco |
| Peticiones por escena (JIT) | `isrc` 1, `titulo` 1, `sinpareja` 2 (+0 la sesión siguiente), `nx` 1, `corte` 1, `forma` 1, `frena` 1, `caida` 2, `atasca` 1, `seguida` 1, `apagado`/`sinllave`/`sinsesion` 0 |
| `ganchos.py` | TODO BIEN (`Codigo/` sin cambios de esta revisión) |
| Lo de las dos revisiones juntas (`aece67a`, sobre `e9c7b32`), las 16 escenas | JIT **97 de 97** y NativeAOT **97 de 97** (con `apagon` y `tarde` del otro revisor) |

**No se puede probar aquí**: el Soloist de verdad (nunca se ejecutó: ¿pasa a «buffering» sin red, como el falso?; ¿cuánto colchón trae?), la API de
verdad con la cuenta del dueño (qué 4xx da el modo desarrollo), el A4 (que 5 s de atasco no salten con la red lenta del bar: si salta mucho,
subir `AtascoMs`), y discos en vivo de verdad para `seguida` (que `FinUtil` del repaso los reconozca).

## Revisión (invisible-y-reglas)

15-11-2026, revisor independiente (enfoque: que de verdad no se note nada —pantalla, avisos, estado, lo más sonado, estadísticas, el crédito,
`rocola_estado.txt` tras un apagón, el buscador—, las reglas A, B y C al pie de la letra contra las decisiones del 6 y 7-11-2026 y el programa
anterior —`Motor/SustitucionSpotify.cs` y `SpotifyMas.cs`—, que con todo apagado o sin llaves sea XP tal cual, y que nunca suene algo que no se
pagó ni se cobre dos veces). Comprobado en el código y corriendo las escenas. Lo arreglado (solo `RocolaOs/RocolaOs.CalidadReglas.cs`,
`RocolaOs/RocolaOs.Calidad.cs` —el latido— y la prueba; **ningún gancho nuevo**, `Codigo/` sin tocar; lo del otro revisor no se tocó):

| # | Qué pasaba | Arreglo | Escena |
|---|---|---|---|
| I1 | **A, la frecuencia en VBR**: un VBR grabado a menos de 44,1 kHz (22 o 32 kHz) no contaba como «suena peor» hasta que el repaso midiera su corte. En el programa anterior la regla «en VBR solo el corte» quitaba la **tasa** (que en VBR sale de la música), no la frecuencia, que la cabecera dice igual | La frecuencia se mira antes de lo del VBR; en VBR la tasa y el pre-eco siguen sin contar | `reglas` (+3 casos: VBR a 22 kHz sí; VBR de 90 kbps con pre-eco y corte de 16,5 kHz no; lo que no es MP3 sin corte no) |
| I2 | **B, por el ISRC sin mirar la duración**: valía cualquier canción con el mismo ISRC. El programa anterior pedía además la duración a 3 s («Spotify a veces devuelve parecidas»; un ISRC mal puesto en la etiqueta o un MP3 cortado) | Mismo ISRC **y** la duración a 3 s (si se saben las dos; sin la del MP3, el ISRC basta). Si ninguna pasa queda «sin pareja» sin probar por título (más estricto que antes: no cuesta peticiones) | `reglas` (+3 casos: 4 s de más no; 2,5 s sí aunque el título sea otro; sin duración, el ISRC) |
| I3 | **La muda del diagnóstico completo** (Ctrl+F3 → Imagen: una canción al azar que la rocola pone callada, sin pagar) la miraba el latido como si alguien la hubiera pedido: si sonaba peor, **una o dos peticiones a la API por algo que nadie pagó** (al abrirla ya iba por tu MP3: `PlayTrackForMeasurement`) | El latido no mira la muda (`EsMudaDiag`) | — (pide el diagnóstico con la tarjeta; arreglo de una línea, visto en el código) |
| I4 | **Lo abierto por delante como MP3 se quedaba**: lo que el calentador de XP abría para la de la cola **antes** de poder sustituirla (al arrancar, con Soloist aún sin sesión; al volver de un apagón) esperaba como MP3 y `BeginAudioOpen` se lo llevaba por la ruta del MP3: esa vez sonaba tu MP3 aunque Soloist ya estuviera listo. (Lo que llegaba con la pareja ya se soltaba; esto no.) | Cada segundo, en el latido y sin disco: si lo que espera por delante ya iría desde Spotify (g60), se suelta; al tocarle entra por g13 | `tarde` (nueva; sin el arreglo, 2 MAL: la cumbia sonó 16,1 s de su MP3 y 0 de Spotify) |

Lo que se miró y **está bien** (sin cambios):
- **La pantalla**: lo que suena, la cola, la portada, el título, el artista y el disco son los del `PlayItem` de XP, que no cambia (`mira`, campo por
  campo, igual con tu MP3 —`frena`— que con su versión de Spotify —`isrc`—). Lo único que puede verse distinto es el reloj de lo que suena
  (`Fmt(cur) / Fmt(tot)` del lector): ya sonando dice lo que dura la de Spotify (a 2 s de la tuya por la regla B) más lo que tardó en empezar
  (~1 s); y si Spotify no empieza, cuenta los 6 s de silencio y vuelve a 0:00 con tu MP3. Es el lector, no un aviso: se deja así.
- **Avisos**: ninguno. La fuente del núcleo va con `SinDevolver` (ni «No se pudo reproducir: crédito devuelto» ni `RefundCredit`); volver al MP3 es
  el camino de XP que retoma una canción, sin `ShowToast` ni `EstadoPasajero` (`nx`, `apagon`).
- **El crédito y el turno**: se cobra una vez, al pedir, como XP (`AddSong`); volver al MP3 no cobra (`PlayTrack`, no `PlayNext`) ni devuelve. Si tu
  MP3 tampoco abre, lo de XP: crédito devuelto, como con una dañada. `rocola_spotify.txt` sin COBRADA ni DEVUELTA en todas las escenas.
- **Lo más sonado y las estadísticas**: `RecordPlay` cuenta al pedir, con tu MP3 (g20 solo calla `spotify:`); ninguna tabla de lo aprendido de XP
  recibe `spotify:` (g23/g24 con la ruta de la sombra) (`isrc`, `apagon`).
- **El apagón**: `rocola_estado.txt` lleva tus MP3 (`sonando=` y `cola=`, `PathKey` del `PlayItem`), nunca `spotify:`, así que R5 del núcleo (lo
  pagado de Spotify que no sonó vuelve como crédito) no los toca: al volver, los mismos créditos, la que sonaba desde el principio (como XP) y la
  de la cola detrás, cada una una vez (`apagon`, nueva).
- **El buscador**: la sustitución no toca nada de él (g40/g41 son del catálogo); lo que encuentra y lo que se pide es tu canción.
- **Nada suena sin pagar**: Soloist solo recibe la uri de la pareja de la canción que le toca sonar (`nowPlaying`, R4 del otro revisor); el
  calentador no abre `spotify:` (`AudioFileSignature` da null), así que el relevo sin pausa nunca se lleva una de Spotify; lo que llega tarde lo
  pausa el núcleo (`NoPedido`). La de Ctrl+F3 que mide y la retomada van por tu MP3.
- **Apagado y sin llaves = XP**: sin `ROCOLA_OS`, con `calidad_spotify=0` o sin la función «spotify», g60/g61/g62 devuelven lo de XP; sin la llave
  de la API no hay ni una petición; sin sesión de Soloist, tu MP3 al momento (`apagado`, `sinllave`, `sinsesion`).
- **A, B y C contra el 7-11-2026**: las cinco reglas de A (con I1); B con ISRC (con I2) o regla 2 —título base, mismas marcas, artista principal
  palabra por palabra sin acentos (`UnicodeXp.FormaD`), duración a 2 s, el disco con tu nombre y no recopilatorios (aquí el disco con tu nombre gana
  siempre y la duración desempata; antes, con 1,5 s de diferencia, la duración le podía ganar: una diferencia menor, se deja)—; C con r ≥ 0,80 y < 0,55, ±3 tramos, al menos 40 tramos y la forma plana «no se sabe», como antes.

**Diferencias con el programa anterior que quedan, para que el dueño decida** (no se cambiaron):
- *128 kbps sin el corte medido*: el programa anterior la sustituía (`!conCorte || corte < 17 500`); aquí no hasta que el repaso mida el corte
  («sin medida no se sustituye», la letra de la decisión: «128 kbps con corte bajo 17,5 kHz»). Con el repaso de XP hecho da lo mismo.
- *Sin título, artista o duración*: el anterior ni preguntaba; aquí se pregunta por el ISRC si lo hay (la regla 2 sin duración no casa nada:
  una petición perdida en un caso raro).

**Abierto** (no arreglado): lo de §8; el reloj de lo que suena (arriba); la bitácora del billar con el id de Spotify. La muda del diagnóstico (I3)
no tiene escena (pide el diagnóstico completo con tarjeta).

Pruebas de esta revisión (rocola-xp de esta rama con lo que el otro revisor tenía en la copia de trabajo en ese momento; Soloist falso, API
falsa, tarjeta con reloj; **nunca el Soloist de verdad**, ni la cuenta del dueño, ni el A4):

| Qué | Resultado |
|---|---|
| `dotnet build -c Release` (JIT) | 0 errores, 13 avisos (los de siempre) |
| `dotnet publish -p:PublishAot=true -r linux-x64` | salida 0, 0 avisos IL/AOT |
| `--prueba-calidad` | **40 de 40** (34 de antes + 6 de I1 e I2) |
| `tarde` sin el arreglo I4 (JIT) | 3 de 5: **2 MAL** (no se suelta; la cumbia, 16,1 s de su MP3 y 0 de Spotify): el fallo existe |
| `sustitucion/prueba.sh`, JIT, 13 escenas (las 11 de antes, `apagon` y `tarde`) | **83 de 83** |
| `sustitucion/prueba.sh`, NativeAOT, las mismas 13 | **82 de 83** la primera vez: en `apagon` el `mira` de la primera sesión, a 1 s del corte, no llegó a contestarse (la comprobación de la prueba, no el programa: la bitácora dice «suena desde Spotify» antes del corte); con el `mira` a los 17 s, `apagon` **7 de 7** en NativeAOT y en JIT |
| `ganchos.py` | TODO BIEN (`Codigo/` sin tocar) |

Las escenas nuevas del otro revisor (`caida`, `atasca`, `seguida`) no se corrieron en esta revisión: son suyas.
