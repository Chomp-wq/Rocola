# Port nativo: el panel del disco sin Panel (tanda 4, oleada 5 y 7, grupo adelantado panel-del-disco: W3f)

Seccion «5c. panel-del-disco (W3f)» de PLAN_TANDA4_A_N7.md, sobre 01fcf68. `BgPanel` (el panel con el cielo de la lista de un disco,
`panelSongs`) deja de heredar de `Panel`: es una vista de la pantalla nativa (`IVista`, con su `CajaVista`) que CONTIENE a otras vistas, como
un Panel de WinForms contiene a sus controles. El resultado visible y las trazas no cambian.

## Lo hecho

Codigo/ (cada archivo con su numero de lineas y de CRLF de 01fcf68):

- **Form1.Fondo.cs:2372-2399 (la clase BgPanel)**: `BgPanel : IVista`.
  - El constructor crea su `CajaVista` con la medida de fabrica de un Panel (200x100: DefaultSize) y `RepintaConMedida = false`: el
    `SetStyle` de XP (AllPaintingInWmPaint | UserPaint | OptimizedDoubleBuffer) NO incluye ResizeRedraw, asi que al cambiar de medida un
    Panel no se invalida entero (Control.SetBoundsCore: «if (GetStyle(ControlStyles.ResizeRedraw)) Invalidate()»; la capa lo hace igual:
    Ventanas.Control.cs:89). Esta era la advertencia de la revision de 3a: `CajaVista` avisaba repintar al cambiar de medida siempre.
  - `OnPaintBackground` (vacio) no existe: `Pinta(SKCanvas)` es fondo y contenido. En su linea van los miembros que XP usa del panel por
    fuera y le daba Control: `Bounds`, `Left`, `Top`, `Width`, `Height`, `SetBounds`, `Visible` e `Invalidate` (todos de la caja), y
    `Seleccionable = false` (un Panel no es Selectable).
  - `BackColor`: un campo propio (el gris de los controles de XP si no se pone; Form1 siempre lo pone) que repinta al cambiar
    (Control.OnBackColorChanged). Va en la linea vacia de XP antes de la llave de la clase.
  - `Pinta(SKCanvas g)` (el `OnPaint`): el estado del Graphics de un WM_PAINT (Graphics.FromImage: InterpolationMode.Bilinear -filtro
    Bilineal-, PixelOffsetMode.Default -sin medio pixel- y CompositingMode.SourceOver -sin copia-) es lo que `DrawCached` usa del
    Graphics. `DrawCached` se llama DIRECTO a la traducida (SKCanvas, filtro, medio, copia): ya no pasa por la firma de XP del puente.
  - `this.Parent.ClientSize` pasa a `PantallaRocola.ClienteDelDueno(this, out cliente)`: el cliente de su dueno (la ventana), o falso si
    no esta en ninguna (`: w` / `: h` como en XP).
- **Form1.cs:1244 y 1549** (dos lineas que el plan no nombra; hacia falta): `panelSongs.Controls.Add(lblAlbum)` pasa a
  `PantallaRocola.AgregaRotulo(panelSongs, lblAlbum)` (lblAlbum es un Label de la capa hasta la oleada 7; entra al panel en el mismo
  orden) y `panelSongs.ClientSize.Width/Height` a `panelSongs.Width/Height` (el cliente de un Panel sin borde ni barras es su medida).
  Las lineas 1245-1247 (`AgregaVista(panelSongs, cubeBox / songList / songPager)`) NO cambian: la pantalla es la que ahora las pone
  dentro del panel.

Nativo/:

- **IVista.cs**: `Seleccionable` (miembro por defecto de la interfaz; `true`) y `CajaVista.RepintaConMedida` (`true` de fabrica: las
  demas vistas no cambian). Una frase en la cabecera sobre las vistas que contienen vistas.
