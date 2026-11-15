# rocola-pulso: nuestra libpulse.so.0 para Spotify Soloist

`rocola-pulso` es una `libpulse.so.0` PROPIA, escrita para la rocola. Spotify
Soloist (el cliente oficial de Spotify para Linux sin pantalla) busca
PulseAudio con `dlopen("libpulse.so.0")`; Rocola OS no tiene PulseAudio ni
PipeWire, y el vigilante (`rocola-soloist`, grupo «soloist») arranca Soloist con
`LD_LIBRARY_PATH=/usr/lib/rocola-pulso`, asi que carga esta. En vez de mandar el
sonido a un servidor, lo deja en un ANILLO de memoria compartida (interfaz I1)
que lee `rocola-xp` como una fuente de sonido mas: el sonido de Spotify pasa
por la cadena de la rocola, con su volumen, y nada sale por otro lado.

Solo depende de libc (glibc 2.34 o mas vieja; Rocola OS tiene 2.42). Licencia
MIT, como el repositorio. Los tipos, tamanos y valores vienen de la
documentacion y las cabeceras PUBLICAS de libpulse (comprobados uno por uno
contra ellas en `prueba/abi.c`); no hay codigo de PulseAudio. La idea del bucle
con hilo que suelta su cerrojo solo mientras espera en `poll()` es la de apulse
(MIT, github.com/i-rinat/apulse); no se copio su codigo.

## Por que

Decisiones del dueno del 14-11-2026: Spotify tiene que funcionar «si o si» para
un cumpleanos esta semana, TODO en la rocola (se busca, se elige, se paga y se
oye en la rocola; el celular solo empareja la bocina una vez) y «no te
conformes con lo que ya esta hecho». librespot ya no puede tocar con muchas
cuentas desde 11-2025, asi que el sonido lo da Soloist, el cliente oficial; lo
nuestro es todo lo de alrededor, empezando por este puente de sonido. Nada aqui
descifra audio ni imita al cliente oficial: Soloist hace todo lo de Spotify y
esta biblioteca solo recibe las muestras ya decodificadas que Soloist le da a
«PulseAudio», como haria el servidor de sonido de cualquier escritorio.

## Como usa Soloist libpulse (visto por fuera, sin ejecutarlo)

Del codigo de Soloist v1.3.10 (desensamblado; no se ejecuto nunca):

- Prueba PipeWire primero (busca `client.conf` y el demonio); sin PipeWire,
  `dlopen("libpulse.so.0")` y `dlsym` de 48 funciones
  (`prueba/soloist-funciones.txt`). Si falta una, usa un controlador mudo
  («Unable to initialize AudioDriver, using dummy»).
- `pa_threaded_mainloop_new/start/lock`, `pa_context_new(api, "spotify")`,
  avisos de estado y de suscripcion, `pa_context_connect(c, NULL,
  PA_CONTEXT_NOAUTOSPAWN, NULL)`. Espera mientras el estado sea CONNECTING,
  AUTHORIZING o SETTING_NAME; con UNCONNECTED, FAILED o TERMINATED lo da por
  perdido (por eso el estado tiene que ser CONNECTING al volver de connect,
  como en libpulse). Con READY se suscribe a las entradas
  (`PA_SUBSCRIPTION_MASK_SINK_INPUT`).
- Flujo «Spotify» con `media.role=music`, `FLOAT32LE` estereo a la frecuencia de
  la cancion, mapa NULL, volumen inicial (de `-i`), atributos
  `maxlength=-1, tlength=pa_usec_to_bytes(pulse_audio_tlength), prebuf=0,
  minreq=pa_usec_to_bytes(pulse_audio_minreq), fragsize=0` y banderas `0x200b`
  (`START_CORKED | INTERPOLATE_TIMING | AUTO_TIMING_UPDATE | ADJUST_LATENCY`).
  Espera mientras el flujo este en CREATING.
- Escribe desde su hilo «PulseAudioDriver Thread»: con el cerrojo, mira el
  estado y `pa_stream_writable_size`; si es 0 no escribe; si no,
  `pa_stream_write(s, datos, n, NULL, 0, PA_SEEK_RELATIVE)`.
- En el `underflow` TAPA el flujo (`pa_stream_cork(s, 1)`); destapa al volver a
  sonar. Pausa = cork + flush (esperando la operacion). Posicion: cada tanto
  `pa_stream_update_timing_info` (esperando) y lee `write_index` y `read_index`
  de `pa_stream_get_timing_info`.
- Volumen: `pa_context_set_sink_input_volume`; en el aviso de cambio de SU
  entrada pide `pa_context_get_sink_input_info` y toma `pa_cvolume_avg` del
  volumen (asi sabe el volumen «del sistema»).
