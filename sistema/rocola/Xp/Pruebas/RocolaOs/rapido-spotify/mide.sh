#!/bin/bash
# LO QUE TARDA LO DE SPOTIFY (rapido-spotify, FASE11_rapido-spotify.md): la rocola entera, fuera de pantalla, con datos INVENTADOS, la API web
# de Spotify FALSA con latencia (Pruebas/RocolaOs/catalogo/ApiFalsa: /control/lento, imglento, llavelento, conexion, caalento), el Cover Art
# Archive falso (la misma API falsa) y el Soloist FALSO del nucleo (Pruebas/RocolaOs/spotify-xp/SoloistFalso, con SOLOIST_FALSO_LENTO: lo que
# tarda en empezar). NUNCA el Soloist de verdad. No comprueba nada: MIDE (las comprobaciones son las baterias de cada grupo) y escribe una
# linea por medida («MEDIDA escena nombre valor») en $W/medidas.txt, con mide.py.
#   bash mide.sh PROGRAMA [escenas...]     PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT; para medir, SIEMPRE esta)
#   escenas: busca disco artista pared suena (de fabrica, todas)
#   LAT: la latencia de cada peticion a la API y de cada portada de Spotify (ms; 120 de fabrica); CONEX: lo que cuesta abrir una conexion
#   (ms; 150 de fabrica: TCP y TLS); CAA: lo de cada portada del Cover Art Archive (ms; 400 de fabrica: sus dos redirecciones); LENTO: lo que
#   tarda el Soloist falso en empezar una (s; 0.6 de fabrica)
#   W: la carpeta de trabajo (se rehace); ETIQUETA: lo que va en cada medida (p. ej. base o nuevo)
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: mide.sh PROGRAMA [escenas]}"; shift
escenas="${*:-busca disco artista pared suena}"
T=${T:-/root/rocola-trabajo/fase11/rapido-spotify}
API=${API:-$T/api-falsa}
FALSO=${FALSO:-$T/falso}
W=${W:-$T/mide}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
LAT=${LAT:-120}; CONEX=${CONEX:-150}; CAA=${CAA:-400}; LENTO=${LENTO:-0.6}
ETIQUETA=${ETIQUETA:-?}
CA="$G/../catalogo"; SX="$G/../spotify-xp"; PG="$G/../pared-generos"
if [ ! -f "$API/api-falsa.dll" ] || [ "$CA/ApiFalsa/Program.cs" -nt "$API/api-falsa.dll" ]; then
  echo "(compilando la API falsa en $API)"; mkdir -p "$API"
  nice dotnet build "$CA/ApiFalsa/ApiFalsa.csproj" -c Release -o "$API" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$API/obj/" > "$API/build.txt" 2>&1 || { cat "$API/build.txt"; exit 1; }
  rm -rf "$API/obj"
fi
AL=$T/alsa; mkdir -p "$AL"
[ -f "$AL/libasound_module_pcm_reloj.so" ] || gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
if [ ! -f "$FALSO/soloist-falso.dll" ] || [ "$SX/SoloistFalso/Program.cs" -nt "$FALSO/soloist-falso.dll" ]; then
  echo "(compilando el Soloist falso en $FALSO)"; mkdir -p "$FALSO"
  nice dotnet build "$SX/SoloistFalso/SoloistFalso.csproj" -c Release -o "$FALSO" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$FALSO/obj/" > "$FALSO/build.txt" 2>&1 || { cat "$FALSO/build.txt"; exit 1; }
  rm -rf "$FALSO/obj"
fi
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
CAT=$T/catalogo-prueba
[ -f "$CAT/rocola_catalogo.tsv.gz" ] || python3 "$PG/catalogo.py" "$CAT" > /dev/null || exit 1

