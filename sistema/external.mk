# Paquetes propios de Rocola OS: la rocola (sistema/rocola), por ahora la de consola.
include $(sort $(wildcard $(BR2_EXTERNAL_ROCOLA_PATH)/package/*/*.mk))

# EL NUCLEO, GENERICO (1-11-2026). Buildroot compila el GCC del A4 con --with-arch=bdver2: todo lo que compila usa instrucciones del
# Piledriver aunque no se pida, tambien el nucleo y su descompresor (BMI y TBM: bextr, blcs, tzmsk...). En el A4 existen, pero un nucleo
# asi no arranca en ningun otro procesador ni en ninguna emulacion (se reinicia en GRUB sin decir nada), y al nucleo no le aporta nada.
# Se le da un compilador con -march=x86-64 explicito (y el envoltorio de Buildroot ya no anade el suyo). La rocola y las bibliotecas
# siguen con las instrucciones del A4.
LINUX_MAKE_FLAGS += CC="$(TARGET_CROSS)gcc -march=x86-64 -mtune=generic"

# WEBKIT CON 3 TRABAJOS (22-11-2026): con los 5 de siempre (4 nucleos + 1) la maquina de GitHub (16 GB) se quedo sin memoria a media
# compilacion de WebKit y se congelo (sistema-39). Ninja toma el ultimo -j: este va despues del de Buildroot.
WPEWEBKIT_BUILD_OPTS += -- -j3
