#!/bin/sh
# (dentro de la raiz de prueba: lo corre prueba.sh) los guiones del sistema con un rocola-xp falso y el BusyBox del sistema. Cada
# comprobacion dice «BIEN» o «MAL»; prueba.sh las cuenta. Los relojes del lanzador se acortan con sus variables de prueba (ROCOLA_ESPERAS,
# ROCOLA_SEGUIDA, ROCOLA_SOLA, ROCOLA_CONFIRMA, ROCOLA_IMPORTA_MAX); lo de verdad no cambia.
mount -t proc proc /proc
export ROCOLA_ASOUND=/asound ROCOLA_VT_ACTIVA=/run/vt PATH=/usr/bin:/bin
unset http_proxy https_proxy HTTP_PROXY HTTPS_PROXY ALL_PROXY all_proxy
API=http://127.0.0.1:$(cat /tmp/gh.puerto)
XPD=/rocola/datos/xp
limpia() {
	killall rocola rocola-sonido rocola-prueba 2>/dev/null; killall -KILL rocola-xp sleep 2>/dev/null; sleep 1
	umount /rocola/disco 2>/dev/null
	rm -rf /run/* /tmp/falso.* /tmp/tty1 /tmp/sonido.txt /rocola/datos/* /rocola/datos/.[!.]* /rocola/disco /rocola/discos /root/rocola.txt \
		/root/rocola-prueba.txt /tmp/rocola-prueba-xp /etc/rocola-sistema-version
	echo tty1 > /run/vt
}
log() { sed 's/^/    /' /tmp/falso.log 2>/dev/null; }
bit() { sed 's/^[0-9: -]*/    bitacora: /' /rocola/datos/rocola_actualizaciones.txt 2>/dev/null; }
# cuantas lineas de ARCHIVO tienen TEXTO (0 si ninguna o no esta)
n() { c=$(grep -c -- "$1" "${2:-/tmp/falso.log}" 2>/dev/null); echo "${c:-0}"; }
si() { if [ "$@" ]; then echo si; else echo no; fi; }
ok() { if [ "$1" = "$2" ]; then echo "  BIEN: $3 [$1]"; else echo "  MAL: $3 [sale «$1», se esperaba «$2»]"; fi; }
# lanza [VAR=valor...]: el lanzador como lo abre S99rocola (su pantalla, tty1, va a /tmp/tty1)
lanza() { ( [ $# -gt 0 ] && export "$@"; exec setsid rocola </dev/null >/tmp/tty1 2>&1 ) & }
# la linea N de lo que apunto el rocola-xp falso, sin la hora
linea() { sed -n "$1p" /tmp/falso.log | sed 's/ \[[0-9]*\]//'; }
# los segundos (redondeados) desde que la rocola se cerro hasta que se volvio a abrir, uno por cada vez que se volvio a abrir
esperas() {
	grep -E 'rocola-xp: \[[0-9]+\] (arranca|sale con)' /tmp/falso.log | sed 's/^rocola-xp: \[\([0-9]*\)\] \([a-z]*\).*/\1 \2/' \
		| awk '$2 == "sale" {s = $1} $2 == "arranca" {if (n++) printf "%s%d", (n > 2 ? " " : ""), ($1 - s + 500) / 1000}'
}
# el estado de los procesos del lanzador (T = parado, S = esperando); sin los que acaban mientras se miran (sus $(...) tambien se llaman rocola)
estado_lanzador() { for p in $(pidof rocola); do awk '{print $3}' "/proc/$p/stat" 2>/dev/null; done | sort -u | tr '\n' ' '; }
datos_xp() { mkdir -p /rocola/disco/Rocola/Release; printf 'teclas\r\n' > /rocola/disco/Rocola/Release/rocola_teclas.txt; echo /rocola/disco/Rocola/Release > /run/rocola-datos-xp; }
limpia
case "$1" in
arranque)   # siempre rocola-xp: el importador primero (el contrato), sus datos, la musica, las letras de XP; rocola_programa.txt se ignora
	mkdir -p /rocola/disco/WINDOWS/Fonts /rocola/disco/Rocola/musica "/rocola/discos/sdb1/Mi musica"; datos_xp
	printf '/rocola/disco/Rocola/musica\n/rocola/discos/sdb1/Mi musica\n/no/esta\n' > /run/rocola-musica
	echo "programa=consola" > /rocola/datos/rocola_programa.txt
	lanza ROCOLA_SOLA=2; sleep 3; log
	ok "$(linea 1)" "rocola-xp: importa args=[--importa-xp /run/rocola-datos-xp] DATOS=$XPD IA=-" "primero el importador, con el contrato"
	ok "$(linea 2)" "rocola-xp: importa sale con 0" "el importador acaba antes de abrir la rocola"
	ok "$(linea 3)" "rocola-xp: arranca args=[] DATOS=$XPD LETRAS=/rocola/disco/WINDOWS/Fonts ALSA=plughw:0,0 PANTALLA=- IA=-" "despues la rocola, con su entorno"
	ok "$(tr -d '\r' < $XPD/rocola_config.txt | tr '\n' '|')" "dir=/rocola/disco/Rocola/musica|dir=/rocola/discos/sdb1/Mi musica|" "las carpetas de /run/rocola-musica como dir= (sin la que no esta)"
	ok "$(cat /rocola/datos/rocola_programa.txt)" "programa=consola" "rocola_programa.txt no se borra"
	ok "$(n 'ya no se usa' /rocola/datos/rocola.txt)" 1 "y rocola.txt dice que ya no se usa"
	ok "$(si -e /rocola/disco/Rocola/Release/rocola_os_escribe.tmp -o -e /rocola/disco/Rocola/Release/rocola_de_xp.txt)" no "en la carpeta de XP no se escribe nada"
	echo "  -- se cierra con 0 a los 3 s (Alt+F4; con ROCOLA_SOLA=2 no es una caida): otra vez en 2 s, con el importador antes"
	echo 0 > /tmp/falso.cierra; sleep 4; log | tail -3
	ok "$(n 'importa args')/$(n 'arranca')" "2/2" "el importador antes de cada arranque"
	ok "$(esperas)" 2 "segundos hasta volver a abrirla"
	ok "$(tr -d '\r' < $XPD/rocola_config.txt | grep -c '^dir=')" 2 "los dir= no se repiten"
	ok "$(n 'se cayo\|se cerro sola' /rocola/datos/rocola_actualizaciones.txt)" 0 "cerrarla no es una caida" ;;
ia)   # el material de la IA: /rocola/disco/rocola-os/ia-xp solo con el disco de la rocola montado con escritura
	mkdir -p /rocola/disco; mount -t tmpfs tmpfs /rocola/disco; mkdir -p /rocola/disco/rocola-os; datos_xp
	lanza; sleep 3; log
	ok "$(linea 1 | sed 's/.* IA=//')" /rocola/disco/rocola-os/ia-xp "con escritura: al importador"
	ok "$(linea 3 | sed 's/.* IA=//')" /rocola/disco/rocola-os/ia-xp "y a la rocola"
	ok "$(si -d /rocola/disco/rocola-os/ia-xp)" si "la carpeta se crea"
	killall rocola; killall -KILL rocola-xp; sleep 1; rm -f /tmp/falso.log; mount -o remount,ro /rocola/disco
	lanza; sleep 2; ok "$(grep arranca /tmp/falso.log | sed 's/.* IA=//')" - "en solo lectura: sin la variable"
	killall rocola; killall -KILL rocola-xp; sleep 1; rm -f /tmp/falso.log; umount /rocola/disco; mkdir -p /rocola/disco
	lanza; sleep 2; ok "$(grep arranca /tmp/falso.log | sed 's/.* IA=//')" - "sin el disco de la rocola (la memoria USB): sin la variable" ;;
importa)   # el importador: sin la carpeta de XP no se llama; si falla o tarda demasiado, se apunta y la rocola se abre igual
	lanza; sleep 2; ok "$(n 'importa args')/$(n arranca)" "0/1" "sin /run/rocola-datos-xp no se llama"
	limpia; datos_xp; echo "3 0" > /tmp/falso.importa
	lanza ROCOLA_SOLA=1; sleep 2; echo 0 > /tmp/falso.cierra; sleep 4; log
	ok "$(n 'importa args')/$(n arranca)" "2/2" "sale con 3: la rocola se abre igual, y se vuelve a intentar al reabrirla"
	ok "$(n 'codigo 3' /rocola/datos/rocola_actualizaciones.txt)" 1 "en la bitacora una vez"
	ok "$(n 'salio con 3' /rocola/datos/rocola.txt)" 2 "en rocola.txt cada vez"
	limpia; datos_xp; echo "0 30" > /tmp/falso.importa
	lanza ROCOLA_IMPORTA_MAX=4 ROCOLA_SOLA=1; sleep 7; log; bit
	ok "$(n 'importa sale')/$(n arranca)" "0/1" "tarda mas que el tope (aqui 4 s): se corta y la rocola se abre"
	ok "$(n 'no acabo en 4 s' /rocola/datos/rocola_actualizaciones.txt)" 1 "queda en la bitacora"
	ok "$(n 'trayendo lo aprendido' /tmp/tty1)" 1 "el aviso en la pantalla (paso de 3 s)"
	ok "$(tail -c 7 /tmp/tty1 | od -An -c | tr -s ' ' | sed 's/^ //')" '033 [ 2 J 033 [ H' "y se borra al acabar"
	echo 0 > /tmp/falso.cierra; sleep 4
	ok "$(n 'importa args')/$(n arranca)" "1/2" "en este encendido ya no se vuelve a intentar"
	limpia; datos_xp; lanza; sleep 2; ok "$(n 'trayendo' /tmp/tty1)" 0 "si acaba en menos de 3 s no se ve nada en la pantalla" ;;
caidas)   # se cae al arrancar una y otra vez: cada vez espera mas (aqui 1, 2, 3 y 4 s; en la rocola 2, 10, 30 s y 2 min)
	echo "134 0" > /tmp/falso.sale
	lanza ROCOLA_ESPERAS="1 2 3 4" ROCOLA_SEGUIDA=5 ROCOLA_SOLA=2; sleep 16
	ok "$(esperas)" "1 2 3 4 4" "segundos entre un arranque y el siguiente"
	bit | head -6
	ok "$(n 'se cayo (codigo 134) a los 0 s: se vuelve a abrir en 1 s' /rocola/datos/rocola_actualizaciones.txt)" 1 "la primera, en la bitacora"
	ok "$(n '5 veces seguidas: se vuelve a abrir en 4 s' /rocola/datos/rocola_actualizaciones.txt)" 1 "la quinta seguida, tambien"
	echo "  -- aguanta mas de 5 s (ROCOLA_SEGUIDA) y se cae: vuelve a la primera espera"
	echo "0 600" > /tmp/falso.sale; sleep 10; echo 134 > /tmp/falso.cierra; sleep 3
	ok "$(esperas | awk '{print $NF}')" 1 "segundos hasta volver a abrirla"
	ok "$(tail -1 /rocola/datos/rocola_actualizaciones.txt | sed -n 's/.*se cayo (codigo 134) a los \([0-9]*\) s: se vuelve a abrir en \([0-9]*\) s$/\1 \2/p' | awk '{print ($1 >= 5) "/" $2}')" "1/1" "en la bitacora (a los 5 s o mas: no es seguida)"
	echo "  -- se cierra sola (con 0) al arrancar: tambien cuenta"
	echo "0 0" > /tmp/falso.sale; echo 0 > /tmp/falso.cierra; sleep 6; bit | tail -3
	ok "$(n 'se cerro sola a los 0 s: se vuelve a abrir en 1 s' /rocola/datos/rocola_actualizaciones.txt)" 1 "la primera, en la bitacora"
	ok "$(n 'se cerro sola a los 0 s, 2 veces seguidas: se vuelve a abrir en 2 s' /rocola/datos/rocola_actualizaciones.txt)" 1 "y la segunda cuenta como seguida" ;;
