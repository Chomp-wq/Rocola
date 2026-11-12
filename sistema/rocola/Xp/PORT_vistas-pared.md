# Port nativo: las vistas de la pared sin Control (oleadas 5 y 6, grupo vistas-pared: W3b)

Seccion «6b. vistas-pared (W3b)» de PLAN_TANDA4_A_N7.md, sobre d05b06d. AttractView (la atraccion), AlbumView (la pared de discos) y
CubeBox (el cubo de la cancion: `cubeBox` dentro del panel del disco y `miniCube`) dejan de heredar de Control: son vistas de la pantalla
nativa (IVista) con su CajaVista, como HelpBar (PORT_armado-vistas.md), AyudaView (PORT_vista-ayuda.md), BgPanel (PORT_panel-del-disco.md)
y las siete de Form1.Vistas.cs (vistas-disco, en paralelo). El precalentado de la atraccion (Form1.Portadas.cs) deja de pasar por la
firma de XP de EnSeco del puente. El resultado visible y las trazas no cambian.

## Lo hecho

Codigo/Form1.Pared.cs (1678 lineas y 1470 con CRLF, las mismas que en d05b06d; cada linea cambiada con «EN LINUX»). Por vista, revisado
contra Rocola/Form1.Pared.cs:

| vista | SetStyle de XP | ResizeRedraw -> RepintaConMedida | TabStop / Selectable | Dispose de XP | raton |
|---|---|---|---|---|---|
| AttractView | AllPaintingInWmPaint, OptimizedDoubleBuffer, UserPaint, ResizeRedraw | true (de fabrica) | de fabrica (true) | no (no es IDisposable) | ninguno propio; Form1 le oye MouseDown (PantallaRocola.OyeRaton) |
| AlbumView | AllPaintingInWmPaint, UserPaint, OptimizedDoubleBuffer | false | de fabrica (true): sigue en el ciclo del tabulador | si | OnMouseClick -> Raton(Clic) |
| CubeBox | ... SupportsTransparentBackColor | false | de fabrica (true) | no (no es IDisposable) | - |

- **Cabeceras**: `: Control, IVista` -> `: IVista` (AttractView y CubeBox) y `: IVista, IDisposable` (AlbumView). AlbumView y CubeBox siguen
  partial como en XP (FluidezGpu.cs, CaratulasSeparadasGpu.cs).
- **SetStyle** -> `caja = new CajaVista(this)` con `RepintaConMedida` = lo que XP pone en ResizeRedraw (Control.SetBoundsCore). Ninguna toca
  Selectable, TabStop, StandardClick ni StandardDoubleClick: los de fabrica de Control, que el hueco de la capa (HuecoDeVista) conserva. Un
  Control mide 0x0 de fabrica, como la caja.
- **OnPaint -> dos Pinta**, en la linea de su OnPaint:
  - `Pinta(SKCanvas)` (la de la pantalla, IVista.Pinta): el estado del Graphics de un WM_PAINT (Graphics.FromImage de la capa: sin suavizado,
    sin copia, filtro Bilinear, sin medio pixel).
  - La de XP con el estado del Graphics en argumentos: `AttractView.Pinta(g, suave, copia, filtro, medio)`, `AlbumView.Pinta(g, suave, copia,
    ref filtro, medio)` (el filtro con ref: el que DrawWall deja puesto, que en XP quedaba en el Graphics, como ya hace DrawWall) y
    `CubeBox.Pinta(g, copia, medio)` (lo unico que lee del estado: el suavizado y el filtro los pone el mismo). El OnPaint de XP lee el
    estado del Graphics que le llega (el cielo de la atraccion y de la pared, la pared con su copia y su filtro); los dos que XP le da son
    el de un WM_PAINT y el del precalentado (EnSeco: Graphics.FromImage con SmoothingMode y TextRenderingHint AntiAlias). Lo que XP le dejaba
    al Graphics (Bilinear y ClearTypeGridFit en la atraccion, el filtro en la pared, Bilinear y AntiAlias en el cubito) se iba con el.
  - Dentro, el cielo ya no pasa por el puente: `Starfield.Draw(g, suave, filtro, medio, copia, ...)` y `DrawReactive(...)` traducidas
    (Form1.Fondo.cs), con el estado que trae el Graphics (el cielo no se lo cambia: Puente.fondo.cs lo dice y el arnes lo comprueba).
