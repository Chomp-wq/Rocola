# Port nativo de XP: el estado extendido, el texto que se mueve y la franja, de GDI+ a SkiaSharp directo

Grupo «reposo» de la tanda 2 del port nativo (el dibujo con GDI+ de los archivos de la capa de la tarjeta), 8-11-2026: `EstadoReposoGpu.cs`,
`TextoMovilGpu.cs` y `FranjaLigeraGpu.cs`. Decisiones del dueño en `AGENTS.md` (8-11-2026: «que tradujeras todo el código para que trabajara
nativamente en Linux», «ningún layer de traducción») y la receta en `PORT_DIBUJO.md` (D2-D4, `Pruebas/BancoDibujo/EjecutaNativo.cs`). Base:
`b609319`; rama `port-reposo`. Aquí va lo que se tradujo, el estado del `Graphics` de cada función, cada costura con lo que todavía no está
traducido (con su razón y cuándo se quita), las comprobaciones con sus números y lo que queda. Nada de esto se probó en el A4.

## Qué se tradujo

Los tres archivos ya no ven los tipos que imitan a GDI+ (`using Rocola.Dibujo2D` fuera: `using System.Drawing; using Rocola.Nativo; using
SkiaSharp;` en la misma línea de antes); los de la capa solo quedan escritos enteros en las costuras de abajo. Todo su dibujo va con SkiaSharp
(`SKCanvas`, `SKPaint`, `SKBitmap`, `SKPath`) y las funciones nativas (`Nativo/`), línea por línea como en XP: cada línea que cambia lleva su
`// EN LINUX: ...`, los archivos conservan su número de líneas (446, 390 y 125: donde XP tenía varias líneas para una llamada a Windows, o un
`using` de un `StringFormat`, las que sobran quedan como comentario `EN LINUX`) y sus finales de línea (LF, sin BOM). La lógica de XP no se
tocó: mismos valores, mismo orden, mismas ramas.

| Archivo | Función | Lo que dibuja | Cómo quedó |
|---|---|---|---|
| `TextoMovilGpu.cs` | `CintasTexto.Add` | la cinta de un texto que no cabe: lo mide y lo hornea en teselas de hasta 1024 | `TextoGdiPlus.Mide`/`Escribe`, `RecorteLienzo.Limites` (VisibleClipBounds), la matriz del lienzo (Transform), `Mapas.Nuevo`; las teselas son `SKBitmap`; la copia del formato, `new FormatoTexto(format)` |
| | `PintaTextoMovil` | el texto que se mueve, o escrito tal cual si no hay cinta | `(SKCanvas, ajuste, copia, medio, ..., Letra, SKPaint, ..., FormatoTexto)`; «`brush as SolidBrush`» es «un `SKPaint` sin shader» |
| | `TextoConCinta`, `CintaVistaPrevia` | si va en cinta | `(SKCanvas, ajuste, ..., Letra, ..., FormatoTexto)` |
| | `PintaArtistaCompacto` | el artista de la franja de abajo | `(SKCanvas, ajuste, copia, medio)`; la letra de `lblNow` por su `Letra` |
| | `CintaTile.Source`, `cintaByBitmap`, `SubeCinta` | las teselas en la tarjeta | `SKBitmap` (por identidad, como el `Bitmap` de XP: `SKBitmap` no cambia `Equals` ni `GetHashCode`); `Upload` las recibe prestadas |
| `FranjaLigeraGpu.cs` | `EmpiezaRecuadro` | vacía el recuadro del reloj o la barra (transparente, copia directa) y deja el recorte puesto | `(SKCanvas, suave, medio, zona)`: `Save` + `ClipRect` de píxeles enteros (el `SetClip`), `Pixel.Rellena` con la copia en una variable |
| `EstadoReposoGpu.cs` | `RenderIdlePanelEn` (y `...Queue`, `...Layout`, `...Grande`) | el panel del estado extendido: fondo, raya, «A H O R A   S U E N A», título, artista, insignia MEJORADA, la cola con tarjetas, portaditas y números | `SKBitmap` (`Mapas.Nuevo`), `Pixel.Rellena/Linea/Traza`, `TextoGdiPlus.Escribe/Mide`, `Imagenes.Pinta` (filtro `Bilineal`, el de fábrica), `Letra.EnPixeles(Letras.Resuelve(...))`, `FormatoTexto`; `GetHeight(g)` es `AltoRenglon` y `Size` es `Tamano` |
| | `LetraTituloReposo` | la letra del título | era `FuenteTituloReposo` (devolvía una `Font`); con ese nombre sigue en el puente |
| | `DrawIdleClock` | el reloj del panel | `(SKCanvas, ajuste, w, h, time)` |
| | `UpdateIdleClock`, `UpdateIdleClockRegion` | el reloj entero o solo su trozo | `SKBitmap`; medir no pide lienzo (el primer `Graphics` de XP solo medía); el `SetClip(dirty)`, `Save` + `ClipRect` |
| | `CopiaRegionReposo` | copia la franja de abajo del panel | el `LockBits` en `Format32bppPArgb` son los puntos mismos del `SKBitmap` (premultiplicados, desde la esquina del trozo y con su paso), como en la capa; el `UnlockBits` del destino, `NotifyPixelsChanged`; el del origen (solo lectura), nada |
| | `BuildIdlePanel`, `idlePanelBmp`, `idleClockUnderlay` | el panel que se le da a la tarjeta | `SKBitmap`; `GetIdlePanel` (D3D9Star.cs) lo recibe prestado |

