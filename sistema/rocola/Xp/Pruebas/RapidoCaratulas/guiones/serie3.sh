#!/bin/bash
# serie.sh NOMBRE ESCENA TIEMPO VUELTAS CONFIG... : corre las configuraciones alternando (vuelta 1: todas, vuelta 2: todas...), cada una con el
# candado pesado, en r/NOMBRE/CONFIG-N. CONFIG = etiqueta:carpeta_aot:extra_os:miniaturas (extra_os vacio = solo XP; p. ej.
# base:base-aot::frias  nuevoR:nuevo2-aot:,caratulas-rapidas:frias). Al final, analiza2 por configuracion.
set -u
N=$1; ESC=$2; T=$3; V=$4; shift 4
W=/root/rocola-trabajo/fase11/rapido-caratulas
for v in $(seq $V); do
  for c in "$@"; do
    IFS=: read -r et prog eos mini <<< "$c"
    flock -o /root/rocola-trabajo/fase11/pesado.lock env MINIATURAS=$mini EXTRA_OS="$eos" $W/e2e3.sh $W/$prog $W/r/$N/$et-$v "$(cat $W/$ESC)" $T
    echo "$et-$v $(tail -3 $W/r/$N/$et-$v/salida.txt | tr "\n" " ")"
  done
done
for c in "$@"; do IFS=: read -r et prog eos mini <<< "$c"; echo "== $et"; python3 -I $W/analiza2.py $W/r/$N/$et-*/mide.tsv | tail -1; done
