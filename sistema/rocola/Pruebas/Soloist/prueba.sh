#!/bin/bash
# LA PRUEBA DE SPOTIFY CON SOLOIST POR EL LADO DEL SISTEMA (sistema/SPOTIFY_SOLOIST.md): el vigilante rocola-soloist, la orden «spotify» de
# la consola, las llaves de Spotify en rocola-buzon y ROCOLA_OS en el lanzador. Como la del lanzador (Pruebas/Lanzador/prueba.sh): con el
# BusyBox DEL SISTEMA (el de la maquina de pruebas tiene cosas que el del sistema no: sin stat, tar sin -z...), su curl y su jq, en una raiz
# aparte y en su propio espacio de procesos. NUNCA con el Soloist de verdad: uno FALSO en guion (soloist-falso, metido en .tar.gz de
# mentira), un servidor de mentira que hace de soloist-builds.spotifycdn.com, de apresolve.spotify.com y del WebSocket (servidor.py, en
# 127.0.0.1) y una memoria USB de mentira (una imagen FAT en un loop, con un /sys/class/block de mentira). Todo inventado.
# Hace falta ser root, python3 y un sistema ya compilado (ROCOLA_TRABAJO/salida-qemu/target: su bin/busybox, curl, jq y sus bibliotecas).
#   bash prueba.sh [CASO...]   sin nada, todos (los casos estan en dentro.sh); cada comprobacion dice BIEN o MAL, y al final cuantas
set -e
AQUI=$(cd "$(dirname "$0")" && pwd); REPO=$(cd "$AQUI/../../../.." && pwd)
T=${ROCOLA_TRABAJO:-$HOME/rocola-trabajo}/salida-qemu/target
O=$REPO/sistema/board/rocola/rootfs_overlay
TRAB=${ROCOLA_PRUEBA_TRABAJO:-/tmp}
R=$(mktemp -d -p "$TRAB" soloist-raiz.XXXXXX)
SRV=; LOOP=
fin() {
	[ -n "$SRV" ] && kill $SRV 2>/dev/null
	[ -n "$LOOP" ] && losetup -d "$LOOP" 2>/dev/null
	if [ -n "$ROCOLA_PRUEBA_DEJA" ]; then echo "(la raiz de prueba se queda en $R)"; else rm -rf "$R"; fi; rm -f "$R.usb.img"
}
trap fin EXIT
mkdir -p "$R/bin" "$R/lib" "$R/usr/bin" "$R/usr/lib/rocola-pulso" "$R/usr/lib/rocola-xp" "$R/run" "$R/proc" "$R/tmp" "$R/rocola/datos/xp" \
	"$R/rocola/discos" "$R/srv" "$R/tars" "$R/dev" "$R/fsys/usbdisk/loop99" "$R/fsys/block" "$R/asound/card0/pcm0p" "$R/root" "$R/etc"
