# Port nativo de XP: el buscador con portadas, de GDI+ a SkiaSharp directo

Grupo «buscador-portadas» de la tanda 2 del port nativo (el dibujo con GDI+ de los archivos de la capa de la tarjeta), 9-11-2026:
`Codigo/BuscadorPortadasGpu.cs`. Decisiones del dueño en `AGENTS.md` (8-11-2026: «que tradujeras todo el código para que trabajara
nativamente en Linux», «ningún layer de traducción») y la receta en `PORT_DIBUJO.md` (D2-D4, `Pruebas/BancoDibujo/EjecutaNativo.cs`). Base:
`f2f9dcb`; rama `port-buscador-portadas`. Aquí va lo que se tradujo, el estado del `Graphics` de cada función, cada costura con lo que
todavía no está traducido (con su razón y cuándo se quita), las comprobaciones con sus números y lo que queda. Nada de esto se probó en el A4.

## Qué se tradujo

`BuscadorPortadasGpu.cs` ya no ve los tipos que imitan a GDI+ (`using Rocola.Dibujo2D` fuera: `using System.Drawing; using Rocola.Nativo;
using SkiaSharp;` en la misma línea de antes); los de la capa solo quedan escritos enteros en las costuras de abajo (`RoundedPath` y la pieza
que sube `SubePieza`). Todo su dibujo va con SkiaSharp (`SKCanvas`, `SKPaint`, `SKShader`, `SKBitmap`) y las funciones nativas (`Nativo/`),
línea por línea como en XP: cada línea que cambia lleva su `// EN LINUX: ...`, el archivo conserva su número de líneas (1 881: las del
medidor de XP que ya no hacen falta quedan como comentario `EN LINUX`) y sus finales de línea (LF, ASCII, sin BOM). La lógica de XP no se
tocó: mismos valores, mismo orden, mismas ramas, las mismas cachés (los horneados y las miniaturas, con las mismas claves).

| Función | Lo que dibuja | Cómo quedó |
|---|---|---|
| `AseguraFuentes`, `Letra`, `Mide`, `Alto`, `Medidor` | las doce letras del buscador, sus cuatro formatos y las medidas | `Rocola.Nativo.Letra.EnPixeles(Letras.SinRemates, px, negrita, cursiva)` (`new Font(GenericSansSerif, px, estilo, Pixel)`); los formatos, `FormatoTexto`: `new StringFormat(StringFormat.GenericTypographic)` es `new FormatoTexto(FormatoTexto.TipograficoCompartido)` (una copia del compartido tal como esté), `|= MeasureTrailingSpaces | NoWrap` es `|= ContarEspacios | SinPartir`, `EllipsisCharacter` es `PuntosPorLetra`; `MeasureString(t, f, ancho, sf)` es `TextoGdiPlus.Mide` en una caja de ese ancho y 999 999 de alto; `GetHeight(g)` es `AltoRenglon`. El medidor de XP (un `Graphics` sobre un mapa de 1×1, hecho una vez) es su modo de letra (`AjusteLetra?`, puesto una vez como XP: el de un `Graphics` nuevo y después `AntiAlias`): de un `Graphics`, medir solo toma eso |
| `Resplandor`, `DibujaQuieto`, `PegaQuieto`, `LupaNueva` | lo quieto horneado: el resplandor, el fondo y el borde de la tecla, el atajo y la tarjeta elegidos, la casilla con su lupa, la sombra de la portada | el horneado es un `SKBitmap` (`Mapas.Nuevo`) con su `SKCanvas`, con el suavizado y el medio píxel del que llama (XP los copiaba) y sin copia; pegarlo es `Imagenes.Pinta` con el filtro, el medio píxel y la copia del lienzo; `FillPath`/`DrawPath` son `Pixel.Rellena`/`Traza`; la lupa, un `SKPaint` con su ancho y la punta redonda (`TrazaOvalo` y `Linea`) |
| `RenderNuevo`, `DibujaTeclaNueva`, `DibujaAtajo`, `DibujaAyudasNuevas` | la imagen grande: los atajos, el teclado sin elegir, la barra de ayudas y el aviso | `RenderNuevo(SKCanvas, ajuste, copia, filtro, medio, hdc)` y las demás con el estado que usa cada una; el `SmoothingMode = AntiAlias` de XP es la variable `suave = true`; `DrawString` es `TextoGdiPlus.Escribe` |
| `DibujaPiezaVivaNueva`, `DibujaCajaNueva` | las imágenes chicas: la tecla o el atajo elegido, la casilla de lo escrito (el cursor, la invitación, la cuenta, lo escrito que no cabe con «…» delante), la tarjeta elegida y la pieza de las tarjetas | `(SKCanvas, ajuste, copia, filtro, medio, ...)`; `suave = true` como arriba |
| `PintaCartasConCambios`, `HorneaCartas`, `DibujaSinCartas` | la pieza de las tarjetas, entera o solo donde cambió, la barrita, y el aviso sin tarjetas | `(SKCanvas, suave, ajuste, copia, filtro, medio, ...)`; `VisibleClipBounds` es `RecorteLienzo.Limites`; el `Save`/`SetClip(area, Intersect)`/`Restore` de cada trozo es `Save` + `ClipRect` (sin suavizar) + `RestoreToCount` del lienzo, y el `SourceCopy` del borrado, la variable `copiaZona` (que vuelve a `SourceOver` antes de hornear, como XP); `Clear` es `SKCanvas.Clear` |
| `DibujaCartaNueva`, `Marca`, `Cinta`, `Renglon`, `Recorta`, `LineaResaltada` | cada tarjeta: el tipo, el nombre con lo tecleado resaltado, el renglón de ayuda, SONANDO y EN COLA, y lo que va en cinta en la elegida | con el estado que usa cada una; `TextoConCinta` es la traducida de `TextoMovilGpu.cs` (lienzo, modo de letra, `Letra`, `FormatoTexto`) |
| `DibujaPortadaCarta`, `Miniatura`, `Escala` | la portada de la tarjeta (o las iniciales sobre el degradado del género) y su miniatura | `TextureBrush(mini, Clamp)` con su `TranslateTransform` es `Degradado.Textura(mini, Envoltura.Transparente, traslación, medio)`; `LinearGradientBrush(cov, c1, c2, 45)` es `Degradado.Lineal` (no escalable, repetido); el `SetClip(gp, Intersect)` en su `Save`, `ClipPath` sin suavizar en un `Save` del lienzo; la miniatura es un `SKBitmap` (`Mapas.Nuevo` + `Imagenes.Pinta` con `BilinealAlta` y medio píxel); las miniaturas guardadas, `SKBitmap` |
| `FirmaCarta.Cover` | (no dibuja) | la miniatura, `SKBitmap`, comparada por identidad como en XP |
| `SubePieza` | (no dibuja: sube la pieza) | la pieza llega como `Rocola.Dibujo2D.Bitmap` (costura, abajo) |

