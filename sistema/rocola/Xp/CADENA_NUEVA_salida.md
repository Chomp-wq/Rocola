# La salida nueva de sonido (cadena de audio nueva, §4 de CADENA_NUEVA.md; D10, D11, D12 y D13)

Del plan aprobado por el dueño el 27-11-2026 («Haz todo lo que me recomiendas en la cadena de sonido»). Hecho por un agente Opus, 27-11-2026.
Hardware nuevo del dueño: rocola (USB) → SMSL PO100 2024 (XMOS XU316, USB Audio Class 2, a S/PDIF óptico) → PROZOR (CS8416 + CS4344,
S/PDIF → RCA) → amplificador. Solo Rocola OS: sin `ROCOLA_OS`, o con `salida_nueva=0` (de fábrica), XP tal cual, bit a bit.

## Lo que hay

### La función `salida_nueva` (RocolaOs/RocolaOs.SalidaNueva.cs)

APAGADA de fábrica. Se enciende desde **Ctrl+F4 → Sonido → «Encender (prueba de 15 s)»**: guarda `salida_nueva=1` y una prueba en
`rocola_salida_prueba.txt` (`antes=salida_nueva:0`, `nueva=salida_nueva:1`: el mismo camino que elegir una salida, RocolaOs.Salida.cs), la rocola
se reabre conservando créditos, cola y lo que suena, y al volver pregunta «¿Se oye bien?» 15 s; sin respuesta (o con «No», o tres arranques sin
contestar) vuelve sola a `salida_nueva=0` y se reabre. «Apagar la salida nueva» hace lo mismo al revés. Sus claves en `rocola_os.txt`:

| clave | de fábrica | qué |
|---|---|---|
| `salida_nueva` | 0 | la función (vale desde el siguiente arranque) |
| `volumen_db` | 0 | el volumen del dueño, de -60 a 0 dB, en vivo (los botones lo cambian y lo guardan) |
| `volumen_mudo` | 0 | el silencio del dueño |
| `salida_nueva_bufer_ms` | 200 | el búfer de la tarjeta (60-500 ms; periodo 20 ms, 10 ms con menos de 120) |
| `salida_nueva_videos` | 1 | el sonido de los videos por la rocola; 0 = mpv directo a la tarjeta, que la rocola le suelta mientras dura el video |
| `salida_nueva_video_lufs` | -18 | a qué sonoridad van los videos medidos, si el dueño la cambia (si no, ver «Los videos») |

### El motor: la tarjeta siempre abierta (RocolaOs.SalidaNuevaMotor.cs, RocolaOs.SalidaNuevaAlsa.cs)

- Un hilo propio (`audio (salida nueva)`, en la prioridad más alta, como la salida de XP) abre la tarjeta UNA vez al arrancar (se espera como
  mucho 2 s, antes de enseñarse la ventana) por **`hw:CARD=id,DEV=n`** (sin plughw), a **44 100 Hz exactos** (sin remuestrear) y en el primer
  formato que acepte de **S32_LE** (24 bits útiles: los 8 de abajo en 0), **S24_3LE** o **S16_LE**, búfer de 200 ms y periodos de 20 ms, sin
  espera (`snd_pcm_wait` con tope: una tarjeta que deja de pedir 3 s cuenta como caída).
- **Al abrir, 0,5 s de ceros** antes de nada. Cuando no hay nada que sonar (entre canciones, al saltar, con la rocola callada) también ceros: el
  silencio exacto sale en **0 de verdad** (sin tramar); lo demás, una sola cuantización a 24 bits (o 16) con **TPDF** de un escalón.
- Cada periodo: la suma de las **fuentes** (cada una por su ganancia, con rampa de 20 ms si cambia) → **el volumen del dueño** (rampa de 10 ms) →
  **la protección en 0 dBFS** (recorte duro; si actúa se cuenta —veces y muestras— y se apunta en la bitácora, como mucho cada 10 s, y en la
  hoja) → **la cuantización**.
