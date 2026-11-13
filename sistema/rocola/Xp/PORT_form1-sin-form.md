# Port nativo: Form1 sin Form (oleada 8a, grupo form1-sin-form: W7)

Seccion «8a. form1-sin-form (W7)» de PLAN_TANDA4_A_N7.md, sobre a375686. Era lo ultimo de WinForms que quedaba en el codigo de XP:
`Form1` heredaba de `Form` (de la capa de la transicion, Plataforma/Ventanas.Form.cs). Ahora no hereda de nada: es la duena de su
ventana, que es la pantalla nativa misma, y de las hojas que abre (Ctrl+F2, Ctrl+F3, los avisos, F12). La rocola ya no tiene ninguna
ventana de la capa; de la capa de ventanas solo queda lo que compone las hojas y lo que usan las 41 pruebas de WinForms, hasta N7.

Commits (rama `port-form1-sin-form`): `60ace0b` (Form1 sin «: Form», su ventana todavia una de la capa), `1aa762c` (la ventana nativa;
fuera la de la capa) y el de este documento.

## Lo hecho

### Codigo/ (16 archivos; los 109 con su numero de lineas y de CRLF de a375686; cada linea cambiada con «EN LINUX»)

- Las 14 partes de `Form1` (Form1.Ajustes, Biblioteca, Buscador, Datos, Fondo, Ia, Pared, Portadas, Sonido, Video, Vistas: linea 20;
  Form1.SinPausa.cs:25; Form1.cs:26; SalidaSonido.cs:154): `public partial class Form1 : Form` -> `public partial class Form1`.
- **Form1.cs**
  - :16 la nota del `using` (el fondo de la ventana recibe el lienzo).
  - :707 `this.DoubleBuffered = true` -> `AbreVentana()`: donde en XP ya habia corrido el constructor de Form, la rocola abre su
    ventana (sin crear ni ensenar). :713-714 `KeyPreview = true` en comentario (la ventana de la rocola ve cada tecla y cada caracter
    antes que nadie) y `this.KeyPress += Form_KeyPress` igual (el aviso es ahora de la rocola).
  - :730 y :740 `OnFormClosed`, :745 y :748 `OnShown`, :1028 `OnResize` y :1030 `OnPaintBackground` ya no sobrescriben nada: su
    ventana los llama en el mismo sitio y orden (los `base.On*` que solo lanzaban un aviso que nadie escucha, en comentario;
    `base.OnFormClosed` -> `AvisaFormClosed()`, el aviso que escuchan Form1.Video.cs y la pantalla). `CloseReason` -> `RazonCierre`
    (los mismos numeros y nombres).
  - :1030-1034 y :1062 `OnPaintBackground(SKCanvas g)`: el estado de un Graphics de WM_PAINT (filtro Bilineal, sin medio pixel, sin
    copia) va escrito y `formStars.DrawCached` se llama directo con el lienzo (ya no hay Graphics de la capa); `base.OnPaintBackground`
    -> `ventana.PintaFondoDeFabrica(g)` (el cliente con BackColor).
  - :956 `Application.ExecutablePath` -> `Environment.ProcessPath ?? ...` (lo que daba la capa). :1112-1113 `this.Icon = ...` y
    `StartPosition = CenterScreen` en comentario (sin barra de tareas; maximizada no cuenta). :1117-1118 `FormBorderStyle = None` y
    `WindowState = Maximized` -> `ventana.SinBorde()` y `ventana.Maximiza()`.
  - :1266 y :1485-1486 `this.Font.FontFamily.Name` -> `this.Font.Familia` (la Font del formulario es una `Letra`: la de fabrica de
    WinForms, Microsoft Sans Serif de 8,25 puntos, que XP nunca cambia).
- **Form1.Designer.cs** :14 y :21 `Dispose(bool)` ya no sobrescribe; `base.Dispose(disposing)` -> `ventana.DesechaLoSuyo(disposing)`
  (lo de la ventana despues de lo de la rocola, como en XP). :33 `AutoScaleMode` en comentario (nadie llama a InitializeComponent).
