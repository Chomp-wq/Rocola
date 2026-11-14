# Rocola OS con la rocola traducida de XP (rocola-xp): ACTIVADA, el único programa

14-11-2026. **Decisión del dueño**: «Lo ideal es la que acabamos de traducir, la otra desecharla y si la que traducimos falla,
arreglarla y pulirla». Desde ahora `rocola-xp` (`sistema/rocola/Xp`: la rocola de XP traducida a Linux, el port nativo terminado) es
**el único programa de Rocola OS**. `rocola-consola` (`sistema/rocola/Consola` y `sistema/rocola/Motor`) se retira: el lanzador ya no
lo abre nunca (ni como respaldo: si `rocola-xp` falla, se arregla `rocola-xp`), el sistema ya no lo compila y la rocola ya no lo baja.
Su código se queda en el repositorio solo como referencia para la fase 11 y para lo que `rocola-xp` toma de él (`Gl.cs`, `Kms.cs`,
`Botones.cs`). Y «traer lo aprendido de XP»: `rocola-xp` empieza en su carpeta propia (`/rocola/datos/xp`) con lo que la rocola de XP
ya aprendió, con las rutas de Linux.

Hasta aquí: activado en la configuración, los guiones y la compilación rápida, y probado con programas falsos y el BusyBox del sistema
(«Qué se probó»). **Falta**: compilar el sistema (la rápida con «rapida_ya»: la lanza el coordinador después de probar el sistema
entero en QEMU), y todo lo del A4. Lo propio de Rocola OS que `rocola-xp` aún no tiene es la fase 11 (lista abajo).

Las piezas, por grupo: el sistema y los guiones (`sistema/ACT_sistema-activa.md`); el importador y el material de la IA en `rocola-xp`
(`Nativo/ImportaXp.cs`, `Nativo/DatosGrandes.cs`: el otro grupo de esta tanda); `--version` y el cierre con SIGUSR1
(`sistema/rocola/Xp/PORT_integra-version-y-cierre.md`).

## 1. La compilación

- **`configs/rocola_a4_defconfig`**: `BR2_PACKAGE_ROCOLA_XP=y`; `BR2_PACKAGE_ROCOLA_CONSOLA` ya no está (no se compila);
  `BR2_PACKAGE_ROCOLA_SPOTIFY=y` se queda (lo usará la fase 11; hoy `rocola-xp` no lo abre). `sistema/Config.in` lee el menú de
  `rocola-xp` y sigue leyendo el de `rocola-consola` (el paquete existe, apagado, como referencia; no hace falta para que la
  configuración valga). Comprobado con Buildroot 2026.02.3 (`make rocola_a4_defconfig` en una carpeta aparte): todas las líneas del
  defconfig quedan en `.config`, `BR2_PACKAGE_ROCOLA_XP=y`, `# BR2_PACKAGE_ROCOLA_CONSOLA is not set`, `BR2_PACKAGE_ROCOLA_SPOTIFY=y`.
- **El paquete** (`package/rocola-xp/rocola-xp.mk`): `dotnet publish -c Release -r linux-x64 -p:RocolaPerfil=a4` en
  `sistema/rocola/Xp` (NativeAOT; `RocolaXp.csproj` pone `IlcInstructionSet x86-64-v2,avx`, salvo con `RocolaPerfil=qemu`), con
  `-p:RocolaVersion=N -p:RocolaCommit=C -p:RocolaFecha=S`: N = el número del sistema (`ROCOLA_SISTEMA_VERSION`, el que pone GitHub;
  en casa, 0), C = el commit (`GITHUB_SHA`, o `git`; si no, «local»), S = cuándo. Pone:
  - `/usr/lib/rocola-xp/rocola-xp` y a su lado `/usr/lib/rocola-xp/libSkiaSharp.so` (la busca junto a sí);
  - `/usr/lib/rocola-xp/version`: lo que contesta `rocola-xp --version` («rocola-xp programa N (C) compilado S»); si dice otra cosa que
    lo esperado, la compilación falla; si el equipo que compila no puede correrlo (otra glibc, sin AVX), se escribe la línea esperada;
  - la canción de prueba que ponía `rocola-consola`: `/usr/share/rocola/prueba.mp3` (la usa `rocola-prueba`) y el disco
    `/rocola/musica/Rocola OS - Prueba/prueba.mp3`, y la carpeta `/rocola/datos`.
