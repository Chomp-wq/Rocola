#!/bin/sh
# Compila Rocola OS. Uso: sh sistema/compila.sh [objetivo de make]   (sin nada: todo; p. ej. "menuconfig" o "linux-menuconfig")
# El trabajo (Buildroot, descargas y lo compilado, varios GB) va FUERA del repositorio: $ROCOLA_TRABAJO o ~/rocola-trabajo.
# La imagen queda en $ROCOLA_TRABAJO/salida/images/disk.img
set -e
SISTEMA=$(cd "$(dirname "$0")" && pwd)
TRABAJO=${ROCOLA_TRABAJO:-$HOME/rocola-trabajo}
VERSION=2026.02.3   # Buildroot de soporte largo (las de febrero: un ano de arreglos)
mkdir -p "$TRABAJO"
if [ ! -d "$TRABAJO/buildroot" ]; then
	git clone --depth 1 -b "$VERSION" https://gitlab.com/buildroot.org/buildroot.git "$TRABAJO/buildroot"
fi
export BR2_DL_DIR="$TRABAJO/descargas"
export BR2_CCACHE_DIR="$TRABAJO/ccache"
if [ ! -f "$TRABAJO/salida/.config" ]; then
	make -C "$TRABAJO/buildroot" O="$TRABAJO/salida" BR2_EXTERNAL="$SISTEMA" rocola_a4_defconfig
fi
make -C "$TRABAJO/buildroot" O="$TRABAJO/salida" "$@"
echo "Listo: $TRABAJO/salida/images/disk.img"
