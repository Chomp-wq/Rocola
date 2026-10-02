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
- Estrellas con polvo de luz estable, centelleo más moderado y puntas en las medianas y grandes.
  Se conservan la distribución, la deriva, la densidad y el relevo de patrones.
- Fugaces con cola afilada y cabeza luminosa, sin añadir partículas o triángulos.
- Aurora con mayor contraste entre filamentos y un contorno luminoso más fino/difuso;
  conserva las ocho bandas, la paleta dinámica, la unión al estado y los ajustes 24/50/60 FPS.
- Velos de luz que evolucionan a distintas alturas, con seis franjas de distribución
  no lineal y una tabla de ondas precalculada. No añade geometría ni texturas.
- Estrella protagonista del icono con material plata/acero, reflejos claros y tinte musical sutil.
  Conserva su movimiento suave; no añade desplazamientos bruscos al ritmo de la canción.
- Ecos de colores, corona y polvo orbital más delicados, sin aumentar sus cantidades.
- Viñeta de borde más suave y grano contra bandas reutilizado con modulación más delicada. El relevo de patrones usa
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

### Corrección de estrellas y cuerpo del cielo — 2 de octubre

La primera versión de Natural resultaba demasiado vacía: casi todas las medianas usaban
la forma de punto y el atlas compartía un núcleo muy estrecho con el polvo pequeño.
Al reducirlo a pocos píxeles, el filtrado lineal no bastaba: una estrella podía casi desaparecer
entre centros de píxel y volver a encenderse al desplazarse. No era el centelleo musical previsto.

El polvo tiene ahora su propio perfil de cobertura dentro de la misma casilla del atlas,
con un tamaño continuo mínimo suficiente para mantener su luz al derivar. Las medianas y grandes
recuperan cuatro puntas, diferenciadas por intensidad, con núcleo y halo suaves.
No se redondea su posición ni se paraliza el movimiento. El centelleo suave sigue activo.

Las tres regiones de nebulosa son más amplias y su nube base tiene más cuerpo; la Vía Láctea
conserva canales oscuros y gana relieve. El cielo alto sigue azul/negro, sin teñirse entero de
la paleta de la canción. Se mantienen los topes de alfa, los vacíos y todos los mandos existentes.
Con fuerza de fábrica, el mapa de nebulosa pasa de alfa medio 5,06 a 7,54 y la Vía Láctea de
0,43 a 1,20 en la muestra de 256×192. No se cambian las preferencias guardadas.

La fixture HAL añade 32 posiciones fraccionarias, cinco tamaños y los dos estados de Estrellas
finas, con centelleo y latido congelados. Mide luz integrada, no la intensidad de un píxel individual:
la amplitud relativa `(máximo-mínimo)/media` pasaba de más de 200% en el polvo, llegando a 908%;
con la corrección queda por debajo de 8% en los diez casos de este PC. El límite de regresión
es 20%. Las láminas se leen del backbuffer real, sin suavizado posterior.

Se conservan el atlas de 128×128, las cantidades, mallas, capas y envíos. El mayor cuerpo de
algunas luces cambia su cobertura de píxeles: no se promete coste GPU idéntico ni FPS universales.
No hay más estrellas, partículas, pasadas de pantalla completa, ruido aleatorio por cuadro ni
asignaciones nuevas en el bucle estelar. Legacy y el renderizado CPU permanecen intactos.
Esa pasada contó con 2425 comprobaciones HAL por rama, además de las 46 baterías.

### Distribución Legacy y acabados propios — segunda revisión del 2 de octubre

El aumento del polvo visible no venía de crear más estrellas: ambas opciones ya compartían
la semilla y la distribución. El nuevo perfil antialias, junto con un cuadrilátero más amplio,
sumaba hasta unas cinco veces la luz de un punto Legacy pequeño. El cielo parecía sobrepoblado.
Ahora se compensa esa cobertura por su área, sin encoger el núcleo hasta recuperar el parpadeo.
Medianas y grandes también equilibran su luz respecto a las dos formas Legacy correspondientes.
Se conservan los centros, cantidades, densidad elegida, grumos, vacíos, tamaños base, formas,
temperaturas, fases y tres profundidades de deriva. No se descartan estrellas ni se cambian
las preferencias del usuario. Natural mantiene un centelleo y pulso de tamaño más contenidos.

