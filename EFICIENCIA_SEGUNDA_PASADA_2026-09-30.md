# Eficiencia: segunda pasada del 30 de septiembre de 2026

Aplicada a HD y Modest. Modest conserva .NET 3.5 y el destino Windows XP.
Cambios automaticos, sin interruptores nuevos. No se modifica el renderizado CPU,
la calidad de audio, el filtro de las caratulas ni su JPEG de cache.

## 1. Registro de fuentes: comparar solo pendientes

FuentesMedidas usa GuardaCambios sobre el diccionario propio de su escritor serial.
Las escrituras ordinarias no construyen una lista completa ni recalculan la huella
de canciones sin cambios. Se mantienen los pendientes hasta confirmar el guardado;
cancelaciones y errores conservan su reintento.

Primera escritura, bajas, modificaciones externas y compactacion siguen usando la
instantanea completa y el reemplazo seguro. El formato ROCOLA-FUENTES v1 no cambia.
Una restauracion externa del mismo largo sigue detectandose por metadatos.

Prueba con 5.001 registros: dos modificaciones y una incorporacion requieren cinco
calculos de huella, no un recorrido de los 5.001. El trabajo normal es proporcional
a los pendientes; reconstruir/compactar sigue siendo O(N). Las otras tablas que
llaman Guarda con una instantanea completa conservan su comparacion anterior:
no se afirma haber convertido todos los diarios en O(1).

## 2. Publicar la portada antes de guardar su miniatura

Solo el trabajador GPU separa la publicacion y el guardado. Al decodificar una
portada sin cache, hace una copia privada de 256 x 256, publica el bitmap original
con sus colores preparados y encola el aviso a la UI ANTES de guardar el JPEG.

El mismo trabajador guarda despues la copia y la libera en finally. No se crea
otro hilo ni una cola de escrituras sin tope. La memoria adicional es una copia
por carril en vuelo, aproximadamente 256 KiB por copia; los carriles actuales
conservan sus presupuestos por CPU/unidad. No se acelera artificialmente un HDD
aumentando su paralelismo. Ese trabajador sigue ocupado hasta completar el guardado:
mejora el momento de publicacion, no elimina el coste total de escribir.

Se verifican generacion e identidad antes de guardar. Si cambian antes de publicar,
no se publica la portada retirada. Una copia de guardado nunca usa la imagen que
la UI puede dibujar o liberar. Si clonar falla, se conserva el camino sincrono;
si escribir falla, la portada publicada permanece disponible.
El decodificador sincrono usado por el generador de miniaturas y el perfil CPU
conserva su comportamiento.

La prueba bloquea el candado real de guardado y confirma que la portada llega a
memoria antes de liberarlo. Destruye entonces la imagen publicada: la copia aun
se guarda bien y el JPEG resultante es identico byte a byte al anterior.
Esto prueba el orden/propiedad, no mide los FPS ni el tiempo de un HDD fisico.

## 3. Puntero de vertices reutilizado dentro del cuadro

El dispositivo D3D9 reutiliza el GCHandle y el puntero cuando varios dibujos
consecutivos usan el mismo arreglo. No reutiliza una copia antigua: los cambios
hechos al arreglo durante la escena son visibles en la siguiente llamada.

Solo conserva el ultimo arreglo, hasta 6 MiB. Al cambiar de arreglo libera el
anterior; los mayores o los dibujos fuera de escena usan un pin temporal como
antes. EndFrame (incluido fallo), reset y Dispose liberan pin y referencia.
No hay listas de arreglos retenidos, busquedas crecientes ni pines entre cuadros
normales. UP y el anillo GPU conservan mezclas, geometria y orden.

Microprueba de 500.000 capturas de puntero en este PC, no FPS:

- HD: temporales 32,22 ms, repetidos 3,60 ms, alternados 34,02 ms.
- Modest: temporales 39,15 ms, repetidos 4,90 ms, alternados 41,68 ms.

Estos valores varian con la carga. Sin reutilizacion hay un pequeno coste adicional
de comprobar/sustituir el ultimo arreglo: en estas ejecuciones, unos 4-5 ns por
captura. No se presenta el ahorro de esta funcion como una aceleracion de todo
el programa. Las superficies HAL comprueban igualdad pixel a pixel entre rutas,
incluyendo mutacion del mismo arreglo durante una escena.

## Verificacion

- PASS en ambas ramas: bateria completa validar-mejoras (30 fixtures y rendercache)
  y validar-capas (31 baterias), compilando GPU/CPU con advertencias como errores.
- PASS eficiencia2 con afinidad de dos hilos en ambas ramas. Solo es una restriccion
  del proceso de prueba en este PC, no una medicion en un CPU antiguo de dos nucleos.
- PASS audio29 con dsporiginal (51), salidacontinua (128), latenciaadaptable (106),
  NLayer integrado y render PCM sintetico. Python: 96 pruebas compartidas en cada
  rama y 102 de Apollo adicionales en HD. No son escucha ni mediciones de DAC.
- Nuevas 32 comprobaciones en eficiencia2: cambios acotados, bajas, restauracion,
  cancelacion despues de formatear, fallo/reintento, publicacion con guardado
  bloqueado, JPEG identico, cache existente, fallo de guardado y propiedad de pines.
- verticesgpu ampliada a 83 comprobaciones HAL, incluyendo pines, EndFrame,
  mutacion dentro de la escena, equivalencia de superficies, reset y Dispose.
- La nueva prueba detecta que el guardado por pendientes falta en el ejecutable
  anterior a esta pasada.
- miniaturas vuelve a compilar tambien en .NET 3.5: se corrige en el fixture el
  uso de Path.Combine de tres argumentos (API ausente en ese runtime).
- Datos aislados en temporales. No se sustituyen releases, configuracion,
  musica, modelos, creditos ni cola del usuario.

Desde cada rama:
`powershell -File pruebas/validar-mejoras-20260929.ps1`
y `powershell -File pruebas/validar-capas.ps1`.
eficiencia2 admite `--dos-hilos` despues del ejecutable, para restringir solo
la prueba; no equivale a probar un procesador antiguo. No se certifican Windows XP,
FPS de la biblioteca real ni rendimiento de HDD/SSD fisicos.
