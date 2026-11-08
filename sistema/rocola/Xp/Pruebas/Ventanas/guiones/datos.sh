#!/bin/bash
# datos.sh [ORIGEN]: arma $W/datos0, los datos de prueba de partida de cada corrida (corre.sh copia una COPIA NUEVA en cada una): la musica,
# los rocola_*.txt (sin .bak, con rocola_estado.txt: la rocola arranca reanudando, como tras un apagon), rocola_teclas.txt y
# rocola_miniaturas. ORIGEN por defecto: la carpeta del programa de la copia principal (sistema/rocola/Xp/bin/Release/net10.0).
W=${W:-/root/rocola-trabajo/port/ventanas}; O=${1:-/home/user/Rocola/sistema/rocola/Xp/bin/Release/net10.0}
rm -rf "$W/datos0"; mkdir -p "$W/datos0"
cp -r "$O/musica" "$O/rocola_miniaturas" "$W/datos0/"
for f in "$O"/rocola_*.txt; do cp "$f" "$W/datos0/"; done
ls "$W/datos0"; du -sh "$W/datos0"
