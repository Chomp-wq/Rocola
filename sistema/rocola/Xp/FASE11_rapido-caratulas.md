# Fase 11, rendimiento: las carátulas de la música local, instantáneas (rama `rapido-caratulas`)

20-11-2026. Sobre `85c931b` (la rama principal). Petición del dueño del 20-11-2026 («muchas mejoras [de rendimiento], priorizando [...] la
carga de las carátulas en general para que sean instantáneas, por lo menos las que pertenecen a la música local»). Lo empezó un agente que
se cortó por el límite de uso (su commit `5de0e44`); lo siguió otro desde ahí. **Nada se probó en el A4**: los números de aquí son de una
máquina de 4 núcleos compartida con otros agentes (carga 8 a 15) y de llvmpipe, no de la Radeon del A4; sirven para comparar antes y después
en las mismas condiciones, no para decir cuánto tarda allí.

## Las reglas que se siguieron

- `Codigo/` es la rocola de XP con solo la traducción: no se le cambió la lógica (el regulador de las carátulas, los perfiles de carga de
  Ctrl+F3, el lector único en dos núcleos, la cola alta y la baja, el colchón de páginas, los topes de la caché). Lo que se hizo es de dos
  clases:
  1. **en `Nativo/`, automático**: la traducción hace lo mismo más rápido, con **los mismos bytes** (cada uno con su prueba de igualdad);
  2. **de Rocola OS** (`RocolaOs/RocolaOs.Caratulas.cs`, la función `caratulas-rapidas`, encendida de fábrica con `ROCOLA_OS` y sin
     interruptor en ninguna ventana porque no cambia el aspecto: decisión del 15-9-2026), con ganchos de una línea en `Codigo/` (g220 y
     g221, del rango g220-g234 de esta rama). Sin `ROCOLA_OS` (o con `caratulas-rapidas=0` en `rocola_os.txt`) cada gancho hace lo de XP.
- Lo que cambiaría la imagen, aunque sea poco, **no se hizo** sin el dueño: fue como propuesta. El 21-11-2026 el dueño eligió las dos
  primeras (§5 y §6): cada una con su interruptor en `rocola_os.txt` (`portadas_dct=` y `portadas_dos_lectores=`, claves de la misma
  función `caratulas-rapidas`, **encendidas de fábrica**, valen al momento), con los ganchos g222-g224. Con las dos en 0 la rama sale
  como antes de ellas (las huellas del banco, iguales) y sin `ROCOLA_OS` es XP.

## El camino de una portada (lo que se midió)

1. La pared pide las 18 de la página (cola alta) y, a los 120 ms quieta, las del colchón (dos páginas a cada lado, cola baja); la de al lado
   ya la pide al cambiar de página (`PrimeNextPage`).
2. **Un** lector (`CoverPump`; `CargaTrabajo.Lectores(2)` = 1 en el A4) toma cada una: si hay miniatura guardada (`rocola_miniaturas`, PNG de
   256×256) la lee y la decodifica (≈1,7 ms en total aquí, con la previa de 32 px, los tonos y la paleta); si no, lee la original, la
   decodifica entera, la reduce a 256 con los mipmaps de Skia (el `HighQualityBilinear` de GDI+) y **guarda la miniatura en PNG** antes de
   pasar a la siguiente.
3. La pantalla, en cada cuadro, sube las que llegaron dentro del presupuesto del regulador de XP (`CoverGovernor`: con cuadros caros, una
   pieza visible cada 66 ms).

**Lo que costaba cada parte** (banco `--banco-caratulas ... partes`, NativeAOT, un núcleo, 300 portadas inventadas; y las 20 más grandes,
de 2 900 a 3 000 px):

| Parte | p50 (300) | p95 (300) | p50 (las 20 grandes) |
|---|---|---|---|
| leer la original | 0,05 ms | 0,30 ms | 0,62 ms |
| decodificarla (Skia, libjpeg-turbo) | 5,9 ms | 88,5 ms | 57,6 ms |
| reducirla a 256 (mipmaps) | 2,1 ms | 9,4 ms | 10,1 ms |
| **guardar la PNG de XP (zlib 6, cinco filtros)** | **41,1 ms** | **58,6 ms** | **36,6 ms** |
| leer la PNG guardada (decodificar) | 1,4 ms | 3,1 ms | 1,0 ms |
| la copia para la tarjeta | 0,03 ms | 0,05 ms | 0,05 ms |

