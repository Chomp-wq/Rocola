#!/bin/bash
# video.sh PROG NOMBRE : la rocola entera con un video de 30 s (VIDEO, por omision W/medios/Un video largo.mp4) en "Zeta Videos/Clips": a los
# 7 y 8,5 s REGRESAR+DERECHA (a la ultima letra), 10 s PONER (abre el disco), 11 s MONEDA, 12,5 s PONER (pide el primer video, el de 30 s).
# Capturas a los 16 y 24 s (con el video) y 44 y 50 s (despues); las capas una vez por segundo; el sonido del video (mpv) a video.wav.
. "$(dirname "$0")/comun.sh"
EXTRA="${VIDEO:-$W/medios/Un video largo.mp4}" "$AQUI/corre.sh" "$1" "$2" 56 \
  ROCOLA_PULSA="7:27+39,8.5:27+39,10:13,11:67,12.5:13" ROCOLA_VIDEO_TRAZA=1 ROCOLA_TRAZA_CAPAS=1 \
  ROCOLA_ALSA="file:FILE=@/video.wav,FORMAT=wav" \
  ROCOLA_CAPTURA="16:@/c16.png,24:@/c24.png,44:@/c44.png,50:@/c50.png" ROCOLA_CAPTURA_SALE=1
grep "^capas" "$W/runs/$2/salida.txt" | sed 's/VideoHost#[0-9]*/VIDEO/; s/HuecoDelVideo#[0-9]*/VIDEO/' | awk '{$2=""; print}' | uniq -c > "$W/runs/$2/capas.txt"
