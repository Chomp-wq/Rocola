# Fase 11, Spotify: el catálogo y las pantallas (grupo «catalogo»)

Trabajo urgente del 14-11-2026 (el cumpleaños es esta semana, en el bar). El dueño decidió: «Lo de Spotify será todo en la rocola, nada
en el celular». Este grupo hace que lo de Spotify **se busque, se vea y se pida EN LA ROCOLA**, con el aspecto de XP (regla R8), usando
la API web de Spotify con la **app propia del dueño** (I7) y pidiendo por **I6** (el núcleo, grupo «spotify-xp», va en paralelo).

Rama `fase11-catalogo` (base `fase11-andamio`). Archivos: `RocolaOs/RocolaOs.SpotifyCatalogo*.cs`, los ganchos g40-g50 en `Codigo/`,
sus líneas en `RocolaOs/GANCHOS.tsv` y `Pruebas/RocolaOs/catalogo/`.

## 1. Lo que hace

| Pantalla | Qué se ve (todo con lo que XP ya dibuja) | Peticiones a la API |
|---|---|---|
| El buscador | Debajo de las primeras 8 de XP, hasta 8 canciones, 3 discos y 2 artistas de Spotify como **tarjetas de XP** (CANCIÓN / ÁLBUM / ARTISTA, su portada, su cubo); luego el resto de XP. Elegir una canción la **pide** (I6), sin cerrar el buscador, como XP. Elegir un disco lo **abre** en la vista de disco de XP (la portada sale del cubo del buscador, como XP). Elegir un artista lleva a **sus discos** en la letra de Spotify, como XP lleva a los de un artista a su letra. | 1 por consulta, a los 300 ms de dejar de escribir; lo ya contestado sale de memoria |
| La vista de disco | La de XP: la portada en el cubo, la lista «NN Título», PONER pide por I6 | 1 (el disco con sus canciones; 2 si tiene más de 50) |
| La pared | Una letra más al final de la barra: **♫** (U+266B, del juego de símbolos de Windows XP, WGL4); la cabecera dice **SPOTIFY** (como «VIDEOS» o «LO MAS SONADO»). Lleva lo de la cuenta (si hay `refresh_token`: «Tus favoritas» como un disco, tus discos guardados y tus listas propias) y los discos abiertos o pedidos en esta sesión (hasta 40, el más reciente primero). REGRESAR+izquierda/derecha también llega a ella. | 0 (lo de la cuenta: 3 una vez por sesión) |
| Las portadas | Las de XP: se bajan al salir en pantalla a `/run/rocola-xp/spotify/portadas/<id>.jpg` (en memoria), y su miniatura a `/run/rocola-xp/spotify/miniaturas` (gancho g43); la previa de 32 px no va a `previews32-v1.bin` (g50). **Nada de Spotify al disco.** | 0 a la API (vienen del servidor de imágenes) |

Lo de Spotify **no entra en la biblioteca de XP** (`albums`, `byLetter`, `searchIndex`, `rocola_biblioteca.txt`): son `Album` y `Track`
de XP que viven en `RocolaOs.SpotifyCatalogo.cs`, con la carpeta `/::SPOTIFY::/<tipo>:<id>` (absoluta y sin palabras: XP adivina el género por el nombre de la carpeta y de la de encima) y la ruta `spotify:track:<id>` (la `Uri` de I6),
y entran a XP solo por los ganchos y por I6. Un resultado de Spotify elegido **no** pasa a los atajos «Buscaste:» de XP (no se guarda).

**Sin el modo** (`spotify=0`, de fábrica, o sin `ROCOLA_OS`) cada gancho devuelve lo de XP tal cual: ni letra, ni resultados, ni una sola
petición a la red (comprobado: escenas `apagado` y `cero`, 0 peticiones).