- Al cerrar: lock, `pa_context_disconnect` y `unref` PRIMERO, despues
  `pa_stream_disconnect` y `unref`, unlock, `stop`, `free`.

## Diseno

```
 Soloist ──dlopen──> /usr/lib/rocola-pulso/libpulse.so.0
   hilo «PulseAudioDriver Thread»: writable_size / write  ──convierte──>  ANILLO (I1)  ──>  rocola-xp
   hilo del bucle (threaded-ml): estados, operaciones, reloj del flujo       (memoria compartida, /run)
```

- `bucle.c`: `pa_mainloop` (poll con io, tiempos y defer; `quit`; destruye) y
  `pa_threaded_mainloop` (un hilo de verdad; cerrojo recursivo con la cuenta
  nuestra, asi `wait` lo suelta del todo; `signal` con `wait_for_accept` y
  `accept`; `stop` y `free` sin dejar hilos; las senales se bloquean en el
  hilo, como libpulse). Ademas `pa_signal_*` para clientes como pacat.
- `contexto.c`: «conectar» es abrir (o crear) el anillo; CONNECTING en el acto
  y AUTHORIZING, SETTING_NAME y READY en el hilo del bucle; si el anillo no se
  puede abrir ni crear, FAILED con `PA_ERR_CONNECTIONREFUSED`. Operaciones con
  cuenta de referencias como libpulse (acaban en el hilo del bucle; al
  desconectar se cancelan). Suscripcion: NEW al conectar un flujo, CHANGE al
  cambiar su volumen, REMOVE al quitarlo. Una salida falsa «rocola» para quien
  pregunte por salidas (mpv) y ninguna entrada (SDL2).
- `flujo.c`: conversion de U8, S16, S24, S24_32, S32 y FLOAT32 (LE y BE; aLaw y
  uLaw no: `PA_ERR_NOTSUPPORTED`) a float32 estereo: mono duplicado; con mas
  de 2 canales, FRONT_LEFT y FRONT_RIGHT segun el mapa (con el mapa de
  fabrica de 6 canales, AIFF, son los canales 0 y 3), si no SIDE, si no MONO o
  FRONT_CENTER, si no 0 y 1. Un NaN o infinito se vuelve 0 (que no llegue a la
  cadena de la rocola). Un reloj del flujo (10 ms con lector y destapado;
  100 ms sin lector; 250 ms tapado) da el latido, mira el inodo cada segundo,
  pasa la cola local al anillo, avisa `started` y `underflow`, actualiza los
  tiempos y acaba los `drain`.
- `anillo.c`: abrir, validar, crear o rehacer por renombre (I1).
- `traza.c`: avisos y rastreo.
- El volumen que pide el cliente NUNCA se aplica: solo se guarda (para
  devolverlo en la info y en `volumen_pedido` del anillo). El volumen es el de
  la rocola.
- Un solo flujo por proceso escribe en el anillo: el ultimo que se conecta. El
  anterior pasa a FAILED con `PA_ERR_KILLED` (como cuando un servidor mata una
  entrada). Si OTRO proceso pone su pid en `pid_escritor`, el nuestro tambien
  se termina asi (el vigilante solo deja un Soloist).

### Decisiones propias (donde libpulse no dice o donde se aparta)

- `tlength` 0 o -1: objetivo de 250 ms; si no, `tlength` acotado a
  [50 ms, 65536 cuadros].
- Lo que se escribe sin sitio en el anillo (sin lector, o mas de lo que cabe)
  va a una cola local del flujo, en orden, hasta el limite (`maxlength`, o
  4 MiB, y nunca mas de 20 s); lo que pase del limite se descarta POR EL
  FINAL (se queda lo primero, el orden no se rompe), se apunta en el rastreo y
  se avisa `overflow`. Soloist mira `writable_size`, asi que no deberia pasar.
- `writable_size` = objetivo menos lo pendiente (anillo + cola local), en bytes
  del cliente; 0 sin lector. Con el flujo tapado tambien se puede llenar hasta
  el objetivo (como en libpulse).
- `read_index` = `write_index` menos lo pendiente del anillo y de la cola
  local. `pa_stream_flush` deja `write_index = read_index` (como libpulse).
  Si el anillo se rehace, lo pendiente del viejo se pierde y cuenta como
  leido (los indices nunca van para atras).
- `started`: la primera vez que el lector toma un cuadro tras conectar,
  destapar, vaciar o un underflow. `underflow`: solo si el LECTOR conto un
  subdesborde nuevo (`subdesbordes`), el flujo esta destapado, ya habia
  empezado y, al mirarlo, no queda nada pendiente. Si el escritor ya relleno,
  no se avisa: Soloist tapa en cada underflow y un aviso de mas seria un corte.
