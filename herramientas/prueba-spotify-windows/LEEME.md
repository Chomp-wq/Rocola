# Demo de la rocola con Spotify (Windows)

Herramienta de prueba del dueno (19-11-2026): un .exe que levanta una pagina en 127.0.0.1 con una demo de la
rocola (pared, disco, buscador, creditos, cola, letra) con musica solo de Spotify, y una pestana Pruebas
(entrar, llave de la API, diagnostico, informe). Basada en `sistema/rocola-spotify/src/main.rs` (copiada y
adaptada; no se enlaza). No toca `sistema/`. Probada solo sin cuenta real.

Compilar desde Linux:

    apt-get install -y mingw-w64 libasound2-dev pkg-config libssl-dev
    rustup target add x86_64-pc-windows-gnu
    cargo build --release --target x86_64-pc-windows-gnu
    # sale target/x86_64-pc-windows-gnu/release/RocolaSpotifyDemo.exe

Para Linux (solo pruebas): `cargo build --release`; variables `PRUEBA_NO_ABRIR=1` (no abre el navegador) y
`PRUEBA_DATOS=ruta` (carpeta de la llave).
