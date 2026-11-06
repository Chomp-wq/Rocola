# Rocola OS

El sistema operativo propio de la rocola, basado en Linux y hecho con Buildroot. Prende y entra directo a la rocola: sin
escritorio, sin Windows y sin nada más corriendo.

Lo pidió el dueño el 29 de octubre de 2026. Preguntó: «¿Linux From Scratch no nos permitiría tener control máximo y máxima
optimización?». Se eligió Buildroot porque da el mismo control: compila todo desde el código fuente, con las opciones que se
elijan y solo lo que se elija. Y además se puede rehacer igual y actualizar cambiando una línea.

**La rocola de Windows XP sigue igual** (carpeta `Rocola/`) y sigue funcionando en el bar. Rocola OS va aparte, en esta carpeta,
hasta que esté probado en el A4.

## Qué hace hoy (1-11-2026)

Rocola OS ya es la rocola entera, hecha con el código de la de XP (el detalle de cada paso, en [ETAPA2.md](ETAPA2.md)):

- **El sonido de XP, igual:** la cadena entera (nivelado al límite, EQ automático, el local, el modo billar, presencia, voz/golpe/
  retumbe, bajo psicoacústico, limitador de pico real, fundido...), comparada muestra a muestra con la de XP. MP3 sin huecos, relevo
  sin pausa (también entre 44100 y 48000), discos continuos sin fundido. Por ALSA, sin servidores de sonido.
- **Lo que XP ya aprendió, sin tocar el disco de XP:** al arrancar se montan en solo lectura los demás discos (el de XP, memorias
  USB), se usa su carpeta `musica` y, la primera vez, se copian las medidas, el EQ, el volumen parejo, las estadísticas, los botones,
  la configuración y las IA, traduciendo las rutas de Windows.
- **La rocola:** la pared de discos por letra, lo más sonado (TOP), los vídeos (con mpv), el buscador con su teclado en pantalla,
  las monedas y los créditos (4 por moneda), el turno («Tu canción es la 3a - en ~7 min»), los créditos de servicio del dueño, la
  cola, y el estado que sobrevive a un apagón (y que el botón de encendido borra, como el cierre de XP). La misma navegación y los
  mismos botones que XP (`rocola_teclas.txt`), con su asistente para configurarlos.
- **Para el dueño:** Ctrl+F3 en un teclado abre los ajustes (los mismos interruptores de XP, del sonido y de la pantalla): «Lo que
  suena ahora» (escuchar sin mejoras y lo que la cadena le hace a la que suena), las comparaciones a ciegas, afinar el local con el
  celular, la prueba de bocinas y el diagnóstico completo de la pantalla; el reinicio de las 7 y el respaldo diario de lo aprendido;
  el repaso de la biblioteca en segundo plano.
- **La pantalla:** por la tarjeta gráfica (KMS y OpenGL ES 2), con el cielo Legacy (estrellas y la aurora, que late con la música) y la
  estrella del cielo en 3D (con su corona, su polvo y su estela); el cubo que gira en la portada elegida; la pantalla viva de XP (ficha
  rápida, letras con lupa, página que se desliza, lista grande, ecualizador, minutos de la cola, el boleto que vuela, la moneda que
  cae, la tarjeta que invita, los avisos claros, letra para leer de lejos, el color del género, los textos que se deslizan, las pistas
  del rojo); el estado extendido cuando nadie toca nada; la atracción como escaparate con lo más pedido esta noche; el buscador con
  portadas; y la ayuda de la estrella (ROJO + ABAJO), sin rostro, que lleva de la mano. Si no hay pantalla gráfica, en modo texto.
- **El sistema:** arranca en BIOS y en UEFI; la primera vez hace la prueba de sonido y gráfica, después entra directo; el volumen
  general al máximo al arrancar (como XP); el sonido en un hilo de tiempo real; si la rocola se cierra, vuelve a abrirse; un fallo no
  la tumba.

