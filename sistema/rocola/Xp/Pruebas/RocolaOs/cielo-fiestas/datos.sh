#!/bin/bash
# datos.sh [DESTINO]: arma los datos INVENTADOS de la prueba de las fiestas (FASE11_cielo-fiestas.md): una musica de 16 artistas x 2 discos x
# 2 canciones (tonos de 300 s en MP3, con sus etiquetas) con portadas de colores (portada.jpg, 500x500), para que la pared, el escaparate, el disco
# y el reposo tengan algo que pintar; sin rocola_estado.txt ni medidas. DESTINO de fabrica: $W_DATOS o ./datos-fiestas.
set -eu
D=${1:-${W_DATOS:-./datos-fiestas}}
command -v ffmpeg >/dev/null || { echo "datos.sh: falta ffmpeg" >&2; exit 1; }
rm -rf "$D"; mkdir -p "$D/musica"
artistas=("Los Ejemplos" "Ana Prueba" "Banda Falsa" "Cumbia Uno" "Dueto Dos" "El Tercero" "Fulano y Mengano" "Grupo Cuatro" "Hermanos Cinco" "Inventada" "Juan Demo" "Kiko Test" "Las Seis" "Mariachi Siete" "Norte Ocho" "Orquesta Nueve")
colores=(c0=0xE03C3C:c1=0x3C78FF c0=0x22A05A:c1=0xFFC83C c0=0x8E44C8:c1=0xFF7AB8 c0=0xFF8A2A:c1=0x3CC8FF c0=0x00A05A:c1=0xD8202E c0=0x3C78FF:c1=0xF4F4F4 c0=0xFF4F6E:c1=0xFFC93C c0=0x7CD957:c1=0x1b1b5e)
i=0
for a in "${artistas[@]}"; do
  for d in 1 2; do
    P="$D/musica/$a/Disco $d"; mkdir -p "$P"
    c=${colores[$(( (i + d) % ${#colores[@]} ))]}
    ffmpeg -v error -y -f lavfi -i "gradients=s=500x500:$c:seed=$((i * 7 + d)):duration=1:speed=0.01" -frames:v 1 "$P/portada.jpg"
    for t in 1 2; do
      f=$(( 220 + i * 23 + d * 41 + t * 60 ))
      ffmpeg -v error -y -f lavfi -i "sine=frequency=$f:duration=300:sample_rate=44100" -af volume=0.5,aformat=channel_layouts=stereo -c:a libmp3lame -ac 2 -b:a 48k \
        -metadata artist="$a" -metadata album="Disco $d" -metadata title="Cancion $t de $a" -metadata track="$t" "$P/0$t Cancion $t.mp3"
    done
  done
  i=$((i + 1))
done
printf 'dir=%s\r\n' "$(cd "$D/musica" && pwd)" > "$D/rocola_config.txt"
echo "datos en $D: $(find "$D/musica" -name '*.mp3' | wc -l) canciones"
printf 'arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=27\r\nmoneda=45\r\nsaltar=16\r\n' > "$D/rocola_teclas.txt"
