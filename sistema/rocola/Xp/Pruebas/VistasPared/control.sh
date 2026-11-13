#!/bin/bash
# control.sh: controles negativos del arnes: mete a proposito un fallo en la traduccion (un sed sobre una copia de Codigo/ y Plataforma/ en la
# carpeta de trabajo), compila el arnes con esa copia y cuenta las lineas distintas de la referencia (hace falta arnes.sh antes: usa
# vistas-ref-falsas.tsv). Ninguno toca la copia de trabajo. control.sh [N...]: solo esos (por su numero).
set -u
S=${PARED:-/root/rocola-trabajo/port/pared}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$X/Pruebas/Comun
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
# (N7, borrar-capas) 2 control(es) que metian su fallo en un puente (Plataforma/Puente*.cs) o en una linea con PuenteDibujo o Rocola.Dibujo2D se
# retiraron: las capas se borraron y esas lineas ya no existen; lo que hacia cada puente lo hace ahora la traducida, con sus controles.
corre "la atraccion con otro modo de letra" Codigo/Form1.Pared.cs '124s/AjusteLetra ajuste = AjusteLetra.ClearTypeAjustada;/AjusteLetra ajuste = AjusteLetra.Suave;/'
corre "el cubo de la cancion sin inclinarse" Codigo/Form1.Pared.cs '1671s/g.RotateDegrees(tilt);/g.RotateDegrees(0);/'
corre "la paleta lee todo como premultiplicado" Codigo/PaletaCaratulaGpu.cs '64s/bool prem = f == FormatoMapa.PArgb32;/bool prem = f == FormatoMapa.PArgb32 || f == FormatoMapa.Argb32;/'
# (las tiras «sin suavizar», '120s/edge, suave)/edge, false)/', dan 0 lineas distintas: un rectangulo de un punto en coordenadas enteras sale
# igual con suavizado y sin el, asi que ese control no decia nada; el suavizado de PackEdge se razona por el codigo de XP, que pone AntiAlias)
corre "las tiras del contorno un punto mas anchas" Codigo/CaratulasSeparadasGpu.cs '120s/right - left, height - 1), edge, suave)/right - left + 1, height - 1), edge, suave)/'
# (el control «el puente de Cube.Draw sin el filtro del Graphics» se retiro el 12-11-2026: la firma de XP de Cube.Draw con un
# Graphics se quito de Plataforma/Puente.pared.cs (limpieza-puentes) y el arnes llama directo a la traducida)
corre "la mezcla de portadas con la mitad del fundido" Codigo/Form1.Pared.cs 's/float\[\]\[\] cm = MatrizColor.Identidad(); cm\[3\]\[3\] = f;/float[][] cm = MatrizColor.Identidad(); cm[3][3] = f * 0.5f;/'
