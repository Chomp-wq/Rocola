# La rocola de XP traducida a Linux (en curso)

Decisión del dueño del 7 de noviembre de 2026: traducir **todo** el código de la rocola de XP para que funcione de forma nativa en Linux
e idéntica. El plan está en `PLAN_TRADUCCION_XP_A_LINUX_MODEST_2026-11-07.md` y la regla en `AGENTS.md`.

## Qué hay aquí

- `Codigo/`: los 109 archivos de `Rocola/`, copiados el 7-11-2026 con la parte mecánica de la traducción ya hecha:
  - se quitaron los `using` de WinForms y GDI+ (`System.Windows.Forms`, `System.Drawing.Drawing2D`, `Imaging` y `Text`) y sus nombres
    completos;
  - todo lo demás está igual que en XP.

  Lo que es de Windows se traduce a mano, archivo por archivo: las llamadas a `user32`, `kernel32` y `winmm`, Direct3D 9 y el reproductor
  de video. Si mientras tanto cambia `Rocola/`, el cambio se pasa también aquí.
- `Plataforma/`: lo nativo de Linux con que se traduce:
  - `Dibujo2D.cs`: GDI+ hecho con SkiaSharp. Usa las letras de XP de `/rocola/datos/letras` (las copia `rocola-discos` del disco de
    Windows: Microsoft Sans Serif, Trebuchet MS, Tahoma, Segoe UI, Arial y, desde el 7-11-2026, Times New Roman y Courier New, las
    genéricas de GDI+; si faltan, DejaVu Sans) con el alto de
    renglón sacado de las tablas de cada letra, el margen de 1/6 de letra de GDI+, el corte con «…» y el ajuste a los píxeles.
  - `Ventanas.Tipos.cs`: las teclas y los tipos sencillos de las ventanas, con los valores de Windows.
  - `Traduccion.cs`: los `global using`, para que cada archivo traducido quede línea por línea como el de XP.
- `RocolaXp.csproj`: el programa nuevo (.NET 10, NativeAOT, SkiaSharp 3.116.1, NAudio.Core 3, NLayer 3).

## Cómo va (7 de noviembre de 2026)

- **Compila entero, con la tarjeta gráfica**: los 109 archivos de XP (69 000 líneas) con la capa de Linux. Ojo: hasta ahora el proyecto no
  definía `ROCOLA_GPU` (el de XP sí), así que los 52 archivos de la tarjeta (`#if ROCOLA_GPU`) se compilaban vacíos; ya está definido y
  compilan. Lo traducido a mano hasta ahora:
  - las ventanas (`Ventanas.*.cs`: controles, formulario, temporizadores, bucle, foco, teclas y capas);
  - el dibujo (`Dibujo2D.cs`, más los codificadores JPEG/PNG, los formatos con paleta, el degradado radial con `Blend`, la clave de color
    de `ImageAttributes`, `Matrix.TransformPoints` y `MeasureString` con letras y renglones);
  - **Direct3D 9 en OpenGL ES 2** (`Direct3DaGL.cs`): cada llamada de XP a Direct3D por su tabla (`D3D9.Fn<T>(objeto, hueco)`, 36 sitios
    en `D3D9Star.cs`, `PerformanceGpu.cs` y `TexturasCompatiblesGpu.cs`) se cambió por la función del mismo nombre de `D3D9.EnGl`
    (`present = D3D9.EnGl.Present;`). Objetos con su cuenta de referencias como COM; colores en el orden de Direct3D de punta a punta (se
    suben tal cual); la regla de medio píxel de Direct3D 9; todo se pinta en texturas (el fondo y las imágenes intermedias); las etapas
    de textura (`SetTextureStageState`) se convierten en un programa de OpenGL por combinación; bloquear da una memoria aparte que se
    sube al soltar; `Present` pasa el fondo al escritorio (`Escritorio.PresentaNativa`). El resto de XP no cambia;
  - NAudio 3 (`NAudioXp.cs`: el puente con `Span`, y el MP3 con libmpg123 en lugar del ACM);
  - la salida de sonido de XP (`ISistemaWaveOut`) en ALSA (`SonidoAlsa.cs`, con los errores de winmm);
  - lo que no es MP3, WAV ni AIFF, por ffmpeg (`AudioXp.cs`);
  - el volumen de Windows en ALSA (`VolumenWindows.cs`, con amixer);
  - las llamadas a Windows del bucle de la tarjeta (`GetDeviceCaps` del refresco, `PeekMessage`, `MsgWaitForMultipleObjectsEx`), a la cola
    del escritorio.

  En `Codigo/` solo cambian las 33 líneas `NAudio 3 (Puente.cs)` y lo que se tradujo a mano. Los finales de línea de XP se conservan.
