# Fase 11, grupo letras: buscar por la letra (L3 y L1) y la letra de lo que suena (solo los datos)

20-11-2026. Rama `letras`, sobre `claude/modest-carson-fa2v2n` (`85c931b`). Lo pedido (AGENTS.md, petición del 20-11-2026 «Si haz las dos
y también la maqueta»): **buscar una canción por su letra** en el buscador de XP (decisión del 7-11-2026 «L1 y L3», con **Genius apagado
de fábrica**: encenderlo es decisión del dueño) y **la fuente de la letra de lo que suena** para el estado extendido, **solo los datos**: el
dibujo espera la maqueta y el sí del dueño (la maqueta la hace otro grupo). La lógica de referencia es la del programa anterior
(`sistema/rocola/Motor/BuscaLetras.cs`, sin tocar); aquí se rehace encima de rocola-xp con las reglas de la fase 11. **Nada se probó con
Genius ni LRCLIB de verdad, ni con la biblioteca real, ni en el A4**: todo con MP3 y letras inventadas y servidores falsos (abajo).

## 1. Qué hace

| Archivo (en `RocolaOs/`) | Qué lleva |
|---|---|
| `RocolaOs.Letras.cs` | L3 (la función «letras_mp3»): el repaso de las letras de los MP3, su índice, el gancho g110 (lo de la letra en la lista de XP) y el g111 (elegir la tarjeta NO ESTÁ); el registro de «letras_mp3» y «busca_letra» |
| `RocolaOs.LetrasGenius.cs` | L1 (la función «busca_letra»): el hilo de Genius, la llave, tus canciones, la de Spotify y la tarjeta NO ESTÁ |
| `RocolaOs.LetraSuena.cs` | La letra de lo que suena (la función «letra_cielo»): LRCLIB, la USLT de respaldo, la foto inmutable del verso, la caché en memoria y en `/run`, y la orden `rocola-xp --prueba-letra-cielo` |

### L3, la letra de tus MP3 («letras_mp3», ENCENDIDA de fábrica, automática)

- **El repaso** (`RocolaOsLetrasMp3`): cuando la biblioteca de XP termina de armarse (el latido lo ve: `libBuilding` y el tamaño del índice
  del buscador; y otra vez cada hora), la pantalla le da al hilo `RocolaOs:letras_mp3` (`RocolaOs.Hilo`: nice 19, por debajo del repaso de
  XP) **una copia** de las rutas de los MP3 del índice. El hilo mira cada uno **una vez** (y otra si cambia su tamaño o su fecha), con 20 ms
  entre archivo y archivo, y lee el marco **USLT** del ID3v2.3 o 2.4 (la lectura de `BuscaLetras.cs`: UTF-16 con o sin BOM, UTF-8, Latin-1,
  la desincronización; comprimido no).
- **Lo que se guarda**: `rocola_letras_mp3.txt` en la carpeta de datos, un renglón por archivo, `ruta TAB tamaño TAB fecha TAB palabras`.
  Las palabras son **las distintas**, normalizadas como las normaliza el buscador de XP (`MotorBusqueda.Normaliza`: sin acentos por
  `UnicodeXp.FormaD`, nunca `string.Normalize`), **sin las vacías** (el, la, de, que, the, you…) ni las de una letra, y **en orden
  alfabético**: con ellas no se puede rehacer la letra. **La letra nunca se guarda.** Una letra de menos de 5 palabras no cuenta (un
  comentario). Como el programa anterior, solo se añaden renglones y al arrancar se compacta si sobra más de un tercio.
- **En el buscador** (g110): una frase de **4 palabras o más** (contadas en lo escrito ya normalizado), con **al menos 3 que cuentan**
  (sin las vacías, sin repetir: «los tigres del norte» son 2 y no busca en las letras; revisión del 21-11), cuyas palabras que cuentan
  estén **3 de cada 4** en la letra de una canción (la última puede ir a medio escribir: vale la que empiece así, desde
  3 letras) la encuentra. Va **debajo de todo lo de XP** (lo de XP manda y sale en su orden), sin repetir lo que XP ya dio, como mucho 20, y
  la casilla de XP las cuenta. Con 3 palabras o menos, nada (lo de XP tal cual).

### L1, Genius («busca_letra», APAGADA de fábrica)

