#!/bin/sh
# Arranca la imagen para QEMU (ROCOLA_PERFIL=qemu sh sistema/compila.sh; la del A4 no corre en QEMU) (sin la Radeon: la grafica va por software). Uso: sh sistema/prueba-qemu.sh [-nographic]
TRABAJO=${ROCOLA_TRABAJO:-$HOME/rocola-trabajo}
exec qemu-system-x86_64 -m 2048 -smp 2 -drive file="$TRABAJO/salida-qemu/images/disk.img",format=raw,if=ide \
	-vga std -audiodev none,id=a -device intel-hda -device hda-duplex,audiodev=a "$@"
