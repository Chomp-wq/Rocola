# Partículas orbitales con menos cálculos — Modest, 19-10-2026

Referencia: `e2ae0ef`, rama `claude/modest-carson-fa2v2n`.
Destino: Windows XP/.NET 3.5, AMD A4-6300, dos núcleos y 3,5 GB de RAM.

## Cambio automático

El polvo que rodea la estrella se dibuja detrás y delante del cuerpo. Ambas
pasadas repetían el cálculo de las mismas órbitas; además, la coordenada vertical
volvía a calcular el seno usado para decidir en qué pasada dibujar cada partícula.
Ahora comparten 18 muestras de seno, coseno y lado. Por pareja de pasadas, una
fase nueva necesita 18 senos y 18 cosenos en lugar de 54 senos y 18 cosenos.
Si la fase se repite o el movimiento está apagado, se reutilizan las muestras.
También se calcula el color de la corona solamente cuando se utiliza.

Solo se guarda la órbita: centro, tamaño, color, transparencia y dimensiones de
pantalla se consultan en cada pasada. La decisión delante/detrás conserva la
comparación original en doble precisión, antes de convertir las coordenadas a
float. Se mantienen las fórmulas, el orden de partículas y la geometría enviada.

La reserva se hace al usar polvo por primera vez: un arreglo de 18 elementos,
216 bytes de datos más cabecera y campos, menos de 300 bytes habituales por
panel. No crece ni se reemplaza por cuadro. No se modifica el renderizador CPU,
el audio, la reproducción, la precarga ni los datos del usuario.

Es una optimización automática sin cambio visual ni nuevo interruptor. Los trece
ajustes de Performance y sus dependencias permanecen vigentes. Textos y portadas
siguen a resolución nativa.

## Comparación independiente

`pruebas/polvoligero.cs` compara con el ejecutable anterior:

- 7.680 escenas y 15.360 pasadas en 1024×768, 1280×1024 y 1920×1080.
- 32 combinaciones de polvo, halo, movimiento, corona compacta y mezcla de color.
- Tiempo repetido y hacia atrás; cambios de centro, tamaño, alfa y colores con la misma fase.
- Orden de pasadas alternado, polvo apagado y pérdida/recuperación del dispositivo.
- Reserva perezosa y reutilización del mismo arreglo de 18 muestras.

**126.371 comprobaciones y 38.271.744 bytes comparados, sin diferencias.**
Sin segundo ejecutable, declara que omite la comparación independiente y realiza
53.843 comprobaciones de contratos.

El controlador artificial usa DrawPrimitiveUP, sin intentar crear buffers reales.
La prueba exige que siga sano y que ambas pasadas envíen las 18 partículas y los
halos esperados; una captura vacía por dispositivo perdido no cuenta como éxito.
Las medidas también verifican que se ejecuten todos los envíos.

El script Windows acepta `-PreviousRelease`; `todo.sh` incluye la nueva batería.
El ZIP se valida con **37 baterías relacionadas sobre el Rocola.exe entregado**.
GPU y CPU compilan contra referencias oficiales de .NET 3.5, con advertencias
tratadas como errores. Pasan tres baterías adicionales del perfil CPU congelado.
Se conservan 27 huellas de textos y 96 huellas de aurora contra el anterior.

Permanecen documentados los fallos previos de `atlasgpu` (comparación GDI,
reproducido contra `8814afa`) y `cielohondo` (maquetación bajo Mono, reproducido
contra `0a3f7b0`). No se repitieron en esta ronda ni cuentan entre las 37 aprobadas.
No se afirma que pase toda la suite ni que se haya probado Windows XP real.

## Mediciones aisladas

Dos rondas con orden de versiones invertido, sin compilaciones ni pruebas
simultáneas. Cada caso tiene 31 lotes de 1.000 parejas de pasadas, tras tres lotes
de calentamiento. Se usan delegados, sin reflexión ni captura de bytes dentro
del tramo cronometrado. Medianas en **microsegundos por pareja de pasadas**:

| Resolución / movimiento | Anterior R1 / R2 | Actual R1 / R2 |
|---|---:|---:|
| 1024×768 / apagado | 3.477 / 3.933 | 2.519 / 2.523 |
| 1024×768 / cada cuadro | 3.657 / 4.121 | 3.156 / 3.174 |
| 1024×768 / fase repetida | 3.622 / 4.085 | 2.838 / 2.866 |
| 1280×1024 / cada cuadro | 3.661 / 4.114 | 3.146 / 3.158 |
| 1920×1080 / cada cuadro | 3.655 / 4.117 | 3.148 / 3.155 |

En la primera ronda, el coste con movimiento continuo baja aproximadamente un
14%; con movimiento apagado, un 27%. La segunda ronda favorece más al cambio,
pero también sube el control sin polvo del anterior: de 0.377 a 0.421 µs en
1024×768, frente a 0.369/0.378 µs actuales. Esa variación limita la atribución
del porcentaje; no se presenta como una ganancia exacta del equipo real.
Los registros incluyen todos los casos, p95 de lotes y máximos, también los que
empeoran. El ahorro absoluto es pequeño y no depende del número de píxeles: no
reduce el trabajo de relleno de la GPU al subir la resolución.

## Regresión del cuadro completo

96 casos en tres resoluciones, estrella 2D/3D, rejilla/estado extendido, títulos
largos y nebulosas animadas. **34.656 cuadros aceptados, cero rechazados.**

| Medida entre los 48 casos de cada versión | Anterior | Actual |
|---|---:|---:|
| Mediana de p50 (ms) | 0.1225 | 0.1220 |
| Mediana de p95 (ms) | 0.1695 | 0.1755 |
| Mediana de p99 (ms) | 1.5760 | 1.4765 |
| Mayor intervalo observado (ms) | 4.717 | 5.440 |

Resultado mixto: p95 y máximo empeoran, aunque p99 baja. No demuestra una mejora
general de fluidez ni identifica la causa de las pausas. El medidor usa celdas
simples, no conecta el asistente ni los cubos vivos y no activa el cielo intermedio.
`MEDICION_SINTETICA.txt` conserva los registros completos de ambas versiones.

Linux/Mono CLR 4 ejecuta binarios dirigidos a .NET 3.5. No hay GPU, monitor ni
música reales: los tiempos no son FPS de la rocola. El beneficio en el AMD A4 y
la fluidez a mayor resolución siguen pendientes de comprobar en el equipo real.