- Se enciende con `busca_letra=1` en `rocola_os.txt` (la orden «ajuste» del buzón, o a mano): **decisión del dueño** (las condiciones de
  Genius son de uso no comercial). Encendida, necesita **la llave** en `rocola_genius.txt` (la carpeta de datos de rocola-xp, o la de
  encima, `/rocola/datos`, donde la dejaba el programa anterior). La llave **se lee donde está**, en el hilo de Genius, y **nunca se copia,
  se apunta, se escribe ni se sube**: va solo en la cabecera `Authorization` (no en la dirección). Sin llave, ni una petición (y la
  bitácora lo dice una vez).
- Con una frase de 4 palabras o más, al **dejar de escribir 0,8 s** el hilo `RocolaOs:busca_letra` pregunta a Genius
  (`GET /search?per_page=5&q=…`). De sus resultados, los tres primeros distintos (sin lo de entre paréntesis: «Querida» y «Querida (En
  Vivo)» son una) van **DELANTE** de lo de XP, cada uno:
  1. **tu canción**, si la tienes: el mismo título como suena (normalizado, sin espacios ni paréntesis, con 1 letra de falta en los de 4 a
     6 y 2 en los de 7 o más) y alguna palabra del artista en el de tu canción o el de su disco; es la tarjeta de XP de esa canción;
  2. si no, **la de Spotify** (con el modo Spotify y su llave): la búsqueda «título artista» del cliente del catálogo (su cuota, su freno
     de 429, su caché de la sesión) y la primera canción con ese título; su tarjeta es la de Spotify del buscador (elegirla la pide por
     I6, con sus créditos). Va como **un pedido aparte en la cola del catálogo** (`BuscaAparte`), no como «la última búsqueda» de lo
     escrito: ni la reemplaza lo que se teclea después ni ella reemplaza lo escrito. Si Spotify **no contestó** (sin red, frenando, más de
     10 s), no se sabe si está: esa no sale (tampoco como NO ESTÁ). Lo que g40 trae de Spotify no repite una canción que ya va delante por
     Genius (la misma `spotify:…`);
  3. si no, **una tarjeta «NO ESTÁ EN LA ROCOLA»**: la tarjeta de canción del buscador de XP, con el título que dice Genius y, en el renglón
     del disco, «NO ESTA EN LA ROCOLA» (con el artista en la de la tarjeta). No hay nada nuevo que dibujar: es un `Album` de mentira (la
     carpeta `/::LETRA::/…`, sin palabras ni portada: XP pone las iniciales) en un `SearchItem` de XP. Elegirla (g111) solo lo dice con el
     aviso del buscador de XP («Esa no esta en la rocola»).
- Al llegar la respuesta (el encargo `RocolaOs:busca_letra:llego`), si se sigue mirando esa frase, la lista se rehace con
  `RecomputeResults` de XP y la elegida conserva su sitio. Lo que esperaba sus 0,8 s para otra frase que ya no se mira (se borró una letra
  o una palabra) se olvida y no se pregunta (`Mirando`).
- **Nada se guarda**: lo dicho por Genius vive en memoria la sesión (40 frases), cada frase se pregunta **una sola vez** por sesión (aunque
  fallara), y como mucho 300 preguntas por sesión. Sin red o con Genius caído, nada (el buscador de XP tal cual).

### La letra de lo que suena («letra_cielo», ENCENDIDA de fábrica: solo para pedirla; **nada se dibuja**)

- El latido (en la pantalla) ve que empezó a sonar otra canción (`nowPlaying`; tuya o de Spotify; un video no) y, en cuanto se sabe su
  duración (la del lector, hasta 5 s), le pide su letra al hilo `RocolaOs:letra_cielo`, que busca:
  1. **LRCLIB** (`https://lrclib.net/api/get?track_name=&artist_name=&album_name=&duration=`, sin llave), con el User-Agent
     `rocola-xp/1 (Rocola OS)` (el nombre del programa, nada del dueño): la letra **sincronizada** (`syncedLyrics`, LRC). Si `/api/get`
     no la acepta (400: su documentación pide los cuatro datos) o, sin disco o sin duración, no la encuentra (404), **una búsqueda**
     `/api/search?track_name=&artist_name=`: de su lista, la de duración más cercana (a 3 s o menos si se sabe), mejor sincronizada;
  2. si no, la **USLT del MP3** (sin tiempos; solo si es un MP3 tuyo);
  3. si tampoco, la de LRCLIB **sin tiempos** (`plainLyrics`), si la dio.
