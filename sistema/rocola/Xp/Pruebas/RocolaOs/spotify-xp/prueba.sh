#!/bin/bash
# LA PRUEBA DEL NUCLEO DE SPOTIFY (fase 11, grupo spotify-xp; FASE11_spotify-xp.md). rocola-xp de verdad, fuera de pantalla, con la tarjeta
# de prueba con reloj (Pruebas/Windows/alsa/reloj.c: suena a su ritmo y guarda lo que DE VERDAD sono), un Soloist FALSO (SoloistFalso/: la
# API WebSocket de la documentacion de Soloist y el sonido de cada cancion en el anillo de I1, un tono con su firma) abierto por un vigilante
# falso (vigilante-falso.sh, en lugar de rocola-soloist), y el doble del catalogo (la funcion «spotify-prueba»: RocolaOs/RocolaOs.SpotifyPrueba.cs)
# que pide canciones de Spotify por I6 (RocolaOsSpotifyPide). Datos INVENTADOS (los de partida de las escenas de ventanas, DATOS0). NUNCA el
# Soloist de verdad.
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   escenas (de fabrica todas): apagado dos mezcla celular fallos apagon
#   W: la carpeta de trabajo (/root/rocola-trabajo/fase11/spotify-xp/prueba; se rehace); FALSO: el Soloist falso compilado (se compila si
#   falta, con el candado de lo pesado); DATOS0 (/root/rocola-trabajo/port/ventanas/datos0, se copian, nunca se tocan)
# Las cuentas del sonido: frecuencias.py (tramos de 50 ms con la frecuencia dominante). Cada cancion de Spotify de prueba lleva su tono en su
# id (spotify:track:fHZdSEG...): 440, 550, 660, 770, 880 Hz; el autoplay del falso, 990 Hz.
# Cada comprobacion: BIEN o MAL en una linea; al final «TODO BIEN (N comprobaciones)» o cuantas MAL (y sale con 1).
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
partes="${*:-apagado dos mezcla celular fallos apagon}"
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
if [ ! -f "$A/libasound_module_pcm_reloj.so" ]; then gcc -shared -fPIC -DPIC -O2 -o "$A/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1; fi

# ---------- una corrida ----------
# abre NOMBRE DATOS "PULSA" "PIDE" SEGUNDOS SENAL [VAR=valor...]: la rocola con esos datos, las teclas PULSA (ROCOLA_PULSA), los pedidos de
# Spotify PIDE («SEG:pide URI|TITULO|ARTISTA|DISCO|MS;...», al doble del catalogo), y a los SEGUNDOS la SENAL. Deja en $W/NOMBRE: salida.txt,
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
      mkdir -p "$o/run/rocola-xp/spotify"; echo "$l" >> "$o/run/rocola-xp/spotify/prueba-pide"; done ) &
  local q=$!
  sleep "$seg"; kill $q 2>/dev/null
  [ "$sen" != NADA ] && kill -"$sen" $p 2>/dev/null; local t0; t0=$(date +%s.%N)
  ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
  echo "$(date +%s.%N) - $t0" | bc > "$o/tardo"
  local hz; hz=$(sed -n 's/.* rate=\([0-9]*\) .*/\1/p' "$o/registro.txt" 2>/dev/null | tail -1)
  [ -f "$o/sonado.raw" ] && python3 "$G/frecuencias.py" "$o/sonado.raw" "${hz:-44100}" > "$o/frec.txt"
}
datos() { local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/; rm -f "$d/rocola_estado.txt"; echo "$d"; }
oye() { awk -v f="$2" '$3>=f-15 && $3<=f+15 {s+=$2-$1} END {printf "%.2f\n", s+0}' "$1"; }   # segundos que se oyo HZ
primero() { awk -v f="$2" '$3>=f-15 && $3<=f+15 && $2-$1>=0.3 {print $1; exit}' "$1"; }    # cuando empezo a oirse HZ (de verdad)
ultimo() { awk -v f="$2" '$3>=f-15 && $3<=f+15 && $2-$1>=0.3 {u=$2} END {print u+0}' "$1"; }
log() { echo "$1/run/rocola-xp/spotify/prueba-pide.log"; }
estado() { sed -n "s/^$2=//p" "$1/run/rocola-xp/spotify/estado" 2>/dev/null; }
T0=0
P="Prueba|Artista inventado|Disco inventado"

