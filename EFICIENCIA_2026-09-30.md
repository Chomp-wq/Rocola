# Eficiencia y rendimiento — 30 de septiembre de 2026

Cambios compartidos por Modest (.NET 3.5, destino XP) y HD (.NET 4.8).
Son optimizaciones automaticas: sin interruptores nuevos, sin cambiar colores,
tipografia, geometria ni muestras de audio. El renderizador CPU sigue congelado.

## 1. Calculo de audio separado de la pantalla

`audioDatosLock` protege generaciones, mediciones originales, filtros del
visualizador y cierres/relevos. `levelLock` solo protege las colas que consulta
la pantalla. Orden si hacen falta ambos: audioDatosLock -> levelLock.
ConsumeLevels y AvanzaAuroraFina no toman la puerta del calculo.

Se conservan la invalidacion de lectores retirados y el cierre de medidas de
la pista anterior. No se ha quitado la proteccion para ganar velocidad.
Los cierres/cambios de pista pueden esperar un calculo en curso; el dibujo
normal ya no tiene que esperar ese calculo por muestra.

## 2. Registro incremental de fuentes

`rocola_fuentes.txt` conserva la cabecera y el formato v1. Se reutiliza
DiarioTabla: primera escritura completa, despues solo diferencias, compactacion
al eliminar entradas o acumular repeticiones y deteccion de cambios externos.
La ultima fila completa de una ruta manda; una ultima linea incompleta no se
adopta ni al arrancar ni al recargar.

SaveFuentesMedidas ya no construye en la UI una lista de todas las fuentes.
El escritor serial crea una copia inicial; despues recibe cambios y bajas.
Solo borra pendientes que corresponden al objeto/version que realmente guardo.
Una solicitud agrupada o un guardado cancelado no pierde los cambios anteriores.

La segunda pasada de esta fecha sustituye la comparacion completa del registro
de fuentes por sus pendientes. Primera escritura, bajas y compactacion siguen
siendo O(N); las otras tablas conservan su comparacion completa en segundo plano.
Detalles y limites: EFICIENCIA_SEGUNDA_PASADA_2026-09-30.md.
Prueba sintetica: 2.000 entradas, primera escritura 81.799 bytes; cambio de una
entrada 40 bytes. Son bytes reales escritos en datos de prueba, no tiempos de HDD.

## 3. Colores de caratula preparados

CoverPump calcula promedio, picos y paleta en su trabajador, antes de publicar
la imagen. La pantalla adopta los colores solo si pertenecen al mismo bitmap.
La cache de preparados comparte las bajas/reemplazos/F5 de la cache de portadas.
La cache de tonos de largo plazo NO conserva referencias a bitmaps.

El extractor GPU lee la misma rejilla de hasta 1.024 muestras mediante LockBits.
Conserva formato, alfa, canales y stride, incluido negativo. Para formatos no
admitidos o PArgb translucido usa GetPixel y mantiene su redondeo original.
El algoritmo de agrupacion/seleccion de colores no cambia. Los calculos y el
renderizado de colores del perfil CPU no se alteran.

## 4. Planes de biblioteca por finalidad

StartDurScan captura pistas, videos, letras y tablas de duraciones, no las
tablas de EQ, sonoridad, conteos, recencia ni cola del analisis completo.
Los planes completos conservan sus copias coherentes y su cancelacion al hacer F5.
Los indices de tablas omitidas son vacios, no referencias a tablas UI mutables.

## 5. Cache de conversion continua

Hasta cuatro parejas de frecuencias, con un maximo de 6 MiB retenidos.
Coeficientes identicos, un unico preparador de baja prioridad, entradas
inmutables capturadas por cada lector. Expulsar una entrada no invalida un lector.
No se reconstruye una tabla al alternar entre parejas ya preparadas.
El presupuesto de conversion usa procesadores disponibles por afinidad, como
el resto del programa; no limita un equipo potente a dos hilos.

## 6. Esperas por senales

Los turnos HDD despiertan por Monitor.PulseAll al liberar la unidad.
Los lectores que ceden a caratulas despiertan al terminar/cancelar trabajos.
F5 notifica ambas puertas. Monitor.Wait libera el cerrojo durante la espera.