**El tipográfico compartido.** `numberFormat` (los números de la cola compacta) es `FormatoTexto.TipograficoCompartido`, el MISMO objeto que
`StringFormat.GenericTypographic` del código sin traducir (no una copia): la alineación `Center, Center` y el `NoWrap` que le pone el panel
(`EstadoReposoGpu.cs:415-416`) se quedan puestos para todos, como en XP, y desde ese momento lo heredan `Espaciado` de `AtraccionGpu.cs`, el
buscador de portadas y los demás, estén traducidos o no (el `using` de XP sobre el genérico no lo suelta: en GDI+ tampoco). El arnés lo
comprueba: el tipográfico sale `Near/Near/24580` antes del primer panel y `Center/Center/28676` después, igual en la referencia.

## El estado del `Graphics`

| Dónde | Suavizado | Modo de letra | Copia | Medio píxel | Matriz y recorte | De dónde sale |
|---|---|---|---|---|---|---|
| `RenderIdlePanelEn` | sí | `SuaveAjustada` | no | no | identidad, sin recorte | fijo: el `Graphics.FromImage` de XP y sus dos líneas (tabla, líneas 372-438: `s=AntiAlias t=AntiAliasGridFit c=SourceOver p=Default m=I r=no` en todas) |
| `DrawIdleClock` | (solo texto: no lo usa) | parámetro (los tres que la llaman dan `SuaveAjustada`) | no | no | en el lienzo (el recorte de `UpdateIdleClockRegion`) | tabla, líneas 195 y 197: solo cambia el recorte (`r=si`/`r=no`), que va en el lienzo; los tres que la llaman (`RenderIdlePanelEn`, `UpdateIdleClock`, `UpdateIdleClockRegion`) no tocan la copia ni el medio píxel |
| la medida de `UpdateIdleClockRegion` | — | `SuaveAjustada` | — | — | — | fijo (tabla, línea 230: `s=None t=AntiAliasGridFit`) |
| `UpdateIdleClock`, el dibujo de `UpdateIdleClockRegion` | (solo texto) | `SuaveAjustada` | no | no | su recorte (`dirty`) en un `Save` | fijo (tabla, línea 239: el `SetClip` sobre un `Graphics` nuevo) |
| `CintasTexto.Add` (medir) | — | parámetro | — | — | la del lienzo del que llama | tabla, línea 62: cambian el modo de letra (`SystemDefault` y `AntiAliasGridFit`) y la matriz (identidad y traslación) |
| `CintasTexto.Add` (las teselas) | (solo texto) | `SuaveAjustada` | no | no | traslación `-i*1024` | fijo: un `Graphics.FromImage` nuevo (tabla, líneas 100-103) |
| `PintaTextoMovil` | (solo texto) | parámetro | parámetro | parámetro | la del lienzo | tabla, línea 152: cambia el modo de letra; la copia y el medio píxel, del que llama (por el puente, los de su `Graphics`) |
| `PintaArtistaCompacto` | (no lo usa) | parámetro | parámetro | parámetro | la del lienzo | tabla, línea 174: cambia el recorte (el del recuadro de la franja) |
| `EmpiezaRecuadro` | parámetro | — | sí, solo en su relleno | parámetro | deja su recorte | tabla, líneas 100 y 103: `s=None p=Default`, la copia solo en el relleno (XP la devuelve después) |

