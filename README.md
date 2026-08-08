# Rocola

Rocola digital para Windows (WinForms + NAudio): pantalla completa, manejo por
botonera/teclado físico (sin depender de un teclado de PC), monedero con créditos,
búsqueda con teclado en pantalla, sección de videos (Windows Media Player), portadas
en cubos 3D, fondo estrellado por género, estadísticas ("Lo más sonado" + TOP por
artista) y modo atracción.

**Versión 2.5.** Esta rama es la **edición clásica (XP)**: la rocola física original.

## Ramas

- **Clásica (XP)** — *esta rama*, la de referencia: .NET Framework **3.5 sin SP1**,
  Windows XP 32-bit, CPU modesto. Todo lo nuevo nace aquí y se prueba en el fierro real.
- **`rocola-hd`** — para PC **potente** (Windows 7+/10, 4+ núcleos, 4+ GB): retargeteada
  a **.NET Framework 4.8**, con salida WASAPI flotante, reconstructor espectral fino,
  render por GPU (Direct3D 9) y **DSEE ULTRA** (IA Apollo con selector de GPU). Mismo
  programa y misma lógica de audio; solo cambia lo que el fierro potente puede aprovechar.

## Qué hace (v2.5)

**Operación (local):**
- Se maneja con la botonera/teclado físico configurable; nada de "lenguaje de teclado de PC".
- Monedero con créditos y cola de canciones pagadas.
- Buscador con teclado en pantalla; sección de videos; portadas en cubos 3D girando.
- La lista de cada disco muestra la **duración** de cada canción. Va en su **propia pasada rápida**:
  sacar solo la duración cuesta ~600 veces menos que el análisis de volumen/EQ (es leer la cabecera,
  no descomprimir el audio), así que la biblioteca entera queda con duración en minutos en vez de
  esperar días al análisis pesado. Se guarda en `rocola_duracion.txt` y sobrevive a los reinicios.
- **Animaciones** (todas **por GPU**, así que no le cuestan nada al CPU; `animacion=0` las apaga):
  - **La portada viaja entre cubos.** La rocola ya dibuja todo como cubos girando (la casilla de la
    cuadrícula, el cubo grande del disco, el cubo gigante de la atracción, el del buscador). La
    portada **sale calcada sobre el cubo de origen** —misma casilla y mismo giro, no se nota el
    relevo—, vuela girando un cuarto de vuelta, se ladea y se endereza, lleva un **destello** detrás,
    y **aterriza calcada sobre el cubo de destino**, donde se funde con él. Cuatro recorridos, el
    mismo código: cuadrícula→disco, disco→cuadrícula, cuadrícula→atracción y atracción→cuadrícula
    (~0.26 s). Desde el buscador sale del cubo grande del resultado elegido.
  - **Buscador**: se abre y se cierra **como una ventana de macOS** — entra al 86 % de su tamaño y un
    poco más abajo, se pasa un ~4 % de su tamaño final y **rebota** hasta asentarse (rebota algo más
    a lo ancho que a lo alto, así que se estira un instante: eso es lo que le da vida). Al cerrarse
    toma **impulso** —crece un pelín— y luego se encoge, se hunde y se va acelerando (~0.21 s).

  Todas son solo un dibujo **encima**: el cambio de pantalla sigue siendo instantáneo, y si algo las
  interrumpe (modo atracción, un video, otra tecla) simplemente dejan de dibujarse — nunca queda nada
  a medias. El avance lo calcula el propio render (60 fps), no el temporizador de la interfaz, así que
  se ven fluidas.
- **Aurora del horizonte** (`espectro=` en config, 0 la apaga): el visualizador de espectro, hecho como
  parte del **cielo** y no como un aparato encima. Un resplandor suave en la franja baja cuya silueta
  la dibujan **8 bandas** medidas en vivo, con el color del género. Va **detrás** de la cuadrícula y
  se atenúa junto con el fondo, así que nunca le pelea a las portadas ni al texto; se apaga sola
  cuando no hay música. Las bandas viajan por la **misma cola retrasada** que el pulso, así que la
  aurora se mueve encima del sonido que se oye y no un segundo antes.
- "Lo más sonado" y TOP por artista; modo atracción cuando nadie la usa.
- **Blindaje de kiosco**: una excepción no controlada se anota en `rocola_error.txt`
  y la rocola **sigue corriendo** (ya no muere con el diálogo de error de .NET).

**Sonido:**
- **Nivelado perceptual por etapas** (tipo LUFS / BS.1770, con *K-weighting*): todas suenan
  parejo; sube las bajitas y **no** baja las que ya suenan fuerte. Seis etapas según lo bajita
  que venga la canción (**ULTRA BAJA / BAJA / MEDIA-BAJA / MEDIA / ALTA / MUY ALTA**), cada una
  con su propio tope de subida (hasta **+20 dB** en las ultra bajas, apenas +4 en las muy altas),
  interpolado entre etapas para que no haya saltos.