- **Probado**: `rocola-xp --prueba-gpu 1024 768 cielo.png 30` pinta el cielo y las estrellas de XP (`GpuStarfield`, lo de la ventana de
  prueba F12) con la traducción, fuera de pantalla: el degradado de tres paradas y las estrellas con su brillo y su destello salen como en
  XP; 30 cuadros a 5,9 ms cada uno con llvmpipe (sin tarjeta) y ningún objeto de OpenGL suelto al acabar. No probado en el A4.
- **El anfitrión** (`Anfitrion.cs`): abre la pantalla por KMS (o fuera de pantalla: `ROCOLA_PANTALLA=fuera:1024x768`, o si no hay
  tarjeta) antes del `Main` de XP, en el mismo hilo, y en cada cuadro compone las capas del escritorio (Dibujo2D, que solo se suben al
  cambiar) con el fondo de la tarjeta de cada control «Nativa» (el `GpuPanel`, marcado así en su constructor), y lo presenta.
  `ROCOLA_CAPTURA=S:ruta.png,...` guarda la pantalla a los S segundos (`ROCOLA_CAPTURA_SALE=1`: y se cierra).
- **La rocola de XP arranca entera en Linux** (`dotnet rocola-xp.dll`, fuera de pantalla, con una carpeta `musica` de prueba): sin
  `rocola_teclas.txt` sale su asistente de botones («CONFIGURACION DE BOTONES DE LA ROCOLA, Paso 1 de 8»); con él, su pantalla principal:
  la barra de arriba (Mover, Entrar / Poner, Volver, BUSCAR, ayuda, LETRA L), las pestañas de las letras, el disco con «3 canciones», el
  cielo con nebulosas, estrellas, aurora, luna y la estrella asistente, y la barra de abajo («Nada reproduciendose», En cola, Inserta una
  moneda). Todo dibujado por el código de XP. Las letras salen con DejaVu (aquí no están las de Windows); en el A4, las de XP.
- **Las teclas** (`TeclasLinux.cs`): los teclados y placas de botones de `/dev/input` (también los que se conectan después) se pasan al
  código de Windows con la tabla de `Motor/Botones.cs` (así `rocola_teclas.txt` del bar vale tal cual) y llegan al escritorio como
  WM_KEYDOWN / WM_KEYUP; la repetición al dejar un botón apretado es la de XP de fábrica (500 ms y unas 30 por segundo), no la del núcleo.
  Probado con teclas programadas (`ROCOLA_PULSA`): PONER abre el disco con su lista de canciones y la moneda da sus créditos («+4
  creditos», «Creditos 8»), como en XP.
- **Arreglado (7-11-2026):** dentro de un disco el fondo salía blanco y a manchas. El anfitrión compone con su propia mezcla y la traducción
  creía que seguía puesta la suya: las nebulosas y la Vía Láctea (blancas casi transparentes) se sumaban enteras. Ahora, después de cada
  composición, se olvida también la mezcla (`EstadoTocado`). Encontrado con las herramientas de prueba de `PruebasLinux.cs` (traza de
  dibujos, volcado de texturas, saltar dibujos, capturar capas).
