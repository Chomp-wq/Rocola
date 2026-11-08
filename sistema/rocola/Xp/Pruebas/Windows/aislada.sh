#!/bin/bash
# aislada.sh PROG VUELTAS SEGUNDOS : la prueba aislada del video (--prueba-video) VUELTAS veces; por vuelta, las lecturas del centro amarillas
# y las demas (con el video empezado). BIEN = 3 o mas amarillas y ninguna otra.
. "$(dirname "$0")/comun.sh"
cd "$W/$1" || exit 1
V="$DATOSXP/musica/Zeta Videos/Clips/Video de prueba.mp4"
N=${2:-3}; S=${3:-3}; bien=0; mal=0
for i in $(seq 1 $N); do
  out=$(ROCOLA_ALSA=null timeout 90 dotnet rocola-xp.dll --prueba-video "$V" $S 2>&1)
  am=$(echo "$out" | awk '/ centro /{split($6,c,","); if ($8+0 > 0.3) { if (c[1]>240 && c[2]>240 && c[3]<30) a++ } } END{print a+0}')
  ot=$(echo "$out" | awk '/ centro /{split($6,c,","); if ($8+0 > 0.3) { if (!(c[1]>240 && c[2]>240 && c[3]<30)) o++ } } END{print o+0}')
  if [ "$am" -ge 3 ] && [ "$ot" -eq 0 ]; then bien=$((bien+1)); else mal=$((mal+1)); echo "vuelta $i MAL: amarillas=$am otras=$ot $(echo "$out" | grep Resumen)"; fi
done
echo "RESUMEN $1: bien=$bien mal=$mal de $N"
