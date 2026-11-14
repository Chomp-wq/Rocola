#!/bin/bash
# EL DISCO DE XP FALSO DE LA PRUEBA EN QEMU (grupo qemu, 14-11-2026): como el del A4 (sistema/ETAPA2.md, «Revision a fondo del instalador y
# prueba de punta a punta»): tabla MBR con la primera particion en el sector 63 (el hueco de 62 sectores que deja XP para el arranque), la
# 1 un NTFS con «Windows XP» (un ntldr y un boot.ini falsos: no arranca nada) y la 2 un NTFS grande con la musica y la carpeta de la
# rocola de XP (lo que hizo mundo.sh). El codigo de arranque del primer sector tampoco es el de Windows: unos bytes que no hacen nada
# (el instalador lo guarda y rocola-desinstalar lo devuelve; aqui solo cuenta que no sea GRUB).
#   bash discoxp.sh   (W: la carpeta de mundo.sh, por omision /root/rocola-trabajo/act/qemu/mundo; deja W/../discoxp.img, 4,5 GB con
#                      huecos: ocupa unos MB). Hace falta ser root (losetup), mkntfs y ntfs-3g.
set -eu
W=${W:-/root/rocola-trabajo/act/qemu/mundo}
I=$(dirname "$W")/discoxp.img
[ -d "$W/d" ] && [ -d "$W/c" ] || { echo "discoxp.sh: falta el mundo ($W): primero mundo.sh" >&2; exit 1; }
CIL=16065                      # un cilindro de XP: 255 cabezas x 63 sectores
P1=63; N1=$((32 * CIL - 63))   # la 1 (C:, «Windows XP»): ~250 MB, desde el sector 63
P2=$((32 * CIL))               # la 2 (D:, la musica): hasta el final
TOTAL=$((9437184))             # 4,5 GB en sectores de 512
N2=$((TOTAL - P2))
rm -f "$I"; truncate -s $((TOTAL * 512)) "$I"
python3 -I - "$I" "$P1" "$N1" "$P2" "$N2" <<'EOF'
import struct, sys
img, p1, n1, p2, n2 = sys.argv[1], *map(int, sys.argv[2:])
mbr = bytearray(512)
codigo = b"\xfa\xeb\xfe" + b"Arranque de un XP FALSO (prueba de Rocola OS en QEMU)"   # cli; jmp $ (no hace nada)
mbr[0:len(codigo)] = codigo
struct.pack_into("<I", mbr, 440, 0x58505846)   # la firma del disco
def entrada(i, boot, tipo, ini, n):
    o = 446 + 16 * i
    mbr[o] = boot; mbr[o + 1:o + 4] = b"\xfe\xff\xff"; mbr[o + 4] = tipo; mbr[o + 5:o + 8] = b"\xfe\xff\xff"
    struct.pack_into("<II", mbr, o + 8, ini, n)
entrada(0, 0x80, 0x07, p1, n1)
entrada(1, 0x00, 0x07, p2, n2)
mbr[510:512] = b"\x55\xaa"
with open(img, "r+b") as f: f.write(mbr)
EOF
pone() {   # pone INICIO SECTORES ETIQUETA ORIGEN SERIE: un NTFS en esa particion con lo de ORIGEN dentro (con sus fechas)
	local l m
	l=$(losetup -f --show -o $(($1 * 512)) --sizelimit $(($2 * 512)) "$I")
	mkntfs -Q -q -F -L "$3" -p "$1" -H 255 -S 63 "$l"
	# (un numero de serie -el UUID- distinto en cada una, como en un disco de verdad: mkntfs en el mismo segundo los repite)
	ntfslabel --new-serial="$5" "$l" >/dev/null
	m=$(mktemp -d)
	ntfs-3g "$l" "$m"
	cp -r --preserve=timestamps "$4/." "$m/"
	sync; umount "$m"; rmdir "$m"
	losetup -d "$l"
}
pone "$P1" "$N1" XP "$W/c" 0x1a2b3c4d5e6f7081
pone "$P2" "$N2" MUSICA "$W/d" 0x2b3c4d5e6f708192
echo "listo: $I ($(du -h "$I" | cut -f1) ocupados)"