- **El ratón** (`TeclasLinux.cs` y `Ventanas.Escritorio.cs`): movimiento, rueda y botones de `/dev/input` (y pantallas táctiles) llegan a
  las ventanas como en Windows: el control de encima recibe el movimiento (MouseEnter / MouseLeave al cambiar), el que recibe el botón
  se queda con el ratón hasta soltarlo, al soltar dentro Click y MouseClick (o el doble clic de XP: 500 ms y 4 puntos) y después MouseUp;
  la rueda, al control con el foco. La flecha (o la mano, `Cursors.Hand`) la dibuja el anfitrión cuando XP la enseña (`Cursor.Show`/`Hide`
  cuentan como ShowCursor). Probado con pasos programados (`ROCOLA_PULSA=7:17+114,10:clic:712:53`): Ctrl+F3 abre la ventana de ajustes de
  XP entera y un clic en la pestaña «Imagen» la cambia, con la mano sobre el botón.
- **NativeAOT** (`dotnet publish -c Release -r linux-x64`, como irá en Rocola OS): un programa nativo de 7,8 MB que arranca y hace lo
  mismo fuera de pantalla (pantalla principal con la tarjeta del disco, disco abierto con monedas, Ctrl+F3). Avisos del compilador por la
  reflexión de XP: el lector rápido de MP3 (`Mp3Ligero.cs`) busca campos privados de NAudio 1.9; con NAudio 3 no los encuentra y XP abre el
  lector normal (el mismo sonido, con más basura para el recolector); el video por el reproductor de Windows (COM) va a mpv en su fase.
- **Pedir una canción** (con monedas y PONER) funciona: aviso «Pusiste ... ya está sonando», se gasta el crédito y el MP3 se abre y se
  descomprime (libmpg123: XP mide 529 muestras de retraso para el «sin huecos»). Hubo que quitar en `AperturaAudio.cs` el
  `Thread.SetApartmentState(STA)` de XP: en Linux no hay COM y lanzaba `PlatformNotSupportedException` (la canción quedaba como
  incompatible). Probado con la salida nula de ALSA (`ROCOLA_ALSA=null`, que no lleva el ritmo); el sonido de verdad, en el A4.
- **El video** (`VideoMpv.cs`, en curso): lo poco que XP usa del reproductor de Windows Media (URL, settings.volume y autoStart,
  controls.play, stop y currentPosition, playState, currentMedia.duration, uiMode, stretchToFit, enableContextMenu; y para medir la
  duración, newMedia y close) lo contesta `ReproductorWmp` con libmpv, con esos mismos nombres (XP los usa por reflexión; en NativeAOT se
  mantienen con `DynamicDependency`). El control del video de XP (`VideoHost`, un AxHost) lo recibe de `AxHost.Fabrica` y queda «Nativa»:
  el anfitrión pinta en su hueco la textura donde libmpv dibuja cada cuadro (su API de render con OpenGL, en el mismo contexto). El
  sonido lo saca mpv por ALSA (como el WMP, fuera de la cadena). El volumen del WMP se toma como amplitud lineal (lo que supone la cuenta
  de XP) y se pasa al volumen cúbico de mpv. La duración para la biblioteca, con ffprobe (`Type.GetTypeFromProgID` del WMP →
  `LlamadasWindows.TipoDeProgId`). Probado fuera de pantalla con un video de prueba: la letra VIDEOS, el disco, el crédito, el aviso, la
  lista que se hace chica, el tiempo que corre (de mpv) y, al acabar, PlayNext como en XP. El contexto de render se crea antes de cargar
  el video y lleva un `glFlush` tras cada cuadro. **Arreglado (7-11-2026):** la imagen salía de un color a los pocos segundos. libmpv pide
  OpenGL en su estado de fábrica y le llegaba el que deja la traducción de Direct3D (la mezcla encendida): cada pase de mpv se sumaba al
  del cuadro anterior hasta quedar magenta. Ahora, antes de cada llamada a mpv, `EstadoDeFabrica` pone ese estado (con el área de dibujo
  del tamaño del video). Y va sin girar (`FLIP_Y` 0: con 1 salía de cabeza). **Arreglado también (7-11-2026): el video negro.** En algunas
  vueltas salía todo negro (1 de cada 3 en la rocola, casi siempre en la prueba aislada). Era un fallo de mpv (el 0.37 de aquí y también
  el 0.40 de Rocola OS, `reinit_scaler` de `video/out/gpu/video.c`): sus escaladores de fábrica (lanczos al agrandar y en el color,
  hermite al achicar) suben su tabla de pesos con dos huecos por fila sin rellenar cuando el filtro tiene 6 puntos, y la leen con filtro
  lineal; con la cuenta exacta de llvmpipe, un NaN de esa basura por un peso de 0 da NaN y la imagen entera sale negra, según lo que haya
  dejado malloc en esa memoria. Pasa igual con un programa en C solo con libmpv. Ahora `VideoMpv.cs` escala como la tarjeta con el WMP
  de XP: bilineal y en una sola pasada (las opciones del perfil «fast» de mpv; mpv entra solo en su modo de una pasada), sin tabla de
  pesos y con mucho menos trabajo para la tarjeta (con llvmpipe, 2-3 ms por cuadro frente a 13-78 ms). Probado fuera de pantalla: la
  prueba aislada 20 de 20 y la rocola entera 5 de 5, con la imagen bien en todos los cuadros (y otra vez al juntarlo: 5 de 5 y 3 de 3);
  también un video de 1920x1080 y uno de 480x360. No volver a poner escaladores con tabla (lanczos, spline36, hermite, mitchell...) sin
  arreglar antes lo de la tabla. `rocola-xp --prueba-video RUTA [SEGUNDOS]` prueba el video solo, sin la rocola (corre al empezar el
  `Main` de XP: dentro del inicializador del módulo, los hilos de mpv se quedaban esperando) y dice al final cuántas lecturas salieron
  negras; `ROCOLA_VIDEO_TEXTURAS=S` vuelca las texturas de mpv con sus NaN (ver la cabecera de `PruebasLinux.cs`). No probado en el A4
  (r600).
