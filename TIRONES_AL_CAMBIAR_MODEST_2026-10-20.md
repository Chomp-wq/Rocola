# Tirones al cambiar de página, de canción y de modo — Modest, 20 de octubre de 2026

Destino: AMD A4-6300, dos núcleos, Windows XP / .NET Framework 3.5, solo la rama Modest (trabajado sobre 6261368). Petición: bajar los **peores cuadros** (pasar de página, cambiar de canción,
cambiar de modo), sin cambiar lo que se ve ni lo que se oye. Un investigador de solo lectura hizo una lista de candidatos; aquí está lo que se verificó en el código, lo que
se hizo, lo que no, con qué prueba y con qué números.

**Todas las cifras son de Linux/Mono con libgdiplus y con una tarjeta imitada que cuenta o pinta en software: sintéticas. No se midió nada en Windows XP, ni en el A4, ni con
un controlador Direct3D de verdad.** Donde un ahorro solo se puede ver en un controlador de verdad, el documento lo dice y lo único que se mide es que se llama lo que hay que llamar.

## Resumen

| # | Candidato | Estado | Resultado (Mono, sintético) |
| --- | --- | --- | --- |
| 0 | Libro de los peores cuadros (diagnóstico) | **hecho** | ocho peores por minuto con once fases; sin reservas por cuadro; encendido cuesta 0 µs medibles aquí |
| 1 | Precalentar lo que pinta la tarjeta | **hecho** | primera vez: estado extendido 37 → 5,2 ms, ficha 30 → 4,5, globos de la estrella 49 → 6,9, avisos 44 → 8,7; buscador 12,3 → 1,0; lista 4,3 → 1,8 |
| 2 | Bordes de la celda separada una vez por `(cw, side)` | **hecho** | 0,242 → 0,186 ms por celda a 1024×768 (−23 %); bytes iguales |
| 3 | `PreLoad` de las celdas de la página siguiente | **hecho** (sin medida posible) | solo se cuentan llamadas: una por textura subida por el adelanto, ninguna en lo visible |
| 4 | Aplazar los pasos de color 1–2 cuadros | **no se hace** (decisión del coordinador) | cambia el tiempo de lo visible; ya lo cubre «Color directo del estado extendido» |
| 5 | Alfa en el formato propio del PArgb | **hecho en (a); (b) descartado** | (a) dentro del ruido aquí; (b) más lento aquí |
| 6 | Reciclar texturas | **no se hace** | ver «No hecho» |
| 7 | No invalidar `ProgressView` y etiquetas bajo la tarjeta | **no se hace** | no se puede confirmar que se pinten; propuesta para el A4 |
| 8 | `GetAvailableTextureMem` cada minuto, no cada 10 s | **hecho** | 18 → 3 llamadas en 180 s de página quieta |
| 9 | Atracción con escaparate: sin interfaz vacía | **hecho** | rehacer la imagen 0,235 → 0,016 ms; una textura, un bloqueo y 31 órdenes de dibujo menos |
| 10 | Atlas de vistas previas LOD | **no se hace** | ver «No hecho» |
| 11 | Sin memoria para una textura: evicción y espera | **hecho** | 100 peticiones seguidas: 100 → 2 llamadas a `CreateTexture` y 1 evicción |
| 12 | Cachear el tamaño de letra que cabe | **no se hace** | 0,02–0,05 ms por repetición aquí; no vale la complejidad |
| 13 | Repartir `BuildStars` | **no se hace** (opcional, poco valor) | — |
| 14 | Delegados de `Fn<>` guardados | **hecho** (el vigía de 25 → 50 ms **no**) | sobre funciones nativas: 35 recolecciones y 0,884 µs por llamada → 0 y 0,025 µs |
| A | Vista previa 32×32 que ya está en el almacén | **hecho** | 2,9 → 0,008 ms por portada que ya la tiene |
| B | Espectro del hilo de audio (`ReportAudioBlock`) | **hecho** | 4,79 → 3,83 ms por segundo de música (−20 %), huella igual bit a bit |

Comprobaciones comunes de los ahorros automáticos (sin interruptores nuevos: todos son automáticos y conservan el aspecto y la función):

- **Huellas de cuadro**: `huellacuadros` en modo `huella` — las 168 pantallas, con sus píxeles, el chorro bruto de órdenes, el canónico y las cuentas — **idénticas línea por línea** a las de
  la rama antes de los cambios (c29b73e) en cuatro puntos intermedios, y en el árbol final. La atracción con escaparate no está entre las 168 (ver 9).
