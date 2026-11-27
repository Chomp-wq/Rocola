#!/bin/bash
# LA PRUEBA DE LA PARED CON EL CATALOGO (fase 11, grupo pared-generos: RocolaOs/RocolaOs.ParedGeneros*.cs; FASE11_pared-generos.md; la
# decision del dueno del 20-11-2026: el orden de XP con los discos del catalogo intercalados y la pastilla del genero en cada disco). La rocola
# entera, fuera de pantalla, sin sonido, con datos INVENTADOS (los de partida de las escenas de ventanas, DATOS0, mas unos discos de prueba
# copiados de los suyos), la API web de Spotify FALSA (Pruebas/RocolaOs/catalogo/ApiFalsa, que ademas da el catalogo de prueba en /catalogo/ y
# las portadas del Cover Art Archive en /release-group/) y un catalogo de prueba INVENTADO y grande (catalogo.py: 9 000 discos en A, B y C).
# Nunca el Soloist de verdad (el falso del nucleo, como la prueba del catalogo).
#   bash prueba.sh PROGRAMA [escenas...]      PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   W (trabajo), API, FALSO, CAPTURAS como en Pruebas/RocolaOs/catalogo/prueba.sh
# (27-11-2026, la pared de Spotify vuelve) Un disco del catalogo solo entra en la pared con su portada A LA MANO (RocolaOs.ParedPortadas.cs): aqui el
# paquete de portadas lo arma sistema/catalogo/arma_portadas.py contra el Cover Art Archive FALSO de la API falsa (404 a los mbid que acaban en 0) y
# se siembra en la carpeta de cada escena (SIN_PAQUETE=1: sin paquete); no entran los discos sin portada ni los marcados (bandas sonoras, videojuegos...).
# Escenas nuevas: exclusiones (lo que no va), nombres (el parecido de nombres), mano (la portada a la mano y el relleno), paquete (la descarga del
# paquete) y letra (la ♫ con el icono de Spotify a 1024x768).
# (junta 3, revision) con ROCOLA_OS=1 el cielo pide el lugar y el clima por internet sin llave, y la letra va a LRCLIB: en la prueba,
# todo eso va al sumidero 127.0.0.1:7 (nada escucha) para no depender de la red ni de lo que conteste un servicio de fuera; una escena
# que ponga su propio falso lo pone delante y manda
export ROCOLA_CLIMA_API="${ROCOLA_CLIMA_API:-http://127.0.0.1:7}" ROCOLA_LUGAR_API="${ROCOLA_LUGAR_API:-http://127.0.0.1:7/lugar}"
export ROCOLA_LETRA_API="${ROCOLA_LETRA_API:-http://127.0.0.1:7}" ROCOLA_GENIUS_API="${ROCOLA_GENIUS_API:-http://127.0.0.1:7}"
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
escenas="${*:-orden pastilla hojea hojeaxp abre portadas tope descarga contrato real sincatalogo quitallave apagado cero sinllave exclusiones nombres mano paquete letra}"
T=${T:-/root/rocola-trabajo/fase11/pared-generos}
API=${API:-$T/api-falsa}
REAL=${REAL:-$T/catalogo-real}   # el catalogo de verdad (escena real), bajado aparte de la version «catalogo»
W=${W:-$T/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
CAPTURAS=${CAPTURAS:-}
FALSO=${FALSO:-$T/falso}
CAT=$W/catalogo
CA="$G/../catalogo"; SX="$G/../spotify-xp"
# (se rehace si su Program.cs no es el de esta rama: la carpeta la comparten los agentes de varias ramas; antes contaba la fecha)
if [ ! -f "$API/api-falsa.dll" ] || ! cmp -s "$CA/ApiFalsa/Program.cs" "$API/Program.cs"; then
  echo "(compilando la API falsa en $API)"; mkdir -p "$API"
  flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$CA/ApiFalsa/ApiFalsa.csproj" -c Release -o "$API" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$API/obj/" > "$API/build.txt" 2>&1 || { cat "$API/build.txt"; exit 1; }
  rm -rf "$API/obj"
  cp "$CA/ApiFalsa/Program.cs" "$API/Program.cs"
fi
AL=$(dirname "$FALSO")/alsa; mkdir -p "$AL"
[ -f "$AL/libasound_module_pcm_reloj.so" ] || gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
# (se rehace si su Program.cs no es el de esta rama: la carpeta la comparten los agentes de varias ramas; antes contaba la fecha)
if [ ! -f "$FALSO/soloist-falso.dll" ] || ! cmp -s "$SX/SoloistFalso/Program.cs" "$FALSO/Program.cs"; then
  echo "(compilando el Soloist falso en $FALSO)"; mkdir -p "$FALSO"
  flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$SX/SoloistFalso/SoloistFalso.csproj" -c Release -o "$FALSO" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$FALSO/obj/" > "$FALSO/build.txt" 2>&1 || { cat "$FALSO/build.txt"; exit 1; }
  rm -rf "$FALSO/obj"
  cp "$SX/SoloistFalso/Program.cs" "$FALSO/Program.cs"
fi
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
python3 "$G/catalogo.py" "$CAT" 9000 12000 > "$W/catalogo.txt" || exit 1
cat "$W/catalogo.txt"
bien=0; mal=0; falta=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
est() { grep -m1 "^$2=" "$1/run/rocola-xp/pared/estado" 2>/dev/null | cut -d= -f2-; }   # un valor del estado de la pared

P=$((20000 + ($$ * 7) % 20000))
apifalsa() {   # NOMBRE [CATALOGO]: una API falsa nueva para la escena (con CATALOGO, da esa carpeta en /catalogo/)
  local o="$W/$1/api"; mkdir -p "$o"; P=$((P + 1))
  API_CATALOGO="${2:-}" dotnet "$API/api-falsa.dll" $P "$o" id-de-prueba secreto-de-prueba refresco-1 > "$o/salida.txt" 2>&1 &
  APIPID=$!
  for i in $(seq 1 100); do [ -f "$o/listo" ] && break; sleep 0.1; done
  : > "$o/peticiones.txt"
}
control() { curl -s --noproxy '*' "http://127.0.0.1:$P/control/$1" > /dev/null; }
# LA ROCOLA: abre NOMBRE DATOS "PULSA" SEGUNDOS [VAR=valor...]; lo grande (y el catalogo) en $W/NOMBRE/disco
abre() {
  local n="$1" d="$2" pulsa="$3" seg="$4"; shift 4
  local o="$W/$n"; mkdir -p "$o/run" "$o/soloist" "$o/disco"
  # (27-11-2026) el paquete de portadas, sembrado en la carpeta del catalogo de la escena (junto a lo grande de la IA, ROCOLA_IA_DATOS/../catalogo)
  local ia="$o/disco/ia-xp" x
  for x in "$@"; do case "$x" in ROCOLA_IA_DATOS=*) ia="${x#ROCOLA_IA_DATOS=}";; esac; done
  if [ -z "${SIN_PAQUETE:-}" ] && [ -f "$PAQ/rocola_portadas.txt" ]; then siembra "$(dirname "$ia")/catalogo/portadas-pack"; fi
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" > "$o/alsa.conf"
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_SPOTIFY_API="http://127.0.0.1:$P/v1" ROCOLA_SPOTIFY_CUENTAS="http://127.0.0.1:$P" \
      ROCOLA_CATALOGO_URL="http://127.0.0.1:$P/catalogo" ROCOLA_PORTADAS_CATALOGO="http://127.0.0.1:$P" ROCOLA_CATALOGO_ESPERA_S=0 \
      ROCOLA_IA_DATOS="$o/disco/ia-xp" ROCOLA_PARED_VOLCADO="$o/volcado" \
      ROCOLA_SOLOIST_DATOS="$o/soloist" ROCOLA_SOLOIST_BIN="$SX/vigilante-falso.sh" SOLOIST_FALSO_BIN="$FALSO" \
      SOLOIST_FALSO_LOG="$o/falso.txt" VIGILANTE_FALSO_LOG="$o/vigilante.txt" \
      "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  ROCOLA=$!
  date +%s%3N > "$o/inicio_ms"   # (rapido-spotify: la hora de Unix del arranque, para contar lo que pidio en un rato: tiempos.txt de la API falsa)
  ( sleep "$seg"; kill -TERM $ROCOLA 2>/dev/null; sleep 30; kill -KILL $ROCOLA 2>/dev/null ) & VIG=$!
}
espera() { wait $ROCOLA; echo $? > "$W/$1/salio"; rm -f "$W/$1/sonado.raw"; kill $VIG 2>/dev/null; wait $VIG 2>/dev/null; kill $APIPID 2>/dev/null; wait $APIPID 2>/dev/null; }
# LOS DATOS: los de partida con unos discos de prueba (copias de los MP3 de los datos de partida, en carpetas cuyo nombre dice el genero, como
# las lee XP: NombreDice) y lo de la escena
datos() {   # NOMBRE MODO(apagado|encendido|sinllave)
  local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/
  rm -f "$d/rocola_biblioteca.txt" "$d/rocola_fuentes.txt"
  local m="$d/musica" s="$DATOS0/musica/Los Ejemplos/Disco Uno"
  # («Artista - Disco», como las nombra XP: SplitArtist; los acentos, sin ellos a proposito: el catalogo los trae con ellos)
  for x in "Angeles Ficticios - Cumbias del Recuerdo" "Alma Nortena - Corridos Bravos" "Amor Inventado - Boleros de Siempre" "Alfa Rockera - Rock de Prueba" \
           "Arena Tropical - Salsa Inventada" "Banda Ejemplar - Banda en Vivo"; do mkdir -p "$m/$x"; cp "$s"/*.mp3 "$m/$x/"; done
  case "$2" in
    encendido) printf 'spotify=1\npared_paginas=%s\n' "${PAGINAS:-500}" > "$d/rocola_os.txt"; printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\n' > "$d/rocola_spotify_api.txt"; chmod 600 "$d/rocola_spotify_api.txt" ;;
    sinllave) printf 'spotify=1\n' > "$d/rocola_os.txt" ;;
    cuenta) printf 'spotify=1\n' > "$d/rocola_os.txt"; printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\nrefresh_token=refresco-1\nusuario=dueno-falso\n' > "$d/rocola_spotify_api.txt"; chmod 600 "$d/rocola_spotify_api.txt" ;;   # (27-11-2026) con la cuenta: la letra ♫
    apagado) printf 'spotify=0\n' > "$d/rocola_os.txt"; printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\n' > "$d/rocola_spotify_api.txt"; chmod 600 "$d/rocola_spotify_api.txt" ;;
  esac
  echo "$d"
}
cap() { [ -n "$CAPTURAS" ] && echo "$1:$CAPTURAS/$2.png" || echo "$1:$W/$2.png"; }
[ -n "$CAPTURAS" ] && mkdir -p "$CAPTURAS"
# N veces la tecla VK desde el segundo S, cada PASO s
veces() { local s="$1" n="$2" vk="$3" paso="${4:-0.15}" r="" i; for ((i = 0; i < n; i++)); do r="$r,$(echo "$s + $i * $paso" | bc):$vk"; done; echo "${r#,}"; }
v1() { grep -c ' /v1/' "$1" 2>/dev/null; }
# EL PAQUETE DE PORTADAS (27-11-2026): empaca CARPETA_DEL_CATALOGO: arma_portadas.py contra el Cover Art Archive FALSO (una API falsa aparte), en la
# misma carpeta (la API falsa la da en /catalogo/); las portadas bajadas se guardan para las siguientes (W/cache-caa)
empaca() {
  local c="$1" n; n=$(basename "$c"); apifalsa "empaca-$n" ""
  python3 "$XP/../../catalogo/arma_portadas.py" arma --catalogo "$c/rocola_catalogo.tsv.gz" --salida "$c" --cache "$W/cache-caa" --base "http://127.0.0.1:$P" \
    --max-discos 1000000 --max-mb 3000 --parte-mb 60 --hilos 8 2> "$W/empaca-$n.txt" || { cat "$W/empaca-$n.txt"; exit 1; }
  kill $APIPID 2>/dev/null; wait $APIPID 2>/dev/null
  echo "paquete de $n: $(tail -1 "$W/empaca-$n.txt")"
}
# SEMBRAR EL PAQUETE en la carpeta de una escena (enlaces a las partes y su «.ok»: la rocola las lee al arrancar, sin red); PAQ: de que catalogo es
PAQ="$CAT"
siembra() {   # DESTINO
  local dst="$1" f; mkdir -p "$dst"
  for f in "$PAQ"/rocola_portadas_*.pak; do [ -e "$f" ] || continue; ln -sf "$f" "$dst/$(basename "$f")"; sed -n "s/^parte[0-9]*=$(basename "$f"),\([0-9a-f]*\),.*/\1/p" "$PAQ/rocola_portadas.txt" > "$dst/$(basename "$f").ok"; done
}
# lo que se hace en todas: moneda a los 7 s
BASE="7:67"
# el paquete de portadas del catalogo de prueba, una vez (lo siembran las escenas; solo si alguna lo necesita: son 30 s)
for e in $escenas; do case $e in orden|pastilla|hojea|hojeaxp|abre|portadas|tope|quitallave|paquete) empaca "$CAT"; break ;; esac; done

