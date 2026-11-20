#!/bin/bash
# LA PRUEBA DE LOS «MP3» QUE NO LO SON, CONVERTIDOS (Rocola OS, funcion «convierte»: RocolaOs/RocolaOs.Convierte.cs; PORT_xp-1718.md, «Lo de
# Linux»). rocola-xp de verdad, fuera de pantalla, con la tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c: lo que DE VERDAD sono)
# y el doble «calidad-prueba» de la sustitucion (RocolaOs/RocolaOs.CalidadPrueba.cs: «pon FINAL» pide la cancion con AddSong de XP, «mira» dice
# lo que suena y los creditos). Datos INVENTADOS: una biblioteca hecha aqui con ffmpeg, cada cancion con su tono (asi se sabe cual se oyo):
#   01 m4a.mp3 (AAC en MP4, 440 Hz)  02 ogg.mp3 (Vorbis, 550)  03 flac.mp3 (FLAC, 660)  04 wav.mp3 (WAV PCM, 770)
#   05 mp3wav.mp3 (un MP3 dentro de un WAV, formato 0x55: lo toca el lector de MP3, pasa como siempre; 880)
#   06 lenta.mp3 (AAC, 990: con un ffmpeg falso que tarda 35 s)  07 buena.mp3 (un MP3 de verdad, 330)
#   bash prueba.sh PROGRAMA [escenas...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   escenas (de fabrica todas): convierte xp lenta sinffmpeg sinsitio sinrocolaos
#   W (/root/rocola-trabajo/fase11/xp-1718-linux/convierte; se rehace)
# Hace falta ser root para «sinsitio» (un tmpfs de 40 MB). Cada comprobacion: BIEN o MAL; al final «TODO BIEN (N)» o cuantas MAL (sale con 1).
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd); SX="$G/../spotify-xp"
prog="${1:?uso: prueba.sh PROGRAMA [escenas]}"; shift
partes="${*:-convierte xp lenta sinffmpeg sinsitio sinrocolaos}"
W=${W:-/root/rocola-trabajo/fase11/xp-1718-linux/convierte}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
mountpoint -q "$W/chico" 2>/dev/null && umount "$W/chico"
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
nocontiene() { ! grep -qF -- "$2" "$1" 2>/dev/null; }
entre() { python3 -c "import sys; v=float(sys.argv[1]); sys.exit(0 if float(sys.argv[2]) <= v <= float(sys.argv[3]) else 1)" "$1" "$2" "$3"; }
AL=$W/alsa; mkdir -p "$AL"
gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1

# ---------- la biblioteca inventada ----------
M=$W/mp3; mkdir -p "$M"
hace() {   # NOMBRE HZ SEG ARGUMENTOS-DE-SALIDA...
  local n="$1" hz="$2" s="$3"; shift 3
  ffmpeg -hide_banner -loglevel error -y -f lavfi -i "sine=frequency=$hz:sample_rate=44100:duration=$s" -ac 2 -metadata title="${n#* }" -metadata artist="Grupo Formatos" -metadata album="Disco Raro" "$@" "$M/$n.mp3" || exit 1
}
hace "01 m4a" 440 6 -c:a aac -b:a 160k -f mp4
hace "02 ogg" 550 6 -c:a libvorbis -q:a 5 -f ogg
hace "03 flac" 660 6 -c:a flac -f flac
hace "04 wav" 770 6 -c:a pcm_s16le -f wav
hace "05 mp3wav" 880 6 -c:a libmp3lame -b:a 192k -f wav
hace "06 lenta" 990 6 -c:a aac -b:a 160k -f mp4
hace "07 buena" 330 6 -c:a libmp3lame -b:a 192k -id3v2_version 3 -f mp3
# datos NOMBRE CANCIONES...: la carpeta de datos de la escena (sin nada aprendido) con esas canciones en «musica/Grupo Formatos/Disco Raro»
datos() {
  local d="$W/datos-$1"; shift; rm -rf "$d"; mkdir -p "$d/musica/Grupo Formatos/Disco Raro"
  cp "$DATOS0/rocola_teclas.txt" "$d/"   # (moneda 67, poner 13...)
  for x in "$@"; do cp -p "$M/$x.mp3" "$d/musica/Grupo Formatos/Disco Raro/"; done
  echo "$d"
}
huellas() { (cd "$1/musica" && find . -type f -print0 | sort -z | xargs -0 sha256sum; find . -type f -printf '%P %s %T@\n' | sort) ; }
# un ffmpeg falso que tarda (y despues hace lo mismo que el de verdad)
printf '#!/bin/sh\nsleep 35\nexec ffmpeg "$@"\n' > "$W/ffmpeg-lento"; chmod +x "$W/ffmpeg-lento"

