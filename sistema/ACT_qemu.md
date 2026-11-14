# Activación de rocola-xp: el sistema entero en QEMU (grupo qemu)

14-11-2026. Rama `act-qemu`, sobre f62d6cb, con las dos ramas de la tanda juntadas (`act-xp-datos` y `act-sistema-activa`). Pone a
prueba, de punta a punta y en una máquina emulada, lo que hicieron los dos grupos: Rocola OS con `rocola-xp` como único programa
(decisión del dueño del 14-11-2026) y lo aprendido por la rocola de XP traído a su carpeta. **Todo con datos inventados** (tonos, ruido,
un video de barras, una carpeta de XP falsa) y **en QEMU sin KVM**. QEMU no es el A4 (otra gráfica, otro sonido, otro procesador): nada
de esto dice cómo va en el A4. No se lanzó nada en GitHub ni se hizo push.

Los guiones están en `sistema/rocola/Pruebas/Qemu` (su `LEEME.md` dice cómo repetirlo). Lo que dejó la prueba (capturas, lo grabado de
la tarjeta de sonido, los registros) está en `/root/rocola-trabajo/act/qemu`, fuera del repositorio.

## 1. La junta

- `git merge --no-ff act-xp-datos` y después `act-sistema-activa`, sobre f62d6cb: **sin choques** (los dos grupos tocaron archivos
  distintos).
- **El contrato del importador casa**: el lanzador llama `ROCOLA_DATOS=/rocola/datos/xp timeout -k 10 600 rocola-xp --importa-xp
  /run/rocola-datos-xp` (el archivo de `rocola-discos`, cuya primera línea dice la carpeta), con `ROCOLA_IA_DATOS` ya puesta; el
  importador (`Nativo/ImportaXp.cs`, `CarpetaXpDe`) acepta el archivo o la carpeta, sin discos busca en `/rocola/discos/*` y
  `/rocola/disco`, sale con 0 (también «ya importado») o con 2, 3 o 4 y una línea; el lanzador abre la rocola igual. Comprobado además
  de verdad en QEMU (apartado 4): el primer arranque «importado de /rocola/disco/Rocola/Release a /rocola/datos/xp: 6 tablas, 35 líneas
  con la ruta traducida, 0 fuera»; los siguientes, «ya importado ...: nada que hacer».
- `rocola-xp` compilado de la rama juntada: `dotnet build -c Release`, **0 errores y los 13 avisos de siempre** (SYSLIB0054 ×9, CA2022 ×2,
  SYSLIB0050, CA2014); NativeAOT con el perfil de QEMU y con el del A4, **0 avisos IL/AOT** (los mismos 13). `Codigo/`: los 109 archivos con
  las mismas líneas y los mismos CRLF que en f62d6cb; `diff-xp.py -v` de los tres que tocó xp-datos: `Form1.Ia.cs` 14/11, `Form1.Portadas.cs`
  76/73, `Form1.Datos.cs` 47/44 (las 2 «sin marca» de siempre, continuación de su comentario «EN LINUX»). Esta tanda no toca `Codigo/`.

## 2. La imagen para QEMU

La imagen del A4 no corre en QEMU (lleva instrucciones del Piledriver: `sistema/LEEME.md`). Se usó la última imagen **completa del perfil
de QEMU** (`/root/rocola-trabajo/salida-qemu/images`, `ROCOLA_PERFIL=qemu sh sistema/compila.sh` del 6-11-2026, commit b6e8f14: núcleo
6.12.47, Mesa con softpipe, mpv 0.40, BusyBox 1.37) y se le puso encima, con `arma.sh`, **lo mismo que pone `sistema/rapida.sh`**:
`rootfs_overlay` entero de esta rama (y fuera lo borrado desde b6e8f14: `rocola-spotify-evento`), el menú de la memoria USB, el
`initrd.gz` del sistema instalado con el `init` de ahora, `rocola-xp` (NativeAOT, perfil de QEMU: sin `IlcInstructionSet`) con
`libSkiaSharp.so`, su `version` («rocola-xp programa 0 (26d241b) compilado 1794641435») y la canción de prueba. Es el camino por el que va
a llegar a la rocola (la rápida con «rapida_ya» sobre sistema-36): como allí, el `rocola-consola` de la base se queda en `/usr/bin` sin
usarse. No se recompiló Buildroot: no hacía falta para lo que se prueba, y cambiar `BR2_EXTERNAL` a esta copia rehacía el núcleo y BusyBox
y pedía `rocola-spotify` (Rust, una hora) sin aportar nada aquí.

