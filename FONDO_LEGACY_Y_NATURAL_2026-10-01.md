# Fondo Legacy y Espacio natural — 1 de octubre de 2026

## Cómo elegirlo

En Ctrl+F3, abrir **Fondo → Estilo** y elegir **Legacy** o **Espacio natural**.
La elección se aplica al instante y se guarda al cerrar la ventana de control.
La hoja **Ajustes** conserva todos los mandos del fondo.

Legacy es el valor de fábrica. Los archivos antiguos sin la clave `estilo_fondo`, y los valores
no reconocidos, usan Legacy. La nueva preferencia admite `legacy` y `natural`.
Cambiar de estilo no cambia la paleta musical, los interruptores individuales ni los FPS de ondas/cubos.
**Fondo como de fábrica** restaura Legacy y las opciones originales del fondo.

## Qué cambia Espacio natural

- Cielo negro/azul más profundo. La paleta musical no tiñe de rojo/rosa todo el cielo alto:
  sigue fluyendo en la aurora y los elementos reactivos.
- Nebulosas neutras con regiones separadas, vetas orgánicas y huecos transparentes.
  Cielo profundo y su deslizador siguen controlando el detalle.
- Vía Láctea más delicada, con canales de polvo por ausencia de luz, sin otra capa negra.
- Estrellas con núcleo fino, centelleo más moderado y destellos reservados a pocas luces grandes.
  Se conservan la distribución, la deriva, la densidad y el relevo de patrones.
- Fugaces con cola afilada y cabeza luminosa, sin añadir partículas o triángulos.
- Aurora con mayor contraste entre filamentos y un contorno luminoso más fino/difuso;
  conserva las ocho bandas, la paleta dinámica, la unión al estado y los ajustes 24/50/60 FPS.
- Velos de luz que evolucionan a distintas alturas, con seis franjas de distribución
  no lineal y una tabla de ondas precalculada. No añade geometría ni texturas.
- Estrella protagonista del icono con material plata/acero, reflejos claros y tinte musical sutil.
  Conserva su movimiento suave; no añade desplazamientos bruscos al ritmo de la canción.
- Ecos de colores, corona y polvo orbital más delicados, sin aumentar sus cantidades.
- Viñeta de borde más suave. El grano contra bandas, las transiciones de reposo/navegación
  y los ajustes individuales existentes siguen activos.
- Halos de selección/género elípticos y proporcionales a las carátulas, con brillo más equilibrado.
  En Natural las nebulosas mantienen el perfil azul nocturno también sin Nebulosas cine;
  ese mando sigue modificando su presentación y Legacy conserva sus tintes originales.

La interfaz, el sonido y el renderizado CPU no se rediseñan en esta pasada.
Las imágenes conceptuales anteriores eran propuestas; las capturas de prueba de esta implementación
son dibujos reales de Direct3D 9 con bandas sintéticas, no fotografías ni reproducción de una biblioteca.

## Coste y cachés

Se reutilizan las pasadas existentes. Aurora: 8640 vértices de relleno y 7992 de luz,
dos envíos. Campo: máximo 1400 estrellas, tres capas. Protagonista: veinte caras,
hasta doce ecos. Fugaces: cuatro triángulos. No hay desenfoque de pantalla completa,
ray tracing ni nuevos shaders.

Las texturas de nebulosa y Vía Láctea conservan un cuarto de la resolución de ventana.
La caché RAM tiene solo dos mapas por familia (uno por estilo), invalidados por tamaño,
detalle y fuerza según corresponda. Solo hay una textura GPU activa por familia.
Los atlas estelares usan como máximo 128 KiB extra en RAM, preparados de forma perezosa;
un fallo de subida conserva el atlas válido y reintenta con espera, sin trabajo caro por cuadro.
Cerrar libera mapas y texturas.

El primer uso de un estilo sí prepara sus mapas; no es coste cero. Corrección de la medida inicial:
el horneado conjunto frío de nebulosa y Vía Láctea naturales a 860×360 se midió aproximadamente
en 75 ms HD / 108 ms Modest en este PC; los 37/52 ms publicados inicialmente no representaban la pareja.
La reutilización evita repetirlo al volver a un estilo ya preparado con los mismos ajustes.
Desde la segunda pasada, Natural prepara esa pareja en un trabajador de prioridad mínima:
captura tamaño/detalle/fuerza, cancela resultados obsoletos por filas y entrega los mapas al hilo
de pantalla, que es el único que toca Direct3D. Abrir Ctrl+F3 con GPU activa adelanta la preparación.
En equipos de hasta dos hilos se cede tiempo entre lotes; equipos mayores no reciben ese descanso
adicional. Hay como máximo una preparación activa, una petición pendiente y un resultado listo,
sin colas crecientes. Los mapas temporales se liberan al cancelar o cerrar, sin esperar con Join.
Cambiar detalle/fuerza no rehace la Vía Láctea; nebulosas apagadas no generan sus mapas naturales.
Mientras se prepara se conserva la textura anterior. En un arranque frío directamente con Natural,
el cielo se dibuja primero y las nebulosas aparecen al terminar; no se promete coste cero de arranque.
El grano común conserva su receta y los reintentos de sus subidas no bloquean otras capas.
Legacy conserva su preparación síncrona original; no se modifica su renderizado.
El aumento aislado de cálculo del campo estelar fue de unos 0,003 ms, medido con reflexión.
Estas cifras no son FPS de la Rocola con biblioteca y música.

## Verificación

En ambas ramas:

- Compilación Release y compilación estricta GPU/CPU.
- Suite de capas ampliada: controles, configuración, audio, carga de portadas y regresiones.
- Cuarenta y cinco baterías por rama, incluyendo HAL real. En la fixture del fondo se mantienen
  quince envíos a 1024×768 y veintidós a 3440×1440. Coste calentado de construcción/envío:
  aproximadamente 0,24–0,31 ms HD / 0,37–0,42 ms Modest para Natural en esta pasada,
  sin Present ni readback en el cronómetro; no implica igualdad de FPS reales.
- Comparación contra los Release anteriores: mapas, atlas, geometría de aurora,
  posiciones y colores Legacy conservados exactamente.
- Catorce métodos del renderizador CPU con instrucciones/operandos idénticos.
- Capturas HAL a 1024×768 y 3440×1440, cambios de estilo, cachés y liberación.
- Mismas cantidades de estrellas, caras y ecos; no se añaden envíos a pantalla completa.
  Las teselas transparentes pueden reducir geometría según el mapa.
- Pruebas de atlas: fallo temporal, ausencia de reintentos por cuadro, recuperación,
  reutilización y retorno exacto a Legacy.
- Nuevas baterías de velos/halos y preparación: claves inmutables, cancelación, último pedido,
  prioridad mínima, cierre sin bloquear, fallos con espera y resultados de mapas equivalentes.
  El sobrecoste aislado de los velos es del orden de 0,025 ms por muestra en este PC,
  con los mismos dos envíos de aurora. No es una medida de FPS de la aplicación.
- 174 comprobaciones HAL por rama: preparación fría, cambio de tamaño/detalle/fuerza,
  grano con espera simulada independiente de las nebulosas y reinicio del dispositivo.
  La batería del preparador también se ejecuta restringida a dos hilos por afinidad:
  comprueba esa ruta de presupuesto, pero no simula la velocidad de un procesador antiguo.

Los ensayos HAL son fixtures sintéticas y no prueban FPS reales con una biblioteca grande.
Modest mantiene el destino .NET 3.5/Windows XP; no se dispone de XP para verificarlo en un equipo real.