**El pincel y la pluma reusables.** XP pintaba aquí con `sbS` y `pnS`, el `SolidBrush` y la `Pen` de `SearchOverlay` que define
`Form1.Buscador.cs` (aún sin traducir), mutándoles el color (y a la pluma el grosor). La traducción usa dos `SKPaint` propios del archivo,
`pincelS` y `plumaS`, mutados igual. Es lo mismo porque nadie lee lo que dejan puesto: en las 39 líneas de este archivo y en las 6 de
`Form1.Buscador.cs` que los usan, el color (y en la pluma también el grosor) se pone justo antes de cada uso; nunca se cambian la unión, las
puntas ni los guiones (los de fábrica de un `SKPaint`, los mismos que ponía la capa: `Miter`, `Butt`, sin guiones), y `Pixel` les devuelve
lo que les pone para pintar.

**El tipográfico compartido.** `sfMide` y `sfCentro` se copian de `FormatoTexto.TipograficoCompartido`, el MISMO objeto que
`StringFormat.GenericTypographic` del código sin traducir: si `EstadoReposoGpu.cs` ya le puso `Center, Center` y `NoWrap` cuando el buscador
hace sus formatos (una sola vez, en `AseguraFuentes`), las copias salen con eso, como en XP. (Las notas de la tarea decían que las líneas
~512 y ~521 llaman a funciones de `EstiloGpu.cs`; no: son esas dos copias del tipográfico compartido, que `EstiloGpu.cs` también usa. Este
archivo no llama a nada de `EstiloGpu.cs`; lo mismo vio la revisión del grupo «acabado-estilo».)

## El estado del `Graphics`

De la tabla de estado por línea (`Pruebas/TrazaDibujo/estado_por_linea.tsv`): 37 líneas de este archivo, todas en `buscador` salvo 545 y
744-748, que salen en los nueve recorridos (el precalentado del buscador, `PrecalientaBuscadorGpu`, lo pinta al arrancar). Solo en 744, 745
y 748 cambia el estado entre llamadas: el modo de letra (`AntiAlias` ×351 del precalentado y `AntiAliasGridFit` ×78/×89 de
`EnsureUiOverlay` y `EnsureUiPieza`).

| Dónde | Suavizado | Modo de letra | Copia | Filtro | Medio píxel | Matriz, recorte, HDC | De dónde sale |
|---|---|---|---|---|---|---|---|
| `RenderNuevo` y lo que llama | `true` (XP lo pone al empezar) | parámetro | parámetro | parámetro | parámetro | el lienzo; `hdc` para el aviso | lo llama `RenderContent(g, caja, true)` (`Form1.Buscador.cs`) desde `EnsureUiOverlay` (`AntiAlias`, `AntiAliasGridFit`, trasladado en un `Save`) y desde el precalentado (`EnSeco`: `AntiAlias`, `AntiAlias`); el puente le pasa el estado de su `Graphics` y le deja `SmoothingMode = AntiAlias`, como XP |
| `DibujaPiezaVivaNueva` y lo que llama | `true` (XP lo pone al empezar) | parámetro | parámetro | parámetro | parámetro | el lienzo | lo llama `DibujaPiezaViva` desde `EnsureUiPieza` (`AntiAlias`, `AntiAliasGridFit`, mapa nuevo); el puente le deja `SmoothingMode = AntiAlias` |
| `PintaCartasConCambios`, `HorneaCartas` | parámetro (XP no lo toca) | parámetro | parámetro; `false` dentro de cada trozo (el `SourceOver` de XP) | parámetro | parámetro | el lienzo, con el recorte de cada trozo | lo llama `EnsureUiPieza` (`Form1.Video.cs`) con el `Graphics` de la pieza |
| el horneado de `PegaQuieto` (`DibujaQuieto`, `Resplandor`, `LupaNueva`) | el del que llama | (no escribe) | `false` (un `Graphics` nuevo) | (no pinta imágenes) | el del que llama | identidad, sin recorte | XP: `gb.SmoothingMode = g.SmoothingMode; gb.PixelOffsetMode = g.PixelOffsetMode` (y `CompositingQuality`, que no entra en lo que se pinta: la capa no la usa) |
| `Escala` | (no pinta formas) | — | `false` | `BilinealAlta` | `true` | identidad | fijo: el `Graphics.FromImage` de XP con `HighQualityBilinear` y `HighQuality` (tabla, 1457) |
| el medidor (`MideAtajos`) | — | `Suave` (`AntiAlias`) | — | — | — | — | fijo (tabla, 419) |

