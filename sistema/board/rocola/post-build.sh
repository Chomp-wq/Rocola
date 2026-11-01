#!/bin/sh
# Despues de armar el sistema: los menus de GRUB (el de BIOS, dentro del sistema; el de UEFI, en la particion EFI) y la primera etapa
# de GRUB de BIOS (para genimage).
set -e
BOARD_DIR=$(dirname "$0")
cp -f "$BOARD_DIR/grub-bios.cfg" "$TARGET_DIR/boot/grub/grub.cfg"
cp -f "$TARGET_DIR/lib/grub/i386-pc/boot.img" "$BINARIES_DIR"
if [ -d "$BINARIES_DIR/efi-part/EFI/BOOT" ]; then
	cp -f "$BOARD_DIR/grub-efi.cfg" "$BINARIES_DIR/efi-part/EFI/BOOT/grub.cfg"
fi
