# Arranque, audio y memoria sin cambiar lo que se oye ni lo que se guarda — Modest, 19 de octubre de 2026

Destino: AMD A4-6300 (dos núcleos), 3,5 GB de RAM, SSD, Windows XP / .NET Framework 3.5. Solo la rama `claude/modest-carson-fa2v2n`. Punto de partida `5d352aa`.
Petición: ahorros que **no son gráficos** (arranque, hilos de fondo, memoria de vida larga, ruta del audio), con los mismos bytes en los archivos de datos, el mismo
formato y el mismo audio. Nada de esto lleva interruptor: no cambia el aspecto ni la funcionalidad (regla del 15-10-2026).

**Todo lo medido aquí es sintético, de Linux/Mono con baterías de pruebas.** No hay una sola cifra de Windows XP ni del A4: lo que falta medir allí está al final,
con la línea del diagnóstico que hay que leer.

## Paso 0 — revertido el cielo de fábrica guardado (commit `d86aa2c`)

Bajaba la media de pantallas pintadas un 18 % (6,45 → 5,30 a 1024×768) pero subía el peor cuadro de 6,45 a 7,45 pantallas (≈ 40 % de los cuadros en reposo repintan
el cielo) y pedía 4/8/16 MB de memoria de la tarjeta. El problema del A4 son los tirones, que dicen p95/p99/máximo, no la media. `git revert` + documento al día
(`OPTIMIZACIONES_AUTOMATICAS_MODEST_2026-10-19.md`, sección «Descartado»). El archivo de huellas no cambia: 168 de 168 pantallas con los píxeles de `e1ae788`, chorro
bruto idéntico en 160 (las 8 que cambian son las del ahorro de las franjas bajo el estado opaco). Baterías `performance huellacuadros dibujoligero presencia cielovivo cachesgraficas
rendercache` en verde con GPU, y `cpucongelado`: 14 métodos idénticos (el dibujo por procesador, congelado, no se tocó).

## Resumen

| # | Qué | Resultado medido (Mono/Linux, sintético) | Comprobación |
| --- | --- | --- | --- |
| 2 | Firma de la biblioteca: minúscula ASCII a mano y orden por la parte del nombre | hash de las tres cadenas de cada archivo, 30.000 rutas: 80 → 12 ms; ordenar: 107 → 31 ms | `firmarapida`: misma firma letra por letra |
| 1 | Firma en CLR 2 (Windows XP): una lista por carpeta con `FindFirstFile` | **no medible aquí**; ahorra una llamada por archivo y otra por carpeta | `firmarapida` con un origen inventado; la llamada de Windows **sin probar** |
| 3 | Diagnóstico del arreglo de la canción (recolecciones de gen2) | solo diagnóstico | `enmemoria` |
| 7 | Respaldo del arranque: bytes en vez de dos SHA-256 | 30 MB de tablas: 1.150 → 90 ms de procesador por respaldo | `respaldobytes` (+ `discoacotado`, `datosdeltas`) |
| 8 | Cosecha de la IA: origen una vez, una confirmación por archivo | confirmaciones en disco por canción en estéreo 6 → 3 | `cosechaunavez`: bytes iguales al ejecutable anterior en 16 casos |
| 6 | Menos objetos de vida larga | fuentes medidas 328 → 210 bytes por canción (20.000: −2,3 MB, 60.000 objetos menos); 30.000 canciones −6 % retenido | `objetosmenos` |
| 4 | Reproducción desde la memoria con el lector ligero | −98 % de la basura del lector con un descompresor sin asignaciones; con NLayer no se nota (domina NLayer) | `reproligera`: mismo PCM muestra a muestra |
| — | Hilos de fondo que no cierran la rocola (`CoverPump`, `ClavesDelIndice`) | robustez | `hilosatrapados` |

## 2. Firma de la biblioteca: hash ASCII y orden por nombre (`fb1cd6b`)

`RootHash` llamaba a `char.ToLowerInvariant` por cada letra (tres cadenas por archivo: ruta, tamaño, fecha). Para ASCII la minúscula es solo A–Z; el resto sigue pasando
por `ToLowerInvariant`. Las rutas de una carpeta se ordenaban comparando la ruta entera (60 letras o más iguales en todas): ahora se salta el comienzo común, **comprobado letra
por letra** en todas, con el mismo `Array.Sort` y la misma respuesta en cada comparación (también en los empates; si el comienzo no es común, el comparador de siempre).

