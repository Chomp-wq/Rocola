# Spotify: lo que hay que hacer al juntar los grupos (apuntado mientras trabajan)

1. **HECHO en la junta** (abajo, «La junta»). **Encendido de fábrica** (decisión del dueño del 15-11-2026, AGENTS.md): `DeFabrica = true` en la función «spotify»
   (`RocolaOs/RocolaOs.SpotifyPide.cs`, grupo spotify-xp); la orden `spotify` (`rootfs_overlay/usr/bin/spotify`, grupo soloist) dice
   «de fábrica encendido»; `SPOTIFY_SOLOIST.md` ya no pide «spotify encender» (sí explica «spotify apagar»); las pruebas que daban por
   hecho que venía apagado se cambian, y se añade la escena «encendido sin ninguna llave = XP tal cual en lo que se ve y suena, sin
   cobrar nada, sin letra ♫ ni resultados de Spotify».
2. **HECHO** (lo hizo el grupo catalogo al juntar el núcleo; comprobado en la junta: el archivo no está y sus pruebas cuentan las
   pedidas por las «COBRADA» del núcleo). Borrar `RocolaOs/RocolaOs.SpotifyCatalogoDobleI6.cs` (el doble de I6 del grupo catalogo) y cambiar sus comprobaciones de «pedidas».
3. **El buscador con Spotify como lo pidió el dueño el 7-11-2026** (AGENTS.md; lo funcional, sin dibujo nuevo):
   **HECHO y JUNTADO** (rama `paredes-buscador`, `FASE11_paredes-buscador.md`; juntada en `70c0929`; abajo, «La junta final»). Al elegir un
   artista de Spotify se abre su pared (punto 4), no sus discos en la letra.
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
4. **Las paredes del buscador** (decisión del dueño del 15-11-2026, AGENTS.md). **HECHO y JUNTADO** (rama `paredes-buscador`, `FASE11_paredes-buscador.md`, ganchos
   g53-g57; juntada en `70c0929`; abajo, «La junta final»). Va en un grupo propio («paredes-buscador») encima de la
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
6. **HECHO y JUNTADO** (juntada en `30c31d4`; abajo, «La junta final»). **La sustitución invisible** (petición del dueño del 15-11-2026, «si una canción de mi rocola existe en mejor calidad en Spotify, que tome
   esa versión»; rama `sustitucion-spotify`, `FASE11_sustitucion.md`): la función «calidad_spotify» (encendida de fábrica), sus ganchos
   **g60-g62** (`AperturaAudio.cs` 238, `Form1.Sonido.cs` 1288, `NivelDisco.cs` 46) y los cambios chicos en lo del núcleo y del catálogo
   (`SinDevolver` en la fuente, la primera línea de `RocolaOsSpotifyAbre`, `partial` y el pedido «pareja» en el cliente de la API). Al juntar:
   la línea de la petición en `AGENTS.md`, y si las paredes del buscador renumeran ganchos, que no choquen con g60-g62.
7. **HECHO y JUNTADO** (rama `asistente-spotify`, 8564ec8, juntada el 20-11-2026; `FASE11_asistente-spotify.md`). **El asistente con lo nuevo de Spotify** (petición del dueño del 15-11-2026, AGENTS.md): grupo «asistente-spotify» después de juntar
   las paredes del buscador: guías nuevas del asistente con su mismo dibujo, solo con el modo Spotify; sin el modo, el de XP tal cual.
   **HECHO en la rama `asistente-spotify`** (`FASE11_asistente-spotify.md`). Se juntó el 20-11-2026 en `junta-spotify` y se probó con la batería corta (abajo, «La junta del 20-11-2026»).
8. **HECHO y JUNTADO** con el 7 (20-11-2026). **«AYUDA» siempre en la casilla de la ayuda** (petición del dueño del 15-11-2026): con Rocola OS activo, `AyudaCasillaGpu` no va
   compacta en 1024x768 (la cabecera cede el sitio). Lo hace el grupo del asistente (es su casilla). **HECHO en la rama `asistente-spotify`**, juntado con el 7.
9. **HECHO y JUNTADO en la junta 2** (rama `pantalla-ctrlf4`, `FASE11_pantalla.md`; con Navegador y Archivos, `FASE11_navegador-archivos.md`). **Después del cumpleaños**: la ventana de Ctrl+F4 con la hoja «Pantalla» (resolución, hercios, colores del monitor, guardado para
   siempre, con prueba de 15 s y vuelta a lo automático). AGENTS.md, petición del 15-11-2026.

