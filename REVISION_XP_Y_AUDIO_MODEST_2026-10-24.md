# Revisión: lo que podía tumbar la rocola en XP, y la cadena de audio — 24 de octubre de 2026

Rama Modest (`claude/modest-carson-fa2v2n`). Petición del dueño: «Revisa que la cadena de audio no tenga bugs ni errores, y revisa que
no haya métodos de renderizado que puedan hacer que el programa crashee en la máquina de Windows XP».

Se revisó leyendo el código y con herramientas sobre el ejecutable compilado (lectores de IL hechos con Mono.Cecil). Cada defecto
que se arregló tiene una prueba que falla con la versión anterior y pasa con la nueva. Todo se probó en Linux con Mono y con tarjetas
de prueba. **Nada de esto se ha probado todavía en el A4 con XP.**

## 1. Lo que se arregló en el renderizado

### Una textura que mide menos de lo apuntado: escribir fuera de su memoria

Al bloquear una textura para subirle una imagen, solo se comprobaba el **ancho** (el paso de fila que da Direct3D), no el alto. Si
una cuenta de la rocola se equivocaba de alto, la copia escribía filas de más **en la memoria del controlador**. En XP eso no da error
en el momento: el programa sigue y truena mucho después, en otro sitio.

La tanda de rendimiento de hoy (el repuesto de texturas, `RespuestaGpu.cs`) abrió una puerta a eso. El fundido de colores apuntaba
las piezas de cristal de la lista con el rectángulo **donde se dibujan** (`songList.Width/Height`), no con la medida del mapa de bits
con el que se subieron (`ClientSize`). Hoy esas medidas coinciden, porque la lista no tiene borde. Pero si algún día no coincidieran
en una potencia de dos, el repuesto daría una textura más chica de lo que dice.

- El fundido de colores apunta la medida del mapa de bits (`BmpW`, `BmpH`), y esa es la que va al repuesto.
- **Antes de subir una imagen entera o un trozo** a una textura que ya existe (`UpdateTexturePow2`, `UpdateTextureRegion`), se le
  pregunta a Direct3D cuánto mide (`GetLevelDesc`, ranura 17). Es una llamada que no espera a la tarjeta. Si mide menos de lo que se
  le va a subir, no se usa y se crea una nueva. Esto protege también cualquier otra cuenta equivocada, no solo la del repuesto.
- Prueba (`buscaligero`, 4c): se pone en el repuesto una textura de 512x128 apuntada como de 600 de alto, y se sube una imagen de
  512x600.
  - **Con la versión anterior, la memoria se corrompe y la prueba muere después con un SIGSEGV.**
  - Con la nueva, esa textura no se usa y la imagen va entera a una textura nueva de su medida.

### WM_PAINT ya no deja la X roja para siempre

El bucle de cuadros atrapaba una excepción de un cuadro y seguía. El `OnPaint` del panel de la tarjeta no la atrapaba.

En .NET 2.0, una excepción que se escapa de `OnPaint` marca el control: desde entonces solo pinta la X roja y no vuelve a llamar a
`OnPaint` hasta reiniciar. WM_PAINT llega al destaparse la ventana, por ejemplo al volver de la pantalla de inicio de sesión de
Windows (la del pantallazo del 22-10) o al cerrar Ctrl+F3.

Ahora `OnPaint` lo atrapa igual que el bucle y lo apunta en `rocola_error.txt`, como mucho una vez cada 10 s. La prueba
`recolector` (5) lo comprueba de verdad: con un panel a medio armar, el cuadro truena y no sale de `OnPaint`. Con la versión anterior,
salía.

### Las carátulas enormes, de una en una

Lo encontró el agente de GDI+. GDI+ descomprime la portada original **entera** para reducirla a miniatura:

- un JPEG de 8000x8000 pesa menos de 1 MB en el disco y pide unos 250 MB al descomprimirse;
- los lectores de carátulas y el calentador de miniaturas trabajan a la vez, y dos de esas juntas pedían +481 MB (medido en Mono;
  en el GDI+ de XP no se ha medido).

El 23-10 a las 13:59, con el calentador en marcha, a la rocola le quedaban 1303 MB de direcciones libres.

