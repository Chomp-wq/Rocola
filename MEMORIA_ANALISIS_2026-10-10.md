# Memoria de trabajo del analisis musical — 10 de octubre de 2026

Continuacion de `RENDIMIENTO_Y_FLUIDEZ_2026-10-10.md`. Es una optimizacion comun de audio en Modest y HD, automatica y sin nuevos interruptores. No cambia el aspecto, la calidad, las recetas de medida ni el renderizado CPU.

## Que cambia

`MeasureFile` reutiliza un espacio de trabajo exclusivo de su hilo. Conserva los buffers PCM, filtros K, estados de bandas, contadores de recorte, ventanas de analisis, analizadores espectrales y listas pequenas de cosecha. La siguiente cancion del mismo formato ya no necesita reservarlos de nuevo.

- Los estados y sumas se ponen a cero al empezar **otra cancion**, no entre los tres tramos de la misma medida.
- Se conserva el bloque logico original de `4096 * canales`, aunque el arreglo retenido sea mayor. Cambiar de ocho canales a mono no cambia el recorrido.
- Los coeficientes se calculan con la misma receta cuando cambia la frecuencia. Solo se conserva un formato de analizadores por hilo; no hay un diccionario creciente por frecuencia.
- Los analizadores L/R de cosecha y el de calidad son independientes. La ventana de medida sigue siendo 2048 en ambas ramas. El reinicio rechaza fuentes/STFT de reproduccion y formatos incompatibles.
- La curva EQ publicada sigue siendo un arreglo nuevo e independiente. El siguiente analisis no puede modificar una curva ya devuelta, almacenada o pendiente de publicacion.
- Cada hilo tiene su propia memoria. Una llamada reentrante recibe un espacio temporal, sin borrar el analisis externo. El espacio no conserva formulario, lector, ruta, resultado publicado ni delegado de cancelacion.
- El lector se sigue cerrando y el espacio se devuelve tambien ante cancelaciones, errores y retornos anticipados.

## Limites de memoria, no de funcionalidad

Se retiene PCM hasta ocho canales: `4096 * 8` flotantes, 128 KiB de datos. Los formatos con mas canales siguen midiendose sin recortarlos, pero usan memoria temporal que no queda en la cache del trabajador.

Cada una de las dos listas de cosecha puede retener hasta 65.536 flotantes, 256 KiB de datos por lista. Un analisis profundo puede crecer por encima de esa cifra y escribir **todos** sus ejemplos; al terminar se descarta esa lista grande. Las listas pequenas se vacian y reutilizan. No se cambia la duracion, la seleccion de cuadros, los limites de cosecha ni el orden de los ejemplos.

Estas cifras son payload de los arreglos indicados, no la memoria total del proceso. Ademas existen estados/ventanas de analisis, cabeceras de objetos, tablas FFT compartidas, el lector y la integrada.

## Verificacion reproducible

Nueva bateria `pruebas/memoriaanalisis.cs`, integrada en `validar-capas.ps1`, `validar-audio-mejoras.ps1` y `todo.sh`. Comprueba reutilizacion real de referencias, aislamiento de hilos/reentrada, formato, Nyquist, limpieza de estados, independencia de resultados, descarte de listas grandes y que el reinicio nunca toque reproduccion.

El lanzador `pruebas/validar-memoria-analisis.ps1` usa por defecto el Release ya compilado y ejecuta la bateria en modo nativo y x86. Con `-ApplicationPath` admite otra compilacion. Con `-BaselinePath` compara ademas una copia de la version anterior. Sin referencia lo indica expresamente: las comprobaciones estructurales no certifican equivalencia antes/despues.

Ejemplo desde la raiz de cada rama, tras compilar Release y guardar una referencia previa:

```powershell
.\pruebas\validar-memoria-analisis.ps1 -BaselinePath 'C:\respaldo\Rocola.exe'
```

La comparacion genera solo senales sinteticas y datos temporales aislados. Incluye 15 WAV con frecuencias de 8–96 kHz, mono/estereo/multicanal, cambios de formato, silencio, recorte, cancelacion, tres tramos y analisis profundo de 75 segundos. Verifica bit a bit RMS, duracion, curva EQ, integrada, picos, mapa de silencios, corpus e indice/CRC de cosecha. Tres casos escriben material de IA no vacio; el caso profundo supera el limite de memoria retenida sin truncar datos.

