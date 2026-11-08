# Port nativo de XP: lo demás de Windows (el video del WMP y las llamadas sueltas)

Pista «windows» de la primera tanda del port nativo (8-11-2026; decisiones del dueño del 8-11-2026 en AGENTS.md: «que tradujeras todo
el código para que trabajara nativamente en Linux», «ningún layer de traducción»). Rama `port-windows`, desde `34cb741`. Aquí va lo que
se tradujo, cómo se comprobó (con los números), las herramientas y lo que queda. Nada de esto está probado en el A4 (no se puede desde
aquí): todo es en la máquina de pruebas, fuera de pantalla (llvmpipe) y con ALSA de archivo o nula.

La referencia es la copia congelada `/root/rocola-trabajo/referencia-xp` (commit `aef2211`, la versión con capas), compilada en
`sistema/rocola/Xp/bin/Release/net10.0`. Cada corrida usa una copia nueva de los datos de prueba (`musica/`, `rocola_*.txt`,
`rocola_teclas.txt`, `rocola_miniaturas/`), siempre en la misma carpeta (`/root/rocola-trabajo/port/windows/run`) para que las rutas
guardadas sean iguales en las dos.

## W1. El video (el reproductor de Windows Media)

**Antes.** `Form1.Video.cs` creaba `VideoHost : AxHost` (el control ActiveX del WMP, CLSID `6bf52a52-...`) y le hablaba por reflexión
(`Get`, `Set` y `Call` con `Type.InvokeMember`); `Form1.Biblioteca.cs` medía la duración de los videos con un reproductor de Windows
suelto (`Type.GetTypeFromProgID("WMPlayer.OCX.7")`, `Activator.CreateInstance`, `newMedia(ruta).duration`, `Marshal.ReleaseComObject`) en
un hilo STA. La traducción anterior dejaba eso igual y lo contestaba una imitación del modelo de objetos del WMP
(`Plataforma/VideoMpv.cs`: `ReproductorWmp`, `AjustesWmp`, `ControlesWmp`, `MedioWmp`, y `AxHost.Fabrica` en `Ventanas.Form.cs`).

**Ahora.** El código de XP llama directo a dos piezas nativas con nombre propio (carpeta `Nativo/`):

- `ReproductorVideo` (`Nativo/ReproductorVideo.cs`, sobre `Nativo/Mpv.cs`: libmpv): solo lo que XP usa. `Inicia` (lo que era crear el
  control al pedir su `Handle`), `Disponible` (`GetOcxObj() != null`), `Carga(ruta)` (`URL = ruta` con `autoStart`), `Toca`
  (`controls.play`), `Para` (`controls.stop`), `QuitaArchivo` (`URL = ""`), `TieneArchivo` (`currentMedia != null`), `Estado`
  (`playState`, con los mismos números que compara XP: 1, 2, 3, 8, 9), `Posicion` (`controls.currentPosition`), `Duracion`
  (`currentMedia.duration`) y `Volumen` (`settings.volume`, 0 a 100 de amplitud: a mpv se le pone 100·∛(v/100)). Y su capa en la
  pantalla, lo que el control hacía como ventana: `EnPantalla` (`Controls.Add`), `FueraDePantalla` (`Controls.Remove`), `PonSitio`
  (`SetBounds`), `Sitio` (`Left`...), `Visible` y `AlFrente` (`BringToFront`, también el de `KeepVideoOnTop`). Conserva los arreglos del
  7-11-2026 de `VideoMpv.cs`: el estado de fábrica de OpenGL antes de cada llamada a mpv (`EstadoDeFabrica`), sin girar (`FLIP_Y` 0) y
  el escalado bilineal de una pasada (sin escaladores con tabla: regla del 7-11-2026).
