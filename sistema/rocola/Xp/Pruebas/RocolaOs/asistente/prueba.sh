#!/bin/bash
# LA PRUEBA DEL ASISTENTE CON SPOTIFY Y DE LA CASILLA «AYUDA» (fase 11, grupo asistente-spotify: RocolaOs/RocolaOs.Asistente*.cs;
# FASE11_asistente-spotify.md). La rocola entera, fuera de pantalla (1024x768), sin sonido, con los datos INVENTADOS de las escenas de
# ventanas (DATOS0), la API web de Spotify FALSA (catalogo/ApiFalsa) y el Soloist FALSO del nucleo (spotify-xp/SoloistFalso, por su
# vigilante falso; NUNCA el Soloist de verdad). Las teclas, con ROCOLA_PULSA (codigos de Windows: arriba 38, abajo 40, izq 37, der 39,
# poner 13, regresar 27, moneda 67). La ayuda se abre con ROJO mantenido 1 s y ABAJO 0,3 s despues (como la escena asistente de las ventanas).
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   W: la carpeta de trabajo (por defecto /root/rocola-trabajo/fase11/asistente/prueba; se rehace)
#   CAPTURAS: si se pone, las capturas (PNG) van ahi
# escenas: menu pide letra sinletra artista tarjetas mas mejores xp sinllave ayuda1024 (todas, de fabrica)
# (junta 3, revision) con ROCOLA_OS=1 el cielo pide el lugar y el clima por internet sin llave, y la letra va a LRCLIB: en la prueba,
# todo eso va al sumidero 127.0.0.1:7 (nada escucha) para no depender de la red ni de lo que conteste un servicio de fuera; una escena
# que ponga su propio falso lo pone delante y manda
export ROCOLA_CLIMA_API="${ROCOLA_CLIMA_API:-http://127.0.0.1:7}" ROCOLA_LUGAR_API="${ROCOLA_LUGAR_API:-http://127.0.0.1:7/lugar}"
export ROCOLA_LETRA_API="${ROCOLA_LETRA_API:-http://127.0.0.1:7}" ROCOLA_GENIUS_API="${ROCOLA_GENIUS_API:-http://127.0.0.1:7}"
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
escenas="${*:-menu pide letra sinletra artista tarjetas mas mejores xp sinllave ayuda1024}"
T0=/root/rocola-trabajo/fase11/asistente
API=${API:-$T0/api-falsa}
W=${W:-$T0/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
CAPTURAS=${CAPTURAS:-}
FALSO=${FALSO:-$T0/falso}
CAT="$G/../catalogo"; SX="$G/../spotify-xp"
# (se rehace si su Program.cs no es el de esta rama: la carpeta la comparten los agentes de varias ramas; antes contaba la fecha)
if [ ! -f "$API/api-falsa.dll" ] || ! cmp -s "$CAT/ApiFalsa/Program.cs" "$API/Program.cs"; then
  echo "(compilando la API falsa en $API)"; mkdir -p "$API"
  flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$CAT/ApiFalsa/ApiFalsa.csproj" -c Release -o "$API" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$API/obj/" > "$API/build.txt" 2>&1 || { cat "$API/build.txt"; exit 1; }
  cp "$CAT/ApiFalsa/Program.cs" "$API/Program.cs"
fi
AL=$T0/alsa; mkdir -p "$AL"
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
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }

P=$((20000 + $$ % 20000))
apifalsa() {
  local o="$W/$1/api"; mkdir -p "$o"; P=$((P + 1))
  dotnet "$API/api-falsa.dll" $P "$o" id-de-prueba secreto-de-prueba refresco-1 > "$o/salida.txt" 2>&1 &
  APIPID=$!
  for i in $(seq 1 100); do [ -f "$o/listo" ] && break; sleep 0.1; done
  : > "$o/peticiones.txt"
}
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
espera() { wait $ROCOLA; echo $? > "$W/$1/salio"; rm -f "$W/$1/sonado.raw"; kill $VIG 2>/dev/null; wait $VIG 2>/dev/null; kill $APIPID 2>/dev/null; wait $APIPID 2>/dev/null; }
datos() {   # NOMBRE MODO(apagado|sinllave|encendido|cuenta)
  local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/
  case "$2" in
    encendido) printf 'spotify=1\n' > "$d/rocola_os.txt"; printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\n' > "$d/rocola_spotify_api.txt" ;;
    cuenta) printf 'spotify=1\n' > "$d/rocola_os.txt"; printf 'client_id=id-de-prueba\nclient_secret=secreto-de-prueba\nrefresh_token=refresco-1\nusuario=dueno-falso\n' > "$d/rocola_spotify_api.txt" ;;
    sinllave) : ;;   # (ROCOLA_OS=1: spotify encendido de fabrica, sin rocola_spotify_api.txt)
    apagado) : ;;
  esac
  [ -f "$d/rocola_spotify_api.txt" ] && chmod 600 "$d/rocola_spotify_api.txt"
  echo "$d"
}
cap() { [ -n "$CAPTURAS" ] && echo "$1:$CAPTURAS/$2.png" || echo "$1:$W/$2.png"; }
capdir() { [ -n "$CAPTURAS" ] && echo "$CAPTURAS" || echo "$W"; }
[ -n "$CAPTURAS" ] && mkdir -p "$CAPTURAS"
escribe() { local s="$1" pal="$2" r="" i c; for ((i = 0; i < ${#pal}; i++)); do c=$(printf '%d' "'${pal:$i:1}"); c=$((c - 32)); r="$r,$(echo "$s + $i * 0.25" | bc):$c"; done; echo "${r#,}"; }
derechas() { local s="$1" n="$2" r="" i; for ((i = 0; i < n; i++)); do r="$r,$(echo "$s + $i * 0.15" | bc):39"; done; echo "${r#,}"; }
cobradas() { grep -cF 'COBRADA spotify:track:' "$1/rocola_spotify.txt" 2>/dev/null; }
bit() { echo "$1/rocola_os_bitacora.txt"; }
# los pasos de la guia que la estrella fue ensenando (la bitacora: «guia de Spotify M: paso N «titulo»»), en una linea
pasos() { grep -o 'paso [0-9]* «[^»]*»' "$(bit "$1")" 2>/dev/null | sed 's/paso [0-9]* «\(.*\)»/\1/' | paste -sd'|'; }
# AYUDA (1 s de ROJO y ABAJO a los 0,3 s) a los S segundos
ayuda() { echo "$1:27~1000,$(echo "$1 + 0.3" | bc):40"; }
# la pregunta de Spotify y su guia N (0, 1 o 2), desde que se abre la ayuda a los S: ABAJO x3, VERDE, ABAJO xN, VERDE
guia() { local s="$1" n="$2" r; r="$(ayuda "$s"),$(echo "$s + 1.5" | bc):40,$(echo "$s + 1.8" | bc):40,$(echo "$s + 2.1" | bc):40,$(echo "$s + 2.6" | bc):13"; local t; t=$(echo "$s + 3.2" | bc)
  for ((i = 0; i < n; i++)); do r="$r,$t:40"; t=$(echo "$t + 0.3" | bc); done; echo "$r,$(echo "$t + 0.3" | bc):13"; }
# cuantos pixeles distintos hay entre dos capturas en un rectangulo (x y ancho alto) con mas de U niveles en algun canal
distintos() { python3 - "$1" "$2" "$3" "$4" "$5" "$6" "$7" <<'PY'
import sys
from PIL import Image, ImageChops
a = Image.open(sys.argv[1]).convert('RGB'); b = Image.open(sys.argv[2]).convert('RGB')
x, y, w, h, u = map(int, sys.argv[3:8])
d = ImageChops.difference(a.crop((x, y, x + w, y + h)), b.crop((x, y, x + w, y + h)))
print(sum(1 for p in d.get_flattened_data() if max(p) > u) if hasattr(d, 'get_flattened_data') else sum(1 for p in d.getdata() if max(p) > u))
PY
}
export -f distintos

for e in $escenas; do
  echo "== $e"
  case "$e" in
  menu)
    # LA PREGUNTA CON SPOTIFY: cuatro opciones; ABAJO x3 llega a «Canciones de Spotify» (y otra vez ABAJO no pasa de ella); VERDE: la
    # pregunta de Spotify (tres); ABAJO x4 se queda en la 3a; ROJO: la de XP con la cuarta elegida; ROJO: se cierra
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$(ayuda 6),7.5:40,7.8:40,8.1:40,8.4:40,9.5:13,10.8:40,11.1:40,11.4:40,11.7:40,13:27,14.5:27" 17 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 7.3 menu-cuatro-opciones),$(cap 9.2 menu-cuarta-elegida),$(cap 10.5 menu-spotify),$(cap 12.5 menu-spotify-tercera),$(cap 14 menu-al-volver),$(cap 16 menu-cerrado)"
    espera $e; o="$W/$e"; c=$(capdir)
    si "menu: sale con 0" test "$(cat $o/salio)" = 0
    si "menu: la bitacora dice la ayuda con Spotify y la pregunta de Spotify (una vez)" bash -c "grep -q 'la ayuda con lo de Spotify (cuatro opciones)' '$(bit $d)' && [ \$(grep -c 'pregunta de Spotify' '$(bit $d)') = 1 ]"
    si "menu: ninguna guia empezo (ROJO en la de Spotify vuelve; ROJO en la de XP cierra)" bash -c "! grep -q 'guia de Spotify' '$(bit $d)'"
    si "menu: las capturas de la pregunta cambian al elegir (cuarta, Spotify, al volver) (al volver, la misma pregunta: dentro del globo, de x=470 en adelante, menos de 1 000 puntos distintos; a su izquierda se ve el disco de atras con su reloj de lo que suena, que cambia entre las dos capturas) y al cerrar no queda el globo" bash -c "[ \$(distintos '$c/menu-cuatro-opciones.png' '$c/menu-cuarta-elegida.png' 430 92 574 499 40) -gt 500 ] && [ \$(distintos '$c/menu-cuarta-elegida.png' '$c/menu-spotify.png' 430 92 574 499 40) -gt 500 ] && [ \$(distintos '$c/menu-cuarta-elegida.png' '$c/menu-al-volver.png' 470 92 534 499 40) -lt 1000 ] && [ \$(distintos '$c/menu-al-volver.png' '$c/menu-cerrado.png' 430 92 574 499 40) -gt 5000 ]" ;;
  pide)
    # GUIA 1, «Pedir una cancion de Spotify»: abre el buscador, escribe «inventad», las tarjetas (la cancion de Spotify sin credito: la
    # moneda; VERDE sin credito no pide; el artista que solo esta en Spotify), la moneda, VERDE: la pide y lo logra
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$(guia 6 0),11.5:27+38,$(escribe 13 inventad),$(derechas 16 14),20:13,21.5:37,23:39,24.5:67,26.5:13" 31 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 10.8 guia1-abre-el-buscador),$(cap 12.6 guia1-escribe),$(cap 19.6 guia1-sin-creditos),$(cap 22.6 guia1-artista-de-spotify),$(cap 26.2 guia1-pidela),$(cap 27.5 guia1-logro)"
    espera $e; o="$W/$e"
    si "pide: sale con 0" test "$(cat $o/salio)" = 0
    si "pide: la guia 1 empezo" contiene "$(bit $d)" "guia de Spotify 4 (Pedir una canción de Spotify)"
    p=$(pasos $d); echo "   pasos: $p"
    si "pide: los pasos en orden (abre, escribe; al pasar por el artista, la moneda; el artista, la moneda, pidela)" test "$p" = "Abre el buscador|Escribe el nombre|Un artista de Spotify|Mete una moneda|Un artista de Spotify|Mete una moneda|Pídela con VERDE"
    si "pide: VERDE sin credito no pidio; con la moneda, una pedida (COBRADA: $(cobradas $d))" test "$(cobradas $d)" = 1
    si "pide: lo logro (pidio una de Spotify)" contiene "$(bit $d)" "guia de Spotify 4: lo logro (pidio una de Spotify)" ;;
  letra)
    # GUIA 2, «La letra ♫ de Spotify» (con la cuenta: la letra ♫ desde el arranque): ROJO+IZQ desde la primera letra llega a la ♫, VERDE
    # abre el primer disco (Tus favoritas), la moneda, VERDE pide la primera
    apifalsa $e; d=$(datos $e cuenta)
    abre $e "$d" "$(guia 6 1),12:27+37,14:13,16.5:67,18.5:13" 23 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 11.6 guia2-ve-a-la-letra),$(cap 13.5 guia2-elige-un-disco),$(cap 16 guia2-moneda),$(cap 18.2 guia2-elige-tu-cancion),$(cap 19.5 guia2-logro)"
    espera $e; o="$W/$e"
    si "letra: sale con 0" test "$(cat $o/salio)" = 0
    si "letra: la guia 2 empezo" contiene "$(bit $d)" "guia de Spotify 5 (La letra ♫ de Spotify)"
    p=$(pasos $d); echo "   pasos: $p"
    # (22-11-2026, decision del dueno: la musica de Spotify ya no esta en la pared, sin letra ♫: RocolaOsSpotifyLetraEnLaPared = false)
    # aun con la cuenta la guia manda al buscador, y con estas teclas (las de la letra) no se pide nada
    si "letra: sin letra ♫ en la pared, la guia manda al buscador («Spotify, en el buscador»)" bash -c "echo '$p' | grep -q '^Spotify, en el buscador'"
    si "letra: nada cobrado sin buscar (COBRADA: $(cobradas $d))" test "$(cobradas $d)" = 0 ;;
  sinletra)
    # (revision) GUIA 2 SIN LA LETRA ♫ (el modo y la llave de la API, sin la cuenta: no hay letra ♫): «Aun no hay letra ♫» manda al buscador
    # con ROJO+ARRIBA y sigue como la guia 1 (con su numero de paso, no «SIGAMOS»): «inventad», una cancion de Spotify, la moneda, VERDE
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$(guia 6 1),12:27+38,$(escribe 13 inventad),$(derechas 16 14),20.5:67,22.5:13" 26 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 11.6 guia2-sin-letra),$(cap 15.5 guia2-sin-letra-escribe),$(cap 20 guia2-sin-letra-moneda),$(cap 22.2 guia2-sin-letra-pidela),$(cap 23.5 guia2-sin-letra-logro)"
    espera $e; o="$W/$e"
    si "sinletra: sale con 0" test "$(cat $o/salio)" = 0
    p=$(pasos $d); echo "   pasos: $p"
    si "sinletra: «Spotify, en el buscador», el buscador, la moneda y «Pidela con VERDE»" bash -c "echo '$p' | grep -q '^Spotify, en el buscador|Escribe el nombre|.*Mete una moneda|Pídela con VERDE$'"
    si "sinletra: una pedida (COBRADA: $(cobradas $d)) y lo logro" bash -c "[ $(cobradas $d) = 1 ] && grep -q 'guia de Spotify 5: lo logro (pidio una de Spotify)' '$(bit $d)'" ;;
  artista)
    # GUIA 3, «Los discos de un artista»: el buscador, «interminable», la tarjeta de Banda Interminable (solo en Spotify), su pared («Canciones
    # de» en la 1a casilla; luego sus discos), entrar a un disco, REGRESAR a la pared, REGRESAR al buscador: lo logra
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$(guia 6 2),12:27+38,$(escribe 13.5 interminable),$(derechas 17 14),21.5:13,25:39,27:13,29.5:27,31.5:27" 35 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 11.6 guia3-abre-el-buscador),$(cap 13.2 guia3-escribe-el-artista),$(cap 21 guia3-artista-de-spotify),$(cap 24.5 guia3-canciones-de),$(cap 26.5 guia3-pared-de-discos),$(cap 29 guia3-vuelve-con-rojo),$(cap 31 guia3-vuelve-al-buscador),$(cap 32.5 guia3-logro)"
    espera $e; o="$W/$e"
    si "artista: sale con 0" test "$(cat $o/salio)" = 0
    p=$(pasos $d); echo "   pasos: $p"
    si "artista: los pasos en orden" test "$p" = "Abre el buscador|Escribe el artista|Un artista de Spotify|Sus canciones|Su pared de discos|Vuelve con ROJO|Vuelve al buscador"
    si "artista: lo logro (volvio al buscador) y nada se cobro" bash -c "grep -q 'guia de Spotify 6: lo logro (volvio al buscador)' '$(bit $d)' && [ $(cobradas $d) = 0 ]" ;;
  tarjetas)
    # GUIA 1 con «mentira» (una cancion, un disco y «Todos los discos de»), con credito: la cancion (sin la moneda), a la derecha el
    # disco, de vuelta, abajo todos los discos (debajo de la cancion) y de vuelta
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "5:67,$(guia 6 0),11.5:27+38,$(escribe 13 mentira),$(derechas 15.5 14),19:39,20.5:37,21:40,22.5:38" 25 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 18.5 guia1-cancion-con-credito),$(cap 20 guia1-un-disco-de-spotify),$(cap 22.2 guia1-todos-los-discos)"
    espera $e; o="$W/$e"
    si "tarjetas: sale con 0" test "$(cat $o/salio)" = 0
    p=$(pasos $d); echo "   pasos: $p"
    si "tarjetas: la cancion con credito (sin la moneda), el disco y todos los discos, y de vuelta" bash -c "echo '$p' | grep -q 'Pídela con VERDE|Un disco de Spotify|Pídela con VERDE|Todos los discos|Pídela con VERDE' && ! echo '$p' | grep -q 'Mete una moneda'" ;;
  mas)
    # GUIA 3 con «otra»: tu artista Otra Banda, que Spotify tambien tiene: su tarjeta de XP abre la pared unida (23-11-2026; ya no sale
    # «Otra Banda · más en Spotify» detras): «Sus discos»; a la derecha, otra tarjeta; de vuelta, «Sus discos»
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$(guia 6 2),12:27+38,$(escribe 13.5 otra),$(derechas 15 14),17.2:37,18:39,19.5:37" 22 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 17.6 guia3-sus-discos),$(cap 19 guia3-otra-tarjeta)"
    espera $e; o="$W/$e"
    si "mas: sale con 0" test "$(cat $o/salio)" = 0
    p=$(pasos $d); echo "   pasos: $p"
    si "mas: en tu tarjeta, «Sus discos» (la pared unida), y otra vez al volver; nada de «Más en Spotify»" bash -c "echo '$p' | grep -q 'Sus discos|[^|]*|Sus discos' && ! echo '$p' | grep -q 'Más en Spotify'" ;;
  mejores)
    # GUIA 3 con «LAS MEJORES CANCIONES DE…» (27-11-2026; RocolaOs.Mejores.cs): el buscador, «Escribe el artista» (su texto habla del atajo de
    # arriba), ARRIBA dos veces y VERDE ponen el prefijo, se escribe «otra» y, en la tarjeta de primera, el paso «Sus mejores canciones»
    apifalsa $e; d=$(datos $e encendido)
    abre $e "$d" "$(guia 6 2),12:27+38,14:38,14.5:38,16:13,$(escribe 17.5 otra),$(derechas 22 9)" 30 ROCOLA_OS=1 \
      ROCOLA_CAPTURA="$(cap 13.2 guia3-mejores-escribe-el-artista),$(cap 20.5 guia3-mejores-con-el-prefijo),$(cap 25.5 guia3-mejores-su-tarjeta)"
    espera $e; o="$W/$e"
    si "mejores: sale con 0" test "$(cat $o/salio)" = 0
    p=$(pasos $d); echo "   pasos: $p"
    si "mejores: «Escribe el artista» y, en la tarjeta de las mejores canciones, «Sus mejores canciones»" bash -c "echo '$p' | grep -q 'Escribe el artista' && echo '$p' | grep -q 'Sus mejores canciones$'"
    si "mejores: nada cobrado (solo se llego a la tarjeta)" test "$(cobradas $d)" = 0 ;;
  xp|sinllave)
    # SIN SPOTIFY = EL ASISTENTE DE XP TAL CUAL: sin ROCOLA_OS (xp) y con ROCOLA_OS=1 (spotify encendido de fabrica) sin la llave de la API
    # (sinllave), lo mismo: la pregunta de tres (ABAJO x4 se queda en la 3a), «Aprender a moverme» con sus pasos, ROJO+ABAJO la guarda; otra
    # vez, «Poner una cancion». Se comparan las trazas (quitando lo de RocolaOs) y las capturas en el globo y en la fila de arriba
    apifalsa $e; d=$(datos $e $([ $e = xp ] && echo apagado || echo sinllave))
    K="$(ayuda 6),7.5:40,7.8:40,8.1:40,8.4:40,9.5:13,11:39,12.5:40,14:27~1000,14.3:40,17:27~1000,17.3:40,18.6:13,20:27~1000,20.3:40"
    CA="$(cap 7.3 $e-menu),$(cap 9.2 $e-menu-tercera),$(cap 10.8 $e-aprender),$(cap 12.2 $e-aprender-2),$(cap 13.6 $e-aprender-3),$(cap 15.5 $e-cerrada),$(cap 19.6 $e-poner)"
    mkdir -p "$W/$e/texturas"
    # (sin llave el vigilante de verdad espera la llave sin abrir nada; el falso abriria el Soloist falso, otro dotnet que le quita CPU a la
    # rocola y cambia las cuentas de la traza: aqui, uno que solo espera, como el de verdad sin llave)
    printf '#!/bin/bash\nwhile kill -0 $PPID 2>/dev/null; do sleep 0.5; done\n' > "$W/vigilante-espera.sh"; chmod +x "$W/vigilante-espera.sh"
    if [ $e = xp ]; then abre $e "$d" "$K" 23 ROCOLA_OS= ROCOLA_CAPTURA="$CA" ROCOLA_VOLCADO_TEXTURAS="$W/$e/texturas"; else abre $e "$d" "$K" 23 ROCOLA_OS=1 ROCOLA_CAPTURA="$CA" ROCOLA_VOLCADO_TEXTURAS="$W/$e/texturas" ROCOLA_SOLOIST_BIN="$W/vigilante-espera.sh"; fi
    espera $e; o="$W/$e"
    si "$e: sale con 0" test "$(cat $o/salio)" = 0
    if [ $e = sinllave ]; then
      si "sinllave: ni la ayuda con Spotify ni una peticion a la API" bash -c "! grep -qF '[asistente]' '$(bit $d)' && [ \$(grep -c ' /v1/' '$o/api/peticiones.txt') = 0 ]"
      R=${RUIDO:-/root/rocola-trabajo/port/ventanas/ruido/sigterm.json}
      python3 "$XP/Pruebas/Ventanas/guiones/compara_trazas.py" "$W/xp/traza.txt" "$o/traza.txt" --ignora RocolaOs: --holgura 80 > "$o/compara.txt" 2>&1; r=$?
      sed 's/^/   /' "$o/compara.txt" | head -8
      si "sinllave: la traza igual a la de sin ROCOLA_OS quitando lo de RocolaOs (compara_trazas.py --ignora, holgura 80)" test $r = 0
    fi ;;
  ayuda1024)
    # «AYUDA» SIEMPRE: la fila de arriba a 1024x768 con ROCOLA_OS=1 (la casilla con su palabra) y sin ROCOLA_OS (compacta, como XP)
    apifalsa $e; d=$(datos $e sinllave); abre $e "$d" "" 8 ROCOLA_OS=1 ROCOLA_CAPTURA="$(cap 6 fila-con-rocola-os)"; espera $e
    apifalsa $e; d=$(datos ${e}xp apagado); abre ${e}xp "$d" "" 8 ROCOLA_OS= ROCOLA_CAPTURA="$(cap 6 fila-sin-rocola-os)"; W2=$W/${e}xp; wait $ROCOLA; kill $VIG 2>/dev/null; kill $APIPID 2>/dev/null
    c=$(capdir)
    python3 -c "import sys;from PIL import Image;[Image.open(sys.argv[1]+'/'+n+'.png').crop((0,0,1024,96)).save(sys.argv[1]+'/'+n+'-arriba.png') for n in ('fila-con-rocola-os','fila-sin-rocola-os')]" "$c"
    si "ayuda1024: la fila de arriba cambia con Rocola OS (la casilla dice AYUDA y la cabecera cede)" bash -c "[ \$(distintos '$c/fila-con-rocola-os.png' '$c/fila-sin-rocola-os.png' 0 0 1024 70 40) -gt 100 ]" ;;
  esac
