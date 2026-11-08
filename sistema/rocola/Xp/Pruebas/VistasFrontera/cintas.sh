#!/bin/bash
# cintas.sh PROGRAMA SALIDAS -- las cintas de la tarjeta (PORT_TARJETA.md) en PROGRAMA (la carpeta con su rocola-xp.dll) con el reproductor
# de $FRONTERA/reproductor, y cada salida contra la de la referencia congelada que ya esta hecha ($TARJETA/salidas/*.ref.txt, deterministas:
# no se rehacen ni se tocan). Tambien las variantes con salida de referencia: --limite-textura 256 (00, 01, 08, 09, 10, n1, n2) y 64 (00),
# --redimensiona (05, 06, 09). Escribe IGUAL o DISTINTO por cinta y un resumen. (port nativo, tanda 2, grupo frontera)
set -u
TARJETA=${TARJETA:-/root/rocola-trabajo/port/tarjeta}
FRONTERA=${FRONTERA:-/root/rocola-trabajo/port/frontera/tarjeta}
prog=$1; sal=$2; mkdir -p "$sal"
iguales=0; distintas=0; cuadros=0
una() {   # una CINTA [opciones]
  local c=$1; shift
  local n=$(basename "$c" .cinta) suf=$(echo "$*" | tr -c 'a-zA-Z0-9\n' '_' | tr -s '_')
  local ref=$TARJETA/salidas/$n$suf.ref.txt
  [ -f "$ref" ] || { echo "SIN REFERENCIA $n $*"; return; }
  LP_NUM_THREADS=${LP_NUM_THREADS:-2} nice -n 5 dotnet "$FRONTERA/reproductor/reproductor-cintas.dll" --programa "$prog" --cinta "$c" --sale "$sal/$n$suf.txt" "$@" > "$sal/$n$suf.log" 2>&1
  local k=$(grep -c '^cuadro' "$ref")
  if diff -q "$ref" "$sal/$n$suf.txt" > /dev/null; then echo "IGUAL $n $*: $k cuadros"; iguales=$((iguales+1)); cuadros=$((cuadros+k))
  else echo "DISTINTO $n $*: $(diff "$ref" "$sal/$n$suf.txt" | grep -c '^>') lineas distintas ($(diff "$ref" "$sal/$n$suf.txt" | grep '^>' | grep -c 'cuadro') cuadros)"; distintas=$((distintas+1)); fi
}
for c in "$TARJETA"/cintas/*.cinta; do una "$c"; done
for n in 00-sintetica 01-pared-disco-buscador-ayuda 08-perf-todo 09-medir 10-pared-1280 n1-nativa-pantallas n2-nativa-aot; do una "$TARJETA/cintas/$n.cinta" --limite-textura 256; done
una "$TARJETA/cintas/00-sintetica.cinta" --limite-textura 64
una "$TARJETA/cintas/09-medir.cinta" --redimensiona 40:1280x1024,200:800x600,500:1024x768
una "$TARJETA/cintas/09-medir.cinta" --redimensiona 40:1280x1024,200:800x600
una "$TARJETA/cintas/06-perf-grano.cinta" --redimensiona 5:640x480,20:1366x768
una "$TARJETA/cintas/05-perf-cuarto.cinta" --redimensiona 10:1600x900 --limite-textura 512
echo "RESUMEN: $iguales iguales ($cuadros cuadros), $distintas distintas"
