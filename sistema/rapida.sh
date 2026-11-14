#!/bin/bash
# LA COMPILACION RAPIDA DEL SISTEMA (peticion del dueno del 7-11-2026: que las compilaciones sean mas rapidas). La mayoria de los cambios
# del sistema son scripts (el instalador, el buzon, el actualizador...), el arranque del sistema instalado (initramfs), el menu de la
# memoria USB o el programa de la rocola: para eso no hace falta que Buildroot lo rehaga todo (unos 30 min). Esto toma la ultima version
# COMPLETA publicada (la «base») y le pone encima todo lo que cambio desde entonces: unos minutos.
#
#   rapida.sh decide BASE_SHA          dice «rapida» o «completa» (lo que cambio desde BASE_SHA, el commit de la base)
#   rapida.sh arma BASE_SHA BASE SALIDA   BASE: la carpeta con sistema.img.gz, disk.img.gz, bzImage e initrd.gz de la base;
#                                         SALIDA: donde deja sistema.img.gz, bzImage, initrd.gz, sha256.txt, disk.img.gz(.sha256)
#                                         (ROCOLA_SISTEMA_VERSION: el numero de esta version, en /etc/rocola-sistema-version)
#
# Lo que la rapida sabe poner (si cambio cualquier otra cosa del sistema -la configuracion de Buildroot, el nucleo, los paquetes,
# post-build.sh, el menu de UEFI...- hace falta la completa):
#   board/rocola/rootfs_overlay/   se copia entero encima (como Buildroot: root, u=rwX,go=rX) y se quita lo que se borro
#   board/rocola/initramfs/init    initrd.gz se rehace con el init nuevo (lo demas de dentro es de la base)
#   board/rocola/grub-bios.cfg     el menu de la memoria USB (/boot/grub/grub.cfg)
#   rocola/Xp                      EL PROGRAMA: la rocola traducida de XP (rocola-xp), el unico de Rocola OS desde el 14-11-2026
#                                  (BR2_PACKAGE_ROCOLA_XP=y en configs/rocola_a4_defconfig; sistema/INTEGRACION_ROCOLA_XP.md): se compila
#                                  (NativeAOT, perfil del A4) y va en /usr/lib/rocola-xp, si cambio (o lo que toma de rocola/Consola y
#                                  rocola/Motor: Gl.cs, Kms.cs y Botones.cs) o si la base no lo trae. rocola-consola ya NO se compila: el de
#                                  una base que lo traiga (sistema-36 y anteriores) se queda en /usr/bin sin usarse hasta la siguiente completa.
# y lo que no va dentro del sistema: los .md, casa/ (Rocola en casa), el resto de rocola/ (rocola-consola, retirado) y todo lo de fuera de
# sistema/.
set -e
REPO=$(cd "$(dirname "$0")/.." && pwd)
B=sistema/board/rocola
# rocola-xp (la traduccion de XP) va en el sistema si la configuracion lo pide (desde el 14-11-2026, si)
con_xp() { grep -q '^BR2_PACKAGE_ROCOLA_XP=y' "$REPO/sistema/configs/rocola_a4_defconfig"; }

# los archivos de sistema/ que cambiaron desde la base y que la rapida NO sabe poner
no_rapidos() {
	git -C "$REPO" diff --no-renames --name-only "$1" HEAD -- sistema | grep -v \
		-e '\.md$' -e '^sistema/casa/' -e '^sistema/rocola/' -e "^$B/rootfs_overlay/" -e "^$B/initramfs/init\$" -e "^$B/grub-bios.cfg\$" \
		-e '^sistema/rapida.sh$' | { if con_xp; then cat; else grep -v '^sistema/package/rocola-xp/'; fi; } || true
	# (el paquete de rocola-xp, si estuviera apagado, no cambiaria el sistema: no pediria la completa)
}

if [ "$1" = decide ]; then
	# sin la base en la historia (otra rama, historia reescrita) no se sabe que cambio: la completa
	if ! git -C "$REPO" merge-base --is-ancestor "$2" HEAD 2>/dev/null; then echo completa; exit 0; fi
	n=$(no_rapidos "$2")
	if [ -n "$n" ]; then echo "$n" | sed 's/^/necesita la completa: /' >&2; echo completa; else echo rapida; fi
	exit 0
