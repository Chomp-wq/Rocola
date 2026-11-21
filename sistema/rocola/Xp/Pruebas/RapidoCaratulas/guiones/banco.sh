#!/bin/bash
# banco.sh PROG(carpeta aot) ETIQUETA MODO LISTA [CPU]: corre el banco con el candado (una vez), en una carpeta de datos propia (BANCOS/ETIQUETA; BANCOS=bancos de fabrica)
set -u
PROG=$1; E=$2; M=$3; L=$4; CPU=${5:-2}
W=/root/rocola-trabajo/fase11/rapido-caratulas; D=$W/${BANCOS:-bancos}/$E; mkdir -p $D
flock -o /root/rocola-trabajo/fase11/pesado.lock bash -c "
  echo \"carga antes: \$(cut -d' ' -f1-3 /proc/loadavg)\" > $D/$M.txt
  cd $D && env ROCOLA_DATOS=$D ROCOLA_PANTALLA=fuera:64x64 ${BANCO_ENV:-} taskset -c $CPU $PROG/rocola-xp --banco-caratulas $L $M $D/$M.huellas.tsv >> $D/$M.txt 2>&1
  echo \"carga despues: \$(cut -d' ' -f1-3 /proc/loadavg)\" >> $D/$M.txt"
