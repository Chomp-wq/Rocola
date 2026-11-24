#!/bin/sh
# (24-11-2026, revision de mejoras) estado.json de rocola-buzon con un estado de mentira: spotify.vigilante, .rocola y .catalogo (necesita jq)
S=${TMPDIR:-/tmp}/buzon-estado.$$; mkdir -p $S
G=$(cd "$(dirname "$0")/../../../board/rocola/rootfs_overlay/usr/bin" && pwd)/rocola-buzon
B=$S/buzon
rm -rf $B; mkdir -p $B/run/rocola-xp/spotify $B/run/rocola-soloist $B/datos/xp $B/t
printf 'fase=corriendo\ndias=12\ncambio=hoy\npendiente=1\n' > $B/run/rocola-soloist/estado
printf 'sesion=1\ncobradas=3\n' > $B/run/rocola-xp/spotify/estado
printf 'api=ok\npeticiones_60min=14\nultimo_429=2026-11-24 10:00\n' > $B/run/rocola-xp/spotify/catalogo
RUN=$B/run DATOS=$B/datos XPD=$B/datos/xp OS=$B/os XPBIN=/bin/true TMP=$B/t DICE=$B/d
export RUN DATOS XPD OS XPBIN TMP DICE
{ sed -n '/^leer()/p' $G; sed -n '/^cola()/,/^}/p' $G; sed -n '/^rocola_pid()/,/^}/p' $G; sed -n '/^estado_json()/,/^}/p' $G; } > $S/fn.sh
cat $S/fn.sh | head -3 | cut -c1-80
. $S/fn.sh
estado_json | jq -c '.spotify'
rm -rf $S