**Solo para la prueba** (no va en Rocola OS; `arma.sh` lo dice y deja `/etc/rocola-prueba-qemu`): una línea de órdenes de root en el
puerto serie (`ttyS0::respawn:-/bin/sh` en `/etc/inittab`), para mirar archivos y procesos desde fuera sin tocar tty1 ni la consola de
mantenimiento. Como el instalador copia la partición del sistema tal cual, el sistema instalado también la lleva.

La máquina (`maquina.py`): QEMU 8.2 sin KVM (TCG), `-cpu SandyBridge` (AVX sin AVX2: lo más parecido al A4 que emula QEMU), 2 núcleos y
3 GB, la VGA de bochs con la pantalla a 1024x768 (la del A4; dentro, `bochs-drm` y Mesa softpipe por KMS/GBM/EGL), el sonido HDA de QEMU
grabado a un WAV, el disco de XP falso con una capa qcow2 encima (el disco base no se toca), sin red. Las teclas entran por el teclado PS/2
emulado (`sendkey` del monitor) y llegan a `rocola-xp` por evdev, como las del A4.

## 3. Los datos inventados

- `mundo.sh`: cinco canciones que son tonos de frecuencias distintas (196, 330, 440, 660 y 1000 Hz; 45 s) y un WAV de ruido rosa, en
  carpetas «Artista - Disco» como las de la rocola de XP (`Los Inventados - Disco Uno`, `Banda Ficticia - Primer Disco`, `Grupo Prueba -
  Disco Dos`; una con acentos: «02 Canción Ñandú.mp3»), sus portadas (`folder.jpg` de colores), un video de 90 s (barras de prueba de
  ffmpeg con un tono de 880 Hz; de 90 s para que la tarjeta, tan lenta en QEMU, llegue a pintarlo: apartado 5) y una canción **nueva** que XP no midió (`03 Ruido Nuevo.wav`). Lo que mide `rocola-xp` de esa música
  (fuera de pantalla, en esta máquina) pasa a `D:\musica\...` (`mundo_xp.py`) y forma la carpeta de la rocola de XP falsa
  (`D:\Rocola\Release`): las seis tablas con rutas de Windows y la fecha de creación de una copia de Windows en `rocola_fuentes.txt`,
  `rocola_teclas.txt` (la moneda es la M), `rocola_config.txt` (`dir=D:\musica`, el modo noche), lo más sonado, **un estado del día con
  5 créditos que no se debe traer**, un error viejo, los modelos de IA del repositorio y un `Rocola.exe` falso.
- `discoxp.sh`: el disco de XP falso como el del A4 (`sistema/ETAPA2.md`, «Revisión a fondo del instalador...»): MBR con la primera
  partición en el sector 63 (el hueco de 62 sectores), la 1 un NTFS con «Windows XP» (un `ntldr` y un `boot.ini` falsos) y la 2 un NTFS de
  4,3 GB con la música y la carpeta de XP; números de serie distintos (dos NTFS formateados en el mismo segundo los repetían); el código
  de arranque del primer sector no es el de Windows (unos bytes que no hacen nada). El `discoxp.img` de antes no se usó: sus carpetas de
  música llevan nombres de artistas reales.

## 4. La prueba y lo que salió

`prueba.py` corre las fases en orden sobre el mismo disco (lo que una deja lo usa la siguiente). Cada comprobación dice BIEN o MAL;
INFO es lo que se apunta sin juzgar. **La corrida final** (10:20-11:03, desde cero: el disco de XP recién hecho, instalar, y todas las
fases con los guiones de ahora): **58 BIEN y 1 MAL**, y ese MAL era de la prueba (contaba 6 canciones en una tabla y ya había 7: la
rocola había medido la canción nueva; comprobado después a mano: ninguna ruta de Windows; corregido en `prueba.py`). Antes hubo cuatro
corridas que sirvieron para corregir la prueba (apartado 5). Lo que queda, en `/root/rocola-trabajo/act/qemu`: `prueba-salida-final.txt`
(y `-final-a.txt`, la instalación: la corrida se paró al empezar el primer arranque por un fallo de la prueba -apartado 5- y siguió con la
misma máquina ya arrancada), `capturas/`, `sonido/` y, de las corridas anteriores, `corridas-1-4/`.

