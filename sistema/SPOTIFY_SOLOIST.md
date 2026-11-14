# Spotify en la rocola, con Spotify Soloist

14-11-2026. Grupo «soloist» del trabajo de Spotify (lo del sistema: el vigilante, la orden `spotify`, las llaves, el lanzador y el paquete
del puente de sonido). **Nada de esto está probado en el A4**: solo con pruebas automáticas fuera de la rocola y un Soloist **falso** (abajo,
«Lo que está probado»).

Decisiones del dueño que manda esto (14-11-2026):
- «Prioriza lo de Spotify [...] debe funcionar sí o sí para un cumpleaños».
- «Lo de Spotify será todo en la rocola, nada en el celular»: se busca, se elige, se paga y se oye **en la rocola**, con sus botones y su
  pantalla. El celular (o la app de escritorio de la laptop) se usa **una sola vez**, para emparejar. Lo que alguien ponga desde un
  celular **no suena** (la rocola lo pausa al momento y no lo cobra).
- Lo de alrededor es nuestro (el puente de sonido, el vigilante, el cobro y los turnos, el diagnóstico). El audio de Spotify lo descifra
  **solo** el programa oficial de Spotify (Soloist): nada nuestro descifra, saca llaves de audio ni se hace pasar por Spotify.

---

## Para el dueño

### Qué es

**Spotify Soloist** es el programa **oficial** de Spotify para aparatos con Linux sin pantalla: hace de «bocina» de Spotify Connect. La
rocola lo **baja sola** de Spotify (no se puede repartir: no va en el sistema ni en el repositorio), lo tiene corriendo como la bocina
«Rocola» y le dice qué canción tocar. Su sonido pasa por la cadena de la rocola, como una canción más.

Lo que necesitas, una sola vez:
1. **La llave de Soloist** (archivo `rocola_spotify.txt`).
2. **La llave de tu app de Spotify** (archivo `rocola_spotify_api.txt`): con ella la rocola busca en Spotify.
3. **Encender el modo** en la rocola (`spotify encender`).
4. **Emparejar** la bocina «Rocola» con tu cuenta, desde la app de Spotify, en la red del bar.

Las dos llaves necesitan tu cuenta **Premium** y son **secretas**: no las mandes por mensaje ni las subas a ningún lado. La rocola las
guarda solo en `/rocola/datos/xp` (que solo lee el sistema) y nunca las escribe en sus bitácoras ni las sube al buzón.

### 1. La llave de Soloist

1. Entra a <https://developer.spotify.com> con tu cuenta Premium (arriba a la derecha, «Log in»).
2. Ve a **Spotify Soloist API Key** (está en la documentación de Spotify Soloist, «Getting started» → «Generate an API key»).
3. Lee y acepta las condiciones si te las pide, y pulsa **Generate**.
4. Copia la llave en un archivo de texto llamado **`rocola_spotify.txt`**, en la raíz de una memoria USB: solo la llave, en una línea.
   (El Bloc de notas de Windows sirve: la rocola le quita los espacios, los saltos de línea de Windows y la marca de UTF-8.)

### 2. Tu app de Spotify (para buscar desde la rocola)

1. En <https://developer.spotify.com/dashboard>, **Create app**.
2. Nombre: «Rocola» (el que quieras). Descripción: la que quieras.
3. **Redirect URI**: `http://127.0.0.1:8898/callback` (pulsa «Add»).
4. En «Which API/SDKs are you planning to use?», marca **Web API**.
5. Acepta las condiciones y **Save**.
6. En la app, **Settings**: copia el **Client ID** y, con «View client secret», el **Client Secret**.
7. En la misma memoria USB, un archivo **`rocola_spotify_api.txt`** con estas dos líneas:

   ```
   client_id=EL_CLIENT_ID
   client_secret=EL_CLIENT_SECRET
   ```

   Si más adelante autorizas tu cuenta desde la laptop (para «tus favoritas», «tus discos» y «tus listas»), se añade una tercera línea
   `refresh_token=...`. Sin ella la rocola busca y abre discos igual.

La app queda en «modo desarrollo»: es solo para ti, y Spotify le pone un tope de peticiones. La rocola pregunta lo mínimo (lo que alguien
busca o abre en ese momento), pero si alguien se pone a buscar sin parar Spotify puede frenarla un rato («Spotify frena» en
`spotify estado`).

### 3. La memoria USB y encender el modo

1. Con la rocola prendida, conecta la memoria USB con los dos archivos.
2. **Ctrl+Alt+F2** (la consola de mantenimiento; usuario `root`). Al entrar, la rocola se cierra sola (la música se para un momento).
3. Escribe `spotify llave`: toma las dos llaves de la memoria. Dirá «Llave de Soloist puesta» y «Llave de la API web puesta».
   (Si no la conectaste, te pide la llave de Soloist escrita; no se ve mientras la escribes.)
