#!/bin/bash
# LA PRUEBA DEL GRUPO LETRAS (fase 11: RocolaOs/RocolaOs.Letras.cs, RocolaOs.LetrasGenius.cs y RocolaOs.LetraSuena.cs; FASE11_letras.md).
# Con MP3 INVENTADOS (hazmp3.py: un tono y una USLT inventada), un Genius y un LRCLIB FALSOS (falso.py, en 127.0.0.1, con datos.json) y, para
# lo de Spotify, la API web falsa y el Soloist falso del grupo catalogo. Nunca letras de canciones de verdad ni llaves de verdad.
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   W: la carpeta de trabajo (por defecto /root/rocola-trabajo/fase11/letras/prueba; se rehace)
# escenas: orden (la letra de lo que suena con la orden --prueba-letra-cielo: LRCLIB, el MP3, /run, sin red) apagado (sin ROCOLA_OS: nada
#   de Rocola OS) l3 (la letra de tus MP3 en el buscador) tres (frases de 3 palabras: nada) genius (L1 con la llave falsa: las tres delante)
#   choque (genius, y mientras Genius busca en Spotify, se teclea y se borra una «x» cada 0,12 s: la de Spotify no sale como NO ESTA)
#   cero (ROCOLA_OS=1 con las cuatro apagadas: la traza igual a la de apagado) sinllave (L1 encendida sin llave: ni una peticion) suena (la letra de la cancion que suena, en la rocola entera); todas, de fabrica
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
escenas="${*:-orden apagado cero l3 tres genius choque sinllave suena}"
W=${W:-/root/rocola-trabajo/fase11/letras/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
CAT="$G/../catalogo"; SX="$G/../spotify-xp"
API=${API:-/root/rocola-trabajo/fase11/letras/api-falsa}
FALSO=${FALSO:-/root/rocola-trabajo/fase11/letras/soloist-falso}
CAPTURAS=${CAPTURAS:-}
LLAVE=llave-falsa-de-genius-0123456789
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"; [ -n "$CAPTURAS" ] && mkdir -p "$CAPTURAS"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
cuenta() { local n; n=$(grep -cE -- "$2" "$1" 2>/dev/null); echo "${n:-0}"; }

# EL FALSO (Genius y LRCLIB): un puerto libre por escena
P=$((21000 + $$ % 20000))
falso() { local o="$W/$1/falso"; mkdir -p "$o"; P=$((P + 1)); python3 "$G/falso.py" $P "$o" "$G/datos.json" > "$o/salida.txt" 2>&1 & FPID=$!
  for i in $(seq 1 100); do [ -f "$o/listo" ] && break; sleep 0.1; done; : > "$o/peticiones.txt"; }
para_falso() { kill $FPID 2>/dev/null; wait $FPID 2>/dev/null; }

# LA API DE SPOTIFY FALSA Y EL SOLOIST FALSO (los del grupo catalogo), solo para la escena genius
prepara_spotify() {
  if [ ! -f "$API/api-falsa.dll" ] || [ "$CAT/ApiFalsa/Program.cs" -nt "$API/api-falsa.dll" ]; then
    mkdir -p "$API"; flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$CAT/ApiFalsa/ApiFalsa.csproj" -c Release -o "$API" --disable-build-servers \
      -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$API/obj/" > "$API/build.txt" 2>&1 || { cat "$API/build.txt"; return 1; }
  fi
  if [ ! -f "$FALSO/soloist-falso.dll" ] || [ "$SX/SoloistFalso/Program.cs" -nt "$FALSO/soloist-falso.dll" ]; then
    mkdir -p "$FALSO"; flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$SX/SoloistFalso/SoloistFalso.csproj" -c Release -o "$FALSO" --disable-build-servers \
      -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$FALSO/obj/" > "$FALSO/build.txt" 2>&1 || { cat "$FALSO/build.txt"; return 1; }
  fi
}
SP=$((41000 + $$ % 15000))
apifalsa() { local o="$W/$1/api"; mkdir -p "$o"; SP=$((SP + 1)); dotnet "$API/api-falsa.dll" $SP "$o" id-de-prueba secreto-de-prueba refresco-1 > "$o/salida.txt" 2>&1 & APIPID=$!
  for i in $(seq 1 100); do [ -f "$o/listo" ] && break; sleep 0.1; done; : > "$o/peticiones.txt"; }
# la tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c): suena a su ritmo
AL=$(dirname "$W")/alsa; mkdir -p "$AL"
[ -f "$AL/libasound_module_pcm_reloj.so" ] || gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1

# LOS DATOS: los de partida de las escenas (DATOS0) y, en su musica, los MP3 inventados de «Los Probados» (LETRAS inventadas)
datos() {   # NOMBRE [rocola_os.txt...]: la carpeta
  local d="$W/datos-$1"; shift; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/
  local m="$d/musica/Los Probados - Disco Letras"; mkdir -p "$m"
  python3 "$G/hazmp3.py" "$m/01 Lluvia Inventada.mp3" 3 "Lluvia Inventada" "Los Probados" "Disco Letras" "voy andando bajo la lluvia\nsin paraguas por la avenida\nmojado y contento de verdad\nhasta que salga el arcoiris" >/dev/null
  python3 "$G/hazmp3.py" "$m/02 Amor de Prueba.mp3" 21 "Amor de Prueba" "Los Probados" "Disco Letras" - >/dev/null
  python3 "$G/hazmp3.py" "$m/03 Luna de Papel Brillante.mp3" 3 "Luna de Papel Brillante" "Los Probados" "Disco Letras" - >/dev/null
  python3 "$G/hazmp3.py" "$m/04 Noche Sin Nombre.mp3" 3 "Noche Sin Nombre" "Los Probados" "Disco Letras" "en la ventana colgaba\nuna luna de papel brillante\nque nadie quiso bajar\ny el gato la miraba" >/dev/null
  python3 "$G/hazmp3.py" "$m/05 Tres Palabras.mp3" 3 "Tres Palabras" "Los Probados" "Disco Letras" "sopla el viento frio constante\nsobre los techos de lamina\nla tarde se hace pequena\ncuando nadie contesta" >/dev/null
  : > "$d/rocola_os.txt"; for l in "$@"; do echo "$l" >> "$d/rocola_os.txt"; done
  echo "$d"
}
# LA ROCOLA: abre NOMBRE DATOS "PULSA" SEGUNDOS [VAR=valor...]: fuera de pantalla; a los SEGUNDOS, SIGTERM (el apagado)
abre() {
  local n="$1" d="$2" pulsa="$3" seg="$4"; shift 4
  local o="$W/$n"; mkdir -p "$o/run" "$o/soloist"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "/dev/null" "$o/registro.txt" > "$o/alsa.conf"
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_GENIUS_API="http://127.0.0.1:$P" ROCOLA_LETRA_API="http://127.0.0.1:$P" ROCOLA_LETRAS_MUESTRA=1 \
      ROCOLA_SPOTIFY_API="http://127.0.0.1:$SP/v1" ROCOLA_SPOTIFY_CUENTAS="http://127.0.0.1:$SP" \
      ROCOLA_SOLOIST_DATOS="$o/soloist" ROCOLA_SOLOIST_BIN="$SX/vigilante-falso.sh" SOLOIST_FALSO_BIN="$FALSO" SOLOIST_FALSO_LOG="$o/sfalso.txt" VIGILANTE_FALSO_LOG="$o/vigilante.txt" \
      "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  ROCOLA=$!
  ( sleep "$seg"; kill -TERM $ROCOLA 2>/dev/null; sleep 30; kill -KILL $ROCOLA 2>/dev/null ) & VIG=$!
}
espera() { wait $ROCOLA; echo $? > "$W/$1/salio"; kill $VIG 2>/dev/null; wait $VIG 2>/dev/null; }
# teclas: lo escrito desde el segundo S (SIN «c» ni «s»: sus codigos, 67 y 83, son la MONEDA y SALTAR en los datos de partida), una tecla cada 0,25 s (las letras con su codigo de Windows, el espacio 32)
escribe() { local s="$1" pal="$2" r="" i c; for ((i = 0; i < ${#pal}; i++)); do c=$(printf '%d' "'${pal:$i:1}"); [ $c -ne 32 ] && c=$((c - 32)); r="$r,$(echo "$s + $i * 0.25" | bc):$c"; done; echo "${r#,}"; }
fin() { echo "$1 + ${#2} * 0.25" | bc; }   # cuando acaba de escribirse
derechas() { local s="$1" n="$2" r="" i; for ((i = 0; i < n; i++)); do r="$r,$(echo "$s + $i * 0.15" | bc):39"; done; echo "${r#,}"; }
BASE="7:67,8:27+38"   # la moneda a los 7 s y el buscador a los 8 s (REGRESAR+ARRIBA)
# lo ultimo que ensenaba el buscador con esa consulta (lo dice la rocola con ROCOLA_LETRAS_MUESTRA=1, solo con ROCOLA_OS)
ultimo() { grep -F "BUSCADOR «$2»" "$1/salida.txt" | tail -1; }
# el numero de la tarjeta (desde 1) en la que va TEXTO en esa linea, o 0
lugar() { echo "$1" | awk -v t="$2" -F' [|] ' '{ for (i = 2; i <= NF; i++) if (index($i, t)) { print i - 1; exit } print 0 }'; }
borra() { local s="$1" n="$2" r="" i; for ((i = 0; i < n; i++)); do r="$r,$(echo "$s + $i * 0.12" | bc):27"; done; echo "${r#,}"; }
sinllave_en() { ! grep -rlF "$LLAVE" "$@" 2>/dev/null | grep -v 'rocola_genius.txt$' | grep -q .; }

# LA ORDEN: --prueba-letra-cielo (sin la rocola: solo la busqueda de la letra y el verso de cada posicion)
orden() { local o="$1"; shift; env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_DATOS="$o/datos" ROCOLA_RUN="$o/run" "$@"; }

for e in $escenas; do
  echo "== $e"
  case "$e" in
  orden)
    falso $e; o="$W/$e"; mkdir -p "$o/datos" "$o/run" "$o/mp3"; f="$o/falso/peticiones.txt"
    python3 "$G/hazmp3.py" "$o/mp3/solo.mp3" 2 "Solo del MP3" "Los Probados" "Disco Letras" "renglon inventado uno\nrenglon inventado dos\nrenglon inventado tres" >/dev/null
    python3 "$G/hazmp3.py" "$o/mp3/nada.mp3" 2 "Sin Letra" "Los Probados" "Disco Letras" - >/dev/null
    L="ROCOLA_LETRA_API=http://127.0.0.1:$P"
    # el tope de /run: 405 letras viejas inventadas ya puestas; al poner la nueva quedan 300 y la nueva entre ellas
    mkdir -p "$o/run/rocola-xp/letras"; for i in $(seq 1 405); do printf 'x\t0\t/viejo/%d\nrenglon\n' $i > "$o/run/rocola-xp/letras/viejo$i.lrc"; done; touch -d '2020-01-01' "$o/run/rocola-xp/letras/"viejo*.lrc
    orden "$o" env $L "${RX[@]}" --prueba-letra-cielo "Amor de Prueba" "Los Probados" "Disco Letras" 20 - -1 0 1.99 2 5.5 12.3 18 19.9 30 otra > "$o/sinc.txt" 2>&1
    sed 's/^/   /' "$o/sinc.txt"
    si "orden: LRCLIB sincronizada, 11 versos (con el vacio del final)" contiene "$o/sinc.txt" "vuelta=1 letra=sincronizada proveedor=LRCLIB versos=11 peticiones=1 fallidas=0"
    si "orden: antes del primero, ninguno y el siguiente el 0" contiene "$o/sinc.txt" "pos=-1 verso=-1 «» siguiente=0 «verso inventado numero 0»"
    si "orden: 0 s -> el 0" contiene "$o/sinc.txt" "pos=0 verso=0 «verso inventado numero 0» siguiente=1"
    si "orden: 1,99 s -> el 0" contiene "$o/sinc.txt" "pos=1.99 verso=0 «verso inventado numero 0» siguiente=1"
    si "orden: 2 s -> el 1" contiene "$o/sinc.txt" "pos=2 verso=1 «verso inventado numero 1» siguiente=2 «verso inventado numero 2»"
    si "orden: 5,5 s -> el 2" contiene "$o/sinc.txt" "pos=5.5 verso=2 «verso inventado numero 2» siguiente=3"
    si "orden: 12,3 s -> el 6" contiene "$o/sinc.txt" "pos=12.3 verso=6 «verso inventado numero 6» siguiente=7"
    si "orden: 19,9 s -> el 9, sin siguiente (el ultimo renglon esta vacio)" contiene "$o/sinc.txt" "pos=19.9 verso=9 «verso inventado numero 9» siguiente=-1 «»"
    si "orden: 30 s -> el vacio del final" contiene "$o/sinc.txt" "pos=30 verso=10 «» siguiente=-1"
    si "orden: otra vez la misma: de memoria (sigue 1 peticion)" contiene "$o/sinc.txt" "vuelta=2 letra=sincronizada proveedor=LRCLIB versos=11 peticiones=1"
    si "orden: la peticion con titulo, artista, disco y duracion" contiene "$f" "/api/get track_name=Amor de Prueba&artist_name=Los Probados&album_name=Disco Letras&duration=20 | ua=rocola-xp/1 (Rocola OS) | auth=no"
    si "orden: /run guarda la letra (rocola-xp/letras/*.lrc) y nada en los datos" bash -c "ls '$o/run/rocola-xp/letras/'*.lrc >/dev/null 2>&1 && [ -z \"\$(ls -A '$o/datos')\" ]"
    si "orden: el tope de /run (405 viejas + la nueva -> 300, la nueva se queda)" bash -c "[ \$(ls '$o/run/rocola-xp/letras/'*.lrc | wc -l) = 300 ] && [ \$(ls '$o/run/rocola-xp/letras/'*.lrc | grep -vc '/viejo') = 1 ]"
    # otro arranque con el mismo /run: de /run, sin peticion (y la letra usada se toca: el recorte quita las menos usadas)
    nueva=$(ls "$o/run/rocola-xp/letras/"*.lrc | grep -v '/viejo' | head -1); touch -d '2020-06-01' "$nueva"
    orden "$o" env $L "${RX[@]}" --prueba-letra-cielo "Amor de Prueba" "Los Probados" "Disco Letras" 20 - 7 > "$o/run2.txt" 2>&1
    si "orden: la letra leida de /run se toca (su fecha ya no es la de 2020)" bash -c "[ \$(date -r '$nueva' +%Y) != 2020 ]"
    si "orden: otro arranque con el mismo /run: de /run, 0 peticiones, y el verso a 7 s es el 3" bash -c "grep -q 'vuelta=1 letra=sincronizada proveedor=LRCLIB versos=11 peticiones=0' '$o/run2.txt' && grep -q 'pos=7 verso=3 «verso inventado numero 3»' '$o/run2.txt'"
    # el offset y dos tiempos en un renglon
    orden "$o" env $L "${RX[@]}" --prueba-letra-cielo "Doble Tiempo" "Los Probados" "" 0 - 0.4 0.6 4.6 8.6 > "$o/doble.txt" 2>&1
    sed 's/^/   /' "$o/doble.txt"
    si "orden: [offset:+500] adelanta medio segundo y un renglon con dos tiempos sale dos veces" bash -c "grep -q 'pos=0.4 verso=-1' '$o/doble.txt' && grep -q 'pos=0.6 verso=0 «estribillo repetido»' '$o/doble.txt' && grep -q 'pos=4.6 verso=1 «verso del medio»' '$o/doble.txt' && grep -q 'pos=8.6 verso=2 «estribillo repetido»' '$o/doble.txt'"
    si "orden: sin disco ni duracion, la peticion sin album_name ni duration" contiene "$f" "/api/get track_name=Doble Tiempo&artist_name=Los Probados | ua="
    si "orden: y /api/get dice 400 (sin disco ni duracion): una busqueda /api/search por titulo y artista, de la que sale la letra" bash -c "grep -q '^/api/search track_name=Doble Tiempo&artist_name=Los Probados | ua=' '$f' && grep -q 'vuelta=1 letra=sincronizada proveedor=LRCLIB versos=3' '$o/doble.txt'"
    # LRCLIB no la tiene: la USLT del MP3 (sin tiempos)
    orden "$o" env $L "${RX[@]}" --prueba-letra-cielo "Solo del MP3" "Los Probados" "Disco Letras" 2 "$o/mp3/solo.mp3" 1 > "$o/mp3.txt" 2>&1
    sed 's/^/   /' "$o/mp3.txt"
    si "orden: LRCLIB 404 -> la USLT del MP3, sin tiempos, 3 renglones" contiene "$o/mp3.txt" "vuelta=1 letra=sin_tiempos proveedor=el MP3 versos=3 peticiones=1 fallidas=1"
    si "orden: sin tiempos no hay verso de ahora" contiene "$o/mp3.txt" "pos=1 verso=-1 «» siguiente=-1"
    # LRCLIB solo sin tiempos y el MP3 sin letra
    orden "$o" env $L "${RX[@]}" --prueba-letra-cielo "Solo Texto" "Los Probados" "Disco Letras" 2 "$o/mp3/nada.mp3" > "$o/texto.txt" 2>&1
    si "orden: LRCLIB sin tiempos (plainLyrics) cuando el MP3 no trae letra" contiene "$o/texto.txt" "vuelta=1 letra=sin_tiempos proveedor=LRCLIB versos=2 peticiones=1"
    # ninguna en ningun lado, tres veces: una sola peticion (otro MP3: /run va por la ruta de la cancion)
    cp "$o/mp3/nada.mp3" "$o/mp3/nada2.mp3"
    orden "$o" env $L "${RX[@]}" --prueba-letra-cielo "No La Tiene Nadie" "Los Probados" "Disco Letras" 2 "$o/mp3/nada2.mp3" otra otra > "$o/ninguna.txt" 2>&1
    si "orden: ninguna en ningun lado, tres veces: UNA peticion" bash -c "grep -q 'vuelta=3 letra=ninguna proveedor= versos=0 peticiones=1 fallidas=1' '$o/ninguna.txt' && [ \$(grep -c 'track_name=No La Tiene Nadie' '$f') = 1 ]"
    # EL FRENO (acortado a 1,5 s con ROCOLA_LETRA_FRENO_MS) en UNA sola corrida: «Cae Con 503» (el falso contesta 503) frena; otra cancion
    # en el freno no pregunta; pasado el freno, la otra SI pregunta (y sale), y la del 503 se vuelve a intentar (no queda sin letra la sesion)
    orden "$o" env $L ROCOLA_LETRA_FRENO_MS=1500 "${RX[@]}" --prueba-letra-cielo "Cae Con 503" "Los Probados" "Disco Letras" 2 - "cancion=Solo Texto" "cancion=Cae Con 503" pausa=1800 "cancion=Solo Texto" "cancion=Cae Con 503" > "$o/freno.txt" 2>&1
    sed 's/^/   /' "$o/freno.txt"
    si "orden: el 503: ninguna, 1 peticion" contiene "$o/freno.txt" "vuelta=1 letra=ninguna proveedor= versos=0 peticiones=1 fallidas=1 cancion=Cae Con 503"
    si "orden: otra cancion en el freno: ninguna y sin peticion" contiene "$o/freno.txt" "vuelta=2 letra=ninguna proveedor= versos=0 peticiones=1 fallidas=1 cancion=Solo Texto"
    si "orden: la del 503 otra vez en el freno: sin peticion" contiene "$o/freno.txt" "vuelta=3 letra=ninguna proveedor= versos=0 peticiones=1 fallidas=1 cancion=Cae Con 503"
    si "orden: pasado el freno, la otra SI pregunta y sale (no se quedo sin letra la sesion)" contiene "$o/freno.txt" "vuelta=4 letra=sin_tiempos proveedor=LRCLIB versos=2 peticiones=2 fallidas=1 cancion=Solo Texto"
    si "orden: y la del 503 se vuelve a intentar (una peticion mas, nunca en bucle)" contiene "$o/freno.txt" "vuelta=5 letra=ninguna proveedor= versos=0 peticiones=3 fallidas=2 cancion=Cae Con 503"
    para_falso
    # sin red: nada, y en el freno de 2 minutos ni una peticion mas (las tres vueltas son la misma cancion; el freno con otra, arriba)
    orden "$o" env ROCOLA_LETRA_API=http://127.0.0.1:9 "${RX[@]}" --prueba-letra-cielo "Amor Sin Red" "Los Probados" "Disco Letras" 20 - otra otra > "$o/sinred.txt" 2>&1
    sed 's/^/   /' "$o/sinred.txt"
    si "orden: sin red: ninguna, una sola peticion en tres vueltas" contiene "$o/sinred.txt" "vuelta=3 letra=ninguna proveedor= versos=0 peticiones=1 fallidas=1"
    orden "$o" env ROCOLA_LETRA_API=http://127.0.0.1:9 "${RX[@]}" --prueba-letra-cielo "Amor Sin Red" "Los Probados" "Disco Letras" 20 "$o/mp3/solo.mp3" > "$o/sinred2.txt" 2>&1
    si "orden: sin red, con la USLT del MP3: la del MP3" contiene "$o/sinred2.txt" "letra=sin_tiempos proveedor=el MP3 versos=3"
    si "orden: nada del dueno en el User-Agent (solo el programa)" bash -c "! grep -v 'ua=rocola-xp/1 (Rocola OS) |' '$f' | grep -q ."
    ;;
  l3)
    # LA LETRA DE TUS MP3: (1) «luna de papel brillante»: lo exacto de XP (el titulo de la 03) primero y en su orden; detras la 04, que la
    # trae en su letra. (2) «perro viento frio tarde»: 3 de 4 en la letra de la 05 -> sale. (3) «perro azul viento frio»: 2 de 4 -> no.
    falso $e; d=$(datos $e spotify=0)
    F1="luna de papel brillante"; F2="perro viento frio tarde"; F3="perro azul viento frio"
    t1=$(fin 9.5 "$F1"); b1=$(echo "$t1 + 2.5" | bc); s2=$(echo "$b1 + ${#F1} * 0.12 + 0.5" | bc); t2=$(fin $s2 "$F2"); b2=$(echo "$t2 + 2.5" | bc); s3=$(echo "$b2 + ${#F2} * 0.12 + 0.5" | bc); t3=$(fin $s3 "$F3")
    T="$BASE,$(escribe 9.5 "$F1"),$(borra $b1 ${#F1}),$(escribe $s2 "$F2"),$(borra $b2 ${#F2}),$(escribe $s3 "$F3")"
    abre $e "$d" "$T" $(echo "$t3 + 4" | bc | cut -d. -f1) ROCOLA_OS=1 ROCOLA_CAPTURA="$( [ -n "$CAPTURAS" ] && echo "$(echo "$t1 + 2" | bc):$CAPTURAS/buscador-letra-mp3.png" || echo "$(echo "$t1 + 2" | bc):$W/$e/buscador.png")"
    espera $e; para_falso; o="$W/$e"
    u1=$(ultimo $o "$F1"); u2=$(ultimo $o "$F2"); u3=$(ultimo $o "$F3")
    echo "   $u1"; echo "   $u2"; echo "   $u3"
    si "l3: sale con 0" test "$(cat $o/salio)" = 0
    si "l3: el repaso miro los 5 MP3 inventados y 3 traen letra" bash -c "grep -q 'MP3 mirados, 3 con letra' '$d/rocola_os_bitacora.txt' && [ \$(grep -c 'Los Probados' '$d/rocola_letras_mp3.txt') = 5 ]"
    si "l3: lo exacto de XP primero: la 03 (titulo) es la 1a tarjeta" test "$(lugar "$u1" "Luna de Papel Brillante")" = 1
    n1=$(echo "$u1" | awk -F' [|] ' '{print NF-1}')
    si "l3: la 04 (por su letra) detras de todo lo de XP: la ultima tarjeta ($n1)" bash -c "[ $(lugar "$u1" "Noche Sin Nombre") = $n1 ] && [ $n1 -ge 2 ]"
    si "l3: la casilla de XP las cuenta (2 resultados)" bash -c "echo '$u1' | grep -qF '«$F1» (2) |'"
    si "l3: 3 de 4 palabras en la letra (perro viento frio tarde): la 05" test "$(lugar "$u2" "Tres Palabras")" -ge 1
    si "l3: 2 de 4 (perro azul viento frio): no sale" test "$(lugar "$u3" "Tres Palabras")" = 0
    si "l3: rocola_letras_mp3.txt solo con palabras sueltas en orden alfabetico (ningun renglon de la letra)" bash -c "! cut -f4 '$d/rocola_letras_mp3.txt' | grep -qE 'luna papel|viento frio|bajo lluvia' && awk -F'\t' '\$4 != \"\" { n = split(\$4, w, \" \"); for (i = 2; i <= n; i++) if (w[i-1] >= w[i]) exit 1 }' '$d/rocola_letras_mp3.txt'"
    si "l3: sin acentos ni vacias (pequena, lamina; sin «la», «de», «el»)" bash -c "cut -f4 '$d/rocola_letras_mp3.txt' | grep -q ' lamina ' && ! cut -f4 '$d/rocola_letras_mp3.txt' | grep -qwE 'la|de|el'"
    si "l3: Genius apagado de fabrica: ni una peticion a Genius (y ninguna a LRCLIB: no sono nada)" test "$(wc -l < $o/falso/peticiones.txt)" = 0 ;;
  tres)
    # FRASES DE 3 PALABRAS: «viento frio tarde» esta entera en la letra de la 05, pero son 3 palabras: lo de XP tal cual (nada)
    falso $e; d=$(datos $e spotify=0)
    F1="viento frio tarde"; t1=$(fin 9.5 "$F1")
    abre $e "$d" "$BASE,$(escribe 9.5 "$F1")" $(echo "$t1 + 4" | bc | cut -d. -f1) ROCOLA_OS=1
    espera $e; para_falso; o="$W/$e"; u1=$(ultimo $o "$F1"); echo "   $u1"
    si "tres: sale con 0" test "$(cat $o/salio)" = 0
    si "tres: el buscador lo dijo" test -n "$u1"
    si "tres: 3 palabras: la 05 no sale (ningun falso)" test "$(lugar "$u1" "Tres Palabras")" = 0 ;;
  apagado|cero)
    # SIN ROCOLA_OS (apagado) y con ROCOLA_OS=1 y las cuatro apagadas (cero): una frase de 4 palabras que esta en una letra; nada de
    # Rocola OS: ni el repaso, ni rocola_letras_mp3.txt, ni /run, ni una peticion; y cero, la traza igual a la de apagado sin lo de RocolaOs
    falso $e; F1="luna de papel brillante"; t1=$(fin 9.5 "$F1")
    if [ $e = apagado ]; then d=$(datos $e); rm -f "$d/rocola_os.txt"; abre $e "$d" "$BASE,$(escribe 9.5 "$F1")" $(echo "$t1 + 4" | bc | cut -d. -f1) ROCOLA_OS=
    else d=$(datos $e spotify=0 letras_mp3=0 busca_letra=0 letra_cielo=0); abre $e "$d" "$BASE,$(escribe 9.5 "$F1")" $(echo "$t1 + 4" | bc | cut -d. -f1) ROCOLA_OS=1; fi
    espera $e; para_falso; o="$W/$e"
    si "$e: sale con 0" test "$(cat $o/salio)" = 0
    si "$e: ni rocola_letras_mp3.txt ni /run/rocola-xp/letras" bash -c "[ ! -e '$d/rocola_letras_mp3.txt' ] && [ ! -e '$o/run/rocola-xp/letras' ]"
    si "$e: ni una peticion a Genius ni a LRCLIB" test "$(wc -l < $o/falso/peticiones.txt)" = 0
    si "$e: en la traza nada de letras" bash -c "! grep -qE 'letras_mp3|busca_letra|letra_cielo' '$o/traza.txt'"
    if [ $e = apagado ]; then si "apagado: ni la bitacora de Rocola OS ni lo que ensena el buscador (solo con ROCOLA_OS)" bash -c "[ ! -e '$d/rocola_os_bitacora.txt' ] && ! grep -q 'BUSCADOR' '$o/salida.txt'"
    elif [ -f "$W/apagado/traza.txt" ]; then
      python3 "$XP/Pruebas/Ventanas/guiones/compara_trazas.py" "$W/apagado/traza.txt" "$o/traza.txt" --ignora RocolaOs: --holgura 80 > "$o/compara.txt" 2>&1; r=$?
      sed 's/^/   /' "$o/compara.txt" | head -8
      si "cero: la traza igual a la de sin ROCOLA_OS quitando lo de RocolaOs (compara_trazas.py --ignora, holgura 80)" test $r = 0
    fi ;;
  genius|choque)
    # L1 ENCENDIDA CON LA LLAVE FALSA (y el modo Spotify con su API falsa): «bajo la lluvia por la avenida». Genius dice 4: «Lluvia Inventada»
    # (tuya), «Tema Tema de Mentira» de Los Inventados (no la tienes: esta en Spotify), «Cancion Desconocida (En Vivo)» (en ningun lado) y
    # «Lluvia Inventada (Remix)» (la misma que la 1a: no cuenta). Delante, en ese orden: la tuya, la de Spotify y la tarjeta NO ESTA.
    prepara_spotify || exit 1
    falso $e; apifalsa $e
    d=$(datos $e spotify=1 busca_letra=1)
    printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\n' > "$d/rocola_spotify_api.txt"; chmod 600 "$d/rocola_spotify_api.txt"
    printf '%s\n' "$LLAVE" > "$d/rocola_genius.txt"; chmod 600 "$d/rocola_genius.txt"
    F1="bajo la lluvia por la avenida"; t1=$(fin 9.5 "$F1")
    T="$BASE,$(escribe 9.5 "$F1")"
    # choque: desde 0,75 s despues de escribir (Genius ya pregunto: 0,8 s tras la ultima tecla) hasta 3,6 s, «x» y borrar cada 0,12 s (cada tecla, otra busqueda de lo escrito en Spotify,
    # justo cuando el hilo de Genius pide «titulo artista»); al final queda la frase
    if [ $e = choque ]; then for i in $(seq 0 11); do T="$T,$(echo "$t1 + 0.75 + $i * 0.24" | bc):88,$(echo "$t1 + 0.87 + $i * 0.24" | bc):27"; done; fi
    # (choque: 16 s, para que con el fallo de antes -10 s esperando una busqueda reemplazada- se viera la NO ESTA)
    abre $e "$d" "$T" $(echo "$t1 + $( [ $e = choque ] && echo 16 || echo 9)" | bc | cut -d. -f1) ROCOLA_OS=1 ROCOLA_CAPTURA="$( [ -n "$CAPTURAS" ] && echo "$(echo "$t1 + 7" | bc):$CAPTURAS/buscador-$e.png" || echo "$(echo "$t1 + 7" | bc):$W/$e/buscador.png")"
    espera $e; para_falso; kill $APIPID 2>/dev/null; wait $APIPID 2>/dev/null; o="$W/$e"; f="$o/falso/peticiones.txt"; a="$o/api/peticiones.txt"
    u1=$(ultimo $o "$F1"); echo "   $u1"; sed 's/^/   genius: /' "$f"
    si "$e: sale con 0" test "$(cat $o/salio)" = 0
    if [ $e = genius ]; then
    si "$e: UNA pregunta a Genius (a los 0,8 s de dejar de escribir), con la frase entera y la llave en la cabecera" bash -c "[ \$(grep -c '^/search ' '$f') = 1 ] && grep -q '^/search per_page=5&q=bajo la lluvia por la avenida | ua=rocola-xp/1 | auth=bien' '$f'"
    else
    # (choque: el buscador de XP solo «posa» la consulta al quedarse quieta, asi que la de la «x» estuvo a la vista mas de 0,8 s y tambien
    # se pregunta; la frase entera, UNA vez)
    si "$e: la frase entera UNA vez a Genius (y como mucho la de la «x», que estuvo a la vista)" bash -c "[ \$(grep -c '^/search per_page=5&q=bajo la lluvia por la avenida | ' '$f') = 1 ] && [ \$(grep -c '^/search ' '$f') -le 2 ]"
    si "$e: la de Spotify no sale como NO ESTA" bash -c "! echo '$u1' | grep -q 'noesta:Tema Tema'"
    fi
    si "$e: la llave nunca en la direccion de la peticion" nocontiene "$f" "$LLAVE"
    si "$e: 1a delante, TU cancion (Lluvia Inventada)" test "$(lugar "$u1" "2:Los Probados - 01 Lluvia Inventada")" = 1
    si "$e: 2a, la de Spotify (Tema Tema de Mentira, por la busqueda «titulo artista»)" bash -c "[ $(lugar "$u1" "spotify:Los Inventados - Tema Tema de Mentira") = 2 ] && grep -q 'q=tema tema de mentira los inventados' '$a'"
    si "$e: 3a, la tarjeta NO ESTA con lo que dice Genius" test "$(lugar "$u1" "noesta:Cancion Desconocida (En Vivo)")" = 3
    si "$e: la casilla las cuenta tambien con el modo Spotify (3 resultados, no «Sin resultados»)" bash -c "echo '$u1' | grep -qF '«$F1» (3) |'"
    si "$e: la 4a de Genius (Remix de la misma) no es otra tarjeta, y la tuya no sale dos veces" bash -c "[ \$(echo '$u1' | grep -o 'Lluvia Inventada' | wc -l) = 1 ] && [ \$(echo '$u1' | grep -o 'noesta:' | wc -l) = 1 ]"
    si "$e: la llave nunca en los datos (salvo su archivo), /run, la salida, la traza ni la bitacora" sinllave_en "$d" "$o/run" "$o/salida.txt" "$o/traza.txt"
    si "$e: nada de Genius guardado en los datos (ni el titulo de la NO ESTA)" bash -c "! grep -rlF 'Cancion Desconocida' '$d' | grep -q ."
    si "$e: el encargo con su nombre (RocolaOs:busca_letra:llego)" contiene "$o/traza.txt" "RocolaOs:busca_letra:llego" ;;
  sinllave)
    # L1 ENCENDIDA SIN LLAVE: ni una pregunta a Genius (y el buscador de XP con lo de la letra de tus MP3, como siempre)
    falso $e; d=$(datos $e spotify=0 busca_letra=1); F1="bajo la lluvia por la avenida"; t1=$(fin 9.5 "$F1")
    abre $e "$d" "$BASE,$(escribe 9.5 "$F1")" $(echo "$t1 + 5" | bc | cut -d. -f1) ROCOLA_OS=1
    espera $e; para_falso; o="$W/$e"; u1=$(ultimo $o "$F1"); echo "   $u1"
    si "sinllave: sale con 0" test "$(cat $o/salio)" = 0
    si "sinllave: ni una peticion a Genius" test "$(grep -c '^/search' $o/falso/peticiones.txt)" = 0
    si "sinllave: la bitacora lo dice" contiene "$d/rocola_os_bitacora.txt" "sin la llave de Genius"
    si "sinllave: la letra de tus MP3 si (la 01 por «bajo lluvia avenida»), sin tarjetas de Genius" bash -c "[ $(lugar "$u1" "01 Lluvia Inventada") -ge 1 ] && ! echo '$u1' | grep -q noesta:" ;;
  suena)
    # LA LETRA DE LO QUE SUENA (letra_cielo, encendida de fabrica): se pide «amor de prueba» (21 s; el LRCLIB falso tiene su LRC de 10 versos,
    # uno cada 2 s) y suena por la tarjeta con reloj. El estado (/run/rocola-xp/letras/estado) se mira cada 0,5 s: sus versos avanzan
    falso $e; d=$(datos $e spotify=0); F1="amor de prueba"; t1=$(fin 9.5 "$F1")
    T="$BASE,$(escribe 9.5 "$F1"),$(derechas $(echo "$t1 + 1" | bc) 14),$(echo "$t1 + 4" | bc):13"
    abre $e "$d" "$T" $(echo "$t1 + 34" | bc | cut -d. -f1) ROCOLA_OS=1
    o="$W/$e"; est="$o/run/rocola-xp/letras/estado"
    ( t0=$(date +%s.%N); while kill -0 $ROCOLA 2>/dev/null; do if [ -f "$est" ]; then echo "$(echo "$(date +%s.%N) - $t0" | bc) $(tr '\n' ' ' < "$est")"; fi; sleep 0.5; done ) > "$o/muestras.txt" &
    MU=$!
    espera $e; wait $MU 2>/dev/null; para_falso; f="$o/falso/peticiones.txt"
    grep -o 'verso=[-0-9]*' "$o/muestras.txt" | uniq -c | tr '\n' ' ' | sed 's/^/   versos vistos: /'; echo
    sed 's/^/   lrclib: /' "$f"
    si "suena: sale con 0" test "$(cat $o/salio)" = 0
    si "suena: UNA peticion a LRCLIB, con el titulo, el artista, el disco y la duracion de la que suena" bash -c "[ \$(wc -l < '$f') = 1 ] && grep -q '^/api/get track_name=Amor de Prueba&artist_name=Los Probados&album_name=Disco Letras&duration=21 | ua=rocola-xp/1 (Rocola OS)' '$f'"
    si "suena: la letra sincronizada de LRCLIB (11 versos)" contiene "$o/muestras.txt" "letra=sincronizada proveedor=LRCLIB versos=11"
    vs=$(grep -o ' verso=[0-9]*' "$o/muestras.txt" | cut -d= -f2 | uniq | tr '\n' ' ')
    si "suena: los versos avanzan en orden ($vs) y se vieron al menos 7 distintos" bash -c "echo '$vs' | awk '{ for (i = 2; i <= NF; i++) if (\$i < \$(i-1)) exit 1; exit (NF >= 7 ? 0 : 1) }'"
    si "suena: con cada verso, el siguiente es el que le sigue (hasta el 8)" bash -c "grep -oE 'verso=[0-8] siguiente=[-0-9]+' '$o/muestras.txt' | awk -F'[= ]' '{ n++; if (\$4 != \$2 + 1) bad = 1 } END { exit (bad || n < 7) }'"
    si "suena: al acabar, nada suena (letra=nada_suena)" bash -c "tail -1 '$o/muestras.txt' | grep -q 'letra=nada_suena'"
    si "suena: la letra en /run (su .lrc) y nada de ella en los datos" bash -c "grep -lq 'verso inventado' '$o/run/rocola-xp/letras/'*.lrc && ! grep -rlF 'verso inventado' '$d' | grep -q ."
    si "suena: el estado sin texto de la letra" nocontiene "$est" "verso inventado" ;;
  *) echo "(escena desconocida: $e)"; mal=$((mal + 1)) ;;
  esac
done
echo "== $bien BIEN, $mal MAL"
[ $mal = 0 ]
