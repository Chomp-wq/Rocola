# Port nativo de XP: la barra de páginas y la barra de letras (`Form1.Vistas.cs`, líneas 1-997), de GDI+ a SkiaSharp directo

Grupo «vistas-a» de la tanda 3 del port nativo (el dibujo con GDI+ de lo que quedaba), 11-11-2026. Decisiones del dueño en `AGENTS.md`
(8-11-2026: «que tradujeras todo el código para que trabajara nativamente en Linux», «ningún layer de traducción») y la receta en
`PORT_DIBUJO.md`. Base: `ae8b50b`; rama `port-vistas-a`. Aquí va lo que se tradujo, cada costura con lo que todavía no está traducido (con
su razón y cuándo se quita), las comprobaciones con sus números y lo que queda. Nada de esto se probó en el A4.

## Qué se tradujo

La primera parte de `Codigo/Form1.Vistas.cs`, hasta el final de la clase `LetterBar` (línea 997). De la 998 en adelante es del grupo
vistas-b (en paralelo): no se tocó.

| Clase | Lo que dibuja | Cómo quedó |
|---|---|---|
| `SongPagerBar` | la barra de páginas de la lista de canciones: el fondo, «N / M» (con el letrero que viaja al cambiar de página) y las dos flechas con su texto | `RenderContent(SKCanvas g, bool suave, bool copia, bool medio)`; `Pixel.Rellena`, `TextoGdiPlus.Mide/Escribe`, la letra guardada es una `Letra` (`EnPuntos(Letras.SinRemates, 9.5, negrita)`) |
| `LetterBar` | la barra de letras: el riel con sus dos degradados, las letras grabadas (tres pasadas), la estrella de lo más sonado, «VIDEO», las cuentas, la cápsula de neón que viaja, el marcador dorado y la lupa | `RenderContent(SKCanvas g, AjusteLetra ajuste, bool copia, Filtro filtro, bool medio[, bool forGpu])`; las dos tiras guardadas (`stripCache`, `lupaCache`) son `SKBitmap` (`Mapas.Nuevo`: BGRA premultiplicado) con su `SKCanvas`; `Degradado.Lineal` en un `SKPaint`, `Pixel.Rellena/Traza/Linea/RellenaPoligono`, `Imagenes.Pinta`, `TextoGdiPlus`, `FormatoTexto`; lo guardado entre cuadros (pinceles, plumas, rutas, letras) es `SKPaint`, `SKPath` y `Letra` |
| `TextoTramo`, `CacheTramos` | (no dibujan: no tenían GDI+) | sin cambios |
| `BarraAvance` | la barra de avance de Ctrl+F3 | **no se tocó aquí**: la pista de ventanas ya la tradujo en esas mismas líneas (rama `port-ventanas2`: pieza de la hoja, `Pixel.Rellena`); tocarla aquí habría hecho chocar las dos ramas |

193 líneas cambiadas, cada una con su `// EN LINUX: ...`. El archivo conserva sus 2 749 líneas (las mismas que en la base) y sus finales
(CRLF en 2 568 líneas, igual que la base: `git diff` enseña solo las 193). La lógica de XP no se tocó: mismos valores, mismo orden,
mismas ramas, las mismas claves de las tiras guardadas. En las líneas 53-997 no queda ningún tipo de GDI+ salvo lo que es de WinForms y
no toca a esta tanda (`OnPaint(PaintEventArgs e)` con `e.Graphics`, y `this.Font` del control, que se lee por `PuenteDibujo.Letra`). El
`using Rocola.Dibujo2D` del archivo se queda: lo necesita el resto del archivo (vistas-b y lo de ventanas).

Detalles de la traducción que pesan:

- `RoundRectF` da un `SKPath` con relleno par-impar (el `Alternate` de fábrica de un `GraphicsPath`) y sus cuatro arcos como en GDI+: el
  primero empieza la figura (`ArcTo(..., true)`), los otros tres se unen con una línea al punto anterior (`false`), y `Close()`.
- `Agrandado` (la lupa) guarda y devuelve la matriz del lienzo (`Save`/`RestoreToCount`) con las tres transformaciones de XP en su orden;
  el resto del estado va en variables y la acción no lo cambia.
- `AnchoDe` mide con `FormatoTexto.TipograficoCompartido`, el MISMO objeto que `StringFormat.GenericTypographic` (con lo que otros le
  hayan cambiado). `sfCen` es un `FormatoTexto` nuevo con `Banderas = SinPartir` (con `=`: solo esa).
