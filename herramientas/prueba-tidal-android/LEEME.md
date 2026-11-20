# Prueba de TIDAL para Android

La misma prueba chica de `herramientas/prueba-tidal-windows`, en un APK. Contesta dos preguntas con tu cuenta:

1. ¿TIDAL deja tocar canciones **completas** desde una app propia, o solo **muestras de 30 s**?
2. ¿En qué **calidad** llegan (FLAC, bits, kHz)?

## Instalar

1. Pasa `RocolaPruebaTidal.apk` al teléfono (Android 8 o más nuevo; hecho pensando en Android 10+).
2. Ábrelo desde el administrador de archivos. Android pedirá permitir «instalar apps desconocidas» para esa app (Archivos, Chrome...). Si Play Protect avisa que no conoce la app, elige «Instalar de todos modos»: está firmada con una llave de prueba mía, no es de Play Store.
3. Abre **Prueba TIDAL**.

## Usar

Igual que en la PC. En developer.tidal.com crea una app y en *Redirect URIs* pon exactamente `http://127.0.0.1:8760/` (la app la muestra con un botón para copiarla; es la misma de la prueba de la PC, así que sirve la misma app de TIDAL). Después: pega el Client ID → «Entrar con TIDAL» → busca una canción → **Revisar** (¿completa o muestra?) y **Tocar** (qué formato, bits y kHz llegan de verdad).

«Mandar el informe» (el botón de guardar informe) abre el menú de compartir de Android para mandar el informe por WhatsApp, correo, etc. No lleva llaves.

## Cómo está hecha

Un `WebView` que carga `http://127.0.0.1:8760/`, que es un pequeño servidor que corre dentro de la misma app (`Servidor.java`): sirve la página y el SDK oficial de TIDAL para la web (`@tidal-music/auth` y `@tidal-music/player`, los mismos archivos de la prueba de Windows) y hace de **puente** hacia TIDAL (`/tx/openapi`, `/tx/auth`, `/tx/api`), sin los encabezados del navegador. Es el mismo diseño que `RocolaPruebaTidal.exe`. El sonido sale del WebView con el reproductor y el DRM (Widevine) de TIDAL; la app le da al WebView el permiso `PROTECTED_MEDIA_ID` que Widevine necesita, solo para su propia página.

Un cambio al SDK, en la versión de las dos pruebas: sus tres direcciones fijas de la licencia de DRM (`api.tidal.com/v2/widevine`) y de la prueba de reloj (`api.tidal.com/v1/ping`, dos veces) se cambiaron para que pasen por el puente (`location.origin + "/tx/api/..."`); la lógica del SDK no cambia. Sin eso el navegador las bloquearía por CORS desde un origen que no es de TIDAL.

Android 11 o más nuevo no deja guardar archivos donde se vean fácil, por eso el informe va por compartir.

## Compilar (desde Linux, sin el SDK de Android)

```
bash construir.sh
```

Necesita Java 17 o más, curl, unzip y python3. La primera vez baja de Maven Central ~155 MB a `.herramientas/` (aapt2 dentro de apktool, dx, apksig y un android.jar de Robolectric para compilar), arma el manifiesto y los assets, compila, pasa a dex, alinea y firma (esquema v2) con una llave de prueba que crea una vez (`.herramientas/rocola-prueba.p12`). Para **actualizar** una app ya instalada hay que firmar con la misma llave; con otra, hay que desinstalar la anterior primero.

## Probado

- El APK: se arma, la firma se verifica (v2), `aapt2 dump badging` lo lee (paquete `com.rocola.pruebatidal`, minSdk 26, targetSdk 34, permisos INTERNET y MODIFY_AUDIO_SETTINGS), el `classes.dex` trae las clases y los datos sin comprimir van alineados a 4 bytes.
- El servidor de Java (`Servidor.java`) en la PC con Chromium y un TIDAL falso: la página y el SDK, la entrada hasta login.tidal.com y la vuelta con el código (POST con cuerpo), la cuenta, los permisos, la búsqueda y la revisión.

**No probado**: en un teléfono ni en un emulador (aquí no hay), con TIDAL de verdad, ni el sonido (Widevine). Puede fallar en el WebView del teléfono (versión vieja de «Android System WebView») o si TIDAL no acepta la dirección de regreso.
