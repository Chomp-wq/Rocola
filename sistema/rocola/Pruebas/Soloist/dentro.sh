#!/bin/sh
# (dentro de la raiz de prueba: lo corre prueba.sh) rocola-soloist, «spotify», las llaves en rocola-buzon y ROCOLA_OS en el lanzador, con
# un soloist FALSO y el BusyBox del sistema. Cada comprobacion dice «BIEN» o «MAL»; prueba.sh las cuenta. Los relojes del vigilante se
# acortan con sus variables de prueba (ROCOLA_SOLOIST_*); lo de verdad no cambia.
mount -t proc proc /proc
unset http_proxy https_proxy HTTP_PROXY HTTPS_PROXY ALL_PROXY all_proxy
P=$(cat /tmp/srv.puerto)
export PATH=/usr/bin:/bin ROCOLA_RUN=/run ROCOLA_XP_DATOS=/rocola/datos/xp ROCOLA_DISCO=/rocola/disco ROCOLA_DISCOS=/rocola/discos \
	ROCOLA_SYS_BLOCK=/fsys/block SOLOIST_URL=http://127.0.0.1:$P/soloist_release_x86_64.tar.gz ROCOLA_SPOTIFY_APRESOLVE=http://127.0.0.1:$P/apresolve \
	ROCOLA_SOLOIST_ESPERAS="1 2 3 4" ROCOLA_SOLOIST_SEGUIDA=8 ROCOLA_SOLOIST_ARRANQUE=6 ROCOLA_SOLOIST_USB=2 ROCOLA_SOLOIST_CADUCADO=3 \
	ROCOLA_SOLOIST_MIN_DISCO_KB=1024 ROCOLA_SOLOIST_MIN_DATOS_KB=512 ROCOLA_SOLOIST_TICK=1
echo "$P" > /tmp/ws.puerto
XPD=/rocola/datos/xp; AQ=/run/rocola-soloist; EST=$AQ/estado; BIT=$XPD/rocola_soloist.txt; SD=$XPD/soloist
DP=/rocola/disco/rocola-os/soloist; DD=$XPD/soloist-programa; SRVF=/srv/soloist_release_x86_64.tar.gz
LL=llaveFALSA_1234567890abcdef   # (inventada)
limpia() {
	killall rocola-soloist rocola 2>/dev/null; killall -KILL soloist sleep rocola-xp 2>/dev/null; sleep 0.3
	umount /rocola/disco 2>/dev/null; umount /rocola/datos 2>/dev/null
	rm -rf /run/* /tmp/soloist.* /tmp/xp.log /tmp/o.* /rocola/datos/* /rocola/datos/.[!.]* /rocola/disco /rocola/discos/* /srv/.modo /srv/*.tar.gz /srv/peticiones.log
	# (los datos, como en la rocola, en su propio sistema de archivos: datos.img; aqui uno en memoria)
	mount -t tmpfs -o size=64m tmpfs /rocola/datos; mkdir -p "$XPD" /rocola/disco
}
ok() { if [ "$1" = "$2" ]; then echo "  BIEN: $3 [$1]"; else echo "  MAL: $3 [sale «$1», se esperaba «$2»]"; fi; }
n() { c=$(grep -c -- "$1" "${2:-$BIT}" 2>/dev/null); echo "${c:-0}"; }
si() { if [ "$@" ]; then echo si; else echo no; fi; }
es() { sed -n "s/^$1=//p" "$EST" 2>/dev/null | head -1; }
ms() { awk '{printf "%d", $1 * 1000}' /proc/uptime; }
bit() { sed 's/^/    bitacora: /' "$BIT" 2>/dev/null | tail -n "${1:-8}"; }
llave() { printf '%s\n' "${1:-$LL}" > "$XPD/rocola_spotify_llave.txt"; chmod 600 "$XPD/rocola_spotify_llave.txt"; }
api() { printf 'client_id=idFALSO123\nclient_secret=secretoFALSO_abcdef\n' > "$XPD/rocola_spotify_api.txt"; chmod 600 "$XPD/rocola_spotify_api.txt"; }
disco() { mount -t tmpfs -o "size=${1:-64m}" tmpfs /rocola/disco; mkdir -p /rocola/disco/rocola-os; }
# srv TAR [EDAD_DIAS]: el servidor de mentira sirve ese .tar.gz, con la fecha de hace tantos dias (0: ahora)
srv() { cp "/tars/$1.tar.gz" "$SRVF"; touch -d "@$(( $(date +%s) - ${2:-0} * 86400 ))" "$SRVF"; }
# lanza [VAR=valor...]: el vigilante como lo abre rocola-xp (aqui su padre es un sh que solo espera); /tmp/o.padre, /tmp/o.vig
lanza() { ( [ $# -gt 0 ] && export "$@"; exec sh -c 'rocola-soloist corre >/dev/null 2>&1 & echo $! > /tmp/o.vig; wait' ) & echo $! > /tmp/o.padre; sleep 0.3; }
# espera_fase FASE SEGUNDOS: hasta que el estado diga esa fase (0) o pase el tiempo (1)
espera_fase() { i=0; while [ "$(es fase)" != "$1" ] && [ $i -lt $(( $2 * 5 )) ]; do sleep 0.2; i=$((i + 1)); done; [ "$(es fase)" = "$1" ]; }
# los soloist (falsos) en marcha: cuantos procesos con «-D» en su linea de ordenes
# (sin los que ya acabaron y esperan a que los recojan: zombis de la prueba, que es el proceso 1 de su espacio)
vivo_p() { [ "$(awk '{print $3}' /proc/$1/stat 2>/dev/null)" != Z ] && [ -d /proc/$1 ]; }
soloists() { c=0; for f in /proc/[0-9]*/cmdline; do p=${f#/proc/}; p=${p%/cmdline}; vivo_p $p || continue; tr '\0' '\n' < "$f" 2>/dev/null | grep -qx -- "$SD" && tr '\0' '\n' < "$f" 2>/dev/null | grep -qx -- -n && c=$((c + 1)); done; echo $c; }
# los vigilantes (sin contar sus subprocesos, que tambien se llaman rocola-soloist: los de la tuberia de la bitacora)
vigilantes() { c=0; for p in $(pidof rocola-soloist); do vivo_p $p || continue; pp=$(awk '{print $4}' /proc/$p/stat 2>/dev/null); [ "$(cat /proc/$pp/comm 2>/dev/null)" = rocola-soloist ] || c=$((c + 1)); done; echo $c; }
# las veces que arranco el soloist falso, y los segundos (enteros, hacia abajo) entre una salida y el siguiente arranque: la espera mas lo que
# tarda el vigilante en notar la caida (hasta 0,5 s) y en arrancarlo (revision: antes se redondeaba, y salia 1 s de mas segun quien ganaba
# la carrera entre la salida del soloist falso y la primera mirada del vigilante)
arranques() { grep -c 'args: -n' /tmp/soloist.log 2>/dev/null || echo 0; }
esperas() { grep -E 'args: -n|sale con' /tmp/soloist.log | sed 's/.*\[\([0-9]*\)\] \([a-z]*\).*/\1 \2/' | awk '$2 == "sale" {s = $1} $2 == "args" {if (n++) printf "%s%d", (n > 2 ? " " : ""), ($1 - s) / 1000}'; }
# cuanto tarda (ms) en no quedar ni el vigilante ni su soloist
hasta_que_no_quede() { t0=$(ms); i=0; while { [ "$(vigilantes)" != 0 ] || [ "$(soloists)" != 0 ]; } && [ $i -lt 150 ]; do sleep 0.1; i=$((i + 1)); done; echo $(( $(ms) - t0 )); }
limpia
case "$1" in
baja)   # bajar bien: al disco de la rocola; despues solo si hay uno mas nuevo (If-Modified-Since); el de antes como anterior
	disco; srv v1 2
	rocola-soloist baja > /tmp/o.1; r=$?; cat /tmp/o.1 | sed 's/^/    /'
	ok "$r/$(si -s $DP/soloist.tar.gz)/$(cmp -s $DP/soloist.tar.gz /tars/v1.tar.gz && echo igual)" "0/si/igual" "baja el programa al disco de la rocola"
	ok "$(date -r $DP/soloist.tar.gz +%s)" "$(date -r $SRVF +%s)" "con la fecha del servidor (curl -R)"
	ok "$(sed -n 's/^version=//p' $DP/soloist.info)/$(sed -n 's/^fuente=//p' $DP/soloist.info)" "1.0.0/version" "su version y de donde salio la fecha (soloist.info)"
	ok "$(si -x $AQ/bin/soloist)/$(si -e $DP/soloist.tar.gz.t)" "si/no" "sacado a /run (en memoria) y sin el .t"
	rocola-soloist baja > /tmp/o.2; r=$?
	ok "$r/$(tail -1 /srv/peticiones.log)/$(n 'ya es el ultimo')" "0/GET /soloist_release_x86_64.tar.gz (If-Modified-Since)/1" "otra vez: pregunta con su fecha y no baja nada"
	ok "$(si -e $DP/soloist.anterior.tar.gz)" no "sin uno nuevo, no hay anterior"
	srv v2 0; rocola-soloist baja > /tmp/o.3; r=$?; sed 's/^/    /' /tmp/o.3
	ok "$r/$(cmp -s $DP/soloist.tar.gz /tars/v2.tar.gz && echo nuevo)/$(cmp -s $DP/soloist.anterior.tar.gz /tars/v1.tar.gz && echo anterior)" "0/nuevo/anterior" "uno mas nuevo: se baja y el de antes queda como anterior"
	rocola-soloist estado > /tmp/o.4; sed 's/^/    /' /tmp/o.4
	ok "$(grep -c 'version 2.0.0' /tmp/o.4)/$(grep -c 'quedan' /tmp/o.4)" "1/1" "«estado» dice la version y cuantos dias le quedan" ;;
caido)   # el servidor de Spotify falla o no contesta: se sigue con el que habia; sin ninguno, fase sin_programa
	disco; srv v1 2; rocola-soloist baja >/dev/null
	echo caido > /srv/.modo; srv v2 0
	rocola-soloist baja > /tmp/o.1; r=$?; sed 's/^/    /' /tmp/o.1
	ok "$r/$(cmp -s $DP/soloist.tar.gz /tars/v1.tar.gz && echo el_de_antes)/$(si -e $DP/soloist.tar.gz.t)" "1/el_de_antes/no" "HTTP 503: no baja, se queda el que habia y sin el .t"
	ok "$(n 'HTTP 503')" 1 "y lo apunta"
	rm -f /srv/.modo
	SOLOIST_URL=http://127.0.0.1:1/soloist_release_x86_64.tar.gz rocola-soloist baja >/dev/null; r=$?
	ok "$r/$(n 'curl 7')/$(cmp -s $DP/soloist.tar.gz /tars/v1.tar.gz && echo el_de_antes)" "1/1/el_de_antes" "nadie escucha (curl 7): igual"
	limpia; disco; llave; echo caido > /srv/.modo; srv v1 0
	lanza; espera_fase sin_programa 20; r=$?   # (curl lo reintenta 2 veces, a los 5 s)
	ok "$r/$(es fase)/$(soloists)" "0/sin_programa/0" "sin programa y sin poderlo bajar: fase sin_programa, nada en marcha"
	ok "$(es error | grep -c 'HTTP 503')" 1 "el estado dice por que" ;;
