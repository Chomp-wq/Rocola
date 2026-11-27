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
| `RocolaOs.Letras.cs` | L3 (la función «letras_mp3»): el repaso de las letras de los MP3, su índice, el gancho g114 (lo de la letra en la lista de XP) y el g115 (elegir la tarjeta NO ESTÁ); el registro de «letras_mp3» y «busca_letra» |
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
- **En el buscador** (g114): una frase de **4 palabras o más** (contadas en lo escrito ya normalizado), con **al menos 3 que cuentan**
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
     carpeta `/::LETRA::/…`, sin palabras ni portada: XP pone las iniciales) en un `SearchItem` de XP. Elegirla (g115) solo lo dice con el
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
ninguno; `junta-2` y `navegador-archivos` llegan a g101; `cielo-nucleo` usa g120-g139). Se usaron dos, g110 y g111. **En la junta 3
(21-11-2026) se renumeraron a g114 y g115**: `xp-1718-linux` habia tomado despues g110-g113 y entro antes (FASE11_JUNTAS.md, «Junta 3»):

| Id | Dónde | De XP | Ahora |
|---|---|---|---|
| g114 | `Codigo/Form1.Biblioteca.cs:3515` (`RecomputeResults`) | `CuentaResultados(query, lastMatches != null ? lastMatches.Count : 0);` | `CuentaResultados(query, (lastMatches != null ? lastMatches.Count : 0) + RocolaOsLetrasResultados(query, res));`: lo de Genius delante y lo de L3 detrás en la misma lista de XP, antes de g40 (que mezcla lo de Spotify encima), y la casilla los cuenta |
| g115 | `Codigo/Form1.Biblioteca.cs:3561` (`ChooseSearchResult`) | `MarkSearchActivity();` | `if (RocolaOsLetrasElige(it)) return; MarkSearchActivity();`: la tarjeta NO ESTÁ solo lo dice (después de g41 de Spotify) |

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

**Resultados tras la revisión (21-11-2026, máquina compartida), con el programa de `a62ebeb` (y las pruebas de `0909f79`):** compila en JIT
y en NativeAOT con 0 errores y los 13 avisos de siempre (ninguno de `RocolaOs/`), **0 avisos IL/AOT**, el ejecutable de 13,8 MB. La batería
`letras`: **88 BIEN, 0 MAL en JIT y 88 BIEN, 0 MAL en NativeAOT** (orden 30, apagado 5, cero 5, l3 10, tres 3, genius 11, choque 12,
sinllave 4, suena 8). Con el programa de antes (`117ac6f`), la escena `choque` dio 8 BIEN y 3 MAL (la de Spotify como NO ESTÁ y dos
preguntas a Genius), y en `orden` las del freno, la del respaldo `/api/search` y la de tocar `/run` fallaron (9 MAL). `ganchos.py`: TODO
BIEN (112 archivos, 17 con ganchos; esta revisión no toca `Codigo/`). El nivel 0 no se volvió a correr: nada de lo cambiado corre sin
`ROCOLA_OS` (los ganchos devuelven antes de llegar a ello), y la escena `apagado` y la `cero` dan lo mismo que antes.

## 6. La letra en el cielo del reposo (K6, maqueta 3), 27-11-2026

> **SUSTITUIDA por la §7 (mismo día):** el dueño pidió que la letra se vea como la de Apple Music; ya no hay dos renglones centrados, ni llenado de ámbar,
> ni lavanda. Lo que sigue se deja como historia de la maqueta 3 y de las pruebas que la acompañaron.

Rama `worktree-agent-aaccd699f49f1d6eb`, encima de `0461bf0` y unida con `claude/modest-carson-fa2v2n`. Es lo que la §1 dejó «solo los datos»: **dibujar**
la letra de lo que suena, **como la aprobó el dueño** (decisión del 20-11-2026, «Me gusta así la letra», maqueta 3; las maquetas se perdieron y esto sigue
su descripción). Solo Rocola OS, solo la tarjeta (GPU), con **la misma función `letra_cielo=`** (encendida de fábrica: la que ya pedía la letra; apagada,
ni se pide ni se dibuja nada). La fuente no se tocó: se lee `RocolaOsLetraSuena.Actual`.

### 6.1 Qué se ve