Si Soloist no tiene sesión (`RocolaOsSpotifyListo` falso), elegir una de Spotify **no pide ni cobra nada** y sale el aviso que XP ya tiene
«Espera un momento y vuelve a intentarlo» (y la bitácora lo dice). Si un disco o un artista no llega (sin red, 429, 5xx), el mismo aviso.

## 2. La API web: qué rutas y por qué (documentación oficial leída el 14-11-2026)

Leída con curl en developer.spotify.com/documentation/web-api (copias en `/root/rocola-trabajo/fase11/catalogo/docs`): referencia de
search, albums, artists, playlists y me; tutoriales de client credentials, PKCE y refresco; conceptos de llaves, cuotas y límites; la guía
de migración de febrero de 2026 y los cambios de marzo, mayo y julio de 2026. Lo que importa:

- **Modo desarrollo** (la app del dueño): el dueño con Premium, hasta 5 usuarios; la **cuota es por cuenta de desarrollador** (julio de
  2026: todas sus apps comparten cubeta) y al pasarse da **429** con `reason: QUOTA_EXCEEDED` (bloqueos de horas, según lo reportado).
- **Búsqueda**: `limit` máximo **10** (de fábrica 5). Se pide `type=track,album,artist&limit=10` en **una** petición.
- **Quitado en febrero de 2026**: `GET /albums?ids=`, `/tracks?ids=`, `/artists?ids=` (en lote), `/artists/{id}/top-tracks`,
  `/browse/*`, `/users/{id}/playlists`, los campos `popularity`, `followers`, `label`, `available_markets`; `/playlists/{id}/tracks` pasa a
  **`/playlists/{id}/items`** (y cada renglón trae la canción en `item`, no en `track`), y **solo para listas propias o colaborativas**
  (las de Spotify ya no dan sus canciones). `external_ids` volvió en marzo.
- `GET /artists/{id}/albums`: `limit` máximo **10**; se pide `include_groups=album,single`.
- `GET /albums/{id}` trae sus canciones (hasta 50): abrir un disco es **1** petición.
- **Client Credentials** (sin usuario): sirve para el catálogo (buscar, discos, artistas); hay que decir el país (`market=MX`).
- **PKCE** con `refresh_token` para lo del usuario (`/me/tracks`, `/me/albums`, `/me/playlists`); al renovar Spotify puede dar **otra**
  `refresh_token` (con PKCE la vieja deja de valer): se guarda encima, de un tirón y 0600. La autorización vence a los 6 meses.
- La llave de librespot (login5) da 429 a todo desde 12-2025: **no se usa**.

| Para qué | Ruta | Llave |
|---|---|---|
| La llave de la app | `POST accounts.spotify.com/api/token` `grant_type=client_credentials` (Basic id:secreto) | — |
| La llave de la cuenta | `POST .../api/token` `grant_type=refresh_token&refresh_token=..&client_id=..` | — |
| Buscar | `GET /v1/search?q=..&type=track,album,artist&limit=10&market=MX` | app |
| Abrir un disco | `GET /v1/albums/{id}?market=MX` (y `/albums/{id}/tracks?offset=50&limit=50` si tiene más de 50) | app |
| Los discos de un artista | `GET /v1/artists/{id}/albums?include_groups=album,single&limit=10&market=MX` | app |
| Tus favoritas | `GET /v1/me/tracks?limit=50&market=MX` | cuenta |
| Tus discos | `GET /v1/me/albums?limit=50&market=MX` | cuenta |
| Tus listas | `GET /v1/me/playlists?limit=50` (solo las propias o colaborativas) | cuenta |
| Abrir una lista propia | `GET /v1/playlists/{id}/items?limit=50&market=MX` | cuenta |

**La cuota se cuida así**: una petición a la vez (un hilo); lo que alguien espera (buscar, abrir) antes que lo de la cuenta; **nada de
fondo** (ni SP4, ni discógrafos, ni precargas); todo lo contestado se queda en memoria la sesión entera (buscar lo mismo otra vez: 0
peticiones); con **429** no se pregunta NADA hasta que pase su `Retry-After` (sin él, 30 s), y se dice «Spotify frena» en la bitácora y en
el estado; con **401** se pide otra llave y se intenta una vez más; con **5xx** o sin red, una vez más al segundo.

