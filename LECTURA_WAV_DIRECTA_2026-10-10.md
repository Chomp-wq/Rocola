# Lectura WAV directa — 10 de octubre de 2026

Continuacion de [Memoria integrada](MEMORIA_INTEGRADA_2026-10-10.md). Correccion compartida en Modest y HD, automatica, sin interruptor nuevo ni cambios visuales o al renderizado CPU.

## Defecto corregido

Un WAV 5.1 valido, IEEE float de 32 bits con `WAVE_FORMAT_EXTENSIBLE`, no se abria en este PC: el lector anterior intentaba convertirlo mediante ACM y fallaba con `NoDriver calling acmFormatSuggest`. Lo rechazaban reproduccion y analisis aunque sus muestras no necesitaban descompresion.

La biblioteca NAudio 1.9 instalada devuelve esa cabecera como `WaveFormatExtraData`, no como `WaveFormatExtensible`. Se comprobo directamente con su ensamblado y con un WAV generado. Por ello no basta llamar a `ToStandardWaveFormat` ni suponer que 32 bits siempre significa float: el subformato puede ser PCM entero.

## Implementacion

- `WavDirecto`, en `AudioControles.cs`, lee los WAV PCM y float con el lector RIFF de NAudio y adapta solo la descripcion del formato para su conversor de muestras. No usa ACM para esos archivos.
- Admite PCM de 8/16/24/32 bits y float de 32/64 bits, tanto en cabecera normal como extensible. Distingue los GUID completos de PCM y IEEE float. Conserva los canales entrelazados, su orden, frecuencia y amplitud, sin convertir a PCM de 16 bits ni remuestrear.
- PCM con menos bits validos que el contenedor conserva su alineacion izquierda: por ejemplo, 20 bits en 24 y 24 en 32. No se desplaza ni amplifica la señal.
- La mascara de altavoces sigue saliendo del archivo mediante `FormatoAudio.DeArchivo`. Se mantiene el layout, incluido centro/LFE y 5.1/7.1; una mascara desconocida/invalida no se inventa. No se cambia la receta ni la version de sonoridad existente.
- Reproduccion (`FileAudioSource`) y medicion (`LectorMedida`) usan la misma ruta. MP3 sigue utilizando su lector seguro y su cache; los demas formatos conservan sus lectores actuales, incluida Media Foundation flotante en HD.
- Un WAV de otro subformato conserva la ruta anterior de codecs, sin tratar sus bytes como PCM. Las cabeceras PCM/float incoherentes y los datos truncados se rechazan.
- El archivo se posee desde antes de construir el lector y se libera tambien si falla la cabecera, la adaptacion o la creacion del canal de muestras.
- Se leen directamente los bloques completos. Para una peticion que divide un cuadro se conserva como maximo **un cuadro de bytes**, reutilizado, y se entrega su resto en la siguiente llamada. No hay predecodificacion de toda la cancion ni reservas por bloque en el adaptador.
- El reloj descuenta los bytes pendientes: precargar un cuadro no adelanta el tiempo mostrado. Buscar limpia los pendientes, alinea por cuadro y conserva los tiempos exactos de reproduccion. Se mantiene la semantica anterior de tiempo del lector de medida.
- Se mantiene la limpieza de NaN/infinito/muestras fuera de rango en la medicion, el volumen, EOF y el cierre repetido seguro. El padding RIFF y los chunks posteriores no se leen como audio.

La mejora evita una conversion y dependencia innecesarias; no se presenta como una mejora audible del audio que ya funcionaba, ni como soporte probado del DAC multicanal.

## Verificacion de esta pasada

