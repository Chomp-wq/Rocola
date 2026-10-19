#!/bin/sh
# Compila una rocola INSTRUMENTADA para las huellas de cuadro (pruebas/huellacuadros.cs):
#   sh pruebas/huella-compila.sh <raiz-del-repositorio> <salida.exe> [simbolos extra de compilacion]
# Copia las fuentes de <raiz>/Rocola a una carpeta temporal, cambia en la COPIA los relojes y el azar por los de
# pruebas/huellareloj.cs (ver ese archivo) y compila el perfil con tarjeta grafica. No toca las fuentes de verdad.
RAIZ_FUENTES="$1"; SALIDA="$2"; EXTRA="$3"
[ -d "$RAIZ_FUENTES/Rocola" ] && [ -n "$SALIDA" ] || { echo "uso: huella-compila.sh <raiz> <salida.exe>"; exit 1; }
AQUI="$(cd "$(dirname "$0")" && pwd)"
TMP="${TMPDIR:-/tmp}/rocola_huella_$$"; rm -rf "$TMP"; mkdir -p "$TMP/Rocola" || exit 1
cp "$RAIZ_FUENTES"/Rocola/*.cs "$TMP/Rocola/"
mkdir -p "$TMP/packages"
cp -r "$RAIZ_FUENTES/packages/NAudio.1.9.0" "$RAIZ_FUENTES/packages/NLayer.1.12.0" "$TMP/packages/" 2>/dev/null \
  || { cp -r "$AQUI/../packages/NAudio.1.9.0" "$AQUI/../packages/NLayer.1.12.0" "$TMP/packages/"; }
for f in "$TMP"/Rocola/*.cs; do
  sed -i \
    -e 's/Environment\.TickCount/Rocola.HuellaReloj.Tick/g' \
    -e 's/\(System\.Diagnostics\.\)\{0,1\}Stopwatch\.GetTimestamp()/Rocola.HuellaReloj.Ticks/g' \
    -e 's/DateTime\.UtcNow/Rocola.HuellaReloj.Ahora/g' \
    -e 's/DateTime\.Now/Rocola.HuellaReloj.Ahora/g' \
    -e 's/new Random()/Rocola.HuellaReloj.NuevoRandom()/g' \
    -e 's/new System\.Random()/Rocola.HuellaReloj.NuevoRandom()/g' "$f"
done
cp "$AQUI/huellareloj.cs" "$TMP/Rocola/HuellaReloj.cs"
# La misma lista de fuentes que pruebas/todo.sh (se lee de alli), apuntada a la copia.
F=$(sed -n '/^FUENTES=/p' "$AQUI/todo.sh" | sed 's/^FUENTES=//; s/"//g' | tr '\n' ' ' | sed "s#\$RAIZ#$TMP#g; s#\$FUENTES##g; s#\.\./Rocola/#$TMP/Rocola/#g")
N="$TMP/packages/NAudio.1.9.0/lib/net35/NAudio.dll"; L="$TMP/packages/NLayer.1.12.0/lib/net35/NLayer.dll"
mcs -sdk:2 -langversion:5 -warn:4 -target:winexe -out:"$SALIDA" -d:ROCOLA_GPU $EXTRA -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$N" -r:"$L" \
    -resource:"$L",Rocola.NLayer.dll $F "$TMP/Rocola/HuellaReloj.cs" > "$TMP/c.txt" 2>&1
rc=$?
[ $rc -ne 0 ] && { echo "NO COMPILA la rocola instrumentada:"; tail -15 "$TMP/c.txt"; }
cp "$N" "$(dirname "$SALIDA")/NAudio.dll" 2>/dev/null
rm -rf "$TMP"
exit $rc
