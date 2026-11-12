# Port nativo: el armado de las vistas (tanda 6, oleada 3 adelantada, grupo armado-vistas: W3-0 con W3e)

Seccion «3a. armado-vistas» de PLAN_TANDA4_A_N7.md, sobre bae626e. Es el paso intermedio del diseno: las vistas de la rocola cumplen IVista
(Nativo/IVista.cs) y todo lo que XP hace con ellas desde fuera de su clase (ponerlas en la ventana, traerlas al frente, darles el foco,
escuchar su raton, pintarlas en la imagen de la interfaz) pasa por la pantalla nativa (Nativo/PantallaRocola.Vistas.cs), que mientras dure
la capa se lo pide a ella (PantallaRocola.ILaCapa). Asi, cuando cada grupo de la oleada 5 y 6 quite la herencia de su vista, solo toca su
archivo. La barra de ayuda (HelpBar, W3e) ya queda sin herencia. El resultado visible y las trazas no cambian.

## Lo hecho

Codigo/ (solo las lineas del grupo; cada archivo con su numero de lineas y de CRLF de bae626e):

- **Las cabeceras**: las 14 vistas que aun heredan declaran «, IVista» (AyudaView; BuscaView y SearchOverlay; BgPanel; AttractView,
  AlbumView y CubeBox; SongPagerBar, LetterBar, HeaderView, PastillaView, GeneroView, ProgressView y SongListView). Control les da Bounds,
  Left, Top, Width, Height, SetBounds, Visible e Invalidate con la misma firma; Pinta y Raton son los de fabrica de IVista (la vista la sigue
  pintando y moviendo la capa). Las notas viejas de SongPagerBar y LetterBar («DrawCtrlOverlay ... la llama asi») se corrigieron: hoy
  DrawCtrlOverlay las llama directo.
- **Form1.cs:1122-1345**: los Controls.Add de las vistas pasan a `PantallaRocola.AgregaVista(dueno, vista)`, en el mismo orden y en las
  mismas lineas, mezclados con los Controls.Add de las etiquetas, que siguen (Label de WinForms hasta la oleada 7: lblStatus, lblNow...,
  lblConfig en 1357). Los de dentro del panel del disco (1245-1247) van con el panel como dueno (lblAlbum, una etiqueta, sigue).
  Con la capa, AgregaVista hace el mismo Controls.Add (o el del hueco de una vista sin herencia): el mismo orden z.
- **El raton de fuera** (Form1.cs:1125-1126 y 1334): `ayudaView.MouseClick +=` y `attractView.MouseDown +=` pasan a
  `PantallaRocola.OyeRaton(vista, AvisoRaton.Clic/Abajo, delegate (Raton e) {...})`; MouseButtons.Left pasa a BotonRaton.Left (mismo numero).
- **Form1.Video.cs:1021, 1026 y Form1.Biblioteca.cs:3217, 3319, 3320**: BringToFront y Focus de las vistas pasan a
  `PantallaRocola.VistaAlFrente` y `PantallaRocola.EnfocaVista`.
- **Form1.Video.cs:1779 y 1854**: DrawCtrlOverlay y EnSeco reciben `IVista` (solo leen el sitio, la medida y la clase).
- **HelpBar sin herencia (Form1.cs:2654-2734, W3e)**: `private class HelpBar : IVista, IDisposable` (vuelve a no ser partial, como en XP).
  Su CajaVista (la crea el constructor, en la linea del SetStyle) da Bounds, Left, Top, Width, Height, SetBounds, Visible, Invalidate y
  Bottom (Form1.Video.cs:1243 lo lee); van en la linea vacia de XP entre el Dispose y el constructor. `Pinta(SKCanvas)` (en la linea de su
  OnPaint) llama directo a `RenderContent(g, AjusteLetra.ClearTypeAjustada, false, false, false)`: el estado del Graphics de un WM_PAINT de
  la capa (Graphics.FromImage: sin copia ni medio pixel, con Save/Restore por control: Escritorio.PintaSolo) mas el ClearTypeGridFit que le
  ponia su OnPaint; lo que XP le dejaba al Graphics (el suavizado) se iba con el. El fondo: BackColor transparente, cuyo OnPaintBackground
  de fabrica no pinta nada en la capa (Ventanas.Control.cs:509-515: «if (c.A == 0) return»). Dispose(bool) queda privado, con
  `IDisposable.Dispose() => Dispose(true)`; el `base.Dispose(disposing)` lo hace su hueco despues (ver Anfitrion). Form1.Portadas.cs no cambia
  (lo suyo de ventanas es 1199-1202, de 6b).

