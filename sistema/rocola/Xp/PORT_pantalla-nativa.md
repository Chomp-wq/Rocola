# Port nativo: la pantalla nativa (tanda 4, oleada 1, grupo pantalla-nativa: W2)

Seccion «1c. pantalla-nativa (W2)» de PLAN_TANDA4_A_N7.md, sobre 178722c. La pantalla nativa (Nativo/PantallaRocola) recibe la
entrada de evdev y compone cada cuadro con un compositor propio; lo que aun es de la capa de ventanas (Plataforma/Ventanas.*.cs) se lo
pide por una sola costura (PantallaRocola.ILaCapa). Codigo/ no cambia. Hoy todo lo de la rocola sigue siendo control de la capa: el
resultado tiene que ser IDENTICO (y lo es: ver las comprobaciones); lo nuevo (IVista sin herencia, la lista nativa, las teclas y el raton
sin la capa) se prueba con una vista falsa.

## Lo hecho

Archivos nuevos (Nativo/):

- **IVista.cs**: la interfaz de las vistas, con lo que el codigo de XP usa de ellas fuera de su clase (inventario W2 y W3-0): `Bounds`,
  `Left`, `Top`, `Width`, `Height`, `SetBounds`, `Visible` (la suya y la de su dueno: GetVisibleCore) e `Invalidate`; y lo que la pantalla
  les pide: `Pinta(SKCanvas)` (OnPaintBackground y OnPaint) y `Raton(AvisoRaton, Raton)` (los On* del raton en el orden de WmMouseDown,
  WmMouseUp, WmMouseMove, WmMouseEnter/Leave y WmMouseWheel). Pinta y Raton son de fabrica (metodos de la interfaz): una vista que aun
  hereda de Control (la oleada 3 les pone «, IVista» en la cabecera) cumple la interfaz con los miembros de Control de la misma firma, sin
  escribir nada, y su Pinta de fabrica la pinta la capa. `AvisoRaton` lleva los numeros de TrazaVentanas.EventoRaton. `CajaVista`: lo
  comun de una vista que ya no hereda (sitio y visible; avisa a la pantalla de cada cambio; SetBounds con el mismo sitio no avisa, como
  Control.SetBounds), sin ser clase base (como CajaPieza de las hojas). Lleva el `InternalsVisibleTo("PruebasVistas")` de la prueba nueva.
- **PantallaRocola.Vistas.cs**: la lista de la ventana (vistas y capas de textura, `[0]` arriba como Controls), con AgregaVista
  (Controls.Add: al final = el fondo), VistaAlFrente (BringToFront: SetChildIndex(this, 0)), VistaAlFondo (SendToBack), QuitaVista
  (Controls.Remove) e IndiceDe; la composicion en tramos (las vistas seguidas, por procesador, en una imagen del tamano de la pantalla,
  partidas por cada capa de textura; el primer tramo con el fondo de la ventana, `FondoDeLaVentana`), cada vista recortada por los de
  encima (WS_CLIPSIBLINGS, Control.cs:1691); el raton sin la capa (WindowFromPoint, la captura de WmMouseDown, el clic solo si se suelta
  encima, el doble clic de 500 ms y 4 puntos, Entra/Sale, la rueda al foco y si no se atiende a la ventana: Control.cs:12532-12764) con las
  hojas nativas primero (Hoja.TomaElRaton), como la capa; las teclas sin la capa (`LlegaTecla`: modificadores, GetKeyState, la hoja activa
  o la ventana con `TeclasDeLaVentana`: ProcessCmdKey, ProcessDialogKey, KeyDown, KeyPress salvo SuppressKeyPress, Alt+F4 por la cola,
  KeyUp); Present de la tarjeta (`PresentaTarjeta`, `OlvidaTarjeta`, `TarjetaDe`) y el pintado sin la capa (`PintadoSinLaCapa`:
  lo que hoy da Escritorio.PintaUno/HaySucio/ProximoPintado). Y `ILaCapa`: lo que la pantalla le pide a la capa mientras dure la
  transicion; con la capa puesta, cada entrada de la pantalla va a la llamada de la capa que hacia lo mismo.
