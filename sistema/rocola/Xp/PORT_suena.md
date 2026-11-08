# Port nativo de XP: lo que suena (`SuenaVivoGpu.cs`), de GDI+ a SkiaSharp directo

Grupo «suena» de la tanda 2 del port nativo (el dibujo con GDI+ de los archivos de la capa de la tarjeta), 8-11-2026. Decisiones del
dueño en `AGENTS.md` (8-11-2026: «que tradujeras todo el código para que trabajara nativamente en Linux», «ningún layer de traducción») y
la receta en `PORT_DIBUJO.md` (D2-D4). Base: `b609319`; rama `port-suena`. Aquí va lo que se tradujo, cada costura con lo que todavía no
está traducido (con su razón y cuándo se quita), las comprobaciones con sus números y lo que queda. Nada de esto se probó en el A4.

## Qué se tradujo

`Codigo/SuenaVivoGpu.cs` ya no ve los tipos que imitan a GDI+ (`using Rocola.Dibujo2D` fuera; quedan escritos enteros solo en las
costuras de abajo). Todo su dibujo va con SkiaSharp (`SKCanvas`, `SKPaint`, `SKPath`, `SKBitmap`, `SKShader`) y las funciones nativas
(`Nativo/`), línea por línea como en XP: cada línea que cambia lleva su `// EN LINUX: ...`, el archivo conserva sus 987 líneas (las
llamadas que en XP ocupaban varias líneas dejan las sobrantes como comentario `EN LINUX`) y sus finales de línea (LF). La lógica de XP no
se tocó: mismos valores, mismo orden, mismas ramas.

| Función | Lo que dibuja | Cómo quedó |
|---|---|---|
| `HorneaAviso`, `TamanoAviso`, `PintaAviso`, `PintaIcono` | el aviso que entra por la derecha (cuatro tipos, con su icono) | `SKBitmap` (`Mapas.Nuevo`), `Letra` (`EnPuntos`), `Pixel.Rellena/Traza/TrazaOvalo/RellenaOvalo/Linea/Lineas`, `TextoGdiPlus.Escribe`, `FormatoTexto` |
| `InvitaAPoner`, `PintaInvita` | la tarjeta «Te falta un crédito» con su ranura | igual |
| `SueltaMoneda`, `TamanoMas`, `PintaMas` | el «+4 créditos» que sube | igual |
| `GpuPanel.BakeMoneda` | la moneda dorada (degradado de trazo, canto, «$») | `Degradado.DeTrazo` en un `SKPaint`, `Letra.EnPixeles(Letras.SinRemates, ...)`; se entrega como `Bitmap` (abajo) |
| `HorneaTituloReposo` | el título que no cabe, en su imagen (la tarjeta lo desliza) | `TextoGdiPlus.Mide` (sin el mapa de 1×1 que XP hacía solo para tener un `Graphics` con que medir) y `Escribe` |
| `PintaTituloGrande`, `PintaMinutosCola` | el título en una línea, el artista, la pastilla del género y los minutos de la cola del panel de reposo | `(SKCanvas, suave, ajuste, copia, medio, ...)`: el estado del que llama como parámetros |
| `PastillaView.XNumero` | dónde aterriza la moneda (mide el número de la cápsula) | `TextoGdiPlus.Mide` con el modo de letra de un `Graphics` nuevo (`DelSistema`), sin el mapa de 1×1 |
| `PreparaSuenaVivo` | sube las cinco texturas | el mapa le llega a `Upload` prestado por el puente (abajo) |

Los cuatro `struct` que se pasan a la tarjeta (`GpuAviso`, `GpuInvita`, `GpuMoneda`, `GpuReposo`) llevan `SKBitmap`; los campos de letras,
`Rocola.Nativo.Letra` (con su espacio entero: `Form1` tiene un método `Letra`).

### El estado del `Graphics`

