# Port nativo de XP: el grupo «video-form1» (la interfaz que se sube a la tarjeta, la barra de ayuda y el precalentado)

Tanda 3 del dibujo del port nativo (decisión y aclaración del dueño del 8-11-2026: «que tradujeras todo el código para que trabajara
nativamente en Linux», «ningún layer de traducción»). Receta, regla del puente y diferencias (a)-(e): `PORT_DIBUJO.md`. Rama `port-video-form1`
(base `ae8b50b`). Nada de esto se probó en el A4 (no se puede desde aquí): todo es fuera de pantalla (llvmpipe) en la máquina de pruebas.

## Qué se tradujo

Solo las líneas de GDI+ de los tres archivos del grupo (las de ventanas, video y sonido son de otras pistas y no se tocaron). Cada línea
cambiada lleva su `// EN LINUX: ...`, cada archivo conserva su número de líneas y sus finales (Form1.Video.cs 1 975 LF; Form1.cs 2 785 con
2 741 CRLF; Form1.Sonido.cs 3 443 con 3 423 CRLF), y ninguno de los tres ve ya los tipos que imitan a GDI+ (`using Rocola.Dibujo2D` fuera: lo
que queda de la capa va escrito entero, en las costuras de abajo).

| Archivo | Qué | Cómo |
|---|---|---|
| `Form1.Video.cs` | `EnsureUiOverlay` (la imagen grande de la interfaz: buscador, interior del disco, atracción sin escaparate, navegación) | un `SKCanvas` sobre los puntos de `uiOverlay` (`PuenteDibujo.Mapa`); el estado del `Graphics` en variables (`suave` = AntiAlias, `ajuste` = AntiAliasGridFit, `copia`, `medio` y `filtro` los de fábrica: esta imagen no los cambia); `Save`/`TranslateTransform`/`Restore` del lienzo; el `SetClip` de la lista en un `Save` con `ClipRect` sin suavizar (no hay otro recorte: reemplazar es recortar); los textos de la atracción con `TextoGdiPlus.Escribe`, dos `Letra` y un `FormatoTexto` centrado |
| | `EnsureUiBotOverlay` (la franja de abajo) y `EnsureUiPieza` (las piezas vivas del buscador) | igual; llaman DIRECTO a las ya traducidas (`EmpiezaRecuadro`, `FranjaLejos`, `PintaTituloCompacto`, `PintaArtistaCompacto`, `PintaSiguienteCompacto`, `PintaCartasConCambios`) |
| | `DrawCtrlOverlay` | `(SKCanvas, suave, ajuste, copia, filtro, medio, Control)`: `Save`+`Translate` del lienzo; la barra de ayuda y la casilla de AYUDA (traducidas) directo; las vistas aún sin traducir por su firma vieja con `PuenteDibujo.GraphicsVideoForm1(lienzo, estado)` |
| | `DrawLabelOverlay` (las tres sobrecargas) | `(SKCanvas, ajuste, copia, medio, Label, ...)`: `FormatoTexto` y `SKPaint` reusables por hilo (`[ThreadStatic]`, como XP), `SinPartir` con `=` (reemplaza las banderas, como `FormatFlags = NoWrap`), `PuntosPorLetra`, las alineaciones de la etiqueta y `TextoGdiPlus.Escribe` con la `Letra` de la etiqueta |
| | `EnSeco` (el precalentado) | `Mapas.Nuevo` + `SKCanvas`; `dibuja` recibe el lienzo y el estado (AntiAlias, AntiAlias) |
| | `PositionMejorada`, `PartirNow` | `TextRenderer.MeasureText` → `TextoGdi.Mide` (sin caja ni banderas: Bottom, como WinForms) con la `Letra` de la etiqueta; `PartirNow` (nadie la llama, tampoco en XP) recibe una `Letra` |
| | la letra de `lblGpu`, las portadas del cubo, el mini-cubo | la letra por `PuenteDibujo.Font(Letra)`; las `Image`/`Bitmap` que solo pasan, con su tipo escrito entero |
| `Form1.cs` | `HelpBar` (la barra de ayuda) | `RenderContent(SKCanvas, ajuste, copia, medio, forGpu)`; su letra cacheada es una `Letra`; su ancho (`Medida`) con `TextoGdiPlus.Mide` y el modo de letra de fábrica de un `Graphics` (SystemDefault), sin el mapa de 1×1 que solo daba el `Graphics`; `GlowAround`, `CtrlPad`, `CtrlTxt` y `CtrlDot` (Form1.Pared.cs) por el puente |
| | las letras de `lblAttract`, `lblConfig`, `pastGenero`; `Font.Height` de las etiquetas de abajo | `PuenteDibujo.Font(Letra.EnPuntos(...))`; `PuenteDibujo.Letra(f).AltoEntero` |
| `Form1.Sonido.cs` | (no dibuja) | solo los tipos de las portadas que pasan, escritos enteros |