- **Form1.Video.cs** :879 `searchOverlay.ClientRectangle` -> `new Rectangle(0, 0, searchOverlay.Width, searchOverlay.Height)` (y
  Form1.Buscador.cs:553 pierde ese miembro); :1877, :1897 y :1904 `OnFormClosing(RazonCierre e)` (la regla del 8-11-2026 intacta:
  con WindowsShutDown o UserClosing se borra rocola_estado.txt; `base.OnFormClosing` en comentario).
- **Form1.Pared.cs** :998 y :1502, **Form1.Fondo.cs** :1267 y **Form1.Datos.cs** :3337: la Font del formulario y la de AlbumView son
  una `Letra` (`this.Font.FontFamily.Name` -> `this.Font.Familia`).
- **Form1.Portadas.cs** :1120 y :1161 `WindowState == Minimized` -> `ventana.Minimizada`.
- **MedidasBitacora.cs** :96 y :103: el vigia de la pantalla recibe a `Form1` (era `Control`; usa IsDisposed, IsHandleCreated y los
  encargos a su ventana).

### Nativo/

- **Form1.Ventana.cs** (nuevo, 69 lineas): la parte de `Form1` que declara, con los nombres con que el codigo de XP los usa, SOLO
  lo que la rocola le usaba a Form, y se lo pide a su ventana: ClientSize, Size, MinimumSize, Width, Height, BackColor, Text,
  IsHandleCreated, IsDisposed, Disposing, Enabled, ContainsFocus, Focus(), Invalidate() e Invalidate(bool), Close(), Dispose(), los
  avisos FormClosed y KeyPress y Font (una `Letra`). `Form1` es `IDuenoDeEncargos` (los BeginInvoke a la ventana viajan con su vida)
  e `IDisposable`. No es una clase con la forma de Form: son miembros de la rocola.
- **PantallaRocola.Rocola.cs**: `IVentanaDeLaRocola` (lo que la rocola le pide a su ventana) y `AvisosDeLaVentana` (lo que la ventana
  le avisa: los On* de XP); `AbreVentana` y `Corre(Form1)` (Application.Run: ensenar la ventana y el bucle hasta FormClosed).
- **PantallaRocola.Ventana.cs** (nuevo, 307 lineas): la ventana nativa. Sus vistas son la lista nativa de PantallaRocola.Vistas.cs,
  su fondo el primer tramo de la composicion y sus teclas `PantallaRocola.Teclas`. Lleva lo que Windows y WinForms hacian con esa
  ventana y XP supone, regla por regla de la fuente de WinForms (Form.cs, Control.cs, ContainerControl.cs) como las fijaron las 41
  pruebas en la capa:
  - la medida (300x300 con marco al nacer; sin borde y maximizada, la pantalla, con su OnResize al crearse);
  - ensenarse (Form.SetVisibleCore): crear (una vida para sus encargos), maximizar, Shown en la cola, verse, activa y foco en la
    primera vista del tabulador;
  - la activa y el foco frente a las hojas (WM_ACTIVATE, ContainerControl.ActiveControl): al volver de una hoja el foco regresa a la
    vista que lo tenia; debajo de una modal no toma el foco ni se activa;
  - cerrarse con su razon (Close y Alt+F4: UserClosing; el apagado: WM_QUERYENDSESSION y WM_ENDSESSION a las hojas y a ella, con
    FormOwnerClosing a sus hojas antes; kill -9 no avisa de nada);
  - desecharse (Form.Dispose y Control.Dispose): deja el foco, se destruye (lo encargado y no atendido se pierde) y desecha a sus
    vistas en el orden de la coleccion, y cada vista a las suyas.
  - Las trazas la nombran como antes: «Form1'Rocola'».
