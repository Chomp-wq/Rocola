#!/bin/sh
# SPDX-License-Identifier: MIT
# Todas las pruebas de rocola-pulso (make -C .. pruebas, o este guion).
# Sin root; los anillos van en una carpeta temporal (PRUEBA_TMP o mktemp).
#   prueba.sh [rapido]   rapido: sin ThreadSanitizer, AddressSanitizer ni valgrind
# Al final: una linea por paso (BIEN/MAL/SALTADO) y el total.
cd "$(dirname "$0")" || exit 2
AQUI=$(pwd)
TMP=${PRUEBA_TMP:-$(mktemp -d /tmp/rocola-pulso.XXXXXX)}
mkdir -p "$TMP"
RAPIDO=${1:-}
BIEN=0; MAL=0; RESUMEN=""
paso() { # paso NOMBRE ESTADO DETALLE
    RESUMEN="$RESUMEN
$2  $1: $3"
    case $2 in BIEN) BIEN=$((BIEN + 1));; MAL) MAL=$((MAL + 1));; esac
}
pesado() { # los arneses grandes, de uno en uno con los demas trabajos
    if [ -w /root/rocola-trabajo/fase11/pesado.lock ] 2>/dev/null; then flock /root/rocola-trabajo/fase11/pesado.lock "$@"; else "$@"; fi
}

echo "== compilar"
if make -s -C .. >"$TMP/make.txt" 2>&1 && make -s todo >>"$TMP/make.txt" 2>&1; then
    avisos=$(grep -c 'warning' "$TMP/make.txt")
    paso compilar BIEN "libpulse.so.0 y las pruebas ($avisos avisos del compilador)"
else
    cat "$TMP/make.txt"; paso compilar MAL "no compila"; echo "$RESUMEN"; exit 1
fi
LIB="$AQUI/../libpulse.so.0"

echo "== (5) lo que exporta y lo que pide"
nm -D --defined-only "$LIB" | awk '{print $3}' | sed 's/@.*//' | sort -u | grep -v '^PULSE_0$' >"$TMP/exporta.txt"
total=$(wc -l <"$TMP/exporta.txt")
nopa=$(grep -vc '^pa_' "$TMP/exporta.txt")
faltan=$(sort soloist-funciones.txt | comm -23 - "$TMP/exporta.txt" | wc -l)
glibc=$(objdump -T "$LIB" | grep -o 'GLIBC_[0-9.]*' | sort -uV | tail -1)
necesita=$(readelf -d "$LIB" | awk '/NEEDED/ {print $5}' | tr -d '[]' | tr '\n' ' ')
soname=$(readelf -d "$LIB" | awk '/SONAME/ {print $5}' | tr -d '[]')
version=$(objdump -T "$LIB" | grep -c 'PULSE_0 *pa_')
glibc_ok=$(printf '%s\nGLIBC_2.34\n' "$glibc" | sort -V | tail -1)
if [ "$faltan" = 0 ] && [ "$nopa" = 0 ] && [ "$glibc_ok" = GLIBC_2.34 ] && [ "$necesita" = "libc.so.6 " ] && [ "$soname" = libpulse.so.0 ]; then
    paso exporta BIEN "las 48 de Soloist de $total funciones pa_* (version PULSE_0: $version), 0 fuera de pa_*, glibc maxima $glibc, necesita: $necesita, soname $soname"
else
    paso exporta MAL "faltan $faltan, fuera de pa_* $nopa, glibc $glibc, necesita $necesita, soname $soname"
fi

echo "== (5b) ABI contra las cabeceras publicas de libpulse"
./bin/abi-real | grep -v '^#' >"$TMP/abi-real.txt" && ./bin/abi-nuestro | grep -v '^#' >"$TMP/abi-nuestro.txt"
if [ -s "$TMP/abi-real.txt" ] && diff "$TMP/abi-real.txt" "$TMP/abi-nuestro.txt" >"$TMP/abi-diff.txt"; then
    paso abi BIEN "$(wc -l <"$TMP/abi-real.txt") tamanos, desplazamientos y valores iguales a $(./bin/abi-real | sed -n 's/^# cabeceras: //p')"
else
    paso abi MAL "$(cat "$TMP/abi-diff.txt" | head -5 | tr '\n' ' ')"