- **CompositorPantalla.cs**: el compositor de OpenGL que estaba en Plataforma/Anfitrion.cs, movido sin cambiar lo que hace (mismo
  programa, misma mezcla, mismas texturas guardadas, misma flecha, mismas capturas y trazas), con capas propias (`CapaPantalla`: lo pintado
  por procesador con su version, o una textura de la tarjeta o del video) en lugar de las `Capa` de la capa. Las capturas por capa
  (ROCOLA_CAPTURA_CAPAS) guardan lo mismo con el mismo nombre («dibujo2d», «hoja», «tarjeta»; las de la lista nativa, «vista»):
  Bitmap.Save de Dibujo2D era `Imagenes.Guarda(Sk, png, 92)`, lo mismo que `Imagenes.GuardaPng`. `PuntoEn` (pruebas) lee un punto.
- **VideoEnPantalla.cs**: IPantallaDelVideo nativo. En una ventana de la capa da el hueco de siempre (VideoEnVentanas.cs, por la capa);
  si no, una capa de textura de la lista de la ventana (Pon, AlFrente, Textura y Quita).

Archivos cambiados:

- **Plataforma/Anfitrion.cs**: ya no compone. Abre la pantalla, pone la capa (`PantallaRocola.LaCapa = new VentanasDeLaCapa()`), el
  compositor y el video nativo, y en cada cuadro le da al compositor las capas de abajo arriba: las de las ventanas de la capa
  (Escritorio.Capas, sin las hojas), la lista nativa de la ventana y las hojas nativas (las que dio Escritorio.Capas, apartadas y puestas
  despues de la lista nativa; ver «Revision»). Guarda
  `VentanasDeLaCapa` (cada metodo es la llamada de la capa que se hacia antes) y `HuecoDeVista` (un control de la capa que ocupa el lugar
  de una vista sin herencia en la lista de una ventana de la capa: la capa lo pinta y le da el raton, y el se lo pasa a la vista).
- **Plataforma/TeclasLinux.cs**: entrega a la pantalla (`PantallaRocola.Entrada`, `LlegaTecla`, `RatonEn`, `RatonMueve`, `RatonBoton`
  con `BotonRaton`, `RatonRueda`; el reloj y la medida de la pantalla de PantallaRocola), no a Escritorio. Las 20 lineas del inventario
  (72-262), mas los comentarios.
- **Nativo/PantallaTarjeta.cs**: sin Control ni Escritorio: Presenta y Olvida van a la pantalla; RefrescoHz y AhoraMs, de la pantalla
  (`PantallaRocola.RefrescoHz`, que pone el anfitrion igual que Escritorio.RefrescoHz). Tocada solo tras componer, como antes (sin
  ventana, solo glFlush y sin Tocada: si no, el dispositivo de las pruebas volveria a poner su estado en cada cuadro).
- **Nativo/PantallaRocola.Rocola.cs**: el apagado (AlApagar) va a la capa por `LaCapa.FinDeSesion` (era Escritorio.FinDeSesion directo).

No se tocaron: Codigo/ (0 lineas), Plataforma/VideoEnVentanas.cs (sigue igual: el hueco del video lo pide VideoEnPantalla por la capa),
Nativo/Hoja*.cs (ver «Lo que queda»).

## Costuras

Quitadas:

- PantallaTarjeta.cs:21-37 ya no usa Control.FromHandle, Escritorio.SinEspera, Escritorio.PresentaNativa, Escritorio.RefrescoHz ni
  Escritorio.AhoraMs (lo de la capa que hace falta va por `ILaCapa.Presenta` y `OlvidaTarjeta`, en Anfitrion.cs).
- TeclasLinux.cs no nombra Escritorio, MouseButtons ni Escritorio.Pantalla/PosicionRaton.
- Anfitrion.cs ya no compone con Escritorio.Capas() por dentro ni lee Escritorio.CursorVisible, PosicionRaton, AhoraMs ni RefrescoHz: lo
  hace el compositor nativo con PantallaRocola.
- PantallaRocola.Rocola.cs ya no nombra Escritorio.

Anadidas (todas de la transicion; se quitan cuando no quede nada de la capa, N7, salvo donde se dice):

- `PantallaRocola.ILaCapa` y `LaCapa` (Nativo/PantallaRocola.Vistas.cs) con su implementacion `VentanasDeLaCapa` (Anfitrion.cs): se
  quitan en N7, cuando Anfitrion.cs pase a Nativo sin la capa. Cada metodo cae cuando su lado deja de ser de la capa: Agrega, AlFrente,
  AlFondo, Quita, Cambio, DuenoVisible y Pinta cuando ninguna vista sea control ni este en una ventana de la capa (oleada 8); Presenta y
  OlvidaTarjeta cuando el panel de la tarjeta no sea control (oleada 6: entonces `PantallaRocola.TarjetaDe` da su capa de textura);
  LugarDelVideo cuando VideoEnVentanas.cs se borre (oleada 7); Tecla, RatonEn, RatonBoton, RatonRueda y FinDeSesion con Form1 sin Form
  (oleada 8: entonces la rocola pone `PantallaRocola.Teclas`, `RuedaDeLaVentana` y `FondoDeLaVentana`); HayQueComponer con la capa.
