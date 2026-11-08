#!/bin/bash
# pruebas_hoja.sh [ETIQUETA]: las reglas de la hoja nativa y sus piezas (../Hoja: Nativo/Hoja*.cs, Pieza.cs, Rotulo.cs, BotonXp.cs, ListaXp.cs,
# AvisoXp.cs, SelectorCarpetas.cs y PinturaXp.cs del programa, con la pantalla y el dibujo nativos que usan), compiladas fuera del repositorio
# ($W/hoja/ETIQUETA). Con AOT=1, tambien con NativeAOT.
G=$(dirname "$(readlink -f "$0")")
W=${W:-/root/rocola-trabajo/port/ventanas}; et="${1:-actual}"; o="$W/hoja/$et"
rm -rf "$o"; mkdir -p "$o/src/Pruebas/Ventanas/Hoja" "$o/src/Nativo"
cp "$G/../Hoja/Program.cs" "$G/../Hoja/PruebasHoja.csproj" "$o/src/Pruebas/Ventanas/Hoja/"
for f in PantallaRocola PantallaRocola.Teclado Reloj Tecla Hoja Hoja.Foco Hoja.Entrada Hoja.Pinta Pieza Rotulo BotonXp ListaXp AvisoXp SelectorCarpetas PinturaXp Letras TextoGdi Pixel Degradado Mapas Imagenes; do
  cp "$G/../../../Nativo/$f.cs" "$o/src/Nativo/"; done
cd "$o/src/Pruebas/Ventanas/Hoja"
dotnet build -c Release -nologo -v q -o "$o/bin" > "$o/compila.txt" 2>&1 || { echo "no compila"; grep -E "error" "$o/compila.txt" | head; exit 2; }
timeout 300 dotnet "$o/bin/PruebasHoja.dll" | tee "$o/salida.txt" | grep -v "^PASA"; r=${PIPESTATUS[0]}
if [ "${AOT:-0}" = 1 ]; then
  dotnet publish -c Release -r linux-x64 -p:PublishAot=true -nologo -v q -o "$o/aot" > "$o/publica.txt" 2>&1 || { echo "no publica"; tail -5 "$o/publica.txt"; exit 2; }
  echo "NativeAOT: $(grep -ci "warn" "$o/publica.txt") lineas con avisos"; timeout 300 "$o/aot/PruebasHoja" | tee "$o/salida-aot.txt" | grep -v "^PASA"; r=$((r + ${PIPESTATUS[0]}))
fi
rm -rf "$o/bin" "$o/aot" "$o/src/Pruebas/Ventanas/Hoja/obj" "$o/src/Pruebas/Ventanas/Hoja/bin"
exit $r
