# Gráficos generales con menos trabajo — Modest, 18-10-2026

Destino: AMD A4-6300, dos núcleos, 3,5 GB de RAM, Windows XP/.NET 3.5.
Solo `claude/modest-carson-fa2v2n`; referencia anterior `3e8988e`.

## Revisión del conjunto

Se revisaron la preparación del cuadro, las subidas de texturas y las rutas de
fondos, portadas/cubos, transiciones, paneles, textos, estrella, asistente y
adornos de reproducción. Esta revisión aplica siete ahorros en funciones
compartidas y en varias familias; no añade otra optimización exclusiva de aurora.

| Familia revisada | Resultado de esta revisión |
|---|---|
| Paneles, etiquetas y textos deslizantes | Cuadriláteros con cuatro esquinas calculadas, dos copias y sin segunda pasada de color. |
| Ventanas y transiciones texturizadas | Usan la misma construcción de cuadriláteros optimizada; conservan recorte, UV, transparencia y orden. |
| Portadas y selección de rejilla | Un coeficiente exponencial de atenuación por rejilla en lugar de uno por portada. Sin rejilla se omite ese cálculo. |
| Cubos, carga y caché de portadas | Se conserva la ruta existente; comprobados 512 cubos, vértices/UV/color/orden y un envío por cubo. No se cambia el regulador de carga. |
| Halos, brillos y efectos de fondo | Ocho puntos calculados por halo en lugar de dieciocho; se envían los mismos dieciocho vértices. |
| Colores animados de estrella y espectro | Con peso de portada cero se calcula solo género; con peso uno, solo portada. En un fundido intermedio siguen ambas. |
| Ecualizador visual de reproducción | Dos coeficientes compartidos de subida/bajada en lugar de cuatro exponenciales por paso. No toca el sonido. |
| Asistente y guía | Sin los dos arreglos temporales al buscar esquina; el borde de cada disco de manos/pies se calcula una vez en lugar de doce. Conserva cuerpo, gestos y ausencia de rostro. |
| Nebulosas, grano, viñeta y cielo intermedio | Se mantienen las cachés, recortes, presupuestos y ajustes existentes; no se modifica su calidad en esta tanda. |

Son **siete ahorros automáticos**: cuadriláteros, halos, atenuación de rejilla,
ecualizador visual, paleta, posición de guía y discos del asistente. No añaden
memoria persistente, texturas ni bitmaps; la guía elimina dos arreglos de cuatro
enteros por consulta. Las demás mejoras reutilizan valores locales o vértices
ya escritos. No cambian el número de envíos de dibujo ni su área de relleno.

No se sustituyen funciones trigonométricas por aproximaciones ni se cambia la
resolución nativa de textos/portadas. Los dieciocho vértices de halo y los seis
de cada cuadrilátero se conservan, incluidos sus duplicados y el ajuste de
medio píxel de Direct3D 9. La paleta sigue avanzando con «Colores fluidos»
apagado: los ecos de estrella también la consultan.

Al no alterar aspecto ni funcionalidad, no necesitan interruptores. Se conservan
los trece ajustes de Performance, sus dependencias y estados en gris. Audio,
reproducción, IAS, datos y renderizador CPU quedan sin cambios.

## Comparación con el release anterior

La nueva batería `graficosgenerales` carga los dos ejecutables de forma
independiente y comprueba:

- 5.000 cuadriláteros coloreados y 5.000 blancos con posiciones, tamaños, UV y
  colores variados; 5.000 halos y 5.000 discos de asistente. Compara todos los
  bytes de los buffers, incluidos prefijos/sufijos que no deben escribirse.
- 6.000 destinos de guía y su seguimiento, desde tamaños mínimos hasta 4K,
  incluidos objetivos vacíos, negativos y parcialmente fuera de pantalla.
- 2.000 pasos de rejilla y 2.000 de ecualizador visual, con cambios de selección,
  atenuación, música e intervalos cero, negativos y NaN.
- 1.000 pasos de paleta con pesos cero/uno/intermedios, portadas cambiantes,
  aurora presente/ausente y «Colores fluidos» encendido/apagado. Compara colores,
  interpolación, fase y pesos.