## Las costuras y los puentes

Todas en `Plataforma/Puente.video-form1.cs` salvo donde se dice; se quitan como dice la última columna.

| Costura | Por qué | Cómo | Se quita cuando |
|---|---|---|---|
| `uiOverlay`, `uiOverlayOtro`, `uiBotOverlay` y `uiPieza[]` siguen siendo `Rocola.Dibujo2D.Bitmap` | los leen y suben archivos sin traducir: `GetUi`, `GetUiBot`, `GetPieza` de `D3D9Star.cs`, `SubeUi` (BuscadorLigeroGpu.cs), `SubeUiBot` (FranjaLigeraGpu.cs), `SubePieza` (BuscadorPortadasGpu.cs), `winSnap` (`Clone`, Form1.Biblioteca.cs), y `EnsureSongPiece` (AcabadoGpu.cs) pone piezas con `Entrega` | se hacen con `PuenteDibujo.Entrega(Mapas.Nuevo(w, h), Format32bppPArgb)` (un `Bitmap` DUEÑO de los puntos, con el formato de XP: lo mismo que `new Bitmap(w, h, PArgb)`, como `EnsureSongPiece`) y se pinta en sus puntos (`new SKCanvas(PuenteDibujo.Mapa(...))`). Los puntos son `Bgra8888` premultiplicado (`Mapas.Nuevo`), lo que pide la tarjeta. El orden de XP de soltar y usar no cambia | `D3D9Star.cs` y esos lectores reciban `SKBitmap` |
| `PuenteDibujo.GraphicsVideoForm1(lienzo, suave, ajuste, copia, filtro, medio)` | lo que la traducida le pide a lo no traducido que pinta con un `Graphics`: las vistas de `DrawCtrlOverlay` (HeaderView, LetterBar, ProgressView, GeneroView, SongListView, SongPagerBar de Form1.Vistas.cs; `PastillaView.RenderContentGpu` de DiscoVivoGpu.cs; BuscaView de Form1.Buscador.cs), `RenderContent` y `DibujaPiezaViva` del buscador | un `Graphics` sobre el MISMO lienzo (con su matriz y su recorte) con el estado del que llama y la matriz del HDC de un mapa de bits (la identidad: el texto de GDI de las vistas no ve la traslación, como en XP). Lo que lo llamado le cambie al estado se queda en ese `Graphics`: en XP, quien llama lo deshacía con su `Restore` (`DrawCtrlOverlay`, el buscador de `EnsureUiOverlay`) o tiraba el `Graphics` (`EnsureUiPieza`). Ninguna vista llamada reemplaza el recorte (solo `SetClip` con `Intersect`, y `ClipBounds` sale del lienzo): un `Graphics` nuevo recorta igual | se traduzcan Form1.Vistas.cs (grupos vistas-a y vistas-b), DiscoVivoGpu.cs (`RenderContentGpu`) y Form1.Buscador.cs (grupo buscador): el coordinador cambia cada llamada a la traducida |
| `GlowAroundVideoForm1`, `CtrlPadVideoForm1`, `CtrlDotVideoForm1`, `CtrlTxtVideoForm1` | la barra de ayuda traducida llama a las formas de Form1.Pared.cs (grupo pared) | un `Graphics` de `GraphicsVideoForm1` con el estado de la barra (filtro de fábrica, HDC identidad: no pintan imágenes ni usan TextRenderer) y, `CtrlTxt`, la `Font` de esa `Letra` (se suelta al acabar). No le cambian el estado | se traduzca Form1.Pared.cs: la llamada pierde el «VideoForm1» |
| la firma de XP `HelpBar.RenderContent(Graphics, bool)` (`HelpBar` pasa a `partial`) | su `OnPaint` de WinForms (el dibujo por procesador) | el lienzo y el estado del `Graphics` a la traducida; después le deja el suavizado en AntiAlias (lo que XP le pone y no devuelve) | se traduzca el `OnPaint` (pista de ventanas, el dibujo por procesador) |
| la firma de XP `EnSeco(Control, Action<Graphics>)` | el precalentado de PrecargaPaginaGpu.cs y Form1.Portadas.cs | la traducida con un delegado que le da a lo que llama un `Graphics` de `GraphicsVideoForm1` con el estado de `EnSeco` | se traduzcan esos dos |
| `OnPaintBackground` (Form1.cs) conserva su `Graphics`, escrito entero | solo pasa el `Graphics` de WinForms al cielo (`Starfield.DrawCached`, Form1.Fondo.cs, grupo fondo): traducirlo sería ir y volver por el puente sin pintar nada (lo mismo que `RenderContentGpu` en pantalla-viva) | — | se traduzca `Starfield` (fondo) y el `OnPaint` (ventanas) |
| la forma del aviso sin tarjeta (`lblToast.Region = new Region(RoundRect(...))`, Form1.cs:2270) | `Control.Region` de WinForms con el `GraphicsPath` de `RoundRect` (Form1.Ajustes.cs), los dos sin traducir | los tipos de la capa, escritos enteros | se traduzcan las ventanas (la etiqueta del aviso) |
| las letras de las etiquetas de WinForms (`lblGpu`, `lblAttract`, `lblConfig`, `pastGenero`) | las etiquetas son de WinForms y piden una `Font` | `PuenteDibujo.Font(Letra.EnPuntos(...))`: la misma familia, tamaño, unidad y estilo que el `new Font` de XP | se traduzcan las ventanas |
| `PastillaView.Ancho(..., this.Font.FontFamily, ...)`, `AnchoToast(text, lblToast.Font)`, `Bold(...)` | pasan la letra de WinForms a funciones de otros archivos sin traducir (Form1.Vistas.cs, Form1.Ajustes.cs, Form1.Fondo.cs) sin nombrar ningún tipo | sin cambio | se traduzcan esos archivos |