### La llave (I7)

`/rocola/datos/xp/rocola_spotify_api.txt` (0600), líneas `clave=valor`:

```
client_id=...        (la app del dueño, en su panel de desarrolladores)
client_secret=...    (SECRETO)
refresh_token=...    (opcional, SECRETO: la cuenta del dueño autorizada con PKCE desde su laptop)
mercado=MX           (opcional)
usuario=...          (opcional: el id de la cuenta, para no enseñar listas ajenas)
```

Nada de esto se apunta, ni sale en el estado, ni en la bitácora, ni en pantalla (la prueba lo busca en todo lo que escribe la rocola). Se
vuelve a leer si el archivo cambia (lo mira cada 5 s como mucho). La copia desde la memoria USB (`rocola_spotify_api.txt` en su raíz) la
hace el vigilante del grupo «soloist» (ver «Para los otros grupos»).

### Lo que se ve de fuera

- `/run/rocola-xp/spotify/catalogo` (sin secretos): `api=bien|sin_llave|frena|falla`, `llave=si|no`, `cuenta=si|no`, `peticiones`,
  `busquedas`, `discos`, `artistas`, `cuenta_peticiones`, `llaves`, `frenadas`, `frena_s` (y « cuota» si fue QUOTA_EXCEEDED), `fallos`,
  `ultimo_codigo`.
- La bitácora de Rocola OS (`rocola_os_bitacora.txt`, `[spotify_catalogo]`): el encendido, «Spotify frena (429)…», las respuestas que no
  son 200 (con la ruta sin la consulta), lo que no se pidió por falta de sesión. Nunca la consulta con la llave, ni la llave.

## 3. Los ganchos (g40-g50)

| Id | Archivo:línea | Para qué |
|---|---|---|
| g40 | `Form1.Biblioteca.cs` 3516 `searchOverlay.Results = res;` | Los resultados de Spotify en la misma lista (**es la línea de g12 del plan**: ver «Riesgos») |
| g41 | `Form1.Biblioteca.cs` 3560 `if (it == null) return;` (ChooseSearchResult) | Elegir un resultado de Spotify |
| g42 | `Form1.cs` 2278 `if (t == null) return;` (AddSong) | PONER en un disco de Spotify pide por I6 |
| g43 | `Form1.Portadas.cs` 737 (ThumbCachePath) | La miniatura de una portada de Spotify en `/run` |
| g44 | `Form1.Biblioteca.cs` 2387 (CurrentList) | Los discos de la letra de Spotify |
| g45 | `Form1.Biblioteca.cs` 623 (LetrasVisibles) | La letra ♫ al final de la barra |
| g46 | `Form1.Biblioteca.cs` 2874 (GotoLetter) | REGRESAR+izq/der llega a la letra ♫ |
| g47 | `Form1.Biblioteca.cs` 2444 (la cabecera) | «SPOTIFY» en la cabecera |
| g48 | `Form1.Vistas.cs` 2295 (Track.ToString) | «NN Título» en la lista del disco |
| g49 | `Form1.Biblioteca.cs` 2926 (EnterAlbum) | Entrar a un disco sin sus canciones las pide |
| g50 | `PreviewsIndexadosGpu.cs` 160 (IndexedSave) | La previa de 32 px de Spotify no va al disco |
| g51 | `Form1.Biblioteca.cs` 669 (RecalculaLetras: `letterBar.Counts = cuenta;`) | La letra ♫ con su número de canciones y su rayita (de la revisión, §11) |
| g52 | `PantallaVivaGpu.cs` 317 (AlbumElegidoEnPared: `return albumView.Page[i];`) | Un disco de Spotify sin canciones que se queda elegido las pide (de la revisión, §11) |