- **Pre-análisis de la biblioteca**: en segundo plano (prioridad mínima, solo cuando la rocola
  está en reposo) se mide el volumen, la **duración** y la **huella tonal del EQ automático** de
  cada canción. Ya **no** hace falta que una canción suene una o dos veces para quedar nivelada
  ni ecualizada: llega ajustada desde la primera.
- **Subir TODO el volumen**: dB extra generales; el **limitador con pre-escucha** baja los picos
  suave y ANTES de que lleguen (fuerte sin distorsión).
- **EQ automático**: corrige el tono de cada canción hacia el promedio de la biblioteca.
- **Bajo psicoacústico** (`bajo=` en config, perilla en Ctrl+F3): las bocinas de un local no bajan de
  ~80 Hz, así que el grave profundo **no sale** por ellas. Subirlo con EQ no sirve — solo gasta
  amplificador y hace toser al cono. En vez de eso se generan los **armónicos** de esa nota grave (el
  doble y el triple de su frecuencia), que la bocina sí reproduce, y **el oído reconstruye solo la
  nota que falta** (efecto de la *fundamental ausente*). El grave se toma de la mezcla mono y sale
  centrado. Verificado: genera el 2º y 3er armónico, **no toca** la nota original ni la voz ni los
  agudos (0.00 dB), en 0 % el audio sale idéntico bit a bit, y cuesta **0.17 % de un núcleo**.
- **DSEE** (realce/reconstrucción de agudos): recrea el "aire" que el MP3 recorta.
  Insignia **MEJORADA** (dorada, que brilla) cuando de verdad está mejorando la canción.
- **Des-recorte (declipper)**: repara los picos aplastados "de fábrica" de archivos mal
  masterizados (reconstruye el pico redondo perdido; `declip=` en config).
- **Fundido** suave de salida al terminar cada canción **y cada video** (`fundido=` en config, 0-3 s).
- **La interfaz sigue por GPU con video sonando** (`video_gpu=` en config, encendido por defecto).
  Antes, un video mandaba **toda** la pantalla a render por CPU (fondo estrellado incluido) mientras
  WMP decodificaba el video — de ahí el rendimiento raro con videos, y por eso las animaciones (que
  van por GPU) nunca podían verse con un video sonando: solo la del buscador, el único modo que ya
  ignoraba el video. El cuadro del video va **encima** del panel y Windows recorta uno donde está el
  otro. Con video visible, el panel baja solo a ~30 cuadros para dejarle la máquina al video. Si en
  algún equipo se viera mal, `video_gpu=0` vuelve al comportamiento anterior. El video a **pantalla
  completa** sí sigue apagando el panel.
- **Bitácora de diagnóstico** (`diagnostico=1` en config): apunta con hora los eventos clave (inicio
  y fin de video, animaciones que arrancan o el motivo por el que no, cambios de modo de render) en
  `rocola_diagnostico.txt` junto al exe. Para cazar problemas del fierro real sin adivinar; apagada
  no cuesta nada.
- **Volumen de los videos**: el video lo reproduce Windows Media Player, cuyo sonido **no pasa**
  por la cadena de audio. Una perilla en Ctrl+F3 (`video_vol=` en config) empareja su volumen con
  el de las canciones ya niveladas, y además sigue a "Subir TODO el volumen".
- **Dither TPDF** al bajar a 16 bits (salida WaveOut de XP): sin ruido de truncado.
- **Respaldo automático** de lo aprendido (volúmenes, EQ, estadísticas, config)
  a otra carpeta/disco; conserva los 3 respaldos más recientes.

### La IA aprende de TODA la biblioteca (sin mover el disco)

Para enseñarle a la red hacen falta pares *"esto es lo que queda / estos eran los agudos reales"*.
En un MP3 dañado los agudos reales **ya no existen**, así que solo sirven de maestras las canciones
que **aún los conservan** (320 kbps, FLAC, WAV).

Mientras el **pre-análisis** recorre la biblioteca en segundo plano, a esas canciones completas les
corta el agudo **a propósito** (a 11, 13, 15, 16.5 y 18 kHz) y guarda el par en
`rocola_ia_datos.bin`. Usa las **mismas funciones** que la reproducción, así que el material de
entrenamiento coincide **exactamente** con lo que la red verá en vivo (verificado: diferencia 0.0000).

Flujo completo:

1. La rocola junta el material sola. El avance se ve en **Ctrl+F3** ("Material para la IA: N ejemplos").
2. Copias `rocola_ia_datos.bin` a una **memoria USB** (el archivo es pequeño; no hay que mover el disco).
3. En una PC potente: `python3 ia/entrenar_ia.py --datos rocola_ia_datos.bin --capas 28,96,96,12`
   (el script viene en la carpeta `ia/`; solo necesita **Python 3 + numpy**).
4. Regresas el `rocola_ia.bin` nuevo junto al `Rocola.exe`.

