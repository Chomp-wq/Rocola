#!/bin/bash
# recorridos.sh PROG SALIDA [nombres...]: los recorridos de la rocola (pantallas de XP) con el programa de PROG, cada uno con una copia
# nueva de los datos (corre.sh). TRAZA=1 apunta la traza por linea de GDI+ (solo la copia instrumentada la escribe); CAPAS=1 guarda cada
# capa en las capturas; VOLCADO=1 vuelca las texturas que se suben; HUELLAS=1 cambia los PNG por sus huellas (huellas.py, para compara.py).
set -u
PROG=$1; SAL=$2; shift 2
W=${ROCOLA_TRABAJO:-/root/rocola-trabajo/port/dibujo}
declare -A P C T
# 1) pared, disco, lo que suena y el estado extendido
P[pared]="7:39,8:39,9:37,10:37,11:40,12:38,13:27+39,14.5:27+39,16:27+37,17.5:27+37,19:13,20.5:40,21.5:38,22.5:13,24:40,25:13,26.5:27,58:39,60:13,62:83,64:27,66:39,68:13,69:13,71:27"
C[pared]="6,9.5,13.8,20,23.5,27.5,45,50,55,61,65,70"; T[pared]=75
# 1b) lo mismo con otras opciones: la estrella en 2D (BakeStar), la luna realista y la del cielo vivo, la interfaz tramada y el cielo profundo
P[pared2]="${P[pared]}"; C[pared2]="6,20,27.5,45,55,70"; T[pared2]=75
declare -A K; K[pared2]="estrella_3d=0;luna_realista=1;luna_del_cielo=1;luna_activa=1;luna_protagonista=1;interfaz_tramada=1;cielo_profundo=1"
# 2) el video
P[video]="7:27+39,8.5:27+39,10:13,11:13,22:39,26:83"
C[video]="9,12,15,18,21,24,28"; T[video]=32
# 3) el buscador: teclas, resultados, aviso, vista previa y atajos
P[buscador]="7:27+38,8:76,8.6:65,10:39,10.4:39,10.8:39,11.2:39,11.6:39,12:39,12.4:39,12.8:39,13.2:39,13.6:39,14:40,15:40,16:38,17.5:13,20:27,21.5:37,22:37,22.5:37,23:37,23.5:37,24:27,24.6:27,25.5:38,26:38,26.5:38,27.5:13,29:39,29.5:39,30:39,30.5:39,31:40,32:13,34:27,35:27+38"
C[buscador]="7.8,9.5,14.5,16.5,19,21,25,28,30,33,36"; T[buscador]=40
# 4) la atraccion (40 s sin tocar nada y sin musica)
P[atraccion]="70:39"
C[atraccion]="45,52,59,66,73"; T[atraccion]=76
# 5) la ayuda, el asistente y la guia del rojo mantenido
P[ayuda]="7:27+40,9:13,11:39,12:39,13:13,15:40,16:13,18:27+40,21:27+40,22.5:40,23:40,24:13,26:39,27:37,28:40,29:38,31:27+40,34:27~5000"
C[ayuda]="8,10,12.5,14,17,19,22,25,27.5,30,33,36,38"; T[ayuda]=42
# 6) Ctrl+F3 por sus pestanas y hojas, Ctrl+F2 y la ventana de prueba F12
P[ctrlf3]="7:17+114,9:clic:394:53,11:clic:500:53,13:clic:712:53,14.5:clic:762:150,16:clic:896:150,17.5:clic:606:150,19:clic:818:53,20.5:clic:324:146,22:clic:510:146,23.5:clic:696:146,25:clic:880:146,26.5:clic:924:53,28:clic:512:146,29.5:clic:820:146,31:clic:606:53,32:9,32.5:9,33:32,33.5:32,35:27,38:17+113,42:27,44:123,48:123"
C[ctrlf3]="8,10,12,14,15.5,17,18.5,20,21.5,23,24.5,26,27.5,29,30.5,32.2,34,36.5,40,46,50"; T[ctrlf3]=52
# 6b) las hojas de Sonido (Ajustes del sonido...)
P[hojas]="7:17+114,9:clic:872:186,11:clic:742:186,12.5:clic:752:502,14:clic:742:243,15.5:clic:752:502,17:clic:742:301,18.5:clic:752:502,20:clic:742:358,21.5:clic:752:502,23:clic:742:186,24.5:clic:198:502,27:27"
C[hojas]="8,10,12,13.5,15,16.5,18,19.5,21,22.5,24,26,28"; T[hojas]=30
# 7) el asistente de botones (sin rocola_teclas.txt)
P[botones]="8:38,9:40,10:37,11:39,12:13,13:27,14:67,15:83,18:39,19:13"
C[botones]="6,8.5,10.5,12.5,14.5,16,20"; T[botones]=22
NOMBRES="$*"; [ -z "$NOMBRES" ] && NOMBRES="pared pared2 video buscador atraccion ayuda ctrlf3 hojas botones"
for n in $NOMBRES; do
  S="$SAL/$n"; rm -rf "$S"; mkdir -p "$S"
  caps=""; for c in ${C[$n]//,/ }; do caps="$caps,$c:$S/c$c.png"; done; caps=${caps#,}
  extra=()
  [ "${TRAZA:-0}" = 1 ] && extra+=("ROCOLA_TRAZA_GDI=$S/traza.tsv")
  [ "${CAPAS:-0}" = 1 ] && extra+=("ROCOLA_CAPTURA_CAPAS=1")
  [ "${VOLCADO:-0}" = 1 ] && { mkdir -p "$S/tex"; extra+=("ROCOLA_VOLCADO_TEXTURAS=$S/tex"); }
  [ "$n" = botones ] && export SIN_TECLAS=1 || export SIN_TECLAS=0
  export CONFIG="${K[$n]:-}"
  [ "${SIN_CAPTURAS:-0}" = 1 ] && caps="${T[$n]}:$S/fin.png"
  TIEMPO=$(( ${T[$n]} + 25 )) "$(dirname "$0")/corre.sh" "$PROG" "$S" ROCOLA_PULSA="${P[$n]}" ROCOLA_CAPTURA="$caps" ROCOLA_CAPTURA_SALE=1 "${extra[@]}"
  [ "${HUELLAS:-0}" = 1 ] && python3 "$(dirname "$0")/huellas.py" "$S"
  echo "$n: $(tail -1 $S/salida.txt)"
done