4. Escribe `spotify encender`.
5. Saca la memoria USB y vuelve a la rocola con **Ctrl+Alt+F1**.

Sin la consola: con el modo encendido, la rocola mira la memoria USB cada 30 s mientras le falte la llave; y el buzón la toma en su vuelta
de cada 15 minutos (también si la de la USB es otra: así se cambia).

La primera vez que se enciende, la rocola baja el programa de Spotify (unos 14 MB; necesita internet).

### 4. Emparejar, una vez, en el bar

Con la rocola abierta y el modo encendido:
1. En el celular (o en la laptop, con la app de Spotify de escritorio), **conectado al wifi del bar** (la misma red que la rocola).
2. Abre Spotify con tu cuenta, pon cualquier canción y toca el **icono de dispositivos** (la bocina con la pantallita).
3. Elige **«Rocola»**.

Listo: la cuenta queda guardada en la rocola y no hay que volver a hacerlo (ni al apagarla, ni al actualizarla). La canción que pusiste
**no suena**: la rocola pausa todo lo que no se pidió en ella.

Otra forma, desde la consola: `spotify emparejar` (te dice lo mismo y espera a que elijas «Rocola» en la app). Sirve también para cambiar
de cuenta.

### 5. Ya está: todo lo demás, en la rocola

Buscar, elegir, pagar con créditos y oír se hace con los botones y la pantalla de la rocola, como cualquier canción: un crédito y su turno
en la cola. Si una de Spotify no puede sonar (sin internet, Spotify falla), su crédito se devuelve y la música de la rocola sigue. Lo
pagado de Spotify que no sonó vuelve como crédito al apagar el modo o tras un apagón. Lo de Spotify no cuenta para lo más sonado.

### La orden `spotify` (consola de mantenimiento, Ctrl+Alt+F2)

| Orden | Qué hace |
|---|---|
| `spotify` | La ayuda y si el modo está encendido |
| `spotify encender` / `spotify apagar` | Enciende o apaga el modo (vale al volver a la rocola con Ctrl+Alt+F1) |
| `spotify estado` | Todo de un vistazo: el modo, si las llaves están puestas (nunca las enseña), el programa y **cuántos días le quedan**, si corre, si la cuenta está emparejada y la bocina activa, internet hasta Spotify y los últimos problemas |
| `spotify llave` | Pone las llaves de la memoria USB; si no hay memoria, pide escrita la de Soloist |
| `spotify llave api` | Pide escritas las de tu app (client_id, client_secret y, si lo tienes, refresh_token) |
| `spotify emparejar` | Empareja (o cambia la cuenta) desde la app de Spotify |
| `spotify prueba` | Una revisión corta: dice BIEN o MAL de cada cosa |
| `spotify bajar` | Baja ahora (o pone al día) el programa de Spotify |
| `spotify nombre NOMBRE` | El nombre de la bocina en la app (de fábrica «Rocola») |
| `spotify olvidar` | La rocola olvida la cuenta (pide escribir SI) |

`rocola-soloist estado` dice lo mismo que la parte del programa de `spotify estado`.

### Si «Rocola» no aparece en la app

1. `spotify estado`: ¿el modo está encendido?, ¿dice «en marcha»?, ¿está la llave de Soloist?
2. El celular tiene que estar en **la misma red** que la rocola. Muchos routers separan el wifi de lo que va por cable o aíslan a los
   aparatos del wifi entre sí («aislamiento de clientes», «AP isolation», red de invitados): en el router del bar, que el wifi no sea de
   invitados y que no aísle.
3. `spotify prueba`: si dice MAL en «Servidores de Spotify», no hay internet hasta Spotify (cable, router, DNS).
4. Si la llave se copió mal, Soloist lo dice al arrancar: `spotify estado` enseña el último problema; vuelve a ponerla (`spotify llave`).
5. Desde la consola, `spotify emparejar` (con la rocola cerrada no hay otra bocina «Rocola» compitiendo).

### Si se pide y no suena

1. `spotify prueba`: el puente de sonido (rocola-pulso) tiene que estar, y «El anillo de sonido» debe decir «flujo a 44100 Hz, sonando»
   mientras suena una de Spotify.
2. `spotify estado`, «Lo que dice la rocola»: «bocina activa: sí». Si alguien la tomó desde su celular, la rocola la recupera sola.
3. Las bitácoras: `/rocola/datos/xp/rocola_soloist.txt` (el programa de Spotify) y `/rocola/datos/xp/rocola_spotify.txt` (lo cobrado, lo
   devuelto, los turnos). Desde casa: la orden `registros` del buzón las trae (no tienen secretos).

### Cuántos días le quedan al programa