confirma)   # un sistema nuevo solo se confirma si rocola-xp aguanta en pie ROCOLA_CONFIRMA s seguidos (en la rocola, 120)
	echo "134 2" > /tmp/falso.sale
	lanza ROCOLA_CONFIRMA=6 ROCOLA_ESPERAS="1 1 1 1"; sleep 20
	ok "$(si -f /run/rocola-confirmado)/$(si $(n arranca) -ge 5)" "no/si" "se cae cada 2 s (5 veces o mas en 20 s): no se confirma"
	limpia; datos_xp; echo "0 10" > /tmp/falso.importa; echo "134 1" > /tmp/falso.sale
	lanza ROCOLA_CONFIRMA=6 ROCOLA_ESPERAS="1 1 1 1"; sleep 16
	ok "$(si -f /run/rocola-confirmado)/$(n 'importa sale')" "no/1" "el importador (10 s) no cuenta, y despues la rocola se cae: no se confirma"
	limpia; lanza ROCOLA_CONFIRMA=6; sleep 4
	ok "$(si -f /run/rocola-confirmado)" no "a los 4 s, todavia no"
	sleep 8; ok "$(si -f /run/rocola-confirmado)" si "aguanta 6 s seguidos: se confirma (a lo mas 5 s despues)" ;;
