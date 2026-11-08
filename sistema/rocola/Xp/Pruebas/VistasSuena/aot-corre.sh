#!/bin/bash
# aot-corre.sh: el binario NativeAOT de esta copia (publicado en $SUENA/aot) por las pantallas del grupo «suena» (la pared, pedir con y sin creditos -el aviso y la
# invitacion-, dos monedas -la moneda que cae y el "+N creditos"-, el estado extendido con el titulo que se desliza y los minutos de la cola,
# otra moneda encima), con los datos de los recorridos (el disco «Larga Duracion»), fuera de pantalla, con el sonido a tiempo real (ritmo.py) y las capturas; deja lo que escribio y rocola_error.txt
set -u
S=${SUENA:-/root/rocola-trabajo/port/suena}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
O=$S/aot-corre; rm -rf $O; mkdir -p $O $S/tarjeta $S/refdatos/sistema/rocola/Xp/bin/Release
# los datos de prueba de los recorridos (con el disco de canciones de 150 s) donde corre.sh de la tarjeta busca los de la referencia
ln -sfn ${ROCOLA_TRABAJO:-/root/rocola-trabajo/port/dibujo}/datos-base $S/refdatos/sistema/rocola/Xp/bin/Release/net10.0
FIFO=$S/ritmo-aot.fifo; [ -p $FIFO ] || mkfifo $FIFO
python3 $X/Pruebas/Recorridos/ritmo.py $FIFO & RP=$!
P="7:39,8:39,19:13,20.5:40,21.5:38,22.5:13,24:40,25:13,26.5:27,30:45,33:45,36:13,37:40,38:13,39:27,66:45"
caps=""; for c in 23 25.5 31 34 38.5 60 67 70; do caps="$caps,$c:$O/c$c.png"; done; caps=${caps#,}
REFERENCIA=$S/refdatos TARJETA=$S/tarjeta CONSERVA=1 ROCOLA_ALSA="file:FILE=$FIFO,FORMAT=wav" ROCOLA_PULSA="$P" ROCOLA_CAPTURA="$caps" ROCOLA_CAPTURA_SALE=1 \
  $X/Pruebas/Tarjeta/guiones/corre.sh $S/aot $O 100 > $O/corre.txt 2>&1
kill $RP 2>/dev/null; wait $RP 2>/dev/null
run=$(sed -n 's/^corrida en //p' $O/corre.txt)
[ -n "$run" ] && { cp -p $run/rocola_error.txt $O/ 2>/dev/null; rm -rf "$run"; }
echo "aot: $(tail -1 $O/registro.txt); capturas: $(ls $O/*.png 2>/dev/null | wc -l)"
