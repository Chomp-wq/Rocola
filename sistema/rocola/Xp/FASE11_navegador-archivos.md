# Fase 11, grupo navegador-archivos: el navegador y el gestor de archivos de la ventana de Ctrl+F4

20-11-2026. Rama `navegador-archivos`, sobre `pantalla-ctrlf4` (2f24a8d: la ventana de Ctrl+F4 con su hoja «Pantalla»;
`FASE11_pantalla.md`). Es la petición del dueño del 20-11-2026 (AGENTS.md: «en la ventana de ctrl f4, debes poner un navegador por si quiero
desarrollar en la misma rocola entrando a Claude, y un gestor de archivos basico solo para lo de la rocola, ahi seria mas facil meter las
keys que se requieran»). **Va después de la compilación del cumpleaños y pide la compilación COMPLETA del sistema** (paquetes nuevos).
**Nada de esto está probado en el A4**, ni con WebKit de verdad (no se compiló aquí: horas y mucho disco), ni con una tarjeta de verdad:
todo fuera de pantalla, con un cog de mentira y una memoria USB de mentira (una carpeta).

Solo con `ROCOLA_OS`; sin él, Ctrl+F4 no hace nada (como XP) y nada de esto corre (comprobado contra la referencia congelada, abajo).

## Qué se ve

La ventana de Ctrl+F4 (la de `FASE11_pantalla.md`, sin tocarla: cada hoja se registra sola con `RocolaOsHojitas.Registra`) lleva ahora tres
pestañas: **Pantalla**, **Navegador** y **Archivos**, con el aspecto y el manejo de las hojas de Ctrl+F3 (las tarjetas, los botones planos,
la hoja nativa con su foco, su tabulador y su ratón). Capturas (fuera de pantalla, NativeAOT, datos y llaves de mentira) en el scratchpad de
la sesión: `archivos-capturas/` (`archivos-datos.png`, `archivos-usb.png`, `archivos-copiada.png`, `archivos-grande.png`, `llave-tapada.png`,
`mostrar-confirma.png`, `llave-mostrada.png`, `nombre-nuevo.png`, `genius-guardada.png`, `borrar-confirma.png`, `archivos-borrado.png`,
`navegador-hoja.png`, `navegador-vuelta.png`, `navegador-sin-cog.png`).

### Navegador

- Tarjeta **NAVEGADOR WEB**: qué es, si el sistema lo trae («Está en el sistema (WPE WebKit con cog)» en verde; si no, en ámbar «No está en
  este sistema: llega con la compilación completa del sistema (WPE WebKit y cog)» y el botón en gris, regla del 16-10-2026), **«Abrir el
  navegador»**, las dos combinaciones para volver, la página con que abre y lo que pasó la última vez («Se cerró con Ctrl+Alt+F1 (8 s
  abierto)»). Con un video sonando el botón va en gris («No con un video sonando: cuando acabe»).
- Tarjeta **LO QUE GUARDA**: dónde (`/rocola/datos/xp/navegador`), cuánto ocupa, el tope, **«Olvidar lo del navegador»** (pide confirmar:
  «Se borran la sesión, las galletas y la caché…») y los tres ajustes.
- **Al abrirlo**, la rocola le presta la pantalla y **la música sigue**; cog sale a pantalla completa con su página de inicio
  (`/usr/share/rocola/navegador/inicio.html`: un campo para escribir la dirección —o lo que se busca, a DuckDuckGo— y atajos a claude.ai, el
  repositorio, Spotify para desarrolladores, Genius y el buscador; nada de fuera: ni scripts ni letras de internet). cog no tiene barra de
  direcciones: por eso la página de inicio.
- **Para volver**: **Ctrl + Alt + F1** cierra el navegador y vuelve a la rocola; **Ctrl + Alt + Inicio** lo abre otra vez en la página de
  inicio (para escribir otra dirección), sin devolver la pantalla entretanto. Ningún botón de la rocola puede dar esas combinaciones (tres
  teclas a la vez, con F1 o Inicio; las placas de botones dan flechas, Intro, Retroceso, cifras y letras, y a lo más Ctrl o Alt sueltos).
  Ctrl+Alt+F1 es, además, «volver a la consola de la rocola» en todo Linux (y el núcleo no hace nada: ya está en la 1).
- **Mientras está abierto, las monedas NO cuentan**: el monedero y el teclado son la misma tecla para la rocola, y cada «5» (o la tecla que
  diga `rocola_teclas.txt`) que se escribiera daría un crédito. Se dice en la hoja. Lo que se escribe en el navegador no le llega a la rocola.

### Archivos

- Arriba, un botón por raíz: **«Datos de la rocola»** y cada **memoria USB** conectada («USB sdb1 (7,5 GB)»), que se miran cada 2 s con la
  hojita a la vista. Debajo, dónde se está (cortado por la izquierda si no cabe) y lo que queda libre en los datos.
