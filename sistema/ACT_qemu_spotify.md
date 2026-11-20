# La junta de Spotify: el sistema entero en QEMU (grupo qemu-spotify)

15-11-2026. Rama `junta-spotify` (la junta ya compilada y con su batería: `sistema/rocola/Xp/SPOTIFY_JUNTA_PENDIENTES.md`, «La junta»).
Como al activar `rocola-xp` (`sistema/ACT_qemu.md`): Rocola OS entero, de punta a punta, en una máquina emulada, ahora con Spotify
**encendido de fábrica** (decisión del dueño del 15-11-2026). **Todo inventado**: el Soloist **falso** (nunca el de verdad), la API web
falsa del catálogo, llaves inventadas en una memoria USB de mentira, tonos en vez de canciones. QEMU sin KVM **no es el A4** (otra gráfica,
otro sonido, otro procesador, y la pantalla unas 100 veces más lenta): nada de esto dice cómo va en el A4. Nada se lanzó en GitHub ni se
hizo push.

Lo empezó un agente que cortó el límite de uso (10:00); este documento recoge lo suyo y lo de después. Los guiones están en
`sistema/rocola/Pruebas/Qemu` (`spotify.py`, nuevo; `LEEME.md` dice cómo repetirlo); lo que dejaron (registros, capturas, lo grabado) en
`/root/rocola-trabajo/act/qemu-spotify`, fuera del repositorio.

## 1. La imagen

Como la armaría `sistema/rapida.sh` con «rapida_ya» sobre la última imagen **completa del perfil de QEMU**
(`/root/rocola-trabajo/salida-qemu/images`, b6e8f14): `arma.sh` pone el `rootfs_overlay` entero de la junta, el menú, el `initrd.gz` con el
`init` de ahora, `rocola-xp` (NativeAOT, perfil de QEMU) con `libSkiaSharp.so` y su versión, y **`rocola-pulso` como el paso 5b de
`rapida.sh`** (nuevo en `arma.sh`): compilado aquí con las mismas comprobaciones, `/usr/lib/rocola-pulso/libpulse.so.0` con **sus 48
funciones** y sin pedir una glibc que el sistema no tenga. Se instaló desde la memoria USB en el disco de XP falso, como en `ACT_qemu.md`.

**Solo para la prueba** (no va en Rocola OS; la imagen lleva `/etc/rocola-prueba-qemu`): root en el puerto serie, como antes; y para
Spotify, `/usr/lib/rocola-prueba/reenvia` (`reenvia.c`) y una línea al principio de `/etc/init.d/S99rocola` que lee
`/etc/rocola-prueba-entorno` si existe (sin ese archivo no cambia nada). La máquina (`maquina.py`, `red=`) lleva una tarjeta de red con la
red de usuario de QEMU **aislada** (`restrict=on`: ni internet ni el anfitrión), salvo `10.0.2.100:PUERTO`, que QEMU lleva a
`127.0.0.1:PUERTO` del anfitrión, donde están los dos servidores de mentira: el **CDN falso** de Soloist (`Pruebas/Soloist/servidor.py`,
sirve el Soloist falso como `soloist_release_x86_64.tar.gz`) y la **API web falsa** del catálogo (`Pruebas/RocolaOs/catalogo/ApiFalsa`).
Dentro, `reenvia` pone esos puertos en `127.0.0.1` y las variables de prueba de `SPOTIFY_SOLOIST.md` y `FASE11_catalogo.md`
(`SOLOIST_URL`, `ROCOLA_SPOTIFY_APRESOLVE`, `ROCOLA_SPOTIFY_API`, `ROCOLA_SPOTIFY_CUENTAS`) apuntan a ellos; `SOLOIST_FALSO_PULSO=1` hace que
el Soloist falso escriba por **nuestro** `libpulse.so.0` (el de `LD_LIBRARY_PATH`, el que le pone el vigilante), como el de verdad.

Al acabar, para volver a probar con los arreglos de abajo, el `rocola-xp` (NativeAOT, a744bf4), la orden `spotify` y el Soloist falso
nuevos se pusieron encima del sistema ya instalado (por el CDN falso, como hace la fase `a4` de `prueba.py`): no se rehízo la memoria USB.

## 2. Las pruebas de la activación (`prueba.py`), con la junta

