#!/bin/bash
# arnes.sh: el arnes del grupo «portadas» (Portadas.cs): compila VistasPortadas con esta copia y con la referencia congelada (copiando el
# proyecto a la carpeta de trabajo, PORTADAS), lo corre y compara las salidas linea por linea (y que la referencia no tenga lineas de
# «DIRECTO DISTINTO»: InteriorOpaco llamado directo contra el puente, solo en la traduccion; ni de «NO NATIVO DISTINTO»: la rama que no es
# BGRA premultiplicado de De, TrabajoTeselas e InteriorOpaco, con el mismo mapa sin premultiplicar, revision). Las miniaturas no tienen texto: una corrida.
set -u
S=${PORTADAS:-/root/rocola-trabajo/port/portadas}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$X/Pruebas/Comun
mkdir -p $S/vistas-ref
cp $X/Pruebas/VistasPortadas/VistasPortadas.csproj $X/Pruebas/VistasPortadas/Portadas.cs $S/vistas-ref/
(cd $X/Pruebas/VistasPortadas && dotnet build -c Release -nologo -v q -o $S/vistas-nuevo-bin 2>&1 | grep -E " error |rror\(s\)|arning\(s\)")
(cd $S/vistas-ref && dotnet build -c Release -nologo -v q -p:Fuente=/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp -o $S/vistas-ref-bin 2>&1 | grep -E " error |rror\(s\)|arning\(s\)")
for p in nuevo ref; do
  (cd $S && env ROCOLA_PANTALLA=fuera:64x64 dotnet $S/vistas-$p-bin/VistasPortadas.dll $S/arnes-$p > $S/vistas-$p.tsv 2> $S/vistas-$p.err)
  echo "$p: $(wc -l < $S/vistas-$p.tsv) lineas; $(tail -1 $S/vistas-$p.err); directo distinto: $(grep -c 'DIRECTO DISTINTO' $S/vistas-$p.err); $(grep -h 'no nativos comparados' $S/vistas-$p.err)"
done
if cmp -s $S/vistas-ref.tsv $S/vistas-nuevo.tsv; then echo "IGUALES"; else echo "DISTINTAS: $(diff $S/vistas-ref.tsv $S/vistas-nuevo.tsv | grep -c '^>') lineas"; fi
[ "${GUARDA:-0}" = 1 ] || rm -rf $S/vistas-nuevo-bin $S/vistas-ref-bin $S/arnes-nuevo $S/arnes-ref
