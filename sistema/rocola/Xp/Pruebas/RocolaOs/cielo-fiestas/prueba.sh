#!/bin/bash
# LA PRUEBA DE LAS FIESTAS DEL CIELO VIVO (fase 11, grupo cielo-fiestas: RocolaOs/RocolaOs.CieloFiesta*.cs; FASE11_cielo-fiestas.md; PLAN_CIELO_VIVO.md §7).
# La rocola entera fuera de pantalla (1024x768, llvmpipe), sin sonido de verdad (la tarjeta de prueba con reloj), sin red (el Open-Meteo y el lugar
# por internet apuntan a un puerto donde no escucha nadie), con datos INVENTADOS (datos.sh: 16 artistas x 2 discos de tonos con portadas de colores)
# ya calientes (una primera corrida les arma la biblioteca y las miniaturas). Zona de la hora: America/Mexico_City.
#   bash prueba.sh PROGRAMA [partes...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT; bin/Release/net10.0)
#   BASE:     el programa de la rama principal (JIT), para «apagado» (de fabrica $W/base)
#   W:        la carpeta de trabajo (de fabrica /tmp/rocola-cielo-fiestas; se rehace salvo los datos); CAPTURAS (de fabrica $W/capturas)
#   PY:       donde estan brotli, numpy y pillow (pip install --target PY brotli numpy pillow; de fabrica $W/py)
# partes (de fabrica todas): ganchos apagado escenas reposo fuegos cumple fugaz contraste
#   ganchos   ganchos.py TODO BIEN, g340 y g341 en el manifiesto y diff-xp.py --sin-ganchos igual que la base
#   apagado   APAGADO = XP: la cinta de la tarjeta (cielo/cinta.py) de la pared en el 25-12 (Navidad), sin ROCOLA_OS, con la funcion «fiestas»
#             apagada y con ella encendida y sus seis claves en 0, igual a la de la base; y el control (todas encendidas): distinta
#   escenas   por cada fecha del calendario (PLAN §6, 17 corridas), de noche (21:00): la pared a los 24 s y el escaparate a los 58 s
#   reposo    el reposo con musica (la franja AHORA SUENA) con cada fecha del calendario, por «Ver como se veria» (/run/rocola-cielo-ver)
#   fuegos    los fuegos del Grito (23:00 del 15-9) y de Ano Nuevo (con su cuenta de 10 s); el tope de 3 destellos por segundo contado en el
#             registro de nacimientos (ROCOLA_FUEGOS_LOG), y con la calma al 35 % (ROCOLA_FUEGOS_CALMA) menos de la mitad
#   cumple    rocola_fechas.txt con un cumpleanos, un aniversario, una fiesta propia con colores y un «quitar»: el mensaje en el escaparate, los
#             globos y el confeti
#   fugaz     la fugaz de festejo (una moneda) con los colores de la fecha
#   contraste la regla 3 (hasta 0,17 de luminancia) medida en las capturas de DIA (13:00) de la pared, el escaparate y el reposo, en cada fecha
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-ganchos apagado escenas reposo fuegos cumple fugaz contraste}"
W=${W:-/tmp/rocola-cielo-fiestas}; CAPTURAS=${CAPTURAS:-$W/capturas}; PY=${PY:-$W/py}; BASE=${BASE:-$W/base}
LETRAS=${LETRAS:-/root/rocola-trabajo/port/dibujo/letras-xp-falsas}
mkdir -p "$W" "$CAPTURAS"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
rx() { if [ "${1#aot:}" != "$1" ]; then echo "$(cd "${1#aot:}" && pwd)/rocola-xp"; else echo "dotnet $(cd "$1" && pwd)/rocola-xp.dll"; fi; }
AL=$W/alsa; mkdir -p "$AL"
[ -f "$AL/libasound_module_pcm_reloj.so" ] || gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
CIUDAD="19.43,-99.13,Ciudad de México"; SINRED="http://127.0.0.1:9"

# ---- los datos: la musica inventada y la biblioteca ya armada (calientes)
DATOS=$W/datos-calientes
if [ ! -f "$DATOS/rocola_biblioteca.txt" ]; then
  echo "== datos (una vez)"
  bash "$G/datos.sh" "$W/datos-frios" > "$W/datos.txt" 2>&1 || { cat "$W/datos.txt"; exit 1; }
  mkdir -p "$W/calienta/run"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj }\n' "$AL/libasound_module_pcm_reloj.so" > "$W/calienta.conf"
  ( cd "$W/datos-frios" && env -u ROCOLA_OS ROCOLA_DATOS="$W/datos-frios" ROCOLA_RUN="$W/calienta/run" ROCOLA_ALSA=reloj ROCOLA_PANTALLA=fuera:1024x768 TZ=America/Mexico_City \
      ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$W/calienta.conf ROCOLA_LETRAS_XP="$LETRAS" timeout -s TERM 150 $(rx "$prog") ) > "$W/calienta/salida.txt" 2>&1
  rm -rf "$DATOS"; mkdir -p "$DATOS"; ( cd "$W/datos-frios" && tar cf - --exclude=./musica . ) | ( cd "$DATOS" && tar xf - )
  rm -f "$DATOS/rocola_estado.txt" "$DATOS/rocola_os.txt"
fi
MUSICA=$W/datos-frios/musica

# corre NOMBRE PROGRAMA SEGUNDOS [VAR=valor...]: la rocola con una copia de los datos en $W/NOMBRE/datos (con $OSTXT en su rocola_os.txt y $FECHAS en
# su rocola_fechas.txt), /run en $W/NOMBRE/run; sale a los SEGUNDOS con SIGTERM. En segundo plano: deja su PID en $W/NOMBRE/pid.
corre() {
  local n="$1" p="$2" seg="$3"; shift 3
  local o="$W/$n"; rm -rf "$o"; mkdir -p "$o/run" "$o/datos"
  ( cd "$DATOS" && tar cf - . ) | ( cd "$o/datos" && tar xf - ); rm -f "$o/datos/rocola_estado.txt"
  printf 'dir=%s\r\n' "$MUSICA" >> "$o/datos/rocola_config.txt"
  if printf '%s\n' "$@" | grep -qx 'ROCOLA_OS=1'; then printf '%b' "${OSTXT-fiestas=1\n}" > "$o/datos/rocola_os.txt"; fi
  [ -n "${FECHAS:-}" ] && printf '%b' "$FECHAS" > "$o/datos/rocola_fechas.txt"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj }\n' "$AL/libasound_module_pcm_reloj.so" > "$o/alsa.conf"
  ( cd "$o/datos" && exec env -u ROCOLA_OS -u ROCOLA_CIELO_FECHA -u ROCOLA_CLIMA -u ROCOLA_CIUDAD ROCOLA_DATOS="$o/datos" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj \
      ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_LETRAS_XP="$LETRAS" TZ=America/Mexico_City \
      ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED "$@" timeout -s TERM "$seg" $(rx "$p") ) > "$o/salida.txt" 2>&1 &
  echo $! > "$o/pid"
}
espera() { for n in "$@"; do wait "$(cat "$W/$n/pid")" 2>/dev/null; done; }
bit() { echo "$W/$1/datos/rocola_os_bitacora.txt"; }
# hasta N corridas a la vez
lote() { while [ "$(jobs -rp | wc -l)" -ge "${1:-3}" ]; do sleep 2; done; }

