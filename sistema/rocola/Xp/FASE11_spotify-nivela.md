# Spotify: nivelado, la «copia», no repetir y la portada que llega tarde (22-11-2026)

Pedidos del dueño del 22-11-2026, en el A4 con Spotify ya emparejado:

- «que las canciones de Spotify suenen igual de fuerte que una canción de la rocola (El volumen varia mucho en ellas)» → «Haz lo del nivelado».
- La cola perdía canciones de Spotify (Soloist tocaba otra uri de la misma canción y la rocola la pausaba y devolvía el crédito).
- «si se pone una cancion de Spotify, luego se puede poner repetida una que este guardada de manera local en el disco, y sabes que no esta
  permitido repetir canciones».
- «mi padre puso una cancion de michale jackson de un disco de Spotify [...] se puso, pero no aparecia su caratula en el reproductor».

Solo Rocola OS (rocola-xp) con el modo Spotify. Sin él, XP tal cual.

## 1. Nivelado de las pedidas (función `spotify_nivela`, encendida de fábrica; `RocolaOs/RocolaOs.SpotifyNivela.cs`)

Antes, una canción de Spotify pedida (buscador o catálogo) sonaba como una de XP sin medir: ganancia 1, lo que manda Soloist (que no
normaliza).

- **Aprende.** Mientras suena se mide como el repaso mide tus MP3 (`MedidorBs1770`: pico, P99, P90 de picos por bloque y sonoridad
  integrada). Si sonó entera (≥ 97 % y ≥ 30 s), se guardan solo esos números por id en `/rocola/datos/xp/rocola_spotify_niveles.txt`
  (tope 20 000 renglones, los más viejos fuera). Va aparte de las tablas de XP: el repaso nunca ve rutas que no son archivos. La siguiente
  vez, `GananciaDe` (por g62: `RocolaOsCalidadGanancia` pregunta primero aquí si es una pedida, no la sombra de una tuya) la nivela desde el
  primer cuadro con `GananciaAlLimite`, más el margen del tono del local y el parejo del billar, como tus canciones.
- **Primera vez.** Arranca con la mediana de las ganancias de las de Spotify ya medidas (esta tabla y las parejas medidas de la sustitución;
  con 5 o más; si no, 1 como antes). Además, un **nivelado suave en vivo solo en los primeros 30 s de sonido**:
  - mide en la fuente lo que llega de Soloist (ponderación K y bloques de 400 ms con las puertas de BS.1770 sobre lo sonado);
  - **objetivo**: la mediana de «sonoridad integrada + ganancia al límite» de las canciones medidas con el algoritmo 5 (tus MP3, `integCache`)
    y de las de Spotify medidas, o sea la sonoridad típica de la rocola ya nivelada. Con menos de 20 medidas, no hay nivelado en vivo;
  - va hacia `objetivo − sonoridad − arranque`, como mucho **0,5 dB/s**, **±8 dB** del arranque y sin pasar de +3 dB sobre lo que aguanta el
    pico visto (el limitador de XP absorbe el resto);
  - no decide hasta tener 2,3 s de medida, ni en partes suaves (los últimos 3 s a más de 10 LU bajo lo integrado, o casi silencio);
  - a los 30 s **se congela** el resto de la canción (el dueño rechazó los brincos el 18-11-2026). La ganancia cambia muestra a muestra.
  - Va en la fuente (`RocolaOsFuenteSpotify.Nivela`), antes de toda la cadena y del limitador.
- `spotify_nivela=0` en `rocola_os.txt`: como antes, bit a bit. La sustitución invisible sigue con lo suyo.

## 2. La «copia» con otra uri y el último intento (`RocolaOs.Spotify.cs`)

Spotify puede tocar la misma canción con otra uri (otra edición, un recopilatorio). Ahora, si después del play Soloist toca una uri con el
mismo título y artista (sin acentos ni mayúsculas) y la duración a 3 s, se acepta como la pedida (`EsLaSuya`, `Alias`): suena, no se pausa
como «NO PEDIDO» ni se da por acabada. Lo que Soloist ya tocaba al pedirla no cuenta. Y si a los 9 s sigue sin tocarla (atorado), se
reactiva la bocina y se manda un último play, antes de los 15 s en que la fuente la da por fallida y devuelve el crédito.

## 3. No se repite aunque una sea de Spotify (g230, `RocolaOs/RocolaOs.SpotifyRepetida.cs`)

La regla de XP (`AddItem`: no entra la misma que la **última** en sonar o en la cola, «Esa ya va seguida», sin cobrar) solo comparaba rutas.
Ahora, con el modo Spotify, una de Spotify y una tuya son la misma si:

- tu canción tiene pareja en la sustitución (`rocola_spotify_pares.txt`) y es esa uri (si la pareja dice otra, no);
- si no hay pareja: título base y marcas de versión iguales (`TituloBase` y `MarcasDe` de la sustitución: «- Remastered 2012» o
  «(Remasterizado)» no cuentan; «(En vivo)» solo si las dos lo traen), el artista principal (`MismoArtista`) y la duración a 3 s (la tuya, la
  del repaso; sin ella no se dan por iguales).

Dos de Spotify con distinta uri siguen como en XP (distintas). En los dos sentidos. Es la regla de XP, solo con otra idea de «la misma»: sigue
comparando con la última, como XP.

