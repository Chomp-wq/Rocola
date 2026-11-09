# Port nativo de XP: la atracción (el escaparate y lo más pedido esta noche), de GDI+ a SkiaSharp directo

Grupo «atraccion» de la tanda 2 del port nativo (el dibujo con GDI+ de los archivos de la capa de la tarjeta), 8-11-2026: `AtraccionGpu.cs`.
Decisiones del dueño en `AGENTS.md` (8-11-2026: «que tradujeras todo el código para que trabajara nativamente en Linux», «ningún layer de
traducción») y la receta en `PORT_DIBUJO.md` (D2-D4, `Pruebas/BancoDibujo/EjecutaNativo.cs`). Base: `f2f9dcb`; rama `port-atraccion`. Aquí va
lo que se tradujo, el estado del `Graphics` de cada función, cada costura con lo que todavía no está traducido (con su razón y cuándo se
quita), las comprobaciones con sus números y lo que queda. Nada de esto se probó en el A4.

## Qué se tradujo

`AtraccionGpu.cs` ya no ve los tipos que imitan a GDI+ (`using Rocola.Dibujo2D` fuera: `using System.Drawing; using Rocola.Nativo; using
SkiaSharp;` en la misma línea de antes); los de la capa solo quedan escritos enteros en las costuras de abajo. Todo su dibujo va con SkiaSharp
(`SKCanvas`, `SKPaint`, `SKBitmap`, `SKPath`, `SKShader`) y las funciones nativas (`Nativo/`), línea por línea como en XP: cada línea que cambia
lleva su `// EN LINUX: ...` (159 líneas cambiadas, las 159 con su marca), el archivo conserva su número de líneas (1 217: donde XP tenía un
`using` de un `StringFormat` o de un `Graphics` que solo medía, la línea queda como comentario `EN LINUX` o como la llave del bloque) y sus
finales de línea (LF, sin BOM). La lógica de XP no se tocó: mismos valores, mismo orden, mismas ramas.

| Función | Lo que dibuja | Cómo quedó |
|---|---|---|
| `Letra(px)` | la letra de la atracción (Trebuchet MS en negrita, en píxeles) | `Rocola.Nativo.Letra.EnPixeles(Letras.Resuelve("Trebuchet MS"), px, true)`: la familia como la resuelve GDI+ (Microsoft Sans Serif si no está). El tipo con su espacio entero: el método se llama `Letra` |
| `HorneaFichaVitrina` | la ficha del escaparate: «EN ESTA ROCOLA», el nombre (hasta tres renglones, con la letra achicada si hace falta), el artista y las pastillas | `SKBitmap` (`Mapas.Nuevo`), `SKCanvas`; las letras son `Letra` (`GetHeight()` es `AltoRenglon`, `Size` es `Tamano`), los formatos `FormatoTexto`; el `Graphics` de 1×1 que XP hacía solo para medir no hace falta (medir no pide lienzo: queda su modo de letra en una variable) |
| `EspaciadoEntero` | el letrero espaciado, dibujado en un mapa aparte y pegado desde su primera fila con tinta | `(SKCanvas, ajuste, ...)`; el mapa aparte es un `SKBitmap`; el `DrawImage` con `SourceOver` y `NearestNeighbor` es `Imagenes.Pinta` con `Filtro.Vecino` y sin copia (XP los ponía y los devolvía: aquí van en variables, el estado de quien llama no se toca) |
| `Tinta` | dónde hay algo de tinta (alfa > 0) | `SKBitmap`; el `LockBits(ReadOnly, Format32bppArgb)` es `Pixeles.LeeSinPremultiplicar` a una memoria aparte (como la copia que da GDI+ de un mapa PArgb) y el `UnlockBits`, soltar esa memoria |
| `Espaciado` | las letras una a una con aire entre ellas | `(SKCanvas, ajuste, ...)`; `new StringFormat(StringFormat.GenericTypographic)` es `new FormatoTexto(FormatoTexto.TipograficoCompartido)` (abajo) y `|= MeasureTrailingSpaces`, `|= ContarEspacios` |
| `Pastilla`, `PastillaGenero` | las pastillas (género, pedidas, canciones) | `(SKCanvas, suave, ajuste, ...)`: el trazo redondo por el puente (abajo), `Pixel.Rellena` y `Pixel.Traza` (la pluma de 1,4 px: un `SKPaint` con su color y su ancho) |
| `Renglon` (las dos) | un renglón que cabe en su caja: achica la letra y acorta por palabras | `(SKCanvas, ajuste, ..., Letra, SKPaint, ..., FormatoTexto)`; `new Font(f.FontFamily, f.Size * esc, f.Style, f.Unit)` es la misma `Letra` a ese tamaño, en su unidad (`EnPixeles` o `EnPuntos`, con su negrita, cursiva y subrayado) |
| `EscalaQueCabe` (las dos), `AcortaPorPalabras`, `AcortaCita`, `DisponPastillas`, `AnchoPastilla` | solo miden | del `Graphics` solo cuenta su modo de letra: reciben `AjusteLetra` |
| `TextoCentrado` | un texto centrado en su caja | `(SKCanvas, ajuste, ...)` |
| `HorneaMensajes` | los mensajes que se turnan, uno por renglón | `SKBitmap`, `SKCanvas`, `Letra`, `FormatoTexto` |
| `HorneaLaLista` | pide las portadas de las cinco | `SKBitmap[]`: los puntos de cada `Image` de `LoadCoverByPath` por el puente (abajo) |
| `HorneaTop` | la lista de lo más pedido: la luna, «Lo más pedido esta noche», cuántas, y cada fila (puesto, portada o tapa, título, artista, carril y «N veces») | `SKBitmap`, `SKCanvas`, `Letra`, `FormatoTexto`; el filtro de las portadas (`HighQualityBilinear`) en una variable |
| `Luna` | la luna en creciente | dos `SKPath` (par-impar, el `Alternate` de fábrica de un `GraphicsPath`); la `Region` con `Exclude` es `SKPath.Op(..., Difference)` sobre una copia (si Skia no puede, se queda como estaba, como en la capa) y `FillRegion`, `Pixel.RellenaRegion` (de píxeles enteros, sin suavizar) |
| `Tapa` | la portada chica de la fila (recortada en redondo) o una tapa con degradado e iniciales | `(SKCanvas, suave, ajuste, filtro, ..., SKBitmap, ...)`: `g.Save()`, `ClipPath` del trazo sin suavizar y `RestoreToCount` (el `SetClip` reemplazaba un recorte que no había); `DrawImage(portada, r)` es `Imagenes.Pinta` con el filtro de quien llama; el `LinearGradientBrush` de 45°, `Degradado.Lineal` en un `SKPaint` |
| `GpuAtraccion`, `vitBmp`, `msgsBmp`, `topBmp`, `SlotFicha`, `FichaQuad` | los mapas horneados y su subida | `SKBitmap`; `Upload` (`D3D9Star.cs`) ya los recibe así (grupo «frontera»): sin puente |
| `ParCuboAtraccion` | las portadas del cubo | no dibuja: pasa las `Image` de la vista de la atracción a quien las pide (los dos sin traducir); el tipo, escrito entero (abajo) |