- **Procesador congelado**: `cpucongelado` con el ejecutable previo (`head_cpu.exe`): 14 métodos, instrucciones y operandos idénticos tras cada cambio.
- **Compilación**: `-sdk:2 -langversion:3 -warn:4 -warnaserror+`, con y sin tarjeta, 0 avisos.
- **Comparación contra el ejecutable anterior** en `tironescambio` (batería nueva): cada ejecutable en **su propio proceso** (cada uno con su `.config`; dos ensamblados `rocola` en un
  solo proceso se confunden y `Assembly.Load(bytes)` pierde el `dllmap` de Mono), y sus resultados se comparan por huella. Además las huellas de antes del cambio van en archivos
  (`celdas_separadas_2026-10-20.txt`, `texturas_2026-10-20.txt`, `aurora_2026-10-20.txt`) sacadas con el ejecutable de c29b73e: son bytes de **este equipo** (libgdiplus), no de GDI+
  de Windows, y solo se comparan en Mono.

## 0. El libro de los peores cuadros

`FluidezGpu` dice cuánto duró un tirón; el libro dice **qué estaba haciendo el cuadro**. Solo con `diagnostico=1`.

`RenderFrame` reparte el tiempo del cuadro en once fases con marcas de reloj fino (`AhoraMs`, un `Stopwatch`), cada una es «lo que pasó desde la marca anterior»:

| fase | de dónde a dónde |
| --- | --- |
| `pre` | desde el principio del cuadro hasta pedir la rejilla (redimensionar, pulido) |
| `rejilla` | `GetGrid` (incluye hornear lo que se pida) |
| `portadas` | portadas nativas, texturas de celda, `PrepareLod`, expulsiones y recortes |
| `cubos` | contorno y portadas de los cubos |
| `estado` | construir y subir el panel del estado extendido |
| `vivos` | ventana del buscador, viaje, aviso, pantalla viva, disco vivo, suena vivo, atracción |
| `interfaz` | `SubeUi`, `SubeUiBot` y las piezas |
| `adornos` | textos en cinta, nieblas, viñeta, asistente, luna, cielo vivo |
| `adelanto` | `WarmAheadCells` |
| `dibujo` | `Begin` … justo antes de `End` |
| `presentar` | `End` / `Present` |

Se guardan los **ocho peores de cada minuto**. Puntos de un cuadro = trabajo (todas las fases menos `presentar`) + lo que `Present` espere **de más de un periodo** (esperar al monitor un
periodo entero es normal). Entra desde 12 ms. Al acabar el minuto sale **una** entrada de bitácora (por el hilo de la bitácora, como el resto) y otra al cerrar la rocola; si ningún cuadro
llegó a 12 ms, no sale nada.

### Cómo leerlo en el A4

```
peores cuadros de los ultimos 60 s: 7 de 3598 pasaron de 12.0 ms (trabajo + lo que Present espere de mas de un periodo); fases en ms, tiempo de reloj del hilo de la pantalla:
  1. hace 41 s: 63.2 ms (trabajo 61.0, presentar 2.2) | rejilla 2.2 portadas 40.1 estado 9.0 dibujo 9.7 | modo 0, celdas 18, texturas subidas 5, pagina,estado_subido | recolecciones en el cuadro 0/0/1 (coincidencia, no causa)
```

(Línea de ejemplo del formato, **no** una medida.) Se lee: el peor cuadro del minuto tardó 63 ms, 40 en `portadas` (subir cinco texturas), con la rejilla a la vista y el estado extendido subido.
Las banderas son: `pagina`, `estado_extendido`, `transicion_del_estado`, `interfaz_subida`, `franja_subida`, `cambio_de_modo`, `ventana_del_buscador`, `viaje_de_portada`,
`adelanto_de_portadas`, `estado_subido`. «Recolecciones en el cuadro» son las de gen 0/1/2 entre el principio y el final del cuadro: **coincidencia, no causa ni duración**. Los milisegundos son
de reloj de pared: si Windows le quita el procesador al hilo en medio de una fase, ese tiempo cuenta en ella. Para atribuir un tirón, cruzarlo con las líneas `pantalla parada … ms` y
con `episodios GPU` del mismo `rocola_diagnostico.txt`, y comprobar antes que el preanálisis de la biblioteca no sigue activo.