Lo que los recorridos no pasan (la insignia MEJORADA, `EstadoReposoGpu.cs:393-400`: en la tabla no hay ninguna llamada de esas líneas) o
pasan poco (la cola vacía, las portaditas y el número de la cola ancha, 407, 422 y 438: dos llamadas cada una; `CintaVistaPrevia`; el artista
largo de `PintaArtistaCompacto`, por `PintaRenglonLejos`) se razonó desde el código de quien lo llama y lo pasa el arnés con ocho estados del
`Graphics`.

## Costuras con lo que no está traducido (la regla: el puente va del lado de lo que no está traducido)

Todo lo del puente de este grupo está en `Plataforma/Puente.reposo.cs`, en una parte por archivo (cada una entró con el commit de su archivo).

| Costura | Dónde | Qué se hace | Se quita cuando |
|---|---|---|---|
| las firmas de XP de `TextoConCinta`, `CintaVistaPrevia`, `PintaTextoMovil` y `PintaArtistaCompacto` (con `Graphics`, `Font`, `Brush`, `StringFormat`) | las llaman `BuscadorPortadasGpu.cs`, `EstiloGpu.cs`, `Form1.Buscador.cs`, `Form1.Vistas.cs`, `SuenaVivoGpu.cs` y `Form1.Video.cs` | sobrecargas delgadas: `PuenteDibujo.Lienzo(g)` y `Estado(g)`, la `Letra` de la `Font`, el pincel en un `SKPaint` (`Pincel(b, medio)`, soltado con su shader al acabar; uno nulo no pinta), el MISMO `FormatoTexto` | se traduzca cada uno de los que las llaman (las llamarán con su lienzo y su estado) |
| `PintaRenglonLejos` (`EstiloGpu.cs`) y `DrawLabelOverlay` (`Form1.Video.cs`) pintan con un `Graphics` | `PintaArtistaCompacto` | `PintaRenglonLejosReposo`, `DrawLabelOverlayReposo`: un `Graphics` sobre el mismo lienzo (`GraphicsReposo`: el estado del que llama, el filtro de fábrica y la matriz del HDC de un mapa, la identidad) | `EstiloGpu.cs` y `Form1.Video.cs` se traduzcan |
| la firma de XP de `EmpiezaRecuadro` | `EnsureUiBotOverlay` (`Form1.Video.cs`) | `g.SetClip(zona)` por el `Graphics` y después la traducida con su lienzo: el `Graphics` lleva la cuenta de su recorte como en XP (un `SetClip` que reemplaza, `ResetClip` y `Restore` lo quitan) y el recorte de la traducida, el mismo rectángulo, cae dentro y se va con él | `Form1.Video.cs` se traduzca |
| `SubeUiBot(Bitmap)` | la franja la hace `Form1.Video.cs` y la suben `Upload` y `UpdateOverlayRegion` (`D3D9Star.cs`) | el tipo escrito entero, `Rocola.Dibujo2D.Bitmap` | se traduzcan los dos |
| `Upload` y `GetIdlePanel` (`D3D9Star.cs`) piden un `Bitmap` | `SubeCinta`, `BuildIdlePanel` | `PuenteDibujo.Bitmap(sk)`: el MISMO `Bitmap` prestado para el mismo mapa (la tarjeta compara por identidad para no volver a subirlo: `idleLastBmp`); no suelta los puntos | `D3D9Star.cs` reciba `SKBitmap` |
| `LoadCoverAsync` (`Form1.Biblioteca.cs`) da una `Image` | las portaditas de la cola (`BuildIdlePanel`) | `PuenteDibujo.Mapa(img)`: sus puntos, los mismos (la huella de la firma es la del `SKBitmap`, también por identidad) | `Form1.Biblioteca.cs` dé `SKBitmap` |
| `DegradadoV` (`PulidoGpu.cs`) da un `Brush` | el fondo del panel | se suelta como en XP; se pinta con `PuenteDibujo.Pincel(degradado, false)` (su color o su shader con el medio píxel de fábrica), soltado con su shader | `PulidoGpu.cs` dé un `SKPaint` o un shader |
| `RoundedPath` (`Form1.Fondo.cs`) da un `GraphicsPath` | la insignia y las tarjetas de la cola | se suelta como en XP; se pinta su `SKPath` (`PuenteDibujo.Ruta`, el mismo, sin copia) | `Form1.Fondo.cs` dé un `SKPath` |
| las firmas de XP que llama `PrecargaPaginaGpu.cs` (la precarga del panel y su reloj) | `RenderIdlePanelEn`, `CopiaRegionReposo`, `UpdateIdleClock`, `UpdateIdleClockRegion` con `Bitmap` e `Image` | sobrecargas delgadas: los mismos puntos (`PuenteDibujo.Mapa`); el mapa que devuelve es el `Bitmap` que se dio si se volvió a usar (como XP) o, si es nuevo, `PuenteDibujo.Entrega(sk, Format32bppPArgb)`, dueño de los puntos (quien llama lo suelta con su `using`) | `PrecargaPaginaGpu.cs` se traduzca |
| `FuenteTituloReposo` (la llama `SuenaVivoGpu.cs`) | el título que se desliza | `PuenteDibujo.Font(LetraTituloReposo(w, h))`: la `Font` con los mismos datos (familia como la resuelve GDI+, tamaño en píxeles, negrita); la `Letra` de en medio se suelta | `SuenaVivoGpu.cs` se traduzca (el grupo «suena» ya lo tiene: tomará `LetraTituloReposo`) |
| `PintaTituloGrande` y `PintaMinutosCola` (`SuenaVivoGpu.cs`) pintan con un `Graphics` | `RenderIdlePanelEn` | `PintaTituloGrandeReposo`, `PintaMinutosColaReposo`: un `Graphics` sobre el mismo lienzo con el estado del que llama, las `Font` de las `Letra`, un `SolidBrush` del color de cada pincel (`PuenteDibujo.BrochaReposo`) y el MISMO `FormatoTexto` | `SuenaVivoGpu.cs` se traduzca: su firma traducida (rama `port-suena`) es la misma que la de estas dos, así que al juntar la llamada pierde el «Reposo» y se borran |
| las etiquetas de WinForms (`lblNow`, `lblMejorada`, `pastGenero`) | `PintaArtistaCompacto` | tal cual (su `Bounds`, `Text`, `TextAlign`, `ForeColor`); su `Font`, por su `Letra` (`PuenteDibujo.Letra`) | las ventanas sean nativas |

