#!/bin/bash
# LA PRUEBA DE LOS BOTONES DE VOLUMEN (27-11-2026, RocolaOs/RocolaOs.TeclasVolumen.cs; ganchos g420-g424; la cabecera de RocolaOs.TeclasVolumen.cs). La rocola entera,
# fuera de pantalla (1024x768), sin sonido, con los datos INVENTADOS de las escenas de ventanas (DATOS0) y un AMIXER FALSO delante en el PATH que apunta
# cada llamada (el volumen de verdad se pide a amixer: Plataforma/VolumenTeclas.cs). Las teclas, con ROCOLA_PULSA (codigos de Windows: arriba 38, poner 13,
# regresar 27; multimedia 0xAD mudo = 173, 0xAE bajar = 174, 0xAF subir = 175).
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   W: la carpeta de trabajo (por defecto /tmp/rocola-volumen; se rehace)
# escenas: pasos (los botones configurados y las teclas multimedia suben, bajan y callan), xp (un boton de volumen que es uno de los 8 de XP: gana XP; las
#   multimedia, siempre), disco (REVISION 27-11-2026: un boton de volumen NO reinicia la inactividad: dentro de un disco, el volumen a los 20 s y el disco se
#   cierra igual a los 30 s) y control (el mismo disco sin tocar el volumen, para comparar)
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
escenas="${*:-pasos xp control disco}"
W=${W:-/tmp/rocola-volumen}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
export ROCOLA_CLIMA_API="${ROCOLA_CLIMA_API:-http://127.0.0.1:7}" ROCOLA_LUGAR_API="${ROCOLA_LUGAR_API:-http://127.0.0.1:7/lugar}"
export ROCOLA_LETRA_API="${ROCOLA_LETRA_API:-http://127.0.0.1:7}" ROCOLA_GENIUS_API="${ROCOLA_GENIUS_API:-http://127.0.0.1:7}"
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W/bin"
printf '#!/bin/sh\necho "$@" >> "$AMIXER_LOG"\ncase "$*" in *scontrols*) echo "Simple mixer control '"'"'Master'"'"',0";; esac\n' > "$W/bin/amixer"; chmod +x "$W/bin/amixer"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
distintos() { python3 -c "import sys;from PIL import Image,ImageChops;a=Image.open(sys.argv[1]).convert('RGB');b=Image.open(sys.argv[2]).convert('RGB');x,y,w,h,u=map(int,sys.argv[3:8]);d=ImageChops.difference(a.crop((x,y,x+w,y+h)),b.crop((x,y,x+w,y+h)));print(sum(1 for p in d.getdata() if max(p)>u))" "$@" 2>/dev/null || echo 0; }
# abre NOMBRE "PULSA" SEGUNDOS [VAR=valor...] con los datos de la escena en $W/datos-NOMBRE (el rocola_teclas_volumen.txt de la escena, si lo hay, en $W/NOMBRE.vol)
abre() {
  local n="$1" pulsa="$2" seg="$3"; shift 3
  local d="$W/datos-$n" o="$W/$n"; rm -rf "$d"; mkdir -p "$d" "$o/run"; cp -r "$DATOS0"/. "$d"/
  printf 'spotify=0\n' > "$d/rocola_os.txt"
  [ -f "$W/$n.vol" ] && cp "$W/$n.vol" "$d/rocola_teclas_volumen.txt"
  : > "$o/amixer.txt"
  ( cd "$d" && exec env PATH="$W/bin:$PATH" AMIXER_LOG="$o/amixer.txt" ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_OS=1 "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  ROCOLA=$!
  ( sleep "$seg"; kill -TERM $ROCOLA 2>/dev/null; sleep 30; kill -KILL $ROCOLA 2>/dev/null ) & VIG=$!
}
espera() { wait $ROCOLA; echo $? > "$W/$1/salio"; kill $VIG 2>/dev/null; wait $VIG 2>/dev/null; }
sset() { grep -c -- "$2" "$W/$1/amixer.txt" 2>/dev/null; }
cap() { echo "$1:$W/$2.png"; }

for e in $escenas; do
  echo "== $e"
  case "$e" in
  pasos)
    # los botones configurados (A = subir, B = bajar) y las multimedia (subir, bajar, mudo): cada toque, un paso del 2 % o el silencio
    printf 'subir=65\r\nbajar=66\r\n' > "$W/$e.vol"
    abre $e "10:65,12:65,14:66,16:175,18:174,20:173" 26; espera $e
    sed 's/^/   amixer: /' "$W/$e/amixer.txt"
    si "pasos: sale con 0" test "$(cat $W/$e/salio)" = 0
    si "pasos: tres toques de subir (A, A y la multimedia): $(sset $e '2%+')" test "$(sset $e '2%+')" = 3
    si "pasos: dos de bajar (B y la multimedia): $(sset $e '2%-')" test "$(sset $e '2%-')" = 2
    si "pasos: el mudo, una vez ($(sset $e toggle))" test "$(sset $e toggle)" = 1 ;;
  xp)
    # (revision 27-11-2026) un boton de volumen guardado que es uno de los 8 de XP (subir=38 es ARRIBA, bajar=13 es PONER): gana el de XP y no sube ni baja
    # nada; la tecla multimedia, si
    printf 'subir=38\r\nbajar=13\r\n' > "$W/$e.vol"
    abre $e "10:38,12:13,14:175" 20; espera $e
    sed 's/^/   amixer: /' "$W/$e/amixer.txt"
    si "xp: sale con 0" test "$(cat $W/$e/salio)" = 0
    si "xp: ARRIBA y PONER (de XP) no tocaron el volumen: ni un «2%-» ($(sset $e '2%-')) y un solo «2%+» ($(sset $e '2%+'), el de la tecla multimedia)" bash -c "[ '$(sset $e '2%-')' = 0 ] && [ '$(sset $e '2%+')' = 1 ]" ;;
  control|disco)
    # UN DISCO ABIERTO (PONER a los 8 s) sale solo a los 30 s sin tocar nada (a los 38 s); con el volumen a los 20 s tiene que salir igual (la captura de los
    # 36 s lo muestra abierto y la de los 43 s, ya cerrado); en «control» no se toca el volumen (lo mismo). Antes el boton de volumen reiniciaba la
    # inactividad y el disco seguia abierto hasta los 50 s
    printf 'subir=65\r\nbajar=66\r\n' > "$W/$e.vol"
    if [ $e = disco ]; then P="8:13,20:175"; else P="8:13"; fi
    abre $e "$P" 48 ROCOLA_CAPTURA="$(cap 36 $e-a-los-36-s),$(cap 43 $e-a-los-43-s)"; espera $e
    si "$e: sale con 0" test "$(cat $W/$e/salio)" = 0
    d=$(distintos "$W/$e-a-los-36-s.png" "$W/$e-a-los-43-s.png" 0 100 1024 600 40)
    si "$e: a los 36 s el disco seguia abierto y a los 43 s ya no ($d puntos distintos)" test "$d" -gt 20000
    [ $e = disco ] && si "disco: el volumen si se movio ($(sset $e '2%+') toque)" test "$(sset $e '2%+')" = 1 ;;
  esac
done
echo "== $bien BIEN, $mal MAL"
[ $mal = 0 ] || exit 1
