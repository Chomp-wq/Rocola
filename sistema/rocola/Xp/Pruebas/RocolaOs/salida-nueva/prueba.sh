#!/bin/bash
# LA PRUEBA DE LA SALIDA NUEVA (cadena de audio nueva, 27-11-2026; RocolaOs/RocolaOs.SalidaNueva*.cs; CADENA_NUEVA_salida.md). Fuera de pantalla,
# sin sonido de verdad: la tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c: suena a su ritmo y guarda lo que de verdad sono y cada
# apertura, arranque y cierre) y un convertidor de prueba en lazo (lazo.c: recorta desde un nivel y se graba de vuelta), un /proc/asound
# simulado (el A4 con el SMSL PO100 por USB) y un amixer falso. Musica inventada: tonos de 440 Hz (44,1 kHz), 1 kHz (48 kHz) y 2 kHz
# (32 kHz) en MP3 y un video con 3 kHz (48 kHz, AAC).
#   bash prueba.sh PROGRAMA [partes...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   partes (de fabrica todas): guiones elige convertidor flujo cadena mide prueba rendida volumen proteccion hoja sin
#   W (/root/rocola-trabajo/fase11/salida-nueva/prueba; se rehace)
# Las partes:
#   guiones     rocola-tarjeta y rocola-sonido (los del sistema, con sh) con el /proc/asound simulado: lo de antes (plughw:N,M) sigue; por
#               nombre (plughw:CARD=id,DEV=n) con el numero de ahora; la guardada que no esta -> la automatica y lo dice; la USB primero
#               SOLO con salida_nueva=1; «rocola-sonido usar N» por nombre solo con ella; sin «stat»
#   elige       la eleccion de la tarjeta del motor (rocola-xp --prueba-salida-nueva elige): las mismas reglas
#   convertidor la medida del convertidor por la consola (rocola-xp --mide-convertidor con el lazo): recortando desde -10 dBFS propone -11; sin
#               recortar, -1; sin senal y con la entrada que recorta, lo dice
#   flujo       la rocola entera con ROCOLA_OS=salida_nueva: tres canciones (44,1, 48 y 32 kHz), un salto a media cancion y un video, y la
#               medida del convertidor desde la rocola: la tarjeta se abre UNA vez (S32_LE a 44 100 Hz) y no se cierra hasta el final; 0,5 s
#               de ceros al abrir; 24 bits; cada tono a su frecuencia (convertido dentro); el video por el mismo flujo; el control de la
#               tarjeta al 100 % una vez; con frecuencia_salida=44100 en la configuracion de XP, la cadena NO convierte (ni con su limitador
#               detras): la de 48 y la de 32 kHz llegan a la salida nueva a su frecuencia y las convierte ella
#   cadena      la cadena nueva (A, cadena_nueva) con la salida nueva: tres canciones (44,1, 48 y 32 kHz) por la A (coma flotante, sin
#               limitador) a la salida nueva, que las convierte (sin el limitador de XP), una sola apertura, la proteccion sin actuar
#   mide        la medida desde la rocola (con una cancion sonando y el volumen del dueno en -6 dB: la medida va tal cual)
#   prueba      una prueba de sonido sola por el mismo flujo (SalidaNueva.EmpiezaPrueba, la de la hojita del billar): en lugar de la musica;
#               «la musica manda»: una larga se corta sola en cuanto empieza una cancion pedida, que suena (no callada); y el ruido por bandas
#               de la hojita «Billar» de Ctrl+F4 (ROCOLA_OS=1, billar_nuevo=1, el clic en «Tocar las 8 bandas») por ese camino
#   rendida     una tarjeta que no da 44,1 kHz por hw: la salida nueva se rinde y la musica suena por el camino de XP; una que no esta:
#               espera (no se rinde) y la cancion espera al aparato como en XP
#   volumen     los botones de volumen (las teclas multimedia 0xAF/0xAE/0xAD): 1 dB por toque, guardado en rocola_os.txt, el nivel de lo que
#               sono baja lo mismo, el silencio, y amixer no se toca
#   proteccion  con 40 dB de mas (ROCOLA_SALIDA_NUEVA_GANANCIA_DB=40, solo pruebas) la proteccion de 0 dBFS actua, se cuenta y se apunta
#   bitabit     (solo si se nombra, con BASE=carpeta del programa de antes) con salida_nueva=0, lo sonado igual que con el de antes
#   hoja        Ctrl+F4 -> Sonido con ROCOLA_OS=1: el bloque de la salida nueva (capturas), «Encender» guarda y reabre, al volver «Si»
#               (Tab e Intro) la deja y sin contestar vuelve sola a salida_nueva=0
#   sin         sin ROCOLA_OS y con salida_nueva=0: lo de siempre (plughw por cancion, 16 bits, cada una a su frecuencia; la tarjeta se abre y
#               se cierra), sin bitacora de la salida nueva
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd); SIS=$(cd "$XP/../../board/rocola/rootfs_overlay/usr/bin" && pwd)
prog="${1:?uso: prueba.sh PROGRAMA [partes]}"; shift
partes="${*:-guiones elige convertidor flujo cadena mide prueba rendida volumen proteccion hoja sin}"
W=${W:-/root/rocola-trabajo/fase11/salida-nueva/prueba}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}
if [ "${prog#aot:}" != "$prog" ]; then RX=("$(cd "${prog#aot:}" && pwd)/rocola-xp"); else RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll"); fi
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
py() { python3 -c "import sys; sys.exit(0 if ($1) else 1)"; }
export ROCOLA_CLIMA_API=http://127.0.0.1:7 ROCOLA_LUGAR_API=http://127.0.0.1:7/lugar ROCOLA_LETRA_API=http://127.0.0.1:7 ROCOLA_GENIUS_API=http://127.0.0.1:7
export ROCOLA_CATALOGO_URL=http://127.0.0.1:9/catalogo ROCOLA_PORTADAS_CATALOGO=http://127.0.0.1:9

# ---------- el /proc/asound del A4 con el PO100 por USB (tarjeta 2) ----------
A=$W/asound; mkdir -p "$A/card0/pcm3p" "$A/card1/pcm0p" "$A/card1/pcm1p" "$A/card1/pcm0c" "$A/card2/pcm0p"
cat > "$A/cards" <<'E'
 0 [HDMI           ]: HDA-Intel - HDA ATI HDMI
                      HDA ATI HDMI at 0xfeb64000 irq 39
 1 [Generic        ]: HDA-Intel - HD-Audio Generic
                      HD-Audio Generic at 0xfeb60000 irq 40
 2 [PO100          ]: USB-Audio - SMSL USB AUDIO
                      SMSL SMSL USB AUDIO at usb-0000:00:12.0-1, high speed
E
echo HDMI > "$A/card0/id"; echo Generic > "$A/card1/id"; echo PO100 > "$A/card2/id"; echo 262a:1048 > "$A/card2/usbid"
printf 'card: 0\ndevice: 3\nid: HDMI 0\nname: HDMI 0\n' > "$A/card0/pcm3p/info"; : > "$A/card0/eld#0.0"
printf 'card: 1\ndevice: 0\nid: ALC892 Analog\nname: ALC892 Analog\n' > "$A/card1/pcm0p/info"
printf 'card: 1\ndevice: 1\nid: ALC892 Digital\nname: ALC892 Digital\n' > "$A/card1/pcm1p/info"
printf 'Codec: Realtek ALC892\n' > "$A/card1/codec#0"
printf 'card: 2\ndevice: 0\nid: USB Audio\nname: USB Audio\n' > "$A/card2/pcm0p/info"
# (sin el PO100: la tarjeta 2 no esta)
A2=$W/asound-sin-usb; cp -r "$A" "$A2"; rm -rf "$A2/card2"; sed -i '/PO100/,+1d' "$A2/cards"
# (el PO100 con otro numero: el 1, y la placa en el 2)
A3=$W/asound-renumerado; mkdir -p "$A3"; cp -r "$A/card0" "$A3/card0"; cp -r "$A/card2" "$A3/card1"; cp -r "$A/card1" "$A3/card2"
sed -e 's/^ 1 \[Generic/ 2 [Generic/' -e 's/^ 2 \[PO100/ 1 [PO100/' "$A/cards" > "$A3/cards"
# el amixer falso (apunta cada llamada; la tarjeta 2 tiene «PCM»)
mkdir -p "$W/bin"
cat > "$W/bin/amixer" <<'E'
#!/bin/sh
echo "$@" >> "${AMIXER_LOG:-/dev/null}"
case "$*" in *scontents*) printf "Simple mixer control 'PCM',0\n  Capabilities: pvolume pswitch\n  Playback channels: Front Left - Front Right\nSimple mixer control 'Mic',0\n  Capabilities: cvolume\n";; esac
exit 0
E
chmod +x "$W/bin/amixer"
# y el aplay -l falso (para rocola-sonido)
cat > "$W/bin/aplay" <<'E'
#!/bin/sh
printf 'card 0: HDMI [HDA ATI HDMI], device 3: HDMI 0 [HDMI 0]\ncard 1: Generic [HD-Audio Generic], device 0: ALC892 Analog [ALC892 Analog]\ncard 1: Generic [HD-Audio Generic], device 1: ALC892 Digital [ALC892 Digital]\ncard 2: PO100 [SMSL USB AUDIO], device 0: USB Audio [USB Audio]\n'
E
chmod +x "$W/bin/aplay"

# ---------------------------------------------------------------- guiones
if [[ " $partes " == *" guiones "* ]]; then
  # con BusyBox si esta (sus applets delante en el PATH, como en el sistema; aqui el de Ubuntu, no el de Buildroot); si no, el sh de la maquina
  if command -v busybox > /dev/null; then
    mkdir -p "$W/bb"; for a in sh sed grep ls cat head tail sort cut date tr awk pidof killall sleep echo printf rm sync test; do ln -sf "$(command -v busybox)" "$W/bb/$a"; done
    SH="$W/bb/sh"; RUTA="$W/bin:$W/bb"
  else SH=sh; RUTA="$W/bin:$PATH"; fi
  echo "== guiones (rocola-tarjeta y rocola-sonido con $( [ "$SH" = sh ] && readlink -f /bin/sh || busybox | head -1 | cut -d' ' -f1-2))"
  si "los guiones no usan stat (el BusyBox del sistema no lo trae)" bash -c "! grep -nE '(^|[^a-z_])stat ' '$SIS/rocola-tarjeta' '$SIS/rocola-sonido' '$SIS/rocola-mide-dac'"
  D=$W/sis; mkdir -p "$D/xp"
  t() { env ROCOLA_ASOUND="${AS:-$A}" ROCOLA_DATOS="$D" PATH="$RUTA" "$SH" "$SIS/rocola-tarjeta" "$@" 2>"$W/tarjeta.err"; }
  rm -f "$D/rocola_tarjeta.txt" "$D/xp/rocola_os.txt"
  si "sin nada y sin la salida nueva: la placa, como antes ($(t))" test "$(t)" = plughw:1,0
  si "y con -c, su numero ($(t -c))" test "$(t -c)" = 1
  printf 'salida_nueva=1\n' > "$D/xp/rocola_os.txt"
  si "con salida_nueva=1: la USB primero ($(t), -c $(t -c))" bash -c "[ '$(t)' = plughw:2,0 ] && [ '$(t -c)' = 2 ]"
  AS=$A2; si "con salida_nueva=1 y sin la USB: la placa ($(t))" test "$(t)" = plughw:1,0; unset AS
  printf 'salida_nueva=0\n' > "$D/xp/rocola_os.txt"
  si "con salida_nueva=0: la placa otra vez ($(t))" test "$(t)" = plughw:1,0
  echo plughw:0,3 > "$D/rocola_tarjeta.txt"
  si "lo de antes, a mano por numero (plughw:0,3): manda ($(t))" test "$(t)" = plughw:0,3
  echo plughw:CARD=PO100,DEV=0 > "$D/rocola_tarjeta.txt"
  si "por nombre (plughw:CARD=PO100,DEV=0): la tarjeta 2 ($(t), -c $(t -c))" bash -c "[ '$(t)' = plughw:2,0 ] && [ '$(t -c)' = 2 ]"
  AS=$A3; si "por nombre con el PO100 renumerado al 1: la 1 ($(t))" test "$(t)" = plughw:1,0; unset AS
  AS=$A2; r=$(t); si "por nombre sin el PO100 conectado: la automatica ($r) y lo dice ($(cat $W/tarjeta.err))" bash -c "[ '$r' = plughw:1,0 ] && grep -q 'PO100' '$W/tarjeta.err'"; unset AS
  echo hw:CARD=Generic,DEV=1 > "$D/rocola_tarjeta.txt"
  si "por nombre con hw: (hw:CARD=Generic,DEV=1): la 1,1 ($(t))" test "$(t)" = plughw:1,1
  # rocola-sonido usar N (sin que haya rocola abierta: no cierra nada)
  s() { env ROCOLA_ASOUND="$A" ROCOLA_DATOS="$D" ROCOLA_XP_DATOS="$D/xp" ROCOLA_RUN="$W/run-sis" PATH="$RUTA" "$SH" "$SIS/rocola-sonido" "$@" > "$W/sonido.out" 2>&1; }
  mkdir -p "$W/run-sis"
  printf 'salida_nueva=0\n' > "$D/xp/rocola_os.txt"; s usar 4
  si "rocola-sonido usar 4 sin la salida nueva: por numero, como antes ($(cat $D/rocola_tarjeta.txt))" test "$(cat $D/rocola_tarjeta.txt)" = plughw:2,0
  printf 'salida_nueva=1\n' > "$D/xp/rocola_os.txt"; s usar 4
  si "rocola-sonido usar 4 con la salida nueva: por nombre ($(cat $D/rocola_tarjeta.txt))" test "$(cat $D/rocola_tarjeta.txt)" = plughw:CARD=PO100,DEV=0
  si "y rocola-tarjeta lo entiende ($(t))" test "$(t)" = plughw:2,0
  s auto
  si "rocola-sonido auto: borra lo guardado" test ! -e "$D/rocola_tarjeta.txt"
fi

# ---------------------------------------------------------------- elige
if [[ " $partes " == *" elige "* ]]; then
  echo "== elige (la tarjeta del motor de la salida nueva)"
  e() { env ROCOLA_ASOUND="${AS:-$A}" ROCOLA_TARJETA_TXT="$W/elige-tarjeta.txt" ROCOLA_PANTALLA=fuera:64x64 "${RX[@]}" --prueba-salida-nueva elige 2>/dev/null | grep '^ELIGE'; }
  rm -f "$W/elige-tarjeta.txt"
  r=$(e); echo "   $r"; si "automatica: la USB, por nombre (hw:CARD=PO100,DEV=0)" bash -c "echo '$r' | grep -q 'pcm=hw:CARD=PO100,DEV=0 tarjeta=2 usb=1 | automática: la USB | aviso=$'"
  AS=$A2; r=$(e); echo "   $r"; si "sin la USB: la placa (hw:CARD=Generic,DEV=0)" bash -c "echo '$r' | grep -q 'pcm=hw:CARD=Generic,DEV=0 tarjeta=1'"; unset AS
  echo plughw:CARD=Generic,DEV=0 > "$W/elige-tarjeta.txt"
  r=$(e); echo "   $r"; si "guardada por nombre: la placa aunque este la USB" bash -c "echo '$r' | grep -q 'pcm=hw:CARD=Generic,DEV=0 .*la guardada (Generic)'"
  echo plughw:CARD=PO100,DEV=0 > "$W/elige-tarjeta.txt"
  AS=$A3; r=$(e); echo "   $r"; si "guardada por nombre con el PO100 renumerado: hw:CARD=PO100 (tarjeta 1)" bash -c "echo '$r' | grep -q 'pcm=hw:CARD=PO100,DEV=0 tarjeta=1'"; unset AS
  AS=$A2; r=$(e); echo "   $r"; si "guardada por nombre sin conectar: la mejor que hay y el aviso" bash -c "echo '$r' | grep -q 'pcm=hw:CARD=Generic,DEV=0 .*aviso=La tarjeta guardada «PO100» no está conectada'"; unset AS
  echo plughw:1,0 > "$W/elige-tarjeta.txt"
  r=$(e); echo "   $r"; si "la de antes por numero (plughw:1,0): esa, por su nombre" bash -c "echo '$r' | grep -q 'pcm=hw:CARD=Generic,DEV=0 .*la guardada (plughw:1,0)'"
fi

# ---------------------------------------------------------------- convertidor
AC="$W/alsa"; mkdir -p "$AC"
gcc -shared -fPIC -DPIC -O2 -o "$AC/libasound_module_pcm_lazo.so" "$G/lazo.c" -lasound -lm || exit 1
gcc -shared -fPIC -DPIC -O2 -o "$AC/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
if [[ " $partes " == *" convertidor "* ]]; then
  echo "== convertidor (rocola-xp --mide-convertidor con el convertidor de prueba en lazo)"
  printf 'pcm_type.lazo { lib "%s" }\npcm.lazo_sal { type lazo modo salida }\npcm.lazo_ent { type lazo modo entrada }\n' "$AC/libasound_module_pcm_lazo.so" > "$AC/lazo.conf"
  m() { env ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$AC/lazo.conf ROCOLA_PANTALLA=fuera:64x64 "$@" timeout 60 "${RX[@]}" --mide-convertidor --salida lazo_sal --entrada lazo_ent > "$W/mide.txt" 2>&1; echo $? > "$W/mide.salio"; sed -n '/^Salida:/,$p' "$W/mide.txt" | sed 's/^/   /'; }
  m LAZO_RECORTA_DB=-10
  si "recorta desde -10 dBFS: 0, -3 y -6 RECORTAN, -10 y -20 limpios" bash -c "grep -q '^0,0 dBFS: .*RECORTA' '$W/mide.txt' && grep -q '^-3,0 dBFS: .*RECORTA' '$W/mide.txt' && grep -q '^-6,0 dBFS: .*RECORTA' '$W/mide.txt' && grep -q '^-10,0 dBFS: .*limpio' '$W/mide.txt' && grep -q '^-20,0 dBFS: .*limpio' '$W/mide.txt'"
  si "y propone techo_dbtp=-11 (sale con 0)" bash -c "grep -q '^Propuesta: techo_dbtp=-11 ' '$W/mide.txt' && [ \$(cat $W/mide.salio) = 0 ]"
  si "la distorsion a 0 dBFS pasa del 5 % (como la del PROZOR medido por GoldenSound)" bash -c "grep '^0,0 dBFS' '$W/mide.txt' | grep -qE 'THD\+N ([5-9][0-9]?|[1-9][0-9]),[0-9] %'"
  m LAZO_RECORTA_DB=0.5
  si "sin recortar: todo limpio y propone -1" bash -c "[ \$(grep -c ' — limpio' '$W/mide.txt') = 5 ] && grep -q '^Propuesta: techo_dbtp=-1 ' '$W/mide.txt'"
  m LAZO_GANANCIA_DB=-95
  si "sin senal: lo dice (y sale con 3)" bash -c "grep -q 'No llega senal' '$W/mide.txt' && [ \$(cat $W/mide.salio) = 3 ]"
  m LAZO_RECORTA_DB=0.5 LAZO_GANANCIA_DB=3
  si "con la entrada que recorta: lo dice" bash -c "grep -q 'la ENTRADA recorta' '$W/mide.txt' && grep -q 'La entrada de la placa recorta' '$W/mide.txt'"
fi

# ---------------------------------------------------------------- la musica de las escenas
M=$W/musica; mkdir -p "$M/Prueba Salida/Disco 44" "$M/Prueba Salida/Disco 48" "$M/Prueba Salida/Disco 32" "$M/Zeta Videos/Clips"
tono() { ffmpeg -hide_banner -loglevel error -y -f lavfi -i "sine=frequency=$1:sample_rate=$2:duration=$3" -af "volume=-12dB" -ac 2 -c:a libmp3lame -b:a 192k "$4" || exit 1; }
tono 440 44100 9 "$M/Prueba Salida/Disco 44/01 tono 440.mp3"
tono 1000 48000 9 "$M/Prueba Salida/Disco 48/01 tono 1000.mp3"
tono 2000 32000 9 "$M/Prueba Salida/Disco 32/01 tono 2000.mp3"
ffmpeg -hide_banner -loglevel error -y -f lavfi -i "sine=frequency=3000:sample_rate=48000:duration=8" -f lavfi -i "color=c=blue:s=160x120:r=25:d=8" -shortest \
  -af "volume=-20dB" -ac 2 -c:v libx264 -pix_fmt yuv420p -c:a aac -b:a 192k "$M/Zeta Videos/Clips/video 3000.mp4" || exit 1
datos() {   # la carpeta de datos de una escena: la musica y los botones de XP; rocola_os.txt con lo que se pida
  local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp "$DATOS0/rocola_teclas.txt" "$d/"; cp -r "$M" "$d/musica"
  { printf 'spotify=0\n'; printf '%b' "${2:-}"; } > "$d/rocola_os.txt"; echo "$d"
}
# escena NOMBRE DATOS SEGUNDOS "ORDENES" [VAR=valor...]: la rocola con la tarjeta con reloj (formato y aperturas en registro.txt, lo sonado en
# sonado.raw); ORDENES: «segundo:orden;segundo:orden» al doble (rocola-xp-salida-nueva-prueba); las monedas a los 4 s
escena() {
  local n="$1" d="$2" seg="$3" ords="$4"; shift 4
  local o="$W/$n"; mkdir -p "$o/run"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\npcm_type.lazo { lib "%s" }\npcm.lazo_sal { type lazo modo salida }\npcm.lazo_ent { type lazo modo entrada }\n' \
    "$AC/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" "$AC/libasound_module_pcm_lazo.so" > "$o/alsa.conf"
  : > "$o/amixer.txt"
  ( cd "$d" && exec env PATH="$W/bin:$PATH" AMIXER_LOG="$o/amixer.txt" ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ROCOLA_ASOUND="$A" \
      ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="4:67,4.3:67,4.6:67,4.9:67,5.2:67,5.5:67" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TARJETA_TXT="$o/rocola_tarjeta.txt" "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$! t0=$(date +%s.%N) ya=0
  for x in $(echo "$ords" | tr ';' '\n' | sed 's/ /@/g'); do
    local s=${x%%:*} c=${x#*:}; c=${c//@/ }
    python3 -c "import time,sys; t0=float(sys.argv[1]); time.sleep(max(0, t0 + float(sys.argv[2]) - time.time()))" "$t0" "$s"
    echo "$c" >> "$o/run/rocola-xp-salida-nueva-prueba"
  done
  python3 -c "import time,sys; t0=float(sys.argv[1]); time.sleep(max(0, t0 + float(sys.argv[2]) - time.time()))" "$t0" "$seg"
  cp "$o/run/rocola-vivo.txt" "$o/vivo.txt" 2>/dev/null
  kill -TERM $p 2>/dev/null
  ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
  cp "$o/run/rocola-xp-salida-nueva-prueba.log" "$o/ordenes.log" 2>/dev/null
}
bit() { grep -F "[salida_nueva]" "$1/rocola_os_bitacora.txt" 2>/dev/null; }

# ---------------------------------------------------------------- flujo
if [[ " $partes " == *" flujo "* ]]; then
  echo "== flujo (la tarjeta siempre abierta: tres canciones de 44,1, 48 y 32 kHz, un salto, un video y la medida)"
  d=$(datos flujo 'salida_nueva=1\n'); printf 'frecuencia_salida=44100\r\nnoche=0\r\n' > "$d/rocola_config.txt"   # (sin el modo noche: el nivel del video no depende de la hora)
  escena flujo "$d" 48 "8:pon 01 tono 440.mp3;8.3:pon 01 tono 1000.mp3;8.6:pon 01 tono 2000.mp3;8.9:pon video 3000.mp4;22:salta;24:mira;35:mira;46:mira" \
    ROCOLA_OS=salida_nueva,salida_nueva-prueba,noche_video ROCOLA_SALIDA_NUEVA_PCM=reloj ROCOLA_SALIDA_NUEVA_TARJETA=2
  o=$W/flujo
  bit "$d" | sed 's/^/   bit: /' | cut -c1-230
  sed 's/^/   ordenes: /' "$o/ordenes.log" | cut -c1-300
  sed 's/^/   registro: /' "$o/registro.txt" | head -12
  python3 "$G/analiza.py" "$o/sonado.raw" s32 44100 > "$o/analisis.txt"; sed 's/^/   analisis: /' "$o/analisis.txt"
  si "flujo: sale con 0" test "$(cat $o/salio)" = 0
  si "flujo: la tarjeta se abrio UNA vez (un PREPARA y un ARRANCA en todo) y se cerro solo al salir" \
    bash -c "[ \$(grep -c ' PREPARA ' '$o/registro.txt') = 1 ] && [ \$(grep -c ' ARRANCA ' '$o/registro.txt') = 1 ] && [ \$(grep -c ' CIERRA ' '$o/registro.txt') -le 1 ] && ! grep -q XRUN '$o/registro.txt'"
  si "flujo: S32_LE (24 bits) a 44 100 Hz, dos canales (todas las lineas del registro)" bash -c "! grep -v 'rate=44100 ch=2 fmt=10 ' '$o/registro.txt' | grep -q ."
  si "flujo: la bitacora: abierta (aqui la de prueba, reloj) en S32_LE a 44100 Hz, apertura 1, y ninguna otra" \
    bash -c "grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'abierta reloj: S32_LE (24 bits) a 44100 Hz.*apertura 1\$' && [ \$(grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -c '\] abierta ') = 1 ]"
  si "flujo: 0,5 s de ceros de verdad al abrir ($(sed -n 's/^CEROS //p' $o/analisis.txt) s)" py "$(sed -n 's/^CEROS //p' $o/analisis.txt) >= 0.5"
  si "flujo: 24 bits de verdad (los 8 de abajo en 0)" grep -q '^BITS24 si' "$o/analisis.txt"
  for f in 440 1000 2000 3000; do
    si "flujo: el tono de $f Hz sono a su frecuencia (convertido dentro: $(grep "TRAMO f=$f " $o/analisis.txt | head -1))" grep -qE "^TRAMO f=($((f - 3))|$((f - 2))|$((f - 1))|$f|$((f + 1))|$((f + 2))|$((f + 3))) " "$o/analisis.txt"
  done
  si "flujo: el de 1 kHz se corto con el salto (menos de 8 s)" bash -c "awk '/^TRAMO f=(999|1000|1001) /{split(\$3,a,\"=\");split(\$4,b,\"=\"); if (a[2] < 40) d+=b[2]-a[2]} END{exit !(d>2 && d<8)}' '$o/analisis.txt'"
  si "flujo: el control de la tarjeta al 100 % una vez, solo el de reproduccion (amixer: $(grep sset $o/amixer.txt))" bash -c "[ \$(grep -c 'sset PCM,0 100% unmute' '$o/amixer.txt') = 1 ] && [ \$(grep -c sset '$o/amixer.txt') = 1 ]"
  si "flujo: el video por la rocola (la bitacora: su sonido, a 48000 Hz)" bash -c "grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'sonido de un video por la rocola: IeeeFloat 32 bits, 2 canales, 48000 Hz'"
  si "flujo: el video se midio de fondo con ffmpeg (rocola_videos_nivel.txt: $(grep 'video 3000' $d/rocola_videos_nivel.txt | cut -f4,5 | tr '\t' ' ') LUFS y dBTP)" \
    bash -c "awk -F'\t' '/video 3000.mp4/ && \$4+0 < -35 && \$4+0 > -41' '$d/rocola_videos_nivel.txt' | grep -q ."
  # el tono del video: -18 dBFS de lavfi y -20 dB = -38 dBFS de pico (-41 de RMS); medido a -38 LUFS y -18 de objetivo: +20 dB, pero con el tope de +12
  si "flujo: el video con su ganancia fija (a -18 LUFS, con el tope de +12 dB: el tono de 3 kHz a unos -32 dBFS: $(sed -n 's/^TRAMO f=3000 .* db=//p' $o/analisis.txt))" \
    bash -c "grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'video video 3000.mp4: .*ganancia fija 12.0 dB' && awk '/^TRAMO f=3000 /{split(\$5,a,\"=\"); exit !(a[2] > -33 && a[2] < -31)}' '$o/analisis.txt'"
  si "flujo: «mira» dice fuentes y una sola apertura" bash -c "grep -q 'aperturas 1, caidas 0' '$o/ordenes.log'"
  si "flujo: con frecuencia_salida=44100 de XP, la cadena no convierte (ni su limitador): la de 48 y la de 32 kHz llegan a la salida nueva a la suya y las convierte ella" \
    bash -c "grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'cancion a 48000 Hz (IeeeFloat 32 bits): la convierte la salida nueva a 44100 Hz con el sinc de XP, sin limitador' && grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'cancion a 32000 Hz (IeeeFloat 32 bits): la convierte la salida nueva'"
  si "flujo: rocola_error.txt sin fallos de Rocola OS" bash -c "! grep -q 'Rocola OS' '$d/rocola_error.txt' 2>/dev/null"
fi

# ---------------------------------------------------------------- cadena (la cadena nueva A por la salida nueva)
if [[ " $partes " == *" cadena "* ]]; then
  echo "== cadena (la A de la cadena nueva por la salida nueva: 44,1, 48 y 32 kHz; la conversion es la de la salida, sin limitador)"
  d=$(datos cadena 'salida_nueva=1\ncadena_nueva=1\n'); printf 'noche=0\r\n' > "$d/rocola_config.txt"
  escena cadena "$d" 40 "8:pon 01 tono 440.mp3;8.3:pon 01 tono 1000.mp3;8.6:pon 01 tono 2000.mp3;20:mira;37:mira" \
    ROCOLA_OS=salida_nueva,salida_nueva-prueba,cadena_nueva,medida_nueva ROCOLA_SALIDA_NUEVA_PCM=reloj
  o=$W/cadena; b="$d/rocola_os_bitacora.txt"
  grep -E '\[(salida_nueva|cadena_nueva)\]' "$b" | sed 's/^/   bit: /' | cut -c1-230
  python3 "$G/analiza.py" "$o/sonado.raw" s32 44100 > "$o/analisis.txt"; sed 's/^/   analisis: /' "$o/analisis.txt"
  si "cadena: sale con 0" test "$(cat $o/salio)" = 0
  si "cadena: las tres sonaron por la A (la bitacora de la cadena nueva: «] A ...: ganancia»)" bash -c "[ \$(grep -F '[cadena_nueva]' '$b' | grep -c '] A .*: ganancia ') -ge 3 ]"
  si "cadena: la de 48 y la de 32 kHz llegaron a la salida nueva a la suya y las convirtio ella, sin limitador" \
    bash -c "grep -F '[salida_nueva]' '$b' | grep -q 'cancion a 48000 Hz (IeeeFloat 32 bits): la convierte la salida nueva' && grep -F '[salida_nueva]' '$b' | grep -q 'cancion a 32000 Hz (IeeeFloat 32 bits): la convierte la salida nueva'"
  for f in 440 1000 2000; do
    si "cadena: el tono de $f Hz sono a su frecuencia ($(grep "TRAMO f=$f " $o/analisis.txt | head -1))" grep -qE "^TRAMO f=($((f - 3))|$((f - 2))|$((f - 1))|$f|$((f + 1))|$((f + 2))|$((f + 3))) " "$o/analisis.txt"
  done
  si "cadena: la tarjeta, abierta una vez en S32_LE a 44 100 Hz; la proteccion no actuo" \
    bash -c "[ \$(grep -c ' PREPARA ' '$o/registro.txt') = 1 ] && ! grep -v 'rate=44100 ch=2 fmt=10 ' '$o/registro.txt' | grep -q . && grep -q 'aperturas 1, caidas 0' '$o/ordenes.log' && ! grep -q 'proteccion [1-9]' '$o/ordenes.log'"
  si "cadena: rocola_error.txt sin fallos de Rocola OS" bash -c "! grep -q 'Rocola OS' '$d/rocola_error.txt' 2>/dev/null"
fi

# ---------------------------------------------------------------- mide (desde la rocola)
if [[ " $partes " == *" mide "* ]]; then
  echo "== mide (la medida del convertidor desde la rocola: el tono lo da el motor por la salida nueva, que aqui es el lazo que recorta desde -10)"
  d=$(datos mide 'salida_nueva=1\nvolumen_db=-6\n')
  escena mide "$d" 30 "7:pon 01 tono 440.mp3;9:mide;10:mira;27:medida;27.5:mira" ROCOLA_OS=salida_nueva,salida_nueva-prueba ROCOLA_SALIDA_NUEVA_PCM=lazo_sal \
    ROCOLA_SALIDA_NUEVA_ENTRADA=lazo_ent LAZO_RECORTA_DB=-10
  o=$W/mide
  sed 's/^/   ordenes: /' "$o/ordenes.log" | cut -c1-400
  si "mide: sale con 0" test "$(cat $o/salio)" = 0
  si "mide: con una cancion sonando y el volumen del dueno en -6 dB, la medida va tal cual: recorta hasta -6, limpio en -10 y propone -11" \
    bash -c "grep ' medida ' '$o/ordenes.log' | grep -q '0,0 dBFS: .*RECORTA.*-10,0 dBFS: .*limpio.*Propuesta: techo_dbtp=-11 '"
  si "mide: la bitacora lo apunta" bash -c "grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'medida del convertidor: .*techo_dbtp=-11'"
  si "mide: la tarjeta siguio abierta (una apertura)" bash -c "grep ' mira ' '$o/ordenes.log' | tail -1 | grep -q 'aperturas 1, caidas 0'"
fi

# ---------------------------------------------------------------- prueba (una prueba de sonido sola por el mismo flujo)
if [[ " $partes " == *" prueba "* ]]; then
  echo "== prueba (SalidaNueva.EmpiezaPrueba: 3 s de 500 Hz a 48 kHz en lugar de la musica, que sigue callada, y la tarjeta sin cerrarse)"
  d=$(datos prueba 'salida_nueva=1\n')
  escena prueba "$d" 22 "7:pon 01 tono 440.mp3;10:prueba;11.5:prueba-oida;15:prueba-oida;15.5:mira" ROCOLA_OS=salida_nueva,salida_nueva-prueba ROCOLA_SALIDA_NUEVA_PCM=reloj
  o=$W/prueba
  python3 "$G/analiza.py" "$o/sonado.raw" s32 44100 > "$o/analisis.txt"; sed 's/^/   analisis: /' "$o/analisis.txt"
  sed 's/^/   ordenes: /' "$o/ordenes.log" | cut -c1-200
  si "prueba: sale con 0" test "$(cat $o/salio)" = 0
  si "prueba: los 500 Hz (convertidos de 48 kHz) unos 3 s, a -9 dBFS (la mitad del seno)" bash -c "awk '/^TRAMO f=(499|500|501) /{split(\$3,a,\"=\");split(\$4,b,\"=\");split(\$5,c,\"=\"); exit !(b[2]-a[2] > 2.7 && b[2]-a[2] < 3.3 && c[2] > -9.5 && c[2] < -8.5)}' '$o/analisis.txt'"
  si "prueba: la cancion antes y despues, y no mientras (dos tramos de 440 Hz con la prueba en medio)" bash -c "[ \$(grep -c '^TRAMO f=440 ' '$o/analisis.txt') = 2 ]"
  si "prueba: lo oido a la mitad y al acabar (prueba-oida)" bash -c "grep -qE 'prueba-oida (1|0)\.[0-9]+ s sonando' '$o/ordenes.log' && grep -qE 'prueba-oida (2\.9|3\.0)[0-9] s acabo' '$o/ordenes.log'"
  si "prueba: la tarjeta, abierta una vez" bash -c "[ \$(grep -c ' PREPARA ' '$o/registro.txt') = 1 ] && grep -q 'aperturas 1, caidas 0' '$o/ordenes.log'"
  # la musica manda: una prueba de 20 s con la rocola callada; a los 3 s se pide una cancion: la prueba se corta y la cancion se oye
  d=$(datos manda 'salida_nueva=1\n')
  escena manda "$d" 22 "7:prueba-larga;8.5:prueba-oida;10:pon 01 tono 440.mp3;12:prueba-oida;16:mira" ROCOLA_OS=salida_nueva,salida_nueva-prueba ROCOLA_SALIDA_NUEVA_PCM=reloj
  o=$W/manda
  python3 "$G/analiza.py" "$o/sonado.raw" s32 44100 > "$o/analisis.txt"; sed 's/^/   analisis: /' "$o/analisis.txt"
  sed 's/^/   ordenes: /' "$o/ordenes.log" | cut -c1-200
  si "manda: sale con 0" test "$(cat $o/salio)" = 0
  si "manda: la prueba larga se corto sola al empezar la cancion (prueba-oida: acabo con menos de 5 s oidos)" bash -c "grep -qE 'prueba-oida [0-4]\.[0-9]+ s acabo' '$o/ordenes.log'"
  si "manda: la bitacora lo dice («la prueba de sonido se corta: empieza a sonar cancion»)" bash -c "grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'la prueba de sonido se corta: empieza a sonar cancion'"
  si "manda: los 500 Hz menos de 5 s y despues la cancion de 440 Hz se oye (mas de 3 s)" bash -c "awk '/^TRAMO f=(499|500|501) /{split(\$3,a,\"=\");split(\$4,b,\"=\"); p+=b[2]-a[2]} /^TRAMO f=(439|440|441) /{split(\$3,a,\"=\");split(\$4,b,\"=\"); c+=b[2]-a[2]} END{exit !(p>1 && p<5 && c>3)}' '$o/analisis.txt'"
  # el ruido por bandas de la hojita «Billar» de Ctrl+F4 (RocolaOs.Billar2Bandas.cs): por la salida nueva, solo, con la rocola callada; Esc lo para.
  # (1024x768: con la pestana Cadena y la Billar son siete, pegadas a la derecha: Billar en (818,54); «Tocar las 8 bandas» en (276,223))
  d=$(datos billar 'salida_nueva=1\nbillar_nuevo=1\n')
  escena billar "$d" 26 "" ROCOLA_OS=1 ROCOLA_SALIDA_NUEVA_PCM=reloj ROCOLA_PULSA="6:17+115,8:clic:818:54,9.5:clic:276:223,21.5:27,22.5:27" ROCOLA_CAPTURA="13:$W/billar/bandas.png"
  o=$W/billar; b="$d/rocola_os_bitacora.txt"
  grep -E '\[(salida_nueva|billar_nuevo)\]' "$b" | tail -6 | sed 's/^/   bit: /' | cut -c1-220
  r=$(python3 - "$o/sonado.raw" <<'PY'
import sys, struct, math
b = open(sys.argv[1], 'rb').read(); n = len(b) // 8; t = 4410; bloques = []
for k in range(n // t):
    s = 0.0
    for i in range(k * t, (k + 1) * t, 7):
        v = struct.unpack_from('<i', b, i * 8)[0] / 2147483648.0; s += v * v
    db = 10 * math.log10(max(1e-20, s / len(range(k * t, (k + 1) * t, 7))))
    bloques.append(db)
ruido = sum(1 for x in bloques if -28 < x < -12)
print("%.1f %.1f" % (ruido / 10.0, len(bloques) / 10.0))
PY
)
  echo "   billar: segundos con ruido de -20 dB eficaces / segundos sonados: $r"
  si "billar: sale con 0" test "$(cat $o/salio)" = 0
  si "billar: las bandas por la salida nueva, solas («prueba de sonido «billar: las 8 bandas en mono» (sola, ganancia 0.0 dB, con el volumen del dueno)» y «(por la salida nueva)»)" \
    bash -c "grep -F '[salida_nueva]' '$b' | grep -q 'prueba de sonido «billar: las 8 bandas en mono» (sola, ganancia 0.0 dB, con el volumen del dueno)' && grep -q 'suenan las 8 bandas en mono para afinar con el celular (por la salida nueva)' '$b'"
  si "billar: el ruido sono entre 8 y 13 s (dos bandas, la segunda cortada por Esc): $r" bash -c "set -- $r; python3 -c 'import sys; v=float(sys.argv[1]); sys.exit(0 if 8 <= v <= 13 else 1)' \$1"
  si "billar: la tarjeta, abierta una vez (S32_LE a 44 100 Hz)" bash -c "[ \$(grep -c ' PREPARA ' '$o/registro.txt') = 1 ] && ! grep -v 'rate=44100 ch=2 fmt=10 ' '$o/registro.txt' | grep -q ."
  si "billar: la captura de la hojita con la banda que suena" test -s "$o/bandas.png"
fi

# ---------------------------------------------------------------- rendida (si la tarjeta no abre por hw:, la musica no se queda muda)
if [[ " $partes " == *" rendida "* ]]; then
  echo "== rendida (una tarjeta que solo da 48 kHz: la salida nueva se rinde y suena por el camino de siempre; una que aun no esta: espera)"
  d=$(datos rendida 'salida_nueva=1\n')
  escena rendida "$d" 20 "7:pon 01 tono 440.mp3;12:mira" ROCOLA_OS=salida_nueva,salida_nueva-prueba ROCOLA_SALIDA_NUEVA_PCM=lazo_sal LAZO_SOLO_TASA=48000
  o=$W/rendida
  grep -F '[salida_nueva]' "$d/rocola_os_bitacora.txt" | sed 's/^/   bit: /' | cut -c1-200
  si "rendida: se rinde y lo apunta" bash -c "grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'NO abrio la salida nueva (lazo_sal: .*set_rate 44100).*camino de siempre'"
  si "rendida: la cancion suena por el camino de siempre (la tarjeta con reloj: 16 bits)" bash -c "grep -q 'fmt=2 ' '$o/registro.txt' && grep -q 'ARRANCA' '$o/registro.txt'"
  si "rendida: «mira» lo dice" bash -c "grep -q 'mira rendida' '$o/ordenes.log'"
  d=$(datos falta 'salida_nueva=1\n')
  escena falta "$d" 14 "7:pon 01 tono 440.mp3;12:mira" ROCOLA_OS=salida_nueva,salida_nueva-prueba ROCOLA_SALIDA_NUEVA_PCM=no_esta_esta_tarjeta
  o=$W/falta
  si "falta: no se rinde (la tarjeta aun no esta): espera y lo vuelve a intentar" bash -c "grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'aun no abre la tarjeta' && ! grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'NO abrio' && grep -q 'mira sin tarjeta' '$o/ordenes.log'"
  si "falta: la cancion espera al aparato como en XP (no se da por danada ni se devuelve)" bash -c "grep -q 'fallo el dispositivo; se espera y se retoma la misma cancion' '$d/rocola_error.txt' && ! grep -qi 'danada\|incompatib' '$d/rocola_error.txt'"
fi

# ---------------------------------------------------------------- volumen
if [[ " $partes " == *" volumen "* ]]; then
  echo "== volumen (los botones: 1 dB por toque, guardado, y lo que suena baja lo mismo)"
  d=$(datos volumen 'salida_nueva=1\nvolumen_db=-3\n')
  escena volumen "$d" 30 "7:pon 01 tono 440.mp3;27:mira" ROCOLA_OS=salida_nueva,salida_nueva-prueba ROCOLA_SALIDA_NUEVA_PCM=reloj \
    ROCOLA_PULSA="4:67,4.3:67,9:174,9.3:174,9.6:174,9.9:174,10.2:174,10.5:174,12:175,12.3:175,13.6:173,14.8:173"
  o=$W/volumen
  python3 "$G/analiza.py" "$o/sonado.raw" s32 44100 > "$o/analisis.txt"
  python3 - "$o/sonado.raw" > "$o/niveles.txt" <<'E'
import sys, numpy as np
d = np.frombuffer(open(sys.argv[1],'rb').read(), dtype='<i4').reshape(-1,2)[:,0] / 2.0**31
v = 4410
for i in range(len(d)//v):
    s = d[i*v:(i+1)*v]; r = np.sqrt(np.mean(s*s)); print("%.1f %.2f" % (i*0.1, 20*np.log10(max(r,1e-12))))
E
  sed 's/^/   ordenes: /' "$o/ordenes.log" | cut -c1-200
  sed 's/^/   amixer: /' "$o/amixer.txt"
  si "volumen: sale con 0" test "$(cat $o/salio)" = 0
  si "volumen: -3 dB, seis toques abajo y dos arriba: guardado volumen_db=-7 ($(grep volumen_db $d/rocola_os.txt))" grep -qx 'volumen_db=-7' "$d/rocola_os.txt"
  si "volumen: el silencio puesto y quitado (volumen_mudo=0 al final)" grep -qx 'volumen_mudo=0' "$d/rocola_os.txt"
  si "volumen: amixer no se toco para el volumen (ningun «2%»)" bash -c "! grep -q '%[+-]' '$o/amixer.txt'"
  # el nivel: antes de los toques, despues de bajar 6 y de subir 2, y en silencio (la cancion suena de los 7,5 a los 16,5 s)
  nv() { awk -v a=$1 -v b=$2 '$1>=a && $1<=b {s+=$2; n++} END{if(n) printf "%.2f", s/n; else print "nan"}' "$o/niveles.txt"; }
  # (lo sonado empieza con la tarjeta: el tiempo de niveles.txt va 0,2 s detras del de la rocola mas lo que tardo en abrir; se mide por
  # diferencias entre mesetas)
  echo "   niveles: $(awk 'NR%10==1' $o/niveles.txt | tr '\n' ' ' | cut -c1-600)"
  python3 - "$o/niveles.txt" > "$o/mesetas.txt" <<'E'
import sys
L=[tuple(map(float,l.split())) for l in open(sys.argv[1])]
# las mesetas: tramos de 0,5 s o mas con el nivel a +-0,15 dB y sonando (> -80)
m=[]; a=0
for i in range(1,len(L)+1):
    if i==len(L) or abs(L[i][1]-L[a][1])>0.15:
        if i-a>=5 and L[a][1]>-80: m.append((L[a][0], L[i-1][0], sum(x[1] for x in L[a:i])/(i-a)))
        a=i
for x in m: print("%.1f %.1f %.2f" % x)
E
  sed 's/^/   meseta: /' "$o/mesetas.txt"
  si "volumen: el tono baja 6 dB con seis toques y sube 2 con dos (las mesetas del nivel)" python3 - "$o/mesetas.txt" <<'E'
import sys
m=[tuple(map(float,l.split())) for l in open(sys.argv[1])]
v=[x[2] for x in m]
ok=False
for i in range(len(v)-2):
    if abs((v[i]-v[i+1])-6)<0.3 and abs((v[i+2]-v[i+1])-2)<0.3: ok=True
# y el silencio: despues de subir, un tramo callado y otra vez el mismo nivel
ok = ok and len(v) >= 4 and abs(v[3]-v[2]) < 0.3
print("   ", [round(x,2) for x in v]); sys.exit(0 if ok else 1)
E
fi

# ---------------------------------------------------------------- proteccion
if [[ " $partes " == *" proteccion "* ]]; then
  echo "== proteccion (40 dB de mas: la proteccion de 0 dBFS actua y se cuenta)"
  d=$(datos proteccion 'salida_nueva=1\n')
  escena proteccion "$d" 22 "7:pon 01 tono 440.mp3;20:mira" ROCOLA_OS=salida_nueva,salida_nueva-prueba ROCOLA_SALIDA_NUEVA_PCM=reloj ROCOLA_SALIDA_NUEVA_GANANCIA_DB=40
  o=$W/proteccion
  python3 "$G/analiza.py" "$o/sonado.raw" s32 44100 > "$o/analisis.txt"
  bit "$d" | grep -i protec | head -3 | sed 's/^/   bit: /' | cut -c1-200
  si "proteccion: actuo y se apunto (bitacora)" bash -c "grep -F '[salida_nueva]' '$d/rocola_os_bitacora.txt' | grep -q 'ACTUO LA PROTECCION'"
  si "proteccion: «mira» la cuenta (veces y muestras > 0)" bash -c "grep -qE 'proteccion [1-9][0-9]* veces / [1-9][0-9]* muestras' '$o/ordenes.log'"
  si "proteccion: nada paso de 0 dBFS (pico $(sed -n 's/^PICO //p' $o/analisis.txt) dBFS)" py "$(sed -n 's/^PICO //p' $o/analisis.txt) <= 0.0"
fi

# ---------------------------------------------------------------- sin
if [[ " $partes " == *" sin "* ]]; then
  echo "== sin (sin ROCOLA_OS y con salida_nueva=0: lo de siempre)"
  for c in xp:"" apagada:"ROCOLA_OS=salida_nueva-prueba"; do
    n=sin-${c%%:*}; os=${c#*:}
    d=$(datos $n 'salida_nueva=0\n')
    if [ -n "$os" ]; then escena $n "$d" 30 "8:pon 01 tono 440.mp3;8.3:pon 01 tono 1000.mp3" $os ROCOLA_SALIDA_NUEVA_PCM=reloj
    else escena $n "$d" 30 "" ROCOLA_PULSA="4:67,4.3:67" ROCOLA_SALIDA_NUEVA_PCM=reloj; fi
    o=$W/$n
    sed 's/^/   registro: /' "$o/registro.txt" | head -8
    si "$n: sale con 0" test "$(cat $o/salio)" = 0
    si "$n: ni bitacora de la salida nueva ni sus archivos" bash -c "! grep -qF '[salida_nueva]' '$d/rocola_os_bitacora.txt' 2>/dev/null && [ ! -e '$d/rocola_videos_nivel.txt' ] && [ ! -e '$o/run/rocola-xp-video.pcm' ]"
    if [ -n "$os" ]; then
      # (lo de XP: la tarjeta por plughw a 16 bits y a la frecuencia de la primera; la siguiente, de 48 kHz, por el relevo sin pausa convertida a la de
      # la que sonaba; al acabar la cola la salida se cierra)
      python3 "$G/analiza.py" "$o/sonado.raw" s16 44100 > "$o/analisis.txt"; sed 's/^/   analisis: /' "$o/analisis.txt"
      si "$n: por la de siempre: 16 bits (fmt=2), nada en 24 bits, y la salida se cierra al acabar la cola" \
        bash -c "grep -q 'rate=44100 ch=2 fmt=2 ' '$o/registro.txt' && ! grep -q 'fmt=10 ' '$o/registro.txt' && grep -q ' CIERRA ' '$o/registro.txt'"
      si "$n: los dos tonos (440 y 1000 Hz) como antes" bash -c "grep -q '^TRAMO f=440 ' '$o/analisis.txt' && grep -qE '^TRAMO f=(999|1000|1001) ' '$o/analisis.txt'"
    fi
  done
fi

# ---------------------------------------------------------------- hoja (Ctrl+F4 -> Sonido, y encender con la prueba de 15 s)
# (1024x768: la hoja en (29,2) y su cliente en (32,31), como en la prueba salida-audio; la pestana Sonido en (606,54), con la pestana Cadena (antes (712,54)); el bloque de la salida
# nueva: «Encender/Apagar» en (639,616) y «Mas y medir el convertidor...» en (857,616))
if [[ " $partes " == *" hoja "* ]]; then
  echo "== hoja (Ctrl+F4 -> Sonido: el bloque de la salida nueva; encender con «¿Se oye bien?»; sin contestar vuelve sola)"
  d=$(datos hoja)
  difs() { python3 -c "import sys;from PIL import Image,ImageChops;a=Image.open(sys.argv[1]).convert('RGB');b=Image.open(sys.argv[2]).convert('RGB');x,y,w,h=map(int,sys.argv[3:7]);d=ImageChops.difference(a.crop((x,y,x+w,y+h)),b.crop((x,y,x+w,y+h)));print(sum(1 for q in d.getdata() if max(q)>40))" "$@" 2>/dev/null || echo 0; }
  # 1: apagada; la hoja; «Encender (prueba de 15 s)»: se guarda y se reabre sola (SIGUSR1)
  escena hoja-1 "$d" 16 "" ROCOLA_OS=1 ROCOLA_PULSA="4:67,6:17+115,8:clic:606:54,10.5:clic:639:616" ROCOLA_CAPTURA="9.5:$W/hoja-1/apagada.png"
  o=$W/hoja-1
  si "hoja 1: la captura de la hoja con la salida nueva apagada" test -s "$o/apagada.png"
  si "hoja 1: «Encender»: salida_nueva=1 en rocola_os.txt y la prueba (antes=salida_nueva:0, nueva=salida_nueva:1)" \
    bash -c "grep -qx 'salida_nueva=1' '$d/rocola_os.txt' && grep -qx 'antes=salida_nueva:0' '$d/rocola_salida_prueba.txt' && grep -qx 'nueva=salida_nueva:1' '$d/rocola_salida_prueba.txt'"
  si "hoja 1: se reabre conservando el estado (la marca del lanzador y sale con 0)" bash -c "grep -q 'se enciende la salida nueva' '$o/run/rocola-xp-reinicio' && [ \$(cat $o/salio) = 0 ]"
  # 2: como la reabre el lanzador: la salida nueva abre, pregunta y se contesta «Si» (Tab, Intro); despues la hoja y su hoja de «Mas»
  escena hoja-2 "$d" 22 "" ROCOLA_OS=1 ROCOLA_SALIDA_NUEVA_PCM=reloj ROCOLA_PULSA="5:9,5.3:13,8:17+115,10:clic:606:54,13:clic:857:616,17:27,18:27" \
    ROCOLA_CAPTURA="11.5:$W/hoja-2/encendida.png,15.5:$W/hoja-2/mas.png"
  o=$W/hoja-2; b="$d/rocola_os_bitacora.txt"
  grep -F '[salida_audio]' "$b" | tail -3 | sed 's/^/   bit: /' | cut -c1-200
  si "hoja 2: abrio la salida nueva y preguntó; con «Si» queda encendida y sin la prueba" \
    bash -c "grep -F '[salida_nueva]' '$b' | grep -q 'abierta reloj' && grep -qF 'confirmada: salida_nueva:1' '$b' && [ ! -e '$d/rocola_salida_prueba.txt' ] && grep -qx 'salida_nueva=1' '$d/rocola_os.txt'"
  n1=$(difs $W/hoja-1/apagada.png $o/encendida.png 520 470 456 230); n2=$(difs $o/encendida.png $o/mas.png 40 40 900 600)
  si "hoja 2: el bloque cambia con la salida nueva encendida ($n1 puntos)" test "$n1" -gt 500
  si "hoja 2: «Mas y medir el DAC...» abre su hoja ($n2 puntos)" test "$n2" -gt 20000
  si "hoja 2: rocola_error.txt sin fallos de Rocola OS" bash -c "! grep -q 'Rocola OS' '$d/rocola_error.txt' 2>/dev/null"
  # 3: la prueba otra vez y nadie contesta: a los 15 s vuelve sola a salida_nueva=0 y se reabre
  printf '# prueba\nantes=salida_nueva:0\nnueva=salida_nueva:1\narranques=0\n' > "$d/rocola_salida_prueba.txt"
  escena hoja-3 "$d" 26 "" ROCOLA_OS=1 ROCOLA_SALIDA_NUEVA_PCM=reloj ROCOLA_CAPTURA="6:$W/hoja-3/pregunta.png"
  o=$W/hoja-3
  si "hoja 3: nadie contesta: vuelve sola a salida_nueva=0, sin la prueba, y se reabre" \
    bash -c "grep -qx 'salida_nueva=0' '$d/rocola_os.txt' && [ ! -e '$d/rocola_salida_prueba.txt' ] && grep -qF 'sin confirmar salida_nueva:1: vuelve a salida_nueva:0' '$b' && [ -e '$o/run/rocola-xp-reinicio' ]"
  si "hoja 3: la captura de la pregunta" test -s "$o/pregunta.png"
  mkdir -p "$W/capturas"; cp "$W/hoja-1/apagada.png" "$W/hoja-2/encendida.png" "$W/hoja-2/mas.png" "$W/hoja-3/pregunta.png" "$W/capturas/" 2>/dev/null
fi

# ---------------------------------------------------------------- bitabit (con BASE: el programa de antes de la salida nueva)
if [[ " $partes " == *" bitabit "* ]]; then
  echo "== bitabit (con salida_nueva=0, lo que suena es lo de antes: el programa de ahora contra BASE=${BASE:-?}, la misma escena)"
  if [ -z "${BASE:-}" ] || [ ! -f "$BASE/rocola-xp.dll" ]; then echo "FALTA BASE (una carpeta con el rocola-xp.dll de antes de la salida nueva)"; mal=$((mal + 1))
  else
    # (XP mide la biblioteca de fondo: si una cancion se mide antes o despues de prepararla cambia su nivel. Para que las dos corridas sean la
    # misma, primero se deja que el programa de antes la mida entera en unos datos y las dos corren con esa misma foto, en la misma carpeta)
    d=$(datos bit 'salida_nueva=0\n'); RX0=("${RX[@]}"); RX=(dotnet "$BASE/rocola-xp.dll")
    escena bit-calienta "$d" 25 "" ROCOLA_OS=calidad-prueba ROCOLA_PULSA=""
    RX=("${RX0[@]}"); mkdir -p "$W/bit-foto"; cp "$d"/rocola_*.txt "$W/bit-foto/"
    si "bitabit: la foto de la biblioteca ya medida (3 canciones en rocola_sonoridad.txt)" bash -c "[ \$(grep -c 'tono' '$W/bit-foto/rocola_sonoridad.txt') = 3 ]"
    for quien in base ahora; do
      rm -f "$d"/rocola_*.txt*; cp "$W/bit-foto"/rocola_*.txt "$d/"
      o=$W/bit-$quien; mkdir -p "$o/run/rocola-xp/spotify"
      ( sleep 8; echo "pon 01 tono 440.mp3" >> "$o/run/rocola-xp/spotify/prueba-xp"; echo "pon 01 tono 1000.mp3" >> "$o/run/rocola-xp/spotify/prueba-xp"; echo "pon 01 tono 2000.mp3" >> "$o/run/rocola-xp/spotify/prueba-xp" ) &
      if [ $quien = base ]; then RX0=("${RX[@]}"); RX=(dotnet "$BASE/rocola-xp.dll"); fi
      escena bit-$quien "$d" 40 "" ROCOLA_OS=calidad-prueba
      if [ $quien = base ]; then RX=("${RX0[@]}"); fi
      wait
    done
    python3 - "$W/bit-base/sonado.raw" "$W/bit-ahora/sonado.raw" > "$W/bitabit.txt" <<'E'
import sys, numpy as np
a = np.frombuffer(open(sys.argv[1],'rb').read(), dtype='<i2').astype(np.int32).reshape(-1, 2)
b = np.frombuffer(open(sys.argv[2],'rb').read(), dtype='<i2').astype(np.int32).reshape(-1, 2)
# (las dos empiezan cuando abre la tarjeta: se alinean por el primer sonido y el cuadro que mejor casa, +-40)
na = np.flatnonzero(np.abs(a[:, 0]) > 2)[0]; nb = np.flatnonzero(np.abs(b[:, 0]) > 2)[0]
mejor = min((np.abs(a[na:na + 44100, 0] - b[nb + o:nb + o + 44100, 0]).max(), o) for o in range(-40, 41) if nb + o >= 0)
x = a[na:]; y = b[nb + mejor[1]:]; n = min(len(x), len(y))
d = np.abs(x[:n] - y[:n])
print("CUADROS %d (base %d, ahora %d; corrido %d)" % (n, len(a) - na, len(b) - nb, mejor[1]))
print("MAXIMA %d" % d.max())
print("MAS_DE_2 %d" % int((d > 2).sum()))
print("MEDIA %.4f" % float((x[:n] - y[:n]).mean()))
E
    sed 's/^/   /' "$W/bitabit.txt"
    si "bitabit: lo que sono es lo mismo que con el programa de antes (a 2 escalones de 16 bits: los dos tramados TPDF de XP, con su semilla de la hora)" \
      bash -c "grep -q '^MAS_DE_2 0$' '$W/bitabit.txt' && awk '/^CUADROS/ {exit !(\$2 > 1000000)}' '$W/bitabit.txt'"
    si "bitabit: y la tarjeta igual: 16 bits por plughw, las mismas aperturas (registro)" bash -c "[ \"\$(awk '{print \$2, \$7, \$8, \$9}' '$W/bit-base/registro.txt')\" = \"\$(awk '{print \$2, \$7, \$8, \$9}' '$W/bit-ahora/registro.txt')\" ]"
  fi
fi

echo
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; exit 1; fi