- **PantallaRocola.Vistas.cs**: las vistas dentro de una vista.
  - `AgregaVista(dueno, hija)` con un dueno que es `IVista`: si el dueno ya esta en una ventana de la capa, su hueco la contiene
    (`ILaCapa.Agrega`); si no (la pantalla nativa, o el panel aun sin poner en ninguna ventana -XP agrega las hijas al panel ANTES de
    agregar el panel a la ventana, Form1.cs:1244-1247 y 1339-), la pantalla anota las hijas (`hijas`, `padre`: el orden de
    `Controls.Add`, la primera arriba). Al entrar el panel en una ventana de la capa, `TraspasaHijas` las pasa a su hueco en el mismo
    orden (esto es lo que la revision de 3a decia que ahora no hacia).
  - `AgregaRotulo(dueno, rotulo)`: lo mismo para un Label de la capa. `ClienteDelDueno`, `OrigenDe`, `IndiceEnElDueno`.
  - Orden z: `VistaAlFrente` y `VistaAlFondo` de una hija mueven dentro de las de su dueno (SetChildIndex dentro del padre);
    `QuitaVista` la saca de las de su dueno y suelta el raton, la captura o el foco si eran suyos o de algo de lo que lleva dentro.
  - Composicion (`PintaRecortada`/`PintaHijas`): el dueno se pinta recortado por las de encima, y despues sus hijas de abajo arriba,
    cada una recortada al cliente del dueno (WS_CHILD: un hijo no sale del cliente de su padre) y por las hermanas que tiene encima
    (WS_CLIPSIBLINGS), con el origen en su esquina; es lo que hace Ventanas.Escritorio.PintaArbol/PintaHijo con los controles.
  - Raton: `VistaEn` baja por las hijas (ChildWindowFromPointEx: la de mas arriba que se ve y contiene el punto; Control.cs:5806); las
    coordenadas que recibe la vista son las suyas (`OrigenDe` suma las de sus duenos); la rueda sin atender sube al dueno y despues a
    la ventana (DefWindowProc: Control.cs:12748-12764); `DuenoVisible` de una hija es el `Visible` de su dueno (GetVisibleCore).
  - `ILaCapa` gana `AgregaRotulo` y `ClienteDelDueno`.
- Plataforma/Anfitrion.cs: `Agrega` con un dueno que es una vista usa su hueco como el `Panel` que contiene (Controls.Add del hueco: su
  orden, su recorte y su raton son los de la capa que fijan las 41 pruebas); `AgregaRotulo`; `ClienteDelDueno` (Parent.ClientSize del
  control o su hueco); el hueco de una vista no `Seleccionable` apaga Selectable y TabStop (como Panel: si no, un clic en el panel le
  daria el foco).
- Plataforma/Puente.fondo.cs: solo el comentario (BgPanel ya no pide la firma de XP de `DrawCached`; Form1.cs:1062 sigue pidiendola
  hasta W7, como dice el plan). No se borra ninguna linea: `DrawCached(Graphics...)`, `Draw(Graphics...)` y `DrawReactive(Graphics...)`
  las siguen llamando Form1.cs, Form1.Pared.cs y Form1.Buscador.cs.

Pruebas/: `Ventanas/PantallaVistas/Program.cs` (casos nuevos del panel con hijas, abajo), `VistasFondo/Fondo.cs` (el panel por `Pinta`
cuando la clase ya no tiene `OnPaint`; sigue compilando contra la referencia congelada, que tiene el Panel de WinForms).

## Costuras

Quitadas: la herencia de `Panel` de BgPanel; el uso de la firma de XP de `Starfield.DrawCached(Graphics, ...)` por BgPanel (la costura
sigue por Form1.cs:1062).

Anadidas (de la transicion): `ILaCapa.AgregaRotulo` (se va con el Label, oleada 7) y `ILaCapa.ClienteDelDueno` (se va con la capa, N7);
`PantallaRocola.AgregaRotulo` (se va con el Label); `TraspasaHijas` y el anotar hijas de un panel que aun no esta en ninguna ventana
(con la capa; sin ella, el panel entra en la lista nativa y sus hijas ya estan donde van).

## Comprobaciones (12-11-2026, maquina compartida con carga de 6 a 7, llvmpipe)

- a) `dotnet build -c Release --no-incremental`: 0 errores y los mismos 15 avisos de la base.
- b) diff-xp.py -v de Form1.Fondo.cs y Form1.cs: las lineas «sin marca» son las mismas dos de cada uno que antes del cambio (comentarios de
  varias lineas de XP); todas las lineas puestas llevan «EN LINUX». Lineas y CRLF de los dos archivos iguales a 01fcf68 (2402 y 2343;
  2785 y 2741).
- c) NativeAOT (`dotnet publish ... -p:PublishAot=true`): 0 avisos IL/AOT (los 15 de siempre son de la compilacion normal).
- Arnes VistasFondo (el panel del disco por `Pinta`, 24 casos: tres medidas, dos atenuaciones, dos acentos, con y sin recorte): 2763 lineas
  contra la referencia congelada, SOLO distinta la primera linea (la marca `traducida=`). El arnes llama al panel nuevo por `SetBounds` y
  `Pinta(lienzo del mismo Graphics)` y al de la referencia por `OnPaint`.