- **Revisión de pantallas (fases 2 a 7, 7-11-2026):** se recorrieron fuera de pantalla:
  - la pared con la barra de letras (dos páginas y el deslizamiento);
  - el disco;
  - lo que suena, con créditos, cola y estado extendido;
  - el fondo, el buscador y la atracción;
  - la ayuda con el asistente y la guía del rojo mantenido.

  Se probó con música inventada (34 discos) y con una salida de ALSA que lleva el ritmo real (el complemento `file` de ALSA manda el
  sonido a una tubería que lo consume al ritmo del WAV). Arreglado en `Dibujo2D.cs`:
  - la lista del disco escribía dos veces, encimado, el renglón de debajo del elegido: los límites del recorte de Skia traen un píxel
    de más por lado, y ahora son exactos, como en GDI+;
  - las formas suavizadas siguen el medio píxel de GDI+ (el centro de los píxeles en las coordenadas enteras): las rayas de 1 px salen
    nítidas y los bordes de los rellenos a medias;
  - los recortes no se suavizan, como las regiones de GDI+;
  - la clave de color tolera un punto, por el redondeo de Skia;
  - el degradado con el centro corrido (la moneda que cae) es cónico, como en GDI+;
  - `HighQualityBilinear` y `HighQualityBicubic` prefiltran al achicar: las portaditas ya no salen a granos;
  - la pluma tiene el límite de esquinas 10 de GDI+;
  - `FontFamily` lanza si la familia no está en las letras de XP: en el A4, sin Segoe UI, Ctrl+F3 queda en Tahoma, como en XP.

  **Arreglado también, en `Ventanas.Escritorio.cs`:** el WM_KEYDOWN de la repetición (una tecla mantenida) no llevaba el bit 30 de
  lParam, que XP mira para no contar otra moneda, ni saltar otra vez, ni repetir sus atajos. La moneda mantenida 1,5 s daba 128
  créditos; ahora da 4, igual que una moneda corta.

  `ROCOLA_PULSA=S:VK~MS` deja una tecla apretada MS milisegundos, con la repetición de XP. Comportamientos que son de XP y no de la
  traducción:
  - un disco sin portada no tiene cara en la pared;
  - con la ayuda preguntando, ROJO+ABAJO la cierra con el rojo y la vuelve a abrir con abajo.

  Sin comprobar: si GDI+ corre también medio píxel el texto y las imágenes escaladas. No probado en el A4.
