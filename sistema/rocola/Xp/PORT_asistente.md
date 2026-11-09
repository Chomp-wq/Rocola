# Port nativo de XP: el asistente, la casilla de AYUDA y el degradado tramado, de GDI+ a SkiaSharp directo

Grupo «asistente» de la tanda 2 del port nativo (el dibujo con GDI+ de los archivos de la capa de la tarjeta), 9-11-2026: `AsistenteRocola.cs`,
`AsistenteGuiaRocola.cs`, `AsistenteEstrellaGpu.cs`, lo que faltaba de `AyudaCasillaGpu.cs` y de `PulidoGpu.cs` (`PintaInterrogacion`,
`BakeInterrogacion` y `HorneaMapaDegradado` ya estaban: `PORT_DIBUJO.md`, D4). Decisiones del dueño en `AGENTS.md` (8-11-2026: «que tradujeras
todo el código para que trabajara nativamente en Linux», «ningún layer de traducción») y la receta en `PORT_DIBUJO.md` (D2-D4,
`Pruebas/BancoDibujo/EjecutaNativo.cs`). Base: `f2f9dcb`; rama `port-asistente`. Aquí va lo que se tradujo, el estado del `Graphics` de cada
función, cada costura con lo que todavía no está traducido (con su razón y cuándo se quita), las comprobaciones con sus números y lo que queda.
Nada de esto se probó en el A4 (no se puede desde aquí): todo es fuera de pantalla con llvmpipe.

## Qué se tradujo

Los cinco archivos ya no ven los tipos que imitan a GDI+ (`using Rocola.Dibujo2D` fuera; `using System.Drawing; using Rocola.Nativo; using
SkiaSharp;` en la misma línea de antes, o solo `SkiaSharp` en `AsistenteEstrellaGpu.cs`); los de la capa solo quedan escritos enteros en las
costuras de abajo (el `GraphicsPath` de `RoundedPath`) y en la línea de WinForms `OnPaint(PaintEventArgs e)`. Todo su dibujo va con SkiaSharp
(`SKCanvas`, `SKPaint`, `SKPath`, `SKShader`, `SKBitmap`) y las funciones nativas (`Nativo/`), línea por línea como en XP: cada línea que cambia
lleva su `// EN LINUX: ...`, los archivos conservan su número de líneas (495, 534, 485, 329 y 499: donde XP tenía un `using` de un
`StringFormat`, o un `try`/`catch` que en Skia no hace falta, las líneas quedan como comentario `EN LINUX` o cambian de papel con la de al lado,
como en D4) y sus finales de línea (LF, sin BOM). La lógica de XP no se tocó: mismos valores, mismo orden, mismas ramas.

