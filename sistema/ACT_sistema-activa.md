# Activación de rocola-xp en Rocola OS: el sistema y los guiones (grupo sistema-activa)

14-11-2026. Rama `act-sistema-activa`, sobre f62d6cb. Pone en práctica la decisión del dueño del 14-11-2026: `rocola-xp` es el único
programa de Rocola OS y `rocola-consola` se retira (`sistema/INTEGRACION_ROCOLA_XP.md`, reescrito para el estado activado). El
importador de lo aprendido en XP y el material de la IA dentro de `rocola-xp` son del otro grupo de la tanda (xp-datos); aquí, el
sistema que los llama. **No se lanzó nada en GitHub**; nada se probó en el A4 ni con el sistema entero.

## Lo hecho

| Archivo | Qué |
|---|---|
| `sistema/configs/rocola_a4_defconfig` | `BR2_PACKAGE_ROCOLA_XP=y`; fuera `BR2_PACKAGE_ROCOLA_CONSOLA`; `BR2_PACKAGE_ROCOLA_SPOTIFY` se queda. Los comentarios de DejaVu, mpv, ffmpeg, mpg123 y jpeg dicen ahora quién los usa. |
| `sistema/Config.in` | lee el menú de `rocola-xp`; el de `rocola-consola` se queda (paquete apagado, como referencia: no hace falta para que el defconfig valga, y así sigue existiendo). |
| `sistema/package/rocola-xp/` | compila con `-p:RocolaVersion` (= `ROCOLA_SISTEMA_VERSION`), `-p:RocolaCommit` (`GITHUB_SHA` o `git`, 7 letras) y `-p:RocolaFecha`; `/usr/lib/rocola-xp/version` = lo que dice `--version` (si dice otra cosa, falla; si el equipo no lo puede correr, la línea esperada); pone `/usr/share/rocola/prueba.mp3`, el disco «Rocola OS - Prueba» y `/rocola/datos`, que ponía `rocola-consola`; `select` de DejaVu y mpg123. |
| `sistema/rapida.sh` | fuera el paso de `rocola-consola`; el de `rocola-xp` (que corre si cambió o si la base no lo trae, también con «rapida_ya») con su versión, la verificación de `--version` y la canción de prueba. |
| `usr/bin/rocola` (el lanzador) | siempre `rocola-xp`; fuera el interruptor (se ignora, no se borra), el programa bajado y la vuelta a `rocola-consola`; el importador antes de cada arranque (el contrato, con tope de 10 min, aviso en pantalla a los 3 s, una línea en la bitácora por código); `ROCOLA_IA_DATOS=/rocola/disco/rocola-os/ia-xp` solo con el disco con escritura; la tarjeta elegida cada vez; esperas de 2 s, 10 s, 30 s y 2 min tras caídas seguidas, apuntadas; la confirmación solo tras 120 s seguidos de un mismo `rocola-xp`; sin `rocola-xp`, aviso y fuera; el importador no cuenta como rocola abierta (ni para la consola, ni para «reiniciar ya», ni para confirmar). |
| `usr/bin/rocola-actualiza` | el canal del programa apagado (nada de `programa-N`; `/rocola/datos/programa` ni se crea ni se toca); el estado dice la versión de `rocola-xp`; el sistema nuevo «entra al siguiente encendido». |
| `usr/bin/rocola-buzon` | la versión, el proceso y los errores de `rocola-xp`; lo «cuando esté libre» y el diagnóstico se contestan («todavía no se puede... manda «... ya»») en lugar de quedar pendientes para siempre; archivos de `/rocola/datos/xp` primero; registros con `xp_`; las «ya» paran siempre antes el lanzador. |
| `usr/bin/rocola-sonido` | para el lanzador, cierra `rocola-xp` con SIGUSR1 si estuviera abierta (con la marca: no es caída) y lo deja seguir aunque se corte a medias; `usar`/`auto` la reabren; los errores de `rocola-xp`. |
| `usr/bin/rocola-prueba` | la canción de prueba con `mpg123 -o alsa -a TARJETA`; la pantalla con `rocola-xp` 20 s (`timeout -s USR1`, datos de prueba en `/tmp` que se borran); lo que comprueba y lo que apunta, igual. |
| `etc/acpi/PWRF/00000080` | solo `rocola-xp` (SIGTERM, 15 s). |
| `etc/init.d/S99rocola` | la confirmación nueva en los comentarios; el reinicio de un sistema que no se confirma para antes el lanzador y cierra `rocola-xp` con SIGUSR1 (antes el `reboot` le mandaba SIGTERM y borraba el estado). |
| `sistema/rocola/Pruebas/Lanzador/` | `prueba.sh`, `dentro.sh`, `falso.c` (también importador), `gh_falso.py` (GitHub de mentira): 14 casos. |
| `sistema/INTEGRACION_ROCOLA_XP.md` | reescrito para el estado activado. |

## Comprobaciones (máquina de pruebas)

- **`sh -n` con el BusyBox del sistema** (`salida-qemu/target/bin/busybox`, v1.37.0): `rocola`, `rocola-buzon`, `rocola-actualiza`,
  `rocola-sonido`, `rocola-prueba`, `00000080`, `S99rocola`, `dentro.sh`: bien. **`bash -n`** de `rapida.sh` y `prueba.sh`: bien.
