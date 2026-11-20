#!/bin/bash
# NV5b (20-11-2026; RocolaOs/RocolaOs.NocheVideo.cs): de noche, a los videos de Rocola OS el mismo estante de graves y el mismo corte del grave
# que a la musica, por el filtro de audio de mpv, puesto al empezar cada video.
#   prueba.sh PROG     PROG: una carpeta con rocola-xp.dll (JIT) o el ejecutable de NativeAOT
#   W: la carpeta de trabajo (/root/rocola-trabajo/fase11/noche-nv/video; se rehace)
# Hace con ffmpeg un video de 40 s con 50 Hz (0,3) y 1 kHz (0,1) y lo pide en la rocola entera (Pruebas/Windows/corre.sh, la misma pulsacion de
# Pruebas/Windows/video.sh) tres veces: de noche (23 h, una zona Etc/GMT de tzdata) sin ROCOLA_OS (lo de XP), de noche con ROCOLA_OS=noche_video
# y de dia (12 h) con ROCOLA_OS=noche_video. El sonido de mpv va a un WAV por el plugin «file» de ALSA (ROCOLA_VIDEO_OPCIONES con su
# audio-device; la rocola, a null). OJO: ese WAV dice s32 en la cabecera pero lleva float32 (lo que le manda mpv): mide.py --float. Comprueba
# el filtro que se puso (la traza ROCOLA_VIDEO_TRAZA), que de noche el 50 Hz baje frente al 1 kHz lo que baja con el mismo filtro en mpv
# suelto (unos 24 dB: el estante de 8 dB y el corte de cuarto orden a 80 Hz) y que el 1 kHz no cambie, y que de dia y sin Rocola OS el video
# suene como el archivo.
AQUI=$(cd "$(dirname "$0")" && pwd); X=$(cd "$AQUI/../../.." && pwd)
P=$(cd "$1" && pwd) || exit 1
W=${W:-/root/rocola-trabajo/fase11/noche-nv/video}
rm -rf "$W"; mkdir -p "$W/medios" "$W/prog"
cp -al "$P/." "$W/prog/" 2>/dev/null || cp -a "$P/." "$W/prog/"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
entre() { python3 -c "import sys; v=float(sys.argv[1]); sys.exit(0 if float(sys.argv[2]) <= v <= float(sys.argv[3]) else 1)" "$1" "$2" "$3"; }
zona() { local h=$1 u o; u=$(date -u +%H); o=$(( (h - 10#$u + 12 + 48) % 24 - 12 )); if [ $o -ge 0 ]; then echo "Etc/GMT-$o"; else echo "Etc/GMT+$(( -o ))"; fi; }
V="$W/medios/Un video largo.mp4"
ffmpeg -loglevel error -y -f lavfi -i "aevalsrc=0.30*sin(2*PI*50*t)+0.10*sin(2*PI*1000*t)|0.30*sin(2*PI*50*t)+0.10*sin(2*PI*1000*t):s=44100:d=40" \
  -f lavfi -i "color=c=blue:s=320x240:r=25:d=40" -shortest -c:v libx264 -pix_fmt yuv420p -c:a aac -b:a 256k "$V" || exit 1
AF='lavfi=[lowshelf=f=120:t=q:w=0.7071:g=-8.00,highpass=f=80.0:t=q:w=0.5412,highpass=f=80.0:t=q:w=1.3066]'
mpv --no-config --vo=null --ao=pcm --ao-pcm-file="$W/mpv-sin.wav" "$V" > /dev/null 2>&1
mpv --no-config --vo=null --ao=pcm --ao-pcm-file="$W/mpv-con.wav" --af="$AF" "$V" > /dev/null 2>&1
for caso in xp-noche:23: nv-noche:23:ROCOLA_OS=noche_video nv-dia:12:ROCOLA_OS=noche_video; do
  IFS=: read -r n h os <<< "$caso"
  W=$W EXTRA="$V" flock /root/rocola-trabajo/fase11/pesado.lock bash "$X/Pruebas/Windows/corre.sh" prog "$n" 56 \
    ROCOLA_PULSA="7:27+39,8.5:27+39,10:13,11:67,12.5:13" ROCOLA_VIDEO_TRAZA=1 TZ=$(zona $h) $os \
    ROCOLA_ALSA=null ROCOLA_VIDEO_OPCIONES="audio-device=alsa/file:FILE=@/video.wav,FORMAT=wav"
done
python3 "$AQUI/mide.py" "$W/mpv-sin.wav" "$W/mpv-con.wav" > "$W/medidas.txt"
python3 "$AQUI/mide.py" --float "$W/runs/xp-noche/video.wav" "$W/runs/nv-noche/video.wav" "$W/runs/nv-dia/video.wav" >> "$W/medidas.txt"
cat "$W/medidas.txt"
d() { grep "$1" "$W/medidas.txt" | sed -n 's/.*1 kHz: mediana \([-0-9.]*\) dB.*/\1/p'; }
k() { grep "$1" "$W/medidas.txt" | sed -n 's/.*; 1 kHz mediana \([-0-9.]*\) dB.*/\1/p'; }
af() { grep -o 'video: af «[^»]*»' "$W/runs/$1/salida.txt" | head -1; }
si "sin Rocola OS el filtro de mpv ni se toca (ninguna traza «video: af»)" test -z "$(af xp-noche)"
si "de noche con noche_video: $(af nv-noche)" test "$(af nv-noche)" = "video: af «$AF»"
si "de dia con noche_video: sin filtro ($(af nv-dia))" test "$(af nv-dia)" = "video: af «»"
si "el archivo: 50 Hz - 1 kHz = $(d mpv-sin) dB (0,3 frente a 0,1: 9,54)" entre "$(d mpv-sin)" 9.4 9.7
si "mpv suelto con el mismo filtro: $(d mpv-con) dB (el 50 Hz baja unos 24 dB)" entre "$(echo "$(d mpv-con) - $(d mpv-sin)" | bc)" -25.5 -22.5
si "de noche sin Rocola OS el video suena como el archivo ($(d xp-noche) dB)" entre "$(echo "$(d xp-noche) - $(d mpv-sin)" | bc)" -0.2 0.2
si "de noche con noche_video baja lo mismo que con mpv suelto ($(d nv-noche) dB frente a $(d mpv-con))" entre "$(echo "$(d nv-noche) - $(d mpv-con)" | bc)" -0.3 0.3
si "y el 1 kHz no cambia ($(k nv-noche) frente a $(k xp-noche) dB, con el mismo volumen de noche)" entre "$(echo "$(k nv-noche) - $(k xp-noche)" | bc)" -0.3 0.3
si "de dia con noche_video el video suena como el archivo ($(d nv-dia) dB)" entre "$(echo "$(d nv-dia) - $(d mpv-sin)" | bc)" -0.2 0.2
echo; echo "$bien bien, $mal mal"
[ $mal = 0 ]
