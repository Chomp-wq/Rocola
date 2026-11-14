################################################################################
# rocola-xp: LA ROCOLA DE XP TRADUCIDA A LINUX (sistema/rocola/Xp), el UNICO programa de Rocola OS (decision del dueno del 14-11-2026;
# sistema/INTEGRACION_ROCOLA_XP.md). Lo compila el SDK de .NET 10 del equipo que arma el sistema (NativeAOT: un programa nativo, sin .NET
# en Rocola OS); para el A4 con sus instrucciones (x86-64-v2 y AVX: RocolaXp.csproj), el perfil de QEMU sin ellas. Va en su carpeta,
# /usr/lib/rocola-xp, con libSkiaSharp.so AL LADO (el programa la busca junto a si; es la de SkiaSharp.NativeAssets.Linux.NoDependencies:
# solo necesita glibc). Lo demas ya lo trae el sistema: libmpv.so.2 (mpv), libmpg123.so.0, libasound.so.2, libEGL.so.1, libGLESv2.so.2,
# libgbm.so.1, libdrm.so.2, ffmpeg y amixer. Las letras de XP NO van aqui (son de Windows): /usr/bin/rocola las lee del disco de Windows.
# El sitio es sistema/rocola entero: RocolaXp.csproj toma tres archivos de ../Consola y ../Motor, y la cancion de prueba es
# Consola/prueba.mp3 (la ponia rocola-consola, retirado: /usr/share/rocola/prueba.mp3 para rocola-prueba y el disco «Rocola OS - Prueba»).
# LA VERSION («rocola-xp --version»: «rocola-xp programa N (COMMIT) compilado SEGUNDOS», lo que leen /usr/bin/rocola, rocola-actualiza y
# rocola-buzon): N es el numero del sistema (ROCOLA_SISTEMA_VERSION, el que pone GitHub; en casa, 0) y COMMIT el del repositorio
# (GITHUB_SHA o git; si no, «local»), como en sistema/rapida.sh. /usr/lib/rocola-xp/version guarda esa misma linea.
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

# (version.esperada: la linea que tiene que contestar el programa; se compila con esos mismos N, COMMIT y SEGUNDOS)
define ROCOLA_XP_BUILD_CMDS
	V=$${ROCOLA_SISTEMA_VERSION:-0}; F=$$(date +%s); \
	C=$${GITHUB_SHA:-$$(git -C $(BR2_EXTERNAL_ROCOLA_PATH) rev-parse HEAD 2>/dev/null)}; C=$$(printf '%s' "$${C:-local}" | cut -c1-7); \
	cd $(@D)/Xp && DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 dotnet publish -c Release -r linux-x64 \
		-p:RocolaPerfil=$(ROCOLA_XP_PERFIL) -p:RocolaVersion=$$V -p:RocolaCommit=$$C -p:RocolaFecha=$$F -o $(@D)/publicado-xp && \
	echo "rocola-xp programa $$V ($$C) compilado $$F" > $(@D)/publicado-xp/version.esperada
endef

# (el archivo «version»: lo que dice el programa; si el equipo que compila no lo puede correr -otra glibc, sin AVX-, la linea esperada;
# si dice otra cosa, la compilacion falla)
define ROCOLA_XP_INSTALL_TARGET_CMDS
	$(INSTALL) -D -m 0755 $(@D)/publicado-xp/rocola-xp $(TARGET_DIR)/usr/lib/rocola-xp/rocola-xp
	$(INSTALL) -D -m 0644 $(@D)/publicado-xp/libSkiaSharp.so $(TARGET_DIR)/usr/lib/rocola-xp/libSkiaSharp.so
	e=$$(cat $(@D)/publicado-xp/version.esperada); l=$$(timeout 30 $(@D)/publicado-xp/rocola-xp --version 2>/dev/null | head -1); \
	if [ -z "$$l" ]; then echo "rocola-xp: aqui no corre --version; se apunta la linea esperada: $$e"; l=$$e; \
	elif [ "$$l" != "$$e" ]; then echo "rocola-xp --version dice «$$l» y se esperaba «$$e»" >&2; exit 1; fi; \
	echo "$$l" > $(TARGET_DIR)/usr/lib/rocola-xp/version
	$(INSTALL) -D -m 0644 $(@D)/Consola/prueba.mp3 $(TARGET_DIR)/usr/share/rocola/prueba.mp3
	$(INSTALL) -D -m 0644 $(@D)/Consola/prueba.mp3 "$(TARGET_DIR)/rocola/musica/Rocola OS - Prueba/prueba.mp3"
	mkdir -p $(TARGET_DIR)/rocola/datos
endef

$(eval $(generic-package))
