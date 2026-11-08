#!/bin/bash
# corre.sh PROG SALIDA [VAR=valor ...]: la rocola de PROG (carpeta con rocola-xp.dll) fuera de pantalla, con una copia NUEVA de los datos
# de prueba en una carpeta fija (corrida/: la misma ruta para la referencia y para la traduccion, porque la rocola ensena rutas), el sonido
# a tiempo real por una tuberia (ritmo.py) y lo que diga en SALIDA/salida.txt. TIEMPO=segundos (120 de fabrica) la corta con SIGTERM.
set -u
PROG=$1; SAL=$2; shift 2
W=${ROCOLA_TRABAJO:-/root/rocola-trabajo/port/dibujo}
C=$W/corrida
rm -rf "$C"; mkdir -p "$C/runtimes" "$SAL"
cp -p "$PROG"/*.dll "$PROG"/*.json "$PROG"/rocola-xp "$C/" 2>/dev/null; cp -p "$PROG"/*.pdb "$C/" 2>/dev/null
cp -rp "$PROG/runtimes/linux-x64" "$C/runtimes/"
cp -rp "$W/datos-base/." "$C/"
# el estado guardado (creditos y cola) con la hora de ahora: la rocola descarta el de antes de las 7 de la manana (como XP) y los recorridos
# darian otra cosa segun la hora a la que se corran
[ -f "$C/rocola_estado.txt" ] && sed -i "s/^ts=.*/ts=$(( ($(date +%s) + 62135596800) * 10000000 ))/" "$C/rocola_estado.txt"
[ "${SIN_TECLAS:-0}" = 1 ] && rm -f "$C/rocola_teclas.txt"; [ -n "${CONFIG:-}" ] && printf "%s\r\n" ${CONFIG//;/ } > "$C/rocola_config.txt"
FIFO=$W/ritmo.fifo; [ -p "$FIFO" ] || mkfifo "$FIFO"
python3 "$(dirname "$0")/ritmo.py" "$FIFO" & RP=$!
(cd "$C" && env ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_ALSA="file:FILE=$FIFO,FORMAT=wav" "$@" timeout -s TERM "${TIEMPO:-120}" dotnet "$C/rocola-xp.dll" > "$SAL/salida.txt" 2>&1)
echo "salio con $?" >> "$SAL/salida.txt"
kill $RP 2>/dev/null; wait $RP 2>/dev/null
cp -p "$C"/rocola_error.txt "$SAL/" 2>/dev/null
exit 0
