#!/bin/bash
# control.sh: controles negativos del arnes: mete a proposito un fallo en la traduccion (un sed sobre una copia de Codigo/ y Plataforma/ en la
# carpeta de trabajo), compila el arnes con esa copia y cuenta las lineas distintas de la referencia (hace falta arnes.sh antes, con GUARDA=1:
# usa vistas-ref-falsas.tsv). Ninguno toca la copia de trabajo.
set -u
S=${ASISTENTE:-/root/rocola-trabajo/port/asistente}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
corre() {   # nombre archivo sed
  local C=$S/control/Xp; rm -rf $S/control; mkdir -p $C
  cp -r $X/Codigo $X/Plataforma $X/Nativo $C/
  sed -i "$3" $C/$2
  if cmp -s $X/$2 $C/$2; then echo "$1: el sed no cambio nada"; return; fi
  mkdir -p $S/control/proy; cp $X/Pruebas/VistasAsistente/VistasAsistente.csproj $X/Pruebas/VistasAsistente/Asistente.cs $S/control/proy/
  ln -s $X/../Consola $S/control/Consola; ln -s $X/../Motor $S/control/Motor
  (cd $S/control/proy && dotnet build -c Release -nologo -v q -p:Fuente=$C -o $S/control/bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
  (cd $S && env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas dotnet $S/control/bin/VistasAsistente.dll > $S/control.tsv 2>/dev/null)
  echo "$1: $(diff $S/vistas-ref-falsas.tsv $S/control.tsv | grep -c '^>') lineas distintas de $(wc -l < $S/vistas-ref-falsas.tsv) ($(diff $S/vistas-ref-falsas.tsv $S/control.tsv | grep '^>' | cut -f1 | sed 's/^> //; s/-[0-9x]*$//' | cut -d- -f1-2 | sort | uniq -c | sort -rn | head -4 | tr '\n' ' '))"
  rm -rf $S/control $S/control.tsv
}
corre "el arco del globo empieza figura" Codigo/AsistenteRocola.cs 's/derecha - r \* 2 + r \* 2, y + r \* 2), 270, 90, false)/derecha - r * 2 + r * 2, y + r * 2), 270, 90, true)/'
corre "el degradado con medio pixel" Codigo/AsistenteRocola.cs '430s/Color.FromArgb(248, 10, 18, 37), medio)/Color.FromArgb(248, 10, 18, 37), true)/'
corre "el puente no deja el estado" Plataforma/Puente.asistente.cs 's/{ g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAlias; }/{ }/'
corre "la flecha sin suavizar" Codigo/AsistenteGuiaRocola.cs '334s/sb, suave, medio, copia)/sb, false, medio, copia)/'
corre "la letra de las teclas en puntos" Codigo/AsistenteGuiaRocola.cs '305s/Letra.EnPixeles(f.Familia/Letra.EnPuntos(f.Familia/'
corre "la casilla sin la copia" Codigo/AyudaCasillaGpu.cs '80s/sb, suave, medio, copia)/sb, suave, medio, false)/'
corre "la guia con el tipografico" Codigo/AsistenteGuiaRocola.cs '295s/new FormatoTexto { Horizontal = Alinea.Lejos }/FormatoTexto.TipograficoCompartido/'
corre "el guardado sin reusar" Codigo/PulidoGpu.cs '152s/continue;/continue; if (d != null) continue;/'
# (revision) dos mas: el borde de la casilla y el modo de letra que el puente le pasa a CtrlTxt
corre "(revision) la casilla sin suavizar el borde" Codigo/AyudaCasillaGpu.cs '81s/pn, suave, medio, copia)/pn, false, medio, copia)/'
corre "(revision) CtrlTxt del puente sin el modo de letra del que llama" Plataforma/Puente.asistente.cs 's/return CtrlTxt(PuenteDibujo.GraphicsAsistente(g, suave, ajuste, copia, medio), ff, s, x, cy, c);/return CtrlTxt(PuenteDibujo.GraphicsAsistente(g, suave, Rocola.Nativo.AjusteLetra.DelSistema, copia, medio), ff, s, x, cy, c);/'
