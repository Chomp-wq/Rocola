# Rocola

## Pantalla viva, tanda 6: el buscador con portadas — 24 de septiembre de 2026

La idea "Teclado compacto y resultados con portada", solo en el dibujo por tarjeta gráfica y en las dos ramas. El teclado
queda compacto y siempre a la vista, a la izquierda; a la derecha los resultados salen en dos columnas de tarjetas grandes
con su portada, qué son (artista, álbum o canción), lo tecleado resaltado y SONANDO / EN COLA; y arriba hay atajos, solo con
datos de verdad: lo más pedido hoy, videos, karaoke y las dos últimas búsquedas. Las tarjetas que llegan entran una tras
otra, y si no caben todas se baja fila a fila. Todo sube y baja con la ventana del buscador, y lo quieto (resplandores y
fondos) va horneado: moverse por el teclado cuesta menos que en el buscador de siempre. Interruptor en Ctrl+F3 → Pantalla
→ **La atracción y el estilo** → **EL BUSCADOR**. Sin tarjeta, o con el interruptor apagado, el buscador sale idéntico al
de antes (comprobado píxel a píxel). No se probó en una tarjeta real ni en XP.
[La guía de la tanda](PANTALLA_VIVA_BUSCADOR_2026-09-24.md).

## El buscador, más ligero para la tarjeta — 24 de septiembre de 2026