**El tipográfico compartido.** `AnchoPastilla` mide con `FormatoTexto.TipograficoCompartido`, el MISMO objeto que `StringFormat.GenericTypographic`
del código sin traducir (no una copia), y `Espaciado` hace su copia de él al llamarlo (`new FormatoTexto(FormatoTexto.TipograficoCompartido)`,
lo que hace `new StringFormat(GenericTypographic)`): lo que le pone el panel de reposo (`EstadoReposoGpu.cs`: `Center`, `Center` y `NoWrap`)
lo heredan los dos, como en XP (la tabla lo dice: en los recorridos, las líneas 777, 778 y 906 ven `FMT(Center,Center,[...NoWrap...])`, y
`Espaciado` dibuja cada letra centrada en su punto; por eso `EspaciadoEntero` busca la tinta). Ninguna función de este archivo cambia el
compartido (el arnés lo comprueba: sale como entró).

**Nota sobre `EstiloGpu.cs`.** El encargo decía que este archivo llama a funciones de `EstiloGpu.cs` hacia las líneas 771 y 906; no es así
(grep de las funciones de `EstiloGpu.cs` en este archivo: ninguna). En esas dos líneas está el tipográfico compartido (771: la copia de
`Espaciado`; 906: el de `AnchoPastilla`), y por eso las dos van con `FormatoTexto.TipograficoCompartido`. No hizo falta el puente con
`EstiloGpu.cs`.

## El estado del `Graphics`

