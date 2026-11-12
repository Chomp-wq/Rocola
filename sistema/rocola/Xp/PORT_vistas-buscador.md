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

## Comprobaciones (12-11-2026, maquina compartida a carga 2-6, llvmpipe)

- a) `dotnet build -c Release --no-incremental`: 0 errores y los 15 avisos de la base (mismos archivos y lineas: comparados con d05b06d).
- b) diff-xp.py Form1.Buscador.cs -v: 261 lineas quitadas, 258 puestas, **0 sin marca**. 1602 lineas y 1541 CRLF, como en d05b06d (BOM
  conservado).
- c) NativeAOT (`dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true`): sale con 0, **0 avisos IL/AOT**.
- e) Arnes VistasBuscador: **IGUALES** a la referencia congelada con las letras falsas y con DejaVu, 4739 lineas (467 llamadas directas en
  la traduccion; 0 excepciones), con los 30 casos nuevos «pinta-» (OnPaint de XP con su fondo contra Pinta). Arnes VistasBuscadorPortadas:
  **IGUALES** con las dos letras, 3128 lineas (250 directas; 5 excepciones en las dos, las mismas: casos del arnes).
- PantallaVistas (pruebas_vistas.sh): **117 de 117** (93 de antes + 24 del tabulador: 8 pasos con el control de la capa, 8 con la vista
  falsa en su hueco -iguales al control- y 8 sin la capa, con lo que dice WinForms).
- f) pruebas41: **41 de 41**; pruebas_hoja: **86 de 86**; `AOT=1 pruebas_pantalla`: **32 de 32** en JIT y en NativeAOT (sus 2 lineas de
  avisos de siempre).
- El tabulador de la ventana principal (escena propia con corre.sh, 16 Tab desde los 7 s, traza del foco), alternada con la referencia:
  - buscador cerrado: referencia y traduccion `HelpBar LetterBar AlbumView ProgressView GpuPanel` x3 + `HelpBar LetterBar`, iguales (y la
    escena entera IGUAL con compara_escena.py);
  - buscador abierto, sin tarjeta (rocola_config.txt con gpu=0: el buscador por procesador, visible y con el foco): las dos
    `HelpBar SearchOverlay HelpBar LetterBar AlbumView ProgressView SearchOverlay ...` (17 cambios de foco iguales; la casilla de BUSCAR,
    con TabStop = false, no esta en el ciclo); la escena entera IGUAL.
- Escenas alternadas con la referencia congelada (JIT, W y SAL propios, datos0 de siempre, ruido de ventanas/ruido): **arranque IGUAL,
  buscador IGUAL, tab IGUAL**. NativeAOT (con la fecha de dotnet en rocola-xp) contra la referencia JIT: **buscador IGUAL**.
- El buscador sin tarjeta (gpu=0; los pasos de la escena buscador): la traza IGUAL (foco `HelpBar SearchOverlay Form` en las dos, el mismo
  estado); las capturas difieren en 17-59 mil puntos del cielo por procesador, que se mueve con el reloj: la referencia contra si misma
  (otra corrida) da lo mismo (17 451, 17 375 y 58 345 frente a 17 353, 17 149 y 58 992). En NativeAOT, igual (21 513, 17 348, 58 676).
- d) Recorridos (con el candado y la carpeta de siempre; solo «buscador»): capas quietas **22 de 22**; texturas quietas: 45 de 78 en la
  primera pasada; la base d05b06d corrida en seguida dio 44 de 78 y la traduccion repetida **76 de 78** (faltan dos 1024x128, de las que
  dependen de la hora y de los pasos). Ninguna diferencia nueva.
- No corridos: el banco y las 29 cintas: no se toco ninguna funcion de dibujo de Nativo/ ni Puente.cs, ni lo que se sube a la tarjeta
  (EnsureUiOverlay y DrawCtrlOverlay llaman a las mismas traducidas; los recorridos lo confirman con las capas y las texturas).

## Riesgos

- **La capa no sigue a WinForms al esconder la vista del foco** (lo vio la prueba del tabulador): Ventanas.Control.SelectNextIfFocused pide
  el contenedor con ContenedorDelFoco, que salta la ventana porque es ContainerControl; WinForms (GetContainerControl, Control.cs:5825-5840)
  solo salta las que dan IsContainerControl, que es false en la Form (Control.cs:2900): en WinForms el foco pasa a la siguiente, en la capa
  se queda en la escondida. Con la rocola: al cerrar el buscador sin tarjeta, el foco se queda en el buscador escondido (la referencia
  igual, por eso las escenas salen iguales). No se nota (las teclas van a Form1 por ProcessCmdKey y KeyPreview, y el Tab sigue desde la
  escondida). La lista nativa (sin la capa) hace lo de WinForms. Se va con la capa (N7); no se toco la capa.
- Invalidate(Rectangle) repinta el buscador entero (como la capa); en XP solo el rectangulo, pero su Pinta pinta todo y lo que se ve es lo
  mismo. Pinta recibe siempre el cliente entero como recorte de RenderContent (lo que da la capa).
- `ClientRectangle` del buscador es un miembro con el nombre de Control (lo lee Form1.Video.cs:879): solo eso; cuando 6a toque esa linea
  puede pasar a `new Rectangle(0, 0, Width, Height)` y quitarlo.
- RepintaConMedida = false en las dos (sin ResizeRedraw como en XP): con la capa es lo que hacia el control (Control.SetBoundsCore);
  en la lista nativa al cambiar de medida no se pinta entera, como en Windows.
