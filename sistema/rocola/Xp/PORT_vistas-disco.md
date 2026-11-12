# Port nativo: las vistas del disco sin Control (oleada 5, grupo vistas-disco: W3a)

Seccion «5b. vistas-disco (W3a)» de PLAN_TANDA4_A_N7.md, sobre d05b06d. Las siete vistas de Form1.Vistas.cs (SongPagerBar, LetterBar,
HeaderView, PastillaView, GeneroView, ProgressView y SongListView) dejan de heredar de Control: son vistas de la pantalla nativa (IVista) con su
CajaVista, como HelpBar (PORT_armado-vistas.md), AyudaView (PORT_vista-ayuda.md) y BgPanel (PORT_panel-del-disco.md). El resultado visible y las
trazas no cambian.

## Lo hecho

Codigo/Form1.Vistas.cs (2749 lineas y 2568 con CRLF, las mismas que en d05b06d; cada linea cambiada con «EN LINUX»). Por vista, revisado
contra Rocola/Form1.Vistas.cs:

| vista | SetStyle de XP | ResizeRedraw -> RepintaConMedida | TabStop -> ParaElTabulador | Dispose de XP | raton |
|---|---|---|---|---|---|
| SongPagerBar | AllPaintingInWmPaint, UserPaint, OptimizedDoubleBuffer, ResizeRedraw | true (de fabrica) | de fabrica (true) | si | - |
| LetterBar | ... SupportsTransparentBackColor | false | de fabrica (true) | no (no es IDisposable) | OnMouseClick -> Raton(Clic) |
| HeaderView | ... SupportsTransparentBackColor | false | false | si | - |
| PastillaView | ... SupportsTransparentBackColor | false | false | si | - |
| GeneroView | ... SupportsTransparentBackColor | false | false | si | - |
| ProgressView | ... SupportsTransparentBackColor | false | de fabrica (true) | si | - |
| SongListView | AllPaintingInWmPaint, UserPaint, OptimizedDoubleBuffer | false | de fabrica (true) | si | OnMouseClick y OnMouseDoubleClick -> Raton(Clic / DobleClic) |

- **Cabeceras**: `: Control, IVista` -> `: IVista, IDisposable` (LetterBar solo IVista: XP no le sobrescribe Dispose). SongPagerBar, LetterBar,
  HeaderView, GeneroView y ProgressView vuelven a no ser partial, como en XP; PastillaView y SongListView siguen partial como en XP (DiscoVivoGpu.cs,
  SuenaVivoGpu.cs, AcabadoGpu.cs; y PastillaView.Ancho en el puente).
- **SetStyle** -> `caja = new CajaVista(this)` con `RepintaConMedida` = lo que XP pone en ResizeRedraw (Control.SetBoundsCore: «if
  (GetStyle(ControlStyles.ResizeRedraw)) Invalidate()»). Selectable, StandardClick y StandardDoubleClick no los toca ninguna: los de fabrica de
  Control, que el hueco de la capa (HuecoDeVista, Anfitrion.cs) conserva. Un Control mide 0x0 de fabrica, como la caja.
- **TabStop = false** (HeaderView, PastillaView, GeneroView) -> `ParaElTabulador => false`; el hueco nace con ese TabStop.
- **OnPaint -> Pinta(SKCanvas)**, en la linea de su OnPaint, con el estado del Graphics de un WM_PAINT (Graphics.FromImage: SmoothingMode None,
  TextRenderingHint SystemDefault = AjusteLetra.DelSistema, SourceOver, InterpolationMode Bilinear, PixelOffsetMode Default):
  SongPagerBar `RenderContent(g, false, false, false)`, LetterBar `(g, DelSistema, false, Bilineal, false, false)` (forGpu false como su OnPaint),
  HeaderView y GeneroView `(g, DelSistema, false, false)`, PastillaView `(g, DelSistema, false, Bilineal, false)`, ProgressView `(g, false, false)`,
  SongListView `(g, false, Bilineal, false)` (en la linea de su OnPaintBackground vacio; el hueco no pinta fondo). Lo que XP le dejaba puesto al
  Graphics (el suavizado, y en la barra de paginas el modo de letra) se iba con el. SongPagerBar: su OnPaintBackground de fabrica pintaba el
  BackColor opaco y su RenderContent lo vuelve a pintar entero sin suavizado: el mismo punto.