| Corrida | Fases | Resultado |
|---|---|---|
| 1 (08:41-09:36) | instala, arranque, teclas, consola, caida, apagon, ia, boton | 46 BIEN, 6 MAL: los 6 de la prueba (apartado 5) |
| 2 (09:37-09:59) | video, caida, apagon, boton, a4 (la prueba corregida) | 18 BIEN, 2 MAL: los dos del video, otra vez de la prueba |
| 3 y 4 (10:00-10:10) | video (navegando con `gpu=0`, viéndolo con la tarjeta) | 3 BIEN, 1 MAL con el video de 20 s (no llegó a pintarse); 3 BIEN, 0 MAL con el de 90 s |
| **final** (10:20-11:03) | instala, arranque, teclas, video, consola, caida, apagon, ia, boton, a4 | **58 BIEN, 1 MAL** (de la prueba) |

### P1: arranca, trae lo de XP, la pantalla, las teclas y el sonido

| Comprobación | Cómo | Resultado (corrida final) |
|---|---|---|
| El instalador, desde la memoria USB (por USB), encuentra el disco | arranque de la memoria y el disco de XP; `/tmp/rocola-instalar.txt` por el puerto serie | BIEN: elige `sda2` (la música), ve XP en `sda1` y los datos de XP en `/Rocola/Release`; el arranque mide 31 284 bytes para un hueco de 31 744 (`capturas/01-instalador.png`) |
| Instala (ENTER: `sendkey ret`) | hasta «instalado en la copia a» | BIEN, en 2 min 13 s: guarda los 63 sectores de antes, copia y comprueba el sistema, GRUB sin errores (`02-instalado.png`) |
| Arranca el sistema instalado | solo el disco de XP: GRUB del hueco, el núcleo y el initramfs desde el NTFS | BIEN: `rocola.slot=a rocola.disco=2b3c4d5e6f708192`; el NTFS en `/rocola/disco`, el sistema y `datos.img` por loop |
| `rocola-discos` | `/run/rocola-discos.txt`, `/run/rocola-musica` | BIEN: la carpeta de XP en `/rocola/disco/Rocola/Release`, la música en `/rocola/disco/musica` |
| La prueba de sonido y gráfica (primer arranque) | `/root/rocola-prueba.txt` | BIEN: el tono, la canción de prueba (mpg123), el cubo (900 cuadros a 6 por segundo) y «Pantalla de la rocola: se dibujo 20 s (y se cerro bien)». (En la corrida 1, «pero no se cerro en 10 s: se corto»: apartado 6.) |
| El lanzador corre el importador y abre `rocola-xp` | `/rocola/datos/rocola.txt`, `/proc/PID/environ` | BIEN: «importado de /rocola/disco/Rocola/Release a /rocola/datos/xp: 6 tablas, 35 lineas con la ruta traducida, 0 fuera», y `rocola-xp` abierta con `ROCOLA_DATOS=/rocola/datos/xp`, `ROCOLA_IA_DATOS=/rocola/disco/rocola-os/ia-xp` y `ROCOLA_ALSA=plughw:0,0`. Las veces siguientes, «ya importado ...: nada que hacer» |
| Lo traído | `/rocola/datos/xp` | BIEN: la marca dice la carpeta y «unidad D: -> /rocola/disco (8 de 8 de la muestra)»; las tablas sin ninguna ruta de Windows y con las canciones en `/rocola/disco/musica/...` (también «02 Canción Ñandú.mp3», con su nombre del disco); el estado del día de XP (5 créditos) **no**; los botones tal cual; la configuración de XP con `dir=/rocola/disco/musica` una sola vez (la del importador; el lanzador no la repite) |
| La carpeta de XP sin tocar | la suma MD5 de cada archivo, dentro y en la máquina de pruebas | BIEN: 13 y 13 iguales |
| La pantalla por KMS | captura del monitor de QEMU | BIEN: la rocola y no la consola (17 726 colores): la atracción de XP con la portada del disco y la estrella (`03-rocola.png`) |
| Las teclas: la moneda | la M (`moneda=77`, de los botones traídos de XP), por el teclado emulado y evdev | BIEN: 4 créditos |
| Pedir una canción | ENTER abre el disco de la letra B, ENTER pide su primera | BIEN: «01 Pulso Grave» suena y quedan 3 créditos (`04-sonando.png`) |
| **Suena** | lo grabado de la tarjeta HDA (`sonido/arranque.wav`), por segundos | BIEN: 44 s de 196 Hz a -4,2 dBFS (antes, el tono de 440 Hz y la canción de prueba de la prueba de arranque) |
| Lo más sonado y el sello | `rocola_stats.txt`, `rocola_fuentes.txt` | BIEN: la cuenta sube de 2 (de XP) a 3; el sello traído vale tal cual con el NTFS leído por ntfs3: la canción no se volvió a medir (el riesgo que apuntó xp-datos) |

