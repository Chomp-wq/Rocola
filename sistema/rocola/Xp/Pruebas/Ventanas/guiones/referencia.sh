#!/bin/bash
# referencia.sh: arma $W/ref-trazas, una copia de trabajo de la referencia congelada (/root/rocola-trabajo/referencia-xp, aef2211: nunca
# se toca) con los ganchos de las trazas SOLO en su Plataforma (../ganchos-referencia.patch: TrazaVentanas.cs, VolcadoEstado.cs y los
# apuntes en Ventanas.*.cs), y la compila (JIT). El programa queda en $W/ref-trazas/sistema/rocola/Xp/bin/Release/net10.0.
G=$(dirname "$(readlink -f "$0")")
W=${W:-/root/rocola-trabajo/port/ventanas}; REF=${REF:-/root/rocola-trabajo/referencia-xp}; D=$W/ref-trazas
rm -rf "$D"; mkdir -p "$D/sistema/rocola/Consola/Pantalla" "$D/sistema/rocola/Motor"
( cd "$REF" && tar cf - --exclude=bin --exclude=obj sistema/rocola/Xp ) | ( cd "$D" && tar xf - )
cp "$REF/sistema/rocola/Consola/Pantalla/Gl.cs" "$REF/sistema/rocola/Consola/Pantalla/Kms.cs" "$D/sistema/rocola/Consola/Pantalla/"
cp "$REF/sistema/rocola/Motor/Botones.cs" "$D/sistema/rocola/Motor/"
( cd "$D" && patch -p1 -s < "$G/../ganchos-referencia.patch" ) || { echo "el parche no entra"; exit 1; }
( cd "$D/sistema/rocola/Xp" && dotnet build -c Release -nologo -v q 2>&1 | grep -E "error|Warn|Advert|Compil" | tail -3 )
ls -la "$D/sistema/rocola/Xp/bin/Release/net10.0/rocola-xp.dll"
