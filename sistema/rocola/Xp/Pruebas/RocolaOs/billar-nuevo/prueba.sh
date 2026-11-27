#!/bin/bash
# LA PRUEBA DE LAS PIEZAS DE LA CADENA NUEVA DEL BILLAR (B; RocolaOs/RocolaOs.Billar2*.cs; CADENA_NUEVA.md §3 y CADENA_NUEVA_B.md). Con SENALES
# SINTETICAS (ruido rosa, tonos, un bombo, un bajo que cambia de nota): nada se oyo ni se midio en el billar ni en el A4.
#   bash prueba.sh PROGRAMA [partes...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   partes (de fabrica todas): fft mono pasoalto curva noche bajo constante disco afinacion guarda cpu hoja
#   W (/root/rocola-trabajo/fase11/billar-nuevo/prueba; se rehace), DATOS0 (los datos de partida de las escenas de ventanas), CAPTURAS (donde
#   se copian las capturas de la hoja, si se pide)
# Las partes de cuentas las hace la orden `rocola-xp --prueba-billar2 PARTE` (RocolaOs/RocolaOs.Billar2Prueba.cs: lo que comprueba cada una, en
# su cabecera). guarda: los ajustes y la afinacion en rocola_os.txt (con ROCOLA_OS=billar_nuevo). hoja: la rocola de verdad fuera de pantalla
# (el KMS simulado de la hoja Pantalla, sin sonido): Ctrl+F4, la pestana «Billar» (solo con la cadena nueva elegida o billar_nuevo=1), una
# lectura del punto 1, dBA y el paso alto; lo que queda en rocola_os.txt; y sin ninguna de las dos, la pestana no sale.
# Cada comprobacion: BIEN o MAL en una linea; al final «TODO BIEN (N comprobaciones)» o cuantas MAL (y sale con 1).
# CON OTROS AGENTES TRABAJANDO, correrla con el candado de lo pesado (como la bateria): flock -o /root/rocola-trabajo/fase11/pesado.lock bash
# prueba.sh ...; la hoja abre la rocola entera tres veces y con la maquina cargada de mas (carga 10-15 en 4 nucleos) se vio una vez la pantalla
# parada mas de 30 s (la ventana no llego a cambiar de pestana); con el candado, 6 de 6 bien.
export ROCOLA_CLIMA_API="${ROCOLA_CLIMA_API:-http://127.0.0.1:7}" ROCOLA_LUGAR_API="${ROCOLA_LUGAR_API:-http://127.0.0.1:7/lugar}"
export ROCOLA_LETRA_API="${ROCOLA_LETRA_API:-http://127.0.0.1:7}" ROCOLA_GENIUS_API="${ROCOLA_GENIUS_API:-http://127.0.0.1:7}"
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-fft mono pasoalto curva noche bajo constante disco afinacion guarda cpu hoja}"
W=${W:-/root/rocola-trabajo/fase11/billar-nuevo/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
CAPTURAS=${CAPTURAS:-}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
guarda_captura() { [ -n "$CAPTURAS" ] && [ -f "$1" ] && { mkdir -p "$CAPTURAS"; cp "$1" "$CAPTURAS/$2"; }; return 0; }
# las cuentas: la orden de prueba; sus BIEN y MAL cuentan aqui
cuentas() {
  local o="$W/cuentas-${1%% *}.txt"
  ( env ROCOLA_PANTALLA=fuera:64x64 "${@:2}" timeout 1800 "${RX[@]}" --prueba-billar2 $1 ) > "$o" 2>&1
  grep -E '^(BIEN|MAL|INFO) ' "$o"
  local b m; b=$(grep -c '^BIEN ' "$o"); m=$(grep -c '^MAL ' "$o")
  bien=$((bien + b)); mal=$((mal + m))
  grep -qE '^(TODO BIEN|[0-9]+ MAL)' "$o" || { echo "MAL   $1: la orden no acabo ($(tail -3 "$o" | tr '\n' ' '))"; mal=$((mal + 1)); }
}
for p in fft mono pasoalto curva noche bajo constante disco afinacion cpu; do
  [[ " $partes " == *" $p "* ]] && { echo "== $p"; cuentas $p; }
done
if [[ " $partes " == *" guarda "* ]]; then
  echo "== guarda (rocola_os.txt con ROCOLA_OS=billar_nuevo)"
  D="$W/guarda"; mkdir -p "$D"
  cuentas "guarda $D" ROCOLA_OS=billar_nuevo ROCOLA_DATOS="$D"
  sed 's/^/   rocola_os.txt: /' "$D/rocola_os.txt"
fi

# ---------------------------------------------------------------- hoja: la rocola de verdad
if [[ " $partes " == *" hoja "* ]]; then
  echo "== hoja (la pestana «Billar» de Ctrl+F4, fuera de pantalla con el KMS simulado)"
  SIM="1920x1080@60*,1920x1080@50,1280x1024@75,1280x1024@60,1024x768@75,1024x768@60,800x600@60;gamma=256;ctm=0;controlador=radeon"
  abre() {   # abre NOMBRE DATOS "PULSA" "CAPTURAS" SEGUNDOS [VAR=valor...]
    local n="$1" d="$2" pulsa="$3" caps="$4" seg="$5"; shift 5
    local o="$W/$n"; rm -rf "$o"; mkdir -p "$o/run"
    local c=""; IFS=',' read -ra L <<< "$caps"
    for x in "${L[@]}"; do [ -z "$x" ] && continue; c="$c,${x%%:*}:$o/${x#*:}.png"; done; c="${c#,}"
    ( cd "$d" && exec env -u ROCOLA_PANTALLA ROCOLA_CATALOGO_URL=http://127.0.0.1:9/catalogo ROCOLA_PORTADAS_CATALOGO=http://127.0.0.1:9 ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=null \
        ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA="$c" ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_KMS_SIMULADO="$SIM" ROCOLA_TARJETA_TXT="$o/rocola_tarjeta.txt" \
        "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
    local p=$!
    local t=0; while [ $t -lt $((seg * 10)) ] && kill -0 $p 2>/dev/null; do sleep 0.1; t=$((t + 1)); done
    kill -TERM $p 2>/dev/null
    ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
    wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
  }
  datos() { local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/; printf 'spotify=0\n%s\n' "$2" > "$d/rocola_os.txt"; echo "$d"; }
  # 1024x768@75: la hoja de 966x764 en (29,2), su cliente en (32,31). Con la pestana Billar son seis, pegadas a la derecha: Pantalla (394,54),
  # Imagen (500,54), Sonido (606,54), Spotify (712,54), Billar (818,54), Archivos (924,54) (con la «Cadena» del marco: Billar sigue en 818).
  # En la hoja: la barra de 63 Hz del punto 1 en (138..388, 288); «dBA» en (251, 544); el paso alto en (692..868, 375); «Tocar» en (276, 223).
  d=$(datos hoja "billar_nuevo=1"); o="$W/hoja"
  abre hoja "$d" "6:17+115,8:clic:818:54,9:clic:300:288,10:clic:251:544,11:clic:821:375,12:clic:276:223,16:27" "8.8:billar,14.5:billar-cambiado" 18 ROCOLA_OS=1
  b="$d/rocola_os.txt"
  sed 's/^/   rocola_os.txt: /' "$b"
  si "hoja: sale con 0" test "$(cat $o/salio)" = 0
  si "hoja: la captura de la pestana (1024x768)" test -s "$o/billar.png"
  si "hoja: un clic en la barra de 63 Hz del punto 1 lo guarda (billar_afina_1=N;x;x;x;x;x;x;x)" bash -c "grep -qE '^billar_afina_1=[0-9]+;x;x;x;x;x;x;x$' '$b'"
  si "hoja: «dBA» lo guarda (billar_afina_pond=A)" bash -c "grep -qx 'billar_afina_pond=A' '$b'"
  si "hoja: el paso alto a tres cuartos de su barra: 125 Hz (billar_paso_alto=125)" bash -c "grep -qx 'billar_paso_alto=125' '$b'"
  si "hoja: la bitacora apunta los cambios y el diseno de fondo" bash -c "grep -qF 'Billar: billar_paso_alto=125' '$d/rocola_os_bitacora.txt' && grep -qF 'diseno 44100 Hz hecho' '$d/rocola_os_bitacora.txt'"
  si "hoja: «Tocar las 8 bandas» (en mono, por su propia salida: aqui la nula)" bash -c "grep -qF 'suenan las 8 bandas en mono' '$d/rocola_os_bitacora.txt'"
  si "hoja: rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  guarda_captura "$o/billar.png" "billar.png"; guarda_captura "$o/billar-cambiado.png" "billar-cambiado.png"
  # sin la cadena nueva ni billar_nuevo: la pestana no sale (en 818 esta Spotify, como siempre: cinco pestanas, Spotify en (818,54))
  d=$(datos sin ""); o="$W/sin"
  abre sin "$d" "6:17+115,8:clic:818:54,10:27" "9:sin-billar" 12 ROCOLA_OS=1
  si "sin la cadena nueva: la pestana Billar no sale (la hoja que se abre en (818,54) es Spotify, como siempre)" bash -c "grep -q 'Rocola OS - Spotify' '$o/traza.txt' && ! grep -q 'Rocola OS - Billar' '$o/traza.txt'"
  # con la cadena nueva elegida (la clave del marco), si sale
  d=$(datos cadena "cadena_nueva=1"); o="$W/cadena"
  abre cadena "$d" "6:17+115,8:clic:818:54,10:27" "9:con-cadena" 12 ROCOLA_OS=1
  si "con cadena_nueva=1 la pestana Billar sale sola" bash -c "grep -q 'Rocola OS - Billar' '$o/traza.txt'"
fi

echo
if [ $mal -eq 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; exit 0; else echo "$mal MAL de $((bien + mal))"; exit 1; fi