- `pa_strerror` fuera de rango devuelve NULL, como libpulse.
- `PA_RATE_MAX` es el de libpulse 17 (48 000 x 16 = 768 000 Hz; la 16 decia 384 000).
- El aviso «change» de la entrada sale solo si el volumen guardado de verdad
  cambio (pedir el mismo no avisa), como el servidor de PulseAudio.
- `pa_stream_write` solo admite `PA_SEEK_RELATIVE` con desplazamiento 0 o
  positivo (silencio); lo demas, `PA_ERR_NOTSUPPORTED`.

## El anillo (I1)

Ruta: `ROCOLA_SPOTIFY_ANILLO` o `/run/rocola-xp/spotify/sonido.anillo`.
Carpetas 0755, archivo 0644. Tamano exacto 4096 + 65536·2·4 = 528 384 bytes.
Lo crea quien llegue primero y no lo encuentre valido (magia, version,
capacidad, canales y tamano): se escribe completo en `<ruta>.t.<pid>` y se
renombra encima. Nadie lo trunca ni lo borra; quien lo encuentre invalido lo
rehace por renombre y el otro lo reabre al ver que cambio el inodo (cada
segundo como mucho). `anillo.h` es la referencia de la cabecera.

| Desp. | Tipo | Campo | Quien | Que |
|---:|---|---|---|---|
| 0 | u32 | magia | E | 0x4F4C5550 (bytes «PULO») |
| 4 | u32 | version | E | 1 |
| 8 | u32 | capacidad | E | 65536 cuadros |
| 12 | u32 | canales | E | 2 (siempre; el escritor convierte) |
| 16 | u32 | frecuencia | E | Hz del flujo actual; 0 sin flujo |
| 20 | u32 | gen_flujo | E | +1 al conectar o desconectar un flujo |
| 24 | u32 | tapado | E | 1 sin flujo o en pausa (cork, START_CORKED); 0 sonando |
| 28 | u32 | gen_vaciado | E | +1 en cada flush y en cada conexion de flujo |
| 32 | u64 | escrito | E | cuadros escritos desde que existe el archivo (release) |
| 40 | u64 | leido | L | cuadros consumidos (release) |
| 48 | u64 | vaciado_hasta | E | `escrito` en el ultimo vaciado (antes de subir gen_vaciado) |
| 56 | u64 | latido_escritor_ns | E | CLOCK_MONOTONIC; al escribir y cada 500 ms o menos con un flujo |
| 64 | u64 | latido_lector_ns | L | CLOCK_MONOTONIC; al leer y cada 500 ms o menos |
| 72 | u32 | pid_escritor | E | |
| 76 | u32 | lector_presente | L | 1 mientras lo tiene abierto y lee |
| 80 | u32 | volumen_pedido | E | 0..65536, promedio de lo que pidio el cliente; SOLO informativo |
| 84 | u32 | formato_original | E | `pa_sample_format_t` del flujo |
| 88 | u64 | subdesbordes | L | veces que el lector se quedo sin datos con el flujo sonando |
| 96 | u32 | latencia_lector_us | L | lo que tarda en oirse lo que toma el lector |
| 100..4095 | | reservado | | ceros |
| 4096.. | f32 | datos | E | L,R intercalados; el cuadro n en `(n mod capacidad)·2` |

Orden de memoria: el escritor copia los datos y publica `escrito` con release;
el lector lee `escrito` con acquire, copia y publica `leido` con release.
Pendientes = `escrito − max(leido, vaciado_hasta)`; el escritor nunca deja mas
de `capacidad` pendientes. Al conectar un flujo el escritor publica, en este
orden: tapado=1, frecuencia, formato_original, pid_escritor, volumen_pedido,
vaciado_hasta=escrito, gen_vaciado+1, gen_flujo+1, latido y el tapado real.

### Lo que el LECTOR (rocola-xp) tiene que hacer

1. Abrir (y crear si hace falta) como arriba; poner `latencia_lector_us`,
   `latido_lector_ns` y despues `lector_presente=1`; al cerrar,
   `lector_presente=0`. Al abrir (y al reabrir), `leido = max(leido,
   vaciado_hasta)`: lo vaciado antes de llegar no se toca.
2. Cada periodo de su tarjeta: latido; si cambio `gen_flujo`, releer
   `frecuencia` (0 = no hay flujo); si cambio `gen_vaciado`,
   `leido = max(leido, vaciado_hasta)`.
3. No consumir con `tapado=1`, con `frecuencia=0`, ni si `latido_escritor_ns`
   tiene mas de 2 s (el escritor murio sin avisar: un Soloist matado deja
   `tapado=0` y la frecuencia puesta).
