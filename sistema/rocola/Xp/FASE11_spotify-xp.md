# Fase 11, grupo spotify-xp: el núcleo del modo Spotify en rocola-xp (con Spotify Soloist)

14-11-2026. Rama `fase11-spotify-xp`, sobre `fase11-andamio` (`b75c4a8`). Trabajo URGENTE pedido por el dueño para el cumpleaños de esta
semana: «Lo de Spotify será todo en la rocola, nada en el celular». Este grupo es el **núcleo** (lo que no se ve): abrir el vigilante de
Soloist, hablar con Soloist por su WebSocket, oír su sonido por la cadena de XP, cobrar y llevar el turno por el camino de XP y cumplir
las reglas R1-R9. El catálogo (la API web y las pantallas) es otro grupo y llama a lo de aquí por **I6**. **Nada de esto se probó en el
A4 ni con el Soloist de verdad** (que nunca se ejecutó aquí): todo con un Soloist FALSO que hace lo que dice su documentación.

Nada de esto descifra audio, saca llaves ni imita al cliente oficial: Soloist **es** el cliente oficial de Spotify; lo nuestro es lo de
alrededor (el anillo, el vigilante, el cobro, los turnos, las reglas).

## Qué se hizo

| Archivo (todos nuevos, en `RocolaOs/`) | Qué lleva |
|---|---|
| `RocolaOs.Spotify.cs` | El hilo que manda («spotify»): el WebSocket de Soloist (I2), los eventos, las reglas R2-R4 (activate, shuffle y repeat apagados, play, lo no pedido en pausa, cuándo acaba una), el vigilante (I4), el estado en `/run` y la bitácora `rocola_spotify.txt` |
| `RocolaOs.SpotifyAnillo.cs` | El lado del LECTOR del anillo de I1: mmap, crearlo por renombre si no vale, reabrirlo si cambió el inodo, el latido del lector, tomar sin esperar nunca |
| `RocolaOs.SpotifyFuente.cs` | La fuente de sonido DENTRO de Form1 (`IAudioSource` es privada de Form1): una canción de Spotify para la cadena de XP |
| `RocolaOs.SpotifyPide.cs` | La función «spotify» del andamio (apagada de fábrica en este grupo; **encendida de fábrica desde la junta**, decisión del 15-11-2026), **I6** (`RocolaOsSpotifyPista`, `RocolaOsSpotifyPide`, `RocolaOsSpotifyListo`, `RocolaOsSpotifyPortada`), el crédito que vuelve y lo que llaman los ganchos |
| `RocolaOs.SpotifyPrueba.cs` | El doble del catálogo, **solo para las pruebas** (función «spotify-prueba», `SoloEnLaLista`: con `ROCOLA_OS=1` nunca) |

