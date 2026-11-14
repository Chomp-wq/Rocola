# Integración en Rocola OS: `--version` y el cierre que conserva el estado (grupo integra-version-y-cierre)

14-11-2026. Rama `port-integra-version-y-cierre`, sobre 7d3d3a7 (el port nativo terminado, N7 hecho). Son los dos pendientes del
programa traducido que dejó `sistema/INTEGRACION_ROCOLA_XP.md`. **No se activó nada**: el paquete y el interruptor siguen apagados y el
sistema de fábrica abre `rocola-consola` igual que antes. Nada se probó en el A4 ni con el sistema entero.

## Qué se hizo

### a) `rocola-xp --version`

- `Nativo/VersionRocolaOs.cs` (nuevo): al cargarse el programa (`[ModuleInitializer]`, como `Plataforma/PruebasLinux.cs` y
  `PruebaAot.cs`), si el primer argumento es `--version`, escribe UNA línea
  `rocola-xp programa N (COMMIT) compilado SEGUNDOS` y acaba con 0. Es **antes de entrar al `Main` de XP**: antes de la instancia única
  (el mutex), de `Form1.CarpetaDatos` (no mira ni crea la carpeta de datos) y de la pantalla y el sonido. `Codigo/Program.cs` no se tocó.
- `RocolaXp.csproj`: las mismas propiedades que `Consola.csproj` (`RocolaVersion` 0 y `RocolaCommit` `local` si no se dan;
  `InformationalVersion` = `N+COMMIT`, sin la revisión de Git; `RocolaFecha` = segundos UTC de la compilación, como
  `AssemblyMetadata`).
- El formato es el que leen `/usr/bin/rocola` (`s/.* compilado \([0-9]*\).*/\1/`), `rocola-actualiza` (ese, `s/.*programa \([0-9]*\).*/\1/`
  y `grep -E "programa N( |$)"`) y `rocola-buzon` (el del número), con «rocola-xp» en lugar de «rocola-consola». Los tres guiones no
  se tocaron en esto y hoy solo se lo preguntan a `rocola-consola`.

### b) El cierre ordenado que conserva el estado (SIGUSR1)

Regla del dueño (8-11-2026): el apagado (SIGTERM = `WindowsShutDown`) y Alt+F4 / Ctrl+Alt+Q (`UserClosing`) borran
`rocola_estado.txt`; los reinicios propios de Rocola OS van por la vía de XP que lo conserva, «cierre por otra causa».

- **La vía de XP elegida: `CloseReason.TaskManagerClosing`.** En XP es un `WM_CLOSE` que le llega de fuera a la ventana de la rocola
  (lo que mandan el Administrador de tareas con «Finalizar tarea» y `taskkill` sin `/F`): `Form.WndProc` le pone
  `TaskManagerClosing` si no había razón (Form.cs:7200-7206 de la fuente de referencia) y `WmClose` la cierra (6638-6783). Es lo más
  parecido a lo que hace Rocola OS (otro programa le pide a la rocola que se cierre). `OnFormClosing` de la rocola
  (`Codigo/Form1.Video.cs:1877`, **sin tocar**) hace lo de siempre al cerrar (para el vigía, las lecturas, `DisposePlaybackNow`,
  `SueltaLookahead`, `GuardaTodoAlCerrar`) y, como la razón no es `WindowsShutDown` ni `UserClosing`, no llama a `BorraEstado` sino a
  `WaitDataWrites(15000)`; `OnFormClosed` (`Form1.cs:730`) guarda estadísticas y la noche y espera 5 s más; `Dispose` suelta la
  tarjeta. El programa sale de `PantallaRocola.Corre` como de `Application.Run`, vacía la bitácora, suelta el mutex y acaba con 0.
- `Nativo/Pieza.cs`: `RazonCierre` gana `TaskManagerClosing = 2` (el mismo número y nombre que `CloseReason`; lo escriben las trazas).
- `Nativo/PantallaRocola.cs`: `EscuchaElCierreDeFuera` (al lado de `EscuchaElApagado`, en `Corre`): `PosixSignalRegistration` de la
  señal 10 (SIGUSR1 en Linux x86-64), `Cancel = true` y un encargo «CierreDeFuera» en el hilo de la pantalla que llama al gancho
  `AlCerrarDeFuera` (lo pone quien corre la rocola, como `AlApagar`).
