# Port nativo de XP: las portadas (miniaturas, previas, teselas y la subida con repuesto), de GDI+ a SkiaSharp directo

Grupo «portadas» de la tanda 3 del port nativo (el dibujo con GDI+ de lo que queda), 11-11-2026: `Form1.Portadas.cs`, `DibujoLigeroGpu.cs`
(`TeselasVisibles`, `TrabajoTeselas` y su parte de `GpuPanel`), `RespuestaGpu.cs` y las líneas de GDI+ de `Form1.Biblioteca.cs` y de
`CaratulasAdaptativas.cs` (las de ventanas, el diálogo del historial de carátulas, son de la pista de ventanas y no se tocaron). Decisiones del
dueño en `AGENTS.md` (8-11-2026: «que tradujeras todo el código para que trabajara nativamente en Linux», «ningún layer de traducción») y la
receta en `PORT_DIBUJO.md` (D2-D4, `Pruebas/BancoDibujo/EjecutaNativo.cs`). Base: `ae8b50b`; rama `port-portadas`. Nada de esto se probó en
el A4.

## Qué se tradujo

Cada línea que cambia lleva su `// EN LINUX: ...`; los archivos conservan su número de líneas y sus finales de línea (CRLF en
`Form1.Portadas.cs` y `Form1.Biblioteca.cs`, línea por línea; LF en los otros). La lógica de XP no se tocó: mismos valores, mismo orden,
mismas ramas. `Form1.Portadas.cs`, `Form1.Biblioteca.cs`, `DibujoLigeroGpu.cs` y `RespuestaGpu.cs` ya no ven los tipos que imitan a GDI+
(`using Rocola.Dibujo2D` fuera, y el alias `Encoder` de `Form1.Biblioteca.cs`); los de la capa solo quedan escritos enteros en las costuras.
`CaratulasAdaptativas.cs` sigue viendo la capa SOLO por sus líneas de ventanas (`Font` en `BuildCoverHistory`, `ShowCoverHistory` y
`TarjetaCargaCaratulas`), que la pista de ventanas ya cambió en `port-ventanas2` sin tocar la línea 3: quitándole el `using`, el compilador
solo se queja en esas líneas (comprobado). Al juntar las dos ramas, la línea 3 puede perder `using Rocola.Dibujo2D;`.

