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
| `sistema/rapida.sh` | fuera el paso de `rocola-consola`; el de `rocola-xp` (que corre si cambió o si la base no lo trae, también con «rapida_ya») con su versión, la verificación de `--version` y la canción de prueba; si no compila, las últimas 60 líneas de lo que dijo `dotnet publish` salen en el registro (antes iban a `/dev/null`). |
| `usr/bin/rocola` (el lanzador) | siempre `rocola-xp`; fuera el interruptor (se ignora, no se borra), el programa bajado y la vuelta a `rocola-consola`; el importador antes de cada arranque (el contrato, con tope de 10 min, aviso en pantalla a los 3 s, una línea en la bitácora por código); `ROCOLA_IA_DATOS=/rocola/disco/rocola-os/ia-xp` solo con el disco con escritura; la tarjeta elegida cada vez; esperas de 2 s, 10 s, 30 s y 2 min tras caídas seguidas, apuntadas; la confirmación solo tras 120 s seguidos de un mismo `rocola-xp`; sin `rocola-xp`, aviso y fuera; el importador no cuenta como rocola abierta (ni para la consola, ni para «reiniciar ya», ni para confirmar). |
| `usr/bin/rocola-actualiza` | el canal del programa apagado (nada de `programa-N`; `/rocola/datos/programa` ni se crea ni se toca); el estado dice la versión de `rocola-xp`; el sistema nuevo «entra al siguiente encendido». |
| `usr/bin/rocola-buzon` | la versión, el proceso y los errores de `rocola-xp`; lo «cuando esté libre» y el diagnóstico se contestan («todavía no se puede... manda «... ya»») en lugar de quedar pendientes para siempre; archivos de `/rocola/datos/xp` primero; registros con `xp_`; las «ya» paran siempre antes el lanzador. |
| `usr/bin/rocola-sonido` | para el lanzador, cierra `rocola-xp` con SIGUSR1 si estuviera abierta (con la marca: no es caída) y lo deja seguir aunque se corte a medias; `usar`/`auto` la reabren; los errores de `rocola-xp`. |
| `usr/bin/rocola-prueba` | la canción de prueba con `mpg123 -o alsa -a TARJETA`; la pantalla con `rocola-xp` 20 s (`timeout -s USR1`, datos de prueba en `/tmp` que se borran); lo que comprueba y lo que apunta, igual. |
| `etc/acpi/PWRF/00000080` | solo `rocola-xp` (SIGTERM, 15 s). |
| `etc/init.d/S99rocola` | la confirmación nueva en los comentarios; el reinicio de un sistema que no se confirma para antes el lanzador y cierra `rocola-xp` con SIGUSR1 (antes el `reboot` le mandaba SIGTERM y borraba el estado). |
| `sistema/rocola/Pruebas/Lanzador/` | `prueba.sh`, `dentro.sh`, `falso.c` (también importador), `gh_falso.py` (GitHub de mentira): 15 casos (el último, `contrato`, con el importador de verdad si se da `ROCOLA_XP_REAL`); la revisión añadió `colgar` y `terminal.c` (16). |
| `sistema/INTEGRACION_ROCOLA_XP.md` | reescrito para el estado activado. |

## Comprobaciones (máquina de pruebas)

- **`sh -n` con el BusyBox del sistema** (`salida-qemu/target/bin/busybox`, v1.37.0): `rocola`, `rocola-buzon`, `rocola-actualiza`,
  `rocola-sonido`, `rocola-prueba`, `00000080`, `S99rocola`, `dentro.sh`: bien. **`bash -n`** de `rapida.sh` y `prueba.sh`: bien.
- **`rapida.sh decide 6a452567dd25`** (sistema-36, la base de hoy, completa): antes (f62d6cb) «rapida»; después «completa» por
  `sistema/Config.in`, `configs/rocola_a4_defconfig`, `package/rocola-xp/Config.in`, `package/rocola-xp/rocola-xp.mk`.