fi
# Las firmas: cada prototipo de pulso_api.h compilado junto a las cabeceras de
# verdad (un tipo distinto en un argumento o en lo que devuelve no compila).
# Tres toman las funciones de aviso como void * (la misma ABI): fuera.
firmas() { # firmas [carpeta de cabeceras]
    sed -e '/#include "pulso_abi.h"/d' -e '/pa_update_mode_t;/d' ../pulso_api.h |
        grep -v -E 'pa_context_set_event_callback|pa_context_get_source_info_(list|by_index)' >"$TMP/firmas.h"
    printf '#include <pulse/pulseaudio.h>\n#include <pulse/mainloop-signal.h>\n#include "%s"\n' "$TMP/firmas.h" |
        ${CC:-gcc} ${1:+-I"$1"} -fsyntax-only -Werror -x c - >"$TMP/firmas-error.txt" 2>&1
}
if firmas; then
    paso firmas BIEN "$(grep -c '^[a-z].*(' "$TMP/firmas.h") prototipos iguales a los de las cabeceras del sistema"
else
    paso firmas MAL "$(grep -m3 error "$TMP/firmas-error.txt" | tr '\n' ' ')"
fi
if [ -n "${PULSO_CABECERAS:-}" ]; then
    if firmas "$PULSO_CABECERAS"; then
        paso firmas-otras BIEN "$(grep -c '^[a-z].*(' "$TMP/firmas.h") prototipos iguales a los de $PULSO_CABECERAS"
    else
        paso firmas-otras MAL "$(grep -m3 error "$TMP/firmas-error.txt" | tr '\n' ' ')"
    fi
fi
# Las de otra version (PULSO_CABECERAS=<carpeta con pulse/*.h>, p. ej. libpulse 17)
if [ -n "${PULSO_CABECERAS:-}" ]; then
    if make -s abi-otras PULSO_CABECERAS="$PULSO_CABECERAS" >"$TMP/abi-otras-make.txt" 2>&1 &&
        ./bin/abi-otras | grep -v '^#' >"$TMP/abi-otras.txt" && [ -s "$TMP/abi-otras.txt" ] &&
        diff "$TMP/abi-otras.txt" "$TMP/abi-nuestro.txt" >"$TMP/abi-otras-diff.txt"; then
        paso abi-otras BIEN "$(wc -l <"$TMP/abi-otras.txt") iguales a $(./bin/abi-otras | sed -n 's/^# cabeceras: //p')"
    else
        paso abi-otras MAL "$(cat "$TMP/abi-otras-make.txt" "$TMP/abi-otras-diff.txt" 2>/dev/null | head -5 | tr '\n' ' ')"
    fi
else
    paso abi-otras SALTADO "sin PULSO_CABECERAS"
fi

echo "== (1) el cliente como Soloist (18 escenarios)"
mkdir -p "$TMP/cliente"
if ROCOLA_SPOTIFY_ANILLO="$TMP/cliente/run/rocola-xp/spotify/sonido.anillo" ./bin/cliente "$LIB" >"$TMP/cliente.txt" 2>&1; then
    paso cliente BIEN "$(tail -1 "$TMP/cliente.txt")"
else
    paso cliente MAL "$(grep -m3 MAL "$TMP/cliente.txt" | tr '\n' ' ') $(tail -1 "$TMP/cliente.txt")"
fi
sed 's/^/    /' "$TMP/cliente.txt" | grep -v '^    cliente:'

echo "== (2) lo raro"
mkdir -p "$TMP/raro"
if PRUEBA_DIR="$TMP/raro" ./bin/raro >"$TMP/raro.txt" 2>"$TMP/raro.err"; then
    paso raro BIEN "$(tail -1 "$TMP/raro.txt")"
else
    paso raro MAL "$(grep -m3 MAL "$TMP/raro.err" | tr '\n' ' ') $(tail -1 "$TMP/raro.txt")"
fi
sed 's/^/    /' "$TMP/raro.txt" | grep -v '^    raro:'

echo "== (2b) hilos y robustez (carreras con vaciados, anillo rehecho, ciclos, cabecera, consumo)"
mkdir -p "$TMP/hilos"
if PRUEBA_DIR="$TMP/hilos" ./bin/hilos >"$TMP/hilos.txt" 2>"$TMP/hilos.err"; then
    paso hilos BIEN "$(tail -1 "$TMP/hilos.txt")"
else
    paso hilos MAL "$(grep -m3 MAL "$TMP/hilos.err" | tr '\n' ' ') $(tail -1 "$TMP/hilos.txt")"
fi
sed 's/^/    /' "$TMP/hilos.txt" | grep -v '^    hilos:'