- `Nativo/PantallaRocola.Rocola.cs`: `AlCerrarDeFuera = CierreDeFuera`.
- `Nativo/PantallaRocola.Ventana.cs`:
  - `CierreDeFuera()` y `VentanaNativa.CierraDeFuera()`: `WmClose` de una ventana que no es modal, con sus hojas: el aviso de que se
    cierra a las hojas de las que es dueña (de la última a la primera, `FormOwnerClosing`; `Hoja.LaDuenaSeCierra`, la misma función
    del apagado), a ella siempre, y si nadie lo impidió, el de que se cerraron, y `Dispose`. Llega aunque la rocola esté debajo de una
    hoja modal (un mensaje puesto en la cola le llega también a una ventana deshabilitada).
  - `DesechaLoSuyo`: antes de lo demás, las hojas de las que la rocola es dueña se desechan, de la última a la primera
    (`Form.Dispose`, Form.cs:3632-3642). Hasta ahora nunca había hojas abiertas en ese punto (con Alt+F4 el diálogo se cierra él; en el
    apagado cada hoja ya se desechó con su `WM_ENDSESSION`): para esos caminos no hace nada (comprobado: las escenas de cierre salen
    iguales que con el programa de antes). Con ellas desechadas, el bucle del diálogo acaba (`dialogo-fin ... None`) y lo que XP hace
    después de `ShowDialog` corre con la rocola ya cerrada, como en el apagado con Ctrl+F3 abierto.
- **Antes de `Corre`** (el arranque: construir `Form1`, el aviso de la carpeta de datos) la señal no se escucha todavía: SIGUSR1 corta
  el programa como antes (sale con 138), que es como un apagón y también conserva el estado. Igual que SIGTERM, que tampoco se escucha
  antes.

### El lanzador (`sistema/board/rocola/rootfs_overlay/usr/bin/rocola`)

Solo `cierra_xp` (donde manda SIGUSR1) y un comentario del bucle. La lógica no hizo falta cambiarla: quien llama a `cierra_xp` deja
antes su marca (`rocola-en-consola`, `rocola-xp-reinicio`) y el bucle la mira **antes** que el código de salida, así que ni el 0 (cierre
ordenado) ni el 138 (cortada al arrancar) cuentan como caída, y los dos quedan apuntados en la bitácora («La rocola se cerró para la
consola de mantenimiento», «rocola-xp se cerró para reiniciarse»). Añadido: si a los 20 s no se cerró y hay que cortarla con SIGKILL,
queda apuntado («rocola-xp no se cerró en 20 s: se cortó (el estado queda como tras un apagón)»). `rocola-buzon` no se tocó (su trozo
ya manda SIGUSR1 y espera 15 s).

### Pruebas nuevas

- `Pruebas/Ventanas/guiones/cierre_rocola_os.sh PROGRAMA [ETIQUETA]` (JIT o `aot:CARPETA`; `W`, `DATOS0`, `SAL` como `corre.sh`): las
  escenas `version`, `sigusr1`, `f3sigusr1`, `f2sigusr1` y `videosigusr1`. Manda la señal al programa mismo (no por `timeout`) y mira
  su código de salida de verdad.
- `sistema/rocola/Pruebas/Lanzador`: `falso.c` atiende SIGUSR1 como el programa de verdad (sale con 0; con `/tmp/falso.usr1` =
  `muere` lo corta, 138; `sordo` no se cierra) y `dentro.sh` gana el caso `cierre` (`prueba.sh cierre`; va también en «todas»).

## Costuras

Ninguna quitada ni añadida (no quedan capas: N7). Lo nuevo es nativo y con nombres propios; `RazonCierre.TaskManagerClosing` es un
valor más de un enumerado que ya existía.

## Comprobaciones (máquina de pruebas, fuera de pantalla con llvmpipe; nada en el A4)