4. Tomar como mucho `escrito − max(leido, vaciado_hasta)` cuadros (acotado a
   la capacidad) desde `max(leido, vaciado_hasta)`, copiar y publicar `leido`
   con release. Si quiso mas de lo que habia estando destapado, sumar 1 a
   `subdesbordes` (con eso Soloist sabe que se acabo la cancion o que se quedo
   corto). Lo que se esta copiando NUNCA lo pisa el escritor, ni aunque a
   media copia llegue un vaciado: el escritor mide su sitio con el `leido`
   tal cual (ver «Revision (hilos-y-robustez)»). El salto del vaciado (paso
   2) hay que darlo tambien con el flujo tapado y sin consumir: hasta que el
   lector salta, lo nuevo espera en la cola local del escritor.
5. Mirar el inodo de la ruta cada segundo y reabrir si cambio. El latido
   (`latido_lector_ns`) es CLOCK_MONOTONIC en nanosegundos: uno de mas de 2 s
   por delante no cuenta como lector vivo.
6. El sonido llega a la frecuencia del flujo (`frecuencia`): convertir a la de
   la tarjeta es cosa del lector. El ritmo lo marca el lector: sin lector
   (`lector_presente=0` o latido de mas de 2 s), Soloist no puede escribir.

`prueba/lector.c` hace exactamente esto (y ademas comprueba la senal); sirve de
modelo para el lector de rocola-xp.

## Compilar

```
make              # libpulse.so.0 (CC, CFLAGS y LDFLAGS desde fuera; -O2 -g si no)
make pruebas      # compila prueba/ y corre prueba/prueba.sh (todo, ~3 min)
prueba/prueba.sh rapido   # sin ThreadSanitizer, AddressSanitizer ni valgrind (~1 min)
make limpia
```

Se instala como `/usr/lib/rocola-pulso/libpulse.so.0` (el paquete de Buildroot
y la compilacion rapida son del grupo «soloist»). `-shared -fPIC -pthread
-Wl,-soname,libpulse.so.0 -Wl,--version-script,pulso.map -Wl,-z,defs`: solo se
exportan las funciones `pa_*`, con la version `PULSE_0` de libpulse (asi la
aceptan tambien los programas enlazados con libpulse, como pacat o mpv).

Dentro de la biblioteca, lo que toca listas de propiedades llama a las
funciones `pa_proplist_*` exportadas (no a lo de dentro): en un proceso donde
otra biblioteca ya trae las suyas (pacat carga libpulsecommon antes), se usan
siempre las mismas.

## Rastreo (para depurar en el A4)

`ROCOLA_PULSO_TRAZA=1` en el entorno de Soloist apunta cada llamada, con sus
argumentos y lo que devolvio, y los cambios de estado, `started`, `underflow`,
reaperturas y desplazamientos, en `$ROCOLA_RUN/rocola-soloist/pulso-traza.txt`
(de fabrica `/run/rocola-soloist/pulso-traza.txt`) o en
`ROCOLA_PULSO_TRAZA_ARCHIVO`. Al pasar de 2 MB se mueve a `.1` y sigue (en
memoria: `/run`). Sin la variable no se abre nada. Avisos en stderr, como
mucho 40 por proceso, siempre con «rocola-pulso:» (el vigilante los guarda en
su bitacora). Nunca pasa por aqui la llave de Soloist ni nada de la cuenta.

## Pruebas (prueba/)

- `cliente.c` (1): toma las 48 funciones con `dlopen` + `dlsym`, como Soloist,
  las usa como Soloist y cuenta que use las 48. 18 escenarios: S16LE,
  FLOAT32LE y S32LE; 1, 2 y 6 canales (mapa de fabrica); 44 100 y 48 000 Hz.
  Cada uno: contexto, suscripcion, flujo con tlength 200 ms y minreq 50 ms,
  START_CORKED, un hilo que escribe 0,5 s mirando writable_size, started,
  underflow al acabarse (y cork, como Soloist), tiempos; en los completos
  ademas volumen (aviso de cambio, `get_sink_input_info`, `volumen_pedido`),
  tapar (el lector no consume), vaciar (`write_index == read_index`),
  destapar, desconectar y otro flujo con `pa_stream_new`, y el cierre en el
  orden de Soloist. El lector (otro proceso) comprueba cuadro a cuadro la
  senal con contador (`senal.h`) y que el ritmo lo marca el.
