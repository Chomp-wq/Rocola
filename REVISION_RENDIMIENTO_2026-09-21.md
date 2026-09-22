# Revisión de rendimiento — 21 de septiembre de 2026

## Alcance

Revisión sobre HD `832ec49` y Modest `5a63ddb`, incluyendo los cinco puntos enviados por el usuario. Cambios comunes aplicados a ambas versiones; el motor Apollo existe solamente en HD. No se modifica el renderizador CPU, no se añaden interruptores de rendimiento ni se cambia la calidad del audio.

## Resultado de los cinco puntos

1. **Precarga del lector:** implementada, pero había casos de propiedad y caducidad sin cubrir. Un lector esperando un callback podía quedar retenido al cerrar; la ruta sola no detectaba cambios del archivo; repetir consecutivamente la misma canción podía impedir la siguiente precarga. Se corrigen esos casos, se revalida tamaño/fechas y se libera explícitamente el lector pendiente. La firma es de metadatos, no un hash del contenido.
2. **Coordinación de mediciones:** implementada, pero se liberaba el turno antes de publicar las tablas. Otro trabajador podía repetir el análisis. Además, un repaso profundo podía saltarse un archivo ocupado y una publicación de duración sola se consideraba completa. El turno ahora dura hasta publicar o descartar el resultado; la espera es cancelable; una medición parcial no bloquea la sonoridad. F5 limpia las marcas terminadas sin borrar los turnos vivos.
3. **Arranque de canción y guardián:** las pruebas vigilan partes importantes, incluido el guardado durable de créditos. Son micropruebas e inspección de llamadas, no una medida completa hasta el primer sonido. Se corrige el mensaje que suponía ejecutar siempre fuera de Windows. No se retira ni difiere el vaciado seguro del estado financiero.
4. **Envíos de geometría:** el inventario es estático; no cuenta llamadas de un cuadro real. Se conserva como guardián y se actualiza su estimación. Se añade una optimización concreta: todas las caras visibles de cada cubo se envían juntas, manteniendo exactamente orden, geometría, UV y color. No se agrupan todavía las texturas diferentes del muro en un atlas.
5. **Apollo y herramientas externas:** pasan las pruebas de mezcla por bloques y métricas del entrenador. Ahorrar el vector de pesos del solape no equivale a procesar toda la canción con memoria constante. Se añade caché acotada de JSON de trabajos/errores: hasta 64 documentos pequeños, metadatos frescos, caducidad de dos segundos y copias independientes. Se siguen validando fuente, modelo, reintentos y publicación atómica. No cambia la salida WAV flotante.

El panel de reposo por eventos también pasa su guardián de invalidación. Se corrige el desbordamiento de la resta de tiempo en la caché de localización del motor de HD.

## Otras mejoras aplicadas

- Carga de carátulas e indexación: un lector en equipos de 1–2 hilos; hasta cuatro en los intermedios; seis con 12 hilos y hasta ocho desde 16. Se conserva la serialización por unidad HDD. Esto no aumenta todos los trabajadores de la aplicación.
- Presupuesto de subidas GPU según trabajo del cuadro anterior y objetivo de FPS, con límites conservadores en dos hilos y más margen cuando está disponible. No modifica imágenes ni efectos.
- Integración de los nuevos guardianes en el lanzador de Windows: 21 baterías. El lanzador de Mono conoce las dos nuevas baterías y ejecuta el script autónomo `test_util.py` correctamente; no se ejecutó la suite Mono completa en este PC.

## Verificación realizada

- Compilación GPU y CPU con advertencias como errores: ambas versiones. Compilar CPU no implica modificar su renderizador.
- Rebuild Release de ambos proyectos con MSBuild, también con advertencias como errores; artefactos en carpetas temporales, sin reemplazar releases ni datos del usuario.
- 21 baterías C# por versión.
- Cubos: 512 combinaciones por versión, comparación byte a byte de vértices/UV/colores/orden y una sola llamada de envío por cubo.
- Apertura y liberación de archivos sintéticos MP3 CBR/VBR, M4A, WMA y WAV; PCM idéntico entre caminos comparados.
- Política de carga con afinidad de dos hilos y diez repeticiones de las nuevas regresiones por versión. Limitar afinidad no reproduce toda la lentitud de un procesador antiguo.
- HD: 53 pruebas de utilidades de audio/Apollo, cuatro del entrenador EQ y cuatro comprobaciones del script autónomo. Modest: cuatro pruebas del entrenador EQ.
- No se midieron FPS reales, inferencia con checkpoint, reproducción audible, HDD físico ni Windows XP.

## Pendiente real, no cerrado por esta revisión

