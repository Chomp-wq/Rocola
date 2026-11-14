# Las letras por género con Spotify: hasta 500 páginas por letra (plan, 14-11-2026)

Petición del dueño del 14-11-2026: «La meta es que al final, Spotify quede completamente integrado en la rocola, ahora en las letras de
abecedario se ordenarán además de por orden alfabético como ahora, también se acomodarán por género. Para que cada letra tenga más
canciones, quería que se tomara música de Spotify, y así que cada letra tuviera 500 páginas juntando mi música local y la de Spotify,
pero poniendo los artistas y discos más populares de cada artista por género y ordenado por el abecedario.»

Va DESPUÉS de la base de Spotify (núcleo, buscador y letra ♫, puente de sonido y vigilante: `FASE11_catalogo.md`, `SPOTIFY_SOLOIST.md`),
que es lo que hace falta para el cumpleaños; esto se junta aparte y solo cuando la base esté probada.

## 1. Lo que se sabe (medido el 14-11-2026)

- La pared de XP: 6 x 3 = **18 discos por página** (`Form1.cs` `Cols`, `Rows`); 500 páginas = **9 000 discos por letra**, unos 234 000
  discos en las 26 letras. Hoy cada letra va por artista y luego por disco (`Form1.Biblioteca.cs`, `albums.Sort`, `byLetter`).
- XP ya tiene SU lista de géneros: `GenreLabel` (`Form1.Fondo.cs`, 34 etiquetas: Regional, Tropical, Bolero, Latino, Pop, Rock...), con
  el género de cada disco y de cada artista aprendido de las etiquetas de los MP3. **Es la única lista de géneros**: el catálogo usa las
  mismas etiquetas (las mismas reglas de palabras, en el mismo orden).
- La API web de Spotify con la app del dueño (modo desarrollo, desde febrero de 2026) **no da popularidad, ni seguidores, ni géneros**, la
  búsqueda trae 10 y la cuota es chica y sin números publicados (bloqueos de horas). **No se puede sacar de Spotify «lo más popular por
  género»**, ni hojear 500 páginas preguntándole a Spotify sin quedarse sin buscador a media fiesta.
- Datos abiertos que sí lo dan (alcanzables y probados): **MusicBrainz** (copia completa `mbdump.tar.bz2`, 7 GB: artistas, discos y sus
  enlaces a Spotify; CC0), **Wikidata** (71 063 artistas con su id de Spotify, 39 472 con género; CC0), **ListenBrainz** (cuánto se
  escucha cada artista y cada disco; CC0) y el **Cover Art Archive** (portadas por disco de MusicBrainz, 250 y 500 px, sin llave: 1,4 s
  la de prueba). Las etiquetas de género de MusicBrainz (`mbdump-derived`) son CC BY-NC-SA: solo como respaldo, y dicho en el LEEME.

## 2. El diseño

**Un catálogo propio de la rocola** («el catálogo»), armado fuera de la rocola con datos abiertos y bajado por ella; **la pared no gasta
ni una petición de la API de Spotify**. Spotify se usa para lo suyo: tocar (Soloist), buscar y abrir un disco.

1. **Armar el catálogo** (`sistema/catalogo/`, Python sin dependencias; compilación a pedido `.github/workflows/rocola-catalogo.yml`,
   que lo publica como archivo de la versión `catalogo`): por cada artista, su nombre, su letra, su género (etiqueta de XP), su fama y su
   id de Spotify; y sus discos más escuchados (hasta 6), con nombre, año, id de Spotify si se sabe y el disco de MusicBrainz para la
   portada. Pensado para un bar de México: pesan más lo regional, lo tropical, el bolero y lo latino, y los artistas de México y de
   países de habla hispana. Tope: 9 000 discos por letra.
2. **La rocola lo baja** sola (del repositorio público, sin llave; se comprueba con sha256) al disco de la rocola
   (`ROCOLA_IA_DATOS/../catalogo`, no a `datos.img`), y lo vuelve a mirar una vez al día.
3. **La pared** (función «pared» de Rocola OS, `RocolaOs/RocolaOs.Pared*.cs`): con el modo Spotify encendido y el catálogo presente, cada
   letra junta los discos locales y los del catálogo y los ordena por **género** (un orden fijo para todas las letras: primero los
   géneros de los que el dueño tiene más música; «Otros» al final), dentro por **artista** (abecedario) y, de cada artista, primero sus
   discos locales y luego los del catálogo del más al menos escuchado. Un artista que ya está en la biblioteca queda junto a sus discos
   de Spotify. Como mucho 500 páginas por letra: lo que sobra del catálogo (lo menos famoso) no entra. Sin el modo, o sin catálogo, la
   pared es la de XP tal cual.
4. **Las portadas** del catálogo: del Cover Art Archive, al verse (4 a la vez), guardadas en el disco de la rocola (con tope); sin
   portada, la casilla de XP para los discos sin carátula. Al abrir el disco, la de Spotify.
5. **Abrir un disco del catálogo**: con su id de Spotify, 1 petición (el disco con sus canciones); sin id, antes 1 búsqueda por nombre y
   artista. Si Spotify no lo tiene en México, el aviso de XP y ese disco ya no se enseña. Pedir, cobrar y sonar: lo de la base (I6).
6. **Moverse**: ARRIBA/ABAJO siguen pasando de página. Con 500 páginas hace falta saltar: **mantener ARRIBA o ABAJO salta de género en
   género** (la cabecera dice «LETRA A · REGIONAL»), y **ROJO + izquierda/derecha cambia de letra conservando el género**. Es lo mínimo
   para poder usarlo; la forma final de moverse y de enseñar el género es de lo visual (presentación del final): el dueño elige.

## 3. Lo que NO se hace aquí

- Nada visual nuevo: el género va como texto en la cabecera de XP; las casillas son las de XP.
- Nada de fondo contra la API de Spotify (ni discógrafos ni precargas): la cuota es para buscar y abrir.
- Nada de Spotify se guarda en el disco (las portadas del catálogo no son de Spotify; las de Spotify siguen en `/run`).

## 4. Orden

1. Grupo «catalogo-datos»: el armador, su prueba con una copia chica inventada, la compilación a pedido y el LEEME con fuentes y
   licencias. Lanzar la compilación y revisar el catálogo que salga (cuántos por letra y por género, artistas de México).
2. Junta de la base de Spotify (núcleo + catálogo/pantallas + pulso + soloist), batería, QEMU, compilación y prueba en el A4.
3. Grupo «pared»: la función en rocola-xp encima de la junta, con API falsa, catálogo de prueba y escenas; junta, batería y compilación.
4. Decisión en AGENTS.md cuando esté hecho y probado (y lo que no se probó en el A4, dicho).