- `raro.c` (2): los 11 formatos (U8 a S24_32BE) exactos, aLaw rechazado, mapa
  de 6 canales con FL y FR en otro sitio, mono; sin lector (writable 0, sin
  started), lector tardio y lector muerto (writable 0 a los 2 s, sin
  underflows inventados, otro lector sigue); anillo borrado y anillo invalido
  (se rehacen, el flujo sigue); dos flujos seguidos (44 100 y 48 000) y dos a
  la vez (el viejo FAILED/KILLED); vaciado en medio; cliente que muere
  (SIGKILL: el latido se para, el siguiente toma el anillo); anillo imposible
  (FAILED, CONNECTIONREFUSED); 51 llamadas con NULL o fuera de orden; el
  bucle sin hilo (io, tiempos, defer, quit, destruye) y el cerrojo recursivo
  con wait, signal y accept.
- `hilos.c` (2b, de la revision de hilos y robustez): todo en un proceso para
  que ThreadSanitizer vea los hilos. Carrera: un hilo escribe como Soloist
  (trozos de 4096 con solo mirar que writable no sea 0, tlength de 2 s: el
  anillo va casi lleno), otro tapa, destapa, vacia, pide tiempos, volumen e
  info esperando las operaciones, el principal recibe la suscripcion y un
  lector en otro hilo (su propio mapa, como rocola-xp) lee al ritmo de una
  tarjeta, a veces copiando en dos mitades con 2 ms en medio, a veces ausente
  300 ms y, en la segunda vuelta, rehaciendo el anillo por renombre: el
  contador de la senal NUNCA debe bajar. Ciclos: 200 vueltas de crear,
  conectar, escribir, vaciar y cerrar (en el orden de Soloist y en el de
  libpulse) sin descriptores ni hilos de mas. Cabecera: un latido del lector
  10 s por delante no cuenta, y una magia pisada se rehace. Consumo: los
  despertares por segundo y el procesador del hilo del bucle sin flujo,
  tapado, sin lector y sonando.
- (3) Lo mismo con ThreadSanitizer, con AddressSanitizer + UBSan y con
  valgrind (hilos.c, con `HILOS_CORTO=1`, en los dos primeros).
- (4) Clientes de verdad (pacat, paplay, mpv) con `LD_LIBRARY_PATH` a la
  nuestra, reproduciendo un WAV con la senal: lo que llega al anillo es
  identico cuadro a cuadro.
- (5) Lo que exporta (las 48, nada fuera de `pa_*`), la glibc que pide (2.34
  como mucho), que solo necesita libc, y la ABI comparada con las cabeceras de
  libpulse (`abi.c`).

Resultados del 14-11-2026 (maquina de pruebas, x86_64, glibc 2.39; cabeceras
de libpulse 16.1, la ABI de estas estructuras es la misma en la 17): ver la
seccion «Comprobado» abajo.

## Comprobado

Ultima corrida de `prueba/prueba.sh` (completa, 15-11-2026, maquina de pruebas
x86_64 con 4 nucleos compartidos, gcc de la maquina, glibc 2.39; cabeceras de
libpulse 16.1; pacat y paplay de pulseaudio-utils 16.1 y mpv 0.37 de la
maquina): **14 pasos bien, 0 mal**.

| Paso | Resultado |
|---|---|
| compilar | libpulse.so.0 y las pruebas, 0 avisos del compilador (`-Wall -Wextra`) |
| (5) exporta | las 48 de Soloist entre 188 funciones `pa_*` (todas con la version `PULSE_0`), 0 fuera de `pa_*`; glibc maxima pedida `GLIBC_2.34`; solo necesita `libc.so.6`; soname `libpulse.so.0` |
| (5b) abi | 141 tamanos, desplazamientos y valores iguales a los de las cabeceras de libpulse |
| (1) cliente | 18 de 18 escenarios (S16LE, FLOAT32LE, S32LE x 1, 2, 6 canales x 44 100, 48 000 Hz), las 48 funciones usadas por `dlsym`, 739 comprobaciones bien, 0 mal; en cada uno el lector comprobo cuadro a cuadro 39 690 o 43 200 cuadros, y 0,5 s de sonido tardaron de 0,496 a 0,500 s al ritmo del lector |
| (2) raro | 113 comprobaciones bien, 0 mal: 11 de 11 formatos exactos; sin lector writable 0 y a los 2,00 s de morir el lector; anillo borrado e invalido rehechos (el lector reabrio 2 veces, 273 600 cuadros sin errores); dos flujos seguidos y dos a la vez; vaciado en medio (48 000 bytes fuera, 60 000 cuadros leidos sin errores); cliente matado (latido parado a los 1,50 s, el siguiente toma el anillo); anillo imposible (FAILED); 51 llamadas con NULL o fuera de orden; bucle sin hilo y cerrojo recursivo |
| (4) paplay S16 48 000 | 72 000 cuadros identicos, 0 errores |
| (4) paplay FLOAT 44 100 | 66 150 cuadros identicos, 0 errores |
| (4) pacat --raw | 72 000 cuadros identicos, 0 errores |
| (4) mpv --ao=pulse | 72 000 cuadros identicos, 0 errores |
| consumo | 10 s de paplay: 480 000 cuadros identicos; paplay entero (con libsndfile) 10,003 s de reloj, 0,011 s de usuario y 0,021 s de sistema (0,3 % de un nucleo) |
| rastreo | 440 lineas (41 de `pa_stream_write`), rota a `.1` con el tope bajado a 30 kB |
| (3) ThreadSanitizer | cliente (2 escenarios) y los 9 casos raros: 0 avisos |
| (3) AddressSanitizer + UBSan | cliente (18 escenarios) y los 9 casos raros: 0 avisos de memoria, fugas ni comportamiento indefinido |
| (3) valgrind | cliente (2 escenarios) y los NULL: 0 errores de memoria, 0 fugas definitivas |