- Release de ambas ramas compila con advertencias tratadas como errores. Modest conserva .NET 3.5; HD .NET 4.8.
- `validar-wav-directo.ps1 -BaselinePath ...`: **7.241 comprobaciones por arquitectura**, nativo y x86, en cada rama. Son 17 combinaciones de formato y layout, mas fallos y cierre de archivos; se comparan todas las muestras, no solo el nivel medio.
- Los seis WAV normales ya soportados se comparan contra el ejecutable previo. Los extensibles que antes se rechazaban se comparan contra el PCM esperado generado independientemente, no contra dos fallos iguales.
- Incluye bloques que dividen canales, offsets, bordes intactos, volumen, duracion, seek con datos pendientes, tiempos, EOF, cabeceras incoherentes, otro subformato, archivos truncados y doce fallos consecutivos sin fugas.
- Sonoridad integrada, picos y silencios de los extensibles coinciden bit a bit con un medidor alimentado directamente por las muestras de referencia; RMS/EQ tambien se comprueban mediante la ruta productiva.
- `validar-memoria-analisis.ps1 -BaselinePath ...`: **147 comprobaciones** de workspace y **283 de integrada**, nativo/x86, por rama. RMS, duracion, EQ, integrada, picos, silencios y cosecha de los formatos anteriores siguen identicos. El WAV 5.1 ahora se admite y se verifica contra muestras independientes.
- `validar-capas.ps1 -SinHal`: **57 baterias por rama**. La comparacion HAL opcional se omite expresamente; no certifica aspecto ni FPS reales.
- CPU congelado: **14 metodos de dibujo** mantienen instrucciones, operandos resueltos, locales y excepciones identicos a la referencia anterior.
- `validar-mejoras-20260929.ps1`: **31 regresiones y 97 verificaciones de cache/pixeles/subidas imitadas** correctas por rama.
- `validar-audio-mejoras.ps1 -SkipLayers`: **33 baterias principales C# y 71 pruebas Python compartidas** correctas por rama. Incluye DSEE, EQ, entrenadores, continuidad, transiciones, limitadores, cosecha y apertura de archivos reales MP3 CBR/VBR, AAC/M4A y WMA, con PCM identico en esas comparaciones. Audio suave/estereo se repite con afinidad limitada a dos procesadores logicos. HD pasa ademas 28 pruebas Python de remuestreo Apollo y sus baterias DMO, salida exclusiva y decodificacion flotante (69 comprobaciones). No se ejecuta inferencia real de Apollo ni escucha A/B.

Las pruebas generan archivos y datos aislados. No se modifica musica, creditos, configuracion ni modelos de produccion; tampoco se entrena sobre los datos del usuario.

Para repetir la verificacion especifica en Windows, ejecutar `pruebas/validar-wav-directo.ps1`; `-BaselinePath` es opcional. Sin referencia sigue comparando todos los formatos contra las muestras conocidas, pero no acredita equivalencia del ejecutable previo.

## Referencias de formato y biblioteca

- [Microsoft: WAVEFORMATEXTENSIBLE](https://learn.microsoft.com/en-us/windows/win32/api/mmreg/ns-mmreg-waveformatextensible), subformato, mascara, bits validos y tamaño del contenedor.
- [NAudio: AudioFileReader, fuente v1.10](https://github.com/naudio/NAudio/blob/v1.10.0/NAudio/Wave/WaveStreams/AudioFileReader.cs), seleccion del lector y conversion ACM. Es una referencia publicada proxima, no se atribuye ese tag a la version instalada.
- [NAudio: SampleChannel, fuente v1.10](https://github.com/naudio/NAudio/blob/v1.10.0/NAudio/Wave/SampleProviders/SampleChannel.cs), conversion PCM/float y volumen. La conducta de la version 1.9 usada por la Rocola se comprobo con el ensamblado instalado.

## Despliegue y limites pendientes

HD: instalados exclusivamente `D:\ReleaseHD\Rocola.exe` y `Rocola.pdb`, con respaldo del par anterior y del nuevo en `respaldo_wav_20261010_cead5117ce7f47559b258f4ffb3c6a0f`. Los hashes instalados coinciden con Release y con la copia preparada; los 26 archivos restantes de la raiz y los 25.367 archivos IA conservan tamaño y fecha. Configuracion, modelos DSEE/EQ y NAudio conservan ademas sus cuatro hashes.

- EXE HD: `DC74DB18C81C05D2A172063E3779E6D3CB7A6CD4EF04A8269EF384500C7F95DA`.
- PDB HD: `D65A2588DF7147A855A44FADC1AA05E3F27A69ED1AF0FA917E2B1F0F33A8B73F`.

Modest: `D:\Release1\Rocola.exe` sigue abierta (PID 21828 al comprobarlo). No se cierra ni se reemplaza. El usuario confirma que la esta usando y autoriza actualizarla si se detecta cerrada durante el trabajo. Queda preparado el ultimo Release verificado, que sustituye los pares historicos de las pasadas anteriores:

- EXE Modest: `515401E6EB2B69457EB1027B07EAE7BC4AE2AFDBF4E9CC43E0F336D1898FD27C`.
- PDB Modest: `DAE0B5E4D880E0C48F2BB14F10BD99C3DFAB4D0D53DC28BE8EE1AA770F15F277`.

Cuando se encuentre cerrada, respaldar su par anterior y sustituir solo EXE/PDB, conservando musica, modelos y datos. No instalar una compilacion historica anterior a esta pasada.

Esta correccion no toca la estrella. Sigue pendiente su A/B real Legacy/Natural, 2D/3D, navegacion/reposo, con y sin repaso de biblioteca y con p95/p99/max de cuadros. No mezclarlo con compilacion, pruebas HAL ni entrenamientos. Las pruebas actuales no certifican ausencia de tirones, compatibilidad real con XP, un HDD ni un procesador antiguo.
