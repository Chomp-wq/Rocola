# Port nativo: la ventana de prueba F12 como hoja (grupo f12-como-hoja, oleada 5 y 7, 5c y 7a)

Rama `port-f12-como-hoja` (desde 01fcf68). Es la seccion «7a. f12-como-hoja (W5)» de `PLAN_TANDA4_A_N7.md`: `GpuTestWindow`, la ventana
que XP abre con F12 (`Codigo/D3D9Star.cs`), deja de ser un `Form` de la capa y pasa a ser una hoja de la pantalla nativa, no modal, como
Ctrl+F2 y los avisos (`PORT_VENTANAS2.md`, paso H). Se tomo ese paso como modelo.

## Lo que XP hace (y esta hoja repite)

`Rocola/D3D9Star.cs:1984-2043`. F12 (si el boton no esta configurado) llama a `GpuTestWindow.Toggle()`:

- Con la ventana abierta, `instance.Close()`. Si no, `new GpuTestWindow()` y `Show()`: **no modal**, sin duena, con el foco del teclado y la
  activacion de la ventana nueva.
- El constructor: titulo, `FormBorderStyle.FixedSingle`, `MaximizeBox = false`, `StartPosition.CenterScreen`, `ClientSize` 1024x700, fondo negro.
- `OnShown` (queda en la cola al ensenarse, `BeginInvoke` de `OnLoad`): `new GpuStarfield(this.Handle, ...)` (un dispositivo de Direct3D 9 sobre
  la ventana: presenta su fondo en el cliente) y, si no se crea, `MessageBox.Show(...)` (modal; la duena es la ventana activa, o sea esta) y
  `Close()`. Si se crea, un reloj de 15 ms pinta el cielo y, cada segundo, pone los cuadros por segundo en el titulo.
- `ProcessCmdKey`: Escape y F12 la cierran (con modificadores no: `keyData` los lleva). `OnFormClosed`: para el reloj, suelta el dispositivo
  y borra `instance`.
- El vigia del foco de la rocola (`Form1.ForceFocus`, cada 1,5 s, sin F10) le quita el primer plano a la ventana: la rocola, a pantalla
  completa, se pone encima de ella. Es de XP y se repite igual (ver la escena `f12vigia`).

## Que se hizo

**`Codigo/D3D9Star.cs` (1856-1914, 12 lineas, las mismas 4539 lineas y finales LF de 01fcf68)**: `: Form` -> `: Hoja`; `Show()` -> `Muestra()`;
`FormBorderStyle`, `MaximizeBox` y `StartPosition` pasan a ser comentarios `EN LINUX: (XP) ...` (los da `MarcoXp.Sencillo`);
`OnShown` -> `AlEnsenarse`; `this.Handle` -> `IdVentana`; `MessageBox.Show` -> `AvisoXp.Muestra` (era el ultimo de la capa); `ProcessCmdKey` ->
`TeclaDeMando`; `OnFormClosed` -> `AlCerrada`. Cada linea con su nota. La logica es la de XP.

**`Nativo/Hoja.cs` y `Hoja.Entrada.cs`** (sin imitar a Form: solo lo que XP usa de una ventana no modal):

- `MarcoXp.Sencillo`: FixedSingle sin maximizar, centrada en la pantalla (`CenterToScreen`). Mide igual que el dialogo (barra de 26, borde de 3:
  `WS_CAPTION` lleva `WS_DLGFRAME`, y la `Form.Marco` de la capa da 3 para FixedSingle con icono): 1030x732 con el cliente de 1024x700.
- `Muestra()` (`Form.Show`): ensena sin modal, sin duena y sin bucle propio; una desechada lanza `ObjectDisposedException`, una que ya se ve no hace
  nada. Centra con `CentraEnPantalla` (sin `CenterParent` hay que mirar `clase`).
- `AlEnsenarse()` (`OnShown`, el `Shown` de la cola) y `AlCerrada(razon)` (`OnFormClosed`, despues del aviso «closed» y antes de desecharse; en
  los tres sitios donde la hoja apunta «closed»: cierre normal, dialogo y apagado). De fabrica no hacen nada.
- `TeclaDeMando` (era privada y daba false) pasa a `internal virtual`: la hoja la llama primero con cada tecla (`Hoja.Tecla`, el `ProcessCmdKey`
  de XP).
- La tarjeta en el cliente: `IdVentana` (la vida de la hoja, el `Handle` del dispositivo; no choca con los de la capa, que empiezan en 0x1000),
  `DeLaVentana`, `PresentaTarjeta`, `OlvidaTarjeta`, `ClienteEnPantalla`, `TexturaGl` (se limpia al desecharse).
- `DetrasDeLaCapa`: ver «Orden de las ventanas».