- **Dispose(bool)**: `void IDisposable.Dispose() { Dispose(true); } private void Dispose(bool disposing)`; el `base.Dispose(disposing)` lo hace el
  hueco despues (el orden de XP), como HelpBar.
- **Raton**: LetterBar `Raton(que, e)` atiende solo Clic (los `base.OnMouseClick(e)` -el aviso MouseClick- los da el hueco o la pantalla despues de
  Raton, PantallaRocola.AvisaOyentes); SongListView atiende Clic (OnPick) y DobleClic (OnActivate). Con StandardClick y StandardDoubleClick el
  segundo clic da DobleClic y no Clic (lo hace la capa, igual que antes).
- **Lo que XP usa por fuera y daba Control** (en la linea de Pinta, de la caja): Bounds, Left, Top, Width, Height, SetBounds, Visible, Invalidate;
  ademas Bottom (LetterBar y HeaderView: Form1.Video.cs:1244-1245), Right (GeneroView: EstiloGpu.cs:67), ClientSize (SongListView: la propia clase
  y AcabadoGpu.cs; sin borde ni barras, la medida) e Invalidate(Rectangle) (LetterBar: el latido de Form1.cs:2632-2633; como en la capa, la vista
  se pinta entera: el rectangulo solo acotaba la region del WM_PAINT en Windows).
- **BackColor**: un campo propio que repinta al cambiar (Control.OnBackColorChanged). Los de SupportsTransparentBackColor los pone transparentes
  su constructor (como en XP); SongPagerBar y SongListView (sin ese estilo) lanzan ArgumentException con un color con alfa, como Control. Sin poner
  seria el ambiental del dueno: Form1 los pone siempre antes de leerlos (Form1.cs:1191, 1243; Form1.Video.cs:461-463).
- **Font** (LetterBar, HeaderView, PastillaView, GeneroView, SongListView: las que la leen o le sobrescriben OnFontChanged): la Font de la capa
  (Rocola.Dibujo2D.Font) hasta la oleada 7, con lo de Control.Font: sin la suya, la ambiental, que es la de la ventana, que XP nunca cambia
  (Form1 no pone su Font; Control.DefaultFont, el mismo objeto: HeaderView la compara por identidad); al ponerla, la propia solo cambia si es otra
  letra (Control.IgualLetra, la de la capa) y avisa si la que se ve cambia. OnFontChanged queda privado y lo llama ese Font; su
  `base.OnFontChanged(e)` es `caja.Invalidate()` (repinta; el aviso FontChanged nadie lo escucha y no tienen hijas).
- Las notas de RenderContent que decian «quien aun pasa un Graphics llega por el puente» y la del using (linea 10) se corrigieron.

Plataforma/:
- **Puente.vistas-a.cs**: se borran las dos firmas de XP (SongPagerBar.RenderContent(Graphics) y LetterBar.RenderContent(Graphics, bool)); queda
  solo el comentario (lo borra la limpieza o N7).
- **Puente.vistas-b.cs**: se borran las firmas de XP con un Graphics de HeaderView, PastillaView (y su RenderContentGpu(Graphics)), GeneroView,
  ProgressView y SongListView. Queda PastillaView.Ancho con una FontFamily, que piden Form1.cs:1485-1486 y Form1.Datos.cs:3337 (se va con la Font
  del Form, oleada 7).

