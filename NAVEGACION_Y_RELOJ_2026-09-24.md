# Navegación y reloj — 24 de septiembre de 2026

Cambios compartidos en HD y Modest. Exclusivos del renderizador GPU, sin alterar el renderizador CPU.

- El tiempo del estado ampliado vuelve a **transcurrido / duración total**, por ejemplo `1:47 / 4:12`. Se elimina la cuenta «faltan…». El parche del reloj y la composición completa usan el mismo formato; una duración desconocida conserva el texto disponible.
- El buscador entra desde debajo de la pantalla y sube con frenado cúbico, sin cambiar su tamaño ni rebotar. Al cerrar baja acelerando. Continúa usando la textura existente y su reutilización al cerrar; no se agregan capturas ni reconstrucciones por fotograma. Los halos de sus controles acompañan la entrada.
- El paginador utiliza las mismas flechas amarillas dibujadas que los controles físicos, junto a ANTERIOR y SIGUIENTE. El input horizontal dispara su halo de 420 ms sin invalidar la textura de interfaz. En los límites la flecha no disponible queda atenuada.
- Arriba/abajo desplaza las páginas verticalmente; izquierda/derecha lo hace horizontalmente. El sentido depende de la entrada, no de si el número de página aumenta: el salto circular entre primera y última canción conserva la dirección pulsada. Los saltos laterales no son circulares, incluso si solo cabe una fila por página.
- Página, selección y página saliente usan una única lectura de posición por fotograma para evitar pequeñas diferencias entre capas. Se recortan tanto las coordenadas como las coordenadas de textura a la caja de canciones, en ambos ejes. Los caminos GPU sin vidrio también recortan el movimiento.
- Se respetan las opciones de animación existentes de Ctrl+F3; no se añade un interruptor de rendimiento ni un ajuste nuevo.

## Verificación

Compilación de ambas ramas: perfiles GPU y CPU con advertencias como errores; Release de los proyectos originales.

Pruebas ampliadas en `pruebas/paginasgpu.cs`: caché y firmas estables en ambos ejes a tres anchos; entrada real a través de MoveSongInput; saltos circulares, límites, animación desactivada, halo sin reconstrucción; curvas del buscador entre 480 y 2160 píxeles; formato del reloj; dibujo Direct3D9 HAL, recorte y UV en ambos ejes; una sola lectura de posición por fotograma.

También se ejecutan capascolor, cristalcola, estilo, respuesta y dibujoligero. Se revisa una imagen generada del paginador. Son pruebas automatizadas y revisión de componentes, no una medición de FPS de la sesión de música del usuario ni una prueba en Windows XP.

Los fallos preexistentes documentados en PAGINAS_Y_ESTADO_2026-09-24.md siguen fuera de esta pasada. No se modifican música, modelos, créditos ni carpetas instaladas.

Nota del 25/09: la casilla, lo elegido y (en el buscador con portadas) las tarjetas van en imágenes chicas aparte de la textura de la ventana; ahora suben y bajan con ella en los dos buscadores, con sus cintas, sin copiar nada (al cerrar, la ventana se queda con sus texturas y las suelta al acabar). Ver `PANTALLA_VIVA_BUSCADOR_2026-09-24.md`.
