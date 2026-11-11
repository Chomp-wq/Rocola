#!/bin/bash
# control.sh: controles negativos del arnes: mete a proposito un fallo en la traduccion (un sed sobre una copia de Codigo/ y Plataforma/ en la
# carpeta de trabajo), compila el arnes con esa copia y cuenta las lineas distintas de la referencia (hace falta arnes.sh antes, con GUARDA=1:
# usa vistas-ref-falsas.tsv). Cada uno corre solo su barra (paginas o letras). Ninguno toca la copia de trabajo. SOLO="parte del nombre": solo esos.
set -u
S=${VISTASA:-/root/rocola-trabajo/port/vistas-a}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
corre() {   # nombre barra archivo sed
  [ -n "${SOLO:-}" ] && [[ "$1" != *"$SOLO"* ]] && return
  local C=$S/control/Xp; rm -rf $S/control; mkdir -p $C
  cp -r $X/Codigo $X/Plataforma $X/Nativo $C/
  sed -i "$4" $C/$3
  if cmp -s $X/$3 $C/$3; then echo "$1: el sed no cambio nada"; return; fi
  mkdir -p $S/control/proy; cp $X/Pruebas/VistasVistasA/VistasVistasA.csproj $X/Pruebas/VistasVistasA/VistasA.cs $S/control/proy/
  ln -s $X/../Consola $S/control/Consola; ln -s $X/../Motor $S/control/Motor
  (cd $S/control/proy && dotnet build -c Release -nologo -v q -p:Fuente=$C -o $S/control/bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
  (cd $S && env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas dotnet $S/control/bin/VistasVistasA.dll solo $2 2>/dev/null | grep "^$2" > $S/control.tsv)
  grep "^$2" $S/vistas-ref-falsas.tsv > $S/control-ref.tsv
  echo "$1: $(diff $S/control-ref.tsv $S/control.tsv | grep -c '^>') lineas distintas de $(wc -l < $S/control-ref.tsv) (huella de la salida: $(md5sum < $S/control.tsv | cut -c1-8))"
  rm -rf $S/control $S/control.tsv $S/control-ref.tsv
}
corre "el segundo arco del redondo empieza figura" letras Codigo/Form1.Vistas.cs '754s/270, 90, false)/270, 90, true)/'
corre "el riel sin suavizar" letras Codigo/Form1.Vistas.cs '421s/lb, suave, medio, copia)/lb, false, medio, copia)/'
corre "la tira con otro modo de letra" letras Codigo/Form1.Vistas.cs '506s/AjusteLetra.DelSistema/AjusteLetra.Suave/'
corre "la estrella sin la copia" letras Codigo/Form1.Vistas.cs '467s/suave, true, medio, copia)/suave, true, medio, false)/'
corre "la tira con el filtro fijo" letras Codigo/Form1.Vistas.cs '653s/filtro, medio, copia)/Filtro.Bilineal, medio, copia)/'
corre "la pluma de la capsula sin union redonda" letras Codigo/Form1.Vistas.cs '686s/SKStrokeJoin.Round/SKStrokeJoin.Miter/'
# (la copia de la letra grabada bajo la capsula que viaja, lineas 484-488, 675 y 851, no se ve sola: con la copia puesta, el relleno de la
# capsula, lineas 694 y 870, reemplaza lo que hay debajo, la letra grabada incluida; el segundo control lo prueba: quitada la copia del
# relleno, la de la sombra de la letra si cambia los puntos: las dos salidas tienen huellas distintas)
corre "el relleno de la capsula sin la copia" letras Codigo/Form1.Vistas.cs '694s/pulseBrush, suave, medio, copia)/pulseBrush, suave, medio, false)/;870s/pulseBrush, suave, medio, copia)/pulseBrush, suave, medio, false)/'
corre "el relleno de la capsula y la sombra de la letra grabada sin la copia" letras Codigo/Form1.Vistas.cs '694s/pulseBrush, suave, medio, copia)/pulseBrush, suave, medio, false)/;870s/pulseBrush, suave, medio, copia)/pulseBrush, suave, medio, false)/;484s/sfCen, copia, medio)/sfCen, false, medio)/'
corre "los cuerpos medidos con un modo fijo" letras Codigo/Form1.Vistas.cs '337s/f, ajuste, new SizeF/f, AjusteLetra.SinSuavizar, new SizeF/'
corre "la lupa agranda solo a lo ancho" letras Codigo/Form1.Vistas.cs '815s/g.Scale(s, s)/g.Scale(s, 1f)/'
corre "el puente de letras no deja el suavizado" letras Plataforma/Puente.vistas-a.cs 's/Letters.Length == 0)) g.SmoothingMode = SmoothingMode.AntiAlias;/Letters.Length == 0)) { }/'
corre "el fondo de las paginas siempre suavizado" paginas Codigo/Form1.Vistas.cs '128s/fondo, suave, medio, copia)/fondo, true, medio, copia)/'
corre "el letrero de las paginas con el tipografico" paginas Codigo/Form1.Vistas.cs '156s/0, 0), null, copia, medio)/0, 0), FormatoTexto.TipograficoCompartido, copia, medio)/'
corre "las paginas con otro modo de letra" paginas Codigo/Form1.Vistas.cs '131s/AjusteLetra.ClearTypeAjustada/AjusteLetra.Suave/'
corre "el puente de paginas no deja el modo de letra" paginas Plataforma/Puente.vistas-a.cs 's/ g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; }/ }/'