Con el núcleo juntado (sus g13, g20, g23 y g24), `ganchos.py`: TODO BIEN (24 ganchos en 11 archivos; los 109 archivos de `Codigo/`
vuelven byte a byte a fc7b667 devolviendo los ganchos). `diff-xp.py --sin-ganchos` en los 109 archivos: 5 095 líneas quitadas, 4 828
puestas y 229 sin marca, las mismas cuentas que la base (22 ganchos devueltos en 9 archivos).

**g40 es la línea de g12 del plan** (el buscador flexible y por letra de la oleada 4, que también va «debajo de lo de XP»). Cuando llegue
g12, las dos cosas tienen que ir en la MISMA línea: `searchOverlay.Results = RocolaOsSpotifyCatalogoResultados(query, <lo de g12>(query, res));`
(lo flexible debajo de lo exacto de XP, y lo de Spotify como hoy: después de las primeras 8). Lo junta el coordinador.

## 4. La junta con el núcleo (I6)

**Juntado** (merge de `fase11-spotify-xp` en 3d5e532 y después hasta 09f96f0, el 15-11-2026): I6 es el de verdad (`RocolaOs/RocolaOs.SpotifyPide.cs`) y el doble
del catálogo (`RocolaOs.SpotifyCatalogoDobleI6.cs`) **se borró**. Lo único que cambió en el catálogo: al pedir se llama
`RocolaOsSpotifyPide(p, searchOpen)` (la sobrecarga del núcleo con los avisos del buscador, los de `ChooseSearchResult` de XP, cuando se
pide desde el buscador; desde un disco, los de `AddSong`). Antes de pedir, el catálogo mira `RocolaOsSpotifyListo`: si Soloist no tiene
sesión no pide nada y sale el aviso de XP «Espera un momento y vuelve a intentarlo».

La prueba ahora corre con el núcleo de verdad y **el Soloist FALSO del núcleo** (`Pruebas/RocolaOs/spotify-xp/SoloistFalso`, abierto por su
`vigilante-falso.sh`; nunca el de verdad): lo pedido se cuenta por las líneas «COBRADA» de la bitácora del núcleo (`rocola_spotify.txt`) y por
los `play` que le llegan al Soloist falso, y suena por la tarjeta de prueba con reloj (`Pruebas/Windows/alsa/reloj.c`).

Lo que el catálogo le pasa al núcleo en cada `RocolaOsSpotifyPista`: `Uri` (`spotify:track:ID`), `Titulo`, `Artista` (los artistas
separados por «, »), `Disco`, `DuracionMs`, `PortadaUrl` (la imagen de ~300 px del disco) y `DiscoUri`. El catálogo baja la portada de cada
disco a `/run/rocola-xp/spotify/portadas/<id del disco>.jpg`, y desde 0aa2ee4 (juntado) el núcleo nombra la suya igual: es el mismo
archivo, no se baja dos veces, y la canción entra en la cola con su portada (la escena `suena` lo comprueba).

## 5. Las comprobaciones (con números)

`Pruebas/RocolaOs/catalogo/prueba.sh` (rocola-xp de verdad, fuera de pantalla, teclas con `ROCOLA_PULSA`, datos inventados de `datos0`, la
API web FALSA de `ApiFalsa/` y el núcleo con el Soloist falso). Diez escenas:

