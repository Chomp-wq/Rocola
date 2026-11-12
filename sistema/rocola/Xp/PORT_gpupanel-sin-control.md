# Port nativo: el panel de la tarjeta sin Control (oleadas 6a y 7b juntas, grupo gpupanel-sin-control: W4)

Seccion «6a. gpupanel-sin-control (W4)» de PLAN_TANDA4_A_N7.md, sobre 8460045. Lo mismo que las oleadas 4 a 6 hicieron con las vistas
(PORT_vista-ayuda.md, PORT_panel-del-disco.md, PORT_vistas-buscador.md...): el `GpuPanel` (el fondo de la rocola que pinta la tarjeta) deja de
heredar de `Control` y es una vista de la pantalla nativa (`IVista`, con su `CajaVista`). Lo que lo distingue de las demas: lo pinta la
tarjeta. En XP era una ventana con un dispositivo de Direct3D 9 creado sobre su `Handle`, que presentaba en ella; aqui el dispositivo recibe
la superficie de la vista (`PantallaRocola.SuperficieDe`) y la pantalla compone lo presentado en su sitio. Con la capa nada cambia: el hueco
de la vista es un control «Nativa» como lo era el GpuPanel.

## Lo hecho

### Codigo/ (cada archivo con su numero de lineas y de CRLF de 8460045; todas las lineas cambiadas con «EN LINUX»)

**D3D9Star.cs** (4539 lineas, LF; solo las lineas del GpuPanel que da el plan, nada de GpuTestWindow):

- :1976 `internal sealed partial class GpuPanel : IVista, IDisposable` (era `: Control`). Sus otras 29 partes (PantallaVivaGpu.cs,
  FluidezGpu.cs...) no cambian: lo que usan de Control (Width y Height, unos 330 usos, y Visible) lo da su CajaVista.
- :4526 (la linea de XP de `OnPaintBackground(...) { }`, que no pintaba nada): la CajaVista (`caja`) y lo que XP le usa de Control: `Bounds`,
  `Left`, `Top`, `Width`, `Height`, `SetBounds`, `Visible` (con la de su dueno: GetVisibleCore) e `Invalidate`; y `DeLaTarjeta = true`
  (IVista, nuevo: la pinta la tarjeta).
- El constructor (:2242-2244), contra Rocola/D3D9Star.cs:2379-2380 (revision de 4b: conservar EXACTAMENTE su SetStyle y su TabStop):
  - `SetStyle(UserPaint | AllPaintingInWmPaint | Opaque, true)` -> `caja = new CajaVista(this) { RepintaConMedida = false }`: la pinta entera
    su Pinta (el WM_PAINT: RenderFrame) sin borrar el fondo antes (Opaque: el hueco no pinta fondo; la pantalla nativa no la pinta por
    procesador); sin ResizeRedraw (XP no lo pone, Control no lo trae): al cambiar de medida no se repinta entera.
  - `SetStyle(OptimizedDoubleBuffer, false)`: en comentario (la vista no tiene bufer por procesador).
  - XP no toca Selectable ni TabStop: quedan los de fabrica de Control, `true` y `true`: `IVista.Seleccionable` y `ParaElTabulador` de
    fabrica. Asi el GpuPanel sigue en el ciclo del tabulador de XP en su sitio (ver «El tabulador»).
  - :2244 era `Nativa = true` (de la capa, sin XP): ahora un comentario; lo que decia lo dice `DeLaTarjeta`.
- El bucle de pintar (Application.Idle):
  - :2358 y :2370 `Application.Idle +=/-= OnIdle` -> `PantallaRocola.Libre +=/-= OnIdle` (con la cola vacia, una vez; lo que lance sale del
    bucle como de FDoIdle: la regla de XP del 8-11-2026).
  - :2222 `AppIdle()` -> `!PantallaRocola.HayPendientes` (lo que PeekMessage con PM_NOREMOVE miraba: encargos, entrada, lo sucio y relojes
    vencidos). :2219-2221 (el `NativeMessage` de Win32 y el PeekMessage de la capa) pasan a comentario: la pantalla nativa no tiene mensajes
    que leer.
  - :2271 `MsgWaitForMultipleObjectsEx(0, IntPtr.Zero, ms, QS_ALLINPUT, MWMO_INPUTAVAILABLE)` -> `PantallaRocola.EsperaPendientes(ms) ? 0 :
    0x102` (WAIT_TIMEOUT); :2263-2264 (su DllImport, que en la capa era una funcion con el nombre de Win32 sobre Escritorio.EsperaMensajes)
    pasan a comentario.
  - :2226-2227 `Form ventana = FindForm(); if (ventana != null && ventana.WindowState == Minimized)` -> `bool minimizada =
    PantallaRocola.VentanaMinimizada(this); if (minimizada)` (con la capa, la Form del hueco; en la pantalla nativa no se minimiza).
