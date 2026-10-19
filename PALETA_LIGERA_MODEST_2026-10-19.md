# Colores de portadas con menos llamadas — Modest, 19-10-2026

Referencia: `e4b443f`, rama `claude/modest-carson-fa2v2n`.
Destino: Windows XP/.NET 3.5, AMD A4-6300, dos núcleos y 3,5 GB de RAM.

## Cambio automático

Al extraer hasta 1.024 muestras de una portada, se consultaban Width y Height
al calcular cada posición. Ahora se leen una vez por método. También se calcula
una sola vez por fila su posición y dirección de memoria.

Los píxeles de 32 bits se leen con una sola operación ReadInt32, conservando
sus canales BGRA. RGB fuerza alfa 255 como antes. PArgb con alfa distinto de 255
sigue abandonando la ruta directa y usando GetPixel, para conservar exactamente
su redondeo al deshacer la premultiplicación. RGB de 24 bits sigue leyendo tres
bytes, sin invadir el píxel siguiente ni el final de la fila. Los formatos
indexados mantienen GetPixel y también aprovechan las dimensiones ya leídas.

Se mantienen los mismos puntos de muestra, descarte por alfa, agrupación,
selección y orden de tonos. El bloqueo es de solo lectura y se libera incluso
cuando se abandona la ruta directa. No añade arreglos ni memoria persistente.
Es automático, sin interruptor porque no cambia el aspecto ni la funcionalidad.
No modifica la reproducción, las prioridades de carga, los datos del usuario,
el renderizador CPU ni los trece ajustes de Performance y sus dependencias.

## Comparación independiente

`pruebas/paletaligera.cs` comprueba 205 imágenes:

- RGB 24/32 bits, ARGB, PArgb e imágenes indexadas.
- Tamaños desde 1×1 hasta 1920×1080, anchos impares y filas con padding.
- Colores variados, grises, negro, alfa variable y el límite 127/128.
- Memoria con stride negativo, imagen nula y liberación del bloqueo.
- Muestras contra GetPixel y el release anterior; paleta final, cantidad y orden
  de colores contra una imagen independiente del release anterior.

**200.400 comprobaciones y 198.278 comparaciones de muestras, sin diferencias.**
El recuento de muestras incluye las comparaciones contra GetPixel y contra el
anterior; no representa esa cantidad de puntos distintos. Sin segundo ejecutable,
se informa la omisión y se realizan 100.053 comprobaciones con 99.139 muestras.

La prueba se integra en `todo.sh` y en el script Windows con `-PreviousRelease`.
También se ejecuta `paletaservicio`: 401 casos de paletas y su integración; la
comprobación HAL se omite por falta de dispositivo real y no se considera probada.
El ZIP pasa **41 baterías relacionadas sobre su Rocola.exe**, con SHA256 en el
informe. GPU y CPU compilan con referencias oficiales de .NET 3.5, sin advertencias;
pasan además tres baterías CPU. Se conservan 27 huellas de textos y 96 de aurora.

Persisten los fallos previos de `atlasgpu` (GDI, reproducido contra `8814afa`) y
`cielohondo` (maquetación Mono, contra `0a3f7b0`). No se repitieron en esta ronda
ni cuentan como aprobados. No se afirma que pase toda la suite del repositorio.

## Medición de extracción completa

Dos rondas con orden de versiones invertido, sin compilaciones ni otras pruebas
simultáneas. 21 lotes de diez extracciones tras dos lotes de calentamiento.
Las imágenes se crean fuera de la medida; el tiempo incluye lectura de muestras
y selección final de colores. Medianas en **microsegundos por extracción**,
con imágenes opacas de colores variados:

| Imagen / formato | Anterior R1 / R2 | Actual R1 / R2 |
|---|---:|---:|
| 73×59 / RGB24 | 259.350 / 261.660 | 131.670 / 132.220 |
| 73×59 / ARGB32 | 238.300 / 235.900 | 111.040 / 110.870 |
| 1024×768 / ARGB32 | 232.070 / 234.000 | 111.690 / 112.000 |
| 1024×768 / PArgb32 | 232.770 / 232.310 | 112.470 / 107.670 |
| 1024×768 / indexada | 297.220 / 300.010 | 173.860 / 154.890 |
| 1024×768 / RGB24 | 4199.680 / 4341.390 | 4065.720 / 4195.700 |

ARGB32 cuesta aproximadamente un 52–53% menos en estos casos. El ahorro absoluto
ronda 0.12 ms en este entorno y ocurre al extraer la paleta, no por cuadro.
En RGB24 grande, el coste de LockBits/GDI+ domina y el ahorro ronda solo el 3%.
No se extrapolan los porcentajes a la carga completa, al disco, a Windows XP
ni a los FPS. Los registros incluyen también transparencia, 1920×1080, p95 y
máximos de lotes, incluidos los resultados que empeoran.

## Regresión del dibujo

Se repiten 96 casos de tres resoluciones, estrella 2D/3D, rejilla/estado extendido,
neón, títulos largos y nebulosas animadas: **34.656 cuadros aceptados, cero rechazados**.

| Medida entre los 48 casos de cada versión | Anterior | Actual |
|---|---:|---:|
| Mediana de p50 (ms) | 0.1245 | 0.1240 |
| Mediana de p95 (ms) | 0.1730 | 0.1655 |
| Mediana de p99 (ms) | 1.2400 | 1.2300 |
| Mayor intervalo observado (ms) | 4.920 | 4.979 |

El medidor utiliza portadas ya preparadas y no mide esta extracción. Sus pequeñas
diferencias no se atribuyen al cambio: sirven como comprobación de continuidad
del dibujo. No conecta asistente, cubos vivos ni cielo intermedio; usa celdas simples.
`MEDICION_SINTETICA.txt` conserva los registros completos.

Linux/Mono CLR 4 ejecuta binarios dirigidos a .NET 3.5. No hay prueba en Windows XP,
GPU, audio o disco del local. La fluidez a mayor resolución sigue pendiente de
comprobar en el AMD A4 real; no se reduce resolución ni calidad de las portadas.
