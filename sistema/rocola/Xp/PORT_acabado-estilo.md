# Port nativo de XP: la lista del disco, la franja de lejos, la guía del rojo y el buscador ligero, de GDI+ a SkiaSharp directo

Grupo «acabado-estilo» de la tanda 2 del port nativo (el dibujo con GDI+ de los archivos de la capa de la tarjeta), 8-11-2026. Decisiones
del dueño en `AGENTS.md` (8-11-2026: «que tradujeras todo el código para que trabajara nativamente en Linux», «ningún layer de
traducción») y la receta en `PORT_DIBUJO.md` (D2-D4). Base: `b609319`; rama `port-acabado-estilo`. Aquí va lo que se tradujo, cada
costura con lo que todavía no está traducido (con su razón y cuándo se quita), las comprobaciones con sus números y lo que queda. Nada de
esto se probó en el A4.

## Qué se tradujo

`Codigo/AcabadoGpu.cs`, `EstiloGpu.cs`, `GuiaRojoGpu.cs` y `BuscadorLigeroGpu.cs` ya no ven los tipos que imitan a GDI+ (`using
Rocola.Dibujo2D` fuera; quedan escritos enteros solo en las costuras de abajo). Todo su dibujo va con SkiaSharp (`SKCanvas`, `SKPaint`,
`SKPath`, `SKBitmap`, `SKShader`) y las funciones nativas (`Nativo/`), línea por línea como en XP: cada línea que cambia lleva su
`// EN LINUX: ...`, cada archivo conserva su número de líneas (411, 259, 182 y 64; las llamadas que en XP ocupaban varias líneas dejan las
sobrantes como comentario `EN LINUX`) y sus finales de línea (LF). La lógica de XP no se tocó: mismos valores, mismo orden, mismas ramas.
`Pruebas/Tarjeta/guiones/diff-xp.py` de los cuatro: 97/95, 54/52, 30/28 y 2/2 líneas quitadas/puestas, **0 sin marca**.

| Archivo | Función | Lo que dibuja | Cómo quedó |
|---|---|---|---|
| `GuiaRojoGpu.cs` | `PintaGuiaRojo`, `PintaGuiaRojoAyuda`, `ResaltaTecla`, `DrawGuiaRojo` | la guía del rojo mantenido: el filo de la barra de letras y de las casillas BUSCAR y AYUDA, las flechitas y la tecla encendida | `(SKCanvas, suave, copia, medio, ...)`; `DrawPath` es `Pixel.Traza`, `FillPath` `Pixel.Rellena`, `FillPolygon` `Pixel.RellenaPoligono` (Alternate); `Pen(color, 2)` un `SKPaint` con su ancho |
| `EstiloGpu.cs` | `FranjaLejos`, `PintaTituloCompacto`, `PintaSiguienteCompacto`, `PintaTituloEnCaja`, `PintaTarjetaSiguiente`, `PintaRenglonLejos` | la franja de lo que suena con la letra para leer de lejos: el título que crece, el artista que baja, la tarjeta «A CONTINUACIÓN» | `(SKCanvas, suave, ajuste, copia, medio, ...)`; `DrawString` es `TextoGdiPlus.Escribe`; `Save`/`TranslateTransform`/`Restore`, los del lienzo |
| `EstiloGpu.cs` | `EscalaTituloLejos`, `CabeConLetra`, `RecortaLejos`, `AnchoLejos`, `FinRenglonArtista` | (miden) | `MeasureString` es `TextoGdiPlus.Mide` (del `Graphics` solo pesa su modo de letra: `AjusteLetra`); `TextRenderer.MeasureText` es `TextoGdi.Mide`; `new Font(f.FontFamily, f.Size * e, f.Style, f.Unit)` es la `Letra` de esa familia y estilo a ese tamaño en su unidad (`EnPuntos` o `EnPixeles`); `f.GetHeight(g)` es `Letra.AltoRenglon` |
| `AcabadoGpu.cs` | `BakeGpuListLayer`, `UpdateGpuSnapshot`, `GpuRowBuffer`, `BakeGpuPrevious` | las capas de la lista del disco para la tarjeta: la página sin elegir, la fila elegida (y la instantánea que la restaura) y la página que se va al hojear | `SKBitmap` (`Mapas.Nuevo`); `DrawImage`/`DrawImageUnscaled` son `Imagenes.Pinta` con el filtro, el medio píxel y la copia de cada `Graphics.FromImage` (los de fábrica; `SourceCopy` donde XP lo pone); el `ImageAttributes` de la clave es un `Color?` (`SetColorKey(BackColor, BackColor)`: la clave baja y la alta) con la envoltura de fábrica (`Clamp` = `Envoltura.Transparente`) |
| `AcabadoGpu.cs` | `BakeSongGlassTint`, `BakeSongGlass` | el vidrio de la lista (neutro o azul): el degradado C4, el brillo a 115° y el filo | `SKBitmap`; `LinearGradientBrush(caja, a, b, 115)` es `Degradado.Lineal`; el pincel de `DegradadoV` en un `SKPaint` (abajo) |
| `AcabadoGpu.cs` | `RenderTransparentGpuTint`, `RenderTransparentGpuMode`, `RenderOpaqueGpu` | la lista en la imagen de la interfaz cuando el vidrio no va en piezas: el vidrio (con el tinte de una `ColorMatrix`) o el velo, y la página (y la que se va) con la clave | `(SKCanvas, suave, copia, filtro, medio, ...)`; la `ColorMatrix` es la `float[5][5]` de `MatrizColor.Identidad()` con `[0][0]`, `[1][1]` y `[2][2]`; el velo, un `SKPaint` guardado como el `SolidBrush` de XP |
| `AcabadoGpu.cs` | `DrawSongPagerGpu` | la barra «PÁGINA n / m» con ANTERIOR y SIGUIENTE | `(SKCanvas, suave, ajuste, copia, medio)`; `new Font(GenericSansSerif, px, Bold, Pixel)` es `Letra.EnPixeles(Letras.SinRemates, px, true)`; el `StringFormat` un `FormatoTexto` (`FormatFlags = NoWrap` deja solo `SinPartir`; `EllipsisCharacter` es `PuntosPorLetra`) |
| `BuscadorLigeroGpu.cs` | `SubeUi` | (no dibuja: sube la imagen de la interfaz) | el `Bitmap` que recibe, escrito entero (costura con `D3D9Star.cs` y `Form1.Video.cs`) |

