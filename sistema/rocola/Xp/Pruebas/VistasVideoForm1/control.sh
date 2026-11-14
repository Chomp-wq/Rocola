#!/bin/bash
# control.sh: controles negativos del arnes: mete a proposito un fallo en la traduccion (un sed sobre una copia de Codigo/ y Plataforma/ en la
# carpeta de trabajo), compila el arnes con esa copia y cuenta las lineas distintas de la referencia (hace falta arnes.sh antes, con GUARDA=1:
# usa vistas-ref-falsas.tsv). Ninguno toca la copia de trabajo.
set -u
S=${VIDEOFORM1:-/root/rocola-trabajo/port/video-form1/arnes}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$X/Pruebas/Comun
corre() {   # nombre archivo sed
  local C=$S/control/Xp; rm -rf $S/control; mkdir -p $C
  cp -r $X/Codigo $X/Plataforma $X/Nativo $C/
  sed -i "$3" $C/$2
  if cmp -s $X/$2 $C/$2; then echo "$1: el sed no cambio nada"; return; fi
  mkdir -p $S/control/proy; cp $X/Pruebas/VistasVideoForm1/VistasVideoForm1.csproj $X/Pruebas/VistasVideoForm1/VideoForm1.cs $S/control/proy/
  ln -s $X/../Consola $S/control/Consola; ln -s $X/../Motor $S/control/Motor
  (cd $S/control/proy && dotnet build -c Release -nologo -v q -p:Fuente=$C -o $S/control/bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
  (cd $S && env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas dotnet $S/control/bin/VistasVideoForm1.dll > $S/control.tsv 2>/dev/null)
  echo "$1: $(diff $S/vistas-ref-falsas.tsv $S/control.tsv | grep -c '^>') lineas distintas de $(wc -l < $S/vistas-ref-falsas.tsv) ($(diff $S/vistas-ref-falsas.tsv $S/control.tsv | grep '^>' | cut -f1 | sed 's/^> //' | cut -d- -f1 | sort | uniq -c | sort -rn | head -4 | tr '\n' ' '))"
  rm -rf $S/control $S/control.tsv
}
corre "las etiquetas cortan por palabra" Codigo/Form1.Video.cs 's/sf.Corte = CorteTexto.PuntosPorLetra;/sf.Corte = CorteTexto.PuntosPorPalabra;/'
corre "la barra de ayuda sin suavizar" Codigo/Form1.cs 's/                bool suave = true;   \/\/ EN LINUX: g.SmoothingMode = AntiAlias, en una variable/                bool suave = false;/'
# (N7, borrar-capas) 1 control(es) que metian su fallo en un puente (Plataforma/Puente*.cs) o en una linea con PuenteDibujo o Rocola.Dibujo2D se
# retiraron: las capas se borraron y esas lineas ya no existen; lo que hacia cada puente lo hace ahora la traducida, con sus controles.