- `EscalaDe` usaba `g.DpiY`: son los 96 puntos por pulgada de la rocola en XP, escritos (`f.Puntos * 96f / 72f * .78f`).
- Las plumas son `SKPaint` con su ancho y su unión (`StrokeJoin.Round` donde XP ponía `LineJoin.Round`); `Pixel.Traza` les pone el mínimo
  de un píxel y el límite de esquinas de GDI+.
- `CB(color)` (`Form1.Pared.cs`, un `SolidBrush` guardado al que se le pone el color en cada uso) pasa a un `SKPaint` de ese color en
  cada uso: nadie lee el color del guardado sin ponérselo antes.

### El estado del `Graphics`

| Dónde | Suavizado | Modo de letra | Copia | Filtro | Medio píxel | De dónde sale |
|---|---|---|---|---|---|---|
| `SongPagerBar.RenderContent`: el fondo | parámetro | — | parámetro | — | parámetro | XP pinta el fondo con el suavizado que trae el `Graphics` |
| `SongPagerBar.RenderContent`: lo demás | sí (lo pone) | `ClearTypeAjustada` (lo pone) | parámetro | — | parámetro | XP: `g.SmoothingMode = AntiAlias; g.TextRenderingHint = ClearTypeGridFit` y no los devuelve. Los recorridos no pasan estas líneas (con tarjeta la pinta `DrawSongPagerGpu`): razonado desde el código y comprobado con el arnés |
| `LetterBar.RenderContent` y `RenderLupa` | sí (lo pone) | parámetro | parámetro | parámetro (el de pegar la tira) | parámetro | XP: `g.SmoothingMode = AntiAlias` y no lo devuelve. Tabla: con tarjeta `t=AntiAliasGridFit` con traslación; por procesador `t=SystemDefault` sobre el HDC; varían `t` y `m` (líneas 467-488, 813-817) |
| las medidas (`AjustaFuentes`, `CuerpoQueQuepa`, `AnchoDe`, `EscalaDe`, `RebuildLupa`) | — | parámetro | — | — | — | el `Graphics` solo medía: pesa su modo de letra (tabla, 313/337/791: `AntiAliasGridFit` ×21/135/291 y `SystemDefault` ×1/3); los cuerpos se eligen con el del que llama la vez que toca, como en XP |
| las dos tiras guardadas (`stripCache`, `lupaCache`): `Riel`, separadores, letras, estrellas, cuentas, rayas | sí | `DelSistema` | no | — | no | fijo: un `Graphics.FromImage` nuevo con solo `SmoothingMode = AntiAlias` (tabla 421-428, 511, 539, 942, 977: `s=AntiAlias t=SystemDefault c=SourceOver p=Default`, identidad, sin recorte, en todas) |

**Lo que cada una le deja al `Graphics` sin devolverlo** (regla 3): la barra de páginas, `AntiAlias` y `ClearTypeGridFit` (si hay más de
una página y la barra mide al menos 60×10); la de letras, `AntiAlias` (si mide al menos 4×4 y tiene letras). Quien las llame directo
tiene que poner después esos valores en su estado; a quien todavía pasa un `Graphics` se los deja el puente (abajo).

## Costuras con lo que no está traducido (la regla: el puente va del lado de lo que no está traducido)

Todas en `Plataforma/Puente.vistas-a.cs` (nuevo; `SongPagerBar` y `LetterBar` pasan a `partial`, con su nota).

| Costura | Dónde | Qué se hace | Se quita cuando |
|---|---|---|---|
| las firmas de XP `SongPagerBar.RenderContent(Graphics)` y `LetterBar.RenderContent(Graphics[, bool])` | las llaman su `OnPaint` (WinForms: el dibujo por procesador) y `DrawCtrlOverlay` de `Form1.Video.cs` (líneas 1787 y 1792; grupo video-form1) | sobrecargas delgadas: `PuenteDibujo.Lienzo(g)` y `Estado(g)` a la traducida, y antes de llamarla le ponen al `Graphics` lo que XP le deja (con las mismas condiciones de salida temprana; antes, para que quede igual también si el dibujo lanza a medias) | `Form1.Video.cs` llame a la traducida con su lienzo y su estado (y ponga después lo que deja), y las ventanas sean nativas (`OnPaint`) |
| `CtrlDirLeft`, `CtrlDirRight`, `CtrlTxt` (`Form1.Pared.cs`, sin traducir) pintan con un `Graphics` (y `CtrlTxt` con una `Font`) | `SongPagerBar.RenderContent` (4 llamadas) | `CtrlDirLeftVistasA`, `CtrlDirRightVistasA`, `CtrlTxtVistasA`: un `Graphics` sobre el MISMO lienzo con el estado del que llama (`PuenteDibujo.GraphicsVistasA`: filtro de fábrica y matriz del HDC identidad, que esas tres no usan) y la `Font` de la `Letra` (soltada al acabar). Ninguna de las tres le cambia el estado al `Graphics` | `Form1.Pared.cs` se traduzca: la llamada pierde el «VistasA» y las tres se borran |
| la letra del control (`this.Font`, de WinForms) | `CuerpoQueQuepa` | `PuenteDibujo.Letra(this.Font).Familia` | las ventanas sean nativas |