10. **Velocidad de todo lo de Spotify** (petición del dueño del 20-11-2026: «Cuando esté implementado todo lo relacionado a Spotify, busca todas las maneras posibles de que todo cargue lo más rápido que se pueda»): cuando estén juntados el núcleo, el buscador, las paredes del buscador, la sustitución y la pared con el catálogo, un grupo mide y acelera: buscar (de la tecla a los resultados), abrir un disco o la pared de un artista, las portadas (Spotify y Cover Art Archive: adelantar las de la página de al lado, conexiones aparte, caché), pedir y que empiece a sonar (de PONER al primer sonido; precargar la siguiente de la cola en Soloist si su API lo permite), el arranque del modo, y la pared de 500 páginas al hojear; sin gastar más cuota de la API ni frenar la música. Ideas probadas en la demo de Windows (herramientas/prueba-spotify-windows-soloist: portadas en puertos aparte, aviso previo de portadas, biblioteca guardada, buscador al momento) y en el programa anterior (AGENTS.md, petición del 7-11-2026 de optimizar). Medido antes y después, con números.

11. **HECHO y JUNTADO en la junta 2** (rama `junta-2`, `FASE11_JUNTAS.md`; era el punto 6 de la rama `pared-generos`). **Las letras con el catálogo** (`PLAN_PARED_GENEROS.md`, con la decisión del dueño del 20-11-2026: el orden de XP con los discos del
   catálogo intercalados y la pastilla del género en cada casilla, no el orden por género): HECHO en la rama `pared-generos`
   (`FASE11_pared-generos.md`, ganchos g90-g98). **No entra en la compilación del cumpleaños**: se junta después. Al juntar: la prueba del
   catálogo (`Pruebas/RocolaOs/catalogo/prueba.sh`) ya le pasa `ROCOLA_CATALOGO_URL` a la API falsa (si no, con el modo encendido y la llave,
   a los 45 s se miraría GitHub); los ganchos g94-g98 tocan `CaratulasSeparadasGpu.cs` (la casilla de la tarjeta) y g91-g92 `Form1.Pared.cs`:
   si otro grupo tocó esas líneas, se renumera en la junta.

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

## La junta final (15 y 20-11-2026, rama `junta-spotify`)

Sobre la junta de arriba (con la prueba del sistema entero en QEMU, `sistema/ACT_qemu_spotify.md`, y sus dos arreglos `f1e7e1b` y
`a744bf4`) se juntaron, con `git merge --no-ff`, **`paredes-buscador`** (`70c0929`, hasta `c7f73b7`: los puntos 3 y 4, con sus dos
revisiones) y **`sustitucion-spotify`** (`30c31d4`, hasta `4a0ea76`: el punto 6, con su revisión). **`asistente-spotify` no** (puntos 7 y 8:
queda para después). Lo cortó el límite de uso el 15-11 con las dos juntas hechas y compiladas; se siguió el 20-11 sin rehacerlas.

- **Choques resueltos** (como en la junta de prueba `7766186` de la rama del asistente): `GANCHOS.tsv` (g53-g57 de las paredes y g60-g62 de
  la sustitución, sin ids repetidos: **34 ganchos en 15 archivos**); la API falsa del catálogo (los dos juegos de datos; los de la
  sustitución en `art80`/`art81` y `dis080`-`dis082`, porque `art50` ya era «Banda Interminable»); el Soloist falso (`SOLOIST_FALSO_SEG` de
  `a744bf4` y la envolvente de la sustitución). Lo de la sustitución en el núcleo no choca con `a744bf4`: `SinDevolver` en la fuente y la
  primera línea de `RocolaOsSpotifyAbre` (la sustitución antes de mirar si la ruta es `spotify:`); R4 sigue igual.
- **Con el encendido de fábrica** (punto 1): las escenas de las paredes daban `spotify=1` a mano; ahora la escena `sinllave` del catálogo
  quita también `rocola_os.txt` (`305e8c3`): encendido de fábrica, sin llaves, ni una petición y la traza de XP. `apagado` y `cero` siguen
  con `spotify=0` / sin `ROCOLA_OS`. La función `calidad_spotify` (encendida de fábrica) tampoco hace nada sin llaves (escena «fabrica»).
- **La prueba de QEMU**: `spotify.py` daba DERECHA 14 veces para llegar al disco, la última tarjeta; ahora la última es «Todos los discos
  de «entira»» y se abría su pared: va a la última y sube una (`b76ccb3`). Era la prueba, no la rocola.
- **La línea de la petición en `AGENTS.md`** (punto 6) ya está en la rama principal (`9fda01a`); no se repite aquí para que no choque al
  juntar `junta-spotify` con ella.
