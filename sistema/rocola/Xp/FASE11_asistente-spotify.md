# Fase 11, Spotify: el asistente explica lo nuevo y «AYUDA» siempre (grupo «asistente-spotify»)

Junta de Spotify para el cumpleaños (15-11-2026). Hace los puntos 7 y 8 de `SPOTIFY_JUNTA_PENDIENTES.md`, las dos peticiones del dueño
del 15-11-2026 (AGENTS.md):

- «también tendrás que ajustar el asistente con nuevas instrucciones, ya que estamos metiendo nuevas cosas»: con el modo Spotify, el
  asistente estrella explica lo nuevo de Spotify **con sus mismas guías y su mismo dibujo**; sin el modo, es el de XP tal cual.
- «La casilla del asistente debe decir ayuda»: con Rocola OS activo, la casilla de arriba dice **siempre «AYUDA»**, también en 1024x768.

Rama `asistente-spotify`, sobre `junta-spotify` (a744bf4) con las ramas `paredes-buscador` y `sustitucion-spotify` juntadas al empezar
(dos merges `--no-ff`; chocaron `GANCHOS.tsv`, la API falsa, el Soloist falso y este archivo de pendientes: se quedaron las dos partes; en
la API falsa los artistas y discos de la sustitución pasaron a `art80`/`art81` y `dis080`-`dis082` porque `art50` ya era «Banda
Interminable» de las paredes). Archivos nuevos: `RocolaOs/RocolaOs.Asistente.cs` (lo que piensa), `RocolaOs/RocolaOs.AsistentePinta.cs`
(lo que dibuja: el dibujo de XP) y `Pruebas/RocolaOs/asistente/prueba.sh`. Ganchos **g70-g84** (15 líneas en 4 archivos de `Codigo/`).

**Nada de esto se probó en el A4, con el Soloist de verdad (nunca se ejecutó) ni con la cuenta real**: todo fuera de pantalla (llvmpipe,
1024x768) con la API falsa, el Soloist falso, datos inventados y las letras de prueba de la máquina (DejaVu, más anchas que Trebuchet MS:
en el A4, con las letras del disco de XP, los renglones caben mejor).

## 1. Lo que ve el cliente

### La pregunta de la estrella

Con el modo Spotify encendido (la función «spotify», de fábrica) **y la llave de la API** (`rocola_spotify_api.txt` con `client_id` y
`client_secret`), ROJO + ABAJO (o la casilla AYUDA) abre la misma pregunta de XP con **una cuarta opción**:

| | |
|---|---|
| Poner una canción | (la de XP) |
| Buscar una canción por su nombre | (la de XP) |
| Aprender a moverme | (la de XP) |
| **Canciones de Spotify** | abre otra pregunta igual, «CANCIONES DE SPOTIFY · ¿Qué quieres saber?», con el texto «Se piden con los mismos botones y cuestan 1 crédito, como las tuyas. Sin créditos no se pide.» y tres guías. Su leyenda dice «volver»: el ROJO regresa a la primera pregunta con «Canciones de Spotify» elegida (otro ROJO la guarda, como XP) |

Las cuatro opciones van un poco más juntas que las tres de XP (desde el 47 % del alto del globo, cada 11,2 %, de 10 % de alto; XP: 50 %,
13,5 %, 11,5 %) para que la cuarta quepa sobre la leyenda; el mismo margen, el mismo ancho, los mismos controles dibujados en la elegida
(flechas rojas y el VERDE que late y se desliza de una a otra), el mismo vidrio, letras y colores. Con el ratón, tocar o pasar por encima
de la cuarta también la elige.

### Las tres guías (como las de XP: miran lo que pasa de verdad y deciden el paso)

Cada paso: título corto, una frase y los botones dibujados, como `TextosGuia` de XP; la estrella señala con la mano y la flecha dorada la
pieza de la pantalla, con el marco dorado que respira; «PASO N DE M» (la moneda solo cuenta si hace falta), «¡MUY BIEN! SIGUIENTE PASO» al
avanzar, «SIGAMOS» si se salió del camino, y al lograrlo «¡LO LOGRASTE!» y se guarda sola (lo de XP: 2,6 s).