**Los cuellos de botella, por lo que se nota:**

1. **Sin miniatura guardada** (biblioteca nueva, discos nuevos, la caché borrada): guardar la PNG era el 70 % de cada portada nueva y el
   lector no pasa a la siguiente hasta guardarla. Una página nueva tardaba **1,4 a 2 s** (y al hojear a 1,5 s por página, la mayoría no
   llegaba a tener sus portadas antes de la siguiente).
2. **Saltar de letra** (REGRESAR con izquierda o derecha): XP adelanta las páginas vecinas, pero no la primera página de la letra de al lado
   (solo la de después, desde la última página): llegaba sin portadas (en caliente, 120-140 ms; sin miniatura, segundos).
3. Decodificar las originales grandes (60-90 ms): sin cambiar los puntos no se puede abaratar (ver propuestas).
4. Lo demás (leer la miniatura guardada, la copia para subirla) ya era de milisegundos o menos.

## Lo hecho

### 1. `Nativo/Imagenes.cs` y `Nativo/Mapas.cs`: leer sin copias de más (commit `5de0e44`, el primer agente; automático)

La lectura de una imagen (`Image.FromStream` de XP) usaba dos decodificadores y dos copias de la imagen entera, y la de un `Stream` copiaba
los bytes dos veces a arreglos del montón grande. Ahora un solo decodificador sobre `SKData` (memoria nativa) y directo a BGRA premultiplicado
si Skia ya decodifica así; `Mapas.Copia` de un mapa entero, fila a fila sin lienzo. **Prueba de igualdad:** `Pruebas/RapidoCaratulas/LeeIgual`
(cada imagen de la biblioteca inventada y unas raras —paleta, gris, alfa, 1×1, basura— por `byte[]`, por `Stream` y por un `Stream` que no se
puede mover, y `Mapas.Copia` con cinco trozos, contra el código de la base; desde la revisión, commit `7d5b02c`, también PNG entrelazadas
—y una entrelazada cortada—, mapas con premultiplicado inválido —color mayor que el alfa, escrito a mano— por `Mapas.Copia`, que también
usa la capa de la interfaz, y `Stream`s que dicen otro largo del que dan): **21 663 comparaciones, 0 distintas** (21-11-2026, sobre
`7d5b02c`). `Lee(Stream)` con un `Stream` que da menos o más de lo que dice su `Length` vuelve a lo de siempre (copiar hasta el final),
como la base.

### 2. g220: la miniatura nueva en PNG con zlib 1 (commit `5de0e44`; Rocola OS)

`SaveThumb` (`Codigo/Form1.Biblioteca.cs`) guarda con el filtro «Up» y zlib 1 en vez de lo de fábrica de Skia: **3,3 ms en vez de 39,9** por
miniatura (banco de PNG, 60 miniaturas, mediana), el archivo un 20 % más grande (108 KB en vez de 89) y, al leerla, **los mismos puntos**
(PNG no pierde nada; lo comprueban las huellas del banco). Las miniaturas que ya están se leen igual: no se rehace ninguna.
Si `ROCOLA_IA_DATOS` está puesta pero no se puede usar (`DatosGrandes.cs`: lo grande cae en la carpeta de datos, `datos.img` de 512 MB),
g220 hace lo de XP (`DatosGrandes.CayoEnDatos`, commit `7d5b02c`): ahí no se gasta el 20 % de más. Sin `ROCOLA_IA_DATOS` (fuera de Rocola
OS, donde el lanzador siempre la pone) sí lo gasta en la carpeta de datos.

### 3. `Nativo/Pixeles.cs`: la copia sin premultiplicar de cuatro en cuatro (commit `2c93736`; automático)

Cada subida a la tarjeta (portadas, casillas, interfaz: `D3D9Star.Upload` y `TexturasCompatiblesGpu`) pasa los puntos a «sin premultiplicar»
con una tabla, punto a punto. Ahora los grupos de cuatro puntos con alfa 0 o 255 (casi todo en una portada o en la interfaz) van tal cual con
`Vector128` (SSE: el A4 no tiene AVX2), y los demás con la tabla de siempre; lo mismo al premultiplicar con los opacos. **Prueba de igualdad:**
`Pruebas/RapidoCaratulas/PixelesIgual` (11 anchos, 6 clases de mapas, 3 trozos, los 3 formatos, `Lee` y `Escribe`, contra el `Pixeles.cs` de
la base): **1 188 comparaciones, 0 distintas**. Lo que tarda (NativeAOT, medianas de 41):