### P2: la consola, el botón, el apagón y lo grande

| Comprobación | Cómo | Resultado (corrida final) |
|---|---|---|
| Ctrl+Alt+F2 cierra la rocola | `sendkey ctrl-alt-f2`, con 10 créditos | BIEN: cerrada en 7 s con SIGUSR1, apuntado «La rocola se cerro para la consola de mantenimiento», los 10 créditos se conservan y no se abre en la consola |
| La consola se ve | captura sin tocar ninguna tecla | BIEN con el arreglo (apartado 5): «rocola login:» al momento (`08-consola.png`). Sin él, negro hasta tocar una tecla (`explora/caps/a15.png`) |
| Ctrl+Alt+F1 la vuelve a abrir | | BIEN: abierta otra vez, con los 10 créditos |
| El botón de encendido | `system_powerdown` (ACPI): `acpid` y `/etc/acpi/PWRF/00000080` | Con el dibujo por procesador (`gpu=0`, apartado 6): BIEN, el estado del día se borró a los 2 s (cierre con `WindowsShutDown`), la máquina se apagó sola a los 8 s y al prender no había créditos. Con la tarjeta, en QEMU no llega (apartado 6) |
| Apagón con créditos, una sonando y otra en la cola | `echo 0 > probando` (un sistema nuevo en prueba) y QEMU se cierra en seco (`quit`) | BIEN: el estado sobrevive (12 créditos, la que sonaba y la cola); al prender **reanuda**: 45 s de 196 Hz (la que sonaba, desde el principio, como XP) y 44 s de 660 Hz (la de la cola) (`sonido/apagon.wav`; el aviso «Reanudando la musica», al explorar: `explora/caps/d03.png`) |
| Lo grande, fuera de lo aprendido | `ROCOLA_IA_DATOS` | BIEN: el material de la IA de la canción nueva (un WAV con sus agudos) en `/rocola/disco/rocola-os/ia-xp/rocola_ia_datos.bin` y de qué canción salió en lo aprendido (`rocola_ia_hechas.txt`); el respaldo y las miniaturas de las portadas (5) también en `ia-xp`; nada de eso en `/rocola/datos/xp` (`datos.img`: 360 KB usados de 487 MB) |
| El NTFS queda limpio | `/proc/fs/ntfs3/sda2/volinfo` | BIEN: «clean» (el `sync` de `S99rocola`) |

### P3: el video, una caída y la confirmación

