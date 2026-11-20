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
- **En el buscador** (g110): una frase de **4 palabras o más** (contadas en lo escrito ya normalizado) cuyas palabras que cuentan (sin las
  vacías, sin repetir) estén **3 de cada 4** en la letra de una canción (la última puede ir a medio escribir: vale la que empiece así, desde
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
  2. si no, **la de Spotify** (con el modo Spotify y su llave): la búsqueda «título artista» del cliente del catálogo (la misma de lo
     escrito: su cuota, su freno de 429) y la primera canción con ese título; su tarjeta es la de Spotify del buscador (elegirla la pide por
     I6, con sus créditos);
  3. si no, **una tarjeta «NO ESTÁ EN LA ROCOLA»**: la tarjeta de canción del buscador de XP, con el título que dice Genius y, en el renglón
     del disco, «NO ESTA EN LA ROCOLA» (con el artista en la de la tarjeta). No hay nada nuevo que dibujar: es un `Album` de mentira (la
     carpeta `/::LETRA::/…`, sin palabras ni portada: XP pone las iniciales) en un `SearchItem` de XP. Elegirla (g111) solo lo dice con el
     aviso del buscador de XP («Esa no esta en la rocola»).
- Al llegar la respuesta (el encargo `RocolaOs:busca_letra:llego`), si se sigue mirando esa frase, la lista se rehace con
  `RecomputeResults` de XP y la elegida conserva su sitio.
- **Nada se guarda**: lo dicho por Genius vive en memoria la sesión (40 frases), cada frase se pregunta **una sola vez** por sesión (aunque
  fallara), y como mucho 300 preguntas por sesión. Sin red o con Genius caído, nada (el buscador de XP tal cual).

### La letra de lo que suena («letra_cielo», ENCENDIDA de fábrica: solo para pedirla; **nada se dibuja**)

- El latido (en la pantalla) ve que empezó a sonar otra canción (`nowPlaying`; tuya o de Spotify; un video no) y, en cuanto se sabe su
  duración (la del lector, hasta 5 s), le pide su letra al hilo `RocolaOs:letra_cielo`, que busca:
  1. **LRCLIB** (`https://lrclib.net/api/get?track_name=&artist_name=&album_name=&duration=`, sin llave), con el User-Agent
     `rocola-xp/1 (Rocola OS)` (el nombre del programa, nada del dueño): la letra **sincronizada** (`syncedLyrics`, LRC);
  2. si no, la **USLT del MP3** (sin tiempos; solo si es un MP3 tuyo);
  3. si tampoco, la de LRCLIB **sin tiempos** (`plainLyrics`), si la dio.
- El título va sin el número de pista (`TituloSinPista` de XP), el artista el de la canción (o el de su disco), el disco el de la canción
  (o el nombre del disco de XP) y la duración en segundos; sin disco o sin duración, la petición va sin ellos.
- **Las fotos inmutables** para la pantalla: `RocolaOsLetraSuena.Actual` (la letra: el proveedor, «LRCLIB» o «el MP3», si es sincronizada,
  sus tiempos y sus versos; `RocolaOsLetra.En(pos)` da el verso de cualquier posición) y `RocolaOsLetraSuena.Verso` (la pone cada latido con
  la posición de lo que suena, `PosicionActual` de XP: **el verso que se canta y el siguiente que dice algo**). El LRC se lee con uno o
  varios tiempos por renglón, `[offset:±ms]` y sin las demás etiquetas.
- **Las letras no se guardan**: en memoria (64 canciones) y en `/run/rocola-xp/letras/*.lrc` (lo vivo: se va al apagar). El estado,
  `/run/rocola-xp/letras/estado`, lleva **solo números** (qué hay, el proveedor, cuántos versos, el de ahora y el siguiente, las
  peticiones y las fallidas), sin texto de la letra.
- **El tope**: una petición por canción y por sesión (ninguna repetida si falló); un fallo de la red, un 429 o un 5xx frena **2 minutos**
  cualquier otra (sin reintentos). Con `ROCOLA_LETRA_API` un LRCLIB falso (las pruebas).

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
| `orden` | `--prueba-letra-cielo` (sin la rocola): LRCLIB sincronizada y el verso a −1, 0, 1,99, 2, 5,5, 12,3, 18, 19,9 y 30 s (y el siguiente); de memoria sin otra petición; la petición con título, artista, disco, duración y el User-Agent genérico; `/run` sí y los datos no; otro arranque con el mismo `/run`: 0 peticiones; `[offset:+500]` y dos tiempos en un renglón; sin disco ni duración; LRCLIB 404 → la USLT del MP3; solo `plainLyrics`; ninguna en ningún lado tres veces = 1 petición; sin red = nada, 1 petición en tres vueltas, y con USLT, la del MP3 |
| `l3` | El repaso (5 MP3, 3 con letra); «luna de papel brillante»: lo exacto de XP (el título de la 03) primero y la 04 (por su letra) la última; «perro viento frio tarde» (3 de 4) la encuentra; «perro azul viento frio» (2 de 4) no; el archivo solo con palabras sueltas en orden alfabético, sin acentos ni vacías; Genius apagado de fábrica: 0 peticiones |
| `tres` | «viento frio tarde» está entera en la letra de la 05, pero son 3 palabras: no sale |
| `apagado`, `cero` | Sin `ROCOLA_OS`, y con `ROCOLA_OS=1` y las cuatro apagadas: ni el archivo, ni `/run`, ni una petición, nada en la traza; y en `cero`, la traza igual a la de `apagado` quitando lo de RocolaOs (`compara_trazas.py --ignora RocolaOs: --holgura 80`) |
| `genius` | Con la llave falsa y el modo Spotify: UNA pregunta a Genius (con la frase entera y la llave bien en la cabecera, nunca en la dirección); delante, la tuya, la de Spotify (por la búsqueda «título artista») y la NO ESTÁ, en ese orden; la 4.ª de Genius (el remix de la misma) no es otra tarjeta; la llave en ningún archivo de los datos (salvo el suyo), `/run`, la salida ni la traza; nada de Genius guardado |
| `sinllave` | Encendida sin llave: 0 peticiones, la bitácora lo dice, y la letra de tus MP3 sí |
| `suena` | La rocola toca «Amor de Prueba» (21 s, LRC de 10 versos cada 2 s): UNA petición a LRCLIB con título, artista, disco y duración; la letra sincronizada de LRCLIB; los versos avanzan en orden; el siguiente es el que le sigue; al acabar, nada suena; la letra en `/run` y nada en los datos; el estado sin texto |

**Resultados (20-11-2026, en la máquina compartida):** JIT **68 BIEN, 0 MAL** (orden 22, apagado 5, cero 5, l3 10, tres 3, genius 12,
sinllave 4, suena 7); NativeAOT: AOTRES. Compila sin avisos nuevos: los 13 de siempre en JIT y en NativeAOT (ninguno de `RocolaOs/`,
ningún IL/AOT). `ganchos.py` TODO BIEN (51 ganchos en 19 archivos); `diff-xp.py --sin-ganchos`: 5 135 quitadas, 4 868 puestas, 229 sin marca
(las de la base). Nivel 0 (sin `ROCOLA_OS`, contra la referencia congelada): NIVEL0RES.

## 4. Lo que falta o no se sabe

- **Nada se dibuja de la letra de lo que suena** (a propósito): espera la maqueta y el sí del dueño. Cuando se haga, el dibujo lee
  `RocolaOsLetraSuena.Verso` (o `Actual.En(pos)` con la posición fina).
- **Genius queda apagado**: encenderlo es del dueño (y sus condiciones, de uso no comercial). Sin probar con el Genius de verdad: su
  `/search` se leyó como lo leía el programa anterior (`response.hits[].result.title` y `primary_artist.name`).
- **LRCLIB de verdad no se probó** (sin red aquí): `/api/get` se usó como lo documenta LRCLIB (404 si no la tiene; `syncedLyrics` y
  `plainLyrics`). Con la biblioteca de XP el artista y el disco salen de las carpetas (`Artista - Disco`); con carpetas sin ese formato el
  artista puede ser otro y LRCLIB no la encontrará (no cuesta más que esa petición).
- **Con el modo Spotify, sus artistas van arriba de las tarjetas de Genius**: g40 pone los artistas de Spotify delante cuando lo primero de
  la lista no es un artista (y las de Genius son canciones). Con una frase de 4 palabras rara vez sale un artista de Spotify (su nombre
  tiene que parecerse a lo escrito), pero puede pasar.
- **La búsqueda de Spotify de L1 comparte la cola de una búsqueda del cliente del catálogo**: si llegara justo cuando la de lo escrito aún
  espera sus 300 ms, la reemplazaría (la de lo escrito ya salió para entonces: Genius pregunta a los 0,8 s y tarda lo suyo).
- **Elegir la tarjeta NO ESTÁ no se probó en la rocola entera** (las teclas del buscador con portadas en las pruebas son delicadas); lo que
  hace es una línea (el aviso de XP).
- **El repaso con la biblioteca real y en el A4**: sin medir (20 ms por archivo a nice 19; con 20 000 MP3, unos 7 minutos la primera vez).
