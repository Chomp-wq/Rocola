#!/bin/bash
# arnes.sh [CARPETA]: el arnes del grupo «vistas-b» (VistasB.cs) compilado con la referencia congelada, con la base de la tanda (ae8b50b) y
# con esta copia, corrido con las letras de XP falsas y con DejaVu (sin ROCOLA_LETRAS_XP), y las salidas comparadas linea por linea.
# CARPETA (de fabrica /root/rocola-trabajo/port/vistas-b/arnes) queda fuera del repositorio: los proyectos copiados, lo compilado y las
# salidas. La referencia no se toca (solo se lee). CUALES="nuevo" repite solo esta copia.
set -eu
AQUI=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$AQUI/../.." && pwd); RAIZ=$(cd "$XP/../../.." && pwd)
W=${1:-/root/rocola-trabajo/port/vistas-b/arnes}
REF=${REF:-/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp}
BASE=${BASE:-ae8b50b771a63fcf19c57e5fd73f5535fce089e7}
LETRAS=${LETRAS_FALSAS:-/root/rocola-trabajo/port/dibujo/letras-xp-falsas}
mkdir -p "$W"
for cual in ${CUALES:-ref base nuevo}; do
  case $cual in
    ref) F=$REF;;
    base) rm -rf "$W/base"; mkdir -p "$W/base"
      git -C "$RAIZ" archive "$BASE" sistema/rocola/Xp/Codigo sistema/rocola/Xp/Plataforma sistema/rocola/Xp/Nativo sistema/rocola/Consola/Pantalla/Gl.cs \
        sistema/rocola/Consola/Pantalla/Kms.cs sistema/rocola/Motor/Botones.cs | tar -xf - -C "$W/base"
      F=$W/base/sistema/rocola/Xp;;
    *) F=$XP;;
  esac
  rm -rf "$W/proj-$cual" "$W/bin-$cual"; mkdir -p "$W/proj-$cual"; cp "$AQUI/VistasVistasB.csproj" "$AQUI/VistasB.cs" "$W/proj-$cual/"
  (cd "$W/proj-$cual" && dotnet build -c Release -nologo -v q -p:Fuente="$F" -o "$W/bin-$cual" 2>&1 | grep -E " error |rror\(s\)" || true)
  env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_LETRAS_XP="$LETRAS" dotnet "$W/bin-$cual/VistasVistasB.dll" > "$W/$cual-falsas.tsv" 2> "$W/$cual-falsas.err" || echo "$cual (falsas): salio con $?"
  env -u ROCOLA_LETRAS_XP ROCOLA_PANTALLA=fuera:64x64 dotnet "$W/bin-$cual/VistasVistasB.dll" > "$W/$cual-dejavu.tsv" 2> "$W/$cual-dejavu.err" || echo "$cual (dejavu): salio con $?"
  rm -rf "$W/proj-$cual/obj" "$W/base"
done
for l in falsas dejavu; do
  echo "== letras $l: $(wc -l < "$W/nuevo-$l.tsv") lineas, $(grep -c 'EXC:' "$W/nuevo-$l.tsv" || true) con excepcion, $(grep -c 'RELOJ:' "$W/nuevo-$l.tsv" || true) sin reloj quieto"
  [ -f "$W/ref-$l.tsv" ] && echo "   nuevo contra la referencia: $(diff "$W/ref-$l.tsv" "$W/nuevo-$l.tsv" | grep -c '^>' || true) distintas de $(wc -l < "$W/nuevo-$l.tsv") (la referencia: $(wc -l < "$W/ref-$l.tsv") lineas, $(grep -c 'RELOJ:' "$W/ref-$l.tsv" || true) sin reloj quieto)"
  [ -f "$W/base-$l.tsv" ] && echo "   nuevo contra la base: $(diff "$W/base-$l.tsv" "$W/nuevo-$l.tsv" | grep -c '^>' || true) distintas; base contra la referencia: $(diff "$W/ref-$l.tsv" "$W/base-$l.tsv" | grep -c '^>' || true) distintas"
done