No había costuras de otros grupos que dependieran de estas dos clases (las que nombran `Form1.Vistas.cs` son de `PastillaView`, del
grupo vistas-b). Las funciones de `EstiloGpu.cs` no se usan en estas líneas.

## Comprobaciones

- **a) Compila**: `dotnet build -c Release`: 0 errores y 15 avisos, los mismos de la base (los mismos archivos y códigos:
  CA2014, CA2022, CS1690, SYSLIB0050, SYSLIB0054).
- **b) El banco**: no se repitió, porque no cambia nada de lo que compila (su lista de `Compile` es fija: `Dibujo2D.cs`, `Puente.cs` y los
  `Nativo/` de siempre; no entran `Puente.vistas-a.cs` ni `Form1.Vistas.cs`). No se tocó `Nativo/`, `Puente.cs` ni `Dibujo2D.cs`.
- **c) Recorridos** (los nueve, dos corridas completas, y `ayuda` y `pared2` otras dos; con el candado; máquina muy cargada, carga 5-7 con
  4 núcleos): las 9 salen con 0 y sin excepciones en las cuatro.
  - Capas quietas: **214 de 217** en las dos corridas. Las tres que no, ninguna de este grupo: la de Ctrl+F2 de `ctrlf3` a los 40 s solo
    cambió de nombre (`capa4-hoja-...`, de la pista de ventanas) y su huella es la de la referencia (`31c56564…`); las de `hojas` a los 18
    y 19,5 s son los textos del repaso de la biblioteca (`PORT_JUNTAS.md`): la de los 18 s sale distinta en mis dos corridas entre sí, y la
    de los 19,5 s (`3dd938bd…`) sale igual en corridas de otros grupos sin este cambio (`pantalla-viva/rev/cmp-base` y `cmp-r2`).
  - Texturas quietas (487, contando las veces): 435 y 460 por corrida (la junta de la base, con menos carga, 449); por recorrido, en la
    mejor corrida de cada uno: atraccion 32/32, ayuda 64/66, botones 28/28, buscador 77/78, ctrlf3 28/28, hojas 24/24, pared 80/83,
    pared2 94/98, video 49/50. **Juntando las corridas, 482 de 487**. Las cinco que faltan: dos son la tira de la barra de letras en
    reposo, que sale idéntica en todas mis corridas pero menos veces (`c9e4fc67…` en `ayuda`, 1 de 2, igual que en la junta de la base;
    `584659ad…` en `buscador`, 3 de 4); `2f591363…` y `1f1712f1…` de `pared2` salen idénticas en mi corrida de `pared`; y `2a697d30…`
    (1024×256, el panel del estado extendido con el reloj en otro segundo) no es de este grupo y la apuntan todos los anteriores.
  - Por qué una corrida suelta tiene menos: con esta carga los recorridos se separan de la referencia por el reloj (en `ayuda`, una
    corrida no subió nada de 1024×128 a los 28,7 s y otra iba por otra pantalla desde los 24 s; al repetirlo dos veces salieron las cuatro
    huellas de ese tramo, `b652908d…`, `12cfc1b4…`, `a9dc2d55…`, `4ce1fc1e…`).
  - Texturas de la traducción que no salen en ninguna de las dos de la referencia: 79 y 93 (la junta de la base, 122). Son los cuadros
    intermedios de la cápsula que viaja (su cronómetro): en una prueba aparte (`pared` hasta los 13 s, referencia y traducción seguidas)
    27 huellas eran comunes y las distintas eran solo franjas de 1024×128 de los tres viajes (a los 11, 12 y 13 s), con distinto número
    de cuadros en cada programa (10 en la referencia, 14 en la traducción), más una de 512×256 del último instante. Esos cuadros, con el
    reloj parado, los compara el arnés.
