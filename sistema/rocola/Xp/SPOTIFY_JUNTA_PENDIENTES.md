# Spotify: lo que hay que hacer al juntar los grupos (apuntado mientras trabajan)

1. **HECHO en la junta** (abajo, «La junta»). **Encendido de fábrica** (decisión del dueño del 15-11-2026, AGENTS.md): `DeFabrica = true` en la función «spotify»
   (`RocolaOs/RocolaOs.SpotifyPide.cs`, grupo spotify-xp); la orden `spotify` (`rootfs_overlay/usr/bin/spotify`, grupo soloist) dice
   «de fábrica encendido»; `SPOTIFY_SOLOIST.md` ya no pide «spotify encender» (sí explica «spotify apagar»); las pruebas que daban por
   hecho que venía apagado se cambian, y se añade la escena «encendido sin ninguna llave = XP tal cual en lo que se ve y suena, sin
   cobrar nada, sin letra ♫ ni resultados de Spotify».
2. **HECHO** (lo hizo el grupo catalogo al juntar el núcleo; comprobado en la junta: el archivo no está y sus pruebas cuentan las
   pedidas por las «COBRADA» del núcleo). Borrar `RocolaOs/RocolaOs.SpotifyCatalogoDobleI6.cs` (el doble de I6 del grupo catalogo) y cambiar sus comprobaciones de «pedidas».
3. **El buscador con Spotify como lo pidió el dueño el 7-11-2026** (AGENTS.md; lo funcional, sin dibujo nuevo):
   - Un disco o lista de Spotify abierto desde el buscador que NO está en la pared: REGRESAR vuelve AL BUSCADOR con lo escrito y la
     misma elección (hoy el grupo catalogo vuelve a la pared, a la letra ♫). Si está en la pared, la pared se queda en él.
   - Artistas de Spotify: solo los que NO están en la rocola (ni por el nombre de un disco tuyo, sin acentos ni mayúsculas), hasta 3,
     justo después de tus artistas que salgan primero (si no hay, arriba); el primero si su nombre se parece a lo escrito (a dos letras),
     los demás si lo contienen. Elegirlo abre sus discos (y lo más conocido que se pueda sacar sin rutas quitadas: tus favoritas de él
     o el orden de la búsqueda `artist:`), y REGRESAR vuelve al buscador. Su foto como portada de la tarjeta de XP. Sin «N seguidores»
     (Spotify quitó followers en 2026).
   - SP9, los atajos del buscador: «tus más escuchadas» (`/v1/me/top/tracks`) y «escuchado hace poco»
     (`/v1/me/player/recently-played`), una petición cada uno por sesión y en memoria; solo con `refresh_token` (si no, no salen).
   - El zoom desde la casilla es visual: a la presentación del final.
4. **Las paredes del buscador** (decisión del dueño del 15-11-2026, AGENTS.md). Va en un grupo propio («paredes-buscador») encima de la
   rama `fase11-catalogo` en cuanto ese grupo termine (toca los mismos archivos), y se junta con la base si está a tiempo:
   - Pared del artista que solo está en Spotify: la rejilla de XP (18 por página, IZQ/DER, ARRIBA/ABAJO de página, PONER abre), fuera de
     la barra de letras; la cabecera dice su nombre. Casilla 1: «Canciones de ARTISTA» (`/v1/search?type=track&q=artist:"..."`, hasta
     50 en 5 páginas de 10, como un disco de XP que se abre y se pide por I6). Luego `/v1/artists/{id}/albums?include_groups=album,single`
     de 10 en 10, pidiendo solo lo que se mira (y la página de al lado).
   - REGRESAR: disco -> su pared (misma página y casilla) -> buscador (lo escrito y la misma elección). Pila de como mucho 4.
   - «Todos los discos de «lo escrito»»: la última tarjeta de los discos de Spotify en el buscador; abre una pared con
     `/v1/search?type=album` de 10 en 10 (offset hasta 1000), solo lo que se mira.
   - Tus artistas: tu tarjeta de siempre igual (lleva a tu pared, como XP) y, justo detrás, «ARTISTA · más en Spotify» (si Spotify lo
     tiene y hay discos que no tienes: sin acentos ni mayúsculas, por nombre de disco) que abre su pared sin los tuyos.
   - Sin el modo, sin la llave de la API o con 429: nada de esto sale (XP tal cual), el aviso de XP si algo no llega.
   - Pruebas con la API falsa: abrir, hojear, regresar en todos los niveles, la cuota (peticiones contadas por escena), 429 a media
     pared, apagado = XP tal cual.
