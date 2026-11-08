#!/bin/bash
# prueba-fuente.sh ref|mio : copia la fuente (la referencia congelada o esta rama) a W/fuente-ref|mio, le pone al dither de 16 bits una semilla
# fija (SOLO para comparar el sonido muestra a muestra: en el programa la semilla es Environment.TickCount; es el unico cambio) y la compila
# para linux-x64 en W/prog-ref-semilla|prog-mio-semilla.
. "$(dirname "$0")/comun.sh"
case "$1" in
  ref) R=$REF ;;
  mio) R=$ROCOLA ;;
  *) echo "uso: prueba-fuente.sh ref|mio"; exit 1 ;;
esac
F=$W/fuente-$1/sistema/rocola
rm -rf "$W/fuente-$1"; mkdir -p "$F/Consola/Pantalla" "$F/Motor"
rsync -a --exclude bin --exclude obj --exclude Pruebas "$R/Xp" "$F/"
cp "$R/Consola/Pantalla/Gl.cs" "$R/Consola/Pantalla/Kms.cs" "$F/Consola/Pantalla/"; cp "$R/Motor/Botones.cs" "$F/Motor/"
sed -i 's/rs = (uint)Environment.TickCount | 1u;/rs = 0x2545F491u;   \/\/ (PRUEBA: semilla fija)/' "$F/Xp/Codigo/Form1.Sonido.cs"
[ "$(grep -c 0x2545F491u "$F/Xp/Codigo/Form1.Sonido.cs")" = 1 ] || { echo "no se encontro la semilla del dither"; exit 1; }
rm -rf "$W/prog-$1-semilla"
(cd "$F/Xp" && dotnet build -c Release -r linux-x64 --self-contained false -o "$W/prog-$1-semilla" 2>&1 | grep -E "error|rror\(s\)" | sort -u | tail -3)
rm -rf "$W/fuente-$1"
du -sh "$W/prog-$1-semilla"
