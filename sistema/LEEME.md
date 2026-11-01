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
- **Para el dueño:** Ctrl+F3 en un teclado abre los ajustes del sonido (los mismos interruptores de XP); el reinicio de las 7 y el
  respaldo diario de lo aprendido; el repaso de la biblioteca en segundo plano.
- **La pantalla:** por la tarjeta gráfica (KMS y OpenGL ES 2), con el cielo Legacy (estrellas y la aurora, que late con la música), la
  portada enfocada con su halo, el disco abierto, lo que suena y lo que sigue. Si no hay pantalla gráfica, en modo texto.
- **El sistema:** arranca en BIOS y en UEFI; la primera vez hace la prueba de sonido y gráfica, después entra directo; el volumen
  general al máximo al arrancar (como XP); si la rocola se cierra, vuelve a abrirse; un fallo no la tumba.

**Lo que aún no tiene de XP:** los cubos y la estrella animada, el estado extendido y los demás adornos de la pantalla
(Ctrl+F3 → Pantalla); las herramientas de diagnóstico del dueño.

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
  6. Lo que pasó queda en la USB: `/root/rocola-prueba.txt` (la prueba) y `/root/rocola.txt` (la rocola). Para verlos: Ctrl+Alt+F2,
     usuario `root`.

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