done
# xp contra sinllave: las capturas en el globo y en la fila de arriba (sin las estrellas: mas de 24 niveles)
if echo " $escenas " | grep -q ' xp ' && echo " $escenas " | grep -q ' sinllave '; then
  echo "== xp contra sinllave (los globos que la ayuda subio a la tarjeta, y las capturas)"
  # cada globo que se hornea (la pregunta, 574x499 a 1024x768, y el adios, en una textura de 1024x512; el de la guia, 409x168, en una de
  # 512x256) se sube entero a su textura: el volcado
  # (ROCOLA_VOLCADO_TEXTURAS) los guarda en PNG; se comparan sus puntos, en el orden en que se subieron, sin repetidos
  globos() { python3 - "$1" <<'PY'
import sys, os, hashlib
from PIL import Image
d = sys.argv[1]; vistos = []
for n in sorted(os.listdir(d)):
    if not ('_1024x512.png' in n or '_512x256.png' in n): continue
    h = hashlib.sha1(Image.open(os.path.join(d, n)).convert('RGBA').tobytes()).hexdigest()[:12] + n[n.rfind('_'):-4]
    if h not in vistos: vistos.append(h)
print(' '.join(vistos))
PY
}
  gx=$(globos "$W/xp/texturas"); gs=$(globos "$W/sinllave/texturas")
  echo "   xp:       $gx"; echo "   sinllave: $gs"
  si "xp-sinllave: los mismos globos de la ayuda, punto por punto y en el mismo orden ($(echo $gx | wc -w) distintos)" bash -c "[ -n '$gx' ] && [ '$gx' = '$gs' ] && [ \$(echo '$gx' | wc -w) -ge 5 ]"
  c=$(capdir)
  for n in menu menu-tercera aprender aprender-2 aprender-3 cerrada poner; do
    echo "   $n: puntos distintos (mas de 24 niveles) en la pantalla bajo la fila de arriba: $(distintos "$c/xp-$n.png" "$c/sinllave-$n.png" 0 60 1024 708 24); en la fila de arriba: $(distintos "$c/xp-$n.png" "$c/sinllave-$n.png" 0 0 1024 60 24)"
  done
fi
echo "== $bien BIEN, $mal MAL"
[ $mal = 0 ]