Costuras de otros grupos que dependían de este y se QUITARON (se llama directo):

- `EstiloGpu.cs:101-102` (acabado-estilo): `DrawLabelOverlay(gg, lblMejorada)` y `DrawCtrlOverlay(gg, pastGenero)` → con el lienzo y el estado
  que llevaba `gg` (`GraphicsAcabado`: el filtro de fábrica, Bilinear, y HDC identidad). `gg` se queda para `PintaArtistaCompacto(gg)` (costura
  entre reposo y acabado-estilo, no de este grupo).
- `TextoMovilGpu.cs:176` (reposo): `DrawLabelOverlayReposo` → `DrawLabelOverlay` directo; se borró `DrawLabelOverlayReposo` de
  `Puente.reposo.cs`.
- `Puente.reposo.cs`: la firma de XP de `EmpiezaRecuadro` (solo la llamaba `EnsureUiBotOverlay`). Ojo: aquel puente ponía el recorte también
  por el `Graphics`; ahora queda solo el de la traducida (el mismo rectángulo, en un `Save`): nada de lo que pinta después la franja lo
  reemplaza ni lo quita, así que pinta los mismos píxeles.
- `Puente.acabado-estilo.cs`: las firmas de XP de `DrawGuiaRojo`, `FranjaLejos`, `PintaTituloCompacto`, `PintaSiguienteCompacto`,
  `DrawSongPagerGpu`, `RenderTransparentGpuTint` y `RenderOpaqueGpu` (solo las llamaba Form1.Video.cs).
- `Puente.buscador-portadas.cs`: la firma de XP de `PintaCartasConCambios`.

