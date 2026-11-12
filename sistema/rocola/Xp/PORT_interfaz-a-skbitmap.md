# Port nativo de XP: la interfaz que se sube a la tarjeta en SKBitmap, con la moneda (grupo «interfaz-a-skbitmap»)

Oleada 5 (tanda 8), sección «5a. interfaz-a-skbitmap, con la moneda» de `PLAN_TANDA4_A_N7.md`, 12-11-2026. Base: `d05b06d`; rama
`port-interfaz-a-skbitmap`. Decisiones del dueño en `AGENTS.md` (8-11-2026: traducir el código para que trabaje nativo en Linux, sin capas
que imiten a Windows; lo idéntico se asegura por el código). Nada de esto se probó en el A4.

## Qué se hizo

Hasta aquí las imágenes que la interfaz le da a la tarjeta seguían siendo `Bitmap` de la capa que imita a GDI+ (`Rocola.Dibujo2D`), con
`PuenteDibujo.Entrega` al crearlas, `PuenteDibujo.Mapa` al pintarlas y subirlas, `PuenteDibujo.Bitmap` (el «prestado») para el panel de
reposo y el aviso, y las sobrecargas con `Bitmap` de `Puente.frontera.cs` y `Puente.portadas.cs`. Ahora todas son `SKBitmap` Bgra8888
premultiplicados —el `Format32bppPArgb` con que XP hace cada una (comprobado en `Rocola/`: `new Bitmap(w, h, PixelFormat.Format32bppPArgb)`
en `EnsureUiOverlay`, `EnsureUiBotOverlay`, `EnsureUiPieza`, `BakeMoneda`, `RenderIdlePanelEn`, el aviso, y
`uiOverlay.Clone(…, Format32bppPArgb)` en la instantánea)—, y quien las pide, las compara o las sube recibe ese mismo objeto:

| Dónde | En XP | Ahora |
|---|---|---|
| `Form1.Video.cs` 821, 825, 1275, 1280, 1283, 1358, 1374, 1377, 1417, 1480, 1483 (y la nota de la cabecera, 10) | `Bitmap uiOverlay`, `uiOverlayOtro`, `uiBotOverlay`, `uiPieza[]`; `new Bitmap(…, PArgb)`; `Graphics.FromImage` | `SKBitmap`; `Mapas.Nuevo` (transparente, como GDI+); `new SKCanvas(mapa)` sin el puente |
| `D3D9Star.cs` 1958, 1970, 2001, 2039, 2055, 2097, 2104, 2149, 2584, 2633, 2650, 2666, 3244 (y las notas de 13, 1043-1046 y 3260) | `GpuWin.Bmp`, `GpuToast.Bmp`, `GetUi`, `GetUiBot`, `GetIdlePanel`, `GetPieza`, `idleLastBmp`, `SubeEstadoExtendido`: `Bitmap` | `SKBitmap`; **2603, 2606, 3251 y 3252 vuelven a ser las líneas de XP** (sin `PuenteDibujo.Mapa`) |
| `BuscadorLigeroGpu.cs` 18, 30 | `SubeUi(Bitmap, GpuWin)` | `SubeUi(SKBitmap, GpuWin)`; `using SkiaSharp;` en la línea de `using System.Drawing;` |
| `FranjaLigeraGpu.cs` 113 / `BuscadorPortadasGpu.cs` 1691 | `SubeUiBot(Bitmap)` / `SubePieza(int, Bitmap)` | `SKBitmap` |
| `AcabadoGpu.cs` 3, 122, 337 | `uiPieza[index] = next;` | **337 vuelve a ser la de XP** (sin `Entrega`); notas |
| `EstadoReposoGpu.cs` 43, 118, 130, 160, 181 | `Bitmap BuildIdlePanel()`, `return idlePanelBmp;` | `SKBitmap BuildIdlePanel()`; **130, 160 y 181 vuelven a ser las de XP** (sin el prestado) |
| `Form1.Ajustes.cs` 1924 | `t.Bmp = toastBmp;` | **vuelve a ser la de XP** |
| `Form1.Biblioteca.cs` 3008 | `winSnap = uiOverlay.Clone(entera, Format32bppPArgb)` | `winSnap = Mapas.Copia(uiOverlay, entera)` (la copia punto por punto, un SKBitmap nuevo; sigue dentro del `try` de XP) |
| `Form1.Datos.cs` 10, 508 | `using System.Drawing;`, `Bitmap winSnap` | `using System.Drawing; using SkiaSharp;` (fuera `using Rocola.Dibujo2D`: el archivo ya no ve la capa), `SKBitmap winSnap` |
| `Form1.Sonido.cs` 1197 | `g.Bmp = winSnap;` | sin cambio: ya era la línea de XP y ahora los dos lados son `SKBitmap` |
| `SuenaVivoGpu.cs` 17, 695, 724, 755 | `Bitmap monedaBmp`, `static Bitmap BakeMoneda`, `return b;` | `SKBitmap`; **755 vuelve a ser la de XP** (sin `Entrega`) |
| `PrecargaPaginaGpu.cs` 4, 123 | `using (Bitmap moneda = GpuPanel.BakeMoneda(96)) { }` | `using (SKBitmap moneda = …)`; la nota de la cabecera (EnSeco ya da el lienzo) |

