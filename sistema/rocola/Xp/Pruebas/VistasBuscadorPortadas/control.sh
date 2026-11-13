#!/bin/bash
# control.sh: los controles negativos del arnes (BuscadorPortadas.cs): copias de esta traduccion con un fallo metido a proposito (uno por
# copia), compiladas con el arnes y comparadas con la salida de la referencia congelada (con las letras de XP falsas y RAPIDO=1: cuatro
# estados, un color y dos pantallas mas). Cada fallo tiene que salir en lineas distintas; si una copia sale igual, el arnes no ve ese fallo.
# Primero compila y corre la referencia y esta copia sin tocar (tienen que salir iguales). Carpeta: BUSCADOR. (El guion es el del grupo
# «pantalla-viva», con sus propios fallos.)
set -u
S=${BUSCADOR:-/root/rocola-trabajo/port/buscador-portadas}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
# (la clase Pru de los arneses: Pruebas/Comun, la de la capa para la referencia y la nativa para el programa nuevo; ver los .csproj)
export ROCOLA_COMUN=$X/Pruebas/Comun
M=$S/mutante; P=$M/sistema/rocola
L="ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas"
corre() { (cd $S && env ROCOLA_PANTALLA=fuera:64x64 RAPIDO=1 $L dotnet $1/VistasBuscadorPortadas.dll > $2 2> $2.err); }
mkdir -p $S/control-proj $S/vistas-ref
cp $X/Pruebas/VistasBuscadorPortadas/VistasBuscadorPortadas.csproj $X/Pruebas/VistasBuscadorPortadas/BuscadorPortadas.cs $S/control-proj/
cp $X/Pruebas/VistasBuscadorPortadas/VistasBuscadorPortadas.csproj $X/Pruebas/VistasBuscadorPortadas/BuscadorPortadas.cs $S/vistas-ref/
(cd $S/vistas-ref && dotnet build -c Release -nologo -v q -p:Fuente=/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp -o $S/control-ref-bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
(cd $S/control-proj && dotnet build -c Release -nologo -v q -p:Fuente=$X -o $S/control-bin 2>&1 | grep -E " error |rror\(s\)" | grep -v " 0 Error")
corre $S/control-ref-bin $S/rapido-ref.tsv; corre $S/control-bin $S/rapido-nuevo.tsv
echo "referencia: $(wc -l < $S/rapido-ref.tsv) lineas; esta copia sin tocar: $(diff $S/rapido-ref.tsv $S/rapido-nuevo.tsv | grep -c '^>') distintas"
# archivo|linea|lo que se cambia|por que|lo que se rompe
CONTROLES=(
  "Codigo/BuscadorPortadasGpu.cs|1371|Envoltura.Transparente|Envoltura.Repite|la portada de la tarjeta: la textura repetida (Clamp de GDI+ es transparente fuera)"
  "Codigo/BuscadorPortadasGpu.cs|1371|SKMatrix.CreateTranslation(cov.X, cov.Y), medio)|SKMatrix.CreateTranslation(cov.X, cov.Y), !medio)|la portada de la tarjeta: la textura sin su medio texel"
  "Codigo/BuscadorPortadasGpu.cs|1020|copiaZona = false;|copiaZona = true;|los trozos de las tarjetas se hornean con la copia puesta"
  "Codigo/BuscadorPortadasGpu.cs|1017|SKClipOperation.Intersect, false)|SKClipOperation.Intersect, true)|el recorte de cada trozo de las tarjetas, suavizado"
  "Codigo/BuscadorPortadasGpu.cs|1383|SKClipOperation.Intersect, false)|SKClipOperation.Intersect, true)|el recorte del brillo de las iniciales, suavizado"
  "Codigo/BuscadorPortadasGpu.cs|1455|Filtro.BilinealAlta;|Filtro.Bilineal;|la miniatura sin el prefiltro de HighQualityBilinear"
  "Codigo/BuscadorPortadasGpu.cs|504|return medidor.Value;|return AjusteLetra.SuaveAjustada;|el medidor de los atajos con AntiAliasGridFit"
  "Codigo/BuscadorPortadasGpu.cs|512|new FormatoTexto(FormatoTexto.TipograficoCompartido);|new FormatoTexto(FormatoTexto.NormalCompartido);|los formatos de medir y de los renglones copiados del normal (con margen), no del tipografico"
  "Codigo/BuscadorPortadasGpu.cs|615|bool suaveQ = suave,|bool suaveQ = true,|lo quieto se hornea siempre suavizado (XP toma el suavizado del que llama)"
  "Codigo/BuscadorPortadasGpu.cs|624|filtro, medio, copia);|filtro, medio, false);|lo quieto se pega sin la copia del lienzo"
  "Codigo/BuscadorPortadasGpu.cs|930|SKStrokeCap.Round;|SKStrokeCap.Butt;|la lupa sin las puntas redondas"
  "Codigo/BuscadorPortadasGpu.cs|1296|float h = f.AltoRenglon;|float h = f.AltoEntero;|la caja de la cinta con el alto del renglon redondeado"
  # (Puente.buscador-portadas.cs se quito al juntar los grupos buscador y video-form1: los tres que iban en el, donde estan ahora; linea 0 =
  # la unica linea que lo lleva)
# (N7, borrar-capas) 2 control(es) que metian su fallo en un puente (Plataforma/Puente*.cs) o en una linea con PuenteDibujo o Rocola.Dibujo2D se
# retiraron: las capas se borraron y esas lineas ya no existen; lo que hacia cada puente lo hace ahora la traducida, con sus controles.
  "Codigo/BuscadorPortadasGpu.cs|1028|HorneaCartas(g, suave, ajuste, copia, filtro,|HorneaCartas(g, true, ajuste, copia, filtro,|las tarjetas enteras, siempre suavizadas"
  "Codigo/BuscadorPortadasGpu.cs|776|CtrlTxt(g, suave, ajuste, copia, medio, fnAyuda|CtrlTxt(g, suave, AjusteLetra.DelSistema, copia, medio, fnAyuda|la barra de ayudas con el modo de letra de fabrica"
  "Codigo/BuscadorPortadasGpu.cs|707|DrawToast(g, suave, ajuste,|DrawToast(g, suave, AjusteLetra.DelSistema,|el aviso con el modo de letra de fabrica"
  # los de la revision (SOLO=revision los pasa solos)
  "Codigo/BuscadorPortadasGpu.cs|1386|pincelS, suave, medio, copia)|pincelS, suave, !medio, copia)|revision: el brillo de las iniciales con el medio pixel al reves"
  "Codigo/BuscadorPortadasGpu.cs|512|sfMide = new FormatoTexto(FormatoTexto.TipograficoCompartido);|sfMide = FormatoTexto.TipograficoCompartido;|revision: el formato de medir es el tipografico compartido mismo y no una copia"
  "Codigo/BuscadorPortadasGpu.cs|1023|finally { g.RestoreToCount(state); }|finally { }|revision: el recorte de un trozo de las tarjetas no se quita"
  "Codigo/BuscadorPortadasGpu.cs|707|medio, hdc, Width|medio, SKMatrix.CreateTranslation(7, 3), Width|revision: el aviso con otra matriz del HDC"
  "Codigo/BuscadorPortadasGpu.cs|538|Letra(15f * s, 11f, true, false);|Letra(15f * s, 11f, false, false);|revision: las palabras de la barra de ayudas sin la negrita de su Letra"
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
  echo "$que: $d de $(wc -l < $S/control.tsv) lineas distintas ($(diff $S/rapido-ref.tsv $S/control.tsv | grep '^>' | cut -f1 | sed -E 's/^> //; s/-(xp|sin-suavizar|copia-medio-traslada|unbit-copia-recorte)-.*$/-\1/; s/-p[0-4]$//' | sort | uniq -c | sort -rn | head -6 | tr -s ' ' | tr '\n' ' '))"
done
rm -rf $M $S/control-bin $S/control-ref-bin $S/control-proj/bin $S/control-proj/obj