Plataforma/ y Nativo/:

- **Nativo/PantallaRocola.Vistas.cs**: `EnfocaVista` (Control.Focus: FocusInternal hace SetFocus solo si CanFocus -visible y habilitada- y
  devuelve Focused; con la capa, el Focus del control o de su hueco; en la lista nativa, la vista que se ve pasa a ser `FocoDeLaVentana`,
  con su traza «foco»), `OyeRaton` y `AvisaOyentes` (solo Abajo = MouseDown y Clic = MouseClick, lo que usa la rocola: los eventos los
  lanza el On* de fabrica de Control, despues de lo que hace la vista en su On*; con la capa, el evento del control; sin ella, una lista que
  la pantalla avisa en `Avisa` despues de `Raton`, en orden de suscripcion como un evento multicast). ILaCapa gana `Enfoca` y
  `OyeComoControl`.
- **Plataforma/Anfitrion.cs**: VentanasDeLaCapa implementa los dos nuevos (Control.Focus; MouseDown/MouseClick += con el MouseEventArgs como
  Raton). El hueco (HuecoDeVista) avisa a los oyentes despues de Raton en OnMouseDown/OnMouseClick, y al desecharse con su ventana (Control.
  Dispose desecha a los hijos) desecha antes a su vista si es IDisposable (el orden de la vista que heredaba: lo suyo y despues
  base.Dispose); quitado de la lista (Controls.Remove) no la desecha, como WinForms. `Anfitrion.VistaDeHueco` da la vista de un hueco.
- **Plataforma/TrazaVentanas.cs** (una linea): Pieza nombra un hueco por su vista («HelpBar», como cuando era el control), para que las
  trazas de raton y foco y el volcado del estado no cambien.
- **Plataforma/Puente.video-form1.cs**: se borra la firma de XP `HelpBar.RenderContent(Graphics, bool)` (la de su OnPaint, :34-46 de bae626e)
  y el EnSeco del puente (:29) recibe IVista (lo llama Form1.Portadas.cs:1199 con attractView).

Fuentes de WinForms citadas (dotnet/winforms release/3.0, /tmp/claude-0/agente-winforms/ref): ControlCollection.Add (al final de la
coleccion = el fondo del orden z) y BringToFront (SetChildIndex(this, 0)), ya en PantallaRocola.Vistas.cs; Control.Focus/FocusInternal/
CanFocus; Control.OnMouseDown y OnMouseClick (lanzan Events[EventMouseDown]/[EventMouseClick]); Control.Dispose(bool) (desecha a los
hijos; ControlCollection.Remove no).

## Costuras

Quitadas:
- `HelpBar.RenderContent(Graphics, bool)` de Puente.video-form1.cs (el OnPaint de la barra ya no existe).
- La herencia de HelpBar de Control de la capa.

Anadidas o cambiadas (de la transicion):
- `ILaCapa.Enfoca` y `ILaCapa.OyeComoControl` (y su implementacion en Anfitrion.cs): se van cuando ninguna vista sea control de la capa
  (oleadas 5 y 6; con la capa, N7).
- `HuecoDeVista`: los oyentes, el desecho de su vista y `VistaDeHueco` (con la linea de TrazaVentanas.Pieza): con el hueco (oleada 8/N7).
- El EnSeco del puente con IVista: se borra en 6b con el precalentado de la atraccion por Pinta.

Quedan (no son de este grupo): la nota de Form1.cs:16 (el using) aun nombra «los Graphics de OnPaint»: es de quien toque esa linea
(W6/W7). Form1.Portadas.cs:1199-1202 (InvokePaint de attractView), de 6b.

## Comprobaciones (12-11-2026, maquina compartida, llvmpipe)

- a) `dotnet build -c Release --no-incremental`: 0 errores, 15 avisos, los de la base (SYSLIB0054, SYSLIB0050, CA2022, CA2014, CS1690;
  mismos archivos y lineas, ninguno en lo tocado).
- b) diff-xp.py -v de los 9 archivos de Codigo/ tocados: las lineas «sin marca» son las mismas que antes del cambio (2, 4, 2, 0, 2, 1, 0,
  0, 2: comentarios de XP de varias lineas); todas las lineas nuevas llevan «EN LINUX». Lineas y CRLF de cada archivo iguales a bae626e.
