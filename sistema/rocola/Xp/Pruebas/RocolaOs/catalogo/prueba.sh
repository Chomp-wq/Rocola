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
# escenas: apagado cero busca mezcla disco artista fallos cuenta sinsesion suena llavemala reabre sinred (las del catalogo) y pared
#   canciones todos unida atajos pared429 (las de las paredes del buscador, grupo paredes-buscador: FASE11_paredes-buscador.md) y mantenido tiempo
#   (las de su revision: el rojo mantenido; la atraccion, la moneda y el buscador cerrado por tiempo) y hojea (la de la revision
#   cuota-hilos-y-fallos: hojear deprisa una pared de 17 paginas con la red lenta, y un 503 a media pared) y atajos503 (los atajos de SP9
#   que no llegaron por un 503 se vuelven a pedir al abrir el buscador pasado un minuto) y sinllave ayuda (la 2a revision funcionamiento-y-reglas:
#   el modo encendido sin llaves es XP; la ayuda con ROJO+ABAJO en medio de la pila) y miente portadas (la 2a pasada de cuota-hilos-y-fallos:
#   el «total» de search que dice de mas no hace pedir solo hasta el final; las portadas que no se bajaron por la red se vuelven a pedir);
#   y inactivo (22-11-2026: lo que se queda abierto del buscador se cierra solo) y cargando (23-11-2026: con Spotify lento el buscador no
#   ensena lo de antes: «Buscando…», el anillo de carga y «Cargando…», y todo junto al llegar) y siempre (23-11-2026: un disco o un artista tuyo elegido
#   en el buscador abre siempre el disco dentro del buscador o una pared del buscador, nunca la pared de verdad); todas, de fabrica
#   y colgada y recorte (24-11-2026, revision de mejoras: A3 la API que no contesta se recuerda «sin red» y no ocupa el hilo; A8 el recorte de /run); la escena
#   y sinanidar (26-11-2026: ni otro buscador ni la estrella dentro de lo del buscador: g279, g280); la escena
#   cuenta (A1) ya NO espera las peticiones de la cuenta (la letra ♫ no esta en la pared: ni se piden), y sinred prueba el «sin red» recordado
# (27-11-2026, las seis mejoras de Spotify que el dueno eligio: FASE11_paredes-buscador.md, ultima seccion, y FASE11_hoja-spotify.md) las escenas
#   repetidas (la misma cancion en tu musica y en Spotify sale una vez), ediciones (sin ediciones repetidas en la pared y «Canciones de» filtrado),
#   teclea (esperar mas tras la ultima letra), memoria (lo que contesto Spotify, en disco: 3 arranques) y hoja (la hoja «Spotify» de Ctrl+F4,
#   con su captura a 1024x768 y el mando de paginas por letra); usan /control/nuevas de la API falsa
# I6 es el NUCLEO de verdad (grupo spotify-xp, juntado: RocolaOs/RocolaOs.SpotifyPide.cs): las pedidas se cuentan por sus lineas «COBRADA» en
# su bitacora (rocola_spotify.txt) y por los «play» que le llegan al Soloist FALSO del nucleo (Pruebas/RocolaOs/spotify-xp/SoloistFalso, abierto
# por su vigilante falso; NUNCA el Soloist de verdad). FALSO: la carpeta del Soloist falso compilado (se compila si falta, con el candado).
# (junta 3, revision) con ROCOLA_OS=1 el cielo pide el lugar y el clima por internet sin llave, y la letra va a LRCLIB: en la prueba,
# todo eso va al sumidero 127.0.0.1:7 (nada escucha) para no depender de la red ni de lo que conteste un servicio de fuera; una escena
# que ponga su propio falso lo pone delante y manda
export ROCOLA_CLIMA_API="${ROCOLA_CLIMA_API:-http://127.0.0.1:7}" ROCOLA_LUGAR_API="${ROCOLA_LUGAR_API:-http://127.0.0.1:7/lugar}"
export ROCOLA_LETRA_API="${ROCOLA_LETRA_API:-http://127.0.0.1:7}" ROCOLA_GENIUS_API="${ROCOLA_GENIUS_API:-http://127.0.0.1:7}"
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
escenas="${*:-apagado cero busca mezcla disco artista fallos cuenta sinsesion suena llavemala reabre sinred pared canciones todos unida atajos pared429 mantenido tiempo hojea atajos503 sinllave ayuda miente portadas inactivo cargando siempre colgada recorte sinanidar repetidas ediciones teclea memoria hoja}"
API=${API:-/root/rocola-trabajo/fase11/catalogo/api-falsa}
W=${W:-/root/rocola-trabajo/fase11/catalogo/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
CAPTURAS=${CAPTURAS:-}
FALSO=${FALSO:-/root/rocola-trabajo/fase11/catalogo/falso}
# (se rehace si su Program.cs no es el de esta rama: la carpeta la comparten los agentes de varias ramas; antes contaba la fecha)
if [ ! -f "$API/api-falsa.dll" ] || ! cmp -s "$G/ApiFalsa/Program.cs" "$API/Program.cs"; then
  echo "(compilando la API falsa en $API)"; mkdir -p "$API"
  flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$G/ApiFalsa/ApiFalsa.csproj" -c Release -o "$API" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$API/obj/" > "$API/build.txt" 2>&1 || { cat "$API/build.txt"; exit 1; }
  cp "$G/ApiFalsa/Program.cs" "$API/Program.cs"
fi
SX="$G/../spotify-xp"
# la tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c, la del nucleo): suena a su ritmo (con «null» una cancion de 30 s se acaba en 2)
AL=$(dirname "$FALSO")/alsa; mkdir -p "$AL"
[ -f "$AL/libasound_module_pcm_reloj.so" ] || gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
# (se rehace si su Program.cs no es el de esta rama: la carpeta la comparten los agentes de varias ramas; antes contaba la fecha)
if [ ! -f "$FALSO/soloist-falso.dll" ] || ! cmp -s "$SX/SoloistFalso/Program.cs" "$FALSO/Program.cs"; then
  echo "(compilando el Soloist falso en $FALSO)"; mkdir -p "$FALSO"
  flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$SX/SoloistFalso/SoloistFalso.csproj" -c Release -o "$FALSO" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$FALSO/obj/" > "$FALSO/build.txt" 2>&1 || { cat "$FALSO/build.txt"; exit 1; }
  cp "$SX/SoloistFalso/Program.cs" "$FALSO/Program.cs"
fi
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
# colores PNG R G B X Y W H: cuantos puntos de ese color (a 28 por canal) hay en el rectangulo
colores() { python3 -c "import sys;from PIL import Image;a=Image.open(sys.argv[1]).convert('RGB');r,g,b,x,y,w,h=map(int,sys.argv[2:9]);c=a.crop((x,y,x+w,y+h));print(sum(1 for p in c.getdata() if abs(p[0]-r)<=28 and abs(p[1]-g)<=28 and abs(p[2]-b)<=28))" "$@" 2>/dev/null || echo 0; }
# distintos A B X Y W H U: cuantos puntos difieren mas de U entre las dos capturas en el rectangulo
distintos() { python3 -c "import sys;from PIL import Image,ImageChops;a=Image.open(sys.argv[1]).convert('RGB');b=Image.open(sys.argv[2]).convert('RGB');x,y,w,h,u=map(int,sys.argv[3:8]);d=ImageChops.difference(a.crop((x,y,x+w,y+h)),b.crop((x,y,x+w,y+h)));print(sum(1 for p in d.getdata() if max(p)>u))" "$@" 2>/dev/null || echo 0; }
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
      ROCOLA_CATALOGO_URL="http://127.0.0.1:$P/catalogo" ROCOLA_PORTADAS_CATALOGO="http://127.0.0.1:$P" \
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
# (paredes) LA CUOTA DE LA ESCENA: cuantas peticiones vio la API falsa, por clase (sin las portadas, que no son de la API)
cuota() { local a="$1"; echo "   cuota: $(grep -c ' /v1/' "$a") a /v1 = $(grep -c '/v1/search app [0-9]* q=[^ ]*$' "$a") busquedas, $(grep -c '/v1/search.* off=' "$a") paginas de busqueda, $(grep -c '/v1/artists/' "$a") de artista, $(grep -c '/v1/albums/' "$a") discos, $(grep -c '/v1/me/' "$a") de la cuenta; $(grep -c '/api/token' "$a") llaves"; }
# (27-11-2026) como «escribe», pero con espacios y cifras (sus teclas son las de Windows: letras = ASCII - 32, el resto igual)
escribe2() { local s="$1" pal="$2" r="" i ch c; for ((i = 0; i < ${#pal}; i++)); do ch="${pal:$i:1}"; c=$(printf '%d' "'$ch"); case "$ch" in [a-z]) c=$((c - 32)) ;; esac; r="$r,$(echo "$s + $i * 0.25" | bc):$c"; done; echo "${r#,}"; }
# una sola vez cada pagina (PATRON), y ninguna otra de esa clase
una() { [ "$(grep -c -- "$2" "$1")" = 1 ]; }

for e in $escenas; do
  echo "== $e"
  case "$e" in
  apagado|cero)
    # SIN EL MODO: sin ROCOLA_OS (apagado) y con ROCOLA_OS=1 y spotify=0 (cero): ni una peticion, ni la letra, ni nada en /run
    apifalsa $e
    # (paredes: y despues, ya en la pared de XP, REGRESAR, un disco, REGRESAR dentro, ARRIBA y REGRESAR otra vez: g53 y g54 sin el modo)
    T="$BASE,$(escribe 9.5 inventad),$(derechas 14 14),17:13,19:27+38,20.5:27,21:13,22.5:27,23:38,23.5:27"
    if [ $e = apagado ]; then d=$(datos $e apagado); abre $e "$d" "$T" 26 ROCOLA_OS=
    else d=$(datos $e apagado); printf 'spotify=0\n' > "$d/rocola_os.txt"; abre $e "$d" "$T" 26 ROCOLA_OS=1; fi
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
    # (paredes: el artista de Spotify «Los Inventados» va arriba, porque XP no da artistas: las canciones son la 2a y la 3a tarjeta)
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 14 14),17.4:13,18.4:40,18.9:37,19.4:13" 25 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 13.8 buscador-solo-spotify),$(cap 20.5 buscador-pedidas)"
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
    # UN DISCO: «mentira» da [cancion, disco, «Todos los discos de...»]; se elige el disco (2a tarjeta), se abre en la vista de XP y se piden
    # la 1a y la 2a con PONER. REGRESAR (paredes, pedido del 7-11-2026): el disco NO estaba en la letra de Spotify -> vuelve AL BUSCADOR con
    # lo escrito y la misma eleccion: PONER lo abre otra vez (de memoria) y se pide la 3a. Ahora ya esta en la letra: REGRESAR -> la pared
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 mentira),$(derechas 13.5 14),16.5:13,19:13,20:40,21:13,22.5:27,24.5:13,26.5:40,27:40,27.6:13,29:27" 33 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 16 buscador-disco),$(cap 20.7 disco-de-spotify),$(cap 24 buscador-al-volver-del-disco),$(cap 31 pared-letra-spotify)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    si "disco: sale con 0" test "$(cat $o/salio)" = 0
    si "disco: las busquedas y UN disco (/v1/albums/dis000), aunque se abrio dos veces: $(v1 $a) peticiones a /v1" bash -c "[ \$(grep -c 'GET /v1/albums/dis000 app 200' '$a') = 1 ] && [ \$(grep -c ' /v1/' '$a') = \$((\$(grep -c '/v1/search' '$a') + 1)) ]"
    si "disco: dos pedidas con PONER: la 1a y la 2a del disco" bash -c "[ '$(pedido $o 1)' = \"\$(printf 'pedida\tspotify:track:can0000')\" ] && [ '$(pedido $o 2)' = \"\$(printf 'pedida\tspotify:track:can0001')\" ]"
    si "disco: REGRESAR volvio al buscador con «mentira» y el disco elegido: PONER lo abrio otra vez y se pidio la 3a (can0002)" test "$(pedido $o 3)" = "$(printf 'pedida\tspotify:track:can0002')"
    si "disco: solo esas tres pedidas ($(pedidos $o))" test "$(pedidos $o)" = 3 ;;
  artista)
    # UN ARTISTA: «imaginario» da el artista (arriba: XP no da artistas; el otro que da la API, «Grupo Ficticio», no contiene lo escrito y no
    # sale), 8 canciones y 2 discos; se elige y abre SU PARED (paredes): «Canciones de Trio Imaginario» y sus 2 discos; a la derecha, el 1o
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 imaginario),$(derechas 14 14),16.5:37,17.5:13,20:39" 25 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 17 buscador-artista),$(cap 19.5 pared-artista-pocos),$(cap 22 pared-discos-del-artista)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    si "artista: sale con 0" test "$(cat $o/salio)" = 0
    si "artista: las busquedas, UNA pagina de su pared (/v1/artists/art03/albums off=0: tiene 2) y UNA del disco que se queda elegido en la pared (g52: /v1/albums/dis006)" bash -c "[ \$(grep -c 'GET /v1/artists/art03/albums app 200 t=album,single off=0$' '$a') = 1 ] && [ \$(grep -c 'GET /v1/albums/dis006 app 200' '$a') = 1 ] && [ \$(grep -c ' /v1/' '$a') = \$((\$(grep -c '/v1/search' '$a') + 2)) ]"
    si "artista: la pared en la bitacora (3 casillas: «Canciones de» y los 2 discos)" contiene "$d/rocola_os_bitacora.txt" "pared del buscador: pared_artista (3 casillas)"
    si "artista: nada de «Grupo Ficticio» (no contiene lo escrito: su foto no se bajo)" bash -c "! grep -q 'artista-art02' '$a'"
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
    # LA CUENTA (refresh_token) SIN LA LETRA ♫ EN LA PARED (24-11-2026, revision de mejoras, A1): lo de la cuenta (/me/tracks, /me/albums,
    # /me/playlists) solo alimenta esa letra, que no sale (RocolaOsSpotifyLetraEnLaPared = false): ni se pide. Antes esta escena esperaba las tres
    # peticiones, la lista propia y la renovacion de la llave de la cuenta; ahora nada de eso (los atajos de SP9, que si usan la cuenta, tienen su escena)
    apifalsa $e; d=$(datos $e cuenta)
    abre $e "$d" "7:67,10:27+37,12:39,12.6:39,13.2:39,14:13,16.5:13,18:27" 22 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 11.5 pared-cuenta)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    sed 's/^/   /' "$a" | grep -v '/img/' | head -12
    si "cuenta: sale con 0" test "$(cat $o/salio)" = 0
    si "cuenta: ninguna peticion de la cuenta (nada de /v1/me/)" bash -c "! grep -q '/v1/me/' '$a'"
    si "cuenta: ninguna peticion a /v1 ni a la llave (la letra no sale: nada que pedir)" test "$(wc -l < $a)" = 0
    si "cuenta: ni el secreto ni las llaves en los datos ni en /run" sinsecretos "$d" "$o/run" ;;
  suena)
    # LO QUE SUENA CON UNA DE SPOTIFY: se pide la 1a de «inventad», se cierra el buscador y la barra de abajo de XP dice lo que suena. Con la
    # tarjeta de prueba con reloj: suena a su ritmo y no se acaba en un momento
    apifalsa $e; d=$(datos $e encendido); o="$W/$e"
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 14 14),17.4:13,19.5:27+38" 25 ROCOLA_OS=1 \
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
    # bitacora (el mismo renglon una vez). (24-11-2026, A3) el «sin red» se RECUERDA: tras el primer fallo no se vuelve a preguntar en 15 s (lo
    # escrito despues contesta «sin red» al instante, sin tocar la red); la API vuelve a los 12 s, pero hasta pasar el plazo nadie la prueba; pasado,
    # una peticion de prueba (la busqueda a los 28 s) sale bien y todo vuelve
    d=$(datos $e cuenta); P=$((P + 1)); o="$W/$e"; mkdir -p "$o/api"; : > "$o/api/peticiones.txt"
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(escribe 11.4 x),28:8" 36 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 11.8 buscador-sin-red)"
    sleep 12.5; apifalsa $e mismo
    espera $e; a="$o/api/peticiones.txt"
    grep -v '/img/' "$a" | sed 's/^/   /' | head -8
    si "sinred: sale con 0" test "$(cat $o/salio)" = 0
    si "sinred: sin red, el renglon «sin red: Spotify no contesta» UNA vez en la bitacora aunque se busco varias veces ($(grep -c 'sin red: Spotify no contesta' $d/rocola_os_bitacora.txt))" test "$(grep -c 'sin red: Spotify no contesta' $d/rocola_os_bitacora.txt)" = 1
    si "sinred: la API que volvio a los 12 s NO recibio nada hasta pasar el plazo (nada de lo escrito entre 12,5 s y 25 s llego a ella: solo la prueba de los 28 s)" bash -c "[ \$(grep -c '/v1/search' '$a') = 1 ] && grep '/v1/search' '$a' | grep -q ' 200 q=inventad\$'"
    si "sinred: la prueba pidio una llave y salio bien (una sola)" bash -c "[ \$(grep -c '^POST /api/token' '$a') = 1 ]"
    si "sinred: la cuenta no se pide (A1: ni /v1/me ni reintentos de la cuenta)" bash -c "! grep -q '/v1/me/' '$a' && ! grep -q 'se vuelve a intentar solo' '$d/rocola_os_bitacora.txt'"
    si "sinred: al salir bien, el estado dice api=bien y con la hora del ultimo exito" bash -c "grep -qx 'api=bien' '$o/run/rocola-xp/spotify/catalogo' && grep -qE '^ultimo_exito=20[0-9-]+T[0-9:]+Z\$' '$o/run/rocola-xp/spotify/catalogo'" ;;
  colgada)
    # LA API QUE NO CONTESTA (24-11-2026, A3): un puerto que acepta la conexion y se queda callado. Lo que alguien espera (busqueda, la llave) vale 6 s
    # en total (antes 15 s de tiempo y 8 de conexion, con un reintento tras dormir 1 s); al agotarse se recuerda «sin red» y lo escrito despues
    # contesta al instante sin abrir otra conexion. El estado dice api=sin_red
    d=$(datos $e encendido); P=$((P + 1)); o="$W/$e"; mkdir -p "$o/api"
    python3 - "$P" "$o/api/conexiones.txt" <<'PY' &
