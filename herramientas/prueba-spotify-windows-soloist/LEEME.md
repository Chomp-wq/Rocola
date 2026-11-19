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
  cache (volver a medir)». La cache va con tope de 1 GB (`--cache-size 1024`; sin tope crece sin limite).
- No es «bit a bit»: Soloist entrega coma flotante de 32 bits a WASAPI. Para la rocola da igual (su cadena procesa todo);
  importa que la fuente sea FLAC.
- Sin confirmar con una cuenta real: hay reportes de que Spotify sigue mandando Vorbis 320 a Soloist aun con Lossless
  elegido. La medida de esta demo es justo para saberlo con la cuenta del dueno. La forma de la cache no la documenta
  Spotify: si cambia en otra version de Soloist, la medida dira «sin medir».
- Probada solo en Linux (servidor, paginas, botones, Soloist no instalado); no en Windows, no con cuenta real.