Cuentas: en `Codigo/` no queda ningún `PuenteDibujo.Mapa`, `PuenteDibujo.Entrega` ni `PuenteDibujo.Bitmap` (en `d05b06d` eran 8 `Mapa`, 6
`Entrega` y 4 `Bitmap`, todos de la interfaz), y `D3D9Star.cs` ya no nombra `Rocola.Dibujo2D`. Diez líneas vuelven a ser idénticas
a las de XP (las de la tabla en negrita). Los 109 archivos de `Codigo/` con el mismo número de líneas y de CRLF que `d05b06d`.

### La identidad y el orden en que se sueltan

XP compara por identidad (`ReferenceEquals`) el panel de reposo (`idleLastBmp`), la instantánea (`winLastBmp` en `EnsureCover`) y la
imagen de la interfaz que se analiza en teselas (`TrabajoTeselas.Fuente`). Antes la identidad la daba el puente (el mismo `Bitmap` para el
mismo `SKBitmap`, o el mismo `SKBitmap` para el mismo `Bitmap`); ahora es la del objeto que XP guarda en su campo: la misma regla. Los
`Dispose` son los de XP, en sus mismas líneas y en el mismo orden (el `uiOverlayOtro` viejo al cambiar de modo, `uiBotOverlay` al cambiar
de medida, la pieza vieja en `EnsureUiPieza`/`EnsureSongPiece`/`SueltaPiezas`, el panel viejo en `BuildIdlePanel`, `winSnap` en
`AbortWinTrans`, el panel al cerrar). Ninguno de esos se lee después de soltarlo: el que se suelta se saca del campo en la misma línea o
ya no es el que dan `GetUi`/`GetUiBot`/`GetPieza`/`GetIdlePanel` (el riesgo de abajo).

## Costuras

| Costura | Qué | Se quita cuando |
|---|---|---|
| Quitada: `Plataforma/Puente.frontera.cs` entero | `CreateOverlay`, `CreateCoverShared`, `UpdateOverlay`, `UpdateOverlayRegion` y `GpuPanel.Upload` con `Bitmap`: ya nadie las llama | — |
| Quitada: `Plataforma/Puente.portadas.cs` entero | `TeselasEnPasos` y `SubeReponiendo` con `Bitmap` | — |
| Quitada: `PuenteDibujo.Bitmap` (y su tabla `prestados`) de `Puente.cs` | el prestado del panel de reposo y del aviso | — |
| Quitada: la de `D3D9Star.cs:2603` (`PuenteDibujo.Mapa(gw.Bmp)`) que dejó cache-portadas | `winSnap` ya es `SKBitmap` | — |
| Se queda, sin llamadores en `Codigo/`: `PuenteDibujo.Entrega` y `PuenteDibujo.Mapa` (`Puente.cs`) | `Mapa` lo usan aún `Puente.fondo.cs:24` y `Puente.pared.cs:27` (muertas, de N7) y los arneses; `Entrega` solo `PruebaPuente` del banco | N7 (con `Puente.cs`) |

Nada nuevo en `Nativo/` ni en `Plataforma/`.

## Arneses adaptados

