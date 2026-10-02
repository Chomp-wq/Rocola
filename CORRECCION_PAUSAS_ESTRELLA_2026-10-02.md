# Corrección de pausas de la estrella — 2 de octubre de 2026

Aplicada en Modest y HD, exclusivamente al reloj del renderizado GPU. Legacy y Espacio natural comparten esta corrección, tanto para la estrella 2D como para la 3D.

## Fallos encontrados

- La rejilla de refrescos emparejaba el tiempo de la Rocola tomado antes de consultar Windows con un contador QPC tomado después. La duración de la consulta, o una pausa del hilo entre ambas muestras, desplazaba la rejilla. Una actualización periódica podía alterar el avance visible aunque el promedio de FPS pareciera correcto.
- El objetivo de presentación se elegía antes de preparar las texturas y la barra de estado. Si esa preparación consumía el plazo, la estrella seguía integrándose para un refresco ya pasado.

## Corrección

- Un único origen QPC para el reloj GPU y la conversión del refresco de Windows. La duración de una consulta ya no cambia el origen ni desplaza la rejilla.
- Se conserva el período de la rejilla del cuadro y, antes de mover la estrella, se revisa el objetivo. Si venció, se avanza al siguiente refresco de esa misma rejilla, sin otra consulta nativa.
- Se mantienen trayectoria, velocidad de crucero, giro, geometría, colores, estela, opciones visuales y reacción al reposo. No se añade ningún interruptor.
- Sin compositor, o con límites manuales no alineados con los refrescos, se conserva el camino anterior. Modest sigue compilando para .NET 3.5 / destino XP. El renderizador CPU permanece congelado.

## Verificación

- `pruebas/pausasestrella.cs`: reproduce 12 fallos con el ejecutable anterior; 33 comprobaciones pasan con el nuevo. Incluye consultas retrasadas, pausas del llamador, objetivos vencidos a 24/50/60/75/144/180/240 Hz y Legacy/Natural 2D/3D.
- `pruebas/estrella3d.cs`: 65 comprobaciones del sólido, trayectoria, giro, estela, memoria, configuración y reparto de cuadros. La ausencia del compositor se simula de forma explícita; ya no exige que el PC de pruebas carezca de él.
- Ambas baterías quedan incluidas en `pruebas/validar-capas.ps1`: 48 baterías por rama, compilación estricta GPU/CPU y 5617 comprobaciones HAL por rama.
- Comparación del código compilado de los 14 métodos del renderizador CPU con la versión anterior: sin cambios.

Las pruebas de reloj simulan retrasos; las de HAL usan GPU real con escenas sintéticas. No son una medición de FPS con la biblioteca del usuario ni una prueba en Windows XP real. Tampoco pueden garantizar que un equipo nunca omita un cuadro por una sobrecarga ajena al reloj.

## Comprobación con música

Dejar que entre la barra extendida en Legacy y observar la estrella durante varios minutos, incluidos los cambios de segundo del reloj. Repetir en Natural. Probar también que al tocar un control se vuelve a navegar con normalidad. Si persiste alguna pausa, anotar estilo, resolución y si coincide con un cambio de canción/reloj; el siguiente paso sería medir los plazos de esos cuadros con la biblioteca real.

Referencia del reloj de Windows: [DWM_TIMING_INFO — Microsoft](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ns-dwmapi-dwm_timing_info).

