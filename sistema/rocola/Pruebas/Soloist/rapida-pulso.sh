#!/bin/bash
# LA PRUEBA DEL PASO 5b DE sistema/rapida.sh (rocola-pulso en la compilacion rapida), sin GitHub ni un sistema montado: se saca ese paso de
# rapida.sh tal cual y se corre contra un rocola-pulso DE MENTIRA (un libpulse.so.0 con funciones vacias y su Makefile con el objetivo
# «todo», como el de verdad: el codigo de verdad lo escribe el grupo «pulso») y una raiz con la libc y la libm del sistema compilado
# (ROCOLA_TRABAJO/salida-qemu/target). Casos: con todas las funciones que busca Soloist (se pone), con una de menos (no se pone), y si
# no compila (no se pone). Cada comprobacion dice BIEN o MAL.
#   bash rapida-pulso.sh
AQUI=$(cd "$(dirname "$0")" && pwd); REPO=$(cd "$AQUI/../../../.." && pwd)
T=${ROCOLA_TRABAJO:-$HOME/rocola-trabajo}/salida-qemu/target
W=$(mktemp -d -p "${ROCOLA_PRUEBA_TRABAJO:-/tmp}"); trap 'rm -rf "$W"' EXIT
FUN=$REPO/sistema/package/rocola-pulso/funciones.txt
bien=0; mal=0
ok() { if [ "$1" = "$2" ]; then echo "  BIEN: $3 [$1]"; bien=$((bien + 1)); else echo "  MAL: $3 [sale «$1», se esperaba «$2»]"; mal=$((mal + 1)); fi; }
# el paso 5b, tal cual (de «# 5b.» a «# 6.»), en una funcion; sudo, cambio y con_pulso de mentira
sed -n '/^# 5b\. /,/^# 6\. /p' "$REPO/sistema/rapida.sh" | sed '$d' > "$W/paso.sh"
[ -s "$W/paso.sh" ] || { echo "  MAL: no se encontro el paso 5b en rapida.sh"; exit 1; }
corre() {   # corre CARPETA_DE_PULSO [vieja] -> 0 si lo puso (con «vieja», una libc de mentira que solo tiene GLIBC_2.2.5)
	rm -rf "$W/M" "$W/T"; mkdir -p "$W/M/lib" "$W/T"; cp -L "$T/lib/libc.so.6" "$T/lib/libm.so.6" "$W/M/lib/"
	if [ "$2" = vieja ]; then
		echo 'int clock_getres(void) { return 0; }' > "$W/c.c"; echo 'GLIBC_2.2.5 { global: *; };' > "$W/v.map"
		gcc -shared -nostdlib -Wl,--version-script="$W/v.map" -o "$W/M/lib/libc.so.6" "$W/c.c"; cp "$W/M/lib/libc.so.6" "$W/M/lib/libm.so.6"
	fi
	mkdir -p "$W/repo/sistema/rocola" "$W/repo/sistema/package/rocola-pulso"; rm -rf "$W/repo/sistema/rocola/pulso"
	cp -a "$1" "$W/repo/sistema/rocola/pulso"; cp "$FUN" "$W/repo/sistema/package/rocola-pulso/"
	( set -e; REPO=$W/repo; M=$W/M; T=$W/T
	  sudo() { "$@"; }; cambio() { return 0; }; con_pulso() { return 0; }
	  . "$W/paso.sh" ) > "$W/salida.txt" 2>&1
	r=$?; sed 's/^/    /' "$W/salida.txt"; return $r
}
haz_pulso() {   # haz_pulso CARPETA [FUNCION_QUE_FALTA] [roto]
	mkdir -p "$1"
	{ echo '/* rocola-pulso DE MENTIRA (solo para probar rapida.sh): las funciones vacias */'
	  grep -v '^#' "$FUN" | grep -vx "${2:-ninguna}" | sed 's/.*/void &(void) {}/'
	  echo '#include <time.h>'; echo 'long rocola_reloj(void) { struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t); return t.tv_sec; }'
	  [ "$3" = roto ] && echo 'esto no compila'; } > "$1/pulso.c"
	printf 'todo: libpulse.so.0\nlibpulse.so.0: pulso.c\n\t$(CC) $(CFLAGS) -shared -Wl,-soname,libpulse.so.0 -o $@ pulso.c\npruebas:\nlimpia:\n\trm -f libpulse.so.0\n' > "$1/Makefile"
}
echo "=== rapida-pulso"
haz_pulso "$W/p1"; corre "$W/p1"; r=$?
ok "$r/$([ -f "$W/M/usr/lib/rocola-pulso/libpulse.so.0" ] && echo puesto)/$(grep -c 'con las 48 funciones' "$W/salida.txt")" "0/puesto/1" "con las 48 funciones: compila, comprueba y lo pone"
ok "$(grep -o 'glibc hasta [^ ]*' "$W/salida.txt")" "glibc hasta GLIBC_2.17" "dice hasta que glibc pide (clock_gettime: GLIBC_2.17)"
corre "$W/p1" vieja; r=$?
ok "$r/$([ -f "$W/M/usr/lib/rocola-pulso/libpulse.so.0" ] && echo puesto)/$(grep -c 'pide GLIBC_2.17 y el sistema no la tiene' "$W/salida.txt")" "1//1" "pide una glibc que el sistema no tiene: no se pone y lo dice"
haz_pulso "$W/p2" pa_stream_write; corre "$W/p2"; r=$?
ok "$r/$([ -f "$W/M/usr/lib/rocola-pulso/libpulse.so.0" ] && echo puesto)/$(grep -c 'no da pa_stream_write' "$W/salida.txt")" "1//1" "le falta pa_stream_write: no se pone y lo dice"
haz_pulso "$W/p3" "" roto; corre "$W/p3"; r=$?
ok "$r/$([ -f "$W/M/usr/lib/rocola-pulso/libpulse.so.0" ] && echo puesto)/$(grep -c 'rocola-pulso no compilo' "$W/salida.txt")" "1//1" "no compila: no se pone y lo dice"
echo "=== RESUMEN: $bien BIEN, $mal MAL"