for e in $escenas; do
  echo "== $e"
  case "$e" in
  orden)
    # EL ORDEN: la letra A armada (volcado) en el orden de XP (artista y disco) con los del catalogo intercalados; 500 paginas; lo de atras del
    # catalogo (los artistas menos famosos) fuera; los tuyos que el catalogo tambien trae, una sola vez; la pastilla de cada disco; cero
    # peticiones a la API
    apifalsa $e "$CAT"; d=$(datos $e encendido)
    abre $e "$d" "$BASE" 22 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 16 pared-generos-letra-a)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "orden: sale con 0" test "$(cat $o/salio)" = 0
    si "orden: el catalogo se bajo (1 vez el .txt y el .tsv.gz) y quedo en el disco de la rocola" bash -c "[ \$(grep -c 'GET /catalogo/rocola_catalogo.txt ' '$a') = 1 ] && [ \$(grep -c 'GET /catalogo/rocola_catalogo.tsv.gz ' '$a') = 1 ] && [ -f '$o/disco/catalogo/rocola_catalogo.tsv.gz' ]"
    si "orden: ninguna peticion a la API de Spotify (/v1: $(v1 $a))" test "$(v1 $a)" = 0
    si "orden: la letra A armada" test -f "$o/volcado/letra-A.tsv"
    python3 "$G/verifica.py" "$o/volcado/letra-A.tsv" "$CAT/rocola_catalogo.tsv.gz" A 5 prueba > "$o/verifica.txt" 2>&1; r=$?
    sed 's/^/   /' "$o/verifica.txt"
    si "orden: verifica.py (orden de XP, intercalados, tuyo antes, sin repetidos, 500 paginas, los mas famosos primero, la pastilla de cada uno)" test $r = 0
    for L in B numeral; do
      LL=$L; [ $L = numeral ] && LL='#'
      python3 "$G/verifica.py" "$o/volcado/letra-$L.tsv" "$CAT/rocola_catalogo.tsv.gz" "$LL" $(awk -F'\t' '$3=="xp"' "$o/volcado/letra-$L.tsv" 2>/dev/null | wc -l) > "$o/verifica-$L.txt" 2>&1; r=$?
      sed 's/^/   /' "$o/verifica-$L.txt" | head -4
      si "orden: la letra $LL (armada por adelantado, una vecina) tambien" test $r = 0
    done
    si "orden: el estado dice letra A, 500 paginas y pastillas en la pagina que se ve ($(est $o pagina_con_pastilla) de 18)" bash -c "[ '$(est $o letra)' = A ] && [ '$(est $o paginas)' = 500 ] && [ '$(est $o pagina_con_pastilla)' -ge 1 ]"
    si "orden: las casillas se hornearon con su pastilla ($(est $o casillas_con_pastilla))" test "$(est $o casillas_con_pastilla)" -ge 1 ;;
  pastilla)
    # LA PASTILLA EN CADA CASILLA: la primera pagina de la A, dos veces: con las pastillas y con ROCOLA_PARED_SIN_PASTILLAS=1 (la misma
    # pared sin pastillas). Casilla por casilla: cambia la cara de las que llevan pastilla y no cambia la de las que no (la elegida, el cubo
    # que gira, no cuenta). Despues, con las pastillas, a la B (su primer disco es tuyo), solo para la captura
    V="$W/orden/volcado/letra-A.tsv"; [ -f "$V" ] || { echo "(hace falta la escena orden antes)"; mal=$((mal + 1)); continue; }
    awk -F'\t' '$1 < 18 {printf "%s:%s ", $1, $2}' "$V" > "$W/pastillas-pagina.txt"
    R=$(awk -F'\t' '$4=="Alfa Rockera"{print $1; exit}' "$V"); pg=$((R / 18))
    echo "   las pastillas de la primera pagina: $(cat $W/pastillas-pagina.txt); «Alfa Rockera» en la casilla $R (pagina $((pg + 1)))"
    for x in con sin; do
      apifalsa $e$x "$CAT"; d=$(datos $e$x encendido)
      if [ $x = sin ]; then
        abre $e$x "$d" "$BASE" 18 ROCOLA_OS=1 ROCOLA_PARED_SIN_PASTILLAS=1 ROCOLA_CAPTURA="$(cap 15 pared-generos-pastillas-$x)"
      else
        # (y despues a la B: su primer disco es «Banda Ejemplar - Banda en Vivo», tuyo; un paso a la derecha para que no sea el elegido)
        abre $e$x "$d" "$BASE,18:27+39,20.5:39" 26 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 15 pared-generos-pastillas-$x),$(cap 23 pared-generos-pastillas-tus-discos)"
      fi
      espera $e$x
      si "pastilla ($x): sale con 0 y ninguna peticion a la API (/v1: $(v1 $W/$e$x/api/peticiones.txt))" bash -c "[ '$(cat $W/$e$x/salio)' = 0 ] && [ $(v1 $W/$e$x/api/peticiones.txt) = 0 ]"
    done
    C1=$( [ -n "$CAPTURAS" ] && echo "$CAPTURAS" || echo "$W")
    python3 - "$C1/pared-generos-pastillas-con.png" "$C1/pared-generos-pastillas-sin.png" "$(cat $W/pastillas-pagina.txt)" 0 > "$W/$e-pixeles.txt" 2>&1 <<'PY'
