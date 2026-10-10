# Rendimiento y fluidez — 9 de octubre de 2026

Cambios compartidos en HD y Modest. No se modifica la apariencia, la calidad final de las carátulas ni el procesamiento de audio previsto. Son optimizaciones automáticas, sin interruptores nuevos. El renderizador CPU permanece congelado; Modest sigue compilando para .NET 3.5 y HD para .NET 4.8.

## Estrella y dibujo GPU

- La pose de la estrella se calcula después de preparar las texturas y de la precarga opcional, inmediatamente antes de dibujar. Antes podía conservar la pose de un refresco que ya había vencido durante ese trabajo.
- El análisis de transparencia de las capas de interfaz se reparte entre cuadros: ocho filas por tramo, desbloqueando el bitmap entre pasos. Mientras termina se dibuja la capa completa; el recorte final conserva exactamente rectángulos, orden, bordes bilineales y vértices.
- La franja compacta no se reconstruye cuando está totalmente oculta detrás del estado extendido. Se actualiza de nuevo al empezar a reaparecer.
- Se priorizan las partes visibles de los textos deslizantes. Las partes fuera del recorte se suben con presupuesto y anticipación; las texturas retiradas se liberan por tandas, con un límite de retención para evitar crecimiento sin control.
- Al minimizar se suspende el trabajo GPU. Al restaurar se reinician los relojes, sin integrar el tiempo que la ventana estuvo minimizada.
- Ctrl+F1 muestra FPS y p95/máximo del intervalo de cuadro. Con diagnóstico habilitado también registra p99 y máximos de preparación, dibujo y presentación. Memoria fija: 512 cuadros; se ordena una vez por segundo únicamente si se pide el contador. El máximo es de la ventana reciente, no de toda la sesión.

Estas modificaciones no cambian la trayectoria, la cadencia seleccionada, el modelo ni los colores de la estrella.

## Biblioteca, carátulas y disco

- Los planes de duración y análisis comparten la captura inmutable de la biblioteca y los índices comunes de duración; mantienen cancelación independiente.
- El censo de miniaturas visita una sola vez cada fuente, conserva solo candidatas pendientes y valida de nuevo su identidad antes de publicar. Se cancela en recarga/cierre y cede entre consultas de metadatos.
- Duración/género y análisis coordinan sus turnos por unidad. Las carátulas visibles tienen prioridad; discos/unidades independientes conservan concurrencia.
- Los tintes publican únicamente dominantes que han cambiado, evitando reconstrucciones repetidas de filtros y letras.
- Las tablas de mediciones mantienen un espejo propio del escritor y guardan cambios pendientes, no copias completas en cada tanda. Fallos/cancelaciones no consumen cambios sin confirmar; se conservan registros de versiones futuras.
- Las estadísticas se ordenan y formatean en el trabajador, conservando datos, orden y cultura de la captura.
- Los respaldos retienen handles y longitudes de una generación completa con un candado corto de metadatos. No esperan en la interfaz al formato, escritura, vaciado a disco ni reemplazo de los escritores. Los anexos en curso se recortan a la longitud previa; una reparación de final corrupto publica un temporal en lugar de truncar una generación retenida. Copia y verificación SHA-256 trabajan en segundo plano y ceden a las carátulas visibles. Las llamadas de metadatos de Windows no tienen un límite absoluto en HDD frío o unidades de red.

## Qué se precarga y por qué

Precargar ayuda si se eligen datos pequeños, repetidos y probablemente próximos. Ahora, después del primer cuadro y con margen, un trabajador de prioridad baja prepara cuatro tablas DSP (FFT, true-peak y sobremuestreo). No abre canciones ni altera el audio. No compite con el precalentado visual ni espera desde la interfaz.

No se carga toda la biblioteca, todas las portadas de máxima resolución ni todo el material de IA al encender. Eso aumentaría memoria y lectura inicial, especialmente con HDD y dos hilos, y podría retrasar el primer toque. Se conservan las precargas de páginas próximas y los presupuestos adaptativos existentes.

Los equipos potentes no se fuerzan a funcionar como uno de dos hilos: los presupuestos y la coordinación se adaptan a procesadores disponibles, unidades y coste medido. El límite protege el trabajo de fondo, no reduce calidad, resolución final ni opciones visuales.

## Audio y entrenadores

- El EQ reutiliza una curva preparada si sus ocho bandas son exactamente iguales, aunque venga de un arreglo nuevo. Una curva distinta mantiene su transición normal.
- DSEE reutiliza los límites exactos de bandas cuando corte y frecuencia superior no cambian.
- El entrenador DSEE extrae conjuntamente entradas y objetivos de RIAD, conservando ejemplos, orden, precisión y operaciones.
- Los entrenadores configuran BLAS antes de importar NumPy según afinidad real. En dos hilos reservan uno para el resto del equipo. Se puede fijar `ROCOLA_HILOS_ENTRENAMIENTO` entre 1 y los procesadores disponibles; el informe registra hilos y versión de NumPy. La reproducción exacta del entrenamiento requiere misma pila BLAS, entorno y presupuesto.
- HD limita las tareas pendientes del escáner externo y evita paralelismo anidado de FFmpeg.

Los entrenadores externos requieren Python 3 y NumPy en un equipo compatible; no son una nueva dependencia de la reproducción Modest/XP. El módulo `ia/presupuesto_entrenamiento.py` debe viajar junto a los entrenadores, no instalarse aisladamente. No se sustituyen modelos entrenados.