# abre NOMBRE DATOS "PULSA" "ORDENES" SEGUNDOS [VAR=valor...]: la rocola con esos datos y las teclas PULSA; las ORDENES («SEG:pon FINAL;SEG:mira»)
# al doble a su hora. A los SEGUNDOS, SIGTERM (el apagado). Deja en $W/NOMBRE: salida.txt, run/, frec.txt, salio y log.txt (lo del doble).
abre() {
  local n="$1" d="$2" pulsa="$3" ord="$4" seg="$5"; shift 5
  local o="$W/$n"; mkdir -p "$o/run"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AL/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" > "$o/alsa.conf"
  local T0; T0=$(date +%s.%N)
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf \
      ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA_SALE=0 "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$!
  ( IFS=';'; for x in $ord; do [ -z "$x" ] && continue; s="${x%%:*}"; l="${x#*:}"
      while [ "$(echo "$(date +%s.%N) - $T0 < $s" | bc)" = 1 ]; do sleep 0.1; done
      mkdir -p "$o/run/rocola-xp/spotify"; echo "$l" >> "$o/run/rocola-xp/spotify/prueba-xp"; done ) &
  local q=$!
  sleep "$seg"; kill $q 2>/dev/null
  kill -TERM $p 2>/dev/null
  ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
  cp "$o/run/rocola-xp/spotify/prueba-xp.log" "$o/log.txt" 2>/dev/null || : > "$o/log.txt"
  [ -f "$o/sonado.raw" ] && python3 "$SX/frecuencias.py" "$o/sonado.raw" "$([ -f "$o/registro.txt" ] && echo "$o/registro.txt" || echo 44100)" > "$o/frec.txt"
  [ -f "$o/frec.txt" ] || : > "$o/frec.txt"
  rm -f "$o/sonado.raw"
}
oye() { [ -f "$1" ] || { echo 0.00; return; }; awk -v f="$2" '$3>=f-15 && $3<=f+15 {s+=$2-$1} END {printf "%.2f\n", s+0}' "$1"; }
primero() { awk -v f="$2" '$3>=f-15 && $3<=f+15 && $2-$1>=0.3 {print $1; exit}' "$1"; }
bit() { echo "$1/rocola_os_bitacora.txt"; }
cobro() { grep -F "pon $2 creditos" "$W/$1/log.txt" | sed -n "${3:-1}p" | sed 's/.*creditos \([0-9]*\)->\([0-9]*\).*/\1 \2/'; }   # «antes despues» de la N-esima vez que se pidio
mira() { grep '^mira' "$W/$1/log.txt" | sed -n "${2}p"; }
cred() { mira "$1" "$2" | sed -n 's/.*creditos=\([0-9]*\).*/\1/p'; }   # los creditos que dice «mira» (solo si algo suena)
conv() { local c="${1:-}"; [ -d "$c" ] && find "$c" -name '*.wav' ! -name '*.haciendo.wav' | wc -l || echo 0; }
sec() { echo "== $1"; }
OS="ROCOLA_OS=convierte,calidad-prueba"

