#!/bin/bash
# pruebas41.sh PROGRAMA ETIQUETA: compila las 41 pruebas de las ventanas (../Pruebas41) contra el rocola-xp.dll de PROGRAMA (la carpeta
# de la compilacion JIT: la referencia o la traduccion) fuera del repositorio ($W/p41/ETIQUETA) y las corre fuera de pantalla. Deja la
# salida en $W/p41/ETIQUETA/salida.txt y dice «pasan N, fallan M». La compilacion (unos 75 MB) se borra al acabar salvo con GUARDA=1.
G=$(dirname "$(readlink -f "$0")")
W=${W:-/root/rocola-trabajo/port/ventanas}
prog="$(readlink -f "$1")"; et="$2"; o="$W/p41/$et"
rm -rf "$o"; mkdir -p "$o/src"
cp "$G/../Pruebas41/Program.cs" "$G/../Pruebas41/Pruebas41.csproj" "$o/src/"
# (un programa con las teclas nativas -Rocola.Tecla, ventanas2 T- compila las pruebas con Nativo=true: Keys es Tecla)
nativo=false; grep -q "PulsoTecla" "$prog/rocola-xp.dll" && nativo=true
dotnet build "$o/src/Pruebas41.csproj" -c Release -p:Rocola="$prog" -p:Nativo=$nativo -o "$o/bin" -nologo -v q > "$o/compila.txt" 2>&1 || { echo "no compila (ver $o/compila.txt)"; tail -5 "$o/compila.txt"; exit 2; }
( cd "$o/bin" && ROCOLA_PANTALLA=fuera:1024x768 timeout 180 dotnet Pruebas41.dll > "$o/salida.txt" 2>&1 )
[ "${GUARDA:-0}" = 1 ] || rm -rf "$o/bin" "$o/src/obj"
grep "^FALLA" -A1 "$o/salida.txt"; tail -1 "$o/salida.txt"
grep -q "pasan 41, fallan 0" "$o/salida.txt"
