#!/bin/bash
# LA PRUEBA DE LA HOJA IMAGEN DE CTRL+F4 (27-11-2026; RocolaOs/RocolaOs.VentanaImagen.cs y RocolaOs.VentanaImagenFechas.cs;
# FASE11_hoja-imagen.md). Con datos INVENTADOS (los de partida de las escenas de ventanas, DATOS0), fuera de pantalla (1024x768) y sin sonido.
# NADA de esto es el A4.
#   bash prueba.sh PROGRAMA [orden interruptores ver tecla fechas sin]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT, con el ejecutable rocola-xp)
#   W (de fabrica /root/rocola-trabajo/fase11/imagen/prueba), DATOS0, CAPTURAS (donde se copian las capturas que se ensenan)
# Las cuatro funciones del cielo vienen encendidas de fabrica (27-11-2026): la escena «interruptores» las parte de APAGADAS a mano (rocola_os.txt); las demas las
# encienden en rocola_os.txt para que el cielo vivo ande y se pueda «Ver como se veria».
export ROCOLA_CLIMA_API="${ROCOLA_CLIMA_API:-http://127.0.0.1:7}" ROCOLA_LUGAR_API="${ROCOLA_LUGAR_API:-http://127.0.0.1:7/lugar}"
export ROCOLA_LETRA_API="${ROCOLA_LETRA_API:-http://127.0.0.1:7}" ROCOLA_GENIUS_API="${ROCOLA_GENIUS_API:-http://127.0.0.1:7}"
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-orden interruptores ver tecla fechas sin}"
W=${W:-/root/rocola-trabajo/fase11/imagen/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
CAPTURAS=${CAPTURAS:-}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "${W:?}"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
medida() { python3 -c "import struct,sys; d=open(sys.argv[1],'rb').read(24); print('%dx%d' % struct.unpack('>II', d[16:24]))" "$1" 2>/dev/null; }
export -f medida
guarda_captura() { [ -n "$CAPTURAS" ] && [ -f "$1" ] && { mkdir -p "$CAPTURAS"; cp "$1" "$CAPTURAS/$2"; }; return 0; }
escribe() { python3 "$G/../archivos/teclas.py" "$@"; }

# (la moneda en la «Q»: la «C» de DATOS0 se comeria letras de lo que se escribe, y la «5» una cifra de la fecha)
datos() { local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/; printf 'spotify=0\n%b' "${2:-}" > "$d/rocola_os.txt"; sed -i 's/^moneda=.*/moneda=81/' "$d/rocola_teclas.txt"; echo "$d"; }
abre() {
  local n="$1" d="$2" pulsa="$3" caps="$4" seg="$5" sen="$6"; shift 6
  local o="$W/$n"; mkdir -p "$o/run"
  local c=""; IFS=',' read -ra L <<< "$caps"
  for x in "${L[@]}"; do [ -z "$x" ] && continue; c="$c,${x%%:*}:$o/${x#*:}.png"; done; c="${c#,}"
  ( cd "$d" && exec env ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=null ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA="$c" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$!
  if [ "$sen" != NADA ]; then sleep "$seg"; kill -"$sen" $p 2>/dev/null; fi
  ( sleep $((seg + 40)); kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
}
# 1024x768: la ventana de Ctrl+F4 en (29,2), su cliente desde (32,31), la pagina desde (32,127). Las pestanas (y 54): Pantalla (500), Imagen (606),
# Sonido (712), Spotify (818), Archivos (924). Las hojitas de Imagen (y 144): Dia y noche (128), Fiestas (281), Clima (419), Vidrio (557), Ver (755).
# En Dia y noche: los interruptores en x 84, y 242 + 54 k (k=0 la funcion «cielo», 1 cielo_sol, 2 sol...); la barra de la ciudad en y 246, x 536 a 960.
# En Ver: «Cambiar la hora» (84,220), «Cambiar el clima» (84,442), «Verlo» (276,527). En Fiestas: «Agregar un cumpleanos» (639,580), «Quitar la elegida» (640,536).
IMAGEN="6:17+115,8:clic:606:54"
SUB_DIA=128; SUB_FIE=281; SUB_CLI=419; SUB_VID=557; SUB_VER=755
TODO=$'cielo=1\nfiestas=1\nclima=1\nvidrio=1\n'

if [[ " $partes " == *" orden "* ]]; then
  echo "== orden (las fechas propias: leer, agregar y quitar renglones sin tocar lo demas; --prueba-imagen)"
  mkdir -p "$W/orden"
  env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_DATOS="$W/orden/datos" timeout 60 "${RX[@]}" --prueba-imagen 2>&1 | grep -v '^Pantalla: ' > "$W/orden.txt"
  r=${PIPESTATUS[0]}
  sed 's/^/   /' "$W/orden.txt"
  si "--prueba-imagen sale con 0 ($(tail -1 "$W/orden.txt"))" test "$r" = 0
fi

if [[ " $partes " == *" interruptores "* ]]; then
  echo "== interruptores (las cuatro funciones puestas en 0 a mano: la hoja las enciende; una idea en gris no se guarda; la ciudad)"
  d=$(datos interruptores 'cielo=0\nfiestas=0\nclima=0\nvidrio=0\n')   # (de fabrica vienen encendidas desde el 27-11-2026: aqui se parten de 0 a mano)
  # (sol en gris; cielo; cielo_sol apagado y encendido; la ciudad con un clic a la mitad de la barra; el vidrio; se guarda al cerrar)
  P="$IMAGEN,9.4:clic:84:350,9.8:clic:84:242,10.6:clic:84:296,11.4:clic:84:296,12.2:clic:748:246,12.8:clic:$SUB_VID:144,13.6:clic:84:242,15.4:27"
  abre interruptores "$d" "$P" "9.2:imagen-dia-apagado,10.2:imagen-dia-encendido,13.2:imagen-vidrio,15.1:imagen-vidrio-encendido" 17 TERM ROCOLA_OS=1
  b="$d/rocola_os_bitacora.txt"; o="$W/interruptores"
  si "sale con 0" test "$(cat $o/salio)" = 0
  si "un clic en una idea en gris (sol, con «cielo» apagado) no guarda nada ni lo apunta" bash -c "! grep -q '^sol=' '$d/rocola_os.txt' && ! grep -qF 'Imagen: sol=' '$b'"
  si "el interruptor de la funcion «cielo»: cielo=1 guardado" bash -c "grep -qx 'cielo=1' '$d/rocola_os.txt' && grep -qF 'Imagen: cielo=1' '$b'"
  si "una idea apagada y encendida: cielo_sol=0 apuntado y despues cielo_sol=1" bash -c "grep -qF 'Imagen: cielo_sol=0' '$b' && grep -qF 'Imagen: cielo_sol=1' '$b' && ! grep -qx 'cielo_sol=0' '$d/rocola_os.txt'"
  si "la ciudad: ciudad_bar=N (de 1 a 41) guardado al cerrar" bash -c "grep -Eq '^ciudad_bar=([1-9]|[1-3][0-9]|4[01])$' '$d/rocola_os.txt' && grep -qF 'Imagen: ciudad_bar=' '$b'"
  si "el vidrio: vidrio=1 guardado, y sus ideas de fabrica apagadas (vidrio_ficha) sin tocar" bash -c "grep -qx 'vidrio=1' '$d/rocola_os.txt' && grep -qF 'Imagen: vidrio=1' '$b' && ! grep -q '^vidrio_ficha=' '$d/rocola_os.txt'"
  si "ninguna otra clave se cambio sola (fiestas y clima sin guardar)" bash -c "! grep -qE '^(fiestas|clima)=1' '$d/rocola_os.txt'"
  si "la captura de la hoja mide 1024x768" bash -c "[ \"\$(medida $o/imagen-dia-encendido.png)\" = 1024x768 ]"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  for f in imagen-dia-apagado imagen-dia-encendido imagen-vidrio imagen-vidrio-encendido; do guarda_captura "$o/$f.png" "$f.png"; done
fi

# ver: Ver como se veria con la hora cambiada: se ve 30 s, vuelve solo y no guarda nada
if [[ " $partes " == *" ver "* ]]; then
  echo "== ver («Ver como se veria»: la hora de las 20:00, 30 s; no guarda nada)"
  d=$(datos ver "$TODO"); cp "$d/rocola_os.txt" "$W/ver-antes.txt"
  P="$IMAGEN,9:clic:$SUB_VER:144,10:clic:84:220,11:clic:276:527"
  abre ver "$d" "$P" "9.6:imagen-ver,13:imagen-ver-rocola" 48 TERM ROCOLA_OS=1 ROCOLA_CLIMA_YA=1
  b="$d/rocola_os_bitacora.txt"; o="$W/ver"
  si "sale con 0" test "$(cat $o/salio)" = 0
  si "la bitacora: ver como se veria a las 20:00 por 30 s, con la ventana cerrada para verlo" bash -c "grep -qE 'ver como se veria: [0-9-]+ 20:00 \(30 s\)' '$b' && grep -qF 'Ctrl+F4 abierta' '$b'"
  si "se acaban los 30 s solos" bash -c "grep -qF 'ver como se veria: se acaban los 30 s' '$b'"
  si "los 30 s pasan de verdad (entre 28 y 33 s de la bitacora)" python3 - "$b" <<'E'
import sys,datetime
L=[l for l in open(sys.argv[1]) if '[cielo]' in l and 'ver como se veria' in l]
f=lambda l: datetime.datetime.strptime(l[:19],'%Y-%m-%d %H:%M:%S')
a=[f(l) for l in L if '(30 s)' in l]; z=[f(l) for l in L if 'se acaban' in l]
d=(z[-1]-a[-1]).total_seconds() if a and z else -1; print('   %.0f s' % d); sys.exit(0 if 28 <= d <= 33 else 1)
E
  si "no se guardo nada: rocola_os.txt igual que antes" cmp -s "$d/rocola_os.txt" "$W/ver-antes.txt"
  si "la captura de la hoja y la de la rocola con lo elegido miden 1024x768" bash -c "[ \"\$(medida $o/imagen-ver.png)\" = 1024x768 ] && [ \"\$(medida $o/imagen-ver-rocola.png)\" = 1024x768 ]"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  guarda_captura "$o/imagen-ver.png" "imagen-ver.png"; guarda_captura "$o/imagen-ver-rocola.png" "imagen-ver-rocola.png"
fi

# tecla: un boton de la rocola quita lo que se ve
if [[ " $partes " == *" tecla "* ]]; then
  echo "== tecla (un boton de la rocola quita «Ver como se veria»; el clima forzado)"
  d=$(datos tecla "$TODO")
  P="$IMAGEN,9:clic:$SUB_VER:144,10:clic:84:442,11:clic:276:527,15:38"
  abre tecla "$d" "$P" "" 19 TERM ROCOLA_OS=1
  b="$d/rocola_os_bitacora.txt"; o="$W/tecla"
  si "sale con 0" test "$(cat $o/salio)" = 0
  si "ver como se veria con clima despejado (sin cambiar hora ni fecha)" bash -c "grep -qE 'ver como se veria: la hora de ahora con despejado \(30 s\)' '$b'"
  si "un boton lo quita al momento: «se toco un boton» y no «se acaban los 30 s»" bash -c "grep -qF 'ver como se veria: se quita (se toco un boton)' '$b' && ! grep -qF 'se acaban los 30 s' '$b'"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
fi

# fechas: agregar un cumpleanos (nombre y fecha en las hojitas de texto), elegirlo y quitarlo
if [[ " $partes " == *" fechas "* ]]; then
  echo "== fechas (tus fechas: agregar un cumpleanos con el teclado y quitarlo)"
  d=$(datos fechas "$TODO")
  printf '# mis fechas\n31-10;mensaje;Pasen a bailar\n' > "$d/rocola_fechas.txt"
  P="$IMAGEN,9:clic:$SUB_FIE:144,10.5:clic:639:580,$(escribe 12 0.15 Juan),13.5:13,$(escribe 15 0.15 15-03),16.2:13"
  abre fechas "$d" "$P" "9.5:imagen-fechas-antes,17.6:imagen-fechas" 19 TERM ROCOLA_OS=1
  b="$d/rocola_os_bitacora.txt"; o="$W/fechas"
  si "sale con 0" test "$(cat $o/salio)" = 0
  si "rocola_fechas.txt: lo que tenia (el comentario y el mensaje) sigue y se agrego «15-03;cumple;Juan»" bash -c "grep -qx '# mis fechas' '$d/rocola_fechas.txt' && grep -qx '31-10;mensaje;Pasen a bailar' '$d/rocola_fechas.txt' && grep -qx '15-03;cumple;Juan' '$d/rocola_fechas.txt'"
  si "la bitacora lo apunta, y el hilo del cielo lo leyo (la fecha nueva)" bash -c "grep -qF 'se agrego un cumpleaños a rocola_fechas.txt' '$b' && grep -qE 'rocola_fechas.txt: 2 fechas' '$b'"
  si "la captura mide 1024x768" bash -c "[ \"\$(medida $o/imagen-fechas.png)\" = 1024x768 ]"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  guarda_captura "$o/imagen-fechas-antes.png" "imagen-fechas-antes.png"; guarda_captura "$o/imagen-fechas.png" "imagen-fechas.png"
  # sin elegir nada, «Quitar» esta en gris; elegido el renglon de Juan (el 2.o de la lista, y = 292), «Quitar la elegida»
  P2="$IMAGEN,9:clic:$SUB_FIE:144,10:clic:640:536,12:27"
  abre fechas2 "$d" "$P2" "" 14 TERM ROCOLA_OS=1
  si "sin elegir nada, «Quitar» esta en gris: no quita nada (los dos renglones siguen)" bash -c "grep -qx '15-03;cumple;Juan' '$d/rocola_fechas.txt' && grep -qx '31-10;mensaje;Pasen a bailar' '$d/rocola_fechas.txt'"
  P3="$IMAGEN,9:clic:$SUB_FIE:144,10:clic:700:292,11:clic:640:536,13:27"
  abre fechas3 "$d" "$P3" "10.5:imagen-fechas-elegida,12:imagen-fechas-quitada" 15 TERM ROCOLA_OS=1
  si "elegido el renglon de Juan y «Quitar la elegida»: ya no esta, y lo demas queda como estaba" bash -c "! grep -q 'Juan' '$d/rocola_fechas.txt' && grep -qx '# mis fechas' '$d/rocola_fechas.txt' && grep -qx '31-10;mensaje;Pasen a bailar' '$d/rocola_fechas.txt'"
  for f in imagen-fechas-elegida imagen-fechas-quitada; do guarda_captura "$W/fechas3/$f.png" "$f.png"; done
fi

# sin: sin ROCOLA_OS, Ctrl+F4 no hace nada (XP tal cual)
if [[ " $partes " == *" sin "* ]]; then
  echo "== sin (sin ROCOLA_OS no hay hoja)"
  d=$(datos sin)
  abre sin "$d" "6:17+115,8:clic:606:54,10:27" "9:sin-ctrlf4" 12 TERM
  si "sin ROCOLA_OS: no se abre ninguna hoja y no se escribe nada de la hoja" bash -c "! grep -q 'dialogo' '$W/sin/traza.txt' && ! grep -q 'Imagen' '$d/rocola_os.txt'"
fi

echo; echo "bien: $bien  mal: $mal"
[ "$mal" = 0 ]
