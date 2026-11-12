# Port nativo de XP: el buscador, la interfaz de la tarjeta y la barra de ayuda llaman DIRECTO a lo ya traducido

Grupo «directas-buscador-video» de la oleada 1 (tanda 4) de `PLAN_TANDA4_A_N7.md` (sección «1a»), 12-11-2026. Base: `178722c`; rama
`port-directas-buscador-video`. Decisiones del dueño en `AGENTS.md` (8-11-2026: «que tradujeras todo el código para que trabajara
nativamente en Linux», «ningún layer de traducción») y la receta en `PORT_DIBUJO.md`. Aquí va lo que se hizo, cada costura quitada (y la que
queda) con su razón, las comprobaciones con sus números, lo que queda y los riesgos. Nada de esto se probó en el A4.

## Qué se hizo

Tras la tanda 3, los archivos ya traducidos se seguían llamando entre sí por los puentes de la capa (un `Graphics` de usar y tirar sobre el
mismo lienzo, que el otro lado volvía a deshacer en lienzo y estado). Ahora se llaman directo, con el lienzo y las variables de estado que
ya llevaba cada uno. Ninguna lógica de XP cambia: solo la llamada.

| Dónde | Antes (por el puente) | Ahora (directo) |
|---|---|---|
| `Form1.Buscador.cs` (20 líneas) | `CtrlTxtBuscador`, `CtrlDotBuscador`, `CtrlTriBuscador`, `CtrlPadBuscador`, `GlowAroundBuscador`, `CtrlDirLeftBuscador` | `CtrlTxt`, `CtrlDot`, `CtrlTri`, `CtrlPad`, `GlowAround`, `CtrlDirLeft` de `Form1.Pared.cs` con `(SKCanvas, suave, ajuste, copia, medio, ...)`: la misma firma que el puente; la `Letra` de `CtrlTxt`, la misma (antes iba a `Font` y volvía) |
| `Form1.Buscador.cs:965` | `DrawReactiveBuscador(Sky, g, suave, ajuste, copia, filtro, medio, hdc, ...)` | `Sky.DrawReactive(g, suave, filtro, medio, copia, ...)` (`Form1.Fondo.cs`; el puente del fondo pasaba esos cuatro, el modo de letra y la matriz del HDC no los mira) |
| `Form1.Buscador.cs:1517-1519` | `cover = ... ? ca : PuenteDibujo.Bitmap(BlendInto(...))` y `CubeDrawBuscador(...)` | `SKBitmap cover = ... ? PuenteDibujo.Mapa(ca) : BlendInto(...)` y `Cube.Draw(g, suave, copia, filtro, medio, area, cover, CubeAngle)`: los mismos puntos (el puente de la pared hacía `Mapa(cover)`; `Mapa(Bitmap(blendBmp))` es `blendBmp`) |
| `Form1.Buscador.cs` (10 + 20 líneas) | `using (Rocola.Dibujo2D.GraphicsPath gp = RoundedPath(...))` y `PuenteDibujo.Ruta(gp)` | `using (SKPath gp = ContornoRedondo(...))` y `gp` (el puente del fondo metía ese mismo `SKPath` en el `GraphicsPath` y `Ruta` lo sacaba sin copia; se suelta igual al salir del `using`) |
| `Form1.Video.cs:1292` (`EnsureUiOverlay`) | `searchOverlay.RenderContent(PuenteDibujo.GraphicsVideoForm1(...), caja, true)` | `{ Filtro filtroGuardado = filtro; searchOverlay.RenderContent(g, ajuste, copia, ref filtroGuardado, medio, SKMatrix.Identity, caja, true); }`: el filtro que deje el cubo va a una copia porque en XP se iba con el `Restore` de la línea siguiente (y aquí, con tarjeta, no se pinta el cubo) |
| `Form1.Video.cs:1498` (`EnsureUiPieza`) | `searchOverlay.DibujaPiezaViva(PuenteDibujo.GraphicsVideoForm1(...), cual, dx, dy)` | `searchOverlay.DibujaPiezaViva(g, ref suave, ajuste, copia, filtro, medio, SKMatrix.Identity, cual, dx, dy)`: lo que le deja al suavizado vuelve a la variable, como al `Graphics` de XP, que se tira ahí mismo |
| `Form1.Video.cs:1786-1793` (`DrawCtrlOverlay`) | las ocho vistas con `PuenteDibujo.GraphicsVideoForm1(...)` | las traducidas con lo que cada puente le sacaba al `Graphics`: `HeaderView(g, ajuste, copia, medio)`, `LetterBar(g, ajuste, copia, filtro, medio, true)`, `PastillaView.RenderContentGpu(g, ajuste, copia, filtro, medio)`, `ProgressView(g, copia, medio)`, `GeneroView(g, ajuste, copia, medio)`, `SongListView(g, copia, filtro, medio)`, `SongPagerBar(g, suave, copia, medio)` (el suyo leía el suavizado ANTES de cambiarlo), `BuscaView(g, ajuste, copia, medio, true)`. Lo que el puente le dejaba al `Graphics` después no importa: era uno de usar y tirar y en XP se iba con el `Restore` |
| `Form1.cs:2745-2755` (`HelpBar.RenderContent`) | `GlowAroundVideoForm1`, `CtrlPadVideoForm1`, `CtrlTxtVideoForm1`, `CtrlDotVideoForm1` | `GlowAround`, `CtrlPad`, `CtrlTxt`, `CtrlDot` de `Form1.Pared.cs`, misma firma |
| `Form1.Portadas.cs:1187-1188, 1194` y `PrecargaPaginaGpu.cs:38-39, 49` (los precalentados) | `EnSeco(c, delegate (Rocola.Dibujo2D.Graphics g) {...})` y las firmas de XP del buscador y la lista | `EnSeco(c, delegate (SKCanvas g, bool suave, AjusteLetra ajuste) {...})` (la traducida de `Form1.Video.cs:1854`) y las traducidas con el estado de un `Graphics.FromImage` que daba el puente: sin copia, `Filtro.Bilineal`, sin medio píxel, HDC identidad |