pacat, paplay y mpv no pidieron ninguna funcion que no tuvieramos (por eso hay
188 y no 48: las que usan esos clientes, como `pa_stream_drain`,
`pa_context_get_sink_info_by_name`, `pa_signal_*` o `pa_channel_map_*`).

## Limites y lo que falta

- NADA de esto se ha probado con el Soloist de verdad (no se puede ejecutar
  aqui) ni en el A4. Lo de Soloist sale de mirar su codigo por fuera.
- Si el anillo no se puede abrir ni crear al arrancar Soloist, el contexto
  falla y Soloist se queda con su controlador mudo hasta que se reinicie: el
  vigilante debe crear `/run/rocola-xp/spotify` (o asegurarse de que se pueda)
  antes de arrancarlo; en la bitacora sale «rocola-pulso: no se pudo abrir ni
  crear el anillo».
- Si Soloist encuentra PipeWire (`client.conf` y el demonio) lo usa en vez de
  esta biblioteca; Rocola OS no lo trae.
- Una version nueva de Soloist (cada 90 dias) podria pedir funciones de mas;
  si falta una, Soloist usa el controlador mudo. Comprobar con
  `objdump -d`/`strings` de la version nueva contra `prueba/soloist-funciones.txt`
  (lo que se hizo para la v1.3.10).
- Solo reproduccion: grabar (`pa_stream_connect_record`) da
  `PA_ERR_NOTSUPPORTED`.

## Revision (abi-y-semantica)

Revision independiente del 15-11-2026: las 48 funciones contra la semantica de
libpulse 17 y sus tipos, y contra como las usa Soloist v1.3.10 (su codigo
desensamblado, `analisis/driver-nombres.asm` de la carpeta de trabajo; Soloist
no se ejecuto).

Lo que se comprobo y estaba bien:

- La ABI contra las cabeceras de libpulse **17.0** (sacadas del codigo fuente
  oficial, fuera del repositorio, solo para comparar): los 109 tamanos y
  desplazamientos de TODOS los campos de las 11 estructuras de `pulso_abi.h`
  iguales; de las 180 constantes `PA_*`, 179 iguales.
- Las firmas: los 185 prototipos de `pulso_api.h` compilan junto a las
  cabeceras de la 16.1 y de la 17.0 (tres toman los avisos como `void *`: la
  misma ABI).
- Como lo usa Soloist: arranca el bucle antes de conectar y espera mientras el
  contexto este en 1-3 (CONNECTING, AUTHORIZING, SETTING_NAME) y el flujo en
  CREATING; sus avisos de estado, `started` y del tapado solo apuntan y hacen
  `signal`; espera `flush` y `update_timing_info` mirando
  `pa_operation_get_state` (aqui acaban en el hilo del bucle, primero el aviso
  y luego DONE, todo con el cerrojo: no se queda colgado); de los tiempos solo
  lee `write_index` (desplazamiento 56) y `read_index` (72) y calcula lo
  pendiente como su resta (`sink_usec` no lo usa); escribe trozos enteros con
  solo mirar que `writable_size` no sea 0 (por eso la cola local); en el aviso
  de cambio lee el volumen en el desplazamiento 172 de `pa_sink_input_info`
  (igual que aqui); no libera las operaciones de `cork` ni de `subscribe`
  (como en libpulse, se quedan sin liberar: unos bytes por pausa).
- `pa_strerror`, `pa_cvolume_set/avg`, `pa_usec_to_bytes`, `pa_timeval_diff`,
  `pa_gettimeofday`, los indices tras `flush` (`write_index = read_index`),
  los estados que entrega en el acto (CONNECTING, CREATING, TERMINATED) y lo
  que devuelve cada una con argumentos malos: como libpulse.