- `MedidorVideo` (`Nativo/MedidorVideo.cs`): la duración de un video sin tocarlo, lo que hacía el reproductor suelto. XP pide la del
  contenedor (`newMedia(ruta).duration`) y la mira cada 100 ms hasta 3 s. Aquí `PideDuracion` lanza `ffmpeg -hide_banner -nostdin -i
  RUTA` (Rocola OS trae el programa ffmpeg; **ffprobe no está en el sistema**: `configs/rocola_a4_defconfig` solo tiene
  `BR2_PACKAGE_FFMPEG_FFMPEG`, así que el medidor de la referencia, que usaba ffprobe, en el A4 no habría medido ningún video), y
  `Duracion` da su «Duration: HH:MM:SS.cc» (la del contenedor según libavformat, redondeada a la centésima, que es lo que guarda XP con
  `"0.##"`) en cuanto llega; XP sigue con su espera de 3 s y `QuitaArchivo` corta el proceso. No se usa libmpv para esto porque su
  `duration` no es la del contenedor: es la de la pista más larga (30,02 s en un video de 30,00 s; medido), y entonces
  `rocola_duracion.txt` salía distinto de la referencia.

Lo que va entre el reproductor y la pantalla es una interfaz (`IPantallaDelVideo`, `ILugarDelVideo`): mientras sigan las ventanas que
imitan a WinForms (las traduce otra pista), `Plataforma/VideoEnVentanas.cs` pone la capa del video como un hueco en la lista de controles
de la ventana de la rocola, para que su orden frente al panel de la tarjeta, `lblAttract`, `lblConfig` y `lblGpu` sea exactamente el que
deciden los `BringToFront` de XP; el anfitrión pinta ahí la textura de libmpv. Cuando la pantalla sea nativa, su lista de lo que va encima
del panel toma ese lugar y `VideoEnVentanas.cs` se borra. El código de XP no ve nada de esto.

Se fueron: `VideoHost : AxHost`, `Get`/`Set`/`Call` (`InvokeMember`), `Marshal.ReleaseComObject`, `Activator.CreateInstance`,
`LlamadasWindows.TipoDeProgId`, el `SetApartmentState(STA)` del hilo de las duraciones, el `Application.DoEvents` de ese hilo,
`Plataforma/VideoMpv.cs` entero (`ReproductorWmp` y compañía), `AxHost` de `Ventanas.Form.cs` y los avisos IL2072/IL2075 que eso traía en
NativeAOT (`Aot.cs`).

**Cómo se comprobó (8-11-2026, JIT).**

- La prueba aislada del video (`rocola-xp --prueba-video`, libmpv pintando en su textura de 498x606 y el punto del centro cada 0,25 s):
  `Pruebas/Windows/aislada.sh prog-mio 20 3` → **20 de 20 bien** (ninguna lectura negra, todas amarillas como el video de prueba).
- La rocola entera con un video de 30 s, contra la referencia (`Pruebas/Windows/video.sh prog-ref|prog-mio NOMBRE`: REGRESAR+DERECHA dos veces, PONER,
  MONEDA, PONER; el video suena en ventana, a los 8 s sin botones pasa a pantalla completa con la franja de la atracción encima y al
  acabar vuelve la pared; capturas a los 16, 24, 44 y 50 s; el sonido de mpv a un WAV por ALSA `file:`):
  - el sonido del video (`video.wav`, 10 584 108 bytes, 30 s a 44,1 kHz estéreo de 32 bits): **igual byte a byte** que el de la
    referencia (con la curva de volumen de XP: el arranque, el nivel por el disco y el fundido del final);
  - los 12 `rocola_*.txt` del final, quitando las horas de los archivos copiados (`Pruebas/Windows/compara.py`): **iguales**, incluido
    `rocola_duracion.txt` (`30|.../Un video largo.mp4` y `8|.../Video de prueba.mp4`, como la referencia; con la duración de libmpv salía
    `30.02` y `8.02`);
  - las capas que compone la pantalla, una vez por segundo (`ROCOLA_TRAZA_CAPAS=1`): la misma sucesión (12 s la pared; 8 s el video en
    ventana en `512,56 498x606` encima del panel; 21 s el video en pantalla completa en `0,46 1024x722` debajo de la franja; la pared otra
    vez). La única diferencia es un segundo de transición que la referencia muestreó con la pared aún sin poner (entre `aviso 7` -el
    video acabó- y la pared hay ~0,3 s; la toma de cada segundo cae o no ahí);
  - las capturas: el video se ve en el mismo sitio y con el mismo cuadro de prueba, y la franja «¡ PRESIONA UN BOTON PARA USAR LA ROCOLA !»
    encima a pantalla completa; los píxeles distintos son los del cielo animado y del cuadro del video, que también cambian entre dos
    corridas de la referencia (26 000 a 540 000 píxeles entre la referencia y esta; 107 000 a 450 000 entre dos de la referencia).