- :2354 `new GpuStarfield(this.Handle, ...)` -> `new GpuStarfield(PantallaRocola.SuperficieDe(this), ...)`: el dispositivo recibe la
  superficie de la vista (un identificador, como el Handle; el tipo del dispositivo no cambia y las cintas tampoco).
- :4519 `protected override void OnPaint(PaintEventArgs e)` -> `public void Pinta(SKCanvas g)` (el mismo cuerpo: RenderFrame con su try).
- :4528 y :4535 el Dispose: `void IDisposable.Dispose() { Dispose(true); } private void Dispose(bool)` y `base.Dispose(disposing)` ->
  `PantallaRocola.SueltaSuperficie(this)` (como destruir la ventana: Present ya no la encuentra). Lo desecha su hueco al desecharse con su
  ventana, antes de lo suyo (Control.Dispose desecha a los hijos), como a las demas vistas.

**Form1.Video.cs** (1975 lineas, LF):
- :851-855, :856 y :864, :865, :866 y :883: `gpuPanel.MouseMove/MouseDown/MouseWheel/MouseClick += delegate...` -> `PantallaRocola.OyeRaton(
  gpuPanel, AvisoRaton.Mueve/Abajo/Rueda/Clic, delegate (Raton e) ...)` (los cierres `};` pasan a `});`); `mouse.Location` y `e.Location` ->
  `new Point(X, Y)`; `MouseButtons.Left` -> `BotonRaton.Left` (:870, :872, :875).
- :981 `Controls.Add(gpuPanel)` -> `PantallaRocola.AgregaVista(this, gpuPanel)`; :990 y :1167 `gpuPanel.BringToFront()` ->
  `PantallaRocola.VistaAlFrente(gpuPanel)`.

**Form1.Biblioteca.cs** (3615 lineas, 3463 CRLF): :3203 y :3311 `gpuPanel.BringToFront()` -> `PantallaRocola.VistaAlFrente(gpuPanel)`.

### Nativo/

- **IVista.cs**: miembro de fabrica `DeLaTarjeta` (false): la vista la pinta la tarjeta (en XP, una ventana con un dispositivo de Direct3D 9).
- **PantallaRocola.Vistas.cs**:
  - La superficie de una vista de la tarjeta: `SuperficieDe(v)` (su identificador, desde 0x60000001: no choca con los de la capa, desde
    0x1000, ni con los de las hojas, desde 0x40000000), `SueltaSuperficie(v)`, `VentanaMinimizada(v)`.
  - `PresentaTarjeta` y `OlvidaTarjeta`: con la superficie de una vista que esta en un hueco de la capa, la capa (`ILaCapa.PresentaEnVista`
    y `OlvidaEnVista`: lo que antes hacia `Control.FromHandle` con el Handle del panel); en la lista nativa, la textura queda en su
    superficie y se compone (como hacia el `TarjetaDe` que nadie ponia: se quito); una hoja (F12) sigue por `ILaCapa.Presenta`; sin nadie,
    glFlush.
  - La composicion de la lista nativa: una vista de la tarjeta que se ve parte los tramos como una capa de textura (la textura de su
    superficie, en su sitio y su lugar del orden) y recorta lo de debajo; no se pinta por procesador. Dentro de otra vista no se compone
    (XP no tiene ninguna).
  - Su WM_PAINT sin la capa: al invalidarla o al ensenarse (tambien al entrar en la ventana), `PintadoSinLaCapa` le da su Pinta antes de
    componer, como Escritorio.PintaUno a un control «Nativa».
  - `OyeRaton` acepta tambien Mueve (MouseMove) y Rueda (MouseWheel), que Form1 oye del panel.
  - `ControlCollection.Add` como WinForms (Control.ControlCollection.cs): el Add de una que ya esta la manda al fondo (:67-71, SendToBack; la
    capa ya lo hacia) y el de una que tenia otro dueno la saca de alli antes (:73-77).
  - `ILaCapa` gana `PresentaEnVista`, `OlvidaEnVista` y `VentanaMinimizada`.
