#!/bin/sh
# Compila los ejecutables de REFERENCIA con los que se comparan las mejoras.
#   rocola_ref.exe  = ANTES de acelerar la BUSQUEDA          (lo usa la bateria 'buscaigual')
#   rocola_prev.exe = ANTES de acelerar el DIBUJO del buscador  (lo usa 'busmov')
#   rocola_ant.exe  = ANTES de leer la duracion por la CABECERA (lo usa 'disco')
# Los dos van FIJADOS a un commit concreto, uno por rama: si se dejaran en HEAD, en cuanto se sube
# la mejora el "antes" pasaria a ser el "despues" y las comparaciones dejarian de medir nada.
# El directorio del script se resuelve ANTES del cd: si no, "." pasaria a significar el repo y los
# ejecutables de referencia acabarian escritos en la carpeta del codigo.
D="$(cd "$(dirname "$0")" && pwd)"
RAIZ="$(cd "$D/.." && pwd)"
cd "$RAIZ" || exit 1
NAUDIO="$RAIZ/packages/NAudio.1.9.0/lib/net35/NAudio.dll"
RAMA="$(git rev-parse --abbrev-ref HEAD)"
case "$RAMA" in
  rocola-hd) ANTES_BUSQUEDA=e3892d6; ANTES_DIBUJO=377e89e; ANTERIOR=cfdf912; SDK="-sdk:4";                  FUENTES="Rocola/Form1.cs Rocola/Form1.Designer.cs Rocola/Program.cs Rocola/D3D9Star.cs" ;;
  *)         ANTES_BUSQUEDA=7f5b443; ANTES_DIBUJO=705c5d3; ANTERIOR=dc4a42d; SDK="-sdk:2 -langversion:5";   FUENTES="Rocola/Form1.cs Rocola/Form1.Designer.cs Rocola/Program.cs Rocola/D3D9Star.cs" ;;
esac
saca() {   # $1 = commit   $2 = salida
  tmp="$D/../src_$2"
  rm -rf "$tmp"; mkdir -p "$tmp"
  for f in $FUENTES; do
    n="$(basename "$f")"
    git show "$1:$f" > "$tmp/$n" || return 1
  done
  mcs $SDK -warn:4 -target:winexe -out:"$D/$2" \
      -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$NAUDIO" "$tmp"/*.cs || return 1
  echo "  hecho  $2  (rama $RAMA, commit $1)"
}
saca "$ANTES_BUSQUEDA" rocola_ref.exe
saca "$ANTES_DIBUJO"   rocola_prev.exe
saca "$ANTERIOR"       rocola_ant.exe