| Archivo | Función | En XP | Ahora |
|---|---|---|---|
| `Form1.Portadas.cs` | `DecodeCoverCore` (la miniatura de 256×256) | `Image.FromStream`; `cimg as Bitmap`; `Clone(zona, PArgb)`; `new Bitmap(256, 256, PArgb)`; `Graphics.FromImage` con `HighQualityBilinear` y `PixelOffsetMode.Half`; `ImageAttributes` con `WrapMode.TileFlipXY` y `DrawImage` (la opaca con «Carátulas en JPEG») o `DrawImage(src, 0, 0, 256, 256)`; `IsAlphaPixelFormat`, `Indexed`, `ImageFlags.HasAlpha`; `(Bitmap)thumb.Clone()` | `Imagenes.Lee` (los puntos y el `FormatoImagen` que diría GDI+); el `SKBitmap` mismo; `Mapas.Copia`; `Mapas.Nuevo`; un `SKCanvas` con el filtro (`Filtro.BilinealAlta`) y el medio píxel (`true`) en variables; la envoltura en una variable (`Envoltura.Espejo`) e `Imagenes.Pinta` (sin atributos: `Transparente`, el `Clamp` de GDI+); `fuenteOpaca` por el `FormatoImagen` (solo `Argb32` trae alfa y marca `HasAlpha`; `Paleta8` es la de paleta); `Mapas.Copia` entera. Devuelve `SKBitmap` y la copia por guardar sale `out SKBitmap` |
| `Form1.Portadas.cs` | `DecodeCover`, `DecodeCoverWithIdentity`, `DecodeCoverForPublication`, `DecodeCoverParaCalentar` | devolvían la `Image` de la miniatura | entregan a la caché de portadas (abajo, costura 1) con `PuenteDibujo.Entrega(mapa, Format32bppPArgb)`: un `Bitmap` de la capa DUEÑO de los puntos y con el formato y las marcas que XP le daba (`new Bitmap(w, h, PArgb)` o `Clone(…, PArgb)`: `HasAlpha`). La de publicar da la copia por guardar como `SKBitmap` (la guarda `SaveThumb`, ya traducida); la del calentador, como `Bitmap` de la capa (la recibe `Form1.cs`) |
| `Form1.Portadas.cs` | `MiniaturaOpaca` | `LockBits(ReadOnly, Format32bppArgb)` | `Pixeles.LeeSinPremultiplicar` a una memoria aparte (la copia que da GDI+ de otro formato), que se suelta en el `finally` |
| `Form1.Portadas.cs` | `HazOpaca` | `PixelFormat != PArgb → return`; `LockBits(ReadWrite, PArgb)` | no BGRA premultiplicado → return (un `SKBitmap` BGRA premultiplicado es el PArgb de XP; el único que la llama le pasa la miniatura de `Mapas.Nuevo`); `GetPixels` y `RowBytes`; `UnlockBits` es `NotifyPixelsChanged` |
| `Form1.Portadas.cs` | `CodecJpegMiniatura` | el `ImageCodecInfo` de MimeType `image/jpeg` | `SKEncodedImageFormat.Jpeg` (SkiaSharp siempre lo trae, como la lista de `Dibujo2D.cs`); `null` sería «no hay» |
| `Form1.Portadas.cs` | `CopiaMiniaturaGpu` | dos `LockBits` PArgb y copia fila a fila | `Mapas.Nuevo`, `GetPixels`/`RowBytes` de los dos, la misma copia, `NotifyPixelsChanged` |
| `Form1.Portadas.cs` | el trabajador de portadas | `Bitmap` de la copia y de la previa | `SKBitmap`; la previa sale de los puntos de la portada publicada (`PuenteDibujo.Mapa(thumb)`) y entra en la caché de previas con `Entrega` (costura 1) |
| `Form1.Portadas.cs` | el precalentado (`EnSeco`) | `delegate (Graphics g)` | `delegate (Rocola.Dibujo2D.Graphics g)`: aquí no se dibuja, solo se pasa el `Graphics` de `EnSeco` (`Form1.Video.cs`) al `RenderContent` de otros (costura 3) |
| `Form1.Biblioteca.cs` | `SaveThumb` | `thumb.Save(tmp, jpeg, ep)` con `Encoder.Quality` 92, o `Save(tmp, ImageFormat.Png)` | `Imagenes.GuardaJpeg(thumb, tmp, 92)` (la calidad en una variable) o `Imagenes.GuardaPng(thumb, tmp)`; recibe un `SKBitmap` |
| `Form1.Biblioteca.cs` | la ventana del buscador que se anima al cerrar (`winSnap`) | `uiOverlay.Clone(entera, PArgb)` | `Mapas.Copia` de los puntos de `uiOverlay` (un `Bitmap` de `Form1.Video.cs`), entregada a `winSnap` (de `Form1.Datos.cs`) con `Entrega` como el `Clone` |
| `CaratulasAdaptativas.cs` | `GpuPreviewStore.Small`, `ToBytes`, `FromBytes` (las previas de 32×32) | `new Bitmap(32, 32, PArgb)`, `Graphics` con `HighQualityBilinear` y `DrawImage`; `LockBits` PArgb de leer y de escribir | `Mapas.Nuevo`, `SKCanvas`, el filtro en una variable e `Imagenes.Pinta` (sin imagen no pinta nada, como `Dibujo2D.cs`); `GetPixels`/`RowBytes` y `NotifyPixelsChanged`. `FromBytes` entrega con `Entrega` (costura 1: la da `GetBatch` de `PreviewsIndexadosGpu.cs`, de otro grupo) |
| `CaratulasAdaptativas.cs` | `PrepareLod` | `stars.CreateOverlay(cell.Preview, …)` (el puente de la frontera) | `CreateOverlay(PuenteDibujo.Mapa(cell.Preview), …)`: la traducida, directo |
| `DibujoLigeroGpu.cs` | `TeselasVisibles.De`, `TrabajoTeselas`, `TeselasQuietas`, `TeselasEnPasos`, `InteriorOpaco` | reciben `Bitmap`; `LockBits` PArgb (el mapa entero, sin copia) si el `Bitmap` es PArgb (`LecturaNativa`), o `LockBits` Argb de la zona | reciben `SKBitmap`; `LecturaNativa` es «BGRA premultiplicado» (el PArgb de XP) y da los puntos mismos (`GetPixels`, `RowBytes`); si no, `Pixeles.LeeSinPremultiplicar` de la zona a una memoria aparte que se suelta. `InteriorOpaco(SKBitmap, alfaOPaleta)`: lo que XP miraba del formato (`IsAlphaPixelFormat` o de paleta) llega como parámetro, porque un `SKBitmap` no lo guarda (lo da el puente) |
| `RespuestaGpu.cs` | `SubeReponiendo` | recibe `Bitmap` y llama a `UpdateOverlay` y `Upload` con él | recibe `SKBitmap` y llama a los de `D3D9Star.cs` que ya reciben `SKBitmap` (frontera) |
| `RespuestaGpu.cs` | `EnsureRecentCover`, `CoverSlot` | `Bitmap` | `Rocola.Dibujo2D.Bitmap` escrito entero: no dibujan, guardan y comparan por identidad las portadas que da `GpuCubeInfo` (`D3D9Star.cs`) y las pasan a `EnsureImmutableCover` (`D3D9Star.cs`, que las sube por su puente) |