El estado que se pasa es exactamente el que el puente metía en su `Graphics` y el otro puente volvía a leer (`PuenteDibujo.Graphics` y
`Estado` son campos simples: la ida y vuelta no pierde nada; `GraphicsVideoForm1` y `GraphicsBuscador` ponían la matriz del HDC identidad
o la del que llama, que ahora se pasa igual).

Las notas `EN LINUX` de esas líneas dicen ahora qué era en XP y por qué es lo mismo; las viejas que decían «aún sin traducir» de archivos ya
traducidos se corrigieron en los archivos del grupo (`Form1.Buscador.cs` 10, 175, 233, 237, 348, 359, 373, 876, 938, 1433, 1467;
`Form1.Video.cs` 10, 663, 821, 1040, 1280, 1374). Todos los archivos de `Codigo/` conservan su número de líneas y de CRLF (109 de 109
comprobados contra `178722c`).

## Las costuras

**Quitadas** (comprobado con `grep` y con el compilador que nadie más las llamaba):

- `Plataforma/Puente.buscador.cs`: `BuscaView.RenderContent(Graphics, bool)` (su cuerpo pasa a la de un parámetro, que en XP era
  `RenderContent(g, false)`), `SearchOverlay.DibujaPiezaViva(Graphics, int, int, int)`, `GraphicsBuscador` y los ocho `*Buscador`
  (`CtrlTxt`, `CtrlDot`, `CtrlTri`, `CtrlPad`, `GlowAround`, `CtrlDirLeft`, `CubeDraw`, `DrawReactive`). Las líneas 33, 59 y 72-120 del plan.
- `Plataforma/Puente.video-form1.cs`: `GlowAroundVideoForm1`, `CtrlPadVideoForm1`, `CtrlDotVideoForm1`, `CtrlTxtVideoForm1` (55-64 del plan).

