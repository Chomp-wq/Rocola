# Port nativo de XP: la casilla de BUSCAR y el buscador de siempre, de GDI+ a SkiaSharp directo

Grupo «buscador» de la tanda 3 del port nativo (el dibujo con GDI+ de lo que queda: las vistas, la pared, el fondo, el buscador, las portadas
y la interfaz que se sube a la tarjeta), 11-11-2026: `Codigo/Form1.Buscador.cs` (`BuscaView`, la casilla de BUSCAR de arriba, y
`SearchOverlay`, el buscador de siempre: la casilla de lo escrito, el teclado, los resultados con sus tarjetas y marcas, el cubo de la vista
previa, el aviso y las imágenes chicas para la tarjeta). Decisiones del dueño en `AGENTS.md` (8-11-2026: «que tradujeras todo el código para
que trabajara nativamente en Linux», «ningún layer de traducción») y la receta en `PORT_DIBUJO.md` (D2-D4, `Pruebas/BancoDibujo/EjecutaNativo.cs`).
Base: `ae8b50b`; rama `port-buscador`. Aquí va lo que se tradujo, el estado del `Graphics` de cada función, cada costura con lo que todavía
no está traducido (con su razón y cuándo se quita), las comprobaciones con sus números y lo que queda. Nada de esto se probó en el A4.

## Qué se tradujo

`Form1.Buscador.cs` ya no ve los tipos que imitan a GDI+ (`using Rocola.Dibujo2D` fuera: `using System.Drawing; using Rocola.Nativo; using
SkiaSharp;` en la misma línea 10 de antes); los de la capa solo quedan escritos enteros en las costuras de abajo (`RoundedPath`, las portadas
de la caché y el `Graphics` de WinForms de `OnPaint`). Todo su dibujo va con SkiaSharp (`SKCanvas`, `SKPaint`, `SKShader`, `SKBitmap`) y las
funciones nativas (`Nativo/`), línea por línea como en XP: 235 líneas cambiadas, cada una con su `// EN LINUX: ...`; el archivo conserva sus
1 602 líneas (donde XP usaba una línea para el mapa de 1×1 del medidor, para la primera firma de `RenderContent` o para el `ImageAttributes`,
queda un comentario `EN LINUX`), su BOM y sus finales de línea (1 541 CRLF y el resto LF, como antes; `git diff --stat`: 235 y 235). La lógica
de XP no se tocó: mismos valores, mismo orden, mismas ramas, las mismas cachés (`kbCache`, `resCache` con sus claves y el retoque de solo las
tarjetas que cambian, `bmpTarj`, `hornoGen`, `hornoMarca`, `blendBmp`).

| Lo de XP | Cómo quedó |
|---|---|
| `new Font(FontFamily.GenericSansSerif, pt, estilo)` (las 14 letras) | `Rocola.Nativo.Letra.EnPuntos(Letras.SinRemates, pt, negrita, cursiva)` (Microsoft Sans Serif, en puntos; `Form1` tiene un método `Letra`, por eso el nombre entero) |
| `SolidBrush`, `Pen` (también `sbS`, `pnS` y `marcaB`, los reusables a los que se les cambia el color y el grosor) | `SKPaint` con `Color` (y `StrokeWidth`); la unión, las puntas y los guiones son los de fábrica de un `SKPaint`, los mismos que ponía la capa (`Miter`, `Butt`, sin guiones); `Pixel` pone el mínimo de un píxel, el límite de esquinas y el suavizado al pintar |
| `LinearGradientBrush(caja, a, b, Vertical)` | `Degradado.Lineal(caja, a, b, Degradado.Vertical, true)` (el modo es un ángulo escalable, repetido fuera como el `WrapMode.Tile` de fábrica) |
| `FillPath`, `DrawPath`, `DrawEllipse`, `DrawLine`, `FillRectangle` | `Pixel.Rellena`, `Traza`, `TrazaOvalo`, `Linea`, `Rellena` con el suavizado, el medio píxel y la copia del lienzo |
| `DrawImageUnscaled`, `DrawImage(img, caja)`, `DrawImage` con `ImageAttributes` y `ColorMatrix` (`Matrix33 = f`) | `Imagenes.Pinta` con el filtro, el medio píxel y la copia del lienzo; la matriz es `MatrizColor.Identidad()` con `[3][3] = f` y la envoltura de fábrica de `ImageAttributes` (`Transparente`, el `Clamp` de GDI+) |
| `new Bitmap(w, h, Format32bppPArgb)` + `Graphics.FromImage` | `Mapas.Nuevo` (premultiplicado, transparente) + `new SKCanvas`; el estado de un `Graphics` nuevo en variables (`SystemDefault`, `Bilinear`, sin copia ni medio píxel, la matriz del HDC la identidad) |
| `MeasureString(s, f)` (la casilla de BUSCAR y la ayuda de la vista previa) | `TextoGdiPlus.Mide(s, letra, ajuste, (0,0), null)` con el modo de letra que tenga el lienzo en ese momento |
| los 23 `TextRenderer.DrawText`/`MeasureText` (el texto de GDI) | `TextoGdi.Escribe(lienzo, hdc, ...)`/`TextoGdi.Mide`; las banderas con su nombre (`Left` `Izquierda`, `HorizontalCenter` `Centrado`, `VerticalCenter` `EnMedio`, `EndEllipsis` `PuntosAlFinal`, `NoPadding` `SinMargen`, `NoClipping` `SinRecorte`; los mismos valores); GDI pinta con la matriz del HDC del que llama (no con la traslación que le pone el código) y sin suavizar solo con los modos `SingleBitPerPixel*` del `Graphics` (`ajuste == SinSuavizar \|\| ajuste == SinSuavizarAjustada`, lo mismo que mira `TextRenderer.DrawText` de la capa). `TextoGdi.Mide` no necesita el lienzo (el medidor de XP solo daba el `Graphics`) |
| `CompositingMode = SourceCopy` / `SourceOver` (el borrado del hueco de una tarjeta que cambia en `resCache`) | la variable `copiaC` |
| `InterpolationMode = Bilinear` (`DrawPreviewCube`, `BlendInto`) | la variable `filtro` (en `DrawPreviewCube` por `ref`: XP se lo deja puesto al `Graphics` del que llama) |
| `SmoothingMode`/`TextRenderingHint` guardados y devueltos (`DrawPreviewInfo`) | las variables `suave` y `ajuste`, que vuelven como en XP |