# el calendario de §6: clave:fecha (la hora se pone aparte)
FIESTAS="anio_nuevo:2027-01-01 reyes:2027-01-06 candelaria:2027-02-02 amor:2027-02-14 bandera:2027-02-24 mujer:2027-03-08 nino:2027-04-30 madres:2027-05-10
padre:2027-06-20 independencia:2026-09-16 muertos:2026-11-01 revolucion:2026-11-20 santa_cecilia:2026-11-22 guadalupana:2026-12-12 posadas:2026-12-18 navidad:2026-12-25"
FIESTAS=$(echo $FIESTAS)
nfiestas=$(echo $FIESTAS | wc -w)

# ---------------------------------------------------------------- ganchos
if [[ " $partes " == *" ganchos "* ]]; then
  echo "== ganchos"
  python3 "$XP/Pruebas/RocolaOs/ganchos.py" > "$W/ganchos.txt" 2>&1; r=$?
  si "ganchos.py: TODO BIEN ($(grep -o '[0-9]* archivos comparados con [0-9a-f]* ([0-9]* con ganchos)' "$W/ganchos.txt"))" test $r = 0 -a "$(tail -1 "$W/ganchos.txt")" = "TODO BIEN"
  si "los dos ganchos de las fiestas (g340 y g341) en el manifiesto" test "$(grep -cP '^g34[01]\t' "$XP/RocolaOs/GANCHOS.tsv")" = 2
  si "ningun gancho fuera de mi rango (g340-g359) en D3D9Star.cs a nombre de las fiestas" bash -c "! grep -P '^g(3[6-9]|[4-9])[0-9]*\\t' '$XP/RocolaOs/GANCHOS.tsv' | grep -q CieloFiesta"