- `HuecoDeVista` (Anfitrion.cs): mientras haya vistas sin herencia en ventanas de la capa (de la oleada 5 a la 8). Va en Anfitrion.cs y no
  en VideoEnVentanas.cs porque este se borra en la oleada 7 y el hueco hace falta hasta la 8.
- `compositor.ConMano` = Escritorio.CursorActual == Cursors.Hand (Anfitrion.cs): la mano de un control de la capa; cae con la capa.

Sin cambiar (ya eran costuras de la capa, siguen igual): los ganchos de Hoja.cs:137-145 (ActivaDeLaCapa, CedeLaCapa...). Los pone el
constructor de Ventanas.Escritorio.cs, que no es de este grupo; la pantalla no los necesita (las hojas ya son nativas), y el plan los
adapta en N7 (9a: «Hoja.cs:137-145 con Hoja.Foco.cs:50»). PantallaRocola.PintaUno/HaySucio/ProximoPintado los sigue poniendo Escritorio;
`PintadoSinLaCapa` es su reemplazo nativo, sin usar mientras haya capa.

## Por que el resultado es el mismo

Con la capa puesta (siempre, en el programa): la entrada llega a `PantallaRocola.LlegaTecla/Raton*` y va tal cual a Escritorio.Tecla,
RatonEn, RatonBoton (`(MouseButtons)(int)BotonRaton`: los mismos numeros) y RatonRueda; RatonMueve suma a la misma flecha
(Escritorio.PosicionRaton es PantallaRocola.Flecha) y el absoluto usa la misma medida (Escritorio.Pantalla es PantallaRocola.Ancho/Alto).
El compositor recibe las mismas capas en el mismo orden (Escritorio.Capas pone las hojas al final; Anfitrion las aparta y las pone al final,
las mismas capas con la misma superficie y version), con la misma clave de textura para las de la capa (su objeto Capa) y las mismas texturas; la lista nativa esta
vacia y no anade nada. Present de la tarjeta hace lo mismo que antes, ahora desde Anfitrion.cs. El video va al mismo hueco.

## Comprobaciones (12-11-2026, maquina compartida con otros grupos, llvmpipe)

- a) `dotnet build -c Release`: 0 errores, 15 avisos, los mismos de la base (las mismas 6 lineas en Codigo/ y Plataforma/
  Ventanas.Control.cs, PruebasLinux.cs; comparado con la base 178722c: iguales).
- b) diff-xp.py: Codigo/ no cambia (git diff 178722c -- sistema/rocola/Xp/Codigo: vacio), nada que revisar.
- c) NativeAOT (dotnet publish ... -p:PublishAot=true): 0 avisos IL/AOT.
- Prueba NUEVA Pruebas/Ventanas/PantallaVistas (pruebas_vistas.sh PROGRAMA ETIQUETA, fuera de pantalla 320x240): **46 de 46**. Con la
  capa: la misma escena con la vista falsa en su hueco y con un control gemelo de la capa (12 pasos cada una: orden de composicion por
  puntos de la pantalla compuesta, raton al moverse, clic, captura y soltar fuera, doble clic, BringToFront y el raton despues, un control
  transparente encima -WS_CLIPSIBLINGS: se ve el fondo de la ventana, no la vista de debajo-, Visible = false con su raton, SetBounds y
  Quita): el gemelo da lo que dice WinForms y la vista falsa da lo mismo que el gemelo. Sin la capa: la lista nativa da el mismo orden,
  la misma composicion, el mismo raton con el mismo guion, el recorte, los tramos partidos por una capa de textura (y la textura recorta lo
  de debajo y se queda con el raton), la vista escondida, la rueda (al foco; sin Handled, a la ventana) y las teclas a la ventana
  (ProcessCmdKey primero y, si la atiende, nada mas que KeyUp; SuppressKeyPress; Mayus; la repeticion; Alt+F4 por la cola). Cada prueba
  cita su regla de WinForms. (La primera corrida dio 44: la flecha del raton, que la prueba no escondia, tapaba dos de los puntos leidos;
  ahora la esconde como la rocola.)
