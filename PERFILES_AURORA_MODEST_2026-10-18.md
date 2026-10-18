# Perfiles de aurora precalculados — Modest, 18-10-2026

Destino: AMD A4-6300, dos núcleos, 3,5 GB de RAM, Windows XP/.NET 3.5.
Solo `claude/modest-carson-fa2v2n`; referencia anterior `f1930f3`.

## Ahorro automático

Las 145 columnas de la aurora siempre ocupan las mismas posiciones relativas.
La distancia a cada banda, su radio y su peso suavizado tampoco cambian con la
música. Ahora se preparan una sola vez dos tablas, para el perfil suave y para
«Bandas definidas». Cada columna guarda las dos bandas que pueden alcanzarla y
sus pesos exactos. Se evitan las búsquedas, divisiones y polinomios espaciales
en cada muestra posterior.

Las intensidades se leen de nuevo en cada muestra musical. Se conserva el
orden de las multiplicaciones, sumas, raíces, límites y redondeos. No se
interpola entre muestras, no se reduce la cantidad de columnas ni se modifica
la cadencia de las ondas. Alternar «Bandas definidas» selecciona la tabla
correspondiente inmediatamente, respetando la invalidación de muestra existente.

Las dos tablas contienen **4.640 bytes** de datos, además de sus cabeceras. Son
compartidas y no crecen con la resolución, el tiempo ni la biblioteca. No se
crean objetos, bitmaps ni texturas por cuadro para este ahorro. Las fórmulas
generales se conservan para entradas distintas de las ocho bandas que entrega
`SampleWave`.

No hay nuevos interruptores porque no se cambia el aspecto ni las funciones.
Se conservan los trece ahorros visuales de Performance, sus dependencias, textos
y portadas a resolución nativa. No se modifica reproducción, audio, IAS, datos
del usuario ni el renderizador CPU.

## Comprobaciones

La batería `perfilesligeros` compara contra `f1930f3`:

- **2.900.000 resultados**, bit a bit: 10.000 espectros × 145 columnas × dos
  estilos. Incluye bandas aisladas, silencio, valores aleatorios, negativos,
  subnormales, NaN e infinitos.
- Entradas nulas, otras cantidades de bandas y columnas fuera del intervalo;
  también verifica que las bandas de entrada no se modifiquen.
- **96 huellas de geometría enviada**, con posiciones, colores y mezcla:
  1024×768, 1280×1024 y 1920×1080, alternando los dos estilos, ondas suaves,
  malla ligera, suelo y halo compacto sobre el mismo panel, con intensidades
  musicales y forma de reposo cambiantes. Todas coinciden con la referencia.

Son **3.062.930 comprobaciones** con el ejecutable anterior; sin ese argumento,
se comparan los perfiles con las fórmulas originales conservadas y se generan
las huellas, pero no se afirma compararlas con otro release. Los modos `huella`
y `medir` también funcionan sobre el ejecutable anterior.

El ZIP se verifica con **30 baterías relacionadas sobre su propio Rocola.exe**,
incluida esta comparación independiente y las 29 del release anterior. El
script Windows `pruebas/validar-estado-extendido.ps1` incorpora la batería y
acepta `-PreviousRelease`. `pruebas/todo.sh` también la incluye.

GPU y CPU compilan contra las referencias oficiales de .NET Framework 3.5,
sin advertencias. Pasan además `estadoeficiente`, `ajustesvivos` y `fichaportada`
contra CPU. Se mantiene documentado el fallo previo de maquetación de
`cielohondo` bajo Mono, reproducido anteriormente también en `0a3f7b0`; no se
presenta la suite completa del repositorio como aprobada.

## Medición y límites

Microbenchmark por delegado, sin reflexión dentro del tramo cronometrado:
31 lotes de 500 perfiles completos de 145 columnas, con 64 espectros diferentes,
después de calentar. Dos rondas invierten el orden de los ejecutables.
Las cifras son **microsegundos por perfil**, promedios por lote, no FPS.

| Estilo | Mediana anterior R1 / R2 | Mediana actual R1 / R2 |
|---|---:|---:|
| Suave | 6.101 / 6.122 | 5.041 / 2.645 |
| Bandas definidas | 6.290 / 6.707 | 2.615 / 2.607 |

El perfil definido cuesta un 58–61% menos en estas mediciones aisladas. El suave
presenta más variación: su p95 por lote actual fue 5.405 / 10.209 µs y su máximo
5.878 / 13.154 µs, frente a 6.135 / 6.991 y 6.153 / 7.075 µs anteriores.
La menor cantidad de operaciones no permite garantizar menores pausas del sistema.

La comparación de cuadros completos mantiene los mismos dos perfiles gráficos
entre versiones, títulos largos, estrella 2D/3D y rejilla/estado extendido en
tres resoluciones. Registra intervalos Start→Start, p50/p95/p99/máximo, aceptados
y rechazados. Se completan **96 casos, 34.656 cuadros aceptados y cero rechazados**.

| Medida entre los 48 casos de cada versión | Anterior | Actual |
|---|---:|---:|
| Mediana de p50 (ms) | 0.1265 | 0.1255 |
| Mediana de p95 (ms) | 0.1600 | 0.1715 |
| Mediana de p99 (ms) | 1.2545 | 1.5740 |
| Mayor intervalo observado (ms) | 4.884 | 7.456 |

**No hay una mejora uniforme de los intervalos completos**; las colas de esta
medición son peores. No se atribuyen esos máximos a una causa sin evidencia ni
se promete eliminar los tirones. La cadencia real de las ondas se conserva en
el medidor completo, de modo que no todos sus cuadros recalculan el perfil.
Los registros completos se incluyen en `MEDICION_SINTETICA.txt`.

Las mediciones se hicieron sin otras pruebas ni compilaciones simultáneas.
El controlador artificial en Linux/Mono no mide trabajo real de GPU, espera
del monitor, audio ni Windows XP. El destino del binario sigue siendo .NET 3.5;
el runtime de estas pruebas es Mono CLR 4. La fluidez y la posibilidad de
subir resolución siguen pendientes de comprobar en el A4 del local.