- **Bibliotecas** (miradas con `readelf -d` y `ldd` en lo publicado, 13-11-2026): `rocola-xp` solo enlaza `libc.so.6` y `libm.so.6`
  (pide glibc 2.38 o menos; el sistema trae 2.42); `libSkiaSharp.so` solo glibc (2.29 o menos; sin HarfBuzz, FreeType ni fontconfig).
  Las que abre al usarlas: `libEGL.so.1`, `libGLESv2.so.2`, `libgbm.so.1`, `libdrm.so.2`, `libasound.so.2`, `libmpg123.so.0`,
  `libmpv.so.2`, y los programas `ffmpeg` y `amixer`. **Todas están ya en el sistema**; el menú de `rocola-xp` pide además ALSA,
  libdrm, mpg123 y DejaVu (las letras de repuesto si no están las de XP), que el defconfig ya traía.
- **La compilación rápida** (`sistema/rapida.sh`): ya no compila ni pone `rocola-consola` (antes, su paso 5); compila `rocola-xp`
  (perfil del A4, con el número y el commit de ese sistema) si cambió `sistema/rocola/Xp` (o `Gl.cs`, `Kms.cs`, `Botones.cs`) o si la
  base no lo trae, y pone lo mismo que el paquete (programa, biblioteca, `version` con lo que dice `--version`, canción de prueba).
  - `rapida.sh decide` con la base de hoy (sistema-36, completa, commit 6a45256): antes de esta tanda decía «rapida»; ahora dice
    «completa» por `sistema/Config.in`, `configs/rocola_a4_defconfig`, `package/rocola-xp/Config.in` y `package/rocola-xp/rocola-xp.mk`.
  - **Con «rapida_ya»** (lo que se va a lanzar): el flujo (`.github/workflows/rocola-sistema.yml`) pone `modo=rapida` y `RAPIDA_YA=1`;
    `rapida.sh arma` sigue aunque haya cambios que piden la completa, y su paso 5 corre porque la configuración ya pide `rocola-xp` y
    sistema-36 no trae `/usr/lib/rocola-xp/rocola-xp`. Lo que **no entra hasta la siguiente completa**: el cambio de la configuración en
    sí (en la imagen de la rápida **sigue `/usr/bin/rocola-consola` de sistema-36, sin usarse**: nadie lo abre ni lo baja) y el menú de
    Buildroot. Nada de eso hace falta para que la rocola funcione con `rocola-xp`.
  - Las pruebas de arranque (de `rapida.sh` no hay; la del flujo mira «Run /sbin/init» en QEMU) no nombran `rocola-consola`: no hubo que
    cambiarlas. Los flujos no se tocaron: los dos trabajos del sistema ya instalan .NET 10 y clang.
  - Sitio: `rocola-xp` añade unos 18 MB; la imagen de pruebas tenía 790 MB libres.

## 2. Lo que va en el sistema

| Qué | Dónde |
|---|---|
| El programa, su biblioteca y su versión | `/usr/lib/rocola-xp/` (paquete `rocola-xp` o `rapida.sh`) |
| La canción de prueba | `/usr/share/rocola/prueba.mp3`, `/rocola/musica/Rocola OS - Prueba/` (paquete `rocola-xp`) |
| El lanzador | `/usr/bin/rocola` (abre siempre `rocola-xp`) |
| Lo demás que nombra a la rocola | `rocola-actualiza`, `rocola-buzon`, `rocola-sonido`, `rocola-prueba`, `/etc/acpi/PWRF/00000080`, `/etc/init.d/S99rocola` |
| Lo de antes, sin usar | `/usr/bin/rocola-consola` solo en una imagen hecha con la rápida sobre sistema-36 (hasta la siguiente completa) |

## 3. El lanzador (`/usr/bin/rocola`)

Lo abre `S99rocola` en tty1 (después de `rocola-discos`; la primera vez, después de `rocola-prueba`). Abre **siempre**
`/usr/lib/rocola-xp/rocola-xp`. Ya no hay interruptor: `/rocola/datos/rocola_programa.txt`, si está, se ignora (no se borra; queda
una línea en `rocola.txt`). Ya no hay nada de `rocola-consola`: ni el programa bajado (`.nueva`, `.anterior`, `.superada`, `.caida`:
lo que hubiera en `/rocola/datos/programa` se queda como está), ni la vuelta a él tras tres caídas, ni la opción `datos=`.

Cada vez que abre la rocola, en este orden:

1. Si está en la consola de mantenimiento, espera a que se vuelva a la 1 (Ctrl+Alt+F1).
2. El volumen de la tarjeta (como siempre) y la tarjeta: `ROCOLA_ALSA` se vuelve a elegir con `rocola-tarjeta` **cada vez** (antes
   una vez al prender: `rocola-sonido usar N` no valía hasta reiniciar el equipo).
3. **Lo aprendido de XP** (el contrato con el importador): si `rocola-discos` encontró la carpeta de la rocola de XP
   (`/run/rocola-datos-xp`), `ROCOLA_DATOS=/rocola/datos/xp /usr/lib/rocola-xp/rocola-xp --importa-xp /run/rocola-datos-xp`. Sale
   enseguida si ya importó esa carpeta; 0 = bien; otro código se apunta en `rocola.txt` (cada vez) y en la bitácora (una vez por
   código) y la rocola se abre igual. Tope de 10 minutos (la primera vez busca miles de archivos): si pasa de 3 s, en la pantalla sale
   «Rocola OS: trayendo lo aprendido por la rocola de XP (solo la primera vez; puede tardar unos minutos)...» y se borra al acabar; si
   llega al tope se corta (SIGTERM y, 10 s después, SIGKILL), se apunta y en ese encendido ya no se vuelve a intentar. El estado del día
   (`rocola_estado.txt`) no se trae, y en la carpeta de XP no se escribe (`Nativo/ImportaXp.cs`). Sustituye a la copia de
   `rocola_teclas.txt` y `rocola_config.txt` que hacía el lanzador cuando estaba apagada.
4. **La música**: cada carpeta de `/run/rocola-musica` (las de `rocola-discos`) como línea `dir=` de `/rocola/datos/xp/rocola_config.txt`,
   una vez cada una (después del importador, que puede traer la configuración de XP con sus `dir=` traducidas). XP busca además `musica`
   junto a sus datos o hasta 3 carpetas más arriba: `/rocola/musica` (ahí está el disco de prueba).
5. Si mientras tanto se fue a la consola, vuelve a esperar (no se abre encima de la consola).
6. **La rocola**: `ROCOLA_DATOS=/rocola/datos/xp`, `ROCOLA_LETRAS_XP` = la carpeta `\WINDOWS\Fonts` del disco de Windows (leída tal
   cual; si no está, `rocola-xp` mira `/rocola/datos/letras` y después las libres del sistema), `ROCOLA_ALSA`, y la pantalla por KMS.

Además, una vez al prender: **`ROCOLA_IA_DATOS=/rocola/disco/rocola-os/ia-xp`** (se crea si falta) solo si Rocola OS va instalado en el
disco y ese disco está montado con escritura; si no (la memoria USB, o el disco en solo lectura), sin la variable. Ahí `rocola-xp` deja
lo grande que no cabe en lo aprendido (512 MB): el material para entrenar la IA y las miniaturas de las portadas
(`Nativo/DatosGrandes.cs`). La carpeta de `rocola-consola` (`ia`) se queda como está.

**Si se cierra o se cae**, se vuelve a abrir. Una caída es salir con un código distinto de 0, o cerrarse sola (con 0) en menos de
10 s; es «seguida» si llega antes de 2 minutos de abrirse. Antes de volver a abrirla espera 2 s, y si se cae seguido 10 s, 30 s y
2 minutos (de la cuarta en adelante), para no quemar el procesador. Cada caída queda en la bitácora
(`/rocola/datos/rocola_actualizaciones.txt`, que también sube el buzón): «rocola-xp se cayó (código 134) a los 0 s, 3 veces seguidas: se
vuelve a abrir en 30 s». Cerrarla con Alt+F4 (en XP la cierra el dueño: borra el estado) no es una caída: 2 s y otra vez. Los cierres
a propósito (consola, «reiniciar ya», la prueba de sonido) tampoco: dejan antes su marca y quedan apuntados.

**Un sistema nuevo se da por bueno** (`/run/rocola-confirmado`, que mira `S99rocola`) solo cuando **`rocola-xp` lleva en pie al menos
2 minutos seguidos**: el mismo proceso abierto todo ese tiempo (el importador, que también se llama `rocola-xp`, no cuenta). Antes
bastaba con verla abierta en un instante a los 3 minutos; con `rocola-xp` como único programa, sin respaldo, eso podía dar por bueno un
sistema cuyo programa se cae en bucle. Ahora ese sistema no se confirma y vuelve solo al anterior (apartado 7).

**Sin `rocola-xp` en el sistema** (no debería pasar): un aviso claro en la pantalla («ROCOLA OS: este sistema no trae el programa de la
rocola...», y cómo volver al sistema de antes) y una línea en la bitácora; el lanzador se va, sin volver a intentarlo. La consola de
mantenimiento sigue.