**El estado del `Graphics`** (tabla de estado por línea: `varia` = «-» en todas las líneas de estos archivos). Las que pasan los recorridos:
`Form1.Portadas.cs:976` (`FromStream`), `981` (`new Bitmap 256×256 PArgb`), `988` (`FromImage`), `998` (`DrawImage` con `HighQualityBilinear` y
`Half`: la rama sin «JPEG»), `CaratulasAdaptativas.cs:361-362` (`HighQualityBilinear`, medio píxel de fábrica) y `368` (`LockBits` PArgb),
`DibujoLigeroGpu.cs:94` (`LockBits` Argb de los hornos de 256×192 y 128×128), `176` y `347` (PArgb), `Form1.Biblioteca.cs:2672` (`Save` en PNG).
El filtro y el medio píxel van fijos donde XP los pone (son los de la tabla), y lo demás es el estado de fábrica de un `Graphics` nuevo (sin
suavizar, mezcla `SourceOver`): ninguna de estas funciones recibe el `Graphics` de otro ni le cambia nada. Las líneas que la tabla no tiene
(la miniatura opaca con `TileFlipXY` y «JPEG», la guardada que se lee, `CopiaMiniaturaGpu`, `HazOpaca`, `MiniaturaOpaca`, el JPEG de `SaveThumb`,
`FromBytes`, `InteriorOpaco`) se razonaron por su código y se comprueban con el arnés (abajo).

**Las teselas de los hornos.** `EnsureNieblas` y `EnsureVineta` (`D3D9Star.cs`) le pasaban a `TeselasVisibles.De` el mapa horneado prestado
como `Bitmap` Format32bppArgb (el formato con que XP lo hacía), que se leía por la copia sin premultiplicar. Ahora le pasan el `SKBitmap` (las
tres líneas vuelven a ser las de XP) y se lee por los puntos mismos. Las dos lecturas dan el mismo byte de alfa (la tabla de
`Nativo/Pixeles.cs` no toca el alfa) y `De` solo mira el alfa: salen las mismas teselas (arnés: los ocho mapas con `argb` y `pargb`, iguales;
recorridos: abajo).

## Costuras y puentes (la regla: el puente va del lado de lo que no está traducido)

