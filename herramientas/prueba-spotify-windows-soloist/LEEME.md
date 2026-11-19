# Demo de la rocola con Spotify, con Soloist (Windows)

Segunda version de la herramienta de prueba del dueno (19-11-2026). La primera (`herramientas/prueba-spotify-windows`,
con librespot) **no sirve**: el dueno avisto que librespot "solo funciona con cuentas creadas desde antes de 2025" y ya
le habia fallado antes. Esta usa **Spotify Soloist**, el cliente oficial de Spotify Connect sin pantalla (agosto de
2026). Novedad importante encontrada al leer la documentacion oficial (developer.spotify.com/documentation/soloist,
que la sesion del 19-11 no habia alcanzado): Soloist **si tiene un build nativo para Windows x86_64**
(`soloist_release_windows_x86_64.zip`, audio por WASAPI) — **no hace falta WSL2** como se pensaba antes de leer la
documentacion.

## Que es y que no es

- Es una prueba de la **conexion y la cuenta** con Soloist, no la rocola ni el modo Spotify de rocola-xp.
- Soloist **no se reparte aqui** (sus propios terminos lo prohiben: "Do not redistribute Spotify Soloist archives or
  binaries directly"). El dueno lo descarga el mismo.
- Soloist es solo el que **toca** la musica (como cualquier aparato Spotify Connect). Para **buscar y ver la
  biblioteca** (discos, listas, `/v1/me`) esta demo entra por su cuenta aparte con el navegador (OAuth, el mismo
  cliente de escritorio que usa librespot, pero pidiendo permisos de la API web directamente: ya no hay una Session de
  librespot que los saque por su cuenta). Son dos entradas independientes: se puede usar Soloist sin la cuenta del
  navegador (solo no habra buscador ni pared) y viceversa (se puede buscar pero no sonara nada sin Soloist emparejado).

## Antes de compilar: lo que el dueno tiene que conseguir el mismo

1. **Soloist para Windows**: developer.spotify.com/documentation/soloist/reference/downloads-and-updates ->
   `soloist_release_windows_x86_64.zip`. Descomprimir en cualquier carpeta (por ejemplo junto al .exe de esta demo).
   El build caduca a los 90 dias de compilado: si `soloist.exe --version` dice que ya caduco, bajar uno nuevo.
2. **La llave de la API de Soloist** (necesita Premium): developer.spotify.com/dashboard/soloist -> generar. Es
   personal, no se comparte.
3. Abrir Spotify en el telefono o el escritorio, en la MISMA red que la PC gamer (Soloist se anuncia por la red local,
   no por internet).

## Compilar desde Linux

    apt-get install -y mingw-w64 libasound2-dev pkg-config libssl-dev
    rustup target add x86_64-pc-windows-gnu
    cargo build --release --target x86_64-pc-windows-gnu
    # sale target/x86_64-pc-windows-gnu/release/RocolaSpotifyDemoSoloist.exe

Para Linux (solo para probar la pagina y los botones; Soloist tambien existe para Linux x86_64, asi que de hecho SI se
puede probar entero aqui): `cargo build --release`. Variables: `PRUEBA_NO_ABRIR=1` (no abre el navegador),
`PRUEBA_DATOS=ruta` (carpeta de la llave y del emparejamiento), `SOLOIST_API_KEY=...` (si no se quiere escribir la
llave en el archivo de texto).

## Usar la herramienta

1. Abrir `RocolaSpotifyDemoSoloist.exe`. Se abre el navegador en `127.0.0.1` (puerto 8744 o el siguiente libre).
2. Pestana **Pruebas**, seccion **Soloist**: pon la ruta a `soloist.exe`, un nombre de aparato, y la llave. «Guardar
   ajustes» y despues «Iniciar Soloist». Cuando diga "corriendo" y "conectado por WebSocket", abre Spotify en el
   telefono/escritorio y elige el aparato por su nombre en el selector de Spotify Connect: eso lo empareja (no pide
   la contraseña aqui).
3. Seccion **Tu cuenta**: «Entrar con el navegador» (para que la pestana Rocola pueda buscar y mostrar tu biblioteca).
4. Pestana **Rocola**: moneda (tecla 5), elige una cancion de tu pared o del buscador (tecla B), Enter o clic para
   pedirla. Si Soloist esta emparejado, debe sonar por la bocina de la PC.
5. «Guardar informe» en Pruebas deja un `informe_spotify_soloist.txt` junto al .exe, sin llaves ni tokens, para
   mandarlo si algo no funciona.

### Prueba mas simple, sin esta herramienta

Para saber lo mas rapido posible si Soloist sirve con la cuenta del dueno (la pregunta de fondo del 19-11-2026), basta
con, desde una consola, despues de arrancar `soloist.exe -n "Prueba" -k LLAVE` y emparejarlo desde el telefono:

    soloist.exe ctl status
    soloist.exe ctl now

Si eso contesta con la cuenta emparejada, Soloist ya funciona con esa cuenta — sin necesitar nada mas de esta demo.

## Limites conocidos (sin probar con cuenta real ni en Windows)

- El permiso de la API web que pide esta demo por el navegador (`user-library-read`, `playlist-read-private`, etc.)
  se pide con el mismo client ID de escritorio que usa librespot. No esta confirmado que Spotify le conceda esos
  permisos a ese cliente (fue pensado por Spotify para "streaming", no para biblioteca): si el navegador rechaza el
  permiso, el buscador y la pared de esta demo no funcionaran, aunque Soloist si pueda tocar lo que ya este en la cola
  desde el telefono.
- No hay letra (Soloist y la API web publica no la dan; se quito respecto a la version con librespot).
- Si algo suena sin pedirlo desde esta pagina (reanudado desde el telefono, o Spotify que sigue solo al acabar), la
  demo lo pausa: nada suena sin credito. Cada pedida es un «play» de una sola cancion; la cola es la de la pagina (no
  se usa la cola de Spotify).

## FLAC (Spotify Lossless)

- Solo el cliente oficial (Soloist) puede recibir Lossless (FLAC hasta 24 bit / 44,1 kHz); librespot se queda en Ogg
  Vorbis de 320 kbps.
- **Se activa en la app de Spotify, aparato por aparato**: en el selector de aparatos, en las opciones de «Rocola
  (prueba Soloist)», poner la calidad en Lossless. La cuenta debe tener Lossless disponible. Esta demo NO puede pedirlo:
  Soloist no tiene esa opcion ni en su linea de comandos ni en su WebSocket.
- **La demo lo mide**: Soloist guarda un archivo (cifrado) por cancion en su cache; su tamano × 8 / la duracion da los
  kbps (lo mismo que hacen los plugins de Volumio y Lyrion). Vorbis 320 da ~330 kbps; FLAC da ~700-1700. Se ve en la
  pestana Pruebas («Calidad de la ultima pedida») y junto a lo que suena (FLAC en verde, los kbps en dorado si no). El
  archivo nunca se abre ni se descifra: solo se mira su tamano. Las medidas se guardan por cancion en
  `calidad_medida.json` (solo ids y numeros).
- Lo que ya estaba en la cache antes de cambiar a Lossless sigue en la calidad vieja: «Detener» y luego «Vaciar la
  cache (volver a medir)». La cache va con tope de 2 GB de fabrica (`--cache-size`; se cambia en Pruebas -> Soloist -> «Cache de Soloist (MB)» y vale al volver a iniciar; sin tope crece sin limite).
- No es «bit a bit»: Soloist entrega coma flotante de 32 bits a WASAPI. Para la rocola da igual (su cadena procesa todo);
  importa que la fuente sea FLAC.
- Sin confirmar con una cuenta real: hay reportes de que Spotify sigue mandando Vorbis 320 a Soloist aun con Lossless
  elegido. La medida de esta demo es justo para saberlo con la cuenta del dueno. La forma de la cache no la documenta
  Spotify: si cambia en otra version de Soloist, la medida dira «sin medir».
- Probada solo en Linux (servidor, paginas, botones, Soloist no instalado); no en Windows, no con cuenta real.

## La pared de 500 paginas por letra (el catalogo)

- La pared ya no depende de la API de Spotify para hojear: se arma con el catalogo abierto de la rocola (MusicBrainz, Wikidata, ListenBrainz; `sistema/catalogo`), que la demo baja sola de la publicacion «catalogo» de este repositorio (publico, sin llave) a `datos_spotify_soloist/catalogo/`. Solo baja el `.tsv.gz` si cambio la version, y antes de usarlo comprueba el tamano y el sha256.
- En cada letra van tus discos guardados y tus listas, mas los del catalogo: primero por genero (Regional, Tropical, Bolero, Latino y Reggaeton primero, luego los generos que mas tienes, y «Otros» al final), y dentro de cada genero los artistas del MAS AL MENOS FAMOSO (la fama del catalogo: escuchas y oyentes de ListenBrainz, Wikipedias, pais; en la L del Regional: Lila Downs, Lucero, Lola Beltran, Los Angeles Azules...). De cada artista, tus discos antes que los del catalogo y estos del mas al menos escuchado. Son como mucho 9000 discos por letra (500 paginas de 18). La cabecera lleva la pastilla del genero y «Pagina N de M».
- Las portadas salen del Cover Art Archive, no de Spotify. Hojear no gasta ninguna peticion de la API.
- Rapidez: el Cover Art Archive manda cada portada por tres redirecciones hasta archive.org (lento). La demo la baja UNA vez y la guarda en `datos_spotify_soloist/portadas` (tope de 600 MB: al pasar se borran las menos usadas); despues sale del disco en milisegundos. Al arrancar baja de fondo las dos primeras paginas de todas las letras, y mientras miras una pagina baja la siguiente, las dos de despues y la anterior (sin ocupar las conexiones del navegador). Las 27 letras se arman al arrancar (cerca de 1 s) y van comprimidas (550 KB en vez de 1,8 MB). Moverse dentro de la misma pagina ya no vuelve a poner las portadas.
- Las portadas van por tres puertos aparte de la pagina (8780, 8781 y 8782, o los siguientes libres): el navegador abre 6 conexiones por puerto, asi que las 18 de una pagina bajan a la vez y las portadas lentas no dejan esperando a Spotify ni al estado.

## Mas rapido y con menos cuota

- **Tus discos al momento:** la lista de tus discos y listas de Spotify (solo ids, nombres y portadas) se guarda en `mis_discos.json`; al abrir la demo otra vez la pared ya los tiene, sin pantalla de espera, y se renuevan de fondo (la primera pagina dice cuantos hay y las demas se piden de 3 en 3 a la vez). «Olvidar esta cuenta» la borra.
- **Abrir un disco:** una sola peticion (`/v1/albums/ID` trae sus primeras 50 canciones y la portada de Spotify, que sale de su CDN rapido). Lo que contesta Spotify se recuerda: un disco 30 dias, tambien en el disco duro (`api_cache`, tope de 200 MB), asi que abrirlo otra vez, aunque sea otro dia, no gasta nada; los discos de un artista 1 dia, una busqueda 30 min, una lista 5 min y lo tuyo 1 min. Si la misma peticion ya va en camino, se espera esa. Se quita de cada respuesta la lista de paises (casi todo el peso).
- **Adelantar:** si el cursor se queda 0,4 s en un disco se baja su portada grande (no gasta cuota), y a los 1,2 s sus canciones (UNA peticion, solo si ya se sabe su id; como mucho una cada 3 s, ninguna si Spotify pidio esperar, y no antes de que toques algo). Al abrirlo sale al momento. Al abrir, primero se ve la portada chica y la grande al llegar; moverse por las canciones ya no repinta la portada.
- **Buscador al momento:** mientras escribes salen los artistas y discos de la rocola (catalogo y tus discos y listas) que tienen todas las palabras, del mas famoso al menos, sin gastar cuota (de 20 a 90 ms). Enter busca ademas canciones, discos y artistas en Spotify (una peticion). Un artista de la rocola abre su pared con sus discos, tambien sin gastar cuota.
- Abrir un disco del catalogo cuesta 1 peticion si ya se sabe su id de Spotify. Si no, la primera vez son 2: una busqueda `album:"..." artist:"..."` y luego abrirlo. El id encontrado (o «-» si Spotify no lo tiene, para no volver a ensenarlo) queda en `catalogo_ids.json`. Tocar va por Soloist y no cuenta para la cuota de la API.
- Sin catalogo ni cuenta, la pared dice que falta. Con catalogo y sin cuenta se puede hojear, pero no abrir discos. En Pruebas, «La pared (catalogo)» muestra la version y tiene un boton para buscar uno nuevo.
- Para probar con un archivo propio: `PRUEBA_CATALOGO=ruta\rocola_catalogo.tsv.gz`.

## Todavia mas rapido (segunda vuelta)

- **La cola de la rocola es del programa, no de la pagina.** Antes la pagina miraba cada 0,7 s si la cancion habia acabado y entonces pedia la siguiente: unos 380 ms de espera en cada cambio (medido con un Soloist falso), y con la pestana oculta el navegador frena sus relojes (a 1 s, y tras unos minutos a una vez por minuto; esto ultimo no se pudo medir aqui). Ahora la cola vive en la herramienta: en cuanto Soloist avisa que acabo, manda el «play» de la siguiente (1 ms), sin depender de la pestana. Los creditos de lo que no llega a sonar (falla, se detiene Soloist) los devuelve la herramienta y la pagina los suma.
- **Pedir una cancion**: la cache de Soloist se recorria dos veces (una para cerrar la medida de la anterior y otra para empezar la nueva) antes de mandar «play», y con el candado puesto (la pagina se quedaba esperando su estado). Ahora es una sola recorrida y fuera de los candados.
- **Portadas, lo que se ve va primero:** antes, las 18 de la pagina compartian turno con las del adelanto y una pagina lejana tardaba 2,5-3,6 s en verse; ahora 1,2 s (lo que tarda una sola portada: tres saltos de 0,4 s en la prueba). Las del adelanto se apartan mientras el navegador espera portadas, y cuantas se bajan a la vez se ajusta a lo que tarda la red (de 4 a 16): con 2,5 s mirando cada pagina, pasar a la siguiente ya no espera (de 0,8-1,1 s a 0,06-0,35 s). Cada portada se reintenta una vez si falla.
- **Red hacia Spotify:** HTTP/2 (las peticiones en paralelo comparten una sola conexion, sin repetir el saludo TLS) y respuestas comprimidas con gzip; las conexiones a Spotify y a las portadas se abren desde el arranque; la llave de la API se renueva 5 minutos antes de caducar (antes la peticion que la encontraba vencida esperaba la renovacion, cada hora) y si varias la piden a la vez solo una renueva. Comprobado en Linux con un servidor local con TLS y HTTP/2: se negocia h2, el gzip se decodifica bien y 7 peticiones usaron 1 sola conexion; en Windows (schannel) no se pudo probar y, si no hubiera h2, sigue como antes.
- **Arranque:** Soloist se inicia antes que la renovacion de la llave de la API web (que espera a la red).
- **Discos del catalogo sin id de Spotify:** si el cursor se queda quieto 1,2 s sobre uno, la herramienta lo busca (1 peticion, que se recuerda) y baja sus canciones (1 mas); al abrirlo no pide nada (21 ms). Una cadena cada 3 s como mucho, ninguna mientras Spotify pida esperar y ninguna hasta que toques algo. Si Spotify no lo tiene, se anota pero no desaparece de la pared mientras la miras; al abrirlo se avisa.
- **Los discos de un artista de Spotify** se piden a la vez (antes uno tras otro). Cuando una busqueda encuentra un disco, solo se rehace la letra de ese artista (antes, las 27).
- **La ventana negra ya no congela al programa:** en Windows, un clic dentro de la consola (seleccion rapida) detiene al programa en su siguiente linea anotada hasta que pulsas una tecla, y con el a cada hilo que anota algo. Ahora se quita la seleccion rapida y las lineas van por un canal a un hilo aparte (reproducido en Linux con una consola que no se vacia: el programa viejo dejaba peticiones colgadas despues de ~36 lineas; el nuevo, ninguna en 150). Por eso ya no se puede seleccionar texto en la ventana negra: todo esta en Pruebas -> Diagnostico.
- **Calculo:** compilado para velocidad en vez de tamano (el .exe pasa de 2,9 a 3,9 MB): armar las 27 letras 15 % mas rapido y la busqueda de 2 letras de 136 a 90 ms.
- **La cache de Soloist** sube a 2 GB de fabrica: lo que ya se bajo suena sin esperar a la red.