fi

[ "$1" = arma ] || { echo "uso: rapida.sh decide BASE_SHA | arma BASE_SHA BASE SALIDA" >&2; exit 2; }
BASE_SHA=$2; BASE=$(cd "$3" && pwd); mkdir -p "$4"; SALIDA=$(cd "$4" && pwd)
# «rapida_ya» (RAPIDA_YA=1): se hace aunque haya cambios que piden la completa; esos (nucleo, paquetes...) no entran hasta la siguiente
if [ -n "$(no_rapidos "$BASE_SHA")" ]; then
	if [ "$RAPIDA_YA" = 1 ]; then no_rapidos "$BASE_SHA" | sed 's/^/rapida ya: no entra hasta la completa: /' >&2
	else echo "rapida.sh: hay cambios que necesitan la compilacion completa" >&2; exit 1; fi
fi
cambio() { git -C "$REPO" diff --no-renames --name-only "$BASE_SHA" HEAD -- "$@" | grep -q .; }
T=$(mktemp -d); M=$T/raiz
trap 'mountpoint -q "$M" && sudo umount "$M"; rm -rf "$T"' EXIT
mkdir -p "$M"

# 1. EL SISTEMA (rootfs.ext2 de la base), montado
gzip -dc "$BASE/sistema.img.gz" > "$T/sistema.img"
sudo mount -o loop "$T/sistema.img" "$M"