| Guía | Pasos (lo que señala) | Se logra |
|---|---|---|
| **1. Pedir una canción de Spotify** | Abre el buscador (la casilla BUSCAR; «ahí salen también las canciones de Spotify») → Escribe el nombre (el teclado; «las de Spotify salen después de tus primeras, con su portada») → en los resultados, según la tarjeta elegida (la tarjeta): una tuya, «Busca las de Spotify»; una canción de Spotify, «Pídela con VERDE» («se pide como las tuyas y cuesta 1 crédito»); un disco o lista, «Un disco de Spotify»; un artista que solo está en Spotify, «Un artista de Spotify» («verás sus discos en su propia pared»); «más en Spotify»; «Todos los discos de…» → **sin créditos, «Mete una moneda: sin créditos no se pide»** (los créditos) → dentro de un disco de Spotify, «Elige tu canción» (la fila elegida) | una canción pedida («¡Tu canción está en la cola! Es de Spotify y suena como las tuyas») |
| **2. La letra ♫ de Spotify** | Ve a la letra ♫ (la barra de letras; ROJO + IZQ/DER) → Elige un disco o una lista (el disco elegido) → la moneda → Elige tu canción. Sin la letra ♫ (sale con la cuenta de Spotify, `refresh_token`): «Aún no hay letra ♫ … búscalas: ROJO + ARRIBA» (la casilla BUSCAR) y sigue como la guía 1 | una canción pedida |
| **3. Los discos de un artista** | Abre el buscador → Escribe el artista («si solo está en Spotify sale su tarjeta; si ya lo tienes, detrás sale «más en Spotify»») → la tarjeta (artista, «más en Spotify», «Todos los discos de…»; en otra, «Busca su tarjeta») → su pared: en la primera casilla «Sus canciones» («La primera casilla trae sus canciones. VERDE abre; el ROJO te regresa al buscador»), en las demás «Su pared de discos» (ARRIBA/ABAJO cambian de página) → en un disco de la pared, «Vuelve con ROJO» (a su pared, en la misma casilla) → otra vez en la pared, «Vuelve al buscador» (la cabecera, que dice el nombre del artista) | volver al buscador desde la pared con REGRESAR («¡Ya sabes ver sus discos! El ROJO siempre te regresa») |

La estrella sigue **sin cara** (nada de esto la toca: solo sus gestos de siempre, los de XP para cada paso).

### Sin el modo Spotify o sin la llave de la API

**El asistente de XP tal cual**: tres opciones, sus misiones, su dibujo. Cada gancho devuelve lo de XP; si va lo de Spotify se mira cada vez
que se abre la ayuda (encender el modo o poner la llave vale desde la siguiente vez que se abre). Sin `ROCOLA_OS` no se lee nada.

### «AYUDA» siempre en la casilla de arriba

Con Rocola OS activo (`ROCOLA_OS` puesta, con o sin Spotify), la casilla de la ayuda dice «AYUDA» junto a su signo también en 1024x768:
XP, antes de dejarla compacta, deja que la cabecera ceda hasta un 15 % de lo que pide; con Rocola OS **cede lo que haga falta mientras le
queden 90 px** (lo mínimo con que XP deja la casilla; con menos XP la esconde entera y se seguiría el camino de XP). La cabecera se encoge
como ya lo hace XP en pantallas angostas (su nombre de medio en medio punto; el número de página, corto). En 1024x768 con la letra de
fábrica: la cabecera dice «LETRA L» entera. Con video (BUSCAR y AYUDA encima del video, `AcomodaSobreVideo`) en 1024x768 ya cabía la
palabra (unos 400 px de los ~500 de la mitad derecha): no hizo falta tocarlo. **Sin `ROCOLA_OS`, como XP** (compacta en 1024x768).

## 2. Los ganchos (g70-g84; `RocolaOs/GANCHOS.tsv`)

