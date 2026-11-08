#!/bin/bash
# graba.sh NOMBRE FIN "PULSA" "TRAMOS" [PROGRAMA] -- graba la cinta $TARJETA/cintas/NOMBRE.cinta corriendo la rocola FIN segundos con
# esas teclas (ROCOLA_PULSA) y apuntando los cuadros de esos tramos (ROCOLA_CINTA_CUADROS, segundos desde que arranca). CONFIG,
# ROCOLA_PANTALLA y ROCOLA_TRAZA_DIBUJOS del entorno valen. PROGRAMA: la carpeta del programa (de fabrica, el de este arbol en JIT).
set -u
. "$(dirname "$(readlink -f "$0")")/comun.sh"
nom=$1; fin=$2; pulsa=$3; tramos=$4; prog=${5:-$arbol/Xp/bin/Release/net10.0}
mkdir -p "$TARJETA/cintas" "$TARJETA/registros"
ROCOLA_CINTA=$TARJETA/cintas/$nom.cinta ROCOLA_CINTA_CUADROS="$tramos" ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA="$fin:$TARJETA/registros/$nom-fin.png" ROCOLA_CAPTURA_SALE=1 \
  "$guiones/corre.sh" "$prog" "$TARJETA/registros/$nom" $((${fin%.*} + 60))
grep -v "^dibujo" "$TARJETA/registros/$nom/registro.txt" | tail -3
ls -la "$TARJETA/cintas/$nom.cinta"