En el **reposo con música**, en el cielo libre entre la estrella y la aurora (el cielo sobre la raya del horizonte, donde empieza el panel AHORA SUENA),
dos renglones centrados: el central a `StatusTopY - 0,115 x alto` (435 de 768) y el que sigue 0,062 x alto más abajo.

| Pieza | Cómo es |
|---|---|
| El que se canta | letra de los títulos de XP (Trebuchet MS en negrita, 0,050 x alto = 38 px), blanco con sombra suave (una nube desenfocada y un filo), que **se llena de ámbar `#FFCE78` de izquierda a derecha al compás** de la canción, con **borde suave de 10 px** |
| El que sigue | más chico (0,033 x alto), **lavanda `#D6CCFF`**, con la misma sombra |
| Al cambiar de verso | 0,3 s: **todo sube un renglón** (el que se cantaba sale arriba y se apaga, el que seguía se hace grande en su sitio, entra otro abajo) |
| «Letra: LRCLIB» | chiquito, abajo a la derecha del cielo, sobre el panel («Letra: el MP3» si es la del MP3) |
| Tramos sin voz | un renglón vacío del LRC (o un verso que dura más de 14 s y ya se llenó 4 s antes): todo se apaga en 0,3 s y queda **el cielo de XP tal cual**; vuelve con el siguiente verso |
| La del MP3 (sin tiempos; maqueta 4) | los renglones con texto se **reparten por la duración de la canción** y suben despacio (el cambio ocupa el último 22 % de cada renglón: ~2 s con 10 renglones en 100 s), **sin llenarse**; es un repaso aproximado |
| Dónde NO sale | pared, buscador, disco abierto, ayuda, Ctrl+F3/Ctrl+F4, atracción ni video: solo con el reposo abierto (`idleBlend`, aparece de 0,55 a 1); si se abre uno de esos con el reposo aún a la vista, se va en 0,15 s |

**El llenado «al compás»**: el renglón i se llena linealmente de su tiempo al del siguiente renglón del LRC (el último, hasta el final o 6 s), pero nunca en
más de 10 s (`LlenaMaxS`); la posición es la que se oye (`PosicionActual` de XP: cuenta el colchón de la salida y el salto de silencios; con Spotify, la de
su fuente), medida como mucho cada 50 ms y suavizada entre una medida y otra. El LRC de LRCLIB es de línea (sin tiempos por palabra): el llenado es parejo
por todo el renglón.

**La calma (regla 5 del cielo vivo), decidido: la letra NO se atenúa ni se detiene con la calma.** Es contenido, no adorno; y no hace falta: el reposo solo
está abierto cuando nadie toca nada (15 s) y en cuanto se toca un botón se cierra y la letra con él.

### 6.2 Cómo está hecho

| Archivo (en `RocolaOs/`) | Qué lleva |
|---|---|
| `RocolaOs.LetraCielo.cs` | `RocolaOsLetraModelo` (las cuentas puras: llenado, voz, escalera del cambio, reparto del MP3), `RocolaOsLetraHornea` (un renglón = un mapa de bits blanco con su sombra negra: **el color lo pone el color del vértice**), `Form1` (lo que XP sabe: ruta, posición, si algo está abierto) y `GpuPanel` (estado, horneado y dibujo) |
| `RocolaOs.LetraCieloPrueba.cs` | la orden `rocola-xp --prueba-letra-cielo-modelo` (17 comprobaciones sin pantalla) |

- **Independiente del cielo vivo**: en esta versión sus cuatro funciones van apagadas de fábrica (`Va` falso), así que la letra **no pasa por sus capas**
  (`PonPintor`): tiene sus dos ganchos. Con el cielo vivo encendido convive igual (probado: escena `cielo`).
- **Se hornea una vez por renglón, en Prepara** (g381, fuera del cuadro: Direct3D no deja crear texturas con la escena abierta), **como mucho dos mapas por
  cuadro**; por renglón hay dos (el grande y el chico); se sueltan los lejanos y todos al irse el reposo.
- **El ámbar son vértices**: lo llenado con el color ámbar, lo que falta con el blanco, y en los 10 px del borde las dos capas con alfas contrarios. Ninguna
  memoria nueva por cuadro (los vértices son un arreglo fijo). La sombra es negra en el mapa y el color del vértice no la tiñe.
