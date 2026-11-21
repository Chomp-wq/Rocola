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

**Prueba de igualdad** (`Mide.cs`, `ROCOLA_MIDE_COMPARA=K`; rehecha tras la revisión). Cada K cuadros, y **siempre en el cuadro en que
una capa que se saltó vuelve a componerse**, dentro del mismo proceso se lee la pantalla compuesta como ahora (`glReadPixels`) y se compone
**como antes, aparte** (`CompositorPantalla.ComponeReferencia`: en un framebuffer propio, con texturas propias que se suben enteras cada
vez, todas las capas sin saltarse ninguna, **sin tocar las texturas ni las versiones del compositor de verdad**), y se comparan punto por
punto. Lo que se presenta es lo que compuso el compositor de verdad. (La primera versión, `ComponeTodo`, componía la de antes con las
texturas del compositor y les ponía su versión: desde ese cuadro una capa saltada quedaba al día aunque el compositor no la hubiera subido,
así que no podía ver el caso «vuelve a verse y hay que subirla»; además se presentaba lo compuesto por ella. Lo vio la revisión.)

- Con las cinco escenas de los recorridos (pared, buscador, Ctrl+F3 con la hoja, ayuda, botones con monedas y avisos), cada una con la
  carpeta nueva (`d0`) y la caliente (`dcal`), dos tandas (`cmp5`, `cmp6`): **5 786 cuadros comparados, 0 distintos** (2 748 y 2 738).
- **Control negativo** (`ROCOLA_MIDE_ROMPE=1`, solo en la compilación de prueba: el compositor no vuelve a subir nunca una capa ya subida):
  Ctrl+F3 da **145 de 231 cuadros distintos** (hasta 384 033 puntos); con la prueba de antes se habría tapado (`ComponeTodo` la subía). La
  pared da 0, porque allí ninguna capa pintada por procesador cambia (todo está en la tarjeta).
- **Lo que la prueba no llega a ver**: en ninguna de esas escenas una capa saltada vuelve a componerse (`vuelven=0` en las doce corridas).
  Pasaría si la tarjeta dejara de tapar la pantalla entera o si un tramo vacío volviera a tener una vista que se pinta; con la tarjeta no
  ocurre en la rocola (todo lo de XP va en ella). Por el código: una capa saltada guarda su textura y su versión; la versión de un tramo
  sube cada vez que se repinta, y al volver se sube si la versión no es la misma (igual que antes con cualquier capa). Si alguna vez ocurre,
  la prueba compara justo ese cuadro y lo cuenta (`vuelven=`, `subidas=`, `comparadas_al_volver=` en el renglón `compara`).
- Todo esto (`Prueba`, `ComponeReferencia`, `ComponeOtraVez`, `LeePantalla`, las cuentas de tramos y de las capas que vuelven, el control
  negativo) **solo se compila en `Pruebas/RapidoGeneral`** (constante `RAPIDO_GENERAL_PRUEBA`): no entra en la rocola.

**Lo que cuesta componer** (`ROCOLA_MIDE_COSTE=4`: en el mismo proceso, alternando, componer como antes y como ahora hasta `glFinish`;
llvmpipe en dos núcleos, carga 20-21): pared **17,2 → 6,6 ms** (p50; p90 31,2 → 12,8), buscador **20,0 → 6,7 ms** (p90 31,4 → 12,4).
**Esto es solo el coste de las mezclas en la tarjeta**: el «antes» se compone con las texturas ya al día (no sube los 3 MB de los tramos
que se repintaban) y no cuenta lo que se hacía en el procesador (el `Clear` de 3 MB y el lienzo nuevo de cada tramo en
`CapasDeLaVentana`). El ahorro de verdad es mayor; esto lo subestima.
En el buscador, 1 088-1 100 repintados de tramos vacíos (2 × 3 MB borrados y un lienzo cada uno) ahorrados en 33 s.