- **CubeBox.BackColor**: un campo propio que repinta al cambiar (Control.OnBackColorChanged); con SupportsTransparentBackColor admite alfa (no
  lanza). Form1 lo pone transparente siempre (Form1.cs:1188, 1271; Form1.Video.cs:462) y el fondo de un control transparente es el de su
  dueno, que la capa y la pantalla pintan debajo: el OnPaintBackground de fabrica no pintaba nada que se viera (la capa tampoco: «if (c.A ==
  0) return»); opaco, lo tapa el `Clear` de su OnPaint.
- **AlbumView**:
  - `Raton(que, e)` atiende solo el clic (OnMouseClick); su `base.OnMouseClick(e)` (el aviso MouseClick) lo da el hueco o la pantalla
    despues de Raton (PantallaRocola.AvisaOyentes).
  - `Dispose(bool)` privado con `IDisposable.Dispose() => Dispose(true)`; el `base.Dispose(disposing)` lo hace su hueco despues.
  - `OnFontChanged` privado, que llama el Font de la vista; su `base.OnFontChanged(e)` es `caja.Invalidate()`.
  - `Font`: la Font de la capa (Rocola.Dibujo2D.Font) hasta la oleada 7, con lo de Control.Font igual que vistas-disco (sin la suya, la
    ambiental de la ventana, que XP nunca cambia: Control.DefaultFont; al ponerla, la propia solo cambia si es otra letra). Nadie se la pone;
    EnsureLabelRes lee su familia.
  - `Invalidate(Rectangle)` (el giro de la casilla y el horneado repartido): como en la capa, la vista se pinta entera.
  - `this.Parent.ClientSize` -> `PantallaRocola.ClienteDelDueno(this, out cliente)` (como BgPanel).
- **Lo que XP usa por fuera y daba Control** (en la linea de Pinta, de la caja): Bounds, Left, Top, Width, Height, SetBounds, Visible e
  Invalidate (y lo de arriba). Inventario de los usos de fuera (Form1.cs, Form1.Video.cs, Form1.Biblioteca.cs, Form1.Portadas.cs,
  FluidezGpu.cs, PantallaVivaGpu.cs, AtraccionGpu.cs): solo esos y miembros propios de la vista.
- **Notas viejas corregidas** (solo comentarios): la del `using` (linea 10); las de GlowAround, CtrlDot, CtrlTxt, CtrlTri, CtrlPad,
  CtrlDirLeft y CtrlDirRight decian que «quien aun pasa un Graphics llega por el puente» (esas firmas se borraron con limpieza-puentes);
  `Cube` vuelve a ser `private static class Cube`, la linea de XP (su parte del puente ya no esta).

Codigo/Form1.Portadas.cs (1229 lineas y 1043 con CRLF, las de d05b06d; tres lineas, :1199, :1201 y :1202): el paso 2 del precalentado,
`EnSeco(attractView, delegate (Graphics g) { using (PaintEventArgs pe = ...) InvokePaint(attractView, pe); })`, pasa a la EnSeco traducida
(Form1.Video.cs:1854, que da el lienzo de su mapa y su estado) y a `attractView.Pinta(g, suave, false, Filtro.Bilineal, false)`:
Control.InvokePaint llama al OnPaint de la vista con ese Graphics, y la Pinta de cinco argumentos es ese OnPaint con el estado de EnSeco
(el suavizado; sin copia, Bilinear y sin medio pixel, los de fabrica de Graphics.FromImage; el modo de letra no lo lee: pone el suyo). El
PaintEventArgs solo daba el recorte (el mapa entero), que el lienzo ya es.

Plataforma/Puente.video-form1.cs: se borran la firma de XP `EnSeco(IVista, Action<Graphics>)` y `PuenteDibujo.GraphicsVideoForm1` (ya
nadie las llama). Queda solo el comentario (lo borra la limpieza o N7).

