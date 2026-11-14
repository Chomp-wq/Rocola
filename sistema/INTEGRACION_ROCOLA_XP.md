# Rocola OS: el paso al programa traducido de XP (rocola-xp), preparado y APAGADO

13-11-2026 (y 14-11-2026: `--version` y el cierre que conserva el estado, hechos). El programa de Rocola OS de hoy es
`sistema/rocola/Consola` (`rocola-consola`). El dueño pidió rehacerlo traduciendo el de XP (`sistema/rocola/Xp`, `rocola-xp`); el port
nativo ya está terminado (N7 hecho: sin capas). Regla del dueño (8-11-2026): **la versión a medio traducir no entra en Rocola OS**.
Aquí queda preparado todo lo demás para que solo falte activar, compilar el sistema (lo pide el dueño) y probar en el A4.

**Con lo de fábrica nada cambia**: `rocola-xp` no se compila ni entra en el sistema, y el lanzador abre `rocola-consola` igual que
antes (comprobado: ver «Qué se probó»).

## 1. Cómo se compila rocola-xp para el sistema

- `dotnet publish -c Release -r linux-x64 -p:RocolaPerfil=a4` en `sistema/rocola/Xp` (NativeAOT; `RocolaXp.csproj` ya pone
  `IlcInstructionSet x86-64-v2,avx` salvo con `RocolaPerfil=qemu`, como `Consola.csproj`). Hace falta el SDK de .NET 10 y clang, que
  los dos trabajos del flujo del sistema ya instalan. En la máquina de pruebas: 1 min 28 s, sale `rocola-xp` (9,4 MB) y
  `libSkiaSharp.so` (9,0 MB).
- **Bibliotecas, miradas con `readelf -d` y `ldd` en lo publicado**:
  - `rocola-xp` solo enlaza `libc.so.6` y `libm.so.6` (pide glibc 2.38 o menos; el sistema trae 2.42).
  - `libSkiaSharp.so` (paquete `SkiaSharp.NativeAssets.Linux.NoDependencies`): solo glibc (`libpthread.so.0`, `libdl.so.2`, `libm`,
    `libc`; pide glibc 2.29 o menos). **No hace falta HarfBuzz, FreeType ni fontconfig** (van dentro de Skia; el proyecto no usa
    HarfBuzzSharp). Tiene que ir **al lado del programa** (lo busca junto a sí).
  - Las que abre al usarlas (`DllImport`): `libEGL.so.1`, `libGLESv2.so.2`, `libgbm.so.1`, `libdrm.so.2` (Mesa y libdrm),
    `libasound.so.2`, `libmpg123.so.0`, `libmpv.so.2`; y los programas `ffmpeg` (WAV comprimidos) y `amixer` (el volumen al
    arrancar). **Todas están ya en el sistema** (`rocola_a4_defconfig`: Mesa, ALSA, `BR2_PACKAGE_MPV` -Buildroot compila mpv con
    `-Dlibmpv=true`: `libmpv.so.2` está en la imagen-, `BR2_PACKAGE_MPG123`, `BR2_PACKAGE_FFMPEG_FFMPEG`, alsa-utils).
- **No hay ningún paquete nuevo de Buildroot que compilar.** Por eso la activación puede hacerse con la compilación RÁPIDA con la
  casilla «rapida_ya» (ver 3); sin esa casilla, el cambio de configuración pide la COMPLETA (una hora), que también vale.

## 2. Lo que va en el sistema

| Qué | Dónde | Estado |
|---|---|---|
| El paquete de Buildroot | `sistema/package/rocola-xp/` (`Config.in`, `rocola-xp.mk`) | escrito; **no está en `sistema/Config.in` ni en el defconfig**: no se compila |
| El programa y su biblioteca | `/usr/lib/rocola-xp/rocola-xp`, `/usr/lib/rocola-xp/libSkiaSharp.so`, `/usr/lib/rocola-xp/version` | los pone el paquete (o `rapida.sh`) solo al activar |
| El lanzador | `/usr/bin/rocola` | cambiado; sin interruptor hace lo de siempre |
| El botón de encendido | `/etc/acpi/PWRF/00000080` | también cierra `rocola-xp` con SIGTERM y lo espera |
| El buzón | `/usr/bin/rocola-buzon` | el estado mira también el proceso `rocola-xp` |

### El interruptor (uno, apagado de fábrica; no está en Ctrl+F3)

