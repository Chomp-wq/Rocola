#!/bin/sh
# Antes de compilar (horas): de donde baja Buildroot las fuentes de Rocola OS y si se llega a cada sitio. Uso: sh sistema/comprueba-red.sh
# Si un sitio no responde, Buildroot prueba despues su copia de respaldo (sources.buildroot.net): con llegar a esa basta.
set -e
SISTEMA=$(cd "$(dirname "$0")" && pwd)
TRABAJO=${ROCOLA_TRABAJO:-$HOME/rocola-trabajo}
VERSION=$(sed -n 's/^VERSION=\([^ ]*\).*/\1/p' "$SISTEMA/compila.sh")
mkdir -p "$TRABAJO"
[ -d "$TRABAJO/buildroot" ] || git clone --depth 1 -b "$VERSION" https://gitlab.com/buildroot.org/buildroot.git "$TRABAJO/buildroot"
[ -f "$TRABAJO/salida/.config" ] || make -s -C "$TRABAJO/buildroot" O="$TRABAJO/salida" BR2_EXTERNAL="$SISTEMA" rocola_a4_defconfig >/dev/null
make -s -C "$TRABAJO/buildroot" O="$TRABAJO/salida" show-info 2>/dev/null | python3 -c '
import json, sys
hosts = {}
for v in json.load(sys.stdin).values():
    for dl in v.get("downloads", []):
        for u in dl.get("uris", [])[:1]:
            h = u.split("|")[-1].split("//")[-1].split("/")[0]
            hosts[h] = hosts.get(h, 0) + 1
for h, c in sorted(hosts.items(), key=lambda x: -x[1]): print(c, h)
' > "$TRABAJO/sitios.txt"
falta=0
probar() {
	if curl -s -o /dev/null --max-time 20 "https://$2/"; then printf "  bien    %-32s %s\n" "$2" "$1"; else printf "  NO      %-32s %s\n" "$2" "$1"; falta=1; fi
}
echo "Fuentes por sitio:"
while read -r c h; do probar "$c archivos" "$h"; done < "$TRABAJO/sitios.txt"
probar "copia de respaldo de todo" sources.buildroot.net
if [ $falta -eq 0 ]; then echo "Se llega a todo: sh sistema/compila.sh"; else echo "Faltan sitios: basta con permitir sources.buildroot.net (la copia de respaldo de Buildroot)."; fi
