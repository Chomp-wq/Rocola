#!/bin/bash
# LA PRUEBA DEL NUCLEO DEL CIELO VIVO (fase 11, grupo cielo-nucleo: RocolaOs/RocolaOs.Cielo*.cs; FASE11_cielo-nucleo.md; PLAN_CIELO_VIVO.md §7).
# La rocola entera fuera de pantalla (1024x768, llvmpipe), sin sonido de verdad (la tarjeta de prueba con reloj), sin red de verdad (un Open-Meteo
# y un «lugar por internet» falsos, clima_falso.py; o ninguno), con datos INVENTADOS: los de las escenas de ventanas (DATOS0: la pared casi
# vacia, para medir el cielo) y los de las maquetas (DATOSM: 67 discos inventados con portadas, para las escenas). Zona de la hora:
# America/Mexico_City (como /etc/localtime en Rocola OS).
#   bash prueba.sh PROGRAMA [partes...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   BASE:     el programa de la rama principal (JIT), para «apagado» y «noche» (de fabrica /root/rocola-trabajo/fase11/cielo-nucleo/base-jit)
#   AOTMIO:   el programa en NativeAOT, para «reloj» (aot:CARPETA; de fabrica /root/rocola-trabajo/fase11/cielo-nucleo/aot)
#   W:        la carpeta de trabajo (de fabrica /root/rocola-trabajo/fase11/cielo-nucleo/prueba; se rehace); CAPTURAS (de fabrica $W/capturas)
#   PY:       donde estan brotli y astral (pip install --target PY brotli astral==3.2; de fabrica /root/rocola-trabajo/fase11/cielo-nucleo/py)
# partes (de fabrica todas): ganchos orden clima apagado noche calma escenas medio reloj
#   ganchos  ganchos.py TODO BIEN y diff-xp.py --sin-ganchos con las cuentas de la base (5135 / 4868 / 229)
#   orden    `rocola-xp --prueba-cielo` (las cuentas: TODO BIEN), las salidas y puestas del sol contra la NOAA (astral) a +-3 min, el calendario
#            de §6 dia por dia (2026) y el tercer domingo de junio
#   clima    el lugar y el clima con los servidores falsos (y lo que se guarda); ciudad_bar sin pedir el lugar; sin red con un clima de hace 2 h
#            (vale) y de hace 4 h (no vale: despejado)
#   apagado  APAGADO = XP: las cintas de la tarjeta (cinta.py) de la pared sin ROCOLA_OS, con ROCOLA_OS=1 y las cuatro funciones en 0, y con
#            ROCOLA_OS=1 y todas sus claves en 0: los degradados del cielo byte a byte y la forma de los cuadros, iguales a los de BASE; y el
#            control (DN1 a las 13:00): distintas. Sin ROCOLA_OS no se escribe nada de Rocola OS.
#   noche    DE NOCHE = XP: ROCOLA_CIELO_FECHA a las 23:30 de un dia sin fiesta, todo encendido y sin clima: la cinta igual a la de BASE
#   calma    la calma de verdad (ROCOLA_CIELO_CALMA): una tecla la baja a 0,35 en 0,4 s y vuelve a 1 en 3 s a los 15 s sin tocar
#   escenas  capturas a las 6:50, 9:30, 13:00, 17:40, 18:35 y 22:00 (con «Ver como se veria» por el buzon: /run/rocola-cielo-ver) de la pared
#            casi vacia (con la regla 3 medida), y del escaparate, la pared llena, el disco abierto y el reposo con musica (DATOSM)
#   medio    el cielo a media resolucion (cielo_media=1) da el mismo degradado que el directo
#   reloj    p50/p95/p99/max del trabajo de cada cuadro (ROCOLA_CIELO_RELOJ) con todo encendido y todo apagado, en NativeAOT (AOTMIO)
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-ganchos orden clima apagado noche calma escenas medio reloj}"
T0=/root/rocola-trabajo/fase11/cielo-nucleo
W=${W:-$T0/prueba}
BASE=${BASE:-$T0/base-jit}
AOTMIO=${AOTMIO:-aot:$T0/aot}
CAPTURAS=${CAPTURAS:-$W/capturas}
PY=${PY:-$T0/py}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
DATOSM=${DATOSM:-/root/rocola-trabajo/maquetas/datos-base}
MUSICAM=${MUSICAM:-/root/rocola-trabajo/maquetas/musica}   # (la musica de DATOSM: no se copia, se le dice a la rocola con «dir=»)
LETRAS=${LETRAS:-/root/rocola-trabajo/port/dibujo/letras-xp-falsas}
rm -rf "$W"; mkdir -p "$W" "$CAPTURAS"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
rx() { if [ "${1#aot:}" != "$1" ]; then echo "$(cd "${1#aot:}" && pwd)/rocola-xp"; else echo "dotnet $(cd "$1" && pwd)/rocola-xp.dll"; fi; }
AL=$T0/alsa; mkdir -p "$AL"
[ -f "$AL/libasound_module_pcm_reloj.so" ] || gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
CIUDAD="19.43,-99.13,Ciudad de México"   # (ROCOLA_CIUDAD: sin pedir el lugar por internet)
SINRED="http://127.0.0.1:9"              # (ROCOLA_CLIMA_API sin nadie detras: no hay clima)

# copia DATOS DESTINO: una copia nueva de los datos (sin el estado de la rocola); los de las maquetas, con su carpeta de musica («dir=»)
copia() { rm -rf "$2"; cp -r "$1" "$2"; rm -f "$2/rocola_estado.txt"; [ "$1" = "$DATOSM" ] && printf 'dir=%s\r\n' "$MUSICAM" >> "$2/rocola_config.txt"; return 0; }
# corre NOMBRE PROGRAMA SEGUNDOS DATOS [VAR=valor...]: la rocola con una copia nueva de DATOS en $W/NOMBRE/datos (o tal cual si DATOS empieza
# por «=»), /run en $W/NOMBRE/run; sale a los SEGUNDOS con SIGTERM. En segundo plano: deja su PID en $W/NOMBRE/pid.
corre() {
  local n="$1" p="$2" seg="$3" d="$4"; shift 4
  local o="$W/$n"; mkdir -p "$o/run"
  if [ "${d#=}" != "$d" ]; then d="${d#=}"; else copia "$d" "$o/datos"; d="$o/datos"; fi
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj }\n' "$AL/libasound_module_pcm_reloj.so" > "$o/alsa.conf"
  ( cd "$d" && exec env -u ROCOLA_OS -u ROCOLA_CIELO_FECHA -u ROCOLA_CLIMA -u ROCOLA_CIUDAD ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj \
      ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_LETRAS_XP="$LETRAS" TZ=America/Mexico_City \
      "$@" timeout -s TERM "$seg" $(rx "$p") ) > "$o/salida.txt" 2>&1 &
  echo $! > "$o/pid"
}
espera() { for n in "$@"; do wait "$(cat "$W/$n/pid")" 2>/dev/null; done; }
bit() { echo "$W/$1/datos/rocola_os_bitacora.txt"; }

# ---------------------------------------------------------------- ganchos
if [[ " $partes " == *" ganchos "* ]]; then
  echo "== ganchos"
  python3 "$XP/Pruebas/RocolaOs/ganchos.py" > "$W/ganchos.txt" 2>&1; r=$?
  si "ganchos.py: TODO BIEN ($(grep -o '[0-9]* archivos comparados con [0-9a-f]* ([0-9]* con ganchos)' "$W/ganchos.txt"))" test $r = 0 -a "$(tail -1 "$W/ganchos.txt")" = "TODO BIEN"
  si "ganchos.py: los 20 del cielo (g120-g139) en el manifiesto" test "$(grep -cP '^g1[23][0-9]\t' "$XP/RocolaOs/GANCHOS.tsv")" = 20
  (cd "$XP" && python3 Pruebas/Tarjeta/guiones/diff-xp.py --sin-ganchos $(ls Codigo)) > "$W/diff-xp.txt" 2>&1
  si "diff-xp.py --sin-ganchos: $(grep '^TOTAL' "$W/diff-xp.txt") (la base: 5135 / 4868 / 229)" contiene "$W/diff-xp.txt" "TOTAL: 5135 lineas quitadas, 4868 puestas (229 sin marca)"
fi

# ---------------------------------------------------------------- orden
if [[ " $partes " == *" orden "* ]]; then
  echo "== orden"
  mkdir -p "$W/orden"
  ( cd "$W/orden" && TZ=America/Mexico_City ROCOLA_PANTALLA=fuera:320x240 ROCOLA_DATOS="$W/orden" timeout 120 $(rx "$prog") --prueba-cielo ) > "$W/orden/salida.txt" 2>&1; r=$?
  grep -E '^(BIEN|MAL) ' "$W/orden/salida.txt" | sed 's/^/   /'
  si "--prueba-cielo: $(tail -1 "$W/orden/salida.txt")" test $r = 0
  PYTHONPATH="$PY" python3 "$G/sol_y_calendario.py" "$W/orden/salida.txt" > "$W/orden/comprueba.txt" 2>&1; r=$?
  sed 's/^/   /' "$W/orden/comprueba.txt"
  si "las salidas y puestas del sol contra la NOAA (+-3 min) y el calendario de §6" test $r = 0
fi

# ---------------------------------------------------------------- clima
if [[ " $partes " == *" clima "* ]]; then
  echo "== clima"
  P=$((21000 + $$ % 20000))
  falso() { local n="$1"; shift; mkdir -p "$W/$n"; : > "$W/$n/peticiones.txt"; rm -f "$W/$n/listo"
    python3 "$G/clima_falso.py" $P --registro "$W/$n/peticiones.txt" --listo "$W/$n/listo" "$@" > "$W/$n/falso.txt" 2>&1 & FALSO=$!
    for i in $(seq 1 50); do [ -f "$W/$n/listo" ] && break; sleep 0.1; done; }
  # A: con red (falsa): ciudad_bar=0, sin ROCOLA_CIUDAD: el lugar por internet (Monterrey) y su clima (lluvia, 63)
  falso clima-red --codigo 63 --nubes 90
  corre clima-red "$prog" 20 "$DATOS0" ROCOLA_OS=1 ROCOLA_CLIMA_YA=1 ROCOLA_CLIMA_API=http://127.0.0.1:$P ROCOLA_LUGAR_API=http://127.0.0.1:$P/lugar; espera clima-red
  kill $FALSO 2>/dev/null; wait $FALSO 2>/dev/null
  o=$W/clima-red; b=$(bit clima-red)
  si "con red: se pidio el lugar una vez (/lugar)" test "$(grep -c '^/lugar' "$o/peticiones.txt")" = 1
  si "con red: el clima se pidio para el lugar de internet (latitude=25.69&longitude=-100.32) con lo de §3" \
     contiene "$o/peticiones.txt" "/v1/forecast?latitude=25.69&longitude=-100.32&current=weather_code,cloud_cover,precipitation,rain,showers,snowfall,wind_speed_10m,wind_direction_10m,temperature_2m,visibility,is_day"
  si "con red: rocola_clima_lugar.txt (ciudad=Monterrey)" contiene "$o/datos/rocola_clima_lugar.txt" "ciudad=Monterrey"
  si "con red: rocola_clima.txt (codigo=63, nubes=90, lat=25.69)" bash -c "grep -q '^codigo=63$' '$o/datos/rocola_clima.txt' && grep -q '^nubes=90$' '$o/datos/rocola_clima.txt' && grep -q '^lat=25.69$' '$o/datos/rocola_clima.txt'"
  si "con red: la bitacora dice el lugar y el clima («el lugar por internet (127.0.0.1): Monterrey», «el clima del cielo: lluvia (de afuera»)" \
     bash -c "grep -q 'el lugar por internet (127.0.0.1): Monterrey' '$b' && grep -q 'el clima del cielo: lluvia (de afuera' '$b'"
  si "con red: el sol, del lugar de internet («el lugar del bar: Monterrey (por internet) (25.69, -100.32)»)" contiene "$b" "el lugar del bar: Monterrey (por internet) (25.69, -100.32)"
  # B: ciudad_bar=23 (Monterrey) y el Open-Meteo que falla (500): no se pide el lugar; el clima, con las coordenadas de la lista
  falso clima-ciudad --falla
  mkdir -p "$W/clima-ciudad"; copia "$DATOS0" "$W/clima-ciudad/datos"; printf 'ciudad_bar=23\n' > "$W/clima-ciudad/datos/rocola_os.txt"
  corre clima-ciudad "$prog" 16 "=$W/clima-ciudad/datos" ROCOLA_OS=1 ROCOLA_CLIMA_YA=1 ROCOLA_CLIMA_API=http://127.0.0.1:$P ROCOLA_LUGAR_API=http://127.0.0.1:$P/lugar; espera clima-ciudad
  kill $FALSO 2>/dev/null; wait $FALSO 2>/dev/null
  o=$W/clima-ciudad; b=$(bit clima-ciudad)
  si "ciudad_bar=23: no se pide el lugar por internet" test "$(grep -c '^/lugar' "$o/peticiones.txt")" = 0
  si "ciudad_bar=23: el clima se pide para Monterrey de la lista (latitude=25.69&longitude=-100.32)" contiene "$o/peticiones.txt" "latitude=25.69&longitude=-100.32"
  si "ciudad_bar=23: el clima falla (500) y se dice («no se pudo pedir el clima: contesto 500»); el cielo, sin saber (despejado)" \
     bash -c "grep -q 'no se pudo pedir el clima: contesto 500' '$b' && grep -q 'el clima del cielo: sin saber (despejado)' '$b' && grep -q 'lugar Monterrey' '$b'"
  # C y D: sin red, con un clima guardado de hace 2 h (vale) y de hace 4 h (ya no)
  for h in 2 4; do
    n=clima-$h; mkdir -p "$W/$n"; copia "$DATOS0" "$W/$n/datos"
    printf '# prueba\nmedida=%s\ncodigo=3\nnubes=95\nlluvia=0\nviento=10\ndireccion=90\ntemperatura=12\nvisibilidad=NaN\nde_dia=0\nlat=19.43\nlon=-99.13\n' \
      "$(date -u -d "-$h hours" +%Y-%m-%dT%H:%M:%SZ)" > "$W/$n/datos/rocola_clima.txt"
    corre $n "$prog" 14 "=$W/$n/datos" ROCOLA_OS=1 ROCOLA_CLIMA_YA=1 ROCOLA_CLIMA_API=$SINRED ROCOLA_CIUDAD="$CIUDAD"
  done
  espera clima-2 clima-4
  si "sin red, un clima de hace 2 h: vale («el clima guardado: nublado ... (vale)», «el clima del cielo: nublado (de afuera»)" \
     bash -c "grep -q 'el clima guardado: nublado .*(vale)' '$(bit clima-2)' && grep -q 'el clima del cielo: nublado (de afuera' '$(bit clima-2)'"
  si "sin red, un clima de hace 2 h: «no se pudo pedir el clima: sin red» y vale el ultimo" contiene "$(bit clima-2)" "no se pudo pedir el clima: sin red"
  si "sin red, un clima de hace 4 h: no vale («ya es viejo: no se usa»; «el clima del cielo: sin saber (despejado)»)" \
     bash -c "grep -q 'ya es viejo: no se usa' '$(bit clima-4)' && grep -q 'el clima del cielo: sin saber (despejado)' '$(bit clima-4)' && ! grep -q 'el clima del cielo: nublado' '$(bit clima-4)'"
  # E: ROCOLA_CLIMA forzado: sin pedir nada
  corre clima-forzado "$prog" 12 "$DATOS0" ROCOLA_OS=1 ROCOLA_CLIMA_YA=1 ROCOLA_CLIMA="tormenta,90,12,30,180,15" ROCOLA_CLIMA_API=$SINRED ROCOLA_CIUDAD="$CIUDAD"; espera clima-forzado
  si "ROCOLA_CLIMA=tormenta: el clima del cielo forzado y sin pedirlo" bash -c "grep -q 'el clima del cielo: tormenta (forzado)' '$(bit clima-forzado)' && ! grep -q 'no se pudo pedir el clima' '$(bit clima-forzado)'"
  # F: la funcion «cielo» encendida pero sin ninguna idea que use el sol (DN1-DN6 y VI5 en 0) y sin el clima, con ciudad_bar=0: no se pide el
  # lugar por internet (nadie lo usaria)
  falso clima-sinlugar --codigo 3 --nubes 50
  mkdir -p "$W/clima-sinlugar"; copia "$DATOS0" "$W/clima-sinlugar/datos"
  printf 'cielo_sol=0\nsol=0\nluna_camino=0\nestrellas_anochecer=0\nhorizonte_hora=0\nnubes=0\nvidrio_luz=0\nclima=0\nciudad_bar=0\n' > "$W/clima-sinlugar/datos/rocola_os.txt"
  corre clima-sinlugar "$prog" 16 "=$W/clima-sinlugar/datos" ROCOLA_OS=1 ROCOLA_CLIMA_YA=1 ROCOLA_CLIMA_API=http://127.0.0.1:$P ROCOLA_LUGAR_API=http://127.0.0.1:$P/lugar; espera clima-sinlugar
  kill $FALSO 2>/dev/null; wait $FALSO 2>/dev/null
  si "sin ideas que usen el sol ni el clima: el cielo arranca y no se pide el lugar ni el clima" \
     bash -c "grep -q '\\[cielo\\] arranca' '$(bit clima-sinlugar)' && ! grep -q . '$W/clima-sinlugar/peticiones.txt'"
fi

# ---------------------------------------------------------------- apagado y noche (las cintas)
cinta() {   # NOMBRE PROGRAMA [VAR=valor...]: la pared de DATOS0 sin teclas, los cuadros de los 10 a los 11,5 s
  local n="$1" p="$2"; shift 2
  corre "$n" "$p" 14 "$DATOS0" ROCOLA_CINTA="$W/$n.cinta" ROCOLA_CINTA_CUADROS=10-11.5 "$@"
}
compara_cinta() { PYTHONPATH="$PY" python3 "$G/cinta.py" compara "$1" "$2"; }
if [[ " $partes " == *" apagado "* ]]; then
  echo "== apagado"
  cinta base-sin "$BASE"; cinta mio-sin "$prog"; espera base-sin mio-sin
  mkdir -p "$W/mio-cero"; copia "$DATOS0" "$W/mio-cero/datos"
  printf 'cielo=0\nfiestas=0\nclima=0\nvidrio=0\n' > "$W/mio-cero/datos/rocola_os.txt"
  corre mio-cero "$prog" 14 "=$W/mio-cero/datos" ROCOLA_OS=1 ROCOLA_CINTA="$W/mio-cero.cinta" ROCOLA_CINTA_CUADROS=10-11.5 ROCOLA_CIELO_FECHA="2026-11-18 13:00" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED
  mkdir -p "$W/mio-claves0"; copia "$DATOS0" "$W/mio-claves0/datos"
  printf 'cielo_sol=0\nsol=0\nluna_camino=0\nestrellas_anochecer=0\nhorizonte_hora=0\nnubes=0\nciudad_bar=0\naurora_fiesta=0\nhorizonte_fiesta=0\nfugaz_fiesta=0\nfuegos=0\nescaparate_fiesta=0\nfechas_propias=0\nclima=0\nlluvia=0\ntormenta=0\nneblina=0\nclima_escaparate=0\nvidrio_ficha=0\nvidrio_franja=0\nvidrio_pastillas=0\nvidrio_lista=0\nvidrio_luz=0\nvidrio_buscador=0\n' > "$W/mio-claves0/datos/rocola_os.txt"
  corre mio-claves0 "$prog" 14 "=$W/mio-claves0/datos" ROCOLA_OS=1 ROCOLA_CINTA="$W/mio-claves0.cinta" ROCOLA_CINTA_CUADROS=10-11.5 ROCOLA_CIELO_FECHA="2026-11-18 13:00" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED
  espera mio-cero mio-claves0
  cinta control-dia "$prog" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 13:00" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED; espera control-dia
  for v in mio-sin mio-cero mio-claves0; do
    compara_cinta "$W/base-sin.cinta" "$W/$v.cinta" > "$W/$v.compara" 2>&1; r=$?
    sed 's/^/   /' "$W/$v.compara"
    si "APAGADO = XP ($v): la cinta de la pared igual a la de la rama principal" test $r = 0
  done
  compara_cinta "$W/base-sin.cinta" "$W/control-dia.cinta" > "$W/control-dia.compara" 2>&1; r=$?
  sed 's/^/   /' "$W/control-dia.compara"
  si "el control (DN1 encendida a las 13:00): la cinta SI cambia (el comparador ve el cielo)" test $r = 1
  si "sin ROCOLA_OS no se escribe nada de Rocola OS (ni rocola_os.txt ni su bitacora) y no corre el hilo del cielo" \
     bash -c "[ ! -e '$W/mio-sin/datos/rocola_os.txt' ] && [ ! -e '$W/mio-sin/datos/rocola_os_bitacora.txt' ] && [ ! -e '$W/mio-sin/datos/rocola_clima.txt' ]"
  si "con las cuatro funciones en 0: el cielo no arranca (ni su hilo ni sus archivos)" \
     bash -c "! grep -q '\\[cielo\\]' '$(bit mio-cero)' && [ ! -e '$W/mio-cero/datos/rocola_clima.txt' ]"
  si "con todas las claves en 0: arranca (la foto), pero no pinta nada (la cinta igual) y no pide el clima" \
     bash -c "grep -q '\\[cielo\\] arranca' '$(bit mio-claves0)' && ! grep -q 'no se pudo pedir el clima' '$(bit mio-claves0)'"
fi
if [[ " $partes " == *" noche "* ]]; then
  echo "== noche"
  [ -f "$W/base-sin.cinta" ] || { cinta base-sin "$BASE"; espera base-sin; }
  cinta noche "$prog" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 23:30" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED; espera noche
  compara_cinta "$W/base-sin.cinta" "$W/noche.cinta" > "$W/noche.compara" 2>&1; r=$?
  sed 's/^/   /' "$W/noche.compara"
  si "DE NOCHE = XP: a las 23:30 del 18-11 (sin fiesta), todo encendido y sin clima, la cinta igual a la de la rama principal" test $r = 0
  si "a las 23:30 la fase es la noche («fase noche» en la bitacora)" contiene "$(bit noche)" "fase noche"
fi

# ---------------------------------------------------------------- escenas
HORAS="06:50 09:30 13:00 17:40 18:35 22:00"
# barrido NOMBRE DESDE: con la rocola ya corriendo, cada 6 s una hora de HORAS por el buzon (/run/rocola-cielo-ver) y la captura 4 s despues
barrido() {
  local n="$1" t0="$2" k=0 caps=""
  for h in $HORAS; do caps="$caps,$(echo "$t0 + $k * 6 + 4" | bc):$CAPTURAS/$n-${h/:/}.png"; k=$((k + 1)); done
  echo "${caps#,}"
}
buzon() {   # NOMBRE DESDE: escribe las horas en su /run a su tiempo (en segundo plano)
  local n="$1" t0="$2" k=0
  ( sleep "$t0"; for h in $HORAS; do echo "2026-11-18 $h" > "$W/$n/run/rocola-cielo-ver"; sleep 6; done ) &
}
contraste() { python3 "$G/contraste.py" "$@"; }
if [[ " $partes " == *" escenas "* ]]; then
  echo "== escenas"
  # la pared casi vacia (DATOS0): la regla 3 medida en la captura
  n=vacia; corre $n "$prog" 52 "$DATOS0" ROCOLA_OS=1 ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CAPTURA="$(barrido $n 12)" ROCOLA_CAPTURA_SALE=1; buzon $n 12
  # el escaparate (la atraccion: 40 s sin tocar), la pared llena (la L y el cursor en la segunda), el disco abierto (sonando) y el reposo con
  # musica (50 s sin tocar con la cancion sonando): las teclas de las escenas de las maquetas (datos-base: MONEDA 45, REGRESAR 27)
  aL="6:27+39,7.3:27+39,8.6:27+39,9.9:27+39,11.2:27+39,12.5:27+39,13.8:27+39,15.1:27+39,16.4:27+39,17.7:27+39"
  pide="6:45,6.8:45,8:27+39,9.3:27+39,10.6:27+39,11.9:27+39,13.2:27+39,14.5:27+39,15.8:27+39,17.1:27+39,18.4:27+39,19.7:27+39"
  pide="$pide,21:39,21.5:39,22:39,22.5:39,23:39,23.5:39,24:39,24.5:39,25:39,25.5:39,26:39,27.5:13,29.5:13"
  n=escaparate; corre $n "$prog" 96 "$DATOSM" ROCOLA_OS=1 ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CAPTURA="$(barrido $n 54)" ROCOLA_CAPTURA_SALE=1; buzon $n 54
  espera vacia
  n=pared; corre $n "$prog" 62 "$DATOSM" ROCOLA_OS=1 ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_PULSA="$aL,19.2:39" ROCOLA_CAPTURA="$(barrido $n 22)" ROCOLA_CAPTURA_SALE=1; buzon $n 22
  espera escaparate pared
  # el disco se cierra solo si nadie toca y las teclas quitarian «ver como se veria»: una corrida por hora, con ROCOLA_CIELO_FECHA
  for h in $HORAS; do
    n=disco-${h/:/}; corre $n "$prog" 46 "$DATOSM" ROCOLA_OS=1 ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CIELO_FECHA="2026-11-18 $h" ROCOLA_PULSA="$pide" \
      ROCOLA_CAPTURA="38:$CAPTURAS/disco-${h/:/}.png" ROCOLA_CAPTURA_SALE=1
    [ $h = 09:30 ] || [ $h = 17:40 ] && espera disco-${h/:/}
  done
  n=reposo; corre $n "$prog" 124 "$DATOSM" ROCOLA_OS=1 ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_PULSA="$pide,31.5:27" ROCOLA_CAPTURA="$(barrido $n 84)" ROCOLA_CAPTURA_SALE=1; buzon $n 84
  espera disco-1835 disco-2200 reposo
  # la regla 3 en la pared casi vacia (la barra de arriba, la portada, la luna con la estrella y la barra de abajo, fuera)
  for h in $HORAS; do
    f=$CAPTURAS/vacia-${h/:/}.png
    if [ -f "$f" ]; then
      contraste "$f" 0,0,1024,100 0,90,260,290 760,100,960,270 > "$W/contraste-$h.txt" 2>&1
      si "regla 3 en la pared a las $h: $(cat "$W/contraste-$h.txt")" contiene "$W/contraste-$h.txt" "regla 3 (<= 0,17): BIEN"
    else si "la captura de la pared a las $h" false; fi
  done
  for e in escaparate pared reposo; do
    nc=0; for h in $HORAS; do [ -f "$CAPTURAS/$e-${h/:/}.png" ] && nc=$((nc + 1)); done
    si "$e: las 6 capturas ($nc) y «ver como se veria» 6 veces en la bitacora" test $nc = 6 -a "$(grep -c 'ver como se veria: 2026-11-18' "$(bit $e)")" = 6
  done
  nc=0; for h in $HORAS; do [ -f "$CAPTURAS/disco-${h/:/}.png" ] && grep -q "arranca: hora 2026-11-18 $h" "$(bit disco-${h/:/})" && nc=$((nc + 1)); done
  si "disco: las 6 capturas con ROCOLA_CIELO_FECHA a su hora ($nc)" test $nc = 6
  # en las escenas llenas solo hay cielo libre en unas zonas: el escaparate, a la derecha del cubo y bajo los textos (x 560-1000, y 560-650);
  # el disco, bajo la portada y a la izquierda de la lista (x 0-430, y 560-650); el reposo con musica, sobre la franja clara del horizonte
  # (que ahi esta en 525: hasta 0,848/0,868 de 525 = 513). La pared llena no deja cielo libre bastante (la mide la pared vacia).
  for h in $HORAS; do
    for z in "escaparate --solo 560,560,1000,650" "disco --solo 0,560,430,650"; do
      set -- $z; e=$1; shift; f=$CAPTURAS/$e-${h/:/}.png
      [ -f "$f" ] || continue
      contraste "$f" "$@" > "$W/contraste-$e-$h.txt" 2>&1
      si "regla 3 en $e a las $h: $(cat "$W/contraste-$e-$h.txt")" contiene "$W/contraste-$e-$h.txt" "regla 3 (<= 0,17): BIEN"
    done
    # el reposo con musica: el cielo sobre la raya (que ahi esta en 525; el panel AHORA SUENA va debajo), hasta 0,868 del horizonte (513).
    # Es donde el resplandor de DN5 subia la luz (0,18-0,19 de dia con 2,2 veces la luz de la raya; con 1,4 en el reposo, bajo 0,17)
    f=$CAPTURAS/reposo-${h/:/}.png
    if [ -f "$f" ]; then
      contraste "$f" --hasta 0.668 --solo 0,100,1024,513 > "$W/contraste-reposo-$h.txt" 2>&1
      si "regla 3 en el reposo con musica a las $h: $(cat "$W/contraste-reposo-$h.txt")" contiene "$W/contraste-reposo-$h.txt" "regla 3 (<= 0,17): BIEN"
    fi
  done
  si "las fases de las capturas (bitacora de la pared vacia): amanecer, manana, mediodia, atardecer, anochecer y noche" \
     bash -c "for x in amanecer manana mediodia atardecer anochecer noche; do grep -q \"fase del dia: \$x\" '$(bit vacia)' || exit 1; done"
fi

# ---------------------------------------------------------------- calma
if [[ " $partes " == *" calma "* ]]; then
  echo "== calma"
  # al arrancar XP cuenta como si se acabara de tocar (lastInput = ahora): calmado 15 s. Una tecla (ABAJO) a los 25 s: la calma baja a 0,35
  # (en 0,4 s) y, a los 15 s sin tocar, vuelve a 1 (en 3 s)
  corre calma "$prog" 48 "$DATOS0" ROCOLA_OS=1 ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_PULSA="25:40" ROCOLA_CIELO_CALMA="$W/calma/calma.txt"; espera calma
  python3 - "$W/calma/calma.txt" > "$W/calma/calma.res" 2>&1 <<'PY'
import sys
m = [l.split() for l in open(sys.argv[1])]
t = [(float(x[0]), float(x[1]), len(x) > 2) for x in m]
ini = [c for (s, c, e) in t if 21 <= s < 24.5]
pri = next((s for (s, c, e) in t if e and s > 20), None)
baja = next((s for (s, c, e) in t if pri is not None and s >= pri and c <= 0.351), None)
fin = next((s for (s, c, e) in t if pri is not None and s > pri and not e), None)
vuelve = next((s for (s, c, e) in t if fin is not None and s >= fin and c >= 0.999), None)
print(f"antes de la tecla {min(ini) if ini else '?'}..{max(ini) if ini else '?'}; elige desde {pri}; en 0,35 a los {None if baja is None else round(baja - pri, 2)} s; deja de elegir a los {None if fin is None else round(fin - pri, 1)} s; en 1 a los {None if vuelve is None else round(vuelve - fin, 2)} s")
ok = ini and min(ini) >= 0.999 and pri and 24.5 <= pri <= 26.5 and baja is not None and baja - pri <= 0.6 and fin is not None and 14 <= fin - pri <= 16.5 and vuelve is not None and 2.6 <= vuelve - fin <= 3.4
sys.exit(0 if ok else 1)
PY
  r=$?
  si "la calma de verdad (Form1: la tecla de los 25 s): $(cat "$W/calma/calma.res")" test $r = 0
fi

# ---------------------------------------------------------------- medio
if [[ " $partes " == *" medio "* ]]; then
  echo "== medio"
  for m in 0 1; do
    n=medio-$m; mkdir -p "$W/$n"; copia "$DATOS0" "$W/$n/datos"
    grep -v '^cielo_media=' "$DATOS0/rocola_config.txt" > "$W/$n/datos/rocola_config.txt" 2>/dev/null; echo "cielo_media=$m" >> "$W/$n/datos/rocola_config.txt"
    corre $n "$prog" 16 "=$W/$n/datos" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 13:00" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CAPTURA="12:$CAPTURAS/medio-$m.png" ROCOLA_CAPTURA_SALE=1
  done
  espera medio-0 medio-1
  python3 "$G/medio.py" "$CAPTURAS/medio-0.png" "$CAPTURAS/medio-1.png" > "$W/medio.txt" 2>&1; r=$?
  si "el cielo a media resolucion y el directo, el mismo degradado: $(cat "$W/medio.txt")" test $r = 0
fi

# ---------------------------------------------------------------- reloj
if [[ " $partes " == *" reloj "* ]]; then
  echo "== reloj (NativeAOT: $AOTMIO)"
  for vuelta in 1 2; do
    for c in encendido apagado; do
      n=reloj-$c-$vuelta; mkdir -p "$W/$n"; copia "$DATOSM" "$W/$n/datos"
      [ $c = apagado ] && printf 'cielo=0\nfiestas=0\nclima=0\nvidrio=0\n' > "$W/$n/datos/rocola_os.txt"
      [ $c = encendido ] && printf 'vidrio_ficha=1\nvidrio_franja=1\nvidrio_lista=1\nvidrio_buscador=1\nluna_camino=1\n' > "$W/$n/datos/rocola_os.txt"
      # la pared llena con la ficha y luego la atraccion (el escaparate), a las 17:40 (el cielo mezclando dos paletas): 70 s, se mide de los 8 s en adelante
      corre $n "$AOTMIO" 70 "=$W/$n/datos" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 17:40" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED \
        ROCOLA_PULSA="6:27+39,7.3:27+39,8.6:27+39,9.9:27+39,11.2:27+39,12.5:27+39,13.8:27+39,15.1:27+39,16.4:27+39,17.7:27+39,19.2:39" ROCOLA_CIELO_RELOJ="$W/$n/reloj.txt"
      espera $n
      echo "   $c (vuelta $vuelta): $(cat "$W/$n/reloj.txt" 2>/dev/null || echo 'sin medida')"
    done
  done
  si "el reloj se midio en las cuatro corridas" bash -c "for n in reloj-encendido-1 reloj-apagado-1 reloj-encendido-2 reloj-apagado-2; do grep -q '^cuadros=' '$W/'\$n'/reloj.txt' || exit 1; done"
fi

echo
echo "$( [ $mal = 0 ] && echo "TODO BIEN ($bien comprobaciones)" || echo "$mal MAL de $((bien + mal))" )"
[ $mal = 0 ]
