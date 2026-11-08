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

## Revisión (revisor independiente, 8-11-2026)

Se revisó el diff `b609319..port-reposo` contra los originales de XP (`Rocola/`) y contra la capa congelada que imita a GDI+ (`Dibujo2D.cs`, la
referencia de las comparaciones), línea por línea y sin fiarse del informe, y se repitieron las comprobaciones. **No se encontró ningún fallo
en la traducción ni en las costuras: no se cambió código.** Se apuntan dos riesgos que no estaban (uno de este grupo y otro de fuera).

### Lo que se miró

- **El GDI+ que queda** (grep de `Graphics`, `Bitmap`, `Image`, `Font`, `Brush`, `Pen`, `GraphicsPath`, `StringFormat`, `TextRenderer`,
  `ImageAttributes`, `LockBits` y los modos del `Graphics`, fuera de los comentarios): solo las costuras de la tabla, escritas enteras
  (`Rocola.Dibujo2D.Bitmap` de `BuildIdlePanel` y `SubeUiBot`, el `Brush` de `DegradadoV`, el `GraphicsPath` de `RoundedPath`,
  `PuenteDibujo.Bitmap` para `Upload`) y la `Font` de `lblNow` (WinForms). Ningún `using Rocola.Dibujo2D`.
- **Línea por línea contra XP**: mismos valores, mismo orden y mismas ramas. Lo que es fácil perder está bien: `numberFormat` es el MISMO
  objeto que `StringFormat.GenericTypographic` del código sin traducir (`Dibujo2D.cs` lo envuelve sin copiarlo) y se cambia donde XP (líneas
  415-416); `FormatFlags =` es `Banderas =` (deja solo esa) y `|=` añade; `new StringFormat(line)` y `format.Clone()` son
  `new FormatoTexto(...)`, la misma copia que hace la capa; `MeasureString(s, f, int, sf)` mide en `(ancho, 999999)`; sin formato se pasa
  `null`, que `TextoGdiPlus` toma como `SinFormato` (lo mismo que la capa); `GetHeight(g)` es `AltoRenglon` y `Size` es `Tamano` (la capa no
  hace otra cosa); las plumas de XP (`Pen(color)` y `Pen(color, 2)`: esquina en punta, punta plana, sin guiones) son un `SKPaint` con su
  color y su ancho, los mismos valores que pone `Pen.Pon` (el mínimo de un píxel y el límite de esquinas los pone `Pixel`); un `SolidBrush`
  solo pone su color (`SolidBrush.Pon`) y el degradado de `DegradadoV` va por `PuenteDibujo.Pincel(b, false)`, el mismo `Pon(p, centros)` que
  hace la capa al rellenar con el medio píxel de fábrica; `DrawImage(imagen, caja)` es `Imagenes.Pinta` con `Bilinear`, sin copia ni medio
  píxel y la envoltura transparente de fábrica; el `LockBits` en `Format32bppPArgb` de la capa es el puntero a los puntos mismos (sin mirar
  la caja, igual que la traducción) y su `UnlockBits` de escritura, `NotifyPixelsChanged`; la identidad de la cinta: ningún archivo de XP
  usa otra unidad que `Pixel` y `Point` ni `FontStyle.Strikeout` (grep en `Rocola/`), así que separa las mismas letras que la de XP.
- **El estado del `Graphics`** contra `estado_por_linea.tsv`: las 44 filas de los tres archivos (26 con estado, todas `b=I` y `d=mapa`). Las
  que varían (`TextoMovilGpu.cs` 62: modo de letra y matriz; 152: modo de letra; 174 y `EstadoReposoGpu.cs` 195/197: el recorte) van como
  parámetro o en el lienzo. Las líneas que la tabla no tiene (la insignia, 386-402, y las llamadas de 383, 385 y 427-431) salen del
  `Graphics` de `RenderIdlePanelEn` (AntiAlias, AntiAliasGridFit, SourceOver, Bilinear, Default). Las cuatro funciones sin traducir que se
  llaman con un `Graphics` nuevo sobre el mismo lienzo (`PintaRenglonLejos`, `DrawLabelOverlay`, `PintaTituloGrande` y `PintaMinutosCola`)
  solo miden, escriben, rellenan y trazan (ni `SetClip`, ni `Save`/`Restore`, ni transformaciones, ni cambian un modo): ese `Graphics` nuevo
  es equivalente al de XP. El suavizado `true` que pasa `PintaArtistaCompacto` es el que tienen sus dos llamadores (`EnsureUiBotOverlay` y
  `FranjaLejos`), y el texto no lo usa. Todo esto corre en el hilo de la pantalla (también el precalentado de `PrecargaPaginaGpu.cs`).