import socket, sys
s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1); s.bind(("127.0.0.1", int(sys.argv[1]))); s.listen(16)
guardadas = []; f = open(sys.argv[2], "a")
while True:
    c, _ = s.accept(); guardadas.append(c); f.write("conexion\n"); f.flush()
PY
    APIPID=$!; sleep 0.5
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(escribe 11.4 x)" 23 ROCOLA_OS=1
    t0=$(date +%s)
    espera $e
    si "colgada: sale con 0" test "$(cat $o/salio)" = 0
    si "colgada: la rocola salio a su hora (no se quedo esperando a la API: $(( $(date +%s) - t0 )) s)" test $(( $(date +%s) - t0 )) -le 40
    si "colgada: UNA sola conexion a la API (la de la llave, que se corto a los 6 s); lo escrito despues no abrio otras ($(grep -c conexion $o/api/conexiones.txt))" test "$(grep -c conexion $o/api/conexiones.txt)" = 1
    si "colgada: el estado dice api=sin_red" bash -c "grep -qx 'api=sin_red' '$o/run/rocola-xp/spotify/catalogo'"
    si "colgada: «sin red: Spotify no contesta» una vez en la bitacora" test "$(grep -c 'sin red: Spotify no contesta' $d/rocola_os_bitacora.txt)" = 1 ;;
  sinsesion)
    # SOLOIST SIN SESION (I6 dice que no esta listo: el vigilante sale al momento y no hay Soloist): no se pide ni se cobra; el aviso de XP
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(derechas 14 14),17.4:13" 22 ROCOLA_OS=1 ROCOLA_SOLOIST_BIN=/bin/true ROCOLA_CAPTURA="$(cap 17.8 buscador-spotify-no-listo)"
    espera $e; o="$W/$e"
    si "sinsesion: sale con 0" test "$(cat $o/salio)" = 0
    si "sinsesion: nada pedido" test "$(pedidos $o)" = 0
    si "sinsesion: la bitacora dice que no estaba listo" contiene "$d/rocola_os_bitacora.txt" "Spotify aun no esta listo" ;;
  pared)
    # LA PARED DE UN ARTISTA QUE SOLO ESTA EN SPOTIFY: «interminable» (XP no tiene nada) da sus artistas arriba (Banda Interminable y Orquesta
    # Interminable, en el orden de la API: Orquesta y Banda; «Grupo Ficticio» no); se elige Banda (la 2a) (40 discos: su pared tiene 41 casillas, 3 paginas). Se abre con su 1a pagina (off=0 y
    # off=10); ARRIBA (la pagina siguiente, como en XP) pide off=20 y off=30; se entra al 1er disco de esa pagina y se pide su 1a; REGRESAR: la
    # pared en la misma pagina y casilla (PONER entra al mismo disco y se pide la 2a); REGRESAR: el buscador con lo escrito y la misma eleccion
    # (PONER abre otra vez la pared de Banda, de memoria); REGRESAR y REGRESAR+ARRIBA: el buscador se cierra y queda la pared de XP de antes
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),17:13,20:38,22.5:13,25:13,26:27,27:13,28:40,28.6:13,29.5:27,31:27,33.5:13,35.5:27,37:27+38" 40 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 15.5 buscador-artistas-de-spotify),$(cap 19 pared-artista),$(cap 22 pared-artista-pagina2),$(cap 24.5 disco-de-la-pared),$(cap 30.5 pared-al-volver-del-disco),$(cap 32.5 buscador-al-volver-de-la-pared),$(cap 35 pared-otra-vez),$(cap 39 pared-xp-al-cerrar-el-buscador)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    si "pared: sale con 0" test "$(cat $o/salio)" = 0
    si "pared: la 1a pagina con DOS peticiones (off=0 y off=10) y la 2a con otras DOS (off=20 y off=30), una vez cada una aunque se abrio dos veces" bash -c "for n in 0 10 20 30; do [ \$(grep -c 'GET /v1/artists/art50/albums app 200 t=album,single off='\$n'$' '$a') = 1 ] || exit 1; done; [ \$(grep -c '/v1/artists/' '$a') = 4 ]"
    si "pared: la 3a pagina no se pidio (nadie la miro)" bash -c "! grep -q 'art50/albums.* off=40' '$a'"
    si "pared: el disco de la 2a pagina con UNA peticion (/v1/albums/dis518), y nada mas que las busquedas: $(v1 $a) a /v1" bash -c "[ \$(grep -c 'GET /v1/albums/dis518 app 200' '$a') = 1 ] && [ \$(grep -c ' /v1/' '$a') = \$((\$(grep -c '/v1/search' '$a') + 5)) ]"
    si "pared: la 1a pedida es la 1a del disco de la casilla 19 (can5181)" test "$(pedido $o 1)" = "$(printf 'pedida\tspotify:track:can5181')"
    si "pared: REGRESAR dejo la pared en la misma pagina y casilla: PONER entro al mismo disco y la 2a pedida es su 2a (can5182)" test "$(pedido $o 2)" = "$(printf 'pedida\tspotify:track:can5182')"
    si "pared: solo esas dos pedidas ($(pedidos $o))" test "$(pedidos $o)" = 2
    si "pared: el buscador volvio con lo escrito y la misma eleccion (PONER abrio otra vez la de Banda: 2 veces en la bitacora; de Orquesta, nada)" bash -c "[ \$(grep -c 'pared del buscador: pared_artista (41 casillas)' '$d/rocola_os_bitacora.txt') = 2 ] && ! grep -q '/v1/.*art99' '$a'"
    si "pared: de «Canciones de» solo su 1a pagina (off=0: se quedo elegida al abrir, para que la ficha diga cuantas trae); las demas no (nadie entro)" bash -c "[ \$(grep -c 'q=artist:' '$a') = 1 ] && grep -q 'q=artist:\"Banda Interminable\" t=track off=0$' '$a'"
    si "pared: sin la cuenta, nada de la cuenta ni de los atajos (/v1/me)" bash -c "! grep -q '/v1/me/' '$a'"
    si "pared: el estado cuenta las paginas del artista (artistas=4)" bash -c "grep -qx 'artistas=4' '$o/run/rocola-xp/spotify/catalogo'"
    si "pared: nada de Spotify en los datos de XP (solo la bitacora del nucleo y la llave)" bash -c "! grep -rlE 'spotify:|can5|dis5|Interminable' '$d' | grep -vE '/(rocola_spotify\.txt|rocola_spotify_api\.txt|rocola_os_bitacora\.txt)$' | grep -q ."
    # (22-11-2026, el dueno: en la pared del buscador no va el abecedario: «REGRESAR AL BUSCADOR» con el boton rojo, y «DISCOS · ARTISTA» con
    # el icono de Spotify; RocolaOs/RocolaOs.ParedLetrero.cs). En 1024x768 la barra va en la franja y=58..94 y la cabecera en la fila de arriba
    ca="${CAPTURAS:-$W}"
    rojos=$(colores "$ca/pared-artista.png" 228 72 72 0 58 1024 36); franja=$(distintos "$ca/pared-artista.png" "$ca/pared-xp-al-cerrar-el-buscador.png" 0 58 1024 36 40)
    verdes=$(colores "$ca/pared-artista.png" 29 185 84 600 0 424 56); verdesxp=$(colores "$ca/pared-xp-al-cerrar-el-buscador.png" 29 185 84 600 0 424 56)
    rojosxp=$(colores "$ca/pared-xp-al-cerrar-el-buscador.png" 228 72 72 300 58 424 36)
    si "pared: sin el abecedario: en su franja, el boton rojo del letrero ($rojos puntos rojos) y la franja distinta de la de XP ($franja puntos)" test "$rojos" -gt 60 -a "$franja" -gt 3000
    si "pared: la cabecera con el icono de Spotify ($verdes puntos verdes); en la pared de XP no ($verdesxp)" test "$verdes" -gt 60 -a "$verdesxp" -lt 10
    si "pared: al cerrar el buscador, la pared de XP con su abecedario (sin el boton rojo del letrero: $rojosxp puntos rojos en medio de la franja)" test "$rojosxp" -lt 40 ;;
  canciones)
    # «CANCIONES DE ARTISTA»: la casilla 1 de la pared de Banda Interminable; al entrar se piden sus canciones (search artist:"..." de 10 en 10)
    # mientras se esta dentro, hasta 50 (5 peticiones); PONER pide la 1a; ARRIBA desde la 1a da la vuelta a la ultima (la 50a) y se pide
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),17:13,19:13,21.5:13,24:38,24.6:13,26:27,27:27" 30 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 20.8 disco-canciones-de),$(cap 25.5 disco-canciones-de-al-final)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    si "canciones: sale con 0" test "$(cat $o/salio)" = 0
    si "canciones: 5 peticiones de canciones (off=0 a 40), una vez cada una, y no la 6a" bash -c "for n in 0 10 20 30 40; do [ \$(grep -c 'GET /v1/search app 200 q=artist:\"Banda Interminable\" t=track off='\$n'$' '$a') = 1 ] || exit 1; done; [ \$(grep -c 'q=artist:' '$a') = 5 ]"
    si "canciones: la 1a pedida es la 1a de la busqueda por artista (can5011)" test "$(pedido $o 1)" = "$(printf 'pedida\tspotify:track:can5011')"
    si "canciones: la 2a es la 50a (can5172): llegaron las 50" test "$(pedido $o 2)" = "$(printf 'pedida\tspotify:track:can5172')"
    si "canciones: solo esas dos pedidas ($(pedidos $o))" test "$(pedidos $o)" = 2 ;;
  todos)
    # «TODOS LOS DISCOS DE «LO ESCRITO»»: la ultima tarjeta de los discos de Spotify (la 14a: 6 abajo); su pared con los 41 discos de la
    # busqueda (3 paginas): se abre con off=0 y off=10; ARRIBA, off=20 y off=30; ARRIBA, off=40; ARRIBA en la ultima no hace nada (ni pide);
    # ABAJO dos veces vuelve a la 1a (de memoria); REGRESAR: el buscador
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),16.4:40,16.8:40,17.2:40,17.6:40,18:40,18.4:40,19.5:13,22:38,23.5:38,25.5:38,26.5:40,27:40,28:27" 32 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 19.2 buscador-todos-los-discos),$(cap 21.5 pared-todos),$(cap 25 pared-todos-pagina3),$(cap 29.5 buscador-al-volver-de-todos)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    si "todos: sale con 0" test "$(cat $o/salio)" = 0
    si "todos: 5 paginas de la busqueda de discos (off=0 a 40), una vez cada una, ninguna mas" bash -c "for n in 0 10 20 30 40; do [ \$(grep -c 'GET /v1/search app 200 q=interminable t=album off='\$n'$' '$a') = 1 ] || exit 1; done; [ \$(grep -c 't=album off=' '$a') = 5 ]"
    si "todos: lo demas, las busquedas y como mucho un disco por pagina mirada (g52; el de 60 canciones son 2): $(v1 $a) a /v1" bash -c "n=\$(grep -c '/v1/albums/' '$a'); [ \$n -le 4 ] && [ \$(grep -c ' /v1/' '$a') = \$((\$(grep -c '/v1/search app [0-9]* q=[^ ]*$' '$a') + 5 + n)) ]"
    si "todos: la pared en la bitacora (41 casillas)" contiene "$d/rocola_os_bitacora.txt" "pared del buscador: pared_todos (41 casillas)"
    si "todos: el estado cuenta las paginas de las paredes (paredes=5)" bash -c "grep -qx 'paredes=5' '$o/run/rocola-xp/spotify/catalogo'" ;;
  unida)
    # LA PARED UNIDA (23-11-2026, el dueno: «unificar los discos locales con los de Spotify»; sustituye a «ARTISTA · mas en Spotify»):
    # «otra» da tu artista «Otra Banda» (la tarjeta de XP de siempre) y ya NO sale otra tarjeta detras. Spotify la tiene (3 discos): tu
    # tarjeta abre la pared unida, al momento: tu disco primero (tu carpeta «Otra Banda» no lleva « - »: para XP es un disco sin nombre de
    # «Otra Banda», y cuenta como el disco «Otra Banda» de Spotify, que no se repite), «Canciones de Otra Banda» y los discos de Spotify que
    # no tienes (Segundo y Tercero). Tres a la derecha (la 4a casilla: el Tercero) y PONER; REGRESAR dos veces: el buscador, con tu tarjeta
    # elegida; PONER la abre otra vez (de memoria) y PONER entra a TU disco (la 1a casilla); REGRESAR vuelve a la pared unida
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 otra),$(derechas 12.5 14),15.2:37,16.5:13,19:39,19.15:39,19.3:39,19.8:13,22:27,23:27,25:13,26.5:13,28.5:27" 31 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 15.8 buscador-tu-artista-sin-mas),$(cap 18.5 pared-unida),$(cap 21.5 disco-de-spotify-en-la-unida),$(cap 24.5 buscador-al-volver-de-la-unida),$(cap 27.5 disco-tuyo-en-la-unida),$(cap 30 pared-unida-al-volver-de-tu-disco)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    si "unida: sale con 0" test "$(cat $o/salio)" = 0
    si "unida: UNA pagina de su pared (/v1/artists/art60/albums off=0: tiene 3), aunque se abrio dos veces" bash -c "[ \$(grep -c 'GET /v1/artists/art60/albums app 200 t=album,single off=0$' '$a') = 1 ] && [ \$(grep -c '/v1/artists/' '$a') = 1 ]"
    si "unida: sin repetir el disco que ya tienes: la 4a casilla es el Tercero (/v1/albums/dis651), y del que tienes (dis649) nada" bash -c "[ \$(grep -c 'GET /v1/albums/dis651 app 200' '$a') = 1 ] && ! grep -q 'albums/dis649' '$a'"
    si "unida: tu tarjeta abrio la pared del buscador dos veces (al momento, con tu disco)" bash -c "[ \$(grep -c 'pared del buscador: pared_artista (' '$d/rocola_os_bitacora.txt') = 2 ]"
    si "unida: las peticiones: las busquedas, la pared y el disco de Spotify (tu disco no pide nada)" bash -c "[ \$(grep -c ' /v1/' '$a') = \$((\$(grep -c '/v1/search' '$a') + 2)) ]"
    si "unida: nada pedido (solo se entro)" test "$(pedidos $o)" = 0 ;;
  siempre)
    # SIEMPRE UNA PARED DEL BUSCADOR O EL DISCO DENTRO DEL BUSCADOR (23-11-2026, el dueno: «siempre si se escoge la opcion de artista o album
    # en el buscador, se abra ya sea una pared de discos temporal, o el disco dentro del buscador»; antes, lo tuyo salia a la pared de XP).
    # Tres rondas con datos de XP inventados (Los Ejemplos - Disco Uno, que Spotify NO conoce; Otra Banda, que si):
    #  a) un DISCO TUYO elegido en el buscador (14 a la derecha: la tarjeta «Disco Uno» de «ejemplos»): se abre dentro del buscador; REGRESAR
    #     vuelve al buscador con lo escrito y la misma tarjeta (PONER lo abre otra vez); sin tocar nada, a los 30 s se cierra todo
    #  b) un ARTISTA TUYO que Spotify no conoce (13 a la derecha): una pared del buscador SOLO con tus discos, sin pedir nada a la pared de la
    #     API y sin el icono de Spotify; PONER entra a su disco, REGRESAR a la pared, REGRESAR al buscador, PONER otra vez la pared; a los 60 s
    #     sin tocar, se cierra todo
    #  c) un ARTISTA TUYO que Spotify tiene y del que ya tienes TODOS sus discos (los de Spotify estan en tu carpeta): se abre la pared unida
    #     (tus 3 discos y «Canciones de Otra Banda», con el icono de Spotify), tambien cuando su pagina ya esta en memoria (antes: lo de XP)
    apifalsa $e-a; da=$(datos $e-a encendido)
    abre $e-a "$da" "$BASE,$(escribe 9.5 ejemplos),$(derechas 12.5 14),16:13,19:27,22:13" 62 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 15.5 buscador-disco-tuyo-elegido),$(cap 18 disco-tuyo-dentro-del-buscador),$(cap 21 buscador-al-volver-del-disco-tuyo),$(cap 25 disco-tuyo-otra-vez),$(cap 57 pared-xp-tras-30-s-en-el-disco-tuyo)"
    espera $e-a; oa="$W/$e-a"; aa="$oa/api/peticiones.txt"
    si "siempre a: sale con 0" test "$(cat $oa/salio)" = 0
    si "siempre a: el disco tuyo se abrio dentro del buscador (la bitacora, dos veces: la de PONER y la de PONER otra vez)" test "$(cuenta $da/rocola_os_bitacora.txt 'disco tuyo abierto desde el buscador')" = 2
    si "siempre a: REGRESAR volvio al buscador con lo escrito (la bitacora dice «ejemplos»)" contiene "$da/rocola_os_bitacora.txt" "REGRESAR: de vuelta al buscador con «ejemplos»"
    si "siempre a: sin tocar nada, a los 30 s se cerro todo (la pared de XP)" contiene "$da/rocola_os_bitacora.txt" "se cierra todo lo del buscador (un disco del buscador, 30 s sin tocar nada)"
    si "siempre a: lo de Spotify no se mezclo: ninguna pared ni disco de la API, solo la busqueda ($(v1 $aa) a /v1)" bash -c "[ \$(grep -c ' /v1/' '$aa') = \$(grep -c '/v1/search' '$aa') ]"
    si "siempre a: nada pedido" test "$(pedidos $oa)" = 0
    # (b) el artista tuyo que Spotify no conoce
    apifalsa $e-b; db=$(datos $e-b encendido)
    abre $e-b "$db" "$BASE,$(escribe 9.5 ejemplos),$(derechas 12.5 10),16:13,19:13,22:27,24:27,28:13" 100 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 17.5 pared-solo-tus-discos),$(cap 20.5 disco-de-la-pared-solo-tus-discos),$(cap 26 buscador-al-volver-de-la-pared-solo-tus-discos),$(cap 29.5 pared-solo-tus-discos-otra-vez),$(cap 98 pared-xp-tras-60-s)"
    espera $e-b; ob="$W/$e-b"; ab="$ob/api/peticiones.txt"
    ca="${CAPTURAS:-$W}"
    si "siempre b: sale con 0" test "$(cat $ob/salio)" = 0
    si "siempre b: la pared del buscador con solo tus discos (1 casilla), abierta dos veces" test "$(cuenta $db/rocola_os_bitacora.txt 'pared del buscador: tuyos \(1 casillas\)')" = 2
    si "siempre b: ninguna peticion de pared ni de artista ni de disco a la API (solo busquedas: $(v1 $ab) a /v1)" bash -c "[ \$(grep -c ' /v1/' '$ab') = \$(grep -c '/v1/search' '$ab') ] && ! grep -q '/v1/artists/' '$ab' && ! grep -q 'q=artist:' '$ab'"
    si "siempre b: REGRESAR de la pared volvio al buscador con «ejemplos»" contiene "$db/rocola_os_bitacora.txt" "REGRESAR: de vuelta al buscador con «ejemplos»"
    verdesb=$(colores "$ca/pared-solo-tus-discos.png" 29 185 84 0 0 1024 100); rojosb=$(colores "$ca/pared-solo-tus-discos.png" 228 72 72 0 58 1024 36)
    si "siempre b: sin el icono de Spotify en la cabecera ni en el letrero ($verdesb puntos verdes), pero con el letrero de REGRESAR ($rojosb puntos rojos)" test "$verdesb" -lt 10 -a "$rojosb" -gt 60
    si "siempre b: a los 60 s sin tocar, se cerro todo (la pared de XP)" contiene "$db/rocola_os_bitacora.txt" "se cierra todo lo del buscador (una pared del buscador, 60 s sin tocar nada)"
    si "siempre b: nada pedido" test "$(pedidos $ob)" = 0
    # (c) tu artista que Spotify tiene, con todos sus discos ya tuyos: «Otra Banda - Segundo Disco» y «Tercer Disco» junto a tu «Otra Banda»
    apifalsa $e-c; dc=$(datos $e-c encendido)
    for n in "Otra Banda - Segundo Disco" "Otra Banda - Tercer Disco"; do mkdir -p "$dc/musica/$n"; cp "$dc/musica/Otra Banda/01 Tema 1.mp3" "$dc/musica/$n/01 Tema.mp3"; done
    abre $e-c "$dc" "$BASE,$(escribe 9.5 otra),$(derechas 12.5 10),17:13" 26 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 15.5 buscador-tu-artista-con-todo),$(cap 20 pared-unida-con-todo-tuyo)"
    espera $e-c; oc="$W/$e-c"; ac="$oc/api/peticiones.txt"
    si "siempre c: sale con 0" test "$(cat $oc/salio)" = 0
    si "siempre c: se abrio la pared unida (de Spotify: pared_artista), aunque tienes todos sus discos" test "$(cuenta $dc/rocola_os_bitacora.txt 'pared del buscador: pared_artista \(')" = 1
    si "siempre c: UNA pagina de su pared (art60 off=0) y ningun disco de Spotify (todos son tuyos)" bash -c "[ \$(grep -c 'GET /v1/artists/art60/albums app 200 t=album,single off=0\$' '$ac') = 1 ] && ! grep -q '/v1/albums/' '$ac'"
    verdesc=$(colores "$ca/pared-unida-con-todo-tuyo.png" 29 185 84 0 0 1024 100)
    si "siempre c: con el icono de Spotify ($verdesc puntos verdes)" test "$verdesc" -gt 60
    ;;
  atajos)
    # SP9, LOS ATAJOS DEL BUSCADOR (con la cuenta): al abrir el buscador se piden «tus mas escuchadas» y «escuchado hace poco» (una vez cada
    # una por sesion) y salen como los atajos de XP, detras de los suyos. ARRIBA dos veces: a los atajos; a la derecha, «Tus mas escuchadas»:
    # sus canciones; PONER pide la 1a. REGRESAR sale del atajo; otra vez a los atajos y «Escuchado hace poco» (sin repetidas): PONER pide la 1a.
    # Se cierra y se abre el buscador: no se pide nada mas
    apifalsa $e; d=$(datos $e cuenta)
    abre $e "$d" "7:67,8:27+38,10:38,10.5:38,12:39,12.5:13,14.5:13,16:27,16.8:38,17.4:39,17.9:39,18.5:13,20.5:13,22:27+38,23.5:27+38" 27 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 11.5 buscador-atajos-spotify),$(cap 14 buscador-tus-mas-escuchadas),$(cap 20 buscador-escuchado-hace-poco)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    si "atajos: sale con 0" test "$(cat $o/salio)" = 0
    si "atajos: UNA peticion de tus mas escuchadas y UNA de lo escuchado hace poco, con la llave de la cuenta (aunque se abrio el buscador dos veces)" bash -c "[ \$(grep -c 'GET /v1/me/top/tracks cuenta 200' '$a') = 1 ] && [ \$(grep -c 'GET /v1/me/player/recently-played cuenta 200' '$a') = 1 ]"
    si "atajos: la 1a pedida es la 1a de tus mas escuchadas (can0012)" test "$(pedido $o 1)" = "$(printf 'pedida\tspotify:track:can0012')"
    si "atajos: la 2a es la 1a de lo escuchado hace poco (can0002)" test "$(pedido $o 2)" = "$(printf 'pedida\tspotify:track:can0002')"
    si "atajos: solo esas dos pedidas ($(pedidos $o))" test "$(pedidos $o)" = 2
    si "atajos: el estado cuenta los atajos (atajos=2)" bash -c "grep -qx 'atajos=2' '$o/run/rocola-xp/spotify/catalogo'"
    si "atajos: ni el secreto ni las llaves en los datos ni en /run" sinsecretos "$d" "$o/run" ;;
  pared429)
    # 429 A MEDIA PARED: se abre la de Banda Interminable (off=0 y off=10); entonces Spotify frena (429, 60 s); ARRIBA: la 2a pagina pide off=20
    # (429) y off=30 ya no sale; ABAJO y ARRIBA otra vez: nada a la red (frena); REGRESAR: el buscador (de memoria); elegir Orquesta: nada a la
    # red, el aviso de XP y el buscador se queda
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),17:13,20:38,22.5:40,23.5:38,25:27,26:37,26.5:13" 30 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 21.5 pared-429),$(cap 27.2 buscador-429-artista)"
    a="$W/$e/api/peticiones.txt"
    ( t=0; until grep -q 'art50/albums app 200 t=album,single off=10$' "$a" 2>/dev/null || [ $t -gt 400 ]; do sleep 0.1; t=$((t + 1)); done; control "429?seg=60" ) &
    espera $e; o="$W/$e"; cuota "$a"
    si "pared429: sale con 0" test "$(cat $o/salio)" = 0
    si "pared429: UN 429 y despues NINGUNA peticion a /v1 (la 2a pagina, off=20, nunca llego)" bash -c "[ \$(grep -c ' 429 ' '$a') = 1 ] && grep ' /v1/' '$a' | tail -1 | grep -q ' 429 ' && ! grep -q 'app 200 t=album,single off=20$' '$a'"
    si "pared429: de Orquesta nada (frena)" bash -c "! grep -q '/v1/.*art99' '$a'"
    si "pared429: la bitacora dice que frena y que la pagina no llego" bash -c "grep -q 'Spotify frena (429)' '$d/rocola_os_bitacora.txt' && grep -q 'no llego una pagina de la pared: Spotify frena' '$d/rocola_os_bitacora.txt'"
    si "pared429: el estado dice api=frena" bash -c "grep -qx 'api=frena' '$o/run/rocola-xp/spotify/catalogo'" ;;
  mantenido)
    # (revision funcionamiento-y-reglas) EL ROJO MANTENIDO EN LA PILA: con la repeticion de XP (a los 500 ms, ~30 por segundo). En un disco de
    # la pared, el rojo mantenido 1,5 s lleva a la pared y ahi se queda (sus repeticiones no siguen hasta el buscador): PONER entra al mismo
    # disco y PONER pide su 1a (can5181); REGRESAR: la pared. En la pared, el rojo mantenido 1,5 s: el buscador, y sus repeticiones NO borran lo escrito (PONER abre
    # otra vez la pared de Banda: 2a vez en la bitacora). ROJO+ARRIBA en la pared (el rojo primero, como se abre el buscador): el buscador se
    # queda abierto con lo escrito (PONER la abre una 3a vez), no se cierra dejando la pared de XP
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),17:13,20:38,22.5:13,24.5:27~1500,27:13,28.5:13,30:27,31:27~1500,34:13,36:27~1200,36.4:38,38.5:13,40.5:27" 43 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 26.5 pared-tras-el-rojo-mantenido-en-un-disco),$(cap 33.5 buscador-tras-el-rojo-mantenido),$(cap 38 buscador-tras-rojo-y-arriba-en-la-pared)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    si "mantenido: sale con 0" test "$(cat $o/salio)" = 0
    si "mantenido: el rojo mantenido en el disco se quedo en la pared: la 1a pedida es la 1a del mismo disco (can5181)" test "$(pedido $o 1)" = "$(printf 'pedida\tspotify:track:can5181')"
    si "mantenido: solo esa pedida ($(pedidos $o))" test "$(pedidos $o)" = 1
    si "mantenido: el rojo mantenido en la pared no borro lo escrito y ROJO+ARRIBA no cerro el buscador (la pared de Banda abierta 3 veces: $(grep -c 'pared del buscador: pared_artista (41 casillas)' $d/rocola_os_bitacora.txt))" bash -c "[ \$(grep -c 'pared del buscador: pared_artista (41 casillas)' '$d/rocola_os_bitacora.txt') = 3 ]"
    si "mantenido: de Orquesta nada, y la pared de Banda de memoria (4 peticiones de artista: off=0..30)" bash -c "! grep -q '/v1/.*art99' '$a' && [ \$(grep -c '/v1/artists/' '$a') = 4 ]" ;;
  tiempo)
    # (revision funcionamiento-y-reglas) LA PILA CON LA ATRACCION, MONEDAS Y EL BUSCADOR CERRADO POR TIEMPO: la pared de Banda abierta y nada
    # mas: a los 40 s entra la atraccion; la moneda la despierta (y acredita) y la pared sigue ahi; REGRESAR: el buscador con lo escrito
    # (PONER la abre otra vez, de memoria); REGRESAR: el buscador; un minuto sin tocar el buscador (solo dos monedas, que no lo cuentan como
    # uso pero alejan la atraccion): se cierra solo y queda la pared de XP de antes de abrirlo; ahi REGRESAR no hace nada (XP) y PONER entra
    # al disco de XP elegido (si siguiera el buscador, abriria la pared de Banda; si quedara una pared de Spotify, pediria un disco a /v1);
    # REGRESAR vuelve a su pared (XP, BackToAlbums)
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),17:13,62:67,63.5:27,65:13,66.5:27,90:67,120:67,128:27,129:13,131.5:27" 134 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 60 atraccion-sobre-la-pared),$(cap 63 pared-tras-la-moneda),$(cap 64.5 buscador-tras-la-moneda),$(cap 68 buscador-otra-vez),$(cap 127.5 pared-xp-tras-cerrarse-por-tiempo),$(cap 130.5 disco-xp-tras-el-tiempo),$(cap 133 pared-xp-al-final)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    si "tiempo: sale con 0" test "$(cat $o/salio)" = 0
    si "tiempo: tras la atraccion y la moneda, REGRESAR volvio al buscador con lo escrito (la pared de Banda abierta 2 veces)" bash -c "[ \$(grep -c 'pared del buscador: pared_artista (41 casillas)' '$d/rocola_os_bitacora.txt') = 2 ]"
    si "tiempo: la pared de Banda de memoria la 2a vez (2 peticiones de artista) y nada de Orquesta" bash -c "[ \$(grep -c '/v1/artists/' '$a') = 2 ] && ! grep -q '/v1/.*art99' '$a'"
    si "tiempo: nada de Spotify pedido ($(pedidos $o))" test "$(pedidos $o)" = 0
    si "tiempo: el buscador se cerro solo y quedo la pared de XP: PONER entro a un disco de XP (ningun disco pedido a /v1 ni la pared de Banda otra vez)" bash -c "! grep -q '/v1/albums/' '$a' && [ \$(grep -c 'pared del buscador: pared_artista' '$d/rocola_os_bitacora.txt') = 2 ]" ;;
  hojea)
    # (revision cuota-hilos-y-fallos) HOJEAR DEPRISA CON LA RED LENTA: Banda Interminable con 300 discos (/control/muchos: su pared, 301 casillas,
    # 17 paginas) y cada peticion a /v1 400 ms mas lenta. Se abre la pared y se MANTIENE ARRIBA 2,5 s (la repeticion de XP la lleva hasta la
    # ultima pagina): las paginas que pasaron sin que nadie las mirara NO se piden despues (de la cola se quitan las que ya no se miran); la
    # ultima (off=280 y 290) si, una vez. Despues un 503 a media pared (2 seguidos: la peticion y su reintento): ABAJO pide lo que le falta
    # a la 16a (off=260 y 270, o solo uno si el otro llego al pasar) y el primero da 503; ARRIBA y ABAJO otra vez: se pide de nuevo y llega.
    # Lo que se dejo no se apunta como fallo.
    apifalsa $e; d=$(datos $e encendido); control muchos; control "lento?ms=400"
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),17:13,20:38~2500,26:40,29:38,30:40" 34 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 19.5 pared-hojea-al-abrir),$(cap 25.5 pared-hojea-ultima),$(cap 28.5 pared-hojea-503),$(cap 33 pared-hojea-tras-el-503)"
    ( sleep 25.7; control "5xx?n=2" ) &
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    grep '/v1/artists/' "$a" | sed 's/^/   /' | tr '\n' ';' | cut -c1-900; echo
    na=$(grep -c '/v1/artists/art50/albums' "$a")
    si "hojea: sale con 0" test "$(cat $o/salio)" = 0
    si "hojea: hojeando deprisa, las paginas que nadie miro no se pidieron ($na peticiones de la pared, de las 30 que harian falta para todas; tope 16)" test "$na" -le 16
    si "hojea: la ultima pagina (off=280 y off=290) se pidio una vez cada una y llego" bash -c "for n in 280 290; do [ \$(grep -c 'art50/albums app 200 t=album,single off='\$n'$' '$a') = 1 ] || exit 1; done"
    x=$(grep -m1 'art50/albums app 503 ' "$a" | sed 's/.* off=//')
    si "hojea: el 503 a media pared: el bloque que faltaba en la 16a (off=${x:-ninguno}) dio 503 (la peticion y su reintento) y despues, al volver a mirar la pagina, llego una vez" bash -c "[ -n '$x' ] && [ \$(grep -c 'art50/albums app 503 t=album,single off=$x$' '$a') = 2 ] && [ \$(grep -c 'art50/albums app 200 t=album,single off=$x$' '$a') = 1 ] && [ \$(grep -n 'art50/albums app 200 t=album,single off=$x$' '$a' | cut -d: -f1) -gt \$(grep -n 'art50/albums app 503 ' '$a' | tail -1 | cut -d: -f1) ] && for n in 260 270; do [ \$(grep -c 'art50/albums app 200 t=album,single off='\$n'$' '$a') = 1 ] || exit 1; done"
    si "hojea: la bitacora dice el 503 de la pagina y nada de las que se dejaron" bash -c "grep -q 'no llego una pagina de la pared: la API contesto 503' '$d/rocola_os_bitacora.txt' && ! grep -q 'ya no se mira' '$d/rocola_os_bitacora.txt'"
    si "hojea: el estado cuenta las dejadas (dejadas=N, N > 0)" bash -c "grep -qE '^dejadas=[1-9][0-9]*$' '$o/run/rocola-xp/spotify/catalogo'" ;;
  atajos503)
    # (revision cuota-hilos-y-fallos) LOS ATAJOS DE SP9 CON SPOTIFY CAIDO AL ABRIR EL BUSCADOR: los dos dan 503 (sin reintento: no los espera
    # nadie mirando); se cierra el buscador y, abierto otra vez antes del minuto, nada se pide; pasado el minuto (una moneda despierta de la
    # atraccion), al abrirlo se piden otra vez, llegan y en «Tus mas escuchadas» PONER pide su 1a. Ni una peticion mas.
    apifalsa $e; d=$(datos $e cuenta)
    ( sleep 7.6; control "5xx?n=2" ) &
    abre $e "$d" "7:67,8:27+38,10:27+38,30:27+38,31.5:27+38,69:67,70.5:27+38,73:38,73.5:38,75:39,75.5:13,77.5:13" 80 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 9.5 buscador-atajos-503),$(cap 72.5 buscador-atajos-tras-el-minuto)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    grep '/v1/me/' "$a" | sed 's/^/   /'
    si "atajos503: sale con 0" test "$(cat $o/salio)" = 0
    si "atajos503: lo de la cuenta de la letra (me/tracks, me/albums, me/playlists) NO se pide (24-11-2026, A1: sin la letra en la pared no se ve)" bash -c "! grep -qE 'GET /v1/me/(tracks|albums|playlists) ' '$a'"
    si "atajos503: cada atajo, un 503 y despues (pasado el minuto) un 200, nada mas" bash -c "for r in top/tracks player/recently-played; do [ \$(grep -c \"GET /v1/me/\$r cuenta 503\" '$a') = 1 ] && [ \$(grep -c \"GET /v1/me/\$r cuenta 200\" '$a') = 1 ] && [ \$(grep -c \"/v1/me/\$r\" '$a') = 2 ] || exit 1; done"
    si "atajos503: la 1a pedida es la 1a de tus mas escuchadas (can0012)" test "$(pedido $o 1)" = "$(printf 'pedida\tspotify:track:can0012')"
    si "atajos503: solo esa pedida ($(pedidos $o))" test "$(pedidos $o)" = 1
    si "atajos503: la bitacora dice que un atajo no llego (503)" contiene "$d/rocola_os_bitacora.txt" "un atajo no llego: la API contesto 503" ;;
  sinllave)
    # (2a revision funcionamiento-y-reglas) EL MODO ENCENDIDO (como viene de fabrica en la junta) SIN NINGUNA LLAVE (ni la de la API ni la
    # cuenta): las mismas teclas que apagado/cero (buscar, elegir, la pared de XP, un disco de XP, REGRESAR, ARRIBA, REGRESAR): ni una
    # peticion, ninguna pared ni vuelta del buscador, nada cobrado, y la traza igual a la de sin ROCOLA_OS quitando lo de RocolaOs
    apifalsa $e
    T="$BASE,$(escribe 9.5 inventad),$(derechas 14 14),17:13,19:27+38,20.5:27,21:13,22.5:27,23:38,23.5:27"
    d=$(datos $e apagado); rm -f "$d/rocola_spotify_api.txt" "$d/rocola_os.txt"; abre $e "$d" "$T" 26 ROCOLA_OS=1   # (la junta) sin rocola_os.txt: el modo va ENCENDIDO DE FABRICA
    espera $e; o="$W/$e"
    si "sinllave: sale con 0" test "$(cat $o/salio)" = 0
    si "sinllave: ninguna peticion a la API falsa (ni la llave)" test "$(wc -l < $o/api/peticiones.txt)" = 0
    si "sinllave: ninguna pared ni vuelta del buscador en la bitacora" bash -c "! grep -qE 'pared del buscador|vuelta al buscador' '$d/rocola_os_bitacora.txt' 2>/dev/null"
    si "sinllave: nada cobrado de Spotify ($(pedidos $o)) ni un play al Soloist falso" bash -c "[ $(pedidos $o) = 0 ] && [ -z \"\$(plays '$o')\" ]"
    if [ -f "$W/apagado/traza.txt" ]; then
      python3 "$XP/Pruebas/Ventanas/guiones/compara_trazas.py" "$W/apagado/traza.txt" "$o/traza.txt" --ignora RocolaOs: --holgura 80 > "$o/compara.txt" 2>&1; r=$?
      sed 's/^/   /' "$o/compara.txt" | head -8
      si "sinllave: la traza igual a la de sin ROCOLA_OS quitando lo de RocolaOs (compara_trazas.py --ignora, holgura 80)" test $r = 0
    fi ;;
  ayuda)
    # (2a revision funcionamiento-y-reglas; CAMBIADA el 26-11-2026: antes la ayuda se abria en medio de la pila, y el dueno pidio que NO se abra
    # dentro del buscador ni de sus paredes y discos: g280, escena sinanidar) LA AYUDA (EL ASISTENTE) EN MEDIO DE LA PILA NO SE ABRE. En la pared de
    # Banda, ROJO+ABAJO: el rojo es REGRESAR (el buscador) y ABAJO con el buscador abierto no abre la estrella; PONER abre la pared otra vez, PONER
    # un disco de ella (casilla 1), ROJO+ABAJO: la pared y la estrella tampoco se abre; REGRESAR: el buscador con lo escrito. El rojo va PRIMERO y
    # mantenido 1 s, y ABAJO 0,3 s despues (como la escena asistente de las ventanas)
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),17:13,19.5:27~1000,19.8:40,22.5:13,24.5:13,26.5:27~1000,26.8:40,29.5:27,31:13" 36 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 21.5 ayuda-sobre-el-buscador-desde-la-pared),$(cap 24 pared-otra-vez),$(cap 28.5 ayuda-sobre-la-pared-desde-un-disco),$(cap 30.5 buscador-al-final-de-ayuda)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    si "ayuda: sale con 0" test "$(cat $o/salio)" = 0
    # la ayuda abierta se veria en su captura: el circulo verde de «Poner una cancion» del globo (948,350: con el modo y la llave la pregunta
    # lleva cuatro opciones, un poco mas juntas -asistente-spotify, g76-; con las tres de XP iria en 948,369); cerrada, no
    ca="${CAPTURAS:-$W}"; verde() { python3 -c "import sys;from PIL import Image;p=Image.open(sys.argv[1]).convert('RGB').getpixel((948,350));sys.exit(0 if (p[1]>=200 and p[0]<160)==(sys.argv[2]=='1') else 1)" "$ca/$1.png" "$2"; }
    si "ayuda: la ayuda NO se abrio ni sobre el buscador (desde la pared) ni sobre la pared (desde un disco de ella) (capturas) ni hay rastro en la bitacora" bash -c "$(declare -f verde); ca='$ca'; verde ayuda-sobre-el-buscador-desde-la-pared 0 && verde ayuda-sobre-la-pared-desde-un-disco 0 && verde buscador-al-final-de-ayuda 0 && ! grep -q 'la ayuda con lo de Spotify' '$d/rocola_os_bitacora.txt'"
    si "ayuda: el buscador seguia con lo escrito (REGRESAR de la pared, y otra vez al final) y la pared se abrio tres veces (PONER al final la abre otra vez)" bash -c "[ \$(grep -c 'pared del buscador: pared_artista (41 casillas)' '$d/rocola_os_bitacora.txt') = 3 ] && [ \$(grep -c 'REGRESAR: de vuelta al buscador con «interminable»' '$d/rocola_os_bitacora.txt') = 2 ]"
    si "ayuda: nada pedido ($(pedidos $o)) y de Orquesta nada" bash -c "[ $(pedidos $o) = 0 ] && ! grep -q '/v1/.*art99' '$a'" ;;
  miente)
    # (revision cuota-hilos-y-fallos, 2a pasada) EL «TOTAL» DE SEARCH QUE DICE DE MAS: Spotify da en search un «total» estimado que puede
    # quedarse muy por encima (aqui 1000 para los 41 discos de «interminable»). Se abre «Todos los discos» y ARRIBA hasta la 3a pagina (la
    # ultima de verdad: off=40 trae 1) y otra vez; despues 7 s sin tocar nada. Sin el arreglo, cada bloque vacio corria la lista, la pagina
    # pedia los siguientes y asi solo hasta off=990 (unas 95 peticiones); con el, la lista se acaba donde se acaba: off=0 a 50 como mucho
    apifalsa $e; d=$(datos $e encendido); control "miente?total=1000"
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),16.4:40,16.8:40,17.2:40,17.6:40,18:40,18.4:40,19.5:13,22:38,23.5:38,25.5:38,27:38" 35 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 21.5 pared-todos-miente),$(cap 33 pared-todos-miente-al-final)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    na=$(grep -c 't=album off=' "$a")
    si "miente: sale con 0" test "$(cat $o/salio)" = 0
    si "miente: las 5 paginas de verdad (off=0 a 40) una vez cada una" bash -c "for n in 0 10 20 30 40; do [ \$(grep -c 'GET /v1/search app 200 q=interminable t=album off='\$n'$' '$a') = 1 ] || exit 1; done"
    si "miente: nada mas alla del final ($na paginas de la busqueda de discos; como mucho 6: off=50 puede salir con la 3a pagina)" bash -c "[ $na -le 6 ] && ! grep -qE 't=album off=([6-9][0-9]|[1-9][0-9][0-9])$' '$a'"
    si "miente: la pared en la bitacora (con el total que dijo Spotify, 1000 casillas)" contiene "$d/rocola_os_bitacora.txt" "pared del buscador: pared_todos (1000 casillas)" ;;
  portadas)
    # (revision cuota-hilos-y-fallos, 2a pasada) LAS PORTADAS QUE NO SE BAJARON POR LA RED: el servidor de imagenes da 503 a todo hasta los
    # 21 s (el buscador y la pared de Banda Interminable ya se abrieron); antes se quedaban sin portada toda la sesion. Ahora cada una se pide
    # otra vez a los 15 s (y a los 60 s): todas llegan, como mucho 3 veces cada una, y la bitacora lo dice una sola vez
    apifalsa $e; d=$(datos $e encendido); control "imgfalla?n=100000"
    ( sleep 21; control "imgfalla?n=0" ) &
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),17:13" 42 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 20 pared-portadas-sin-red),$(cap 40 pared-portadas-al-volver-la-red)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    n503=$(grep -c ' /img/.* img 503' "$a"); n200=$(grep -c ' /img/.* img 200' "$a")
    echo "   portadas: $n503 con 503, $n200 con 200"
    si "portadas: sale con 0" test "$(cat $o/salio)" = 0
    si "portadas: cada portada que dio 503 llego despues (200) y ninguna se pidio mas de 3 veces ($n503 con 503, $n200 con 200)" bash -c "[ $n503 -gt 0 ] && for f in \$(grep ' img 503' '$a' | awk '{print \$2}' | sort -u); do grep -q \"GET \$f img 200\" '$a' || exit 1; [ \$(grep -c \"GET \$f img\" '$a') -le 3 ] || exit 1; done"
    si "portadas: estan en /run (las de la pared y el buscador: $(ls $o/run/rocola-xp/spotify/portadas 2>/dev/null | grep -c 'jpg$'))" bash -c "[ \$(ls '$o/run/rocola-xp/spotify/portadas' | grep -c 'jpg$') -ge $n200 ] && [ $n200 -gt 0 ]"
    si "portadas: la bitacora lo dice una sola vez ($(grep -c 'una portada no se bajo' $d/rocola_os_bitacora.txt) renglones)" bash -c "[ \$(grep -c 'una portada no se bajo (503)' '$d/rocola_os_bitacora.txt') = 1 ]" ;;
  inactivo)
    # LO QUE SE QUEDA ABIERTO SE CIERRA SOLO (22-11-2026, el dueno: «si un cliente se queda dentro de una pared de discos temporal de
    # Spotify, despues de cierto tiempo se debe cerrar eso y el buscador [...] y tambien si alguien se queda dentro de un disco, despues de
    # 30 segundos se saldra automaticamente de el»). (1) la pared de Banda (17,5 s) sin tocar nada: a los 59 s sigue, a los 61 s se cerro
    # toda y queda la pared de XP de antes (la moneda de los 81 s despierta la atraccion); (2) «mentira»: su disco desde el buscador (90,5 s):
    # a los 31 s, la pared de XP (no el buscador); (3) la pared de Banda y su 1a casilla («Canciones de»): a los 31 s, la pared de XP;
    # (4) un disco de XP (PONER en la pared de XP): a los 30 s, su pared, como siempre
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),17:13,81:67,82:27+38,$(escribe 83.5 mentira),$(derechas 86.5 14),90.5:13,124:67,125:27+38,$(escribe 126.5 interminable),$(derechas 130.5 14),133.5:13,136:13,170:13" 206 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 80 inactivo-pared-xp-tras-60-s),$(cap 119.5 inactivo-disco-del-buscador-a-los-29-s),$(cap 123 inactivo-pared-xp-tras-30-s-en-el-disco),$(cap 165 inactivo-disco-de-la-pared-a-los-29-s),$(cap 168.5 inactivo-pared-xp-tras-30-s-en-el-disco-de-la-pared),$(cap 171.5 inactivo-disco-xp),$(cap 199 inactivo-disco-xp-a-los-29-s),$(cap 203 inactivo-pared-xp-tras-30-s-en-el-disco-xp)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; b="$d/rocola_os_bitacora.txt"; cuota "$a"; ca="${CAPTURAS:-$W}"
    grep 'se cierra todo lo del buscador' "$b" | sed 's/^/   /'
    si "inactivo: sale con 0" test "$(cat $o/salio)" = 0
    si "inactivo: la pared del buscador se cerro sola a los 60 s (una vez)" bash -c "[ \$(grep -c 'se cierra todo lo del buscador (una pared del buscador, 60 s sin tocar nada)' '$b') = 1 ]"
    si "inactivo: el disco del buscador se cerro solo a los 30 s (una vez)" bash -c "[ \$(grep -c 'se cierra todo lo del buscador (un disco del buscador, 30 s sin tocar nada)' '$b') = 1 ]"
    si "inactivo: el disco de la pared del buscador se cerro solo a los 30 s (una vez)" bash -c "[ \$(grep -c 'se cierra todo lo del buscador (un disco de una pared del buscador, 30 s sin tocar nada)' '$b') = 1 ]"
    l=$(grep 'se cierra todo lo del buscador' "$b" | sed 's/.*letra //' | sort -u)
    si "inactivo: las tres veces quedo la pared de XP en la letra de antes de abrir el buscador (letra $(echo $l | tr '\n' ' '))" bash -c "[ \$(echo '$l' | wc -l) = 1 ] && [ -n '$l' ] && [ '$l' != '♪' ]"
    # (a los 59 s la atraccion de XP -40 s- tapa la pared; la hora del cierre la dice la bitacora)
    rojo61=$(colores "$ca/inactivo-pared-xp-tras-60-s.png" 228 72 72 300 58 424 36)
    si "inactivo: tras los 60 s, la pared de XP sin el letrero de la pared del buscador ($rojo61 puntos rojos)" test "$rojo61" -lt 40
    dd=$(distintos "$ca/inactivo-disco-del-buscador-a-los-29-s.png" "$ca/inactivo-pared-xp-tras-30-s-en-el-disco.png" 0 100 1024 600 40)
    si "inactivo: a los 29 s el disco del buscador seguia abierto y a los 31 s no ($dd puntos distintos)" test "$dd" -gt 20000
    dx=$(distintos "$ca/inactivo-disco-xp-a-los-29-s.png" "$ca/inactivo-pared-xp-tras-30-s-en-el-disco-xp.png" 0 100 1024 600 40); dp=$(distintos "$ca/inactivo-pared-xp-tras-30-s-en-el-disco-de-la-pared.png" "$ca/inactivo-pared-xp-tras-30-s-en-el-disco-xp.png" 0 100 1024 600 40)
    si "inactivo: el disco de XP salio solo a los 30 s a su pared, como siempre ($dx puntos distintos del disco; $dp de la pared de antes)" test "$dx" -gt 20000 -a "$dp" -lt 20000
    si "inactivo: el buscador no se volvio a abrir solo (solo las 3 veces que se abrio a mano)" bash -c "[ \$(grep -c 'pared del buscador: pared_artista (41 casillas)' '$b') = 2 ]"
    si "inactivo: nada de Spotify pedido ($(pedidos $o))" test "$(pedidos $o)" = 0 ;;
  cargando)
    # (23-11-2026) EL ANILLO DE CARGA: con Spotify 1,2 s mas lento (/control/lento) y «tema» escrito, el buscador NO ensena resultados mientras espera:
    # la casilla dice «Buscando…», a los 150 ms sale el anillo (arco de color de genero girando una vuelta cada 0,9 s) con «Cargando…» debajo, y al
    # llegar Spotify salen las tarjetas de XP y de Spotify juntas y el anillo se va. Capturas: durante la espera (dos angulos) y despues.
    apifalsa $e; d=$(datos $e encendido); control "lento?ms=1700"
    abre $e "$d" "$BASE,$(escribe 9.5 tema)" 17 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 10.6 cargando-1),$(cap 11.35 cargando-2),$(cap 11.95 cargando-3),$(cap 14.5 cargando-resultados)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; ca="${CAPTURAS:-$W}"
    si "cargando: sale con 0" test "$(cat $o/salio)" = 0
    si "cargando: la busqueda a Spotify se hizo una vez con todo lo escrito (q=tema)" bash -c "grep '/v1/search' '$a' | tail -1 | grep -q ' q=tema\$'"
    # el anillo: dentro de los resultados, centrado, a un cuarto de su alto (RocolaOs.BuscadorCargando.cs): su recuadro, de 220x110, tomado de la captura
    A1=$(python3 "$G/anillo.py" "$ca/cargando-1.png" "$ca/cargando-2.png" "$ca/cargando-3.png" "$ca/cargando-resultados.png" 2>&1); echo "$A1" | sed 's/^/   /'
    si "cargando: durante la espera sale el anillo (a unos 0,5 s de empezar a esperar, en su sitio)" bash -c "echo '$A1' | grep -q 'ring1=ok'"
    si "cargando: el anillo gira (el arco no esta en el mismo sitio en dos capturas con 0,4 s)" bash -c "echo '$A1' | grep -q 'gira=ok'"
    si "cargando: sin tarjetas mientras se espera, y con ellas (y sin anillo) al llegar Spotify" bash -c "echo '$A1' | grep -q 'tarjetas=ok'"
    si "cargando: el aviso de XP «Sin resultados para...» no sale mientras se espera (el sitio de arriba de los resultados queda vacio)" bash -c "echo '$A1' | grep -q 'aviso=ok'"
    si "cargando: la casilla de arriba cambia al llegar (de «Buscando…» al numero)" bash -c "echo '$A1' | grep -q 'casilla=ok'" ;;
  recorte)
    # EL RECORTE DE /run (24-11-2026, A8): al abrir el buscador, un hilo de fondo suma portadas y miniaturas de Spotify de /run y, si pasan del tope
    # (aqui 100 KB, ROCOLA_SPOTIFY_RECORTE_BYTES), borra las de acceso mas viejo, nunca las de menos de 30 min ni las protegidas. Se preparan en /run
    # 20 portadas falsas de 10 KB y 10 miniaturas, todas de hace dos horas salvo una portada de ayer-noche y dos recientes (de hace 5 min)
    apifalsa $e; d=$(datos $e encendido); o="$W/$e"
    r="$o/run/rocola-xp/spotify"; mkdir -p "$r/portadas" "$r/miniaturas"
    for i in $(seq 1 20); do head -c 10240 /dev/zero > "$r/portadas/vieja$i.jpg"; done
    for i in $(seq 1 10); do head -c 10240 /dev/zero > "$r/miniaturas/mini$i.png"; done
    touch -d '2 hours ago' "$r"/portadas/vieja*.jpg "$r"/miniaturas/mini*.png
    for i in 1 2; do head -c 10240 /dev/zero > "$r/portadas/reciente$i.jpg"; touch -d '5 minutes ago' "$r/portadas/reciente$i.jpg"; done
    abre $e "$d" "$BASE" 14 ROCOLA_OS=1 ROCOLA_SPOTIFY_RECORTE_BYTES=102400
    espera $e
    n=$(ls "$r/portadas" "$r/miniaturas" | grep -cE 'jpg|png'); mb=$(cat "$r"/portadas/* "$r"/miniaturas/* 2>/dev/null | wc -c)
    si "recorte: sale con 0" test "$(cat $o/salio)" = 0
    si "recorte: queda por debajo del tope de 100 KB entre las dos carpetas ($mb bytes en $n archivos)" test "$mb" -le 102400
    si "recorte: se borraron las mas viejas (y quedan algunas: no se barrio todo)" bash -c "[ $n -gt 2 ] && [ \$(ls '$r/portadas' | grep -c vieja) -lt 20 ]"
    si "recorte: las recientes (5 min) siguen" bash -c "[ -f '$r/portadas/reciente1.jpg' ] && [ -f '$r/portadas/reciente2.jpg' ]" ;;
  sinanidar)
    # NI OTRO BUSCADOR NI LA ESTRELLA DENTRO DE LO DEL BUSCADOR (26-11-2026, el dueno: «se puede abrir otro buscador dentro de una pared de discos
    # temporal, y dentro del buscador se puede abrir el asistente, tampoco se debe poder abrir el asistente dentro de una pared de discos
    # temporal»; g279 en Form1.cs, g280 en AsistenteRocola.cs; RocolaOs.BuscadorSinAnidar.cs). OJO: en una pared o un disco del buscador el rojo
    # (REGRESAR) actua AL BAJAR (g53, g54): el rojo solo ya vuelve (del disco a la pared, de la pared al buscador) y la 2a tecla del combo llega
    # cuando ya se esta un paso mas atras. La pared de Banda se abre a los 17 s y su disco a los 19,5 s:
    #  a) del disco, ROJO+ARRIBA (21,5 s): el rojo vuelve a la pared y ARRIBA no abre otro buscador encima de ella (antes si)
    #  c) del disco otra vez (25 s), ROJO+ABAJO (26,5 s): vuelve a la pared y la estrella no se abre encima de ella (antes si)
    #  b) ROJO+ABAJO en la pared (30,5 s): el rojo vuelve al buscador con lo escrito y la estrella no se abre encima de el (antes si)
    #  d) en la pared de XP: ROJO+ARRIBA abre el buscador (38,5 s) y lo cierra (41,5 s), ROJO+ABAJO abre la estrella (45 s) y el rojo la cierra (49 s)
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(derechas 14 14),17:13,19.5:13,21.5:27~1000,21.8:38,25:13,26.5:27~1000,26.8:40,30.5:27~1000,30.8:40,34.5:27~1000,34.8:38,38.5:27~1000,38.8:38,41.5:27~1000,41.8:38,45:27~1000,45.3:40,49:27" 53 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 18.5 sinanidar-pared),$(cap 20.8 sinanidar-disco),$(cap 23.5 sinanidar-pared-tras-rojo-arriba),$(cap 28.5 sinanidar-pared-tras-rojo-abajo),$(cap 32.5 sinanidar-buscador-tras-rojo-abajo),$(cap 36.5 sinanidar-pared-de-xp),$(cap 40.5 sinanidar-buscador-abierto-en-xp),$(cap 43.5 sinanidar-pared-de-xp-otra-vez),$(cap 47.5 sinanidar-estrella-en-xp),$(cap 51 sinanidar-estrella-cerrada)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; cuota "$a"
    ca="${CAPTURAS:-$W}"; b="$d/rocola_os_bitacora.txt"
    verde() { python3 -c "import sys;from PIL import Image;p=Image.open(sys.argv[1]).convert('RGB').getpixel((948,350));sys.exit(0 if (p[1]>=200 and p[0]<160)==(sys.argv[2]=='1') else 1)" "$ca/$1.png" "$2"; }
    dif() { distintos "$ca/sinanidar-$1.png" "$ca/sinanidar-$2.png" 0 0 1024 768 40; }
    si "sinanidar: sale con 0" test "$(cat $o/salio)" = 0
    si "sinanidar: la pared de Banda se abrio una vez (17 s) y ninguna mas" test "$(cuenta $b 'pared del buscador: pared_artista \(41 casillas\)')" = 1
    si "sinanidar: el disco de la pared se abrio ($(dif pared disco) puntos distintos de la pared)" test "$(dif pared disco)" -gt 20000
    si "sinanidar a: del disco, ROJO+ARRIBA volvio a la pared y no abrio otro buscador ($(dif pared pared-tras-rojo-arriba) puntos distintos de la pared)" test "$(dif pared pared-tras-rojo-arriba)" -lt 20000
    si "sinanidar c: del disco, ROJO+ABAJO volvio a la pared y no abrio la estrella (sin el circulo verde; $(dif pared pared-tras-rojo-abajo) puntos distintos de la pared)" bash -c "$(declare -f verde); ca='$ca'; verde sinanidar-pared-tras-rojo-abajo 0 && [ $(dif pared pared-tras-rojo-abajo) -lt 20000 ]"
    si "sinanidar b: de la pared, ROJO+ABAJO volvio al buscador con lo escrito (una vez) y no abrio la estrella (sin verde; $(dif pared buscador-tras-rojo-abajo) puntos distintos de la pared: es el buscador)" bash -c "$(declare -f verde); ca='$ca'; verde sinanidar-buscador-tras-rojo-abajo 0 && [ $(dif pared buscador-tras-rojo-abajo) -gt 20000 ] && [ $(cuenta $b 'REGRESAR: de vuelta al buscador con «interminable»') = 1 ]"
    si "sinanidar: la estrella no se abrio hasta la pared de XP (una sola apertura en la bitacora)" test "$(cuenta $b 'la ayuda con lo de Spotify')" = 1
    si "sinanidar d: en la pared de XP, ROJO+ARRIBA abrio el buscador ($(dif pared-de-xp buscador-abierto-en-xp) puntos distintos) y otro ROJO+ARRIBA lo cerro (vuelve la pared: $(dif pared-de-xp pared-de-xp-otra-vez))" bash -c "[ $(dif pared-de-xp buscador-abierto-en-xp) -gt 20000 ] && [ $(dif pared-de-xp pared-de-xp-otra-vez) -lt 20000 ]"
    si "sinanidar d: en la pared de XP, ROJO+ABAJO abrio la estrella (circulo verde) y el rojo la cerro" bash -c "$(declare -f verde); ca='$ca'; verde sinanidar-estrella-en-xp 1 && verde sinanidar-estrella-cerrada 0"
    si "sinanidar: nada pedido" test "$(pedidos $o)" = 0 ;;
  repetidas)
    # SIN REPETIDAS EN EL BUSCADOR (27-11-2026): «cancion 2» (tus dos «Cancion 2» de XP; la API falsa: la de Los Ejemplos, de 3,239 s como la tuya, y otra
    # de «Grupo Distinto»): la de Los Ejemplos es la MISMA grabacion de la tuya y no sale; la de otro artista si. Despues «cancion 3» (la de Spotify dice
    # «- Remastered 2012»: la misma) y «cancion 1» (la de Spotify es «(En Vivo)»: otra grabacion, sale)
    apifalsa $e; control nuevas; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe2 9.5 'cancion 2'),15:8,15.1:8,15.2:8,15.3:8,15.4:8,15.5:8,15.6:8,15.7:8,15.8:8,$(escribe2 16.5 'cancion 3'),24:8,24.1:8,24.2:8,24.3:8,24.4:8,24.5:8,24.6:8,24.7:8,24.8:8,$(escribe2 25.5 'cancion 1')" 36 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 14 repetidas-cancion-2),$(cap 22 repetidas-cancion-3),$(cap 33 repetidas-cancion-1)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; b="$d/rocola_os_bitacora.txt"
    grep 'ya estan en tu musica' "$b" | sort -u | sed 's/^/   /'
    si "repetidas: sale con 0" test "$(cat $o/salio)" = 0
    si "repetidas: «cancion 2»: la de Los Ejemplos no sale (la tuya si)" contiene "$b" "«Cancion 2» de Los Ejemplos"
    si "repetidas: «cancion 2»: la de Grupo Distinto se queda (otro artista)" nocontiene "$b" "«Cancion 2» de Grupo Distinto"
    si "repetidas: «cancion 3»: la «- Remastered 2012» tampoco sale (la remasterizada es la misma grabacion)" contiene "$b" "«Cancion 3 - Remastered 2012» de Los Ejemplos"
    si "repetidas: «cancion 1»: la «(En Vivo)» si sale (otra grabacion)" nocontiene "$b" "«Cancion 1 (En Vivo)»"
    si "repetidas: nada pedido" test "$(pedidos $o)" = 0 ;;
  ediciones)
    # SIN EDICIONES REPETIDAS Y «CANCIONES DE» FILTRADO (27-11-2026): «quinteto» da el artista «Quinteto Eco» (solo esta en Spotify: 7 discos, de ellos
    # Eco Total x3 -original, Remastered 2011 y Deluxe-, Eco Segundo x2 -Deluxe de 9 y 25th Anniversary de 5-, Eco En Vivo y Eco Total (Taylor's Version),
    # que son otros). Su pared: «Canciones de» y 4 discos (queda el original de Eco Total y, de Eco Segundo, el de mas canciones). Entrando a «Canciones
    # de Quinteto Eco» (21 s): la busqueda por artista trae 27 (24 suyas, dos versiones de otro grupo con su nombre en el titulo y un dueto donde
    # canta con otro): las 2 de otros se quitan y el dueto se queda
    apifalsa $e; control nuevas; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 quinteto),$(derechas 13.5 14),16:37,17:13,21:13" 28 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 19 ediciones-pared),$(cap 25 ediciones-canciones-de)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; b="$d/rocola_os_bitacora.txt"; cuota "$a"
    grep 'edicion repetida\|no son de' "$b" | sed 's/^.*  //' | sort -u | sed 's/^/   /'
    si "ediciones: sale con 0" test "$(cat $o/salio)" = 0
    si "ediciones: la pared del artista: 5 casillas (Canciones de + 4 discos)" contiene "$b" "pared del buscador: pared_artista (5 casillas)"
    si "ediciones: Eco Total (Remastered 2011) y (Deluxe Edition) no salen: queda «Eco Total»" bash -c "grep -q 'edicion repetida: «Eco Total (Remastered 2011)» no sale (queda «Eco Total»)' '$b' && grep -q 'edicion repetida: «Eco Total (Deluxe Edition)» no sale (queda «Eco Total»)' '$b'"
    si "ediciones: de Eco Segundo queda la Deluxe (9 canciones) y no sale la 25th Anniversary (5)" contiene "$b" "edicion repetida: «Eco Segundo - 25th Anniversary Edition» no sale (queda «Eco Segundo (Deluxe Edition)»)"
    si "ediciones: Eco En Vivo y Eco Total (Taylor's Version) SI salen (otros discos)" bash -c "! grep -q 'edicion repetida: «Eco En Vivo' '$b' && ! grep -q \"edicion repetida: «Eco Total (Taylor's\" '$b'"
    si "ediciones: «Canciones de Quinteto Eco» sin las 2 de otro grupo (la pagina 3; el dueto se queda)" contiene "$b" "2 canciones de la pagina 3 no son de Quinteto Eco (se quitan)"
    si "ediciones: nada pedido" test "$(pedidos $o)" = 0 ;;
  teclea)
    # ESPERAR MAS AL TECLEAR (27-11-2026): «inve» y, 0,5 s despues de la ultima letra, «ntad»: antes de esa pausa (300 ms) se preguntaba a Spotify por
    # «inve» y otra vez por «inventad» (2 busquedas); ahora (650 ms) la pausa de medio segundo no alcanza: UNA sola busqueda, con todo lo escrito
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(escribe 9.5 inve),$(escribe 10.75 ntad)" 16 ROCOLA_OS=1
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    echo "   busquedas: $(grep -c '/v1/search' "$a")"
    si "teclea: sale con 0" test "$(cat $o/salio)" = 0
    si "teclea: una sola busqueda, con todo lo escrito (q=inventad)" bash -c "[ \$(grep -c '/v1/search' '$a') = 1 ] && grep '/v1/search' '$a' | grep -q ' q=inventad\$'" ;;
  memoria)
    # LA MEMORIA EN DISCO (27-11-2026): tres arranques con el mismo disco aparte (ROCOLA_IA_DATOS) y una API falsa nueva cada vez: abrir «quinteto», su
    # pared y «Canciones de». (1) todo se pregunta y se guarda; (2) apagar y volver: la API NO ve ninguna peticion a /v1 (todo del disco); (3) con las
    # busquedas guardadas hace 30 h (vencen a las 12 h) se vuelven a preguntar, pero la pared (3 dias) y sus paginas siguen del disco. Sin disco aparte
    # (las otras escenas): nada en la carpeta de datos
    d=$(datos $e encendido); G1="$W/grande-memoria"; mkdir -p "$G1"
    T="$BASE,$(escribe 9.5 quinteto),$(derechas 13.5 14),16:37,17:13,21:13"
    for r in 1 2 3; do
      n="$e$r"; apifalsa $n; control nuevas
      [ $r = 3 ] && find "$G1/rocola_spotify_cache" -name 'busqueda-*' -exec touch -d '30 hours ago' {} +
      abre $n "$d" "$T" 27 ROCOLA_OS=1 ROCOLA_IA_DATOS="$G1"
      espera $n
      echo "   arranque $r: $(grep -c ' /v1/' "$W/$n/api/peticiones.txt") peticiones a /v1 ($(grep -c '/v1/search' "$W/$n/api/peticiones.txt") busquedas); $(ls "$G1/rocola_spotify_cache" 2>/dev/null | wc -l) archivos en la memoria"
    done
    c="$G1/rocola_spotify_cache"
    si "memoria: los tres arranques salieron con 0" bash -c "[ \$(cat $W/${e}1/salio) = 0 ] && [ \$(cat $W/${e}2/salio) = 0 ] && [ \$(cat $W/${e}3/salio) = 0 ]"
    si "memoria: 1.er arranque: pregunto a Spotify ($(grep -c ' /v1/' $W/${e}1/api/peticiones.txt)) y guardo ($(ls $c | wc -l) archivos)" bash -c "[ \$(grep -c ' /v1/' '$W/${e}1/api/peticiones.txt') -ge 4 ] && [ \$(ls '$c' | wc -l) -ge 4 ]"
    si "memoria: 2.o arranque: NINGUNA peticion a /v1 (todo del disco)" test "$(grep -c ' /v1/' $W/${e}2/api/peticiones.txt)" = 0
    si "memoria: 2.o arranque: el estado dice los aciertos del disco" bash -c "grep -E '^cache_aciertos=[1-9]' '$W/${e}2/run/rocola-xp/spotify/catalogo'"
    si "memoria: 3.er arranque: solo la busqueda vencida se volvio a preguntar (ninguna pagina de pared)" bash -c "[ \$(grep -c '/v1/search' '$W/${e}3/api/peticiones.txt') -ge 1 ] && ! grep -q '/v1/artists/' '$W/${e}3/api/peticiones.txt'"
    si "memoria: la memoria solo lleva datos publicos (sin el secreto, ni refresco, ni llaves)" bash -c "! grep -rlE 'secreto-de-prueba|refresco-|app-[0-9]+|cuenta-[0-9]+' '$c'"
    si "memoria: sin disco aparte, nada en la carpeta de datos" test ! -e "$d/rocola_spotify_cache" ;;
  hoja)
    # LA HOJA «SPOTIFY» DE CTRL+F4 (27-11-2026): Ctrl+F4, Tab, Tab (la pestana Spotify) e Intro: la hoja con el vigilante sembrado (corriendo, caduca en 82
    # dias), la API, las ultimas de la bitacora y la cache; despues, con Tab x4 (Archivos, Listo, Vaciar), el mando de paginas por letra: dos veces la flecha derecha (100 -> 200)
    # y Escape: guardado pared_paginas=200
    apifalsa $e; d=$(datos $e encendido); mkdir -p "$W/$e/run/rocola-soloist"
    printf 'fase=corriendo\nversion=1.3.10.86 build 20261119\ncaduca=2027-02-17\ndias=82\npid=1234\nreinicios=0\n' > "$W/$e/run/rocola-soloist/estado"
    printf '2026-11-27 21:02:11  acabo spotify:track:can0001 (Tema de Mentira): termino, sono 187.4 s [huecos]\n2026-11-27 21:08:40  DEVUELTA spotify:track:can0002 (Otra Tonada 2): Soloist no la empezo, sono 0.0 s [huecos]: se devuelve el credito\n2026-11-27 21:15:02  acabo spotify:track:can0003 (Cancion Inventada 3): termino, sono 201.0 s [huecos]\n' > "$d/rocola_spotify.txt"
    abre $e "$d" "6:17+115,7:9,7.3:9,7.6:13,8.6:9,8.8:9,9:9,9.2:9,10.2:39,10.5:39,12:27" 15 ROCOLA_OS=1 ROCOLA_IA_DATOS="$W/grande-hoja" ROCOLA_CAPTURA="$(cap 8.3 hoja-spotify),$(cap 11 hoja-spotify-ajustada)"
    espera $e; o="$W/$e"; b="$d/rocola_os_bitacora.txt"
    si "hoja: sale con 0" test "$(cat $o/salio)" = 0
    si "hoja: Ctrl+F4 abierta y sin fallos de Rocola OS en rocola_error.txt" bash -c "grep -q 'Ctrl+F4 abierta' '$b' && ! grep -q 'Rocola OS' '$d/rocola_error.txt' 2>/dev/null"
    si "hoja: la captura mide 1024x768" bash -c "python3 -c \"from PIL import Image;import sys;sys.exit(0 if Image.open('${CAPTURAS:-$W}/hoja-spotify.png').size==(1024,768) else 1)\""
    si "hoja: el mando guardo pared_paginas=200 y lo apunto" bash -c "grep -qx 'pared_paginas=200' '$d/rocola_os.txt' && grep -q 'paginas por letra de la pared: 200' '$b'"
    si "hoja: ni una llave en lo que se escribio" sinsecretos "$d" "$o/run" ;;
  esac
done

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; fi
[ $mal = 0 ]