- **Buildroot 2026.02.3** (`make rocola_a4_defconfig` en una carpeta aparte): ninguna línea del defconfig falta en `.config`;
  `BR2_PACKAGE_ROCOLA_XP=y`, `# BR2_PACKAGE_ROCOLA_CONSOLA is not set`, `BR2_PACKAGE_ROCOLA_SPOTIFY=y`; `printvars` de
  las órdenes del paquete, expandidas como se esperaba.
- **`prueba.sh`** (los 14 casos, BusyBox del sistema, con la máquina cargada por las pruebas del otro grupo: carga 27 a 38 en 4 núcleos):
  **90 BIEN, 0 MAL** (sale con 0). Por caso: arranque 11/11 (el importador primero con el contrato, el entorno, los `dir=`,
  `rocola_programa.txt` ignorado y sin borrar, nada escrito en la carpeta de XP, Alt+F4 no es caída y vuelve en 2 s), ia 5/5 (con
  escritura al importador y a la rocola; en solo lectura y sin disco, sin la variable), importa 10/10 (sin carpeta de XP no se llama;
  sale con 3: se abre igual, una línea en la bitácora y una por vez en rocola.txt; pasa del tope de 4 s: cortado con 143, apuntado, aviso
  en pantalla y borrado, no se vuelve a llamar; si acaba en menos de 3 s la pantalla no cambia), caidas 7/7 (esperas medidas «1 2 3 4 4»
  con el reloj acortado; tras aguantar más de lo «seguido», otra vez 1; los cierres solos con 0 cuentan), confirma 4/4 (cayéndose cada 2 s
  no se confirma; el importador de 10 s no cuenta; aguantando sí), consola 5/5 (SIGUSR1, nunca SIGTERM, no se abre en la consola, vuelve
  con el importador; en la consola a mitad de la importación, el importador acaba y la rocola no se abre hasta volver), reinicia 8/8
  (ordenado y cortado al arrancar: no son caídas y los avisos se quitan; uno que no se cierra, cortado a los 20 s y apuntado), boton 1/1,
  sinprograma 3/3 (aviso en pantalla, una línea en la bitácora, sin bucle; con el sistema en prueba, «vuelve solo al anterior»),
  sonido 10/10 (el lanzador parado mientras suena y suelto al acabar, también si se corta a medias; con la rocola abierta, SIGUSR1 y otra
  vez, sin contar como caída; `usar 2` y `auto` se ven en la siguiente rocola), prueba 6/6 (`mpg123 -q -o alsa -a plughw:0,0`, la pantalla
  con `rocola-xp` y datos en `/tmp/rocola-prueba-xp`, SIGUSR1 a los 20 s, «se dibujo 20 s (y se cerro bien)», los datos de prueba
  borrados y `/rocola/datos/xp` sin crear), s99 3/3 (sin confirmar: SIGUSR1, sin lanzador y reboot; confirmado: «quedo bien»), buzon 11/11
  (las cuatro «cuando esté libre» contestadas y nada pendiente; archivos de `rocola-xp` primero; las llaves no, tampoco la de su carpeta;
  `xp_` en los registros; el estado con la versión, el proceso y los errores de `rocola-xp`; «reiniciar_programa ya», «reiniciar_equipo
  ya» -SIGUSR1- y «apagar ya» -SIGTERM-; con la rocola en la consola el lanzador también se para), actualiza 6/6 (ni una petición a los
  archivos de `programa-99` ni `/rocola/datos/programa`; el sistema 8 bajado a la copia b, en prueba, «entra al siguiente encendido»; lo
  que había en `/rocola/datos/programa` sin tocar).
- **La corrida final, los 15 casos con el importador de verdad** (`ROCOLA_XP_REAL`, en `0defcd8`; carga 4 a 10): **95 BIEN, 0 MAL**. (En
  la anterior salió una vez «MAL: y sigue al acabar [R S]»: la prueba pedía que todos los procesos del lanzador estuvieran esperando y uno
  corría en ese instante; ahora mira que ninguno esté parado.)
