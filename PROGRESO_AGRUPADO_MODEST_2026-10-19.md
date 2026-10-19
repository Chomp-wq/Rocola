# Barra de progreso con menos trabajo interno — Modest, 19-10-2026

Referencia: `26fdd6a`, rama `claude/modest-carson-fa2v2n`.
Destino: Windows XP/.NET 3.5, AMD A4-6300, dos núcleos y 3,5 GB de RAM.

## Cambio automático

El tubo de neón preparaba diez quads y solicitaba su dibujo por separado:
resplandor, tubo y núcleo, con sus bordes transparentes. Ahora los prepara en
un solo arreglo y solicita una malla de veinte triángulos. La chispa sigue
dibujándose después, con textura y mezcla aditiva. Una barra simple también
reúne sus dos quads en una solicitud.

Se conserva el orden de todos los triángulos, la corrección de medio píxel,
los colores, UV, degradados y transparencias. El progreso, su suavizado, la
transición al estado extendido y los ajustes de neón conservan su lógica.
No modifica sonido, reproducción, datos del usuario ni renderizador CPU.

**El agrupador general ya estaba activo de fábrica.** La mejora reduce las
peticiones internas y las copias pequeñas a ese agrupador; no se atribuye una
reducción equivalente de llamadas al controlador. En el tubo aislado, incluyendo
la chispa, las peticiones pasan de once a dos. Con el agrupador general activo,
ambas versiones terminan enviando dos llamadas al controlador. Sin él, los envíos
sí pasan de once a dos. No se reducen triángulos ni píxeles dibujados.

El arreglo de progreso pasa de seis a sesenta vértices: 1.680 bytes de datos,
1.512 bytes más por panel. Se reserva al construir el panel y se reutiliza;
no crece ni se reserva memoria por cuadro. Las optimizaciones son automáticas,
sin nuevos interruptores. Los trece ajustes de Performance y sus dependencias
siguen vigentes; textos y portadas conservan resolución nativa.

## Comprobaciones

`pruebas/progresoagrupado.cs` captura los triángulos enviados y compara cada
vértice junto con su textura, modo de textura y mezcla contra el release anterior.
Solo permite variar los límites entre llamadas de dibujo.

- 10.800 escenas: 1024×768, 1280×1024 y 1920×1080; barra simple y neón tenue/intenso.
- Agrupador general apagado y encendido, dentro de una escena activa y vaciando
  lo pendiente; se comprueba que el agrupador realmente reduzca los envíos.
- Coordenadas fraccionarias, anchos nulos/negativos/subpíxel, alturas pequeñas,
  colores y alfa variables, tiempos negativos/largos y tiempo NaN.
- Todos los triángulos esperados, dispositivo sano, buffer estable y
  pérdida/recuperación del dispositivo sin restos de dibujos anteriores.

**224.527 comprobaciones y 18.349.632 bytes de vértices comparados, sin diferencias.**
Sin segundo ejecutable, informa que omite esa comparación y comprueba 80.911
contratos. La prueba se integra en `todo.sh` y en el script Windows, que acepta
`-PreviousRelease`. También se ejecuta `septimatanda`, que cubre el neón original.

El ZIP se valida con **39 baterías relacionadas sobre su Rocola.exe**. GPU y CPU
compilan con referencias oficiales de .NET 3.5 y advertencias como errores;
pasan tres baterías del perfil CPU congelado. Se comparan 27 huellas de textos
y 96 de aurora. El informe identifica el ejecutable mediante SHA256.

Persisten los fallos previos de `atlasgpu` (comparación GDI, reproducida contra
`8814afa`) y `cielohondo` (maquetación bajo Mono, reproducida contra `0a3f7b0`).
No se repitieron aquí ni se cuentan entre las 39 aprobadas. No se afirma que
pase toda la suite del repositorio.

## Medición aislada

Dos rondas con orden de versiones invertido, sin compilaciones ni otras pruebas
simultáneas. 31 lotes de 1.000 dibujos tras tres lotes de calentamiento. Delegados
sin reflexión ni capturas en el tramo cronometrado; se comprueba que el dispositivo
siga sano y envíe todos los triángulos. Medianas en **microsegundos por dibujo**,
con el agrupador general activo como en producción:

| Resolución / dibujo | Anterior R1 / R2 | Actual R1 / R2 |
|---|---:|---:|
| 1024×768 / barra simple | 0.162 / 0.164 | 0.123 / 0.125 |
| 1024×768 / neón tenue | 1.387 / 1.406 | 0.973 / 0.993 |
| 1024×768 / neón intenso | 1.394 / 1.394 | 0.971 / 0.994 |
| 1280×1024 / neón intenso | 1.390 / 1.398 | 0.967 / 0.984 |
| 1920×1080 / neón intenso | 1.389 / 1.412 | 0.958 / 0.997 |

La preparación aislada del neón cuesta aproximadamente un 29–31% menos en estos
casos; el ahorro absoluto ronda 0.4 µs en este entorno. No equivale a ese aumento
de FPS ni a un cambio medido en el AMD A4. Algunos p95/máximos de lotes empeoran:
los registros completos incluyen también esos resultados y el agrupador apagado.

## Cuadro completo y límites

`medirestado` admite ahora un séptimo argumento `progreso`, que activa el neón
al 37% y el agrupador general. Se usa en ambas versiones, en tres resoluciones,
con estrella 2D/3D, rejilla/estado extendido, títulos largos y nebulosas animadas.
96 casos, **34.656 cuadros aceptados, cero rechazados**.

| Medida entre los 48 casos de cada versión | Anterior | Actual |
|---|---:|---:|
| Mediana de p50 (ms) | 0.1245 | 0.1255 |
| Mediana de p95 (ms) | 0.1760 | 0.1635 |
| Mediana de p99 (ms) | 1.2350 | 1.2330 |
| Mayor intervalo observado (ms) | 4.832 | 6.019 |

El resultado es mixto: p95 baja, pero p50 y máximo empeoran. No demuestra una
mejora general de fluidez ni identifica la causa de las pausas. Se usan celdas
simples; no se conecta el asistente ni los cubos vivos ni el cielo intermedio.
El detalle completo queda en `MEDICION_SINTETICA.txt`.

Linux/Mono CLR 4 ejecuta binarios dirigidos a .NET 3.5. Sin GPU, monitor ni audio
reales, estas cifras no son FPS de la rocola. La prueba en Windows XP y la fluidez
a mayor resolución en el AMD A4 siguen pendientes. Este cambio ahorra preparación
en CPU; conserva el trabajo de relleno de la GPU.