Las fases de siempre sobre la imagen de la junta (Spotify encendido de fábrica y sin llaves: lo que llega al bar), corridas por el agente
anterior: `instala arranque teclas pared video consola caida apagon` (07:54-08:22, la corrida se cortó en `apagon` tras 2 comprobaciones)
y otra vez `apagon ia boton` (08:30-08:42). **57 de 57 BIEN, 0 MAL** (43 + 16, sin contar dos veces las 2 primeras de `apagon`):
instalar y GRUB en el hueco de 62 sectores (31 284 de 31 744 bytes), el primer arranque con la prueba de sonido y gráfica, el importador
de XP (6 tablas, rutas traducidas, sin el estado del día, la carpeta de XP sin tocar), las letras de XP del disco de Windows, la pantalla
por KMS, la moneda y una de XP que **suena** (45 s de 196 Hz en lo grabado), lo más sonado, la pared con la tarjeta, el video (99 s de
880 Hz y las barras en pantalla), la consola de mantenimiento (Ctrl+Alt+F2 y F1 con el estado), la caída (`kill -SEGV`, se vuelve a
abrir), el apagón (reanuda la que sonaba y la de la cola), la confirmación del sistema nuevo a los 120 s, lo grande de la IA en
`ia-xp` y el botón de encendido (borra el estado del día y apaga).

Frente a los **58 de 59** de `ACT_qemu.md`: aquel MAL era de la prueba (ya corregido) y aquella corrida llevaba la fase `a4` (el
`rocola-xp` del perfil del A4 en el SandyBridge emulado), que **no se corrió aquí** (hay que compilar el del A4 y armar `a4.img`; la junta
no cambia nada de las instrucciones del procesador); y esta lleva `pared` (añadida en la revisión de `ACT_qemu.md`). Con Spotify
encendido y sin llaves, todo lo de XP salió igual.

## 3. Spotify (`spotify.py`)

Las fases: `prepara` (el `.tar.gz` del Soloist falso, la memoria de las llaves y los dos servidores), `limpia` (la rocola instalada sin
nada de Spotify y sin créditos: el botón de encendido), `sinllave`, `red`, `pide`, `noche`, `kill`, `sonido` y `fin`. La corrida que
vale (las de antes sirvieron para encontrar los fallos del apartado 4):

| Fase | Comprobaciones | Resultado |
|---|---|---|
| prepara (14:06) | el Soloist falso compilado; la memoria USB (FAT32, MBR) con `rocola_spotify.txt` y `rocola_spotify_api.txt`; la API falsa y el CDN falso contestan | 3 BIEN |
| limpia (14:07) | sin llaves, sin el programa bajado ni `rocola_os.txt`; apagada con el botón | 2 BIEN |
| sinllave (14:08-14:10) | ver abajo | 8 BIEN |
| red (14:11-14:12) | ver abajo | 17 BIEN, 1 MAL (de la prueba: el Soloist falso no contestaba a un GET normal; arreglado y comprobado después) |
| pide (15:57-16:03) | ver abajo | 9 BIEN |
| noche (16:03) | el guion pasa el reloj a las 23:00:05 con la primera de Spotify sonando (el nivel se mira en `sonido`) | 1 BIEN |
| kill (16:03-16:07) | ver abajo | 8 BIEN |
| sonido (16:07) | ver abajo | 4 BIEN |
| fin (16:07-16:08) | ninguna llave fuera de sus dos archivos; `rocola_error.txt` sin nada (quitando las paradas de pantalla de QEMU) | 1 BIEN |

La corrida que vale de `pide noche kill sonido fin` (`corrida6.log`, sobre `rocola-xp` a744bf4 en NativeAOT, perfil de QEMU): **23 BIEN,
0 MAL**; con `prepara limpia sinllave red` (30 BIEN, 1 MAL de la prueba, ya arreglado), **53 de 54**, y el que falló era de la prueba.

