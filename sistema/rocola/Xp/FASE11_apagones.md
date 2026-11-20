# Fase 11: la auditoría del modo contra apagones

20-11-2026. Rama local `apagones`. Qué guarda la rocola de XP para reanudar tras un corte de luz, cuándo lo descarta, y si rocola-xp
(con el sistema de Rocola OS alrededor) hace exactamente lo mismo. La regla es la aclaración del dueño del 8-11-2026 en `AGENTS.md`:
**el apagado a propósito no conserva nada; solo un apagón conserva el estado**, y los reinicios propios de Rocola OS van por la vía de XP
que conserva («cierre por otra causa»). **Nada de esto se probó en el A4**: fuera de pantalla en esta máquina y en QEMU (abajo).

Resultado en una línea: el programa hace lo de XP en todas las vías (apagón, caída, apagado, reinicios de Rocola OS, el lanzador, el
importador, Spotify), y **había un fallo de Linux**: el cambio de nombre con el que XP publica el estado no llegaba al disco hasta el
siguiente asiento del diario de ext4 (hasta 5 s), y un corte de luz en ese rato devolvía el estado de **antes** (las monedas recién echadas
se perdían). Arreglado en el sistema, sin tocar el código de XP: `/rocola/datos` se monta con `dirsync` (`board/rocola/initramfs/init`).

## 1. Lo que hace XP (Rocola/, igual en Codigo/)

**Qué guarda** — `rocola_estado.txt` en la carpeta de datos (`Form1.Datos.cs`, «ESTADO VIVO»), con CRLF:

| Línea | Qué es |
|---|---|
| `ROCOLA-ESTADO v1` | la cabecera (sin ella se lee la copia `.bak`; sin ninguna buena, no se reanuda) |
| `ts=` | cuándo se guardó (ticks de `DateTime.Now`); mientras lo de antes de un apagón no se ha restaurado, se conserva el `ts` de entonces |
| `creditos=` | los créditos (los de antes de un apagón aún sin restaurar más los de ahora: `EstadoPorRestaurar`) |
| `sonando=S\|ruta` o `V\|ruta` | la canción o el video que sonaba (**sin posición**: vuelve a sonar DESDE EL PRINCIPIO, a petición; un `pos=` viejo se ignora) |
| `cola=S\|ruta` / `V\|ruta` | la cola, en orden (el turno es ese orden; el letrero «turno» es configuración, no estado) |

**Cuándo** — en cada evento (moneda, pedir, cambio de canción, `SaveState`); el texto se arma en la pantalla y lo escribe un hilo de fondo
en orden (`EscribeEstado`, `GuardaUltimo`, números de encargo: uno viejo nunca pisa a uno nuevo ni resucita uno borrado). **Cómo** —
`EscribeSeguroYa`: archivo de paso `.tmp`, `FlushFileBuffers` (bajarlo al disco de verdad) y `File.Replace(tmp, estado, .bak)` (el bueno
pasa a `.bak` y el nuevo a su sitio de un tirón).

**Al volver** — `RestoreState` cuando la biblioteca ya está armada (o cuando vuelve el disco que faltaba): los mismos créditos, la cola, y
la que sonaba entra primero y suena desde el principio. Lo que ya no existe (y su disco sí está) **devuelve su crédito**. Una moneda antes de
restaurar se suma a lo de antes (no lo pisa).

**Cuándo se descarta** (los únicos tres caminos; un apagón no dispara ninguno):
1. `OnFormClosing` con `WindowsShutDown` (el botón de poder: Windows se apaga) o `UserClosing` (Alt+F4): `BorraEstado` → `BorraSeguro`
   borra la copia, el de paso y por último el bueno (si se va la luz a medias, queda el bueno: se reanuda). Con cualquier otra razón
   («cierre por otra causa») NO se borra y se espera hasta 15 s a que lo encargado llegue al disco (`WaitDataWrites`). Si la lectura del
   arranque no acabó (`startupReadPending`), tampoco se borra.
2. Al cruzar las 7 AM **ociosa** (nada sonando, cola vacía, 10 min sin botones): créditos a 0 y `BorraEstado` (`CheckBusinessDayReset`).
3. Al restaurar, si es de otro «día del negocio» (corte a las 7 AM) y de hace más de 30 min: `BorraEstado`.

## 2. rocola-xp y el sistema: vía por vía

