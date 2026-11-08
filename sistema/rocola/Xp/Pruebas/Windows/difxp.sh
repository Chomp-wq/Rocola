#!/bin/bash
# difxp.sh ARCHIVO.cs : lo que cambia la traduccion frente a la rocola de XP (Rocola/ARCHIVO.cs), sin lo mecanico (fines de linea, lineas en
# blanco, espacios, los using de arriba)
. "$(dirname "$0")/comun.sh"
norm() { sed -e 's/\r$//' -e '1s/^\xEF\xBB\xBF//' "$1" | grep -v '^using [A-Za-z0-9_.= ]*;\( *//.*\)\?$' | grep -v '^\s*$'; }
diff -u -w <(norm "$ROCOLA/../../Rocola/$1") <(norm "$ROCOLA/Xp/Codigo/$1") | tail -n +3
