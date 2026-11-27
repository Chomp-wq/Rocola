#!/bin/bash
# LA PRUEBA DEL NUCLEO DE SPOTIFY (fase 11, grupo spotify-xp; FASE11_spotify-xp.md). rocola-xp de verdad, fuera de pantalla, con la tarjeta
# de prueba con reloj (Pruebas/Windows/alsa/reloj.c: suena a su ritmo y guarda lo que DE VERDAD sono), un Soloist FALSO (SoloistFalso/: la
# API WebSocket de la documentacion de Soloist y el sonido de cada cancion en el anillo de I1, un tono con su firma) abierto por un vigilante
# falso (vigilante-falso.sh, en lugar de rocola-soloist), y el doble del catalogo (la funcion «spotify-prueba»: RocolaOs/RocolaOs.SpotifyPrueba.cs)
# que pide canciones de Spotify por I6 (RocolaOsSpotifyPide). Datos INVENTADOS (los de partida de las escenas de ventanas, DATOS0). NUNCA el
# Soloist de verdad.
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   escenas (de fabrica todas): apagado fabrica dos adelanto adelantoxp adelantoreloj mezcla celular carrera fallos lento fuera frecuencia apagon portada pulso
#   vigilante noche (adelanto y adelantoxp: el play adelantado de la siguiente de Spotify, rapido-spotify: FASE11_rapido-spotify.md); del 22-11-2026: copiauri nivela portadatarde repetida vacia cortada speq (speq: el EQ automatico y el brillo a la medida de una de Spotify, FASE11_spotify-nivela.md §7); del 24-11-2026 (revision de mejoras): cierraws sordo adelantoviejo diag repetidad cortadalarga (cierraws: Soloist que cuelga el WebSocket al instante; sordo: Soloist vivo pero sordo, se pide el reinicio al vigilante con el archivo «reinicia»; adelantoviejo: el archivo sin_adelanto caduca a los 7 dias; diag: sellos en ms, huecos, adelanto usado, eventos raros una vez y las opciones ya sabidas; repetidad: la pareja es otra uri; cortadalarga: con Soloist reiniciandose seguir una cortada tiene 60 s)
#   PULSO: el libpulse.so.0 de rocola-pulso (sistema/rocola/pulso, otro grupo) para las escenas «pulso» y «vigilante» (sin el, no se corren)
#   VIGILANTE: el rocola-soloist de verdad (sistema/board/rocola/rootfs_overlay/usr/bin/rocola-soloist, del grupo soloist) para la escena
#   «vigilante» (sin el, no se corre): rocola-xp lo abre y el abre el Soloist falso por soloist-envoltorio.sh (SOLOIST_BIN); y para «fabrica»
#   (el de verdad, SIN llave: espera la llave y no abre nada)
# De la junta de Spotify (15-11-2026): «fabrica» (el modo encendido de fabrica sin ninguna llave = XP tal cual, sin una peticion a la red y
# con el vigilante esperando sin gastar; contra la misma escena sin ROCOLA_OS) y «noche» (el modo noche de XP baja una de Spotify al
# noche_pct de noche y con «Probar», con su rampa, igual que una de XP; niveles.py; desde el 17-11-2026, la bajada de una hora desde las 22 h)
#   W: la carpeta de trabajo (/root/rocola-trabajo/fase11/spotify-xp/prueba; se rehace); FALSO: el Soloist falso compilado (se compila si
#   falta, con el candado de lo pesado); DATOS0 (/root/rocola-trabajo/port/ventanas/datos0, se copian, nunca se tocan)
# Las cuentas del sonido: frecuencias.py (tramos de 50 ms con la frecuencia dominante). Cada cancion de Spotify de prueba lleva su tono en su
# id (spotify:track:fHZdSEG...): 440, 550, 660, 770, 880 Hz; el autoplay del falso, 990 Hz.
# Cada comprobacion: BIEN o MAL en una linea; al final «TODO BIEN (N comprobaciones)» o cuantas MAL (y sale con 1).
# (junta 3, revision) con ROCOLA_OS=1 el cielo pide el lugar y el clima por internet sin llave, y la letra va a LRCLIB: en la prueba,
# todo eso va al sumidero 127.0.0.1:7 (nada escucha) para no depender de la red ni de lo que conteste un servicio de fuera; una escena
# que ponga su propio falso lo pone delante y manda
export ROCOLA_CLIMA_API="${ROCOLA_CLIMA_API:-http://127.0.0.1:7}" ROCOLA_LUGAR_API="${ROCOLA_LUGAR_API:-http://127.0.0.1:7/lugar}"
export ROCOLA_LETRA_API="${ROCOLA_LETRA_API:-http://127.0.0.1:7}" ROCOLA_GENIUS_API="${ROCOLA_GENIUS_API:-http://127.0.0.1:7}"
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
partes="${*:-apagado fabrica dos adelanto adelantoxp adelantoreloj mezcla celular carrera fallos lento fuera frecuencia apagon portada pulso vigilante noche copiauri nivela portadatarde repetida vacia cortada speq volumen cierraws sordo adelantoviejo diag repetidad cortadalarga arranque sordopide mitad}"
PULSO=${PULSO:-/root/rocola-trabajo/fase11/spotify-xp/pulso/libpulse.so.0}
VIGILANTE=${VIGILANTE:-$XP/../../board/rocola/rootfs_overlay/usr/bin/rocola-soloist}
W=${W:-/root/rocola-trabajo/fase11/spotify-xp/prueba}
FALSO=${FALSO:-/root/rocola-trabajo/fase11/spotify-xp/falso}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
cuenta() { local n; n=$(grep -cF -- "$2" "$1" 2>/dev/null); echo "${n:-0}"; }
entre() { python3 -c "import sys; v=float(sys.argv[1]); sys.exit(0 if float(sys.argv[2]) <= v <= float(sys.argv[3]) else 1)" "$1" "$2" "$3"; }

# ---------- lo que hace falta: el Soloist falso y la tarjeta con reloj ----------
# (se rehace si su Program.cs no es el de esta rama: la carpeta la comparten los agentes de varias ramas; antes contaba la fecha)
if [ ! -f "$FALSO/soloist-falso.dll" ] || ! cmp -s "$G/SoloistFalso/Program.cs" "$FALSO/Program.cs"; then
  echo "(compilando el Soloist falso en $FALSO)"
  flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$G/SoloistFalso/SoloistFalso.csproj" -c Release -o "$FALSO" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$FALSO/obj/" > "$W/falso-build.txt" 2>&1 || { cat "$W/falso-build.txt"; exit 1; }
  cp "$G/SoloistFalso/Program.cs" "$FALSO/Program.cs"
fi
A=$W/../alsa; mkdir -p "$A"
if [ ! -f "$A/libasound_module_pcm_reloj.so" ] || [ "$XP/Pruebas/Windows/alsa/reloj.c" -nt "$A/libasound_module_pcm_reloj.so" ]; then gcc -shared -fPIC -DPIC -O2 -o "$A/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1; fi

