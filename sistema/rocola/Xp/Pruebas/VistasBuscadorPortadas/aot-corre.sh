#!/bin/bash
# aot-corre.sh: el binario NativeAOT de esta copia (publicado en $BUSCADOR/aot: dotnet publish -c Release -r linux-x64 -o $BUSCADOR/aot) por las
# pantallas del grupo «buscador-portadas», con las teclas y las capturas de los recorridos (Pruebas/Recorridos/recorridos.sh): «buscador» (abrirlo,
# teclear, las tarjetas que llegan con su portada, elegir, los atajos y volver a abrirlo) y «ayuda» (el asistente y la guia del rojo, que pasa por
# la imagen grande del buscador). Con los datos de los recorridos, el estado guardado con la hora de ahora (como corre.sh de los recorridos),
# fuera de pantalla, el sonido a tiempo real (ritmo.py) y Pruebas/Tarjeta/guiones/corre.sh. Deja lo que escribio, rocola_error.txt y las
# capturas en $BUSCADOR/aot-NOMBRE, y cuenta las excepciones.
set -u
S=${BUSCADOR:-/root/rocola-trabajo/port/buscador-portadas}
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
P[buscador]=$(sed -n 's/^P\[buscador\]="\(.*\)"$/\1/p' $X/Pruebas/Recorridos/recorridos.sh); C[buscador]=$(sed -n 's/^C\[buscador\]="\([^"]*\)".*$/\1/p' $X/Pruebas/Recorridos/recorridos.sh); T[buscador]=40
P[ayuda]=$(sed -n 's/^P\[ayuda\]="\(.*\)"$/\1/p' $X/Pruebas/Recorridos/recorridos.sh); C[ayuda]=$(sed -n 's/^C\[ayuda\]="\([^"]*\)".*$/\1/p' $X/Pruebas/Recorridos/recorridos.sh); T[ayuda]=42
for n in ${*:-buscador ayuda}; do
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