Lo que se arreglo:

1. `PA_RATE_MAX` era el de la 16 (384 000 Hz); en la 17 es 768 000. Ahora el
   de la 17 (y `abi.c` acepta la diferencia conocida con cabeceras viejas).
2. Pedir el mismo volumen avisaba «change» siempre; el servidor de PulseAudio
   solo avisa si cambio. Soloist contesta a ese aviso pidiendo la info y
   pasando el volumen hacia arriba: un aviso de mas podia hacer un bucle.
   Ahora solo avisa si cambio (`flujo_pon_volumen` dice si cambio), y
   `cliente.c` lo comprueba en los 9 escenarios completos.
3. Las pruebas de la ABI eran una muestra (141 lineas): ahora `abi.c` compara
   TODOS los campos y TODAS las constantes de `pulso_abi.h` (289 lineas), hay
   un paso `firmas` (los prototipos contra las cabeceras) y, con
   `PULSO_CABECERAS=<carpeta con pulse/*.h>`, `abi-otras` y `firmas-otras`
   comparan tambien contra otra version (asi se comparo con la 17).

Comprobado de la revision (15-11-2026, `prueba/prueba.sh` completo con
`PULSO_CABECERAS` apuntando a las cabeceras de libpulse 17.0): **17 pasos
bien, 0 mal**. abi 289 iguales a la 16.1 y 289 iguales a la 17.0; firmas 185
y 185; cliente 18 de 18 escenarios, 757 comprobaciones bien, 0 mal (las 18 de
mas son el «mismo volumen no avisa»); raro 113 bien, 0 mal; paplay, pacat y
mpv identicos cuadro a cuadro (72 000, 66 150, 72 000 y 72 000 cuadros);
consumo 480 000 cuadros identicos; ThreadSanitizer, AddressSanitizer + UBSan y
valgrind sin avisos.

Lo que queda (no se puede arreglar aqui): ver «Limites». Ademas, para el grupo
«soloist»: el anillo se crea 0644, asi que Soloist tiene que correr con el
mismo usuario que lo crea (o con uno que pueda escribirlo); si lo crea
rocola-xp como root y Soloist corre con otro usuario, el contexto falla
(`PA_ERR_CONNECTIONREFUSED`) y Soloist usa su controlador mudo.

## Revision (hilos-y-robustez)

Revision independiente del 15-11-2026, con el enfoque en los hilos, el orden de
memoria del anillo, el lector ausente y presente, el anillo rehecho, el
cliente que muere, lo que podria tumbar a Soloist, los descriptores y el
consumo. Soloist no se ejecuto; lo suyo sale del desensamblado
(`analisis/driver-nombres.asm`): su hilo de sonido toma el cerrojo, mira el
estado y `pa_stream_writable_size` y, si no es 0, escribe un trozo entero;
espera `flush` y `update_timing_info` con `wait` hasta que la operacion deja
de correr, y sus avisos solo hacen `signal`.

Lo que se comprobo y estaba bien:

- El cerrojo: recursivo con la cuenta propia; `wait` lo suelta del todo y lo
  retoma con la misma cuenta; los avisos salen del hilo del bucle con el
  cerrojo tomado; `g_mx` (el del anillo) nunca se tiene mientras se llama a un
  aviso del cliente y siempre se toma despues del cerrojo del bucle (y la
  traza despues de los dos): no hay ciclo posible. `en_poll` solo cambia con
  el cerrojo tomado, asi que `despierta` no pierde despertares.
- Las operaciones que espera Soloist acaban en el hilo del bucle, primero el
  aviso (que hace `signal`) y luego DONE, sin soltar el cerrojo entre medias;
  si el flujo cae (FAILED, KILLED, TERMINATED), las operaciones se cancelan
  ANTES de avisar el estado, asi que el que espera se despierta y ve
  CANCELLED.
- Descriptores con `O_CLOEXEC`/`EFD_CLOEXEC` (anillo, eventfd, tuberia de las
  senales, traza `"ae"`): no pasan a los hijos de Soloist.
- El hilo del bucle bloquea todas las senales (el SIGTERM del vigilante le
  llega a Soloist, no a nosotros).

Lo que se arreglo (con la prueba nueva `prueba/hilos.c`):

