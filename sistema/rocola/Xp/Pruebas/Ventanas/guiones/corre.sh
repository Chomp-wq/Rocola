#!/bin/bash
# corre.sh PROGRAMA NOMBRE "PULSA" "CAPTURAS" [SEGUNDOS] [VAR=valor ...]
# Corre una rocola de XP (PROGRAMA = carpeta con rocola-xp.dll, JIT; o aot:CARPETA con el ejecutable rocola-xp de NativeAOT) fuera de
# pantalla, con una COPIA NUEVA de los datos de prueba (al cerrar a proposito la rocola borra su estado, como XP), las teclas de PULSA
# (ROCOLA_PULSA) y las capturas de CAPTURAS ("S:nombre,..."). Deja en $SAL/NOMBRE: salida.txt, traza.txt (ROCOLA_TRAZA_VENTANAS),
# las capturas, los estados pedidos con ESTADO="S:nombre,..." (ROCOLA_ESTADO_EN) y los archivos de datos que quedaron (rocola_estado.txt
# solo si sobrevivio, rocola_config.txt, rocola_error.txt, rocola_teclas.txt). Las variables de mas (VAR=valor) van al programa.
#   W       la carpeta de trabajo (fuera del repositorio; por defecto /root/rocola-trabajo/port/ventanas)
#   DATOS0  los datos de prueba de partida (por defecto $W/datos0: copia de los de sistema/rocola/Xp/bin/Release/net10.0)
#   SAL     donde quedan las salidas (por defecto $W/salidas)
#   SALE=0  sin ROCOLA_CAPTURA_SALE (la rocola sigue despues de la ultima captura hasta SEGUNDOS)
#   SENAL   la senal que manda timeout a los SEGUNDOS (TERM, KILL; por defecto TERM, y KILL 30 s despues si no salio)
W=${W:-/root/rocola-trabajo/port/ventanas}
DATOS0=${DATOS0:-$W/datos0}
SAL=${SAL:-$W/salidas}
prog="$1"; nom="$2"; pulsa="$3"; caps="$4"; seg="${5:-40}"; shift 5 2>/dev/null || shift $#
d=$W/corridas/$nom; o=$SAL/$nom
rm -rf "$d" "$o"; mkdir -p "$d" "$o"
cp -r "$DATOS0"/. "$d"/
c=""; IFS=',' read -ra L <<< "$caps"
for x in "${L[@]}"; do [ -z "$x" ] && continue; s="${x%%:*}"; n="${x#*:}"; c="$c,$s:$o/$n.png"; done; c="${c#,}"
e=""; IFS=',' read -ra L <<< "${ESTADO:-}"
for x in "${L[@]}"; do [ -z "$x" ] && continue; s="${x%%:*}"; n="${x#*:}"; e="$e,$s:$o/estado-$n.txt"; done; e="${e#,}"
if [ "${prog#aot:}" != "$prog" ]; then cmd=("${prog#aot:}/rocola-xp"); else cmd=(dotnet "$prog/rocola-xp.dll"); fi
inicio=$(date +%s.%N)
( cd "$d" && env ROCOLA_DATOS="$d" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA="$c" \
    ROCOLA_CAPTURA_SALE=${SALE:-1} ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_ESTADO_EN="$e" "$@" \
    timeout -s ${SENAL:-TERM} -k 30 "$seg" "${cmd[@]}" > "$o/salida.txt" 2>&1; echo "salio con $?" >> "$o/salida.txt" )
fin=$(date +%s.%N)
for f in rocola_estado.txt rocola_config.txt rocola_error.txt rocola_teclas.txt; do [ -f "$d/$f" ] && cp "$d/$f" "$o/$f"; done
echo "duro $(echo "$fin - $inicio" | bc) s" >> "$o/salida.txt"
rm -rf "$d"
tail -2 "$o/salida.txt"
