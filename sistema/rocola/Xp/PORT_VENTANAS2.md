# Port nativo: las ventanas, segunda tanda (ventanas2)

Sigue a `PORT_VENTANAS.md` (V1-V3: el banco, la pantalla y los relojes nativos, los encargos). Esta tanda convierte lo de WinForms
que el codigo de XP usa con el teclado, la hoja y Ctrl+F3 en piezas nativas propias, en tres pasos: T (las teclas, la flecha y el
foco), H (la hoja y sus piezas con Ctrl+F2 y los avisos) y C (Ctrl+F3 entero). Las reglas son las de siempre (AGENTS.md, decisiones
del 8-11-2026): la logica de XP no cambia, cada linea cambiada lleva su «EN LINUX», cada archivo conserva su numero de lineas y sus
finales, y cada regla de una pieza nativa cita su linea de la fuente de WinForms (dotnet/winforms release/3.0, portada de .NET
Framework 4.8: `/tmp/claude-0/agente-winforms/ref`; ahi se bajaron tambien `Keys.cs` y `KeyPressEventArgs.cs` con su `baja.sh`) o la
documentacion de Win32. Se compara siempre contra la copia congelada de la referencia (`/root/rocola-trabajo/referencia-xp`, aef2211)
con los ganchos de las trazas (`$W/ref-trazas`). Rama `port-ventanas2`, desde a6eba8b mas la pista windows (9cb5ba0) y el grupo
frontera (564788b) juntados. Nada de esto se ha probado en el A4.

## T. Las teclas, la flecha, el foco y el contexto del hilo

### Lo que se convirtio en el codigo de XP

Cada linea en su sitio, sin anadir ni quitar lineas, con sus finales (CRLF o LF) tal cual; lo que sobra queda como comentario
«EN LINUX: (XP) ...».

- `Keys` -> `Tecla` (los mismos numeros): AsistenteRocola.cs (13), CaratulasAdaptativas.cs (1), D3D9Star.cs (2), Form1.Ajustes.cs
  (23), Form1.Biblioteca.cs (10), Form1.cs (30), GuiaRojoGpu.cs (3). (Las cuentas del inventario que traian AtraccionGpu.cs,
  Form1.Buscador.cs, Form1.Datos.cs y Form1.cs:344 son `Dictionary.Keys`, no el tipo: esos archivos no se tocaron por esto.)
- `ProcessCmdKey(ref Message msg, Keys keyData)` de Form1 (Form1.cs:1962) y de la ventana de prueba F12 (GpuTestWindow,
  D3D9Star.cs:1903) -> `internal bool ProcessCmdKey(ref PulsoTecla msg, Tecla keyData)`: ya no sobrescriben nada, la capa las llama.
  El bit 30 de lParam (`(msg.LParam.ToInt64() & 0x40000000) != 0`, la repeticion de la tecla mantenida) -> `msg.Repetida`
  (Form1.cs:1971, 2002, 2018, 2020, 2023, 2148; Form1.Ajustes.cs:1805; AsistenteRocola.cs:186). `return base.ProcessCmdKey(...)` ->
  `return false` (Form1.cs:2154 y 2187, D3D9Star.cs:1906): el de un Form sin menus ni aceleradores da false (Control.cs:9440-9454 sin
  menu contextual y sin padre; ContainerControl.cs:1190-1208 sin ToolStrips; Form.cs:4862-4895 sin MainMenu ni MDI).
- `teclasEnEspera` (Form1.cs:805, 857, 859, 1985, 1986) guarda `KeyValuePair<PulsoTecla, Tecla>` y las repite por el mismo
  ProcessCmdKey.
- `OnKeyUp` de Form1 (Form1.cs:1932): `internal void OnKeyUp(PulsoTecla e)`; su `base.OnKeyUp(e)` (el aviso KeyUp de la ventana, que
  nadie escucha en la rocola) queda en comentario (1935). `Form_KeyPress` (Form1.cs:931) recibe un `PulsoCaracter`.
- `AsistenteEntrada(Tecla, ref PulsoTecla)` (AsistenteRocola.cs:179) y `CaptureConfigKey(Tecla, PulsoTecla)` (Form1.Ajustes.cs:1803).
- `GetKeyState` (Form1.cs:35) -> `PantallaRocola.SegunCola`: el estado segun lo que la pantalla ya atendio. `GetAsyncKeyState`
  (AsistenteRocola.cs:87) -> `TeclasLinux.Fisica`: el estado fisico en el momento (EVIOCGKEY), como en XP.
- La flecha y la pantalla: `Cursor.Hide()`/`Cursor.Show()` -> `PantallaRocola.EscondeFlecha()`/`EnsenaFlecha()` (Form1.cs:769,
  Form1.Ajustes.cs:668 y 1711, Form1.Datos.cs:1870 y 1970); `Cursor.Position = ...` -> `PantallaRocola.FlechaEn(x, y)` (Form1.cs:770 y
  909); `Screen.PrimaryScreen.Bounds` -> la pantalla nativa, `PantallaRocola.Ancho` y `Alto` (Form1.cs:770 y 908).
- El foco (ForceFocus, Form1.cs:882-915; FocusTimer_Tick, 921; RocolaConTeclado, AsistenteRocola.cs:77): `GetForegroundWindow() ==
  this.Handle` -> `PantallaRocola.Delante == this`; `SetForegroundWindow(this.Handle)` y `this.Activate()` -> `PantallaRocola.AlFrente(this)`
  (Form.Activate es SetForegroundWindow, Form.cs:2869-2882); `this.BringToFront()` -> `PantallaRocola.Encima(this)` (de una ventana de
  arriba, SetWindowPos(HWND_TOP), Control.cs:4881-4894); `SetFocus(this.Handle)` -> `PantallaRocola.Enfoca(this)`. `TopMost` y el truco
  de `AttachThreadInput` (pegarse al hilo de OTRO programa que tiene el foco) quedan en comentario: en la pantalla de la rocola no hay
  otros programas, la de delante es de este hilo o ninguna, y XP no se pegaba (fgT == myT o 0). `mouse_event` (el clic sintetico para
  que Windows le de el foco a un programa de fondo) tambien: aqui la rocola solo deja de estar delante debajo de una hoja suya modal, y una
  ventana deshabilitada por una modal no recibe el clic. Las declaraciones de esas llamadas (Form1.cs:36-42) quedan en comentario.
- Program.cs: `Application.SetUnhandledExceptionMode(CatchException)` (94): la pantalla nativa ya da a `AlFallar` lo que lanzan sus
  teclas, relojes, encargos y pintados; en su linea va ahora, a la vista, el gancho de las pruebas de Linux que necesitan hilos
  (`PruebasLinux.DespuesDelInicio`: `--prueba-video`, `--prueba-lector`, `--prueba-salida`), que antes iba escondido dentro del
  `SetUnhandledExceptionMode` de la capa, en el mismo momento del arranque. `EnableVisualStyles` y
  `SetCompatibleTextRenderingDefault(false)` (104-105) quedan en comentario: el tema de XP y el texto de GDI son de las piezas.
- Las piezas de Ctrl+F3 que aun heredan de la capa (Deslizador, Interruptor, BotonPlano: Form1.Ajustes.cs:456-471, 532-535, 591-595) y
  los KeyDown de las hojas (Form1.Ajustes.cs:1687, CaratulasAdaptativas.cs:94) comparan con `Tecla`.

### Las piezas nativas

