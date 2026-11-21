# Fase 11, rendimiento: lo general del programa (rama `rapido-general`)

21-11-2026. Sobre `6c4dc1a` (la rama principal; solo difiere de `85c931b` en `AGENTS.md`). Petición del dueño del 20-11-2026 (muchas
mejoras de rendimiento; esta rama lleva lo general, sin las carátulas ni Spotify, que llevan `rapido-caratulas` y `rapido-spotify`).
**Nada se probó en el A4**: los números son de una máquina de 4 núcleos compartida con otros agentes (carga 11 a 22 durante las medidas) y
de llvmpipe (la tarjeta por procesador), no de la Radeon r600; sirven para comparar antes y después en las mismas condiciones.

## Las reglas que se siguieron

- `Codigo/` no se tocó (ni una línea): todo lo hecho está en `Nativo/` (la traducción), con **el mismo resultado punto por punto** que antes
  (cada cosa con su prueba de igualdad, abajo). Sin ganchos nuevos (no hizo falta ninguno del rango g250-g264) y sin interruptores: no cambia
  el aspecto ni la función (decisión del 15-9-2026).
- Lo que cambiaría la imagen o pide al dueño no se hizo: va en «Propuestas».
- Medido siempre en NativeAOT, la base y lo nuevo alternados, con la misma biblioteca y las mismas teclas.

## Cómo se midió

- **La biblioteca inventada** (`Pruebas/RapidoGeneral/guiones/genera.py`): 700 artistas, 1 200 discos, 13 119 canciones (MP3 de 8 s
  enlazados de 6 bases hechas con ffmpeg: 19 MB en disco), portadas JPEG chicas (300-500 px) generadas en el 90 % de los discos, nombres con
  acentos. Dos carpetas de datos: `d0` (nueva: la biblioteca se recorre y el repaso empieza) y `dcal` (la misma después de 50 min de
  repaso: las 13 119 medidas de sonoridad, las tablas grandes, 1,4 a 2,5 MB cada una).
- **La función de prueba `medir-general`** (`Pruebas/RapidoGeneral/Mide.cs`, solo con `ROCOLA_OS=medir-general`, compilada aparte en
  `Pruebas/RapidoGeneral/RapidoGeneral.csproj`, no va en el programa): usa **la toma de cuadros del diagnóstico completo de XP**
  (`DiagCaptura`: el hueco entre dos cuadros presentados, sus fases y los GC, sin reservar nada por cuadro) y su foto del procesador por hilo
  (`DiagInstantanea`), por tramos de tiempo; apunta además la memoria nueva del hilo de la pantalla y de todos
  (`GC.GetAllocatedBytesForCurrentThread`, `GetTotalAllocatedBytes`), los despertares por hilo (`/proc/self/task/*/status`), el primer cuadro
  y la biblioteca publicada (al milisegundo, desde que nace el proceso).
- **La rocola entera fuera de pantalla** a 1024×768 (`corre.sh`): el programa en los núcleos 0 y 1 (dos, como el A4) y los hilos de llvmpipe
  (lo que en el A4 hace la tarjeta) en el 2 y el 3; las teclas por `ROCOLA_PULSA`, las de los recorridos de `Pruebas/Recorridos` (pared,
  buscador, ayuda, Ctrl+F3, botones).
- **Dónde se va el tiempo**: muestreo de pilas con gdb (`muestrea.py`, sobre el ejecutable con sus símbolos) y **muestreo de la memoria
  nueva** (`asigna.py`: un punto de parada en `RhpGcAlloc`, la reserva lenta del recolector, una cada ~8 KB nuevos, solo en el hilo de la
  pantalla, con la pila).

## Los cuellos de botella que se encontraron (antes de tocar nada)

