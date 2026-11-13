#!/bin/bash
# compara.sh NUEVO REF SALIDA -- el arnes de la frontera (Frontera.cs) compilado con la traduccion (carpeta NUEVO) y con la referencia
# congelada (carpeta REF; ver VistasFrontera.csproj), fuera de pantalla, con las letras de XP falsas y sin ellas (DejaVu). (La puerta «puente»,
# el Bitmap de la capa por las sobrecargas del puente, se quito en la oleada 8b, 13-11-2026: iba por la misma SKBitmap que «sk».) Compara lo que escriben linea por linea y las texturas volcadas (por orden, medida,
# zona y huella; sin el numero de OpenGL ni la hora). Deja en SALIDA las salidas y las huellas (los PNG se borran).
set -u
nuevo=$1; ref=$2; sal=$3; mkdir -p "$sal"
LETRAS=${LETRAS:-/root/rocola-trabajo/port/dibujo/letras-xp-falsas}
corre() {   # corre NOMBRE DLL LETRAS [argumentos]
  local d="$sal/$1"; rm -rf "$d"; mkdir -p "$d/tex"
  if [ -n "$3" ]; then export ROCOLA_LETRAS_XP=$3; else unset ROCOLA_LETRAS_XP; fi
  ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_VOLCADO_TEXTURAS="$d/tex" LP_NUM_THREADS=${LP_NUM_THREADS:-2} nice -n 5 dotnet "$2" ${4:-} > "$d/salida.tsv" 2> "$d/error.txt"
  echo "salida $?" >> "$d/error.txt"
  ( cd "$d/tex" && for f in *.png; do [ -f "$f" ] || continue; echo "$(echo "$f" | sed -E 's/^([0-9]+)_[0-9.]+s_([0-9]+x[0-9]+)_n[0-9]+_(en.*)\.png$/\1 \2 \3/') $(md5sum < "$f" | cut -c1-16)"; done ) > "$d/texturas.txt"
  rm -rf "$d/tex"
}
for l in xp dejavu; do
  L=""; [ $l = xp ] && L=$LETRAS
  corre ref-$l "$ref/VistasFrontera.dll" "$L"
  corre sk-$l "$nuevo/VistasFrontera.dll" "$L"
  for v in sk; do
    a=$(diff <(grep -v "^GpuStarfield Ok" "$sal/ref-$l/salida.tsv") <(grep -v "^GpuStarfield Ok" "$sal/$v-$l/salida.tsv") | grep -c '^[<>]')
    t=$(diff "$sal/ref-$l/texturas.txt" "$sal/$v-$l/texturas.txt" | grep -c '^[<>]')
    echo "$v ($l): $(wc -l < "$sal/$v-$l/salida.tsv") lineas, $a distintas de la referencia; $(wc -l < "$sal/$v-$l/texturas.txt") texturas volcadas ($(wc -l < "$sal/ref-$l/texturas.txt") en la referencia), $t lineas distintas; $(tail -1 "$sal/$v-$l/error.txt")"
  done
done
