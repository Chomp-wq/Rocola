#!/bin/bash
# datos_h.sh: los datos de prueba de las escenas de la hoja (ventanas2, paso H), sacados de los de siempre ($W/datos0), en $DH:
#   $DH/barras   con 21 carpetas de musica de mas en rocola_config.txt (dir=): una que existe y esta vacia ($DH/extra-vacia, la primera) y 20 que
#                no estan con nombres largos: la lista de Ctrl+F2 lleva las dos barras
#   $DH/sin      sin la carpeta de la musica: al arrancar, la hoja de Ctrl+F2 se abre sola (PideElegirMusica, Form1.cs)
#   $DH/arbol    unas carpetas para el selector de carpetas (ROCOLA_CARPETAS_RAIZ en la escena f2agrega)
W=${W:-/root/rocola-trabajo/port/ventanas}; DH=${DH:-/root/rocola-trabajo/port/ventanas2/datos}
rm -rf "$DH"; mkdir -p "$DH/barras" "$DH/sin" "$DH/extra-vacia" "$DH/arbol/Discos/Uno/Rock" "$DH/arbol/Discos/Uno/Baladas" "$DH/arbol/Discos/Dos" "$DH/arbol/Otra"
cp -r "$W/datos0"/. "$DH/barras/"
{ printf 'dir=%s\r\n' "$DH/extra-vacia"; for i in $(seq -w 1 20); do printf 'dir=/media/disco-de-la-musica-%s/Coleccion de musica con un nombre muy largo para la barra horizontal %s\r\n' "$i" "$i"; done; } > "$DH/barras/rocola_config.txt"
cp -r "$W/datos0"/. "$DH/sin/"; rm -rf "$DH/sin/musica"
du -sh "$DH"