1. **Un vaciado con el anillo casi lleno pisaba lo que el lector copiaba.** El
   sitio para escribir se media con `max(leido, vaciado_hasta)`: tras un
   `pa_stream_flush`, el escritor podia copiar hasta 65 536 cuadros nuevos
   encima de los que el lector estaba copiando en ese momento (el lector toma
   `leido` y `escrito`, y a media copia llega el vaciado). Con un tlength grande
   o la cola local llena (Soloist escribe trozos enteros) pasa en cada pausa o
   cambio de cancion: unos milisegundos de sonido revuelto. Reproducido: con
   tlength de 2 s y el lector copiando a veces en dos mitades, el contador de
   la senal BAJO 5, 7 y 8 veces en tres corridas de 6 s; con el arreglo, 0 en
   todas. Ahora `anillo_libre` mide el sitio con el `leido` tal cual: lo nuevo
   no cae encima de nada que el lector aun pueda copiar, y espera en la cola
   local hasta que el lector salta (`leido = vaciado_hasta`). I1 se sigue
   cumpliendo (nunca mas de capacidad pendientes) y `writable_size` no cambia.
2. **Un latido del lector «del futuro» contaba siempre como lector vivo.** Un
   lector que apuntara mal su reloj (otras unidades) y muriera dejaria a
   Soloist escribiendo para nadie. Ahora solo vale a 2 s o menos de ahora,
   hacia atras o hacia adelante.
3. **Una cabecera pisada en su sitio (sin cambiar el inodo) no se veia.** Ahora
   se mira cada segundo tambien el tamano del archivo mapeado (`fstat`, antes de
   leer nada) y la magia; si no estan, se reabre y se rehace por renombre
   (0,89 s en la prueba). Truncarlo en su sitio sigue dando SIGBUS al tocar las
   paginas que ya no existen (I1: nadie lo trunca); esto solo acorta el tiempo
   en peligro.
4. `anillo_abre` de uno en uno dentro del proceso: dos contextos rehaciendolo a
   la vez usaban el mismo `<ruta>.t.<pid>`.
5. `drena_cola` (el reloj del flujo) solo escribe si el flujo sigue siendo el
   del anillo (antes miraba solo su marca local).
6. `poll` con ENOMEM o EAGAIN ya no mata el hilo del bucle (Soloist se quedaria
   esperando para siempre): 10 ms sin el cerrojo y sigue.
7. `prueba/lector.c`, el modelo del lector de rocola-xp, hace ahora todo lo
   que dice «Lo que el LECTOR tiene que hacer»: `leido = max(leido,
   vaciado_hasta)` al abrir, al reabrir y al leer, y no consume con el latido
   del escritor de mas de 2 s.

Consumo del hilo del bucle (prueba `hilos consumo`, maquina de pruebas,
cambios de contexto voluntarios y `schedstat`): sin flujo 0,0 despertares/s
(0,00 % de un nucleo); tapado (Soloist en pausa) 4,0/s (0,01 %); destapado sin
lector 10,0/s (0,03 %); sonando 98,3/s (0,18 %). Sin esperas activas.

Comprobado de la revision (15-11-2026, `prueba/prueba.sh` completo con
`PULSO_CABECERAS` a las de libpulse 17.0): **18 pasos bien, 0 mal**. Los 17
de antes con los mismos numeros (cliente 757 bien, raro 113 bien, abi 289 y
289, firmas 185 y 185, paplay, pacat y mpv identicos, consumo 480 000
cuadros) y el nuevo `hilos`: 24 comprobaciones bien, 0 mal (carrera 6 s:
3757 trozos, 471 vaciados, 207 tiempos, 244 volumenes, 237 infos, el lector
179 280 cuadros con 0 bajadas; con el anillo rehecho 7 veces: 163 680 cuadros,
0 bajadas, 6 reaperturas; 200 ciclos con descriptores 5 -> 5 e hilos 2 -> 2;
cabecera; consumo). ThreadSanitizer (cliente, raro y hilos corto: 19 bien),
AddressSanitizer + UBSan (cliente, raro y hilos corto, con 50 ciclos:
descriptores 6 -> 6, hilos 2 -> 2) y valgrind: 0 avisos.

Lo que queda:

- Truncar el anillo en su sitio (`ftruncate` o abrirlo con `O_TRUNC`, por
  ejemplo un `FileMode.Create` de C#) tumba a Soloist con SIGBUS. rocola-xp
  NUNCA debe abrirlo asi: si lo encuentra invalido, lo rehace por renombre
  (I1). No se pone un manejador de SIGBUS dentro de Soloist.
- El lector de rocola-xp (grupo spotify-xp) tiene que dar el salto del vaciado
  tambien con el flujo tapado (si no, tras una pausa con el anillo lleno lo
  nuevo espera en la cola local hasta que lo de) y usar el latido en
  nanosegundos de CLOCK_MONOTONIC.
- Que Soloist aguante `writable_size` 0 mucho tiempo (sin lector) sin girar en
  vacio depende de su hilo de sonido: solo se ve en el A4 (el procesador de
  Soloist con la rocola sin el modo Spotify leyendo).