- `Nativo/Tecla.cs`:
  - `Tecla`: los numeros de Keys (VK_* y Mayus 0x10000, Ctrl 0x20000, Alt 0x40000; Keys.cs:23-28 y 977-987), porque
    rocola_teclas.txt guarda los botones como numeros (LoadKeys/SaveKeys no cambian de formato) y Botones.cs traduce evdev a ellos. Y
    los mismos NOMBRES que tenia Keys en la referencia, no menos: XP escribe cada tecla con su nombre en la marca de la pantalla
    (Form1.cs:1964, `VigiaPantalla.Hace("una tecla (" + keyData + ")")` -> «una tecla (F3, Control)», lo que la bitacora dice de un
    tiron); con solo los que XP compara saldria otro texto (un numero u otra combinacion de [Flags]). A la referencia ya le faltaban 22
    nombres de Keys.cs, de 18 codigos (las teclas de Asia, del IME, del navegador menos BrowserBack y de lanzar programas: 0x15,
    0x17-0x19, 0x1C-0x1F, 0xA7-0xAC, 0xB4-0xB7), que Botones.cs no da nunca. (Revision: decia «21 nombres».)
  - `PulsoTecla` (una tecla apretada o soltada; solo lo que XP lee, segun el inventario): `KeyData`; `KeyCode` (la tecla sola, o None si
    Keys no define el codigo: KeyEventArgs.cs, «Keys can be discontiguous, keeping Enum.IsDefined», con la lista de Keys.cs);
    `Handled`; `SuppressKeyPress` (que tambien pone Handled, KeyEventArgs.cs); `Repetida` (el bit 30 de WM_KEYDOWN, «the previous key
    state»). Sin Shift, Control, Alt ni Modifiers: XP no los lee.
  - `PulsoCaracter` (el WM_CHAR): `KeyChar` y `Handled`, como KeyPressEventArgs.cs.
- `Nativo/PantallaRocola.Teclado.cs` (parte de la pantalla nativa):
  - `SegunCola(vk)` y `AnotaTecla(vk, abajo)`: el estado de cada tecla segun lo ya atendido (GetKeyState: «changes as a thread reads key
    messages from its message queue»); se anota al atender la tecla, antes de darsela a nadie, y de el sale `Repetida`.
  - `Ancho` y `Alto` (Screen.PrimaryScreen.Bounds; las pone el anfitrion); la flecha: la cuenta de ShowCursor (`EscondeFlecha`,
    `EnsenaFlecha`, `FlechaVisible` con la cuenta >= 0: Cursor.cs:483-486 y 611-614) y donde esta (`Flecha`, `FlechaEn`: SetCursorPos,
    Cursor.cs:234-242, sin avisar a nadie, como en la referencia).
  - `Delante`, `AlFrente`, `Encima`, `Enfoca`: la ventana de delante y el foco, con las reglas de arriba. TRANSICION: mientras las
    ventanas sean las de la capa, las contesta ella (Escritorio, al empezar, hace lo mismo que hacia la referencia: la activa, Form.Activate,
    AlFrente y Control.Focus).
  - `Contexto` y `PonContexto()`: el SynchronizationContext del hilo de la pantalla, en lugar del de la capa (ContextoVentana). Como el
    WindowsFormsSynchronizationContext que WinForms pone al crear el primer control y al empezar el bucle (InstallIfNeeded): Post es un
    encargo de la pantalla sin dueno (BeginInvoke a una ventana que vive lo que el hilo: no se pierde) y Send, desde otro hilo, espera a
    que se haga (Invoke); en su hilo, en el momento. Lo usan SalidaPropia.cs (avisa la parada del sonido) y el BackgroundWorker del
    repaso (Form1.cs: RunWorkerCompleted, el «encargo contexto» de las trazas).
- `TeclasLinux.Fisica(vk)` (Plataforma/TeclasLinux.cs, lo nativo de la entrada): el estado fisico de la tecla en el momento, lo que
  contesta GetAsyncKeyState («whether a key is up or down at the time the function is called»): el del nucleo con EVIOCGKEY en cada
  aparato abierto (linux/input.h:171, «get global key state»: las teclas abajo aunque su evento aun no se haya leido; el numero,
  0x80604518 = EVIOCGKEY(96), comprobado compilando contra linux/input.h, con KEY_MAX 0x2ff de input-event-codes.h:812), y el de las
  teclas de prueba (ROCOLA_PULSA: se marca antes de mandar su evento, como el estado fisico de Windows va antes que su WM_KEYDOWN). Un
  codigo de Windows puede venir de varias teclas de Linux (las dos Mayus): cuenta cualquiera (Botones.cs al reves).
- `Plataforma/LlamadasWindows.cs` se quedo vacio (lo que conservaba de esta pista: TeclaAbajo, VentanaDelante, PonDelante, PonFoco) y se
  borro.

### Lo que convive con la capa, y cuando se va

- La capa (Escritorio) sigue decidiendo quien tiene el foco y el orden de WinForms con cada tecla (ProcessCmdKey por la cadena de padres,
  IsInputKey, ProcessDialogKey, KeyPreview, KeyDown, el caracter, KeyUp; ya revisado contra la fuente) y, donde la tecla llega a la
  rocola, llama a lo nativo: `Escritorio.TeclaDeMando` (el ProcessCmdKey de Form1 o de GpuTestWindow cuando la tecla llega a su
  ventana de arriba) y `TeclaArriba` (el OnKeyUp de Form1). Se van cuando la pantalla le de las teclas a la rocola sin la capa (Form1 sin
  Form) y la ventana F12 sea una hoja (paso H).
- `KeyEventArgs` y `KeyPressEventArgs` de la capa son ahora `PulsoTecla` y `PulsoCaracter` con su nombre de WinForms, para las piezas
  que aun heredan de la capa (sus OnKeyDown: las de Ctrl+F3 en el paso C; las vistas en la tanda siguiente). `Keys`, `Message`,
  `Screen`, `UnhandledExceptionMode`, `ContextoVentana`, y `Cursor.Hide/Show/Position/Current/Clip` ya no estan en la capa; `Cursor`
  y `Cursors` quedan solo para `Control.Cursor` (la mano de las piezas de Ctrl+F3: paso C).
- Form1 sigue con `this.KeyPreview = true; this.KeyPress += Form_KeyPress;` (Form1.cs:713-714: el aviso KeyPress de la capa, con
  `PulsoCaracter`): se van con «Form1 sin Form», cuando la pantalla le de los caracteres a Form_KeyPress directo.
- El raton de las vistas y del panel de la tarjeta (los MouseMove, MouseDown, MouseWheel y MouseClick de Form1.Video.cs:851-882 y
  Form1.cs:1125-1126 y 1334, con MouseEventArgs y MouseButtons) son avisos de controles que aun heredan de la capa: pasan a `Raton`
  con las vistas y GpuPanel (tanda siguiente). En este paso no hizo falta `Raton`.

### Diferencias con la referencia, cada una porque XP hace lo nuevo

1. GetAsyncKeyState es el estado fisico (EVIOCGKEY y las teclas de prueba), no lo ya atendido: la referencia contestaba lo mismo a
   GetKeyState y a GetAsyncKeyState (documentacion de Win32 de las dos; AsistenteRocola.cs lo explica: «vale tambien el estado fisico
   del boton (GetAsyncKeyState), que no depende de quien se quedo con el mensaje»). Solo cambia algo si un boton se aprieta o se suelta
   entre que llega su evento y la pantalla lo atiende (los dos a la vez); en las escenas, no.
2. `PulsoTecla.KeyCode` (el KeyEventArgs.KeyCode de las piezas y el de Form1.OnKeyUp) es None para un codigo que Keys no define
   (KeyEventArgs.cs); la referencia daba el codigo tal cual. Botones.cs no da ninguno de esos: no cambia nada en Rocola OS.

### El banco, lo nuevo