- **PantallaRocola.Foco.cs**: el TabIndex por coleccion (ver «El tabulador»).
- **PantallaTarjeta.cs**: solo los comentarios (la «ventana» del dispositivo es la superficie de la vista).

### Plataforma/Anfitrion.cs (solo lo del GpuPanel y la lista nativa)

- `VentanasDeLaCapa.Presenta` ya no busca un control por su Handle (`Control.FromHandle`): solo la hoja de F12; `PresentaEnVista`,
  `OlvidaEnVista` (el hueco «Nativa» guarda el fondo y el escritorio compone con el, lo que se hacia con el control) y `VentanaMinimizada`
  (`FindForm` del hueco).
- El hueco de una vista de la tarjeta es «Nativa» (`Nativa = v.DeLaTarjeta`): el escritorio deja su hueco, compone su textura y su WM_PAINT
  llama a su OnPaint (Escritorio.PintaYa), que llama a Pinta de la vista.
- El hueco avisa a los que oyen MouseMove y MouseWheel (despues de Raton, donde WinForms lanza el evento); `OyeComoControl` tambien con
  MouseMove y MouseWheel.
- La capa de la tarjeta se nombra por la vista del hueco (`GpuPanel`, como cuando era el control; ROCOLA_TRAZA_CAPAS).

### Pruebas/

- **Ventanas/PantallaVistas/Program.cs**: escenas nuevas (ver comprobaciones). La vista falsa gana `Tarjeta` (DeLaTarjeta, y su Pinta apunta
  «G.Pinta») y el gemelo un constructor «Nativa» (lo que era el GpuPanel en la capa) con su OnPaint y su OnMouseWheel apuntados.
- **VistasFrontera/Frontera.cs**: el contador de cuadros (EnsureFpsTex) se probaba en un GpuPanel sin construir poniendole el campo
  `bounds` de Control; en la traduccion el sitio esta en su CajaVista (`caja`, que un objeto sin construir no tiene): se le pone una nueva
  por reflexion y se escribe su `bounds`. Sigue compilando contra la referencia congelada (alli, el `bounds` de Control).

## El tabulador (avisos de las revisiones de 6c)

1. **El GpuPanel en el ciclo de XP.** En XP el GpuPanel esta en el ciclo del tabulador de la ventana («HelpBar LetterBar AlbumView
   ProgressView GpuPanel», 16 Tab): su constructor no toca Selectable ni TabStop. Ahora es una IVista con `Seleccionable` y
   `ParaElTabulador` de fabrica (true), asi que la lista nativa (que solo recorre IVista) lo recorre en su sitio; con la capa, su hueco
   toma `TabStop = Seleccionable && ParaElTabulador = true` y entra en `Controls.Add` en el mismo momento que el control, con el mismo
   TabIndex. Comprobado en la capa (escena del tabulador contra la referencia) y en la lista nativa (PantallaVistas: «ciclo L,G,H,L,G»).
2. **TabIndex.** La lista nativa usaba un contador comun. WinForms (Control.ControlCollection.cs:82-95) le pone al que no lo tiene el mayor
   de los de SU coleccion mas uno (Control.TabIndex da 0 al que no lo tiene: Control.cs:3614-3616) y no lo cambia despues. Da el mismo orden
   mientras ninguna vista cambie de dueno, pero no cuando una que ya tenia TabIndex entra en otra coleccion: alli empata con las de esa
   coleccion y decide su lugar en la lista (GetNextControl). Ahora es por coleccion (la lista de la ventana o las hijas de una vista) y
   cuenta tambien lo que no se elige (capas de textura y rotulos: en XP, controles con su TabIndex). Caso de prueba («pasa-de-dueno»: A en
   P, X y P en la ventana, A pasa a la ventana): WinForms y la capa dan desde X «A, P, X»; con el contador de antes seria «P, A, X».

## Costuras

Quitadas:
- La herencia de `Control` del GpuPanel (con `SetStyle`, `Nativa`, `FindForm`, `Handle`, `OnPaint`, `OnPaintBackground` y el
  `Dispose` de Control).
