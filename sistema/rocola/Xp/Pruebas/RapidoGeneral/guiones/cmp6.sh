#!/bin/bash
# cmp6.sh: el control negativo (ROCOLA_MIDE_ROMPE=1: nunca vuelve a subir una capa) en dos escenas, y la prueba de igualdad entera con las cuentas de las capas que vuelven
W=/root/rocola-trabajo/fase11/rapido-general; . $W/guiones/escenas.sh
for e in PARED CTRLF3; do
  rm -rf $W/dt-cmp6; cp -a $W/d0 $W/dt-cmp6
  ROCOLA_MIDE_ROMPE=1 ROCOLA_MIDE_COMPARA=4 $W/guiones/corre.sh $W/n6-aot $W/r/rompe-$e $W/dt-cmp6 "${!e}" 40 "3:todo,37:fin" > /dev/null
done
rm -rf $W/dt-cmp6
PROG=n6-aot ET=cmp6 $W/guiones/cmp5.sh