## W2. El sonido (winmm, el ACM y el AudioFileReader)

**Antes.** `SalidaPropia.cs` (la salida con hilo propio de XP, `WaveOutPropio`) hablaba con Windows por la interfaz `ISistemaWaveOut`
(con forma de winmm: `Prepara`/`Escribe`/`Desprepara` con WAVEHDR en memoria no administrada, la bandera WHDR_DONE, los MMSYSERR) y la
traducción la contestaba con una imitación de winmm sobre ALSA (`Plataforma/SonidoAlsa.cs`: `AlsaWaveOut` con su propio hilo y sus
WAVEHDR, `MmResult`, `MmException` y un `WaveOut` con la forma del de NAudio para «la de siempre»). Los MP3 se descomprimían con
`Mp3FileReader.CreateAcmFrameDecompressor` (el ACM de Windows, contestado con libmpg123) y lo demás con un `AudioFileReader` que imitaba
al de NAudio 1.9 pero abría con ffmpeg todo lo que en XP abre Media Foundation (que Windows XP no tiene).

**Ahora.**

- `Nativo/SalidaAlsa.cs`: la tarjeta por libasound, sin disfraz. `Abre` (snd_pcm_open de la tarjeta de la rocola -ROCOLA_ALSA, o la
  primera que no sea HDMI-, el formato tal cual, la frecuencia exacta con plughw convirtiendo, un periodo de un bloque de XP -50 ms- y un
  búfer de todos los bloques -750 a 1000 ms-, umbral de arranque 1 como `waveOutWrite`), `Libres`/`Escribe` (snd_pcm_avail, snd_pcm_writei;
  un XRUN se recupera solo, como winmm, que se calla un momento), `Sonado` (lo escrito menos snd_pcm_delay), `Vacia` (`waveOutReset`:
  snd_pcm_drop, la cuenta a 0), `Pausa`, `Cierra`. Los errores son `ErrorAlsa` (el -errno de ALSA y la función; `Formato` cuando la tarjeta
  no acepta el formato).
- `WaveOutPropio` (Codigo/SalidaPropia.cs) la llama directo: los mismos bloques de 50 ms, el mismo orden, el mismo hilo, la misma espera
  de 100 ms como mucho, el mismo final y la misma parada. Lo que en XP contestaba Windows sobre los bloques se calcula con lo sonado: un
  bloque «ya sonó» (WHDR_DONE) cuando lo sonado pasa de donde acaba, y el hilo espera justo hasta que acabe el más viejo (en XP, Windows
  señala el evento en ese momento). El reloj (`GetPosition`) es lo sonado en bytes y de 32 bits, como el MMTIME de `waveOutGetPosition`
  (Form1.AmpliaRelojSalida cuenta sus vueltas).