Spotify hace que **cada versión de Soloist caduque a los 90 días** de compilada. `spotify estado` dice «Caduca el AAAA-MM-DD: quedan N
días». La rocola se pone al día **sola** cuando quedan menos de 7 días (con internet): baja la nueva y la usa en cuanto no esté sonando
nada de Spotify. Si caducara de todos modos (por ejemplo, semanas sin internet), lo dice, baja la última en cuanto puede y mientras tanto
la música de la rocola sigue. `spotify bajar` lo hace a mano.

### Probarlo en casa antes de ir al bar (con la laptop de Pop!_OS)

La laptop sí tiene PipeWire, así que Soloist suena en ella sin nada nuestro. Sirve para saber que la cuenta y la llave valen:

```
curl --fail --location -o soloist.tar.gz https://soloist-builds.spotifycdn.com/soloist_release_x86_64.tar.gz
tar -xzf soloist.tar.gz
./soloist --version
./soloist -n Prueba -k "TU_LLAVE_DE_SOLOIST"
```

En el celular, en el wifi de tu casa, elige «Prueba» y pon una canción: debe sonar en la laptop. (Ctrl+C lo cierra.) Y la llave de tu app:

```
curl -s -X POST -d grant_type=client_credentials -u "CLIENT_ID:CLIENT_SECRET" https://accounts.spotify.com/api/token
```

debe contestar con un `"access_token"`. Si contesta `invalid_client`, el Client ID o el secreto están mal copiados.

---

## El diseño (para los grupos y la junta)

### Las piezas

| Pieza | Dónde | Qué hace |
|---|---|---|
| Spotify Soloist | se baja a `/rocola/disco/rocola-os/soloist/soloist.tar.gz` (o a `/rocola/datos/xp/soloist-programa/`), se saca a `/run/rocola-soloist/bin/soloist` | El cliente oficial: la cuenta, el audio, la bocina de Spotify Connect, la API por WebSocket |
| `rocola-soloist` | `/usr/bin` (sistema/board/rocola/rootfs_overlay) | El vigilante: bajar, poner al día, llaves, arrancar, volver a arrancar, estado, bitácora |
| `spotify` | `/usr/bin` | La orden del dueño en la consola de mantenimiento |
| rocola-pulso | `/usr/lib/rocola-pulso/libpulse.so.0` (paquete `package/rocola-pulso`; código del grupo «pulso», `sistema/rocola/pulso`) | Nuestro libpulse: Soloist le escribe el sonido y lo deja en el anillo (I1) |
| rocola-xp | (grupos «spotify-xp» y «catalogo») | Abre el vigilante, habla con Soloist por WebSocket, lee el anillo, cobra y enseña |

### El vigilante (I4): `rocola-soloist corre`

- **Lo abre rocola-xp** como hijo, con el modo encendido (`spotify=1`), con `SPOTIFY_NOMBRE` y `ROCOLA_SPOTIFY_ANILLO` en el entorno; lo
  cierra con SIGTERM (SIGKILL a los 10 s si no salió).
- **Un solo vigilante**: candado (`flock`) en `/run/rocola-soloist/candado`. Uno que llegue con otro en marcha **espera** a que acabe (no
  se va: si rocola-xp se cae y el lanzador la abre a los 2 s, el vigilante nuevo toma el relevo en cuanto el viejo se cierra).
- **Sale si muere su padre** (lo mira cada 2 s, cada 0,5 s con Soloist en marcha y cada 0,2 s mientras baja o espera a `--version`: su
  proceso padre cambia) o con SIGTERM/SIGINT: SIGTERM a **todo el grupo** de Soloist (lo arranca con `setsid`, así se cierran también sus
  hijos), hasta 8 s por el reloj, y después SIGKILL; y lo que quede **fuera** del grupo (un hijo que se hizo su propia sesión, como el
  ayudante de fallos crashpad) se cierra también (revisión, abajo). Las esperas largas (bajar, `--version`) van en segundo plano y se
  cortan al momento. Medido (abajo, pasada de la revisión): 0,17 s normal, 8,32 s con un Soloist que no hace caso, 0,14 s a media
  bajada, 0,65 s al morir el padre, 0,24 s al morir el padre a media bajada y 0,80 s con un hijo de Soloist fuera de su grupo (todo
  fuera: el vigilante, Soloist y lo suyo).
- **Un Soloist huérfano** (de un vigilante muerto con SIGKILL) que use la misma carpeta de datos se cierra antes de arrancar otro.
- **La llave**: de `rocola_spotify_llave.txt`; si falta, fase `sin_llave` y busca la memoria USB cada 30 s. Si el archivo cambia, se
  vuelve a arrancar Soloist con la nueva. Si falta la de la API, se mira la USB al empezar y cada 15 min.
- **El programa**: si no hay, se baja; si caduca en menos de 7 días, se intenta bajar uno nuevo (como mucho una vez por hora) antes de
  arrancar, y con Soloist en marcha se revisa cada hora; si se bajó uno nuevo y la rocola no tiene nada de Spotify sonando ni en la cola
  (`/run/rocola-xp/spotify/estado`: `sonando=1` / `en_cola=0`), se cambia ya.