- f) pruebas41.sh: **41 de 41**; pruebas_hoja.sh: **62 de 62**; AOT=1 pruebas_pantalla.sh: **32 de 32** (JIT y NativeAOT; sus 2 avisos
  CS0649 son del arnes con los archivos sin cambiar).
- Escenas alternadas con la referencia congelada (JIT, RPN/PN, las 22 de la lista: arranque moneda video tab buscador cierreq altf4
  sigterm f3sigterm kill9 asistente captura salirq f2 f2barras f2sin aviso f3vistas f3grises f3teclas f3raton f3billar): IGUALES todas
  salvo sigterm y f3sigterm (lo aceptado: rocola_estado.txt se borra) y, en la primera pasada con la maquina a carga 4-5, cuatro por
  cuentas de relojes y encargos fuera de la holgura (video: reloj Form1.cs:724 296 frente a 348 y el volcado del estado un suceso antes;
  captura: 348/329; salirq: encargo MedidasBitacora.cs:129 325/292; f2: 655/788); repetidas una vez (RPN2/PN2, carga 3,5): las cuatro
  IGUALES.
- Escenas NativeAOT (PNA, con la fecha de dotnet en rocola-xp) contra la referencia JIT: cierreq, kill9, buscador, f3vistas y f3teclas
  IGUALES; arranque dio el reloj Form1.cs:724 del segundo 0 con 0 frente a 21-22 ticks contra RPN-arranque, que fue la primera corrida
  de la maquina (la referencia JIT tardo en arrancar); con otra corrida de la referencia (RPN3) las dos de NativeAOT (PNA y PNA2) IGUALES.
- d) Recorridos (con el candado): 9 de 9 salen con 0, sin excepciones; capas quietas **217 de 217**. Texturas quietas: en la primera
  pasada 448 de 487, con buscador 44 de 78 (la maquina con las escenas de otros grupos: la rocola llego a menos pasos, 74 texturas
  frente a 94); buscador, pared2 y video otra vez con la maquina mas tranquila: buscador 76 de 78, pared2 95 de 98, video 50 de 50; asi
  **481 de 487** (las que faltan dependen de la hora, como en la ultima junta y en los grupos de hoy -directas-vistas 480, junta11 477-:
  dos 1024x128 del buscador, tres de pared2 -1024x256 y dos 1024x128- y una 512x256 de la ayuda).
- e) Las 29 cintas (Pruebas/VistasFrontera/cintas.sh): **29 iguales** (6 616 cuadros), 0 distintas. No se toco nada del dibujo ni los
  Puente*.cs: el banco no hace falta.

## Lo que queda

- La oleada 3 (W3-0) pone «, IVista» en las cabeceras y cambia los Controls.Add de las vistas por `PantallaRocola.AgregaVista(this,
  vista)`, BringToFront por `VistaAlFrente`, etc.: ya funcionan con vistas que son controles (van a la capa tal cual).
- El foco del teclado en una vista (el buscador, oleada 6c): `FocoDeLaVentana` solo lleva hoy la rueda; las teclas van a la ventana
  (Form1 tiene KeyPreview y ve todo antes). Cuando haga falta que una vista reciba KeyDown/KeyPress tras la ventana, se anade a IVista.
- Sin la capa, la activacion de la ventana de la rocola frente a una hoja no modal al hacer clic (lo que hoy hace la capa con
  Hoja.ActivaLaDeLaCapa) y el cierre de la ventana (FormClosing con su razon) son de la oleada 8 (Form1 sin Form).
- «Lo que queda debajo de una textura que tapa la ventana entera no se pinta» (Escritorio.TapaTodo) no se porto a los tramos nativos: no
  cambia lo que se ve; es un ahorro para cuando el panel de la tarjeta este en la lista nativa (oleada 6).
- Los ganchos de Hoja.cs:137-145, en N7 (9a).

## Riesgos

- `VentanasDeLaCapa.Pinta` (la pintura de una vista que aun es control, para quien la pida por IVista.Pinta: la oleada 3) usa la matriz
  del lienzo como la del HDC (`PuenteDibujo.Graphics(g, g.TotalMatrix)`); hoy nadie la llama. Si la oleada 3 la usa, comparar con el
  arnes de su vista.
- El hueco de una vista sin herencia nace con lo de fabrica de Control (Selectable, TabStop): el foco y el tabulador de la vista que XP
  configuraba en su constructor (SetStyle) los tendra que decidir cada grupo que quite la herencia (oleadas 5 y 6).