- **Sin cambios de código** después de las dos juntas: `Codigo/`, `RocolaOs/`, `Nativo/` y `Plataforma/` quedan como en `30c31d4`. Los
  programas probados (JIT y NativeAOT) son los de `30c31d4`.

### La tabla final (20-11-2026; JIT = `dotnet build -c Release --no-incremental`, AOT = `dotnet publish -p:PublishAot=true -r linux-x64`)

La máquina de pruebas, compartida con otros grupos (carga entre 5 y 21 en 4 núcleos, y otra QEMU de otro grupo): lo que falló a la primera
fue de tiempos y se repitió la escena; dice cuál y por qué.

| Prueba | JIT | NativeAOT |
|---|---|---|
| Compilar | 0 errores, **13 avisos = los 13 de `avisos-base.txt`** (archivo, línea y código) | sale con 0, **0 avisos IL/AOT**, los mismos 13 del compilador |
| `ganchos.py` | **TODO BIEN**: 34 ganchos en 15 archivos; `Codigo/` = `fc7b667` devolviéndolos (109 archivos, 13 con ganchos) | (el mismo código) |
| `diff-xp.py --sin-ganchos` (109 archivos) | **5 095 / 4 828 / 229**, las de la base; 32 ganchos devueltos en 13 archivos | |
| `andamio/prueba.sh` | **101 de 101** | **101 de 101** |
| `catalogo/prueba.sh` (27 escenas, con las de las paredes: pared, canciones, todos, mas, atajos, pared429, mantenido, tiempo, hojea, atajos503, sinllave, ayuda, miente, portadas) | **154 de 156** a la primera: `cero` (la traza de `apagado`, que arrancó en frío, perdió sus últimas teclas: lo que ya apuntó la revisión de las paredes) y `fallos` (con la carga, el 429 cayó en «inve» y no en «inven»; después, ni una petición: lo que se quiere). Repetidas `apagado cero sinllave fallos`: **21 de 21** | **154 de 156** a la primera: `cero` y `sinllave` (lo mismo de `apagado`). Repetidas `apagado cero sinllave`: **14 de 14** |
| `spotify-xp/prueba.sh` (15 escenas, con «fabrica» y «noche») | **148 de 149**: `celular`, `no_pedidas=1` en vez de ≥2 (con la carga, los dos «NO PEDIDO» caen en la misma ventana de 1-2 s). Repetida con carga 5: **10 de 10** (`no_pedidas=2`) | **141 de 149** con carga 15-21: `dos` (3 y 1 huecos, saltos en la onda), `celular` (lo mismo) y `noche` (la de Spotify no llegó a cruzar las 23:00 sonando: 0,0 dB). Repetidas `dos celular noche` con carga 10: **49 de 49** |
| `sustitucion/prueba.sh` | **97 de 97** | **96 de 97**: `titulo`, la buena sonó 8,85 s (con la carga) en vez de ≥9; repetida: **6 de 6** (9,95 s) |
| Escena «fabrica» (encendido sin ninguna llave = XP) | BIEN entera (en `spotify-xp`) | BIEN entera |
| Escena «noche» (repetición AOT) | | Spotify **-3,22 dB** con rampa de 11,6 s; XP **-3,22 dB**, 11,5 s; con «Probar» las dos -3,22 dB en 0,9 s, 14,9 s abajo y vuelven solas |
| Nivel 0 (`nivel0.sh`, pasos previo codigo ganchos escenas aot cierre importa; 37 min) | **38 BIEN, 2 ACEPTADO, 0 MAL**: `Codigo/` 109 de 109 con sus líneas, CRLF y último byte; las **24 escenas JIT** IGUAL (tab, salirq y f12vigia a la segunda, solo cuentas o la hora; sigterm y f3sigterm ACEPTADAS: el estado se borra); `cierre_rocola_os.sh` 43 de 43; `Pruebas/Importa` 145 de 145 | las **7 escenas AOT** IGUAL; `cierre_rocola_os.sh` 43 de 43; `Pruebas/Importa` 145 de 145 |
| QEMU, solo Spotify (`spotify.py` entero sobre la máquina instalada, con el `rocola-xp` de la junta final del perfil de QEMU; `sistema/ACT_qemu_spotify.md` §8) | | **56 BIEN, 0 MAL** (prepara 3, limpia 2, sinllave 9, red 19, pide-noche-kill-sonido-fin 23); modo noche -3,22 dB en lo grabado; R5 tras el SIGKILL |