| Mapa | Antes | Ahora |
|---|---|---|
| portada opaca 256×256 | 0,037 ms | 0,018 ms |
| 1024×768, casi todo 0/255 con algo a medias | 1,106 ms | 0,555 ms |
| 1024×768 casi transparente con un 10 % de puntos a medias al azar | 1,515 ms | 1,721 ms |
| 256×256 con alfa al azar (lo peor) | 0,104 ms | 0,115 ms |

Es poco por portada; donde más ahorra es en las capas de la interfaz que se suben enteras. En el peor caso (puntos a medias repartidos al
azar, que no es lo que pinta la rocola) es un 10-14 % más lento.

Desde la revisión (commit `7d5b02c`), también una **interfaz dibujada** de 1024×768 (texto con antialias, sombras, degradados, sobre
transparente): 21-11-2026, sobre `7d5b02c`, NativeAOT en un núcleo, cuatro corridas (carga 4,8): **antes 1,114 / 1,238 / 1,276 / 1,191 ms,
ahora 1,161 / 1,254 / 1,329 / 1,244** (medianas de 41): de 1 a 4 % más lenta (0,02-0,05 ms por subida entera), a la par del ruido de la
medida; la «casi transparente con un 10 % a medias al azar» 13-17 % más lenta (1,36-2,03 → 1,60-2,26 ms) y la portada opaca, la mitad
(0,036-0,053 → 0,018-0,029 ms). Los mismos bytes en todos: **1 200 comparaciones, 0 distintas**. No se midió con capas sacadas de la rocola
de verdad; se deja porque en lo que más se sube (portadas, y capas casi todas 0 o 255) gana el doble y en lo dibujado pierde a lo más un 4 %.

### 4. g221: las letras de al lado, adelantadas (commit `a97c2f4`; Rocola OS)

En `CoverSettle_Tick` (`Codigo/Form1.Portadas.cs`), después de lo de XP, se encargan también **la primera página de la letra de antes y de
la de después** (a donde lleva REGRESAR con izquierda o derecha), en la **misma cola baja** que el colchón de XP: el lector único de XP las
atiende cuando no hay nada visible, con sus mismos topes, y la caché de memoria de XP (220 o 1 000) las guarda como a las demás. Son 36
portadas como mucho por parada. Recorre **la misma rueda que `GotoLetter`** (`RocolaOsSpotifyCatalogoOrden(LetrasOrden)`, el orden de g46),
así que adelanta justo las letras a las que lleva REGRESAR+izquierda/derecha: con el modo Spotify y su letra (♫) después de VIDEO, la de al
lado de VIDEO es ♫ y la de al lado de ♫ son VIDEO y la primera letra con discos; ♫ misma no se adelanta (no es música local) pero tampoco se
salta; en una pared del buscador (orden vacío), nada. (Arreglo de la revisión, commit `7d5b02c`: antes recorría `LetrasOrden` a secas y, con
♫ al lado, adelantaba la letra equivocada —la de la estrella o VIDEO, 18 portadas de más en la cola baja— y desde ♫, ninguna.)

### 5. g222: la JPEG grande, decodificada ya reducida (escala DCT; `portadas_dct=`, elegida por el dueño el 21-11-2026)

La primera vez de cada portada XP descomprime la original **entera** (una de 3 000 px: 9 millones de puntos) y la achica a 256 con los
mipmaps. En `DecodeCoverCore` (`Codigo/Form1.Portadas.cs`, la línea de `Imagenes.Lee` de la original) el gancho pasa por
`RocolaOsLeeOriginal`: si es una JPEG en color o en gris y sus dos lados miden al menos 8/7 de **2 × 256 = 512**, libjpeg-turbo la
decodifica a la escala M/8 (M de 1 a 7) **más chica que deje los dos lados en 512 o más** (`SKCodec.GetPixels` con la medida de
`GetScaledDimensions`; nunca más chica que lo que se pinta, y con el doble de margen); de ahí sigue **el camino de XP sin cambiar nada**:
la reducción a 256 con `HighQualityBilinear` y medio punto (los mipmaps), el premultiplicado (`Mapas.Nuevo`, BGRA premultiplicado; la
opaca con su formato `Rgb24`, como `Imagenes.Lee`), «Carátulas en JPEG» (`HazOpaca`), la previa, los tonos, la paleta y **la miniatura que
se guarda, que es la misma** que se publica. PNG, GIF, lo que Skia no decodifica en BGRA o en gris y las JPEG de menos de 586 px en su lado menor (ni 7/8 dejaría 512): lo de siempre. Después de la
primera vez se lee la miniatura guardada, como siempre (no se rehace ninguna de las que ya están).