| Dónde | Suavizado | Modo de letra | Copia | Medio píxel | De dónde sale |
|---|---|---|---|---|---|
| `PintaAviso`, `PintaIcono`, `PintaInvita`, `BakeMoneda` | sí | `SuaveAjustada` (AntiAliasGridFit) | no | no | fijo: el `Graphics.FromImage` de XP y sus dos líneas (tabla: `s=AntiAlias t=AntiAliasGridFit c=SourceOver p=Default`, igual en las 20 + 9 + 9 llamadas) |
| `PintaMas`, el título de `HorneaTituloReposo` | (solo texto: no lo usa) | `SuaveAjustada` | no | no | fijo (tabla: 9 y 4 llamadas iguales) |
| la medida de `HorneaTituloReposo` | — | `SuaveAjustada` | — | — | fijo (tabla, línea 517: `s=None t=AntiAliasGridFit`) |
| `XNumero` | — | `DelSistema` (SystemDefault) | — | — | fijo: un `Graphics.FromImage` sin tocar (no pasó por los recorridos: razonado desde el código) |
| `PintaTituloGrande`, `PintaMinutosCola` | parámetro | parámetro | parámetro | parámetro | los llama `EstadoReposoGpu.cs` (sin traducir) con su `Graphics`: el puente le pasa su lienzo y su estado (en los recorridos siempre `AntiAlias`, `AntiAliasGridFit`, sin copia, sin medio píxel, identidad, sin recorte); ninguna de las dos le cambia el estado al `Graphics` |

Ninguna de estas funciones dibuja imágenes, así que el filtro no hace falta.

## Costuras con lo que no está traducido (la regla: el puente va del lado de lo que no está traducido)

| Costura | Dónde | Qué se hace | Se quita cuando |
|---|---|---|---|
| `Redondo` (`PantallaVivaGpu.cs`) devuelve un `GraphicsPath` | `PintaAviso`, `PintaInvita` (3), `PintaTituloGrande` | `using (Rocola.Dibujo2D.GraphicsPath p = Redondo(...))` y se pinta su `SKPath` (`PuenteDibujo.Ruta(p)`, el mismo, sin copia) | `Redondo` devuelva un `SKPath` (se quita `Ruta` y el tipo) |
| la letra de `lblToast` (una etiqueta de WinForms) | `HorneaAviso`, `InvitaAPoner`, `SueltaMoneda` | `PuenteDibujo.Letra(lblToast.Font).Familia` | las ventanas sean nativas |
| `AnchoTexto(string, Font)` (`Form1.Ajustes.cs`) | `TamanoAviso`, `TamanoMas` | `AnchoTextoSuena(string, Letra)` (`Plataforma/Puente.suena.cs`): la `Font` de esa `Letra`, soltada al acabar | `AnchoTexto` mida con una `Letra` |
| `PintaTextoMovil` (`TextoMovilGpu.cs`) pinta con `Graphics`, `Font`, `Brush` y `StringFormat` | `PintaTituloGrande`, `PintaMinutosCola` | `PintaTextoMovilSuena` (`Puente.suena.cs`): un `Graphics` sobre el mismo lienzo con el estado del que llama, la `Font` de la `Letra`, un `SolidBrush` del color del pincel (`PuenteDibujo.BrochaSuena`) y el MISMO `FormatoTexto` | `TextoMovilGpu.cs` se traduzca |
| `FuenteTituloReposo` (`EstadoReposoGpu.cs`) devuelve una `Font` | `BuildReposoState` | `PuenteDibujo.Letra(...)` al recibirla (soltar la `Letra` es lo que hacía soltar la `Font`) | `EstadoReposoGpu.cs` se traduzca |
| las letras de la cápsula (`fEtq`, `fVal`, de `Form1.Vistas.cs`) | `XNumero` | `PuenteDibujo.Letra(fEtq)` al medir | `Form1.Vistas.cs` se traduzca |
| `Upload` (`D3D9Star.cs`) pide un `Bitmap` | `PreparaSuenaVivo` (4), `BakeMoneda` | `PuenteDibujo.Bitmap(sk)`: el mismo `Bitmap` prestado para el mismo mapa (los puntos no se copian); `BakeMoneda` sigue devolviendo `Rocola.Dibujo2D.Bitmap` con `PuenteDibujo.Entrega(b, Format32bppPArgb)` (el dueño de los puntos, con el formato con que XP lo hacía) porque también lo pide así `PrecargaPaginaGpu.cs`; `monedaBmp` es ese `Bitmap` | `Upload` reciba `SKBitmap` (y `PrecargaPaginaGpu.cs` se traduzca): `BakeMoneda` devuelve `SKBitmap` y se van las entregas |
| las firmas de XP que llama `PrecargaPaginaGpu.cs` (la precarga, case 6) | `TamanoAviso`, `PintaAviso`, `PintaInvita`, `TamanoMas`, `PintaMas` con `Bitmap` y `Font` | sobrecargas delgadas en `Puente.suena.cs`: los mismos puntos (`PuenteDibujo.Mapa`) y la misma `Letra` | `PrecargaPaginaGpu.cs` se traduzca |
| las firmas de XP que llama `EstadoReposoGpu.cs` | `PintaTituloGrande`, `PintaMinutosCola` con `Graphics`, `Font`, `Brush` y `StringFormat` | sobrecargas delgadas en `Puente.suena.cs`: `PuenteDibujo.Lienzo(g)` y `Estado(g)`, los pinceles en `SKPaint` (`Pincel(b, medio)`, soltados después con su shader; uno nulo no pinta, como en GDI+) y el MISMO `FormatoTexto` | `EstadoReposoGpu.cs` se traduzca (las llamará con su lienzo y su estado) |