No hacía falta ninguna función nativa nueva: todo lo que usa el archivo ya estaba en `Nativo/` (y probado por el banco). `TextoGdi` tenía
todas las banderas que usa este archivo.

## El estado del `Graphics`

De la tabla de estado por línea (`Pruebas/TrazaDibujo/estado_por_linea.tsv`): 25 líneas de este archivo, todas de la casilla de BUSCAR
(213-302), de las letras (537-546) y de `FlechaArriba` (401-402, que pinta la barra de ayudas del buscador con portadas); salen en los nueve
recorridos. En 252-302 cambian entre llamadas el modo de letra, la matriz, la del HDC y el recorte (`ClearTypeGridFit` del `OnPaint` de la
capa y `AntiAliasGridFit` de `DrawCtrlOverlay`, trasladado); en 401-402, el modo de letra. Lo demás (todo `SearchOverlay` sin portadas: con
tarjeta el buscador es el de portadas) no lo pasan los recorridos y se razonó desde quien llama y se comprobó con el arnés (abajo).

| Dónde | Suavizado | Modo de letra | Copia | Filtro | Medio píxel | Matriz, recorte, HDC | De dónde sale |
|---|---|---|---|---|---|---|---|
| `BuscaView.RenderContent` y lo que llama (`Lupa`, `Flecha`) | `true` (XP lo pone al empezar) | parámetro | parámetro | (no pinta imágenes) | parámetro | el lienzo | `OnPaint` (`ClearTypeGridFit`) y `DrawCtrlOverlay` (`Form1.Video.cs`, `AntiAliasGridFit`, trasladado en un `Save`): el puente le pasa el estado de su `Graphics` y le deja `SmoothingMode = AntiAlias` si XP llega a ponerlo |
| `AnchoJusto` (la medida con su letra) | — | `Suave` (el `AntiAlias` que XP le pone al `Graphics` de 1×1) | — | — | — | — | fijo (tabla, 216-218) |
| `SearchOverlay.RenderContent` y lo que llama (`DibujaCajaEscrito`, `DrawKeys`, `DrawOneKey`, `DrawResults`, `DrawOneResultCard`, `MarcaRes`, `DrawToast`, `DrawPreviewCube`, `DrawPreviewInfo`, `FlechaArriba`) | `true` (XP lo pone después de `RenderNuevo`) | parámetro | parámetro | parámetro (y `Bilinear` desde el cubo, que se le queda al que llama) | parámetro | el lienzo; `hdc` para el texto de GDI | `OnPaint`, `EnsureUiOverlay` (`Form1.Video.cs`) y los precalentados (`Form1.Portadas.cs`, `PrecargaPaginaGpu.cs`): el puente le deja al `Graphics` el suavizado y el filtro que le deja XP |
| `DibujaPiezaViva` | parámetro por `ref` (XP pone `AntiAlias` en tres caminos y se lo deja) | parámetro | parámetro | parámetro | parámetro | el lienzo; `hdc` | `EnsureUiPieza` (`Form1.Video.cs`); con el buscador con portadas va a `DibujaPiezaVivaNueva` y con la tarjeta aparte a `DrawExtraGpu` (`AcabadoGpu.cs`), los dos ya traducidos, directo |
| el teclado guardado (`kbCache`) y la cabecera y las tarjetas guardadas (`resCache`) | `true` | `DelSistema` | `false` (el hueco de una tarjeta que cambia se borra con la copia, como el `SourceCopy` de XP) | `Bilineal` | `false` | identidad, sin recorte; HDC identidad | un `Graphics.FromImage` nuevo |
| los horneados (`HorneaTarjeta`, `HorneaMarcaRes`, `HorneaGeneroRes`) y la mezcla de portadas (`BlendInto`) | `true` (la mezcla no pinta formas) | (no escriben) | `false` | `Bilineal` | `false` | identidad | un `Graphics.FromImage` nuevo |