Pruebas/:
- **VistasVistasA/VistasA.cs**: sin casts a Control (SetBounds, BackColor, Font, Width y Height por reflexion: de Control en la referencia, suyos en
  la traduccion); donde llamaba a la firma de XP (que ya no esta) llama a la traducida con el estado del Graphics y le pone despues lo que XP le
  dejaba (lo que hacia el puente); los casos de OnPaint llaman a Pinta en la traduccion (16).
- **VistasVistasB/VistasB.cs**: lo mismo (lo que hacia el puente, en el arnes) y 243 casos NUEVOS `*-onpaint`: el OnPaint de XP (referencia) contra
  Pinta (traduccion), con el Graphics de un WM_PAINT trasladado y recortado como lo da la capa, para las cinco vistas.

## Costuras

Quitadas: las firmas de XP de RenderContent con un Graphics de las siete vistas (y RenderContentGpu(Graphics)) del puente; la herencia de Control
de las siete vistas.
Anadidas: ninguna. La Font sigue siendo la de la capa (Rocola.Dibujo2D.Font, y Control.DefaultFont y Control.IgualLetra de la capa para lo
ambiental): se va con la oleada 7 (Control.Font pasa a Letra).
Quedan (no son de este grupo): PastillaView.Ancho(FontFamily) (oleada 7); Puente.vistas-a.cs solo con comentarios (limpieza o N7); el hueco de la
capa (HuecoDeVista) y ILaCapa (N7).

## Comprobaciones (12-11-2026, maquina compartida a carga 3-7, llvmpipe)

- a) `dotnet build -c Release --no-incremental`: 0 errores y los mismos 15 avisos de la base (misma lista, mismas lineas).
- b) diff-xp.py Form1.Vistas.cs -v: 545 lineas quitadas, 542 puestas, 2 sin marca (las 2 de la base: llaves bajo un comentario de XP). Lineas y CRLF
  iguales a d05b06d (2749 / 2568).
- c) NativeAOT (`dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true`): publica, 0 avisos IL/AOT (solo los 15 de siempre).
- e) Arnes VistasVistasA: **IGUALES** con las letras falsas y con DejaVu (1265 lineas; 1466 directas y 16 por Pinta). Arnes VistasVistasB: **0
  distintas de 3108** con las dos letras (las 2865 de siempre y 243 -onpaint nuevas; con DejaVu 18 lineas con excepcion, las mismas en la referencia).
- PantallaVistas: **93 de 93**. pruebas41: **41 de 41**. pruebas_hoja: **86 de 86**. AOT=1 pruebas_pantalla: **32 de 32** en JIT y NativeAOT (sus 2
  avisos de siempre).
- El tabulador (escena propia, corre.sh con 16 Tab desde los 7 s, traza del foco; referencia ref-trazas contra esta):
  - en la pared con tarjeta: las dos `HelpBar LetterBar AlbumView ProgressView GpuPanel` x3 + `HelpBar LetterBar` (17 cambios de foco iguales);
  - con un disco abierto (Intro a los 7 s), con tarjeta: las dos `HelpBar CubeBox SongListView ProgressView GpuPanel` (ciclo de 5) x3 + HelpBar CubeBox;
  - con un disco abierto y sin tarjeta (gpu=0): las dos `HelpBar CubeBox SongListView ProgressView` (ciclo de 4).
  La traduccion apunta ademas `foco nadie` al cerrarse con el SIGTERM de los 15 s (la referencia no llega a apuntar su cierre: es el SIGTERM
  aceptado; ver PORT_JUNTAS.md).
- El raton en la lista (escena propia sin tarjeta, gpu=0, con el disco abierto: clic en la fila 2, doble clic en la 3, clic en la 1, doble clic en otra
  parte): las mismas 12 lineas de raton en la traza (baja/clic, baja con 2 clics/doble, en SongListView y en las mismas coordenadas), el mismo foco,
  y la lista de las capturas (442,186 a 998,654) punto por punto igual en las cinco; abajo (lo que suena, las capsulas, la barra) solo difieren
  estrellas del cielo que se mueve.