La comprobación HAL compara los dos estilos con 32 posiciones fraccionarias, seis tamaños
incluyendo las grandes y ambos estados de Estrellas finas. En este PC la luz media Natural
queda entre 96,8% y 102,4% de Legacy, con oscilación por deriva de como máximo 8,0%.
Las guardas de regresión exigen un rango de 85–115% para luz y menos de 20% de oscilación.
Se compara luz integrada, no una captura elegida ni el brillo de un único píxel.

Se revisaron todos los efectos del fondo. Natural no es una versión con menos funciones:

| Efecto | Acabado Legacy | Acabado Espacio natural |
| --- | --- | --- |
| Bóveda | Degradado azul original | Perfil nocturno negro/azul de tres alturas |
| Nebulosas | Receta original, con o sin detalle y tinte cine | Regiones orgánicas, filamentos, huecos y tinte nocturno propio |
| Vía Láctea | Banda original | Banda con canales de polvo oscuro y relieve |
| Campo estelar | Atlas original | Mismo patrón y jerarquía, núcleos suaves estables y puntas ópticas propias |
| Centelleo y relevo | Intensidad y mezcla originales | Centelleo moderado, mezcla smoothstep sin apagar el campo |
| Fugaces/bólidos | Cola original | Cola afilada, cabeza clara y matiz dorado de bólidos |
| Cortina de aurora | Distribución lineal original | Seis alturas no lineales con velos luminosos que evolucionan |
| Rayos y ondas | Contraste original | Filamentos más definidos y disipación propia, conservando las ocho bandas musicales |
| Filo, horizonte y suelo | Neón original | Contorno fino/difuso y caída de luz propia, unidos a la barra |
| Estrella protagonista 3D | Material coloreado original | Plata/acero, reflejos por normales y tinte musical sutil |
| Estrella protagonista 2D | Textura facetada azul original | Nueva textura plata/acero, bisel fino y reflejos contenidos, también sin detalle |
| Corona | Halo original | Corona más concentrada con luz blanca/azulada |
| Polvo orbital | Tamaño/luz originales | Tamaño y luminosidad por profundidad, delante y detrás del cuerpo |
| Ecos de la estrella | Caída y borde originales | Curva y borde propios, con la misma paleta musical dinámica |
| Viñeta y grano | Recetas originales | Borde más suave y grano más delicado; la misma teja evita ruido por cuadro |
| Entrada/salida de reposo | Curva original | Amortiguación crítica y luz interpolada, sin desunir los elementos |
| Halo de selección/género y vidrio | Acabado original | Halo elíptico, cristal oscuro, borde fino y cachés separadas por estilo |

La segunda revisión completa el material de la estrella 2D y el tratamiento del grano,
que antes compartían su receta visual. La geometría, los relojes, la música, la paleta y los
mandos comunes siguen compartidos: duplicar esos motores no mejoraría el aspecto y pondría
en riesgo funciones y rendimiento. Cambiar entre estilos actualiza la textura 2D existente;
ni el color musical, ni la resolución, ni los cuadros estables vuelven a subirla.
No hay estrellas, partículas, caras, ecos, pasadas o interruptores nuevos. Legacy conserva
sus píxeles originales, incluida la estrella 2D; el renderizado CPU sigue congelado.

La fixture HAL ampliada cuenta 5617 comprobaciones e incluye capturas con protagonista
en 2D y 3D a 1024×768 y 3440×1440. La batería estelar verifica además la distribución exacta
en tres resoluciones, tres densidades y los dos modos de Estrellas finas, y contrasta Legacy
con el Release anterior. Se repiten las 46 baterías en ambas ramas. Las capturas son ensayos
aislados de Direct3D, no FPS certificados con biblioteca/música ni una prueba real de XP.

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