### El estado del `Graphics`

| Dónde | Suavizado | Modo de letra | Copia | Filtro | Medio píxel | De dónde sale |
|---|---|---|---|---|---|---|
| `GuiaRojoGpu.cs` (líneas 81-124 de la tabla) | parámetro | — | parámetro | — | parámetro | `DrawGuiaRojo` la llama `EnsureUiOverlay` (`Form1.Video.cs`, sin traducir) con su `Graphics`: el puente le pasa su lienzo y su estado (en los recorridos, 7 de los 9 y 440 llamadas: siempre `AntiAlias`, sin copia, `Default`, identidad, sin recorte) |
| `EstiloGpu.cs` (77, 85, 98-103, 110, 171-174, 196, 214) | parámetro | parámetro | parámetro | — | parámetro | la franja la pinta `EnsureUiBotOverlay` (sin traducir): siempre `AntiAlias`, `AntiAliasGridFit`, `SourceOver`, `Bilinear`, `Default`, trasladada (`m=T`), en los 9 recorridos; lo único que cambia entre llamadas (las 8 líneas «que varían» de la tabla) es el recorte (`r=si` x95 / `no` x32 en 98-103: el recuadro de `EmpiezaRecuadro` cuando solo cambia el reloj), que lleva el lienzo |
| `AcabadoGpu.cs`, cada `Graphics.FromImage` (84-164, 344-355) | fijo | fijo | fijo | fijo | fijo | un `Graphics` nuevo: `None`, `SystemDefault`, `SourceOver`, `Bilinear`, `Default`, identidad (tabla: de 8 a 50 llamadas por línea, iguales, en 6 recorridos); `SourceCopy` donde XP lo pone (99: la variable `copia`, devuelta en el `finally` como en XP; 113: `copiaCopy`, la del `Graphics` nuevo de la fila) y `AntiAlias` en el vidrio (354) |
| `RenderTransparentGpuTint`, `RenderOpaqueGpu`, `DrawSongPagerGpu` | parámetro | parámetro | parámetro | parámetro | parámetro | no salen en la tabla (los recorridos usan el vidrio en piezas y discos de una página): los llama `EnsureUiOverlay` con su `Graphics` (`AntiAlias`, `AntiAliasGridFit`, trasladado, con el recorte de la lista); razonado desde el código y comprobado con el arnés (abajo) con 6 modos de letra, 4 de suavizado, copia, 4 de medio píxel, 4 filtros, traslaciones, escala, giro y recortes |

Ninguna de las funciones traducidas le cambia el estado al `Graphics` del que llama sin devolverlo (las que lo cambian lo guardan y lo
devuelven, como `FranjaLejos` y `UpdateGpuSnapshot`): los puentes no tienen nada que devolverle. Las funciones sin traducir que llaman las
traducidas (`PintaArtistaCompacto`, `DrawLabelOverlay`, `DrawCtrlOverlay`, `CtrlDirLeft`, `CtrlDirRight`) tampoco dejan nada que las
traducidas tengan que leer después: `GeneroView.RenderContent` pone `AntiAlias` (el que ya había) dentro del `Save`/`Restore` de
`DrawCtrlOverlay`; las demás no tocan el estado.

## Costuras con lo que no está traducido (la regla: el puente va del lado de lo que no está traducido)

Lo que hace falta y no está en `Plataforma/Puente.cs` va en **`Plataforma/Puente.acabado-estilo.cs`** (`PuenteDibujo` y `Form1` son
`partial`; los nombres nuevos llevan «Acabado» para no chocar con los de otro grupo). No se tocó `Puente.cs`, `Dibujo2D.cs` ni ningún
`Nativo/*.cs`, ni ningún archivo de otro grupo: quien llama a estas funciones no cambia.

