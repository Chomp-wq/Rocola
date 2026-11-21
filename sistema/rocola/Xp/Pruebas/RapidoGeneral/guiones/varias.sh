#!/bin/bash
# varias.sh NOMBRE VUELTAS PROG...: las escenas PARED BUSCADOR AYUDA CTRLF3 con los datos calientes (dcal), alternadas
W=/root/rocola-trabajo/fase11/rapido-general; N=$1; V=$2; shift 2
for e in PARED BUSCADOR AYUDA CTRLF3; do DBASE=$W/dcal $W/guiones/serie.sh $N-$e $V $e 40 "3:reposo,7:escena,37:fin" "$@"; done
