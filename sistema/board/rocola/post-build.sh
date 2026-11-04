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
# LA MEMORIA PARA PROBAR EN CUALQUIER PC (ROCOLA_PERFIL=pc, nombre «rocola-pc»): solo prueba. Sin la entrada del instalador en los menus
# (lo primero pasa a ser «Probar Rocola OS») y con /etc/rocola-solo-probar, que hace que rocola-instalar se niegue aunque se llame a mano:
# en una PC ajena no se toca ningun disco (al probar, los discos se leen en solo lectura).
if grep -q '^BR2_TARGET_GENERIC_HOSTNAME="rocola-pc"' "$BR2_CONFIG"; then
	for f in "$TARGET_DIR/boot/grub/grub.cfg" "$BINARIES_DIR/efi-part/EFI/BOOT/grub.cfg"; do
		[ -f "$f" ] || continue
		awk '/^menuentry "Instalar/ {salta=1} salta && /^}/ {salta=0; next} !salta' "$f" > "$f.nuevo" && mv -f "$f.nuevo" "$f"
	done
	echo "Esta memoria es para probar Rocola OS en cualquier PC: no instala nada." > "$TARGET_DIR/etc/rocola-solo-probar"
else
	rm -f "$TARGET_DIR/etc/rocola-solo-probar"
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
# e2fsck para reparar datos.img si no monta (BR2_PACKAGE_E2FSPROGS), con sus bibliotecas; si falta alguna, el init sigue como antes
if [ -x "$TARGET_DIR/sbin/e2fsck" ]; then
	cp -L "$TARGET_DIR/sbin/e2fsck" "$I/bin/"
	for l in libext2fs.so.2 libcom_err.so.2 libe2p.so.2 libblkid.so.1 libuuid.so.1; do
		for d in lib usr/lib; do [ -e "$TARGET_DIR/$d/$l" ] && { cp -L "$TARGET_DIR/$d/$l" "$I/lib/"; break; }; done
	done
fi
cp "$BOARD_DIR/initramfs/init" "$I/init"; chmod 755 "$I/init"
mkdir -p "$I/etc"; [ -e "$TARGET_DIR/etc/localtime" ] && cp -L "$TARGET_DIR/etc/localtime" "$I/etc/localtime"   # la hora de Mexico
mkdir -p "$TARGET_DIR/usr/share/rocola/instalar"
(cd "$I" && find . | cpio -o -H newc --quiet | gzip -9) > "$TARGET_DIR/usr/share/rocola/instalar/initrd.gz"
rm -rf "$I"
# LO APRENDIDO DEL SISTEMA INSTALADO (rocola-instalar lo pone en \rocola-os\datos.img): un ext4 vacio de 512 MB, con su diario (un apagon no
# lo estropea). Se hace aqui porque el sistema no trae mkfs.ext4 (solo el mke2fs de BusyBox, sin diario). Comprimido ocupa ~1 MB.
D=$(mktemp -u)
MKE2FS=$HOST_DIR/sbin/mke2fs; [ -x "$MKE2FS" ] || MKE2FS=mke2fs   # (host-e2fsprogs: BR2_PACKAGE_HOST_E2FSPROGS)
"$MKE2FS" -q -t ext4 -L DATOS -E lazy_itable_init=0,lazy_journal_init=0 -F "$D" 512M
gzip -9c "$D" > "$TARGET_DIR/usr/share/rocola/instalar/datos.img.gz"
echo $(( $(stat -c %s "$D") / 1024 )) > "$TARGET_DIR/usr/share/rocola/instalar/datos.img.kb"
rm -f "$D"
# LA VERSION DEL SISTEMA (la pone GitHub al compilarlo: .github/workflows/rocola-sistema.yml; en casa, la 0). rocola-actualiza la compara
# con la ultima publicada.
echo "${ROCOLA_SISTEMA_VERSION:-0}" > "$TARGET_DIR/etc/rocola-sistema-version"
