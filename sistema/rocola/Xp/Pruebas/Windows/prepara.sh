#!/bin/bash
# prepara.sh ref|mio : copia el programa compilado (JIT; sin los runtimes de otras plataformas) a W/prog-ref o W/prog-mio
. "$(dirname "$0")/comun.sh"
case "$1" in
  ref) src=$REF/Xp/bin/Release/net10.0 ;;
  mio) src=$ROCOLA/Xp/bin/Release/net10.0 ;;
  *) echo "uso: prepara.sh ref|mio"; exit 1 ;;
esac
dst=$W/prog-$1
rm -rf "$dst"; mkdir -p "$dst/runtimes"
cp "$src"/*.dll "$src"/*.json "$src"/rocola-xp "$dst"/ 2>/dev/null
cp "$src"/*.pdb "$dst"/ 2>/dev/null
cp -r "$src/runtimes/linux-x64" "$dst/runtimes/"
echo "programa en $dst: $(du -sh "$dst" | cut -f1)"