Lo que los recorridos no pasan (en la tabla no hay ninguna llamada de esas líneas): el atajo elegido (652-653), el borrado y el horneado de
la pieza de las tarjetas por trozos (1014-1023), la barrita (1142-1143), SONANDO y EN COLA (1277-1290), las iniciales sobre el degradado
(1378-1389) y el aviso (`DrawToast`, 707); de los tres avisos sin tarjetas (1152-1154) los recorridos solo pasan «Escribe para buscar, o
elige un atajo de arriba». Se razonó desde el código de quien llama y lo pasa el arnés (abajo) con once estados del `Graphics`.

## Costuras con lo que no está traducido (la regla: el puente va del lado de lo que no está traducido)

Lo que hace falta y no está en `Plataforma/Puente.cs` va en **`Plataforma/Puente.buscador-portadas.cs`** (`Form1.SearchOverlay` es
`partial`; los nombres nuevos llevan «Portadas» para no chocar con los de otro grupo). No se tocó `Puente.cs`, `Dibujo2D.cs`, ningún
`Nativo/*.cs` ni ningún archivo de otro grupo: quien llama a estas funciones no cambia.

| Costura | Dónde | Qué se hace | Se quita cuando |
|---|---|---|---|
| las firmas de XP de `RenderNuevo(Graphics)` y `DibujaPiezaVivaNueva(Graphics, ...)` | las llaman `RenderContent` y `DibujaPiezaViva` (`Form1.Buscador.cs`) | sobrecargas delgadas: le ponen al `Graphics` `SmoothingMode = AntiAlias` (lo que XP le ponía al empezar y le dejaba puesto) y llaman a la traducida con `PuenteDibujo.Lienzo(g)` y su estado (`PuenteDibujo.Estado(g)`: modo de letra, copia, filtro, medio píxel y, para el aviso, la matriz del HDC) | `Form1.Buscador.cs` se traduzca (las llamará con su lienzo y su estado) |
| la firma de XP de `PintaCartasConCambios(Graphics, ...)` | la llama `EnsureUiPieza` (`Form1.Video.cs`) | sobrecarga delgada: el lienzo y el estado (también el suavizado: XP no lo toca); no le cambia nada al `Graphics` (el `Save`/`Restore` de cada trozo es del lienzo) | `Form1.Video.cs` se traduzca |
| `CtrlPad`, `CtrlTxt`, `CtrlDot` (`Form1.Pared.cs`) y `FlechaArriba` (`Form1.Buscador.cs`) pintan con un `Graphics` (y `CtrlTxt` con una `Font`) | `DibujaAyudasNuevas` (la barra de ayudas) | `CtrlPadPortadas`, `CtrlTxtPortadas`, `CtrlDotPortadas`, `FlechaArribaPortadas`: un `Graphics` sobre el MISMO lienzo (su matriz y su recorte) con el estado del que llama (`GraphicsPortadas`: suavizado, modo de letra, copia y medio píxel; el filtro de fábrica y la matriz del HDC de un mapa, la identidad: no pintan imágenes ni texto de GDI) y, para `CtrlTxt`, la `Font` de la `Letra` (`PuenteDibujo.Font`: familia, tamaño, estilo y unidad; se suelta al acabar). No le cambian el estado al `Graphics`: no hay nada que leer después | `Form1.Pared.cs` y `Form1.Buscador.cs` se traduzcan: la llamada pierde el «Portadas» y se borran |
| `DrawToast` (`Form1.Buscador.cs`) pinta con un `Graphics`; su texto es de GDI (`TextRenderer`) | `RenderNuevo` (el aviso del buscador) | `DrawToastPortadas`: como arriba, pero con la matriz del HDC del que llama (GDI pinta con ella, no con la del lienzo: en `EnsureUiOverlay` el lienzo va corrido `searchOverlay.Left, Top` y la del HDC es la identidad, y el texto del aviso no se corre, como en XP) | `Form1.Buscador.cs` se traduzca |
| `RoundedPath` (`Form1.Fondo.cs`) da un `GraphicsPath` | 14 sitios (los resplandores, los fondos y bordes, la barrita, las marcas, el resaltado, la portada) | `using (Rocola.Dibujo2D.GraphicsPath gp = RoundedPath(...))` y se pinta su `SKPath` (`PuenteDibujo.Ruta(gp)`: el mismo, sin copia; su relleno, `EvenOdd`, el de fábrica de GDI+); se suelta como en XP | `Form1.Fondo.cs` dé un `SKPath` |
| `GetCoverYa` (lo pone `Form1.cs` con `PortadaEnMemoria`, de `Form1.Portadas.cs`) da una `Image` | `Miniatura` | `PuenteDibujo.Mapa(img)`: sus puntos, los mismos, sin copia | la caché de portadas dé `SKBitmap` |
| `SubePieza` recibe la pieza como `Bitmap` (la hace `EnsureUiPieza` de `Form1.Video.cs` y la da `D3D9Star.cs` con `GetPieza`) y la pasa a `UpdateOverlayRegion` y `SubeReponiendo` (`RespuestaGpu.cs`) | `GpuPanel.SubePieza` | el tipo escrito entero, `Rocola.Dibujo2D.Bitmap` (como `SubeUi` del grupo «acabado-estilo»); `UpdateOverlayRegion` va por su puente (`Puente.frontera.cs`) | las piezas sean `SKBitmap` (`Form1.Video.cs`, `D3D9Star.cs`, `RespuestaGpu.cs`) |

## Lo que se supuso (y por qué es lo mismo)

- **`pincelS` y `plumaS` en vez de `sbS` y `pnS`** (arriba): nadie lee el color ni el grosor que dejan.
- **El horneado no guarda el estado en su clave**: como en XP, `PegaQuieto` busca por tipo, medida, colores y escala; un horneado hecho con un
  estado (el primero) se pega después con otro. La traducción busca igual y hornea con el estado del primero, como XP.