- `Control.FromHandle` en `VentanasDeLaCapa.Presenta` y `OlvidaTarjeta` (Anfitrion.cs): ya ningun control de la capa tiene dispositivo.
- `Escritorio.HayMensajes` y `Escritorio.EsperaMensajes` desde D3D9Star.cs (el PeekMessage y el MsgWaitForMultipleObjectsEx de la capa):
  la pantalla nativa directa. (Escritorio.HayMensajes y EsperaMensajes quedan en Ventanas.Escritorio.cs sin llamadores: los borra N7.)
- `Application.Idle` del GpuPanel: PantallaRocola.Libre directo.
- `PantallaRocola.TarjetaDe` (el sitio para la capa de textura de la tarjeta, que nadie ponia).

Anadidas (de la transicion; se van con la capa, N7): `ILaCapa.PresentaEnVista`, `OlvidaEnVista` y `VentanaMinimizada` (y su
implementacion en Anfitrion.cs), el hueco «Nativa» de una vista de la tarjeta y sus avisos de MouseMove y MouseWheel, y el `OyeComoControl`
de MouseMove y MouseWheel. Lo demas (la superficie, su composicion y su WM_PAINT sin la capa, el TabIndex por coleccion, OyeRaton con Mueve
y Rueda) es de la pantalla nativa y se queda.

## Comprobaciones (12-11-2026, maquina compartida a carga 5-9, llvmpipe)

- a) `dotnet build -c Release --no-incremental`: 0 errores y los 15 avisos de la base, la misma lista archivo(linea): aviso que 8460045
  (SYSLIB0054 x9, SYSLIB0050, CA2022 x2, CA2014, CS1690 x2; ninguno en lo tocado).
- b) diff-xp.py -v: D3D9Star.cs 427 quitadas / 291 puestas, 11 sin marca (antes 12: las 11 que quedan son comentarios de XP de varias lineas
  de la tarjeta, de otros grupos; la que se fue era la del MsgWaitForMultipleObjectsEx de la capa, ahora con su marca); Form1.Video.cs
  194/193, 4 sin marca (las mismas 4 de antes); Form1.Biblioteca.cs 98/95, 2 sin marca (las mismas 2). Todas las lineas cambiadas llevan
  «EN LINUX». Lineas y CRLF iguales a 8460045: D3D9Star.cs 4539/0, Form1.Video.cs 1975/0, Form1.Biblioteca.cs 3615/3463. `git diff -U0`
  de Codigo/: solo las lineas de arriba.
- c) NativeAOT (`dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true`): sale con 0, **0 avisos IL/AOT**.
- PantallaVistas (pruebas_vistas.sh): **166 de 166** (117 de antes + 49 nuevas):
  - con la capa, la vista falsa de la tarjeta en su hueco contra un control «Nativa» de la capa (lo que era el GpuPanel), iguales y como
    WinForms: su WM_PAINT al entrar (G.Pinta), su capa en su sitio (40,40,120x90), al invalidarla, nada al esconderla, al ensenarla, nada
    al moverla sin cambiar de medida, el raton con quien lo oye (MouseMove, MouseDown, MouseClick y MouseWheel despues de lo de la vista),
    la rueda al foco, FindForm().WindowState (False, True minimizando la ventana, False); solo la vista: Present en su superficie (la textura
    en su hueco), OlvidaTarjeta (0) y la superficie soltada (Present ya no la encuentra: False);
  - el tabulador con la de la tarjeta (H, L, N sin TabStop, G de la tarjeta): ciclo «L,G,H,L,G», Mayus+Tab «L,H», el TabIndex al cambiar de
    dueno «A,P,X» y el Add de una que ya esta (al fondo): gemelo, vista en su hueco y lista nativa, los tres iguales;
  - sin la capa (lista nativa): lo mismo con la composicion (3 tramos «vista,tarjeta,vista», la de la tarjeta en su sitio y sin textura
    antes de Present, recorta a la de debajo), Present (la textura en su capa y se compone), el pintado sin la capa, el raton, la rueda,
    minimizada False, olvida y soltada.
- e) Arnes de la frontera (VistasFrontera/compara.sh, compilado aqui con la traduccion y con la referencia congelada): sk y puente, con las
  letras falsas y con DejaVu: **106 lineas, 0 distintas; 153 texturas, 0 distintas** (el contador EnsureFpsTex en el GpuPanel sin
  construir, por su CajaVista). Arnes VistasAcabadoEstilo (pone un GpuPanel sin construir en Form1): 5901 lineas iguales a la referencia
  hasta que el arnes aborta en Vistas.cs:382 (un cast de SongPagerBar a Control: ya pasaba en 8460045, desde vistas-disco; lo arregla 8b).