**Por qué 2 × 256** (`rocola-xp --banco-dct`, `Pruebas/RapidoCaratulas/BancoDct.cs`: las 246 JPEG de las 300 portadas de la muestra,
NativeAOT en un núcleo, 21-11-2026). Para cada portada, la miniatura de XP (entera) contra la de la DCT con el margen 1,5, 2, 3 y 4 veces
256, y las dos contra una **referencia**: la entera achicada a 256 por el promedio exacto de lo que cubre cada punto (lo que daría un filtro
ideal), sin el marco de 3 puntos (donde XP mezcla el borde con lo transparente a propósito):

| Margen | Se aplica a | Diferencia con XP: media (mediana / peor portada) | p99,9 (mediana / peor) | Contra la referencia: XP media · p99,9 peor | DCT media · p99,9 peor |
|---|---|---|---|---|---|
| 1,5 × 256 | 232 | 0,77 / 1,39 | 14 / 54 | 0,572 · 32 | 0,555 · **50** |
| **2 × 256** | 179 | 0,60 / 1,06 | 11 / 41 | 0,612 · 32 | **0,520 · 29** |
| 3 × 256 | 115 | 0,51 / 0,84 | 11 / 36 | 0,714 · 32 | 0,565 · 29 |
| 4 × 256 | 76 | 0,40 / 0,82 | 9 / 42 | 0,786 · 32 | 0,623 · 28 |

(de 0 a 255 por canal; «peor»: la peor portada). La miniatura de la DCT **no es igual** a la de XP (media de 0,6 de 255; en algún punto de
un borde fuerte de estas portadas inventadas, hasta 111), pero con el margen de 2 o más **se aparta de la referencia lo mismo o menos que
la de XP** (con 2: 0,52 contra 0,61 de media y 29 contra 32 en el p99,9 de la peor): no es peor, es otro redondeo del achicado (los mipmaps de
XP desde otra medida). Con 1,5 el p99,9 de la peor portada ya es peor que XP (50 contra 32): por eso el margen es 2 (`RocolaOsDctVeces`).
Lo que tarda decodificar y achicar (ms, p50 / p95), por clase, con el margen 2:

| JPEG | n | XP (entera) | DCT | Contra la referencia, media: XP · DCT |
|---|---|---|---|---|
| en color de más de 2 200 px | 22 | 56,5 / 109,2 | **17,5 / 61,3** | 0,970 · 0,458 |
| en color de 1 450 a 2 200 px | 21 | 30,1 / 53,6 | **15,8 / 31,6** | 0,795 · 0,445 |
| en color de 586 a 1 450 px | 127 | 8,5 / 16,6 | **5,9 / 8,3** | 0,580 · 0,536 |
| en gris (hasta 1 450 px) | 9 | 3,9 / 4,2 | 3,4 / 5,3 | 0,509 · 0,457 |

(las grandes que siguen lentas con la DCT son las progresivas: el 40 % de las grandes de la biblioteca inventada; la escala ahorra la
transformada, no leer los coeficientes).

### 6. g223 y g224: dos lectores mientras la página que se ve está sin miniaturas (`portadas_dos_lectores=`, elegida por el dueño el 21-11-2026)

En dos núcleos XP pone **un** lector de portadas (`CargaTrabajo.Lectores(2)` = 1: «no quitarle procesador a la pantalla»; la decisión del
1-10-2026 de no pasar de los límites de dos hilos, que esta elección del dueño cambia solo aquí). Ahora:

- **g224** (`CoverPump`, la línea de `DecodeCoverForPublication`): antes de decodificar, si la portada no tiene miniatura guardada (va de
  la original), no está en un disco con cabezal (`DiscoLento`: ahí XP deja un lector por unidad) y la **fila alta** (lo que se ve) tiene más
  portadas esperando, se marca «la página que se ve está sin miniaturas» y se llama a `EnsureCoverReaders` de XP, que abre el segundo
  lector. El lector que decodifica cuando ya hay tantos decodificando como deja XP va a **nice 19** (por debajo del repaso de XP, nice 4:
  solo toma el procesador que dejan libre la pantalla, el sonido y el lector de XP) y vuelve a su prioridad de XP (`PrioridadHilo.AlArrancar`)
  en cuanto decodifica dentro del cupo de XP.
- **g223** (`AdaptiveCoverReaders`, el cupo de lectores que usan `TakeCoverJob`, `WantedCoverReaders`, `EnsureCoverReaders` y la espera del
  lector, siempre bajo `coverLock`): mientras esté la marca, uno más que lo de XP (como mucho tantos como núcleos); en cuanto la fila alta
  se vacía (la página ya se repartió) se quita la marca y vuelve el cupo de XP: el de más acaba su portada y se va con las reglas de XP
  (sobra un lector: no toma trabajo y sale).
- **Nunca dos lecturas del mismo archivo**: cada portada es un trabajo de la cola de XP (`coverLoading`: una ruta, un trabajo; el que se ve
  pasa de la fila baja a la alta sin duplicarse), el segundo lector toma de la misma cola. El calentador de XP se aparta mientras haya
  portadas pendientes (`YieldCoverWork`) y el semáforo de XP (`coverSem`, 2 turnos) ya tenía sitio para dos.
- Lo que no se cambió: el regulador de XP (la pausa de los lectores bajo presión, `ReaderPause`, vale para los dos), los perfiles de
  Ctrl+F3, la cola, las cachés. Lo que puede pasar: si el de nice 19 tiene el turno de una portada **enorme** (más de 16 Mpx,
  `CaratulaEnormeCerrojo`), el lector de XP espera a que acabe (con la DCT, mucho menos que antes); no se vio en las corridas.

## Antes y después, con la rocola entera

`e2e2.sh` (en `/root/rocola-trabajo/fase11/rapido-caratulas`, fuera del repositorio): la rocola en NativeAOT fuera de pantalla a 1024×768
con la biblioteca inventada (2 000 discos, 1 916 con portada: 674 JPEG de 500-700 px, 471 de 1 000-1 400, 285 de 1 500-3 000 —el 40 %
progresivos—, 220 PNG opacas, 93 PNG con alfa, 74 JPEG en gris y 99 con otro nombre; 585 MB), en los **núcleos 0 y 1** (dos, como el A4) y
los hilos de llvmpipe (lo que en el A4 hace la tarjeta) en el 2 y el 3; las teclas por `ROCOLA_PULSA`; la medida por la función de prueba
`medir-caratulas` (`Pruebas/RapidoCaratulas/Mide.cs`): por cada página, cuándo tiene sus 18 portadas en la caché de memoria («en memoria») y
cuándo las dio por subidas el regulador de XP («subidas»). Una página que no llegó antes de la siguiente cuenta con lo que estuvo a la vista.
Las tres configuraciones alternadas, con el candado de lo pesado: **base** (`85c931b`), **xp** (esta rama sin `ROCOLA_OS`: solo lo de
`Nativo/`) y **ros** (esta rama con `caratulas-rapidas`). Las miniaturas, guardadas y fuera de la caché del sistema (`posix_fadvise`), salvo
en «sin miniaturas».

Medianas (p50) y p90 de todas las páginas de todas las vueltas, en ms desde que cambia la página; «sin llegar»: cuántas no tuvieron sus
portadas antes de la siguiente; los cuadros, la mediana de los p95 y p99 de cada 5 s. Carga de la máquina durante las corridas: 8 a 15 (en
4 núcleos, con otros agentes compilando y corriendo sus pruebas).

