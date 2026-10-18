# Cubos y textos largos reutilizados — Modest, 18-10-2026

Destino: AMD A4-6300, dos núcleos, 3,5 GB de RAM, Windows XP/.NET 3.5.
Solo `claude/modest-carson-fa2v2n`; referencia anterior `eaba9d0`.

## Cambios automáticos

**Cubos de portadas.** El dispositivo conserva hasta cuatro poses con su
geometría y sombreado. Una pose se reutiliza únicamente si coinciden posición,
tamaño, giro, inclinación, UV y modo con/sin textura; el relleno sin portada
incluye también su color en la clave. Cambiar la opacidad actualiza sus vértices
y la textura se selecciona en cada dibujo. No se conservan referencias a
texturas ni bitmaps en la caché. El orden de caras y los redondeos no cambian.

Esto evita reconstruir una pose entre muestras de animación y al dibujar dos
portadas con la misma pose durante un fundido. No cambia la cadencia de cubos:
respeta el ajuste existente. Las cuatro entradas se reciclan, sin crecer al
navegar. Contienen 2.688 bytes de vértices más metadatos y cabeceras (menos de
4 KiB en conjunto). No hay reservas por cuadro una vez ocupadas las entradas.

**Títulos largos divididos.** Los fragmentos de un título comparten su
desplazamiento cuando coinciden tick, inicio de animación y exceso de ancho.
Cambiar cualquiera de ellos recalcula la posición. Se usa tanto al comprobar
visibilidad de texturas pendientes como al dibujar. Los títulos de hasta 1.024
píxeles conservan la fórmula directa, sin consultar esta caché.

Se añaden 20 bytes de campos por título, antes de alineación; el límite existente
de cinco grupos de 48 títulos acota esos campos a 4.800 bytes. No hay nuevas
texturas, copias de portadas ni arreglos por cuadro para este ahorro.

Ambos cambios son automáticos y conservan aspecto y funcionalidad. No añaden
interruptores ni cambian las dependencias de los trece ajustes de Performance.
Textos/portadas siguen a resolución nativa. No se modifica sonido, reproducción,
IAS, datos del usuario, rostro del asistente (sigue sin rostro) ni renderizador CPU.

## Verificación

`cubosligeros` compara el ejecutable actual contra `eaba9d0`:

- 3.072 dibujos en 1024×768, 1280×1024 y 1920×1080, alternando cuatro cubos,
  poses repetidas, opacidades y texturas.
- 36 cambios de claves y visibilidad: posición, tamaño, UV, ángulos, presencia
  de portada, color del relleno y opacidades cero/superiores a uno.
- Reutilización de cuatro poses sin congelar textura ni transparencia, reciclado
  de las cuatro entradas y comportamiento con dispositivo perdido/recuperado.
- 25.800 desplazamientos de textos, incluidos anchos e inicios cambiantes en el
  mismo tick, silencio inicial del desplazamiento y desbordamiento del reloj.

Resultado: **41.387 comprobaciones y 1.015.896 bytes comparados**, sin diferencias.
También coinciden **27 huellas de textos** (vértices, estados y píxeles de las
texturas) con el anterior. Las pruebas específicas de textos conservan 447 y
631 comprobaciones aprobadas, respectivamente.

Sin referencia anterior, `cubosligeros` verifica la caché y compara posiciones
de texto con la fórmula conservada; informa expresamente que omite la comparación
de cubos contra otro ejecutable. El script Windows admite `-PreviousRelease` y
`pruebas/todo.sh` incorpora la batería.

El ZIP se valida con **33 baterías relacionadas sobre su propio Rocola.exe**:
las 32 anteriores más `cubosligeros` con referencia independiente. Incluye las
comparaciones de gráficos generales, poses de estrella, perfiles de aurora y
textos. `VALIDACION.txt` identifica el ejecutable mediante SHA256.

GPU y CPU compilan con referencias oficiales de .NET 3.5, sin advertencias.
Pasan además `estadoeficiente`, `ajustesvivos` y `fichaportada` contra CPU.
Permanece documentado el fallo previo de maquetación de `cielohondo` bajo Mono,
reproducido anteriormente también en `0a3f7b0`; no se afirma que pase toda la
suite del repositorio.

## Medición de las rutas modificadas

Microbenchmark por delegado, sin reflexión ni copia de capturas en el tramo
medido. 31 lotes de 1.000 cuadros sintéticos, después de calentar: tres cubos,
poses avanzadas a 60/30/24 muestras por 60 cuadros y, opcionalmente, un fundido
de dos portadas en el primer cubo. Dos rondas invierten el orden de versiones.
Se conserva el mismo número de envíos: la prueba no mide una GPU real.

Medianas en **microsegundos por cuadro sintético de cubos**, no FPS:

| Cadencia de pose / fundido | Anterior R1 / R2 | Actual R1 / R2 |
|---|---:|---:|
| 60 / no | 1.339 / 1.399 | 1.445 / 1.445 |
| 60 / sí | 1.783 / 1.871 | 1.549 / 1.548 |
| 30 / no | 1.332 / 1.378 | 0.859 / 0.862 |
| 30 / sí | 1.775 / 1.834 | 1.003 / 0.999 |
| 24 / no | 1.324 / 1.365 | 0.748 / 0.750 |
| 24 / sí | 1.818 / 1.824 | 0.885 / 0.887 |

Con poses repetidas, el caso sin fundido cuesta aproximadamente un 35–45% menos.
**Cuando las tres poses cambian en cada cuadro y no hay fundido, no hay ahorro:**
consultar/actualizar la caché añade 0.046–0.106 µs al conjunto en estas mediciones
(aproximadamente 3–8%). Se conserva este coste explícito al evaluar la mejora;
no se presenta la caché como una aceleración universal.

El desplazamiento de un título dividido en cuatro partes pasa de **0.118 a
0.065 µs** en ambas rondas, aproximadamente un 45% menos en esa operación.
Los títulos de una sola textura conservan la ruta directa. Estos porcentajes
no se suman ni se interpretan como una mejora equivalente de FPS.

## Cuadros completos y límites

El medidor completo usa estrella 2D/3D, títulos largos, nebulosas animadas y
rejilla/estado extendido en las tres resoluciones. Dos perfiles iguales entre
versiones y dos rondas con orden invertido. Este medidor no conecta cubos vivos;
su coste se mide por separado arriba. Registra intervalos Start→Start,
p50/p95/p99/máximo, aceptados y rechazados.

**96 casos, 34.656 cuadros aceptados y cero rechazados.**

| Medida entre los 48 casos de cada versión | Anterior | Actual |
|---|---:|---:|
| Mediana de p50 (ms) | 0.1245 | 0.1220 |
| Mediana de p95 (ms) | 0.1850 | 0.1825 |
| Mediana de p99 (ms) | 1.2795 | 1.5955 |
| Mayor intervalo observado (ms) | 7.382 | 6.101 |

La comparación completa es mixta: p99 empeora aunque otras medidas bajen.
No demuestra eliminar tirones ni permite atribuir los máximos a una causa.
Los registros completos incluyen p95/p99/máximos en `MEDICION_SINTETICA.txt`.

Las mediciones se hicieron sin pruebas ni compilaciones simultáneas. Linux/Mono
CLR 4 ejecuta binarios destinados a .NET 3.5; no equivale a probar Windows XP,
GPU, monitor ni audio reales. La fluidez a mayor resolución sigue pendiente de
comprobar en la rocola del local.