Sin diagnóstico cada marca es un `if` sobre un `bool`. Con él, memoria fija (ocho puestos y sus fases se crean con el panel): dos millones de cuadros pasados por el libro, con y sin entradas
nuevas, no provocan ni una recolección de gen 0. Un cuadro con la tarjeta imitada cuesta 65 µs apagado y 66 µs encendido (mediana de siete rondas alternadas).

## 1. Precalentar lo que pinta la tarjeta

**Lo que decía el candidato era cierto y más.** `PrecalientaUnPoco` calentaba solo caminos del procesador: el buscador con `forGpu = false` (que con tarjeta ni se pinta; la tarjeta pinta con
`true`, y con `Nuevo` por `RenderNuevo`), la lista de un disco sin la marca `pintandoGpu` (otro horneado y otro camino) y la pantalla de reposo del procesador. Lo que la tarjeta pinta de
verdad la primera vez pagaba compilar el código (JIT), crear familias de letra y pinceles, y los horneados, **dentro de un cuadro**.

Con tarjeta ahora, en `PrecalientaUnPoco` (`Form1.Portadas.cs`) y `PrecargaPaginaGpu.cs`:

- paso 0 y 1: el buscador con `forGpu = true`, y la lista con `pintandoGpu = true` (la marca vuelve como estaba); el paso 2 (reposo por el procesador) no cambia;
- pasos 3 a 6 nuevos, dibujando **una vez** con datos de relleno y en mapas de bits propios que se sueltan: el panel del estado extendido y su reloj (`RenderIdlePanelEn`, `UpdateIdleClock`,
  `UpdateIdleClockRegion`), la pastilla y la ficha del disco (`PintaFoco`, `PintaFicha`), los globos de la estrella (`PintaPanelAsistente`, `PintaAdiosAsistente`) y los avisos
  (`PintaAviso`, `PintaInvita`, la moneda y el «+4 créditos»).

No toca `idlePanelBmp`, `idleClockUnderlay`, el panel del asistente, los avisos ni la ficha/pastilla a la vista (la batería comprueba, leyendo el código ya compilado, que ni los lee ni los
escribe), y conserva las tres guardas: `TryEnter(precalTrabajoLock)`, `MsTrabajo + precalUltimoCoste > 28` y `PrecalQuieta`. Sin tarjeta, todo igual. 32 pasadas por cuatro pantallas dejan 12 KB.

Primera vez de cada pieza, **en un proceso nuevo** (mediana de 5, alternando fría y precalentada; ms):

| pieza | fría | precalentada |
| --- | --- | --- |
| estado extendido | 37,0 | 5,2 |
| pastilla y ficha | 30,0 | 4,5 |
| globos de la estrella | 48,9 | 6,9 |
| avisos y moneda | 43,5 | 8,7 |

Buscador por el camino de la tarjeta (por control, mediana de 7, proceso con el código ya compilado): 12,3 → 1,0 ms; lista del disco con la marca de la tarjeta al abrir un disco: 4,3 → 1,8 ms.
Los píxeles del buscador y de la lista son los mismos recién abiertos que precalentados. Las cifras de la primera vez incluyen el JIT de Mono, que no es el de .NET 3.5; en el A4 «hay que
multiplicar por 4 o 5» según `primera.cs`. **No se midió el efecto en cuadros reales ni en el A4.**

## 2. Bordes de la celda separada

`BakeSeparatedCell` dibujaba cuatro tiras de contorno por celda (un mapa de bits, un `Graphics` y un lápiz por tira, y otro `Graphics` para pegarla) que dependen solo de `(cw, side)`. `PegaBordes`
las dibuja una vez con el mismo `PackEdge` en un mapa de bits pequeño y transparente (una sola entrada, se rehace al cambiar la medida) y cada celda copia sus filas. Se comprobó que `PortadasLod`
no entra en los bordes (solo en `layout.HideBlank`), así que no hace falta invalidar por él.

Bytes: 24 celdas (6 pantallas × en cola o no × LOD), cada una horneada con los bordes dibujados y con los copiados, tienen el MD5 y el plano de antes del cambio. Tiempo: **0,242 → 0,186 ms por celda** a 1024×768
(mediana de 5 procesos alternados). Sin medir en XP, donde crear un `Graphics` cuesta más.

## 3. `PreLoad`

