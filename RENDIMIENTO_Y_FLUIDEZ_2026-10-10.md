# Fluidez GPU y pausas entre cuadros — 10 de octubre de 2026

Continuacion posterior del punto 3: `MEMORIA_ANALISIS_2026-10-10.md` documenta la reutilizacion ya implementada y su equivalencia de medidas/cosecha. Los resultados y hashes de este informe corresponden a la pasada GPU anterior, no al nuevo despliegue.

## Cambios compartidos en Modest y HD

- El campo de estrellas alterna dos juegos de buffers para el patrón actual y el que se desvanece. Reutiliza también los vértices y los centros de agrupación. Tras preparar los dos juegos no vuelve a reservar un arreglo de vértices de aproximadamente 235 KiB en cada relevo de 60/180 segundos. No cambia semillas, orden, posiciones, densidad, colores, distribución Legacy/Natural ni transición; los cambios de tamaño/densidad conservan su comportamiento anterior.
- El reloj del estado extendido conserva su texto mientras no cambia el segundo. Sigue leyendo posición y duración en cada cuadro: no ralentiza la barra, los saltos ni la detección del final. Mantiene exactamente el formato transcurrido / duración y el fallback anterior. La clave es una referencia débil reutilizable para no retener un lector cerrado ni su canción en memoria.
- Ctrl+F1 amplía el diagnóstico: cada intervalo entre inicios de cuadro se relaciona con las fases del cuadro **anterior**, el hueco entre cuadros, el coste de agenda y las diferencias de los contadores GC del proceso. No confundir esas diferencias con una medición de cuánto tiempo duró GC ni con prueba de causalidad.
- Se conservan 512 muestras y hasta 32 episodios en memoria fija. No se crean arreglos/objetos ni se escribe en disco por cuadro; los textos se producen como máximo una vez por segundo y el escritor existente trabaja en segundo plano. La consulta de GC y el cronómetro adicional de agenda solo se activan con el contador visible.
- El umbral de episodios considera refresco, tope de FPS y limitación de vídeo. Un cuadro normal a 24/50/60 FPS no se trata como un tirón solo porque el monitor permita ir más rápido. Los cambios de tope no reclasifican el periodo del cuadro anterior.
- Un intento de dibujo no presentado corta la continuidad del intervalo para no atribuir sus fases al último cuadro presentado. Conserva las muestras y episodios anteriores; los fallos siguen contándose en `FallosMedidos`. El primer cuadro recuperado establece un nuevo origen, no certifica que no hubiera una pausa visible.

Optimizaciones automáticas, sin interruptores nuevos. No cambian modelos, audio, opciones visuales ni calidad final. El renderizado CPU permanece congelado.

## Evidencia real y sus límites

Copia aislada de Modest, música real, Legacy, estrella 3D, estado extendido y resolución 3440×1440; Ryzen 7 5700X3D, 8 núcleos/16 hilos, pantalla de 180 Hz y tope de 200 FPS. Esta captura usa la compilación de referencia instrumentada, **anterior** a las optimizaciones de buffers/reloj de esta pasada.

En el tramo estable 00:54:25–00:55:20, los 56 informes de ventanas recientes de 512 cuadros dieron:

| Dato | Resultado |
| --- | --- |
| p95 por ventana | 6,2–6,6 ms |
| p99 por ventana | 22,6–29,2 ms |
| Mayor intervalo informado | 44,6 ms |
| Mayor hueco fuera del render presentado | 44,1 ms |
| Episodios de 20 ms o más capturados | 167 |

Los 167 episodios coincidieron con un incremento de algún contador GC. Hay picos tanto entre cuadros como en presentación. Los máximos de las fases son independientes; no se suman ni identifican necesariamente el episodio con el intervalo máximo. No son percentiles agregados de toda la sesión ni una comparación controlada antes/después.

La biblioteca ya había terminado el escaneo, pero el registro mostraba **28.677 archivos pendientes de preanálisis en un carril**. Desactivar la cosecha de datos IA (`ia_datos=0`) no desactiva ese repaso. Por tanto, esta medición no aísla la estrella del análisis de audio ni permite atribuir todos los tirones a su animación.