Se conserva una comprobacion de cancelacion de respaldo cada 250 ms para
canceladores Func<bool> que no emiten una senal; no se promete eliminar todos
los despertares periodicos. Los respiros de lectura y las esperas por video
que forman parte del presupuesto existente no se eliminan.

## 7. Anillo GPU adaptativo

Las tandas grandes pueden usar un vertex buffer DYNAMIC | WRITEONLY acotado:
NOOVERWRITE para agregar y DISCARD al dar la vuelta. Se mantienen vertices,
mezclas, texturas y orden de pintado. Los dibujos pequenos conservan UP.
El recurso DEFAULT se libera antes de Reset y al cerrar; se recrea al necesitarlo.
Hay respaldo UP si la creacion/bloqueo no esta soportada.

La seleccion compara el tiempo de envio de 44 dibujos reales de igual numero
de triangulos, alternando caminos y omitiendo las primeras parejas de calentado.
No dibuja geometria extra. Solo conserva el anillo si aventaja UP por al menos
5 % en esa calibracion. Se mide el envio CPU/controlador, no el tiempo completo
de GPU ni los FPS de la rocola. No hay limite de FPS, de nucleos ni toggle.
La ruta software vertex processing usa los flags correspondientes.

Referencia de implementacion:
[Optimizacion de buffers dinamicos de Direct3D 9, Microsoft](https://learn.microsoft.com/en-us/windows/win32/direct3d9/performance-optimizations).

## Verificacion y limites

Resultado en este PC, en ambas ramas: las 34 comprobaciones nuevas de eficiencia,
las 45 comprobaciones HAL del anillo, la tanda de mejoras y las 31 baterias de
capas pasaron. Tambien pasan DSP original (51), salida continua (128), latencia
adaptable (106) y las regresiones Python del entrenador compartido (96).
La comprobacion de eficiencia se repite con afinidad reducida a dos hilos.

La ampliacion de regresiones encontro un defecto anterior en la subida parcial
del aviso del buscador: GDI podia alterar alfa fuera de la pastilla, en las mismas
filas. La ruta GPU sube ahora esa franja completa, no toda la imagen; las 54
composiciones de buscaligero vuelven a ser identicas pixel a pixel. No se modifica
el dibujo CPU, ni se oculta el fallo debilitando la comparacion.

- Compilacion GPU y sin GPU en ambas ramas, con advertencias como errores.
- `pruebas/eficiencia.cs`: planes reducidos, puertas independientes, despertar
  HDD, reutilizacion y presupuesto de tablas, equivalencia de pixeles,
  propiedad/invalidacion de colores, diario incremental, bajas y linea cortada.
- `pruebas/verticesgpu.cs`: Direct3D HAL real, lectura de superficies,
  equivalencia pixel a pixel entre UP/anillo con transparencias, reutilizacion,
  vuelta del anillo, Reset, recreacion, seleccion automatica y liberacion.
- Suites existentes de caches/render, cierres/relevos de audio, fuentes,
  medidas, lectura anticipada, biblioteca, interfaces y modelos.
- Datos de prueba aislados. No se sustituyen ejecutables instalados, musica,
  configuracion ni modelos.

Ejecutar desde cada rama:
`./pruebas/validar-mejoras-20260929.ps1 -OutputDirectory <directorio-temporal>`
y `./pruebas/validar-capas.ps1 -OutputDirectory <otro-directorio-temporal>`.
El primer lanzador incluye las baterias nuevas. La prueba verticesgpu requiere
un dispositivo HAL disponible; no puede darse por validada sin el.

La prueba eficiencia admite `--dos-hilos` despues de la ruta del ejecutable:
restringe solo su propio proceso antes de cargar la rocola. Es una comprobacion
con afinidad reducida, NO un ensayo en procesador antiguo de 2C/2T.
No hay comprobacion fisica de Windows XP, de HDD/SSD del usuario, de DAC ni
medicion de FPS en su biblioteca real. Las mediciones HAL sinteticas no se
presentan como una ganancia garantizada: segun el controlador, el anillo puede
ser mas lento y por eso la seleccion es automatica.
