#!/bin/sh
# Compila tres ejecutables HISTORICOS fijados a commits concretos. Si el historial no esta
# disponible, falla: una referencia tomada de HEAD o una comparacion contra el mismo metodo
# convertirian una regresion real en un falso verde.
D="$(cd "$(dirname "$0")" && pwd)" || exit 1
RAIZ="$(cd "$D/.." && pwd)" || exit 1
PROYECTO="$RAIZ/Rocola/Rocola.csproj"
NAUDIO="$RAIZ/packages/NAudio.1.9.0/lib/net35/NAudio.dll"

if ! command -v git >/dev/null 2>&1 || ! git -C "$RAIZ" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "  No hay historial Git en $RAIZ; no se pueden fabricar referencias historicas fiables."
  exit 1
fi
if [ -n "$(git -C "$RAIZ" rev-parse --show-prefix 2>/dev/null)" ]; then
  echo "  $RAIZ esta dentro de otro repositorio, pero no es su raiz."
  echo "  Se nego usar commits que podrian corresponder a otro proyecto."
  exit 1
fi
if ! command -v mcs >/dev/null 2>&1; then
  echo "  Falta mcs para compilar las referencias historicas."
  exit 1
fi
if [ ! -f "$NAUDIO" ]; then
  echo "  Falta NAudio: $NAUDIO"
  exit 1
fi

if grep -q '<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>' "$PROYECTO" 2>/dev/null; then
  ANTES_BUSQUEDA=e3892d6
  ANTES_DIBUJO=377e89e
  ANTERIOR=cfdf912
  SDK_FLAGS="-sdk:4"
  REF_ID="hd:$ANTES_BUSQUEDA:$ANTES_DIBUJO:$ANTERIOR"
elif grep -q '<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>' "$PROYECTO" 2>/dev/null; then
  ANTES_BUSQUEDA=7f5b443
  ANTES_DIBUJO=705c5d3
  ANTERIOR=dc4a42d
  SDK_FLAGS="-sdk:2 -langversion:5"
  REF_ID="xp:$ANTES_BUSQUEDA:$ANTES_DIBUJO:$ANTERIOR"
else
  echo "  No se reconoce el framework declarado en $PROYECTO"
  exit 1
fi
FUENTES="Rocola/Form1.cs Rocola/Form1.Designer.cs Rocola/Program.cs Rocola/D3D9Star.cs"

TMP_REF="${TMPDIR:-/tmp}/rocola_refs_$$"
mkdir -p "$TMP_REF" || exit 1
limpia_tmp() { rm -rf "$TMP_REF"; }
trap limpia_tmp 0
trap 'exit 2' 1 2 15

saca() { # $1 = commit, $2 = nombre del exe
  commit=$1
  salida=$2
  src="$TMP_REF/src_$salida"
  log="$TMP_REF/$salida.log"
  if ! git -C "$RAIZ" cat-file -e "$commit^{commit}" 2>/dev/null; then
    echo "  Falta el commit historico $commit para $salida"
    return 1
  fi
  mkdir -p "$src" || return 1
  for f in $FUENTES; do
    n=$(basename "$f")
    if ! git -C "$RAIZ" show "$commit:$f" > "$src/$n"; then
      echo "  No se pudo extraer $f del commit $commit"
      return 1
    fi
  done
  if ! mcs $SDK_FLAGS -warn:4 -warnaserror+ -target:winexe -out:"$TMP_REF/$salida" \
       -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$NAUDIO" "$src"/*.cs > "$log" 2>&1; then
    echo "  No compila sin avisos $salida (commit $commit):"
    tail -8 "$log"
    return 1
  fi
  if grep -q 'warning CS' "$log"; then
    echo "  $salida produjo avisos y no sirve como referencia fiable:"
    grep 'warning CS' "$log" | head -5
    return 1
  fi
  [ -s "$TMP_REF/$salida" ] || { echo "  $salida quedo vacio"; return 1; }
  echo "  hecho  $salida  (commit $commit)"
}

saca "$ANTES_BUSQUEDA" rocola_ref.exe || exit 1
saca "$ANTES_DIBUJO" rocola_prev.exe || exit 1
saca "$ANTERIOR" rocola_ant.exe || exit 1

# El manifiesto se publica AL FINAL. Si una copia falla, queda ausente y todo.sh se niega a usar
# una mezcla de ejecutables viejos y nuevos en la proxima corrida.
rm -f "$D/referencias.version" || exit 1
for f in rocola_ref.exe rocola_prev.exe rocola_ant.exe; do
  cp -f "$TMP_REF/$f" "$D/$f.nuevo" || exit 1
done
for f in rocola_ref.exe rocola_prev.exe rocola_ant.exe; do
  mv -f "$D/$f.nuevo" "$D/$f" || exit 1
done
printf '%s\n' "$REF_ID" > "$D/referencias.version.nuevo" || exit 1
mv -f "$D/referencias.version.nuevo" "$D/referencias.version" || exit 1
