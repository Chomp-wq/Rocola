#!/bin/bash
# top.sh PROG SALIDA: la rocola de PROG (carpeta con rocola-xp.dll) por la atraccion con la lista de lo mas pedido esta noche, que los
# recorridos no pasan (hacen falta tres canciones pedidas esa noche): los datos de los recorridos mas un rocola_noche.txt con cinco de la
# noche de hoy, en una carpeta fija propia (la misma para la referencia y la traduccion: la rocola ensena rutas), sin tocar nada, con
# capturas por capa y las texturas volcadas (huellas.py). Capturas: el escaparate (45 s), la lista entrando, entera y saliendo (70-85 s) y
# el escaparate otra vez (89 s). Se compara como los recorridos: compara.py REF TRADUCCION [REF2].
set -u
PROG=$1; SAL=$2
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
W=${TOP_TRABAJO:-/root/rocola-trabajo/port/atraccion/top-trabajo}
mkdir -p $W
[ -d $W/datos-base ] || cp -rp /root/rocola-trabajo/port/dibujo/datos-base $W/datos-base
C=$W/corrida
N=$(python3 -c "import datetime; print((datetime.datetime.now() - datetime.timedelta(hours=8)).strftime('%Y-%m-%d'))")
printf "noche=%s\r\n5|%s\r\n3|%s\r\n2|%s\r\n1|%s\r\n1|%s\r\n" "$N" "$C/musica/Los Ejemplos/Disco Uno/01 Cancion 1.mp3" \
  "$C/musica/Otra Banda/Primer Disco/02 Tema 2.mp3" "$C/musica/Larga Duracion/Disco Largo/02 Ángel de amor (versión ñandú) & más.mp3" \
  "$C/musica/Los Ejemplos/Disco Uno/03 Cancion 3.mp3" "$C/musica/Otra Banda/Primer Disco/01 Tema 1.mp3" > $W/datos-base/rocola_noche.txt
rm -rf "$SAL"; mkdir -p "$SAL/tex"
caps=""; for c in 45 66 71 72 74 78 84 89; do caps="$caps,$c:$SAL/c$c.png"; done; caps=${caps#,}
ROCOLA_TRABAJO=$W TIEMPO=115 $X/Pruebas/Recorridos/corre.sh "$PROG" "$SAL" ROCOLA_PULSA="93:39" ROCOLA_CAPTURA="$caps" ROCOLA_CAPTURA_SALE=1 \
  ROCOLA_CAPTURA_CAPAS=1 ROCOLA_VOLCADO_TEXTURAS=$SAL/tex
[ "${PNG:-0}" = 1 ] && { mkdir -p "$SAL/png"; cp -p "$SAL"/*.png "$SAL"/tex/*.png "$SAL/png/" 2>/dev/null; }
python3 $X/Pruebas/Recorridos/huellas.py "$SAL"
echo "$(basename $SAL): $(tail -1 $SAL/salida.txt); noche=$N"