- **d) El arnés** (`Pruebas/VistasVistasA`: `arnes.sh`): el programa entero de esta copia y el de la referencia congelada con otra entrada
  que pinta las dos barras con datos fijos y once estados del `Graphics` (los dos de la tabla y nueve más: los modos de letra, copia,
  medio píxel, filtros, traslación entera y con decimales, escala, giro y recorte), por la firma de XP (`RenderContent` con un
  `Graphics`, y `OnPaint`) y, en la traducción, también llamando directo a la traducida; da la huella de los puntos (tal cual y como los
  sube la tarjeta) y el estado que le queda al `Graphics` y al tipográfico compartido. **1 249 líneas, IGUALES a la referencia** con las
  letras de XP falsas y con DejaVu (432 + 152 + 11 + 8 de la barra de páginas, 532 + 104 + 8 de la de letras; 715 y 720 llamadas directas;
  0 casos sin conseguir el instante). Lo que depende del reloj se fija (el letrero que viaja, a 0, 1, 60, 150 y 229 ms; la cápsula que
  viaja, con el paso recién empezado).
  - Controles negativos (`control.sh`: un fallo metido a propósito en una copia; líneas distintas de 644 de letras o 603 de páginas): el
    segundo arco empieza figura 612; el riel sin suavizar 612; la tira con otro modo de letra 376; la estrella sin la copia 68; la tira
    con el filtro fijo 40; la pluma sin unión redonda 4; los cuerpos medidos con un modo fijo 292; la lupa solo a lo ancho 222; el puente
    de letras no deja el suavizado 142; el fondo de las páginas siempre suavizado 235; el letrero con el tipográfico 472; las páginas con
    otro modo de letra 492; el puente de páginas no deja el modo de letra 245.
  - Lo que el arnés NO puede ver, comprobado: la copia de la letra grabada bajo la cápsula que viaja (líneas 484-488 desde 675 y 851).
    Quitarla da 0 líneas distintas, porque con la copia puesta el relleno de la cápsula (694 y 870) reemplaza lo de debajo, la letra
    incluida (en XP igual). Con la copia del relleno quitada, la de la sombra de la letra sí cambia la salida (dos controles: 68 líneas
    distintas en los dos, con huellas de salida distintas, `f517f33a` y `be0864e2`). La de la línea 488 además pinta con un color opaco,
    donde copiar y mezclar dan lo mismo.
- **e) Las cintas**: no se repitieron: no cambia la subida a la tarjeta ni `Nativo/` (las tiras se pintan en la imagen de la interfaz, que
  sube quien no se tocó; lo que llega a la tarjeta lo comparan los recorridos, arriba).
- **f) NativeAOT**: `dotnet publish -c Release -r linux-x64`: 0 avisos IL/AOT (los 15 de siempre). El binario nativo corre fuera de pantalla
  (`aot-corre.sh`) por `pared` (12 de 12 capturas), `pared2` (6 de 6) y `botones` (7 de 7): sale con 0 y 0 excepciones en lo que escribe
  y en `rocola_error.txt`; mirada la captura de `pared` a los 13,8 s (la barra con «L», «O» elegida y «VIDEO», con sus cuentas).
- **g) `diff-xp.py Form1.Vistas.cs -v`**: en las líneas 1-997, 198 quitadas y 195 puestas, todas con su marca salvo dos `}` que el diff
  empareja mal (no cambiaron: `git diff` contra la base da 193 líneas, las 193 con «EN LINUX»). Las otras 6 sin marca que enseña son de
  la 998 en adelante (la base ya enseñaba 7 sin marca en el archivo).

## Lo que queda

- `BarraAvance` (líneas 24-52) la trae traducida la rama `port-ventanas2`.
- Al juntar: la línea 10 del archivo (los `using`) la cambian tres ramas con comentarios distintos (esta, `port-vistas-b` y
  `port-ventanas2`): chocará; vale cualquiera de las tres con el comentario junto.
- Las tres costuras de arriba, cuando se traduzcan `Form1.Video.cs` (`DrawCtrlOverlay`), `Form1.Pared.cs` (`CtrlDirLeft`, `CtrlDirRight`,
  `CtrlTxt`) y las ventanas (`OnPaint`, `this.Font`).
- Quien llame directo a las traducidas tiene que poner después lo que dejan (arriba); el arnés lo comprueba solo por el puente.
