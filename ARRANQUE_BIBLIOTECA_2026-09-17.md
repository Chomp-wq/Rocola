# Biblioteca e índice — 17/09/2026

## Cambios en Modest y HD

La firma previa de biblioteca conserva temporalmente listas de archivos y subcarpetas ya enumeradas. Si hace falta reconstruir el catálogo, el escáner consume esas listas en lugar de pedirlas otra vez al disco.

- Se conserva el orden original de enumeración, la clasificación audio/vídeo y la preferencia de portadas.
- Antes de reutilizar una carpeta se comprueba su fecha: si cambió, se enumera normalmente. Las entradas consumidas se retiran inmediatamente.
- Límites: 2.048 carpetas, 32.768 rutas y 8 MiB de contabilidad estimada de cadenas/entradas, no un límite de RAM total del proceso. Al agotarlos se sigue por el recorrido normal sin perder contenido.
- La memoria pertenece a un solo escaneo y se libera también al salir con error. No es una caché persistente.
- Se conservan las firmas completas antes/después, incluidos archivos profundos, tamaños y fechas. La verificación final nunca usa estas listas temporales. No cambia el formato de caché ni se elimina la detección de modificaciones durante el escaneo.
- El worker de biblioteca trabaja con prioridad inferior a la interfaz y restaura la prioridad del hilo compartido al terminar.
- El índice reserva margen con la misma política de CPU disponible: en 2C/2T no crea trabajadores adicionales; en equipos mayores, máximo cuatro trabajadores totales y prioridad inferior. No cambia normalización, máscaras, resultados ni su orden.

El ahorro consiste en evitar reenumeraciones de las carpetas que caben en la memoria temporal y siguen sin cambios. No se afirma un porcentaje de mejora de arranque: depende del almacenamiento, tamaño de biblioteca y estado de la caché. Un arranque que acepta la caché también paga la captura temporal, acotada por los límites anteriores.

## Verificación

- Doce baterías C# correctas en ambas ramas; compilaciones GPU/CPU con avisos como errores.
- Firma comparada con una implementación independiente del algoritmo anterior.
- Comparación de catálogo, orden y portadas con/sin reutilización; archivos profundos, altas, bajas, renombres, carpetas excluidas y fallback al llenar la memoria temporal.
- Límites de almacenamiento y limpieza/restauración de prioridad tras error del worker.
- 20.000 claves y máscaras del índice iguales al procesamiento secuencial; límites para 1, 2, 3, 4, 8 y 32 procesadores.
- Pruebas adicionales de escaneo y carga adaptativa con afinidad limitada a dos procesadores en ambas ramas. No equivale a probar una CPU antigua física.
- Release compiladas en carpetas temporales; Modest conserva el aviso conocido del SDK 6.0A sin errores de compilación. Sin reemplazar ejecutables de producción ni datos del usuario.

Sin cambios visuales, interruptores nuevos ni modificaciones del renderizador CPU. No se probó Windows XP ni se midieron FPS o arranque completo con una biblioteca real.

## Pendiente

Esto mejora el recorrido y el índice, no completa todo el arranque por fases. Siguen pendientes preparación anticipada del decodificador, coordinación/deduplicación restante de análisis y medición previa a nuevas agrupaciones de geometría GPU.
