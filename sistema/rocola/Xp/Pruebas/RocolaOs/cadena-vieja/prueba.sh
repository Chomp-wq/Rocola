#!/bin/bash
# Pruebas/RocolaOs/cadena-vieja/prueba.sh PROG XP W : la fase 1 de «La cadena vieja borrada» (CADENA_NUEVA.md) fuera de pantalla, JIT (PROG: la carpeta con rocola-xp.dll; XP: sistema/rocola/Xp)
#  a) respaldo: sin /proc/asound y sin ROCOLA_SALIDA_NUEVA_PCM, la salida nueva abre la de ROCOLA_ALSA (la tarjeta con reloj) y una cancion suena
#     por la cadena nueva (lista con cadena_nueva y salida_nueva)
#  b) ROCOLA_OS=1: cadena_nueva y salida_nueva encendidas aunque rocola_os.txt diga 0; los datos viejos a la papelera; el material de la IA apartado
set -u
prog="$1"; XP="$2"; W="$3"
RX=(dotnet "$(cd "$prog" && pwd)/rocola-xp.dll")
rm -rf "$W"; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
export ROCOLA_CLIMA_API=http://127.0.0.1:7 ROCOLA_LUGAR_API=http://127.0.0.1:7/lugar ROCOLA_LETRA_API=http://127.0.0.1:7 ROCOLA_GENIUS_API=http://127.0.0.1:7
export ROCOLA_CATALOGO_URL=http://127.0.0.1:9/catalogo ROCOLA_PORTADAS_CATALOGO=http://127.0.0.1:9
DATOS0=/root/rocola-trabajo/port/ventanas/datos0
AC=$W/alsa; mkdir -p "$AC"
gcc -shared -fPIC -DPIC -O2 -o "$AC/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
M=$W/musica; mkdir -p "$M/Prueba Fase/Disco 44"
ffmpeg -hide_banner -loglevel error -y -f lavfi -i "sine=frequency=440:sample_rate=44100:duration=9" -af "volume=-12dB" -ac 2 -c:a libmp3lame -b:a 192k "$M/Prueba Fase/Disco 44/01 tono 440.mp3" || exit 1
mkdir -p "$W/sin-asound"
escena() {   # escena NOMBRE SEGUNDOS ORDENES VAR=valor...
  local n="$1" seg="$2" ords="$3"; shift 3
  local d="$W/datos-$n" o="$W/$n"; mkdir -p "$o/run"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj  file "%s"  registro "%s" }\n' "$AC/libasound_module_pcm_reloj.so" "$o/sonado.raw" "$o/registro.txt" > "$o/alsa.conf"
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj ROCOLA_ASOUND="$W/sin-asound" ROCOLA_IA_DATOS="$W/ia-$n" \
      ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="4:67,4.3:67,4.6:67" \
      ROCOLA_CAPTURA_SALE=0 ROCOLA_TARJETA_TXT="$o/rocola_tarjeta.txt" "$@" "${RX[@]}" > "$o/salida.txt" 2>&1 ) &
  local p=$! t0=$(date +%s.%N)
  for x in $(echo "$ords" | tr ';' '\n' | sed 's/ /@/g'); do
    [ -z "$x" ] && continue
    local s=${x%%:*} c=${x#*:}; c=${c//@/ }
    python3 -c "import time,sys; t0=float(sys.argv[1]); time.sleep(max(0, t0 + float(sys.argv[2]) - time.time()))" "$t0" "$s"
    echo "$c" >> "$o/run/rocola-xp-salida-nueva-prueba"
  done
  python3 -c "import time,sys; t0=float(sys.argv[1]); time.sleep(max(0, t0 + float(sys.argv[2]) - time.time()))" "$t0" "$seg"
  cp "$o/run/rocola-vivo.txt" "$o/vivo.txt" 2>/dev/null
  kill -TERM $p 2>/dev/null
  ( sleep 40; kill -KILL $p 2>/dev/null ) & local v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
}
prepara() { local d="$W/datos-$1"; rm -rf "$d"; mkdir -p "$d"; cp "$DATOS0/rocola_teclas.txt" "$d/"; cp -r "$M" "$d/musica"; printf 'spotify=0\n%b' "${2:-}" > "$d/rocola_os.txt"; echo "$d"; }