| Dónde | Suavizado | Modo de letra | Copia | Medio píxel | Filtro | Matriz y recorte | De dónde sale |
|---|---|---|---|---|---|---|---|
| `HorneaFichaVitrina` (su `Graphics`) | sí | `SuaveAjustada` | no | no | — | identidad, sin recorte | fijo: el `Graphics.FromImage` de XP y sus dos líneas (tabla, 697: `s=AntiAlias t=AntiAliasGridFit c=SourceOver i=Bilinear p=Default m=I r=no d=mapa`) |
| la medida de `HorneaFichaVitrina` (`g1`) | — | `SuaveAjustada` | — | — | — | — | fijo (tabla, 670: `t=AntiAliasGridFit`); solo mide |
| `EspaciadoEntero` | (el de quien llama: solo lo copiaba `gt`, que solo escribe) | parámetro | no (XP pone `SourceOver` para su imagen) | no | `Vecino` (XP lo pone para su imagen) | la de quien llama | tabla, 743: `s=AntiAlias t=AntiAliasGridFit c=SourceOver i=NearestNeighbor p=Default`; lo llama solo `HorneaFichaVitrina` |
| `Espaciado` (en `gt`) | — | parámetro | no | no | — | identidad | `gt` es un `Graphics` nuevo con el modo de letra de quien llama (tabla, 777-778) |
| `Pastilla`, `PastillaGenero` | parámetro | parámetro | no | no | — | la de quien llama | tabla, 791-792 y 805-806: el de `HorneaFichaVitrina`, su único llamador |
| `Renglon` | (el texto no lo mira) | parámetro | no | no | — | la de quien llama | tabla, 827: el suavizado varía (`AntiAlias` desde la ficha, `None` desde los mensajes; desde la lista, que los recorridos no pasan, `AntiAlias`), y no cuenta para el texto; el modo de letra (`AntiAliasGridFit`), la copia y el medio píxel son los mismos en los tres que lo llaman |
| las que miden (`EscalaQueCabe`, `AcortaPorPalabras`, `AcortaCita`, `DisponPastillas`, `AnchoPastilla`) | — | parámetro | — | — | — | — | tabla, 843-906: medir solo mira el modo de letra (906 varía el suavizado: `g1` y el de la ficha) |
| `TextoCentrado` | — | parámetro | no | no | — | la de quien llama | tabla, 913 (desde las pastillas); desde `Tapa`, el de `HorneaTop` |
| `HorneaMensajes` | (no lo pone: `None`) | `SuaveAjustada` | no | no | — | identidad | fijo (tabla, 833-867 desde los mensajes: `s=None t=AntiAliasGridFit c=SourceOver p=Default`) |
| `HorneaTop` | sí | `SuaveAjustada` | no | no | `BilinealAlta` | identidad, sin recorte | fijo: su `Graphics.FromImage` y sus tres líneas (no está en la tabla: ver abajo) |
| `Luna` | (`FillRegion` no lo mira) | — | no | no | — | identidad | el de `HorneaTop` |
| `Tapa` | parámetro | parámetro | no | no | parámetro | la de quien llama; pone su recorte en un `Save` | el de `HorneaTop` |

Lo que los recorridos no pasan y por eso no está en la tabla: la lista entera (`HorneaLaLista`, `HorneaTop`, `Luna`, `Tapa`, 946-1054: hacen
falta tres canciones pedidas esa noche), la cita de los mensajes (`AcortaCita` con «», 880 y 888: el mensaje de la favorita sale solo con la
lista), la letra más chica del nombre (675-676: un nombre que no cabe en dos renglones) y el renglón que no cabe de la ficha. Se razonó
desde el código de quien lo llama (arriba) y lo pasan el arnés y `top.sh` (abajo).

## Costuras con lo que no está traducido (la regla: el puente va del lado de lo que no está traducido)

Este grupo no necesitó un `Puente.atraccion.cs`: nadie de fuera llama a sus funciones de dibujo (son de este archivo; `Renglon` e `Iniciales`
del buscador son otras, de su clase), los mapas que entrega van a `Upload`, que ya recibe `SKBitmap`, y lo que recibe de lo no traducido pasa
por lo que ya hay en `Plataforma/Puente.cs`.

| Costura | Dónde | Qué se hace | Se quita cuando |
|---|---|---|---|
| `Redondo` (`PantallaVivaGpu.cs`) da un `GraphicsPath` | las pastillas, el carril de la barra y la tapa | se suelta como en XP; se pinta y se recorta con su `SKPath` (`PuenteDibujo.Ruta`, el mismo, sin copia) | `PantallaVivaGpu.cs` dé un `SKPath`: el grupo «pantalla-viva» ya lo hace en su rama (`RutaRedonda`, y `Redondo` queda en su puente, `Puente.pantalla-viva.cs`, con ESE `SKPath` dentro del `GraphicsPath`); juntados los dos, este archivo puede llamar a `RutaRedonda` y la costura se va |
| `LoadCoverByPath` (`Form1.Biblioteca.cs`) da una `Image` | las portadas de la lista (`HorneaLaLista`) | `PuenteDibujo.Mapa(img)`: sus puntos, los mismos (la que no llegó, `null`, como en XP) | `Form1.Biblioteca.cs` dé `SKBitmap` |
| `ParCuboAtraccion` pasa las `Image` del cubo | de `AttractView` (`Form1.Vistas.cs`) a `Form1.Biblioteca.cs` y `Form1.Video.cs` | no dibuja: el tipo, escrito entero (`Rocola.Dibujo2D.Image`) | se traduzcan los tres |
| `Upload` (`D3D9Star.cs`) | las fichas, los mensajes y la lista | ya recibe `SKBitmap` (grupo «frontera»): se le pasan directo | — |

## Lo que se supuso (y por qué es lo mismo)

- **Medir no pide lienzo.** XP mide con el `Graphics` que tenga a mano (uno de 1×1 en la ficha); `MeasureString` solo mira su modo de letra
  (la capa: `TextoGdiPlus.Mide` con `TextRenderingHint`), así que las funciones que solo miden reciben el modo de letra.
- **La copia y el medio píxel son los de fábrica en todo el archivo.** Ningún `Graphics` de estos hornos los pone (tres `Graphics.FromImage`
  nuevos), y `EspaciadoEntero` pone `SourceOver` para su imagen; por eso no van como parámetro (las funciones nativas los toman por defecto).
- **El recorte de `Tapa` no reemplaza nada.** `SetClip(p)` de GDI+ reemplaza el recorte de antes; quien llama (`HorneaTop`, sobre un mapa
  nuevo) no tiene ninguno, así que es poner el del trazo dentro del `Save` (el mismo que deshace `Restore`).