consola)   # Ctrl+Alt+F2: se cierra con SIGUSR1 (conserva el estado), no se abre en la consola y vuelve con Ctrl+Alt+F1
	datos_xp; lanza; sleep 3
	echo tty2 > /run/vt; sleep 6; log
	ok "$(pidof rocola-xp | wc -w)/$(n SIGUSR1)/$(n SIGTERM)/$(n arranca)" "0/1/0/1" "en la consola: cerrada con SIGUSR1, nunca SIGTERM, y no se vuelve a abrir"
	echo tty1 > /run/vt; sleep 4; log | tail -3; bit
	ok "$(n arranca)/$(n 'importa args')" "2/2" "al volver a la 1 se abre otra vez, con el importador antes"
	ok "$(n 'consola de mantenimiento' /rocola/datos/rocola_actualizaciones.txt)/$(n 'se cayo' /rocola/datos/rocola_actualizaciones.txt)" "1/0" "apuntado, y no es una caida"
	echo "  -- a la consola mientras se trae lo de XP (6 s): el importador sigue; la rocola no se abre hasta volver"
	limpia; datos_xp; echo "0 6" > /tmp/falso.importa
	lanza; sleep 2; echo tty2 > /run/vt; sleep 7
	ok "$(n 'importa sale con 0')/$(n arranca)" "1/0" "el importador acabo y la rocola no se abrio en la consola"
	rm -f /tmp/falso.importa; echo tty1 > /run/vt; sleep 4
	ok "$(n arranca)" 1 "al volver, se abre" ;;