- **El recorte de `EmpiezaRecuadro`**: la traducida deja en el lienzo un `Save` más con el mismo rectángulo que el `SetClip` que el puente pone
  antes por el `Graphics`. Se siguió lo que ese `Graphics` puede hacer después (`SetClip` que reemplaza, `ResetClip`, `Save`/`Restore`,
  `TranslateTransform`, `ClipBounds`) con las cuentas de la capa (`baseCuenta`, `RestoreToCount`): los mismos píxeles con y sin ese `Save`
  (y el arnés lo pinta: 48 líneas del recuadro).
- **Costuras**: el `Bitmap` prestado del panel es el mismo objeto para el mismo `SKBitmap`, y `D3D9Star.cs` solo compara el de antes por
  identidad (`SubeEstadoExtendido`: `ReferenceEquals` corta antes de leer `Width`; `DibujoLigeroGpu.cs` y `PerformanceGpu.cs`, `!= null`); los
  prestados no sueltan los puntos y lo de `Entrega` sí (la precarga lo suelta con su `using`); las portadas son de la caché. `SKBitmap` no
  cambia `Equals` ni `GetHashCode` (comprobado por reflexión: los de `System.Object`, y la huella no cambia al soltarlo), así que
  `cintaByBitmap` y la firma del panel van por identidad como con el `Bitmap` de XP. `filaReposo` y la captura de cintas son `ThreadStatic`
  y `PuenteDibujo.Bitmap` lleva su cerrojo. Las firmas traducidas de `PintaTituloGrande` y `PintaMinutosCola` de la rama `port-suena` son
  las de `PintaTituloGrandeReposo` y `PintaMinutosColaReposo`.
- **Finales de línea y número de líneas**: 446, 390 y 125 (las de la base), LF y sin BOM; `Puente.reposo.cs` y este documento, LF. Fuera de lo
  permitido no se tocó nada (`git diff --stat b609319`: los tres archivos, `Plataforma/Puente.reposo.cs`, `Pruebas/VistasReposo` y este
  documento); los commits, con el autor de siempre y sus dos líneas.

### Riesgos que se añaden

- **Un `SKBitmap` ya soltado tumba la rocola** (nuevo con la traducción). Usar un `Bitmap` soltado en XP lanza una excepción, que se puede
  atrapar (`ArgumentException`; en la capa, `NullReferenceException`); con SkiaSharp 3.116.1, `Width` o `GetPixels()` de un `SKBitmap`
  soltado es una violación de segmento (comprobado con un programa aparte: sale con 139). Aquí los leen `CintaTile.Source.Width`
  (`CintaVisible`, `DibujaCintasTexto`), la subida de una tesela (`PuenteDibujo.Bitmap` y `Upload`), `idlePanelBmp`/`idleClockUnderlay`
  (`Width` y `CopiaRegionReposo`) y las portadas (`Imagenes.Pinta`). En los flujos de XP no pasa: cada vez que se suelta una cinta cambia
  `CintasTexto.Version` (`End`, la de antes en `Add`, `Dispose` con cintas) y en cada cuadro los horneados (`GetIdlePanel`, `GetUi`,
  `GetUiBot` y `GetPieza`, `D3D9Star.cs` 2584-2666) van antes de `PreparaCintasTexto` (2673), que rehace la lista antes de dibujarla (2848,
  2882 y `CintasConLaVentana`); el panel y la tira de debajo de su reloj (`idleClockUnderlay`) se sueltan solo al cambiarlos (en el mismo
  paso en que se hace el nuevo) y al cerrar, y el prestado viejo solo se compara; las portadas las suelta `DrainCoverTrash` en el hilo de la
  pantalla, fuera del repintado (`Form1.cs:2389`), nunca entre `LoadCoverAsync` y el dibujo. Quien cambie el orden del cuadro o traduzca
  `D3D9Star.cs` tiene que conservarlo: un descuido así ya no sería una excepción, sino la rocola cerrada.
- **Fuera de este grupo (no se arregla aquí): `HorneaDegradadoV`** (`PulidoGpu.cs:174-177`) suelta el mapa justo después de hacer el
  `TextureBrush`, como XP. En GDI+ vale (el `TextureBrush` copia la imagen), pero el de la capa (`Dibujo2D.cs`) guarda la `Image` y hace su
  shader al pintar: `SKShader.CreateBitmap(null)` lanza `ArgumentNullException` (comprobado). Solo con degradados de más de 2 048 píxeles de
  alto, y desde este grupo no se llega (`IdlePanelHeight` da como mucho 340); le toca a quien traduzca `PulidoGpu.cs` (o a la capa).