- **a) `dotnet build -c Release`**: 0 errores, **13 avisos, los mismos 13 de la base** (comparados archivo y código de aviso:
  SYSLIB0054 ×9, CA2022 ×2, SYSLIB0050 y CA2014: los de 7d3d3a7 compilado aquí mismo).
- **c) NativeAOT** (`dotnet publish -c Release -r linux-x64 -p:PublishAot=true`): 0 errores, **0 avisos IL/AOT**, los mismos 13 del
  compilador.
- **b) `Codigo/`**: no se tocó ningún archivo. Los 109 con sus líneas y sus CRLF iguales que en 7d3d3a7 (contados uno por uno);
  `diff-xp.py` da lo mismo que en la base porque son los mismos bytes.
- **`--version`**: JIT `rocola-xp programa 0 (local) compilado 1794629693`, sale con 0 en 60 ms; NativeAOT lo mismo en 13 ms. Con
  `-p:RocolaVersion=57 -p:RocolaCommit=abc1234 -p:RocolaFecha=1762500000`: `rocola-xp programa 57 (abc1234) compilado 1762500000`.
  Una línea, nada por la salida de errores, las tres expresiones de los guiones leen lo suyo, no crea la carpeta de datos, no deja traza
  (no abre la pantalla) y con otra rocola abierta en los mismos datos contesta igual y la abierta sigue.
- **`cierre_rocola_os.sh`**: **36 de 36 en JIT y 36 de 36 en NativeAOT**. En las cuatro escenas de SIGUSR1: sale con 0 (30 a 90 ms
  desde la señal), `closing` y `closed` de `Form1'Rocola'` con `TaskManagerClosing` y con ninguna otra razón, `rocola_estado.txt`
  conservado con los créditos de antes de cerrar, al abrir otra vez con esos datos los mismos créditos, y entonces SIGTERM cierra con
  `WindowsShutDown`, sale con 0 y lo borra. Con Ctrl+F3 y Ctrl+F2: `closing Hoja FormOwnerClosing`, `closing Form1
  TaskManagerClosing`, `closed Hoja FormOwnerClosing`, `closed Form1 TaskManagerClosing`, `dialogo-fin Hoja None`. Con un video
  sonando: el estado lleva `sonando=V|...`. `rocola_error.txt` sin apuntes nuevos en ninguna.
- **Las escenas de cierre de siempre, contra el programa de antes del cambio** (7d3d3a7 compilado aquí, alternadas, `compara.sh` con
  `RUIDO=.../ventanas2/ruido`): `sigterm`, `f3sigterm`, `altf4`, `cierreq`, `salirq`, `kill9`: **IGUAL** las seis. `f2sigterm` y
  `f12sigterm`: la misma secuencia de sucesos y `rocola_estado.txt` borrado en los dos; DIFERENTE solo en las cuentas de relojes y
  encargos del repaso (no hay ruido medido para esas dos escenas: holgura 2; la máquina iba con carga 6 en 4 núcleos; repetidas una
  vez, lo mismo: 214 frente a 221 ticks en total, etc.).
- **Contra la referencia congelada**: `cierreq`, `altf4`, `kill9`, `arranque`, `moneda`: IGUAL (las dos últimas con sus capturas).
  `sigterm` y `f3sigterm`: la diferencia aceptada (la referencia no atiende el apagado; aquí `rocola_estado.txt` se borra). `salirq`:
  dos veces una cuenta de reloj fuera de holgura (248 frente a 243 y 245 frente a 251, holgura 4: en los dos sentidos) y a la tercera
  IGUAL; contra el programa de antes, IGUAL.
- **NativeAOT contra JIT** (`escenas.sh aot:`): `sigterm`, `f3sigterm`, `altf4`, `kill9`, `cierreq`: IGUAL.
- **`pruebas_hoja.sh`: 86 de 86. `AOT=1 pruebas_pantalla.sh`: 32 de 32** en JIT y NativeAOT (sus 2 avisos CS0649 de siempre: el gancho
  nuevo lleva `= null` para no añadir un tercero). Las 41 pruebas contra WinForms ya no existen (se borraron con la capa en N7).
