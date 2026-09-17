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

## Luna sólida y movimiento contextual (GPU, Modest y HD)

- La superficie detallada usa alfa completo; conserva transparencia solo en el borde suavizado, halo y polvo.
- Al expandirse la barra de estado aumenta suavemente el recorrido, tamaño, libración y velocidad orbital. Al interactuar vuelve a un movimiento discreto y reduce la respuesta musical del halo.
- La fase se integra con el tiempo transcurrido para no saltar al cambiar de velocidad. Se reutiliza la textura de relieve: no se genera otra por fotograma.
- Se conservan las opciones visuales existentes de luna activa, relieve, halo, polvo y protagonismo; no se añaden interruptores.

## Carátulas durante el cambio de página

- Un paso aislado muestra las imágenes disponibles durante la animación; la ocultación opcional se reserva para pasos consecutivos separados por menos de 300 ms. La opción visual se llama «Ocultar caratulas en hojeo rapido».
- El prehorneado GPU prepara la página siguiente y la anterior con imágenes que ya están en memoria. Revisa también las que llegaron tarde, sin hornear discos vacíos ni pedir disco desde ese recorrido.
- Conserva los presupuestos de horneado/subida por cuadro y la resolución completa. Una imagen todavía no leída sigue dependiendo del almacenamiento; esto no promete cargas instantáneas ni ausencia absoluta de tirones.
- Corregido un defecto común del despachador: cambiar página invalidaba trabajos sin retirar sus registros pendientes y podía bloquear nuevas solicitudes. Ahora cancela cola y registro juntos, conserva las lecturas en vuelo y prioriza la página visible. F5 sigue invalidando el contenido y rechazando generaciones antiguas.
- No se modifica el renderizado CPU. La corrección del despachador común sí beneficia a ambos modos.

### Verificación de esta continuación

Diez baterías automatizadas pasan en ambas ramas. La nueva `pruebas/hojeoportadas.cs` comprueba página aislada, reutilización, ráfagas, opción visual, llegadas tardías, 20 cambios de página sin solicitudes fantasma, prioridad, ausencia de duplicados en vuelo y F5. Las pruebas de luna comprueban alfa interior completo, velocidad contextual, continuidad temporal y persistencia de opciones.

Compilaciones GPU/CPU de pruebas y Release de ambas ramas correctas. Modest emite el aviso del SDK Windows 6.0A ausente, sin impedir compilar. Los ejecutables quedan en carpetas temporales `rocola-luna-caratulas-release-hd-20260916` y `rocola-luna-caratulas-release-modest-20260916`; no se reemplazan archivos de producción. Pendiente valoración visual con música y medición de FPS reales; no se probó XP.
