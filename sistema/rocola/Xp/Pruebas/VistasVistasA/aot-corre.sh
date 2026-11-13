#!/bin/bash
# aot-corre.sh: el binario NativeAOT de esta copia (publicado en $VISTASA/aot: dotnet publish -c Release -r linux-x64 -o $VISTASA/aot) por las
# pantallas del grupo «vistas-a», con las teclas y las capturas de los recorridos (Pruebas/Recorridos/recorridos.sh): «pared» (la barra de
# letras: cambiar de letra con la capsula que viaja, la estrella de lo mas sonado, entrar en un disco y volver), «pared2» (lo mismo con otras
# opciones) y «botones» (el asistente de botones: la barra de tres letras). Con los datos de los recorridos, el estado guardado con la hora de
# ahora (como corre.sh de los recorridos), fuera de pantalla, el sonido a tiempo real (ritmo.py) y Pruebas/Tarjeta/guiones/corre.sh. Deja lo
# que escribio, rocola_error.txt y las capturas en $VISTASA/aot-NOMBRE, y cuenta las excepciones.
set -u
S=${VISTASA:-/root/rocola-trabajo/port/vistas-a}
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
P[pared2]="${P[pared]}"; C[pared2]=$(sed -n 's/^P\[pared2\]=.*C\[pared2\]="\([^"]*\)".*$/\1/p' $X/Pruebas/Recorridos/recorridos.sh); T[pared2]=75
P[botones]=$(sed -n 's/^P\[botones\]="\(.*\)"$/\1/p' $X/Pruebas/Recorridos/recorridos.sh); C[botones]=$(sed -n 's/^C\[botones\]="\([^"]*\)".*$/\1/p' $X/Pruebas/Recorridos/recorridos.sh); T[botones]=22
declare -A K; K[pared2]=$(sed -n 's/^declare -A K; K\[pared2\]="\(.*\)"$/\1/p' $X/Pruebas/Recorridos/recorridos.sh)
for n in ${*:-pared pared2 botones}; do
  O=$S/aot-$n; rm -rf $O; mkdir -p $O
  # los datos, nuevos para cada uno: «botones» va sin rocola_teclas.txt (asi sale el asistente de botones) y «pared2» con sus opciones
  rm -rf $S/refdatos; mkdir -p $D; cp -rp $W/datos-base/. $D/
  [ -f $D/rocola_estado.txt ] && sed -i "s/^ts=.*/ts=$(( ($(date +%s) + 62135596800) * 10000000 ))/" $D/rocola_estado.txt
  [ $n = botones ] && rm -f $D/rocola_teclas.txt
  CFG="${K[$n]:-}"; export CONFIG="${CFG//;/ }"
  caps=""; for c in ${C[$n]//,/ }; do caps="$caps,$c:$O/c$c.png"; done; caps=${caps#,}
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