reinicia)   # «reiniciar el programa ya» (buzon): SIGUSR1, ordenado (sale con 0) o cortado al arrancar (138); uno que no se cierra, a los 20 s
	for modo in ordenado muere; do
		limpia; echo $modo > /tmp/falso.usr1
		lanza; sleep 3
		for i in 1 2; do touch /run/rocola-reinicia-programa-ya; sleep 9; done
		ok "$(pidof rocola-xp | wc -w)/$(n arranca)/$(n SIGTERM)" "1/3/0" "$modo: dos reinicios, abierta otra vez, nunca SIGTERM"
		ok "$(n 'reiniciarse (pedido desde casa)' /rocola/datos/rocola_actualizaciones.txt)/$(n 'se cayo' /rocola/datos/rocola_actualizaciones.txt)" "2/0" "$modo: apuntados y no son caidas"
		ok "$(si -f /run/rocola-reinicia-programa-ya)/$(si -f /run/rocola-xp-reinicio)" "no/no" "$modo: los avisos se quitan"
	done
	ok "$(n 'SIGUSR1 (cierre ordenado')" 0 "muere: sin cierre ordenado (138, como un apagon)"
	limpia; echo sordo > /tmp/falso.usr1; lanza; sleep 3; touch /run/rocola-reinicia-programa-ya; sleep 27; bit
	ok "$(n 'no se cerro en 20 s: se corto' /rocola/datos/rocola_actualizaciones.txt)/$(n arranca)" "1/2" "sordo: se corta a los 20 s, se apunta y se abre otra" ;;
boton)   # el boton de encendido: el lanzador y la rocola (SIGTERM: el apagado, que borra el estado) y se apaga
	lanza; sleep 3; sh /etc/acpi/PWRF/00000080; sleep 1; log
	ok "$(pidof rocola rocola-xp | wc -w)/$(n SIGTERM)/$(n SIGUSR1)/$(n 'poweroff:')" "0/1/0/1" "ni lanzador ni rocola, con SIGTERM, y se apaga" ;;