- Tres escenas mas (`Pruebas/Ventanas/guiones/escenas.sh`; no estan en «todas»: se piden por su nombre):
  - `asistente`: ROJO mantenido 1,5 s y ABAJO (abre la ayuda: RojoMantenido con lo ya atendido, GetKeyState, y lo fisico,
    GetAsyncKeyState); ABAJO con la ayuda abierta (se lo queda la ayuda: la letra sigue en L); ABAJO mantenido 0,8 s y ROJO (la cierra:
    AbajoMantenido; la repeticion acaba dentro del regreso animado de 0,8 s, si no la letra cambiaria segun la hora); ABAJO (ahora si cambia
    la letra). En las dos queda en rocola_error.txt «Rojo mantenido sin pistas: la ayuda esta preguntando».
  - `captura`: F8 y el asistente de los botones (CaptureConfigKey): una tecla mantenida 1,2 s (sus 21-22 repeticiones no cuentan: msg.Repetida),
    la misma otra vez («ya usada»), las otras siete; despues la moneda nueva da 4 creditos. rocola_teclas.txt se compara byte a byte
    (corre.sh lo guarda y compara_escena.py lo compara, como rocola_config.txt).
  - `salirq`: Ctrl+Alt+Q con la Q mantenida 1,2 s (22 repeticiones, que no cuentan) y dos Q mas: se cierra con UserClosing y
    rocola_estado.txt falta.
- Su ruido, de la referencia contra si misma (3 corridas, RT1-RT3): `RUIDO=/root/rocola-trabajo/port/ventanas2/ruido` (compara.sh
  busca ahi primero y despues en `$W/ruido`; ruido.sh lo escribe ahi y con `PREFIJO=RT` mide las RT1..RTN). Las tres, secuencia igual.
- Las 41 pruebas se compilan igual contra la referencia y contra un programa con Tecla (pruebas41.sh lo nota en el rocola-xp.dll y
  compila con `Nativo=true`: `using Keys = Rocola.Tecla`).
- Las reglas de la pantalla nativa: 4 mas en `Pruebas/Ventanas/Pantalla` (con PantallaRocola.Teclado.cs): las teclas ya atendidas y la
  repeticion, la cuenta de ShowCursor, la ventana de delante y el foco (los pide a quien lleva las ventanas), y el contexto del hilo (Post
  como encargo en orden, Send desde otro hilo espera, en su hilo en el momento). pruebas_pantalla.sh ya borra la compilacion de NativeAOT
  que dejaba en `src/.../bin` (unos 84 MB por corrida).
- `Pruebas/Tarjeta/guiones/diff-xp.py` quita los CR de los dos lados: con un archivo de XP con CRLF daba todo el archivo como cambiado
  (Form1.cs: 2742 lineas «quitadas» antes de este paso; 58 con el arreglo).

### Comprobaciones (8-11-2026, maquina de pruebas compartida con otras pistas, carga de 4 a 7, llvmpipe)

- Compila (`dotnet build -c Release`): 0 errores y los mismos 15 avisos de la base, en los mismos archivos y lineas (CA2014, CA2022,
  CS1690, SYSLIB0050, SYSLIB0054), antes y despues de juntar la rama principal.
- `--prueba-lector` (el gancho de Program.cs:94) sigue corriendo antes de abrir la rocola: lee un MP3 de prueba y sale.
- Las reglas de la pantalla nativa: 32 de 32 (28 de antes y 4 nuevas) en JIT y en NativeAOT.
- Las 41 pruebas: 41 de 41 con este programa (antes y despues de juntar) y 41 de 41 con la referencia de los ganchos, con el mismo
  fuente; la salida de las dos es la misma linea por linea.
- Las escenas, alternando referencia (RT) y traduccion (T) para que corran con la misma carga:
  - RT1/T1: arranque, moneda, video, buscador, cierreq y kill9 IGUALES; sigterm y f3sigterm distintos solo en lo aceptado en V2
    (encargo Apagado, FormClosing y FormClosed con WindowsShutDown, `rocola_estado.txt` borrado). tab: traza igual, pero en la hoja de
    Ctrl+F3 los textos del repaso de la biblioteca («33 %» frente a «0 %», «ya esta al dia» frente a «faltan 3 archivos»: 3886 puntos
    distintos) porque en T1 el repaso de fondo iba atrasado con la maquina a carga 7; altf4: los relojes y el vigia de la pantalla en
    total, fuera del ruido (la referencia arranco 128 ms tarde). Otra vez alternando (RT2/T2): tab y altf4 IGUALES; y la referencia
    contra si misma (RT1 contra RT2) en altf4 tambien sale del ruido medido (163 frente a 206 encargos del vigia): es la carga.
  - asistente, captura y salirq (RT1/T1, con su ruido): IGUALES (secuencia, estados, rocola_teclas.txt byte a byte, las capas de
    Dibujo2D a 0 puntos distintos, `rocola_estado.txt` como XP).
  - Despues de juntar la rama principal (564788b: el grupo frontera), las 13 alternadas (RT4/T4): arranque, moneda, video, buscador,
    cierreq, altf4, kill9, asistente y captura IGUALES; sigterm y f3sigterm, lo aceptado; salirq con la secuencia y los estados iguales
    y 6 relojes de 42 ms mas en total (254 frente a 260, holgura 4: el ruido de tres corridas es corto); tab otra vez con el repaso
    atrasado en la hoja de Ctrl+F3.
  - El repaso atrasado de tab no es de este paso: es una carrera de los dos carriles del repaso con la pantalla (si la pantalla aun no
    publico la medida entera de una cancion cuando el otro carril llega a la segunda tanda, la vuelve a medir: el encargo de
    Form1.Biblioteca.cs:1735, y a los 17,4 s aun «faltan 2 archivos»). Pasa en la referencia (R7-tab de V3) y en el programa de la rama
    principal sin este paso (564788b contra la referencia, 3 pares: M1 con «faltan 5 archivos» y el encargo de 1735, M2 y M3 al dia);
    con este paso: T1, T4 y T5 atrasadas, T2 y T6 al dia (T6 corrida antes que su referencia); la referencia al dia en RT1-RT6. En las
    corridas en que el repaso llega a tiempo, tab sale IGUAL (RT2/T2, RT6/T6).
- NativeAOT (`dotnet publish -c Release -r linux-x64 -p:PublishAot=true`): 0 avisos de IL ni de AOT (los 15 de compilacion de siempre),
  antes y despues de juntar. Con el ejecutable nativo contra la referencia (AT1/RT3 y, ya junto, AT2/RT7): arranque, cierreq y kill9
  IGUALES; sigterm, lo aceptado; asistente, captura y salirq con la secuencia, los estados, rocola_teclas.txt y las capas iguales y mas
  relojes en el primer segundo (19-22 frente a 5-11: NativeAOT arranca antes; lo mismo que se vio en V3).
- `rocola_estado.txt`: falta tras Ctrl+Alt+Q x3 (cierreq y salirq), Alt+F4 y SIGTERM; queda tras SIGKILL; en JIT y en NativeAOT.
- `diff-xp.py` en los 9 archivos de XP tocados: todas las lineas cambiadas en este paso llevan su «EN LINUX»; las que salen sin marca
  son de antes y de otras pistas (las TextRenderingHint del dibujo, Equipo.ProcesadorMhz y PrioridadHilo de la pista windows, el
  bloque de la tarjeta en D3D9Star.cs, el SetForegroundWindow de la otra rocola en Program.cs) y una linea en blanco de Form1.cs que
  diff mete en el trozo de ForceFocus sin que cambie. Cada archivo conserva su numero de lineas y sus finales (Form1.cs: 2785 lineas,
  2741 CRLF; `git diff --stat`: solo las lineas cambiadas).