**Lo que aún no tiene de XP:** nada de lo que el dueño usa a diario; quedan detalles de pulido gráfico de XP (los de Ctrl+F3 → Fondo →
Pulido y los ahorros de Imagen → Performance, pensados para Direct3D 9 en XP).

**Nada se ha probado aún en el A4:** todo está probado aquí (comparación con XP, pruebas fuera de pantalla) y en QEMU (arranque BIOS y
UEFI, pantalla por KMS, sonido grabado de la tarjeta emulada, vídeo, botones por teclado, botón de encendido). Falta la máquina real.

## Las etapas

1. **El sistema mínimo** (esta etapa): arranca en el A4 desde una memoria USB y comprueba el sonido (ALSA) y la gráfica
   (Mesa r600 por KMS/EGL). Al arrancar corre `rocola-prueba`.
2. **La rocola en Linux:**
   - se queda en C# con .NET moderno: la cadena de audio, la biblioteca, las IA y la lógica;
   - se reescribe solo lo que es de Windows: la salida de sonido (winmm → ALSA), el dibujo (Direct3D 9 → OpenGL ES por EGL/KMS)
     y la ventana (WinForms → pantalla completa propia);
   - se compila como un solo programa nativo (NativeAOT): no hace falta instalar .NET en el sistema.
3. **El cambio en el bar:** cuando la de Linux esté probada en el A4, se instala en su SSD, guardando la música, las IA y la
   configuración.

## Qué lleva (`configs/rocola_a4_defconfig`)

| Pieza | Elección | Por qué |
|---|---|---|
| Procesador | x86-64 para Piledriver (`-march=bdver2`) | todo compilado para el A4-6300 exacto |
| Biblioteca C | glibc, con C++ | la necesitan Mesa y .NET |
| Núcleo | Linux 6.12 (soporte largo), la base de PC de Buildroot y `board/rocola/linux.fragment` | |
| Respuesta | expropiación completa y 1000 Hz | menos latencia para el sonido y la pantalla |
| Procesador a toda velocidad | gobernador «performance» | una rocola no ahorra batería |
| Gráfica | Mesa: r600 (la Radeon HD 8370D del A4) y softpipe (QEMU), por KMS/GBM/EGL, sin X ni Wayland | |
| Sonido | ALSA directo, sin mezcladores intermedios | |
| Firmware | Radeon (ARUBA), microcódigo AMD y la red Realtek | |
| Arranque | GRUB de BIOS y de UEFI: un solo `disk.img` (sistema en la partición 1, EFI en la 2) para grabar en una memoria USB o en el SSD | |
| Arranque por la partición | `PARTUUID=524f4341-01` | arranca igual desde la USB que desde el SSD |
| Consolas | tty1, la rocola; tty2, mantenimiento (Ctrl+Alt+F2, usuario `root`) | |

## Estado (31-10-2026)

- **La receta está validada contra Buildroot 2026.02.3:** `make rocola_a4_defconfig` acepta todas sus opciones tal cual (GCC
  14.3, glibc, núcleo 6.12.47, Mesa con r600 y softpipe sin LLVM, GRUB de BIOS).
- **Faltaba el programa de la prueba:** el arranque llamaba a `/usr/bin/rocola-prueba` y no existía. Ya está
  (`board/rocola/rootfs_overlay/usr/bin/rocola-prueba`). Apunta procesador, memoria, gráfica y sonido, toca un tono de 3 s, gira
  el cubo 15 s y deja el resultado en pantalla (para la foto) y en `/root/rocola-prueba.txt`. Busybox lleva `timeout`
  (`board/rocola/busybox.fragment`).
- **Aún no se ha compilado.** En el entorno en la nube donde se prepara, la red solo deja bajar de GitHub y PyPI, y Buildroot necesita
  78 fuentes de 19 sitios (gnu.org, kernel.org, freedesktop.org...). `sh sistema/comprueba-red.sh` dice cuáles faltan. **Con
  permitir `sources.buildroot.net`** (la copia de respaldo de Buildroot, que tiene todas) basta.