roto)   # lo bajado no vale: un tar cortado o sin «soloist»: no se pone
	disco; srv v1 2; rocola-soloist baja >/dev/null
	srv roto 0; rocola-soloist baja > /tmp/o.1; r=$?
	ok "$r/$(cmp -s $DP/soloist.tar.gz /tars/v1.tar.gz && echo el_de_antes)/$(si -e $DP/soloist.tar.gz.t)/$(si -e $DP/soloist.anterior.tar.gz)" "1/el_de_antes/no/no" "tar cortado: se queda el que habia"
	srv sinsoloist 0; rocola-soloist baja >/dev/null; r=$?
	ok "$r/$(cmp -s $DP/soloist.tar.gz /tars/v1.tar.gz && echo el_de_antes)/$(n 'no trae «soloist»')" "1/el_de_antes/2" "tar sin soloist: igual (y lo apunta)" ;;
lleno)   # el disco de la rocola sin sitio: a los datos; sin sitio ni ahi (o falla al escribir): no se pone nada a medias
	disco 900k; srv v1 0
	rocola-soloist baja >/dev/null; r=$?
	ok "$r/$(si -s $DD/soloist.tar.gz)/$(si -e $DP/soloist.tar.gz)" "0/si/no" "con menos de lo minimo libre en el disco: a /rocola/datos/xp/soloist-programa"
	limpia; disco 1200k; srv grande 0
	rocola-soloist baja > /tmp/o.1; r=$?; sed 's/^/    /' /tmp/o.1; bit 2
	ok "$r/$(si -e $DP/soloist.tar.gz)/$(si -e $DP/soloist.tar.gz.t)/$(n 'curl 23')" "1/no/no/1" "se llena al escribir (curl 23): nada a medias" ;;
sindisco)   # sin el disco de la rocola, en solo lectura o «sin revisar» (un apagon): a los datos; y de ahi arranca
	srv v1 0
	rocola-soloist baja >/dev/null; ok "$(si -s $DD/soloist.tar.gz)" si "sin disco: a los datos"
	limpia; disco; mount -o remount,ro /rocola/disco; srv v1 0
	rocola-soloist baja >/dev/null; ok "$(si -s $DD/soloist.tar.gz)" si "disco en solo lectura: a los datos"
	limpia; disco; touch /run/rocola-disco-revisar; srv v1 0
	rocola-soloist baja >/dev/null; ok "$(si -s $DD/soloist.tar.gz)/$(si -e $DP)" "si/no" "disco «sin revisar»: no se escribe en el, a los datos"
	llave; lanza; espera_fase corriendo 8; r=$?
	ok "$r/$(es programa)/$(soloists)" "0/$DD/soloist.tar.gz/1" "y arranca desde ahi" ;;
