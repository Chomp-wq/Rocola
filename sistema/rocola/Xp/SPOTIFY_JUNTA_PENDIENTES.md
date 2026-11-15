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