Lo demás, como estaba: la consola de mantenimiento (Ctrl+Alt+F2 la cierra con SIGUSR1 y no la abre hasta volver a la 1; si en ese
momento se está trayendo lo de XP, eso sigue), «reiniciar el programa ya» del buzón (SIGUSR1, y otra vez), el volumen, los códigos de
salida 42 a 46 (`rocola-xp` hoy no sale con ninguno: son de la fase 11; 42 = otra vez, 43 y 46 = reiniciar el equipo, 44 = apagar,
45 = volver a Windows XP), `/root/rocola.txt`. Se quitaron `ROCOLA_CONVERTIDAS` y `ROCOLA_COPIAS` (eran de `rocola-consola`).

## 4. Apagado y reinicios (regla del 8-11-2026)

| Qué pasa | Qué recibe rocola-xp | Estado (`rocola_estado.txt`) |
|---|---|---|
| Botón de encendido | primero el lanzador; SIGTERM = `WindowsShutDown` (se le esperan 15 s) | **se borra**, como XP |
| Alt+F4 | `UserClosing` | se borra; el lanzador la abre otra vez |
| Apagón | nada | sobrevive: al prender reanuda |
| Consola de mantenimiento (Ctrl+Alt+F2), «reiniciar el programa ya» del buzón | **SIGUSR1** (cierre ordenado «por otra causa», `TaskManagerClosing`; 20 s y después SIGKILL) | **se conserva** |
| «Reiniciar el equipo ya» del buzón | el buzón para antes el lanzador y manda **SIGUSR1** (15 s; después SIGKILL), y reinicia | se conserva |
| «Apagar ya» del buzón | el buzón para antes el lanzador y manda SIGTERM (15 s, como el botón), y apaga | se borra |
| Un sistema nuevo que no se confirma (S99rocola, a los 15 min) | **nuevo**: para el lanzador y manda **SIGUSR1** (20 s; después SIGKILL), y reinicia | se conserva (antes el reboot le mandaba SIGTERM y lo borraba) |
| `rocola-sonido` (la prueba de las salidas, `usar`, `auto`) | si estaba abierta (desde la consola ya no lo está): SIGUSR1; el lanzador la vuelve a abrir | se conserva |
| `rocola-prueba` (la pantalla 20 s, la primera vez) | SIGUSR1 a los 20 s, con una carpeta de datos de prueba que se borra | no toca el de verdad |

El cierre con SIGUSR1 (hecho el 14-11-2026, `PORT_integra-version-y-cierre.md`): `rocola-xp` se cierra por la vía de XP que no es ni el
apagado ni Alt+F4 (`TaskManagerClosing`): hace todo lo que hace al cerrar (para el sonido y el video, guarda lo aprendido) y no llama a
`BorraEstado`; sale con 0. Si la señal le llega antes de estar lista (los primeros instantes) la corta en seco (138): como un apagón,
el estado también sobrevive. Quien la cierra deja antes su marca (`/run/rocola-en-consola`, `/run/rocola-xp-reinicio` con el motivo), y
el lanzador la mira antes que el código de salida: ni el 0 ni el 138 cuentan como caída.

Queda sin cubrir: apagar con la rocola ya cerrada para la consola de mantenimiento (el botón de encendido, o `poweroff` escrito ahí):
`rocola-xp` no está abierta para borrar el estado (se cerró con SIGUSR1 al ir a la consola, que lo conserva), y al prender reanuda los
créditos y la cola (antes igual).

## 5. Actualizaciones (`rocola-actualiza`)

- **El sistema** (`sistema-N`), como siempre: se baja a la otra copia y queda en prueba. `rocola-xp` aún no reinicia sola «cuando está
  libre» (fase 11): el sistema nuevo entra **al siguiente encendido** (en el bar, al día siguiente) o con «reiniciar el equipo ya»
  desde casa. Lo dice así en la bitácora y en su estado.
- **El programa** (`programa-N`): **apagado**. Ese canal era de `rocola-consola`: no se baja ni se pone nada de él; lo que ya hubiera en
  `/rocola/datos/programa` se queda como está. `rocola-xp` llega con el sistema (su número es el del sistema que lo trajo). Vuelve
  cuando el programa traducido se publique por ese canal (que `rocola-programa.yml` compile `rocola-xp` y el lanzador sepa ponerlo).
  `rocola-programa.yml` no se tocó: si se lanza, sigue publicando `rocola-consola`, que la rocola ya no baja.