| Vía | En Rocola OS | Razón que ve XP | Estado | Visto en |
|---|---|---|---|---|
| Corte de luz | nada corre | — | se conserva | QEMU (§4) |
| `kill -9` | nada corre | — | se conserva y reanuda desde el principio | prueba local `kill9` |
| Caída (SIGSEGV, excepción sin atrapar) | nada corre (.NET aborta; la del reposo sale de `Corre` sin cerrar, como WinForms) | — | se conserva | `segv` |
| Botón de encendido (`/etc/acpi/PWRF/00000080`) | el lanzador, `SIGTERM` a rocola-xp, 20 s, `sync`, `poweroff` | `WindowsShutDown` (`PantallaRocola`, `PosixSignalRegistration`) | **se borra** (`.bak` y `.tmp` también) | `sigterm` |
| Apagar desde casa (buzón) | igual que el botón | `WindowsShutDown` | se borra | (mismo camino que `sigterm`) |
| Reiniciar el programa (buzón), la consola Ctrl+Alt+F2, `rocola-sonido`, el reinicio de un sistema nuevo sin confirmar (`S99rocola`), reiniciar el equipo (buzón) | `SIGUSR1` (`cierra_xp`, nunca `SIGTERM`); a los 20 s `SIGKILL` (= apagón) | `TaskManagerClosing` | se conserva (espera a que lo encargado llegue al disco) | `usr1` |
| `SIGHUP` (al morir el lanzador se cuelga tty1) | el lanzador la ignora (`trap '' HUP`) y rocola-xp la hereda ignorada | — | nada pasa | `hup` |
| `SIGTERM`/`SIGUSR1` en los primeros instantes (antes de registrar los avisos) | la señal la corta en seco | — | se conserva (como en XP: sin ventana, Windows mata el proceso) | revisado en el código |
| El lanzador vuelve a abrirla tras una caída | no toca el estado (`/usr/bin/rocola`: ni lo lee ni lo borra) | — | se reanuda | revisado; la reapertura, en `kill9` y `segv` (la abre la prueba, no el lanzador) |
| `rocola-actualiza` | no reinicia: el sistema nuevo entra al siguiente encendido | — | — | revisado |
| Apagar desde la consola (`poweroff`) | rocola-xp ya se cerró con `SIGUSR1` al cambiar de consola | `TaskManagerClosing` | se conserva (reinicio propio de Rocola OS) | revisado |
| El importador de XP (`--importa-xp`) | no trae `rocola_estado.txt` de XP ni pisa el de rocola-xp | — | igual byte a byte | `importa` |
| Lo de Rocola OS al cerrar (`RocolaOs.Cierre.cs`) | corre antes de `OnFormClosing` y no toca el estado de XP (`ParaAlCerrar` solo para procesos e hilos) | — | — | revisado |
| Spotify pagado y sin sonar | su `spotify:track:` va en el estado de XP; al restaurar no existe en la biblioteca y XP devuelve su crédito (R5) | — | vuelve como crédito | `spotify-xp/prueba.sh apagon` |

`Codigo/` solo difiere de XP en lo de traducción: `FlushFileBuffers` → `fs.Flush(true)` (`fsync` del archivo) y `CloseReason` →
`RazonCierre`. `File.Replace` en .NET para Linux es `unlink(.bak)`, `link(estado, .bak)` y `rename(.tmp, estado)` (visto con strace): la
misma copia y el mismo cambio de un tirón que `ReplaceFile`. La hora del «día del negocio» es la local de México (`/etc/localtime`, el
reloj de la placa leído en hora local por `S02hora`, corregido por NTP al minuto): igual que XP, que también se fía del reloj de la placa.

## 3. El sistema de archivos y lo que Linux necesita

`/rocola/datos` es un ext4 (diario `data=ordered`, `commit=5`, `barrier`; visto en QEMU) dentro de `datos.img` (512 MB, escrito entero al
instalar: sin huecos), que está en el NTFS del disco de la rocola (`/rocola/disco`, ntfs3), por un `loop`. Un `fsync` del de paso hace que
ext4 asiente su diario, el `loop` pide bajar `datos.img` y ntfs3 lo baja (`generic_file_fsync`) y vacía la caché del disco.

- **Vacío o roto, nunca**: el de paso se baja antes del cambio de nombre, ext4 en modo `ordered` no publica un nombre sin sus datos, y si
  aun así el bueno faltara o quedara vacío, XP lee la copia `.bak` (`LeeLineasSeguro`, la cabecera en `RestoreState`). Comprobado matando la
  rocola justo en cada llamada del guardado (§4, `inyecta`).
- **Viejo, sí (EL FALLO)**: en Windows el cambio de nombre lo lleva el diario de NTFS; en Linux, POSIX no lo baja hasta un `fsync` de la
  carpeta, y ext4 lo deja en la memoria hasta el siguiente asiento (hasta 5 s, o el siguiente `fsync` de cualquier archivo). Visto en QEMU
  con el sistema instalado: cinco guardados como los de XP (de paso con `fsync`, cambio de nombre) y el corte 0,5 s después: quedó el **4.º**
  y el de paso suelto. Con la rocola eso es: echar una moneda y que se vaya la luz al segundo → al volver, los créditos de antes.