### Las comprobaciones, repetidas por el revisor

Salidas en `/root/rocola-trabajo/port/reposo/revision` (fuera del repositorio), con la máquina compartida (carga de 3 a 7 en 4 núcleos).

- **Compila** (`dotnet build -c Release --no-incremental`, en `1dfa3da`): 0 errores y 15 avisos, la lista ordenada idéntica a la de la base.
- **Diff contra XP** (`diff-xp.py -v`): 115/113, 49/47 y 9/8, **0 sin marca**; `git diff --numstat b609319`: 113/113, 47/47 y 8/8.
- **Arnés** (`arnes.sh`, el programa entero de esta copia y el de la referencia congelada): **1 791 líneas iguales** con las letras de XP
  falsas y con DejaVu (640 llamadas directas a las traducidas).
- **Banco** (`banco.sh`): semillas 7 y 11 (letras falsas) y 5 (DejaVu), 3 000 casos, ejecutores `gdi`, `nativo` y `puente`: **0 distintos en
  los 9**; `puente-objetos`: 407 bien, 0 mal.
- **Recorridos** (una copia de `1dfa3da` con el volcado de las texturas, `instrumenta-volcado.sh`; con el candado y la carpeta de siempre,
  `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra `cmp-ref` y `cmp-ref2`): `pared`, `pared2` y `video`, 12 de 12, 6 de 6 y 7 de 7 capturas, 0
  excepciones en lo que escribió y en `rocola_error.txt`. **Capas de Dibujo2D quietas: 52 de 52 idénticas** (las 2 distintas de `video`,
  `c21`, tampoco son iguales entre las dos de la referencia). Texturas quietas: 226 de 231 (160 de 165 distintas) en la primera ronda; una
  segunda ronda de `pared` y `pared2` dio 168 de 181 (en `pared` faltaron cuatro de los últimos cuadros, a los 69-70 s —la captura de
  los 70 s cierra la rocola— y dos franjas, a los 13 y 17,5 s, que sí salieron en la primera). Juntando las dos rondas, `pared` 58 de 58
  y `pared2` 66 de 67 distintas; la que falta (`2f591363…`, la franja de `pared2` a los 62,4 s) sale idéntica en tres corridas del traductor
  (`cmp2/pared`, `cmp2/pared2` y `cmp3/pared2`, el mismo código), y la de `video` (`faffed06…`, 512×256 a los 9,7 s) sale idéntica en mis
  `pared` y `pared2` (a los 15,8 y 15,6 s) y en el `video` del traductor: todo lo quieto sale idéntico en alguna corrida de la traducción, y
  lo que falta en una corrida es de la hora.
- **Las zonas chicas** (con `ROCOLA_VOLCADO_MINIMO=1`, `pared` en la traducción y en una copia de la referencia hecha aquí con
  `referencia-chicas.sh`, las dos con el candado): la referencia subió 52 (39 distintas), la traducción 53 (41 distintas), **35 distintas
  idénticas**; las 4 que solo salen en la referencia y las 6 que solo salen en la traducción son el reloj en otros segundos (vistas: «0:10»,
  «0:27», «0:33» y «0:04 / 2:30» en la referencia; «0:13», «0:14», «0:23», «0:24», «0:25» y «0:27 / 2:30» en la traducción, en el recuadro
  de la franja, 134×28, o en el del panel, 118×45).
- **Cada commit compila por separado** (`df74d26`, `0143d42` y `e05873d`, sacados con `git archive`, `--no-incremental`): 0 errores y
  los 15 avisos de la base en los tres.
- **NativeAOT** (`dotnet publish -c Release -r linux-x64`, con `nice 19`): los 15 avisos del compilador de la base y **ningún aviso IL ni
  de AOT**. El binario nativo por `pared`, `buscador` y `video` (`aot-corre.sh`, con el candado): 12 de 12, 11 de 11 y 7 de 7 capturas,
  0 excepciones en lo que escribió y en `rocola_error.txt`; la captura del estado extendido (45 s) enseña el panel con «AHORA SUENA», el
  título y el artista, la cola con su portadita, «02 Ángel de amor (versión ñand…» y «en ~2 min», y el reloj.
- **Cintas de la tarjeta**: no hacen falta (no cambia nada de lo que se sube).