| Costura | Para quién | Por qué | Se quita cuando |
|---|---|---|---|
| 1. **La caché de portadas y la de previas siguen siendo imágenes de la capa** (`coverCache`, `coverTrash`, `gpuPreviewCache`, `PortadaEnMemoria`, `LoadCover`, `LoadCoverAsync`, `RequestCover`, `LoadCoverByPath`, `CachePut`, `CoverEnCache`, `PeekGpuPreview`, `PutGpuPreview`, `WarmGpuPreview`, `StartDiscTrans`, `RetienePortada`, `GpuPreviewStore.Get`, `FromBytes`, `LodTex.Bmp`; `Rocola.Dibujo2D.Image`/`Bitmap` escrito entero). Lo que entra se hace con SkiaSharp y se entrega con `PuenteDibujo.Entrega(mapa, Format32bppPArgb)` (dueño de los puntos, con el formato y las marcas que XP le daba); lo que se lee de ellas para dibujar se toma con `PuenteDibujo.Mapa` (los mismos puntos) | las leen `Form1.cs`, `Form1.Video.cs`, `Form1.Fondo.cs`, `Form1.Buscador.cs`, `Form1.Pared.cs` (y `cubeBox.Cover`), `Form1.Vistas.cs`, `AtraccionGpu.cs`, `EstadoReposoGpu.cs`, `BuscadorPortadasGpu.cs`, `PantallaVivaGpu.cs`, `DiscoVivoGpu.cs` y `D3D9Star.cs` (`GpuCubeInfo.CoverA`/`CoverB`, `GpuGridCell.Preview`/`FullCover`, `EnsureCover`), casi todos de otros grupos o sin traducir; la pared (`port-pared`, en paralelo) ya las recibe como `Bitmap` de la capa. Cambiar el tipo ahora rompería esas ramas al juntarlas. XP compara las portadas por identidad (no volver a subir la misma, `tonos.Fuente`, `RetienePortada`): la caché guarda el MISMO objeto que dan todos | se traduzcan los que las leen: entonces la caché guarda `SKBitmap` y se van `Entrega`, `Mapa` y los tipos escritos enteros (con ellas, las costuras de otros grupos que dicen «la caché de portadas dé `SKBitmap`»: pared, atraccion —`LoadCoverByPath`—, buscador-portadas —`GetCoverYa`—, pantalla-viva —`PeekCoverGpu`—). La paleta (`ExtractCoverPalette`, pared) y los tonos (`ColoresDePortada`, fondo) reciben el `Bitmap` de la capa: el formato de la miniatura es siempre PArgb |
| 2. `SaveThumb(Rocola.Dibujo2D.Bitmap, string)` (`Plataforma/Puente.portadas.cs`) | `Form1.cs` (el calentador guarda la copia que le dio `DecodeCoverParaCalentar`) | `Form1.cs` no está traducido | se traduzca `Form1.cs` y `DecodeCoverParaCalentar` dé la copia como `SKBitmap` |
| 3. `EnSeco(…, delegate (Rocola.Dibujo2D.Graphics g))` en el precalentado | `Form1.Video.cs` (`EnSeco`), `Form1.Buscador.cs` y `Form1.Vistas.cs` (`RenderContent`), `attractView` (`InvokePaint`) | aquí no se dibuja: el `Graphics` de la capa solo se pasa | se traduzcan `Form1.Video.cs` y el dibujo por procesador de las vistas |
| 4. `TeselasEnPasos(ref TrabajoTeselas, Rocola.Dibujo2D.Bitmap, …)` (puente) | `D3D9Star.cs` con `GetUi()` y `GetUiBot()` (la interfaz que hace `Form1.Video.cs`) | la interfaz es un `Bitmap` de la capa | `Form1.Video.cs` haga la interfaz como `SKBitmap` (y `GetUi` lo dé) |
| 5. `InteriorOpaco(Rocola.Dibujo2D.Bitmap)` (puente) | `CaratulasSeparadasGpu.cs` (`cell.FullCover`, la portada entera de la casilla) | la portada es de la capa (costura 1) y su formato dice si XP la miraba (un JPEG de 24 bits es opaco sin mirar) | la portada entera sea `SKBitmap` y lleve su formato (la pared hizo `Nativo/FormatoMapa.cs` en su rama para lo mismo) |
| 6. `SubeReponiendo(…, Rocola.Dibujo2D.Bitmap, …)` (puente) | `BuscadorLigeroGpu.cs` (`SubeUi`) y `BuscadorPortadasGpu.cs` (`SubePieza`) | la interfaz y las piezas los hace `Form1.Video.cs` como `Bitmap` de la capa | las piezas y la interfaz sean `SKBitmap` (con ellas se van las de acabado-estilo y buscador-portadas sobre `SubeUi` y `SubePieza`) |
| 7. `DecodeCoverParaCalentar` da la copia por guardar como `Rocola.Dibujo2D.Bitmap` (`Entrega`) | `Form1.cs` | como la 2 | como la 2 |

Los puentes son delgados: le pasan a la traducida los MISMOS puntos (`PuenteDibujo.Mapa`) y, en `InteriorOpaco`, lo que XP miraba del
formato. Ninguna de las traducidas le cambia el estado a un `Graphics` (no reciben ninguno). Un `Bitmap` ya soltado no tiene puntos:
`PuenteDibujo.Mapa` da `null` y la traducida hace lo que hace con `null` (ver «Riesgos»).

**Costura de otro grupo que se quitó.** La 3 de la frontera (`PORT_frontera.md`): `TeselasVisibles.De` recibía el mapa horneado prestado por
`PuenteDibujo.Bitmap(…, Format32bppArgb)`; ahora recibe el `SKBitmap` (las tres líneas de `EnsureNieblas` y `EnsureVineta` de `D3D9Star.cs`
vuelven a ser las de XP, sin marca). Las sobrecargas con `Bitmap` de `Plataforma/Puente.frontera.cs` (`CreateOverlay`, `CreateCoverShared`,
`UpdateOverlay`, `UpdateOverlayRegion`, `Upload`) siguen usándose todas (`BuscadorLigeroGpu.cs`, `BuscadorPortadasGpu.cs`,
`CaratulasSeparadasGpu.cs`, `FranjaLigeraGpu.cs`, `SuenaVivoGpu.cs`, `TextoMovilGpu.cs`; comprobado quitándolas): no se quitó ninguna; los
archivos de este grupo ya no las usan.

## Herramientas (`Pruebas/VistasPortadas`)