Y en `Pruebas/RocolaOs/spotify-xp/`: el Soloist falso (`SoloistFalso/`, C# aparte), el vigilante falso (`vigilante-falso.sh`), el
envoltorio que hace pasar al Soloist falso por un programa para el vigilante de verdad (`soloist-envoltorio.sh`), `frecuencias.py` (qué se
oyó, de la tarjeta de prueba con reloj) y `prueba.sh` (las nueve escenas: apagado, dos, mezcla, celular, fallos, apagon, portada, pulso y
vigilante; las dos últimas necesitan el `libpulse.so.0` de rocola-pulso y el `rocola-soloist` del grupo soloist, que aún no están en esta
rama: `PULSO=` y `VIGILANTE=`).

## Cómo funciona una canción de Spotify, de punta a punta

1. **El catálogo la pide** con `RocolaOsSpotifyPide(p)` en el hilo de la pantalla. Se arma un `Track` de XP con `Path = "spotify:track:ID"`,
   su título, artista y disco (y `Genre` vacío) en un `PlayItem` y va por **`AddItem` de XP**: sin créditos la cápsula se sacude y no entra
   (y la invitación de XP o «Inserta una moneda»); si ya va seguida, no entra; si entra, cobra 1 crédito, toma su turno, el aviso de XP con
   el turno, la fugaz, y si no suena nada, `PlayNext`. Los avisos son los de `AddSong` (desde un disco) o los de `ChooseSearchResult`
   (desde el buscador: `RocolaOsSpotifyPide(p, true)`). **Nunca entra en la biblioteca de XP** (`albums`).
2. **Cuando le toca**, `PlayTrack` de XP → `OpenAudio` → gancho **g13**: una ruta «spotify:track:» se abre con `RocolaOsFuenteSpotify` (solo
   crea el objeto: no pide nada a Soloist). Lo que XP le hace a un archivo (relleno, silencios largos, sello, mono a estéreo) no va.
3. **La cadena de XP entera** (R7): el nivelado de las no medidas, el local, el billar, el limitador, la salida. Sin fundido de salida (su
   `TotalTime` es 0 hasta que suena: la canción de Spotify trae su propio final y la espera en silencio del principio correría el fundido).
4. **La primera lectura** (hilo del sonido, sin esperar): tira lo viejo del anillo, crea su petición y la deja marcada. El hilo «spotify»
   (en menos de 50 ms) manda, si hace falta, `activate`; siempre `set_shuffle false`, `set_repeat_track false` y `set_repeat_context false`
   (en ese orden: el de la documentación para «off»); apunta dónde va el anillo (`EscritoAlPedir`) y manda `play` con su uri.
5. **Nada se oye hasta que Soloist dice que toca la suya** (su uri en «playing» o «buffering»: la fase «Suena»; revisión): lo que llegue antes
   se queda en el anillo sin perderse (solo se saltan los vaciados). Desde ahí, **lo que llega después de `EscritoAlPedir` es suyo**: se
   copia; lo que falta, silencio (la red que tarda). Si en la espera Soloist tocó otra cosa (un celular, lo de antes que aún no paraba), el
   ancla pasa a donde va el anillo al empezar la suya y eso no se oye. Ritmo: la tarjeta de la rocola (el escritor solo escribe lo que falta
   para su objetivo, ~250 ms).
6. **Acaba** (R4) al llegar a su **frontera** o a su **duración**, lo primero:
   - la frontera la pone el hilo «spotify» al ver que Soloist pasó a otra (`track_changed`), se paró (`paused`/`idle` sin que la rocola lo
     pidiera), dejó de ser la bocina activa, perdió la sesión, el WebSocket se cayó más de 10 s o el anillo no creció en 5 s estando en
     «playing»: el `escrito` del anillo en ese momento;
   - la duración, la de Soloist (`duration_ms` del `track_changed`) desde `EscritoAlPedir`;
   - además, un **vaciado** del anillo a media canción (Soloist empezó otra cosa: un celular, un salto) la acaba ahí sin leerlo.
   Al acabar devuelve lo último (o 0: fin de canción para XP). XP sigue con la siguiente como con una canción que terminó (sin relevo sin
   pausa: §«Decisiones»).
7. **El hilo «spotify» la remata**: `pause` si Soloist sigue tocando (que no siga solo), el crédito si toca y la bitácora.
8. **El crédito vuelve** (R4) si acabó por un fallo con menos de 10 s de sonido, o si no llegó a sonar en 15 s desde que se pidió: por el
   camino de XP (`RefundCredit` y el mismo aviso «No se pudo reproducir: credito devuelto», en un encargo `RocolaOs:spotify-devuelve`). A XP
   nunca se le lanza un error (si no, contaría `playFailStreak` y con tres seguidas pararía la cola: R9 pide que la música siga).

## Las reglas (I5), dónde se cumplen

| Regla | Cómo |
|---|---|
| R1 apagado de fábrica (**sustituida en la junta**: desde la decisión del dueño del 15-11-2026 va **encendida** de fábrica, `DeFabrica = true`; solo `spotify=0` -«spotify apagar»- la apaga; encendida sin ninguna llave es XP tal cual: la escena «fabrica», SPOTIFY_JUNTA_PENDIENTES.md «La junta») | Lo que sigue es como quedó en el grupo: la función «spotify» con `DeFabrica = false`: sin `spotify=1` en `rocola_os.txt` (o sin «spotify» en la lista) no se arranca nada (ni hilos, ni vigilante, ni archivos); los ganchos devuelven lo de XP (`RocolaOsSpotifyAbre` → null, `RocolaOsSpotifyNoCuenta` → false, `RocolaOsSpotifyAprende` → true para toda ruta que no empiece por «spotify:»). La orden «spotify encender» es del grupo soloist; vale desde el siguiente arranque (como toda función del andamio) |
| R2 la bocina y activate | Al entrar la sesión y cuando deja de ser la activa (`device_changed`, `auth_state`, `playback_state`): `activate`, como mucho cada 5 s; antes de cada `play`, si no es la activa, `activate` |
| R3 se pide en la rocola; lo no pedido se pausa | Solo `RocolaOsSpotifyPide` mete canciones; todo lo que suene en Soloist sin una petición viva de la rocola (un celular, autoplay, radio, su cola) se pausa al momento (como mucho una pausa por segundo; mientras Soloist cambia a la pedida, hasta 8 s de gracia SOLO para lo que ya tocaba al mandar `play`; si en esa espera empieza otra cosa, se le pide otra vez la suya, una vez, y si no, pausa: revisión) y se apunta «NO PEDIDO»; no se cobra nada; y nada de eso se oye (la fuente solo lee con la suya sonando). Tras cada canción, `pause`. Shuffle y repeat, apagados al entrar, antes de cada play y si alguien los enciende |
| R4 cuándo acaba y cuándo se devuelve | Arriba (6 y 8) |
| R5 lo pagado que no sonó vuelve | Tras un apagón, y tras un cierre que conserva el estado (SIGUSR1): las de Spotify van en `rocola_estado.txt` («sonando=S|spotify:...», «cola=S|spotify:...»), `RestoreState` de XP no las encuentra en `albums` y suma su crédito (`Form1.Datos.cs` 2826-2840). Con el apagado (SIGTERM, Alt+F4) XP borra el estado entero, también los créditos: es la regla del 8-11-2026 y vale igual para lo de Spotify. Apagar el modo vale desde el siguiente arranque: lo de la cola vuelve por `RestoreState` |
| R6 no cuenta | Gancho **g20** (`RecordPlay`): ni lo más sonado, ni el artista, ni TOP, ni `rocola_stats.txt`, ni lo más pedido esta noche. `Genre` vacío: `PlayNext` no apunta género |
| R7 la cadena, como una sin medir | Ganchos **g23** (`ArmaCadena`: ni su sonoridad ni su EQ) y **g24** (`AprendeParejo`: ni el volumen parejo del billar). El volumen que pide el celular (`volumen_pedido` del anillo) nunca se aplica |
| R8 el aspecto de XP | Lo que suena: el título y el artista del `Track` (lo dibuja XP como lo suyo). La portada: si el catálogo ya la bajó (`RocolaOsSpotifyPortada`), el `PlayItem` lleva un `Album` de XP **fuera de la biblioteca** con su `CoverPath` en `/run`: XP la pinta y la hace volar a la cola como una suya. Nada nuevo se dibuja aquí |
| R9 si falla, la música sigue | Una que no puede sonar acaba sin error (en ≤ 15 s), devuelve su crédito y XP pasa a la siguiente. Sin sesión, `RocolaOsSpotifyListo` es false y no se pide nada |

## Las interfaces

- **I1, el anillo**: el lector, al pie de la letra (`RocolaOs.SpotifyAnillo.cs`): la ruta (`ROCOLA_SPOTIFY_ANILLO` o
  `$ROCOLA_RUN/rocola-xp/spotify/sonido.anillo`), carpetas 0755 y archivo 0644, el tamaño exacto (528 384 bytes), crearlo por `<ruta>.t.<pid>` y
  renombrar, reabrir al cambiar el inodo (mirado cada segundo; el mapa viejo se suelta a los 10 s porque el hilo del sonido puede estar
  leyendo de él), `gen_vaciado`/`vaciado_hasta`, `leido` con release y `escrito` con acquire, `latido_lector` (en cada lectura y cada 250 ms
  mientras hay una de Spotify abierta), `lector_presente` (1 solo mientras una de Spotify está abierta: así, sin una pedida, el escritor no
  puede escribir y lo de un celular ni siquiera avanza), `latencia_lector_us` (el `RetrasoAudible` de XP), `subdesbordes`.
- **I2**: `ws.port` y `ws.addr` de `ROCOLA_SOLOIST_DATOS` o `/rocola/datos/xp/soloist`; `ws://127.0.0.1:PUERTO` (una dirección que no sea de esta
  máquina se cambia a 127.0.0.1); sin proxy; si cambia `ws.port`, se reconecta; reintentos de 0,5 s a 5 s.
- **I3**: `spotify=` y `spotify_nombre=` (de fábrica «Rocola»; texto de hasta 64 sin comillas ni barras) en `rocola_os.txt`; el estado
  `/run/rocola-xp/spotify/estado` (`ws`, `sesion`, `activa`, `sonando`, `en_cola`, `esperando_credito`, `cobradas`, `devueltas`, y además
  `no_pedidas`, `soloist` (lo que dice Soloist que hace: playing, paused, idle...), `vigilante` (la `fase=` de `/run/rocola-soloist/estado`),
  `anillo`, `error`; se reescribe de un tirón cuando cambia, como mucho cada segundo); la bitácora
  `/rocola/datos/xp/rocola_spotify.txt` (rota a `.1` al pasar de 1 MB; nunca la llave). **`esperando_credito`** = cuántas veces se pidió
  una de Spotify sin créditos en esta sesión (la interfaz no lo definía; decidido aquí).
- **I4**: el vigilante como hijo, desde su hilo, con `corre` y `SPOTIFY_NOMBRE` y `ROCOLA_SPOTIFY_ANILLO` en el entorno (lo demás del entorno
  se hereda, `ROCOLA_RUN` incluido); si sale, se vuelve a abrir a los 2 s, 10 s, 30 s y 2 min (vuelve a 2 s tras 10 min en pie); al cerrar,
  SIGTERM y hasta 2,5 s; si no salió, un hilo lo mata con SIGKILL a los 10 s si la rocola sigue viva (si la rocola muere antes, el vigilante
  sale solo al ver que murió su padre: I4).
- **I6** (para el catálogo):

```csharp
// en el hilo de la pantalla, al elegir con PONER una canción de Spotify
RocolaOsSpotifyPista p = new RocolaOsSpotifyPista { Uri = "spotify:track:" + id, Titulo = nombre, Artista = artista, Disco = disco,
                                                    DuracionMs = ms, PortadaUrl = url, DiscoUri = "spotify:album:" + idDisco };
if (!RocolaOsSpotifyListo) { /* el catálogo lo dice con lo de XP, sin pedir */ return; }
bool entro = RocolaOsSpotifyPide(p);        // como PONER en un disco (avisos de AddSong)
// o RocolaOsSpotifyPide(p, true)          // como desde el buscador (avisos de ChooseSearchResult)
string portada = RocolaOsSpotifyPortada(p); // /run/rocola-xp/spotify/portadas/<id>.jpg si ya está; si no, la pide y null
```

  La portada se baja en un hilo (`spotify-portadas`), solo de `https://*.scdn.co`/`*.spotifycdn.com` (y `http://127.0.0.1` en las pruebas),
  como mucho 2 MB y solo si es un JPEG, a `/run` (nunca al disco); las de más de un día se borran al arrancar. Se nombra por su **disco**
  (`portadas/<id de DiscoUri>.jpg`, el mismo nombre que le da el catálogo: si el catálogo ya la bajó, no se baja otra vez, y todas las
  canciones de un disco usan una); sin `DiscoUri` (o con un id que no es base62), por la canción (`portadas/<id de la canción>.jpg`). Conviene que el catálogo la pida
  al enseñar la canción: así, al pedirla, ya está y sale en lo que suena.

## Los ganchos

Todos en `RocolaOs/GANCHOS.tsv` (ordenado por id) y `ganchos.py` → TODO BIEN. Los cuatro, una línea cambiada en su sitio, con sus finales.

| Id | Dónde | Antes → ahora | Apagado |
|---|---|---|---|
| g13 | `Codigo/Form1.Sonido.cs:1456` (`OpenAudio`) | `string original = RutaOriginalDe(path);` → `{ IAudioSource rocolaOsSpotify = RocolaOsSpotifyAbre(path); if (rocolaOsSpotify != null) return rocolaOsSpotify; } string original = ...` | null: lo de XP |
| g20 | `Codigo/Form1.Datos.cs:2878` (`RecordPlay`) | `if (it == null) return;` → `if (it == null \|\| RocolaOsSpotifyNoCuenta(it)) return;` | false |
| g23 | `Codigo/Form1.Sonido.cs:1296` (`ArmaCadena`) | `bool entera = saltadoSeg <= 0 && !iaMejorada;` → `... && RocolaOsSpotifyAprende(t.Path);` | true |
| g24 | `Codigo/VolumenParejo.cs:173` (`AprendeParejo`) | `if (c == null \|\| ... c.Cancion.Path == null) return;` → `... \|\| !RocolaOsSpotifyAprende(c.Cancion.Path)) return;` | true |

**No hicieron falta** (mirado en el código de hoy):
- **g19** (la apertura por adelantado): `PrecalientaSiguiente` abre un `FileStream` con la ruta antes de pedir el lector por adelantado; con
  «spotify:» falla (y lo atrapa) y nunca llega a `PideLookahead`; y `SinSilencios.cs:266` sí lo pide, pero `AudioFileSignature` da null y
  `MiraPorDelante.Pide` lo suelta. Resultado: una de Spotify nunca se abre por adelantado, el relevo sin pausa (`lookahead.Cumple`) nunca la
  toma y suena por el camino de siempre (cierra la salida y abre la siguiente). Es lo que el plan pedía («si no está precargada, se acaba la
  de antes y empieza como en el programa anterior»), y así nunca hay dos de Spotify vivas a la vez. Lo medido: 1,05 s entre dos de
  Spotify seguidas (la tarjeta con reloj); la latencia real de Soloist en el A4 se sumará.
- **Cobrar y encolar**: `RocolaOsSpotifyPide` llama a `AddItem` de XP desde una parte de Form1 (no hace falta gancho).
- **Dañada**: `MarcaDanada` necesita `AudioFileSignature`, que con «spotify:» es null: XP nunca marca dañada una de Spotify (y la fuente no
  lanza errores).
- **La portada**: `PlayItem.Disc` con un `Album` fuera de `albums` (sin gancho).

## Decisiones (lo que no estaba dicho)

- **Sin relevo sin pausa entre Spotify y lo demás** (ver g19): entre dos canciones, la salida se cierra y se abre (1,05 s medido + lo que
  tarde Soloist). Si en el A4 molesta, el siguiente paso es g19 (una firma fija para «spotify:») y que la fuente solo mande `play` al
  empezar a leerse (ya lo hace).
- **Un vaciado del anillo a media canción la acaba** (sin oír lo de después): es lo que hace Soloist (su libpulse) al empezar otra cosa.
  Si el Soloist de verdad vaciara también en otros momentos (no se sabe sin el A4), se notaría como canciones cortadas con el crédito
  devuelto: está en la lista del A4.
- **Una cortada por la rocola** (SALTAR, `Dispose` de XP) no devuelve el crédito, como una de XP saltada.
- **Una que sonó y se reinició la rocola** (SIGUSR1) vuelve como crédito aunque haya sonado (XP no sabe qué parte sonó).
- **`esperando_credito`**: cuántos pedidos sin créditos (arriba).
- **La pausa de lo no pedido**: como mucho una por segundo y no en los 2 s siguientes a una pausa de la rocola (si no, la pausa de la
  rocola al acabar una canción se apuntaba también como «NO PEDIDO»).

## La junta con los otros tres grupos

- **catálogo** (`fase11-catalogo`): su `RocolaOs/RocolaOs.SpotifyCatalogoDobleI6.cs` se borra entero al juntar (declara otra vez la función
  «spotify», `RocolaOsSpotifyPista`, `RocolaOsSpotifyListo` y `RocolaOsSpotifyPide`: no compilan juntos). Lo que ya casa: su
  `RocolaOsSpotifyPista` tiene los mismos siete campos; nombra la portada igual (`/run/rocola-xp/spotify/portadas/<id del disco>.jpg`: el
  núcleo la encuentra y no la baja otra vez); su g43 lleva a `/run` la miniatura que XP hace de esa portada (sin el catálogo, la escena
  «portada» ve 2 miniaturas en los datos: con él, ninguna). Lo que conviene cambiar al juntar: desde el buscador (g41) llamar a
  `RocolaOsSpotifyPide(p, true)` (los avisos de `ChooseSearchResult`, como XP desde el buscador); desde un disco (g42),
  `RocolaOsSpotifyPide(p)` como ya hace. Sus ganchos (g40-g50) y los del núcleo (g13, g20, g23, g24) no tocan las mismas líneas.
- **pulso** (`fase11-pulso`): la escena «pulso» (y «vigilante») corre con su `libpulse.so.0` compilado de `b761896`. Su LEEME pide además
  al lector no consumir con `tapado=1`, sin flujo o con el latido del escritor parado; I1 no lo pide y el núcleo sí consume lo pendiente
  hasta la frontera de la canción (es su final: lo que Soloist escribió antes de taparse). Sin consecuencias en las pruebas.
- **soloist** (`fase11-soloist`): la escena «vigilante» corre con su `rocola-soloist` de `0cd4314` (`ROCOLA_SOLOIST_BIN` sin poner = el
  suyo en `/usr/bin`). Lee `ROCOLA_SOLOIST_DATOS` y `ROCOLA_RUN` igual que rocola-xp; el núcleo apunta su `fase=` en su estado.

## Comprobaciones

Todo con el Soloist FALSO (nunca el de verdad), canciones inventadas (un tono por canción: 440, 550, 660, 770, 880 Hz; el autoplay del
falso, 990 Hz), la tarjeta de prueba con reloj (`libasound_module_pcm_reloj.so`: lo que sale por la tarjeta, a su ritmo, a un archivo) y
`frecuencias.py` (tramos de 50 ms con su frecuencia). Programa final: `09f96f0` (las cuentas de abajo); la base sigue siendo
`fase11-andamio` `b75c4a8` (no cambió: no había nada que juntar).

| Qué | Cómo | Resultado |
|---|---|---|
| Compilar | `dotnet build -c Release --no-incremental` | 0 errores, **13 avisos**, los mismos 13 de `avisos-base.txt` (archivo, línea y código; 0 nuevos) |
| NativeAOT | `dotnet publish -p:PublishAot=true -r linux-x64` | salida 0, **0 avisos IL/AOT** |
| Las escenas de Spotify, JIT | `prueba.sh` (9 escenas, rocola-xp de verdad sin pantalla) | **86 de 86** BIEN |
| Las escenas de Spotify, NativeAOT | `prueba.sh aot:...` | **86 de 86** BIEN |
| Los ganchos | `ganchos.py` y `diff-xp.py --sin-ganchos` (los 109 archivos) | TODO BIEN (13 ganchos en 7 archivos; `Codigo/` vuelve byte a byte a fc7b667); 5095 quitadas, 4828 puestas y 229 sin marca (las de la base) |
| Nivel 0 (sin `ROCOLA_OS`), con `nivel0.sh` del grupo nivel0 (`00ae545`) | `codigo`, `ganchos`, 10 escenas JIT alternadas con la referencia (arranque, moneda, video, sigterm, f3sigterm, kill9, cierreq, altf4, buscador, f3billar: las que pasan por lo que tocan los ganchos: abrir, armar la cadena, contar lo sonado, el billar), las 7 de NativeAOT y `cierre_rocola_os.sh` | codigo 109 de 109; escenas JIT 8 IGUALES y 2 ACEPTADAS (sigterm y f3sigterm: el estado se borra, lo aceptado de siempre); NativeAOT 7 de 7 IGUALES (las escenas con el programa de `0aa2ee4`: el último commit solo añade la línea `vigilante=` al estado de Spotify, que con el modo apagado no corre); cierre 43 de 43 en JIT y 43 de 43 en NativeAOT (con `09f96f0`) |
| Nivel 1 (`ROCOLA_OS=1`, `spotify` apagada de fábrica) | `NIVEL=1 nivel0.sh`, 8 escenas JIT y las 7 de NativeAOT, comparando sin «RocolaOs:» | salida 0: JIT 7 IGUALES y 1 ACEPTADA (sigterm: el estado se borra); NativeAOT 7 de 7 IGUALES (con `09f96f0`; sin red; nada quedo en la carpeta `/run` de prueba) |

Las escenas de `prueba.sh` (cada una, una rocola-xp entera con sus datos de prueba, sus teclas por `ROCOLA_PULSA` y los pedidos de Spotify
por el doble del catálogo):

- **apagado** (12): sin `ROCOLA_OS` y con `ROCOLA_OS=1` (spotify apagada): no se abre el vigilante, nada en `/run`, ni bitácora, nada de
  «RocolaOs:spotify» en la traza, el pedido no se atiende y la canción de XP suena como siempre.
- **dos** (20): dos pedidas seguidas (8 s y 6 s): el vigilante con `SPOTIFY_NOMBRE` y el anillo (528 384 bytes); cobra 4 → 3 → 2;
  activate, shuffle y los dos repeat apagados antes del primer play; play con cada uri en su orden; **la primera entera (8,00 s), la segunda
  entera (6,00 s), en su orden, 1,05 s de hueco entre las dos**; el autoplay que el falso empieza al acabar cada una: 0,00 s oído; pause
  tras cada una; al cerrar con SIGTERM, lo de Spotify en 18 ms (en total, de la señal a salir: 0,07 s), el vigilante recibe SIGTERM y el
  falso borra su `ws.port`; nada de «spotify:» en `rocola_stats.txt`, `rocola_noche.txt` ni en las tablas de lo aprendido (R6, R7).
- **mezcla** (8): XP, Spotify, XP, Spotify en la misma cola: cada una toma su turno detrás de la de XP (créditos 3 → 2 con turno 1; 1 → 0
  como la tercera), sin créditos no entra ni cobra (desde un disco y desde el buscador), la que no entró no suena, las dos enteras (6,05 y
  5,00 s), el orden que se oye es XP, Spotify, XP, Spotify; con SIGUSR1 `rocola_stats.txt` cuenta lo de XP y nada de Spotify.
- **celular** (10): un «celular» pone algo con la rocola callada y otra cosa a media canción de la rocola, y algo en la cola de Spotify:
  nada de eso se oye (0,00 s), se pausa y se apunta «NO PEDIDO»; la cortada por el celular (sonó unos 5 s) devuelve su crédito; dejar de
  ser la bocina activa acaba la canción, devuelve y manda activate; sin sesión, el pedido no entra ni cobra; los créditos vuelven a 4.
- **fallos** (7): la que nunca empieza (se devuelve a los 15 s); el anillo sin sonido 5 s estando en «playing» (acaba y se devuelve); el
  Soloist que se cae a media canción (acaba y se devuelve); el vigilante lo vuelve a abrir, la rocola se reconecta y una nueva suena entera
  (3,95 de 4 s); 3 devueltas, quedan 3 créditos.
- **apagon** (4): SIGKILL con una de Spotify sonando y otra en la cola: el estado de XP las guarda con 2 créditos; el vigilante y el falso se
  van solos al morir la rocola; al volver, lo pagado de Spotify vuelve como crédito (2 + 2 = 4) y no suena nada de Spotify.
- **portada** (10): la portada se baja a `/run` con el nombre de su disco (la primera vez `null`, a los 2 s la ruta), es la del servidor,
  otra canción del mismo disco usa la misma sin bajarla otra vez, la pedida la lleva, lo que no es JPEG no se guarda, de otro sitio ni se
  pide, un id raro no sale de la carpeta, nada va a los datos, y la canción suena entera.
- **pulso** (6): lo de «dos» con el sonido por el `libpulse.so.0` de rocola-pulso (`fase11-pulso` `b761896`, compilado aquí): conecta por él,
  las dos enteras (8,05 y 6,00 s) y en su orden, el autoplay 0,00 s, sin devolver.
- **vigilante** (9): lo mismo con el `rocola-soloist` DE VERDAD del grupo soloist (`fase11-soloist` `0cd4314`) entre rocola-xp y el falso
  (con una llave inventada en `rocola_spotify_llave.txt`): fase=corriendo (y el estado de rocola-xp lo dice), el falso arrancó con la
  bocina «Rocola» y rocola-pulso, las dos enteras (8,00 y 6,00 s), el autoplay 0,00 s, sin devolver, **la llave no sale en ninguna
  bitácora ni estado**, y al cerrar la rocola el vigilante y el falso se van (0,18 s).

Lo que no se pudo comprobar aquí: todo lo del A4 (abajo). Los recorridos no se corrieron: los ganchos no tocan nada que se dibuje (con el
modo apagado devuelven lo de XP), y las 17 escenas comparadas (con la pared, el buscador, Ctrl+F3 y el billar) salieron iguales.

## Para el A4 (solo con el Soloist de verdad y la cuenta del dueño)

1. Que Soloist cargue nuestro `libpulse.so.0` (rocola-pulso) y escriba en el anillo: `/run/rocola-xp/spotify/estado` con `anillo=1` y, al
   pedir una, que suene por las bocinas de la rocola (ni por el HDMI ni callada).
2. Emparejar una vez desde la app de Spotify (celular o la de escritorio de la laptop) con la bocina «Rocola» en la red del bar: después,
   `sesion=1` en el estado y `RocolaOsSpotifyListo` verdadero; tras reiniciar la rocola, la sesión vuelve sola.
3. Pedir dos seguidas: cada una entera, sin el principio de la siguiente ni un trozo de otra al final; cuánto silencio hay entre las dos.
4. Que al acabar una, Soloist no siga solo con su autoplay (se oye silencio o la siguiente de la rocola, nunca otra canción de Spotify).
5. Poner algo desde un celular mientras suena una de la rocola y con la rocola callada: no se oye (la bitácora dice «NO PEDIDO»).
6. Desconectar el cable de internet a media canción: acaba en menos de 15 s, devuelve el crédito si sonó menos de 10 s, y lo de XP sigue.
7. Si Soloist vacía su flujo en algún otro momento (un tirón de red, el cambio de calidad): se vería como canciones cortadas a media con
   «vaciado a media cancion» en la bitácora.
8. El volumen: lo de Spotify suena como una de XP sin medir (no más fuerte ni más bajo que lo normal); el volumen del celular no cambia nada.
9. La portada en lo que suena (con el catálogo).
10. Entre dos canciones (y con la rocola sin una de Spotify abierta) el lector no está (`lector_presente=0`): rocola-pulso le da a Soloist
    `writable_size` 0 y Soloist espera. Que Soloist no se queje ni se desconecte por eso (su bitácora, `rocola_soloist.txt`) y que la
    siguiente empiece bien.
11. El cierre (botón de encendido): `rocola_spotify.txt` dice «se cierra (WindowsShutDown) ... en total N ms» con N pequeño, y Soloist sale.
12. (revisión) Que Soloist diga en `track_changed` la MISMA uri que se le pidió. Si cambiara una canción por su equivalente de otro país
    (el «relinking» de Spotify) diría otra uri: la rocola la tomaría por no pedida, la pediría otra vez y, si sigue igual, la pausaría y
    devolvería el crédito a los 15 s (nunca suena algo que no sea la pedida). Si pasa, la bitácora dice «NO PEDIDO: mientras se esperaba
    spotify:track:...» con la otra uri: el catálogo tendría que buscar con el mercado del dueño (`market`).
13. (revisión) Qué escribe Soloist en `ws.addr` (la documentación no lo dice): la rocola se conecta siempre a `127.0.0.1` con `ws.port`, y si
    `ws.addr` dice otra cosa lo apunta una vez en `rocola_spotify.txt` («ws.addr dice ...»).

## Riesgos

- **Nada probado con el Soloist de verdad**: el falso hace lo que dice la documentación; el de verdad puede mandar los eventos en otro orden
  (p. ej. `playback_changed` antes de `track_changed`), tardar más en empezar (15 s de plazo) o vaciar su flujo en otros momentos.
- **Si Soloist manda `track_changed` de la siguiente antes de acabar de escribir la de antes**, la frontera la cortaría unos ms antes; si lo
  manda tarde, se oirían unos ms de la siguiente (lo limita la duración de Soloist). Solo se sabe en el A4.
- **El candado de lo pesado**: los servidores de compilación de .NET (VBCSCompiler) heredan el descriptor del candado y lo retienen hasta
  10 min sin hacer nada; aquí se compiló siempre con `--disable-build-servers -p:UseSharedCompilation=false` para no hacerlo.
- **La función de prueba va dentro del programa** (`RocolaOs.SpotifyPrueba.cs`, como la del andamio): solo con `ROCOLA_OS=...,spotify-prueba`.

## Revisión (xp-intacto-y-reglas)

Revisor independiente, 15-11-2026, sobre 67a25f8 (la base fase11-andamio b75c4a8 sigue igual y no hay fase11-junta0: nada que juntar).
Enfoque: con el modo apagado (y sin `ROCOLA_OS`) todo sale como XP, y con el modo encendido R1-R9 e I6 al pie de la letra.

**Lo que se comprobó y está bien.** Los cuatro ganchos, leídos contra el código de hoy: apagados devuelven exactamente lo de XP y sin
efectos (`RocolaOs.Activo` primero; con `ROCOLA_OS=1` y `spotify=0` solo cambia algo para rutas «spotify:», que XP no puede tener); una
línea cambiada en su sitio, con su marca y su final de línea (CRLF en Form1.Datos.cs y Form1.Sonido.cs, LF en VolumenParejo.cs, como
estaban). `ganchos.py`: TODO BIEN (13 ganchos en 7 archivos, Codigo/ vuelve byte a byte a fc7b667); `diff-xp.py --sin-ganchos` en los 109:
5095 quitadas, 4828 puestas, 229 sin marca (lo de la base). El pedir va por `AddItem` de XP (dañada, sin créditos con la sacudida, «ya va
seguida», turno, `RecordPlay` -que g20 calla-, cobro, `PlayNext`) con los avisos de `AddSong` y `ChooseSearchResult` copiados letra por
letra; la devolución por `RefundCredit` con el aviso de XP; tras un apagón `RestoreState` no encuentra la «spotify:» en `albums`,
`RaizConfiguradaAusente` da null y suma su crédito (R5); la apertura por adelantado nunca abre una «spotify:» (`MiraPorDelante.Pide`: la
firma es null), así que g19 no hace falta; `EnsureTags` no toca el título (sin extensión .mp3).

**Lo que estaba mal y se arregló** (commit «Spotify (spotify-xp, revision): ...»):

1. **R3, GRAVE: lo que ponía un celular mientras la pedida esperaba SONABA, y la pedida no.** Con `play` mandado y Soloist aún sin tocar la
   pedida, `NoPedido` dejaba pasar CUALQUIER cosa 8 s, y la fuente oía todo lo que llegaba al anillo desde el `play`. Reproducido con la
   escena nueva «carrera» (Soloist que tarda 3 s en empezar y un celular a 1 s del `play`) contra el programa de antes: **el celular se oyó
   6,00 s, la pedida 0,00 s y el crédito NO se devolvió** (el vaciado de Soloist al empezar la pedida, ya con «sonido», la acabó ahí).
   Ahora: la fuente no lee nada hasta la fase «Suena» (Soloist dice que toca SU uri; lo de antes se queda en el anillo sin perderse); si en
   la espera Soloist tocó otra cosa, el ancla pasa a donde va el anillo al empezar la suya; y en la espera solo tiene gracia (8 s) lo que ya
   tocaba al mandar `play`: otra cosa es «NO PEDIDO» al momento y se le pide otra vez la suya (una vez; si vuelve a pasar, pausa y, a los
   15 s, el crédito). Con el arreglo: celular 0,00 s, la pedida 8,00 s entera, 1 cobrada y 0 devueltas, también por rocola-pulso.
2. **I2: la conexión usaba `ws.addr:ws.port`.** La documentación de Soloist no dice si `ws.addr` trae solo la dirección; si trae
   «127.0.0.1:PUERTO», la uri salía mal y la rocola no se conectaba NUNCA. Ahora siempre `ws://127.0.0.1:PUERTO` (lo que dice I2); un
   `ws.addr` distinto solo se apunta una vez.
3. **El LEEME de rocola-pulso** (su revisión 94ad648/896bc5d, «Lo que el LECTOR tiene que hacer», paso 4) pide saltar los vaciados también
   sin consumir; la fuente lo hace ahora mientras espera (`SaltaVaciado`). Lo demás de esa lista ya se cumplía o no hace daño: consumir con
   `tapado=1` (paso 3) se dejó como estaba (lo pendiente hasta la frontera es el final de la suya; en `flujo.c` de rocola-pulso, `started`
   solo cuenta destapado y destapar vuelve a marcar desde lo consumido).

**Comprobaciones repetidas (con el candado de lo pesado):**

| Qué | Resultado |
|---|---|
| `dotnet build -c Release --no-incremental` | 0 errores, 13 avisos: los mismos 13 de `avisos-base.txt` (archivo, línea y código) |
| NativeAOT (`dotnet publish -p:PublishAot=true -r linux-x64`) | salida 0, 0 avisos IL/AOT |
| `prueba.sh`, JIT, las 10 escenas (con «carrera») | **92 de 92** BIEN; y «pulso» con el libpulse.so.0 de la rama de hoy de rocola-pulso (896bc5d): **9 de 9** (con «pulso-carrera») |
| `prueba.sh`, NativeAOT, las 10 escenas | **95 de 95** BIEN (con «pulso-carrera»; vigilante: el rocola-soloist de fase11-soloist 0cd4314) |
| «carrera» contra el programa de antes (67a25f8, JIT) | **3 MAL de 6**: celular 6,00 s, pedida 0,00 s (la escena sí atrapa el fallo) |
| `ganchos.py` / `diff-xp.py --sin-ganchos` | TODO BIEN / 5095, 4828, 229 (lo de la base) |
| Nivel 0 (sin `ROCOLA_OS`, alternadas con la referencia; `nivel0.sh` de fase11-nivel0 00ae545, programa e78b6aa) | JIT arranque, moneda, kill9, buscador IGUALES y sigterm ACEPTADA (el estado se borra); NativeAOT arranque, kill9, buscador IGUALES: **8 BIEN, 1 ACEPTADO, 0 MAL** |
| Nivel 1 (`NIVEL=1`, `ROCOLA_OS=1`, spotify apagada de fábrica, sin red) | JIT arranque, moneda, buscador IGUALES y sigterm ACEPTADA; NativeAOT arranque y buscador IGUALES: **6 BIEN, 1 ACEPTADO, 0 MAL** |

**Lo que queda (no se arregla aquí; para la junta o el A4):**

- Un celular A MEDIA canción de la rocola: lo que Soloist escriba de la otra entre su cambio y que la rocola vea el `track_changed`
  (milisegundos) se oye, salvo que Soloist vacíe el flujo al cambiar (el falso lo hace y no se oye nada: 0,00 s). La escena acepta hasta
  0,2 s. Solo se sabe en el A4.
- Con Soloist caído o sin sesión, cada una de Spotify que llegue a su turno espera 15 s en silencio antes de devolverse (R4 al pie de la
  letra; R9 se cumple, pero tres seguidas son 45 s sin música). Se podría acortar a 10 s con el WebSocket caído; no se hizo sin decidirlo.
- Si el escritor rehace el anillo (otro inodo) a media canción, la de Spotify que suena se corta (los contadores del anillo nuevo empiezan
  en 0). Es raro (solo si el anillo deja de valer).
- `RocolaOs.SpotifyPrueba.cs` sigue dentro del programa (solo con `ROCOLA_OS=...,spotify-prueba`): lo decide la junta.

## Revisión (sonido-hilos-y-fallos)

Revisor independiente, 15-11-2026, sobre 925612b (la base fase11-andamio b75c4a8 sigue igual y no hay fase11-junta0: nada que juntar).
Enfoque: el hilo del sonido nunca espera; la lectura del anillo (orden de memoria, vaciados, flujos, frecuencia, cortes y empalmes); los
hilos del WebSocket y del vigilante (vidas, reconexiones, cierres, huérfanos); y lo que pasa cuando algo falla.

**Lo que se comprobó y está bien.** El hilo del sonido no toma cerrojos ni espera: `Toma` copia lo que haya (acquire de `escrito`, release
de `leido`), `Pide` solo publica la petición, `Apunta` solo encola; la fase se lee antes que el ancla (`EscritoAlPedir`) y el hilo de
Spotify la escribe en el orden contrario. Los vaciados (`vaciado_hasta` antes de `gen_vaciado`) se aplican esperando y sonando. Los
eventos de una conexión vieja se tiran por su `Gen`; cada conexión tiene su hilo que acaba con `Abort`. XP no cierra una fuente mientras
el hilo de la música la lee (`SueltaLectoresAudio` espera a `CuandoPare`). El vigilante y el Soloist falso se van solos si la rocola muere
(escena «apagon»).

**Lo que estaba mal y se arregló** (commit «Spotify (spotify-xp, revision): ...»):

1. **Un hueco de silencio en el primer segundo de una canción de Spotify.** Al abrirse la salida, el hilo de la música de XP
   (`BombaSonido`) lee de golpe su primer segundo y su colchón (1,2 s), y el anillo solo guarda lo que Soloist deja por delante (unos
   250 ms). Si Soloist empieza durante esa lectura de golpe, el anillo se seca y la fuente pone silencio DENTRO de la canción. Con el
   programa de antes: **la primera canción de cuatro escenas tenía un hueco de 50 a 150 ms a unos 0,8 s de empezar** (dos, pulso,
   portada, fuera). Ahora la
   fuente no empieza a leer la canción hasta haber entregado ese 1,3 s en silencio (`ArranqueSinHuecoMs`); lo que llega mientras se queda
   en el anillo. Si Soloist tarda lo normal en empezar (la red), no cambia nada; con el falso, que empieza al instante, el hueco entre dos
   canciones pasa de 1,05 s a 1,34 s. Comprobado con `huecos` (tramos de otra cosa entre el primer y el último sonido de cada canción).
2. **La conversión de frecuencia perdía un cuadro en cada lectura** (un chasquido cada 25 ms en toda la canción). Pasa cuando el anillo no
   dice al abrirse la fuente la frecuencia que trae después: Soloist abre su flujo con la primera canción (el anillo dice 0 y la fuente
   declaraba 44,1 kHz) y lo abre a otra frecuencia. Con el programa de antes y el falso a 48 kHz con el flujo abierto tarde: **217 saltos
   en 7,5 s** (`saltos.py`: el resto de un seno, x[n+1] + x[n-1] − 2 cos(w) x[n]). Ahora la conversión guarda entre lecturas los cuadros
   que aún hacen falta: **0 saltos**. Y la fuente declara la última frecuencia que tuvo el anillo cuando este dice 0 (`UltimaFrecuencia`),
   así XP la convierte como un archivo.
3. **Con Soloist fuera (caído y sin volver, sin llave, caducado, sin sesión), cada una de Spotify de la cola esperaba 15 s en silencio**
   antes de devolverse (lo que dejó en «queda» la revisión anterior). Ahora, con Soloist fuera más de 10 s (el plazo del WebSocket de R4),
   la que llega a su turno acaba al momento y devuelve su crédito (R9). Con el programa de antes, en la escena nueva «fuera» la segunda
   seguía sin devolverse a los 32 s; ahora se devuelve en el mismo segundo que la primera.
4. **El mapa del anillo se soltaba al cerrar mientras el hilo del sonido podía estar leyendo de él.** El cierre de Rocola OS va ANTES de
   que XP pare el sonido (`OnFormClosing`), y el hilo de Spotify, al acabar, hacía `SueltaTodo` (munmap): una lectura en ese instante sería
   un SIGSEGV al cerrar (un reinicio por SIGUSR1 saldría como caída). Ahora el mapa se retira (`Cierra`: `lector_presente` a 0, `Toma` ya no
   ve nada) y se va con el programa. No se reprodujo (la ventana es de microsegundos); se arregló leyendo el orden del cierre.
5. **`LectoresAbiertos` podía quedar en −1** si `Acaba` (hilo del sonido) y `Dispose` cruzaran su `contado` (no atómico). Con −1, el
   `lector_presente` ya no subiría nunca, rocola-pulso daría `writable_size` 0 siempre y ninguna de Spotify volvería a sonar. XP no los
   cruza hoy (ver arriba), pero ahora es atómico (`Interlocked.Exchange`).
6. **Menores.** Al entrar la sesión se manda `get_state` (si la sesión entra después de conectar, como con Soloist recién abierto, su
   `playback_state` de al conectar no llegó, y lo que se sabía de antes de una caída ya no vale). Si la rocola empieza a cerrarse justo
   mientras abre el vigilante, este recibe SIGTERM al momento (antes quedaba hasta que viera morir a su padre).

**Las pruebas nuevas** (`Pruebas/RocolaOs/spotify-xp/`): `saltos.py`; `frecuencias.py` y `saltos.py` con el registro de la tarjeta (cada
apertura de la salida a su ritmo: XP abre a 48 kHz para una de 48 kHz, y antes todo `sonado.raw` se medía con la última frecuencia); el
Soloist falso con `SOLOIST_FALSO_HZ` y `SOLOIST_FALSO_FLUJO_TARDE=1`; el vigilante falso con `VIGILANTE_FALSO_UNA=1` (no vuelve a abrir a
Soloist); las escenas «fuera» y «frecuencia», y en «dos», «pulso», «vigilante» y «frecuencia» las comprobaciones de huecos y saltos.

**Comprobaciones repetidas (con el candado de lo pesado; el Soloist de verdad nunca se ejecutó):**

| Qué | Resultado |
|---|---|
| `dotnet build -c Release --no-incremental` | 0 errores, 13 avisos: los mismos 13 de `avisos-base.txt` (archivo, línea y código) |
| NativeAOT (`dotnet publish -p:PublishAot=true -r linux-x64`) | salida 0, 0 avisos IL |
| `prueba.sh`, JIT, las 12 escenas (con «fuera» y «frecuencia»; «pulso» con el libpulse.so.0 de fase11-pulso 896bc5d compilado aparte; «vigilante» con el rocola-soloist de fase11-soloist 0cd4314) | **111 de 111** BIEN |
| `prueba.sh`, NativeAOT, las 12 escenas | **111 de 111** BIEN |
| «dos», «fuera» y «frecuencia» contra el programa de antes (925612b, JIT) | **7 MAL de 34** (hueco en la primera de «dos» y de «frecuencia», 217 saltos, la de la cola sin devolver): las escenas atrapan los fallos |
| `ganchos.py` / `diff-xp.py --sin-ganchos` (109 archivos) | TODO BIEN / 5095, 4828, 229 (lo de la base); Codigo/ no se tocó |

No se repitieron el nivel 0 ni el nivel 1: lo cambiado solo corre con la función «spotify» encendida (la fuente la abre g13 solo con el
modo encendido) y ni Codigo/ ni los ganchos cambiaron.

**Lo que queda:**

- Si Soloist se queda colgado (vivo, con el WebSocket abierto, sin contestar), nada lo nota hasta el plazo de cada canción: la que suena
  acaba a los 5 s de anillo seco y las siguientes a los 15 s sin empezar. No se puso un `KeepAliveTimeout` al WebSocket porque no se sabe si
  Soloist contesta los ping (si no, la rocola se desconectaría cada pocos segundos). Para el A4: mirar si Soloist contesta ping.
- Con Soloist en «buffering» largo a media canción (la red), la canción espera hasta 30 s de silencio (`SeguridadMs`) antes de acabarse:
  R4 solo cuenta el anillo seco en «playing». Si en el A4 un corte de internet deja a Soloist en «buffering», tardará 30 s y no 15.
- Si Soloist manda un «paused» pasajero al empezar una canción (después de que ya dijo «playing» con la pedida), la rocola la daría por
  parada al momento y devolvería el crédito. El falso no lo hace; la documentación no lo dice. Para el A4: que ninguna se acabe sola al
  empezar («acaba ...: se paro» con «sono 0.0 s» en la bitácora).
- Para el A4: la frecuencia con que Soloist abre su flujo (`/run/rocola-xp/spotify/sonido.anillo`, la cabecera en 16) y si lo abre al
  arrancar o con la primera canción; y que la primera canción tras abrir Soloist suene sin chasquidos.