| Archivo | Función | Lo que dibuja | Cómo quedó |
|---|---|---|---|
| `PulidoGpu.cs` | `DegradadoV`, `CopiaDegradado`, `HorneaDegradadoV` | el degradado vertical tramado de los paneles (C4: globos, panel de reposo, vidrio de la lista) o el lineal de siempre | dan el **shader** del pincel (`Degradado.Lineal` o `Degradado.Textura` con la traslación del `TranslateTransform` de XP), con un parámetro nuevo `medio`: el medio píxel del dibujo donde se pinta (lo que GDI+ mira al pintar con el `TextureBrush`; la capa lo tomaba del `Graphics` en `Pon`). El guardado de cuatro mapas (`DegradadoGuardado.Mapa`) es de `SKBitmap`, sin el puente que tenía (las `Entrega` se fueron). Skia copia los puntos del mapa al hacer el shader, como el `TextureBrush` de GDI+ |
| `AsistenteRocola.cs` | `AsistenteGloboCamino` | el contorno del globo con su cola | un `SKPath` hecho como lo hace un `GraphicsPath` de GDI+ (regla 5): relleno par-impar de fábrica, el primer arco con `forceMoveTo` y los demás arcos, líneas y Bézier unidos con una línea al punto anterior (`Hasta` de la capa: `AddLine` y `AddBezier` empiezan con un `LineTo` a su primer punto), las cajas como `(x, y, x + ancho, y + alto)` con las mismas cuentas de `float` |
| | `PintaAdiosAsistente`, `PintaPanelAsistente` | los globos de la despedida y de la pregunta (con las tres opciones, sus teclas y la leyenda) | `SKBitmap` (`Mapas.Nuevo`), `SKCanvas`, `Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), ...)`, `SKPaint` (pinceles y plumas), `Pixel.Traza/Rellena`, `TextoGdiPlus.Escribe/Mide`, `FormatoTexto`; `Font.Size` es `Letra.Tamano` y `new Font(opcion.FontFamily, px, opcion.Style, Pixel)` es `Letra.EnPixeles(opcion.Familia, px, opcion.Negrita, opcion.Cursiva, opcion.Subrayada)`; `MeasureString(s, f, PointF.Empty, formato)` mide en la caja `(0, 0)` |
| | `asistentePanel`, `EnsureAsistentePanel` | el globo que se le da a la tarjeta | `SKBitmap` de punta a punta |
| `AsistenteGuiaRocola.cs` | `PintaGuiaActual`, `PintaGuiaAsistente` | el globo pequeño de la guía (paso, título, texto, teclas, pie) | `SKBitmap`; lo mismo que los de arriba; el `new StringFormat { Alignment = Far }` del pie es `new FormatoTexto { Horizontal = Alinea.Lejos }` |
| | `PintaTeclas`, `TeclaFlecha` | los botones dibujados (rojo, verde, moneda, flechas, «+» y «o») | `(SKCanvas, suave, ajuste, copia, medio, ...)`: el estado del que llama como parámetros; la letra, una `Letra`; la moneda con `Pixel.RellenaOvalo` y `TrazaOvalo` |
| `AsistenteEstrellaGpu.cs` | `AsistenteEstadoGpu.Panel`, `asistenteBmp`, `PreparaAsistente` | la subida del globo | `SKBitmap`: `Upload` (`D3D9Star.cs`) ya lo recibe tal cual, sin el puente de `Puente.frontera.cs`; `asistenteBmp` solo se compara por identidad, como el `Bitmap` de XP |
| `AyudaCasillaGpu.cs` | `AyudaView.Ancho` | lo que mide la casilla con un alto | `TextoGdiPlus.Mide` sin el mapa de 1×1 que XP hacía solo para tener un `Graphics` con que medir; la letra, `Letra.EnPuntos(Letras.SinRemates, ..., negrita)` (la genérica sin remates de XP, en puntos) |
| | `AyudaView.RenderContent` | la casilla: el fondo dorado, el signo (sin tarjeta), «AYUDA», el rojo, el «+» y la flecha | `(SKCanvas, copia, medio, forGpu)`: el suavizado y el modo de letra los pone ella al empezar (en variables), la copia y el medio píxel son los del que llama; su letra (`fTxt`) es una `Letra` |
| | `AyudaView.FlechaAbajo` | la tecla de la flecha de abajo | `(SKCanvas, suave, ajuste, copia, medio, ...)` |
| | `PreparaAyudaIcono` | la subida del signo | el `SKBitmap` de `BakeInterrogacion` va tal cual a `Upload` (sin la `Entrega`) |

Las funciones que el código sin traducir sigue llamando con la firma de XP tienen esa firma en el puente (abajo). Los demás llamadores son del
grupo y llaman a las traducidas.

## El estado del `Graphics`