echo "== (4) clientes de verdad de PulseAudio con nuestra libpulse"
real() { # real NOMBRE BITS CUADROS orden...
    n=$1; b=$2; esperados=$3; shift 3
    ROCOLA_SPOTIFY_ANILLO="$TMP/real/$n/sonido.anillo" ./bin/lector --bits "$b" --reporte "$TMP/real/$n.rep" &
    L=$!
    sleep 0.1
    ROCOLA_SPOTIFY_ANILLO="$TMP/real/$n/sonido.anillo" LD_LIBRARY_PATH="$AQUI/.." timeout 30 "$@" >"$TMP/real/$n.out" 2>&1
    r=$?
    sleep 0.3
    kill $L; wait $L 2>/dev/null
    c=$(sed -n 's/^cuadros=//p' "$TMP/real/$n.rep"); e=$(sed -n 's/^errores=//p' "$TMP/real/$n.rep")
    if [ "$r" = 0 ] && [ "$e" = 0 ] && [ "$c" = "$esperados" ]; then
        paso "real-$n" BIEN "$c cuadros identicos, 0 errores"
    else
        paso "real-$n" MAL "salida $r, $c cuadros (esperados $esperados), $e errores: $(tail -2 "$TMP/real/$n.out" | tr '\n' ' ')"
    fi
}
mkdir -p "$TMP/real"
./bin/genera-wav "$TMP/real/s16.wav" 48000 1.5
./bin/genera-wav "$TMP/real/f32.wav" 44100 1.5 float
tail -c +45 "$TMP/real/s16.wav" >"$TMP/real/s16.raw"
if command -v paplay >/dev/null; then
    real paplay-s16-48000 16 72000 paplay "$TMP/real/s16.wav"
    real paplay-float-44100 16 66150 paplay "$TMP/real/f32.wav"
else paso real-paplay SALTADO "no hay paplay"; fi
if command -v pacat >/dev/null; then
    real pacat-raw 16 72000 pacat --raw --format=s16le --rate=48000 --channels=2 "$TMP/real/s16.raw"
else paso real-pacat SALTADO "no hay pacat"; fi
if command -v mpv >/dev/null; then
    real mpv 16 72000 mpv --no-config --ao=pulse --no-video --audio-channels=stereo "$TMP/real/s16.wav"
else paso real-mpv SALTADO "no hay mpv"; fi

echo "== consumo: 10 s de paplay con nuestra libpulse (el lector en otro proceso)"
if command -v paplay >/dev/null && command -v bash >/dev/null; then
    ./bin/genera-wav "$TMP/real/diez.wav" 48000 10
    ROCOLA_SPOTIFY_ANILLO="$TMP/real/diez/sonido.anillo" ./bin/lector --reporte "$TMP/real/diez.rep" &
    L=$!
    sleep 0.1
    cpu=$(ROCOLA_SPOTIFY_ANILLO="$TMP/real/diez/sonido.anillo" LD_LIBRARY_PATH="$AQUI/.." \
        bash -c 'TIMEFORMAT="%R %U %S"; time paplay "$0" >/dev/null 2>&1' "$TMP/real/diez.wav" 2>&1 | tail -1)
    sleep 0.2
    kill $L; wait $L 2>/dev/null
    c=$(sed -n 's/^cuadros=//p' "$TMP/real/diez.rep"); e=$(sed -n 's/^errores=//p' "$TMP/real/diez.rep")
    set -- $cpu
    if [ "$c" = 480000 ] && [ "$e" = 0 ]; then
        paso consumo BIEN "10 s de paplay: $c cuadros identicos; paplay entero (libsndfile + nuestra libpulse): $1 s de reloj, $2 s de usuario, $3 s de sistema"
    else
        paso consumo MAL "$c cuadros, $e errores, tiempos $cpu"
    fi
fi

echo "== rastreo (ROCOLA_PULSO_TRAZA=1, tope bajado a 30 kB para ver que rota)"
mkdir -p "$TMP/traza"
ROCOLA_PULSO_TRAZA=1 ROCOLA_PULSO_TRAZA_ARCHIVO="$TMP/traza/pulso-traza.txt" ROCOLA_PULSO_TRAZA_TOPE=30000 \
    ROCOLA_SPOTIFY_ANILLO="$TMP/traza/sonido.anillo" ./bin/cliente "$LIB" rapido >/dev/null 2>&1
lin=$(cat "$TMP/traza/pulso-traza.txt" "$TMP/traza/pulso-traza.txt.1" 2>/dev/null | wc -l)
tam=$(wc -c <"$TMP/traza/pulso-traza.txt" 2>/dev/null)
escr=$(cat "$TMP/traza/pulso-traza.txt" "$TMP/traza/pulso-traza.txt.1" 2>/dev/null | grep -c 'pa_stream_write(')
if [ -f "$TMP/traza/pulso-traza.txt.1" ] && [ "${tam:-0}" -lt 40000 ] && [ "$escr" -gt 0 ]; then
    paso rastreo BIEN "$lin lineas ($escr de pa_stream_write), roto a .1, el actual de $tam bytes"
else
    paso rastreo MAL "lineas $lin, tam $tam, writes $escr, .1: $(ls "$TMP/traza")"
fi