- **La lista**: carpetas primero, nombre, tamaño y fecha; las llaves con una llavecita; «.. (subir)»; se elige con el ratón (doble clic
  abre), con las flechas, Inicio, Fin, AvPag y RePag, Intro abre y Retroceso sube; escribir el principio de un nombre salta a él (como el
  Explorador de Windows; se junta lo escrito mientras no pase 1,5 s). Ojo: en esta hoja las monedas SÍ cuentan (como en Ctrl+F3), así que la
  tecla de la moneda no sirve para saltar.
- **Botones**: **Abrir/Entrar/Ver**, **Subir**, **Copiar a los datos** (de una USB, a la carpeta de los datos donde se estuvo por última vez;
  si ya hay uno, pregunta «¿Reemplazar?»), **Renombrar** (pide el nombre), **Borrar** (pide confirmar; solo archivos y carpetas vacías) y
  **Archivo nuevo** (pide el nombre y abre el editor). En una USB solo Ver y Copiar; en los datos, todos menos Copiar.
- **El editor** (una hoja de 920x660, letra de ancho fijo de XP, Courier New, o DejaVu Sans Mono sin las de XP): hasta **64 KB** de texto
  (UTF-8; uno que no lo es se ve pero no se guarda; uno con bytes 0, «no es de texto»); se escribe con el teclado (distribución de EE. UU.,
  como la tabla de XP, con todos los signos: lo que hace falta para una llave), flechas, Inicio, Fin, AvPag, RePag, Retroceso, Suprimir,
  Intro; **Guardar** (o Ctrl+S) lo guarda de un tirón (conserva CRLF, la marca BOM y el salto final si los tenía; las llaves siempre con
  salto final); Escape o «Cerrar» cierra (con cambios sin guardar, pregunta). Con el editor abierto las monedas no cuentan (se escribe).
- **Las llaves**: un archivo cuyo nombre lleva llave, key, api, token, secret, clave, password, contrasena/contraseña, credencial o
  credential, o termina en .pem, o es uno de los conocidos (`rocola_github.txt`, `rocola_buzon.txt`, `rocola_genius.txt`,
  `rocola_spotify_llave.txt`, `rocola_spotify_api.txt`), o está dentro de una carpeta `soloist/` o `spotify/`, **se guarda y se copia con
  permisos 0600** y **en la pantalla se ve TAPADO**: cada renglón con sus 3 primeros y sus 3 últimos caracteres y «•» en medio (con menos de
  16, todo tapado —revisión: con 6 una contraseña de 8 dejaba ver 6—; la misma cantidad de caracteres, para que el cursor caiga donde va). **«Mostrar»** pide confirmar («Se verá entera en la
  pantalla: cualquiera que la mire la puede copiar») y vale hasta cerrar el archivo («Tapar» la vuelve a tapar). La bitácora dice «llave a la
  vista: NOMBRE» y «guardado: NOMBRE (llave, 0600)», nunca lo de dentro.
- **`rocola_spotify.txt` de una USB se copia como `rocola_spotify_llave.txt`**: así trae el dueño la llave de Soloist (SPOTIFY_SOLOIST.md), y
  en los datos `rocola_spotify.txt` es la bitácora de Spotify, que el buzón sí sube.

## Cómo está hecho

| Archivo | Qué lleva |
|---|---|
| `RocolaOs/RocolaOs.Navegador.cs` | La función «navegador» (de fábrica encendida; `navegador=0` la apaga), sus tres claves, la hojita «Navegador», el traspaso de la pantalla (`RocolaOsNavegador.Cede`/`Retoma`), el proceso de `rocola-navegador`, la hoja que no se ve con las dos combinaciones, `RocolaOsConfirma` (la confirmación de «No»/«Sí», que usa también Archivos) |
| `RocolaOs/RocolaOs.Archivos.cs` | La función «archivos» (de fábrica encendida), las raíces (los datos y las USB: detectarlas y montarlas de solo lectura), la regla de no salir (realpath), las llaves, leer y guardar el texto, copiar, renombrar, borrar |
| `RocolaOs/RocolaOs.ArchivosPiezas.cs` | Las dos piezas propias de la hoja nativa: la lista (`RocolaOsListaArchivos`) y el editor (`RocolaOsEditor`), pintadas con SkiaSharp como las de Ctrl+F3 |
| `RocolaOs/RocolaOs.ArchivosHoja.cs` | La hojita «Archivos», pedir un nombre y la hoja del editor |
| `RocolaOs/RocolaOs.ArchivosPrueba.cs` | La orden `rocola-xp --prueba-archivos DATOS USB FUERA` (55 comprobaciones de la regla de no salir, las llaves, copiar, guardar...) |
| `board/rocola/rootfs_overlay/usr/bin/rocola-navegador` | El guion: `hay`, `abre [URL]`, `olvida`, `cuanto` (BusyBox ash, sin `stat`) |
| `board/rocola/rootfs_overlay/usr/share/rocola/navegador/inicio.html` | La página de inicio |
| `configs/rocola_a4_defconfig` | Los paquetes del navegador |
| `rocola-buzon`, `rocola-actualiza` | Toman las llaves también de `/rocola/datos/xp` (donde las deja Archivos); el buzón no sube nada que Archivos tenga por llave |
| `Pruebas/RocolaOs/navegador-archivos/prueba.sh`, `teclas.py` | La prueba (y lo que hay que poner en `ROCOLA_PULSA` para escribir un texto) |

