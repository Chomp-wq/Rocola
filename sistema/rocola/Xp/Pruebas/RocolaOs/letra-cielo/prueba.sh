#!/bin/bash
# LA PRUEBA DE LA LETRA EN EL CIELO (K6; RocolaOs/RocolaOs.LetraCielo.cs; FASE11_letras.md §6). La rocola entera fuera de pantalla (1024x768, llvmpipe),
# con la tarjeta de sonido de prueba con reloj (suena a su ritmo), MP3 y letras INVENTADOS (hazmp3.py de ../letras: un tono con su ID3 y su USLT) y un
# LRCLIB FALSO (falso.py de ../letras, con datos.json de aqui). Nunca letras de canciones de verdad.
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   BASE:     el programa de antes de la letra (JIT; de fabrica /root/rocola-trabajo/fase11/letra-cielo/base) para «apagado»
#   W:        la carpeta de trabajo (de fabrica /root/rocola-trabajo/fase11/letra-cielo/prueba; se rehace); CAPTURAS (de fabrica $W/capturas)
#   PY:       donde esta brotli (pip install --target PY brotli)
# escenas (de fabrica todas): ganchos modelo apagado compas mp3 sinvoz nada reloj
#   ganchos  ganchos.py TODO BIEN y los dos ganchos (g380, g381) en el manifiesto
#   modelo   `rocola-xp --prueba-letra-cielo-modelo`: las cuentas puras (llenado, cambio de verso, sin voz, reparto de la del MP3)
#   apagado  con letra_cielo=0 la cinta de la tarjeta del reposo con musica es la de BASE; con letra_cielo=1 es otra (control); sin ROCOLA_OS, nada
#   compas   la letra sincronizada: capturas a mitad del llenado, en el cambio de verso, y el compas (el llenado va al tiempo de la cancion)
#   mp3      la letra del MP3 (sin tiempos): los renglones suben despacio, «Letra: el MP3»
#   sinvoz   el tramo sin voz (un renglon vacio del LRC): el cielo de XP tal cual
#   nada     no sale con la pared, el buscador, el disco abierto ni la ayuda
#   reloj    p50/p95/p99/max del trabajo de cada cuadro con la letra y sin ella (ROCOLA_CIELO_RELOJ), en NativeAOT si AOTMIO
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
LET="$G/../letras"; CIE="$G/../cielo"
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
escenas="${*:-ganchos modelo apagado compas mp3 sinvoz nada reloj}"
T0=/root/rocola-trabajo/fase11/letra-cielo
W=${W:-$T0/prueba}
BASE=${BASE:-$T0/base}
AOTMIO=${AOTMIO:-aot:$T0/aot}
CAPTURAS=${CAPTURAS:-$W/capturas}
PY=${PY:-$T0/py}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
LETRAS=${LETRAS:-/root/rocola-trabajo/port/dibujo/letras-xp-falsas}
rm -rf "$W"; mkdir -p "$W" "$CAPTURAS"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
rx() { if [ "${1#aot:}" != "$1" ]; then echo "$(cd "${1#aot:}" && pwd)/rocola-xp"; else echo "dotnet $(cd "$1" && pwd)/rocola-xp.dll"; fi; }
AL=$T0/alsa; mkdir -p "$AL"
[ -f "$AL/libasound_module_pcm_reloj.so" ] || gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
CIUDAD="19.43,-99.13,Ciudad de México"; SINRED="http://127.0.0.1:9"

# EL LRCLIB FALSO: un puerto libre por escena
P=$((21000 + $$ % 20000))
falso() { local o="$W/$1/falso"; mkdir -p "$o"; P=$((P + 1)); python3 "$LET/falso.py" $P "$o" "$G/datos.json" > "$o/salida.txt" 2>&1 & FPID=$!
  for i in $(seq 1 100); do [ -f "$o/listo" ] && break; sleep 0.1; done; : > "$o/peticiones.txt"; }
para_falso() { kill $FPID 2>/dev/null; wait $FPID 2>/dev/null; }

# LOS DATOS: los de partida de las escenas de ventanas (DATOS0) y, en su musica, tres MP3 inventados de 100 s: «Rio Dorado» (su letra sincronizada
# esta en el LRCLIB falso), «Luna de Papel» (solo la USLT del MP3: el LRCLIB falso dice 404) y «Viento Quieto» (sin letra). Los titulos no llevan
# «c» ni «s»: sus teclas son la moneda y saltar en esos datos.
datos() {   # NOMBRE [lineas de rocola_os.txt...]
  local d="$W/datos-$1"; shift; rm -rf "$d"; mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/
  local m="$d/musica/Los Probados - Disco Letras"; mkdir -p "$m"
  python3 "$LET/hazmp3.py" "$m/01 Rio Dorado.mp3" 100 "Rio Dorado" "Los Probados" "Disco Letras" - >/dev/null
  python3 "$LET/hazmp3.py" "$m/02 Luna de Papel.mp3" 100 "Luna de Papel" "Los Probados" "Disco Letras" "primer renglon inventado de la luna\nsegundo renglon inventado de papel\ntercer renglon inventado que sigue\ncuarto renglon inventado del medio\nquinto renglon inventado un poco mas largo que los demas\nsexto renglon inventado de la tarde\nseptimo renglon inventado para repartir\noctavo renglon inventado casi al final\nnoveno renglon inventado de cierre\ndecimo renglon inventado y ultimo" >/dev/null
  python3 "$LET/hazmp3.py" "$m/03 Viento Quieto.mp3" 100 "Viento Quieto" "Los Probados" "Disco Letras" - >/dev/null
  : > "$d/rocola_os.txt"; for l in "$@"; do echo "$l" >> "$d/rocola_os.txt"; done
  echo "$d"
}
# LA ROCOLA: abre NOMBRE DATOS "PULSA" SEGUNDOS [VAR=valor...]: fuera de pantalla; a los SEGUNDOS, SIGTERM
abre() {
  local n="$1" d="$2" pulsa="$3" seg="$4"; shift 4
  local o="$W/$n"; mkdir -p "$o/run"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "/dev/null" "$o/registro.txt" > "$o/alsa.conf"
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" \
      ROCOLA_LETRAS_XP="$LETRAS" ROCOLA_LETRA_API="http://127.0.0.1:$P" ROCOLA_CLIMA_API="${ROCOLA_CLIMA_API:-$SINRED}" ROCOLA_LUGAR_API="$SINRED/lugar" ROCOLA_CIUDAD="$CIUDAD" TZ=America/Mexico_City \
      ROCOLA_LETRA_LOG="$o/letra.log" "$@" $(rx "${PROG:-$prog}") > "$o/salida.txt" 2>&1 ) &
  ROCOLA=$!
  ( sleep "$seg"; kill -TERM $ROCOLA 2>/dev/null; sleep 30; kill -KILL $ROCOLA 2>/dev/null ) & VIG=$!
}
espera() { wait $ROCOLA; echo $? > "$W/$1/salio"; kill $VIG 2>/dev/null; wait $VIG 2>/dev/null; }
escribe() { local s="$1" pal="$2" r="" i c; for ((i = 0; i < ${#pal}; i++)); do c=$(printf '%d' "'${pal:$i:1}"); [ $c -ne 32 ] && c=$((c - 32)); r="$r,$(echo "$s + $i * 0.25" | bc):$c"; done; echo "${r#,}"; }
fin() { echo "$1 + ${#2} * 0.25" | bc; }
derechas() { local s="$1" n="$2" r="" i; for ((i = 0; i < n; i++)); do r="$r,$(echo "$s + $i * 0.15" | bc):39"; done; echo "${r#,}"; }
borra() { local s="$1" n="$2" r="" i; for ((i = 0; i < n; i++)); do r="$r,$(echo "$s + $i * 0.3" | bc):27"; done; echo "${r#,}"; }
BASEK="7:67,8:27+38"   # la moneda a los 7 s y el buscador a los 8 s (REGRESAR+ARRIBA)
# LAS TECLAS que piden una cancion por su titulo: se escribe, se baja a la tarjeta y se pone (ENTER)
pide() { local f="$1" t1; t1=$(fin 9.5 "$f"); echo "$BASEK,$(escribe 9.5 "$f"),$(derechas $(echo "$t1 + 1" | bc) 14),$(echo "$t1 + 4" | bc):13,$(borra $(echo "$t1 + 8" | bc) ${#f}),$(echo "$t1 + 8 + ${#f} * 0.3 + 1" | bc):27+38"; }   # (REGRESAR borra lo escrito y REGRESAR+ARRIBA cierra el buscador; el reposo se abre 15 s despues)
# cuando empieza a sonar lo pedido (segundos desde el arranque): el ENTER + 0,5 s de carga, apuntado; el reposo se abre 15 s despues
CAPS() { local r="" t; for t in "$@"; do r="$r,$t:$CAPTURAS/${NOMBRE}-$t.png"; done; echo "${r#,}"; }
muestra_pos() { grep -o 'pos=[0-9.]*' "$1" | sed -n "$2"p | cut -d= -f2; }
# lo que dice el log en el instante de la hora de pared de un archivo (ms de epoca): la linea mas cercana
linea_log() { python3 - "$1" "$2" "${3:-0}" <<'EOF'
import sys, os
log, png = sys.argv[1], sys.argv[2]
m = os.stat(png).st_mtime * 1000 - float(sys.argv[3])   # (el cuadro se compuso antes de escribirse el PNG: la captura para el cuadro cerca de un segundo)
mejor = None
for l in open(log):
    p = l.split()
    if not p or not p[0].isdigit(): continue
    d = abs(int(p[0]) - m)
    if mejor is None or d < mejor[0]: mejor = (d, l.strip())
print("%d %s" % (mejor[0], mejor[1]) if mejor else "")
EOF
}

# ---------------------------------------------------------------- ganchos
if [[ " $escenas " == *" ganchos "* ]]; then
  echo "== ganchos"
  python3 "$XP/Pruebas/RocolaOs/ganchos.py" > "$W/ganchos.txt" 2>&1; r=$?
  si "ganchos.py: TODO BIEN ($(grep -o '[0-9]* archivos comparados con [0-9a-f]* ([0-9]* con ganchos)' "$W/ganchos.txt"))" test $r = 0 -a "$(tail -1 "$W/ganchos.txt")" = "TODO BIEN"
  si "g380 y g381 en el manifiesto" test "$(grep -cP '^g38[01]\t' "$XP/RocolaOs/GANCHOS.tsv")" = 2
fi

# ---------------------------------------------------------------- modelo
if [[ " $escenas " == *" modelo "* ]]; then
  echo "== modelo"
  mkdir -p "$W/modelo"
  ( cd "$W/modelo" && ROCOLA_PANTALLA=fuera:320x240 ROCOLA_DATOS="$W/modelo" timeout 120 $(rx "$prog") --prueba-letra-cielo-modelo ) > "$W/modelo/salida.txt" 2>&1; r=$?
  grep -E '^(BIEN|MAL) ' "$W/modelo/salida.txt" | sed 's/^/   /'
  si "--prueba-letra-cielo-modelo: $(tail -1 "$W/modelo/salida.txt")" test $r = 0
fi


source "$G/escenas.sh"
