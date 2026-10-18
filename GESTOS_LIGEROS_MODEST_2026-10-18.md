# Gestos con menos cálculos — Modest, 18-10-2026

Referencia: `2d853c4`, rama `claude/modest-carson-fa2v2n`.
Destino: Windows XP/.NET 3.5, AMD A4-6300, dos núcleos y 3,5 GB de RAM.

## Cambios

El asistente comparte muestras matemáticas que antes calculaba dos veces en
la misma pose: respiración/rebote, pasos alternos, saludo, explicación,
confirmación, despedida, golpecitos con el pie y vaivén. Conserva las fórmulas,
el orden de operaciones de cada componente y las conversiones originales
entre double y float. No guarda poses entre cuadros ni aproxima el tiempo:
los movimientos y la respuesta a cada gesto siguen actualizándose normalmente.

Las caras de los cubos calculan cuatro esquinas y copian las dos compartidas
entre triángulos. Antes volvían a escribirlas mediante dos llamadas adicionales.
Se conservan seis vértices, UV, colores, orden de dibujo y caché de poses.

Son ahorros automáticos, sin cambios visuales ni interruptores nuevos. No añaden
texturas, campos persistentes, cachés ni reservas por cuadro. Los trece ajustes
de Performance y sus dependencias permanecen vigentes. No se modifica audio,
reproducción, datos, resolución de textos/portadas ni renderizado CPU.
La estrella conserva su cuerpo y personalidad sin rostro.

## Comparación independiente

`pruebas/gestosligeros.cs` compara todas las componentes float, por sus bits,
contra el ejecutable anterior. Cubre 51.858 poses: nueve gestos, 21 segundos de
ciclos a 240 muestras/s, selección cambiante, animación desactivada, fronteras
de impulsos, tiempos negativos, NaN e infinitos. Resultado: **1.452.024
comprobaciones aprobadas**, sin diferencias. Sin segundo ejecutable realiza
726.012 comprobaciones de valores finitos e informa que omite la comparación.

`cubosligeros` conserva sus 41.387 comprobaciones y 1.015.896 bytes comparados
contra el anterior, incluyendo tres resoluciones, fundidos, texturas y cambios
de claves. Se comparan también 27 huellas de textos y 96 de aurora.

El release se verifica con 34 baterías relacionadas sobre el Rocola.exe que
se entrega; `VALIDACION.txt` identifica ese binario y enumera los resultados.
GPU y CPU compilan con referencias oficiales de .NET 3.5 y advertencias tratadas
como errores. Se comprueban además tres baterías del perfil CPU congelado.
Los scripts `todo.sh` y `validar-estado-extendido.ps1` incluyen la prueba nueva;
el segundo admite `-PreviousRelease` para la referencia independiente.

Permanece el fallo previo de maquetación de `cielohondo` bajo Mono, reproducido
anteriormente también en `0a3f7b0`. No se afirma que pase toda la suite ni se
sustituye con esta prueba la comprobación visual en hardware.

## Medición aislada

Dos rondas con orden de versiones invertido; sin compilaciones ni pruebas
simultáneas. Cada gesto usa 31 lotes de 10.000 poses tras calentamiento, mediante
un delegado generado que llama directamente al método y consume su resultado.
No incluye reflexión en el tramo medido. Medianas en **microsegundos por pose**:

| Gesto | Anterior R1 / R2 | Actual R1 / R2 |
|---|---:|---:|
| Reposo | 0.2579 / 0.2583 | 0.2184 / 0.2265 |
| Saludar | 0.2801 / 0.2799 | 0.2423 / 0.2439 |
| Explicar | 0.2963 / 0.2984 | 0.2510 / 0.2539 |
| Buscar | 0.2514 / 0.2556 | 0.2209 / 0.2204 |
| Señalar | 0.2799 / 0.2811 | 0.2566 / 0.2528 |
| Confirmar | 0.2617 / 0.2622 | 0.2259 / 0.2288 |
| Volver | 0.2461 / 0.2518 | 0.2214 / 0.2181 |
| Crédito | 0.2910 / 0.2913 | 0.2675 / 0.2605 |
| Despedir | 0.2741 / 0.2752 | 0.2404 / 0.2334 |

El cálculo de poses cuesta aproximadamente **8–15% menos**, según gesto y
ronda. Es un ahorro pequeño en tiempo absoluto: no equivale a ese porcentaje
de mejora de FPS ni resuelve por sí solo los tirones.

Los cubos muestran diferencias pequeñas y mixtas. Por ejemplo, tres cubos con
pose distinta cada cuadro y sin fundido: 1.445/1.406 → 1.427/1.403 µs; con
fundido: 1.547/1.569 → 1.549/1.570 µs. No se atribuye una aceleración general a
la copia de esquinas. Los registros completos incluyen p95 y máximos de lotes.

## Cuadros completos y límites

96 casos en 1024×768, 1280×1024 y 1920×1080; estrella 2D/3D, rejilla/estado,
títulos largos, nebulosas animadas, dos perfiles y dos rondas. **34.656 cuadros
aceptados, cero rechazados.** Este medidor no conecta el asistente ni cubos
vivos: es una regresión general, no una medición del ahorro de sus poses.

| Medida entre los 48 casos de cada versión | Anterior | Actual |
|---|---:|---:|
| Mediana de p50 (ms) | 0.1230 | 0.1230 |
| Mediana de p95 (ms) | 0.1580 | 0.1655 |
| Mediana de p99 (ms) | 1.3455 | 1.2180 |
| Mayor intervalo observado (ms) | 6.400 | 4.763 |

Resultados mixtos, sin evidencia de mejora general de fluidez. No se atribuyen
los máximos a GC ni a otra causa sin medirla. `MEDICION_SINTETICA.txt` contiene
los registros de ambas versiones, p95/p99/máximos y contadores de presentación.

Linux/Mono CLR 4 con controlador artificial no reproduce Windows XP, GPU,
monitor ni música reales. El beneficio en el AMD A4 y la fluidez a mayor
resolución siguen pendientes de comprobar en la máquina del local.