- El inventario con el compilador (`Pruebas/Inventario`, en una copia que tambien compila Nativo/: 163 archivos, 0 errores):
  referencias a WinForms de 4400 a 4148. Ya no hay ninguna a Keys, Message, KeyPressEventArgs, Control.ProcessCmdKey,
  Control.OnKeyUp, Cursor.Hide/Show/Position, Screen, Form.Activate, Form.TopMost, Application.EnableVisualStyles,
  SetCompatibleTextRenderingDefault ni SetUnhandledExceptionMode. Quedan, para sus pasos: Control.IsInputKey(Tecla) 6,
  Control.OnKeyDown(KeyEventArgs) 8 y el tipo KeyEventArgs 7 (las piezas y las hojas de Ctrl+F3: paso C), Control.Cursor y
  Cursors.Hand 3 (paso C), MessageBox 5 (paso H), Control.Handle 2 (el dispositivo de la ventana F12 y el del panel: con la tarjeta).

### Lo que queda

- Los pasos H (la hoja, Ctrl+F2, los avisos, F12) y C (Ctrl+F3).
- Con la tanda siguiente: el raton de las vistas y del panel (`Raton`), y con «Form1 sin Form» las lineas de KeyPreview y KeyPress de
  Form1 (713-714), `Escritorio.TeclaDeMando`/`TeclaArriba` y los ganchos de la ventana de delante (que pasaran a la pantalla).
- EVIOCGKEY no se pudo probar con un teclado de verdad: la maquina de pruebas no tiene /dev/input (ni uinput). El numero se comprobo
  contra linux/input.h; el resto (los bits de cada tecla, que el aparato este abierto) se vera en Rocola OS.
- SetCursorPos: en Windows mover la flecha suele mandar un WM_MOUSEMOVE a la ventana de debajo; la referencia no avisa a nadie y asi
  se queda (en XP no cambia nada: en OnShown el panel de la tarjeta aun no existe, y la ultima rama de ForceFocus solo pasa debajo de
  una hoja modal).
- El caracter de cada tecla sigue con la tabla de la referencia (Escritorio.Caracter); la distribucion del teclado del A4 (acentos,
  la ñ) sigue siendo un riesgo del diseno.
- Fuera de este paso, para quien corresponda: `Pruebas/BancoDibujo/Nuevo` no compila desde que incluye todos los `Nativo/*.cs`
  (DescompresorMp3.cs y otros de la pista windows necesitan NAudio y NLayer; PantallaRocola.Rocola.cs, Form1); en `$W/pantalla`
  quedan unos 336 MB de compilaciones de NativeAOT de corridas viejas de pruebas_pantalla.sh; LEEME.md nombra
  `Plataforma/LlamadasWindows.cs`, que ya no existe.
- Nada de esto se ha probado en el A4.

### Revision (revisor independiente, 8-11-2026)

Se reviso el paso entero (65a26c5, el merge f5da162 y 3933e0c) contra `Rocola/` (XP), contra la fuente de WinForms
(`/tmp/claude-0/agente-winforms/ref`) y contra la capa que sustituye (la de 9cb5ba0), y se repitieron las comprobaciones con etiquetas
propias (RV para la referencia, V y VA para este programa).

Lo comprobado en el codigo:

- Cada llamada cambiada hace lo mismo que la capa de antes: en ForceFocus, `TopMost` de la referencia era una propiedad sin efecto,
  `SetForegroundWindow` y `Activate` eran los dos `Form.Activate`, `BringToFront` de una ventana sin padre era `Escritorio.AlFrente`,
  `SetFocus` era `Control.Focus` y `GetForegroundWindow() == Handle` comparaba con la activa sin desechar: son los cuatro ganchos de
  ahora. ProcessCmdKey: en la capa solo lo sobrescribian Form1 y GpuTestWindow y la cadena de padres acababa con false en la ventana de
  arriba, asi que `TeclaDeMando(TopLevelControl)` da lo mismo (con Ctrl+F3 abierto, false sin llamar a la rocola, como antes y como
  WinForms con la hoja modal). El `base.OnKeyUp` que se quito solo lanzaba el aviso KeyUp de la rocola, y nadie lo escucha. `Contexto`:
  Post como ContextoVentana (Send no lo usa nadie: SalidaPropia.cs y el BackgroundWorker usan Post). `AnotaTecla` va donde iba
  `TeclasAbajo` (antes de mirar el destino de la tecla).
- `Tecla` tiene los mismos miembros, valores y orden que la `Keys` de la referencia (las dos listas, comparadas: iguales), asi
  `keyData.ToString()` de VigiaPantalla.Hace escribe lo mismo; `DefineKeys` es exactamente el conjunto de codigos de Keys.cs (comprobado
  de 0 a 0xFFFF contra los valores del archivo: 0 diferencias). EVIOCGKEY = (_IOC_READ << 30) | (96 << 16) | ('E' << 8) | 0x18 = 0x80604518.
- Lo que quedo sin convertir tiene su razon: `Cursor = Cursors.Hand` y los `KeyEventArgs`/`IsInputKey` de las piezas de Ctrl+F3
  (paso C); el raton de Form1 (Form1.cs:1125-1126 y 1334, Form1.Video.cs:851-882) y de las vistas son avisos de controles que aun heredan
  de la capa, y el diseno pone `Raton` con IVista y GpuPanel (apartados 5 y 6); KeyPreview y KeyPress de Form1 (713-714), con «Form1 sin
  Form». La escena `captura` usa F8 porque el asistente de los botones (StartConfig) solo se abre con F8 o la primera vez, no desde Ctrl+F3.
- Finales de linea: los 9 archivos de Codigo tienen las mismas lineas y los CRLF en las mismas lineas que antes del paso (comparado
  linea por linea, no solo la cuenta).

Lo encontrado y lo hecho:

1. (menor, arreglado) Citas: Tecla.cs citaba «Control.cs:9600-9700» (lo de SuppressKeyPress es 9680-9685) y KeyEventArgs.cs,
   KeyPressEventArgs.cs y WindowsFormsSynchronizationContext.cs sin linea; decia que a Tecla le faltan las teclas «del navegador»
   (BrowserBack si esta) y que «con menos nombres saldria un numero» (con [Flags] tambien puede salir otra combinacion u otro alias: por
   eso van los mismos nombres y en el mismo orden); y este documento decia «21 nombres» (son 22, de 18 codigos). Solo comentarios.
2. (nota para el paso H, sin cambio) `Encima` (BringToFront): WinForms solo sube una ventana de arriba si esta habilitada (Control.cs:4887,
   IsWindowEnabled); la capa, como la referencia, no lo mira. Hoy no cambia nada: ForceFocus no corre con una hoja modal abierta (Ctrl+F3 y
   Ctrl+F2 ponen maintMode y lo llaman en su finally, ya cerradas) salvo si un encargo abre Ctrl+F2 con Ctrl+F3 abierto, y aun asi la
   rocola queda debajo de sus hojas (SubeLasSuyas). Cuando las ventanas sean nativas, Encima tiene que mirarlo.
3. (sin cambio) Program.cs:94 llama a la vista al gancho de las pruebas de Linux: es el mismo momento en que corria escondido en
   SetUnhandledExceptionMode, sin `--prueba-*` no hace nada, y sacarlo de ahi lo esconderia otra vez en una pieza nativa o pediria otro
   Main en Plataforma (StartupObject) que cambiaria el de XP. Queda en los riesgos.
4. (sin cambio) `ioctlBits` declara la peticion como `uint` (en C es `unsigned long`), como el `ioctl` que ya habia en TeclasLinux.cs y
   en Botones.cs: en x86-64 el valor llega con la mitad de arriba a cero y el nucleo la lee como `unsigned int`.
5. Las cuentas de `captura` y `salirq` con el ruido de tres corridas salen fuera por poco (el vigia de la pantalla, 430 frente a 445; el
   reloj de 42 ms, 260 frente a 265), pero la referencia contra si misma ya se separa mas: en seis corridas suyas (RT1-RT4, RT7, RV1) el
   vigia va de 353 a 466 y el reloj de 42 ms de 240 a 261. Con ese ruido (`/root/rocola-trabajo/port/ventanas2/rv/ruido`, 6 corridas)
   salen IGUALES. La secuencia, los estados, rocola_teclas.txt y las capas, iguales siempre.