**`Plataforma/` (transicion, se va con la capa)**: `Capa.TarjetaDeHoja` y `Capa.Detras`, y en `Escritorio.Capas()` la capa de la tarjeta justo despues
de la superficie de su hoja (lo que la capa hacia con `f.TexturaGl`); `Anfitrion.Presenta` y `OlvidaTarjeta` le pasan el fondo a la hoja (antes
`Control.FromHandle`), y `Anfitrion.Capas` pone esa capa con las hojas (o antes de todo si la hoja quedo detras). `Escritorio.TeclaDeMando`:
se quito la linea de `GpuTestWindow`.

## Orden de las ventanas (lo que no estaba en el plan y la escena `f12vigia` saco a la luz)

En XP, activar la rocola (`ForceFocus`, un clic en ella, el fin de un dialogo suyo) la sube por encima de las ventanas que no son de nadie: la
ventana F12 queda detras, escondida (la rocola ocupa la pantalla), y sigue viva hasta que F12 la cierra. Las hojas iban siempre encima de la capa
(lo cierto para las modales y las que tienen duena). Ahora `Hoja.DesactivaPorLaCapa()` (la llama `Escritorio.ActivaForma`) marca como
`DetrasDeLaCapa` las hojas visibles sin duena y no modales; esas van antes de todas las capas, no reciben el raton (`TomaElRaton`, `PiezaEn`) y
`Hoja.AlFrente`/`Activate` las suben otra vez. Las modales y las que tienen duena (el aviso de «sin dispositivo», sobre la ventana F12) no se
marcan. Probado en la escena y en las pruebas de la hoja.

## Costuras

| Costura | Donde | Quitada / anadida | Se quita cuando |
|---|---|---|---|
| `GpuTestWindow : Form` y su `MessageBox` | `D3D9Star.cs:1856-1914` | quitada | |
| linea de `GpuTestWindow` en `Escritorio.TeclaDeMando` | `Ventanas.Escritorio.cs:507` | quitada | |
| `Escritorio.TeclaDeMando` (el metodo) | `Ventanas.Escritorio.cs` | **queda**: Form1 la usa por `LlamaProcessCmdKey` | 8a (Form1 sin Form) |
| `Capa.TarjetaDeHoja`, `Capa.Detras`, `capasTarjetaHoja` | `Ventanas.Escritorio.cs` | anadida | N7 (la pantalla pedira las hojas a `Hoja.Abiertas`) |
| rama de hoja en `Presenta`, `OlvidaTarjeta` y `Capas` | `Anfitrion.cs` | anadida | N7 |
| `Hoja.IdVentana` / `DeLaVentana` / `PresentaTarjeta` | `Hoja.cs` | anadida | cuando el dispositivo reciba su textura sin identificador de ventana (W4, el panel de la tarjeta) |
| `GpuStarfield(IntPtr hwnd, ...)` | `D3D9Star.cs:1019` | **queda** (es de otro grupo) | W4 |
| `Plataforma/PruebasLinux.cs:195` | | sin cambio: `GpuStarfield(IntPtr.Zero, ...)` es la prueba fuera de pantalla, sin ventana | W4 |

## Pruebas nuevas

- `Pruebas/Ventanas/Hoja/Program.cs` (`HojaComoF12` imita las sobrescrituras de `GpuTestWindow`): 24 pruebas nuevas, 86 en total (62 antes): medidas
  y sitio (1030x732 en 0,18), Show no modal, Shown despues de Visible y una vez, identificador y cliente en pantalla, Present/soltar, Show
  repetido, otras teclas y Mayus+F12 no cierran, Escape, F12, Close, Alt+F4, la «X», el apagado (consulta y fin, y la consulta impedida),
  Close de la cerrada, Show de la desechada, detras de la capa (sin raton) y Activate, la modal que no se va detras, y el aviso de «sin
  dispositivo» (modal sobre la hoja, ella sigue, Escape lo acepta, vuelve a ser la activa y se cierra).
- Escenas (`Pruebas/Ventanas/guiones/escenas.sh`): `f12` (con F10 antes: la ventana, la activa, se queda con las teclas; abrir y cerrar con
  Escape, F12, Alt+F4 y la «X»; la moneda y Ctrl+F2 no llegan a la rocola), `f12vigia` (sin F10: el vigia de foco), `f12sigterm` (el apagado
  con la ventana abierta) y `f12fallo` (el dispositivo no se crea: `gl_falla.sh` arma una libGLESv2 que reenvia todo a la de Mesa menos
  `glGetShaderiv`, que dice que ningun sombreador compila mientras exista un archivo; asi falla igual donde se quiera).
- `compara_trazas.py` y `compara_escena.py`: los cuadros por segundo del titulo (`[N FPS, M estrellas]`, que dependen de la maquina) y el nombre
  del reloj de la ventana (la linea donde se crea: 1997 en la referencia, 1867 aqui; si 4a mueve esas lineas, cambiar los dos numeros), y las capas
  de Dibujo2D transparentes del todo (la referencia arma un tramo vacio encima de la tarjeta de cada ventana; la hoja no) se dejan de lado y las
  demas se emparejan por orden.