- **31-10, compilada.** Con `sources.buildroot.net` permitido, la imagen del A4 se compila entera en ~1 hora en la nube
  (`disk.img`, 537 MB). **No corre en QEMU:** el núcleo y los programas usan instrucciones propias del Piledriver (TBM: `blcs`,
  `blcfill`...) que el emulador no tiene, y se cae antes de escribir nada. En el A4 son normales. Para probar en QEMU está
  `ROCOLA_PERFIL=qemu sh sistema/compila.sh`: la misma receta para cualquier x86-64, en `salida-qemu/`.
- **31-10, probada en QEMU** (la versión `qemu`): arranca sola, corre `rocola-prueba`, el sonido (HDA emulado) pasa y el cubo se
  dibuja por KMS/EGL con Mesa. Falta el A4 de verdad (la Radeon, el códec Realtek, BIOS o UEFI).
- **31-10, la rocola dentro:** el paquete `rocola-consola` (la rocola sin pantalla, de la etapa 2) va en la imagen. La prueba de
  arranque la usa para tocar una canción de prueba por la cadena de sonido de la rocola. **Hace falta el SDK de .NET 10 en el
  equipo que compila el sistema** (no en Rocola OS). En QEMU: toca. La imagen del A4 hay que recompilarla para que la lleve.
- La etapa 2 (la rocola en Linux) está planeada en [ETAPA2.md](ETAPA2.md).

## Cómo se compila

```sh
sh sistema/comprueba-red.sh              # antes: si se llega a todas las fuentes
sh sistema/compila.sh                    # todo (la primera vez, horas: herramientas, núcleo, glibc, Mesa)
sh sistema/compila.sh menuconfig         # cambiar paquetes u opciones del sistema
sh sistema/compila.sh linux-menuconfig   # cambiar opciones del núcleo
sh sistema/compila.sh savedefconfig      # guardar lo cambiado en configs/rocola_a4_defconfig
```

El trabajo va **fuera del repositorio**, en `$ROCOLA_TRABAJO` (por omisión `~/rocola-trabajo`): Buildroot, las descargas,
ccache y lo compilado, varios GB. La imagen queda en `~/rocola-trabajo/salida/images/disk.img`.

## Cómo se prueba

- **En QEMU (sin la Radeon):** primero `ROCOLA_PERFIL=qemu sh sistema/compila.sh` y luego `sh sistema/prueba-qemu.sh` (o
  `sh sistema/prueba-qemu-captura.sh`, sin ventana: deja una captura de la pantalla). La gráfica va por software.
- **En el A4:**
  1. Compilar la imagen del A4 (`sh sistema/compila.sh`, en una máquina con Linux, unos 15 GB libres, el SDK de .NET 10 y sus
     herramientas de NativeAOT: en Ubuntu, `dotnet-sdk-10.0`, `dotnet-sdk-aot-10.0`, `clang` y `zlib1g-dev`, más `build-essential`,
     `cpio`, `rsync`, `bc`, `unzip` y `file`). Sale `~/rocola-trabajo/salida/images/disk.img` (1,1 GB).
  2. Grabarla en una memoria USB **que se pueda borrar**: `dd if=disk.img of=/dev/sdX bs=4M conv=fsync` en Linux, o Rufus /
     balenaEtcher en Windows en modo «DD».
  3. Arrancar el A4 desde la USB (F8, F11 o F12 al prender, según la placa). **Arranca en BIOS (heredado) y en UEFI.** El menú
     (3 s) tiene «Rocola OS» y «Rocola OS - prueba de sonido y grafica».
  4. **La primera vez** corre la prueba: tono de 3 s, la canción de prueba, el cubo 15 s y la rocola 20 s; luego deja el resumen
     en pantalla 60 s (para la foto: procesador, gráfica, sonido y **la música que encontró**) y entra en la rocola. Las siguientes
     veces entra directo en la rocola (la prueba, desde el menú).
  5. **La música y lo aprendido se leen del disco de XP en solo lectura**: Rocola OS monta los demás discos sin escribir en ellos
     (`rocola-discos`), usa sus carpetas `musica` y la primera vez copia lo que la rocola de XP aprendió (medidas, EQ, botones,
     configuración, estadísticas, IA: `rocola_importado_xp.txt` dice qué). El SSD con Windows XP no se toca: se quita la USB y el
     A4 vuelve a arrancar en XP como siempre.
  6. Lo que pasó queda en la USB: `/root/rocola-prueba.txt` (la prueba) y `/root/rocola.txt` (la rocola; va con lo aprendido, en `/rocola/datos/rocola.txt`, y no se pierde al actualizar el sistema). Para verlos: Ctrl+Alt+F2,
     usuario `root`.
  7. **Si la imagen del A4 no arranca allí** (se reinicia, o se queda en negro tras el menú): la imagen de QEMU
     (`ROCOLA_PERFIL=qemu sh sistema/compila.sh`, en `salida-qemu/images/disk.img`) es la misma rocola para cualquier x86-64 y también
     sirve en el A4, solo que sin las instrucciones propias del Piledriver en el sistema. Con la foto de la pantalla y
     `/root/rocola-prueba.txt` se ve qué pasó.

