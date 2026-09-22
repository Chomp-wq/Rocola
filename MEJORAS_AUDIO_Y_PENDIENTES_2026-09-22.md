# Mejoras de audio: implementación y guía para continuar
Fecha: 22 de septiembre de 2026.

## Estado de esta entrega

Aplicado a **rocola-hd** (.NET 4.8) y **claude/modest-carson-fa2v2n** (.NET 3.5, destino XP).
No se modificó el renderizado por CPU. No se añadieron interruptores ni dependencias de
audio nuevas. No se desplegaron ejecutables ni se reemplazaron música, modelos, créditos,
preferencias o datos de las instalaciones del usuario.

Este documento separa lo que YA está implementado de propuestas que todavía necesitan
desarrollo y medición. La revisión anterior está en REVISION_AUDIO_2026-09-22.md.

| Apartado | Estado |
|---|---|
| Cambios de volumen y excitador suaves | Implementado y probado |
| Transición entre ecualizaciones | Implementado y probado |
| Excitador consistente entre frecuencias de muestreo | Implementado y probado |
| Huella AutoEQ por potencia de canales | Implementado y probado |
| Migración incremental de huellas antiguas | Implementado y probado |
| Medición de cortes independiente de recolectar IA | Defecto adicional corregido y probado |
| Guardián del entrenador DSEE y mejor checkpoint evaluado | Implementado y probado; modelos actuales intactos |
| Normalización del entrenador AutoEQ sin usar validación | Implementado y probado |
| Escucha A/B, DAC, XP real y CPU antiguo | Pendiente |
| Sonoridad integrada, true-peak, antialiasing, salidas y continuidad | Pendiente; plan abajo |
| Paridad del extractor IA (contrato EXTRACTOR=2) | Implementado y probado; ver 1.7 |
| Identidad de origen, particiones por canción y estéreo por canal | Implementado y probado; ver 1.8 |
| Selección contra curva e instalado, informe reproducible | Implementado y probado; ver 1.9 |
| Publicación conjunta de modelo y vectores | Implementado y probado; ver 1.10 |

## 1. Qué se implementó y cómo funciona

### 1.1 Controles suaves en reproducción

Archivos: Rocola/AudioControles.cs y LevelTap en Rocola/Form1.Sonido.cs.

- Gain × Master recorre una rampa lineal de **20 ms**, calculada según la frecuencia del audio.
- Excite usa otra rampa de 20 ms. Al desactivarlo, termina la rampa y deja de procesarse.
- Las rampas avanzan una vez por cuadro multicanal, no por canal. Conservan su posición
  aunque Read reciba cantidades que no sean múltiplos del número de canales.
- Si cambia el objetivo antes de acabar, la nueva rampa parte del valor actual.
- Al abrir una canción, su configuración inicial se aplica directamente: no se debilita
  artificialmente el primer golpe con un fundido añadido.
- Los objetivos se leen por bloque; el equipo de salida puede tener audio ya almacenado.
  Los 20 ms describen la transición DSP, NO garantizan 20 ms desde pulsar un control.
- Sin ganancia, EQ o excitador activos se conserva la ruta de paso directo.

El EQ mezcla la salida del filtro anterior y la del nuevo durante **30 ms**. Cada filtro
conserva sus propios estados y coeficientes. No se interpolan coeficientes de biquads.
Hay como máximo dos cascadas activas durante la transición; si llegan más ajustes, se
termina la mezcla y se toma el último objetivo disponible. No crece una cola de filtros.

Los coeficientes se preparan fuera del bucle de muestras. La etapa nueva no reserva
arreglos por muestra. Esto no significa que toda la cadena histórica carezca de
asignaciones: ReportAudioBlock sigue teniendo sus propios caminos de inicialización.

Coste: una rampa pequeña y trabajo doble de EQ únicamente durante su transición.
No se aumentaron tamaño de red, FFT, número de trabajadores ni frecuencia de salida.
Aun así, no se promete coste cero ni ausencia de clics para cualquier driver/equipo sin
escucha real: las pruebas verifican continuidad numérica y llegada al objetivo.

### 1.2 Filtro del excitador

El coeficiente anterior era 0,55 a todas las frecuencias. Ahora:

    a(fs) = 1 - 0,45^(44100 / fs)

A 44,1 kHz se conserva exactamente 0,55. A otras frecuencias se conserva la constante
temporal del polo de referencia. Se calcula una vez al construir la etapa.
Esto no elimina el aliasing de la no linealidad; eso es otra tarea pendiente.

### 1.3 Medición AutoEQ que no cancela el estéreo