| Id | Archivo:línea | Para qué |
|---|---|---|
| g70 | `AsistenteRocola.cs` 264 (`SeleccionaAsistente`) | cuántas opciones: 4 en la primera pregunta con Spotify, 3 en la de Spotify (`RocolaOsAsistenteOpciones`; sin Spotify, `AsistenteOpciones` de XP) |
| g71, g72 | `AsistenteRocola.cs` 285 y 295 (`AsistenteClick`, `AsistenteMueveRaton`) | el toque y el ratón llegan a la cuarta |
| g73-g75 | `AsistenteEstrellaGpu.cs` 176, 201 y 202 (el latido del VERDE que se desliza) | el latido llega a la cuarta |
| g76 | `AsistenteRocola.cs` 338 (`AsistenteOpcionRect`) | con cuatro, un poco más juntas |
| g77 | `AsistenteRocola.cs` 274 (`EligeAsistente`) | «Canciones de Spotify» abre la de Spotify; en ella, cada opción empieza su guía |
| g78 | `AsistenteRocola.cs` 212 (`AsistenteEntrada`, en la pregunta) | ROJO en la de Spotify vuelve a la de XP |
| g79 | `AsistenteRocola.cs` 322 (`EnsureAsistentePanel`, la página) | al abrirse mira si va Spotify; la pregunta con Spotify es la página 40 o 41 (se hornea otra vez al cambiar) |
| g80 | `AsistenteRocola.cs` 328 (`EnsureAsistentePanel`, el dibujo) | el globo de la pregunta con Spotify (`RocolaOsPintaPregunta`: `PintaPanelAsistente` de XP copiada línea por línea con los textos y las opciones de parámetro) |
| g81 | `AsistenteGuiaRocola.cs` 95 (`EvaluaGuia`) | el latido de una guía de Spotify |
| g82 | `AsistenteGuiaRocola.cs` 160 (`ObjetivoGuia`) | un paso de Spotify señala lo mismo que el paso de XP que señala esa pieza |
| g83 | `AsistenteGuiaRocola.cs` 247 (`PintaGuiaActual`) | el globo de una guía de Spotify: `PintaGuiaAsistente` de XP con sus textos |
| g84 | `AyudaCasillaGpu.cs` 160 (`AcomodaAyuda`) | «AYUDA» siempre: la cabecera cede hasta 90 px |

Las misiones de Spotify son 4, 5 y 6 y sus pasos 40 a 59 (XP: misiones 1-3, pasos 1-12): números que XP nunca tiene. Lo de la pregunta va
en estáticos porque lo usan funciones estáticas de XP (`AsistenteOpcionRect`, `AsistenteVerdeEn` de la tarjeta); solo los escribe la
pantalla, y la tarjeta dibuja en el mismo hilo. La bitácora de Rocola OS (`rocola_os_bitacora.txt`, `[asistente]`) apunta cuándo la ayuda
sale con Spotify, la pregunta de Spotify, cada guía, cada paso nuevo y el logro (lo leen las pruebas). **Peticiones a la API: ninguna** (el
asistente solo mira lo que ya está en memoria).

## 3. Las pruebas (15-11-2026)

JIT = `dotnet build -c Release`; AOT = `dotnet publish -c Release -r linux-x64 -p:PublishAot=true`. La máquina, compartida con otros
grupos (carga de 7 a 11 en 4 núcleos).

| Prueba | Resultado |
|---|---|
| Compilar, JIT | 0 errores; los **13 avisos de siempre** (los de `avisos-base.txt`) |
| Publicar NativeAOT | sale con 0; **0 avisos IL/AOT**; los mismos 13 del compilador |
| `ganchos.py` | **TODO BIEN**: 49 ganchos en 19 archivos (47 en 17 de `Codigo/` y 2 en `Nativo/`); `Codigo/` = `fc7b667` devolviéndolos |
| `diff-xp.py --sin-ganchos` (109 archivos) | **5 095 / 4 828 / 229**, las de la base; 47 ganchos devueltos en 17 archivos |
| `Pruebas/RocolaOs/asistente/prueba.sh` (9 escenas) | **JIT 26 de 26** y **AOT 26 de 26** (abajo) |
| Arnés `VistasAsistente` (la copia contra la referencia congelada, con `RocolaOs/` compilado) | **IGUALES** con las letras falsas y con DejaVu: 2 043 líneas, 777 llamadas directas a la traducida |
| Nivel 0 (`nivel0.sh`, sin `ROCOLA_OS`, contra la referencia) | VER_NIVEL0 |

Las escenas de `asistente/prueba.sh` (las teclas con `ROCOLA_PULSA`; la ayuda con ROJO mantenido 1 s y ABAJO a los 0,3 s):