Se verificó la ranura contra el orden de `d3d9.h`: `IDirect3DResource9` = `IUnknown` (0–2), `GetDevice` 3, `SetPrivateData` 4, `GetPrivateData` 5, `FreePrivateData` 6, `SetPriority` 7, `GetPriority` 8,
**`PreLoad` 9**, `GetType` 10; coincide con la tabla ya usada (`LockRect` 19, `UnlockRect` 20, `GetSurfaceLevel` 18 en `IDirect3DTexture9`). `PreLoad` **devuelve `void`**, así que lleva su propio delegado
(`VoidSelf`). Y `EvictManagedResources` es la ranura 5 del dispositivo (`IDirect3DDevice9`: `TestCooperativeLevel` 3, `GetAvailableTextureMem` 4, `EvictManagedResources` 5), no 5 de un recurso.

Las texturas son `POOL_MANAGED`: crearlas y llenarlas deja la copia en memoria del sistema y el controlador la sube a la tarjeta la primera vez que un dibujo la usa, o sea en el cuadro del cambio de
página. `WarmAheadCells` (el hueco del adelanto, que ya sube la página siguiente) llama a `PreLoad` sobre lo que acaba de subir (etiqueta y portada nativa; las divididas, tira por tira) y cuenta lo que
tarda en `CoverControl.Spend` **sin** meterlo en el coste aprendido de una subida visible. Solo ahí, solo en un dispositivo creado de verdad (`preLoadOk`: una tarjeta imitada puede no tener la ranura), y si falla
una vez se apaga.

Con la tarjeta imitada: 24 texturas subidas por el adelanto, 24 llamadas; ninguna en las celdas a la vista; ninguna al repasar celdas ya subidas; ninguna sin habilitar. **El ahorro en el cuadro del cambio de
página solo se puede medir con un controlador de verdad.**

## 5. El alfa en el formato propio del PArgb

(a) `TeselasVisibles.De`, `TrabajoTeselas.Avanza` e `InteriorOpaco` pedían `Format32bppArgb` a mapas de bits PArgb solo para mirar el alfa, y GDI+ copia y des-premultiplica toda la zona. Ahora bloquean el PArgb **entero**
en su formato y leen las filas por desplazamiento. «Entero» a propósito: **se encontró un fallo de la libgdiplus de Linux** — un bloqueo de un trozo en el mismo formato escribe el trozo de vuelta arriba a la izquierda aunque
sea de solo lectura (corrompió bitmaps en la prueba); en Windows no pasa, pero el bloqueo entero vale en los dos. Resultados idénticos: 240 huellas (rectángulos, fracción, interior opaco, bytes de textura subida) de 6 tamaños × 8
clases de alfa. Tiempo: **dentro del ruido** aquí (según la tanda, de −24 % a +15 %; la copia que se evita es más cara en el GDI+ de Windows, sin medir).

(b) Subir sin convertir los PArgb totalmente opacos (`SubeBitmapBorde`): **se probó y se descartó**. En esta máquina mirar el alfa en código administrado cuesta más que la conversión de libgdiplus (estado extendido
opaco 1024×260: 2,06 → 2,25 ms).

## 8. `GetAvailableTextureMem`

Es una llamada síncrona al controlador (en XP puede tardar milisegundos dentro de un cuadro) y su resultado solo cambia si cambia la memoria de la tarjeta. Se pregunta al empezar, cada **minuto** (antes cada 10 s), al cambiar
la resolución y cuando una textura no se pudo crear (`Upload` y `CreateCoverShared` dejan pedido el sondeo; como mucho uno cada 2 s), y nunca con el estado extendido abierto o entrando/saliendo. Se conservan las guardas
de siempre (600 ms de asentarse, sin carga pendiente ni cambio de modo urgente). Con la tarjeta imitada que cuenta la ranura 4: 3 llamadas en 180 s de página quieta contra 18 del ejecutable anterior; 1 al cambiar la resolución;
no más de una cada 2 s con peticiones en cada cuadro; 0 con el estado extendido. **Lo que cuesta cada llamada en el A4 no se midió.** Cambio de semántica a conocer: el presupuesto de portadas tarda hasta un minuto en
enterarse de que otro programa se llevó memoria de la tarjeta (antes, 10 s).

## 9. La atracción con el escaparate