echo "== a) respaldo por ROCOLA_ALSA (sin /proc/asound)"
d=$(prepara respaldo)
escena respaldo 26 "8:pon 01 tono 440.mp3" ROCOLA_OS=cadena_nueva,medida_nueva,salida_nueva,salida_nueva-prueba
o=$W/respaldo
grep -F "[salida_nueva]" "$d/rocola_os_bitacora.txt" | sed 's/^/   bit: /' | cut -c1-230 | head -12
grep -F "[cadena_nueva]" "$d/rocola_os_bitacora.txt" | sed 's/^/   bit: /' | cut -c1-230 | head -6
sed 's/^/   registro: /' "$o/registro.txt" | head -6
si "respaldo: lo dice (RESPALDO ... reloj)" grep -q "RESPALDO: respaldo: la del lanzador (ROCOLA_ALSA) (reloj" "$d/rocola_os_bitacora.txt"
si "respaldo: la abrio la salida nueva una vez" bash -c "[ \$(grep -c 'abierta reloj' '$d/rocola_os_bitacora.txt') = 1 ]"
si "respaldo: la cancion por la cadena nueva (A)" grep -q "\[cadena_nueva\] A 01 tono 440.mp3: ganancia" "$d/rocola_os_bitacora.txt"
si "respaldo: algo sono (sonado.raw de mas de 1 MB)" bash -c "[ \$(stat -c %s '$o/sonado.raw' 2>/dev/null || echo 0) -gt 1000000 ]"
si "respaldo: no se rindio (nada del camino de siempre)" bash -c "! grep -q 'camino de siempre' '$d/rocola_os_bitacora.txt'"

echo "== b) ROCOLA_OS=1: siempre, y lo viejo a la papelera"
d=$(prepara siempre 'cadena_nueva=0\nsalida_nueva=0\n')
printf 'x\n' > "$d/rocola_billar_nivel.txt"; printf 'y\n' > "$d/rocola_billar_bitacora.txt"; printf 'z\n' > "$d/rocola_billar_bitacora.txt.1"
mkdir -p "$d/IAS"; printf 'm\n' > "$d/IAS/rocola_eq_ia.bin"; printf 'g\n' > "$d/IAS/rocola_ia_genero_bolero.bin"; printf 'd\n' > "$d/IAS/rocola_ia.bin"
printf 'v\n' > "$d/rocola_volumen.txt"
mkdir -p "$W/ia-siempre"; head -c 3000000 /dev/zero > "$W/ia-siempre/rocola_ia_datos.bin"; printf 'r\n' > "$W/ia-siempre/respaldo-no-tocar"; printf 'i\n' > "$W/ia-siempre/rocola_ia_datos.idx"
escena siempre 16 "" ROCOLA_OS=1
grep -F "[cadena_nueva]" "$d/rocola_os_bitacora.txt" | sed 's/^/   bit: /' | cut -c1-260 | head -6
si "siempre: la salida nueva arranco aunque salida_nueva=0" grep -q "\[salida_nueva\] arranca" "$d/rocola_os_bitacora.txt"
si "siempre: la papelera se apunto en la bitacora" grep -q "a la papelera de Ctrl+F4" "$d/rocola_os_bitacora.txt"
si "siempre: lo viejo fuera de los datos" bash -c "[ ! -e '$d/rocola_billar_nivel.txt' ] && [ ! -e '$d/rocola_billar_bitacora.txt' ] && [ ! -e '$d/rocola_billar_bitacora.txt.1' ] && [ ! -e '$d/IAS/rocola_eq_ia.bin' ] && [ ! -e '$d/IAS/rocola_ia_genero_bolero.bin' ]"
si "siempre: en la papelera (7 cosas: 3 del billar, 3 modelos y la carpeta IAS)" bash -c "[ \$(ls '$d/papelera' 2>/dev/null | wc -l) = 7 ]"
si "siempre: ni un modelo de IA ni IAS en los datos" bash -c "[ ! -e '$d/IAS' ] && [ ! -e '$d/rocola_ia.bin' ]"
si "siempre: se quedan las tablas que aun se leen" bash -c "[ -e '$d/rocola_volumen.txt' ]"
si "siempre: apuntado (papelera=1)" grep -q "papelera=1" "$d/rocola_cadena_vieja.txt"
mkdir -p "$W/ia-siempre/rocola_miniaturas"
si "siempre: el material de la IA borrado (y lo demas de su carpeta, no)" bash -c "[ ! -e '$W/ia-siempre/rocola_ia_datos.bin' ] && [ ! -e '$W/ia-siempre/rocola_ia_datos.idx' ] && [ -e '$W/ia-siempre/respaldo-no-tocar' ]"
echo "== $bien bien, $mal mal"
