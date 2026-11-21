#!/bin/bash
# serie4.sh NOMBRE ESCENA TIEMPO VUELTAS CONFIG... : como serie3.sh con e2e4.sh. CONFIG = etiqueta:carpeta_aot:miniaturas:musica:os_txt
# (os_txt con \n entre renglones; vacio: sin rocola_os.txt). Siempre con ROCOLA_OS=medir-caratulas,caratulas-rapidas.
set -u
N=$1; ESC=$2; T=$3; V=$4; shift 4
W=/root/rocola-trabajo/fase11/rapido-caratulas
for v in $(seq $V); do
  for c in "$@"; do
    IFS=: read -r et prog mini mus ostxt <<< "$c"
    flock -o /root/rocola-trabajo/fase11/pesado.lock env MINIATURAS=$mini MUSICA=$mus OS_TXT="$ostxt" EXTRA_OS=,caratulas-rapidas $W/e2e4.sh $W/$prog $W/r/$N/$et-$v "$(cat $W/$ESC)" $T
    echo "$et-$v $(tail -3 $W/r/$N/$et-$v/salida.txt | tr "\n" " ")"
  done
done
for c in "$@"; do IFS=: read -r et prog mini mus ostxt <<< "$c"; echo "== $et"; python3 -I $W/analiza2.py $W/r/$N/$et-*/mide.tsv | tail -1; done