import sys
from PIL import Image
a, b = Image.open(sys.argv[1]).convert("RGB"), Image.open(sys.argv[2]).convert("RGB")
pas = dict(x.split(":") for x in sys.argv[3].split())
sel = int(sys.argv[4]) % 18 if sys.argv[4] else -1
mal = 0
for i in range(18):
    c, r = i % 6, i // 6
    cx, y0 = 97 + 166 * c, 112 + 185 * r
    caja = (cx - 47, y0, cx + 47, y0 + 131)   # la cara del disco en 1024x768
    n = sum(1 for p, q in zip(a.crop(caja).get_flattened_data() if hasattr(a, 'get_flattened_data') else a.crop(caja).getdata(), b.crop(caja).get_flattened_data() if hasattr(b, 'get_flattened_data') else b.crop(caja).getdata()) if sum(abs(u - v) for u, v in zip(p, q)) > 60)
    lleva = pas.get(str(i), "-") != "-"
    bien = i == sel or (n > 400 if lleva else n < 250)   # (revision: las estrellas del cielo que titilan detras de una casilla vacia cambian ~100 puntos; una pastilla, 700 o mas)
    if not bien: mal += 1
    print("casilla %2d %-12s %5d puntos distintos%s%s" % (i, pas.get(str(i), "-"), n, " (la elegida)" if i == sel else "", "" if bien else "  MAL"))
