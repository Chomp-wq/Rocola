# Paquetes propios de Rocola OS: la rocola (sistema/rocola), por ahora la de consola.
include $(sort $(wildcard $(BR2_EXTERNAL_ROCOLA_PATH)/package/*/*.mk))

# EL NUCLEO, GENERICO (1-11-2026). Buildroot compila el GCC del A4 con --with-arch=bdver2: todo lo que compila usa instrucciones del
# Piledriver aunque no se pida, tambien el nucleo y su descompresor (BMI y TBM: bextr, blcs, tzmsk...). En el A4 existen, pero un nucleo
# asi no arranca en ningun otro procesador ni en ninguna emulacion (se reinicia en GRUB sin decir nada), y al nucleo no le aporta nada.
# Se le da un compilador con -march=x86-64 explicito (y el envoltorio de Buildroot ya no anade el suyo). La rocola y las bibliotecas
# siguen con las instrucciones del A4.
LINUX_MAKE_FLAGS += CC="$(TARGET_CROSS)gcc -march=x86-64 -mtune=generic"

# LLVM PARA radeonsi (27-11-2026, A10-7870K): Mesa radeonsi necesita host-llvm y llvm con X86 + AMDGPU (Buildroot compila LLVM dos veces,
# la del anfitrion y la de la rocola). Lo pesado es ENLAZAR libLLVM (varios GB de memoria cada enlace): un solo enlace a la vez
# (LLVM_PARALLEL_LINK_JOBS usa un «pool» de Ninja y no baja los trabajos de compilar, que siguen siendo los 5 de siempre; con el swap de
# 12 GB del workflow caben). La maquina de GitHub tiene 16 GB; la vez que se congelo con WebKit fue con 5 compilaciones enormes a la
# vez. Si aun asi se congelara, bajar tambien los trabajos de compilar con «-- -j3» como se hizo con WebKit (HOST_LLVM_BUILD_OPTS y
# LLVM_BUILD_OPTS += -- -j3; Ninja toma el ultimo -j).
HOST_LLVM_CONF_OPTS += -DLLVM_PARALLEL_LINK_JOBS=1
LLVM_CONF_OPTS += -DLLVM_PARALLEL_LINK_JOBS=1
