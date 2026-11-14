#!/bin/bash
# LA PRUEBA DE LOS GUIONES DE ROCOLA OS CON rocola-xp COMO UNICO PROGRAMA (decision del dueno del 14-11-2026; sistema/INTEGRACION_ROCOLA_XP.md):
# el lanzador (/usr/bin/rocola), el boton de encendido, la confirmacion de un sistema nuevo (S99rocola), rocola-buzon, rocola-actualiza,
# rocola-sonido y rocola-prueba. Con un rocola-xp FALSO (falso.c: apunta como lo llamaron, su entorno, la hora y la senal que lo cierra;
# hace tambien de importador, «--importa-xp»), programas de mentira para lo que toca el equipo (poweroff, reboot, amixer, aplay,
# speaker-test, mpg123, kmscube: solo apuntan que los llamaron), con-terminal (terminal.c: el lanzador con una terminal de control, como
# en tty1; el caso «colgar»), un GitHub de mentira (gh_falso.py, en 127.0.0.1) y el BusyBox DEL
# SISTEMA (el de la maquina de pruebas tiene cosas que el del sistema no: AGENTS.md), con el curl y el jq del sistema, en una raiz aparte
# y en su propio espacio de procesos (los killall de los guiones no tocan nada de fuera). Hace falta ser root, gcc, python3 y un sistema
# ya compilado (ROCOLA_TRABAJO/salida-qemu/target: su bin/busybox, curl, jq y sus bibliotecas).
#   bash prueba.sh [CASO...]   sin nada, todos (los casos estan en dentro.sh); cada comprobacion dice BIEN o MAL, y al final cuantas
#   ROCOLA_XP_REAL=.../rocola-xp: el caso «contrato» llama al importador DE VERDAD (rocola-xp --importa-xp, NativeAOT) como lo llama el
#   lanzador; sin la variable ese caso no prueba nada.
set -e
AQUI=$(cd "$(dirname "$0")" && pwd); REPO=$(cd "$AQUI/../../../.." && pwd)
T=${ROCOLA_TRABAJO:-$HOME/rocola-trabajo}/salida-qemu/target
O=$REPO/sistema/board/rocola/rootfs_overlay
R=$(mktemp -d)
GH=
trap '[ -n "$GH" ] && kill $GH 2>/dev/null; rm -rf "$R"' EXIT
mkdir -p "$R/bin" "$R/lib" "$R/usr/bin" "$R/usr/lib/rocola-xp" "$R/usr/share/rocola" "$R/etc/acpi/PWRF" "$R/etc/init.d" "$R/run" "$R/proc" \
	"$R/root" "$R/tmp" "$R/rocola/datos" "$R/asound/card0/pcm0p" "$R/asound/card1/pcm0p" "$R/dev/dri" "$R/gh"
