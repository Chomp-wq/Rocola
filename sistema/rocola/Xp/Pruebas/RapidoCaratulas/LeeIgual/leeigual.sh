#!/bin/bash
# leeigual.sh [BASE] : lee-igual (Prueba.cs) con las imagenes raras (raras.py) y todas las portadas de una lista (LISTA, opcional), contra
# Imagenes.cs y Mapas.cs de BASE (de fabrica 85c931b, la base de rapido-caratulas). Trabaja en LEEIGUAL (de fabrica /root/rocola-trabajo/fase11/rapido-caratulas/leeigual).
set -u
BASE=${1:-85c931b}; S=${LEEIGUAL:-/root/rocola-trabajo/fase11/rapido-caratulas/leeigual}
X=$(cd "$(dirname "$(readlink -f "$0")")/../../.." && pwd); P=$X/Pruebas/RapidoCaratulas/LeeIgual
rm -rf $S; mkdir -p $S/viejo $S/raras
for f in Imagenes Mapas; do git -C $X show $BASE:sistema/rocola/Xp/Nativo/$f.cs | sed 's/^namespace Rocola\.Nativo$/namespace Rocola.NativoViejo/' > $S/viejo/$f.cs; done
python3 -I $P/raras.py $S/raras > /dev/null
ls $S/raras/* > $S/lista.txt; [ -n "${LISTA:-}" ] && cat "$LISTA" >> $S/lista.txt
(cd $P && dotnet build -c Release -nologo -v q -p:Viejo=$S/viejo -p:BaseIntermediateOutputPath=$S/obj/ -o $S/bin 2>&1 | grep -E " error |rror\(s\)|warn")
dotnet $S/bin/LeeIgual.dll $S/lista.txt; r=$?
rm -rf $S/obj; exit $r