if [ "$RAPIDO" != rapido ]; then
    echo "== (3) ThreadSanitizer"
    if pesado make -s VARIANTE=tsan todo >"$TMP/tsan-make.txt" 2>&1; then
        mkdir -p "$TMP/tsan"
        TSAN_OPTIONS="halt_on_error=0 second_deadlock_stack=1" ROCOLA_SPOTIFY_ANILLO="$TMP/tsan/a/sonido.anillo" \
            pesado ./bin-tsan/cliente "$AQUI/bin-tsan/libpulse.so.0" rapido >"$TMP/tsan.txt" 2>&1; r1=$?
        TSAN_OPTIONS="halt_on_error=0" PRUEBA_DIR="$TMP/tsan" pesado ./bin-tsan/raro >>"$TMP/tsan.txt" 2>&1; r2=$?
        TSAN_OPTIONS="halt_on_error=0 second_deadlock_stack=1" HILOS_CORTO=1 PRUEBA_DIR="$TMP/tsan" pesado ./bin-tsan/hilos >>"$TMP/tsan.txt" 2>&1; r3=$?
        n=$(grep -c 'WARNING: ThreadSanitizer' "$TMP/tsan.txt")
        if [ "$n" = 0 ] && [ $r1 = 0 ] && [ $r2 = 0 ] && [ $r3 = 0 ]; then
            paso tsan BIEN "cliente (rapido), los 9 casos raros y los de hilos: 0 avisos; $(grep -h 'comprobaciones' "$TMP/tsan.txt" | tr '\n' ' ')"
        else
            paso tsan MAL "$n avisos (salidas $r1 $r2 $r3): $(grep -m2 -A3 'WARNING: ThreadSanitizer' "$TMP/tsan.txt" | tr '\n' ' ' | cut -c1-400)"
        fi
    else paso tsan MAL "no compila"; fi

    echo "== (3) AddressSanitizer + UBSan"
    if pesado make -s VARIANTE=asan todo >"$TMP/asan-make.txt" 2>&1; then
        mkdir -p "$TMP/asan"
        export ASAN_OPTIONS="detect_leaks=1"
        ROCOLA_SPOTIFY_ANILLO="$TMP/asan/a/sonido.anillo" LD_PRELOAD= pesado ./bin-asan/cliente "$AQUI/bin-asan/libpulse.so.0" >"$TMP/asan.txt" 2>&1; r1=$?
        PRUEBA_DIR="$TMP/asan" pesado ./bin-asan/raro >>"$TMP/asan.txt" 2>&1; r2=$?
        HILOS_CORTO=1 PRUEBA_DIR="$TMP/asan" pesado ./bin-asan/hilos >>"$TMP/asan.txt" 2>&1; r3=$?
        unset ASAN_OPTIONS
        n=$(grep -c -E 'ERROR: (Address|Leak)Sanitizer|runtime error' "$TMP/asan.txt")
        if [ "$n" = 0 ] && [ $r1 = 0 ] && [ $r2 = 0 ] && [ $r3 = 0 ]; then
            paso asan BIEN "cliente (18 escenarios), los 9 casos raros y los de hilos: 0 avisos de memoria, fugas ni comportamiento indefinido; $(grep -h 'comprobaciones' "$TMP/asan.txt" | tr '\n' ' ')"
        else
            paso asan MAL "$n avisos (salidas $r1 $r2 $r3): $(grep -m2 -A4 -E 'ERROR: |runtime error' "$TMP/asan.txt" | tr '\n' ' ' | cut -c1-400)"
        fi
    else paso asan MAL "no compila"; fi

    echo "== (3) valgrind"
    if command -v valgrind >/dev/null; then
        mkdir -p "$TMP/vg"
        ROCOLA_SPOTIFY_ANILLO="$TMP/vg/a/sonido.anillo" pesado valgrind -q --error-exitcode=9 --leak-check=full \
            --errors-for-leak-kinds=definite ./bin/cliente "$LIB" rapido >"$TMP/vg.txt" 2>&1; r=$?
        PRUEBA_DIR="$TMP/vg" pesado valgrind -q --error-exitcode=9 --leak-check=full --errors-for-leak-kinds=definite \
            ./bin/raro nulos >>"$TMP/vg.txt" 2>&1; r2=$?
        if [ $r = 0 ] && [ $r2 = 0 ]; then
            paso valgrind BIEN "cliente (rapido) y los NULL: 0 errores de memoria, 0 fugas definitivas"
        else
            paso valgrind MAL "salidas $r $r2: $(grep -m3 '==' "$TMP/vg.txt" | tr '\n' ' ')"
        fi
    else paso valgrind SALTADO "no hay valgrind"; fi
fi

echo "== resumen (carpeta $TMP)"
echo "$RESUMEN"
echo "rocola-pulso: $BIEN pasos bien, $MAL mal"
[ "$MAL" = 0 ]
