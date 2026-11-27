#!/bin/bash
# una escena a mano: rapida.sh PROGRAMA NOMBRE SEGUNDOS "OSTXT" VAR=valor...   (ROCOLA_OS=1 va en las variables si hace falta)
G=$(cd "$(dirname "$0")" && pwd); prog="$1"; n="$2"; seg="$3"; export OSTXT="$4"; shift 4
source "$G/comun.sh"; prepara_datos
corre "$n" "$prog" "$seg" "$@"; espera "$n"; tail -3 "$W/$n/salida.txt"; ls "$CAPTURAS" | grep "^$n" | head
