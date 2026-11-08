#!/bin/bash
# compara.sh A B [ESCENA ...]: compara las escenas de la corrida B (p. ej. N1, la traduccion) con las de A (p. ej. R1, la referencia)
# con la holgura del ruido medido ($RUIDO/ESCENA.json o $W/ruido/ESCENA.json, de ruido.sh). Dice IGUAL o DIFERENTE por escena; sale con 1 si alguna difiere.
G=$(dirname "$(readlink -f "$0")")
W=${W:-/root/rocola-trabajo/port/ventanas}; SAL=${SAL:-$W/salidas}
a="$1"; b="$2"; shift 2
todas="arranque moneda video tab buscador cierreq altf4 sigterm f3sigterm kill9"
esc="${*:-$todas}"; malo=0
for e in $esc; do
  r=(); for d in ${RUIDO:-} "$W/ruido"; do [ -f "$d/$e.json" ] && { r=(--ruido "$d/$e.json"); break; }; done
  s=$(python3 "$G/compara_escena.py" "$SAL/$a-$e" "$SAL/$b-$e" "${r[@]}"); c=$?
  echo "== $e: $(echo "$s" | tail -1)"; [ $c -ne 0 ] && { echo "$s" | grep -v ": igual$\|pixeles distintos (pantalla\|: 0 pixeles"; malo=1; }
done
exit $malo
