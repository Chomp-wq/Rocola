#!/bin/bash
# arnes.sh: el arnes del grupo «reposo» (Reposo.cs): compila VistasReposo con esta copia y con la referencia congelada (copiando el proyecto a
# la carpeta de trabajo, REPOSO), lo corre con las letras de XP falsas y con DejaVu, y compara las salidas linea por linea
set -u
S=${REPOSO:-/root/rocola-trabajo/port/reposo}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
mkdir -p $S/vistas-ref
cp $X/Pruebas/VistasReposo/VistasReposo.csproj $X/Pruebas/VistasReposo/Reposo.cs $S/vistas-ref/
(cd $X/Pruebas/VistasReposo && dotnet build -c Release -nologo -v q -o $S/vistas-nuevo-bin 2>&1 | grep -E " error |rror\(s\)|arning\(s\)")
(cd $S/vistas-ref && dotnet build -c Release -nologo -v q -p:Fuente=/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp -o $S/vistas-ref-bin 2>&1 | grep -E " error |rror\(s\)|arning\(s\)")
for letras in falsas dejavu; do
  if [ $letras = falsas ]; then L="ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas"; else L="SIN_LETRAS=1"; fi
  for p in nuevo ref; do
    (cd $S && env ROCOLA_PANTALLA=fuera:64x64 $L dotnet $S/vistas-$p-bin/VistasReposo.dll > $S/vistas-$p-$letras.tsv 2> $S/vistas-$p-$letras.err)
    echo "$p $letras: $(wc -l < $S/vistas-$p-$letras.tsv) lineas; $(tail -1 $S/vistas-$p-$letras.err)"
  done
  if cmp -s $S/vistas-ref-$letras.tsv $S/vistas-nuevo-$letras.tsv; then echo "IGUALES ($letras)"; else echo "DISTINTAS ($letras): $(diff $S/vistas-ref-$letras.tsv $S/vistas-nuevo-$letras.tsv | grep -c '^>') lineas"; fi
done
# los dos programas compilados ocupan unos 150 MB: fuera (GUARDA=1 los deja)
[ "${GUARDA:-0}" = 1 ] || rm -rf $S/vistas-nuevo-bin $S/vistas-ref-bin