- **Una portada soltada.** Si `LoadCoverByPath` diera una `Image` ya soltada, el puente da `null` y la fila sale con su tapa, donde XP lanzaría
  al pintarla y la dejaría vacía (con `try/catch`). No pasa: la caché de portadas saca una portada antes de soltarla y las suelta en el reloj
  (`DrainCoverTrash`), y `HorneaLaLista` las pide y las pinta en el mismo paso del reloj (lo mismo que se supuso en el grupo «reposo»).
- **Un `SKBitmap` soltado** tumba la rocola donde un `Bitmap` soltado lanzaría (riesgo ya apuntado en `PORT_JUNTAS.md`). Aquí: `vitBmp`,
  `msgsBmp` y `topBmp` se sueltan y se ponen a `null` en el mismo paso (`AseguraFicha`, `HorneaLosMensajes`, `HorneaLaLista`,
  `SueltaHorneado`), y la tarjeta solo los lee en el cuadro que los pidió (`PreparaAtraccion` y `DibujaAtraccion`, en el hilo de la pantalla,
  `D3D9Star.cs` 2610 y 2824): el orden de XP no cambia.

## Comprobaciones

Con datos y programas en `/root/rocola-trabajo/port/atraccion` (fuera del repositorio), la máquina compartida con otros agentes (carga de 5 a
12 en 4 núcleos durante las corridas) y llvmpipe. Nada en el A4. Los guiones, en `Pruebas/VistasAtraccion`.

- **Compila** (`dotnet build -c Release`): 0 errores y los mismos 15 avisos de la base `f2f9dcb` (las dos listas ordenadas,
  `archivo(línea,columna): código`, iguales línea por línea: `SYSLIB0054` ×9, `CA2022` ×2, `SYSLIB0050`, `CA2014` y `CS1690` ×2; ninguno de
  este archivo).