- Ahora las de más de 16 millones de píxeles se decodifican **de una en una**.
- La medida se lee de la cabecera (JPEG, PNG, GIF, BMP) antes de abrirlas.
- Las normales siguen a la vez y sin esperar.
- La miniatura sale idéntica píxel a píxel.
- De paso, una miniatura que falla a medias ya no se queda sin soltar.

Prueba nueva: `caratulaenorme`.

## 2. Lo revisado en el renderizado que está bien

- **Las ranuras de Direct3D 9.** Las 33 que se usan (IDirect3D9, IDirect3DDevice9, IDirect3DTexture9, IDirect3DVertexBuffer9) se
  compararon una por una con el orden de `d3d9.h`, y todas coinciden.
  - Esto las pruebas no lo pueden ver: la tarjeta imitada usa los mismos números que la rocola.
- **Las estructuras que se le pasan al controlador:**
  - `D3DPRESENT_PARAMETERS`, `D3DLOCKED_RECT` y el vértice miden lo que deben;
  - la de capacidades tiene 512 bytes reservados para los 304 de `D3DCAPS9`, con los desplazamientos correctos.
- **Ningún hilo de fondo llega a Direct3D.** El dispositivo se crea sin `D3DCREATE_MULTITHREADED`, así que solo puede usarlo la
  pantalla. Se recorrieron con un grafo de llamadas las 34 entradas de hilos (Thread, ThreadPool, Timer y BackgroundWorker), y
  ninguna llega a una llamada de Direct3D.
  - El grafo sigue las llamadas directas, no las que pasan por interfaces o delegados.
- **Los hilos de fondo atrapan sus excepciones** (en .NET 2.0, una que se escape cierra la rocola): las 34 se revisaron.
- **Ni un método de .NET 2.0 SP2 o posterior.** El A4 tiene 2.0.50727.1433, que es SP1.
  - Se leyeron todas las llamadas de `Rocola.exe`, NAudio y NLayer. La única de SP2 es `WaitOne(Int32)` dentro de `WaveOutEvent` de
    NAudio, y la rocola no lo usa (usa `WaveOut` con ventana).
  - `GCSettings.LatencyMode` es de SP1.
- **Las llamadas a Windows** (`dwmapi.dll`, que XP no tiene, y CoreAudio de NAudio, que es de Vista) están protegidas y no se llaman
  en XP. Los demás P/Invoke existen en XP SP3. La rocola solo abre MP3 y WAV, nunca por Media Foundation.
- **El dispositivo solo se suelta en `StopGpu`,** que antes vacía el repuesto. Una textura de un dispositivo viejo no puede pasar al
  nuevo.
- **El anillo de vértices** bloquea justo lo que copia, y nunca más que su capacidad.
- **GDI+** (lo revisó el agente):
  - ningún objeto GDI+ local queda sin soltar (escáner de IL validado con una fuga a propósito);
  - no se usan GetHdc, GetHbitmap ni ToHfont;
  - la canción en memoria tiene tope.

### Lo que no se pudo resolver desde el código

`rocola_error.txt` trae paradas de la pantalla **dentro del Present** de la tarjeta:

- 2015 ms el 23-10 a las 13:59;
- 500 ms dos veces el 24-10.

Las tres fueron con el calentador de miniaturas en marcha.

Un Present que espera tanto es el controlador esperando a la tarjeta, el mismo tipo de espera que el pantallazo 0xEA del 22-10. La
rocola pide un Present normal (ventana, sincronizado con la pantalla). No hay en el código nada que explique dos segundos, así que no
se cambió nada a ciegas. El tope de las carátulas enormes baja la memoria de esos momentos. Si vuelve a pasar, el próximo
diagnóstico completo dirá qué se marcó en el hueco.

## 3. La cadena de audio

(En curso: se añade cuando termine su revisión.)

## 4. Pruebas

- Nuevas o ampliadas: `buscaligero` (4c, y la tarjeta imitada ahora contesta `GetLevelDesc`), `recolector` (5) y `caratulaenorme`.
- Las de la tarjeta afectadas siguen en verde: `recuperagpu`, `estadoeficiente`, `performance`, `huellacuadros`, `tironescambio`,
  `buscaportadas` y `dibujoligero`.