**Quedan** (con su razón y cuándo se quitan):

| Costura | Quién la usa | Se quita cuando |
|---|---|---|
| `BuscaView.RenderContent(Graphics)` y `SearchOverlay.RenderContent(Graphics, Rectangle, bool)` (`Puente.buscador.cs`) | los `OnPaint` de WinForms (la capa de ventanas) | se traduzcan los `OnPaint` (oleada 6 del plan) |
| `HelpBar.RenderContent(Graphics, bool)` (`Puente.video-form1.cs`) | su `OnPaint` | igual |
| `EnSeco(Control, Action<Graphics>)` y `PuenteDibujo.GraphicsVideoForm1` (`Puente.video-form1.cs`) | el precalentado de la atracción (`Form1.Portadas.cs:1199`, `InvokePaint` del `OnPaint` de `attractView`) | se traduzca ese `OnPaint` |
| las portadas de la caché como `Rocola.Dibujo2D.Image` (17 sitios de `Form1.Buscador.cs`) y `PuenteDibujo.Mapa(ca)` en el cubo | la caché las publica como `Image` de la capa y `D3D9Star.cs` las sube como `Bitmap` | la caché y la subida den `SKBitmap` |
| `e.Graphics.TextRenderingHint = ClearTypeGridFit` en `BuscaView.OnPaint` | WinForms | se traduzca el `OnPaint` |

**Muertas en archivos compartidos, que se quedan** (las borra el grupo que es dueño o N7): en `Plataforma/Puente.pared.cs`, `Cube.Draw(Graphics,
Rectangle, Image, double)` y `CtrlTri(Graphics, ...)` ya no las llama nadie (solo `Form1.Buscador.cs` las usaba); `GlowAround`, `CtrlDot`,
`CtrlTxt`, `CtrlPad` y `CtrlDirLeft` con `Graphics` pueden seguir vivas por `Form1.Vistas.cs` (grupo 1b, en paralelo). En
`Plataforma/Puente.fondo.cs`, `Starfield.DrawReactive(Graphics, ...)` sigue viva (la llama `Form1.Pared.cs:1527` con el `Graphics` de su
`OnPaint`) y `RoundedPath` sigue para `Form1.Ajustes.cs` y `Form1.Vistas.cs`.

## Los arneses

- `Pruebas/VistasBuscador/Buscador.cs`: el halo se llama por la traducida de `Form1.Pared.cs` (`Nativa(typeof(Form1), "GlowAround")`, la de
  primer parámetro `SKCanvas`; en la referencia no la hay y va la de XP). Sin las firmas de XP de `BuscaView.RenderContent(g, forGpu)` y
  `DibujaPiezaViva(g, ...)`, las líneas que las llamaban (en la traducción) hacen lo que hacía su puente: la traducida con el lienzo y el
  estado del `Graphics` y, después, lo que XP le dejaba (`CasillaSinPuente`: con `forGpu = false` la firma de un parámetro, que sigue;
  con `true` la traducida y el suavizado si pasa de su primer `return`; `PiezaSinPuente`: la condición de las tres líneas `AntiAlias` de
  XP, mirada antes de dibujar). Así se sigue comparando con la referencia también lo que le queda al `Graphics`.
- `Pruebas/VistasVideoForm1/VideoForm1.cs`: el halo por `GlowAround` con lienzo. Y un arreglo que ya hacía falta en la base: buscaba la de
  XP con `GetMethod("GlowAround")`, que desde la junta de pared es ambiguo en la traducción (están la traducida y la firma de XP de
  `Puente.pared.cs`): el arnés abortaba a la línea 585 de 723. Ahora la busca por su primer parámetro (`Graphics`).
- `Pruebas/VistasBuscadorPortadas/BuscadorPortadas.cs` (fuera de la lista del grupo; dos sitios mínimos, dichos en el informe): la misma
  `PiezaSinPuente` para `DibujaPiezaViva`, y `PintaCartasConCambios` sin su firma de XP (se quitó en la junta de video-form1 y el arnés
  daba 465 excepciones y 942 líneas distintas en la base; XP no le cambia nada al `Graphics` en esa función).