6. (fuera de este paso, para el coordinador) La carrera del repaso en `tab` (el encargo de Form1.Biblioteca.cs:1735 y «faltan N archivos»
   en la hoja de Ctrl+F3): contando todas las corridas guardadas (`ventanas/salidas` y `ventanas2/salidas`), la referencia la da 1 vez en
   23 (R7) y las traducciones 6 en 21 (N5 de V3, J1, M1 de la rama principal sin este paso, y T1, T4, T5); con este paso, en mis cuatro
   pares (V1, V3, V4, V5) no salio. No viene de este paso (sale sin el: N5, J1, M1), pero la diferencia parece de la pantalla nativa o de
   la tanda V2/V3 (la latencia de los encargos frente a las teclas con la maquina cargada): habria que medirla alli.

Comprobaciones repetidas (8-11-2026, maquina compartida, carga de 3 a 7):

- Compila: 0 errores y 15 avisos; la lista archivo:linea:codigo es la misma que la de la rama principal sin el paso (564788b, compilada
  aparte de un `git archive`), antes y despues de los arreglos.
- Las 41 pruebas: 41 de 41 con este programa y 41 de 41 con ref-trazas; la salida de las dos, igual linea por linea.
- Las reglas de la pantalla: 32 de 32 en JIT y en NativeAOT (sin avisos de IL ni de AOT: solo dos CS0649 del propio banco).
- Escenas alternadas con la referencia (RV1/V1, JIT): arranque, moneda, video, tab, buscador, cierreq, altf4, kill9 y asistente IGUALES;
  captura y salirq IGUALES con el ruido de seis corridas (punto 5); sigterm y f3sigterm, solo lo aceptado en V2 (encargo Apagado, closing
  y closed con WindowsShutDown y rocola_estado.txt borrado). Las capas de Dibujo2D, 0 puntos distintos en las 35 comparadas (las hojas de
  Ctrl+F3 de moneda y tab incluidas); los estados de XP y de cada pieza, todos iguales; rocola_teclas.txt igual en las 13;
  rocola_estado.txt falta tras cierreq, salirq, altf4 y SIGTERM y queda tras kill9. Tres pares mas de tab (RV3-RV5/V3-V5): IGUALES.
  Con el programa final (los arreglos son comentarios), asistente, captura, salirq y cierreq otra vez (RV6/V6): IGUALES.
- NativeAOT (`dotnet publish -c Release -r linux-x64 -p:PublishAot=true`): 0 avisos de IL ni de AOT (solo los 15 de compilacion).
  Contra la referencia (RV2/VA1): arranque, cierreq, kill9 y captura IGUALES; sigterm, lo aceptado; asistente y salirq iguales salvo el
  reloj de 42 ms en el primer segundo (11 frente a 22 y 9 frente a 21: NativeAOT arranca antes, como en V3); estados, rocola_teclas.txt y
  capas iguales; rocola_estado.txt como debe.
- `diff-xp.py -v` en los 9 archivos: lo mismo que dice el paso (sin marca solo las lineas de otras pistas y la linea en blanco de Form1.cs).
- Nada de esto se ha probado en el A4.

## H. La hoja y sus piezas: Ctrl+F2 y los avisos

Lo que en XP eran las ventanas de dialogo de WinForms (Form con Label, ListBox y Button; MessageBox; FolderBrowserDialog) pasa a piezas
nativas de la rocola en `Nativo/`, y el codigo de XP de Ctrl+F2 y de los avisos las llama directo. Base: d94eaed (la rama principal con el
paso T ya juntado; el merge del principio fue un avance rapido). Nada de esto se ha probado en el A4.

### Lo que se convirtio en el codigo de XP

Cada linea en su sitio, con su «EN LINUX», sin anadir ni quitar lineas y con sus finales (Form1.Datos.cs: 3436 lineas, 3426 CRLF;
Form1.Ajustes.cs: 2044, 2011 CRLF; Program.cs: 148, LF; `git diff --stat`: 23 lineas cambiadas, las de abajo). El guion que las cambio
queda como registro: `/root/rocola-trabajo/port/ventanas2/h/traduce_h.py` (avisa si una linea no estaba como se esperaba).

- Ctrl+F2 (`ShowMusicDirManager`, Form1.Datos.cs:1871-1957), 18 lineas: `new Font(FontFamily.GenericSansSerif, 10f)` ->
  `Letra.EnPuntos(Letras.SinRemates, 10f)` (la misma letra, ya nativa: la de la hoja); `new Form()` -> `new Hoja(MarcoXp.Dialogo)`; las
  lineas de `FormBorderStyle.FixedDialog`, `MaximizeBox/MinimizeBox/ShowInTaskbar/ShowIcon` y `StartPosition.CenterParent` quedan en
  comentario (es lo que da `MarcoXp.Dialogo`); `Label` -> `Rotulo`; `ListBox` -> `ListaXp`; `BorderStyle.FixedSingle` ->
  `BordeXp.FixedSingle` y `FlatStyle.Flat` -> `EstiloBoton.Flat` (los mismos nombres, como Tecla con Keys); `Button` -> `BotonXp` (dos
  lineas); `FolderBrowserDialog` -> `SelectorCarpetas` y `fb.ShowDialog(dlg) != DialogResult.OK` -> `fb.MuestraModal(dlg) != Resultado.OK`;
  `MessageBox.Show(dlg, ...)` -> `AvisoXp.Muestra(dlg, ...)` (tres); `dlg.CancelButton = bOk` -> `dlg.BotonCancelar = bOk`;
  `dlg.ShowDialog(this)` -> `dlg.MuestraModal(this)`. Las demas lineas de armado (SetBounds, Text, ClientSize, BackColor, ForeColor, Font,
  IntegralHeight, HorizontalScrollbar, FlatAppearance.BorderColor, Items.Clear/Add, SelectedIndex, Enabled, Click, Controls.Add, Close,
  Description, ShowNewFolderButton, SelectedPath) no cambian: las piezas tienen esos miembros, y solo esos.
- `MonedaTambienAqui` (Form1.Ajustes.cs:2009-2021), 4 lineas: `(Form dlg)` -> `(ITeclasAntes dlg)`; `dlg.KeyPreview = true;` queda en
  comentario (suscribirse a TeclaAntes y TeclaArribaAntes lo pone); `dlg.KeyDown += delegate (object so, KeyEventArgs ke)` ->
  `dlg.TeclaAntes += delegate (PulsoTecla ke)` y el KeyUp igual con `TeclaArribaAntes`. La llaman Ctrl+F2 (ya hoja) y Ctrl+F3 y el
  historial de caratulas (aun de la capa: paso C), por eso el tipo es la interfaz y la capa la cumple mientras dure.
- Program.cs:98: `MessageBox.Show(aviso, "Rocola - Carpeta de datos", MessageBoxButtons.OK, MessageBoxIcon.Warning)` ->
  `AvisoXp.Muestra(aviso, "Rocola - Carpeta de datos", BotonesAviso.OK, IconoAviso.Warning)`.
- El inventario con el compilador (`Pruebas/Inventario`, la copia de `/root/rocola-trabajo/port/ventanas2/inv`, 177 archivos, 0 errores):
  referencias a WinForms de 4148 a 4058. En Ctrl+F2 (Form1.Datos.cs:1860-1972) y en MonedaTambienAqui ya no queda ninguna; de MessageBox
  queda solo D3D9Star.cs:1885 (el de la ventana F12, que va con el panel de la tarjeta). En Program.cs queda `Application.StartupPath`
  (linea 29, el texto del aviso: la carpeta del programa; no es de las ventanas).

### Las piezas nativas