`PintaTextoMovilSuena` hace el `Graphics` con el filtro de fábrica (el texto no lo usa) y la matriz del HDC de un mapa de bits, la
identidad: quien llama a `PintaTituloGrande` y `PintaMinutosCola` es el panel de reposo, que pinta en un mapa (con un `Graphics` de un
mapa, la traslación que el código le ponga se ve igual en `Transform`, que es lo que mira la cinta). `BrochaSuena` solo sabe pasar un
pincel de color (XP solo pasa por ahí `SolidBrush`: `color`, el del artista de `PintaTituloGrande`, y `muted`, el `tenue` de
`PintaMinutosCola`; `white` solo va al `DrawString` del título, por `PuenteDibujo.Pincel`); uno con shader lanza en vez de pintar otra cosa.

## Comprobaciones

Con datos y programas en `/root/rocola-trabajo/port/suena` (fuera del repositorio), la máquina compartida con otros agentes (carga de 6 a
11 en 4 núcleos durante las corridas) y llvmpipe. Nada en el A4.

- **Compila** (`dotnet build -c Release`): 0 errores y los mismos 15 avisos de la base `b609319` (las dos listas ordenadas, iguales línea
  por línea: `Thread.VolatileRead`, `FormatterServices`, `Control.bounds`, dos `Stream.Read`, un `stackalloc`; ninguno de este grupo).
- **Diff contra XP** (`Pruebas/Tarjeta/guiones/diff-xp.py SuenaVivoGpu.cs -v`): 172 líneas quitadas y 169 puestas, **0 sin marca** (las 3 de
  diferencia son los cuatro `using` de XP en una línea, de D1). `git diff` solo enseña las 170 líneas cambiadas; el archivo sigue en 987
  líneas y LF.
- **Banco por función** (`Pruebas/BancoDibujo`; este grupo no toca `Nativo/`, `Dibujo2D.cs` ni `Puente.cs`, y `Puente.suena.cs` no entra
  en el banco; se corrió igual): semillas 7 y 11 con las letras de XP falsas y la 5 con DejaVu, 3 000 casos, ejecutores `gdi`, `nativo` y
  `puente` contra la capa congelada: **0 distintos en los 9**; `puente-objetos`: 407 bien, 0 mal.