- e) Las 29 cintas (cintas.sh): **29 iguales (6 616 cuadros), 0 distintas**.
- f) pruebas41: **41 de 41**; pruebas_hoja: **86 de 86**; `AOT=1 pruebas_pantalla`: **32 de 32** en JIT y en NativeAOT (sus 2 lineas de
  avisos de siempre).
- El tabulador de la ventana principal (16 Tab desde los 7 s, cada 0,4 s, la traza del foco), referencia y traduccion: las dos
  `HelpBar LetterBar AlbumView ProgressView GpuPanel` x3 + `HelpBar LetterBar` (17 cambios de foco iguales); al final la traduccion apunta
  el apagado (SIGTERM de timeout: «foco nadie» tras WindowsShutDown), lo aceptado.
- f) Escenas alternadas con la referencia congelada (JIT, gpupanelR/gpupanelN, ruido de ventanas2/ruido), las 23 (las 22 de la lista y
  f12): IGUALES moneda, buscador, cierreq, altf4, kill9, asistente, captura, f2sin, aviso, f3vistas, f3grises, f3teclas, f3raton, f3billar
  y f12; sigterm y f3sigterm DIFERENTES por lo aceptado (rocola_estado.txt se borra al apagar); en la primera pasada (carga 5-7) arranque
  (reloj Form1.cs:724 del segundo 1), video (el volcado del estado un suceso antes), salirq (reloj), f2 (encargo MedidasBitacora.cs:129),
  f2barras (el volcado del estado antes del aviso) y tab: repetidas (gpupanelR2/N2) arranque, video y f2 IGUALES; salirq solo el reloj
  (194 frente a 188, holgura 4); tab y f2barras otra vez distintas, y otra en orden inverso (N3 antes que R3). Las dos son de la carga:
  - tab: la traza IGUAL (508 sucesos) y lo unico distinto son los rotulos de lo que el repaso lleva medido en la hoja de Sonido (la
    referencia 12 aceptables, la traduccion 8-9) y sus pixeles; la base 8460045 contra esta traduccion, alternadas (B4/N4): la traduccion
    MAS adelantada (12 frente a 11), y la traza igual: el repaso va por su lado (lo vieron igual armado-vistas y vista-ayuda);
  - f2barras: con la carga 7-9 el volcado del estado de los 15,6 s cae antes o despues de que se atienda la tecla; la base contra la
    traduccion (B4/N4): la secuencia IGUAL y solo las cuentas de un reloj y un encargo.
- NativeAOT (con la fecha de dotnet en rocola-xp) alternada con la referencia JIT (gpupanelRA/NA): **arranque, cierreq, kill9, buscador,
  f3vistas, f3teclas y f12 IGUALES** (7 de 7).
- d) Recorridos (con el candado y la carpeta de siempre, carga 7-9): 9 de 9 salen con 0, sin excepciones. Capas quietas 211 de 217 (210 en
  la repeticion): las distintas son ctrlf3 c46 (capas 3, 4 y 6, a los 2 s de abrir la ventana de prueba F12) y hojas c18, c19.5 y c21 (la
  hoja de Sonido con lo medido por el repaso); la base 8460045 corrida a la misma hora da las MISMAS (c46 con las mismas huellas que la
  traduccion en las capas 3, 4 y 6; hojas c18 y c19.5). Texturas quietas: 442 de 487 en la primera pasada; repetidos ayuda (64/66), pared2
  (94/98) y buscador; el buscador da 43-45 de 78 en las dos, la base y la traduccion, alternadas tres veces (base 74 y 43, traduccion 45,
  44 y 44): es de la carga (vistas-buscador vio 44-45 con carga y 76 sin ella). Lo que falta en ayuda y pared2 son 1024x128, 1024x256 y
  512x256, las que dependen de la hora y de los pasos.
- No corrido: el banco (no se toco ninguna funcion de dibujo de Nativo/ ni Puente.cs).

## Lo que queda

- `Form1.Video.cs:879` sigue leyendo `searchOverlay.ClientRectangle` (el miembro con el nombre de Control que dejo 6c en Form1.Buscador.cs,
  que no es de este grupo): se puede cambiar por `new Rectangle(0, 0, Width, Height)` y quitar el miembro cuando se toquen los dos.