- PantallaVistas (pruebas_vistas.sh): **93 de 93** (59 de antes + 34 nuevas): el panel con dos hijas agregadas ANTES de poner el panel en la
  ventana, en tres escenas con los mismos pasos (posicion relativa, recorte al panel, orden, raton con las coordenadas de la hija, captura,
  BringToFront, mover y esconder el panel, quitar una hija): (1) con un Control de la capa de panel y de hijas -lo que fija WinForms-,
  (2) con vistas falsas, el panel en su hueco de la capa, (3) sin la capa, en la lista nativa. Las tres dan lo mismo y lo que cada paso dice
  de WinForms.
- f) pruebas41: **41 de 41**; pruebas_hoja: **62 de 62**; `AOT=1 pruebas_pantalla`: **32 de 32** en JIT y NativeAOT (sus 2 avisos CS0649
  de siempre).
- d) Recorridos (con el candado y la carpeta de siempre; pared, buscador y video; la pared incluye entrar a un disco, que es donde se pinta
  el panel): 3 de 3 corren; capas quietas: pared 24 de 24, buscador 22 de 22, video 16 de 16 (en la segunda pasada: en la primera
  `c24` perdio una capa del video por el ritmo de la maquina); texturas quietas: pared 81 de 83, buscador 75 de 78, video 50 de 50. Las
  5 que faltan son 1024x128/512x256 de los rotulos que dependen de la hora y de cuantos pasos da la rocola; la base (01fcf68) corrida a la
  misma hora y con la misma carga da peor (buscador 56 de 78, pared 82 de 83). Ninguna diferencia nueva.
- e) Escenas alternadas con la referencia congelada (ref-trazas), JIT, arranque, moneda, video, tab, buscador y f3raton: la secuencia de
  sucesos, las teclas, el estado y los pixeles IGUALES; f3raton IGUAL entera; las demas solo por las cuentas del reloj Form1.cs:724 y del
  encargo MedidasBitacora.cs:129 fuera de la holgura (carga de la maquina, en las dos direcciones: a veces mas la referencia, a veces la
  nueva), repetidas una vez (RPD2/PD2) con lo mismo. tab, ademas, los rotulos de lo que el repaso de fondo lleva medido al abrir Ctrl+F3
  («Calidad: 12 aceptables» frente a «8», «33 %» frente a «0 %») y por eso 5 080 puntos de la capa de esa hoja: es lo que PORT_armado-vistas.md
  ya documento para esta escena (depende de la carga; no toca el repaso ni Ctrl+F3). NativeAOT (con la fecha de dotnet en rocola-xp) contra
  la referencia JIT: arranque IGUAL salvo las cuentas del reloj.
- No corridas: las 29 cintas y el banco (no se toco nada de lo que se sube a la tarjeta ni de Nativo/ de dibujo; el panel pinta con la misma
  llamada y el mismo estado, lo comprueba el arnes del fondo).

## Lo que queda

- `lblAlbum` sigue siendo un Label de la capa dentro del panel (oleada 7): entra por `PantallaRocola.AgregaRotulo`. Sin la capa no hay
  forma de pintarlo: cuando sea una vista, pasara a `AgregaVista(panelSongs, lblAlbum)` y se borra `AgregaRotulo`.
- `cubeBox`, `songList` y `songPager` aun heredan de Control (5b y 6b los sueltan): ya entran por `AgregaVista(panelSongs, ...)`, que sirve
  igual cuando sean vistas sin herencia (probado con vistas falsas).
- `Starfield.DrawCached(Graphics, ...)` del puente: lo sigue pidiendo Form1.cs:1062 hasta W7.

## Riesgos

- El hueco del panel es un `Control` de la capa y no un `Panel`: se le apagan Selectable y TabStop como a un Panel, pero no tiene
  ScrollableControl (AutoScroll, DisplayRectangle, AllPaintingInWmPaint apagado que pone ScrollableControl): nada de eso lo usa la rocola
  (BgPanel lo vuelve a encender). Se va con la capa (N7).
- `QuitaVista(panel)` con la capa desecha el hueco y, con el, a las hijas que lleve (Control.Dispose desecha a los hijos); WinForms al quitar
  con `Controls.Remove` no las desecha. La rocola nunca quita `panelSongs`.