caducado)   # sale con 10: se baja el ultimo y arranca; si no hay uno nuevo, fase caducado y se reintenta
	disco; llave; srv v1 1; rocola-soloist baja >/dev/null
	echo 1.0.0 > /tmp/soloist.caducadas; srv v2 1
	lanza; espera_fase corriendo 15; r=$?; bit 6
	ok "$r/$(es version)/$(grep -c 'soloist 1.0.0.*sale con 10' /tmp/soloist.log)/$(grep -c 'soloist 2.0.0.*WebSocket abierto' /tmp/soloist.log)" "0/2.0.0/1/1" "el 1.0.0 sale con 10, se baja el 2.0.0 y queda en marcha"
	ok "$(n 'caduco (salio con 10)')/$(cmp -s $DP/soloist.anterior.tar.gz /tars/v1.tar.gz && echo anterior)" "1/anterior" "lo apunta, y el viejo queda como anterior"
	echo 2.0.0 >> /tmp/soloist.caducadas; killall -KILL soloist
	espera_fase caducado 10; r=$?; sleep 1
	ok "$r/$(es error | grep -c 'no hay uno nuevo')/$(es dias)" "0/1/-1" "caducado y sin uno nuevo: fase caducado, lo dice"
	sleep 5; ok "$(grep -c 'soloist 2.0.0.*sale con 10' /tmp/soloist.log)" 2 "y se vuelve a intentar a los ROCOLA_SOLOIST_CADUCADO s (aqui 3)"
	ok "$(tail -n 1 /srv/peticiones.log)" "GET /soloist_release_x86_64.tar.gz (If-Modified-Since)" "la segunda vez solo pregunta si hay uno mas nuevo (no baja los 14 MB cada vez)" ;;
pronto)   # caduca en menos de 7 dias: antes de arrancar se busca uno nuevo; sin fecha en --version, la del archivo + 90 dias
	disco; llave; srv v4 1; rocola-soloist baja >/dev/null
	ok "$(sed -n 's/^caduca=//p' $DP/soloist.info)" "$(date -d "@$(( $(date +%s) + 3 * 86400 ))" +%Y-%m-%d)" "la 4.0.0 caduca en 3 dias (por su fecha de compilacion)"
	srv v2 0; lanza ROCOLA_SOLOIST_REINTENTO=0; espera_fase corriendo 10; r=$?; sleep 2.5; bit 5
	ok "$r/$(es version)/$(grep -c 'soloist 4.0.0.*args: -n' /tmp/soloist.log)/$(es dias)" "0/2.0.0/0/89" "se baja la 2.0.0 antes de arrancar (y sus dias, de lo que dice al arrancar)"
	limpia; disco; llave; touch /tmp/soloist.sin_fecha; srv v1 100; rocola-soloist baja >/dev/null
	ok "$(sed -n 's/^fuente=//p' $DP/soloist.info)/$(sed -n 's/^caduca=//p' $DP/soloist.info)" "bajada/$(date -d "@$(( $(date -r $SRVF +%s) + 90 * 86400 ))" +%Y-%m-%d)" "sin fecha en --version: la del archivo bajado mas 90 dias"
	srv v2 0; lanza ROCOLA_SOLOIST_REINTENTO=0; espera_fase corriendo 10; r=$?
	ok "$r/$(es version)" "0/2.0.0" "y como ya caduco por esa cuenta, se busca el nuevo" ;;
caidas)   # se cae una y otra vez: 1, 2, 3, 4, 4 s (en la rocola 2 s, 10 s, 30 s, 2 min); tras ROCOLA_SOLOIST_SEGUIDA en pie, otra vez 1
	disco; llave; srv v2 0; rocola-soloist baja >/dev/null
	echo "sale 3" > /tmp/soloist.modo; lanza; sleep 17
	ok "$(esperas | cut -d' ' -f1-5)" "1 2 3 4 4" "segundos entre una caida y el siguiente arranque"
	ok "$(n 'Soloist salio con 3' | awk '{print ($1 >= 5)}')/$(es reinicios | awk '{print ($1 >= 5)}')" "1/1" "cada caida en la bitacora y en el estado"
	rm -f /tmp/soloist.modo; espera_fase corriendo 8; sleep 9; : > /tmp/soloist.log; killall -KILL soloist; sleep 3
	ok "$(esperas)" "" "(nada antes)"
	ok "$(grep -c 'args: -n' /tmp/soloist.log)/$(grep 'Soloist salio con' $BIT | tail -1 | grep -o 'en [0-9]* s$')" "1/en 1 s" "en pie mas de 8 s y se cae: otra vez 1 s" ;;
lento)   # arranca despacio: arrancando y despues corriendo; si nunca abre su WebSocket, se vuelve a arrancar
	disco; llave; srv v2 0; rocola-soloist baja >/dev/null
	echo "lento 3" > /tmp/soloist.modo; lanza; sleep 1.5
	ok "$(es fase)" arrancando "a 1,5 s, arrancando"
	espera_fase corriendo 6; ok "$? $(es pid | grep -c .)" "0 1" "a los 3 s abre su WebSocket: corriendo, con su pid"
	limpia; disco; llave; srv v2 0; rocola-soloist baja >/dev/null
	echo sin_ws > /tmp/soloist.modo; lanza ROCOLA_SOLOIST_ARRANQUE=3; mx=0
	for i in $(seq 20); do c=$(soloists); [ "$c" -gt $mx ] && mx=$c; sleep 0.5; done
	ok "$(arranques | awk '{print ($1 >= 2)}')/$(n 'no abrio su WebSocket en 3 s' | awk '{print ($1 >= 1)}')/$mx" "1/1/1" "sin WebSocket en 3 s: se cierra y se vuelve a arrancar (nunca dos a la vez)" ;;
dos)   # dos vigilantes a la vez: uno solo tiene a Soloist; el otro espera y toma el relevo
	disco; llave; srv v2 0; rocola-soloist baja >/dev/null
	lanza; espera_fase corriendo 6; a=$(cat /tmp/o.vig)
	( exec sh -c 'rocola-soloist corre >/dev/null 2>&1 & echo $! > /tmp/o.vig2; wait' ) & sleep 3
	ok "$(vigilantes)/$(soloists)/$(n 'este espera a que acabe')/$(es vigilante)" "2/1/1/$a" "el segundo espera; un solo Soloist"
	kill -TERM "$a"; i=0; while [ "$(es vigilante)" = "$a" ] || [ "$(es fase)" != corriendo ]; do sleep 0.2; i=$((i + 1)); [ $i -gt 50 ] && break; done
	ok "$(es fase)/$(es vigilante)/$(soloists)/$(vigilantes)" "corriendo/$(cat /tmp/o.vig2)/1/1" "el primero se cierra: el segundo toma el relevo" ;;
padre)   # muere su padre (la rocola, con SIGKILL): el vigilante lo nota (cada 2 s) y se cierra con Soloist
	disco; llave; srv v2 0; rocola-soloist baja >/dev/null
	lanza; espera_fase corriendo 6
	kill -KILL "$(cat /tmp/o.padre)"; t=$(hasta_que_no_quede)
	ok "$(awk -v t=$t 'BEGIN {print (t < 4000) ? "menos de 4 s" : t " ms"}')/$(n 'murio su padre')/$(es fase)" "menos de 4 s/1/parado" "todo fuera ($t ms)" ;;