- Las decisiones de XP con cada MMSYSERR (`SalidaSonido.cs`), con los errores de ALSA que dicen lo mismo:
  | XP (winmm) | Linux (ALSA) | XP decide |
  |---|---|---|
  | BadDeviceId (desenchufado) | ENOENT (no hay tarjeta con ese nombre), ENXIO, ENODEV (ya no está) | el aparato: se espera y se retoma la misma canción |
  | AlreadyAllocated | EBUSY (la tiene otro programa; antes se espera 1,5 s por si es el video que aún la suelta: en Windows los dos suenan a la vez por el mezclador) | el aparato |
  | InvalidHandle | EBADFD, ENODEV con la salida abierta, EIO (dejó de contestar) | el aparato |
  | NoDriver | no está libasound (ErrorAlsa.SinAlsa) | el aparato |
  | NotEnabled, NoDriverCallback | no pueden darse en Linux (un controlador quitado es una tarjeta que no está; ALSA no avisa por el controlador) | — |
  | WaveBadFormat | EINVAL al poner formato, canales, frecuencia o acceso (`ErrorAlsa.Formato`) | el formato: en Vista, de coma flotante a 16 bits |
  | otro | otro | de la canción (o, si falló la salida propia por sí misma, la de siempre) |
  «La de siempre» (en XP, el WaveOut de NAudio, con winmm y aviso por ventana) no existe en Linux: es otra salida propia nueva sobre la misma
  ALSA, marcada `LaDeSiempre` para que Form1 decida con ella exactamente lo mismo que con el WaveOut (`EsLaPropia`: `w is WaveOutPropio` de
  XP; `salida is WaveOut` en el reloj de 32 bits). `FlotanteAdmitido` es lo que decidía en el A4 con Windows XP 5.1 (no: 16 bits con
  dither) y no la versión del núcleo; con eso `IdSalidaPorDefecto` sigue sin preguntar, como en XP.
- `Nativo/DescompresorMp3.cs`: lo que en XP era el ACM (`DescompresorMp3.Crea`, llamado directo desde Form1.Sonido.cs y
  CancionEnMemoria.cs): **libmpg123** (la biblioteca nativa de MP3; Rocola OS la trae) y, sin ella, NLayer en 16 bits. Se queda libmpg123
  delante aunque el encargo decía NLayer, por tres razones medidas: es lo que usa la referencia (así el sonido sale igual muestra a
  muestra), es lo que se eligió para Rocola OS el 2-11-2026 («NLayer se comía un cuadro en algunos y es ~8 veces más lento») y, con estas
  canciones de prueba, NLayer descomprime distinto algunos cuadros (error eficaz de 265 frente a una señal de 1 099 en la primera canción,
  concentrado en unos pocos cuadros). El respaldo con NLayer de la referencia **no sonaba nunca** (su adaptador de cuadros leía desde la
  cabecera y NLayer se salía del arreglo en el primer cuadro: con `ROCOLA_SIN_MPG123=1`, las tres canciones fallaban con
  IndexOutOfRangeException en la referencia); aquí usa el adaptador de XP (`CuadroNLayer` de FlotanteNLayer.cs) y suenan las tres.
  `Mp3FileReader.CreateAcmFrameDecompressor` sale del lector de NAudio 1.9 traducido (`Mp3Xp.cs`, que se queda: es una biblioteca, no
  Windows).
- `Nativo/LectorAudio.cs`: lo que hace el `AudioFileReader` de NAudio 1.9 para lo que no es un MP3 ni un WAV de siempre (Form1.Sonido.cs:
  `FileAudioSource` y `LectorMedida`), con los lectores de NAudio.Core que no son de Windows (WaveFileReader, AiffFileReader,
  SampleChannel) y sus mismas cuentas de largo y posición. El WAV comprimido, que en XP abre el ACM, lo descomprime ffmpeg a PCM de 16 bits
  (lo que sugiere el codec) solo en los formatos con codec ACM en un XP de fábrica (Microsoft e IMA ADPCM, ley A y mu, GSM 6.10,
  MPEG Layer-3, TrueSpeech, G.723.1, WMA 1 y 2), con el largo que calcula el codec (bloques enteros por muestras de cada bloque en los ADPCM
  y el GSM); los demás no abren. Lo que en Windows abre Media Foundation **no abre**, como en el A4 con XP (`FormatoSinLector`: XP lo toma
  como su DllNotFoundException de mfplat.dll, «no se pudo abrir», sin darla por dañada); la referencia lo abría con ffmpeg.