- **`CompositingQuality`**: XP la copia al horneado; ni la capa (la referencia) ni las funciones nativas la usan, y los que llaman la dejan de
  fábrica.
- **Una portada ya soltada** (`Image` sin puntos): en la referencia `Escala` lanza al leer su medida y `Miniatura` la descarta (`null`); por el
  puente llega `null` y `Miniatura` devuelve `null` antes. El mismo resultado (sin miniatura, se pide otra vez al llegar). Llamada directo con
  una soltada, `Escala` lanza `NullReferenceException` en las dos.

## Comprobaciones

Carpeta de trabajo, fuera del repositorio: `/root/rocola-trabajo/port/buscador-portadas` (las salidas citadas abajo están ahí). Nada en el A4.
Los guiones, en `Pruebas/VistasBuscadorPortadas`.

- **(a) Compila** (`dotnet build -c Release --no-incremental`): **0 errores y los mismos 15 avisos de la base `f2f9dcb`** (las dos listas,
  `archivo(línea,columna): código`, iguales línea por línea: `avisos-base.txt` y `avisos-1.txt`; `SYSLIB0054` ×9, `CA2022` ×2,
  `SYSLIB0050`, `CA2014` y `CS1690` ×2, ninguno de este grupo). Así en cada commit.
- **(b) Banco por función** (`banco.sh`; este grupo no tocó `Nativo/`, `Dibujo2D.cs` ni `Puente.cs`, y el banco no compila
  `Puente.buscador-portadas.cs`, pero se pasó igual): semillas 7 y 11 con las letras de XP falsas y la 5 con DejaVu, 3 000 casos, los
  ejecutores `gdi`, `nativo` y `puente` contra la capa congelada: **0 distintos en los 9 (27 000 casos)**; `puente-objetos`: **407 bien, 0
  mal**.
- **(c) Recorridos del programa entero** (`Pruebas/Recorridos`, `VOLCADO=1 CAPAS=1 HUELLAS=1`, siempre con el candado y la carpeta de
  siempre; el volcado de las texturas, `Nativo/VolcadoTexturas.cs`, ya está en el programa desde el grupo «frontera»; contra las dos
  corridas de la referencia `cmp-ref` y `cmp-ref2`, que no se rehicieron; la máquina compartida, con carga de 4 a 6 en 4 núcleos):
  - los nueve (`cmp/`): todos salen con 0 y **ninguna excepción** en lo que escribió la rocola ni en `rocola_error.txt` (tampoco en las
    corridas de abajo). **Capas de Dibujo2D quietas: 216 de 217** idénticas (la otra, `hojas` c18 capa4, es una hoja de Ctrl+F3, que este
    archivo no pinta, y la misma que vio la revisión de «acabado-estilo»; repetida, 37 de 37). Texturas quietas: **480 de 487** como
    multiconjunto y **375 de 381** distintas. Las que faltaron son de un instante, y se comprobó con más corridas (`cmp2/`, `cmp3/`):
    - `buscador` (otra vez, con los PNG): **las 60 distintas, 78 de 78** como multiconjunto. Una de las dos que faltaban, `e843916c…` (a los
      8,6 s), **es de este archivo: la casilla con la «l» escrita y «6 resultados»** (la pinta `DibujaCajaNueva`); en la primera corrida esa
      imagen no llegó a subirse antes de la «a» (se subió la de «la»), en esta sí, y sale idéntica a la de la referencia. La otra, `8cd9108c…` (a los 26 s), es
      la cabecera con la letra que se desliza (otro archivo), también idéntica;
    - `ayuda` (otra vez): 66 de 66; `hojas` (otra vez): texturas 24 de 24 y capas 37 de 37;
    - `pared2` (otras dos veces, una con los PNG y la referencia congelada otra vez con los PNG, `cmp3ref/`): de las cuatro que faltaron, dos
      salen idénticas en las corridas nuevas (`21d8fe91…`, `1f1712f1…`); las otras dos son de un instante y de otros archivos: `2a697d30…`
      (a los 41,8 s, el primer panel del estado extendido) sale en esta copia como `8fc23430…` (a los 41,6 s), con **149 puntos distintos,
      todos en la caja (230,221)-(251,233)**, la cuenta del reloj del panel; y `8fc23430…` es una de las dos maneras de la propia
      referencia (sale en `cmp-ref/pared` a los 42,3 s). `2f591363…` (la franja con el reloj de la canción a los 62,4-62,8 s) no la dio
      ninguna de las dos corridas nuevas, tampoco la de la referencia (esa vez la referencia no subió nada entre los 62 y los 63,5 s); es la
      que la revisión de «acabado-estilo» midió como el reloj (`0:39 / 2:30` contra `0:01 / 2:30`). En `pared2` este archivo solo pinta en
      el precalentado (la imagen grande del buscador en un mapa que se tira: no se sube);
    - juntando las corridas de esta copia, **379 de las 381 texturas quietas distintas salen idénticas** (las dos de arriba, no). En
      `buscador`, todo lo que se sube al abrirlo (la imagen grande, la tecla elegida, la casilla y la pieza de las tarjetas), al teclear (la
      casilla y las tarjetas) y al moverse por el teclado y por las tarjetas (la tecla y la tarjeta elegidas) sale idéntico.
