#!/bin/bash
# control.sh: los controles negativos del arnes (Buscador.cs): copias de esta traduccion con un fallo metido a proposito (uno por
# copia), compiladas con el arnes y comparadas con la salida de la referencia congelada (con las letras de XP falsas y RAPIDO=1: cuatro
# estados, un color y sin el cielo). Cada fallo tiene que salir en lineas distintas; si una copia sale igual, el arnes no ve ese fallo.
# Primero compila y corre la referencia y esta copia sin tocar (tienen que salir iguales). Carpeta: BUSCADOR. (El guion es el del grupo
# «buscador-portadas», con los fallos de este grupo.)
set -u
S=${BUSCADOR:-/root/rocola-trabajo/port/buscador}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$X/Pruebas/Comun
M=$S/mutante; P=$M/sistema/rocola
L="ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas"
corre() { (cd $S && env ROCOLA_PANTALLA=fuera:64x64 RAPIDO=1 $L dotnet $1/VistasBuscador.dll > $2 2> $2.err); }
mkdir -p $S/control-proj $S/vistas-ref
cp $X/Pruebas/VistasBuscador/VistasBuscador.csproj $X/Pruebas/VistasBuscador/Buscador.cs $S/control-proj/
cp $X/Pruebas/VistasBuscador/VistasBuscador.csproj $X/Pruebas/VistasBuscador/Buscador.cs $S/vistas-ref/
(cd $S/vistas-ref && dotnet build -c Release -nologo -v q -p:Fuente=/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp -o $S/control-ref-bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
(cd $S/control-proj && dotnet build -c Release -nologo -v q -p:Fuente=$X -o $S/control-bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
corre $S/control-ref-bin $S/rapido-ref.tsv; corre $S/control-bin $S/rapido-nuevo.tsv
echo "referencia: $(wc -l < $S/rapido-ref.tsv) lineas; esta copia sin tocar: $(diff $S/rapido-ref.tsv $S/rapido-nuevo.tsv | grep -c '^>') distintas"
# archivo|linea|lo que se cambia|por que|lo que se rompe
CONTROLES=(
  "Codigo/Form1.Buscador.cs|216|AjusteLetra.Suave;|AjusteLetra.SuaveAjustada;|AnchoJusto mide con AntiAliasGridFit (XP: AntiAlias)"
  "Codigo/Form1.Buscador.cs|252|sb, suave, medio, copia)|sb, suave, !medio, copia)|la casilla de BUSCAR con el medio pixel al reves"
  "Codigo/Form1.Buscador.cs|1131|filtro, medio, copia);|filtro, medio, false);|el teclado guardado se pega sin la copia del lienzo"
  "Codigo/Form1.Buscador.cs|1151|Degradado.Vertical, true)|Degradado.Horizontal, true)|el degradado de las teclas horizontal"
  # (Vertical no escalable da lo mismo que escalable: Degradado.Extremos con 90 grados; por eso el control es otro angulo)
  "Codigo/Form1.Buscador.cs|1244|suaveC, false, copiaC);|suaveC, false, false);|el hueco de una tarjeta que cambia se borra sin la copia (se mezcla)"
  "Codigo/Form1.Buscador.cs|1296|filtro, medio, copia);|filtro, !medio, copia);|el fondo horneado de la tarjeta con el medio pixel al reves"
  "Codigo/Form1.Buscador.cs|1389|ajuste == AjusteLetra.SinSuavizar |ajuste == AjusteLetra.Suave |el nombre del resultado suavizado aunque el Graphics sea de un bit"
  "Codigo/Form1.Buscador.cs|1445|cm[3][3] = f;|cm[3][3] = 1f;|la mezcla de dos portadas sin la matriz de alfa"
  "Codigo/Form1.Buscador.cs|1266|BanderasGdi.Izquierda, false);|BanderasGdi.Centrado, false);|la cabecera Resultados centrada"
  "Codigo/Form1.Buscador.cs|1127|AjusteLetra.DelSistema, false, false, SKMatrix.Identity|AjusteLetra.DelSistema, false, false, SKMatrix.CreateTranslation(2, 1)|las teclas guardadas con otra matriz del HDC"
  "Codigo/Form1.Buscador.cs|1552|AjusteLetra.ClearTypeAjustada;|AjusteLetra.Suave;|las ayudas de la vista previa sin ClearTypeGridFit"
  "Plataforma/Puente.buscador.cs|52|g.InterpolationMode = (InterpolationMode)(int)filtro;|;|el puente de RenderContent no le deja al Graphics el filtro del cubo"
  "Plataforma/Puente.buscador.cs|65|if (pone) g.SmoothingMode|if (false) g.SmoothingMode|el puente de DibujaPiezaViva no le deja el suavizado"
  "Plataforma/Puente.buscador.cs|37|if (Width >= 30 && Height >= 12) g.SmoothingMode|if (true) g.SmoothingMode|el puente de la casilla le deja el suavizado aunque XP salga antes"
)
for c in "${CONTROLES[@]}"; do
  IFS='|' read -r f l viejo nuevo que <<< "$c"
  [ -n "${SOLO:-}" ] && [[ ! "$que" =~ $SOLO ]] && continue   # (SOLO=expresion: solo los controles cuyo nombre case con ella)
  rm -rf $M; mkdir -p $P/Xp $P/Consola/Pantalla $P/Motor
  cp -r $X/Codigo $X/Plataforma $X/Nativo $P/Xp/; cp $X/../Consola/Pantalla/Gl.cs $X/../Consola/Pantalla/Kms.cs $P/Consola/Pantalla/; cp $X/../Motor/Botones.cs $P/Motor/
  python3 - "$P/Xp/$f" "$l" "$viejo" "$nuevo" <<'EOF' || { echo "NO SE PUDO METER: $que"; continue; }
import sys
ruta, l, viejo, nuevo = sys.argv[1], int(sys.argv[2]), sys.argv[3], sys.argv[4]
t = open(ruta, encoding='utf-8').read().split('\n')
idx = [i for i in range(len(t)) if viejo in t[i]] if l == 0 else [l - 1]
if len(idx) != 1 or viejo not in t[idx[0]]: sys.exit(1)
t[idx[0]] = t[idx[0]].replace(viejo, nuevo, 1)
open(ruta, 'w', encoding='utf-8').write('\n'.join(t))
EOF
  (cd $S/control-proj && dotnet build -c Release -nologo -v q -p:Fuente=$P/Xp -o $S/control-bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
  corre $S/control-bin $S/control.tsv
  d=$(diff $S/rapido-ref.tsv $S/control.tsv | grep -c '^>')
  echo "$que: $d de $(wc -l < $S/control.tsv) lineas distintas ($(diff $S/rapido-ref.tsv $S/control.tsv | grep '^>' | cut -f1 | sed -E 's/^> //; s/-(onpaint|sin-suavizar|copia-medio-traslada|unbit-copia-recorte)-.*$/-\1/; s/-p[0-3]$//' | sort | uniq -c | sort -rn | head -6 | tr -s ' ' | tr '\n' ' '))"
done
rm -rf $M $S/control-bin $S/control-ref-bin $S/control-proj/bin $S/control-proj/obj
