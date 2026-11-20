#!/bin/bash
# LA PRUEBA DE LA SUSTITUCION INVISIBLE (fase 11, funcion «calidad_spotify»: RocolaOs/RocolaOs.Calidad*.cs; FASE11_sustitucion.md). rocola-xp de
# verdad, fuera de pantalla, con la tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c: lo que DE VERDAD sono), el Soloist FALSO del
# nucleo (Pruebas/RocolaOs/spotify-xp/SoloistFalso, abierto por su vigilante falso; NUNCA el de verdad) y la API web FALSA del catalogo
# (Pruebas/RocolaOs/catalogo/ApiFalsa: cuenta cada peticion). Datos INVENTADOS: una biblioteca de MP3 hechos aqui con ffmpeg, cada uno con
# su tono (el MP3 suena a 180-390 Hz; su version de Spotify, a 440-770 Hz: asi se sabe de donde salio lo que se oyo) y su envolvente de
# volumen (la 1 o la 2 de RocolaOsCalidad.Envolvente, para la forma). Las canciones se piden con el doble «calidad-prueba» (AddSong de XP).
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   escenas (de fabrica todas): reglas isrc titulo sinpareja nx corte forma frena apagado sinllave sinsesion caida atasca seguida apagon tarde
#   W (/root/rocola-trabajo/fase11/sustitucion/prueba; se rehace), API y FALSO (la API falsa y el Soloist falso compilados; se compilan si
#   faltan o cambiaron, con el candado de lo pesado)
# Cada comprobacion: BIEN o MAL en una linea; al final «TODO BIEN (N comprobaciones)» o cuantas MAL (y sale con 1).
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd); SX="$G/../spotify-xp"; CA="$G/../catalogo"
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
partes="${*:-reglas isrc titulo sinpareja nx corte forma frena apagado sinllave sinsesion caida atasca seguida apagon tarde}"
W=${W:-/root/rocola-trabajo/fase11/sustitucion/prueba}
API=${API:-/root/rocola-trabajo/fase11/sustitucion/api-falsa}
FALSO=${FALSO:-/root/rocola-trabajo/fase11/sustitucion/falso}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
entre() { python3 -c "import sys; v=float(sys.argv[1]); sys.exit(0 if float(sys.argv[2]) <= v <= float(sys.argv[3]) else 1)" "$1" "$2" "$3"; }

# ---------- lo que hace falta: la API falsa, el Soloist falso y la tarjeta con reloj ----------
compila() {   # PROYECTO FUENTE SALIDA DLL
  if [ ! -f "$3/$4" ] || [ "$2" -nt "$3/$4" ]; then
    echo "(compilando $(basename "$1") en $3)"; mkdir -p "$3"
    flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$1" -c Release -o "$3" --disable-build-servers -p:UseSharedCompilation=false \
      -p:BaseIntermediateOutputPath="$3/obj/" > "$3/build.txt" 2>&1 || { cat "$3/build.txt"; exit 1; }
  fi
}
compila "$CA/ApiFalsa/ApiFalsa.csproj" "$CA/ApiFalsa/Program.cs" "$API" api-falsa.dll
compila "$SX/SoloistFalso/SoloistFalso.csproj" "$SX/SoloistFalso/Program.cs" "$FALSO" soloist-falso.dll
AL=$(dirname "$FALSO")/alsa; mkdir -p "$AL"
[ -f "$AL/libasound_module_pcm_reloj.so" ] || gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1

