#!/bin/sh
# (dentro de la raiz de prueba: lo corre prueba.sh) la prueba del lanzador con programas falsos y el BusyBox del sistema
mount -t proc proc /proc
export ROCOLA_ASOUND=/asound ROCOLA_VT_ACTIVA=/run/vt PATH=/usr/bin:/bin
limpia() { killall rocola rocola-antes 2>/dev/null; killall -KILL rocola-consola rocola-xp sleep 2>/dev/null; sleep 1; rm -rf /run/* /tmp/falso.* /rocola/datos/* /root/rocola.txt; echo tty1 > /run/vt; }
log() { sed 's/^/    /' /tmp/falso.log 2>/dev/null; }
reg() { grep -v '^--- [A-Z][a-z][a-z] ' /rocola/datos/rocola.txt | sed 's/^/    | /'; }
lanza() { setsid "${1:-rocola}" </dev/null >/dev/null 2>&1 & }
limpia
case "$1" in
fabrica)   # sin interruptor: lo de hoy, y lo mismo que el lanzador de antes
	for l in rocola-antes rocola; do
		limpia; echo "42 3" > /tmp/falso.sale; lanza $l; sleep 4; echo "0 600" > /tmp/falso.sale; sleep 4
		echo tty2 > /run/vt; sleep 24; echo "  ($l) en la consola: abiertos=[$(pidof rocola-consola rocola-xp)] aviso=$(ls /run | tr '\n' ' ')"; echo tty1 > /run/vt; sleep 4
		echo "  == $l"; log; reg; cp /tmp/falso.log /tmp/fab-$l.log; grep -v '^--- [A-Z][a-z][a-z] ' /rocola/datos/rocola.txt > /tmp/fab-$l.reg; sed 's/^[0-9: -]*//' /rocola/datos/rocola_actualizaciones.txt > /tmp/fab-$l.bit
	done
	for x in log reg bit; do sed 's/rocola-antes/rocola/' /tmp/fab-rocola-antes.$x | cmp -s - /tmp/fab-rocola.$x && echo "  IGUAL ($x)" || echo "  DISTINTO ($x)"; done ;;
xp)
	mkdir -p "/rocola/disco/WINDOWS/Fonts" "/rocola/disco/Rocola/Release" /rocola/disco/Rocola/musica "/rocola/discos/sdb1/Mi musica"
	printf 'teclas\r\n' > /rocola/disco/Rocola/Release/Rocola_Teclas.txt; printf 'noche=1\r\ndir=D:\\musica2' > /rocola/disco/Rocola/Release/rocola_config.txt
	echo /rocola/disco/Rocola/Release > /run/rocola-datos-xp; printf '/rocola/disco/Rocola/musica\n/rocola/discos/sdb1/Mi musica\n/no/esta\n' > /run/rocola-musica
	echo "programa=xp" > /rocola/datos/rocola_programa.txt
	lanza; sleep 3; echo "  -- arranco:"; log; reg; echo "  datos/xp: $(ls /rocola/datos/xp | tr '\n' ' ')"; od -c /rocola/datos/xp/rocola_config.txt | sed 's/^/    /'
	echo "  -- consola (tty2): debe cerrarse con USR1, no SIGTERM"; echo tty2 > /run/vt; sleep 4; echo "  abiertos=[$(pidof rocola-consola rocola-xp)] run=$(ls /run | tr '\n' ' ')"; log | tail -2
	echo tty1 > /run/vt; sleep 5; echo "  -- de vuelta a tty1: abiertos=[$(pidof rocola-xp)]"; log | tail -1
	echo "  -- reiniciar el programa ya (buzon)"; touch /run/rocola-reinicia-programa-ya; sleep 9; echo "  aviso quitado: $([ -f /run/rocola-reinicia-programa-ya ] && echo NO || echo si)"; log | tail -2
	echo "  -- segunda vez no repite dir= ni copia:"; od -c /rocola/datos/xp/rocola_config.txt | tail -3 | sed 's/^/    /'; cat /rocola/datos/xp/rocola_de_xp.txt | sed 's/^/    /'
	echo "  -- boton de encendido (SIGTERM a rocola-xp):"; killall -TERM rocola; killall -TERM rocola-xp; sleep 1; log | tail -1
	reg; sed 's/^/    bitacora: /' /rocola/datos/rocola_actualizaciones.txt 2>/dev/null ;;
caidas)   # rocola-xp se cae 3 veces seguidas: al programa de siempre
	echo "programa=xp" > /rocola/datos/rocola_programa.txt; echo "134 0" > /tmp/falso.sale
	lanza; sleep 9; echo "0 600" > /tmp/falso.sale; sleep 4; log; reg; sed 's/^/    bitacora: /' /rocola/datos/rocola_actualizaciones.txt; echo "  run: $(ls /run | tr '\n' ' ')" ;;
datosxp)
	mkdir -p /rocola/disco/Rocola/Release; echo /rocola/disco/Rocola/Release > /run/rocola-datos-xp; : > /run/rocola-musica
	printf 'programa=xp\ndatos=xp\n' > /rocola/datos/rocola_programa.txt
	lanza; sleep 3; log; ls -a /rocola/disco/Rocola/Release | tr '\n' ' '; echo
	limpia; echo /rocola/no-existe > /run/rocola-datos-xp; printf 'programa=xp\ndatos=xp\n' > /rocola/datos/rocola_programa.txt; lanza; sleep 3; log; reg ;;
buzon)   # «reiniciar el equipo ya» y «apagar ya» de rocola-buzon con rocola-xp abierta: el trozo del script (sin el reboot ni el poweroff)
	sed -n '/^\t\tif pidof rocola-xp >\/dev\/null; then$/,/^\t\tfi$/p' /usr/bin/rocola-buzon > /tmp/trozo.sh; echo "  (trozo de rocola-buzon: $(wc -l < /tmp/trozo.sh) lineas)"
	for que in reinicia apaga; do
		limpia; echo "programa=xp" > /rocola/datos/rocola_programa.txt; lanza; sleep 3
		que=$que sh /tmp/trozo.sh; sleep 3; echo "  -- $que: abiertos=[$(pidof rocola rocola-xp)] (nadie: ni el lanzador la vuelve a abrir)"; log | tail -1
	done
	limpia; lanza; sleep 3; que=reinicia sh /tmp/trozo.sh; echo "  -- sin rocola-xp: sigue rocola-consola=[$(pidof rocola-consola)] y el lanzador=[$(pidof rocola)]" ;;
sinprograma)   # el interruptor encendido en un sistema que no trae rocola-xp
	mv /usr/lib/rocola-xp/rocola-xp /usr/lib/rocola-xp/x; echo "programa=xp" > /rocola/datos/rocola_programa.txt; lanza; sleep 3; log; mv /usr/lib/rocola-xp/x /usr/lib/rocola-xp/rocola-xp ;;
esac
limpia
case "$1" in
otracarpeta)
	limpia; echo /rocola/disco/Rocola/Release > /run/rocola-datos-xp; : > /run/rocola-musica
	printf 'programa=xp\r\ndatos=/rocola/disco/rocola-os/xp\r\n' > /rocola/datos/rocola_programa.txt
	lanza; sleep 3; log; reg; ls /rocola/disco/rocola-os/xp | tr '\n' ' '; echo; limpia ;;
esac