sinprograma)   # un sistema sin rocola-xp: el aviso en la pantalla y en la bitacora, sin dar vueltas
	mv /usr/lib/rocola-xp/rocola-xp /usr/lib/rocola-xp/x
	lanza; sleep 4; sed 's/^/    tty1: /' /tmp/tty1; bit
	ok "$(pidof rocola | wc -w)/$(n 'NO TRAE EL PROGRAMA' /rocola/datos/rocola_actualizaciones.txt)" "0/1" "el lanzador se va (sin bucle) y lo apunta una vez"
	ok "$(n 'este sistema no trae el programa' /tmp/tty1)/$(n 'version anterior del sistema' /tmp/tty1)" "1/1" "el aviso en la pantalla"
	limpia; mkdir -p /rocola/disco/rocola-os; touch /rocola/disco/rocola-os/probando; lanza; sleep 3
	ok "$(n 'vuelve solo al anterior' /tmp/tty1)" 1 "un sistema nuevo en prueba: vuelve solo al anterior"
	mv /usr/lib/rocola-xp/x /usr/lib/rocola-xp/rocola-xp ;;
sonido)   # rocola-sonido: para el lanzador mientras suena; si la rocola estaba abierta, SIGUSR1 y la vuelve a abrir; «usar N»
	lanza; sleep 3; echo tty2 > /run/vt; sleep 3
	rocola-sonido > /tmp/sonido.txt 2>&1 & s=$!; sleep 3
	ok "$(estado_lanzador)" "T " "desde la consola: el lanzador parado mientras suena"
	wait $s; sleep 1
	ok "$(estado_lanzador)" "S " "y sigue al acabar"
	ok "$(n 'speaker-test: -D plughw:0,0')/$(n 'speaker-test: -D plughw:1,0')/$(n SIGUSR1)" "1/1/1" "las dos salidas; la rocola solo se cerro al ir a la consola"
	echo tty1 > /run/vt; sleep 4; ok "$(n arranca)" 2 "al volver, se abre"
	limpia; lanza; sleep 3; rocola-sonido > /tmp/sonido.txt 2>&1; sleep 4; log; bit
	ok "$(n SIGUSR1)/$(n SIGTERM)/$(n arranca)" "1/0/2" "con la rocola abierta: SIGUSR1 y se abre al acabar"
	ok "$(n 'reiniciarse (la prueba de sonido (rocola-sonido))' /rocola/datos/rocola_actualizaciones.txt)/$(n 'se cayo' /rocola/datos/rocola_actualizaciones.txt)" "1/0" "apuntado, no es una caida"
	limpia; lanza; sleep 3; rocola-sonido usar 2 > /dev/null; sleep 4
	ok "$(cat /rocola/datos/rocola_tarjeta.txt)/$(grep arranca /tmp/falso.log | tail -1 | sed 's/.*ALSA=\([^ ]*\).*/\1/')" "plughw:1,0/plughw:1,0" "usar 2: se abre otra vez con esa salida"
	rocola-sonido auto > /dev/null; sleep 4
	ok "$(si -f /rocola/datos/rocola_tarjeta.txt)/$(grep arranca /tmp/falso.log | tail -1 | sed 's/.*ALSA=\([^ ]*\).*/\1/')" "no/plughw:0,0" "auto: otra vez la de la placa"
	ok "$(n 'se cayo' /rocola/datos/rocola_actualizaciones.txt)" 0 "ninguna caida"
	limpia; lanza; sleep 3; echo tty2 > /run/vt; sleep 3
	# (SIGTERM solo al guion: el trap corre cuando acaba lo que esta haciendo, hasta 4 s; con Ctrl+C en la consola, al momento)
	rocola-sonido > /dev/null 2>&1 & s=$!; sleep 2; kill -TERM $s; sleep 6
	ok "$(estado_lanzador)" "S " "cortada a medias: el lanzador sigue" ;;
