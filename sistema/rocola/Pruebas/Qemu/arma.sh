#!/bin/bash
# LA IMAGEN DE LA PRUEBA EN QEMU (grupo qemu, 14-11-2026): lo mismo que hace sistema/rapida.sh («arma», el camino por el que va a llegar
# rocola-xp a la rocola: la rapida con «rapida_ya» sobre una completa vieja), pero sobre la ultima imagen COMPLETA del perfil de QEMU
# (ROCOLA_TRABAJO/salida-qemu/images, la de `ROCOLA_PERFIL=qemu sh sistema/compila.sh`; la del A4 no corre en QEMU: sistema/LEEME.md):
#   1. el sistema (rootfs.ext2 de esa imagen), copiado y montado;
#   2. board/rocola/rootfs_overlay entero encima (como Buildroot: root, u=rwX,go=rX) y fuera lo que se borro desde BASE_SHA;
#   3. el menu de la memoria USB (grub-bios.cfg);
#   4. el initrd.gz del sistema instalado con el init de ahora;
#   5. rocola-xp (el ejecutable que se de: el del perfil de QEMU), su libSkiaSharp.so, su «version» (lo que dice --version), la
#      cancion de prueba y /rocola/datos;
#   6. /etc/rocola-sistema-version;
#   y SOLO PARA LA PRUEBA (no va en Rocola OS): una linea de ordenes de root en el puerto serie (ttyS0, en /etc/inittab), para mirar desde
#   fuera lo que pasa dentro sin tocar tty1 ni la consola de mantenimiento, y /etc/rocola-prueba-qemu que lo dice.
#   7. la memoria USB: el disk.img de esa imagen con el sistema nuevo en su particion 1.
#   bash arma.sh CARPETA_DE_ROCOLA_XP [SALIDA]   (CARPETA: la de dotnet publish, con rocola-xp y libSkiaSharp.so; SALIDA, por omision
#                                                 /root/rocola-trabajo/act/qemu/usb.img, con huecos: unos 250 MB ocupados)
#   BASE_SHA: el commit de la imagen base (por omision b6e8f14, la de salida-qemu del 6-11-2026); VERSION: la del sistema (0).
set -eu
G=$(cd "$(dirname "$0")" && pwd); REPO=$(cd "$G/../../../.." && pwd)
PX=$(cd "${1:?uso: arma.sh CARPETA_DE_ROCOLA_XP [SALIDA]}" && pwd)
SALIDA=${2:-/root/rocola-trabajo/act/qemu/usb.img}
BASE=${ROCOLA_TRABAJO:-$HOME/rocola-trabajo}/salida-qemu/images
BASE_SHA=${BASE_SHA:-b6e8f14}
VERSION=${VERSION:-0}
B=sistema/board/rocola
T=$(mktemp -d "$(dirname "$SALIDA")/arma.XXXXXX"); M=$T/raiz
trap 'mountpoint -q "$M" && umount "$M"; rm -rf "$T"' EXIT
mkdir -p "$M"
[ -x "$PX/rocola-xp" ] && [ -f "$PX/libSkiaSharp.so" ] || { echo "arma.sh: falta rocola-xp o libSkiaSharp.so en $PX" >&2; exit 1; }

