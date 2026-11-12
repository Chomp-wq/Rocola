#!/bin/bash
# pruebas_carpeta.sh [ETIQUETA]: la carpeta del programa (Nativo/CarpetaPrograma.cs) contra lo que daba la capa (Application.StartupPath de
# Plataforma/Ventanas.Form.cs; si ya no esta, la de bae626e) y contra la regla de WinForms, con JIT y con NativeAOT, desde varias carpetas (la de
# la compilacion, otra carpeta actual, una carpeta con espacios y acentos, un enlace al ejecutable). Se compila fuera del repositorio ($W/carpeta/ETIQUETA).
G=$(dirname "$(readlink -f "$0")"); X="$G/../../.."
W=${W:-/root/rocola-trabajo/port/ventanas}; et="${1:-actual}"; o="$W/carpeta/$et"
rm -rf "$o"; mkdir -p "$o/src/Pruebas/Ventanas/Carpeta" "$o/src/Nativo"
cp "$G/../Carpeta/Program.cs" "$G/../Carpeta/PruebasCarpeta.csproj" "$o/src/Pruebas/Ventanas/Carpeta/"
cp "$X/Nativo/CarpetaPrograma.cs" "$o/src/Nativo/"
# La cuenta de la capa, copiada tal cual de su linea
if [ -f "$X/Plataforma/Ventanas.Form.cs" ]; then capa=$(cat "$X/Plataforma/Ventanas.Form.cs"); de="Plataforma/Ventanas.Form.cs"
else capa=$(git -C "$X" show bae626e:sistema/rocola/Xp/Plataforma/Ventanas.Form.cs); de="bae626e:Plataforma/Ventanas.Form.cs"; fi
linea=$(printf '%s\n' "$capa" | grep -E 'public static string StartupPath \{ get \{ return .*; \} \}' | head -1)
[ -n "$linea" ] || { echo "no encuentro StartupPath en $de"; exit 2; }
expr=$(printf '%s\n' "$linea" | sed -E 's/.*get \{ return (.*); \} \}.*/\1/')
echo "la capa ($de): $expr"
printf '// (escrito por pruebas_carpeta.sh: la cuenta de Application.StartupPath de %s)\nusing System;\nnamespace Rocola { internal static class CapaTransicion { internal static string StartupPath { get { return %s; } } } }\n' "$de" "$expr" > "$o/src/Pruebas/Ventanas/Carpeta/Capa.cs"
cd "$o/src/Pruebas/Ventanas/Carpeta"
dotnet build -c Release -nologo -v q -o "$o/bin" > "$o/compila.txt" 2>&1 || { echo "no compila"; grep -E "error" "$o/compila.txt" | head; exit 2; }
r=0; otra="$o/con espacios y acentos ñá"
corre() { "$@" | tee -a "$o/salida.txt" | grep -v "^PASA"; r=$((r + ${PIPESTATUS[0]})); }
: > "$o/salida.txt"
corre dotnet "$o/bin/PruebasCarpeta.dll" jit-compilacion "$o/bin"
(cd / && corre dotnet "$o/bin/PruebasCarpeta.dll" jit-desde-raiz "$o/bin"; exit $r) || r=$((r + 1))
mkdir -p "$otra"; cp -r "$o/bin" "$otra/jit"
(cd /tmp && corre dotnet "$otra/jit/PruebasCarpeta.dll" jit-espacios "$otra/jit"; exit $r) || r=$((r + 1))
dotnet publish -c Release -r linux-x64 -p:PublishAot=true -nologo -v q -o "$o/aot" > "$o/publica.txt" 2>&1 || { echo "no publica"; tail -5 "$o/publica.txt"; exit 2; }
echo "NativeAOT: $(grep -cE "warning (IL|AOT)" "$o/publica.txt") avisos IL/AOT"
corre "$o/aot/PruebasCarpeta" aot-compilacion "$o/aot"
(cd / && corre "$o/aot/PruebasCarpeta" aot-desde-raiz "$o/aot"; exit $r) || r=$((r + 1))
cp -r "$o/aot" "$otra/aot"
(cd /tmp && corre "$otra/aot/PruebasCarpeta" aot-espacios "$otra/aot"; exit $r) || r=$((r + 1))
mkdir -p "$o/enlace"; ln -sf "$otra/aot/PruebasCarpeta" "$o/enlace/rocola"
(cd / && corre "$o/enlace/rocola" aot-por-enlace "$otra/aot"; exit $r) || r=$((r + 1))
echo "total: $(grep -c '^PASA' "$o/salida.txt") pasan, $(grep -c '^FALLA' "$o/salida.txt") fallan"
rm -rf "$o/bin" "$o/aot" "$otra" "$o/src/Pruebas/Ventanas/Carpeta/obj" "$o/src/Pruebas/Ventanas/Carpeta/bin"
exit $r