- Un renglón largo se achica hasta el 60 % para caber en 0,86 del ancho; si aún no cabe, se corta con puntos.
- Para las pruebas: `ROCOLA_LETRA_LOG=archivo` (una línea cada 50 ms con la letra a la vista: hora de pared, posición, verso, llenado, `u`, voz, idle) y
  `ROCOLA_LETRA_CAMBIO_S` (alarga el cambio de verso: cada captura para el cuadro casi un segundo y de 0,3 s no se vería nada).

### 6.3 Ganchos (rango g380-g389; se usaron dos)

| Id | Dónde | De XP | Ahora |
|---|---|---|---|
| g381 | `Codigo/D3D9Star.cs` (`RenderFrame`, tras `StepIdle`) | `idleBlend = StepIdle(...);` | `... RocolaOsLetraPrepara();`: el estado y los horneados de la letra, antes de abrir el cuadro |
| g380 | `Codigo/D3D9Star.cs` (`RenderFrame`, tras el buscador con portadas) | `PiezasConLaVentana(gw, uiPropia);` | `... RocolaOsLetraPinta();`: los dos renglones, antes de la interfaz de XP del pie y del panel del reposo |

`ganchos.py`: TODO BIEN (112 archivos, 34 con ganchos). En el merge con la rama principal el único choque fue `GANCHOS.tsv` (dos ramas añadieron al final): se
dejaron las filas de las dos.

### 6.4 Las pruebas (`Pruebas/RocolaOs/letra-cielo/`, `bash prueba.sh PROGRAMA [escenas]`)

La rocola entera fuera de pantalla (llvmpipe, 1024x768) con la tarjeta de sonido de prueba con reloj, MP3 **inventados** (los de `../letras/hazmp3.py`), un
**LRCLIB falso** (`../letras/falso.py`) y letras **inventadas** (`datos.json`; nunca letras ni fuentes de verdad). El LRC: 8 s, 16 s, un verso largo de 24
a 34 s, **un tramo sin voz de 34 a 44 s**, 44, 52 y 60 s. (Las capturas están fuera del repositorio.)

| Escena | Qué comprueba |
|---|---|
| `ganchos` | `ganchos.py` TODO BIEN y g380-g381 en el manifiesto |
| `modelo` | el llenado a su tiempo (error 0 cada 10 ms), el tope de 10 s, la voz (vacío, verso largo), la escalera del cambio, el reparto del MP3 |
| `compas` | la posición que usa la letra avanza al paso del reloj (razón 0,998); en cuatro instantes de la canción el **ámbar que se ve** avanza como lo que dice la canción (a 0,05 entre capturas) y va retrasado siempre lo mismo (0,03 a 0,08 del renglón, ~0,7 s: lo que tarda en escribirse el PNG, que para el cuadro); en el cambio de verso (3 s en la prueba) los renglones suben (altura media de la tinta 452 a 424 px); en el tramo sin voz no hay letra; regla 3 con el cielo de XP: blanco 7,8 a 1, ámbar 5,3, lavanda 5,2 |
| `mp3` | la letra es la del MP3 (el LRCLIB falso dijo 404), `llena=-1` siempre, sin ámbar, los renglones cambian entre 44, 56 y 68 s |
| `nada` | con el reposo abierto la letra se ve; tras tocar (pared), abrir el buscador, un disco o la ayuda ya no hay reposo abierto para ella |
| `cielo` | la regla 3 con **cielo, fiestas y clima encendidos**: de día con nubes (blanco 6,8; ámbar 4,7; lavanda 4,5), el 20-11 (fiesta) con lluvia (6,1 / 4,2 / 4,1), al anochecer (6,8 / 4,7 / 4,5) y de noche (7,2 / 4,9 / 4,8) |
| `apagado` | con `letra_cielo=0` la forma de los cuadros de la tarjeta y sus dibujos de textura por cuadro (5,00) son los de la rama sin la letra; con ella, 10,00 (control); sin `ROCOLA_OS`, ni log ni archivos |
| `reloj` | p50/p95/p99/max del trabajo de cada cuadro (`ROCOLA_CIELO_RELOJ`, NativeAOT, 70 s): **con la letra 1,62 / 2,39 / 3,24 / 11,8 ms; sin ella 1,67 / 2,53 / 4,67 / 228,8 ms** (3 328 y 2 791 cuadros; máquina compartida y cargada: el máximo de «sin» es un tropiezo de la máquina) |

### 6.5 Lo que no se sabe

- **Nada en el A4, ni con LRCLIB de verdad, ni con letras de canciones de verdad.** La sincronizada de LRCLIB es de línea: el llenado es parejo por renglón y
  puede ir un poco adelantado o atrasado según el LRC.