# 1. el sistema de la base
cp --sparse=always "$BASE/rootfs.ext2" "$T/sistema.img"
mount -o loop "$T/sistema.img" "$M"
# 2. el overlay, como Buildroot, y fuera lo borrado desde la base
rsync -a --chmod=u=rwX,go=rX --chown=0:0 --exclude .empty --exclude '*~' "$REPO/$B/rootfs_overlay/" "$M/"
git -C "$REPO" diff --no-renames --name-only --diff-filter=D "$BASE_SHA" HEAD -- "$B/rootfs_overlay" | while read -r f; do
	r=${f#"$B/rootfs_overlay"}; [ -e "$REPO/$f" ] || { echo "se quita $r"; rm -f "$M$r"; }
done
# 3. el menu de la memoria USB
cp -f "$REPO/$B/grub-bios.cfg" "$M/boot/grub/grub.cfg"
# 4. el initrd del sistema instalado con el init de ahora
mkdir "$T/initrd"
(cd "$T/initrd" && gzip -dc "$M/usr/share/rocola/instalar/initrd.gz" | cpio -id --quiet)
install -m 755 "$REPO/$B/initramfs/init" "$T/initrd/init"
(cd "$T/initrd" && find . | cpio -o -H newc --quiet | gzip -9) > "$T/initrd.gz"
install -D -m 644 "$T/initrd.gz" "$M/usr/share/rocola/instalar/initrd.gz"
# 5. rocola-xp (lo mismo que el paso 5 de rapida.sh y el paquete de Buildroot)
linea=$(timeout 30 "$PX/rocola-xp" --version 2>/dev/null | head -1)
[ -n "$linea" ] || { echo "arma.sh: rocola-xp --version no contesta" >&2; exit 1; }
install -D -m 755 "$PX/rocola-xp" "$M/usr/lib/rocola-xp/rocola-xp"
install -D -m 644 "$PX/libSkiaSharp.so" "$M/usr/lib/rocola-xp/libSkiaSharp.so"
echo "$linea" > "$M/usr/lib/rocola-xp/version"
install -D -m 644 "$REPO/sistema/rocola/Consola/prueba.mp3" "$M/usr/share/rocola/prueba.mp3"
install -D -m 644 "$REPO/sistema/rocola/Consola/prueba.mp3" "$M/rocola/musica/Rocola OS - Prueba/prueba.mp3"
mkdir -p "$M/rocola/datos"
echo "rocola-xp: $linea"
# 6. la version del sistema
echo "$VERSION" > "$M/etc/rocola-sistema-version"
# SOLO PARA LA PRUEBA: root en el puerto serie (QEMU lo da por un socket) y la marca de que esta imagen es de prueba
grep -q '^ttyS0::' "$M/etc/inittab" || printf '\n# PRUEBA EN QEMU (sistema/rocola/Pruebas/Qemu/arma.sh): root en el puerto serie; no va en Rocola OS\nttyS0::respawn:-/bin/sh\n' >> "$M/etc/inittab"
echo "Imagen de PRUEBA EN QEMU (sistema/rocola/Pruebas/Qemu/arma.sh): Rocola OS con root en el puerto serie (ttyS0). No va en la rocola." > "$M/etc/rocola-prueba-qemu"
sync; umount "$M"
e2fsck -fy "$T/sistema.img" >/dev/null 2>&1 || [ $? -le 1 ] || { echo "arma.sh: el sistema quedo con errores" >&2; exit 1; }

# 7. la memoria USB: el disk.img de la base con el sistema nuevo en su particion 1 (con huecos: lo que era cero no ocupa)
rm -f "$SALIDA"; cp --sparse=always "$BASE/disk.img" "$SALIDA"
read -r inicio sectores < <(python3 -I -c "
import struct,sys; d=open(sys.argv[1],'rb').read(512); s,n=struct.unpack('<II',d[454:462]); print(s,n)" "$SALIDA")
[ $((sectores * 512)) -eq "$(stat -c %s "$T/sistema.img")" ] || { echo "arma.sh: la particion 1 de disk.img no mide lo que el sistema" >&2; exit 1; }
fallocate -p -o $((inicio * 512)) -l $((sectores * 512)) "$SALIDA"   # (lo de la base fuera; asi «sparse» no deja restos)
dd if="$T/sistema.img" of="$SALIDA" bs=1M seek=$((inicio * 512)) oflag=seek_bytes conv=notrunc,sparse status=none
echo "listo: $SALIDA ($(du -h "$SALIDA" | cut -f1) ocupados)"
