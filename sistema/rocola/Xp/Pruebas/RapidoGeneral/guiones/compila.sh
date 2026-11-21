#!/bin/bash
# compila.sh ETIQUETA FUENTE [aot|jit|ambos]: el programa de medir (Pruebas/RapidoGeneral) de la carpeta Xp FUENTE en W/ETIQUETA-{jit,aot}
set -u
E=$1; F=$2; Q=${3:-aot}; W=/root/rocola-trabajo/fase11/rapido-general
P=/home/user/Rocola/.claude/worktrees/rapido-general/sistema/rocola/Xp/Pruebas/RapidoGeneral
OBJ=$W/obj-$E
cd $P
flock -o /root/rocola-trabajo/fase11/pesado.lock bash -c "
  if [ $Q != aot ]; then rm -rf $W/$E-jit; dotnet build -c Release -nologo -v q -p:Fuente=$F -p:MideBase=${MIDEBASE:-0} ${EXTRAPROP:-} -p:BaseIntermediateOutputPath=$OBJ/ -o $W/$E-jit 2>&1 | grep -E ' error |rror\(s\)|arning\(s\)'; fi
  if [ $Q != jit ]; then rm -rf $W/$E-aot; dotnet publish -c Release -r linux-x64 -nologo -v q -p:Fuente=$F -p:MideBase=${MIDEBASE:-0} ${EXTRAPROP:-} -p:BaseIntermediateOutputPath=$OBJ/ -o $W/$E-aot 2>&1 | grep -E ' error |rror\(s\)|arning|IL[0-9]'; fi
"
rm -rf $OBJ $P/bin; [ "${DBG:-0}" = 1 ] || rm -f $W/$E-aot/*.dbg
ls -la $W/$E-aot/rocola-xp 2>&1 | tail -1
