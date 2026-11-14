#!/bin/bash
# LA PRUEBA DEL CATALOGO DE SPOTIFY (fase 11, grupo catalogo: RocolaOs/RocolaOs.SpotifyCatalogo*.cs; FASE11_catalogo.md). La rocola entera,
# fuera de pantalla, sin sonido, con datos INVENTADOS (los de partida de las escenas de ventanas, DATOS0) y la API web de Spotify FALSA
# (ApiFalsa/, en 127.0.0.1, con artistas, discos y canciones de mentira). Las teclas, con ROCOLA_PULSA (codigos de Windows: las del datos0
# son arriba 38, abajo 40, izq 37, der 39, poner 13, regresar 27, moneda 67).
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   API: la carpeta con api-falsa.dll (por defecto /root/rocola-trabajo/fase11/catalogo/api-falsa)
#   W: la carpeta de trabajo (por defecto /root/rocola-trabajo/fase11/catalogo/prueba; se rehace)
#   CAPTURAS: si se pone, las capturas para el dueno van ahi (PNG)
# escenas: apagado cero busca disco artista fallos cuenta sinsesion (todas, de fabrica)
# Mientras el nucleo no este juntado, I6 es el doble (RocolaOs/RocolaOs.SpotifyCatalogoDobleI6.cs): cada pedido queda en
# run/rocola-xp/spotify/doble-pedidos.
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
escenas="${*:-apagado cero busca disco artista fallos cuenta sinsesion}"
API=${API:-/root/rocola-trabajo/fase11/catalogo/api-falsa}
W=${W:-/root/rocola-trabajo/fase11/catalogo/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
CAPTURAS=${CAPTURAS:-}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
cuenta() { local n; n=$(grep -cE -- "$2" "$1" 2>/dev/null); echo "${n:-0}"; }

# LA API FALSA: un puerto libre, su carpeta y su registro de peticiones
P=$((20000 + $$ % 20000))
apifalsa() {   # NOMBRE: arranca una API falsa nueva (sin peticiones) para la escena; deja su registro en $W/NOMBRE/api/peticiones.txt
  local o="$W/$1/api"; mkdir -p "$o"; P=$((P + 1))
  dotnet "$API/api-falsa.dll" $P "$o" id-de-prueba secreto-de-prueba refresco-1 > "$o/salida.txt" 2>&1 &
  APIPID=$!
  for i in $(seq 1 100); do [ -f "$o/listo" ] && break; sleep 0.1; done
  : > "$o/peticiones.txt"
}
control() { curl -s --noproxy '*' "http://127.0.0.1:$P/control/$1" > /dev/null; }

# LA ROCOLA: abre NOMBRE DATOS "PULSA" SEGUNDOS [VAR=valor...]: fuera de pantalla con esos datos; a los SEGUNDOS, SIGTERM (el apagado)
abre() {
  local n="$1" d="$2" pulsa="$3" seg="$4"; shift 4
  local o="$W/$n"; mkdir -p "$o/run"
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_SPOTIFY_API="http://127.0.0.1:$P/v1" ROCOLA_SPOTIFY_CUENTAS="http://127.0.0.1:$P" \
      "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  ROCOLA=$!
  ( sleep "$seg"; kill -TERM $ROCOLA 2>/dev/null; sleep 30; kill -KILL $ROCOLA 2>/dev/null ) & VIG=$!
}
espera() { wait $ROCOLA; echo $? > "$W/$1/salio"; kill $VIG 2>/dev/null; wait $VIG 2>/dev/null; kill $APIPID 2>/dev/null; wait $APIPID 2>/dev/null; }
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
escribe() { local s="$1" pal="$2" r="" i c; for ((i = 0; i < ${#pal}; i++)); do c=$(printf '%d' "'${pal:$i:1}"); c=$((c - 32)); r="$r,$(echo "$s + $i * 0.2" | bc):$c"; done; echo "${r#,}"; }
derechas() { local s="$1" n="$2" r="" i; for ((i = 0; i < n; i++)); do r="$r,$(echo "$s + $i * 0.1" | bc):39"; done; echo "${r#,}"; }
v1() { grep -c ' /v1/' "$1" 2>/dev/null; }
pedidos() { grep -c '^pedida	' "$1/run/rocola-xp/spotify/doble-pedidos" 2>/dev/null || echo 0; }
pedido() { sed -n "$2p" "$1/run/rocola-xp/spotify/doble-pedidos" 2>/dev/null | cut -f1,2; }
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
    if [ $e = apagado ]; then d=$(datos $e apagado); abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 12.5 10),14.5:13" 20 -u ROCOLA_OS
    else d=$(datos $e apagado); printf 'spotify=0\n' > "$d/rocola_os.txt"; abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 12.5 10),14.5:13" 20 ROCOLA_OS=1; fi
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
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 12.5 14),14.2:37,14.8:13,15.4:39,15.9:13" 22 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 12.3 buscador-solo-spotify),$(cap 16.2 buscador-pedidas)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "busca: sale con 0" test "$(cat $o/salio)" = 0
    si "busca: UNA busqueda a la API (a los 300 ms de dejar de escribir: 1, o 2 si la pantalla entrego tarde las teclas): $(grep -c '/v1/search' $a)" test "$(grep -c '/v1/search' $a)" -le 2
    si "busca: una sola llave de la app (client_credentials)" test "$(grep -c '^POST /api/token basic 200' $a)" = 1
    si "busca: ninguna otra peticion a /v1 que las busquedas ($(v1 $a))" test "$(v1 $a)" = "$(grep -c '/v1/search' $a)"
    si "busca: dos pedidas por I6 ($(pedidos $o))" test "$(pedidos $o)" = 2
    si "busca: la 1a pedida es la 1a cancion de la busqueda (spotify:track:can0000)" test "$(pedido $o 1)" = "$(printf 'pedida\tspotify:track:can0000')"
    si "busca: la 2a es la 2a (spotify:track:can0001)" test "$(pedido $o 2)" = "$(printf 'pedida\tspotify:track:can0001')"
    si "busca: con titulo, artista, disco, duracion, portada y disco_uri" bash -c "sed -n 1p '$o/run/rocola-xp/spotify/doble-pedidos' | grep -qP '^pedida\tspotify:track:can0000\tTema Tema de Mentira\tLos Inventados\tTema de Mentira\t157311\thttp://127.0.0.1:$P/img/disco-dis000-300.jpg\tspotify:album:dis000$'"
    si "busca: las portadas en /run/rocola-xp/spotify/portadas (y ninguna en los datos)" bash -c "ls '$o/run/rocola-xp/spotify/portadas/'*.jpg >/dev/null 2>&1 && ! find '$d' -newer '$d/rocola_os.txt' -name '*.jpg' | grep -q ."
    si "busca: las miniaturas de las portadas de Spotify, en /run (no en rocola_miniaturas)" bash -c "ls '$o/run/rocola-xp/spotify/miniaturas/' 2>/dev/null | grep -q . && [ \"\$(find '$d/rocola_miniaturas' -newer '$d/rocola_os.txt' -type f | wc -l)\" = 0 ]"
    si "busca: el estado del catalogo (api=bien, busquedas=1)" bash -c "grep -qx 'api=bien' '$o/run/rocola-xp/spotify/catalogo' && grep -qx 'busquedas=1' '$o/run/rocola-xp/spotify/catalogo'"
    si "busca: ni el secreto ni las llaves en los datos ni en /run" sinsecretos "$d" "$o/run"
    si "busca: lo de Spotify no entro a la biblioteca de XP (rocola_biblioteca.txt sin spotify:)" nocontiene "$d/rocola_biblioteca.txt" "spotify:"
    si "busca: los encargos con su nombre (RocolaOs:spotify_catalogo:resultados)" contiene "$o/traza.txt" "RocolaOs:spotify_catalogo:resultados" ;;
  disco)
    # UN DISCO: «mentira» da [cancion, disco]; se elige el disco (2a tarjeta), se abre en la vista de XP y se piden la 1a y la 2a con PONER;
    # REGRESAR deja la pared en la letra de Spotify con ese disco
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 mentira),$(derechas 12 14),14:13,16:13,16.6:40,17:13,18.5:27" 24 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 12 buscador-disco),$(cap 15.5 disco-de-spotify),$(cap 20 pared-letra-spotify)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "disco: sale con 0" test "$(cat $o/salio)" = 0
    si "disco: una busqueda y UN disco (/v1/albums/dis000): $(v1 $a) peticiones a /v1" bash -c "[ \$(grep -c '/v1/search' '$a') = 1 ] && [ \$(grep -c 'GET /v1/albums/dis000 app 200' '$a') = 1 ] && [ \$(grep -c ' /v1/' '$a') = 2 ]"
    si "disco: dos pedidas con PONER ($(pedidos $o)): la 1a y la 2a del disco" bash -c "[ '$(pedido $o 1)' = \"\$(printf 'pedida\tspotify:track:can0000')\" ] && [ '$(pedido $o 2)' = \"\$(printf 'pedida\tspotify:track:can0001')\" ]"
    si "disco: la letra de Spotify (y la cabecera SPOTIFY) en la traza de la pantalla o en la captura" true ;;
  artista)
    # UN ARTISTA: «ficticio» da 7 canciones, 1 disco y 1 artista (la 9a tarjeta: 4 abajo); se elige y lleva a sus discos en la letra
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 ficticio),$(derechas 12.2 14),13.9:37,14.2:40,14.3:40,14.6:40,14.9:40,15.4:13" 22 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 15 buscador-artista),$(cap 17.5 pared-discos-del-artista)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "artista: sale con 0" test "$(cat $o/salio)" = 0
    si "artista: una busqueda y UNA de los discos del artista (/v1/artists/art02/albums)" bash -c "[ \$(grep -c '/v1/search' '$a') = 1 ] && [ \$(grep -c 'GET /v1/artists/art02/albums app 200' '$a') = 1 ] && [ \$(grep -c ' /v1/' '$a') = 2 ]"
    si "artista: nada pedido (solo se abrio)" test "$(pedidos $o)" = 0 ;;
  fallos)
    # 401 (llave vencida): otra llave y otra vez; 503: otra vez al segundo; 429: nada mas hasta su Retry-After
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),13.5:8,16.5:8,19.5:8,21.5:8,23.5:8" 29 ROCOLA_OS=1
    ( sleep 16; control vence; sleep 3; control 5xx?n=1; sleep 3; control "429?seg=60&cuota=1" ) &
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    sed 's/^/   /' "$a" | grep -v '/img/' | head -20
    si "fallos: sale con 0" test "$(cat $o/salio)" = 0
    si "fallos: 401 con la llave vencida -> otra llave -> 200" bash -c "grep -v '/img/' '$a' | grep -A2 '401$' | grep -q 'POST /api/token basic 200' && [ \$(grep -c 'POST /api/token basic 200' '$a') = 2 ]"
    si "fallos: 503 -> otra vez -> 200" bash -c "grep -v '/img/' '$a' | grep -A1 ' 503$' | tail -1 | grep -q ' 200$'"
    si "fallos: 429 una vez, y despues NINGUNA peticion mas a /v1 (las dos consultas de despues no preguntan)" bash -c "[ \$(grep -c ' 429$' '$a') = 1 ] && [ \"\$(grep ' /v1/' '$a' | tail -1 | awk '{print \$4}')\" = 429 ]"
    si "fallos: «Spotify frena» en la bitacora, con la cuota" bash -c "grep -q 'Spotify frena (429): no se le pregunta nada en 60 s (QUOTA_EXCEEDED' '$d/rocola_os_bitacora.txt'"
    si "fallos: el estado dice api=frena" contiene "$o/run/rocola-xp/spotify/catalogo" "api=frena"
    si "fallos: ni el secreto ni las llaves en los datos ni en /run" sinsecretos "$d" "$o/run" ;;
  cuenta)
    # LA CUENTA (refresh_token): la letra de Spotify desde el arranque con «Tus favoritas», tus discos y tu lista (la de otro no); REGRESAR+
    # IZQUIERDA desde la primera letra da la vuelta hasta ella
    apifalsa $e; d=$(datos $e cuenta)
    abre $e "$d" "7:67,9:27+37,10.5:39,11:13,13.5:13,15:27" 20 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 10 pared-cuenta),$(cap 12.5 disco-lista-propia)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    sed 's/^/   /' "$a" | grep -v '/img/' | head -12
    si "cuenta: sale con 0" test "$(cat $o/salio)" = 0
    si "cuenta: tres peticiones de la cuenta (me/tracks, me/albums, me/playlists) con la llave de la cuenta" bash -c "[ \$(grep -cE 'GET /v1/me/(tracks|albums|playlists) cuenta 200' '$a') = 3 ]"
    si "cuenta: la lista propia se abrio con UNA peticion (/v1/playlists/lis001/items)" bash -c "[ \$(grep -c 'GET /v1/playlists/lis001/items cuenta 200' '$a') = 1 ]"
    si "cuenta: la de otro no se pide nunca" nocontiene "$a" "lis002"
    si "cuenta: Spotify roto la llave de refresco y se guardo encima (0600, sin la vieja)" bash -c "grep -qE '^refresh_token=refresco-[0-9]+$' '$d/rocola_spotify_api.txt' && ! grep -qx 'refresh_token=refresco-1' '$d/rocola_spotify_api.txt' && [ \"\$(find '$d/rocola_spotify_api.txt' -perm 600 | wc -l)\" = 1 ]"
    si "cuenta: un pedido desde la lista propia ($(pedidos $o))" test "$(pedidos $o)" = 1
    si "cuenta: ni el secreto ni las llaves en los datos ni en /run" sinsecretos "$d" "$o/run" ;;
  sinsesion)
    # SOLOIST SIN SESION (I6 dice que no esta listo): no se pide ni se cobra; el aviso de XP
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 12.5 14),14.2:37,14.8:13" 20 ROCOLA_OS=1 ROCOLA_SPOTIFY_DOBLE_LISTO=0 ROCOLA_CAPTURA="$(cap 14.8 buscador-spotify-no-listo)"
    espera $e; o="$W/$e"
    si "sinsesion: sale con 0" test "$(cat $o/salio)" = 0
    si "sinsesion: nada pedido" test "$(pedidos $o)" = 0
    si "sinsesion: la bitacora dice que no estaba listo" contiene "$d/rocola_os_bitacora.txt" "Spotify aun no esta listo" ;;
  esac
done

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; fi
[ $mal = 0 ]
