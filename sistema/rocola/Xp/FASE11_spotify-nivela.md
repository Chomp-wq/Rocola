# Spotify: nivelado, EQ y brillo, la «copia», no repetir y la portada que llega tarde (22-11-2026)

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

## 7. EQ automático y brillo a la medida de las pedidas (funciones `spotify_eq` y `spotify_brillo`, encendidas de fábrica; `RocolaOs/RocolaOs.SpotifyEq.cs`)

Decisión del dueño del 22-11-2026 (eligió las dos mejoras de sonido para Spotify). Antes, una pedida de Spotify no aprendía nada (g23, R7):
sin huella de tono, `EqCorrFor` daba null (sin EQ automático) y `ExciteFactorFor` daba 1 (el realce de una sin medir), y Soloist llega en Ogg
Vorbis 160 kbps (cortado a unos 16 kHz).

- **Aprende con la misma medida de la cadena.** g250 (`ArmaCadena`, `c.EqRuta`): de una pedida (su fuente es `RocolaOsFuenteSpotify`, no la
  sombra de la sustitución), desde el principio, sin IA y aún sin huella, la ruta «spotify:track:ID» va a la medida de XP: el `LevelTap` junta
  la energía de las 8 bandas como con tus MP3. Se arma si `spotify_brillo` está encendida o si el EQ mide huellas (`EqMideHuella`).
- **El corte de agudos, en la misma pasada.** Tus MP3 sacan el corte de la ficha de calidad del repaso (un `SpectralTap` solo de análisis: 88
  cuadros de 2048 del canal 0 con sonido, del 10 al 90 % de la canción) y, mientras suenan, del DSEE (solo si está encendido: apagado no
  analiza). Una de Spotify no pasa por el repaso: la fuente hace lo mismo que esa ficha con lo que llega de Soloist (`RocolaOsSpotifyCorte`,
  los 88 cuadros repartidos del 10 al 90 % de su duración, en el hilo del sonido, con la memoria reservada al abrirla). Al cerrar manda el del
  DSEE si lo midió (como en tus MP3); si no, el de la fuente.
- **Se guarda aparte.** g251 (`CierraHuellaDeFuente`): la medida de una ruta «spotify:» se cierra aquí y XP no la ve (ni `eqCache`, ni
  `rocola_eq.txt`, ni el promedio de la biblioteca, ni `rocola_fuentes.txt`). Solo si sonó entera (≥ 97 % de su duración y ≥ 30 s), con las
  mismas cuentas que `CierraHuella` (las 8 bandas contra la mediana del cuerpo), por id en `/rocola/datos/xp/rocola_spotify_eq.txt` (8 bandas,
  corte, corte de abajo y la fecha; tope 20 000, los más viejos fuera). Las retomadas o saltadas no se guardan.
- **Desde la segunda vez, el cálculo de XP.** g252 (`EqCorrCancion`), g253 (`ExciteFactorFor`) y g254 (`FaltaDeCuerpoDb`, la canción flaca del
  billar) leen esa huella como si estuviera en `eqCache`: la fuerza automática, el objetivo por canción de la IA del EQ, la canción rara, las
  reglas del modo billar con la afinación del local, nada sobre el corte; y el realce con su corte (más a la cortada), la mitad del extra con
  el DSEE encendido y lo de «a la medida». `spotify_eq` decide el EQ y lo flaco del billar; `spotify_brillo`, el realce. El realce y el EQ en
  vivo de Ctrl+F3 (`ApplyNormNow`) y el relevo sin pausa usan lo mismo.
- **La sustitución invisible no cambia** (g61): su lector es `RocolaOsFuenteCalidad` (nunca se mide aquí) y una uri que es pareja de una de tus
  canciones (`rocola_spotify_pares.txt`) no se lee de esta tabla.
- Lo que sigue sin la huella de Spotify (no se pidió): el bajo psicoacústico (`BassFactorFor`, `BajoAuto`), el margen del ecualizador del
  dueño (E3, `PreganCancionDb`: sin huella usa lo máximo de la curva, más margen), el des-recorte (`DeclipVale`: sin medir, se pone) y la
  duración mínima del EQ del billar (`SegundosDe` no sabe la de Spotify). El DSEE ya decide en vivo con el corte que mide de lo que suena; para
  que arranque con el guardado haría falta otro gancho (sembrar `cutHz` del `SpectralTap` al armarlo), y no hace falta.
- Con las dos apagadas, sin el modo Spotify o sin Rocola OS: como antes, bit a bit (ningún gancho cambia nada con una ruta que no es «spotify:»).

## 8. Cinco mejoras de Spotify (27-11-2026; elegidas por el dueño de la lista del 24-11)

Solo Rocola OS con el modo Spotify. Ganchos g430-g437 (más la ayuda de g252-g254); nada cambia sin el modo.