- Si la tarjeta se va a media sesión (se desenchufó el PO100): las fuentes lanzan `ErrorAlsa` con ENODEV, que XP trata como «falló el aparato»
  (espera y retoma la misma canción, Codigo/SalidaSonido.cs), y el motor la busca cada segundo (cada 3 s después de 10 intentos). Si al ARRANCAR
  la tarjeta está pero no acepta hw: a 44,1 kHz en ninguno de los tres formatos, se **rinde**: esa sesión suena por el camino de XP (plughw, 16
  bits) y lo apunta (la música nunca se queda muda por esto). Una tarjeta que aún no está al arrancar (o ocupada) no es rendirse: se espera.

### La tarjeta por su nombre (RocolaOs.SalidaNuevaTarjetas.cs; guiones rocola-tarjeta y rocola-sonido)

- La guardada en `/rocola/datos/rocola_tarjeta.txt`: por nombre (`plughw:CARD=id,DEV=n` o `hw:CARD=id,DEV=n`) o la de antes por número
  (`plughw:N,M`, que sigue valiendo: se le busca el id). En automático, **la primera USB** (por `cardN/usbid`, «USB» en su renglón de `cards` o
  en su id) que no sea HDMI y, si no hay, la primera que no sea HDMI (las reglas de siempre).
- Si la guardada no está: AVISO en la bitácora y en la hoja Sonido, se usa la mejor que haya y cada 2 s se mira si volvió (o si apareció la USB,
  en automático) para cambiarse a ella.
- Con la salida nueva encendida, elegir una salida en la hoja (y `rocola-sonido usar N`) la guarda **por nombre**; sin ella, por número, como
  antes (la prueba `salida-audio` y la del lanzador en QEMU siguen viendo `plughw:N,M`).
- `rocola-tarjeta` entiende las dos formas (por nombre: da el `plughw:N,M` del número de ahora; si no está, la automática y lo dice por la salida
  de errores) y, **solo con `salida_nueva=1`** en `rocola_os.txt` de rocola-xp, prefiere la USB en automático (así el lanzador sube al 100 % la
  misma tarjeta que abre el motor, y el camino de XP de respaldo va a ella). Sin `stat` (probado con BusyBox 1.36 de Ubuntu; falta con el del
  sistema, en QEMU).
- El **control de volumen por hardware**, si la tarjeta tiene, se pone **una vez** al 100 % (0 dB) y sin silencio (en su hilo, por amixer: en una
  USB los de volumen de reproducción que no sean de captura ni micrófono; en las demás, los de salida de siempre) y no se toca más. El lanzador
  y VolumenWindows.cs siguen poniendo el 100 %, que con esto es lo correcto.

### Las fuentes: «abrir la tarjeta» = enchufarse al motor (RocolaOs.SalidaNuevaCliente.cs; Nativo/SalidaAlsa.cs)

- `SalidaAlsa.Abre` (Nativo, líneas añadidas y el gancho g444 en `Escritos`) le pregunta primero a `SalidaAlsa.RocolaOsAbre`, que pone la
  función: con la salida nueva da una FUENTE (`IFuenteRocolaOs`) y SalidaAlsa le pasa todo (Libres, Escribe, Sonado, Escritos, Vacia, Pausa,
  Cierra). Así **todo lo que XP abría y cerraba** (cada canción sin relevo, Spotify y la siguiente, la retomada, la sustitución que vuelve a tu
  MP3, la canción muda del diagnóstico, DisposePlaybackNow) es enchufar y desenchufar fuentes: **la tarjeta no se cierra**. Ni una línea de
  Codigo/ de esas cambió.
- La fuente acepta coma flotante de 32 o PCM de 16/24/32, uno, dos o más canales (más de dos: los dos primeros, y lo apunta) y **cualquier
  frecuencia**: lo que no viene a 44,1 kHz lo convierte con el **sinc de XP** (`RemuestreoContinuo`, Codigo/SinPausa.cs), en el hilo del que
  escribe, nunca en el de la pantalla ni en el del motor (la tabla, si no estaba, la hace el primer bloque); si la razón pasa de 4 (8 kHz), en dos
  pasos. Al final de lo que hay, la media ventana del convertidor (~2 ms) se saca con ceros detrás («tramo» nuevo).