`BrochaReposo` solo sabe pasar un pincel de un color (XP solo pasa por ahí `SolidBrush`: `white`, `color` y `muted`); uno con shader lanza en
vez de pintar otra cosa. Ningún puente le cambia el estado al `Graphics` de quien llama, salvo el recorte de `EmpiezaRecuadro`, que es lo que
hace XP.

## Lo que se supuso (y por qué es lo mismo)

- **La matriz del HDC en las cintas es la identidad.** `CintasTexto.Add` toma la posición de la cinta con `source.Transform` (la matriz del
  código sobre la del HDC); la traducción usa la matriz del lienzo. Son la misma cuando la del HDC es la identidad, y lo es siempre que se
  apunta una cinta: solo con la captura puesta (`CapturaTexto`, que solo la ponen los hornos de la tarjeta, sobre mapas de bits). La tabla lo
  confirma: `b=I` en todas las llamadas de esas líneas (62, 152 y 174 de `TextoMovilGpu.cs`).
- **«`brush as SolidBrush`» es «un `SKPaint` sin shader».** Los pinceles de GDI+ que no son de un color siempre ponen su shader
  (`LinearGradientBrush`, `PathGradientBrush`, `TextureBrush`: `Nativo/Degradado.cs`); XP solo pasa `SolidBrush` a `PintaTextoMovil`.
- **La identidad de una cinta** (`Name`, `Size`, `(int)Style`, `(int)Unit` de la `Font`) se arma con los mismos valores desde la `Letra`
  (familia, tamaño, negrita 1, cursiva 2, subrayada 4; píxel 2, punto 3): las unidades y los estilos que XP usa. Solo se compara consigo
  misma.
