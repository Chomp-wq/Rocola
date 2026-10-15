# Correcciones de la revisión de Modest — 15 de octubre de 2026

Petición: corregir los hallazgos necesarios de la revisión de `6dbd4c9` y ampliar
de 15 a 45 segundos la inactividad del asistente. Solo la rama
`claude/modest-carson-fa2v2n`; HD suspendida. No se modifica el renderizador CPU.

## Cambios

### Guardado de Ctrl+F3

El texto se confirma únicamente después de escribirlo, tanto en el guardado de
fondo como al cerrar. Un fallo conserva el pendiente y se reintenta con una pausa
de 2 segundos desde el latido, sin bloquear la pantalla, dormir al trabajador ni
llenar su cola. Como máximo hay un trabajo de la ventana en curso. Los turnos
antiguos no pisan ni confirman un guardado más nuevo. Reabrir la ventana tampoco
olvida un intento fallido.

### Modelos de IAS

Se toma una copia temporal única del modelo suelto, impidiendo escrituras sobre
el origen durante la operación. La copia debe pasar el mismo cargador RIA1 que
la reproducción: dimensiones, capas, tamaño, números finitos y contrato DSEE
28→12 o EQ 5→3. Solo entonces sustituye al activo con `File.Replace`, dejando el
anterior en `.anterior-N`. El duplicado suelto solo se retira tras activar su
copia validada. Los pesos no se leen ni copian en cada arranque si ya están en IAS.

Un bloqueo, un archivo truncado o un contrato incorrecto no apartan al instalado.
Si el sistema de archivos no admite el reemplazo, se conserva el anterior y el
suelto; no se fuerza un intercambio destructivo. Como defensa adicional, si un
fallo parcial deja el destino ausente y existe el respaldo, se intenta restaurar
una copia sin borrar ese respaldo. Las copias temporales normales se limpian.
Esto comprueba integridad/contrato, no certifica calidad audible del modelo.

### Subgrave de pistas mono

Se registra el fin de la pista mono. La siguiente pista continua compatible puede
heredar la memoria del filtro, como ya ocurría en estéreo. Una lectura de cero
muestras no marca fin ni consume prematuramente esa herencia. No se cambian los
coeficientes, los ajustes, el contenido de una canción aislada ni se añaden
reservas de memoria al procesamiento.

### PNG transparentes con la caché JPEG activada

La PNG transparente es un resultado definitivo, no una conversión JPEG pendiente.
Se recuerda junto a su identidad en una pequeña marca `.png.alfa`, después de
guardar la imagen. El calentador puede reutilizarla sin releer la original y la
decisión sobrevive al reinicio. Si cambia la fuente cambia la identidad; una
marca sin imagen no oculta una miniatura ausente. Una sustitución opaca retira la
marca. El barrido normal de miniaturas retira también su marca asociada.
Las PNG antiguas sin marca se comprueban una vez cuando el calentador las visita.
No se cambia el alfa ni la calidad visual.

### Validación del DSEE por género

Después de filtrar, se exigen los mínimos del reparto general: grupos 1/3/3 y
ejemplos 500/200/200 en entrenamiento/validación/prueba. La comprobación se hace
antes de entrenar o guardar un candidato. No se reasignan canciones a otras
particiones. Una falta de diversidad no se presenta como contaminación entre
entrenamiento y prueba ni como prueba de peor sonido en modelos ya existentes.

### Asistente

Empieza la despedida después de **45 segundos sin actividad**, no 15. Conserva
los 2,2 segundos de gesto y globo antes de cerrarse. Un nuevo toque reinicia la
espera o cancela la despedida. Sigue sin rostro.

## Verificación reproducible

`pruebas/validar-revision-octubre.ps1` compila GPU y el perfil común/CPU con el
compilador nativo .NET 3.5, con advertencias como errores, y ejecuta las
regresiones de ajustes, IAS, subgrave, grave mono, carátulas JPEG, despedida,
atajo del asistente, hojeo y lector MP3. Todos los datos son temporales y aislados.

Se prueban temporal bloqueado y reintento, reapertura y guardado fuera de orden;
modelos DSEE/EQ válidos, corruptos, de contrato distinto y bloqueados tanto en
origen como destino, con comparación de respaldos; continuidad mono bit a bit
contra una sola pista con bloques de 1, 137 y 4.800 muestras; PNG transparente
sin original disponible, invalidación por versión y sustitución opaca; y los
límites de 45 segundos, actividad nueva, cancelación y vuelta del reloj.

Tres herramientas antiguas se adaptan al destino: `hojeofino` usa un delegado
compatible con .NET 3.5; `mp3ligero` comprueba que exista el contador de reservas
antes de usarlo. En CLR 2 se omite explícitamente solo esa medida, no la
comparación de cuadros y bytes del lector. `mp3medidaligera` distingue la vista
que omite ID3 del archivo completo por su tipo, no por tener un stream propio:
el lector ligero ya usa un stream propio en ambos caminos desde el 13 de octubre.
Se conserva la comparación PCM bit a bit con lecturas fragmentadas y seek.
No se inventa una medida de memoria
ni se confunde la ausencia de esa API de prueba con un fallo del reproductor.

Python: `python -m unittest discover -p 'test_*.py'` desde `ia`. 118 pruebas:
117 aprobadas y una omitida porque requiere configurar el renderizador PCM
opcional. Las nuevas pruebas cubren mínimos por partición, límites inclusivos,
rechazo de muchos ejemplos con pocos grupos y salida del CLI sin entrenar ni
guardar en ese caso.

Resultado final: las 18 ejecuciones de regresión específica (nueve baterías en
GPU y común/CPU) terminaron correctamente. También pasan la auditoría general
(65.611 comprobaciones GPU y 65.596 comunes/CPU), el controlador artificial de
texturas (26.672), la guía (10.112 GPU y una comprobación de ausencia en CPU)
y la batería completa de `validar-capas.ps1 -SinHal`. La comprobación MP3 de
esa batería conserva 797 controles y PCM idéntico a la apertura de reproducción
histórica, con las grabaciones de prueba del repositorio; no es comparación
entre ejecutables de Release ni una escucha en el local.

Son verificaciones automatizadas: no equivalen a FPS con música real, escucha
en el local, pruebas de DAC, hardware antiguo de dos hilos o Windows XP.
No se instala ningún ejecutable ni se tocan biblioteca, corpus, créditos,
configuración o modelos de producción.
