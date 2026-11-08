# Port nativo: las ventanas

Pista de las ventanas del port nativo de la rocola de XP a Rocola OS (decisiones del 8-11-2026 en AGENTS.md, «Diseno del port
nativo»): lo que en XP hace WinForms (el bucle de mensajes, los temporizadores, los encargos de otros hilos, el foco, las teclas, las
hojas) pasa a piezas nativas propias que el codigo de XP llama directo, sin clases que imiten a Form, Control ni Timer. Se compara
siempre contra la copia congelada de la referencia (`/root/rocola-trabajo/referencia-xp`, aef2211), que nunca se toca. Base de la
rama: 34cb741. Nada de esto se ha probado en el A4.

## V1. El banco

Lo que llega al codigo de XP desde las ventanas, apuntado en el sitio por donde pasa (hoy la capa de `Plataforma/Ventanas.*.cs`;
despues la pantalla y la hoja nativas), igual en el programa y en una copia de trabajo de la referencia:

- `ROCOLA_TRAZA_VENTANAS=ruta` (`Plataforma/TrazaVentanas.cs`): cada tecla con su keyData, si es la repeticion (el bit 30 de
  WM_KEYDOWN) y lo que devolvio ProcessCmdKey; cada KeyDown (con Handled y SuppressKeyPress), cada caracter a KeyPress (en la
  rocola, Form_KeyPress por KeyPreview), cada KeyUp; cada Tick con el sitio donde se creo su reloj (archivo:linea) y su intervalo;
  cada encargo (BeginInvoke) con el sitio de la llamada, y los que se pierden porque su ventana ya no esta; cada aviso del raton con
  su destino; cada cambio de foco; cada ventana que se ensena o se esconde (con su DialogResult), el fin de cada ShowDialog; Shown,
  FormClosing y FormClosed con la razon del cierre.
- `ROCOLA_ESTADO_EN=S:ruta,...` (`Plataforma/VolcadoEstado.cs`): a los S segundos, en el hilo de la pantalla y sin reflexion, el
  estado de XP (creditos, la cola, lo que suena, la letra, la pagina, el disco y la cancion elegidos, el buscador y lo escrito, el
  asistente de los botones, la atraccion, el mantenimiento, la pestana de Ctrl+F3, el foco y la ventana activa) y cada pieza de cada
  hoja abierta (clase, texto, sitio, si se ve, si esta activa, su orden del tabulador, Checked o Value, y la del foco).
- La referencia con los mismos ganchos solo en su Plataforma: `Pruebas/Ventanas/ganchos-referencia.patch` y `guiones/referencia.sh`.
- Las 41 pruebas de WinForms, los guiones de las escenas y la herramienta de Roslyn del inventario, en `Pruebas/` (ver
  `Pruebas/Ventanas/LEEME.md`).

Comprobado (8-11-2026, maquina de pruebas compartida con otras pistas, llvmpipe, JIT):

- Las 41 pruebas: 41 de 41 con la referencia congelada, con la referencia con ganchos y con este programa; la salida de las pruebas
  de la congelada y la de la de ganchos es la misma linea por linea.
- Los ganchos no cambian lo que se ve: en las escenas moneda (Ctrl+F3 en Sonido y en Imagen) y tab (45 Tab, 12 Mayus+Tab, la pestana
  Fondo y 25 Tab), las capas de Dibujo2D de las capturas (la hoja de Ctrl+F3 de 966x764 y las de la rocola) salen iguales pixel a
  pixel entre la congelada sin ganchos y la de ganchos (0 pixeles distintos en las 15 capas). La capa de la tarjeta y la pantalla
  entera no se pueden comparar asi: el cielo se mueve solo entre dos corridas.
- El ruido de la referencia contra si misma (3 corridas de las 10 escenas): la SECUENCIA (teclas, ProcessCmdKey, KeyDown, caracteres,
  KeyUp, raton, foco, ventanas, cierres) sale igual en las 3 en las 10 escenas, una vez sacada de ella la repeticion de una tecla
  mantenida (la manda otro hilo cada 33 ms: donde cae entre las demas teclas depende de la hora; se cuenta aparte). Las cuentas se
  mueven: relojes por segundo hasta 4-6 (moneda, tab, sigterm, kill9) y hasta 10-24 en las escenas que arrancan o cierran con
  carga (arranque, video, buscador, cierreq, altf4); encargos de otros hilos por segundo hasta 4-29 y en total hasta 10-75 (casi
  todos son el vigia de la pantalla, MedidasBitacora.cs:129, unas 30 por segundo). Con carga en la maquina las teclas de prueba
  llegan tarde (en dos corridas de cierreq las Q llegaron 600 ms tarde), por eso cuentan las secuencias y no los milisegundos.
  Los estados de XP (ROCOLA_ESTADO_EN) salen iguales en las tres.
