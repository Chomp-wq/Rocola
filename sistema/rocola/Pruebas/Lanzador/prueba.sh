#!/bin/bash
# LA PRUEBA DEL LANZADOR (/usr/bin/rocola) CON EL INTERRUPTOR DE rocola-xp (sistema/INTEGRACION_ROCOLA_XP.md). Con programas falsos
# (falso.c: apuntan como los llamaron, su entorno y la senal que los cierra) y el BusyBox DEL SISTEMA (el de la maquina de pruebas tiene
# cosas que el del sistema no: AGENTS.md), en una raiz aparte y en su propio espacio de procesos (los killall del lanzador no tocan nada
# de fuera). Hace falta ser root, gcc y un sistema ya compilado (ROCOLA_TRABAJO/salida-qemu/target: su bin/busybox y sus bibliotecas).
#   bash prueba.sh [fabrica xp caidas datosxp sinprograma otracarpeta buzon]
# fabrica: sin interruptor, el lanzador de ahora hace lo mismo que el de antes del cambio (ANTES: el commit con que comparar, 7113804).
set -e
AQUI=$(cd "$(dirname "$0")" && pwd); REPO=$(cd "$AQUI/../../../.." && pwd)
T=${ROCOLA_TRABAJO:-$HOME/rocola-trabajo}/salida-qemu/target
O=$REPO/sistema/board/rocola/rootfs_overlay
R=$(mktemp -d)
trap 'rm -rf "$R"' EXIT
mkdir -p "$R/bin" "$R/lib" "$R/usr/bin" "$R/usr/lib/rocola-xp" "$R/run" "$R/proc" "$R/root" "$R/tmp" "$R/rocola/datos" "$R/asound/card0/pcm0p"
cp "$T/bin/busybox" "$R/bin/"; for l in ld-linux-x86-64.so.2 libc.so.6 libm.so.6 libresolv.so.2; do cp -L "$T/lib/$l" "$R/lib/"; done; ln -s lib "$R/lib64"
mkdir -p "$R/dev"; mknod "$R/dev/null" c 1 3
chroot "$R" /bin/busybox --install -s /bin
gcc -static -O1 -w -o "$R/usr/bin/rocola-consola" "$AQUI/falso.c"; cp "$R/usr/bin/rocola-consola" "$R/usr/lib/rocola-xp/rocola-xp"
echo "rocola-xp programa 0 (prueba) compilado 1700000000" > "$R/usr/lib/rocola-xp/version"
# una tarjeta de sonido como la de la placa (rocola-tarjeta: ROCOLA_ASOUND)
printf ' 0 [PCH            ]: HDA-Intel - HDA Intel PCH\n' > "$R/asound/cards"; echo PCH > "$R/asound/card0/id"; echo 'name: ALC887 Analog' > "$R/asound/card0/pcm0p/info"
cp "$O/usr/bin/rocola" "$O/usr/bin/rocola-tarjeta" "$O/usr/bin/rocola-buzon" "$R/usr/bin/"
git -C "$REPO" show "${ANTES:-7113804}:sistema/board/rocola/rootfs_overlay/usr/bin/rocola" > "$R/usr/bin/rocola-antes"
cp "$AQUI/dentro.sh" "$R/prueba.sh"; chmod 755 "$R/usr/bin/"* "$R/prueba.sh"
chroot "$R" /bin/sh -n /usr/bin/rocola
[ $# -gt 0 ] || set -- fabrica xp caidas datosxp sinprograma otracarpeta buzon
for t in "$@"; do echo "=== $t"; unshare --pid --fork --mount chroot "$R" /prueba.sh "$t"; done