- El título va sin el número de pista (`TituloSinPista` de XP), el artista el de la canción (o el de su disco), el disco el de la canción
  (o el nombre del disco de XP) y la duración en segundos; sin disco o sin duración, la petición va sin ellos.
- **Las fotos inmutables** para la pantalla: `RocolaOsLetraSuena.Actual` (la letra: el proveedor, «LRCLIB» o «el MP3», si es sincronizada,
  sus tiempos y sus versos; `RocolaOsLetra.En(pos)` da el verso de cualquier posición) y `RocolaOsLetraSuena.Verso` (la pone cada latido con
  la posición de lo que suena, `PosicionActual` de XP: **el verso que se canta y el siguiente que dice algo**). El LRC se lee con uno o
  varios tiempos por renglón, `[offset:±ms]` y sin las demás etiquetas.
- **Las letras no se guardan**: en memoria (64 canciones) y en `/run/rocola-xp/letras/*.lrc` (lo vivo: se va al apagar; `/run` es
  memoria, así que con **más de 400** letras se quitan las de fecha más vieja hasta **300**, nunca la que se acaba de poner: una rocola
  prendida meses no la llena). El estado,
  `/run/rocola-xp/letras/estado`, lleva **solo números** (qué hay, el proveedor, cuántos versos, el de ahora y el siguiente, las
  peticiones y las fallidas), sin texto de la letra.
- **El tope**: si LRCLIB **contestó** que no la tiene (y el MP3 tampoco), esa canción ya no se pide otra vez en la sesión. Un fallo de la
  red, un 429 o un 5xx frena **2 minutos** cualquier petición, y esa canción **sí** se vuelve a intentar la siguiente vez que suene pasado el
  freno (una petición cada vez que empieza a sonar, nunca en bucle; revisión del 21-11: antes quedaba sin letra toda la sesión, y la rocola
  está prendida semanas y puede arrancar antes que la red). Lo mismo una que se encontró y luego salió de la memoria y de `/run`. La letra
  leída de `/run` se toca, así el recorte quita las menos usadas. Con `ROCOLA_LETRA_API` un LRCLIB falso y con `ROCOLA_LETRA_FRENO_MS`
  otro freno (las pruebas).

**Sin `ROCOLA_OS`** (o con las tres apagadas) los ganchos devuelven lo de XP sin tocar nada (`RocolaOsLetrasResultados` devuelve 0 antes de
normalizar nada; `RocolaOsLetrasElige`, false), no corre ningún hilo, no se lee ni se escribe ningún archivo y no sale ninguna petición: el
buscador de XP bit a bit (la traza, abajo).

## 2. Los ganchos

g110-g119 estaban libres en todas las ramas (`git show RAMA:sistema/rocola/Xp/RocolaOs/GANCHOS.tsv` de todas: `xp-1718-linux` no usa
ninguno; `junta-2` y `navegador-archivos` llegan a g101; `cielo-nucleo` usa g120-g139). Se usaron dos:

| Id | Dónde | De XP | Ahora |
|---|---|---|---|
| g110 | `Codigo/Form1.Biblioteca.cs:3515` (`RecomputeResults`) | `CuentaResultados(query, lastMatches != null ? lastMatches.Count : 0);` | `CuentaResultados(query, (lastMatches != null ? lastMatches.Count : 0) + RocolaOsLetrasResultados(query, res));`: lo de Genius delante y lo de L3 detrás en la misma lista de XP, antes de g40 (que mezcla lo de Spotify encima), y la casilla los cuenta |
| g111 | `Codigo/Form1.Biblioteca.cs:3561` (`ChooseSearchResult`) | `MarkSearchActivity();` | `if (RocolaOsLetrasElige(it)) return; MarkSearchActivity();`: la tarjeta NO ESTÁ solo lo dice (después de g41 de Spotify) |

Y en lo de otro grupo, lo mínimo: `RocolaOs.SpotifyCatalogo.cs` (catálogo) suma `rocolaOsLetrasMetidas` en sus dos `CuentaResultados`
(con el modo Spotify, g40 vuelve a contar la casilla y sin esto decía «Sin resultados» con las tarjetas de la letra a la vista; lo vio la
captura de la escena `genius`).

Una línea cambiada en su sitio cada uno, con su CRLF (`Form1.Biblioteca.cs` mezcla CRLF y LF: cada línea conserva el suyo).
`ganchos.py`: TODO BIEN (112 archivos comparados con 69e6547, 17 con ganchos).