cp "$T/bin/busybox" "$R/bin/"
for l in ld-linux-x86-64.so.2 libc.so.6 libm.so.6 libresolv.so.2 libatomic.so.1; do cp -L "$T/lib/$l" "$R/lib/"; done
for l in libcurl.so.4 libssl.so.3 libcrypto.so.3 libz.so.1 libjq.so.1; do cp -L "$T/usr/lib/$l" "$R/lib/"; done
cp "$T/usr/bin/curl" "$T/usr/bin/jq" "$R/usr/bin/"
ln -s lib "$R/lib64"
mknod "$R/dev/null" c 1 3; : > "$R/dev/dri/card0"; mkdir -p "$R/dev/pts"
chroot "$R" /bin/busybox --install -s /bin
gcc -static -O1 -w -o "$R/usr/lib/rocola-xp/rocola-xp" "$AQUI/falso.c"
gcc -static -O1 -w -o "$R/usr/bin/con-terminal" "$AQUI/terminal.c"   # (el caso «colgar»: el lanzador con su terminal de control, como en tty1)
[ -n "$ROCOLA_XP_REAL" ] && mkdir -p "$R/usr/lib/rocola-xp/real" && cp "$ROCOLA_XP_REAL" "$R/usr/lib/rocola-xp/real/rocola-xp"
echo "rocola-xp programa 0 (local) compilado 1700000000" > "$R/usr/lib/rocola-xp/version"
# dos tarjetas de sonido (rocola-tarjeta las lee de ROCOLA_ASOUND): la de la placa (la 0) y una USB (para «rocola-sonido usar 2»)
printf ' 0 [PCH            ]: HDA-Intel - HDA Intel PCH\n 1 [Device         ]: USB-Audio - USB Audio Device\n' > "$R/asound/cards"
echo PCH > "$R/asound/card0/id"; echo 'name: ALC887 Analog' > "$R/asound/card0/pcm0p/info"
echo Device > "$R/asound/card1/id"; echo 'name: USB Audio' > "$R/asound/card1/pcm0p/info"
# los guiones del sistema, tal cual
for g in usr/bin/rocola usr/bin/rocola-tarjeta usr/bin/rocola-buzon usr/bin/rocola-actualiza usr/bin/rocola-sonido usr/bin/rocola-prueba \
	etc/acpi/PWRF/00000080 etc/init.d/S99rocola; do cp "$O/$g" "$R/$g"; done
cp "$REPO/sistema/rocola/Consola/prueba.mp3" "$R/usr/share/rocola/prueba.mp3"
# los programas de mentira (apuntan que los llamaron: nada se apaga, ni suena, ni se dibuja de verdad)
for p in poweroff reboot mpg123 kmscube; do printf '#!/bin/sh\necho "%s: $*" >> /tmp/falso.log\n' "$p" > "$R/usr/bin/$p"; done
printf '#!/bin/sh\nexit 0\n' > "$R/usr/bin/amixer"
printf '#!/bin/sh\n[ "$1" = -l ] && printf "card 0: PCH [HDA Intel PCH], device 0: ALC887 Analog [ALC887 Analog]\\ncard 1: Device [USB Audio Device], device 0: USB Audio [USB Audio]\\n"\nexit 0\n' > "$R/usr/bin/aplay"
printf '#!/bin/sh\necho "speaker-test: $*" >> /tmp/falso.log\nexec sleep 10\n' > "$R/usr/bin/speaker-test"
cp "$AQUI/dentro.sh" "$R/prueba.sh"
chmod 755 "$R/usr/bin/"* "$R/prueba.sh" "$R/etc/acpi/PWRF/00000080" "$R/etc/init.d/S99rocola"
# sh -n de todos los guiones, con el BusyBox del sistema
for g in usr/bin/rocola usr/bin/rocola-buzon usr/bin/rocola-actualiza usr/bin/rocola-sonido usr/bin/rocola-prueba etc/acpi/PWRF/00000080 etc/init.d/S99rocola; do
	chroot "$R" /bin/sh -n "/$g" && echo "sh -n (BusyBox del sistema) bien: /$g"
done
# el GitHub de mentira (fuera de la raiz; escucha en 127.0.0.1, que se ve igual desde dentro)
python3 -I "$AQUI/gh_falso.py" "$R/gh" "$R/tmp/gh.puerto" & GH=$!
for i in $(seq 30); do [ -s "$R/tmp/gh.puerto" ] && break; sleep 0.2; done
[ $# -gt 0 ] || set -- arranque ia importa caidas confirma consola reinicia boton colgar sinprograma sonido prueba s99 buzon actualiza contrato
for t in "$@"; do echo "=== $t"; unshare --pid --fork --mount chroot "$R" /prueba.sh "$t" | tee -a "$R/tmp/resumen.txt"; done
echo "=== RESUMEN: $(grep -c '^  BIEN' "$R/tmp/resumen.txt") BIEN, $(grep -c '^  MAL' "$R/tmp/resumen.txt") MAL"
