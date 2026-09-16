# Continuación de rendimiento — 16/09/2026

## Ecualizador preparado antes de entregar audio (Modest y HD)

Al cambiar la curva del EQ, antes se calculaban hasta ocho filtros y se reservaban sus matrices dentro de `LevelTap.Read`, justo cuando el dispositivo pide muestras. Ahora el llamador prepara coeficientes y memoria; una única referencia publicada de forma atómica entrega el conjunto completo al hilo de audio.

El lector sigue aplicando el cambio en el siguiente bloque. No cambia fórmulas, precisión, orden de bandas, ganancia ni realce. Conserva los estados al apagar/volver a activar la misma curva, y no reinicia el filtro por cambios intermedios que no llegaron a aplicarse. La memoria de estado solo la modifica el lector, no el hilo que publica ajustes.

Esta mejora traslada trabajo fuera de la entrega de audio; no pretende eliminar el cálculo ni demostrar una subida de FPS. No añade interruptores, no cambia el renderizado CPU ni los formatos de audio.

## Verificación

- Nueve baterías C# en ambas ramas; compilaciones GPU y CPU con avisos tratados como errores.
- Nueva prueba `pruebas/eqpreparado.cs`: 24 combinaciones de formato/realce, 14 cambios de curva por combinación, bloques de distintos tamaños y offsets.
- Frecuencias 22,05/44,1/48/96 kHz; uno, dos y seis canales; EQ apagado, identidad, bandas omitidas y curvas nuevas/equivalentes.
- Comparación exacta de muestras contra una referencia independiente y los ejecutables Release anteriores a este cambio.
- Publicación concurrente de 2.000 ajustes mientras se leen 2.000 bloques, sin estados incompletos ni valores no finitos.
- Se conserva la prueba de preparación completa antes de leer y de reutilización al asignar de nuevo la misma curva.

Las compilaciones se generan en directorios temporales: no se reemplazan las releases del usuario ni sus datos. No se probó hardware XP ni una sesión real con música. Esta pasada cierra la preparación del EQ (#8); siguen pendientes las oportunidades enumeradas en `OPTIMIZACIONES_2026-09-15.md`.