1. **El compositor de la pantalla mezclaba en cada cuadro dos capas de 1024×768 que no tenían nada** (`Nativo/CompositorPantalla.cs` y
   `PantallaRocola.Vistas.cs`). Con la tarjeta, la ventana de la rocola se compone de abajo arriba como: el tramo de lo pintado por
   procesador debajo de la tarjeta, la textura de la tarjeta (pantalla completa, sin mezcla) y el tramo de encima. Con la tarjeta todo lo de
   XP está en la tarjeta: **los dos tramos son transparentes enteros** (comprobado con `ROCOLA_CAPTURA_CAPAS`: sin un solo punto) y el de
   abajo además queda tapado. Aun así cada cuadro los mezclaba (dos pasadas de 786 432 puntos con mezcla) y cada vez que una vista de encima
   se marcaba para repintar, los borraba (3 MB cada uno), creaba su lienzo y los volvía a subir a la tarjeta (`TexImage2D` de 3 MB). En el
   buscador eso pasaba **en casi cada cuadro** (1 088-1 100 repintados de los dos tramos en 33 s). Coste medido de componer (abajo): 17-20 ms
   de 6,6 con llvmpipe.
2. **El texto de GDI+ (`Nativo/TextoGdiPlus.cs`, `Escribe`) creaba objetos de .NET en cada llamada**: `SKCanvas.DrawText(string, ...)` de
   SkiaSharp arma un `SKTextBlobBuilder` y un `SKTextBlob` (cada uno con su envoltorio, su finalizador y su apunte en el diccionario de
   objetos de SkiaSharp, con candado compartido por todos los hilos que dibujan) y un `SKPaint` nuevo. Con la barra de letras de XP y su
   lupa (`LetterBar.RebuildLupa`) era **el 70 % de la memoria nueva del hilo de la pantalla al hojear** (170 de 250 muestras).
3. Por cuadro, además: la foto de la lista de la ventana (`ToArray`), listas nuevas, las capas nuevas y `GetType().Name` (en NativeAOT arma
   la cadena cada vez) para el nombre de la capa de la tarjeta (que solo usan las trazas); un `SKPath` nuevo por polígono (`Pixel.cs`).
4. **El repaso de la biblioteca** (el trabajo de fondo más largo): el 80 % del procesador se va en la medida de XP (`MedidorBs1770`,
   `HuellaDelTrozoDesde`, `SpectralTap`, `MedidorZumbido`, `PicosBloques`...: `Codigo/`, no se toca) y el 20 % en descomprimir el MP3
   (`mpg123_decode`, la biblioteca nativa); lo de la traducción alrededor (`Puente.LeeBytes`, las lecturas) no se ve en el muestreo. No hay
   nada que ganar ahí sin cambiar la lógica de XP. Unos 47-50 ms de procesador por canción de 8 s.
5. **El arranque**: el primer cuadro a 1,15-1,35 s del nacimiento del proceso (con las tablas grandes: leídas en 148-388 ms según la carga) y
   la biblioteca publicada a 0,28-0,63 s. En el muestreo el hilo de la pantalla pasa ese segundo casi entero esperando a llvmpipe (compilar
   sus programas y pintar los primeros cuadros) y horneando el cielo de XP (`Starfield`, `BakeRuido`); lo de la traducción es poco (ver
   «Lo que se probó y no ganó» y las propuestas).
6. Ctrl+F3: la hoja se repinta entera solo cuando algo cambia (39-45 veces en 33 s, p50 2,6-3,2 ms): no es un cuello.

## Lo hecho (por lo que más se nota)

### 1. El compositor no mezcla lo que no se ve (commits `e619cf0`, `ac45b3f`, `20feb6e`; `Nativo/CompositorPantalla.cs`, `Nativo/PantallaRocola.Vistas.cs`)

- Una capa pintada por procesador en la que **no se pintó nada** (ni el fondo de la ventana ni ninguna vista que se vea: `Tramo.Vacio`,
  `CapaPantalla.Vacia`) no se sube ni se mezcla: con `ONE, ONE_MINUS_SRC_ALPHA` un punto transparente deja el de debajo tal cual.
- Lo que queda **debajo de la última capa de textura que tapa la pantalla entera** (la tarjeta: se pone sin mezcla) no se compone.
- Las capas que se saltan guardan su textura si ya la tenían (se sube cuando vuelvan a verse y si cambiaron).
- Un tramo que ya estaba vacío y en el que tampoco ahora se pintaría nada **no se borra ni se le crea lienzo** (`AlgunaSePinta`, la misma
  condición que `PintaRecortada`: ninguna vista que se pinte deja de pintarse).
