#!/bin/bash
# corre.sh PROGRAMA SALIDA [segundos-limite] -- corre la rocola de XP (JIT: rocola-xp.dll; NativeAOT: rocola-xp sin .dll al lado) con
# una COPIA NUEVA de los datos de prueba, fuera de pantalla y sin sonido. Las variables ROCOLA_* del entorno se respetan (ROCOLA_PULSA,
# ROCOLA_CAPTURA, ROCOLA_CINTA, ROCOLA_TRAZA_DIBUJOS...). Deja en SALIDA/registro.txt lo que escribio y borra la carpeta de la corrida
# (salvo CONSERVA=1). CONFIG="clave=valor ...": un rocola_config.txt con esas claves (los ahorros de Performance, etc.).
set -u
. "$(dirname "$(readlink -f "$0")")/comun.sh"
prog=$(readlink -f "$1"); sal=$(readlink -m "$2"); lim=${3:-120}
datos=$REFERENCIA/sistema/rocola/Xp/bin/Release/net10.0
mkdir -p "$sal"
run=$(mktemp -d "$TARJETA/corrida.XXXXXX")
for f in "$prog"/*; do
  b=$(basename "$f")
  case "$b" in
    runtimes) ln -s "$f" "$run/runtimes" ;;
    musica|rocola_*|corrida.*) ;;
    *.so|*.dbg) ln -s "$f" "$run/$b" ;;
    *) cp -r "$f" "$run/" ;;
  esac
done
cp -r "$datos/musica" "$run/"
for f in "$datos"/rocola_*.txt; do case "$f" in *.bak) ;; *) cp "$f" "$run/" ;; esac; done
cp -r "$datos/rocola_miniaturas" "$run/"
if [ -n "${CONFIG:-}" ]; then for kv in $CONFIG; do printf '%s\r\n' "$kv"; done > "$run/rocola_config.txt"; fi
export ROCOLA_PANTALLA=${ROCOLA_PANTALLA:-fuera:1024x768}
export ROCOLA_ALSA=${ROCOLA_ALSA:-null}
cd "$run"
if [ -f "$run/rocola-xp.dll" ]; then cmd=(dotnet "$run/rocola-xp.dll"); else cmd=("$run/rocola-xp"); fi
timeout -k 5 "$lim" "${cmd[@]}" > "$sal/registro.txt" 2>&1
echo "salida $?" >> "$sal/registro.txt"
if [ "${CONSERVA:-0}" = 1 ]; then echo "corrida en $run"; else rm -rf "$run"; fi