Tambien compara un archivo roto y la ruta logica de video seguida de musica. Para esta ultima clasifica temporalmente un WAV conocido como video en los ensamblados de prueba: verifica que no se publiquen EQ, integrada ni cosecha. **No** es una prueba de un decodificador de video real.

Referencias anteriores a esta optimizacion, guardadas antes de editar:

- HD: commit `33927ac1090abbc9dc64cf1d461c9252aec9790d`; SHA-256 del Release `44807F7400D778CFE94A216F4C09C55914AC2DB8765CAF73B1A671A29753BEFE`.
- Modest: commit `1dc6cab5f7c4a2fb83e4f6bde1a8b7e3712022b8`; SHA-256 del Release `404904923218AC27E5BFD893498D35DF8728882735CB971D40B2DCB93E9A2FA2`.

Resultados de cierre:

- Equivalencia contra esas referencias: 146 comprobaciones correctas por rama y arquitectura; nativo y x86.
- Release con advertencias como errores: correcto en ambas ramas.
- `validar-capas.ps1 -SinHal`: 55 baterias correctas por rama, compilaciones GPU/CPU incluidas. La omision HAL opcional es explicita; no es una medida de FPS con musica real.
- CPU congelado: 14 metodos de dibujo mantienen instrucciones, operandos resueltos, locales y excepciones identicos a la referencia previa en cada rama.
- `validar-mejoras-20260929.ps1`: 31 regresiones y 97 verificaciones de cache/pixeles/subidas imitadas correctas por rama.
- `validar-audio-mejoras.ps1 -SkipLayers`: correcto por rama; 31 baterias principales C#, 71 pruebas Python compartidas, formatos MP3 CBR/VBR, WMA/AAC y repeticion de audio suave/estereo con afinidad a dos procesadores logicos. HD pasa ademas las 28 pruebas de remuestreo de Apollo y las baterias propias de salida moderna, exclusivo y decodificacion flotante. No hay inferencia real de Apollo ni escucha comparativa en este resultado.

## Despliegue de esta continuacion

HD: actualizados exclusivamente `D:\ReleaseHD\Rocola.exe` y `Rocola.pdb`, con copia recuperable del par anterior y del nuevo en `respaldo_memoria_20261010_ec9466bd6a6a4f898d868fac5ec5fb9c`. Se verifico la coincidencia exacta de los archivos instalados con Release y el paquete preparado. No se sustituyen configuracion, modelos, biblioteca, medidas ni herramientas externas.

- SHA-256 EXE HD: `6435D12111A42148A026A7054E687542503757BE77ED8B1F9A14A03627EE4384`.
- SHA-256 PDB HD: `5FFBB2023E92BBA0D438F4DEA422877C8EC822A64F4D0BC6A508AD76A00F341F`.

Modest: `D:\Release1\Rocola.exe` sigue abierto; no se cierra ni se reemplaza. Queda compilada y verificada la continuacion con EXE SHA-256 `A65F3245E40F712C3279186A3245998C00DE305BE70F190295F2A106C84F80CE` y PDB `1EEB206F9C0EBE8C566860BBBC1D03ECD2D6863B58677FB1B36F852334268AB4`. La proxima instalacion debe usar este par actualizado, no el Release anterior a la reutilizacion del analisis.

## Que no resuelve ni certifica esta pasada

No elimina las asignaciones del indice/decodificador MP3 de NAudio, las copias del escritor de cosecha ni las asignaciones del medidor de sonoridad integrada. Este ultimo sigue usando instancias nuevas: reutilizarlo exige un reinicio completo y su propia comparacion, no basta con vaciar la lista de bloques.

Reducir reservas repetidas es un ahorro verificable por reutilizacion de buffers, no una medicion de la duracion de GC ni prueba de que GC causaba todos los tirones. Falta la comparacion real controlada Legacy/Natural, 2D/3D, navegacion/reposo, primero sin repaso pendiente y despues con repaso activo, usando intervalos p95/p99/max. La Rocola de produccion abierta no se cierra por cuenta del agente.

Modest conserva compilacion de destino .NET 3.5/XP; las pruebas x86 en este Windows y la afinidad a dos hilos no sustituyen un equipo XP, HDD o procesador antiguo real.