- `Sonado` (lo que XP preguntaba a la tarjeta para su reloj y sus bloques) sale de las marcas que deja el motor al tomar y de lo que la tarjeta
  lleva sonado (`snd_pcm_delay` en el hilo del motor; las fuentes no llaman a ALSA). `Vacia` (saltar) tira lo que no sonó, pero los 5 ms que
  iban a sonar se apagan con media curva de coseno (lo que ya estaba en el búfer de la tarjeta, hasta 200 ms, suena: el salto se oye ese poco
  después).

### Los ganchos de Codigo/ (g440-g443 y g445; GANCHOS.tsv)

| gancho | dónde | qué |
|---|---|---|
| g440 | Form1.Sonido.cs:1424 (StartOpenedTrack) | `AbreSalidaClasica(outChain, RocolaOsSalidaNuevaFlotante(FlotanteAdmitido()))`: con la salida nueva, coma flotante (sin el dither de 16 de XP) |
| g441 | Form1.Sonido.cs:338 (AFrecuenciaDeLaTarjeta) | con la salida nueva la cadena **no se convierte** (`RocolaOsSalidaNuevaHz` da 0): ni con `frecuencia_salida=` de Ctrl+F3 ni con el limitador que XP pone detrás |
| g442 | Form1.Sonido.cs:356 (CalientaFrecuenciaSalida) | no se calienta nada aquí: las tablas a 44 100 Hz las calienta el motor (48, 32, 22,05 y 24 kHz, de fondo) |
| g443 | Form1.SinPausa.cs:119 (el relevo) | no hay frecuencia de la tarjeta que esperar: la salida va a la de la canción |
| g445 | Form1.SinPausa.cs:123 (el relevo) | el relevo sin pausa solo encadena una siguiente del **mismo formato** (`RocolaOsSalidaNuevaRelevoConvierte` da no) |

Y fuera de Codigo/: g444 (Nativo/SalidaAlsa.cs, `Escritos`), líneas añadidas en Nativo/SalidaAlsa.cs (la fuente), Nativo/ReproductorVideo.cs
(cuatro enchufes: las opciones de sonido de mpv, el volumen, antes de cargar y al soltar; y `Archivo`) y Plataforma/VolumenTeclas.cs
(`VolumenTeclas.Desvio`). `python3 Pruebas/RocolaOs/ganchos.py`: TODO BIEN.

**La conversión de frecuencia con la salida nueva es la de la salida nueva, sin limitador** (lo que dejó pedido el agente de la cadena, 28-11-2026).
XP convierte de frecuencia en dos sitios, y los dos ponen detrás su `LimitadorPicoReal` (dinámico, techo 0,88 = -1,1 dBTP: mueve el nivel en
tiempo real, justo lo que la cadena nueva no quiere): `AFrecuenciaDeLaTarjeta` (con `frecuencia_salida=`, g441) y el relevo sin pausa entre dos
canciones de distinta frecuencia (`ConvierteSalida` en `PreparaSiguienteSinPausa`). Con la salida nueva ninguno de los dos se usa: la cadena (la de
hoy, o las nuevas A y B, que entregan coma flotante estéreo a la frecuencia de la canción en `CadenaCancion.Salida`, sin limitador) sale a la
frecuencia de la canción y **la fuente del motor la lleva a 44,1 kHz con el mismo sinc** (`RemuestreoContinuo`); detrás solo queda la protección
de 0 dBFS, que cuenta y apunta (si actúa, algo está mal: con el techo de -1 dBTP de la cadena nueva, lo que sube el sinc entre muestras cabe). Con
g445, una siguiente de otra frecuencia ya no se encadena: abre su salida al acabar la que suena, que en el motor es un cambio de fuente (la
tarjeta sigue abierta, ceros en medio, sin chasquido); solo se pierde el «sin pausa» en ese cambio (un disco con pistas de 44,1 y de 48 kHz, casi
nunca). La primera canción (o video, o prueba) de cada frecuencia que no es 44,1 kHz lo dice en la bitácora: «cancion a 48000 Hz (IeeeFloat 32
bits): la convierte la salida nueva a 44100 Hz con el sinc de XP, sin limitador». La hoja lo dice en «Más...». Sin la salida nueva, lo de XP tal
cual (`frecuencia_salida=` y el relevo con `ConvierteSalida`, con su limitador). `SalidaNueva.Va` dice si la salida nueva va.