## 4. La portada que llega después (`RocolaOs.SpotifyPide.cs`)

Si al pedirla la portada aún no estaba en `/run`, la canción entraba sin disco y se quedaba sin portada para siempre. Ahora, al bajarse, se
le pone (`RocolaOsSpotifyPortadaLlego`) a lo que suena o espera de esa uri y se repinta como cuando XP cambia de canción
(`UpdateNowPlayingUI`, `RefrescaCola`). Sin dirección de portada en la pista: la de su disco ya leído (`DiscoYa`) o una petición del disco a
la API (una por disco en la sesión, solo con llave y sin el freno del 429; nada de fondo).

## 5. La canción VACÍA de Soloist y el play adelantado en bucle (el registro del A4, 21-11-2026 22:53; `RocolaOs.Spotify.cs`)

El registro real: cada segundo «play adelantado …» → «suena» → «acaba …: Soloist paso a otra» → «NO PEDIDO: Soloist paso a «» ()» → «el play
adelantado … no se uso» → otra vez, y al final «DEVUELTA …: Soloist paso a otra, sono 0.0 s». Soloist manda a veces un `playback_state` con la
canción vacía (uri «») mientras carga o cambia, y la rocola lo tomaba por otra canción. Ahora:

- La canción vacía nunca es «otra canción» ni «no pedido»: no contamina la pedida, no la acaba, no pausa (`ItemVacioDesdeMs` en el estado;
  Evalua y NoPedido). Con la vacía, una pausa o «idle» solo cuenta pasados 5 s (`VaciaMs`); si de verdad no suena, «el anillo sin sonido».
- El play adelantado de una canción que ya falló dos veces adelantada (`AdelantoFallosMax`) ya no se adelanta: suena con el play de siempre.
- `queue_changed` solo da el aviso, una vez por cambio.
- Prueba `vacia`: el Soloist falso con `SOLOIST_FALSO_VACIO=400` (la vacía 400 ms al empezar y a la mitad de cada una, y la cola con 1 de
  contexto). Con el código de antes reproduce lo del A4 (las dos «DEVUELTA … sono 0.0 s», nada se oye: 6 MAL de 9); ahora 9/9: las dos de
  20 s suenan enteras, un solo play adelantado, sin DEVUELTA.

## 6. Un corte a media canción sigue desde donde iba (la de la Sonora Santanera; `RocolaOs.Spotify.cs`, `RocolaOs.SpotifyFuente.cs`)

- Con 10 s o más sonados, un corte (el anillo sin sonido 5 s, el WebSocket caído, dejó de ser la bocina activa, una pausa que no mandó la
  rocola) se intenta seguir UNA vez: la fuente espera en silencio, la rocola manda activate, play y seek a donde iba (`position_ms`) y la
  fuente se re-ancla en el anillo (lo de después del seek es de esa posición: la duración y el final cuadran). Si en 15 s no vuelve a sonar, o
  se corta otra vez, acaba SIN devolver el crédito.
- Una retomada de XP (volvió el aparato de sonido: `Salta`, antes no hacía nada y empezaba de cero como una pedida nueva que, si fallaba en
  sus 10 primeros s, devolvía el crédito) sigue desde donde iba con play y seek, y nunca devuelve (ya sonó).
- rocola_spotify.txt dice siempre al acabar el motivo y los segundos sonados, y «(retomada desde N s)» o «(siguió tras un corte: motivo)».
- Prueba `cortada`, tres corridas: deja de mandar sonido 6 s a los 16 s y vuelve (siguió con seek a 16 s, Soloist la acabó él mismo, 30 s
  oídos una sola vez); deja de mandar y no vuelve (un intento, acaba «sono 16.1 s» sin devolver); se pausa sola (siguió y sonó entera).
- SIN CONFIRMAR con el Soloist de verdad: el nombre del campo del seek (`position_ms`, el de su `playback_state`) y si un seek vacía el flujo
  (un vaciado justo después del re-anclaje no acaba la canción). Si no lo hiciera caso, la canción seguiría desde el principio y se cortaría
  por su duración. La retomada de XP no se probó de punta a punta (la tarjeta de prueba no sabe fallar).

## Pruebas (`Pruebas/RocolaOs/spotify-xp`, con el Soloist falso)

El Soloist falso aprende dos cosas: `q` y dígitos en el id (el pico del tono a −N dBFS) y `SOLOIST_FALSO_COPIA=«A>B»` (al pedir A toca B con
el nombre de A).

- `copiauri`: la copia suena entera, «COPIA» en la bitácora, sin «NO PEDIDO» de la copia ni crédito devuelto.
- `nivela`: con 25 medidas inventadas, una a −30 dBFS sube 7,1 dB en sus primeros 30 s sin saltos de más de 0,3 dB por tramo de 100 ms y se
  congela; se mide y se guarda; la segunda vez suena 19,8 dB más fuerte desde el principio y quieta.
- `portadatarde`: con el servidor de portadas tardando 4 s, entra sin portada y al llegar se le pone a la que suena.
- `repetida`: Spotify y luego tu MP3 (no entra, no se cobra), tu MP3 y luego Spotify (no entra, no se cobra), y otro artista con el mismo
  título sí entra.

No probado con Spotify ni Soloist reales ni en el A4.
