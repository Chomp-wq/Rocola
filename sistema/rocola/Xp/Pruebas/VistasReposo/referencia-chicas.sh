#!/bin/bash
# referencia-chicas.sh DESTINO: una copia de la referencia congelada (/root/rocola-trabajo/referencia-xp, que no se toca) en DESTINO, con el
# volcado de las texturas de su Direct3DaGL.cs para las zonas de CUALQUIER medida (ROCOLA_VOLCADO_MINIMO=n; en la referencia, solo las de
# 64x64 o mas), compilada; deja en DESTINO/prog lo que corre.sh necesita. Solo para las pruebas del grupo «reposo»: las zonas que se suben
# solas (el reloj del panel de reposo, UpdateIdleClockRegion, de 118x45; el recuadro del reloj de la franja, EmpiezaRecuadro, de 134x28) son
# mas chicas que 64 y con el volcado de siempre no se comparan. Con instrumenta-volcado.sh (la traduccion) y ROCOLA_VOLCADO_MINIMO=1 en las
# dos, chicas.py compara las zonas chicas de las dos corridas.
set -eu
DESTINO=$1
REF=/root/rocola-trabajo/referencia-xp/sistema/rocola
rm -rf "$DESTINO"; mkdir -p "$DESTINO/sistema/rocola"
(cd "$REF" && tar -cf - --exclude=bin --exclude=obj --exclude=Pruebas Xp/Codigo Xp/Plataforma Xp/RocolaXp.csproj Consola/Pantalla/Gl.cs Consola/Pantalla/Kms.cs Motor/Botones.cs) | tar -xf - -C "$DESTINO/sistema/rocola"
python3 - "$DESTINO/sistema/rocola/Xp/Plataforma/Direct3DaGL.cs" <<'FIN'
import sys
p = sys.argv[1]; s = open(p, encoding='utf-8').read()
viejo = "if (volcado != null && t.BW >= 64 && t.BH >= 64) Vuelca(t);"
assert s.count(viejo) == 1, "Direct3DaGL.cs de la referencia cambio: revisar"
s = s.replace(viejo, "if (volcado != null && t.BW >= volcadoMinimo && t.BH >= volcadoMinimo) Vuelca(t);")
marca = '            private static readonly string volcado = Environment.GetEnvironmentVariable("ROCOLA_VOLCADO_TEXTURAS");\n'
assert s.count(marca) == 1
s = s.replace(marca, marca + '            private static readonly int volcadoMinimo = int.TryParse(Environment.GetEnvironmentVariable("ROCOLA_VOLCADO_MINIMO"), out int m) ? m : 64;   // (pruebas, copia del grupo reposo)\n')
open(p, 'w', encoding='utf-8').write(s)
FIN
cd "$DESTINO/sistema/rocola/Xp"
dotnet build -c Release -nologo -v q 2>&1 | grep -E " error |rror\(s\)" || true
B=bin/Release/net10.0
mkdir -p "$DESTINO/prog/runtimes"
cp -p $B/*.dll $B/*.json $B/rocola-xp $B/*.pdb "$DESTINO/prog/"; cp -rp $B/runtimes/linux-x64 "$DESTINO/prog/runtimes/"
rm -rf bin obj
echo "listo: $DESTINO/prog"