Se fueron: `Plataforma/SonidoAlsa.cs` (AlsaWaveOut, MmResult, MmException, WaveOut), `Plataforma/AudioXp.cs` (AudioFileReader y su
FfmpegLector), `ISistemaWaveOut`, `CabeceraWave` y las banderas WHDR de SalidaPropia.cs, `CreateAcmFrameDecompressor` y el aviso IL3050 de
la WAVEHDR (`Aot.cs`).

**Cómo se comprobó (8-11-2026, JIT).**

- Una tarjeta de prueba con reloj (`Pruebas/Windows/alsa/reloj.c`, un plugin de ALSA: suena a su ritmo como una tarjeta de verdad, guarda
  en crudo lo que de verdad sonó -lo que se tira con drop no- y apunta cada arranque, parada y XRUN). Con ALSA `null` o `file` todo
  «suena» al instante (el puntero no avanza con el reloj) y no se ve nada de los tiempos.
- `rocola-xp --prueba-salida 3` (la salida de XP con un tono de 3 s): abre en 7-12 ms con 20 bloques, la posición sigue al reloj con
  ~18 ms de diferencia (lo que tarda en empezar), lo leído va ~0,95 s por delante (el colchón de XP), el aviso de parada llega a los 3,022 s
  sin error y la posición final es 529 200 bytes (3,000 s). Con `--prueba-salida 3 1.5` (Stop a media): el aviso llega en 1 ms, lo que no
  sonó se tira (sonaron 1,51 s) y la posición vuelve a 0, como `waveOutReset`. Con una tarjeta que no existe: `ErrorAlsa` ENOENT y Form1 lo
  toma como fallo del aparato.
- La rocola entera con tres canciones del primer disco (`Pruebas/Windows/sonido.sh` y `variantes.sh`: MONEDA mantenida, PONER, PONER, ABAJO+PONER dos veces;
  con el dither de 16 bits con semilla fija en las dos -`Pruebas/Windows/prueba-fuente.sh` compila copias de las dos fuentes con ese único
  cambio, solo para comparar-, la música copiada con sus fechas y las tablas ya medidas, `datos-estables`):
  - la referencia no sale igual de una corrida a otra: en 3 corridas dio 3 sonidos distintos (siempre iguales en la primera canción hasta
    los 2,70 s; las diferencias empiezan en un relevo entre canciones, de unas 47 000 muestras: decisiones de XP que dependen de cuándo
    acaba cada hilo);
  - esta rama, en 3 corridas, dio 2 de esos mismos 3, **iguales byte a byte** a corridas de la referencia (1 694 140 bytes). Antes, con
    las tablas sin medir, 5 corridas de la referencia y 4 de esta: 6 de las 9 con la misma huella (3 de cada una), las otras tres con una
    huella cada una (las diferencias, otra vez desde un relevo);
  - la firma de la biblioteca (`sig=7:7:bcb5f46874067ab0`) y los 12 `rocola_*.txt`, iguales en todas.
- `rocola-xp --prueba-lector` con WAV de prueba hechos con ffmpeg: IMA ADPCM 2,0063 s (88 479 cuadros: 87 bloques de 1 017, como el
  codec), MS ADPCM, ley mu y ley A, GSM 6.10 (2,0 s) y MP3 dentro de un WAV abren y su pico coincide con el de ffmpeg; G.726 (sin codec en
  XP), M4A y FLAC (Media Foundation) y un WAV de 24 bits extensible (en XP lo abre antes WavDirecto, nunca llega aquí) no abren, con «no se
  pudo abrir» y sin darlos por dañados; AIFF abre.
- Con `ROCOLA_SIN_MPG123=1`, esta rama toca las tres canciones (847 070 muestras, como con libmpg123) y la referencia ninguna.

## W3. Lo demás de Windows (las llamadas sueltas)

