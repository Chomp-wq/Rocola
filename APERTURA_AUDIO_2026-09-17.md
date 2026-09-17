# Apertura de audio sin bloquear la interfaz — 17/09/2026

## Cambios compartidos

La apertura de los lectores se ejecuta fuera del hilo de la interfaz. Esto incluye el recorrido inicial del MP3 y, cuando hace falta, su reconstrucción tolerante existente. No cambia el decodificador, las muestras, el formato de salida ni los efectos de sonido.

- Un solo trabajador de prioridad inferior; una sola solicitud pendiente, siempre la más reciente. No se abre un lector por cada pulsación.
- Al saltar o cerrar se invalida el trabajo anterior. Los lectores obsoletos se liberan sin reproducirlos ni tratar la cancelación como error que devuelva créditos.
- Si el lector ya está listo pero su aviso no se ha atendido, cancelar también lo libera. Un aviso que llega después se ignora.
- Si falla la publicación porque la ventana desapareció, se libera el resultado.
- La creación de la salida y el arranque siguen en la interfaz, conservando el callback por ventana de WaveOut en XP.
- El salto diferido tras un error comprueba que no haya comenzado otra solicitud entre tanto.
- HD conserva el retorno al original cuando falla la caché de Apollo y el reintento único mediante WaveOut si falla WASAPI. El modo de reintento se captura antes de la apertura asíncrona.
- El medidor explícito de Ctrl+F3 conserva una preparación síncrona separada: necesita la cadena lista antes de medir. No sustituye una canción que esté abriendo; su audio de prueba se silencia antes de Play y un fallo de preparación no devuelve créditos ni avanza la cola.

No se añaden interruptores. No se modifica el renderizador CPU, ni música, modelos, configuración o datos del usuario.

## Verificación

- Compilaciones GPU/CPU y trece baterías de regresión en ambas ramas.
- Un trabajador, coalescencia de 100 solicitudes pendientes, cancelación en vuelo y después de publicar, ventana cerrada, errores y reintento desde el callback.
- WAV sintético PCM16 y flotante32, mono/estéreo: comparación exacta de muestras, formato y longitud con la apertura anterior.
- MP3 CBR/VBR, WMA y AAC/M4A sintéticos: comparación exacta al consumir el lector desde otro hilo después de terminar el trabajador; archivos liberados tras Dispose. Son pruebas con los codecs de este Windows, no de XP.
- Repeticiones de las pruebas y política de carga con afinidad limitada a dos procesadores. No equivalen a una CPU antigua física.
- Baterías adicionales de modo nocturno, tiempo de espera y medidor; respaldo DSEE en HD. Se actualizan sus puntos de inspección al método que inicia la cadena después de abrir el lector.
- Release compiladas en directorios temporales; sin sustituir ejecutables de producción. Modest conserva su destino .NET 3.5 y el aviso conocido del SDK 6.0A.

Para repetir las pruebas de formatos: primero ejecutar `pruebas/validar-capas.ps1 -OutputDirectory <directorio>`; después `pruebas/validar-audio-formatos.ps1 -OutputDirectory <mismo directorio>`. La segunda requiere FFmpeg y conserva sus señales sintéticas en una carpeta temporal independiente.

## Alcance y pendientes

Esto evita esperar en la interfaz por la apertura; no reduce necesariamente el tiempo hasta el primer sonido. Un codec nativo ya iniciado no se aborta: si tarda, el último pedido espera a que termine, con la interfaz libre para seguir atendiendo.

No es precarga anticipada del decodificador siguiente ni reproducción sin pausas. La lectura de etiquetas, la consulta de la caché IA y la inicialización de salida/DSP aún tienen trabajo en la interfaz. Siguen pendientes su revisión, la coordinación restante de análisis y las mediciones que justifiquen nuevas agrupaciones de geometría GPU.

No se han medido FPS ni latencia con una biblioteca real en HDD/SSD; tampoco se ha probado Windows XP ni la reproducción por las bocinas del equipo del usuario.