Sin clase base comun para las piezas y sin la API de WinForms (ni Handle, ni mensajes, ni Invoke, ni IWin32Window, ni Dock, Anchor,
AutoScroll o maquetado). Lo que toda pieza tiene en comun va en su CAJA (`CajaPieza`, una por pieza, por composicion), y cada pieza expone
de ella solo lo que XP usa de esa pieza; lo que solo usan la pantalla, la composicion y las pruebas es `internal`. Cada regla cita su linea
de la fuente de WinForms (`/tmp/claude-0/agente-winforms/ref`), comprobada una por una contra esos archivos (las de la primera escritura,
de memoria, casi todas apuntaban a otras lineas: solo coincidian BringToFront, Form.Activate y Control.ProcessCmdKey; se corrigieron todas
con el principio y el final de cada metodo). Las reglas se PORTARON de la capa (Ventanas.Form.cs,
Ventanas.Control.cs y Ventanas.Escritorio.cs, revisadas contra la fuente), con las mismas llamadas a las funciones nativas del dibujo y los
mismos argumentos que les pasaba la capa: por eso los puntos salen iguales.

- `Nativo/Pieza.cs`: `IPieza` (la caja y lo que la pieza hace al pintarse, con las teclas, con el raton y con el foco; lo de fabrica, con
  metodos por defecto de la interfaz: no hacer nada, como los On* de Control); `CajaPieza` (sitio y SetBounds, el padre, BackColor con
  IsValidBackColor y el error de un fondo transparente, ForeColor y Font heredados, Text, TabIndex sin poner = -1, TabStop, Visible y
  Enabled efectivos con SelectNextIfFocused, VisibleChanged que baja a las hijas que se ven, el pintado con la «X» roja de
  PaintWithErrorHandling, BringToFront, Dispose en cascada con Disposed); `IContenedor` (la hoja y los grupos); `Piezas`
  (ControlCollection: Controls[0] arriba, Add con SendToBack y el TabIndex siguiente, Remove con el foco que se va); `Raton`
  (MouseEventArgs con Handled de la rueda); `ITeclasAntes`; y los tipos con los numeros y nombres de WinForms que XP o las trazas usan:
  `Resultado` (DialogResult), `RazonCierre` (CloseReason), `AlineaTexto` (ContentAlignment), `BordeXp` (BorderStyle), `BotonRaton`.
- `Nativo/Hoja.cs` (y `Hoja.Foco.cs`, `Hoja.Entrada.cs`, `Hoja.Pinta.cs`): la ventana. El marco de XP Luna (barra de 26 y borde de 3:
  966x764 para 960x732, 646x404 para Ctrl+F2), el titulo, la «X» con sus tres estados (normal, encima, apretada), el arrastre por la barra
  y el borde (las zonas de WM_NCHITTEST); CenterParent sobre su duena con los topes del area de trabajo, y CenterScreen sin duena; el modal
  (ShowDialog: duena dada o la activa; el mismo bucle de la pantalla anidado, PantallaRocola.CorreModal, asi siguen los relojes, los
  encargos y la tarjeta; las ventanas de debajo sin teclas ni raton y sin tocar su Enabled; al acabar, la que estaba activa vuelve, se
  esconde, se destruye su ventana y los encargos pendientes con ella se pierden); Close y WmClose con UserClosing, CheckCloseDialog
  (closing una vez, closed al acabar), el apagado (WM_QUERYENDSESSION y WM_ENDSESSION con WindowsShutDown, y FormOwnerClosing a las suyas);
  la activacion y el orden (las suyas encima de su duena); el foco (al abrir, la primera del tabulador; Tab y Mayus+Tab entrando en los
  grupos con el GetNextControl de WinForms; flechas dentro del mismo padre con vuelta; la del foco que se esconde, se apaga o se quita pasa
  a la siguiente; al cerrar, de vuelta a su sitio; LostFocus y GotFocus en su orden); las teclas en el orden de WinForms (ProcessCmdKey
  -false en una hoja de XP-, IsInputKey, ProcessDialogKey de la pieza a la hoja: Intro al boton por defecto, Escape al de cancelar, Tab y
  flechas; KeyDown primero en la hoja con KeyPreview y despues en la pieza; el caracter salvo con SuppressKeyPress; Alt+F4 sin atender cierra
  la hoja por la cola; KeyUp); el raton (la de encima de todo, MouseEnter/Leave, captura al bajar, Click y MouseClick solo soltando encima de
  la misma pieza, doble clic de 500 ms y 4 puntos -el de XP-, la rueda a la del foco que sube al padre si no la atiende, la mano); el
  pintado (la hoja entera en su superficie cuando algo cambio, como la capa de la referencia: el marco, lo de dentro recortado al cliente y
  las piezas de la ultima a la primera, cada una recortada por las hermanas de encima como WS_CLIPSIBLINGS -una transparente ensena a su
  padre y no a las de debajo-, con la «X» roja de la que lance). Una hoja nueva y cada pieza ponen el contexto del hilo de la pantalla
  (InstallIfNeeded), como un control de WinForms. No hay hojas no modales: XP solo abre asi la ventana F12, que va con el panel.
- `Nativo/Rotulo.cs`: `Rotulo` (Label: no toma el foco; TextoGdi con WordBreak, TextBoxControl, EndEllipsis con AutoEllipsis, NoPrefix sin
  UseMnemonic, las nueve alineaciones; el gris de deshabilitado de TextRenderer.DisabledTextColor; 100x23) y `Grupo` (Panel: sin foco,
  sus piezas si; VisibleChanged y Disposed como los usan las paginas de Ctrl+F3; el borde gris de XP; 200x100).
- `Nativo/BotonXp.cs`: `BotonXp` (Button: el clic al soltar el izquierdo encima del mismo boton, Espacio que lo aprieta al bajar y lo pulsa
  al soltar, Intro si es el boton por defecto; PerformClick; al pulsarlo la hoja toma su resultado; pintado plano con los colores de
  ControlPaint -ButtonFlatAdapter- o con el tema Luna, el de los avisos; 75x23), `EstiloBoton` (FlatStyle) y `AspectoPlano`
  (FlatAppearance: solo BorderColor, lo unico que XP le pone).
- `Nativo/ListaXp.cs`: `ListaXp` (el ListBox de Ctrl+F2: renglones del alto de la letra, el elegido en el azul de XP, flechas, Inicio, Fin,
  AvPag y RePag que la lista se queda, la rueda de 3 en 3 que no sube, las barras de 17 puntos con el pulgar que se arrastra, la horizontal
  con HorizontalScrollbar, el borde FixedSingle o Fixed3D; 120x96) y `RenglonesLista` (Items: Clear y Add).
- `Nativo/AvisoXp.cs`: `AvisoXp` (MessageBox, una hoja: las medidas de user32 -Tahoma 8, margenes de 12, 16 entre icono, texto y botones,
  botones de 75x23 en espanol, el texto en 4/5 de la pantalla-, el icono de advertencia, Aceptar como boton por defecto y de cancelar, y la
  «X» o Alt+F4 contestan Aceptar como user32; desde otro hilo se da por aceptado), `BotonesAviso` y `IconoAviso` (solo lo que XP usa: OK;
  Warning o ninguno).
- `Nativo/SelectorCarpetas.cs`: `SelectorCarpetas` (FolderBrowserDialog: el «Buscar carpeta» de XP, nativo, con la primera raiz elegida y
  Aceptar como boton por defecto) y su arbol `ArbolCarpetas` (las raices: /rocola y los discos montados de /proc/mounts, sin los del
  sistema; sin la Papelera ni System Volume Information; teclado y raton como el TreeView de Windows). `ROCOLA_CARPETAS_RAIZ` (pruebas)
  cambia las raices.