| Dónde | Suavizado | Modo de letra | Copia | Medio píxel | Matriz y recorte | De dónde sale |
|---|---|---|---|---|---|---|
| `PintaAdiosAsistente`, `PintaPanelAsistente`, `PintaGuiaAsistente` | sí | `Suave` (AntiAlias) | no | no | identidad, sin recorte | fijo: el `Graphics.FromImage` de XP y sus dos líneas (tabla: `AsistenteRocola.cs` 397-464 y `AsistenteGuiaRocola.cs` 280-295, `s=AntiAlias t=AntiAlias c=SourceOver p=Default m=I r=no` en todas sus llamadas, `varia` = «-») |
| `TeclaFlecha` | parámetro | parámetro | parámetro | parámetro | la del lienzo | tabla, 334-335: 68 llamadas, todas con el estado de arriba (sus dos llamadores, `PintaPanelAsistente` y `PintaTeclas`, se lo pasan) |
| `PintaTeclas` | parámetro | parámetro | parámetro | parámetro | la del lienzo | la llama solo `PintaGuiaAsistente` (tabla, 305: la `Font`); la moneda (313-314) no la pasaron los recorridos (siempre había crédito): razonado desde quien la llama y pasada por el arnés |
| `AyudaView.Ancho` | — | `Suave` | — | — | — | fijo (tabla, 49-50: `s=None t=AntiAlias`) |
| `AyudaView.RenderContent` | sí (lo pone ella) | `Suave` (lo pone ella) | parámetro | parámetro | en el lienzo | tabla, 80-81 y 105-106: cambian la matriz, la del HDC y el recorte (`m=I b=T r=hdc` desde `OnPaint`, 349 llamadas; `m=T b=I r=no` desde `DrawCtrlOverlay`, 164): todo eso va en el lienzo; la copia y el medio píxel son los del `Graphics` del que llama (en los recorridos, `SourceOver` y `Default`) |
| `AyudaView.FlechaAbajo` | parámetro | parámetro | parámetro | parámetro | la del lienzo | la llama solo `RenderContent` |
| `PintaInterrogacion` (D4) | parámetro | — | parámetro | parámetro | la del lienzo | tabla, 130-131 (cambian el modo de letra, que el trazo no usa, la del HDC y el recorte) |
| `DegradadoV` | — | — | — | parámetro (`medio`) | — | el del dibujo donde se pinta el pincel: los globos pasan el de su lienzo (el de fábrica); `EstadoReposoGpu.cs` y `AcabadoGpu.cs` llegan por la firma de XP y `PuenteDibujo.Pincel(b, false)` |

## Costuras con lo que no está traducido (la regla: el puente va del lado de lo que no está traducido)

Todo lo del puente de este grupo está en `Plataforma/Puente.asistente.cs`, en una parte por archivo (cada una entró con el commit de su archivo).

| Costura | Dónde | Qué se hace | Se quita cuando |
|---|---|---|---|
| la firma de XP de `DegradadoV` (devuelve un `Brush` de GDI+) | la piden `EstadoReposoGpu.cs` y `AcabadoGpu.cs` (ya traducidos, la pasan a `SKPaint` con `PuenteDibujo.Pincel(b, false)`) | un shader no tiene vuelta a un pincel de GDI+ (lo mismo que `BrochaReposo` y `BrochaSuena`), así que la firma de XP lleva el camino de XP (`PulidoGpu.cs` 142-177) con los pinceles de GDI+ (`LinearGradientBrush`, `TextureBrush` con su traslación), sobre el MISMO guardado de cuatro mapas, el mismo cerrojo y las mismas cuentas (`DegradadosGuardados`, `DegradadosReusados`) que la traducida: los dos caminos se reparten los mapas como en XP. El `TextureBrush` pinta el mapa del guardado prestado (`PuenteDibujo.Bitmap`: el dueño es el guardado, como el `Bitmap` de XP) | esos dos llamen a la traducida, `DegradadoV(r, c0, c1, false)` (una línea cada uno) |
| la firma de XP de `AyudaView.RenderContent` (con un `Graphics`) | `OnPaint` (WinForms, en el mismo archivo) y `DrawCtrlOverlay` (`Form1.Video.cs`) | `PuenteDibujo.Lienzo(g)` (con su matriz y su recorte) y la copia y el medio píxel de `PuenteDibujo.Estado(g)`; después le deja al `Graphics` lo que le deja XP: `SmoothingMode` y `TextRenderingHint` en `AntiAlias`, si la casilla tiene medida para pintarse (las mismas cuentas de su primera línea) | `Form1.Video.cs` se traduzca y las ventanas sean nativas (`OnPaint`) |
| `CtrlDot`, `CtrlTxt` y `CtrlTri` (`Form1.Pared.cs`) pintan con un `Graphics` (y `CtrlTxt` con una `Font`) | `PintaPanelAsistente`, `PintaTeclas`, `TeclaFlecha`, `RenderContent`, `FlechaAbajo` | `CtrlDotAsistente`, `CtrlTxtAsistente`, `CtrlTriAsistente`: un `Graphics` sobre el MISMO lienzo con el estado del que llama (`PuenteDibujo.GraphicsAsistente`: el filtro de fábrica, que no usan; la matriz del HDC, la identidad, que solo mira `TextRenderer` y estas no lo usan) y la `Font` de la `Letra` (los mismos datos; se suelta al acabar). Ninguna de las tres le cambia el estado al `Graphics` | `Form1.Pared.cs` se traduzca: la llamada pierde el «Asistente» (con el lienzo y el estado como aquí) y las tres se borran |
| `RoundedPath` (`Form1.Fondo.cs`) da un `GraphicsPath` | las opciones del globo, la tecla de la flecha, la casilla | se suelta como en XP; se pinta su `SKPath` (`PuenteDibujo.Ruta`, el mismo, sin copia) | `Form1.Fondo.cs` dé un `SKPath` |
| `PrecargaPaginaGpu.cs` (sin traducir) precalienta los globos con `using (Bitmap panel = PintaPanelAsistente(...)) { }` | las dos líneas 101 y 102 | ahora `using (SkiaSharp.SKBitmap ...)`, con su nota `EN LINUX`: los globos dan un `SKBitmap` de punta a punta (la tarjeta lo sube tal cual, como pedía la tanda) y la firma de XP no puede quedarse en el puente, porque solo cambia lo que devuelve. Son las dos únicas líneas tocadas fuera del grupo | — (ya es la línea traducida de ese archivo) |