1. **La sustituida suena con el EQ y el nivel de tu MP3** (`RocolaOs.Calidad.cs`, `RocolaOs.SpotifyEq.cs`). Antes la sombra (g61) usaba solo lo de Spotify: sin EQ automático ni
   realce, y nivelada sola. Ahora `RocolaOsSpotifyEqHuella` (g252/g253/g254) da la huella de `eqCache` de tu MP3 por la uri de la sombra (`RocolaOsCalidad.Mp3DeSombra`), así el EQ y
   el realce son los de tu archivo (su corte, más bajo que el de Spotify, hace el EQ más prudente). El nivel (`RocolaOsCalidadGananciaDe`): la ganancia de tu MP3 (la del
   disco si suena en racha, `GananciaDeDisco`; si no `NormGainFor`) por lo que Spotify se aparta de ella: con la medida de la pareja, el cociente de las dos ganancias al límite; sin
   ella, la mediana de ese cociente en las parejas medidas (3 o más; si no, 1). Sin la medida al límite de tu MP3, como antes. No hecho: el LRA y el nivel del local del billar.
2. **Billar y cadena completa con Spotify**: g430 `BassFactorFor`, g431 `BajoAuto`, g433 margen E3 (`EqDuenoPesosDe`), g437 rasgos del parejo estimado (pedidas con su huella de
   `rocola_spotify_eq.txt`, sombras con la de tu MP3); g432 `DeclipVale` (solo la sombra; una pedida sigue con des-recorte por si acaso, su recorte no se mide); g434/g435 la
   duración (`RocolaOsSegundosDe`: la de la pista de Spotify o la de tu MP3) para el mínimo de 45 s del EQ del billar y el parejo; g436 el parejo de la sombra es el de tu MP3.
   La bitácora de la sustitución dice, al armar la cadena, EQ, realce, bajo, des-recorte, E3, duración y parejo.
3. **No cobrar con Soloist sordo**: `RocolaOsSpotify.PuedePedir` (sin WebSocket, sin sesión, conectado 10 s sin contestar a `get_auth_state`, vigilante «esperando» o «arrancando»,
   archivo «reinicia» puesto). `RocolaOsSpotifyListo` (que usa el catálogo) y `RocolaOsSpotifyPide` lo usan: no se cobra ni entra y sale «Espera un momento y vuelve a intentarlo»;
   la bitácora dice el motivo. Las tuyas sustituidas no cambian.
4. **Devolver si se corta antes de la mitad** (`RocolaOs.SpotifyFuente.cs`, `Acaba`): un fallo que no es natural devuelve el crédito si lo sonado no llegó a la mitad de la
   duración (antes solo con menos de 10 s); desde la mitad, no. La bitácora dice «NO LLEGO A LA MITAD (iba en el s X de Y)». Sustituye «acaba SIN devolver» del 22-11.
5. **Sin silencio al empezar** (función `spotify_arranque`, `RocolaOs.SpotifyArranque.cs`, encendida): medido, el arranque sin hueco de la fuente (unos 1,3 s de la tarjeta de XP) se
   queda; lo que se quita es el silencio digital que Soloist entrega antes de la música: la fuente lo consume del anillo sin entregarlo (todo ≤ −80 dBFS, tope 1500 ms, hasta 16
   tomas por lectura), completa la lectura con la música que sigue y le pone 1 ms de fundido; no toca retomadas ni cortes seguidos. Con el Soloist falso (1400 ms de ceros) la música
   llega ~1 s antes. Efecto aceptado: el anillo queda sin colchón un instante (huecos al azar también aparecen sin la función con la máquina cargada).

**Revisión de lo anterior (27-11-2026, un revisor de solo lectura sobre lo hecho desde e759e3f).** Siete puntos comprobados uno a uno:

- **Caché en disco truncada** (`RocolaOs.SpotifyCatalogoMemoria.cs`; REAL): un apagón tras el cambio de nombre (sin fsync, NTFS por ntfs3) dejaba un archivo cortado que `Lee` aceptaba
  (un disco con menos canciones 30 días, una página con menos discos). Ahora cada archivo termina con `fin<TAB>N` (N = las líneas de datos) y `Lee` lo exige y comprueba la cuenta; si falta o no
  coincide, se borra y es como si no hubiera (se vuelve a pedir). Los archivos viejos, sin cierre, se piden otra vez (una vez). No se usó `Canciones.Count == NumCanciones`: `total_tracks` cuenta
  las que no se pueden tocar (que no se guardan) y las de más de 100, y habría descartado discos sanos para siempre; la cuenta del cierre detecta el truncado sin ese riesgo.
- **Claves de edición «Vol 1/Vol 2 Deluxe»** (`RocolaOs.SpotifyEdiciones.cs`; NO ES REAL): el segundo bucle de `Clave` devuelve el número a la clave (`greatest hits vol 1` / `vol 2`,
  `now 20` / `now 21`). Casos dejados en `Pruebas/RocolaOs/catalogo/EdicionesClave` (`dotnet run`, sin la rocola).