# ---------- la biblioteca inventada (una vez) ----------
# mp3 NOMBRE HZ SEG KBPS ENVOLVENTE(0,1) TITULO ARTISTA DISCO [ISRC]
M=$W/mp3; mkdir -p "$M"
mp3() {
  local f="$M/$1.mp3" vol="volume=1"
  [ "$5" = 1 ] && vol="volume='pow(10,(9*sin(2*PI*t/14)+4*sin(2*PI*t/4.6))/20)':eval=frame"
  ffmpeg -hide_banner -loglevel error -y -f lavfi -i "sine=frequency=$2:sample_rate=44100:duration=$3" -af "$vol" -ac 2 -c:a libmp3lame -b:a "$4k" \
    -id3v2_version 3 -metadata title="$6" -metadata artist="$7" -metadata album="$8" ${9:+-metadata TSRC="$9"} "$f" || exit 1
}
mp3 bolero 330 32 96 1 "Bolero de Prueba" "Trio Calidad" "Joyas de Prueba" MXA112600001
mp3 vals 360 32 96 1 "Vals Sin Codigo" "Trio Calidad" "Joyas de Prueba"
mp3 cumbia 390 32 96 1 "Cumbia Otra Forma" "Trio Calidad" "Joyas de Prueba" MXA112600003
mp3 son 300 32 96 1 "Son Que No Llega" "Trio Calidad" "Joyas de Prueba" MXA112600005
mp3 danzon 270 12 96 1 "Danzon Huerfano" "Trio Calidad" "Joyas de Prueba" MXA112600009
mp3 cortada 240 32 96 1 "Cumbia Cortada" "Trio Calidad" "Joyas de Prueba" MXA112600006
mp3 buena 210 10 320 0 "Buena Calidad" "Grupo Bueno" "Disco Bueno"
mp3 relleno 180 20 320 0 "Relleno" "Grupo Bueno" "Disco Bueno"
# datos NOMBRE: la carpeta de datos de la escena (sin nada aprendido) con su musica (la de la rocola esta junto a los datos: «musica»)
datos() {
  local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d/musica/Trio Calidad/Joyas de Prueba" "$d/musica/Grupo Bueno/Disco Bueno"
  cp "$DATOS0/rocola_teclas.txt" "$d/"   # (las teclas de las escenas de ventanas: moneda 67, poner 13...)
  local t=1; for x in bolero vals cumbia son danzon cortada; do cp "$M/$x.mp3" "$d/musica/Trio Calidad/Joyas de Prueba/0$t $x.mp3"; t=$((t + 1)); done
  cp "$M/buena.mp3" "$d/musica/Grupo Bueno/Disco Bueno/01 buena.mp3"; cp "$M/relleno.mp3" "$d/musica/Grupo Bueno/Disco Bueno/02 relleno.mp3"
  printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\n' > "$d/rocola_spotify_api.txt"; chmod 600 "$d/rocola_spotify_api.txt"
  echo "$d"
}
# siembra DATOS LINEA...: rocola_spotify_pares.txt con esas lineas (RUTA se cambia por la de su MP3; BYTES y FECHA, las del archivo)
siembra() {
  local d="$1"; shift
  python3 - "$d" "$@" <<'EOP'
import os, sys
d = sys.argv[1]; out = ["ROCOLA-SPOTIFY-PARES v1"]
for l in sys.argv[2:]:
    c = l.split('\t')
    if c[0].startswith('MP3:'):
        r = os.path.join(d, 'musica/Trio Calidad/Joyas de Prueba', c[0][4:])
        st = os.stat(r); c[0] = r; c[1] = str(st.st_size); c[2] = str(st.st_mtime_ns // 100 + 621355968000000000)
    out.append('\t'.join(c))
open(os.path.join(d, 'rocola_spotify_pares.txt'), 'w').write('\n'.join(out) + '\n')
EOP
}

# LA API FALSA de la escena (un puerto nuevo) y sus peticiones
P=$((24000 + $$ % 10000))
apifalsa() { local o="$W/$1/api"; mkdir -p "$o"; P=$((P + 1)); dotnet "$API/api-falsa.dll" $P "$o" id-de-prueba secreto-de-prueba refresco-1 > "$o/salida.txt" 2>&1 & APIPID=$!
  for i in $(seq 1 100); do [ -f "$o/listo" ] && break; sleep 0.1; done; : > "$o/peticiones.txt"; }
control() { curl -s --noproxy '*' "http://127.0.0.1:$P/control/$1" > /dev/null; }
v1() { local n; n=$(grep -c ' /v1/' "$W/$1/api/peticiones.txt" 2>/dev/null); echo "${n:-0}"; }

# abre NOMBRE DATOS "PULSA" "ORDENES" SEGUNDOS SENAL [VAR=valor...]: la rocola con esos datos y las teclas PULSA (ROCOLA_PULSA); las ORDENES
# («SEG:pon FINAL;SEG:mira;SEG:copia;SEG:429») a su hora: «pon» y «mira» al doble (prueba-xp), «copia» copia los estados de /run y «429» le dice
# a la API falsa que la siguiente conteste 429 (120 s). A los SEGUNDOS, la SENAL. Deja en $W/NOMBRE: salida.txt, traza.txt, run/, falso.txt,
# sonado.raw, registro.txt, frec.txt, salio.
abre() {
  local n="$1" d="$2" pulsa="$3" ord="$4" seg="$5" sen="$6"; shift 6
  local o="$W/$n"; mkdir -p "$o/run" "$o/soloist"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" > "$o/alsa.conf"
  local T0; T0=$(date +%s.%N)
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf \
      ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" \
      ROCOLA_SPOTIFY_API="http://127.0.0.1:$P/v1" ROCOLA_SPOTIFY_CUENTAS="http://127.0.0.1:$P" \
      ROCOLA_SOLOIST_DATOS="$o/soloist" ROCOLA_SOLOIST_BIN="$SX/vigilante-falso.sh" SOLOIST_FALSO_BIN="$FALSO" \
      SOLOIST_FALSO_LOG="$o/falso.txt" VIGILANTE_FALSO_LOG="$o/vigilante.txt" "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$!
  ( IFS=';'; for x in $ord; do [ -z "$x" ] && continue; s="${x%%:*}"; l="${x#*:}"
      while [ "$(echo "$(date +%s.%N) - $T0 < $s" | bc)" = 1 ]; do sleep 0.1; done
      mkdir -p "$o/run/rocola-xp/spotify"
      case "$l" in
        copia) cp "$o/run/rocola-xp/spotify/estado" "$o/estado.txt" 2>/dev/null; cp "$o/run/rocola-xp/spotify/calidad" "$o/calidad.txt" 2>/dev/null ;;
        429) control "429?seg=120" ;;
        5xx) control "5xx?n=1000" ;;
        *) echo "$l" >> "$o/run/rocola-xp/spotify/prueba-xp" ;;
      esac; done ) &
  local q=$!
  sleep "$seg"; kill $q 2>/dev/null
  [ "$sen" != NADA ] && kill -"$sen" $p 2>/dev/null
  ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
  [ -f "$o/sonado.raw" ] && python3 "$SX/frecuencias.py" "$o/sonado.raw" "$([ -f "$o/registro.txt" ] && echo "$o/registro.txt" || echo 44100)" > "$o/frec.txt"
  rm -f "$o/sonado.raw"
}
fin() { kill $APIPID 2>/dev/null; wait $APIPID 2>/dev/null; }
oye() { [ -f "$1" ] || { echo 0.00; return; }; awk -v f="$2" '$3>=f-15 && $3<=f+15 {s+=$2-$1} END {printf "%.2f\n", s+0}' "$1"; }
primero() { awk -v f="$2" '$3>=f-15 && $3<=f+15 && $2-$1>=0.3 {print $1; exit}' "$1"; }
ultimo() { awk -v f="$2" '$3>=f-15 && $3<=f+15 && $2-$1>=0.3 {u=$2} END {print u+0}' "$1"; }
log() { echo "$W/$1/run/rocola-xp/spotify/prueba-xp.log"; }
cal() { sed -n "s/^$2=//p" "$W/$1/calidad.txt" 2>/dev/null; }
bit() { echo "$1/rocola_os_bitacora.txt"; }
par() { awk -F'\t' -v r="$2" 'index($1, r) > 0 {print}' "$1/rocola_spotify_pares.txt" 2>/dev/null; }   # el renglon de la tabla de esa cancion
# los renglones de la tabla, sin la ruta: solo ids de Spotify, ISRC, numeros y «isrc»/«titulo»
solo_numeros() { awk -F'\t' 'NR > 1 && $0 !~ /^#/ { for (i = 2; i <= NF; i++) if ($i !~ /^(-|-?[0-9.]+|[0-9A-Za-z]+|isrc|titulo)$/) { print "raro: " $i; bad=1 } } END { exit bad }' "$1/rocola_spotify_pares.txt"; }
ROS="ROCOLA_OS=spotify,calidad_spotify,calidad-prueba"
sec() { echo "== $1"; }
cuota() { local b k; b=$(grep -c ' /v1/search ' "$W/$1/api/peticiones.txt" 2>/dev/null); k=$(grep -c '/api/token' "$W/$1/api/peticiones.txt" 2>/dev/null)
  echo "   cuota de la escena $1: $(v1 $1) peticiones a /v1 (${b:-0} busquedas) y ${k:-0} de la llave"; }
export -f par