cp "$T/bin/busybox" "$R/bin/"
for l in ld-linux-x86-64.so.2 libc.so.6 libm.so.6 libresolv.so.2 libatomic.so.1; do cp -L "$T/lib/$l" "$R/lib/"; done
for l in libcurl.so.4 libssl.so.3 libcrypto.so.3 libz.so.1 libjq.so.1; do cp -L "$T/usr/lib/$l" "$R/lib/"; done
cp "$T/usr/bin/curl" "$T/usr/bin/jq" "$R/usr/bin/"
ln -s lib "$R/lib64"
mknod "$R/dev/null" c 1 3
chroot "$R" /bin/busybox --install -s /bin
# los guiones del sistema, tal cual
for g in rocola-soloist spotify rocola-buzon rocola rocola-tarjeta; do cp "$O/usr/bin/$g" "$R/usr/bin/$g"; done
# un libpulse.so.0 cualquiera, para que «spotify prueba» lo encuentre (no se carga: el soloist falso es un guion)
echo "(rocola-pulso de mentira)" > "$R/usr/lib/rocola-pulso/libpulse.so.0"
# lo que el lanzador necesita (como Pruebas/Lanzador): una tarjeta de sonido, amixer y aplay de mentira, y un rocola-xp FALSO en guion que
# solo apunta su ROCOLA_OS
printf ' 0 [PCH            ]: HDA-Intel - HDA Intel PCH\n' > "$R/asound/cards"; echo PCH > "$R/asound/card0/id"
echo 'name: ALC887 Analog' > "$R/asound/card0/pcm0p/info"
printf '#!/bin/sh\nexit 0\n' > "$R/usr/bin/amixer"
printf '#!/bin/sh\n[ "$1" = -l ] && printf "card 0: PCH [HDA Intel PCH], device 0: ALC887 Analog [ALC887 Analog]\\n"\nexit 0\n' > "$R/usr/bin/aplay"
cat > "$R/usr/lib/rocola-xp/rocola-xp" <<'EOF'
#!/bin/sh
[ "$1" = --version ] && { echo "rocola-xp programa 0 (local) compilado 1700000000"; exit 0; }
echo "rocola-xp ${1:-rocola}: ROCOLA_OS=[${ROCOLA_OS-SIN}]" >> /tmp/xp.log
[ "$1" = --importa-xp ] && exit 0
trap 'exit 0' TERM USR1
while :; do sleep 1 & wait $!; done
EOF
# EL SOLOIST FALSO, en sus .tar.gz de mentira (como el de verdad: soloist, CHANGELOG.md y THIRD_PARTY_LICENSES.txt): las versiones 1.0.0
# (le quedan 60 dias), 2.0.0 (89) y 4.0.0 (3: caduca pronto); uno roto (cortado), uno sin «soloist» y uno grande (para llenar el disco)
haz_tar() {   # haz_tar NOMBRE VER DIAS [relleno_KB]
	d=$(mktemp -d -p "$TRAB"); sed -e "s/@VER@/$2/" -e "s/@DIAS@/$3/" "$AQUI/soloist-falso" > "$d/soloist"; chmod 555 "$d/soloist"
	printf '# Soloist Changelog\n\n## Release v%s\n' "$2" > "$d/CHANGELOG.md"; echo "(licencias de mentira)" > "$d/THIRD_PARTY_LICENSES.txt"
	[ -n "$4" ] && head -c "$(( $4 * 1024 ))" /dev/urandom > "$d/relleno.bin"
	(cd "$d" && tar --owner=0 --group=0 -czf "$R/tars/$1.tar.gz" *); rm -rf "$d"
}
haz_tar v1 1.0.0 60; haz_tar v2 2.0.0 89; haz_tar v4 4.0.0 3; haz_tar grande 5.0.0 80 2048
head -c "$(( $(wc -c < "$R/tars/v2.tar.gz") / 2 ))" "$R/tars/v2.tar.gz" > "$R/tars/roto.tar.gz"
d=$(mktemp -d -p "$TRAB"); echo hola > "$d/LEEME"; (cd "$d" && tar -czf "$R/tars/sinsoloist.tar.gz" LEEME); rm -rf "$d"
# UN PROGRAMA QUE SOLO ESPERA (estatico, para la raiz): hace de lo que quede del programa de Soloist fuera de su grupo (caso «restos»)
if command -v gcc >/dev/null; then
	printf '#include <unistd.h>\nint main(void) { for (;;) pause(); }\n' > "$R/tars/dormilon.c"
	gcc -static -O2 -o "$R/tars/dormilon" "$R/tars/dormilon.c" 2>/dev/null || rm -f "$R/tars/dormilon"; rm -f "$R/tars/dormilon.c"