- f) Escenas alternadas con la referencia (alterna.sh, RVD/VD, datos0 de siempre, ruido de ventanas2): arranque, moneda, video, buscador: solo las
  cuentas del reloj Form1.cs:724 y del encargo MedidasBitacora.cs:129 fuera de la holgura (en las dos direcciones; carga 3-7); f3raton IGUAL; tab: la
  traza igual (508 sucesos) salvo el encargo del repaso Form1.Biblioteca.cs:1661 dos segundos corrido, y los rotulos de lo que el repaso lleva medido en
  la hoja de Sonido («12 aceptables» frente a «9», «33%» frente a «0%») con sus 5 082 puntos de la capa de esa hoja: lo de siempre (PORT_armado-vistas.md).
- f) NativeAOT (arranque, con la fecha de dotnet en rocola-xp) contra la referencia JIT: la secuencia, el foco y las capturas iguales; solo las cuentas
  del reloj y del encargo. Repetidas arranque, moneda y buscador en JIT (RVD2/VD2, carga 7-8): lo mismo, solo esas cuentas.
- d) Recorridos (con el candado y la carpeta de siempre): 9 de 9 salen con 0. Primera pasada (carga 7): capas quietas 211 de 217 y texturas quietas
  390 de 487 (buscador 44 de 78: la rocola dio 467 composiciones en 36 s frente a 869 de otra corrida; pared 34 de 83). Como salia por debajo de la
  ultima junta, se corrio la BASE d05b06d (sin este cambio, compilada fuera: /root/rocola-trabajo/port/vistas-disco/base) alternada con esta, con la
  misma carga:
  - pared: base 83 de 83, esta 82 de 83 (la que falta, una 1024x128 de los rotulos de la hora); capas 24 de 24 las dos;
  - buscador: base 76 y 76 de 78, esta 45 y 75 de 78 (la de 45 con la mitad de composiciones); capas 22 de 22 las dos;
  - ctrlf3, hojas, ayuda, pared2 y atraccion: base y esta dan EXACTAMENTE lo mismo: texturas 231 de 248 y capas 141 de 146 las dos, con las mismas
    capas distintas (ctrlf3 c46: tres capas de 1030x732 y 1024x768; hojas c18 y c19.5: la capa 4 de la hoja de Sonido, el repaso). Esas capas
    tambien salen distintas en la base de hoy: dependen de la hora o del repaso, no de este cambio.
  Ninguna diferencia nueva frente a la base.
- No se corrieron el banco (no se toco Nativo/ de dibujo ni Puente.cs) ni las 29 cintas (lo que se sube a la tarjeta no cambio: DrawCtrlOverlay ya
  llamaba a las traducidas, y la pagina horneada de la lista y el EnSeco no cambiaron).

## Lo que queda

- La Font de las vistas (oleada 7), PastillaView.Ancho(FontFamily) del puente (oleada 7).
- PantallaVistas no gana casos: las reglas de la pantalla no cambiaron (las vistas falsas ya cubren una vista sin herencia dentro y fuera del panel).

## Riesgos

- Invalidate(Rectangle) de LetterBar pinta la vista entera (como la capa); en Windows solo se repintaba el rectangulo y lo de fuera quedaba con los
  puntos de antes. Con tarjeta la barra va por DrawCtrlOverlay y no por WM_PAINT.
- La Font ambiental es Control.DefaultFont: vale porque Form1 nunca pone su Font (ni el panel del disco). Si alguien la pusiera, las vistas sin la suya
  no la heredarian (ni recibirian OnFontChanged por el padre). Se va con la oleada 7.
- BackColor sin poner da el gris de las ventanas de XP (CajaPieza.ColorDeFondo) y no el del dueno; Form1 siempre lo pone antes de leerlo.
- Como las demas vistas sin herencia: sin la capa no tienen Enabled ni foco de teclado propio en la lista nativa (oleada 8).
