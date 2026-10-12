# Barrido de fallos visuales y de uso — Modest, 12 de octubre de 2026

Petición del usuario: «Haz una tanda buscando bugs, glitches gráficos y errores de interfaz, cosas que arruinen la
experiencia visual y de uso».

## Cómo se buscó

- **Ctrl+F3:** la ventana real, sus 6 páginas y todas sus hojas. Se fotografiaron y se midieron:
  - textos cortados;
  - mandos encimados;
  - cosas fuera de la ventana o debajo del botón Listo.

  Se midió con la letra de Linux y con otra más ancha, como referencia de XP.
- **La pantalla principal:** la rocola entera, con una biblioteca de nombres difíciles (muy largos, con acentos, con
  Ñ). Se fotografió en 13 estados: la pared, cambiar de página, la cola llena, discos largos, el buscador, avisos
  largos, sin créditos y atracción.

  Aquí no hay tarjeta gráfica: esas fotos salen por el dibujo por CPU. Sirven para textos y posiciones, no para ver
  cómo luce el bar.
- **La franja de abajo, tal como la arma la tarjeta.** Su imagen se hace con GDI+, y eso sí corre aquí. Se probó con
  títulos y artistas larguísimos en 800, 1024 y 1366 de ancho.
- **Revisión del código:** los avisos, la línea de estado y el video en ventana.

## Lo que se arregló

### 1. Con un video en ventana, los avisos quedaban tapados

El cuadro del video es una ventana de Windows que va **encima** del dibujo de la tarjeta: lo que queda debajo no se ve.

- El aviso de siempre («Pusiste…», «Inserta una moneda», «No se pudo reproducir…») salía centrado: la mitad derecha
  quedaba detrás del video.
- El «aviso que se entiende» salía a la derecha: quedaba entero detrás.
- La invitación a meter una moneda salía centrada: quedaba a medias.

Ahora, con un video en ventana, los tres van enteros en la parte que se ve, a la izquierda del video. Sin video, salen
como siempre: el aviso de siempre sale idéntico píxel a píxel.

### 2. Avisos que se quedaban para siempre en la línea de estado

- **«Encontradas N canciones…» podía quedarse puesto** después de leer la biblioteca: un aviso de avance que llegaba
  tarde pisaba el letrero ya limpio. En este Linux pasaba siempre; en Windows es menos probable, pero también queda
  cubierto.
- **Los avisos de error se quedaban hasta el día siguiente**, a veces con el mensaje técnico de Windows: «No se pudo
  reproducir … (mensaje de Windows)», «Video (WMP): …» y «Aviso al reproducir: …». Solo se borraban al volver a leer la
  biblioteca. Ahora:
  - se van solos a los 15 segundos;
  - el de tres fallos seguidos, al minuto;
  - el detalle sigue en `rocola_error.txt`, y el cliente ya recibió su aviso.
- **«Sin salida de sonido…» seguía puesto con las bocinas ya de vuelta.** Mientras se reintenta, sigue; cuando ya no
  hace falta, se va solo.
- Solo se borran si nadie puso otro letrero encima entretanto.

### 3. La franja de abajo: el artista largo se cortaba a media palabra

En 800×600 (y en 1024 si era muy largo), el nombre del artista se cortaba en seco donde empieza «En cola», por ejemplo
«…Sinfonica de». Ahora termina en «…», igual que el título.

### 4. Ctrl+F3

- **Pantalla → Animación del asistente** decía «Rojo + verde 3 segundos: ayuda», el atajo viejo. Ahora dice: «La
  ayuda se abre con ROJO + ABAJO o con su casilla».
- **La fecha de la versión no se veía nunca.** «Esta rocola es del…» estaba debajo del borde de la ventana desde que
  esta mide 732 px. Ahora va en la barra de título, en todas las páginas.
- **Dos notas llegaban hasta debajo del botón Listo:** en Imagen, lo que se pierde con cada nivel; en Colores, la nota
  del pie. Ahora terminan antes del botón.
- **Dos textos se cortaban con una letra más ancha**, como la que puede usar XP (Tahoma): el aviso «Esta rocola NO
  está dibujando con la tarjeta gráfica» y la nota de los perfiles de carátulas. Se acortaron.
- **El DSEE no disponible** decía «No disponible en este equipo:» y nada más cuando no había motivo. Ahora termina en
  punto.

## Lo que se miró y está bien

- La pantalla principal en las resoluciones de siempre:
  - la fila de arriba y BUSCAR/AYUDA, también encima del video;
  - las letras;
  - los títulos largos, con «…» en la lista del disco y en la franja;
  - los avisos largos.
- Ctrl+F3, después de los arreglos: ni con la letra ancha queda nada fuera, cortado ni debajo de Listo.
- Las pistas del rojo, la ficha rápida del disco y la portada que crece no caen debajo del video.

## Lo que no se tocó

- **En las fotos de este Linux faltan cosas:** el dibujo por CPU no pinta todos los discos de la pared, y el buscador
  sale a medias. Es el dibujo por CPU en Mono (congelado por regla), no el de la tarjeta del bar.
- **La batería `estilo` falla igual que antes.** Espera un diseño anterior de la tarjeta «A continuación». En la imagen,
  el artista y MEJORADA no se pisan con la tarjeta.
- **Las canciones sin etiqueta conservan el «01 - » del nombre del archivo,** como siempre. Con etiqueta ID3 no pasa.

## Cómo se comprobó

- **Pruebas automáticas nuevas:**
  - `ventanaf3` (2.125 comprobaciones): la ventana de verdad, todas sus páginas y hojas, con dos letras. Contra el
    ejecutable de antes, detecta todos los fallos de Ctrl+F3 de arriba.
  - `avisosvideo` (19): los avisos con video en ventana.
  - `lineaestado` (13): el avance tardío del escaneo y los avisos de error que caducan.
  - `asistentecombo` comprueba además que ningún texto anuncie el acorde viejo.
- **Siguen pasando:** `ajustes`, `detalles`, `hojasfondo`, `efectoslayout`, `ajustesfluidez`, `pestanas`, `guiarojo`,
  `subgrave`, `silencios`, `noche`, `suenavivo`, `franjareloj`, `textosgpu`, `buscaportadas`, `filabaja`, `salida`,
  `videoaudio`, `transiciones` y `sinpausa`.
- **Legacy y CPU sin cambios:** la huella de Legacy es idéntica, y los 14 métodos del dibujo por CPU también.
- **No comprobado:** en la máquina del bar, con un video real y la tarjeta gráfica de verdad.