| Costura | Dónde | Qué se hace | Se quita cuando |
|---|---|---|---|
| las firmas de XP que llama `Form1.Video.cs` (`EnsureUiOverlay`: la imagen de la interfaz) | `DrawGuiaRojo(Graphics)`, `DrawSongPagerGpu(Graphics)`, `SongListView.RenderTransparentGpuTint(Graphics, ...)` y `RenderOpaqueGpu(Graphics)` | sobrecargas delgadas en `Puente.acabado-estilo.cs`: `PuenteDibujo.Lienzo(g)` y `Estado(g)` a la traducida; nada que devolver | `Form1.Video.cs` se traduzca (las llamará con su lienzo y su estado) |
| las firmas de XP que llama `Form1.Video.cs` (`EnsureUiBotOverlay`: la franja de abajo) | `FranjaLejos(Graphics)`, `PintaTituloCompacto(Graphics, bool)`, `PintaSiguienteCompacto(Graphics)` | igual | igual |
| la firma de XP que llama `TextoMovilGpu.cs` (`PintaArtistaCompacto`) | `PintaRenglonLejos(Graphics, string, Font, Color, RectangleF, StringAlignment)` | igual, con la MISMA `Letra` de la `Font` (`PuenteDibujo.Letra`, sin copia) y la alineación por su valor (`StringAlignment` y `Alinea` valen lo mismo) | `TextoMovilGpu.cs` se traduzca |
| lo que la franja le pide a lo que no está traducido y pinta con un `Graphics` | `FranjaLejos`: `PintaArtistaCompacto` (`TextoMovilGpu.cs`), `DrawLabelOverlay` y `DrawCtrlOverlay` (`Form1.Video.cs`) | `gg = PuenteDibujo.GraphicsAcabado(lienzo, suave, ajuste, copia, medio)`: un `Graphics` sobre el MISMO lienzo (ve la traslación del renglón del artista y el recorte), con el estado de la franja; el `Save`/`RestoreToCount` del lienzo los envuelve como el `Save`/`Restore` de XP | se traduzcan `TextoMovilGpu.cs` y `Form1.Video.cs` |
| `TextoConCinta` (`TextoMovilGpu.cs`) pide `Graphics`, `Font` y `StringFormat` | `PintaRenglonLejos` | `TextoConCintaAcabado`: el `Graphics` de `GraphicsAcabado`, la `Font` de esa `Letra` (`PuenteDibujo.Font`: familia, tamaño, estilo y unidad, que son lo que va en la clave de la cinta; se suelta al acabar: la cinta no la guarda) y el MISMO `FormatoTexto` (`PuenteDibujo.StringFormat`) | `TextoMovilGpu.cs` se traduzca |
| `CtrlDirLeft` y `CtrlDirRight` (`Form1.Pared.cs`) pintan con un `Graphics` | `DrawSongPagerGpu` | `GraphicsAcabado` con el estado de la barra (no le cambian el estado) | `Form1.Pared.cs` se traduzca |
| `RoundedPath` (`Form1.Fondo.cs`) devuelve un `GraphicsPath` | `PintaGuiaRojoAyuda` (3), `ResaltaTecla` (2), `PintaTarjetaSiguiente`, `DrawSongPagerGpu`, `BakeSongGlassTint` | `using (Rocola.Dibujo2D.GraphicsPath p = RoundedPath(...))` y se pinta su `SKPath` (`PuenteDibujo.Ruta(p)`: el mismo, sin copia; su relleno, `EvenOdd`, el de fábrica de GDI+) | `RoundedPath` devuelva un `SKPath` |
| `DegradadoV` (`PulidoGpu.cs`) devuelve un `Brush` (un degradado, o con la interfaz tramada una textura) | `BakeSongGlassTint` | su pincel en un `SKPaint` (`PuenteDibujo.Pincel(b, medio)`: el shader con el medio píxel de este lienzo, el de fábrica), soltado con su shader al acabar | `DegradadoV` se traduzca |
| las páginas horneadas (`hornoPag`, `hornoAnt`) y `PintaPagina` (`Form1.Vistas.cs`) | `BakeGpuListLayer`, `UpdateGpuSnapshot`, `BakeGpuPrevious`, `RenderTransparentGpuTint`, `RenderOpaqueGpu` | sus puntos por `PuenteDibujo.Mapa` (los mismos, sin dueño); `PintaPagina` con un `Graphics` sobre el lienzo nuevo (`PuenteDibujo.Graphics(lienzo, identidad)`: como `Graphics.FromImage`, estado de fábrica y la matriz del HDC de un mapa de bits), el MISMO para las dos `PintaPagina` de la fila elegida (la segunda hereda lo que deja la primera, como en XP) | `Form1.Vistas.cs` se traduzca |
| las piezas de la tarjeta (`uiPieza`, de `Form1.Video.cs`, que sube `D3D9Star.cs`) son `Bitmap` | `EnsureSongPiece` | `PuenteDibujo.Entrega(next, Format32bppPArgb)`: un `Bitmap` dueño de los puntos con el formato con que XP lo hacía (al soltar la pieza se sueltan sus puntos, como antes) | las piezas sean `SKBitmap` (`Form1.Video.cs` y `D3D9Star.cs`) |
| las letras de las etiquetas (`lblNowTop`, `lblNow`, `lblNext`: WinForms) | `FinRenglonArtista`, `PintaTituloEnCaja`, `PintaTituloCompacto`, `PintaSiguienteCompacto` | `PuenteDibujo.Letra(label.Font)`: la misma `Letra`, que no se suelta | las ventanas sean nativas |
| `DrawExtraGpu` (no dibuja) pasa el `Graphics` de `DibujaPiezaViva` a `DrawOneResultCard` y `DrawPreviewInfo` (`Form1.Buscador.cs`) | `AcabadoGpu.cs` | el tipo escrito entero (`Rocola.Dibujo2D.Graphics`) | `Form1.Buscador.cs` se traduzca |
| `SubeUi` (no dibuja) recibe la imagen de la interfaz como `Bitmap` (`GetUi` de `D3D9Star.cs`, hecha por `EnsureUiOverlay`) y la da a `UpdateOverlayRegion` y `SubeReponiendo` | `BuscadorLigeroGpu.cs` | el tipo escrito entero (`Rocola.Dibujo2D.Bitmap`) | la imagen de la interfaz sea `SKBitmap` |