**2. Encendido de fábrica, sin llaves** (`sinllave`): el lanzador abre `rocola-xp` con `ROCOLA_OS=1` sin `rocola_os.txt`; el vigilante
(`rocola-soloist corre`, hijo de `rocola-xp`) queda en **`fase=sin_llave`** («falta la llave de Soloist»); no se baja ni se arranca nada
de Soloist; `rocola-xp` sin sesión ni nada cobrado; el catálogo `api=sin_llave` y **0 peticiones**; sin llave el CDN falso no recibió
**ninguna** petición del `.tar.gz` (0). La pantalla, la de XP (`20-sin-llave.png`); y suena como XP: es la imagen de las 57 de
`prueba.py`. «spotify estado» dice «Modo Spotify: encendido (...; de fabrica encendido)», «Spotify (Soloist): esperando la llave
(rocola_spotify.txt en una memoria USB, o «spotify llave»)», «Llave de Soloist: FALTA», «Llave de la API web: falta (sin ella no se puede
buscar en Spotify desde la rocola)» y que no hay internet; «spotify prueba» sale con error y dice «MAL: falta la llave de Soloist
(«spotify llave»)», «MAL: falta la llave de la API web», «MAL: el programa de Spotify no esta bajado» y «BIEN: el puente de sonido
(rocola-pulso)». Con eso apareció el fallo de `od` (apartado 4, a).

**3. Con las llaves inventadas** (`red`): con la red aislada, `curl` a `1.1.1.1` no llega; los dos servidores de mentira sí, por
`127.0.0.1`; `rocola-xp` con las variables de prueba. Enchufada la memoria USB en caliente, «spotify llave» pone las dos llaves en 13 s:
**0600** las dos, la de Soloist limpia (sin los espacios ni el CRLF del Bloc de notas), la de la API con sus tres renglones, la memoria se
desmonta (solo se leyó). El vigilante **baja el Soloist falso del CDN falso** (GET del `.tar.gz`) en 4 s, lo guarda en
`/rocola/disco/rocola-os/soloist/soloist.tar.gz`, lo saca a `/run` y lo arranca: **un solo Soloist**, con
`LD_LIBRARY_PATH=/usr/lib/rocola-pulso` y **nuestro `libpulse.so.0` en sus mapas**; la llave va en `-k`. `rocola-xp` habla con él por el
WebSocket (`ws=1 sesion=1 activa=1`). «spotify prueba»: 8 BIEN y 1 MAL, «El WebSocket de Soloist: NO contesta»: era del Soloist falso, que
cerraba sin contestar un GET que no es de WebSocket (uno de verdad contesta 400 o 426); ahora contesta 426 (comprobado: `curl` da 426).
**Ninguna llave** fuera de sus dos archivos (ni en las bitácoras, ni en el estado, ni en `/run`) en ninguna corrida.

**Pedir, sonar, cobrar, la cola** (`pide`, `kill`, `sonido`; con el arreglo de R4). Todo con las teclas de la rocola por el monitor de QEMU
(evdev), como `prueba.py`, de una máquina recién encendida con las llaves de `red`:
- la moneda da 4 y «01 Pulso Grave» (XP, 196 Hz) cobra 1 (21 -> 24);
- REGRESAR+ARRIBA abre el buscador; «entira» pregunta a la API falsa (`q=entira`) y salen la canción «Tema de Mentira» y el disco de
  Spotify (`21-buscador-spotify.png`); el catálogo dice `api=bien llave=si cuenta=si` con la llave de la API que llegó por la USB, **sin
  reiniciar la rocola**;
- PONER en la tarjeta del disco lo abre (la API falsa da `GET /v1/albums/dis000`) en la vista de disco de XP (`22-disco-spotify.png`, sus
  8 canciones); PONER y ABAJO+PONER piden dos de Spotify: `COBRADA` en la bitácora del núcleo y **un crédito cada una** (24 -> 22); en
  `rocola_estado.txt` van como `sonando=S|spotify:track:can0000` y `cola=S|spotify:track:can0001` (lo pagado: vuelve tras un apagón);
- REGRESAR deja la pared en la letra ♫ («SPOTIFY» en la cabecera, 32 discos y listas; `23-pared-letra-spotify.png`, con el aviso «Modo
  noche: el volumen baja al 80 %» de la fase `noche`); dos letras a la izquierda, otra de XP (`prueba.mp3`, 220 Hz) a la cola, después de
  las dos de Spotify (22 -> 21);
- en lo grabado de la tarjeta de sonido de la máquina (el WAV de QEMU): **0-45 s 196 Hz** (la de XP), **46-198 s 300 Hz** (las dos de
  Spotify, 75 s cada una: el tono del Soloist falso, que escribe por **nuestro `libpulse.so.0`** al anillo y la rocola lo saca por su
  cadena y su ALSA), **198-201 s 220 Hz** (la de XP de la cola) y **202-240 s 300 Hz** (la de `kill`): **Spotify suena por la salida de la
  rocola y la cola sigue en su orden**; nada del «autoplay» del Soloist falso (990 Hz) se oye (la rocola lo para: «NO PEDIDO ... pausa, no
  suena ni se cobra»);