No se tocó la ventana de Ctrl+F4 (`RocolaOs.Ventana.cs`) ni nada de la hoja Pantalla: las dos hojitas se registran solas (órdenes 20 y 30).
Lo único de lo del otro grupo: dos `Tab` más en la parte «teclado» de `Pruebas/RocolaOs/pantalla/prueba.sh` (con tres pestañas, de la
primera a la barra de la resolución hay dos paradas más del tabulador); con eso, la prueba de la hoja Pantalla da 65 de 65 en esta rama.

### Los ganchos (g100-g101)

| Id | Dónde | Qué | Sin `ROCOLA_OS` |
|---|---|---|---|
| g100 | `Nativo/PantallaRocola.Ventana.cs:176` | `Minimizada { get { return false; } }` → `return RocolaOs.NavegadorDelante;` | `false` (solo se pone al abrir el navegador, que necesita `ROCOLA_OS`) |
| g101 | `Nativo/Anfitrion.cs:40` | `Actual = new Anfitrion(s);` → `…; RocolaOs.LaSuperficie = s;` | se guarda y nadie la usa |

`ganchos.py`: TODO BIEN (39 ganchos en 18 archivos). `diff-xp.py --sin-ganchos`: 5 095 / 4 828 / 229, las de la base. En `Codigo/` no
se tocó nada.

### El traspaso de la pantalla (la vía y por qué)

rocola-xp **no se cierra** (si se cerrara, como para la consola de mantenimiento, la música pararía): sigue abierta y sonando, y le presta la
pantalla al navegador.

1. **Cede** (en el hilo de la pantalla): la ventana de la rocola cuenta como **minimizada** (g100). Es lo que hace XP en Windows con la
   ventana minimizada: el panel de la tarjeta deja de pintar (`PuedeDibujar`, `renderSuspendido`) y las portadas dejan de prepararse
   (`Form1.Portadas.cs`); el sonido va en su hilo y no se entera. Además la composición no hace nada (`PantallaRocola.Componer`, el delegado
   que pone el anfitrión, se guarda y se cambia por uno vacío) y la pantalla de KMS se **suelta**: `ISuperficie.Suelta` (`Kms.Suelta`:
   espera el cambio de página pendiente y `drmDropMaster`, lo que ya estaba para ceder la pantalla a mpv). La guarda el anfitrión (g101).
2. Abre `rocola-navegador abre`: cog abre la tarjeta y, como nadie es su amo, el núcleo lo hace amo (DRM master); pone su modo y pinta.
3. Mientras tanto la rocola **sigue leyendo las teclas** (evdev, sin quitárselas a nadie: libinput también las lee) en una hoja suya que no
   se ve, que tira todo salvo Ctrl+Alt+F1 y Ctrl+Alt+Inicio (y no cuenta monedas).
4. Al acabar el guion: **retoma** (`Kms.Recupera`: `drmSetMaster`, y el siguiente cuadro vuelve a poner el modo de la rocola con
   `drmModeSetCrtc`), vuelve a poner los colores de la hoja Pantalla (cog pudo dejar otra tabla de gamma), devuelve la composición y deja de
   estar minimizada.
5. Si la rocola se cierra con el navegador abierto (SIGUSR1 de la consola de mantenimiento, el apagado), `ParaAlCerrar` lo cierra (y espera
   hasta 3 s); si la rocola se cae en seco, el guion lo ve (vigila al dueño cada segundo) y cierra cog: el lanzador vuelve a abrir la rocola,
   que necesita la pantalla.

**udev, Wayland o DRM.** La plataforma DRM de cog necesita libinput, y libinput necesita udev (`BR2_PACKAGE_HAS_UDEV`). **El encargo decía que
Rocola OS no lo trae, pero sí lo trae**: el defconfig del A4 lleva `BR2_ROOTFS_DEVICE_CREATION_DYNAMIC_EUDEV=y` desde la etapa 1 (2f257d7,
29-10-2026), que selecciona `BR2_PACKAGE_EUDEV` y con él `BR2_PACKAGE_HAS_UDEV`; el sistema compilado tiene `/sbin/udevd`, `libudev.so.1` y
`/etc/init.d/S10udevd` (en `salida-qemu/target`), y `ETAPA2.md` ya habla del udev del sistema (renombraba `eth0`). Así que **la vía elegida es
la más corta y la que no cambia nada del arranque: cog por su plataforma DRM, sin Wayland ni compositor**, con el udev que ya corre. Se
descartaron: cog por Wayland con `cage` (añade wlroots, seatd/libseat, un compositor entero y un proceso más, y cage también necesita
libinput y udev: no ahorra nada) y arrancar eudev solo mientras el navegador está abierto (no hace falta: ya arranca con el sistema). Sin la
plataforma Wayland de cog (`# BR2_PACKAGE_COG_PLATFORM_FDO is not set`), que solo serviría con un compositor.

