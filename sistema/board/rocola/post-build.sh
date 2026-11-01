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
# EL INITRAMFS DEL SISTEMA INSTALADO (rocola-instalar lo copia al disco de la rocola): BusyBox con sus bibliotecas y el init de
# board/rocola/initramfs, empaquetado (cpio + gzip) en /usr/share/rocola/instalar/initrd.gz.
I=$(mktemp -d)
mkdir -p "$I/bin" "$I/lib" "$I/dev" "$I/proc" "$I/sys" "$I/disco" "$I/raiz"
ln -s lib "$I/lib64"
cp -a "$TARGET_DIR/bin/busybox" "$I/bin/"
ln -s busybox "$I/bin/sh"
for l in ld-linux-x86-64.so.2 libc.so.6 libresolv.so.2 libm.so.6; do
	[ -e "$TARGET_DIR/lib/$l" ] && cp -L "$TARGET_DIR/lib/$l" "$I/lib/"
done
cp "$BOARD_DIR/initramfs/init" "$I/init"; chmod 755 "$I/init"
mkdir -p "$TARGET_DIR/usr/share/rocola/instalar"
(cd "$I" && find . | cpio -o -H newc --quiet | gzip -9) > "$TARGET_DIR/usr/share/rocola/instalar/initrd.gz"
rm -rf "$I"
# LA VERSION DEL SISTEMA (la pone GitHub al compilarlo: .github/workflows/rocola-sistema.yml; en casa, la 0). rocola-actualiza la compara
# con la ultima publicada.
echo "${ROCOLA_SISTEMA_VERSION:-0}" > "$TARGET_DIR/etc/rocola-sistema-version"
