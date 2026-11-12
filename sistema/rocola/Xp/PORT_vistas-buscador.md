# Port nativo: la casilla de BUSCAR y el buscador sin herencia (oleada 6c, grupo vistas-buscador: W3c)

Seccion «6c. vistas-buscador (W3c)» de PLAN_TANDA4_A_N7.md, sobre d05b06d. Lo mismo que vista-ayuda hizo con AyudaView y panel-del-disco con
BgPanel (PORT_vista-ayuda.md, PORT_panel-del-disco.md): BuscaView (la casilla de BUSCAR) y SearchOverlay (el buscador de siempre) dejan de
heredar de Control y son vistas de la pantalla nativa (IVista) con su CajaVista. El foco del teclado entre las vistas, en la lista nativa
(sin la capa), lo lleva la pantalla (Nativo/PantallaRocola.Foco.cs). Con la capa nada cambia: el hueco de cada vista toma el Selectable y el
TabStop de XP.

## Lo hecho

Codigo/Form1.Buscador.cs (1602 lineas y 1541 CRLF, las mismas que en d05b06d; todas las lineas cambiadas con «EN LINUX»):

- **BuscaView** (:175): `private class BuscaView : IVista, IDisposable` (vuelve a no ser partial, como en XP).
  - :180 (la linea vacia de XP antes del Dispose): su CajaVista y lo de IVista (Bounds, Left, Top, Width, Height, SetBounds, Visible,
    Invalidate) y `ParaElTabulador = false` (el `TabStop = false` de su constructor).
  - :181 y :185: `IDisposable.Dispose() => Dispose(true)` y `Dispose(bool)` privado; el `base.Dispose` lo hace su hueco despues (como
    AyudaView y HelpBar).
  - :190-192 (el constructor): `caja = new CajaVista(this) { RepintaConMedida = false }` en la linea del SetStyle (su SetStyle no pone
    ResizeRedraw, y Control no lo trae: al cambiar de medida no se repinta entera, como el panel del disco); BackColor transparente (el
    OnPaintBackground de fabrica no pinta nada) y TabStop = false, en comentarios con donde quedaron.
  - :227-230 OnPaint -> `Pinta(SKCanvas g)`: `AjusteLetra ajuste = ClearTypeAjustada` (el `TextRenderingHint = ClearTypeGridFit` de su OnPaint)
    y `RenderContent(g, ajuste, false, false, false)` (XP: `RenderContent(e.Graphics)` = `RenderContent(g, false)`): sin copia ni medio pixel,
    el Graphics de un WM_PAINT de la capa (Graphics.FromImage; Escritorio.PintaSolo). El suavizado que la firma de XP le dejaba al Graphics se
    iba con el. Notas de :233 y :237 corregidas (ya no hay puente).
- **SearchOverlay** (:320): `private partial class SearchOverlay : IVista` (sigue partial: sus otras partes de XP en AcabadoGpu.cs,
  BuscadorPortadasGpu.cs, Form1.Biblioteca.cs y AsistenteGuiaRocola.cs, que no cambian).
  - :553 (la linea vacia de XP antes del constructor): su CajaVista y lo de IVista; `Invalidate(Rectangle)` (XP la llama en 599, 601, 607, 649
    y 691), que la repinta entera como la capa (el rectangulo solo acota en Windows y Pinta pinta todo: se ve lo mismo); `ClientRectangle`
    (Form1.Video.cs:879 lo lee para el clic con tarjeta: el cliente de un control sin borde, su medida en el origen; asi Form1.Video.cs, de
    6a, no se toca); `BackColor` (un campo que repinta al cambiar, Control.OnBackColorChanged). Seleccionable y ParaElTabulador, los de
    fabrica de IVista (true): su Selectable y su `TabStop = true`.
  - :556-558 (el constructor): la CajaVista con `RepintaConMedida = false` en la linea del SetStyle (AllPaintingInWmPaint, UserPaint,
    OptimizedDoubleBuffer y Selectable: sin ResizeRedraw); el TabStop en comentario; el BackColor (8, 12, 36) al suyo.
  - :786 ClickAt: `Raton r = ...; OnMouseClick(r); PantallaRocola.AvisaOyentes(this, AvisoRaton.Clic, r)`: el aviso MouseClick que en XP lanzaba
    el base.OnMouseClick al final (hoy nadie lo escucha).
  - :789 `protected override void OnMouseClick(MouseEventArgs e)` -> `void IVista.Raton(que, e) { if (que == Clic) OnMouseClick(e); } private
    void OnMouseClick(Raton e)`: el raton se lo da la pantalla (el hueco en su OnMouseClick, o la lista nativa) y el aviso MouseClick lo lanzan
    ellos despues (AvisaOyentes), en el mismo sitio que el base.OnMouseClick de XP (al final de cada camino). :792 `e.Location` ->
    `new Point(e.X, e.Y)`; :798, :802, :809 y :811 sin el `base.OnMouseClick(e)` (en comentario).
  - :814 OnPaint -> `Pinta(SKCanvas g)`: el WM_PAINT entero, primero el OnPaintBackground de fabrica (Control.PaintBackColor: con BackColor
    opaco, el cliente de ese color: PinturaXp.Rellena, lo mismo que CajaPieza.PintaFondoDeFabrica y Ventanas.Control.OnPaintBackground) y
    despues `RenderContent(g, DelSistema, false, ref Bilineal, false, g.TotalMatrix, cliente, false)`: el estado de un Graphics de WM_PAINT de
    la capa (modo de letra SystemDefault, sin copia, filtro Bilinear, sin medio pixel; la matriz del HDC, la de su esquina) y el recorte que da
    la capa (el cliente entero). El hueco tiene el OnPaintBackground vacio: por eso el fondo va en Pinta. Nota de :938 corregida.
  - :961-962 `this.Parent.ClientSize` -> `PantallaRocola.ClienteDelDueno(this, out cliente)` (como BgPanel).

