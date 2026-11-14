#!/bin/bash
# EL MUNDO DE LA PRUEBA EN QEMU (grupo qemu, 14-11-2026), TODO INVENTADO: unas canciones (tonos y ruido generados con ffmpeg), un video
# corto (barras de prueba con un tono) y la carpeta de datos de una rocola de XP FALSA, como la habria dejado XP en Windows: lo que mide
# rocola-xp de esa musica (aqui, fuera de pantalla), con las rutas pasadas a «D:\musica\...», mas los botones, la configuracion, lo mas
# sonado, el estado del dia (que NO se debe traer), el programa (Rocola.exe falso: solo cuenta su largo y su fecha) y los modelos de IA
# del repositorio (no son de nadie). Nada de canciones, letras ni nombres reales.
#   bash mundo.sh ROCOLA_XP   (el ejecutable NativeAOT de rocola-xp de esta maquina; W: la carpeta, por omision
#                              /root/rocola-trabajo/act/qemu/mundo, se rehace entera)
# Deja W/d (lo que va en la particion «D:» del disco de XP falso: musica/ y Rocola/Release/) y W/c (la «C:»: ntldr, boot.ini, WINDOWS/).
set -eu
G=$(cd "$(dirname "$0")" && pwd); REPO=$(cd "$G/../../../.." && pwd)
RX=$(cd "$(dirname "${1:?uso: mundo.sh ROCOLA_XP}")" && pwd)/$(basename "$1")
W=${W:-/root/rocola-trabajo/act/qemu/mundo}
rm -rf "$W"; mkdir -p "$W"
D=$W/d; C=$W/c; M=$D/musica; XP=$D/Rocola/Release
gen() {   # gen ARCHIVO FUENTE_LAVFI SEGUNDOS TITULO ARTISTA DISCO [opciones de salida]
	mkdir -p "$(dirname "$1")"
	ffmpeg -hide_banner -loglevel error -y -f lavfi -i "$2" -t "$3" -ac 2 -ar 44100 ${7:--c:a libmp3lame -b:a 128k} \
		-metadata title="$4" -metadata artist="$5" -metadata album="$6" "$1"
}
portada() { ffmpeg -hide_banner -loglevel error -y -f lavfi -i "$2" -frames:v 1 -q:v 4 "$1"; }

echo "=== la musica inventada (cada cancion con su tono: en el WAV grabado de QEMU se sabe cual sono)"
# Una carpeta por disco, «Artista - Disco», como la musica de la rocola de XP (XP toma la primera carpeta bajo «musica» por el disco y
# busca ahi su portada). Los tonos no repiten el de la cancion de prueba del sistema (prueba.mp3, 220 Hz).
gen "$M/Los Inventados - Disco Uno/01 Tono La.mp3" "sine=frequency=440" 45 "Tono La" "Los Inventados" "Disco Uno"
gen "$M/Los Inventados - Disco Uno/02 Tono Mi.mp3" "sine=frequency=330" 45 "Tono Mi" "Los Inventados" "Disco Uno"
gen "$M/Banda Ficticia - Primer Disco/01 Pulso Grave.mp3" "sine=frequency=196" 45 "Pulso Grave" "Banda Ficticia" "Primer Disco"
gen "$M/Banda Ficticia - Primer Disco/02 Canción Ñandú.mp3" "sine=frequency=660" 45 "Cancion Nandu" "Banda Ficticia" "Primer Disco"   # (acentos por el NTFS)
gen "$M/Grupo Prueba - Disco Dos/01 Tono Alto.mp3" "sine=frequency=1000" 45 "Tono Alto" "Grupo Prueba" "Disco Dos"
# un WAV con todos sus agudos (ruido rosa): XP solo saca material para la IA de las que conservan los agudos (Form1.Ia.cs)
gen "$M/Grupo Prueba - Disco Dos/02 Ruido Rosa.wav" "anoisesrc=color=pink:amplitude=0.3:seed=7" 30 "Ruido Rosa" "Grupo Prueba" "Disco Dos" "-c:a pcm_s16le"
portada "$M/Los Inventados - Disco Uno/folder.jpg" "color=c=0x2060c0:s=300x300"
portada "$M/Banda Ficticia - Primer Disco/folder.jpg" "color=c=0xc04020:s=300x300"
portada "$M/Grupo Prueba - Disco Dos/folder.jpg" "testsrc2=s=300x300"
# el video: barras de prueba con un tono de 880 Hz (mpeg4 parte 2 y AAC: ligero de descomprimir en QEMU)
mkdir -p "$M/Videos Inventados"
ffmpeg -hide_banner -loglevel error -y -f lavfi -i "testsrc2=s=320x240:r=25" -f lavfi -i "sine=frequency=880" -t 20 \
	-c:v mpeg4 -q:v 5 -c:a aac -b:a 96k -ac 2 -ar 44100 -metadata title="Barras de Prueba" "$M/Videos Inventados/Barras de Prueba.mp4"