# ---------------------------------------------------------------- reglas: las de A, B y C con casos inventados, y lo que se lee de los MP3
if [[ " $partes " == *" reglas "* ]]; then
  sec "reglas (rocola-xp --prueba-calidad)"
  ROCOLA_PANTALLA=fuera:64x64 "${RX[@]}" --prueba-calidad "$M/bolero.mp3" "$M/vals.mp3" "$M/buena.mp3" > "$W/reglas.txt" 2>&1; r=$?
  grep -v '^Pantalla' "$W/reglas.txt" | sed 's/^/   /'
  si "reglas: la orden sale con 0 y TODO BIEN ($(grep -c '^BIEN' "$W/reglas.txt") reglas)" bash -c "[ $r = 0 ] && grep -q '^TODO BIEN' '$W/reglas.txt' && ! grep -q '^MAL' '$W/reglas.txt'"
  si "reglas: el MP3 de 96 kbps CBR suena peor y trae su ISRC; el de 320 kbps no" bash -c "grep -q 'bolero.mp3: 96 kbps, 44100 Hz, 32... ms, ISRC MXA112600001, «Bolero de Prueba» de «Trio Calidad» en «Joyas de Prueba»; suena peor (sin el corte): si, 96 kbps' '$W/reglas.txt' && grep -q 'buena.mp3: 320 kbps.*suena peor (sin el corte): no' '$W/reglas.txt'"
fi

# ---------------------------------------------------------------- isrc: una mala con pareja por ISRC suena desde Spotify, invisible
if [[ " $partes " == *" isrc "* ]]; then
  sec "isrc (la pareja por su ISRC; suena desde Spotify sin que nada cambie; V2 con la mediana de 5; V1 y C; la segunda vez, con su medida)"
  d=$(datos isrc); apifalsa isrc
  # (V2: cinco canciones de otras rutas ya medidas desde Spotify: la primera vez se nivela con su mediana)
  L5=(); for k in 1 2 3 4 5; do L5+=("/no/existe/otra$k.mp3	1	1	f300d030m1z$k	-	30000	30000	1	1	-$((k + 2)).0	-$((k + 4)).0	-$((k + 7)).0	0.95	isrc"); done
  siembra "$d" "${L5[@]}"
  abre isrc "$d" "7:67" "9:pon relleno.mp3;9.5:pon 01 bolero.mp3;45:mira;64:pon relleno.mp3;64.6:pon 01 bolero.mp3;90:mira;90.5:copia" 91 USR1 $ROS
  fin; o="$W/isrc"; f="$o/frec.txt"; b=$(bit "$d")
  sed 's/^/   /' "$(log isrc)"; grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; cuota isrc
  si "isrc: sale con 0" test "$(cat $o/salio)" = 0
  si "isrc: las dos pedidas cobran como XP: relleno 4->3 y la mala 3->2 (y la segunda vez 2->1, 1->0)" bash -c "grep -q 'pon relleno.mp3 creditos 4->3' '$(log isrc)' && grep -q 'pon 01 bolero.mp3 creditos 3->2' '$(log isrc)' && grep -q 'pon 01 bolero.mp3 creditos 1->0' '$(log isrc)'"
  si "isrc: UNA peticion a la API en toda la escena, la del ISRC (y la llave)" bash -c "[ $(v1 isrc) = 1 ] && grep -q 'GET /v1/search app 200 q=isrc:MXA112600001' '$o/api/peticiones.txt'"
  si "isrc: la pareja es la de su ISRC" contiene "$b" "01 bolero.mp3: pareja por ISRC -> spotify:track:f440d032m1bo (1 peticiones)"
  si "isrc: el Soloist falso toco esa (play spotify:track:f440d032m1bo), dos veces" bash -c "[ \$(grep -c '\"play\".*spotify:track:f440d032m1bo' '$o/falso.txt') -ge 2 ]"
  t440=$(oye "$f" 440); t330=$(oye "$f" 330)
  si "isrc: se oyo la de Spotify (440 Hz: $t440 s = 32 s la primera + la segunda hasta el cierre) y nada del MP3 (330 Hz: $t330 s)" bash -c "python3 -c 'import sys; sys.exit(0 if 34 <= $t440 <= 40 and $t330 <= 0.1 else 1)'"
  si "isrc: la pantalla es la de tu cancion: lo que suena es el MP3 con su titulo, su artista y su disco (y el lector, el de Spotify)" bash -c "grep -q '^mira .*01 bolero.mp3 | Bolero de Prueba | Trio Calidad | Joyas de Prueba | .* | lector=spotify creditos=2 cola=0' '$(log isrc)'"
  si "isrc: la primera vez, con la mediana de las 5 medidas (V2)" contiene "$b" "(la mediana de 5 de Spotify medidas)"
  si "isrc: se midio al limite (V1) y la forma salio la misma (C)" bash -c "grep -q '01 bolero.mp3: la de Spotify medida al limite' '$b' && grep -q '01 bolero.mp3: la forma -> misma' '$b'"
  si "isrc: la segunda vez, con su medida y sin preguntar" bash -c "[ \$(grep -c 'la cadena de 01 bolero.mp3 con spotify:track:f440d032m1bo: nivelado .* (su medida)' '$b') = 1 ]"
  si "isrc: la tabla: su renglon con el id, el ISRC, confirmada (1) y su medida" bash -c "par '$d' '01 bolero.mp3' | awk -F'\t' '\$4==\"f440d032m1bo\" && \$5==\"MXA112600001\" && \$8==1 && \$10!=\"-\" && \$12!=\"-\" {ok=1} END {exit !ok}'"
  si "isrc: la tabla solo lleva rutas, ids y numeros" solo_numeros "$d"
  si "isrc: lo mas sonado y las estadisticas son las de tu cancion (rocola_stats.txt la cuenta; nada de spotify:)" bash -c "grep -q '01 bolero.mp3' '$d/rocola_stats.txt' && ! grep -q 'spotify:' '$d/rocola_stats.txt'"
  si "isrc: ni cobrada ni devuelta como de Spotify (rocola_spotify.txt sin COBRADA ni DEVUELTA)" bash -c "! grep -qE 'COBRADA|DEVUELTA' '$d/rocola_spotify.txt'"
  si "isrc: ninguna tabla de lo aprendido de XP con «spotify:» (R7)" bash -c "! grep -rlsF 'spotify:' '$d' --include='rocola_*.txt' | grep -vE 'rocola_spotify.txt|rocola_os_bitacora.txt|rocola_error.txt|rocola_estado.txt' | grep -q ."
  si "isrc: el estado: 2 desde Spotify, 1 confirmada, 1 medida, 0 al MP3" bash -c "[ '$(cal isrc desde_spotify)/$(cal isrc confirmadas)/$(cal isrc medidas)/$(cal isrc al_mp3)' = '2/1/1/0' ]"
fi

