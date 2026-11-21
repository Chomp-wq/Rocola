#!/bin/bash
# compila.sh ETIQUETA FUENTE : el banco (JIT y NativeAOT) de una carpeta Xp, en /root/rocola-trabajo/fase11/rapido-caratulas/ETIQUETA-{jit,aot}
set -u
E=$1; F=$2; W=/root/rocola-trabajo/fase11/rapido-caratulas
P=/home/user/Rocola/.claude/worktrees/rapido-caratulas/sistema/rocola/Xp/Pruebas/RapidoCaratulas
OBJ=$W/obj-$E
rm -rf $W/$E-jit $W/$E-aot
cd $P
flock -o /root/rocola-trabajo/fase11/pesado.lock bash -c "
  dotnet build -c Release -nologo -v q -p:Fuente=$F -p:BaseIntermediateOutputPath=$OBJ/ -o $W/$E-jit 2>&1 | grep -E ' error |rror\(s\)|arning\(s\)'
  dotnet publish -c Release -r linux-x64 -nologo -v q -p:Fuente=$F -p:BaseIntermediateOutputPath=$OBJ/ -o $W/$E-aot 2>&1 | grep -E ' error |rror\(s\)|arning|IL[0-9]'
"
rm -rf $OBJ /home/user/Rocola/.claude/worktrees/rapido-caratulas/sistema/rocola/Xp/Pruebas/RapidoCaratulas/bin
ls -la $W/$E-aot/rocola-xp 2>&1
