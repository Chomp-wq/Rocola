#!/bin/bash
# bancos.sh VUELTAS : el banco de las caratulas (frio y guardada, 300 portadas) de base, xp (nuevo2 sin Rocola OS) y ros (nuevo2 con
# caratulas-rapidas), alternando, en el nucleo 1; cada uno en bancos/ETIQUETA-N. Al final las huellas comparadas y los totales.
set -u
W=/root/rocola-trabajo/fase11/rapido-caratulas; L=$W/muestra300.txt; V=${1:-2}
for v in $(seq $V); do
  for c in base:base-aot: xp:nuevo2-aot: ros:nuevo2-aot:ROCOLA_OS=caratulas-rapidas; do
    IFS=: read -r et prog os <<< "$c"
    rm -rf $W/bancos/$et-$v
    BANCO_ENV="$os" $W/banco.sh $W/$prog $et-$v frio $L 1
    BANCO_ENV="$os BANCO_DESALOJA=1" $W/banco.sh $W/$prog $et-$v guardada $L 1
    rm -rf $W/bancos/$et-$v/rocola_miniaturas
  done
done
for m in frio guardada; do
  echo "== $m (TODAS 9-total: n p50 p95 max suma)"
  for d in $W/bancos/*-[0-9]*; do printf "%-10s %s | carga %s\n" "${d##*/}" "$(grep -P '^TODAS\t9-total' $d/$m.txt | cut -f3-7 | tr '\t' ' ')" "$(head -1 $d/$m.txt | cut -d' ' -f3)"; done
  echo "-- huellas (sin la del archivo guardado, columna 4) contra base-1"
  for d in $W/bancos/*-[0-9]*; do printf "%-10s " "${d##*/}"; if cmp -s <(cut -f1-3,5- $W/bancos/base-1/$m.huellas.tsv) <(cut -f1-3,5- $d/$m.huellas.tsv); then echo iguales; else echo DISTINTAS; fi; done
done