# ---------------------------------------------------------------- titulo: sin ISRC, la regla 2 (y una buena no se toca)
if [[ " $partes " == *" titulo "* ]]; then
  sec "titulo (sin ISRC: regla 2 entre el recopilatorio, la en vivo y la de otro artista; la buena, ni una peticion)"
  d=$(datos titulo); apifalsa titulo
  abre titulo "$d" "7:67" "9:pon relleno.mp3;9.5:pon 02 vals.mp3;10:pon 01 buena.mp3;45:mira;73:copia" 74 TERM $ROS
  fin; o="$W/titulo"; f="$o/frec.txt"; b=$(bit "$d")
  grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; cuota titulo
  si "titulo: sale con 0" test "$(cat $o/salio)" = 0
  si "titulo: UNA peticion, la de titulo y artista" bash -c "[ $(v1 titulo) = 1 ] && grep -q 'q=track:\"Vals Sin Codigo\" artist:\"Trio Calidad\"' '$o/api/peticiones.txt'"
  si "titulo: la de su disco (con acento en Spotify), ni la del recopilatorio, ni la en vivo, ni la de otro artista" contiene "$b" "02 vals.mp3: pareja por titulo y artista -> spotify:track:f550d032m1va"
  si "titulo: se oyo la de su disco (550 Hz: $(oye "$f" 550) s) y nada de las otras (560/570/580: $(oye "$f" 560) $(oye "$f" 570) $(oye "$f" 580)) ni del MP3 (360: $(oye "$f" 360))" bash -c "python3 -c 'import sys; sys.exit(0 if 31.5 <= $(oye "$f" 550) <= 32.5 and $(oye "$f" 360) + $(oye "$f" 570) + $(oye "$f" 580) <= 0.1 else 1)'"
  si "titulo: la buena (320 kbps) suena como siempre (210 Hz: $(oye "$f" 210) s) y ni se busca (ni la de relleno: buenas=2)" bash -c "python3 -c 'import sys; sys.exit(0 if 9.5 <= $(oye "$f" 210) <= 10.5 else 1)' && ! grep -q '01 buena.mp3' '$b' && [ '$(cal titulo buenas)' = 2 ]"
  si "titulo: la primera vez sin medidas de Spotify: como una de XP sin medir" contiene "$b" "(sin medir: como una de XP sin medir)"
fi

# ---------------------------------------------------------------- sinpareja: suena el MP3 y no se vuelve a preguntar (en la sesion ni en la siguiente)
if [[ " $partes " == *" sinpareja "* ]]; then
  sec "sinpareja (dos peticiones -ISRC y titulo- y nada; ni otra vez en la sesion, ni en la siguiente)"
  d=$(datos sinpareja); apifalsa sinpareja
  abre sinpareja "$d" "7:67" "9:pon relleno.mp3;9.5:pon 05 danzon.mp3;42.5:pon 05 danzon.mp3;55:copia" 56 TERM $ROS
  fin; o="$W/sinpareja"; f="$o/frec.txt"; b=$(bit "$d")
  apifalsa sinpareja2; abre sinpareja2 "$d" "7:67" "9:pon 05 danzon.mp3;20:copia" 21 TERM $ROS; fin
  grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; cuota sinpareja; cuota sinpareja2
  si "sinpareja: salen con 0" test "$(cat $o/salio)$(cat $W/sinpareja2/salio)" = 00
  si "sinpareja: DOS peticiones (la del ISRC no dio nada: la de titulo y artista)" bash -c "[ $(v1 sinpareja) = 2 ] && grep -q 'q=isrc:MXA112600009' '$o/api/peticiones.txt' && grep -q 'q=track:\"Danzon Huerfano\"' '$o/api/peticiones.txt'"
  si "sinpareja: se apunta sin pareja" contiene "$b" "05 danzon.mp3: sin pareja (sin resultados; 2 peticiones)"
  si "sinpareja: suena su MP3 las dos veces (270 Hz: $(oye "$f" 270) s de 24)" entre "$(oye "$f" 270)" 23 24.6
  si "sinpareja: la sesion siguiente no pregunta (0 peticiones) y lo dice" bash -c "[ $(v1 sinpareja2) = 0 ] && grep -q 'sin pareja (buscada hace 0 dias): no se vuelve a preguntar' '$b'"
  si "sinpareja: y suena su MP3 (270 Hz: $(oye "$W/sinpareja2/frec.txt" 270) s)" entre "$(oye "$W/sinpareja2/frec.txt" 270)" 10 12.5
  si "sinpareja: la tabla lo guarda sin id (con su fecha)" bash -c "par '$d' '05 danzon.mp3' | awk -F'\t' '\$4==\"-\" && \$9>1700000000 {ok=1} END {exit !ok}'"
fi

# ---------------------------------------------------------------- nx: Spotify no empieza: a los 6 s, el MP3 entero, un solo credito
if [[ " $partes " == *" nx "* ]]; then
  sec "nx (Spotify no empieza a sonar: a los 6 s tu MP3 entero, desde el principio, sin aviso ni cobro de mas)"
  d=$(datos nx); apifalsa nx
  abre nx "$d" "7:67" "9:pon relleno.mp3;9.5:pon 04 son.mp3;50:mira;71:copia" 72 TERM $ROS
  fin; o="$W/nx"; f="$o/frec.txt"; b=$(bit "$d")
  sed 's/^/   /' "$(log nx)"; grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; cuota nx
  r=$(ultimo "$f" 180); s=$(primero "$f" 300)
  si "nx: sale con 0" test "$(cat $o/salio)" = 0
  si "nx: suena desde Spotify, que no empieza (play spotify:track:nxsonnollega)" bash -c "grep -q 'suena desde Spotify: 04 son.mp3 -> spotify:track:nxsonnollega' '$b' && grep -q '\"play\".*nxsonnollega' '$o/falso.txt'"
  si "nx: vuelve a tu MP3 a los 6 s, desde el principio" contiene "$b" "vuelve a tu MP3: 04 son.mp3 (Spotify no empezo a sonar en 6 s), desde el principio"
  si "nx: el MP3 empezo unos 6 s despues de acabar el relleno (de 5,5 a 9 s: ${r:-?} -> ${s:-?})" entre "$(echo "${s:-0} - ${r:-0}" | bc)" 5.5 9
  si "nx: y sono entero (300 Hz: $(oye "$f" 300) s de 32)" entre "$(oye "$f" 300)" 31.5 32.5
  si "nx: un solo credito por cancion (4 -> 3 -> 2), nada devuelto ni cobrado de mas" bash -c "grep -q 'pon 04 son.mp3 creditos 3->2' '$(log nx)' && grep -q 'creditos=2' '$(log nx)' && ! grep -qE 'DEVUELTA|COBRADA' '$d/rocola_spotify.txt'"
  si "nx: y a media cancion la rocola sigue con su MP3 (lector=xp)" bash -c "grep -q '^mira .*04 son.mp3 | Son Que No Llega .* | lector=xp' '$(log nx)'"
  si "nx: ningun aviso de XP de mas (ni «No se pudo reproducir»)" nocontiene "$o/traza.txt" "No se pudo reproducir"
  si "nx: el estado: al_mp3=1" test "$(cal nx al_mp3)" = 1
fi