Con el escaparate los textos de la atracción son suyos (`AtraccionGpu.cs`) y `EnsureUiOverlay` reservaba, borraba y subía a la tarjeta una imagen transparente de ancho completo y casi todo el alto (2,7 MB a 1024×768, textura
de 1024×1024) cada vez que cambiaba su firma o la medida. Ahora `EnsureUiOverlay` avisa con la versión y con `uiOverlayVacio` y la tarjeta (`GetUiVacia`) suelta la textura y no sube, no analiza teselas y no dibuja nada de esa
imagen. Se conservó, con la misma condición de antes, **la interrogación de AYUDA**, y el fundido de paleta guarda su «textura vieja» vacía y su rampa de 420 ms igual. (No se dejó la textura vieja viva sin dibujarla: el
fundido de paleta y la ventana del buscador la habrían recogido con el contenido de otro modo.) Al salir o al abrir el buscador la imagen vuelve por el camino de siempre.

Pixel a pixel: no está entre las 168 pantallas. Lo que se quita es una textura **totalmente transparente** dibujada con mezcla normal, que no cambia ni un píxel por definición; el resto (interrogación, paleta) se comprobó por
código. Medido: rehacer la imagen 0,235 → 0,016 ms (sin la subida a la tarjeta, que aquí no se mide); en 31 cuadros, una textura y un bloqueo menos y 31 órdenes de dibujo menos.

## 11. Sin memoria para una textura

Si el controlador contesta `D3DERR_OUTOFVIDEOMEMORY` o `E_OUTOFMEMORY`, la misma textura se volvía a pedir en cada cuadro. `CreaTexturaManejada`: la primera vez llama a `EvictManagedResources` y reintenta **una** vez;
si sigue sin caber, durante 200 ms no se vuelve a pedir una textura de ese tamaño o mayor (las más chicas siguen intentándolo), y como mucho una evicción por segundo. Otros errores: como siempre. Con la tarjeta imitada que
contesta «sin memoria»: 100 peticiones seguidas (una por ms): 100 llamadas a `CreateTexture` con el ejecutable anterior, 2 ahora y 1 evicción. La evicción puede costar en el A4 una resubida de lo manejado: sin medir.

## 14. Los delegados de `Fn<>`

`Marshal.GetDelegateForFunctionPointer` crea un delegado y su puente nativo nuevos en cada llamada, y `Fn<>` se llamaba al bloquear y desbloquear cada textura subida, al soltarla y al cambiar la imagen intermedia del cielo
a media resolución. Un delegado solo depende de la dirección de la función y del tipo: se guardan los últimos 48 por hilo, sin cerrojos. Sobre funciones nativas de verdad (`getpid` de libc como ranura; con delegados
administrados Mono devuelve el delegado de origen sin reservar y no se vería nada): **35 recolecciones de gen 0 y 0,884 µs por llamada → 0 recolecciones y 0,025 µs** (900 mil llamadas, mediana de 5 procesos alternados).

**El toque del vigía de 25 a 50 ms: no se hizo.** Con 50 ms una parada de 190 ms puede medirse como 140 (el toque se encola hasta 50 ms después de que empiece la parada) y no llegar al umbral de 150 ms de la bitácora: el umbral
dejaría de ser exacto, que era la condición.

## A. La vista previa 32×32 que ya está en el almacén

`WarmGpuPreview` reducía la imagen a 32×32 y la pasaba a bytes aunque el almacén ya tuviera esa identidad; `GpuPreviewStore.Save` no sobrescribe un registro que existe, así que el resultado se tiraba. Ahora se mira antes
`Contains(identity)`. La identidad lleva el tamaño y la fecha del archivo; el almacén queda byte a byte igual antes y después, con los dos ejecutables. **2,9 → 0,008 ms** por portada que ya la tiene. No se tocó la ruta de
decodificación de `Form1.Portadas.cs`, donde la vista previa también se rehace al decodificar una portada para mostrarla (propuesta: saltarlo si `gpuPreviewCache` ya tiene la clave).

## B. El espectro del hilo de audio

Con la aurora encendida `ReportAudioBlock` costaba 4,6–4,9 ms por segundo de música (0,9 sin ella), casi todo en la escalera de 7 peldaños × 3 polos por cuadro. Los tres polos de cada peldaño van ahora uno detrás de otro
(sigue habiendo una sentencia por polo, con las mismas expresiones y el mismo orden), con el coeficiente y las casillas del arreglo en locales. **Bit a bit igual**: MD5 de todo lo que sale (medidas gruesas y finas con sus horas, acumuladores, estado de
los filtros) en cinco configuraciones de 20 s (44,1 kHz estéreo en bloques de 170 ms, 48 kHz en bloques de 10 y de 23 ms, mono, 22 kHz), iguales a las del ejecutable de antes. **4,79 → 3,83 ms por segundo de música**. El audio no se
toca: la función solo lee el bloque.