## 3. Las pruebas (`Pruebas/RocolaOs/letras/`)

`bash prueba.sh PROGRAMA [escenas]` (PROGRAMA: la carpeta con `rocola-xp.dll` o `aot:CARPETA`). Con MP3 **inventados** (`hazmp3.py`: un
tono de ffmpeg con su ID3v2.3 y una USLT inventada), un **Genius y un LRCLIB falsos** (`falso.py`, en 127.0.0.1, con `datos.json`: letras y
canciones inventadas, una llave inventada) y, para lo de Spotify, la API web falsa y el Soloist falso del grupo catálogo. La rocola entera
fuera de pantalla con los datos de partida de las escenas (`datos0`) más cinco MP3 de «Los Probados - Disco Letras». Ojo al escribir: las
teclas «c» y «s» son la moneda (67) y saltar (83) en esos datos, así que las frases no las llevan.

| Escena | Qué comprueba |
|---|---|
| `orden` | `--prueba-letra-cielo` (sin la rocola): el tope de `/run` (405 viejas inventadas puestas antes: quedan 300 con la nueva); LRCLIB sincronizada y el verso a −1, 0, 1,99, 2, 5,5, 12,3, 18, 19,9 y 30 s (y el siguiente); de memoria sin otra petición; la petición con título, artista, disco, duración y el User-Agent genérico; `/run` sí y los datos no; otro arranque con el mismo `/run`: 0 peticiones, y la letra leída se toca; `[offset:+500]` y dos tiempos en un renglón; sin disco ni duración: el LRCLIB falso dice 400 (como lo documenta LRCLIB) y la búsqueda `/api/search` la trae; LRCLIB 404 → la USLT del MP3; solo `plainLyrics`; ninguna en ningún lado tres veces = 1 petición; **el freno en una sola corrida** (acortado a 1,5 s): la del 503 frena, otra canción en el freno no pregunta, la del 503 otra vez tampoco, pasado el freno la otra sí pregunta y sale y la del 503 se vuelve a intentar (1 petición más); sin red = nada, 1 petición en tres vueltas, y con USLT, la del MP3 |
| `l3` | El repaso (5 MP3, 3 con letra); «luna de papel brillante»: lo exacto de XP (el título de la 03) primero y la 04 (por su letra) la última; «perro viento frio tarde» (3 de 4) la encuentra; «perro azul viento frio» (2 de 4) no; el archivo solo con palabras sueltas en orden alfabético, sin acentos ni vacías; Genius apagado de fábrica: 0 peticiones |
| `tres` | «viento frio tarde» está entera en la letra de la 05, pero son 3 palabras: no sale |
| `apagado`, `cero` | Sin `ROCOLA_OS`, y con `ROCOLA_OS=1` y las cuatro apagadas: ni el archivo, ni `/run`, ni una petición, nada en la traza; y en `cero`, la traza igual a la de `apagado` quitando lo de RocolaOs (`compara_trazas.py --ignora RocolaOs: --holgura 80`) |
| `genius` | Con la llave falsa y el modo Spotify: UNA pregunta a Genius (con la frase entera y la llave bien en la cabecera, nunca en la dirección); delante, la tuya, la de Spotify (por la búsqueda «título artista») y la NO ESTÁ, en ese orden; la 4.ª de Genius (el remix de la misma) no es otra tarjeta; la llave en ningún archivo de los datos (salvo el suyo), `/run`, la salida ni la traza; nada de Genius guardado |
| `choque` | La de `genius`, y desde 0,75 s después de escribir (Genius ya preguntó) hasta 3,6 s se teclea y se borra una «x» cada 0,12 s: cada tecla es otra búsqueda de lo escrito en Spotify justo cuando el hilo de Genius pide «título artista». Las mismas comprobaciones (salvo «UNA pregunta»: el buscador de XP solo «posa» la consulta al quedarse quieta, así que la de la «x» estuvo a la vista más de 0,8 s y también se pregunta; la frase entera, una vez), y que la de Spotify no sale como NO ESTÁ: la de Spotify sigue en su sitio (con el programa de `117ac6f` salía «noesta:Tema Tema de Mentira» tras esperar 10 s, y Genius preguntaba dos veces: también la frase con la «x», ya borrada) |
| `sinllave` | Encendida sin llave: 0 peticiones, la bitácora lo dice, y la letra de tus MP3 sí |
| `suena` | La rocola toca «Amor de Prueba» (21 s, LRC de 10 versos cada 2 s): UNA petición a LRCLIB con título, artista, disco y duración; la letra sincronizada de LRCLIB; los versos avanzan en orden; el siguiente es el que le sigue; al acabar, nada suena; la letra en `/run` y nada en los datos; el estado sin texto |