### El volumen del dueño, digital (D13)

Los botones de atrás (los del asistente de botones y las teclas multimedia VK 0xAD-0xAF, RocolaOs.TeclasVolumen.cs → Plataforma/VolumenTeclas.cs)
con la salida nueva van a `VolumenTeclas.Desvio`: **1 dB por toque** (apretado, unas 10 veces por segundo, como antes), de -60 a 0 dB; subir
quita el silencio, bajar no; la tecla de silencio pone o quita el silencio. En coma flotante, antes de la única cuantización; se guarda en
`rocola_os.txt` (`volumen_db=`, `volumen_mudo=`) y vale al volver a arrancar. amixer no se toca. Sin la salida nueva, el 2 % por amixer de antes.

### Los videos por la rocola (D11; RocolaOs.SalidaNuevaVideo.cs)

La vía elegida, sin paquetes nuevos: mpv no trae PulseAudio en el sistema (rocola-pulso es solo para Soloist, mpv tendría que compilarse con
libpulse: paquete nuevo), `ao=pcm` de mpv no tiene reloj (el video correría), un plugin de ALSA propio o snd-aloop piden la compilación completa.
Lo que sí hay: el **«file» de alsa-lib** (`pcm.file` de alsa.conf, con «null» detrás).

- Al arrancar, la rocola hace una **tubería** `/run/rocola-xp-video.pcm` y la tiene abierta para leer siempre (mpv nunca espera a que alguien la
  abra). A mpv (al abrir libmpv): `audio-device=alsa/file:FILE=/run/rocola-xp-video.pcm,FORMAT=wav`, `audio-format=float`,
  `audio-channels=stereo`, `alsa-buffer-time=20000` y `audio-delay=-0,277` (unos 277 ms: el búfer de la tarjeta, el anillo de la rocola, la
  tubería de 4 KB y los 20 ms de ALSA; el «null» no tiene reloj y mpv cree que lo que escribe ya suena: así la imagen va con la voz).
- La rocola lee cada flujo (su cabecera WAV: alsa-lib dice PCM aunque sea coma flotante; con 32 bits es flotante), SOLO lo que cabe en un anillo
  de ~60 ms (la tubería se llena y mpv espera: el ritmo lo pone la tarjeta), lo convierte con el sinc a 44,1 kHz y lo saca por el mismo flujo; al
  acabar (mpv cierra), lo último se apaga en 5 ms.
- **Su ganancia, fija por video**: g = min(T − L, C − TP), hasta +12 dB, con L y TP medidos **una vez de fondo** con el ebur128 de ffmpeg (en
  cuanto se pide un video y los de la biblioteca poco a poco, cada 10 min desde los 2 min; prioridad mínima;
  `rocola_videos_nivel.txt`: ruta, bytes, fecha, LUFS y dBTP). T: la de `salida_nueva_video_lufs` si el dueño la cambió; si no, `nivel_lufs=` de
  la cadena nueva si está declarada y encendida; si no, **la sonoridad típica de la cadena de hoy ya nivelada** (la mediana de tus canciones
  medidas, la misma del nivelado de Spotify: con la cadena de hoy, que va más fuerte que -18, los videos no quedan bajos); sin nada, -18. C:
  `techo_dbtp=` de la cadena nueva o -1. Se decide al empezar el video y no cambia; **sin medida, la regla de hoy** (lo que XP le ponía a mpv).
  Encima, el **modo noche** como la música (FactorNoche con su rampa) y el fundido del final de XP; mpv se queda en 100 y su filtro de noche
  (`noche_video`) sigue igual.
- Si la tubería no se pudo hacer, o con `salida_nueva_videos=0`: mpv abre la tarjeta como siempre y el motor se la **suelta** mientras dura el
  video (al cargarlo; espera como mucho 600 ms a cerrarla) y la vuelve a abrir al soltarlo (con sus 0,5 s de ceros).

### Las pruebas de sonido por el mismo flujo (para el billar y cualquier otra; RocolaOs.SalidaNuevaPruebaSonido.cs)

