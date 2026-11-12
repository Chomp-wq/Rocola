#!/bin/bash
# gl_falla.sh [CARPETA]: arma la libGLESv2 de prueba de la escena f12fallo (gl_falla.c) en CARPETA ($W/gl-falla): una copia de la libGLESv2 de
# Mesa con otro nombre (libGLESr2) y la de prueba, que la envuelve. Se usa con LD_LIBRARY_PATH=CARPETA y F12_FALLA_FLAG=archivo.
G=$(dirname "$(readlink -f "$0")")
W=${W:-/root/rocola-trabajo/port/ventanas}; d="${1:-$W/gl-falla}"
real=$(readlink -f "$(ldconfig -p | awk '/libGLESv2\.so\.2 /{print $NF; exit}')")
mkdir -p "$d" && cp "$real" "$d/libGLESr2.so.2" && LC_ALL=C sed -i 's/libGLESv2\.so\.2/libGLESr2.so.2/g' "$d/libGLESr2.so.2" || exit 2
gcc -shared -fPIC -o "$d/libGLESv2.so.2" "$G/gl_falla.c" -Wl,-soname,libGLESv2.so.2 -L"$d" -Wl,--no-as-needed -l:libGLESr2.so.2 -ldl -Wl,-rpath,'$ORIGIN' || exit 2
echo "$d"
