#!/bin/bash
# banco.sh: el banco por funcion (Pruebas/BancoDibujo, PORT_DIBUJO.md) como lo pide la tanda 2 (el mismo guion del grupo «reposo»): semillas 7
# y 11 con las letras de XP falsas y la 5 con DejaVu, 3 000 casos, los ejecutores gdi y nativo de esta copia contra la capa congelada
# de la referencia. Las salidas, en $BUSCADOR/banco.
set -u
S=${BUSCADOR:-/root/rocola-trabajo/port/buscador-portadas}/banco
B=$(cd "$(dirname "$(readlink -f "$0")")/../BancoDibujo" && pwd)
mkdir -p $S
(cd $B && dotnet build -c Release -nologo -v q Congelado 2>&1 | grep -E " error |rror\(s\)"; dotnet build -c Release -nologo -v q Nuevo 2>&1 | grep -E " error |rror\(s\)")
for s in 7 11 5; do
  if [ $s = 5 ]; then L="SIN_LETRAS=1"; else L="ROCOLA_LETRAS_XP=/root/rocola-trabajo/port/dibujo/letras-xp-falsas"; fi
  env $L dotnet $B/Congelado/bin/Release/net10.0/BancoCongelado.dll casos 3000 semilla $s salida $S/c$s.tsv 2> $S/c$s.err
  for e in gdi nativo; do   # (n7-adelanto: el ejecutor «puente» y puente-objetos se quitaron del banco)
    env $L dotnet $B/Nuevo/bin/Release/net10.0/BancoNuevo.dll casos 3000 semilla $s salida $S/n$s-$e.tsv ejecutor $e 2> $S/n$s-$e.err
    echo "semilla $s, $e: $(wc -l < $S/n$s-$e.tsv) casos, $(diff $S/c$s.tsv $S/n$s-$e.tsv | grep -c '^>') distintos"
  done
done
