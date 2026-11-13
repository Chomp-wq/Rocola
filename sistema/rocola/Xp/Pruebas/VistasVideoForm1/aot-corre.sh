#!/bin/bash
# aot-corre.sh [NOMBRES]: el binario NativeAOT de esta copia (publicado en $VIDEOFORM1/../aot: dotnet publish -c Release -r linux-x64 -o
# /root/rocola-trabajo/port/video-form1/aot) por las pantallas del grupo «video-form1» (la interfaz que se sube a la tarjeta esta en todas: de
# fabrica pared, buscador, atraccion y ayuda), con las teclas, las capturas y el tiempo de los recorridos (Pruebas/Recorridos/recorridos.sh). Con
# los datos de los recorridos, el estado guardado con la hora de ahora (como corre.sh de los recorridos), fuera de pantalla, el sonido a tiempo
# real (ritmo.py) y Pruebas/Tarjeta/guiones/corre.sh. Deja lo que escribio, rocola_error.txt y las capturas en $VIDEOFORM1/aot-NOMBRE, y cuenta
# las excepciones.
set -u
S=${VIDEOFORM1:-/root/rocola-trabajo/port/video-form1}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$X/Pruebas/Comun
W=${ROCOLA_TRABAJO:-/root/rocola-trabajo/port/dibujo}
D=$S/refdatos/sistema/rocola/Xp/bin/Release/net10.0
rm -rf $S/refdatos; mkdir -p $D $S/tarjeta
cp -rp $W/datos-base/. $D/
[ -f $D/rocola_estado.txt ] && sed -i "s/^ts=.*/ts=$(( ($(date +%s) + 62135596800) * 10000000 ))/" $D/rocola_estado.txt
FIFO=$S/ritmo-aot.fifo; [ -p $FIFO ] || mkfifo $FIFO
R=$X/Pruebas/Recorridos/recorridos.sh
for n in ${*:-pared buscador atraccion ayuda}; do
  P=$(sed -n "s/^P\[$n\]=\"\(.*\)\"$/\1/p" $R); C=$(sed -n "s/^C\[$n\]=\"\([^\"]*\)\".*$/\1/p" $R); T=$(sed -n "s/^.*T\[$n\]=\([0-9]*\).*$/\1/p" $R)
  O=$S/aot-$n; rm -rf $O; mkdir -p $O
  caps=""; for c in ${C//,/ }; do caps="$caps,$c:$O/c$c.png"; done; caps=${caps#,}
  python3 $X/Pruebas/Recorridos/ritmo.py $FIFO & RP=$!
  REFERENCIA=$S/refdatos TARJETA=$S/tarjeta CONSERVA=1 ROCOLA_ALSA="file:FILE=$FIFO,FORMAT=wav" ROCOLA_PULSA="$P" ROCOLA_CAPTURA="$caps" \
    ROCOLA_CAPTURA_SALE=1 $X/Pruebas/Tarjeta/guiones/corre.sh $S/aot $O $(( T + 25 )) > $O/corre.txt 2>&1
  kill $RP 2>/dev/null; wait $RP 2>/dev/null
  run=$(sed -n 's/^corrida en //p' $O/corre.txt)
  [ -n "$run" ] && { cp -p $run/rocola_error.txt $O/ 2>/dev/null; rm -rf "$run"; }
  e1=$(grep -c 'Exception' $O/registro.txt 2>/dev/null); e2=$(grep -c 'Exception' $O/rocola_error.txt 2>/dev/null)
  echo "$n: $(tail -1 $O/registro.txt); capturas $(ls $O/*.png 2>/dev/null | wc -l) de $(echo ${C//,/ } | wc -w);" \
       "excepciones en lo que escribio ${e1:-0}, en rocola_error.txt ${e2:-0}"
done
rm -rf $S/refdatos $S/tarjeta