## Comprobaciones (11-11-2026, con la máquina compartida: carga de 3 a 5 en 4 núcleos)

- **(a) Compila** (`dotnet build -c Release`): 0 errores y los MISMOS 15 avisos de la base `ae8b50b` (los mismos archivos y líneas:
  `Thread.VolatileRead`, `FormatterServices`, `Control.bounds`, dos `Stream.Read`, un `stackalloc`; ninguno nuevo).
- **(b) Banco por función** (aunque solo se tocaron puentes de grupo, que el banco no compila): semillas 7 y 11 con las letras de XP falsas y
  5 con DejaVu, 3 000 casos, ejecutores gdi, nativo y puente contra el congelado: **0 distintos en los 9**; `puente-objetos`: **407 bien, 0 mal**.
- **(c) Recorridos** (los nueve, con el candado y la carpeta de siempre, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra `cmp-ref` y `cmp-ref2`):
  9 de 9 salen con 0, ninguna excepción (en `rocola_error.txt` solo apuntes «(sin excepcion)», como la referencia). **Texturas quietas 480 de
  487; capas de Dibujo2D quietas 215 de 217.** Lo que falta, con su causa:
  - capas: `ctrlf3` c40 capa 4 es la hoja de Ctrl+F2, que solo cambió de nombre (`capa4-hoja-...` en vez de `capa4-dibujo2d-...`) y tiene la
    MISMA huella (31c56564160785fd) que en las dos de la referencia; `hojas` c18 capa 4 es la hoja de Ctrl+F3 con lo que lleva medido el
    repaso de la biblioteca (las prioridades de hilo de XP puestas de verdad: `PORT_JUNTAS.md`). Ninguna de las dos es de estos archivos.
  - texturas: 7, todas de la franja de abajo (1024×128) o de la imagen de arriba (1024×256) en un instante que depende del reloj
    (`pared2` a los 41,9, 62,3, 62,4 y 69,2 s; `buscador` a los 8,6 y 25,9 s; `ayuda` 1). Repetidos `pared2`, `buscador` y `ayuda` (`cmp2`):
    `ayuda` 66 de 66, `buscador` la de 8,6 s sale en la segunda corrida; las cinco que no salen en ninguna de las dos de esta rama son
    contenidos que SÍ sacan otras corridas de programas traducidos de los otros grupos, ya juntados (las cinco huellas están en 12 a 70
    corridas de `asistente`, `frontera`, `reposo`, `suena`, `vistas-a`... en `/root/rocola-trabajo/port`): el reloj de la canción y el
    contador que rueda en ese instante, como en D2 y D4 de `PORT_DIBUJO.md` (los otros grupos sacaban de 92 a 95 de las 98 quietas de `pared2`;
    aquí 94 y 93). Todo lo demás de la franja y de la imagen de arriba sale idéntico (cientos de versiones en los nueve recorridos).
- **(c2) Lo que los nueve no pasan, con la referencia:**
  - la atracción SIN el escaparate (`Pruebas/VistasVideoForm1/atraccion.sh`, `escaparate=0`: la rama de `EnsureUiOverlay` que escribe
    «N canciones» y «toca un boton para elegir»), esta rama una vez y la referencia dos: **texturas quietas 29 de 29** (con la imagen de la
    interfaz de 1024×1024 que sin el escaparate se sube), **capas 10 de 10**;
  - dentro del disco SIN el vidrio en piezas (`Pruebas/VistasAcabadoEstilo/disco.sh`: `glass_capas=0`, `canciones_cristal=0`,
    `canciones_transparentes=0`, las ramas de `RenderTransparentGpuTint`, el velo y `RenderOpaqueGpu` con el `SetClip` de la lista), contra las
    dos corridas de la referencia del grupo acabado-estilo (`/root/rocola-trabajo/port/acabado-estilo/disco-ref` y `disco-ref2`): **texturas
    quietas 100 de 100, capas 60 de 60**.