El archivo **`/rocola/datos/rocola_programa.txt`** con la línea `programa=xp`. Sin el archivo, con otra cosa dentro, o si el sistema
no trae `/usr/lib/rocola-xp/rocola-xp`, se abre `rocola-consola` como siempre. Se mira cada vez que se abre el programa (no hace falta
reiniciar el equipo). Líneas opcionales del mismo archivo:

- (nada): los datos de `rocola-xp` en **`/rocola/datos/xp`** (carpeta propia, dentro de lo aprendido).
- `datos=/una/carpeta`: en esa carpeta (por ejemplo `/rocola/disco/rocola-os/xp`, en el disco de la rocola).
- `datos=xp`: en la carpeta de la rocola de XP que eligió `rocola-discos` (`/run/rocola-datos-xp`), si se puede escribir en ella
  (solo si está en el disco donde va instalado Rocola OS; los otros discos van en solo lectura). **No es lo de fábrica**: ver
  «Decisiones del dueño».

**Red de seguridad**: si `rocola-xp` se cae 3 veces seguidas en sus primeros 30 s, el lanzador vuelve a `rocola-consola` hasta el
siguiente encendido (`/run/rocola-xp-caida`) y lo apunta en `rocola_actualizaciones.txt`.

### Con el interruptor encendido, el lanzador abre rocola-xp así

- **Pantalla**: KMS (sin `ROCOLA_PANTALLA`; el mismo `Kms.cs` que el programa actual). **Sonido**: `ROCOLA_ALSA` de `rocola-tarjeta`
  (ya lo exportaba), y el volumen de la tarjeta como siempre.
- **Datos** (`ROCOLA_DATOS`): la carpeta de arriba. Si es propia, la primera vez copia de la carpeta de XP (solo se lee)
  `rocola_teclas.txt` y `rocola_config.txt` (los botones y los ajustes: sin los botones, `rocola-xp` abre la configuración de
  botones, como XP la primera vez; visto aquí) y deja `rocola_de_xp.txt` con lo que copió. **No trae las tablas de lo aprendido**
  (sonoridad, volumen, EQ...): llevan rutas de Windows y `rocola-xp` no tiene importador (pendiente).
- **Música**: XP busca `musica` junto a sus datos o hasta 3 carpetas más arriba (con la carpeta propia: `/rocola/musica`) y las
  carpetas de Ctrl+F2 (líneas `dir=` de `rocola_config.txt`). El lanzador apunta como `dir=` cada carpeta de `/run/rocola-musica`
  (las que encontró `rocola-discos`), una vez cada una. (XP no entra en carpetas que son atajos: una carpeta de enlaces no sirve;
  comprobado.) Con `datos=xp` no se apunta nada: ahí XP encuentra su `musica` como siempre.
- **Letras de XP**: `ROCOLA_LETRAS_XP` = la carpeta `\WINDOWS\Fonts` del disco de Windows, leída tal cual al abrir. No van en el
  repositorio ni en la imagen. (Si no se encuentra, `rocola-xp` mira `/rocola/datos/letras`, la copia que `rocola-discos` ya hacía en
  la rocola, y si no las libres del sistema.)
- **Códigos de salida**: el bucle es el mismo (42, 43, 44, 45, 46). `rocola-xp` no sale con ninguno hoy (son funciones de la fase
  11); sale con 0 al cerrarse (también con el cierre que conserva el estado, SIGUSR1) y el lanzador lo vuelve a abrir.

### Apagado y reinicios (regla del 8-11-2026)

Así lo espera `rocola-xp` (mirado en `Nativo/PantallaRocola.cs` y `Codigo/Form1.Video.cs`, y probado con el programa de verdad):

| Qué pasa | Qué recibe rocola-xp | Estado (`rocola_estado.txt`) |
|---|---|---|
| Botón de encendido, apagado del sistema | SIGTERM = `WindowsShutDown` | **se borra** (sale con 0), como XP |
| Alt+F4 | `UserClosing` | se borra; el lanzador la abre otra vez |
| Apagón | nada | sobrevive: al prender reanuda |
| Consola de mantenimiento (Ctrl+Alt+F2), «reiniciar el programa ya» del buzón | **SIGUSR1** = cierre ordenado «por otra causa» (`TaskManagerClosing`), sale con 0 (20 s; después SIGKILL). Nunca SIGTERM | **se conserva** (y se guarda lo aprendido) |
| «Reiniciar el equipo ya» del buzón | el buzón cierra antes el lanzador y manda **SIGUSR1** (15 s; después SIGKILL), y entonces reinicia | se conserva |
| «Apagar ya» del buzón | el buzón cierra antes el lanzador y manda SIGTERM, lo espera 15 s (como el botón de encendido) y apaga | se borra |

