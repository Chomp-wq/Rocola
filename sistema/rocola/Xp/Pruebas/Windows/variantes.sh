#!/bin/bash
# variantes.sh PREFIJO VUELTAS : la escena de sonido (tres canciones del primer disco: MONEDA mantenida, PONER, PONER, ABAJO+PONER dos veces)
# VUELTAS veces con la referencia y con esta rama alternadas (W/prog-ref-semilla y W/prog-mio-semilla, de prueba-fuente.sh), con los datos
# de DATOS si se dan (W/datos-estables: todo medido), y la huella de lo que sono en cada una.
. "$(dirname "$0")/comun.sh"
P="7:67~1500,10:13,11.5:13,12.5:40,13:13,14:40,14.5:13"
for i in $(seq 1 $2); do
  for v in ref mio; do "$AQUI/sonido.sh" prog-$v-semilla $1-$v-$i 34 "$P"; done
done
for f in "$W"/runs/$1-*/sonado.raw; do echo "$(md5sum < "$f" | cut -c1-12) $(stat -c %s "$f") $f"; done | sort