# ---------------------------------------------------------------- corte: se corta a media cancion: tu MP3 desde donde iba
if [[ " $partes " == *" corte "* ]]; then
  sec "corte (Spotify se corta a media cancion, con mas de 10 s sonados: tu MP3 sigue desde donde iba)"
  d=$(datos corte); apifalsa corte
  abre corte "$d" "7:67" "9:pon relleno.mp3;9.5:pon 06 cortada.mp3;74:copia" 75 TERM $ROS SOLOIST_FALSO_GUION="42:corta"
  fin; o="$W/corte"; f="$o/frec.txt"; b=$(bit "$d")
  grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; cuota corte
  t770=$(oye "$f" 770); t240=$(oye "$f" 240); desde=$(sed -n 's/.*vuelve a tu MP3: 06 cortada.mp3 .*desde \([0-9.]*\) s.*/\1/p' "$b" | head -1)
  si "corte: sale con 0" test "$(cat $o/salio)" = 0
  si "corte: sono desde Spotify mas de 10 s y se corto (770 Hz: $t770 s)" entre "$t770" 10 25
  si "corte: vuelve a tu MP3 desde donde iba (${desde:-?} s)" bash -c "[ -n '$desde' ] && python3 -c 'import sys; sys.exit(0 if abs($desde - $t770) <= 1.5 else 1)'"
  si "corte: y suena el resto del MP3 (240 Hz: $t240 s; 32 - ${desde:-?})" bash -c "python3 -c 'import sys; sys.exit(0 if abs($t240 - (32 - ${desde:-0})) <= 1.2 else 1)'"
  si "corte: sin devolver ni cobrar como de Spotify" bash -c "! grep -qE 'DEVUELTA|COBRADA' '$d/rocola_spotify.txt'"
fi

# ---------------------------------------------------------------- forma: la de Spotify es otra grabacion (r < 0,55): se descarta
if [[ " $partes " == *" forma "* ]]; then
  sec "forma (la de Spotify con otra forma: se apunta como otra y ya no se sustituye)"
  d=$(datos forma); apifalsa forma
  abre forma "$d" "7:67" "9:pon relleno.mp3;9.5:pon 03 cumbia.mp3;67:pon 03 cumbia.mp3;77:mira;78:copia" 79 TERM $ROS
  fin; o="$W/forma"; f="$o/frec.txt"; b=$(bit "$d")
  grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; cuota forma
  rr=$(sed -n 's/.*03 cumbia.mp3: la forma -> otra (r \([-0-9.]*\),.*/\1/p' "$b")
  si "forma: sale con 0" test "$(cat $o/salio)" = 0
  si "forma: la primera vez sono desde Spotify (660 Hz: $(oye "$f" 660) s)" entre "$(oye "$f" 660)" 31.5 32.5
  si "forma: se comparo y es otra (r ${rr:-?} < 0,55)" bash -c "[ -n '$rr' ] && python3 -c 'import sys; sys.exit(0 if $rr < 0.55 else 1)'"
  si "forma: la tabla la marca como otra (-1)" bash -c "par '$d' '03 cumbia.mp3' | awk -F'\t' '\$8==-1 {ok=1} END {exit !ok}'"
  si "forma: la segunda vez, tu MP3 (390 Hz: $(oye "$f" 390) s; lector=xp) y ni una peticion mas" bash -c "grep -q '^mira .*03 cumbia.mp3 .* lector=xp' '$(log forma)' && [ $(v1 forma) = 1 ] && python3 -c 'import sys; sys.exit(0 if $(oye "$f" 390) >= 8 else 1)'"
fi

# ---------------------------------------------------------------- frena: 429 en la busqueda
if [[ " $partes " == *" frena "* ]]; then
  sec "frena (429 en la busqueda: nada se apunta, suena tu MP3 y no se pregunta mas)"
  d=$(datos frena); apifalsa frena
  abre frena "$d" "7:67" "8:429;9:pon relleno.mp3;9.5:pon 01 bolero.mp3;12:pon 02 vals.mp3;39:mira;40:copia" 41 TERM $ROS
  fin; o="$W/frena"; f="$o/frec.txt"; b=$(bit "$d")
  grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; cuota frena
  si "frena: sale con 0" test "$(cat $o/salio)" = 0
  si "frena: una sola peticion a /v1, la del 429" bash -c "[ $(v1 frena) = 1 ] && grep -q ' 429 q=isrc:MXA112600001' '$o/api/peticiones.txt'"
  si "frena: no se apunta nada (la busqueda no llego: Spotify frena)" bash -c "grep -q '01 bolero.mp3: la busqueda de su pareja no llego (Spotify frena)' '$b' && [ -z \"\$(par '$d' '01 bolero.mp3')\" ]"
  si "frena: la siguiente mala ni se pregunta" contiene "$b" "02 vals.mp3 suena peor (96 kbps): no se busca su pareja (Spotify frena (429); si sigue en la cola, se vuelve a mirar en 5 min)"
  # (revision «sonido-cuota-y-fallos»: antes, frenando, cada segundo un renglon y la cabecera leida otra vez por cada mala de la cola)
  si "frena: un solo renglon por cancion en toda la escena (vals $(grep -c '02 vals.mp3 suena peor' "$b"), bolero $(grep -c '01 bolero.mp3: la busqueda de su pareja no llego' "$b"))" bash -c "[ \$(grep -c '02 vals.mp3 suena peor' '$b') = 1 ] && [ \$(grep -c '01 bolero.mp3: la busqueda de su pareja no llego' '$b') = 1 ]"
  # (lo que la rocola da por sonando con tu MP3 aqui y con la version de Spotify en «isrc»: lo mismo campo por campo, sin el lector)
  if [ -f "$(log isrc)" ]; then
    a=$(grep '^mira ' "$(log isrc)" | head -1 | sed 's/.*musica\///; s/ | lector=.*//'); z=$(grep '^mira ' "$(log frena)" | head -1 | sed 's/.*musica\///; s/ | lector=.*//')
    echo "   lo que suena con tu MP3: «$z»; con la de Spotify (isrc): «$a»"
    si "frena: lo que suena con tu MP3 es lo mismo, campo por campo, que con su version de Spotify (escena isrc)" test -n "$a" -a "$a" = "$z"
  fi
  si "frena: suena tu MP3 (330 Hz: $(oye "$f" 330) s) y nada de Spotify (440: $(oye "$f" 440))" bash -c "grep -q '^mira .*01 bolero.mp3 .* lector=xp' '$(log frena)' && python3 -c 'import sys; sys.exit(0 if $(oye "$f" 330) >= 7 and $(oye "$f" 440) <= 0.1 else 1)'"
fi

