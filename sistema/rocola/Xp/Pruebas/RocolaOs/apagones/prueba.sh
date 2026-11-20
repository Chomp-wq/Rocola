#!/bin/bash
# LA PRUEBA DEL MODO CONTRA APAGONES (fase 11, auditoria «apagones»; FASE11_apagones.md). rocola-xp de verdad, fuera de pantalla, con la
# tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c: guarda lo que DE VERDAD sono) y datos INVENTADOS: los de partida de las
# escenas de ventanas con las tres canciones de «Los Ejemplos - Disco Uno» cambiadas por tonos de 30 s que cambian a la mitad (la 1: 440 Hz
# los primeros 15 s y 470 Hz los otros 15; la 2: 550/580; la 3: 660/690), para saber desde DONDE vuelve a sonar la que sonaba.
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   escenas (de fabrica todas): kill9 segv sigterm usr1 hup inyecta strace importa
#   W: la carpeta de trabajo (/root/rocola-trabajo/fase11/apagones/prueba; se rehace); DATOS0 (/root/rocola-trabajo/fase11/apagones/datos0,
#   ver arriba; se copia, nunca se toca)
# Lo que XP hace (Rocola/Form1.Datos.cs, «ESTADO VIVO», y Form1.Video.cs, OnFormClosing) y se comprueba aqui:
#   - un apagon (kill -9) o una caida (SIGSEGV): no corre nada al cerrar -> rocola_estado.txt sobrevive y al volver: los mismos creditos, la misma cola
#     y la que sonaba vuelve a entrar y suena DESDE EL PRINCIPIO (XP: «a peticion: media cancion confunde mas de lo que ayuda»)
#   - SIGTERM (el apagado del sistema = WindowsShutDown) -> se borra (y su .bak y su .tmp): arranca limpia
#   - SIGUSR1 (los reinicios propios de Rocola OS = TaskManagerClosing, «cierre por otra causa») -> se conserva y al volver reanuda
#   - SIGHUP (no la atiende: el lanzador la ignora para sus hijos) -> como un apagon: se conserva
#   - un corte JUSTO mientras guarda (strace -e inject: SIGKILL en la escritura del de paso, en su fsync, en el cambio de nombre): al volver,
#     un estado entero (el de antes o el nuevo), nunca roto
#   - como guarda (strace): de paso, fsync del de paso, copia .bak por enlace y cambio de nombre (y si se baja la carpeta al disco)
#   - el importador de XP no trae el estado del dia ni pisa el de rocola-xp
# Cada comprobacion: BIEN o MAL en una linea; al final «TODO BIEN (N comprobaciones)» o cuantas MAL (y sale con 1).
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
partes="${*:-kill9 segv sigterm usr1 hup inyecta strace importa}"
W=${W:-/root/rocola-trabajo/fase11/apagones/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/fase11/apagones/datos0}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
A=$W/../alsa; mkdir -p "$A"
if [ ! -f "$A/libasound_module_pcm_reloj.so" ] || [ "$XP/Pruebas/Windows/alsa/reloj.c" -nt "$A/libasound_module_pcm_reloj.so" ]; then
  gcc -shared -fPIC -DPIC -O2 -o "$A/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1; fi

