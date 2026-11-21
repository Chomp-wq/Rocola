#!/bin/bash
# e2e4.sh PROG SALIDA PULSA TIEMPO : como e2e3.sh, pero CON MUSICA a tiempo real (el complemento «file» de ALSA a una tuberia que lee
# ritmo.py al ritmo de su cabecera, como Pruebas/Recorridos/corre.sh) y 20 creditos en rocola_estado.txt (la escena pide una cancion larga
# al empezar; con MUSICA=0, la salida nula de ALSA, sin musica a tiempo real, como e2e3.sh); OS_TXT: el contenido de rocola_os.txt (vacio: sin archivo). La medida del colchon del sonido sale en mide.tsv (Mide.cs).
set -u
PROG=$1; SAL=$2; PULSA=$3; T=$4
W=/root/rocola-trabajo/fase11/rapido-caratulas; D=$W/d
mkdir -p "$SAL" "$D/run"; rm -f "$SAL/mide.tsv" "$D/rocola_error.txt" "$D/rocola_os_bitacora.txt" "$D/rocola_estado.txt" "$D/rocola_os.txt"
[ -n "${OS_TXT:-}" ] && printf "%b" "$OS_TXT" > "$D/rocola_os.txt"
printf 'ROCOLA-ESTADO v1\r\nts=%s\r\ncreditos=20\r\n' "$(( ($(date +%s) + 62135596800) * 10000000 ))" > "$D/rocola_estado.txt"
case "${MINIATURAS:-frias}" in
  nada) [ -d "$D/rocola_miniaturas" ] && mv "$D/rocola_miniaturas" "$D/miniaturas.guardadas"; for f in "$D"/*.previews* "$D"/rocola_previas*; do [ -e "$f" ] && mv "$f" "$f.guardada"; done ;;
  frias) python3 -I $W/desaloja.py "$D/rocola_miniaturas" > /dev/null ;;
  calientes) cat "$D"/rocola_miniaturas/* > /dev/null 2>&1 ;;
esac
L0=$(cut -d" " -f1-3 /proc/loadavg)
[ "${ORIGINALES_FRIAS:-1}" = 1 ] && python3 -I $W/desaloja.py "$W/musica" "$PROG" > /dev/null
FIFO=$W/ritmo.fifo; [ -p "$FIFO" ] || mkfifo "$FIFO"
if [ "${MUSICA:-1}" = 1 ]; then ALSA="file:FILE=$FIFO,FORMAT=wav"; python3 -I $W/ritmo.py "$FIFO" & RP=$!; else ALSA=null; RP=""; fi
(
  cd "$D" && env ROCOLA_DATOS="$D" ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_ALSA="$ALSA" ROCOLA_RUN="$D/run" ROCOLA_OS=medir-caratulas${EXTRA_OS:-} \
   LP_NUM_THREADS=2 ROCOLA_MIDE_CARATULAS="$SAL/mide.tsv" ROCOLA_PULSA="$PULSA" taskset -c 0,1 timeout -s TERM "$T" "$PROG/rocola-xp" > "$SAL/salida.txt" 2>&1 &
  PID=""; for i in $(seq 50); do PID=$(pgrep -n -f "^$PROG/rocola-xp" || true); [ -n "$PID" ] && break; sleep 0.1; done
  while [ -n "$PID" ] && kill -0 "$PID" 2>/dev/null; do
    for t in /proc/$PID/task/*; do c=$(cat $t/comm 2>/dev/null); case "$c" in llvmpipe*) taskset -p -c 2,3 ${t##*/} >/dev/null 2>&1 ;; *) taskset -p -c 0,1 ${t##*/} >/dev/null 2>&1 ;; esac; done
    sleep 0.3
  done
  wait
)
[ -n "$RP" ] && { kill $RP 2>/dev/null; wait $RP 2>/dev/null; }
echo "carga al empezar $L0" >> "$SAL/salida.txt"
echo "carga al acabar $(cut -d' ' -f1-3 /proc/loadavg)" >> "$SAL/salida.txt"
cp -p "$D"/rocola_error.txt "$D"/rocola_os_bitacora.txt "$SAL/" 2>/dev/null
[ "${MINIATURAS:-frias}" = nada ] && echo "miniaturas hechas $(ls "$D/rocola_miniaturas" 2>/dev/null | wc -l)" >> "$SAL/salida.txt"
if [ "${MINIATURAS:-frias}" = nada ]; then
  rm -rf "$D/rocola_miniaturas"; [ -d "$D/miniaturas.guardadas" ] && mv "$D/miniaturas.guardadas" "$D/rocola_miniaturas"
  for f in "$D"/*.guardada; do [ -e "$f" ] && { rm -f "${f%.guardada}"; mv "$f" "${f%.guardada}"; }; done
fi
rm -f "$D/rocola_os.txt"
exit 0
