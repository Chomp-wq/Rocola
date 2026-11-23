#!/bin/bash
# LA PRUEBA DE LA CUENTA DE LA SEMANA DEL MODO ATRACCION (RocolaOs.AtraccionSemanaLogica.cs): compila ese archivo con Prueba.cs y la corre.
#   bash prueba.sh        (usa un directorio aparte: W, por defecto /tmp/atraccion-semana)
set -eu
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd); W=${W:-/tmp/atraccion-semana}
mkdir -p "$W"; cp "$G/Prueba.cs" "$W/"; cp "$XP/RocolaOs/RocolaOs.AtraccionSemanaLogica.cs" "$W/"
cat > "$W/p.csproj" <<'X'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><InvariantGlobalization>true</InvariantGlobalization><Nullable>disable</Nullable><ImplicitUsings>disable</ImplicitUsings></PropertyGroup></Project>
X
cd "$W" && dotnet run -c Release 2>&1 | grep -v "^$"