Prueba (`firmarapida`): el código de antes va copiado en la batería. Los 65.536 caracteres uno por uno, 4.051 cadenas (ASCII, acentos, I turca con y sin punto, eszett,
signo Kelvin, Cirílico, pares sustitutos, null, vacía, prefijos largos), 360 listas de rutas de Windows y de Linux (291 con empates de mayúsculas, otras sin prefijo común o con
una ruta vacía) y árboles de verdad (acentos, atajo, carpeta «Windows», 3.000 archivos) en los dos caminos (.NET 4 y CLR 2): misma firma. Con el ejecutable anterior como segundo
argumento, también igual que él.

## 1. Firma en CLR 2: una lista por carpeta (`74d1fe3`)

En .NET 3.5 la firma pedía `Directory.GetFiles`, luego `new FileInfo(ruta).Length` por archivo, luego `GetDirectories` y `DirectoryInfo.Attributes` por carpeta. `RecorridoRapido.cs`
saca nombre, atributos, tamaño y fecha de cada entrada de un solo recorrido (`FindFirstFileW`/`FindNextFileW`) y `FirmaListado` apunta lo mismo que antes: mismo orden por nombre, mismos tres datos,
misma memoria del recorrido (`ScanDirectoryMemo`, archivos y carpetas en el orden del sistema) y mismas carpetas por visitar. Solo actúa con CLR 2 en Windows. **Todo lo que no es el caso limpio
devuelve null y esa carpeta se firma como siempre, con sus errores (`!TipoDeError`)**: sin permiso o carpeta desaparecida (falla `FindFirstFile`), ruta de 240 letras o más (`GetFullPath` lanza
`PathTooLongException` a partir de 260 y el tipo forma parte de la firma), punto o espacio al final del nombre, nombres de aparato (CON, NUL, AUX, PRN, COM1–9, LPT1–9), caracteres prohibidos, rutas
`\\?\`, un fallo a medias o una fecha imposible.

La construcción de la lista (`ListadoCarpeta.Construye`) no toca Windows, y la firma toma la fuente de datos de un campo (`fuenteListado`): las pruebas lo cambian por una lista inventada del disco de
verdad y llaman a `FirmaCarpeta` con el modo de CLR 2. Se comprueba: misma firma que el código anterior, misma memoria del recorrido, mismos errores con la fuente que devuelve null o lanza, tamaños
de más de 4 GB, fecha cero, atajos, «.» y «..», raíz de unidad, nombres raros y rutas largas, y que `WIN32_FIND_DATA` mide 592 bytes con texto Unicode.

**Lo que NO está probado:** la llamada de Windows (`ListadoWin32.Lee`: `FindFirstFileW`, `FindNextFileW`, `FindClose`, el `ByValTStr` de la estructura y el código de error 18 del final) no se puede
ejecutar fuera de Windows. Hay que probarla en XP: la firma de la biblioteca con la rocola nueva tiene que salir **igual** que con la anterior (si no, la biblioteca guardada se descarta y se recorre el disco: no se
pierde nada, pero se pierde el ahorro). Línea a leer: `biblioteca: lista en … comprobar la guardada (recorrer las carpetas) N ms` (antes 0,52–0,56 s de 0,87–0,99 s en 30.000 canciones con Mono).
No se hizo la lectura de las tablas en paralelo con el recorrido (1b del informe): no se pudo medir aquí.

## 3. El arreglo de la canción en memoria: solo el diagnóstico (`e7e9001`)

Cada canción en memoria es un `byte[]` de 1 a 32 MB (monton de objetos grandes); pedirlo puede disparar una recolección de generación 2. Con `diagnostico=1`, la línea `cancion:` de `rocola_diagnostico.txt` lleva
ahora de dónde vino el último arreglo (lectura por adelantado o al abrir), cuánto pesaba, cuánto tardó pedirlo, los contadores gen2 y gen0 antes y después y el total de gen2. **Una diferencia de contadores es una
coincidencia en el tiempo (otro hilo pudo provocarla), no su duración ni su causa.** Sin diagnóstico no se mide ni se guarda nada.

**El reuso de arreglos (reserva de dos huecos) NO se hizo.** El arreglo pasa por la lectura por adelantado, el `MemoryStream`, el lector de NAudio, la cadena de sonido y el hilo de la música, que lee mientras la
pantalla cierra la fuente; hoy lo protege el recolector de basura. Reusarlo exigiría probar que ninguna lectura sigue en curso al soltarlo, y la ganancia no está medida: queda para después de leer la línea en el A4.
Línea a leer en la rocola real: `cancion: abierta en … ms, sonando en … ms (…); ultimo arreglo de cancion (lectura por adelantado): … KB en … ms, gen2 a -> b, …; gen2 en total: …`. Si `gen2` sube cada
canción y coincide con tirones (`pantalla parada … ms`), entonces tiene sentido el reuso.

## 7. Respaldo del arranque: bytes en vez de dos SHA-256 (`3f19a3c`)

`BackupToCapturado` leía cada archivo para calcular un SHA-256 mientras lo copiaba y releía la copia para calcular otro (2–8 s de procesador en .NET 3.5 según el informe, en un hilo de baja prioridad pero encima
del arranque). Ahora, con la copia confirmada en disco, `CompruebaCopiaDeRespaldo` la relee y la compara byte a byte con el origen capturado (los primeros «largo» bytes: un anexo en curso sigue cortado), con los mismos bloques
de 64 KiB y las mismas cesiones. Se mantienen «La copia del respaldo no coincide», el error del origen truncado y la publicación por renombrado solo tras comprobar.

Medido (Mono): 30 MB de tablas, 5 respaldos, 1.150 ms → 90 ms de procesador. El SHA-256 de Mono no es el `SHA256Managed` de .NET 3.5: la ganancia real en XP no se ha medido.
**Cambio de semántica, dicho:** se compara con el origen tal como está al comprobar y no con lo leído al copiar; un origen reescrito *en sitio* en esa ventana daría «no coincide» y no se publicaría (los guardados de la rocola
anexan o reemplazan el archivo, no lo reescriben en sitio; un respaldo no publicado conserva los anteriores). `respaldobytes`: copia igual pasa en 9 tamaños; 28 copias con un bit cambiado (primero, último, borde de bloque), más corta,
más larga, corrida y llena de ceros se rechazan; de punta a punta (origen que cambia tras confirmar la copia) no se publica, no deja carpeta a medias y el respaldo anterior queda. La parte de punta a punta necesita
`FlushFileBuffers` (Windows) o, en Linux, un puente de pruebas: sin él la batería lo dice y lo salta; `discoacotado` y `datosdeltas` (que también lo necesitan) fallaban así antes en Linux.

## 8. Cosecha de la IA: origen una vez, una confirmación por archivo (`10760b8`)

Una canción en estéreo sacaba el origen dos veces (etiqueta del archivo, género y huellas, una por canal) y confirmaba en disco seis veces (datos, índice y géneros de cada canal). Ahora `MeasureFile` saca el
origen una vez (`IaOrigenParaCanal` lo copia con el otro canal) y `IaAppendPairsLote` anexa los canales juntos con las mismas comprobaciones y el mismo orden, y confirma cada archivo una vez: **primero los datos,
y solo después el índice y los géneros** (la regla de siempre: un ejemplo sin entrada queda «sin origen», que el entrenador no usa para validar, y nunca se atribuye mal). Medido con un `FlushFileBuffers` que cuenta: 6,0 → 3,0
por canción. `cosechaunavez` compara byte a byte los tres archivos con los del ejecutable anterior en 16 casos (estéreo, mono, varias seguidas, 1.536 filas, lotes inválidos, sin origen, géneros raros, archivos nuevos que
apartan el índice y los géneros, cola rota de un apagón en los tres archivos, índice y datos ajenos, tope del archivo con cabida para dos/uno/ninguno). Lo único que cambia: si se va la luz entre los dos canales, el canal 0 ya no tiene su
entrada de origen.

## 6. Menos objetos de vida larga (`62628f5`)

- **a) Espejo del EQ:** `RegistroEq` comparte el arreglo de `eqCache` en vez de copiarlo (12 floats por canción). Es seguro porque esos arreglos no se modifican una vez puestos (cada medida arma otro; se revisaron todos los
  sitios que escriben en uno); la batería `diario` lo vigila y `objetosmenos` comprueba que se comparte y que una medida nueva pone otro arreglo.
- **b) Fuentes medidas:** cuatro objetos por canción (registro, `FuenteMedida`, dos cadenas hexadecimales) pasan a uno (tres números del sello y dos huellas de 64 bits). `rocola_fuentes.txt` queda igual (mismo formato, 16 cifras en
  mayúsculas): 3.000 filas leídas y reescritas igual, y igual que el código anterior. **Diferencias, solo con un archivo tocado a mano:** una fila con 16 caracteres que no son hexadecimales ya no se acepta (antes se guardaba y
  nunca coincidía), y una huella en minúsculas se reescribe en mayúsculas. Medido: 328 → 210 bytes por canción con 20.000 (−2,3 MB, 60.000 objetos menos).
- **c) `Track.FromFile`:** el artista que sale del nombre de la carpeta se calcula una vez por carpeta (una sola referencia inmutable, segura entre hilos) y las canciones de ella comparten la cadena. Mismas canciones que antes en 6.030
  llamadas (por carpetas y revueltas, y contra el ejecutable anterior), cuatro hilos sin mezclar carpetas; 30.000 canciones retienen un 6 % menos.

## 4. Reproducción desde la memoria con el lector ligero (`570fac6`)

`AbreMp3` de memoria abre `LectorMp3Ligero` (el que ya usaba el repaso, `Mp3Ligero.cs`) si NAudio es la 1.9 esperada: el mismo `Mp3FileReader`, con su cuenta de `Read`/`Position`, pero reutilizando la memoria de cada cuadro en
lugar de crear un `Mp3Frame` y dos arreglos por cuadro (5,8–9,6 MB de basura por canción medidos en el repaso). La reproducción de un MP3 abierto del archivo (más de 32 MB o sin memoria) no cambia.

`reproligera` exige, con el descompresor de verdad de la coma flotante (NLayer) y leyendo como la reproducción (`SampleChannel`, bloques de 4.096 muestras), **el mismo PCM muestra a muestra** que con NAudio en 27 MP3 de pruebas
(≈ 100 millones de muestras): canción entera, saltos (a mitad, atrás, al principio, casi al final, por tiempo), reanudar con otro lector, relevo sin pausa (la siguiente ya abierta, la primera cerrada) y la pantalla preguntando
`Position`/`TotalTime`/`Length` desde otro hilo; mismas posiciones y duración; un MP3 roto falla igual con los dos. **Honestidad sobre la ganancia:** con NLayer casi toda la asignación es de NLayer (19.974 KB frente a
19.913 KB), así que solo se midió el ahorro del lector con un descompresor sin asignaciones (`mp3ligero`, 98 %). Con el descompresor de Windows (ACM) no se ha medido; la mayoría de las canciones usan ACM.
`enmemoria` ya vigilaba que `AbreMp3` de memoria sea el de archivo letra por letra; ahora lo compara sin los comentarios y con el lector ligero como único cambio.

## Hilos de fondo que no cierran la rocola (`491f2be`)

En CLR 2 una excepción sin atrapar en un hilo cierra el programa. `CoverPump` solo atrapaba dentro de sus bloques: ahora todo el hilo va en un try/catch/finally (se apunta con `LogError`, se baja `coverPumpCount`
—antes solo bajaba por la salida normal, así que no se armaba otro lector— y la carátula en curso queda para reintentar a los 30 s). Los ayudantes de `ClavesDelIndice` atrapan su fallo y su tramo se rehace en el hilo que reparte.
`hilosatrapados` provoca el fallo de verdad; con la rocola de antes el proceso muere con la excepción sin atrapar.

## Con una errata que se corrigió en el camino

Las comparaciones «contra el ejecutable anterior» de `firmarapida`, `cosechaunavez` y `objetosmenos` cargaban el ejecutable anterior con `Assembly.LoadFrom`, que devuelve el montaje ya cargado si tiene la misma identidad: comparaban la
rocola nueva consigo misma. Se corrigió (`afc6ff0`, `Assembly.Load` de los bytes, como otras baterías) y se rehicieron: siguen saliendo iguales contra el ejecutable anterior de verdad. Las medidas de memoria y de confirmaciones se hicieron
en procesos aparte y no estaban afectadas.

## Propuestas que no se hicieron

| Idea | Por qué no |
| --- | --- |
| 5. Caché de miniaturas en formato crudo `.bgra` en el SSD | 256 KB por disco; prioridad baja, y cambia datos del usuario (la caché de miniaturas) |
| 9. Media del EQ con sumas corridas | Cambia el orden de los flotantes (≈ 1e-4 dB): **no es idéntico bit a bit** |
| 10. Precalentar los «bolsos» de búsqueda de una letra | Requiere reestructurar `SearchItem.Score`; riesgo medio sin medida |
| 11. NGEN de `Rocola.exe` | Es del despliegue, no del código; probar en el A4 |
| Reuso de los arreglos de canción (reserva de dos huecos) | Ver el punto 3: primero la línea del A4 |
| Lectura de las tablas en paralelo con el recorrido (1b) | Sin medida posible aquí |
| FileStreams de 64 KB para índices/decodificación | Marginal |

## Verificación

- Compila con `-sdk:2 -langversion:3 -warn:4 -warnaserror+` (receta de la entrega, C# 3), perfiles con y sin tarjeta, 0 avisos.
- Baterías nuevas en `todo.sh` y `pruebas/LEEME.md`: `firmarapida`, `respaldobytes`, `cosechaunavez`, `objetosmenos`, `reproligera`, `hilosatrapados`; ajustadas a la estructura nueva (sin quitar lo que vigilaban): `enmemoria`
  (diagnóstico, `AbreMp3`), `discoacotado` (comprobación por bytes), `iaorigen` (la cosecha por `IaAppendPairsLote`), `eficiencia` (campos del registro de fuentes).
- Tanda completa (`pruebas/todo.sh`, con el `rocola.exe.config` de Mono y un puente de pruebas para `FlushFileBuffers`, ambos locales y sin subir): **ningún nombre con problemas nuevo** respecto a la tanda de referencia. Siguen
  fallando los mismos de antes, ajenos a estos cambios: abc, atraccion, auditoriageneral, calma, caratulasadaptativas, colacompacta, descompresor, discovivo, duerme, estilo, iasdisparo, leer, memoriaintegrada, moneda,
  mp3medidaligera, paginar, pantallaviva, partescaratulas, planbiblioteca, sala, tope, vigiapantalla. Dejan de fallar respecto a la referencia: `auroraeficiente`, `cintasligeras`, `estadoeficiente`, `hojeoportadas`,
  `resolucionligera`, `trabajovisible` (solo faltaba el archivo de configuración de Mono) y `discoacotado` (su parte de respaldo necesita `FlushFileBuffers`, que aquí aporta el puente). `resol` y `dibujoligero` pasaron.
- `cpucongelado`: 14 métodos del dibujo por procesador idénticos al ejecutable previo.
- Pruebas automatizadas en Linux/Mono con datos sintéticos: no prueban Windows XP, el A4, el SSD, el audio real ni la pantalla del bar.

## Qué falta en el A4 / XP

1. **La firma con `FindFirstFile` en XP:** que la firma salga igual que con la rocola anterior (si no, la biblioteca guardada se descarta) y cuánto baja `comprobar la guardada (recorrer las carpetas) N ms` en `rocola_diagnostico.txt`.
2. **`gen2` por canción:** la línea `cancion: … ultimo arreglo de cancion … gen2 a -> b` (con `diagnostico=1`) junto a las `pantalla parada … ms`.
3. **Respaldo del arranque:** que el tiempo de procesador del respaldo baje (aquí 1.150 → 90 ms con Mono; el `SHA256Managed` de .NET 3.5 es mucho más lento).
4. **Reproducción con el lector ligero y ACM:** que no cambie nada que se oiga (la batería lo prueba con NLayer) y cuánto baja la basura por canción.
