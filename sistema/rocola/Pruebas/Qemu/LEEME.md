# La prueba del sistema entero en QEMU (grupo qemu, 14-11-2026)

Rocola OS con `rocola-xp` como único programa, de punta a punta en una máquina emulada: instalar desde la memoria USB en un disco de XP
falso, arrancar el sistema instalado, traer lo aprendido de una rocola de XP falsa, la pantalla por KMS, las teclas, el sonido, el video,
la consola de mantenimiento, una caída, un apagón, el botón de encendido y la confirmación de un sistema nuevo. **Todo inventado**: tonos y
ruido generados con ffmpeg, un video de barras, una carpeta de XP falsa (nada de canciones, letras ni nombres reales; los modelos de IA son
los del repositorio). QEMU sin KVM (TCG): muy lento, y **no es el A4** (otra gráfica, otro sonido, otro procesador). Resultados y lo que
no se pudo probar: `sistema/ACT_qemu.md`.

Hace falta: root (losetup, mount), QEMU (`qemu-system-x86_64`, `qemu-img`), mkntfs, ntfs-3g y ntfslabel, ffmpeg, python3 con numpy y
PIL, el SDK de .NET 10 (para compilar `rocola-xp`) y la última imagen **completa del perfil de QEMU** en
`$ROCOLA_TRABAJO/salida-qemu/images` (`ROCOLA_PERFIL=qemu sh sistema/compila.sh`; la del A4 no corre en QEMU: `sistema/LEEME.md`).

## Los pasos

```sh
Q=/root/rocola-trabajo/act/qemu                      # (la carpeta de trabajo; unos 450 MB con todo)
cd sistema/rocola/Xp && dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true -p:RocolaPerfil=qemu -o $Q/aot-qemu
cd ../Pruebas/Qemu
bash mundo.sh $Q/aot-qemu/rocola-xp      # la musica inventada y la carpeta de XP falsa (rocola-xp mide la musica aqui, fuera de pantalla)
bash discoxp.sh                          # el disco de XP falso: MBR, la 1 en el sector 63 (XP), la 2 con la musica y la carpeta de XP
bash arma.sh $Q/aot-qemu                 # la memoria USB: lo de sistema/rapida.sh sobre la imagen completa de salida-qemu
qemu-img convert -c -O qcow2 $Q/usb.img $Q/usb.qcow2 && rm $Q/usb.img   # (opcional: ocupa 100 MB en vez de 225)
python3 prueba.py                        # todas las fases (una hora larga); o algunas: python3 prueba.py consola caida
```

- `mundo.sh`: `W/d` (la partición «D:»: `musica/Artista - Disco/`, el video y `Rocola/Release`, la carpeta de XP con las tablas medidas
  por `rocola-xp` y las rutas pasadas a `D:\musica\...` por `mundo_xp.py`, botones, configuración, lo más sonado, un estado del día con 5
  créditos que **no** se debe traer y un `Rocola.exe` falso) y `W/c` (la «C:»: un `ntldr` y un `boot.ini` falsos, y `WINDOWS/Fonts` con
  letras **libres** de esta máquina -DejaVu Serif- con los nombres de las de XP: para ver que Rocola OS toma las letras del disco de Windows;
  nada de Microsoft). Una canción llega después de lo medido (`03 Ruido Nuevo.wav`): la mide la rocola y saca de ella material para la IA.
- `discoxp.sh`: `discoxp.img` (4,5 GB con huecos, unos 40 MB): como el del A4, la primera partición en el sector 63 (el hueco de 62
  sectores para el arranque), dos NTFS con números de serie distintos y un código de arranque que no hace nada (no es el de Windows).
- `arma.sh`: `usb.img`, la memoria USB: la imagen completa de `salida-qemu` (del 6-11-2026, b6e8f14) con lo que pone `rapida.sh`
  (`rootfs_overlay` entero, el menú, el `initrd.gz` con el `init` de ahora, `rocola-xp` y su versión, la canción de prueba), que es el camino
  por el que va a llegar `rocola-xp` a la rocola (la rápida con «rapida_ya»). **Y solo para la prueba**: root en el puerto serie
  (`ttyS0` en `/etc/inittab`) y `/etc/rocola-prueba-qemu`, que lo dice. Con eso se miran archivos y procesos de dentro sin tocar tty1 ni la
  consola de mantenimiento.
- `maquina.py`: arranca QEMU (TCG, `-cpu SandyBridge`: AVX sin AVX2, lo más parecido al A4 que emula QEMU; 2 núcleos, 3 GB; la VGA de bochs
  a 1024x768; el sonido HDA grabado a un WAV; el disco de XP con una capa qcow2 encima, `xp.qcow2`, para no tocar `discoxp.img`; sin red)
  y lo maneja desde fuera: teclas (`sendkey`), capturas (`screendump`), el botón de encendido (`system_powerdown`), el apagón (`quit`) y
  órdenes por el puerto serie. También a mano: `python3 maquina.py orden $Q/maq 'cat /rocola/datos/rocola.txt'`, `... tecla $Q/maq m`,
  `... captura $Q/maq foto.png`.
- `sonido.py WAV`: qué sonó en lo grabado (por segundos: el nivel y la frecuencia que domina; cada canción de la prueba es un tono).
- `prueba.py`: las fases `instala arranque teclas pared video consola caida apagon ia boton`, en ese orden (cada una usa lo que deja la
  anterior; `pared`, de la revisión: la pared -la navegación- dibujada por la tarjeta, no solo la atracción), y `a4`, aparte: el `rocola-xp` del perfil del A4 (`dotnet publish ... -p:RocolaPerfil=a4`) en un disco más, `$Q/a4.img`: un MBR
  con una partición ext2 con ese `rocola-xp` (`mke2fs -d`), que `rocola-discos` monta en solo lectura (`sistema/ACT_qemu.md`). Cada comprobación dice BIEN o MAL; INFO es lo que se apunta sin juzgar. Deja
  `$Q/prueba.txt`, `$Q/capturas/` y `$Q/sonido/`.

## Lo que hay que saber de QEMU

- **La pantalla de la tarjeta va muy lenta**: con Mesa softpipe emulado, cada cuadro de la rocola tarda 20 a 30 s (el cubo de la prueba de
  arranque, 6 por segundo). `rocola-xp` atiende las teclas y los cierres entre cuadro y cuadro: las teclas se mandan seguidas (en ráfaga) y
  se mira el resultado en `rocola_estado.txt` y en las capturas; la rocola entra en la atracción a los 40 s sin teclas, y cambia de letra
  una vez por cuadro (las pulsaciones que llegan juntas mientras pasa la página se pierden, como en XP): para llegar a la letra de los
  videos, la fase `video` navega con el dibujo por procesador y después vuelve a la tarjeta. Lo que en QEMU no
  llega a tiempo por eso (no por un fallo): la prueba de arranque le da 20 s a `rocola-xp` y lo corta a los 10 s de pedirle que se cierre;
  la consola de mantenimiento la corta si no se cerró en 20 s; el botón de encendido apaga a los 15 s. Por eso la fase `boton` pone
  antes el dibujo por procesador (`gpu=0`, unos 5 s por cuadro en QEMU).
- `rocola_error.txt` se llena de «Pantalla parada ...»: es lo lento de QEMU.
