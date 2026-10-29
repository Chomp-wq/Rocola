# Rocola OS

El sistema operativo propio de la rocola, basado en Linux y hecho con Buildroot. Prende y entra directo a la rocola: sin
escritorio, sin Windows y sin nada más corriendo.

Lo pidió el dueño el 29 de octubre de 2026. Preguntó: «¿Linux From Scratch no nos permitiría tener control máximo y máxima
optimización?». Se eligió Buildroot porque da el mismo control: compila todo desde el código fuente, con las opciones que se
elijan y solo lo que se elija. Y además se puede rehacer igual y actualizar cambiando una línea.

**La rocola de Windows XP sigue igual** (carpeta `Rocola/`) y sigue funcionando en el bar. Rocola OS va aparte, en esta carpeta,
hasta que esté probado en el A4.

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
| Arranque | GRUB de BIOS: un solo `disk.img` para grabar en una memoria USB o en el SSD | |
| Arranque por la partición | `PARTUUID=524f4341-01` | arranca igual desde la USB que desde el SSD |
| Consolas | tty1, la rocola; tty2, mantenimiento (Ctrl+Alt+F2, usuario `root`) | |

## Cómo se compila

```sh
sh sistema/compila.sh                    # todo (la primera vez, horas: herramientas, núcleo, glibc, Mesa)
sh sistema/compila.sh menuconfig         # cambiar paquetes u opciones del sistema
sh sistema/compila.sh linux-menuconfig   # cambiar opciones del núcleo
sh sistema/compila.sh savedefconfig      # guardar lo cambiado en configs/rocola_a4_defconfig
```

El trabajo va **fuera del repositorio**, en `$ROCOLA_TRABAJO` (por omisión `~/rocola-trabajo`): Buildroot, las descargas,
ccache y lo compilado, varios GB. La imagen queda en `~/rocola-trabajo/salida/images/disk.img`.

## Cómo se prueba

- **En QEMU (sin la Radeon):** `sh sistema/prueba-qemu.sh`. La gráfica va por software.
- **En el A4:**
  1. Grabar `disk.img` en una memoria USB **que se pueda borrar**: `dd if=disk.img of=/dev/sdX bs=4M conv=fsync` en Linux, o
     Rufus / balenaEtcher en Windows en modo «DD».
  2. Arrancar el A4 desde la USB: F8, F11 o F12 al prender, según la placa.
  3. Se oye un tono de 3 s y se ve un cubo girando 15 s.
  4. El resultado queda en `/var/log/rocola-prueba.txt`: procesador, memoria, gráfica y tarjeta de sonido que vio el núcleo, y
     si sonó y si dibujó.
  5. El SSD con Windows XP no se toca.

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