Archivos: AudioControles.cs, MeasureFile en Form1.Sonido.cs, ReportAudioBlock
(en Form1.Sonido.cs de HD y Form1.Video.cs de Modest), Form1.Datos.cs,
Form1.Biblioteca.cs. Las constantes de formato están en Form1.Fondo.cs;
ese cambio es de datos de audio, no de renderizado.

Antes se filtraba (L + R) / 2. Con L = -R la medida quedaba sin energía.
AccumulateEqFrame ahora filtra cada canal con estados independientes y acumula:

    energía[banda] += suma(salida_del_filtro[canal]^2) / número_de_canales

Se mantienen las ocho bandas, su referencia a la mediana del cuerpo y los límites de
corrección existentes. Esto es una huella tonal; NO una medida multicanal BS.1770
con ponderaciones de altavoces y tratamiento de LFE.

La medición requiere más filtros durante el aprendizaje —dos por banda en estéreo—,
no durante la reproducción normal de una canción cuya huella ya está actualizada.
La migración de una biblioteca grande añade lectura/decodificación temporal; se mantienen
los límites y prioridades existentes de sus trabajadores, sin lanzar un barrido paralelo nuevo.

Formato de rocola_eq.txt:

    b0;b1;b2;b3;b4;b5;b6;b7;corteHz;recorte;corteGrave;versionHuella|ruta

EqLen pasa de 11 a 12; el último campo vale 1 para potencia por canales.
Las líneas antiguas de 8 a 11 valores siguen cargándose y reciben versión 0.
Se utilizan hasta sustituirlas; no se borra el caché ni se pierde su información de cortes.
Al reemplazar una huella vieja, NO se promedia 50/50 con la medida mono equivocada.
Las huellas nuevas sí conservan el suavizado histórico entre medidas nuevas.

TandaPorMedir vuelve a admitir huellas antiguas aunque la duración o el repaso completo
estuvieran registrados. No obliga a repetir eternamente archivos cortos que tienen
duración pero nunca produjeron huella. La reproducción puede aprender una huella faltante
o antigua y la publica al cerrar su medición, manteniendo el guardado asíncrono existente.

**Precaución de reversión:** un ejecutable anterior que solo admite hasta 11 campos puede
ignorar las líneas nuevas de 12. Antes de desplegar, respaldar rocola_eq.txt. Para volver
a un ejecutable viejo, restaurar SU copia, no borrar las mediciones originales.
El entrenador AutoEQ sigue leyendo las primeras ocho bandas y el corte, tolerando el
campo adicional. Conviene reentrenarlo después de completar la migración, no mientras
casi toda la biblioteca aún usa la medida antigua. No se invalidó ni reemplazó la red existente.

### 1.4 Recolectar ejemplos ya no cambia la ficha de calidad

Una prueba con ruido de banda ancha mostró que activar iaHarvestOn podía cambiar el corte
de graves almacenado: la FFT había observado distintas cantidades de audio.
MeasureFile ahora toma los cortes de la misma ventana inicial de **IaWarmFrames + 24**
cuadros útiles (actualmente 88), tanto al recolectar como al no hacerlo. El analizador
puede seguir avanzando para generar ejemplos sin modificar esa ficha.

Se mantienen el reparto y límites de cuadros cosechados. Con recolección activa puede
haber hasta 24 FFT iniciales adicionales cuando antes se saltaban por el reparto.
No se cambió el criterio de aceptación de cada par ni se declaró que una ventana inicial
sea un análisis completo de todos los cambios de una canción.

### 1.5 Entrenamiento DSEE más seguro

Archivos: ia/calidad_dsee.py, ia/entrenar_ia.py, ia/entrenar_dsee.py.

- Se guarda en memoria el mejor checkpoint FINITO entre los evaluados: primera época,
  múltiplos de diez y última. No se afirma que sea el mejor entre todas las épocas.
- Se validan dimensiones y pesos del contrato RIA1: 28 entradas, 12 salidas,
  de 1 a 8 matrices y hasta 512 neuronas por capa.
- Los pesos se redondean a float32 ANTES de la evaluación final. La aritmética de NumPy
  no pasa por ello a ser idéntica a la de C#; la paridad de inferencia se prueba aparte.
- La evaluación se hace por lotes, también sobre los datos mapeados; acumula MSE de
  cada una de las doce bandas, sin materializar todo el conjunto.
- Para publicar se exige como política inicial al menos **5% de reducción del MSE global**
  y ninguna banda con **más de 10% de aumento de MSE**, con tolerancia numérica de 1e-12.
  Se rechazan NaN, infinito, datos inválidos y una referencia ya prácticamente perfecta.
- Si no cumple, devuelve código 2 y conserva modelo y vectores anteriores.
  El lanzador explica que el rechazo es una decisión de calidad.
- RIA1 y vectores se escribían mediante sustitución atómica por archivo, pero el conjunto no
  era una transacción. **Resuelto en 1.10**: ahora se publican como una versión con manifiesto.