# ---------------------------------------------------------------- convierte: los cuatro de otro formato suenan desde su WAV; el MP3 en WAV y el bueno, igual
if [[ " $partes " == *" convierte "* ]]; then
  sec "convierte (M4A, OGG, FLAC y WAV con nombre .mp3: suenan desde su copia en WAV; el MP3 dentro de un WAV y el MP3 de verdad, como siempre)"
  d=$(datos convierte "01 m4a" "02 ogg" "03 flac" "04 wav" "05 mp3wav" "07 buena"); huellas "$d" > "$W/convierte-antes.txt"; C=$W/convierte-wav
  abre convierte "$d" "7:67,7.4:67" "10:pon 01 m4a.mp3;10.3:pon 02 ogg.mp3;10.6:pon 03 flac.mp3;10.9:pon 04 wav.mp3;11.2:pon 05 mp3wav.mp3;11.5:pon 07 buena.mp3;13:mira;58:pon 07 buena.mp3" 60 $OS ROCOLA_CONVERTIDAS="$C"
  o="$W/convierte"; f="$o/frec.txt"; b=$(bit "$d")
  sed 's/^/   /' "$o/log.txt"; grep -F '[convierte]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'
  si "convierte: sale con 0" test "$(cat $o/salio)" = 0
  for x in "01 m4a:440" "02 ogg:550" "03 flac:660" "04 wav:770" "05 mp3wav:880" "07 buena:330"; do
    n=${x%:*}; hz=${x#*:}; t=$(oye "$f" $hz); c=$(cobro convierte "$n.mp3")
    si "convierte: «$n» se cobro (${c:-?}) y se oyo entera ($hz Hz: $t s de 6)" bash -c "[ -n '$c' ] && [ \$(echo '$c' | awk '{print \$1-\$2}') = 1 ] && python3 -c 'import sys; sys.exit(0 if 5.5 <= $t <= 6.6 else 1)'"
  done
  a=$(cobro convierte "01 m4a.mp3" | cut -d' ' -f1); z=$(cobro convierte "07 buena.mp3" 2 | cut -d' ' -f1)
  si "convierte: ningun credito devuelto (antes de la primera, $a; al pedir otra al final, ${z:-?} = $a menos las 6)" test "${z:-x}" = "$((a - 6))"
  si "convierte: cuatro copias en WAV en ROCOLA_CONVERTIDAS ($(conv "$C")): ni el MP3 dentro del WAV ni el bueno" test "$(conv "$C")" = 4
  si "convierte: cada copia es un WAV PCM de 16 bits" bash -c "for w in '$C'/*.wav; do ffprobe -v error -show_entries stream=codec_name -of csv=p=0 \"\$w\" 2>/dev/null | grep -qx pcm_s16le || exit 1; done"
  si "convierte: la bitacora dice las cuatro (WAV listo) y ninguna mas" bash -c "[ \$(grep -c 'WAV listo' '$b') = 4 ] && ! grep -qE '(05 mp3wav|07 buena).mp3: WAV' '$b'"
  huellas "$d" > "$W/convierte-despues.txt"
  si "convierte: LOS ORIGINALES IGUALES byte a byte (sha256), con su tamano y su fecha" cmp -s "$W/convierte-antes.txt" "$W/convierte-despues.txt"
  si "convierte: nada en rocola_incompatibles.txt" bash -c "[ ! -s '$d/rocola_incompatibles.txt' ]"
  si "convierte: lo mas sonado las cuenta por su ruta .mp3 de siempre (rocola_stats.txt), nunca por la del WAV" bash -c "for n in '01 m4a' '02 ogg' '03 flac' '04 wav'; do grep -q \"|.*Disco Raro/\$n.mp3\" '$d/rocola_stats.txt' || exit 1; done; ! grep -q '\\.wav' '$d/rocola_stats.txt'"
  si "convierte: el repaso las midio desde su WAV, bajo la ruta del original (rocola_volumen.txt: las seis)" bash -c "for n in '01 m4a' '02 ogg' '03 flac' '04 wav' '05 mp3wav' '07 buena'; do grep -q \"|.*Disco Raro/\$n.mp3\$\" <(tr -d '\r' < '$d/rocola_volumen.txt') || exit 1; done; ! grep -q '\\.wav' '$d/rocola_volumen.txt'"
fi

# ---------------------------------------------------------------- xp: sin la funcion, lo de XP (credito devuelto, danada, rocola_incompatibles.txt)
if [[ " $partes " == *" xp "* ]]; then
  sec "xp (ROCOLA_OS sin «convierte»: lo de XP del 17-11-2026)"
  d=$(datos xp "01 m4a" "02 ogg" "07 buena"); huellas "$d" > "$W/xp-antes.txt"; C=$W/xp-wav
  abre xp "$d" "7:67" "10:pon 01 m4a.mp3;10.3:pon 02 ogg.mp3;10.6:pon 07 buena.mp3;13:mira;22:pon 01 m4a.mp3;23:mira" 25 ROCOLA_OS=calidad-prueba ROCOLA_CONVERTIDAS="$C"
  o="$W/xp"; f="$o/frec.txt"
  sed 's/^/   /' "$o/log.txt"
  si "xp: sale con 0" test "$(cat $o/salio)" = 0
  si "xp: no se oyen (440: $(oye "$f" 440) s, 550: $(oye "$f" 550) s) y la buena si (330: $(oye "$f" 330) s)" bash -c "python3 -c 'import sys; sys.exit(0 if $(oye "$f" 440) + $(oye "$f" 550) <= 0.1 and 5.5 <= $(oye "$f" 330) <= 6.6 else 1)'"
  a=$(cobro xp "01 m4a.mp3" | cut -d' ' -f1)
  si "xp: los dos creditos devueltos (de $a: queda la buena cobrada, $(cred xp 1))" test "$(cred xp 1)" = "$((a - 1))"
  si "xp: pedida otra vez, danada: no se cobra ($(cobro xp '01 m4a.mp3' 2))" bash -c "c='$(cobro xp '01 m4a.mp3' 2)'; [ -n \"\$c\" ] && [ \"\${c% *}\" = \"\${c#* }\" ]"
  si "xp: las dos en rocola_incompatibles.txt («no es un MP3»)" bash -c "grep -q '01 m4a.mp3.*no es un MP3: es M4A/AAC' '$d/rocola_incompatibles.txt' && grep -q '02 ogg.mp3.*no es un MP3: es OGG' '$d/rocola_incompatibles.txt'"
  si "xp: ninguna copia (ni la carpeta)" test ! -e "$C"
  huellas "$d" > "$W/xp-despues.txt"
  si "xp: los originales iguales" cmp -s "$W/xp-antes.txt" "$W/xp-despues.txt"
fi

# ---------------------------------------------------------------- lenta: tarda mas de 20 s: credito devuelto sin marcarla danada; la proxima vez suena
if [[ " $partes " == *" lenta "* ]]; then
  sec "lenta (un ffmpeg que tarda 35 s: pedida, 20 s de espera y el credito devuelto; sin marcarla danada; a la proxima suena)"
  d=$(datos lenta "06 lenta" "07 buena"); huellas "$d" > "$W/lenta-antes.txt"; C=$W/lenta-wav
  abre lenta "$d" "7:67" "9:pon 06 lenta.mp3;31.5:mira;62:pon 06 lenta.mp3;64:mira" 72 $OS ROCOLA_CONVERTIDAS="$C" ROCOLA_FFMPEG="$W/ffmpeg-lento"
  o="$W/lenta"; f="$o/frec.txt"; b=$(bit "$d")
  sed 's/^/   /' "$o/log.txt"; grep -F '[convierte]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'
  si "lenta: sale con 0" test "$(cat $o/salio)" = 0
  a=$(cobro lenta "06 lenta.mp3" | cut -d' ' -f1)
  si "lenta: la primera vez se cobra ($(cobro lenta '06 lenta.mp3')), a los 31,5 s ya no esta (mira: $(mira lenta 1 | cut -c1-12)) y el credito se devolvio (al pedirla otra vez habia $(cobro lenta '06 lenta.mp3' 2 | cut -d' ' -f1))" bash -c "[ '$(mira lenta 1 | cut -c1-9)' = 'mira nada' ] && [ '$(cobro lenta '06 lenta.mp3' 2 | cut -d' ' -f1)' = '$a' ]"
  si "lenta: el motivo: se esta convirtiendo (rocola_error.txt)" contiene "$d/rocola_error.txt" "se esta convirtiendo"
  si "lenta: la copia llego despues (bitacora: WAV listo)" bash -c "grep -q '06 lenta.mp3: WAV listo' '$b'"
  si "lenta: no quedo danada: la segunda vez se cobra ($(cobro lenta '06 lenta.mp3' 2))" bash -c "c='$(cobro lenta '06 lenta.mp3' 2)'; [ -n \"\$c\" ] && [ \$(echo \"\$c\" | awk '{print \$1-\$2}') = 1 ]"
  t=$(oye "$f" 990); p=$(primero "$f" 990)
  si "lenta: y suena entera la segunda vez (990 Hz: $t s, desde el s ${p:-?} de lo sonado)" bash -c "python3 -c 'import sys; sys.exit(0 if 5.5 <= $t <= 6.6 else 1)'"
  si "lenta: la segunda vez, sonando (mira: lo que suena es la cancion de la rocola, su ruta .mp3)" bash -c "mira() { grep '^mira' '$o/log.txt' | sed -n 2p; }; mira | grep -q 'Disco Raro/06 lenta.mp3 |'"
  huellas "$d" > "$W/lenta-despues.txt"
  si "lenta: el original igual" cmp -s "$W/lenta-antes.txt" "$W/lenta-despues.txt"
fi

# ---------------------------------------------------------------- sinffmpeg: lo de XP
if [[ " $partes " == *" sinffmpeg "* ]]; then
  sec "sinffmpeg (sin ffmpeg: lo de XP)"
  d=$(datos sinffmpeg "01 m4a" "07 buena"); huellas "$d" > "$W/sinffmpeg-antes.txt"; C=$W/sinffmpeg-wav
  abre sinffmpeg "$d" "7:67" "10:pon 01 m4a.mp3;13:mira;16:pon 01 m4a.mp3" 18 $OS ROCOLA_CONVERTIDAS="$C" ROCOLA_FFMPEG=/no/existe/ffmpeg
  o="$W/sinffmpeg"; f="$o/frec.txt"; b=$(bit "$d")
  sed 's/^/   /' "$o/log.txt"; grep -F '[convierte]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'
  a=$(cobro sinffmpeg "01 m4a.mp3" | cut -d' ' -f1)
  si "sinffmpeg: sale con 0" test "$(cat $o/salio)" = 0
  b2=$(cobro sinffmpeg "01 m4a.mp3" 2 | cut -d' ' -f1)
  si "sinffmpeg: el credito devuelto (de $a, al pedirla otra vez ${b2:-?}) y no se oye (440: $(oye "$f" 440) s)" bash -c "[ '$b2' = '$a' ] && python3 -c 'import sys; sys.exit(0 if $(oye "$f" 440) <= 0.1 else 1)'"
  si "sinffmpeg: danada (la segunda vez no se cobra: $(cobro sinffmpeg '01 m4a.mp3' 2)) y en rocola_incompatibles.txt" bash -c "c='$(cobro sinffmpeg '01 m4a.mp3' 2)'; [ -n \"\$c\" ] && [ \"\${c% *}\" = \"\${c#* }\" ] && grep -q '01 m4a.mp3.*no es un MP3' '$d/rocola_incompatibles.txt'"
  si "sinffmpeg: la bitacora dice por que (sin ffmpeg) y ninguna copia" bash -c "grep -q '01 m4a.mp3: no se pudo (sin ffmpeg' '$b' && [ $(conv "$C") = 0 ]"
  huellas "$d" > "$W/sinffmpeg-despues.txt"
  si "sinffmpeg: el original igual" cmp -s "$W/sinffmpeg-antes.txt" "$W/sinffmpeg-despues.txt"
fi

# ---------------------------------------------------------------- sinsitio: menos de 1 GB libre: lo de XP
if [[ " $partes " == *" sinsitio "* ]]; then
  sec "sinsitio (ROCOLA_CONVERTIDAS en un disco de 40 MB: lo de XP)"
  d=$(datos sinsitio "01 m4a" "07 buena"); huellas "$d" > "$W/sinsitio-antes.txt"; C=$W/chico
  mkdir -p "$C"; mount -t tmpfs -o size=40m tmpfs "$C" || { echo "MAL   sinsitio: no se pudo montar el tmpfs (hace falta root)"; mal=$((mal + 1)); }
  abre sinsitio "$d" "7:67" "10:pon 01 m4a.mp3;13:mira;14:pon 01 m4a.mp3" 16 $OS ROCOLA_CONVERTIDAS="$C"
  o="$W/sinsitio"; f="$o/frec.txt"; b=$(bit "$d")
  sed 's/^/   /' "$o/log.txt"; grep -F '[convierte]' "$b" | sed 's/^[0-9-]* [0-9:]*  /   /'
  a=$(cobro sinsitio "01 m4a.mp3" | cut -d' ' -f1)
  b2=$(cobro sinsitio "01 m4a.mp3" 2)
  si "sinsitio: el credito devuelto y danada (de $a; al pedirla otra vez: ${b2:-?}), no se oye (440: $(oye "$f" 440) s) y en rocola_incompatibles.txt" bash -c "[ '$b2' = '$a $a' ] && python3 -c 'import sys; sys.exit(0 if $(oye "$f" 440) <= 0.1 else 1)' && grep -q '01 m4a.mp3.*no es un MP3' '$d/rocola_incompatibles.txt'"
  si "sinsitio: la bitacora dice por que (sin sitio) y el disco chico sin nada" bash -c "grep -q '01 m4a.mp3: no se pudo (sin sitio' '$b' && [ -z \"\$(ls -A '$C')\" ]"
  umount "$C" 2>/dev/null
  huellas "$d" > "$W/sinsitio-despues.txt"
  si "sinsitio: el original igual" cmp -s "$W/sinsitio-antes.txt" "$W/sinsitio-despues.txt"
fi

# ---------------------------------------------------------------- sinrocolaos: sin ROCOLA_OS, ni se mira (el repaso la rechaza como XP)
if [[ " $partes " == *" sinrocolaos "* ]]; then
  sec "sinrocolaos (sin ROCOLA_OS: la de XP; ni copias ni nada de Rocola OS)"
  d=$(datos sinrocolaos "01 m4a" "07 buena"); C=$W/sinrocolaos-wav
  abre sinrocolaos "$d" "" "" 25 ROCOLA_CONVERTIDAS="$C"
  si "sinrocolaos: sale con 0" test "$(cat $W/sinrocolaos/salio)" = 0
  si "sinrocolaos: ninguna copia, ni bitacora ni rocola_os.txt" bash -c "[ ! -e '$C' ] && [ ! -e '$d/rocola_os_bitacora.txt' ] && [ ! -e '$d/rocola_os.txt' ] && [ ! -e '$d/convertidas' ]"
  si "sinrocolaos: el repaso no la mide (como XP: rocola_volumen.txt sin «01 m4a»; la buena si)" bash -c "! grep -q '01 m4a.mp3' '$d/rocola_volumen.txt' 2>/dev/null && grep -q '07 buena.mp3' '$d/rocola_volumen.txt'"
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; fi
[ $mal = 0 ]
