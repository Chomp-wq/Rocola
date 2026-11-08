# Plan: traducir todo el programa de XP a Linux (7 de noviembre de 2026)

Decisión del dueño: «la única forma de que el programa de rocola quede bien en Linux [...] es que revises el código del programa en XP,
y que absolutamente todo el código lo traduzcas, no que solo por ver capturas adivines cómo está hecho, traducir el código para que
funcione nativamente en Linux, pero que quede idéntico, y después de que quede idéntico, implementar con cuidado y de manera coherente
todo lo demás que hemos hecho».

## Dónde estamos (inventario del 7-11-2026)

El programa de XP: 109 archivos, 69 000 líneas (47 400 dentro de funciones). Comparado función por función con Rocola OS:

| Parte | Traducida en Rocola OS |
|---|---|
| El sonido: las etapas de la cadena (GraveMono, Realce, PicoReal, SinPausa, Siseo, Zumbido, PreEco, el billar...), la sonoridad, el nivelado, el buscador de XP, las tablas | Sí, casi línea por línea (80-100 %; la cadena se comprobó bit a bit) |
| Form1.Sonido (70 %), Form1.Datos (64 %), Form1.Ia (73 %), AudioControles (69 %), Form1.Biblioteca (23 %) | A medias |
| **Todo lo que se ve**: la pared, las letras, el disco, el buscador, lo que suena, el cielo, la estrella y la luna (Direct3D 9), la atracción, la ayuda, el asistente, Ctrl+F3, el video, el diagnóstico | **No (0-10 %)**: se rehizo desde cero imitando el aspecto |

En total, el 25 % del código de XP está traducido; lo que falta es casi todo lo visual. Esa es la causa de que se vea «recortada».

## Cómo se traduce (nativo de Linux, sin imitar Windows)

El programa de XP pinta sus controles con GDI+ (la librería de dibujo de Windows) y compone el fondo con Direct3D 9. La traducción
reescribe cada llamada con su equivalente nativo de Linux, sin una capa que finja ser Windows (la regla del 2-11-2026 sigue: nada de
un Direct3D o un WinForms de mentira):

| En XP | En Linux |
|---|---|
| GDI+ (Graphics, Brush, Pen, GraphicsPath, Font, DrawString, LinearGradientBrush...) | **SkiaSharp** (la librería de dibujo de Chrome y Android; mismas operaciones: trazos, degradados, texto, imágenes). Probado el 7-11-2026 con el compilado NativeAOT. |
| Direct3D 9 (D3D9Star.cs: el cielo, las estrellas, los cubos, la composición de las piezas) | **OpenGL ES 2** (el que ya usa Rocola OS por KMS), con la misma geometría y las mismas cuentas |
| Las letras de Windows | **Las mismas letras de XP**, leídas del disco de Windows del A4 (C:\WINDOWS\Fonts); en la memoria de prueba, las libres más parecidas |
| WinForms (Form, Control, sus medidas y sus eventos) | Los mismos controles de la rocola (AlbumView, LetterBar, SongList, HelpBar, SearchOverlay...) como clases propias, con sus mismas medidas, su mismo pintado y sus mismos eventos (teclas y ratón por evdev) |
| Windows Media Player (videos) | mpv (como ya hace Rocola OS), con la lógica de Form1.Video traducida alrededor |
| winmm / WaveOut, el volumen de Windows | ALSA (ya hecho) |
| El renderizado por CPU de XP (obsoleto y congelado desde el 8-9-2026) | Propuesta: no se traduce; el A4 usa el de tarjeta gráfica, y ese es el que se traduce |

## El port nativo (8-11-2026): cómo se traduce de verdad

Aclaración del dueño del 8-11-2026: lo hecho hasta ese día dejó el código de XP llamando a WinForms, GDI+, Direct3D 9 y el WMP, con
una capa debajo que los imita. Eso no es lo pedido: el código de cada archivo de XP se traduce para que llame directo a lo nativo de
Linux, sin capas («ningún layer de traducción, ya que eso frenaría el procesador»). El diseño sale de un inventario hecho con el
compilador (15 646 referencias: WinForms 2 469 líneas en 57 archivos, GDI+ 2 903 en 45, Direct3D 9 518 en 22, WMP 41 en 3, otras de
Windows 183 en 26; 40 archivos no usan nada de Windows) y de tres estudios de estructura con prueba de concepto.