| Escena | Qué hace | Comprueba |
|---|---|---|
| `apagado` | sin `ROCOLA_OS`, escribe en el buscador | 0 peticiones a la API (ni la llave), nada en /run, nada de `RocolaOs:spotify` en la traza |
| `cero` | `ROCOLA_OS=1` y `spotify=0` | lo mismo, y la traza igual a la de `apagado` quitando lo de RocolaOs (`compara_trazas.py --ignora`, con la holgura del ruido) |
| `busca` | «inventad» (XP no tiene nada), entra a las tarjetas y pide la 1.ª y la 2.ª | 1 búsqueda a la API, 1 llave de la app, ninguna otra petición; 2 COBRADAS en su orden con título y artista; el `play` de la 1.ª en el Soloist falso; portadas y miniaturas en /run y nada en los datos; el estado; ni secretos ni llaves en datos y /run; nada en `rocola_biblioteca.txt` |
| `mezcla` | «tema»: lo de XP y lo de Spotify juntos | las de XP primero (captura), 1 o 2 búsquedas, la última «tema» |
| `disco` | abre un disco desde el buscador y pide la 1.ª y la 2.ª con PONER; REGRESAR | las búsquedas y **1** petición del disco; las 2 COBRADAS son la 1.ª y la 2.ª del disco |
| `artista` | elige un artista | las búsquedas y **1** de sus discos; nada pedido |
| `fallos` | 401 de llave vencida, 503 y 429 (la API falsa cambia de humor por lo que ya contestó) | 401 → otra llave → la misma otra vez (200); 503 → otra vez al segundo (200); 429 una vez y **ninguna** petición más a /v1 (dos consultas más no preguntan); «Spotify frena (429)…» con la cuota en la bitácora; el estado dice `api=frena` y `frena_s=N cuota` |
| `cuenta` | con `refresh_token`: la letra ♫ con lo de la cuenta, abre una lista propia y pide | 3 peticiones de la cuenta, 1 de la lista propia, la ajena nunca; la llave de refresco rotada se guarda encima (0600, sin la vieja); 1 COBRADA |
| `sinsesion` | sin Soloist (el vigilante sale al momento) | nada pedido ni cobrado; la bitácora lo dice |
| `suena` | pide una, cierra el buscador | 1 COBRADA y el Soloist la toca; el núcleo dice que suena; salió sonido por la tarjeta |

**Peticiones por acción** (contadas por la API falsa): buscar = **1** (más la llave de la app una vez por hora); abrir un disco = **1**; los
discos de un artista = **1**; lo de la cuenta = **3**, una vez por sesión; abrir una lista propia = **1**; buscar lo mismo otra vez = **0**;
las portadas no son de la API.

RESULTADOS: ver «Resultados» al final.

## 6. Para lo visual (para la presentación del final; no se hizo nada nuevo)

- La letra de Spotify en la barra es el carácter ♫ (U+266B, WGL4: está en las letras de XP) dibujado como cualquier letra de XP; no lleva
  color ni marca propia. La cabecera dice «SPOTIFY».
- Las tarjetas del buscador de Spotify son idénticas a las de XP: nada dice que son de Spotify (la línea de abajo dice el artista y el
  disco). Una marca de «Spotify» sería visual nueva (K3 del plan).
- La casilla del buscador cuenta también las de Spotify (con la cuenta de XP).
- La lista de un disco de Spotify no enseña la duración (XP solo la enseña de las canciones ya medidas, y las de Spotify no se miden: el
  dato está, `DuracionMs`).
- Avisos: con Soloist sin sesión, o un disco que no llega, sale «Espera un momento y vuelve a intentarlo» (texto de XP). Un aviso propio
  («Spotify no responde», «Spotify frena») sería nuevo.
- «Tus favoritas» y las listas propias usan la portada de su primera canción o la de la lista.
- Lo que suena con una de Spotify lo pinta el núcleo (con los datos de I6).

## 7. Para los otros grupos

- **soloist** (el vigilante): copia `rocola_spotify_api.txt` de la raíz de la memoria USB a `/rocola/datos/xp/rocola_spotify_api.txt`
  (0600), igual que la llave de Soloist (en su rama ya está: `rocola-soloist`, `importa_llaves`). El catálogo vuelve a leer el archivo si
  cambia (lo mira cada 5 s como mucho): no hace falta reiniciar la rocola.