- La posición con **Spotify** sale de la fuente de Spotify (`CurrentTime` de `SpotifyFuente`, que `PosicionActual` ya usa): no se probó con Soloist real.
- Se probó con las cuatro funciones del cielo vivo encendidas y fecha/clima forzados, no con las ideas de vidrio de otras ramas (VI1 a VI6).
- El contraste se midió con la letra de repuesto de la máquina de pruebas (Trebuchet MS cae a DejaVu Sans Bold sin las letras del disco de Windows): en el A4
  la forma de las letras cambia un poco, no la sombra ni los colores.
- El horneado cuesta 1 a 2 ms por mapa (dos por cuadro como mucho): al cambiar de canción el primer renglón puede verse un cuadro más tarde (no medido en el A4).

## 7. La letra en el cielo del reposo, como la de Apple Music (K6 rehecho), 27-11-2026

Petición del dueño de hoy, con una captura de Apple Music en el teléfono: «Te dije que la letra de las canciones debía verse como las de Apple Music».
**Sustituye la §6** (la maqueta 3: dos renglones centrados que se llenan de ámbar, el siguiente en lavanda; ya no hay ámbar ni lavanda). Lo demás de la §6
sigue: solo Rocola OS, solo la tarjeta (GPU), la misma función `letra_cielo=` (encendida de fábrica), la fuente (`RocolaOsLetraSuena.Actual`), la posición que se
oye (`PosicionActual`), el reposo abierto (`idleBlend`) y dónde no sale, y «Letra: LRCLIB» / «Letra: el MP3».

### 7.1 Qué se ve

| Pieza | Cómo es |
|---|---|
| La columna | varios versos (el anterior arriba y los que siguen abajo, 5 o 6 según quepan) **alineados a la izquierda**, en la letra de los títulos de XP en negrita (Trebuchet MS, 0,052 x alto = 40 px en 1024x768), con aire entre versos (medio renglón). Un verso largo **se parte en renglones** (hasta 4; si sobra, el último termina en puntos), no se achica |
| Dónde | de `x = 224` a `x = 978` y de `y = 108` a `y = 488` en 1024x768 (todo en proporción: `LetraGeometria`). El borde izquierdo es **a la derecha de la estrella en el extremo izquierdo de su recorrido**: su centro mínimo (`mx` de `StarTravelCenter`, 117) más 1,5 radios (el de `DrawDetailedMoon` en el reposo, 71). Abajo, 0,045 x alto sobre el panel del reposo **con el reposo del todo abierto** (la columna no se mueve mientras el panel sube); arriba, en 0,14 x alto |
| La estrella | la estrella de XP **recorre todo el cielo**, no solo la izquierda (`StarTravelCenter`), así que el borde izquierdo no basta: donde la estrella esté en ese cuadro, la letra se **atenúa a 0,2** en un círculo de 1,25 radios (con borde suave desde 0,8 radios; las celdas son de 24 px), para no taparla. No se atenúa si la estrella está lejos |
| El verso que se canta | **blanco opaco** con la sombra suave de antes (una nube y un filo, regla 3), hacia el tercio de arriba de la columna (a 0,27 de su alto) |
| Los demás | **blanco translúcido** (0,38 el siguiente, un poco menos cuanto más lejos hasta 0,30) y **un poco desenfocados**, más cuanto más lejos (mezcla de 0,45 a 1 con el mapa desenfocado). El anterior se queda arriba a 0,34 y se apaga más arriba; todo se **desvanece por los bordes** de arriba y de abajo de la columna (15 % del alto de la columna, suave, sin cortes) |
| Al cambiar de verso | la columna entera **sube con un resorte crítico** (sin rebote, ~0,55 s, `5,9 / 0,55` de frecuencia), con **35 ms de retraso por verso hacia abajo** (los de abajo llegan un poco después); el nuevo pasa de translúcido a opaco, de desenfocado a nítido y **crece de 0,96 a 1 desde su borde izquierdo**, todo porque alfa, desenfoque y tamaño son funciones de la **posición** de cada verso (su distancia al sitio del que se canta) |
| Tramo sin voz > 5 s | (un renglón vacío del LRC, o antes del primer verso) en el sitio del verso, **tres puntos blancos que respiran** uno tras otro (crecen y se iluminan cada 2,6 s, con 0,16 de desfase), entran en 0,5 s y **se van 0,45 s antes del siguiente verso**. Si el tramo es de 5 s o menos, nada (el verso anterior sigue). El renglón vacío del final de la canción no cuenta |
| La del MP3 (sin tiempos) | como Apple con la letra no sincronizada: **todos los versos igual de visibles** (blanco a 0,85, sin desenfoque, sin verso resaltado ni escala) y la columna **se desliza despacio y continua** a lo largo de la canción: el último verso queda en su sitio al acabar (`Reparto`) |
| Lo demás | solo con el reposo abierto (de 0,55 a 1), se va en 0,15 s si se abre pared, buscador, disco, ayuda, Ctrl+F3/F4, atracción o video; «Letra: LRCLIB» / «Letra: el MP3» chiquito abajo a la derecha; **no se atenúa con la calma** |