**Antes.** En cada archivo de XP la traducción dejaba la llamada de Windows con su nombre y su estructura, y debajo una función falsa
que contestaba con Linux: `GlobalMemoryStatusEx(ref MemStatusEx)` llenaba la MEMORYSTATUSEX con `/proc/meminfo`, `FlushFileBuffers(IntPtr)`
hacía fsync del descriptor, `timeBeginPeriod`/`timeEndPeriod` devolvían 0, `EnumDisplayDevices(..., ref DispositivoPantalla, ...)` llenaba
la DISPLAY_DEVICE; lo de debajo estaba en `Plataforma/LlamadasWindows.cs` (`Memoria`, `Direcciones`, `AlDisco`, `Adaptador`, `TarjetaDrm`,
`PrioridadDelProceso`). Lo demás quedaba como en XP y en Linux fallaba o no hacía nada: el registro (`Microsoft.Win32.Registry` en Form1 y
DiagnosticoCompleto: en Linux lanza PlatformNotSupportedException y los MHz salían 0; advapi32 en Form1.Ajustes: DllNotFoundException),
`Thread.Priority` (en .NET de Linux se guarda y no hace nada: todos los hilos iban en la del programa), las declaraciones de kernel32 del
diagnóstico (nunca se llamaban: XP ya toma su camino de Unix) y `ListadoWin32` (FindFirstFileW, solo con CLR 2 en Windows).

**Ahora.** El código de XP llama directo a Linux, con dos piezas propias en `Nativo/` (`Equipo.cs` y `PrioridadHilo.cs`); en el sitio de
cada declaración de Windows queda un comentario `EN LINUX` que dice qué era.

| XP (Windows) | Ahora (Linux) | Dónde |
|---|---|---|
| `GlobalMemoryStatusEx`: ullTotalPhys, ullAvailPhys, dwMemoryLoad | `Equipo.Memoria`: MemTotal y MemAvailable de `/proc/meminfo`, el uso en % | Form1 (tope de portadas, casillas horneadas, nivel del audio), CancionEnMemoria, MedidasBitacora, DiagnosticoCompleto |
| ullTotalVirtual, ullAvailVirtual (los 2 GB de un programa de 32 bits) | `Equipo.Direcciones`: 128 TB menos VmSize de `/proc/self/status` | MedidasBitacora (`direcciones_libres`) |
| `FlushFileBuffers` | `FileStream.Flush(true)` (fsync); `RespaldoEnDisco`: false si fsync falla (IOException), como XP con la llamada que falla | Form1.Datos, DiarioTablas, Form1.Ia (11 llamadas) |
| registro: `ProcessorNameString` y `~MHz` | `Equipo.Procesador` («model name» de `/proc/cpuinfo`, el mismo texto de CPUID) y `Equipo.ProcesadorMhz` (cpufreq `cpuinfo_max_freq`; sin cpufreq, «cpu MHz») | Form1 (`ProbeAudioTier`), DiagnosticoCompleto |
| registro: `MMDevices\Audio\Render` (advapi32, la clave de Vista) | nada: lista vacía, la misma que da XP en el A4 (esa clave no existe en Windows XP) | Form1.Ajustes |
| `EnumDisplayDevices` (el principal, su DeviceString) | `Equipo.Adaptador`: GL_RENDERER y el controlador y el número PCI de `/sys/class/drm` | DiagnosticoCompleto |
| `GetSystemTimes`, `OpenThread`, `GetThreadTimes`, `CloseHandle`, `GetCurrentThreadId` | `/proc/stat`, `/proc/self/task/N/stat` y gettid: el camino de Unix que XP ya tenía (`EsUnix` siempre) | DiagnosticoCompleto |
| `FindFirstFileW`/`FindNextFileW`/`FindClose` (`ListadoWin32`) | nada: con .NET 10 XP recorre con `DirectoryInfo.GetFiles`, que trae los datos de cada entrada en el mismo recorrido (readdir); `fuenteListado` = null. Tampoco en Windows se usaría con este .NET | RecorridoRapido, Form1.Biblioteca |
| `Environment.OSVersion` en el texto del diagnóstico | `Equipo.SistemaOperativo`: «Linux» y el núcleo (`/proc/sys/kernel/osrelease`), y el nombre del sistema (`PRETTY_NAME` de `/etc/os-release`) | Form1.DiagnosticoCompleto |
| `Environment.OSVersion` en una decisión | lo que decidía en el A4 (Windows XP, 5.1): coma flotante a la tarjeta no (W2), videos sin medir (ya estaba), `EsUnix` sí | SalidaSonido, Form1.Video, DiagnosticoCompleto |
| `timeBeginPeriod(1)` / `timeEndPeriod(1)` | nada: Linux ya despierta al milisegundo (`Sleep(1)` = 1,2 ms medido) | Form1 |
| `SetPriorityClass` (`Process.PriorityClass = AboveNormal`) | `PrioridadHilo.DelProceso` | Program |
| `SetThreadPriority` (`Thread.Priority`, 20 sitios en 9 archivos) | `PrioridadHilo.Con`/`AlArrancar` (al arrancar el hilo) y `Pon` (el hilo que llama): setpriority del hilo (gettid) | AperturaAudio, BombaSonido, CaratulasAdaptativas, Form1, Form1.Biblioteca, Form1.Datos, Form1.Portadas, SalidaPropia, SinPausa |

