#!/bin/bash
# LA PRUEBA DEL CATALOGO DE SPOTIFY (fase 11, grupo catalogo: RocolaOs/RocolaOs.SpotifyCatalogo*.cs; FASE11_catalogo.md). La rocola entera,
# fuera de pantalla, sin sonido, con datos INVENTADOS (los de partida de las escenas de ventanas, DATOS0) y la API web de Spotify FALSA
# (ApiFalsa/, en 127.0.0.1, con artistas, discos y canciones de mentira). Las teclas, con ROCOLA_PULSA (codigos de Windows: las del datos0
# son arriba 38, abajo 40, izq 37, der 39, poner 13, regresar 27, moneda 67).
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   API: la carpeta con api-falsa.dll (por defecto /root/rocola-trabajo/fase11/catalogo/api-falsa; se compila si falta o si cambio)
#   W: la carpeta de trabajo (por defecto /root/rocola-trabajo/fase11/catalogo/prueba; se rehace)
#   CAPTURAS: si se pone, las capturas para el dueno van ahi (PNG)
# escenas: apagado cero busca mezcla disco artista fallos cuenta sinsesion suena llavemala reabre sinred (todas, de fabrica)
# I6 es el NUCLEO de verdad (grupo spotify-xp, juntado: RocolaOs/RocolaOs.SpotifyPide.cs): las pedidas se cuentan por sus lineas «COBRADA» en
# su bitacora (rocola_spotify.txt) y por los «play» que le llegan al Soloist FALSO del nucleo (Pruebas/RocolaOs/spotify-xp/SoloistFalso, abierto
# por su vigilante falso; NUNCA el Soloist de verdad). FALSO: la carpeta del Soloist falso compilado (se compila si falta, con el candado).
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
escenas="${*:-apagado cero busca mezcla disco artista fallos cuenta sinsesion suena llavemala reabre sinred}"
API=${API:-/root/rocola-trabajo/fase11/catalogo/api-falsa}
W=${W:-/root/rocola-trabajo/fase11/catalogo/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
CAPTURAS=${CAPTURAS:-}
FALSO=${FALSO:-/root/rocola-trabajo/fase11/catalogo/falso}
if [ ! -f "$API/api-falsa.dll" ] || [ "$G/ApiFalsa/Program.cs" -nt "$API/api-falsa.dll" ]; then
  echo "(compilando la API falsa en $API)"; mkdir -p "$API"
  flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$G/ApiFalsa/ApiFalsa.csproj" -c Release -o "$API" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$API/obj/" > "$API/build.txt" 2>&1 || { cat "$API/build.txt"; exit 1; }
fi
SX="$G/../spotify-xp"
# la tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c, la del nucleo): suena a su ritmo (con «null» una cancion de 30 s se acaba en 2)
AL=$(dirname "$FALSO")/alsa; mkdir -p "$AL"
[ -f "$AL/libasound_module_pcm_reloj.so" ] || gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
if [ ! -f "$FALSO/soloist-falso.dll" ] || [ "$SX/SoloistFalso/Program.cs" -nt "$FALSO/soloist-falso.dll" ]; then
  echo "(compilando el Soloist falso en $FALSO)"; mkdir -p "$FALSO"
  flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$SX/SoloistFalso/SoloistFalso.csproj" -c Release -o "$FALSO" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$FALSO/obj/" > "$FALSO/build.txt" 2>&1 || { cat "$FALSO/build.txt"; exit 1; }
fi
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
cuenta() { local n; n=$(grep -cE -- "$2" "$1" 2>/dev/null); echo "${n:-0}"; }

# LA API FALSA: un puerto libre, su carpeta y su registro de peticiones
P=$((20000 + $$ % 20000))
apifalsa() {   # NOMBRE [MISMO]: arranca una API falsa nueva (sin peticiones) para la escena; deja su registro en $W/NOMBRE/api/peticiones.txt
  local o="$W/$1/api"; mkdir -p "$o"; [ -n "${2:-}" ] || P=$((P + 1))   # (con MISMO, en el puerto de antes: el que ya tiene la rocola)
  dotnet "$API/api-falsa.dll" $P "$o" id-de-prueba secreto-de-prueba refresco-1 > "$o/salida.txt" 2>&1 &
  APIPID=$!
  for i in $(seq 1 100); do [ -f "$o/listo" ] && break; sleep 0.1; done
  : > "$o/peticiones.txt"
}
control() { curl -s --noproxy '*' "http://127.0.0.1:$P/control/$1" > /dev/null; }

# LA ROCOLA: abre NOMBRE DATOS "PULSA" SEGUNDOS [VAR=valor...]: fuera de pantalla con esos datos; a los SEGUNDOS, SIGTERM (el apagado)
abre() {
  local n="$1" d="$2" pulsa="$3" seg="$4"; shift 4
  local o="$W/$n"; mkdir -p "$o/run" "$o/soloist"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" > "$o/alsa.conf"
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_SPOTIFY_API="http://127.0.0.1:$P/v1" ROCOLA_SPOTIFY_CUENTAS="http://127.0.0.1:$P" \
      ROCOLA_SOLOIST_DATOS="$o/soloist" ROCOLA_SOLOIST_BIN="$SX/vigilante-falso.sh" SOLOIST_FALSO_BIN="$FALSO" \
      SOLOIST_FALSO_LOG="$o/falso.txt" VIGILANTE_FALSO_LOG="$o/vigilante.txt" \
      "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  ROCOLA=$!
  ( sleep "$seg"; kill -TERM $ROCOLA 2>/dev/null; sleep 30; kill -KILL $ROCOLA 2>/dev/null ) & VIG=$!
}
espera() { wait $ROCOLA; echo $? > "$W/$1/salio"; [ "$1" = suena ] || rm -f "$W/$1/sonado.raw"; kill $VIG 2>/dev/null; wait $VIG 2>/dev/null; kill $APIPID 2>/dev/null; wait $APIPID 2>/dev/null; }
datos() {   # NOMBRE MODO(apagado|encendido|cuenta): los datos de partida con lo de la escena
  local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/
  case "$2" in
    encendido) printf 'spotify=1\n' > "$d/rocola_os.txt"; printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\n' > "$d/rocola_spotify_api.txt" ;;
    cuenta) printf 'spotify=1\n' > "$d/rocola_os.txt"; printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\nrefresh_token=refresco-1\nusuario=dueno-falso\n' > "$d/rocola_spotify_api.txt" ;;
    apagado) printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\n' > "$d/rocola_spotify_api.txt" ;;
  esac
  [ -f "$d/rocola_spotify_api.txt" ] && chmod 600 "$d/rocola_spotify_api.txt"
  echo "$d"
}
cap() { [ -n "$CAPTURAS" ] && echo "$1:$CAPTURAS/$2.png" || echo "$1:$W/$2.png"; }
[ -n "$CAPTURAS" ] && mkdir -p "$CAPTURAS"
# teclas: «inventad» y otras palabras, desde el segundo S, una letra cada 0,2 s
escribe() { local s="$1" pal="$2" r="" i c; for ((i = 0; i < ${#pal}; i++)); do c=$(printf '%d' "'${pal:$i:1}"); c=$((c - 32)); r="$r,$(echo "$s + $i * 0.25" | bc):$c"; done; echo "${r#,}"; }
derechas() { local s="$1" n="$2" r="" i; for ((i = 0; i < n; i++)); do r="$r,$(echo "$s + $i * 0.15" | bc):39"; done; echo "${r#,}"; }
v1() { grep -c ' /v1/' "$1" 2>/dev/null; }
# las pedidas que entraron (I6 del nucleo): sus «COBRADA» en la bitacora del nucleo, y los «play» que recibio el Soloist falso
cobradas() { grep -F 'COBRADA spotify:track:' "$W/datos-$(basename "$1")/rocola_spotify.txt" 2>/dev/null; }
pedidos() { cobradas "$1" | wc -l; }
pedido() { cobradas "$1" | sed -n "$2p" | sed 's/.*COBRADA \(spotify:track:[A-Za-z0-9]*\).*/pedida	\1/'; }
plays() { grep -P '	orden	' "$1/falso.txt" 2>/dev/null | grep '"play"' | grep -o 'spotify:track:[A-Za-z0-9]*' | tr '\n' ' '; }
export -f cobradas plays; export W
sinsecretos() {   # ninguna llave ni el secreto en lo que escribe la rocola (datos y /run), salvo en el propio archivo de la llave
  ! grep -rlE 'secreto-de-prueba|app-[0-9]+|cuenta-[0-9]+|refresco-[0-9]+' "$1" "$2" 2>/dev/null | grep -v 'rocola_spotify_api.txt' | grep -q .
}
# lo que hace la rocola en todas: moneda a los 7 s, abre el buscador a los 8 s (REGRESAR+ARRIBA) y escribe desde los 9,5 s
BASE="7:67,8:27+38"

for e in $escenas; do
  echo "== $e"
  case "$e" in
  apagado|cero)
    # SIN EL MODO: sin ROCOLA_OS (apagado) y con ROCOLA_OS=1 y spotify=0 (cero): ni una peticion, ni la letra, ni nada en /run
    apifalsa $e
    if [ $e = apagado ]; then d=$(datos $e apagado); abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 14 14),17:13" 22 ROCOLA_OS=
    else d=$(datos $e apagado); printf 'spotify=0\n' > "$d/rocola_os.txt"; abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 14 14),17:13" 22 ROCOLA_OS=1; fi
    espera $e; o="$W/$e"
    si "$e: sale con 0" test "$(cat $o/salio)" = 0
    si "$e: ninguna peticion a la API falsa (ni la llave)" test "$(wc -l < $o/api/peticiones.txt)" = 0
    si "$e: nada de Spotify en /run" test ! -e "$o/run/rocola-xp/spotify"
    si "$e: en la traza nada de RocolaOs:spotify" nocontiene "$o/traza.txt" "spotify"
    if [ $e = cero ]; then
      R=${RUIDO_SIGTERM:-/root/rocola-trabajo/port/ventanas/ruido/sigterm.json}
      python3 "$XP/Pruebas/Ventanas/guiones/compara_trazas.py" "$W/apagado/traza.txt" "$o/traza.txt" --ignora RocolaOs: --holgura 80 > "$o/compara.txt" 2>&1; r=$?
      sed 's/^/   /' "$o/compara.txt" | head -8
      si "cero: la traza igual a la de sin ROCOLA_OS quitando lo de RocolaOs (compara_trazas.py --ignora, holgura 80)" test $r = 0
    fi ;;
  busca)
    # EL BUSCADOR: «inventad» (XP no tiene nada): las de Spotify llegan, se entra a las tarjetas y se piden la 1a y la 2a
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 14 14),16.6:37,17.4:13,18.4:39,19.2:13" 25 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 13.8 buscador-solo-spotify),$(cap 20.5 buscador-pedidas)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "busca: sale con 0" test "$(cat $o/salio)" = 0
    si "busca: las busquedas a la API (desde 3 letras, a los 300 ms de dejar de escribir): $(grep -c '/v1/search' $a), la ultima con todo lo escrito" bash -c "[ \$(grep -c '/v1/search' '$a') -le 4 ] && grep '/v1/search' '$a' | tail -1 | grep -q ' q=inventad$'"
    si "busca: una sola llave de la app (client_credentials)" test "$(grep -c '^POST /api/token basic 200' $a)" = 1
    si "busca: ninguna otra peticion a /v1 que las busquedas ($(v1 $a))" test "$(v1 $a)" = "$(grep -c '/v1/search' $a)"
    si "busca: dos pedidas por I6 ($(pedidos $o))" test "$(pedidos $o)" = 2
    si "busca: la 1a pedida es la 1a cancion de la busqueda (spotify:track:can0000)" test "$(pedido $o 1)" = "$(printf 'pedida\tspotify:track:can0000')"
    si "busca: la 2a es la 2a (spotify:track:can0001)" test "$(pedido $o 2)" = "$(printf 'pedida\tspotify:track:can0001')"
    si "busca: la 1a con su titulo y su artista en la bitacora del nucleo (1 credito, su turno)" bash -c "cobradas $o | sed -n 1p | grep -qF 'COBRADA spotify:track:can0000 «Tema Tema de Mentira» de «Los Inventados»: 1 credito, '"
    si "busca: al Soloist falso le llego el play de la 1a ($(plays $o))" bash -c "[ \"\$(plays $o | cut -d' ' -f1)\" = spotify:track:can0000 ]"
    si "busca: las portadas en /run/rocola-xp/spotify/portadas (y ninguna en los datos)" bash -c "ls '$o/run/rocola-xp/spotify/portadas/'*.jpg >/dev/null 2>&1 && ! find '$d' -newer '$d/rocola_os.txt' -name '*.jpg' | grep -q ."
    si "busca: las miniaturas de las portadas de Spotify, en /run (no en rocola_miniaturas)" bash -c "ls '$o/run/rocola-xp/spotify/miniaturas/' 2>/dev/null | grep -q . && [ \"\$(find '$d/rocola_miniaturas' -newer '$d/rocola_os.txt' -type f | wc -l)\" = 0 ]"
    si "busca: el estado del catalogo (api=bien, y las busquedas que vio la API falsa)" bash -c "grep -qx 'api=bien' '$o/run/rocola-xp/spotify/catalogo' && grep -qx \"busquedas=\$(grep -c '/v1/search' '$a')\" '$o/run/rocola-xp/spotify/catalogo'"
    si "busca: ni el secreto ni las llaves en los datos ni en /run" sinsecretos "$d" "$o/run"
    si "busca: lo de Spotify no entro a la biblioteca de XP (rocola_biblioteca.txt sin spotify:)" nocontiene "$d/rocola_biblioteca.txt" "spotify:"
    si "busca: los encargos con su nombre (RocolaOs:spotify_catalogo:resultados)" contiene "$o/traza.txt" "RocolaOs:spotify_catalogo:resultados" ;;
  mezcla)
    # LO DE XP Y LO DE SPOTIFY JUNTOS: «tema» (XP tiene «Tema 1..3» de Otra Banda; la API falsa, canciones y discos con «Tema»): las de XP
    # primero y en su orden, despues las de Spotify; la casilla cuenta todas
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 tema)" 17 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 13.5 buscador-xp-y-spotify)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "mezcla: sale con 0" test "$(cat $o/salio)" = 0
    si "mezcla: una o dos busquedas, la ultima «tema»" bash -c "[ \$(grep -c '/v1/search' '$a') -le 2 ] && grep '/v1/search' '$a' | tail -1 | grep -q ' q=tema$'" ;;
  disco)
    # UN DISCO: «mentira» da [cancion, disco]; se elige el disco (2a tarjeta), se abre en la vista de XP y se piden la 1a y la 2a con PONER;
    # REGRESAR deja la pared en la letra de Spotify con ese disco
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 mentira),$(derechas 13.5 14),16.5:13,19:13,20:40,21:13,22.5:27" 28 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 16 buscador-disco),$(cap 20.7 disco-de-spotify),$(cap 24.5 pared-letra-spotify)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "disco: sale con 0" test "$(cat $o/salio)" = 0
    si "disco: las busquedas y UN disco (/v1/albums/dis000): $(v1 $a) peticiones a /v1" bash -c "[ \$(grep -c 'GET /v1/albums/dis000 app 200' '$a') = 1 ] && [ \$(grep -c ' /v1/' '$a') = \$((\$(grep -c '/v1/search' '$a') + 1)) ]"
    si "disco: dos pedidas con PONER ($(pedidos $o)): la 1a y la 2a del disco" bash -c "[ '$(pedido $o 1)' = \"\$(printf 'pedida\tspotify:track:can0000')\" ] && [ '$(pedido $o 2)' = \"\$(printf 'pedida\tspotify:track:can0001')\" ]"
    si "disco: solo esas dos pedidas" test "$(pedidos $o)" = 2 ;;
  artista)
    # UN ARTISTA: «imaginario» da 8 canciones, 2 discos y 1 artista (la 11a tarjeta: 5 abajo); se elige y lleva a sus discos en la letra
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 imaginario),$(derechas 14 14),16.5:37,17:40,17.4:40,17.8:40,18.2:40,18.6:40,19.5:13" 25 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 19.2 buscador-artista),$(cap 22 pared-discos-del-artista)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "artista: sale con 0" test "$(cat $o/salio)" = 0
    si "artista: las busquedas, UNA de los discos del artista (/v1/artists/art03/albums) y UNA del disco que se queda elegido en la pared (g52: /v1/albums/dis006)" bash -c "[ \$(grep -c 'GET /v1/artists/art03/albums app 200' '$a') = 1 ] && [ \$(grep -c 'GET /v1/albums/dis006 app 200' '$a') = 1 ] && [ \$(grep -c ' /v1/' '$a') = \$((\$(grep -c '/v1/search' '$a') + 2)) ]"
    si "artista: nada pedido (solo se abrio)" test "$(pedidos $o)" = 0 ;;
  fallos)
    # 401 (llave vencida): otra llave y otra vez; 503: otra vez al segundo; 429: nada mas hasta su Retry-After. La API falsa cambia de
    # humor segun lo que ya contesto (no por reloj): vence las llaves tras «inventad», da 503 tras «inventa» y 429 tras «invent»
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),15:8,18.5:8,22:8,25:8,27:8" 31 ROCOLA_OS=1
    a="$W/$e/api/peticiones.txt"
    ( t=0; paso() { until grep -q "$1" "$a" 2>/dev/null || [ $t -gt 400 ]; do sleep 0.1; t=$((t + 1)); done; }
      paso ' 200 q=inventad$'; control vence; paso ' 200 q=inventa$'; control 5xx?n=1; paso ' 200 q=invent$'; control "429?seg=60&cuota=1" ) &
    espera $e; o="$W/$e"
    grep -v '/img/' "$a" | sed 's/^/   /' | head -20
    si "fallos: sale con 0" test "$(cat $o/salio)" = 0
    si "fallos: 401 con la llave vencida (inventa) -> otra llave -> la misma otra vez, 200" bash -c "grep -v '/img/' '$a' | grep -A2 ' 401 q=inventa$' | tr '\n' '|' | grep -q 'POST /api/token basic 200|GET /v1/search app 200 q=inventa|' && [ \$(grep -c 'POST /api/token basic 200' '$a') = 2 ]"
    si "fallos: 503 (invent) -> la misma otra vez al segundo, 200" bash -c "grep -v '/img/' '$a' | grep -A1 ' 503 q=invent$' | tail -1 | grep -q ' 200 q=invent$'"
    si "fallos: 429 una vez (inven), y despues NINGUNA peticion mas a /v1 (inve e inv no preguntan)" bash -c "[ \$(grep -c ' 429 ' '$a') = 1 ] && grep ' /v1/' '$a' | tail -1 | grep -q ' 429 q=inven$'"
    si "fallos: «Spotify frena» en la bitacora, con la cuota" bash -c "grep -q 'Spotify frena (429): no se le pregunta nada en 60 s (QUOTA_EXCEEDED' '$d/rocola_os_bitacora.txt'"
    si "fallos: el estado dice api=frena (y frena_s con la cuota)" bash -c "grep -qx 'api=frena' '$o/run/rocola-xp/spotify/catalogo' && grep -qE '^frena_s=[0-9]+ cuota$' '$o/run/rocola-xp/spotify/catalogo'"
    si "fallos: ni el secreto ni las llaves en los datos ni en /run" sinsecretos "$d" "$o/run" ;;
  cuenta)
    # LA CUENTA (refresh_token): la letra de Spotify desde el arranque con «Tus favoritas», tus discos y tu lista (la de otro no); REGRESAR+
    # IZQUIERDA desde la primera letra da la vuelta hasta ella
    apifalsa $e; d=$(datos $e cuenta)
    abre $e "$d" "7:67,10:27+37,12:39,12.6:39,13.2:39,14:13,16.5:13,18:27" 22 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 11.5 pared-cuenta),$(cap 16 disco-lista-propia)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    sed 's/^/   /' "$a" | grep -v '/img/' | head -12
    si "cuenta: sale con 0" test "$(cat $o/salio)" = 0
    si "cuenta: tres peticiones de la cuenta (me/tracks, me/albums, me/playlists) con la llave de la cuenta" bash -c "[ \$(grep -cE 'GET /v1/me/(tracks|albums|playlists) cuenta 200' '$a') = 3 ]"
    si "cuenta: la lista propia se abrio con UNA peticion (/v1/playlists/lis001/items)" bash -c "[ \$(grep -c 'GET /v1/playlists/lis001/items cuenta 200' '$a') = 1 ]"
    si "cuenta: la de otro no se pide nunca" nocontiene "$a" "lis002"
    si "cuenta: Spotify roto la llave de refresco y se guardo encima (0600, sin la vieja)" bash -c "grep -qE '^refresh_token=refresco-[0-9]+$' '$d/rocola_spotify_api.txt' && ! grep -qx 'refresh_token=refresco-1' '$d/rocola_spotify_api.txt' && [ \"\$(find '$d/rocola_spotify_api.txt' -perm 600 | wc -l)\" = 1 ]"
    si "cuenta: un pedido desde la lista propia ($(pedidos $o))" test "$(pedidos $o)" = 1
    si "cuenta: la llave de la cuenta se renovo como PKCE (client_id en el cuerpo, sin el secreto)" bash -c "grep -q '^POST /api/token nada 200' '$a' && ! grep -q '^POST /api/token nada 400' '$a'"
    si "cuenta: ni el secreto ni las llaves en los datos ni en /run" sinsecretos "$d" "$o/run" ;;
  suena)
    # LO QUE SUENA CON UNA DE SPOTIFY: se pide la 1a de «inventad», se cierra el buscador y la barra de abajo de XP dice lo que suena. Con la
    # tarjeta de prueba con reloj: suena a su ritmo y no se acaba en un momento
    apifalsa $e; d=$(datos $e encendido); o="$W/$e"
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 14 14),16.6:37,17.4:13,19.5:27+38" 25 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 19.2 buscador-sonando-spotify),$(cap 22.5 suena-spotify)"
    espera $e
    si "suena: sale con 0" test "$(cat $o/salio)" = 0
    si "suena: una pedida, y Soloist la toca ($(plays $o))" bash -c "[ $(pedidos $o) = 1 ] && [ \"\$(plays $o)\" = 'spotify:track:can0000 ' ]"
    si "suena: la pedida va con la portada que bajo el catalogo (una sola por disco: dis000.jpg)" bash -c "cobradas $o | grep -q 'COBRADA spotify:track:can0000 .*; con portada' && [ -f '$o/run/rocola-xp/spotify/portadas/dis000.jpg' ] && [ ! -e '$o/run/rocola-xp/spotify/portadas/can0000.jpg' ]"
    si "suena: el nucleo dice que suena (y no que acabo)" bash -c "grep -qF 'suena spotify:track:can0000' '$d/rocola_spotify.txt' && ! grep -qF 'acabo spotify:track:can0000' '$d/rocola_spotify.txt'"
    si "suena: por la tarjeta salio sonido (sonado.raw de mas de 1 s)" test "$(cat "$o/sonado.raw" 2>/dev/null | wc -c)" -gt 352800; rm -f "$o/sonado.raw"
    si "suena: nada de Spotify en los datos de XP (solo en rocola_spotify.txt, la bitacora del nucleo, y la llave)" bash -c "! grep -rlE 'spotify:|can0000|dis000|Inventados' '$d' | grep -vE '/(rocola_spotify\.txt|rocola_spotify_api\.txt)$' | grep -q ." ;;
  llavemala)
    # LLAVES MALAS: client_secret que Spotify rechaza (400 invalid_client): se pide UNA vez y no otra con el mismo archivo, aunque se busque
    # mas; XP sigue igual. Al poner el bueno (el archivo cambia) se pide otra vez y se busca (y cuenta: una sola llave buena)
    apifalsa $e; d=$(datos $e encendido); printf 'client_id=id-de-prueba\nclient_secret=secreto-malo\n' > "$d/rocola_spotify_api.txt"; chmod 600 "$d/rocola_spotify_api.txt"
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(escribe 13 x),$(escribe 14 y),20:8,20.5:8,$(escribe 21 o)" 26 ROCOLA_OS=1
    ( sleep 16.5; printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\n' > "$d/rocola_spotify_api.txt.n"; chmod 600 "$d/rocola_spotify_api.txt.n"; mv "$d/rocola_spotify_api.txt.n" "$d/rocola_spotify_api.txt" ) &
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    grep -v '/img/' "$a" | sed 's/^/   /' | head -8
    si "llavemala: sale con 0" test "$(cat $o/salio)" = 0
    si "llavemala: la llave mala se pidio UNA sola vez ($(grep -c 'POST /api/token basic 400' $a)) aunque se busco 6 veces" test "$(grep -c 'POST /api/token basic 400' $a)" = 1
    si "llavemala: con la mala, ninguna peticion a /v1" bash -c "! sed '/POST \/api\/token basic 200/,\$d' '$a' | grep -q ' /v1/'"
    si "llavemala: la bitacora dice el 400 una vez, y en las 6 busquedas no se llena ($(grep -c 'no valen' $d/rocola_os_bitacora.txt) renglones)" bash -c "[ \$(grep -c '\] la llave no se dio (400: client_id o client_secret no valen)' '$d/rocola_os_bitacora.txt') = 1 ] && [ \$(grep -c 'no valen' '$d/rocola_os_bitacora.txt') -le 3 ]"
    si "llavemala: con el archivo bueno, una llave (200) y la busqueda (200)" bash -c "[ \$(grep -c 'POST /api/token basic 200' '$a') = 1 ] && grep -q 'GET /v1/search app 200' '$a'"
    si "llavemala: ni el secreto ni las llaves en los datos ni en /run" bash -c "! grep -rlE 'secreto-malo|secreto-de-prueba|app-[0-9]+' '$d' '$o/run' 2>/dev/null | grep -v 'rocola_spotify_api.txt' | grep -q ." ;;
  reabre)
    # EL 429 VALE TAMBIEN SI ROCOLA-XP SE VUELVE A ABRIR (se cayo y el lanzador la abrio): la 1a vez la API contesta 429 con Retry-After como
    # FECHA (RFC 9110) de dentro de 120 s; la 2a vez (otro rocola-xp, el mismo /run) no le pregunta nada aunque se busque
    apifalsa $e; d=$(datos $e encendido); control "429?seg=120&cuota=1&fecha=1"
    abre $e "$d" "$BASE,$(escribe 9.5 inventad)" 15 ROCOLA_OS=1
    wait $ROCOLA; echo $? > "$W/$e/salio1"; kill $VIG 2>/dev/null; wait $VIG 2>/dev/null
    a="$W/$e/api/peticiones.txt"; antes=$(v1 $a)
    abre $e "$d" "$BASE,$(escribe 9.5 imaginario),15:8,15.5:8,$(escribe 16 x)" 20 ROCOLA_OS=1
    espera $e; o="$W/$e"
    grep -v '/img/' "$a" | sed 's/^/   /' | head -6
    si "reabre: sale con 0 las dos veces" test "$(cat $o/salio1)$(cat $o/salio)" = 00
    si "reabre: la 1a vez, un 429 y nada mas a /v1 ($antes)" bash -c "[ $antes = 1 ] && grep -q ' 429 q=inv' '$a'"
    si "reabre: el plazo del 429 en /run (catalogo.frena, con la cuota)" bash -c "grep -qE '^[0-9]+ cuota$' '$o/run/rocola-xp/spotify/catalogo.frena'"
    si "reabre: la 2a vez, NINGUNA peticion a /v1 ni a la llave ($(v1 $a) a /v1 en total)" bash -c "[ \$(grep -c ' /v1/' '$a') = 1 ] && [ \$(grep -c 'POST /api/token' '$a') = 1 ]"
    si "reabre: la 2a vez la bitacora dice que sigue frenando (unos 100 s)" bash -c "grep -qE 'Spotify sigue frenando \(429 de antes de abrir\): no se le pregunta nada en (9[0-9]|1[01][0-9]) s \(QUOTA_EXCEEDED' '$d/rocola_os_bitacora.txt'"
    si "reabre: el estado dice api=frena con la cuota" bash -c "grep -qx 'api=frena' '$o/run/rocola-xp/spotify/catalogo' && grep -qE '^frena_s=[0-9]+ cuota$' '$o/run/rocola-xp/spotify/catalogo'" ;;
  sinred)
    # SIN INTERNET AL ARRANCAR (la API no contesta: nada escucha en su puerto) con la cuenta: XP sigue; lo buscado no traba ni llena la
    # bitacora (el mismo renglon una vez); lo de la cuenta se vuelve a intentar solo y, cuando vuelve la red (la API a los 12 s), llega
    d=$(datos $e cuenta); P=$((P + 1)); o="$W/$e"; mkdir -p "$o/api"; : > "$o/api/peticiones.txt"
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(escribe 11.4 x)" 46 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 11.8 buscador-sin-red)"
    sleep 12.5; apifalsa $e mismo
    espera $e; a="$o/api/peticiones.txt"
    grep -v '/img/' "$a" | sed 's/^/   /' | head -8
    si "sinred: sale con 0" test "$(cat $o/salio)" = 0
    si "sinred: sin red, «busqueda sin respuesta: sin red» UNA vez en la bitacora aunque se busco dos veces ($(grep -c 'busqueda sin respuesta: sin red' $d/rocola_os_bitacora.txt))" test "$(grep -c 'busqueda sin respuesta: sin red' $d/rocola_os_bitacora.txt)" = 1
    si "sinred: lo de la cuenta se vuelve a intentar solo" contiene "$d/rocola_os_bitacora.txt" "se vuelve a intentar solo"
    si "sinred: al volver la red, lo de la cuenta llego (me/tracks, me/albums, me/playlists: una vez cada una)" bash -c "[ \$(grep -cE 'GET /v1/me/(tracks|albums|playlists) cuenta 200' '$a') = 3 ]"
    si "sinred: la bitacora dice lo de la cuenta" contiene "$d/rocola_os_bitacora.txt" "lo de la cuenta: " ;;
  sinsesion)
    # SOLOIST SIN SESION (I6 dice que no esta listo: el vigilante sale al momento y no hay Soloist): no se pide ni se cobra; el aviso de XP
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 14 14),16.6:37,17.4:13" 22 ROCOLA_OS=1 ROCOLA_SOLOIST_BIN=/bin/true ROCOLA_CAPTURA="$(cap 17.8 buscador-spotify-no-listo)"
    espera $e; o="$W/$e"
    si "sinsesion: sale con 0" test "$(cat $o/salio)" = 0
    si "sinsesion: nada pedido" test "$(pedidos $o)" = 0
    si "sinsesion: la bitacora dice que no estaba listo" contiene "$d/rocola_os_bitacora.txt" "Spotify aun no esta listo" ;;
  esac
done

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; fi
[ $mal = 0 ]
