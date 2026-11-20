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
| El lanzador vuelve a abrirla tras una caída | no toca el estado | — | se reanuda | `kill9`, `segv` (la segunda corrida) |
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
  operaciones espera al disco; en esta máquina el cambio de nombre del estado pasó de 0,05 ms a ~1 ms (strace, §4), en hilos de fondo, y en
  18 s de uso la rocola hizo unas 40 (casi todas el estado). **En el A4 no se midió.**
- **El NTFS «sin revisar»**: escribir dentro de `datos.img` sin agrandarlo no marca el NTFS sucio (ntfs3 solo lo marca al crear, agrandar,
  reservar o borrar: `fs/ntfs3/file.c`, `frecord.c`, `inode.c`), así que guardar el estado no deja el disco para revisar tras un apagón. Lo
  demás que se escribe en `/rocola/disco` ya lo cuida el vigilante de `S99rocola` (un `sync` cada 15 s si está sucio). Sin cambios.
- **El apagado a propósito** ya hacía `sync` y `umount -a -r` (inittab), así que sus borrados llegaban al disco; con `dirsync` llegan
  además en el momento (si se va la luz entre el botón y el `poweroff`, no resucita el estado).

## 4. Las pruebas (con números)

**Fuera de pantalla** — `Pruebas/RocolaOs/apagones/prueba.sh PROGRAMA` (rocola-xp de verdad en JIT, la tarjeta con reloj que guarda lo que
de verdad sonó, datos inventados con tres tonos de 30 s que cambian a la mitad para saber desde dónde vuelve a sonar). RESULTADOS_LOCALES

**Spotify con el Soloist falso** — `Pruebas/RocolaOs/spotify-xp/prueba.sh PROGRAMA apagon` (nunca el Soloist de verdad). RESULTADOS_SPOTIFY

**El corte de luz de verdad, en QEMU** — `Pruebas/Qemu/apagones.py` sobre el sistema instalado de la prueba de Spotify
(`/root/rocola-trabajo/act/qemu-spotify/xp.qcow2`, con una capa encima que se tira): QEMU se cierra en seco (`quit`: lo que la máquina no
había bajado a su disco se pierde) sin `sync`, y al volver a prender se mira qué quedó. RESULTADOS_QEMU

## 5. Lo que queda

PENDIENTES