- **Arranca** Soloist así (la orden **no se apunta**):
  `LD_LIBRARY_PATH=/usr/lib/rocola-pulso setsid soloist -n "$SPOTIFY_NOMBRE" -k LLAVE -D /rocola/datos/xp/soloist -C /run/rocola-soloist/cache -z 100 -w 127.0.0.1:0 -i 100`,
  con `HOME` y `XDG_*` en `/run/rocola-soloist`, sin `STATE_DIRECTORY` ni `CACHE_DIRECTORY`, con la prioridad normal (nice 0, aunque lo
  abra un hilo de fondo de rocola-xp con nice 19: Soloist decodifica el sonido) y `ROCOLA_PULSO_TRAZA` si viene puesta. El nombre:
  `SPOTIFY_NOMBRE`, o `spotify_nombre=` de `rocola_os.txt`, o «Rocola».
- **Listo** cuando aparece `ws.port` en su carpeta de datos (fase `corriendo`); antes de arrancarlo se borran `ws.port`/`ws.addr` viejos
  (rocola-xp nunca se conecta a un puerto de otro arranque). Si en 90 s no abre su WebSocket, se cierra y se vuelve a arrancar.
- **Si sale**: con **10** (caducado) baja la última (sin preguntar la fecha) y vuelve a arrancar; si no hay una nueva, fase `caducado` y
  lo reintenta cada 10 min. Con otro código: espera 2 s, 10 s, 30 s y 2 min (y 2 min las siguientes); tras 10 min seguidos en pie vuelve a
  2 s.

### Bajar y poner al día (`rocola-soloist baja`)

- `curl --fail --location -R` (la fecha del archivo = la del servidor, `Last-Modified`) con `-z` (If-Modified-Since: si no hay uno más
  nuevo, el servidor contesta 304 y no se baja nada), a un `.t`, con 2 reintentos.
- Se comprueba **entero** (`gzip -t`: uno cortado no pasa) y que **trae `soloist`**; el que había pasa a `soloist.anterior.tar.gz`. Si
  algo falla (CDN caído, tar roto, disco lleno), se borra el `.t` y se sigue con el que había.
- **Dónde**: en el disco de la rocola (`/rocola/disco/rocola-os/soloist/`) si está montado **con escritura**, no está «sin revisar»
  (`/run/rocola-disco-revisar`, un apagón: ahí no se escribe nada nuevo, como en `rocola-actualiza`) y tiene 64 MB libres; si no, en
  `/rocola/datos/xp/soloist-programa/` (40 MB libres). Se usa el más nuevo de los dos.
- **Nunca se ejecuta desde el disco** (NTFS): se saca a `/run/rocola-soloist/bin/` (en memoria) cuando cambia (por su contenido, md5).
- **Su versión y cuándo caduca**: `soloist --version` (la versión, y la fecha de compilación si la dice como `AAAA-MM-DD` o en segundos);
  si no, la versión del `CHANGELOG.md` del tar y la fecha del `.tar.gz` bajado (la del servidor) **más 90 días**; y en cuanto Soloist
  arranca, lo que él mismo dice («client expires in N days», que se lee de su salida) manda. Queda en `soloist.info` junto al tar.
- Si el programa nuevo no contesta a `--version` y el anterior sí, se usa el anterior (el nuevo queda como `soloist.malo.tar.gz`).

### Los archivos (I3)

| Archivo | Quién | Qué |
|---|---|---|
| `/rocola/datos/xp/rocola_os.txt` | rocola-xp (dueño); `spotify encender/apagar/nombre` con la rocola cerrada | `spotify=1` o `0` (de fábrica apagado), `spotify_nombre=` |
| `/rocola/datos/xp/rocola_spotify_llave.txt` | `rocola-soloist`, `spotify llave`, el buzón | La llave de Soloist (0600, una línea) |
| `/rocola/datos/xp/rocola_spotify_api.txt` | los mismos | `client_id=`, `client_secret=`, opcional `refresh_token=` (0600) |
| `/rocola/datos/xp/soloist/` | Soloist | Su identidad y la sesión de la cuenta (0700) |
| `/run/rocola-soloist/` | el vigilante | `estado`, `candado`, `bin/`, `cache/` (con `-z 100`), `casa/` |
| `/run/rocola-soloist/estado` | el vigilante | `fase=sin_llave\|sin_programa\|bajando\|arrancando\|corriendo\|caducado\|esperando\|parado`, `version`, `caduca` (AAAA-MM-DD), `dias`, `pid`, `reinicios`, `error`, y además `programa`, `vigilante` (su pid) y `cambio` (la hora) |
| `/rocola/datos/xp/rocola_soloist.txt` (+ `.1`) | el vigilante | Su bitácora y lo que dice Soloist, con la hora, **sin ninguna línea que lleve la llave**; se rota al pasar de 1 MB |