**Arreglos de la revisión, sin cambio de resultado**: en las salidas tempranas de `CapasDeLaVentana` (lista vacía, ventana que no se ve) la
foto de la lista se vacía (`SueltaFoto`: ya no agarra vistas desechadas hasta el siguiente cuadro con vistas), y los tramos que sobran
sueltan lo que agarraban sus capas reusadas (el mapa, la textura de la tarjeta o su vista). `CapasDeLaVentana` no es reentrante, como no lo
era antes (`tramos` ya era una lista estática): solo la llama el compositor, y el compositor solo corre desde el bucle de la pantalla
(`PintaUno`) y desde el `Present` de la tarjeta (`PresentaTarjeta`), nunca desde el `Pinta` de una vista por procesador.

### 2. El texto de GDI+ sin objetos de .NET por llamada (commit `108548c`; `Nativo/TiraSkia.cs`, `Nativo/TextoGdiPlus.cs`)

- `TiraSkia.Dibuja` hace **las mismas llamadas a libSkiaSharp** que `SKCanvas.DrawText(string, x, y, SKFont, SKPaint)` de SkiaSharp 3.116
  (`sk_font_text_to_glyphs` para contar y llenar en UTF-16, `sk_textblob_builder_new/alloc_run/make/delete`, `sk_canvas_draw_text_blob`,
  `sk_textblob_unref`), sin los envoltorios.
- El pincel de `Escribe` es uno por hilo, reusado: se le pone todo lo que se le ponía al nuevo (estilo, color o degradado, mezcla,
  suavizado); lo demás nunca se toca. Al acabar suelta el degradado, **también si algo lanza a medio texto** (`try/finally`, tras la
  revisión: antes de ella el degradado se quedaba en el pincel guardado hasta el siguiente texto del hilo; no cambiaba ningún punto).
- `TiraSkia` supone que `SKCanvas.DrawText(string, x, y, SKFont, SKPaint)` de **SkiaSharp 3.116.1** es exactamente esas llamadas (las firmas
  de las `DllImport`, `size_t` como `IntPtr` y la estructura de 4 punteros del búfer de la tira). Lo sostienen el banco IGUAL y el control
  negativo; **si se sube SkiaSharp, hay que volver a revisarlo** (y correr el banco).

**Prueba de igualdad**: el banco del dibujo (`nivel0.sh`, paso «banco»: 3 000 casos por semilla contra la copia congelada de la
referencia) **IGUAL en las semillas 7, 11 y 5**; y como control negativo, la tira corrida 0,25 px da **DISTINTO** (53, 49 y 71 líneas): el
banco sí mira este camino.

### 3. Los polígonos en un `SKPath` del hilo (commit `793442b`; `Nativo/Pixel.cs`)

`RellenaPoligono` y `TrazaPoligono` reusan un `SKPath` por hilo (`Reset` lo deja como uno nuevo; se le pone el mismo relleno y los mismos
puntos). Banco IGUAL en las tres semillas.

## Antes y después, con la rocola entera

`varias.sh` (en `/root/rocola-trabajo/fase11/rapido-general`): cada escena 40 s (el tramo medido, de 7 a 37 s, con las teclas de los
recorridos), con la carpeta caliente, **base** (`6c4dc1a` con la medida, `base2-aot`) y **ahora** alternados, dos vueltas cada uno (`v2`; pocas: la serie `v3`, más abajo, tiene cuatro y la dispersión).
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
→ 456, Ctrl+F3 339 → 348.) 
**Otra serie, tras la revisión** (`v3`: el programa con los arreglos de la revisión, `n6-aot`, frente a la misma base `base2-aot`; **cuatro
vueltas por lado**, alternadas; la máquina menos cargada que en `v2`, 2 a 8, así que hay el doble de cuadros en los dos lados). Mediana de
las cuatro y, entre corchetes, la menor y la mayor:

| Escena | Programa | Cuadros | p50 | p95 | p99 | máx | KB/cuadro |
|---|---|---|---|---|---|---|---|
| Pared | base | 886 [884-895] | 33,0 [32,2-33,3] | 41,0 [38,1-45,7] | 52,0 [43,3-58,3] | 67,4 [56,0-85,0] | 6,0 |
| | **ahora** | **1 106** [927-1 188] | **26,5** [25,0-30,7] | **32,6** [29,5-47,3] | **39,9** [33,9-56,1] | 69,6 [64,0-85,4] | **3,8** |
| Buscador | base | 980 [826-993] | 30,0 [29,5-34,7] | 36,7 [35,6-49,5] | 41,7 [39,7-60,2] | 73,8 [44,5-80,2] | 2,6 |
| | **ahora** | **1 223** [1 011-1 226] | **24,1** [23,9-28,6] | **29,5** [29,1-41,5] | **33,6** [32,8-49,9] | **44,4** [40,2-62,7] | **1,7** |
| Ayuda y asistente | base | 921 [732-944] | 32,0 [31,1-35,4] | 38,6 [37,6-63,0] | 45,3 [44,0-74,9] | 59,0 [53,1-108,0] | 3,2 |
| | **ahora** | **1 100** [850-1 130] | **26,8** [26,2-32,0] | **33,1** [31,7-54,2] | **37,8** [36,7-65,3] | **52,8** [47,7-84,8] | **1,8** |
| Ctrl+F3 | base | 819 [577-946] | 36,2 [31,2-53,7] | 47,4 [36,4-69,5] | 56,6 [42,1-82,3] | 73,9 [52,3-105,3] | 11,1 |
| | **ahora** | **893** [834-1 054] | **31,1** [27,8-34,5] | 50,7 [35,4-52,5] | 57,8 [38,6-66,7] | 72,9 [48,5-92,9] | **9,4** |

