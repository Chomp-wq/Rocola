################################################################################
# rocola-consola: la rocola (sistema/rocola): en la pantalla por KMS y OpenGL ES, en modo texto o sin pantalla. La compila el SDK de .NET 10 del equipo que arma el sistema (NativeAOT:
# sale un solo programa nativo, sin .NET en Rocola OS). Para el A4 lleva sus instrucciones (SSE4.2 y AVX); el perfil de QEMU, no.
################################################################################
ROCOLA_CONSOLA_VERSION = 1
ROCOLA_CONSOLA_SITE = $(BR2_EXTERNAL_ROCOLA_PATH)/rocola
ROCOLA_CONSOLA_SITE_METHOD = local
ROCOLA_CONSOLA_DEPENDENCIES = alsa-lib libdrm mesa3d dejavu jpeg

ifeq ($(BR2_x86_piledriver),y)
ROCOLA_CONSOLA_PERFIL = a4
else
ROCOLA_CONSOLA_PERFIL = qemu
endif

define ROCOLA_CONSOLA_BUILD_CMDS
	cd $(@D)/Consola && DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 dotnet publish -c Release -r linux-x64 \
		-p:RocolaPerfil=$(ROCOLA_CONSOLA_PERFIL) -o $(@D)/publicado
endef

define ROCOLA_CONSOLA_INSTALL_TARGET_CMDS
	$(INSTALL) -D -m 0755 $(@D)/publicado/rocola-consola $(TARGET_DIR)/usr/bin/rocola-consola
	$(INSTALL) -D -m 0644 $(@D)/Consola/prueba.mp3 $(TARGET_DIR)/usr/share/rocola/prueba.mp3
	$(INSTALL) -D -m 0644 $(@D)/Consola/prueba.mp3 "$(TARGET_DIR)/rocola/musica/Rocola OS - Prueba/prueba.mp3"
	mkdir -p $(TARGET_DIR)/rocola/datos
endef

$(eval $(generic-package))