Lo que pintan las funciones de otros archivos que el traducido llama (por el puente, abajo) no le cambia el estado al `Graphics` sin
devolverlo: `CtrlTxt`, `CtrlDot`, `CtrlTri`, `CtrlPad`, `CtrlDirLeft`, `GlowAround` y `Cube.Draw` (`Form1.Pared.cs`) solo pintan con su pincel
y su pluma; `Starfield.DrawReactive` (`Form1.Fondo.cs`) pone `SourceCopy` para el cielo y devuelve el que había (`BlitSkyAndStars`), y lo
demás que llama (`PintaLuna`, `BlitEstrellas`, `DrawBeatFlare`, `DrawShoots`) no toca el estado. Por eso esos puentes van por valor.

## Las costuras y los puentes (`Plataforma/Puente.buscador.cs`)

La regla de `PORT_DIBUJO.md`: el puente va del lado de lo que no está traducido, y los que llaman no cambian. Nada de esto queda.

| Costura | Quién | Cómo | Se quita cuando |
|---|---|---|---|
| las firmas de XP de `BuscaView.RenderContent(Graphics)` y `(Graphics, bool)` | `OnPaint` (WinForms, la capa de ventanas) y `DrawCtrlOverlay` (`Form1.Video.cs`) | sobrecargas delgadas en `Puente.buscador.cs`: `PuenteDibujo.Lienzo(g)` y `Estado(g)`; después le dejan `SmoothingMode = AntiAlias` (lo que XP le pone tras su primer `return` y no devuelve) | se traduzcan `Form1.Video.cs` (grupo video-form1) y el dibujo de `OnPaint` (ventanas) |
| la firma de XP de `SearchOverlay.RenderContent(Graphics, Rectangle, bool)` | `OnPaint`, `EnsureUiOverlay` (`Form1.Video.cs`), los precalentados (`Form1.Portadas.cs`, `PrecargaPaginaGpu.cs`) | igual; después le deja `SmoothingMode = AntiAlias` (si pasa del primer `return`) y el filtro que devuelve la traducida (`Bilinear` si pintó el cubo) | se traduzcan esos tres y el `OnPaint` |
| la firma de XP de `SearchOverlay.DibujaPiezaViva(Graphics, int, int, int)` | `EnsureUiPieza` (`Form1.Video.cs`) | igual; la traducida devuelve el suavizado por `ref`, que no distingue `AntiAlias` de `HighQuality`, así que el puente mira antes (dibujar no lo cambia) si XP llega a una de sus tres líneas `SmoothingMode = AntiAlias` y entonces se lo pone | se traduzca `Form1.Video.cs` |
| `CtrlTxt`, `CtrlDot`, `CtrlTri`, `CtrlPad`, `GlowAround`, `CtrlDirLeft` pintan con un `Graphics` (y `CtrlTxt` con una `Font`) | la casilla de BUSCAR, la barra de ayudas del buscador, las teclas, `FlechaArriba`, la ayuda de la vista previa | `CtrlTxtBuscador` y los demás: un `Graphics` sobre el MISMO lienzo (su matriz y su recorte) con el estado del que llama (`GraphicsBuscador`; el filtro de fábrica y la matriz del HDC identidad: no pintan imágenes ni texto de GDI), y la `Font` de la `Letra` (`PuenteDibujo.Font`, se suelta al acabar) | se traduzca `Form1.Pared.cs` (grupo pared): la llamada pierde el «Buscador» y se borran |
| `Cube.Draw` pinta con un `Graphics` y una `Image` | `DrawPreviewCube` (el cubo del dibujo por procesador) | `CubeDrawBuscador`, con el filtro y la matriz del HDC del que llama; la portada mezclada (`BlendInto`, un `SKBitmap`) va prestada (`PuenteDibujo.Bitmap`: el MISMO `Bitmap` cada vez para el mismo mapa, como el `blendBmp` de XP, y no suelta los puntos) | se traduzca `Form1.Pared.cs` |
| `Starfield.DrawReactive` pinta con un `Graphics` | el cielo del buscador por procesador | `DrawReactiveBuscador`, con todo el estado del que llama (mira el recorte con `ClipBounds` y pinta mapas) | se traduzca `Form1.Fondo.cs` (grupo fondo) |
| `RoundedPath` devuelve un `GraphicsPath` | las 10 formas redondeadas | el tipo escrito entero (`Rocola.Dibujo2D.GraphicsPath`) en su `using` (se suelta como en XP) y su `SKPath` por `PuenteDibujo.Ruta` (el mismo, sin copia; relleno par-impar como el `Alternate` de fábrica) | se traduzca `Form1.Fondo.cs` |
| las portadas son `Image` de la caché (`GetCover`, `GetCoverYa`, `GetPreviewCube`, `artistCovers`, `Portada`, `ComputePreview`, `EnsureArtistCovers`) | el cubo y la mezcla | el tipo escrito entero (`Rocola.Dibujo2D.Image`, 17 sitios: `RetienePortada` de `Form1.Biblioteca.cs` las compara por identidad); para pintarlas, sus puntos (`PuenteDibujo.Mapa`) | se traduzcan `Form1.Portadas.cs`, `Form1.Biblioteca.cs` y quien sube el cubo a la tarjeta (`Form1.Video.cs`, `Form1.Sonido.cs`) |
| `e.Graphics.TextRenderingHint = ClearTypeGridFit` en `OnPaint` | WinForms | el tipo escrito entero (`Rocola.Dibujo2D.TextRenderingHint`) | se traduzca el `OnPaint` (ventanas) |