# abre NOMBRE DATOS "PULSA" SEGUNDOS SENAL [VAR=valor...] [-- PREFIJO...]: la rocola con esos datos y las teclas PULSA (ROCOLA_PULSA); a los
# SEGUNDOS, la SENAL (NADA: ninguna; espera a que salga sola). Deja en $W/NOMBRE: salida.txt, traza.txt, sonado.raw, registro.txt,
# frec.txt (frecuencias.py), salio (el codigo) y estado-N.txt (los ROCOLA_ESTADO_EN que se pidan con ESTADO="S:N,...").
abre() {
  local n="$1" d="$2" pulsa="$3" seg="$4" sen="$5"; shift 5
  local o="$W/$n"; mkdir -p "$o"
  local vars=() pre=()
  while [ $# -gt 0 ]; do if [ "$1" = -- ]; then shift; pre=("$@"); break; fi; vars+=("$1"); shift; done
  cat > "$o/alsa.conf" <<EOC
pcm_type.reloj { lib "$A/libasound_module_pcm_reloj.so" }
pcm.reloj { type reloj  file "$o/sonado.raw"  registro "$o/registro.txt" }
EOC
  local e="" x
  IFS=',' read -ra L <<< "${ESTADO:-}"; for x in "${L[@]}"; do [ -z "$x" ] && continue; e="$e,${x%%:*}:$o/estado-${x#*:}.txt"; done; e="${e#,}"
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf \
      ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_ESTADO_EN="$e" \
      "${vars[@]}" "${pre[@]}" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$!
  if [ "$sen" = NADA ]; then
    ( sleep "$seg"; kill -KILL $p 2>/dev/null ) & local v=$!
  else
    sleep "$seg"
    # (con strace delante, la senal va a la rocola, no a strace)
    local q; q=$(pgrep -P $p -f rocola-xp | head -1); [ -n "${pre[*]}" ] && [ -n "$q" ] && kill -"$sen" "$q" 2>/dev/null || kill -"$sen" $p 2>/dev/null
    [ "$sen" = SEGV ] && { sleep 1; kill -SEGV $p 2>/dev/null; }   # (el segundo: ver «segv»)
    ( sleep 40; kill -KILL $p 2>/dev/null; pkill -KILL -P $p 2>/dev/null ) & local v=$!
  fi
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
  [ -f "$o/sonado.raw" ] && python3 "$XP/Pruebas/RocolaOs/spotify-xp/frecuencias.py" "$o/sonado.raw" "$([ -f "$o/registro.txt" ] && echo "$o/registro.txt" || echo 44100)" > "$o/frec.txt"
  for f in rocola_estado.txt rocola_estado.txt.bak rocola_estado.txt.tmp; do [ -f "$d/$f" ] && cp "$d/$f" "$o/quedo-$f"; done
  return 0
}
datos() { local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/; rm -f "$d"/rocola_estado.txt*; echo "$d"; }
lineas() { tr -d '\r' < "$1" 2>/dev/null | grep -E '^(creditos|sonando|cola)=' ; }   # (las lineas del estado de XP, sin ts=)
val() { sed -n "s/^$2=//p" "$1" 2>/dev/null | head -1; }                               # (del estado de prueba, ROCOLA_ESTADO_EN)
oye() { [ -f "$1" ] || { echo 0.0; return; }; awk -v f="$2" '$3>=f-6 && $3<=f+6 {s+=$2-$1} END {printf "%.1f\n", s+0}' "$1"; }   # segundos que se oyo HZ
primero() { awk -v f="$2" '$3>=f-6 && $3<=f+6 && $2-$1>=0.3 {print $1; exit}' "$1" 2>/dev/null; }      # cuando empezo a oirse HZ
entre() { python3 -c "import sys; v=float(sys.argv[1]); sys.exit(0 if float(sys.argv[2]) <= v <= float(sys.argv[3]) else 1)" "$1" "$2" "$3"; }
antes_que() { local a b; a=$(primero "$1" "$2"); b=$(primero "$1" "$3"); [ -n "$a" ] && { [ -z "$b" ] || entre "$a" -1 "$b"; }; }

# LAS TECLAS (las de los datos de partida: moneda 67, poner 13, abajo 40): a los 7 s cuatro monedas; la pared abre el primer disco (Los
# Ejemplos - Disco Uno) y pide su cancion 1 (suena) y la 2 y la 3 (a la cola): cada moneda da 4 creditos (los datos de partida), quedan
# 16 - 3 = 13
PIDE="7:67,7.4:67,7.8:67,8.2:67,9:13,10.5:13,11.5:40,12:13,13:40,13.5:13"
como_esperado() { local l; l=$(lineas "$1"); [ "$(echo "$l" | wc -l)" = 4 ] && [[ "$(echo "$l" | sed -n 1p)" == "creditos=13" ]] \
  && [[ "$(echo "$l" | sed -n 2p)" == "sonando=S|"*"/01 Cancion 1.mp3" ]] && [[ "$(echo "$l" | sed -n 3p)" == "cola=S|"*"/02 Cancion 2.mp3" ]] \
  && [[ "$(echo "$l" | sed -n 4p)" == "cola=S|"*"/03 Cancion 3.mp3" ]]; }

# una corrida que se corta con SENAL a los S s (a media cancion 1: sono ~ S - 11 s) y otra que vuelve a abrir con lo que quedo
corta_y_vuelve() {
  local n="$1" sen="$2" s="$3" d; d=$(datos "$n")
  abre "$n-1" "$d" "$PIDE" "$s" "$sen"
  cp "$d/rocola_estado.txt" "$W/$n-quedo.txt" 2>/dev/null
  ESTADO="6:a,14:b" abre "$n-2" "$d" "" 40 TERM
  echo "$d"
}

# ---------------------------------------------------------------- kill -9 (el apagon) a media cancion
if [[ " $partes " == *" kill9 "* ]]; then
  echo "== kill9: kill -9 con la 1 sonando (a los 9 s de empezar) y la 2 y la 3 en la cola"
  d=$(corta_y_vuelve kill9 KILL 20)
  si "kill9: salio por la senal (137)" test "$(cat $W/kill9-1/salio)" = 137
  si "kill9: rocola_estado.txt sobrevivio con 13 creditos, la 1 sonando y la 2 y la 3 en la cola" como_esperado "$W/kill9-quedo.txt"
  si "kill9: la 1 sono a media cancion antes del corte (5 a 12 s de 440 Hz, nada de 470)" bash -c "$(declare -f entre oye); entre \$(oye '$W/kill9-1/frec.txt' 440) 5 12 && entre \$(oye '$W/kill9-1/frec.txt' 470) 0 0.2"
  si "kill9: al volver, 13 creditos y la cola de 2 (la 2 y la 3)" bash -c "[ '$(val $W/kill9-2/estado-a.txt creditos)' = 13 ] && [[ '$(val $W/kill9-2/estado-a.txt cola)' == 2*'Cancion 2'*'Cancion 3'* ]]"
  si "kill9: al volver suena la 1 (la que sonaba)" bash -c "[[ '$(val $W/kill9-2/estado-a.txt suena)' == *'Cancion 1'* ]]"
  si "kill9: y DESDE EL PRINCIPIO, como XP: sus 15 s de 440 Hz enteros antes de 470 Hz" bash -c "$(declare -f entre oye primero antes_que); entre \$(oye '$W/kill9-2/frec.txt' 440) 13.5 16 && antes_que '$W/kill9-2/frec.txt' 440 470"
  si "kill9: y despues la 2 (550 Hz), en su turno" bash -c "$(declare -f entre oye primero antes_que); antes_que '$W/kill9-2/frec.txt' 470 550 && entre \$(oye '$W/kill9-2/frec.txt' 550) 1 16"
  si "kill9: al cerrar la segunda con SIGTERM (el apagado) el estado se borro entero (ni .bak ni .tmp)" bash -c "[ ! -e '$d/rocola_estado.txt' ] && [ ! -e '$d/rocola_estado.txt.bak' ] && [ ! -e '$d/rocola_estado.txt.tmp' ]"
fi

# ---------------------------------------------------------------- una caida de verdad (SIGSEGV: no corre nada al cerrar)
# (un SIGSEGV mandado con kill: el primero lo recoge el manejador de .NET, que vuelve a poner el de fabrica y sigue; el segundo ya la tira,
# como un fallo de memoria de verdad -lo mismo se vio en QEMU el 14-11-2026: Pruebas/Qemu/prueba.py, «caida»-. SIGABRT tampoco la tira:
# visto aqui, siguio sonando hasta el SIGKILL de la prueba)
if [[ " $partes " == *" segv "* ]]; then
  echo "== segv: el programa se cae (SIGSEGV, dos veces) con la 1 sonando"
  d=$(corta_y_vuelve segv SEGV 20)
  si "segv: se cayo por la senal (139)" test "$(cat $W/segv-1/salio)" = 139
  si "segv: el estado sobrevivio como tras un apagon" como_esperado "$W/segv-quedo.txt"
  si "segv: al volver, 13 creditos, la 1 suena y la 2 y la 3 en la cola" bash -c "[ '$(val $W/segv-2/estado-a.txt creditos)' = 13 ] && [[ '$(val $W/segv-2/estado-a.txt suena)' == *'Cancion 1'* ]] && [[ '$(val $W/segv-2/estado-a.txt cola)' == 2* ]]"
fi

# ---------------------------------------------------------------- SIGTERM (el apagado del sistema)
if [[ " $partes " == *" sigterm "* ]]; then
  echo "== sigterm: el apagado del sistema con la 1 sonando"
  d=$(corta_y_vuelve sigterm TERM 20)
  si "sigterm: salio con 0" test "$(cat $W/sigterm-1/salio)" = 0
  si "sigterm: cerro con WindowsShutDown" grep -q "closed Form1'Rocola' WindowsShutDown" "$W/sigterm-1/traza.txt"
  si "sigterm: el estado se borro entero (ni rocola_estado.txt, ni .bak, ni .tmp)" bash -c "[ ! -e '$W/sigterm-quedo.txt' ] && [ ! -e '$W/sigterm-1/quedo-rocola_estado.txt.bak' ] && [ ! -e '$W/sigterm-1/quedo-rocola_estado.txt.tmp' ]"
  si "sigterm: al volver arranca limpia (0 creditos, nada sonando, cola vacia)" bash -c "[ '$(val $W/sigterm-2/estado-a.txt creditos)' = 0 ] && [ -z '$(val $W/sigterm-2/estado-a.txt suena)' ] && [ '$(val $W/sigterm-2/estado-a.txt cola)' = 0 ]"
  si "sigterm: y no suena nada" bash -c "$(declare -f entre oye); entre \$(oye '$W/sigterm-2/frec.txt' 440) 0 0.2"
fi

# ---------------------------------------------------------------- SIGUSR1 (los reinicios propios de Rocola OS)
if [[ " $partes " == *" usr1 "* ]]; then
  echo "== usr1: el cierre que conserva (consola de mantenimiento, reiniciar el programa, el buzon) con la 1 sonando"
  d=$(corta_y_vuelve usr1 USR1 20)
  si "usr1: salio con 0" test "$(cat $W/usr1-1/salio)" = 0
  si "usr1: cerro con TaskManagerClosing (cierre por otra causa)" grep -q "closed Form1'Rocola' TaskManagerClosing" "$W/usr1-1/traza.txt"
  si "usr1: el estado se conservo (13 creditos, la 1 sonando, la 2 y la 3)" como_esperado "$W/usr1-quedo.txt"
  si "usr1: al volver reanuda (13 creditos, la 1, cola de 2)" bash -c "[ '$(val $W/usr1-2/estado-a.txt creditos)' = 13 ] && [[ '$(val $W/usr1-2/estado-a.txt suena)' == *'Cancion 1'* ]] && [[ '$(val $W/usr1-2/estado-a.txt cola)' == 2* ]]"
fi

# ---------------------------------------------------------------- SIGHUP (la terminal se cuelga; el lanzador la ignora para sus hijos)
if [[ " $partes " == *" hup "* ]]; then
  echo "== hup: SIGHUP sin ignorar (la rocola no la atiende: es como un apagon) y con SIGHUP ignorada como la deja el lanzador (no hace nada)"
  d=$(corta_y_vuelve hup HUP 20)
  si "hup: salio por la senal (129)" test "$(cat $W/hup-1/salio)" = 129
  si "hup: el estado sobrevivio (como un apagon: nada corre al cerrar)" como_esperado "$W/hup-quedo.txt"
  d=$(datos hupign)
  abre hupign "$d" "$PIDE" 20 HUP -- bash -c 'trap "" HUP; exec "$@"' _
  si "hup: con SIGHUP ignorada (trap '' HUP, como /usr/bin/rocola) sigue abierta hasta el SIGKILL de la prueba" test "$(cat $W/hupign/salio)" = 137
fi

# ---------------------------------------------------------------- un corte JUSTO mientras guarda (strace -e inject)
# Cada guardado del estado: openat(.tmp, O_CREAT|O_TRUNC), write, fsync, close, unlink(.bak), link(estado, .bak), rename(.tmp, estado). Se
# mata la rocola (SIGKILL) en el N-esimo de una de esas llamadas sobre esos archivos (con error=EIO la llamada no se hace: el corte es justo
# antes; sin el, justo despues) y se vuelve a abrir: el estado que queda debe ser entero (el de antes o el de despues) y la rocola lo lee.
# (strace 6.8: sin --seccomp-bpf, que no entrega la senal inyectada; «when» cuenta por hilo, asi que es el N-esimo de algun hilo que guarda)
if [[ " $partes " == *" inyecta "* ]]; then
  echo "== inyecta: SIGKILL justo en la escritura, el fsync, el enlace de la copia y el cambio de nombre del estado"
  command -v strace >/dev/null || { echo "MAL   inyecta: falta strace"; mal=$((mal + 1)); }
  k=0
  for caso in "pwrite64:3:EIO" "fsync:3:EIO" "fsync:3:" "link:2:EIO" "link:2:" "rename:3:EIO" "rename:3:" "unlink:2:"; do
    sc=${caso%%:*}; r=${caso#*:}; nth=${r%%:*}; er=${r#*:}; k=$((k + 1)); n="iny$k"
    d=$(datos "$n"); st="$d/rocola_estado.txt"
    inj="inject=$sc:signal=SIGKILL:when=$nth${er:+:error=$er}"
    abre "$n" "$d" "$PIDE" 45 NADA -- strace -f -qq -o "$W/$n.strace" -P "$st" -P "$st.tmp" -P "$st.bak" -e trace=openat,pwrite64,write,fsync,fdatasync,link,linkat,rename,renameat,renameat2,unlink,unlinkat -e "$inj"
    killed=$(grep -c "+++ killed by SIGKILL" "$W/$n.strace" 2>/dev/null)
    cp "$st" "$W/$n-quedo.txt" 2>/dev/null; cp "$st.bak" "$W/$n-quedo.bak" 2>/dev/null
    ESTADO="8:a" abre "$n-v" "$d" "" 12 KILL
    # el estado que quedo: entero (cabecera, ts y creditos) y la rocola lo leyo (los mismos creditos)
    q="$W/$n-quedo.txt"; [ -s "$q" ] || q="$W/$n-quedo.bak"
    cr=$(tr -d '\r' < "$q" 2>/dev/null | sed -n 's/^creditos=//p')
    si "inyecta $sc (la $nth.a${er:+, sin hacerla}): la rocola murio ahi, el estado quedo entero (creditos=${cr:-?}) y al volver lo leyo igual" \
      bash -c "[ '${killed:-0}' -ge 1 ] && [ \"\$(tr -d '\\r' < '$q' | head -1)\" = 'ROCOLA-ESTADO v1' ] && tr -d '\\r' < '$q' | grep -q '^ts=' && [ -n '$cr' ] && [ '$(val $W/$n-v/estado-a.txt creditos)' = '$cr' ]"
  done
fi

# ---------------------------------------------------------------- como se guarda (strace, sin cortar)
if [[ " $partes " == *" strace "* ]]; then
  echo "== strace: las llamadas de cada guardado del estado (y si la carpeta se baja al disco)"
  d=$(datos strace); st="$d/rocola_estado.txt"
  abre strace "$d" "$PIDE" 18 KILL -- strace -f -qq --seccomp-bpf -o "$W/strace.strace" -e trace=openat,write,fsync,fdatasync,sync,syncfs,link,linkat,rename,renameat,renameat2,unlink,unlinkat
  s="$W/strace.strace"
  tmpfd=$(grep -E "openat\(.*rocola_estado\.txt\.tmp\".*O_CREAT" "$s" | head -1 | sed -E 's/.*= ([0-9]+)$/\1/')
  ren=$(grep -cE "rename(at2?)?\(.*rocola_estado\.txt\.tmp\", .*rocola_estado\.txt\"" "$s")
  fs=$(grep -cE "fsync\($tmpfd\)" "$s")
  dirfd=$(grep -E "openat\(.*\"$d\".*O_DIRECTORY" "$s" | wc -l)
  echo "  guardados (cambios de nombre): $ren; fsync del de paso: $fs; aperturas de la carpeta de datos: $dirfd"
  awk -v p="rocola_estado" 'index($0, p) || /fsync\(/' "$s" | grep -E "rocola_estado|fsync" | head -14 | sed 's/^/    /'
  si "strace: el estado se guarda con un de paso, fsync, copia .bak (link) y cambio de nombre" bash -c "[ '$ren' -ge 3 ] && [ '$fs' -ge 1 ] && grep -qE 'link(at)?\(.*rocola_estado\.txt\", .*rocola_estado\.txt\.bak\"' '$s'"
  # tras cada cambio de nombre, ¿se baja la carpeta (fsync de un descriptor de la carpeta de datos)? (en Windows el cambio de nombre lo
  # lleva el diario de NTFS; en Linux, POSIX pide fsync de la carpeta para que el nombre nuevo sobreviva a un corte de luz)
  python3 - "$s" "$d" > "$W/strace-carpeta.txt" <<'EOP'
import re, sys
s, d = sys.argv[1], sys.argv[2]
dirfds, ok, ren = {}, 0, 0
pend = {}
for l in open(s, errors="replace"):
    m = re.match(r"(\d+)\s+(.*)", l);
    if not m: continue
    pid, c = m.group(1), m.group(2)
    m2 = re.match(r'openat\(.*"' + re.escape(d) + r'".*O_DIRECTORY.*= (\d+)$', c)
    if m2: dirfds[(pid, m2.group(1))] = 1
    if re.match(r'rename(at2?)?\(.*rocola_estado\.txt\.tmp", .*rocola_estado\.txt"', c): ren += 1; pend[pid] = 1
    m3 = re.match(r'f(data)?sync\((\d+)\)', c)
    if m3 and pend.get(pid) and (pid, m3.group(2)) in dirfds: ok += 1; pend[pid] = 0
print(ren, ok)
EOP
  read -r nren nok < "$W/strace-carpeta.txt"
  echo "  cambios de nombre del estado: $nren; seguidos de fsync de la carpeta: $nok"
  si "strace: cada cambio de nombre del estado va seguido del fsync de su carpeta (sobrevive a un corte de luz)" bash -c "[ '$nren' -ge 3 ] && [ '$nok' = '$nren' ]"
fi

# ---------------------------------------------------------------- el importador de XP no trae el estado del dia ni pisa el de rocola-xp
if [[ " $partes " == *" importa "* ]]; then
  echo "== importa: --importa-xp con un estado del dia en la carpeta de XP (5 creditos) y otro en la de rocola-xp (13 creditos y su cola)"
  xpd="$W/xp-falso"; rm -rf "$xpd"; mkdir -p "$xpd"
  printf 'ROCOLA-ESTADO v1\r\nts=%s\r\ncreditos=5\r\nsonando=S|D:\\musica\\x.mp3\r\n' "$(python3 -c 'import time; print(int(time.time()*1e7)+621355968000000000)')" > "$xpd/rocola_estado.txt"
  cp "$xpd/rocola_estado.txt" "$xpd/rocola_estado.txt.bak"
  printf 'arriba=38\r\n' > "$xpd/rocola_teclas.txt"; : > "$xpd/Rocola.exe"
  d=$(datos importa); s0="$d/rocola_estado.txt"
  printf 'ROCOLA-ESTADO v1\r\nts=1\r\ncreditos=1\r\ncola=S|/x/y.mp3\r\n' > "$s0"; md5=$(md5sum < "$s0")
  ( cd "$d" && ROCOLA_DATOS="$d" "${RX[@]}" --importa-xp "$xpd" "$d/musica" > "$W/importa.txt" 2>&1; echo $? >> "$W/importa.txt" )
  si "importa: importo (salio con 0)" bash -c "tail -1 '$W/importa.txt' | grep -qx 0 && [ -f '$d/rocola_importado_xp.txt' ]"
  si "importa: el estado de rocola-xp quedo igual, byte a byte (no trajo el de XP ni lo piso)" bash -c "[ '$(md5sum < "$s0")' = '$md5' ] && [ ! -e '$s0.bak' ] && [ ! -e '$s0.antes-xp' ]"
fi

echo
if [ $mal -eq 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; exit 0; else echo "$mal MAL de $((bien + mal))"; exit 1; fi
