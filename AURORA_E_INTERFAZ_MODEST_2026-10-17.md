# Aurora e interfaz con menos cálculos — Modest, 17-10-2026

Destino: AMD A4-6300, dos núcleos, 3,5 GB de RAM, Windows XP/.NET 3.5.
Solo `claude/modest-carson-fa2v2n`. Referencia anterior: `b04fd38`.

## Ahorros automáticos, sin cambio visual

- **Geometría de capas quietas.** `DibujaTeselas` reutiliza la malla sin recortar
  de una imagen mientras coincidan posición, tamaño, coordenadas de textura y
  color. Sirve también a la interfaz y la barra compacta que usan este camino.
  Cada nuevo análisis tiene su propia malla; cambios, fundidos y desplazamientos
  la reconstruyen. Se conserva el buffer existente: solo se añade una firma de
  sus parámetros. No retiene imágenes ni añade otro buffer por capa.
- **Ondas compartidas de aurora.** Altura y brillo necesitaban siete senos por
  columna; dos eran idénticos. Se calculan cinco y se usan en ambas expresiones,
  manteniendo precisión, orden de las sumas y límites. Son 290 llamadas a seno
  menos por muestra de las 145 columnas. No cambia la cadencia ni las bandas
  musicales; las ondas de reposo adicionales siguen siendo las mismas.

Las cachés de recortes del release anterior siguen separadas: recortar, aplicar
viñeta por vértices o dibujar en el cielo intermedio no altera la malla original.
Los textos y las portadas conservan su resolución nativa. Audio, reproducción,
controles y renderizador CPU no se modifican.

## Opción visual: Halo de aurora compacto

En **Ctrl+F3 → Imagen → Performance**. Reduce el ancho del resplandor de la cresta
de 26 a 8 píxeles y elimina una de sus cuatro bandas. Conserva exactamente el
núcleo luminoso, la cortina, la línea del horizonte y el reflejo existente.

| Malla de brillo (no toda la aurora) | Normal | Halo compacto |
|---|---:|---:|
| Todas las columnas | 7.992 vértices | 6.264 vértices |
| Con «Aurora con menos columnas» | 3.996 vértices | 3.132 vértices |

Son **21,6% menos vértices en esta malla**. La franja de la cresta ocupa 69,2%
menos área geométrica antes de recortes y ocultaciones. Estas cifras no son un
porcentaje de mejora de FPS ni del coste total del cuadro.

- Apagado de fábrica; clave persistente `aurora_halo_compacto`.
- Los presets Bajo, Medio y Performance, y «Solo Performance», lo activan.
  Alto y «Gráficos como de fábrica» lo apagan.
- Al actualizar una configuración anterior, el nuevo ajuste queda apagado.
  Para usarlo, encender su interruptor o volver a aplicar el preset.
- Sin aurora, queda en gris: «Necesita: Aurora del horizonte en Medio o Alto».
  Sin GPU, queda en gris junto al aviso común de tarjeta. En ambos casos conserva
  su valor guardado; al restaurar el requisito vuelve a estar disponible.
- Compatible con menos columnas, sin velo inferior, estado opaco y ondas suaves.
  No necesita desactivar esos ajustes; no se bloquean controles útiles.
- Se aplica al instante, incluso entre muestras de la animación. Al apagarlo se
  recupera la malla normal. No cambia ni vuelve a reservar el relleno.

La lista desplazable de Performance contiene ahora trece ahorros.

## Comprobaciones

`auroraeficiente`: 244 comprobaciones. Incluye 14.000 muestras de altura/brillo
comparadas bit a bit con las fórmulas anteriores; coincidencia del horizonte y
núcleo; activación y restauración entre muestras; combinaciones de columnas y
suelo; igualdad de envíos de la caché, invalidación de todos sus parámetros y
convivencia con recortes, viñeta y cielo intermedio. Resoluciones: 1024×768,
1280×1024 y 1920×1080.