**Lo que se escribe en el navegador no queda en la consola de la rocola**: las teclas también le llegan al núcleo, que las mete en tty1 (la
consola de la rocola: la misma de siempre, lo mismo pasa hoy con lo que se escribe en la rocola). Mientras el navegador está abierto, el guion
le quita el eco y los renglones (`stty -F /dev/tty1 -echo -icanon min 0 time 0`) y al cerrarlo tira lo acumulado y la deja como estaba: una
contraseña escrita en claude.ai no aparece en la consola.

### Los datos del navegador

`/rocola/datos/xp/navegador` (0700): `datos/` (la sesión de WebKit: `XDG_DATA_HOME`), `galletas.sqlite` (`--cookie-jar=sqlite:...`, para que
la sesión quede), `cache/` (`XDG_CACHE_HOME`), `config/`. **Va en lo aprendido** (`datos.img`, 512 MB con todo lo de la rocola), así que hay
**tope**: `navegador_tope_mb=` (128 de fábrica, de 32 a 256): al abrir y al cerrar, si lo pasa —o si en los datos quedan menos de 64 MB
libres—, se borra la caché (la sesión y las galletas se quedan); si aun así lo pasa, se dice en `/run/rocola-navegador.txt` y se abre igual
(«Olvidar lo del navegador» lo borra todo). Lo que dice cog va a `/run/rocola-navegador.txt` (en memoria, 0600, se rehace cada vez; el buzón
no lo sube); la bitácora de la rocola solo dice «cede / abre / cierra (Ctrl+Alt+F1) / se cerro (codigo 0) / retoma», nunca una dirección.

Las claves (en `rocola_os.txt`, con la orden «ajuste» o a mano; valen al abrir el navegador):
- `navegador_inicio=` (vacío de fábrica: la página de inicio de la rocola; o una `https://`, `http://` o `file:///`),
- `navegador_teclado=` (`us` de fábrica: la distribución de XKB del navegador; `latam`, `es`...),
- `navegador_tope_mb=` (128).
`navegador=0` apaga la hojita (en gris diciendo por qué); `archivos=0`, la otra.

### Los paquetes y el tamaño

En `configs/rocola_a4_defconfig`: `BR2_PACKAGE_WPEWEBKIT`, `BR2_PACKAGE_COG` con `BR2_PACKAGE_COG_PLATFORM_DRM` (sin la de Wayland),
`BR2_PACKAGE_COG_PROGRAMS_HOME_URI=""`, `BR2_PACKAGE_LIBSOUP3_SSL` (HTTPS: glib-networking y GnuTLS; los certificados ya están) y
`BR2_PACKAGE_XKEYBOARD_CONFIG` (sin él libxkbcommon no arma el teclado y cog no tendría teclas). Sin `WPEWEBKIT_MULTIMEDIA` (GStreamer entero:
sin video ni audio de las páginas), sin WebDriver ni el sandbox de bubblewrap.

**Comprobado con el Buildroot del proyecto** (en un directorio de salida aparte, borrado al acabar): `make rocola_a4_defconfig` deja todo
puesto (`BR2_PACKAGE_COG_PLATFORM_DRM=y`, `HAS_UDEV=y`, `LIBSOUP3_SSL=y`, `XKEYBOARD_CONFIG=y`, `MESA3D_LEGACY_BIND_WAYLAND_DISPLAY=y` que
pide wpebackend-fdo...), y `make show-info` resuelve las dependencias: cog 0.18.5 (`-Dplatforms='headless,drm'`, depende de dbus, libdrm,
libinput, libgbm, libegl, udev, wayland, wpebackend-fdo y wpewebkit), WPE WebKit 2.50.5, libinput 1.31.1 (sobre el udev de eudev 3.2.14),
xkeyboard-config 2.38, libsoup3 3.6.6, glib-networking 2.76.0, GnuTLS 3.8.13, ICU 78.2. **35 paquetes nuevos del sistema**: cog dbus fontconfig
glib-networking gmp gnutls icu libepoxy libevdev libffi libgcrypt libglib2 libgpg-error libinput libpng libpsl libsoup3 libtasn1 libunistring
libwpe libxkbcommon libxml2 libxslt nettle nghttp2 p11-kit pcre2 sqlite wayland wayland-protocols webp wpebackend-fdo wpewebkit
xkeyboard-config xutil_util-macros; y 20 de la máquina que compila (host-ruby, host-icu, las de X11 para xkbcomp...). **No se compiló**
(WebKit son horas y varios GB; aquí quedaban ~2,5 GB): el tamaño es una **estimación**, no una medida.

