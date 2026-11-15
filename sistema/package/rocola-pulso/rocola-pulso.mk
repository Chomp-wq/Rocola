################################################################################
#
# rocola-pulso: EL SONIDO DE SPOTIFY SOLOIST PARA LA ROCOLA (14-11-2026; sistema/SPOTIFY_SOLOIST.md)
#
# Nuestro libpulse.so.0 (sistema/rocola/pulso, en C): Soloist lo carga (dlopen de «libpulse.so.0») en lugar de PulseAudio porque
# rocola-soloist lo arranca con LD_LIBRARY_PATH=/usr/lib/rocola-pulso; su sonido queda en el anillo de memoria compartida que lee
# rocola-xp. Va en su carpeta propia, /usr/lib/rocola-pulso, para que NADA MAS del sistema lo encuentre (mpv, por ejemplo, no lo usa).
# Su Makefile (objetivos todo, pruebas y limpia) toma CC y CFLAGS de la linea de make y deja libpulse.so.0 en su carpeta.
# Al instalarlo se comprueba que da TODAS las funciones de libpulse que busca Soloist (funciones.txt, sacadas del programa de Soloist
# por fuera: las 48 que pide con dlsym); si falta una, la compilacion falla (Soloist no sonaria).
# La compilacion rapida (sistema/rapida.sh) hace lo mismo con el gcc del servidor de GitHub.
#
################################################################################

ROCOLA_PULSO_VERSION = 1
ROCOLA_PULSO_SITE = $(BR2_EXTERNAL_ROCOLA_PATH)/rocola/pulso
ROCOLA_PULSO_SITE_METHOD = local
ROCOLA_PULSO_LICENSE = propio (Rocola OS)

# (primero «limpia»: el sitio local se copia tal cual, y un libpulse.so.0 o unos .o compilados a mano en la carpeta, con el gcc de la
# maquina, se tomarian por hechos)
define ROCOLA_PULSO_BUILD_CMDS
	$(TARGET_MAKE_ENV) $(MAKE) -C $(@D) limpia
	$(TARGET_MAKE_ENV) $(MAKE) -C $(@D) CC="$(TARGET_CC)" CFLAGS="$(TARGET_CFLAGS)" todo
endef

define ROCOLA_PULSO_INSTALL_TARGET_CMDS
	$(INSTALL) -D -m 0755 $(@D)/libpulse.so.0 $(TARGET_DIR)/usr/lib/rocola-pulso/libpulse.so.0
	f=$$($(TARGET_NM) -D --defined-only $(@D)/libpulse.so.0 | awk '$$2 ~ /^[TtWiI]$$/ {sub(/@.*/, "", $$3); print $$3}'); \
	n=0; for s in $$(grep -v '^#' $(ROCOLA_PULSO_PKGDIR)/funciones.txt); do \
		echo "$$f" | grep -qx "$$s" || { echo "rocola-pulso: libpulse.so.0 no da $$s (Soloist la busca)" >&2; exit 1; }; n=$$((n + 1)); \
	done; echo "rocola-pulso: libpulse.so.0 da las $$n funciones que busca Soloist"
endef

$(eval $(generic-package))