- La referencia y el apagado: tras Ctrl+Alt+Q x3 y tras Alt+F4 falta `rocola_estado.txt` (UserClosing lo borra, como XP); tras
  SIGTERM y tras SIGKILL queda: la referencia no atiende SIGTERM (lo cambia V2, regla del 8-11-2026).

Herramientas: `Pruebas/Ventanas/guiones/` (datos.sh, referencia.sh, escenas.sh, corre.sh, ruido.sh, compara.sh, compara_trazas.py,
compara_escena.py, pruebas41.sh, teclas_tab.py) y `Pruebas/Inventario/` (Roslyn: 0 errores de compilacion, 4 578 referencias a
WinForms, como el inventario del diseno).

## V2. La pantalla y los relojes nativos, debajo de la capa

Piezas nuevas en `Nativo/` (el codigo de XP aun no cambia: la capa de `Plataforma/Ventanas.*.cs` les pasa todo):

- `PantallaRocola.cs`, el bucle de la rocola en su hilo, con el orden de GetMessage que XP supone: cada vuelta UNA cosa, la primera
  de: un encargo (BeginInvoke) en orden, una entrada (tecla o raton de evdev) en orden, el pintado de lo sucio (WM_PAINT), un reloj
  vencido (WM_TIMER); con la cola vacia, Libre (Application.Idle) una vez y a esperar con el AutoResetEvent hasta que llegue algo,
  venza un reloj o toque pintar. `Encola`/`EncolaDesde` (con la vida de su dueno: sin ventana lanza; si su ventana cambio, el encargo
  se pierde sin ThreadException), `Entrada`, `Despierta`, `EnSuHilo` (lo contrario de InvokeRequired), `Bombea` (DoEvents: todo sin
  Libre, y solo en su hilo), `HayPendientes` (PeekMessage), `EsperaPendientes` (MsgWaitForMultipleObjectsEx con MWMO_INPUTAVAILABLE),
  `Corre` y `CorreModal` (Application.Run y el bucle anidado de ShowDialog), `Sal`, `AlFallar`/`Falla` (ThreadException) y el apagado:
  SIGTERM (PosixSignalRegistration, sin cortar el programa) encola «Apagado» al hilo de la pantalla, que llama a `AlApagar`.
  Se llama PantallaRocola y no Pantalla porque `Rocola.Pantalla` es el espacio de nombres de Consola/Pantalla (Gl.cs, Kms.cs, que no
  se tocan): desde el codigo de XP, en el espacio Rocola, `Pantalla.X` seria siempre ese espacio de nombres.
- `Reloj.cs`, el temporizador con las reglas de WM_TIMER y de Timer.cs: plazo fijo desde el vencimiento anterior, los atrasados salen
  una vez, minimo 10 ms, 100 ms de fabrica, menos de 1 lanza, cambiar Interval en marcha lo reinicia (el mismo valor no), Tick con su
  remitente.
- Cada regla cita su linea de la fuente de WinForms (`/tmp/claude-0/agente-winforms/ref`: Application.cs, Control.cs,
  NativeWindow.cs, Form.cs, Timer.cs) en la cabecera de los dos archivos.

En la capa: `Escritorio` (AhoraMs, HiloUi, la cola -Pendiente envuelve el encargo nativo-, la entrada, HayMensajes, EsperaMensajes,
Bombea) llama a PantallaRocola y le da el pintado (PintaUno, HaySucio, ProximoPintado: el panel de la tarjeta y la composicion de las
hojas); `Application` (Run, Idle, DoEvents, Exit, ThreadException) y `Timer` (envuelve un Reloj, con el Timer de remitente) tambien.
Para el apagado, `Escritorio.FinDeSesion` y `Form.FinDeSesion` hacen lo de WinForms con WM_QUERYENDSESSION y WM_ENDSESSION
(Form.cs:7208-7211 y WmClose, 6638-6790): de arriba abajo, FormClosing con CloseReason.WindowsShutDown (un dialogo por
CheckCloseDialog; uno que no lo es, antes a las suyas con FormOwnerClosing) y despues FormClosed y Dispose.