Se probó llevar los 21 polos y las sumas a variables locales o desenrollar los 7 peldaños: bit a bit igual pero **más lento** en Mono (≈ 10 ms por segundo, con tantas locales el compilador se rinde) y se descartó. No se tocó la
sentencia `bnd * bnd` ni se calculó una vez para las dos sumas: en el JIT de 32 bits de .NET 3.5 (x87) el producto intermedio puede ir en precisión extendida y un cambio de forma sí podría cambiar el último bit. Falta comprobar en el A4
que la huella sale igual con el JIT de XP.

## No hecho, y por qué

| Candidato | Por qué no |
| --- | --- |
| 4 (aplazar pasos de color) | Decisión del coordinador: cambia el tiempo de lo visible; «Color directo del estado extendido» ya cubre esa necesidad. **Propuesta**, no implementada. |
| 6 (reciclar texturas por `(tw, th)`) | Riesgo medio por la propiedad de las texturas (cinco sitios las sueltan: celdas separadas, textos móviles, adaptativas, compatibles) y ganancia solo visible con un controlador de verdad. Aquí no se puede medir. |
| 7 (no invalidar `ProgressView` y etiquetas bajo la tarjeta) | Habría que confirmar que se pintan. En Mono/X11 no se puede (el orden de recorte de hermanos de Windows, `WS_CLIPSIBLINGS`, no está), y el contador en `ProgressView.OnPaint` tocaría código del procesador congelado. **Propuesta para el A4**: contar `OnPaint` de las vistas bajo la tarjeta con `diagnostico=1`. |
| 10 (atlas de vistas previas LOD) | Riesgo medio, valor bajo, sin medida posible aquí. |
| 12 (cachear el tamaño de letra que cabe) | Se implementó con una caché de 24 puestos (clave: clase, familia, texto, estilo, rango, caja y el `Graphics`: resolución y modo de texto) y la salida era igual, pero ganaba 0,02–0,05 ms por repetición (letra de la pastilla 0,023 → 0,012 ms; título de la franja 0,053 → 0,009 ms) y solo cuando se repite el mismo texto. Se revirtió. |
| 13 (repartir `BuildStars`) | Opcional, valor bajo. |
| 5(b) y vigía a 50 ms | Ver arriba. |

## Pruebas

- Batería nueva `tironescambio` (registrada en `todo.sh` y `pruebas/LEEME.md`): una tarjeta imitada que cuenta (texturas creadas, bloqueos, `PreLoad`, memoria libre, evicciones), sin pintar. Con un ejecutable anterior como
  segundo argumento compara además cada resultado contra él, cada uno en su proceso, y mide antes/después alternando procesos. Sin él, compara contra los archivos de huellas de antes del cambio y contra sí misma (fría/tibia).
- Ampliados: `huellacuadros` acepta `k/n` como filtro para repartir las 168 pantallas entre procesos; `tironescaratulas` (el sondeo de memoria ahora es de un minuto).
- Archivos de huellas de antes del cambio (Linux/Mono): `celdas_separadas_2026-10-20.txt`, `texturas_2026-10-20.txt`, `aurora_2026-10-20.txt`.
- Pruebas automatizadas en Linux/Mono con datos sintéticos: no prueban Windows XP, el A4, un controlador Direct3D de verdad, el audio real ni la pantalla del bar.

## Qué falta en el A4 / XP

1. **Poner `diagnostico=1`** y leer `peores cuadros de los ultimos 60 s` durante una noche: qué fase domina en los cambios de página, de canción y de modo. Es la medida que decide cuál de los pendientes (6, 7, 10) merece la pena.
2. **`PreLoad`** (3): comparar los peores cuadros del cambio de página con y sin la precarga (hay que compilar sin ella); y que `texturas subidas` no cambie.
3. **`GetAvailableTextureMem`** (8): si en el libro desaparece un cuadro de unos ms cada 10 s.
4. **Precalentado** (1): que la primera vez del estado extendido, de la ficha, de los globos de la estrella y del buscador ya no aparezca como peor cuadro tras unos minutos sin tocar nada.
5. **Evicción** (11): que, si la tarjeta se queda sin memoria, no aparezca una resubida enorme tras `EvictManagedResources`.
6. **Espectro** (B): la huella de las cinco configuraciones con el JIT de XP, y el ahorro real en el hilo de audio.
7. **`ProgressView` bajo la tarjeta** (7): contar sus `OnPaint`.