# ---------- una corrida ----------
# abre NOMBRE DATOS "PULSA" "PIDE" SEGUNDOS SENAL [VAR=valor...]: la rocola con esos datos, las teclas PULSA (ROCOLA_PULSA), los pedidos de
# Spotify PIDE («SEG:pide URI|TITULO|ARTISTA|DISCO|MS;...», al doble del catalogo; «SEG:copia» copia el estado de /run a estado.txt), y a los SEGUNDOS la SENAL. Deja en $W/NOMBRE: salida.txt,
# traza.txt, run/ (ROCOLA_RUN), soloist/ (sus datos), falso.txt (lo que recibio y paso en el Soloist falso), vigilante.txt, sonado.raw,
# registro.txt, frec.txt, salio y tardo (de la senal a la salida).
abre() {
  local n="$1" d="$2" pulsa="$3" pide="$4" seg="$5" sen="$6"; shift 6
  local o="$W/$n"; mkdir -p "$o/run" "$o/soloist"
  cat > "$o/alsa.conf" <<EOC
pcm_type.reloj { lib "$A/libasound_module_pcm_reloj.so" }
pcm.reloj { type reloj  file "$o/sonado.raw"  registro "$o/registro.txt" }
EOC
  ( cd "$d" && exec env ROCOLA_CATALOGO_URL=http://127.0.0.1:9/catalogo ROCOLA_PORTADAS_CATALOGO=http://127.0.0.1:9 ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf \
      ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" \
      ROCOLA_SOLOIST_DATOS="$o/soloist" ROCOLA_SOLOIST_BIN="$G/vigilante-falso.sh" SOLOIST_FALSO_BIN="$FALSO" \
      SOLOIST_FALSO_LOG="$o/falso.txt" VIGILANTE_FALSO_LOG="$o/vigilante.txt" "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$!
  # los pedidos, a su hora (el doble del catalogo los lee en su latido)
  ( IFS=';'; for x in $pide; do [ -z "$x" ] && continue; s="${x%%:*}"; l="${x#*:}"
      while [ "$(echo "$(date +%s.%N) - $T0 < $s" | bc)" = 1 ]; do sleep 0.1; done
      mkdir -p "$o/run/rocola-xp/spotify"
      if [ "$l" = copia ]; then cp "$o/run/rocola-xp/spotify/estado" "$o/estado.txt" 2>/dev/null; cp "$o/run/rocola-soloist/estado" "$o/vigilante-estado.txt" 2>/dev/null; else echo "$l" >> "$o/run/rocola-xp/spotify/prueba-pide"; fi; done ) &
  local q=$!
  sleep "$seg"; kill $q 2>/dev/null
  [ "$sen" != NADA ] && kill -"$sen" $p 2>/dev/null; local t0; t0=$(date +%s.%N)
  ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
  echo "$(date +%s.%N) - $t0" | bc > "$o/tardo"
  # (revision) con el registro de la tarjeta: cada apertura de la salida a su ritmo (XP abre a 48 kHz para una de 48 kHz)
  [ -f "$o/sonado.raw" ] && python3 "$G/frecuencias.py" "$o/sonado.raw" "$([ -f "$o/registro.txt" ] && echo "$o/registro.txt" || echo 44100)" > "$o/frec.txt"
}
datos() { local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/; rm -f "$d/rocola_estado.txt"; echo "$d"; }
oye() { [ -f "$1" ] || { echo 0.00; return; }; awk -v f="$2" '$3>=f-15 && $3<=f+15 {s+=$2-$1} END {printf "%.2f\n", s+0}' "$1"; }   # segundos que se oyo HZ
primero() { awk -v f="$2" '$3>=f-15 && $3<=f+15 && $2-$1>=0.3 {print $1; exit}' "$1"; }    # cuando empezo a oirse HZ (de verdad)
ultimo() { awk -v f="$2" '$3>=f-15 && $3<=f+15 && $2-$1>=0.3 {u=$2} END {print u+0}' "$1"; }
log() { echo "$1/run/rocola-xp/spotify/prueba-pide.log"; }
# (revision) los saltos de la onda de HZ en lo que sono (cuadros perdidos o repetidos; saltos.py), sin los 0,3 s de cada punta
saltos() { local d h r; d=$(primero "$1" "$3"); h=$(ultimo "$1" "$3"); [ -z "$d" ] && { echo -1; return; }
  r=$(python3 "$G/saltos.py" "$2" "$4" "$(echo "$d + 0.3" | bc)" "$(echo "$h - 0.3" | bc)"); echo "$r" >&2; echo "$r" | awk '{print $2}'; }
# (revision) cuantos tramos de OTRA cosa (silencio u otro tono) hay entre el primer y el ultimo sonido de HZ: un hueco dentro de la cancion
huecos() { awk -v f="$2" 'BEGIN{p=-1} {es=($3>=f-15 && $3<=f+15); if (es) {if (p>=0 && n>0) h+=n; p=NR; n=0} else if (p>=0) n++} END {print h+0}' "$1"; }
hzde() { echo "$1/registro.txt"; }   # (lo que saltos.py necesita para saber el ritmo de cada tramo)
estado() { sed -n "s/^$2=//p" "$1/estado.txt" 2>/dev/null; }   # (el estado de /run copiado con «SEG:copia», antes de cerrar)
T0=0
P="Prueba|Artista inventado|Disco inventado"

# ---------------------------------------------------------------- apagado: sin el modo nada de Spotify (sin ROCOLA_OS, o con «1» y spotify=0)
# (antes R1, «apagado de fabrica»; desde la decision del 15-11-2026 va encendido de fabrica: con «1» lo apaga spotify=0, lo de «spotify apagar»)
if [[ " $partes " == *" apagado "* ]]; then
  echo "== apagado (sin ROCOLA_OS, y con ROCOLA_OS=1 y spotify=0)"
  for v in sin uno; do
    d=$(datos apagado-$v); T0=$(date +%s.%N); [ $v = uno ] && printf 'spotify=0\n' > "$d/rocola_os.txt"
    if [ $v = sin ]; then abre apagado-$v "$d" "7:67,9:13,10.5:13" "11:pide spotify:track:f440d008aa|$P|8000" 16 TERM env -u ROCOLA_OS
    else abre apagado-$v "$d" "7:67,9:13,10.5:13" "11:pide spotify:track:f440d008aa|$P|8000" 16 TERM ROCOLA_OS=1; fi
    o="$W/apagado-$v"
    si "apagado-$v: sale con 0" test "$(cat $o/salio)" = 0
    si "apagado-$v: no se abrio el vigilante (ni Soloist)" test ! -e "$o/vigilante.txt" -a ! -e "$o/falso.txt"
    si "apagado-$v: nada de Spotify en /run (ni estado, ni anillo) y el pedido sin atender" bash -c "[ ! -e '$o/run/rocola-xp/spotify/estado' ] && [ ! -e '$o/run/rocola-xp/spotify/sonido.anillo' ] && [ ! -e '$(log $o)' ]"
    si "apagado-$v: ni rocola_spotify.txt" test ! -e "$d/rocola_spotify.txt"
    si "apagado-$v: en la traza nada de RocolaOs:spotify" nocontiene "$o/traza.txt" "RocolaOs:spotify"
    si "apagado-$v: la cancion de XP sono (lo de siempre)" bash -c "[ \$(awk '\$3>0 {s+=\$2-\$1} END {print int(s)}' '$o/frec.txt') -ge 2 ]"
  done
fi

# ---------------------------------------------------------------- fabrica: el modo ENCENDIDO de fabrica, sin ninguna llave = XP tal cual
# (decision del 15-11-2026). ROCOLA_OS=1 (lo del lanzador) sin rocola_os.txt, sin la llave de Soloist ni la de la API, con el vigilante DE
# VERDAD (rocola-soloist, que sin llave no baja ni abre nada); contra la misma escena sin ROCOLA_OS: moneda, una de XP, el buscador con
# «inventad» y REGRESAR. Las dos bajo strace (solo connect y execve, con seccomp: lo demas no se detiene) para contar las peticiones a la
# red (connect a una direccion de internet: AF_INET o AF_INET6) y los programas que se abren.
escribe() { local s="$1" pal="$2" r="" i c; for ((i = 0; i < ${#pal}; i++)); do c=$(printf '%d' "'${pal:$i:1}"); c=$((c - 32)); r="$r,$(echo "$s + $i * 0.25" | bc):$c"; done; echo "${r#,}"; }
if [[ " $partes " == *" fabrica "* ]]; then
  echo "== fabrica (encendido de fabrica sin ninguna llave: se ve y suena como XP, sin red, el vigilante esperando)"
  # (las capturas -el disco, la pared con la barra de letras y el buscador con «inventad»- se comparan a ojo: el cielo se mueve y dos
  # corridas nunca dan los mismos pixeles; aqui solo se dice cuantos cambian)
  if [ ! -f "$VIGILANTE" ]; then echo "(sin $VIGILANTE: no se corre)"; else
  FT="7:67,9:13,10.5:13,13:27,20:27+38,$(escribe 21 inventad),27:27"
  FC="12:$W/fabrica-CUAL-disco.png,18:$W/fabrica-CUAL-pared.png,26.5:$W/fabrica-CUAL-buscador.png"
  mkdir -p "$W/sin-block" "$W/sin-disco" "$W/sin-discos"
  for v in xp os; do
    d=$(datos fabrica-$v); T0=$(date +%s.%N); o="$W/fabrica-$v"; mkdir -p "$o"
    ST=(strace -D -f -ttt --seccomp-bpf -qq -e trace=connect,execve -o "$o/strace.txt")
    # (lo que gasta el vigilante ya esperando, de los 18 a los 38 s: su /proc/PID/stat -con lo de sus hijos ya acabados- y la hora, las
    # dos veces; y a los 38 s, los procesos que cuelgan de la rocola y del vigilante)
    ( mira() { vp=$(cat "$o/run/rocola-soloist/vigilante.pid" 2>/dev/null); [ -n "$vp" ] || return
        echo "$vp $(date +%s.%N) $(cut -d' ' -f1 /proc/uptime) $(sed 's/^.*) //' "/proc/$vp/stat" | awk '{print $12+$13+$14+$15}')" >> "$o/vigilante-cpu.txt"; }
      sleep 18; mira; sleep 20; mira; vp=$(cat "$o/run/rocola-soloist/vigilante.pid" 2>/dev/null); rp=; [ -n "$vp" ] && rp=$(awk '{print $4}' "/proc/$vp/stat" 2>/dev/null)
      [ -n "$rp" ] && ps -o pid=,ppid=,comm= --ppid "$rp" > "$o/hijos.txt" 2>/dev/null
      [ -n "$vp" ] && ps -o pid=,ppid=,comm= --ppid "$vp" >> "$o/hijos.txt" 2>/dev/null ) & sp=$!
    if [ $v = xp ]; then abre fabrica-$v "$d" "$FT" "38.5:copia" 40 TERM env -u ROCOLA_OS ROCOLA_CAPTURA="${FC//CUAL/$v}" "${ST[@]}"
    else abre fabrica-$v "$d" "$FT" "38.5:copia" 40 TERM ROCOLA_OS=1 ROCOLA_CAPTURA="${FC//CUAL/$v}" ROCOLA_SOLOIST_BIN="$VIGILANTE" \
      ROCOLA_XP_DATOS="$d" ROCOLA_DISCO="$W/sin-disco" ROCOLA_DISCOS="$W/sin-discos" ROCOLA_SYS_BLOCK="$W/sin-block" \
      ROCOLA_SPOTIFY_API="http://127.0.0.1:9/v1" ROCOLA_SPOTIFY_CUENTAS="http://127.0.0.1:9" ROCOLA_CLIMA_API="http://127.0.0.1:7" \
      ROCOLA_LUGAR_API="http://127.0.0.1:7/lugar" ROCOLA_LETRA_API="http://127.0.0.1:7" ROCOLA_GENIUS_API="http://127.0.0.1:7" "${ST[@]}"; fi
    wait $sp 2>/dev/null
  done
  a="$W/fabrica-xp"; o="$W/fabrica-os"; d="$W/datos-fabrica-os"; b="$d/rocola_spotify.txt"
  # (junta 3: el clima del cielo y la letra de LRCLIB salen a internet sin llave -decisiones del dueno del 20-11-2026, cielo-nucleo y
  # letras-; aqui van al sumidero 127.0.0.1:7, donde nada escucha, y no cuentan como peticion a la red: se cuentan aparte, sumidero())
  red() { grep -E 'connect\(.*sa_family=AF_INET6?' "$1/strace.txt" 2>/dev/null | grep -vFc 'htons(7), sin_addr=inet_addr("127.0.0.1")'; }   # solo el sumidero 127.0.0.1:7 se descuenta (strace escribe el puerto y despues la direccion)
  sumidero() { grep -E 'connect\(.*sa_family=AF_INET6?' "$1/strace.txt" 2>/dev/null | grep -Fc 'htons(7), sin_addr=inet_addr("127.0.0.1")'; }
  progs() { grep -c ' execve(' "$1/strace.txt" 2>/dev/null; }
  # los programas abiertos entre las dos miradas (de los 18 a los 38 s: el vigilante ya esperando); solo los que de verdad se abrieron
  # (no los intentos por cada carpeta del PATH que no lo tiene, ENOENT) y sin las envolturas que la sesion de pruebas pone delante en el
  # PATH (/root/.claude/...: un «rm» que vuelve a abrir /usr/bin/rm), que no son del vigilante
  progs2() { local t1 t2; t1=$(sed -n 1p "$1/vigilante-cpu.txt" 2>/dev/null | cut -d' ' -f2); t2=$(sed -n 2p "$1/vigilante-cpu.txt" 2>/dev/null | cut -d' ' -f2)
    [ -n "$t1" ] && [ -n "$t2" ] && awk -v a="$t1" -v b="$t2" '$2+0 >= a && $2+0 <= b && / execve\(/ && !/ENOENT/ && !/execve\("\/root\/\.claude\//' "$1/strace.txt" | grep -c . || echo -1; }
  si "fabrica: las dos salen con 0" test "$(cat $a/salio)$(cat $o/salio)" = 00
  si "fabrica: con ROCOLA_OS=1 y sin rocola_os.txt, el modo encendido (de fabrica)" bash -c "grep -F 'encendidas:' '$d/rocola_os_bitacora.txt' | grep -qw spotify"
  si "fabrica: ...y sin llaves la bitacora del catalogo lo dice (SIN llave)" contiene "$d/rocola_os_bitacora.txt" "SIN llave"
  si "fabrica: rocola_os.txt no se escribio (nada cambio)" test ! -e "$d/rocola_os.txt"
  python3 "$XP/Pruebas/Ventanas/guiones/compara_trazas.py" "$a/traza.txt" "$o/traza.txt" --ignora RocolaOs: --holgura 80 > "$o/compara.txt" 2>&1; r=$?
  sed 's/^/   /' "$o/compara.txt" | head -8
  # (como nivel0.sh con REPITE=1: si solo difieren las CUENTAS de relojes o encargos -las de los hilos cambian solas de una corrida a otra
  # con la carga de la maquina-, la de sin ROCOLA_OS se corre otra vez, en seguida, y vale si una de las dos sale igual)
  if [ $r != 0 ] && ! grep '^DIFERENTE' "$o/compara.txt" | grep -qvE '^DIFERENTE (reloj|encargo) .*(en total|frente a)'; then
    echo "   (solo cuentas: la de sin ROCOLA_OS otra vez)"
    d2=$(datos fabrica-xp2); mkdir -p "$W/fabrica-xp2"; T0=$(date +%s.%N)
    abre fabrica-xp2 "$d2" "$FT" "" 40 TERM env -u ROCOLA_OS strace -D -f -ttt --seccomp-bpf -qq -e trace=connect,execve -o "$W/fabrica-xp2/strace.txt"
    python3 "$XP/Pruebas/Ventanas/guiones/compara_trazas.py" "$W/fabrica-xp2/traza.txt" "$o/traza.txt" --ignora RocolaOs: --holgura 80 > "$o/compara2.txt" 2>&1; r=$?
    sed 's/^/   /' "$o/compara2.txt" | head -8
  fi
  si "fabrica: la traza IGUAL a la de sin ROCOLA_OS quitando lo de RocolaOs (los mismos avisos, teclas, relojes y cierre)" test $r = 0
  sa=$(awk '$3>0 {s+=$2-$1} END {printf "%.2f", s}' "$a/frec.txt"); so=$(awk '$3>0 {s+=$2-$1} END {printf "%.2f", s}' "$o/frec.txt")
  ta=$(awk '$3>0 && $2-$1>=0.3 {print $3}' "$a/frec.txt" | tr '\n' ' '); to=$(awk '$3>0 && $2-$1>=0.3 {print $3}' "$o/frec.txt" | tr '\n' ' ')
  echo "   sonado: sin ROCOLA_OS $sa s ($ta); con ROCOLA_OS=1 $so s ($to)"
  si "fabrica: suena lo mismo que XP (los mismos tonos, en el mismo orden, y lo mismo de tiempo a 0,3 s: $sa s y $so s)" bash -c "[ '$ta' = '$to' ] && [ -n '$ta' ] && echo '$sa - $so < 0.3 && $so - $sa < 0.3' | bc | grep -q 1"
  si "fabrica: nada cobrado ni pedido de Spotify (cobradas=0, esperando_credito=0, ni un COBRADA)" bash -c "grep -qx 'cobradas=0' '$o/estado.txt' && grep -qx 'esperando_credito=0' '$o/estado.txt' && ! grep -q COBRADA '$b' 2>/dev/null"
  si "fabrica: sin sesion (Listo falso: ni la letra de la cuenta ni nada que pedir)" bash -c "grep -qx 'sesion=0' '$o/estado.txt'"
  ra=$(red "$a"); ro=$(red "$o")
  si "fabrica: ni una peticion a la red (connect a internet: sin ROCOLA_OS $ra, con ROCOLA_OS=1 $ro)" test "$ra/$ro" = "0/0"
  echo "   (lo de sin llave del cielo y la letra, al sumidero 127.0.0.1:7: sin ROCOLA_OS $(sumidero "$a"), con ROCOLA_OS=1 $(sumidero "$o"))"
  si "fabrica: ni una a la API (ni a la de prueba de 127.0.0.1:9)" bash -c "! grep -q 'htons(9)' '$o/strace.txt'"
  pa=$(progs "$a"); po=$(progs "$o")
  hz=$(getconf CLK_TCK); pe=$(progs2 "$o")
  cpu=$(awk -v hz="$hz" 'NR == 1 {u=$3; c=$4} NR == 2 {if ($3 - u > 0) printf "%.2f %.1f", 100 * ($4 - c) / hz / ($3 - u), $3 - u} END {if (NR < 2) print "-1 0"}' "$o/vigilante-cpu.txt" 2>/dev/null)
  [ -n "$cpu" ] || cpu="-1 0"
  echo "   el vigilante esperando la llave: CPU ${cpu%% *} % de un nucleo (con sus hijos) en $(echo $cpu | cut -d' ' -f2) s, y $pe programas abiertos en ese rato;"
  echo "   en toda la escena (40 s, con el arranque del vigilante: mira una vez todos los procesos por si quedo un Soloist): sin ROCOLA_OS $pa, con ROCOLA_OS=1 $po"
  echo "   hijos a los 38 s:"; sed 's/^/     /' "$o/hijos.txt" 2>/dev/null
  si "fabrica: el vigilante esperando la llave (fase=sin_llave) y rocola-xp lo sabe (vigilante=sin_llave)" bash -c "grep -qx 'fase=sin_llave' '$o/vigilante-estado.txt' && grep -qx 'vigilante=sin_llave' '$o/estado.txt'"
  si "fabrica: sin llave no se abre ningun Soloist ni se baja nada (ni soloist ni curl en strace)" bash -c "! grep -E ' execve\(\"[^\"]*(soloist|curl|wget)' '$o/strace.txt' | grep -v 'rocola-soloist' | grep -q ."
  si "fabrica: de la rocola cuelga un solo proceso (el vigilante) y del vigilante solo su sleep" bash -c "[ \$(awk '{print \$3}' '$o/hijos.txt' | grep -cx rocola-soloist) = 1 ] && ! awk '{print \$3}' '$o/hijos.txt' | grep -vxE 'rocola-soloist|sleep' | grep -q ."
  si "fabrica: el vigilante, esperando, gasta menos del 0,5 % de un nucleo (${cpu%% *} %) y abre como mucho un programa por segundo ($pe en 20 s)" bash -c "echo '${cpu%% *} >= 0 && ${cpu%% *} < 0.5 && $pe >= 0 && $pe <= 20' | bc | grep -q 1"
  vp=$(awk '$3 == "rocola-soloist" {print $1; exit}' "$o/hijos.txt" 2>/dev/null)
  si "fabrica: y el vigilante se fue con la rocola (se cierra con la senal; pid ${vp:-?} ya no esta)" bash -c "[ -n '$vp' ] && ! kill -0 '$vp' 2>/dev/null && grep -q 'Vigilante: se cierra (senal' '$d/rocola_soloist.txt'"
  # (sin los avisos «Pantalla parada ... ms» de XP y su renglon «(sin excepcion)»: solo dependen de la carga de la maquina)
  sinparadas() { sed 's/^[0-9-]* [0-9:.]* *//' "$1" 2>/dev/null | awk '/^\[Pantalla parada /{s=3; next} s > 0 {s--; if (/^\(sin excepcion\)/) s=0; next} !/^\r?$/' | wc -l; }
  ea=$(sinparadas "$W/datos-fabrica-xp/rocola_error.txt"); eo=$(sinparadas "$d/rocola_error.txt")
  si "fabrica: rocola_error.txt igual de largo que sin ROCOLA_OS sin contar las «Pantalla parada» ($ea y $eo renglones: ningun aviso raro)" test "$ea" = "$eo"
  if python3 -c "import PIL" 2>/dev/null; then
    for c in disco pared buscador; do
      dif=$(python3 - "$W/fabrica-xp-$c.png" "$W/fabrica-os-$c.png" <<'EOP'
import sys
from PIL import Image, ImageChops
a, b = Image.open(sys.argv[1]).convert('RGB'), Image.open(sys.argv[2]).convert('RGB')
d = ImageChops.difference(a, b).convert('L').point(lambda v: 255 if v > 24 else 0)
bb = d.getbbox(); n = sum(1 for v in d.tobytes() if v)
print(n, bb if bb else '-')
EOP
)
      echo "   captura $c: $dif (pixeles que cambian mas de 24 de 255 frente a sin ROCOLA_OS, y donde)"
    done
  fi
  fi
fi

# ---------------------------------------------------------------- dos: dos de Spotify seguidas, de principio a fin
if [[ " $partes " == *" dos "* ]]; then
  echo "== dos (pedir, sonar entera, la siguiente, el autoplay pausado, el cierre)"
  d=$(datos dos); T0=$(date +%s.%N)
  abre dos "$d" "7:67" "9:pide spotify:track:f440d008aa|$P|8000;9.2:pide spotify:track:f550d006bb|$P|6000;31:copia" 32 TERM ROCOLA_OS=spotify,spotify-prueba
  o="$W/dos"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -20
  si "dos: sale con 0" test "$(cat $o/salio)" = 0
  si "dos: el vigilante con SPOTIFY_NOMBRE=Rocola y el anillo de ROCOLA_RUN (I4)" bash -c "grep -qF 'SPOTIFY_NOMBRE=Rocola ROCOLA_SPOTIFY_ANILLO=$o/run/rocola-xp/spotify/sonido.anillo' '$o/vigilante.txt'"
  si "dos: el anillo con su tamano de I1 (4096 + 65536*8 = 528384)" test "$(wc -c < "$o/run/rocola-xp/spotify/sonido.anillo")" = 528384
  si "dos: cobra una y una (4 -> 3 -> 2) y entran" bash -c "grep -qF 'pide spotify:track:f440d008aa listo=1 entro=1 creditos 4->3' '$(log $o)' && grep -qF 'pide spotify:track:f550d006bb listo=1 entro=1 creditos 3->2' '$(log $o)'"
  si "dos: R2/R3 antes del primer play: activate, shuffle y los dos repeat apagados" python3 - "$o/falso.txt" <<'EOP'
import sys
L=[l.split('\t',2)[2].strip() for l in open(sys.argv[1]) if l.split('\t')[1]=='orden']
i=next(k for k,x in enumerate(L) if '"play"' in x)
a=L[:i]
ok=any('"activate"' in x for x in a) and any('"set_shuffle","enabled":false' in x for x in a) and any('"set_repeat_track","enabled":false' in x for x in a) and any('"set_repeat_context","enabled":false' in x for x in a)
sys.exit(0 if ok else 1)
EOP
  si "dos: play con la uri de cada una, en su orden" python3 -c "
import sys,re
L=[l for l in open('$o/falso.txt') if '\"play\"' in l]
u=[re.search(r'spotify:track:\w+',l).group(0) for l in L if 'spotify:track' in l]
sys.exit(0 if u[:2]==['spotify:track:f440d008aa','spotify:track:f550d006bb'] else 1)"
  t440=$(oye "$f" 440); t550=$(oye "$f" 550); t990=$(oye "$f" 990)
  si "dos: la primera se oye entera (8 s: $t440 s)" entre "$t440" 7.7 8.3
  si "dos: la segunda se oye entera (6 s: $t550 s)" entre "$t550" 5.7 6.3
  si "dos: en su orden (440 antes que 550)" entre "$(primero "$f" 550)" "$(ultimo "$f" 440)" 1000
  hueco=$(echo "$(primero "$f" 550) - $(ultimo "$f" 440)" | bc)
  si "dos: el hueco entre las dos, menos de 3 s ($hueco s)" entre "$hueco" 0 3
  si "dos: el autoplay del final (990 Hz) no se oye ($t990 s)" entre "$t990" 0 0.15
  si "dos: ni un hueco dentro de cada una (revision: el arranque sin hueco; 440: $(huecos "$f" 440), 550: $(huecos "$f" 550))" test "$(huecos "$f" 440)$(huecos "$f" 550)" = 00
  si "dos: la onda de cada una sin saltos (revision: ni un cuadro perdido ni repetido)" bash -c "[ '$(saltos "$f" "$o/sonado.raw" 440 "$(hzde $o)")' = 0 ] && [ '$(saltos "$f" "$o/sonado.raw" 550 "$(hzde $o)")' = 0 ]"
  si "dos: tras cada una, pause (Soloist no sigue solo)" bash -c "[ \$(grep -c '\"pause\"' '$o/falso.txt') -ge 2 ]"
  si "dos: la bitacora: 2 cobradas y 2 acabadas de verdad, sin devolver" bash -c "[ \$(grep -c 'COBRADA' '$b') = 2 ] && [ \$(grep -c ': termino, sono' '$b') = 2 ] && ! grep -q DEVUELTA '$b'"
  si "dos: el estado: ws=1 sesion=1 cobradas=2 devueltas=0" bash -c "[ '$(estado $o ws)$(estado $o sesion)' = 11 ] && [ '$(estado $o cobradas)' = 2 ] && [ '$(estado $o devueltas)' = 0 ]"
  si "dos: el cierre de lo de Spotify cabe (bitacora «se cierra»)" contiene "$b" "se cierra (WindowsShutDown)"
  ms=$(sed -n 's/.*se cierra (WindowsShutDown).* en total \([0-9]*\) ms.*/\1/p' "$b" | tail -1)
  si "dos: y tardo menos de 4000 ms (${ms:-?} ms; SIGTERM -> salir $(cat $o/tardo) s)" entre "${ms:-99999}" 0 4000
  si "dos: el vigilante recibio SIGTERM" contiene "$o/vigilante.txt" "SIGTERM"
  si "dos: el Soloist falso salio y borro ws.port" test ! -e "$o/soloist/ws.port"
  si "dos: rocola_stats.txt y rocola_noche.txt sin «spotify:» (R6)" bash -c "! grep -qs 'spotify:' '$d/rocola_stats.txt' '$d/rocola_noche.txt'"
  si "dos: ninguna tabla de lo aprendido con «spotify:» (R7; fuera de las bitacoras)" bash -c "! grep -rlsF 'spotify:' '$d' --include='rocola_*.txt' | grep -vE 'rocola_spotify.txt|rocola_os_bitacora.txt|rocola_error.txt|rocola_estado.txt' | grep -q ."
fi

# ---------------------------------------------------------------- adelanto (rapido-spotify): el play adelantado entre dos de Spotify
# El Soloist falso tarda 1 s en empezar cada una (SOLOIST_FALSO_LENTO): sin el play adelantado, ese segundo se sumaba al hueco entre las dos
# (la de antes acababa de sonar en la tarjeta y solo entonces se mandaba el play de la siguiente). Con el, el play de la 2a se manda al
# acabar de llegar la 1a: el hueco es el de siempre (el arranque sin hueco de la fuente). Todo lo demas, como en «dos».
if [[ " $partes " == *" adelanto "* ]]; then
  echo "== adelanto (el play de la siguiente de Spotify, adelantado; Soloist tarda 1 s en empezar)"
  d=$(datos adelanto); T0=$(date +%s.%N)
  abre adelanto "$d" "7:67" "9:pide spotify:track:f440d008aa|$P|8000;9.2:pide spotify:track:f550d006bb|$P|6000;31:copia" 32 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_LENTO=1
  o="$W/adelanto"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -12
  si "adelanto: sale con 0" test "$(cat $o/salio)" = 0
  si "adelanto: la bitacora dice el play adelantado de la 2a al acabar la 1a" contiene "$b" "play adelantado spotify:track:f550d006bb (Prueba): la siguiente de la cola, al acabar spotify:track:f440d008aa"
  si "adelanto: y nada de un adelanto que no se uso" nocontiene "$b" "no se uso"
  si "adelanto: un solo play de la 2a (la fuente la tomo: no se pidio otra vez)" test "$(grep '"play"' "$o/falso.txt" | grep -c f550d006bb)" = 1
  t440=$(oye "$f" 440); t550=$(oye "$f" 550); t990=$(oye "$f" 990)
  si "adelanto: la primera entera (8 s: $t440 s)" entre "$t440" 7.7 8.3
  si "adelanto: la segunda entera (6 s: $t550 s)" entre "$t550" 5.7 6.3
  hueco=$(echo "$(primero "$f" 550) - $(ultimo "$f" 440)" | bc)
  si "adelanto: el hueco entre las dos sin el segundo de Soloist (menos de 1,7 s: $hueco s; sin adelantar, unos 2,3)" entre "$hueco" 0 1.7
  si "adelanto: el autoplay del final (990 Hz) no se oye ($t990 s)" entre "$t990" 0 0.15
  si "adelanto: ni un hueco dentro de cada una (440: $(huecos "$f" 440), 550: $(huecos "$f" 550))" test "$(huecos "$f" 440)$(huecos "$f" 550)" = 00
  si "adelanto: la bitacora: 2 cobradas y 2 acabadas de verdad, sin devolver" bash -c "[ \$(grep -c 'COBRADA' '$b') = 2 ] && [ \$(grep -c ': termino, sono' '$b') = 2 ] && ! grep -q DEVUELTA '$b'"
  si "adelanto: el estado: cobradas=2 devueltas=0" bash -c "[ '$(estado $o cobradas)$(estado $o devueltas)' = 20 ]"
fi
# ---------------------------------------------------------------- adelantoxp (rapido-spotify): de una de XP a una de Spotify
if [[ " $partes " == *" adelantoxp "* ]]; then
  echo "== adelantoxp (una de XP y detras una de Spotify: su play, adelantado al final de la de XP; Soloist tarda 1 s)"
  d=$(datos adelantoxp); T0=$(date +%s.%N)
  abre adelantoxp "$d" "7:67,9.5:13,11:13" "11.6:pide spotify:track:f440d006aa|$P|6000;25:copia" 26 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_LENTO=1
  o="$W/adelantoxp"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -12
  si "adelantoxp: sale con 0" test "$(cat $o/salio)" = 0
  si "adelantoxp: la bitacora dice el play adelantado al acabar la de XP" contiene "$b" "play adelantado spotify:track:f440d006aa (Prueba): la siguiente de la cola, al acabar la de XP que suena"
  si "adelantoxp: y nada de un adelanto que no se uso" nocontiene "$b" "no se uso"
  si "adelantoxp: un solo play (la fuente la tomo)" test "$(grep '"play"' "$o/falso.txt" | grep -c f440d006aa)" = 1
  t440=$(oye "$f" 440)
  si "adelantoxp: la de Spotify entera (6 s: $t440 s)" entre "$t440" 5.7 6.3
  si "adelantoxp: ni un hueco dentro (440: $(huecos "$f" 440))" test "$(huecos "$f" 440)" = 0
  # el hueco: de lo ultimo de XP (otro tono) a lo primero de 440
  xp=$(awk -v p="$(primero "$f" 440)" '$1 < p && $3 != 0 && ($3 < 425 || $3 > 455) {u=$2} END {print u+0}' "$f")
  hueco=$(echo "$(primero "$f" 440) - $xp" | bc)
  si "adelantoxp: el hueco de la de XP a la de Spotify sin el segundo de Soloist (menos de 1,9 s: $hueco s)" entre "$hueco" 0 1.9
  si "adelantoxp: el adelanto sigue encendido (ni la salvaguarda ni su archivo)" bash -c "! grep -q 'SE APAGA' '$b' && [ ! -f '$d/rocola_spotify_sin_adelanto.txt' ]"
  si "adelantoxp: 1 cobrada y 1 acabada de verdad, sin devolver" bash -c "[ \$(grep -c 'COBRADA' '$b') = 1 ] && [ \$(grep -c ': termino, sono' '$b') = 1 ] && ! grep -q DEVUELTA '$b'"
fi

# ---------------------------------------------------------------- adelantoreloj (revision): la salvaguarda del play adelantado
# Un Soloist que SIN LECTOR sigue avanzando por su reloj (SOLOIST_FALSO_RELOJ=1; el falso de siempre no avanza): la de Spotify adelantada desde
# una de XP pierde su principio. La rocola lo ve (sono menos que su duracion), apaga el adelanto para siempre y la siguiente suena entera, con
# su play al abrir la fuente (como antes de rapido-spotify)
if [[ " $partes " == *" adelantoreloj "* ]]; then
  echo "== adelantoreloj (Soloist que avanza sin lector: la salvaguarda apaga el adelanto)"
  d=$(datos adelantoreloj); T0=$(date +%s.%N)
  abre adelantoreloj "$d" "7:67,9.5:13,11:13" "11.6:pide spotify:track:f440d006aa|$P|6000;11.9:pide spotify:track:f550d006bb|$P|6000;33:copia" 34 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_LENTO=0.2 SOLOIST_FALSO_RELOJ=1
  o="$W/adelantoreloj"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -12
  si "adelantoreloj: sale con 0" test "$(cat $o/salio)" = 0
  si "adelantoreloj: el falso avanzo sin lector" contiene "$o/falso.txt" "sin lector: avanza por el reloj"
  si "adelantoreloj: la 1a de Spotify, adelantada" contiene "$b" "play adelantado spotify:track:f440d006aa"
  si "adelantoreloj: la salvaguarda la vio corta y apago el adelanto" contiene "$b" "EL PLAY ADELANTADO SE APAGA para siempre (la adelantada spotify:track:f440d006aa"
  si "adelantoreloj: y lo guardo (rocola_spotify_sin_adelanto.txt)" test -f "$d/rocola_spotify_sin_adelanto.txt"
  si "adelantoreloj: la 2a ya no se adelanto" nocontiene "$b" "play adelantado spotify:track:f550d006bb"
  t550=$(oye "$f" 550)
  si "adelantoreloj: la 2a entera (6 s: $t550 s)" entre "$t550" 5.7 6.3
  si "adelantoreloj: 2 cobradas, ninguna devuelta" bash -c "[ \$(grep -c 'COBRADA' '$b') = 2 ] && ! grep -q DEVUELTA '$b'"
  # y al volver a abrir con el mismo archivo, ni se intenta
  T0=$(date +%s.%N)
  abre adelantoreloj2 "$d" "7:67,9.5:13,11:13" "11.6:pide spotify:track:f440d006aa|$P|6000;24:copia" 25 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_LENTO=0.2 SOLOIST_FALSO_RELOJ=1
  o="$W/adelantoreloj2"; f="$o/frec.txt"
  t440=$(oye "$f" 440)
  si "adelantoreloj: otra vez: sin adelanto (lo guardado manda) y entera (6 s: $t440 s)" bash -c "[ \$(grep -c 'play adelantado' '$b') = 1 ] && python3 -c 'import sys; sys.exit(0 if 5.7 <= float(sys.argv[1]) <= 6.3 else 1)' '$t440'"
fi

# ---------------------------------------------------------------- mezcla: con las de XP, el turno, sin creditos
if [[ " $partes " == *" mezcla "* ]]; then
  echo "== mezcla (las de XP y las de Spotify en la misma cola; sin creditos)"
  d=$(datos mezcla); T0=$(date +%s.%N)
  abre mezcla "$d" "7:67,9.5:13,11:13,12.6:40,13:13" "11.6:pide spotify:track:f440d006aa|$P|6000;13.5:pide spotify:track:f550d005bb|$P|5000;14:pide spotify:track:f660d005cc|$P|5000;14.2:busca spotify:track:f660d005cc|$P|5000" 36 USR1 ROCOLA_OS=spotify,spotify-prueba
  o="$W/mezcla"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -30
  sed 's/^/   /' "$(log $o)"
  si "mezcla: sale con 0" test "$(cat $o/salio)" = 0
  si "mezcla: la de Spotify detras de la de XP: turno 1 en la cola, 3 creditos -> 2" contiene "$(log $o)" "pide spotify:track:f440d006aa listo=1 entro=1 creditos 3->2 cola=1"
  si "mezcla: la segunda de Spotify detras de la segunda de XP: 1 -> 0, la 3.a de la cola" contiene "$(log $o)" "pide spotify:track:f550d005bb listo=1 entro=1 creditos 1->0"
  si "mezcla: sin creditos no entra ni se cobra (lo de XP), desde un disco y desde el buscador" bash -c "grep -qF 'pide spotify:track:f660d005cc listo=1 entro=0 creditos 0->0' '$(log $o)' && grep -qF 'busca spotify:track:f660d005cc listo=1 entro=0 creditos 0->0' '$(log $o)'"
  si "mezcla: la que no entro no suena (660 Hz)" entre "$(oye "$f" 660)" 0 0.1
  si "mezcla: las dos de Spotify enteras (6 s: $(oye "$f" 440); 5 s: $(oye "$f" 550))" bash -c "python3 -c 'import sys; sys.exit(0 if 5.7<=$(oye "$f" 440)<=6.3 and 4.7<=$(oye "$f" 550)<=5.3 else 1)'"
  # el orden: lo de XP (cualquier otro tono) antes de 440, otra vez XP entre 440 y 550
  si "mezcla: el orden XP, Spotify, XP, Spotify" python3 - "$f" <<'EOP'
import sys
s=[(float(a),float(b),int(c)) for a,b,c in (l.split() for l in open(sys.argv[1]))]
def cat(f): return 'S440' if abs(f-440)<=15 else 'S550' if abs(f-550)<=15 else ('-' if f==0 else 'XP')
o=[]
for a,b,f in s:
    if b-a<0.3: continue
    c=cat(f)
    if c=='-' : continue
    if not o or o[-1]!=c: o.append(c)
print('   orden:', o)
sys.exit(0 if o[:4]==['XP','S440','XP','S550'] else 1)
EOP
  si "mezcla: con SIGUSR1 (se conserva el estado) rocola_stats.txt cuenta lo de XP y nada de Spotify (R6)" bash -c "grep -qs 'Cancion' '$d/rocola_stats.txt' && ! grep -qs 'spotify:' '$d/rocola_stats.txt'"
fi

# ---------------------------------------------------------------- celular: lo no pedido no suena ni se cobra; inactiva; sin sesion
if [[ " $partes " == *" celular "* ]]; then
  echo "== celular (R3: lo que no pidio la rocola se pausa; R4: inactiva; I6: sin sesion no se pide)"
  d=$(datos celular); T0=$(date +%s.%N)
  abre celular "$d" "7:67" "11:pide spotify:track:f440d020aa|$P|20000;23:pide spotify:track:f550d020bb|$P|20000;34:pide spotify:track:f880d010cc|$P|10000;37:copia" 38 TERM \
    ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_GUION="9:celular:spotify:track:f660d030zz;17:celular:spotify:track:f770d030yy;20:cola:spotify:track:f770d030xx;29:inactiva;33:sinsesion"
  o="$W/celular"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -30
  sed 's/^/   /' "$(log $o)"
  si "celular: sale con 0" test "$(cat $o/salio)" = 0
  si "celular: lo que pone un celular sin pedirlo no suena (660 Hz: $(oye "$f" 660) s)" entre "$(oye "$f" 660)" 0 0.1
  si "celular: y se pauso (bitacora «NO PEDIDO»)" contiene "$b" "NO PEDIDO: Soloist tocaba «spotify:track:f660d030zz»"
  si "celular: un celular a media cancion de la rocola: no se oye (770 Hz: $(oye "$f" 770) s)" entre "$(oye "$f" 770)" 0 0.2
  si "celular: la cortada (sono unos 5 s, menos de 10) devuelve su credito" bash -c "grep -q 'DEVUELTA spotify:track:f440d020aa' '$b'"
  si "celular: lo de la cola de Spotify se apunta y no suena" contiene "$b" "la cola de Spotify trae"
  si "celular: deja de ser la bocina activa: acaba, devuelve y manda activate" bash -c "grep -q 'acaba spotify:track:f550d020bb: dejo de ser la bocina activa' '$b' && grep -q 'DEVUELTA spotify:track:f550d020bb' '$b' && [ \$(grep -c '\"activate\"' '$o/falso.txt') -ge 2 ]"
  si "celular: sin sesion, el pedido no entra ni cobra (listo=0)" contiene "$(log $o)" "pide spotify:track:f880d010cc listo=0 entro=0 creditos 4->4"
  si "celular: los creditos vuelven a 4 (4 -> 3 -> devuelto -> 3 -> devuelto)" bash -c "grep -qF 'pide spotify:track:f550d020bb listo=1 entro=1 creditos 4->3' '$(log $o)'"
  si "celular: el estado: devueltas=2, no_pedidas>=2, sesion=0" bash -c "[ '$(estado $o devueltas)' = 2 ] && [ '$(estado $o no_pedidas)' -ge 2 ] && [ '$(estado $o sesion)' = 0 ]"
fi

# ---------------------------------------------------------------- carrera: un celular pone algo mientras la pedida aun no empieza (revision)
# Soloist tarda 3 s en empezar la pedida (SOLOIST_FALSO_LENTO) y a 1 s del play un celular pone otra (660 Hz): no se tiene que oir nada de
# ella, la pedida se vuelve a pedir y suena ENTERA, sin devolver ni cobrar de mas.
if [[ " $partes " == *" carrera "* ]]; then
  echo "== carrera (R3: lo que pone un celular mientras la pedida espera no suena)"
  d=$(datos carrera); T0=$(date +%s.%N)
  abre carrera "$d" "7:67" "11:pide spotify:track:f440d008aa|$P|8000;28:copia" 29 TERM \
    ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_LENTO=3 SOLOIST_FALSO_CELULAR_TRAS_PLAY="1:spotify:track:f660d030zz"
  o="$W/carrera"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -20
  si "carrera: sale con 0" test "$(cat $o/salio)" = 0
  si "carrera: el celular puso la suya en la espera (el falso lo dice)" contiene "$o/falso.txt" "celular (tras el play) spotify:track:f660d030zz"
  si "carrera: lo del celular no se oye (660 Hz: $(oye "$f" 660) s)" entre "$(oye "$f" 660)" 0 0.1
  si "carrera: y se apunta como NO PEDIDO, con play otra vez a la pedida" contiene "$b" "NO PEDIDO: mientras se esperaba spotify:track:f440d008aa"
  si "carrera: la pedida suena entera (8 s: $(oye "$f" 440) s)" entre "$(oye "$f" 440)" 7.7 8.3
  si "carrera: una cobrada, ninguna devuelta" bash -c "[ '$(estado $o cobradas)' = 1 ] && [ '$(estado $o devueltas)' = 0 ] && ! grep -q DEVUELTA '$b'"
fi

# ---------------------------------------------------------------- fallos: no empieza, se corta el sonido, se cae Soloist
if [[ " $partes " == *" fallos "* ]]; then
  echo "== fallos (R4 y R9: el credito vuelve y la rocola sigue)"
  d=$(datos fallos); T0=$(date +%s.%N)
  abre fallos "$d" "7:67" "9:pide spotify:track:nx0001|$P|30000;27:pide spotify:track:f440d030aa|$P|30000;41:pide spotify:track:f550d030bb|$P|30000;58:pide spotify:track:f660d004cc|$P|4000;65:copia" 66 TERM \
    ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_GUION="31:corta;38:sigue;47:cae"
  o="$W/fallos"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -30
  sed 's/^/   /' "$(log $o)"
  si "fallos: sale con 0" test "$(cat $o/salio)" = 0
  si "fallos: la que nunca empieza: a los 15 s se devuelve" contiene "$b" "DEVUELTA spotify:track:nx0001 (Prueba): no empezo en 15 s"
  si "fallos: el anillo sin sonido 5 s: acaba y se devuelve (sono menos de 10 s)" bash -c "grep -q 'acaba spotify:track:f440d030aa: el anillo sin sonido mas de 5 s' '$b' && grep -q 'DEVUELTA spotify:track:f440d030aa' '$b'"
  si "fallos: Soloist se cae a media cancion: acaba y se devuelve" contiene "$b" "DEVUELTA spotify:track:f550d030bb"
  si "fallos: el vigilante lo vuelve a abrir y la rocola se reconecta" bash -c "grep -q 'soloist salio con 1' '$o/vigilante.txt' && [ \$(grep -c 'conectado con Soloist' '$b') -ge 2 ]"
  si "fallos: despues, una nueva suena entera (660 Hz, 4 s: $(oye "$f" 660) s)" entre "$(oye "$f" 660)" 3.7 4.3
  si "fallos: 3 devueltas y la ultima cobrada: quedan 3 creditos (estado: devueltas=$(estado $o devueltas))" bash -c "[ '$(estado $o devueltas)' = 3 ] && grep -qF 'pide spotify:track:f660d004cc listo=1 entro=1 creditos 4->3' '$(log $o)'"
fi

# ---------------------------------------------------------------- lento: la tarjeta de la ROCOLA tarda en arrancar (R4 no culpa a Soloist)
# (la prueba del sistema entero en QEMU, sistema/ACT_qemu_spotify.md: con la pantalla parada 6 s al arrancar la cancion, la rocola no leia
# el anillo, Soloist no podia escribir -rocola-pulso solo le deja ~250 ms por delante- y a los 5 s «el anillo sin sonido» devolvia la
# primera de Spotify, 3 de 3 veces). La tarjeta de prueba tarda 7 s en arrancar (RELOJ_ARRANQUE_LENTO_MS): la cancion no se devuelve y
# suena entera; y «fallos» sigue viendo el corte de verdad (Soloist deja de escribir con la rocola leyendo).
if [[ " $partes " == *" lento "* ]]; then
  echo "== lento (R4: la rocola tarda en leer; no es Soloist: no se devuelve)"
  d=$(datos lento); T0=$(date +%s.%N)
  abre lento "$d" "7:67" "9:pide spotify:track:f440d020aa|$P|20000;37:copia" 38 TERM ROCOLA_OS=spotify,spotify-prueba RELOJ_ARRANQUE_LENTO_MS=7000
  o="$W/lento"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -10; grep -E 'suena|acaba|acabo|DEVUELTA' "$b" | sed 's/^/   /'; grep -c ARRANCA-LENTO "$o/registro.txt" | sed 's/^/   arranques lentos de la tarjeta: /'
  si "lento: sale con 0" test "$(cat $o/salio)" = 0
  si "lento: la tarjeta tardo en arrancar (7 s)" contiene "$o/registro.txt" "ARRANCA-LENTO"
  si "lento: no se devuelve (la que no leia era la rocola, no Soloist)" nocontiene "$b" "DEVUELTA spotify:track:f440d020aa"
  si "lento: acaba al terminar (no por «el anillo sin sonido»)" contiene "$b" "acabo spotify:track:f440d020aa (Prueba): termino"
  si "lento: y suena entera (440 Hz, 20 s: $(oye "$f" 440) s)" entre "$(oye "$f" 440)" 19.4 20.6
fi

# ---------------------------------------------------------------- fuera: Soloist se cae y NO vuelve (sin llave, caducado) (revision)
# Con Soloist fuera mas de 10 s, lo de Spotify de la cola acaba al momento y devuelve su credito (R4, R9): no 15 s de silencio cada una.
if [[ " $partes " == *" fuera "* ]]; then
  echo "== fuera (R9: Soloist se cae y no vuelve; lo de la cola se devuelve al momento)"
  d=$(datos fuera); T0=$(date +%s.%N)
  abre fuera "$d" "7:67" "9:pide spotify:track:f440d030aa|$P|30000;9.2:pide spotify:track:f550d030bb|$P|30000;31:copia" 32 TERM \
    ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_GUION="13:cae" VIGILANTE_FALSO_UNA=1
  o="$W/fuera"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -20; grep -E 'acaba|DEVUELTA' "$b" | sed 's/^/   /'
  si "fuera: sale con 0" test "$(cat $o/salio)" = 0
  si "fuera: la que sonaba acaba (WebSocket caido mas de 10 s) y se devuelve" bash -c "grep -q 'acaba spotify:track:f440d030aa: el WebSocket se cayo mas de 10 s' '$b' && grep -q 'DEVUELTA spotify:track:f440d030aa' '$b'"
  si "fuera: la de la cola tambien se devuelve" contiene "$b" "DEVUELTA spotify:track:f550d030bb"
  dt=$(python3 - "$b" <<'EOP'
import sys, datetime
t={}
for l in open(sys.argv[1]):
    for u in ('f440d030aa','f550d030bb'):
        if 'DEVUELTA spotify:track:'+u in l: t[u]=datetime.datetime.strptime(l[:19],'%Y-%m-%d %H:%M:%S')
print((t['f550d030bb']-t['f440d030aa']).total_seconds() if len(t)==2 else 99)
EOP
)
  si "fuera: y sin esperar sus 15 s (${dt} s despues de la primera)" entre "$dt" 0 3
  si "fuera: el estado: devueltas=2, ws=0" bash -c "[ '$(estado $o devueltas)' = 2 ] && [ '$(estado $o ws)' = 0 ]"
fi

# ---------------------------------------------------------------- frecuencia: Soloist a 48 kHz y su flujo abierto con la primera (revision)
# La primera se abre con el anillo sin flujo (0 Hz): la fuente declara 44,1 kHz y convierte lo que llega a 48 kHz; la segunda ya declara
# 48 kHz (la ultima que tuvo el anillo) y la convierte XP. Las dos enteras, en su tono y sin saltos en la onda.
if [[ " $partes " == *" frecuencia "* ]]; then
  echo "== frecuencia (el anillo a 48 kHz, abierto tarde: la conversion de la fuente y la de XP)"
  d=$(datos frecuencia); T0=$(date +%s.%N)
  abre frecuencia "$d" "7:67" "9:pide spotify:track:f440d008aa|$P|8000;9.2:pide spotify:track:f550d006bb|$P|6000;31:copia" 32 TERM \
    ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_HZ=48000 SOLOIST_FALSO_FLUJO_TARDE=1
  o="$W/frecuencia"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -20
  si "frecuencia: el falso abrio su flujo a 48 kHz con la primera" bash -c "grep -q 'el flujo se conecta con la primera cancion' '$o/falso.txt' && grep -q 'flujo conectado a 48000 Hz' '$o/falso.txt'"
  si "frecuencia: la primera entera y en su tono (8 s: $(oye "$f" 440) s)" entre "$(oye "$f" 440)" 7.7 8.3
  si "frecuencia: la segunda entera y en su tono (6 s: $(oye "$f" 550) s)" entre "$(oye "$f" 550)" 5.7 6.3
  si "frecuencia: ni un hueco dentro de cada una (440: $(huecos "$f" 440), 550: $(huecos "$f" 550))" test "$(huecos "$f" 440)$(huecos "$f" 550)" = 00
  si "frecuencia: la primera sin saltos (convertida por la fuente)" test "$(saltos "$f" "$o/sonado.raw" 440 "$(hzde $o)")" = 0
  si "frecuencia: la segunda sin saltos" test "$(saltos "$f" "$o/sonado.raw" 550 "$(hzde $o)")" = 0
  si "frecuencia: 2 acabadas de verdad, sin devolver" bash -c "[ \$(grep -c ': termino, sono' '$b') = 2 ] && ! grep -q DEVUELTA '$b'"
fi

# ---------------------------------------------------------------- apagon: lo pagado de Spotify vuelve como credito (R5)
if [[ " $partes " == *" apagon "* ]]; then
  echo "== apagon (R5)"
  d=$(datos apagon); T0=$(date +%s.%N)
  abre apagon1 "$d" "7:67" "9:pide spotify:track:f440d030aa|$P|30000;9.2:pide spotify:track:f550d030bb|$P|30000" 14 KILL ROCOLA_OS=spotify,spotify-prueba
  cp "$d/rocola_estado.txt" "$W/apagon-estado.txt" 2>/dev/null
  si "apagon: el estado de XP guardado con la que suena y la de la cola, de Spotify, y 2 creditos" bash -c "tr -d '\\r' < '$W/apagon-estado.txt' | grep -qx 'creditos=2' && grep -qF 'sonando=S|spotify:track:f440d030aa' '$W/apagon-estado.txt' && grep -qF 'cola=S|spotify:track:f550d030bb' '$W/apagon-estado.txt'"
  sleep 3
  si "apagon: el vigilante y el Soloist falso se fueron solos al morir la rocola" bash -c "grep -q 'murio el padre' '$W/apagon1/vigilante.txt' && ! pgrep -f '[s]oloist-falso.dll.*apagon1/soloist' >/dev/null"
  T0=$(date +%s.%N)
  abre apagon2 "$d" "" "" 14 TERM ROCOLA_OS=spotify,spotify-prueba ROCOLA_ESTADO_EN="12:$W/apagon2-xp.txt"
  si "apagon: al volver, lo pagado de Spotify vuelve como credito (2 + 2 = 4)" contiene "$W/apagon2-xp.txt" "creditos=4"
  si "apagon: y no suena nada de Spotify" bash -c "python3 -c \"import sys; sys.exit(0 if $(oye "$W/apagon2/frec.txt" 440)+$(oye "$W/apagon2/frec.txt" 550) < 0.1 else 1)\""
fi

# ---------------------------------------------------------------- portada: I6 baja la portada a /run (nunca al disco) y lo que suena la lleva
if [[ " $partes " == *" portada "* ]]; then
  echo "== portada (I6: la portada a /run, una sola vez, solo JPEG y solo de Spotify; la pedida la lleva)"
  d=$(datos portada); o="$W/portada"; srv="$W/servidor"; mkdir -p "$srv"
  convert -size 64x64 xc:'#3060a0' "$srv/p.jpg" && convert -size 64x64 xc:'#a03060' "$srv/q.png" && cp "$srv/q.png" "$srv/q.jpg"
  pto=$(python3 -c "import socket; s=socket.socket(); s.bind(('127.0.0.1',0)); print(s.getsockname()[1]); s.close()")
  python3 -I -m http.server --bind 127.0.0.1 --directory "$srv" "$pto" > "$W/servidor.txt" 2>&1 & sp=$!
  U="http://127.0.0.1:$pto"; T0=$(date +%s.%N)
  abre portada "$d" "7:67" "9:portada spotify:track:f440d008aa|$P|8000|$U/p.jpg|spotify:album:disco0001;9.1:portada spotify:track:f550d006bb|$P|6000|$U/q.jpg|spotify:album:disco0002;9.2:portada spotify:track:f660d006cc|$P|6000|https://example.invalid/x.jpg|spotify:album:disco0003;9.3:portada spotify:track:f880d006dd|$P|6000|$U/p.jpg|spotify:album:../../fuera;11:portada spotify:track:f440d008aa|$P|8000|$U/p.jpg|spotify:album:disco0001;11.5:pide spotify:track:f440d008aa|$P|8000|$U/p.jpg|spotify:album:disco0001;12:portada spotify:track:f4400000ee|$P|8000|$U/p.jpg|spotify:album:disco0001;21:copia" 22 TERM ROCOLA_OS=spotify,spotify-prueba
  kill $sp 2>/dev/null; wait $sp 2>/dev/null
  f="$o/frec.txt"; b="$d/rocola_spotify.txt"; pp="$o/run/rocola-xp/spotify/portadas"
  sed 's/^/   /' "$(log $o)"; ls "$pp" 2>/dev/null | sed 's/^/   portadas: /'
  si "portada: sale con 0" test "$(cat $o/salio)" = 0
  si "portada: la primera vez se pide (null) y a los 2 s ya esta en /run con el nombre de su disco" bash -c "grep -qx 'portada spotify:track:f440d008aa (pedida)' '$(log $o)' && grep -qx 'portada spotify:track:f440d008aa $pp/disco0001.jpg' '$(log $o)'"
  si "portada: es la misma que dio el servidor (JPEG)" cmp -s "$srv/p.jpg" "$pp/disco0001.jpg"
  si "portada: otra cancion del mismo disco usa la misma, sin bajarla otra vez (2 peticiones de p.jpg: la de disco0001 y la del id raro)" bash -c "grep -qx 'portada spotify:track:f4400000ee $pp/disco0001.jpg' '$(log $o)' && [ \$(grep -c 'GET /p.jpg' '$W/servidor.txt') = 2 ]"
  si "portada: la pedida la lleva (bitacora «con portada»)" bash -c "grep -q 'COBRADA spotify:track:f440d008aa.*con portada' '$b'"
  si "portada: lo que no es JPEG no se guarda" bash -c "[ ! -e '$pp/disco0002.jpg' ] && grep -q 'no es un JPEG' '$b'"
  si "portada: de otro sitio que no es Spotify ni se pide" bash -c "[ ! -e '$pp/disco0003.jpg' ] && ! grep -q 'f660d006cc' '$W/servidor.txt'"
  si "portada: un disco con un id raro usa el de la cancion (nada fuera de la carpeta)" bash -c "[ ! -e '$o/run/rocola-xp/fuera.jpg' ] && [ ! -e '$pp/../../fuera.jpg' ] && ls '$pp' | grep -qx 'f880d006dd.jpg'"
  si "portada: nada de las portadas en los datos (el disco)" bash -c "! find '$d' -name 'disco000*' | grep -q . && ! find '$d' -type f -size -2k -exec cmp -s '$srv/p.jpg' {} \\; -print | grep -q ."
  si "portada: y la cancion suena entera (8 s: $(oye "$f" 440) s)" entre "$(oye "$f" 440)" 7.7 8.3
  echo "   (informativo) miniaturas nuevas de XP en los datos: $(find "$d/rocola_miniaturas" -newer "$d/rocola_biblioteca.txt" -type f 2>/dev/null | wc -l) (con el catalogo juntado, su g43 las lleva a /run)"
fi

# ---------------------------------------------------------------- pulso: lo mismo que «dos», con el sonido por NUESTRO libpulse.so.0
if [[ " $partes " == *" pulso "* ]]; then
  echo "== pulso (el Soloist falso escribe por el libpulse.so.0 de rocola-pulso, como lo haria Soloist)"
  if [ ! -f "$PULSO" ]; then echo "(sin $PULSO: no se corre; compila sistema/rocola/pulso con make)"; else
  d=$(datos pulso); T0=$(date +%s.%N)
  abre pulso "$d" "7:67" "9:pide spotify:track:f440d008aa|$P|8000;9.2:pide spotify:track:f550d006bb|$P|6000;31:copia" 32 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_PULSO="$PULSO"
  o="$W/pulso"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$f" | head -20
  si "pulso: el Soloist falso conecto por el libpulse.so.0" contiene "$o/falso.txt" "conectado por $PULSO"
  si "pulso: la primera entera (8 s: $(oye "$f" 440) s)" entre "$(oye "$f" 440)" 7.7 8.3
  si "pulso: la segunda entera (6 s: $(oye "$f" 550) s)" entre "$(oye "$f" 550)" 5.7 6.3
  si "pulso: ni un hueco dentro de cada una (440: $(huecos "$f" 440), 550: $(huecos "$f" 550))" test "$(huecos "$f" 440)$(huecos "$f" 550)" = 00
  si "pulso: en su orden" entre "$(primero "$f" 550)" "$(ultimo "$f" 440)" 1000
  si "pulso: el autoplay no se oye ($(oye "$f" 990) s)" entre "$(oye "$f" 990)" 0 0.15
  si "pulso: 2 acabadas de verdad, sin devolver" bash -c "[ \$(grep -c ': termino, sono' '$b') = 2 ] && ! grep -q DEVUELTA '$b'"
  # (revision) la carrera por rocola-pulso: la pedida espera sin consumir y salta los vaciados (el LEEME de rocola-pulso, paso 4)
  d=$(datos pulso-carrera); T0=$(date +%s.%N)
  abre pulso-carrera "$d" "7:67" "11:pide spotify:track:f440d008aa|$P|8000;28:copia" 29 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_PULSO="$PULSO" \
    SOLOIST_FALSO_LENTO=3 SOLOIST_FALSO_CELULAR_TRAS_PLAY="1:spotify:track:f660d030zz"
  o="$W/pulso-carrera"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  si "pulso-carrera: lo del celular no se oye (660 Hz: $(oye "$f" 660) s)" entre "$(oye "$f" 660)" 0 0.1
  si "pulso-carrera: la pedida suena entera (8 s: $(oye "$f" 440) s)" entre "$(oye "$f" 440)" 7.7 8.3
  si "pulso-carrera: una cobrada, ninguna devuelta" bash -c "[ '$(estado $o cobradas)' = 1 ] && ! grep -q DEVUELTA '$b'"
  fi
fi

# ---------------------------------------------------------------- vigilante: el rocola-soloist DE VERDAD entre rocola-xp y el Soloist falso
if [[ " $partes " == *" vigilante "* ]]; then
  echo "== vigilante (I4 de punta a punta: rocola-xp abre el rocola-soloist de verdad, que abre el Soloist falso con rocola-pulso)"
  if [ ! -f "$PULSO" ] || [ ! -f "$VIGILANTE" ]; then echo "(sin $PULSO o sin $VIGILANTE: no se corre)"; else
  d=$(datos vigilante); o="$W/vigilante"; K=llave-inventada-0123456789abcdef-de-prueba
  printf '%s\n' "$K" > "$d/rocola_spotify_llave.txt"; chmod 600 "$d/rocola_spotify_llave.txt"
  T0=$(date +%s.%N)
  abre vigilante "$d" "7:67" "12:pide spotify:track:f440d008aa|$P|8000;12.2:pide spotify:track:f550d006bb|$P|6000;33:copia" 34 TERM ROCOLA_OS=spotify,spotify-prueba \
    ROCOLA_SOLOIST_BIN="$VIGILANTE" SOLOIST_BIN="$G/soloist-envoltorio.sh" SOLOIST_FALSO_PULSO=1 ROCOLA_XP_DATOS="$d" ROCOLA_DISCO="$W/sin-disco" \
    ROCOLA_DISCOS="$W/sin-discos" ROCOLA_PULSO_DIR="$(dirname "$PULSO")" ROCOLA_SOLOIST_USB=3600
  sleep 1
  f="$o/frec.txt"; b="$d/rocola_spotify.txt"; v="$d/rocola_soloist.txt"
  sed 's/^/   /' "$f" | head -20; sed 's/^/   vigilante: /' "$o/vigilante-estado.txt" 2>/dev/null
  si "vigilante: sale con 0" test "$(cat $o/salio)" = 0
  si "vigilante: el de verdad estaba corriendo (fase=corriendo) y el estado de rocola-xp lo dice" bash -c "grep -qx 'fase=corriendo' '$o/vigilante-estado.txt' && grep -qx 'vigilante=corriendo' '$o/estado.txt'"
  si "vigilante: arranco el Soloist falso con la bocina Rocola y con rocola-pulso" bash -c "grep -q 'arranca: bocina «Rocola»' '$o/falso.txt' && grep -q 'conectado por $(dirname "$PULSO")/libpulse.so.0' '$o/falso.txt'"
  si "vigilante: la primera entera (8 s: $(oye "$f" 440) s)" entre "$(oye "$f" 440)" 7.7 8.3
  si "vigilante: la segunda entera (6 s: $(oye "$f" 550) s)" entre "$(oye "$f" 550)" 5.7 6.3
  si "vigilante: el autoplay no se oye ($(oye "$f" 990) s)" entre "$(oye "$f" 990)" 0 0.15
  si "vigilante: ni un hueco dentro de cada una (440: $(huecos "$f" 440), 550: $(huecos "$f" 550))" test "$(huecos "$f" 440)$(huecos "$f" 550)" = 00
  si "vigilante: 2 acabadas de verdad, sin devolver" bash -c "[ \$(grep -c ': termino, sono' '$b') = 2 ] && ! grep -q DEVUELTA '$b'"
  si "vigilante: la llave no sale en ninguna bitacora ni estado" bash -c "! grep -rqsF -- '$K' '$b' '$v' '$o/run' '$o/estado.txt' '$o/vigilante-estado.txt' '$o/salida.txt'"
  si "vigilante: al cerrar la rocola, el vigilante y el Soloist falso se van (en $(cat $o/tardo) s)" bash -c "! pgrep -f '[s]potify-xp.*rocola-soloist corre' >/dev/null && ! pgrep -f '[s]oloist-falso.dll.*vigilante/soloist' >/dev/null"
  fi
fi

# ---------------------------------------------------------------- noche: el modo noche de XP baja tambien lo de Spotify, igual que una de XP
# (pregunta del dueno del 15-11-2026; pendiente 5 de SPOTIFY_JUNTA_PENDIENTES.md). Seis corridas, todas con el modo noche de fabrica (noche=1,
# noche_pct=80: -3,22 dB, 10*log2(0,8)): una de Spotify (la fuente de RocolaOs.SpotifyFuente.cs por ArmaCadena, 440 Hz) y una de XP (un MP3
# de 1000 Hz y 80 s en lugar de los de los datos de prueba), (1) cruzando las 10 de la noche a media cancion (una zona horaria de mentira
# hecha con zic, en la que la rocola arranca a las 21:59:30), (2) prendida ya de noche (las 23:30) y (3) con «Probar» (el boton de Ctrl+F3 ->
# Sonido, por el doble del catalogo: «noche-probar») de dia (la zona a las 12:00), tocado otra vez a los 15 s. Lo que sono, en la tarjeta de
# prueba con reloj; el nivel con niveles.py. Desde el 17-11-2026 (decision del dueno, la rocola de XP; AGENTS.md) la noche empieza a las 22 h
# y baja en UNA HORA, pareja en dB (-3,22 dB en 60 min: -0,0537 dB por minuto), sin escalon y sin aviso; prendida ya de noche, arranca en el
# nivel de la noche; «Probar» baja en 1,5 s (0,93 s hasta medio volumen) y se queda hasta otro toque (antes, 15 s y volvia sola).
# Desde el 20-11-2026 (el deslizador de 50 a 90 %, antes de 70 a 90) dos corridas mas con noche_pct=50 (-10 dB) en un rocola_config.txt de
# los datos: (4) prendida ya de noche y (5) «Probar», con su rampa (de 50 a 65 % la rampa da un paso mayor: sigue en 1,5 s; con el de antes,
# 2,1 s).
# Y desde el 20-11-2026 (lo de los vecinos, AUDIO_NOCHE_VECINOS_MODEST_2026-11-20.md: NV4 «Mas bajo de madrugada», de fabrica desde la 1 y al
# 70 %) una corrida mas: (6) cruzando la 1 de la manana a media cancion (la zona a las 00:59:30), ya con la noche entera (-3,22 dB) y desde la
# 1 la segunda bajada de una hora, pareja en dB, hasta el 70 % (-5,15 dB: -1,93 dB en 60 min, -0,0321 dB por minuto; los graves de noche de 8
# a 12 dB y el corte a 80 Hz casi no tocan los tonos de 440 y 1000 Hz: -0,04 dB), sin escalon. (Los graves de noche a 8 dB de fabrica bajan
# el tono de 440 Hz 0,05 dB, antes 0,02: dentro de lo que ya se admitia.)
if [[ " $partes " == *" noche "* ]]; then
  echo "== noche (el modo noche de XP con una de Spotify y una de XP: al cruzar las 22 h, prendida de noche y con «Probar», con sus rampas)"
  ZI="$W/zonas"; mkdir -p "$ZI"
  # zona NOMBRE HH:MM: una zona horaria en la que AHORA son las HH:MM (los segundos, los de UTC). Se llama justo antes de abrir la rocola.
  zona() {
    local n="$1" h="${2%:*}" m="${2#*:}" u off sg=""
    u=$(( 10#$(date -u +%H) * 60 + 10#$(date -u +%M) )); off=$(( (10#$h * 60 + 10#$m - u + 2880) % 1440 )); [ $off -gt 840 ] && off=$((off - 1440))
    [ $off -lt 0 ] && { sg=-; off=$((-off)); }
    echo "Zone $n $sg$((off / 60)):$(printf %02d $((off % 60))) - PRB" > "$ZI/$n.txt"; zic -d "$ZI" "$ZI/$n.txt" || return 1; echo "$ZI/$n"; }
  enelsegundo() { while [ "$(date -u +%S)" != "$1" ]; do sleep 0.2; done; }   # (para que las 22:00 caigan a los 30 s de abrir)
  [ -f "$W/tono1000.mp3" ] || ffmpeg -loglevel error -y -f lavfi -i "aevalsrc=0.25*sin(2*PI*1000*t)|0.25*sin(2*PI*1000*t):s=44100:d=80" -c:a libmp3lame -b:a 192k "$W/tono1000.mp3"
  for c in sp xp; do
    for m in noche yanoche probar yanoche50 probar50 madrugada; do
      d=$(datos noche-$m-$c)
      case $m in *50) printf 'noche_pct=50\r\n' > "$d/rocola_config.txt" ;; esac   # (20-11-2026) lo mas bajo del deslizador
      [ $c = xp ] && for f in "$d"/musica/*/*/*.mp3; do cp "$W/tono1000.mp3" "$f"; done
      cruce=""; largo=50000
      case $m in
        noche) enelsegundo 30; z=$(zona noche-$c 21:59); cruce=$(( ($(date +%s) / 60 + 1) * 60 )); pide=""; fin=72; largo=75000 ;;
        yanoche) z=$(zona ya-$c 23:30); pide=""; fin=40 ;;
        probar) z=$(zona dia-$c 12:00); pide="20:noche-probar;35:noche-probar"; fin=48 ;;
        yanoche50) z=$(zona ya50-$c 23:30); pide=""; fin=40 ;;
        probar50) z=$(zona dia50-$c 12:00); pide="20:noche-probar;35:noche-probar"; fin=48 ;;
        madrugada) enelsegundo 30; z=$(zona mad-$c 00:59); cruce=$(( ($(date +%s) / 60 + 1) * 60 )); pide=""; fin=72; largo=75000 ;;   # (20-11-2026, NV4)
      esac
      [ $c = sp ] && pide="9:pide spotify:track:f440d050aa|$P|$largo${pide:+;$pide}"
      printf 'noche_desde_min=0\n' >> "$d/rocola_os.txt"   # (22-11-2026: Rocola OS empieza a las 22:30 -g235-; estas escenas miden la noche de XP, desde las 22 h)
      T0=$(date +%s.%N)
      if [ $c = sp ]; then abre noche-$m-$c "$d" "7:67" "$pide" $fin TERM ROCOLA_OS=spotify,spotify-prueba TZ="$z"
      else abre noche-$m-$c "$d" "7:67,9:13,10.5:13" "$pide" $fin TERM ROCOLA_OS=spotify,spotify-prueba TZ="$z"; fi
      hz=440; [ $c = xp ] && hz=1000
      python3 "$G/niveles.py" "$W/noche-$m-$c/sonado.raw" "$W/noche-$m-$c/registro.txt" $hz --serie ${cruce:+--cruce $cruce} > "$W/noche-$m-$c/niveles.txt"
      echo "   $m $c: $(head -1 "$W/noche-$m-$c/niveles.txt")"
    done
  done
  nv() { head -1 "$W/noche-$1/niveles.txt" | tr ' ' '\n' | sed -n "s/^$2=//p"; }
  for c in sp xp; do
    o="$W/noche-noche-$c"
    si "noche $c: sale con 0" test "$(cat $o/salio)" = 0
    si "noche $c: el cruce de las 22 h cae con la cancion sonando ($(nv noche-$c cruce) s de lo sonado)" entre "$(nv noche-$c cruce)" 8 50
    si "noche $c: antes de las 22 h no baja ($(nv noche-$c pend_antes) dB/min)" entre "$(nv noche-$c pend_antes)" -0.012 0.012
    si "noche $c: desde las 22 h baja PAREJO, una hora para -3,22 dB (-0,0537 dB/min: $(nv noche-$c pend_despues) dB/min)" entre "$(nv noche-$c pend_despues)" -0.075 -0.035
    si "noche $c: sin escalon (el mayor cambio entre dos tramos de 100 ms: $(nv noche-$c salto) dB; la rampa de 20 s de antes daba unos 0,016)" entre "$(nv noche-$c salto)" 0 0.005
    o="$W/noche-yanoche-$c"
    si "yanoche $c: sale con 0" test "$(cat $o/salio)" = 0
    si "yanoche $c: prendida ya de noche, suena desde el principio al 80 % de lo de dia (-3,22 dB: $(nv yanoche-$c mediana) - $(nv probar-$c antes) dB)" \
      entre "$(echo "$(nv yanoche-$c mediana) - $(nv probar-$c antes)" | bc)" -3.40 -3.05
    si "yanoche $c: y quieta (no hay rampa que ver: $(nv yanoche-$c baja) dB)" entre "$(nv yanoche-$c baja)" -0.1 0.1
    o="$W/noche-probar-$c"
    si "probar $c: sale con 0" test "$(cat $o/salio)" = 0
    si "probar $c: «Probar» la baja al 80 % (-3,22 dB: $(nv probar-$c baja) dB)" entre "$(nv probar-$c baja)" -3.40 -3.05
    si "probar $c: con la rampa rapida (0,93 s: $(nv probar-$c rampa) s)" entre "$(nv probar-$c rampa)" 0.6 1.4
    # (20-11-2026) los dos toques llegan por prueba-pide, que el doble lee en su latido de 1 s (RocolaOs.LatidoMs): cada uno puede entrar
    # hasta 1 s tarde, asi que lo de abajo va de unos 14 a 16 s (con la maquina cargada se vio 15,9 dos veces y 13,9 una); antes 14,4-15,8
    si "probar $c: se queda hasta el otro toque, 15 s despues ($(nv probar-$c abajo) s abajo), y vuelve a lo de antes ($(nv probar-$c antes) -> $(nv probar-$c vuelve) dB, en $(nv probar-$c subida) s)" \
      bash -c "echo '$(nv probar-$c abajo) > 13.9 && $(nv probar-$c abajo) < 16.2 && $(nv probar-$c vuelve) - $(nv probar-$c antes) < 0.15 && $(nv probar-$c antes) - $(nv probar-$c vuelve) < 0.15 && $(nv probar-$c subida) > 0.5 && $(nv probar-$c subida) < 1.4' | bc | grep -q 1"
    # (20-11-2026) al 50 %, lo mas bajo del deslizador: -10 dB (10*log2(0,5)), de noche y con «Probar»
    o="$W/noche-yanoche50-$c"
    si "yanoche50 $c: sale con 0" test "$(cat $o/salio)" = 0
    si "yanoche50 $c: con noche_pct=50, prendida ya de noche suena desde el principio al 50 % de lo de dia (-10 dB: $(nv yanoche50-$c mediana) - $(nv probar50-$c antes) dB)" \
      entre "$(echo "$(nv yanoche50-$c mediana) - $(nv probar50-$c antes)" | bc)" -10.25 -9.75
    si "yanoche50 $c: y quieta ($(nv yanoche50-$c baja) dB)" entre "$(nv yanoche50-$c baja)" -0.1 0.1
    o="$W/noche-probar50-$c"
    si "probar50 $c: sale con 0" test "$(cat $o/salio)" = 0
    si "probar50 $c: «Probar» con noche_pct=50 la baja -10 dB ($(nv probar50-$c baja) dB)" entre "$(nv probar50-$c baja)" -10.25 -9.75
    si "probar50 $c: con la rampa rapida de 1,5 s (el paso mayor de 50 a 65 %; con el de antes, 2,1 s: $(nv probar50-$c rampa) s)" entre "$(nv probar50-$c rampa)" 1.2 1.85
    si "probar50 $c: se queda hasta el otro toque ($(nv probar50-$c abajo) s abajo) y vuelve a lo de antes ($(nv probar50-$c antes) -> $(nv probar50-$c vuelve) dB, en $(nv probar50-$c subida) s)" \
      bash -c "echo '$(nv probar50-$c abajo) > 13.9 && $(nv probar50-$c abajo) < 16.2 && $(nv probar50-$c vuelve) - $(nv probar50-$c antes) < 0.15 && $(nv probar50-$c antes) - $(nv probar50-$c vuelve) < 0.15 && $(nv probar50-$c subida) > 1.2 && $(nv probar50-$c subida) < 1.9' | bc | grep -q 1"
    # (20-11-2026) NV4: la madrugada, cruzando la 1
    o="$W/noche-madrugada-$c"
    si "madrugada $c: sale con 0" test "$(cat $o/salio)" = 0
    si "madrugada $c: el cruce de la 1 cae con la cancion sonando ($(nv madrugada-$c cruce) s de lo sonado)" entre "$(nv madrugada-$c cruce)" 8 50
    si "madrugada $c: antes de la 1 ya suena la noche entera (-3,22 dB: $(nv madrugada-$c mediana) - $(nv probar-$c antes) dB) y quieta ($(nv madrugada-$c pend_antes) dB/min)" \
      bash -c "echo '$(nv madrugada-$c mediana) - $(nv probar-$c antes) > -3.45 && $(nv madrugada-$c mediana) - $(nv probar-$c antes) < -3.05 && $(nv madrugada-$c pend_antes) > -0.012 && $(nv madrugada-$c pend_antes) < 0.012' | bc | grep -q 1"
    si "madrugada $c: desde la 1 baja PAREJO, una hora del 80 al 70 % (-1,93 dB: -0,0321 dB/min: $(nv madrugada-$c pend_despues) dB/min)" entre "$(nv madrugada-$c pend_despues)" -0.045 -0.020
    si "madrugada $c: sin escalon ($(nv madrugada-$c salto) dB entre dos tramos de 100 ms)" entre "$(nv madrugada-$c salto)" 0 0.005
  done
  dp=$(echo "$(nv madrugada-sp pend_despues) - $(nv madrugada-xp pend_despues)" | bc)
  si "madrugada: la de Spotify baja igual que la de XP (la pendiente: $dp dB/min de diferencia)" bash -c "echo '$dp < 0.012 && $dp > -0.012' | bc | grep -q 1"
  dp=$(echo "$(nv noche-sp pend_despues) - $(nv noche-xp pend_despues)" | bc)
  si "noche: la de Spotify baja igual que la de XP (la pendiente: $dp dB/min de diferencia)" bash -c "echo '$dp < 0.015 && $dp > -0.015' | bc | grep -q 1"
  db=$(echo "$(nv yanoche-sp mediana) - $(nv probar-sp antes) - $(nv yanoche-xp mediana) + $(nv probar-xp antes)" | bc)
  si "yanoche: la de Spotify baja igual que la de XP ($db dB de diferencia)" bash -c "echo '$db < 0.1 && $db > -0.1' | bc | grep -q 1"
  db=$(echo "$(nv probar-sp baja) - $(nv probar-xp baja)" | bc); dr=$(echo "$(nv probar-sp rampa) - $(nv probar-xp rampa)" | bc)
  si "probar: la de Spotify baja igual que la de XP (lo que baja: $db dB de diferencia; la rampa: $dr s)" bash -c "echo '$db < 0.1 && $db > -0.1 && $dr < 0.6 && $dr > -0.6' | bc | grep -q 1"
  db=$(echo "$(nv yanoche50-sp mediana) - $(nv probar50-sp antes) - $(nv yanoche50-xp mediana) + $(nv probar50-xp antes)" | bc)
  si "yanoche50: al 50 % la de Spotify baja igual que la de XP ($db dB de diferencia)" bash -c "echo '$db < 0.1 && $db > -0.1' | bc | grep -q 1"
  db=$(echo "$(nv probar50-sp baja) - $(nv probar50-xp baja)" | bc); dr=$(echo "$(nv probar50-sp rampa) - $(nv probar50-xp rampa)" | bc)
  si "probar50: al 50 % la de Spotify baja igual que la de XP (lo que baja: $db dB de diferencia; la rampa: $dr s)" bash -c "echo '$db < 0.1 && $db > -0.1 && $dr < 0.6 && $dr > -0.6' | bc | grep -q 1"
  si "noche sp: la de Spotify sono por Spotify (cobrada, por la fuente de RocolaOs.SpotifyFuente.cs)" bash -c "grep -q 'COBRADA spotify:track:f440d050aa' '$W/datos-noche-noche-sp/rocola_spotify.txt'"
