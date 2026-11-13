################################################################################
# rocola-xp: LA ROCOLA DE XP TRADUCIDA A LINUX (sistema/rocola/Xp). PREPARADO Y APAGADO (sistema/INTEGRACION_ROCOLA_XP.md): no se compila
# ni entra en el sistema mientras BR2_PACKAGE_ROCOLA_XP no este en configs/rocola_a4_defconfig (y package/rocola-xp/Config.in en
# sistema/Config.in). La version a medio traducir NO entra en Rocola OS (regla del dueno del 8-11-2026).
# Como rocola-consola: lo compila el SDK de .NET 10 del equipo que arma el sistema (NativeAOT, un programa nativo); para el A4 con sus
# instrucciones (x86-64-v2 y AVX: RocolaXp.csproj), el perfil de QEMU sin ellas. Va en su carpeta, /usr/lib/rocola-xp, con libSkiaSharp.so
# AL LADO (el programa la busca junto a si; es la de SkiaSharp.NativeAssets.Linux.NoDependencies: solo necesita glibc). Lo demas ya lo
# trae el sistema: libmpv.so.2 (mpv), libmpg123.so.0, libasound.so.2, libEGL.so.1, libGLESv2.so.2, libgbm.so.1, libdrm.so.2, ffmpeg y amixer.
# Las letras de XP NO van aqui (son de Windows): /usr/bin/rocola las lee del disco de Windows al abrirlo.
# El sitio es sistema/rocola entero: RocolaXp.csproj toma tres archivos de ../Consola y ../Motor.
################################################################################
ROCOLA_XP_VERSION = 1
ROCOLA_XP_SITE = $(BR2_EXTERNAL_ROCOLA_PATH)/rocola
ROCOLA_XP_SITE_METHOD = local
ROCOLA_XP_DEPENDENCIES = alsa-lib libdrm mesa3d mpv mpg123

ifeq ($(BR2_x86_piledriver),y)
ROCOLA_XP_PERFIL = a4
else
ROCOLA_XP_PERFIL = qemu
endif

define ROCOLA_XP_BUILD_CMDS
	cd $(@D)/Xp && DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 dotnet publish -c Release -r linux-x64 \
		-p:RocolaPerfil=$(ROCOLA_XP_PERFIL) -o $(@D)/publicado-xp
endef

# (el archivo «version»: mientras rocola-xp no tenga --version, cuando se compilo; lo apunta /usr/bin/rocola al abrirlo)
define ROCOLA_XP_INSTALL_TARGET_CMDS
	$(INSTALL) -D -m 0755 $(@D)/publicado-xp/rocola-xp $(TARGET_DIR)/usr/lib/rocola-xp/rocola-xp
	$(INSTALL) -D -m 0644 $(@D)/publicado-xp/libSkiaSharp.so $(TARGET_DIR)/usr/lib/rocola-xp/libSkiaSharp.so
	echo "rocola-xp programa 0 (sistema) compilado $$(date +%s)" > $(TARGET_DIR)/usr/lib/rocola-xp/version
endef

$(eval $(generic-package))