Una segunda sesión con un álbum original de 17 canciones empezó todavía con 14 pendientes. No se completó una comparación controlada de la compilación optimizada; los tramos posteriores de esa sesión no se presentan como certificación de fluidez del estado extendido. La Rocola de producción quedó abierta después y no se cerró por cuenta del agente.

## Auditoría de las pausas restantes

El lector MP3 de NAudio construye un índice completo al abrir: crea objetos de índice, cuadros y pequeños arreglos por cada cuadro MP3. La decodificación añade cuadros y bytes temporales. Son miles de objetos por canción y un candidato concreto a recolecciones recurrentes durante el repaso. `MeasureFile` utiliza una sola apertura para sus tres tramos; la integrada completa puede requerir otra apertura posterior si aún falta, pero no se repite cuando la primera medida ya produjo una integrada válida.

No se encontró una reserva de varios MiB cada medio segundo en los callbacks normales del panel ampliado. El diagnóstico no escribe archivos desde el dibujo. Esto no descarta publicaciones del repaso, el precocinado de carátulas, la retirada de imágenes, las pausas globales de GC ni el controlador gráfico.

Siguientes comprobaciones, sin cambiar la calidad ni sustituir a ciegas el decodificador:

1. Repetir una raíz de un álbum original cuyas duraciones, EQ estéreo vigente e integrada algoritmo 3 estén completos; confirmar que no aparezca repaso pendiente. Medir Legacy/Natural, 2D/3D, navegación y reposo con la misma pista y ajustes.
2. Repetir después con la biblioteca completa y repaso activo. Comparar episodios y no solo FPS medios; no mezclar pruebas HAL, compilaciones o entrenamientos con esa medición.
3. Evaluar un espacio de trabajo de análisis reutilizable por trabajador. El caso estéreo de 44,1–48 kHz, mayor de 60 segundos, sin cosecha ni integrada, reserva 36 arreglos propios y un analizador: 68.201 bytes de payload en x86 y 68.233 en x64 (unos 66,6 KiB, sin cabeceras ni asignaciones de NAudio). No todo es reutilizable: la curva publicada de 48 bytes debe permanecer independiente. La integrada/cosecha añade más memoria. Esto no mide GC, no elimina las reservas del lector MP3 externo ni garantiza resolver todos los tirones.
4. Medir publicaciones de medidas, horneado de vecinos y retirada de imágenes antes de añadir límites o mover trabajo. No pausar el repaso ni ocultar episodios para aparentar una mejora.

No se promete animación 100% libre de pausas de Windows. No hay validación física disponible en XP ni en HDD/procesador antiguo de dos hilos. La afinidad y pruebas sintéticas no sustituyen ese equipo.

### Condiciones de la próxima reutilización del análisis

- Un espacio exclusivo por trabajador, sin compartirlo con carriles, calentador o generaciones superpuestas. No retener formularios, rutas, lectores ni delegados; controlar la reentrada.
- Resetear filtros, estados y acumuladores entre canciones, **no** entre los tres tramos de una misma medida: eso cambiaría el resultado. Leer siempre el tamaño lógico `4096 * canales`, no la capacidad retenida del arreglo.
- Recalcular coeficientes con la misma receta al cambiar la frecuencia; conservar bandas ausentes bajo Nyquist. El analizador de medidas conserva ventana 2048 en ambas ramas y se reemplaza si cambia su frecuencia/geometría. Reiniciar cortes; no conectarlo a grupos ni fuentes de reproducción.
- Mantener separados los analizadores de calidad y cosecha L/R. Reiniciar llenado, turno, muestras vistas, fase y listas; conservar orden, selección y límites. Acotar capacidad retenida sin recortar datos medidos.
- La curva EQ publicada mediante `BeginInvoke` debe ser una copia independiente: reutilizarla permitiría que otra canción modificara el resultado ya encolado o almacenado.
- Tratar la integrada como fase separada. Antes de reutilizar, resetear filtros K, fase, cuadros, subbloques, anillo, conteo de bloques, histograma, picos y mapa de silencios. Reutilizar coeficientes/pesos solo con el mismo formato y disposición; limitar memoria retenida, no la duración analizada.