Con la salida nueva la tarjeta es del motor y no se abre otra vez. Dos caminos, los dos sin cerrar nada:

1. **Lo que abre su propia salida con `CreaWaveOut()`** (la prueba de bocinas y el ruido por bandas de Ctrl+F3 de XP, SalaLocal.cs y
   AfinarLocal.cs): YA suena por el mismo flujo sin cambiar nada, porque `CreaWaveOut` → `WaveOutPropio` → `SalidaAlsa.Abre` da una fuente del
   motor (16 bits o coma flotante, cualquier frecuencia). Se SUMA a lo que haya (las de XP solo se dejan con la rocola callada), pasa por el
   volumen del dueño y la protección, y su `Stop`/`Dispose` la desenchufa. Es Codigo/ de XP: se queda así.
2. **Una prueba que ocupa el flujo SOLA** (lo que pidió el billar: en lugar de la música, con su ganancia fija):

   ```csharp
   SalidaNuevaPruebaSonido p = SalidaNueva.EmpiezaPrueba(fuente /* ISampleProvider, 1 o 2 canales, cualquier frecuencia */,
                                                         "billar: ruido por bandas", 1f /* ganancia lineal */, true /* con el volumen del dueño */);
   if (p == null) { /* sin la salida nueva (o ya hay otra prueba o la medida): el camino de XP, CreaWaveOut */ }
   p.Sonando;        // false al acabarse la fuente (Read devolvió 0) o al pararla
   p.SegundosOidos;  // lo que ya se oyó (lo dado menos el búfer de la tarjeta): para «la banda que suena»
   p.Para();
   ```

   Mientras dura, lo que ya sonaba sigue su curso CALLADO (su reloj, sus bloques y su fin no se paran: no se da por caída ni se retoma); al
   acabar vuelve a oírse en el mismo periodo. **La música manda**: si mientras tanto algo NUEVO quiere sonar (una canción pedida, un video, la
   prueba de bocinas de Ctrl+F3: cualquier fuente que se enchufa), la prueba **se corta sola** en ese momento, lo apunta («la prueba de sonido se
   corta: empieza a sonar cancion») y `p.Sonando` pasa a false: nada pagado suena callado. Sin la protección contada ni (si se pide) el volumen
   del dueño. Si no viene a 44,1 kHz se convierte con el sinc de XP, construido en el hilo que llama (la tabla, si no estaba, ~20 ms). El `Read`
   de la fuente lo llama el hilo del motor cada 20 ms: rápido y sin esperar. Una a la vez (también la medida del convertidor va por aquí:
   `Exclusiva`; si la corta una canción, la medida lo dice: «Se cortó: empezó a sonar una canción o un video»). Probado (parte «prueba»): 3 s de
   500 Hz a 48 kHz con una canción sonando: la canción antes y después, la prueba en medio a su nivel, la tarjeta sin cerrarse; y una de 20 s que
   se corta al pedir una canción, que se oye.

   **El ruido por bandas de la hojita «Billar» de Ctrl+F4** (RocolaOs.Billar2Bandas.cs, de la cadena nueva B; lo dejó pedido su agente) va ya por
   este camino: con la salida nueva abierta, «Tocar las 8 bandas» es `EmpiezaPrueba(bandas, "billar: las 8 bandas en mono", 1f, true)`: en coma
   flotante (sin los 16 bits de `CreaWaveOut`), ganancia 1 y con el volumen del dueño (el mismo con el que suena la música que la afinación va a
   corregir); «la banda que suena» en grande sale de `p.SegundosOidos` (con el búfer de la tarjeta descontado); cerrar Ctrl+F4 (o cambiar de
   hojita) la para; si empieza una canción, se corta sola y la hojita deja de decir la banda. Sin la salida nueva (o con la tarjeta aún sin
   abrir), su `CreaWaveOut` de antes. Probado (parte «prueba», escena «billar», con Ctrl+F4 de verdad fuera de pantalla).

### Medir el convertidor (D12; RocolaOs.SalidaNuevaMide.cs, guion rocola-mide-dac)

