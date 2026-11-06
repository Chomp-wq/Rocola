# Rocola en casa

La herramienta para trabajar en la rocola desde casa (pedido del dueño del 7-11-2026), en la laptop con **Pop!_OS** o en la
computadora con **Windows**. Un solo archivo, sin instalar nada: `rocola-casa` (Linux) o `rocola-casa.exe` (Windows). Al abrirlo,
abre el **tablero** en el navegador (solo se puede abrir desde esa misma computadora).

## Qué hace

- **Resumen**: si la rocola está en línea (y desde cuándo no escribe), si la actualización ya está (versión en la rocola contra la
  última publicada, si ya la bajó y si se pone sola), si el programa corre, los errores, las compilaciones de GitHub en curso o
  fallidas, lo que suena, la cola y los créditos, el equipo (carga, temperatura, memoria, discos), la red (IP, MAC, Wake on LAN),
  Spotify, el núcleo (dmesg) y los discos de música.
- **Errores**: los últimos y los que más se repiten (agrupados sin la hora ni los números); traer `rocola_error.txt` entero.
- **Registros**: todas las bitácoras de la rocola (lo último de cada una, hasta 256 KB), con buscador y «solo errores».
- **Archivos**: pedir un archivo entero de los datos (las bitácoras y las tablas: sonoridad, EQ, lo más sonado, la bitácora del billar,
  el diagnóstico...) y guardarlo en `~/RocolaCasa` para trabajar con él. Nunca las llaves ni la cuenta de Spotify.
- **Actualizaciones**: versiones, compilar el programa o el sistema en GitHub, «Buscar actualización ahora» y «Poner la versión
  nueva ya».
- **Órdenes**: ping, estado, registros, archivo, buscar actualización, diagnóstico completo de la pantalla (cuando la rocola esté
  libre), reiniciar el programa o el equipo (cuando esté libre, o «ya»), con lo que contestó la rocola.
- **Encender y apagar**: si está prendida, «despertar» la hace buscar actualización al momento; si está apagada, manda el paquete
  mágico de Wake on LAN al router del bar (si el router lo reenvía). Y **apagarla** al terminar de trabajar: «cuando esté libre» (nada
  sonando ni en la cola, 2 minutos sin botones) o «ya»; los créditos y la cola se guardan y vuelven al prender. «Cancelar lo pendiente»
  quita un apagado o reinicio que aún espera.
- En la terminal: `rocola-casa --estado` (lo esencial) y `rocola-casa --orden buscar_actualizacion` (cualquier orden).

## Cómo funciona

La rocola está detrás del router del bar, así que todo pasa por un **buzón**: un repositorio privado de GitHub (`rocola-buzon`). La
rocola (`rocola-buzon`, en el sistema) lee ahí tus órdenes cada minuto y sube su estado cuando algo cambia (al menos cada hora).

## Prepararlo (una vez)

1. En GitHub, crea un repositorio **privado** `rocola-buzon` (con «Add a README»).
2. **La llave de la rocola**: Settings → Developer settings → Personal access tokens → Fine-grained tokens → Generate new token.
   Solo el repositorio `rocola-buzon`, **Contents: Read and write**. Guárdala en `rocola_buzon.txt` (solo la llave) en una memoria USB y
   conéctala a la rocola prendida: la copia sola. Ctrl+F3 → Internet dice «Desde casa (buzón): al día...».
3. **La llave de casa**: otra igual con los repositorios `Rocola` y `rocola-buzon`, **Contents: Read and write** y **Actions: Read and
   write**. Abre `rocola-casa`, ve a **Ajustes**, pégala y «Comprobar la llave».
4. La rocola necesita el sistema con el buzón (el siguiente «sistema-N» después del 7-11-2026).

En Pop!_OS: `chmod +x rocola-casa && ./rocola-casa`. En Windows: doble clic en `rocola-casa.exe` (Windows puede avisar por ser un
programa nuevo: «Más información» → «Ejecutar de todas formas»).

## La llave de Genius (buscar canciones por su letra)

La rocola puede buscar una canción por un pedazo de su letra preguntándole a Genius. Para eso necesita una llave gratuita:
1. Entra a **genius.com/api-clients** (con una cuenta de Genius) → **New API Client**: nombre «Rocola», y en «App website URL» cualquier
   dirección (por ejemplo la de tu Facebook o `https://example.com`). Guardar.
2. En el cliente creado, **Generate Access Token** y copia el **Client Access Token**.
3. Ponlo en la rocola: en **Ajustes** de Rocola en casa, «Llave de Genius para la rocola» → «Mandar a la rocola»; o en un archivo
   `rocola_genius.txt` en una memoria USB conectada a la rocola.

## Compilarlo

`dotnet publish -c Release -r linux-x64` (o `-r win-x64`) en esta carpeta, o en GitHub: Actions → «Rocola en casa» → Run workflow (publica
«casa-N» con los dos).