**Lo que se sabe de la imagen del A4 sin haberla puesto en el A4 (1-11-2026):** se compila entera; su núcleo es genérico y arranca en
QEMU; la rocola (el binario de .NET) corre entera —sonido y pantalla— emulando un Piledriver sin AVX2. El resto del sistema (glibc,
BusyBox, Mesa...) va compilado con las instrucciones del Piledriver (XOP, FMA4, TBM), que QEMU no emula: eso solo se puede probar en
el A4.

## Probarla en cualquier PC (la memoria «PC»)

La memoria del A4 (`disk.img.gz`) no arranca en una PC actual: va compilada con instrucciones que solo tienen los AMD de la época del
A4. Para probar la rocola en otra PC (Intel o Ryzen, con cualquier tarjeta gráfica), cada versión trae también **`disk-pc.img.gz`**
(`ROCOLA_PERFIL=pc sh sistema/compila.sh`, en `salida-pc/`; se publica un rato después que la del A4):

- **Solo prueba:** su menú no tiene el instalador y `rocola-instalar` se niega aunque se llame a mano. Los discos de la PC solo se leen
  (busca en ellos una carpeta `musica`, también en discos NVMe); lo aprendido se queda en la memoria.
- **La pantalla:** la que deja puesta la UEFI de la placa (simpledrm), a la resolución que ella elija; la rocola dibuja con el
  procesador (Mesa llvmpipe). Sirve con NVIDIA (que no tiene controlador libre ligero), AMD o Intel. La fluidez no dice nada de la del
  A4, que dibuja con su Radeon.
- **Arranque:** en UEFI y con **Secure Boot desactivado** (en la BIOS de la placa); en el menú de arranque (F8, F11 o F12) elegir la
  memoria «UEFI». El sonido sale por la tarjeta de la placa (la salida de los audífonos o las bocinas, no por HDMI), o por una USB.
- Se graba igual que la otra (balenaEtcher o Rufus en modo DD).

## Instalarlo en la rocola (el instalador)

1. Graba `disk.img` en una memoria USB (paso 2 de arriba), conéctala a la rocola y préndela desde la memoria.
2. **El instalador arranca solo** (es lo primero del menú, a los 5 s; `rocola-instalar`). Busca el disco de la rocola (un NTFS que no
   sea la memoria, con la carpeta `musica`) y enseña qué encontró: el disco, sus carpetas `musica`, cuántas canciones, la carpeta de los
   datos de la rocola de XP y si está Windows XP.
3. **ENTER (el botón verde) instala;** cualquier otro botón cancela sin tocar nada. Antes de preguntar comprueba que el disco es MBR
   (el de XP), que hay hueco para el arranque y que hay sitio (unos **2,8 GB libres**: las dos copias del sistema, lo aprendido y el
   arranque); si algo falta, lo dice y no toca nada.
