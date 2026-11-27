# LO COMUN DE LA PRUEBA DEL VIDRIO (se carga con «source»): los datos inventados, «corre» y las ayudas. Ver prueba.sh.
set -u
G=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd); XP=$(cd "$G/../../.." && pwd)
W=${W:-/tmp/rocola-cielo-vidrio}; CAPTURAS=${CAPTURAS:-$W/capturas}; PY=${PY:-$W/py}; BASE=${BASE:-$W/base}
mkdir -p "$W" "$CAPTURAS"
bien=${bien:-0}; mal=${mal:-0}
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
rx() { if [ "${1#aot:}" != "$1" ]; then echo "$(cd "${1#aot:}" && pwd)/rocola-xp"; else echo "dotnet $(cd "$1" && pwd)/rocola-xp.dll"; fi; }
AL=$W/alsa; mkdir -p "$AL"
[ -f "$AL/libasound_module_pcm_reloj.so" ] || gcc -shared -fPIC -DPIC -O2 -o "$AL/libasound_module_pcm_reloj.so" "$XP/Pruebas/Windows/alsa/reloj.c" -lasound || exit 1
CIUDAD="19.43,-99.13,Ciudad de México"; SINRED="http://127.0.0.1:9"
LETRAS=${LETRAS:-/nada}
# los datos: la musica inventada (la de las fiestas) y la biblioteca ya armada (calientes)
DATOS=$W/datos-calientes
prepara_datos() {
  [ -f "$DATOS/rocola_biblioteca.txt" ] && return 0
  echo "== datos (una vez)"
  if [ ! -d "$W/datos-frios/musica" ]; then
    bash "$XP/Pruebas/RocolaOs/cielo-fiestas/datos.sh" "$W/datos-frios" > "$W/datos.txt" 2>&1 || { cat "$W/datos.txt"; exit 1; }
  fi
  mkdir -p "$W/calienta/run"
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj }\n' "$AL/libasound_module_pcm_reloj.so" > "$W/calienta.conf"
  ( cd "$W/datos-frios" && env -u ROCOLA_OS ROCOLA_DATOS="$W/datos-frios" ROCOLA_RUN="$W/calienta/run" ROCOLA_ALSA=reloj ROCOLA_PANTALLA=fuera:1024x768 TZ=America/Mexico_City \
      ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$W/calienta.conf ROCOLA_LETRAS_XP="$LETRAS" timeout -s TERM 150 $(rx "$prog") ) > "$W/calienta/salida.txt" 2>&1
  rm -rf "$DATOS"; mkdir -p "$DATOS"; ( cd "$W/datos-frios" && tar cf - --exclude=./musica . ) | ( cd "$DATOS" && tar xf - )
  rm -f "$DATOS/rocola_estado.txt" "$DATOS/rocola_os.txt"
}
MUSICA=$W/datos-frios/musica
# corre NOMBRE PROGRAMA SEGUNDOS [VAR=valor...]: la rocola con una copia de los datos en $W/NOMBRE/datos (con $OSTXT en su rocola_os.txt), /run en
# $W/NOMBRE/run; sale a los SEGUNDOS con SIGTERM; en segundo plano (su PID en $W/NOMBRE/pid).
corre() {
  local n="$1" p="$2" seg="$3"; shift 3
  local o="$W/$n"; rm -rf "$o"; mkdir -p "$o/run" "$o/datos"
  ( cd "$DATOS" && tar cf - . ) | ( cd "$o/datos" && tar xf - ); rm -f "$o/datos/rocola_estado.txt"
  printf 'dir=%s\r\n' "$MUSICA" >> "$o/datos/rocola_config.txt"
  if printf '%s\n' "$@" | grep -qx 'ROCOLA_OS=1'; then printf '%b' "${OSTXT-vidrio=1\n}" > "$o/datos/rocola_os.txt"; fi
  printf 'pcm_type.reloj { lib "%s" }\npcm.reloj { type reloj }\n' "$AL/libasound_module_pcm_reloj.so" > "$o/alsa.conf"
  ( cd "$o/datos" && exec env -u ROCOLA_OS -u ROCOLA_CIELO_FECHA -u ROCOLA_CLIMA -u ROCOLA_CIUDAD ROCOLA_DATOS="$o/datos" ROCOLA_RUN="$o/run" ROCOLA_ALSA=reloj \
      ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:$o/alsa.conf ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_LETRAS_XP="$LETRAS" TZ=America/Mexico_City \
      ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED "$@" timeout -s TERM "$seg" $(rx "$p") ) > "$o/salida.txt" 2>&1 &
  echo $! > "$o/pid"
}
espera() { for n in "$@"; do wait "$(cat "$W/$n/pid")" 2>/dev/null; done; }
bit() { echo "$W/$1/datos/rocola_os_bitacora.txt"; }
lote() { while [ "$(jobs -rp | wc -l)" -ge "${1:-3}" ]; do sleep 2; done; }