`estadoeficiente`: 200 comprobaciones, incluyendo persistencia, independencia,
presets y acceso por desplazamiento a los trece interruptores. `ajustesvivos`:
97 comprobaciones, incluyendo gris, motivo y conservación del halo compacto.

El release se valida con 22 baterías relacionadas sobre el ejecutable empaquetado:
`auroraeficiente`, `trabajovisible`, `campoestelarbuffers`, `dibujoligero`,
`resolucionligera`, `estadoeficiente`, `performance`, `rendercache`, `franjareloj`,
`panelreposo`, `fluidezcuadros`, `estrellaflujo`, `estrella3d`, `estelaestelar`,
`ajustesvivos`, `fichaportada`, `caratulasjpeg`, `hojeofino`, `papeleraportadas`,
`asistentecombo`, `asistentevisual` y `ayudaguia`. El ZIP registra los resultados
y el SHA256 del ejecutable en `VALIDACION.txt`.

GPU y CPU compilan con referencias oficiales de .NET 3.5 y advertencias como
error; se comprueban además `estadoeficiente`, `ajustesvivos` y `fichaportada`
contra CPU. El guion Windows `pruebas/validar-estado-extendido.ps1` y el guion
Linux `pruebas/todo.sh` incluyen la nueva prueba.

Limitación conocida anterior: `cielohondo` tiene una aserción de maquetación
que falla bajo Mono, reproducida anteriormente también en `0a3f7b0`. No se
presenta esta selección como una aprobación de todas las pruebas del repositorio.

## Comparación sintética

Se comparan `b04fd38`, los ahorros automáticos nuevos y los mismos ahorros con
halo compacto. Mismos siete ajustes anteriores activos en los tres casos.
Dos rondas con orden invertido, tres resoluciones, estrella 2D/3D, rejilla
estática/estado extendido y fases de nebulosas que cambian 24 de cada 60 cuadros
simulados. Cada escenario registra 360 intervalos Start→Start, p95/p99/máximo,
cuadros aceptados y fallos. No se ejecutan otras pruebas durante la medición.

El controlador es artificial: no hace trabajo de GPU ni espera al monitor;
no hay audio ni entrada del usuario. El medidor tampoco recrea toda la interfaz
real ni activa el cielo intermedio. No mide los FPS de XP ni permite prometer
fluidez a 1080p. El coste real y la resolución viable siguen pendientes de medir
en el AMD A4 del local, con la biblioteca estabilizada y música real.

Se completaron 72 escenarios: 25.992 cuadros aceptados y ninguno rechazado.
No hubo nuevas texturas ni mapas durante los intervalos medidos. Las latencias
no mejoran de manera uniforme entre rondas: estos resultados no demuestran una
reducción del p99 o del máximo en la máquina real.

Estado extendido con estrella 3D. Cada celda indica **p95 / p99 / máximo en ms**:

| Resolución / ronda | Anterior | Automáticos | Automáticos + halo |
|---|---:|---:|---:|
| 1024x768 / 1 | 0.310 / 2.232 / 4.137 | 0.137 / 1.917 / 2.128 | 0.131 / 1.797 / 2.084 |
| 1024x768 / 2 | 0.121 / 1.677 / 2.220 | 0.156 / 1.901 / 2.167 | 0.133 / 1.830 / 2.444 |
| 1280x1024 / 1 | 0.239 / 2.316 / 3.163 | 0.190 / 3.052 / 3.573 | 0.200 / 2.217 / 3.292 |
| 1280x1024 / 2 | 0.237 / 2.519 / 3.050 | 0.140 / 2.209 / 3.070 | 0.147 / 2.256 / 3.091 |
| 1920x1080 / 1 | 0.182 / 2.608 / 4.171 | 0.209 / 2.921 / 4.576 | 0.216 / 2.575 / 4.823 |
| 1920x1080 / 2 | 0.191 / 2.579 / 4.106 | 0.239 / 2.869 / 4.062 | 0.167 / 2.592 / 4.129 |

Los resultados completos van en `MEDICION_SINTETICA.txt` del ZIP.
