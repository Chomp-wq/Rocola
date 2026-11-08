#!/bin/bash
# pruebas_pantalla.sh [ETIQUETA]: las reglas de la pantalla nativa (../Pantalla: Nativo/PantallaRocola.cs, PantallaRocola.Teclado.cs y Reloj.cs del programa, una
# por una), compiladas fuera del repositorio ($W/pantalla/ETIQUETA). Con AOT=1, tambien con NativeAOT.
G=$(dirname "$(readlink -f "$0")")
W=${W:-/root/rocola-trabajo/port/ventanas}; et="${1:-actual}"; o="$W/pantalla/$et"
rm -rf "$o"; mkdir -p "$o/src/Pruebas/Ventanas/Pantalla" "$o/src/Nativo"
cp "$G/../Pantalla/Program.cs" "$G/../Pantalla/PruebasPantalla.csproj" "$o/src/Pruebas/Ventanas/Pantalla/"
cp "$G/../../../Nativo/PantallaRocola.cs" "$G/../../../Nativo/PantallaRocola.Teclado.cs" "$G/../../../Nativo/Reloj.cs" "$o/src/Nativo/"
cd "$o/src/Pruebas/Ventanas/Pantalla"
dotnet build -c Release -nologo -v q -o "$o/bin" > "$o/compila.txt" 2>&1 || { echo "no compila"; grep -E "error" "$o/compila.txt" | head; exit 2; }
timeout 120 dotnet "$o/bin/PruebasPantalla.dll" | tee "$o/salida.txt" | grep -v "^PASA"; r=${PIPESTATUS[0]}
if [ "${AOT:-0}" = 1 ]; then
  dotnet publish -c Release -r linux-x64 -p:PublishAot=true -nologo -v q -o "$o/aot" > "$o/publica.txt" 2>&1 || { echo "no publica"; tail -5 "$o/publica.txt"; exit 2; }
  echo "NativeAOT: $(grep -ci "warn" "$o/publica.txt") lineas con avisos"; timeout 120 "$o/aot/PruebasPantalla" | tee "$o/salida-aot.txt" | grep -v "^PASA"; r=$((r + ${PIPESTATUS[0]}))
fi
rm -rf "$o/bin" "$o/aot" "$o/src/Pruebas/Ventanas/Pantalla/obj" "$o/src/Pruebas/Ventanas/Pantalla/bin"   # (la de NativeAOT deja ahi su compilacion: unos 84 MB)
exit $r