## Revision (revisor independiente, 12-11-2026)

Leido el diff entero (178722c..cc0c267) contra la base y la fuente de WinForms (/tmp/claude-0/agente-winforms/ref/Control.cs: las citas
de WS_CLIPSIBLINGS 1691, CaptureInternal 12567-12571, WmMouseUp 12700-12722 y WmMouseWheel 12748-12764, y StandardClick/StandardDoubleClick
de fabrica 449-454 estan bien puestas). Codigo/ no cambia (git diff vacio) y no se toco nada fuera de los archivos del grupo.

Lo encontrado:

- **Las hojas nativas se pedian dos veces por cuadro (arreglado).** Anfitrion.Capas saltaba las capas de hoja de Escritorio.Capas (que ya
  habia llamado a Hoja.Superficie) y volvia a llamar a Hoja.Superficie desde Hoja.Abiertas. Con una pieza que lanza al pintarse (la «X»
  roja de PintaFondoYContenido, que vuelve a ensuciar la hoja dentro de su pintura) la hoja se pintaba otra vez en el mismo cuadro y se
  subia esa segunda pintura con otra version: el cuadro compuesto ya no era el de la referencia, contra la regla de que la composicion no
  cambia (el informe lo llevaba como riesgo aceptado). Ahora Anfitrion toma las capas de hoja tal cual las dio Escritorio.Capas (su
  superficie, su version y su objeto Capa como clave de textura, como antes de este grupo) y solo las aparta para ponerlas despues de la
  lista nativa (hoy vacia): mismas capas, mismo orden, mismas claves, una sola pintura por cuadro. Sin la capa (N7), la pantalla las
  pedira a Hoja.Abiertas una vez por cuadro. El riesgo correspondiente se quito de «Riesgos».

Lo comprobado y aceptado sin cambio:

- El compositor (CompositorPantalla.cs) es el de Anfitrion movido: mismo programa, mezcla, texturas, flecha (la mano por ConMano =
  Escritorio.CursorActual == Cursors.Hand, como antes), capturas y trazas; Application.Avisa era PantallaRocola.Falla.
- TeclasLinux: Escritorio.Entrada, AhoraMs, PosicionRaton y Pantalla eran ya PantallaRocola.Entrada, AhoraMs, Flecha y Ancho/Alto;
  Escritorio.RatonMueve era RatonEn(PosicionRaton + d), lo mismo que PantallaRocola.RatonMueve con la capa. MouseButtons y BotonRaton
  tienen los mismos numeros.
- PantallaTarjeta: con la capa, Presenta y Olvida hacen lo mismo (Control.FromHandle, PresentaNativa, Tocada solo si hubo control).
  Escritorio.SinEspera y PantallaRocola.SinEspera no los lee nadie (ya pasaba antes): se apunta para la oleada 6.
- VideoEnPantalla: Form1 (un Control) siempre es la ventana del video, asi que va al hueco de VideoEnVentanas como antes.
- Lo nativo sin la capa (lista, tramos, raton, teclas) hoy no corre en el programa; solo lo prueba PantallaVistas.
- El InternalsVisibleTo("PruebasVistas") se deja (lo dice Riesgos); las otras pruebas usan reflexion.

Comprobaciones del revisor (despues del arreglo):

- a) `dotnet build -c Release --no-incremental`: 0 errores, 15 avisos (SYSLIB0054, SYSLIB0050, CA2022, CA2014, CS1690: los de la base;
  ninguno en los archivos del grupo).
- b) Codigo/ sin cambios: diff-xp no tiene nada que revisar.
- c) NativeAOT publish: 0 avisos IL/AOT.
- PantallaVistas: 46 de 46. pruebas41: 41 de 41. pruebas_hoja: 62 de 62. AOT=1 pruebas_pantalla: 32 de 32 en JIT y en NativeAOT.
- Escenas alternadas con la referencia (JIT, RPNRV/PNRV), las que tocan las hojas o la composicion: arranque, f2, aviso, f3vistas,
  f3teclas y f3raton, las seis IGUALES. NativeAOT (PNRVA contra RPNRV): f2 y f3teclas IGUALES.
- Las 29 cintas: 29 iguales (6 616 cuadros), 0 distintas.
- No se repitieron los recorridos ni las demas escenas (el arreglo solo toca las capas de hoja; la verificacion completa es de la junta).