| Escena | Config. | Vueltas (páginas) | En memoria p50 / p90 (sin llegar) | Subidas p50 / p90 (sin llegar) | Cuadros p95 / p99 |
|---|---|---|---|---|---|
| **Saltar de letra** (REGRESAR+derecha cada 2,5 s, 16 veces; miniaturas guardadas) | base | 3 (48) | 142 / 325 (0) | 392 / 1 389 (0) | 83 / 109 |
| | xp | 3 (48) | 122 / 200 (0) | 323 / 1 074 (0) | 81 / 107 |
| | **ros** | 3 (48) | **0 / 0** (0) | 218 / 1 318 (0) | 82 / 114 |
| **Hojear hacia adelante** (ARRIBA cada 1,5 s y un salto de letra cada 8 páginas, 30 pasos; miniaturas guardadas) | base | 2 (60) | 0 / 0 (0), máx. 348 | 696 / 1 617 (11) | 168 / 325 |
| | xp | 2 (60) | 0 / 0 (0), máx. 458 | 98 / 1 311 (3) | 131 / 216 |
| | ros | 2 (60) | 0 / 0 (0), máx. 0 | 140 / 1 454 (8) | 131 / 271 |
| **Hojear sin miniaturas guardadas** (la primera vez, 20-11-2026; abajo, la repetición; la misma escena; la biblioteca como nueva: `rocola_miniaturas` y las previas apartadas en cada corrida) | base | 4 (120) | 1 300 / 1 723 (51) | 1 300 / 1 744 (52) | 88 / 167 |
| | xp | 4 (120) | 0 / 1 591 (26) | 132 / 1 591 (27) | 71 / 95 |
| | **ros** | 4 (121) | **0 / 477** (2) | 51 / 1 320 (12) | 61 / 72 |

**La escena sin miniaturas NO es concluyente** (lo vio la revisión). Por vuelta, la mediana de «en memoria» de esa tabla: base 478, 590,
1 541 y 1 064 ms; xp 1 545, 0, 0 y 0; ros 0, 0, 0 y 0, con la carga de 2,8 a 14,8: dentro de una misma configuración va de 0 a 1 545. Y el
banco de abajo da casi el mismo frío para base y xp (16,5-17,0 s contra 15,2-15,7 s las 300 portadas: un 5-8 %), así que el «1 300 → 0» de
base a xp no se le puede achacar a la lectura sin copias de más. Se repitió (21-11-2026, sobre `7d5b02c`, `serie3.sh frio3`, cinco vueltas
alternadas, la carga de 3,4 a 12,0 —más baja que la primera vez— y contando cuántas miniaturas había hecho la rocola al acabar, lo de la
página y lo del calentador de fondo de XP):

| Config. | Mediana de «en memoria» por vuelta (ms) | Sin llegar por vuelta (de 30) | Miniaturas hechas en 58 s por vuelta | Todas: p50 / p90 (sin llegar) |
|---|---|---|---|---|
| base | 0, 0, 938, 718, 0 | 1, 4, 11, 7, 1 | 612, 614, 571, 590, 629 | 182 / 1 504 (24) |
| xp | 0, 105, 0, 212, 144 | 0, 5, 0, 1, 0 | 657, 612, 661, 637, 638 | 0 / 1 230 (6) |
| ros | 0, **1 506**, 0, 0, 0 | 0, **26**, 0, 0, 0 | 698, **448**, 698, 695, 698 | 0 / 1 506 (26) |

Lo que sí se ve: es **de todo o nada**. Si el lector único de XP va por delante del paseo (una página cada 1,5 s, con el colchón de dos
páginas en la cola baja), cada página llega ya en memoria (0 ms); si se queda atrás al principio (la primera página tardó más), ya no
alcanza y cada página se ve casi entera sin portadas (≈1,5 s). Por eso las medianas por vuelta saltan de 0 a 1 500 en la misma
configuración, y cinco vueltas no bastan para separar base de xp: xp hizo un 4-6 % más miniaturas que base en el mismo tiempo (lo mismo que
dice el banco) y se quedó atrás menos veces, pero no es una diferencia firme. ros hizo 695-698 (lo que pide la escena, o casi) en cuatro
vueltas de cinco y en ellas fue siempre por delante; en la otra (ros-2: 448, carga 4,5) se quedó atrás desde la segunda
página; esa vuelta y base-3 (la de base que más se quedó atrás) son las dos únicas de las quince con una «Pantalla parada» al arrancar
(572 ms en ros-2, en un cuadro de la tarjeta), lo que apunta a la máquina compartida más que al cambio. Lo firme
es el banco de abajo (guardar 38 → 3,3 ms con las mismas huellas); lo de la rocola entera sin miniaturas, solo una tendencia. Con las
miniaturas guardadas (las dos primeras escenas) xp y ros solo se separan al saltar de letra (g221).