| Comprobación | Cómo | Resultado (corrida final) |
|---|---|---|
| Un video se ve y suena | la letra de los videos (cuatro veces ARRIBA desde la B, un cuadro entre una y otra), ENTER y ENTER, con el dibujo por procesador (rápido en QEMU); después, vuelta a la tarjeta por la consola de mantenimiento y la rocola lo reanuda con la tarjeta (`gpu=1`) | BIEN: pedido (un crédito); con la tarjeta **se ve** a pantalla completa y con sus colores, con el aviso «¡ PRESIONA UN BOTON PARA USAR LA ROCOLA !» de XP (`12-video-tarjeta-8.png`: el cuadro 322, a los 12,9 s; 590 751 puntos de los colores de las barras) y **suena** (105 s de 880 Hz en `sonido/arranque.wav`: los 90 del video con la tarjeta y lo que sonó antes con `gpu=0`). Con un video de 20 s (corrida 3) sonó pero no llegó a pintarse (20 a 30 s por cuadro): por eso el video de la prueba dura 90 s |
| Una caída | `kill -SEGV` a `rocola-xp` desde el puerto serie | BIEN: apuntado «rocola-xp se cayo (codigo 139) a los 154 s: se vuelve a abrir en 2 s» y abierta otra vez (otro proceso). El primer SIGSEGV mandado con `kill` lo recoge .NET (apartado 5): hizo falta un segundo |
| Un sistema nuevo solo se confirma tras 120 s en pie | `probando` puesto antes del apagón; el initramfs lo cuenta y `S99rocola` espera `/run/rocola-confirmado` | BIEN: `probando` pasa a 1 al arrancar, se borra cuando `rocola-xp` lleva 122 s en pie y `sistema.txt` dice «el sistema 0 quedo bien» (corridas 1 y 2: 123 y 125 s) |

### Además: el `rocola-xp` del perfil del A4

`rocola-xp` compilado como lo compila `rapida.sh` para el A4 (`IlcInstructionSet x86-64-v2,avx`), en un disco aparte (`a4.img`), copiado
encima del instalado y reiniciado como «reiniciar ya» (SIGUSR1 con su marca). Con `qemu-x86_64` de usuario, en procesadores sin AVX
(`qemu64`, `Nehalem`) no arranca y lo dice («The required instruction sets are not supported by the current CPU»); en `SandyBridge` (AVX
sin AVX2) sí. En la máquina (corrida final y corrida 2): el procesador emulado tiene AVX y no AVX2; contesta `--version` («rocola-xp
programa 0 (ab6a26e) compilado 1794645805»), se abre, pinta la pared por KMS (`13-a4.png`) y una canción pedida suena (44 s de 196 Hz en
`sonido/a4.wav`). (Las corridas 3 y 4 del video ya fueron con este.) Eso dice que no lleva instrucciones de más para un procesador como el
del A4 (AVX sin AVX2), no que funcione en el A4.

## 5. Lo que se arregló

1. **La consola de mantenimiento se quedaba en negro** (`usr/bin/rocola`, commit ab6a26e). Ctrl+Alt+F2 cerraba `rocola-xp` con SIGUSR1
   como debe, pero la pantalla seguía negra hasta tocar una tecla: `rocola-xp` era la dueña de la pantalla (KMS), la consola cambió debajo
   de ella y al soltarla el núcleo no la repintó (`explora/caps/a15.png`; con una tecla, «rocola login:», `a16.png`). El dueño la vería
   como si Ctrl+Alt+F2 no hubiera hecho nada (la queja del 7-11-2026 en el A4 era justo esa tecla). Ahora el vigilante de la consola, después
   de cerrar la rocola, pasa la consola de delante a video inverso y vuelve (`ESC [?5h ESC [?5l`), lo que obliga a repintarla entera; solo
   si había una rocola abierta. Comprobado: a mano en QEMU (`a19.png`), `sh -n` con el BusyBox del sistema, `prueba.sh` del lanzador
   (consola, reinicia, colgar: 17 BIEN, 0 MAL; entero, sus 16 casos: 94 BIEN, 0 MAL, y el caso «contrato» con el importador de verdad de esta
   rama: 5 BIEN, 0 MAL) y las corridas 1 y final («rocola login:» sin tocar nada, `08-consola.png`). En QEMU la pantalla es la
   de bochs, con su copia en memoria; con la Radeon del A4 puede que no pasara, pero repintar no estorba.

Lo demás que salió MAL era de la prueba, no del sistema, y se corrigió en los guiones (corrida 1, salvo donde se dice):