- **PantallaRocola.Vistas.cs / Foco.cs / Teclado.cs**: con la ventana de la rocola, las vistas no se ven hasta que se ensena ni
  despues de desecharse; el clic en la ventana la activa; las teclas van a la vista del foco pasando antes por la ventana (KeyPreview)
  con la misma traza que la capa; Alt+F4 se encarga a la ventana (si se destruye antes, se pierde); la rueda y el raton sobre la
  ventana se apuntan igual; lo que queda bajo una textura que tapa la ventana entera no se pinta (Escritorio.TapaTodo); Present de una
  hoja (F12) sin la capa. Sin ventana de la rocola (las pruebas de la lista nativa) todo queda como estaba.

### Plataforma/ (transicion)

- **Puente.fondo.cs**: borrado `DrawCached(Graphics, ...)` (lo que mandaba el plan: nadie lo llama ya).
- **Anfitrion.cs**: `PantallaRocola.SueltaLaCapa` (10 lineas): al abrir la rocola su ventana, `LaCapa = null` (las vistas van a la
  lista nativa y la entrada la reparte la pantalla). Se quita en N7 con la capa.
- **Ventanas.Escritorio.cs** (3 lineas: TeclaDeMando y TeclaArriba ya no nombran a Form1) y **VolcadoEstado.cs** (el foco y la activa
  los da la pantalla; Form1 ya no es una Form que saltar). Los dos se borran o adaptan en N7 como dice el plan.

## Pendientes de oleadas anteriores

| Pendiente | Que se hizo |
|---|---|
| La Font del formulario y AlbumView.Font a Letra | Hecho (Form1.Ventana.cs `Font`; Form1.Pared.cs:998 y :1502). |
| Form1.Video.cs:879 searchOverlay.ClientRectangle | Hecho; el miembro se fue de Form1.Buscador.cs:553. |
| Invalidate(true) con sus hijas | `VentanaNativa.Invalida(true)`: el fondo y las vistas por procesador se repintan (un tramo) y cada hija de la tarjeta o capa de textura recibe su WM_PAINT, uno por vuelta, como Escritorio.PintaUno. |
| La pantalla mira la forma de las hermanas de encima | Ya lo hacian PintaRecortada y PintaHijas (RecortaForma con Difference) desde la junta de los rotulos; comprobado que es lo de Escritorio.PintaHijo. |
| El panel no pinta bajo sus hijas (WS_CLIPCHILDREN) | NO se cambia: la pantalla pinta el panel y despues sus hijas encima, lo mismo que la capa de la referencia (PintaArbol). Da el mismo resultado que WS_CLIPCHILDREN mas PaintTransparentBackground mientras el Pinta del dueno no dependa del recorte (BgPanel no depende). Queda como riesgo. |
| Desechar la ventana desecha a sus hijas; HelpBar con su ventana | `DesechaVistas` y `DesechaVista` (Control.Dispose: cada vista lo suyo y despues sus hijas); HelpBar es IDisposable y esta en la lista. |
| El cierre con razon y rocola_estado.txt | Igual que antes (ver escenas). |
| Una excepcion en Application.Idle sale del programa | No se toco: `PantallaRocola.DaLibre` sigue sin try (pruebas_pantalla 32 de 32). |

## Costuras

- Quitadas: `Starfield.DrawCached(Graphics, ...)` de Puente.fondo.cs; `Form1 : Form` (14 partes); el paso de teclas de la capa a
  Form1 (Escritorio.TeclaDeMando y TeclaArriba); la ventana de la capa del primer paso (`VentanaDeLaRocola`) no llego a quedarse.
- Nuevas o que quedan (todas se van en N7):
  - `PantallaRocola.SueltaLaCapa` (Anfitrion.cs): mientras exista la capa.
  - Los ganchos de Hoja con nombre «DeLaCapa» (`Hoja.ActivaDeLaCapa`, `CedeLaCapa`, `QuitaFocoALaCapa`, `ActivaLaDeLaCapa`,
    `VivaEnLaCapa`, `SitioEnLaCapa`, `ActivaLaDeArribaDeLaCapa`): ahora los pone la ventana nativa (PantallaRocola.Ventana.cs,
    AbreNativa); Hoja*.cs no se toco. En N7 pueden ser llamadas directas.
  - Las ramas `ILaCapa c = LaCapa; if (c != null) ...` de PantallaRocola.Vistas.cs: con la rocola abierta ya no corren (solo en las 41
    pruebas).
  - VolcadoEstado.cs y TrazaVentanas.cs siguen nombrando tipos de la capa (plan, 9a).
  - Las capturas por capa nombran «dibujo2d» al tramo por procesador de la ventana (PantallaRocola.Vistas.cs, `tipo`), para que
    Pruebas/Recorridos/compara.py empareje con la referencia.

