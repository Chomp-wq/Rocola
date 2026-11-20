# Spotify: lo que hay que hacer al juntar los grupos (apuntado mientras trabajan)

1. **Encendido de fábrica** (decisión del dueño del 15-11-2026, AGENTS.md): `DeFabrica = true` en la función «spotify»
   (`RocolaOs/RocolaOs.SpotifyPide.cs`, grupo spotify-xp); la orden `spotify` (`rootfs_overlay/usr/bin/spotify`, grupo soloist) dice
   «de fábrica encendido»; `SPOTIFY_SOLOIST.md` ya no pide «spotify encender» (sí explica «spotify apagar»); las pruebas que daban por
   hecho que venía apagado se cambian, y se añade la escena «encendido sin ninguna llave = XP tal cual en lo que se ve y suena, sin
   cobrar nada, sin letra ♫ ni resultados de Spotify».
2. Borrar `RocolaOs/RocolaOs.SpotifyCatalogoDobleI6.cs` (el doble de I6 del grupo catalogo) y cambiar sus comprobaciones de «pedidas».
3. **El buscador con Spotify como lo pidió el dueño el 7-11-2026** (AGENTS.md; lo funcional, sin dibujo nuevo):
   - Un disco o lista de Spotify abierto desde el buscador que NO está en la pared: REGRESAR vuelve AL BUSCADOR con lo escrito y la
     misma elección (hoy el grupo catalogo vuelve a la pared, a la letra ♫). Si está en la pared, la pared se queda en él.
   - Artistas de Spotify: solo los que NO están en la rocola (ni por el nombre de un disco tuyo, sin acentos ni mayúsculas), hasta 3,
     justo después de tus artistas que salgan primero (si no hay, arriba); el primero si su nombre se parece a lo escrito (a dos letras),
     los demás si lo contienen. Elegirlo abre sus discos (y lo más conocido que se pueda sacar sin rutas quitadas: tus favoritas de él
     o el orden de la búsqueda `artist:`), y REGRESAR vuelve al buscador. Su foto como portada de la tarjeta de XP. Sin «N seguidores»
     (Spotify quitó followers en 2026).
   - SP9, los atajos del buscador: «tus más escuchadas» (`/v1/me/top/tracks`) y «escuchado hace poco»
     (`/v1/me/player/recently-played`), una petición cada uno por sesión y en memoria; solo con `refresh_token` (si no, no salen).
   - El zoom desde la casilla es visual: a la presentación del final.
4. **Las paredes del buscador** (decisión del dueño del 15-11-2026, AGENTS.md). Va en un grupo propio («paredes-buscador») encima de la
   rama `fase11-catalogo` en cuanto ese grupo termine (toca los mismos archivos), y se junta con la base si está a tiempo:
   - Pared del artista que solo está en Spotify: la rejilla de XP (18 por página, IZQ/DER, ARRIBA/ABAJO de página, PONER abre), fuera de
     la barra de letras; la cabecera dice su nombre. Casilla 1: «Canciones de ARTISTA» (`/v1/search?type=track&q=artist:"..."`, hasta
     50 en 5 páginas de 10, como un disco de XP que se abre y se pide por I6). Luego `/v1/artists/{id}/albums?include_groups=album,single`
     de 10 en 10, pidiendo solo lo que se mira (y la página de al lado).
   - REGRESAR: disco -> su pared (misma página y casilla) -> buscador (lo escrito y la misma elección). Pila de como mucho 4.
   - «Todos los discos de «lo escrito»»: la última tarjeta de los discos de Spotify en el buscador; abre una pared con
     `/v1/search?type=album` de 10 en 10 (offset hasta 1000), solo lo que se mira.
   - Tus artistas: tu tarjeta de siempre igual (lleva a tu pared, como XP) y, justo detrás, «ARTISTA · más en Spotify» (si Spotify lo
     tiene y hay discos que no tienes: sin acentos ni mayúsculas, por nombre de disco) que abre su pared sin los tuyos.
   - Sin el modo, sin la llave de la API o con 429: nada de esto sale (XP tal cual), el aviso de XP si algo no llega.
   - Pruebas con la API falsa: abrir, hojear, regresar en todos los niveles, la cuota (peticiones contadas por escena), 429 a media
     pared, apagado = XP tal cual.
5. **El modo noche también con Spotify** (pregunta del dueño del 15-11-2026): comprobar con una escena que una canción de Spotify (la
   fuente de `RocolaOs.SpotifyFuente.cs` por `ArmaCadena`) baja al `noche_pct` de noche y con «Probar», con su rampa, igual que una de XP.
6. **SP2 y SP4 en rocola-xp** (decisión del dueño del 15-11-2026, AGENTS.md): la canción de la rocola que suena peor y tiene pareja
   estricta en Spotify suena desde Spotify sin ningún cambio visible; la pareja se busca solo al entrar a la cola (cuota), se guarda en
   `rocola_spotify_pares.txt`; 6 s sin sonar = el MP3 sin aviso. Grupo «sustitucion» en su propia rama sobre junta-spotify.
7. **El asistente con lo nuevo de Spotify** (petición del dueño del 15-11-2026, AGENTS.md): grupo «asistente-spotify» después de juntar
   las paredes del buscador: guías nuevas del asistente con su mismo dibujo, solo con el modo Spotify; sin el modo, el de XP tal cual.
8. **«AYUDA» siempre en la casilla de la ayuda** (petición del dueño del 15-11-2026): con Rocola OS activo, `AyudaCasillaGpu` no va
   compacta en 1024x768 (la cabecera cede el sitio). Lo hace el grupo del asistente (es su casilla).
9. **Después del cumpleaños**: la ventana de Ctrl+F4 con la hoja «Pantalla» (resolución, hercios, colores del monitor, guardado para
   siempre, con prueba de 15 s y vuelta a lo automático). AGENTS.md, petición del 15-11-2026.

10. **Velocidad de todo lo de Spotify** (petición del dueño del 20-11-2026: «Cuando esté implementado todo lo relacionado a Spotify, busca todas las maneras posibles de que todo cargue lo más rápido que se pueda»): cuando estén juntados el núcleo, el buscador, las paredes del buscador, la sustitución y la pared con el catálogo, un grupo mide y acelera: buscar (de la tecla a los resultados), abrir un disco o la pared de un artista, las portadas (Spotify y Cover Art Archive: adelantar las de la página de al lado, conexiones aparte, caché), pedir y que empiece a sonar (de PONER al primer sonido; precargar la siguiente de la cola en Soloist si su API lo permite), el arranque del modo, y la pared de 500 páginas al hojear; sin gastar más cuota de la API ni frenar la música. Ideas probadas en la demo de Windows (herramientas/prueba-spotify-windows-soloist: portadas en puertos aparte, aviso previo de portadas, biblioteca guardada, buscador al momento) y en el programa anterior (AGENTS.md, petición del 7-11-2026 de optimizar). Medido antes y después, con números.
