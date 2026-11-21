#!/bin/bash
# e2e2.sh PROG SALIDA PULSA TIEMPO : la rocola (PROG: carpeta con rocola-xp NativeAOT) fuera de pantalla con la biblioteca inventada, en la
# carpeta de datos FIJA d/ (las miniaturas valen de una corrida a otra), la medida de la pared (Mide.cs) en SALIDA/mide.tsv.
# El programa va en los nucleos 0,1 (dos, como el A4) y los hilos de llvmpipe (lo que en el A4 hace la tarjeta) en el 2,3.
# MINIATURAS=frias (de fabrica: las guardadas, fuera de la cache del sistema) | calientes | nada (se apartan y se borran las nuevas)
# EXTRA_OS: mas funciones de ROCOLA_OS (p. ej. ",caratulas-rapidas")
set -u
PROG=$1; SAL=$2; PULSA=$3; T=$4
W=/root/rocola-trabajo/fase11/rapido-caratulas; D=$W/d
mkdir -p "$SAL" "$D/run"; rm -f "$SAL/mide.tsv" "$D/rocola_error.txt" "$D/rocola_os_bitacora.txt" "$D/rocola_estado.txt"
case "${MINIATURAS:-frias}" in
  nada) [ -d "$D/rocola_miniaturas" ] && mv "$D/rocola_miniaturas" "$D/miniaturas.guardadas"; for f in "$D"/*.previews* "$D"/rocola_previas*; do [ -e "$f" ] && mv "$f" "$f.guardada"; done ;;
  frias) python3 -I $W/desaloja.py "$D/rocola_miniaturas" > /dev/null ;;
  calientes) cat "$D"/rocola_miniaturas/* > /dev/null 2>&1 ;;
esac
L0=$(cut -d" " -f1-3 /proc/loadavg)
[ "${ORIGINALES_FRIAS:-1}" = 1 ] && python3 -I $W/desaloja.py "$W/musica" "$PROG" > /dev/null
(
  cd "$D" && env ROCOLA_DATOS="$D" ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_ALSA=null ROCOLA_RUN="$D/run" ROCOLA_OS=medir-caratulas${EXTRA_OS:-} \
   LP_NUM_THREADS=2 ROCOLA_MIDE_CARATULAS="$SAL/mide.tsv" ROCOLA_PULSA="$PULSA" taskset -c 0,1 timeout -s TERM "$T" "$PROG/rocola-xp" > "$SAL/salida.txt" 2>&1 &
  PID=""; for i in $(seq 50); do PID=$(pgrep -n -f "^$PROG/rocola-xp" || true); [ -n "$PID" ] && break; sleep 0.1; done
  while [ -n "$PID" ] && kill -0 "$PID" 2>/dev/null; do
    for t in /proc/$PID/task/*; do c=$(cat $t/comm 2>/dev/null); case "$c" in llvmpipe*) taskset -p -c 2,3 ${t##*/} >/dev/null 2>&1 ;; *) taskset -p -c 0,1 ${t##*/} >/dev/null 2>&1 ;; esac; done
    sleep 0.3
  done
  wait
)
echo "carga al empezar $L0" >> "$SAL/salida.txt"
echo "carga al acabar $(cut -d' ' -f1-3 /proc/loadavg)" >> "$SAL/salida.txt"
cp -p "$D"/rocola_error.txt "$D"/rocola_os_bitacora.txt "$SAL/" 2>/dev/null
[ "${MINIATURAS:-frias}" = nada ] && echo "miniaturas hechas $(ls "$D/rocola_miniaturas" 2>/dev/null | wc -l)" >> "$SAL/salida.txt"
if [ "${MINIATURAS:-frias}" = nada ]; then
  rm -rf "$D/rocola_miniaturas"; [ -d "$D/miniaturas.guardadas" ] && mv "$D/miniaturas.guardadas" "$D/rocola_miniaturas"
  for f in "$D"/*.guardada; do [ -e "$f" ] && { rm -f "${f%.guardada}"; mv "$f" "${f%.guardada}"; }; done
fi
exit 0