**Cuánto crece el sistema (estimado)**: WPE WebKit 2.50 con su Skia ~80-90 MB, los datos de ICU ~30-35 MB, xkeyboard-config ~5 MB y lo demás
(glib, GnuTLS y compañía, libxml2, sqlite, dbus, libinput...) ~20-30 MB: **unos 140-170 MB** (stripped). **Lo que hay**: la imagen del
sistema (`rootfs.ext2`) mide 1 GB fijo (`BR2_TARGET_ROOTFS_EXT2_SIZE="1024M"`); la última compilada aquí (`salida-qemu`, 6-11-2026, con
rocola-consola) usa 233 MB (202 521 bloques libres de 262 144); rocola-xp en NativeAOT mide 13,9 MB más `libSkiaSharp.so` 8,9 MB (medido en
esta rama). Con el navegador quedaría en **~400-450 MB usados de 1 GB**: cabe con holgura. El `sistema.img.gz` que se baja al actualizar
crecerá también (~50-70 MB comprimido, estimado). La memoria: WebKit con claude.ai puede usar 0,5-1 GB de los 3,5 GB del A4 (estimado).

**Pide la COMPLETA**: cambia `configs/`, que `rapida.sh` no sabe poner (lo dice ahora en su cabecera). Con «rapida_ya» entran el guion, la
página de inicio y el programa, pero no cog: la hoja lo dice y el botón queda en gris; nada se rompe. (Comprobado: con el commit,
`rapida.sh decide` contra la base de antes dice «completa».)

### Archivos: lo que alcanza y la regla de no salir

- **Los datos**: la carpeta de datos de rocola-xp (`Form1.CarpetaDatos`: `/rocola/datos/xp`, `ROCOLA_DATOS`) y sus subcarpetas.
- **Las USB**: las particiones de un disco que se puede sacar (`/sys/class/block/<disco>/removable` = 1, o colgado de USB en `/sys`), sin
  loop, ram ni zram; un disco con particiones se usa por sus particiones. Las ya montadas se ven donde están (no la que es `/`, `/boot` ni
  `/rocola/datos*`: Rocola OS arrancado desde esa USB); las que no, se montan aquí `ro,nosuid,nodev,noexec` en `/run/rocola-xp/usb/<partición>`
  y se desmontan al cerrar la ventana. Nunca se escribe en ellas. (Pruebas: `ROCOLA_ARCHIVOS_USB=carpeta[:carpeta...]`, sin montar.)
- **Nunca fuera**: cada ruta se resuelve con `realpath` de libc (los «..» y los enlaces, hasta el final) y tiene que quedar dentro de su raíz
  (la raíz misma o «raíz/...»: una raíz que empieza igual, `datos2`, no vale). Un enlace que sale **no se lista** (se cuenta: «1 enlace sale de
  la carpeta: no se muestra») ni se sigue; uno que se queda dentro sí. Un nombre nuevo es un nombre solo (sin «/», ni «.», ni «..», sin
  caracteres de control, ni espacios al principio o al final, hasta 255 bytes). Solo se abren y se copian **archivos normales** (`stat`: ni tuberías
  ni aparatos ni sockets; revisión del 20-11-2026). Lo que se escribe va a un archivo de paso en la misma carpeta
  (creado ya con sus permisos), `fsync` y cambio de nombre; un destino que es un enlace no se escribe; un enlace se borra él, no a donde apunta.
- **Dónde las esperan las órdenes que ya existen**: las de Spotify (`rocola_spotify_llave.txt`, `rocola_spotify_api.txt`) van en
  `/rocola/datos/xp`, donde ya las leen `spotify llave`, `rocola-soloist`, el buzón y rocola-xp: nada que cambiar. Las de GitHub, del buzón y
  de Genius (`rocola_github.txt`, `rocola_buzon.txt`, `rocola_genius.txt`) esas órdenes las guardan en `/rocola/datos` (fuera de lo que alcanza
  Archivos): ahora `rocola-actualiza` y `rocola-buzon` las buscan también en `/rocola/datos/xp` (después de las USB montadas y la carpeta de XP,
  antes de montar las USB sueltas) y las copian a su sitio como hacían desde una USB (el buzón, en su vuelta de cada 15 minutos; la de Genius,
  solo si falta).
- **El buzón no sube ninguna llave**: su `permitido` dejaba fuera github, buzon, genius, `*llave*`, `*_api.txt*`, `soloist*`; ahora también
  todo lo que Archivos tiene por llave (key, api, token, secret, clave, password, contras..., credencial, .pem, sin distinguir mayúsculas).
  Comprobado: sigue subiendo `rocola_error.txt`, `rocola_os_bitacora.txt`, `rocola_spotify.txt`, `rocola_config.txt`, `rocola_calidad.txt`,
  `rocola_navegador.txt` y no sube `rocola_genius.txt`, `rocola_spotify_llave.txt`, `rocola_spotify_api.txt`, `rocola_mi_token.txt`,
  `rocola_claude_key.txt`, `rocola_secreto.txt`, `rocola_clave.txt`, `rocola_API.txt` ni `rocola_password.txt`.

