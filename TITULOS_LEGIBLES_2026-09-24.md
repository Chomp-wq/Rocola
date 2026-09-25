# Títulos legibles y prefijos de pista — 24/09/2026

Aplicado a HD y Modest, exclusivamente en la presentación GPU. No cambia archivos,
etiquetas, rutas, índices de búsqueda, orden, audio ni el aspecto del renderizador CPU.

## Comportamiento
- Se ocultan prefijos explícitos de pista como `03 - Dreamer`, `02-Crazy Train`,
  `-07 - Hellraiser` o `01. Título`. Se conservan títulos como `99 Luftballons`,
  `1979`, años de cuatro cifras y números decimales. No se eliminan números separados
  solamente por espacios: no hay una señal suficientemente clara de que sean pistas.
- Los nombres largos de la barra compacta (título, artista y «A continuación»),
  la cola ampliada y el artista del panel ampliado se deslizan.
- En el interior con capas de vidrio GPU, se deslizan todos los títulos largos de
  la página visible, incluida la selección. Las capturas de la página saliente
  conservan texto estático durante la transición.
- La vista previa del resultado seleccionado del buscador desplaza título y subtítulo.
  Las tarjetas de resultados mantienen su resaltado de coincidencias y su recorte;
  el texto completo se lee en la vista previa al seleccionar el resultado.
- El título principal ampliado conserva su desplazamiento anterior.
- Pausa inicial de 2,2 segundos, recorrido suave, pausa final de 1,4 segundos y
  regreso suave; se repite. Los textos que caben permanecen quietos.
- Ctrl+F3 → Pantalla → La atracción y el estilo → **Títulos largos deslizantes (GPU)**.
  Preferencia persistente `textos_deslizantes=`. Este control gobierna las nuevas
  cintas; el título principal ampliado mantiene el comportamiento que ya tenía.
- El artista compacto reserva espacio para género, MEJORADA y siguiente canción.

## Rendimiento y límites
El texto se rasteriza al cambiar contenido, fuente, espacio o color. Moverlo no
rehace la barra, la página ni sus texturas: se desplazan y recortan coordenadas GPU.
La caché sobrevive a los cambios del reloj y los colores no reinician el recorrido.
Tiras de hasta 1024 píxeles, 48 textos y 2.097.152 píxeles por grupo (8 MiB de imágenes
como máximo por grupo). Hay cinco grupos. Metadatos anormalmente largos mantienen
el recorte estático. Se liberan las imágenes retiradas y las texturas al parar GPU.

La ventana de texto y el límite exterior de la lista se recortan por separado,
también durante transiciones verticales y horizontales. No se capturan filas fuera
del bitmap de selección.

## Verificación realizada
- Compilaciones Release de ambas ramas; además perfiles GPU y CPU con sus compiladores
  .NET 4 y .NET 3.5, sin avisos tratados como errores.
- `textosgpu`: 631 comprobaciones por rama; caché, pausas, límites, 12 canciones,
  selección, prefijos, desactivación y liberación.
- `estilo`, `capascolor`, `cristalcola`, `respuesta` y `dibujoligero`: pasan.
- Inspección de imágenes de componentes a 800 px; también se generan muestras de
  1024 y 1920 px. Son composiciones de componentes, no capturas del programa abierto.
- `paginasgpu` avanza por sus comprobaciones de caché pero no completa la fase
  Direct3D: **HAL no disponible en esta sesión**. No se da por aprobada esa batería.

Pendiente: comprobar reproducción y movimiento real en el equipo del usuario,
con cambios de página, apertura/cierre del buscador, expansión y vuelta de reposo
y distintas resoluciones. No se afirman FPS reales ni compatibilidad probada en XP.
No se copiaron ejecutables a las instalaciones con música.
