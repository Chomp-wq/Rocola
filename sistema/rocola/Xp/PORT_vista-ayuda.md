# Port nativo: la casilla de AYUDA sin herencia (tanda 7, oleada 4, grupo vista-ayuda: W3d)

Seccion «4b. vista-ayuda» de PLAN_TANDA4_A_N7.md, sobre 809389d. Es lo mismo que la oleada 3a hizo con HelpBar (PORT_armado-vistas.md): AyudaView
(la casilla de AYUDA al lado de la de BUSCAR) deja de heredar de Control y es una vista de la pantalla nativa (IVista) con su CajaVista.

## Lo hecho

Codigo/AyudaCasillaGpu.cs (329 lineas, LF, las mismas que en 809389d; 10 lineas con cambio, todas con «EN LINUX»):

- **Cabecera** (:17): `private class AyudaView : IVista, IDisposable` (antes `private partial class AyudaView : Control, IVista`). Vuelve a no ser
  partial, como en XP: la unica parte que quedaba en otro archivo era la firma de XP de RenderContent en el puente.
- **CajaVista** (:22, la linea vacia de XP entre los campos y el constructor): Bounds, Left, Top, Width, Height, SetBounds, Visible e Invalidate
  de su CajaVista (Nativo/IVista.cs). Sin Bottom: nadie lo lee de la casilla (HelpBar si, Form1.Video.cs).
- **SetStyle** (:25) y lo que XP le ponia al control (revisado contra Rocola/AyudaCasillaGpu.cs:28-30):
  - `SetStyle(AllPaintingInWmPaint | UserPaint | OptimizedDoubleBuffer | SupportsTransparentBackColor, true)`: sin ventana propia no hay
    WM_ERASEBKGND, doble bufer ni comprobacion del fondo transparente; la pinta su Pinta entera. No toca Selectable ni StandardClick ni
    DoubleClick ni ResizeRedraw ni Opaque: los de fabrica de Control, que el hueco de la capa conserva (el clic llega igual: Form1.cs:1125).
  - `BackColor = Color.Transparent`: el OnPaintBackground de fabrica con un color transparente no pinta nada (Ventanas.Control.cs:509-515);
    Pinta no pinta fondo.
  - `TabStop = false`: la vista no toma el foco del tabulador; en la pantalla nativa las teclas van a la ventana (Form1.ProcessCmdKey) y el
    tabulador no recorre vistas. (Ver riesgos: con la capa su hueco conserva el TabStop de fabrica, como el de HelpBar.)
- **Dispose** (:29, :32): `IDisposable.Dispose() => Dispose(true)` y `Dispose(bool)` privado, igual que HelpBar; el `base.Dispose(disposing)` (sacarla de
  la ventana) lo hace su hueco despues, en el mismo orden que cuando heredaba.
- **OnPaint -> Pinta(SKCanvas)** (:65): `Pinta(g) { RenderContent(g, false, false, false); }`. XP: `OnPaint(e) { RenderContent(e.Graphics, false); }`.
  El Graphics de un WM_PAINT de la capa es el de fabrica (Graphics.FromImage: sin copia ni medio pixel, con el Save/Restore por control de
  Escritorio.PintaSolo), asi que el estado que la firma de XP tomaba de el (la copia y el medio pixel) es falso y falso. Lo que la firma de XP le
  dejaba puesto al Graphics (suavizado y modo de letra AntiAlias, si la casilla mide 30x12 o mas) se iba con el Graphics: el lienzo de Pinta no lo
  conserva. El raton (MouseClick, que Form1 oye con PantallaRocola.OyeRaton) no cambia: lo da el hueco.
- Notas «EN LINUX» de la linea 3 y de RenderContent corregidas (ya no hay puente para OnPaint).

Plataforma/Puente.asistente.cs: se borra `AyudaView.RenderContent(Graphics, bool)` (la firma de XP con el Graphics de OnPaint y de
DrawCtrlOverlay; nadie mas la llamaba: DrawCtrlOverlay, Form1.Video.cs:1794, ya llama a la traducida con el lienzo), el bloque `partial class
AyudaView` entero y los `using` que ya no hacen falta. Queda el comentario del grupo y la nota de la parte que se quito antes.

