#!/bin/bash
# pantallas.sh PROGRAMA NOMBRE [PANTALLA...] -- corre la rocola en cada pantalla (todas si no se dicen) con ROCOLA_TRAZA_DIBUJOS (un
# cuadro) y ROCOLA_ERRORES_GL=1, y deja la traza canonica (sin caja ni color, las texturas por orden de aparicion: traza-canon.py) en
# $TARJETA/trazas/NOMBRE/PANTALLA.txt y la captura en $TARJETA/trazas/NOMBRE/PANTALLA.png. Despues: estados.py REF1 REF2 NOMBRE...
set -u
. "$(dirname "$(readlink -f "$0")")/comun.sh"
prog=$1; nom=$2; shift 2
todas="pared disco buscador asistente video perf-todo perf-grano perf-media ctrlf3 atraccion"
for p in ${@:-$todas}; do
  cfg=""; pulsa=""
  case $p in
    pared) t=8 ;;
    disco) t=7.5; pulsa="6:13" ;;
    buscador) t=8.5; pulsa="6:27+38" ;;
    asistente) t=9; pulsa="6:27+40" ;;
    video) t=15; pulsa="7:27+39,8.5:27+39,10:13,11:67,12.5:13" ;;
    perf-todo) t=8; cfg="cielo_media=1 cielo_cuarto=1 grano_vineta=1 fondo_capa=1" ;;
    perf-grano) t=8; cfg="grano_vineta=1" ;;
    perf-media) t=8; cfg="cielo_media=1" ;;
    ctrlf3) t=9; pulsa="4:17+114,6:clic:712:53" ;;
    atraccion) t=44 ;;
    *) echo "no conozco la pantalla $p"; continue ;;
  esac
  fin=$(python3 -c "print($t + 1)")
  r=$TARJETA/trazas/$nom/r-$p
  mkdir -p "$TARJETA/trazas/$nom"
  CONFIG="$cfg" ROCOLA_ERRORES_GL=1 ROCOLA_TRAZA_DIBUJOS=$t ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA="$fin:$TARJETA/trazas/$nom/$p.png" ROCOLA_CAPTURA_SALE=1 \
    "$guiones/corre.sh" "$prog" "$r" $(( ${fin%.*} + 60 ))
  python3 "$guiones/traza-canon.py" "$r/registro.txt" > "$TARJETA/trazas/$nom/$p.txt"
  echo "$nom $p: $(wc -l < "$TARJETA/trazas/$nom/$p.txt") dibujos, errores de OpenGL: $(grep -c 'tarjeta: error de OpenGL' "$r/registro.txt"), $(tail -1 "$r/registro.txt")"
  rm -rf "$r"
done