prueba)   # rocola-prueba: la cancion de prueba con mpg123 y la pantalla con rocola-xp 20 s (SIGUSR1, datos de prueba que se borran)
	mkdir -p /rocola/disco/WINDOWS/Fonts
	setsid rocola-prueba </dev/null >/tmp/tty1 2>&1 & sleep 29; log
	ok "$(n 'mpg123: -q -o alsa -a plughw:0,0 /usr/share/rocola/prueba.mp3')/$(n 'Rocola: toco la cancion de prueba' /root/rocola-prueba.txt)" "1/1" "la cancion de prueba"
	ok "$(grep arranca /tmp/falso.log | sed 's/.*arranca //')" "args=[] DATOS=/tmp/rocola-prueba-xp LETRAS=/rocola/disco/WINDOWS/Fonts ALSA=plughw:0,0 PANTALLA=- IA=-" "rocola-xp con datos de prueba"
	ok "$(n SIGUSR1)/$(n SIGTERM)" "1/0" "a los 20 s, SIGUSR1 (nunca SIGTERM)"
	ok "$(n 'Pantalla de la rocola: se dibujo 20 s (y se cerro bien)' /root/rocola-prueba.txt)" 1 "lo que apunta"
	ok "$(si -e /tmp/rocola-prueba-xp)/$(si -e $XPD)/$(si -f /rocola/datos/prueba_hecha)" "no/no/si" "los datos de prueba se borran y los de verdad no se tocan"
	ok "$(n 'kmscube: -c 900')" 1 "el cubo"
	killall rocola-prueba sleep 2>/dev/null ;;
s99)   # S99rocola, un sistema nuevo en prueba (con 2 vueltas de 1 s en lugar de 90 de 10 s): sin confirmar, SIGUSR1 y a reiniciar
	sed -n '/^\t\tif \[ -f \/rocola\/disco\/rocola-os\/probando \]; then$/,/^\t\tfi$/p' /etc/init.d/S99rocola | sed 's/-lt 90/-lt 2/; s/sleep 10/sleep 1/' > /tmp/s99.sh
	echo "  (trozo de S99rocola: $(wc -l < /tmp/s99.sh) lineas)"
	mkdir -p /rocola/disco/rocola-os; touch /rocola/disco/rocola-os/probando; echo 7 > /etc/rocola-sistema-version
	lanza; sleep 3; sh /tmp/s99.sh; sleep 6; log
	ok "$(n SIGUSR1)/$(n SIGTERM)/$(n 'reboot:')/$(pidof rocola rocola-xp | wc -w)" "1/0/1/0" "sin confirmar: la rocola con SIGUSR1 (conserva el estado), sin lanzador, y a reiniciar"
	ok "$(n 'el sistema 7 no llego a confirmarse' /rocola/disco/rocola-os/sistema.txt)" 1 "apuntado en sistema.txt"
	limpia; mkdir -p /rocola/disco/rocola-os; touch /rocola/disco/rocola-os/probando /run/rocola-confirmado; echo 7 > /etc/rocola-sistema-version
	sh /tmp/s99.sh; sleep 2
	ok "$(si -f /rocola/disco/rocola-os/probando)/$(n 'el sistema 7 quedo bien' /rocola/disco/rocola-os/sistema.txt)/$(n 'reboot:')" "no/1/0" "confirmado: queda bien" ;;
buzon)   # rocola-buzon con un GitHub de mentira: lo «cuando este libre» no se acepta; los archivos de rocola-xp; el estado; las «ya»
	B="ROCOLA_API=$API ROCOLA_RUN=/run ROCOLA_DATOS=/rocola/datos ROCOLA_DISCO=/rocola/disco"
	rm -rf /gh/repo /gh/api.log
	printf 'llave-de-prueba\nprueba/buzon\n' > /rocola/datos/rocola_buzon.txt; echo secreto > /rocola/datos/rocola_github.txt
	mkdir -p $XPD; printf 'error de xp 1\r\nerror de xp 2\r\n' > $XPD/rocola_error.txt; echo secreto > $XPD/rocola_github.txt
	echo "error viejo de rocola-consola" > /rocola/datos/rocola_error.txt; echo "lo que dijo la rocola" > /rocola/datos/rocola.txt
	cat > /gh/ordenes.json <<'EOF'
[{"id":"1","orden":"reiniciar_programa"},{"id":"2","orden":"apagar"},{"id":"3","orden":"reiniciar_equipo"},{"id":"4","orden":"diagnostico"},
 {"id":"5","orden":"archivo","arg":"rocola_error.txt"},{"id":"6","orden":"archivo","arg":"rocola.txt"},{"id":"7","orden":"archivo","arg":"rocola_github.txt"},
 {"id":"8","orden":"registros"},{"id":"9","orden":"cancelar"}]