- **El arreglo**: `mount -o noatime,dirsync` (`DATOS_OPC` en `initramfs/init`). Con `dirsync` el núcleo baja cada crear, enlazar, cambiar
  de nombre y borrar en `/rocola/datos` antes de volver de la llamada. Se eligió en el sistema y no en el programa porque: no toca `Codigo/`
  (XP no hace `fsync` de carpetas: sería lógica nueva, no traducción), cubre todo lo que se guarda ahí (lo de XP, lo de Rocola OS, los
  borrados del apagado a propósito) y entra con una compilación rápida (el initrd se rehace con el `init` nuevo). El costo: cada una de esas
  operaciones espera al disco; en esta máquina el cambio de nombre del estado pasó de 0,05 ms a 1-4 ms de mediana (strace, §4), en hilos de fondo, y en
  18 s de uso la rocola hizo unas 40 (casi todas del estado). **En el A4 no se midió.**
- **El NTFS «sin revisar»**: escribir dentro de `datos.img` sin agrandarlo no marca el NTFS sucio (ntfs3 solo lo marca al crear, agrandar,
  reservar o borrar: `fs/ntfs3/file.c`, `frecord.c`, `inode.c`), así que guardar el estado no deja el disco para revisar tras un apagón. Lo
  demás que se escribe en `/rocola/disco` ya lo cuida el vigilante de `S99rocola` (un `sync` cada 15 s si está sucio). Sin cambios.
- **El apagado a propósito** ya hacía `sync` y `umount -a -r` (inittab), así que sus borrados llegaban al disco; con `dirsync` llegan
  además en el momento (si se va la luz entre el botón y el `poweroff`, no resucita el estado).

## 4. Las pruebas (con números)

**Fuera de pantalla** — `Pruebas/RocolaOs/apagones/prueba.sh PROGRAMA` (rocola-xp de verdad en JIT, la tarjeta con reloj que guarda lo que
de verdad sonó, datos inventados con tres tonos de 30 s que cambian a la mitad para saber desde dónde vuelve a sonar). 36 de 36 BIEN (corrida final, 20-11-2026, `local-jit-6.log`):

| Escena | Qué se hace | Comprobaciones |
|---|---|---|
| `kill9` | 4 monedas (16 créditos), pide la 1 (suena), la 2 y la 3 (cola): 13 créditos; `kill -9` a los 9 s de la 1; se vuelve a abrir y al final `SIGTERM` | 8: salió con 137; el estado quedó con 13, la 1 sonando, la 2 y la 3; antes del corte sonaron de 5 a 12 s de la 1 y nada de su segunda mitad; al volver 13 créditos y cola de 2; suena la 1 **desde el principio** (sus 15 s de 440 Hz enteros antes de los 470 Hz); después la 2 en su turno; el `SIGTERM` final lo borró entero (ni `.bak` ni `.tmp`) |
| `segv` | igual, con dos `SIGSEGV` (el primero lo toma .NET) | 3: salió con 139; el estado sobrevivió; al volver 13, la 1 y cola de 2 |
| `sigterm` | igual, con `SIGTERM` (el apagado) | 5: salió con 0; cerró con `WindowsShutDown`; el estado borrado entero; al volver 0 créditos, nada, cola vacía; no suena nada |
| `usr1` | igual, con `SIGUSR1` (reinicios de Rocola OS) | 4: salió con 0; cerró con `TaskManagerClosing`; el estado se conservó; al volver reanuda |
| `hup` | `SIGHUP` con su acción de fábrica, y con `SIGHUP` ignorada como la deja el lanzador | 3: con la de fábrica muere (129) y el estado sobrevive; ignorada, sigue abierta hasta el `SIGKILL` de la prueba |
| `inyecta` | strace mata la rocola (`SIGKILL`) justo en una llamada del guardado: `pwrite64` (sin hacerla), `fsync` (sin hacerla y hecha), `link` del `.bak` (sin hacerla y hecha), `rename` (sin hacerla y hecha), `unlink` del `.bak` | 8: cada vez murió ahí, el estado que quedó está entero (cabecera, `ts`, créditos: 8, 12 o 16 según el momento) y al volver la rocola leyó los mismos créditos |
| `strace` | las llamadas de cada guardado; las opciones del `init` del repositorio; un ext4 en un archivo montado con ellas (y otro sin `dirsync`) y la rocola guardando allí | 3: de paso + `fsync` + `link` `.bak` + `rename` (7 guardados en 18 s); el `init` monta con `noatime,dirsync`; con `dirsync` el cambio de nombre tarda 4,3 ms de mediana (el más rápido 0,64 ms) frente a 0,05 ms sin él: espera al disco |
| `importa` | `--importa-xp` con un estado del día en la carpeta de XP (5 créditos) y otro en la de rocola-xp (13 y su cola) | 2: importó (0); el de rocola-xp quedó igual byte a byte, sin `.bak` ni `.antes-xp` |