4. Instala Rocola OS **dentro del mismo disco**, en la carpeta `\rocola-os` (el sistema va en un archivo, `sistema.img`): no se
   reparticiona ni se formatea nada, **la música se queda donde está** y la rocola la encuentra sola. Los archivos de la rocola de XP
   (su configuración, lo aprendido, los volúmenes, los botones, las IA) tampoco se tocan, y la primera vez Rocola OS los copia
   (`rocola_importado_xp.txt` dice qué; las rutas de Windows se traducen aunque estén escritas con otras mayúsculas). La llave de
   GitHub (`rocola_github.txt`), si está en la carpeta de la rocola de XP, se copia al instalar. Lo que la memoria ya tuviera de haberla
   probado en la rocola se lleva también.
5. El sistema se copia y se **comprueba byte a byte**; lo aprendido va en `\rocola-os\datos.img` (ext4 con diario: un apagón no lo
   estropea). Si Windows no se apagó bien (o está hibernado), no escribe en el disco: pide prender XP y apagarlo desde Inicio.
6. Al acabar pide **quitar la memoria USB** y se reinicia sola: desde entonces prende directo en la rocola.

Windows XP deja de arrancar, pero no se borra nada suyo: con **ESC al prender** sale el menú, que tiene «Windows XP (la rocola de
antes)». El arranque de antes (el primer sector del disco y el hueco que usa GRUB, sin la tabla de particiones) queda guardado en
`\rocola-os\arranque-xp.original.bin`; **para dejar la rocola como antes**, `rocola-desinstalar` en la consola de mantenimiento
(Ctrl+Alt+F2, usuario `root`) lo devuelve y la rocola vuelve a prender en XP (no borra `\rocola-os`). Volver a instalar (otra versión)
pone el sistema en la otra copia y conserva lo aprendido. Lo que hizo el instalador queda en `\rocola-os\instalar.txt`.

**Probado (3-11-2026) en una máquina emulada** con el núcleo de sistema-5 y un disco como el de XP (NTFS desde el sector 63, con su
`ntldr`): instala, el disco prende solo en Rocola OS, monta lo aprendido y vuelve solo al sistema anterior si el nuevo no se confirma.
**No probado en el A4.**

## Internet, actualizaciones y encendido remoto

La rocola se conecta **por cable** (DHCP: basta conectarla al router). Si el router tarda en prender (tras un apagón prenden los dos a
la vez) o el cable se conecta después, la rocola lo sigue intentando cada 20 s y se conecta sola. La hora es la de México
(`America/Mexico_City`, sin cambio de horario) y se pone por internet; el reloj de la placa se deja en hora local, como lo tiene
Windows XP. Con internet:

- **El programa de la rocola se actualiza solo.** Cada cambio que se sube a GitHub lo compila GitHub
  (`.github/workflows/rocola-programa.yml`) y lo publica como la versión «programa-N» del repositorio. La rocola la busca al minuto
  de prender y cada hora, la baja, comprueba su suma y que arranca en ese equipo, y la pone **sola en cuanto está libre** (nada
  sonando ni en la cola, y nadie tocándola en 2 minutos; los créditos se conservan). Si la nueva se cae al arrancar, vuelve sola a la
  anterior y no la vuelve a bajar. Y desde la versión con las dos copias, **también el sistema entero** (ver abajo).
- **Ctrl+F3 → Internet:** la versión, la red (con la MAC), cómo va la actualización, «Buscar actualización ahora» y el interruptor
  «Actualizarse sola». Lo que pasó, en `rocola_actualizaciones.txt`.

**El sistema operativo también se actualiza (soporte continuo).** GitHub compila el sistema entero con Buildroot en cada cambio de
`sistema/` (`.github/workflows/rocola-sistema.yml`; la primera vez tarda horas, después mucho menos) y publica la versión
«sistema-N». La rocola instalada guarda **dos copias del sistema** (`\rocola-os\a` y `\rocola-os\b`): la nueva se baja en la que no se
usa, se comprueba y la rocola se reinicia sola cuando está libre. Si el sistema nuevo no llega a funcionar 3 minutos (dos intentos),
**vuelve solo al anterior** y no lo vuelve a bajar. Lo aprendido y el programa van aparte (`\rocola-os\datos.img`) y no se tocan. Con
ESC al prender, el menú tiene también «la versión anterior del sistema». Lo que pasa, en `\rocola-os\sistema.txt`.