## Las pruebas

20-11-2026, en la máquina de pruebas compartida (otros agentes compilando y un QEMU de otro grupo corriendo). JIT = `dotnet build -c
Release`; AOT = `dotnet publish -p:PublishAot=true -r linux-x64` (con el cerrojo `fase11/pesado.lock`). **Todo fuera de pantalla con llvmpipe,
un cog de mentira y una USB de mentira; datos y llaves inventados. Nada en el A4, ni con WebKit, ni con una tarjeta o un teclado de verdad.**

| Prueba | Resultado |
|---|---|
| Compilar, JIT | 0 errores, los 13 avisos de siempre |
| Publicar NativeAOT | sale con 0, sin avisos IL/AOT; rocola-xp 13,9 MB |
| Un arnés (`Pruebas/VistasAjustes`, que compila `RocolaOs/`) | 0 errores |
| `ganchos.py` | TODO BIEN (39 ganchos en 18 archivos; `Codigo/` = `fc7b667` devolviéndolos) |
| `diff-xp.py --sin-ganchos` (109 archivos) | 5 095 / 4 828 / 229, las de la base |
| `Pruebas/RocolaOs/navegador-archivos/prueba.sh` | **JIT 51 de 51; AOT 51 de 51** (abajo) |
| Sin `ROCOLA_OS` contra la referencia congelada (la escena «sin» de la hoja Pantalla: Ctrl+F4, moneda y Ctrl+Alt+Q x3, `compara_escena.py`) | **IGUAL** en JIT y en AOT |
| `Pruebas/RocolaOs/pantalla/prueba.sh` (la hoja Pantalla, con las tres pestañas) | JIT: TODO BIEN, 71 de 71 (con los dos `Tab` de más en «teclado»; sin ellos, las 3 de «teclado» fallan: el foco cae en otra pestaña) |
| `make rocola_a4_defconfig` + `make show-info` (Buildroot del proyecto) | todo resuelto (arriba) |

`prueba.sh`, por partes (las mismas en JIT y AOT):
- **orden** (1, con 55 dentro desde la revisión; 52 antes): `rocola-xp --prueba-archivos`: los «..» que salen, los enlaces a una carpeta y a un archivo de fuera, el
  relativo con «..», un archivo de dentro por el enlace de fuera y una raíz que empieza igual: NO; el enlace que se queda dentro: sí; la
  lista sin los enlaces que salen (contados); leer: CRLF, más de 64 KB no, un byte 0 no; guardar: conserva CRLF, una llave nueva 0600, por un
  enlace NO (y lo de fuera sigue igual), por «fuga/x.txt» y por «sub/../../FUERA/y.txt» NO, en la USB NO, un archivo que no es llave conserva
  sus permisos (640); copiar: la llave 0600 e igual, `rocola_spotify.txt` como `rocola_spotify_llave.txt`, «YA_HAY» sin pisar, por el enlace
  de la USB NO, a una carpeta de fuera NO, por el enlace de los datos NO, una carpeta NO, desde fuera de la USB NO; renombrar con «/», «..», a
  un nombre que ya está y en la USB: NO; a un nombre de llave: 0600; borrar: el enlace que sale se borra él (lo de fuera sigue), una carpeta
  con cosas NO, la raíz NO, en la USB NO, con «..» NO; `EsLlave`, `Tapa`, `NombreNoVale`; lo de fuera intacto al final; el teclado del editor.
- **guion** (20): `rocola-navegador` con el BusyBox del sistema (`salida-qemu/target`, en chroot) y un cog de mentira: `sh -n`, sin `stat`;
  `hay` (sin cog sale con 1 y lo dice; con cog, 0); `abre` (cog con la página de inicio, `--platform=drm`, las galletas en sqlite, los
  `XDG_*` en `navegador/`, teclado `us`; la carpeta 0700; el pid en `/run`); otra vez con uno abierto: no; `olvida` abierto: no; SIGTERM: cog
  recibe TERM, sale con 0 y quita el pid; lo de cog a `/run/rocola-navegador.txt` (0600); con URL y `latam`; el tope (una caché de 3 MB con
  tope de 1 MB se borra al abrir; la sesión y las galletas se quedan); `cuanto`; `olvida`; **el dueño se cae: cog se cierra solo (0,1 a 1,2 s según la corrida; el guion mira cada segundo)**;
  cog que se cae al abrir (5): el guion sale con 5; el uso; la página de inicio sin nada de fuera; `rocola-buzon` y `rocola-actualiza` con
  `sh -n`, miran `$XPD`, y `permitido` (arriba).
