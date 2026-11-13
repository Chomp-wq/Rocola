#!/bin/bash
# control.sh: los controles negativos del arnes (PantallaViva.cs): copias de esta traduccion con un fallo metido a proposito (uno por copia)
# compiladas con el arnes y comparadas con la salida de la referencia (las letras de XP falsas): cada fallo tiene que salir en lineas
# distintas; si una copia sale igual, el arnes no ve ese fallo. Antes, arnes.sh (deja vistas-ref-falsas.tsv). Carpeta: PANTALLAVIVA.
set -u
S=${PANTALLAVIVA:-/root/rocola-trabajo/port/pantalla-viva}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$X/Pruebas/Comun
M=$S/mutante; P=$M/sistema/rocola
# archivo|linea|lo que se cambia|por que|lo que se rompe
CONTROLES=(
  "Codigo/PantallaVivaGpu.cs|716|270, 90, false)|270, 90, true)|RutaRedonda: el segundo arco empieza figura (GDI+ lo une al anterior)"
  "Codigo/PantallaVivaGpu.cs|431|270, 180, false)|270, 180, true)|Pildora: el segundo arco empieza figura"
  "Codigo/PantallaVivaGpu.cs|591|AjusteLetra.SuaveAjustada|AjusteLetra.Suave|PintaFicha: el modo de letra AntiAlias en vez de AntiAliasGridFit"
  "Codigo/PantallaVivaGpu.cs|611|SKClipOperation.Intersect, false)|SKClipOperation.Intersect, true)|PintaFicha: el recorte de la portadita suavizado"
  "Codigo/PantallaVivaGpu.cs|782|(int)((j + .5) * b.Height / 6)));|(int)((j + .5) * b.Height / 6)).WithAlpha(255));|ColorDePortada: el alfa de los puntos no cuenta"
  "Codigo/PantallaVivaGpu.cs|782|(int)((i + .5) * b.Width / 6)|(int)((i + .4) * b.Width / 6)|ColorDePortada: los puntos, un poco a la izquierda"
  "Codigo/DiscoVivoGpu.cs|412|PuenteDibujo.Letra(fCola), ajuste, pc,|PuenteDibujo.Letra(fCola), AjusteLetra.DelSistema, pc,|PastillaSonandoEq: el texto con el modo de letra de fabrica"
  "Codigo/DiscoVivoGpu.cs|409|filtro, medio, copia)|Filtro.Bilineal, medio, copia)|PastillaSonandoEq: la pastilla con el filtro de fabrica en vez del del lienzo"
  "Codigo/DiscoVivoGpu.cs|278|CorteTexto.PuntosPorLetra|CorteTexto.PorLetra|PintaBoleto: el titulo recortado por letra, sin puntos"
  # (los dos controles de Plataforma/Puente.pantalla-viva.cs -el puente de AnchoTexto y el de PastillaSonandoEq- se retiraron el
  # 12-11-2026: ese puente se quito entero, limpieza-puentes; el de PastillaSonandoEq ya no tenia a que agarrarse desde vistas-b)
  # los de la revision (10-11-2026): lo que los diez de arriba no tocaban (la portadita del boleto, el letrero, el medio pixel y la copia de
  # la pastilla y el orden del estado en su firma puente, una pluma y un suavizado)
  "Codigo/DiscoVivoGpu.cs|256|Filtro.BilinealAlta|Filtro.Bilineal|PintaBoleto: la portadita con el filtro de fabrica"
  "Codigo/DiscoVivoGpu.cs|271|SKClipOperation.Intersect, false)|SKClipOperation.Intersect, true)|PintaBoleto: el recorte de la portadita suavizado"
  "Codigo/PantallaVivaGpu.cs|387|AjusteLetra.SuaveAjustada|AjusteLetra.Suave|PintaFoco: el modo de letra AntiAlias en vez de AntiAliasGridFit"
  "Codigo/PantallaVivaGpu.cs|592|Filtro.BilinealAlta|Filtro.Bilineal|PintaFicha: la portadita con el filtro de fabrica"
  "Codigo/DiscoVivoGpu.cs|409|filtro, medio, copia)|filtro, !medio, copia)|PastillaSonandoEq: la pastilla con el medio pixel al reves"
  "Codigo/DiscoVivoGpu.cs|409|filtro, medio, copia)|filtro, medio, !copia)|PastillaSonandoEq: la pastilla con la copia al reves"
  "Codigo/PantallaVivaGpu.cs|402|StrokeWidth = 1f|StrokeWidth = 2f|PintaFoco: el filo de la pastilla con una pluma de 2"
  "Codigo/PantallaVivaGpu.cs|656|suave)|false)|PintaFicha: el circulito del numero sin suavizar"
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
  mkdir -p $S/control-proj; cp $X/Pruebas/VistasPantallaViva/VistasPantallaViva.csproj $X/Pruebas/VistasPantallaViva/PantallaViva.cs $S/control-proj/
  (cd $S/control-proj && dotnet build -c Release -nologo -v q -p:Fuente=$P/Xp -o $S/control-bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
  (cd $S && env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas dotnet $S/control-bin/VistasPantallaViva.dll > $S/control.tsv 2> $S/control.err)
  d=$(diff $S/vistas-ref-falsas.tsv $S/control.tsv | grep -c '^>')
  echo "$que: $d de $(wc -l < $S/control.tsv) lineas distintas ($(diff $S/vistas-ref-falsas.tsv $S/control.tsv | grep '^>' | cut -f1 | sed -E 's/^> //; s/-[0-9].*//' | sort | uniq -c | tr -s ' ' | tr '\n' ' '))"
done
rm -rf $M $S/control-bin $S/control-proj/bin $S/control-proj/obj
