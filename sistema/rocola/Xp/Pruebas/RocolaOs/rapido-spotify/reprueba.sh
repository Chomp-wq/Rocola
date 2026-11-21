#!/bin/bash
# reprueba.sh: las escenas que fallaron con la maquina cargada, alternando base y fin3 (2 vueltas)
T=/root/rocola-trabajo/fase11/rapido-spotify
cd /home/user/Rocola/.claude/worktrees/rapido-spotify/sistema/rocola/Xp
for v in 1 2; do for P in base-aot fin3-aot; do
  for par in "spotify-xp:apagado fabrica celular apagon vigilante noche" "catalogo:cero mas sinllave" "sustitucion:titulo"; do
    b=${par%%:*}; esc=${par#*:}
    W=$T/bat/w-r-$b API=$T/api-falsa FALSO=$T/falso T2=$T PULSO=$T/pulso/libpulse.so.0 \
      bash Pruebas/RocolaOs/$b/prueba.sh aot:$T/$P $esc > $T/bat/r-$b-$P-$v.txt 2>&1
    echo "$v $P $b: $(tail -n 1 $T/bat/r-$b-$P-$v.txt) | $(grep '^MAL' $T/bat/r-$b-$P-$v.txt | cut -c1-90 | tr '\n' ';') (carga $(cut -d' ' -f1 /proc/loadavg))" >> $T/bat/reprueba.txt
    rm -rf $T/bat/w-r-$b
  done
done; done
echo FIN >> $T/bat/reprueba.txt