# la fecha de los archivos: la de cuando se grabaron (en XP una copia conserva la de escritura); con decimales, como NTFS (100 ns)
find "$M" -type f -exec touch -m -d '2019-05-01 10:00:00.1234567' {} +

echo "=== lo que mide rocola-xp de esa musica (lo mismo que habria medido XP), fuera de pantalla"
P=$W/prep; mkdir -p "$P"
printf 'diagnostico=1\r\ndir=%s\r\n' "$M" > "$P/rocola_config.txt"
printf 'arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=27\r\nmoneda=77\r\nsaltar=83\r\n' > "$P/rocola_teclas.txt"
( cd "$P" && exec env ROCOLA_DATOS="$P" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 "$RX" ) > "$W/prep.salida.txt" 2>&1 &
pid=$!
n=$(find "$M" -type f \( -name '*.mp3' -o -name '*.wav' \) | wc -l)
medidas() { local c; c=$(grep -c '|/' "$P/$1" 2>/dev/null); echo "${c:-0}"; }
t=0
until [ "$(medidas rocola_sonoridad.txt)" -ge "$n" ] && [ "$(medidas rocola_fuentes.txt)" -ge "$n" ] && [ "$(medidas rocola_volumen.txt)" -ge "$n" ]; do
	t=$((t + 1)); [ $t -lt 400 ] && kill -0 $pid 2>/dev/null || break; sleep 0.5
done
kill -USR1 $pid 2>/dev/null   # (cierre ordenado: guarda todo lo que XP guarda al cerrar)
t=0; while kill -0 $pid 2>/dev/null && [ $t -lt 60 ]; do sleep 0.5; t=$((t + 1)); done
kill -9 $pid 2>/dev/null || true; wait $pid 2>/dev/null || true
for f in rocola_sonoridad.txt rocola_volumen.txt rocola_eq.txt rocola_fuentes.txt rocola_duracion.txt; do
	echo "  $f: $(medidas "$f") medidas"
done
[ "$(medidas rocola_volumen.txt)" -ge "$n" ] || { echo "mundo.sh: rocola-xp no midio las $n canciones (ver $W/prep.salida.txt)" >&2; exit 1; }

# una cancion NUEVA (XP no la midio: llego despues): rocola-xp la mide en la rocola, y como es un WAV con todos sus agudos, saca de ella
# material para la IA (a ROCOLA_IA_DATOS)
gen "$M/Grupo Prueba - Disco Dos/03 Ruido Nuevo.wav" "anoisesrc=color=pink:amplitude=0.3:seed=11" 20 "Ruido Nuevo" "Grupo Prueba" "Disco Dos" "-c:a pcm_s16le"
touch -m -d '2019-05-02 10:00:00.1234567' "$M/Grupo Prueba - Disco Dos/03 Ruido Nuevo.wav"
echo "=== la carpeta de la rocola de XP falsa (D:\\Rocola\\Release) y la C: de Windows XP falsa"
python3 -I "$G/mundo_xp.py" "$P" "$D/" "$XP" "$REPO"
mkdir -p "$C/WINDOWS/system32" "$C/Documents and Settings"
printf 'NTLDR FALSO (prueba de Rocola OS en QEMU: no arranca nada)\r\n' > "$C/ntldr"
printf '[boot loader]\r\ntimeout=30\r\ndefault=multi(0)disk(0)rdisk(0)partition(1)\\WINDOWS\r\n[operating systems]\r\nmulti(0)disk(0)rdisk(0)partition(1)\\WINDOWS="Windows XP falso (prueba)" /fastdetect\r\n' > "$C/boot.ini"
echo "listo: $W (d = la particion de la musica, c = la de Windows XP)"
