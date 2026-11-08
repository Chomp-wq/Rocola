#!/bin/bash
# moneda.sh PROG SALIDA [conserva] (con flock /root/rocola-trabajo/port/recorridos.lock y ROCOLA_TRABAJO, como recorridos.sh): un recorrido propio del grupo «suena» (las monedas que caen, el "+N creditos", el aviso de pedir con y
# sin creditos y el estado extendido con el titulo que se desliza), con el volcado de TODAS las texturas (ROCOLA_VOLCADO_MINIMO=1: tambien
# las chicas, que los recorridos de siempre no vuelcan) y sus huellas. Con 'conserva' deja los PNG.
set -u
PROG=$1; S=$2; CONSERVA=${3:-}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
rm -rf "$S"; mkdir -p "$S/tex"
P="7:39,8:39,19:13,20.5:40,21.5:38,22.5:13,24:40,25:13,26.5:27,30:45,33:45,36:13,37:40,38:13,39:27,66:45"
caps=""; for c in 31 34 38.5 45 60 67 70; do caps="$caps,$c:$S/c$c.png"; done; caps=${caps#,}
export SIN_TECLAS=0 CONFIG=""
TIEMPO=95 "$X/Pruebas/Recorridos/corre.sh" "$PROG" "$S" ROCOLA_PULSA="$P" ROCOLA_CAPTURA="$caps" ROCOLA_CAPTURA_SALE=1 \
  ROCOLA_CAPTURA_CAPAS=1 ROCOLA_VOLCADO_TEXTURAS="$S/tex" ROCOLA_VOLCADO_MINIMO=1
if [ -n "$CONSERVA" ]; then
  mkdir -p "$S/png"; cp -p "$S"/tex/*.png "$S/png/" 2>/dev/null
fi
python3 "$X/Pruebas/Recorridos/huellas.py" "$S"
echo "moneda: $(tail -1 $S/salida.txt) $(grep -c textura $S/huellas.tsv) texturas"