Pruebas/VistasAsistente/Asistente.cs: el arnes ya no hace cast a Control (`PonSitio`, SetBounds por reflexion: lo da Control en la referencia y la
CajaVista en la traduccion); donde llamaba a la firma de XP (que en la traduccion ya no existe) llama a la traducida con el estado del Graphics
y reproduce lo que la de XP le dejaba al Graphics (suavizado y modo de letra), como hace VistasVideoForm1 con HelpBar; el caso OnPaint (revision)
llama a Pinta cuando el Graphics es de fabrica (copia y medio falsos) y a la traducida con su estado en los otros siete estados de prueba. Caso
NUEVO `casilla-pinta-*` (56 lineas: 7 medidas x 4 variantes x 2 sitios): OnPaint de XP contra Pinta, con el Graphics de fabrica, en su sitio y
trasladado y recortado como lo da la capa (el «ayuda-pinta» de VistasVideoForm1).

## Costuras

Quitadas: `AyudaView.RenderContent(Graphics, bool)` (Puente.asistente.cs) y la herencia de AyudaView de Control.
Anadidas: ninguna.
Quedan (no son de este grupo): `ILaCapa.Enfoca`, `OyeComoControl` y el `HuecoDeVista` de Anfitrion.cs (se van con la oleada 8 / N7); el archivo
Puente.asistente.cs, ahora solo con comentarios, lo borra la limpieza o N7.

## Comprobaciones (12-11-2026, maquina compartida a carga 7-9, llvmpipe)

- a) `dotnet build -c Release`: 0 errores, 15 avisos (los de la base).
- b) diff-xp.py AyudaCasillaGpu.cs -v: 55 lineas quitadas, 52 puestas, 0 sin marca. Lineas (329) y finales de linea (LF, 0 CR) iguales a 809389d.
- c) NativeAOT: publica; los avisos son los de la base (SYSLIB0054, CA2022); 0 avisos IL/AOT.
- e) Arnes VistasAsistente (ASISTENTE=/root/rocola-trabajo/port/vista-ayuda/asistente): **IGUALES** con las letras falsas y con DejaVu, 2043
  lineas (777 llamadas directas a la traducida contra 0 en la referencia).
- PantallaVistas (pruebas_vistas.sh): **59 de 59**. pruebas41: **41 de 41**. AOT=1 pruebas_pantalla: **32 de 32** en JIT y en NativeAOT (sus 2 avisos
  CS0649 de siempre).
- Escenas alternadas con la referencia congelada (JIT, RVA2/VA2, datos0 de siempre, ruido de ventanas2/ruido): **arranque IGUAL y asistente IGUAL**. (Una
  primera pasada sin la carpeta de datos dio arranque DIFERENTE por la hoja de datos vacia -cuentas de MedidasBitacora.cs:129 y capa2-dibujo2d
  de 1388 pixeles-; repetida con los datos de siempre, igual.)
- No se corrieron los recorridos (d) ni las cintas: la casilla se sube a la tarjeta por DrawCtrlOverlay, que llama a la misma RenderContent con
  los mismos argumentos (el arnes lo comprueba pixel a pixel con los ocho estados); ni el banco (no se toco Nativo ni Puente.cs).

## Riesgos

- TabStop: XP ponia `TabStop = false` a la casilla; en la capa su hueco (HuecoDeVista, Anfitrion.cs) nace con el TabStop de fabrica de Control,
  igual que el de HelpBar. Form1 no usa el tabulador para moverse (las teclas pasan por ProcessCmdKey y la pantalla nativa no recorre vistas),
  asi que no cambia nada visible; desaparece con la capa (N7).
- La vista no tiene Enabled ni foco de teclado en la lista nativa (como las demas sin herencia, ver PORT_armado-vistas.md).

## Revision (revisor independiente, 12-11-2026)