EOF
	lanza; sleep 3; env $B rocola-buzon una
	r() { jq -r --arg i "$1" '.[] | select(.id == $i) | .respuesta' /gh/repo/rocola/respuestas.json; }
	for i in 1 2 3 4 5 6 7 8 9; do echo "    respuesta $i: $(r $i)"; done
	ok "$(r 1 | grep -c 'cuando este libre')/$(r 2 | grep -c 'apagar ya')/$(r 3 | grep -c 'reiniciar_equipo ya')/$(r 4 | grep -c 'todavia no')" "1/1/1/1" "lo «cuando este libre» y el diagnostico: no se aceptan y se dice por que"
	ok "$(ls /run | grep -c 'rocola-reinicia-programa\|rocola-apaga-equipo\|rocola-reinicia-equipo\|rocola-pide-diagnostico')" 0 "y no se deja nada pendiente"
	ok "$(tr -d '\r' < /gh/repo/rocola/archivos/rocola_error.txt | tail -1)/$(head -1 /gh/repo/rocola/archivos/rocola.txt)" "error de xp 2/lo que dijo la rocola" "archivo: el de rocola-xp primero; si no, el de /rocola/datos"
	ok "$(r 7 | grep -c 'no se puede pedir')/$(si -e /gh/repo/rocola/archivos/rocola_github.txt)" "1/no" "las llaves no (tampoco la de la carpeta de rocola-xp)"
	ok "$(jq -r 'keys | map(select(. == "xp_rocola_error.txt" or . == "rocola_error.txt" or . == "rocola.txt")) | join(",")' /gh/repo/rocola/registros.json)" "rocola.txt,rocola_error.txt,xp_rocola_error.txt" "registros: tambien los de rocola-xp (xp_...)"
	ok "$(jq -r '.versiones | [.programa, .programa_linea, .programa_cual, .programa_nueva, .programa_malas] | join("|")' /gh/repo/rocola/estado.json)" "0|rocola-xp programa 0 (local) compilado 1700000000|rocola-xp||" "el estado: la version de rocola-xp"
	ok "$(jq -r '.rocola.corriendo + "|" + (.errores.ultimos | split("\n") | last) + "|" + (.errores.total_lineas | tostring)' /gh/repo/rocola/estado.json)" "si|error de xp 2|2" "el estado: rocola-xp corre y sus errores"
	echo "  -- «reiniciar_programa ya»: el lanzador la cierra con SIGUSR1 y la vuelve a abrir"
	echo '[{"id":"10","orden":"reiniciar_programa","arg":"ya"}]' > /gh/ordenes.json; env $B rocola-buzon una; sleep 8
	ok "$(n SIGUSR1)/$(n SIGTERM)/$(n arranca)" "1/0/2" "cerrada con SIGUSR1 y abierta otra vez"
	echo "  -- «reiniciar_equipo ya»: primero el lanzador, la rocola con SIGUSR1, y a reiniciar"
	echo '[{"id":"11","orden":"reiniciar_equipo","arg":"ya"}]' > /gh/ordenes.json; env $B rocola-buzon una; sleep 1
	ok "$(n SIGUSR1)/$(n SIGTERM)/$(n 'reboot:')/$(pidof rocola rocola-xp | wc -w)" "2/0/1/0" "conserva el estado y reinicia"
	echo "  -- «apagar ya»: la rocola con SIGTERM (como el boton) y se apaga"
	limpia; printf 'llave-de-prueba\nprueba/buzon\n' > /rocola/datos/rocola_buzon.txt; lanza; sleep 3
	echo '[{"id":"12","orden":"apagar","arg":"ya"}]' > /gh/ordenes.json; env $B rocola-buzon una; sleep 1
	ok "$(n SIGUSR1)/$(n SIGTERM)/$(n 'poweroff:')/$(pidof rocola rocola-xp | wc -w)" "0/1/1/0" "borra el estado y se apaga"
	echo "  -- «reiniciar_equipo ya» con la rocola cerrada (en la consola): el lanzador se para igual (que no la abra mientras se apaga)"
	limpia; printf 'llave-de-prueba\nprueba/buzon\n' > /rocola/datos/rocola_buzon.txt; lanza; sleep 3; echo tty2 > /run/vt; sleep 4
	echo '[{"id":"13","orden":"reiniciar_equipo","arg":"ya"}]' > /gh/ordenes.json; env $B rocola-buzon una; sleep 1
	ok "$(n 'reboot:')/$(pidof rocola rocola-xp | wc -w)/$(n arranca)" "1/0/1" "sin lanzador y sin volver a abrirla" ;;