**Costuras de otros grupos que se quitaron** (las que decían «se quita cuando se traduzca `Form1.Buscador.cs`»):

- `AcabadoGpu.cs`: `DrawExtraGpu(Rocola.Dibujo2D.Graphics, ...)` pasa a `(SKCanvas, suave, ajuste, copia, filtro, medio, hdc, ...)` y llama
  directo a `DrawOneResultCard` y `DrawPreviewInfo` (3 líneas).
- `BuscadorPortadasGpu.cs`: `DrawToastPortadas` y `FlechaArribaPortadas` pasan a `DrawToast` y `FlechaArriba`, directo (2 líneas).
- `Plataforma/Puente.buscador-portadas.cs`: fuera las firmas de XP de `RenderNuevo(Graphics)` y `DibujaPiezaVivaNueva(Graphics)` (ahora las
  llaman `RenderContent` y `DibujaPiezaViva` directo, con su lienzo y su estado; XP les ponía `AntiAlias` al empezar: el traducido pone
  `suave = true` antes de `DibujaPiezaVivaNueva`, y `RenderNuevo` lo pone por su cuenta) y `FlechaArribaPortadas`/`DrawToastPortadas`. Quedan
  `PintaCartasConCambios` (lo llama `Form1.Video.cs`) y `CtrlPad/Dot/TxtPortadas` (`Form1.Pared.cs`).

Ningún otro archivo cambió. No se tocaron `Puente.cs`, `Dibujo2D.cs` ni ningún `Nativo/*.cs`.

## Comprobaciones

- **Compila** (`dotnet build -c Release`, también `--no-incremental`): 0 errores y los mismos 15 avisos de la base (mismos archivos, líneas
  y códigos: `Thread.VolatileRead`, `FormatterServices`, `Control.bounds`, dos `Stream.Read` y un `stackalloc`).