| En XP | En el port nativo |
|---|---|
| Direct3D 9 (D3D9Device: 107 líneas con llamadas) | El dispositivo llama a **OpenGL ES 2** directo: 3 programas fijos (las 3 combinaciones de etapas que usa XP), texturas con su envoltura y su filtro, el búfer de fondo como textura con framebuffer. La lógica del dispositivo (cachés de estado, juntar dibujos, potencias de 2, imagen intermedia, teselas) no cambia. Prueba de concepto: 5 de 5 cuadros iguales byte a byte a la referencia. |
| GDI+ y el TextRenderer de GDI | Cada archivo dibuja con **SkiaSharp** directo (SKCanvas, SKPaint, SKPath, SKBitmap) y llama a funciones propias (carpeta `Nativo/`) solo donde GDI+ hace las cuentas distinto que Skia: el centro de los píxeles, el acomodo del texto, la letra de GDI, el muestreo de las imágenes, los degradados, el premultiplicado. Sin clases con la forma de Graphics, Font, Brush, Pen o Bitmap. Prueba de concepto: 1 800 comparaciones, 0 bytes distintos. |
| WinForms | Ninguna clase de XP hereda de nada de Windows: Form1 es la rocola y su ventana es la pantalla. Un bucle propio (`Pantalla`: encargos de otros hilos, teclas por evdev, relojes con la regla de WM_TIMER, el reposo) y, para lo que XP abre como ventana (Ctrl+F3, Ctrl+F2, avisos, F12), una hoja propia con el foco, el tabulador y el ratón que XP espera, pintada con SkiaSharp. Solo lo que XP usa, con nombres propios. |
| Reproductor de Windows Media | La lógica de Form1.Video llama directo a un reproductor propio sobre **libmpv**, sin imitar el modelo de objetos del WMP. |
| Otras de Windows (memoria, registro, foco, archivos, prioridades, winmm, ACM) | Lo de Linux directo (/proc, fsync, readdir, nice, ALSA, NLayer). |
| El dibujo por procesador (sin tarjeta) | Se traduce también (a SkiaSharp, con su mismo aspecto): en Rocola OS es lo que se ve si falla la tarjeta. |

Decisiones tomadas para no detener la traducción (el dueño puede cambiarlas):
- «Agregar carpeta...» de Ctrl+F2: un selector de carpetas nativo, en lugar del FolderBrowserDialog de Windows.
- Lo que no está en el código sino en el Windows XP del A4 se toma como lo supone la referencia: el tema Luna, la repetición de teclas
  (500 ms y unas 30 por segundo) y el doble clic (500 ms y 4 puntos). Leerlo del registro del disco de XP queda como mejora.
- Una excepción en el reposo (Application.Idle) sale del programa, como en WinForms; lo demás se apunta y la rocola sigue
  (CatchException), como en XP.

Cómo se asegura que es idéntico (sin capturas del A4): una copia congelada de la versión con capas (`/root/rocola-trabajo/referencia-xp`,
commit aef2211, fuera del repositorio) sirve de referencia. La tarjeta se comprueba con cintas (cada llamada grabada se reproduce en las
dos y el fondo se compara byte a byte); el dibujo, con un banco por función, la traza por línea de XP y un banco de vistas; las ventanas,
con las 41 pruebas de WinForms, la traza de lo que llega al código de XP y su estado. Todo en JIT y en NativeAOT. Mientras dura, la
versión a medio traducir no entra en Rocola OS. Al final se borran las capas (Ventanas.*.cs, Dibujo2D.cs, Direct3DaGL.cs,
ReproductorWmp).

## La referencia

**Decisión del dueño del 8-11-2026: no hacen falta capturas.** «Idéntico» se asegura por el código:
- `Xp/Codigo` es `Rocola/` con solo las diferencias de traducción, revisadas con un diff que quita lo mecánico;
- la capa de `Plataforma` hace lo que GDI+, WinForms, Direct3D 9 y el WMP hacen en Windows XP.