- El estado («programa rocola-xp N (llega con el sistema); sistema...») lee `rocola-xp --version`.
- «Actualizarse sola» (`actualizar_solo=0` en `/rocola/datos/rocola_config.txt`, el de `rocola-consola`) se sigue respetando: lo que el
  dueño decidió vale. Hoy solo se cambia desde la consola de mantenimiento; buscar a mano, con la orden `buscar_actualizacion` del buzón.

## 6. Rocola en casa (`rocola-buzon`)

- El estado (`rocola/estado.json`): `versiones.programa` = el número de `rocola-xp --version`, y además `programa_linea` (la línea
  entera) y `programa_cual` («rocola-xp»); `programa_nueva` y `programa_malas`, vacíos (no hay canal del programa); `rocola.corriendo`
  y `segundos_abierta` de `rocola-xp` (no del importador); los errores, de `/rocola/datos/xp/rocola_error.txt`.
- **Lo «cuando esté libre» ya no se acepta** (reiniciar_programa, reiniciar_equipo y apagar sin «ya», y el diagnóstico): dependía de
  que el programa mirara `/run/rocola-reinicia-programa` y compañía, y `rocola-xp` no lo hace (fase 11). Antes se quedaba pendiente
  para siempre; ahora contesta por qué y qué mandar («manda «reiniciar_programa ya»...»; el diagnóstico, en la rocola: Ctrl+F3 ->
  Imagen -> Diagnóstico completo, y después «archivo rocola_diagnostico.txt»). Las «ya», como antes; además el lanzador se para
  siempre antes de reiniciar o apagar (también con la rocola cerrada en la consola: que no la abra mientras se apaga).
- Los archivos que se piden (solo bitácoras y tablas; nunca las llaves ni la cuenta de Spotify, con las mismas reglas): primero en
  `/rocola/datos/xp` (es el programa) y, si ahí no está, en `/rocola/datos` (los del sistema y los que dejó `rocola-consola`). Los
  registros llevan también los de `/rocola/datos/xp`, con «xp_» delante.
- **Rocola en casa** (`sistema/casa`, no se tocó) compara `versiones.programa` con la última `programa-N` publicada (de
  `rocola-consola`): dirá que hay una más nueva. Pendiente (no era de este grupo).

## 7. Cómo se vuelve atrás

- **Solo, si el sistema nuevo no sirve**: un sistema nuevo cuyo `rocola-xp` no aguanta 2 minutos seguidos no se confirma; a los
  15 minutos `S99rocola` reinicia (cerrando la rocola con SIGUSR1) y a la tercera vez el initramfs vuelve al sistema anterior y aparta
  el nuevo como malo (no se vuelve a bajar). Unos 30 minutos.
- **A mano, en la rocola**: ESC al prender -> «Rocola OS - la versión anterior del sistema»: queda como el de siempre y el nuevo como
  malo. El sistema anterior (sistema-36 o el que fuera) abre `rocola-consola` como antes, con sus datos de `/rocola/datos`, que
  `rocola-xp` no toca (los suyos van en `/rocola/datos/xp`).
- **En el repositorio**: revertir los commits de esta tanda y compilar el sistema.
- Windows XP sigue en el menú de arranque (ESC), como siempre.

## 8. Lo que se pierde, hasta la fase 11

`rocola-xp` es la rocola de XP tal cual. Lo que `rocola-consola` tenía y él todavía no:

- Spotify entero (el modo, las portadas, las letras, el volumen), el buscador flexible y por letra, los artistas de Spotify.
- La hoja de Internet y el estado de la conexión; «Actualizarse sola» y «Buscar actualización ahora» en Ctrl+F3.
- Las actualizaciones del programa por su canal (apagado: llega con el sistema) y reiniciar «cuando esté libre» (un sistema nuevo
  entra al siguiente encendido).
- Del buzón, las órdenes «cuando esté libre» y el diagnóstico; `/run/rocola-vivo.txt` (lo que suena ahora).
- Ctrl+F3 -> Detalles -> «La rocola» (reiniciar, apagar, volver a Windows XP, reimportar), el ratón propio de Rocola OS (el de XP sí
  está), el cielo por hora y el clima, color vivo, movimiento, vidrio y letra, las mejoras visuales V.
- Devolver el crédito de un archivo dañado, convertir los «MP3» que no lo son, entrenar la IA en reposo.
- Lo aprendido por `rocola-consola` en `/rocola/datos` no lo usa `rocola-xp` (empieza con lo de XP).