- **Busca en el hilo de la pantalla** (`RocolaOs.SpotifyCatalogoApi.cs`; REAL): `Busca` consultaba `TieneBusqueda` (SHA1, ruta, `FileInfo` en el disco de la música) en cada tecla. Ahora el pedido nace con la
  espera entera (`Perezosa`) y el hilo del catálogo (`DecideBusqueda`, antes de tomarlo) la quita si lo escrito ya está en el disco. Lo que se ve no cambia (lo sabido sigue sin esperar).
- **La uri de una sustituida pedida después** (`RocolaOs.Calidad.cs`, `RocolaOs.SpotifyPide.cs`; REAL, menor): los ganchos g430-g437 miran por la uri sola, y `sombraDe` la dejaba asociada a tu MP3
  hasta 4000 uris. `RocolaOsSpotifyAbre` (la pedida directa) ahora quita la asociación (`QuitaSombra`) y lo apunta («pedida directa de ...: ya no es la sombra de ...»). La sombra la vuelve a poner al armar su cadena.
- **Motivo real al no poder pedir** (`RocolaOs.SpotifyCatalogo.cs`, `RocolaOsSpotifyCatalogoPide`; REAL, menor): usa `PuedePedir(out motivo)` y apunta sordo, reiniciándose, sin WebSocket... en vez de «Soloist sin sesion».
- **La hoja Spotify** (`RocolaOs.VentanaSpotify.cs`; REAL, menor): `RocolaOsSpotifyArmaVista` (cada 0,5 s en el hilo de la pantalla) releía el archivo del vigilante y la cola de 160 KB de la bitácora con dos
  expresiones por línea. Ahora se recuerdan y se releen solo cada 5 s o si cambió tamaño o fecha (un `FileInfo` por vuelta). La carpeta de la caché solo se recorre una vez (hasta que `Limpia`, al arrancar el hilo de la API, ya sabe la suma): no hacía falta más.
- **Recorte del silencio inicial** (`RocolaOs.SpotifyFuente.cs`): (a) REAL, arreglado: tras 16 tomas sin música, la última (sin mirar) se devolvía como sonido (silencio contado como sonido, o música
  sin recortar); ahora son 16 tomas y una vuelta 17 que solo mira, y lo no mirado no se entrega (probado solo por lectura: con el Soloist falso el anillo no tiene colchón para 16 tomas llenas seguidas).
  (b) Microcortes tras el recorte: SIN EVIDENCIA, no se cambió. Medido con el Soloist falso, `arranque` (huecos a media canción de la bitácora, ms): con la función y 1400 ms de ceros 504 y 694, sin ella
  822 y 442; sin ceros 945 y 0 (con la función) y 1509 y 254 (sin ella, con intro suave): son los mismos huecos al azar de la máquina cargada, con y sin recorte (ya lo decía la nota de la escena).

Pruebas: spotify-xp `arranque`, `sordopide`, `mitad` (cortada2 y cortadalarga ajustadas a la regla nueva); sustitucion `nivelmp3`. Soloist falso: `SOLOIST_FALSO_SILENCIO=MS` y
`SOLOIST_FALSO_INTRO_SUAVE=MS`. No con Soloist real ni en el A4.

## Pruebas (`Pruebas/RocolaOs/spotify-xp`, con el Soloist falso)

El Soloist falso aprende dos cosas: `q` y dígitos en el id (el pico del tono a −N dBFS) y `SOLOIST_FALSO_COPIA=«A>B»` (al pedir A toca B con
el nombre de A).

- `copiauri`: la copia suena entera, «COPIA» en la bitácora, sin «NO PEDIDO» de la copia ni crédito devuelto.
- `nivela`: con 25 medidas inventadas, una a −30 dBFS sube 7,1 dB en sus primeros 30 s sin saltos de más de 0,3 dB por tramo de 100 ms y se
  congela; se mide y se guarda; la segunda vez suena 19,8 dB más fuerte desde el principio y quieta.
- `portadatarde`: con el servidor de portadas tardando 4 s, entra sin portada y al llegar se le pone a la que suena.
- `speq`: una de 34 s con el tono y ruido blanco cortado a 16 kHz («r16» en el id: el Soloist falso lo añade con 16 polos), 60 huellas sanas
  inventadas en `rocola_eq.txt` y sin `spotify_nivela`. La primera vez: sin EQ, se mide entera y se guarda con el corte (17 000 Hz, de la fuente, 88 cuadros: donde el ruido ya está 27 dB
  abajo);
  nada en las tablas de XP. La segunda: su EQ automático ([+1,9 +0,3 −2,7 −2,6 +1,8 +2,7 +0,5 +0,7] dB contra el promedio inventado) y su realce
  con el corte (0,39 en vez de 0,35); lo que sonó cambia como dice el EQ (de 1,5 a 3,5 kHz +1,5 dB contra +1,8 de la banda de 2,5 kHz). Con las dos apagadas:
  nada en la bitácora, la tabla sin tocar y suena como la primera vez (cada banda a 0,3 dB).
- `repetida`: Spotify y luego tu MP3 (no entra, no se cobra), tu MP3 y luego Spotify (no entra, no se cobra), y otro artista con el mismo
  título sí entra.

No probado con Spotify ni Soloist reales ni en el A4.
