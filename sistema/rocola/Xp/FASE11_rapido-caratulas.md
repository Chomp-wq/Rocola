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
- Lo que cambiaría la imagen, aunque sea poco, **no se hizo**: va abajo como propuesta para el dueño.

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
puede mover, y `Mapas.Copia` con cinco trozos, contra el código de la base): **15 569 comparaciones, 0 distintas**.

### 2. g220: la miniatura nueva en PNG con zlib 1 (commit `5de0e44`; Rocola OS)

`SaveThumb` (`Codigo/Form1.Biblioteca.cs`) guarda con el filtro «Up» y zlib 1 en vez de lo de fábrica de Skia: **3,3 ms en vez de 39,9** por
miniatura (banco de PNG, 60 miniaturas, mediana), el archivo un 20 % más grande (108 KB en vez de 89) y, al leerla, **los mismos puntos**
(PNG no pierde nada; lo comprueban las huellas del banco). Las miniaturas que ya están se leen igual: no se rehace ninguna.

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

### 4. g221: las letras de al lado, adelantadas (commit `a97c2f4`; Rocola OS)

En `CoverSettle_Tick` (`Codigo/Form1.Portadas.cs`), después de lo de XP, se encargan también **la primera página de la letra de antes y de
la de después** (a donde lleva REGRESAR con izquierda o derecha), en la **misma cola baja** que el colchón de XP: el lector único de XP las
atiende cuando no hay nada visible, con sus mismos topes, y la caché de memoria de XP (220 o 1 000) las guarda como a las demás. Son 36
portadas como mucho por parada; la letra de Spotify no (no es música local).

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
| **Hojear sin miniaturas guardadas** (la misma escena; la biblioteca como nueva: `rocola_miniaturas` y las previas apartadas en cada corrida) | base | 4 (120) | 1 300 / 1 723 (51) | 1 300 / 1 744 (52) | 88 / 167 |
| | xp | 4 (120) | 0 / 1 591 (26) | 132 / 1 591 (27) | 71 / 95 |
| | **ros** | 4 (121) | **0 / 477** (2) | 51 / 1 320 (12) | 61 / 72 |

Por vuelta, la mediana de «en memoria» sin miniaturas: base 478, 590, 1 541 y 1 064 ms; xp 1 545, 0, 0 y 0; ros 0, 0, 0 y 0 (la carga de la
máquina al acabar cada una, de 2,8 a 14,8: la vuelta 1 de xp y la 3 de base fueron las de más carga). xp gana sin miniaturas por la lectura
sin copias de más (en las originales grandes, el p95 de decodificar y reducir baja de 78-86 a 50-54 ms en el banco de abajo); ros, además,
porque guardar cada miniatura nueva pasa de 38 a 3 ms (g220) y el calentador de miniaturas de XP (el de fondo) acaba antes. Con las
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

1. **Decodificar ya reducido (escala DCT de libjpeg-turbo)** las JPEG grandes, a la escala más chica que deje al menos 512 px, y reducir de
   ahí a 256 como siempre: en las 20 más grandes (≈3 000 px) **78,9 → 23,4 ms** por portada nueva; en las de 1 000-1 400 px, 12,7 → 5,8 ms.
   Pero los puntos no salen iguales: diferencia media 1,1 de 255 (0,7 en las medianas) y en algún punto de borde hasta 109-117. Solo con la
   elección del dueño (sería una función de Rocola OS con su interruptor), y solo para la primera vez de cada portada: después se lee la
   miniatura guardada.
2. **Dos lectores en dos núcleos cuando la página que se ve está sin miniaturas**: XP pone uno (`CargaTrabajo.Lectores(2)`) para no quitarle
   procesador a la pantalla; con un SSD el disco ya no es el límite, pero sí el procesador. Cambia la regla de XP de los «límites de 2 hilos»
   (decisión del 1-10-2026: no se pasan): no se hizo.
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
distintas; las huellas del banco de las portadas, iguales en las seis corridas.

La batería del andamio de Rocola OS (`Pruebas/RocolaOs/andamio/prueba.sh`, NativeAOT del código de esta rama, 21-11-2026; salida en `/root/rocola-trabajo/fase11/rapido-caratulas/andamio.out`): **99 BIEN y 2 MAL de 101**. Los dos MAL son de la prueba, no del programa ni de esta rama: `prueba.sh` (líneas 50 y 59) sigue esperando la base vieja de `ganchos.py` («109 archivos comparados con fc7b667», «NO queda como en fc7b667»), y desde `PORT_xp-1718.md` la base es `69e6547` con 112 archivos (lo mismo que dice el paso «ganchos» del nivel 0, BIEN). Lo demás (la orden, los ajustes de `rocola_os.txt`, las escenas sin la variable, con «0», con «1» y con la función de prueba, los cierres y el apagón), BIEN. No se tocó `prueba.sh` (no es de esta rama): queda para quien la cuide.

## Lo no probado

- Nada en el A4 (ni la Radeon por KMS ni su procesador): ni cuánto tarda allí una página, ni si el regulador de XP está «bajo presión» con la
  tarjeta, ni el SSD del bar.
- Las «subidas» con una tarjeta de verdad (aquí las manda llvmpipe).
- El buscador, la ficha, lo que suena y la cola usan la misma caché de portadas que la pared (`RequestCover`/`LoadCover`/`GetCoverYa`): lo
  hecho les sirve igual, pero no se midieron aparte.
- Con la letra de Spotify al lado, g221 no adelanta esa (no es música local) ni salta por encima de ella.
