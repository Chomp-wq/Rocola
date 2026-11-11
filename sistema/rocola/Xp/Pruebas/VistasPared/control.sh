#!/bin/bash
# control.sh: controles negativos del arnes: mete a proposito un fallo en la traduccion (un sed sobre una copia de Codigo/ y Plataforma/ en la
# carpeta de trabajo), compila el arnes con esa copia y cuenta las lineas distintas de la referencia (hace falta arnes.sh antes: usa
# vistas-ref-falsas.tsv). Ninguno toca la copia de trabajo. control.sh [N...]: solo esos (por su numero).
set -u
S=${PARED:-/root/rocola-trabajo/port/pared}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
SOLO=" $* "; N=0
corre() {   # nombre archivo sed
  N=$((N + 1)); [ "$SOLO" != "  " ] && [[ "$SOLO" != *" $N "* ]] && return
  local C=$S/control/Xp; rm -rf $S/control; mkdir -p $C
  cp -r $X/Codigo $X/Plataforma $X/Nativo $C/
  sed -i "$3" $C/$2
  if cmp -s $X/$2 $C/$2; then echo "$N $1: el sed no cambio nada"; return; fi
  mkdir -p $S/control/proy; cp $X/Pruebas/VistasPared/VistasPared.csproj $X/Pruebas/VistasPared/Pared.cs $S/control/proy/
  ln -s $X/../Consola $S/control/Consola; ln -s $X/../Motor $S/control/Motor
  (cd $S/control/proy && dotnet build -c Release -nologo -v q -p:Fuente=$C -o $S/control/bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
  (cd $S && env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas dotnet $S/control/bin/VistasPared.dll > $S/control.tsv 2>/dev/null)
  echo "$N $1: $(diff $S/vistas-ref-falsas.tsv $S/control.tsv | grep -c '^>') lineas distintas de $(wc -l < $S/vistas-ref-falsas.tsv) ($(diff $S/vistas-ref-falsas.tsv $S/control.tsv | grep '^>' | cut -f1 | sed 's/^> //' | cut -d- -f1 | sort | uniq -c | sort -rn | head -5 | tr '\n' ' '))"
  rm -rf $S/control $S/control.tsv
}
corre "el cubo con el filtro fijo" Codigo/Form1.Pared.cs '174s/rect.Bottom), filtro, medio, copia)/rect.Bottom), Filtro.Bilineal, medio, copia)/'
corre "el contorno del cubo sin suavizar" Codigo/Form1.Pared.cs '183s/ep, suave, medio, copia)/ep, false, medio, copia)/'
corre "el halo con el centro corrido" Codigo/Form1.Pared.cs '236s/new PointF(cx, cy)/new PointF(cx + 1, cy)/'
corre "CtrlTxt sin el modo de letra del que llama" Codigo/Form1.Pared.cs '261,262s/s, f, ajuste,/s, f, AjusteLetra.DelSistema,/'
corre "la etiqueta con otras banderas (|= en vez de =)" Codigo/Form1.Pared.cs '1005s/lblSF.Banderas = BanderasTexto.SoloEnteros;/lblSF.Banderas = BanderasTexto.SoloEnteros | BanderasTexto.SinPartir;/'
corre "la casilla chica sin encoger" Codigo/Form1.Pared.cs '1249s/if (encoge) g.Scale(esc, esc);/if (encoge) g.Scale(1f, 1f);/'
corre "el palomito con las puntas rectas" Codigo/Form1.Pared.cs '1274s/p.StrokeCap = SKStrokeCap.Round; //'
corre "la etiqueta de la tarjeta copiada un punto corrida" Codigo/Form1.Pared.cs '483s/(long)src.X \* 4)/(long)(src.X + 1) * 4)/'
corre "OnPaint no le deja el filtro al Graphics" Codigo/Form1.Pared.cs '1552s/ gx.InterpolationMode = (Rocola.Dibujo2D.InterpolationMode)(int)filtro;//'
corre "la atraccion con otro modo de letra" Codigo/Form1.Pared.cs '124s/AjusteLetra ajuste = AjusteLetra.ClearTypeAjustada;/AjusteLetra ajuste = AjusteLetra.Suave;/'
corre "el cubo de la cancion sin inclinarse" Codigo/Form1.Pared.cs '1671s/g.RotateDegrees(tilt);/g.RotateDegrees(0);/'
corre "la paleta lee todo como premultiplicado" Codigo/PaletaCaratulaGpu.cs '64s/bool prem = f == FormatoMapa.PArgb32;/bool prem = f == FormatoMapa.PArgb32 || f == FormatoMapa.Argb32;/'
corre "las tiras del contorno sin suavizar" Codigo/CaratulasSeparadasGpu.cs '120s/edge, suave)/edge, false)/'
corre "el puente de Cube.Draw sin el filtro del Graphics" Plataforma/Puente.pared.cs 's/Draw(PuenteDibujo.Lienzo(g), e.Suave, e.Copia, e.Filtro, e.Medio, bounds/Draw(PuenteDibujo.Lienzo(g), e.Suave, e.Copia, Filtro.Bilineal, e.Medio, bounds/'
corre "el puente de la paleta sin el formato" Plataforma/Puente.pared.cs 's/PuenteDibujo.FormatoMapaPared(bitmap.PixelFormat))/FormatoMapa.PArgb32)/'
