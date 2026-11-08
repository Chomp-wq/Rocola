#!/bin/bash
# control.sh NOMBRE ARCHIVO VIEJO NUEVO CINTA... -- control negativo: copia lo que compila el programa de este arbol, cambia en
# Xp/ARCHIVO el texto VIEJO por NUEVO (un fallo puesto a proposito), lo compila aparte y reproduce esas cintas ($TARJETA/cintas/CINTA.cinta)
# contra la salida de la referencia ($TARJETA/salidas/CINTA.ref.txt, de reproduce.sh): un control negativo tiene que salir distinto.
set -eu
. "$(dirname "$(readlink -f "$0")")/comun.sh"
nom=$1; arch=$2; old=$3; new=$4
d=$TARJETA/control/$nom; rm -rf "$d"; mkdir -p "$d/src/Consola/Pantalla" "$d/src/Motor"
rsync -a --exclude bin --exclude obj --exclude Pruebas "$arbol/Xp" "$d/src/"
cp "$arbol/Consola/Pantalla/Gl.cs" "$arbol/Consola/Pantalla/Kms.cs" "$d/src/Consola/Pantalla/"; cp "$arbol/Motor/Botones.cs" "$d/src/Motor/"
python3 - "$d/src/Xp/$arch" "$old" "$new" <<'PY'
import sys
f, old, new = sys.argv[1], sys.argv[2], sys.argv[3]
s = open(f, encoding="utf-8").read()
assert s.count(old) >= 1, "no esta: " + old
open(f, "w", encoding="utf-8").write(s.replace(old, new))
PY
(cd "$d/src/Xp" && dotnet build -c Release -o "$d/salida" 2>&1 | grep -E "rror\(s\)")
shift 4
for c in "$@"; do
  dotnet "$TARJETA/reproductor/reproductor-cintas.dll" --programa "$d/salida" --cinta "$TARJETA/cintas/$c.cinta" --sale "$d/$c.txt" > /dev/null 2>&1 || true
  total=$(grep -c "^cuadro" "$TARJETA/salidas/$c.ref.txt")
  dist=$(diff <(grep "^cuadro" "$TARJETA/salidas/$c.ref.txt" | awk '{print $5}') <(grep "^cuadro" "$d/$c.txt" | awk '{print $5}') | grep -c "^>" || true)
  echo "control $nom, $c: $dist de $total cuadros distintos"
done
rm -rf "$d/src" "$d/salida"