- **El video** (dos MAL en la corrida 1, dos en la 2 y uno en la 3): con la tarjeta cada cuadro tarda 20 a 30 s; cuando la prueba fue a
  pedir el video, la canción ya había acabado y la rocola estaba en la atracción (la primera tecla solo despierta). Y las cuatro ARRIBA
  seguidas para llegar a la letra de los videos solo cambian una letra: la rocola cambia de letra una vez por cuadro y las pulsaciones que
  llegan juntas mientras pasa la página se pierden (lo mismo en XP). Ahora se navega con el dibujo por procesador y se ve con la tarjeta
  (apartado 4, P3), con un video de 90 s: el de 20 s sonaba entero antes de que la pantalla llegara a pintarlo.
- **La caída** (dos MAL): un `kill -SEGV` no la tiró: .NET atrapa SIGSEGV (para sus excepciones), y si la señal no viene de un fallo de
  memoria de verdad vuelve a poner el manejador de fábrica y sigue. El segundo SIGSEGV ya la tira (código 139), como la tiraría un fallo de
  verdad. No es un fallo de la rocola; para probar una caída en el A4: dos veces `kill -SEGV`, o `kill -ABRT`.
- **«Y reanuda»** (un MAL): la prueba miraba la tarjeta cuando ya habían sonado las dos canciones (cinco minutos de cuadros lentos); ahora
  se mira lo grabado.
- **«La máquina se apagó sola»** (un MAL): sí se apagó; QEMU quedó como proceso zombi de la prueba y la prueba lo contaba como vivo.
- En la corrida final, dos más de la prueba: la línea de órdenes del puerto serie se quedó esperando el final de una comilla (lo que la
  prueba tecleó mientras la máquina aún arrancaba llegó a medias) y ya no contestaba: ahora cada orden va precedida de Ctrl+C. Y «las
  tablas traducidas» contaba exactamente 6 canciones en `rocola_volumen.txt`, pero si la rocola lleva un rato abierta ya midió la
  canción nueva (7): ahora pide al menos las 6 de XP.
- Antes de la corrida 1, al explorar: las portadas no salían porque la música inventada iba en `Artista/Disco/` (XP toma la primera carpeta
  bajo «musica» por el disco); ahora va en `Artista - Disco/`, como la de la rocola. Y la canción de 220 Hz se confundía con la canción de
  prueba del sistema (también 220 Hz): ahora es de 196 Hz.

## 6. Lo que QEMU no puede decir (y lo que conviene mirar en el A4)

- **La lentitud de QEMU sin KVM.** Con la tarjeta (`gpu=1`, lo de fábrica), Mesa softpipe emulado tarda **20 a 30 s por cuadro** de la
  rocola (`rocola_error.txt` se llena de «Pantalla parada 26 000 ms»), y `rocola-xp` atiende las teclas y las señales de cierre entre
  cuadro y cuadro. Por eso en QEMU, y no por un fallo de la rocola:
  - la prueba de arranque le da 20 s y otros 10 para cerrarse: en la corrida 1 dijo «se dibujo 20 s (pero no se cerro en 10 s: se
    corto)» y en la final «se dibujo 20 s (y se cerro bien)»: depende de en qué parte del cuadro le llega la señal;
  - la consola de mantenimiento la corta si no se cerró en 20 s: al explorar pasó una vez («rocola-xp no se cerro en 20 s: se corto (el
    estado queda como tras un apagon)»: el estado sobrevive igual);
  - **el botón de encendido**: `00000080` le da 15 s y apaga; al explorar, con la tarjeta, la rocola no llegó a cerrarse y **el estado del
    día sobrevivió** (7 créditos al prender, contra la regla del 8-11-2026). Con `gpu=0` (unos 5 s por cuadro) se borró a los 2 a 5 s y la
    máquina se apagó a los 8 a 11.
  En el A4 cada cuadro debería tardar milisegundos, pero **no está medido allí**. Si el A4 tardara en atender el cierre (al arrancar, con
  una biblioteca grande, o con una hoja abierta), lo primero que lo diría es el botón: conviene probar en el A4 apagar con el botón con
  créditos puestos y ver que al prender no están; si sobreviven, subir la espera de `00000080` (Windows XP le daba 20 s a cada programa).