Lo de abajo es lo que se pensaba antes y queda como historia:

«Idéntico» se comprueba contra la rocola de XP de verdad, no contra fotos sueltas:

- **Capturas de referencia del A4**: un modo de captura en el programa de XP (Ctrl+F12, por ejemplo) que recorre las pantallas (la pared,
  cada letra, un disco, lo que suena, el buscador, la atracción, Ctrl+F3 hoja por hoja) y guarda cada una en PNG. El dueño lo corre una
  vez en el bar y trae la carpeta. Rocola OS saca las mismas pantallas y se comparan lado a lado y punto a punto.
- **El programa de XP abre en Linux con Mono** (probado el 7-11-2026, con una librería que contesta a las llamadas de Windows): sirve para
  comprobar la lógica y las medidas, no el aspecto final (Mono pinta distinto lo transparente y no tiene Direct3D).
- Lo que no puede salir idéntico punto a punto: el suavizado de las letras (GDI de XP contra FreeType) difiere en el borde de cada letra.
  El tamaño, la posición, el color y la forma sí.

## Las fases

Cada fase se traduce entera, se compara con la referencia y se publica. El bar sigue con XP hasta que la de Linux esté igual.

1. **La base**: SkiaSharp en el sistema, la ventana y la composición de piezas como el modo GPU de XP, las letras de XP, el bucle y las
   teclas y el ratón. (Program.cs, Form1.cs: LayoutControls y lo que arma la pantalla.)
2. **La pared y las letras** (Form1.Pared, la barra de letras de Form1.Vistas, PantallaVivaGpu tanda 1, PrecargaPaginaGpu,
   CaratulasAdaptativas, Form1.Portadas, CaratulasSeparadasGpu, PreviewsIndexadosGpu).
3. **Dentro del disco** (la lista de canciones de Form1.Vistas, DiscoVivoGpu).
4. **Lo que suena, los créditos y la cola** (SuenaVivoGpu, EstadoReposoGpu, TextoMovilGpu, FranjaLigeraGpu, RelojPantallaGpu).
5. **El fondo**: el cielo, la aurora, las estrellas, la luna, la estrella 3D (Form1.Fondo, D3D9Star, CieloVivoGpu, CieloNaturalGpu,
   AuroraFinaGpu, EstrellaGpu, Estrella3dGpu, LunaGpu, PulidoGpu, AcabadoGpu, PaletaCaratulaGpu, EstiloGpu).
6. **El buscador** (Form1.Buscador, BuscadorPortadasGpu, BuscadorLigeroGpu).
7. **La atracción, la ayuda y el asistente** (AtraccionGpu, AyudaCasillaGpu, GuiaRojoGpu, AsistenteRocola, AsistenteGuiaRocola,
   AsistenteEstrellaGpu, AsistentePersonalidadGpu).
8. **Ctrl+F3 entero** (Form1.Ajustes, AjustesVivos, HojasFondo, Rendimiento, PerformanceGpu, el diagnóstico completo).
9. **El video** (Form1.Video).
10. **Lo no visual que falta**, función por función (Form1.Biblioteca, Form1.Datos, Form1.Sonido, Form1.Ia, las salidas, el resto).
11. **Lo de Rocola OS, encima y con cuidado**: el ratón, internet, Spotify (todo SP y P), el cielo por hora y fecha y el clima, el color
    **Decisión del dueño del 8-11-2026:** empieza cuando la traducción esté lista. Lo no visual se pone encima. Lo visual del programa
    de Linux anterior no se trasplanta: se proponen ideas visuales nuevas que conserven el aspecto de XP y solo le añadan, y el dueño
    elige antes de hacerlas.
    vivo, el movimiento, el vidrio y la letra, el buscador flexible y por letra, la conversión de archivos, Rocola en casa y el buzón,
    las actualizaciones, los botones de La rocola. Cada uno como opción coherente con lo traducido, con su interruptor donde toque.

Mientras dura: el programa de Linux actual se queda como está (no se le añade nada); lo nuevo se construye aparte y lo sustituye cuando
llegue a la fase 11.