Sí están, porque son de XP: la cadena de sonido entera, el modo billar, el modo noche, los videos, Ctrl+F3 y Ctrl+F2 de XP.

## 9. `--version`

`rocola-xp --version` escribe UNA línea, `rocola-xp programa N (COMMIT) compilado SEGUNDOS`, y sale con 0 antes de la instancia única, los
datos y la pantalla (`Nativo/VersionRocolaOs.cs`). La leen `rocola-actualiza` (`s/.*programa \([0-9]*\).*/\1/`) y `rocola-buzon` (el
número y la línea); el paquete y `rapida.sh` la guardan en `/usr/lib/rocola-xp/version`, que el lanzador apunta en `rocola.txt` al
abrirla. No cambiar ese formato sin cambiar esos guiones.

## Qué se probó y qué no

Probado aquí (máquina de pruebas, no el A4), 14-11-2026:

- `sistema/rocola/Pruebas/Lanzador/prueba.sh`: los guiones del sistema tal cual, con el **BusyBox del sistema** (y su curl y su jq) en una
  raíz aparte y su propio espacio de procesos, un `rocola-xp` falso (que también hace de importador), programas de mentira para lo que
  toca el equipo y un GitHub de mentira: el arranque (el importador antes, con el contrato), el material de la IA (con escritura, en
  solo lectura, sin disco), el importador que falla o tarda (el aviso en la pantalla, el tope), las esperas tras caídas (1, 2, 3, 4 s con
  el reloj acortado), la confirmación (no con caídas en bucle ni con el importador; sí tras aguantar seguido), la consola (también a
  mitad de la importación), «reiniciar ya» (ordenado, cortado, sordo), el botón de encendido, un sistema sin `rocola-xp`,
  `rocola-sonido` (desde la consola, con la rocola abierta, `usar` y `auto`, cortado a medias), `rocola-prueba`, la vuelta de
  `S99rocola`, el buzón entero (órdenes, archivos, registros, estado, las tres «ya») y `rocola-actualiza` (el canal del programa
  apagado; un sistema nuevo). Los números, en `sistema/ACT_sistema-activa.md`.
- El contrato con el importador de verdad (el `rocola-xp` NativeAOT del otro grupo, con la glibc del sistema): el lanzador lo llama, sale
  con 0, trae los botones y la configuración, no cambia la carpeta de XP, y la segunda vez dice «ya importado».
- `rapida.sh arma` de verdad con una base de mentira (64 MB) y un `dotnet` de mentira: con «rapida_ya» pone `rocola-xp` con su versión,
  la canción de prueba y el lanzador nuevo, deja el `rocola-consola` de la base, y el `disk.img` lleva ese sistema; sin «rapida_ya», se
  niega. Las órdenes del paquete de Buildroot, tal cual, con el mismo `dotnet` de mentira.
- `sh -n` con el BusyBox del sistema de todos los guiones tocados; `bash -n rapida.sh`; `rapida.sh decide` antes y después.
- La configuración con Buildroot (`make rocola_a4_defconfig` y `printvars` de las órdenes del paquete).

No probado:

- **Nada en el A4.** Ni el sistema entero en QEMU con `rocola-xp` (pantalla KMS, `/dev/input`, sonido, video): lo hace el coordinador
  antes de compilar.
- El paquete de Buildroot compilado de verdad (`make rocola-xp`) y `rapida.sh arma` con la base de verdad y el `dotnet` de verdad (no hay
  sitio en el disco de pruebas para la imagen entera, y la máquina estaba ocupada): la compilación la hace GitHub.
- El importador con datos reales de XP y el tiempo que tarda la primera vez en el A4.
- Cuánto tarda `rocola-xp` en cerrarse con SIGTERM o SIGUSR1 con música sonando por una tarjeta de verdad (aquí, sin tarjeta, 30 a 90 ms;
  el lanzador corta a los 20 s y el buzón a los 15 s).

## Decisiones del dueño que quedan

1. **Cuándo se cambia en el bar** (compilar el sistema y probarlo en el A4), sabiendo lo que se pierde hasta la fase 11.
2. **Si `rocola-xp` se actualiza solo** por el canal del programa (hay que publicarlo en `rocola-programa.yml` y que el lanzador lo
   ponga) o solo con el sistema (hoy).
3. **La fase 11** (regla del 8-11-2026): lo no visual de Rocola OS encima; lo visual, propuestas nuevas que elige el dueño.