- Por cuadro: la foto de la lista en un arreglo que se reusa, las listas y las capas de la ventana reusadas (el compositor solo las usa en
  el cuadro), el nombre del tipo una vez por tipo, y las claves de las hojas sin copiarlas.

**Prueba de igualdad** (`Mide.cs`, `ROCOLA_MIDE_COMPARA=K`): cada K cuadros, dentro del mismo proceso, la pantalla compuesta como ahora se
lee (`glReadPixels`), se vuelve a componer **desde cero con todas las capas, como antes** (`CompositorPantalla.ComponeTodo`) y se compara
punto por punto. Con las escenas de los recorridos (pared, buscador, Ctrl+F3 con la hoja, ayuda, botones con monedas y avisos):
**1 198 cuadros comparados, 0 distintos** (503 con el primer cambio y la carpeta nueva; 695 con todo y la carpeta caliente).

**Lo que cuesta componer** (`ROCOLA_MIDE_COSTE=4`: en el mismo proceso, alternando, componer como antes y como ahora hasta `glFinish`;
llvmpipe en dos núcleos, carga 20-21): pared **17,2 → 6,6 ms** (p50; p90 31,2 → 12,8), buscador **20,0 → 6,7 ms** (p90 31,4 → 12,4).
En el buscador, 1 088-1 100 repintados de tramos vacíos (2 × 3 MB borrados y un lienzo cada uno) ahorrados en 33 s.

### 2. El texto de GDI+ sin objetos de .NET por llamada (commit `108548c`; `Nativo/TiraSkia.cs`, `Nativo/TextoGdiPlus.cs`)

- `TiraSkia.Dibuja` hace **las mismas llamadas a libSkiaSharp** que `SKCanvas.DrawText(string, x, y, SKFont, SKPaint)` de SkiaSharp 3.116
  (`sk_font_text_to_glyphs` para contar y llenar en UTF-16, `sk_textblob_builder_new/alloc_run/make/delete`, `sk_canvas_draw_text_blob`,
  `sk_textblob_unref`), sin los envoltorios.
- El pincel de `Escribe` es uno por hilo, reusado: se le pone todo lo que se le ponía al nuevo (estilo, color o degradado, mezcla,
  suavizado); lo demás nunca se toca. Al acabar suelta el degradado.

**Prueba de igualdad**: el banco del dibujo (`nivel0.sh`, paso «banco»: 3 000 casos por semilla contra la copia congelada de la
referencia) **IGUAL en las semillas 7, 11 y 5**; y como control negativo, la tira corrida 0,25 px da **DISTINTO** (53, 49 y 71 líneas): el
banco sí mira este camino.

### 3. Los polígonos en un `SKPath` del hilo (commit `793442b`; `Nativo/Pixel.cs`)

`RellenaPoligono` y `TrazaPoligono` reusan un `SKPath` por hilo (`Reset` lo deja como uno nuevo; se le pone el mismo relleno y los mismos
puntos). Banco IGUAL en las tres semillas.

## Antes y después, con la rocola entera

`varias.sh` (en `/root/rocola-trabajo/fase11/rapido-general`): cada escena 40 s (el tramo medido, de 7 a 37 s, con las teclas de los
recorridos), con la carpeta caliente, **base** (`6c4dc1a` con la medida, `base2-aot`) y **ahora** alternados, dos vueltas cada uno.
Medianas de las vueltas; los cuadros son los presentados en los 30 s (más es mejor); «MB»: memoria nueva del hilo de la pantalla en el
tramo; carga de la máquina de 11 a 14.

| Escena | Programa | Cuadros | p50 | p95 | p99 | máx | MB pantalla | KB/cuadro |
|---|---|---|---|---|---|---|---|---|
| Pared (hojear, letras, abrir disco) | base | 472 | 60,6 | 91,3 | 114,6 | 131,9 | 3,55 | 7,7 |
| | **ahora** | **638** | **44,5** | **71,3** | **93,9** | 142,4 | **3,06** | **4,9** |
| Buscador (escribir, resultados, atajos) | base | 468 | 62,5 | 88,6 | 108,2 | 149,5 | 1,86 | 4,0 |
| | **ahora** | **602** | **48,5** | **71,7** | **85,3** | **97,5** | **1,40** | **2,4** |
| Ayuda y asistente | base | 440 | 66,1 | 96,3 | 111,4 | 150,8 | 1,47 | 3,4 |
| | **ahora** | **541** | **53,6** | **80,6** | **99,2** | **141,1** | 1,40 | **2,6** |
| Ctrl+F3 (pestañas, clics, Tab) | base | 338 | 82,6 | 165,2 | 245,3 | 323,8 | 5,82 | 18,0 |
| | **ahora** | **566** | **50,7** | **86,6** | **113,2** | **198,1** | 5,98 | **11,0** |

