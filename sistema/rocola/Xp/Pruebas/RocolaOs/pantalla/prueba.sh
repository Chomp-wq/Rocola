#!/bin/bash
# LA PRUEBA DE LA HOJA PANTALLA DE CTRL+F4 (fase 11, grupo pantalla-ctrlf4: RocolaOs/RocolaOs.Pantalla*.cs, RocolaOs.Ventana*.cs, los ganchos
# g85-g87, Consola/Pantalla/Kms.cs y rootfs_overlay/usr/bin/rocola-pantalla; FASE11_pantalla.md). Con datos INVENTADOS (los de partida de
# las escenas de ventanas, DATOS0), fuera de pantalla con un KMS SIMULADO (RocolaOs/RocolaOs.PantallaSimulada.cs: los modos de un monitor
# inventado, la tabla de gamma de la radeon -256, por la llamada de siempre- y sin matriz de color) y sin sonido. NADA de esto es el A4 ni
# un monitor de verdad.
#   bash prueba.sh PROGRAMA [numeros guion ventana teclado arranque caidas malo monitor navegador lista apagada sin]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT, con el ejecutable rocola-xp)
#   W: la carpeta de trabajo (/root/rocola-trabajo/fase11/pantalla/prueba); se rehace en cada corrida
#   CAPTURAS: donde se copian las capturas que se ensenan (de fabrica, ninguna)
#   DATOS0, REF (la referencia con trazas, para «sin»), RUIDO (el ruido medido de la escena «sigterm»), TARGET (el sistema compilado, para
#   el BusyBox de «guion»)
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd); SIS=$(cd "$XP/../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-numeros guion ventana teclado arranque caidas malo monitor navegador lista apagada sin}"
W=${W:-/root/rocola-trabajo/fase11/pantalla/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
REF=${REF:-/root/rocola-trabajo/port/ventanas/ref-trazas/sistema/rocola/Xp/bin/Release/net10.0}
RUIDO=${RUIDO:-/root/rocola-trabajo/port/ventanas/ruido/sigterm.json}
TARGET=${TARGET:-/root/rocola-trabajo/salida-qemu/target}
CAPTURAS=${CAPTURAS:-}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
hay() { [ -e "$1" ]; }
nohay() { [ ! -e "$1" ]; }
medida() { python3 -c "import struct,sys; d=open(sys.argv[1],'rb').read(24); print('%dx%d' % struct.unpack('>II', d[16:24]))" "$1" 2>/dev/null; }
export -f medida
guarda_captura() { [ -n "$CAPTURAS" ] && [ -f "$1" ] && { mkdir -p "$CAPTURAS"; cp "$1" "$CAPTURAS/$2"; }; return 0; }
SIM="1920x1080@60*,1920x1080@50,1280x1024@75,1280x1024@60,1024x768@75,1024x768@60,800x600@60;gamma=256;ctm=0;falla=1280x1024@60;controlador=radeon"
orden() { env ROCOLA_DATOS="$W/o" ROCOLA_PANTALLA=fuera:64x64 timeout 60 "${RX[@]}" --prueba-pantalla "$@" 2>/dev/null | grep -v '^Pantalla: '; }
mkdir -p "$W/o"
orden tabla 256 25 100 100 -25 | head -3 | tr '\n' ' ' | sed 's/ $//' > "$W/tabla-25.txt"   # (la de brillo 25 y temperatura -25: la comparan las escenas)

# ------------------------------------------------------------------ numeros: las cuentas de los colores y lo guardado
if [[ " $partes " == *" numeros "* ]]; then
  echo "== numeros"
  orden tabla 256 0 100 100 0 > "$W/t0.txt"
  si "de fabrica la tabla es la recta: r[i] = round(i*65535/255) = i*257 (256 entradas)" python3 -c "
import sys; L=[l.split() for l in open('$W/t0.txt')]
ok=all(l[0] in 'rgb' and [int(v) for v in l[1:]]==[i*257 for i in range(256)] for l in L[:3]); sys.exit(0 if ok else 1)"
  si "la tabla de fabrica del nucleo (lo que pone QuitaColores con la llamada de siempre) es i<<8" python3 -c "
import sys; L=[l.split() for l in open('$W/t0.txt')]; sys.exit(0 if L[3][0]=='fabrica' and [int(v) for v in L[3][1:]]==[i<<8 for i in range(256)] else 1)"
  orden tabla 256 25 100 100 -25 > "$W/t1.txt"
  si "brillo +25 y temperatura -25: r[0]=r(0+0,125)=8192, b menor que g menor que r en el medio, todo creciente y dentro de 0..65535" python3 -c "
import sys; L=[[int(v) for v in l.split()[1:]] for l in open('$W/t1.txt')][:3]
r,g,b=L; ok = r[0]==round(0.125*65535) and b[128]<g[128]<r[128] and all(x[i]<=x[i+1] for x in L for i in range(255)) and max(max(x) for x in L)<=65535 and min(min(x) for x in L)>=0
print('   r[0]=%d r[128]=%d g[128]=%d b[128]=%d r[255]=%d' % (r[0], r[128], g[128], b[128], r[255])); sys.exit(0 if ok else 1)"
  orden tabla 8 0 100 200 0 > "$W/t2.txt"
  si "gamma 2,00 aclara los medios: en 8 entradas r = round((i/7)^0.5*65535)" python3 -c "
import sys; r=[int(v) for v in open('$W/t2.txt').readline().split()[1:]]; e=[round((i/7)**0.5*65535) for i in range(8)]; print('  ', r); sys.exit(0 if r==e else 1)"
  orden tabla 8 0 150 100 0 > "$W/t3.txt"
  si "contraste 150 %: alrededor del gris medio, y lo que se sale al borde (0 y 65535)" python3 -c "
import sys; r=[int(v) for v in open('$W/t3.txt').readline().split()[1:]]; e=[max(0,min(65535,round(((i/7-0.5)*1.5+0.5)*65535))) for i in range(8)]; print('  ', r); sys.exit(0 if r==e else 1)"
  orden ctm 100 > "$W/c100.txt"; orden ctm 0 > "$W/c0.txt"; orden ctm 50 > "$W/c50.txt"
  si "saturacion 100 %: la CTM identidad (1,0 = 0x0000000100000000 en S31.32)" bash -c "sed -n 2p '$W/c100.txt' | grep -qx 's31.32 0000000100000000 0000000000000000 0000000000000000 0000000000000000 0000000100000000 0000000000000000 0000000000000000 0000000000000000 0000000100000000'"
  si "saturacion 0 %: las tres filas con los pesos de Rec. 709 (gris)" bash -c "sed -n 1p '$W/c0.txt' | grep -qx 'ctm 0.2126 0.7152 0.0722 0.2126 0.7152 0.0722 0.2126 0.7152 0.0722'"
  si "saturacion 50 %: cada fila suma 1 (el blanco sigue blanco)" python3 -c "
import sys; v=[float(x) for x in open('$W/c50.txt').readline().split()[1:]]; sys.exit(0 if all(abs(sum(v[3*f:3*f+3])-1)<1e-6 for f in range(3)) else 1)"
  printf '# un comentario\nmodo=1920x1080@60\nbrillo=99\ncontraste=120\nclave_rara=7\nsin_confirmar=1\nmodo_malo=1280x1024@75 (no)\n' > "$W/o/p.txt"
  orden lee "$W/o/p.txt" > "$W/lee.txt"
  si "lee: el modo, el brillo acotado a 50, el contraste, lo que no conoce y su comentario" bash -c "head -1 '$W/lee.txt' | grep -qF 'modo=1920x1080@60 brillo 50, contraste 120 %, gamma 1.00, temperatura 0, saturacion 100 % sin_confirmar=1 modo_malo=1280x1024@75 (no) otras=2'"
  si "lee y escribe: guarda lo que no conoce (clave_rara=7) y solo lo que no es de fabrica" bash -c "grep -qx 'clave_rara=7' '$W/lee.txt' && grep -qx 'brillo=50' '$W/lee.txt' && ! grep -q '^gamma=' '$W/lee.txt'"
  printf 'modo=1920x1080@\nbrillo=x\n' > "$W/o/p2.txt"; orden lee "$W/o/p2.txt" > "$W/lee2.txt"
  si "un modo que no vale es lo automatico; un numero que no vale, el de fabrica" bash -c "head -1 '$W/lee2.txt' | grep -qF 'modo=auto brillo 0,'"
  orden lee "$W/o/no-esta.txt" > "$W/lee3.txt"
  si "sin archivo: lo automatico y los colores de fabrica" bash -c "head -1 '$W/lee3.txt' | grep -qF 'modo=auto brillo 0, contraste 100 %, gamma 1.00, temperatura 0, saturacion 100 % sin_confirmar=0'"
fi

# ------------------------------------------------------------------ guion: rocola-pantalla con el BusyBox del sistema (chroot)
if [[ " $partes " == *" guion "* ]]; then
  echo "== guion (BusyBox de $TARGET)"
  if [ ! -x "$TARGET/bin/busybox" ] || [ "$(id -u)" != 0 ]; then echo "NO    sin $TARGET/bin/busybox o sin root: no se prueba el guion"; mal=$((mal + 1))
  else
    R="$W/raiz"; mkdir -p "$R/bin" "$R/lib" "$R/usr/bin" "$R/run" "$R/proc" "$R/rocola/datos/xp" "$R/drm/card0-HDMI-A-1" "$R/drm/card0-VGA-1" "$R/drm/card0"
    cp "$TARGET/bin/busybox" "$R/bin/"; for l in ld-linux-x86-64.so.2 libc.so.6 libm.so.6 libresolv.so.2; do cp -L "$TARGET/lib/$l" "$R/lib/" 2>/dev/null; done
    ln -s lib "$R/lib64"; mkdir -p "$R/dev"; mknod "$R/dev/null" c 1 3; chroot "$R" /bin/busybox --install -s /bin
    cp "$SIS/board/rocola/rootfs_overlay/usr/bin/rocola-pantalla" "$R/usr/bin/"
    echo connected > "$R/drm/card0-HDMI-A-1/status"; printf '1920x1080\n1920x1080\n1280x1024\n1024x768\n1024x768\n800x600\n' > "$R/drm/card0-HDMI-A-1/modes"
    echo disconnected > "$R/drm/card0-VGA-1/status"; : > "$R/drm/card0-VGA-1/modes"
    c() { chroot "$R" /bin/env ROCOLA_DRM=/drm ROCOLA_RUN=/run /bin/sh /usr/bin/rocola-pantalla "$@"; }
    si "sh -n con el BusyBox del sistema" chroot "$R" /bin/sh -n /usr/bin/rocola-pantalla
    si "no usa stat (BusyBox sin CONFIG_STAT)" bash -c "! grep -qw stat '$R/usr/bin/rocola-pantalla' || ! grep -E '(^|[;|&(]\s*)stat ' '$R/usr/bin/rocola-pantalla' | grep -q ."
    c modos > "$W/g-modos.txt" 2>&1
    si "modos: la salida conectada con sus modos sin repetir (4 de 6 renglones), y no la desconectada" bash -c "grep -qx 'card0-HDMI-A-1 (conectada):' '$W/g-modos.txt' && [ \$(grep -c '^  [0-9]' '$W/g-modos.txt') = 4 ] && ! grep -q VGA '$W/g-modos.txt'"
    echo disconnected > "$R/drm/card0-HDMI-A-1/status"; c modos > "$W/g-modos2.txt" 2>&1; echo connected > "$R/drm/card0-HDMI-A-1/status"
    si "modos sin monitor: lo dice y lista las salidas" bash -c "grep -q '^Ninguna salida' '$W/g-modos2.txt' && grep -qF 'card0-VGA-1: disconnected' '$W/g-modos2.txt'"
    c estado > "$W/g-e0.txt" 2>&1
    si "estado sin nada guardado: automatica" contiene "$W/g-e0.txt" "Pantalla: automatica, colores de fabrica"
    printf '# La pantalla de Rocola OS\nmodo=1920x1080@60\nbrillo=25\ntemperatura=-25\nsin_confirmar=1\nmodo_malo=1280x1024@75 (2 arranques seguidos sin llegar a 60 s en pie)\n' > "$R/rocola/datos/xp/rocola_pantalla.txt"
    cp "$R/rocola/datos/xp/rocola_pantalla.txt" "$R/rocola/datos/xp/rocola_pantalla.txt.bak"
    echo "pantalla=1920x1080@60 guardada colores" > "$R/run/rocola-vivo.txt"
    c estado > "$W/g-e1.txt" 2>&1; sed 's/^/   /' "$W/g-e1.txt"
    si "estado: el modo guardado, los colores, los arranques sin confirmar, el modo malo y lo de ahora" bash -c "grep -qx 'Modo: 1920x1080@60 (guardado en Ctrl+F4)' '$W/g-e1.txt' && grep -qF 'brillo 25, contraste 100 %' '$W/g-e1.txt' && grep -qF 'temperatura -25' '$W/g-e1.txt' && grep -qF 'sin llegar a 60 s en pie: 1' '$W/g-e1.txt' && grep -qF 'El ultimo modo que no sirvio: 1280x1024@75' '$W/g-e1.txt' && grep -qF 'Ahora (la rocola abierta): 1920x1080@60 guardada colores' '$W/g-e1.txt'"
    c auto > "$W/g-a.txt" 2>&1
    si "auto: borra el archivo, su .bak y su .tmp, y dice que vale al volver a la rocola" bash -c "[ ! -e '$R/rocola/datos/xp/rocola_pantalla.txt' ] && [ ! -e '$R/rocola/datos/xp/rocola_pantalla.txt.bak' ] && grep -qF 'Ctrl+Alt+F1' '$W/g-a.txt'"
    c auto > "$W/g-a2.txt" 2>&1
    si "auto otra vez: no hay nada que borrar" contiene "$W/g-a2.txt" "No hay nada guardado"
    printf 'modo=1920x1080@60\n' > "$R/rocola/datos/xp/rocola_pantalla.txt.bak"; c estado > "$W/g-e2.txt" 2>&1
    si "estado: sin el bueno, lee el .bak (como la rocola) y lo dice" bash -c "grep -qF 'Modo: 1920x1080@60' '$W/g-e2.txt' && grep -qF 'leido de la copia' '$W/g-e2.txt'"
    c > "$W/g-uso.txt" 2>&1; r0=$?; c otra > /dev/null 2>&1; r1=$?
    si "sin orden: el uso y sale con 0; con una que no es: sale con 2" test $r0 = 0 -a $r1 = 2
  fi
fi

# ------------------------------------------------------------------ las escenas (la rocola entera con el KMS simulado)
# abre NOMBRE DATOS "PULSA" "CAPTURAS" SEGUNDOS SENAL [VAR=valor...]: la rocola con esos datos, sin ROCOLA_PANTALLA (la pantalla la abre g86)
abre() {
  local n="$1" d="$2" pulsa="$3" caps="$4" seg="$5" sen="$6"; shift 6
  local o="$W/$n"; rm -rf "$o"; mkdir -p "$o/run"
  local c=""; IFS=',' read -ra L <<< "$caps"
  for x in "${L[@]}"; do [ -z "$x" ] && continue; c="$c,${x%%:*}:$o/${x#*:}.png"; done; c="${c#,}"
  ( cd "$d" && exec env -u ROCOLA_PANTALLA ROCOLA_CATALOGO_URL=http://127.0.0.1:9/catalogo ROCOLA_PORTADAS_CATALOGO=http://127.0.0.1:9 ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=null ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA="$c" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_KMS_SIMULADO_TRAZA="$o/kms.txt" "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$!
  echo $p > "$o/pid"
  sleep "$seg"; [ "$sen" != NADA ] && kill -"$sen" $p 2>/dev/null
  ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
}
datos() { local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/; printf 'spotify=0\n' > "$d/rocola_os.txt"; echo "$d"; }
# segundos entre dos renglones del kms.txt (el primero que contiene A y el siguiente que contiene B)
entre_kms() { awk -v a="$2" -v b="$3" 'index($0,a) && !t {t=$1; next} t && index($0,b) {printf "%.1f", ($1-t)/1000; exit}' "$1"; }
pt() { grep -F "$2" "$1" | tail -1 | cut -d' ' -f2-; }   # (lo que dice un renglon del kms.txt, sin su hora)

if [[ " $partes " == *" ventana "* ]]; then
  echo "== ventana (Ctrl+F4, probar un modo sin contestar, probar y confirmar, los colores, Escape)"
  d=$(datos ventana); o="$W/ventana"
  # 1024x768@75 es lo automatico (el preferido es 1920x1080, pero sin la resolucion nativa, V30, la de XP: 1024x768 con su mas alto refresco).
  # La hoja de 966x764 en (29,2): su cliente empieza en (32,31). En 1920x1080 en (477,158): cliente en (480,187).
  P="6:17+115,9:clic:480:293,10:clic:276:434,13:67,28:clic:480:293,29:clic:276:434,31.5:clic:1078:609,33:clic:1297:377,34:clic:1094:611,36:27"
  ( sleep 14.5; cp "$d/rocola_estado.txt" "$W/ventana-estado-14.txt" 2>/dev/null; cp "$d/rocola_pantalla.txt" "$W/ventana-pantalla-14.txt" 2>/dev/null ) &
  abre ventana "$d" "$P" "8:ventana-1024,12:prueba-15s,27:vuelta-sola,32.5:guardado-1920,35:colores,38:cerrada" 40 TERM ROCOLA_OS=1 ROCOLA_KMS_SIMULADO="$SIM"
  wait
  k="$o/kms.txt"; sed 's/^/   kms: /' "$k" | cut -c1-110
  si "sale con 0" test "$(cat $o/salio)" = 0
  si "arranca en lo automatico, 1024x768@75" bash -c "head -1 '$k' | grep -q ' modo 1024x768@75$'"
  si "las capturas: la hoja en 1024x768, la prueba en 1920x1080, la vuelta en 1024x768, lo guardado en 1920x1080" \
    test "$(medida $o/ventana-1024.png) $(medida $o/prueba-15s.png) $(medida $o/vuelta-sola.png) $(medida $o/guardado-1920.png) $(medida $o/cerrada.png)" = "1024x768 1920x1080 1024x768 1920x1080 1920x1080"
  s=$(entre_kms "$k" "modo 1920x1080@60" "modo 1024x768@75")
  si "sin contestar vuelve sola a la de antes a los 15 s ($s s entre el cambio y la vuelta)" python3 -c "import sys; sys.exit(0 if 14.5 <= float('${s:-0}') <= 17.0 else 1)"
  si "mientras se prueba no se guarda nada (a los 14,5 s no hay rocola_pantalla.txt)" nohay "$W/ventana-pantalla-14.txt"
  si "una moneda con «¿Se ve bien?» abierto cuenta (a los 14,5 s, creditos=$(sed -n 's/^creditos=//p' $W/ventana-estado-14.txt 2>/dev/null | tr -d '\r'); el estado viejo de los datos se descarta al arrancar)" bash -c "n=\$(sed -n 's/^creditos=//p' '$W/ventana-estado-14.txt' | tr -d '\r'); [ \"\${n:-0}\" -gt 0 ]"
  si "probada otra vez y confirmada con «Si»: guardada (modo=1920x1080@60, sin sin_confirmar)" bash -c "grep -qx 'modo=1920x1080@60' '$d/rocola_pantalla.txt' && ! grep -q sin_confirmar '$d/rocola_pantalla.txt'"
  si "los colores guardados (brillo=25, temperatura=-25)" bash -c "grep -qx 'brillo=25' '$d/rocola_pantalla.txt' && grep -qx 'temperatura=-25' '$d/rocola_pantalla.txt'"
  si "la ultima tabla puesta en la tarjeta es la de brillo 25 y temperatura -25 (las 768 cifras)" bash -c "[ \"\$(grep ' gamma 256 ' '$k' | tail -1 | cut -d' ' -f5-)\" = \"\$(cat '$W/tabla-25.txt')\" ]"
  si "sin CTM no se pide la matriz de color" nocontiene "$k" " ctm "
  si "al cerrarse la rocola, los colores de fabrica (para la consola)" bash -c "tail -1 '$k' | grep -q ' fabrica$'"
  b="$d/rocola_os_bitacora.txt"
  si "la bitacora: abierta, la prueba sin contestar, la vuelta, lo guardado y los colores" bash -c "grep -qF '[ventana] Ctrl+F4 abierta' '$b' && grep -qF 'nadie contesto en 15 s' '$b' && grep -qF 'vuelve a 1024x768@75' '$b' && grep -qF 'guardado: 1920x1080@60' '$b' && grep -qF 'colores guardados: brillo 25' '$b'"
  si "la rocola sigue: el apagado de siempre (WindowsShutDown, rocola_estado.txt borrado)" bash -c "grep -q '	closed Form1.Rocola. WindowsShutDown$' '$o/traza.txt' && [ ! -e '$d/rocola_estado.txt' ]"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  for f in ventana-1024 prueba-15s vuelta-sola guardado-1920 colores cerrada; do guarda_captura "$o/$f.png" "$f.png"; done
fi

if [[ " $partes " == *" teclado "* ]]; then
  echo "== teclado (lo mismo sin raton: Tab, las flechas, Intro; asi se maneja tambien en la prueba de QEMU)"
  d=$(datos teclado); o="$W/teclado"
  # Ctrl+F4 (el foco en la pestana); Tab, Tab: la barra de la resolucion; Fin: la mas grande (1920x1080, sus hercios mas altos: 60);
  # Tab (los hercios), Tab («Probar»), Intro: cambia y pregunta, con el foco en «No»; Tab («Si»), Intro: guardado.
  # El foco vuelve a la barra de la resolucion; Tab (los hercios), Tab («Volver a lo automatico»: «Probar» esta en gris), Tab (el brillo),
  # tres veces derecha: brillo 3; y Escape.
  # (navegador-archivos: con las pestanas Navegador y Archivos, dos Tab mas desde la primera pestana hasta la barra)
  P="6:17+115,7:9,7.15:9,7.3:9,7.45:9,7.6:35,8:9,8.3:9,8.6:13,11:9,11.4:13,13:9,13.2:9,13.4:9,13.6:39,13.8:39,14:39,16:27"
  abre teclado "$d" "$P" "10:teclado-pregunta,15:teclado-guardado" 19 TERM ROCOLA_OS=1 ROCOLA_KMS_SIMULADO="$SIM"
  k="$o/kms.txt"; sed 's/^/   kms: /' "$k" | cut -c1-110
  si "sale con 0" test "$(cat $o/salio)" = 0
  si "con Tab, Fin, Tab, Tab e Intro: la prueba de 1920x1080@60 (la captura de la pregunta mide 1920x1080)" bash -c "grep -q ' modo 1920x1080@60$' '$k' && [ \"\$(medida $o/teclado-pregunta.png)\" = 1920x1080 ]"
  si "con Tab e Intro en la pregunta («Si»): guardado modo=1920x1080@60 y no vuelve a 1024x768" bash -c "grep -qx 'modo=1920x1080@60' '$d/rocola_pantalla.txt' && [ \$(grep -c ' modo ' '$k') = 2 ]"
  si "con Tab, Tab, Tab y tres veces la flecha derecha: brillo=3 guardado, y su tabla en la tarjeta" bash -c "grep -qx 'brillo=3' '$d/rocola_pantalla.txt' && [ \"\$(grep ' gamma 256 ' '$k' | tail -1 | cut -d' ' -f5-)\" = \"\$(env ROCOLA_DATOS='$W/o' ROCOLA_PANTALLA=fuera:64x64 timeout 60 ${RX[*]} --prueba-pantalla tabla 256 3 100 100 0 2>/dev/null | grep -v '^Pantalla: ' | head -3 | tr '\n' ' ' | sed 's/ \$//')\" ]"
  b="$d/rocola_os_bitacora.txt"
  si "la bitacora dice los modos del monitor al arrancar (7, el automatico con *)" bash -c "grep -qF 'modos del monitor (7): 1920x1080@60 1920x1080@50 1280x1024@75 1280x1024@60 1024x768@75* 1024x768@60 800x600@60' '$b'"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  guarda_captura "$o/teclado-pregunta.png" "teclado-pregunta.png"
fi

if [[ " $partes " == *" arranque "* ]]; then
  echo "== arranque (lo guardado se pone antes de pintar; 60 s en pie lo confirman)"
  d=$(datos arranque); o="$W/arranque"
  printf '# un comentario del dueno\nmodo=1920x1080@60\nbrillo=25\ntemperatura=-25\nclave_rara=1\n' > "$d/rocola_pantalla.txt"
  ( sleep 10; cp "$d/rocola_pantalla.txt" "$W/arranque-10.txt" 2>/dev/null ) &
  abre arranque "$d" "" "5:arranque-1920" 68 TERM ROCOLA_OS=1 ROCOLA_KMS_SIMULADO="$SIM"
  wait
  k="$o/kms.txt"; sed 's/^/   kms: /' "$k" | cut -c1-110
  si "sale con 0" test "$(cat $o/salio)" = 0
  si "el primer modo puesto es el guardado (antes de pintar nada: ni un cuadro en 1024x768)" bash -c "head -1 '$k' | grep -q ' modo 1920x1080@60$' && [ \$(grep -c ' modo ' '$k') = 1 ]"
  si "la tabla guardada se pone justo despues de abrir la pantalla (las 768 cifras de brillo 25 y temperatura -25)" bash -c "[ \"\$(sed -n 2p '$k' | cut -d' ' -f5-)\" = \"\$(cat '$W/tabla-25.txt')\" ]"
  si "la captura de los 5 s mide 1920x1080" test "$(medida $o/arranque-1920.png)" = 1920x1080
  si "el arranque se cuenta antes de abrir (a los 10 s, sin_confirmar=1)" contiene "$W/arranque-10.txt" "sin_confirmar=1"
  si "a los 60 s en pie se confirma (sin sin_confirmar al final) y la bitacora lo dice" bash -c "! grep -q sin_confirmar '$d/rocola_pantalla.txt' && grep -qF 'lleva 60 s en pie: confirmado' '$d/rocola_os_bitacora.txt'"
  si "lo que no conoce sigue en su sitio (el comentario y clave_rara=1)" bash -c "grep -qx '# un comentario del dueno' '$d/rocola_pantalla.txt' && grep -qx 'clave_rara=1' '$d/rocola_pantalla.txt'"
  guarda_captura "$o/arranque-1920.png" "arranque-1920.png"
fi

if [[ " $partes " == *" caidas "* ]]; then
  echo "== caidas (2 arranques seguidos que se caen antes de 60 s con el modo guardado: el tercero es lo automatico)"
  d=$(datos caidas)
  printf 'modo=1920x1080@60\n' > "$d/rocola_pantalla.txt"
  abre caida1 "$d" "" "" 8 KILL ROCOLA_OS=1 ROCOLA_KMS_SIMULADO="$SIM"; cp "$d/rocola_pantalla.txt" "$W/caida1.txt"
  abre caida2 "$d" "" "" 8 KILL ROCOLA_OS=1 ROCOLA_KMS_SIMULADO="$SIM"; cp "$d/rocola_pantalla.txt" "$W/caida2.txt"
  abre caida3 "$d" "" "6:caida3" 9 TERM ROCOLA_OS=1 ROCOLA_KMS_SIMULADO="$SIM"
  si "primera caida: sin_confirmar=1 y en 1920x1080" bash -c "grep -qx 'sin_confirmar=1' '$W/caida1.txt' && head -1 '$W/caida1/kms.txt' | grep -q '1920x1080@60'"
  si "segunda caida: sin_confirmar=2 y en 1920x1080" bash -c "grep -qx 'sin_confirmar=2' '$W/caida2.txt' && head -1 '$W/caida2/kms.txt' | grep -q '1920x1080@60'"
  si "tercera: lo automatico desde el primer cuadro (1024x768@75)" bash -c "head -1 '$W/caida3/kms.txt' | grep -q ' modo 1024x768@75$' && [ \"\$(medida '$W/caida3/caida3.png')\" = 1024x768 ]"
  si "y lo guardado: modo=auto, modo_malo dice por que, sin sin_confirmar" bash -c "grep -qx 'modo=auto' '$d/rocola_pantalla.txt' && grep -qF 'modo_malo=1920x1080@60 (2 arranques seguidos sin llegar a 60 s en pie)' '$d/rocola_pantalla.txt' && ! grep -q sin_confirmar '$d/rocola_pantalla.txt'"
  si "la bitacora lo dice" contiene "$d/rocola_os_bitacora.txt" "en 2 arranques seguidos: vuelve a lo automatico"
fi

if [[ " $partes " == *" malo "* ]]; then
  echo "== malo (el controlador no acepta el modo guardado, ni uno que se prueba)"
  d=$(datos malo); o="$W/malo"
  printf 'modo=1920x1080@60\n' > "$d/rocola_pantalla.txt"
  # 1280x1024 es la del medio de las tres de la barra; 60 Hz la de la izquierda de las dos de esa resolucion
  abre malo "$d" "6:17+115,8:clic:276:293,9:clic:73:383,10:clic:276:434" "12:no-se-pudo" 14 TERM ROCOLA_OS=1 ROCOLA_KMS_SIMULADO="${SIM/falla=1280x1024@60/falla=1280x1024@60/1920x1080@60}"
  k="$o/kms.txt"; sed 's/^/   kms: /' "$k" | cut -c1-110
  si "al abrir: el guardado no se acepta y queda lo automatico" bash -c "sed -n 1p '$k' | grep -q ' falla 1920x1080@60$' && sed -n 2p '$k' | grep -q ' modo 1024x768@75$'"
  si "lo guardado: modo=auto y modo_malo con el porque" bash -c "grep -qx 'modo=auto' '$d/rocola_pantalla.txt' && grep -qF 'modo_malo=1920x1080@60: el controlador no lo acepta' '$d/rocola_pantalla.txt'"
  si "probar 1280x1024@60 (no lo acepta): no cambia nada ni pregunta" bash -c "grep -q ' falla 1280x1024@60$' '$k' && [ \$(grep -c ' modo ' '$k') = 1 ] && ! grep -qF 'Se ve bien' '$d/rocola_os_bitacora.txt'"
  si "la bitacora dice que no se pudo" contiene "$d/rocola_os_bitacora.txt" "no se pudo poner el modo 1280x1024@60"
  guarda_captura "$o/no-se-pudo.png" "no-se-pudo.png"
fi

if [[ " $partes " == *" monitor "* ]]; then
  echo "== monitor (el modo guardado no lo da este monitor)"
  d=$(datos monitor)
  printf 'modo=2560x1440@60\n' > "$d/rocola_pantalla.txt"
  abre monitor "$d" "" "" 8 TERM ROCOLA_OS=1 ROCOLA_KMS_SIMULADO="$SIM"
  si "lo automatico, sin contar el arranque, y lo guardado se queda (por si vuelve el monitor)" bash -c "head -1 '$W/monitor/kms.txt' | grep -q ' modo 1024x768@75$' && [ \"\$(cat '$d/rocola_pantalla.txt')\" = 'modo=2560x1440@60' ]"
  si "la bitacora lo dice" contiene "$d/rocola_os_bitacora.txt" "el modo guardado 2560x1440@60 no lo da este monitor"
fi

if [[ " $partes " == *" navegador "* ]]; then
  # (revision de la junta 2) AL VOLVER DEL NAVEGADOR el mando de la tarjeta puede no volver a la primera (Retoma: drmSetMaster sin mirar lo
  # que devuelve): el KMS simulado con «retoma=N» hace fallar el modo N veces despues de Recupera. Con 2, el modo guardado se vuelve a poner
  # y NO se borra; con 14 (mas que los 10 reintentos de los 15 s de gracia), lo de siempre: lo automatico y modo_malo
  NAV="$SIS/board/rocola/rootfs_overlay/usr/bin/rocola-navegador"; INICIO="$SIS/board/rocola/rootfs_overlay/usr/share/rocola/navegador/inicio.html"
  for v in 2 14; do
    n=navegador-$v; echo "== $n (el modo guardado 1920x1080@60; Ctrl+F4 -> Navegador -> Abrir; Ctrl+Alt+F1; el mando no vuelve en $v intentos)"
    d=$(datos $n); o="$W/$n"
    printf 'modo=1920x1080@60\n' > "$d/rocola_pantalla.txt"
    mkdir -p "$W/cog-$v"
    cat > "$W/cog-$v/cog" <<EOF
#!/bin/sh
echo "\$(date +%s.%N) cog \$*" >> "$W/cog-$v/cog.txt"
trap 'exit 0' TERM
while :; do sleep 0.2; done
EOF
    chmod 755 "$W/cog-$v/cog"
    # en 1920x1080 el cliente de la hoja empieza en (480,187): la pestana Navegador en (1266,210) y su boton Abrir en (724,489)
    P="6:17+115,8:clic:1266:210,10.5:clic:724:489,15:17~1200,15.1:18~1000,15.3:112"
    abre $n "$d" "$P" "" $([ $v = 2 ] && echo 26 || echo 36) TERM ROCOLA_OS=1 ROCOLA_KMS_SIMULADO="$SIM;retoma=$v" \
      ROCOLA_NAVEGADOR_ORDEN="$NAV" ROCOLA_COG="$W/cog-$v/cog" ROCOLA_NAVEGADOR_TTY=/dev/null ROCOLA_NAVEGADOR_INICIO_HTML="$INICIO" ROCOLA_NAVEGADOR_TRAZA="$o/nav.txt"
    k="$o/kms.txt"; b="$d/rocola_os_bitacora.txt"
    grep -v ' gamma ' "$k" | sed 's/^/   kms: /' | cut -c1-110; grep -F '[pantalla]' "$b" | sed 's/^/   bit: /' | cut -c1-200
    si "$n: sale con 0, el navegador se abrio y se cerro con Ctrl+Alt+F1, y el mando no volvio a la primera" bash -c "[ \"\$(cat '$o/salio')\" = 0 ] && grep -q ' cierra (Ctrl+Alt+F1)' '$o/nav.txt' && grep -q ' retoma (composiciones ' '$o/nav.txt' && grep -q ' falla al retomar 1920x1080@60\$' '$k'"
    if [ $v = 2 ]; then
      si "$n: al volver, el mismo modo otra vez (2 veces sin el mando, la tercera bien) y la bitacora lo dice" bash -c "[ \$(grep -c ' falla sin el mando 1920x1080@60\$' '$k') = 2 ] && [ \"\$(grep ' modo ' '$k' | tail -1 | cut -d' ' -f2-)\" = 'modo 1920x1080@60' ] && grep -qF 'el mismo modo otra vez, bien (intento 3)' '$b'"
      si "$n: el modo guardado NO se borra (modo=1920x1080@60, sin modo_malo) y nunca se paso a lo automatico" bash -c "grep -qx 'modo=1920x1080@60' '$d/rocola_pantalla.txt' && ! grep -q modo_malo '$d/rocola_pantalla.txt' && ! grep -q ' modo 1024x768@75\$' '$k'"
    else
      si "$n: tras los 10 reintentos, lo de siempre: lo automatico (1024x768@75) y modo_malo con el porque" bash -c "[ \$(grep -c ' falla sin el mando 1920x1080@60\$' '$k') = 10 ] && grep -q ' modo 1024x768@75\$' '$k' && grep -qx 'modo=auto' '$d/rocola_pantalla.txt' && grep -qF 'modo_malo=1920x1080@60: drmModeSetCrtc -13' '$d/rocola_pantalla.txt'"
    fi
  done
fi

if [[ " $partes " == *" lista "* ]] || [[ " $partes " == *" apagada "* ]]; then
  for v in lista apagada; do
    [[ " $partes " == *" $v "* ]] || continue
    echo "== $v (Rocola OS sin la funcion «pantalla»: Ctrl+F4 abre la ventana, la hojita en gris diciendo por que)"
    d=$(datos $v)
    if [ $v = lista ]; then abre $v "$d" "6:17+115" "8:$v" 10 TERM ROCOLA_OS=vida,red ROCOLA_KMS_SIMULADO="$SIM"
    else printf 'spotify=0\npantalla=0\n' > "$d/rocola_os.txt"; printf 'modo=1920x1080@60\nbrillo=25\n' > "$d/rocola_pantalla.txt"; cp "$d/rocola_pantalla.txt" "$W/apagada-antes.txt"
         abre $v "$d" "6:17+115" "8:$v" 10 TERM ROCOLA_OS=1 ROCOLA_KMS_SIMULADO="$SIM"; fi
    si "$v: ni el KMS simulado ni nada de la pantalla (sin kms.txt; la pantalla de siempre)" bash -c "[ ! -e '$W/$v/kms.txt' ] && grep -q 'Pantalla: 1024x768 fuera de pantalla' '$W/$v/salida.txt'"
    if [ $v = lista ]; then si "$v: no escribe rocola_pantalla.txt" nohay "$d/rocola_pantalla.txt"
    else si "$v: rocola_pantalla.txt no se toca" cmp -s "$d/rocola_pantalla.txt" "$W/apagada-antes.txt"; fi
    si "$v: Ctrl+F4 abre la ventana" contiene "$d/rocola_os_bitacora.txt" "[ventana] Ctrl+F4 abierta"
    guarda_captura "$W/$v/$v.png" "$v.png"
  done
fi

if [[ " $partes " == *" sin "* ]]; then
  echo "== sin ROCOLA_OS: Ctrl+F4 no hace nada y nada se lee ni se pone (XP tal cual)"
  d=$(datos sin); rm -f "$d/rocola_os.txt"; printf 'modo=1920x1080@60\nbrillo=25\n' > "$d/rocola_pantalla.txt"; cp "$d/rocola_pantalla.txt" "$W/sin-antes.txt"
  abre sin "$d" "6:17+115,7:67" "8:sin" 10 TERM ROCOLA_KMS_SIMULADO="$SIM"
  si "sin: ni KMS simulado ni nada de Rocola OS (sin kms.txt, ni bitacora, ni RocolaOs en la traza)" bash -c "[ ! -e '$W/sin/kms.txt' ] && [ ! -e '$d/rocola_os_bitacora.txt' ] && ! grep -q RocolaOs '$W/sin/traza.txt'"
  si "sin: rocola_pantalla.txt no se toca (aunque diga 1920x1080) y la pantalla es la de siempre" bash -c "cmp -s '$d/rocola_pantalla.txt' '$W/sin-antes.txt' && grep -q 'Pantalla: 1024x768 fuera de pantalla' '$W/sin/salida.txt'"
  si "sin: con Ctrl+F4 ninguna hoja (la captura es la pantalla de la rocola)" bash -c "! grep -q 'dialogo' '$W/sin/traza.txt'"
  # y contra la referencia congelada: la misma escena (Ctrl+F4 a los 6 s, una moneda a los 7 y Ctrl+Alt+Q tres veces, como «cierreq»: con SIGTERM la referencia en JIT y este en NativeAOT no cierran igual, por eso el nivel 0 no corre «sigterm» en NativeAOT) con corre.sh
  if [ -f "$REF/rocola-xp.dll" ]; then
    VW="$W/v"; mkdir -p "$VW"
    for lado in ref este; do
      if [ $lado = ref ]; then pp="$REF"; elif [ "${prog#aot:}" != "$prog" ]; then pp="aot:$(cd "${prog#aot:}" && pwd)"; else pp="$(cd "$prog" && pwd)"; fi
      W="$VW" DATOS0="$DATOS0" SAL="$VW/salidas" SALE=0 bash "$XP/Pruebas/Ventanas/guiones/corre.sh" "$pp" "$lado" "6:17+115,7:67,9.5:17~900,9.55:18~800,9.65:81,9.8:81,9.95:81" "" 25 ROCOLA_KMS_SIMULADO="$SIM" > /dev/null 2>&1
    done
    r=(); [ -f "$RUIDO" ] && r=(--ruido "$RUIDO")
    si "sin: las dos rocolas de la comparacion abrieron y se cerraron solas (Ctrl+Alt+Q)" bash -c "grep -q 'closed Form1.Rocola. UserClosing' '$VW/salidas/ref/traza.txt' && grep -q 'closed Form1.Rocola. UserClosing' '$VW/salidas/este/traza.txt'"
    python3 "$XP/Pruebas/Ventanas/guiones/compara_escena.py" "$VW/salidas/ref" "$VW/salidas/este" "${r[@]}" > "$W/sin-compara.txt" 2>&1; rc=$?
    tail -1 "$W/sin-compara.txt" | sed 's/^/   /'
    si "sin: la escena contra la referencia congelada (Ctrl+F4, moneda y Ctrl+Alt+Q x3): IGUAL" test $rc = 0
  else echo "NO    sin la referencia con trazas ($REF): no se compara"; mal=$((mal + 1)); fi
  guarda_captura "$W/sin/sin.png" "sin-rocola-os.png"
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; exit 0; else echo "$mal MAL de $((bien + mal))"; exit 1; fi