Tonos de 1 kHz a 0, -3, -6, -10 y -20 dBFS (1,6 s cada uno, 0,6 s entre ellos, ~12 s) por la salida, grabados a la vez por la **entrada de
línea de la placa** (la primera tarjeta que no es HDMI ni USB con grabación; se le pone «Line», Capture a 0 dB y sin refuerzo, por amixer). De
cada tono, 1 s del medio: se ajusta una senoidal (amplitud, fase, continua y la frecuencia exacta, ±1,5 Hz, porque los relojes de salida y entrada
no son el mismo) y lo que sobra es el **THD+N**; y su nivel en la entrada. Limpio = la distorsión cerca de la mejor (el doble, o 0,05 %), no más de
1 % y sin comprimirse frente al de -20 (a 1 dB). **Propone el techo**: el nivel más alto limpio menos 1 dB (como mucho -1). Dice si no llega señal
(cable, «Line») o si la entrada misma recorta (baja Capture).

- Desde la rocola: Ctrl+F4 → Sonido → «Más y medir el DAC...» (con la salida nueva abierta): el tono lo da el motor EN EXCLUSIVA (sin el
  volumen del dueño ni la protección; la música sigue su curso en silencio). Con instrucciones en pantalla; «Guardar techo_dbtp=X» escribe la
  clave si la cadena nueva ya la declaró (si no, lo dice).
- Desde la consola de mantenimiento: **`rocola-mide-dac`** (cierra la rocola conservando el estado, como rocola-sonido, prepara la entrada y
  corre `rocola-xp --mide-convertidor [--salida PCM] [--entrada PCM]` fuera de pantalla; todo en un hilo; lo dicho va también a
  `/rocola/datos/rocola_sonido.txt`). Sale con 0 si propone un techo, 3 si no.

### La hoja Sonido de Ctrl+F4 (RocolaOs.SalidaNuevaHoja.cs; RocolaOs.VentanaSalida.cs)

Debajo de «LA QUE SE USA» (sus botones no se movieron; el texto de ayuda se acortó), el bloque **SALIDA NUEVA**: apagada (qué haría), o la tarjeta
(nombre, si es USB, hw:...), lo abierto de verdad (formato, bits y frecuencia; búfer), el control por hardware, el volumen del dueño en dB y el
silencio, la protección (si actuó), los videos y los AVISOS (la guardada que no está; se pinta en ámbar). «Encender/Apagar» y «Más y medir el
DAC...»: una hoja con todo en grande (aperturas, caídas, xruns, fuentes, el pico más alto antes de la protección, el último sonido de
video), el volumen a mano (-1/+1 dB, silencio) y la medida.

## Las pruebas (fuera de pantalla; `Pruebas/RocolaOs/salida-nueva/prueba.sh PROGRAMA [partes]`)

Con la tarjeta de prueba con reloj (Pruebas/Windows/alsa/reloj.c: lo que de verdad sonó y cada apertura), un convertidor de prueba en lazo
(`lazo.c`: recorta desde `LAZO_RECORTA_DB` y se graba de vuelta por una «entrada»), un /proc/asound del A4 con el PO100 por USB, un amixer falso y
música inventada (tonos de 440 Hz a 44,1 kHz, 1 kHz a 48 kHz y 2 kHz a 32 kHz en MP3; un video con 3 kHz a 48 kHz en AAC). `analiza.py` lee lo
sonado. (Solo para pruebas: `ROCOLA_SALIDA_NUEVA_PCM`, `ROCOLA_SALIDA_NUEVA_TARJETA`, `ROCOLA_SALIDA_NUEVA_ENTRADA`,
`ROCOLA_SALIDA_NUEVA_GANANCIA_DB`, y la función doble `salida_nueva-prueba`, RocolaOs.SalidaNuevaPrueba.cs.)

Las partes (de fábrica todas menos `bitabit`, que pide `BASE=` con el programa de antes compilado):