(La primera serie, sin el tramo que sigue vacío, `v1`, dio lo mismo en dirección: pared 475 → 546 cuadros, buscador 350 → 510, ayuda 398
→ 456, Ctrl+F3 339 → 348.) **Lo que no dicen**: con llvmpipe el cuadro lo marca casi entero la tarjeta por procesador (la fase «presentar»
del diagnóstico de XP es el 90 % de cada cuadro), así que estos milisegundos no son los del A4. Lo que sí vale para el A4 es el trabajo que
se quitó: dos mezclas de pantalla entera por cuadro y su subida de 3 MB (con la Radeon es ancho de banda de la memoria compartida del APU
con el procesador) y la memoria nueva por cuadro (−30 a −40 %).

## Lo que se probó y no ganó (y no se dejó)

- **`OptimizationPreference=Speed` en NativeAOT**: el repaso de la biblioteca gasta lo mismo por canción (≈48 ms de procesador por
  canción de 8 s con y sin él, dos vueltas, ruido de la carga mayor que la diferencia) y el programa crece 0,7 MB (13,7 → 14,4 MB). No se
  puso (como ya se había medido en el programa anterior).
- **`SKBitmap.SetPixel` del grano del cielo al arrancar** (`Codigo/Form1.Fondo.cs`, `BakeRuido`: 4 096 puntos dos veces): 6-7 ms por vez
  aquí, frente a 0,07 ms escribiendo los puntos directamente (los mismos bytes, comprobado en un banco aparte). Son ~14 ms una vez al
  arrancar; el cambio sería una línea de traducción de `Codigo/` y no compensa tocar `Codigo/` por eso. Queda como propuesta.
- **Cachear los `glEnableVertexAttribArray` por dibujo** (`SombreadoresTarjeta.cs`): Mesa ya los descarta si no cambian; no se tocó.

## Propuestas (piden al dueño o tocan otra cosa)

1. **La caché de programas de Mesa en Rocola OS.** Mesa guarda los programas compilados de la tarjeta en `~/.cache/mesa_shader_cache`
   (aquí llvmpipe los lee al arrancar). En Rocola OS, si el raíz es de solo lectura o se borra en cada arranque, la r600 los compila todos
   cada vez. Poner `MESA_SHADER_CACHE_DIR` en una carpeta de los datos (`/rocola/datos/...`) en el lanzador ahorraría ese tiempo en cada
   encendido. Es del sistema (`sistema/board`), no del programa: no se hizo ni se midió en el A4.
2. **El texto de GDI (`TextoGdi.Pinta`) y los pinceles de las hojas (`PinturaXp.Pincel/Pluma`)** con la misma técnica de la tira (Ctrl+F3,
   Ctrl+F2, avisos): ahora son el 70 % de la memoria nueva al repintar una hoja, pero la hoja se repinta poco (40 veces en 33 s, 3 ms);
   no compensa el riesgo (los pinceles los guardan y sueltan los de `Codigo/`).
3. **El grano del cielo** (punto de arriba): ~14 ms al arrancar.

## Las comprobaciones

(Se completan al acabar la batería del nivel 0.)

## Lo no probado

- Nada en el A4 (ni la Radeon por KMS ni su procesador): ni cuánto ahorra allí no mezclar las dos capas, ni los cuadros.
- El video (libmpv): no se midió (la biblioteca inventada no tiene videos); el compositor deja la capa del video como antes (es de textura
  y no tapa la pantalla entera salvo a pantalla completa, donde lo de debajo no se ve igual que antes).
- Con `gpu=0` (el dibujo por procesador): la capa de la ventana sí se pinta y se compone como antes (no está vacía); no se midió.
