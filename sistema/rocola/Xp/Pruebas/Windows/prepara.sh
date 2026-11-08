#!/bin/bash
# prepara.sh ref|mio|aot : copia el programa compilado (JIT; sin los runtimes de otras plataformas) a W/prog-ref o W/prog-mio, o el de
# NativeAOT de esta rama (dotnet publish -c Release -r linux-x64; sin los .dbg) a W/prog-aot
. "$(dirname "$0")/comun.sh"
case "$1" in
  ref) src=$REF/Xp/bin/Release/net10.0 ;;
  mio) src=$ROCOLA/Xp/bin/Release/net10.0 ;;
  aot) src=$ROCOLA/Xp/bin/Release/net10.0/linux-x64/publish
       rm -rf "$W/prog-aot"; mkdir -p "$W/prog-aot"
       (cd "$src" && find . -type f ! -name "*.dbg" -exec cp --parents {} "$W/prog-aot/" \;)
       echo "programa en $W/prog-aot: $(du -sh "$W/prog-aot" | cut -f1)"; exit 0 ;;
  *) echo "uso: prepara.sh ref|mio|aot"; exit 1 ;;
esac
dst=$W/prog-$1
rm -rf "$dst"; mkdir -p "$dst/runtimes"
cp "$src"/*.dll "$src"/*.json "$src"/rocola-xp "$dst"/ 2>/dev/null
cp "$src"/*.pdb "$dst"/ 2>/dev/null
cp -r "$src/runtimes/linux-x64" "$dst/runtimes/"
echo "programa en $dst: $(du -sh "$dst" | cut -f1)"