- `Nativo/PinturaXp.cs`: lo que pintan todas: la barra de titulo de Luna, la «X» roja (Control.PaintException), la barra de desplazamiento
  de XP, los colores de ControlPaint (HLS de 0 a 240) y lo que GDI+ hace con un pincel, una pluma y un trazo (GraphicsPath: Alternate, los
  arcos unidos), con Pixel, Degradado y TextoGdi de la pista del dibujo.
- `Nativo/PantallaRocola.Teclado.cs`: `CaracterDe`, la tabla del caracter de cada tecla (WM_CHAR), que estaba en la capa
  (Escritorio.Caracter, que ahora la llama).

### Lo que convive con la capa, y cuando se quita

- Las teclas y el raton los sigue leyendo la capa (Escritorio, desde TeclasLinux): con una hoja activa le pasa la tecla (con los
  modificadores de ahora y la repeticion de PantallaRocola.AnotaTecla); con el raton encima de una hoja, con una hoja modal abierta o con
  algo capturado por una hoja, el raton (y a las de la capa les manda antes MouseLeave). La rueda va a la hoja activa. Se va con «Form1 sin
  Form», cuando la pantalla de la rocola reciba la entrada directo.
- `Escritorio.Capas` pone encima de las capas de la capa la superficie de cada hoja (`Capa.Mapa`), que el anfitrion sube a la tarjeta solo
  si cambio (su Version), en el orden de las hojas; con `ROCOLA_CAPTURA_CAPAS=1` la guarda como `capaN-hoja-X,Y-WxH.png`.
- Los ganchos de la hoja a la capa (`Hoja.ActivaDeLaCapa`, `CedeLaCapa`, `QuitaFocoALaCapa`, `ActivaLaDeLaCapa`,
  `ActivaLaDeArribaDeLaCapa`, `VivaEnLaCapa`, `SitioEnLaCapa`, `HayQueComponer`) y de la capa a la hoja (`Escritorio.ActivaForma` desactiva
  la hoja activa, `PonFoco` le quita el foco, `Deshabilitada` cuenta una hoja modal, `FinDeSesion` pregunta primero a las hojas y
  `Form.FinDeSesion` avisa con FormOwnerClosing a las hojas de las que es duena). Se van con la capa.
- La `Form` de la capa cumple `ITeclasAntes` (Ctrl+F3 y el historial de caratulas, en el paso C): sus TeclaAntes y TeclaArribaAntes salen
  despues del KeyDown/KeyUp de WinForms, en el orden en que XP se suscribia (primero el Escape de la hoja, despues la moneda).
- Los ganchos de prueba (`TrazaVentanas.Pieza`, `VolcadoEstado`) nombran las hojas y las piezas nativas con su clase y su texto, igual que a
  las de la capa.

### Diferencias con la referencia, cada una porque XP hace lo nuevo o por una decision

1. «Agregar carpeta...» abre el selector de carpetas nativo (decision del 8-11-2026, diseno del port nativo); la referencia contestaba
   Cancelar sin ensenar nada (su FolderBrowserDialog de mentira). En XP abre el «Buscar carpeta» de Windows. Escena `f2agrega`: igual al
   programa del paso T hasta pulsar Agregar; despues el selector, la carpeta elegida con el teclado y su `dir=` en rocola_config.txt.
2. El apagado (SIGTERM) con Ctrl+F2 abierto: como lo acepto V2 (la referencia no lo atiende). Comparado con el programa del paso T (Ctrl+F2
   aun de la capa): la misma secuencia (closing de la hoja con WindowsShutDown, closing con FormOwnerClosing, closing de la rocola, closed
   de la hoja, el foco, closed de la rocola, dialogo-fin Cancel, pck 1) y `rocola_estado.txt` borrado en los dos.
3. (No es de comportamiento) En las trazas y los estados las hojas y las piezas nativas salen con su clase (Hoja, Rotulo, ListaXp, BotonXp,
   AvisoXp, Grupo): `compara_trazas.py` y `compara_escena.py` llevan la tabla de equivalencias del diseno (Hoja = Form, Rotulo = Label,
   ListaXp = ListBox, BotonXp = Button, AvisoXp = CajaAviso, Grupo = Panel), solo en los sitios donde la traza nombra una pieza, y la capa
   `-hoja-` se compara con la `-dibujo2d-` de la referencia, punto por punto.

Lo que no se hizo porque XP no lo usa: Enter y Leave (ninguna pieza de XP los escucha), las hojas no modales (solo F12), el boton «Crear
nueva carpeta» del selector (XP pone ShowNewFolderButton = false), otros botones e iconos de MessageBox.

### El banco, lo nuevo

- Escenas (`escenas.sh`, no estan en «todas»; datos de `datos_h.sh` en `$DH`, por defecto `/root/rocola-trabajo/port/ventanas2/datos`):
  `f2` (Ctrl+F2 con la musica de siempre, cada paso a un segundo o mas del anterior: la lista con el foco, Abajo, Tab y Mayus+Tab, la
  moneda con la hoja abierta, clics en la lista, en el boton apagado y en Listo, y otra vez abrir y cerrar con Escape, Alt+F4 y la «X»), `f2barras` (21 carpetas de mas: las dos barras, Fin,
  derecha, Inicio, AvPag, la rueda, clics en las flechas y el canal, Quitar una carpeta, los dos avisos de Quitar con Intro y Escape, Listo:
  rocola_config.txt byte a byte), `f2sin` (sin la carpeta de la musica: la hoja se abre sola), `aviso` (la carpeta de datos no se puede
  usar: el aviso de Program.cs antes de la rocola, con su captura), `f2sigterm` y `f2agrega`. `corre.sh`: `CORRIDA` (la carpeta de la
  corrida con el mismo nombre en las dos: Ctrl+F2 ensena la ruta de la musica) y `SIN_DATOS=1` (el programa se copia junto a los datos y
  ROCOLA_DATOS no se puede usar).
- `Pruebas/Ventanas/Hoja` (`guiones/pruebas_hoja.sh`, con `AOT=1` tambien NativeAOT): 48 pruebas con los mismos archivos del programa. Las de
  las 41 que miran ventanas de dialogo y sus controles, contra Hoja y las piezas con las mismas cadenas de WinForms (de la vida de la ventana
  se quitan HandleCreated, Load, Activated, Deactivate y HandleDestroyed, que la hoja no tiene porque XP no los usa); y las nuevas: medidas
  de Ctrl+F2, KeyPreview con y sin Handled, Escape al boton de cancelar, Intro al del foco, Espacio al soltarlo, Tab y Mayus+Tab por un grupo,
  flechas con vuelta, LostFocus/GotFocus, el foco que se va al esconder o apagar la del foco, VisibleChanged y Disposed de un grupo, la
  lista con el teclado y la rueda, Alt+F4 y la «X» de una hoja, los avisos (Alt+F4, Escape, Intro, la «X», el foco de vuelta en la duena)
  y el selector de carpetas (aceptar con el teclado; Escape).
- Las reglas de la pantalla compilan tambien `Nativo/Tecla.cs` (por CaracterDe).
- El ruido de la referencia en las escenas nuevas (3 corridas, RH1-RH3; la secuencia igual en las tres):
  `/root/rocola-trabajo/port/ventanas2/ruido/{f2,f2barras,f2sin,aviso}.json`.

### Comprobaciones (8-11-2026, maquina compartida con otras pistas, carga de 4 a 8, llvmpipe)

- Compila (`dotnet build -c Release`): 0 errores y los mismos 15 avisos de la base, en los mismos archivos y lineas (comparada la lista
  archivo:linea:codigo con la de d94eaed).