# 2. LO DE rootfs_overlay: entero encima, como lo pone Buildroot, y fuera lo que se borro desde la base
sudo rsync -a --chmod=u=rwX,go=rX --chown=0:0 --exclude .empty --exclude '*~' "$REPO/$B/rootfs_overlay/" "$M/"
git -C "$REPO" diff --no-renames --name-only --diff-filter=D "$BASE_SHA" HEAD -- "$B/rootfs_overlay" | while read -r f; do
	r=${f#"$B/rootfs_overlay"}; [ -e "$REPO/$f" ] || { echo "se quita $r"; sudo rm -f "$M$r"; }
done

# 3. EL MENU DE LA MEMORIA USB (post-build.sh lo copia igual)
sudo cp -f "$REPO/$B/grub-bios.cfg" "$M/boot/grub/grub.cfg"

# 4. EL ARRANQUE DEL SISTEMA INSTALADO: el initrd.gz de la base con el init de ahora
mkdir "$T/initrd"
(cd "$T/initrd" && gzip -dc "$BASE/initrd.gz" | sudo cpio -id --quiet)
sudo install -m 755 "$REPO/$B/initramfs/init" "$T/initrd/init"
(cd "$T/initrd" && sudo find . | sudo cpio -o -H newc --quiet | gzip -9) > "$T/initrd.gz"
sudo install -D -m 644 "$T/initrd.gz" "$M/usr/share/rocola/instalar/initrd.gz"

# 5. EL PROGRAMA: rocola-xp (la rocola traducida de XP), si esta en la configuracion: si cambio (o lo que toma de rocola-consola: Gl.cs,
# Kms.cs y Botones.cs) o la base no lo trae (sistema-36 y anteriores: ahi estaba rocola-consola). Lo mismo que el paquete rocola-xp de
# Buildroot (package/rocola-xp/rocola-xp.mk): el programa y, a su lado, libSkiaSharp.so (las demas bibliotecas -libmpv, libmpg123, ALSA,
# EGL/GLES, GBM, DRM- ya estan en la base), su version (/usr/lib/rocola-xp/version: lo que dice «rocola-xp --version», compilado con el
# numero de este sistema y su commit) y la cancion de prueba (/usr/share/rocola/prueba.mp3 y el disco «Rocola OS - Prueba», que ponia
# rocola-consola). (rocola-consola ya no se compila ni se pone: el de la base se queda sin usarse hasta la siguiente completa.)
if con_xp && { [ ! -x "$M/usr/lib/rocola-xp/rocola-xp" ] || cambio sistema/rocola/Xp sistema/rocola/Consola/Pantalla/Gl.cs sistema/rocola/Consola/Pantalla/Kms.cs sistema/rocola/Motor/Botones.cs; }; then
	echo "rocola-xp (la traduccion de XP): se compila"
	V=${ROCOLA_SISTEMA_VERSION:-0}; F=$(date +%s)
	C=${GITHUB_SHA:-$(git -C "$REPO" rev-parse HEAD 2>/dev/null)}; C=${C:-local}; C=${C:0:7}
	(cd "$REPO/sistema/rocola/Xp" && DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 dotnet publish -c Release -r linux-x64 \
		-p:RocolaPerfil=a4 -p:RocolaVersion="$V" -p:RocolaCommit="$C" -p:RocolaFecha="$F" -o "$T/programa-xp" >/dev/null)
	esperada="rocola-xp programa $V ($C) compilado $F"
	linea=$(timeout 30 "$T/programa-xp/rocola-xp" --version 2>/dev/null | head -1) || true
	if [ -z "$linea" ]; then echo "rocola-xp: aqui no corre --version; se apunta la linea esperada: $esperada"; linea=$esperada
	elif [ "$linea" != "$esperada" ]; then echo "rapida.sh: rocola-xp --version dice «$linea» y se esperaba «$esperada»" >&2; exit 1; fi
	sudo install -D -m 755 "$T/programa-xp/rocola-xp" "$M/usr/lib/rocola-xp/rocola-xp"
	sudo install -D -m 644 "$T/programa-xp/libSkiaSharp.so" "$M/usr/lib/rocola-xp/libSkiaSharp.so"
	echo "$linea" | sudo tee "$M/usr/lib/rocola-xp/version" >/dev/null
	sudo install -D -m 644 "$REPO/sistema/rocola/Consola/prueba.mp3" "$M/usr/share/rocola/prueba.mp3"
	sudo install -D -m 644 "$REPO/sistema/rocola/Consola/prueba.mp3" "$M/rocola/musica/Rocola OS - Prueba/prueba.mp3"
	sudo mkdir -p "$M/rocola/datos"
	echo "rocola-xp: $linea"
fi

# 6. LA VERSION
echo "${ROCOLA_SISTEMA_VERSION:-0}" | sudo tee "$M/etc/rocola-sistema-version" >/dev/null
sync; sudo umount "$M"
sudo e2fsck -fy "$T/sistema.img" >/dev/null 2>&1 || [ $? -le 1 ] || { echo "rapida.sh: el sistema quedo con errores" >&2; exit 1; }

# 7. LA MEMORIA USB: el disk.img de la base con el sistema nuevo en su particion 1 (el mismo tamano; GRUB, el nucleo y la de UEFI no
# cambian)
gzip -dc "$BASE/disk.img.gz" > "$T/disk.img"
read -r inicio sectores < <(python3 -c "
import struct,sys; d=open(sys.argv[1],'rb').read(512); s,n=struct.unpack('<II',d[454:462]); print(s,n)" "$T/disk.img")
[ $((sectores * 512)) -eq "$(stat -c %s "$T/sistema.img")" ] || { echo "rapida.sh: la particion 1 de disk.img no mide lo que el sistema" >&2; exit 1; }
dd if="$T/sistema.img" of="$T/disk.img" bs=512 seek="$inicio" conv=notrunc status=none

# 8. LO QUE SE PUBLICA (los mismos archivos que la completa)
gzip -9c "$T/sistema.img" > "$SALIDA/sistema.img.gz"; rm -f "$T/sistema.img"
cp "$BASE/bzImage" "$SALIDA/bzImage"
cp "$T/initrd.gz" "$SALIDA/initrd.gz"
(cd "$SALIDA" && sha256sum sistema.img.gz bzImage initrd.gz > sha256.txt)
gzip -9c "$T/disk.img" > "$SALIDA/disk.img.gz"
(cd "$SALIDA" && sha256sum disk.img.gz > disk.img.gz.sha256)
mv "$T/disk.img" "$SALIDA/disk.img"   # para la prueba de arranque (no se publica)
echo "rapida.sh: listo en $SALIDA"