- **archivos** (11), la rocola entera con Ctrl+F4 y el ratón y el teclado de prueba (`ROCOLA_PULSA`): las 11 capturas; copiar de la USB la
  llave de la API (igual, 0600) y la de Soloist (como `rocola_spotify_llave.txt`, 0600, sin pisar la bitácora); el grande (70 KB) no se abre y
  la USB no cambia; **un archivo nuevo con el nombre y el contenido escritos con el teclado y Ctrl+S: `rocola_genius.txt` =
  «GENIUS-prueba_125=ok/x», 0600, con salto final** (el «5» no se lo comió la moneda); renombrar (`notas.txt` → `notas2.txt`) y borrar con su
  confirmación; lo de fuera intacto; la bitácora; **ninguna llave en las bitácoras, `rocola_error.txt` ni la salida**; `rocola_error.txt` sin
  fallos de Rocola OS.
- **navegador** (14), con el guion de verdad y un cog de mentira, una canción de 60 s y la tarjeta de prueba con reloj
  (`Pruebas/Windows/alsa/reloj.c`): cede, abre (página de inicio, `--platform=drm`); **con el navegador delante la rocola no compone ni un
  cuadro** (las composiciones al ceder y al retomar, iguales: 149 y 149 en JIT, 194 y 194 en AOT; en corridas anteriores 197/197 y 221/221); **la música sigue: 7,5 s de audio en
  7,6 s de reloj (JIT), 7,8 en 7,8 (AOT)** con el navegador delante, y la misma canción, más adelante, al volver; las monedas («5» x3) y las
  letras no llegan a la rocola (créditos 3 → 3 → 3); Ctrl+Alt+Inicio cierra cog y lo abre en la página de inicio sin devolver la pantalla;
  Ctrl+Alt+F1 cierra cog y retoma; **de Ctrl+Alt+F1 a la pantalla de vuelta: 0,23 s (JIT) y 0,11 s (AOT)**; de vuelta, una moneda con la ventana sí cuenta (3 → 7);
  la captura pedida con el navegador delante no se hizo hasta volver; la bitácora sin direcciones; sin fallos.
- **caidas** (4): SIGUSR1 (la consola de mantenimiento) con el navegador abierto: cog recibe TERM y la rocola sale con 0 sin dejar ningún cog;
  SIGKILL a la rocola: el guion lo ve y cierra cog; sin cog: no se abre nada (captura `navegador-sin-cog.png`).
- **sin** (2): sin `ROCOLA_OS`: ni bitácora de Rocola OS, ni traza del navegador, ni cog.

**No se hizo la prueba en QEMU**: el QEMU del proyecto estaba ocupado por otro grupo todo el rato (`act/qemu-pantalla`, un `qemu-system`
corriendo) y en el disco quedaban ~2,5 GB (cada disco de XP de las pruebas de QEMU mide 4,8 GB). Además, sin WebKit compilado no se podía
probar cog de verdad; el traspaso de DRM master con un programa de mentira en QEMU es lo primero que queda por hacer (abajo).

## Lo que queda abierto y lo que hay que comprobar en el A4

- **La compilación completa con WebKit** (horas en GitHub; a pedido del dueño) y medir de verdad cuánto crece el sistema y la descarga.
- **El traspaso de DRM master con la radeon**: que `drmDropMaster` + cog + `drmSetMaster` funcione (cog tiene que poder ser amo; la rocola,
  volver a serlo al cerrarse cog; el modo de la rocola vuelve con el siguiente cuadro); que no quede la pantalla en negro; que los colores de
  la hoja Pantalla vuelvan. En QEMU primero (bochs, con un programa de mentira que abra la tarjeta y ponga un modo), después en el A4.
- **cog en el A4**: que arranque con Mesa r600 por DRM/GBM/EGL, que libinput vea el teclado y el ratón (por el udev de siempre), la flecha del
  ratón (cog dibuja la suya por el plano del cursor), que claude.ai cargue y deje entrar (la sesión queda en `galletas.sqlite`), cuánta
  memoria usa y si la música no tiembla con WebKit trabajando (son dos núcleos; la rocola minimizada no pinta).
- **La distribución del teclado**: el navegador va con `us` de fábrica (`navegador_teclado=`); el editor de Archivos y el resto de la rocola,
  con la de EE. UU. de la tabla de XP. Si el teclado del A4 es latinoamericano, `navegador_teclado=latam`, y en el editor algunos signos
  saldrán en otra tecla (riesgo ya apuntado en el diseño del port).
- **El eco en tty1**: comprobado solo el orden de las llamadas; en el A4, que nada de lo escrito en el navegador aparezca en la consola.
- **Las monedas**: con el navegador o el editor abiertos no cuentan (se dice en la hoja); en el bar, abrir el navegador cuando no hay clientes.
- **Las USB de verdad**: detectarlas por `/sys` y montarlas con el `mount` de BusyBox (vfat, exfat, ntfs3: lo que el núcleo traiga) solo se
  probó con carpetas de mentira (`ROCOLA_ARCHIVOS_USB`).
