#!/bin/bash
# ruido.sh REF N [ESCENA ...]: corre N veces las escenas con la referencia (REF = carpeta con rocola-xp.dll o aot:CARPETA; etiquetas
# R1..RN) y mide el ruido de la referencia contra si misma: $W/ruido/ESCENA.json (lo que mas se separan dos corridas en las cuentas de
# relojes, encargos y repeticiones; y si la secuencia salio igual). Con SOLO_MIDE=1 no corre nada: mide con las R* que ya esten.
G=$(dirname "$(readlink -f "$0")")
W=${W:-/root/rocola-trabajo/port/ventanas}; SAL=${SAL:-$W/salidas}
ref="$1"; n="$2"; shift 2
todas="arranque moneda video tab buscador cierreq altf4 sigterm f3sigterm kill9"
esc="${*:-$todas}"
if [ "${SOLO_MIDE:-0}" != 1 ]; then for i in $(seq 1 "$n"); do "$G/escenas.sh" "$ref" "R$i" $esc; done; fi
mkdir -p "$W/ruido"
for e in $esc; do
  t=(); for i in $(seq 1 "$n"); do [ -f "$SAL/R$i-$e/traza.txt" ] && t+=("$SAL/R$i-$e/traza.txt"); done
  echo -n "$e (${#t[@]} corridas): "; python3 "$G/compara_trazas.py" --mide-ruido "$W/ruido/$e.json" "${t[@]}"
done