| parte | qué comprueba | resultado (28-11-2026, juntada con la A y la B) |
|---|---|---|
| guiones | rocola-tarjeta y rocola-sonido con BusyBox (1.36 de Ubuntu, sus applets delante en el PATH): lo de antes por número sigue; por nombre con el número de ahora (también renumerado); la guardada que no está → la automática y lo dice; la USB primero SOLO con `salida_nueva=1`; `usar N` por nombre solo con ella; sin `stat` | 15/15 |
| elige | la elección del motor (`rocola-xp --prueba-salida-nueva elige`): las mismas reglas, con el aviso | 6/6 |
| convertidor | `rocola-xp --mide-convertidor` con el lazo: recortando desde -10 dBFS, 0/-3/-6 RECORTAN (32/26/17 % de THD+N), -10 y -20 limpios y propone -11; sin recortar, todo limpio y -1; sin señal y con la entrada que recorta, lo dice | 6/6 |
| flujo | la rocola entera (fuera de pantalla) con tres canciones (44,1/48/32 kHz), un salto a media canción y un video: la tarjeta se abre UNA vez (S32_LE, 44 100 Hz) y no se cierra hasta salir; 0,5 s de ceros de verdad; 24 bits; cada tono a su frecuencia (convertidos dentro); el corte del salto; el control de la tarjeta al 100 % una vez (solo PCM, no Mic); el video por la rocola (48 kHz, medido con ffmpeg a -38 LUFS, ganancia fija +12 dB con el tope: su tono sonó a -32,1 dBFS; sin el modo noche, que a la 1 de la mañana lo bajaba 5 dB); con `frecuencia_salida=44100` en la configuración de XP, la de 48 y la de 32 kHz llegan a la salida nueva a la suya (la cadena no las convirtió ni puso su limitador) y las convierte ella | 18/18 |
| cadena | la cadena nueva A (`cadena_nueva`) por la salida nueva: tres canciones de 44,1, 48 y 32 kHz, cada una a -18 LUFS por la A (ganancias +16,0, +15,3 y +12,9 dB), coma flotante a la salida, que convierte la de 48 y la de 32 kHz sin limitador; cada tono a su frecuencia; una sola apertura; la protección sin actuar | 8/8 |
| mide | la medida desde la rocola (el lazo como salida nueva, una canción sonando y el volumen del dueño en -6 dB): la medida va tal cual y propone -11; la tarjeta sigue abierta | 4/4 |
| prueba | `SalidaNueva.EmpiezaPrueba` (3 s de 500 Hz a 48 kHz) con una canción sonando: la canción antes y después, la prueba en medio a su nivel, lo oído a la mitad y al acabar, una sola apertura; «la música manda»: una de 20 s se corta sola al pedir una canción (a los 3,6 s), lo apunta, y la canción se oye; el ruido por bandas de la hojita «Billar» de Ctrl+F4 (`ROCOLA_OS=1`, `billar_nuevo=1`, el clic en «Tocar las 8 bandas»): por la salida nueva sola, 10,5 s de ruido a -20 dB eficaces hasta Esc, «Suena: 63 Hz 1 de 8» en la captura, una apertura | 14/14 |
| rendida | una tarjeta que solo da 48 kHz: se rinde, lo apunta y la canción suena por el camino de XP; una que no está: no se rinde, la canción espera al aparato como en XP (ni dañada ni devuelta) | 5/5 |
| volumen | las teclas multimedia: seis toques abajo y dos arriba desde -3 → `volumen_db=-7` guardado; lo que sonó bajó 6,00 dB y subió 2,00 (mesetas del nivel); el silencio puesto y quitado; amixer sin tocar | 5/5 |
| proteccion | con 40 dB de más (solo pruebas): actúa, se cuenta (veces y muestras) y se apunta; nada pasó de 0 dBFS | 3/3 |
| hoja | Ctrl+F4 → Sonido con `ROCOLA_OS=1` (capturas): «Encender» guarda `salida_nueva=1` y la prueba y se reabre conservando el estado; al volver abre, pregunta y con «Sí» (Tab, Intro) queda; el bloque cambia; «Más y medir el DAC...» abre su hoja; la prueba sin contestar vuelve sola a `salida_nueva=0` y se reabre | 9/9 |
| sin | sin `ROCOLA_OS` y con `salida_nueva=0` (`ROCOLA_OS=salida_nueva-prueba`): ni bitácora ni archivos de la salida nueva; por plughw a 16 bits; la de 48 kHz por el relevo convertida a la de la que sonaba (lo de XP); la salida se cierra al acabar la cola | 6/6 |
| bitabit | con `salida_nueva=0`, la misma escena (tres canciones con relevos y conversión) con el programa de antes (fee0890) y con el de ahora, sobre la misma foto de la biblioteca ya medida: lo que sonó, igual a 2 escalones de 16 bits como mucho en 1 190 696 cuadros (los dos tramados TPDF de XP, con su semilla de la hora), y el mismo registro de la tarjeta; el 28-11-2026, otra vez con la A y la B juntadas (y apagadas) | 3/3 |