`GraphicsAcabado` hace el `Graphics` con el filtro de fábrica (`Bilinear`) y la matriz del HDC de un mapa de bits (la identidad): quien
llama a las traducidas pinta en mapas (`uiOverlay` y `uiBotOverlay` de `Form1.Video.cs`, hechos con `Graphics.FromImage` y sin tocar el
filtro), y lo que se llama con ese `Graphics` no pinta imágenes ni usa `TextRenderer` (el único que mira la matriz del HDC).

## Comprobaciones

Carpeta de trabajo, fuera del repositorio: `/root/rocola-trabajo/port/acabado-estilo` (las salidas citadas abajo están ahí).

- **(a) Compila**: `dotnet build -c Release` en `sistema/rocola/Xp`, desde cero (`--no-incremental`): **0 errores y 15 avisos, los mismos
  15 de la base** (`avisos-base.txt` contra `avisos-ahora.txt`, normalizados por archivo y línea: diff vacío; son los de siempre:
  `Thread.VolatileRead`, `FormatterServices`, `Control.bounds`, dos `Stream.Read` y un `stackalloc`, en código que este grupo no toca).
  Cada uno de los cuatro pasos de la traducción se compiló antes de su commit (0 errores, 15 avisos).
- **(b) Banco por función** (`Pruebas/BancoDibujo`; este grupo no tocó `Nativo/`, `Dibujo2D.cs` ni `Puente.cs`, y el banco no compila
  `Puente.acabado-estilo.cs`, pero se pasó igual): semillas 7 y 11 con las letras de XP falsas y la 5 con DejaVu, 3 000 casos cada una, los
  ejecutores `gdi`, `nativo` y `puente` contra el Congelado: **0 distintos** en los 27 000; `puente-objetos`: **407 comprobaciones bien, 0
  mal**.
- **(c) Recorridos del programa entero** (`Pruebas/Recorridos`, `VOLCADO=1 CAPAS=1 HUELLAS=1`, siempre con el candado y la carpeta de
  siempre; contra las dos corridas de la referencia `cmp-ref` y `cmp-ref2`, que no se rehicieron):
  - con el programa tal cual (`cmp/`): las **capas de Dibujo2D quietas, 217 de 217 idénticas**; texturas, ninguna: el volcado de las
    texturas (`ROCOLA_VOLCADO_TEXTURAS`) estaba en `Plataforma/Direct3DaGL.cs` y se fue con él en la tanda 1, así que el programa ya no
    vuelca nada (lo vio también el grupo «suena»);
  - con una copia instrumentada del programa (`instr/`, hecha con `Pruebas/VistasSuena/instrumenta-volcado.sh` del grupo «suena», de su
    rama `port-suena`, usado desde su copia: el mismo volcado puesto donde ahora se suben los puntos, `TexturasGl.Sube`; el programa no
    cambia): capas **217 de 217**; texturas quietas **477 de 487** (como multiconjunto) y **373 de 381** distintas
    (`cmp-instr-quietas.txt`). Las que faltaron son de un instante: repitiendo
    `pared2`, `buscador` y `ayuda` (`cmp-instr2/`), las dos corridas juntas dan `ayuda` 56 de 56, `buscador` 58 de 60 y `pared2` 65 de 67;
    y otra corrida de `buscador` guardando los PNG (borrados después de mirarlos; lo visto, en `NOTAS.txt`) trae también las dos de `buscador` que faltaban (la barra del buscador a los
    8,1 s con la «l» escrita, `e843916c…`, y la cabecera con la letra que se desliza de L a O a los 25,6 s, `8cd9108c…`; ninguna es de
    estos archivos, y entre la referencia, la base instrumentada y esta copia los cuadros intermedios de esa animación salen distintos en
    cada corrida). Las texturas de la franja (las de `EstiloGpu.cs`: «Nada reproduciendose» en grande, «Disco Largo» bajado con la letra de
    lejos) salen idénticas a las de la referencia (`76bfb78e…`, `05e950e3…`, `94e21e64…`). Y otra corrida de `pared2` con los PNG, de la
    referencia y de esta copia: la franja que faltaba (`2f591363…`, a los 62,7 s en la referencia) es en esta copia `844f13fe…` a los
    62,8 s, con **197 puntos distintos, todos en la caja (902,74)-(924,85): el reloj** (`0:39 / 2:30` en la referencia, `0:01 / 2:30` aquí:
    la canción saltó un instante antes; `reloj-pared2-62s.png`), y a los 64 s las dos dan la misma (`1f1e16c5…`). La otra de `pared2`
    (`2a697d30…`, 1024×256 a los 41,8 s: el panel del estado extendido, de otro grupo) no salió tampoco en esta corrida nueva de la
    referencia: es de un instante;
  - **dentro del disco sin el vidrio en piezas** (`Pruebas/VistasAcabadoEstilo/disco.sh`: lo que los nueve recorridos no pasan, la lista
    pintada en la imagen de la interfaz por `RenderTransparentGpuTint` con el vidrio, con el velo, y por `RenderOpaqueGpu`): la referencia
    dos veces (`disco-ref`, `disco-ref2`: **100 de 100 texturas y 60 de 60 capas iguales entre sí**, es un recorrido sin reloj) y esta copia
    instrumentada (`disco-nuevo`): **capas 60 de 60; texturas quietas 72 de 72 distintas** (97 de 100 como multiconjunto: las mismas texturas subidas
    otro número de veces, un 512×256 y la cabecera una vez menos, y en `glass_capas` dos 1024×1024 una vez más) y **0 texturas que no estén en la
    referencia**.