Diferencias con la referencia, cada una porque XP (WinForms) hace lo nuevo:

1. SIGTERM es el apagado de Windows (regla del 8-11-2026): la rocola se cierra con WindowsShutDown y XP borra su estado; la
   referencia no atendia SIGTERM y el estado quedaba.
2. Lo que lanza un manejador de Libre sale de Corre (FDoIdle sin try, Application.cs:3869-3873; RunMessageLoopInner solo con
   try/finally, 3348-3420); de un modal sube a la tecla que lo abrio, que lo da a ThreadException. La referencia lo apuntaba y seguia.
3. Lo que lanza un encargo llega a ThreadException como t.GetBaseException() (Control.cs:6800-6823).
4. Poner el mismo Interval con el reloj en marcha no lo reinicia (Timer.cs:153); la referencia lo reiniciaba.
5. DoEvents desde otro hilo no atiende la cola de la pantalla (vacia la del hilo que lo llama, ThreadContext.FromCurrent,
   Application.cs:958-961). La referencia la atendia en ese hilo: la medida de la duracion de los videos del repaso
   (Form1.Biblioteca.cs:1318) podia correr relojes, teclas y encargos de la rocola fuera de su hilo.
6. EsperaPendientes espera su plazo aunque quede una senal de antes (MsgWaitForMultipleObjectsEx vuelve solo si hay algo en la cola);
   la referencia volvia antes de tiempo y el bucle de la tarjeta daba una vuelta de mas.

Comprobado (8-11-2026, JIT, llvmpipe, con la maquina compartida):

- Las reglas una por una (`Pruebas/Ventanas/Pantalla`, con los mismos archivos del programa): 27 de 27 (orden de una vuelta, Libre
  una vez por vaciado, Bombea sin Libre y no desde otro hilo, relojes: fabrica, Interval 0, minimo de 10 ms, vencimientos juntados y
  el siguiente en su sitio, mismo Interval no reinicia, otro si, remitente; excepciones de encargo, Invoke, tecla, reloj, pintado,
  Libre en Corre y en un modal; encargo sin ventana y de una ventana destruida; HayPendientes y la espera; el modal; SIGTERM).
- Las 41 pruebas de WinForms sobre la capa (que ya llama a la pantalla nativa): 41 de 41.
- Las 10 escenas, alternando referencia y traduccion para que las dos corran con la misma carga, dos veces (R4/N2 y, con el
  programa final, R5/N3): arranque, moneda, video, tab, buscador, cierreq, altf4 y kill9 IGUALES (secuencia igual; relojes,
  encargos y repeticiones dentro del ruido de 5 corridas de la referencia; estados de XP iguales; las capas de las hojas igual pixel a
  pixel); sigterm y f3sigterm distintos solo en lo que debia cambiar: «encargo Apagado», FormClosing y FormClosed con WindowsShutDown
  (con Ctrl+F3 abierto: la hoja, FormOwnerClosing a la hoja, la rocola; el ShowDialog acaba con Cancel) y `rocola_estado.txt`
  borrado. Tres escenas pedian el ultimo estado en el mismo instante que la ultima captura, que cierra el programa, y a veces no
  llegaba (en la referencia tambien): ahora lo piden antes (arranque 8,5 s, moneda 17,5 s, video 15,5 s; R6/N4: IGUALES).
- NativeAOT (`dotnet publish -c Release -r linux-x64`): 0 avisos de IL ni de AOT (quedan los 15 avisos de compilacion del codigo de
  XP, los mismos de la referencia: CA2014, CA2022, CS1690, SYSLIB0050, SYSLIB0054). Las reglas una por una: 27 de 27 tambien en
  NativeAOT. Las escenas arranque, moneda, cierreq, altf4, sigterm, f3sigterm y kill9 con el ejecutable nativo (A1) contra la
  referencia: como en JIT; la unica diferencia de pixeles (544, en la hoja de Ctrl+F3) es la fecha del programa en su titulo (con
  JIT es la de dotnet; con NativeAOT, la del ejecutable): la caja de la diferencia es 141x11 en (316, 11), la barra de titulo.