**`rocola_os.txt` desde la consola** sigue el formato de `RocolaOs.Ajustes.cs` (rama del andamio): `clave=valor`, LF, sin marca de UTF-8;
la primera línea de la clave se cambia en su sitio y las repetidas se quitan; lo demás (otras claves, comentarios) queda igual; se escribe
de un tirón (el de paso y el cambio de nombre) y el de antes queda como `.bak`, como lo guarda la rocola. Si el archivo no está y hay
`.bak`, se parte del `.bak` (es lo que leería la rocola); si no hay nada, se crea solo con esa línea. Con la rocola abierta no se toca (es
la dueña del archivo): espera hasta 25 s a que se cierre (al entrar en la consola se cierra sola) y si no, lo dice.

### La llave nunca sale de su archivo

- Solo vive en una variable del vigilante (sin exportar) y en la línea de órdenes de Soloist (`-k`: es la única forma que tiene; queda
  en `/proc/PID/cmdline`, que en la rocola solo lee root; nada de lo nuestro la apunta ni la sube).
- La bitácora quita **toda línea** que la lleve (lo que dice Soloist pasa por un filtro que recibe la llave por el entorno, no por su línea
  de órdenes).
- Ni el estado, ni `spotify estado`/`prueba`, ni los mensajes la enseñan: dicen «puesta» o «falta».
- Escrita en la consola: `read -s` (no se ve) y por la entrada de `rocola-soloist pon-llave` (nunca como argumento).
- Las copias de paso son ocultas (`.rocola_spotify_llave.txt.n`, `.usb.*`) y se borran; las de la USB se leen montándola en solo lectura.
- **El buzón** nunca sube `rocola_spotify_llave.txt`, `rocola_spotify_api.txt`, nada con «llave» o «_api.txt» en el nombre, la carpeta
  `soloist/` ni archivos ocultos (`permitido`). Sí sube, con «registros» o «archivo», `rocola_soloist.txt` y `rocola_spotify.txt`.
- En `estado.json` del buzón: `spotify: {modo, llave: "si"/"", llave_api: "si"/"", vigilante: (el estado sin la hora), rocola: (el de
  rocola-xp)}` y `bitacoras.soloist`.

### La memoria USB

Como `busca_llave` de `rocola-buzon`: primero en los otros discos (`/rocola/discos/*/` y un nivel más), después en la carpeta de la rocola
de XP, y después en las memorias USB removibles que no estén montadas (se montan en solo lectura, se copia el archivo a `/run` y se
desmontan). `rocola_spotify.txt` → `rocola_spotify_llave.txt` (la primera línea con algo, sin espacios ni marca; tiene que parecer una
llave: letras, cifras y `._~+/=:-`, de 8 a 1024); `rocola_spotify_api.txt` → solo `client_id=`, `client_secret=` y `refresh_token=` (la
primera de cada una; tienen que estar las dos primeras). Lo que no vale no se pone y se dice (sin enseñarlo).

- El vigilante: solo las que faltan (cada 30 s sin la de Soloist; la de la API al empezar y cada 15 min).
- `spotify llave` (`rocola-soloist llave`): las dos, y las cambia si las de la USB son otras.
- El buzón, en su vuelta de 15 min: las dos, y las cambia si son otras; apunta solo «Llave de Spotify puesta (de USB)» (y «Llave de la
  API web de Spotify puesta (de USB)») en `rocola_actualizaciones.txt`.

### ROCOLA_OS en el lanzador

`/usr/bin/rocola` abre `rocola-xp` con `ROCOLA_OS=1` (lo planeado en PLAN_FASE11.md, tanda A), salvo si existe
`/rocola/datos/xp/rocola_os_apagado` (el interruptor de emergencia: entonces `ROCOLA_OS` va vacía, que `RocolaOs.cs` toma como apagada, y
lo apunta en `rocola.txt`). Se mira cada vez que se abre la rocola. Solo para la rocola: el importador (`--importa-xp`) va sin ella. Una
`ROCOLA_OS` puesta desde fuera (pruebas) manda.

### Buildroot: el paquete rocola-pulso

- `package/rocola-pulso/` (Config.in, rocola-pulso.mk, funciones.txt), en `sistema/Config.in` y `BR2_PACKAGE_ROCOLA_PULSO=y` en
  `configs/rocola_a4_defconfig` (el perfil de QEMU sale de ese archivo: `compila.sh`; no hay `rocola_qemu_defconfig` en el repositorio).
  `external.mk` ya incluye todos los `package/*/*.mk`: no cambió.