**Resultados (20-11-2026, en la máquina compartida, carga de 10 a 13 en 4 núcleos), con el programa de `88b0e80`:**

| Qué | Resultado |
|---|---|
| Compila (JIT, `dotnet build -c Release`) | 0 errores; los **13 avisos de siempre** (SYSLIB0054, SYSLIB0050, CA2022, CA2014; todos de `Codigo/` y `Plataforma/`), ninguno de `RocolaOs/` |
| NativeAOT (`dotnet publish -p:PublishAot=true`) | 0 errores; los mismos 13 avisos; **0 avisos IL/AOT**; el ejecutable de 13,8 MB |
| Batería `letras` en JIT | **69 BIEN, 0 MAL** (orden 23, apagado 5, cero 5, l3 10, tres 3, genius 11, sinllave 4, suena 8) |
| Batería `letras` en NativeAOT | **69 BIEN, 0 MAL** (los mismos). Una corrida anterior (el programa de `4c58187`, sin el tope de `/run`) dio 67 BIEN y 1 MAL en `cero`: solo la cuenta del reloj de 42 ms (`Form1.cs:724`, 354 frente a 450 con holgura 80, con la carga a 13) y un error de sintaxis al final porque `prueba.sh` se editó mientras corría; con el programa nuevo y la misma escena, IGUALES |
| `ganchos.py` | TODO BIEN: 51 ganchos en 19 archivos; `Codigo/` devolviendo los ganchos, 112 archivos iguales a 69e6547 (17 con ganchos) |
| `diff-xp.py --sin-ganchos` | 5 135 quitadas, 4 868 puestas, 229 sin marca: las de la base (49 ganchos devueltos en 17 archivos) |
| Nivel 0 (`nivel0.sh`, sin `ROCOLA_OS`, alternando con la referencia congelada) | previo, código (112 archivos con sus líneas, CRLF y último byte), ganchos y diff-xp **BIEN**; **escena buscador IGUAL en JIT y en NativeAOT**; arranque IGUAL; captura IGUAL (a la segunda: la primera, solo las cuentas del reloj de 42 ms, 336 frente a 306); **moneda MAL por la base, no por esta rama**: la hoja Sonido de Ctrl+F3 trae el botón «Ecualizador...» de los cambios de XP del 17 y 18-11 (69e6547), que la referencia congelada (aef2211) no tiene (`PORT_xp-1718.md` ya lo dice); ningún gancho de esta rama corre sin `ROCOLA_OS` |

## 4. Lo que falta o no se sabe

- **Nada se dibuja de la letra de lo que suena** (a propósito): espera la maqueta y el sí del dueño. Cuando se haga, el dibujo lee
  `RocolaOsLetraSuena.Verso` (o `Actual.En(pos)` con la posición fina).
- **Genius queda apagado**: encenderlo es del dueño (y sus condiciones, de uso no comercial). Sin probar con el Genius de verdad: su
  `/search` se leyó como lo leía el programa anterior (`response.hits[].result.title` y `primary_artist.name`).
- **LRCLIB de verdad no se probó** (sin red aquí: lrclib.net no está en la red permitida de la sesión): `/api/get` se usó como lo documenta
  LRCLIB (404 si no la tiene; `syncedLyrics` y `plainLyrics`; disco y duración obligatorios, de ahí el respaldo de `/api/search`). De
  Spotify la duración sale del lector (si no la da en 5 s, va sin ella y entra el respaldo). Con la biblioteca de XP el artista y el disco salen de las carpetas (`Artista - Disco`); con carpetas sin ese formato el
  artista puede ser otro y LRCLIB no la encontrará (no cuesta más que esa petición).
- **Con el modo Spotify, sus artistas van arriba de las tarjetas de Genius**: g40 pone los artistas de Spotify delante cuando lo primero de
  la lista no es un artista (y las de Genius son canciones). Con una frase de 4 palabras rara vez sale un artista de Spotify (su nombre
  tiene que parecerse a lo escrito), pero puede pasar.