- **coordinador**: g40 comparte la línea de g12 (§3). La API falsa y el Soloist falso solo existen en `Pruebas/` (fuera de
  `RocolaXp.csproj`).

## 8. Riesgos

- **La cuota de Spotify** (modo desarrollo, por cuenta de desarrollador): una fiesta con muchas búsquedas. Se busca desde 3 letras, a los
  300 ms de dejar de escribir y una sola vez por consulta (luego, de memoria). Con 429 la rocola no pregunta nada hasta su `Retry-After`; los
  bloqueos reportados son de horas: entonces el buscador solo da lo de XP (y lo ya buscado, de memoria).
- **La API web cambia seguido** (febrero, marzo, mayo y julio de 2026). Si una ruta deja de valer, lo de esa pantalla queda vacío y la
  bitácora apunta el código; la música de XP sigue igual.
- **Client Credentials con `market=MX`**: si Spotify no tiene una canción en México, no sale (es lo que se puede tocar en el bar).
- **g40 y g12** en la misma línea (§3).
- Lo de la cuenta (`refresh_token`) vence a los 6 meses de autorizarla: entonces la letra ♫ solo enseña lo buscado y lo abierto.

## 9. Para el A4 (solo se puede comprobar allí, con la app del dueño y el Soloist de verdad)

1. Con `rocola_spotify_api.txt` (client_id y client_secret de la app del dueño) en la USB: que el vigilante lo copie y que
   `/run/rocola-xp/spotify/catalogo` diga `api=bien` tras la primera búsqueda.
2. Buscar un artista conocido: que salgan sus canciones de Spotify debajo de las de XP, con portada, en menos de 1-2 s con la red del bar.
3. Pedir una con PONER: que cobre 1 crédito, entre en su turno y suene por la rocola (esto ya es el núcleo con Soloist y rocola-pulso).
4. Abrir un disco y un artista de Spotify desde el buscador; volver con REGRESAR y ver el disco en la letra ♫.
5. Contar en `/run/rocola-xp/spotify/catalogo` las `peticiones` tras una hora de uso normal (para saber si la cuota alcanza en la fiesta).
6. Si el dueño autoriza su cuenta (PKCE, `refresh_token`): que la letra ♫ tenga «Tus favoritas», sus discos y sus listas.
7. Sin internet (desconectar el cable): el buscador solo con lo de XP, sin trabarse; la música de XP sigue.

## 10. Resultados

Del 15-11-2026, con el núcleo juntado hasta 09f96f0 (la rama en su último commit):

| Comprobación | Resultado |
|---|---|
| `prueba.sh` en JIT (las 10 escenas) | **55 de 55** BIEN |
| `prueba.sh` en NativeAOT (las 10 escenas) | **55 de 55** BIEN |
| `dotnet build -c Release --no-incremental` | 0 errores, **13 avisos** (los de la base, todos en `Codigo/` y `Plataforma/`; ninguno nuevo) |
| `dotnet publish` NativeAOT | sale con 0, **0 avisos IL/AOT** |
| `ganchos.py` | TODO BIEN: 24 ganchos en 11 archivos; `Codigo/` vuelve byte a byte a fc7b667 |
| `diff-xp.py --sin-ganchos` (109 archivos) | 5 095 / 4 828 / 229: las cuentas de la base |
| Nivel 0 (`nivel0.sh` del grupo nivel0, sin `ROCOLA_OS`; pasos previo, código, ganchos, escenas, aot) | previo, código (109 de 109), ganchos.py y diff-xp.py BIEN; escenas alternadas con la referencia: arranque, moneda, tab, buscador, captura y asistente **IGUAL**, sigterm **ACEPTADO** (el estado se borra: lo aceptado de siempre); NativeAOT: arranque y buscador **IGUAL**. Sale con 0 |
| Nivel 1 (`NIVEL=1`, `ROCOLA_OS=1`, `spotify` apagado de fábrica) | las mismas 7 escenas: 6 **IGUAL** y sigterm **ACEPTADO**; NativeAOT arranque y buscador **IGUAL**. Sale con 0 |

