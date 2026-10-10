# Memoria de la sonoridad integrada — 10 de octubre de 2026

Continuacion de `MEMORIA_ANALISIS_2026-10-10.md`. Cambio compartido en Modest y HD, automatico, sin interruptores nuevos y sin modificar el aspecto ni el renderizado CPU.

Seguimiento de esta misma fecha: la limitacion WAV extensible documentada abajo queda resuelta en la pasada [Lectura WAV directa](LECTURA_WAV_DIRECTA_2026-10-10.md). Este informe conserva los resultados y hashes historicos de la optimizacion de memoria; usar el informe nuevo para el despliegue mas reciente.

## Que se ha implementado

El espacio de trabajo de cada hilo de analisis conserva ahora **un solo medidor BS.1770** reutilizable: filtros K en doble precision, historia de canales, anillo de subbloques, bloques de energia, histograma de picos y mapa de silencios.

- Se reutiliza solo si coinciden la frecuencia y todos los pesos de altavoces, incluidos sus bits. Cambiar de frecuencia, canales o disposicion sustituye la instancia; no crea un diccionario creciente por formatos.
- Antes de medir otra cancion se reinician TODOS los estados, contadores, cuadros partidos, bloques, picos y posiciones de silencio. No se reinicia entre los tres tramos de una misma medida.
- Un formato incompatible se rechaza sin alterar el medidor previo. Los pesos siguen siendo una copia privada, no una referencia mutable del llamador.
- `MeasureFile` y `MideIntegrada` usan el mismo espacio exclusivo del trabajador. La segunda ruta tambien reutiliza el PCM, conservando la lectura logica de `4096 * canales`; no usa la capacidad mayor que pudiera haber dejado otro formato.
- Las lecturas partidas siguen completandose en el medidor. Se conservan el orden de las operaciones, la ponderacion K, las puertas, el P99 y el mapa de silencios. El algoritmo guardado sigue siendo el **3**: no se invalidan medidas existentes.
- Las medidas publicadas son valores independientes, y la curva EQ sigue siendo un arreglo propio. No se publican los buffers del medidor reutilizable.
- Los hilos y las llamadas reentrantes mantienen medidores independientes. No se introduce un candado global ni se retienen lectores, formularios, rutas o delegados de cancelacion.
- La memoria se devuelve tambien en cancelaciones, errores y retornos anticipados.

No es una precarga de toda la biblioteca ni se acelera el disco aumentando la concurrencia. Se evita construir repetidamente los mismos objetos cuando el siguiente archivo tiene un formato compatible. La prioridad y las cesiones al usuario/carátulas no cambian.

## Limite de retencion, no de duracion

Un trabajador puede retener hasta **8192 bloques de energia**, 64 KiB de payload de `double`, ademas del histograma y los estados pequenos. La lista sigue creciendo cuando haga falta durante una cancion larga; si su capacidad supera ese limite, se descarta al terminar, incluso al cancelar. El siguiente analisis vuelve a empezar con una instancia pequena.

No se limita el numero de bloques medidos ni se trunca audio. La prueba procesa una cancion sintetica de **13 minutos y 40 segundos**: 6.560.000 cuadros a 8 kHz, 8197 bloques integrados y capacidad real de 16384. Su medida completa coincide con una instancia nueva; solo despues se libera la retencion grande.

El limite es por trabajador y se refiere al arreglo indicado, no a toda la memoria del proceso. Siguen existiendo reservas del lector/decodificador, pesos leidos del formato, resultados publicados, listas de cosecha y tablas espectrales. No se afirma que el analisis sea libre de asignaciones.

## Pruebas reproducibles

Nueva bateria `pruebas/memoriaintegrada.cs`, conectada a `validar-capas.ps1`, `validar-audio-mejoras.ps1`, `todo.sh` y al lanzador `validar-memoria-analisis.ps1` junto con la bateria anterior. El lanzador ejecuta ambas en nativo y x86; admite `-ApplicationPath` y una referencia previa con `-BaselinePath`.

Desde la raiz de cada rama, despues de compilar Release:

```powershell
.\pruebas\validar-memoria-analisis.ps1 -BaselinePath 'C:\respaldo\Rocola.exe'
```

Guardar la referencia antes de actualizarla. Sin `-BaselinePath` el lanzador avisa que las pruebas de reutilizacion NO acreditan equivalencia antes/despues.

Referencias de esta pasada, tomadas antes de editar:

| Rama | Commit anterior | SHA-256 del EXE anterior |
| --- | --- | --- |
| HD | `971076bab8677786cab57aac4fe75baa38df6a30` | `6435D12111A42148A026A7054E687542503757BE77ED8B1F9A14A03627EE4384` |
| Modest | `9a8d857c364b9827a66544a286cfd7e1bfe59015` | `A65F3245E40F712C3279186A3245998C00DE305BE70F190295F2A106C84F80CE` |

Resultados comprobados:

- Medidor integrado: **282 comprobaciones** correctas por rama y arquitectura, con referencia; filtros, puertas, picos, P99, silencios, formatos, pesos, reentrada, hilos y retencion. Los casos de 8–96 kHz incluyen el limite no entero de subbloques a 11.025 kHz, mono/estereo/seis canales directos y lecturas partidas.
- Ruta `MideIntegrada`: comparacion binaria de todos los campos publicados para WAV mono/estereo, silencio, cambio de formato, cancelacion, audio corto, archivos rotos/ausentes y cancion larga; `MeasureFile` intercalado en el mismo trabajador no contamina la siguiente medida.
- Bateria anterior `memoriaanalisis`: **147 comprobaciones** correctas por rama y arquitectura contra la referencia de esta pasada, incluidas tres cosechas no vacias, sus bytes/indice/CRC y curvas publicadas independientes.
- Release con advertencias como errores: correcto en ambas ramas.
- `validar-capas.ps1 -SinHal`: **56 baterias** correctas por rama, GPU/CPU. La omision de las pruebas HAL opcionales es explicita; hay comprobaciones sinteticas de graficos, no una medicion real de FPS.
- CPU congelado: **14 metodos de dibujo** con instrucciones, operandos resueltos, locales y excepciones identicos a su referencia previa en cada rama.

- `validar-mejoras-20260929.ps1`: **31 regresiones y 97 verificaciones de cache/pixeles/subidas imitadas** correctas por rama.
- `validar-audio-mejoras.ps1 -SkipLayers`: **32 baterias principales C# y 71 pruebas Python compartidas** correctas por rama; incluye DSEE, EQ, entrenadores, transiciones, limitadores, continuidad, cosecha y formatos reales MP3 CBR/VBR, AAC/M4A y WMA. Se repiten audio suave y estereo con afinidad limitada a dos procesadores logicos. HD pasa ademas las 28 pruebas de remuestreo de Apollo y las baterias propias de remuestreo DMO, salida exclusiva y decodificacion flotante. No hay inferencia real de Apollo ni escucha A/B en estos resultados.

Las pruebas sin referencia del medidor integrado cuentan 237 comprobaciones; las 282 anteriores incluyen ademas la comparacion con el ejecutable previo. Se conservan senales y registros en carpetas temporales aisladas, no en los datos de produccion.

## Limitacion de lectura encontrada, no introducida por el cambio

El WAV sintetico **5.1 IEEE float con WAVE_FORMAT_EXTENSIBLE** tiene cabecera y pesos de altavoces validos, comprobados por la bateria. En este PC lo rechazan tanto la referencia como el ejecutable nuevo. La apertura directa del lector de NAudio devuelve `NoDriver calling acmFormatSuggest` desde `AudioFileReader.CreateReaderStream`.

Esta comparacion comprueba que la optimizacion no altera la respuesta previa, pero **NO acredita decodificacion multicanal extensible completa**. Los calculos multicanal del medidor se comparan directamente contra instancias nuevas; el archivo desconocido sin mascara conserva su rechazo. No se presentan ambos fallos del lector como un audio multicanal medido correctamente.

Queda revisar en otra pasada una ruta directa de lectura WAV PCM/float extensible sin conversion ACM innecesaria, preservando mascara, muestras, saltos, duracion y cierre de recursos, y probarla en ambas ramas. El hallazgo no se mezcla con un cambio de decodificador dentro de esta optimizacion de memoria.

## Despliegue y comprobacion de fluidez

HD: actualizados exclusivamente `D:\ReleaseHD\Rocola.exe` y `Rocola.pdb`, con respaldo del par anterior y del nuevo en `respaldo_integrada_20261010_9a2fe1da86854cba8adcce6ef9af8d62`. Los hashes instalados coinciden con Release y con el par preparado. Los 26 archivos restantes de la raiz y los 25.367 archivos del paquete IA mantienen tamano y fecha; configuracion, modelos DSEE/EQ y NAudio mantienen tambien sus hashes. No se reemplazan musica, modelos, configuracion, creditos, medidas ni herramientas externas.

- SHA-256 EXE HD: `D0844A7145C072F156EB3BC93AFCD4D04BB89C1D00C83AFF1521F5132C825864`.
- SHA-256 PDB HD: `C78D1BAB4A60F2462B604429CDB12AB97C05007FD3BF385987DD7F2AA1AE9122`.

Modest: `D:\Release1\Rocola.exe` sigue abierto (PID 7652 al comprobarlo). No se cierra ni se sustituye. La compilacion verificada mas reciente de esta continuacion queda preparada con EXE `FDA6A45E6B73338636A0FF68355C359B9666765391F3FCA4CCD4203D1AF4A65D` y PDB `EECEE6283BCC7DD24E50286492DFE423743A2259A6BE1904036FE23D0CF15941`. Instalar este par despues del cierre con respaldo, no el par historico de las pasadas previas.

La reutilizacion de referencias y la igualdad de resultados son pruebas de ahorro de reservas repetidas y de conservacion del analisis, **no una medicion de GC ni de FPS de la estrella**. Falta el A/B real controlado Legacy/Natural, 2D/3D, navegacion/reposo, primero con una biblioteca sin repaso pendiente y despues con repaso activo, con la misma pista/resolucion/topes y percentiles p95/p99/max. No ejecutar compilacion/HAL/entrenadores durante esa comparacion, ni atribuir a GC la duracion de un tiron solo por coincidencia de contadores.

Modest conserva .NET 3.5/XP como destino. Las pruebas de 32 bits en este Windows y con afinidad limitada no sustituyen Windows XP, un HDD o un CPU antiguo real. La actualizacion coherente del paquete externo de Apollo y su inferencia/escucha real, descritas en el informe anterior de rendimiento, siguen siendo verificaciones distintas.