- **(d) Arnés directo** (`Pruebas/VistasAcabadoEstilo`, `compara.sh`): el programa entero compilado con la referencia congelada, con la
  base `b609319` y con esta copia, con otra entrada que llama con datos fijos a lo que los recorridos no pasan o pasan con un solo estado
  (ver el commit y el comentario de `Vistas.cs`): 700 renglones de la franja (`PintaRenglonLejos`, un cuarto con las cintas de texto
  puestas), 2 880 recortes con puntos (`RecortaLejos`) y 1 500 escalas del título (`EscalaTituloLejos`, los 6 modos de letra, 8 letras, 10
  textos con acentos, eñe, `&`, emoji, tabuladores, saltos y nulo), 240 tarjetas «A CONTINUACIÓN», 300 guías del rojo (`PintaGuiaRojoAyuda`,
  `PintaGuiaRojo`, `ResaltaTecla`), 36 vidrios (8 medidas, neutro o azul, con la interfaz tramada y sin ella), 160 barras de páginas
  (`DrawSongPagerGpu`, que los recorridos no pasan: 1 a 12 páginas), 24 listas del disco (la capa, la fila elegida dos veces con la
  instantánea que restaura, el horno, el dibujo en la imagen de la interfaz con el vidrio, el tinte, el velo y la opaca, y hojeando de lado
  y de arriba abajo a 10, 100 y 200 ms con su capa de la página que se va) y 120 franjas enteras como las pinta `EnsureUiBotOverlay` (letra
  de lejos o no, recuadro, MEJORADA, la pastilla del género, cintas); cada caso con un estado del `Graphics` (la mitad el de los
  recorridos; la otra al azar: suavizado, modo de letra, copia, medio píxel, filtro, traslación entera o con decimales, escala, giro,
  recortes que reemplazan, se cruzan o de un trazo) y su huella (los puntos tal cual y como los sube la tarjeta), lo que devuelve y las
  cintas que deja. Resultado, con las letras de XP falsas y con DejaVu: **6 607 líneas cada vez, 0 excepciones; esta copia contra la
  referencia, 0 distintas de 6 603; contra la base, 0 distintas de 6 607** (también las de `BuscadorLigeroGpu.cs`: el IL de `SubeUi`,
  `RobaUiParaVentana` y `VentanaEnUso` con los nombres resueltos sale igual que en la base; frente a la referencia cambia
  `RobaUiParaVentana`, por la tanda 1: `D3D9.Release` es `Tarjeta.Suelta`). Las funciones se llaman como lo haría el código sin traducir:
  con un `Graphics` (en la referencia, la de XP; aquí, la firma de XP que deja el puente) o con el lienzo y el estado por el puente.
- **(e) Cintas de la tarjeta**: no aplica. Este grupo no cambia el dispositivo ni sus llamadas; lo que se sube (los puntos de cada pieza
  de la lista, de la franja y de la imagen de la interfaz) es lo mismo: ver los volcados de (c) y las capas de (d).
- **(f) NativeAOT**: `dotnet publish -c Release -r linux-x64 -o /root/rocola-trabajo/port/acabado-estilo/aot`: **ningún aviso IL/AOT ni de
  recorte** y los mismos 15 avisos del compilador de C# (`avisos-aot.txt` = `avisos-base-norm.txt`). El binario nativo
  (`Pruebas/VistasAcabadoEstilo/aot-corre.sh`: la pared con la franja de lejos, un disco con su lista, pedir sin crédito, el rojo mantenido
  con su guía, el buscador, con el sonido a tiempo real), de fábrica y con `glass_capas=0` (la lista en la imagen de la interfaz): **sale con
  0, 13 capturas cada vez, 0 líneas con `Exception` en lo que escribe y en `rocola_error.txt`** (solo el aviso de siempre del volumen de
  Windows y una «pantalla parada» de llvmpipe). En las capturas se ven la guía del rojo, la franja con la letra de lejos y la lista con su
  vidrio.