sigterm)   # SIGTERM (la rocola apaga el modo o se cierra): todo fuera en menos de 10 s, medido; tambien con un Soloist que no hace caso
	disco; llave; srv v2 0; rocola-soloist baja >/dev/null
	lanza; espera_fase corriendo 6; kill -TERM "$(cat /tmp/o.vig)"; t=$(hasta_que_no_quede)
	ok "$(awk -v t=$t 'BEGIN {print (t < 10000) ? "menos de 10 s" : t " ms"}')/$(grep -c 'SIGTERM: sale con 0' /tmp/soloist.log)/$(es fase)" "menos de 10 s/1/parado" "SIGTERM: Soloist recibe SIGTERM y todo fuera ($t ms)"
	ok "$(si -e $SD/ws.port)" no "sin ws.port viejo"
	echo terco > /tmp/soloist.modo; lanza; espera_fase corriendo 6; kill -TERM "$(cat /tmp/o.vig)"; t=$(hasta_que_no_quede)
	ok "$(awk -v t=$t 'BEGIN {print (t < 10000 && t >= 7500) ? "entre 7,5 y 10 s" : t " ms"}')/$(n 'SIGKILL')" "entre 7,5 y 10 s/1" "Soloist no hace caso: SIGKILL a los 8 s, todo fuera ($t ms)"
	limpia; disco; llave; echo "lento 30" > /srv/.modo; srv v2 0
	lanza; espera_fase bajando 6; kill -TERM "$(cat /tmp/o.vig)"; t=$(hasta_que_no_quede)
	ok "$(awk -v t=$t 'BEGIN {print (t < 3000) ? "menos de 3 s" : t " ms"}')/$(si -e $DP/soloist.tar.gz.t)" "menos de 3 s/no" "SIGTERM mientras baja: se corta la bajada ($t ms)" ;;
usb)   # la llave desde una memoria USB: en otro disco, en una USB removible de verdad (loop), mal escrita; y rocola-buzon
	disco; srv v2 0; rocola-soloist baja >/dev/null
	mkdir -p /rocola/discos/sdz1; printf 'llaveDISCO_abcdef123456\r\n' > /rocola/discos/sdz1/rocola_spotify.txt
	lanza; espera_fase corriendo 8; r=$?
	ok "$(n 'falta la llave de Soloist')" 1 "sin llave: lo dice (fase sin_llave) y la busca"
	ok "$r/$(cat $XPD/rocola_spotify_llave.txt)/$(ls -l $XPD/rocola_spotify_llave.txt | cut -c1-10)" "0/llaveDISCO_abcdef123456/-rw-------" "la encuentra sola (cada ROCOLA_SOLOIST_USB s), 0600, y arranca"
	ok "$(n 'Llave de Spotify puesta (de USB)')" 1 "y lo apunta sin la llave"
	limpia
	if [ -b /dev/loop99 ]; then
		rocola-soloist llave > /tmp/o.1; r=$?; sed 's/^/    /' /tmp/o.1
		ok "$r/$(cat $XPD/rocola_spotify_llave.txt)" "0/llaveUSB_soloist_ABCdef123456" "USB removible (FAT, CRLF y marca de UTF-8): la de Soloist, limpia"
		ok "$(tr '\n' '|' < $XPD/rocola_spotify_api.txt)/$(ls -l $XPD/rocola_spotify_api.txt | cut -c1-10)" "client_id=idUSB0123456789|client_secret=secretoUSB_9876543210|refresh_token=refrescoUSB_555|/-rw-------" "y la de la API web, solo sus tres claves, 0600"
		ok "$(grep -c rocola-soloist/usb /proc/mounts)/$(ls -a $AQ | grep -c '^\.usb')" "0/0" "la USB se desmonta y no queda copia en /run"
		rocola-soloist llave > /tmp/o.2; r=$?
		ok "$r/$(grep -c 'la misma' /tmp/o.2)" "1/2" "otra vez: son las mismas, no cambia nada"
		echo 'llaveVIEJA_desde_otra_vez' > $AQ/.usb.rocola_spotify.txt; rm -f $XPD/rocola_spotify_llave.txt
		mkdir -p /mnt; mount -o ro /dev/loop99 /mnt 2>/dev/null; rocola-soloist llave > /tmp/o.5; r=$?; umount /mnt 2>/dev/null
		ok "$r/$(si -e $XPD/rocola_spotify_llave.txt)/$(si -e $AQ/.usb.rocola_spotify.txt)" "2/no/no" "una copia de paso que quedo de otra vez (y la USB ya montada por otro): no cuenta como encontrada"
	else echo "  MAL: no hay loop: la USB removible no se probo"; fi
	limpia; mkdir -p /rocola/discos/sdz1; echo 'esto no es una llave!' > /rocola/discos/sdz1/rocola_spotify.txt
	rocola-soloist llave > /tmp/o.3; r=$?; sed 's/^/    /' /tmp/o.3
	ok "$(si -e $XPD/rocola_spotify_llave.txt)/$(grep -c 'no parece' /tmp/o.3)" "no/1" "una que no parece llave: no se pone, y lo dice"
	limpia; mkdir -p /rocola/discos/sdz1 /rocola/datos; llave llaveVIEJA_000000000
	printf 'llaveNUEVA_1111111111\n' > /rocola/discos/sdz1/rocola_spotify.txt
	printf 'client_id=idB\nclient_secret=secB_222222\n' > /rocola/discos/sdz1/rocola_spotify_api.txt
	ROCOLA_DATOS=/rocola/datos rocola-buzon una > /tmp/o.4 2>&1
	ok "$(cat $XPD/rocola_spotify_llave.txt)/$(tr '\n' '|' < $XPD/rocola_spotify_api.txt)" "llaveNUEVA_1111111111/client_id=idB|client_secret=secB_222222|" "rocola-buzon: la de la USB es otra: se cambia (y la de la API)"
	ok "$(n 'Llave de Spotify puesta (de USB)' /rocola/datos/rocola_actualizaciones.txt)/$(grep -c 'llaveNUEVA\|secB' /rocola/datos/rocola_actualizaciones.txt)" "1/0" "apunta solo «Llave de Spotify puesta (de USB)»"
	ok "$(ls -l $XPD/rocola_spotify_llave.txt $XPD/rocola_spotify_api.txt | cut -c1-10 | tr '\n' ' ')/$(ls -a $XPD /run/rocola-buzon.d | grep -c -e '\.n$' -e '^\.api' -e '^llave.txt$')" "-rw------- -rw------- /0" "las dos 0600 y sin copias de paso (ni en los datos ni en /run)"
	sed -n '/^permitido() {/,/^}/p' /usr/bin/rocola-buzon > /tmp/o.perm; . /tmp/o.perm
	r=; for f in rocola_spotify_llave.txt rocola_spotify_api.txt rocola_spotify_llave.txt.bak soloist .rocola_spotify_llave.txt.n; do permitido "$f" && r="$r $f"; done
	ok "${r:-ninguno}" ninguno "rocola-buzon nunca sube las llaves ni la sesion de Soloist"
	r=; for f in rocola_soloist.txt rocola_spotify.txt rocola_soloist.txt.1 rocola_os.txt; do permitido "$f" || r="$r $f"; done
	ok "${r:-todas}" todas "y si las bitacoras (sin secretos)" ;;