- `Pruebas/VistasSuena/Suena.cs`: `BakeMoneda` va por reflexión (en la traducción da `SKBitmap`); su formato se escribe como el de GDI+
  que le toca (Bgra8888 premultiplicado = `Format32bppPArgb` con `HasAlpha` = `flags=2`; cualquier otro se escribe tal cual y saldría
  distinto).
- `Pruebas/BancoDibujo/EjecutaGdi.cs` (ejecutor `puente`): el `Bitmap` prestado lo hace él (`new Bitmap(Mapas.Nuevo(w, h), f) { Ajeno =
  true }`, lo mismo que daba `PuenteDibujo.Bitmap` con un mapa nuevo). `PruebaPuente.cs`: fuera las 5 comprobaciones del prestado (de 407
  a 402).
- `Pruebas/VistasFrontera/Frontera.cs`: solo la nota; con `puente` busca la sobrecarga con `Bitmap` por reflexión y, si no hay, la de
  `SKBitmap`, así que ahora las dos puertas van por la misma.
- `VistasAcabadoEstilo` compara la huella del IL de `SubeUi` y `VentanaEnUso` contra la base: cambia (el tipo del parámetro y de las
  sobrecargas a las que llama); con los nombres de tipo `SkiaSharp.SKBitmap` y `Rocola.Dibujo2D.Bitmap` cambiados por uno solo, el IL de
  la base y el nuevo son **idénticos** (diff vacío en los dos).

## Comprobaciones (con números)

Salidas en `/root/rocola-trabajo/port/interfaz-a-skbitmap` (fuera del repositorio; sin binarios ni PNG).

- **(a) Compila** (`dotnet build -c Release --no-incremental`): 0 errores, 15 avisos, la misma lista archivo:línea:aviso que la base.
- **(b) diff-xp.py -v** de los 13 archivos: 0 líneas sin marca nuevas (las «sin marca» por archivo, las de la base: Form1.Video 4,
  D3D9Star 12, Form1.Ajustes 9, Form1.Biblioteca 2, Form1.Datos 2, Form1.Sonido 10). Quitadas/puestas: D3D9Star 415/279 → 411/275,
  AcabadoGpu 101/99 → 100/98, EstadoReposoGpu 114/112 → 111/109, Form1.Ajustes 403/400 → 402/399, SuenaVivoGpu 172/169 → 171/168,
  Form1.Datos 43/40 → 44/41 (la nota de `winSnap`); los demás igual. Las 10 líneas puestas sin «EN LINUX» del `git diff` son las que
  vuelven a ser de XP.
- **(c) NativeAOT** (`dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true`): 0 avisos IL/AOT; los 15 del
  compilador.
- **(e) Arneses**, contra la referencia congelada:
  - **Cintas** (`VistasFrontera/cintas.sh`): **29 de 29 iguales (6 616 cuadros)**.
  - frontera (`VistasFrontera/compara.sh`): sk y puente, con letras falsas y DejaVu: 106 líneas, 0 distintas; 153 texturas volcadas
    (153 en la referencia), 0 distintas.
  - suena: 488 líneas IGUALES (falsas y DejaVu; 72 llamadas directas); las 5 monedas `Format32bppPArgb flags=2` con la misma huella.
  - reposo: 1 791 IGUALES (las dos; 768 directas).
  - acabado-estilo: 0 distintas de 6 603 contra la referencia (las dos); contra la base solo `il-SubeUi` e `il-VentanaEnUso` (arriba).
  - buscador-portadas: 3 128 IGUALES (las dos; 250 directas, 5 excepciones las de siempre).
  - portadas: 478 IGUALES, 0 excepciones, 0 «directo distinto», 104 no nativos comparados.
  - **Banco** (semillas 7 y 11 con las letras falsas, 5 con SIN_LETRAS; ejecutores gdi, nativo y puente; 3 000 casos): **0 distintos en
    los 9**; `puente-objetos`: 402 bien, 0 mal.