- **NativeAOT sin avisos y las llamadas a Windows (8-11-2026):** los 18 avisos de reflexión y de estructuras de Windows
  (IL2xxx/IL3xxx) se revisaron uno por uno. Se comprobó que el programa de JIT y el de NativeAOT hacen lo mismo, y cada supresión va con
  su motivo en `Plataforma/Aot.cs`. Al compilar y al publicar quedan 0 avisos. Se probó así:
  - con `rocola-xp --prueba-aot` (32 respuestas iguales en los dos);
  - con la rocola entera fuera de pantalla: pantalla principal, disco, moneda, pedir canción, el video con su tiempo y su fin, Ctrl+F3
    y el diagnóstico completo;
  - con el repaso, que da las mismas medidas.

  **El lector de MP3 de XP** es ahora el de NAudio 1.9 traducido línea por línea (`Plataforma/Mp3Xp.cs`, licencia MIT de NAudio). El de
  NAudio 3 no hacía lo mismo:
  - devolvía silencio tras dos saltos seguidos;
  - estimaba la duración al abrir;
  - se saltaba el cuadro «Info» de LAME;
  - perdía cuadros junto a trozos rotos y a etiquetas APE.

  Comparado con NAudio 1.9 de verdad (con Mono y `rocola-xp --prueba-mp3`) en 14 MP3 raros, sale igual línea por línea. Pedir una
  canción la toca con el «sin huecos» de XP (529 muestras de retraso). El lector ligero de XP (`Mp3Ligero.cs`) sigue apagado, como manda
  XP cuando NAudio no es la 1.9.

  **Las llamadas a Windows** están listadas en la cabecera de `LlamadasWindows.cs`:
  - el diagnóstico completo dice la tarjeta (OpenGL y `/sys/class/drm`) y la carga de la memoria;
  - la bitácora apunta las direcciones libres de verdad;
  - la prioridad «por encima de lo normal» del proceso llega a todos sus hilos (nice −6), como en Windows; antes solo llegaba al principal.

  `videoSinMedir` queda como en el A4 con Windows XP (los videos no se miden): en Linux la versión del sistema es la del núcleo y los
  habría medido. Medirlos va en la fase 11.

  Pendiente:
  - la prioridad de cada hilo (`Thread.Priority` no hace nada en Linux);
  - los `Mp3Frame.LoadFromStream` directos (comparar NLayer y el camino tolerante de `Form1.Sonido`), que siguen buscando cuadros como
    NAudio 3;
  - el nombre de la tarjeta por DRM, probado solo con una carpeta simulada.

  No probado en el A4.
- **Ctrl+F3 y los demás formularios (fase 8, 8-11-2026, en `Ventanas.*.cs`):** la capa de ventanas hace lo que WinForms en XP:
  - cada control se recorta por los hermanos que tiene encima; uno transparente enseña lo que pinta su padre, no lo de debajo (en «Lo que
    suena ahora» el «Necesita: …» salía encima de la nota);
  - el foco y el teclado son los de WinForms:
    - el orden del tabulador es el orden de entrada; Tab y Mayús+Tab entran en las tarjetas; las flechas se mueven dentro del mismo padre;
    - el foco empieza en la pestaña «Sonido»;
    - al esconder el control con el foco, el foco pasa al siguiente, y al cerrar una hoja vuelve a su sitio;
    - Intro pulsa el botón por defecto y Escape el de cancelar (Ctrl+F2 ya se cierra con Escape);
  - las hojas modales deshabilitan lo de debajo, y las ventanas con dueño quedan encima de su dueña;
  - la barra de título es la de XP, con su «X», y la hoja se arrastra;
  - MessageBox se ve como en XP y espera a Intro (PONER);
  - el botón y la lista de Ctrl+F2 se pintan como en XP;
  - la ventana de prueba F12 enseña su cielo.

  Probado fuera de pantalla:
  - las 6 páginas y sus 22 hojas, el teclado y lo guardado (32 vistas comparadas antes y después: iguales salvo lo arreglado);
  - «Medir», «Comparar perfiles» y el diagnóstico;
  - Ctrl+F2, el aviso de la carpeta de datos, F12 y el asistente de botones.

  Comportamientos que son de XP y se conservan:
  - tras «Ajustes del sonido…» con el teclado, el foco se queda en «Modo billar» ya escondido;
  - sin la pausa F10, la rocola tapa la ventana F12;
  - SALTAR sin nada sonando borra los créditos.

  Quedan: probar el arrastre de la hoja (ROCOLA_PULSA no sabe arrastrar), Alt+F4 (en XP cierra también la rocola), los iconos de MessageBox
  (dibujos aproximados) y el margen de TextRenderer (unos 1 px menos que GDI).