- **Arnés** (`Pruebas/VistasSuena`, el programa entero de esta copia y el de la referencia congelada con la misma entrada): 487 líneas
  —144 avisos (los cuatro tipos, seis textos con y sin renglón, tres anchos de pantalla, dos familias; 76 imágenes distintas), 80
  invitaciones (50 distintas, con la ranura y el radio que devuelve), 9 «+N créditos», 5 monedas (con su formato `Format32bppPArgb` y sus
  marcas), 15 títulos que se deslizan (con el que no se rehace), 108 del título grande (ocho estados del `Graphics` —suavizado, los seis
  modos de letra, copia, medio píxel, traslación con decimales, escala, giro y recorte— × 4 casos × 2 anchos, 12 más con la captura de la
  cinta de `TextoMovilGpu.cs` puesta y 32 llamando directo a la traducida), 120 de los minutos de la cola (ocho estados × 5 casos × sin
  cinta, con cinta y directo) y 6 de `XNumero`—: **iguales línea por línea** a la referencia con las letras de XP falsas y con DejaVu
  (la traducción hizo 72 llamadas directas; la referencia, ninguna). De las 96 llamadas con la captura puesta, 20 dejaron su cinta (el
  texto que no cabe), con sus teselas, su caja y su clave iguales. Pasa por todas las líneas que los recorridos no ven: los iconos de
  error y de novedad, la pastilla del género (al lado y abajo), el título que cabe, los minutos en tarjeta estrecha y sin sitio, `XNumero`.
- **Recorridos** (`Pruebas/Recorridos`, `VOLCADO=1 CAPAS=1 HUELLAS=1`, siempre con el candado y la carpeta de siempre). Con el programa tal
  cual no se vuelca ninguna textura (ver «Herramientas»): las capas de Dibujo2D de los cuatro recorridos de este grupo (pared, pared2, video,
  ayuda) salen **78 de 78 quietas idénticas** y 0 texturas. Por eso se corrieron con la copia instrumentada (`instrumenta-volcado.sh`):
  - la copia instrumentada de la BASE `b609319` contra las dos corridas de la referencia: pared 83 de 83 texturas quietas y video 50 de 50
    (la prueba de que el volcado es el mismo); con la máquina cargada, pared2 y ayuda pierden algunas del final o de un instante (las
    mismas que pierde la traducción, abajo);
  - la TRADUCCIÓN (instrumentada) contra la referencia: capas **78 de 78** quietas idénticas; texturas quietas 291 de 297 (como conjunto,
    218 de 221 distintas). Las tres que faltan no son de este archivo o son de la hora: `1f1712f1…` (pared2, 69,2 s) sale al repetir
    pared2; `2a697d30…` (pared2, 41,8 s) es el panel de reposo con el reloj en otro segundo —el mismo panel con «0:20 / 2:30» que hace la
    traducción a los 41,4 s está en la corrida `pared` de la referencia (`8fc23430…`), y el de los 62 s (`21d8fe91…`, con el artista y
    «en ~2 min», de `PintaTituloGrande` y `PintaMinutosCola`) sale idéntico—; `577a5e4c…` (ayuda, 27,5 s) es un globo intermedio del
    asistente (no de este archivo), que la base tampoco sacó con la máquina cargada. Los avisos de pared2 («Pusiste «01 Cancion Larga…»»
    y los otros dos, `c0dbfa2d…`, `0bea6c99…`, `4e71f01b…`) salen idénticos a los de la referencia;
  - un recorrido propio (`Pruebas/VistasSuena/moneda.sh`: dos monedas, pedir con y sin créditos, el estado extendido con el título que
    se desliza y otra moneda encima) con el volcado de TODAS las texturas (también las chicas, que la referencia no tiene), la traducción
    contra dos corridas de la base instrumentada: 84 de 87 texturas quietas distintas; están todas las de este archivo (el «+4 créditos»
    de 120×31, el título que se desliza de 2 048×64, los dos avisos, la invitación, la moneda y los paneles) y las 3 que faltan son
    parches de una zona que dependen del instante (dos del reloj del panel, `…_en206,211_118x45`, y uno del número que rueda en la cápsula
    de créditos, `…_en878,68_134x28`); capas 14 de 14.
  - los nueve recorridos con la traducción (instrumentada) contra las dos de la referencia: capas de Dibujo2D quietas **216 de 217**;
    texturas quietas 474 de 487 (como conjunto, 372 de 381 distintas). Todo lo que faltó es del instante: al repetir buscador, ctrlf3,
    hojas, ayuda y pared, la capa distinta (`hojas`, `c18.png.capa4`) sale idéntica a la de la referencia (`b9c547d3…`) y las texturas
    `b6b5d818…` (la de 256×256 de los 2 s), `700ae3a4…` (ayuda, 23,8 s) y `edb06a76…` (pared, 67 s) salen. Juntando las corridas de la
    traducción, **379 de las 381** texturas quietas distintas salen idénticas; las dos que no: `2a697d30…` (el panel con el reloj en otro
    segundo, arriba) y `e843916c…` (buscador, 8,5 s: la caja del buscador con «l», entre la «L» de los 8 s y la «A» de los 8,6 s; con la
    máquina cargada las dos teclas llegan juntas y esa caja intermedia no se sube; es de `BuscadorPortadasGpu.cs`, no de este archivo).
    Resumen: en ninguna corrida salió distinta una textura de `SuenaVivoGpu.cs`.
  - Sin excepciones en lo que escribió la rocola ni en `rocola_error.txt` en ninguna corrida (solo «Pantalla parada» de la máquina cargada,
    el volumen de Windows que no hay y «Sin huecos»).