## Comprobaciones

Salidas fuera del repositorio en `/root/rocola-trabajo/port/directas-buscador-video` (solo huellas y resúmenes; los programas compilados se
borraron después de usarlos).

- **(a) Compila** (`dotnet build -c Release --no-incremental`): 0 errores y 15 avisos, los mismos de la base (mismos archivos, líneas y
  códigos: siete `Thread.VolatileRead`, `FormatterServices`, `Control.bounds` x2, dos `Read` inexactos y el `stackalloc`).
- **Líneas y finales de línea**: los 109 archivos de `Codigo/` con el mismo número de líneas y de CRLF que en `178722c`; `git diff --stat`
  de `Codigo/`: 95 líneas cambiadas, 95 y 95.
- **(b) `diff-xp.py -v`**: `Form1.Buscador.cs` 238 quitadas / 235 puestas, 0 sin marca; `Form1.Video.cs` 177/176, 4 sin marca;
  `Form1.cs` 157/156, 2 sin marca; `Form1.Portadas.cs` 75/72, 0; `PrecargaPaginaGpu.cs` 22/21, 0. Las 6 sin marca son las mismas que en
  la base (comentarios del video y de `Equipo.ProcesadorMhz`, de otros grupos); las dos líneas puestas de más (`Form1.Portadas.cs:1188` y
  `PrecargaPaginaGpu.cs:39`, que antes eran iguales a XP) llevan su `EN LINUX`.