**El cierre que conserva el estado (hecho el 14-11-2026; `sistema/rocola/Xp/PORT_integra-version-y-cierre.md`).** Al recibir
SIGUSR1, `rocola-xp` se cierra por la vía de XP que no es ni el apagado ni Alt+F4: en XP, un `WM_CLOSE` que le llega de fuera a la
ventana (el Administrador de tareas, `taskkill` sin `/F`) = `CloseReason.TaskManagerClosing`. `OnFormClosing` de la rocola
(`Codigo/Form1.Video.cs`, sin tocar) hace entonces todo lo que hace al cerrar (para el sonido y el video, suelta los archivos,
`GuardaTodoAlCerrar`) y, como la razón no es `WindowsShutDown` ni `UserClosing`, **no** llama a `BorraEstado`: espera a que el último
estado llegue al disco (`WaitDataWrites`). Con una hoja abierta (Ctrl+F3, Ctrl+F2) se cierra igual: antes su hoja con
`FormOwnerClosing`. Sale con **0**. Medido aquí (fuera de pantalla, sin música o con un video): 30 a 90 ms. `rocola_estado.txt`
queda y al abrir reanuda (los créditos; con un video sonando, la línea `sonando=`).

- Si SIGUSR1 le llega **antes de estar lista** (los primeros instantes del arranque: antes de abrir la pantalla, o con el aviso de la
  carpeta de datos puesto), la corta en seco como antes (sale con 138): es como un apagón, el estado también sobrevive.
- El lanzador no cambia de lógica: quien cierra deja antes su marca (`rocola-en-consola`, `rocola-xp-reinicio`) y el bucle la mira
  antes que el código de salida, así que ni el 0 ni el 138 cuentan como caída, y los dos quedan apuntados en la bitácora. Añadido: si
  a los 20 s no se cerró y hay que cortarla (SIGKILL), también queda apuntado («rocola-xp no se cerró en 20 s: se cortó»).
- SIGTERM, Alt+F4, Ctrl+Alt+Q y kill -9 siguen exactamente igual (comparados con el programa de antes del cambio).

Añadido en la revisión (13-11-2026), probado con programas falsos (`prueba.sh buzon` y `xp`):

- Un `reboot` o `poweroff` directo le llega a `rocola-xp` como SIGTERM y el sistema solo espera 1 s antes de cortar. Por eso las
  órdenes «ya» del buzón cierran antes `rocola-xp` como dice la tabla (sin `rocola-xp` abierta el buzón hace lo de siempre).
  **Queda sin cubrir**: el reinicio de `S99rocola` cuando un sistema nuevo en prueba no llega a confirmarse, y un `reboot` escrito a
  mano: ahí `rocola-xp`, si está abierta, recibe SIGTERM y borra el estado (si le da tiempo).
- Si `rocola-xp` llega a abrirse justo al cambiar a la consola de mantenimiento, el vigilante la cierra igual (antes se quedaba
  pintando encima de la consola).
- El cierre de «reiniciar el programa ya» no cuenta como caída (no suma para las 3 seguidas) y queda apuntado en la bitácora.

## 3. La compilación rápida y los flujos

- `sistema/rapida.sh` (cambiado, sin efecto hoy): con `BR2_PACKAGE_ROCOLA_XP=y` en el defconfig, compila `rocola-xp` y lo pone en
  `/usr/lib/rocola-xp` (paso 5b) si cambió `sistema/rocola/Xp` (o `Gl.cs`, `Kms.cs`, `Botones.cs`, que toma del programa actual) o
  la base no lo trae. Sin esa línea no hace nada. El paquete `package/rocola-xp/` no pide la completa mientras esté apagado
  (probado: `rapida.sh decide` dice «rapida» con estos cambios, y «completa» al añadir la línea).
- Los flujos (`rocola-sistema.yml`, `rocola-programa.yml`) **no se tocaron** y no hace falta tocarlos para activar: los dos trabajos
  del sistema ya tienen .NET 10 y clang.