Ninguna parte del puente le cambia el estado al `Graphics` de quien llama, salvo lo que `RenderContent` le deja en XP.

## Lo que se supuso (y por qué es lo mismo)

- **El pincel es su shader, hecho para un medio píxel.** El `TextureBrush` de GDI+ no sabe dónde se usará; GDI+ corre la textura medio punto
  según el `PixelOffsetMode` del `Graphics` donde se pinta. `Degradado.Textura` (D2) necesita ese medio píxel al hacer el shader: por eso
  `DegradadoV` lo recibe. Sus usos de XP pintan siempre con el de fábrica (tabla: `p=Default` en todas), y los llamadores pasan el de su lienzo.
- **Skia copia los puntos al hacer el shader.** Comprobado con un programa aparte (SkiaSharp 3.116.1: se hizo el shader, se borró el mapa en
  rojo y se soltó; el shader siguió pintando los puntos de antes, con la traslación y la repetición bien). Así `HorneaDegradadoV` (más de 2 048
  de alto) suelta su mapa y el pincel sigue teniéndolo, como el `TextureBrush` de GDI+; en la capa no (abajo, «Riesgos»).
- **El contorno del globo con las mismas cuentas.** Las cajas de los arcos se escriben como las calcula la capa (`x + ancho` con el ancho ya
  redondeado a `float`: `derecha - r * 2 + r * 2`, que no siempre es `derecha`); el arnés compara verbos, puntos y pesos bit a bit.
- **`CtrlDot`, `CtrlTxt` y `CtrlTri` con un `Graphics` nuevo sobre el mismo lienzo** son lo mismo que con el de XP: solo rellenan, trazan, miden
  y escriben con el suavizado, el modo de letra, la copia y el medio píxel del que llama (ni recortes, ni `Save`/`Restore`, ni
  transformaciones, ni `TextRenderer`, ni imágenes).
- **La casilla pintada por `OnPaint`** lleva la matriz de su sitio en la del HDC (`b=T`); lo que la traducida le pide a `CtrlTxt` va con la
  identidad como matriz del HDC, que solo usaría `TextRenderer`.
- Todo esto corre en el hilo de la pantalla (también el precalentado de `PrecargaPaginaGpu.cs`, que llama `Form1.Portadas.cs` desde su reloj).

## Comprobaciones