### 7.2 Cómo está hecho

| Archivo (en `RocolaOs/`) | Qué lleva |
|---|---|
| `RocolaOs.LetraCielo.cs` | `RocolaOsLetraModelo` (cuentas puras: `Arma` los versos y los tramos sin voz, `Actual`, `PuntosVis`, `PuntoBrillo`, `AlfaD`/`BlurD`/`EscalaD`, `Mascara`, `Resorte`, `Reparto`), `RocolaOsLetraHornea` (`Parte` el verso en renglones con la medida de XP, `Dibuja` el mapa blanco con su sombra, `Desenfoca`, `Punto`), `Form1` (lo que XP sabe) y `GpuPanel` (estado, resortes, horneado y dibujo) |
| `RocolaOs.LetraCieloPrueba.cs` | la orden `rocola-xp --prueba-letra-cielo-modelo`: 20 comprobaciones sin pantalla |

- **Un verso = dos mapas**, el nítido y el desenfocado (el mismo, desenfocado con sigma de 0,075 x tamaño de letra): se hornean **una vez, en Prepara** (g381, fuera
  del cuadro) y **UN mapa por cuadro como mucho**: el nítido del verso que falte primero (el que se canta, los que siguen, el anterior) y, en el cuadro
  siguiente, su desenfocado (queda guardado un solo mapa pendiente). Se sueltan los lejanos (más de 4 antes o 11 después del que se canta) y todos al irse
  el reposo. El mapa de un verso mide el ancho de la columna (755 px) más margen, por 56 px el renglón: hasta 14 versos x 2 mapas, unos 25 MB de textura como mucho.
- **Cada verso tiene su posición** (arreglos del tamaño de la letra, una vez por canción: `letraY`, `letraVel`, `letraOk`). Al cambiar de verso, cada uno sigue un
  **resorte crítico** (`Resorte`: solución exacta, estable con cualquier paso) hacia su sitio nuevo, que le llega con su retraso. Si el cambio es un salto de
  más de 5 versos (buscar en la canción), se coloca sin animar. El tamaño de cada verso (cuántos renglones) se mide con la letra de XP la primera vez.
- **El dibujo** (g380): cada mapa es una caja recortada a la columna y partida en tiras (para el desvanecido de arriba y abajo) y, si la estrella toca la caja,
  en celdas de 24 px (para atenuarla cerca de la estrella); el alfa de cada esquina sale de `LetraAlfaEn`. Un lote (`DrawTexturedBatch`) por mapa. Sin memoria nueva
  por cuadro (los vértices, un arreglo fijo).
- Para las pruebas: `ROCOLA_LETRA_LOG=archivo` (una línea cada 50 ms con la letra a la vista: hora, posición, cosa de la columna, `y` del verso que se canta,
  `mov` lo que le falta, y lo que tardaron el dibujo, el desenfoque y la subida; la primera línea, `geo`, dice dónde está la columna) y `ROCOLA_LETRA_CAMBIO_S`
  (alarga el resorte: con 3 s se puede capturar a la mitad de un cambio; el retraso crece con él).

### 7.3 Ganchos (rango g380-g389; los mismos dos de la §6, sin ninguno nuevo)

| Id | Dónde | De XP | Ahora |
|---|---|---|---|
| g381 | `Codigo/D3D9Star.cs` (`RenderFrame`, tras `StepIdle`) | `idleBlend = StepIdle(...);` | `... RocolaOsLetraPrepara();`: el estado, los resortes y el horneado, antes de abrir el cuadro |
| g380 | `Codigo/D3D9Star.cs` (`RenderFrame`, tras el buscador con portadas) | `PiezasConLaVentana(gw, uiPropia);` | `... RocolaOsLetraPinta();`: la columna, antes de la interfaz de XP del pie y del panel del reposo |