## Comprobaciones (1aa762c; la maquina con otros dos grupos a la vez)

- Compilar desde cero (JIT): 0 errores y los mismos 15 avisos de la base (lista identica). NativeAOT (`dotnet publish`): 0 avisos
  IL/AOT.
- diff-xp.py de los 16 archivos: solo lineas de traduccion; las «sin marca» son las de la base (Form1.Designer.cs baja de 1 a 0).
  Lineas y CRLF: 109 archivos, 0 distintos.
- pruebas41: 41 de 41. pruebas_hoja: 86 de 86. AOT=1 pruebas_pantalla: 32 de 32 en JIT y 32 de 32 en NativeAOT. PantallaVistas:
  194 de 194.
- El ciclo del tabulador (16 Tab en la ventana de la rocola, traza del foco y de las teclas frente a la referencia): 82 sucesos
  identicos; el foco da HelpBar, LetterBar, AlbumView, ProgressView, GpuPanel tres vueltas y HelpBar, LetterBar, como la referencia
  (la unica diferencia es el cierre final por SIGTERM, que la referencia no apunta).
- Escenas alternadas con la referencia (Rform1e/form1e, con el ruido de ventanas2): arranque, moneda, tab, buscador, cierreq, altf4,
  kill9, f2, f2sin, aviso, f3vistas, f3grises, f3teclas, f3raton, f3billar y f12 IGUALES (16); sigterm y f3sigterm, lo aceptado (la
  traduccion apunta el apagado con WindowsShutDown y borra el estado); video, asistente, captura, salirq y f2barras, solo cuentas del
  reloj Form1.cs:724 y del encargo MedidasBitacora.cs:129 (y en video el orden de un volcado de estado por la hora): repetidas
  (Rform1f/form1f), ver abajo. Con el binario anterior a quitar la ventana de la capa (rondas 4 y 5) las 23 habian dado IGUAL salvo
  sigterm, f3sigterm y tab (los rotulos del repaso, lo de siempre).
- rocola_estado.txt: falta tras cierreq, altf4, salirq, sigterm y f3sigterm; esta tras kill9 (JIT y NativeAOT). Las trazas:
  `closing/closed Form1'Rocola' UserClosing` con Ctrl+Alt+Q y Alt+F4; con SIGTERM y Ctrl+F3 abierto, la hoja primero
  (WindowsShutDown y FormOwnerClosing) y despues la rocola (WindowsShutDown).
- NativeAOT (Rform1a/form1a): arranque, cierreq, kill9, buscador, f3vistas, f3teclas y f12 IGUALES (7 de 7).
- Recorridos (los nueve, con el candado): 9 de 9 con 0; capas quietas 213 de 217 (las de la junta 15: tres de ctrlf3 c46 con F12 y la
  de hojas c18, el repaso); texturas quietas 479 de 487 (ayuda 62 de 66, buscador 77 de 78, pared2 95 de 98: franjas de 1024x128 y
  1024x256 que dependen de la hora).
- No corridos (no se toco nada suyo): el banco de dibujo, las 29 cintas y los arneses Pruebas/Vistas* (son de 8b en esta oleada;
  DrawCached con Graphics no lo llamaba ninguno en el programa).