- **`rapida.sh decide 6a452567dd25`** (sistema-36, la base de hoy, completa): antes (f62d6cb) «rapida»; después «completa» por
  `sistema/Config.in`, `configs/rocola_a4_defconfig`, `package/rocola-xp/Config.in`, `package/rocola-xp/rocola-xp.mk`.
- **Buildroot 2026.02.3** (`make rocola_a4_defconfig` en una carpeta aparte): las 0 líneas del defconfig que faltan en `.config`
  (todas quedan), `BR2_PACKAGE_ROCOLA_XP=y`, `# BR2_PACKAGE_ROCOLA_CONSOLA is not set`, `BR2_PACKAGE_ROCOLA_SPOTIFY=y`; `printvars` de
  las órdenes del paquete, expandidas como se esperaba.
- **`prueba.sh`** (todos los casos): PENDIENTE_RESUMEN
- **grep** en `rootfs_overlay`: `rocola-consola` ya no se abre en ningún sitio; queda nombrado solo en comentarios (11 líneas: la historia
  del cambio y dónde quedan sus datos).
- PENDIENTE_AOT

## Contrato con el otro grupo (xp-datos)

- Antes de cada arranque, si `/run/rocola-datos-xp` existe y no está vacío:
  `ROCOLA_DATOS=/rocola/datos/xp /usr/lib/rocola-xp/rocola-xp --importa-xp /run/rocola-datos-xp` (el argumento es el archivo de
  `rocola-discos`, cuya primera línea dice la carpeta; `ImportaXp.cs` acepta el archivo o la carpeta), con `ROCOLA_IA_DATOS` ya puesta
  si toca. 0 = bien (también «ya importado»); otro código: se apunta (rocola.txt cada vez; la bitácora una vez por código) y la rocola se
  abre igual. Tope 600 s (`timeout -k 10 600`: SIGTERM y, 10 s después, SIGKILL); si llega al tope, no se vuelve a llamar en ese
  encendido. Se le puede cortar (consola: no; «reiniciar el equipo ya» o «apagar ya» del buzón, el botón de encendido: sí) y se le
  vuelve a llamar la siguiente vez: tiene que poder repetirse (lo hace: carpeta de paso y marca al final).
- `ROCOLA_IA_DATOS=/rocola/disco/rocola-os/ia-xp` (creada por el lanzador) solo si `/rocola/disco/rocola-os` existe y `/rocola/disco`
  está montado `rw`; si no, sin la variable. Se pone una vez al prender, para el importador y para la rocola.
- Un `rocola-xp` con `--importa-xp` en su línea de órdenes no cuenta como la rocola abierta (la consola no lo cierra, no confirma un
  sistema nuevo, el buzón no lo cuenta como «corriendo»).

## Lo que queda

- Compilar el sistema (la rápida con «rapida_ya», el coordinador) y probarlo entero en QEMU: pantalla KMS, teclas, sonido, video, el
  importador con una carpeta de XP de mentira, la consola, el botón, un apagón.
- Rocola en casa (`sistema/casa/Resumen.cs`) compara la versión del programa con la última `programa-N` (de `rocola-consola`): con
  `rocola-xp` dirá que hay una más nueva. Puede usar `versiones.programa_cual` («rocola-xp») para no compararla. No era de este grupo.
- `rocola-instalar` (no era de este grupo) solo traduce `/rocola/discos/<partición>/` a `/rocola/disco/` en los `rocola_*.txt` de la raíz
  de lo aprendido, no en `/rocola/datos/xp`: si se prueba con la memoria USB en la rocola y después se instala, las tablas de
  `rocola-xp` llevarían rutas de la memoria. Hoy el importador vuelve a buscar las canciones por disco; sin probar.
- `LEEME.md` y `ETAPA2.md` siguen hablando de `rocola-consola` como el programa (son historia; no eran de este grupo).
- Todo lo del A4.

## Riesgos

- **Sin respaldo**: si `rocola-xp` se cae al arrancar en el A4 (algo de la tarjeta, de Mesa r600, de ALSA), la rocola no suena hasta
  arreglarlo; el lanzador reintenta cada 2 min y lo apunta. Un sistema nuevo así vuelve solo al anterior (no se confirma), pero el
  primero que lleve `rocola-xp` viene de sistema-36: si ese falla, el anterior es sistema-36 (con `rocola-consola`), y se vuelve a él
  solo o con ESC.
- **La primera importación** puede tardar en el A4 (miles de archivos en NTFS). El tope es 10 min; si se pasa, la rocola abre sin lo de
  XP ese día y lo intenta al siguiente encendido. Mientras tanto, la pantalla dice que está trayendo lo aprendido.
- **La confirmación** necesita 2 min seguidos de `rocola-xp` dentro de los 15 min: si el dueño entra a la consola de mantenimiento
  justo al estrenar un sistema y se queda más de 13 min, `S99rocola` reinicia (como antes).
- **Apagar con la rocola cerrada** (en la consola) no borra el estado del día: al prender reanuda (antes igual).
- **`pidof`/`killall` por nombre**: el importador también se llama `rocola-xp`. El lanzador, el buzón y `rocola-sonido` lo distinguen
  por `--importa-xp` en su línea de órdenes; el botón, `S99rocola` y las «ya» del buzón lo cortan con lo demás (se vuelve a llamar).
- La rápida sobre sistema-36 deja `/usr/bin/rocola-consola` en la imagen sin usarse (unos 6 MB) hasta la siguiente completa.
- El número de SIGUSR1 (10) es el de x86-64.