Dos arreglos de la prueba en esta tanda (no del programa): `hup` pone la acción de fábrica de `SIGHUP` antes de abrir la rocola (corrida
con `nohup`, la rocola la heredaba ignorada y no moría: 2 MAL en `local-jit-5.log`, por eso); y la escena `strace` mide `dirsync` en vez
de buscar un `fsync` de la carpeta, que el arreglo no usa.

**Spotify con el Soloist falso** — `Pruebas/RocolaOs/spotify-xp/prueba.sh PROGRAMA apagon` (nunca el Soloist de verdad). 4 de 4 BIEN: el estado guardado con la de Spotify sonando y otra en la cola y 2 créditos; al morir la rocola (`kill -9`) el vigilante y el
Soloist falso se fueron solos; al volver, lo pagado de Spotify volvió como crédito (2 + 2 = 4) y no sonó nada de Spotify. Con `SIGUSR1`
(se conserva) el camino es el mismo de XP (la ruta `spotify:` no está en la biblioteca: crédito devuelto); revisado en el código, no
corrido.

**El corte de luz de verdad, en QEMU** — `Pruebas/Qemu/apagones.py` sobre el sistema instalado de la prueba de Spotify
(`/root/rocola-trabajo/act/qemu-spotify/xp.qcow2`, con una capa encima que se tira): QEMU se cierra en seco (`quit`: lo que la máquina no
había bajado a su disco se pierde) sin `sync`, y al volver a prender se mira qué quedó. Primero se vio el fallo; después, el arreglo de verdad: el `init` del repositorio entra en un cpio aparte **pegado al final** del
`initrd.gz` instalado (el núcleo desempaca los cpio seguidos y el último `init` manda; BusyBox no arma cpio, así que se arma aquí y entra
por un segundo disco en bruto), se vuelve a prender y `/proc/mounts` dice `rw,dirsync,noatime`.

| Prueba | Sin el arreglo (como está hoy) | Con el `init` del repositorio (`dirsync`) |
|---|---|---|
| `dd`: cinco guardados como los de XP (de paso con `fsync`, cambio de nombre), el corte 0,5 s después del 5.º | quedó el **4.º** y el de paso suelto (`v=4`, `prueba_apagon.txt.tmp`), las tres veces (17:08, 20:51 y 21:11) | quedó el 5.º y nada suelto |
| `dd`, el corte al momento | — | quedó el 5.º |
| `moneda`: rocola-xp de verdad, una moneda (13 → 17 créditos, visto en `rocola_estado.txt`), el corte 0,5 s después | al volver, **13** (la moneda perdida) | al volver, 17 |
| `moneda`, el corte al momento de verlo | al volver, **13** | al volver, 17 |

(La corrida de las 21:11 pedía `mount -o remount,dirsync`, que el núcleo no aplica al remontar —`dirsync` no está en `MS_RMT_MASK`—: medía
sin `dirsync`; por eso el arreglo se prueba con el `init` de verdad. El modo quedó en el guion solo para verlo.)

## 5. Lo que queda

- **Nada en el A4.** Ni el corte de luz de verdad, ni cuánto tarda allí cada cambio de nombre con `dirsync` (en esta máquina 0,6 a 4 ms;
  el A4 lleva un SSD, más el `loop` y ntfs3 debajo).
- El arreglo llega con la siguiente compilación del sistema (la rápida rehace el `initrd.gz` con el `init` nuevo). No se compiló aquí.
- En QEMU no se miró si el corte dejó el NTFS de `/rocola/disco` «sin revisar» (lo cuida `S99rocola` desde el 7-11-2026; guardar el
  estado no lo marca, según el código de ntfs3).
- Solo JIT: el programa no cambió (el arreglo es del sistema), así que no se compiló NativeAOT.
- `SIGUSR1` con Spotify en la cola y el botón de encendido de verdad en QEMU (ACPI) no se corrieron en esta tanda: el primero es el mismo
  camino que el apagón (§2); el segundo ya se probó en QEMU el 14-11-2026 (`Pruebas/Qemu/prueba.py`) y no cambió.
- El reloj: si el de la placa está muy mal al prender (antes del NTP del primer minuto), la regla de las 7 AM puede descartar o conservar
  un estado que no debía, igual que en XP. No se cambia.
- En QEMU el puerto serie a veces no contesta en todo un arranque con la máquina cargada (dos veces en esta tanda); `apagones.py` ahora
  vuelve a prender (hasta tres veces).