Las escenas del nivel 0 y 1 se corrieron con el programa juntado hasta 3d5e532; lo que el núcleo añadió después (0aa2ee4, 09f96f0) solo
toca `RocolaOs/` y `Pruebas/` y no hace nada sin el modo (no cambia ningún gancho de `Codigo/`). Se eligieron las escenas que pueden mover
los ganchos del catálogo (la pared y sus letras, la moneda y PONER, el buscador, Ctrl+F3, el asistente, las capturas y el cierre); la
batería entera del nivel 0 (arneses, banco, cintas, recorridos, ventanas, cierre, importa) es de la junta.

Capturas para el dueño (fuera de pantalla, 1024×768, con la API falsa y datos inventados), en `/root/rocola-trabajo/fase11/catalogo/capturas`:
`buscador-solo-spotify`, `buscador-pedidas`, `buscador-xp-y-spotify`, `buscador-disco`, `disco-de-spotify`, `pared-letra-spotify`,
`buscador-artista`, `pared-discos-del-artista`, `pared-cuenta`, `disco-lista-propia`, `buscador-spotify-no-listo`,
`buscador-sonando-spotify` y `suena-spotify` (lo que suena con una de Spotify).

## 11. Revisión (xp-intacto-y-aspecto)

Revisión independiente del 15-11-2026 (sin el modo, XP exacta; con el modo, todo lo de Spotify en los elementos y el dibujo de XP; el pedir
por I6; nada de Spotify en la biblioteca de XP). Se leyó el diff entero de la rama contra fase11-andamio, cada gancho en su sitio de
`Codigo/` y lo de XP que toca lo de Spotify (la barra de letras, la ficha y el letrero de la portada elegida, la tarjeta del buscador,
ChooseSearchResult, AddSong, CurrentList, NavigateToAlbum, EnterAlbum, las miniaturas y las previas), y se miraron todas las capturas.

**Lo que estaba bien (comprobado, sin cambios):**
- Sin el modo cada gancho devuelve lo de XP tal cual: g40/g44-g47 miran el modo antes de nada; g41/g42/g48/g49 solo actúan con rutas o
  carpetas de Spotify («spotify:track:», «/::SPOTIFY::/»), que XP nunca tiene; g43/g50 solo con la carpeta de las portadas en `/run`, que
  sin el modo no existe. El modo se decide UNA vez por sesión (`RocolaOs.Encendida`): no hay apagado en vivo que deje lo de Spotify a
  medias en las pantallas de XP.
- Elegir un resultado de Spotify sale antes de `ApuntaBusqueda` (los atajos «Buscaste:» no guardan nada de Spotify). Los avisos son textos
  y colores de XP («Espera un momento y vuelve a intentarlo», Form1.cs 1948); los de pedir, los de AddSong/ChooseSearchResult (I6 del
  núcleo). La ♫ se dibuja como ★ (un carácter de la barra; WGL4) y la cabecera dice SPOTIFY como VIDEOS o LO MAS SONADO.
- Nada de Spotify entra en la biblioteca de XP: después de buscar, pedir y sonar (escena «suena»), en los datos solo
  `rocola_spotify.txt` (la bitácora del núcleo) y la llave nombran algo de Spotify (comprobación nueva de la prueba).

**Lo encontrado y arreglado (commit c47c732):**
1. (menor) La letra ♫ salía sin su número de canciones ni su rayita, que XP pone debajo de cada letra (y de ★): se veía distinta a las
   demás. Gancho **g51** (`Form1.Biblioteca.cs` 669, RecalculaLetras: `letterBar.Counts = RocolaOsSpotifyCatalogoCuentas(cuenta)`): con el
   modo, la ♫ cuenta las canciones que se saben de sus discos; sin el modo, el mismo diccionario de XP sin tocar. Cuando cambian (llega un
   disco, la cuenta, un reciente) se cambia el diccionario entero como hace XP (`RocolaOsSpotifyCatalogoPonCuenta`).