secreto)   # la llave NUNCA aparece fuera de su archivo: ni en la bitacora, ni en el estado, ni en lo que dicen las ordenes
	disco; llave; api; srv v2 0; rocola-soloist baja > /tmp/o.s1 2>&1
	lanza; espera_fase corriendo 8; sleep 2
	ok "$(grep -c "\-k $LL" /tmp/soloist.log)/$(grep -c 'la llave es' /tmp/soloist.log)" "1/0" "Soloist la recibe con -k (y la linea suya que la lleva...)"
	ok "$(n 'la llave es')/$(n 'arrancando')" "0/1" "...no llega a la bitacora (lo demas que dice, si)"
	ok "$(n 'llave recortada')/$(n 'llaveFALSA_12')" "0/0" "ni una linea con un trozo de ella (sus primeras 12 letras)"
	rocola-soloist estado > /tmp/o.s2 2>&1; spotify estado > /tmp/o.s3 2>&1; spotify prueba > /tmp/o.s4 2>&1; spotify > /tmp/o.s5 2>&1
	spotify encender > /tmp/o.s6 2>&1
	printf '%s\n' "$LL" | rocola-soloist pon-llave > /tmp/o.s7 2>&1
	kill -TERM "$(cat /tmp/o.vig)"; hasta_que_no_quede >/dev/null
	c=$(grep -rl -e "$LL" -e secretoFALSO /run /tmp/o.s* "$BIT" "$XPD/rocola_os.txt" 2>/dev/null | tr '\n' ' ')
	ok "${c:-en ninguno}" "en ninguno" "ni la llave de Soloist ni el secreto de la API en /run, la bitacora, el estado ni lo que dicen las ordenes"
	ok "$(ls -l $XPD/rocola_spotify_llave.txt | cut -c1-10)/$(cat $XPD/rocola_spotify_llave.txt)" "-rw-------/$LL" "solo en su archivo, 0600" ;;
encender)   # «spotify encender» y «apagar»: con y sin rocola_os.txt (el formato del andamio), con .bak, con la rocola abierta
	ok "$(spotify | grep -c 'Ahora: encendido')/$(spotify | grep -c 'de fabrica va encendido')" "1/1" "sin rocola_os.txt: encendido (de fabrica, decision del 15-11-2026), y la ayuda lo dice"
	printf 'spotify=x\n' > $XPD/rocola_os.txt; ok "$(spotify | grep -c 'Ahora: encendido')" 1 "con un valor que no vale (spotify=x): el de fabrica, encendido (como RocolaOs.Ajustes.cs)"
	rm -f $XPD/rocola_os.txt
	spotify encender > /tmp/o.1; r=$?; sed 's/^/    /' /tmp/o.1
	ok "$r/$(cat $XPD/rocola_os.txt)/$(grep -c 'Ctrl+Alt+F1' /tmp/o.1)" "0/spotify=1/1" "sin el archivo: se crea solo con esa linea, y dice cuando vale"
	ok "$(grep -c 'Falta la llave de Soloist' /tmp/o.1)/$(grep -c 'Falta la llave de la API web' /tmp/o.1)/$(grep -c 'Falta emparejar' /tmp/o.1)" "1/1/1" "y avisa de lo que falta (las llaves, emparejar)"
	spotify apagar >/dev/null; ok "$(cat $XPD/rocola_os.txt)/$(cat $XPD/rocola_os.txt.bak)" "spotify=0/spotify=1" "apagar: spotify=0 (el de antes, .bak)"
	ok "$(spotify | grep -c 'Ahora: apagado')" 1 "y despues de apagar, la ayuda dice apagado"
	printf '\357\273\277# comentario\r\nred=1\r\n spotify = 0\r\nfuturo=x y z\r\nspotify=0\r\n' > $XPD/rocola_os.txt
	spotify encender >/dev/null
	ok "$(tr '\n' '|' < $XPD/rocola_os.txt)" "# comentario|red=1|spotify=1|futuro=x y z|" "con otras claves, CRLF, marca y repetidas: en su sitio, las demas igual, sin repetir"
	rm -f $XPD/rocola_os.txt; printf 'red=0\nspotify=0\n' > $XPD/rocola_os.txt.bak
	spotify encender >/dev/null; ok "$(tr '\n' '|' < $XPD/rocola_os.txt)" "red=0|spotify=1|" "solo el .bak (lo que leeria la rocola): se parte de el"
	touch $XPD/rocola_os_apagado; spotify encender > /tmp/o.2; ok "$(grep -c 'APAGADO de emergencia' /tmp/o.2)" 1 "con rocola_os_apagado, lo avisa"
	spotify nombre Rocola del Bar >/dev/null; ok "$(grep '^spotify_nombre=' $XPD/rocola_os.txt)" "spotify_nombre=Rocola del Bar" "«spotify nombre»"
	cp $XPD/rocola_os.txt /tmp/o.antes; /usr/lib/rocola-xp/rocola-xp & sleep 0.3   # (el rocola-xp falso: la rocola abierta)
	ROCOLA_SPOTIFY_ESPERA=2 spotify apagar > /tmp/o.3; r=$?
	ok "$r/$(cmp -s /tmp/o.antes $XPD/rocola_os.txt && echo igual)/$(grep -c 'La rocola sigue abierta' /tmp/o.3)" "1/igual/1" "con la rocola abierta (es la duena del archivo): no se toca"
	ok "$(spotify | grep -c 'spotify encender')" 1 "«spotify» solo: la ayuda" ;;
estado)   # «spotify estado» sin nada y con todo
	echo caido > /srv/.modo
	spotify estado > /tmp/o.1; r=$?; sed 's/^/    /' /tmp/o.1
	ok "$r/$(grep -c 'Modo Spotify: encendido (spotify= en /rocola/datos/xp/rocola_os.txt; de fabrica encendido)' /tmp/o.1)/$(grep -c 'Llave de Soloist: FALTA' /tmp/o.1)/$(grep -c 'no esta bajado' /tmp/o.1)/$(grep -c 'HTTP 503' /tmp/o.1)" "0/1/1/1/2" "sin nada: lo dice todo (modo encendido de fabrica, llave, programa, internet)"
	limpia; disco; llave; api; srv v2 0; rocola-soloist baja >/dev/null; spotify encender >/dev/null
	mkdir -p /run/rocola-xp/spotify; cp /tars/anillo /run/rocola-xp/spotify/sonido.anillo
	printf 'ws=1\nsesion=1\nactiva=1\nsonando=1\nen_cola=2\nesperando_credito=0\ncobradas=3\ndevueltas=1\nerror=\n' > /run/rocola-xp/spotify/estado
	lanza; espera_fase corriendo 6; sleep 1
	spotify estado > /tmp/o.2; sed 's/^/    /' /tmp/o.2
	ok "$(grep -c 'Modo Spotify: encendido' /tmp/o.2)/$(grep -c 'en marcha (pid' /tmp/o.2)/$(grep -c 'version 2.0.0' /tmp/o.2)/$(grep -c 'quedan 89 dias' /tmp/o.2)" "1/1/1/1" "con todo: el modo, en marcha, la version y los dias"
	ok "$(grep -c 'Llave de Soloist: puesta' /tmp/o.2)/$(grep -c 'API web: puesta' /tmp/o.2)/$(grep -c 'cuenta (sesion): si; bocina activa: si' /tmp/o.2)/$(grep -c 'llega (HTTP 200)' /tmp/o.2)" "1/1/1/2" "las llaves, la cuenta y la bocina activa (de la rocola), internet" ;;