- **Una portada soltada** (`Image` sin puntos) daría `null` por el puente y el cuadrito del color, donde XP lanzaría al pintarla: no pasa,
  porque la caché de portadas saca una portada antes de soltarla y las suelta en el reloj (`DrainCoverTrash`), nunca entre que el panel la
  pide y la pinta. La huella de la firma del panel es la del `SKBitmap` (por identidad, como la de la `Image`).
- **El `Bitmap` prestado de un `SKBitmap` ya soltado** (el panel de antes en `idleLastBmp` de `D3D9Star.cs`, cuando cambia de medida) solo se
  compara por identidad (`D3D9Star.cs`, `DibujoLigeroGpu.cs`, `PerformanceGpu.cs`), como el `Bitmap` soltado de XP.

## Comprobaciones

Con datos y programas en `/root/rocola-trabajo/port/reposo` (fuera del repositorio), la máquina compartida con otros agentes (carga de 4 a
11 en 4 núcleos durante las corridas) y llvmpipe. Nada en el A4. Los guiones, en `Pruebas/VistasReposo`.

- **Compila** (`dotnet build -c Release --no-incremental`): 0 errores y los mismos 15 avisos de la base `b609319` (las dos listas ordenadas,
  `archivo(línea,columna): código`, iguales línea por línea: `SYSLIB0054` ×9, `CA2022` ×2, `SYSLIB0050`, `CA2014` y `CS1690` ×2; ninguno de
  este grupo), y así **en cada uno de los tres commits** (cada archivo entró con su parte del puente y compila solo).
