#!/bin/bash
# bancos2.sh VUELTAS : el banco de las caratulas (frio y guardada, 300 portadas) alternando, en el nucleo 1:
#   rama-xp (nuevo4 sin ROCOLA_OS), xp (nuevo5 sin ROCOLA_OS), rama (nuevo4 con caratulas-rapidas), cero (nuevo5 con caratulas-rapidas y las
#   dos claves en 0), dct (nuevo5 con caratulas-rapidas de fabrica: portadas_dct=1). Cada uno en bancos2/ETIQUETA-N. Al final, totales y huellas.
set -u
W=/root/rocola-trabajo/fase11/rapido-caratulas; L=$W/muestra300.txt; V=${1:-2}
for v in $(seq $V); do
  for c in rama-xp:nuevo4-aot:: xp:nuevo5-aot:: rama:nuevo4-aot:ROCOLA_OS=caratulas-rapidas: cero:nuevo5-aot:ROCOLA_OS=caratulas-rapidas:0 dct:nuevo5-aot:ROCOLA_OS=caratulas-rapidas:; do
    IFS=: read -r et prog os cero <<< "$c"
    D=$W/bancos2/$et-$v; rm -rf $D; mkdir -p $D
    [ "$cero" = 0 ] && printf 'portadas_dct=0\nportadas_dos_lectores=0\n' > $D/rocola_os.txt

    BANCOS=bancos2 BANCO_ENV="$os" $W/banco.sh $W/$prog $et-$v frio $L 1
    BANCOS=bancos2 BANCO_ENV="$os BANCO_DESALOJA=1" $W/banco.sh $W/$prog $et-$v guardada $L 1
    rm -rf $D/rocola_miniaturas
  done
done
for m in frio guardada; do
  echo "== $m (TODAS 9-total: n p50 p95 max suma)"
  for d in $W/bancos2/*-[0-9]*; do printf "%-10s %s | carga %s\n" "${d##*/}" "$(grep -P '^TODAS\t9-total' $d/$m.txt | cut -f3-7 | tr '\t' ' ')" "$(head -1 $d/$m.txt | cut -d' ' -f3)"; done
  echo "-- huellas completas contra rama-1 (y sin la columna del archivo guardado contra rama-xp-1)"
  for d in $W/bancos2/*-[0-9]*; do printf "%-10s " "${d##*/}"; cmp -s $W/bancos2/rama-1/$m.huellas.tsv $d/$m.huellas.tsv && printf "igual-a-rama " || printf "distinta-de-rama "; cmp -s <(cut -f1-3,5- $W/bancos2/rama-xp-1/$m.huellas.tsv) <(cut -f1-3,5- $d/$m.huellas.tsv) && echo "puntos-de-xp" || echo "OTROS-PUNTOS"; done
done