- **(g) diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py ... -v`): `AcabadoGpu.cs` 97 líneas quitadas / 95 puestas, `EstiloGpu.cs`
  54/52, `GuiaRojoGpu.cs` 30/28, `BuscadorLigeroGpu.cs` 2/2; **0 sin marca** (todas llevan `EN LINUX`). Números de línea iguales a la base
  (411, 259, 182 y 64) y finales de línea LF como estaban (`git diff` solo enseña las líneas cambiadas).

## Riesgos y lo que se vio

- **El `Restore` de la capa y el recorte del que llama.** En la capa (`Dibujo2D.cs`), `Graphics.Restore` además de volver al estado
  guardado olvida que había un recorte de `SetClip` (`conRecorte = false`): después, un `SetClip` que reemplaza ya no quita el de antes,
  lo cruza. En XP, `FranjaLejos` hacía `Save`/`Restore` sobre el `Graphics` de `EnsureUiBotOverlay`; traducida, guarda y vuelve el
  LIENZO (`Save`/`RestoreToCount`) y ese `Graphics` no se entera. Solo se notaría si después de la franja alguien hiciera un `SetClip` que
  reemplaza o un `ResetClip` sobre ese `Graphics` con el recuadro de `EmpiezaRecuadro` puesto: en `EnsureUiBotOverlay` no lo hace nadie
  (`DrawLabelOverlay` solo escribe; `DrawCtrlOverlay` y lo que pinta solo usan `SetClip(..., Intersect)` dentro de su `Save`/`Restore`,
  que da el mismo recorte en los dos casos). En GDI+ de verdad `Restore` no tiene ese efecto. Se va con la capa.
- **`GraphicsAcabado` fija el filtro (`Bilinear`) y la matriz del HDC (identidad)**: valen para los que llaman hoy (arriba). Si alguien
  llamara a las traducidas desde un `Graphics` con otro filtro o en un control con su propia matriz del HDC, lo llamado con ese `Graphics`
  (que hoy no pinta imágenes ni usa `TextRenderer`) no lo vería.
- **Unidades de letra que XP no usa.** `new Font(f.FontFamily, f.Size * e, f.Style, f.Unit)` traducido es `EnPuntos`/`EnPixeles` con el
  tamaño de la `Letra`: igual para `Point`, `Pixel`, `World` y `Display`; con `Inch`, `Document` o `Millimeter` el tamaño en píxeles se
  redondearía distinto (`(t*96)*e` contra `(t*e)*96`). Y la `Font` que `TextoConCintaAcabado` le da a la cinta lleva `Pixel` o `Point`:
  con `World` o `Display` la clave de la cinta (solo su nombre en la caché, no lo que se pinta) diría otra unidad. Las letras de la franja
  y de la lista son `Point` y `Pixel`.
- **Un fallo de la capa, no de estos archivos, a la vista**: `HorneaDegradadoV` (`PulidoGpu.cs`, para degradados de más de 2048 de alto)
  suelta el `Bitmap` que acaba de darle al `TextureBrush`; en GDI+ el pincel tiene su propia copia, en `Dibujo2D.cs` no, y pintar con él
  fallaría (igual en la referencia y aquí). `BakeSongGlassTint` no llega ahí (la lista mide como mucho la pantalla). No se tocó.
- La traducción copia la referencia, con sus diferencias (a)-(e) frente a GDI+ de verdad (`PORT_DIBUJO.md`, D0): la franja y la barra de
  páginas escriben con `AntiAliasGridFit` (el gris de la referencia) y su texto va con traslación (e2); las páginas de la lista se copian
  punto por punto, sin escalar; no hay ClearType ni plumas de menos de un píxel aquí.

## Lo que queda

- Quitar cada puente de `Puente.acabado-estilo.cs` cuando se traduzca el archivo que lo necesita (la tabla de costuras): `Form1.Video.cs`
  (las firmas de XP de la imagen de la interfaz y de la franja, `DrawLabelOverlay`/`DrawCtrlOverlay`, las piezas `uiPieza`),
  `TextoMovilGpu.cs` (`PintaRenglonLejos(Graphics, ...)`, `PintaArtistaCompacto`, `TextoConCintaAcabado`), `Form1.Vistas.cs` (las páginas
  horneadas y `PintaPagina`), `Form1.Fondo.cs` (`RoundedPath`), `PulidoGpu.cs` (`DegradadoV`), `Form1.Pared.cs` (`CtrlDirLeft`/`Right`),
  `Form1.Buscador.cs` (`DrawExtraGpu`) y `D3D9Star.cs` (la imagen de la interfaz y las piezas como `SKBitmap`). Cuando no quede ninguna,
  se borra el archivo entero (y al final, con `Dibujo2D.cs` y `Puente.cs`).
- Las letras de las etiquetas (`Label.Font`) pasan por `PuenteDibujo.Letra` hasta que las ventanas sean nativas (pista de las ventanas).
- El volcado de las texturas para los recorridos (`VOLCADO=1`) no está en el programa desde la tanda 1: aquí se usó la copia
  instrumentada del grupo «suena». Si vuelve al programa (como las líneas de pruebas de la grabadora de cintas) lo decide quien junta.
- Las diferencias (a)-(e) de la referencia frente a GDI+ de verdad siguen como estaban (`PORT_DIBUJO.md`); corregirlas, en su función
  nativa, lo decide el dueño.
- Nada de esto se probó en el A4.

## Revisión (revisor independiente, 8-11-2026)

Se revisó el diff `b609319..port-acabado-estilo` contra `Rocola/` (XP), contra lo que hacía cada llamada en la capa (`Dibujo2D.cs`) y
contra `estado_por_linea.tsv`, y se repitieron las comprobaciones sin fiarse del informe. **No se encontró ningún fallo en el código**: la
traducción hace, línea por línea, las mismas llamadas a las funciones nativas que hacía la capa, con el mismo estado y en el mismo orden.
Solo se corrigió documentación (abajo). Carpeta de trabajo de la revisión: `/root/rocola-trabajo/port/acabado-estilo/revision2`.

### Lo mirado en el código

- **Lo que queda de GDI+** (grep de `Graphics`, `Bitmap`, `Font`, `Brush`, `Pen`, `GraphicsPath`, `StringFormat`, `TextRenderer`,
  `ImageAttributes`, `LockBits`, `Dibujo2D` en los cuatro archivos): solo las costuras de la tabla, escritas enteras; ningún
  `using Rocola.Dibujo2D`.
- **Cada llamada contra la capa**: `DrawImage(..., ImageAttributes)` es `Imagenes.Pinta` con `Envoltura.Transparente` (la de fábrica de
  `ImageAttributes`, `Clamp`) y la clave baja y la alta del mismo color, o con la matriz de color; `DrawImageUnscaled(img, x, y)` es el trozo
  `(0, 0, ancho, alto)` en `(x, y, x + ancho, y + alto)`; los `Fill*` y `Draw*` son `Pixel.*` con el mismo suavizado, medio píxel y copia
  (`FillPolygon` con `Alternate`); `Pen(color[, ancho])` y `SolidBrush` son un `SKPaint` con lo mismo que les ponía la capa (`Pen.Pon`:
  unión `Miter`, puntas `Butt`, sin guiones, que son los de fábrica de un `SKPaint`); `LinearGradientBrush(caja, a, b, 115)` es
  `Degradado.Lineal` (no escalable, `Repeat`); `new Font(GenericSansSerif, px, Bold, Pixel)` es `Letra.EnPixeles(Letras.SinRemates, px,
  true)`; `new Font(f.FontFamily, f.Size * e, f.Style, f.Unit)` da la misma `Letra` que la capa en `Point`, `Pixel`, `World` y `Display`
  (las etiquetas de la franja son `Bold(tamaño)`, en puntos); `TextRenderer.MeasureText(s, f)` es `TextoGdi.Mide(s, letra)` (`Abajo`, caja
  sin fin); `FormatFlags = NoWrap` deja solo `SinPartir`, `|= NoWrap` y `&= ~LineLimit` (`SoloEnteros`) igual, el `Clone()` del tipográfico
  es `new FormatoTexto(TipograficoCompartido)` y las medidas usan el MISMO objeto compartido; `SetClip(caja, Intersect)` dentro del `Save`
  es `ClipRect(Intersect)` sin suavizar, que quita el `RestoreToCount`; la `ColorMatrix` es `MatrizColor.Identidad()` con `[0][0]`,
  `[1][1]` y `[2][2]`. Las imágenes nulas o soltadas no pintan o lanzan lo mismo que en la capa.
- **El estado**: el de la tabla de arriba, línea por línea (en las 8 líneas de `EstiloGpu.cs` que varían solo cambia el recorte, que va en
  el lienzo). `CtrlDirLeft`/`CtrlDirRight` (`Form1.Pared.cs`) no le cambian el estado al `Graphics` (solo `FillPolygon` y `DrawPolygon`
  con su pincel y su pluma), así que el de `GraphicsAcabado` les basta; lo que llama `FranjaLejos` con `gg` tampoco lo deja cambiado
  (`GeneroView` pone `AntiAlias` dentro del `Save`/`Restore` de `DrawCtrlOverlay`).
- **El riesgo del `Restore` de la capa** (arriba, «Riesgos»), comprobado en lo que pinta `EnsureUiBotOverlay` después de la franja
  (`DrawLabelOverlay(lblTime)`; `DrawCtrlOverlay` de `pastCred`, `pastCola` y `progress`): `PastillaView` solo usa `SetClip(...,
  Intersect)` dentro de su `Save`/`Restore` y `ProgressView` ningún recorte, así que el recorte con que pintan es el mismo con el `Graphics`
  que se entera del `Restore` (la referencia) y con el que no (la traducción): no cambia ningún punto. (El `SetClip(pt)` que reemplaza de
  `DiscoVivoGpu.cs` es del `Graphics` propio de `PintaBoleto`.)
- **Costuras**: las piezas (`uiPieza`) van con `Entrega` (un `Bitmap` dueño y nuevo cada vez, como el `new Bitmap` de XP; el préstamo con
  identidad no hace falta); nada se suelta dos veces ni se queda sin soltar (el `SKBitmap` del horno pasa a la pieza; el del vidrio y los de
  las filas los suelta `SueltaAcabadoGpu`; `hornoPag` y `hornoAnt` solo se prestan); el `SKPaint` del velo solo lo usa el hilo de la
  pantalla y `Pixel` le devuelve lo que le pone.
- **La nota de la tarea** decía que a `EstiloGpu.cs` lo llaman `AtraccionGpu.cs` (~771, ~906), `BuscadorPortadasGpu.cs` (~512, ~521),
  `Form1.Ajustes.cs` (~286, ~311) y `Form1.Vistas.cs` (~791, ~1782, ~1841): esas líneas no llaman a ninguna función de este grupo; usan
  `StringFormat.GenericTypographic`, el tipográfico compartido, que `EstiloGpu.cs` mide con el MISMO objeto. Lo de estos archivos solo lo
  llaman `Form1.Video.cs` y `TextoMovilGpu.cs` (y `Form1.Buscador.cs` a `DrawExtraGpu`, `Form1.Vistas.cs` a `SueltaAcabadoGpu`), por los
  puentes.

### Lo encontrado y lo hecho

- (menor, documentación) La tabla del estado mezclaba la numeración de `Rocola/` con la de `Xp/Codigo` (la de la tabla de estado por
  línea): «101 y 113» y «(356)» son 99 y 113 y 354 en `Xp/Codigo/AcabadoGpu.cs`, y la copia de la 113 es `copiaCopy` (la del `Graphics`
  nuevo de la fila), no la `copia` que se devuelve en el `finally`. Corregido en la tabla.
- En el código, nada que arreglar.

### Comprobaciones repetidas

- **(a) Compila**: `dotnet build -c Release --no-incremental`: 0 errores y 15 avisos; la base `b609319` compilada aparte (`git archive`)
  da los mismos 15, por archivo y línea (diff vacío).
- **(b) Banco** (Congelado y Nuevo compilados de nuevo): semillas 7 y 11 con las letras de XP falsas y 5 con DejaVu, ejecutores `gdi`,
  `nativo` y `puente`: **0 distintos de 27 000**; `puente-objetos`: **407 bien, 0 mal** (`Nativo/`, `Dibujo2D.cs` y `Puente.cs` son los de
  la base byte a byte; el banco no compila `Puente.acabado-estilo.cs`).
- **(c) Recorridos** (la copia instrumentada de esta rama con `instrumenta-volcado.sh` del grupo «suena», siempre con el candado):
  - los nueve, con la máquina compartida (carga de 4,6 a 6,8 en 4 núcleos: otras revisiones y la junta compilando y corriendo a la vez):
    capas de Dibujo2D quietas **216 de 217** (la otra, `hojas` c18 capa4, es una hoja de Ctrl+F3, que estos archivos no pintan; repetida: 37
    de 37) y texturas quietas 449 de 487 (358 de 381 distintas). Con la máquina así cambia hasta lo que pasa: en `buscador` se abrió otro
    disco (el del video) y en `video` la pantalla del video llegó 3 s tarde (a los 14,1 s y no a los 11,1), con el reloj de la franja en
    otro segundo;
  - por eso, **A/B**: la base `b609319` (instrumentada igual) y esta rama, una justo después de la otra con el mismo candado, por
    `buscador`, `video`, `ayuda`, `hojas`, `pared` y `pared2`: texturas quietas distintas **la base 279 de 304, esta rama 298 de 304**;
    capas 136 de 137 las dos (la misma c18 de `hojas`). En esa pasada fue la base la que se apartó en `buscador` (a los 27,5 s no abrió
    ningún disco); esta rama dio justo las de la referencia al abrir el disco (la imagen de la interfaz `70a892bd…`, las páginas `3f0fd9e9…`
    y `1332c023…`, las filas `6c2d6060…`, `602f424e…` y `f80538a4…` y las de los 29 a los 32 s);
  - juntando todas las corridas de esta traducción (las del grupo y las de la revisión: de 2 a 5 por pantalla), **380 de las 381 texturas
    quietas distintas salen en alguna**; la que no (`e843916c…`, `buscador` a los 8,5 s) es la barra del buscador con la «l» escrita (en el
    PNG de otra corrida, la de los 8,6 s es esa barra con «la»), que tampoco sale en la base: no es de estos archivos;
  - **el disco sin piezas** (`disco.sh`: `RenderTransparentGpuTint` con el vidrio y con el velo, y `RenderOpaqueGpu`, un recorrido sin
    reloj): esta rama contra las dos de la referencia: capas **60 de 60**, texturas quietas 99 de 100 (una resubida de 512x256 menos), **72
    de 72 distintas y 0 que no estén en la referencia**; la base en la misma pasada: 97 de 100 y 72 de 72; las 97 de la base salen idénticas
    en esta rama.
- **(d) Arnés** (`compara.sh`, el programa entero con la referencia, la base y esta rama): 6 607 líneas por juego de letras, 0 excepciones;
  **contra la referencia 0 distintas de 6 603, contra la base 0 de 6 607**, con las letras de XP falsas y con DejaVu. No es trivial: 276
  huellas distintas en 887 renglones, 229 en 297 tarjetas, 269 en 300 guías, 321 en 378 líneas de listas, 129 en 164 de franjas.
- **(e) Cintas**: no aplica (no cambia el dispositivo ni lo que se sube).
- **(f) NativeAOT**: `dotnet publish -c Release -r linux-x64`: 0 avisos IL/AOT, los mismos 15 de C#; `aot-corre.sh` de fábrica y con
  `glass_capas=0`: salida 0, 13 capturas cada vez, 0 `Exception` en lo que escribió y en `rocola_error.txt` (solo el apunte del volumen de
  Windows). En las capturas, la guía del rojo (c18) y la lista con el vidrio pintado en la imagen de la interfaz (c12, `glass_capas=0`).
- **(g) diff contra XP**: `diff-xp.py -v`: 97/95, 54/52, 30/28 y 2/2, 0 sin marca; con `git diff -U0`, todas las líneas puestas llevan
  `// EN LINUX`; 411, 259, 182 y 64 líneas y finales LF como la base; fuera de lo permitido no se tocó nada (`Nativo/`, `Dibujo2D.cs`,
  `Puente.cs`, `AGENTS.md`, `LEEME.md`, `ETAPA2.md` y Consola, sin cambios).