- En la lista nativa las flechas sin atender mueven el foco entre las vistas (ContainerControl.ProcessArrowKey), como en WinForms; en la
  rocola ProcessCmdKey de Form1 atiende las flechas antes, asi que no llega.
- Quitar (QuitaVista) la vista del foco en la lista nativa suelta el foco sin elegir otra (como antes); WinForms (ContainerControl al
  quitar el activo) elige otro. La rocola no quita vistas.

## Revision (revisor independiente, 12-11-2026)

Leido el diff d05b06d..port-vistas-buscador entero contra Rocola/Form1.Buscador.cs y la fuente de WinForms (/tmp/claude-0/agente-winforms/ref).

Lo comprobado sin fallos:
- BuscaView y SearchOverlay sin Control; el constructor conserva lo de XP: BuscaView no Selectable ni TabStop en el ciclo (TabStop = false ->
  ParaElTabulador false; el hueco hace TabStop = Seleccionable && ParaElTabulador), SearchOverlay Selectable y TabStop = true, BackColor
  (8, 12, 36) en el mismo sitio del constructor, BuildKeys despues; ninguno con ResizeRedraw (RepintaConMedida = false).
- El estado que reciben las traducidas desde Pinta es el que daba el puente con el Graphics del WM_PAINT de la capa: Escritorio.PintaSolo
  guarda el estado del Graphics recien hecho (Graphics.FromImage: SystemDefault, SourceOver, Bilinear, sin medio pixel) y SellaRecorte pone
  la matriz del HDC = la matriz del lienzo, que es la g.TotalMatrix que pasa Pinta; ClearTypeGridFit en la casilla como en su OnPaint.
- El fondo del buscador: antes lo pintaba el OnPaintBackground de la capa (FillRectangle sin suavizar del BackColor en el cliente); ahora el
  hueco no pinta fondo y Pinta lo rellena igual (PinturaXp.Rellena sin suavizar). La casilla, transparente, no pinta fondo (antes tampoco).
- OnMouseClick: cuerpo de XP; el aviso MouseClick (base.OnMouseClick) despues, por el hueco o la lista nativa (AvisaOyentes) y en ClickAt.
- Vidas: fTxt se suelta en Dispose(true) por el hueco (IDisposable, como el Control.Dispose de los hijos); igual que AyudaCasillaGpu.cs y
  Form1.cs (el modelo). Ningun tipo de la capa queda en Form1.Buscador.cs; Puente.buscador.cs sin codigo.
- PantallaRocola.Foco.cs contra la fuente: GetNextControl (adelante y atras, con los empates por «hitCtl»), GetNextSelectableControl
  (wrap y alreadyWrapped), ContainerControl.ProcessDialogKey (Tab y flechas sin Alt ni Control, despues del ProcessDialogKey de Form1, que
  XP no sobreescribe), ProcessArrowKey (el grupo es el padre del activo) y SelectNextIfFocused antes de cambiar Visible (SetVisibleCore),
  con GetContainerControl = la ventana (IsContainerControl es false en la Form: Control.cs:2900 y 5825-5840). Bien.

Lo encontrado y arreglado:
- (menor) Codigo/Form1.Buscador.cs:10, la nota del using decia aun que «el Graphics de WinForms de OnPaint pasa por el puente
  (Plataforma/Puente.cs y Puente.buscador.cs)»: corregida (ya nada pasa por el puente; sus OnPaint son Pinta). Mismas lineas y CRLF.

Lo que queda (no es de este grupo o no se cambia):
- Codigo/BuscadorPortadasGpu.cs:697 y :827 dicen que «a quien aun pasa un Graphics el puente de este (Plataforma/Puente.buscador.cs) le deja
  el suavizado»: ese puente ya no existe. El archivo no es de este grupo: lo corrige quien lo toque (o 9b).
- En la lista nativa el tabulador solo recorre IVista: lo que en XP es un control seleccionable y no es vista (el GpuPanel, que esta en el
  ciclo de XP: «... ProgressView GpuPanel ...») queda fuera. Con la capa no pasa (el GpuPanel es un control de la capa); cuando el GpuPanel
  deje de serlo (oleada 8), hay que darle su sitio en el orden del tabulador o el ciclo de la lista nativa no sera el de XP.
- El TabIndex de la lista nativa es un contador comun por orden de alta; WinForms da el mayor de los hermanos mas uno (Control.
  ControlCollection.cs:82-95). Da el mismo orden mientras ninguna vista cambie de dueno (la rocola no mueve vistas entre duenos); el
  diccionario tabIndex no suelta las vistas (la rocola no las tira).

Comprobaciones repetidas por el revisor:
- `dotnet build -c Release --no-incremental`: 0 errores, 15 avisos (ninguno en archivos de este grupo; los mismos archivos y lineas).
- diff-xp.py Form1.Buscador.cs -v: 261 quitadas, 258 puestas, 0 sin marca; 1602 lineas y 1541 CRLF, como en d05b06d.
- Arnes VistasBuscador: IGUALES a la referencia con letras falsas y con DejaVu (4739 lineas, 467 directas, 0 excepciones; 30 «pinta-»).
- Arnes VistasBuscadorPortadas: IGUALES con las dos letras (3128 lineas, 250 directas, 5 excepciones en los dos lados).
- PantallaVistas (pruebas_vistas.sh): pasan 117, fallan 0.
- NativeAOT publish: sale con 0, 0 avisos IL/AOT.
- No repetidos (lo del codigo de este arreglo es un comentario): recorridos, escenas, pruebas41, hoja y pantalla.
