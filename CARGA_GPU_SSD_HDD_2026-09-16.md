# Carga y renderizado: presupuestos para 2C/2T

Aplicado en Modest (.NET 3.5, destino XP) y HD (.NET 4.8). Se incluye la continuación previa de luna sólida, movimiento contextual y corrección de solicitudes de portadas fantasma.

## Implementado

- Precarga de texturas GPU de las páginas vecinas a partir de imágenes ya compuestas. No lee disco ni compone desde el callback. Intenta como máximo una subida cada 40 ms en equipos de hasta dos procesadores lógicos (20 ms en el resto), solo con margen de tiempo y la página visible ya disponible.
- Presupuestos de composición y subida medidos con reloj de alta resolución: 1,5 ms por etapa en hasta dos procesadores lógicos y 3 ms en el resto, además del máximo de seis piezas. Aprende el coste de cada pieza y aplaza las siguientes si no queda margen. Una pieza visible indivisible puede exceder el presupuesto; se permite para no bloquear el progreso.
- La precarga mantiene los límites existentes de 54 texturas/32 MiB. No expulsa elementos visibles ni alterna indefinidamente las vecinas que no caben. Si falta memoria o tiempo, se omite trabajo anticipado, no calidad.
- Límite global de trabajadores de portadas: uno en 1–2 procesadores lógicos, dos en tres, tres en cuatro y como máximo cuatro. Prioridad inferior a la interfaz. No se crea un hilo por portada.
- Los nuevos presupuestos consideran los procesadores permitidos por la afinidad del proceso, no solo los que anuncia el runtime; el cálculo se hace una vez, no por fotograma.
- Planificación por unidad lógica de origen: un lector por unidad clasificada como HDD; SSD permite concurrencia dentro del límite global. Una unidad ocupada no impide atender solicitudes de otra. Se conserva la clasificación automática/manual existente: no es detección de discos físicos ni separa particiones del mismo dispositivo.
- F5 conserva el registro de unidades ocupadas hasta que terminan sus lecturas, aunque invalide sus resultados. Cambio de página retira cola y registro conjuntamente, sin invalidar lecturas útiles en vuelo.
- La prelectura musical y el análisis de archivos ceden por bloques a las portadas. En 2C/2T ceden ante cualquier carga de portada; en equipos mayores, ante la misma unidad. Durante la espera siguen comprobando cancelación. No cambia el callback de reproducción, las muestras, los resultados completos del análisis ni la calidad.

No hay interruptores de rendimiento nuevos, ni recortes visuales, ni modificaciones del renderizador CPU. Las reglas de publicar en GitHub y priorizar equipos 2C/2T quedan en AGENTS.md.

## Pruebas

- Once baterías C# en cada rama; compilación GPU y CPU con avisos como errores.
- Nueva prueba de límites para 1, 2, 3, 4, 8 y 32 procesadores, progreso de piezas costosas y predicción de presupuesto.
- Colas HDD/SSD simuladas: prioridad, unidades simultáneas, límites, liberación y F5 con lecturas en vuelo.
- Direct3D HAL: una subida anticipada por llamada, reutilización con presupuesto de subida cero, renovación de vecinas, protección de visibles, 32 MiB sin ciclos de expulsión y recuperación de recursos.
- Análisis WAV: cancelación durante espera de portadas, reanudación y equivalencia de RMS, duración y curva EQ.
- Se conservan comparaciones de píxeles, muestras PCM, controles y cachés de las baterías anteriores.
- Repetidas las baterías de carga adaptativa y regresiones generales con afinidad limitada a dos procesadores; ambas ramas detectan dos y pasan. Esto limita la concurrencia, no simula la velocidad de una CPU antigua.
- Release de ambas ramas compiladas en carpetas temporales, sin sustituir música, configuraciones ni ejecutables de producción. Modest conserva el aviso conocido de SDK 6.0A ausente, sin errores de compilación.

Las pruebas de planificación no son mediciones de un HDD físico ni certificación de rendimiento de una CPU antigua. Quedan pendientes mediciones de FPS/tiempos de cuadro con música en equipos modestos y prueba real de XP.

## Lo que esta pasada no cierra

La reorganización completa del arranque/catálogo y la preparación anticipada del decodificador siguen pendientes. También siguen pendientes deduplicación de mediciones entre trabajadores y agrupación adicional de geometría, que necesita identificar primero el coste dominante y conservar el orden de transparencias. No se presentan como implementadas por esta optimización de carga.