**Lo que no dicen estos números.** Las «subidas» las manda el regulador de XP con los cuadros de llvmpipe (47-65 ms de trabajo: con más de
30 ms el regulador se pone «bajo presión» y deja una portada visible cada 66 ms), así que aquí casi todo lo que tarda una página después de
tener sus portadas en memoria es eso, y no se puede comparar con el A4 (con la tarjeta, cuadros mucho más baratos). Lo que sí es de aquí y
de allí es «en memoria»: lo que tarda el lector.

## Banco de las portadas (sin la pantalla)

`bancos.sh 2` (en `/root/rocola-trabajo/fase11/rapido-caratulas`): `rocola-xp --banco-caratulas` con 300 portadas de la biblioteca
inventada (la muestra fija `muestra300.txt`), NativeAOT, en **un** núcleo (`taskset -c 1`), dos vueltas alternadas de las tres
configuraciones; «frío»: sin miniatura (decodificar la original, reducirla, la previa, los tonos y guardar la PNG); «guardada»: con la
miniatura guardada y sacada de la caché del sistema. Carga de la máquina antes de cada corrida: 6,9 a 10,8. En ms por portada (p50 / p95, y
la suma de las 300 en s):

| Modo | Parte | base (vuelta 1 · 2) | xp (1 · 2) | ros (1 · 2) |
|---|---|---|---|---|
| frío | decodificar y reducir | 7,3 / 77,8 · 7,5 / 86,0 | 6,5 / 54,1 · 6,2 / 50,4 | 6,4 / 54,8 · 6,3 / 52,3 |
| frío | guardar la miniatura | 37,7 / 46,9 · 38,3 / 46,1 | 38,6 / 47,1 · 38,1 / 45,3 | **3,3 / 4,1 · 3,4 / 4,3** |
| frío | **total** | 46,9 / 113,5 (16,5 s) · 48,1 / 124,5 (17,0 s) | 47,1 / 92,2 (15,7 s) · 45,8 / 87,5 (15,2 s) | **10,0 / 58,4 (5,0 s) · 10,3 / 56,0 (5,1 s)** |
| guardada | total | 2,25 / 5,16 (0,76 s) · 1,76 / 2,24 (0,55 s) | 1,67 / 2,32 (0,55 s) · 1,59 / 2,67 (0,55 s) | 1,76 / 2,02 (0,54 s) · 1,78 / 2,44 (0,59 s) |

**Las huellas** (de cada portada: la miniatura, la previa de 32 px, los tonos y la paleta; y en «guardada», lo leído de la miniatura) de
las seis corridas, **iguales** a las de base-1 en los dos modos (sin la columna del archivo guardado, que en ros es otro PNG con los mismos
puntos).

## Lo que se probó y no ganó (y no se dejó)

- **PNG sin comprimir (zlib 0)** para las miniaturas de Rocola OS: decodificar 0,41 ms en vez de 1,1 (y 3,3 al guardar), pero 256 KB por
  miniatura en vez de 108 (2,4 veces el disco: unos 500 MB con 2 000 discos) y leerla del disco, el doble; unos 10 ms por página en total.
  No compensa. zlib 3 con «Up»: 0,87 ms al leer y 8 ms al guardar; tampoco.

## Propuestas (cambian la imagen o necesitan al dueño)

1. ~~Decodificar ya reducido (escala DCT)~~ y 2. ~~Dos lectores cuando la página que se ve está sin miniaturas~~: **elegidas por el dueño el
   21-11-2026 y hechas** (§5 y §6).
3. **Las letras de al lado ya subidas a la tarjeta**, no solo en memoria (como hace XP con la página siguiente, `WarmAheadCells`): necesita
   tocar la lista de adelanto de la pared de XP o un horno propio; no se hizo.

## Las comprobaciones

`nivel0.sh` entero sobre `a97c2f4` (el código de esta rama; después solo cambió este documento, los guiones y un comentario), 89 min con la
carga de la máquina de 8 a 10 (en `/root/rocola-trabajo/fase11/rapido-caratulas/n0`, resumen en `n0.out`): **16 BIEN, 5 ACEPTADO, 4 MAL**.