5. **HECHO en la junta** (abajo, escena «noche»). **El modo noche también con Spotify** (pregunta del dueño del 15-11-2026): comprobar con una escena que una canción de Spotify (la
   fuente de `RocolaOs.SpotifyFuente.cs` por `ArmaCadena`) baja al `noche_pct` de noche y con «Probar», con su rampa, igual que una de XP.

## La junta (15-11-2026, rama `junta-spotify`)

Se juntaron, sobre la rama principal: `fase11-andamio` (hasta `3b62e15`), `fase11-pulso`, `fase11-soloist`, `fase11-spotify-xp`,
`fase11-catalogo` (que ya traía el núcleo y había borrado el doble de I6) y, en esta junta, `fase11-nivel0` (la batería del nivel 0, de la
oleada 0; sin choques; su `LEEME.md`, que no estaba en su commit, se copió de su copia de trabajo). Sin conflictos de git; nadie había
compilado ni probado la junta. Quedan fuera (los hace otro grupo en paralelo): los puntos 3 y 4 de arriba (el buscador y las paredes del
buscador). **Nada de esto se probó con el Soloist de verdad (nunca se ejecutó: `/root/rocola-trabajo/fase11/soloist-bin` no se toca), con
la cuenta real ni en el A4**: todo con el Soloist falso, la API falsa, datos inventados y la tarjeta de prueba con reloj, fuera de pantalla
(llvmpipe), en la máquina de pruebas compartida.

### Lo que se cambió

- **Pendiente 1, encendido de fábrica.** `RocolaOs.SpotifyPide.cs`: `DeFabrica = true` de la función «spotify» (con `ROCOLA_OS=1`, la
  del lanzador, va salvo `spotify=0` en `rocola_os.txt`; lo guardado manda). Sin `ROCOLA_OS` (o con `rocola_os_apagado`) nada cambia: la
  función ni se mira. `Pedida("spotify")` no la usa nadie (lo de Spotify decide con `Encendida`, ya con `rocola_os.txt` leído). La orden
  `spotify`: «encendido» salvo `spotify=0` (igual que `RocolaOs.Ajustes.cs`: un valor que no sea 0 ni 1 vale el de fábrica), «de fabrica
  encendido» en `spotify estado` y la ayuda pone primero `spotify apagar`. `SPOTIFY_SOLOIST.md`: ya no pide «spotify encender»; explica
  «spotify apagar» y que sin llaves la rocola es la de XP. `FASE11_spotify-xp.md`: R1 marcada como sustituida.
- **El vigilante esperando la llave** (`rocola-soloist`, lo que hará ahora SIEMPRE una rocola sin llaves): el estado
  (`/run/rocola-soloist/estado`) se escribía cada 2 s (mkdir, date y mv) y `segundos()` abría `cut`; ahora el estado se escribe al
  cambiar de fase (o si alguien lo quitó) y `segundos()` lee `/proc/uptime` sin abrir nada (probado con el BusyBox del sistema). Medido en
  la escena «fabrica»: de 333 programas abiertos en 40 s a 228-257 (los ~200 del arranque son una sola mirada a todos los procesos por si
  quedó un Soloist, `quita_huerfanos`), y esperando, **15 programas en 20 s (los `sleep` de la espera y la mirada a la USB cada 30 s) y
  0,15-0,25 % de un núcleo** con sus hijos.