fi

# ---------------------------------------------------------------- copiauri (22-11-2026): Soloist toca la «copia» de la pedida con OTRA uri
# (Spotify la enlaza a otra edicion): mismo titulo, artista y duracion -> se acepta como la pedida (antes: «NO PEDIDO», pausa y credito devuelto)
if [[ " $partes " == *" copiauri "* ]]; then
  echo "== copiauri (la copia con otra uri suena como la pedida)"
  d=$(datos copiauri); T0=$(date +%s.%N)
  abre copiauri "$d" "7:67" "9:pide spotify:track:f440d012ca|Cancion f440d012ca|Artista inventado|Disco inventado|12000;26:copia" 27 TERM ROCOLA_OS=spotify,spotify-prueba \
    SOLOIST_FALSO_COPIA="spotify:track:f440d012ca>spotify:track:f440d012cb"
  o="$W/copiauri"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  si "copiauri: sale con 0" test "$(cat $o/salio)" = 0
  si "copiauri: el Soloist falso toco la copia" contiene "$o/falso.txt" "toca la copia spotify:track:f440d012cb"
  si "copiauri: la rocola la acepto como la pedida (COPIA)" contiene "$b" "COPIA: Soloist toca «spotify:track:f440d012cb»"
  si "copiauri: la copia no se pauso como NO PEDIDO y nada se devolvio" bash -c "! grep -q 'NO PEDIDO.*f440d012cb' '$b' && ! grep -q DEVUELTA '$b'"
  t440=$(oye "$f" 440)
  si "copiauri: se oye entera (12 s: $t440 s)" entre "$t440" 11.5 12.4
  si "copiauri: el estado: cobradas=1 devueltas=0" bash -c "[ '$(estado $o cobradas)$(estado $o devueltas)' = 10 ]"
