#!/bin/bash
# corre.sh PROG NOMBRE SEGUNDOS [VAR=valor ...] : la rocola de W/PROG (prog-ref, prog-mio, prog-ref-semilla...) fuera de pantalla, con ALSA
# nula salvo que se diga, y una copia NUEVA de los datos de prueba SIEMPRE en W/run (las rutas guardadas salen iguales en todas). Deja en
# W/runs/NOMBRE la salida, los rocola_*.txt de despues y lo que se pida (en las variables, @ = esa carpeta). Si ya existe, la de antes se
# renombra.
#   EXTRA=archivo   un video mas en musica/Zeta Videos/Clips
#   DATOS=carpeta   los rocola_*.txt de partida (por ejemplo los de una corrida anterior, con todo medido en estas mismas rutas)
# La musica se copia con sus fechas (cp -a): la rocola la reconoce de una corrida a otra.
. "$(dirname "$0")/comun.sh"
p="$1"; n="$2"; seg="$3"; shift 3
[ -d "$W/$p" ] || { echo "no esta $W/$p (prepara.sh o prueba-fuente.sh)"; exit 1; }
d=$W/runs/$n
if [ -e "$d" ]; then mv "$d" "$d.viejo.$$"; fi
mkdir -p "$d"
rm -rf "$W/run"; mkdir -p "$W/run"; cd "$W/run" || exit 1
cp -al "$W/$p/." .
D=${DATOS:-$DATOSXP}
for f in "$D"/rocola_*.txt; do case "$f" in *.bak) ;; *) cp "$f" . ;; esac; done
cp -a "$DATOSXP/rocola_miniaturas" "$DATOSXP/musica" .
if [ -n "$EXTRA" ]; then cp "$EXTRA" "musica/Zeta Videos/Clips/"; fi
args=(); for a in "$@"; do args+=("${a//@/$d}"); done
env ROCOLA_ALSA=${ROCOLA_ALSA:-null} ROCOLA_PANTALLA=fuera:1024x768 "${args[@]}" timeout $((seg + 20)) dotnet rocola-xp.dll > "$d/salida.txt" 2>&1
echo "salio con $?" >> "$d/salida.txt"
cp rocola_*.txt "$d"/
cd "$W"; rm -rf "$W/run"
