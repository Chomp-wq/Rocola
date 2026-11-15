#!/bin/bash
# LA PRUEBA DEL NUCLEO DE SPOTIFY (fase 11, grupo spotify-xp; FASE11_spotify-xp.md). rocola-xp de verdad, fuera de pantalla, con la tarjeta
# de prueba con reloj (Pruebas/Windows/alsa/reloj.c: suena a su ritmo y guarda lo que DE VERDAD sono), un Soloist FALSO (SoloistFalso/: la
# API WebSocket de la documentacion de Soloist y el sonido de cada cancion en el anillo de I1, un tono con su firma) abierto por un vigilante
# falso (vigilante-falso.sh, en lugar de rocola-soloist), y el doble del catalogo (la funcion «spotify-prueba»: RocolaOs/RocolaOs.SpotifyPrueba.cs)
# que pide canciones de Spotify por I6 (RocolaOsSpotifyPide). Datos INVENTADOS (los de partida de las escenas de ventanas, DATOS0). NUNCA el
# Soloist de verdad.
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   escenas (de fabrica todas): apagado fabrica dos mezcla celular carrera fallos lento fuera frecuencia apagon portada pulso vigilante noche
#   PULSO: el libpulse.so.0 de rocola-pulso (sistema/rocola/pulso, otro grupo) para las escenas «pulso» y «vigilante» (sin el, no se corren)
#   VIGILANTE: el rocola-soloist de verdad (sistema/board/rocola/rootfs_overlay/usr/bin/rocola-soloist, del grupo soloist) para la escena
#   «vigilante» (sin el, no se corre): rocola-xp lo abre y el abre el Soloist falso por soloist-envoltorio.sh (SOLOIST_BIN); y para «fabrica»
#   (el de verdad, SIN llave: espera la llave y no abre nada)
# De la junta de Spotify (15-11-2026): «fabrica» (el modo encendido de fabrica sin ninguna llave = XP tal cual, sin una peticion a la red y
# con el vigilante esperando sin gastar; contra la misma escena sin ROCOLA_OS) y «noche» (el modo noche de XP baja una de Spotify al
# noche_pct de noche y con «Probar», con su rampa, igual que una de XP; niveles.py)
#   W: la carpeta de trabajo (/root/rocola-trabajo/fase11/spotify-xp/prueba; se rehace); FALSO: el Soloist falso compilado (se compila si
#   falta, con el candado de lo pesado); DATOS0 (/root/rocola-trabajo/port/ventanas/datos0, se copian, nunca se tocan)
# Las cuentas del sonido: frecuencias.py (tramos de 50 ms con la frecuencia dominante). Cada cancion de Spotify de prueba lleva su tono en su
# id (spotify:track:fHZdSEG...): 440, 550, 660, 770, 880 Hz; el autoplay del falso, 990 Hz.
# Cada comprobacion: BIEN o MAL en una linea; al final «TODO BIEN (N comprobaciones)» o cuantas MAL (y sale con 1).
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
partes="${*:-apagado fabrica dos mezcla celular carrera fallos lento fuera frecuencia apagon portada pulso vigilante noche}"
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
if [ ! -f "$FALSO/soloist-falso.dll" ] || [ "$G/SoloistFalso/Program.cs" -nt "$FALSO/soloist-falso.dll" ]; then
  echo "(compilando el Soloist falso en $FALSO)"
  flock /root/rocola-trabajo/fase11/pesado.lock dotnet build "$G/SoloistFalso/SoloistFalso.csproj" -c Release -o "$FALSO" --disable-build-servers \
    -p:UseSharedCompilation=false -p:BaseIntermediateOutputPath="$FALSO/obj/" > "$W/falso-build.txt" 2>&1 || { cat "$W/falso-build.txt"; exit 1; }
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
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf \
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
      ROCOLA_SPOTIFY_API="http://127.0.0.1:9/v1" ROCOLA_SPOTIFY_CUENTAS="http://127.0.0.1:9" "${ST[@]}"; fi
    wait $sp 2>/dev/null
  done
  a="$W/fabrica-xp"; o="$W/fabrica-os"; d="$W/datos-fabrica-os"; b="$d/rocola_spotify.txt"
  red() { grep -cE 'connect\(.*sa_family=AF_INET6?' "$1/strace.txt" 2>/dev/null; }
  progs() { grep -c ' execve(' "$1/strace.txt" 2>/dev/null; }
  # los programas abiertos entre las dos miradas (de los 18 a los 38 s: el vigilante ya esperando)
  progs2() { local t1 t2; t1=$(sed -n 1p "$1/vigilante-cpu.txt" 2>/dev/null | cut -d' ' -f2); t2=$(sed -n 2p "$1/vigilante-cpu.txt" 2>/dev/null | cut -d' ' -f2)
    [ -n "$t1" ] && [ -n "$t2" ] && awk -v a="$t1" -v b="$t2" '$2+0 >= a && $2+0 <= b && / execve\(/' "$1/strace.txt" | grep -c . || echo -1; }
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
  ea=$(sed 's/^[0-9-]* [0-9:.]* *//' "$W/datos-fabrica-xp/rocola_error.txt" 2>/dev/null | grep -vc '^$'); eo=$(sed 's/^[0-9-]* [0-9:.]* *//' "$d/rocola_error.txt" 2>/dev/null | grep -vc '^$')
  si "fabrica: rocola_error.txt igual de largo que sin ROCOLA_OS ($ea y $eo renglones: ningun aviso raro)" test "$ea" = "$eo"
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
# (pregunta del dueno del 15-11-2026; pendiente 5 de SPOTIFY_JUNTA_PENDIENTES.md). Cuatro corridas, todas con el modo noche de fabrica (noche=1,
# noche_pct=80: -3,22 dB, 10*log2(0,8)): una de Spotify (la fuente de RocolaOs.SpotifyFuente.cs por ArmaCadena, 440 Hz) y una de XP (un MP3
# de 1000 Hz y 60 s en lugar de los de los datos de prueba), (1) cruzando las 11 de la noche a media cancion (una zona horaria de mentira
# hecha con zic, en la que la rocola arranca a las 22:59:30) y (2) con «Probar» (el boton de Ctrl+F3 -> Sonido, por el doble del catalogo:
# «noche-probar») de dia (la zona a las 12:00). Lo que sono, en la tarjeta de prueba con reloj; el nivel con niveles.py. La rampa de XP:
# medio volumen en 20 s (de 1 a 0,69: 12,4 s) al entrar la noche, y en 1,5 s con «Probar» (0,93 s), que dura 15 s y vuelve sola.
if [[ " $partes " == *" noche "* ]]; then
  echo "== noche (el modo noche de XP con una de Spotify y una de XP: de noche y con «Probar», con su rampa)"
  ZI="$W/zonas"; mkdir -p "$ZI"
  # zona NOMBRE HH:MM: una zona horaria en la que AHORA son las HH:MM (los segundos, los de UTC). Se llama justo antes de abrir la rocola.
  zona() {
    local n="$1" h="${2%:*}" m="${2#*:}" u off sg=""
    u=$(( 10#$(date -u +%H) * 60 + 10#$(date -u +%M) )); off=$(( (10#$h * 60 + 10#$m - u + 2880) % 1440 )); [ $off -gt 840 ] && off=$((off - 1440))
    [ $off -lt 0 ] && { sg=-; off=$((-off)); }
    echo "Zone $n $sg$((off / 60)):$(printf %02d $((off % 60))) - PRB" > "$ZI/$n.txt"; zic -d "$ZI" "$ZI/$n.txt" || return 1; echo "$ZI/$n"; }
  enelsegundo() { while [ "$(date -u +%S)" != "$1" ]; do sleep 0.2; done; }   # (para que las 23:00 caigan a los 30 s de abrir)
  [ -f "$W/tono1000.mp3" ] || ffmpeg -loglevel error -y -f lavfi -i "aevalsrc=0.25*sin(2*PI*1000*t)|0.25*sin(2*PI*1000*t):s=44100:d=60" -c:a libmp3lame -b:a 192k "$W/tono1000.mp3"
  for c in sp xp; do
    for m in noche probar; do
      d=$(datos noche-$m-$c)
      [ $c = xp ] && for f in "$d"/musica/*/*/*.mp3; do cp "$W/tono1000.mp3" "$f"; done
      if [ $m = noche ]; then enelsegundo 30; z=$(zona noche-$c 22:59); pide=""; fin=48; else z=$(zona dia-$c 12:00); pide="20:noche-probar"; fin=45; fi
      [ $c = sp ] && pide="9:pide spotify:track:f440d050aa|$P|50000${pide:+;$pide}"
      T0=$(date +%s.%N)
      if [ $c = sp ]; then abre noche-$m-$c "$d" "7:67" "$pide" $fin TERM ROCOLA_OS=spotify,spotify-prueba TZ="$z"
      else abre noche-$m-$c "$d" "7:67,9:13,10.5:13" "$pide" $fin TERM ROCOLA_OS=spotify,spotify-prueba TZ="$z"; fi
      hz=440; [ $c = xp ] && hz=1000
      python3 "$G/niveles.py" "$W/noche-$m-$c/sonado.raw" "$W/noche-$m-$c/registro.txt" $hz --serie > "$W/noche-$m-$c/niveles.txt"
      echo "   $m $c: $(head -1 "$W/noche-$m-$c/niveles.txt")"
    done
  done
  nv() { head -1 "$W/noche-$1/niveles.txt" | tr ' ' '\n' | sed -n "s/^$2=//p"; }
  for c in sp xp; do
    o="$W/noche-noche-$c"
    si "noche $c: sale con 0" test "$(cat $o/salio)" = 0
    si "noche $c: baja al 80 % de lo que se oye (-3,22 dB: $(nv noche-$c baja) dB)" entre "$(nv noche-$c baja)" -3.40 -3.05
    si "noche $c: con su rampa (12,4 s: $(nv noche-$c rampa) s), sin escalon" entre "$(nv noche-$c rampa)" 11.4 13.4
    o="$W/noche-probar-$c"
    si "probar $c: sale con 0" test "$(cat $o/salio)" = 0
    si "probar $c: «Probar» la baja al 80 % (-3,22 dB: $(nv probar-$c baja) dB)" entre "$(nv probar-$c baja)" -3.40 -3.05
    si "probar $c: con la rampa rapida (0,93 s: $(nv probar-$c rampa) s)" entre "$(nv probar-$c rampa)" 0.6 1.4
    si "probar $c: 15 s abajo ($(nv probar-$c abajo) s) y vuelve sola a lo de antes ($(nv probar-$c antes) -> $(nv probar-$c vuelve) dB, en $(nv probar-$c subida) s)" \
      bash -c "echo '$(nv probar-$c abajo) > 14.4 && $(nv probar-$c abajo) < 15.8 && $(nv probar-$c vuelve) - $(nv probar-$c antes) < 0.15 && $(nv probar-$c antes) - $(nv probar-$c vuelve) < 0.15 && $(nv probar-$c subida) > 0.5 && $(nv probar-$c subida) < 1.4' | bc | grep -q 1"
  done
  for m in noche probar; do
    db=$(echo "$(nv $m-sp baja) - $(nv $m-xp baja)" | bc); dr=$(echo "$(nv $m-sp rampa) - $(nv $m-xp rampa)" | bc)
    si "$m: la de Spotify baja igual que la de XP (lo que baja: $db dB de diferencia; la rampa: $dr s)" bash -c "echo '$db < 0.1 && $db > -0.1 && $dr < 0.6 && $dr > -0.6' | bc | grep -q 1"
  done
  si "noche sp: la de Spotify sono por Spotify (cobrada, por la fuente de RocolaOs.SpotifyFuente.cs)" bash -c "grep -q 'COBRADA spotify:track:f440d050aa' '$W/datos-noche-noche-sp/rocola_spotify.txt'"
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; fi
[ $mal = 0 ]