- **Los archivos de XP abiertos con la rocola andando**: editar `rocola_config.txt` u otro de XP mientras la rocola corre puede no servir (XP lo
  reescribe al guardar lo suyo); para lo de XP, Ctrl+F3 y Ctrl+F2. Archivos es para las llaves y lo de Rocola OS.
- **Al juntar**: los números de los ganchos (g100-g101) y `GANCHOS.tsv`; las dos líneas de la prueba de la hoja Pantalla (dos `Tab` más).

## Revisión independiente (20-11-2026)

Revisión con ojo adversarial de 5e12441 (seguridad del gestor, el traspaso de la pantalla, sin `ROCOLA_OS`, los paquetes). Arreglado:

1. **Una tubería (FIFO) o un aparato en una USB colgaba la rocola**: `LeeTexto` y `Copia` abrían cualquier cosa que no fuera carpeta. Una
   tubería mide 0 y `open` se queda esperando a quien escriba: la hoja (el hilo de la pantalla) se congelaba y con ella la cola de la música
   al acabar la canción. Un aparato de bloques (una USB de ext4 puede traerlos; `nodev` no vale para una que ya montó otro) también mide 0 y
   se habría leído o copiado entero. Ahora solo archivos normales (`RocolaOsArchivos.EsArchivoNormal`, `stat` de glibc 2.33+ —la de Rocola
   OS la exporta—, `st_mode` en x86-64). 3 comprobaciones nuevas en `--prueba-archivos` (con `mkfifo`).
2. **Tapar las llaves cortas**: con «3 primeros y 3 últimos» una contraseña de 8 caracteres dejaba ver 6. Ahora los renglones de menos de 16
   caracteres van enteros tapados (`TapaEntero`); los largos, como antes. 1 comprobación más.

Comprobado sin cambiar: la regla de no salir (realpath; «..», enlaces, nombres con «/», el renglón «..» no se renombra ni se borra, un enlace
se borra él, `chmod` nunca sigue un enlace), nada se escribe en la USB, 0600 de las llaves, ninguna llave en bitácoras; el traspaso (si
`Lanza` falla, `finally` retoma; el guion cierra cog con su trampa en ≤3 s, antes del SIGKILL de 5 s; si la rocola se cae, el vigilante del
guion cierra cog); sin `ROCOLA_OS` g100 da `false` y g101 solo guarda; `make rocola_a4_defconfig` con el Buildroot del proyecto deja
WPEWEBKIT, COG, COG_PLATFORM_DRM, LIBSOUP3_SSL, XKEYBOARD_CONFIG, LIBINPUT y HAS_UDEV puestos (y `COG_USE_SYSTEM_DBUS` sin poner: cog sin bus
de sesión sigue como aplicación no única); `rapida.sh` pide la completa por `configs/`.

Los dos `Tab` de `Pruebas/RocolaOs/pantalla/prueba.sh` («teclado»: `7.15:9` y `7.45:9`, uno por cada pestaña nueva, Navegador y Archivos,
entre la pestaña Pantalla y la barra de la resolución) están bien: con ellos 71 de 71; sin ellos fallan las 3 de «teclado» (comprobado).
**Al juntar**: el número de `Tab` depende de cuántas hojitas tenga Ctrl+F4; si otro grupo añade una, otro `Tab` más.

Pruebas de la revisión (máquina compartida): compilar JIT 0 errores, 13 avisos de siempre; NativeAOT sin avisos IL/AOT (13,9 MB);
`ganchos.py` TODO BIEN; `navegador-archivos/prueba.sh` JIT 51 de 51 (antes del arreglo) y, con el arreglo, «orden» y «archivos» 12 de 12 en
JIT y todo 51 de 51 en AOT (`--prueba-archivos` 55 de 55); `pantalla/prueba.sh` JIT 71 de 71.

Queda señalado, sin tocar (no es un fallo claro o pide decidir):
- **cog corre como root y sin el sandbox de bubblewrap**: un fallo de WebKit explotado desde una página sería root en la rocola. Lo prudente
  es entrar solo a páginas de confianza (claude.ai, GitHub); hacerlo correr con otro usuario pide darle la tarjeta y la entrada (grupos
  `video` e `input`) y que el DRM master funcione sin root (por ser el primero en abrir): para probar en el A4.
- `Kms.Recupera` no mira si `drmSetMaster` falló (si un cog quedara vivo, pantalla negra sin aviso). Hoy no pasa: el guion no acaba mientras
  cog viva; solo si el guion se matara con SIGKILL antes de su trampa.
- Al escribir una llave, lo último tecleado se ve (los 3 últimos del renglón): quien mire mientras se escribe la va viendo pasar.
- `/run/rocola-navegador.pid` viejo de un pid que reusó otro proceso: «Ya hay un navegador abierto» (raro: /run se borra al prender).
- Una USB que se monta mientras se cierra la ventana puede quedar montada (de solo lectura) hasta la siguiente vez.
