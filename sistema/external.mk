# Paquetes propios de Rocola OS: la rocola (sistema/rocola), por ahora la de consola.
include $(sort $(wildcard $(BR2_EXTERNAL_ROCOLA_PATH)/package/*/*.mk))
