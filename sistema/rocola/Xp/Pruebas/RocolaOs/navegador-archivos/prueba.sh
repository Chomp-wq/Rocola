#!/bin/bash
# LA PRUEBA DEL NAVEGADOR Y DEL GESTOR DE ARCHIVOS DE CTRL+F4 (fase 11, grupo navegador-archivos: RocolaOs/RocolaOs.Navegador.cs,
# RocolaOs.Archivos*.cs, los ganchos g100 y g101, rootfs_overlay/usr/bin/rocola-navegador; FASE11_navegador-archivos.md). Con datos
# INVENTADOS (los de partida de las escenas de ventanas, DATOS0), una memoria USB DE MENTIRA (una carpeta: ROCOLA_ARCHIVOS_USB), llaves de
# mentira y un cog DE MENTIRA (un guion que apunta como lo abrieron y espera a que lo cierren); fuera de pantalla y sin sonido. NADA de esto
# es el A4, ni WebKit, ni una tarjeta de verdad.
#   bash prueba.sh PROGRAMA [orden guion archivos navegador caidas sin]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT, con el ejecutable rocola-xp)
#   W: la carpeta de trabajo (de fabrica /root/rocola-trabajo/fase11/navegador-archivos/prueba); se rehace en cada corrida
#   CAPTURAS: donde se copian las capturas que se ensenan (de fabrica, ninguna)
#   DATOS0, TARGET (el sistema compilado, para el BusyBox de «guion»)
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd); SIS=$(cd "$XP/../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-orden guion archivos navegador caidas sin}"
W=${W:-/root/rocola-trabajo/fase11/navegador-archivos/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
TARGET=${TARGET:-/root/rocola-trabajo/salida-qemu/target}
CAPTURAS=${CAPTURAS:-}
NAV="$SIS/board/rocola/rootfs_overlay/usr/bin/rocola-navegador"
INICIO="$SIS/board/rocola/rootfs_overlay/usr/share/rocola/navegador/inicio.html"
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "${W:?}"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
modo() { stat -c %a "$1" 2>/dev/null; }   # (en la maquina de pruebas; los guiones del sistema no usan stat)
medida() { python3 -c "import struct,sys; d=open(sys.argv[1],'rb').read(24); print('%dx%d' % struct.unpack('>II', d[16:24]))" "$1" 2>/dev/null; }
guarda_captura() { [ -n "$CAPTURAS" ] && [ -f "$1" ] && { mkdir -p "$CAPTURAS"; cp "$1" "$CAPTURAS/$2"; }; return 0; }
escribe() { python3 "$G/teclas.py" "$@"; }

# las llaves de mentira (nada real)
API_PRUEBA=$'client_id=IDDEPRUEBA0123456789\nclient_secret=SECRETODEPRUEBA9876543210\n'
SOLOIST_PRUEBA=$'LLAVEDESOLOIST-prueba-000111\n'
GENIUS_PRUEBA='GENIUS-prueba_125=ok/x'
export API_PRUEBA SOLOIST_PRUEBA GENIUS_PRUEBA

# ------------------------------------------------------------------ orden: la regla de no salir, las llaves, copiar, guardar (--prueba-archivos)
if [[ " $partes " == *" orden "* ]]; then
  echo "== orden (rocola-xp --prueba-archivos: la regla de no salir de las carpetas, las llaves, copiar, guardar, renombrar, borrar)"
  mkdir -p "$W/orden"
  env ROCOLA_PANTALLA=fuera:64x64 ROCOLA_DATOS="$W/orden/datos" timeout 60 "${RX[@]}" --prueba-archivos "$W/orden/datos" "$W/orden/usb" "$W/orden/FUERA" 2>&1 | grep -v '^Pantalla: ' > "$W/orden.txt"
  r=${PIPESTATUS[0]}
  sed 's/^/   /' "$W/orden.txt"
  si "--prueba-archivos sale con 0 ($(tail -1 "$W/orden.txt"))" test "$r" = 0
fi

# ------------------------------------------------------------------ guion: rocola-navegador con el BusyBox del sistema (chroot)
if [[ " $partes " == *" guion "* ]]; then
  echo "== guion (rocola-navegador con el BusyBox de $TARGET)"
  if [ ! -x "$TARGET/bin/busybox" ] || [ "$(id -u)" != 0 ]; then echo "NO    sin $TARGET/bin/busybox o sin root: no se prueba el guion"; mal=$((mal + 1))
  else
    R="$W/raiz"; mkdir -p "$R/bin" "$R/lib" "$R/usr/bin" "$R/run" "$R/rocola/datos/xp" "$R/usr/share/rocola/navegador" "$R/dev"
    cp "$TARGET/bin/busybox" "$R/bin/"; for l in ld-linux-x86-64.so.2 libc.so.6 libm.so.6 libresolv.so.2; do cp -L "$TARGET/lib/$l" "$R/lib/" 2>/dev/null; done
    ln -s lib "$R/lib64"; mknod "$R/dev/null" c 1 3; mknod "$R/dev/zero" c 1 5; chroot "$R" /bin/busybox --install -s /bin
    cp "$NAV" "$R/usr/bin/"; cp "$INICIO" "$R/usr/share/rocola/navegador/"
    # el cog de mentira: apunta sus argumentos y lo que le importa del entorno; con TERM sale con 0 (como cog); «falla» sale con 5 al momento
    cat > "$R/usr/bin/cog" <<'EOF'
#!/bin/sh
echo "cog $* | HOME=$HOME XDG_DATA_HOME=$XDG_DATA_HOME XDG_CACHE_HOME=$XDG_CACHE_HOME XKB=$XKB_DEFAULT_LAYOUT" >> /run/cog.txt
[ -f /run/cog-falla ] && exit 5
trap 'echo TERM >> /run/cog.txt; exit 0' TERM
# las descargas como WebKit: la carpeta de descargas de XDG (glib la lee de $XDG_CONFIG_HOME/user-dirs.dirs), a «NOMBRE.wkdownload» y al
# acabar con su nombre; /run/cog-baja dice «NOMBRE KB» (de 256 en 256 KB cada 0,2 s)
D=$(. "$XDG_CONFIG_HOME/user-dirs.dirs" 2>/dev/null; echo "$XDG_DOWNLOAD_DIR")
echo "descargas: $D" >> /run/cog.txt
if [ -f /run/cog-baja ]; then
  read n kb < /run/cog-baja
  i=0; while [ $i -lt $((kb / 256)) ]; do dd if=/dev/zero bs=1024 count=256 >> "$D/$n.wkdownload" 2>/dev/null; i=$((i + 1)); sleep 0.2; done
  mv "$D/$n.wkdownload" "$D/$n"; echo "bajado $n" >> /run/cog.txt
fi
while :; do sleep 1; done
EOF
    chmod 755 "$R/usr/bin/cog"
    c() { chroot "$R" /bin/env ROCOLA_RUN=/run ROCOLA_NAVEGADOR_TTY=/dev/null "$@"; }
    si "sh -n con el BusyBox del sistema" chroot "$R" /bin/sh -n /usr/bin/rocola-navegador
    si "no usa stat (BusyBox sin CONFIG_STAT)" bash -c "! grep -E '(^|[;|&(\`]\s*)stat ' '$NAV' | grep -v '^\s*#' | grep -q ."
    mv "$R/usr/bin/cog" "$R/usr/bin/cog.no"; c /bin/sh /usr/bin/rocola-navegador hay > "$W/g-hay0.txt" 2>&1; r0=$?; mv "$R/usr/bin/cog.no" "$R/usr/bin/cog"
    c /bin/sh /usr/bin/rocola-navegador hay > "$W/g-hay1.txt" 2>&1; r1=$?
    si "hay: sin cog sale con 1 y dice que llega con la completa; con cog, 0" bash -c "[ $r0 = 1 ] && grep -q 'compilacion completa' '$W/g-hay0.txt' && [ $r1 = 0 ]"
    # abre: sin URL, la pagina de inicio; los datos en /rocola/datos/xp/navegador (0700); SIGTERM lo cierra y sale con lo de cog
    ( c /bin/sh /usr/bin/rocola-navegador abre > "$W/g-abre.txt" 2>&1; echo $? > "$W/g-abre.salio" ) &
    sleep 2
    p=$(cat "$R/run/rocola-navegador.pid" 2>/dev/null)
    si "abre: cog con la pagina de inicio, --platform=drm, las galletas en sqlite y sus datos en navegador/ (XDG_*), teclado us" bash -c "grep -qF 'cog --platform=drm --cookie-jar=sqlite:/rocola/datos/xp/navegador/galletas.sqlite file:///usr/share/rocola/navegador/inicio.html | HOME=/rocola/datos/xp/navegador XDG_DATA_HOME=/rocola/datos/xp/navegador/datos XDG_CACHE_HOME=/rocola/datos/xp/navegador/cache XKB=us' '$R/run/cog.txt'"
    si "abre: la carpeta de datos 0700 y el pid de cog en /run" bash -c "[ \"\$(stat -c %a '$R/rocola/datos/xp/navegador')\" = 700 ] && [ -n '$p' ]"
    si "abre: las descargas a /rocola/datos/xp/descargas (0700) por user-dirs.dirs (XDG_DOWNLOAD_DIR), y cog (glib) la lee de ahi" bash -c "[ \"\$(cat '$R/rocola/datos/xp/navegador/config/user-dirs.dirs')\" = 'XDG_DOWNLOAD_DIR=\"/rocola/datos/xp/descargas\"' ] && [ \"\$(stat -c %a '$R/rocola/datos/xp/descargas')\" = 700 ] && grep -qx 'descargas: /rocola/datos/xp/descargas' '$R/run/cog.txt' && grep -q 'descargas en /rocola/datos/xp/descargas, tope 64 MB' '$R/run/rocola-navegador.txt'"
    c /bin/sh /usr/bin/rocola-navegador abre > "$W/g-abre2.txt" 2>&1; r2=$?
    si "abre otra vez con uno abierto: no (sale con 4)" test $r2 = 4
    c /bin/sh /usr/bin/rocola-navegador olvida > "$W/g-olv0.txt" 2>&1; r3=$?
    si "olvida con el navegador abierto: no (sale con 1)" test $r3 = 1
    pkill -TERM -f "^/bin/sh /usr/bin/rocola-navegador abre" 2>/dev/null; sleep 2
    si "SIGTERM al guion: cog recibe TERM, el guion sale con 0 y quita el pid" bash -c "grep -qx TERM '$R/run/cog.txt' && [ \"\$(cat '$W/g-abre.salio')\" = 0 ] && [ ! -e '$R/run/rocola-navegador.pid' ]"
    si "lo que dice cog va a /run/rocola-navegador.txt (0600), no a los datos" bash -c "grep -q 'cerrado (codigo 0)' '$R/run/rocola-navegador.txt' && [ \"\$(stat -c %a '$R/run/rocola-navegador.txt')\" = 600 ] && ! ls '$R/rocola/datos/xp' | grep -qvE '^(navegador|descargas)\$'"
    # con URL y otro teclado
    : > "$R/run/cog.txt"
    ( chroot "$R" /bin/env ROCOLA_RUN=/run ROCOLA_NAVEGADOR_TTY=/dev/null ROCOLA_NAVEGADOR_TECLADO=latam /bin/sh /usr/bin/rocola-navegador abre https://claude.ai/ > /dev/null 2>&1 ) &
    sleep 2; pkill -TERM -f "^/bin/sh /usr/bin/rocola-navegador abre" 2>/dev/null; sleep 2
    si "abre URL: cog con esa direccion y el teclado latam" bash -c "grep -qF 'https://claude.ai/ | ' '$R/run/cog.txt' && grep -qF 'XKB=latam' '$R/run/cog.txt'"
    # el tope: una cache de 3 MB con tope de 1 MB se borra al abrir; la sesion y las galletas se quedan
    mkdir -p "$R/rocola/datos/xp/navegador/cache" "$R/rocola/datos/xp/navegador/datos"
    dd if=/dev/zero of="$R/rocola/datos/xp/navegador/cache/grande" bs=1M count=3 2>/dev/null; echo galleta > "$R/rocola/datos/xp/navegador/galletas.sqlite"; echo s > "$R/rocola/datos/xp/navegador/datos/sesion"
    ( chroot "$R" /bin/env ROCOLA_RUN=/run ROCOLA_NAVEGADOR_TTY=/dev/null ROCOLA_NAVEGADOR_TOPE_MB=1 /bin/sh /usr/bin/rocola-navegador abre > /dev/null 2>&1 ) &
    sleep 2; pkill -TERM -f "^/bin/sh /usr/bin/rocola-navegador abre" 2>/dev/null; sleep 2
    si "el tope (1 MB): la cache de 3 MB se borra al abrir; la sesion y las galletas se quedan; se dice" bash -c "[ ! -e '$R/rocola/datos/xp/navegador/cache' ] && [ -f '$R/rocola/datos/xp/navegador/galletas.sqlite' ] && [ -f '$R/rocola/datos/xp/navegador/datos/sesion' ] && grep -q 'se borro la cache' '$R/run/rocola-navegador.txt'"
    c /bin/sh /usr/bin/rocola-navegador cuanto > "$W/g-cuanto.txt" 2>&1
    si "cuanto: los KB y el tope" bash -c "grep -qE '^[0-9]+ KB en /rocola/datos/xp/navegador \(tope 128 MB\)' '$W/g-cuanto.txt'"
    c /bin/sh /usr/bin/rocola-navegador olvida > "$W/g-olv.txt" 2>&1; r4=$?
    si "olvida: la carpeta queda vacia (sale con 0)" bash -c "[ $r4 = 0 ] && [ -d '$R/rocola/datos/xp/navegador' ] && [ -z \"\$(ls -A '$R/rocola/datos/xp/navegador')\" ]"
    # el dueno (rocola-xp) se cae: cog se cierra solo
    : > "$R/run/cog.txt"
    chroot "$R" /bin/sh -c 'sleep 300' & dueno=$!
    sleep 0.5; dp=$(pgrep -f "^sleep 300" | head -1)
    ( chroot "$R" /bin/env ROCOLA_RUN=/run ROCOLA_NAVEGADOR_TTY=/dev/null ROCOLA_NAVEGADOR_DUENO="$dp" /bin/sh /usr/bin/rocola-navegador abre > /dev/null 2>&1; echo $? > "$W/g-dueno.salio" ) &
    sleep 2; t0=$(date +%s.%N); kill -9 "$dp" "$dueno" 2>/dev/null
    i=0; while [ ! -s "$W/g-dueno.salio" ] && [ $i -lt 100 ]; do sleep 0.1; i=$((i + 1)); done; t1=$(date +%s.%N)
    s=$(python3 -c "print('%.1f' % ($t1 - $t0))")
    si "si el dueno (rocola-xp) se cae, cog se cierra solo ($s s) y lo dice" bash -c "grep -qx TERM '$R/run/cog.txt' && grep -q 'rocola-xp ya no esta' '$R/run/rocola-navegador.txt' && python3 -c 'import sys; sys.exit(0 if $s < 4 else 1)'"
    # LAS DESCARGAS Y SU TOPE: una que pasa el tope (1 MB) se para: cog fuera, lo que bajaba fuera, lo ya bajado se queda; sale con 6
    DX="$R/rocola/datos/xp/descargas"
    head -c 100000 /dev/zero > "$DX/viejo.bin"; touch -d '-1 hour' "$DX/viejo.bin"
    : > "$R/run/cog.txt"; echo "grande.bin 4096" > "$R/run/cog-baja"
    t0=$(date +%s.%N)
    chroot "$R" /bin/env ROCOLA_RUN=/run ROCOLA_NAVEGADOR_TTY=/dev/null ROCOLA_NAVEGADOR_DESCARGAS_TOPE_MB=1 /bin/sh /usr/bin/rocola-navegador abre > /dev/null 2>&1 & gp=$!
    i=0; while kill -0 $gp 2>/dev/null && [ $i -lt 150 ]; do sleep 0.1; i=$((i + 1)); done; wait $gp; r8=$?; t1=$(date +%s.%N)
    s=$(python3 -c "print('%.1f' % ($t1 - $t0))")
    si "una descarga que pasa el tope (1 MB): cog fuera en $s s, sale con 6, lo que bajaba borrado, lo ya bajado se queda, y lo dice" bash -c "[ $r8 = 6 ] && grep -qx TERM '$R/run/cog.txt' && ! ls '$DX' | grep -q grande && [ -f '$DX/viejo.bin' ] && grep -q 'pasaban su tope de 1 MB' '$R/run/rocola-navegador.txt' && ! grep -q '^bajado' '$R/run/cog.txt' && [ ! -e '$R/run/rocola-navegador.tope' ]"
    # una que cabe: se queda
    : > "$R/run/cog.txt"; echo "chica.bin 512" > "$R/run/cog-baja"
    ( c /bin/sh /usr/bin/rocola-navegador abre > /dev/null 2>&1; echo $? > "$W/g-chica.salio" ) &
    sleep 3; pkill -TERM -f "^/bin/sh /usr/bin/rocola-navegador abre" 2>/dev/null; sleep 2
    si "una descarga que cabe (512 KB, tope 64 MB): se queda con su nombre y el guion sale con 0" bash -c "grep -q '^bajado chica.bin' '$R/run/cog.txt' && [ \"\$(wc -c < '$DX/chica.bin')\" = 524288 ] && [ \"\$(cat '$W/g-chica.salio')\" = 0 ]"
    # los datos que se llenan (el piso de lo libre): tambien se para
    # (df necesita /proc/mounts y que los datos sean un montaje, como en el sistema: en el chroot, /proc y los datos montados sobre si mismos)
    mkdir -p "$R/proc"; mount -t proc proc "$R/proc"; mount --bind "$R/rocola/datos" "$R/rocola/datos"
    : > "$R/run/cog.txt"; echo "otra.bin 2048" > "$R/run/cog-baja"
    timeout 30 chroot "$R" /bin/env ROCOLA_RUN=/run ROCOLA_NAVEGADOR_TTY=/dev/null ROCOLA_DESCARGAS_PISO_KB=999999999 /bin/sh /usr/bin/rocola-navegador abre > /dev/null 2>&1; r9=$?
    umount "$R/rocola/datos"; umount "$R/proc"
    si "con los datos casi llenos (menos libre que el piso) una descarga se para igual (sale con 6, lo dice)" bash -c "[ $r9 = 6 ] && ! ls '$DX' | grep -q otra && grep -q 'en los datos quedaban' '$R/run/rocola-navegador.txt' && [ -f '$DX/chica.bin' ]"
    rm -f "$R/run/cog-baja"
    # las descargas que son un enlace a otro lado: se quita el enlace (lo de fuera no se toca) y se hace la carpeta
    mkdir -p "$R/fuera"; echo nada > "$R/fuera/x"; mv "$DX" "$DX.bien"; ln -s /fuera "$DX"
    ( c /bin/sh /usr/bin/rocola-navegador abre > /dev/null 2>&1 ) &
    sleep 2; pkill -TERM -f "^/bin/sh /usr/bin/rocola-navegador abre" 2>/dev/null; sleep 2
    si "si las descargas son un enlace a otro lado: el enlace fuera, carpeta nueva 0700, lo de fuera intacto" bash -c "[ ! -L '$DX' ] && [ -d '$DX' ] && [ \"\$(stat -c %a '$DX')\" = 700 ] && [ \"\$(ls '$R/fuera')\" = x ]"
    rm -rf "$DX"; mv "$DX.bien" "$DX"
    c /bin/sh /usr/bin/rocola-navegador cuanto > "$W/g-cuanto2.txt" 2>&1
    si "cuanto: tambien las descargas y su tope" grep -qE '^[0-9]+ KB en /rocola/datos/xp/descargas \(tope 64 MB\)' "$W/g-cuanto2.txt"
    si "olvida no toca las descargas" bash -c "c() { chroot '$R' /bin/env ROCOLA_RUN=/run /bin/sh /usr/bin/rocola-navegador \"\$@\"; }; c olvida > /dev/null && [ -f '$DX/chica.bin' ] && [ -f '$DX/viejo.bin' ]"
    # cog que se cae al abrir: el guion devuelve su codigo
    touch "$R/run/cog-falla"; c /bin/sh /usr/bin/rocola-navegador abre > /dev/null 2>&1; r5=$?; rm -f "$R/run/cog-falla"
    si "cog que se cae al abrir (5): el guion sale con 5 y no deja el pid" bash -c "[ $r5 = 5 ] && [ ! -e '$R/run/rocola-navegador.pid' ]"
    c /bin/sh /usr/bin/rocola-navegador > /dev/null 2>&1; r6=$?; c /bin/sh /usr/bin/rocola-navegador otra > /dev/null 2>&1; r7=$?
    si "sin orden: el uso y 0; con una que no es: 2" test $r6 = 0 -a $r7 = 2
    # rocola-buzon y rocola-actualiza: siguen siendo validos, toman las llaves de /rocola/datos/xp (donde las deja Archivos) y el buzon NO
    # sube ningun archivo que Archivos tenga por llave (permitido: su funcion, sacada del guion y corrida con el BusyBox del sistema)
    cp "$SIS/board/rocola/rootfs_overlay/usr/bin/rocola-buzon" "$SIS/board/rocola/rootfs_overlay/usr/bin/rocola-actualiza" "$R/usr/bin/"
    si "rocola-buzon y rocola-actualiza: sh -n con el BusyBox del sistema" bash -c "chroot '$R' /bin/sh -n /usr/bin/rocola-buzon && chroot '$R' /bin/sh -n /usr/bin/rocola-actualiza"
    si "las dos miran tambien \$XPD (la del buzon para rocola_buzon.txt y rocola_genius.txt; la de actualiza para rocola_github.txt)" bash -c "grep -qF '[ -s \"\$XPD/\$nom\" ] && { echo \"\$XPD/\$nom\"; return; }' '$R/usr/bin/rocola-buzon' && grep -qF '[ -s \"\$XPD/rocola_github.txt\" ]' '$R/usr/bin/rocola-actualiza'"
    sed -n '/^permitido() {/,/^}/p' "$R/usr/bin/rocola-buzon" > "$R/permitido.sh"
    printf '. /permitido.sh\nfor n in "$@"; do permitido "$n" && echo "si $n" || echo "no $n"; done\n' > "$R/p.sh"
    chroot "$R" /bin/sh /p.sh rocola_error.txt rocola_os_bitacora.txt rocola_spotify.txt rocola_config.txt rocola_calidad.txt rocola_navegador.txt \
      rocola_genius.txt rocola_spotify_llave.txt rocola_spotify_api.txt rocola_mi_token.txt rocola_claude_key.txt rocola_secreto.txt rocola_clave.txt rocola_API.txt rocola_password.txt > "$W/g-perm.txt" 2>&1
    sed 's/^/   /' "$W/g-perm.txt" | tr '\n' ' '; echo
    si "el buzon sigue subiendo las bitacoras y no sube ninguna llave (tampoco las de nombre nuevo: token, key, secret, clave, API, password)" bash -c "[ \$(grep -c '^si ' '$W/g-perm.txt') = 6 ] && [ \$(grep -c '^no ' '$W/g-perm.txt') = 9 ] && ! grep -q '^si rocola_.*\(genius\|llave\|api\|token\|key\|secret\|clave\|API\|password\)' '$W/g-perm.txt'"
    # la pagina de inicio: sin nada de fuera (ni scripts ni letras de internet)
    si "inicio.html: sin scripts, estilos ni letras de fuera" bash -c "! grep -qiE '<script[^>]+src=|<link[^>]+href=|@import|url\\(' '$INICIO'"
  fi
fi

# ------------------------------------------------------------------ las escenas (la rocola entera fuera de pantalla)
# (la moneda en el «5», la de un teclado sin rocola_teclas.txt: la de DATOS0 es la «C», que se comeria la «c» de lo que se escribe en la
# lista de Ctrl+F4, que cuenta monedas como Ctrl+F3)
datos() { local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/; printf 'spotify=0\n' > "$d/rocola_os.txt"; sed -i 's/^moneda=.*/moneda=53/' "$d/rocola_teclas.txt"; echo "$d"; }
abre() {
  local n="$1" d="$2" pulsa="$3" caps="$4" seg="$5" sen="$6"; shift 6
  local o="$W/$n"; mkdir -p "$o/run"
  local c=""; IFS=',' read -ra L <<< "$caps"
  for x in "${L[@]}"; do [ -z "$x" ] && continue; c="$c,${x%%:*}:$o/${x#*:}.png"; done; c="${c#,}"
  ( cd "$d" && exec env ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=null ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA="$c" \
      ROCOLA_CAPTURA_SALE=0 "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$!
  echo $p > "$o/pid"
  if [ "$sen" != NADA ]; then sleep "$seg"; kill -"$sen" $p 2>/dev/null; fi
  ( sleep $((seg + 40)); kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
}
# la USB de mentira y lo de fuera
usb() {
  local u="$W/usb-$1" f="$W/FUERA-$1"; rm -rf "$u" "$f"; mkdir -p "$u/carpeta" "$f"
  echo "lo de fuera" > "$f/secreto.txt"
  printf '%s' "$API_PRUEBA" > "$u/rocola_spotify_api.txt"; printf '%s' "$SOLOIST_PRUEBA" > "$u/rocola_spotify.txt"; echo "texto" > "$u/leeme.txt"
  head -c 70000 /dev/zero | tr '\0' 'x' > "$u/grande.txt"
  ln -s "$f" "$u/fuga"; ln -s "$f/secreto.txt" "$u/fuga.txt"
  echo "$u"
}

# Coordenadas (1024x768): la ventana de Ctrl+F4 en (29,2), su cliente desde (32,31); las pestanas Pantalla, Navegador y Archivos en
# (712,54), (818,54) y (924,54); la pagina desde (32,127). Archivos: «Datos de la rocola» (137,148), «Descargas» (323,148), «Papelera»
# (509,148), la 1a USB (695,148) (en la papelera los botones son Ver, -, Devolver, -, Borrar ya, Vaciar papelera); la lista, renglon k
# en y = 235 + 24k; los botones Abrir (113,620), Subir (246,620), Copiar (409,620), Renombrar (587,620), Borrar (735,620), Archivo nuevo
# (888,620). El editor (920x660) con su cliente desde (52,67): Guardar (184,691), Mostrar (420,691), Cerrar (840,691).
if [[ " $partes " == *" archivos "* ]]; then
  echo "== archivos (ver, copiar de la USB, la llave tapada y «Mostrar», un archivo nuevo con el teclado, renombrar, borrar a la papelera y devolver; las descargas; vaciar la papelera con y sin confirmar)"
  d=$(datos archivos); u=$(usb archivos); o="$W/archivos"; f="$W/FUERA-archivos"
  printf 'hola\r\nmundo\r\n' > "$d/notas.txt"
  ln -s "$f" "$d/fuga"
  # lo que «bajo el navegador»: un texto y una llave (0600)
  mkdir -p "$d/descargas"; chmod 700 "$d/descargas"; echo "de internet" > "$d/descargas/bajado.txt"
  printf 'token=TOKENBAJADODEPRUEBA-0123456789\n' > "$d/descargas/token_bajado.txt"; chmod 600 "$d/descargas/token_bajado.txt"
  P="6:17+115,8:clic:924:54,10:clic:695:148"
  P="$P,12:clic:300:331,12.5:clic:409:620,14:clic:300:307,14.5:clic:409:620,16:clic:300:259,16.5:clic:113:620"
  P="$P,18:clic:137:148,$(escribe 19 0.12 rocola_spotify_a),21.5:13,23.5:clic:420:691,25:9,25.3:13,27:27"
  P="$P,28:clic:888:620,$(escribe 29 0.12 rocola_genius.txt),32:13,$(escribe 33 0.12 "$GENIUS_PRUEBA"),36.5:17+83,37.5:27"
  P="$P,38.5:clic:300:235,$(escribe 39 0.12 notas),39.8:clic:587:620,40.8:8,41:8,41.2:8,41.4:8,$(escribe 41.6 0.12 2.txt),42.5:13"
  P="$P,43.5:clic:735:620,45:9,45.3:13"
  # la papelera: notas2.txt, devolver. Las descargas: copiar bajado.txt a los datos; bajado.txt y la llave, a la papelera
  P="$P,47:clic:509:148,49:clic:300:235,49.5:clic:409:620"
  P="$P,52:clic:323:148,54:clic:300:235,54.5:clic:409:620,57:clic:735:620,58.5:9,58.8:13,60:clic:300:259,60.5:clic:735:620,62:9,62.3:13"
  # la papelera con las dos: ver la llave (tapada, sin «Mostrar»); vaciar: primero «No» (Intro con el foco en No), luego «Sí»
  P="$P,64:clic:509:148,66:clic:300:235,66.5:clic:113:620,69:27,70:clic:888:620,71.5:13,73:clic:888:620,74:9,74.3:13,77:27"
  abre archivos "$d" "$P" "9.5:archivos-datos,11.5:archivos-usb,13.5:archivos-copiada,17.5:archivos-grande,23:llave-tapada,24.5:mostrar-confirma,26.5:llave-mostrada,31.5:nombre-nuevo,37:genius-guardada,44.5:borrar-confirma,46.5:archivos-borrado,48.5:papelera,51:papelera-devuelto,53.5:descargas,56:descargas-copiada,59.5:descargas-borrada,65.5:papelera-llena,68:papelera-llave-tapada,71:vaciar-confirma,72.5:vaciar-no,76:papelera-vacia" \
    80 TERM ROCOLA_OS=archivos,navegador ROCOLA_ARCHIVOS_USB="$u"
  b="$d/rocola_os_bitacora.txt"
  si "sale con 0" test "$(cat $o/salio)" = 0
  CAPS="archivos-datos archivos-usb archivos-copiada archivos-grande llave-tapada mostrar-confirma llave-mostrada nombre-nuevo genius-guardada borrar-confirma archivos-borrado papelera papelera-devuelto descargas descargas-copiada descargas-borrada papelera-llena papelera-llave-tapada vaciar-confirma vaciar-no papelera-vacia"
  n=0; for x in $CAPS; do [ "$(medida $o/$x.png)" = 1024x768 ] && n=$((n + 1)); done
  si "las 21 capturas (1024x768): $n" test $n = 21
  si "copiada de la USB: rocola_spotify_api.txt igual y con permisos 0600 ($(modo $d/rocola_spotify_api.txt))" bash -c "cmp -s '$u/rocola_spotify_api.txt' '$d/rocola_spotify_api.txt' && [ \"\$(stat -c %a '$d/rocola_spotify_api.txt')\" = 600 ]"
  si "rocola_spotify.txt de la USB quedo como rocola_spotify_llave.txt (0600) y no piso la bitacora de Spotify" bash -c "cmp -s '$u/rocola_spotify.txt' '$d/rocola_spotify_llave.txt' && [ \"\$(stat -c %a '$d/rocola_spotify_llave.txt')\" = 600 ] && ! cmp -s '$u/rocola_spotify.txt' '$d/rocola_spotify.txt' 2>/dev/null"
  si "el grande (70 KB) no se abre y lo dice; nada se escribio en la USB" bash -c "[ \$(ls -A '$u' | wc -l) = 7 ] && cmp -s <(printf '%s' \"\$API_PRUEBA\") '$u/rocola_spotify_api.txt'"
  si "archivo nuevo escrito con el teclado (nombre y contenido) y Ctrl+S: rocola_genius.txt = «$GENIUS_PRUEBA» y 0600" bash -c "[ \"\$(cat '$d/rocola_genius.txt')\" = '$GENIUS_PRUEBA' ] && [ \"\$(stat -c %a '$d/rocola_genius.txt')\" = 600 ] && [ \"\$(tail -c 1 '$d/rocola_genius.txt' | od -An -c | tr -d ' ')\" = '\\n' ]"
  si "renombrado notas.txt -> notas2.txt, borrado con su confirmacion A LA PAPELERA y devuelto (igual, en su sitio)" bash -c "[ ! -e '$d/notas.txt' ] && cmp -s '$d/notas2.txt' <(printf 'hola\r\nmundo\r\n') && grep -qF 'renombrado: notas.txt -> notas2.txt' '$b' && grep -qF 'a la papelera: notas2.txt' '$b' && grep -qF 'devuelto de la papelera: notas2.txt' '$b'"
  si "descargas: bajado.txt copiado a los datos; bajado.txt y la llave, a la papelera (las descargas quedan vacias)" bash -c "[ \"\$(cat '$d/bajado.txt')\" = 'de internet' ] && [ -z \"\$(ls -A '$d/descargas')\" ] && grep -qF 'copiado de las descargas a los datos: bajado.txt' '$b' && grep -qF 'a la papelera: bajado.txt' '$b' && grep -qF 'a la papelera: token_bajado.txt' '$b'"
  si "vaciar: con «No» no se vacia; con «Sí» si (una sola vez: 2 cosas) y la papelera queda vacia" bash -c "[ \$(grep -c 'papelera vaciada' '$b') = 1 ] && grep -qF 'papelera vaciada: 2' '$b' && [ -z \"\$(ls -A '$d/papelera')\" ]"
  si "la llave de la papelera se vio tapada: nunca «a la vista» (ni en la bitacora)" bash -c "! grep -qF 'llave a la vista: token_bajado.txt' '$b'"
  si "lo de fuera, intacto (y el enlace de los datos que sale, sin tocar)" bash -c "[ \"\$(cat '$f/secreto.txt')\" = 'lo de fuera' ] && [ \$(ls -A '$f' | wc -l) = 1 ] && [ -L '$d/fuga' ]"
  si "la bitacora: copias, la llave a la vista, lo guardado (sin lo de dentro)" bash -c "grep -qF 'copiado de la USB a los datos: rocola_spotify_api.txt (llave, 0600)' '$b' && grep -qF 'copiado de la USB a los datos: rocola_spotify_llave.txt (llave, 0600)' '$b' && grep -qF 'llave a la vista: rocola_spotify_api.txt' '$b' && grep -qF 'guardado: rocola_genius.txt (llave, 0600)' '$b'"
  si "NINGUNA llave en las bitacoras, rocola_error.txt ni la salida (ni la de la API, ni la de Soloist, ni la de Genius)" bash -c "! grep -rqE 'SECRETODEPRUEBA|IDDEPRUEBA|LLAVEDESOLOIST|GENIUS-prueba|TOKENBAJADO' '$d'/rocola_os_bitacora.txt '$d'/rocola_error.txt '$o/salida.txt'"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  for x in $CAPS; do guarda_captura "$o/$x.png" "$x.png"; done
fi

# ------------------------------------------------------------------ navegador: el traspaso de la pantalla con un cog de mentira
# El guion de verdad (rocola-navegador, con el sh de esta maquina: el de BusyBox se prueba en «guion») con un cog de mentira que apunta
# como lo abrieron. La musica: una moneda y dos Intro piden la primera cancion de la pared antes de abrir el navegador.
cog_falso() {
  cat > "$1" <<EOF
#!/bin/sh
echo "\$(date +%s.%N) cog \$*" >> "$2"
echo "\$(date +%s.%N) descargas: \$(cat "\$XDG_CONFIG_HOME/user-dirs.dirs")" >> "$2"
trap 'echo "\$(date +%s.%N) TERM" >> "$2"; exit 0' TERM
while :; do sleep 0.2; done
EOF
  chmod 755 "$1"
}
if [[ " $partes " == *" navegador "* ]]; then
  echo "== navegador (Ctrl+F4 -> Navegador -> Abrir; las teclas no llegan a la rocola ni las monedas cuentan; Ctrl+Alt+Inicio; Ctrl+Alt+F1)"
  d=$(datos navegador); o="$W/navegador"; mkdir -p "$o"
  cog_falso "$o/cog" "$o/cog.txt"
  # LA MUSICA DE VERDAD: la primera cancion, de 60 s (un tono), y la tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c: suena a su
  # ritmo y guarda lo sonado en sonado.raw): se mide cuanto sono mientras el navegador estaba delante
  ffmpeg -loglevel error -y -f lavfi -i "sine=frequency=440:duration=60" -ac 2 -ar 44100 -b:a 128k "$d/musica/Los Ejemplos/Disco Uno/01 Cancion 1.mp3" || echo "(sin ffmpeg: la cancion de 3 s)"
  AL="$W/alsa"; mkdir -p "$AL"
  gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound 2>/dev/null || echo "(sin la tarjeta con reloj)"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" > "$o/alsa.conf"
  ( t0=$(date +%s.%N); for i in $(seq 1 120); do echo "$(python3 -c "print('%.2f' % ($(date +%s.%N) - $t0))") $(wc -c < "$o/sonado.raw" 2>/dev/null || echo 0)" >> "$o/sonado-medida.txt"; sleep 0.25; done ) &
  MID=$!
  # 3: moneda, 3.5 y 4.5: Intro (el disco y su cancion: suena). 6: Ctrl+F4; 8: Navegador; 10.5: Abrir.
  # 13-13.6: «5» tres veces (monedas) y «a», «b». 15: Ctrl+Alt+Inicio. 19: Ctrl+Alt+F1. 23: «5» (con la ventana: cuenta).
  P="3:53,3.5:13,4.5:13,6:17+115,8:clic:818:54,10.5:clic:276:333,13:53,13.2:53,13.4:53,13.6:65,13.8:66"
  P="$P,15:17~1200,15.1:18~1000,15.3:36,19:17~1200,19.1:18~1000,19.3:112,23:53"
  abre navegador "$d" "$P" "9.5:navegador-hoja,12:navegador-abierto,21.5:navegador-vuelta" 27 TERM \
    ROCOLA_OS=archivos,navegador ROCOLA_NAVEGADOR_ORDEN="$NAV" ROCOLA_COG="$o/cog" ROCOLA_NAVEGADOR_TTY=/dev/null ROCOLA_NAVEGADOR_INICIO_HTML="$INICIO" \
    ROCOLA_NAVEGADOR_TRAZA="$o/nav.txt" ROCOLA_ESTADO_EN="12.5:$o/e-antes.txt,14.5:$o/e-durante.txt,22:$o/e-vuelta.txt,25:$o/e-moneda.txt" \
    ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf
  kill $MID 2>/dev/null
  t="$o/nav.txt"; sed 's/^/   nav: /' "$t"; sed 's/^/   cog: /' "$o/cog.txt" 2>/dev/null
  cr() { sed -n 's/^creditos=//p' "$1" 2>/dev/null | tr -d '\r'; }
  # lo que sono entre cede y retoma (bytes de 16 bits estereo a 44,1 kHz: 176 400 por segundo)
  son=$(python3 -c "
import re
L=[l.split(' ',1) for l in open('$t')]
a=[float(x[0])/1000 for x in L if x[1].startswith('cede')]; b=[float(x[0])/1000 for x in L if x[1].startswith('retoma')]
M=[tuple(map(float,l.split())) for l in open('$o/sonado-medida.txt') if len(l.split())==2]
def en(t): return min(M, key=lambda m: abs(m[0]-t))
if not a or not b or not M: print('0 0'); raise SystemExit
x, y = en(a[0]+0.5), en(b[0]-0.5)
print('%.1f %.1f' % (y[0]-x[0], (y[1]-x[1])/176400.0))" 2>/dev/null)
  echo "   sonado con el navegador delante: ${son#* } s de audio en ${son% *} s de reloj"
  si "sale con 0" test "$(cat $o/salio)" = 0
  si "abrir: cede la pantalla (sin componer) y abre el guion; cog con la pagina de inicio y --platform=drm" bash -c "grep -q ' cede (composiciones ' '$t' && grep -q ' abre (inicio) pid ' '$t' && grep -qF 'cog --platform=drm --cookie-jar=sqlite:$d/navegador/galletas.sqlite file://$INICIO' '$o/cog.txt'"
  c1=$(sed -n 's/.* cede (composiciones \([0-9]*\)).*/\1/p' "$t" | head -1); c2=$(sed -n 's/.* retoma (composiciones \([0-9]*\)).*/\1/p' "$t" | tail -1)
  si "con el navegador delante la rocola no compone ni un cuadro (composiciones al ceder $c1, al retomar $c2)" test -n "$c1" -a "$c1" = "$c2"
  si "la musica sigue sonando con el navegador delante: ${son#* } s de audio en ${son% *} s (la tarjeta con reloj)" python3 -c "import sys; a,b=map(float,'${son:-0 0}'.split()); sys.exit(0 if a > 5 and abs(b - a) < 1.0 else 1)"
  si "lo que sonaba al abrir sigue sonando al volver, mas adelante" python3 -c "
import re,sys; t=open('$t').read()
a=re.search(r'antes: suena «(.*?)» a ([0-9.]+) s',t); b=re.search(r'despues: suena «(.*?)» a ([0-9.]+) s',t)
print('   ', a and a.groups(), b and b.groups())
sys.exit(0 if a and b and a.group(1)==b.group(1) and float(b.group(2)) > float(a.group(2)) + 5 else 1)"
  si "con el navegador abierto las monedas («5» x3) y las letras no llegan a la rocola (creditos $(cr $o/e-antes.txt) -> $(cr $o/e-durante.txt) -> $(cr $o/e-vuelta.txt))" bash -c "[ '$(cr $o/e-antes.txt)' = '$(cr $o/e-durante.txt)' ] && [ '$(cr $o/e-durante.txt)' = '$(cr $o/e-vuelta.txt)' ]"
  si "Ctrl+Alt+Inicio: cierra cog y lo abre otra vez en la pagina de inicio SIN devolver la pantalla" python3 -c "
import sys; L=[l.split(' ',1)[1].strip() for l in open('$t')]
i=[k for k,l in enumerate(L) if l.startswith('cierra (Ctrl+Alt+Inicio)')]; r=[k for k,l in enumerate(L) if l.startswith('retoma')]
ab=[k for k,l in enumerate(L) if l.startswith('abre (inicio)')]
sys.exit(0 if i and len(ab)==2 and ab[1]>i[0] and r and r[0]>ab[1] else 1)"
  si "el navegador baja a la carpeta de descargas de los datos ($d/descargas) con el tope de descargas_tope_mb (64)" bash -c "grep -qF 'descargas: XDG_DOWNLOAD_DIR=\"$d/descargas\"' '$o/cog.txt' && grep -q 'tope 64 MB' '$o/run/rocola-navegador.txt' && [ -d '$d/descargas' ]"
  si "Ctrl+Alt+F1: cierra cog (TERM) y retoma la pantalla" bash -c "grep -q ' cierra (Ctrl+Alt+F1)' '$t' && [ \$(grep -c ' TERM\$' '$o/cog.txt') = 2 ] && grep -q ' retoma (composiciones ' '$t'"
  s=$(python3 -c "
L=[l.split(' ',1) for l in open('$t')]
a=[float(x[0]) for x in L if x[1].startswith('cierra (Ctrl+Alt+F1)')]; b=[float(x[0]) for x in L if x[1].startswith('retoma')]
print('%.2f' % ((b[-1]-a[0])/1000) if a and b else 'nan')")
  si "de Ctrl+Alt+F1 a la pantalla de vuelta: $s s (menos de 2 s)" python3 -c "import sys; sys.exit(0 if '$s' != 'nan' and float('$s') < 2 else 1)"
  cv=$(cr $o/e-vuelta.txt); cm=$(cr $o/e-moneda.txt)
  si "de vuelta, una moneda con la ventana abierta si cuenta (creditos $cv -> $cm)" test "${cm:-0}" -gt "${cv:-0}"
  tc=$(sed -n 's/.*navegador-abierto.png (\([0-9.]*\) s.*/\1/p' "$o/salida.txt")
  si "la captura pedida a los 12 s (con el navegador abierto) no se hizo hasta volver ($tc s): la rocola no pinto nada entretanto" python3 -c "import sys; sys.exit(0 if float('${tc:-0}') > 19 else 1)"
  b="$d/rocola_os_bitacora.txt"
  si "la bitacora: cede, abre, cierra, retoma (sin direcciones)" bash -c "grep -qF '[navegador] cede' '$b' && grep -qF '[navegador] retoma' '$b' && ! grep -q 'file://\\|https://' '$b'"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  for x in navegador-hoja navegador-vuelta; do guarda_captura "$o/$x.png" "$x.png"; done
fi

# ------------------------------------------------------------------ caidas: la rocola se cierra (SIGUSR1) o se cae (SIGKILL) con el navegador abierto
if [[ " $partes " == *" caidas "* ]]; then
  echo "== caidas (la rocola se cierra o se cae con el navegador abierto: cog no se queda con la pantalla)"
  d=$(datos usr1); o="$W/usr1"; mkdir -p "$o"; cog_falso "$o/cog" "$o/cog.txt"
  P="6:17+115,8:clic:818:54,10:clic:276:333"
  abre usr1 "$d" "$P" "" 13 USR1 ROCOLA_OS=archivos,navegador ROCOLA_NAVEGADOR_ORDEN="$NAV" ROCOLA_COG="$o/cog" ROCOLA_NAVEGADOR_TTY=/dev/null ROCOLA_NAVEGADOR_INICIO_HTML="$INICIO" ROCOLA_NAVEGADOR_TRAZA="$o/nav.txt"
  sed 's/^/   nav: /' "$o/nav.txt"
  si "SIGUSR1 (la consola de mantenimiento) con el navegador abierto: cog recibe TERM y la rocola sale con 0" bash -c "grep -q ' TERM\$' '$o/cog.txt' && [ \"\$(cat $o/salio)\" = 0 ] && grep -q 'cierra (la rocola se cierra)' '$o/nav.txt'"
  si "y no queda ningun cog de mentira vivo" bash -c "! pgrep -f '^/bin/sh $o/cog --platform' > /dev/null"
  d=$(datos kill9); o="$W/kill9"; mkdir -p "$o"; cog_falso "$o/cog" "$o/cog.txt"
  abre kill9 "$d" "$P" "" 13 KILL ROCOLA_OS=archivos,navegador ROCOLA_NAVEGADOR_ORDEN="$NAV" ROCOLA_COG="$o/cog" ROCOLA_NAVEGADOR_TTY=/dev/null ROCOLA_NAVEGADOR_INICIO_HTML="$INICIO" ROCOLA_NAVEGADOR_TRAZA="$o/nav.txt"
  sleep 3
  si "SIGKILL a la rocola con el navegador abierto: el guion lo ve y cierra cog solo" bash -c "grep -q ' TERM\$' '$o/cog.txt' && grep -q 'rocola-xp ya no esta' '$o/run/rocola-navegador.txt' && ! pgrep -f '^/bin/sh $o/cog --platform' > /dev/null"
  # sin cog (el sistema de hoy): el boton en gris diciendo por que
  d=$(datos sincog); o="$W/sincog"; mkdir -p "$o"
  abre sincog "$d" "6:17+115,8:clic:818:54,10:clic:276:333" "9.5:navegador-sin-cog" 13 TERM ROCOLA_OS=archivos,navegador ROCOLA_NAVEGADOR_ORDEN="$NAV" ROCOLA_COG="$o/no-esta" ROCOLA_NAVEGADOR_TRAZA="$o/nav.txt"
  si "sin cog: no se abre nada (el boton en gris: «llega con la compilacion completa»)" bash -c "[ ! -s '$o/nav.txt' ] || ! grep -q ' cede' '$o/nav.txt'"
  guarda_captura "$o/navegador-sin-cog.png" "navegador-sin-cog.png"
fi

# ------------------------------------------------------------------ sin ROCOLA_OS: nada
if [[ " $partes " == *" sin "* ]]; then
  echo "== sin (sin ROCOLA_OS: Ctrl+F4 no hace nada; ni las USB ni el navegador)"
  d=$(datos sin); u=$(usb sin); o="$W/sin"; mkdir -p "$o"; cog_falso "$o/cog" "$o/cog.txt"
  abre sin "$d" "6:17+115,8:clic:818:54,10:clic:276:333" "9:sin-ctrlf4" 13 TERM ROCOLA_NAVEGADOR_ORDEN="$NAV" ROCOLA_COG="$o/cog" ROCOLA_ARCHIVOS_USB="$u" ROCOLA_NAVEGADOR_TRAZA="$o/nav.txt"
  si "sale con 0" test "$(cat $o/salio)" = 0
  si "ni bitacora de Rocola OS, ni traza del navegador, ni cog" bash -c "[ ! -e '$d/rocola_os_bitacora.txt' ] && [ ! -e '$o/nav.txt' ] && [ ! -e '$o/cog.txt' ]"
fi

echo "== $bien bien, $mal mal"
[ "$mal" = 0 ]