Pruebas/:
- **VistasPared/Pared.cs**: sin casts a Control (SetBounds y BackColor por reflexion: de Control en la referencia, de la vista en la
  traduccion; Dispose solo si es IDisposable). Donde la referencia llama al OnPaint de la vista, la traduccion llama a `Pinta(SKCanvas)`
  cuando el Graphics trae el estado de un WM_PAINT y a la Pinta con el estado en argumentos en los demas (los nueve estados de prueba), y
  le pone despues al Graphics lo que XP le dejaba (lo que hacia el puente). Caso NUEVO `atraccion-*-precalienta` (4): Form1.PrecalientaUnPoco
  en su paso 2 sobre un Form1 sin construir (sin tarjeta), con la mezcla de portadas que hornea (blendBmp) y el paso al que queda (3 =
  hecho; 2 con un reintento si la vista mide menos de 8: EnSeco no dibuja).
- **VistasVideoForm1/VideoForm1.cs**: el caso `enseco-*` llamaba a la firma de XP de EnSeco del puente; en la traduccion llama a la
  traducida con lo que hacia el puente en el arnes (un Graphics sobre el lienzo con su estado, `PuenteDibujo.Graphics(lienzo,
  EstadoLienzo)`), y el texto de GDI (`textogdi-*`) por ese mismo Graphics en vez de GraphicsVideoForm1. Sigue compilando contra la
  referencia congelada (todo por reflexion).
- VistasAtraccion y Ventanas/PantallaVistas no nombran estas vistas: no cambian.

## Costuras

Quitadas:
- La herencia de Control de AttractView, AlbumView y CubeBox (y sus OnPaint, OnMouseClick, OnFontChanged y Dispose de Control).
- `EnSeco(IVista, Action<Graphics>)` y `PuenteDibujo.GraphicsVideoForm1` (Puente.video-form1.cs).
- El uso de las firmas de XP del cielo `Starfield.Draw(Graphics, ...)` y `DrawReactive(Graphics, ...)` por la atraccion y la pared.

Anadidas: ninguna (la Font de AlbumView sigue siendo la de la capa, como en vistas-disco, hasta la oleada 7).

Quedan muertas en archivos que no son de este grupo (compilan igual; las borra la limpieza o N7):
- Plataforma/Puente.fondo.cs: `Starfield.Draw(Graphics, ...)` y `DrawReactive(Graphics, ...)` ya no las llama nadie (el buscador ya llamaba
  directo). `DrawCached(Graphics, ...)` sigue por Form1.cs:1062 hasta W7.
- Plataforma/Puente.video-form1.cs: solo comentarios.

## Comprobaciones (12-11-2026, maquina compartida a carga 5-7, llvmpipe)

- a) `dotnet build -c Release --no-incremental`: 0 errores y los MISMOS 15 avisos de la base (comparados linea por linea: archivo, linea,
  columna y codigo).
- b) diff-xp.py Form1.Pared.cs Form1.Portadas.cs -v: Form1.Pared.cs 244 quitadas, 241 puestas, 1 sin marca (la misma de la base: una «{»
  bajo su nota de XP); Form1.Portadas.cs 72/69, 0 sin marca. Lineas y CRLF de los dos archivos iguales a d05b06d.
- c) NativeAOT (`dotnet publish ... -p:PublishAot=true`, sobre 5645dfc; el commit siguiente solo cambia comentarios y la cabecera de Cube):
  0 avisos IL/AOT (los 15 de siempre son de la compilacion normal).
- e) Arneses:
  - VistasPared (PARED=.../vistas-pared/pared): **IGUALES** con las letras falsas y con DejaVu, 1719 lineas (1612 llamadas directas a la
    traducida y 51 a Pinta(SKCanvas)); los 4 casos nuevos del precalentado: paso 3 y la mezcla 320x320 con la misma huella en las dos, y en
    7x300 paso 2 con un reintento, en las dos.
  - VistasAtraccion: **IGUALES** con las dos letras, 19120 lineas (3610 mapas, 68432 llamadas directas).
  - VistasVideoForm1: **IGUALES** con las dos letras, 735 lineas (140 llamadas directas, las mismas que en armado-vistas).
  - PantallaVistas (pruebas_vistas.sh): **93 de 93**.