sys.exit(1 if mal else 0)
PY
    r=$?; sed 's/^/   /' "$W/$e-pixeles.txt"
    si "pastilla: cambia la cara de las casillas con pastilla y no la de las que no llevan" test $r = 0 ;;
  hojea|hojeaxp)
    # HOJEAR DEPRISA: ARRIBA mantenido 40 s (la repeticion de XP: a los 500 ms, ~30 por segundo) desde la primera pagina de la A: las 500
    # paginas sin tirones (lo que tarda cada cuadro), la memoria con tope, cero peticiones a la API y casi ninguna portada (solo las de la
    # pagina en que se para). hojeaxp: lo mismo con el modo apagado (la pared de XP, que da la vuelta a sus letras de 1 pagina), para comparar
    apifalsa $e "$CAT"
    if [ $e = hojea ]; then d=$(datos $e encendido); M="ROCOLA_PARED_VIVO=1"; else d=$(datos $e apagado); M="ROCOLA_PARED_MIDE=1"; fi
    abre $e "$d" "$BASE,12:38~40000" 60 ROCOLA_OS=1 ROCOLA_PARED_RELLENO=0 $M ROCOLA_CAPTURA="$(cap 30 pared-generos-hojeando-$e),$(cap 56 pared-generos-tras-hojear-$e)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    echo "   $(grep -E '^(paginas_vistas|letra|pagina|hojeo_cuadros|hojeo_p50_ms|hojeo_p95_ms|hojeo_p99_ms|hojeo_max_ms|hojeo_de_mas_de_50ms|hojeo_de_mas_de_100ms|todos_p50_ms|todos_p95_ms|todos_p99_ms|todos_max_ms|memoria_mb_max|gc_mb_max|vivo_mb_max|portadas_pedidas|portadas_dejadas|armadas|armada_ms_max)=' $o/run/rocola-xp/pared/estado | tr '\n' ' ')"
    si "$e: sale con 0" test "$(cat $o/salio)" = 0
    si "$e: ninguna peticion a la API de Spotify (/v1: $(v1 $a))" test "$(v1 $a)" = 0
    if [ $e = hojea ]; then
      si "hojea: pasaron las 500 paginas de la A ($(est $o paginas_vistas) vistas)" test "$(est $o paginas_vistas)" -ge 500
      # (rapido-spotify) desde que las de las paginas de al lado y las de la primera pagina de las letras de al lado se bajan por adelantado
      # (de la pagina donde empieza y de donde se para: hasta 7 paginas de 18 cada vez), lo que se cuenta es lo pedido MIENTRAS se hojea (de
      # los 12,5 a los 52 s: ARRIBA mantenido), que tiene que seguir siendo casi nada; el total, solo se dice
      hj=$(awk -v i="$(cat $o/inicio_ms)" '$4 ~ /^\/release-group\// && $1 >= i + 12500 && $1 <= i + 52000' "$o/api/tiempos.txt" | wc -l)
      # (revision) mientras se hojea, como la base (unos 16; el defecto de 6a49a70 pedia 64-148): 30 como mucho. Y el total con tope (la
      # base, 90 como mucho; con lo adelantado, 174-186): 200, para ver si el adelanto vuelve a gastar de mas en la red y el disco del bar
      ht=$(grep -c 'GET /release-group/' $a)
      si "hojea: portadas pedidas al Cover Art Archive mientras se hojea: $hj de 30 como mucho ($(est $o portadas_dejadas) dejadas sin pedir)" test "$hj" -le 30
      si "hojea: portadas pedidas en total: $ht de 200 como mucho (las de la 1a pagina, las de donde se paro y las de al lado de las dos)" test "$ht" -le 200
      # (27-11-2026) lo que se mide es lo que SE QUEDA en la memoria tras recoger la basura (vivo_mb_max, ROCOLA_PARED_VIVO=1: un GC completo cada
      # segundo, solo en esta prueba), no el monton tal cual (gc_mb_max, que lleva la basura que aun no se recogio: con la misma pared oscilaba entre
      # 116 y 266 MB segun cuando corriera el recolector y la carga de la maquina, y fallaba el tope sin que nada se hubiera quedado de mas). Una
      # fuga aparece aqui como un vivo que sigue subiendo con las paginas; con las 4 letras en memoria medi 34 MB
      si "hojea: la memoria con tope (lo que se queda tras recoger la basura: $(est $o vivo_mb_max) MB de 100 como mucho; el monton con basura: $(est $o gc_mb_max) MB; el tamano en la memoria: $(est $o memoria_mb_max) MB)" test "$(est $o vivo_mb_max)" -le 100
      si "hojea: en la memoria como mucho 4 letras ($(est $o en_memoria))" test "$(est $o en_memoria | wc -c)" -le 5
    else
      si "hojeaxp: nada del catalogo (ni el .txt ni portadas)" bash -c "! grep -q 'catalogo\|release-group' '$a'"
    fi ;;
  abre)
    # ABRIR UN DISCO DEL CATALOGO: uno con id de Spotify (1 peticion; PONER pide su 1a por I6); REGRESAR a la misma casilla; uno que Spotify no
    # tiene (404: el aviso de XP y se esconde), uno sin id que la busqueda no encuentra (se esconde) y uno sin id que si (1 busqueda y el disco)
    V="$W/orden/volcado/letra-A.tsv"; [ -f "$V" ] || { echo "(hace falta la escena orden antes)"; mal=$((mal + 1)); continue; }
    I=$(python3 - "$V" "$CAT/rocola_catalogo.tsv.gz" <<'PY'
import sys, gzip
ids = {}
for l in gzip.open(sys.argv[2], "rt", encoding="utf-8"):
    c = l.rstrip("\n").split("\t")
    if c[0] == "D": ids[c[4]] = c[3]
for l in open(sys.argv[1], encoding="utf-8"):
    f = l.rstrip("\n").split("\t")
    if int(f[0]) < 18 and f[2] == "catalogo" and ids.get(f[5], "").startswith("cat") and not ids.get(f[5], "").startswith("catno"): print(f[0], ids[f[5]], f[4]); break
PY
)
    i0=$(echo "$I" | cut -d' ' -f1); id0=$(echo "$I" | cut -d' ' -f2)
    R=$(awk -F'\t' '$5=="Disco Que No Esta"{print $1; exit}' "$V"); pg=$((R / 18)); sl=$((R % 18))
    echo "   con id: la casilla $i0 ($id0); «Disco Que No Esta» en la casilla $R (pagina $((pg + 1)))"
    apifalsa $e "$CAT"; d=$(datos $e encendido)
    T="$BASE"; t=12; [ $i0 -gt 0 ] && T="$T,$(veces $t $i0 39 0.3)"; t=$(echo "$t + $i0 * 0.3 + 1" | bc)
    T="$T,$t:13"; tc1=$(echo "$t + 3" | bc); T="$T,$(echo "$t + 4" | bc):13,$(echo "$t + 6" | bc):27"; t=$(echo "$t + 8" | bc)
    if [ $pg = 0 ]; then   # (en la misma pagina que el de antes: de su casilla a la de «Disco Que No Esta»)
      dl=$((R - i0)); if [ $dl -gt 0 ]; then T="$T,$(veces $t $dl 39 0.3)"; elif [ $dl -lt 0 ]; then T="$T,$(veces $t $((-dl)) 37 0.3)"; fi
      t=$(echo "$t + ${dl#-} * 0.3 + 1" | bc)
    else
      T="$T,$(veces $t $pg 38 0.3)"; t=$(echo "$t + $pg * 0.3 + 1" | bc)
      [ $sl -gt 0 ] && T="$T,$(veces $t $sl 39 0.3)"; t=$(echo "$t + $sl * 0.3 + 1" | bc)
    fi
    tc2=$(echo "$t + 1.5" | bc); T="$T,$t:13"; t=$(echo "$t + 4" | bc)
    T="$T,$t:13"; t=$(echo "$t + 4" | bc)
    T="$T,$t:13"; tc3=$(echo "$t + 3" | bc); T="$T,$(echo "$t + 4.5" | bc):27"; t=$(echo "$t + 7" | bc)
    abre $e "$d" "$T" $(echo "$t + 2" | bc | cut -d. -f1) ROCOLA_OS=1 ROCOLA_PARED_RELLENO=0 ROCOLA_CAPTURA="$(cap $tc1 pared-generos-disco-del-catalogo),$(cap $tc2 pared-generos-disco-que-spotify-no-tiene),$(cap $tc3 pared-generos-disco-buscado-por-nombre)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; b="$d/rocola_os_bitacora.txt"
    grep -E 'pared_generos\] (se abre|el disco)' "$b" | sed 's/^.*\] /   /'
    si "abre: sale con 0" test "$(cat $o/salio)" = 0
    si "abre: el disco con id, 1 peticion (/v1/albums/$id0)" test "$(grep -c "GET /v1/albums/$id0 " $a)" = 1
    si "abre: PONER pidio su 1a cancion por I6 (spotify:track:c${id0}1)" bash -c "grep -q 'COBRADA spotify:track:c${id0}1 ' '$d/rocola_spotify.txt'"
    si "abre: «Disco Que No Esta» (catno0001): 404 y escondido" bash -c "grep -q 'GET /v1/albums/catno0001 .* 404' '$a' && grep -q 'Disco Que No Esta» no llego (Spotify no lo tiene' '$b'"
    si "abre: «Disco Sin Spotify»: 1 busqueda, ninguno, escondido" bash -c "[ \$(grep -c 'q=album:\"Disco Sin Spotify\"' '$a') = 1 ] && grep -q 'Disco Sin Spotify» no llego (Spotify no lo tiene' '$b'"
    si "abre: «Disco Uno Por Nombre»: 1 busqueda y el disco (catb...)" bash -c "[ \$(grep -c 'q=album:\"Disco Uno Por Nombre\"' '$a') = 1 ] && [ \$(grep -c 'GET /v1/albums/catb' '$a') = 1 ]"
    si "abre: dos escondidos y la eleccion en su sitio (casilla $R: $(est $o elegido))" bash -c "[ '$(est $o ocultos)' = 2 ] && [ '$(est $o elegido)' = $R ]"
    si "abre: en total 5 peticiones a /v1 ($(v1 $a))" test "$(v1 $a)" = 5 ;;
  portadas)
    # LAS PORTADAS DEL CATALOGO, A LA MANO (27-11-2026; antes se bajaban del Cover Art Archive al verse): solo entran en la pared los discos cuya
    # portada ya esta en el PAQUETE (sembrado aqui) o en la cache; al verse una pagina (y por adelantado las de al lado y la que sigue hacia donde se
    # va) sus portadas se SACAN del paquete a la cache del disco (un archivo chico, sin red): ninguna peticion al Cover Art Archive. Sin portada
    # no hay disco. A la 2a vez, la cache ya las tiene
    apifalsa $e "$CAT"; d=$(datos $e encendido)
    abre $e "$d" "$BASE,14:38,18:40" 22 ROCOLA_OS=1 ROCOLA_PARED_RELLENO=0 ROCOLA_IA_DATOS="$W/disco-portadas/ia-xp"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; n1=$(grep -c 'GET /release-group/' $a)
    pc="$W/disco-portadas/catalogo/portadas"; ex=$(est $o paquete_extraidas)
    echo "   portadas: $n1 pedidas a la red, $ex sacadas del paquete, $(ls $pc/*.jpg 2>/dev/null | wc -l) en la cache, $(ls $pc/miniaturas 2>/dev/null | wc -l) miniaturas"
    si "portadas: sale con 0" test "$(cat $o/salio)" = 0
    si "portadas: ninguna al Cover Art Archive (todo salio del paquete): $n1" test "$n1" = 0
    si "portadas: solo las de 2 paginas vistas, las 2 de al lado y la 1a de las 2 letras de al lado, por adelantado (de 108 casillas): $ex sacadas" test "$ex" -le 108 -a "$ex" -ge 20
    si "portadas: la cache del disco tiene las sacadas y ninguna de los mbid que acaban en 0 (sin portada)" bash -c "[ \$(ls '$pc'/*.jpg | wc -l) -ge 20 ] && ! ls '$pc' | grep -q '0\.jpg\$'"
    si "portadas: las miniaturas de XP de estas portadas van a su cache (no a rocola_miniaturas)" bash -c "[ \$(ls '$pc/miniaturas' | wc -l) -ge 1 ]"
    si "portadas: la previa de 32 px no va a previews32 (nada de la cache del catalogo en el indice de XP)" bash -c "! grep -qa 'disco-portadas/catalogo' '$d'/rocola_miniaturas/previews32* 2>/dev/null"
    apifalsa ${e}2 "$CAT"; d=$(datos ${e}2 encendido)
    abre ${e}2 "$d" "$BASE,14:38,18:40" 22 ROCOLA_OS=1 ROCOLA_IA_DATOS="$W/disco-portadas/ia-xp"
    espera ${e}2; o2="$W/${e}2"
    si "portadas: la 2a vez, de la cache del disco: del paquete solo sacadas las que faltaban ($(est $o2 paquete_extraidas) de $ex)" test "$(est $o2 paquete_extraidas)" -le $((ex / 4)) ;;
  tope)
    # EL TOPE DE LA CACHE DE PORTADAS (1 MB para la prueba; 300 de fabrica): hojeando 10 paginas despacio, se borran las mas viejas y, medida
    # (cada 15 s, con las miniaturas que hace XP), queda debajo
    apifalsa $e "$CAT"; d=$(datos $e encendido)
    abre $e "$d" "$BASE,$(veces 12 10 38 2.5)" 58 ROCOLA_OS=1 ROCOLA_PARED_PORTADAS_MB=1
    espera $e; o="$W/$e"; pc="$o/disco/catalogo/portadas"
    kb=$(du -sk --apparent-size "$pc" | cut -f1)
    echo "   bajadas $(est $o portadas_bajadas), borradas $(est $o portadas_borradas), en el disco $kb KB"
    si "tope: sale con 0" test "$(cat $o/salio)" = 0
    si "tope: se borraron las de mas ($(est $o portadas_borradas)) y quedo debajo del tope de 1 MB ($kb KB de archivos; se mide cada 15 s, con las miniaturas)" bash -c "[ '$(est $o portadas_borradas)' -gt 0 ] && [ $kb -le 1100 ]" ;;
  descarga)
    # BAJAR EL CATALOGO: (1) la 1a vez, el .txt y el .tsv.gz; (2) otra vez el mismo dia: nada (con el del disco); (3) pasado un dia y con otro
    # en la red: lo baja y se cambia; (4) uno que no es el que dice su sha256: no se usa y sigue el de antes
    DD="$W/disco-descarga/ia-xp"
    apifalsa ${e}1 "$CAT"; d=$(datos ${e}1 encendido); abre ${e}1 "$d" "$BASE" 14 ROCOLA_OS=1 ROCOLA_IA_DATOS="$DD"; espera ${e}1
    si "descarga 1: el .txt y el .tsv.gz una vez cada uno" bash -c "[ \$(grep -c 'GET /catalogo/rocola_catalogo.txt ' '$W/${e}1/api/peticiones.txt') = 1 ] && [ \$(grep -c 'GET /catalogo/rocola_catalogo.tsv.gz ' '$W/${e}1/api/peticiones.txt') = 1 ] && [ '$(est $W/${e}1 catalogo)' = bajado ]"
    apifalsa ${e}2 "$CAT"; d=$(datos ${e}2 encendido); abre ${e}2 "$d" "$BASE" 14 ROCOLA_OS=1 ROCOLA_IA_DATOS="$DD"; espera ${e}2
    si "descarga 2: el mismo dia, ninguna peticion al catalogo y el del disco ($(est $W/${e}2 catalogo), $(est $W/${e}2 catalogo_discos) discos)" bash -c "! grep -q '/catalogo/' '$W/${e}2/api/peticiones.txt' && [ '$(est $W/${e}2 catalogo)' = guardado ] && [ '$(est $W/${e}2 catalogo_discos)' = 30074 ]"
    C2="$W/catalogo2"; python3 "$G/catalogo.py" "$C2" 8000 > /dev/null
    echo $(( $(date +%s) - 2 * 86400 )) > "$W/disco-descarga/catalogo/rocola_catalogo_mirado.txt"
    apifalsa ${e}3 "$C2"; d=$(datos ${e}3 encendido); abre ${e}3 "$d" "$BASE" 14 ROCOLA_OS=1 ROCOLA_IA_DATOS="$DD"; espera ${e}3
    si "descarga 3: pasado un dia, el nuevo ($(est $W/${e}3 catalogo), $(est $W/${e}3 catalogo_discos) discos)" bash -c "[ '$(est $W/${e}3 catalogo)' = bajado ] && [ '$(est $W/${e}3 catalogo_discos)' = 24074 ]"
    C3="$W/catalogo3"; python3 "$G/catalogo.py" "$C3" 7000 > /dev/null; cp "$C2/rocola_catalogo.txt" "$C3/rocola_catalogo.txt"
    echo $(( $(date +%s) - 2 * 86400 )) > "$W/disco-descarga/catalogo/rocola_catalogo_mirado.txt"
    # (el .txt de la red dice el sha256 del de antes: el mismo que el del disco, asi que no se baja nada; con otro sha256 y un .gz que no es:)
    printf 'sha256=%064d\nbytes=%d\n' 7 $(stat -c %s "$C3/rocola_catalogo.tsv.gz") > "$C3/rocola_catalogo.txt"
    apifalsa ${e}4 "$C3"; d=$(datos ${e}4 encendido); abre ${e}4 "$d" "$BASE" 14 ROCOLA_OS=1 ROCOLA_IA_DATOS="$DD"; espera ${e}4
    si "descarga 4: uno que no es el de su sha256 no se usa: sigue el de antes ($(est $W/${e}4 catalogo), $(est $W/${e}4 catalogo_discos) discos)" bash -c "grep -q 'no es el que dice su sha256' '$W/datos-${e}4/rocola_os_bitacora.txt' && [ '$(est $W/${e}4 catalogo_discos)' = 24074 ]" ;;
  contrato)
    # EL CONTRATO «I8» CON EL GRUPO catalogo-datos: su archivo de prueba tal cual (contrato/rocola_catalogo_prueba.tsv.gz, copia del de
    # sistema/catalogo/prueba del armador juntado, con los 12 artistas trampa del pre-mapeo de generos: 187 artistas y 673 discos inventados
    # -se leen de su cabecera «#rocola-catalogo»-, las 27 letras, acentos, «Ñ», #, discos sin año, sin id y sin portada) y
    # un .txt con TODAS las claves que escribe su armador (formato, version, fecha, sha256, bytes, artistas, discos, fuentes, licencia)
    C4="$W/catalogo-contrato"; mkdir -p "$C4"; cp "$G/contrato/rocola_catalogo_prueba.tsv.gz" "$C4/rocola_catalogo.tsv.gz"
    read -r _ _ _ NART NDIS < <(zcat "$C4/rocola_catalogo.tsv.gz" | head -1)
    printf 'formato=1\nversion=20261114.1\nfecha=2026-11-15\nsha256=%s\nbytes=%d\nartistas=%s\ndiscos=%s\nfuentes=MusicBrainz, Wikidata, ListenBrainz\nlicencia=CC0 y CC BY-NC-SA 3.0\n' \
      "$(sha256sum "$C4/rocola_catalogo.tsv.gz" | cut -d' ' -f1)" "$(wc -c < "$C4/rocola_catalogo.tsv.gz")" "$NART" "$NDIS" > "$C4/rocola_catalogo.txt"
    empaca "$C4"; PAQ="$C4"   # (27-11-2026: sus portadas a la mano)
    apifalsa $e "$C4"; d=$(datos $e encendido)
    abre $e "$d" "$BASE,12:27+39" 20 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 11 pared-generos-catalogo-del-contrato)"
    PAQ="$CAT"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "contrato: sale con 0" test "$(cat $o/salio)" = 0
    si "contrato: el catalogo del grupo catalogo-datos se bajo y se leyo entero ($(est $o catalogo), $(est $o catalogo_discos) discos)" bash -c "[ '$(est $o catalogo)' = bajado ] && [ '$(est $o catalogo_discos)' = '$NDIS' ]"
    si "contrato: ninguna peticion a la API (/v1: $(v1 $a))" test "$(v1 $a)" = 0
    for L in A B; do
      python3 "$G/contrato.py" "$o/volcado/letra-$L.tsv" "$C4/rocola_catalogo.tsv.gz" $L $([ $L = A ] && echo 5 || echo 1) > "$o/verifica-$L.txt" 2>&1; r=$?
      sed 's/^/   /' "$o/verifica-$L.txt"
      si "contrato: la letra $L armada con los discos del archivo (nombres, pastilla) y los tuyos" test $r = 0
      python3 "$G/verifica.py" "$o/volcado/letra-$L.tsv" "$C4/rocola_catalogo.tsv.gz" $L $([ $L = A ] && echo 5 || echo 1) > "$o/verifica2-$L.txt" 2>&1; r=$?
      sed 's/^/   /' "$o/verifica2-$L.txt" | grep -v "^   tus discos"
      si "contrato: la letra $L en el orden de XP con los del catalogo intercalados (verifica.py)" test $r = 0
    done ;;
  real)
    # EL CATALOGO DE VERDAD (el de la version «catalogo» del repositorio, bajado aparte a $REAL; datos abiertos, sin llave): servido por la API
    # falsa, la letra A armada con tus 5 discos: 500 paginas, el orden de XP, los mas famosos primero; lo que tarda y la memoria
    # (sin el, sus 4 comprobaciones cuentan como FALTA y la bateria sale con 2: no se puede dar por buena sin ellas)
    [ -f "$REAL/rocola_catalogo.tsv.gz" ] || { echo "FALTA real: sin el catalogo de verdad en $REAL (REAL=...): sus 4 comprobaciones no se hicieron"; falta=$((falta + 4)); continue; }
    empaca "$REAL"; PAQ="$REAL"   # (27-11-2026: sus portadas a la mano; con el catalogo de verdad son 180 000 discos con portada del Cover Art Archive falso: tarda)
    apifalsa $e "$REAL"; d=$(datos $e encendido)
    abre $e "$d" "$BASE,14:38,15:38,16:38" 26 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 20 pared-generos-catalogo-real)"
    PAQ="$CAT"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    echo "   $(grep -E '^(catalogo|catalogo_discos|catalogo_kb|armadas|armada_ms_max|memoria_mb_max|gc_mb_max|pagina_con_pastilla|letra_con_pastilla)=' $o/run/rocola-xp/pared/estado | tr '\n' ' ')"
    si "real: sale con 0" test "$(cat $o/salio)" = 0
    si "real: el catalogo de verdad se leyo ($(est $o catalogo_discos) discos)" test "$(est $o catalogo_discos)" -gt 100000
    si "real: ninguna peticion a la API (/v1: $(v1 $a))" test "$(v1 $a)" = 0
    python3 "$G/verifica.py" "$o/volcado/letra-A.tsv" "$REAL/rocola_catalogo.tsv.gz" A 5 > "$o/verifica.txt" 2>&1; r=$?
    sed 's/^/   /' "$o/verifica.txt" | cut -c1-400
    si "real: la letra A con el catalogo de verdad (verifica.py)" test $r = 0 ;;
  sincatalogo)
    # SIN CATALOGO (la red no lo tiene): la pared de XP tal cual (la A con tus 5 discos, sin pastilla) y ninguna peticion a la API
    apifalsa $e ""; d=$(datos $e encendido)
    abre $e "$d" "$BASE" 18 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 14 pared-generos-sin-catalogo)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "sincatalogo: sale con 0" test "$(cat $o/salio)" = 0
    si "sincatalogo: se pidio el .txt (404) y nada mas; ni la API ($(v1 $a))" bash -c "grep -q 'GET /catalogo/rocola_catalogo.txt catalogo 404' '$a' && ! grep -q 'tsv.gz' '$a' && [ $(v1 $a) = 0 ]"
    si "sincatalogo: la pared de XP (va=$(est $o va), sin pastilla, sin letras armadas)" bash -c "[ '$(est $o va)' = no ] && [ -z '$(est $o pastilla)' ] && [ '$(est $o vista)' = 0 ] && [ ! -d '$o/volcado' ]" ;;
  quitallave)
    # (revision) LA LLAVE QUITADA A MEDIA SESION, en la pagina 3 de la A (casilla 38, del catalogo): la pared vuelve a la de XP al momento (sin
    # letras armadas en la memoria, sin pastillas) y la eleccion queda dentro de la lista de XP (en el tuyo que iba despues: el primero)
    apifalsa $e "$CAT"; d=$(datos $e encendido)
    ( sleep 17; rm -f "$d/rocola_spotify_api.txt" ) &
    abre $e "$d" "$BASE,12:38,13:38,14:39,14.5:39" 30 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 15 pared-generos-antes-de-quitar-la-llave),$(cap 27 pared-generos-llave-quitada)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    grep -E 'pared_generos\] (ya no va|letra A)' "$d/rocola_os_bitacora.txt" | sed 's/^.*\] /   /' | cut -c1-160
    si "quitallave: sale con 0 y ninguna peticion a la API (/v1: $(v1 $a))" bash -c "[ '$(cat $o/salio)' = 0 ] && [ $(v1 $a) = 0 ]"
    si "quitallave: antes, la A armada; despues, la pared de XP (va=$(est $o va), vista=$(est $o vista), en memoria «$(est $o en_memoria)»)" bash -c "grep -q 'letra A: 9000 discos' '$d/rocola_os_bitacora.txt' && grep -q 'ya no va' '$d/rocola_os_bitacora.txt' && [ '$(est $o va)' = no ] && [ '$(est $o vista)' = 0 ] && [ -z '$(est $o en_memoria)' ]"
    si "quitallave: la eleccion dentro de la lista de XP (pagina $(est $o pagina), elegido $(est $o elegido))" bash -c "[ '$(est $o pagina)' = 0 ] && [ '$(est $o elegido)' = 0 ]" ;;
  apagado|cero|sinllave)
    # SIN EL MODO: sin ROCOLA_OS (apagado), con ROCOLA_OS=1 y spotify=0 (cero) y con el modo encendido pero SIN la llave de la API (sinllave):
    # ni una peticion (ni el catalogo), ni pared por generos; la traza igual a la de sin ROCOLA_OS quitando lo de RocolaOs (cero y sinllave)
    apifalsa $e "$CAT"
    TT="$BASE,12:38,12.8:38,13.6:39,14.4:39,15.4:13,17:27,18:40,19:27+39,20:27+37"
    case $e in
      apagado) d=$(datos $e apagado); SIN_PAQUETE=1 abre $e "$d" "$TT" 23 ROCOLA_OS= ROCOLA_CAPTURA="$(cap 11 pared-xp-apagado)" ;;
      cero) d=$(datos $e apagado); SIN_PAQUETE=1 abre $e "$d" "$TT" 23 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 11 pared-xp-cero)" ;;
      sinllave) d=$(datos $e sinllave); SIN_PAQUETE=1 abre $e "$d" "$TT" 23 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 11 pared-xp-sin-llave)" ;;
    esac
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"
    si "$e: sale con 0" test "$(cat $o/salio)" = 0
    si "$e: ninguna peticion a la API falsa (ni el catalogo, ni portadas, ni la llave)" test "$(wc -l < $a)" = 0
    si "$e: nada del catalogo en el disco ni letras armadas" bash -c "[ ! -d '$o/disco/catalogo' ] && [ ! -d '$o/volcado' ]"
    if [ $e != apagado ] && [ -f "$W/apagado/traza.txt" ]; then
      python3 "$XP/Pruebas/Ventanas/guiones/compara_trazas.py" "$W/apagado/traza.txt" "$o/traza.txt" --ignora RocolaOs: --holgura 80 > "$o/compara.txt" 2>&1; r=$?
      sed 's/^/   /' "$o/compara.txt" | head -6
      si "$e: la traza igual a la de sin ROCOLA_OS quitando lo de RocolaOs (compara_trazas.py --ignora, holgura 80)" test $r = 0
    fi ;;
  exclusiones)
    # LO QUE NO VA EN LA PARED (27-11-2026): un catalogo chico a proposito (especial.py; la columna 7 son las marcas): bandas sonoras (por el nombre, sin
    # marca, como el catalogo ya publicado; y por el genero del artista), un videojuego (por la marca del archivo), karaoke (por el nombre del artista),
    # para dormir o meditar, infantil y un disco sin portada. Todas las portadas estan a la mano (sembradas en la cache): si no entran, es por la regla.
    # Y lo que ya tienes con otra edicion: «Cumbias del Recuerdo (Remastered 2015)» y «Corridos Bravos - Edicion Especial» de «Alma Norteña feat. Invitado»
    E="$W/catalogo-excl"; python3 "$G/especial.py" "$E" > "$W/$e-catalogo.txt"; cat "$W/$e-catalogo.txt"
    apifalsa $e "$E"; d=$(datos $e encendido); mkdir -p "$W/$e/disco/catalogo/portadas"
    while IFS=$'\t' read -r m resto; do curl -s --noproxy '*' -L "http://127.0.0.1:$P/release-group/$m/front-250" -o "$W/$e/disco/catalogo/portadas/$m.jpg"; done < "$E/mbids.txt"
    SIN_PAQUETE=1 abre $e "$d" "$BASE" 16 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 15 pared-generos-exclusiones)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; V="$o/volcado/letra-A.tsv"
    si "exclusiones: sale con 0 y ninguna peticion a la API de Spotify (/v1: $(v1 $a))" bash -c "[ '$(cat $o/salio)' = 0 ] && [ $(v1 $a) = 0 ]"
    si "exclusiones: la letra A armada" test -f "$V"
    python3 "$G/verifica_excl.py" "$V" "$E/mbids.txt" > "$o/verifica.txt" 2>&1; r=$?
    sed 's/^/   /' "$o/verifica.txt"
    si "exclusiones: solo entran los que van a la pared (cine, videojuego, karaoke, dormir, infantil y sin portada, fuera; lo que ya tienes con otra edicion, tampoco)" test $r = 0
    si "exclusiones: el estado cuenta los 7 excluidos ($(est $o excluidos))" test "$(est $o excluidos)" -ge 7 ;;
  nombres)
    # EL PARECIDO DE NOMBRES y las marcas, caso por caso, DENTRO de rocola-xp (ROCOLA_NOMBRES_PRUEBA): los de nombres.tsv (disco y artista) y los de
    # sistema/catalogo/prueba/exclusiones.tsv (las mismas palabras que usa el armador: marcas)
    { grep -v '^#' "$G/nombres.tsv"; awk -F'\t' '!/^#/ && NF >= 3 {print "marcas\t" $1 "\t" $2 "\t" $3 "\t" $4}' "$XP/../../catalogo/prueba/exclusiones.tsv"; } > "$W/nombres-casos.tsv"
    apifalsa $e "$CAT"; d=$(datos $e encendido)
    abre $e "$d" "$BASE" 10 ROCOLA_OS=1 ROCOLA_NOMBRES_PRUEBA="$W/nombres-casos.tsv"
    espera $e; o="$W/$e"; S="$W/nombres-casos.tsv.salida"
    grep -E '^(MAL|TOTAL|ERROR)' "$S" | sed 's/^/   /'
    si "nombres: sale con 0" test "$(cat $o/salio)" = 0
    si "nombres: $(grep -c '^BIEN' "$S") casos bien (disco, artista y marcas, las de arma_catalogo.py) y ninguno mal" bash -c "[ \$(grep -c '^MAL' '$S') = 0 ] && [ \$(grep -c '^BIEN' '$S') -ge 70 ]" ;;
  mano)
    # LA PORTADA A LA MANO Y EL RELLENO: sin paquete ni cache, un disco del catalogo NO entra (la A es la de XP con tus 5 discos); de fondo, el relleno
    # baja las primeras portadas de la letra (como mucho 500 de esta sesion, 2 hilos, del Cover Art Archive falso) y la pared se rearma con los discos
    # que ya tienen la suya (la eleccion no salta). Nunca una casilla esperando la red: lo que no esta, no entra
    MS="$W/catalogo-sinpaquete"; mkdir -p "$MS"; cp "$CAT"/rocola_catalogo.tsv.gz "$CAT"/rocola_catalogo.txt "$MS/"   # (el catalogo, sin el paquete: la API falsa no lo da)
    apifalsa $e "$MS"; d=$(datos $e encendido)
    SIN_PAQUETE=1 abre $e "$d" "$BASE" 48 ROCOLA_OS=1 ROCOLA_PARED_RELLENO=500 ROCOLA_CAPTURA="$(cap 10 pared-generos-sin-portadas),$(cap 44 pared-generos-con-lo-bajado)"
    espera $e; o="$W/$e"; a="$o/api/peticiones.txt"; b="$d/rocola_os_bitacora.txt"
    grep -E 'pared_generos\] letra A:' "$b" | sed 's/^.*\] /   /' | cut -c1-200
    si "mano: sale con 0 y ninguna peticion a la API de Spotify (/v1: $(v1 $a))" bash -c "[ '$(cat $o/salio)' = 0 ] && [ $(v1 $a) = 0 ]"
    si "mano: la 1.a vez que se armo la A no traia nada del catalogo (sin portadas a la mano) y esperaba portadas" bash -c "grep -E 'pared_generos\] letra A:' '$b' | head -1 | grep -qE ' 0 del catalogo.* [1-9][0-9]* esperando su portada'"
    si "mano: despues se armo con discos del catalogo (el relleno: $(est $o relleno_bajadas) bajadas de $(est $o relleno_encargadas) encargadas)" bash -c "grep -E 'pared_generos\] letra A:' '$b' | tail -1 | grep -qvE ' 0 del catalogo' && [ '$(est $o relleno_bajadas)' -ge 25 ]"
    si "mano: el relleno respeto su tope (500): $(est $o relleno_encargadas) encargadas, $(grep -c 'GET /release-group/' $a) pedidas al Cover Art Archive" bash -c "[ '$(est $o relleno_encargadas)' -le 500 ] && [ \$(grep -c 'GET /release-group/' '$a') -le 520 ]" ;;
  paquete)
    # LA DESCARGA DEL PAQUETE DE PORTADAS (sin llave, como el catalogo): el manifiesto (rocola_portadas.txt) y cada parte, comprobadas por bytes y sha256,
    # al disco de la rocola; se leen al momento (el indice); una vez al dia (la 2.a vez, del disco, sin pedir las partes); una parte que no es la que dice
    # su sha256 no se usa (y las demas si)
    DP="$W/disco-paquete/ia-xp"; NP=$(sed -n 's/^partes=//p' "$CAT/rocola_portadas.txt")
    apifalsa ${e}1 "$CAT"; d=$(datos ${e}1 encendido)
    SIN_PAQUETE=1 abre ${e}1 "$d" "$BASE" 30 ROCOLA_OS=1 ROCOLA_IA_DATOS="$DP"; espera ${e}1
    o="$W/${e}1"; a="$o/api/peticiones.txt"; pk="$W/disco-paquete/catalogo/portadas-pack"
    echo "   el paquete: $(est $o paquete), $(est $o paquete_portadas) portadas en $(est $o paquete_partes) partes (el manifiesto dice $NP); bajadas $(est $o paquete_bajadas)"
    si "paquete 1: sale con 0" test "$(cat $o/salio)" = 0
    si "paquete 1: el manifiesto una vez y cada parte una vez" bash -c "[ \$(grep -c 'GET /catalogo/rocola_portadas.txt ' '$a') = 1 ] && [ \$(grep -c 'GET /catalogo/rocola_portadas_[0-9]*.pak ' '$a') = $NP ]"
    si "paquete 1: al dia, con las $NP partes y sus «.ok» en el disco de la rocola" bash -c "[ '$(est $o paquete)' = 'al dia' ] && [ '$(est $o paquete_partes)' = $NP ] && [ \$(ls '$pk'/*.pak.ok | wc -l) = $NP ] && [ '$(est $o paquete_portadas)' -gt 1000 ]"
    apifalsa ${e}2 "$CAT"; d=$(datos ${e}2 encendido)
    SIN_PAQUETE=1 abre ${e}2 "$d" "$BASE" 14 ROCOLA_OS=1 ROCOLA_IA_DATOS="$DP"; espera ${e}2
    o="$W/${e}2"; a="$o/api/peticiones.txt"
    si "paquete 2: el mismo dia, ninguna parte pedida y el indice del disco ($(est $o paquete), $(est $o paquete_portadas) portadas)" bash -c "! grep -q 'rocola_portadas' '$a' && [ '$(est $o paquete)' = guardado ] && [ '$(est $o paquete_partes)' = $NP ]"
    # una parte con otro sha256: la 1.a del manifiesto
    M="$W/catalogo-malo"; rm -rf "$M"; mkdir -p "$M"; cp "$CAT"/rocola_catalogo.* "$M/"; ln -s "$CAT"/rocola_portadas_*.pak "$M/"
    awk -F'[=,]' '/^parte1=/ {printf "parte1=%s,%064d,%s\n", $2, 5, $4; next} {print}' "$CAT/rocola_portadas.txt" > "$M/rocola_portadas.txt"
    apifalsa ${e}3 "$M"; d=$(datos ${e}3 encendido)
    SIN_PAQUETE=1 abre ${e}3 "$d" "$BASE" 30 ROCOLA_OS=1 ROCOLA_IA_DATOS="$W/disco-paquete3/ia-xp"; espera ${e}3
    o="$W/${e}3"
    si "paquete 3: una parte que no es la de su sha256 no se usa (la bitacora lo dice) y las demas si ($(est $o paquete_partes) de $NP)" bash -c "grep -q 'no es la que dice su sha256' '$d/rocola_os_bitacora.txt' && [ '$(est $o paquete_partes)' = $((NP - 1)) ]" ;;
  paginas)
    # LAS PAGINAS POR LETRA (27-11-2026): de fabrica 100 (sin pared_paginas=), y pared_paginas=1000 se respeta (tope 1000)
    apifalsa $e "$CAT"; d=$(datos $e encendido); printf 'spotify=1\n' > "$d/rocola_os.txt"
    abre $e "$d" "$BASE" 20 ROCOLA_OS=1; espera $e; o="$W/$e"
    si "paginas: de fabrica, 100 paginas en la A ($(est $o paginas))" test "$(est $o paginas)" = 100 ;;
  letra)
    # SIN LA LETRA ♫ (27-11-2026, el dueno: «Lo de la casilla final con icono de Spotify quitala, esa casilla no tiene utilidad realmente»; antes esta
    # escena comprobaba que la ♫ llevaba el icono de Spotify, 22-11-2026): RocolaOsSpotifyLetraEnLaPared = false, asi que al final del abecedario no hay
    # casilla de Spotify ni su verde, sin elegir y con REGRESAR+IZQUIERDA dos veces desde la A (la vuelta da a la ultima letra de verdad). Con la cuenta
    apifalsa $e "$CAT"; d=$(datos $e cuenta)
    abre $e "$d" "7:67,10:27+37,11.4:27+37" 17 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 9 pared-generos-letra-spotify),$(cap 14 pared-generos-letra-spotify-elegida)"
    espera $e; o="$W/$e"
    C1=$( [ -n "$CAPTURAS" ] && echo "$CAPTURAS" || echo "$W")
    si "letra: sale con 0" test "$(cat $o/salio)" = 0
    python3 "$G/icono.py" --ausente "$C1/pared-generos-letra-spotify.png" "$C1/pared-generos-letra-spotify-elegida.png" > "$o/icono.txt" 2>&1; r=$?
    sed 's/^/   /' "$o/icono.txt"
    si "letra: SIN la casilla ♫ ni el verde de Spotify al final del abecedario, sin elegir y con la vuelta elegida, en 1024x768" test $r = 0 ;;
  *) echo "(escena desconocida: $e)" ;;
  esac
done
echo "== $bien BIEN, $mal MAL$([ $falta = 0 ] || echo ", $falta FALTA")"
[ $mal = 0 ] || exit 1
[ $falta = 0 ] || exit 2