- **Diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py AtraccionGpu.cs -v`): 163 líneas quitadas y 161 puestas, 2 «sin marca», que son
  dos llaves `{` que no cambiaron (831 y 934) y que el `diff` de siempre mete en un bloque cambiado; con `diff --minimal` salen 161 quitadas y
  159 puestas, **0 sin marca** (la diferencia de 2 son los `using` de XP en una línea, de D1). `git diff f2f9dcb`: 159 líneas quitadas y 159
  puestas, **las 159 con su `EN LINUX`**; el archivo sigue en 1 217 líneas, LF y sin BOM.
- **Banco por función**: no hace falta (este grupo no toca `Nativo/`, `Dibujo2D.cs` ni el puente: no hay `Puente.atraccion.cs`).
- **Arnés** (`arnes.sh`: `VistasAtraccion.csproj` y `Atraccion.cs`, el programa entero de esta copia y el de la referencia congelada con la
  misma entrada): **19 120 líneas iguales línea por línea** a la referencia, con las letras de XP falsas y con DejaVu. Son 3 610 mapas (1 458
  distintos con las letras falsas y 1 428 con DejaVu), cada uno tal cual y como lo sube la tarjeta, y lo que devuelve cada llamada:
  - la letra de la atracción (`Letra`, seis tamaños: familia, tamaño, negrita, unidad y alto del renglón);
  - 84 fichas del escaparate (siete fichas —vacía, normal, nombre que necesita la letra más chica, nombre que no cabe ni en tres renglones,
    artista que no cabe, acentos, eñe y `&`, una palabra larguísima—, en seis medidas de 40×480 a 1000×1440, con el tipográfico compartido
    de fábrica y como lo deja `EstadoReposoGpu.cs`);
  - 24 tiras de mensajes (las de XP con y sin favorita, la cita que no cabe ni al 75 %, «», una cita sin cerrar, una palabra que no cabe,
    la lista vacía; cuatro medidas) con sus anchos;
  - 30 listas de lo más pedido (cinco y tres filas; sin portadas, con portadas opaca, con alfa, de 1600×1000 y de 7×9; títulos que no caben,
    filas sin artista; la geometría de `GeometriaTop` en cinco pantallas, de 640×480 a 1920×1080) con la barra que devuelven;
  - 12 casos de `Tinta` (vacío, texto, un punto de alfa 1 en la esquina, un óvalo suave);
  - las piezas con ocho estados del `Graphics` (los tres de XP —la ficha, los mensajes y la lista— y otros cinco: los seis modos de letra,
    suavizado `None`/`AntiAlias`/`HighQuality`, filtros, traslación entera y con decimales, escala, giro y recorte), en la traducción con el
    lienzo y el estado de ese mismo `Graphics`: 15 360 medidas (`EscalaQueCabe` con el mínimo de fábrica y con 0,62, `AcortaPorPalabras` y
    `AcortaCita`; ocho letras —en píxeles, en puntos, cursiva y subrayada, una familia que no existe—, los dos formatos de XP, doce textos y
    cinco anchos, el 0 incluido), 128 de `AnchoPastilla` y `DisponPastillas`, 1 920 renglones (`Renglon` con los dos mínimos), 576
    pastillas (las dos), 192 textos centrados, 192 `Espaciado` y 384 `EspaciadoEntero` (con los dos tipográficos), 48 lunas y 160 tapas
    (sin portada y con cuatro, dos filas y dos cajas; sin recorte previo, como en `HorneaTop`);
  - el tipográfico compartido sale como entró (`Near/Near/24580/None` y, cambiado, `Center/Center/28676/None`).
  En la traducción, 68 432 llamadas directas a lo traducido (en la referencia, la llamada de XP).
- **La lista de lo más pedido en el programa entero** (`top.sh`: la atracción con un `rocola_noche.txt` de cinco canciones de la noche de
  hoy, en una carpeta fija propia, con capturas por capa y las texturas volcadas; la referencia dos veces y la traducción una, comparadas
  con `compara.py`): la textura de la lista (1024×1024, `83ddf07e…`, a los 70 s) y la de los mensajes con la favorita y su cita
  (`b97e4e77…`) **idénticas** en las tres; **28 de 28** texturas quietas distintas en la traducción y ninguna que no esté en la referencia;
  **16 de 16 capas de Dibujo2D** (las ocho capturas) idénticas; 0 excepciones en lo que escribió y en `rocola_error.txt`. La que el recuento
  da por «falta» es una ficha que salió una vez menos: cuál sale cuántas veces depende de en qué punto del ciclo de portadas entra la
  atracción (en la referencia, `51acaa…` dos veces; en la traducción, `66ebdc…` y `1da8fd…` dos veces).
- **Los recorridos** (`Pruebas/Recorridos`, con el candado y la carpeta de siempre, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra `cmp-ref` y
  `cmp-ref2`). `atraccion` sola, al principio: **27 de 27** texturas quietas distintas idénticas (las tres fichas, los mensajes y lo demás) y
  ninguna nueva, **10 de 10 capas de Dibujo2D** idénticas; la que el recuento da por «falta» es, como arriba, una ficha que salió una vez
  menos por el punto del ciclo. Los nueve (`cmp`), con 0 excepciones en lo que escribió la rocola y en `rocola_error.txt` y todas las
  capturas (12, 6, 7, 11, 5, 13, 21, 13 y 7): **las 217 capas de Dibujo2D quietas salen idénticas** (las 4 distintas, de `video`, `ctrlf3` y
  `hojas`, tampoco son iguales entre las dos de la referencia). Texturas quietas: 468 de 487 contando las veces y 366 de 381 contenidos
  distintos; `atraccion` (la única donde corre este archivo: los otros ocho no llegan a los 40 s sin tocar, y las texturas del escaparate
  solo salen en ella) **32 de 32** contando las veces, y ninguna textura nueva. Las que faltaban eran de `pared2`, `buscador` y `ayuda`, y
  son de la hora: con otra ronda de esas tres (`cmp2`) y otra de `ayuda` (`cmp3`), juntas dan **376 de 381**; `pared2` 65 de 67, `buscador`
  58 de 60, `ayuda` 55 de 56 y las otras seis enteras. Las cinco que quedan son las que ya apuntaron los grupos «reposo» y «suena» como de la
  hora: `2a697d30…` (el primer panel del estado extendido, 41,9 s) y `2f591363…` (la franja de `pared2` a los 62,4 s), que tampoco salen en
  las corridas de la referencia de hoy (`reposo/ref-hoy`, `frontera/cmp-ref3`); `8cd9108c…` (la franja de `buscador` a los 26 s), que sale
  idéntica en mis `pared`, `pared2` (las dos rondas) y `video`; `e843916c…` (un cuadro de `buscador` a los 8,5 s, entre dos letras), que
  tampoco sale en `reposo/ref-hoy`; y `a9dc2d55…` (la portadita de la ficha rápida de `ayuda` a los 29 s, `PantallaVivaGpu.cs`, que se
  hornea a los 850 ms de quedarse en un disco si cae un cuadro antes de la tecla siguiente), que la referencia corrida aquí mismo y a la
  misma hora (`ref-hoy`, con el candado) tampoco sacó (51 de sus 56 quietas), ni las del coordinador de esa media hora. En `ayuda`, la
  primera ronda siguió por otro camino desde los 21 s (texturas de 1024×64 a los 26-29 s): el mismo que tomaron a la misma hora las
  corridas del coordinador de la rama principal sin este cambio (`junta4/cmp`, `junta5/ab-nue1`: las mismas texturas nuevas,
  `0042efca…`, `745690fa…`, `910a6aca…`).
- **NativeAOT** (`dotnet publish -c Release -r linux-x64 -o /root/rocola-trabajo/port/atraccion/aot`): los mismos 15 avisos del compilador de
  la base y **ningún aviso IL ni de AOT** (0 IL2xxx/IL3xxx). El binario nativo (`aot-corre.sh`: la atracción con las teclas y capturas del
  recorrido, y la lista como `top.sh`): 5 de 5 y 8 de 8 capturas, **0 excepciones** en lo que escribió y en `rocola_error.txt`; las capturas
  enseñan el escaparate («EN ESTA ROCOLA», «Tu rocola», «Otra Banda», las pastillas «Regional» y «3 canciones», el mensaje) y la lista (la
  luna, «Lo más pedido esta noche», «12 canciones pedidas», las cinco filas con su puesto, su portada recortada en redondo, título, disco,
  carril con su barra y «5 veces»… «1 vez»).
- **Cintas de la tarjeta**: no hacen falta: lo que se sube no cambia (los mismos puntos, ahora sin el puente; el volcado de las texturas lo
  compara arriba) y no se toca nada de la tarjeta (`D3D9Star.cs`, `Nativo/`).
- **Al reanudar (9-11-2026, tras reiniciarse la máquina), sin rehacer lo de arriba**: otra vez compila con 0 errores y los mismos 15 avisos
  (la lista, igual a la de la base línea por línea); `diff-xp.py` da lo mismo (163/161 y las dos llaves; con `--minimal` y lo mecánico de la
  tarjeta, 161/159 y 0 sin marca); `compara.py` sobre las huellas guardadas de los recorridos y de `top.sh` da los mismos números (468 de
  487 y 217 de 217; 366, y con `cmp2`/`cmp3` 376, de 381 contenidos; `top`: 28 de 28 y 16 de 16).
- **Juntado con lo de los demás** (prueba local con `git merge-tree`, sin commit en ninguna rama y fuera del repositorio): con la rama
  principal de ahora (`f997e61`, que ya trae el grupo «acabado-estilo») y con `port-pantalla-viva` (que cambia `Redondo` por su puente sobre
  `RutaRedonda`), sin conflictos; ese árbol compila con 0 errores y 15 avisos, y el arnés compilado sobre él sale **igual a la referencia**
  (las 19 120 líneas, con las letras de XP falsas y con DejaVu; 68 432 llamadas directas). Con `port-buscador-portadas` y `port-ventanas2`,
  también sin conflictos (ninguna de esas ramas toca `AtraccionGpu.cs`).
- Los dos programas compilados del arnés y los PNG de las capturas del binario nativo se borraron después de comparar (quedan sus salidas y
  huellas en la carpeta de trabajo).

## Lo que queda

- Las costuras de arriba, cada una cuando se traduzca el archivo del otro lado: `PantallaVivaGpu.cs` (`Redondo`: en cuanto se junte
  «pantalla-viva», cambiar aquí las cuatro llamadas a `RutaRedonda`, que da el `SKPath`), `Form1.Biblioteca.cs` (`LoadCoverByPath`),
  `Form1.Vistas.cs`, `Form1.Biblioteca.cs` y `Form1.Video.cs` (las portadas del cubo de `ParCuboAtraccion`).
- La nota de arriba de `Plataforma/Puente.frontera.cs` pone a `AtraccionGpu.cs` entre los que llaman a la sobrecarga puente de `Upload`: ya no
  la llama (le pasa sus `SKBitmap` directo). No se tocó (es de otro grupo): la quita quien la junte.
- Las diferencias (a)-(e) de la referencia frente a GDI+ de verdad (`PORT_DIBUJO.md`, D0): la traducción copia la referencia. De este archivo
  le toca la (e1) a las portadas de la lista (`Tapa`: escaladas con `HighQualityBilinear` y el medio píxel de fábrica); no la (a) (sus mapas
  son `Format32bppPArgb`) ni la (b) (el modo de letra es `AntiAliasGridFit`).
- Nada de esto se probó en el A4.

## Revisión (revisor independiente, 9-11-2026)

Se revisó el diff `f2f9dcb..port-atraccion` contra el original de XP (`Rocola/AtraccionGpu.cs`) y contra lo que hacía la capa
(`Plataforma/Dibujo2D.cs`: cada sobrecarga de GDI+ que usaba el archivo y la función nativa a la que la capa la manda), línea por línea y
sin fiarse del informe, y se repitieron las comprobaciones (salidas en `/root/rocola-trabajo/port/atraccion/rev`, fuera del repositorio).
**No se encontró ningún fallo en la traducción, en las costuras ni en las pruebas: no se cambió código.** Solo se añade esta sección.

### Lo que se miró

- **El GDI+ que queda** (grep de `Graphics`, `Bitmap`, `Image`, `Font`, `Brush`, `Pen`, `GraphicsPath`, `Region`, `Matrix`, `StringFormat`,
  `TextRenderer`, `ImageAttributes`, `LockBits` y los modos del `Graphics`, fuera de los comentarios): solo las tres costuras de la tabla,
  escritas enteras (`Rocola.Dibujo2D.GraphicsPath` de `Redondo` en cuatro sitios, `Rocola.Dibujo2D.Image` en la firma de `ParCuboAtraccion`
  y `PuenteDibujo.Mapa` de lo que da `LoadCoverByPath`). Ningún `using Rocola.Dibujo2D` ni tipo nuevo con la forma de uno de GDI+.
- **Cada llamada contra la capa**:
  - `new Font("Trebuchet MS", px, Bold, Pixel)` es `FontFamily.DeFont` (`Letras.Resuelve`) y `Letra.EnPixeles(nombre, px, negrita)`, lo
    mismo que `Letra(px)`; `new Font(f.FontFamily, f.Size * esc, f.Style, f.Unit)` es `EnPixeles`/`EnPuntos` con la familia ya resuelta, el
    tamaño en su unidad y sus tres estilos (aquí solo llegan letras en píxeles); `GetHeight()` es `AltoRenglon` y `Size` es `Tamano`;
    `fNom != fTit` sigue siendo por identidad (`Letra` no redefine `==`).
  - `DrawString` con caja y desde un punto (la caja `(x, y, 0, 0)`) es `TextoGdiPlus.Escribe` con `copia` y `medio` en falso, y
    `MeasureString(s, f, PointF.Empty, sf)` y `(s, f, SizeF, sf, out, out)` son `Mide` con `(0, 0)` y con la caja. Los dos cortan el texto
    vacío como la capa (`Escribe` no pinta; `Mide` da 0×0 y 0 letras y renglones), y aquí puede llegar (`Iniciales` de una fila sin artista
    ni título da `""`).
  - `SolidBrush` es un `SKPaint` con su color (`SolidBrush.Pon` no pone otra cosa); `Pen(color, 1,4)`, un `SKPaint` con su color y su ancho
    (la esquina en punta y la punta plana son las de fábrica de `SKPaint`, las mismas que pone `Pen.Pon`; el mínimo de un píxel y el límite
    de esquinas 10 los pone `Pixel.Traza`); el `LinearGradientBrush(caja, a, b, 45)` es `Degradado.Lineal(caja, a, b, 45)`: los mismos
    extremos (`Extremos(caja, 45, false)`), los dos colores, la envoltura `Repeat` (el `Tile` de fábrica) y el negro opaco del `SKPaint`
    nuevo (el alfa del pincel con shader no cambia).
  - `FillPath`, `DrawPath` y `FillRegion` son `Pixel.Rellena`, `Traza` y `RellenaRegion` con `medio` y `copia` en falso; `new Region(luna)`
    con `Exclude(corte)` es la copia del trazo y `SKPath.Op(Difference)` (si no se puede, la copia sin tocar, como la capa); los dos
    `GraphicsPath` de la luna son `SKPath` par-impar con `AddOval` de la misma caja (las mismas sumas en `float`).
  - `DrawImage(img, caja)` y `DrawImage(img, Rectangle, ox, oy, ow, oh, Pixel)` son `Imagenes.Pinta` con la imagen entera o el trozo
    `(ox, oy, ox + ow, oy + oh)` (sumas de enteros), el filtro del `Graphics` (`BilinealAlta` en la lista; `Vecino` en `EspaciadoEntero`,
    que XP pone y devuelve), sin medio píxel ni copia y con la envoltura transparente de fábrica. El `try/catch` de `Tapa` se conserva (la
    capa y la traducción llegan a la misma `Pinta`).
  - `Save` + `SetClip(p)` + `Restore` de `Tapa` es `Save` + `ClipPath(p, Intersect, sin suavizar)` + `RestoreToCount`: lo que hace la capa
    (su `SetClip` que reemplaza solo quita un recorte suyo de antes, y `HorneaTop` no tiene ninguno; pone un `Save` más, que no cambia nada).
  - `LockBits(ReadOnly, Format32bppArgb)` es `Pixeles.LeeSinPremultiplicar` (la misma `Lee(..., Argb32, ...)` y el mismo paso) a una memoria
    aparte; `Tinta` solo mira el alfa.
  - `new StringFormat()` es `new FormatoTexto()` (Near, Near, sin banderas, `PorLetra`); `Trimming` es `Corte` con el mismo valor
    (`EllipsisCharacter` 3, `EllipsisWord` 4); `FormatFlags =` reemplaza (`NoWrap` = `SinPartir` = 4096, `LineLimit` = `SoloEnteros` = 8192)
    y `|= MeasureTrailingSpaces` añade (`ContarEspacios` = 2048); `new StringFormat(GenericTypographic)` es
    `new FormatoTexto(TipograficoCompartido)`, la copia, en ese momento, del MISMO objeto que cambia `EstadoReposoGpu.cs`, y `AnchoPastilla`
    mide con el compartido mismo.
- **El estado del `Graphics`** contra `estado_por_linea.tsv` (33 filas de este archivo; sus números de línea casan con la traducción): en las
  dos que varían (827 y 906) lo que varía es el suavizado, y allí solo se mide o se escribe texto, que no lo mira; 743 (`DrawImage` con
  `NearestNeighbor`), 777-778 (la copia del tipográfico con `Center`, `Center` y `NoWrap`) y las demás, con los valores fijos o los
  parámetros que lleva la traducción. Lo que la tabla no tiene (la lista entera, la cita de los mensajes, la letra más chica del nombre) sale
  de quien lo llama: `HorneaTop` (AntiAlias, AntiAliasGridFit, HighQualityBilinear, SourceOver, Default), `HorneaMensajes` y
  `HorneaFichaVitrina`.
- **Las costuras**: no hay llamadores de fuera con un `Graphics` (grep de las funciones de dibujo; el `Renglon` y el `Iniciales` del
  buscador son de `SearchOverlay`) ni llamadas a `EstiloGpu.cs` (grep de sus 21 funciones: ninguna; la nota del encargo no era cierta),
  así que no hace falta `Puente.atraccion.cs`. `Upload` recibe los mismos `SKBitmap` que antes le pasaba la sobrecarga puente de
  `Puente.frontera.cs` (`PuenteDibujo.Mapa`: el mismo objeto), no compara por identidad y no guarda ni suelta el mapa. Los mapas horneados
  los sueltan los mismos sitios que en XP (`AseguraFicha`, `HorneaLosMensajes`, `HorneaLaLista`, `SueltaHorneado`), que los ponen a `null`
  en el mismo paso; `atrCuadro` solo se lee entre `PreparaAtraccion`, que lo rehace en cada cuadro, y `DibujaAtraccion`, en el hilo de la
  pantalla, igual que `AtraccionTic` y `DrainCoverTrash` (`Form1.cs` 2387 y 2389). Las portadas de `LoadCoverByPath` salen de la caché
  (`CoverEnCache`), que las saca antes de soltarlas.
- **Archivos**: `AtraccionGpu.cs` sigue con 1 217 líneas, LF y sin BOM, como en la base; `git diff --stat f2f9dcb` solo tiene
  `AtraccionGpu.cs`, este documento y `Pruebas/VistasAtraccion` (su `obj/` lo ignora Git; `RocolaXp.csproj` quita `Pruebas/**`); los
  commits, con el autor de siempre y sus dos líneas.

### Lo encontrado

Nada que arreglar en el código ni en las pruebas. Una nota menor, de fuera del grupo y sin tocar: la cabecera de
`Plataforma/Puente.frontera.cs` sigue poniendo a `AtraccionGpu.cs` entre los que llaman a la sobrecarga puente de `Upload`, y ya no la llama
(este documento ya lo apuntaba en «Lo que queda»). Es solo un comentario de otro grupo: que lo quite quien junte.

### Las comprobaciones, repetidas por el revisor

Con la máquina compartida (carga de 4 a 7 en 4 núcleos) y llvmpipe. Nada en el A4.

- **Compila** (`dotnet build -c Release`): 0 errores y 15 avisos; la lista ordenada, idéntica a la de la base (`base-avisos.txt`).
- **Diff contra XP** (`diff-xp.py AtraccionGpu.cs -v`): 163 quitadas y 161 puestas; las 2 «sin marca» son las llaves `{` de 831 y 934, que
  no cambian respecto a la base. Con `diff --minimal` y lo mecánico de la tarjeta: 161 y 159, **0 sin marca**; `git diff f2f9dcb`: 159 y
  159, ninguna sin `EN LINUX`.
- **Arnés** (`arnes.sh`, el programa entero de esta copia y el de la referencia congelada): **IGUALES las 19 120 líneas** con las letras de
  XP falsas y con DejaVu (sha256 `c7a995d4…` y `fd79bc6f…`, los mismos de la pista); 3 610 mapas (1 458 y 1 428 distintos) y 68 432
  llamadas directas a lo traducido.
- **Recorrido `atraccion`** (con el candado y la carpeta de siempre, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra `cmp-ref` y `cmp-ref2`): salió con
  0, 5 de 5 capturas, 0 excepciones en lo que escribió y en `rocola_error.txt`. Lo QUIETO: **texturas 32 de 32** (contando las veces) y
  **capas de Dibujo2D 10 de 10**, ninguna textura que no esté en la referencia.
- **La lista de la noche** (`top.sh` con la traducción, contra las dos corridas de la referencia que dejó la pista, la misma noche
  2026-11-08): salió con 0, 8 de 8 capturas, 0 excepciones. Lo QUIETO: **texturas 30 de 30** y **capas 16 de 16**, ninguna nueva; la
  lista (`83ddf07e…`, 1024×1024) y los mensajes con la favorita (`b97e4e77…`) idénticos.
- **NativeAOT** (`dotnet publish -c Release -r linux-x64`, a la carpeta de la revisión): los mismos 15 avisos del compilador y **0 avisos
  IL/AOT**; el binario nativo (`aot-corre.sh atraccion top`): salió con 0 las dos veces, 5 de 5 y 8 de 8 capturas, 0 excepciones en lo
  que escribió y en `rocola_error.txt`; las capturas enseñan el escaparate («EN ESTA ROCOLA», «Tu rocola», «Otra Banda», «Regional»,
  «3 canciones», el mensaje) y la lista (la luna, «Lo más pedido esta noche», «12 canciones pedidas», las cinco filas con la portada
  recortada, el título achicado de la tercera, el carril con su barra y «5 veces»… «1 vez»).
- **Juntar** (`git merge-tree`, sin commit): con la rama principal de ahora (`99000e8`) y con `port-pantalla-viva`,
  `port-buscador-portadas`, `port-ventanas2` y `port-asistente`, sin conflictos.
- **Banco y cintas**: no hacen falta (no se tocó `Nativo/`, `Dibujo2D.cs`, ningún puente ni la tarjeta).
- Los programas del arnés, el binario nativo y los PNG se borraron después de comparar (quedan las salidas y las huellas).
