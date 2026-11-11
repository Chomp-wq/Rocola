#!/bin/bash
# aot-corre.sh [nombres]: el binario NativeAOT de esta copia (publicado en $PANTALLAVIVA/aot: dotnet publish -c Release -r linux-x64 -o
# $PANTALLAVIVA/aot) por las pantallas del grupo «pantalla-viva», con las teclas y las capturas de los recorridos
# (Pruebas/Recorridos/recorridos.sh): «pared» (el letrero de la portada enfocada, la ficha rapida, la lista del disco con la lista grande, el
# ecualizador y los minutos, el boleto que vuela a la cola), «pared2» (lo mismo con la estrella en 2D, la luna y la interfaz tramada),
# «video» y «buscador». Con los datos de los recorridos, el estado guardado con la hora de ahora (como corre.sh de los recorridos), fuera de
# pantalla, el sonido a tiempo real (ritmo.py) y Pruebas/Tarjeta/guiones/corre.sh. Deja lo que escribio, rocola_error.txt y las capturas en
# $PANTALLAVIVA/aot-NOMBRE, y cuenta las excepciones.
set -u
S=${PANTALLAVIVA:-/root/rocola-trabajo/port/pantalla-viva}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
W=${ROCOLA_TRABAJO:-/root/rocola-trabajo/port/dibujo}
D=$S/refdatos/sistema/rocola/Xp/bin/Release/net10.0
rm -rf $S/refdatos; mkdir -p $D $S/tarjeta
cp -rp $W/datos-base/. $D/
[ -f $D/rocola_estado.txt ] && sed -i "s/^ts=.*/ts=$(( ($(date +%s) + 62135596800) * 10000000 ))/" $D/rocola_estado.txt
FIFO=$S/ritmo-aot.fifo; [ -p $FIFO ] || mkfifo $FIFO
R=$X/Pruebas/Recorridos/recorridos.sh
declare -A P C T K
for n in pared buscador video; do
  P[$n]=$(sed -n "s/^P\[$n\]=\"\(.*\)\"$/\1/p" $R); C[$n]=$(sed -n "s/^C\[$n\]=\"\([^\"]*\)\".*$/\1/p" $R)
done
P[pared2]=${P[pared]}; C[pared2]=$(sed -n 's/^.*C\[pared2\]="\([^"]*\)".*$/\1/p' $R)
T[pared]=75; T[pared2]=75; T[buscador]=40; T[video]=32
K[pared2]=$(sed -n 's/^.*K\[pared2\]="\([^"]*\)".*$/\1/p' $R)
for n in ${*:-pared pared2 video buscador}; do
  O=$S/aot-$n; rm -rf $O; mkdir -p $O
  caps=""; for c in ${C[$n]//,/ }; do caps="$caps,$c:$O/c$c.png"; done; caps=${caps#,}
  python3 $X/Pruebas/Recorridos/ritmo.py $FIFO & RP=$!
  REFERENCIA=$S/refdatos TARJETA=$S/tarjeta CONSERVA=1 CONFIG="${K[$n]:+${K[$n]//;/ }}" ROCOLA_ALSA="file:FILE=$FIFO,FORMAT=wav" ROCOLA_PULSA="${P[$n]}" \
    ROCOLA_CAPTURA="$caps" ROCOLA_CAPTURA_SALE=1 $X/Pruebas/Tarjeta/guiones/corre.sh $S/aot $O $(( ${T[$n]} + 25 )) > $O/corre.txt 2>&1
  kill $RP 2>/dev/null; wait $RP 2>/dev/null
  run=$(sed -n 's/^corrida en //p' $O/corre.txt)
  [ -n "$run" ] && { cp -p $run/rocola_error.txt $O/ 2>/dev/null; rm -rf "$run"; }
  e1=$(grep -c 'Exception' $O/registro.txt 2>/dev/null); e2=$(grep -c 'Exception' $O/rocola_error.txt 2>/dev/null)
  echo "$n: $(tail -1 $O/registro.txt); capturas $(ls $O/*.png 2>/dev/null | wc -l) de $(echo ${C[$n]//,/ } | wc -w);" \
       "excepciones en lo que escribio ${e1:-0}, en rocola_error.txt ${e2:-0}"
done
