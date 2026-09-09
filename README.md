# Rocola

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
- **DSEE** (realce/reconstrucción de agudos): recrea el "aire" que el MP3 recorta.
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

- **Salida WASAPI flotante** (`salida=auto|wasapi|waveout` en `rocola_config.txt`): muestras
  *float* de punta a punta hasta el motor de audio moderno, sin bajar a 16 bits ni pasar por
  el mezclador clásico; la latencia real depende del controlador (el pulso visual se re-alinea).
  Si falla la construcción, inicialización o arranque síncrono de WASAPI, reabre la pista
  una sola vez por **WaveOut**. Los fallos asíncronos durante reproducción siguen el manejo
  de error de pista; no se promete un cambio transparente de dispositivo a mitad de canción.
- **Reconstructor espectral fino**: el DSEE espectral usa **STFT de 4096** (bins más finos =
  detección del corte y costura más limpias) en vez de 2048.
- **IA de envolvente incluida** (`rocola_ia.bin`): un MLP predice la envolvente de agudos para
  el reconstructor espectral. Se reentrena con tu música (`--musica`; ver `ia/LEEME.md`)
  y tiene pruebas de paridad Python↔C#. Este archivo **no es Apollo**.
- **DSEE ULTRA — Apollo por demanda**: motor externo Python, con selector de GPU en Ctrl+F3.
  Requiere instalar sus dependencias, código y checkpoint por separado (`ia/upscale`).
  Si la versión procesada aún no está lista, suena el original con el respaldo DSEE disponible.
- **Render por GPU (Direct3D 9)**: estrellas, cubos 3D y buscador acelerados; con caída a GDI si no hay GPU.

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