# ---------------------------------------------------------------- apagado: sin ROCOLA_OS, o con calidad_spotify=0: XP tal cual
if [[ " $partes " == *" apagado "* ]]; then
  sec "apagado (sin ROCOLA_OS; con ROCOLA_OS=1 y calidad_spotify=0; aun con la pareja en la tabla)"
  for v in sin cero; do
    d=$(datos apagado-$v); apifalsa apagado-$v
    # (solo el disco de la mala, para pedirla con las teclas de XP: moneda, entrar al disco, PONER la primera)
    rm -rf "$d/musica/Grupo Bueno"; siembra "$d" "MP3:01 bolero.mp3	0	0	f440d032m1bo	MXA112600001	32000	32000	1	1	-3	-5	-8	0.95	isrc"
    if [ $v = sin ]; then abre apagado-$v "$d" "7:67,9:13,10.5:13" "" 24 TERM env -u ROCOLA_OS
    else printf 'calidad_spotify=0\n' > "$d/rocola_os.txt"; abre apagado-$v "$d" "7:67,9:13,10.5:13" "" 24 TERM ROCOLA_OS=1; fi
    fin; o="$W/apagado-$v"; f="$o/frec.txt"; b=$(bit "$d")
    si "apagado-$v: sale con 0" test "$(cat $o/salio)" = 0
    si "apagado-$v: suena tu MP3 (330 Hz: $(oye "$f" 330) s) y nada de Spotify (440: $(oye "$f" 440))" bash -c "python3 -c 'import sys; sys.exit(0 if $(oye "$f" 330) >= 9 and $(oye "$f" 440) <= 0.1 else 1)'"
    si "apagado-$v: ni una peticion a la API" test "$(v1 apagado-$v)" = 0
    si "apagado-$v: nada de calidad_spotify (ni en la bitacora ni en /run) y la tabla sin tocar" bash -c "! grep -qs 'calidad_spotify\]' '$b' && [ ! -e '$o/run/rocola-xp/spotify/calidad' ] && [ \$(grep -c . '$d/rocola_spotify_pares.txt') = 2 ]"
  done
  si "apagado-cero: con ROCOLA_OS=1 la funcion no va (encendidas: sin calidad_spotify)" bash -c "grep -F 'encendidas:' '$W/datos-apagado-cero/rocola_os_bitacora.txt' | grep -v -q calidad_spotify"
fi

# ---------------------------------------------------------------- sinllave: sin la llave de la API: ni una peticion, tu MP3
if [[ " $partes " == *" sinllave "* ]]; then
  sec "sinllave (sin rocola_spotify_api.txt: ni una peticion, suena tu MP3)"
  d=$(datos sinllave); rm -f "$d/rocola_spotify_api.txt"; apifalsa sinllave
  abre sinllave "$d" "7:67" "9:pon 01 bolero.mp3;20:mira;21:copia" 22 TERM $ROS
  fin; o="$W/sinllave"; f="$o/frec.txt"; b=$(bit "$d")
  si "sinllave: sale con 0, ni una peticion (ni la llave) y lo dice" bash -c "[ '$(cat $o/salio)' = 0 ] && [ ! -s '$o/api/peticiones.txt' ] && grep -q '01 bolero.mp3 suena peor (96 kbps): no se busca su pareja (sin la llave de la API; ' '$b' && [ \$(grep -c '01 bolero.mp3 suena peor' '$b') = 1 ]"
  si "sinllave: suena tu MP3 (330 Hz: $(oye "$f" 330) s)" bash -c "grep -q '^mira .*01 bolero.mp3 .* lector=xp' '$(log sinllave)' && python3 -c 'import sys; sys.exit(0 if $(oye "$f" 330) >= 8 else 1)'"
fi

# ---------------------------------------------------------------- sinsesion: Soloist no arranca (sin sesion): tu MP3 al momento, aun con pareja
if [[ " $partes " == *" sinsesion "* ]]; then
  sec "sinsesion (Soloist no arranca: aunque la tabla tenga su pareja, tu MP3 al momento y ni una peticion)"
  d=$(datos sinsesion); apifalsa sinsesion
  siembra "$d" "MP3:01 bolero.mp3	0	0	f440d032m1bo	MXA112600001	32000	32000	1	1	-3	-5	-8	0.95	isrc" "MP3:02 vals.mp3	0	0	-	-	0	32000	0	$(date +%s)	-	-	-	-	-"
  abre sinsesion "$d" "7:67" "9:pon 01 bolero.mp3;9.5:pon 02 vals.mp3;20:mira;21:copia" 22 TERM $ROS ROCOLA_SOLOIST_BIN=/bin/true
  fin; o="$W/sinsesion"; f="$o/frec.txt"; b=$(bit "$d")
  t330=$(primero "$f" 330)
  si "sinsesion: sale con 0, ni una peticion" bash -c "[ '$(cat $o/salio)' = 0 ] && [ $(v1 sinsesion) = 0 ]"
  si "sinsesion: tu MP3 suena (330 Hz: $(oye "$f" 330) s, desde ${t330:-?} s del sonado: sin esperar los 6 s) y nada de Spotify" bash -c "grep -q '^mira .*01 bolero.mp3 .* lector=xp' '$(log sinsesion)' && [ -n '$t330' ] && python3 -c 'import sys; sys.exit(0 if $(oye "$f" 330) >= 8 and $t330 < 3 and $(oye "$f" 440) <= 0.1 else 1)'"
  si "sinsesion: no se sustituye ni se busca (Soloist sin sesion)" bash -c "! grep -q 'suena desde Spotify' '$b' && ! grep -q 'se busca su pareja' '$b'"
fi

# ================================================================ (revision «sonido-cuota-y-fallos»)
# ---------------------------------------------------------------- caida: la API en 503 todo el rato: dos peticiones (la y su reintento) y ya
if [[ " $partes " == *" caida "* ]]; then
  sec "caida (la API contesta 503 a todo: la busqueda no llega UNA vez y no se vuelve a preguntar cada segundo; suena tu MP3)"
  d=$(datos caida); apifalsa caida
  abre caida "$d" "7:67" "8:5xx;9:pon relleno.mp3;9.5:pon 01 bolero.mp3;45:mira;60:copia" 61 TERM $ROS
  fin; o="$W/caida"; f="$o/frec.txt"; b=$(bit "$d")
  grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; cuota caida
  si "caida: sale con 0" test "$(cat $o/salio)" = 0
  si "caida: DOS peticiones a /v1 en 51 s con la mala en la cola y sonando (la del ISRC y su reintento de 5xx: $(v1 caida))" bash -c "[ $(v1 caida) = 2 ] && [ \$(grep -c ' 503 q=isrc:MXA112600001' '$o/api/peticiones.txt') = 2 ]"
  si "caida: un solo renglon (la API contesto 503) y se vuelve a mirar en 5 min" bash -c "[ \$(grep -c '01 bolero.mp3: la busqueda de su pareja no llego' '$b') = 1 ] && grep -q '01 bolero.mp3: la busqueda de su pareja no llego (la API contesto 503): nada se apunta; si sigue en la cola, se vuelve a mirar en 5 min' '$b'"
  si "caida: suena tu MP3 (330 Hz: $(oye "$f" 330) s; lector=xp) y nada se apunta en la tabla" bash -c "grep -q '^mira .*01 bolero.mp3 .* lector=xp' '$(log caida)' && python3 -c 'import sys; sys.exit(0 if $(oye "$f" 330) >= 25 else 1)' && [ -z \"\$(par '$d' '01 bolero.mp3')\" ]"