- **Con la rama principal de ahora** (`f2f9dcb`, que ya trae el grupo «reposo»: `TextoMovilGpu.cs`, `FranjaLigeraGpu.cs` y
  `EstadoReposoGpu.cs` traducidos, y las teclas de ventanas2): `git merge-tree` sin conflictos (`GuiaRojoGpu.cs` cambió allí en otras
  líneas, `Keys` → `Tecla`); lo juntado compila (0 errores, 15 avisos) y el arnés de este grupo compilado con lo juntado da **0 distintas
  de la referencia (6 603) y de esta rama (6 607)**, con las dos letras.

### Lo que queda (para quien junte)

- Con «reposo» ya en la rama principal, el renglón del artista de la franja pasa por puentes de los dos grupos: `FranjaLejos` (traducida)
  → `PintaArtistaCompacto(gg)` (el puente de `Puente.reposo.cs`) → `PintaRenglonLejosReposo` → `PintaRenglonLejos(Graphics, ...)` (el de
  `Puente.acabado-estilo.cs`) → `PintaRenglonLejos` traducida → `TextoConCintaAcabado` → `TextoConCinta(Graphics, ...)` (el de
  `Puente.reposo.cs`) → `TextoConCinta` traducida. Da lo mismo (el arnés con lo juntado, 0 distintas), pero al juntar se puede llamar
  directo: en `FranjaLejos`, `PintaArtistaCompacto(g, ajuste, copia, medio)`; en `PintaArtistaCompacto`, `PintaRenglonLejos(g, suave,
  ajuste, copia, medio, ..., PuenteDibujo.Letra(lblNow.Font), ...)`; en `PintaRenglonLejos`, `TextoConCinta(g, ajuste, texto, f, c, r,
  sf)`; y quitar `TextoConCintaAcabado`, `PintaRenglonLejos(Graphics, ...)` y `PintaRenglonLejosReposo` si nadie más los usa.
- Lo demás de «Lo que queda» de arriba sigue igual. Nada probado en el A4.