- `Portadas.cs` + `VistasPortadas.csproj` (patrón de `Pruebas/BancoVistas`): el programa entero de esta copia o de la referencia congelada
  (`-p:Fuente=...`, el proyecto copiado fuera) con otra entrada que llama por su nombre (reflexión, con el tipo que pida cada versión) a:
  las miniaturas de 13 portadas —JPEG de 600×600, 1600×1000, 200×900 y 256×256; PNG con alfa de 300×300, 256×256 y 7×9; PNG opaca de 500×500;
  PNG de 1×1; GIF de paleta con color transparente y sin él; BMP de 24 bits; basura— sin guardar, guardadas (PNG o JPEG, con su marca `.alfa`),
  leídas de lo guardado, para publicar (con la copia, guardada después con `SaveThumb`) y leídas otra vez (`CopiaMiniaturaGpu`), del
  calentador (también rehaciendo desde la original la que solo estaba en PNG, y guardando su copia por el puente), con una guardada de otra
  medida, con «Carátulas en JPEG» apagado y encendido (la huella de los puntos, el formato y las marcas de lo que se publica, la de la copia y
  la de cada archivo que queda en la carpeta); `MiniaturaOpaca`, `HazOpaca` y `CopiaMiniaturaGpu` con 8 mapas de alfa en 256 y 33; las previas
  (`Small`, `ToBytes`, `FromBytes`, y `Small` sin imagen); `TeselasVisibles.De` (lado 2 con borde, lado 16 sin él) y `TrabajoTeselas` paso a
  paso y de una vez, de 8 mapas (vacío, lleno, puntos sueltos, centro vacío, esquinas, 1×1, la franja y la interfaz de pantalla) en los dos
  formatos de XP; `InteriorOpaco` por el puente con 8 alfas, 3 lados (256, 5 y 4) y 4 formatos, y directo (`InteriorOpaco(SKBitmap, bool)`)
  contra el puente.
- `arnes.sh`: compila las dos y compara las salidas línea por línea. `control.sh`: diez fallos puestos a propósito. `aot-corre.sh`: el
  binario NativeAOT por las pantallas del grupo.

## Comprobaciones (con números)

Salidas en `/root/rocola-trabajo/port/portadas` (fuera del repositorio; los PNG volcados se cambiaron por sus huellas o se borraron).

- **(a) Compila** (`dotnet build -c Release`): 0 errores y los mismos 15 avisos de la base (`Thread.VolatileRead`, `FormatterServices`,
  `Control.bounds`, dos `Stream.Read`, un `stackalloc`): la lista de archivo, línea y aviso sale idéntica en la base y en la publicación
  NativeAOT de esta rama. Cada paso (los dos commits de la traducción) compila solo.
- **(b) Banco por función** (se añadió un puente, `Plataforma/Puente.portadas.cs`; no se tocó `Nativo/`, `Puente.cs` ni `Dibujo2D.cs`): semillas
  7 y 11 con las letras de XP falsas y 5 sin ellas, 3 000 casos cada una, ejecutores `gdi`, `nativo` y `puente` contra el congelado:
  **los 9 iguales** (27 000 casos, 0 distintos); `puente-objetos`: **407 bien, 0 mal**.
- **(c) Recorridos** (los nueve, `VOLCADO=1 CAPAS=1 HUELLAS=1`, con el candado y la carpeta de siempre, contra `cmp-ref` y `cmp-ref2`):
  los nueve salen con 0 y **sin excepciones** (ni en lo que escriben ni en `rocola_error.txt`). **Capas de Dibujo2D quietas: 215 de 217**; las
  dos que faltan son las dos de siempre de `PORT_JUNTAS.md` (9-11-2026): la hoja de Ctrl+F2 de `ctrlf3` a los 40 s, que solo cambió de nombre
  al ser una hoja nativa (`capa4-hoja-…` en vez de `capa4-dibujo2d-…`), y la hoja de Ctrl+F3 de `hojas` a los 18 s, que enseña cuánto lleva
  medido el repaso de la biblioteca (las prioridades de hilo de XP puestas de verdad). **Texturas quietas: 480 de 487** (en la última junta,
  479). Las 7 que faltan, en `pared2` (3), `ayuda` (2) y `buscador` (2), ninguna es de estos archivos (son la franja de 1024×128, el panel de
  1024×256 y el de 512×256, de otros grupos; todas las portadas de 256×256 y las teselas de los hornos salen) y son de la hora, como en D2, D4
  y la frontera. Otra corrida de esos tres recorridos (`cmp2`) recupera 3 (en B o B2: `pared2` 96 de 98, `ayuda` 65 de 66, `buscador` 77 de 78);
  las 4 que faltan en las dos son justo las que `PORT_frontera.md` ya explicó con la referencia corrida otra vez: la de 1024×256 de `pared2`
  a los 41,9 s (en ese instante esta rama sube otro contenido, a los 41,6 s), la del reloj de la canción a los 62 s de `pared2`, la de
  `ayuda` que la referencia sube dos veces y la traducción una (`c9e4fc67`, en las dos corridas de la traducción sale una vez) y la caja del
  buscador entre dos letras a los 8,6 s (`e843916c`). Al revés, lo quieto de las dos corridas de esta rama en esos tres recorridos (110, 66 y
  77 texturas) sale en `cmp-ref` 95, 65 y 76 veces y en `cmp-ref2` 99, 64 y 76: el mismo ruido en los dos sentidos.
