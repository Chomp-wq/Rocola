#!/bin/bash
# LA PRUEBA DE LA HOJA SONIDO DE CTRL+F4 (el selector de la salida de sonido, 21-11-2026; RocolaOs/RocolaOs.Salida.cs y
# RocolaOs.VentanaSalida.cs; FASE11_salida-audio.md). Con datos INVENTADOS (los de partida de las escenas de ventanas, DATOS0), fuera de
# pantalla con el KMS SIMULADO de la hoja Pantalla, sin sonido (ROCOLA_ALSA=null) y un /proc/asound SIMULADO como el del A4 (la HDA del HDMI
# de la Radeon como tarjeta 0, la de la placa -Realtek ALC892- como tarjeta 1). El lanzador lo hace la prueba: tras cada cierre para
# reabrirse, la vuelve a abrir con los mismos datos. NADA de esto es el A4 ni una tarjeta de sonido de verdad.
#   bash prueba.sh PROGRAMA [lista elige vuelta confirma auto probar caidas sin]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   W (/root/rocola-trabajo/fase11/salida-audio/prueba), DATOS0, CAPTURAS (donde se copian las capturas que se ensenan)
export ROCOLA_CLIMA_API="${ROCOLA_CLIMA_API:-http://127.0.0.1:7}" ROCOLA_LUGAR_API="${ROCOLA_LUGAR_API:-http://127.0.0.1:7/lugar}"
export ROCOLA_LETRA_API="${ROCOLA_LETRA_API:-http://127.0.0.1:7}" ROCOLA_GENIUS_API="${ROCOLA_GENIUS_API:-http://127.0.0.1:7}"
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd); SIS=$(cd "$XP/../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-lista elige vuelta confirma auto probar caidas sin}"
W=${W:-/root/rocola-trabajo/fase11/salida-audio/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
CAPTURAS=${CAPTURAS:-}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
guarda_captura() { [ -n "$CAPTURAS" ] && [ -f "$1" ] && { mkdir -p "$CAPTURAS"; cp "$1" "$CAPTURAS/$2"; }; return 0; }
creditos() { sed -n 's/^creditos=//p' "$1" 2>/dev/null | tr -d '\r'; }
SIM="1920x1080@60*,1920x1080@50,1280x1024@75,1280x1024@60,1024x768@75,1024x768@60,800x600@60;gamma=256;ctm=0;controlador=radeon"

# EL /proc/asound DEL A4 (simulado): «0 [HDMI]: HDA ATI HDMI» con su salida 3 y su ELD, y la de la placa con la analogica y la digital
A="$W/asound"
mkdir -p "$A/card0/pcm3p" "$A/card1/pcm0p" "$A/card1/pcm1p" "$A/card1/pcm0c"
printf ' 0 [HDMI           ]: HDA-Intel - HDA ATI HDMI\n                      HDA ATI HDMI at 0xf0144000 irq 33\n 1 [Generic        ]: HDA-Intel - HD-Audio Generic\n                      HD-Audio Generic at 0xf0140000 irq 16\n' > "$A/cards"
echo HDMI > "$A/card0/id"; printf 'Codec: ATI R6xx HDMI\nAddress: 0\n' > "$A/card0/codec#0"; printf 'monitor_present\t\t1\n' > "$A/card0/eld#0.0"
printf 'card: 0\ndevice: 3\nsubdevice: 0\nstream: PLAYBACK\nid: HDMI 0\nname: HDMI 0\n' > "$A/card0/pcm3p/info"
echo Generic > "$A/card1/id"; printf 'Codec: Realtek ALC892\nAddress: 0\n' > "$A/card1/codec#0"
printf 'card: 1\ndevice: 0\nsubdevice: 0\nstream: PLAYBACK\nid: ALC892 Analog\nname: ALC892 Analog\n' > "$A/card1/pcm0p/info"
printf 'card: 1\ndevice: 1\nsubdevice: 0\nstream: PLAYBACK\nid: ALC892 Digital\nname: ALC892 Digital\n' > "$A/card1/pcm1p/info"
printf 'card: 1\ndevice: 0\nstream: CAPTURE\nname: ALC892 Analog\n' > "$A/card1/pcm0c/info"

# abre NOMBRE DATOS "PULSA" "CAPTURAS" SEGUNDOS SENAL [VAR=valor...] (como la prueba de la hoja Pantalla); el sistema de cada escena en
# $W/NOMBRE-sis (rocola_tarjeta.txt) salvo que la escena diga otro con SIS_DE
abre() {
  local n="$1" d="$2" pulsa="$3" caps="$4" seg="$5" sen="$6"; shift 6
  local o="$W/$n"; rm -rf "$o"; mkdir -p "$o/run" "$SISD"
  local c=""; IFS=',' read -ra L <<< "$caps"
  for x in "${L[@]}"; do [ -z "$x" ] && continue; c="$c,${x%%:*}:$o/${x#*:}.png"; done; c="${c#,}"
  ( cd "$d" && exec env -u ROCOLA_PANTALLA ROCOLA_CATALOGO_URL=http://127.0.0.1:9/catalogo ROCOLA_PORTADAS_CATALOGO=http://127.0.0.1:9 ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=null ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA="$c" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_KMS_SIMULADO="$SIM" ROCOLA_ASOUND="$A" ROCOLA_TARJETA_TXT="$SISD/rocola_tarjeta.txt" "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$!
  local t=0; while [ $t -lt $((seg * 10)) ] && kill -0 $p 2>/dev/null; do sleep 0.1; t=$((t + 1)); done
  [ "$sen" != NADA ] && kill -"$sen" $p 2>/dev/null
  ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
}
datos() { local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/; printf 'spotify=0\n' > "$d/rocola_os.txt"; echo "$d"; }
# 1024x768@75 (lo automatico del monitor simulado): la hoja de 966x764 en (29,2), su cliente en (32,31). Las pestanas (pegadas a la
# derecha): Pantalla (712,54), Sonido (818,54), Archivos (924,54) (el 27-11-2026 el navegador se quito: las pestanas de antes se corren una a la derecha). Los renglones de la lista: x 276, y 188 + 58 i. Los botones
# de la derecha: «Usar» (748,286), «Probar» (748,332), «Automatica» (748,378).
FILA0=188; FILA2=304
# una moneda (C) a los 4 y 4,3 s; Ctrl+F4 a los 6; la pestana Sonido a los 8
INICIO="4:67,4.3:67,6:17+115,8:clic:818:54"

if [[ " $partes " == *" lista "* ]]; then
  echo "== lista (la hoja con las salidas del A4 simulado)"
  d=$(datos lista); SISD="$W/lista-sis"
  abre lista "$d" "$INICIO,10:27" "9.5:salida-audio" 12 TERM ROCOLA_OS=1
  b="$d/rocola_os_bitacora.txt"
  grep -F '[salida_audio]' "$b" | sed 's/^/   bit: /' | cut -c1-200
  si "sale con 0" test "$(cat $W/lista/salio)" = 0
  si "las 3 salidas de reproduccion en el orden de aplay -l (la captura no cuenta), la del HDMI marcada; en uso la automatica, la placa" \
    contiene "$b" "salidas (3): 1 plughw:0,3 HDMI, 2 plughw:1,0, 3 plughw:1,1; en uso plughw:1,0 (automatica)"
  si "la captura de la hoja (1024x768)" test -s "$W/lista/salida-audio.png"
  si "sin abrir nada mas no se escribe nada (ni rocola_tarjeta.txt ni la prueba)" bash -c "[ ! -e '$SISD/rocola_tarjeta.txt' ] && [ ! -e '$d/rocola_salida_prueba.txt' ]"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  guarda_captura "$W/lista/salida-audio.png" "salida-audio.png"
  # un equipo sin sonido (sin /proc/asound: fuera de pantalla, o sin tarjetas): la hoja lo dice, sin fallos
  d=$(datos lista-sin); SISD="$W/lista-sin-sis"
  abre lista-sin "$d" "$INICIO,10:27" "9.5:salida-sin-tarjetas" 12 TERM ROCOLA_OS=1 ROCOLA_ASOUND="$W/no-esta"
  si "sin /proc/asound: ninguna salida, sin fallos en rocola_error.txt" bash -c "grep -qF 'salidas (0): ninguna; en uso default (automatica)' '$d/rocola_os_bitacora.txt' && ! grep -qF 'Rocola OS' '$d/rocola_error.txt'"
fi

# elige: la salida 1 (el HDMI) y «Usar»: se guarda, se apunta la prueba y la rocola se cierra para reabrirse conservando el estado
elige() {
  local n="$1" d="$2"
  abre "$n" "$d" "$INICIO,9:clic:276:$FILA0,10:clic:748:286" "9.6:salida-elegida" 20 NADA ROCOLA_OS=1
}
if [[ " $partes " == *" elige "* ]] || [[ " $partes " == *" vuelta "* ]]; then
  echo "== elige y vuelta (elegir el HDMI, reabrir conservando el estado; al volver nadie contesta: vuelve sola a la automatica)"
  d=$(datos vuelta); SISD="$W/vuelta-sis"
  elige elige "$d"
  o="$W/elige"; cp "$d/rocola_estado.txt" "$W/elige-estado.txt" 2>/dev/null
  si "elige: se cierra sola en unos segundos y sale con 0 (el lanzador la vuelve a abrir)" test "$(cat $o/salio)" = 0
  si "elige: rocola_tarjeta.txt dice plughw:0,3 (como «rocola-sonido usar 1»)" bash -c "[ \"\$(cat '$SISD/rocola_tarjeta.txt')\" = plughw:0,3 ]"
  si "elige: la prueba en los datos (antes=auto, nueva=plughw:0,3, arranques=0)" bash -c "grep -qx antes=auto '$d/rocola_salida_prueba.txt' && grep -qx nueva=plughw:0,3 '$d/rocola_salida_prueba.txt' && grep -qx arranques=0 '$d/rocola_salida_prueba.txt'"
  si "elige: la marca del lanzador ($o/run/rocola-xp-reinicio: no es una caida)" contiene "$o/run/rocola-xp-reinicio" "otra salida de sonido (Ctrl+F4)"
  n0=$(creditos "$W/elige-estado.txt")
  si "elige: el cierre que CONSERVA el estado (TaskManagerClosing) y rocola_estado.txt con los creditos de las 2 monedas (creditos=$n0)" \
    bash -c "grep -q 'closed Form1.Rocola. TaskManagerClosing' '$o/traza.txt' && [ '${n0:-0}' -gt 0 ]"
  si "elige: rocola_sonido.txt lo apunta, como rocola-sonido" contiene "$SISD/rocola_sonido.txt" "se eligio a mano la salida plughw:0,3 en Ctrl+F4"
  # la vuelta: el lanzador la abre otra vez (aqui, la prueba); nadie contesta
  abre vuelta "$d" "" "6:salida-pregunta" 30 NADA ROCOLA_OS=1
  o="$W/vuelta"; b="$d/rocola_os_bitacora.txt"
  grep -F '[salida_audio]' "$b" | sed 's/^/   bit: /' | cut -c1-200
  s=$(awk '/«¿Se oye bien\?»$/ && !t {t=$2} /nadie contesto/ && t {print $2; exit}' "$b")
  si "vuelta: pregunta «¿Se oye bien?» y nadie contesta en 15 s (la bitacora)" bash -c "grep -qF 'prueba de plughw:0,3: «¿Se oye bien?»' '$b' && grep -qF 'prueba de plughw:0,3: nadie contesto en 15 s' '$b'"
  si "vuelta: la pregunta y la vuelta, entre 14 y 17 s (por la hora de la bitacora)" python3 - "$b" <<'E'
import sys,datetime
L=[l for l in open(sys.argv[1]) if '[salida_audio]' in l]
f=lambda l: datetime.datetime.strptime(l[:19],'%Y-%m-%d %H:%M:%S')
a=[f(l) for l in L if l.rstrip().endswith('«¿Se oye bien?»')]; b=[f(l) for l in L if 'nadie contesto' in l]
d=(b[-1]-a[-1]).total_seconds() if a and b else -1; print('   %.0f s' % d); sys.exit(0 if 14 <= d <= 17 else 1)
E
  si "vuelta: vuelve sola a la automatica (rocola_tarjeta.txt borrado), sin la prueba, y se reabre otra vez conservando el estado" \
    bash -c "[ ! -e '$SISD/rocola_tarjeta.txt' ] && [ ! -e '$d/rocola_salida_prueba.txt' ] && [ \"\$(cat $o/salio)\" = 0 ] && grep -q 'closed Form1.Rocola. TaskManagerClosing' '$o/traza.txt' && grep -qF 'la salida de sonido de antes' '$o/run/rocola-xp-reinicio'"
  si "vuelta: los creditos siguen tras los dos reinicios (creditos=$(creditos $d/rocola_estado.txt), antes $n0)" test "$(creditos $d/rocola_estado.txt)" = "${n0:-x}"
  si "vuelta: la captura de la pregunta" test -s "$o/salida-pregunta.png"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  guarda_captura "$W/elige/salida-elegida.png" "salida-elegida.png"; guarda_captura "$o/salida-pregunta.png" "salida-pregunta.png"
fi

if [[ " $partes " == *" confirma "* ]]; then
  echo "== confirma (elegir el HDMI y al volver «Si, se oye bien» con Tab e Intro)"
  d=$(datos confirma); SISD="$W/confirma-sis"
  elige confirma-elige "$d"
  abre confirma "$d" "5:9,5.3:13" "" 9 TERM ROCOLA_OS=1
  b="$d/rocola_os_bitacora.txt"
  si "confirma: queda plughw:0,3, sin la prueba, y la bitacora lo dice" bash -c "[ \"\$(cat '$SISD/rocola_tarjeta.txt')\" = plughw:0,3 ] && [ ! -e '$d/rocola_salida_prueba.txt' ] && grep -qF 'confirmada: plughw:0,3' '$b'"
  si "confirma: no se reabre (sigue hasta el SIGTERM: el apagado de siempre, WindowsShutDown)" bash -c "[ ! -e '$W/confirma/run/rocola-xp-reinicio' ] && grep -q 'closed Form1.Rocola. WindowsShutDown' '$W/confirma/traza.txt'"
  # y otra vez: ya no pregunta
  abre confirma2 "$d" "" "" 8 TERM ROCOLA_OS=1
  si "confirma: el siguiente arranque ya no pregunta y dice la elegida a mano" bash -c "[ \$(grep -cF 'plughw:0,3: «¿Se oye bien?»' '$b') = 1 ] && grep -qF 'en uso plughw:0,3 (elegida a mano)' '$b'"
fi

if [[ " $partes " == *" auto "* ]]; then
  echo "== auto (con el HDMI guardado, «Automatica»)"
  d=$(datos auto); SISD="$W/auto-sis"; mkdir -p "$SISD"; echo plughw:0,3 > "$SISD/rocola_tarjeta.txt"
  abre auto "$d" "$INICIO,9.5:clic:748:378" "9:salida-auto" 20 NADA ROCOLA_OS=1
  si "auto: rocola_tarjeta.txt borrado, la prueba antes=plughw:0,3 nueva=auto, y se reabre" bash -c "[ ! -e '$SISD/rocola_tarjeta.txt' ] && grep -qx antes=plughw:0,3 '$d/rocola_salida_prueba.txt' && grep -qx nueva=auto '$d/rocola_salida_prueba.txt' && [ -e '$W/auto/run/rocola-xp-reinicio' ]"
  si "auto: rocola_sonido.txt lo apunta" contiene "$SISD/rocola_sonido.txt" "se eligio la salida automatica en Ctrl+F4"
fi

if [[ " $partes " == *" probar "* ]]; then
  echo "== probar (un pitido por el HDMI, otra tarjeta; por la digital de la placa, la misma tarjeta que suena, no)"
  d=$(datos probar); SISD="$W/probar-sis"
  printf '#!/bin/sh\necho "$*" >> "%s"\nsleep 1\n' "$W/tono.txt" > "$W/tono.sh"; chmod 755 "$W/tono.sh"
  abre probar "$d" "$INICIO,9:clic:276:$FILA0,9.5:clic:748:332,12:clic:276:$FILA2,12.5:clic:748:332,13.5:27" "10:salida-probando,12.8:salida-misma-tarjeta" 15 TERM ROCOLA_OS=1 ROCOLA_TONO="$W/tono.sh"
  si "probar: un pitido, por plughw:0,3 (tarjeta 0), y ninguno por la 1,1" bash -c "[ \"\$(cat '$W/tono.txt')\" = 'plughw:0,3 0' ]"
  si "probar: no cambia nada (ni rocola_tarjeta.txt ni la prueba) y no se reabre" bash -c "[ ! -e '$SISD/rocola_tarjeta.txt' ] && [ ! -e '$d/rocola_salida_prueba.txt' ] && [ ! -e '$W/probar/run/rocola-xp-reinicio' ]"
  si "probar: la bitacora" contiene "$d/rocola_os_bitacora.txt" "probada plughw:0,3 (un pitido de 3 s)"
  guarda_captura "$W/probar/salida-misma-tarjeta.png" "salida-misma-tarjeta.png"
fi

if [[ " $partes " == *" caidas "* ]]; then
  echo "== caidas (la prueba con 2 arranques sin contestar: el tercero vuelve a la de antes sin preguntar)"
  d=$(datos caidas); SISD="$W/caidas-sis"; mkdir -p "$SISD"; echo plughw:0,3 > "$SISD/rocola_tarjeta.txt"
  printf 'antes=plughw:1,0\nnueva=plughw:0,3\narranques=2\n' > "$d/rocola_salida_prueba.txt"
  abre caidas "$d" "" "" 12 NADA ROCOLA_OS=1
  b="$d/rocola_os_bitacora.txt"
  si "caidas: vuelve a plughw:1,0 sin preguntar, sin la prueba, y se reabre" bash -c "[ \"\$(cat '$SISD/rocola_tarjeta.txt')\" = plughw:1,0 ] && [ ! -e '$d/rocola_salida_prueba.txt' ] && ! grep -qF '«¿Se oye bien?»' '$b' && grep -qF 'lleva 3 arranques sin contestar: vuelve a plughw:1,0' '$b' && [ -e '$W/caidas/run/rocola-xp-reinicio' ]"
  # un arranque normal con la prueba (el apagon a media prueba): la cuenta sube y vuelve a preguntar
  d=$(datos apagon); SISD="$W/apagon-sis"; mkdir -p "$SISD"; echo plughw:0,3 > "$SISD/rocola_tarjeta.txt"
  printf 'antes=auto\nnueva=plughw:0,3\narranques=0\n' > "$d/rocola_salida_prueba.txt"
  abre apagon "$d" "" "" 5 KILL ROCOLA_OS=1
  si "apagon: a media prueba (kill -9) la prueba sigue con arranques=1 y la salida nueva puesta" bash -c "grep -qx arranques=1 '$d/rocola_salida_prueba.txt' && [ \"\$(cat '$SISD/rocola_tarjeta.txt')\" = plughw:0,3 ]"
fi

if [[ " $partes " == *" sin "* ]]; then
  echo "== sin ROCOLA_OS: nada (ni se lee la prueba ni se toca rocola_tarjeta.txt)"
  d=$(datos sin); rm -f "$d/rocola_os.txt"; SISD="$W/sin-sis"; mkdir -p "$SISD"; echo plughw:0,3 > "$SISD/rocola_tarjeta.txt"
  printf 'antes=auto\nnueva=plughw:0,3\narranques=2\n' > "$d/rocola_salida_prueba.txt"
  abre sin "$d" "6:17+115" "" 9 TERM
  si "sin: la prueba y rocola_tarjeta.txt como estaban, sin bitacora de Rocola OS" bash -c "grep -qx arranques=2 '$d/rocola_salida_prueba.txt' && [ \"\$(cat '$SISD/rocola_tarjeta.txt')\" = plughw:0,3 ] && [ ! -e '$d/rocola_os_bitacora.txt' ]"
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; exit 0; else echo "$mal MAL de $((bien + mal))"; exit 1; fi
