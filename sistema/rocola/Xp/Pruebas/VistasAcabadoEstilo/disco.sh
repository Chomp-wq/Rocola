#!/bin/bash
# disco.sh PROG SALIDA: el recorrido «dentro del disco» del grupo «acabado-estilo» con la lista SIN el vidrio en piezas (lo que los nueve
# recorridos no pasan: alli el vidrio va en piezas), en tres configuraciones: glass_capas=0 (el vidrio pintado en la imagen de la interfaz,
# RenderTransparentGpuTint), canciones_cristal=0 (el velo) y canciones_transparentes=0 (RenderOpaqueGpu); con las capas y las texturas como
# recorridos.sh con VOLCADO=1 CAPAS=1 HUELLAS=1 (la traduccion, con la copia instrumentada de Pruebas/VistasSuena/instrumenta-volcado.sh).
# Usa Recorridos/corre.sh (la carpeta fija de siempre): correrlo SIEMPRE con el candado de los recorridos, como ellos.
set -u
PROG=$1; SAL=$2
R=$(cd "$(dirname "$0")/../Recorridos" && pwd)
P="7:13,8.5:40,9.5:40,10.5:38,11.5:39,12.5:37,13.5:27,15:13,16.5:40,18:27"
C="8,9,10,11,12,13,14,16,17.5,19.5"
for cfg in ${CONFIGS:-glass_capas=0 canciones_cristal=0 canciones_transparentes=0}; do
  n=disco-${cfg%%=*}
  S="$SAL/$n"; rm -rf "$S"; mkdir -p "$S/tex"
  caps=""; for c in ${C//,/ }; do caps="$caps,$c:$S/c$c.png"; done; caps=${caps#,}
  CONFIG="$cfg" SIN_TECLAS=0 TIEMPO=45 "$R/corre.sh" "$PROG" "$S" ROCOLA_PULSA="$P" ROCOLA_CAPTURA="$caps" ROCOLA_CAPTURA_SALE=1 \
    ROCOLA_CAPTURA_CAPAS=1 ROCOLA_VOLCADO_TEXTURAS="$S/tex"
  [ "${HUELLAS:-1}" = 1 ] && python3 "$R/huellas.py" "$S"   # (HUELLAS=0: deja los PNG para mirarlos)
  echo "$n: $(tail -1 "$S/salida.txt")"
done
