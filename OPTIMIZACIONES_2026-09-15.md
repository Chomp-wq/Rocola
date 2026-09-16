# Optimizaciones sin recortes — avance del 15/09/2026

## Política de configuración (actualizada por petición del usuario)

Las optimizaciones que no cambian apariencia ni funcionalidad son automáticas y siempre activas. Se retiró el submenú **Optimizaciones**, el interruptor de caché del buscador y el de portadas recientes. Solo los efectos visuales conservan interruptores; los ajustes funcionales de sonido y operación no se retiraron.

Las claves antiguas `rendimiento_opciones`, `buscador_cache` y `cache_portadas_gpu` se ignoran al cargar y ya no se escriben. Las demás preferencias se conservan. No hay que editar la configuración ni reiniciar la música para elegir optimizaciones.

El control «Caché de transiciones de color» pasa a llamarse «Transiciones suaves de color»: se conserva porque sí cambia la transición visible.

Modest conserva .NET 3.5/Windows XP como destino y comparte las mejoras generales. HD conserva .NET 4.8 y añade las mejoras propias de Apollo. El renderizador CPU permanece congelado y no recibió cambios de dibujo.

## Implementado

| Mejora | Alcance |
| --- | --- |
| Reloj del estado en caché | Ambas, solo GPU. Conserva una banda de fondo y actualiza el reloj sin recrear la cola. Envía solo las últimas 34 filas a Direct3D, con fallback completo si falla la subida regional. Colores, contenido y dimensiones siguen invalidando el panel completo. |
| Pausar refresco de ajustes ocultos | Ambas. El temporizador no recalcula Sonido mientras se ve otra página; entrar en Sonido refresca inmediatamente. No cambia la aplicación de ajustes. |
| Contabilidad de texturas en caché | Ambas, solo GPU. Recalcula ocupación cuando cambian recursos, no en cada fotograma estable; conserva límites, protección de visibles y expulsión existentes. |
| Verificar respaldo durante la copia | Ambas. Calcula SHA256 del origen durante la lectura de copia, sin releerlo; mantiene lectura verificadora del destino, WriteThrough, FlushFileBuffers y publicación atómica. |
| Reutilizar tablas de análisis de audio | Ambas. Comparte únicamente ventanas y tablas FFT inmutables de 2048/4096; estados y acumuladores siguen privados. Las pruebas comparan estas tablas con instancias privadas de referencia. |
| Solicitudes Apollo en segundo plano | Solo HD. Un trabajador publica solicitudes, deduplica las pendientes y captura mezcla/ruta sin acceder a controles. Conserva validación fresca de archivo/modelo y publicación atómica. |
| Caché de consultas del buscador | Ambas. Hasta ocho consultas y 65.536 referencias, máximo 32.768 coincidencias por consulta y 128 caracteres. Conserva todas las coincidencias y puntuaciones, no solo el top visible. Se vacía al reconstruir el índice. |
| Detener análisis que ya no se necesitan | Ambas. Comprueba cancelación entre bloques del análisis de archivos por cambio de generación/cola/cierre. No publica una medida parcial ni cosecha material cancelado; libera el lector. No afecta a la lectura de audio que se está reproduciendo. |
| Mezcla Apollo con menos copias en RAM | Solo HD. Reutiliza buffers privados de la mezcla float64 contigua sin cambiar operaciones/reducciones globales. Se aplica automáticamente, también a trabajos antiguos sin marca `memoria_mezcla`. No altera modelo, solapes, normalización ni WAV float32 final. |

Los resultados de Apollo ya calculados no se reprocesan, porque se conserva su contenido.

Se añadió una caché del contador de vídeos medidos: las consultas repetidas desde Sonido no recorren de nuevo la biblioteca. Se invalida al cargar/publicar medidas, finalizar el análisis de reproducción y reconstruir o limpiar la videoteca. También invalida el volumen automático: antes podía reutilizar una base obsoleta cuando cambiaba una medida sin cambiar el número de registros.

### Continuación del 16/09: sondeo de Apollo (solo HD)

- Enumera trabajos con una instantánea de metadatos, conservando antigüedad y patrón de nombres; evita una consulta separada de fecha por archivo.
- Si un trabajo desaparece durante la enumeración, continúa con los demás.
- La búsqueda de resultados hace una consulta de metadatos por candidato, conservando precedencia MP3/WAV/original, tamaños mínimos y seguimiento de enlaces.
- No prepara carpetas temporales para trabajos en espera, ya listos o rechazados. Los plazos, número de intentos, validación de fuente/modelo y reparación de publicaciones incompletas no cambian.
- No se omitió la revalidación de trabajos pendientes ni se añadió un interruptor. Modest no utiliza este daemon.

## Verificación