## Comprobaciones (numeros)

- a) `dotnet build -c Release`: 0 errores, 15 avisos (los de la base, ninguno nuevo).
- b) `diff-xp.py D3D9Star.cs`: 414 lineas quitadas y 278 puestas (la base, 402 y 266: las 12 de este grupo), 12 sin marca (las mismas de la base, de
  otros bloques). Lineas y finales de `D3D9Star.cs`: 4539 y LF como en 01fcf68.
- c) NativeAOT (`dotnet publish -r linux-x64 -p:PublishAot=true`): 0 avisos IL/AOT (los 15 de siempre son de analisis de codigo). 59 s.
- e) `pruebas_hoja.sh`: 86 de 86 en JIT y 86 de 86 en NativeAOT (0 lineas con avisos).
- f) `pruebas41.sh` 41 de 41; `AOT=1 pruebas_pantalla.sh` 32 de 32 (JIT y NativeAOT); las 2 lineas de aviso de esa publicacion son CS0649 de
  `PantallaRocola.cs`, no de este grupo.
- Escenas alternadas con la referencia (JIT, la ruido de 3 corridas de la referencia para `f12` y `f12vigia`): `f12` IGUAL (secuencia de 107
  sucesos, estados, capas); `f12vigia` IGUAL (51 sucesos y las capturas con la ventana detras de la rocola); `arranque`, `f2`, `aviso` y
  `f3teclas` IGUAL. (`f2` y `aviso` pasaron la segunda vez: la primera se pasaron de la holgura las cuentas del reloj de la rocola y de
  `MedidasBitacora`, con la maquina cargada.) `f12` con NativeAOT contra la referencia: IGUAL.
- d) Recorridos del programa entero (9 de 9 con 0, con el candado): capas de Dibujo2D quietas 214 de 217 y texturas quietas 476 de 487 (las que faltan
  son las de la hora: buscador, ayuda, video y pared). Las 3 capas «distintas» son de `ctrlf3` en la captura de los 46 s, la de la ventana F12
  abierta (el recorrido la abre a los 44 s): `compara.py` empareja por el numero de capa y la referencia arma, por cada ventana, un tramo de
  Dibujo2D transparente encima de su tarjeta (capa 3) que la hoja no arma, asi que las de la rocola pasan de la 4 y la 6 a la 3 y la 5. Repetido
  con los PNG y apareando por orden: la capa de la ventana F12 (detras de la rocola, por el vigia del foco) sale con 0 puntos de diferencia sin el
  texto del titulo (los cuadros por segundo), y las capas de la rocola de esa captura, transparentes del todo en las dos. `compara.py`
  (Pruebas/Recorridos, que no es de este grupo) aun no deja de lado las capas transparentes como `compara_escena.py`.
- `f12sigterm`: la referencia no atiende SIGTERM como el apagado de Windows (ya aceptado: sigterm y f3sigterm), asi que se mira el resultado, que
  es el de XP: FormClosing de la ventana F12 y de la rocola con `WindowsShutDown` (la de arriba primero), los FormClosed, `rocola_estado.txt`
  borrado; igual en JIT y en NativeAOT.
- `f12fallo`: la referencia no usa el sombreador que se hace fallar (su dispositivo se crea igual) y no se puede comparar; se mira la secuencia:
  ventana, aviso modal (centrado en ella, 342,330), Intro (o Escape) lo acepta, la ventana se cierra (UserClosing) y la rocola vuelve a tener
  el foco; dos veces; igual en JIT y en NativeAOT.

## Lo que queda y riesgos

- `f12vigia` en NativeAOT contra la referencia en JIT: la secuencia difiere en una linea (el KeyUp de F12 llega a la rocola y no a la ventana).
  Es la carrera del vigia del foco (cada 1,5 s desde que arranca la rocola): NativeAOT arranca antes y la fase del vigia cae entre el KeyDown y el
  KeyUp. No hay referencia en NativeAOT para mirar si la tiene tambien; en JIT, las 3 corridas con el codigo final coinciden.
- Con la ventana detras de la rocola, un clic en la pantalla no la alcanza (la rocola la cubre): como en XP.
- El aviso «sin dispositivo» se centra sobre su duena (la ventana F12), como `CajaAviso` de la capa y todos los avisos de la hoja; user32 centra un
  MessageBox en el monitor. Aqui son 3 puntos de diferencia (la ventana mide 1030 en una pantalla de 1024); es de `AvisoXp`, no de este grupo.
- Los botones de minimizar y maximizar (apagado) de un FixedSingle no se pintan: la capa de la referencia tampoco, y la hoja de Ctrl+F2 tampoco.
- No se pudo comparar «el dispositivo no se crea» con la referencia, por lo de arriba.
- No probado en el A4.