- Sin la capa, desechar el panel no desecha a sus hijas (no hay quien las desee: el desecho de Form1 es de la oleada 8).
- La rueda sin atender sube del hijo al dueno y a la ventana (en XP, DefWindowProc la pasa al padre); la rocola no da el foco a una hija
  del panel, asi que hoy no se usa.

## Revision (revisor independiente, 12-11-2026)

Leido el diff entero (01fcf68..3ea42d2) contra Rocola/Form1.Fondo.cs y Rocola/Form1.cs y la fuente de referencia de WinForms
(/tmp/claude-0/agente-winforms/ref: Control.cs, CreateParams con WS_CLIPCHILDREN siempre; Invalidate() = Invalidate(false); el BackColor
ambiental). Lo comprobado:

- BgPanel: la logica de OnPaint igual linea por linea (Width/Height, el corte de 4, Sky nulo, SkyOffset por Left/Top, SkyFull por el cliente
  del dueno, SkyMid, Acento, Dim y DrawCached); el estado que recibe DrawCached es el de un Graphics de WM_PAINT (Bilinear, sin medio pixel,
  SourceOver), el mismo que daba la capa por el puente (Dibujo2D: InterpolationMode.Bilinear de fabrica; PintaSolo guarda y devuelve el
  estado). SetStyle sin ResizeRedraw: RepintaConMedida = false, y el hueco de la capa tampoco lo lleva (Ventanas.Control.cs: estilos de
  fabrica). Panel de la capa (Ventanas.Control.cs:820) apaga Selectable y TabStop: el hueco tambien.
- Las hijas: XP las agrega al panel antes de poner el panel en la ventana (Form1.cs:1244-1247 y 1339); entre esas lineas nadie mira su
  Parent ni su Visible, asi que anotarlas y pasarlas al hueco al entrar el panel da el mismo arbol y el mismo orden (lblAlbum arriba).
  Sus BackColor los pone todos Form1 (1186-1193 y 1244; Form1.Video.cs:460-464): el ambiental del hueco no se ve.
- Las firmas Draw y DrawReactive del puente (las «:43 y :53» del plan) las siguen pidiendo Form1.Pared.cs:114 y 1527 y Form1.Buscador.cs
  (por su puente): se quedan; DrawCached(Graphics) se queda por Form1.cs:1062.
- Lineas y CRLF de Codigo/ iguales a 01fcf68 (Form1.Fondo.cs 2402/2343, Form1.cs 2785/2741); diff-xp -v: solo las 2 lineas sin marca de la
  base en cada archivo.

Lo encontrado y arreglado (solo comentarios; ningun cambio de logica):

- Nota vieja en Form1.Fondo.cs:1752 (Starfield.DrawCached traducida): decia que la firma del puente era para «Form1.cs y BgPanel.OnPaint»;
  BgPanel ya la llama directo. Corregida (una linea fuera del rango 2372-2397, lejos de lo que toca 4a: ~771, ~1021, ~1147).
- La nota de BgPanel.BackColor decia que sin poner es el gris de XP; en WinForms es el del dueno (ambiental) o, sin dueno,
  SystemColors.Control. Precisada (no cambia nada: Form1 siempre lo pone).

Repetido por el revisor: a) build Release --no-incremental 0 errores y los 15 avisos de la base; b) diff-xp igual que la base; c) NativeAOT
0 avisos IL/AOT; arnes VistasFondo: 2763 lineas, solo distinta la marca (24 casos del panel por Pinta); PantallaVistas 93 de 93; pruebas41
41 de 41; pruebas_hoja 62 de 62; AOT=1 pruebas_pantalla 32 de 32 (JIT y NativeAOT). Recorridos y escenas no repetidos (la revision no los
pone en duda: el cambio del revisor es solo de comentarios).

Riesgos que anade la revision:

- Sin la capa (lista nativa), PintaHijas pinta el panel entero y despues las hijas encima; en XP el panel tiene WS_CLIPCHILDREN y no pinta
  debajo de sus hijos (y una hija transparente pide al padre su fondo, PaintTransparentBackground). El resultado es el mismo mientras el
  Pinta del panel no dependa del recorte (BgPanel no depende: usa Width/Height). Tampoco mira la Region de las hermanas de encima (la capa
  si: Ventanas.Escritorio.PintaHijo); ninguna hija del panel tiene Region.
- Las tablas hijas/padre son estaticas: lo que se agrega a un panel queda referido hasta QuitaVista (la rocola no lo quita nunca).