- desde la letra ♫ (REGRESAR+DERECHA dos veces), una moneda y dos de «Tus favoritas» (`25b-disco-spotify.png`): cobradas, un crédito cada
  una (21 -> 23), y suena la primera (`24-suena-spotify.png`: «04 Otra Tonada 4 ► SONANDO», «05 Otra Tonada 5 EN COLA 1», abajo lo que suena
  con su portada y el siguiente).

**El modo noche** (de fábrica: 23 a 7, al 80 % = -3,2 dB). Con la primera de Spotify sonando, un guion de la máquina pasa el reloj a las
23:00:05; sale el aviso de XP «Modo noche: el volumen baja al 80 %». En lo grabado, el tono de 300 Hz: la primera de Spotify, de día,
**-15,1 dBFS**; la de `kill`, pedida ya de noche, **-18,3 dBFS: -3,22 dB**, lo mismo que a las de XP. Lo que en QEMU **no** se ve: la rampa
de 20 s a media canción. Con música la pantalla de QEMU se para 6 a 7 s en cada vuelta (`rocola_error.txt`: «Pantalla parada 6848 ms ... el
reloj de la cancion») y XP no da un paso de rampa de más de 5 s (`AvanzaModoNoche`: «el equipo estuvo dormido: no dar un salto»), así que
la rampa solo avanza con la rocola callada (vueltas de 3 a 4 s): la que sonaba al pasar las 23:00 y la siguiente se quedaron a -15,1 y
la de 15 min después ya salió baja. Es de QEMU (en el A4 la vuelta es de unos 40 ms) y de XP tal cual; la rampa a media canción con una de
Spotify la mide la escena «noche» de `spotify-xp` en el anfitrión (la junta: TODO BIEN).

**SIGKILL con una de Spotify sonando y otra en la cola** (`kill`): en el disco, `creditos=23`, `sonando=S|spotify:track:can0003` y
`cola=S|spotify:track:can0004`; `kill -9` a `rocola-xp` (en la misma orden que copia el estado). El lanzador la vuelve a abrir (496 ->
6584) y apunta «rocola-xp se cayo (codigo 137)»; el vigilante viejo se fue con su padre («murio su padre, la rocola; Soloist cerrado en 1
s»), hay uno nuevo y **un solo Soloist**; al volver **no se pide nada a Soloist** (ningún `play`). `RestoreState` de XP no encuentra las
dos de Spotify en la biblioteca y **devuelve su crédito** (R5) en memoria; con una moneda después, el estado escrito dice `creditos=29`
(23 + 2 + 4) y **nada de Spotify** (en la corrida anterior se vio también en pantalla: «Creditos 17» = 15 + 2, antes de la moneda).

## 4. Lo que se encontró

**a) La orden `spotify` (de la rocola: arreglado, f1e7e1b).** «spotify prueba» y «spotify estado» decían `arithmetic syntax error` con
el anillo de sonido recién creado por la rocola (todo a cero salvo la magia): el `od` de BusyBox pone «*» en las líneas repetidas, el «*»
se volvía los nombres de los archivos de la carpeta de trabajo y corría los números. Ahora `od -v`. La prueba de `Pruebas/Soloist` lo
reproduce (desde una carpeta con varios archivos con guion: con la orden de antes, MAL) y pasa con la nueva: `prueba` y `estado`, 7 BIEN,
0 MAL, con el BusyBox del sistema.