- Compilación GPU y CPU con avisos tratados como errores, en ambas ramas.
- Nueve baterías C# por rama mediante `pruebas/validar-capas.ps1`; incluye `optimizaciones.cs` y `eqpreparado.cs`.
- Prueba adicional `pestanas.cs` en ambas ramas: cuatro páginas completas, sin recortes, controles visuales accesibles y pestañas sin solapamientos.
- 240 combinaciones de tamaño, transparencia, cola y reloj: comparación píxel a píxel con la reconstrucción completa. Comparación adicional local con los ejecutables Release previos.
- Direct3D HAL real: subida regional frente a completa, versiones del panel, ausencia de subida sin cambios, fallback con región inválida, 150 cambios de texturas, expulsión y limpieza.
- Audio sintético: tablas/espectros idénticos y PCM del DSEE idéntico con tablas compartidas/privadas; comparación adicional con Release previo. Mono/estéreo, 44,1/48 kHz, FFT 2048 y también 4096 en HD; conserva la fracción final de la señal.
- Buscador sintético con 20.000 entradas: mismos resultados, orden y puntos; retroceso, consultas repetidas, límites de caché y reconstrucción con índice vacío.
- Configuración antigua con optimizaciones desactivadas: las claves retiradas se ignoran y desaparecen al guardar, conservando las preferencias visuales. No hay controles de rendimiento en Imagen/Fondo. 512 combinaciones visuales y comprobación de solapamientos.
- Respaldo: archivos vacíos y mayores de un bloque, dos destinos independientes y archivos opcionales ausentes.
- Cancelación: WAV sintético, interrupción entre bloques, lector liberado y contexto de cancelación limpiado antes del siguiente trabajo; RMS y EQ idénticos cuando no se cancela.
- HD: deduplicación de 100 solicitudes pendientes, instantánea de mezcla, publicación automática en segundo plano de mezclas distintas y ausencia de temporales de publicación sobrantes.
- Vídeos: 20.000 entradas, duplicados y mayúsculas, silencio, recarga y reconstrucción. Recalcula el volumen al cambiar medidas conservando el mismo número de registros.
- HD: 44 pruebas Python de mezcla, exportación, WAV flotante y regresiones del daemon. Incluyen equivalencia byte a byte, alias, entradas de solo lectura, float32/64, layouts, NaN/infinito, orden de trabajos, desaparición de archivos y espera/reintento sin preparar temporales.

### Mediciones sintéticas locales

No son FPS ni medidas de la aplicación completa:

- Panel de 3440×340, 12 canciones: la auditoría midió aproximadamente 5,2 ms al reconstruirlo. Actualizar solamente el reloj cuesta aproximadamente 0,05–0,06 ms y modifica 34/340 filas. Cambios reales de contenido todavía reconstruyen el panel.
- Consultas repetidas con 20.000 entradas: HD aproximadamente 0,21–0,25 → 0,02 ms; Modest aproximadamente 0,29–0,30 → 0,03 ms en pasadas separadas. Las mediciones bajo otras pruebas simultáneas varían; no extrapolar a todas las consultas.
- Mezcla Apollo de 30 s: memoria pico trazada de temporales más salida 80,75 → 40,38 MiB. Excluye entradas preexistentes, modelo y resto del proceso. WAV exportado idéntico.

Se compilaron ambos proyectos Release a directorios temporales independientes. No se reemplazaron ejecutables de producción, música, preferencias ni modelos. No se midió una sesión real con música ni se comprobó Windows XP. El MSBuild 3.5 mostró el aviso conocido de SDK 6.0A ausente y terminó correctamente.

## Trabajo pendiente de la auditoría de 16 oportunidades

Estos cambios no cierran toda la auditoría:

- Preparación anticipada del decodificador/salida para arrancar canciones sin bloqueo (#3).
- Completar arranque por fases sin esperar en la interfaz, preservando restauración de datos/créditos (#5).
- Compartir enumeración y metadatos entre firma y construcción de biblioteca (#6).
- HD: resto de consultas de caché Apollo que aún son síncronas; no se ha eliminado toda la E/S del inicio de reproducción (#4 parcial).
- Audio (#8 completado): tablas FFT compartidas y preparación de coeficientes/estados EQ fuera del callback. Ver `OPTIMIZACIONES_2026-09-16.md`.
- Coordinación/deduplicación de mediciones y prioridades entre trabajadores; aquí se añadió cancelación de trabajo obsoleto (#9 parcial).
- HD: almacenamiento acotado/mapeado de canciones completas de Apollo; aquí se redujeron temporales de la mezcla, no entrada/salida/pesos de longitud completa (#10 parcial).
- HD: caché segura de lectura de trabajos/errores durante esperas (#11 parcial); ya se eliminaron consultas duplicadas de metadatos y preparación innecesaria de temporales.
- Métricas por lotes del entrenamiento AutoEQ (#14), envíos de geometría D3D9 si el perfil lo justifica (#15), normalización compartida en herramientas A/B offline (#16).
- El panel aún inspecciona su firma cada 400 ms; una invalidación puramente por eventos necesitaría pruebas adicionales (#1 parcial).

No aplicar cambios de precisión, reducir efectos o eliminar verificaciones de seguridad para cerrar esta lista. Cualquier paso siguiente mantiene pruebas de equivalencia y compatibilidad de destino. Los interruptores son solo para cambios visuales; nunca para cachés u otras optimizaciones transparentes.