- **Todo junto (8-11-2026):** con los cuatro trabajos juntos (video, pantallas, NativeAOT y llamadas a Windows, Ctrl+F3), el programa de
  JIT y el de NativeAOT (`dotnet publish -c Release -r linux-x64`: 0 avisos, 7,9 MB) se probaron por el mismo camino, sin excepciones:
  - la moneda mantenida 1,5 s suma 4 créditos;
  - pedir una canción la toca a ritmo real;
  - Ctrl+F3 en la pestaña Imagen sale igual en los dos;
  - el video sale con su imagen en todos los cuadros.

  Con NativeAOT la fecha de la versión sale bien; con JIT da la del programa `dotnet`.
- **Los acentos (8-11-2026):** el programa corre sin los datos de idioma (`InvariantGlobalization`; Rocola OS no lleva ICU) y ahí
  `Normalize(FormD)` devuelve el texto tal cual. XP lo usa en 4 sitios para quitar los acentos: la letra de la pared, el buscador (dos)
  y el motor de búsqueda. Con eso «Ángel» se iba a «#» y «cancion» no encontraba «canción». Ahora esos 4 sitios llaman a
  `UnicodeXp.FormaD` (`Plataforma/UnicodeXp.cs`, una línea «EN LINUX» cada uno), que es la misma descomposición sacada de ICU (2061
  puntos de código, más el coreano con su regla). Comprobado:
  - igual que ICU en los 1 112 064 puntos de código, y lanza la misma excepción con un sustituto suelto o U+FFFE;
  - en textos con varias marcas seguidas solo cambia el orden de las marcas, que XP quita justo después;
  - en la rocola, un disco «Ángeles de Prueba» sale en la letra A, como en XP.

  Lo demás del idioma da igual que en XP si el A4 está en español de México: XP compara casi todo en ordinal (`OrdinalIgnoreCase`),
  escribe las fechas con su formato («dd/MM/yyyy HH:mm») y los números con `#,0` y `0.0`, que en es-MX salen igual que sin idioma.
- **Lo siguiente:**
  - sin capturas (decisión del dueño del 8-11-2026): «idéntico» se asegura por el código. `Codigo/` frente a `Rocola/` (diff sin lo
    mecánico, 8-11-2026): 494 líneas en 35 archivos, todas de traducción (el puente de NAudio 3, winmm a ALSA, el mezclador a amixer,
    Direct3D a `EnGl`, las ventanas, el foco, la memoria y el reloj de Windows a los de Linux, COM fuera, `videoSinMedir` como en el A4).
    Lo que queda es revisar la capa de `Plataforma` función por función contra lo que hacen GDI+, WinForms y Direct3D 9 en XP;
  - la prioridad de cada hilo (`Thread.Priority` no hace nada en Linux) y los `Mp3Frame.LoadFromStream` directos;
  - llevar el programa a la imagen del sistema: se le propone antes al dueño.
- **Medido con Mono:** el alto de renglón y `Font.Height` salen iguales a GDI+. Los anchos de texto quedan a 1 o 2 píxeles de libgdiplus,
  que no pone el margen de 1/6 de GDI+; por eso la referencia buena son las capturas de la rocola de XP, no Mono.
- Ninguna compilación del sistema ni del programa lo usa todavía (`sistema/rapida.sh` solo recompila el programa si cambian `Motor/` o
  `Consola/`). Nada probado en el A4.
