# Apollo: memoria adaptativa — 21 de septiembre de 2026

## Implementado en esta continuación

Solo HD contiene Apollo y el conversor por lotes; Modest no recibe dependencias nuevas ni cambios de audio o de compatibilidad XP. Se actualiza este informe en ambas ramas para mantener el mismo estado de pendientes.

- `memoria_audio.py` decide por canción según tamaño del WAV decodificado y memoria disponible. Con margen suficiente conserva el procesamiento en RAM.
- Cuando la estimación supera un presupuesto conservador, entrada, salida y almacenamiento auxiliar de RMS se respaldan en archivos mapeados, junto al WAV temporal. No se crean junto a la música original.
- Lectura, operaciones de mezcla y exportación usan bloques. La inferencia conserva exactamente los trozos, solapes, precisión y parámetros anteriores.
- El RMS conserva la reducción global: no se sustituyó por una media aproximada de los RMS de los trozos. La salida corta con disposición Fortran conserva la ruta anterior para no alterar el orden de suma.
- Los mapas se cierran y sus archivos se retiran al terminar o fallar. Si falta espacio se utiliza el manejo de error existente; no se publica audio incompleto ni se sobrescribe el original.
- Integrado tanto en `daemon_ia.py` como en `05_procesar_todo.py`. El formato de caché continúa siendo WAV float32; no se cambia la huella del pipeline porque no cambian modelo, parámetros ni operaciones de audio.
- Corregida además la liberación explícita del candado de salida del conversor por lotes, también al retornar por error.

## Selección y costes

La estimación es tamaño del WAV × 10 + 32 MiB. El presupuesto es un tercio de la memoria disponible después de reservar 256 MiB; si no puede consultarse, se usa un presupuesto de 256 MiB. No es un límite de velocidad ni de RAM idéntico para todos los equipos.

En Windows se consulta el menor de memoria física disponible y capacidad de compromiso disponible mediante [MEMORYSTATUSEX](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex). Es una instantánea: otros procesos pueden consumir RAM después. No cambia los hilos del modelo ni la prioridad de la rocola.

La ruta mapeada necesita espacio y añade I/O temporal. Evita reservas completas grandes en el heap, pero **no garantiza RAM residente constante**: el sistema decide qué páginas retiene. Tampoco limita la memoria de PyTorch o de la GPU. En HDD y con el modelo real todavía hay que medir el balance entre I/O y evitar paginación.

## Pruebas

- 62 pruebas unitarias/de integración del directorio `ia/upscale`, cuatro del entrenador EQ y las cuatro comprobaciones del script `test_util.py`: pasan.
- Comparación exacta de muestras y bytes WAV PCM16/float32 entre RAM y mapas; mono/estéreo, contrafase, vacío, silencio, casi silencio, mezclas parciales/completas, bordes y solapes de dos o más fragmentos.
- FFmpeg/ffprobe reales, señal sintética y procesador determinista de prueba: motor y herramienta por lotes producen WAV idénticos; un fallo del procesador conserva el trabajo para reintento y no publica salida incompleta.
- Limpieza de temporales y rechazo por falta de espacio.
- Prueba a 44,1 kHz y trozos de 10 s: 500.000 frente a 5.000.000 de muestras estéreo (10× longitud), pico de asignaciones temporales aproximadamente **16,9 MiB en ambos**. Medido con tracemalloc; no equivale a RSS/VRAM ni incluye pesos de Apollo.
- No se ejecutó inferencia real con checkpoint, escucha, FPS ni prueba física en HDD/XP. No cambió C#; no se recompilaron ni reemplazaron los releases en esta continuación.

## Pendiente

Validar RAM residente e I/O con canciones largas y Apollo real en el equipo destino. Siguen pendientes el arranque por fases, el I/O restante al elegir canción y la medición GPU real/atlas del muro descritos en el informe de revisión. Esta tanda resuelve el almacenamiento de los arrays grandes, no todos esos otros apartados.