prueba)   # «spotify prueba» sin nada y con todo
	echo caido > /srv/.modo; rm -f /usr/lib/rocola-pulso/libpulse.so.0.no
	spotify prueba > /tmp/o.1; r=$?; sed 's/^/    /' /tmp/o.1
	ok "$r/$(tail -1 /tmp/o.1)" "1/=== 1 BIEN, 6 MAL ===" "sin nada: solo rocola-pulso bien"
	# (el anillo recien creado por la rocola, todo a cero salvo la magia: «od» sin -v ponia «*» y salia «arithmetic syntax error»;
	# lo vio la prueba en QEMU, sistema/ACT_qemu_spotify.md)
	mkdir -p /run/rocola-xp/spotify; cp /tars/anillo0 /run/rocola-xp/spotify/sonido.anillo
	# (desde una carpeta con varios archivos de nombre con guion: el «*» se volvia sus nombres y corria los numeros)
	mkdir -p /tmp/cwd; : > /tmp/cwd/rocola-xp.txt; : > /tmp/cwd/rocola-os.txt; ( cd /tmp/cwd && spotify prueba > /tmp/o.1b 2>&1 ); sed 's/^/    /' /tmp/o.1b
	ok "$(grep -c -e arithmetic -e syntax /tmp/o.1b)/$(grep -c 'vale; sin flujo; escritos 0 cuadros' /tmp/o.1b)" "0/1" "el anillo recien creado (ceros): se lee sin errores"
	rm -f /run/rocola-xp/spotify/sonido.anillo
	limpia; disco; llave; api; srv v2 0; rocola-soloist baja >/dev/null; mkdir -p $SD; echo s > $SD/sesion.falsa
	mkdir -p /run/rocola-xp/spotify; cp /tars/anillo /run/rocola-xp/spotify/sonido.anillo
	printf 'ws=1\nsesion=1\nactiva=1\nsonando=0\nen_cola=0\n' > /run/rocola-xp/spotify/estado
	lanza; espera_fase corriendo 6
	spotify prueba > /tmp/o.2; r=$?; sed 's/^/    /' /tmp/o.2
	ok "$r/$(tail -1 /tmp/o.2)" "0/=== 9 BIEN, 0 MAL ===" "con todo: todo bien"
	ok "$(grep -c '44100 Hz, sonando' /tmp/o.2)/$(grep -c 'soloist ctl status: Logged in: yes' /tmp/o.2)" "1/1" "el anillo y lo que dice «soloist ctl status»" ;;
emparejar)   # «spotify emparejar» (soloist -p) y «olvidar»; con Soloist en marcha no se puede
	disco; srv v2 0; rocola-soloist baja >/dev/null
	spotify emparejar > /tmp/o.0; ok "$?/$(grep -c 'Primero la llave' /tmp/o.0)" "1/1" "sin llave: lo dice"
	llave; spotify emparejar > /tmp/o.1 2>&1; r=$?; sed 's/^/    /' /tmp/o.1
	ok "$r/$(grep -c 'Listo' /tmp/o.1)/$(si -s $SD/sesion.falsa)/$(grep -c "args: -p -n Rocola -k $LL -D $SD" /tmp/soloist.log)" "0/1/si/1" "empareja con soloist -p (en pantalla, lo que dice Soloist)"
	ok "$(grep -c "$LL" /tmp/o.1)/$(n 'Emparejar: acabo bien')" "0/1" "sin la llave en pantalla, y lo apunta"
	echo 1 > /tmp/soloist.par; spotify emparejar > /tmp/o.2 2>&1; ok "$?/$(grep -c 'No se pudo' /tmp/o.2)" "1/1" "si Soloist falla, lo dice"
	rm -f /tmp/soloist.par; lanza; espera_fase corriendo 6
	ROCOLA_SOLOIST_CANDADO=3 spotify emparejar > /tmp/o.3 2>&1; ok "$?/$(grep -c 'La rocola tiene Soloist abierto' /tmp/o.3)" "1/1" "con Soloist en marcha: no, y dice como (desde la app)"
	kill -TERM "$(cat /tmp/o.vig)"; hasta_que_no_quede >/dev/null
	echo SI | spotify olvidar > /tmp/o.4 2>&1; ok "$?/$(ls $SD | wc -l)/$(ls -ld $SD | cut -c1-10)" "0/0/drwx------" "«spotify olvidar»: la sesion borrada" ;;
entorno)   # como arranca Soloist: sus argumentos, su entorno, su grupo, su prioridad; la llave que cambia; un Soloist huerfano
	disco; llave; srv v2 0; rocola-soloist baja >/dev/null
	( nice -n 19 sh -c 'SPOTIFY_NOMBRE="Rocola Bar" ROCOLA_SPOTIFY_ANILLO=/run/x.anillo ROCOLA_PULSO_TRAZA=1 rocola-soloist corre >/dev/null 2>&1 & echo $! > /tmp/o.vig; wait' ) &
	espera_fase corriendo 6; sleep 0.5
	ok "$(grep 'args: -n' /tmp/soloist.log | sed 's/.*args: //')" "-n Rocola Bar -k $LL -D $SD -C /run/rocola-soloist/cache -z 100 -w 127.0.0.1:0 -i 100" "sus argumentos (I4)"
	e=$(grep 'env LD' /tmp/soloist.log | sed 's/.*env //')
	ok "$(echo "$e" | sed 's/ pgid=.*//')" "LD_LIBRARY_PATH=/usr/lib/rocola-pulso ANILLO=/run/x.anillo TRAZA=1 HOME=/run/rocola-soloist/casa" "su entorno: nuestro libpulse, el anillo y la traza de la rocola, su casa en /run"
	ok "$(echo "$e" | sed 's/.*pgid=\([0-9]*\).*/\1/')/$(echo "$e" | sed 's/.*nice=//')" "$(es pid)/0" "en su propio grupo, y con prioridad normal aunque al vigilante lo abran con nice 19"
	ok "$(ls -ld $SD | cut -c1-10)/$(ls -ld $AQ | cut -c1-10)" "drwx------/drwxr-xr-x" "su carpeta de datos 0700"
	llave llaveOTRA_9999999999; sleep 4
	ok "$(grep -c 'args: -n Rocola Bar -k llaveOTRA_9999999999' /tmp/soloist.log)/$(n 'La llave de Soloist cambio')/$(n llaveOTRA)" "1/1/0" "la llave cambia: se vuelve a arrancar con la nueva (y no se apunta)"
	kill -TERM "$(cat /tmp/o.vig)"; hasta_que_no_quede >/dev/null
	( exec $AQ/bin/soloist -n huerfano -k x -D "$SD" ) >/dev/null 2>&1 & sleep 0.5
	lanza; espera_fase corriendo 8
	ok "$(n 'Habia un Soloist de antes')/$(soloists)/$(grep -c 'soloist 2.0.0.*SIGTERM: sale con 0' /tmp/soloist.log)" "1/1/3" "un Soloist huerfano de antes: se cierra (su SIGTERM, el 3o: los otros dos, al cambiar la llave y al cerrar) y queda solo el nuevo" ;;