Las cinco de cuentas, repetidas (Rform1f/form1f): video, asistente y f2barras IGUALES; salirq IGUAL a la tercera (Rform1g/form1g; en
la segunda, el reloj Form1.cs:724 263 frente a 248 en total, holgura 4); captura no llego a dar IGUAL en tres corridas, siempre con la
secuencia identica (51 sucesos, 22 teclas repetidas) y rocola_teclas.txt igual, y solo por cuantas veces entra el encargo del vigia de
la pantalla (MedidasBitacora.cs:129, de otro hilo, unas 30 por segundo) y el reloj de 42 ms: en la primera la referencia llevaba 20 en
el segundo 1 y la traduccion 30; en la tercera, 29 y 21 en los segundos 1 a 6 y lo mismo (38, 38, 36...) del 7 en adelante. Va en los
dos sentidos y sigue a la carga de la maquina (carga media de 4 a 9 con 4 nucleos durante toda la verificacion); con el binario
anterior (ronda 4) captura habia dado IGUAL. Queda para la verificacion del coordinador con la maquina libre.

## Lo que queda

- N7: borrar la capa (Ventanas.*.cs), `SueltaLaCapa`, las ramas de `LaCapa` y los ganchos «DeLaCapa» de Hoja; adaptar
  VolcadoEstado.cs y TrazaVentanas.cs; pruebas41 se va con la capa.

## Riesgos

- La ventana nativa reproduce de WinForms solo lo que la rocola usa: MinimumSize no hace nada, Enabled es siempre true, Minimizada
  siempre false (en Rocola OS nadie la minimiza ni la encoge). Si la fase 11 pone la rocola en una ventana que no sea la pantalla,
  hay que completarlo.
- WS_CLIPCHILDREN no se reproduce (ver la tabla): un Pinta de un dueno que dependiera del recorte se veria distinto.
- El orden exacto de activar y enfocar entre la ventana y las hojas esta comprobado por las escenas (f2, f2barras, f2sin, aviso, f3*,
  f12, f3sigterm), no por una bateria propia de la ventana nativa: las 41 pruebas siguen corriendo contra las Form de la capa.
- Con la capa suelta, `Escritorio` sigue cargado (su constructor corre antes de soltarla para que no pise lo nativo despues): si
  alguien vuelve a tocar un miembro de Escritorio que reparte entrada, no hara nada.
- Nada de esto esta probado en el A4.

## Revision

Revision independiente de a375686..ec834bb (el diff entero leido contra Rocola/ y contra la capa de la que sale cada regla:
Ventanas.Form.cs, Ventanas.Control.cs y Ventanas.Escritorio.cs), con la maquina casi libre (carga media de 1 a 4).

Lo que se comprobo y esta bien:

- Los ocho `override` de Form1 en XP (OnFormClosed, OnShown, OnResize, OnPaintBackground, OnKeyUp, ProcessCmdKey, OnFormClosing y
  Dispose(bool)) siguen en su sitio, con el mismo cuerpo y el `base.X` sustituido donde estaba; `: Form` no queda en ninguna parte;
  `DrawCached(Graphics` y `searchOverlay.ClientRectangle` no quedan (fuera de los obj/ de los arneses de 8b).
- La letra del formulario: `new Font("Microsoft Sans Serif", 8.25f).FontFamily.Name` de la capa y `CajaPieza.LetraDeFabrica.Familia`
  dan el mismo nombre (los dos por `Letras.Resuelve`). `Application.ExecutablePath` era en la capa lo mismo que se escribio.
- El cierre: `VentanaNativa.WmClose` y `FinDeSesion` son linea por linea los de Ventanas.Form.cs (sin la parte de los dialogos, que
  la rocola no es); `FinDeSesion` de la pantalla, el de Escritorio (hojas de delante, la rocola, hojas de detras).
- La composicion con una hoja detras de la rocola (F12 tras activar la rocola) sigue yendo debajo (Anfitrion.Capas la pone antes).
- Compilar desde cero: 0 errores, 15 avisos. NativeAOT: 0 avisos IL/AOT. diff-xp de los 16: las «sin marca» son las de la base
  (medidas las dos). Lineas y CRLF de los 109: 0 distintos.