- Compila `sistema/rocola/pulso` con `make -C ... CC="$(TARGET_CC)" CFLAGS="$(TARGET_CFLAGS)" todo` (el Makefile de pulso toma CC y CFLAGS
  de la línea de make y deja `libpulse.so.0` en su carpeta) y lo instala en `/usr/lib/rocola-pulso/libpulse.so.0`. Al instalar comprueba con
  `$(TARGET_NM)` que da **las 48 funciones** de `funciones.txt` (las que Soloist busca con dlsym, sacadas del programa por fuera); si falta
  una, falla la compilación.
- Va en su carpeta propia para que **nada más** del sistema lo encuentre: solo Soloist, por el `LD_LIBRARY_PATH` que le pone el vigilante.
- Soloist además pide glibc ≥ 2.38 (el sistema trae la 2.42), `libatomic.so.1` (está en `/lib`), libm y libc.
- Revisado a mano contra Buildroot 2026.02.3 (`pkg-generic.mk`: `$(PKG)_PKGDIR`, `TARGET_NM`, `TARGET_CFLAGS`) y contra los paquetes
  rocola-xp y rocola-spotify. **No se compiló Buildroot.**

### La compilación rápida y la completa

- `sistema/rapida.sh`, paso 5b: si `BR2_PACKAGE_ROCOLA_PULSO=y` y cambió `sistema/rocola/pulso` (o la base no trae
  `/usr/lib/rocola-pulso/libpulse.so.0`: sistema-37 y anteriores), compila rocola-pulso con el gcc del servidor de GitHub (Ubuntu 24.04,
  glibc 2.39; `-O2 -march=x86-64`: vale para el A4 y para QEMU), comprueba que da las 48 funciones y que **no pide ninguna versión de glibc
  que no esté en la libc/libm del sistema**, lo deja en `/usr/lib/rocola-pulso/` y apunta en el registro «rocola-pulso: libpulse.so.0 con
  las 48 funciones que busca Soloist; glibc hasta X (el sistema trae hasta Y)».
- El trabajo «decidir» de `.github/workflows/rocola-sistema.yml` no cambió: `sistema/rocola/pulso` cae en `sistema/rocola/` (la rápida
  sabe ponerlo); el paquete nuevo (`package/rocola-pulso`, `Config.in`, el defconfig) **pide la completa**, como todo paquete. Con
  **«rapida_ya»** se hace la rápida igual y rocola-pulso entra por el paso 5b (lo del defconfig espera a la siguiente completa, que lo
  compilará con Buildroot).
- Los guiones (`rocola-soloist`, `spotify`, `rocola`, `rocola-buzon`) están en `rootfs_overlay`: entran en la rápida.
- Lanzar cualquier compilación de GitHub, solo cuando el dueño lo pida.

### Lo que está probado (fuera de la rocola) y lo que no

Con el **BusyBox del sistema** (`salida-qemu/target`: sin `stat`, tar sin `-z`, sed sin `\x`), su curl y su jq, en una raíz aparte y su
propio espacio de procesos; con un **Soloist falso** en guion (nunca el de verdad), un servidor de mentira que hace de
soloist-builds.spotifycdn.com, de apresolve.spotify.com y del WebSocket, y una memoria USB de mentira (imagen en un loop con un
`/sys/class/block` de mentira). Todo inventado.

- `sistema/rocola/Pruebas/Soloist/prueba.sh`: 23 casos desde la revisión (abajo; antes 20: bajar bien, CDN caído, tar roto, disco lleno, sin disco, caducado, caduca pronto,
  caídas, arranque lento, dos vigilantes, muere el padre, SIGTERM medido, la llave desde la USB, la llave nunca aparece, `spotify
  encender/apagar`, `spotify estado`, `spotify prueba`, emparejar y olvidar, el entorno de Soloist, ROCOLA_OS en el lanzador; y de la
  revisión: `restos`, `padrebaja` y `cambio`): **100 de 100 bien** (la pasada entera del revisor, 14-11-2026; antes de la revisión, 89 de
  89 en 20 casos), y `sh -n` con el BusyBox del sistema de los cuatro guiones tocados.
- `sistema/rocola/Pruebas/Soloist/rapida-pulso.sh`: el paso 5b de `rapida.sh` contra un rocola-pulso de mentira y, desde la revisión,
  contra el **de verdad** (el de `sistema/rocola/pulso`, o `ROCOLA_PULSO_FUENTE`): **6 de 6** (el de verdad, de la rama fase11-pulso:
  compila, da las 48 y pide glibc hasta la 2.34).
- `sistema/rocola/Pruebas/Lanzador/prueba.sh` (las de antes del lanzador), con el lanzador nuevo: 92 de 94 en la pasada entera (las 2
  del caso `caidas`, por tiempos con la máquina cargada: repetido solo, 7 de 7, igual que con el lanzador de antes).

**No probado**: nada con el Soloist de verdad, ni con una cuenta, ni en el A4, ni el paquete de Buildroot compilado. Lo que hay que mirar
en el A4 está en la lista de la junta.

