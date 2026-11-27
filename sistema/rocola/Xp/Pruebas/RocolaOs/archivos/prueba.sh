#!/bin/bash
# LA PRUEBA DEL GESTOR DE ARCHIVOS DE CTRL+F4 (fase 11, grupo archivos: RocolaOs/RocolaOs.Archivos*.cs y RocolaOs.Papelera.cs;
# FASE11_navegador-archivos.md; antes tambien probaba el navegador y las descargas, que se quitaron el 27-11-2026). Con datos INVENTADOS
# (los de partida de las escenas de ventanas, DATOS0), una memoria USB DE MENTIRA (una carpeta: ROCOLA_ARCHIVOS_USB) y llaves de mentira;
# fuera de pantalla y sin sonido. NADA de esto es el A4.
#   bash prueba.sh PROGRAMA [orden archivos sin]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT, con el ejecutable rocola-xp)
#   W: la carpeta de trabajo (de fabrica /root/rocola-trabajo/fase11/archivos/prueba); se rehace en cada corrida
#   CAPTURAS: donde se copian las capturas que se ensenan (de fabrica, ninguna)
#   DATOS0
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd); SIS=$(cd "$XP/../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-orden archivos sin}"
W=${W:-/root/rocola-trabajo/fase11/archivos/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
CAPTURAS=${CAPTURAS:-}
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

# Coordenadas (1024x768): la ventana de Ctrl+F4 en (29,2), su cliente desde (32,31); las pestanas Pantalla, Sonido y Archivos en
# (712,54), (818,54) y (924,54); la pagina desde (32,127). Archivos: «Datos de la rocola» (137,148), «Papelera» (323,148), la 1a USB
# (509,148) (en la papelera los botones son Ver, -, Devolver, -, Borrar ya, Vaciar papelera); la lista, renglon k
# en y = 235 + 24k (la lista se desplaza al elegir: los renglones de abajo salen de las capturas); los botones Abrir (113,620), Subir (246,620), Copiar (409,620), Renombrar (587,620), Borrar (735,620), Archivo nuevo
# (888,620). El editor (920x660) con su cliente desde (52,67): Guardar (184,691), Mostrar (420,691), Cerrar (840,691).
if [[ " $partes " == *" archivos "* ]]; then
  echo "== archivos (ver, copiar de la USB, la llave tapada y «Mostrar», un archivo nuevo con el teclado, renombrar, borrar a la papelera y devolver; vaciar la papelera con y sin confirmar)"
  d=$(datos archivos); u=$(usb archivos); o="$W/archivos"; f="$W/FUERA-archivos"
  printf 'hola\r\nmundo\r\n' > "$d/notas.txt"
  ln -s "$f" "$d/fuga"
  # dos para tirar a la papelera: un texto y una llave (0600)
  echo "de internet" > "$d/0_bajado.txt"
  printf 'token=TOKENBAJADODEPRUEBA-0123456789\n' > "$d/0_token_bajado.txt"; chmod 600 "$d/0_token_bajado.txt"
  P="6:17+115,8:clic:924:54,10:clic:509:148"
  P="$P,12:clic:300:331,12.5:clic:409:620,14:clic:300:307,14.5:clic:409:620,16:clic:300:259,16.5:clic:113:620"
  P="$P,18:clic:137:148,$(escribe 19 0.12 rocola_spotify_a),21.5:13,23.5:clic:420:691,25:9,25.3:13,27:27"
  P="$P,28:clic:888:620,$(escribe 29 0.12 rocola_genius.txt),32:13,$(escribe 33 0.12 "$GENIUS_PRUEBA"),36.5:17+83,37.5:27"
  P="$P,38.5:clic:300:283,$(escribe 39 0.12 notas),39.8:clic:587:620,40.8:8,41:8,41.2:8,41.4:8,$(escribe 41.6 0.12 2.txt),42.5:13"
  P="$P,43.5:clic:735:620,45:9,45.3:13"
  # la papelera: notas2.txt, devolver. 0_bajado.txt y la llave, a la papelera
  P="$P,47:clic:323:148,49:clic:300:235,49.5:clic:409:620"
  P="$P,52:clic:137:148,54:clic:300:307,57:clic:735:620,58.5:9,58.8:13,60:clic:300:307,60.5:clic:735:620,62:9,62.3:13"
  # la papelera con las dos: ver la llave (tapada, sin «Mostrar»); vaciar: primero «No» (Intro con el foco en No), luego «Sí»
  P="$P,64:clic:323:148,66:clic:300:235,66.5:clic:113:620,69:27,70:clic:888:620,71.5:13,73:clic:888:620,74:9,74.3:13,77:27"
  abre archivos "$d" "$P" "9.5:archivos-datos,11.5:archivos-usb,13.5:archivos-copiada,17.5:archivos-grande,23:llave-tapada,24.5:mostrar-confirma,26.5:llave-mostrada,31.5:nombre-nuevo,37:genius-guardada,44.5:borrar-confirma,46.5:archivos-borrado,48.5:papelera,51:papelera-devuelto,53.5:datos-con-bajados,59.5:bajado-tirado,65.5:papelera-llena,68:papelera-llave-tapada,71:vaciar-confirma,72.5:vaciar-no,76:papelera-vacia" \
    80 TERM ROCOLA_OS=archivos ROCOLA_ARCHIVOS_USB="$u"
  b="$d/rocola_os_bitacora.txt"
  si "sale con 0" test "$(cat $o/salio)" = 0
  CAPS="archivos-datos archivos-usb archivos-copiada archivos-grande llave-tapada mostrar-confirma llave-mostrada nombre-nuevo genius-guardada borrar-confirma archivos-borrado papelera papelera-devuelto datos-con-bajados bajado-tirado papelera-llena papelera-llave-tapada vaciar-confirma vaciar-no papelera-vacia"
  n=0; for x in $CAPS; do [ "$(medida $o/$x.png)" = 1024x768 ] && n=$((n + 1)); done
  si "las 20 capturas (1024x768): $n" test $n = 20
  si "copiada de la USB: rocola_spotify_api.txt igual y con permisos 0600 ($(modo $d/rocola_spotify_api.txt))" bash -c "cmp -s '$u/rocola_spotify_api.txt' '$d/rocola_spotify_api.txt' && [ \"\$(stat -c %a '$d/rocola_spotify_api.txt')\" = 600 ]"
  si "rocola_spotify.txt de la USB quedo como rocola_spotify_llave.txt (0600) y no piso la bitacora de Spotify" bash -c "cmp -s '$u/rocola_spotify.txt' '$d/rocola_spotify_llave.txt' && [ \"\$(stat -c %a '$d/rocola_spotify_llave.txt')\" = 600 ] && ! cmp -s '$u/rocola_spotify.txt' '$d/rocola_spotify.txt' 2>/dev/null"
  si "el grande (70 KB) no se abre y lo dice; nada se escribio en la USB" bash -c "[ \$(ls -A '$u' | wc -l) = 7 ] && cmp -s <(printf '%s' \"\$API_PRUEBA\") '$u/rocola_spotify_api.txt'"
  si "archivo nuevo escrito con el teclado (nombre y contenido) y Ctrl+S: rocola_genius.txt = «$GENIUS_PRUEBA» y 0600" bash -c "[ \"\$(cat '$d/rocola_genius.txt')\" = '$GENIUS_PRUEBA' ] && [ \"\$(stat -c %a '$d/rocola_genius.txt')\" = 600 ] && [ \"\$(tail -c 1 '$d/rocola_genius.txt' | od -An -c | tr -d ' ')\" = '\\n' ]"
  si "renombrado notas.txt -> notas2.txt, borrado con su confirmacion A LA PAPELERA y devuelto (igual, en su sitio)" bash -c "[ ! -e '$d/notas.txt' ] && cmp -s '$d/notas2.txt' <(printf 'hola\r\nmundo\r\n') && grep -qF 'renombrado: notas.txt -> notas2.txt' '$b' && grep -qF 'a la papelera: notas2.txt' '$b' && grep -qF 'devuelto de la papelera: notas2.txt' '$b'"
  si "dos a la papelera: 0_bajado.txt y la llave (ya no estan en los datos)" bash -c "[ ! -e '$d/0_bajado.txt' ] && [ ! -e '$d/0_token_bajado.txt' ] && grep -qF 'a la papelera: 0_bajado.txt' '$b' && grep -qF 'a la papelera: 0_token_bajado.txt' '$b'"
  si "vaciar: con «No» no se vacia; con «Sí» si (una sola vez: 2 cosas) y la papelera queda vacia" bash -c "[ \$(grep -c 'papelera vaciada' '$b') = 1 ] && grep -qF 'papelera vaciada: 2' '$b' && [ -z \"\$(ls -A '$d/papelera')\" ]"
  si "la llave de la papelera se vio tapada: nunca «a la vista» (ni en la bitacora)" bash -c "! grep -qF 'llave a la vista: token_bajado.txt' '$b'"
  si "lo de fuera, intacto (y el enlace de los datos que sale, sin tocar)" bash -c "[ \"\$(cat '$f/secreto.txt')\" = 'lo de fuera' ] && [ \$(ls -A '$f' | wc -l) = 1 ] && [ -L '$d/fuga' ]"
  si "la bitacora: copias, la llave a la vista, lo guardado (sin lo de dentro)" bash -c "grep -qF 'copiado de la USB a los datos: rocola_spotify_api.txt (llave, 0600)' '$b' && grep -qF 'copiado de la USB a los datos: rocola_spotify_llave.txt (llave, 0600)' '$b' && grep -qF 'llave a la vista: rocola_spotify_api.txt' '$b' && grep -qF 'guardado: rocola_genius.txt (llave, 0600)' '$b'"
  si "NINGUNA llave en las bitacoras, rocola_error.txt ni la salida (ni la de la API, ni la de Soloist, ni la de Genius)" bash -c "! grep -rqE 'SECRETODEPRUEBA|IDDEPRUEBA|LLAVEDESOLOIST|GENIUS-prueba|TOKENBAJADO' '$d'/rocola_os_bitacora.txt '$d'/rocola_error.txt '$o/salida.txt'"
  si "rocola_error.txt sin fallos de Rocola OS" nocontiene "$d/rocola_error.txt" "Rocola OS"
  for x in $CAPS; do guarda_captura "$o/$x.png" "$x.png"; done
fi

# ------------------------------------------------------------------ sin ROCOLA_OS: nada
if [[ " $partes " == *" sin "* ]]; then
  echo "== sin (sin ROCOLA_OS: Ctrl+F4 no hace nada; ni las USB)"
  d=$(datos sin); u=$(usb sin); o="$W/sin"; mkdir -p "$o"
  abre sin "$d" "6:17+115,8:clic:924:54,10:clic:276:333" "9:sin-ctrlf4" 13 TERM ROCOLA_ARCHIVOS_USB="$u"
  si "sale con 0" test "$(cat $o/salio)" = 0
  si "ni bitacora de Rocola OS" bash -c "[ ! -e '$d/rocola_os_bitacora.txt' ]"
fi

echo "== $bien bien, $mal mal"
[ "$mal" = 0 ]
