# Fase 11: la hoja «Spotify» de Ctrl+F4

Mejora que el dueño eligió el 27-11-2026. Solo Rocola OS con `ROCOLA_OS` (sin él, Ctrl+F4 no hace nada); la hoja existe aunque el modo Spotify
esté apagado y lo dice. Sin ganchos nuevos (se registra con `RocolaOsHojitas.Registra`, orden 16, entre Sonido y Archivos).
`RocolaOs/RocolaOs.VentanaSpotify.cs`. Mismo aspecto de Pantalla y Sonido (tarjetas, rótulos, deslizador y botón planos de XP, 960x580).
Solo se VE (se refresca cada medio segundo), salvo dos mandos seguros. NUNCA enseña una llave ni nada de la cuenta (no lee esos archivos).

## Lo que enseña

- **SOLOIST Y LA CUENTA**: el vigilante `rocola-soloist` (fase, de `/run/rocola-soloist/estado`: en marcha, arrancando, bajando, caducó...), la
  versión, los **días que le quedan** a su build (verde; ámbar bajo 21; rojo a 7 o menos) y cuándo caduca; si la rocola está **conectada** a
  Soloist (WebSocket), si está **emparejado** (sesión), la bocina (y si es la activa) y qué hace (tocando, pausa...); y la API web: estado (bien,
  frena 429 con segundos, sin red, falla, sin llave), si hay llave de la app y cuenta, **último código**, **peticiones de la última hora**, último
  éxito y último 429 (UTC). Sale de `RocolaOsSpotifyCatalogoApi.TextoEstado()` (el mismo texto del buzón, de memoria). Desde la revisión del 27-11-2026 la hoja no lee disco a
  cada vuelta de 0,5 s: el archivo del vigilante y la cola de la bitácora se recuerdan y se releen cada 5 s o si cambió su tamaño o fecha.
- **LO PEDIDO**: cobradas, devueltas, no pedidas y en cola de esta sesión, y las **últimas 7 canciones** de Spotify (hora, «acabó» o «DEVUELTA»
  con el motivo y los segundos que sonó, o «NO PEDIDO»; de las últimas líneas de `rocola_spotify.txt`).
- **LA CACHÉ**: la memoria en disco de lo que contestó Spotify (`RocolaOs.SpotifyCatalogoMemoria.cs`): archivos, peso (tope 48 MB), aciertos,
  vencidas servidas y guardadas. Botón **Vaciar la caché**: borra solo esos archivos (se vuelven a pedir solos; no toca la cuenta ni nada más);
  en gris sin archivos. Cada archivo guardado termina con una línea de cierre `fin<TAB>N` (revisión del 27-11-2026): uno truncado por un apagón no se acepta, se borra y se vuelve a pedir.
- **LA PARED**: mando «Páginas por letra en la pared» (`pared_paginas=` de `rocola_os.txt`, de 10 a 1000, 100 de fábrica): 29 pasos (de 10 en
  10 hasta 100, de 50 en 50 después), flechas o ratón; se guarda a los 0,5 s o al cerrar con `RocolaOs.PonAjuste`. Debajo, lo que ocuparían las
  portadas como mucho (páginas x 18 discos x 27 letras x ~15 KB; con 100, ~712 MB) y que el paquete de portadas trae unas 60 000. «Vale en un
  minuto o al reabrir la letra» (`RocolaOsPgPaginasMax` relee la clave cada minuto; no se rearma la letra desde la hoja). La clave se DECLARA en
  `RocolaOs.ParedGeneros.cs` (una línea `Claves` de la función «pared»: sin declararla `PonAjuste` la rechaza; si la junta ya la declara allí,
  que quede una sola).

## Pestañas y pruebas

Las pestañas van pegadas a la derecha: Pantalla (606,54), Sonido (712,54), **Spotify (818,54)**, Archivos (924,54) con 1024x768. Con la pestaña
nueva, hasta la barra de la resolución de la hoja Pantalla hay un Tab más: `Pruebas/RocolaOs/pantalla/prueba.sh` (5 Tab) y
`Pruebas/Qemu/pantalla.py` (5 Tab); `salida-audio` hace clic en Sonido en x=712. Escena `hoja` de `Pruebas/RocolaOs/catalogo/prueba.sh` (vigilante y
bitácora sembrados; captura a 1024x768; Tab x3 hasta el deslizador y dos flechas: `pared_paginas=200`). No probado con Soloist real ni en el A4.

**Revisión del 27-11-2026 (páginas por letra)**: el texto de la hoja decía «vale en un minuto o al reabrir la letra» y no era cierto (las letras armadas
no se rearmaban). Ahora `pared_paginas` se lee cada 2 s, entra en la firma de las letras de la pared (`RocolaOsPgGenSig`) y la letra que se mira se
rearma de inmediato (las demás en memoria, al visitarlas); si el disco elegido deja de caber, la elección se queda en el último que queda.
