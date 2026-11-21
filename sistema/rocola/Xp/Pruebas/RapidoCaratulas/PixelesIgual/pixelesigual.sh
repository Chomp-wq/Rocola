#!/bin/bash
# pixelesigual.sh [BASE] : pixeles-igual (Prueba.cs) en NativeAOT, Nativo/Pixeles.cs de ahora contra el de BASE (de fabrica 85c931b, la base
# de rapido-caratulas). Trabaja en PIXELESIGUAL (de fabrica /root/rocola-trabajo/fase11/rapido-caratulas/pixelesigual). Sale con 0 si todo es igual.
set -u
BASE=${1:-85c931b}; S=${PIXELESIGUAL:-/root/rocola-trabajo/fase11/rapido-caratulas/pixelesigual}
X=$(cd "$(dirname "$(readlink -f "$0")")/../../.." && pwd); P=$X/Pruebas/RapidoCaratulas/PixelesIgual
rm -rf $S; mkdir -p $S/viejo
git -C $X show $BASE:sistema/rocola/Xp/Nativo/Pixeles.cs | sed 's/^namespace Rocola\.Nativo$/namespace Rocola.NativoViejo/' > $S/viejo/Pixeles.cs
(cd $P && dotnet publish -c Release -r linux-x64 -nologo -v q -p:Viejo=$S/viejo -p:BaseIntermediateOutputPath=$S/obj/ -o $S/bin 2>&1 | grep -E " error |rror\(s\)|warn|IL[0-9]")
rm -rf $S/obj $P/bin
taskset -c ${CPU:-1} $S/bin/PixelesIgual; r=$?
exit $r