- c) NativeAOT (`dotnet publish ... -p:PublishAot=true`): 0 avisos IL/AOT.
- e) Arnes VistasVideoForm1 (VIDEOFORM1=.../armado-vistas/videoform1): 734 lineas, IGUALES con las letras falsas y con DejaVu (140
  llamadas directas); el caso nuevo «ayuda-pinta» (12: OnPaint de XP contra Pinta, en su sitio y trasladada y recortada como la capa)
  sale igual. VistasVistasA: IGUALES (falsas y DejaVu, 1265 lineas). VistasVistasB: 0 distintas de 2865 contra la referencia y contra la
  base, con las dos letras.
- PantallaVistas (pruebas_vistas.sh): **59 de 59** (46 de antes + 13 nuevas: con la capa, gemelo y vista falsa en su hueco: los oyentes
  despues de la vista, el foco con su traza nombrada por la vista, Focus de una escondida = False, Controls.Remove sin desechar y el
  desecho con el dueno; sin la capa: los oyentes, Focus y Focus de una escondida).
- f) pruebas41: **41 de 41**; pruebas_hoja: **62 de 62**; AOT=1 pruebas_pantalla: **32 de 32** en JIT y en NativeAOT (sus 2 avisos CS0649
  de siempre).
- Escenas alternadas con la referencia congelada (JIT, RAV/AV, la maquina a carga 8-11 con los otros grupos; ruido de
  /root/rocola-trabajo/port/ventanas/ruido y ventanas2/ruido): arranque, moneda, video, buscador, asistente y f3raton IGUALES. captura:
  solo cuentas de relojes y encargos (reloj Form1.cs:724 269 frente a 300, holgura 11; repetida, RAV2/AV2: 272/315) con la maquina a carga
  11: cuentas de un reloj y un encargo fuera de la holgura, nada del orden ni de lo visto. tab: lo unico distinto son los rotulos de lo que
  el repaso de fondo lleva medido al abrir Ctrl+F3 («Calidad: 7 aceptables / faltan 6» frente a «9 / faltan 4» en la primera pasada; en la
  repetida al reves, «33% / van 12» frente a «0% / aun no hay bastantes»: cambia de sentido de una corrida a otra) y sus pixeles en la
  capa de la hoja: depende de la carga, no de este cambio (no toca el repaso ni Ctrl+F3). NativeAOT (con la fecha de dotnet en rocola-xp)
  contra la referencia JIT (RAVA/AVA): arranque y buscador IGUALES.
- d) Recorridos (con el candado, carga 7-11): 9 de 9 corren; capas quietas 215 de 217: las dos que faltan son c18 y c19.5 de «hojas» (la
  hoja de Sonido de la capa, 966x764, con lo medido por el repaso); la referencia congelada corrida a la misma hora con la misma carga
  (cmp-refhoy) da las MISMAS dos distintas (y c16.5): dependen de la carga. Texturas quietas: 473 de 487 en la primera pasada; repetidos
  hojas, pared, pared2, buscador y ayuda (pared 83 de 83), tomando la mejor de cada uno **479 de 487**; las 8 que faltan son de las que
  dependen de la hora y de cuantos pasos llega a dar la rocola (ayuda y buscador: dos 1024x128 cada uno; pared2: dos 1024x256, una
  1024x64 y una 1024x128), como en la ultima junta (481) y en pantalla-nativa (481). Lo que se sube a la tarjeta no cambio (DrawCtrlOverlay
  llama a lo mismo): no se corrieron las 29 cintas.

## Riesgos

- La vista sin herencia en el hueco: su hueco nace con lo de fabrica de Control (Selectable, TabStop, StandardClick/DoubleClick), que es
  lo mismo que tenia HelpBar (su SetStyle no tocaba esos). Con otra vista, cada grupo de las oleadas 5 y 6 debe mirar su SetStyle.
- Con la lista nativa (sin la capa) las vistas no tienen Enabled ni el foco del teclado (las teclas van a la ventana, que tiene KeyPreview);
  el foco de una vista escondida no pasa al siguiente (SelectNextIfFocused): de 6c (el buscador) y de la oleada 8.
- OyeRaton acepta solo Abajo y Clic (lanza con otros): si un grupo necesita otro evento, se anade.