- Sitio: `rocola-xp` añade unos 18 MB; la imagen del sistema de pruebas tiene 790 MB libres.
- **Para que rocola-xp se actualice solo como el programa actual** (no hecho; cambia lo publicado): en `rocola-programa.yml`,
  publicar también `rocola-xp-a4.gz` y `libSkiaSharp.so.gz` con sus sumas; en `rocola-actualiza`, bajarlos a
  `/rocola/datos/programa/xp/`; en `/usr/bin/rocola`, preferir el bajado si es más nuevo. Necesita antes el `--version` (ver 4).
  Hasta entonces `rocola-xp` solo cambia con una compilación del sistema (la rápida, unos minutos).

## 4. `--version`

`/usr/bin/rocola`, `rocola-actualiza` y `rocola-buzon` leen de `rocola-consola --version` una línea como
`rocola-consola programa 57 (abc1234) compilado 1762500000` con: `s/.*programa \([0-9]*\).*/\1/`, `s/.* compilado \([0-9]*\).*/\1/`
y `grep -E "programa N( |$)"`.

- **Hecho (14-11-2026)**: `rocola-xp --version` escribe UNA línea, `rocola-xp programa N (COMMIT) compilado SEGUNDOS`, y sale con 0
  **antes** de la instancia única, los datos y la pantalla (`Nativo/VersionRocolaOs.cs`: al cargarse el programa, antes de entrar al
  `Main` de XP, que no se tocó). Con las mismas propiedades que `Consola.csproj` (`-p:RocolaVersion=N -p:RocolaCommit=abc1234`;
  sin ellas, `0` y `local`; `RocolaFecha` = cuándo se compiló, como `AssemblyMetadata`). Probado en JIT y NativeAOT (13 ms en
  NativeAOT): las tres expresiones de los guiones leen lo suyo, no crea la carpeta de datos, no abre la pantalla y contesta también
  con otra rocola abierta en los mismos datos. No cambiar ese formato sin cambiar los tres scripts.
- Nada de lo preparado lo llama todavía: el lanzador sigue apuntando `/usr/lib/rocola-xp/version`, que escribe la compilación
  (paquete y `rapida.sh`, con «programa 0 (sistema)»). **Pendiente, cuando se quiera** (hace falta para que `rocola-xp` se
  actualice solo, apartado 3): que el paquete y `rapida.sh` compilen con `-p:RocolaVersion`/`-p:RocolaCommit` y que el lanzador
  apunte y compare con `"$XPBIN" --version` (la función `compilado` ya sirve tal cual).

## Cómo se activa (cuando N7 acabe y el dueño lo pida)

1. Añadir a `sistema/Config.in`: `source "$BR2_EXTERNAL_ROCOLA_PATH/package/rocola-xp/Config.in"`.
2. Añadir a `sistema/configs/rocola_a4_defconfig`: `BR2_PACKAGE_ROCOLA_XP=y`.
3. Compilar el sistema (solo a pedido del dueño): la completa, o la rápida con «rapida_ya».
4. En la rocola, ya con ese sistema: en la consola de mantenimiento (Ctrl+Alt+F2),
   `echo programa=xp > /rocola/datos/rocola_programa.txt` y volver con Ctrl+Alt+F1. Para volver al programa actual: borrar ese
   archivo (o esperar: si se cae 3 veces seguidas vuelve solo).

Los pasos 1 a 3 solo meten el programa en el sistema; no cambia nada en la rocola hasta el paso 4.

## Lo que falta tras N7, en orden

1. ~~N7 acabado y `rocola-xp` comprobado sin capas (del port).~~ Hecho (13-11-2026).
2. ~~En `rocola-xp`: `--version` (apartado 4) y el cierre que conserva el estado al recibir SIGUSR1.~~ Hecho (14-11-2026), probado
   fuera de pantalla en JIT y NativeAOT; no en el sistema entero ni en el A4.
3. Activar la compilación (pasos 1 a 3) y probar en QEMU con el sistema entero: que abre por KMS, lee `/dev/input`, suena.
4. **En el A4**, con el interruptor: pantalla, botones, sonido por la salida del amplificador, un video, las letras de XP, apagar
   con el botón, un apagón, Ctrl+Alt+F2 y volver. Medir la fluidez allí.
5. Decidir los datos (abajo) y, si se quieren las tablas de XP, el importador con rutas de Linux (lo que hace `Motor/ImportaXP.cs`
   para el programa actual).
6. **Fase 11**: lo propio de Rocola OS encima de la traducción.

### Lo que se pierde al activar, hasta la fase 11

`rocola-xp` es la rocola de XP tal cual. Lo que el programa actual tiene y él todavía no:

- Spotify entero (el modo, las portadas, las letras, el volumen), el buscador flexible y por letra, los artistas de Spotify.
- La hoja de Internet y el estado de la conexión; «Actualizarse sola» y «Buscar actualización ahora» en Ctrl+F3.
- **Actualizaciones**: `rocola-actualiza` sigue corriendo, pero el programa que baja es `rocola-consola` (no se usa con el
  interruptor) y `rocola-xp` no reinicia «cuando esté libre»: un sistema nuevo ya bajado entra en el siguiente encendido.
- **Buzón / Rocola en casa**: el estado, los registros y los archivos siguen (son del script). No se atienden las órdenes «cuando
  esté libre» (reiniciar programa o equipo, apagar, diagnóstico); las «ya» sí. No hay `/run/rocola-vivo.txt` (lo que suena ahora).
  Los archivos que se piden se buscan en `/rocola/datos`, no en la carpeta de `rocola-xp`.
- Ctrl+F3 -> Detalles -> «La rocola» (reiniciar, apagar, volver a Windows XP, reimportar), el ratón propio de Rocola OS (el de XP
  sí está), el cielo por hora y el clima, color vivo, movimiento, vidrio y letra, las mejoras visuales V.
- Devolver el crédito de un archivo dañado, convertir los «MP3» que no lo son, entrenar la IA en reposo, el volumen de Spotify.
- Lo aprendido por el programa actual en `/rocola/datos` no lo usa `rocola-xp` (ni al revés).
- Con la carpeta propia, una carpeta de `/run/rocola-musica` que el dueño quite con Ctrl+F2 vuelve a apuntarse en el siguiente
  arranque del programa (el lanzador la apunta siempre que falte).

Sí están, porque son de XP: la cadena de sonido entera, el modo billar, el modo noche, los videos, Ctrl+F3 y Ctrl+F2 de XP.

## Decisiones del dueño

1. **Dónde guarda sus datos rocola-xp.** De fábrica, carpeta propia (`/rocola/datos/xp`), empezando solo con los botones y los
   ajustes de XP. La otra opción, `datos=xp`, usa la carpeta de la rocola de XP: tendría sus ajustes y XP y Rocola OS compartirían
   créditos y estadísticas, pero `rocola-xp` escribiría rutas de Linux en tablas que XP lee con rutas de Windows (la biblioteca se
   reharía en cada cambio y lo medido no casaría en ninguno de los dos sentidos), con la rocola de XP todavía como la del bar. No
   se probó con datos reales de XP. Por eso no es lo de fábrica.
2. **Si la carpeta propia va en lo aprendido (512 MB) o en el disco de la rocola** (`datos=/rocola/disco/rocola-os/xp`): XP
   guarda ahí miniaturas, respaldos y el material de la IA, que pueden pasar de 512 MB.
3. **Si se traen las tablas de lo aprendido de XP** (hace falta el importador) o `rocola-xp` vuelve a medir la biblioteca.
4. **Cuándo se cambia en el bar**, sabiendo lo que se pierde hasta la fase 11 (lista de arriba), o si se espera a la fase 11.
5. **Cómo se enciende el interruptor**: hoy solo desde la consola de mantenimiento. Se puede añadir una orden de Rocola en casa o
   un archivo en una memoria USB.
6. **Si rocola-xp se actualiza solo** por el canal del programa (apartado 3) o solo con el sistema.
7. **La compilación del sistema** para activarlo (completa o «rapida_ya») la pide el dueño.

## Qué se probó y qué no

Probado aquí (máquina de pruebas, no el A4):

- (14-11-2026, `--version` y el cierre con SIGUSR1) `Pruebas/Ventanas/guiones/cierre_rocola_os.sh` con el `rocola-xp` de verdad
  fuera de pantalla, en JIT y en NativeAOT: 36 comprobaciones de 36 en cada uno (`--version`; SIGUSR1 con la rocola sola, con
  Ctrl+F3, con Ctrl+F2 y con un video: `TaskManagerClosing`, sale con 0, `rocola_estado.txt` conservado, al abrir reanuda, y
  entonces SIGTERM lo borra). SIGTERM, Ctrl+F3 con SIGTERM, Alt+F4, Ctrl+Alt+Q y kill -9: iguales que con el programa de antes del
  cambio. `prueba.sh` del lanzador entera con el BusyBox del sistema: sin interruptor IGUAL (lo que recibe el programa, el registro y
  la bitácora); con interruptor, el programa falso ahora atiende SIGUSR1 como el de verdad (sale con 0), y el caso nuevo `cierre`:
  cuatro cierres seguidos (dos por la consola, dos de «reiniciar ya») ordenados (0) o cortados (138) no cuentan como caída y quedan
  en la bitácora; uno que no se cierra se corta a los 20 s y queda apuntado.

