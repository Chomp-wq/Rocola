#!/bin/bash
# LA PRUEBA DEL ANDAMIO DE LA FASE 11 (grupo andamio: RocolaOs/RocolaOs*.cs; la funcion de prueba RocolaOs/PruebaAndamio.cs, que solo va
# con ROCOLA_OS=prueba-andamio). Con datos INVENTADOS (los de partida de las escenas de ventanas, DATOS0), fuera de pantalla y sin sonido.
#   bash prueba.sh PROGRAMA [ganchos orden escenas]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT, bin/Release/net10.0) o aot:CARPETA (NativeAOT, con el ejecutable rocola-xp)
#   W: la carpeta de trabajo (por defecto /root/rocola-trabajo/fase11/andamio/prueba); se rehace en cada corrida (unos 3 MB)
#   DATOS0: los datos de partida (por defecto /root/rocola-trabajo/port/ventanas/datos0, de Pruebas/Ventanas/guiones/datos.sh: se copian,
#           nunca se tocan)
#   RUIDO_SIGTERM: el ruido medido de la escena «sigterm» (ruido.sh; por defecto /root/rocola-trabajo/port/ventanas/ruido/sigterm.json;
#           sin el, una holgura de 80 en las cuentas)
# ganchos: ganchos.py sobre el manifiesto de verdad (TODO BIEN) y sobre manifiestos estropeados a proposito (cada uno, MAL): un gancho
#          movido, uno que falta, una linea de antes que no es la de la base, la marca de otro id y un id repetido.
# orden:   `rocola-xp --prueba-rocola-os` (RocolaOs/PruebaAndamio.cs), sin la rocola: lo que decide ROCOLA_OS sin la variable, con «0»,
#          «1», una lista y un valor raro; y los ajustes de rocola_os.txt: lo de fabrica, lo que dice el archivo (la ultima de una
#          repetida, uno que no vale), PonAjuste y la orden «ajuste» del buzon (AjusteDeFuera), las claves que no conoce y los comentarios
#          en su sitio, la escritura segura (el .bak, nada de .tmp), el .bak cuando falta el bueno y que sin la variable no se lee ni se
#          escribe nada.
# escenas: la rocola entera (moneda a los 7 s): sin la variable y con «0» (ni latido ni nada de Rocola OS), con «1» (el latido, solo, y la
#          traza IGUAL a la de sin la variable quitando lo de RocolaOs: con compara_trazas.py --ignora), y con la funcion de prueba:
#          el arranque, sus ajustes, el hilo de fondo (nice 19), el encargo, los latidos, los renglones de vivo.txt (ROCOLA_RUN), el
#          envoltorio del cierre con SIGTERM (WindowsShutDown), SIGUSR1 (TaskManagerClosing) y Alt+F4 (UserClosing), ANTES de que XP guarde
#          (rocola_estado.txt sigue ahi), rocola_os.txt guardado con lo desconocido en su sitio, su respaldo, un latido y un hilo que
#          lanzan (la rocola sigue; rocola_error.txt), el plazo comun del cierre (4 s) y el apagon (kill -9: no corre nada).
# Cada comprobacion: BIEN o MAL en una linea; al final «TODO BIEN (N comprobaciones)» o cuantas MAL (y sale con 1).
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [ganchos orden escenas]}"; shift
partes="${*:-ganchos orden escenas}"
W=${W:-/root/rocola-trabajo/fase11/andamio/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
hay() { [ -e "$1" ]; }
nohay() { [ ! -e "$1" ]; }
igual() { cmp -s "$1" "$2"; }
cuenta() { grep -cF -- "$2" "$1" 2>/dev/null; }
entre() { [ "$1" -ge "$2" ] && [ "$1" -le "$3" ]; }

# ---------------------------------------------------------------- ganchos
if [[ " $partes " == *" ganchos "* ]]; then
  echo "== ganchos"
  GP="$XP/Pruebas/RocolaOs/ganchos.py"; M="$XP/RocolaOs/GANCHOS.tsv"
  python3 "$GP" > "$W/ganchos.txt" 2>&1; r=$?
  sed 's/^/   /' "$W/ganchos.txt"
  si "ganchos.py con el manifiesto: sale con 0 y TODO BIEN" test $r = 0 -a "$(tail -1 "$W/ganchos.txt")" = "TODO BIEN"
  si "ganchos.py: los 112 archivos de Codigo/ comparados con la base" contiene "$W/ganchos.txt" "112 archivos comparados con d5bc2b0"   # (junta 3: la base es d5bc2b0; en la junta 2, 69e6547)
  # manifiestos estropeados a proposito (con --manifiesto): cada uno tiene que salir con 1 y decir por que
  estropea() {   # NOMBRE "lo que tiene que decir" PROGRAMA_DE_AWK
    local n="$1" dice="$2"; awk -F'\t' -v OFS='\t' "$3" "$M" > "$W/m-$n.tsv"
    python3 "$GP" --manifiesto "$W/m-$n.tsv" > "$W/g-$n.txt" 2>&1; local r=$?
    si "ganchos.py con $n: sale con 1 y dice «$dice»" test $r = 1 -a "$(grep -cF -- "$dice" "$W/g-$n.txt")" -ge 1
  }
  estropea movido "SE MOVIO" '$1=="g03"{$3=$3+1} {print}'
  estropea falta "y no esta en el manifiesto" '$1!="g05"{print}'
  estropea antes "NO queda como en d5bc2b0" '$1=="g02"{$4=$4"x"} {print}'
  estropea marca "no lleva su marca" '$1=="g04"{$1="g44"} {print}'
  estropea repetido "esta dos veces" '{print} $1=="g06"{print}'
  estropea fuera "en Nativo/Form1.Ventana.cs no esta la linea" '$1=="g22"{$5=$5" "} {print}'
fi

# ---------------------------------------------------------------- orden (sin la rocola)
orden() {   # DATOS ROCOLA_OS ARGUMENTOS...: la orden de prueba, con su salida en $W/o.txt
  local d="$1" v="$2"; shift 2
  if [ "$v" = "-" ]; then env -u ROCOLA_OS ROCOLA_DATOS="$d" ROCOLA_PANTALLA=fuera:64x64 timeout 60 "${RX[@]}" --prueba-rocola-os "$@" > "$W/o.txt" 2>&1
  else env ROCOLA_CATALOGO_URL=http://127.0.0.1:9/catalogo ROCOLA_PORTADAS_CATALOGO=http://127.0.0.1:9 ROCOLA_OS="$v" ROCOLA_DATOS="$d" ROCOLA_PANTALLA=fuera:64x64 timeout 60 "${RX[@]}" --prueba-rocola-os "$@" > "$W/o.txt" 2>&1; fi
  oc=$?
}
if [[ " $partes " == *" orden "* ]]; then
  echo "== orden"
  D="$W/datos-orden"; mkdir -p "$D"
  printf 'prueba-andamio=1\n' > "$D/rocola_os.txt"   # (con «1» no la enciende: es solo de la lista)
  orden "$D" - variable; cp "$W/o.txt" "$W/o-sin.txt"
  si "sin ROCOLA_OS: activo=0, prueba-andamio ni pedida ni encendida" contiene "$W/o-sin.txt" "activo=0 variable=«» lista=0"
  si "sin ROCOLA_OS: prueba-andamio apagada" contiene "$W/o-sin.txt" "funcion prueba-andamio: pedida=0 encendida=0"
  orden "$D" 0 variable
  si "ROCOLA_OS=0: activo=0 y nada encendido" bash -c "grep -qF 'activo=0 variable=«0» lista=0' '$W/o.txt' && grep -qF 'funcion prueba-andamio: pedida=0 encendida=0' '$W/o.txt'"
  orden "$D" 1 variable
  si "ROCOLA_OS=1: activo=1 sin lista" contiene "$W/o.txt" "activo=1 variable=«1» lista=0"
  si "ROCOLA_OS=1 con «prueba-andamio=1» en el archivo: la de prueba sigue apagada (solo con la lista)" contiene "$W/o.txt" "funcion prueba-andamio: pedida=0 encendida=0"
  orden "$D" prueba-andamio variable
  si "ROCOLA_OS=prueba-andamio: activo=1 con lista" contiene "$W/o.txt" "activo=1 variable=«prueba-andamio» lista=1"
  si "ROCOLA_OS=prueba-andamio: pedida y encendida" contiene "$W/o.txt" "funcion prueba-andamio: pedida=1 encendida=1"
  si "una funcion que no existe: ni pedida ni encendida" contiene "$W/o.txt" "funcion no-existe: pedida=0 encendida=0"
  orden "$D" "otra, prueba-andamio" variable
  si "ROCOLA_OS=«otra, prueba-andamio» (comas y espacios): encendida" contiene "$W/o.txt" "funcion prueba-andamio: pedida=1 encendida=1"
  orden "$D" si variable
  si "ROCOLA_OS=si (ni 1 ni 0): se toma como lista, sin nada encendido" bash -c "grep -qF 'activo=1 variable=«si» lista=1' '$W/o.txt' && grep -qF 'funcion prueba-andamio: pedida=0 encendida=0' '$W/o.txt'"
  si "las claves declaradas: las de la prueba, sin la de encenderla (es solo de la lista)" contiene "$W/o.txt" "claves: prueba-andamio.cierre_ms prueba-andamio.contador prueba-andamio.falla prueba-andamio.numero prueba-andamio.texto"
  si "y la orden sale con 0" test $oc = 0
  # LOS AJUSTES: un archivo con de todo (CRLF, comentario, claves de otra version, una repetida, una que no vale, un renglon raro, uno
  # vacio y el ultimo sin salto de linea)
  printf '# comentario del dueno\r\nclave_de_otra_version=algo\r\nprueba-andamio.numero=42\r\notra.desconocida = con espacios = y signos\r\nprueba-andamio.texto=adios\r\nprueba-andamio.numero=43\r\nprueba-andamio.cierre_ms=noesnumero\r\nrenglon raro sin igual\r\n\r\nultima.clave=1' > "$D/rocola_os.txt"
  cp "$D/rocola_os.txt" "$W/original.txt"
  # sin la variable: nada se lee ni se escribe
  orden "$D" - ajustes pon prueba-andamio.numero 50 fuera prueba-andamio.contador=5
  si "sin ROCOLA_OS: los de fabrica (no lee el archivo: numero 7, texto hola)" bash -c "grep -qF 'lee prueba-andamio.numero = «7» cambiado=0' '$W/o.txt' && grep -qF 'lee prueba-andamio.texto = «hola» cambiado=0' '$W/o.txt'"
  si "sin ROCOLA_OS: PonAjuste no hace nada" contiene "$W/o.txt" "pon prueba-andamio.numero «50» -> no"
  si "sin ROCOLA_OS: la orden «ajuste» contesta que esta apagado" contiene "$W/o.txt" "-> mal: Rocola OS esta apagado"
  si "sin ROCOLA_OS: rocola_os.txt igual byte a byte, sin .bak, sin .tmp y sin bitacora" bash -c "cmp -s '$D/rocola_os.txt' '$W/original.txt' && [ ! -e '$D/rocola_os.txt.bak' ] && [ ! -e '$D/rocola_os.txt.tmp' ] && [ ! -e '$D/rocola_os_bitacora.txt' ]"
  # con la variable
  orden "$D" prueba-andamio ajustes pon prueba-andamio.numero 50 pon prueba-andamio.numero 500 quita prueba-andamio.texto pon no-declarada x \
    fuera prueba-andamio.contador=5 fuera no-existe=1 fuera prueba-andamio.numero=abc fuera "sin igual" fuera prueba-andamio.cierre_ms= fuera prueba-andamio.numero=9
  cp "$W/o.txt" "$W/o-ajustes.txt"
  si "de una repetida vale la ultima (numero 43), y esta cambiada" contiene "$W/o.txt" "lee prueba-andamio.numero = «43» cambiado=1"
  si "lo que dice el archivo (texto adios)" contiene "$W/o.txt" "lee prueba-andamio.texto = «adios» cambiado=1"
  si "una que no esta: la de fabrica (contador 0, falla 0)" bash -c "grep -qF 'lee prueba-andamio.contador = «0» cambiado=0' '$W/o.txt' && grep -qF 'lee prueba-andamio.falla = «0» cambiado=0' '$W/o.txt'"
  si "una que no vale (cierre_ms=noesnumero): la de fabrica, y no cuenta como cambiada" contiene "$W/o.txt" "lee prueba-andamio.cierre_ms = «0» cambiado=0"
  si "una sin declarar: null" contiene "$W/o.txt" "lee no-declarada = «(null)» cambiado=0"
  si "PonAjuste de un valor que vale: si; uno fuera de su rango: no; una sin declarar: no" bash -c "grep -qF 'pon prueba-andamio.numero «50» -> si' '$W/o.txt' && grep -qF 'pon prueba-andamio.numero «500» -> no' '$W/o.txt' && grep -qF 'pon no-declarada «x» -> no' '$W/o.txt'"
  si "quitar una: vuelve a la de fabrica" contiene "$W/o.txt" "quita prueba-andamio.texto -> si"
  si "«ajuste» de fuera bien, y como no cambia en vivo, dice que vale desde el siguiente arranque" contiene "$W/o.txt" "fuera «prueba-andamio.contador=5» -> bien: prueba-andamio.contador=5 (vale desde el siguiente arranque del programa)"
  si "«ajuste» de una clave que no es de Rocola OS: mal, y dice cuales hay" contiene "$W/o.txt" "fuera «no-existe=1» -> mal: «no-existe» no es un ajuste de Rocola OS (los que hay: prueba-andamio.cierre_ms"
  si "«ajuste» con un valor que no vale: mal, y dice cuales valen" contiene "$W/o.txt" "fuera «prueba-andamio.numero=abc» -> mal: «abc» no vale para «prueba-andamio.numero» (un numero de 0 a 100)"
  si "«ajuste» sin «=»: mal" contiene "$W/o.txt" "fuera «sin igual» -> mal: se espera «clave=valor»"
  si "«ajuste» con el valor vacio: la de fabrica" contiene "$W/o.txt" "fuera «prueba-andamio.cierre_ms=» -> bien: prueba-andamio.cierre_ms=0 (el de fabrica)"
  si "«ajuste» de una que cambia en vivo: sin «siguiente arranque»" bash -c "grep -qxF 'fuera «prueba-andamio.numero=9» -> bien: prueba-andamio.numero=9' '$W/o.txt'"
  si "lo que queda: numero 9, texto hola, contador 5, cierre_ms 0" bash -c "grep -qF 'queda prueba-andamio.numero = «9»' '$W/o.txt' && grep -qF 'queda prueba-andamio.texto = «hola»' '$W/o.txt' && grep -qF 'queda prueba-andamio.contador = «5»' '$W/o.txt' && grep -qF 'queda prueba-andamio.cierre_ms = «0»' '$W/o.txt'"
  printf '# comentario del dueno\nclave_de_otra_version=algo\nprueba-andamio.numero=9\notra.desconocida = con espacios = y signos\nrenglon raro sin igual\n\nultima.clave=1\nprueba-andamio.contador=5\n' > "$W/esperado.txt"
  si "rocola_os.txt byte a byte: lo desconocido y los comentarios en su sitio, la repetida una sola vez donde la primera, lo quitado fuera, lo nuevo al final, en LF" igual "$D/rocola_os.txt" "$W/esperado.txt"
  si "escritura segura: el .bak es el de antes, y no queda .tmp" bash -c "cmp -s '$D/rocola_os.txt.bak' '$W/original.txt' && [ ! -e '$D/rocola_os.txt.tmp' ]"
  si "la bitacora propia: lo que no valia, la clave sin declarar y los ajustes de fuera" bash -c "grep -qF '«prueba-andamio.cierre_ms=noesnumero» no vale' '$D/rocola_os_bitacora.txt' && grep -qF 'PonAjuste de una clave sin declarar: «no-declarada»' '$D/rocola_os_bitacora.txt' && [ \"\$(grep -cF 'ajuste de fuera:' '$D/rocola_os_bitacora.txt')\" = 3 ]"
  si "y la orden sale con 0" test $oc = 0
  # sin el bueno, el .bak (como LeeLineasSeguro de XP)
  D2="$W/datos-bak"; mkdir -p "$D2"; printf 'prueba-andamio.numero=33\n' > "$D2/rocola_os.txt.bak"
  orden "$D2" prueba-andamio ajustes
  si "sin rocola_os.txt y con su .bak: lee el .bak" contiene "$W/o.txt" "lee prueba-andamio.numero = «33» cambiado=1"
  si "y sin cambios no escribe nada" nohay "$D2/rocola_os.txt"
fi

# ---------------------------------------------------------------- escenas (la rocola entera)
# abre NOMBRE DATOS "PULSA" SEGUNDOS SENAL [VAR=valor...]: la rocola fuera de pantalla con esos datos; a los SEGUNDOS, la SENAL (NADA: no se
# manda, sale sola) y espera a que salga (40 s; despues la mata). Deja en $W/NOMBRE: salida.txt, traza.txt, run/ (ROCOLA_RUN), salio (el
# codigo de verdad) y tardo (de la senal a la salida).
abre() {
  local n="$1" d="$2" pulsa="$3" seg="$4" sen="$5"; shift 5
  local o="$W/$n"; rm -rf "$o"; mkdir -p "$o/run"
  ( cd "$d" && exec env ROCOLA_CATALOGO_URL=http://127.0.0.1:9/catalogo ROCOLA_PORTADAS_CATALOGO=http://127.0.0.1:9 ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$!
  sleep "$seg"; [ "$sen" != NADA ] && kill -"$sen" $p 2>/dev/null; local t0; t0=$(date +%s.%N)
  ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
  echo "$(date +%s.%N) - $t0" | bc > "$o/tardo"
}
datos() { local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/; echo "$d"; }
latidos() { grep -c "	tick RocolaOs:latido 1000$" "$1" 2>/dev/null; }
if [[ " $partes " == *" escenas "* ]]; then
  echo "== escenas"
  [ -f "$DATOS0/rocola_estado.txt" ] || echo "(aviso: $DATOS0 sin rocola_estado.txt: las comprobaciones del estado no valen)"
  # SIN LA VARIABLE y con «0»: ni latido ni nada de Rocola OS
  for v in sin cero; do
    d=$(datos $v)
    if [ $v = sin ]; then abre $v "$d" "7:67" 10 TERM; else abre $v "$d" "7:67" 10 TERM ROCOLA_OS=0; fi
    t="$W/$v/traza.txt"
    si "$v: sale con 0" test "$(cat $W/$v/salio)" = 0
    si "$v: en la traza nada de RocolaOs (ni latido ni encargos)" nocontiene "$t" "RocolaOs"
    si "$v: no escribe rocola_os.txt ni la bitacora de Rocola OS, ni respaldo suyo" bash -c "[ ! -e '$d/rocola_os.txt' ] && [ ! -e '$d/rocola_os_bitacora.txt' ] && [ ! -e '$d/respaldo/rocola-os' ]"
    si "$v: el apagado de siempre (WindowsShutDown y rocola_estado.txt borrado)" bash -c "grep -q '	closed Form1.Rocola. WindowsShutDown$' '$t' && [ ! -e '$d/rocola_estado.txt' ]"
  done
  # CON «1» (nada que encender): el latido, solo. Desde la junta de Spotify (15-11-2026) la funcion «spotify» va ENCENDIDA de fabrica:
  # para que aqui no haya nada que encender, rocola_os.txt la apaga («spotify=0», lo que deja «spotify apagar»). Encendida de fabrica y
  # sin llaves: la escena «fabrica» de Pruebas/RocolaOs/spotify-xp/prueba.sh.
  d=$(datos uno); printf 'spotify=0\n' > "$d/rocola_os.txt"; cp "$d/rocola_os.txt" "$W/uno-original.txt"
  abre uno "$d" "7:67" 10 TERM ROCOLA_OS=1; t="$W/uno/traza.txt"
  n=$(latidos "$t")
  si "uno: sale con 0" test "$(cat $W/uno/salio)" = 0
  si "uno: el latido, una vez por segundo ($n en 10 s)" entre "$n" 6 11
  # (lo que sigue vale mientras ninguna otra funcion de los grupos que vienen este encendida de fabrica; con ellas, lo que valga es la
  # comparacion con --ignora de abajo)
  si "uno: en la traza, de Rocola OS solo el latido" test "$(grep -c "RocolaOs" "$t")" = "$n"
  si "uno: la bitacora dice que arranco, sin la de prueba (solo va con la lista)" bash -c "grep -qF 'arranca con ROCOLA_OS=«1»:' '$d/rocola_os_bitacora.txt' && ! grep -F 'arranca con ROCOLA_OS' '$d/rocola_os_bitacora.txt' | grep -qF 'prueba-andamio'"
  si "uno: y el cierre (WindowsShutDown)" contiene "$d/rocola_os_bitacora.txt" "cierre (WindowsShutDown): "
  si "uno: no cambia rocola_os.txt (nada cambio)" igual "$d/rocola_os.txt" "$W/uno-original.txt"
  si "uno: con spotify=0, nada de Spotify (ni su bitacora ni nada en /run)" bash -c "[ ! -e '$d/rocola_spotify.txt' ] && [ ! -e '$W/uno/run/rocola-xp' ]"
  si "uno: el apagado de siempre (rocola_estado.txt borrado)" nohay "$d/rocola_estado.txt"
  # (con el ruido medido de la escena «sigterm» de las ventanas, que es esta misma: moneda a los 7 s y SIGTERM a los 10; las cuentas por
  # segundo de los relojes y de los encargos de otros hilos cambian solas de una corrida a otra: arranques mas o menos rapidos)
  R=${RUIDO_SIGTERM:-/root/rocola-trabajo/port/ventanas/ruido/sigterm.json}; if [ -f "$R" ]; then hr=(--ruido "$R"); else hr=(--holgura 80); fi
  python3 "$XP/Pruebas/Ventanas/guiones/compara_trazas.py" "$W/sin/traza.txt" "$t" --ignora RocolaOs: "${hr[@]}" > "$W/uno/compara.txt" 2>&1; r=$?
  sed 's/^/   /' "$W/uno/compara.txt" | head -12
  si "uno: la traza IGUAL a la de sin la variable quitando lo de RocolaOs (compara_trazas.py --ignora RocolaOs:)" test $r = 0
  si "uno: --ignora quito justo los $n ticks del latido (y nada de la traza sin la variable)" contiene "$W/uno/compara.txt" "NOTA ignorados (RocolaOs:): en A 0 relojes y 0 encargos, en B $n relojes y 0 encargos"
  # LA FUNCION DE PRUEBA, con SIGTERM (el apagado)
  d=$(datos lista); printf '# del dueno\nclave_vieja=7\n' > "$d/rocola_os.txt"; cp "$d/rocola_os.txt" "$W/lista-original.txt"
  abre lista "$d" "7:67" 10 TERM ROCOLA_OS=prueba-andamio; t="$W/lista/traza.txt"; b="$d/rocola_os_bitacora.txt"
  si "lista: sale con 0" test "$(cat $W/lista/salio)" = 0
  si "lista: arranca con la de prueba encendida" contiene "$b" "encendidas: prueba-andamio"
  si "lista: su arranque en el hilo de la pantalla, con lo de fabrica" contiene "$b" "arranque: numero=7 texto=hola contador=0 encendida=si en la pantalla=si"
  si "lista: su hilo de fondo con nice 19 (por debajo del repaso de XP, 4), de fondo y con su nombre" contiene "$b" "hilo de fondo: nice=19 nombre=RocolaOs:prueba-andamio de fondo=si en la pantalla=no"
  si "lista: el encargo corre en el hilo de la pantalla" contiene "$b" "encargo: en la pantalla=si"
  si "lista: y en la traza se nombra «RocolaOs:prueba-andamio»" contiene "$t" "	encargo RocolaOs:prueba-andamio"
  si "lista: el latido en el hilo de la pantalla" contiene "$b" "primer latido: en la pantalla=si"
  n=$(latidos "$t")
  si "lista: $n latidos en 10 s" entre "$n" 6 11
  si "lista: vivo.txt con su renglon (RenglonesVivos, en ROCOLA_RUN)" bash -c "grep -qE '^prueba-andamio=latidos [0-9]+$' '$W/lista/run/rocola-os-prueba-vivo.txt'"
  si "lista: AlCerrar en la pantalla con la razon WindowsShutDown, ANTES de que XP borre el estado (rocola_estado.txt aun estaba)" contiene "$b" "al cerrar (pantalla): razon=WindowsShutDown razon del andamio=WindowsShutDown latidos="
  si "lista: (lo de antes: estado de XP presente=si, en la pantalla)" bash -c "grep -F 'al cerrar (pantalla)' '$b' | grep -qF 'estado de XP presente=si en la pantalla=si'"
  si "lista: ParaAlCerrar en su hilo, con la misma razon, y acaba" bash -c "grep -qF 'para al cerrar (su hilo): razon=WindowsShutDown en la pantalla=no espera 0 ms' '$b' && grep -qF 'para al cerrar: acabo' '$b'"
  si "lista: la bitacora del cierre con lo que tardo cada parte" bash -c "grep -qE 'cierre \(WindowsShutDown\): prueba-andamio \(pantalla\) [0-9]+ ms; prueba-andamio \(su hilo\) [0-9]+ ms; en total' '$b'"
  si "lista: y XP borro el estado despues (WindowsShutDown)" bash -c "[ ! -e '$d/rocola_estado.txt' ] && grep -q '	closed Form1.Rocola. WindowsShutDown$' '$t'"
  printf '# del dueno\nclave_vieja=7\nprueba-andamio.contador=1\n' > "$W/lista-esperado.txt"
  si "lista: rocola_os.txt guardado detras (contador 1), con lo que no conoce en su sitio" igual "$d/rocola_os.txt" "$W/lista-esperado.txt"
  si "lista: su .bak es el de antes" igual "$d/rocola_os.txt.bak" "$W/lista-original.txt"
  si "lista: su respaldo (respaldo/rocola-os/<fecha>) con lo de ANTES de la sesion" bash -c "f=\$(ls '$d'/respaldo/rocola-os/*/rocola_os.txt 2>/dev/null | head -1); [ -n \"\$f\" ] && cmp -s \"\$f\" '$W/lista-original.txt'"
  si "lista: el respaldo de XP no lo ve (sigue con sus carpetas de fecha)" bash -c "ls '$d/respaldo' | grep -qE '^[0-9]{4}-[0-9]{2}-[0-9]{2}_'"
  # la misma, otra vez con SIGUSR1 (un reinicio propio de Rocola OS: XP conserva el estado)
  cp "$DATOS0/rocola_estado.txt" "$d/" 2>/dev/null
  abre usr1 "$d" "7:67" 10 USR1 ROCOLA_OS=prueba-andamio; t="$W/usr1/traza.txt"
  si "usr1: sale con 0" test "$(cat $W/usr1/salio)" = 0
  si "usr1: AlCerrar con TaskManagerClosing, antes de que XP guarde" bash -c "grep -F 'al cerrar (pantalla): razon=TaskManagerClosing razon del andamio=TaskManagerClosing' '$b' | grep -qF 'estado de XP presente=si'"
  si "usr1: ParaAlCerrar con TaskManagerClosing" contiene "$b" "para al cerrar (su hilo): razon=TaskManagerClosing"
  si "usr1: XP conserva el estado (rocola_estado.txt queda)" hay "$d/rocola_estado.txt"
  si "usr1: el contador subio otra vez (2): lo guardado se leyo al arrancar" contiene "$d/rocola_os.txt" "prueba-andamio.contador=2"
  si "usr1: dos respaldos suyos (uno por arranque)" test "$(ls -d "$d"/respaldo/rocola-os/*/ 2>/dev/null | wc -l)" = 2
  # Alt+F4 (UserClosing): sale sola
  abre altf4 "$d" "7:67,9:18+115" 14 NADA ROCOLA_OS=prueba-andamio; t="$W/altf4/traza.txt"
  si "altf4: sale con 0" test "$(cat $W/altf4/salio)" = 0
  si "altf4: AlCerrar con UserClosing, antes de que XP borre el estado" bash -c "grep -F 'al cerrar (pantalla): razon=UserClosing razon del andamio=UserClosing' '$b' | grep -qF 'estado de XP presente=si'"
  si "altf4: y XP borro el estado (UserClosing)" bash -c "[ ! -e '$d/rocola_estado.txt' ] && grep -q '	closed Form1.Rocola. UserClosing$' '$t'"
  # un latido y un hilo que lanzan: se apuntan y la rocola sigue
  d=$(datos falla); printf 'prueba-andamio.falla=1\n' > "$d/rocola_os.txt"
  abre falla "$d" "7:67" 10 TERM ROCOLA_OS=prueba-andamio; t="$W/falla/traza.txt"; b="$d/rocola_os_bitacora.txt"
  si "falla: sale con 0 (la rocola siguio)" test "$(cat $W/falla/salio)" = 0
  si "falla: el latido que lanza, en la bitacora" contiene "$b" "[prueba-andamio] FALLO en latido: InvalidOperationException: fallo de prueba en el latido"
  si "falla: el hilo que lanza, en la bitacora" contiene "$b" "[prueba-andamio] FALLO en hilo: InvalidOperationException: fallo de prueba en el hilo"
  si "falla: los dos, con su pila, en rocola_error.txt (como LogError de XP)" bash -c "grep -qF '[Rocola OS: prueba-andamio: latido]' '$d/rocola_error.txt' && grep -qF '[Rocola OS: prueba-andamio: hilo]' '$d/rocola_error.txt' && grep -qF 'fallo de prueba en el latido' '$d/rocola_error.txt'"
  n=$(latidos "$t")
  si "falla: el latido siguio despues del fallo ($n latidos)" entre "$n" 6 11
  si "falla: y los latidos de la funcion tambien (vivo.txt pasa de 2)" bash -c "[ \$(sed -n 's/^prueba-andamio=latidos //p' '$W/falla/run/rocola-os-prueba-vivo.txt') -gt 2 ]"
  # el plazo comun del cierre: un ParaAlCerrar de 8 s no detiene el cierre mas de 4 s
  d=$(datos plazo); printf 'prueba-andamio.cierre_ms=8000\n' > "$d/rocola_os.txt"
  abre plazo "$d" "7:67" 10 TERM ROCOLA_OS=prueba-andamio; b="$d/rocola_os_bitacora.txt"
  tardo=$(cat $W/plazo/tardo)
  si "plazo: sale con 0" test "$(cat $W/plazo/salio)" = 0
  si "plazo: la bitacora dice que no acabo en el plazo (4000 ms) y sigue de fondo" contiene "$b" "prueba-andamio (su hilo) NO ACABO en 4000 ms (sigue de fondo)"
  si "plazo: se cerro entre 4 y 7 s despues de SIGTERM ($tardo s)" bash -c "echo '$tardo >= 4 && $tardo < 7' | bc | grep -q 1"
  si "plazo: XP cerro igual (rocola_estado.txt borrado)" nohay "$d/rocola_estado.txt"
  # el apagon: no corre nada
  d=$(datos kill9); abre kill9 "$d" "7:67" 10 KILL ROCOLA_OS=prueba-andamio; b="$d/rocola_os_bitacora.txt"
  si "kill9: ningun cierre en la bitacora (con un apagon no corre nada)" nocontiene "$b" "cierre ("
  si "kill9: el estado sobrevive (XP)" hay "$d/rocola_estado.txt"
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; fi
[ $mal = 0 ]