- **Elegir la tarjeta NO ESTÁ no se probó en la rocola entera** (las teclas del buscador con portadas en las pruebas son delicadas); lo que
  hace es una línea (el aviso de XP).
- **El repaso con la biblioteca real y en el A4**: sin medir (20 ms por archivo a nice 19; con 20 000 MP3, unos 7 minutos la primera vez).

## 5. La revisión del 21-11-2026

Un revisor miró la rama. Cada punto se comprobó en el código antes de tocar nada:

| Punto | Qué se vio | Qué se hizo |
|---|---|---|
| GRAVE: la búsqueda de Spotify de Genius (`DeSpotify`) usaba `Busca`, que guarda **una sola** búsqueda pendiente | Cierto. Una tecla en esos 300 ms la reemplazaba sin contestar; el hilo esperaba 10 s y la de Spotify salía como **NO ESTÁ** toda la sesión. Reproducido con la escena nueva `choque` contra el programa de `117ac6f`: «noesta:Tema Tema de Mentira». Se vio además que Genius preguntaba **dos veces** (también la frase con la «x» ya borrada) | `BuscaAparte` (un pedido propio en la cola del catálogo, que siempre contesta); si Spotify no contesta, esa no sale (ni NO ESTÁ); `Mirando` olvida lo que esperaba para una frase que ya no se mira |
| La letra de lo que suena se apuntaba como intentada antes de mirar el freno y la red | Cierto: una canción que sonaba en el freno, sin red o tras salir de la memoria y de `/run` no volvía a tener letra en toda la sesión | Solo queda «sin letra» lo que LRCLIB contestó que no tiene; lo demás se intenta otra vez la siguiente vez que suene pasado el freno; `/run` se toca al leerla |
| La prueba del freno no probaba el freno | Cierto (la segunda corrida era otro proceso y las tres vueltas eran la misma canción) | `--prueba-letra-cielo` con `cancion=` y `pausa=`, `ROCOLA_LETRA_FRENO_MS`, y cinco comprobaciones en una sola corrida (escena `orden`) |
| Duda: LRCLIB pide disco y duración | No se pudo mirar (lrclib.net no está en la red de la sesión); su documentación los da por obligatorios | Si `/api/get` dice 400, o 404 sin disco o sin duración, `/api/search` por título y artista (la duración más cercana, a 3 s). El LRCLIB falso ahora contesta 400 sin ellos |
| L3 con 2 palabras que cuentan | Cierto («los tigres del norte» pedía 2 de 2) | Al menos 3 que cuentan (`PalabrasQueCuentan`); Genius sigue con la frase de 4 palabras |
| g40 podía repetir la canción de Spotify que Genius ya puso delante | Cierto (objetos distintos, mismo `spotify:`) | g40 se salta las canciones cuya `spotify:` ya va en la lista |
| Duda: el coste de `Busca` en cada tecla | La última palabra a medio escribir recorría **todo** el índice | Las palabras del índice en orden y la búsqueda por mitades; sin medir con una biblioteca real |
| Genius con las frases a medio escribir (con los botones de la rocola, cada palabra nueva es una frase) | Cierto, pero menos grave de lo que parece: cada frase cuesta 1 a Genius (tope 300 por sesión) y lo de Spotify **se recuerda por consulta** (mismo «título artista» = 0 peticiones) | No se cambió (apagado de fábrica; decidir cuánto esperar es del dueño si lo enciende) |
| Los artistas de Spotify encima de las tarjetas de Genius | Ya estaba en §4 | Sin cambio |
| `rocola_letras_mp3.txt` guarda para siempre los MP3 que ya no están | Cierto; solo palabras sueltas, crece poco | Sin cambio (quitarlos al compactar, sin la biblioteca a la vista, borraría lo de un disco desconectado) |
| USLT del ID3v2.3 con desincronización de toda la etiqueta | Cierto: las cabeceras de los marcos no se desincronizan; da null, nada se rompe | Sin cambio |
| El repaso sin `ionice` | Con los planificadores CFQ/BFQ la prioridad de disco sale del nice (19 = la más baja de las normales); con mq-deadline no cuenta | Sin cambio; sin medir en un disco duro |
| Elegir la tarjeta NO ESTÁ | Los caminos de XP que se miraron comprueban null | Sigue sin probarse en la rocola entera (ya estaba en §4) |