Lo que se puede decir con esto: **más cuadros y la mediana más baja en las cuatro escenas, con los rangos que casi no se tocan** en pared,
buscador y ayuda (cuadros +11 a +25 %, p50 −14 a −20 %), y la memoria nueva por cuadro −15 a −44 %. **Las colas (p95, p99, máx) no son
concluyentes en todas**: mejoran en buscador y ayuda, en la pared p95 y p99 mejoran pero el máximo queda igual (67 frente a 70, dentro del
ruido; en `v2`, con 2 vueltas, había salido 132 frente a 142), y en Ctrl+F3 p95, p99 y máx quedan iguales dentro del ruido (allí la hoja se
pinta por procesador y es lo que manda). La «memoria de pantalla» total de Ctrl+F3 que en `v2` subía (5,82 → 5,98 MB en 30 s) es porque se
presentan más cuadros: por cuadro baja (18,0 → 11,0 KB en `v2`; 11,1 → 9,4 en `v3`), y en `v3` también el total (8,7 → 7,7 MB).
**Lo que no dicen**: con llvmpipe el cuadro lo marca casi entero la tarjeta por procesador (la fase «presentar»
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

La batería del nivel 0 (`Pruebas/RocolaOs/nivel0.sh`) sobre el código de `f85de5b` (lo que va después son solo los guiones y este
documento), en dos tandas: `n0` (de `previo` a `ventanas`, 21-11-2026 03:08-04:19, carga 14-20) y `n2` (escenas, NativeAOT, cierre e
importador, 06:15-07:10, carga 4-8), con la base (`6c4dc1a`, compilada de `base-src`) corrida con las mismas escenas al lado (`nb`) para
separar lo que ya fallaba de lo nuevo. Carpetas en `/root/rocola-trabajo/fase11/rapido-general` (`n0`, `n2`, `nb`, `n3`, `n4-*`, `rv`; los programas y las salidas grandes ya borrados) y
`/root/rocola-trabajo/fase11/rg-imp`.

| Comprobación | Resultado | Con la base |
|---|---|---|
| `Codigo/` con sus líneas y CRLF | BIEN: 112 archivos iguales a `69e6547` (no se tocó) | — |
| `ganchos.py` / `diff-xp.py --sin-ganchos` | BIEN: TODO BIEN, 49 ganchos en 19 archivos; 5135 / 4868 / 229 (las de la base) | — |
| Compilar en JIT | BIEN: 0 errores, 13 avisos (los de `avisos-base.txt`) | 13 |
| Arneses de las vistas (13) | BIEN (VistasFondo ACEPTADO: la línea de marca, como siempre) | — |
| VistasAcabadoEstilo / VistasAjustes / VistasFrontera | BIEN: 0 de 6 603; 0 de 545; 0 de 106 líneas y 0 de 153 texturas | — |
| VistasVistasB | **0 distintas de 3 108** con las dos letras; la batería lo marca MAL por **18 excepciones con DejaVu que la referencia congelada da igual** (`ref-dejavu.tsv`: las mismas 18 `capsula-*-mide ArgumentException`) | igual en `nivel0/base` y `junta-segunda` |
| Banco del dibujo | BIEN: IGUAL en las semillas 7, 11 y 5 (3 000 líneas cada una) | — |
| Cintas de la tarjeta | BIEN: 29 iguales (6 616 cuadros), 0 distintas | — |
| Recorridos (9) | 9 de 9 salen con 0; 477 de 487 texturas y 201 de 217 capas quietas. **ctrlf3** MAL con las mismas cinco capas que ya salen en `rapido-caratulas` y `junta-segunda` (lo de la carga del repaso en la hoja de Sonido). **video** salió MAL en esa tanda (a la segunda también) porque a los 24 s ya no estaba la capa del video: se repitió solo (`rv`, carga 4-7) alternando con la base: **ahora 2 de 2 BIEN (50 de 50 texturas, 16 de 16 capas)**, la base 1 de 2 (una vez 35 de 50 texturas): es el tiempo con la máquina cargada, no el cambio | ctrlf3 igual |
| Ventanas | BIEN: la hoja 86, la pantalla 32 en JIT y 32 en NativeAOT, PantallaVistas 78 | — |
| Escenas en JIT (24) | IGUAL o ACEPTADO: arranque, video, buscador, cierreq, altf4, sigterm (ACEPTADO), kill9, asistente, captura, aviso, f2, f2sin, f12, f12vigia (ACEPTADO: el reloj saturado de F12). MAL **las mismas nueve que la base**: moneda, tab, f3sigterm, f2barras, f3vistas, f3grises, f3teclas, f3raton, f3billar (lo de `PORT_xp-1718.md` frente a la referencia congelada: «Modo noche (de 22 a 7 h)» en vez de 23, el ecualizador del dueño en Ctrl+F3; `cielo-nucleo/nivel0-base` da el mismo `moneda`, 52 183 puntos). **salirq**: «solo cuentas», en 4 de 8 vueltas: los encargos del vigía de la pantalla (`MedidasBitacora.cs:129`, uno cada 25 ms más lo que tarda la pantalla en atenderlo) y el reloj de 42 ms (`Form1.cs:724`) llegan **más veces en lo nuevo que en la referencia** (N0 300-348 encargos frente a R0 287-298; la base 291-301): la pantalla contesta antes y caben más vueltas en los 11 s; la lógica es la misma (las mismas cosas en el mismo orden) | las nueve igual; salirq 6 de 7 BIEN |
| NativeAOT | BIEN: publica con 0 avisos IL/AOT (14 MB); escenas arranque, cierreq, kill9, buscador y f12 IGUAL; f3vistas y f3teclas MAL como en `rapido-caratulas` (lo mismo de arriba) | igual |
| `cierre_rocola_os.sh` | BIEN: 43 de 43 en JIT y en NativeAOT | — |
| `Pruebas/Importa` | BIEN: 145 de 145 en JIT y en NativeAOT (`rg-imp`). En `n2` dio 48 MAL en los dos: **trampa de la prueba, no del programa**: con la carpeta de trabajo dentro de `rapido-general`, la rocola de XP (`MusicFolder`, `Form1.Datos.cs:1987`: busca una carpeta «musica» hasta 3 niveles arriba de los datos) encontró la biblioteca inventada de esta rama (`rapido-general/musica`, 13 119 canciones) en vez de las seis de la prueba. Fuera de esa carpeta, todo bien | — |


### Otra vez, tras los arreglos de la revisión (`a77cd74`, `n7` y `n8`, 21-11-2026 08:55-09:20, carga 4-6)

`previo`, `codigo`, `ganchos.py`/`diff-xp.py`, `compila` (0 errores, 13 avisos), `banco` (IGUAL en las semillas 7, 11 y 5), `cintas` (29
iguales, 6 616 cuadros), `ventanas` (la hoja 86, la pantalla 32 en JIT y en NativeAOT, PantallaVistas 78), `aot` (0 avisos IL/AOT) y las
escenas arranque, buscador, cierreq, altf4 y kill9 IGUAL y sigterm ACEPTADO en JIT; arranque, buscador y cierreq IGUAL en NativeAOT.
f3vistas MAL como en la base (lo de `PORT_xp-1718.md`, arriba). salirq: «solo cuentas» las dos veces (abajo). En `n8`: `cierre_rocola_os.sh` **43 de 43 en JIT y en NativeAOT**, y
las siete escenas de NativeAOT: arranque, cierreq, kill9, buscador y f12 IGUAL; f3vistas y f3teclas MAL como en la base y en
`rapido-caratulas` (lo mismo de arriba). (En `n7` el paso del cierre salió 0 de 0 por la prueba: la lista ESCENAS que se le dio a la
batería la heredó `cierre_rocola_os.sh`, que no conoce esas escenas.)

## Para la junta: la escena salirq

**salirq sale «solo cuentas» más a menudo con esta rama** (4 de 8 vueltas en `n2`, 2 de 2 en `n7`, frente a 1 de 7 de la base), y la
batería, tras repetirla una vez, la marca MAL. Es siempre la misma cuenta, `encargo MedidasBitacora.cs:129`, y **siempre de más** en el
programa: 345 y 362 frente a 306 y 329 de la referencia en `n7` (holgura 12). Es el vigía de la pantalla de XP (`Codigo/MedidasBitacora.cs`,
`Bucle`): duerme su paso, encola un encargo a la pantalla y espera a que se atienda; **cuanto antes contesta la pantalla, más vueltas caben**
en los 11 s de la escena. Con esta rama la pantalla compone menos por cuadro y contesta antes. Las cosas pasan en el mismo orden y con los
mismos estados (la secuencia y los estados de la escena, iguales). **No se cambió la tolerancia** (`Pruebas/RocolaOs/clasifica.py` es de la
batería de todos): quien junte puede aceptar en salirq ese encargo cuando el programa da más, nunca menos, o repetirla. No es un fallo nuevo.

## Lo que queda en el disco

Se borró lo que ya no servía (los programas viejos `base-jit`, `base-aot`, `n4-*`, `n5-aot`, las carpetas `n0`, `n2`, `nb` y `rg-aot` y las salidas grandes de `n7` y `n8`: unos 430 MB).
Quedan en `/root/rocola-trabajo/fase11/rapido-general` las medidas (`r/`, 11 MB), la base y su medida (`base-src`, `base2-aot`), los resúmenes de `n7` y `n8`, lo nuevo
(`n6-aot`), las carpetas de datos (`d0`, `dcal`) y la biblioteca inventada (`musica`, 19 MB). **Cuidado con `musica`**: la rocola de XP
busca una carpeta «musica» hasta 3 niveles arriba de sus datos (`MusicFolder`, `Form1.Datos.cs:1987`), así que cualquier prueba con su
carpeta de datos dentro de `rapido-general` encuentra esas 13 119 canciones (eso hizo fallar `Pruebas/Importa` en `n2`). Las medidas la
necesitan donde está (`d0` y `dcal` no llevan la suya); para otras pruebas, una carpeta de trabajo fuera de `rapido-general`.

## Lo no probado

- Nada en el A4 (ni la Radeon por KMS ni su procesador): ni cuánto ahorra allí no mezclar las dos capas, ni los cuadros.
- El video (libmpv): no se midió (la biblioteca inventada no tiene videos); el compositor deja la capa del video como antes (es de textura
  y no tapa la pantalla entera salvo a pantalla completa, donde lo de debajo no se ve igual que antes).
- Con `gpu=0` (el dibujo por procesador): la capa de la ventana sí se pinta y se compone como antes (no está vacía); no se midió.
