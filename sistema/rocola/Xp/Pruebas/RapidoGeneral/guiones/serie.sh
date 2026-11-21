#!/bin/bash
# serie.sh NOMBRE VUELTAS ESCENA T TRAMOS PROG1 PROG2 ...: las configuraciones alternadas, cada vuelta con la carpeta de datos copiada de DBASE
set -u
N=$1; V=$2; E=$3; T=$4; TR=$5; shift 5
W=/root/rocola-trabajo/fase11/rapido-general; . $W/guiones/escenas.sh
for v in $(seq $V); do for p in "$@"; do
  rm -rf $W/dt-$N; cp -a ${DBASE:-$W/d0} $W/dt-$N
  $W/guiones/corre.sh $W/$p $W/r/$N/$p-$v $W/dt-$N "${!E}" $T "$TR" > /dev/null
done; done
rm -rf $W/dt-$N