P=$((30000 + ($$ * 13) % 20000))
apifalsa() {   # NOMBRE: una API falsa nueva (con el catalogo de prueba) y su latencia
  local o="$W/$1/api"; mkdir -p "$o"; P=$((P + 1))
  API_CATALOGO="$CAT" dotnet "$API/api-falsa.dll" $P "$o" id-de-prueba secreto-de-prueba refresco-1 > "$o/salida.txt" 2>&1 &
  APIPID=$!
  for i in $(seq 1 100); do [ -f "$o/listo" ] && break; sleep 0.1; done
  for c in "lento?ms=$LAT" "imglento?ms=$LAT" "llavelento?ms=$LAT" "conexion?ms=$CONEX" "caalento?ms=$CAA"; do curl -s --noproxy '*' "http://127.0.0.1:$P/control/$c" > /dev/null; done
  : > "$o/peticiones.txt"; : > "$o/tiempos.txt"
}
abre() {   # NOMBRE DATOS "PULSA" SEGUNDOS [VAR=valor...]
  local n="$1" d="$2" pulsa="$3" seg="$4"; shift 4
  local o="$W/$n"; mkdir -p "$o/run" "$o/soloist" "$o/disco"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" > "$o/alsa.conf"
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_SPOTIFY_API="http://127.0.0.1:$P/v1" ROCOLA_SPOTIFY_CUENTAS="http://127.0.0.1:$P" \
      ROCOLA_CATALOGO_URL="http://127.0.0.1:$P/catalogo" ROCOLA_PORTADAS_CATALOGO="http://127.0.0.1:$P" ROCOLA_CATALOGO_ESPERA_S=0 \
      ROCOLA_IA_DATOS="$o/disco/ia-xp" \
      ROCOLA_SOLOIST_DATOS="$o/soloist" ROCOLA_SOLOIST_BIN="$SX/vigilante-falso.sh" SOLOIST_FALSO_BIN="$FALSO" SOLOIST_FALSO_LENTO="$LENTO" \
      SOLOIST_FALSO_LOG="$o/falso.txt" VIGILANTE_FALSO_LOG="$o/vigilante.txt" \
      "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  ROCOLA=$!
  date +%s%3N > "$o/inicio_ms"
  ( sleep "$seg"; kill -TERM $ROCOLA 2>/dev/null; sleep 30; kill -KILL $ROCOLA 2>/dev/null ) & VIG=$!
}
espera() { wait $ROCOLA; echo $? > "$W/$1/salio"; kill $VIG 2>/dev/null; wait $VIG 2>/dev/null; kill $APIPID 2>/dev/null; wait $APIPID 2>/dev/null; }
datos() {   # NOMBRE: los de partida, el modo encendido con la llave de la API y los discos de prueba de la pared (como pared-generos)
  local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/
  rm -f "$d/rocola_biblioteca.txt" "$d/rocola_fuentes.txt" "$d/rocola_estado.txt"
  local m="$d/musica" s="$DATOS0/musica/Los Ejemplos/Disco Uno"
  for x in "Angeles Ficticios - Cumbias del Recuerdo" "Alma Nortena - Corridos Bravos" "Amor Inventado - Boleros de Siempre" "Alfa Rockera - Rock de Prueba" \
           "Arena Tropical - Salsa Inventada" "Banda Ejemplar - Banda en Vivo"; do mkdir -p "$m/$x"; cp "$s"/*.mp3 "$m/$x/"; done
  printf 'spotify=1\n' > "$d/rocola_os.txt"; printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\n' > "$d/rocola_spotify_api.txt"; chmod 600 "$d/rocola_spotify_api.txt"
  echo "$d"
}
escribe() { local s="$1" pal="$2" r="" i c; for ((i = 0; i < ${#pal}; i++)); do c=$(printf '%d' "'${pal:$i:1}"); c=$((c - 32)); r="$r,$(echo "$s + $i * 0.25" | bc):$c"; done; echo "${r#,}"; }
veces() { local s="$1" n="$2" vk="$3" paso="${4:-0.15}" r="" i; for ((i = 0; i < n; i++)); do r="$r,$(echo "$s + $i * $paso" | bc):$vk"; done; echo "${r#,}"; }
BASE="7:67,8:27+38"   # moneda; el buscador (REGRESAR+ARRIBA)

for e in $escenas; do
  echo "== $e (LAT=$LAT CONEX=$CONEX CAA=$CAA LENTO=$LENTO; carga $(cut -d' ' -f1-3 /proc/loadavg))"
  echo "$e $(cut -d' ' -f1 /proc/loadavg)" >> "$W/carga.txt"
  case "$e" in
  busca)
    # dos busquedas: «inventad» (la primera: la llave y las conexiones aun no estan) y, borrada, «mentira» (ya con todo abierto)
    apifalsa $e; d=$(datos $e)
    abre $e "$d" "$BASE,$(escribe 9.5 inventad),$(veces 15 8 8 0.1),$(escribe 16 mentira)" 22 ROCOLA_OS=1
    espera $e ;;
  disco)
    # un disco de Spotify del buscador: «mentira», a su tarjeta (la 2a), PONER lo abre (1 peticion)
    apifalsa $e; d=$(datos $e)
    abre $e "$d" "$BASE,$(escribe 9.5 mentira),$(veces 13.5 14 39),16.5:13" 21 ROCOLA_OS=1
    espera $e ;;
  artista)
    # la pared de un artista que solo esta en Spotify (Banda Interminable, 40 discos): PONER la abre; ARRIBA, su 2a pagina
    apifalsa $e; d=$(datos $e)
    abre $e "$d" "$BASE,$(escribe 9.5 interminable),$(veces 14 14 39),17:13,22:38" 26 ROCOLA_OS=1
    espera $e ;;
  pared)
    # la letra A con el catalogo (9 000 discos): sus portadas del Cover Art Archive (CAA ms cada una); ARRIBA (la siguiente) cada 4 s, 5 paginas; luego
    # ABAJO 2 (las ya vistas)
    apifalsa $e; d=$(datos $e)
    abre $e "$d" "7:67,$(veces 14 5 38 4),34:40,37:40" 41 ROCOLA_OS=1
    espera $e ;;
  suena)
    # dos de Spotify seguidas con la rocola callada (el doble del catalogo las pide: «spotify-prueba»); el Soloist falso tarda LENTO s en
    # empezar cada una
    apifalsa $e; d=$(datos $e)
    mkdir -p "$W/$e/run/rocola-xp/spotify"
    abre $e "$d" "7:67" 34 ROCOLA_OS=spotify,spotify-prueba
    ( sleep 10; echo "pide spotify:track:f440d008aa|Prueba|Artista inventado|Disco inventado|8000" >> "$W/$e/run/rocola-xp/spotify/prueba-pide"
      sleep 0.3; echo "pide spotify:track:f550d006bb|Prueba|Artista inventado|Disco inventado|6000" >> "$W/$e/run/rocola-xp/spotify/prueba-pide" ) &
    espera $e
    [ -f "$W/$e/sonado.raw" ] && python3 "$SX/frecuencias.py" "$W/$e/sonado.raw" "$W/$e/registro.txt" > "$W/$e/frec.txt" ;;
  esac
  python3 "$G/mide.py" "$e" "$W/$e" "$ETIQUETA" | tee -a "$W/medidas.txt"
  rm -f "$W/$e/sonado.raw"
done