Se leyo el diff entero (809389d..04ef3a3) contra Rocola/AyudaCasillaGpu.cs (XP), la capa (Anfitrion.cs, Ventanas.Control.cs) y la pantalla
nativa. La pintura, el Dispose, el raton y las notas estan bien: Pinta recibe el estado de un WM_PAINT de la capa (sin copia ni medio
pixel, como leia el puente borrado), lo que la firma de XP dejaba en el Graphics se iba con el, el raton llega por los oyentes
(OyeComoControl ya no la toma: no es Control; el hueco y la pantalla avisan despues de Raton), y DrawCtrlOverlay ya llamaba a la traducida.

**Fallo encontrado: el `TabStop = false` de XP no seguia valiendo.** El hueco de la capa nace con el TabStop de fabrica de Control (true) y la
casilla de XP lo tenia en false. No es solo un riesgo «sin efecto»: con el Tab en la ventana principal (base.ProcessCmdKey -> ProcessDialogKey
-> SelectNextControl con tabStopOnly) el foco se paraba en la casilla. Medido con una escena propia (16 Tab desde los 7 s, traza del foco):
- referencia congelada: HelpBar LetterBar AlbumView ProgressView GpuPanel HelpBar ... (ciclo de 5);
- 04ef3a3: HelpBar LetterBar AlbumView ProgressView GpuPanel **AyudaView** HelpBar ... (ciclo de 6);
- con el arreglo: el mismo ciclo de 5 que la referencia (los 17 cambios de foco iguales).

Arreglo (commit aparte, «Port nativo (vista-ayuda, revision): ...»):
- Nativo/IVista.cs: miembro de fabrica `bool ParaElTabulador { get { return true; } }` (Control.TabStop, true de fabrica: lo unico de TabStop
  que XP usa en sus vistas). No imita a Control: es un dato de la vista, como Pinta y Raton de fabrica; la lista nativa aun no recorre vistas
  con el tabulador (oleada 6c), que lo podra leer; las vistas de las oleadas 5 y 6 que XP pone `TabStop = false` (Form1.Vistas.cs,
  Form1.Buscador.cs) lo tienen ya.
- Plataforma/Anfitrion.cs: el HuecoDeVista toma `TabStop = v.ParaElTabulador` al nacer (antes de Controls.Add, como el constructor de XP).
  Una linea; se va con la capa (N7).
- Codigo/AyudaCasillaGpu.cs: `public bool ParaElTabulador { get { return false; } }` en la linea de su CajaVista (:22) y la nota del
  `TabStop = false` (:27) dice donde quedo. 329 lineas y 0 CR como en 809389d; diff-xp.py: 55 quitadas, 52 puestas, 0 sin marca.
- Toca dos archivos fuera del grupo (una linea en cada uno, mas el comentario de IVista): Nativo/IVista.cs y Plataforma/Anfitrion.cs.

Tambien mirado: el clic en la casilla con la tarjeta (640,30 a los 7 s) va al GpuPanel en la referencia y en la traduccion (mismas lineas de
traza, asistente_botones=0 en las dos): con tarjeta la casilla no recibe el raton por la capa en ninguna de las dos.

Repetido por el revisor (maquina a carga 5-6):
- a) build Release: 0 errores, 15 avisos (los de la base), antes y despues del arreglo.
- b) diff-xp.py AyudaCasillaGpu.cs -v: 55/52, 0 sin marca; lineas y CR iguales a 809389d.
- c) NativeAOT: 0 avisos IL/AOT (antes y despues del arreglo).
- e) Arnes VistasAsistente: IGUALES con las letras falsas y con DejaVu (2043 lineas, 777 llamadas directas; los 56 casilla-pinta sin
  excepciones), antes y despues del arreglo. PantallaVistas 59 de 59 y pruebas41 41 de 41, antes y despues; AOT=1 pruebas_pantalla 32 de 32
  en JIT y NativeAOT (no usa IVista.cs).
- Escenas alternadas con la referencia (RVArev/VArev, con el arreglo): asistente IGUAL; arranque DIFERENTE dos veces solo en las cuentas del
  reloj Form1.cs:724 y del encargo MedidasBitacora.cs:129 (la secuencia de sucesos, el foco y las capturas iguales; en la segunda pasada 176
  frente a 180 ticks y la diferencia cambia de signo segun el segundo): de las que dependen de la carga, como dice la regla.
