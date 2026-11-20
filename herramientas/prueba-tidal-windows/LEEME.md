# Prueba de TIDAL para la rocola (Windows)

Una prueba chica, no la demo entera. Contesta dos preguntas con tu cuenta antes de hacer la demo completa:

1. ¿TIDAL deja tocar canciones **completas** desde una app propia, o solo **muestras de 30 s**?
2. ¿En qué **calidad** llegan (FLAC, bits, kHz)?

Usa el SDK oficial de TIDAL para la web (con un solo cambio: sus direcciones de la licencia de DRM y de la prueba de reloj pasan por el puente, ver abajo) (`@tidal-music/auth` 1.7.0 y `@tidal-music/player` 0.20.1, licencia Apache-2.0, empaquetados en `src/tidal_sdk.js`). El sonido sale del navegador (Chrome o Edge), con el reproductor y el DRM de TIDAL.

## Lo que se sabe antes de probar

- El reproductor oficial pide FLAC y FLAC Hi-Res (`FLAC_HIRES`, hasta 24 bit / 192 kHz) y encadena canciones sin hueco (`setNext`).
- La API de TIDAL contesta, para cada canción, si da la **completa** (`FULL`) o una **muestra** (`PREVIEW`) y por qué. Uno de los motivos es `FULL_REQUIRES_HIGHER_ACCESS_TIER`: hace falta un nivel de acceso de programador más alto.
- A mediados de 2026 lo que se ha publicado (documentación del SDK, foros de TIDAL y de otros programas) dice que las apps normales de programador reciben solo muestras de 30 s. Esta prueba lo comprueba con tu cuenta.
- El SDK tiene fijas tres direcciones de `api.tidal.com` (`/v2/widevine`, la licencia del DRM, y `/v1/ping`, dos veces); desde una página que no es de TIDAL el navegador las bloquearía por CORS, así que se cambiaron en `src/tidal_sdk.js` por `location.origin + "/tx/api/..."` (pasan por el puente). La lógica del SDK no cambia.
- No se usan las llaves internas de las apps de TIDAL (eso hacen proyectos no oficiales y va contra sus condiciones).

## Cómo usarla

1. Entra a **developer.tidal.com** con tu cuenta de TIDAL → Dashboard → crea una app.
2. En la app, en *Redirect URIs*, pon exactamente `http://127.0.0.1:8760/` (la página te la muestra con un botón para copiarla). Si TIDAL no acepta `127.0.0.1`, pon `http://localhost:8760/` y abre la página desde esa dirección.
3. Copia el **Client ID** de la app.
4. Abre `RocolaPruebaTidal.exe` (abre Chrome/Edge solo en `http://127.0.0.1:8760/`). El puerto 8760 es fijo: es el de la dirección de regreso.
5. Paso 1: pega el Client ID. (Opcional: «Ver qué permisos deja pedir TIDAL» y «Usar todos».)
6. Paso 2: «Entrar con TIDAL» → entras en login.tidal.com → vuelves solo a la página. Se ve tu país y tu **nivel de acceso de programador** (`THIRD_PARTY` es el de una app normal).
7. Paso 3: la calidad (de fábrica la máxima, Hi-Res FLAC) y si nivelar el volumen (de fábrica no).
8. Paso 4: busca una canción.
   - **Revisar** le pregunta a TIDAL sin tocar: «COMPLETA» o «MUESTRA (motivo)» y qué formatos manda.
   - **Tocar** la toca: «Lo que suena» dice en grande si es la canción completa y con qué formato, bits y kHz llega, o si es una muestra y por qué.
9. «Guardar informe» deja `informe_tidal.txt` junto al programa (sin llaves ni tokens). Mándame eso o una captura.

## Cómo funciona por dentro

- `RocolaPruebaTidal.exe` (Rust) sirve la página en `127.0.0.1:8760` y hace de **puente** para la API y la entrada de TIDAL (`/tx/openapi`, `/tx/auth`, `/tx/api`), porque los servidores de TIDAL pueden no contestar a una página de `127.0.0.1`. El SDK deja cambiar esas direcciones con sus funciones públicas (`setApiUrl`, `setLegacyApiUrl`, `tidalAuthServiceBaseUri`); la licencia del DRM (Widevine) va directo a TIDAL, como trae el SDK.
- El programa no guarda llaves: las guarda el SDK en el navegador. En la ventana negra solo se ven los caminos pedidos y su resultado.
- El sonido: el navegador entrega el FLAC sin pérdida a Windows, que lo pasa a la frecuencia de tu tarjeta (no es salida «bit a bit»).

## Compilar desde Linux

```
cargo build --release --target x86_64-pc-windows-gnu
```

Para rehacer `src/tidal_sdk.js` (solo si cambia la versión del SDK): en una carpeta aparte, `npm install @tidal-music/auth@1.7.0 @tidal-music/player@0.20.1 esbuild`, un `entrada.js` con `import * as auth from '@tidal-music/auth'; import * as player from '@tidal-music/player'; window.TidalSDK = { auth, player };` y `npx esbuild entrada.js --bundle --format=esm --minify --target=chrome110 --outfile=tidal_sdk.js`.

Para probar sin TIDAL: `PRUEBA_TIDAL=http://127.0.0.1:PUERTO` manda el puente a un servidor falso.

## Probado

En Linux con Chromium contra un TIDAL falso con la forma de la API documentada: la entrada (ida a login.tidal.com con el Client ID, PKCE y la dirección de regreso; vuelta con el código), la cuenta y su nivel de acceso, los permisos, la búsqueda y la revisión (muestra con su motivo). No probado con TIDAL de verdad ni en Windows, y no se pudo tocar sonido (sin TIDAL ni DRM aquí).