- **(c) NativeAOT** (`dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true`): 0 avisos IL/AOT (los 15 de C#). El
  binario nativo, fuera de pantalla, en las escenas `arranque`, `buscador` y `video` contra la referencia (RDBV/DBVA): las tres IGUALES.
- **(e) Arneses** (programa entero de esta copia y de la referencia congelada, letras de XP falsas y DejaVu):
  - `VistasBuscador`: 4 709 líneas, **IGUALES** con las dos letras, 437 llamadas directas, 0 excepciones.
  - `VistasVideoForm1`: 723 líneas, **IGUALES** con las dos letras, 128 llamadas directas (con el arreglo del `GetMethod` ambiguo; sin él
    abortaba a la línea 585 también en la base).
  - `VistasBuscadorPortadas`: 3 128 líneas, **IGUALES** con las dos letras, 250 directas, 5 excepciones en los dos programas (las mismas de
    la referencia). Antes del arreglo de `PintaCartasConCambios`: 465 excepciones y 942 líneas distintas, por la firma quitada en la junta
    de video-form1, no por este grupo.
  - `VistasVistasA`: 1 266 líneas, **IGUALES** con las dos letras, 722 directas.
  - `VistasVistasB`: 2 865 líneas, 0 distintas contra la referencia y contra la base (ae8b50b) con las dos letras.
- **El banco** (`Pruebas/BancoDibujo`): no se corrió; este grupo no toca `Nativo/`, `Puente.cs` ni `Dibujo2D.cs` (lo único que compila
  el banco).
- **(d) Recorridos** (los nueve, con el candado y la carpeta de siempre, `VOLCADO=1 CAPAS=1 HUELLAS=1`, contra `cmp-ref` y `cmp-ref2`):
  los nueve salen con 0. **Capas quietas 216 de 217**: la que falta es la de siempre, la hoja de Ctrl+F3 de `hojas` a los 18 s (capa 4,
  lo que lleva medido el repaso de la biblioteca): sale con la huella `c45e76a2be1093d1`, la misma que dio la corrida de directas-vistas
  de hoy y las que apuntó `PORT_video-form1.md` en otras corridas; la referencia da `b9c547d314bcff22`; ningún archivo de este grupo
  dibuja esa hoja. Otra corrida de `atraccion`, `ayuda`, `buscador`, `pared2` y `hojas` (`cmp2`) da la misma. **Texturas quietas 479 de
  487** en la primera corrida; por huellas únicas, juntando las dos corridas faltan solo 4, las de la hora ya apuntadas en `PORT_buscador.md`
  y `PORT_video-form1.md`: `buscador` `e843916c…` (8,6 s) y `pared2` `2a697d30…` (41,9 s), `2f591363…` (62,4 s) y `1f1712f1…` (69,2 s);
  la corrida de la junta de la base (`junta11`) tampoco las tiene (477 de 487). Las texturas que salen en esta copia y no en la referencia:
  una de 128×128 en los nueve recorridos (`085b52bd…`, a los 0,4 s: la tiene también `junta11`, de la base, y la de directas-vistas: es de
  la fecha) y `7ce06f34…` de `buscador` (a los 35 s: sale en otras diez corridas de grupos anteriores).
- **Las 29 cintas** (`VistasFrontera/cintas.sh`): 29 iguales (6 616 cuadros), 0 distintas.
- **(f) Escenas alternadas con la referencia** (RDBV/DBV): `arranque`, `buscador` y `video` IGUALES; `captura` DIFERENTE solo por la
  cuenta del encargo `MedidasBitacora.cs:129` (466 frente a 480, holgura 12) y repetida (RDBVb/DBVb) IGUAL. `tab` salió DIFERENTE dos
  veces, solo por el orden de dos sucesos: el estado `shifttab12` (que el programa apunta a los 17,2 s de su reloj) y el clic en la
  pestaña Fondo (que el inyector manda a los 17,04 s). En la referencia el clic llega a los 17 284-17 304 ms, después del estado; en la base
  (`178722c`, compilada aparte, alternada con la referencia: IGUAL) a los 17 210 ms, 3 ms después del estado de 17 207; en esta copia a
  los 17 165 y 17 187 ms, antes del estado de 17 215-17 219. Es una carrera de la escena (el clic se manda 160 ms antes del estado y
  depende de cuánto tarde el cuadro que se está pintando): esta copia atiende el clic antes que la base porque `DrawCtrlOverlay` y la
  barra de ayuda ya no hacen un `Graphics` de la capa por vista y por símbolo en cada imagen de la interfaz. El resto de la escena (las
  teclas, los focos, los otros estados y las capturas) sale igual. No se tocaron ventanas, teclas, ratón ni cierre, así que no se corrieron
  pruebas41, la hoja ni la pantalla.
- **`Pruebas/Inventario`**: no da cuentas válidas hoy (no compila `Nativo/`: decenas de CS0234 en todas las copias), así que no se usó.

## Lo que queda

- Los `OnPaint` de `BuscaView`, `SearchOverlay`, `HelpBar` y `attractView` (oleada 6): con ellos se van las firmas de XP que quedan en
  `Puente.buscador.cs` y `Puente.video-form1.cs` (y el archivo entero).
- Las portadas como `Image`/`Bitmap` de la capa (caché de portadas, `D3D9Star.cs`).
- En archivos compartidos: `Cube.Draw(Graphics, ...)` y `CtrlTri(Graphics, ...)` de `Puente.pared.cs` quedan muertas (las borra el dueño
  de ese puente o N7). La nota de la línea 4 de `PrecargaPaginaGpu.cs` («lo que aún es de la capa (el Graphics de EnSeco...)») habla
  todavía del `Graphics`: esa línea no era de este grupo (solo 38-49).

## Riesgos

- La escena `tab` depende de una carrera de 160 ms (arriba). Con la máquina más rápida o más lenta puede salir en un orden o en el otro;
  conviene que la escena mande el clic después del estado (o el estado antes del clic) para que no dependa del ritmo de los cuadros.
- En `EnsureUiOverlay` el filtro que deja el buscador va a una copia (`filtroGuardado`) para respetar el `Restore` de XP; con tarjeta el
  buscador no pinta el cubo, así que el filtro no cambia, pero si alguien quita esa copia y pasa `ref filtro` la diferencia no se vería
  en ninguna prueba.
- En las firmas de XP del arnés (`CasillaSinPuente`, `PiezaSinPuente`) la condición de lo que XP le deja al `Graphics` está copiada de los
  puentes borrados: si cambia la lógica de XP de esas funciones, hay que cambiarla también allí (el arnés lo diría: compara con la
  referencia).
- Durante las pruebas se hizo un `git stash` de ida y vuelta en la copia mientras el arnés de buscador-portadas compilaba; ese arnés se
  repitió entero después (IGUALES) y el commit de los arneses es posterior.

## Revisión

Revisión independiente del mismo día (`git diff 178722c..` leído entero; las comprobaciones repetidas en esta copia).

- **Equivalencia, línea por línea.** Cada llamada directa recibe el mismo estado que antes le llegaba por los dos puentes (el `Graphics`
  de usar y tirar y la firma de XP del otro lado): las `Ctrl*`/`GlowAround` de `Form1.Pared.cs` (suavizado, modo de letra, copia y medio
  píxel; el filtro de fábrica y la matriz del HDC identidad no los miran); `Sky.DrawReactive` (suavizado, filtro, medio píxel y copia, los
  que sacaba `Puente.fondo.cs`); `Cube.Draw` (`Mapa(Bitmap(blendBmp))` es `blendBmp`; `Mapa(ca)` es lo que hacía `Puente.pared.cs`);
  `ContornoRedondo` (el mismo `SKPath` que `RoundedPath` metía en su `GraphicsPath`); las ocho vistas de `DrawCtrlOverlay` (cada una con
  los campos que su firma de XP en `Puente.vistas-a.cs`, `Puente.vistas-b.cs` y `Puente.buscador.cs` sacaba del `Graphics`, mismo orden
  que XP `Form1.Video.cs:1786-1795`); `EnsureUiOverlay` (filtro a una copia: en XP se iba con el `Restore` de la línea siguiente, que
  guarda también `InterpolationMode` y `SmoothingMode`); `EnsureUiPieza` (`ref suave`: en XP el `Graphics` se tira ahí, nada lo lee
  después); los precalentados (lo de `Graphics.FromImage`: sin copia, `Bilinear`, sin medio píxel, HDC identidad). En XP ni
  `Starfield.DrawReactive` (solo toca el estado de su `lg`), ni `Cube.Draw`, ni las ayudas de los botones cambian el estado del `Graphics`
  del que llama, así que no había nada que devolver. Ninguna lógica de XP cambia.
- **Encontrado y arreglado**: la nota `EN LINUX` de `EnSeco` (`Form1.Video.cs:1854`) seguía diciendo que `PrecargaPaginaGpu.cs` y
  `Form1.Portadas.cs` llegan por la firma de XP del puente; ahora dice que el buscador y la lista de los precalentados la llaman directo
  y que solo la atracción (`Form1.Portadas.cs:1199`) pasa por el puente. Solo el comentario (líneas y finales iguales).
- **Sin arreglar (fuera del rango del grupo)**: la nota de la línea 4 de `PrecargaPaginaGpu.cs` (ya apuntada arriba).
- **Repetido**: compila (`--no-incremental`) con 0 errores y los mismos 15 avisos de la base (comparados uno a uno); los 109 archivos de
  `Codigo/` con el mismo número de líneas y de CRLF que `178722c`; `diff-xp.py -v` de los cinco archivos igual que en el informe (6 sin
  marca, las de la base, de otros grupos); arneses `VistasBuscador` 4 709 líneas IGUALES con las dos letras (437 directas, 0
  excepciones), `VistasVideoForm1` 723 IGUALES con las dos letras (128 directas), `VistasBuscadorPortadas` 3 128 IGUALES con las dos
  letras (250 directas, 5 excepciones, las mismas de la referencia); las 29 cintas: 29 iguales (6 616 cuadros). No se repitieron
  NativeAOT, `VistasVistasA`/`VistasVistasB`, recorridos ni escenas (nada de la revisión los pone en duda; la junta los corre).