- **El cierre del primer SIGSEGV** (apartado 5): .NET lo atrapa. Una caída de verdad no se ve afectada.
- **«Cortes de sonido» en `rocola_error.txt`** (corrida final): «"Pulso Grave": 1 aviso tardio de la salida (el peor, 280 ms); latencia
  abierta 1000 ms» y otro de 538 ms: la caja negra del sonido de XP apunta que la salida avisó tarde (con la pantalla comiéndose la máquina
  emulada); en lo grabado no se ve ningún hueco. En el A4 conviene mirar ese archivo después de una noche de música.
- **Lo que suena** pasa por la tarjeta HDA de QEMU (`hda-duplex`, códec genérico), no por la Realtek del A4; `rocola-tarjeta` eligió
  `plughw:0,0`. Que el sonido salga por la salida verde del A4 no se puede ver aquí.
- **La gráfica** es `bochs-drm` con softpipe, no la Radeon con r600: no dice nada de r600, ni de la velocidad, ni de cómo se ve con la
  Radeon. El video se vio con `gpu=0` al explorar (`explora/caps/c05-2.png`: las barras a pantalla completa con el aviso «¡ PRESIONA UN
  BOTON PARA USAR LA ROCOLA !» y su tono de 880 Hz) y con la tarjeta en las corridas 4 y final (`capturas/12-video-tarjeta-8.png`), sin el
  negro ni el magenta de los arreglos del 7-11-2026.
- Tras un apagón la canción que sonaba vuelve a empezar desde el principio (como en XP).
- El instalador deja montada en solo lectura la partición de XP que miró (`/tmp/rocola-mira/sda1`) hasta reiniciar; no estorba.

## 7. Lo que no se probó

- **Nada en el A4.**
- La red: el buzón (Rocola en casa), `rocola-actualiza` (el sistema nuevo se simuló poniendo `probando` a mano), la hora por NTP; la
  máquina iba sin red.
- `rocola-sonido`, «reiniciar ya» desde el buzón y la memoria USB sin instalar («Probar Rocola OS sin instalar»): sin cambios desde que los
  probó sistema-activa con programas falsos; aquí no se repitieron con el sistema entero.
- El Windows XP de verdad (el `ntldr` es falso: no se probó volver a XP desde el menú) y un NTFS hecho por Windows (este lo hizo
  `mkntfs`/`ntfs-3g`); la carpeta de XP de verdad y su música.
- `rapida.sh` en sí: se reprodujeron sus pasos (`arma.sh`) sobre la imagen de QEMU; `rapida.sh arma` de verdad, con sistema-36 y el `dotnet`
  de verdad, lo hará GitHub (sistema-activa lo probó con una base y un `dotnet` de mentira). No hay sitio en el disco de pruebas para sus
  copias sin huecos (unos 2,5 GB).
- La fase 11 (Spotify, internet, el ratón propio, Ctrl+F3 -> La rocola...): no es de esta tanda.

## 8. Cómo se repite

`sistema/rocola/Pruebas/Qemu/LEEME.md`. En resumen, con root, QEMU, mkntfs/ntfs-3g, ffmpeg, python3 (numpy, PIL), el SDK de .NET 10 y
`salida-qemu/images`: compilar `rocola-xp` (perfil de QEMU), `mundo.sh`, `discoxp.sh`, `arma.sh` y `prueba.py` (una hora y media; las
fases se pueden correr sueltas: `prueba.py video caida`; si la máquina ya está encendida, `arranque` sigue con ella). En
`/root/rocola-trabajo/act/qemu` quedan las capturas (`capturas/`; las de la exploración en `explora/caps/`), lo grabado (`sonido/`), los
resúmenes (`prueba.txt`, `prueba-salida-final*.txt`), lo de las corridas 1 a 4 (`corridas-1-4/`), el mundo inventado (`mundo/`), el disco de
XP falso (`discoxp.img`) y, encima, lo que dejó Rocola OS en él (`xp.qcow2`, comprimido; queda con el `rocola-xp` del perfil del A4, de la
fase `a4`). La memoria USB, `a4.img` y el `rocola-xp` del A4 se borraron para no pasar de 500 MB: se rehacen con `arma.sh` y como dice
`LEEME.md`. Para repetir desde cero: `discoxp.sh` y `prueba.py` (la fase `instala` rehace `xp.qcow2`).