- **Cintas de la tarjeta**: no aplica. Este grupo no cambia el dispositivo ni lo que se le sube (los puntos de cada textura son los
  mismos: ver los volcados), solo que `Upload` recibe el `Bitmap` prestado del mapa.
- **NativeAOT** (`dotnet publish -c Release -r linux-x64 -o /root/rocola-trabajo/port/suena/aot`): genera el código nativo con los mismos
  15 avisos de la base y **ningún aviso IL ni de AOT** (0 IL2xxx/IL3xxx). El binario nativo (`Pruebas/VistasSuena/aot-corre.sh`, con
  `Pruebas/Tarjeta/guiones/corre.sh`, los datos de los recorridos, el sonido a tiempo real y 8 capturas) pasa fuera de pantalla por la
  invitación sin créditos, las dos monedas con su «+4 créditos» y el aviso, el aviso «Pusiste «02 Ángel de amor…» · ya está sonando», el
  estado extendido con el título que se desliza («Ángel de amor» a los 60 s, «…sión ñandú) &» a los 67 s) y «Disco Largo», y una moneda
  encima: sale con 0, sin excepciones en lo que escribió ni en `rocola_error.txt`.

## Herramientas nuevas (`Pruebas/VistasSuena`, fuera del programa)

- `VistasSuena.csproj` + `Suena.cs`: el arnés. Compila el programa entero de una carpeta `Xp` (esta copia o, copiando el proyecto fuera,
  la referencia con `-p:Fuente=...`) con otra entrada que llama a lo de `SuenaVivoGpu.cs` con datos fijos y da la huella de cada mapa.
  `arnes.sh` lo compila de las dos maneras, lo corre con las letras de XP falsas y con DejaVu y compara (carpeta de trabajo: `SUENA`, de
  fábrica `/root/rocola-trabajo/port/suena`).
- `quietas.py A A2 B...`: como `compara.py`, pero las texturas quietas como CONJUNTO (sin contar cuántas veces se subieron, que depende de
  la carga de la máquina) y juntando varias corridas de B; dice cuáles faltan, con su medida y su instante.
- `moneda.sh PROG SALIDA [conserva]`: el recorrido propio de las monedas, con el volcado de todas las texturas; `aot-corre.sh`: el binario
  NativeAOT por esas mismas pantallas.