2. (menor, a la vista) Los discos de Spotify que llegan sin canciones (los de un artista, las listas propias) salían en la pared con
   «0 canciones» en el letrero de la portada elegida y en la ficha de XP, y la ficha decía «Entrar al disco» sin canciones (captura
   `pared-discos-del-artista` de antes). Gancho **g52** (`PantallaVivaGpu.cs` 317, AlbumElegidoEnPared: `return
   RocolaOsSpotifyCatalogoElegido(albumView.Page[i])`): un disco de Spotify sin canciones que se queda elegido 0,6 s las pide (UNA
   petición por disco y sesión: alguien lo está mirando; nada de fondo); al llegar solo se llena (la letra no se reacomoda) y la ficha y el
   letrero se rehacen con lo que XP ya vigila (generoVer, duracionesVer). Ahora dice «10 canciones» y las tres primeras (captura nueva).
   Cuesta, como mucho, una petición más por disco mirado; la escena «artista» lo cuenta (búsquedas + 1 del artista + 1 del disco elegido).

**Queda (para lo visual o para la junta; no arreglado):**
- La tarjeta ARTISTA de Spotify en el buscador dice «1 disco» (XP cuenta el disco de su `AlbumRef`, que aquí es el del artista, y aún no se
  saben sus discos). Arreglarlo pide otro gancho en la tarjeta (BuscadorPortadasGpu.cs, DatosCarta) o una tarjeta nueva: para la
  presentación.
- Las portadas de los resultados de Spotify se bajan al crearse cada resultado (hasta 13 por búsqueda), no solo las que se ven; no gastan
  la cuota de la API (son del servidor de imágenes), van una a la vez y a `/run`.
- Sin `usuario=` en `rocola_spotify_api.txt`, las listas que el dueño sigue y no son suyas también salen en la letra y al abrirlas dan 404
  («Espera un momento...»). Para el A4: poner `usuario=` (el id de la cuenta) o filtrarlas por el dueño con `/me` (una petición más).

**Comprobaciones repetidas (con el programa de c47c732):**

| Comprobación | Resultado |
|---|---|
| `dotnet build -c Release --no-incremental` | 0 errores, 13 avisos (los de la base) |
| `dotnet publish` NativeAOT | sale con 0, 0 avisos IL/AOT |
| `prueba.sh` en JIT (10 escenas) | **56 de 56** BIEN (una más que antes: nada de Spotify en los datos; la de «artista» cuenta la petición de g52) |
| `prueba.sh` en NativeAOT (10 escenas) | **56 de 56** BIEN |
| `ganchos.py` | TODO BIEN: 26 ganchos en 12 archivos; `Codigo/` vuelve byte a byte a fc7b667 |
| Nivel 0 (pasos previo, código, ganchos, escenas, aot; escenas arranque, moneda, buscador y captura alternadas con la referencia; NativeAOT arranque y buscador) | **11 BIEN**, 0 MAL; diff-xp.py --sin-ganchos 5 095 / 4 828 / 229 (las de la base; 24 ganchos devueltos en 10 archivos). Sale con 0 |
| Nivel 1 (`NIVEL=1`, `ROCOLA_OS=1`, spotify apagado; las mismas escenas y las de NativeAOT) | **8 BIEN**, 0 MAL. Sale con 0 |

Las escenas son las que pueden mover g51 (la barra de letras: arranque, moneda, captura) y g52 (la pared con la tarjeta: arranque, moneda,
buscador); sin el modo los dos devuelven lo de XP sin hacer nada más. Las capturas de `/root/rocola-trabajo/fase11/catalogo/capturas` se
rehicieron con este programa (la ♫ con su número; `pared-discos-del-artista` con «10 canciones»).