fi
# EL ANILLO de mentira (la cabecera de I1 con un flujo a 44100 Hz sonando; los latidos, de ahora): para «spotify estado» y «prueba»
python3 -I - "$R/tars/anillo" <<'EOF'
import struct, sys, time
n = time.clock_gettime_ns(time.CLOCK_MONOTONIC)
c = struct.pack("<IIIIIIII", 0x4F4C5550, 1, 65536, 2, 44100, 1, 0, 1) + struct.pack("<QQQQQ", 441000, 440000, 0, n, n)
c += struct.pack("<IIIIQI", 1234, 1, 65536, 5, 0, 20000)
open(sys.argv[1], "wb").write(c + bytes(4096 - len(c)) + bytes(65536 * 8))
# (y el recien creado por la rocola, antes de que nadie escriba: la magia y lo demas a cero; con ceros seguidos «od» sin -v pone «*»)
c0 = struct.pack("<IIIIIIII", 0x4F4C5550, 1, 65536, 2, 0, 0, 0, 0)
open(sys.argv[1] + "0", "wb").write(c0 + bytes(4096 - len(c0)) + bytes(65536 * 8))
EOF
cp "$AQUI/dentro.sh" "$R/prueba.sh"; chmod 755 "$R/usr/bin/"* "$R/usr/lib/rocola-xp/rocola-xp" "$R/prueba.sh"
# LA MEMORIA USB DE MENTIRA: una imagen en un loop (como /dev/loop99 dentro), con las dos llaves inventadas (CRLF y la marca de UTF-8,
# como las deja el Bloc de notas de Windows), y su /sys/class/block de mentira (removible). FAT si el nucleo de la maquina de pruebas lo
# tiene (las memorias de verdad); si no, ext2 (a rocola-soloist le da igual: «mount -o ro» la monta sea cual sea).
truncate -s 4M "$R.usb.img"
if grep -qw vfat /proc/filesystems; then mkfs.vfat "$R.usb.img" >/dev/null; else mke2fs -q -t ext2 "$R.usb.img"; fi
if LOOP=$(losetup -f --show "$R.usb.img" 2>/dev/null); then
	mm=$(mktemp -d -p "$TRAB"); mount "$LOOP" "$mm"
	printf '\357\273\277llaveUSB_soloist_ABCdef123456\r\n' > "$mm/rocola_spotify.txt"
	printf '\357\273\277client_id = idUSB0123456789\r\nclient_secret=secretoUSB_9876543210\r\notra=cosa\r\nrefresh_token=refrescoUSB_555\r\n' > "$mm/rocola_spotify_api.txt"
	umount "$mm"; rmdir "$mm"
	mknod "$R/dev/loop99" b 7 "${LOOP#/dev/loop}"
	echo 1 > "$R/fsys/usbdisk/removable"; echo 1 > "$R/fsys/usbdisk/loop99/partition"
	ln -s ../usbdisk/loop99 "$R/fsys/block/loop99"; ln -s ../usbdisk "$R/fsys/block/usbdisk"
else echo "(sin loop: el caso «usb» no probara la memoria removible)"; LOOP=; fi
# sh -n de cada guion tocado, con el BusyBox del sistema
for g in rocola-soloist spotify rocola-buzon rocola; do chroot "$R" /bin/sh -n "/usr/bin/$g" && echo "sh -n (BusyBox del sistema) bien: /usr/bin/$g"; done
# el servidor de mentira (fuera de la raiz; escucha en 127.0.0.1, que se ve igual desde dentro)
python3 -I "$AQUI/servidor.py" "$R/srv" "$R/tmp/srv.puerto" & SRV=$!
for i in $(seq 30); do [ -s "$R/tmp/srv.puerto" ] && break; sleep 0.2; done
[ $# -gt 0 ] || set -- baja caido roto lleno sindisco caducado pronto caidas lento dos padre sigterm usb secreto encender estado prueba \
	emparejar entorno lanzador restos padrebaja cambio
for t in "$@"; do echo "=== $t"; unshare --pid --fork --mount chroot "$R" /prueba.sh "$t" | tee -a "$R/resumen.txt"; done
echo "=== RESUMEN: $(grep -c '^  BIEN' "$R/resumen.txt") BIEN, $(grep -c '^  MAL' "$R/resumen.txt") MAL"