actualiza)   # rocola-actualiza: el canal del programa apagado (nada de programa-N) y el sistema como siempre (con el texto nuevo)
	rm -rf /gh/assets /gh/api.log; mkdir -p /gh/assets
	echo "sistema de prueba" | gzip > /gh/assets/201; echo nucleo > /gh/assets/202; echo initrd > /gh/assets/203
	( cd /gh/assets && cp 201 sistema.img.gz && cp 202 bzImage && cp 203 initrd.gz && sha256sum sistema.img.gz bzImage initrd.gz > 204 && rm sistema.img.gz bzImage initrd.gz )
	echo programa > /gh/assets/101
	cat > /gh/releases.json <<'EOF'
[{"tag_name":"programa-99","draft":false,"published_at":"2026-11-13T00:00:00Z","assets":[{"name":"rocola-consola-a4.gz","id":101},{"name":"rocola-consola-generico.gz","id":101},{"name":"sha256.txt","id":101}]},
 {"tag_name":"sistema-8","draft":false,"published_at":"2026-11-13T00:00:00Z","assets":[{"name":"sistema.img.gz","id":201},{"name":"bzImage","id":202},{"name":"initrd.gz","id":203},{"name":"sha256.txt","id":204}]}]
EOF
	# (el disco de la rocola, montado: df tiene que encontrarlo para ver si caben los 1,4 GB)
	mkdir -p /rocola/disco; mount -t tmpfs -o size=2g tmpfs /rocola/disco
	echo 7 > /etc/rocola-sistema-version; mkdir -p /rocola/disco/rocola-os/a /rocola/disco/rocola-os/b
	printf 'set rocola_slot=a\nset rocola_otro=b\n' > /rocola/disco/rocola-os/arranque.cfg
	A="ROCOLA_API=$API ROCOLA_SIN_HORA=1 ROCOLA_RUN=/run ROCOLA_DATOS=/rocola/datos ROCOLA_DISCO=/rocola/disco"
	env $A rocola-actualiza; sed 's/^/    api: /' /gh/api.log; echo "    dice: $(cat /run/rocola-actualiza.txt)"; bit
	ok "$(grep -c 'assets/101' /gh/api.log)/$(si -e /rocola/datos/programa)" "0/no" "del programa (programa-99) no se baja nada, ni se crea su carpeta"
	ok "$(sed 's/^[0-9:]* //' /run/rocola-actualiza.txt)" "programa rocola-xp 0 (llega con el sistema); sistema 8 listo (entra al siguiente encendido)" "lo que dice"
	ok "$(cat /rocola/disco/rocola-os/b/sistema.img)/$(sed -n 's/^set rocola_slot=//p' /rocola/disco/rocola-os/arranque.cfg)/$(cat /rocola/disco/rocola-os/probando)" "sistema de prueba/b/0" "el sistema nuevo, como siempre"
	ok "$(n 'Bajado el sistema 8: entra al siguiente encendido' /rocola/datos/rocola_actualizaciones.txt)" 1 "en la bitacora"
	echo "  -- con lo que bajo rocola-consola en /rocola/datos/programa: no se toca ni se usa"
	mkdir -p /rocola/datos/programa; echo 57 > /rocola/datos/programa/version; echo x > /rocola/datos/programa/rocola-consola.nueva
	env $A rocola-actualiza
	ok "$(cat /rocola/datos/programa/version)/$(ls /rocola/datos/programa | tr '\n' ' ')" "57/rocola-consola.nueva version " "se queda como estaba"
	ok "$(sed 's/^[0-9:]* //' /run/rocola-actualiza.txt)" "programa rocola-xp 0 (llega con el sistema); sistema nuevo en prueba" "lo que dice" ;;
esac
limpia