- **(d) Recorridos** (los nueve, `VOLCADO=1 CAPAS=1 HUELLAS=1`, con el candado y la carpeta de siempre, contra `cmp-ref` y `cmp-ref2`):
  los nueve salen con 0. **Capas de Dibujo2D quietas 213 de 217**: las 3 de `ctrlf3` en la captura de los 46 s (la ventana F12 abierta:
  `compara.py` empareja por número de capa y la referencia arma un tramo de Dibujo2D más por ventana; lo explica `PORT_f12-como-hoja.md`,
  sale igual desde la junta de 7a) y la de `hojas` a los 18 s (la hoja de Ctrl+F3 con lo que lleva medido el repaso, `PORT_JUNTAS.md`);
  repetido `ctrlf3`, las mismas 3. **Texturas quietas 478 de 487** en la primera corrida; con otra de `ayuda`, `buscador`, `pared2` y
  `ctrlf3` (`cmp2`, con la máquina a carga 4-5: esa sola da menos) y contando las huellas distintas, de 381 quietas salen 377: las 4 que
  faltan son las de la hora o de la carga que ya explican los grupos anteriores (`pared2` 1024×256 `2a697d30` y 1024×128 `2f591363`,
  `buscador` 1024×128 `8cd9108c`, `ayuda` 1024×128 `b652908d`, `PORT_vistas-a.md`). Ninguna textura de la interfaz, de una pieza, del
  panel de reposo, del aviso ni de la moneda falta.
- **(f) Escenas alternadas con la referencia** (`alterna.sh`, etiquetas `Rinterfaz1`/`interfaz1`, comparadas con el ruido de
  ventanas2): arranque, moneda, buscador y captura **IGUALES**; video y aviso solo por las cuentas del reloj `Form1.cs:724` y del encargo
  `MedidasBitacora.cs:129` (la traducción con menos ticks, carga 5); repetidas (`Rinterfaz2`/`interfaz2`): video **IGUAL**; aviso solo
  por el total del reloj `Form1.cs:724`, ahora con el signo cambiado (107 frente a 98, holgura 6; carga 5,5 a 6,8): ruido de carga, lo
  aceptado. **NativeAOT** (`interfazA1`, `touch -r` de dotnet en `rocola-xp`) contra `Rinterfaz1`: arranque y moneda **IGUALES**.

## Riesgos

- **Un `SKBitmap` soltado que se lea cierra la rocola** (violación de segmento), donde XP lanzaba `ArgumentException` (GDI+). Antes de este
  grupo el puente lo disimulaba a medias (`PuenteDibujo.Mapa` de un `Bitmap` soltado daba `null`). En los caminos de XP no llega ninguno:
  cada `Dispose` de estas imágenes va con su `= null` o con el cambio del campo en la misma línea, y lo que leen `SubeUi`, `SubeUiBot`,
  `SubePieza`, `SubeEstadoExtendido`, `EnsureCover`, `TeselasEnPasos` y la moneda es siempre lo que está en el campo en ese cuadro. Las
  comparaciones por identidad con un mapa ya soltado (`idleLastBmp`, `winLastBmp`, `TrabajoTeselas.Fuente`) no lo leen. Quien traduzca
  algo que guarde una de estas imágenes entre cuadros tiene que conservarlo.
- `Mapas.Copia` (la instantánea) dibuja el mapa sobre uno transparente con SrcOver y sin filtro, a la misma medida: da los mismos puntos
  premultiplicados que el `Clone` de XP (ya lo usaba la base, ahora sin el puente alrededor). Un `Clone` con la zona fuera lanzaba
  `OutOfMemoryException` y XP lo atrapa; aquí la zona es siempre la imagen entera.
- El formato ya no viaja con el mapa: todas estas imágenes son PArgb en XP (comprobado en `Rocola/`); si alguna vez se hiciera una con otro
  formato, nadie lo vería. Hoy ningún lector de ellas mira el formato (solo las portadas, `PORT_cache-portadas.md`).
- Nada se probó en el A4.

## Lo que queda

- `PuenteDibujo.Entrega` y `PuenteDibujo.Mapa` (`Puente.cs`) quedan sin llamadores en `Codigo/`; los usan los puentes muertos de
  `Puente.fondo.cs` y `Puente.pared.cs` y los arneses: N7.
- Notas viejas en archivos que no son de este grupo y que nombran `Puente.portadas.cs` o el prestado: `DibujoLigeroGpu.cs:257` y `:263`,
  `RespuestaGpu.cs:111`, `TextoMovilGpu.cs:12` (comentarios; las arregla el dueño de cada archivo en su oleada o 9b).
- `Pruebas/VistasFrontera/compara.sh` sigue corriendo la puerta `puente`, que ya es la misma que `sk` (se puede quitar en 8b).
