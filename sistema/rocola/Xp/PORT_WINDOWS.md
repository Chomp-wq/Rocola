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
  `herramientas/aislada.sh mio 20 3` → **20 de 20 bien** (ninguna lectura negra, todas amarillas como el video de prueba).
- La rocola entera con un video de 30 s, contra la referencia (`herramientas/video.sh ref|mio NOMBRE`: REGRESAR+DERECHA dos veces, PONER,
  MONEDA, PONER; el video suena en ventana, a los 8 s sin botones pasa a pantalla completa con la franja de la atracción encima y al
  acabar vuelve la pared; capturas a los 16, 24, 44 y 50 s; el sonido de mpv a un WAV por ALSA `file:`):
  - el sonido del video (`video.wav`, 10 584 108 bytes, 30 s a 44,1 kHz estéreo de 32 bits): **igual byte a byte** que el de la
    referencia (con la curva de volumen de XP: el arranque, el nivel por el disco y el fundido del final);
  - los 12 `rocola_*.txt` del final, quitando las horas de los archivos copiados (`compara.py`): **iguales**, incluido
    `rocola_duracion.txt` (`30|.../Un video largo.mp4` y `8|.../Video de prueba.mp4`, como la referencia; con la duración de libmpv salía
    `30.02` y `8.02`);
  - las capas que compone la pantalla, una vez por segundo (`ROCOLA_TRAZA_CAPAS=1`): la misma sucesión (12 s la pared; 8 s el video en
    ventana en `512,56 498x606` encima del panel; 21 s el video en pantalla completa en `0,46 1024x722` debajo de la franja; la pared otra
    vez). La única diferencia es un segundo de transición que la referencia muestreó con la pared aún sin poner (entre `aviso 7` -el
    video acabó- y la pared hay ~0,3 s; la toma de cada segundo cae o no ahí);
  - las capturas: el video se ve en el mismo sitio y con el mismo cuadro de prueba, y la franja «¡ PRESIONA UN BOTON PARA USAR LA ROCOLA !»
    encima a pantalla completa; los píxeles distintos son los del cielo animado y del cuadro del video, que también cambian entre dos
    corridas de la referencia (26 000 a 540 000 píxeles entre la referencia y esta; 107 000 a 450 000 entre dos de la referencia).