- **Diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py ... -v`): `EstadoReposoGpu.cs` 115 líneas quitadas y 113 puestas,
  `TextoMovilGpu.cs` 49 y 47, `FranjaLigeraGpu.cs` 9 y 8, **0 sin marca** en los tres (la diferencia de 2, 2 y 1 son los `using` de XP en
  una línea, de D1). `git diff b609319` solo enseña las líneas cambiadas (113, 47 y 8, las mismas quitadas que puestas); los archivos siguen
  en 446, 390 y 125 líneas, LF y sin BOM.
- **Banco por función** (`banco.sh`; este grupo no toca `Nativo/`, `Dibujo2D.cs` ni `Puente.cs`, y `Puente.reposo.cs` no entra en el banco;
  se corrió igual): semillas 7 y 11 con las letras de XP falsas y la 5 con DejaVu, 3 000 casos, ejecutores `gdi`, `nativo` y `puente` contra
  la capa congelada: **0 distintos en los 9**; `puente-objetos`: 407 bien, 0 mal.
- **Arnés** (`arnes.sh`: `VistasReposo.csproj` y `Reposo.cs`, el programa entero de esta copia y el de la referencia congelada con la misma
  entrada, que llama a lo de los tres archivos con datos fijos y da la huella de cada mapa, tal cual y como lo sube la tarjeta, y lo que
  devuelve cada llamada): **1 791 líneas iguales línea por línea** a la referencia, con las letras de XP falsas y con DejaVu. Son 126
  paneles de reposo (seis pantallas de 1024×768 a 2560×1440 y 480×437, seis contenidos —títulos que caben y que no, nulos, acentos, eñe y
  `&`, la cola de 0 a 12, la docena, las dos insignias, lo que suena en grande con su género y sus minutos, transparente, opaco, con y sin
  portadas de tres medidas y con alfa, la interfaz tramada o no—, con y sin la captura de las cintas, el mapa que se vuelve a usar del mismo
  tamaño y de otro, y 36 llamando directo a la traducida), 96 del reloj (la franja copiada, el reloj entero y por trozos con otras horas,
  más largas, vacías y nulas, el borde de antes, y 36 directo con y sin recorte) y 6 de trozos copiados a otro sitio (`CopiaRegionReposo`),
  577 del texto que se mueve (ocho estados del `Graphics` —suavizado, los seis modos de letra, copia, medio píxel, traslación entera y con
  decimales, escala, giro y recorte— × cuatro textos × seis combinaciones de pincel —de color, con alfa, degradado, textura y ninguno—,
  formato —cuatro, el compartido incluido—, caja —cuatro— y letra —tres—, sin cinta, con cinta y directo, y el formato nulo con la captura:
  `NullReferenceException` en las dos, como en XP), 384 de la cinta y 256 de la de la vista previa (con la imagen de cada tesela y la clave,
  la identidad y el sitio de cada cinta), una tanda que pasa por el reuso de cintas, las de más de 1024 (dos teselas), las de más de 256 de
  alto y el tope de 48, 288 del artista de la franja (textos que caben y que no —el de `PintaRenglonLejos`—, cuatro alineaciones, con y sin
  MEJORADA), 48 del recuadro de la franja (y lo que se pinta después con el mismo `Graphics`: dentro, con otro `SetClip`, dentro de un
  `Save`/`Restore` y tras `ResetClip`, con sus `ClipBounds`) y 6 de la letra del título; el tipográfico compartido sale cambiado igual. 260
  imágenes distintas, 218 líneas con cintas; en la traducción, 640 llamadas directas a las traducidas (en la referencia esas líneas son la
  llamada de XP otra vez).
- **Los recorridos** (`Pruebas/Recorridos`, los nueve, con el candado y la carpeta de siempre, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra las dos
  corridas de la referencia de D2). El volcado de las texturas se fue con `Direct3DaGL.cs` en la tanda 1 (desde `b609319`, `VOLCADO=1` del
  programa no vuelca nada); como hizo el grupo «suena», los recorridos corren una copia de este commit con el mismo volcado donde ahora se
  suben los puntos (`instrumenta-volcado.sh`, que solo añade eso a `Nativo/TexturasGl.cs`). Las **217 capas de Dibujo2D quietas salen
  idénticas** (y 218 de 221 en total: las 3 distintas tampoco son iguales entre las dos de la referencia). Texturas quietas: 469 de 487
  (contando las veces) y **367 de 381 contenidos distintos** en esa corrida. Las que faltan son todas de la hora (cuántos cuadros salen y
  dónde cae cada segundo: en `pared`, de 654 a 1 239 cuadros en 70 s según la corrida, también en la referencia), y se comprobó con más
  corridas: `pared`, `pared2`, `buscador` y `ayuda` otra vez (dos y tres veces) y la referencia otra vez hoy en `ayuda`, `buscador` y
  `pared2`. Juntando las corridas de esta copia, **378 de los 381** salen en su mismo recorrido, idénticos; de los otros tres, dos salen
  idénticos en otros recorridos de esta copia (`2a697d30…`, el primer panel del estado extendido, que según el segundo en que se hace sale
  de una de dos maneras, también en la referencia: en `pared` de `cmp-ref` sale la otra; y `8cd9108c…`, la franja de `buscador` a los 26 s,
  que sale en `pared`, `pared2` y `video`) y el último (`e843916c…`, la franja de `buscador` a los 8,5 s, entre dos letras tecleadas) es un
  cuadro intermedio que hoy tampoco sale con la referencia. Los de `ayuda` a los 28 s (la ficha rápida del disco, `PantallaVivaGpu.cs`, que
  se hornea a los 850 ms de quedarse en un disco si cae un cuadro antes de la siguiente tecla, al segundo; con ella su portadita y la
  franja) faltaron en tres corridas y salieron idénticos en la cuarta, y en esa la referencia perdió la de los 27 s. Ninguna excepción en lo
  que escribió la rocola ni en `rocola_error.txt` en los nueve.
- **Las zonas chicas** (lo que los recorridos no comparan: el volcado solo guarda zonas de 64×64 o más, y el reloj del panel de reposo,
  `UpdateIdleClockRegion`, sube un trozo de 118×45, y el recuadro del reloj de la franja, `EmpiezaRecuadro`, uno de 134×28):
  `referencia-chicas.sh` hace una copia de la referencia que vuelca zonas de cualquier medida (`ROCOLA_VOLCADO_MINIMO=1`) y `pared` corrió
  con las dos (la traducción, en una copia de antes de que el puente se ordenara por archivos: las mismas funciones, en otro orden en
  `Puente.reposo.cs`, y esa copia de la referencia). Zonas de menos de 64: la referencia 57 subidas (44 distintas), la traducción 55 (41
  distintas), **38 distintas iguales**; las 6 de la referencia y las 3 de la traducción que no salen en la otra son el reloj en otros
  segundos (con la imagen: «0:14», «0:01», «0:02» y «0:33 / 2:30» en la referencia; «0:15», «0:19» y «0:04 / 2:30» en la traducción, por
  dónde cayó cada segundo; `chicas.py`).
- **NativeAOT** (`dotnet publish -c Release -r linux-x64 -o /root/rocola-trabajo/port/reposo/aot`): los mismos 15 avisos del compilador de
  la base y **ningún aviso IL ni de AOT** (0 IL2xxx/IL3xxx). El binario nativo (`aot-corre.sh`, con `Pruebas/Tarjeta/guiones/corre.sh`, los
  datos de los recorridos, el sonido a tiempo real y las teclas y capturas de los recorridos) por `pared` (la franja con su reloj, las
  cintas de la lista, el estado extendido con su reloj por trozos), `buscador` y `video`: 12 de 12, 11 de 11 y 7 de 7 capturas, **0
  excepciones** en lo que escribió y en `rocola_error.txt`; la captura del estado extendido (45 s) enseña el panel con «AHORA SUENA», el
  título y el artista que se deslizan, la cola con su portadita y «en ~2 min», y el reloj «0:23 / 2:30».
- **Cintas de la tarjeta**: no hacen falta (este grupo no toca nada de lo que se sube a la tarjeta, `D3D9Star.cs` ni `Nativo/`; lo que se
  sube lo comparan los recorridos).

## Lo que queda

- Las costuras de arriba, cada una cuando se traduzca el archivo del otro lado: `D3D9Star.cs` (las teselas, el panel y la franja como
  `SKBitmap`: fuera `PuenteDibujo.Bitmap` y el `Rocola.Dibujo2D.Bitmap` de `BuildIdlePanel` y `SubeUiBot`); `Form1.Video.cs` (la franja:
  fuera la firma de XP de `EmpiezaRecuadro` y de `PintaArtistaCompacto`, y `DrawLabelOverlayReposo`); `EstiloGpu.cs`
  (`PintaRenglonLejosReposo`, y las firmas de XP de `TextoConCinta` y `PintaArtistaCompacto` que llama); `SuenaVivoGpu.cs`
  (`PintaTituloGrandeReposo`, `PintaMinutosColaReposo`, `FuenteTituloReposo`, y la firma de XP de `PintaTextoMovil` que llama);
  `PulidoGpu.cs` (`DegradadoV`); `Form1.Fondo.cs` (`RoundedPath`); `Form1.Biblioteca.cs` (`LoadCoverAsync`); `PrecargaPaginaGpu.cs` (las
  firmas de XP de `RenderIdlePanelEn`, `CopiaRegionReposo`, `UpdateIdleClock` y `UpdateIdleClockRegion`); `Form1.Vistas.cs`
  (`PintaTextoMovil`), `Form1.Buscador.cs` (`CintaVistaPrevia`) y `BuscadorPortadasGpu.cs` (`TextoConCinta`); y las etiquetas de WinForms
  (las ventanas). Al final, `Puente.reposo.cs` entero se borra con `Dibujo2D.cs` y `Puente.cs`.
- Al juntar con la rama `port-suena`: su `PintaTextoMovilSuena` (`Puente.suena.cs`) lleva a la firma de XP de `PintaTextoMovil`, que aquí
  sigue en el puente; la llamada directa a la traducida es `PintaTextoMovil(g, ajuste, copia, medio, ...)` (sin el suavizado, que el texto no
  usa). Su `PuenteDibujo.Letra(FuenteTituloReposo(...))` puede pasar a `LetraTituloReposo(...)`. Las dos de `PintaTituloGrandeReposo` y
  `PintaMinutosColaReposo` pasan a ser la llamada directa a las suyas, con la misma firma.
- Las diferencias (a)-(e) de la referencia frente a GDI+ de verdad (`PORT_DIBUJO.md`, D0): la traducción copia la referencia. De estos
  archivos les toca la (e1) a las portaditas de la cola (`EstadoReposoGpu.cs:422`, escaladas con el medio píxel de fábrica) y, solo si quien
  llama a `PintaTextoMovil` trajera una traslación con decimales, la (e2) (en los recorridos, sus líneas solo ven la identidad y
  traslaciones enteras); no la (a) (los mapas de estos archivos son `Format32bppPArgb`). El modo de letra es `AntiAliasGridFit` o el del que
  llama (en los recorridos, `AntiAliasGridFit` y `SystemDefault`; este, con lo que la referencia supone para el modo del sistema de Windows,
  como en (b2)).
- Nada de esto se probó en el A4.