### Para probarlo fuera de la rocola

Variables (los guiones de verdad no las usan): `ROCOLA_RUN`, `ROCOLA_XP_DATOS`, `ROCOLA_DISCO`, `ROCOLA_DISCOS`, `ROCOLA_SYS_BLOCK`,
`ROCOLA_SOLOIST_DATOS`, `ROCOLA_PULSO_DIR`, `ROCOLA_SPOTIFY_ANILLO`, `SOLOIST_URL` (de dónde se baja), `SOLOIST_BIN` (un soloist ya puesto:
no se baja nada), `ROCOLA_SPOTIFY_APRESOLVE`, y los relojes `ROCOLA_SOLOIST_ESPERAS` («2 10 30 120»), `_SEGUIDA` (600), `_ARRANQUE` (90),
`_TERM` (8), `_USB` (30), `_REVISA` (3600), `_REINTENTO` (3600), `_CADUCADO` (600), `_CANDADO` (30), `_NICE` (0), `_MIN_DISCO_KB` (65536),
`_MIN_DATOS_KB` (40960); `ROCOLA_SPOTIFY_ESPERA` (25) en `spotify`.

---

## Revisión (llave-y-robustez)

14-11-2026, revisor independiente del grupo «soloist» (enfoque: que la llave nunca salga de su archivo, que todo corra con el BusyBox del
sistema, los caminos de falla, el lanzador, el paquete de Buildroot y `rapida.sh`). Se leyó el diff entero contra la base y se repitieron
las pruebas. Lo que estaba bien: el filtro de la llave en la bitácora, el estado y las órdenes sin secretos, la lista del buzón
(`permitido`: ninguna llave, ni `soloist/`, ni ocultos), las copias de paso con `umask 077`, ningún `stat` (todo `date -r`; las applets
que usan los guiones están todas en el BusyBox del sistema, comprobado con su `--list`), el cambio del lanzador (8 líneas, solo
`ROCOLA_OS`) y el paquete contra Buildroot 2026.02 (`$(PKG)_PKGDIR`, `TARGET_NM`, `TARGET_CFLAGS`).

Lo que se encontró y se arregló (cada uno con su prueba, que **falla con el código de antes**: 5 MAL de 14 con el `rocola-soloist`
anterior, medido):

1. **Grave: un hijo de Soloist fuera de su grupo colgaba el cierre.** El programa trae el ayudante de fallos **crashpad** (está en sus
   cadenas) y en Linux ese ayudante se suele separar en su propia sesión: la señal al grupo no lo alcanza y se queda con la salida de
   Soloist, la tubería de la bitácora. El vigilante esperaba a la bitácora (`wait`) para siempre: con SIGTERM no salía (medido con el de
   antes: más de 15 s, Soloist «corriendo» en el estado; rocola-xp lo habría matado con SIGKILL a los 10 s y el ayudante seguiría vivo).
   Ahora `recoge` espera a la bitácora como mucho unos 2 s y cierra lo que quede **fuera del grupo**: los procesos que siguen escribiendo
   en esa tubería y los que son el mismo programa sacado a `/run` (salvo `ctl`, `--version` y `-p`, que son de la consola). Lo mismo al
   arrancar el vigilante (lo de uno anterior). Se apunta «Quedaban procesos de Soloist fuera de su grupo (pid ...)». Un solo `ls` y un
   `awk` para todo `/proc` (no un proceso por cada uno: el cierre tiene que caber en los 10 s). Caso nuevo `restos`.
2. **Muere el padre a media bajada**: `corta` (las esperas largas: curl hasta 15 min, `--version`) solo se cortaba con SIGTERM; si la rocola
   se caía mientras bajaba, el vigilante seguía bajando con el candado y el vigilante de la rocola reabierta esperaba. Ahora mira al
   padre cada 0,2 s también ahí. Medido con el de antes: 16,3 s (lo que tardaba el servidor lento); ahora, abajo. Caso nuevo `padrebaja`.
3. **El cierre medido por el reloj**: la espera de 8 s tras SIGTERM contaba vueltas de 0,1 s y en cada vuelta abría un `awk`; en el A4
   (lento abriendo procesos) los 8 s podían ser 9 y pico, demasiado cerca de los 10 s de rocola-xp. Ahora cuenta por `/proc/uptime` y
   mira si Soloist vive (y al padre) con `read`, sin abrir procesos.
4. **«Caduca pronto» con Soloist en marcha y la uri de rocola-xp**: el vigilante solo cambia al programa nuevo si la rocola no está
   tocando nada de Spotify, y lo miraba con `sonando=1`; el núcleo de rocola-xp (grupo spotify-xp) escribe en `sonando=` la **uri** de la
   que suena (o nada). Con eso lo habría cambiado a media canción. Ahora cualquier valor que no sea vacío, `0` o `no` es «suena» (y
   `spotify estado` lo dice bien). Este camino no tenía prueba: caso nuevo `cambio` (con la uri no cambia; vacío y `en_cola=0`, sí).