- Las 41 pruebas de la capa: 41 de 41 (la capa sigue para Ctrl+F3, las vistas y Form1).
- Las pruebas de la hoja: 48 de 48 en JIT y en NativeAOT (0 avisos de AOT).
- Las reglas de la pantalla (`pruebas_pantalla.sh`, AOT=1): 32 de 32 en JIT y en NativeAOT (los dos CS0649 de siempre del banco).
- Las escenas alternando referencia (RH) y traduccion (H), JIT:
  - RH4/H4: arranque, moneda, video, buscador, cierreq, altf4, kill9 y asistente IGUALES; captura y salirq IGUALES con el ruido de seis
    corridas (`ventanas2/rv/ruido`, como en la revision del paso T); sigterm y f3sigterm, lo aceptado en V2; tab con la carrera conocida del
    repaso (el encargo de Form1.Biblioteca.cs:1735 y «faltan 4 archivos» en la hoja de Ctrl+F3, que es aun de la capa: ver el paso T), y
    otra vez alternando (RH5/H5, RH6/H6) IGUAL.
  - Las nuevas, RH4/H4: f2, f2barras, f2sin y aviso IGUALES con el ruido medido: la secuencia (teclas, KeyDown, caracteres, KeyUp, el raton
    con sus destinos, el foco, la hoja abierta y cerrada, closing, closed, dialogo-fin con su resultado), los estados de XP y de cada pieza,
    las capas de las hojas a 0 puntos distintos (6 de Ctrl+F2 en f2, con el foco en la lista, Agregar, Listo y la lista con eleccion; 10 en
    f2barras, con las barras, la lista corrida, el boton de Quitar con el foco y los dos avisos encima; 2 en f2sin; y en `aviso` -RH7/H7, la
    escena corregida para que la pantalla se componga con el aviso abierto- la del aviso con su icono), rocola_config.txt byte a byte despues
    de Quitar, y `rocola_estado.txt` como XP.
  - f2sigterm contra el programa del paso T (TT4/H4): la misma secuencia y `rocola_estado.txt` borrado en los dos.
- NativeAOT (`dotnet publish -c Release -r linux-x64 -p:PublishAot=true`): 0 avisos de IL ni de AOT (los 15 de compilacion de siempre).
  Contra la referencia (RHA1/HA1, RHA2/HA2): arranque, cierreq, kill9 y f2barras IGUALES; sigterm, lo aceptado; f2, f2sin y aviso con la
  secuencia, los estados, las capas y los archivos iguales y mas relojes del vigia en total (NativeAOT arranca antes y no se atrasa; la
  referencia en JIT iba cargada: 360 frente a 273 vencimientos del reloj de 42 ms en f2), como ya se vio en V3 y en el paso T.
- `diff-xp.py -v` en Form1.Datos.cs, Form1.Ajustes.cs y Program.cs: todas las lineas de este paso con su «EN LINUX»; las que salen sin
  marca son de antes y de otras pistas (el fsync y las prioridades de la pista windows, los comentarios del registro, las TextRenderingHint
  del dibujo, el SetForegroundWindow de la otra rocola).

### Despues de juntar la rama principal (f997e61: los grupos reposo y acabado-estilo del dibujo)

El merge se hizo solo (ningun choque: esos grupos no tocan lo de este paso). Otra vez todo:

- Compila: 0 errores y los mismos 15 avisos (la lista archivo:linea:codigo, igual). Las 41 pruebas de la capa: 41 de 41. Las de la hoja:
  48 de 48 en JIT y en NativeAOT. Las reglas de la pantalla: 32 de 32 en JIT y en NativeAOT.
- Alternando con la referencia en JIT (RH8/H8): arranque, moneda, video, buscador, cierreq, altf4, kill9, asistente, f2barras y f2sin
  IGUALES; captura y salirq IGUALES con el ruido de seis corridas; sigterm y f3sigterm, lo aceptado; tab con la carrera del repaso, esta vez
  en la referencia (RH8 lleva el encargo de Form1.Biblioteca.cs:1735 y H8 no). f2sigterm contra el programa del paso T (TT8): la misma
  secuencia y `rocola_estado.txt` borrado en los dos. f2agrega: la carpeta elegida en el selector queda en rocola_config.txt.
- NativeAOT publicado otra vez: 0 avisos de IL ni de AOT. RHA3/HA3: arranque, cierreq, kill9 y f2barras IGUALES; sigterm, lo aceptado.
- Lo que salio distinto fue de la carga de la maquina (carga de 8 a 9 con otras pistas), no de la traduccion:
  - `f2`: con las teclas a 0,3 s unas de otras, la referencia en JIT recibio un Tab 0,9 s tarde y su estado y sus capturas cayeron antes o
    despues de otra tecla que en la traduccion (en una captura el foco en Listo y en la otra en Agregar; el estado antes o despues de la
    moneda). La escena se separo (cada paso a un segundo o mas del anterior, 34 s), se volvio a medir su ruido (RF1-RF3) y, alternando otra
    vez, IGUAL en JIT (RH13/H13) y en NativeAOT (RHA4/HA4).
  - `aviso` y `f2sin`: la secuencia, los estados, las capas y los archivos iguales; solo las cuentas del reloj de 42 ms y del vigia, con un
    ruido medido en un rato tranquilo. En el aviso, con la maquina tranquila los dos dan 136-140 vencimientos del reloj (RH1-RH3, RH7, H7) y
    con carga la referencia en JIT baja a 95-103 (RH8, RH13) y la traduccion a 109-129; en NativeAOT el primer segundo de la rocola lleva
    14-15 frente a 3-7 (arranca antes, como en V3 y el paso T). Con el ruido de todas las corridas de la referencia (aviso: seis, de 95 a 140;
    f2sin: siete; en `/root/rocola-trabajo/port/ventanas2/ruido6`) salen IGUALES todas: aviso en RH7/H7, RH8/H8, RH13/H13, RHA2/HA2 y
    RHA4/HA4; f2sin en RH4/H4, RH8/H8, RHA1/HA1 y RHA3/HA3.

### Lo que queda

- Paso C: Ctrl+F3 y el historial de caratulas como hojas (MonedaTambienAqui recibira solo hojas; la `Form` de la capa deja de cumplir
  `ITeclasAntes`). Las piezas propias de XP (Tarjeta : Panel; Deslizador, Interruptor y BotonPlano : Control, Form1.Ajustes.cs:379-620)
  no pueden ganar lineas: tendran que heredar de una pieza nativa que les de lo que les daba Control (sitio, letra, colores, foco, Invalidate)
  o cumplir IPieza en sus mismas lineas; se decide en ese paso.
- Con el panel de la tarjeta: la ventana F12 (una hoja no modal FixedSingle centrada en la pantalla, con su ProcessCmdKey) y el aviso de
  D3D9Star.cs:1885. Con «Form1 sin Form»: la entrada directo a la pantalla y a las hojas, sin pasar por Escritorio.
- El selector de carpetas no tiene referencia contra que comparar sus puntos: imita el «Buscar carpeta» de XP a la vista (marco, letra,
  botones y barras de la hoja; el arbol con su cuadrito y la carpeta amarilla). Que en Rocola OS las raices sean /rocola y los discos
  montados sale del diseno; no se ha visto en el A4.
- Nada de esto se ha probado en el A4.

### Riesgos

- El «&» de Label y Button: la referencia lo quita a mano y despues el texto de GDI lo vuelve a tratar como prefijo (un «&&» saldria como
  un subrayado, no como «&»). Se porto igual (la referencia manda); ningun texto de Ctrl+F2 ni de los avisos lleva «&». Mirarlo en el paso C
  con los textos de Ctrl+F3.
- KeyPreview: en XP lo pone una linea (`dlg.KeyPreview = true`); aqui lo pone suscribirse a TeclaAntes/TeclaArribaAntes. Es lo mismo en todo
  lo que XP hace (cada hoja de XP que escucha teclas pone KeyPreview), pero una hoja con KeyPreview sin escuchar nada no se puede expresar.
- El caracter de cada tecla sigue con la tabla de la referencia (un teclado de EE. UU.); la distribucion del A4 (acentos, la ñ) sigue
  siendo un riesgo del diseno.