lanzador)   # ROCOLA_OS en el lanzador: =1 para la rocola (no para el importador); vacia con rocola_os_apagado
	export ROCOLA_ASOUND=/asound ROCOLA_VT_ACTIVA=/run/vt ROCOLA_SOLA=1; echo tty1 > /run/vt
	mkdir -p /rocola/disco/Rocola/Release; printf 'teclas\r\n' > /rocola/disco/Rocola/Release/rocola_teclas.txt; echo /rocola/disco/Rocola/Release > /run/rocola-datos-xp
	( exec setsid rocola </dev/null >/dev/null 2>&1 ) & sleep 3
	ok "$(tr '\n' '|' < /tmp/xp.log)" "rocola-xp --importa-xp: ROCOLA_OS=[SIN]|rocola-xp rocola: ROCOLA_OS=[1]|" "la rocola con ROCOLA_OS=1; el importador sin ella"
	touch $XPD/rocola_os_apagado; : > /tmp/xp.log; killall -TERM rocola-xp; sleep 4
	ok "$(grep 'rocola-xp rocola' /tmp/xp.log)/$(grep -c 'apagado de emergencia' /rocola/datos/rocola.txt)" "rocola-xp rocola: ROCOLA_OS=[]/1" "con rocola_os_apagado: vacia (= sin ella; RocolaOs.cs la toma como apagada) y lo apunta"
	rm -f $XPD/rocola_os_apagado; : > /tmp/xp.log; killall -TERM rocola-xp; sleep 4
	ok "$(grep 'rocola-xp rocola' /tmp/xp.log)" "rocola-xp rocola: ROCOLA_OS=[1]" "sin el archivo, otra vez 1 (se mira cada vez que se abre)" ;;
restos)   # (revision) lo que quede de Soloist FUERA de su grupo: un hijo en otra sesion con la tuberia de la bitacora (como crashpad) y
	# el mismo programa sacado a /run en marcha: el vigilante no se queda esperando la bitacora y los cierra (salvo «ctl» de la consola)
	disco; llave; srv v2 0; rocola-soloist baja >/dev/null
	echo escapa > /tmp/soloist.modo; lanza; espera_fase corriendo 6; sleep 0.5
	e=$(grep -o 'hijo escapado [0-9]*' $BIT | tail -1 | awk '{print $3}')
	ok "$(si -n "$e")/$(vivo_p "$e" && echo vivo)" "si/vivo" "Soloist deja un hijo en otra sesion con su salida"
	if [ -x /tars/dormilon ]; then
		cp /tars/dormilon $AQ/bin/soloist.n && mv -f $AQ/bin/soloist.n $AQ/bin/soloist
		( exec setsid $AQ/bin/soloist ) & sleep 0.2; ( exec setsid $AQ/bin/soloist ctl ) & sleep 0.3
		d1=$(pidof soloist | tr ' ' '\n' | while read p; do [ "$(tr '\0' ' ' < /proc/$p/cmdline)" = "$AQ/bin/soloist " ] && echo $p; done)
		d2=$(pidof soloist | tr ' ' '\n' | while read p; do [ "$(tr '\0' ' ' < /proc/$p/cmdline)" = "$AQ/bin/soloist ctl " ] && echo $p; done)
	else echo "  (sin gcc: no se prueba el programa sacado a /run que se quedo)"; fi
	kill -TERM "$(cat /tmp/o.vig)"; t=$(hasta_que_no_quede)
	ok "$(awk -v t=$t 'BEGIN {print (t < 10000) ? "menos de 10 s" : t " ms"}')/$(vivo_p "$e" && echo vivo)/$(n 'fuera de su grupo')/$(es fase)" "menos de 10 s//1/parado" "SIGTERM: todo fuera ($t ms), el hijo de otra sesion tambien, y lo apunta"
	if [ -n "$d1" ]; then
		ok "$(vivo_p "$d1" && echo vivo)/$(vivo_p "$d2" && echo vivo)" "/vivo" "el programa sacado a /run que se quedo: cerrado; un «soloist ctl» de la consola: no se toca"
		kill -KILL "$d2" 2>/dev/null
	fi ;;
cambio)   # (revision) caduca pronto y hay uno nuevo con Soloist en marcha: no se cambia mientras suena algo de Spotify (rocola-xp pone en
	# sonando= la uri de la que suena) y si cuando la rocola no tiene nada de Spotify
	disco; llave; srv v4 1; rocola-soloist baja >/dev/null; srv v2 0
	mkdir -p /run/rocola-xp/spotify; printf 'ws=1\nsesion=1\nactiva=1\nsonando=spotify:track:inventado0123456789\nen_cola=0\n' > /run/rocola-xp/spotify/estado
	lanza ROCOLA_SOLOIST_REINTENTO=5 ROCOLA_SOLOIST_REVISA=2; espera_fase corriendo 6; r=$?
	ok "$r/$(es version)" "0/4.0.0" "arranca la 4.0.0 (caduca en 3 dias; acaba de buscar uno nuevo: no lo vuelve a buscar antes de 5 s)"
	sleep 9
	ok "$(es version)/$(n 'se cambia ahora')/$(cmp -s $DP/soloist.tar.gz /tars/v2.tar.gz && echo bajado)" "4.0.0/0/bajado" "baja la 2.0.0 pero no la cambia: suena una de Spotify (sonando=spotify:track:...)"
	printf 'ws=1\nsesion=1\nactiva=1\nsonando=\nen_cola=0\n' > /run/rocola-xp/spotify/estado
	i=0; while [ "$(es version)" != 2.0.0 ] && [ $i -lt 60 ]; do sleep 0.25; i=$((i + 1)); done
	espera_fase corriendo 6; r=$?
	ok "$r/$(es version)/$(n 'se cambia ahora')" "0/2.0.0/1" "sin nada de Spotify (sonando= vacio, en_cola=0): se cambia ya a la 2.0.0" ;;