fi

# ---------------------------------------------------------------- apagado = XP
cinta() { local n="$1" p="$2"; shift 2; corre "$n" "$p" 14 ROCOLA_CINTA="$W/$n.cinta" ROCOLA_CINTA_CUADROS=10-11.5 "$@"; }
if [[ " $partes " == *" apagado "* ]]; then
  echo "== apagado"
  F="2026-12-25 21:00"
  mkdir -p "$W/py-ok"
  OSTXT='' cinta base "$BASE"; OSTXT='' cinta mio-sin "$prog"; espera base mio-sin
  OSTXT='fiestas=0\ncielo=0\n' cinta mio-fiestas0 "$prog" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$F"
  OSTXT='fiestas=1\ncielo=0\naurora_fiesta=0\nhorizonte_fiesta=0\nfugaz_fiesta=0\nfuegos=0\nescaparate_fiesta=0\nfechas_propias=0\n' cinta mio-claves0 "$prog" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$F"
  espera mio-fiestas0 mio-claves0
  OSTXT='fiestas=1\ncielo=0\n' cinta control "$prog" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$F"
  espera control
  for v in mio-sin mio-fiestas0 mio-claves0; do
    PYTHONPATH="$PY" python3 "$XP/Pruebas/RocolaOs/cielo/cinta.py" compara "$W/base.cinta" "$W/$v.cinta" > "$W/$v.compara" 2>&1; r=$?
    sed 's/^/   /' "$W/$v.compara"
    si "APAGADO = XP ($v): la cinta de la pared igual a la de la rama principal" test $r = 0
  done
  PYTHONPATH="$PY" python3 "$XP/Pruebas/RocolaOs/cielo/cinta.py" compara "$W/base.cinta" "$W/control.cinta" > "$W/control.compara" 2>&1; r=$?
  sed 's/^/   /' "$W/control.compara"
  si "el control (las fiestas encendidas el 25-12): la cinta SI cambia (el comparador ve los adornos y la aurora)" test $r = 1
  si "sin ROCOLA_OS no se escribe nada de Rocola OS" bash -c "[ ! -e '$W/mio-sin/datos/rocola_os.txt' ] && [ ! -e '$W/mio-sin/datos/rocola_os_bitacora.txt' ]"
fi

# ---------------------------------------------------------------- escenas: la pared y el escaparate de cada fecha
HORA_ESC=${HORA_ESC:-21:00}
escena() {   # NOMBRE FECHA HORA: una corrida con la pared a los 24 s y el escaparate a los 58 s
  local n="$1" f="$2" h="$3"
  corre "$n" "$prog" 70 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$f $h" ROCOLA_CAPTURA="24:$CAPTURAS/pared-$n.png,58:$CAPTURAS/escaparate-$n.png" ROCOLA_CAPTURA_SALE=1
}
if [[ " $partes " == *" escenas "* ]]; then
  echo "== escenas ($HORA_ESC)"
  for x in $FIESTAS; do lote 3; escena "${x%%:*}-noche" "${x#*:}" "$HORA_ESC"; sleep 1; done
  wait
  for x in $FIESTAS; do
    k=${x%%:*}
    ok=1; [ -f "$CAPTURAS/pared-$k-noche.png" ] && [ -f "$CAPTURAS/escaparate-$k-noche.png" ] || ok=0
    si "$k: la pared y el escaparate capturados, y la bitacora dice la fiesta de hoy" bash -c "[ $ok = 1 ] && grep -q 'lo de hoy ($(date -d ${x#*:} +%F)): .*$k' '$(bit $k-noche)'"
  done
  # los mensajes de la fecha entre los del escaparate
  si "independencia: el escaparate arma sus mensajes con el de la fecha (5 + 1)" contiene "$(bit independencia-noche)" "escaparate: 5 mensajes (los de la fecha: 1"