- `instrumenta-volcado.sh FUENTE DESTINO`: una copia del programa con el volcado de las texturas que se suben (`VOLCADO=1` de los
  recorridos). Hace falta porque el volcado estaba en `Plataforma/Direct3DaGL.cs` y se fue con él en la tanda 1: desde `b609319`,
  `VOLCADO=1` no vuelca nada y los recorridos solo comparan las capas de Dibujo2D (las texturas de la referencia sí están). La copia pone
  el mismo volcado donde ahora se suben los puntos (`TexturasGl.Sube`, lo que era `UnlockRect`), con los mismos puntos, el mismo PNG, el
  mismo nombre y la misma regla (zonas de 64×64 o más; `ROCOLA_VOLCADO_MINIMO=n` la baja). El programa no cambia.

## Lo que queda

- Quitar las costuras de arriba a medida que se traduzcan `PantallaVivaGpu.cs` (`Redondo`), `TextoMovilGpu.cs` (`PintaTextoMovil`),
  `EstadoReposoGpu.cs` (`FuenteTituloReposo` y las llamadas a `PintaTituloGrande` y `PintaMinutosCola`), `PrecargaPaginaGpu.cs` (case 6),
  `Form1.Ajustes.cs` (`AnchoTexto`), `Form1.Vistas.cs` (las letras de la cápsula), las ventanas (`lblToast`) y `Upload` de `D3D9Star.cs`;
  cuando no quede ninguna, `Plataforma/Puente.suena.cs` se borra entero. Si al juntar los grupos alguno de esos archivos ya cambió de
  firma (por ejemplo `Redondo` devolviendo un `SKPath` o `Upload` recibiendo un `SKBitmap`), la costura de este archivo se cambia por la
  llamada directa en esa misma línea.
- El volcado de las texturas de los recorridos (`VOLCADO=1`) no existe en el programa desde la tanda 1 (se fue con `Direct3DaGL.cs`):
  aquí se suplió con una copia instrumentada de prueba (`Pruebas/VistasSuena/instrumenta-volcado.sh`); decidir si vuelve al programa
  como las líneas de pruebas de la grabadora de cintas (`ROCOLA_CINTA`) lo decide quien junta (toca `Nativo/TexturasGl.cs`, que este grupo
  no podía tocar).
- Las diferencias (a)-(e) de `PORT_DIBUJO.md` siguen como estaban (la traducción llama a las mismas funciones nativas que la capa).
- Nada se probó en el A4.

## Revisión (revisor independiente, 8-11-2026)

Se revisó el diff `b609319..port-suena` contra el original de XP (`Rocola/SuenaVivoGpu.cs`) y contra la capa (`Plataforma/Dibujo2D.cs`: cada
sobrecarga de GDI+ que usaba el archivo y la función nativa a la que la capa la manda), y se repitieron las comprobaciones (carpeta
`/root/rocola-trabajo/port/suena/revision`, fuera del repositorio). **No se encontró ningún fallo en la traducción.** Se corrigieron una
imprecisión de la documentación y un arreglo del arnés para cuando se junte con el grupo «reposo» (abajo).

Lo que se miró, línea por línea:

- Las líneas: 987 en la base y en la traducción, LF y sin BOM en las dos; 169 distintas por posición, todas con su `EN LINUX`;
  `diff-xp.py -v`: 172 quitadas, 169 puestas, 0 sin marca. Fuera de las costuras no queda nada de GDI+ (`Rocola.Dibujo2D.GraphicsPath` de
  `Redondo` en 5 sitios, el `Rocola.Dibujo2D.Bitmap` de la moneda y su `PixelFormat`), y no hay choques de nombres entre los tipos de
  `Rocola.Nativo` y miembros de `Form1` o `GpuPanel` (la `Letra` va con su espacio entero).