- f) pruebas41: **41 de 41**; `AOT=1 pruebas_pantalla`: **32 de 32** en JIT y en NativeAOT (sus 2 avisos CS0649 de siempre).
- El ciclo del tabulador (16 Tab desde los 7 s, la traza del foco): referencia congelada y esta rama, los mismos 17 cambios de foco:
  HelpBar LetterBar AlbumView ProgressView GpuPanel (tres vueltas) HelpBar LetterBar. Al final esta rama apunta ademas el cierre por
  SIGTERM (`closing ... WindowsShutDown`, «foco nadie»), que la base d05b06d apunta igual y la referencia de trazas no llega a escribir en
  esta escena: no es de este cambio.
- Escenas alternadas con la referencia congelada (JIT, RVP/VP, ruido de ventanas2/ruido): **arranque, moneda y asistente IGUALES**. tab: la
  traza IGUAL (508 sucesos, 348 ticks, 324 encargos) y distintos solo los rotulos de lo que el repaso de fondo lleva medido al abrir
  Ctrl+F3 («Calidad: 10 aceptables / faltan 3» en la referencia frente a «12 / ya esta al dia», «0%» frente a «33%») y por eso 3886
  puntos de la capa de esa hoja: lo mismo que documentaron armado-vistas y panel-del-disco para esta escena (depende de la carga; no toca el
  repaso ni Ctrl+F3). NativeAOT (con la fecha de dotnet en rocola-xp) contra la referencia JIT (RVPA/VPA): **arranque IGUAL**.
- d) Recorridos pared, pared2 y atraccion (con el candado y la carpeta de siempre): 3 de 3 corren; **capas quietas 46 de 46** (pared 24,
  pared2 12, atraccion 10); texturas quietas: atraccion **32 de 32**, pared **83 de 83**, pared2 93 de 98. Las 5 de pared2 que faltan
  (repetido dos veces, las mismas) son de las que dependen de la hora y de cuantos pasos da la rocola: dos 1024x128 (62,4 s y 69,2 s), dos
  1024x256 (41,9 s y 62,3 s) y una 1024x64 (62,3 s); la base d05b06d (el programa de la rama principal) corrida a la misma hora da 94 de 98
  (le faltan tres 1024x128 -13,0 s, 62,4 s y 69,2 s- y la 1024x256 de 41,9 s; la pareja de 62,3 s le sale en 63,0 s). armado-vistas apunto
  esas mismas (pared2: dos 1024x256, una 1024x64 y una 1024x128). Ninguna de estas vistas va a la tarjeta: con tarjeta la pared no se pinta
  por procesador (GpuCovered) y DrawCtrlOverlay no las pinta.
- No corridas: el banco (no se toco Nativo/ ni Puente.cs) y las 29 cintas (no cambia nada de lo que se sube a la tarjeta).

## Lo que queda

- Notas de archivos de otros grupos que ahora quedan viejas: Form1.Video.cs:10 y :1854 (de 5a) dicen que el precalentado de la atraccion
  llega por la firma de XP del puente; Form1.Fondo.cs:1667 y :2190 (las notas de Starfield.Draw y DrawReactive) dicen que la firma de XP
  con un Graphics es para Form1.Pared.cs.
- La Font de AlbumView es la de la capa hasta la oleada 7 (Control.Font pasa a Letra).
- El hueco de la capa (HuecoDeVista) y ILaCapa se van con la capa (N7).

## Riesgos

- El MouseDown de la atraccion (que la despierta sin tarjeta) y el clic en la pared por procesador no tienen escena propia: con tarjeta el
  raton va al GpuPanel en la referencia y en esta rama. El camino (vista sin herencia en su hueco, Raton y despues los oyentes) lo cubre
  PantallaVistas (93 de 93) y es el mismo de LetterBar y SongListView.
- Sin la capa (lista nativa) las vistas no tienen foco de teclado ni recorren el tabulador: el ciclo de XP (HelpBar, LetterBar, AlbumView,
  ProgressView, GpuPanel) hoy lo da el hueco; la lista nativa tendra que leer `ParaElTabulador` y `Seleccionable` (oleada 6c / 8).
- La Pinta de cinco argumentos de la atraccion es la que pide el precalentado: si algun dia EnSeco le pusiera otro estado (la copia o el
  medio pixel), Form1.Portadas.cs tendria que pasarselo (hoy son los de fabrica de Graphics.FromImage, como en XP).
