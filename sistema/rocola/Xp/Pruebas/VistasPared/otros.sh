#!/bin/bash
# otros.sh: los arneses de los grupos ya juntados cuyas costuras con la pared se quitaron (asistente, buscador-portadas y acabado-estilo:
# sus globos, su guia, su casilla, sus ayudas y sus flechas llaman ya directo a CtrlDot, CtrlTxt, CtrlTri, CtrlPad, CtrlDirLeft y
# CtrlDirRight), compilados con ESTA copia y comparados con la salida de la referencia congelada que cada grupo dejo en su carpeta de
# trabajo (no se rehace: la referencia no cambia). Tienen que seguir iguales linea por linea.
set -u
S=${PARED:-/root/rocola-trabajo/port/pared}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$X/Pruebas/Comun
uno() {   # carpeta del arnes, dll, salida de la referencia (con %L = falsas o dejavu; «fresca»: se compila y se corre aqui), filtro de lineas
  [ -n "${SOLO:-}" ] && [ "$SOLO" != "$1" ] && return
  rm -rf $S/otro-bin
  if [ "$3" = fresca ]; then   # la salida guardada es de antes de que el arnes creciera: la referencia, otra vez
    rm -rf $S/otro-ref $S/otro-ref-bin; mkdir -p $S/otro-ref; cp $X/Pruebas/$1/*.csproj $X/Pruebas/$1/*.cs $S/otro-ref/
    (cd $S/otro-ref && dotnet build -c Release -nologo -v q -p:Fuente=/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp -o $S/otro-ref-bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
    for letras in falsas dejavu; do
      if [ $letras = falsas ]; then L="ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas"; else L="SIN_LETRAS=1"; fi
      (cd $S && env -u ROCOLA_LETRAS_XP ROCOLA_PANTALLA=fuera:64x64 $L dotnet $S/otro-ref-bin/$2.dll > $S/otro-ref-$1-$letras.tsv 2>/dev/null)
    done
    rm -rf $S/otro-ref $S/otro-ref-bin; set -- "$1" "$2" "$S/otro-ref-$1-%L.tsv" "$4"
  fi
  (cd $X/Pruebas/$1 && dotnet build -c Release -nologo -v q -o $S/otro-bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
  for letras in falsas dejavu; do
    if [ $letras = falsas ]; then L="ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas"; else L="SIN_LETRAS=1"; fi
    (cd $S && env -u ROCOLA_LETRAS_XP ROCOLA_PANTALLA=fuera:64x64 $L dotnet $S/otro-bin/$2.dll > $S/otro-$1-$letras.tsv 2> $S/otro-$1-$letras.err)
    ref=${3//%L/$letras}
    echo "$1 ($letras): $(wc -l < $S/otro-$1-$letras.tsv) lineas; distintas de la referencia: $(diff <(grep -v "$4" $ref) <(grep -v "$4" $S/otro-$1-$letras.tsv) | grep -c '^>'); $(tail -1 $S/otro-$1-$letras.err)"
  done
  rm -rf $S/otro-bin $X/Pruebas/$1/obj $X/Pruebas/$1/bin
}
uno VistasAsistente VistasAsistente fresca '^$'
uno VistasBuscadorPortadas VistasBuscadorPortadas /root/rocola-trabajo/port/buscador-portadas/vistas-ref-%L.tsv '^$'
uno VistasAcabadoEstilo VistasAcabadoEstilo /root/rocola-trabajo/port/acabado-estilo/vistas/ref-%L.tsv '^il-'