- **`prueba.sh contrato` con el importador de verdad** (`ROCOLA_XP_REAL` = el `rocola-xp` NativeAOT que el otro grupo compiló a las 06:06
  de su rama, `Nativo/ImportaXp.cs`; corre con la glibc del sistema en la raíz de prueba): **5 BIEN, 0 MAL**: sale con 0 («importado de
  /rocola/disco/Rocola/Release a /rocola/datos/xp...»), deja su marca y la rocola se abre; los botones tal cual y la configuración de XP con
  el `dir=` del lanzador detrás; la carpeta de XP sin cambiar (lista con fechas y contenido); la segunda vez «ya importado», 0, y el `dir=` sin
  repetir. Su `--version` con la glibc del sistema: «rocola-xp programa 0 (local) compilado 1794636290» (el `sed` de los guiones lee 0).
- **`rapida.sh arma` de verdad, con una base de mentira** (un sistema ext4 de 64 MB con un `rocola-consola` falso, su `disk.img` con la
  partición 1 del mismo tamaño, un `initrd.gz`) y un `dotnet` de mentira que apunta cómo lo llamaron y deja un `rocola-xp` que contesta lo
  que se le pasó: sin «rapida_ya» sale con 1 («hay cambios que necesitan la compilacion completa»); con `RAPIDA_YA=1` y
  `ROCOLA_SISTEMA_VERSION=99`, sale con 0 y lista los cuatro archivos que no entran hasta la completa; `dotnet publish -c Release -r
  linux-x64 -p:RocolaPerfil=a4 -p:RocolaVersion=99 -p:RocolaCommit=d8de93a -p:RocolaFecha=...` en `sistema/rocola/Xp`; en la imagen:
  `/usr/lib/rocola-xp/{rocola-xp,libSkiaSharp.so,version}` («rocola-xp programa 99 (d8de93a) compilado ...», igual a lo que dice
  `--version`), `prueba.mp3` (igual a la del repositorio) en `/usr/share/rocola` y en el disco de prueba, el lanzador nuevo, el
  `rocola-consola` de la base todavía ahí, `/etc/rocola-sistema-version` = 99; `disk.img` lleva ese sistema en su partición 1; las sumas
  bien. Con un `--version` que dice otra cosa: sale con 1 y lo dice; con uno que no corre: apunta la línea esperada; con un `dotnet` que
  falla: sale con 1 y enseña lo que dijo. Sin montajes ni `loop` colgados al acabar.
- **Las órdenes del paquete** (`ROCOLA_XP_BUILD_CMDS` e `..._INSTALL_TARGET_CMDS`, tal cual, con bash como Buildroot y el mismo `dotnet` de
  mentira): `programa 41 (d8de93a)` con `ROCOLA_SISTEMA_VERSION=41`; `programa 0 (0123456)` con solo `GITHUB_SHA`; instala el programa, la
  biblioteca, `version`, las dos `prueba.mp3` y `/rocola/datos`.
- **grep** en `rootfs_overlay`: `rocola-consola` ya no se abre en ningún sitio; queda nombrado solo en comentarios (11 líneas: la historia
  del cambio y dónde quedan sus datos).
- **No se compiló `rocola-xp` aquí** (ni `dotnet build` ni NativeAOT): este grupo no tocó `sistema/rocola/Xp` (ni `Consola` ni `Motor`:
  `git diff f62d6cb` vacío ahí), y la máquina estuvo saturada por las pruebas del otro grupo toda la tanda. Lo que dice el programa con
  `-p:RocolaVersion/-p:RocolaCommit/-p:RocolaFecha` lo comprobó el grupo de `--version` (14-11-2026); el binario de verdad corrió aquí como
  importador y con `--version` (arriba).

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
- ~~`rocola-instalar` solo traduce `/rocola/discos/<partición>/` a `/rocola/disco/` en los `rocola_*.txt` de la raíz de lo aprendido,
  no en `/rocola/datos/xp`~~: arreglado en la revisión (abajo). (Y no era cierto que el importador lo resolviera solo: daba la carpeta
  por «la misma en otro disco» y no volvía a traducir.)
- `LEEME.md` y `ETAPA2.md` siguen hablando de `rocola-consola` como el programa (son historia; no eran de este grupo).
- Todo lo del A4.