Revisados todos los apartados del buscador (buscar, teclear, moverse, posar, avisos, abrir, cerrar, quieto), solo en el
dibujo por tarjeta gráfica y en las dos ramas. La búsqueda, el tecleo y el movimiento ya estaban optimizados de antes. Lo
nuevo es lo que se le manda a la tarjeta: al posar una consulta, pedir una canción o salir un aviso se sube solo el trozo
que cambió (la columna de resultados o el aviso) en vez de la imagen entera; al cerrar, la animación usa la textura que
ya tenía la imagen en vez de copiarla y volver a subirla; y al abrir se reutiliza la textura grande de la última ventana
en vez de crear otra. La imagen se sigue haciendo exactamente igual. Automático y sin interruptor. No se probó en una
tarjeta real ni en XP. [La guía](OPTIMIZACIONES_2026-09-24.md#el-buscador-más-ligero-para-la-tarjeta).

## El dibujo por tarjeta, más ligero — 24 de septiembre de 2026

Más imágenes por segundo sin cambiar lo que se ve, solo en el dibujo por tarjeta gráfica y en las dos ramas. El cuadro ya
no borra la pantalla (el cielo, opaco, la tapa entera); la vía láctea, la viñeta y la interfaz quieta se pintan solo por los
trozos que tienen algo, y lo que va con opacidad cero no se manda; y la aurora deja de repetir en cada cuadro las cuentas
de color que solo cambian con la canción. En la simulación (1024×768, navegando con música) cada cuadro pinta 6,89
pantallas en vez de 8,58 y el procesador tarda 0,39 ms en vez de 0,64 (HD) o 0,77 (Modest). Automático y sin interruptor.
No se midieron FPS en una tarjeta real ni se probó en XP. [La guía](OPTIMIZACIONES_2026-09-24.md).

## Pantalla viva, tanda 5: el estilo general — 24 de septiembre de 2026

Las dos últimas ideas elegidas, solo en el dibujo por tarjeta gráfica y en las dos ramas. Letra para leer de lejos: en
la tarjeta de lo que suena, abajo, el título crece, el artista baja casi un renglón y lo que sigue pasa a ese mismo
renglón, a la derecha, en un gris mucho más claro; la franja mide lo mismo y nada más se mueve. El color del género en todo:
la portada de lo que suena y la de dentro de un disco llevan detrás un resplandor del color del género que respira
despacio, y el pulso de la letra elegida toma el color de su género. Cada idea tiene su interruptor en Ctrl+F3 →
Pantalla → **La atracción y el estilo**, tarjeta **ESTILO GENERAL**. Sin tarjeta, o con las ideas apagadas, todo sale
como siempre. Con esta tanda están hechas las cinco. [La guía de la tanda](PANTALLA_VIVA_ESTILO_2026-09-24.md).

## Pantalla viva, tanda 4: el modo atracción — 24 de septiembre de 2026

Las dos ideas elegidas para cuando la rocola se queda sola, solo en el dibujo por tarjeta gráfica y en las dos ramas.
El escaparate: el cubo gigante se va a la izquierda y se mece, y a su lado sale el disco que enseña (su nombre en
grande, el artista, el género, cuántas veces se ha pedido y cuántas canciones trae), con un mensaje debajo que se turna:
cómo elegir, lo que da una moneda, lo más pedido y cómo buscar. Y cada 30 segundos, la lista de las cinco canciones más
pedidas esta noche (desde las 8 de la mañana), con sus portadas y barras que crecen; la cuenta se guarda en
`rocola_noche.txt`. Cada idea tiene su interruptor en Ctrl+F3 → Pantalla, en la hoja nueva **La atracción y el
estilo**. Sin tarjeta, o con las ideas apagadas, la atracción es la de siempre.
[La guía de la tanda](PANTALLA_VIVA_ATRACCION_2026-09-24.md).

## Pantalla viva, tanda 3: lo que suena y los créditos — 24 de septiembre de 2026

Las ideas elegidas para lo que suena, las monedas y los avisos, solo en el dibujo por tarjeta gráfica y en las dos
ramas. Cuando nadie toca la rocola, el panel de "A H O R A   S U E N A" lleva un vinilo que gira detrás de la portada,
el título en una sola línea (si no cabe, se desliza despacio), cuánto le falta a la canción y en cuánto suena cada una
de la cola. Al meter una moneda cae una moneda dorada que aterriza en el número de créditos, la cápsula da un golpe y
sube "+4 créditos". Si alguien pide algo sin créditos, en vez del letrero naranja sale una tarjeta amable que le dice
qué le falta y cuánto da una moneda; no pone nada en la cola sola. Y los avisos entran por la derecha con su color, su
icono y dos renglones: qué pasó y el detalle. Cada idea tiene su interruptor en Ctrl+F3 → Pantalla, en la hoja nueva
**Lo que suena y los créditos**. El dibujo por procesador no cambia.
[La guía de la tanda](PANTALLA_VIVA_SUENA_2026-09-24.md).

## La estrella ya no baila con la música — 24 de septiembre de 2026

La estrella que cruza el cielo daba brincos porque seguía la música: con cada golpe cambiaban de golpe su velocidad, su
giro, su balanceo y su tamaño. Ahora va siempre a su ritmo de crucero, el de una canción normal. Solo se calma al
navegar y se anima en reposo, despacio. En Ctrl+F3 → Fondo el interruptor se llama ahora **Estrella viajera con órbitas**,
y la corona, **Corona estelar**. Solo cambia el dibujo por tarjeta gráfica, en las dos ramas.
[Los detalles](ESTRELLA_3D_2026-09-23.md).

## Pantalla viva, tanda 2: dentro del disco — 24 de septiembre de 2026

Las ideas elegidas para el interior de un disco, solo en el dibujo por tarjeta gráfica y en las dos ramas. Si suena
una canción del disco abierto, un vinilo sale de detrás de la portada, gira a 33 ⅓ vueltas y lleva la portada en su
etiqueta. La lista se lee de lejos: unas nueve canciones por página, con la letra más grande. La que suena lleva un
ecualizador de cuatro barritas que se mueve con la música, y las pedidas dicen cuánto falta: "EN COLA 3 · en ~9 min".
Al pedir una canción, su boleto vuela desde la fila hasta "En cola" y el número sube cuando llega. Cada idea tiene su
interruptor en Ctrl+F3 → Pantalla, en la tarjeta **Dentro del disco**. El dibujo por procesador no cambia.
[La guía de la tanda](PANTALLA_VIVA_DISCO_2026-09-24.md).

## Pantalla viva, tanda 1 — 24 de septiembre de 2026

Las primeras ideas elegidas del lienzo de bocetos, solo en el dibujo por tarjeta gráfica y en las dos ramas.
La portada elegida crece, brilla con su propio color y dice cuántas canciones trae y cuánto dura, mientras las
demás se apagan un poco. Si el cursor se queda un segundo en un disco, sale una ficha con sus canciones más
pedidas y cuántas van en la cola. Al pasar de página, la vieja sale mientras entra la nueva, columna a columna, y
el cielo se corre un poco más despacio. La barra de letras lleva lupa y una rayita que dice cuánta música hay
detrás de cada letra. Cada idea tiene su interruptor en la pestaña nueva de Ctrl+F3, **Pantalla**. El dibujo por
procesador no cambia.
[La guía de la tanda](PANTALLA_VIVA_2026-09-24.md).

## Estrella reactiva — 23 de septiembre de 2026

Estrella de cinco puntas como el icono, ahora **sólida en 3D**: gira vueltas enteras
sobre su eje mientras recorre el fondo, con luz por facetas y un brillo que corre al
girar. Su estela, tipo Sandevistan, son copias congeladas con el giro de ese momento:
una por cada radio recorrido, aparecen sin saltos y se desvanecen en 1,2 s con los
colores de la canción. Con el compositor de Windows el panel dibuja un cuadro por
refresco de la pantalla y coloca la estrella para el refresco en que se verá: eso quita
los saltitos al moverse (sin compositor, como en XP, sigue como antes). Interruptor
nuevo en Ctrl+F3 → Fondo, "Estrella en 3D girando (GPU)". El flujo de portada/género
es más rápido en el espectro; el cielo conserva su degradado nocturno azul/negro. GPU
compartida por Modest y HD.
[La estrella 3D](ESTRELLA_3D_2026-09-23.md) · [Cielo y estela musical](CIELO_ESTRELLA_2026-09-23.md).

## Audio — 23 de septiembre de 2026

Rampas de volumen/excitador, transición de EQ, huellas estéreo sin cancelación y
protecciones del entrenamiento; además, sonoridad integrada BS.1770 de la canción
entera, limitador de pico real entre muestras, realce de agudos sin plegado (la
saturación trabaja a 2× con el camino seco alineado) y cambios en vivo del grave
sintético y del DSEE sin escalones ni saltos, y continuidad sin pausas entre
canciones del mismo formato (la siguiente entra por la misma salida, sin solaparse,
con cola y créditos como siempre) y una salida que entrega coma flotante en Vista
o posterior y, si falla el aparato, espera y retoma la misma canción sin gastar la
cola, compartido por Modest y HD (el sonido de los vídeos sigue siendo del
reproductor de Windows Media: la guía explica las opciones y espera una decisión). Solo en
HD, Apollo pasa a 44,1 kHz con SoXR cuando el FFmpeg lo trae (con alternativa
probada) y reaprovecha sin reprocesar lo que no cambia.
De noche, al bajar el volumen, devuelve los graves y agudos que el oído pierde
(ISO 226), y el grave por debajo de 120 Hz sale igual por todas las bocinas (una
grabación con un canal al revés conserva su grave). Las dos ramas.
Detalles, mediciones y tareas aún pendientes (escucha A/B, Windows XP real, CPU
antiguo) en [la guía de audio](MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md).

## Respuesta y transiciones GPU (10 de septiembre de 2026)

Tres interruptores independientes, activados por defecto, en Ctrl+F3 → Fondo:

- **Respuesta inmediata al toque:** una tecla o clic solicita actualizar la UI
  en el próximo cuadro, sin esperar su intervalo de trabajo pesado (hasta 250 ms).
  También inicia el regreso desde el estado ampliado sin esperar al temporizador.
  No ejecuta el render dentro del evento, ni retrasa o duplica acciones.
- **Transiciones de vista y selección:** fundido breve de la interfaz al cambiar
  entre muro e interior (140 ms) y del énfasis de resultados del buscador (120 ms).
  Los controles mantienen sus posiciones y siguen respondiendo durante el efecto.
  Se conservan las animaciones existentes de cubos, buscador, páginas y avisos.
- **Caché de portadas recientes:** hasta ocho texturas GPU y 8 MiB adicionales
  de texeles ARGB, incluido redondeo de dimensiones. Reutiliza las texturas al
  revisitar portadas, sin reducir resolución. Se libera al apagar la opción o la GPU.
  La contabilidad no representa todo el consumo del controlador y puede retener
  referencias a los bitmaps originales mientras estén en la caché.

Las mejoras no cambian el renderizador CPU, audio, orden de cola o contenido.
Desactivarlas recupera su ruta previa. No prometen eliminar el tiempo necesario
para leer una portada que todavía no esté cargada.

Verificación: pruebas/respuesta.cs comprueba las curvas a 24–240 Hz, prioridad
de entrada y la caché con HAL local (150 cambios, identidad de texturas,
propietario único, límite y liberación). pruebas/cristalcola.cs comprueba
32 combinaciones de las cinco opciones recientes y su guardado/restauración.
Son pruebas automatizadas; no una medición de latencia/FPS de la aplicación
completa con música ni una validación de Windows XP real.

## Fondo, cristal y cola ampliada

- La pestaña Fondo permanece accesible desde todas las páginas de Ctrl+F3.
- En Ctrl+F3 → Fondo, «Interior de discos con cristal (GPU)» añade esquinas
  redondeadas, un borde iluminado y reflejos a la lista transparente. Requiere
  «Lista de canciones transparente» en Imagen; apagado conserva el velo plano.
  Es un acabado de vidrio con textura reutilizada, no desenfoque del fondo.
- «Cola ampliada de 12 canciones (GPU)» muestra hasta doce próximas canciones
  en tres columnas y cuatro filas, numeradas de izquierda a derecha. Apagado
  recupera tres canciones. En espacios estrechos se omiten miniaturas y se
  abrevian textos; la cola real y su orden no se modifican.
- En el estado expandido, el cubo llega hasta la fila inferior; a su derecha
  quedan el tiempo y el progreso. El espacio del reloj se mide con su texto real
  al actualizar el panel, sin mediciones por cuadro. Se elimina la tarjeta oscura
  detrás del cubo, sin alterar la imagen de la carátula ni sus caras sombreadas.

Las dos opciones nuevas están activadas por defecto y se guardan al cerrar
los ajustes. El renderizador CPU permanece congelado.
Prueba: pruebas/cristalcola.cs (persistencia, geometría y transparencia).
Prueba adicional: pruebas/cubofila.cs, 45 combinaciones de ancho, altura y tiempo;
comprueba alineación, espacio del progreso y ausencia de relleno tras el cubo.

## Política de renderizado (8 de septiembre de 2026)

El renderizador CPU es obsoleto y queda congelado por decisión del usuario.
Las nuevas mejoras visuales y optimizaciones de renderizado se realizan solo en GPU,
con interruptores persistentes en Ctrl+F3. No se retira el código CPU existente.
Consultar [AGENTS.md](AGENTS.md) antes de modificar el proyecto.

La opción «Nebulosas azul / violeta (solo GPU)» da a las dos capas del fondo una
base azul y violeta, conservando un 35 % de sus colores musicales. No cambia los
colores extraídos de los discos ni los de la ventana de detalles. Al desactivarla
se recuperan los tintes originales; requiere nebulosas en nivel medio o alto.

### Acabado, transparencias y buscador (9 de septiembre de 2026)

Se añaden seis interruptores independientes, activados por defecto y guardados en
Ctrl+F3 → Imagen:

- Neon de progreso intenso: halo más amplio, tubo luminoso y núcleo más brillante.
  Requiere el neón; funciona también con el suavizado del progreso desactivado.
- Estado ampliado transparente: elimina el relleno de la franja expandida y conserva
  un velo tenue en las tarjetas para mantener legibles sus textos.
- Lista de canciones transparente: aplica un velo oscuro de aproximadamente 82 %
  de opacidad al muro interior del disco (18 % de transparencia), manteniendo
  títulos, selección, insignias y desplazamiento.
- Optimizar selección del buscador: reutiliza la capa principal y actualiza por
  separado la tarjeta seleccionada y la información de su vista previa.
- Nebulosas con detalle natural: textura de nubes con varias escalas de detalle,
  calculada al crear o regenerar la textura, no en cada cuadro.
- Aurora con bandas más definidas: picos locales menos mezclados y gradación de
  color por frecuencia. Es diferenciación visual del espectro, no separación real
  de instrumentos ni procesamiento del audio.

Desactivar cada opción recupera su comportamiento anterior. No se cambia el
renderizador CPU. Las nebulosas requieren que sus capas estén habilitadas y la
transparencia ampliada requiere el estado ampliado en reposo.
Ver [verificación y despliegue](../auditoria-rendimiento/ACABADO-GPU-20260909.md).

### Paquete visual GPU

Revisión de legibilidad del 9 de septiembre: el estado ampliado utiliza tipografía
Trebuchet MS más grande y en negrita, encabezados diferenciados y numeración de cola.
El indicador MEJORADA aparece en una cápsula y su halo acompaña la expansión;
conserva la condición real de mejora y el color propio de cada rama.
El tiempo queda a la izquierda de la barra de progreso, en una fila inferior
reservada. Son ajustes de los efectos existentes, con sus mismos interruptores
en Ctrl+F3 → Imagen; el renderizado CPU no cambia.

Actualización del 9 de septiembre:

- Con música y 15 segundos sin interacción, el muro queda al 2 % de opacidad.
- «Estado y cola ampliados en reposo (GPU)» hace crecer la franja hacia arriba,
  agranda la carátula y muestra las tres próximas canciones con título, artista,
  portada, número de turno y total en cola. Las ondas ascienden con la franja.
  Al interactuar vuelve suavemente al tamaño habitual. Requiere el modo de discos
  tenues en reposo; no se activa en ajustes, buscador, lista de canciones o vídeo.
- «Ocultar carátulas al pasar de página» sustituye el horneado a baja resolución:
  durante el deslizamiento se omiten carátulas y etiquetas. Cubos discretos del
  tono del fondo ocupan su lugar hasta que cada celda está lista. Al detenerse,
  la carga completa se reparte entre cuadros. Se conserva la clave hojeo_ligero.
- «Progreso ultrafluido (solo GPU)» separa la barra de la textura de estado y la
  dibuja por cuadro, con coordenadas fraccionarias y suavizado temporal del audio.
  El interruptor del neón continúa siendo independiente.

Los nuevos efectos no modifican el renderizador CPU. La valoración del rendimiento
y la percepción de las transiciones con música corresponde al usuario.

En Ctrl+F3 → Imagen, cada efecto tiene un interruptor independiente y persistente:

- Viñeta suave: oscurece gradualmente los bordes del fondo, sin oscurecer discos ni textos.
- Halo de selección: luz del acento del fondo detrás del disco seleccionado; se atenúa junto con el muro en reposo.
- Estrellas finas: reduce las puntas grandes y conserva algunas estrellas destacadas, sin cambiar el patrón al alternar.
- Ondas suaves: reduce altura e intensidad de la aurora, conservando su reacción musical.
- Nebulosas azul/violeta, barra de progreso neón y discos tenues en reposo con música.
- El hojeo ligero existente sigue disponible por separado.

Las opciones están activadas por defecto. Desplazar la página hacia abajo para
alcanzar todos los controles. Apagarlas recupera el aspecto anterior de cada efecto.
La viñeta reutiliza una textura GPU de 128 × 128; el halo reutiliza la textura de
brillo existente. No se añadieron temporizadores. El rendimiento real será evaluado
por el usuario; las comprobaciones automatizadas no equivalen a una medición con música.

Rocola digital para Windows (WinForms + NAudio): pantalla completa, manejo por
botonera/teclado físico (sin depender de un teclado de PC), monedero con créditos,
búsqueda con teclado en pantalla, sección de videos (Windows Media Player), portadas
en cubos 3D, fondo estrellado por género, estadísticas ("Lo más sonado" + TOP por
artista) y modo atracción.

**Versión 2.5.** Esta rama es la **edición HD** (`rocola-hd`), para PC potente.

## Ramas

- **Clásica (XP)** — la rocola física original y rama de referencia: .NET Framework
  **3.5 sin SP1**, Windows XP 32-bit, CPU modesto. Los cambios comunes se mantienen en ambas
  versiones; probar en CLR 2 de un Windows moderno no sustituye la validación en XP real.
- **`rocola-hd`** — *esta rama*, para PC **potente** (Windows 7+/10, 4+ núcleos, 4+ GB):
  retargeteada a **.NET Framework 4.8**. Mismo programa y **misma lógica de audio** que la
  clásica, más el **NIVEL HD** que la máquina activa sola al arrancar (`ProbeAudioTier`).

## Qué hace (v2.5)

**Operación (local):**
- Se maneja con la botonera/teclado físico configurable; nada de "lenguaje de teclado de PC".
- Monedero con créditos y cola de canciones pagadas.
- Buscador con teclado en pantalla; sección de videos; portadas en cubos 3D girando.
- La lista de cada disco muestra la **duración** de cada canción. Va en su **propia pasada rápida**:
  consulta cabeceras cuando el formato lo permite; otros archivos requieren un recorrido mayor.
  El tiempo depende de los archivos y del disco, sin un factor de aceleración universal.
  Se guarda en `rocola_duracion.txt` y sobrevive a los reinicios.
- **Animaciones** (aceleradas en el perfil **GPU**, con trabajo de preparación en CPU;
  el coste y la fluidez dependen del equipo; `animacion=0` las apaga):
  - **La portada viaja entre cubos.** La rocola ya dibuja todo como cubos girando (la casilla de la
    cuadrícula, el cubo grande del disco, el cubo gigante de la atracción, el del buscador). La
    portada **sale calcada sobre el cubo de origen** —misma casilla y mismo giro, no se nota el
    relevo—, vuela girando un cuarto de vuelta, se ladea y se endereza, lleva un **destello** detrás,
    y **aterriza calcada sobre el cubo de destino**, donde se funde con él. Cuatro recorridos, el
    mismo código: cuadrícula→disco, disco→cuadrícula, cuadrícula→atracción y atracción→cuadrícula
    (~0.26 s). Desde el buscador sale del cubo grande del resultado elegido.
  - **Buscador**: se abre y se cierra **como una ventana de macOS** — entra al 86 % de su tamaño y un
    poco más abajo, se pasa un ~4 % de su tamaño final y **rebota** hasta asentarse (rebota algo más
    a lo ancho que a lo alto, así que se estira un instante: eso es lo que le da vida). Al cerrarse
    toma **impulso** —crece un pelín— y luego se encoge, se hunde y se va acelerando (~0.21 s).

  Todas son solo un dibujo **encima**: el cambio de pantalla sigue siendo instantáneo, y si algo las
  interrumpe (modo atracción, un video, otra tecla) simplemente dejan de dibujarse — nunca queda nada
  a medias. El avance lo calcula el propio render (60 fps), no el temporizador de la interfaz, así que
  se ven fluidas.
- **Aurora del horizonte** (`espectro=` en config, 0 la apaga): el visualizador de espectro, hecho como
  parte del **cielo** y no como un aparato encima. Un resplandor suave en la franja baja cuya silueta
  la dibujan **8 bandas** medidas en vivo, con el color del género. Va **detrás** de la cuadrícula y
  se atenúa junto con el fondo, así que nunca le pelea a las portadas ni al texto; se apaga sola
  cuando no hay música. Las bandas viajan por la **misma cola retrasada** que el pulso, así que la
  aurora se mueve encima del sonido que se oye y no un segundo antes.
- "Lo más sonado" y TOP por artista; modo atracción cuando nadie la usa.
- **Registro de errores de kiosco**: intenta anotar las excepciones en `rocola_error.txt`.
  El manejador de UI permite continuar en algunos casos; un error fatal de otro hilo puede
  terminar el proceso. No hay garantía de recuperación ni reinicio automático del programa.

**Sonido (idéntico en las dos ramas):**
- **Nivelado perceptual por etapas** (tipo LUFS / BS.1770, con *K-weighting*): busca emparejar
  canciones; sube las bajitas y **no** baja las fuertes en esta etapa. Seis etapas según lo bajita
  que venga la canción (**ULTRA BAJA / BAJA / MEDIA-BAJA / MEDIA / ALTA / MUY ALTA**), cada una
  con su propio tope de subida (hasta **+20 dB** en las ultra bajas, apenas +4 en las muy altas),
  interpolado entre etapas para que no haya saltos.
- **Pre-análisis de la biblioteca**: en segundo plano (prioridad mínima, solo cuando la rocola
  está en reposo) se mide el volumen, la **duración** y la **huella tonal del EQ automático** de
  cada canción legible. Si su análisis ya está disponible, llega ajustada desde la primera
  reproducción; de lo contrario se mide al sonar para aplicar lo aprendido después.
- **Subir TODO el volumen**: dB extra generales; el **limitador con pre-escucha** baja los picos
  suave y ANTES de que lleguen. No garantiza ausencia de distorsión en el archivo o las bocinas.
- **EQ automático**: corrige el tono de cada canción hacia el promedio de la biblioteca.
- **Bajo psicoacústico** (`bajo=` en config, perilla en Ctrl+F3): las bocinas de un local no bajan de
  ~80 Hz, así que el grave profundo **no sale** por ellas. Subirlo con EQ no sirve — solo gasta
  amplificador y hace toser al cono. En vez de eso se generan los **armónicos** de esa nota grave (el
  doble y el triple de su frecuencia), que la bocina sí reproduce, y **el oído reconstruye solo la
  nota que falta** (efecto de la *fundamental ausente*). El grave se toma de la mezcla mono y sale
  centrado. Verificado: genera el 2º y 3er armónico, **no toca** la nota original ni la voz ni los
  agudos en las señales de prueba; en 0 % el audio sale idéntico bit a bit.
  El coste de CPU y el resultado audible dependen del equipo y del material.
- **Grave en mono** (`grave_mono=` en config, el corte en Hz; 0 lo quita): por debajo de 120 Hz el mismo grave sale
  por los dos canales, así que con bocinas repartidas nadie se queda sin bajo por estar junto a la "equivocada". Lo
  mono no cambia de nivel; el grave sale unos 4 ms tarde, como en el cruce de una bocina de dos vías. Una grabación
  con un canal al revés no pierde su grave: se detecta y se devuelve en fase. Ver 1.24 de la guía de audio.
- **Modo noche que no apaga el sonido** (`noche_compensa=` en config, en %; 0 lo quita): mientras la noche baja el
  volumen, devuelve los graves y agudos que el oído pierde al bajar (curvas de igual sonoridad ISO 226): con los 4 dB
  de fábrica, +1,6 dB en 50 Hz y +0,7 en 12,5 kHz. De día no hace nada. Ver 1.23 de la guía de audio.
- **DSEE** (realce/reconstrucción de agudos): recrea el "aire" que el MP3 recorta. Copia por
  bloques con la fase seguida entre ventanas (como el SBR de AAC): un tono recreado sale limpio, al
  nivel de la curva de diseño y con el corte quieto (ver 1.21 de `MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md`).
  Insignia **MEJORADA** (dorada, que brilla) cuando actúa el reconstructor o, en HD,
  se reproduce una versión procesada por Apollo. Indica procesamiento, no una garantía
  de mejor calidad subjetiva ni recuperación exacta de información perdida.
- **Des-recorte (declipper)**: repara los picos aplastados "de fábrica" de archivos mal
  masterizados (reconstruye el pico redondo perdido; `declip=` en config).
- **Fundido** suave de salida al terminar cada canción **y cada video** (`fundido=` en config, 0-3 s).
- **La interfaz sigue por GPU con video sonando** (`video_gpu=` en config, encendido por defecto).
  Antes, un video mandaba **toda** la pantalla a render por CPU (fondo estrellado incluido) mientras
  WMP decodificaba el video — de ahí el rendimiento raro con videos, y por eso las animaciones (que
  van por GPU) nunca podían verse con un video sonando: solo la del buscador, el único modo que ya
  ignoraba el video. El cuadro del video va **encima** del panel y Windows recorta uno donde está el
  otro. Con video visible, el panel baja solo a ~30 cuadros para dejarle la máquina al video. Si en
  algún equipo se viera mal, `video_gpu=0` vuelve al comportamiento anterior. El video a **pantalla
  completa** sí sigue apagando el panel.
- **Bitácora de diagnóstico** (`diagnostico=1` en config): apunta con hora los eventos clave (inicio
  y fin de video, animaciones que arrancan o el motivo por el que no, cambios de modo de render) en
  `rocola_diagnostico.txt` junto al exe. Para cazar problemas del fierro real sin adivinar; apagada
  no cuesta nada.
- **Volumen de los videos**: el video lo reproduce Windows Media Player, cuyo sonido **no pasa**
  por la cadena de audio. Una perilla en Ctrl+F3 (`video_vol=` en config) empareja su volumen con
  el de las canciones, y además sigue a "Subir TODO el volumen". En esta rama, el pre-análisis
  **también mide los videos**, así que se nivelan solos al mismo objetivo que las canciones.
- **Dither TPDF** al bajar a 16 bits (en la salida WaveOut de respaldo).
- **Respaldo automático** de lo aprendido (volúmenes, EQ, estadísticas, config)
  a otra carpeta/disco; conserva las 3 instantáneas completas más recientes, incluso del mismo día.
  Cada archivo se copia y verifica antes de publicar la carpeta. Si falla, conserva los
  anteriores y registra el error. No es una copia de la música ni una instantánea atómica
  de todas las tablas a la vez.

### La IA aprende del material apto de la biblioteca (sin mover la música)

Para enseñarle a la red hacen falta pares *"esto es lo que queda / estos eran los agudos reales"*.
En un MP3 dañado los agudos reales **ya no existen**, así que solo sirven de maestras las canciones
que **aún los conservan** (320 kbps, FLAC, WAV).

Mientras el **pre-análisis** recorre la biblioteca en segundo plano, a esas canciones completas les
corta el agudo **a propósito** (a 11, 13, 15, 16.5 y 18 kHz) y guarda el par en
`rocola_ia_datos.bin`. Usa las **mismas funciones** que la reproducción, así que el material de
entrenamiento coincide **exactamente** con lo que la red verá en vivo (verificado: diferencia 0.0000).

Flujo completo:

1. La rocola junta el material sola. El avance se ve en **Ctrl+F3** ("Material para la IA: N ejemplos").
2. Copias `rocola_ia_datos.bin` a una **memoria USB** con espacio suficiente (hasta 24 GiB por defecto).
3. En una PC potente: `python3 ia/entrenar_ia.py --datos rocola_ia_datos.bin --capas 28,96,96,12`
4. Regresas el `rocola_ia.bin` nuevo junto al `Rocola.exe`.

Se apaga con `ia_datos=0` en `rocola_config.txt`.

### Entrenamiento a fondo (mantenimiento)

Botón **"ENTRENAMIENTO A FONDO"** en Ctrl+F3. Mientras está encendido, la rocola **se dedica por
completo** a dejar todo calibrado:

- Repasa **toda** la biblioteca (no solo lo que falta) y analiza cada canción **completa**, no por
  muestras — la medida más exacta posible de volumen, EQ y material para la IA.
- **Se puede parar y seguir.** Apunta en `rocola_afondo.txt` cada canción (y cada video) que ya
  repasó, así que al detenerlo y volverlo a encender —o si apagas la rocola, o se va la luz—
  **continúa donde iba**, no vuelve a empezar por la primera. La barra también sigue desde donde
  estaba. Cuando ya no queda nada, el panel lo dice: *"la biblioteca ya está repasada entera"*.
  Lo que agregues después entra solo en la cuenta.
- **No espera** a que la rocola esté en reposo ni descansa entre archivos, y el hilo sube a
  prioridad normal.
- Una **barra de avance** y el texto muestran cuántas canciones lleva y cuántos ejemplos de IA.
- Sigue corriendo aunque cierres el panel; se detiene con el **mismo botón**.

**Los dos modos se acumulan con límites.** La cosecha pasiva tiene un presupuesto de 192 cuadros
por pasada y hasta ocho pasadas por canción. A fondo permite hasta 1.536 cuadros repartidos
por el tema (ocho veces el presupuesto de una pasada pasiva, no tres). El número aprovechado
depende de la duración, los cortes y los filtros de calidad. `rocola_ia_hechas.txt` registra
el avance para no repetir indefinidamente la aportación de una canción.

El archivo de material tiene un **tope de 24 GiB** (`ia_datos_mb=` en config; 0 lo quita). Con eso
cabe una biblioteca de ~20 000 canciones aptas cosechadas a fondo, y entra holgado en una memoria de
64 GB. Ojo: la memoria tiene que estar en **exFAT o NTFS** — en FAT32 ningún archivo puede pasar de
4 GB, y ahí habría que bajar el tope. En Ctrl+F3 se ve el tamaño actual y si llegó al tope.

**La RAM depende del tamaño de la muestra**: el entrenador mapea el archivo desde disco, pero
materializa una muestra en float64 y copias de reordenación, además de buffers de entrenamiento.
La muestra se reparte por el archivo para no tomar solo las primeras canciones. Se ajusta con
`--max-ejemplos N` (por defecto 4 millones; `0` = todos y puede requerir mucha RAM).

El modo a fondo **no** se guarda en la configuración a propósito: si se va la luz, la rocola vuelve
sola a su modo normal y nunca se queda a todo gas en horario de negocio.

## Nivel HD (solo esta rama)

- **Decodificación en coma flotante**: los MP3 los descomprime el decodificador de MP3 de Windows en coma flotante
  (como MFT) cuando queda comprobado contra el de siempre, una vez por sesión; AAC, M4A y WMA, Media Foundation en
  coma flotante. Lo que el MP3 trae por encima del máximo digital ya no se corta antes de bajar el volumen. Si algo
  no cuadra, el de siempre, bit a bit como antes. Sin probar todavía en Windows: ver 1.22 de
  `MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md` y la línea «MP3 en coma flotante» de `rocola_error.txt`.
- **Salida WASAPI exclusiva** (`salida=auto|exclusivo|wasapi|waveout` en `rocola_config.txt`;
  `SalidaExclusiva.cs`): la rocola toma el aparato para ella sola y la música no pasa por el
  mezclador de Windows. Formato exacto que el aparato admite a su propia frecuencia (24 bits,
  32, coma flotante; 16 con dither solo si no hay otro), volumen de Windows respetado (en el
  aparato, o aplicado por la rocola si el aparato no tiene volumen propio) y caída automática a
  **WASAPI compartido** (muestras *float* de punta a punta) si otro programa usa el aparato,
  Windows no lo permite o falla al arrancar. `salida=wasapi` vuelve al compartido de antes.
  Si falla la construcción, inicialización o arranque síncrono del compartido, reabre la pista
  una sola vez por **WaveOut**. Los vídeos suenan como siempre: la música suelta el aparato
  antes de cada vídeo. Detalles y límites en `MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md` (1.19).
- **Reconstructor espectral fino**: el DSEE espectral usa **STFT de 4096** (bins más finos =
  detección del corte y costura más limpias) en vez de 2048.
- **IA de envolvente incluida** (`rocola_ia.bin`): un MLP predice la envolvente de agudos para
  el reconstructor espectral. Se reentrena con tu música (`--musica`; ver `ia/LEEME.md`)
  y tiene pruebas de paridad Python↔C#. Este archivo **no es Apollo**.
- **DSEE ULTRA — Apollo por demanda**: motor externo Python, con selector de GPU en Ctrl+F3.
  Requiere instalar sus dependencias, código y checkpoint por separado (`ia/upscale`).
  Si la versión procesada aún no está lista, suena el original con el respaldo DSEE disponible.
- **Render por GPU (Direct3D 9)**: estrellas, cubos 3D y buscador acelerados; con caída a GDI si no hay GPU.

## Luna con relieve, halo y polvo orbital (15-09-2026)

- Nuevo diseno exclusivamente GPU: superficie procedural con mares oscuros, crateres con bordes y sombra lateral; el relieve es aparente, no una simulacion astronomica. La cara nocturna conserva una iluminacion tenue y el exterior del disco es transparente.
- Halo del color del ambiente que respira con los graves y tres trazas pequenas de polvo orbital, dibujadas por delante/detras del disco. La superficie no rebota ni se deforma. El interruptor existente de luna musical controla el movimiento y la respuesta al audio.
- Al atenuarse el fondo durante la navegacion, la luna gana luminosidad y hasta un 22% de diametro siguiendo la misma transicion. Sigue detras de los controles. No depende de que el muro de discos este en reposo.
- Cuatro interruptores persistentes en Ctrl+F3 → Fondo: diseno con crateres, halo, polvo y realce al atenuar. Los tres ultimos requieren el nuevo diseno; al desactivarlo vuelve la luna anterior. El interruptor general de luna sigue ocultando el conjunto.
- Textura fija de 256 × 256 (256 KiB sin contar recursos del controlador), reutilizada entre cuadros y resoluciones. Sin modelos de IA, descargas, shaders nuevos ni reconstruccion por latido. CPU congelado; se conservan .NET 3.5 en Modest y .NET 4.8 en HD.

Validacion: siete baterias, incluida superficie determinista/transparencia/luz lateral, 16 configuraciones lunares, realce, reutilizacion y liberacion de textura en Direct3D 9. No equivale a medir FPS con musica real ni a probar Windows XP.

## Reposo integrado y cola compacta (15-09-2026)

- Desde 1500 px de ancho, las tarjetas de la cola ampliada tienen un máximo de 520 × 79 px y se agrupan arriba, con la carátula y las dos líneas de texto alineadas. Una o dos canciones ya no generan paneles enormes. Se conserva el reparto de las resoluciones bajas y la capacidad de 12 canciones.
- «Controles tenues en reposo (GPU)» atenúa al 2% la barra de letras, el botón Buscar, las instrucciones, el encabezado, sus halos y el contador de FPS cuando se expande el estado. Recuperan su opacidad con la animación de regreso al interactuar. La canción actual, su cola y los avisos permanecen legibles; las vistas activas del buscador y del disco no se atenúan.
- «Ondas unidas al estado (GPU)» hace que el núcleo del horizonte y el borde de la barra compartan posición durante toda la expansión y contracción. Corrige la separación anterior sin reconstruir texturas por cuadro.

Los dos interruptores están en Ctrl+F3 → Fondo, activados por defecto. Se guardan como `controles_reposo` y `ondas_unidas`; apagarlos recupera la visibilidad o separación anterior. Cambios exclusivos GPU; CPU congelado.

`pruebas/validar-capas.ps1` ejecuta seis baterías: incluye 297 disposiciones de cola, 126 posiciones del horizonte verificadas contra su malla, curvas de atenuación a 24–240 Hz y comprobaciones con Direct3D 9. No son mediciones de FPS con la biblioteca real ni pruebas en XP.

## Vidrio, cola compacta y cambios de color (14-09-2026)

Cambios compartidos con Modest/HD, exclusivamente en el renderizador GPU:

- La cola ampliada reparte hasta 12 canciones en una cuadrícula según el ancho, el alto y la cantidad real. No se ocultan las miniaturas en pantallas pequeñas; el título y artista conservan elipsis cuando falta ancho. Con «Cola ampliada de 12 canciones» apagado se conserva la distribución de tres filas.
- «Vidrio por capas y color vivo (GPU)» en Ctrl+F3 → Fondo separa el vidrio, la página y la fila seleccionada. El vidrio sigue el acento musical mediante color de vértice, sin reconstruir el bitmap por cada tono intermedio. Se conserva el hojeo y el aspecto de textos/marcas; desactivarlo recupera la composición anterior. Requiere las opciones de cristal y lista transparente.
- «Cache de transiciones de color (GPU)» hornea el buscador con el color de destino una vez por cambio. La GPU mezcla los extremos durante 420 ms; tocar un control o cambiar el contenido cancela la mezcla para no mantener textos anteriores. La retención adicional está limitada a 64 MiB y se libera al terminar, cerrar o desactivar. Si no cabe, se aplica el destino directamente.
- «Luna musical con orbitas (GPU)» añade desplazamiento, halo y tres estelas orbitales moduladas por nivel, medios y agudos suavizados. Sube con el estado ampliado para no esconderse detrás. Reutiliza la textura lunar y el atlas de halos; no genera bitmaps por cuadro. Requiere la luna habilitada en Detalles. Apagarlo recupera la luna anterior.

Claves persistentes nuevas: `glass_capas`, `colores_cache`, `luna_activa` (1 activado / 0 desactivado).

Validación reproducible en Windows: `powershell -File pruebas/validar-capas.ps1`.
Compila con advertencias como errores y ejecuta pruebas de píxeles, disposición, configuración y recursos D3D9. Los tiempos de preparación son microbenchmarks, **no FPS del programa con música**. Modest sigue apuntando a .NET 3.5/XP, pero no se ha probado en Windows XP. El renderizador CPU permanece congelado; compilarlo solo comprueba que no se rompió el proyecto.

## Colores de fondo y portadas

En Detalles, los tres botones asignan al tono del disco un peso de **20%, 50% y 100%**
en el acento; el resto corresponde al género. Con género, una nebulosa conserva ese tono.
BTS/Bangtan con género reconocido conserva su morado fijo. **Sin género reconocido**,
cualquier peso de portada mayor que cero usa solo el tono disponible del disco, también
en «Mitad y mitad» y para BTS. Apagar el efecto o poner peso cero desactiva esa sustitución.

El género se busca en canción, disco, artista y nombre/carpeta; no se limita a ID3.
El extractor ignora píxeles transparentes y representa el tono dominante/promedio y picos,
manteniendo brillo, viveza, transiciones y degradados del diseño, no el RGB literal.
Sin portada cromática disponible se usa el fondo de reserva.

## Compilar

Proyecto de **.NET Framework 4.8** (Visual Studio). Abre `Rocola.slnx` y compila en Release.
El resultado (`Rocola.exe` + `NAudio.dll`) se copia junto a la carpeta `musica`, como siempre.

- Código: `Rocola/Form1.cs` (monolítico) + `Form1.Designer.cs` + `Program.cs` + `D3D9Star.cs`.
- Dependencia: **NAudio 1.9.0**: la DLL viene **incluida** en `packages/` y el proyecto la
  referencia directo — compila **sin NuGet y sin internet** (ya no hay `packages.config`).
- Herramientas de IA (opcionales, Python): `ia/` — entrenamiento (`entrenar_ia.py`,
  `entrenar_con_mi_musica.bat`), motor en tiempo real (`ia/tiempo_real/`) y upscale por lotes (`ia/upscale/`).

> `bin/`, `obj/`, `.vs/` y el resto del paquete NuGet no se versionan (ver `.gitignore`);
> solo se incluye la DLL de NAudio que el `HintPath` necesita.

## Optimizaciones aplicadas (sin cambiar aspecto ni comportamiento)

1. **Estabilidad — `PlayTrack`**: si varias canciones seguidas no abren (archivos
   movidos/corruptos), antes se apilaba `PlayTrack → PlayNext → PlayTrack…` (posible
   *StackOverflow*). Ahora el `PlayNext` del `catch` va por `BeginInvoke`, que desenreda la pila.
2. **Búsqueda — `Score` / `RecomputeResults`**: `" " + q` y `q.Split(' ')` se calculaban
   dentro de `Score` (corre por **cada** entrada del índice en **cada** tecla). Ahora se calculan
   una sola vez por pulsación (puntaje idéntico), evitando decenas de miles de asignaciones.
3. **Render — `Cube.Draw`**: los tres arreglos de la proyección 3D (`ex`, `depth`, `order`)
   se reutilizan vía `[ThreadStatic]` en vez de asignarse en cada llamada.
4. **Escaneo del disco en UNA pasada** (`ScanFolders`): antes se caminaba el árbol dos
   veces (audio y video) y además se pedía la lista de cada carpeta otra vez para buscar
   la portada — **tres lecturas por carpeta**. Ahora se camina una sola vez y de esa misma
   lista salen las tres cosas. Verificado contra el método anterior: mismas canciones en el
   mismo orden, mismos videos y **la misma portada elegida** en cada carpeta.