- **(d) Arnés** (`Pruebas/VistasPortadas/arnes.sh`): la referencia congelada y esta rama, **466 líneas iguales** (0 excepciones; las 96 llamadas a
  `InteriorOpaco` directo, iguales a las del puente). Lo que cubre, arriba («Herramientas»). Fuera de la comparación, a propósito: `HazOpaca`
  con un mapa Format32bppArgb (XP no lo toca; en la traducción no existe un mapa así: todos los `SKBitmap` son el PArgb de XP y el único que
  la llama le pasa la miniatura recién hecha); la referencia lo comprueba aparte (16 de 16 «no lo toca»). **Controles negativos**
  (`control.sh`, la traducción con un fallo puesto a propósito, contra la salida de la referencia): sin el medio píxel de la miniatura, 191
  líneas distintas de 466; sin el espejo del borde, 55; la miniatura sin prefiltrar, 100; la previa sin prefiltrar, 24; las GIF de paleta
  como opacas, 22; `MiniaturaOpaca` con otro umbral de alfa, 1; `InteriorOpaco` sin mirar si el formato es de paleta, 7; las teselas sin la
  fila de abajo, 8; `FromBytes` con un punto menos por fila, 24; el JPEG guardado con otra calidad, 55.
- **(e) Cintas de la tarjeta**: no aplica. Lo que se sube es lo mismo (las mismas huellas en los recorridos, también las portadas) y no se
  tocó el dispositivo ni lo que la grabadora apunta: las cintas reproducen llamadas al dispositivo, que no pasan por estos archivos.
- **(f) NativeAOT** (`dotnet publish -c Release -r linux-x64 -o …/aot`): **0 avisos IL/AOT**, los 15 del compilador de siempre (la misma lista).
  El binario nativo por las pantallas del grupo (`Pruebas/VistasPortadas/aot-corre.sh`, las teclas, capturas y opciones de los recorridos):
  `pared` 12 de 12 capturas, `pared2` 6 de 6, `buscador` 11 de 11, `video` 7 de 7, las cuatro salen con 0 y **0 excepciones** en lo que
  escriben y en `rocola_error.txt` (solo «Volumen de Windows al arrancar: no se pudo (no hay mezclador de altavoces)», sin excepción, como
  en la referencia). Las capturas enseñan la pared con sus portadas.