fi

# ---------------------------------------------------------------- atasca: Soloist se queda en «buffering» sin sonido a media cancion
if [[ " $partes " == *" atasca "* ]]; then
  sec "atasca (Soloist se queda en «buffering» sin mandar nada: a los 5 s tu MP3 desde donde iba, no a los 30; y la siguiente vez otra vez desde Spotify)"
  d=$(datos atasca); apifalsa atasca
  abre atasca "$d" "7:67" "9:pon relleno.mp3;9.5:pon 06 cortada.mp3;70:pon 06 cortada.mp3;105:copia" 106 TERM $ROS SOLOIST_FALSO_GUION="42:atasca;56:sigue"
  fin; o="$W/atasca"; f="$o/frec.txt"; b=$(bit "$d")
  grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; cuota atasca
  p240=$(primero "$f" 240)
  a770=$(awk -v t="${p240:-0}" '$3>=755 && $3<=785 && $2<=t+0.05 {s+=$2-$1} END {printf "%.2f\n", s+0}' "$f")
  u770=$(awk -v t="${p240:-0}" '$3>=755 && $3<=785 && $2<=t+0.05 && $2-$1>=0.3 {u=$2} END {print u+0}' "$f")
  d770=$(awk -v t="${p240:-0}" '$3>=755 && $3<=785 && $1>t {s+=$2-$1} END {printf "%.2f\n", s+0}' "$f")
  desde=$(sed -n 's/.*vuelve a tu MP3: 06 cortada.mp3 (Spotify se atasco 5 s a media cancion), desde \([0-9.]*\) s.*/\1/p' "$b" | head -1)
  t240=$(oye "$f" 240)
  si "atasca: sale con 0" test "$(cat $o/salio)" = 0
  si "atasca: vuelve a tu MP3 por el atasco, desde donde iba (${desde:-?} s; sono de Spotify $a770 s)" bash -c "[ -n '$desde' ] && python3 -c 'import sys; sys.exit(0 if 10 <= $desde and abs($desde - $a770) <= 1.5 else 1)'"
  si "atasca: el hueco entre lo ultimo de Spotify y tu MP3, de 2,5 a 8 s y no los 30 del nucleo (${u770:-?} -> ${p240:-?}; 5 s de lo entregado, menos el colchon de la salida)" entre "$(echo "${p240:-0} - ${u770:-0}" | bc)" 2.5 8
  si "atasca: y suena el resto del MP3 (240 Hz: $t240 s; 32 - ${desde:-?})" bash -c "python3 -c 'import sys; sys.exit(0 if abs($t240 - (32 - ${desde:-0})) <= 1.5 else 1)'"
  # (la segunda vez se pide cuando tu MP3 ya acabo: XP no deja pedir la que suena)
  si "atasca: la siguiente vez, otra vez desde Spotify (dos «suena desde Spotify»; 770 Hz despues: $d770 s)" bash -c "[ \$(grep -c 'suena desde Spotify: 06 cortada.mp3' '$b') = 2 ] && python3 -c 'import sys; sys.exit(0 if $d770 >= 25 else 1)'"
  si "atasca: sin devolver ni cobrar como de Spotify, y al_mp3=1" bash -c "! grep -qE 'DEVUELTA|COBRADA' '$d/rocola_spotify.txt' && [ '$(cal atasca al_mp3)' = 1 ]"
fi

# ---------------------------------------------------------------- seguida: la pista que sigue del mismo disco, con la frontera continua
if [[ " $partes " == *" seguida "* ]]; then
  sec "seguida (la pista que sigue del mismo disco y empalma con musica: suena su MP3 por el relevo sin pausa de XP, sin hueco, aunque tenga pareja)"
  d=$(datos seguida); apifalsa seguida
  siembra "$d" "MP3:02 vals.mp3	0	0	f550d032m1va	-	32000	32000	1	1	-3	-5	-8	0.95	titulo"
  abre seguida "$d" "7:67" "9:pon relleno.mp3;9.5:pon 01 bolero.mp3;10:pon 02 vals.mp3;75:mira;96:copia" 97 TERM $ROS
  fin; o="$W/seguida"; f="$o/frec.txt"; b=$(bit "$d")
  grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; cuota seguida
  u440=$(ultimo "$f" 440); p360=$(primero "$f" 360)
  si "seguida: sale con 0" test "$(cat $o/salio)" = 0
  si "seguida: el bolero (su pareja por ISRC, 1 peticion) suena desde Spotify (440 Hz: $(oye "$f" 440) s)" bash -c "[ $(v1 seguida) = 1 ] && grep -q 'suena desde Spotify: 01 bolero.mp3' '$b' && python3 -c 'import sys; sys.exit(0 if 31 <= $(oye "$f" 440) <= 33 else 1)'"
  si "seguida: el vals, con pareja, suena de su MP3 (360 Hz: $(oye "$f" 360) s; 550: $(oye "$f" 550); lector=xp)" bash -c "! grep -q 'suena desde Spotify: 02 vals.mp3' '$b' && grep -q '^mira .*02 vals.mp3 .* lector=xp' '$(log seguida)' && python3 -c 'import sys; sys.exit(0 if $(oye "$f" 360) >= 30 and $(oye "$f" 550) <= 0.1 else 1)'"
  si "seguida: sin hueco entre las dos (${u440:-?} -> ${p360:-?})" entre "$(echo "${p360:-0} - ${u440:-0}" | bc)" -0.3 0.4
fi