semanas)   # (24-11-2026, revision de mejoras) caduca en 20 dias (menos de 21) con Soloist en marcha: se baja el nuevo, queda PENDIENTE (el estado lo dice)
	# mientras suena algo de Spotify y se aplica en cuanto hay hueco; con 2 dias o menos basta con que no suene (aunque haya cola)
	disco; llave; srv v20 1; rocola-soloist baja >/dev/null; srv v2 0
	ok "$(sed -n 's/^caduca=//p' $DP/soloist.info)" "$(date -d "@$(( $(date +%s) + 20 * 86400 ))" +%Y-%m-%d)" "la 3.0.0 caduca en 20 dias"
	mkdir -p /run/rocola-xp/spotify; printf 'ws=1\nsesion=1\nactiva=1\nsonando=spotify:track:inventado0123456789\nen_cola=1\n' > /run/rocola-xp/spotify/estado
	lanza ROCOLA_SOLOIST_REINTENTO=4 ROCOLA_SOLOIST_REVISA=2; espera_fase corriendo 6; r=$?
	ok "$r/$(es version)/$(es dias)" "0/3.0.0/20" "arranca la 3.0.0 con 20 dias (el estado los lleva)"
	sleep 9
	ok "$(es version)/$(es pendiente)/$(cmp -s $DP/soloist.tar.gz /tars/v2.tar.gz && echo bajado)/$(n 'se cambia ahora')" "3.0.0/1/bajado/0" "baja la 2.0.0 y queda pendiente: suena una de Spotify"
	printf 'ws=1\nsesion=1\nactiva=1\nsonando=\nen_cola=1\n' > /run/rocola-xp/spotify/estado
	sleep 3; ok "$(es version)" "3.0.0" "sin sonar pero con cola (y 20 dias): sigue esperando el hueco"
	printf 'ws=1\nsesion=1\nactiva=1\nsonando=\nen_cola=0\n' > /run/rocola-xp/spotify/estado
	i=0; while [ "$(es version)" != 2.0.0 ] && [ $i -lt 40 ]; do sleep 0.25; i=$((i + 1)); done; espera_fase corriendo 6; r=$?
	ok "$r/$(es version)/$(es pendiente)/$(n 'se cambia ahora')" "0/2.0.0//1" "en cuanto hay hueco (nada sonando ni en cola) se cambia y ya no hay pendiente"
	;;
diasdos)   # (24-11-2026, revision de mejoras) con 2 dias o menos de vida basta con que no suene nada de Spotify, aunque haya cola
	disco; llave; srv v2d 1; rocola-soloist baja >/dev/null; srv v2 0
	mkdir -p /run/rocola-xp/spotify; printf 'ws=1\nsesion=1\nactiva=1\nsonando=\nen_cola=3\n' > /run/rocola-xp/spotify/estado
	lanza ROCOLA_SOLOIST_REINTENTO=4 ROCOLA_SOLOIST_REVISA=2; espera_fase corriendo 6
	i=0; while [ "$(es version)" != 2.0.0 ] && [ $i -lt 60 ]; do sleep 0.25; i=$((i + 1)); done
	ok "$(es version)/$(n 'quedan 2 dias')" "2.0.0/1" "con 2 dias de vida basta con que no suene nada de Spotify (aunque haya cola)" ;;
atras)   # (24-11-2026, revision de mejoras) la build recien bajada no abre su WebSocket: a los 3 arranques se vuelve a la anterior y no se
	# vuelve a bajar esa misma build en 24 h
	disco; llave; srv v20 1; rocola-soloist baja >/dev/null; srv v2 0; echo 2.0.0 > /tmp/soloist.sin_ws
	lanza ROCOLA_SOLOIST_REINTENTO=4 ROCOLA_SOLOIST_REVISA=2; espera_fase corriendo 6; r=$?
	ok "$r/$(es version)" "0/3.0.0" "arranca la 3.0.0 (caduca en 20 dias)"
	i=0; while [ "$(n 'recien bajado no abrio su WebSocket')" = 0 ] && [ $i -lt 240 ]; do sleep 0.25; i=$((i + 1)); done
	espera_fase corriendo 15; r=$?; sleep 6; bit 8
	ok "$r/$(es version)/$(n 'Se estrena el programa')/$(grep -c 'soloist 2.0.0.*args: -n' /tmp/soloist.log)" "0/3.0.0/1/3" "la 2.0.0 se estrena, no abre su WebSocket 3 veces y vuelve la 3.0.0"
	ok "$(cmp -s $DP/soloist.tar.gz /tars/v20.tar.gz && echo anterior)/$(cmp -s $DP/soloist.malo.tar.gz /tars/v2.tar.gz && echo mala)/$(si -s $DP/soloist.malo.marca)" "anterior/mala/si" "la actual es la de antes y la mala queda aparte, con su marca"
	ok "$(n 'no se vuelve a poner en 24 h' | awk '{print ($1 >= 1)}')/$(grep -c 'soloist 2.0.0.*args: -n' /tmp/soloist.log)" "1/3" "esa build ya no se vuelve a poner aunque Spotify la siga ofreciendo (y la 3.0.0 caduca pronto: lo intento)" ;;
reinicia)   # (24-11-2026, revision de mejoras) rocola-xp pide reiniciar a Soloist con un archivo «reinicia» en su carpeta de datos: una vez cada ROCOLA_SOLOIST_REINICIA_MIN s
	disco; llave; srv v2 0; rocola-soloist baja >/dev/null
	espera_arranques() { i=0; while [ "$(arranques)" -lt "$1" ] && [ $i -lt $(( $2 * 5 )) ]; do sleep 0.2; i=$((i + 1)); done; espera_fase corriendo 8; }
	lanza ROCOLA_SOLOIST_REINICIA_MIN=6; espera_fase corriendo 8; a=$(arranques)
	echo "la prueba de reinicio" > $SD/reinicia; espera_arranques $((a + 1)) 8
	ok "$(arranques)/$(si -e $SD/reinicia)/$(n 'pidio reiniciar a Soloist (la prueba de reinicio): se vuelve a arrancar')" "$((a + 1))/no/1" "reinicia a Soloist una vez, con el motivo, y borra el archivo"
	echo "otra vez" > $SD/reinicia; sleep 2
	ok "$(arranques)/$(si -e $SD/reinicia)/$(n 'se ignora')" "$((a + 1))/no/1" "otro enseguida: se borra, se apunta que se ignoro y no reinicia"
	sleep 5; echo "ya paso el plazo" > $SD/reinicia; espera_arranques $((a + 2)) 8
	ok "$(arranques)/$(n 'pidio reiniciar a Soloist (ya paso el plazo): se vuelve')" "$((a + 2))/1" "pasado el plazo vuelve a poder reiniciar" ;;
padrebaja)   # (revision) muere el padre mientras baja el programa (una bajada lenta): el vigilante no se queda minutos bajando con el candado
	disco; llave; echo "lento 30" > /srv/.modo; srv v2 0
	lanza; espera_fase bajando 6; kill -KILL "$(cat /tmp/o.padre)"; t=$(hasta_que_no_quede)
	ok "$(awk -v t=$t 'BEGIN {print (t < 3000) ? "menos de 3 s" : t " ms"}')/$(si -e $DP/soloist.tar.gz.t)/$(n 'murio su padre')" "menos de 3 s/no/1" "todo fuera ($t ms) y sin el .t" ;;
*) echo "  MAL: no hay caso «$1»" ;;
esac
limpia
