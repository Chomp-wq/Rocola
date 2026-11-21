#!/bin/bash
# corre.sh PROG SALIDA D PULSA TIEMPO TRAMOS: la rocola (PROG: carpeta con rocola-xp) fuera de pantalla a 1024x768 con la carpeta de datos D
# (la biblioteca inventada en D/musica), la medida de lo general (Mide.cs) en SALIDA/mide.tsv. El programa en los nucleos 0,1 (dos, como
# el A4) y los hilos de llvmpipe (lo que en el A4 hace la tarjeta) en el 2,3. EXTRA_OS: mas funciones de ROCOLA_OS.
set -u
PROG=$1; SAL=$2; D=$3; PULSA=$4; T=$5; TR=$6
mkdir -p "$SAL" "$D/run"; rm -f "$SAL/mide.tsv" "$D/rocola_error.txt" "$D/rocola_diagnostico.txt" "$D/rocola_estado.txt"
echo "carga al empezar $(cut -d' ' -f1-3 /proc/loadavg)" > "$SAL/salida.txt"
(
  cd "$D"; env ROCOLA_DATOS="$D" ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_ALSA=${ALSA:-null} ROCOLA_RUN="$D/run" ROCOLA_OS=medir-general${EXTRA_OS:-} \
   LP_NUM_THREADS=2 ROCOLA_MIDE_GENERAL="$SAL/mide.tsv" ROCOLA_MIDE_TRAMOS="$TR" ROCOLA_PULSA="$PULSA" taskset -c 0,1 timeout -s TERM "$T" "$PROG/rocola-xp" >> "$SAL/salida.txt" 2>&1 &
  TP=$!; PID=""; for i in $(seq 50); do PID=$(pgrep -P $TP || true); [ -n "$PID" ] && break; sleep 0.1; done
  while [ -n "$PID" ] && kill -0 "$PID" 2>/dev/null; do
    for t in /proc/$PID/task/*; do c=$(cat $t/comm 2>/dev/null); case "$c" in llvmpipe*) taskset -p -c 2,3 ${t##*/} >/dev/null 2>&1 ;; *) taskset -p -c 0,1 ${t##*/} >/dev/null 2>&1 ;; esac; done
    sleep 0.3
  done
  wait
)
echo "carga al acabar $(cut -d' ' -f1-3 /proc/loadavg)" >> "$SAL/salida.txt"
cp -p "$D"/rocola_error.txt "$D"/rocola_diagnostico.txt "$D"/rocola_os_bitacora.txt "$SAL/" 2>/dev/null
exit 0