- Cada llamada contra lo que hacía la capa: los rellenos y trazos van a `Pixel.*` con `(suave, medio, copia)` en el mismo orden;
  `DrawString` con caja a `TextoGdiPlus.Escribe(..., copia, medio)` y desde un punto con la caja `(x, y, 0, 0)` y sin formato (`null` =
  `SinFormato`, como la capa); `MeasureString(s, f)` y `(s, f, PointF, sf)` a `Mide` con `(0, 0)`; `Font.Height`, `GetHeight()` y
  `GetHeight(g)` a `AltoEntero` y `AltoRenglon`; `new Font(familia, pt, Bold)` y `(..., Pixel)` a `Letra.EnPuntos`/`EnPixeles` con la misma
  familia (`Letra.Familia` es el `FontFamily.Name` de la `Font`); `GenericSansSerif` a `Letras.SinRemates` sin resolver (como
  `new FontFamily(nombre, true)`); `new StringFormat()` a `new FormatoTexto()` y `new StringFormat(GenericDefault)` a una copia de
  `NormalCompartido`; `FormatFlags =` reemplaza (`NoWrap` = `SinPartir` = 4096, `LineLimit` = `SoloEnteros` = 8192) y `Trimming` pasa a
  `Corte` con el mismo valor; las puntas y uniones redondas de la pluma a `StrokeCap`/`StrokeJoin` (la capa pone una punta para las dos);
  el `PathGradientBrush` de la moneda a `Degradado.DeTrazo` con la caja del trazo (`GetBounds`), el centro corrido y sin `Blend`, en un
  `SKPaint` cuyo color no tiñe (`Pixel` pone el negro opaco bajo el shader, como `Pon`); el `GraphicsPath` a un `SKPath` par-impar con
  `AddOval` de la misma caja; `new Bitmap(w, h, PArgb)` a `Mapas.Nuevo` (lo que ya hacía la capa) y la entrega de la moneda con
  `Format32bppPArgb` y `TraeAlfa` como el `new Bitmap(n, n, PArgb)` de XP.
- El estado del `Graphics` contra `estado_por_linea.tsv` (46 líneas de este archivo, ninguna varía) y, en las que no están en la tabla,
  desde quien llama (`EstadoReposoGpu.cs`, `PrecargaPaginaGpu.cs`, `HorneaAviso`, `SueltaMoneda`): el mismo. Ni `PintaTituloGrande` ni
  `PintaMinutosCola` (ni `PintaTextoMovil`) le dejan cambiado el estado al `Graphics` del que llama, así que las sobrecargas puente no
  tienen nada que devolverle.
- Las costuras: el `Bitmap` prestado es el mismo para el mismo mapa y `Upload` no lo guarda ni lo suelta; los puntos los suelta quien los
  soltaba en XP (los mapas, `SuenaVivoGpu.cs`; la moneda entregada es dueña y la suelta quien la recibe); soltar la `Letra` del título
  que se desliza es lo que hacía soltar la `Font`; las letras que hace el puente en cada llamada (`AnchoTextoSuena`,
  `PintaTextoMovilSuena`) se sueltan al acabar y no son las del que llama; el `Graphics` de `PintaTextoMovilSuena` ve la misma matriz
  (`Transform` = la del lienzo, que es lo que apunta la cinta), los mismos límites del recorte (`VisibleClipBounds` sale del lienzo) y el
  mismo modo de letra; todo va en el hilo de la pantalla, como en XP.

Lo corregido:

- `d826464`: el comentario de `PuenteDibujo.BrochaSuena` y este documento decían que por ahí pasan `white`, `color` y `muted`; pasan solo
  `color` y `muted` (`white` va al `DrawString` del título por `PuenteDibujo.Pincel`). Solo texto.
- `a64e8ce`: el arnés recorría las teselas de la cinta con `foreach (Bitmap b ...)`; en la rama del grupo «reposo» (que traduce
  `TextoMovilGpu.cs`) son `SKBitmap` y el arnés no compilaba al juntar las dos. Ahora las recorre como `object` (`Puntos` lee las dos).

Las comprobaciones, repetidas:

- **Compila**: 0 errores y los mismos 15 avisos de la base, también después de los arreglos; **NativeAOT**: 0 avisos IL/AOT y los mismos
  15; el binario nativo por las pantallas del grupo (`aot-corre.sh`: la invitación «Te falta un crédito», dos monedas con su «+4
  créditos», el aviso «Pusiste…» y el estado extendido con «Ángel de amor» deslizándose): salida 0, 8 capturas, sin excepciones en lo que
  escribió ni en `rocola_error.txt`.