28-11-2026, juntada la rama de la cadena nueva (A y B, 48fcdbf/af2d1be/27c8abd) y hecho lo que pidió su agente (la conversión sin el limitador de
XP, g441-g445; el ruido por bandas del billar por el flujo; «la música manda»): JIT todas las partes de fábrica, 99 de 99 (en dos tandas: la
primera se cortó tras `mide` por editar el guion mientras corría; todo lo de antes de eso, bien) y `bitabit` contra fee0890 3 de 3; NativeAOT
(`dotnet publish -r linux-x64`, 0 avisos IL/AOT) todas las partes de fábrica, 99 de 99. `salida-audio` 29/29 (con la pestaña Sonido en su sitio
nuevo, (606,54): la pestaña Cadena corrió las de la izquierda), `billar-nuevo` parte `hoja` (NativeAOT, sin la salida nueva: su `CreaWaveOut` de
antes) 10/10; `ganchos.py` TODO BIEN; compila con los 13 avisos de siempre.

27-11-2026: JIT: la corrida entera 77 de 78 (`bitabit` falló porque el repaso de XP midió una canción antes en una corrida que en la otra: se arregló la
prueba con la foto de la biblioteca ya medida) y después `rendida` + `bitabit` 8 de 8. NativeAOT (`dotnet publish -p:PublishAot=true`, 0 avisos
IL/AOT): todas las partes de fábrica, 81 de 81. Además, sin cambios: `Pruebas/RocolaOs/salida-audio/prueba.sh` (la hoja Sonido de antes: sus botones en su sitio)
29/29 y `Pruebas/RocolaOs/volumen/prueba.sh` (los botones con amixer, sin la salida nueva) 11/11; `ganchos.py` TODO BIEN; compila con los 13
avisos de siempre. La batería del nivel 0 (escenas contra la referencia congelada) no se pudo correr en esta máquina (no está
/root/rocola-trabajo/referencia-xp ni sus trazas); en su lugar, `bitabit` contra el programa de antes.

## Lo que no se pudo comprobar

- NADA en el A4, ni con el SMSL PO100 ni con el PROZOR de verdad, ni oído: que el PO100 acepte hw: a 44,1 kHz en S32_LE (UAC2: debería; si solo
  diera S24_3LE o S16, se usa ese), su nombre en ALSA (el id puede no ser «PO100»: se usa el que diga /proc/asound), si tiene control de volumen
  por hardware, que el CS8416 enganche con los 0,5 s de ceros y no suene un golpe, la latencia real de la imagen y la voz de los videos (el
  `audio-delay` está calculado, no medido), el mpv 0.40 del sistema (aquí 0.37) con la tubería, y la medida con la entrada de línea de la placa
  del A4 (qué controles tiene la ALC892 para «Line» y si su entrada recorta con ~1 Vrms del PROZOR).
- Los guiones con el BusyBox de Buildroot (aquí el de Ubuntu 1.36): falta en QEMU.
- Con la cadena nueva: la escena `cadena` es la A; la B (modo billar) entrega lo mismo a la salida (coma flotante estéreo a la frecuencia de la
  canción, sin limitador) pero no se corrió por la salida nueva en escena. El «sin pausa» que se pierde entre dos canciones de distinta frecuencia
  (g445) se ve en la escena como unos 0,2-0,3 s entre tonos (con el fundido de XP de canciones sueltas); no se midió con un disco continuo de
  frecuencias mezcladas.
- Un XRUN de verdad (la tarjeta de prueba no se queda sin nada en estas pruebas) y desenchufar el USB con música sonando.
