# Fondo Legacy y Espacio natural — 1 de octubre de 2026

## Cómo elegirlo

En Ctrl+F3, abrir **Fondo** y elegir **Legacy** o **Espacio natural** en el selector
que permanece visible en la parte superior.
La elección se aplica al instante y se guarda al cerrar la ventana de control.
Las hojas **Cielo y aurora**, **Interfaz** y **Estrella** conservan todos los mandos del fondo.

Legacy es el valor de fábrica. Los archivos antiguos sin la clave `estilo_fondo`, y los valores
no reconocidos, usan Legacy. La nueva preferencia admite `legacy` y `natural`.
Cambiar de estilo no cambia la paleta musical, los interruptores individuales ni los FPS de ondas/cubos.
**Fondo como de fábrica** restaura Legacy y las opciones originales del fondo.

## Organización de Fondo — corrección del 2 de octubre

El selector anterior quedaba detrás de las tarjetas de ajustes: podía tener `Visible=true`
y aun así no verse ni recibir clics. El selector ocupa ahora una fila independiente,
siempre a la vista. Debajo hay tres hojas con dos tarjetas cada una:

- **Cielo y aurora:** nebulosas, estrellas, ondas y paleta musical; detalle natural y fuerza.
- **Interfaz:** barra, cola, progreso, cristal del interior, transiciones y respuesta al toque.
- **Estrella:** superficie, corona, polvo, ecos, 3D y comportamiento en reposo.

Se reutilizan los mismos veintiocho interruptores y sus eventos; no se añaden preferencias
ni se pierden funciones. Cambiar de hoja no modifica valores y cambiar de estilo no los restablece.
El botón de fábrica y Listo siguen disponibles. Las notas del pie ya no se superponen a Listo.

Fondo usa un área cliente de 960×694, aprovechando el espacio superior sin reducir las letras
ni los controles. Su marco cabe a escala estándar en 1024×768. Al salir de Fondo se restauran
el tamaño y la posición de Listo originales de las otras secciones; se oculta la fecha del pie
solo en Fondo. El título de la ventana también identifica la sección seleccionada.

La prueba `estilosconfig` verifica límites, textos, ausencia de solapamientos, orden de teclado,
persistencia, retorno entre hojas y el control que realmente recibe el clic (`GetChildAtPoint`),
no solo la propiedad Visible. Una ventana de prueba aislada con los controles reales permite
comprobar las tres hojas mediante clics sin abrir la biblioteca ni guardar preferencias del usuario.
También se repiten las cuarenta y seis baterías GPU/CPU y HAL en ambas ramas.
Esto no certifica escalas de pantalla arbitrarias, FPS con música ni ejecución real en XP.

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
- Viñeta de borde más suave y grano contra bandas reutilizado. El relevo de patrones usa
  una mezcla suavizada sin ampliar su duración ni dibujar más estrellas.
- Reposo Natural con amortiguación crítica: panel, horizonte, cubo, estrella y controles comparten
  la misma posición animada. Invertir el destino mantiene la velocidad; se vuelve más deprisa
  a los controles que al reposo. La luz estelar interpola el latido con el reloj GPU.
- Halos de selección/género elípticos y proporcionales a las carátulas, con brillo más equilibrado.
  En Natural las nebulosas mantienen el perfil azul nocturno también sin Nebulosas cine;
  ese mando sigue modificando su presentación y Legacy conserva sus tintes originales.

El vidrio de la lista GPU también tiene acabado Natural: oscuro y ligeramente transparente,
esquinas más redondas, reflejo contenido y borde fino. Mantiene texto, distribución y funciones.
Se hornea por tamaño/estilo; cambiar de color solo modula la textura neutral, sin rehornearla.
El proveedor por capas y el compuesto invalidan correctamente al cambiar de estilo.
El polvo orbital ya pasaba delante y detrás de la estrella; Natural añade tamaño y luminosidad
continuos según profundidad, reutilizando el seno de la órbita y las mismas dieciocho motas.

El sonido y el renderizado CPU no se rediseñan en esta pasada.
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
- Cuarenta y seis baterías por rama, incluyendo HAL real. En la fixture del fondo se mantienen
  quince envíos a 1024×768 y veintidós a 3440×1440. Coste calentado de construcción/envío:
  aproximadamente 0,24–0,31 ms HD / 0,37–0,42 ms Modest para Natural en la segunda pasada,
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

## Lista original y cierre del 2 de octubre

La propuesta era “espacio profundo con luz natural”, no cambiar la distribución de carátulas.
Todos sus apartados quedan cubiertos por la alternativa Natural y las funciones existentes:

| Idea propuesta | Implementación GPU y límites |
| --- | --- |
| Cielo azul/negro profundo | Perfil CieloEstilo; la paleta musical no recolorea toda la bóveda. |
| Nebulosas, velo y Vía Láctea | Mapas orgánicos con huecos y filamentos, preparados fuera del hilo de pantalla. |
| Estrellas y profundidad | Atlas de núcleos finos, tamaños y blancos variados; las tres capas de deriva existentes. |
| Centelleo, pulso y destellos | Intensidad moderada y cruces/rayos selectivos; sin aumentar estrellas. |
| Fugaces y bólidos | Cabeza definida, cola afilada y progresiva; las fugaces anchas son doradas, cuatro triángulos. |
| Relevo, viñeta y grano | Mezcla smoothstep dentro de los 1300 ms originales, viñeta Natural y grano común contra bandas. |
| Aurora y ondas | Velos a seis alturas no lineales, rayos y pliegues; mismas ocho bandas y cadencia seleccionada. |
| Filo, horizonte y suelo | Núcleo fino, resplandor suave y disipación; misma coordenada que la barra, incluso animándose. |
| Estrella protagonista | Icono de veinte caras plata/acero, reflejos por normales y cuerpo sólido. |
| Corona y polvo orbital | Halo contenido, órbitas inclinadas con profundidad de luz/tamaño y orden delante/detrás. |
| Estela | Hasta doce copias con tinte dinámico y caída suave por edad; protagonista nítido. |
| Respiración y calma al navegar | Respiración existente más centelleo moderado e interpolación de luz estelar entre latidos. |
| Revelado en reposo | Una posición amortiguada compartida por panel, horizonte, cubo, estrella y controles. |
| Halos de selección/género y vidrio | Halos elípticos concentrados, cristal oscuro y bordes delicados; cachés conservadas. |
| Colores dinámicos | La paleta existente sigue viajando por aurora, protagonista y ecos; el cielo alto conserva la noche. |

No se añaden capas de pantalla completa, shaders, partículas, caras, ecos ni preferencias.
Legacy conserva su receta, su curva de reposo y su vidrio original. Los interruptores visuales
existentes siguen gobernando cada efecto y no se cambian al seleccionar un estilo.

La nueva batería acabadofinalnatural comprueba mezcla de patrones, reposo a 24/50/60/180 Hz,
inversión de destino, interpolación de luz, profundidad de polvo y caché de vidrio en sus dos
proveedores. Compara píxel a píxel el vidrio Legacy con el Release anterior cuando se le pasa
como tercer argumento. No abre Form1 real ni toca música, créditos o preferencias.

Las capturas y los tiempos HAL siguen siendo fixtures sintéticas: no certifican los FPS con
una biblioteca grande y música ni igualdad de rendimiento en todos los equipos. Lo que se
verifica es el presupuesto de geometría/envíos y la ausencia de trabajo nuevo caro por cuadro.