**La memoria USB, sin compilar nada:** cada versión «sistema-N» trae `disk.img.gz` (la imagen del instalador). Se baja de GitHub
(el repositorio → Releases → la última «Sistema de la rocola»), se descomprime y se graba en la memoria (Rufus o balenaEtcher).

**La llave de GitHub (una vez).** El repositorio es privado: la rocola necesita una llave que solo pueda leerlo.
1. En GitHub: tu foto → **Settings → Developer settings → Personal access tokens → Fine-grained tokens → Generate new token**.
2. Nombre «rocola», caducidad la más larga, **Repository access: Only select repositories → Chomp-wq/Rocola**,
   **Permissions → Repository permissions → Contents: Read-only** (nada más). **Generate token** y copia la llave.
3. Guárdala en un archivo de texto **`rocola_github.txt`** (solo la llave) en una memoria USB y conéctala a la rocola prendida: en la
   siguiente revisión la copia (o «Buscar actualización ahora»). También vale ponerla en la carpeta de la rocola de XP antes de instalar.
Si la llave caduca o se borra, la rocola sigue funcionando: solo deja de actualizarse (Ctrl+F3 lo dice).

**Lo que hace la actualización (`rocola-actualiza`):** busca la llave en las memorias USB conectadas (aunque se conecten con la
rocola prendida) o en la carpeta de XP, pone la hora por NTP, baja el programa y el sistema con sus sumas comprobadas (una descarga
rota no cambia nada y se reintenta en una hora), y con «Actualizarse sola» apagado solo busca si se pide «Buscar actualización ahora».

**Encenderla desde casa (Wake on LAN).** La rocola deja su tarjeta de red escuchando el «paquete mágico» al apagarse.
1. **En el BIOS del A4** (Supr al prender): activar *Wake on LAN* / *Power On by PCI-E* / *Resume by LAN* (el nombre cambia según la
   placa) y, si lo hay, desactivar *ErP / Deep Sleep*.
2. **En el router del bar:** en su app o su página, la función *Wake on LAN* (o *Encender equipo*), con la **MAC** de la rocola
   (Ctrl+F3 → Internet). Conviene darle a la rocola una **IP fija** (reserva de DHCP) en el router.
3. Probarlo con la rocola apagada desde el botón (no desenchufada): desde la app del router, en el bar y luego desde casa.
Si el router no lo trae, hay otras formas (abrir un puerto y una app de Wake on LAN en el celular): dímelo y se prepara.

## Spotify en las reuniones familiares

Pedido del dueño el 6-11-2026: **Spotify dentro de la rocola, solo para la familia**, con un interruptor **apagado de fábrica**
(Spotify es para uso personal: no es para el público del bar).

**Cómo se usa:**
1. Ctrl+F3 → **Spotify** → «Spotify en la rocola (reuniones familiares)»: **SÍ**. Hace falta **Spotify Premium**.
2. **Solo la primera vez:** en el celular, conectado al wifi del mismo router que la rocola, abre Spotify → el icono de dispositivos →
   **«Rocola»**. Con eso la rocola recibe tu cuenta y la guarda (solo la llave, en `/rocola/datos/spotify`); desde entonces entra sola y
   el celular ya no hace falta. Ctrl+F3 → Spotify dice cómo va.
3. En la pared aparece la letra **SPOTIFY** (junto a TOP): **tus favoritas, tus listas y tus discos guardados**, con sus portadas. Y el
   **buscador** (ROJO + ARRIBA) busca también en Spotify: canciones, discos y listas salen detrás de los de tu música.
