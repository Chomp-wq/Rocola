#!/bin/bash
# reproduce.sh CINTA PROGRAMA [opciones del reproductor] -- la cinta en la referencia congelada ($TARJETA/ref) y en PROGRAMA (la carpeta
# con su rocola-xp.dll); compara las dos salidas linea por linea (IGUAL o DISTINTO).
set -u
. "$(dirname "$(readlink -f "$0")")/comun.sh"
cinta=$1; prog=$2; shift 2
n=$(basename "$cinta" .cinta); suf=$(echo "$*" | tr -c 'a-zA-Z0-9\n' '_' | tr -s '_')
s=$TARJETA/salidas; mkdir -p "$s"
dotnet "$TARJETA/reproductor/reproductor-cintas.dll" --programa "$TARJETA/ref" --cinta "$cinta" --sale "$s/$n$suf.ref.txt" "$@" | tail -1 | sed 's/^/  ref: /'
dotnet "$TARJETA/reproductor/reproductor-cintas.dll" --programa "$prog" --cinta "$cinta" --sale "$s/$n$suf.nat.txt" "$@" | tail -1 | sed 's/^/  nat: /'
if diff -q "$s/$n$suf.ref.txt" "$s/$n$suf.nat.txt" > /dev/null; then
  echo "IGUAL $n $*: $(grep -c '^cuadro' "$s/$n$suf.ref.txt") cuadros"
else
  echo "DISTINTO $n $*: $(diff "$s/$n$suf.ref.txt" "$s/$n$suf.nat.txt" | grep -c '^>') lineas distintas ($(diff "$s/$n$suf.ref.txt" "$s/$n$suf.nat.txt" | grep '^>' | grep -c 'cuadro') cuadros)"
fi
