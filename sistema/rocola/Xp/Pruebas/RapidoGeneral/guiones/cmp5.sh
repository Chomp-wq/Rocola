#!/bin/bash
# cmp5.sh: la prueba de igualdad del compositor (la de antes aparte: ComponeReferencia), cada escena con d0 y con dcal
W=/root/rocola-trabajo/fase11/rapido-general; . $W/guiones/escenas.sh
for e in PARED BUSCADOR CTRLF3 AYUDA BOTONES; do for d in d0 dcal; do
  rm -rf $W/dt-cmp5; cp -a $W/$d $W/dt-cmp5
  ROCOLA_MIDE_COMPARA=4 $W/guiones/corre.sh $W/${PROG:-n5-aot} $W/r/${ET:-cmp5}-$e-$d $W/dt-cmp5 "${!e}" 40 "3:todo,37:fin" > /dev/null
done; done
rm -rf $W/dt-cmp5
