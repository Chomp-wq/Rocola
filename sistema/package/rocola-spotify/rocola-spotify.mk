################################################################################
#
# rocola-spotify: SPOTIFY DENTRO DE LA ROCOLA (decision del dueno del 6-11-2026: «solo lo usare para reuniones familiares»)
#
# El codigo esta en sistema/rocola-spotify (Rust, con las piezas de librespot 0.8.0). Como paquete local no pasa por la descarga, asi que
# Buildroot no le prepara las dependencias: aqui cargo las baja de crates.io (sin --offline), pero con --locked, exactamente las de
# Cargo.lock (las mismas que librespot 0.8.0).
#
################################################################################

ROCOLA_SPOTIFY_VERSION = 1.0.0
ROCOLA_SPOTIFY_SITE = $(BR2_EXTERNAL_ROCOLA_PATH)/rocola-spotify
ROCOLA_SPOTIFY_SITE_METHOD = local
ROCOLA_SPOTIFY_LICENSE = MIT
ROCOLA_SPOTIFY_DEPENDENCIES = host-pkgconf openssl

define ROCOLA_SPOTIFY_BUILD_CMDS
	cd $(@D) && \
	$(TARGET_MAKE_ENV) $(TARGET_CONFIGURE_OPTS) $(PKG_CARGO_ENV) \
		cargo build --release --manifest-path Cargo.toml --locked
endef

define ROCOLA_SPOTIFY_INSTALL_TARGET_CMDS
	cd $(@D) && \
	$(TARGET_MAKE_ENV) $(TARGET_CONFIGURE_OPTS) $(PKG_CARGO_ENV) \
		cargo install --root $(TARGET_DIR)/usr/ --bins --path ./ --no-track --force --locked -Z target-applies-to-host
endef

$(eval $(cargo-package))
