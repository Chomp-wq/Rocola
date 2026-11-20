#!/bin/sh
# NOCHE Y VECINOS (20-11-2026): corre el banco de medida del documento AUDIO_NOCHE_VECINOS_MODEST_2026-11-20.md.
#   sh pruebas/nochevecinos.sh [CARPETA_DE_TRABAJO]
# Compila la rocola de XP (perfil sin tarjeta, como pruebas/todo.sh) y nochevecinos.cs en la carpeta de trabajo, pasa cada cancion
# sintetica por la cadena de verdad en cada propuesta, la analiza con nochevecinos.py y BORRA el audio al acabar cada cancion
# (unos 250 MB por cancion mientras dura). Deja analisis.json y medidas.tsv, e imprime las tablas. Hace falta mcs, mono y python3 con numpy.
cd "$(dirname "$0")" || exit 1
RAIZ="$(cd .. && pwd)"
TRABAJO="${1:-${TMPDIR:-/tmp}/rocola_nochevecinos}"
BIN="$TRABAJO/bin"; SAL="$TRABAJO/sal"
mkdir -p "$BIN" "$SAL" || exit 1
NAUDIO="$RAIZ/packages/NAudio.1.9.0/lib/net35/NAudio.dll"
NLAYER="$RAIZ/packages/NLayer.1.12.0/lib/net35/NLayer.dll"
eval "$(grep '^FUENTES=' todo.sh)"
echo "  compilando la rocola y el banco..."
mcs -sdk:2 -langversion:5 -warn:4 -target:winexe -out:"$BIN/rocola.exe" -r:System.Windows.Forms.dll -r:System.Drawing.dll \
    -r:"$NAUDIO" -r:"$NLAYER" -resource:"$NLAYER",Rocola.NLayer.dll $FUENTES > "$BIN/c_rocola.txt" 2>&1 || { tail -5 "$BIN/c_rocola.txt"; exit 1; }
cp "$NAUDIO" "$NLAYER" "$BIN/"
mcs -sdk:4 -warn:4 -r:"$BIN/NAudio.dll" -r:System.Windows.Forms.dll -r:System.Drawing.dll -out:"$BIN/nochevecinos.exe" \
    nochevecinos.cs leercodigo.cs > "$BIN/c_banco.txt" 2>&1 || { tail -5 "$BIN/c_banco.txt"; exit 1; }
rm -f "$SAL/medidas.tsv" "$SAL/analisis.json"
for c in cumbia banda reggaeton rock balada rosa; do
  (cd "$BIN" && mono nochevecinos.exe rocola.exe "$SAL" "$c") > "$SAL/corrida_$c.txt" 2>&1 || { tail -5 "$SAL/corrida_$c.txt"; exit 1; }
  python3 -I "$(pwd)/nochevecinos.py" "$SAL" --borra || exit 1
done
python3 -I "$(pwd)/nochevecinos.py" --tablas "$SAL"