- **(d) Arnés** (`arnes.sh`: `VistasBuscadorPortadas.csproj` y `BuscadorPortadas.cs`; el programa entero de esta copia, el de la referencia
  congelada y el de la base `f2f9dcb`, con la misma entrada): **3 128 líneas iguales línea por línea a la referencia y a la base, con las
  letras de XP falsas y con DejaVu** (0 distintas en las cuatro comparaciones; entre las dos letras cambian 2 028 líneas, así que las letras
  sí entran). Son 510 imágenes grandes (`RenderContent` con `forGpu`: 330 en 1024×768 con los once estados del `Graphics`, tres colores —uno
  tan oscuro que `Vivo` da el de repuesto— y diez contenidos; 120 en otras seis pantallas, de la escala 0,6 a la 2; 10 con el tipográfico
  compartido cambiado como lo deja `EstadoReposoGpu.cs`; 50 directo), 51 de ellas con el aviso (`DrawToast`); 1 530 imágenes chicas
  (`PiezaViva` y `DibujaPiezaViva`: 306 de la tecla o el atajo elegido, BORRAR incluida; 510 de la casilla, vacía con el cursor y la
  invitación, con lo escrito que cabe y que no, con la cuenta y con un atajo puesto; 204 de la tarjeta elegida, 74 con cintas capturadas; y
  510 de la pieza de las tarjetas) y 1 020 «no hay pieza» (donde XP no la da); en la pieza de las tarjetas, `PintaCartasConCambios` entera y
  después por trozos en el mismo mapa tras un cambio (una portada que llega, la cola que cambia, los datos, las portadas): 54 con zonas
  repintadas, 284 sin nada que repintar y 150 rehechas enteras, con las tarjetas para la tarjeta gráfica (`LlenaCartas`, 357 con tarjetas);
  las tarjetas de artista, disco, canción y servicio, con portada (opaca, con alfa, ancha, alta, de 3×3) o sin ella (las iniciales sobre el
  degradado del género), lo tecleado resaltado, SONANDO, EN COLA y la barrita; y 30 miniaturas (`Escala`, cinco lados, también 1 y 4; con la
  portada soltada, `NullReferenceException` en sus cinco lados en la referencia, en la base y aquí: las 5 excepciones de cada salida). Después de cada llamada con la firma de XP se apunta el estado
  que le queda al `Graphics` (el suavizado que dejan `RenderNuevo` y `DibujaPiezaVivaNueva`, la matriz y el recorte): igual. 2 555 imágenes
  distintas con las letras falsas y 2 551 con DejaVu; en la traducción, 250 llamadas directas a las traducidas (en la referencia esas líneas
  son la llamada de XP otra vez).
- **Controles negativos del arnés** (`control.sh`): quince copias de esta traducción, cada una con un fallo metido a propósito, compiladas con
  el arnés (con `RAPIDO=1`: cuatro estados, un color y dos pantallas más, 519 líneas; la referencia y esta copia sin tocar, **0 distintas**)
  y comparadas con la referencia. **Las quince salen distintas**: la textura de la portada repetida en vez de transparente fuera (64 líneas)
  o sin su medio texel (72), los trozos de las tarjetas horneados con la copia puesta (8), el recorte de cada trozo suavizado (2, solo con
  la traslación de decimales) y el del brillo de las iniciales (56), la miniatura sin el prefiltro (92), el medidor con `AntiAliasGridFit`
  (88), los formatos copiados del normal y no del tipográfico (280), lo quieto horneado siempre suavizado (21, en los estados sin suavizar)
  y pegado sin la copia del lienzo (27, en el de copia), la lupa sin puntas redondas (80), la cinta con el alto del renglón redondeado (14),
  el puente de `RenderNuevo` sin dejarle el suavizado al `Graphics` (30: lo ve el estado que queda), el de las tarjetas siempre suavizado
  (14) y la barra de ayudas con el modo de letra de fábrica (11).
- **(e) Cintas de la tarjeta**: no aplica. Este grupo no cambia el dispositivo ni lo que se le pide (la subida de las piezas, `SubePieza`,
  queda igual, con el mismo `Bitmap`); lo que se sube (la imagen grande y las piezas) son los mismos puntos: lo comprueban (c) y (d), este
  como lo sube la tarjeta (`LockBits` `Format32bppArgb`).
- **(f) NativeAOT** (`dotnet publish -c Release -r linux-x64 -o /root/rocola-trabajo/port/buscador-portadas/aot`): **ningún aviso IL ni de
  AOT** (0 `IL2xxx`/`IL3xxx`) y los mismos 15 avisos del compilador de C# de la base (`avisos-aot.txt` = `avisos-base.txt`). El binario
  nativo (`aot-corre.sh`, con `Pruebas/Tarjeta/guiones/corre.sh`, los datos de los recorridos, el sonido a tiempo real y las teclas y las
  capturas de los recorridos) por `buscador` y `ayuda`: **sale con 0, 11 de 11 y 13 de 13 capturas, 0 líneas con `Exception`** en lo que
  escribió y en `rocola_error.txt` (solo el apunte de siempre del volumen de Windows). En las capturas se ven el teclado con la tecla
  elegida, el atajo «Videos», la casilla con «la» y «4 resultados», las cuatro tarjetas con su portada y lo tecleado resaltado, la tarjeta
  elegida con su aro y la barra de ayudas.