5. **La llave dicha a medias**: la bitácora quitaba las líneas con la llave entera; ahora también las que llevan sus primeras o sus
   últimas 10 letras (si Soloist la dijera recortada). Comprobado en el caso `secreto`.
6. **Caducado sin uno nuevo**: cada 10 min se volvía a bajar **entero** (14 MB) para ver si había otro; ahora entero solo la primera vez y
   después con If-Modified-Since (304 si no hay uno nuevo). Comprobado en `caducado`.
7. **Menores**: sin sitio en los datos, el de paso de la llave (`.rocola_spotify_llave.txt.n`, con un trozo de ella) ya no se queda;
   una copia `.usb.*` o una memoria montada en `/run/rocola-soloist/usb` que quedaran de una vez cortada con SIGKILL ya no cuentan ni
   bloquean (antes la memoria se saltaba siempre por «ya montada»); `saca` usa una carpeta de paso propia (`bin.n.PID`: «spotify bajar» o
   «spotify prueba» en la consola a la vez que el vigilante no se pisan; las que quedan de uno muerto, 36 MB en memoria, se quitan). En
   el buzón: la copia de paso de la USB con `umask 077` (antes quedaba legible un momento en `/run`) y la llave de la API escrita en un de
   paso en la **misma** carpeta y después el cambio de nombre (de `/run` a los datos, `mv` copiaba encima: un apagón la dejaba a medias).
   En Buildroot y en `rapida.sh`: `make limpia` antes de `todo` (el sitio local se copia tal cual: un `libpulse.so.0` compilado a mano en
   la carpeta, con el gcc de la máquina, se habría tomado por hecho); y `rapida.sh` dice por qué falla si no encuentra la libc del sistema
   (con `set -e` salía sin decir nada).

Repetido por el revisor (con el BusyBox del sistema y el Soloist falso; nunca el de verdad):

- `Pruebas/Soloist/prueba.sh`, pasada entera: **100 de 100 bien**, 23 casos (los 20 de antes y `restos`, `padrebaja`, `cambio`), y `sh -n`
  con el BusyBox del sistema de los cuatro guiones. Tiempos hasta que no queda nada: SIGTERM 170 ms; Soloist que no hace caso 8320 ms
  (antes 8470); a media bajada 140 ms; muere el padre 650 ms; muere el padre a media bajada 240 ms (antes 16 290); con un hijo fuera del
  grupo 800 ms (antes más de 15 s, sin salir). Las esperas tras caídas, 1 2 3 4 4 s.
- Con el `rocola-soloist` de antes, los casos nuevos: `restos`, `padrebaja`, `secreto` y `caducado`, **5 MAL de 14**; `cambio`, **1 MAL de 3**
  (cambiaba el programa con una de Spotify sonando).
- `Pruebas/Soloist/rapida-pulso.sh`: **6 de 6**, con un caso nuevo con el rocola-pulso **de verdad** (rama fase11-pulso,
  `ROCOLA_PULSO_FUENTE`): el paso 5b lo compila con su Makefile, da las 48 funciones y pide glibc hasta la 2.34 (el sistema trae la
  2.42); con un `libpulse.so.0` viejo encima en su carpeta, `make limpia` lo quita y compila de nuevo.
- `Pruebas/Lanzador/prueba.sh` (las de antes del lanzador y del buzón), con todo lo de esta rama: **94 de 94 bien**.
- Un cambio en la prueba: `caidas` medía las esperas redondeando y daba 1 s de más según quién ganaba la carrera entre la salida del
  Soloist falso y la primera mirada del vigilante (con la mirada sin abrir procesos, el vigilante llega antes y espera los 0,5 s de su
  vuelta); ahora cuenta los segundos enteros hacia abajo (espera entre `e` y `e`+1 s).

Lo que queda (no se pudo arreglar aquí):
- La llave de Soloist va en su línea de órdenes (`-k`, la única forma que acepta: no hay variable de entorno ni archivo, lo dice su
  documentación). `/proc/PID/cmdline` lo puede leer cualquier proceso del equipo, no solo root. Nada nuestro lo apunta ni lo sube, pero
  si se quiere cerrar del todo: montar `/proc` con `hidepid=invisible` (cambio del sistema, para la junta; los guiones que leen `/proc`
  corren como root y no les afecta).
- Que Soloist de verdad deje un hijo fuera de su grupo (crashpad) es una suposición por sus cadenas; si no lo hace, lo nuevo no hace nada.
  En el A4: tras `spotify apagar`, que no quede ningún proceso del programa (`ls -l /proc/*/exe | grep rocola-soloist`).
- El formato de `soloist --version` sigue sin verse; `client expires in %d %s%s` sí está en sus cadenas (la cuenta de los días lo lee).