## Riesgos

- **Sin respaldo**: si `rocola-xp` se cae al arrancar en el A4 (algo de la tarjeta, de Mesa r600, de ALSA), la rocola no suena hasta
  arreglarlo; el lanzador reintenta cada 2 min y lo apunta. Un sistema nuevo así vuelve solo al anterior (no se confirma), pero el
  primero que lleve `rocola-xp` viene de sistema-36: si ese falla, el anterior es sistema-36 (con `rocola-consola`), y se vuelve a él
  solo o con ESC.
- **La primera importación** puede tardar en el A4 (miles de archivos en NTFS). El tope es 10 min; si se pasa, la rocola abre sin lo de
  XP ese día y lo intenta al siguiente encendido. Mientras tanto, la pantalla dice que está trayendo lo aprendido y la rocola no se puede
  usar. Si en el A4 siempre tardara más de 10 min, cada encendido esperaría esos 10 min y nunca acabaría: se vería en la bitácora («no
  acabo en 600 s») y habría que subir el tope (`IMPORTA_MAX` en el lanzador) o hacerlo más rápido.
- **La confirmación** necesita 2 min seguidos de `rocola-xp` dentro de los 15 min: si el dueño entra a la consola de mantenimiento
  justo al estrenar un sistema y se queda más de 13 min, `S99rocola` reinicia (como antes). Con la primera importación en ese mismo
  arranque hay margen: a lo más 10 min de importar y 2 de rocola, unos 13 con el arranque.
- **Apagar con la rocola cerrada** (en la consola) no borra el estado del día: al prender reanuda (antes igual).
- **`pidof`/`killall` por nombre**: el importador también se llama `rocola-xp`. El lanzador, el buzón y `rocola-sonido` lo distinguen
  por `--importa-xp` en su línea de órdenes; el botón, `S99rocola` y las «ya» del buzón lo cortan con lo demás (se vuelve a llamar).
- La rápida sobre sistema-36 deja `/usr/bin/rocola-consola` en la imagen sin usarse (unos 6 MB) hasta la siguiente completa.
- El número de SIGUSR1 (10) es el de x86-64.

## Revisión (revisor independiente, 14-11-2026)

Sobre 740f872: el diff entero contra la tarea y las reglas, las comprobaciones principales repetidas aquí y lo que estaba mal, arreglado
en commits aparte («Rocola OS (sistema-activa, revision): ...»). Sin push y sin nada lanzado en GitHub.

### Lo encontrado y arreglado

1. **Grave: con el botón de encendido el estado del día sobrevivía, y en los reinicios `rocola-xp` no se cerraba en orden.** `S99rocola`
   abre el lanzador con `setsid sh -c 'exec /usr/bin/rocola </dev/tty1 >/dev/tty1 2>&1'`: una sesión nueva que toma tty1 como terminal de
   control, y `rocola-xp` va en el grupo de procesos del lanzador (el de primer plano de tty1). `00000080`, `S99rocola` (sin confirmar) y
   las «ya» del buzón matan PRIMERO al lanzador; al morir el que controla la terminal, el núcleo cuelga tty1 y manda SIGHUP a ese grupo, y
   `rocola-xp` (que solo atiende SIGTERM y SIGUSR1: `Nativo/PantallaRocola.cs`) se moría en seco antes de que le llegara su señal.
   Comprobado con el `rocola-xp` de verdad (el NativeAOT de xp-datos, fuera de pantalla, una moneda: 4 créditos): SIGHUP sola, sale con
   129 sin `closing` y `rocola_estado.txt` queda; con un lanzador mínimo y una pseudoterminal de control, en el orden de `00000080`
   (SIGTERM al lanzador y luego a `rocola-xp`): sale por la SIGHUP y el estado QUEDA con los 4 créditos (contra la regla del 8-11-2026); con
   SIGHUP ignorada: `closing`/`closed` con `WindowsShutDown` y el estado BORRADO; con SIGUSR1 en vez de SIGTERM: `TaskManagerClosing` y el
   estado conservado. Venía de la integración apagada (el mismo orden en f62d6cb); las pruebas no lo veían porque abrían el lanzador sin
   terminal (`</dev/null`). **Arreglo**: `trap '' HUP` en el lanzador; `rocola-xp` y el importador la heredan ignorada (exec la conserva) y
   atienden solo las señales que les mandan a ellos. **Prueba nueva** `colgar` (`terminal.c`, «con-terminal»: el lanzador en una sesión con
   una pseudoterminal de control, como en tty1): el botón, `S99rocola` sin confirmar y «apagar ya»; sin el arreglo **0 de 4**, con él
   **4 de 4**.