Antes de implementarlo, exigir comparación bit a bit de RMS, curva EQ, integrada, picos, silencios y bytes/CRC de cosecha contra instancias nuevas. Intercalar frecuencias/canales, cosecha encendida/apagada, vídeos, cancelaciones y archivos fallidos. La auditoría anterior es estática; no se modificó el audio en esta pasada.

## Verificación automatizada

Las nuevas pruebas `relojreposo`, `campoestelarbuffers` y la ampliación de `fluidezcuadros` se integran en los lanzadores. Se comprueban formato/píxeles del reloj, lecturas de posición, liberación de lectores, igualdad exacta de patrones/vértices frente a la receta anterior, independencia y reutilización de los dos juegos, periodos e integración del diagnóstico.

La comparación aislada CPU contra HEAD verifica 14 métodos de dibujo con instrucciones, operandos resueltos, locales y excepciones idénticos en ambas ramas. Los tipos GPU nuevos no existen en las compilaciones CPU.

Resultados finales por rama:

- Compilación Release con advertencias como errores, y compilaciones GPU/CPU con el compilador propio de .NET 3.5 (Modest) y .NET 4 (HD): correctas.
- `validar-capas.ps1 -SinHal`: 54 baterías correctas. Se omiten explícitamente las comparaciones HAL opcionales indicadas por ese parámetro; otras baterías conservan sus comprobaciones gráficas sintéticas. No es una medición con música real.
- `validar-mejoras-20260929.ps1`: 31 regresiones correctas, más 97 verificaciones de caché/píxeles/subidas imitadas.
- Reloj: 11.100 comprobaciones. Campo estelar: 18.280 con receta independiente y 22.480 en la revisión independiente que añade la compilación anterior como referencia. Diagnóstico/teselas: 1.911 comprobaciones, incluyendo topes, cambios de periodo e intentos no presentados.
- `suenavivo`: 86 comprobaciones GPU y 9 CPU correctas. Se reparó su preparación antigua de ventanas sintéticas, que solo asignaba campos de Mono y dejaba tamaño 0×0 en Windows. Ahora valida el tamaño efectivo en ambos entornos; no se relajaron las comprobaciones de apariencia ni se cambió la interfaz del programa.

Los tiempos sintéticos no se convierten en una promesa de FPS ni en una ganancia real antes/después.

## Despliegue y pendientes conservados

No se sustituyen música, modelos, configuración ni tablas de medidas. Cada instalación requiere que su Rocola esté cerrada y respaldo de los archivos sustituidos. Una compilación y un push no equivalen a haber actualizado un ejecutable abierto.

HD: ejecutable y símbolos actualizados en `D:\ReleaseHD`, con respaldo recuperable en `respaldo_fluidez_20261010_d1e68fb0f5b74118a9da173db9198fb1`. Se comprobaron hashes del paquete instalado, configuración y modelos; los demás archivos de datos conservan tamaño y fecha. SHA-256 del ejecutable nuevo: `44807F7400D778CFE94A216F4C09C55914AC2DB8765CAF73B1A671A29753BEFE`.

Modest: compilación nueva preparada, pero `D:\Release1\Rocola.exe` continúa abierto. Su instalación y la comparación real adicional quedan pendientes hasta que el usuario lo cierre; no se termina su proceso.

Apollo instalado conserva su paquete anterior: su actualización pendiente debe sustituir un conjunto coherente de motor/utilidades/documentación, no solo `util_audio.py`. Se comprobó la coherencia de protocolo y pruebas sin inferencia; eso no certifica calidad audible ni velocidad GPU del modelo real. La caché de la generación anterior se conserva, pero no sirve como resultado de la nueva huella de procesamiento.