**b) R4 devolvía la de Spotify cuando la que no leía era la rocola (de la rocola: arreglado, a744bf4).** En QEMU la primera de Spotify
que se pedía con la rocola callada se devolvía **3 de 3 veces**: «acaba ...: el anillo sin sonido mas de 5 s», «DEVUELTA ... sono 0.3 s»
(y la siguiente, pedida justo después, sonaba bien). Al arrancar la canción la pantalla se quedó parada 6,3 s («Pantalla parada 6269 ms,
despues de: arrancar la cancion ya abierta»); en ese rato la rocola no leía el anillo, Soloist no podía escribir más (rocola-pulso solo le
deja unos 250 ms por delante: «sono 0.3 s») y R4 culpaba a Soloist. El crédito volvía bien, pero la canción no sonaba. Ahora los 5 s
solo cuentan con el anillo **vacío para la rocola** (`Anillo.Pendientes == 0`): si hay sonido esperando a que la rocola lo lea, no es
Soloist. Escena nueva **«lento»** en `Pruebas/RocolaOs/spotify-xp` (la tarjeta de prueba tarda 7 s en arrancar:
`RELOJ_ARRANQUE_LENTO_MS`, nuevo en `Pruebas/Windows/alsa/reloj.c`): con el programa de antes, **3 MAL de 5** (devuelta, 0,25 s de
440 Hz); con el arreglo, **la batería entera de spotify-xp en JIT: TODO BIEN (149 comprobaciones, 15 escenas)**, «fallos» incluida (el
corte de verdad, Soloist que deja de escribir con la rocola leyendo, se sigue viendo). Compila con los 13 avisos de siempre; NativeAOT
(perfil de QEMU) sin avisos IL/AOT; `ganchos.py`: TODO BIEN. En el A4 la pantalla no se para 5 s al arrancar una canción, pero un
tirón largo (la primera vez que abre un disco grande, el recolector) podía dar lo mismo: era un riesgo para la fiesta.

**c) Los 7 MAL de la corrida del agente anterior (spotify.txt), uno por uno:**

| MAL | De quién | Qué era |
|---|---|---|
| 1. llegar a los servidores falsos; `curl 7` al bajar Soloist | de la prueba | `reenvia` se lanzaba por el puerto serie con la máquina aún arrancando y a veces no llegaba a correr (`Failed to connect to 127.0.0.1` = nadie escuchaba). Ahora lo abre `S99rocola` desde `/etc/rocola-prueba-entorno`, antes que la rocola: escucha desde el principio y el vigilante baja Soloist en 4 s |
| 2. sin llave «sí se pidió /apresolve» | de la prueba | esos dos `GET /apresolve` eran del `curl` de la propia prueba («y llega a los servidores...»). El vigilante sin llave no pide nada (0 peticiones del `.tar.gz`; y la escena «fabrica» de spotify-xp, bajo strace, 0 `connect` a internet). Como dice SPOTIFY_SOLOIST.md: sin llave no gasta red. Solo «spotify estado» y «spotify prueba» miran internet, y solo cuando el dueño las pide |
| 3. «las dos de Spotify cobradas» con créditos 3 -> 5 | de la prueba | la bitácora dice «COBRADA ... quedan 6 creditos»: había 7 al pedir, no 3: entre medias entró una moneda de la corrida de antes que la prueba no vio (y la cuenta de la prueba leía también las COBRADA de corridas anteriores). Ahora la prueba lee solo la bitácora desde su marca, mira los créditos justo antes de pedir y arranca de una máquina recién encendida; en la corrida que vale: 16 -> 14, un crédito cada una |
| 4. el reloj no pasó a las 23:00 | de la prueba | el guion de la noche esperaba a `can0000`, que en esa corrida no llegó a sonar (se devolvió: b). Ahora espera a la primera de Spotify, va en `/run/noche.sh` con `setsid` y la prueba comprueba que está esperando; el reloj pasó a las 23:00:05 en todas las corridas después |
| 5. «y suena» la pedida desde la letra ♫ | de la prueba | la canción del Soloist falso duraba 30 s y la prueba la buscaba 2 min después (en QEMU cada tecla tarda): ya había acabado («acabo ... sono 30.0 s»). Ahora duran 75 s (`SOLOIST_FALSO_SEG`) y la prueba mira también «suena» en la bitácora |
| 6. tras el SIGKILL, créditos 7 -> 7 y «nada de Spotify en el estado» | de la prueba | no sonaba ni esperaba nada de Spotify al matarla (la de 30 s había acabado), así que 7 -> 7 es lo correcto: R5 devuelve lo pagado que no sonó. Ahora la prueba pide dos (una suena, otra en la cola), copia el estado y manda el SIGKILL en la misma orden, y espera `creditos + 2` |
| 7. el modo noche: -0,00 dB | de la prueba (y de QEMU) | la prueba medía una de las que ya sonaban al pasar las 23:00, y en QEMU la rampa no avanza mientras suena algo (ver «El modo noche»). En lo grabado de esa misma corrida, la de después de `kill` ya estaba a -3,2 dB (-18,3 frente a -15,1 dBFS). Ahora se mide la última, pedida de noche: -3,22 dB |

Y los 4 MAL de la siguiente corrida (`corrida5.log`, 19 BIEN), todos de la prueba:

| MAL | De quién | Qué era |
|---|---|---|
| «después de las dos de Spotify suena la de XP de la cola» (15 min) | de la prueba | la de XP de la cola (`prueba.mp3`) dura 3 s y la prueba miraba `sonando=` cada pocos segundos: sonó entre dos vistazos (en lo grabado: 198-201 s, 220 Hz). Ahora vale «sonando o ya sonada» y lo que sonó lo dice lo grabado |
| «la cola sigue: 196 -> 300 -> 300» | de la prueba | buscaba tramos de 6 s o más y la de XP dura 3. Ahora de 2 s: 196 -> 300 -> 220 -> 300 |
| «al volver, lo pagado de Spotify vuelve como crédito (15 + 2)»: 15 -> 15 y lo de Spotify aún en el estado | de la prueba | `RestoreState` de XP devuelve el crédito **en memoria** y no reescribe `rocola_estado.txt` hasta el siguiente cambio (como XP). Se comprobó en esa misma máquina: la pantalla decía «Creditos 17» (15 + 2) y, con una moneda, el archivo `creditos=21` sin nada de Spotify. Ahora la prueba echa una moneda al volver y espera lo de antes + 2 + 4 |
| el modo noche: -0,00 dB | de la prueba | la medida de la «segunda» caía en el tramo de las que sonaban al pasar las 23:00 (ver el 7 de arriba) |

Ningún MAL fue de la rocola en estas dos corridas: lo que sí era de la rocola (a y b) ya estaba arreglado.

Y de las corridas de esta tarde, también de la prueba: la letra a la izquierda de ♫ es VIDEO y un video ocupa la pantalla entera (las
teclas solo la despiertan): «otra de XP» se pide ahora dos letras a la izquierda; y un PONER que se pierde en QEMU se repite a los 90 s.

## 5. Las capturas

En `/root/rocola-trabajo/act/qemu-spotify/capturas` (y copiadas en la carpeta de capturas de la sesión), todas con el dibujo por
procesador (`gpu=0`, como deja la fase `sinllave`; con la tarjeta de QEMU un cuadro tarda aún más):
- `20-sin-llave.png`: Spotify encendido de fábrica sin llaves: la pared de XP;
- `21-buscador-spotify.png`, `21b-buscador-elegido.png`: el buscador de XP con «entira»: «CANCION · Tema de Mentira» y «ALBUM» de Spotify;
- `22-disco-spotify.png`: el disco de Spotify abierto en la vista de disco de XP, con su portada (de la API falsa) y sus 8 canciones;
- `23-pared-letra-spotify.png`: la pared en la letra ♫ («SPOTIFY»: tus favoritas, discos, listas) con lo que suena de Spotify abajo y el
  aviso del modo noche; `23b-pared-otra-letra.png`, `25-pared-letra-spotify-otra-vez.png`, `25b-disco-spotify.png`;
- `24-suena-spotify.png`: lo que suena con una de Spotify («► SONANDO», «EN COLA 1», su portada, su artista y el siguiente);
- `26-disco-xp-gpu0.png`: un disco de XP en la misma máquina, para comparar: el título de arriba del disco sale **medio tapado** igual que en
  los de Spotify (`22`, `24`, `25b`), así que no es de la junta; no se comparó aquí con la copia de referencia con `gpu=0` (queda abierto).

## 6. Lo que NO se probó

- **El Soloist de verdad**: nunca se ejecutó (ni el binario bajado en `/root/rocola-trabajo/fase11/soloist-bin`). Todo con el Soloist
  falso, que imita lo que documenta Soloist (WebSocket, `-k`, `ctl status`) y escribe por nuestro `libpulse.so.0`; cómo se porta el de
  verdad con `rocola-pulso` (cuándo abre su flujo, cuánto escribe por delante, qué hace con la red lenta) no se sabe hasta el A4.
- **La cuenta real de Spotify y la API web de verdad**: llaves inventadas y la API falsa del catálogo. Ni el emparejado con el celular,
  ni la cuota del modo desarrollo, ni las portadas de Spotify de verdad.
- **El A4**: ni su gráfica, ni su sonido, ni su procesador, ni su red. QEMU es unas 100 veces más lento en pantalla.
- **La rampa del modo noche a media canción en el sistema entero**: en QEMU no avanza mientras suena algo (ver arriba); se vio el nivel de
  la noche (-3,22 dB) en una de Spotify pedida de noche, y la rampa la mide la escena «noche» de `spotify-xp` en el anfitrión.