2. **`rocola-instalar`** (instalar después de probar desde la memoria USB) traducía `/rocola/discos/<partición>/` → `/rocola/disco/`
   solo en los `rocola_*.txt` de `/rocola/datos`, no en `/rocola/datos/xp`. No es verdad que el importador lo arreglara solo (lo decía
   «Lo que queda»): con su marca diciendo la carpeta de la memoria, ya instalada la daba por «la misma carpeta en otro disco»
   (`Nativo/ImportaXp.cs`) y salía con 0 sin volver a traducir: `rocola-xp` se quedaba con rutas que ya no existen. Ahora el mismo `sed`
   pasa también por `xp/rocola_*.txt` (tablas, configuración y la marca: la siguiente vez dice «ya importado»). El trozo, probado con el
   BusyBox del sistema en una raíz aparte: rutas de la partición traducidas, las de otro disco intactas, CRLF y acentos («Ángel - Ñandú»,
   «Canción») intactos, archivos sin rutas sin tocar.
3. **El lanzador medía con la hora** (`date +%s`) lo que tarda el importador (el tope de 10 min) y lo que aguantó la rocola (caídas
   seguidas, cerrarse sola). `rocola-actualiza` pone la hora por NTP al minuto de prender, justo durante la primera importación: un salto
   (horas, si el reloj de la placa estaba mal) daba por «no acabó en 600 s» una importación buena. Ahora con `/proc/uptime` (`segundos`).
4. **Comentarios y documentos**: `initramfs/init` decía «la rocola lo borra cuando lleva 3 minutos funcionando» (ahora, 2 minutos
   seguidos de `rocola-xp`); `INTEGRACION_ROCOLA_XP.md`: lo grande de `rocola-xp` incluye el respaldo diario (`DatosGrandes.cs` de
   xp-datos, c9c8aa7), el instalador, la SIGHUP y `/proc/uptime`.

### Lo comprobado (repetido aquí, máquina de pruebas)

- **La decisión del dueño, entera**: el lanzador abre siempre `rocola-xp` (sin interruptor ni respaldo; `rocola_programa.txt` se ignora y
  no se borra); `rocola-consola` ni se compila ni se baja; Spotify se queda en el defconfig. La `.config` de Buildroot 2026.02.3 de
  f62d6cb y la de ahora solo difieren en `BR2_PACKAGE_ROCOLA_CONSOLA=y` → `BR2_PACKAGE_ROCOLA_XP=y` (nada más se pierde: DejaVu, jpeg,
  ALSA y libdrm siguen). `git diff f62d6cb` vacío en `sistema/rocola/Xp`, `Consola`, `Motor`, `AGENTS.md`, `PORT_JUNTAS.md`, `.github` y
  `sistema/casa`; los 9 commits del grupo con el autor de f62d6cb y las dos líneas finales.
- **La regla del apagado**: SIGTERM solo en `00000080` y «apagar ya»; SIGUSR1 en la consola, «reiniciar ya», `rocola-sonido`,
  `rocola-prueba` (`timeout -s USR1`), `S99rocola` sin confirmar y «reiniciar_equipo ya» (y, con el arreglo 1, de verdad les llega).
- **El contrato** es el de la tarea, palabra por palabra (`ROCOLA_DATOS=/rocola/datos/xp /usr/lib/rocola-xp/rocola-xp --importa-xp
  /run/rocola-datos-xp`, antes de cada apertura, y después los `dir=`); el importador de verdad acepta el archivo (`CarpetaXpDe`).