- **Arranque por fases sin espera de interfaz:** `CompleteStartupDataLoad` todavía espera al lector. Separarlo requiere mantener orden de restauración de créditos/cola e impedir que una interacción inicial se pierda al restaurar el estado.
- **I/O restante al elegir canción:** etiquetas y consultas de disponibilidad aún pueden tocar disco. Las micropruebas no demuestran ausencia de pausas en HDD.
- **Apollo, validación de memoria real e I/O:** entrada/salida y RMS ya tienen respaldo mapeado adaptativo y operaciones por bloques; ver [continuación de memoria](APOLLO_MEMORIA_2026-09-21.md). Queda medir RAM residente, disco y modelo real; no se promete un límite estricto de memoria total.
- **Escalado adicional:** siguen existiendo límites fijos (por ejemplo caché gráfica de 32 MiB y algunos intervalos/hilos). No afirmar que todo el programa se adapta ya a cualquier equipo potente.
- **Contador GPU real y atlas del muro:** el inventario no sustituye tiempos de CPU/GPU y conteo durante navegación real. El atlas requiere validar filtrado, bordes, invalidación y pérdida del dispositivo.
- **Validación en equipos destino:** tiempos de cuadro, primera carátula/primer sonido, HDD/SSD y XP real.

Las optimizaciones no bajan calidad para favorecer equipos modestos. Aun así, que una política funcione en pruebas no garantiza el mejor rendimiento en todo hardware; los límites fijos restantes deben evaluarse con mediciones reales.

## Continuación: etiquetas sin leer la imagen incrustada

En ambas ramas, el lector completo de ID3 v2.3/v2.4 ahora recorre los marcos y salta carátulas (APIC) y datos que no utiliza. Sigue leyendo título, artista, álbum y género con el mismo parser, incluyendo marcos repetidos, banderas, cabeceras extendidas y fallback ID3v1.

ID3 v2.3 con desincronización global conserva el lector anterior: ahí los escapes cambian posiciones y no se puede saltar usando el tamaño del marco sin más. Se mantiene el límite de 32 MiB y la carga de etiquetas una sola vez por objeto Track. No se cambia la imagen mostrada ni se escribe en el MP3.

Verificación de esta continuación:

- 23 baterías C# por rama; compilaciones GPU/CPU con advertencias como errores.
- Rebuild Release de HD y Modest sin advertencias, en temporales; no se reemplazaron ejecutables ni datos del usuario.
- 644 comparaciones entre recorrido selectivo y parser anterior: versiones, codificaciones, banderas, truncados, lecturas parciales y cabeceras inválidas.
- En los casos con APIC de 1 MiB, como máximo 145 bytes lógicos de estructura/texto leídos. No equivale a bytes físicos del disco: FileStream y Windows tienen sus propios búferes.
- Pruebas del camino real EnsureTags con archivos temporales, fallback v1, desincronización global y segunda consulta sin reabrir.
- La nueva batería pasa también contra Release con afinidad de dos hilos. No es una prueba en CPU antiguo ni XP real.

Elimina una lectura grande evitable al elegir canción; EnsureTags aún es síncrono. CompleteStartupDataLoad sigue esperando a las tablas y conserva el orden de restauración. No se presenta esta mejora como arranque totalmente asíncrono ni como latencia/FPS medidos en HDD.

## Continuación: tablas de arranque sin duplicar sus índices

Las seis lecturas de datos aprendidos (sonoridad/duración, EQ, cosecha IA, repaso completo, duración rápida e incompatibles) preparan ahora tablas privadas y adoptan sus referencias solo al terminar cada archivo. Se elimina el segundo diccionario/conjunto poblado y su recorrido de copia durante el arranque. Los lectores que mezclan datos con tablas ya existentes conservan la publicación transaccional; se comparte el parser para evitar diferencias de formatos, duplicados o niveles de IA.

Se utiliza un único lector secuencial con búfer de 64 KiB y el mismo trabajador de prioridad baja: no se añaden hilos ni límites para máquinas potentes. La cancelación se revisa antes y después de cada registro, no solo al terminar cada archivo. Es cooperativa: no interrumpe una lectura del sistema operativo ya bloqueada ni garantiza cierre instantáneo.

Verificación:

- 24 baterías C# por rama; compilaciones GPU/CPU y Rebuild Release sin advertencias. No se modificó el renderizador CPU.
- Nueva batería: equivalencia de las seis tablas, formatos viejos, duplicados, marcas completas validadas contra sus medidas, fallos de lectura sin publicación parcial, cancelación, archivos UTF-8 con BOM, archivos ausentes, copia de las rutas del trabajador y liberación de lectores.
- Prueba sintética de 100.000 duraciones: en HD, aproximadamente 33,7 MB asignados con copia frente a 25,2 MB con adopción (unos 8,5 MB menos de asignaciones acumuladas; no es RAM residente ni pico del proceso). No se extrapola a tiempo total de arranque, FPS o HDD físico.
- La nueva batería también pasa contra ambos Release con afinidad de dos hilos. No equivale a una CPU antigua ni a Windows XP real.
- Batería adicional de persistencia/apagón aprobada en ambos Release: guardado/copia, recuperación, escrituras concurrentes y raíz desconectada. Se corrigieron dos llamadas de la propia prueba que usaban una variante de Path.Combine no disponible en .NET 3.5.
- Artefactos en temporales; no se reemplazaron releases, configuración, música ni modelos.

Sigue pendiente el arranque totalmente no bloqueante: CompleteStartupDataLoad conserva su espera y el orden de créditos/cola. Esta tanda reduce trabajo y asignaciones de memoria, pero no adelanta la interacción mientras se recupera el estado.