# ---------------------------------------------------------------- apagon (revision «invisible-y-reglas»): tras un corte de luz con una
# sustituida sonando, el estado de XP es el de tus MP3 (nada de «spotify:»), lo pagado no se devuelve (R5 es solo para lo que se pidio de
# Spotify) ni se cobra otra vez, y al volver la rocola retoma como XP: la que sonaba desde el principio y la de la cola despues. (La de la
# cola es la 03 y no la 02: entre dos pistas seguidas de un disco continuo suena tu MP3 por el relevo de XP, RocolaOsCalidadEmpalma.)
if [[ " $partes " == *" apagon "* ]]; then
  sec "apagon (corte de luz con una sustituida sonando: el estado de XP es el de tus MP3; al volver, ni devuelve ni cobra)"
  d=$(datos apagon); apifalsa apagon
  siembra "$d" "MP3:01 bolero.mp3	0	0	f440d032m1bo	MXA112600001	32000	32000	1	1	-3	-5	-8	0.95	isrc" "MP3:03 cumbia.mp3	0	0	f660d032m1cu	MXA112600003	32000	32000	1	1	-3	-5	-8	0.95	isrc"
  abre apagon1 "$d" "7:67" "9:pon 01 bolero.mp3;9.3:pon 03 cumbia.mp3;17:mira" 21 KILL $ROS
  cp "$d/rocola_estado.txt" "$W/apagon-estado.txt" 2>/dev/null; fin; sleep 3
  apifalsa apagon2
  abre apagon2 "$d" "" "26:mira;52:mira;53:copia" 54 TERM $ROS
  fin; b=$(bit "$d"); e="$W/apagon-estado.txt"
  grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; sed 's/^/   1: /' "$(log apagon1)"; sed 's/^/   2: /' "$(log apagon2)"; tr -d '\r' < "$e" | sed 's/^/   estado: /'
  si "apagon: antes del corte sonaba desde Spotify (lector=spotify, 2 creditos, 1 en la cola)" bash -c "grep -q '^mira .*01 bolero.mp3 .* lector=spotify creditos=2 cola=1' '$(log apagon1)'"
  si "apagon: el estado de XP lleva tus MP3 (sonando el bolero, en la cola la cumbia), 2 creditos y nada de «spotify:»" bash -c "tr -d '\\r' < '$e' | grep -qx 'creditos=2' && tr -d '\\r' < '$e' | grep -q '^sonando=S|.*01 bolero.mp3$' && tr -d '\\r' < '$e' | grep -q '^cola=S|.*03 cumbia.mp3$' && ! grep -q 'spotify:' '$e'"
  si "apagon: al volver retoma la que sonaba, con los mismos 2 creditos (nada devuelto como de Spotify) y la de la cola detras" bash -c "grep -q '^mira .*01 bolero.mp3 .* creditos=2 cola=1' '$(log apagon2)' && grep -q '^mira .*03 cumbia.mp3 .* creditos=2 cola=0' '$(log apagon2)'"
  t440=$(oye "$W/apagon2/frec.txt" 440); t660=$(oye "$W/apagon2/frec.txt" 660); t330=$(oye "$W/apagon2/frec.txt" 330); t390=$(oye "$W/apagon2/frec.txt" 390)
  si "apagon: al volver el bolero suena UNA vez, entero, desde Spotify o tu MP3 (440+330: $t440+$t330 s) y la cumbia detras (660+390: $t660+$t390 s)" bash -c "python3 -c 'import sys; a=$t440+$t330; v=$t660+$t390; sys.exit(0 if 31 <= a <= 33 and 15 <= v <= 33 else 1)'"
  si "apagon: ni COBRADA ni DEVUELTA como de Spotify, ni avisos de credito devuelto" bash -c "! grep -qE 'COBRADA|DEVUELTA' '$d/rocola_spotify.txt' && ! grep -qs 'credito devuelto' '$W/apagon1/traza.txt' '$W/apagon2/traza.txt'"
  si "apagon: al volver, Soloist ya listo, la de la cola suena desde Spotify (660 Hz: $t660 s; 390: $t390 s)" bash -c "grep -q '^mira .*03 cumbia.mp3 .* lector=spotify' '$(log apagon2)' && python3 -c 'import sys; sys.exit(0 if $t660 >= 15 and $t390 <= 0.1 else 1)'"
  si "apagon: ninguna tabla de XP con «spotify:» (ni el estado)" bash -c "! grep -rlsF 'spotify:' '$d' --include='rocola_*.txt' | grep -vE 'rocola_spotify.txt|rocola_os_bitacora.txt|rocola_error.txt' | grep -q ."
fi

# ---------------------------------------------------------------- tarde (revision «invisible-y-reglas»): Soloist tiene sesion cuando lo de
# la cola ya se abrio por delante como MP3: se suelta y, al tocarle, suena desde Spotify (antes, esa vez, tu MP3)
if [[ " $partes " == *" tarde "* ]]; then
  sec "tarde (Soloist llega tarde: lo que XP abrio por delante como MP3 se suelta y la de la cola suena desde Spotify)"
  d=$(datos tarde); apifalsa tarde
  siembra "$d" "MP3:01 bolero.mp3	0	0	f440d032m1bo	MXA112600001	32000	32000	1	1	-3	-5	-8	0.95	isrc" "MP3:03 cumbia.mp3	0	0	f660d032m1cu	MXA112600003	32000	32000	1	1	-3	-5	-8	0.95	isrc"
  abre tarde "$d" "7:67" "8:pon 01 bolero.mp3;8.3:pon 03 cumbia.mp3;20:mira;55:mira;56:copia" 57 TERM $ROS ROCOLA_SOLOIST_BIN="$G/vigilante-tarde.sh" VIGILANTE_TARDE=14
  fin; o="$W/tarde"; f="$o/frec.txt"; b=$(bit "$d")
  grep -F '[calidad_spotify]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'; sed 's/^/   /' "$(log tarde)"
  si "tarde: sale con 0" test "$(cat $o/salio)" = 0
  si "tarde: la primera, sin sesion aun, tu MP3 al momento (330 Hz: $(oye "$f" 330) s; lector=xp)" bash -c "grep -q '^mira .*01 bolero.mp3 .* lector=xp' '$(log tarde)' && python3 -c 'import sys; sys.exit(0 if 31 <= $(oye "$f" 330) <= 33 else 1)'"
  si "tarde: lo abierto por delante como MP3 se suelta en cuanto Soloist tiene sesion" contiene "$b" "03 cumbia.mp3: lo que XP habia abierto por delante como MP3 se suelta (ya va desde Spotify)"
  si "tarde: y la de la cola suena desde Spotify (660 Hz: $(oye "$f" 660) s) y nada de su MP3 (390: $(oye "$f" 390) s)" bash -c "grep -q '^mira .*03 cumbia.mp3 .* lector=spotify creditos=2 cola=0' '$(log tarde)' && python3 -c 'import sys; sys.exit(0 if $(oye "$f" 660) >= 10 and $(oye "$f" 390) <= 0.1 else 1)'"
  si "tarde: ni COBRADA ni DEVUELTA" bash -c "! grep -qE 'COBRADA|DEVUELTA' '$d/rocola_spotify.txt'"
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; exit 0; else echo "$mal MAL de $((bien + mal))"; exit 1; fi