Se apaga con `ia_datos=0` en `rocola_config.txt`.

### Entrenamiento a fondo (mantenimiento)

Botón **"ENTRENAMIENTO A FONDO"** en Ctrl+F3. Mientras está encendido, la rocola **se dedica por
completo** a dejar todo calibrado:

- Repasa **toda** la biblioteca (no solo lo que falta) y analiza cada canción **completa**, no por
  muestras — la medida más exacta posible de volumen, EQ y material para la IA.
- **Se puede parar y seguir.** Apunta en `rocola_afondo.txt` cada canción que ya repasó, así que al
  detenerlo y volverlo a encender —o si apagas la rocola, o se va la luz— **continúa donde iba**, no
  vuelve a empezar por la primera. La barra también sigue desde donde estaba. Cuando ya no queda
  nada, el panel lo dice: *"la biblioteca ya está repasada entera"*. La música que agregues después
  entra sola en la cuenta.
- **No espera** a que la rocola esté en reposo ni descansa entre archivos, y el hilo sube a
  prioridad normal.
- Una **barra de avance** y el texto muestran cuántas canciones lleva y cuántos ejemplos de IA.
- Sigue corriendo aunque cierres el panel; se detiene con el **mismo botón**.

**Los dos modos se acumulan.** El modo automático saca un puñado de ejemplos por canción; el repaso
a fondo la recorre **entera** y saca el triple, repartidos por todo el tema. Se apunta *hasta qué
nivel* llegó cada canción (`rocola_ia_hechas.txt`), así que el repaso a fondo **vuelve a entrar** en
las que solo tienen el muestreo y les añade el resto — pero dentro del mismo nivel una canción
aporta una sola vez, para no sesgar el entrenamiento hacia ella.

El archivo de material tiene un **tope de 24 GB** (`ia_datos_mb=` en config; 0 lo quita). Con eso
cabe una biblioteca de ~20 000 canciones aptas cosechadas a fondo, y entra holgado en una memoria de
64 GB. Ojo: la memoria tiene que estar en **exFAT o NTFS** — en FAT32 ningún archivo puede pasar de
4 GB, y ahí habría que bajar el tope. En Ctrl+F3 se ve el tamaño actual y si llegó al tope.

**El entrenador no necesita 24 GB de RAM**: se asoma al archivo desde el disco y toma una muestra
repartida de punta a punta (uno de cada tantos ejemplos, así entran canciones de toda la biblioteca
y no solo las primeras). Se ajusta con `--max-ejemplos N` (por defecto 4 millones; `0` = todos).

El modo a fondo **no** se guarda en la configuración a propósito: si se va la luz, la rocola vuelve
sola a su modo normal y nunca se queda a todo gas en horario de negocio.

## Compilar

Proyecto clásico de **.NET Framework 3.5** (Visual Studio). Abre `Rocola.slnx` y compila en
Release. El resultado (`Rocola.exe` + `NAudio.dll`) se copia junto a la carpeta `musica`, como siempre.

- Código: `Rocola/Form1.cs` (monolítico) + `Form1.Designer.cs` + `Program.cs` + `D3D9Star.cs`.
- Dependencia: **NAudio 1.9.0** (net35): la DLL viene **incluida** en `packages/` y el proyecto
  la referencia directo — compila **sin NuGet y sin internet** (ya no hay `packages.config`).

> `bin/`, `obj/`, `.vs/` y el resto del paquete NuGet no se versionan (ver `.gitignore`);
> solo se incluye la DLL de NAudio que el `HintPath` necesita.

## Optimizaciones aplicadas (sin cambiar aspecto ni comportamiento)

1. **Estabilidad — `PlayTrack`**: si varias canciones seguidas no abren (archivos
   movidos/corruptos), antes se apilaba `PlayTrack → PlayNext → PlayTrack…` (posible
   *StackOverflow*). Ahora el `PlayNext` del `catch` va por `BeginInvoke`, que desenreda la pila.
2. **Búsqueda — `Score` / `RecomputeResults`**: `" " + q` y `q.Split(' ')` se calculaban
   dentro de `Score` (corre por **cada** entrada del índice en **cada** tecla). Ahora se calculan
   una sola vez por pulsación (puntaje idéntico), evitando decenas de miles de asignaciones.
3. **Render — `Cube.Draw`**: los tres arreglos de la proyección 3D (`ex`, `depth`, `order`)
   se reutilizan vía `[ThreadStatic]` en vez de asignarse en cada llamada.
4. **Escaneo del disco en UNA pasada** (`ScanFolders`): antes se caminaba el árbol dos
   veces (audio y video) y además se pedía la lista de cada carpeta otra vez para buscar
   la portada — **tres lecturas por carpeta**. Ahora se camina una sola vez y de esa misma
   lista salen las tres cosas. Verificado contra el método anterior: mismas canciones en el
   mismo orden, mismos videos y **la misma portada elegida** en cada carpeta.