- **(d) Arnés directo** (`Pruebas/VistasVideoForm1`: `arnes.sh` compila el programa entero de esta copia y el de la referencia congelada con
  otra entrada, `VideoForm1.cs`): 722 líneas IGUALES a la referencia con las letras de XP falsas y con DejaVu (128 llamadas directas a lo
  traducido): 224 etiquetas (`DrawLabelOverlay`: las 9 alineaciones, 5 textos con acentos, eñe, `&`, comillas y vacío, 3 letras, la caja de la
  etiqueta, con color, angosta y sin ancho, en 8 estados del `Graphics` del que llama: suavizado, los modos de letra, copia, medio píxel,
  traslación entera y con decimales, escala, giro y recorte), 168 vistas por `DrawCtrlOverlay` (la barra de ayuda normal y justa,
  ProgressView normal y neón, GeneroView, LetterBar y PastillaView, en 3 medidas y los 8 estados), 192 de la barra de ayuda (por la firma de
  XP de su `OnPaint`, con y sin tarjeta, y DIRECTO como la llama `DrawCtrlOverlay`) y su ancho (`Ancho` y `AnchoJusta` de 6 a 63 px), 40 del
  halo del botón pulsado por el puente (5 niveles fijos: el de la barra depende del reloj), 60 de `PartirNow`, 5 de `EnSeco` (el estado que
  le da a lo que llama: AntiAlias, AntiAlias, lo demás de fábrica, identidad, la medida de un texto) y 32 del texto de GDI (`TextRenderer`)
  por el `Graphics` del puente (la matriz del HDC: los recorridos no pasan por ningún `TextRenderer.DrawText` del código de XP). Después de cada
  llamada se apunta el estado que le queda al `Graphics` del que llama (en XP lo deja el `Restore`; en la traducción no se toca): igual.
  Controles negativos (`control.sh`, un fallo metido a propósito en una copia): las etiquetas cortando por palabra → 73 líneas distintas; la
  barra de ayuda sin suavizar → 240; el puente con la matriz del lienzo como HDC en vez de la identidad → 32.
- **(e) Cintas de la tarjeta:** no se corrieron: este grupo no cambia nada de la tarjeta ni de cómo se sube (`D3D9Star.cs`, `Upload` y los
  `Sube*` siguen igual y reciben el mismo `Bitmap` de la capa); lo que se sube sí sale de código traducido, y eso lo comparan las texturas
  volcadas de (c) y (c2).
- **(f) NativeAOT** (`dotnet publish -c Release -r linux-x64`): **0 avisos IL/AOT** (solo los 15 del compilador de siempre). El binario nativo
  por pared, buscador, atracción y ayuda (`Pruebas/VistasVideoForm1/aot-corre.sh`, con las teclas y capturas de los recorridos): sale con 0,
  12 de 12, 11 de 11, 5 de 5 y 13 de 13 capturas, **0 excepciones** en lo que escribió y en `rocola_error.txt`.
- **(g) `diff-xp.py -v`** (`Form1.Video.cs Form1.cs Form1.Sonido.cs EstiloGpu.cs TextoMovilGpu.cs`): las líneas sin marca que da son las de
  antes de este grupo (comentarios del video, `Equipo.ProcesadorMhz`, los `Read(Span)` de NAudio 3, una línea en blanco); contra la base
  `ae8b50b`, las líneas que cambió este grupo (116, 38, 3, 3 y 1) llevan TODAS su `EN LINUX`.

## Lo que queda

- Las costuras de la tabla de arriba, cada una cuando se traduzca lo suyo: las vistas (vistas-a, vistas-b, buscador: `DrawCtrlOverlay`,
  `RenderContent` y `DibujaPiezaViva` llamarán directo), las formas de la barra de ayuda (pared), el cielo de `OnPaintBackground` (fondo),
  `D3D9Star.cs` y los demás lectores de `uiOverlay`, `uiBotOverlay` y `uiPieza` (que pasen a `SKBitmap`, sin `Entrega`), y las ventanas
  (`OnPaint` de la barra, el aviso sin tarjeta, las letras de las etiquetas).
- La franja de lejos (`EstiloGpu.cs`) todavía hace un `Graphics` (`gg`) para `PintaArtistaCompacto(gg)`, ya traducida: costura entre reposo y
  acabado-estilo que puede llamar directo (no es de este grupo).
- Nada de esto se probó en el A4.