- pruebas41 41 de 41; pruebas_hoja 86 de 86; pruebas_pantalla 32 de 32; PantallaVistas 194 de 194.
- Escenas alternadas con la referencia (Rform1rev/form1rev): altf4, kill9, f12 y tab IGUALES; sigterm y f3sigterm, lo aceptado, y
  ademas IGUALES a la base a375686 compilada aparte (la capa hacia la misma secuencia de `closing`/`closed`: no cambia nada del
  apagado); captura y salirq IGUALES a la segunda (la primera fue la primera corrida tras arrancar la maquina: la referencia, que va
  delante, llevaba 0 ticks en los segundos 0 y 1 por la lectura en frio). **captura da IGUAL**: lo que quedaba pendiente era la carga.
- NativeAOT: cierreq, altf4 y sigterm sin rocola_estado.txt y kill9 con el; altf4, kill9 y sigterm IGUALES a la de JIT.

Lo encontrado:

1. (menor, arreglado) La cabecera de Nativo/PantallaRocola.Vistas.cs y dos comentarios suyos seguian diciendo «la rocola aun es una
   ventana de la capa» y «lo pone la rocola cuando su ventana sea la pantalla (oleada 8)». Corregidos (solo comentarios).
2. (menor, no se cambia; faltaba decirlo) **Con el dibujo por procesador (gpu=0) el foco al cerrar el buscador ya no es el de la
   referencia.** Escena propia (abrir y cerrar el buscador, y Ctrl+F3 con el abierto), referencia, base y rama: la referencia y la
   base dejan el foco en el buscador escondido y con `this.Focus()` pasa a `Form1'Rocola'`; la rama lo pasa a `HelpBar` al
   esconderlo (PantallaRocola.SeEsconde, de vistas-buscador). Es el riesgo que ya apuntaba PORT_vistas-buscador.md: la capa no sigue
   a WinForms (su ContenedorDelFoco salta la ventana; en WinForms `Form.IsContainerControl` es false, Control.cs:2900 y 5825-5840, y
   SelectNextIfFocused si elige la siguiente), y la lista nativa si. Hasta este grupo la rocola no usaba la lista nativa, asi que
   la diferencia aparece ahora. No se nota en lo que hace la rocola (las teclas pasan antes por ProcessCmdKey y KeyPreview; las
   capturas son las mismas salvo el cielo, que se mueve), pero la traza cambia: `foco HelpBar` donde la referencia dice
   `foco Form1'Rocola'`, y las teclas siguientes «a HelpBar». Con la tarjeta (lo normal) el buscador no toma el foco y no hay
   diferencia. Ninguna de las 23 escenas corre con gpu=0.
3. (menor, no se cambia; faltaba decirlo) **La traza del raton sobre las vistas de la rocola tiene otro formato que la de la capa**
   (escena propia con clics, doble clic y rueda sobre la pared, con tarjeta y sin ella, base contra rama): la pantalla apunta
   `raton arriba X` despues de cada `clic` o `doble` (la capa llamaba a OnMouseUp dentro del mismo `clic`, sin apuntarlo) y el
   `doble` con 2 clics (la capa apuntaba 1 y entregaba 2). Lo que llega al codigo de XP es lo mismo y en el mismo orden (Click,
   MouseClick, MouseUp; DoubleClick, MouseDoubleClick con 2, MouseUp). Lo demas de esa escena (entra, sale, mueve, baja, rueda a la
   vista del foco y despues a `Form1'Rocola'`, el clic en el fondo de la ventana) sale identico. Ninguna de las 23 escenas hace
   clic en la rocola (solo en las hojas), por eso no se vio.
4. (no arreglado) Los commits 1aa762c y ec834bb acaban con «Co-Authored-By: Claude Fable 5.2» y la tarea pedia «Claude Opus 5.5»;
   no se reescribe la historia de la rama (lo decide quien junta). El de la revision lleva lo que manda la sesion.

No repetido aqui (lo corre el coordinador al juntar): los nueve recorridos, las 23 escenas completas, el banco y las cintas. Las
notas «EN LINUX» de las vistas que dicen «en la ventana de la capa la pone su hueco» siguen siendo ciertas para las ventanas de las
41 pruebas; se limpian con la capa (N7).