- `rocola_estado.txt`: falta tras Ctrl+Alt+Q x3, tras Alt+F4 y tras SIGTERM; queda tras SIGKILL (el apagon); en JIT y en NativeAOT.

Pendiente de V2, fuera del programa: que el apagado de Rocola OS de tiempo a la rocola. El init de BusyBox manda SIGTERM a todo y
SIGKILL un segundo despues, y `S99rocola stop` solo pone el WOL: el cierre de XP (GuardaTodoAlCerrar, BorraEstado,
Bitacora.Vacia(3000); Form1.Video.cs:1877-1905) puede quedarse a medias. Hay que mandarle SIGTERM desde `S99rocola stop` (o el
`::shutdown` de inittab) y esperar a que salga. No se toco (es de la imagen del sistema, no de esta pista).

## V3. Lo de ventanas que no depende de la herencia, en el codigo de XP

Cada linea cambia en su sitio, sin anadir ni quitar lineas (las trazas nombran los relojes y los encargos por archivo:linea, y asi
salen igual que en la referencia), con su «EN LINUX». Guion: `Pruebas/Ventanas/guiones/traduce_v3.py CARPETA_CODIGO relojes|encargos` (la lista de
lineas, lo que tenian y lo que tienen; avisa si una no esta como se esperaba; ya aplicado: es el registro de lo que se cambio).

1. Program.cs y los relojes:
   - `PantallaRocola.AlFallar = delegate (Exception ex) { LogCrash("ThreadException", ex); };` en vez de Application.ThreadException;
     `PantallaRocola.Corre(new Form1());` en vez de Application.Run (`Nativo/PantallaRocola.Rocola.cs`, aparte porque es lo unico de
     la pantalla que conoce a la rocola: mientras Form1 herede la ventana de la capa, la capa la ensena, avisa al cerrarse y la cierra
     en el apagado). Quedan SetUnhandledExceptionMode, EnableVisualStyles y SetCompatibleTextRenderingDefault: en la capa no hacen
     nada (el primero es ademas el gancho de `--prueba-video`, PruebasLinux.DespuesDelInicio); se van con la capa.
   - Los 11 relojes de XP pasan de Timer a Reloj (declaracion y `new`): Form1.cs (los 7 del constructor, focusTimer y `anterior` de
     DetieneVigiaFoco), Form1.Biblioteca.cs, Form1.Datos.cs, Form1.Portadas.cs, Form1.Video.cs, SalidaSonido.cs (reintentoSalida),
     Form1.Ajustes.cs (tTrain de Ctrl+F3) y D3D9Star.cs (el de la ventana F12). Ningun manejador de Tick usa el remitente.
   - Comprobado: compila (los 15 avisos de siempre), 41 de 41, y las 10 escenas alternando con la referencia (R7/N5): arranque,
     video, buscador, cierreq, altf4 y kill9 IGUALES; sigterm y f3sigterm, el apagado como en V2. Dos diferencias que no son de
     este cambio: en moneda la repeticion de la moneda mantenida llego despues de soltarla y conto como otra moneda (8 creditos en vez
     de 4: un fallo de TeclasLinux.cs que la referencia tambien tiene; se arregla abajo) y en tab el estado de los 22 s se cruzo con
     la ultima captura (como en V2).

   - La repeticion despues de soltar (TeclasLinux.cs, lo nativo de la entrada): el hilo que repite la tecla mantenida comprobaba
     que seguia apretada y la entregaba fuera del candado; si se soltaba justo entre las dos cosas, la repeticion llegaba detras del
     soltar y la capa la tomaba por una pulsacion nueva (en la escena moneda, otra moneda: 8 creditos en vez de 4). En Windows una
     repeticion nunca llega despues de WM_KEYUP. Ahora la pulsacion, el soltar y cada repeticion se entregan dentro del mismo candado
     (en la lectura de evdev, en la repeticion y en las teclas de prueba): una repeticion va antes del soltar o ya no sale. Es un
     cruce raro (una vez en unas 30 corridas con teclas mantenidas; 30 pulsaciones de 503 a 706 ms seguidas no lo dieron ni en la
     referencia ni aqui), arreglado por construccion. moneda, cierreq y tab otra vez alternando (R8/N6): IGUALES. La escena tab pide
     su ultimo estado a los 21,5 s.
