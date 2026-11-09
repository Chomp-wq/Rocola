#!/bin/bash
# aot-corre.sh AOT SALIDA [atraccion] [top]: el binario NativeAOT de esta copia (dotnet publish -c Release -r linux-x64 -o AOT) por las
# pantallas del grupo «atraccion»: «atraccion» (40 s sin tocar y sin musica: el escaparate con sus fichas, sus pastillas y sus mensajes;
# las teclas y las capturas del recorrido) y «top» (la lista de lo mas pedido esta noche, como top.sh, con un rocola_noche.txt de la noche
# de hoy). En una carpeta fija propia (la rocola ensena rutas, y rocola_noche.txt las lleva), con los datos de los recorridos, el estado
# guardado con la hora de ahora, fuera de pantalla y el sonido a tiempo real (ritmo.py). Deja lo que escribio, rocola_error.txt y las
# capturas en SALIDA/NOMBRE y cuenta las excepciones.
set -u
AOT=$(readlink -f "$1"); SAL=$2; shift 2
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
W=${AOT_TRABAJO:-/root/rocola-trabajo/port/atraccion/aot-trabajo}
C=$W/corrida
FIFO=$W/ritmo.fifo; mkdir -p $W; [ -p $FIFO ] || mkfifo $FIFO
for n in ${*:-atraccion top}; do
  O=$SAL/$n; rm -rf "$O" "$C"; mkdir -p "$O" "$C"
  for f in "$AOT"/*; do case "$f" in *.dbg) ;; *) cp -p "$f" "$C/" ;; esac; done
  cp -rp /root/rocola-trabajo/port/dibujo/datos-base/. "$C/"
  [ -f "$C/rocola_estado.txt" ] && sed -i "s/^ts=.*/ts=$(( ($(date +%s) + 62135596800) * 10000000 ))/" "$C/rocola_estado.txt"
  if [ "$n" = top ]; then
    N=$(python3 -c "import datetime; print((datetime.datetime.now() - datetime.timedelta(hours=8)).strftime('%Y-%m-%d'))")
    printf "noche=%s\r\n5|%s\r\n3|%s\r\n2|%s\r\n1|%s\r\n1|%s\r\n" "$N" "$C/musica/Los Ejemplos/Disco Uno/01 Cancion 1.mp3" \
      "$C/musica/Otra Banda/Primer Disco/02 Tema 2.mp3" "$C/musica/Larga Duracion/Disco Largo/02 Ángel de amor (versión ñandú) & más.mp3" \
      "$C/musica/Los Ejemplos/Disco Uno/03 Cancion 3.mp3" "$C/musica/Otra Banda/Primer Disco/01 Tema 1.mp3" > "$C/rocola_noche.txt"
    P="93:39"; caps="45 66 71 72 74 78 84 89"; T=115
  else
    P="70:39"; caps="45 52 59 66 73"; T=101
  fi
  arg=""; for c in $caps; do arg="$arg,$c:$O/c$c.png"; done; arg=${arg#,}
  python3 "$X/Pruebas/Recorridos/ritmo.py" "$FIFO" & RP=$!
  (cd "$C" && env ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_ALSA="file:FILE=$FIFO,FORMAT=wav" ROCOLA_PULSA="$P" ROCOLA_CAPTURA="$arg" \
     ROCOLA_CAPTURA_SALE=1 timeout -s TERM "$T" "$C/rocola-xp" > "$O/salida.txt" 2>&1)
  echo "salio con $?" >> "$O/salida.txt"
  kill $RP 2>/dev/null; wait $RP 2>/dev/null
  cp -p "$C/rocola_error.txt" "$O/" 2>/dev/null
  e1=$(grep -c 'Exception' "$O/salida.txt"); e2=$(grep -c 'Exception' "$O/rocola_error.txt" 2>/dev/null)
  echo "$n: $(tail -1 "$O/salida.txt"); capturas $(ls "$O"/*.png 2>/dev/null | wc -l) de $(echo $caps | wc -w); excepciones en lo que escribio ${e1:-0}, en rocola_error.txt ${e2:-0}"
done
rm -rf "$C"