fi

# ---------------------------------------------------------------- nivela (22-11-2026, spotify_nivela): una de Spotify muy baja (-30 dBFS)
# la primera vez sube sola en sus primeros 30 s (hacia la sonoridad tipica de las medidas: 25 inventadas en rocola_spotify_niveles.txt) y se
# congela; al acabar entera se mide y se guarda; la segunda vez (otra corrida con esos datos) suena nivelada desde el primer segundo
if [[ " $partes " == *" nivela "* ]]; then
  echo "== nivela (primera vez: subida suave y congelada; se aprende; segunda vez: nivelada desde el principio)"
  d=$(datos nivela1)
  { echo "ROCOLA-SPOTIFY-NIVELES v1"; for i in $(seq 10 34); do printf 'semilla%s\t-1\t-2\t-4\t-10\t1700000000\n' $i; done; } > "$d/rocola_spotify_niveles.txt"
  T0=$(date +%s.%N)
  NV="9:pide spotify:track:f440d034q30|$P|34000"
  abre nivela1 "$d" "7:67" "$NV;55:copia" 56 TERM ROCOLA_OS=spotify,spotify-prueba,spotify_nivela
  o="$W/nivela1"; b="$d/rocola_spotify.txt"
  serie() { echo "# t dB (440 Hz, tramos de 100 ms)"; python3 -c "import sys; sys.path.insert(0, sys.argv[1]); from niveles import niveles; [print('%.2f %.3f' % p) for p in niveles(sys.argv[2], sys.argv[3], 440)]" "$G" "$1/sonado.raw" "$1/registro.txt"; }
  ventana() { python3 - "$1" "$2" "$3" <<'EOP'
import sys
L=[l.split() for l in open(sys.argv[1]).read().splitlines()[1:] if l.strip()]
v=sorted(float(b) for a,b in L if float(sys.argv[2])<=float(a)<=float(sys.argv[3]))
print("%.2f" % (v[len(v)//2] if v else -99))
EOP
  }
  serie "$o" > "$o/niveles.txt"
  i0=$(awk 'NR>1{print $1; exit}' "$o/niveles.txt")
  a1=$(ventana "$o/niveles.txt" "$(echo "$i0 + 3" | bc)" "$(echo "$i0 + 5" | bc)"); z1=$(ventana "$o/niveles.txt" "$(echo "$i0 + 26" | bc)" "$(echo "$i0 + 32" | bc)")
  echo "   primera vez: al empezar $a1 dB, al final $z1 dB"
  si "nivela1: sale con 0" test "$(cat $o/salio)" = 0
  si "nivela1: arranca con la mediana de las 25 medidas y en vivo" bash -c "grep -q 'nivelado de f440d034q30: arranca con la mediana de 25 de Spotify medidas' '$b' && grep -q 'en vivo los primeros 30 s hacia' '$b'"
  si "nivela1: se congela a los 30 s" contiene "$b" "congelado a los 30.0 s"
  si "nivela1: subio entre 5 y 8,5 dB en la cancion ($a1 -> $z1)" bash -c "echo '$z1 - $a1 >= 5 && $z1 - $a1 <= 8.5' | bc | grep -q 1"
  si "nivela1: sin brincos (pasada la entrada de XP, ningun salto de mas de 0,3 dB entre tramos de 100 ms)" python3 - "$o/niveles.txt" "$i0" <<'EOP'
import sys
L=[l.split() for l in open(sys.argv[1]).read().splitlines()[1:] if l.strip()]
v=[float(b) for a,b in L if float(sys.argv[2])+3<=float(a)<=float(sys.argv[2])+33]
sys.exit(0 if v and max(abs(v[i+1]-v[i]) for i in range(len(v)-1)) <= 0.3 else 1)
EOP
  si "nivela1: se midio y se guardo en rocola_spotify_niveles.txt" bash -c "grep -q '^f440d034q30	' '$d/rocola_spotify_niveles.txt'"
  d2="$W/datos-nivela2"; rm -rf "$d2"; cp -r "$d" "$d2"; rm -f "$d2/rocola_estado.txt"
  T0=$(date +%s.%N)
  abre nivela2 "$d2" "7:67" "$NV;30:copia" 31 TERM ROCOLA_OS=spotify,spotify-prueba,spotify_nivela
  o2="$W/nivela2"
  serie "$o2" > "$o2/niveles.txt"
  i2=$(awk 'NR>1{print $1; exit}' "$o2/niveles.txt")
  a2=$(ventana "$o2/niveles.txt" "$(echo "$i2 + 3" | bc)" "$(echo "$i2 + 5" | bc)"); z2=$(ventana "$o2/niveles.txt" "$(echo "$i2 + 12" | bc)" "$(echo "$i2 + 18" | bc)")
  echo "   segunda vez: al empezar $a2 dB, a los 12-18 s $z2 dB"
  si "nivela2: sale con 0" test "$(cat $o2/salio)" = 0
  si "nivela2: ya medida: sin nivelado en vivo" bash -c "! grep -q 'nivelado de f440d034q30' '$d2/rocola_spotify.txt' || [ \$(grep -c 'nivelado de f440d034q30: arranca' '$d2/rocola_spotify.txt') = 1 ]"
  si "nivela2: desde el primer segundo, mas fuerte que la primera vez al empezar (al menos 8 dB: $a1 -> $a2)" bash -c "echo '$a2 - $a1 >= 8' | bc | grep -q 1"
  si "nivela2: quieta (al empezar y a los 12-18 s a 0,5 dB: $a2, $z2)" bash -c "echo '$z2 - $a2 <= 0.5 && $a2 - $z2 <= 0.5' | bc | grep -q 1"
fi

# ---------------------------------------------------------------- portadatarde (22-11-2026): la pedida entra sin portada (aun bajandose, el
# servidor tarda 4 s) y, al llegar, se le pone a la que suena (antes se quedaba sin portada para siempre)
if [[ " $partes " == *" portadatarde "* ]]; then
  echo "== portadatarde (la portada que llega despues se pone a la que suena)"
  d=$(datos portadatarde); o="$W/portadatarde"; srv="$W/servidor-lento"; mkdir -p "$srv"
  convert -size 64x64 xc:'#3060a0' "$srv/p.jpg"
  pto=$(python3 -c "import socket; s=socket.socket(); s.bind(('127.0.0.1',0)); print(s.getsockname()[1]); s.close()")
  python3 -I - "$srv" "$pto" > "$W/servidor-lento.txt" 2>&1 <<'EOP' & sp=$!
import sys, time, http.server, functools
class H(http.server.SimpleHTTPRequestHandler):
    def do_GET(self):
        time.sleep(4); return super().do_GET()
http.server.ThreadingHTTPServer(('127.0.0.1', int(sys.argv[2])), functools.partial(H, directory=sys.argv[1])).serve_forever()
EOP
  U="http://127.0.0.1:$pto"; T0=$(date +%s.%N)
  abre portadatarde "$d" "7:67" "9:pide spotify:track:f440d012ee|$P|12000|$U/p.jpg|spotify:album:disco0009;20:copia" 21 TERM ROCOLA_OS=spotify,spotify-prueba
  kill $sp 2>/dev/null; wait $sp 2>/dev/null
  b="$d/rocola_spotify.txt"
  si "portadatarde: sale con 0" test "$(cat $o/salio)" = 0
  si "portadatarde: entro sin portada (aun no estaba)" bash -c "grep -q 'COBRADA spotify:track:f440d012ee' '$b' && ! grep -q 'COBRADA spotify:track:f440d012ee.*con portada' '$b'"
  si "portadatarde: al llegar se le puso a la que suena" contiene "$b" "la portada de spotify:track:f440d012ee llego: puesta a 1 (la que suena)"
  si "portadatarde: en /run" test -s "$o/run/rocola-xp/spotify/portadas/disco0009.jpg"
fi

# ---------------------------------------------------------------- repetida (22-11-2026): la de Spotify y tu MP3 de la misma grabacion son la
# MISMA para la regla de XP de no repetir («Esa ya va seguida», sin cobrar), en los dos sentidos. Tu cancion: «01 Cancion 1» de «Disco Uno»
# (el artista que XP saca de su carpeta; 3,24 s, de los datos de partida); la de Spotify con su mismo titulo, artista y duracion. Mientras suena otra larga de Spotify.
if [[ " $partes " == *" repetida "* ]]; then
  echo "== repetida (Spotify y tu MP3 de la misma grabacion no se repiten)"
  R="spotify:track:f440d003re|01 Cancion 1|Disco Uno|Disco Uno|3240"
  d=$(datos repetida-a); T0=$(date +%s.%N)
  abre repetida-a "$d" "7:67,10:13,11.5:13" "9:pide spotify:track:f550d020xa|$P|20000;9.5:pide $R;13:pide spotify:track:f660d004ya|$P|4000;18:copia" 19 TERM ROCOLA_OS=spotify,spotify-prueba
  o="$W/repetida-a"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$(log $o)"
  si "repetida-a: sale con 0" test "$(cat $o/salio)" = 0
  si "repetida-a: la de Spotify entro (4->3->2)" contiene "$(log $o)" "pide spotify:track:f440d003re listo=1 entro=1 creditos 3->2"
  si "repetida-a: tu MP3 de la misma grabacion no entro despues (ya va seguida)" bash -c "grep -q 'no entra: «01 Cancion 1» ya va seguida (mismo titulo, artista y duracion' '$b'"
  si "repetida-a: y no se cobro (la siguiente va de 2 a 1)" contiene "$(log $o)" "pide spotify:track:f660d004ya listo=1 entro=1 creditos 2->1"
  d=$(datos repetida-b); T0=$(date +%s.%N)
  abre repetida-b "$d" "7:67,10:13,11.5:13" "9:pide spotify:track:f550d020xa|$P|20000;13:pide $R;14:pide spotify:track:f660d004ya|$P|4000;18:copia" 19 TERM ROCOLA_OS=spotify,spotify-prueba
  o="$W/repetida-b"; b="$d/rocola_spotify.txt"
  sed 's/^/   /' "$(log $o)"
  si "repetida-b: sale con 0" test "$(cat $o/salio)" = 0
  si "repetida-b: con tu MP3 en la cola, la de Spotify de la misma grabacion no entra ni se cobra (2->2)" contiene "$(log $o)" "pide spotify:track:f440d003re listo=1 entro=0 creditos 2->2"
  si "repetida-b: el motivo en la bitacora" bash -c "grep -q 'no entra: «01 Cancion 1» ya va seguida' '$b'"
  si "repetida-b: otra distinta si entra (2->1)" contiene "$(log $o)" "pide spotify:track:f660d004ya listo=1 entro=1 creditos 2->1"
  d=$(datos repetida-c); T0=$(date +%s.%N)
  abre repetida-c "$d" "7:67,10:13,11.5:13" "9:pide spotify:track:f550d020xa|$P|20000;13:pide spotify:track:f440d003rf|01 Cancion 1|Otro Artista|Disco Uno|3240;18:copia" 19 TERM ROCOLA_OS=spotify,spotify-prueba
  o="$W/repetida-c"
  si "repetida-c: otro artista con el mismo titulo si entra (2->1)" contiene "$(log $o)" "pide spotify:track:f440d003rf listo=1 entro=1 creditos 2->1"
fi

# ---------------------------------------------------------------- vacia (22-11-2026, el registro del A4 del 21-11 a las 22:53): Soloist dice la
# cancion VACIA (uri «») unos cientos de ms al empezar cada una y a la mitad, y un queue_changed con 1 de contexto. Antes eso era «Soloist paso
# a otra» (y NO PEDIDO: pausa), el play adelantado de la siguiente entraba en bucle cada segundo y al final se devolvia el credito sin sonar.
# Ahora la vacia es pasajera: las dos suenan enteras, sin pausas, sin DEVUELTA y con un solo play adelantado.
if [[ " $partes " == *" vacia "* ]]; then
  echo "== vacia (la cancion vacia de Soloist es pasajera: ni otra cancion, ni no pedido, ni bucle del play adelantado)"
  d=$(datos vacia); T0=$(date +%s.%N)
  abre vacia "$d" "7:67" "9:pide spotify:track:f440d020va|$P|20000;9.3:pide spotify:track:f550d020vb|$P|20000;55:copia" 56 TERM \
    ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_VACIO=400
  o="$W/vacia"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  grep -E 'play|suena|acaba|acabo|DEVUELTA|NO PEDIDO|cola de Spotify' "$b" | sed 's/^/   /'
  si "vacia: sale con 0" test "$(cat $o/salio)" = 0
  si "vacia: el Soloist falso mando la cancion vacia (4 veces: al empezar y a la mitad de cada una)" bash -c "[ \$(grep -c 'cancion vacia' '$o/falso.txt') -ge 4 ]"
  t440=$(oye "$f" 440); t550=$(oye "$f" 550)
  si "vacia: la primera suena entera (20 s: $t440 s)" entre "$t440" 19.4 20.6
  si "vacia: la segunda suena entera (20 s: $t550 s)" entre "$t550" 19.4 20.6
  si "vacia: la vacia no fue «otra cancion» ni NO PEDIDO (solo el autoplay del final de cada una), ni DEVUELTA" bash -c "! grep -qE 'paso a «»|DEVUELTA' '$b' && [ \$(grep -c 'NO PEDIDO' '$b') = 2 ] && [ \$(grep -c 'NO PEDIDO.*autoplay' '$b') = 2 ]"
  si "vacia: un solo play adelantado (sin bucle)" bash -c "[ \$(grep -c 'play adelantado spotify' '$b') -le 1 ]"
  si "vacia: las dos acaban al terminar" bash -c "grep -q 'acabo spotify:track:f440d020va (Prueba): termino' '$b' && grep -q 'acabo spotify:track:f550d020vb (Prueba): termino' '$b'"
  si "vacia: la cola de Spotify: solo el aviso, una vez por cambio" bash -c "[ \$(grep -c 'la cola de Spotify trae' '$b') -le 2 ]"
  si "vacia: el estado: cobradas=2 devueltas=0" bash -c "[ '$(estado $o cobradas)$(estado $o devueltas)' = 20 ]"
fi

# ---------------------------------------------------------------- volumen (22-11-2026, el dueno eligio «Vigilar el volumen de Soloist»): Soloist
# arranca con el volumen de Spotify Connect en 40 (lo ultimo del telefono); antes de tocar la rocola le pide 100 y lo apunta una vez.
if [[ " $partes " == *" volumen "* ]]; then
  echo "== volumen (Soloist con el volumen bajo: la rocola le pide 100)"
  d=$(datos volumen); T0=$(date +%s.%N)
  abre volumen "$d" "7:67" "9:pide spotify:track:f440d010vo|$P|10000;25:copia" 26 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_VOLUMEN=40
  o="$W/volumen"; b="$d/rocola_spotify.txt"
  grep -E 'volumen|acabo' "$b" | sed 's/^/   /'
  si "volumen: sale con 0" test "$(cat $o/salio)" = 0
  # (23-11-2026) desde la revision, si Soloist aun no dijo su volumen al tocar se le pide 100 de una vez (y se apunta): el 40 puede no llegar
  si "volumen: se apunto y se pidio 100 (el 40 una sola vez, o antes de que Soloist lo dijera)" bash -c "n=\$(grep -c 'Soloist tenia el volumen en 40' '$b'); [ \$n = 1 ] || { [ \$n = 0 ] && grep -q 'Soloist aun no decia su volumen' '$b'; }"
  si "volumen: Soloist recibio set_volume 100" bash -c "grep -q 'volume.*100\|set_volume' '$o/falso.txt'"
  si "volumen: la cancion suena entera (cobradas=1 devueltas=0)" bash -c "[ '$(estado $o cobradas)$(estado $o devueltas)' = 10 ]"
fi

# ---------------------------------------------------------------- cortada (22-11-2026, la de la Sonora Santanera que se corto a media cancion y
# devolvio el credito): con 10 s o mas sonados, un corte se intenta seguir UNA vez desde donde iba (play y seek); si no se puede, acaba SIN
# devolver. Tres corridas: (1) deja de mandar sonido 6 s a los ~15 s y vuelve; (2) deja de mandar sonido y no vuelve; (3) se pausa sola.
if [[ " $partes " == *" cortada "* ]]; then
  echo "== cortada (un corte a media cancion sigue desde donde iba; si no se puede, acaba y el credito vuelve solo si no llego a la mitad)"
  d=$(datos cortada1); T0=$(date +%s.%N)
  abre cortada1 "$d" "7:67" "9:pide spotify:track:f440d030ca|$P|30000;58:copia" 59 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_GUION="25:corta;31:sigue"
  o="$W/cortada1"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  grep -E 'play|suena|acaba|acabo|corto|sigue|seguir|DEVUELTA' "$b" | sed 's/^/   /'; grep -E 'seek|acabo' "$o/falso.txt" | sed 's/^/   falso: /'
  t440=$(oye "$f" 440)
  si "cortada1: sale con 0" test "$(cat $o/salio)" = 0
  si "cortada1: se corto con mas de 10 s y se intenta seguir" bash -c "grep -q 'se corto spotify:track:f440d030ca (Prueba) a los 1[0-9].[0-9] s: el anillo sin sonido mas de 5 s; se intenta seguir' '$b' && grep -q 'sigue spotify:track:f440d030ca: activate, play y seek a 1[0-9].[0-9] s' '$b'"
  si "cortada1: Soloist recibio el seek" contiene "$o/falso.txt" "seek a 1"
  si "cortada1: siguio desde donde iba hasta su final (Soloist la acabo, sin volver a empezar)" bash -c "grep -q 'acabo spotify:track:f440d030ca' '$o/falso.txt' && grep -q 'acabo spotify:track:f440d030ca (Prueba): termino, sono .* (siguio tras un corte' '$b'"
  si "cortada1: se oye entera una sola vez (30 s: $t440 s)" entre "$t440" 29.2 30.8
  si "cortada1: sin DEVUELTA (cobradas=1 devueltas=0)" bash -c "! grep -q DEVUELTA '$b' && [ '$(estado $o cobradas)$(estado $o devueltas)' = 10 ]"

  d=$(datos cortada2); T0=$(date +%s.%N)
  abre cortada2 "$d" "7:67" "9:pide spotify:track:f550d060cb|$P|60000;45:copia" 46 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_GUION="25:corta"
  o="$W/cortada2"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  grep -E 'play|suena|acaba|acabo|corto|sigue|seguir|DEVUELTA' "$b" | sed 's/^/   /'
  si "cortada2: sale con 0" test "$(cat $o/salio)" = 0
  si "cortada2: se intenta seguir una vez" contiene "$b" "sigue spotify:track:f550d060cb: activate, play y seek a"
  si "cortada2: no se pudo seguir y sono menos de la mitad (27-11-2026): la bitacora dice el motivo, lo sonado y que no llego a la mitad" bash -c "grep -qE 'se corto otra vez spotify:track:f550d060cb|no se pudo seguir spotify:track:f550d060cb' '$b' && grep -qE 'DEVUELTA spotify:track:f550d060cb \(Prueba\): el anillo sin sonido mas de 5 s, sono 1[0-9].[0-9] s.*NO LLEGO A LA MITAD' '$b'"
  si "cortada2: el credito vuelve (cobradas=1 devueltas=1)" bash -c "grep -q DEVUELTA '$b' && [ '$(estado $o cobradas)$(estado $o devueltas)' = 11 ]"

  d=$(datos cortada3); T0=$(date +%s.%N)
  abre cortada3 "$d" "7:67" "9:pide spotify:track:f660d030cc|$P|30000;50:copia" 51 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_GUION="25:pausa"
  o="$W/cortada3"; f="$o/frec.txt"; b="$d/rocola_spotify.txt"
  grep -E 'play|suena|acaba|acabo|corto|sigue|seguir|DEVUELTA' "$b" | sed 's/^/   /'
  t660=$(oye "$f" 660)
  si "cortada3: sale con 0" test "$(cat $o/salio)" = 0
  si "cortada3: la pausa que no fue de la rocola se sigue desde donde iba" bash -c "grep -q 'se corto spotify:track:f660d030cc (Prueba) a los 1[0-9].[0-9] s: se paro; se intenta seguir' '$b' && grep -q 'acabo spotify:track:f660d030cc (Prueba): termino' '$b'"
  si "cortada3: se oye entera una sola vez (30 s: $t660 s)" entre "$t660" 29.2 30.8
  si "cortada3: sin DEVUELTA" nocontiene "$b" "DEVUELTA"
fi

# ---------------------------------------------------------------- speq (22-11-2026, spotify_eq y spotify_brillo): una de Spotify con un tono
# y ruido cortado a 16 kHz (el acantilado de Vorbis 160; «r16» en el id del Soloist falso). La primera vez: sin EQ automatico y con el realce
# de una sin medir, se mide entera y su huella (8 bandas y el corte) va por id a rocola_spotify_eq.txt, nada a rocola_eq.txt. La segunda vez
# (otra corrida con esos datos): su EQ automatico y su realce con el corte medido, como una tuya. Con las dos funciones apagadas (los mismos
# datos, con la huella guardada): como la primera vez (sin EQ ni realce a la medida) y la tabla sin tocar. Sin spotify_nivela (ganancia 1:
# lo que cambia el sonido es solo el EQ y el realce). La biblioteca: 60 huellas sanas inventadas en rocola_eq.txt (el promedio de la casa).
if [[ " $partes " == *" speq "* ]]; then
  echo "== speq (EQ automatico y brillo a la medida en una de Spotify: primera vez se mide, segunda se corrige, apagadas como antes)"
  EQID=f440d034r16
  EQP="9:pide spotify:track:$EQID|$P|34000"
  d=$(datos speq1)
  for i in $(seq 1 60); do printf -- '-8;0.5;0;-1.5;-2;-1;-3.5;-6;20000;0;30;2|/musica/inventada/huella%s.mp3\r\n' $i; done >> "$d/rocola_eq.txt"
  # el espectro de lo que sono (estereo 16 bits; los tramos de cada apertura con su ritmo): energia por bandas, en el tramo quieto
  bandas() { python3 - "$1/sonado.raw" "$1/registro.txt" "$G" <<'EOP'
import sys, numpy as np
sys.path.insert(0, sys.argv[3]); from frecuencias import tramos
a = np.fromfile(sys.argv[1], dtype='<i2').astype(np.float64) / 32768.0; x = a[0::2]
mejor = None
for ini, k, fs in tramos(sys.argv[2], len(x)):
    if mejor is None or k > mejor[1]: mejor = (ini, k, fs)
ini, k, fs = mejor
# de lo que sono, los segundos 6 a 26 de la cancion (el primer tramo con sonido de verdad)
y = x[ini:ini + k]; v = np.abs(y) > 1e-4; i0 = int(np.argmax(v)) if v.any() else 0
y = y[i0 + int(6 * fs): i0 + int(26 * fs)]
N = 8192; w = np.hanning(N); P = np.zeros(N // 2 + 1); n = 0
for j in range(0, len(y) - N, N // 2): P += np.abs(np.fft.rfft(y[j:j + N] * w)) ** 2; n += 1
P /= max(1, n); f = np.fft.rfftfreq(N, 1.0 / fs)
def db(a, b): m = (f >= a) & (f < b); return 10 * np.log10(P[m].mean() + 1e-20)
# (sin la banda del tono de 440 Hz) el grave, el medio, los agudos y lo de arriba del corte
print("g=%.2f m=%.2f a=%.2f s=%.2f c=%.2f" % (db(60, 120), db(1500, 3500), db(8000, 12000), db(12500, 15000), db(17000, 19500)))
EOP
  }
  campo() { echo "$1" | tr ' ' '\n' | sed -n "s/^$2=//p"; }
  T0=$(date +%s.%N)
  abre speq1 "$d" "7:67" "$EQP;50:copia" 51 TERM ROCOLA_OS=spotify,spotify-prueba,spotify_eq,spotify_brillo
  o="$W/speq1"; b="$d/rocola_spotify.txt"; tq="$d/rocola_spotify_eq.txt"
  grep -E "eq de $EQID" "$b" | sed 's/^/   /'
  b1=$(bandas "$o"); echo "   primera vez: $b1"
  si "speq1: sale con 0" test "$(cat $o/salio)" = 0
  si "speq1: la primera vez sin huella: sin EQ automatico y se mide" contiene "$b" "eq de $EQID: sin huella (sin EQ automatico y el realce de una sin medir); se mide para la proxima"
  si "speq1: al acabar entera se aprendio su huella y su corte" contiene "$b" "eq de $EQID: huella aprendida en 3"
  corte=$(awk -F'\t' -v id=$EQID '$1==id {print $10}' "$tq" 2>/dev/null)
  si "speq1: guardada por id en rocola_spotify_eq.txt con el corte de agudos medido (15-17,5 kHz: ${corte:-nada})" bash -c "[ -n '$corte' ] && python3 -c 'import sys; sys.exit(0 if 15000 <= float(sys.argv[1]) <= 17500 else 1)' '$corte'"
  si "speq1: nada de Spotify en las tablas de XP (rocola_eq.txt, rocola_sonoridad.txt, rocola_fuentes.txt)" bash -c "! grep -q 'spotify:' '$d/rocola_eq.txt' '$d/rocola_sonoridad.txt' '$d/rocola_fuentes.txt'"
  # la segunda vez
  d2="$W/datos-speq2"; rm -rf "$d2"; cp -r "$d" "$d2"; rm -f "$d2/rocola_estado.txt" "$d2/rocola_spotify.txt"   # (la bitacora de Spotify, de cero: solo lo de esta vez)
  T0=$(date +%s.%N)
  abre speq2 "$d2" "7:67" "$EQP;40:copia" 41 TERM ROCOLA_OS=spotify,spotify-prueba,spotify_eq,spotify_brillo
  o2="$W/speq2"; b2f="$d2/rocola_spotify.txt"
  grep -E "eq de $EQID" "$b2f" | sed 's/^/   /'
  b2=$(bandas "$o2"); echo "   segunda vez: $b2"
  lin=$(grep -m1 "eq de $EQID: con su huella" "$b2f")
  si "speq2: sale con 0" test "$(cat $o2/salio)" = 0
  si "speq2: con su huella: EQ automatico con correccion (no la primera vez)" bash -c "echo '$lin' | grep -q 'EQ automatico \[' && ! grep -q 'eq de $EQID: sin huella' '$b2f'"
  re=$(echo "$lin" | sed -n 's/.*; realce \([0-9.]*\) (sin medir \([0-9.]*\)).*/\1 \2/p')
  si "speq2: el realce con el corte medido, distinto del de una sin medir (${re:-nada})" bash -c "[ -n '$re' ] && set -- $re && [ \"\$1\" != \"\$2\" ]"
  si "speq2: no se vuelve a medir (ya tiene huella)" nocontiene "$b2f" "eq de $EQID: huella aprendida"
  # el sonido: lo que dijo el EQ en las bandas de 2,5 y 10 kHz contra lo que cambio de verdad (medio y agudos, contra el grave de referencia)
  cq=$(echo "$lin" | sed -n 's/.*EQ automatico \[\([^]]*\)\].*/\1/p')
  si "speq2: el sonido cambio como dice el EQ (2,5 kHz y 10 kHz, a 1,5 dB; EQ [$cq])" python3 - "$b1" "$b2" "$cq" <<'EOP'
import sys
a = dict(p.split('=') for p in sys.argv[1].split()); b = dict(p.split('=') for p in sys.argv[2].split())
q = [float(v) for v in sys.argv[3].split()]
d = lambda k: float(b[k]) - float(a[k])
ok = abs(d('m') - q[4]) <= 1.5 and abs((d('a') - d('m')) - (q[6] - q[4])) <= 1.5 and (abs(d('m')) + abs(d('a')) > 0.3 or abs(q[4]) + abs(q[6]) < 0.3)
print("   cambio medido: medio %+.2f dB, agudos %+.2f dB (EQ: 2,5 kHz %+.1f, 10 kHz %+.1f)" % (d('m'), d('a'), q[4], q[6]))
sys.exit(0 if ok else 1)
EOP
  # con las dos funciones apagadas: como la primera vez
  d3="$W/datos-speq3"; rm -rf "$d3"; cp -r "$d" "$d3"; rm -f "$d3/rocola_estado.txt" "$d3/rocola_spotify.txt"; cp "$tq" "$W/speq-tabla-antes.txt"
  T0=$(date +%s.%N)
  abre speq3 "$d3" "7:67" "$EQP;50:copia" 51 TERM ROCOLA_OS=spotify,spotify-prueba
  o3="$W/speq3"; b3=$(bandas "$o3"); echo "   apagadas: $b3"
  si "speq3: sale con 0" test "$(cat $o3/salio)" = 0
  si "speq3: apagadas: nada de eq en la bitacora y la tabla sin tocar" bash -c "! grep -q 'eq de $EQID' '$d3/rocola_spotify.txt' && cmp -s '$W/speq-tabla-antes.txt' '$d3/rocola_spotify_eq.txt'"
  si "speq3: apagadas suena como la primera vez (cada banda a 0,3 dB)" python3 - "$b1" "$b3" <<'EOP'
import sys
a = dict(p.split('=') for p in sys.argv[1].split()); b = dict(p.split('=') for p in sys.argv[2].split())
sys.exit(0 if all(abs(float(a[k]) - float(b[k])) <= 0.3 for k in 'gmas') else 1)
EOP
fi

# ---------------------------------------------------------------- revision de mejoras del 24-11-2026 (rocola-xp, Spotify): cierraws, sordo, adelantoviejo, diag, repetidad
# cierraws: un Soloist que acepta el WebSocket y lo cuelga al instante, una y otra vez: la espera de reconexion crece (500 ms, 1, 2, 4 y 5 s) y
# no se queda en 0,5 s (antes «_cerrado» la pisaba): en 30 s unas 8 conexiones, no 40 o mas.
if [[ " $partes " == *" cierraws "* ]]; then
  echo "== cierraws (el WebSocket que se cuelga al instante no se reconecta cada 0,5 s)"
  d=$(datos cierraws); T0=$(date +%s.%N)
  abre cierraws "$d" "7:67" "" 30 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_CIERRA_AL_CONECTAR=1
  o="$W/cierraws"; b="$d/rocola_spotify.txt"; n=$(cuenta "$b" "conectado con Soloist")
  echo "   conectado con Soloist: $n veces en 30 s"
  si "cierraws: sale con 0" test "$(cat $o/salio)" = 0
  si "cierraws: se reconecto (al menos 3 veces) y con espera creciente (menos de 16 en 30 s: $n)" bash -c "[ $n -ge 3 ] && [ $n -lt 16 ]"
fi

# sordo (24-11-2026): Soloist vivo pero sordo. El vigilante (la fase de /run/rocola-soloist/estado, que aqui se escribe a mano) en «corriendo»; el
# Soloist falso acepta el WebSocket y no dice ni contesta nada (SOLOIST_FALSO_SORDO=1); los umbrales se achican con ROCOLA_SPOTIFY_SORDO_MS
# (sin_sesion,sin_auth,entre_reinicios). Un lector hace de vigilante: guarda cada «reinicia» que aparece y lo borra.
sordo() {   # sordo NOMBRE FASE UMBRALES SEGUNDOS [VAR=valor...]
  local n="$1" fase="$2" um="$3" seg="$4"; shift 4
  local o="$W/$n"; mkdir -p "$o/run/rocola-soloist" "$o/soloist"; echo "fase=$fase" > "$o/run/rocola-soloist/estado"
  ( for i in $(seq 1 $((seg * 5))); do if [ -e "$o/soloist/reinicia" ]; then { date +%s; cat "$o/soloist/reinicia"; } >> "$o/reinicias.txt"; rm -f "$o/soloist/reinicia"; fi; sleep 0.2; done ) & local c=$!
  abre "$n" "$(datos $n)" "7:67" "" "$seg" TERM ROCOLA_OS=spotify,spotify-prueba ROCOLA_SPOTIFY_SORDO_MS="$um" "$@"
  kill $c 2>/dev/null; wait $c 2>/dev/null
}
if [[ " $partes " == *" sordo "* ]]; then
  echo "== sordo (Soloist vivo pero sordo: se pide al vigilante reiniciarlo, sin repetir)"
  T0=$(date +%s.%N); sordo sordo-a corriendo 600000,3000,600000 22 SOLOIST_FALSO_SORDO=1
  o="$W/sordo-a"; b="$W/datos-sordo-a/rocola_spotify.txt"; grep -E 'SORDO' "$b" | sed 's/^/   /'
  si "sordo-a: sale con 0" test "$(cat $o/salio)" = 0
  si "sordo-a: sin contestar a get_auth_state en 3 s: se pidio UN reinicio (el archivo reinicia, con su motivo y sin llaves)" bash -c "[ \$(grep -c 'rocola-xp: ' '$o/reinicias.txt') = 1 ] && grep -q 'sin contestar a get_auth_state' '$o/reinicias.txt'"
  si "sordo-a: la bitacora lo apunta una vez (1 de 6 hoy) y, con 10 min entre reinicios, no se repite" bash -c "[ \$(grep -c 'SOLOIST VIVO PERO SORDO' '$b') = 1 ] && grep -q '1 de 6 hoy' '$b'"
  si "sordo-a: se le volvio a preguntar get_auth_state (cada 10 s sin respuesta)" bash -c "[ \$(grep -c 'get_auth_state' '$o/falso.txt') -ge 2 ]"
  T0=$(date +%s.%N); sordo sordo-b sin_llave 600000,3000,600000 14 SOLOIST_FALSO_SORDO=1
  o="$W/sordo-b"; b="$W/datos-sordo-b/rocola_spotify.txt"
  si "sordo-b: con el vigilante sin llave (no «corriendo») no se pide nada" bash -c "[ ! -e '$o/reinicias.txt' ] && ! grep -q 'SORDO' '$b'"
  T0=$(date +%s.%N); sordo sordo-c corriendo 600000,3000,6000 24 SOLOIST_FALSO_SORDO=1
  o="$W/sordo-c"; b="$W/datos-sordo-c/rocola_spotify.txt"
  si "sordo-c: con 6 s entre reinicios se pide otro despues (3 o 4 en 22 s, no uno por vuelta)" bash -c "n=\$(grep -c 'SOLOIST VIVO PERO SORDO' '$b'); [ \$n -ge 3 ] && [ \$n -le 4 ]"
  T0=$(date +%s.%N); sordo sordo-d corriendo 4000,600000,600000 16 SOLOIST_FALSO_SESION=0
  o="$W/sordo-d"; b="$W/datos-sordo-d/rocola_spotify.txt"; grep -E 'SORDO' "$b" | sed 's/^/   /'
  si "sordo-d: conectado sin sesion de Spotify mas de 4 s (umbral de la prueba): se pide el reinicio" bash -c "[ \$(grep -c 'rocola-xp: ' '$o/reinicias.txt') = 1 ] && grep -q 'sin sesion de Spotify' '$o/reinicias.txt'"
  T0=$(date +%s.%N); sordo sordo-e esperando 600000,3000,600000 14 SOLOIST_FALSO_SORDO=1
  si "sordo-e: con el vigilante reiniciando Soloist (esperando) no se pide otro" bash -c "[ ! -e '$W/sordo-e/reinicias.txt' ]"
  T0=$(date +%s.%N); sordo sordo-f corriendo 600000,3000,600000 14
  si "sordo-f: un Soloist que si oye (sesion y respuestas) nunca se reinicia" bash -c "[ ! -e '$W/sordo-f/reinicias.txt' ] && ! grep -q 'SORDO' '$W/datos-sordo-f/rocola_spotify.txt'"
fi

# adelantoviejo (24-11-2026): el archivo que apaga el play adelantado caduca a los 7 dias (se borra y se vuelve a probar, apuntandolo)
if [[ " $partes " == *" adelantoviejo "* ]]; then
  echo "== adelantoviejo (rocola_spotify_sin_adelanto.txt caduca a los 7 dias)"
  d=$(datos adelantoviejo-a); printf '2026-11-01 00:00:00\tprueba\n' > "$d/rocola_spotify_sin_adelanto.txt"; touch -d '8 days ago' "$d/rocola_spotify_sin_adelanto.txt"; T0=$(date +%s.%N)
  abre adelantoviejo-a "$d" "7:67" "" 12 TERM ROCOLA_OS=spotify,spotify-prueba
  si "adelantoviejo-a: de hace 8 dias: se borro y se apunto" bash -c "[ ! -e '$d/rocola_spotify_sin_adelanto.txt' ] && grep -q 'de hace mas de 7 dias' '$d/rocola_spotify.txt'"
  d=$(datos adelantoviejo-b); printf '2026-11-23 00:00:00\tprueba\n' > "$d/rocola_spotify_sin_adelanto.txt"; touch -d '2 days ago' "$d/rocola_spotify_sin_adelanto.txt"; T0=$(date +%s.%N)
  abre adelantoviejo-b "$d" "7:67" "" 12 TERM ROCOLA_OS=spotify,spotify-prueba
  si "adelantoviejo-b: de hace 2 dias: se queda y se apunta que esta apagado" bash -c "[ -e '$d/rocola_spotify_sin_adelanto.txt' ] && grep -q 'el play adelantado esta apagado' '$d/rocola_spotify.txt'"
fi

# diag (24-11-2026): lo que apunta la bitacora de Spotify (sellos en ms, huecos, el adelanto usado, eventos raros y errores de command_result una vez)
# y las opciones (shuffle/repeat) que ya se saben no se vuelven a mandar antes de cada play: dos canciones de 6 s seguidas con el play adelantado.
if [[ " $partes " == *" diag "* ]]; then
  echo "== diag (sellos en ms, huecos, adelanto usado, eventos raros una vez, opciones ya sabidas)"
  d=$(datos diag); T0=$(date +%s.%N)
  abre diag "$d" "7:67" "9:pide spotify:track:f440d006di|$P|6000;9.2:pide spotify:track:f550d006dj|$P|6000;24:copia" 25 TERM ROCOLA_OS=spotify,spotify-prueba \
    SOLOIST_FALSO_GUION="14:raro;15:raro;16:resulterr;17:resulterr"
  o="$W/diag"; b="$d/rocola_spotify.txt"
  grep -E 'suena|play|acabo|adelantado|raro|error a' "$b" | sed 's/^/   /'
  si "diag: sale con 0" test "$(cat $o/salio)" = 0
  si "diag: «suena» dice a cuantos ms del play" bash -c "[ \$(grep -cE 'suena spotify:track:f[45]{2}0d006d[ij] .*, a [0-9]+ ms del play' '$b') = 2 ]"
  si "diag: la segunda dice el hueco desde que acabo la anterior" bash -c "grep -qE 'suena spotify:track:f550d006dj .*, hueco [0-9]+ ms desde que acabo la anterior' '$b'"
  si "diag: «acabo» dice los huecos a media cancion y los subdesbordes" bash -c "[ \$(grep -cE 'acabo spotify:track:f[45]{2}0d006d[ij] .*\[huecos a media cancion [0-9]+ ms, subdesbordes del anillo [0-9]+\]' '$b') = 2 ]"
  si "diag: el adelanto usado se apunta (con su cuenta)" bash -c "grep -qE 'play adelantado usado: spotify:track:f550d006dj .*usadas 1 de 1' '$b'"
  si "diag: el evento raro se apunta UNA vez (mandado 2) y solo con los nombres de sus campos" bash -c "[ \$(grep -c 'evento que no se conoce: «zz_evento_raro» con los campos type,campo_a,campo_b' '$b') = 1 ]"
  si "diag: el command_result con error, una vez (mandado 2)" bash -c "[ \$(grep -c 'Soloist contesto con error a «play»: fallo de prueba' '$b') = 1 ]"
  si "diag: set_shuffle una sola vez (al entrar la sesion: las opciones ya sabidas no se repiten antes de cada play)" bash -c "[ \$(grep -c 'set_shuffle' '$o/falso.txt') = 1 ]"
fi

# repetidad (24-11-2026): tu MP3 tiene pareja en la sustitucion invisible pero con OTRA uri que la de Spotify que se pide: antes no se miraba mas y
# la misma grabacion se repetia; ahora cae a titulo, artista y duracion (salvo pareja confirmada como otra grabacion, Verif -1)
if [[ " $partes " == *" repetidad "* ]]; then
  echo "== repetidad (la pareja de tu MP3 es otra uri: se mira titulo, artista y duracion)"
  R="spotify:track:f440d003re|01 Cancion 1|Disco Uno|Disco Uno|3240"
  for v in 0 -1; do
    n=repetidad$([ $v = 0 ] && echo a || echo b)
    d=$(datos $n); T0=$(date +%s.%N)
    { printf 'ROCOLA-SPOTIFY-PARES v1\n'; printf '%s\t0\t0\tf990d003zz\t-\t3240\t3240\t%s\t%s\t-\t-\t-\t-\ttitulo\n' "$d/musica/Los Ejemplos - Disco Uno/01 Cancion 1.mp3" "$v" "$(date +%s)"; } > "$d/rocola_spotify_pares.txt"
    abre $n "$d" "7:67,10:13,11.5:13" "9:pide spotify:track:f550d020xa|$P|20000;13:pide $R;18:copia" 19 TERM ROCOLA_OS=spotify,spotify-prueba
    o="$W/$n"; sed 's/^/   /' "$(log $o)"
    si "$n: sale con 0" test "$(cat $o/salio)" = 0
    if [ $v = 0 ]; then
      si "$n: pareja con otra uri sin Verif -1: la de Spotify de la misma grabacion no entra ni se cobra" contiene "$(log $o)" "pide spotify:track:f440d003re listo=1 entro=0 creditos 2->2"
    else
      si "$n: pareja confirmada como otra grabacion (Verif -1): si entra (2->1)" contiene "$(log $o)" "pide spotify:track:f440d003re listo=1 entro=1 creditos 2->1"
    fi
  done
fi

# cortadalarga (24-11-2026): Soloist se cae a los 25 s y el vigilante tarda 30 s en volver a abrirlo (VIGILANTE_FALSO_ESPERA). Con la fase del
# vigilante en «esperando» (Soloist se esta reiniciando) la ventana de seguir la cortada es de 60 s y, cuando vuelve, sigue desde donde iba
# sin devolver el credito; con «corriendo» se rinde a los 15 s (acaba sin devolver: ya habia sonado mas de 10 s)
if [[ " $partes " == *" cortadalarga "* ]]; then
  echo "== cortadalarga (con Soloist reiniciandose, seguir una cortada tiene 60 s y no 15)"
  for v in esperando corriendo; do
    n=cortadalarga-$v; id=f770d030cd; [ $v = corriendo ] && id=f770d040cd; d=$(datos $n); o="$W/$n"; mkdir -p "$o/run/rocola-soloist"; echo "fase=corriendo" > "$o/run/rocola-soloist/estado"; T0=$(date +%s.%N)
    # (27-11-2026: con el vigilante reiniciando a Soloist ya no se deja pedir: «esperando» se pone a los 15 s, con la cancion sonando, antes del corte de los 25 s)
    if [ $v = esperando ]; then ( sleep 15; echo "fase=esperando" > "$o/run/rocola-soloist/estado" ) & fi
    abre $n "$d" "7:67" "9:pide spotify:track:$id|$P|$([ $v = corriendo ] && echo 40000 || echo 30000);88:copia" 89 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_GUION="25:cae" VIGILANTE_FALSO_ESPERA=30
    f="$o/frec.txt"; b="$d/rocola_spotify.txt"
    grep -E 'play|suena|acaba|acabo|corto|sigue|seguir|reiniciando|DEVUELTA|desconect' "$b" | sed 's/^/   /'
    t770=$(oye "$f" 770)
    si "$n: sale con 0" test "$(cat $o/salio)" = 0
    si "$n: se corto por el WebSocket caido y se intento seguir" contiene "$b" "se corto spotify:track:$id (Prueba) a los 1"
    if [ $v = esperando ]; then
      si "$n: se apunta que seguir tiene 60 s en vez de 15" contiene "$b" "Soloist se esta reiniciando (vigilante esperando): seguir tiene 60 s en vez de 15"
      si "$n: no se rindio a los 15 s" nocontiene "$b" "no se pudo seguir"
      si "$n: cuando Soloist volvio siguio desde donde iba y acabo de verdad, sin devolver" bash -c "grep -q 'acabo spotify:track:$id (Prueba): termino, sono .* (siguio tras un corte' '$b' && ! grep -q DEVUELTA '$b'"
    else
      si "$n: con el vigilante «corriendo» se rinde a los 15 s y, como no llego a la mitad, el credito vuelve (27-11-2026)" bash -c "grep -q 'no se pudo seguir spotify:track:$id en 15 s' '$b' && grep -q 'DEVUELTA spotify:track:$id' '$b' && ! grep -q 'se esta reiniciando' '$b'"
    fi
  done
fi

# ---------------------------------------------------------------- arranque (27-11-2026, spotify_arranque): sin el silencio digital del principio
# El Soloist falso entrega ceros al empezar cada cancion (SOLOIST_FALSO_SILENCIO=MS; el silencio SUSTITUYE los primeros MS ms de la cancion). Se mide
# cuando empieza a oirse el tono (frec.txt de la tarjeta con reloj) contra la misma cancion sin silencio: sin la funcion el silencio se oye; con
# ella el principio vuelve a donde estaba; con 3000 ms (mas que el tope de 1500) se quitan 1500 y suena el resto; un principio suave de verdad (el
# tono a -40 dBFS 600 ms) NO se toca. El arranque de una cancion varia unos 0,4 s de una corrida a otra (cuando abre la tarjeta contra el play de
# Soloist), asi que se usan silencios grandes y se comparan con ese margen.
if [[ " $partes " == *" arranque "* ]]; then
  echo "== arranque (sin el silencio digital del principio, con tope, sin comerse la musica suave ni chasquido)"
  paso() {   # paso NOMBRE LISTA_DE_FUNCIONES [VAR=valor...]: una de 440 Hz de 8 s
    local n="$1" fu="$2"; shift 2; local d; d=$(datos $n); T0=$(date +%s.%N)
    abre $n "$d" "7:67" "9:pide spotify:track:f440d008aa|$P|8000;24:copia" 25 TERM ROCOLA_OS=$fu "$@"
  }
  paso arranque0 spotify,spotify-prueba,spotify_arranque
  paso arranque1 spotify,spotify-prueba SOLOIST_FALSO_SILENCIO=1400
  paso arranque2 spotify,spotify-prueba,spotify_arranque SOLOIST_FALSO_SILENCIO=1400
  paso arranque3 spotify,spotify-prueba,spotify_arranque SOLOIST_FALSO_SILENCIO=3000
  paso arranque4 spotify,spotify-prueba,spotify_arranque SOLOIST_FALSO_INTRO_SUAVE=600
  paso arranque5 spotify,spotify-prueba SOLOIST_FALSO_INTRO_SUAVE=600
  t0=$(primero "$W/arranque0/frec.txt" 440); t1=$(primero "$W/arranque1/frec.txt" 440); t2=$(primero "$W/arranque2/frec.txt" 440); t3=$(primero "$W/arranque3/frec.txt" 440)
  echo "   primer tono: sin silencio $t0 s; 1400 ms de ceros sin la funcion $t1 s, con ella $t2 s; 3000 ms con ella $t3 s"
  for n in arranque0 arranque1 arranque2 arranque3 arranque4 arranque5; do si "$n: sale con 0" test "$(cat $W/$n/salio)" = 0; done
  si "arranque: sin la funcion, el silencio de 1400 ms se oye (diferencia $(echo "$t1 - $t0" | bc) s: 0,9 a 2)" entre "$(echo "$t1 - $t0" | bc)" 0.9 2.0
  si "arranque: con la funcion, el principio llega bastante antes que sin ella ($(echo "$t1 - $t2" | bc) s antes: 0,6 o mas; la diferencia con la cancion sin silencio, $(echo "$t2 - $t0" | bc) s)" entre "$(echo "$t1 - $t2" | bc)" 0.6 3
  si "arranque: con 3000 ms de ceros se quitan solo 1500 (tope): quedan 1500 de silencio (diferencia con la cancion sin silencio $(echo "$t3 - $t0" | bc) s: 1 a 2,4)" entre "$(echo "$t3 - $t0" | bc)" 1.0 2.4
  b2="$W/datos-arranque2/rocola_spotify.txt"; b3="$W/datos-arranque3/rocola_spotify.txt"; b4="$W/datos-arranque4/rocola_spotify.txt"
  grep -h 'arranque de' "$b2" "$b3" | sed 's/^/   /'
  si "arranque: la bitacora dice cuantos ms quito (1400 -> entre 1350 y 1450)" bash -c "grep -qE 'arranque de spotify:track:f440d008aa: (13[5-9][0-9]|14[0-4][0-9]) ms de silencio digital al principio quitados' '$b2'"
  si "arranque: con 3000 la bitacora dice 1500 y el tope" contiene "$b3" "arranque de spotify:track:f440d008aa: 1500 ms de silencio digital al principio quitados (el tope: 1500 ms; lo que sigue es de la grabacion)"
  si "arranque: sin la funcion no apunta nada (como antes)" nocontiene "$W/datos-arranque1/rocola_spotify.txt" "silencio digital al principio quitados"
  # el principio suave (el tono a -40 dBFS, 600 ms) no se come: no se apunta nada y se oye la cancion entera (8 s) con y sin la funcion
  e4=$(oye "$W/arranque4/frec.txt" 440); e5=$(oye "$W/arranque5/frec.txt" 440)
  si "arranque: el principio suave no apunta nada (nada quitado)" nocontiene "$b4" "silencio digital al principio quitados"
  si "arranque: y se oye entero, con la funcion ($e4 s) y sin ella ($e5 s), de 8 s" bash -c "python3 -c 'import sys; sys.exit(0 if 7.75 <= $e4 <= 8.4 and 7.75 <= $e5 <= 8.4 else 1)'"
  # la cancion, entera menos lo que el falso puso de ceros en lugar de tono (la funcion no se come musica: lo que se quita es de los ceros)
  for p in "arranque0 8" "arranque2 6.6" "arranque3 5"; do
    set -- $p; n=$1; esp=$2; f="$W/$n/frec.txt"; t440=$(oye "$f" 440)
    si "$n: el tono se oye entero ($t440 s de $esp)" bash -c "python3 -c 'import sys; sys.exit(0 if $esp - 0.4 <= $t440 <= $esp + 0.3 else 1)'"
  done
  # (los huecos a media cancion de la bitacora no se comparan: con la maquina cargada salen al azar en todas las corridas, tambien en las que no quitan nada)
  # sin chasquido en el primer sonido: el falso pone el tono a media fase tras sus ceros (un escalon de hasta la amplitud entera sin la funcion); con
  # ella, el fundido de 1 ms lo hace rampa. De 50 ms antes del primer tono a 100 ms despues, ningun salto entre dos muestras seguidas mayor que 0,03
  # (el de un seno de 440 Hz de esa amplitud es 0,009)
  salto() { python3 - "$W/$1/sonado.raw" "$2" <<'EOP'
import sys, numpy as np
a = np.fromfile(sys.argv[1], dtype='<i2').astype(np.float64) / 32768.0; x = a[0::2]
i = int(float(sys.argv[2]) * 44100); d = np.abs(np.diff(x[max(0, i - 2205):i + 4410]))
print("%.4f" % d.max())
EOP
  }
  s0=$(salto arranque0 "$t0"); s2=$(salto arranque2 "$t2"); s3=$(salto arranque3 "$t3")
  s1=$(salto arranque1 "$t1")
  si "arranque: sin chasquido en el primer sonido (mayor salto entre muestras: con 1400 ms $s2, con 3000 ms $s3; 0,03 como mucho; sin la funcion, con el escalon del falso: $s1)" bash -c "python3 -c 'import sys; sys.exit(0 if max($s2, $s3) <= 0.03 else 1)'"
fi

# ---------------------------------------------------------------- sordopide (27-11-2026): con Soloist sordo no se cobra
# El nucleo no deja pedir una de Spotify si Soloist no responde: sin sesion, sordo (conectado y sin contestar a get_auth_state), reiniciandose (el
# vigilante «esperando») o con el reinicio ya pedido (el archivo «reinicia»). Entonces no se cobra ni entra en la cola y sale el aviso de XP
# («Espera un momento y vuelve a intentarlo»). Antes, con el vigilante reiniciando a Soloist (que seguia con su sesion) se cobraba y se devolvia despues.
if [[ " $partes " == *" sordopide "* ]]; then
  echo "== sordopide (con Soloist sordo, reiniciandose o con el reinicio pedido: no se cobra ni entra)"
  sp() {   # sp NOMBRE FASE_DEL_VIGILANTE [REINICIA] [VAR=valor...]
    local n="$1" fase="$2" rei="$3"; shift 3; local o="$W/$n" d; d=$(datos $n); mkdir -p "$o/run/rocola-soloist" "$o/soloist"
    echo "fase=$fase" > "$o/run/rocola-soloist/estado"; [ "$rei" = reinicia ] && echo "rocola-xp: prueba" > "$o/soloist/reinicia"
    T0=$(date +%s.%N); abre $n "$d" "7:67" "14:pide spotify:track:f440d010aa|$P|10000;16:copia" 17 TERM ROCOLA_OS=spotify,spotify-prueba "$@"
  }
  sp sordopide-a corriendo no SOLOIST_FALSO_SORDO=1
  sp sordopide-b esperando no
  sp sordopide-c corriendo reinicia
  sp sordopide-d corriendo no
  for v in a b c d; do o="$W/sordopide-$v"; b="$W/datos-sordopide-$v/rocola_spotify.txt"; sed 's/^/   /' "$(log $o)" | head -3; grep -E "no se pide|COBRADA" "$b" | sed 's/^/   /'; done
  si "sordopide-a: Soloist sordo (conectado, sin contestar): no entra ni se cobra (4->4)" contiene "$(log $W/sordopide-a)" "pide spotify:track:f440d010aa listo=0 entro=0 creditos 4->4"
  si "sordopide-b: Soloist con sesion pero el vigilante lo esta reiniciando: no entra ni se cobra (4->4)" contiene "$(log $W/sordopide-b)" "pide spotify:track:f440d010aa listo=0 entro=0 creditos 4->4"
  si "sordopide-c: con el reinicio ya pedido (archivo reinicia): no entra ni se cobra (4->4)" contiene "$(log $W/sordopide-c)" "pide spotify:track:f440d010aa listo=0 entro=0 creditos 4->4"
  si "sordopide-d: un Soloist que oye y un vigilante corriendo: entra y cobra (4->3), como siempre" contiene "$(log $W/sordopide-d)" "pide spotify:track:f440d010aa listo=1 entro=1 creditos 4->3"
  for v in a b c; do
    si "sordopide-$v: sale con 0, sin COBRADA ni DEVUELTA y nada sono (el cobro nunca ocurrio)" bash -c "[ \"\$(cat $W/sordopide-$v/salio)\" = 0 ] && ! grep -qE 'COBRADA|DEVUELTA' '$W/datos-sordopide-$v/rocola_spotify.txt' && [ '$(oye "$W/sordopide-$v/frec.txt" 440)' = 0.00 ]"
  done
  si "sordopide: la bitacora dice por que no se pidio (sordo / reiniciandose / reinicio pedido)" bash -c "grep -q 'no se pide spotify:track:f440d010aa: Soloist .*sordo.* (no se cobra)' '$W/datos-sordopide-a/rocola_spotify.txt' || grep -q 'no se pide spotify:track:f440d010aa: Soloist sin sesion (no se cobra)' '$W/datos-sordopide-a/rocola_spotify.txt'; grep -q 'Soloist reiniciandose (vigilante esperando) (no se cobra)' '$W/datos-sordopide-b/rocola_spotify.txt' && grep -q 'Soloist sordo, con el reinicio pedido (no se cobra)' '$W/datos-sordopide-c/rocola_spotify.txt'"
fi

# ---------------------------------------------------------------- mitad (27-11-2026): una que se corta y no se puede seguir devuelve el credito si no llego a la mitad
# Cuarenta segundos de cancion: cortada a los ~14 s (antes de la mitad, 20 s) y sin poder seguirla (el Soloist falso no vuelve a mandar sonido): el
# credito vuelve y la bitacora dice que no llego a la mitad; cortada a los ~25 s (pasada la mitad): acaba sin devolver, como desde el 22-11.
if [[ " $partes " == *" mitad "* ]]; then
  echo "== mitad (cortada sin poder seguirla: el credito vuelve si sono menos de la mitad; desde la mitad, no)"
  d=$(datos mitada); T0=$(date +%s.%N)
  abre mitada "$d" "7:67" "9:pide spotify:track:f440d040aa|$P|40000;48:copia" 49 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_GUION="25:corta"
  o="$W/mitada"; b="$d/rocola_spotify.txt"; grep -E 'play|suena|corto|seguir|DEVUELTA|acabo' "$b" | sed 's/^/   /'
  si "mitada: sale con 0" test "$(cat $o/salio)" = 0
  si "mitada: se corto con mas de 10 s y se intento seguir una vez, sin poder" bash -c "grep -q 'se corto spotify:track:f440d040aa (Prueba) a los 1[0-9].[0-9] s' '$b' && grep -qE 'no se pudo seguir spotify:track:f440d040aa en 15 s|se corto otra vez spotify:track:f440d040aa' '$b'"
  si "mitada: antes de la mitad: DEVUELTA y la bitacora lo dice (iba en el s 1x de 40)" bash -c "grep -qE 'DEVUELTA spotify:track:f440d040aa \(Prueba\): .*NO LLEGO A LA MITAD \(iba en el s 1[0-9]\.[0-9] de 40\.0\): se devuelve el credito' '$b' && ! grep -q 'acabo spotify:track:f440d040aa' '$b'"
  si "mitada: el estado: cobradas=1 devueltas=1" bash -c "[ '$(estado $o cobradas)$(estado $o devueltas)' = 11 ]"
  d=$(datos mitadb); T0=$(date +%s.%N)
  abre mitadb "$d" "7:67" "9:pide spotify:track:f550d040bb|$P|40000;58:copia" 59 TERM ROCOLA_OS=spotify,spotify-prueba SOLOIST_FALSO_GUION="36:corta"
  o="$W/mitadb"; b="$d/rocola_spotify.txt"; grep -E 'play|suena|corto|seguir|DEVUELTA|acabo' "$b" | sed 's/^/   /'
  si "mitadb: sale con 0" test "$(cat $o/salio)" = 0
  si "mitadb: cortada pasada la mitad (a los 2x s de 40): no se pudo seguir y acaba SIN devolver (como desde el 22-11)" bash -c "grep -q 'se corto spotify:track:f550d040bb (Prueba) a los 2[0-9].[0-9] s' '$b' && grep -qE 'acabo spotify:track:f550d040bb \(Prueba\): el anillo sin sonido mas de 5 s, sono 2[0-9].[0-9] s' '$b' && ! grep -q DEVUELTA '$b'"
  si "mitadb: el estado: cobradas=1 devueltas=0" bash -c "[ '$(estado $o cobradas)$(estado $o devueltas)' = 10 ]"
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; fi
[ $mal = 0 ]