- **Arnés**: IGUAL a la referencia, 487 líneas (72 llamadas directas), con las letras de XP falsas y con DejaVu, antes y después del
  arreglo. Y en una copia de la rama principal (`564788b`, con el grupo «frontera» ya junto) con `port-suena` y `port-reposo` juntos:
  se juntan sin conflictos, compila (0 errores, 15 avisos) y el arnés sale IGUAL a la referencia en las 487 líneas con las dos letras.
- **Recorridos** (la copia instrumentada de esta rama, con el candado): pared, pared2, video y ayuda: capas de Dibujo2D quietas **78 de
  78**; texturas quietas 282 de 297 (212 de 221 distintas) con la máquina cargada por otros. Repitiendo pared, pared2 y ayuda: capas 62
  de 62, texturas 244 de 247 (179 de 181). Juntando las corridas de la traducción (y otra de pared2 con los PNG), **las 221 texturas
  quietas distintas salen idénticas**. Lo que faltaba en cada corrida es del instante: con los PNG, el panel de reposo de los 41-42 s de
  la referencia (`8fc23430…`) y el de la traducción (`2a697d30…`) solo se diferencian en el reloj («0:20» contra «0:19», 149 puntos en
  x 230-250, y 221-232), y la referencia saca los dos según la corrida (igual `21d8fe91…` y `93f517ca…` a los 62 s). Las texturas de este
  archivo (el título que se desliza `8d443691…`, los avisos `4e71f01b…`, `c0dbfa2d…`, `0bea6c99…`) salen idénticas.
- **Recorrido de las monedas** (`moneda.sh`, todas las texturas): la traducción contra dos corridas de la base instrumentada: capas 14 de
  14; texturas quietas 82 de 85 distintas; faltan dos parches del instante (el número que rueda en la cápsula y el reloj del panel) y una
  subida de la franja de abajo a los 35 s, que en otra pareja de corridas (base y traducción una tras otra, con los PNG) sale idéntica
  en las dos (`c550e0ad…`). En esa pareja todas las texturas de este archivo son las mismas: los avisos (512×128 y 512×64), la moneda
  (128×128), el «+4 créditos» (128×32), el título que se desliza (2048×64) y el panel; lo distinto es de animaciones de otros archivos
  (la franja mientras rueda el número, y una fila del disco de `DiscoVivoGpu.cs` con hasta 5 niveles de diferencia en todo su fondo).
- **Banco** (aunque ningún archivo que compila —`Dibujo2D.cs`, `Puente.cs`, `Nativo/`, `Pruebas/BancoDibujo`— cambió en esta rama):
  semillas 7 y 11 con las letras de XP falsas y 5 con DejaVu, 3 000 casos, ejecutores `gdi`, `nativo` y `puente`: 0 distintos en los 9;
  `puente-objetos`: 407 bien, 0 mal.
- **Cintas**: no aplica (ni el dispositivo ni los puntos que se suben cambian; `Upload` recibe el `Bitmap` prestado del mapa).

Para quien junte: la rama principal ya tiene el grupo «frontera» (`Upload` recibe `SKBitmap` y la firma con `Bitmap` queda en
`Puente.frontera.cs`): las cuatro `PuenteDibujo.Bitmap(x)` de `PreparaSuenaVivo` pasan a `x`, y `BakeMoneda` puede devolver el `SKBitmap`
cuando `PrecargaPaginaGpu.cs` deje de pedir un `Bitmap`. También vuelve `VOLCADO=1` (`Nativo/VolcadoTexturas.cs`), así que
`instrumenta-volcado.sh` ya no hace falta allí. La rama «reposo» tiene `PintaTituloGrandeReposo` y `PintaMinutosColaReposo`
(`Puente.reposo.cs`) con los parámetros en el mismo orden que las traducidas de aquí: se cambian por la llamada directa;
`PintaTextoMovilSuena` pasa a la `PintaTextoMovil(SKCanvas, ajuste, copia, medio, ...)` traducida de allí y `FuenteTituloReposo` a
`LetraTituloReposo`.