- **diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py -v`): `Form1.Buscador.cs` 238 quitadas y 235 puestas, 0 sin marca;
  `AcabadoGpu.cs` 99/97, 0 sin marca; `BuscadorPortadasGpu.cs` 215/213, 0 sin marca. `git diff --stat` del archivo: 235 y 235 (los finales
  de línea, como estaban).
- **Arnés** (`Pruebas/VistasBuscador`, `arnes.sh`): el programa entero de esta copia y el de la referencia congelada (compilada con
  `-p:Fuente=/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp` desde una copia del proyecto) con otra entrada (`Buscador.cs`) que pinta:
  la casilla de BUSCAR en siete altos (uno que no llega a pintar), cerrada (late, dos fases) y abierta, de tres maneras (`OnPaint`, para la
  tarjeta y sin tarjeta), y su medida (`AnchoJusto`); el buscador de siempre a 1024×768 con 17 contenidos (vacío; lo escrito con resultados,
  lo tecleado resaltado, EN COLA, SONANDO, géneros y discos; más resultados de los que caben; el foco en una canción, un disco, un artista con
  varias portadas y la mezcla de dos —dos fases—, sin portada y con una portada alta; BORRAR y ESPACIO elegidos; lo escrito que no cabe; el
  aviso; la tarjeta aparte con un artista y con la de servicio), por procesador y para la tarjeta, con la caja entera y una parcial, las
  cuatro imágenes chicas (`PiezaViva`/`DibujaPiezaViva`) y la cola que cambia (solo se retocan las tarjetas que cambian); seis pantallas más
  (de 59×400 y 70×70 a 1920×1080); el cielo (`Starfield`, con un recorte parcial y la capa de estrellas fija para que no dependa del reloj); y
  todo con once estados del `Graphics` (sin suavizar, `HighQuality`, copia, medio píxel `Half` y `HighQuality`, los seis modos de letra, cinco
  filtros, traslación entera y con decimales, escala, giro y recorte). Cada línea: la huella de los puntos tal cual y como los sube la tarjeta
  y lo que le queda al `Graphics` después (suavizado, modo de letra, copia, filtro, medio píxel, matriz y recorte). En la traducción, 371
  llamadas más van DIRECTO a las traducidas (`RenderContent`, `DibujaPiezaViva` y la casilla con su lienzo y su estado; el filtro y el
  suavizado que devuelven se apuntan como los del `Graphics` en la referencia). Resultado: **4 642 líneas, IGUALES a la referencia** con las
  letras de XP falsas y con DejaVu, 0 excepciones.
- **Controles negativos del arnés** (`control.sh`, `RAPIDO=1`, 1 574 líneas, la copia sin tocar 0 distintas): los 14 fallos metidos a
  propósito salen todos: AnchoJusto con `AntiAliasGridFit` 4 líneas; la casilla con el medio píxel al revés 264; el teclado guardado sin la
  copia del lienzo 23; el degradado de las teclas horizontal 215 (el primer intento, vertical no escalable, sale igual porque con 90° da lo
  mismo: `Degradado.Extremos`); el hueco de una tarjeta que cambia sin la copia 5; el fondo de la tarjeta con el medio píxel al revés 10; el
  nombre del resultado suavizado con un `Graphics` de un bit 19; la mezcla de portadas sin la matriz de alfa 20; la cabecera «Resultados»
  centrada 320; las teclas guardadas con otra matriz del HDC 152; las ayudas de la vista previa sin `ClearTypeGridFit` 134; el puente de
  `RenderContent` sin devolver el filtro del cubo 10; el puente de `DibujaPiezaViva` sin dejar el suavizado 96; el de la casilla dejándolo
  aunque XP salga antes 48.
- **Recorridos** (`Pruebas/Recorridos`, los nueve, con el cerrojo y la carpeta de siempre, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra `cmp-ref`
  y `cmp-ref2`; salidas en `/root/rocola-trabajo/port/buscador/cmp` y `cmp2`): los nueve salen con 0, sin excepciones. **Capas de Dibujo2D
  quietas 215 de 217**; las dos que faltan son las ya apuntadas en `PORT_JUNTAS.md`: la hoja de Ctrl+F3 de `hojas` a los 18 s (lo que lleva
  medido el repaso de la biblioteca, que va más despacio con la máquina cargada: las prioridades de hilo de XP puestas de verdad) y la de
  Ctrl+F2 de `ctrlf3` a los 40 s, que solo cambió de nombre (`capa4-hoja-...`, de la pista de ventanas). **Texturas quietas 469 de 487** en
  la primera corrida (`atraccion`, `botones`, `ctrlf3`, `hojas`, `pared` y `video` todas; `ayuda` 53 de 66, `buscador` 77 de 78, `pared2` 94
  de 98). Otra corrida de `ayuda`, `buscador` y `pared2` (`cmp2`, con la máquina a carga 4-6) da `ayuda` 65 de 66, y juntando las dos,
  **481 de 487**. Las seis que faltan son de la hora y ya las apuntaron otros grupos, que tampoco las sacan siempre: `buscador` `e843916c…`
  (la casilla con la «l», entre dos letras tecleadas, a los 8,5 s: sale en 3 de las 5 corridas de la referencia que hay en la carpeta de
  trabajo y en 16 de 49 de las traducciones y bases); `pared2` `2a697d30…` (el primer panel del estado extendido con el reloj en otro
  segundo: 3 de 6 de la referencia, 9 de 56 de las demás), `21d8fe91…` y `2f591363…` (la tanda de SALTAR y la franja a los 62 s: 4 y 3 de 6
  de la referencia) y `1f1712f1…` (a los 69 s: 27 de 56 de las demás); `ayuda` `c9e4fc67…` (la referencia la sube dos veces y la traducción
  una, ya apuntada en `PORT_frontera.md` y `PORT_asistente.md`). Ninguna es del buscador de siempre (con tarjeta el buscador es el de
  portadas) ni de la casilla de BUSCAR, que sale en la cabecera de todos los recorridos y cuyas texturas salen todas.
- **Banco por función** (`Pruebas/BancoDibujo`, con el guion de `VistasBuscadorPortadas/banco.sh`; no se tocó `Nativo/` ni `Puente.cs`, se
  corrió igual): semillas 7 y 11 con las letras de XP falsas y 5 con DejaVu, 3 000 casos, ejecutores gdi, nativo y puente contra el
  congelado: **0 distintos en los 9**; `puente-objetos`: 407 bien, 0 mal.
- **NativeAOT** (`dotnet publish -c Release -r linux-x64 -o /root/rocola-trabajo/port/buscador/aot`): **0 avisos IL/AOT** (los 15 avisos de
  C# de siempre). El binario nativo, fuera de pantalla con los datos y las teclas de los recorridos (`VistasBuscadorPortadas/aot-corre.sh`
  con `BUSCADOR=/root/rocola-trabajo/port/buscador`): `buscador` salida 0, 11 de 11 capturas; `ayuda` salida 0, 13 de 13 capturas; **0
  excepciones** en lo que escribió y en `rocola_error.txt`. (Pinta la casilla de BUSCAR y la barra de ayudas del buscador con la flecha de
  `FlechaArriba`, vistas en la captura de los 14,5 s.)
- **Cintas de la tarjeta**: no hacen falta (no cambia nada de lo que se sube a la tarjeta: las imágenes las siguen haciendo los mismos
  archivos, y las que pinta este, con los mismos puntos, como dicen el arnés y los recorridos).

## Riesgos y lo que queda

- **Una portada de la caché ya soltada en la mezcla del cubo** (`BlendInto`, solo el dibujo por procesador): la capa de la referencia no pinta
  nada si el `Image` ya no tiene puntos (`Graphics.Imagen`: `img.Sk == null`); GDI+ de verdad lanzaría `ArgumentException`; la traducción
  (`PuenteDibujo.Mapa` da `null`) lanza `NullReferenceException` al leer su medida. Pasa solo si la caché suelta una portada que el cubo aún
  enseña (`RetienePortada` lo evita). La de la izquierda ya llevaba `if (a != null)` en XP, que no mira si está soltada. Se deja como XP.
- **El halo de los botones del buscador por procesador** (`GlowAroundBuscador`): el arnés no lo pasa (su brillo depende de los milisegundos
  desde la pulsación, `InstrFlash`); el puente es el mismo que los otros cinco de `Form1.Pared.cs`, que sí pasa.
- **El cielo** (`DrawReactiveBuscador`) se comprobó solo con un recorte parcial (sin las fugaces, que van con el reloj) y la capa de
  estrellas fija.
- Lo que queda de este archivo para después: el `OnPaint` y lo de WinForms (`Control`, `Invalidate`, `Width`/`Height`...) es de la pista de
  ventanas; las costuras de arriba se van cuando se traduzcan `Form1.Pared.cs`, `Form1.Fondo.cs`, `Form1.Video.cs`, `Form1.Portadas.cs`,
  `Form1.Biblioteca.cs` y `PrecargaPaginaGpu.cs`.
- La traducción línea por línea se aplicó con un guion que comprueba cada línea de XP antes de cambiarla (`/root/rocola-trabajo/port/buscador/guion/tradu.py`, fuera del repositorio).
- Nada de esto se probó en el A4.

## Revisión (revisor independiente, 11-11-2026)

Se revisó el diff `ae8b50b..port-buscador` contra el original de XP (`Rocola/Form1.Buscador.cs`, y `Rocola/Form1.Pared.cs` y
`Rocola/Form1.Fondo.cs` para lo que va por el puente) y contra la capa (`Dibujo2D.cs`), línea por línea y sin fiarse del informe, y se
repitieron las comprobaciones. **La traducción de `Form1.Buscador.cs` y su puente están bien: no se cambió ninguna línea del programa que
pinte.** Se encontró una costura de otro grupo que quedó colgada y un hueco del arnés. Salidas en `/root/rocola-trabajo/port/buscador/rev-*`.
Nada en el A4.

### Lo que se miró

- **El GDI+ que queda** (grep de `Graphics`, `Bitmap`, `Image`, `Font`, pinceles, `Pen`, `GraphicsPath`, `StringFormat`, `TextRenderer`,
  `ImageAttributes`, `LockBits` y `Dibujo2D` fuera de los comentarios): solo las costuras de la tabla, con el tipo escrito entero
  (`RoundedPath`, las portadas de la caché, el `TextRenderingHint` del `OnPaint`); ningún `using Rocola.Dibujo2D`.
- **Línea por línea** (las 235 cambiadas, emparejadas con las de la base por número de línea): mismos valores y mismo orden. Las 14 letras
  (`GenericSansSerif` → `Letras.SinRemates`, negrita, cursiva y normal como en XP); los `TextRenderer` (16 `DrawText` y 7 `MeasureText`)
  con las banderas equivalentes, la matriz del HDC del que llama y «sin suavizar» solo con `SingleBitPerPixel*` (como `TextRenderer` de la
  capa); los mapas horneados (`HorneaTarjeta`, `HorneaMarcaRes`, `HorneaGeneroRes`, el teclado y los resultados guardados, `blendBmp`) con
  el estado de un `Graphics` nuevo (sin medio píxel, sin copia, `SystemDefault` → `DelSistema`, `Bilinear`); el hueco transparente con
  `SourceCopy` y vuelta a `SourceOver`; `DrawImageUnscaled` con el filtro, el medio píxel y la copia del que llama; la mezcla del cubo con
  `Matrix33 = f` y la envoltura `Clamp` (= `Transparente`) que trae un `ImageAttributes` nuevo; `DrawPreviewInfo` cambia el suavizado y la
  letra solo en sus variables y las devuelve (a quien llama no le cambia nada, como XP con `oldsm`/`oldth`); `DrawPreviewCube` devuelve el
  `Bilinear` por `ref` y `RenderContent` lo pasa a lo que pinta después (teclas, resultados).
- **El puente** (`Puente.buscador.cs`): las firmas de XP dejan al `Graphics` justo lo que XP deja (el `AntiAlias` cuando XP pasa la
  primera salida —`30×12` en la casilla, `60×60` en el buscador, donde `RenderNuevo` también lo pone—, y el filtro del cubo);
  `DibujaPiezaViva` mira antes de pintar los tres caminos en que XP pone el suavizado (con `PiezaTecla = 0` y `PiezaEscrito = 1`, la
  condición `cual < 2` es la de XP). Lo que pinta `Form1.Pared.cs` (`CtrlTxt`, `CtrlDot`, `CtrlTri`, `CtrlPad`, `GlowAround`,
  `CtrlDirLeft`, `Cube.Draw`) no le cambia el estado al `Graphics` (se leyó en XP); `Starfield.DrawReactive` pone la copia en
  `BlitSkyAndStars` y la devuelve. La portada mezclada llega a `Cube.Draw` como el mismo `Bitmap` prestado cada vez (`blendBmp` no se
  suelta nunca, ni en XP), y nadie la compara por identidad.
- **Hilos**: todo lo de este archivo corre en el hilo de la pantalla (también los precalentados), como en XP; los `SKPaint` reusables
  (`sbS`, `pnS`, `marcaB`) son de la vista. `BuscadorPortadasGpu.cs` sigue con sus `pincelS`/`plumaS` (su revisión ya explicó por qué es lo
  mismo que los `sbS`/`pnS` de XP); ahora que estos son `SKPaint` se podrían compartir, pero no hace falta: nadie lee lo que dejan puesto.
- **Finales de línea y número de líneas**: `Form1.Buscador.cs` 1 602 líneas, 1 541 CRLF, igual que la base; los demás archivos tocados,
  LF como estaban, mismo número de líneas en los de `Codigo/`.

### Lo que se arregló

1. **La costura de `CintaVistaPrevia` (grupo reposo) quedó colgada.** `PORT_reposo.md` la lista con «se quita cuando se traduzca cada uno
   de los que las llaman», y el único que la llamaba con un `Graphics` era `Form1.Buscador.cs`, que ya la llama directo. Se quitó la firma
   de XP de `Plataforma/Puente.reposo.cs` (y su mención en la cabecera), se puso al día el comentario de `TextoMovilGpu.cs:141` y el arnés
   `Pruebas/VistasReposo/Reposo.cs`, que la llamaba por su firma de XP: donde ya no está (la traducción) llama a la traducida con el estado
   del `Graphics`, como hacía el puente; en la referencia, la de XP. También se pusieron al día los comentarios de `RenderNuevo`,
   `DibujaAyudasNuevas` y `DibujaPiezaVivaNueva` en `BuscadorPortadasGpu.cs`, que aún decían que `Form1.Buscador.cs` estaba sin traducir y
   que las llamaba un puente que ya no existe. Solo comentarios y un puente sin llamadas: el programa pinta igual.
2. **El halo de los símbolos por procesador (`GlowAroundBuscador`) no lo pasaba el arnés** (su brillo depende de los milisegundos desde la
   pulsación, y en `RenderContent` casi siempre sale apagado). Se añadió al arnés la parte 4: el halo con niveles fijos (0,01, que no pinta;
   0,35 y 1), dos colores y los once estados del `Graphics`, sobre el fondo con la franja; en la traducción por el puente con el lienzo y el
   estado del `Graphics`, en la referencia la `GlowAround` de XP. Sale igual (abajo).

### Comprobaciones repetidas

- **Compila** (`dotnet build -c Release --no-incremental`, con lo de arriba): 0 errores y los mismos 15 avisos (archivo, línea y código,
  comparados con los de la base: `rev-avisos.txt`).
- **diff contra XP** (`diff-xp.py -v`): `Form1.Buscador.cs` 238/235, `AcabadoGpu.cs` 99/97, `BuscadorPortadasGpu.cs` 215/213,
  `TextoMovilGpu.cs` 49/47; **0 sin marca** en los cuatro.
- **Arnés del buscador** (`arnes.sh`, con la parte del halo): **4 708 líneas (66 más), IGUALES a la referencia** con las letras de XP falsas
  y con DejaVu, 0 excepciones, 437 llamadas directas. Las líneas del halo cambian con el nivel y el color (no es un dibujo vacío) y la de
  0,01 no pinta, como en XP.
- **Arnés de reposo** (`Pruebas/VistasReposo/arnes.sh`, por el puente quitado): 1 790 líneas, **IGUALES** con las dos letras, 768
  llamadas directas.
- **Arnés de buscador-portadas** (`Pruebas/VistasBuscadorPortadas/arnes.sh`, porque `RenderNuevo`, `DibujaPiezaVivaNueva`, `DrawToast` y
  `FlechaArriba` ya no pasan por su puente): 3 127 líneas, **IGUALES** con las dos letras, 250 llamadas directas (las 5
  excepciones de las miniaturas de la portada soltada, en los dos lados, como en su revisión).
- **Recorridos** `buscador` y `ayuda` (con el cerrojo y la carpeta de siempre, `VOLCADO=1 CAPAS=1 HUELLAS=1`, `rev-cmp`): los dos salen con
  0 y sin excepciones; **capas de Dibujo2D quietas 48 de 48**; texturas quietas `buscador` 76 de 78 y `ayuda` 53 de 66. Lo que falta es lo
  de la máquina cargada, no de la traducción: en `buscador` faltan `8cd9108c…` (25,9 s) y `e843916c…` (8,6 s), que faltan igual en las
  corridas de otros 13 grupos que no tocan este archivo (`asistente`, `atraccion`, `pared`, `portadas`, `video-form1`...); en `ayuda` faltan
  las mismas 13 (de 24 a 31 s y `c9e4fc67…` a los 7,1 s) que en la primera corrida del autor, y que faltan igual en `asistente/cmp-b2`,
  `atraccion/cmp` y `vistas-a/cmp2` (sin este archivo); la segunda corrida del autor (`cmp2`) da 65 de 66.
- **Banco por función**: no se repitió (ni la traducción ni la revisión tocan `Nativo/` ni `Puente.cs`). **Cintas**: no hacen falta (nada
  de lo que se sube a la tarjeta cambia). **NativeAOT** (`dotnet publish -c Release -r linux-x64`, a
  `/root/rocola-trabajo/port/buscador/aot`): **0 avisos IL/AOT** (los 15 de C# de siempre); el binario nativo por `buscador` y `ayuda`
  (`VistasBuscadorPortadas/aot-corre.sh`): salida 0, 11 de 11 y 13 de 13 capturas, 0 excepciones en lo que escribió y en `rocola_error.txt`.

### Lo que queda

- El riesgo de `BlendInto` con una portada ya soltada (arriba) sigue: la capa no pinta, GDI+ de verdad lanzaría `ArgumentException` y la
  traducción lanza `NullReferenceException`; los dos últimos son una excepción en el mismo sitio, y `RetienePortada` lo evita.
- El cielo por procesador sigue comprobado solo con un recorte parcial y la capa de estrellas fija.