| Escena | Qué comprueba |
|---|---|
| `menu` | con la llave, la pregunta de cuatro; ABAJO x4 se queda en la cuarta; VERDE, la de Spotify; ABAJO x4 se queda en la tercera; ROJO vuelve a la primera (la misma captura salvo el latido del VERDE, menos de 5 000 puntos frente a 47 369 de elegir otra) y ROJO la guarda; ninguna guía empezó |
| `pide` | guía 1 con «inventad» y sin créditos: los pasos «Abre el buscador › Escribe el nombre › Un artista de Spotify › Mete una moneda › Un artista de Spotify › Mete una moneda › Pídela con VERDE»; **VERDE sin crédito no pidió** (0 COBRADA); con la moneda, 1 COBRADA y «lo logró (pidió una de Spotify)» |
| `letra` | guía 2 con la cuenta: «Ve a la letra ♫ › Elige un disco o una lista › Mete una moneda › Elige tu canción», 1 COBRADA y el logro |
| `artista` | guía 3 con «interminable»: «Abre el buscador › Escribe el artista › Un artista de Spotify › Sus canciones › Su pared de discos › Vuelve con ROJO › Vuelve al buscador», el logro al volver al buscador y 0 COBRADA |
| `tarjetas` | guía 1 con «mentira» y crédito: la canción («Pídela con VERDE», sin la moneda), el disco, «Todos los discos» y de vuelta |
| `mas` | guía 3 con «otra»: tu artista («Busca su tarjeta») y «Más en Spotify» |
| `xp` y `sinllave` | **sin Spotify = XP**: la misma secuencia (la pregunta, ABAJO x4, «Aprender a moverme», sus pasos, ROJO+ABAJO, otra vez la ayuda y «Poner una canción») sin `ROCOLA_OS` y con `ROCOLA_OS=1` sin la llave (el modo encendido de fábrica): **la traza igual** (`compara_trazas.py --ignora RocolaOs:`, holgura 80: IGUALES en JIT y en AOT), ni una petición a la API, nada `[asistente]` en la bitácora, y **los globos que la ayuda subió a la tarjeta, punto por punto, los mismos 10 y en el mismo orden** (el volcado de texturas, `ROCOLA_VOLCADO_TEXTURAS`: la pregunta, cada paso de la guía, el adiós). Las capturas de la pantalla no son iguales y no se piden iguales: la casilla de arriba dice AYUDA con Rocola OS (lo pedido), la flecha que señala la cabecera la señala con su nuevo ancho, y lo que late o se mueve (el marco dorado, la flecha que bota, la estrella) cae en otra fase |
| `ayuda1024` | la fila de arriba a 1024x768 con y sin `ROCOLA_OS`: con, «? AYUDA ● + ▼»; sin, «? ● + ▼» (compacta, la de XP) |

Una nota de la prueba: en AOT la primera comparación de `sinllave` dio «solo cuentas» (el reloj `Form1.cs:724` 366 frente a 463 y el
encargo `MedidasBitacora.cs:129`), dos veces en el mismo sentido. Era la prueba: el vigilante FALSO del núcleo abre el Soloist falso (otro
`dotnet`) aunque no haya llave y le quitaba CPU a la rocola (el de verdad, sin llave, solo espera). Con un vigilante que solo espera (lo que
hace el de verdad sin llave), IGUALES.

Capturas (PNG, 1024x768) en el scratchpad de la sesión, `asistente-capturas/`: la pregunta (`menu-*`), cada paso de las tres guías
(`guia1-*`, `guia2-*`, `guia3-*`), la fila de arriba con y sin Rocola OS (`fila-*-arriba.png`) y las de `xp-*`/`sinllave-*`.

## 4. Lo que queda abierto

- **Nada probado en el A4**, con el Soloist de verdad ni con la cuenta real. Con las letras de XP (Trebuchet MS) los textos caben en menos
  renglones que con DejaVu; el «¡Tu canción está en la cola!» de XP se corta en dos renglones con DejaVu (igual que en XP con esas letras).
- La ♫ en «La letra ♫ de Spotify» y «Ve a la letra ♫» se dibuja con la letra del globo (Trebuchet MS en el A4): si esa letra no la trae, se
  vería un cuadro; con DejaVu sale bien. Mirarlo en la prueba corta del A4 (si sale un cuadro, se cambia el texto por «de Spotify»).
- Con un ancho menor que 1024 donde a la cabecera no le quedan 90 px, la casilla sigue compacta (el camino de XP); en el bar (1024x768) no
  pasa.
- Es visual en lo que añade (una opción y textos) con el dibujo de XP, como pidió el dueño; la presentación del final puede proponer algo
  más (p. ej. el zoom o una marca de Spotify en la guía).
