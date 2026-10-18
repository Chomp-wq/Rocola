# Iluminación con menos cálculos — Modest, 18-10-2026

Destino: AMD A4-6300, dos núcleos, 3,5 GB de RAM, Windows XP/.NET 3.5.
Solo `claude/modest-carson-fa2v2n`; referencia anterior `5cee759`.

## Dos ahorros automáticos

1. **Color del suelo de la aurora.** Sus cinco filas parten del mismo color por
   columna. Se calcula una vez la saturación y la posición relativa de sus tres
   canales; cada fila aplica después su intensidad. Se conservan las operaciones,
   límites y redondeos anteriores, sin aproximar colores. Las cinco intensidades
   geométricas constantes se preparan una vez. Con suelo visible se pasa de 725
   descomposiciones de color a 145 por muestra de la aurora. Si el suelo está
   oculto o desactivado, se sigue omitiendo todo ese trabajo.
2. **Iluminación de la estrella 3D.** Los doce vértices comparten su longitud y
   dirección hacia la cámara entre las caras que los usan. Los tintes de borde
   y del reflejo del género se preparan una vez por pose, en lugar de repetirlos
   por cada vértice de cada cara. Se conservan las normales de las caras, sus
   reflejos, destellos y redondeos. La dirección de vista se omite cuando los
   ajustes activos no la usan. Los ecos conservan su caché de geometría.

Son ahorros sin cambios de aspecto ni funciones y no añaden interruptores.
Se mantienen todos los ajustes existentes y sus dependencias, la animación y
la resolución nativa de textos/portadas. No se modifica audio, reproducción,
datos del usuario ni el renderizador CPU.

Memoria adicional de datos: 20 bytes para los niveles del suelo, 192 bytes por
panel para los doce vértices y doce indicadores para la estela, además de las
cabeceras de los arreglos y un pequeño valor temporal de tinte. No se crean
texturas, bitmaps ni arreglos por cuadro para estos ahorros.

## Actualización del ajuste de degradado

Al revisar la estela se detectó que su caché no incluía `EstrellaDegradado`.
Cambiar ese ajuste podía conservar copias con el sombreado anterior hasta que
se reemplazasen. Ahora cada ranura registra también ese valor y reconstruye
su pose al cambiarlo. No modifica el interruptor ni su persistencia.

## Verificación

`coloresligeros` compara **1.200.960 colores del suelo** con la fórmula anterior:
negros, grises, colores saturados, colores aleatorios y niveles dentro/fuera de
rango, incluidos NaN e infinitos. Comprueba que no se escriban otras columnas.

Con `5cee759` como segundo ejecutable, compara **12.288 poses completas** en
1024×768, 1280×1024 y 1920×1080: 32 combinaciones de ajustes, 64 orientaciones
por combinación, estrella principal y ecos. Los vértices, colores, bordes,
recuentos y posición/intensidad del destello coinciden. También compara la
estela cacheada con poses nuevas mientras se alterna el degradado.

Resultado: **49.187 comprobaciones y 36.264.816 bytes comparados**, sin
diferencias. Sin segundo ejecutable, la batería comprueba los colores y la
invalidación de estela, e informa que omite la comparación de poses anterior.
El script Windows acepta `-PreviousRelease` para ejecutar esa comparación.

El ZIP se valida con **29 baterías relacionadas sobre su propio ejecutable**:
las 27 anteriores, más `coloresligeros` (con referencia anterior) y
`ecosgeometria`. Esta última comprueba otras 8.292 condiciones y 8.837.640 bytes
de geometría de ecos. `VALIDACION.txt` identifica el ejecutable mediante SHA256.

GPU y CPU compilan con referencias oficiales de .NET 3.5, sin advertencias.
También pasan `estadoeficiente`, `ajustesvivos` y `fichaportada` contra CPU.
Se conserva documentado el fallo previo de maquetación de `cielohondo` bajo
Mono, reproducido anteriormente también en `0a3f7b0`; no se afirma que todas
las pruebas del repositorio pasen.

## Medición y límites

El modo `medir` de `coloresligeros` mide una pose completa por delegado, sin
reflexión dentro del tramo cronometrado. Usa 31 lotes de 500 poses tras calentar,
con giros cambiantes, tres combinaciones de ajustes y estrella principal/ecos.
Las muestras son promedios por lote; no son latencias de cuadro ni FPS.

Medianas, en microsegundos por pose (dos rondas):

| Caso | Anterior R1 / R2 | Actual R1 / R2 |
|---|---:|---:|
| Estrella, pulido completo | 12.306 / 12.651 | 9.293 / 9.468 |
| Eco, degradado activado | 5.793 / 5.871 | 5.408 / 5.422 |
| Estrella, destello sin borde/reflejo/degradado | 5.697 / 5.572 | 4.582 / 4.664 |
| Eco, degradado apagado | 2.741 / 2.756 | 2.747 / 2.823 |
| Estrella, solo degradado | 5.639 / 5.712 | 4.893 / 5.294 |

La pose principal con pulido completo cuesta aproximadamente un 25% menos en
esta prueba aislada. El eco sin degradado no obtiene ahorro; su camino ya era
simple. Los ecos congelados de la aplicación reutilizan su geometría, de modo
que la mejora de su construcción solo se aplica cuando les corresponde una
pose nueva. Ninguna de estas cifras es un porcentaje de mejora de FPS.

La comparación de cuadros completos usa títulos largos, estrella 2D/3D,
rejilla/estado extendido y dos perfiles iguales entre versiones, en las tres
resoluciones. Dos rondas invierten el orden de los ejecutables. Se registran
p50/p95/p99/máximo de intervalos Start→Start, aceptados y rechazados.

Se completan **96 casos, 34.656 cuadros aceptados y cero rechazados**. La mediana
de los p50 entre casos pasa de 0.128 a 0.1275 ms, y la de p99 de 1.2395 a
1.2285 ms. No hay mejora uniforme en máximos: el mayor intervalo observado pasa
de 5.955 a 6.623 ms. La prueba no demuestra eliminar los tirones. Los registros
completos están incluidos en `MEDICION_SINTETICA.txt` del ZIP.

Las mediciones se ejecutan sin otras compilaciones ni pruebas simultáneas.
El controlador artificial no mide el trabajo de la GPU, la espera del monitor,
una sesión musical ni Windows XP. No permite prometer fluidez o una resolución
mayor en el A4 del local; falta la comprobación en esa máquina.