La correspondencia de las prioridades (`Nativo/PrioridadHilo.cs`, a la vista). La clase del programa, como la traduce .NET (tiempo real
-19, alta -11, por encima de lo normal -6, normal 0, por debajo de lo normal 10, inactiva 19); en Windows es de todo el proceso, así que
todos los hilos de `/proc/self/task` se mueven lo mismo que el principal (los de .NET y Mesa que ya existían). El nivel de cada hilo, sobre la
del programa: la más alta -10, por encima -5, normal 0, por debajo +5, la mínima +10. Con la rocola (por encima de lo normal): la salida de
sonido -16, la música -11, la pantalla -6, las portadas y lo que va por debajo -1, el repaso y lo mínimo +4. El planificador de Windows
atiende siempre primero la prioridad más alta; el de Linux (CFS) reparte por pesos, 1,25 veces por punto: 5 puntos son unas 3 veces el
procesador por nivel, que acerca ese «primero el de arriba» sin dejar sin nada al de abajo. Lo que no puede ser igual: en Linux un hilo
nuevo hereda el nice del que lo crea (en Windows nace en la normal). Por eso todos los hilos que crea el código de XP se ponen la suya al
arrancar (la que XP les puso antes de `Start` o, si no les puso ninguna -FlotanteNLayer, MedidasBitacora-, la normal); los del grupo de
hilos de .NET y los de las bibliotecas heredan la del que los pide.

Se fueron: las estructuras y declaraciones de Windows de Form1, CancionEnMemoria, MedidasBitacora y DiagnosticoCompleto
(MEMORYSTATUSEX, DISPLAY_DEVICE, WIN32_FIND_DATA, `RegistroAudio`, `ListadoWin32`, las de kernel32 del diagnóstico), las funciones falsas
(`GlobalMemoryStatusEx`, `FlushFileBuffers`, `timeBeginPeriod`...), de `LlamadasWindows.cs` todo menos lo de las otras pistas (teclas,
foco, refresco de la pantalla), y los ocho avisos IL3050 de `Marshal.SizeOf(typeof(...))` de `Aot.cs`.

**Cómo se comprobó (8-11-2026, JIT).**

- `rocola-xp --prueba-aot`, antes y después: lo mismo salvo lo que debía cambiar. `diag.cpu` dice ahora los MHz, como XP en el A4
  («Intel(R) Xeon(R) Processor @ 2.30GHz (2300 MHz), 4 nucleo(s)/hilo(s)» en la máquina de pruebas; la referencia, sin los MHz, porque su
  registro fallaba); `win.sistema` = «Linux 6.18.44-fc-v310 (Ubuntu 24.04.5 LTS)» aquí; la memoria, las direcciones, el adaptador
  (`llvmpipe (LLVM 20.1.2, 256 bits)`) y la tarjeta de `/sys/class/drm` de prueba (`radeon|1002:990C|HDMI-A-1`), iguales; `win.prioridad`:
  todos los hilos que había (15 en esa corrida) pasan de 0 a -6; `win.prioridad_hilo`: Highest -16, AboveNormal -11, Normal -6,
  BelowNormal -1, Lowest +4 y de vuelta -6.