Con datos y programas en `/root/rocola-trabajo/port/asistente` (fuera del repositorio), la máquina compartida con otros agentes (carga de 4 a
8 en 4 núcleos durante las corridas) y llvmpipe. Nada en el A4. Los guiones, en `Pruebas/VistasAsistente`.

- **Compila** (`dotnet build -c Release`, también `--no-incremental`): 0 errores y los mismos 15 avisos de la base `f2f9dcb` (las dos listas
  ordenadas, `archivo(línea,columna): código`, iguales línea por línea: `SYSLIB0054` ×9, `CA2022` ×2, `SYSLIB0050`, `CA2014` y `CS1690` ×2;
  ninguno de este grupo), y así **en cada uno de los cuatro commits** (sacados con `git archive` y compilados aparte, `--no-incremental`:
  0 errores y la lista de avisos de la base en los cuatro).
- **Diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py ... -v`): `AsistenteRocola.cs` 98 líneas quitadas y 95 puestas, `AsistenteGuiaRocola.cs`
  50 y 47, `AsistenteEstrellaGpu.cs` 3 y 3, `AyudaCasillaGpu.cs` 48 y 45, `PulidoGpu.cs` 22 y 20, `PrecargaPaginaGpu.cs` 4 y 3; **0 líneas sin
  marca** salvo una en `AsistenteGuiaRocola.cs`, una llave `{` de XP que no cambió y que el diff mete en el trozo de `PintaGuiaAsistente` (la
  diferencia entre quitadas y puestas son los `using` de XP en una línea, de D1; a la continuación de la línea del degradado del signo, de
  D4, se le puso su marca). `git diff --numstat f2f9dcb`: 78/78, 46/46, 3/3, 28/28, 16/16 y 2/2 (las mismas quitadas que puestas); los
  archivos siguen en 495, 534, 485, 329, 499 y 231 líneas, LF y sin BOM.
- **Banco por función** (`Pruebas/VistasReposo/banco.sh`, con la salida en esta carpeta; este grupo no toca `Nativo/`, `Dibujo2D.cs` ni
  `Puente.cs`, y `Puente.asistente.cs` no entra en el banco; se corrió igual): semillas 7 y 11 con las letras de XP falsas y la 5 con
  DejaVu, 3 000 casos, ejecutores `gdi`, `nativo` y `puente` contra la capa congelada: **0 distintos en los 27 000**; `puente-objetos`:
  **407 bien, 0 mal**.
- **Arnés** (`arnes.sh`: `VistasAsistente.csproj` y `Asistente.cs`, el programa entero de esta copia y el de la referencia congelada con la
  misma entrada): **1 758 líneas iguales línea por línea** a la referencia, con las letras de XP falsas y con DejaVu (928 imágenes
  distintas; 512 llamadas directas a las traducidas, que en la referencia son la llamada de XP otra vez). Son 74 globos de la pregunta
  (las siete pantallas de 2560×1440 a 480×437 con las cuatro selecciones, y nueve medidas raras: de menos de 64 —el globo rectangular—,
  justo 64, angostas —las opciones no caben y la letra se achica—, de 1×1 y negativas), 32 de la despedida y 260 de la guía (los catorce
  pasos con sus textos y teclas, los dos logros, textos nulos, todas las teclas con la moneda), con la interfaz tramada y sin ella; 9
  contornos del globo (verbos, puntos y pesos de las cónicas, bit a bit); 96 flechas y 160 tiras de teclas en los ocho estados del
  `Graphics` (suavizado, los modos de letra, copia, medio píxel, traslación entera y con decimales, escala, giro y recorte); 30 anchos de la
  casilla; 448 casillas por la firma de XP (siete medidas —dos sin medida para pintarse—, compacta o no, abierta o no, con y sin tarjeta,
  en los ocho estados, con lo que le queda puesto al `Graphics` y dónde quedó la tecla) y 448 directas; 24 flechas de abajo; 106 degradados
  (lineal, sin ancho y sin alto —`OutOfMemoryException` en las dos—, tramados de 2 a 2 048, movidos de su esquina, rellenando rectángulos y
  elipses, en los ocho estados, por la firma de XP y directos), las cuentas del guardado tras cada uno (47 líneas, al final 52/203 en las
  dos) y los de más de 2 048 (la firma de XP lanza `ArgumentNullException` al pintar en las dos; la traducida directa sale igual que el
  `TextureBrush` de GDI+ con el mapa vivo hecho en la referencia).
- **Controles negativos del arnés** (`control.sh`: un fallo metido a propósito en una copia, compilada aparte): el primer arco del globo
  sin unir al anterior, 340 líneas distintas de 1 758; el degradado del globo con medio píxel, 35; el puente sin dejarle el estado al
  `Graphics`, 280; la flecha sin suavizar, 326; la letra de las teclas en puntos, 214; la casilla sin la copia, 160; el pie de la guía con el
  tipográfico compartido, 210; el guardado sin reusar, 47. El arnés ve los ocho.
- **El arnés de reposo** (`Pruebas/VistasReposo/arnes.sh`, con la salida en esta carpeta: `EstadoReposoGpu.cs` pide `DegradadoV` por la
  firma de XP del puente): **1 791 líneas iguales** a la referencia, con las letras de XP falsas y con DejaVu, también la última, con las
  cuentas del guardado (`degradados=28/68` en las dos).
- **Los recorridos** (`Pruebas/Recorridos`, los nueve, con el candado y la carpeta de siempre, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra las dos
  corridas de la referencia de D2; salida en `cmp`): 0 excepciones en lo que escribió la rocola y en `rocola_error.txt` en los nueve, y
  todas las capturas (95). **Capas de Dibujo2D quietas: 216 de 217**; la que falta es la hoja de Ctrl+F3 de `hojas` a los 18 s (capa 4),
  la que `PORT_JUNTAS.md` explica: depende de cuánto lleva medido el repaso de la biblioteca («faltan 5» frente a «faltan 4»), que va más
  despacio con la máquina cargada desde que la traducción pone las prioridades de hilo de XP; su huella (`8653e10a…`) es la misma que dio la
  rama principal al juntar (`junta/cmp`), y la referencia también da otras en otras corridas (`b8e88a60…` en `junta/pares/h-ref1` y
  `h-ref2`). Texturas quietas: **479 de 487**. Las 8 que faltan son de la hora y ninguna es de este grupo: en `ayuda`, dos franjas de 1024×128
  (`c9e4fc67…`, que la referencia sube dos veces y la traducción una, ya apuntada en `PORT_frontera.md`, y `584659ad…` a los 7 s); en
  `buscador`, `e843916c…` (entre dos letras tecleadas) y `8cd9108c…` (la franja a los 26 s); en `pared2`, `584659ad…`, `2a697d30…` (el
  primer panel del estado extendido) y dos de la tanda de SALTAR a los 62 s (`21d8fe91…`, `2f591363…`). Todas salen idénticas en corridas de
  la traducción de otros grupos. Que es la hora y no la traducción:
  - `ayuda`, `buscador` y `pared2` otra vez, en pares seguidos traducción-referencia con la misma carga (`cmp-b2` y `cmp-r3`), y `ayuda` dos
    veces más (`cmp-b3`, `cmp-b4`): la referencia, con la misma carga, también pierde quietas (`cmp-r3`: 63 de 66, 77 de 78 y 92 de 98;
    la traducción, 64, 76 y 94 en `cmp` y 53, 76 y 96 en `cmp-b2`); en `cmp-b3` y `cmp-b4` salen `584659ad…` a los 7 s y, en `cmp-b3`,
    `c9e4fc67…` dos veces, como en la referencia;
  - juntando mis corridas, **483 de las 487** quietas salen idénticas; las 4 que no (`e843916c…` y `8cd9108c…` del buscador, `2a697d30…` y
    `2f591363…` de `pared2`) son las que ya apuntaron frontera y reposo, salen en corridas de la traducción de otros grupos y le faltan a
    corridas de la referencia (`cmp-r3` no tiene ni `8cd9108c…` ni `2f591363…`);
  - las capas de Dibujo2D quietas de esos tres recorridos salen idénticas en todas las corridas: las 26 de `ayuda` en las cuatro de la
    traducción y en `cmp-r3`, y las 22 de `buscador` y las 12 de `pared2` en `cmp`, `cmp-b2` y `cmp-r3`;
  - lo que sale en todas las corridas de un lado y en ninguna del otro (`cmp` y `cmp-b2` contra `cmp-ref`, `cmp-ref2` y `cmp-r3`): en `ayuda`
    (con las cuatro de la traducción) nada; en `buscador`, dos de la traducción (`1f1e16c5…` y `4d4dd133…`, al cerrar el buscador a los
    34 s) y una de la referencia (`e843916c…`); en `pared2`, cuatro de la traducción y una de la referencia: todas salen también en
    corridas del otro lado o de otros grupos (las cuatro de `pared2`, en `reposo/ref-hoy`, una corrida de la referencia; las dos del
    buscador, en `junta4/cmp` y `atraccion/cmp`, la traducción sin este grupo; `e843916c…` en `junta/cmp` y `pantalla-viva/cmp2`). Las
    texturas de una sola corrida son estados intermedios de las animaciones (franjas de 1024×128 entre dos teclas), en números parecidos
    a los de la referencia (en `buscador` y `pared2`, `cmp-r3` tiene 15 y 38 que no salen en ninguna otra);
  - los globos de la estrella (las texturas de 1024×512 y 512×256 de `ayuda`) salen todos en `cmp`. En una primera corrida de `ayuda`, antes
    de la de los nueve, salió además uno que no tenían las dos de la referencia (`173044c2…`, el globo con la segunda opción elegida, a los
    23 s: entre las dos pulsaciones de ABAJO la referencia no llegó a subirlo), y la referencia lo sube igual en otra corrida
    (`reposo/diag-ref`, a los 22,8 s).
- **NativeAOT** (`dotnet publish -c Release -r linux-x64 -o /root/rocola-trabajo/port/asistente/aot`): los mismos 15 avisos del compilador
  y **ningún aviso IL ni de AOT** (0 IL2xxx/IL3xxx). El binario nativo (`aot-corre.sh`, con `Pruebas/Tarjeta/guiones/corre.sh`, los datos de
  los recorridos, el sonido a tiempo real y las teclas y capturas de los recorridos) por `ayuda` y `pared`: 13 de 13 y 12 de 12 capturas,
  **0 excepciones** en lo que escribió y en `rocola_error.txt`; las capturas enseñan el globo de la pregunta (las tres opciones, la elegida
  con sus flechas y el botón verde, la leyenda «mover, elegir, salir»), el de la guía («PASO 1 DE 5», las teclas amarillas, «ROJO + ABAJO:
  guardar la ayuda» a la derecha) y la casilla de AYUDA en la fila de arriba.
- **Cintas de la tarjeta**: no hacen falta (este grupo no toca la tarjeta, `D3D9Star.cs` ni `Nativo/`: el globo y el signo llegan a la misma
  `Upload` como `SKBitmap`, lo que antes le daba la sobrecarga del puente de `Puente.frontera.cs` con los mismos puntos; lo que se sube lo
  comparan los recorridos).

## Riesgos y diferencias que quedan

- **Los degradados de más de 2 048 de alto por la firma de XP del puente**: lanzan `ArgumentNullException` al pintarlos, como en la
  referencia y en la base (la capa guarda la imagen en el `TextureBrush` y `HorneaDegradadoV` la suelta antes); la traducida los pinta como
  GDI+ (Skia copia los puntos al hacer el shader: el arnés lo compara con el `TextureBrush` de XP con el mapa vivo). Por la firma de XP
  llegan `EstadoReposoGpu.cs` (el panel mide como mucho 340 de alto: nunca) y `AcabadoGpu.cs` (el vidrio de la lista del disco, del alto de
  la lista: solo con listas de más de 2 048 px, no en el A4 a 1024×768). Se va cuando esos dos llamen a la traducida (lo apuntó la
  revisión de reposo; aquí queda arreglado en la traducida y no en la firma de XP, para que las comparaciones con la referencia sigan
  exactas).
- **La lógica de `DegradadoV` en dos sitios mientras dure el puente** (la traducida y la firma de XP, con el mismo guardado, cerrojo y
  cuentas): si cambiara en XP, hay que pasarlo a las dos; mejor, que los dos que faltan llamen a la traducida y se borre la del puente.
- **Un `SKBitmap` ya soltado tumba la rocola** (el riesgo general de la revisión de reposo): el globo (`asistentePanel`) se suelta al
  cambiarlo en `EnsureAsistentePanel` —en la misma llamada que le da el nuevo a la tarjeta— y en `SueltaAsistente`; la tarjeta solo compara
  el de antes por identidad (`asistenteBmp`) y sube el que le da `BuildAsistenteState` en ese momento, y solo si se ve. El `TextureBrush`
  de la firma de XP toma el mapa del guardado prestado, y los que lo piden lo pintan al momento, en el hilo de la pantalla, sin otra
  llamada a `DegradadoV` en medio. Quien cambie ese orden tiene que conservarlo.
- **`CtrlTxtAsistente` hace una `Font` (y su `SKFont`) en cada llamada** donde XP usaba la misma (`fTxt`, `pequeno`, `chico`): las mismas
  letras y el mismo dibujo (el arnés y los recorridos lo comparan), algo más de trabajo al rehacer la imagen de la interfaz; se va con la
  traducción de `Form1.Pared.cs`.
- **`PrecargaPaginaGpu.cs`**: dos líneas tocadas fuera del grupo (el tipo de lo que se suelta), con su nota.
- La hoja de Ctrl+F3 del recorrido `hojas` a los 18 s sigue saliendo según cuánto midió el repaso de la biblioteca (`PORT_JUNTAS.md`): no es
  de este grupo.
- No se vio ningún fallo de XP en estas funciones.
- Nada de esto se probó en el A4.

## Para quien junte

La rama se junta sin choques con la principal de ahora (`99000e8`, con el grupo acabado-estilo ya dentro): probado en una copia aparte
(`git merge --no-commit`), compila con 0 errores y los 15 avisos de siempre, y el arnés de acabado-estilo
(`Pruebas/VistasAcabadoEstilo/compara.sh`, que pasa por `BakeSongGlassTint` y la firma de XP de `DegradadoV` de este puente) sale con **0
líneas distintas** de 6 603 frente a la referencia y de 6 607 frente a su base, con las dos letras. Después de juntar, las dos llamadas a
`DegradadoV` con la firma de XP (`EstadoReposoGpu.cs:353` y `AcabadoGpu.cs:347`) pueden pasar a `DegradadoV(r, c0, c1, false)` (un
`SKShader` en un `SKPaint`, en lugar de `PuenteDibujo.Pincel`), y entonces se borra esa parte de `Puente.asistente.cs`.

## Lo que queda

- Las costuras de arriba, cada una cuando se traduzca el archivo del otro lado: `EstadoReposoGpu.cs` y `AcabadoGpu.cs` (llamar a
  `DegradadoV(r, c0, c1, false)` y borrar la firma de XP del puente), `Form1.Video.cs` y las ventanas nativas (la firma de XP de
  `RenderContent`), `Form1.Pared.cs` (`CtrlDotAsistente`, `CtrlTxtAsistente`, `CtrlTriAsistente` y `GraphicsAsistente`), `Form1.Fondo.cs`
  (`RoundedPath`). Al final, `Puente.asistente.cs` entero se borra con `Dibujo2D.cs` y `Puente.cs`.
- El resto de `AyudaCasillaGpu.cs` que no es dibujo (`AyudaView : Control`, `OnPaint`, `Invalidate`, `SetBounds`...) es de las ventanas.
- Las diferencias (a)-(e) de la referencia frente a GDI+ de verdad (`PORT_DIBUJO.md`, D0): la traducción copia la referencia. De estos archivos
  les toca la (a) solo al mapa del degradado (Format32bppArgb premultiplicado al guardarse; ya en D4) y la (e2) solo si quien llama a
  `RenderContent`, `TeclaFlecha` o `PintaTeclas` trajera una traslación con decimales, escala o giro (en los recorridos, la casilla solo ve
  traslaciones enteras).
- Nada de esto se probó en el A4.
