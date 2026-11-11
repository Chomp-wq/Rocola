#!/bin/bash
# atraccion.sh PROG SALIDA: el recorrido «atraccion» SIN el escaparate (escaparate=0), lo que los nueve recorridos no pasan: alli la atraccion
# va con el escaparate y sus textos los pone AtraccionGpu.cs; sin el, EnsureUiOverlay (Form1.Video.cs) escribe en la imagen de la interfaz
# «N canciones» y «toca un boton para elegir» (las dos lineas de DrawString de su rama de la atraccion). Con las capas y las texturas como
# recorridos.sh con VOLCADO=1 CAPAS=1 HUELLAS=1. Usa Recorridos/corre.sh (la carpeta fija de siempre): correrlo SIEMPRE con el candado de los
# recorridos, como ellos.
set -u
PROG=$1; SAL=$2
R=$(cd "$(dirname "$0")/../Recorridos" && pwd)
P="70:39"; C="45,52,59,66,73"
for cfg in ${CONFIGS:-escaparate=0}; do
  n=atraccion-${cfg%%=*}
  S="$SAL/$n"; rm -rf "$S"; mkdir -p "$S/tex"
  caps=""; for c in ${C//,/ }; do caps="$caps,$c:$S/c$c.png"; done; caps=${caps#,}
  CONFIG="$cfg" SIN_TECLAS=0 TIEMPO=101 "$R/corre.sh" "$PROG" "$S" ROCOLA_PULSA="$P" ROCOLA_CAPTURA="$caps" ROCOLA_CAPTURA_SALE=1 \
    ROCOLA_CAPTURA_CAPAS=1 ROCOLA_VOLCADO_TEXTURAS="$S/tex"
  [ "${HUELLAS:-1}" = 1 ] && python3 "$R/huellas.py" "$S"   # (HUELLAS=0: deja los PNG para mirarlos)
  echo "$n: $(tail -1 "$S/salida.txt")"
done