- La rocola entera, el nice de cada hilo a los 13 s de la escena de las tres canciones (`/proc/PID/task/*/stat`): «audio (salida)» -16,
  «Rocola: la música» -11, la pantalla, el vigía de la pantalla, los de .NET (también los 4 del grupo de hilos) y los de Mesa -6, un hilo
  de lo mínimo +4 y el de la caché de sombreadores de Mesa +13 (Mesa lo pone en 19). En la referencia, todos los de la rocola en -6.
- El sonido con las tablas medidas (`sonido.sh`, 5 corridas): las 5 iguales byte a byte a la corrida 1 de la referencia (huella
  `708cb13b725e`, 1 694 140 bytes), la misma variante que antes de W3.
- La escena del video (`video.sh`, otra vez con la referencia): `video.wav` igual byte a byte, los 12 `rocola_*.txt` iguales, y las capas
  en el mismo orden (12 s la pared, 8 s el video en ventana, 19-20 s a pantalla completa, la pared; el segundo de la transición cae o no).

## Las herramientas (`Pruebas/Windows/`, fuera del programa)

Todas cargan `comun.sh`: `W` es la carpeta de trabajo fuera del repositorio (por omisión `/root/rocola-trabajo/port/windows`), `ROCOLA`
esta rama, `REF` la referencia congelada (`/root/rocola-trabajo/referencia-xp/sistema/rocola`, solo se lee) y `DATOSXP` los datos de prueba
(`/home/user/Rocola/sistema/rocola/Xp/bin/Release/net10.0`). Cada corrida usa una copia nueva de los datos en `W/run` (rutas iguales en
todas) y deja lo suyo en `W/runs/NOMBRE`.

- `prepara.sh ref|mio`: copia el programa compilado (JIT) a `W/prog-ref` o `W/prog-mio`.
- `corre.sh PROG NOMBRE SEGUNDOS [VAR=valor...]`: la rocola fuera de pantalla con ALSA nula (`EXTRA=` un video más; `DATOS=` las tablas de
  partida; la música con sus fechas).
- `video.sh PROG NOMBRE`: la escena del video de 30 s (capturas, capas, `video.wav`); `aislada.sh PROG VUELTAS SEGUNDOS`: la prueba
  aislada del video.
- `prueba-fuente.sh ref|mio`: copias de las dos fuentes con el dither de semilla fija (solo para comparar el sonido), compiladas en
  `W/prog-ref-semilla` y `W/prog-mio-semilla`.
- `sonido.sh PROG NOMBRE SEGUNDOS TECLAS`: la rocola con la tarjeta de prueba con reloj (`alsa/reloj.c`, se compila sola con gcc y
  libasound); `variantes.sh PREFIJO VUELTAS`: la escena de las tres canciones con las dos, alternadas, y la huella de cada sonido.
- `compara.py A B` (las tablas sin las horas de los archivos copiados, las capturas y `video.wav`), `difsonido.py A.raw B.raw` (dos
  crudos de 16 bits, muestra a muestra, por tramos), `difxp.sh ARCHIVO.cs` (lo que cambia la traducción frente a `Rocola/`, sin lo
  mecánico) y `finales.py REV ARCHIVO...` (devuelve a cada línea el fin de línea que tenía en REV: muchos archivos de XP mezclan CRLF y LF).
- En el programa: `rocola-xp --prueba-video`, `--prueba-lector ARCHIVO...` (el lector de lo que no es MP3 ni WAV de siempre, y la
  duración de un video con el medidor) y `--prueba-salida SEGUNDOS [PARA]` (la salida de XP con un tono, la posición frente al reloj y la
  parada); se piden con `ROCOLA_ALSA=reloj` y `ALSA_CONFIG_PATH=/usr/share/alsa/alsa.conf:<un .conf como el que escribe sonido.sh>` para
  oírlas con la tarjeta de prueba.
