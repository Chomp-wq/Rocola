# Cómo activar Spotify en la rocola (paso a paso)

El modo Spotify ya viene encendido en la rocola: solo le faltan **tus dos llaves** y **emparejar** una vez. Mientras no las tenga, la
rocola se ve y suena igual que la de XP. Necesitas tu cuenta de Spotify **Premium**, una memoria USB y la rocola conectada a internet.
Las llaves son secretas: no las mandes por mensaje ni las subas a ningún lado.

Lo largo, con todas las órdenes y qué hacer si algo falla: `sistema/SPOTIFY_SOLOIST.md`.

## Paso 1. La llave de Soloist (en tu computadora)

1. Entra a <https://developer.spotify.com> con tu cuenta Premium («Log in», arriba a la derecha).
2. Abre la documentación de **Spotify Soloist** → «Getting started» → **Generate an API key**. Acepta las condiciones y pulsa
   **Generate**.
3. Copia la llave en un archivo de texto llamado **`rocola_spotify.txt`**, en la raíz de la memoria USB. Solo la llave, en una línea
   (el Bloc de notas sirve).

## Paso 2. La llave de tu app (para buscar desde la rocola)

Ya tienes una app («Chomps») de la demo de Windows: puedes usar esa misma.

1. En <https://developer.spotify.com/dashboard>, abre tu app (o **Create app**, marcando **Web API**).
2. En **Settings**, en **Redirect URIs**, que esté `http://127.0.0.1:8898/callback` (puedes dejar también la de la demo,
   `http://127.0.0.1:8898/login`). **Save**.
3. Copia el **Client ID** y, con «View client secret», el **Client Secret**.
4. En la misma memoria USB, un archivo **`rocola_spotify_api.txt`** con estas dos líneas:

   ```
   client_id=EL_CLIENT_ID
   client_secret=EL_CLIENT_SECRET
   ```

## Paso 3. Dárselas a la rocola

**La forma fácil:** con la rocola prendida y con internet, conecta la memoria USB y espera un minuto. Mientras le falte la llave, la
rocola mira la memoria cada 30 s y la toma sola. Después saca la memoria.

**La forma segura** (si quieres ver que las tomó):
1. Conecta la memoria USB.
2. **Ctrl+Alt+F2** (consola de mantenimiento, usuario `root`). La rocola se cierra un momento.
3. Escribe **`spotify llave`**. Debe decir «Llave de Soloist puesta» y «Llave de la API web puesta».
4. Saca la memoria y vuelve a la rocola con **Ctrl+Alt+F1**.

Con la llave puesta, la rocola baja sola el programa de Spotify (unos 14 MB).

## Paso 4. Emparejar (una sola vez, en el bar)

1. En tu celular, **conectado al wifi del bar** (la misma red que la rocola), abre Spotify con tu cuenta.
2. Pon cualquier canción y toca el **icono de dispositivos** (la bocina con la pantallita).
3. Elige **«Rocola»**.

Listo. La canción que pusiste no suena (la rocola pausa lo que no se pidió en ella), y la cuenta queda guardada: no hay que volver a
hacerlo, ni al apagar ni al actualizar.

## Paso 5. Comprobar

En la consola (Ctrl+Alt+F2): **`spotify prueba`** dice BIEN o MAL de cada cosa, y **`spotify estado`** dice si las llaves están puestas,
si la bocina está emparejada y cuántos días le quedan al programa de Spotify. Vuelve con **Ctrl+Alt+F1**.

Ya en la rocola: la letra **♫** aparece en la pared, el buscador trae resultados de Spotify, y cada canción de Spotify cuesta un crédito
como cualquier otra. Si una no puede sonar, el crédito se devuelve.

## Si algo no sale

- **«Rocola» no aparece en la app:** el celular y la rocola tienen que estar en la misma red; si el wifi del bar es «de invitados» o
  aísla los aparatos, no se ven. También puedes escribir `spotify emparejar` en la consola.
- **No encuentra nada al buscar:** revisa `rocola_spotify_api.txt` (el Client ID y el secreto bien copiados) y vuelve a hacer
  `spotify llave`.
- **Para apagarlo:** `spotify apagar` (y `spotify encender` para volver).

Nada de esto se ha probado todavía con tu cuenta en el A4: si algo no funciona, el informe de `spotify prueba` me dice dónde está el
problema.