La referencia es la curva fija histórica del entrenador, NO una reproducción PCM completa
del reconstructor clásico. Los márgenes 5%/10% son criterios conservadores iniciales,
no un estándar perceptual ni garantía de que la red sea mejor para toda música.
NO se entrenó ni instaló un modelo nuevo del usuario en esta pasada.

### 1.6 Entrenador AutoEQ

Archivo: ia/entrenar_eq.py, normalizar_entrenamiento.
Primero se divide el conjunto; media y desviación se calculan exclusivamente con canciones
de entrenamiento. Después se transforman entrenamiento y validación con esas estadísticas.
Al exportar se absorbe la normalización en la primera capa, conservando el formato RIA1.

Esto corrige una fuga de información en la normalización. No equivale a una división
por artista ni corrige automáticamente otros posibles sesgos. El promedio de referencia
de canciones sanas sigue siendo el utilizado por la biblioteca, como antes.

### 1.7 Contrato del extractor DSEE (entrega 1 del bloque A)

Archivos: ia/contrato_dsee.py (nuevo), ia/entrenar_ia.py, ia/calidad_dsee.py,
ia/paridad_extractor.txt (nuevo), pruebas/iaparidad.cs (nueva).

Manda la geometría de la rocola: la red se aplica en RegenNeural y la cosecha sale de MakePair.
El extractor Python anterior (ahora llamado versión 1) se apartaba en cinco cosas, no en una:

| | Rocola (C#) | Extractor Python v1 |
|---|---|---|
| Techo de los objetivos | topBin = mín(20,5 kHz, 95 % Nyquist, 2·corte−2 bins, ny−4) | mín(2·corte, 20 kHz, 95 % Nyquist) |
| Corte | redondeado a bin | continuo |
| Entrada 25 y bandas de entrada | con el corte redondeado | con el corte sin redondear |
| Tope superior del objetivo | +0,1 | +0,5 |
| Sub-banda vacía | el par no vale | se estiraba un bin |

La diferencia de 20 kHz frente a 20,5 kHz no es solo una constante: el techo de la rocola sale
de redondear a bins, del límite 2·corte−2 (la fuente t/2 debe quedar bajo el corte) y de
Nyquist. contrato_dsee.py transcribe MakePair/BuildIaFeatures con el mismo redondeo float32 y
entrenar_ia.py lo usa (EXTRACTOR = 2). El filtro de «cuadro casi mudo» pasó a donde lo aplica la
cosecha (energía del cuadro ≤ 1e-8, antes de analizar) en vez de formar parte del extractor.
curva_fija usa el mismo techo (geometría de 44,1 kHz y cuadro 2048).

Datos y modelos: la reproducción no cambió, así que los modelos RIA1 instalados funcionan igual.
MakePair y BuildIaFeatures no han cambiado desde su creación (3 de agosto de 2026, comprobado en
el historial), por lo que rocola_ia_datos.bin ya está en la geometría v2 y no se reinterpreta.
Lo afectado eran los modelos entrenados con --musica o audio sintético: aprendían bandas que no
son las que la rocola rellena. No se reentrenó ni reemplazó ningún modelo.

Comprobado (automático, Mono en Linux, ambas ramas):
- 300 casos del archivo de paridad (178 pares válidos, 122 rechazados igual): diferencia máxima
  9,7e-9 en log10 con espectro inyectado y 3,3e-6 partiendo de la señal (FFT float32 de la rocola
  frente a FFT de doble precisión).
- Barrido de todos los cortes a 22,05/32/44,1/48/88,2/96 kHz: 17.712 pares y 12.839
  reconstrucciones en HD (cuadros 2048 y 4096) y 5.784 y 4.214 en Modest (solo 2048); cero
  discrepancias, peor diferencia 4,8e-7.
  Incluye las bandas que rellena RegenNeural, medidas por los bins que toca.
- La batería se pone roja al cambiar el techo a 20 kHz, el tope a +0,5, un límite de banda o el
  redondeo del corte, y al alterar un valor del archivo de paridad.

Riesgo medido, no comprobado en XP: de 500.721 divisiones hz/binHz de toda la geometría, 5.312
(1,06 %) caerían en otro bin si se calcularan sin redondear a float32, lo que puede hacer la FPU
x87 de un Windows XP de 32 bits. En una misma máquina la cosecha y la reproducción usan el mismo
código, pero la paridad exacta con Python en XP solo se sabrá corriendo pruebas/iaparidad.cs allí
(con Python instalado o fijando ROCOLA_TEST_PYTHON).

### 1.8 De qué canción viene cada ejemplo, y particiones que no mezclan (entrega 2)

Archivos: Rocola/Form1.Ia.cs y Form1.Sonido.cs (índice al cosechar), ia/origen_dsee.py (nuevo),
ia/entrenar_ia.py, ia/entrenar_dsee.py, ia/preparar_entrenamiento.py (HD), pruebas/iaorigen.cs.

rocola_ia_datos.bin (RIAD v1) no cambia: los lectores y ejecutables anteriores siguen funcionando.
Al lado, la rocola escribe rocola_ia_datos.idx: una entrada de 64 bytes por canción cosechada con
primer registro, cuenta, versión del extractor, frecuencia, cuadro, canal, huellas FNV-1a de la
canción (nombre de archivo + duración), de la carpeta y del artista, CRC32 de esos registros y CRC32
de la propia entrada. Solo huellas: ni rutas ni nombres. El artista queda en 0 («desconocido»): la
lista de canciones vive en el hilo de la pantalla y no se lee desde el hilo de fondo.

Robustez (nunca atribuir mal; como mucho quedarse «sin origen»):
- Se escribe después de que los ejemplos llegaron al disco; un apagón entre ambos deja esos
  ejemplos sin origen.
- Entrada cortada, con CRC malo, fuera del archivo de datos, que pisa otra o cuyos datos no
  coinciden con su CRC: se ignora. Si el archivo de datos vuelve a empezar, el índice viejo se
  aparta como .anterior; un índice ajeno no se toca.
- Coste medido (Mono, sin disco real): CRC de 0,66 ms por canción del repaso normal y 5 ms por
  canción a fondo, en el hilo de fondo; más 64 bytes y un vaciado a disco por canción.

Entrenamiento:
- Grupos por unión de canción, carpeta y artista (--agrupar artista|carpeta|cancion; por defecto
  artista). Una canción cosechada en dos carpetas une ambas. Si un grupo supera la mitad de los
  ejemplos se baja de nivel y se informa.
- Tres particiones por grupo, antes de tomar cuadros: entrenamiento, validación (solo elige el
  checkpoint) y prueba (solo decide la publicación). Reparto determinista; mínimos de 3 grupos y
  200 ejemplos en validación y prueba.
- Sin índice (todo el material juntado hasta ahora) no se inventan grupos: se entrena para poder
  mirar, se avisa «VALIDACIÓN NO INDEPENDIENTE» y se devuelve el código 3 sin publicar. Lo mismo
  si se suman ejemplos sin origen con --incluir-sin-origen. entrenar_dsee.py conserva el modelo
  anterior con el código 3 y reconoce el .idx.
- WAV: cada canal da sus ejemplos (antes se promediaba L y R y una contrafase L = s, R = −s
  quedaba en silencio); los canales de un archivo llevan el mismo grupo. preparar_entrenamiento.py
  deja origen.tsv con la huella de la carpeta de cada WAV. Los trozos se guardan en float32.
- Memoria: repartir 30.000 filas de un material de 19,2 MB con 3.000 canciones pide 2,5 MB de pico
  (tracemalloc) y 0,5 s; el material se sigue leyendo por lotes (memmap).

Comprobado (automático): pruebas/iaorigen.cs escribe con el código de la rocola, rompe el índice
de todas las formas anteriores y lo hace leer al lector Python; se pone roja si la cosecha vuelve
al guardado sin origen, si el índice viejo no se aparta, si se escribe tras una entrada cortada o
si el CRC de la entrada se calcula mal. test_origen_dsee (300 repartos al azar sin ningún grupo en
dos particiones, RIAD v1 sin grupos inventados, memoria) y test_estereo_dsee. De punta a punta con
material sintético: con índice llega a decidir (en esa prueba rechazó: tres bandas más de 10 %
peor); sin índice devuelve 3 y deja modelo y vectores intactos.

Pendiente que necesita tus datos: que la rocola nueva coseche con índice (días de uso o una noche
de repaso a fondo). Con el rocola_ia_datos.bin actual el entrenador se negará a publicar.

### 1.9 Selección del modelo e informe reproducible (entrega 3)

Archivos: ia/calidad_dsee.py (cargar_ria1, evaluar_tres, decide), ia/entrenar_ia.py,
ia/entrenar_dsee.py, pruebas/dsee.cs, ia/test_seleccion_dsee.py (nueva).

- El candidato se compara, sobre la MISMA partición de prueba, con la curva fija y con el modelo
  instalado. El instalado se lee con las mismas reglas que RedChica.TryLoad; si la rocola no lo
  cargaría, se dice y solo cuenta la curva (que es lo que la rocola usa en ese caso).
- Política inicial, escrita como tal en el informe: contra la curva, al menos 5 % de mejora global
  y ninguna banda más de 10 % peor (la de antes); contra el instalado, al menos 2 % global y
  ninguna banda más de 10 % peor. Una red más grande que empata no gana: el tamaño se informa
  pero no puntúa. No son prueba de superioridad audible.
- Se conservan finitos, dimensiones, redondeo float32 antes de evaluar, errores por banda y mejor
  checkpoint (ahora con su época en el informe). --epocas cambia las épocas (60 por defecto).
- informe_dsee.json se escribe en cada entrenamiento, publique o no: extractor, huella de los
  datos, reparto (grupos y ejemplos), checkpoint, errores por banda de los tres en validación y
  prueba, estado y SHA-256 del instalado, política, decisión con sus motivos y SHA-256 del
  candidato. Sin fechas ni rutas: la misma entrada da el mismo archivo byte a byte (probado).
- Los vectores de inferencia salen de la partición de prueba y su primera línea lleva el SHA-256
  del modelo. pruebas/dsee.cs lo comprueba si está (los del repositorio son del formato anterior y
  lo dice). entrenar_dsee.py compara con el rocola_ia.bin arrastrado o con el que tenía la carpeta.

Comprobado: test_seleccion_dsee (gana a la curva pero pierde contra el instalado → no; gana a los
dos → sí; empate de una red más grande → no; una banda mucho peor → no; no finitos; lotes; lector
RIA1 contra archivos truncados, con bytes sobrantes, NaN o del EQ; informe idéntico en dos
corridas; paridad de los vectores con el modelo leído, 2e-6). En rojo al quitar la comparación con
el instalado, al meter una fecha en el informe o al estropear la huella de los vectores.

### 1.10 Publicación conjunta de modelo y vectores (entrega 4)

Archivos: ia/publicar_dsee.py (nuevo), ia/entrenar_ia.py, ia/entrenar_dsee.py,
ia/test_publicacion_dsee.py (nueva).

- La versión se escribe entera en ia/modelos/.preparando-*: modelo, vectores e informe, y el
  manifiesto (SHA-256 y tamaño de cada archivo, versión del extractor y su propio SHA-256 al
  final) el último; todo forzado a disco. Luego se renombra de una vez a ia/modelos/dsee-<huella>.
- Activar es reemplazar de forma atómica ia/modelos/ACTIVO («versión sha256-del-manifiesto»):
  es el ÚNICO paso que decide. Las versiones anteriores quedan en HISTORIAL para volver atrás
  (python publicar_dsee.py --activar dsee-…; --estado; --recuperar).
- ia/rocola_ia.bin, ia/ia_vectores_prueba.txt y la carpeta entrenado/ son COPIAS de la versión
  activa. recuperar() (al empezar cualquier entrenamiento) quita versiones a medio preparar,
  comprueba la activa byte a byte contra su manifiesto —si está dañada vuelve a la última sana
  del historial— y rehace las copias que no cuadren.
- La primera vez, el par que ya hubiera se archiva como versión «previo-…» y queda activo antes
  de tocar nada; se puede volver a él.
- entrenar_dsee.py deja de apartar y devolver copias .anterior del DSEE (las del EQ siguen igual);
  si no se publica, no se toca nada.
- Qué se publica: modelo, vectores de inferencia (con su huella) e informe sin rutas. Nunca el
  material de entrenamiento. ia/modelos/ e ia/informe_dsee.json están en .gitignore.

Comprobado: se simula un apagón en CADA uno de los 25 pasos de una publicación (escrituras,
vaciados a disco, renombrado, activación y cada copia derivada, también en entrenado/) y, tras
recuperar, queda el conjunto viejo si el corte fue hasta el reemplazo de ACTIVO (paso 13) y el
nuevo desde ahí; nunca una mezcla. Versiones truncadas o alteradas no se activan; manifiestos
alterados se detectan; copias estropeadas se reparan; el par previo se archiva y se puede volver.
En rojo si recuperar no rehace las copias, no limpia lo preparado a medias, si ACTIVO se escribe
sin reemplazo atómico o si no se comparan los hashes. De punta a punta con el lanzador sobre
material sintético con índice: comparó con el modelo puesto, rechazó y no tocó nada.

Límite: la atomicidad depende del sistema de archivos (os.replace/rename en el mismo volumen).
El vaciado de la carpeta no es posible en Windows; allí la garantía es la del reemplazo NTFS.
No se probó un corte de luz real ni un disco USB extraído a media escritura.

## 2. Comprobaciones y cómo repetirlas

En cada repositorio, con sus herramientas de compilación .NET, Python con NumPy y FFmpeg:

    ./pruebas/validar-audio-mejoras.ps1 -Python "C:\ruta\python.exe"

Se puede indicar -OutputDirectory apuntando a una carpeta TEMPORAL de pruebas y
-Ffmpeg a su ejecutable. No usar release1/releaseHD como salida de este script.

El script:

1. Compila GPU y CPU con advertencias tratadas como errores; ejecuta las 29 baterías
   de validar-capas.ps1. Compilar CPU no significa modificar su renderizado.
2. Ejecuta once baterías adicionales: dsee, limite, entrenador, iaparidad, iaorigen, iadatos,
   eqauto, calidad, nivel, bajoauto y bajo.
3. Ejecuta las pruebas Python de los entrenadores y sus protecciones (test_calidad_dsee,
   test_entrenar_eq, test_contrato_dsee, test_origen_dsee, test_estereo_dsee,
   test_seleccion_dsee y test_publicacion_dsee).

Nota del 22 de septiembre (bloque A): este script NO se pudo ejecutar en el entorno donde se
hizo el bloque A (Linux, sin PowerShell, Windows ni FFmpeg). Sus piezas sí: todas las baterías
con Mono mediante pruebas/todo.sh y todas las pruebas Python, en las dos ramas. Queda pendiente
correrlo completo en Windows.
4. Genera WAV de ruido reproducible en temporal para probar cosecha activada/desactivada.
5. Comprueba decodificación MP3 CBR/VBR, WMA, AAC, WAV PCM y float32 y liberación de archivos.
6. Repite los nuevos controles y medición con afinidad de hasta dos procesadores lógicos,
   restaurándola al acabar. Esto NO simula la velocidad de un CPU antiguo.

Pruebas nuevas: audiosuave.cs (2.779.898 comprobaciones) y audioestereo.cs (358).
Cubren 22,05/44,1/48/96 kHz, 1/2/6 canales, fragmentación, objetivos rápidos, primer cuadro
de transición, final exacto, fase, archivos reales sintéticos y migración incremental.
eqpreparado.cs sigue comparando el DSP de configuración inicial con su referencia;
las transiciones deliberadamente nuevas se verifican en audiosuave.

También se compila Release con MSBuild fuera de las instalaciones.
Resultados automatizados de esta pasada: ambas ramas pasan. Los modelos binarios y sus
vectores del repositorio permanecen intactos. No se probó XP real, reproducción en la
rocola instalada, Apollo con los checkpoints del usuario ni escucha A/B o salida del DAC.

## 3. Pendientes concretos para continuar por tu cuenta

Orden propuesto: A, B/C, D/E, F/G. Cada bloque debe terminar con pruebas y cambios en ambas
ramas cuando sea común; publicar cada entrega comprobada y no mezclar todo en un único parche.

### A. IA: paridad del extractor y validación realmente independiente — prioridad alta

Dónde: SpectralTap.BuildIaFeatures/MakePair/RegenNeural en Form1.Sonido.cs,
IaAppendPairs en Form1.Ia.cs, entrenar_ia.py y calidad_dsee.py.

1. Añadir vectores de PARIDAD DEL EXTRACTOR: misma señal, ventana, bins y frecuencia en
   Python y C#, comparando las 28 entradas y 12 objetivos, no solo la salida de la red.
2. Hay una discrepancia a resolver explícitamente: _bandas_objetivo de Python limita a
   20 kHz; MakePair/regen C# usa un límite de 20,5 kHz con ajustes de bins/Nyquist.
   Las pruebas existentes de 24 vectores prueban inferencia, no detectan esta diferencia.
   Definir un contrato único y una versión de datos; no reinterpretar silenciosamente
   datos ya cosechados ni cambiar modelos instalados sin pruebas y migración.
3. Conservar RIA1 de reproducción. Para datos nuevos, añadir origen de canción y versión
   de extractor en RIAD v2 o un índice lateral robusto. RIAD v1 no trae esos identificadores;
   no se pueden reconstruir con fiabilidad solo por posición del ejemplo.
4. Separar por canción, y preferiblemente artista/álbum, ANTES de dividir en cuadros.
   Mantener un conjunto externo de prueba que no sirva para elegir checkpoints.
5. Comparar con referencia clásica y con modelo instalado sobre el mismo conjunto.
   Actualmente el guardián compara contra la curva fija, no contra el modelo instalado.
6. Revisar load_wavs: su mezcla mono también puede cancelar contenido en contrafase.
   Extraer canales separados conservando el mismo grupo de origen, sin duplicar grupos
   entre entrenamiento y prueba. No confundir esta tarea con la huella AutoEQ ya corregida.
7. Exportar informe de errores por banda, transitorios, clases de señal y paridad C#.
   Hacer A/B igualado de volumen y comprobar que las fuentes buenas conservan su carácter.
8. Unificar publicación de modelo y vectores con un manifiesto/hash o carpeta versionada
   y activación final; probar cortes/fallos en cada paso.

Aceptación: ningún origen compartido entre particiones, paridad de características con
tolerancia documentada, cero datos no finitos y superioridad verificada también al escuchar.
No requiere una red más grande ni añade coste a la reproducción.

Estado (22 de septiembre, cuatro entregas en las dos ramas): hechos 1, 2, 3, 4, 5, 6 y 8 (ver
1.7 a 1.10). Del 7 están el informe por banda y la paridad C#; faltan el desglose por
transitorios y clases de señal. Lo que queda y por qué:
- Entrenar de verdad con validación independiente: necesita material cosechado por la rocola
  nueva (con rocola_ia_datos.idx). Con el material actual el entrenador se niega a publicar.
- Superioridad audible: A/B a volumen igualado en tu equipo; ningún número de aquí la demuestra.
- Paridad en XP real: correr pruebas/iaparidad.cs allí. Riesgo medido: 1,06 % de las divisiones
  de la geometría caería en otro bin sin redondeo float32 (FPU x87).
- validar-audio-mejoras.ps1 completo en Windows.
- El artista queda en 0 en el índice: leerlo desde el hilo de fondo no es seguro hoy.

### B. Medida de sonoridad integrada y caché versionada

Dónde: MeasureFile/ReportAudioBlock, Form1.Datos.cs y planificador de biblioteca.
La implementación actual es energía K-ponderada; el modo automático analiza tramos.
No rotularla como LUFS integrado completo.

1. Crear analizador separado con bloques, solapes y puertas de sonoridad. Usar como
   referencia [ITU-R BS.1770-5](https://www.itu.int/rec/R-REC-BS.1770-5-202311-I)
   y sus requisitos; comprobar señales de conformidad antes de anunciar compatibilidad.
2. Mantener dos estados distintos: estimación rápida por tramos y medida integrada de
   canción completa. No promocionar la primera a la segunda solo por tener duración.
3. Añadir versión y validez a una caché nueva o extensión compatible; preservar medidas
   anteriores hasta terminar la sustitución. Excluir lecturas incompletas/canceladas.
4. Normalizar usando la medida disponible y límites de ganancia; las rampas ya implementadas
   pueden aplicar objetivos nuevos sin escalón.
5. Medir mono/estéreo, silencios, intros largas, jazz/clásica, compresión fuerte, canales
   multicanal y LFE según el estándar. No inferir disposición de altavoces del número de canales.
6. Ajustar también el mínimo de aprendizaje en vivo: EqMinFrames todavía es 882.000
   cuadros fijos; equivale a 20 s solo a 44,1 kHz. Versionar/pruebar cualquier cambio
   a un mínimo por duración, incluyendo lecturas parciales que no completan un cuadro.

Coste: análisis completo más lento en HDD; reproducir una canción ya medida solo lee caché.
Conservar pausas, cancelación y prioridad frente a carátulas/arranque, sin saturar 2C/2T.

### C. Protección de picos entre muestras

Dónde: LookaheadLimiter y salida posterior al último DSP/remuestreo.

1. Separar el detector de pico real del camino de señal. Prototipar FIR polifásico,
   empezando por 4× y comprobando su error según frecuencia de entrada.
2. Compensar retrasos del detector y anticipación; enlazar canales con la misma ganancia.
   No permitir que el pico escape mientras el detector aún se está llenando.
3. Cubrir vaciado al final, cambios de formato, seek, silencio, impulsos y archivos cortos.
4. Probar la senoide Fs/4 con fase pi/4: puede tener muestras menores que uno y picos
   reconstruidos mayores que uno. Comparar contra una reconstrucción independiente
   de mayor precisión, no contra el propio detector.
5. Fijar margen de salida después de medir errores, no asegurar protección bajando
   arbitrariamente el techo del limitador actual.
6. Medir CPU por bloque y máximos de latencia junto al resto de DSP, no solo promedio.

La referencia de medida de pico real es también BS.1770-5, citada arriba.
No afirmar true-peak por pasar las pruebas actuales de sample-peak. No forzar sobremuestreo
de toda la cadena en Modest. No activarlo globalmente antes de validar coste y escucha.

### D. Aliasing y transiciones de otros efectos

Dónde: excitador de LevelTap, DeepBassProvider, SbrProvider/SpectralTap y controles existentes.

1. Capturar barridos/multitonales a 44,1/48/96 kHz con intensidades típicas y extremas.
2. Medir armónicos deseados, bandas plegadas, intermodulación y diferencias de estéreo.
3. Solo si hay mejora justificable, sobremuestrear 2× la etapa no lineal que lo necesita,
   con filtrado antialias y regreso correcto; comparar también 4× como referencia offline.
4. Mantener una salida seca sincronizada para mezcla/bypass y drenar los filtros al acabar.
5. Extender rampas a otros controles que aún cambien abruptamente: Amount del grave
   sintético y activación/intensidad DSEE. No asumir que las rampas de LevelTap cubren todo.
6. Pruebas: estado inicial idéntico al solicitado, cambios rápidos, silencio, no modificar
   camino apagado, no crear asignaciones por muestra ni reiniciar filtros cada bloque.

El grave sintético ya filtra su banda: necesita medición propia, no copiar automáticamente
el tratamiento del excitador agudo.

### E. Remuestreo y Apollo — principalmente HD

Dónde: SalidaRemuestreada, apertura/salida y ia/upscale.

1. Medir el remuestreador DMO actual con impulsos, barridos, ruido, duración y cola para
   44,1↔48/96 kHz. Conservar el manejo MTA y cancelación existentes.
2. En Apollo comparar la preparación offline actual con SoXR, si el FFmpeg instalado
   lo incluye. FFmpeg documenta ambos motores y su precisión en
   [su manual de remuestreo](https://ffmpeg.org/ffmpeg-resampler.html).
3. Probar un candidato offline como aresample=resampler=soxr:precision=28 sin forzar
   esa opción en equipos que no lo admitan; detectar capacidad y conservar alternativa.
4. Mantener WAV float32 en caché y 44,1 kHz donde lo exige el modelo; no elevar todo a
   96/192 kHz suponiendo que eso restaura información que no existe.
5. Comparar resultado alineado, picos, canales, duración, final de audio y costuras de
   fragmentos. Mantener originales, límites de caché y mezcla actual.
6. Registrar versión del procesador en la clave de caché si cambia su salida, para no
   reutilizar archivos anteriores atribuyéndoles la calidad nueva.

No cambiar modelos Apollo por tamaño/reputación sin probar pesos, memoria, coste y
escucha con material del usuario. No introducir dependencias de HD en Modest/XP.

### F. Salida al dispositivo

Modest conserva PCM16 con dither TPDF; HD usa su salida actual.
No aplicar dither repetidamente dentro de la cadena flotante.

1. Evaluar soporte real de PCM24/float en WaveOut por dispositivo y Windows.
2. Mantener fallback fiable a PCM16 si el driver rechaza el formato.
3. Para HD, evaluar WASAPI exclusivo solo como una decisión de producto explícita:
   cambia el uso compartido del dispositivo; no garantiza por sí solo mejor sonido.
4. Probar cambios/desconexión de dispositivo, frecuencia, arranque, cierre y recuperación.
5. Validar con loopback y, si se promete salida física, captura del DAC; el software
   flotante no demuestra resolución efectiva del equipo.

Requiere hardware y XP real para cerrar compatibilidad; no cambiar NAudio a una versión
incompatible con .NET 3.5 para obtener un formato nuevo.

### G. Continuidad entre canciones y audio de vídeo

FadeProvider es fundido de salida; no ofrece por sí mismo reproducción gapless o crossfade real.
El audio WMP de vídeos sigue fuera de la cadena musical.

1. Diseñar dos lectores/etapas preparadas y un mezclador de transición de coste acotado.
2. Definir cuánto se solapa, preservar cola y créditos, y cuándo se considera terminada
   una canción. Fallos/cancelación no pueden consumir dos créditos ni reproducir duplicado.
3. Compensar latencia de DSP/remuestreo; gestionar encoder delay/padding cuando exista.
4. Probar álbumes continuos, cambio de formato, skip reiterado y pérdida de archivo.
5. Para vídeos, estudiar una cadena común con sincronía audiovisual y fallback. No
   intentar resolverlo elevando el volumen WMP por encima de su rango.

Este bloque es rediseño, no un ajuste rápido de calidad.

## 4. Criterios de entrega y despliegue

- Un cambio compartido debe existir y pasar pruebas en las DOS ramas.
- Registrar tiempo medio y peor bloque en 2C/2T reales y equipo potente. No limitar al
  equipo potente a presupuestos fijos de una máquina lenta.
- No lanzar entrenamiento, Apollo, barrido intensivo y precarga agresiva simultáneamente
  en el equipo modesto. Conservar coordinación/prioridades existentes.
- Escucha comparada a volumen igualado: percusión, sibilantes, voces, estéreo amplio,
  silencio, archivos dañados y canciones ya bien grabadas.
- Respaldar ejecutables y cachés antes de desplegar con la rocola cerrada. Copiar solo
  binarios/dependencias necesarios; no sobrescribir modelos/configuración/biblioteca.
- Publicar commits comprobados y verificar el SHA remoto de ambas ramas.
- No usar etiquetas como «sin pérdida», «master original recuperado», «LUFS integrado»
  o «true-peak» para prestaciones que las pruebas todavía no demuestran.