- **La fase `a4` de `prueba.py`** no se corrió (ver el apartado 2).
- Internet de verdad: la red estaba aislada; el vigilante nunca habló con `soloist-builds.spotifycdn.com` ni con `apresolve.spotify.com`.

## 7. Lo que queda abierto

- Nada de esto se probó con el Soloist de verdad, con la cuenta real de Spotify ni en el A4 (apartado 6). Lo primero en el A4: la prueba
  corta de Spotify (`spotify prueba`) con la llave de verdad, y oír una de Spotify y una de XP alternadas.
- El título de arriba de la vista de disco sale medio tapado con `gpu=0` en QEMU, en los discos de XP y en los de Spotify por igual
  (`26-disco-xp-gpu0.png`): mirarlo contra la copia de referencia (aef2211) con `gpu=0` antes de darlo por bueno o por fallo.
- La fase `a4` de `prueba.py` (el `rocola-xp` del perfil del A4) no se corrió con la junta.
- Los puntos 3 y 4 de `SPOTIFY_JUNTA_PENDIENTES.md` (el buscador y las paredes del buscador) aún no están en la junta: al juntarlos, repetir
  `spotify.py pide noche kill sonido fin` (unos 11 min sobre la máquina ya instalada) y `prueba.py`.
- Para repetir: la máquina instalada queda en `/root/rocola-trabajo/act/qemu-spotify` (`xp.qcow2` sobre `discoxp.img`); la memoria de
  instalar (`usb.img`) se borró para ahorrar disco y se rehace con `arma.sh`.


## 8. Otra vez, con la junta final (20-11-2026)

Con las paredes del buscador y la sustitución invisible ya juntadas (`SPOTIFY_JUNTA_PENDIENTES.md`, «La junta final»), solo la parte de
Spotify: `spotify.py` entero sobre la máquina ya instalada. Como al final del apartado 1, el `rocola-xp` nuevo (NativeAOT, perfil de QEMU,
compilado de la junta final: `rocola-xp programa 0 (local) compilado 1795155866`) se puso encima del sistema instalado por el CDN falso
(sha256 comprobado dentro: igual), porque desde la imagen de `arma.sh` del apartado 1 solo cambió `rocola-xp` (ni `rootfs_overlay`, ni
`rocola-pulso`, ni el `init`); el Soloist falso (NativeAOT) y la API falsa se recompilaron de la junta final.

| Fase | Resultado |
|---|---|
| prepara, limpia, sinllave, red (corrida7) | **3 + 2 + 9 + 19 BIEN, 0 MAL** (sin llaves: el vigilante en `sin_llave`, 0 peticiones al CDN y a la API, «spotify estado» dice «encendido (de fabrica encendido)»; con la memoria: llaves 0600 en 10 s, Soloist falso bajado y en marcha en 4 s con nuestro `libpulse.so.0`, «spotify prueba» 9 BIEN 0 MAL) |
| pide, noche, kill, sonido, fin (corrida9) | **23 BIEN, 0 MAL**: una de XP (créditos 5 -> 8 con la moneda), el buscador «entira» a la API falsa, el disco de Spotify abierto, dos de Spotify a un crédito cada una (8 -> 6), otra de XP en la cola (6 -> 5); el reloj a las 23:00:05 con la primera de Spotify sonando; tras el SIGKILL, R5 (7 + 2 + 4 = 13) y ni un play a Soloist; en lo grabado 196 -> 300 -> 220 -> 300 Hz, sin el autoplay de 990 Hz, y el modo noche **-3,22 dB** |

**Lo que falló, y era de la prueba**: en la corrida7, `pide` daba DERECHA 14 veces para llegar al disco, la última tarjeta; con las paredes
del buscador la última es «Todos los discos de «entira»» (`21b-buscador-elegido.png`) y abría su pared (`GET /v1/search ... t=album`) en
vez del disco. Ahora va a la última y sube una (`spotify.py`). Una corrida8 intermedia con otro cambio mío (suponer que REGRESAR volvía al
buscador) se paró: el disco `dis000` ya está en la letra ♫ y, como en XP, REGRESAR vuelve a la pared (`23-pared-letra-spotify.png`); se
dejó como estaba. Capturas nuevas (20 a 25b) en la carpeta de capturas de la sesión y en `capturas/`.