# ---------------------------------------------------------------- apagado: R1, sin el modo nada de Spotify
if [[ " $partes " == *" apagado "* ]]; then
  echo "== apagado (R1)"
  for v in sin uno; do
    d=$(datos apagado-$v); T0=$(date +%s.%N)
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

# ---------------------------------------------------------------- dos: dos de Spotify seguidas, de principio a fin
if [[ " $partes " == *" dos "* ]]; then
  echo "== dos (pedir, sonar entera, la siguiente, el autoplay pausado, el cierre)"
  d=$(datos dos); T0=$(date +%s.%N)
  abre dos "$d" "7:67" "9:pide spotify:track:f440d008aa|$P|8000;9.2:pide spotify:track:f550d006bb|$P|6000" 32 TERM ROCOLA_OS=spotify,spotify-prueba
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
  abre celular "$d" "7:67" "11:pide spotify:track:f440d020aa|$P|20000;23:pide spotify:track:f550d020bb|$P|20000;34:pide spotify:track:f880d010cc|$P|10000" 38 TERM \
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

# ---------------------------------------------------------------- fallos: no empieza, se corta el sonido, se cae Soloist
if [[ " $partes " == *" fallos "* ]]; then
  echo "== fallos (R4 y R9: el credito vuelve y la rocola sigue)"
  d=$(datos fallos); T0=$(date +%s.%N)
  abre fallos "$d" "7:67" "9:pide spotify:track:nx0001|$P|30000;27:pide spotify:track:f440d030aa|$P|30000;41:pide spotify:track:f550d030bb|$P|30000;58:pide spotify:track:f660d004cc|$P|4000" 66 TERM \
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

# ---------------------------------------------------------------- apagon: lo pagado de Spotify vuelve como credito (R5)
if [[ " $partes " == *" apagon "* ]]; then
  echo "== apagon (R5)"
  d=$(datos apagon); T0=$(date +%s.%N)
  abre apagon1 "$d" "7:67" "9:pide spotify:track:f440d030aa|$P|30000;9.2:pide spotify:track:f550d030bb|$P|30000" 14 KILL ROCOLA_OS=spotify,spotify-prueba
  cp "$d/rocola_estado.txt" "$W/apagon-estado.txt" 2>/dev/null
  si "apagon: el estado de XP guardado con la que suena y la de la cola, de Spotify, y 2 creditos" bash -c "grep -qx 'creditos=2' '$W/apagon-estado.txt' && grep -qF 'sonando=S|spotify:track:f440d030aa' '$W/apagon-estado.txt' && grep -qF 'cola=S|spotify:track:f550d030bb' '$W/apagon-estado.txt'"
  sleep 3
  si "apagon: el vigilante y el Soloist falso se fueron solos al morir la rocola" bash -c "grep -q 'murio el padre' '$W/apagon1/vigilante.txt' && ! pgrep -f 'soloist-falso.dll.*$W/apagon1' >/dev/null"
  T0=$(date +%s.%N)
  abre apagon2 "$d" "" "" 14 TERM ROCOLA_OS=spotify,spotify-prueba ROCOLA_ESTADO_EN="12:$W/apagon2-xp.txt"
  si "apagon: al volver, lo pagado de Spotify vuelve como credito (2 + 2 = 4)" contiene "$W/apagon2-xp.txt" "creditos=4"
  si "apagon: y no suena nada de Spotify" bash -c "python3 -c \"import sys; sys.exit(0 if $(oye "$W/apagon2/frec.txt" 440)+$(oye "$W/apagon2/frec.txt" 550) < 0.1 else 1)\""
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; fi
[ $mal = 0 ]
