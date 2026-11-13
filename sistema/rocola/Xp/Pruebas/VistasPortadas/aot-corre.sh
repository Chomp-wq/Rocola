#!/bin/bash
# aot-corre.sh: el binario NativeAOT de esta copia (publicado en $PORTADAS/aot: dotnet publish -c Release -r linux-x64 -o $PORTADAS/aot) por las
# pantallas del grupo «portadas», con las teclas, las capturas y las opciones de los recorridos (Pruebas/Recorridos/recorridos.sh): «pared» y
# «pared2» (las portadas de la pared, que se leen, se reducen, se guardan y se suben con sus previas; el cielo con sus teselas), «buscador» (las
# portadas de las tarjetas, la interfaz con repuesto y la ventana que se anima al cerrar) y «video». Con los datos de los recorridos, el estado guardado con la hora de ahora (como corre.sh de los recorridos),
# fuera de pantalla, el sonido a tiempo real (ritmo.py) y Pruebas/Tarjeta/guiones/corre.sh. Deja lo que escribio, rocola_error.txt y las
# capturas en $BUSCADOR/aot-NOMBRE, y cuenta las excepciones.
set -u
S=${PORTADAS:-/root/rocola-trabajo/port/portadas}
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
R=$X/Pruebas/Recorridos/recorridos.sh
for n in pared video buscador; do
  P[$n]=$(sed -n "s/^P\\[$n\\]=\"\\(.*\\)\"\$/\\1/p" $R); C[$n]=$(sed -n "s/^C\\[$n\\]=\"\\([^\"]*\\)\".*\$/\\1/p" $R); T[$n]=$(sed -n "s/^.*T\\[$n\\]=\\([0-9]*\\).*\$/\\1/p" $R)
done
P[pared2]=${P[pared]}; C[pared2]=$(sed -n 's/^.*C\[pared2\]="\([^"]*\)".*$/\1/p' $R); T[pared2]=${T[pared]}
declare -A K; K[pared2]=$(sed -n 's/^.*K\[pared2\]="\([^"]*\)".*$/\1/p' $R)
for n in ${*:-pared pared2 buscador video}; do
  export CONFIG="${K[$n]:-}"; CONFIG=${CONFIG//;/ }
  O=$S/aot-$n; rm -rf $O; mkdir -p $O
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
