################################################################################
#
# librespot: SPOTIFY EN LA ROCOLA (decision del dueno del 6-11-2026: «solo lo usare para reuniones familiares»)
#
# El cliente libre de Spotify Connect. Solo la salida por tuberia (la rocola lee su sonido y lo pasa por su cadena: Motor/Spotify.cs);
# sin rodio ni ALSA propios (la tarjeta es de la rocola). TLS con la OpenSSL del sistema y anuncio en la red local (zeroconf) propio,
# para que el celular vea «Rocola» en su lista de dispositivos.
#
################################################################################

LIBRESPOT_VERSION = 0.8.0
# Del registro de Rust (crates.io): es el mismo codigo que la version 0.8.0 de GitHub, con su Cargo.lock, y se baja tambien desde
# donde GitHub no deja (la nube donde se prepara). El .crate es un tar.gz con otro nombre.
LIBRESPOT_SITE = https://static.crates.io/crates/librespot
LIBRESPOT_SOURCE = librespot-$(LIBRESPOT_VERSION).crate
LIBRESPOT_LICENSE = MIT
LIBRESPOT_LICENSE_FILES = LICENSE
LIBRESPOT_DEPENDENCIES = host-pkgconf openssl
LIBRESPOT_CARGO_BUILD_OPTS = --no-default-features --features with-libmdns,native-tls
LIBRESPOT_CARGO_INSTALL_OPTS = --no-default-features --features with-libmdns,native-tls

define LIBRESPOT_EXTRACT_CMDS
	$(ZCAT) $(LIBRESPOT_DL_DIR)/$(LIBRESPOT_SOURCE) | $(TAR) --strip-components=1 -C $(@D) $(TAR_OPTIONS) -
endef

$(eval $(cargo-package))