- `Escritorio.HayMensajes`, `Escritorio.EsperaMensajes` (Ventanas.Escritorio.cs) y la `Nativa` de Control quedan para N7.
- Sin la capa (oleada 8): Form1 tendra que invalidar el panel como `Invalidate(true)` invalidaba a sus hijos «Nativa»
  (Ventanas.Control.cs:501); la pantalla ya le da el WM_PAINT a una vista de la tarjeta invalidada.

## Riesgos

- Las cintas que grabe el programa (ROCOLA_CINTA) apuntan el identificador de la ventana del dispositivo: antes el Handle del control de la
  capa (desde 0x1000), ahora el de la superficie (desde 0x60000001). El reproductor no lo usa (crea el dispositivo con IntPtr.Zero) y las 29
  cintas salen iguales; una cinta nueva comparada byte a byte con una vieja diferiria en ese numero.
- Una vista de la tarjeta dentro de otra vista (un panel) no se compone en la lista nativa: XP no tiene ninguna.

## Revision (12-11-2026, revisor independiente)

Leido el diff entero (8460045..34d8342) contra Rocola/D3D9Star.cs, Form1.Video.cs y Form1.Biblioteca.cs de XP y la fuente de WinForms
(/tmp/claude-0/agente-winforms/ref). Comprobado: la herencia, SetStyle y Nativa se van y lo que XP pone queda (UserPaint, AllPaintingInWmPaint
y Opaque: su hueco no pinta fondo y su WM_PAINT es Escritorio.PintaYa como antes; sin ResizeRedraw; Selectable y TabStop de fabrica, en el
ciclo del tabulador); Application.Idle, PeekMessage y MsgWaitForMultipleObjectsEx van a PantallaRocola.Libre, HayPendientes y
EsperaPendientes, lo mismo que hacia la capa (Escritorio.HayMensajes y EsperaMensajes eran esas dos); los cuatro eventos del raton en el
mismo orden de alta y con los mismos datos; FindForm().WindowState y this.Handle; el TabIndex por coleccion y el Add otra vez y de otro dueno
como Control.ControlCollection.cs:67-95; ninguna identidad de XP compara el panel; ninguna clase nueva imita a Windows. Lineas y CRLF de los
tres archivos de Codigo/ iguales a 8460045.

Encontrado y arreglado (commit aparte):
- SueltaSuperficie (el base.Dispose de XP) solo soltaba la superficie: en la lista nativa la vista se quedaba en la ventana y, como corte de
  tramos sin textura, tapaba lo de debajo. Control.Dispose con disposing hace «_parent.Controls.Remove(this)» (Control.cs:5348-5350); ahora,
  fuera de la capa, SueltaSuperficie la quita de su dueno (QuitaVista) y la olvida de las listas del WM_PAINT. En la capa no cambia nada (lo
  hace el hueco al desecharse). La vista no tiene finalizador, asi que Dispose(false) no llega nunca y quitarla siempre es lo mismo. Nota de
  D3D9Star.cs:4535 corregida (misma linea).
- QuitaVista no olvidaba que una vista de la tarjeta «se veia»: al volver a ponerla (Controls.Add otra vez) no recibia su WM_PAINT, y en
  WinForms la ventana se vuelve a ensenar (SetParent y ShowWindow). Ahora QuitaVista la saca de tarjetasQueSeVen y tarjetasPorPintar.
- Dos pruebas nuevas en PantallaVistas (sin la capa): quitar y volver a poner da G.Pinta; soltada sale de la ventana (IndiceDe -1).

Comprobaciones repetidas tras el arreglo: build Release --no-incremental 0 errores y los mismos 15 avisos (misma lista archivo:linea que la
base); diff-xp.py -v igual que el informe (D3D9Star 427/291 con 11 sin marca de otros grupos, Form1.Video 194/193 con 4, Form1.Biblioteca
98/95 con 2); PantallaVistas 168 de 168; pruebas41 41 de 41; las 29 cintas iguales (6616 cuadros); NativeAOT 0 avisos IL/AOT. No repetidos
(el arreglo no toca su camino: la ruta de la capa sale antes en SueltaSuperficie y QuitaVista solo anade olvidar dos listas de la ruta
nativa): pruebas_hoja y pruebas_pantalla (no compilan PantallaRocola.Vistas.cs), recorridos y escenas.
