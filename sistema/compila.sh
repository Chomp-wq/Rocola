#!/bin/sh
# Compila Rocola OS. Uso: sh sistema/compila.sh [objetivo de make]   (sin nada: todo; p. ej. "menuconfig" o "linux-menuconfig")
# El trabajo (Buildroot, descargas y lo compilado, varios GB) va FUERA del repositorio: $ROCOLA_TRABAJO o ~/rocola-trabajo.
# La imagen queda en $ROCOLA_TRABAJO/salida/images/disk.img
#
# ROCOLA_PERFIL=qemu: la misma receta para cualquier x86-64, en $ROCOLA_TRABAJO/salida-qemu. La del A4 lleva instrucciones del
# Piledriver (TBM: blcs, blcfill...) que QEMU no tiene: alli se cae antes de escribir nada. Esta es solo para probar en QEMU.
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
SALIDA="$TRABAJO/salida"
if [ "${ROCOLA_PERFIL:-a4}" = qemu ]; then
	SALIDA="$TRABAJO/salida-qemu"
	if [ ! -f "$SALIDA/.config" ]; then
		mkdir -p "$SALIDA"
		sed 's/^BR2_x86_piledriver=y/BR2_x86_x86_64=y/' "$SISTEMA/configs/rocola_a4_defconfig" > "$SALIDA/rocola_qemu_defconfig"
		make -C "$TRABAJO/buildroot" O="$SALIDA" BR2_EXTERNAL="$SISTEMA" BR2_DEFCONFIG="$SALIDA/rocola_qemu_defconfig" defconfig
	fi
elif [ ! -f "$SALIDA/.config" ]; then
	make -C "$TRABAJO/buildroot" O="$SALIDA" BR2_EXTERNAL="$SISTEMA" rocola_a4_defconfig
fi
make -C "$TRABAJO/buildroot" O="$SALIDA" "$@"
echo "Listo: $SALIDA/images/disk.img"