4. Se elige con los botones de siempre y **cuesta créditos como cualquier canción** (la moneda, el turno, «Inserta una moneda»): la
   rocola clásica de siempre, con música de Spotify.

**Cómo suena y qué guarda:**
- Va **por la cadena de la rocola**: nivelado (al nivel de las canciones aún sin medir), el local, el modo billar, el limitador.
- Si una canción de Spotify no se puede tocar (sin internet, no disponible), **se devuelve el crédito**. Al apagar el interruptor, o tras
  un apagón, lo pagado de Spotify que aún no sonó **vuelve como créditos**.
- Lo que suena de Spotify **no cuenta para lo más sonado**; solo se mide su volumen (para nivelarlo). **No se graba ni se descarga música**: el sonido va de la memoria a
  la tarjeta, y las portadas solo viven en la memoria mientras la rocola está prendida.
- Apagado el interruptor, la rocola es la de siempre (el programa de Spotify ni se abre).

**Lo que Spotify le añade a la rocola** (cada cosa con su interruptor en Ctrl+F3 → Spotify, encendidas; nada suena sin créditos):
- **Tus canciones dañadas suenan desde Spotify, sin que se note:** la rocola busca cada canción tuya en Spotify (poco a poco, de
  fondo). Si una de las tuyas trae los agudos cortados y en Spotify está la misma grabación, suena la de Spotify (320 kbps) en su
  lugar. **En la pantalla no cambia nada**: mismo nombre, portada, disco y crédito. Si Spotify falla, suena tu MP3.
- **Letras al estilo Apple Music** en el estado extendido: la línea que se canta grande y encendida, las demás tenues, deslizándose
  suave; en los tramos sin voz, los tres puntos. También para tus canciones que tienen pareja en Spotify.
- **Portadas y géneros que faltan:** tus discos sin carátula toman la de Spotify; tus canciones sin género, el del artista.
- **Completar tus discos:** las canciones que le faltan a un disco tuyo salen al final, marcadas «· Spotify», con su crédito.
- **Éxitos del momento** (Top 50 y Virales de México) en la letra SPOTIFY, si Spotify los deja ver; y en el buscador, los atajos
  **«Tus más escuchadas»** y **«Escuchado hace poco»**.
- **La foto del artista** de fondo en el estado extendido y el año del disco.
- Lo de Spotify se nivela como tu música (la segunda vez, al límite).

**Lo que hay que saber:** usa las piezas de **librespot**, un programa libre que **no es oficial de Spotify** (como ncspot). Spotify no lo
autoriza: **podría bloquear la cuenta** (no es habitual) y, si Spotify cambia algo, puede dejar de funcionar hasta que se actualice
(llegaría con la actualización del sistema). Lo que pasa queda en `/rocola/datos/rocola_spotify.txt`. **Probado aquí con un Spotify
simulado (la pared, el buscador, los créditos, el sonido por la cadena); no probado con una cuenta real ni en el A4.**

## Personalizarlo

Todo se cambia en esta carpeta y se recompila:

- **Paquetes:** `configs/rocola_a4_defconfig` (o `menuconfig`).
- **Núcleo:** `board/rocola/linux.fragment`.
- **Archivos propios del sistema:** `board/rocola/rootfs_overlay/` (por ejemplo, lo que corre al arrancar, en
  `etc/init.d/S99rocola`).
- **Menú de arranque:** `board/rocola/grub-bios.cfg`.

Ideas para después:

- una pantalla de arranque con el logo;
- la red para actualizar música;
- acceso remoto para revisar el equipo;
- un núcleo de tiempo real (PREEMPT_RT) si hiciera falta;
- actualizaciones con copia de seguridad (dos particiones de sistema).

## Lo que aún no se sabe

Nada de esto se ha probado en el A4; solo se compila y se arranca aquí, en QEMU. En particular, falta comprobar en la
máquina real:

- si arranca en BIOS o en UEFI;
- qué códec de sonido tiene la placa;
- que la Radeon funcione con r600.

**La prueba de la etapa 1 es para eso.**