Solo cambió el texto de la explicación de g380 (decía «dos renglones»). `ganchos.py`: TODO BIEN (112 archivos, 34 con ganchos).

### 7.4 Las pruebas (`Pruebas/RocolaOs/letra-cielo/`, `bash prueba.sh PROGRAMA [escenas]`)

Como en la §6.4 (la rocola entera fuera de pantalla, MP3 y LRCLIB **inventados**, el LRC de `datos.json` con 13 renglones: 0 vacío, 8, 14, 20 un verso largo,
30, **36 vacío (12 s sin voz hasta el 48)**, 48, 54, 60, 66, 72, 80 y 88 vacío). Las escenas con medidas (`columna`, `mp3`, `nada`) van **con el cielo vivo
apagado** (su sol, luna y nubes meten tinta clara); `cielo` es la que lo enciende. `analiza.py` mide con la geometría que el log dice (`geo`).

| Escena | Qué comprueba |
|---|---|
| `ganchos` | `ganchos.py` TODO BIEN y g380-g381 en el manifiesto |
| `modelo` | 20 comprobaciones puras (los versos y los tramos sin voz con y sin renglón vacío, un tramo corto sin puntos, los puntos antes del primer verso, `PuntosVis`, alfa/desenfoque/escala continuos y con los valores pedidos, la máscara, el resorte sin rebote y exacto con cualquier paso, el reparto parejo del MP3, partir un verso largo sin perder palabras y en 4 renglones como mucho) |
| `columna` | la geometría (borde izquierdo ≥ 224 = estrella en su extremo izquierdo, derecho cerca del borde, vertical sobre la raya); **el resorte en el log** (en cada cambio el verso baja sin retroceder, sin pasarse más de 0,6 px y llega); varios renglones a la vista, todos arrancando en el borde izquierdo; el verso largo en 4 renglones de blanco opaco; el que se canta opaco y los demás translúcidos; el desvanecido de abajo; **regla 3** del verso opaco; en el cambio la tinta sube; **los tres puntos** en el tramo sin voz (a los 56 y 60 s, ningún verso opaco); y pasado el tramo el verso que vuelve es el opaco |
| `mp3` | la letra es la del MP3, todos los versos igual de visibles (ni opacos ni translúcidos), alineados a la izquierda, la columna sube de los 44 a los 68 s y el verso de referencia no se mueve a más de 30 px por segundo de canción (lo normal: 6 a 19) |
| `nada` | con el reposo abierto la letra se ve; tras tocar (pared, buscador, disco, ayuda) el log de la letra (que solo anota con la letra a la vista) calla 4 s antes de la captura |
| `cielo` | la regla 3 con **cielo, fiestas y clima encendidos**: de día con nubes, el 20-11 (fiesta) con lluvia, al anochecer y de noche |
| `apagado` | con `letra_cielo=0` la forma de los cuadros de la tarjeta y sus dibujos de textura (sueltos y en lote) son los de la rama sin la letra; con ella, más lotes (control); sin `ROCOLA_OS`, ni log ni archivos |
| `reloj` | p50/p95/p99/max del trabajo de cada cuadro (`ROCOLA_CIELO_RELOJ`, NativeAOT, 70 s) con la letra y sin ella |

**Resultados (27-11-2026, la máquina compartida con carga de 20 a 45 en 4 núcleos, una escena a la vez):**

