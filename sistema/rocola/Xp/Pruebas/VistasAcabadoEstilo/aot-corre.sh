#!/bin/bash
# aot-corre.sh AOT SALIDA [CONFIG]: el binario NativeAOT (la carpeta de dotnet publish) por las pantallas del grupo «acabado-estilo» (la
# pared con la franja de lo que suena con la letra de lejos, un disco con su lista -el vidrio en piezas, o en la imagen de la interfaz con
# CONFIG="glass_capas=0"-, pedir, el rojo mantenido con su guia y el buscador), con los datos de los recorridos (el disco «Larga Duracion»),
# fuera de pantalla, con el sonido a tiempo real (Recorridos/ritmo.py) y capturas; deja lo que escribio (registro.txt) y rocola_error.txt.
# Usa Tarjeta/guiones/corre.sh con su propia carpeta de trabajo (TARJETA=SALIDA/tarjeta), no la de los recorridos.
set -u
A=$(readlink -f "$1"); O=$(readlink -m "$2"); CFG=${3:-}
X=$(cd "$(dirname "$(readlink -f "$0")")/../.." && pwd)
rm -rf "$O"; mkdir -p "$O/tarjeta" "$O/refdatos/sistema/rocola/Xp/bin/Release"
# los datos de prueba de los recorridos, donde corre.sh de la tarjeta busca los de la referencia
ln -sfn "${ROCOLA_TRABAJO:-/root/rocola-trabajo/port/dibujo}/datos-base" "$O/refdatos/sistema/rocola/Xp/bin/Release/net10.0"
FIFO=$O/ritmo.fifo; mkfifo "$FIFO"
python3 "$X/Pruebas/Recorridos/ritmo.py" "$FIFO" & RP=$!
P="7:39,8:39,9:13,10.5:40,11.5:40,12.5:38,13.5:13,15:27,16:27~4000,21:27+38,22:76,22.6:65,24:40,25:39,26:13,28:27,29:27,31:39,33:13,34.5:13,36:27"
caps=""; for c in 8.5 10 12 14 18 19.5 23 24.5 26.5 30 34 35.5 38; do caps="$caps,$c:$O/c$c.png"; done; caps=${caps#,}
CONFIG="$CFG" REFERENCIA="$O/refdatos" TARJETA="$O/tarjeta" CONSERVA=1 ROCOLA_ALSA="file:FILE=$FIFO,FORMAT=wav" ROCOLA_PULSA="$P" \
  ROCOLA_CAPTURA="$caps" ROCOLA_CAPTURA_SALE=1 "$X/Pruebas/Tarjeta/guiones/corre.sh" "$A" "$O" 90 > "$O/corre.txt" 2>&1
kill $RP 2>/dev/null; wait $RP 2>/dev/null
run=$(sed -n 's/^corrida en //p' "$O/corre.txt")
[ -n "$run" ] && { cp -p "$run/rocola_error.txt" "$O/" 2>/dev/null; rm -rf "$run"; }
rm -f "$FIFO"
cuenta() { [ -f "$2" ] && grep -c "$1" "$2"; true; }
echo "aot ${CFG:-de fabrica}: $(tail -1 "$O/registro.txt"); capturas: $(ls "$O"/*.png 2>/dev/null | wc -l); lineas con Exception en lo que escribio: $(cuenta Exception "$O/registro.txt"); en rocola_error.txt: $(cuenta Exception "$O/rocola_error.txt") (de $(cuenta '^20' "$O/rocola_error.txt") apuntes)"
