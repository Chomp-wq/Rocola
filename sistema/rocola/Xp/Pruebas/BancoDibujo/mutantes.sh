#!/bin/bash
# El banco ve los errores: copias del Dibujo2D.cs congelado con un error metido a proposito (uno por copia), compiladas con el banco, y
# cuantos casos salen distintos de la referencia.
W=${ROCOLA_TRABAJO:-/root/rocola-trabajo/port/dibujo}; B=$(cd "$(dirname "$0")" && pwd)
REF=/root/rocola-trabajo/referencia-xp/sistema/rocola/Xp/Plataforma/Dibujo2D.cs
declare -a M
M[1]='s/p.StrokeMiter = 10f;/p.StrokeMiter = 4f;/'
M[2]='s/private float Corrimiento { get { return !Centros ? 0f : Suave ? 0.5f : 15f \/ 32f; } }/private float Corrimiento { get { return !Centros ? 0f : Suave ? 0f : 15f \/ 32f; } }/'
M[3]='s/Math.Min(255, c \* 255 \/ a)/Math.Min(255, (c * 255 + a \/ 2) \/ a)/'
M[4]='s/de = (int)Math.Ceiling(m \* 1.5f); }/de = (int)Math.Ceiling(m * 1.4f); }/'
M[5]='s/public static StringFormat GenericTypographic { get { return genericoTipografico; } }/public static StringFormat GenericTypographic { get { return new StringFormat(genericoTipografico); } }/'
M[6]='s/if (w == WrapMode.Clamp) return SKShaderTileMode.Decal;/if (w == WrapMode.Clamp) return SKShaderTileMode.Clamp;/'
M[7]='s/private const float Holgura = 0.005f;/private const float Holgura = 0.05f;/'
M[8]='s/int ppem = (int)Math.Ceiling(96f \* f.SizeInPoints \/ 72f);/int ppem = (int)Math.Round(96f * f.SizeInPoints \/ 72f);/'
for i in "${!M[@]}"; do
  D=$W/mutantes/m$i; mkdir -p $D/sistema/rocola/Xp/Plataforma
  sed "${M[$i]}" $REF > $D/sistema/rocola/Xp/Plataforma/Dibujo2D.cs
  if cmp -s $REF $D/sistema/rocola/Xp/Plataforma/Dibujo2D.cs; then echo "m$i: el cambio no se aplico"; continue; fi
  (cd $B/Congelado && dotnet build -c Release -p:Referencia=$D -o $D/bin > $D/build.txt 2>&1) || { echo "m$i: no compila"; continue; }
  ROCOLA_LETRAS_XP=$W/letras-xp-falsas dotnet $D/bin/BancoCongelado.dll casos 3000 semilla 7 salida $D/salida.tsv 2>/dev/null
  n=$(paste $W/banco-c7.tsv $D/salida.tsv | awk -F'\t' '$3!=$7 || $4!=$8' | wc -l)
  echo "m$i (${M[$i]:0:60}...): $n de 3000 casos distintos"
done