Resultado: **76.013 comprobaciones y 67.920.000 bytes comparados**, sin diferencias.
Sin segundo ejecutable, la batería verifica el contrato básico del cuadrilátero
y avisa expresamente que omite la comparación independiente.

El ZIP se comprueba con **32 baterías relacionadas sobre su propio Rocola.exe**:
las 30 anteriores, `graficosgenerales` con referencia `3e8988e` y `cuboagrupado`.
También se conservan las comparaciones de perfiles, poses 3D y ecos anteriores.
`VALIDACION.txt` identifica el ejecutable mediante SHA256.

GPU y CPU compilan contra referencias oficiales de .NET Framework 3.5, sin
advertencias. Pasan además `estadoeficiente`, `ajustesvivos` y `fichaportada`
contra CPU. El script Windows admite `-PreviousRelease` para las comparaciones
independientes y `pruebas/todo.sh` incorpora la nueva batería.

Permanece documentado el fallo previo de maquetación de `cielohondo` bajo Mono,
reproducido anteriormente también en `0a3f7b0`; no se afirma que pasen todas las
baterías del repositorio.

## Medición por familias

Microbenchmarks por delegado, sin reflexión en el tramo medido: 31 lotes de
1.000 llamadas, después de calentar. Dos rondas invierten el orden de los
programas. Las cifras son **microsegundos por operación**, promedios por lote,
no latencias de cuadro ni FPS. No se ejecutaron otras pruebas ni compilaciones
simultáneas.

| Operación | Mediana anterior R1 / R2 | Mediana actual R1 / R2 |
|---|---:|---:|
| Cuadrilátero | 0.032 / 0.054 | 0.027 / 0.027 |
| Halo | 0.159 / 0.159 | 0.096 / 0.096 |
| Disco del asistente | 0.617 / 0.634 | 0.599 / 0.567 |
| Posición de guía | 0.158 / 0.156 | 0.120 / 0.120 |
| Atenuación de 36 casillas | 0.512 / 0.513 | 0.122 / 0.122 |
| Paso de ecualizador visual | 0.147 / 0.147 | 0.135 / 0.137 |
| Paleta, solo género | 4.876 / 4.926 | 2.435 / 2.487 |
| Paleta, mezcla al 50% | 4.985 / 4.845 | 4.835 / 4.913 |
| Paleta, solo portada | 4.828 / 4.865 | 2.444 / 2.440 |

En estas pruebas, los halos cuestan alrededor de un 40% menos, la atenuación de
rejilla un 76% menos y las paletas de un solo origen cerca de la mitad. La paleta
al 50% sigue haciendo el mismo trabajo útil y no muestra un ahorro consistente.
Los costes absolutos son pequeños: estos porcentajes **no se suman ni representan
una mejora equivalente de FPS**.

## Cuadros completos y límites

Comparación con los mismos dos perfiles gráficos en ambas versiones, estrella
2D/3D, títulos largos, rejilla/estado extendido y nebulosas animadas. Resoluciones
1024×768, 1280×1024 y 1920×1080. Dos rondas, orden invertido; 360 intervalos
Start→Start por caso.

**96 casos, 34.656 cuadros aceptados y cero rechazados.**

| Medida entre los 48 casos de cada versión | Anterior | Actual |
|---|---:|---:|
| Mediana de p50 (ms) | 0.1265 | 0.1230 |
| Mediana de p95 (ms) | 0.1915 | 0.1905 |
| Mediana de p99 (ms) | 1.4240 | 1.4160 |
| Mayor intervalo observado (ms) | 4.948 | 7.112 |

Las medianas bajan ligeramente, pero el mayor intervalo es peor. Esta medición
no demuestra eliminar tirones ni permite atribuir los máximos a una causa. Los
registros incluyen p95/p99/máximos y están en `MEDICION_SINTETICA.txt`.

Las pruebas se ejecutan en Linux/Mono CLR 4 sobre binarios destinados a .NET 3.5,
con controlador artificial: no miden una GPU real, el monitor ni audio. Falta
comprobar XP y el A4 del local; no se promete que una resolución mayor sea fluida.