fi

# ---------------------------------------------------------------- reposo con musica: cada fecha, por «Ver como se veria»
if [[ " $partes " == *" reposo "* ]]; then
  echo "== reposo"
  t0=${T0_REPOSO:-110}; paso=6
  fin=$(( t0 + nfiestas * paso + 12 ))
  caps=""; k=0
  for x in $FIESTAS; do caps="$caps,$(echo "$t0 + $k * $paso + 4" | bc):$CAPTURAS/reposo-${x%%:*}-noche.png"; k=$((k + 1)); done
  corre reposo "$prog" $fin ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 21:00" ROCOLA_PULSA="20:45,21:45,32:13,38:13,44:27" ROCOLA_CAPTURA="${caps#,}" ROCOLA_CAPTURA_SALE=1
  ( sleep "$t0"; for x in $FIESTAS; do echo "${x#*:} 21:00" > "$W/reposo/run/rocola-cielo-ver"; sleep $paso; done ) &
  espera reposo
  nc=0; for x in $FIESTAS; do [ -f "$CAPTURAS/reposo-${x%%:*}-noche.png" ] && nc=$((nc + 1)); done
  si "reposo: las $nfiestas capturas ($nc) y «ver como se veria» $nfiestas veces en la bitacora" test $nc = $nfiestas -a "$(grep -c 'ver como se veria: 20' "$(bit reposo)")" = $nfiestas
fi

# ---------------------------------------------------------------- fuegos
cuenta_destellos() {   # REGISTRO: cuantos nacimientos y el mayor numero en una ventana de 1 s
  python3 - "$1" <<'PY'
import sys
t = sorted(float(l.split()[0]) for l in open(sys.argv[1]) if l.strip())
m = 0
for i, a in enumerate(t):
    m = max(m, sum(1 for b in t[i:] if b - a < 1.0))
print(len(t), m, sum(1 for b in t if b - t[0] < 14.0))
PY
}
if [[ " $partes " == *" fuegos "* ]]; then
  echo "== fuegos"
  # el Grito: a los 40 s de arrancar empieza la atraccion; son las 23:00:10 (los fuegos van desde las 23:00)
  rm -f "$W"/*.fuegos
  corre grito "$prog" 75 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-09-15 22:59:30" ROCOLA_FUEGOS_LOG="$W/grito.fuegos" ROCOLA_CAPTURA="44:$CAPTURAS/fuegos-grito-1.png,47:$CAPTURAS/fuegos-grito-2.png,51:$CAPTURAS/fuegos-grito-3.png,55:$CAPTURAS/fuegos-grito-4.png" ROCOLA_CAPTURA_SALE=1
  # Ano Nuevo: la cuenta de 10 s son los segundos 70 a 80 de la corrida (23:59:50 a 00:00:00); despues, los fuegos
  corre nuevo "$prog" 105 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-12-31 23:58:40" ROCOLA_FUEGOS_LOG="$W/nuevo.fuegos" ROCOLA_CAPTURA="72:$CAPTURAS/fuegos-nuevo-cuenta-1.png,76:$CAPTURAS/fuegos-nuevo-cuenta-2.png,84:$CAPTURAS/fuegos-nuevo-1.png,88:$CAPTURAS/fuegos-nuevo-2.png,92:$CAPTURAS/fuegos-nuevo-3.png" ROCOLA_CAPTURA_SALE=1
  espera grito nuevo   # (de dos en dos: con mas corridas a la vez los cuadros se espacian y los fuegos nacen menos)
  # con la calma al 35 %
  corre grito-calma "$prog" 75 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-09-15 22:59:30" ROCOLA_FUEGOS_LOG="$W/grito-calma.fuegos" ROCOLA_FUEGOS_CALMA=0.35
  # el mismo Grito pero con la clave «fuegos» apagada: ninguno
  OSTXT='fiestas=1\nfuegos=0\n' corre grito-sin "$prog" 60 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-09-15 22:59:30" ROCOLA_FUEGOS_LOG="$W/grito-sin.fuegos"
  # y fuera de la hora: el 15-9 a las 20:00 no hay fuegos
  corre grito-temprano "$prog" 60 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-09-15 20:00" ROCOLA_FUEGOS_LOG="$W/grito-temprano.fuegos"
  espera grito-calma grito-sin grito-temprano
  for n in grito nuevo; do read -r total maxs _ < <(cuenta_destellos "$W/$n.fuegos" 2>/dev/null || echo "0 0 0"); echo "   $n: $total destellos, como mucho $maxs en un segundo"
    si "$n: hay fuegos y como mucho 3 destellos por segundo ($total, $maxs)" test "$total" -ge 8 -a "$maxs" -le 3; done
  read -r _ mc tc < <(cuenta_destellos "$W/grito-calma.fuegos" 2>/dev/null || echo "0 0 0"); read -r _ mn tn < <(cuenta_destellos "$W/grito.fuegos" 2>/dev/null || echo "0 0 0")
  si "con la calma en 0,35: la mitad o menos de destellos en los primeros 14 s ($tc frente a $tn) y como mucho 3 por segundo ($mc)" test "$tc" -ge 1 -a $((tc * 2)) -le "$tn" -a "$mc" -le 3
  si "con «fuegos=0» no nace ninguno" test ! -s "$W/grito-sin.fuegos"
  si "el 15-9 a las 20:00 no nace ninguno" test ! -s "$W/grito-temprano.fuegos"
  si "la bitacora dice como empiezan los de Ano Nuevo con su cuenta de 10 s" contiene "$(bit nuevo)" "fuegos artificiales: empiezan 2027-01-01 00:00 con una cuenta de 10 s"
fi

# ---------------------------------------------------------------- cumple
if [[ " $partes " == *" cumple "* ]]; then
  echo "== cumple"
  FECHAS='# fechas de la prueba\n16-03;cumple;Juan\n16-03;aniversario;El Billar\n10-04;fiesta;Mi fiesta|#ff0000,#ffffff\n24-12;quitar;navidad\n05-05;cumple;Ana\n05-05;mensaje;¡Hoy se come mole!\n' \
    corre cumple "$prog" 70 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-03-16 21:00" ROCOLA_CAPTURA="24:$CAPTURAS/cumple-pared.png,50:$CAPTURAS/cumple-escaparate-1.png,56:$CAPTURAS/cumple-escaparate-2.png,62:$CAPTURAS/cumple-escaparate-3.png" ROCOLA_CAPTURA_SALE=1
  FECHAS='10-04;fiesta;Mi fiesta|#ff0000,#ffffff\n24-12;quitar;navidad\n05-05;mensaje;¡Hoy se come mole!\n' \
    corre propia "$prog" 70 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-04-10 21:00" ROCOLA_CAPTURA="24:$CAPTURAS/propia-pared.png,58:$CAPTURAS/propia-escaparate.png" ROCOLA_CAPTURA_SALE=1
  FECHAS='24-12;quitar;navidad\n' corre quitar "$prog" 40 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-12-24 21:00"
  FECHAS='05-05;mensaje;¡Hoy se come mole!\n' corre mensaje "$prog" 70 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-05-05 21:00"
  espera cumple propia quitar mensaje
  si "cumpleanos y aniversario de hoy: la bitacora los lee (cumple Juan, aniversario El Billar) y arma 7 mensajes (4 de XP + 1 de XP... + 2 propios)" \
     bash -c "grep -q 'lo de hoy (2026-03-16): cumple Juan, aniversario El Billar' '$(bit cumple)' && grep -q 'escaparate: [0-9]* mensajes (los de la fecha: 0, propios: 2)' '$(bit cumple)'"
  si "una fiesta propia («fiesta;Mi fiesta|#ff0000,#ffffff»): la bitacora la dice (propia)" contiene "$(bit propia)" "lo de hoy (2026-04-10): propia"
  si "«24-12;quitar;navidad»: el 24-12 queda solo posadas (sin Navidad)" bash -c "grep -q 'lo de hoy (2026-12-24): posadas\$' '$(bit quitar)'"
  si "el mensaje de rocola_fechas.txt (el 5-5) entra entre los del escaparate" contiene "$(bit mensaje)" "(los de la fecha: 1"
fi

# ---------------------------------------------------------------- fugaz
if [[ " $partes " == *" fugaz "* ]]; then
  echo "== fugaz"
  # una moneda a los 20 s (la fugaz de festejo) y tres capturas a su paso
  for x in independencia:2026-09-16 navidad:2026-12-25; do
    k=${x%%:*}
    lote 3
    corre fugaz-$k "$prog" 40 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="${x#*:} 21:00" ROCOLA_PULSA="20:45" ROCOLA_CAPTURA="20.25:$CAPTURAS/fugaz-$k-1.png,20.5:$CAPTURAS/fugaz-$k-2.png,20.8:$CAPTURAS/fugaz-$k-3.png,21.2:$CAPTURAS/fugaz-$k-4.png" ROCOLA_CAPTURA_SALE=1
  done
  wait
  nc=0; for k in independencia navidad; do for i in 1 2 3 4; do [ -f "$CAPTURAS/fugaz-$k-$i.png" ] && nc=$((nc + 1)); done; done
  si "fugaz: las 8 capturas de la moneda ($nc); mirarlas: la estela lleva los colores de la fecha" test $nc = 8
fi

# ---------------------------------------------------------------- contraste (regla 3)
contraste() { PYTHONPATH="$PY" python3 "$XP/Pruebas/RocolaOs/cielo/contraste.py" "$@"; }
if [[ " $partes " == *" contraste "* ]]; then
  echo "== contraste (de dia, 13:00)"
  for x in $FIESTAS; do lote 3; escena "${x%%:*}-dia" "${x#*:}" 13:00; sleep 1; done
  wait
  for x in $FIESTAS; do
    k=${x%%:*}
    f=$CAPTURAS/pared-$k-dia.png
    if [ -f "$f" ]; then
      contraste "$f" 0,0,1024,100 0,90,260,290 760,100,960,270 > "$W/contraste-pared-$k.txt" 2>&1
      si "regla 3 en la pared de $k a las 13:00: $(cat "$W/contraste-pared-$k.txt")" contiene "$W/contraste-pared-$k.txt" "regla 3 (<= 0,17): BIEN"
    else si "la captura de la pared de $k de dia" false; fi
    f=$CAPTURAS/escaparate-$k-dia.png
    if [ -f "$f" ]; then
      contraste "$f" --solo 560,560,1000,650 > "$W/contraste-escaparate-$k.txt" 2>&1
      si "regla 3 en el escaparate de $k a las 13:00: $(cat "$W/contraste-escaparate-$k.txt")" contiene "$W/contraste-escaparate-$k.txt" "regla 3 (<= 0,17): BIEN"
    else si "la captura del escaparate de $k de dia" false; fi
  done
  # el reposo, de dia: la misma corrida del reposo con las fechas a las 13:00
  T0_REPOSO=${T0_REPOSO:-110}; paso=6; t0=$T0_REPOSO
  caps=""; k=0; for x in $FIESTAS; do caps="$caps,$(echo "$t0 + $k * $paso + 4" | bc):$CAPTURAS/reposo-${x%%:*}-dia.png"; k=$((k + 1)); done
  corre reposo-dia "$prog" $(( t0 + nfiestas * paso + 12 )) ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 13:00" ROCOLA_PULSA="20:45,21:45,32:13,38:13,44:27" ROCOLA_CAPTURA="${caps#,}" ROCOLA_CAPTURA_SALE=1
  ( sleep "$t0"; for x in $FIESTAS; do echo "${x#*:} 13:00" > "$W/reposo-dia/run/rocola-cielo-ver"; sleep $paso; done ) &
  espera reposo-dia
  for x in $FIESTAS; do
    k=${x%%:*}; f=$CAPTURAS/reposo-$k-dia.png
    if [ -f "$f" ]; then
      contraste "$f" --hasta 0.668 --solo 0,100,1024,513 > "$W/contraste-reposo-$k.txt" 2>&1
      si "regla 3 en el reposo de $k a las 13:00: $(cat "$W/contraste-reposo-$k.txt")" contiene "$W/contraste-reposo-$k.txt" "regla 3 (<= 0,17): BIEN"
    else si "la captura del reposo de $k de dia" false; fi
  done
fi

echo
echo "$( [ $mal = 0 ] && echo "TODO BIEN ($bien comprobaciones)" || echo "$mal MAL de $((bien + mal))" )"
[ $mal = 0 ]