- **Las pruebas** que daban por hecho que venía apagado: `spotify-xp` «apagado-uno» y la escena «uno» del andamio llevan ahora
  `spotify=0` (lo de «spotify apagar»); en `Pruebas/Soloist`, «sin nada» dice «encendido (de fabrica encendido)» y tres comprobaciones
  nuevas (sin `rocola_os.txt`, encendido; `spotify=x`, encendido; después de apagar, apagado).
- **Escena nueva «fabrica»** (`Pruebas/RocolaOs/spotify-xp/prueba.sh`): `ROCOLA_OS=1` sin `rocola_os.txt`, sin la llave de Soloist ni la
  de la API, con el vigilante DE VERDAD; la misma escena sin `ROCOLA_OS`: moneda, una de XP, vuelta a la pared, el buscador con
  «inventad» y REGRESAR, 40 s y SIGTERM; las dos bajo `strace` (solo `connect` y `execve`, con seccomp). Comprueba la traza igual quitando
  lo de `RocolaOs:` (con una repetición si solo difieren cuentas, como `nivel0.sh`), el mismo sonido, nada cobrado, sin sesión, ni un
  `connect` a internet ni a la API, el vigilante en `sin_llave` y lo que gasta, que se va con la rocola y `rocola_error.txt` igual.
- **Pendiente 5, escena nueva «noche»** (`niveles.py`, y el doble del catálogo aprende `noche-probar`, que llama a `PruebaNoche()` de XP
  como el botón «Probar»): una de Spotify (440 Hz, por la fuente de `RocolaOs.SpotifyFuente.cs` y `ArmaCadena`) y una de XP (un MP3 de
  1000 Hz y 60 s puesto en los datos de prueba), cruzando las 23:00 a media canción (una zona horaria hecha con `zic` en la que la rocola
  arranca a las 22:59:30) y con «Probar» de día. Lo que sonó, de la tarjeta de prueba con reloj.

### La revisión (mirada adversaria)

- **Funciones registradas**: 3 (`prueba-andamio` y `spotify-prueba`, las dos `SoloEnLaLista`, y `spotify`); el catálogo no registra
  ninguna: pregunta `Encendida("spotify")`. Con `ROCOLA_OS=1` la bitácora dice «3 funciones registradas; encendidas: spotify» y ningún
  error de registro (un nombre o una clave repetidos se apuntarían ahí). **Claves de `rocola_os.txt`**: `spotify` (la de encender, la
  declara `Registra`), `spotify_nombre` y las cinco `prueba-andamio.*`; ninguna repetida. Las llaves de la API van en
  `rocola_spotify_api.txt` (con `mercado=`), no en `rocola_os.txt`.
- **Ganchos**: 26 en `GANCHOS.tsv` (g01-g07, g13, g20-g24, g40-g52), ids sin repetir; los del núcleo (g13 y g23 en `Form1.Sonido.cs`,
  g20 en `Form1.Datos.cs`, g24 en `VolumenParejo.cs`) y los del catálogo (g40-g52) en líneas distintas; los ocho de `Form1.Biblioteca.cs`
  son del catálogo, cada uno en su línea. `ganchos.py`: TODO BIEN; `diff-xp.py --sin-ganchos`: 5 095 / 4 828 / 229, las de la base, y los
  109 archivos de `Codigo/` vuelven a `fc7b667` devolviendo los ganchos.
- **Sin `ROCOLA_OS`**: cada gancho mira primero `RocolaOs.Activo` (o algo que XP nunca tiene: una ruta «spotify:», la carpeta
  `/::SPOTIFY::/`, la carpeta de portadas de Spotify, que es null) y devuelve lo de XP; los `[ModuleInitializer]` solo registran. Lo
  comprueban el nivel 0 (abajo) y «apagado-sin» de las dos pruebas.
