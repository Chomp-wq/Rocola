#!/bin/bash
# LA PRUEBA DEL RELLENO DE LOS HUECOS (R1; funciones «huecos_mide» y «relleno_huecos»: RocolaOs/RocolaOs.Huecos*.cs; FASE11_relleno-huecos.md).
# Musica SINTETICA (musica.py: acordes, voz, bombo, charles, platillo y fondo; 20 s) codificada aqui con ffmpeg: LAME a 320, 192, 128, 96 y
# 64 kbps y shine (el codificador simple, como muchos MP3 viejos) a 128 y 96; y uno de MPEG-2 (22 kHz, mono, 32 kbps), que este lector no
# entiende y no debe tocarse. Nada se oyo: son cuentas.
#   bash prueba.sh PROGRAMA [partes...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   partes (de fabrica todas): mide decodifica oido salta costo escena
#   W (/root/rocola-trabajo/fase11/huecos/prueba; se rehace)
# Las partes:
#   mide        la medida del repaso (tres tramos o el archivo entero): TRAEN HUECOS LAME 96 y 64 y shine; NO LAME 320, 192 y 128 ni el de MPEG-2
#   decodifica  por el lector de XP (Mp3FileReader con libmpg123): el envuelto sin sumar sale IGUAL bit a bit (como apagado); el DOBLE que
#               anula (el lector sintetiza la cancion entera con el signo cambiado y se suma) deja casi silencio: su lectura, su sintesis, su
#               escala y su momento son los de libmpg123 (y los de NLayer en coma flotante, el de las que llegan al maximo); en las que traen
#               huecos, el relleno pone algo; en el de MPEG-2, nada
#   oido        frente al ORIGINAL (compara.py): en las que traen huecos baja la parte de lo que se oye que queda 15 dB o mas por debajo, y lo
#               que sobra (6 dB o mas por encima) queda chico; en la de 320 no cambia nada
#   salta       con saltos (el Reset del descompresor): el doble sigue anulando y la cola no se desfasa
#   costo       lo que cuesta (ms por segundo de cancion, la mejor de 7 vueltas) y que no pasa de 15 ms por segundo aqui
#   escena      la rocola de verdad (fuera de pantalla, la tarjeta con reloj): con ROCOLA_OS=huecos_mide,relleno_huecos el repaso mide la
#               biblioteca (rocola_huecos.txt: solo numeros) y la vieja que se pide suena envuelta y con relleno; con relleno_huecos apagada,
#               nada se envuelve; y sin ROCOLA_OS ni se mide
# Cada comprobacion: BIEN o MAL en una linea; al final «TODO BIEN (N comprobaciones)» o cuantas MAL (y sale con 1).
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-mide decodifica oido salta costo escena}"
W=${W:-/root/rocola-trabajo/fase11/huecos/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
num() { sed -n "s/.* $2=\([^ ]*\).*/\1/p" "$1" | head -1; }   # num ARCHIVO CLAVE: el valor de «clave=valor» en esa linea
py() { python3 -c "import sys; sys.exit(0 if ($1) else 1)"; }
export ROCOLA_PANTALLA=fuera:64x64

# ---------- la musica ----------
M=$W/mp3; mkdir -p "$M"
python3 "$G/musica.py" "$M/original.wav" 20 || exit 1
for k in 320 192 128 96 64; do ffmpeg -hide_banner -loglevel error -y -i "$M/original.wav" -c:a libmp3lame -b:a ${k}k "$M/lame$k.mp3" || exit 1; done
for k in 128 96; do ffmpeg -hide_banner -loglevel error -y -i "$M/original.wav" -c:a libshine -b:a ${k}k "$M/shine$k.mp3" || exit 1; done
ffmpeg -hide_banner -loglevel error -y -i "$M/original.wav" -ar 22050 -ac 1 -c:a libmp3lame -b:a 32k "$M/mpeg2.mp3" || exit 1
TRAEN="lame96 lame64 shine128 shine96"; NO="lame320 lame192 lame128"; TODOS="$NO $TRAEN"

# ---------------------------------------------------------------- mide
if [[ " $partes " == *" mide "* ]]; then
  echo "== mide (la medida del repaso)"
  "${RX[@]}" --prueba-huecos mide $(for f in $TODOS mpeg2; do echo "$M/$f.mp3"; done) 2>&1 | grep '^MIDE' > "$W/mide.txt"
  sed 's/^/   /' "$W/mide.txt"
  for f in $TRAEN; do l=$(grep "^MIDE $f.mp3 " "$W/mide.txt"); si "mide: $f trae huecos (relleno $(echo "$l" | sed -n 's/.* relleno_db=\([^ ]*\).*/\1/p') dB)" bash -c "echo '$l' | grep -q 'tramos: .* trae=si .* | entero: .* trae=si'"; done
  for f in $NO; do l=$(grep "^MIDE $f.mp3 " "$W/mide.txt"); si "mide: $f no trae huecos (relleno $(echo "$l" | sed -n 's/.* relleno_db=\([^ ]*\).*/\1/p') dB)" bash -c "echo '$l' | grep -q 'tramos: .* trae=no .* | entero: .* trae=no'"; done
  si "mide: el de MPEG-2 (22 kHz) no se entiende y no se mide (ni una candidata)" bash -c "grep '^MIDE mpeg2.mp3 ' '$W/mide.txt' | grep -q 'tramos: cuadros=0 candidatas=0 .* trae=no'"
fi

# ---------------------------------------------------------------- decodifica
if [[ " $partes " == *" decodifica "* || " $partes " == *" oido "* ]]; then
  echo "== decodifica (por el lector de XP con libmpg123: sin, envuelto, envuelto sin sumar y el doble que anula)"
  for f in $TODOS mpeg2; do "${RX[@]}" --prueba-huecos decodifica "$M/$f.mp3" "$W/$f" 2>&1 | grep '^DECODIFICA' > "$W/dec-$f.txt"; sed 's/^/   /' "$W/dec-$f.txt"; done
fi
if [[ " $partes " == *" decodifica "* ]]; then
  for f in $TODOS mpeg2; do
    d="$W/dec-$f.txt"
    si "decodifica $f: envuelto sin sumar = sin envolver, bit a bit (como con el relleno apagado o «Escuchar sin mejoras»)" bash -c "grep -q ' pasa_igual=si ' '$d' && grep -q ' pasa_sumadas=0 ' '$d'"
    si "decodifica $f: las mismas muestras con y sin el relleno ($(num "$d" muestras_con))" test "$(num "$d" muestras_con)" = "$(num "$d" muestras_sin)"
  done
  for f in $TODOS; do
    d="$W/dec-$f.txt"
    si "decodifica $f: el doble anula a $(num "$d" anula_db) dB (lo de 16 bits): lee, sintetiza y cae como libmpg123" py "$(num "$d" anula_db) <= -90 and $(num "$d" anula_desfases) == 0"
    si "decodifica $f: y en coma flotante (NLayer, el de las que llegan al maximo) a $(num "$d" anula_flotante_db) dB" py "$(num "$d" anula_flotante_db) <= -100 and $(num "$d" flotante_desfases) == 0"
  done
  for f in $TRAEN; do
    d="$W/dec-$f.txt"
    si "decodifica $f: el relleno pone algo ($(num "$d" rellenadas) bandas, $(num "$d" relleno_db) dB de la cancion)" py "$(num "$d" rellenadas) > 100 and -60 <= $(num "$d" relleno_db) <= -25"
  done
  d="$W/dec-mpeg2.txt"
  si "decodifica mpeg2: envuelto a la fuerza, no suma nada (no lo entiende: $(num "$d" raros) cuadros raros)" py "$(num "$d" sumadas) == 0 and $(num "$d" rellenadas) == 0"
fi

# ---------------------------------------------------------------- oido
if [[ " $partes " == *" oido "* ]]; then
  echo "== oido (frente al original: lo que queda 15 dB o mas por debajo y lo que sobra 6 dB o mas por encima, de 3 a 16 kHz)"
  for f in $TODOS; do python3 "$G/compara.py" "$M/original.wav" "$W/$f" > "$W/oido-$f.txt"; echo "   $f: $(grep '3000-16000' "$W/oido-$f.txt")"; done
  for f in $TRAEN; do
    l=$(grep '3000-16000' "$W/oido-$f.txt"); s=$(echo "$l" | sed -n 's/.*huecos_sin=\([^ ]*\).*/\1/p'); c=$(echo "$l" | sed -n 's/.*huecos_con=\([^ ]*\).*/\1/p'); so=$(echo "$l" | sed -n 's/.*sobra_con=\([^ ]*\).*/\1/p')
    si "oido $f: menos huecos ($s -> $c %) y lo que sobra chico ($so %): los huecos + lo que sobra bajan" py "$c < $s and $so <= 3 and $c + $so < $s"
  done
  for f in shine128 shine96; do
    l=$(grep '3000-16000' "$W/oido-$f.txt"); s=$(echo "$l" | sed -n 's/.*huecos_sin=\([^ ]*\).*/\1/p'); c=$(echo "$l" | sed -n 's/.*huecos_con=\([^ ]*\).*/\1/p')
    si "oido $f: en el codificador malo, los huecos bajan a la mitad o menos ($s -> $c %)" py "$c <= $s / 2"
  done
  l=$(grep '3000-16000' "$W/oido-lame320.txt"); s=$(echo "$l" | sed -n 's/.*huecos_sin=\([^ ]*\).*/\1/p'); c=$(echo "$l" | sed -n 's/.*huecos_con=\([^ ]*\).*/\1/p'); so=$(echo "$l" | sed -n 's/.*sobra_con=\([^ ]*\).*/\1/p')
  si "oido lame320: aun envuelta a la fuerza, casi nada cambia ($s -> $c %, sobra $so %)" py "abs($c - $s) <= 0.05 and $so <= 0.1"
fi

# ---------------------------------------------------------------- salta
if [[ " $partes " == *" salta "* ]]; then
  echo "== salta (con saltos a la mitad y al quinto: el Reset del descompresor)"
  for f in shine96 lame128; do
    "${RX[@]}" --prueba-huecos salta "$M/$f.mp3" 2>&1 | grep '^SALTA' > "$W/salta-$f.txt"; sed 's/^/   /' "$W/salta-$f.txt"
    si "salta $f: el doble sigue anulando tras los saltos ($(num "$W/salta-$f.txt" anula_db) dB) y la cola no se desfasa" py "$(num "$W/salta-$f.txt" anula_db) <= -90 and $(num "$W/salta-$f.txt" desfases) == 0"
  done
fi

# ---------------------------------------------------------------- costo
if [[ " $partes " == *" costo "* ]]; then
  echo "== costo (la mejor de 7 vueltas; ms por segundo de cancion)"
  for f in shine96 lame128 lame320; do
    "${RX[@]}" --prueba-huecos costo "$M/$f.mp3" 2>&1 | grep '^COSTO' > "$W/costo-$f.txt"; sed 's/^/   /' "$W/costo-$f.txt"
    si "costo $f: el relleno cuesta $(num "$W/costo-$f.txt" ms_por_s_relleno) ms por segundo de cancion aqui (menos de 15)" py "$(num "$W/costo-$f.txt" ms_por_s_relleno) < 15"
  done
fi

# ---------------------------------------------------------------- escena: la rocola de verdad
if [[ " $partes " == *" escena "* ]]; then
  echo "== escena (rocola-xp fuera de pantalla, con la tarjeta con reloj)"
  AL=$W/alsa; mkdir -p "$AL"
  gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
  datos() {   # la carpeta de datos de la escena con su musica: la vieja (shine 96) y la buena (LAME 320)
    local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d/musica/Grupo Viejo/Disco Viejo"
    cp "$DATOS0/rocola_teclas.txt" "$d/"
    cp "$M/shine96.mp3" "$d/musica/Grupo Viejo/Disco Viejo/01 vieja.mp3"; cp "$M/lame320.mp3" "$d/musica/Grupo Viejo/Disco Viejo/02 buena.mp3"
    echo "$d"
  }
  # escena NOMBRE DATOS [VAR=valor...]: la moneda a los 7 s, «pon 01 vieja.mp3» a los 12 (el doble calidad-prueba), la foto de vivo.txt a
  # los 18 y a los 24 el cierre (SIGTERM)
  escena() {
    local n="$1" d="$2"; shift 2
    local o="$W/$n"; mkdir -p "$o/run/rocola-xp/spotify"
    printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" > "$o/alsa.conf"
    ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf \
        ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="7:67" ROCOLA_CAPTURA_SALE=0 ROCOLA_HUECOS_SIN_PAUSA=1 \
        ROCOLA_CLIMA_API=http://127.0.0.1:7 ROCOLA_LUGAR_API=http://127.0.0.1:7/lugar ROCOLA_LETRA_API=http://127.0.0.1:7 ROCOLA_GENIUS_API=http://127.0.0.1:7 \
        "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
    local p=$!
    sleep 12; echo "pon 01 vieja.mp3" >> "$o/run/rocola-xp/spotify/prueba-xp"
    sleep 6; cp "$o/run/rocola-os-prueba-vivo.txt" "$o/vivo.txt" 2>/dev/null
    sleep 6; kill -TERM $p 2>/dev/null
    ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
    wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null; rm -f "$o/sonado.raw"
  }
  d=$(datos con); escena con "$d" ROCOLA_OS=huecos_mide,relleno_huecos,calidad-prueba,prueba-andamio
  o=$W/con; sed 's/^/   vivo: /' "$o/vivo.txt" 2>/dev/null | grep -E 'huecos|relleno'; sed 's/^/   tabla: /' "$d/rocola_huecos.txt" 2>/dev/null
  grep -F '[huecos_mide]' "$d/rocola_os_bitacora.txt" 2>/dev/null | sed 's/^/   /'
  si "escena con: sale con 0" test "$(cat $o/salio)" = 0
  si "escena con: el repaso midio las dos (rocola_huecos.txt) y dice que una trae huecos" bash -c "[ \$(wc -l < '$d/rocola_huecos.txt') = 2 ] && grep -q '2 MP3 medidos, 1 con huecos' '$d/rocola_os_bitacora.txt'"
  si "escena con: la tabla solo trae la ruta y numeros" bash -c "awk -F'\t' 'NF != 8 {bad=1} {for (i = 2; i <= 8; i++) if (\$i !~ /^-?[0-9]+\$/) bad=1} END {exit bad}' '$d/rocola_huecos.txt'"
  si "escena con: la vieja suena envuelta y con relleno (vivo: $(grep '^relleno_huecos=' "$o/vivo.txt" 2>/dev/null))" bash -c "grep -qE '^relleno_huecos=encendida abiertas=[1-9][0-9]* rellenadas=[1-9]' '$o/vivo.txt'"
  si "escena con: ningun fallo de Rocola OS apuntado (rocola_error.txt)" bash -c "! grep -q 'Rocola OS: relleno_huecos\|Rocola OS: huecos_mide' '$d/rocola_error.txt' 2>/dev/null"
  d=$(datos apagado); escena apagado "$d" ROCOLA_OS=huecos_mide,calidad-prueba,prueba-andamio
  o=$W/apagado; sed 's/^/   vivo: /' "$o/vivo.txt" 2>/dev/null | grep -E 'huecos|relleno'
  si "escena apagado: sale con 0, mide igual y no envuelve nada (sin renglon de relleno_huecos)" bash -c "[ \$(cat $o/salio) = 0 ] && grep -q '^huecos_mide=listo 2 medidas, 1 con huecos' '$o/vivo.txt' && ! grep -q '^relleno_huecos' '$o/vivo.txt' && grep -q '^calidad-prueba\|^prueba-andamio' '$o/vivo.txt'"
  d=$(datos sinros); escena sinros "$d"
  o=$W/sinros
  si "escena sin ROCOLA_OS: sale con 0 y ni se mide (sin rocola_huecos.txt ni bitacora de Rocola OS)" bash -c "[ \$(cat $o/salio) = 0 ] && [ ! -e '$d/rocola_huecos.txt' ] && [ ! -e '$d/rocola_os_bitacora.txt' ]"
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; exit 1; fi
