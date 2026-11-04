#!/bin/sh
# Compila Rocola OS. Uso: sh sistema/compila.sh [objetivo de make]   (sin nada: todo; p. ej. "menuconfig" o "linux-menuconfig")
# El trabajo (Buildroot, descargas y lo compilado, varios GB) va FUERA del repositorio: $ROCOLA_TRABAJO o ~/rocola-trabajo.
# La imagen queda en $ROCOLA_TRABAJO/salida/images/disk.img
#
# ROCOLA_PERFIL=qemu: la misma receta para cualquier x86-64, en $ROCOLA_TRABAJO/salida-qemu. La del A4 lleva instrucciones del
# Piledriver (TBM: blcs, blcfill...) que QEMU no tiene: alli se cae antes de escribir nada. Esta es solo para probar en QEMU.
# ROCOLA_PERFIL=pc: para probar en cualquier PC (Intel o Ryzen, cualquier tarjeta grafica), en $ROCOLA_TRABAJO/salida-pc.
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
if [ "${ROCOLA_PERFIL:-a4}" = pc ]; then
	# ROCOLA_PERFIL=pc: PARA PROBAR EN CUALQUIER PC (3-11-2026): la de QEMU (sin instrucciones del Piledriver) y ademas la pantalla de la
	# UEFI como tarjeta (simpledrm: sirve con NVIDIA), Mesa llvmpipe (dibuja con el procesador, rapido en una PC actual), discos NVMe y
	# sonido por USB (board/rocola/linux-pc.fragment). Nombre «rocola-pc»: su memoria solo prueba, nunca instala (post-build.sh).
	SALIDA="$TRABAJO/salida-pc"
	if [ ! -f "$SALIDA/.config" ] || [ "$SISTEMA/configs/rocola_a4_defconfig" -nt "$SALIDA/.config" ] || [ "$SISTEMA/compila.sh" -nt "$SALIDA/.config" ]; then
		mkdir -p "$SALIDA"
		# (sin r600, el controlador de la Radeon del A4: en una PC no sirve, y con el LLVM compilaba tambien su parte AMDGPU, horas de mas)
		sed -e 's/^BR2_x86_piledriver=y/BR2_x86_x86_64=y/' -e '/^BR2_PACKAGE_MESA3D_GALLIUM_DRIVER_R600=y/d' -e 's/^BR2_TARGET_GENERIC_HOSTNAME="rocola"/BR2_TARGET_GENERIC_HOSTNAME="rocola-pc"/' \
		    -e 's|^BR2_LINUX_KERNEL_CONFIG_FRAGMENT_FILES="\(.*\)"|BR2_LINUX_KERNEL_CONFIG_FRAGMENT_FILES="\1 $(BR2_EXTERNAL_ROCOLA_PATH)/board/rocola/linux-pc.fragment"|' \
		    "$SISTEMA/configs/rocola_a4_defconfig" > "$SALIDA/rocola_pc_defconfig"
		printf 'BR2_PACKAGE_MESA3D_LLVM=y\nBR2_PACKAGE_MESA3D_GALLIUM_DRIVER_LLVMPIPE=y\n' >> "$SALIDA/rocola_pc_defconfig"
		make -C "$TRABAJO/buildroot" O="$SALIDA" BR2_EXTERNAL="$SISTEMA" BR2_DEFCONFIG="$SALIDA/rocola_pc_defconfig" defconfig
	fi
elif [ "${ROCOLA_PERFIL:-a4}" = qemu ]; then
	SALIDA="$TRABAJO/salida-qemu"
	# La configuracion se vuelve a aplicar si la del repositorio es mas nueva que la de la compilacion (si no, al actualizar se
	# seguiria compilando con la vieja: paso el 1-11-2026, con un genimage ya renombrado).
	if [ ! -f "$SALIDA/.config" ] || [ "$SISTEMA/configs/rocola_a4_defconfig" -nt "$SALIDA/.config" ]; then
		mkdir -p "$SALIDA"
		sed 's/^BR2_x86_piledriver=y/BR2_x86_x86_64=y/' "$SISTEMA/configs/rocola_a4_defconfig" > "$SALIDA/rocola_qemu_defconfig"
		make -C "$TRABAJO/buildroot" O="$SALIDA" BR2_EXTERNAL="$SISTEMA" BR2_DEFCONFIG="$SALIDA/rocola_qemu_defconfig" defconfig
	fi
elif [ ! -f "$SALIDA/.config" ] || [ "$SISTEMA/configs/rocola_a4_defconfig" -nt "$SALIDA/.config" ]; then
	make -C "$TRABAJO/buildroot" O="$SALIDA" BR2_EXTERNAL="$SISTEMA" rocola_a4_defconfig
fi
make -C "$TRABAJO/buildroot" O="$SALIDA" "$@"
echo "Listo: $SALIDA/images/disk.img"
