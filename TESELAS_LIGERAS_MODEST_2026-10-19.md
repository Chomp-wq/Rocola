# Análisis de transparencias con recorrido ordenado — Modest, 19-10-2026

Referencia: `f234c2c`, rama `claude/modest-carson-fa2v2n`.
Destino: Windows XP/.NET 3.5, AMD A4-6300, dos núcleos y 3,5 GB de RAM.

## Cambio automático

El análisis de transparencias reúne las teselas visibles en rectángulos para
omitir las zonas vacías del cielo y de la interfaz. Antes, cada tramo de una
fila volvía a buscar desde el principio en todos los tramos de la fila anterior.
Con muchos huecos pequeños, esa búsqueda podía crecer cuadráticamente.

Ambas listas ya están ordenadas de izquierda a derecha y sus tramos son
disjuntos. Ahora un cursor avanza por la fila anterior y descarta los tramos
que quedan a la izquierda. Cada candidato se visita como máximo una vez por
fila: la búsqueda pasa de O(actuales × anteriores) a O(actuales + anteriores).
Solo se unen los tramos cuyos dos extremos coinciden, como antes.

Se aplica tanto a `TeselasVisibles.De` como al trabajo repartido entre cuadros,
`TrabajoTeselas.Avanza`, mediante el mismo método `TramoAnterior`. El cursor
se reinicia al comenzar cada banda. No añade campos, arreglos, cachés ni reservas
por cuadro. Conserva las listas existentes, el presupuesto de tiempo y las
unidades de trabajo del análisis incremental.

No cambia la máscara de transparencia, su margen de filtrado, el orden de
rectángulos, la geometría final ni el criterio para usar el recorte. No reduce
resolución, calidad ni funcionalidad. Es automático y sin interruptor; los trece
ajustes de Performance y sus dependencias permanecen vigentes. No se modifica
reproducción, sonido, precarga, datos del usuario ni renderizador CPU.

## Comparación independiente

`pruebas/teselasligeras.cs` compara con el ejecutable anterior:

- 512 máscaras de nueve bloques, lados de tesela 1/2/4/16 y borde forzado o libre.
- Imágenes vacías, opacas, con columnas separadas, con tramos cambiantes y aleatorias.
- Dimensiones 1×1, 17×33, 257×129, 1024×768, 1280×1024 y 1920×1080.
- Ejecución completa e incremental, presupuesto mínimo de un tick y de 1 ms,
  presupuesto no positivo y consulta de un trabajo ya terminado.
- Dimensiones, número y orden de rectángulos, fracción visible y bytes de vértices.

**80.105 comprobaciones, 4.216 casos y 130.171.352 bytes comparados, sin diferencias.**
Sin segundo ejecutable, informa expresamente que omite la comparación
independiente y realiza 25.297 comprobaciones entre rutas y contratos.

El script Windows acepta `-PreviousRelease` y `todo.sh` incluye la nueva batería.
El ZIP se valida con **36 baterías relacionadas sobre el Rocola.exe entregado**;
`VALIDACION.txt` enumera los resultados e identifica el binario mediante SHA256.
GPU y CPU compilan contra referencias oficiales de .NET 3.5, con advertencias
tratadas como errores. Pasan además tres baterías del perfil CPU congelado.

### Particularidad del entorno de prueba

La sonda incluida reproduce que libgdiplus modifica el origen tras desbloquear
una región solicitada con `ImageLockMode.ReadOnly`: un píxel de la imagen
sintética pasa de `01000000` a `00123456`. Ocurre tanto con esta versión como con
`f234c2c`. Reutilizar el mismo bitmap para dos análisis consecutivos hacía que
el segundo recibiese otra imagen; por eso cada análisis incremental recibe
una copia independiente. En las mediciones, esa copia se prepara fuera del
tramo cronometrado. No se añade ninguna copia a Rocola ni se modifica aquí la
ruta GDI de producción. Este comportamiento del entorno no demuestra un fallo
equivalente en Windows XP.

Permanecen documentados los fallos previos de `atlasgpu` (comparación GDI,
reproducido en la ronda anterior contra `8814afa`) y `cielohondo` (maquetación
bajo Mono, reproducido anteriormente contra `0a3f7b0`). No se repitieron en esta
ronda ni cuentan entre las 36 aprobadas. No se afirma que pase toda la suite.

## Mediciones

Dos rondas con orden de versiones invertido, sin compilaciones ni baterías
simultáneas. 21 mediciones por patrón/resolución/ruta después de calentamiento.
La ruta incremental suma todo el análisis, usando presupuestos de 0,5 ms;
no representa la duración de un cuadro. Los patrones sintéticos incluyen
vacío, opaco, columnas separadas y tramos cambiantes. No hay GPU ni música real.

Caso fragmentado: columnas de tres píxeles cada doce, teselas de dos píxeles.
Medianas en **milisegundos por análisis completo**, no FPS:

| Resolución / ruta | Anterior R1 / R2 | Actual R1 / R2 |
|---|---:|---:|
| 1024×768 / completa | 9.1545 / 9.1087 | 2.6210 / 2.6024 |
| 1024×768 / incremental total | 9.7688 / 9.9190 | 3.2163 / 3.2692 |
| 1280×1024 / completa | 17.3461 / 17.6823 | 4.4073 / 4.3411 |
| 1280×1024 / incremental total | 18.6573 / 19.1081 | 5.1423 / 5.1827 |
| 1920×1080 / completa | 36.2967 / 36.2342 | 6.9995 / 6.9759 |
| 1920×1080 / incremental total | 39.7911 / 39.7209 | 8.1743 / 8.3601 |

En este caso exigente, el análisis completo cuesta aproximadamente un 71–81%
menos y el incremental total un 67–79% menos. No se extrapolan estos porcentajes
a la navegación, a todas las imágenes ni al FPS: el análisis se hace al crear
las máscaras, no en cada dibujo de una máscara ya preparada.

Los casos sencillos no tienen esa ganancia. Por ejemplo, el incremental opaco
1920×1080 pasa de 5.6189/5.7067 a 5.7287/5.6184 ms: diferencias pequeñas y mixtas,
incluido un aumento cercano al 2% en la primera ronda. Los registros completos
incluyen todos los patrones, p95 y máximo; no solo el caso favorable.

## Regresión del cuadro completo

96 casos en tres resoluciones, estrella 2D/3D, rejilla/estado extendido, títulos
largos y nebulosas animadas. **34.656 cuadros aceptados, cero rechazados.**
El medidor usa máscaras ya preparadas: comprueba la continuidad del dibujo,
no el ahorro de construirlas. Ese coste se mide por separado arriba.

| Medida entre los 48 casos de cada versión | Anterior | Actual |
|---|---:|---:|
| Mediana de p50 (ms) | 0.1220 | 0.1225 |
| Mediana de p95 (ms) | 0.1655 | 0.1585 |
| Mediana de p99 (ms) | 1.2765 | 1.2320 |
| Mayor intervalo observado (ms) | 4.870 | 5.313 |

Resultado mixto; el máximo empeora aunque algunas medianas bajen. No demuestra
una mejora general de fluidez ni identifica la causa de las pausas.
`MEDICION_SINTETICA.txt` conserva todos los registros de ambas versiones.

Linux/Mono CLR 4 ejecuta binarios dirigidos a .NET 3.5; no reproduce Windows XP,
la GPU, el monitor ni la música del local. El beneficio en el AMD A4 y la fluidez
a mayor resolución siguen pendientes de comprobación en la rocola real.