- **El lanzador** (`prueba.sh`, BusyBox del sistema en su raíz y su espacio de procesos): `fabrica` **IGUAL** (log, registro y
  bitácora, contra el lanzador de 7113804); `xp`: la consola y «reiniciar ya» cierran con SIGUSR1 y el programa falso sale con 0, el
  botón de encendido con SIGTERM; `caidas`, `datosxp`, `sinprograma`, `otracarpeta`, `buzon`: como antes; `cierre` (nuevo): cuatro
  cierres seguidos ordenados (0) → 5 arranques de rocola-xp, ninguna caída, cuatro apuntes en la bitácora; lo mismo cortada (138); la
  que no se cierra, cortada a los 20 s, apuntada y abierta otra al volver. `sh -n` del lanzador con el BusyBox del sistema (lo hace
  `prueba.sh`), `bash -n` de `prueba.sh` y de `cierre_rocola_os.sh`.
- **No corridos**: los recorridos del programa entero, los arneses de vistas, el banco y las cintas (nada de lo tocado pinta ni sube
  nada a la tarjeta; `arranque` y `moneda` con capturas salieron IGUAL que la referencia).

## Lo que queda

- Que el lanzador use `"$XPBIN" --version` (hoy apunta el archivo `/usr/lib/rocola-xp/version`, que escriben el paquete y
  `rapida.sh` con «programa 0 (sistema)») y que la compilación del sistema dé `-p:RocolaVersion`/`-p:RocolaCommit`: hace falta para que
  `rocola-xp` se actualice solo por el canal del programa (INTEGRACION_ROCOLA_XP.md, apartado 3). No era de este grupo.
- `Pruebas/Ventanas/LEEME.md` no nombra el guion nuevo (no era de este grupo).
- Probar SIGUSR1 con el sistema entero (QEMU) y en el A4, con música sonando por una tarjeta de verdad.
- Lo demás de INTEGRACION_ROCOLA_XP.md: activar (lo pide el dueño), el importador de las tablas, la fase 11.

## Riesgos

- **El cierre de XP puede tardar más que la espera del lanzador.** `OnFormClosing` espera hasta 15 s a las escrituras
  (`WaitDataWrites(15000)`) y `OnFormClosed` 5 s más; el lanzador corta a los 20 s y el buzón a los 15 s. Aquí tardó 30 a 90 ms; con un
  disco lento o atascado podría llegar el SIGKILL a medio guardar (el estado quedaría como tras un apagón, y queda apuntado).
- **Si una hoja impidiera el cierre** (`AlCerrarse` devolviendo true con `FormOwnerClosing`), la rocola no se cerraría (como en
  WinForms) y el lanzador la cortaría a los 20 s. Ninguna hoja de XP lo impide (solo `AvisoXp` lo sobrescribe, y devuelve false).
- **La razón se queda puesta, como en WinForms** (`if (razon == None) razon = TaskManagerClosing`): si antes hubo un apagado impedido
  por una hoja (la razón quedaría en `WindowsShutDown`), un SIGUSR1 posterior cerraría con esa razón y borraría el estado. Hoy no puede
  pasar (nadie impide un cierre).
- **Las hojas sin dueña** (la ventana de prueba F12) no reciben aviso con SIGUSR1: en XP el `WM_CLOSE` va solo a la ventana de la
  rocola; el programa acaba al salir de `Corre`, como al salir de `Application.Run`. No probado con F12 abierta.
- El número de SIGUSR1 (10) es el de Linux en x86-64 (el A4 y QEMU); en otra arquitectura sería otro.
- Lo que XP hace después de `ShowDialog` (Ctrl+F3, Ctrl+F2) corre con la rocola ya cerrada, igual que en el apagado con la hoja
  abierta (`f3sigterm`); no dejó apuntes en `rocola_error.txt`.
- `--version` va en un `ModuleInitializer`: cualquier programa de prueba que compile `Nativo/**` y reciba `--version` como primer
  argumento contestaría la línea y saldría (ninguno lo hace).
- `RocolaFecha` cambia en cada compilación: dos compilaciones del mismo código ya no dan el mismo `rocola-xp.dll` byte a byte (como
  `rocola-consola`).
