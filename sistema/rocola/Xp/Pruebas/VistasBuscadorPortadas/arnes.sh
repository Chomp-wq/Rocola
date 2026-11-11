#!/bin/bash
# arnes.sh: el arnes del grupo «buscador-portadas» (BuscadorPortadas.cs): compila VistasBuscadorPortadas con esta copia y con la referencia
# congelada (copiando el proyecto a la carpeta de trabajo, BUSCADOR), lo corre con las letras de XP falsas y con DejaVu, y compara las salidas
# linea por linea. BASE=commit: compila tambien esa version de esta rama (git archive a la carpeta de trabajo) y la compara igual.
set -u
S=${BUSCADOR:-/root/rocola-trabajo/port/buscador-portadas}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
mkdir -p $S/vistas-ref
cp $X/Pruebas/VistasBuscadorPortadas/VistasBuscadorPortadas.csproj $X/Pruebas/VistasBuscadorPortadas/BuscadorPortadas.cs $S/vistas-ref/
(cd $X/Pruebas/VistasBuscadorPortadas && dotnet build -c Release -nologo -v q -o $S/vistas-nuevo-bin 2>&1 | grep -E " error |rror\(s\)|arning\(s\)")
(cd $S/vistas-ref && dotnet build -c Release -nologo -v q -p:Fuente=/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp -o $S/vistas-ref-bin 2>&1 | grep -E " error |rror\(s\)|arning\(s\)")
PROGS="nuevo ref"
if [ -n "${BASE:-}" ]; then
  rm -rf $S/base-src && mkdir -p $S/base-src $S/vistas-base
  (cd "$(git -C $X rev-parse --show-toplevel)" && git archive "$BASE" sistema/rocola/Xp/Codigo sistema/rocola/Xp/Plataforma sistema/rocola/Xp/Nativo sistema/rocola/Consola/Pantalla/Gl.cs sistema/rocola/Consola/Pantalla/Kms.cs sistema/rocola/Motor/Botones.cs | tar -x -C $S/base-src)
  cp $X/Pruebas/VistasBuscadorPortadas/VistasBuscadorPortadas.csproj $X/Pruebas/VistasBuscadorPortadas/BuscadorPortadas.cs $S/vistas-base/
  (cd $S/vistas-base && dotnet build -c Release -nologo -v q -p:Fuente=$S/base-src/sistema/rocola/Xp -o $S/vistas-base-bin 2>&1 | grep -E " error |rror\(s\)|arning\(s\)")
  PROGS="nuevo ref base"
fi
for letras in falsas dejavu; do
  if [ $letras = falsas ]; then L="ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas"; else L="SIN_LETRAS=1"; fi
  for p in $PROGS; do
    (cd $S && env ROCOLA_PANTALLA=fuera:64x64 $L dotnet $S/vistas-$p-bin/VistasBuscadorPortadas.dll > $S/vistas-$p-$letras.tsv 2> $S/vistas-$p-$letras.err)
    echo "$p $letras: $(wc -l < $S/vistas-$p-$letras.tsv) lineas; $(tail -1 $S/vistas-$p-$letras.err)"
  done
  for p in ref base; do
    [ -f $S/vistas-$p-$letras.tsv ] || continue
    if cmp -s $S/vistas-$p-$letras.tsv $S/vistas-nuevo-$letras.tsv; then echo "IGUALES a $p ($letras)"; else echo "DISTINTAS de $p ($letras): $(diff $S/vistas-$p-$letras.tsv $S/vistas-nuevo-$letras.tsv | grep -c '^>') lineas"; fi
  done
done
# los programas compilados ocupan unos 150 MB cada uno: fuera (GUARDA=1 los deja)
[ "${GUARDA:-0}" = 1 ] || rm -rf $S/vistas-nuevo-bin $S/vistas-ref-bin $S/vistas-base-bin $S/base-src
