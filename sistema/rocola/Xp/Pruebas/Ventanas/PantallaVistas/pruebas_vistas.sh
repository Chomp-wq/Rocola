#!/bin/bash
# pruebas_vistas.sh PROGRAMA ETIQUETA: la prueba de las vistas falsas en la lista de la ventana (Program.cs de esta carpeta) contra el rocola-xp.dll de PROGRAMA (la
# carpeta de la compilacion JIT), compilada fuera del repositorio ($W/vistas/ETIQUETA) y corrida fuera de pantalla (llvmpipe). Deja la
# salida en $W/vistas/ETIQUETA/salida.txt y dice «pasan N, fallan M». La compilacion se borra al acabar salvo con GUARDA=1.
G=$(dirname "$(readlink -f "$0")")
W=${W:-/root/rocola-trabajo/port/pantalla-nativa}
prog="$(readlink -f "$1")"; et="${2:-actual}"; o="$W/vistas/$et"
rm -rf "$o"; mkdir -p "$o/src"
cp "$G/Program.cs" "$G/PruebasVistas.csproj" "$o/src/"
dotnet build "$o/src/PruebasVistas.csproj" -c Release -p:Rocola="$prog" -o "$o/bin" -nologo -v q > "$o/compila.txt" 2>&1 || { echo "no compila (ver $o/compila.txt)"; grep -E "error" "$o/compila.txt" | head -5; exit 2; }
( cd "$o/bin" && ROCOLA_PANTALLA=fuera:320x240 timeout 120 dotnet PruebasVistas.dll > "$o/salida.txt" 2>&1 )
[ "${GUARDA:-0}" = 1 ] || rm -rf "$o/bin" "$o/src/obj"
grep "^FALLA" -A1 "$o/salida.txt"; tail -1 "$o/salida.txt"
grep -q "fallan 0" "$o/salida.txt"
