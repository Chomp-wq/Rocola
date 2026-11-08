#!/bin/bash
# sonido.sh PROG NOMBRE SEGUNDOS PULSA [VAR=valor ...] : la rocola de W/PROG con la tarjeta de prueba con reloj (alsa/reloj.c: suena a su ritmo
# y guarda en runs/NOMBRE/sonado.raw lo que de verdad sono -16 bits, la frecuencia que pidio la rocola- y en registro.txt cada arranque,
# parada, pausa y XRUN), las teclas PULSA (ROCOLA_PULSA) y salida con la captura de los SEGUNDOS-1. Como corre.sh (DATOS=, EXTRA=).
. "$(dirname "$0")/comun.sh"
reloj
p="$1"; n="$2"; seg="$3"; pulsa="$4"; shift 4
d=$W/runs/$n
cfg=$W/alsa/$n.conf
cat > "$cfg" <<EOC
pcm_type.reloj { lib "$W/alsa/libasound_module_pcm_reloj.so" }
pcm.reloj { type reloj  file "$d/sonado.raw"  registro "$d/registro.txt" }
EOC
if [ -e "$d" ]; then mv "$d" "$d.viejo.$$"; fi
ROCOLA_ALSA=reloj "$AQUI/corre.sh" "$p" "$n" "$seg" ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$cfg ROCOLA_PULSA="$pulsa" \
  ROCOLA_CAPTURA="$((seg-1)):@/fin.png" ROCOLA_CAPTURA_SALE=1 "$@"
rm -f "$cfg"
