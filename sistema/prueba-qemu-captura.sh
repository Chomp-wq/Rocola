#!/bin/sh
# Arranca la imagen para QEMU (ROCOLA_PERFIL=qemu sh sistema/compila.sh; la del A4 no corre en QEMU) sin ventana, espera y saca una captura de la pantalla (tty1) a $TRABAJO/captura.ppm.
# Para comprobar en una maquina sin pantalla (en la nube) que el sistema arranca y corre la prueba. Uso: sh sistema/prueba-qemu-captura.sh [segundos]
TRABAJO=${ROCOLA_TRABAJO:-$HOME/rocola-trabajo}
ESPERA=${1:-90}
SOCK="$TRABAJO/qemu-monitor.sock"
rm -f "$SOCK" "$TRABAJO/captura.ppm"
qemu-system-x86_64 -m 2048 -smp 2 -drive file="$TRABAJO/salida-qemu/images/disk.img",format=raw,if=ide,snapshot=on \
	-vga std -display none -audiodev none,id=a -device intel-hda -device hda-duplex,audiodev=a \
	-monitor unix:"$SOCK",server,nowait &
PID=$!
sleep "$ESPERA"
printf 'screendump %s\nquit\n' "$TRABAJO/captura.ppm" | python3 -c '
import socket, sys, time
s = socket.socket(socket.AF_UNIX); s.connect(sys.argv[1])
for linea in sys.stdin: s.sendall(linea.encode()); time.sleep(2)
' "$SOCK"
wait $PID
ls -la "$TRABAJO/captura.ppm"