- **(g) Diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py … -v`): `Form1.Portadas.cs` 74 líneas quitadas y 71 puestas, 0 sin marca;
  `DibujoLigeroGpu.cs` 24 y 23, 0; `RespuestaGpu.cs` 5 y 5, 0; `CaratulasAdaptativas.cs` 31 y 28, 0; `Form1.Biblioteca.cs` 92 y 89, 2 sin marca
  (las dos del medidor de videos de la pista windows, de antes; la del `EncoderParameter`, que en la base iba sin marca, ahora es de esta
  traducción y la lleva); `D3D9Star.cs` de 405/269 a 402/266 (las tres líneas de las teselas vuelven a ser las de XP), 12 sin marca, las de
  antes. `git diff` desde la base: solo las líneas cambiadas (63, 33, 25, 23, 5 y 3), con el mismo número de líneas y de CRLF en cada archivo.

## Riesgos

- **Un `Bitmap` de la capa ya soltado** que llegue por un puente o por `PuenteDibujo.Mapa`: sus puntos son `null` y la traducida hace lo de
  `null` (`InteriorOpaco` da falso, `TeselasEnPasos` no analiza, `Small` no pinta, `SaveThumb` no guarda), donde XP —y la referencia— lanzaban
  una excepción al leer su medida. En los caminos de XP no se llega: las cachés sacan una portada antes de soltarla y la sueltan en el reloj
  de la pantalla (`DrainCoverTrash`), y `GetUi` da siempre la interfaz viva.
- **Un `SKBitmap` soltado no lanza: cierra la rocola** (`PORT_JUNTAS.md`). `TrabajoTeselas` guarda los puntos de la interfaz entre cuadros;
  solo avanza después de comprobar en ese mismo cuadro, en el hilo de la pantalla, que son los de la interfaz viva (`ReferenceEquals` con
  `PuenteDibujo.Mapa(GetUi())` y su medida), como en XP. Cuando la interfaz sea un `SKBitmap` de `Form1.Video.cs`, conservar ese orden (no
  soltar la interfaz que se está analizando sin que `GetUi` deje de darla).
- **El formato de XP ya no viaja con el mapa.** `LecturaNativa` decide por el tipo del `SKBitmap` (BGRA premultiplicado = PArgb): los hornos
  que XP hacía Format32bppArgb se leen ahora por los puntos mismos y no por la copia sin premultiplicar; `De` solo mira el alfa, que es el mismo
  byte en las dos (arnés y recorridos: iguales). `HazOpaca` hace lo mismo con su comprobación de formato. `InteriorOpaco` recibe lo que
  importaba del formato (alfa o paleta) del puente; cuando la portada entera sea un `SKBitmap`, quien la dé tiene que dar también ese dato
  (un JPEG de 24 bits XP lo daba por opaco sin mirarlo; si se perdiera, se mira punto a punto y sale lo mismo salvo que el borde suave de un
  JPEG quede a menos de 255 por dentro: no pasa con un JPEG leído, que es opaco del todo).
- **La caché de portadas sigue siendo de la capa** (costura 1): es la costura grande del grupo y de varios otros. Cuando se cambie, conservar
  la identidad (un objeto por portada publicada, el mismo para todos) y quién suelta los puntos (la caché, en `DrainCoverTrash`).
- `CaratulasAdaptativas.cs` conserva `using Rocola.Dibujo2D` en la línea 3 solo por las líneas de ventanas; quitarlo al juntar con
  `port-ventanas2` (allí esas líneas ya no usan `Font`).
- Nada de esto se probó en el A4.

## Lo que queda

- Las costuras 1 a 7, cuando se traduzcan `Form1.cs`, `Form1.Video.cs`, `Form1.Fondo.cs`, `Form1.Buscador.cs`, `Form1.Vistas.cs` y los de la
  pared (la caché de portadas con `SKBitmap` y su formato), y al final borrar `Plataforma/Puente.portadas.cs`.
- Las diferencias (a)-(e) de `PORT_DIBUJO.md` que tocan a este grupo: (e1) la miniatura se reduce con `HighQualityBilinear` y el medio píxel
  `Half` (con `Half` no aplica la duda del medio píxel), y las previas de 32×32 con el medio píxel de fábrica (sí aplica). La traducción copia la
  referencia; corregirlas lo decide el dueño.

## Revisión (revisor independiente, 11-11-2026)

Se revisó el diff `ae8b50b..port-portadas` línea por línea contra `Rocola/` (el original de XP) y se repitieron las comprobaciones.
**No se encontró ningún fallo de la traducción**; solo se añadió cobertura al arnés.

**Lo revisado y por qué está bien:**
- Ningún uso de GDI+ sin traducir en `Form1.Portadas.cs`, `DibujoLigeroGpu.cs`, `RespuestaGpu.cs` ni `Form1.Biblioteca.cs` (ni dibujo ni
  `Clone`/`Save`/`LockBits` sobre la capa): solo quedan `Rocola.Dibujo2D.Image`/`Bitmap`/`PixelFormat`/`Graphics` escritos enteros en las
  costuras 1-7. En `CaratulasAdaptativas.cs`, solo las líneas de ventanas (`Font` del historial de carátulas).
- Equivalencias: `fuenteOpaca` (`!= Argb32 && != Paleta8 && != Argb32`) es exactamente lo de XP con la lectura de `Dibujo2D.cs`
  (`Flags` solo marca `HasAlpha` en Argb32, `Indexed` solo en Paleta8); `InteriorOpaco` recibe `IsAlphaPixelFormat || Indexed` del puente y
  `!alfaOPaleta` es la condición de XP; `Entrega(…, Format32bppPArgb)` deja el mismo formato y `Flags` que el `Clone`/`new Bitmap` de la
  referencia (TraeAlfa verdadero); `Mapas.Copia` con el trozo entero es la copia punto por punto; `HazOpaca`, `CopiaMiniaturaGpu`, `ToBytes` y
  `FromBytes` leen y escriben los enteros BGRA premultiplicados como el `LockBits` PArgb directo de la capa. `De`, `Avanza`, `InteriorOpaco`
  y `MiniaturaOpaca` solo miran el byte de alfa, que es el mismo premultiplicado o no. El estado (filtro `BilinealAlta`, medio píxel en la
  miniatura y no en la previa) es el de `estado_por_linea.tsv` (líneas 998, 362).
- Identidad y dueños: `PuenteDibujo.Mapa` da siempre el mismo `SKBitmap` para la misma imagen de la capa (`Sk` solo se pone al crearla), así
  que `ReferenceEquals(trabajo.Fuente, b)` de `TeselasEnPasos` se comporta como en XP; lo publicado (`Entrega`) es dueño de sus puntos y lo
  suelta `DrainCoverTrash`; la previa se entrega o se suelta en el `finally` del trabajador, la copia por guardar también; ningún `SKBitmap`
  se comparte entre hilos sin el candado de XP (la miniatura es privada hasta `CachePut`).
- Las tres líneas de `D3D9Star.cs` (`EnsureNieblas`, `EnsureVineta`) son ahora idénticas a las de XP.
- Finales de línea y número de líneas: los seis archivos de código, iguales a la base línea por línea (patrón de CR comparado).

**Añadido (commit «revision»): la rama «no nativa».** Ningún mapa de la rocola llega hoy a `De`, `TrabajoTeselas` o `InteriorOpaco` sin
ser BGRA premultiplicado, así que su rama de copia sin premultiplicar (`Pixeles.LeeSinPremultiplicar`, la del `LockBits` Format32bppArgb de
XP) no la pasaba ninguna prueba. El arnés la pasa ahora con el mismo mapa en BGRA sin premultiplicar y la compara con la premultiplicada
(solo a la salida de errores, `NO NATIVO DISTINTO`: la comparación con la referencia no cambia): **104 comparaciones, 0 distintas** (los 8
mapas de teselas con `De` de lado 2 y 16 y `TrabajoTeselas` paso a paso; las 96 de `InteriorOpaco`).

**Comprobaciones repetidas (números):**
- `dotnet build -c Release`: 0 errores, 15 avisos, la misma lista archivo:línea:aviso que la base (ninguno en los archivos del grupo).
- `diff-xp.py -v`: `Form1.Portadas.cs` 74/71 (0 sin marca), `DibujoLigeroGpu.cs` 24/23 (0), `RespuestaGpu.cs` 5/5 (0),
  `CaratulasAdaptativas.cs` 31/28 (0), `Form1.Biblioteca.cs` 92/89 (2, las del medidor de videos de la pista windows), `D3D9Star.cs` 402/266
  (12, las de antes).
- Arnés: 466 líneas IGUALES con la referencia congelada, 0 excepciones, 0 «DIRECTO DISTINTO», 0 «NO NATIVO DISTINTO» de 104.
- Recorridos `pared`, `pared2`, `buscador`, `video` (`VOLCADO=1 CAPAS=1 HUELLAS=1`, candado y carpeta de siempre; salida
  `/root/rocola-trabajo/port/portadas/rev`): los cuatro salen con 0, sin excepciones (en `rocola_error.txt` solo el aviso del mezclador y la
  «Pantalla parada» del arranque con la máquina cargada). Contra `cmp-ref`/`cmp-ref2`: **capas de Dibujo2D quietas 74 de 74**; texturas
  quietas 304 de 309. Las 5 que faltan no son de este grupo ni de su tamaño (ninguna portada de 256×256 ni tesela de horno): `pared2`
  1024×256 a los 41,9 s y `buscador` `e843916c` a los 8,6 s (las de la hora que explica `PORT_frontera.md`), y en `pared2` la tanda de los
  62 s (la tecla SALTAR: 1024×64 `906bfb35` y 1024×128 `2f591363`) y la franja de los 69,2 s (`1f1712f1`); `906bfb35` y `1f1712f1` sí salen
  en las corridas anteriores de esta rama (`cmp`, `cmp2`): ruido de la hora.
- Banco: no se repitió porque no compila ningún archivo que cambie esta rama (`Nuevo.csproj` toma `Dibujo2D.cs`, `Puente.cs` y una lista
  de `Nativo/`, todos iguales a la base; `Puente.portadas.cs` no entra): su resultado es el de la base por construcción.
- Cintas: no aplica (no se tocó el dispositivo ni la grabadora). NativeAOT: no se repitió (los cambios son de tipos, sin reflexión nueva);
  queda la lista de avisos de la publicación del autor (`w-aot.txt`), idéntica a la base.

**Riesgos que se añaden** (de XP o de la capa, sin cambiar nada):
- `GpuPreviewStore.Small` con una imagen `null` (solo una portada ya soltada: inalcanzable, la miniatura es privada hasta publicarse)
  devuelve una previa transparente, como `Dibujo2D.cs`; GDI+ de verdad lanzaría `ArgumentNullException` en `DrawImage`. Se conserva como la
  referencia.
- `Imagenes.Lee(Stream)` copia el flujo a un arreglo nuevo (como `Dibujo2D.cs`): la lectura de la miniatura vuelve a reservar un arreglo del
  tamaño del archivo, que XP evitaba reusando `lecturaCaratula`. No cambia nada de lo que se ve; es rendimiento, de `Nativo/Imagenes.cs`.
