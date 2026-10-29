#!/bin/sh
# Despues de armar el sistema: el menu de GRUB y su primera etapa (para genimage).
set -e
BOARD_DIR=$(dirname "$0")
cp -f "$BOARD_DIR/grub-bios.cfg" "$TARGET_DIR/boot/grub/grub.cfg"
cp -f "$TARGET_DIR/lib/grub/i386-pc/boot.img" "$BINARIES_DIR"