Capturas de QEMU (20 a 25b): `/tmp/claude-0/-home-user-Rocola/b54ee812-4417-50ce-9f97-015b8aeef72d/scratchpad/qemu-spotify-capturas/` y
`/root/rocola-trabajo/act/qemu-spotify/capturas`. Registros: `/root/rocola-trabajo/fase11/junta2` (`bateria.log`, `repite.log`,
`ronda2.log`, `b-*`, `r-*`, `r2-*`, `r3-*`, `n0/resumen.txt`) y `/root/rocola-trabajo/act/qemu-spotify` (`corrida7.log`, `corrida9.log`).

### Lo que NO se probó aquí

- **Nada con el Soloist de verdad** (nunca se ejecutó), **ni con la cuenta real ni la API web de verdad, ni en el A4.** Todo con el Soloist
  falso, la API falsa, llaves inventadas y la tarjeta de prueba con reloj (o QEMU sin KVM), fuera de pantalla con llvmpipe y letras DejaVu.
- De `nivel0.sh`: los arneses, el banco, las cintas, los recorridos, las pruebas de la hoja y la pantalla (`ventanas`) y el nivel 1. El
  código de XP solo cambió en los ganchos, que sin `ROCOLA_OS` devuelven lo de XP (lo comprueban las escenas); quedan para la siguiente
  corrida completa.
- La fase `a4` de `prueba.py` (el `rocola-xp` del perfil del A4 en QEMU) y el resto de `prueba.py` con la junta final.
- Las pruebas de tiempos que fallaron a la primera con la carga solo se repitieron una vez cada una (todas BIEN): con la máquina libre,
  repetir la batería entera de corrido.

### Lo que queda abierto

- ~~El asistente con Spotify y «AYUDA» siempre (puntos 7 y 8)~~: juntados el 20-11-2026 (abajo).
- Lo abierto de cada grupo: `FASE11_paredes-buscador.md` (el tope de aperturas de XP al volver al buscador, «spotify apagar» con alguien
  dentro de una pared, la tarjeta ARTISTA que dice «1 disco», las portadas de `/run` sin tope en la sesión), `FASE11_sustitucion.md` y lo
  de «La junta» de arriba (el vigilante mira la USB cada 30 s sin llave).
- La compilación del sistema y el paso al A4 los hace el coordinador, con permiso del dueño. Esta junta solo cambia `rocola-xp` y los
  guiones de prueba (desde la junta de arriba, la del `rootfs_overlay` -`rocola-soloist` y la orden `spotify`- sigue igual): basta la
  rápida.

## La junta del 20-11-2026 (rama `junta-spotify`, juntada en la rama principal)

Encima de la junta final (`59de02f`) se juntaron:
- `asistente-spotify` (`8564ec8`, ya revisada): el asistente explica lo de Spotify y la casilla de la ayuda dice siempre «AYUDA» (puntos
  7 y 8). Conflictos solo en `GANCHOS.tsv` (se quedan g53-g62 y los del asistente, g70-g84) y en este documento.
- La regla nueva de la sustitución invisible (decisión del dueño del 20-11-2026, AGENTS.md: Soloist suena en «Alta», Ogg Vorbis 160): solo
  sustituye con el corte medido bajo 16 kHz, menos de 128 kbps en CBR o grabada a menos de 44,1 kHz; fuera «128 kbps con corte bajo
  17,5 kHz» y «pre-eco con 192 kbps o menos y corte bajo 19 kHz» (`RocolaOs.CalidadReglas.cs` y su prueba, `FASE11_sustitucion.md`).
- `xp-1718` (`PORT_xp-1718.md`): los cambios de la rocola de XP del 17 y 18-11-2026 pasados a `Codigo/` (el ecualizador del dueño, el
  modo noche desde las 22 h con la bajada de una hora y «Probar» como interruptor, el modo billar sin las cinco etapas bruscas, `NoEsMp3`, la
  entrada de 10 ms tras un corte en la salida ALSA y dos arreglos de esa salida); base nueva de los ganchos `69e6547`.

Batería corta sobre la junta (rocola-xp JIT y NativeAOT, compilados otra vez con todo junto): 13 avisos de siempre, 0 de IL/AOT;
`ganchos.py` TODO BIEN; `diff-xp.py --sin-ganchos` sin líneas nuevas fuera de la traducción; `sustitucion` 97 y 97; `asistente` 29 y 29;
`spotify-xp` 140 de 140 (JIT) y 138 de 140 (AOT: las dos medidas de «Probar» del modo noche, 15,9 s y 13,9 s abajo en vez de unos 15 s,
con la máquina a carga 8; en la corrida de `xp-1718` la misma escena dio 28 de 28 en las dos); `catalogo` 156 y 156. Se repite la escena
«noche» en AOT con la máquina libre antes de dar la compilación por buena.

