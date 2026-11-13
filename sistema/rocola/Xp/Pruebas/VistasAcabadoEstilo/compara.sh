#!/bin/bash
# compara.sh [CARPETA]: el arnes del grupo «acabado-estilo» (Vistas.cs) compilado con la referencia congelada, con la base de la tanda
# (b609319) y con esta copia, corrido con las letras de XP falsas y con DejaVu (sin ROCOLA_LETRAS_XP), y las salidas comparadas linea por
# linea. CARPETA (de fabrica /root/rocola-trabajo/port/acabado-estilo/vistas) queda fuera del repositorio: los proyectos copiados, lo
# compilado y las salidas. La referencia no se toca (solo se lee). Las lineas il-* (el IL de BuscadorLigeroGpu.cs con los nombres resueltos)
# se comparan solo con la base: frente a la referencia cambian por la tanda 1 (la tarjeta: D3D9.Release es Tarjeta.Suelta).
set -eu
AQUI=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$AQUI/../.." && pwd); RAIZ=$(cd "$XP/../../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$XP/Pruebas/Comun
W=${1:-/root/rocola-trabajo/port/acabado-estilo/vistas}
REF=${REF:-/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp}
BASE=${BASE:-b609319}
LETRAS=${LETRAS_FALSAS:-/root/rocola-trabajo/port/dibujo/letras-xp-falsas}
mkdir -p "$W"
rm -rf "$W/base"; mkdir -p "$W/base"
git -C "$RAIZ" archive "$BASE" sistema/rocola/Xp/Codigo sistema/rocola/Xp/Plataforma sistema/rocola/Xp/Nativo sistema/rocola/Consola/Pantalla/Gl.cs \
  sistema/rocola/Consola/Pantalla/Kms.cs sistema/rocola/Motor/Botones.cs | tar -xf - -C "$W/base"
for cual in ${CUALES:-ref base nuevo}; do
  case $cual in ref) F=$REF;; base) F=$W/base/sistema/rocola/Xp;; *) F=$XP;; esac
  rm -rf "$W/proj-$cual" "$W/bin-$cual"; mkdir -p "$W/proj-$cual"; cp "$AQUI/VistasAcabadoEstilo.csproj" "$AQUI/Vistas.cs" "$W/proj-$cual/"
  (cd "$W/proj-$cual" && dotnet build -c Release -nologo -v q -p:Fuente="$F" -o "$W/bin-$cual" 2>&1 | grep -E " error |rror\(s\)" || true)
  env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_LETRAS_XP="$LETRAS" dotnet "$W/bin-$cual/VistasAcabadoEstilo.dll" > "$W/$cual-falsas.tsv" 2> "$W/$cual-falsas.err"
  env -u ROCOLA_LETRAS_XP ROCOLA_PANTALLA=fuera:64x64 dotnet "$W/bin-$cual/VistasAcabadoEstilo.dll" > "$W/$cual-dejavu.tsv" 2> "$W/$cual-dejavu.err"
  rm -rf "$W/proj-$cual/obj"
done
for l in falsas dejavu; do
  echo "== letras $l: $(wc -l < "$W/nuevo-$l.tsv") lineas, $(grep -c 'EXC:' "$W/nuevo-$l.tsv" || true) con excepcion"
  echo "   nuevo contra la referencia (sin il-*): $(diff <(grep -v '^il-' "$W/ref-$l.tsv") <(grep -v '^il-' "$W/nuevo-$l.tsv") | grep -c '^>' || true) distintas de $(grep -vc '^il-' "$W/nuevo-$l.tsv")"
  echo "   nuevo contra la base (todo): $(diff "$W/base-$l.tsv" "$W/nuevo-$l.tsv" | grep -c '^>' || true) distintas de $(wc -l < "$W/nuevo-$l.tsv")"
  echo "   base contra la referencia (sin il-*): $(diff <(grep -v '^il-' "$W/ref-$l.tsv") <(grep -v '^il-' "$W/base-$l.tsv") | grep -c '^>' || true) distintas"
done