## Verificación

- Compilación Release de ambos proyectos y compilación GPU/CPU estricta.
- Batería de capas: 52 grupos y 5 617 comprobaciones HAL por rama, incluyendo Legacy/Natural, estrella 2D/3D y resoluciones 1024×768 y 3440×1440.
- Nuevas baterías: arranque 29, datos 39 y fluidez 953 comprobaciones por rama; audio eficiente 1 908 HD y 631 Modest.
- Batería adicional: 31 grupos y rendercache 97 comprobaciones. Las fixtures fijan la muestra temporal de la aurora y prueban subida inmediata de textos visibles, cola oculta que aparece y liberación de retiradas. Se conservan igualdad exacta de píxeles, 360 cuadros idénticos y 14 invalidaciones de paleta, sin rebajar umbrales.
- Audio: pruebas de formatos reales MP3 CBR/VBR, WMA y M4A, apertura/cierre de lectores y suavidad estéreo con afinidad de dos hilos. La afinidad no equivale a un procesador antiguo.
- Entrenadores: 100 pruebas Python por rama (una omisión opcional); herramientas HD 37. Paridad del extractor y audio comparado sin modificar las muestras esperadas.
- CPU congelado: los 14 métodos de Cube/AlbumView/CubeBox tienen instrucciones, operandos resueltos, variables locales y excepciones idénticos a HEAD, compilando antes/después con el mismo compilador.

Prueba sintética de transparencia 4K: el trabajo monolítico medido de unos 7–8 ms se reparte en tramos de aproximadamente 0,38–0,44 ms máximos en este PC. No equivale a FPS del programa ni a un HDD antiguo.

### Música real en este PC

Copia aislada con música real, resolución 3440×1440, estrella 3D y barra extendida. Se preservaron las instalaciones y sus datos durante la prueba. Rangos de informes de ventanas recientes de 512 cuadros, no percentiles agregados de toda la sesión:

| Escena | Intervalo observado | p95 | p99 | Máximo |
| --- | --- | --- | --- | --- |
| Modest Legacy | 14:00:55–14:01:16 | 6,1–6,5 ms | 6,9–24,2 ms | 32,7 ms |
| HD Natural | 14:11:14–14:11:37 | 5,7–6,1 ms | 5,8–7,1 ms | 11,9 ms |

En Natural estable los máximos de preparación/dibujo fueron 0,8/1,4 ms, y presentación 11,5 ms. En Legacy hubo también picos en preparación, dibujo y presentación. Estos datos no identifican por sí solos su causa, ni son una comparación controlada antes/después o Legacy/Natural. Durante entrada a disco/arranque de reproducción hubo picos mayores.

El intervalo se mide entre inicios de cuadro. Los máximos de cada fase son independientes: no se suman ni identifican necesariamente el cuadro con el intervalo máximo. Las esperas del planificador, eventos de interfaz y trabajo entre cuadros no se incluyen en las tres fases, aunque sí pueden afectar al intervalo siguiente.

## Límites y continuación

Se corrigieron fuentes concretas de trabajo irregular, pero no se promete ausencia absoluta de pausas de Windows/controlador/almacenamiento. Legacy conserva picos que requieren trazas de los episodios restantes antes de atribuirlos a la estrella o declarar el problema totalmente cerrado. Falta validación en XP real y en HDD/CPU antiguo; no existe ese equipo disponible para esta pasada.

La comprobación de la estrella y de p95/p99/máximo queda incorporada al flujo de trabajo en AGENTS.md. No basta un promedio alto de FPS para declarar fluidez perfecta.

## Instalación y publicación — 10 de octubre de 2026

Se actualizaron `D:\Release1` y `D:\ReleaseHD` con el EXE/PDB Release de su rama y el
conjunto completo de entrenadores, comprobando las huellas de cada archivo copiado.
No se ejecutó entrenamiento ni se activó ningún candidato. Las instrucciones locales
están en `ia\USO_EN_RELEASE.md`: arrastrar la carpeta Release completa al lanzador para
que encuentre también los modelos instalados de referencia.

Respaldo recuperable de los archivos sustituidos, en cada Release:
`respaldo_actualizacion_20261010_e95d01b0e46b4e8fac15a4edd4ae391e\anterior`.
La subcarpeta `nuevo` conserva también el paquete instalado. Se verificaron sin cambios
35 archivos de raíz en Modest y 26 en HD: huellas completas por debajo de 64 MiB y
longitud/fecha en los corpus grandes, que no fueron abiertos para escribir ni copiados.
Música, configuración, modelos, índices y mediciones no se sustituyeron.

- Modest EXE SHA-256: `BE362F80CE999AD1986D7DD05DDD4489055CDD82EA2148E231B3B0BDA6662FDF`.
- HD EXE SHA-256: `0E9D9175A149E0E97BDA0611929202D53DE9A1254CCD9890EF5412D395999553`.

Apollo instalado se conserva sin cambios. Su paquete antiguo usa una API distinta:
copiar solamente la nueva `util_audio.py` desactivaría su mezcla automática. El escáner
optimizado queda en el repositorio; requiere desplegar un paquete Apollo coherente,
no mezclar utilidades y motores de distintas generaciones. Esto queda pendiente.

HD no dispone de `rocola_ia_datos.idx` junto al corpus actual. Sin un índice válido de
origen no se puede certificar validación independiente DSEE; no se fabricó uno ni se
sustituyeron datos para eludir esa protección.