| Paso | Resultado |
|---|---|
| código | BIEN: 112 archivos con las mismas líneas, CRLF y último byte que en `69e6547` |
| `ganchos.py` | BIEN: TODO BIEN, 51 ganchos en 19 archivos (g220 y g221 los de esta rama) |
| `diff-xp.py --sin-ganchos` | BIEN: 5 135 líneas quitadas, 4 868 puestas y 229 sin marca, **las mismas que la base** |
| compilar JIT | BIEN: 0 errores, 13 avisos, los de `avisos-base.txt` (0 nuevos) |
| NativeAOT | BIEN: 0 avisos IL/AOT (14 MB) |
| banco del dibujo (semillas 7, 11 y 5) | BIEN: IGUAL las tres (3 000 líneas cada una) |
| cintas de la tarjeta | BIEN: 29 iguales (6 616 cuadros), 0 distintas |
| recorridos | 9 de 9 salieron con 0; atracción, botones y video BIEN; ayuda, buscador, hojas, pared y pared2 ACEPTADO (lo de siempre: las texturas de la hora y el ritmo, y la hoja de Sonido con lo que lleva medido el repaso); **ctrlf3 MAL** (5 capas de la hoja de Sonido) |
| escenas NativeAOT | arranque, cierreq, kill9, buscador y f12 IGUAL; **f3vistas y f3teclas MAL** (la capa de la hoja de Sonido y `rocola_config.txt`) |

**Los MAL no son de esta rama:** son los de la rama principal desde los cambios de XP del 17 y 18-11-2026 (`PORT_xp-1718.md`: Ctrl+F3 ->
Sonido ya no es el de la copia congelada de la referencia, `aef2211`: el modo noche con sus graves, el botón del ecualizador, sin «Presencia
nueva o antes» ni I3). La misma batería sobre la rama principal sola (`424ade7`, que solo difiere de `85c931b` en `AGENTS.md`; la corrió otra rama en `/root/rocola-trabajo/fase11/junta-segunda/n0base`) da los mismos MAL con las mismas
capas (`c32.2`, `c46`, `c8`; `s0`...`s8vuelta`; `t1`, `t2`) y los mismos tamaños.

Las pruebas propias de esta rama (`Pruebas/RapidoCaratulas`): `LeeIgual` 15 569 comparaciones, 0 distintas; `PixelesIgual` 1 188, 0
distintas; las huellas del banco de las portadas, iguales en las seis corridas. Tras los arreglos de la revisión (`7d5b02c`, 21-11-2026):
`LeeIgual` **21 663, 0 distintas**; `PixelesIgual` **1 200, 0 distintas**; `ganchos.py` TODO BIEN (112 archivos contra `69e6547`). El
nivel 0 no se volvió a correr: `7d5b02c` solo toca `RocolaOs/` (g221), `Nativo/Imagenes.cs` (un `Stream` con otro largo, que en la rocola no
llega: las dos llamadas pasan un `MemoryStream`), `Nativo/DatosGrandes.cs` (una marca) y las pruebas; sin `ROCOLA_OS` lo que se pinta es lo
mismo de `a97c2f4`.

La batería del andamio de Rocola OS (`Pruebas/RocolaOs/andamio/prueba.sh`, NativeAOT del código de esta rama, 21-11-2026; salida en `/root/rocola-trabajo/fase11/rapido-caratulas/andamio.out`): **99 BIEN y 2 MAL de 101**. Los dos MAL son de la prueba, no del programa ni de esta rama: `prueba.sh` (líneas 50 y 59) sigue esperando la base vieja de `ganchos.py` («109 archivos comparados con fc7b667», «NO queda como en fc7b667»), y desde `PORT_xp-1718.md` la base es `69e6547` con 112 archivos (lo mismo que dice el paso «ganchos» del nivel 0, BIEN). Lo demás (la orden, los ajustes de `rocola_os.txt`, las escenas sin la variable, con «0», con «1» y con la función de prueba, los cierres y el apagón), BIEN. No se tocó `prueba.sh` (no es de esta rama): queda para quien la cuide.

## Lo no probado

- Nada en el A4 (ni la Radeon por KMS ni su procesador): ni cuánto tarda allí una página, ni si el regulador de XP está «bajo presión» con la
  tarjeta, ni el SSD del bar.
- Las «subidas» con una tarjeta de verdad (aquí las manda llvmpipe).
- El buscador, la ficha, lo que suena y la cola usan la misma caché de portadas que la pared (`RequestCover`/`LoadCover`/`GetCoverYa`): lo
  hecho les sirve igual, pero no se midieron aparte.
- g221 con la letra de Spotify de verdad (el Soloist falso con discos en ♫): no se corrió; el orden es el mismo código que `GotoLetter` (arriba, §4).
