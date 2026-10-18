# Portadas y atlas con menos cálculos — Modest, 18-10-2026

Referencia: `8814afa`, rama `claude/modest-carson-fa2v2n`.
Destino: Windows XP/.NET 3.5, AMD A4-6300, dos núcleos, 3,5 GB de RAM.

## Cambios automáticos

**Portadas nativas.** La malla de nueve parches que suaviza el borde calcula
una sola vez sus 16 esquinas distintas. Después copia esas esquinas al mismo
buffer final de 54 vértices. Antes calculaba las 54, repitiendo coordenadas, UV,
ajuste de medio píxel y decisión de transparencia. Conserva los 18 triángulos,
el orden de envío y el borde suave; no reduce la resolución de la portada.

Se añade un buffer de 16 vértices por panel: **448 bytes de datos** más cabecera
de arreglo y referencia. Se crea una vez y se reutiliza entre todas las celdas;
no crece al navegar ni reserva memoria por cuadro. No contiene bitmaps ni
referencias a texturas. Sus valores se recalculan en cada portada: no congela
posiciones, tamaños, color o transparencia.

**Atlas de textos y bordes.** Cada rectángulo calcula cuatro esquinas, escribe
su color y copia las dos compartidas. Elimina dos cálculos y el recorrido
posterior de seis colores por rectángulo. Sirve tanto para atlas completos
como para etiquetas y contornos separados. Conserva el muestreo del centro de
la fila en los bordes verticales y el ocultamiento de portadas pendientes.

Ambos cambios quedan activos siempre, sin opciones nuevas ni cambios visuales.
Los trece ajustes de Performance y sus dependencias permanecen vigentes.
No se modifica el regulador, los presupuestos de carga, los hilos de precarga,
la reproducción, el audio, los datos ni el renderizador CPU congelado.

## Verificación

`pruebas/rejillaligera.cs` compara el envío real de `DrawNativeCover` y la salida
de `AtlasCellVertices` con el ejecutable anterior mediante un dispositivo
artificial. 3.000 portadas y 3.000 atlas en 1024×768, 1280×1024 y 1920×1080:
posiciones fraccionarias, tamaños fuente cambiantes, UV, color/transparencia,
atlas completos/separados, portada pendiente, relleno y ocultamiento.

**25.800 comprobaciones, 7.156.800 bytes comparados, sin diferencias.**
Sin segundo ejecutable informa expresamente la omisión de la comparación;
solo comprueba 6.000 condiciones de envío y recuento de triángulos.
El script Windows acepta `-PreviousRelease`; `todo.sh` incluye la prueba.

El paquete se verifica con 35 baterías relacionadas sobre el propio Rocola.exe;
`VALIDACION.txt` enumera las aprobadas e identifica el binario mediante SHA256.
Incluye comparaciones independientes de poses, cubos, gráficos generales,
colores, textos y aurora. GPU y CPU compilan con referencias oficiales de
.NET 3.5 y advertencias tratadas como errores. Pasan además tres baterías CPU.

**Fallo previo comprobado en esta ronda:** `atlasgpu` falla en la comparación
GDI de imágenes: 3.260 píxeles distintos, máximo 255, a 1024×768. Se ejecutó
la misma prueba contra `8814afa` y produjo exactamente el mismo fallo. La
composición de bitmaps no cambia en esta revisión; esta prueba no cuenta entre
las 35 aprobadas. Se incluyen ambos registros en `MEDICION_SINTETICA.txt`.
No se ha determinado aquí su causa última ni se considera aprobada en Windows.

También permanece documentado el fallo previo de maquetación `cielohondo`
bajo Mono, reproducido anteriormente en `0a3f7b0`; no se repitió en esta ronda.
No se afirma que toda la suite del repositorio esté en verde.

## Medición aislada de la rejilla

Dos rondas invierten el orden de versiones. Por caso: calentamiento y 31 lotes
de 1.000 preparaciones de 36 celdas, mediante delegados tipados sin reflexión
ni copia de capturas dentro del tramo medido. Atlas mixtos (18 completos y 18
separados). La medición de portadas sí atraviesa su ruta de dibujo, con envíos
simulados; no hay GPU, monitor ni audio reales.

Medianas en **microsegundos por conjunto de 36 celdas**:

| Trabajo / resolución | Anterior R1 / R2 | Actual R1 / R2 |
|---|---:|---:|
| Portadas / 1024×768 | 19.579 / 19.794 | 17.563 / 17.397 |
| Portadas / 1280×1024 | 19.578 / 20.603 | 16.841 / 17.562 |
| Portadas / 1920×1080 | 19.515 / 20.224 | 16.845 / 17.110 |
| Atlas / 1024×768 | 10.734 / 11.264 | 9.631 / 10.256 |
| Atlas / 1280×1024 | 10.569 / 11.538 | 9.498 / 9.958 |
| Atlas / 1920×1080 | 10.598 / 11.198 | 9.553 / 9.924 |

Aproximadamente **10–15% menos tiempo en portadas** y **9–14% en atlas**.
Son ahorros pequeños de preparación CPU; no son porcentajes de FPS ni se suman.
La GPU sigue recibiendo los mismos triángulos y píxeles a resolución nativa.

## Regresión del cuadro completo

96 casos: tres resoluciones, estrella 2D/3D, rejilla y estado extendido, títulos
largos, nebulosas animadas, dos perfiles y dos rondas. **34.656 cuadros aceptados,
cero rechazados.** Este medidor conserva celdas simples; no activa las portadas
nativas ni los atlas modificados. Se usa como regresión general, mientras que
la ruta modificada se mide arriba. No conecta tampoco asistente ni cubos vivos.

| Medida entre los 48 casos de cada versión | Anterior | Actual |
|---|---:|---:|
| Mediana de p50 (ms) | 0.1220 | 0.1225 |
| Mediana de p95 (ms) | 0.1835 | 0.1820 |
| Mediana de p99 (ms) | 1.2245 | 1.2350 |
| Mayor intervalo observado (ms) | 5.460 | 5.375 |

Resultado prácticamente estable y mixto; no demuestra una mejora general de
fluidez. Se incluyen p95/p99/máximos y contadores completos en los registros.
Las mediciones se hicieron sin compilaciones ni baterías simultáneas.
Linux/Mono CLR 4 no reproduce Windows XP ni el controlador del AMD A4: el
beneficio en la rocola real y a mayor resolución sigue pendiente de comprobar.