Plataforma/Puente.buscador.cs: se borran las dos firmas de XP de los OnPaint (`BuscaView.RenderContent(Graphics)` y
`SearchOverlay.RenderContent(Graphics, Rectangle, bool)`), con sus clases parciales y sus `using`; queda el archivo solo con comentarios
(lo borra la limpieza o N7).

Nativo/ (el foco del teclado):

- **PantallaRocola.Foco.cs** (nuevo): en la lista nativa (sin la capa) las reglas de WinForms del foco entre las vistas, como Hoja.Foco.cs
  con las piezas: el TabIndex que pone Controls.Add (Control.ControlCollection.cs:82-95: el orden del alta), SelectNextControl/GetNextControl
  (Control.cs:10709-10721 y 6177-6351) con CanSelect = Seleccionable y Visible (Control.CanSelectCore) y TabStop = ParaElTabulador, Tab y
  Mayus+Tab (Form.ProcessTabKey, Form.cs:4995-5015) y las flechas (ContainerControl.ProcessArrowKey, :1119-1128), solo sin Alt ni Control
  (ContainerControl.ProcessDialogKey, :1156-1185), y SelectNextIfFocused al esconder la vista que tiene el foco (Control.SetVisibleCore,
  Control.cs:11221-11228 y 10770-10784; GetContainerControl da la ventana: IsContainerControl es false en la Form, Control.cs:2900 y
  5825-5840). Control.Focus sigue en EnfocaVista (no mira TabStop ni Selectable, como FocusInternal).
- PantallaRocola.Vistas.cs: tres lineas: `AnotaTab` al entrar una vista en la lista nativa o en un panel nativo, y en `LlegaTecla`, despues
  de la ProcessDialogKey de la ventana (TeclaDeDialogo), la parte de ContainerControl (Tab y flechas); comentarios al dia.
- IVista.cs: el setter de `CajaVista.Visible` avisa `PantallaRocola.SeEsconde` antes de esconder una que se veia (en un hueco no hace nada:
  lo hace la capa); comentarios de Seleccionable y ParaElTabulador al dia.

Pruebas/:

- **VistasBuscador/Buscador.cs**: la medida por `PonMedida`/`Medida` (Size en la referencia; SetBounds y Width/Height en la traduccion);
  donde llamaba a las firmas de XP de los OnPaint (ya no estan) hace lo que hacia el puente (la traducida con el estado del Graphics y
  despues el suavizado y el filtro que XP le dejaba: `CasillaSinPuente` con forGpu = false y `GrandeSinPuente`). Caso NUEVO «pinta-»
  (5): el WM_PAINT como lo da la capa (trasladado, recortado, Save y SellaRecorte, como Escritorio.PintaArbol/PintaSolo): en la referencia
  `PintaFondoYContenido` del control (OnPaintBackground y OnPaint de XP), en la traduccion `Pinta` en el lienzo de ese Graphics (lo que hace
  su hueco); la casilla (3 altos x cerrada/abierta x 2 sitios) y el buscador (3 medidas x 3 contenidos x 2 sitios).
- **VistasBuscadorPortadas/BuscadorPortadas.cs**: lo mismo (medida y `GrandeSinPuente` con forGpu = true).
- **Ventanas/PantallaVistas/Program.cs**: escena NUEVA del tabulador (EscenaTab), con un Control de la capa (lo que fija WinForms en la capa),
  con la vista falsa en su hueco y sin la capa (la lista nativa): B (Selectable, TabStop = false, la casilla), S y X (Selectable y TabStop,
  el buscador) y N (sin Selectable, un panel): Focus, Tab x4, Mayus+Tab x2, Focus de la que no tiene TabStop, Tab desde ella, flechas,
  esconder la del foco y Tab con una escondida. La vista falsa gana `Sel`/`Tab` y el gemelo un constructor con Selectable y TabStop.

## Costuras

Quitadas: la herencia de Control de BuscaView y SearchOverlay; `BuscaView.RenderContent(Graphics)` y
`SearchOverlay.RenderContent(Graphics, Rectangle, bool)` de Puente.buscador.cs.
Anadidas: ninguna de la transicion (PantallaRocola.Foco.cs es de la pantalla nativa y se queda).
Quedan (de otros): el archivo Puente.buscador.cs solo con comentarios (limpieza o N7); el hueco de la capa (Anfitrion.cs, N7).

## Comprobaciones

(abajo, con numeros)

## Riesgos

(abajo)
