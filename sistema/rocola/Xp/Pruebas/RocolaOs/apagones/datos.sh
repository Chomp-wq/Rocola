#!/bin/bash
# datos.sh [ORIGEN]: arma DATOS0 (de fabrica /root/rocola-trabajo/fase11/apagones/datos0), los datos de partida de prueba.sh: la musica y
# rocola_teclas.txt de los datos de las escenas de ventanas (ORIGEN: la carpeta del programa de la copia principal, como
# Pruebas/Ventanas/guiones/datos.sh), SIN los demas rocola_*.txt (la rocola arranca sin estado ni medidas), y las tres canciones de «Los
# Ejemplos - Disco Uno» cambiadas por tonos de 30 s (MP3 192 kbps, estereo, 44,1 kHz) que cambian a la mitad: la 1 440 Hz y 470 Hz, la 2
# 550/580, la 3 660/690. prueba.sh lo llama solo si DATOS0 no existe.
set -u
D=${DATOS0:-/root/rocola-trabajo/fase11/apagones/datos0}; O=${1:-/home/user/Rocola/sistema/rocola/Xp/bin/Release/net10.0}
[ -d "$O/musica" ] && [ -f "$O/rocola_teclas.txt" ] || { echo "datos.sh: falta $O/musica o $O/rocola_teclas.txt" >&2; exit 1; }
command -v ffmpeg >/dev/null || { echo "datos.sh: falta ffmpeg" >&2; exit 1; }
rm -rf "$D"; mkdir -p "$D"
cp -r "$O/musica" "$D/" && cp "$O/rocola_teclas.txt" "$D/" || exit 1
E="$D/musica/Los Ejemplos/Disco Uno"; mkdir -p "$E"
for t in "01 Cancion 1:440:470" "02 Cancion 2:550:580" "03 Cancion 3:660:690"; do
  n=${t%%:*}; r=${t#*:}; a=${r%%:*}; b=${r#*:}
  ffmpeg -v error -y -f lavfi -i "sine=frequency=$a:duration=15:sample_rate=44100" -f lavfi -i "sine=frequency=$b:duration=15:sample_rate=44100" \
    -filter_complex "[0][1]concat=n=2:v=0:a=1,volume=0.7,aformat=channel_layouts=stereo" -c:a libmp3lame -b:a 192k "$E/$n.mp3" || exit 1
done
ls -R "$D" | head -20; du -sh "$D"