- **(g) Diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py BuscadorPortadasGpu.cs -v`): **215 líneas quitadas y 213 puestas, 0 sin
  marca** (todas llevan `EN LINUX`; las 2 de diferencia son los `using` de XP en una línea, de D1; en la base el mismo guion daba 1 sin
  marca, la del medidor, que ya no está). `git diff f2f9dcb` solo enseña las 213 líneas cambiadas; el archivo sigue en 1 881 líneas, LF,
  ASCII, sin BOM. Fuera de lo permitido no se tocó nada (`Nativo/`, `Dibujo2D.cs`, `Puente.cs`, los demás `Codigo/*.cs`, `AGENTS.md`,
  `LEEME.md`, `ETAPA2.md` y Consola, sin cambios).
- **Con la rama principal de ahora** (`99000e8`, que ya trae el grupo «acabado-estilo»): `git merge-tree` sin conflictos; lo juntado compila
  (0 errores, los mismos 15 avisos) y el arnés de este grupo compilado con lo juntado da **0 distintas de la referencia** (3 128 líneas, con
  las letras de XP falsas). Esa corrida es también la del arnés tal como quedó en la rama (con la opción `RAPIDO` de los controles, añadida
  después de las de (d); sin ella hace lo mismo que antes).

## Riesgos y lo que se vio

- **El `Restore` de la capa y el recorte del que llama** (lo mismo que vio el grupo «acabado-estilo»). En la capa (`Dibujo2D.cs`),
  `Graphics.Restore` además de volver al estado guardado olvida que había un recorte de `SetClip`; en XP, `PintaCartasConCambios` (cada
  trozo) y `DibujaPortadaCarta` (el brillo de las iniciales) hacían `Save`/`Restore` sobre el `Graphics` de `EnsureUiPieza`; traducidas,
  guardan y vuelven el LIENZO y ese `Graphics` no se entera. Solo se notaría si después alguien hiciera un `SetClip` que reemplaza o un
  `ResetClip` sobre ese `Graphics`: `EnsureUiPieza` no lo hace (suelta el `Graphics` al volver). En GDI+ de verdad `Restore` no tiene ese
  efecto. Se va con la capa.
- **`GraphicsPortadas` fija el filtro (`Bilinear`) y, salvo para el aviso, la matriz del HDC (identidad)**: `CtrlPad`, `CtrlTxt`, `CtrlDot` y
  `FlechaArriba` no pintan imágenes ni texto de GDI (solo `FillEllipse`, `DrawEllipse`, `FillPolygon`, `FillPath`, `DrawPath`,
  `MeasureString` y `DrawString`), así que no los miran. Si alguien los cambiara para usarlos, el puente tendría que pasarlos.
- **La matriz del HDC que se le pasa al aviso** es la del `Graphics` de quien llama (`PuenteDibujo.Estado(g).Hdc`). Hoy es siempre la
  identidad (los dos que llaman pintan en mapas de bits hechos con `Graphics.FromImage`), y el arnés solo la pasa así.
- **Los `SKPaint` reusables no se sueltan** (`pincelS` y `plumaS`, uno de cada por buscador: hay uno en toda la rocola), como `sbS` y `pnS` en
  XP.
- La traducción copia la referencia, con sus diferencias (a)-(e) frente a GDI+ de verdad (`PORT_DIBUJO.md`, D0): el texto va con
  `AntiAliasGridFit` (el gris de la referencia) y, en la imagen grande, con la traslación de `EnsureUiOverlay` (e2); lo quieto se pega punto
  por punto (sin escalar: la copia exacta, que no pasa por el filtro ni por (e1)) y la miniatura se achica con `HighQualityBilinear` y medio
  píxel (con el prefiltro de mipmaps de `Imagenes.Pinta`). No hay ClearType ni plumas de menos de un píxel aquí.

## Lo que queda

- Quitar cada puente de `Puente.buscador-portadas.cs` cuando se traduzca el archivo que lo necesita (la tabla de costuras):
  `Form1.Buscador.cs` (las firmas de XP de `RenderNuevo` y `DibujaPiezaVivaNueva`; `FlechaArriba` y `DrawToast`), `Form1.Video.cs` (la de
  `PintaCartasConCambios`, y las piezas como `SKBitmap` con `D3D9Star.cs` y `RespuestaGpu.cs`: entonces `SubePieza` recibe un `SKBitmap`),
  `Form1.Pared.cs` (`CtrlPad`, `CtrlTxt`, `CtrlDot`), `Form1.Fondo.cs` (`RoundedPath`) y la caché de portadas (`GetCoverYa`). Cuando no quede
  ninguna, se borra el archivo entero (y al final, con `Dibujo2D.cs` y `Puente.cs`).
- Las diferencias (a)-(e) de la referencia frente a GDI+ de verdad siguen como estaban (`PORT_DIBUJO.md`); corregirlas, en su función
  nativa, lo decide el dueño.
- Nada de esto se probó en el A4.

## Revisión (revisor independiente, 10-11-2026)

Se revisó el diff `f2f9dcb..port-buscador-portadas` contra el original de XP (`Rocola/BuscadorPortadasGpu.cs`) y contra la capa que imita a
GDI+ (`Dibujo2D.cs`, la referencia de las comparaciones), línea por línea y sin fiarse del informe, y se repitieron las comprobaciones.
**No se encontró ningún fallo en la traducción ni en las costuras: no se cambió código del programa.** Lo único que se tocó es
`Pruebas/VistasBuscadorPortadas/control.sh` (seis controles negativos más, abajo). Salidas en
`/root/rocola-trabajo/port/buscador-portadas/rev`. Nada en el A4.

### Lo que se miró

- **El GDI+ que queda** (grep de `Graphics`, `Bitmap`, `Image`, `Font`, `FontFamily`, los pinceles, `Pen`, `GraphicsPath`, `GraphicsState`,
  `StringFormat`, `TextRenderer`, `ImageAttributes`, `LockBits`, `Region`, `Matrix`, `PixelFormat` y `Dibujo2D`, fuera de los comentarios):
  solo las costuras de la tabla, escritas enteras (las 14 de `Rocola.Dibujo2D.GraphicsPath` de `RoundedPath` y el `Rocola.Dibujo2D.Bitmap`
  de `SubePieza`). Ningún `using Rocola.Dibujo2D`.
- **Línea por línea contra XP** (las 213 líneas del diff): mismos valores, mismo orden y mismas ramas. Lo que es fácil perder está bien:
  - `sbS`/`pnS` → `pincelS`/`plumaS`: en las 6 líneas de `Form1.Buscador.cs` que usan los de XP (928-929, 1051-1052, 1155 y 1160) y en
    todas las de aquí el color (y el grosor de la pluma) se pone justo antes de pintar, y nadie le cambia a `pnS` la unión, las puntas ni los
    guiones; un `SKPaint` nuevo trae lo mismo que pone `Pen.Pon` (esquina en punta, punta plana, sin guiones) y `Pixel` le devuelve lo que
    le cambia para pintar (suavizado, estilo, ancho mínimo, límite de esquinas, mezcla);
  - los formatos: `new FormatoTexto(FormatoTexto.TipograficoCompartido)` es la misma copia que hace `new StringFormat(StringFormat.GenericTypographic)`
    en la capa (también `SinMargen`); `|=` en las dos (`sfMide`, `sfCentro`), ninguna con `=`; `sfIzq` y `sfLinea` copian `sfMide` ya con
    sus banderas, en el orden de XP;
  - `MeasureString(t, f, ancho, sf)` mide en `(ancho, 999999)`; `GetHeight(g)` es `AltoRenglon` (la capa no mira el `Graphics`); el medidor
    de XP solo aporta su modo de letra (`AntiAlias` = `AjusteLetra.Suave`, los mismos valores del enumerado);
  - `DrawImage(q.B, caja, 0, 0, ancho, alto, Pixel)` es `Imagenes.Pinta` con el filtro, el medio píxel y la copia del lienzo y la envoltura
    transparente de fábrica; el horneado toma del que llama el suavizado y el medio píxel y nada más (un `Graphics` nuevo: sin copia);
  - `TextureBrush(mini, Clamp)` + `TranslateTransform` es `Degradado.Textura(mini, Envoltura.Transparente, traslación, medio)`: el cuarto
    argumento que pasa la capa es `!centros`, es decir `medio`; `LinearGradientBrush(cov, c1, c2, 45f)` es `Degradado.Lineal` no escalable y
    repetido (la sobrecarga de la capa); un `SKPaint` solo con `Shader` trae el color negro opaco que `Pixel` pone de todas formas;
  - `Save`/`SetClip(…, Intersect)`/`Restore`: en la capa el `SetClip` dentro de un `Save` es un `ClipRect`/`ClipPath` sin suavizar en el
    lienzo y `Restore` es `RestoreToCount`: lo mismo que queda escrito, también anidados (el del brillo de las iniciales dentro del de cada
    trozo); el `SourceCopy` del borrado de cada trozo no llega a `HorneaCartas` (XP lo vuelve a `SourceOver` antes) y fuera de los trozos
    vale la copia del que llama;
  - la lupa: `StartCap = EndCap = Round` es `SKStrokeCap.Round` (lo que hace `Pen.Pon`); `DrawEllipse(pn, x, y, w, h)` y `DrawLine` con los
    mismos números;
  - `Miniatura`: `GetCoverYa` puede dar `null` (→ `null`, como XP) o una imagen ya soltada (XP: `Escala` lanza y el `catch` da `null`; aquí
    `PuenteDibujo.Mapa` da `null` y se devuelve `null` antes): el mismo resultado. `FirmaCarta.Cover` se sigue comparando por identidad
    con lo que da `Miniatura` (la misma caché, las mismas claves);
  - quién suelta qué: los horneados, las miniaturas, los shaders y los `SKPaint` de un uso se sueltan donde XP soltaba sus `Bitmap` y sus
    pinceles; el `Graphics` de cada puente no es dueño del lienzo (no lo suelta) y la `Font` de `CtrlTxtPortadas` suelta solo los `SKFont` de
    su propia `Letra` (`Letra.Dispose` no toca la cara, que es compartida): `fnAyuda` sigue viva.
- **El estado del `Graphics`** contra `estado_por_linea.tsv` (37 filas, 30 con estado, todas `m=I b=I r=no d=mapa`): solo 744, 745 y 748
  varían (el modo de letra) y va como parámetro; el filtro, la copia y el medio píxel también van como parámetro aunque en la tabla no
  cambien; 419 (el medidor, `t=AntiAlias`) y 1457 (`HighQualityBilinear`, `HighQuality`) fijos. Lo que la tabla no tiene sale del
  `Graphics` de `EnsureUiPieza` (`Form1.Video.cs` 1485-1486: `AntiAlias`, `AntiAliasGridFit`) por el puente.
- **Las costuras**: `CtrlPad`, `CtrlTxt`, `CtrlDot` (`Form1.Pared.cs` 253-279), `FlechaArriba` y `DrawToast` (`Form1.Buscador.cs` 396 y
  1044) solo rellenan, trazan, miden y escriben: ninguna le cambia un modo, la matriz ni el recorte al `Graphics`, así que no hay estado que
  devolver; `RenderNuevo` y `DibujaPiezaVivaNueva` sí le dejaban `SmoothingMode = AntiAlias` y el puente se lo deja; la matriz del HDC del
  aviso es la del `Graphics` del que llama (`baseMatriz`, que un `Save`/`TranslateTransform` de quien llama no cambia). `SubePieza` no
  cambia más que el nombre entero del tipo.
- **Finales de línea y lo permitido**: 1 881 líneas como la base, LF, ASCII, sin BOM; `git diff f2f9dcb` solo toca
  `BuscadorPortadasGpu.cs` (213 líneas, todas con `EN LINUX`), el puente nuevo, la carpeta de pruebas y este documento. Autor y pie de los
  tres commits, como se pedía.

### Comprobaciones repetidas (10-11-2026, la máquina con carga de 5 a 9 en 4 núcleos)

- **Compila** (`dotnet build -c Release --no-incremental`): 0 errores y 15 avisos, la misma lista que `avisos-base.txt`.
- **Diff contra XP** (`diff-xp.py BuscadorPortadasGpu.cs -v`): 215 quitadas, 213 puestas, 0 sin marca.
- **Arnés** (`arnes.sh`, con la referencia congelada compilada otra vez): 3 128 líneas, **0 distintas con las letras de XP falsas y 0 con
  DejaVu** (250 llamadas directas, las 5 excepciones de las miniaturas de la portada soltada en los dos lados; entre las dos letras cambian
  2 028 líneas).
- **Controles negativos propios** (añadidos a `control.sh`; `SOLO=revision` los pasa solos; con `RAPIDO=1`, 519 líneas, esta copia sin tocar
  0 distintas): el brillo de las iniciales con el medio píxel al revés (56 líneas), `sfMide` como el tipográfico compartido mismo y no una
  copia (3: lo ve el estado del tipográfico), el recorte de un trozo de las tarjetas sin quitar (8), el aviso con otra matriz del HDC (8),
  el puente de `DibujaPiezaVivaNueva` sin dejarle el suavizado al `Graphics` (60) y las palabras de la barra de ayudas sin la negrita de su
  `Letra` (70). **Los seis salen distintos.** Uno más NO lo ve el arnés: `sfCentro.Banderas = SinPartir` en vez de `|=` (se pierden
  `FitBlackBox`, `LineLimit` y `NoClip` del tipográfico) da 0 líneas distintas de 3 128, y tampoco con tres pantallas diminutas añadidas a
  mano (320×200, 200×150 y 120×90: 0 de 699). No es un hueco del arnés sino que aquí no se nota: lo que escribe con `sfCentro` (las teclas,
  los atajos, las marcas y las iniciales) va siempre en cajas más altas que su renglón y el texto se recorta antes por el ancho, así que ni
  `LineLimit` ni el recorte a la caja cambian un punto. Que es `|=` como en XP queda comprobado solo por lectura.
- **Recorridos** (`buscador` y `ayuda`, tres veces, con el candado, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra `cmp-ref`/`cmp-ref2`): salen con 0,
  0 líneas con `Exception`. **Capas de Dibujo2D quietas: 48 de 48 las tres veces.** Texturas quietas: 130, 141 y **144 de 144**; juntando
  las tres, las 116 distintas (60 de `buscador` y 56 de `ayuda`) salen idénticas. Las tres veces la pantalla se paró varias veces de 520 a 790 ms
  (lo apunta `rocola_error.txt`, por la carga) y en las dos primeras faltaron estados intermedios: en `buscador`, la casilla con la «l» y «6
  resultados» (`e843916c…`, no llegó a subirse antes de la «a»; la de «la», `a0b3f4a7…`, idéntica las tres veces; la tercera vez sale
  también la de la «l», idéntica); en `ayuda`, los globos del asistente y la guía del rojo (de otros archivos). Lo que sale aquí y no en
  ninguna de las dos de la referencia: en `buscador`, los cuadros de la cabecera que se desliza de 25,7 a 26,7 s (la referencia saca 16 y
  18 distintos entre sí) y la guía del rojo de los 34,4-35 s (`1f1e16c5…`, `4d4dd133…`, las mismas que la referencia sube en `ayuda` y
  `pared`); en `ayuda`, `173044c2…`, `0042efca…`, `745690fa…`, `328dd8d9…` y `9302a6ea…`, que están en las corridas de otros grupos (y la primera
  también en una de la referencia, `atraccion/ref-hoy`): variantes de un instante, ninguna de este archivo (en `ayuda` el buscador no se abre).
  `hojas` una vez: texturas 24 de 24, capas 36 de 37; la que falta es la misma `c18` capa 4 (`c45e76a2…`, la que dan las corridas con carga
  de `junta/pares`: «faltan N archivos» del repaso, `PORT_JUNTAS.md`). Con las huellas que guardó el traductor (`cmp/`, los nueve) el
  guion da lo que dice arriba: 480 de 487 texturas y 216 de 217 capas.
- **NativeAOT** (`dotnet publish -c Release -r linux-x64 -o …/rev/aot`): 0 avisos `IL2xxx`/`IL3xxx` y los mismos 15 del compilador; el
  binario nativo (`aot-corre.sh`) por `buscador` y `ayuda`: sale con 0, 11 de 11 y 13 de 13 capturas, 0 excepciones; en la captura de los
  14,5 s se ven el teclado, el atajo «Videos», la casilla con «la» y «4 resultados», las cuatro tarjetas con su portada y lo tecleado
  resaltado, la elegida con su aro y la barra de ayudas.
- **Con la rama principal de ahora** (`02374fc`, con atracción y la hoja de ventanas ya juntadas): `git merge-tree` sin conflictos; lo
  juntado compila (0 errores, 15 avisos) y el arnés con `RAPIDO=1` da las 519 líneas iguales a la referencia.
- **No se repitieron**: el banco por función (este grupo no toca `Nativo/`, `Dibujo2D.cs` ni `Puente.cs`, y el banco no compila
  `Puente.buscador-portadas.cs` ni el archivo traducido: su lista de `Compile` es fija) ni las cintas (la subida no cambia); los otros siete
  recorridos se miraron solo por las huellas guardadas.

### Lo que queda (de la revisión)

- Nada que arreglar. Siguen valiendo los riesgos de arriba (el `Restore` de la capa, el filtro fijo de `GraphicsPortadas`, la matriz del HDC
  del aviso solo probada con la identidad en el programa; el control de la revisión la cambia y el arnés lo ve).
- Nada de esto se probó en el A4.