- **BusyBox del sistema** (v1.37.0, `salida-qemu/target`): no trae `stat` (lista de applets) y su `timeout` acepta `-k`. `sh -n` bien de
  `rocola`, `rocola-buzon`, `rocola-actualiza`, `rocola-sonido`, `rocola-prueba`, `00000080`, `S99rocola`, `rocola-instalar`,
  `rocola-discos`, `rocola-tarjeta`, `initramfs/init` y `dentro.sh`; `bash -n` bien de `rapida.sh` y `prueba.sh`; ningún `stat` en los
  guiones; todos con LF y su modo; `terminal.c` sin avisos con `-Wall`.
- **`rapida.sh decide 6a452567dd25`** (sistema-36): en f62d6cb «rapida»; ahora «completa» por `sistema/Config.in`, el defconfig y los dos
  archivos del paquete. Con «rapida_ya» el paso de `rocola-xp` corre (la base no lo trae): leído en `rapida.sh` y en el flujo.
- **`grep rocola-consola` en `rootfs_overlay`**: 11 líneas, todas comentarios (la historia del cambio): nada lo abre.
- `mpg123` del sistema trae su salida ALSA (`output_alsa.so`); `prueba.mp3` dura unos 3 s.
- **`prueba.sh`** con el importador de verdad (el NativeAOT de xp-datos de las 06:36): en 740f872, **95 BIEN, 0 MAL**; con los arreglos
  2 a 4 (1b56733), **95 BIEN, 0 MAL**; con todo (16 casos, `colgar` incluido): **99 BIEN, 0 MAL** (arranque 11, ia 5, importa 10,
  caidas 7, confirma 4, consola 5, reinicia 8, boton 1, colgar 4, sinprograma 3, sonido 10, prueba 6, s99 3, buzon 11, actualiza 6,
  contrato 5; carga 3 a 4).

### Lo que se deja como está (anotado)

- `rocola-xp --version` (`rocola-actualiza` cada hora, el estado del buzón cada 2 min o más) también se llama `rocola-xp` y lleva
  argumentos: `xp_abierta` solo aparta `--importa-xp`, así que durante sus ~13 ms cuenta como «abierta». Lo peor: la consola o
  «reiniciar ya» le mandan SIGUSR1 y ese guion se queda una vez sin la versión; la confirmación no se engaña (pide 120 s del mismo
  proceso). No se cambió.
- El código 42 vuelve a abrir sin espera (como antes con `rocola-consola`); `rocola-xp` no sale con él.
- Un importador que falla (código distinto de 0) se vuelve a correr en cada apertura de ese encendido (la tarea dice «CADA vez»); el otro
  grupo midió 1 s con 20 000 canciones: no pesa.
- Si el primer arranque no encontrara la carpeta de XP, `rocola-xp` haría sus propias tablas y una importación posterior no las pisaría
  («ya había de rocola-xp, no se tocan»: decisión del importador). En el A4 la carpeta de XP está en el disco instalado: siempre se ve.
- Probar mucho tiempo desde la memoria USB: sin `ROCOLA_IA_DATOS`, `xp/` crece (miniaturas, respaldo, material) y al instalar podría no
  caber en `datos.img` (512 MB; `cp -a` lo dejaría a medias). Pasaba igual con el material de `rocola-consola`; lo apuntó también xp-datos.
- Apagar con la rocola ya cerrada para la consola no borra el estado (ya estaba en «Riesgos»).
- Rocola en casa (`Resumen.cs`) compara con `programa-N` y dice «se confirma a los 3 min»; `LEEME.md` y `ETAPA2.md`, historia: fuera de
  este grupo.
- **Incidente de la revisión**: en un experimento en la máquina (no en la raíz de prueba) se mandó SIGUSR1 por nombre a todos los
  `rocola-xp`, y dos eran de otra prueba (la revisión de xp-datos, `/root/rocola-trabajo/act/xp-datos/revision/aot1`, hacia las 07:07 a
  07:09 UTC): si alguna escena suya salió mal a esa hora, es por esto y hay que repetirla.
