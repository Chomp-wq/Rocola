#!/bin/bash
# compara.sh [CARPETA]: el arnes de ventanas2 C (Vistas.cs) compilado con la referencia congelada y con esta copia, corrido con las letras
# de XP falsas y con DejaVu (sin ROCOLA_LETRAS_XP), y las salidas comparadas linea por linea. CARPETA (de fabrica
# /root/rocola-trabajo/port/ventanas2/vistas) queda fuera del repositorio: los proyectos copiados, lo compilado (se borra al acabar) y las
# salidas. La referencia no se toca (solo se lee).
set -eu
AQUI=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$AQUI/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$XP/Pruebas/Comun
W=${1:-/root/rocola-trabajo/port/ventanas2/vistas}
REF=${REF:-/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp}
LETRAS=${LETRAS_FALSAS:-/root/rocola-trabajo/port/dibujo/letras-xp-falsas}
mkdir -p "$W"
for cual in ${CUALES:-ref nuevo}; do
  case $cual in ref) F=$REF;; *) F=$XP;; esac
  rm -rf "$W/proj-$cual" "$W/bin-$cual"; mkdir -p "$W/proj-$cual"; cp "$AQUI/VistasAjustes.csproj" "$AQUI/Vistas.cs" "$W/proj-$cual/"
  (cd "$W/proj-$cual" && dotnet build -c Release -nologo -v q -p:Fuente="$F" -o "$W/bin-$cual" 2>&1 | grep -E " error |rror\(s\)" || true)
  env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_LETRAS_XP="$LETRAS" dotnet "$W/bin-$cual/VistasAjustes.dll" > "$W/$cual-falsas.tsv" 2> "$W/$cual-falsas.err"
  env -u ROCOLA_LETRAS_XP ROCOLA_PANTALLA=fuera:64x64 dotnet "$W/bin-$cual/VistasAjustes.dll" > "$W/$cual-dejavu.tsv" 2> "$W/$cual-dejavu.err"
  rm -rf "$W/proj-$cual" "$W/bin-$cual"
done
for l in falsas dejavu; do
  echo "== letras $l: $(wc -l < "$W/nuevo-$l.tsv") lineas, $(grep -c 'EXC:' "$W/nuevo-$l.tsv" || true) con excepcion"
  echo "   nuevo contra la referencia: $(diff "$W/ref-$l.tsv" "$W/nuevo-$l.tsv" | grep -c '^>' || true) distintas de $(wc -l < "$W/nuevo-$l.tsv")"
done