- (Revisión independiente, 13-11-2026) Repetido: la compilación NativeAOT con el perfil del A4 (ningún aviso IL), `readelf -d` y
  `readelf -V` de `rocola-xp` (libm, libc; glibc 2.38) y de `libSkiaSharp.so` (libpthread, libdl, libm, libc; glibc 2.29), los nombres
  de las bibliotecas que abre por `DllImport` (los siete de arriba) y que están en `salida-qemu/target`; con el `rocola-xp` de verdad
  fuera de pantalla, SIGTERM sale con 0 y borra `rocola_estado.txt` y SIGUSR1 lo deja (sale con 138); `prueba.sh` entera con el
  BusyBox del sistema (sin interruptor: IGUAL al lanzador de 7113804 en lo que recibe el programa, el registro y la bitácora).

- `rocola-xp` compila en NativeAOT con el perfil del A4 (15 avisos del compilador -CA, CS, SYSLIB-, ninguno de NativeAOT -IL-; 1 min 28 s). Sus bibliotecas, con
  `readelf`/`ldd`, contra lo que trae el sistema compilado (`salida-qemu/target`).
- El `rocola-xp` de verdad **corre con las bibliotecas del sistema** (dentro de la raíz del sistema de QEMU: glibc 2.42, Mesa
  softpipe), fuera de pantalla: abre, lee la carpeta de música apuntada con `dir=` y escribe sus datos donde dice `ROCOLA_DATOS`.
- Las señales con el programa de verdad (tabla de arriba). (El 13-11-2026 `--version` abría la rocola y SIGUSR1 la cortaba con 138:
  ya no, ver la entrada del 14-11-2026.)
- El lanzador con programas falsos y el **BusyBox del sistema** en su propio espacio de procesos
  (`sistema/rocola/Pruebas/Lanzador/prueba.sh`): sin interruptor hace **lo mismo que el lanzador de antes** (lo que se le pasa al
  programa, el registro y la bitácora, iguales: arranque, salida con 42 y la consola de mantenimiento); con interruptor: entorno,
  datos, botones y `dir=` (sin repetir), la consola con SIGUSR1 y la vuelta, «reiniciar ya», el botón de encendido con SIGTERM, tres
  caídas -> programa actual, `datos=xp` (con y sin poder escribir), `datos=/carpeta`, y el interruptor en un sistema sin `rocola-xp`.
- `sh -n` con el BusyBox del sistema en los tres scripts; `bash -n rapida.sh` y `rapida.sh decide` (apagado: «rapida»; activado:
  «completa»). No había shellcheck.

No probado:

- **Nada en el A4.** Ni en QEMU con el sistema entero (pantalla KMS, `/dev/input`, sonido).
- El paquete de Buildroot (`make rocola-xp`) y el paso 5b de `rapida.sh arma` (no había sitio en el disco de pruebas para armar una
  imagen); son las mismas órdenes que se corrieron a mano.
- El video (`libmpv.so.2` del sistema: solo se vio que está y que trae la API de dibujo) y `libmpg123` con `rocola-xp` dentro del
  sistema.
- Las letras de XP leídas de un disco de Windows de verdad; `rocola_teclas.txt` y `rocola_config.txt` reales de XP.
- El cambio del botón de encendido y el del estado del buzón, fuera de `sh -n`; lo del buzón con las órdenes «ya» solo como trozo
  suelto con programas falsos (`prueba.sh buzon`), no con el buzón entero ni con un reinicio de verdad.
- Cuánto tarda `rocola-xp` en cerrarse con SIGTERM o SIGUSR1 mientras suena música por una tarjeta de verdad (aquí, sin tarjeta:
  30 a 90 ms, también con un video). El cierre de XP puede esperar hasta 15 s + 5 s a que las escrituras lleguen al disco
  (`WaitDataWrites`); el lanzador corta a los 20 s y el buzón a los 15 s (si pasa, el estado queda como tras un apagón).
- SIGUSR1 con el sistema entero (el `killall -USR1` del lanzador y del buzón de verdad) y en el A4.