- **Encendido sin llaves**: la búsqueda no pregunta (sin llave, `OlvidaBusqueda` y lo de XP tal cual), la letra ♫ solo sale con discos de
  la cuenta (sin llave no hay cuenta) y nada pide sin sesión de Soloist. Lo que sí corre: el hilo «spotify» (mira `ws.port` cada 0,5-5 s,
  en `127.0.0.1`), el vigilante, y el anillo de 528 384 bytes en `/run` (memoria). Cierre medido: 23-26 ms para lo de Spotify.
- **Riesgo que queda (de diseño, no de la junta)**: sin llave, el vigilante mira cada 30 s si hay una memoria USB (la monta solo lectura,
  busca `rocola_spotify.txt` y la desmonta; las ya montadas no las toca). Antes solo pasaba con el modo encendido a mano; ahora pasa en toda
  rocola sin llave. Es lo que dice `SPOTIFY_SOLOIST.md`; si molesta, `spotify apagar`.

### Las pruebas (15-11-2026; JIT = `dotnet build -c Release`, AOT = `dotnet publish -p:PublishAot=true -r linux-x64`)

| Prueba | Resultado |
|---|---|
| Compilar rocola-xp, JIT | 0 errores, **13 avisos, los mismos 13 de `avisos-base.txt`** (archivo, línea y código; 0 nuevos) |
| Publicar NativeAOT | sale con 0, **0 avisos IL/AOT**, los mismos 13 del compilador |
| `ganchos.py` | TODO BIEN: 26 ganchos en 12 archivos; `Codigo/` = `fc7b667` devolviéndolos (10 archivos con ganchos) |
| `diff-xp.py --sin-ganchos` (109 archivos) | 5 095 líneas quitadas, 4 828 puestas, 229 sin marca (las de la base); 24 ganchos devueltos en 10 archivos |
| `andamio/prueba.sh` | **JIT 101 de 101; AOT 101 de 101** |
| `spotify-xp/prueba.sh` (14 escenas: las 12 del grupo, «fabrica» y «noche») | **JIT 144 de 144; AOT 144 de 144** (en JIT «fabrica» repitió una vez la de sin `ROCOLA_OS`: solo difería la cuenta de `MedidasBitacora.cs:129`, 990 frente a 882; la segunda, IGUAL; las dos corridas sueltas de antes, 801/1094 y 868/954, en los dos sentidos: ruido) |
| `catalogo/prueba.sh` (13 escenas) | **JIT 74 de 74; AOT 74 de 74** |
| `pulso/prueba/prueba.sh` (completo: TSan, ASan, valgrind) | **16 pasos bien, 0 mal** (0 avisos del compilador; 757 comprobaciones del cliente, 113 raras, 24 de hilos) |
| `Pruebas/Soloist/prueba.sh` (BusyBox, curl y jq del sistema, `salida-qemu/target`, en chroot) | **103 BIEN, 0 MAL** (188 s), con el vigilante cambiado |
| Nivel 0 (`nivel0.sh`, sin `ROCOLA_OS`, contra la referencia congelada; con el programa ya compilado y publicado de la junta; pasos previo, codigo, ganchos, escenas, aot, cierre, importa, cintas, ventanas) | **43 BIEN, 2 ACEPTADO, 0 MAL** (sale con 0): `Codigo/` 109 de 109 con sus líneas, CRLF y último byte; las **24 escenas JIT** (22 IGUAL; sigterm y f3sigterm ACEPTADAS: el estado se borra, lo de siempre) y las **7 NativeAOT** IGUAL; `cierre_rocola_os.sh` 43 de 43 en JIT y en AOT; `Pruebas/Importa` 145 de 145 en JIT y en AOT; cintas de la tarjeta 29 iguales (6 616 cuadros); hoja 86, pantalla 32 (JIT) y 32 (AOT), PantallaVistas 78, sin fallos |
| Nivel 1 (`NIVEL=1 ROCOLA_OS_LISTA=1`: `ROCOLA_OS=1`, lo del lanzador, con **spotify encendido de fábrica**; sin red; comparando sin «RocolaOs:») | 24 escenas JIT: **20 IGUAL** (2 a la segunda, solo cuentas), **3 ACEPTADAS** (tab: los rótulos del repaso; sigterm y f3sigterm: el estado se borra) y **salirq MAL «solo cuentas»** (la cuenta del reloj `Form1.cs:724`, con holgura de 4 ticks); las **7 NativeAOT IGUAL**. salirq es la carga de la máquina, no la junta: con la carga a 11-13 (otros grupos trabajando) se corrió alternando 3 veces con y sin Spotify (`ROCOLA_OS_LISTA=1` y `vida,red`, sin repetir): **con Spotify 2 de 3 MAL, sin Spotify 3 de 3 MAL**, y la diferencia va en los dos sentidos (186/194, 155/183 con; 177/191, 186/166 sin). Queda por repetir con la máquina libre |
| Arnés `VistasAjustes` (compila `Codigo/`, `Nativo/`, `Plataforma/` y `RocolaOs/` de la junta con su propia entrada) | compila; **0 distintas de 545** con las letras falsas y con DejaVu, 0 con excepción. Los otros 16 arneses, el banco y los recorridos NO se corrieron aquí (unos 80 min más; lo que compilan de `Codigo/` solo cambia en los ganchos, que sin `ROCOLA_OS` devuelven lo de XP -lo comprueban las escenas del nivel 0-, y `RocolaOs/` ya compila en este arnés: queda para la siguiente corrida completa de `nivel0.sh`) |
| Escena «fabrica» (JIT / AOT) | traza IGUAL quitando `RocolaOs:` (39 ticks del latido); sonó lo mismo (2,90 s y 2,90 s, los mismos tonos); `cobradas=0`, `esperando_credito=0`, `sesion=0`, ni un COBRADA; **`connect` a internet: 0 y 0**; ni uno a la API de prueba; el vigilante `fase=sin_llave` y rocola-xp `vigilante=sin_llave`; de la rocola cuelga 1 proceso (el vigilante) y de él solo su `sleep`; **esperando: 0,15 % (JIT) y 0,15 % (AOT) de un núcleo, 15 programas en 20 s**; ni Soloist ni curl; el vigilante se va con la rocola; `rocola_error.txt` del mismo largo (9 y 9); en las capturas (disco, pared, buscador) la misma pantalla que XP: sin ♫ en la barra, «Sin resultados» en el buscador, 3 créditos (solo cambian las estrellas del cielo) |
| Escena «noche» (JIT / AOT) | de noche: Spotify **-3,22 dB** con rampa de 11,6 s; XP **-3,22 dB**, 11,6-11,7 s (la rampa de XP: 12,4 s de 1 a 0,69, medida entre 0,1 dB de cada punta); con «Probar»: Spotify y XP **-3,22 dB** en 0,8-0,9 s, **14,8-14,9 s abajo** y vuelven solas a lo de antes (±0,00 dB) en 0,9 s. Diferencia Spotify - XP: 0 dB y ≤ 0,1 s |

### Lo que queda abierto

- **Nada probado con el Soloist de verdad, con la cuenta real ni en el A4.** La prueba corta en el A4 sigue siendo lo primero (decisión
  del 14-11-2026: «de Spotify, lo que no se ve va ya, con la prueba corta en el A4 primero»).
- Los puntos 3 y 4 (el buscador y las paredes del buscador): del otro grupo; al juntarlos, volver a correr esta batería entera.
- `nivel0.sh` completo con la máquina libre: faltan aquí los 16 arneses que no se corrieron, el banco y los recorridos, y volver a correr
  salirq del nivel 1.
- La junta no está en QEMU ni compilada para el sistema (lo hace el coordinador, con permiso del dueño); el cambio de `rocola-soloist`
  pide solo la compilación rápida (es del `rootfs_overlay`).
- Sin llave, el vigilante mira la USB cada 30 s (arriba, «Riesgo que queda»).
