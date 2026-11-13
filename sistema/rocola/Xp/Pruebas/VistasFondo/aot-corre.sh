#!/bin/bash
# aot-corre.sh: el binario NativeAOT de esta copia (publicado en $FONDO/aot: dotnet publish -c Release -r linux-x64 -o $FONDO/aot) por las
# pantallas del grupo «fondo», con las teclas y las capturas de los recorridos (Pruebas/Recorridos/recorridos.sh): «botones» y «video» (los que
# pintan el cielo del dibujo por procesador: las franjas y el panel del disco, con la luna, las nebulosas y el grano), «atraccion» (el cielo de
# la atraccion, Starfield.Draw) y «pared» (los contornos redondeados de la tarjeta, ContornoRedondo, y el color de las portadas). Con
# los datos de los recorridos, el estado guardado con la hora de ahora (como corre.sh de los recorridos), fuera de pantalla, el sonido a tiempo
# real (ritmo.py) y Pruebas/Tarjeta/guiones/corre.sh. Deja lo que escribio, rocola_error.txt y las capturas en $ASISTENTE/aot-NOMBRE, y cuenta las
# excepciones.
set -u
S=${FONDO:-/root/rocola-trabajo/port/fondo}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$X/Pruebas/Comun
W=${ROCOLA_TRABAJO:-/root/rocola-trabajo/port/dibujo}
D=$S/refdatos/sistema/rocola/Xp/bin/Release/net10.0
rm -rf $S/refdatos; mkdir -p $D $S/tarjeta
cp -rp $W/datos-base/. $D/
[ -f $D/rocola_estado.txt ] && sed -i "s/^ts=.*/ts=$(( ($(date +%s) + 62135596800) * 10000000 ))/" $D/rocola_estado.txt
FIFO=$S/ritmo-aot.fifo; [ -p $FIFO ] || mkfifo $FIFO
declare -A P C T
P[pared]=$(sed -n 's/^P\[pared\]="\(.*\)"$/\1/p' $X/Pruebas/Recorridos/recorridos.sh); C[pared]=$(sed -n 's/^C\[pared\]="\([^"]*\)".*$/\1/p' $X/Pruebas/Recorridos/recorridos.sh); T[pared]=75
for r in botones:22 video:32 atraccion:76; do k=${r%%:*}; T[$k]=${r##*:}
  P[$k]=$(sed -n "s/^P\\[$k\\]=\"\\(.*\\)\"\$/\\1/p" $X/Pruebas/Recorridos/recorridos.sh); C[$k]=$(sed -n "s/^C\\[$k\\]=\"\\([^\"]*\\)\".*\$/\\1/p" $X/Pruebas/Recorridos/recorridos.sh)
done
for n in ${*:-botones video atraccion pared}; do
  O=$S/aot-$n; rm -rf $O; mkdir -p $O
  caps=""; for c in ${C[$n]//,/ }; do caps="$caps,$c:$O/c$c.png"; done; caps=${caps#,}
  # «botones» va sin rocola_teclas.txt (el asistente de botones), como en los recorridos (SIN_TECLAS)
  rm -rf $D; mkdir -p $D; cp -rp $W/datos-base/. $D/; [ -f $D/rocola_estado.txt ] && sed -i "s/^ts=.*/ts=$(( ($(date +%s) + 62135596800) * 10000000 ))/" $D/rocola_estado.txt
  [ $n = botones ] && rm -f $D/rocola_teclas.txt
  python3 $X/Pruebas/Recorridos/ritmo.py $FIFO & RP=$!
  REFERENCIA=$S/refdatos TARJETA=$S/tarjeta CONSERVA=1 ROCOLA_ALSA="file:FILE=$FIFO,FORMAT=wav" ROCOLA_PULSA="${P[$n]}" ROCOLA_CAPTURA="$caps" \
    ROCOLA_CAPTURA_SALE=1 $X/Pruebas/Tarjeta/guiones/corre.sh $S/aot $O $(( ${T[$n]} + 25 )) > $O/corre.txt 2>&1
  kill $RP 2>/dev/null; wait $RP 2>/dev/null
  run=$(sed -n 's/^corrida en //p' $O/corre.txt)
  [ -n "$run" ] && { cp -p $run/rocola_error.txt $O/ 2>/dev/null; rm -rf "$run"; }
  e1=$(grep -c 'Exception' $O/registro.txt 2>/dev/null); e2=$(grep -c 'Exception' $O/rocola_error.txt 2>/dev/null)
  echo "$n: $(tail -1 $O/registro.txt); capturas $(ls $O/*.png 2>/dev/null | wc -l) de $(echo ${C[$n]//,/ } | wc -w);" \
       "excepciones en lo que escribio ${e1:-0}, en rocola_error.txt ${e2:-0}"
done