| Qué | Resultado |
|---|---|
| Compila (JIT, `dotnet build -c Release`) | 0 errores; los 13 avisos de siempre |
| NativeAOT (`dotnet publish -p:PublishAot=true -r linux-x64`) | 0 errores; los mismos 13 avisos; **0 avisos IL/AOT** |
| `prueba.sh` completa (JIT; `reloj` en NativeAOT) | **37 BIEN, 0 MAL** (ganchos 2, modelo 1 con sus 20, columna 13, mp3 6, nada 4, cielo 4, apagado 5, reloj 1; los números de las escenas están en este documento) |
| `modelo` | 20 BIEN, 0 MAL |
| Resorte en el log (`columna`, cambio de 3 s) | 8 cambios de verso: ninguno retrocede, se pasan 0,4 px como mucho (redondeo) y llegan al 95 % en 2,9 s con el resorte de 3 s (con el de 0,55 s serían 0,44 s, por la cuenta) |
| Regla 3 (blanco opaco con sombra contra el cielo, medido en el verso que se canta) | cielo de XP 7,6; con cielo vivo: de día con nubes 7,9; el 20-11 con lluvia 8,5; al anochecer 8,3; de noche 8,0 (se pide 4,5) |
| `apagado` | con `letra_cielo=0`: misma forma de cuadros, 5,00 dibujos sueltos y 2,00 lotes por cuadro (igual que sin la letra); con la letra, 6,00 y 6,00; sin `ROCOLA_OS`: ni log ni archivos |
| `reloj` (NativeAOT, 70 s, p50/p95/p99/max en ms por cuadro) | **con la letra 2,41 / 4,56 / 9,20 / 27,0; sin ella 2,29 / 3,54 / 6,58 / 169,3** (891 y 1 275 cuadros). Con la máquina a carga 35 la cifra absoluta no vale (la §6 midió 1,62 / 2,39 / 3,24 con la máquina más libre): vale la diferencia, unos 0,1 ms en p50 y 1 a 2,6 ms en p95 y p99 (el horneado y las tiras del dibujo); el máximo de «sin» es un tropiezo de la máquina |
| Horneado (de la columna `ms_dibuja`, `ms_borra` y `ms_sube` del log, carga 30) | el mapa nítido 1,5 a 6 ms, el desenfocado 0,7 a 2 ms, la subida 0,4 a 1 ms; uno por cuadro |

**Lo que falló por el camino y se corrigió (no era de antes):** la geometría medida con el panel a medio subir (el log ahora anota `geo` con el panel abierto del todo y la
columna usa su posición final: así no se mueve mientras el reposo se abre); las manchas de la estrella y la luna del cielo vivo contaban como tinta (las escenas con
medidas van con el cielo vivo apagado); la estrella atenuando el verso que se canta hasta quitarle el contraste (el círculo de atenuación bajó de 1,9 a 1,25 radios:
`cielo-fiesta` medía 2,67 con la estrella cerca y ahora 8,5); un horneado de dos mapas por cuadro con picos de 10 a 20 ms bajo carga (ahora uno por cuadro y la sombra se
desenfoca una vez en vez de dos).

### 7.5 Lo que no se sabe

- **Nada en el A4, ni con LRCLIB de verdad, ni con letras de canciones de verdad, ni comparado con la captura de Apple Music del dueño** (no la tengo: se hizo con
  su descripción). Los parámetros que dependen del gusto están como constantes: tamaño de letra 0,052, alfas 0,38 / 0,34 / 0,85, desenfoque, 0,55 s, 35 ms de retraso.
- **La estrella recorre todo el cielo.** La letra no puede evitarla del todo: el borde izquierdo la deja pasar a su izquierda (donde estaba en la captura del dueño)
  y, cuando pasa por encima de la columna, la letra se atenúa a 0,2 en un círculo de 1,25 radios alrededor de ella, así que **ese trozo del verso que se canta
  deja de ser blanco opaco** mientras la estrella pasa (regla 3 contra regla «no tapar la estrella»: gana la estrella). Si el dueño prefiere lo contrario, es una constante.
- El contraste (regla 3) se midió con la letra de repuesto de la máquina (Trebuchet MS cae a DejaVu Sans Bold sin las letras del disco de Windows): en el A4 la forma
  cambia un poco, no la sombra ni los colores. Se midió sin la estrella encima del verso.
- Con Spotify la posición sale de su fuente (como en la §6): no se probó con Soloist real. La sincronizada de LRCLIB es de línea: el verso entra a su tiempo, no por palabra.
- El horneado cuesta más que el de los dos renglones de la §6 (versos de hasta 4 renglones, dos mapas por verso, el desenfoque): **medido a 1,5 a 6 ms por mapa con la
  máquina a carga 20 a 45 en 4 núcleos** (las cifras de `reloj` salen muy ruidosas con esa carga); en el A4 sin medir. Al empezar la canción los versos de abajo
  aparecen uno por cuadro (unos 12 cuadros).
- El mapa nítido y el desenfocado se mezclan con dos alfas (no una mezcla exacta): el resultado es parecido, no idéntico, a desenfocar con radio variable.
