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
| Sonoridad integrada BS.1770 de la canción entera, aparte de la estimación | Implementado y probado; ver 1.12 |
| Mínimos de aprendizaje por duración y lecturas que parten un cuadro | Implementado y probado; ver 1.12 |
| Limitador de pico real entre muestras (true-peak) | Implementado y probado, con un límite medido; ver 1.13 |
| Plegado (aliasing) del realce de agudos: saturación a 2× con el seco alineado | Implementado y probado; ver 1.14 |
| Plegado del grave sintético | Medido: no hace falta sobremuestrear; ver 1.14 |
| Transiciones en vivo del grave sintético y del DSEE | Implementado y probado; ver 1.15 |
| Remuestreo de Apollo a 44,1 kHz con SoXR, caché versionada y reaprovechamiento | Implementado y probado (HD); ver 1.16 |
| Continuidad sin pausas entre canciones (misma salida, sin cebado, cola y créditos intactos) | Implementado y probado; ver 1.17. Retardo y relleno propios del MP3: medidos, sin implementar |
| Remuestreo en reproducción (HD, DMO de Windows) | Medición preparada y validada; el DMO se mide en Windows; sin cambios; ver 1.18 |
| Salida al dispositivo: coma flotante en Vista o posterior, fallos del aparato sin gastar canciones, cambio de aparato | Implementado y probado con salidas simuladas; ver 1.19. WASAPI exclusivo: estudiado, decisión tuya |
| Audio de vídeo (reproductor de Windows Media) | Documentado y con batería que fija las reglas de hoy; procesamiento compartido: decisión tuya; ver 1.20 |
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
Esto no eliminaba el aliasing de la no linealidad; eso se resolvió después (etapa 3, ver 1.14).

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

Riesgo medido, no comprobado en XP: la FPU x87 de un Windows XP de 32 bits puede calcular con
más precisión que float32. Sobre las 500.721 divisiones hz/binHz de toda la geometría: si solo la
división se hace con más precisión (hz redondeado al pasarlo a Hz(), una llamada normal) no cambia
NINGUNA; si además el compilador mete Hz() en línea y deja sin redondear el producto que da hz,
cambiarían de bin 5.312 (1,06 %). En una misma máquina la cosecha y la reproducción usan el mismo
código, pero la paridad exacta con Python en XP solo se sabrá corriendo pruebas/iaparidad.cs allí
(con Python instalado o fijando ROCOLA_TEST_PYTHON).

### 1.8 De qué canción viene cada ejemplo, y particiones que no mezclan (entrega 2)

Archivos: Rocola/Form1.Ia.cs y Form1.Sonido.cs (índice al cosechar), ia/origen_dsee.py (nuevo),
ia/entrenar_ia.py, ia/entrenar_dsee.py, ia/preparar_entrenamiento.py (HD), pruebas/iaorigen.cs.

rocola_ia_datos.bin (RIAD v1) no cambia: los lectores y ejecutables anteriores siguen funcionando.
Al lado, la rocola escribe rocola_ia_datos.idx: una entrada de 64 bytes por canción cosechada con
primer registro, cuenta, versión del extractor, frecuencia, cuadro, canal, huellas FNV-1a de la
canción (nombre de archivo + duración), de la carpeta y del artista, CRC32 de esos registros y CRC32
de la propia entrada. Solo huellas: ni rutas ni nombres. El artista entra SOLO si viene en la
etiqueta del MP3 (ID3v2 o ID3v1, leída con Track.SoloArtista, que salta la carátula y no toca la
lista de la pantalla): nunca se adivina del nombre del archivo o de la carpeta. Sin etiqueta, o
con comodines como «Various Artists» o «Desconocido», queda en 0 («no se sabe»). Coste medido:
2,2 ms por canción cosechada con una carátula de 300 KB delante (primera llamada incluida).

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

### 1.11 Informe por clases de señal, transitorios y paridad de inferencia (entrega 6)

Archivos: ia/calidad_dsee.py, ia/entrenar_ia.py, ia/origen_dsee.py, ia/contrato_dsee.py,
ia/entrenar_con_mi_musica.bat (HD), pruebas/iaparidad.cs, ia/test_informe_dsee.py (nueva).

- Lo que calcularía la rocola: prediccion_como_la_rocola imita RedChica.Predict en float32 y en su
  mismo orden de suma. pruebas/iaparidad.cs carga un modelo aleatorio 28-32-32-12 en la red de la
  rocola y compara 400 salidas: idénticas bit a bit (Mono). Con otro orden de suma, 0 de 400.
- Guardia nueva: si sobre la partición de prueba la rocola no calcularía lo mismo que lo evaluado
  (diferencia > 1e-3, el mismo límite que dsee.cs, o alguna fila que desborda en float32), no se
  publica. Con una red 28-96-96-12 aleatoria la diferencia medida es 1,7e-6 (20.000 filas, 1 s).
- Errores por clase de señal, con las propias 28 entradas (sirve también para el material de la
  rocola): nivel, tonalidad y brillo por terciles del conjunto evaluado, y corte (<13, 13-16, ≥16
  kHz). Candidato, curva fija e instalado por clase, en informe_dsee.json.
- Avisos por clase: si en una clase con al menos 200 filas el candidato queda más de un 10 % peor
  que el instalado (o que la curva si no hay instalado), se avisa para escuchar con cuidado ese tipo
  de música. NO cambia la decisión: la política sigue siendo global y por banda. Si quieres que un
  aviso bloquee la publicación es una decisión tuya; está preparado para hacerlo.
- Transitorios: un cuadro es ataque si su energía es al menos 4 veces (+6 dB) la del que empieza
  medio cuadro antes; el primero de cada trozo no cuenta. Solo se sabe con la señal a mano (WAV o
  sintético); en el material de la rocola, que guarda cuadros sueltos, el informe dice por qué no.
- Comprobar el índice lee el material entero: ahora avisa cada GB para que no parezca colgado.
- entrenar_con_mi_musica.bat (HD) trataba cualquier código distinto de 0 como «falló»; el 2 (no
  supera la política) y el 3 (no verificable) ya se explican como lo que son.

Comprobado: test_informe_dsee (emulación float32 frente a float64 y desbordes; clases que suman el
total y no dependen del tamaño de lote; aviso en la clase que empeora aunque el global mejore;
transitorios en señal real y sintética; la guardia de paridad bloquea; avance al comprobar). En
rojo sin la guardia de paridad, con la emulación en float64, con avisos que no comparan contra el
instalado o contando el primer cuadro como ataque. Los ejemplos que produce build_dataset no
cambian ni un bit (comparado con la versión publicada).

### 1.12 Sonoridad integrada ITU-R BS.1770 de la canción entera (etapa 1 de audio)

Archivos: Rocola/Sonoridad.cs (nuevo), Form1.Sonido.cs (MeasureFile, NormGainFor, ReportAudioBlock;
en Modest ReportAudioBlock está en Form1.Video.cs), Form1.Datos.cs, Form1.Biblioteca.cs,
Form1.Portadas.cs, Form1.Ajustes.cs, Form1.Fondo.cs, Form1.Ia.cs (Form1.cs en Modest),
pruebas/sonoridad.cs y pruebas/ref_bs1770.py (nuevos).

Dos medidas, separadas y con su nombre:

- ESTIMACIÓN RÁPIDA (rocola_volumen.txt, sin cambios): tres tramos de 20 s, o la canción entera si
  dura menos de un minuto, con un filtro parecido a la ponderación K y sin puertas; y lo aprendido
  oyendo. Su cálculo no cambió: la batería lo comprueba bit a bit. Medido: su filtro se aparta del de
  la norma −0,05 dB en graves y agudos y hasta −0,49 dB cerca de 1,5 kHz.
- MEDIDA INTEGRADA (rocola_sonoridad.txt, nueva): la canción entera como la define BS.1770-4/-5:
  ponderación K exacta en doble precisión, bloques de 400 ms con 75 % de solape (subbloques de 100 ms
  con límites en floor(k·fs/10), así a 11,025 kHz cada bloque mide 400 ms justos), puerta absoluta
  −70 LKFS y relativa −10 LU. A 48 kHz los coeficientes son los de la tabla de la norma (diferencia
  8,9e-16); a otras frecuencias salen del mismo prototipo con transformación bilineal predistorsionada.

Canales: mono y estéreo según su formato (1 canal es mono y 2 son izquierdo y derecho en MP3 y en
WAV sin máscara). Con 3 o más, SOLO si el WAV trae máscara de altavoces (WAVE_FORMAT_EXTENSIBLE): LFE
fuera, laterales a 1,41, traseros a 1,41 si no hay laterales (5.1 clásico) y a 1 si los hay (7.1),
resto a 1, según la regla de posición de BS.1770 (60°–120° de acimut y menos de 30° de altura).
Sin máscara, o con una máscara que no cuadra, no hay integrada y se queda la estimación.

Solo se publica lo que se midió ENTERO: nada cancelado, ni lecturas que se quedan cortas o que traen
de más respecto de lo que el archivo dice durar (margen: 100 ms o una milésima), ni tramos haciéndose
pasar por canción entera. Menos de 400 ms: no hay medida. Todo por debajo de −70 LKFS: «muda».

La tabla nueva lleva cabecera de formato («ROCOLA-SONORIDAD v1») y la versión del algoritmo en cada
registro. Una tabla con otra cabecera, o que no se pudo leer, no se usa NI se pisa. Los registros de
un algoritmo posterior se guardan intactos. Si falta el archivo bueno se lee su copia .bak. Entra en
el respaldo diario. La estimación vieja nunca se borra: sigue mandando mientras no haya integrada.

Nivelado: NormGainFor usa la integrada si existe, en la misma escala de siempre (la raíz de la
potencia K media por canal: 10^((L+0,691)/10) dividido entre la suma de pesos), y con los MISMOS
topes (el de la etapa, +24 dB absoluto, nunca baja). Un tema mono y el mismo tema en estéreo reciben
la misma ganancia. Una medida nueva NO cambia el volumen de la canción que ya suena: se aplica la
próxima vez que suene, desde su primera muestra y sin fundido de entrada. Lo que se toca en vivo en
Ctrl+F3 sigue pasando por las rampas existentes. La etapa que se muestra sale de la misma medida.

Cuándo se mide:
- De paso, cuando el análisis ya lee la canción entera (canciones de menos de un minuto, repaso a
  fondo, y el calentador de la siguiente con esas canciones cortas).
- El resto, en una SEGUNDA TANDA del repaso de la biblioteca, después de todas las estimaciones y en
  el mismo hilo: prioridad mínima, un solo carril en el equipo de dos núcleos, espera 8 s sin que nadie
  toque la rocola y sin vídeo, cede el disco a las carátulas, pide turno para no medir lo mismo que
  otro, y con música sonando descansa al menos lo que trabajó (como mucho la mitad de un núcleo). Va
  en el orden de siempre: lo pedido, lo más sonado, lo más reciente. Los vídeos se quedan con la
  estimación (su sonido lo pone Windows).
- Ctrl+F3, pestaña de biblioteca: «Primera medida lista; ahora mide cada cancion entera: faltan N».
  Es solo texto de estado; no hay interruptor nuevo.
- Bitácora (rocola_diagnostico.txt, si está encendida): cada 50 canciones, cuánto se aparta en
  promedio la estimación de la integrada. Es el dato real de tu biblioteca para saber cuánto cambia.

Mínimos por duración: la huella tonal oída pide 20 s de música a cualquier frecuencia (antes 882.000
cuadros: 40 s a 22,05 kHz y 9,2 s a 96 kHz) y la estimación oída 9,07 s (antes 800.000 muestras: 18 s
en mono y 4,2 s en estéreo a 96 kHz).

Lecturas que parten un cuadro: la sonoridad oída recuerda el canal que toca entre bloques y la huella
guarda el cuadro partido para completarlo; el análisis por tramos lee siempre cuadros enteros. Antes
ese trozo se perdía y todo el bloque siguiente se leía corrido un canal (el izquierdo por el filtro del
derecho). Con cuadros enteros, la cuenta es la misma de antes bit a bit.

Comprobado (pruebas automáticas con Mono en Linux; no es XP real ni escucha):
- EBU Tech 3341, casos 1 a 6, a 22,05/32/44,1/48/88,2/96 kHz: todos dentro de ±0,1 LU; el peor, 0,051
  LU (caso 5 a 22,05 kHz). A 48 kHz: +0,007 +0,007 −0,014 −0,014 +0,021 −0,016.
- Contra la referencia independiente en Python (tabla de 48 kHz aplicada por FFT, bloques por suma
  acumulada, puertas comparadas en LKFS) con señales de programa: a 48 kHz iguales a 4 decimales
  (estéreo con tramos, silencio digital y bajo la puerta; 5.1 con LFE); a 44,1/32/96/22,05 kHz se
  apartan 0,004/0,024/0,022/0,059 LU, que es la distinta forma de llevar el filtro a esa frecuencia.
- Leyendo en trozos de 1, 3, 883, 8192, 65537 muestras y al azar: resultado idéntico bit a bit, en
  estéreo y con cinco canales. Mono, estéreo, 5.1, 7.1, LFE, máscaras erróneas, cabeceras WAV con
  trozos intercalados, RF64 y cabeceras cortadas.
- Tabla: ida y vuelta, registros rotos, algoritmo posterior intacto, cabecera ajena sin tocar,
  disco que falla a mitad sin publicar nada a medias, .bak, y la octava tabla del arranque.
- Solo entera: cortado, cancelado, seis canales sin máscara, tramos de 63,16 s (que suman lo mismo
  que el archivo) y lectura de más.
- Mínimos a 22,05/44,1/48/96 kHz en mono y estéreo; lecturas partidas en vivo.
- En rojo al quitar cada una: puerta relativa, puerta absoluta, LFE fuera, fase entre lecturas
  (medidor y en vivo), cuadro partido de la huella, lectura completa, división por pesos, preferencia
  de la integrada, mínimo en segundos, no pisar tabla ajena, conservar registros ajenos, un solo tramo.
- Coste en esta máquina (Mono): el medidor gasta 5,8 ns por muestra en estéreo, 0,5 ms por segundo de
  música (0,12 s en una canción de 4 minutos) y 19 KB por canción. En un WAV de 4 minutos, la medida
  entera tardó 254 ms y la estimación por tramos 481 ms. En MP3 manda el descompresor, que aquí no se
  puede medir (Linux no tiene el de Windows): es la cifra que falta medir en el equipo del bar.
- En el hilo del sonido, ReportAudioBlock con la medición en vivo encendida (solo en canciones aún sin
  analizar) cuesta lo mismo que antes: 7,4 ms por segundo de música en las dos versiones; el peor
  bloque (93 ms de audio) entre 0,7 y 1,7 ms en ambas, que es ruido de la máquina.
- Tandas completas (pruebas/todo.sh): HD 164 baterías bien y Modest 152, las dos con la batería nueva;
  en las dos sigue en rojo solo `disco`, como antes de esta etapa (pendiente de tu decisión).

Limitaciones y pendientes:
- No pude abrir aquí el texto oficial de BS.1770-5 ni EBU Tech 3341 (el acceso a itu.int y a
  tech.ebu.ch está bloqueado en este entorno). Lo implementado sigue la norma tal como la conozco; la
  regla de pesos por posición se confirmó por búsqueda. Conviene cotejar con el PDF oficial. Los casos 7
  y 8 de Tech 3341 son grabaciones reales que tampoco se pudieron descargar.
- Un WAV multicanal con máscara lo abre NAudio a través del conversor de Windows: aquí se probó la
  lectura de la máscara y el medidor con esos pesos, no el archivo de punta a punta.
- Falta: XP real, CPU antigua real, MP3 reales, cuánto tarda la tanda en tu biblioteca y escucha A/B a
  volumen igualado (en canciones con intros largas o silencios la integrada sube menos que antes).
- Sin sonoridad momentánea, de corto plazo ni rango (EBU Tech 3342): la rocola no los usa.

### 1.13 Limitador de pico real entre muestras (etapa 2 de audio)

Archivos: Rocola/PicoReal.cs (nuevo), Form1.Sonido.cs (dónde va en la cadena), pruebas/picoreal.cs y
pruebas/ref_picoreal.py (nuevos); limite.cs, atoron.cs y arranque.cs miden ahora el limitador que va en
la cadena.

El limitador de antes (LookaheadLimiter) solo miraba las muestras. Pero el convertidor del equipo
reconstruye la onda que pasa por ellas, y esa onda sube ENTRE muestras: un tono a fs/4 desfasado 45°
tiene las muestras en 0,707 y la onda en 1,0. Medido con la referencia, el limitador viejo dejaba salir
la onda hasta +1,94 dBTP con señales de prueba y hasta +0,99 dBTP con música tipo MP3 pasada por la
cadena real (nivelado, realce y grave sintético).

El nuevo (LimitadorPicoReal) es el mismo limitador con pre-escucha (misma ganancia para todos los
canales, ataque que llega antes que el pico, soltar de 120 ms, techo 0,98), pero el pico que manda es
el de la onda reconstruida:
- Detector 4× con un filtro de interpolación de 24 coeficientes por fase (Kaiser β=6) y una parábola
  donde la rejilla tiene un máximo. Solo DETECTA sobremuestreado: la señal no se sobremuestrea; la
  ganancia se aplica a la frecuencia de siempre. Por simetría del filtro, 36 multiplicaciones por muestra
  y canal; si ninguna de las 24 muestras en juego pasa de 0,98/2,30, ni se calcula (no puede pasar).
- Espera 12 muestras (0,27 ms a 44,1 kHz) a que el detector vea el pico entero, APARTE de los 3 ms de
  pre-escucha; al terminar la canción vacía las dos cosas. Retardo total 144 cuadros a 44,1 kHz, ya
  incluido en la cuenta del fundido final.
- Va al final: en Modest es lo último antes del fundido (que solo baja) y del paso a 16 bits con su
  dither de un bit. En HD además va DESPUÉS del remuestreador de la salida moderna, que es lo último que
  puede crear picos. Lo que haga Windows detrás (volumen del sistema, mejoras de sonido) no depende de la
  rocola.
- Lo que no pasa del techo sale intacto, bit a bit (solo retrasado).

Cómo se eligió (medido, no supuesto; prototipos en Python contra una reconstrucción 64× exacta):
- Con tonos largos cualquier diseño parece casi perfecto, porque alguna fase del filtro acierta; en un
  golpe corto el pico puede caer en la fase que más cae. Por eso se midió con golpes, impulsos en posición
  fraccionaria, ruido, cuadradas, sierras y señal recortada.
- Error del detector en señales limitadas a 20 kHz (a 44,1): con 12 coeficientes por fase, hasta −0,82
  dB; con 16, −0,32 dB; con 24, −0,10 dB (el elegido); más coeficientes apenas mejoran. Sin la parábola,
  el margen medido baja de 0,15 a 0,016 dB.
- Con material tipo MP3 (cortado a 16 o 19 kHz), incluso con el realce, cualquier diseño de 12 o más
  coeficientes se equivoca menos de 0,03 dB; el de 24, menos de 0,013 dB.
- Lo que el detector NO ve bien es lo que hay entre 0,4535·fs y Nyquist (20–22 kHz a 44,1): ahí su filtro
  se corta. Se queda corto hasta 0,18 dB con ruido blanco sin filtrar y 0,25 dB con un master recortado
  sin comprimir; con 64 coeficientes seguiría en 0,14 dB. Es un límite de cualquier detector 4×.

Margen de salida MEDIDO (referencia independiente pruebas/ref_picoreal.py: interpolación de banda
limitada exacta a 32×, se equivoca menos de 0,0003 dB en tonos de 1 a 20 kHz):
- Señales limitadas a 20 kHz, a 22,05/44,1/48/96 kHz, mono, estéreo y 6 canales: la onda sale como mucho
  a −0,152 dBTP. Techo −0,175 dB: quedan 0,15 dB hasta 0 dBTP.
- Música tipo MP3 por la cadena real (nivelado +4 dB, realce 60 %, grave 80 %): −0,145 dBTP.
- LÍMITE: con contenido fuerte hasta Nyquist (saltos en seco, ruido blanco sin filtrar) la onda puede
  pasar de 0 dBTP: +0,018 dBTP, y +0,217 dBTP en la cadena con platillos de ruido blanco hasta 22 kHz
  (extremo que un MP3 o un AAC no tienen; un WAV/FLAC de un CD muy recortado sí podría acercarse). El
  viejo, con esas mismas señales, +0,89 dBTP. No se bajó el techo para taparlo.
  Alternativas concretas si eso importa en tu equipo: bajar el techo lo medido (0,25 dB: de 0,98 a
  0,952), o un detector de 64 coeficientes por fase (se queda en 0,14 dB, cuesta el triple).

Comprobado: pruebas/picoreal.cs (35 comprobaciones): filtro (fase 0 = la muestra, simetrías, ganancia
máxima), casos al estilo de EBU Tech 3341 15–19 con la verdad analítica (fs/4 a 0° y 45°, fs/6 a 60°,
fs/8 a 67,5°, fs/4 a +3 dBTP: error ≤ 0,006 dB), detector por bloques igual bit a bit que muestra a
muestra, contra la referencia (se queda corto ≤ 0,10 dB; lee de más ≤ 0,2 dB, que solo limita antes);
salida contra la referencia a cuatro frecuencias y 1/2/6 canales; intacto bajo el techo; silencio,
fuente vacía, archivos de 1, 7, 100 y 200 cuadros, final a todo volumen, saltos bruscos (lo que deja un
salto de posición), origen que parte cuadros y lecturas chicas (bit a bit iguales); la misma ganancia en
los dos canales (diferencia 7e-8); tonos sostenidos sin distorsión y ganancia asentada que no se mueve
(sin bombeo); cableado en las dos salidas. En rojo al quitar: la rejilla fina, la parábola, la espera del
detector, el umbral de salto, la corrección final. limite.cs exige lo mismo a los dos limitadores: el
nuevo no aplana ni una muestra en los golpes secos (el viejo, 12).
Coste en esta máquina (Mono, que hace las cuentas en float convirtiéndolas a double y exagera): sin
limitar, lo mismo que el viejo (39–44 frente a 38–42 ns por muestra); limitando, 111–119 frente a 36
(≈ 10 ms por segundo de música estéreo). En la cadena completa por bloque de 50 ms (atoron.cs): de 0,83
a 1,01–1,15 ms; con la estimación de «el bar es 4 veces más lento», 4,0–4,6 ms por bloque, lejos de los
16,7 ms de un cuadro de pantalla. Por eso Modest usa el mismo detector. No medido en XP real ni en el
equipo del bar.
LookaheadLimiter ya no va en la cadena; se conserva como referencia de las pruebas.

### 1.14 Realce de agudos sin plegado; el grave sintético, medido aparte (etapa 3 de audio)

Archivos: Rocola/Realce.cs (nuevo), LevelTap en Form1.Sonido.cs (cómo entra en la lectura y en la cuenta
del fundido), pruebas/realce.cs y pruebas/ref_realce.py (nuevos); eqpreparado.cs rehace aparte el
realce nuevo para seguir comparando bit a bit.

El problema, medido: el realce satura la banda alta (tanh) para crear armónicos, y a 44,1 kHz los que
pasan de 22 kHz no desaparecen: se pliegan y vuelven como tonos que no son armónicos de nada (un
platillo en 15 kHz da su tercer armónico en 45 kHz, que reaparece en 0,9 kHz). Contra una referencia
independiente (ref_realce.py: la misma saturación a 16× con interpolación exacta, lo que pasa de
Nyquist se tira), con el realce de fábrica:
- tonos de 11 a 20 kHz a −6 dB: productos plegados 47 a 50 dB por debajo del tono (38 a 41 con el
  realce al máximo);
- música tipo MP3 que llega a 19 kHz: el peor tercio de octava por debajo de 16 kHz quedaba a solo
  27 dB de lo que suena en esa banda (18 al máximo); la que llega a 16 kHz, a 57 dB.
Se distinguió lo deseado de lo plegado: la referencia se queda con los armónicos e intermodulación
que caben (lo que se quiere), y el plegado es lo que la rocola saca de más. La intermodulación de
verdad (por ejemplo 1 kHz entre 19 y 20 kHz) está en la referencia, no se cuenta como error.

El grave sintético, medido aparte igual (tonos de 30 a 100 Hz, dos tonos, bombo, barrido y música):
su plegado queda entre 102 y 129 dB por debajo de los armónicos que genera. Trabaja por debajo de
320 Hz; no se sobremuestrea.

Lo que se hizo: la saturación se calcula al doble de frecuencia, pero solo su parte NO lineal
(tanh(2,5h) = 2,5h + resto): la parte lineal no crea nada y no se filtra (filtrarla le quitaría agudos
al realce). El resto se sube a 2×, se satura allí y se baja con un filtro de media banda de 63
coeficientes (Kaiser 7; medido: se aparta 0,002 dB hasta 0,40·fs y rechaza 74,7 dB desde 0,60·fs). Retrasa 31
muestras (0,70 ms a 44,1 kHz; 0,65 a 48; 0,32 a 96) y el seco y la parte lineal se retrasan exactamente
lo mismo: todo sale alineado. Se hace por canal y por trozos de 512 cuadros; la memoria se reserva al
crear la etapa, nunca al leer.

Resultado medido (pruebas/realce.cs contra ref_realce.py, 44,1 y 48 kHz):
- tonos de 11 a 17 kHz y barrido: de −48 a entre −74 y −126 dB con el realce de fábrica (−65 a −117
  al máximo); pegados a Nyquist (19, 20 y 19+20 kHz): −68 a −75 dB (−58 a −66 al máximo);
- música tipo MP3 que llega a 19 kHz: peor tercio de octava −55/−59 dB (−46/−50 al máximo); la que
  llega a 16 kHz, −89/−95 dB. En nivel absoluto, la banda con más plegado pasa de −79 a −103 dBFS con
  el realce de fábrica (de −70 a −94 al máximo);
- los armónicos que sí se quieren siguen ahí: con tonos fuertes de 1 a 3 kHz, cuyos armónicos caben
  sin plegarse, la salida es la del realce de antes (retrasada) dentro de 3e-7, con un efecto de 0,1.

Por qué 2× y no 4× (comparado, no supuesto): lo que queda a 2× viene de dos sitios. La imagen de lo
que hay muy cerca de Nyquist (19–22 kHz), que un 4× no arregla porque depende del primer filtro; y los
productos de quinto orden de lo fuerte por encima de 13 kHz. Ahí un 4× bien hecho (63 + 15
coeficientes) sí gana: 11 a 13 dB en la peor banda de la música que llega a 19 kHz y hasta −100 dB en
tonos puros fuertes; pero esa banda ya está a −103 dBFS, cuesta 1,6 a 1,75 veces más (81–88 frente a
48–50 ns por muestra y canal, medido) y retrasa 35 muestras en vez de 31. Un 4× con el segundo filtro
corto (7 u 11 coeficientes) resultó PEOR que el 2× (−39 a −57 dB en música). Filtros de 2× más largos
(95 coeficientes) no mejoran la música; β = 7 fue mejor que 8, 9 o 10 con la misma longitud. Modest usa
el mismo 2×.

Cuándo se usa y qué pasa con los controles:
- La canción que EMPIEZA con el realce encendido (lo normal: viene de fábrica) usa el 2× toda la
  canción. Si se apaga desde Ctrl+F3, la rampa de 20 ms lo lleva a cero y después sale el seco intacto,
  bit a bit, retrasado 31 cuadros (cambiar el retraso a media canción sería un salto en el tiempo).
- La canción que empieza con el realce en cero va por el camino directo de siempre: intacta, sin
  retraso ni coste. Si alguien lo sube a media canción, esa canción usa el realce de antes (sin
  sobremuestrear) hasta que termina, para no saltar; la siguiente ya empieza con el 2×.
- Al terminar la canción se entregan los 31 cuadros retenidos (su cola completa) y el fundido final
  los tiene en la cuenta. No hay fundido nuevo al principio: los primeros 31 cuadros son el retraso.

Comprobado: pruebas/realce.cs (33 comprobaciones): filtro medido; paso directo bit a bit y sin retraso;
apagado a media canción = seco intacto de hace 31 cuadros, bit a bit; alineado (con señal chica es el
realce de antes retrasado, dentro de 1e-10; con tonos de 1 a 3 kHz fuertes, dentro de 3e-7 con un
efecto de 0,1); plegado contra la referencia (arriba); seis canales, cada uno bit a bit como si fuera
solo, y el canal en silencio sigue en silencio exacto; 119 combinaciones de tamaños de lectura
(incluidas de una muestra y partiendo cuadros) iguales bit a bit con la misma cola; archivos de 1 a
4000 cuadros con la cola entera, fuente vacía, final a todo volumen, silencio, NaN e infinitos; subirlo
a media canción no salta; el grave medido aparte; el cableado (el retraso entra en el fundido; leer no
reserva memoria). En rojo al romper, una por una: el seco un cuadro corrido, la parte lineal corrida, la
muestra de la rejilla corrida, quitar el 2×, no vaciar la cola, decidir siempre 2×, no contar el retraso
en el fundido, cruzar canales, normalizar mal el filtro, perder el historial entre trozos y equivocar el
canal con que empieza un trozo. eqpreparado.cs rehace aparte el 2× (historial entero, sin anillos ni
trozos) y compara bit a bit a 22,05/44,1/48/96 kHz con 1, 2 y 6 canales.

Coste (Mono en esta máquina, que exagera las cuentas en float): LevelTap entero pasa de 29 ns por
muestra con el realce de antes a 74 ns con el 2× (sin realce, 12): 6,5 ms por segundo de música
estéreo. Peor bloque de 50 ms de LevelTap: 0,88 ms. En la cadena completa (atoron.cs, alternando
versiones), un bloque de 50 ms —el que usa la salida clásica— pasa de 1,01 a 1,20 ms; con la
estimación de «el bar es 4 veces más lento», de 4,0 a 4,8 ms, por debajo de los 16,7 ms de un cuadro.
atoron marca «se nota» en su fila hipotética de bloques de 200 ms (19,2 ms estimados; antes 16,1),
que la rocola no usa. No medido en XP real ni en el equipo del bar.

Límites y pendientes: la escucha A/B a volumen igualado (el realce suena igual por diseño, sin los
tonos plegados; que se oiga la diferencia depende del material y del equipo) y medir el coste en XP
real y en el equipo del bar. Si allí hiciera falta, la alternativa concreta es acortar el filtro a 47
coeficientes (un cuarto menos de multiplicaciones; medido en los prototipos: la música que llega a
19 kHz queda igual con el realce de fábrica y hasta 7 dB peor al máximo, y un tono de 20 kHz, 8 dB
peor), no quitarlo.

### 1.15 Transiciones en vivo del grave sintético y del DSEE (etapa 4 de audio)

Archivos: DeepBassProvider y SpectralTap en Form1.Sonido.cs; pruebas/transiciones.cs (nueva);
ref_realce.py y realce.cs saben que el grave se suma una muestra después (ver abajo).

Auditoría (lo que había): las rampas de LevelTap (volumen, realce y EQ) no cubrían estos dos:
- Grave sintético: la mezcla nueva entraba de golpe en el siguiente bloque (un escalón cada vez que se
  tocaba la perilla). Al ponerlo en cero se saltaba el cálculo y sus filtros se quedaban congelados: al
  volver a encenderlo arrancaba con el estado de hacía minutos. Además suponía cuadros enteros: una
  lectura que partiera un cuadro dejaba una muestra sin grave (un chasquido).
- DSEE: al alternarlo se tiraba su colchón. Al APAGARLO se perdían de golpe los 23 a 93 ms de música
  que ya había leído (un salto hacia delante, con chasquido). Al ENCENDERLO la banda recreada entraba
  sin fundido, y la primera ventana (mitad ceros) medía mal el corte: con el salto de cero a música
  en su centro lo daba por «completo» (20 kHz), la reconstrucción no empezaba hasta que esa medida falsa
  se diluía (cientos de ms) y entonces aparecía de golpe. Y si una lectura traía un cuadro partido, la
  muestra que sobraba se tiraba y los canales quedaban cruzados el resto de la canción.
- El DSEE en vivo no tiene perilla de intensidad: su intensidad efectiva cambia al encenderlo o
  apagarlo; el realce, que depende de si el DSEE está activo, ya iba en rampa (LevelTap).

Lo que se hizo:
- Grave: la mezcla va en rampa de 20 ms hasta el valor pedido exacto. Al empezar la canción, la pedida
  desde la primera muestra (sin fundido que debilite el golpe). Si la canción empieza en cero, ni se
  calcula (pase directo exacto, 0,2 ns por muestra). Una vez usado, sus filtros siguen la música aunque
  esté en cero (sin sumar nada: sale intacto bit a bit), así que al volver no trae estado viejo. El grave
  de un cuadro se suma al cuadro siguiente (una muestra, 23 µs: a 320 Hz son 2,6 grados): así un cuadro
  que llega partido entre dos lecturas se completa sin tocar lo ya entregado, y da igual cómo lleguen
  las lecturas. Los redondeos a float son explícitos, para que la cuenta sea la misma también en el FPU
  x87 de XP. Un valor roto (NaN o infinito) se ignora.
- DSEE: la banda recreada entra y sale con un fundido por ventana (~100 ms con cuadros de 2048; con los
  de 4096 del nivel HD, al menos 4 ventanas: 171–186 ms). Al apagarlo, el STFT sigue hasta que el fundido
  llega a cero; entonces entrega lo que ya había leído, tal cual, y sigue en pase directo: no se pierde
  ni se repite ninguna muestra. Al encenderlo arranca justo donde iba el pase directo. La ventana de
  arranque ya no mide el corte ni gasta un paso del fundido. Un cuadro partido espera a completarse con
  la lectura siguiente. Al empezar la canción no hay fundido.

Comprobado: pruebas/transiciones.cs (70 comprobaciones en HD; 46 en Modest, que solo tiene cuadros de 2048):
- Grave: empieza en cero = intacto y sin calcular; empieza encendido = mezcla pedida desde la primera
  muestra; la mezcla, medida desde fuera frente a una corrida fija (en un canal en silencio, donde la
  salida es exactamente lo agregado), cambia cuadro a cuadro sin pasar de la pendiente de 20 ms
  (7,7e-4 por cuadro) y llega EXACTA al valor pedido; apagado sale bit a bit intacto; al volver, lo
  agregado es el grave de verdad por su mezcla (sin estado viejo); cientos de cambios, NaN y lecturas
  partidas sin nada roto y acabando exacto en el último valor; un NaN o un infinito solos no mueven la
  mezcla; 47 combinaciones de lecturas (1/2/6 canales, de una muestra, partiendo cuadros) bit a bit
  iguales; el mismo grave en los seis canales.
- DSEE, con cuadros de 2048 y 4096, a 44,1 y 48 kHz: empieza encendido sin fundido; apagado a media
  canción no pierde ni una muestra (por debajo de 8 kHz la salida es la entrada: −138 a −141 dB, puro
  redondeo) y, acabado el fundido, sale bit a bit la entrada y bit a bit lo mismo que apagándolo de
  golpe; encendido a media canción: el corte se mide con la primera ventana de música (15,8 kHz con
  música que corta en 16; antes, la ventana de arranque lo falseaba); antes intacta, después nada
  repetido ni corrido, fundido paso a paso
  y en los primeros 40 ms recrea el 6–20 % de lo que recrearía encendido de golpe; acabado el fundido,
  bit a bit lo mismo; alternado cada 5–65 ms durante 3 s con lecturas que parten cuadros: nada perdido,
  repetido ni cruzado; la canción puede acabar a media transición; con el encendido fijo, cualquier
  tamaño de lectura da lo mismo bit a bit. También en mono y con 3 canales (el tercero va solo, fuera de
  las parejas de la FFT), mirando cada canal. Ni leer ni procesar reservan memoria. Y iaparidad sigue
  viendo las mismas bandas en RegenNeural: el analizador de la cosecha, que no lee, recrea sin fundido.
En rojo al romper, una por una: el grave sin rampa, con sus filtros congelados al apagarlo, con fundido al
empezar la canción, dejando que un NaN mueva la mezcla y perdiendo la suma de un cuadro partido; el DSEE sin volver al pase directo, tirando lo
ya leído al apagarlo, con el fundido de golpe, tirando el resto de un cuadro partido y midiendo el corte
en la ventana de arranque. Arrancar el STFT a media trama resultó inofensivo (trata igual a todos los
canales y devuelve cada muestra a su sitio): no se añadió ninguna guarda para eso.

Coste (Mono): grave apagado desde el principio 0,2 ns por muestra; encendido 14,7 ns (1,3 ms por segundo
de música estéreo; antes de esta etapa ~16); apagado a media canción, lo mismo que encendido (sigue la
música). Una primera versión muestra a muestra costaba 33 ns: en Mono, hacer la división de la suma
DESPUÉS de sumar el grave duplicaba el coste; con el orden cambiado (misma cuenta) quedó en 14,7. El
DSEE cuesta lo mismo que antes mientras recrea; durante el fundido de salida sigue su STFT (100–186 ms) y
después vuelve al pase directo. Cadena completa por bloque de 50 ms (atoron.cs, alternando versiones):
1,23 ms, frente a 1,24–1,25 con la etapa 3: sin coste añadido.

Limitaciones: como antes, en el primer instante de una canción el DSEE necesita una ventana de música
para medir el corte (23–46 ms), y su interferencia entre ventanas hace que un tono puro recreado varíe
de nivel entre ventanas (no es de esta entrega). Escucha A/B pendiente.

### 1.16 Remuestreo de Apollo a 44,1 kHz con SoXR (etapa 5 de audio, solo HD)

Archivos: ia/upscale/util_audio.py (elección y argumentos comunes), ia/tiempo_real/daemon_ia.py (motor
en vivo), ia/upscale/05_procesar_todo.py (por lotes), escanear_calidad.py y 00_verificar.py;
ia/upscale/test_remuestreo.py (nueva). Modest no tiene Apollo: no cambia nada allí.

Apollo solo trabaja a 44,1 kHz, así que el motor decodifica cada canción con FFmpeg a 44,1 kHz estéreo
float32 (sin cuantizar). Una canción que ya viene a 44,1 kHz no se remuestrea: sale igual bit a bit con
cualquier opción (comprobado con FFmpeg 6.1 y 7.0 en WAV float, WAV de 16 bits, mono, 5.1 y MP3). Las
demás pasaban por el remuestreador por defecto de FFmpeg (swr). Medido contra la verdad analítica
(tonos puros, ajuste por mínimos cuadrados y espectro del resto):

| Conversión | Qué se mira | swr por defecto (antes) | SoXR precisión 28 (ahora) |
|---|---|---|---|
| 48 → 44,1 kHz | tono de 19 / 20 kHz | −0,22 / −1,28 dB | 0,00 / 0,00 dB |
| 48 → 44,1 kHz | lo que había en 22,5 / 23 kHz vuelve plegado a | −14 / −20 dB | −161 / −160 dB |
| 96 → 44,1 kHz | 24 kHz plegado | −36 dB | −193 dB |
| 22,05 → 44,1 kHz | imagen de un tono de 10 kHz (en 12,05 kHz) | −25 dB | −167 dB |
| 32 → 44,1 kHz | imagen de un tono de 15 kHz (en 17 kHz) | −17 dB | −162 dB |
| 88,2 → 44,1 kHz | 20 kHz | −1,29 dB | 0,00 dB |

Por qué importa para Apollo: las imágenes de la subida quedan justo encima del corte original, y el
detector del corte (el que decide si una canción ya es buena y cuánta IA lleva) las toma por agudos.
Con música de ruido rosa: a 32 kHz con contenido hasta 15 kHz medía 17,4 kHz (mezcla 1,00 en vez de
0,82); a 24 kHz hasta 11,5 kHz, 13,2 kHz (0,82 en vez de 0,70); a 22,05 kHz hasta 10,5 kHz, 12 kHz.
Con SoXR mide 15,3 / 11,7 / 10,8 kHz. A 48 kHz no cambia la medida (16,2 y 19,8 kHz con los dos).

Lo que se hizo:
- Se usa SoXR de alta precisión (`aresample=resampler=soxr:precision=28`) si el FFmpeg instalado lo
  ejecuta; si no, un swr reforzado (`filter_size=256:cutoff=0.92:kaiser_beta=12`: plegados −119 a
  −141 dB, −0,42 dB a 20 kHz; solo se le escapa un tono pegado al corte original, a menos de 30 Hz);
  si tampoco, el de siempre. No se da por hecho que un FFmpeg traiga
  SoXR: se PRUEBA convirtiendo 0,1 s de dos tonos a 48 kHz y mirando formato, largo y nivel. Se recuerda
  por ejecutable (ruta, tamaño y fecha); otro FFmpeg en la misma ruta se vuelve a probar, también con el
  motor en marcha.
- Alineación, duración y final, medidos con los tres motores y a 48/96/32/22,05 kHz con largos que no
  cuadran: un impulso sale en su sitio (desvío 0,000 muestras), la duración a una muestra de lo exacto
  (redondeo) y una ráfaga en los últimos 8 ms sale entera (energía 1,0000). Sin cruce de canales.
- Picos: remuestrear convierte en muestras los picos que la onda ya tenía entre muestras. Una
  cuadrada a 0 dBFS sale a +1,96 dB con el de siempre y +2,15 dB con SoXR; música recortada a 0,99,
  igual con los tres (±0,1 dB). La caché es WAV float: no se recorta, y el limitador de pico real de la
  rocola lo controla al reproducir.
- Coste medido (un núcleo, 4 minutos de música, con la escritura del WAV): 48 kHz 0,24 → 0,51 s;
  22,05 kHz 0,19 → 0,20 s; 96 kHz 0,31 → 0,62 s. El swr reforzado, 0,58–1,59 s. Nada al lado de lo que
  tarda Apollo por canción; el motor sigue con prioridad baja y un trabajo a la vez.
- Mismo remuestreo en el motor en vivo, en `5_procesar.bat`, en el escaneo de calidad y en las
  herramientas de prueba (piloto y comparación de mezcla). `00_verificar.py` dice cuál se usará.

Caché versionada: el remuestreador forma parte de la huella del motor (`modelo.fingerprint`), así que
la rocola nunca toma un resultado viejo como si fuera nuevo. Con el de siempre la huella es EXACTAMENTE
la anterior (prueba HuellaTests). Para no repetir trabajo que no cambia:
- Motor en vivo: si una canción pedida con la clave nueva tiene resultado con la clave de otro
  remuestreador y TODAS sus pistas vienen a 44,1 kHz (según ffprobe), el archivo se renombra a la
  clave nueva (una operación atómica, sin copiar ni volver a pasar por Apollo). Esos trabajos van
  primero en la cola. Las canciones a otra frecuencia se vuelven a procesar cuando se piden; mientras
  tanto suena el original (con el DSEE), como con cualquier canción aún sin procesar.
- Por lotes: el manifiesto apunta el remuestreador de cada canción (`ninguno` si no hizo falta). Al
  reanudar, las entradas de antes que siguen valiendo (44,1 kHz) se reapuntan una vez; las de otra
  frecuencia se rehacen.

Comprobado (pruebas automáticas en Linux con FFmpeg 6.1 y ffprobe; 28 casos en test_remuestreo.py):
elección con un FFmpeg simulado (con SoXR, sin SoXR, sin ninguno, salida de largo o nivel equivocado,
recuerdo por ejecutable y nueva prueba si cambia); calidad del elegido frente al de siempre (la tabla de
arriba); 44,1 kHz bit a bit en cinco formatos; alineación, duración, final y canales; picos; motor de
punta a punta (la de 48 kHz se decodifica con el elegido y cambia; la de 44,1 kHz y una marca `.orig` se
reaprovechan sin llamar a Apollo; la de 48 kHz se rehace y su archivo viejo no se toca; primero lo
reaprovechable y, sin cambio de huella, el orden de siempre); revisión del FFmpeg en marcha; y la
herramienta por lotes con un manifiesto de antes (la de 44,1 kHz no se rehace, la de 48 kHz sí, y una
tercera pasada no tiene nada pendiente). Las pruebas que ya había de Apollo siguen en verde, y ahora
corren también las de integración con FFmpeg real que antes se saltaban.

Riesgos de migración y cómo volver atrás:
- Al actualizar, la huella cambia (si tu FFmpeg trae SoXR). Lo de 44,1 kHz se reaprovecha al pedirse;
  lo de otras frecuencias se vuelve a procesar una vez, a medida que suena. Los WAV viejos de esas
  canciones quedan en la caché hasta que la limpieza por uso los retire (cuentan para el tope).
- Si vuelves a un FFmpeg sin SoXR (o sin ninguno de los dos), el motor lo detecta y la huella vuelve a
  la correspondiente; lo reaprovechable se renombra de vuelta del mismo modo.
- Si vuelves a la versión ANTERIOR del código, esta no sabe renombrar: las canciones cuyo resultado ya
  se movió a la clave nueva se volverían a procesar (tiempo de GPU; ni música ni configuración se
  pierden). Para evitarlo, respalda `ia\tiempo_real\cache\listos` antes de actualizar.
- `5_procesar.bat` añade una línea por canción reapuntada al manifiesto la primera vez (JSONL que solo
  crece; se lee la última línea de cada canción).

Pendiente: correr test_remuestreo con el FFmpeg de tu equipo (validar-audio-mejoras.ps1 lo incluye) y
escuchar A/B canciones a 22,05/32/48 kHz. Un MP3 que cambia de frecuencia a media canción (raro) se
toma por su primera frecuencia al decidir si se reaprovecha.

### 1.17 Continuidad sin pausas entre canciones (etapa 7 de audio, las dos ramas)

Archivos: Rocola/SinPausa.cs (nuevo: la secuencia `Encadenado` y `MedidaCancion`), Rocola/Form1.SinPausa.cs
(nuevo, igual en las dos ramas: cuándo se prepara la siguiente y qué pasa con la cola y la pantalla),
Form1.Sonido.cs (la cadena de una canción se arma en `ArmaCadena`, que usan el arranque de siempre y el
relevo; `LevelTap` pasa la medida en vivo; `FadeProvider.Quita`; `Output_PlaybackStopped`), Form1.Datos.cs,
Form1.cs, AperturaAudio.cs (`MiraPorDelante.Cumple`); pruebas/sinpausa.cs (nueva) y las baterías que
buscaban la cadena dentro de `StartOpenedTrack`, puestas al día (noche, turno, picoreal y realce; en HD
también respaldo y ventana).

Antes, al acabar una canción se cerraba la salida de audio, `PlayNext` abría la siguiente, se armaba su
cadena y se abría otra salida, que empieza llenando su colchón. Entre canción y canción quedaba un silencio
que no está en la música: el cierre y la apertura de la salida (no se puede medir sin Windows) más el
cebado de la cadena nueva, que sí se mide aquí: 144 cuadros (3,3 ms) con la cadena mínima y 243 cuadros
(5,5 ms) con des-recorte, DSEE, realce y limitador, a 44,1 kHz.

Cómo funciona ahora:
- La salida se queda abierta y lee de una secuencia. Cuando a la canción que suena le quedan 12 s, se arma
  la siguiente de la cola con la MISMA `ArmaCadena` (suena igual que si empezara sola), usando el lector
  que el calentador ya había abierto por delante, y solo si viene en el mismo formato (frecuencia, canales
  y tipo de muestra). Cuando la cadena que suena se acaba -con sus colas ya entregadas: realce, limitador,
  fundido- la siguiente sigue en la misma lectura, sin su cebado.
- No es un fundido cruzado: nada se solapa ni se mezcla. El fundido de salida de siempre se conserva entre
  canciones sueltas; solo se quita cuando la siguiente es la pista que sigue en el MISMO disco (no en un
  disco TOP, no con vídeos) y el fundido aún no empezó, para que un disco continuo siga continuo.
- La cola, el letrero y «lo que suena» cambian cuando la siguiente empieza a OÍRSE (pasado el colchón de la
  salida: 850 ms con WaveOut, 240 ms con WASAPI en HD), no cuando empieza a leerse. Los créditos no se tocan:
  se cobran al encolar, como siempre. El estado guardado para un corte de luz dice lo que de verdad suena.
- Va por el camino de siempre (cerrar la salida y `PlayNext`): la siguiente en otro formato, un vídeo, la
  medida de Ctrl+F3, una canción que aún no abrió el calentador, o si la que suena acaba antes de haber
  recogido el relevo anterior (canciones de menos de un segundo).
- Saltar sigue siendo `PlayNext`: para todo y empieza la siguiente desde el principio. Si se salta justo
  cuando la siguiente ya entró pero aún no se oía, esa siguiente empieza desde el principio (no se pierde).
- Fallos: si la siguiente falla al leer (archivo roto o desaparecido, disco extraíble quitado), la
  secuencia lo atrapa; la salida acaba como al terminar una canción y el fallo se atiende UNA vez, como el
  de siempre: aviso, un crédito devuelto a la canción que falló y sigue la próxima. Si lo que falla es la
  SALIDA (DAC desenchufado) con la siguiente ya dentro pero sin oírse, esa siguiente NO se da por sonada:
  sigue primera en la cola y empieza desde el principio, como antes.
- La sonoridad y la huella tonal que se aprenden oyendo pasan a la canción nueva en su primer bloque, en el
  hilo del sonido y sin reservar memoria (`MedidaCancion`); lo medido de la anterior se cierra una sola vez.
  Los ajustes en vivo de Ctrl+F3 y el modo noche también llegan a la siguiente ya preparada. En HD, el DSEE
  de la siguiente se decide con SU versión de Apollo; para la que suena da lo mismo que antes.

Medido (Mono, esta máquina; no es un procesador antiguo): entre las dos canciones sale 0 muestras de hueco
(bit a bit igual a cada una sola, seguidas); armar la cadena de la siguiente cuesta 0,12-0,28 ms en el hilo
de la pantalla, 12 s antes del final; leer a través de la secuencia añade menos de 0,2 ns por muestra (por
debajo del ruido de la medida). Leer y pasar a la siguiente no reservan memoria (comprobado en el código
compilado). Memoria de más: la cadena de la siguiente (sus búferes de DSEE, realce y limitador) durante esos
12 s; el lector ya lo tenía abierto el calentador.

Lo único que se descarta al empezar la siguiente es su cebado. Sin el DSEE es silencio (el realce 2× deja a
lo sumo su pre-oscilación, 2·10⁻⁶); con el DSEE detrás del des-recorte queda además el eco previo de la
banda que recrea sobre el silencio que mete el des-recorte: −67 dBFS a 44,1 kHz y −69 dBFS a 48 kHz, durante
1,2-1,4 ms (en la prueba, una entrada brusca a plena escala). Sin des-recorte no existe.

RETARDO Y RELLENO DEL MP3: medidos, SIN implementar. Cada MP3 lleva al principio el retardo del codificador
(576 muestras con LAME) y al final un relleno hasta completar el último cuadro. Medido con un disco continuo
sintético partido en tres pistas y codificado con LAME (128 y 320 kbit/s, 44,1 kHz): con un decodificador
de 529 muestras de retardo que no lee la etiqueta LAME, en cada unión quedan 2.027-2.257 muestras (46-51 ms)
que no son música. La rocola decodifica los MP3 con el descompresor ACM de Windows (NAudio); cuánto retrasa
ese descompresor y si entrega el final no se puede medir aquí (Linux no tiene ACM). Recortar por la etiqueta
LAME sin conocerlo podría cortar música o dejar parte del hueco, así que no se hizo. En discos en WAV (y en
cualquier formato sin retardo de codificador) la unión es exacta. Siguiente paso: medir en Windows el
retardo del ACM con un MP3 de prueba con un clic en una muestra conocida y, con ese dato, recortar SOLO en los
relevos entre pistas seguidas del mismo disco. AAC/WMA (HD, Media Foundation): sin medir.

Comprobado (pruebas automáticas, Mono; pruebas/sinpausa.cs, 73 comprobaciones en HD y 72 en Modest):
- La secuencia con cadenas de contenido conocido: 1, 2 y 6 canales y lecturas de 1, 7, 441 y 4096 cuadros
  y al azar (sin una muestra de más ni de menos); relevo apuntado una vez y recogido cuando se oye; otro
  formato no entra; fallo al empezar, a media canción y durante el cebado; siguiente vacía; cuadro a medias;
  dos hilos a la vez (150 canciones, cientos de retiradas); leer no reserva memoria.
- Las cadenas de verdad (des-recorte, DSEE, nivelado con realce, grave, limitador, fundido) a 44,1 y
  48 kHz: el cebado dura justo lo que suman sus retrasos y la música empieza en ese cuadro; encadenadas,
  bit a bit iguales a cada una sola.
- La rocola sin ventana: la medida en vivo; el relevo pasa la cola como `PlayNext` (créditos intactos,
  lector anterior soltado, estado guardado); al parar se suelta la preparada; «pista siguiente del mismo
  disco»; los cinco casos al parar la salida (acaba bien, falla la salida con la siguiente dentro o antes,
  falla la siguiente, falla la que suena); y de punta a punta con `ArmaCadena`, `PreparaSiguienteSinPausa` y
  el relevo: la salida es cada canción tal cual sonaría sola, seguidas, bit a bit; no se prepara a 48 kHz
  tras una de 44,1, ni un vídeo, ni a 13 s del final, ni durante la medida de Ctrl+F3; con otro disco se
  conserva el fundido.
- Nueve fallos puestos a propósito (no descartar el cebado, dar por sonada la siguiente cuando falla la
  salida, no mirar el formato, no pasar la medida, «siguiente del disco» siempre cierto, no sacar de la
  cola, no apuntar el relevo, dejar escapar el fallo de lectura, preparar a cualquier distancia): la
  batería se pone en rojo en todos.

Tandas completas con Mono: HD 170 baterías bien y Modest 157; en las dos solo falla `disco`, que no es de
esta etapa y espera tu decisión (leer solo el género ya no cuesta menos de la mitad desde que la lectura
completa se salta las carátulas).

Cómo repetirlo: `sh pruebas/todo.sh sinpausa` (Linux con Mono) o validar-audio-mejoras.ps1 en Windows
(la incluye). Para oírlo: un disco continuo (en vivo, mezclas) en WAV, encolado entero.

Riesgos de migración y cómo volver atrás:
- No cambia ningún archivo de datos, preferencia, crédito, caché ni modelo: volver al ejecutable anterior
  deja la rocola exactamente como estaba.
- Cambio audible deliberado: entre pistas seguidas del mismo disco ya no hay fundido de salida. Si se
  prefiere el fundido también ahí, es una línea (`PreparaSiguienteSinPausa`, la llamada a `Quita`).
- El letrero cambia cuando pasa el colchón estimado de la salida (el mismo que ya usaba el pulso visual);
  con un controlador de otra latencia puede adelantarse o atrasarse un poco.
- Pendiente de comprobar en Windows y en el bar: que WaveOut (Modest y HD) y WASAPI (HD) no dejan hueco al
  encadenar, con el oído y con una grabación del DAC; y XP real.

### 1.18 Remuestreo en reproducción (etapa 6 de audio, HD): medido el método, falta medir el DMO en Windows

Dónde convierte la rocola de frecuencia mientras suena: solo en HD, por WASAPI compartido, cuando la canción
viene a una frecuencia y Windows mezcla a otra (lo típico: canción a 44,1 kHz y equipo a 48 kHz).
`SalidaRemuestreada` usa el remuestreador DMO de Windows Media a través de NAudio y va ANTES del limitador
final, para que Windows no convierta después del limitador. Con WaveOut (Modest siempre, y HD si WASAPI no
arranca) convierte Windows: en XP el mezclador del sistema, cuya calidad se elige en el Panel de control
(Dispositivos de sonido y audio → Volumen → opciones avanzadas de los altavoces → Rendimiento: calidad de la
conversión de la frecuencia de muestreo; el nombre exacto depende del idioma de Windows); en Vista y
posteriores, el motor de audio. Eso no lo controla la rocola.

Lo que se sabe leyendo el código de NAudio 1.9 (sin Windows no se puede medir):
- NAudio no le pone calidad al DMO: trabaja con la de fábrica. La calidad va de 1 a 60 («media longitud del
  filtro», IWMResamplerProps::SetHalfFilterLength); cuánto vale la de fábrica no lo dice NAudio.
- Para que el DMO entregue sus últimas muestras, `SalidaRemuestreada` le añade 512·⌈entrada/salida⌉ cuadros
  de silencio y corta en la duración exacta. Si el DMO retrasara la señal sin compensarlo, ese corte se
  llevaría las últimas muestras (solo al final de la secuencia: con la continuidad sin pausas el
  remuestreador sigue de una canción a la otra). La batería lo comprueba con una ráfaga al final.
- El envoltorio de NAudio reserva dos arreglos pequeños en cada lectura (hilo de WASAPI, cada ~85 ms). No se
  cambió: no hay indicios de cortes y cambiarlo pide reescribir el envoltorio.
- El DMO nace en el hilo que lo lee (MTA), como exige; la cancelación y el vaciado no se tocan.
- La rocola solo convierte a la frecuencia a la que mezcla Windows, porque el modo compartido lo exige; no
  sube la frecuencia por su cuenta. Si el equipo está puesto a 96 o 192 kHz en el Panel de sonido, convierte
  a eso: no aporta información y cuesta más; si el DAC lo admite, 44,1 o 48 kHz basta.

Batería nueva pruebas/remuestreo.cs (solo HD). Mide con señales de contenido conocido: tonos de 1 a 20 kHz
(ganancia de cada canal y resto tras quitar el tono), un barrido logarítmico (respuesta continua, dónde cae
1 y 3 dB), ruido blanco por bandas, plegado al bajar de frecuencia (tonos por encima de la nueva mitad),
imágenes al subir, retraso y eco de un impulso, duración exacta, final (energía de 8 ms de tono al final) y
cruce de canales, en 44,1→48, 48→44,1, 44,1→96, 44,1→192 y 48→96 kHz, y el coste por segundo de música. En
Windows mide el DMO como lo usa la rocola y con calidad 60, y exige duración exacta, final entero, canales
separados y 1 kHz a su nivel y limpio. Donde no hay DMO (aquí: Linux con Mono) lo dice y solo comprueba que
la vara de medir sirve, con dos remuestreadores de referencia escritos en la propia batería:

| 44,1 → 48 kHz | Referencia (polifásica, doble precisión) | Interpolación lineal (mala a propósito) |
|---|---|---|
| Tonos de 1 a 20 kHz | 0,00 dB; resto −137 a −151 dB | −0,01 a −6,34 dB; resto −62 a −3 dB |
| Barrido | desvío 0,02 dB hasta 15 kHz; cae 1 dB en 21,6 kHz | cae 1 dB en 8,4 kHz y 3 dB en 14,7 kHz |
| Ruido por bandas hasta 15 kHz | 0,07 dB | 2,31 dB |
| Imágenes por encima de 22,05 kHz | −137 dB | −5 dB |
| Retraso, duración, final | 0,000 ms, exacta, 1,0000 | 0,000 ms, exacta, 0,9966 |
| Plegado 48 → 44,1 kHz | −128 dB | −5 dB |

Qué se evaluó y no se adoptó (todavía): subir la calidad del DMO a 60 (la batería la mide en Windows; se
sube solo si la de fábrica pierde más de 0,5 dB antes de 18 kHz, deja plegado o imágenes por encima de
−80 dB o no entrega el final, y la 60 lo arregla con un coste razonable en el equipo de 2 núcleos); el
remuestreador WDL de NAudio (su modo sinc no es público en NAudio 1.9 y el público es de filtro IIR: no
sirve); un polifásico propio como la referencia, pero ligero (solo si el DMO sale mal: no hay razón para
sustituir un componente del sistema sin números).

Cómo repetirlo: `sh pruebas/todo.sh remuestreo` (Linux: solo la vara de medir) o validar-audio-mejoras.ps1 en
Windows (mide el DMO; también `remuestreo.exe Rocola.exe` a mano). Pendiente: correrla en Windows y en el
equipo del bar, y decidir con esos números. No hay riesgo de migración: no cambia nada de lo que suena.

### 1.19 Salida al dispositivo (etapa 8 de audio, las dos ramas)

Archivos: Rocola/SalidaSonido.cs (nuevo, igual en las dos ramas), Form1.Sonido.cs (arranque, parada, lectores
que saltan, cadena retomada), Form1.Video.cs (Modest: parar y barra de progreso), Form1.SinPausa.cs (cambio de
aparato), Realce.cs (un defecto corregido, abajo); pruebas/salida.cs (nueva).

Lo que había: WaveOut (Modest siempre; HD si WASAPI no arranca o con `salida=waveout`) recibía 16 bits con
dither TPDF; HD por WASAPI compartido, coma flotante de punta a punta. Y un fallo de la SALIDA se trataba como
un fallo de la canción: crédito devuelto, apuntada en rocola_incompatibles.txt y a la siguiente. Con el DAC o
la tarjeta desenchufados, cada canción fallaba igual: la cola entera se vaciaba en segundos (devolviendo
créditos) y, al volver el aparato, la rocola seguía callada.

Implementado:
- FORMATO. En Windows Vista o posterior WaveOut recibe coma flotante de 32 bits tal cual sale de la cadena: allí
  el motor de audio de Windows mezcla en coma flotante, y entregarle 16 bits obligaba a redondear y a meter el
  ruido del dither para nada. Si el sistema la rechaza, 16 bits con dither TPDF, y no se vuelve a probar en esa
  sesión (queda apuntado en rocola_error.txt). En XP, 16 bits con dither como siempre: allí no se puede probar
  que el mezclador la admita. El dither se pone en un solo sitio, al final y solo en 16 bits. PCM de 24 bits no
  se usa: en Vista o posterior la coma flotante ya evita cuantizar, y en XP no se puede probar.
- EL APARATO NO ES LA CANCIÓN. Un fallo del dispositivo (no hay aparato ni controlador, lo tiene otro programa
  en exclusiva, se quitó, no hay servicio de audio o aparato predeterminado; la lista de códigos está en
  SalidaSonido.cs) ya no devuelve crédito, ni salta la canción, ni la apunta como incompatible: se espera
  (medio segundo, 2 s y después cada 10 s) y se retoma LA MISMA desde lo que ya se había oído (lo leído menos el
  colchón de la salida). La cola, los créditos y el historial no cambian; el letrero lo dice y a la tercera
  espera sale un aviso. Saltar cancela la espera. Si la salida se cae A MEDIA canción más de cinco veces seguidas
  con la misma, se trata como antes (crédito devuelto y siguiente), para no quedarse en un bucle. Un formato
  rechazado o un archivo roto siguen como siempre.
- RETOMAR. El lector salta al punto, el fundido de salida cuenta desde ahí (acaba donde acaba la canción) y de
  un trozo no se aprende sonoridad ni huella tonal. En HD, si WASAPI no arranca y se reintenta por WaveOut, se
  retoma en el mismo sitio.
- EL VIGÍA. Diez segundos con la salida en marcha sin pedir música (un controlador que se queda mudo sin avisar)
  cuentan como fallo del aparato. Un atorón corto o el cambio de canción sin pausa no cuentan.
- CAMBIO DE APARATO. Con la continuidad sin pausas la salida puede seguir abierta canción tras canción. Si Windows
  cambia de aparato predeterminado (Vista o posterior; se pregunta al motor de audio), la siguiente no se
  encadena: se abre en el aparato nuevo al acabar la que suena, como antes de la continuidad.
- DEFECTO CORREGIDO. En coma flotante NAudio no le da a la cadena un arreglo propio: le da una vista sobre su
  búfer de bytes. El vaciado del realce 2× al final de cada canción la borraba con `Array.Clear`, que sobre esa
  vista borra bytes en otro sitio: las últimas ~47 muestras de cada canción (con realce) salían con restos de la
  vuelta anterior en vez de silencio. Ya pasaba en HD por WASAPI cuando no hace falta remuestrear (desde la etapa
  3); con la coma flotante nueva habría llegado también a Modest. Ahora se borra muestra a muestra, y la batería
  lee la cadena entera -des-recorte, DSEE, realce, grave, limitador, fundido- y el relevo sin pausa por esa vista
  y exige que salgan idénticos (238.626 muestras, 0 distintas).

Estudiado y no adoptado:
- WASAPI EXCLUSIVO (HD). La rocola tomaría el aparato para ella sola, sin el mezclador de Windows: lo que sale
  llega tal cual al DAC, en su formato y con menos retraso. En un bar tiene contras: mientras suena, ningún otro
  programa (ni los sonidos de Windows) puede usar el aparato; el volumen de Windows deja de actuar; hay que
  entregar un formato que el aparato admita, y remuestrear en la rocola si no admite la frecuencia de la canción;
  falla si otro programa lo tiene o si Windows no permite el modo exclusivo; y necesita su propia recuperación de
  errores. No se implementó ni se volvió predeterminado: es una decisión tuya. Si lo quieres, iría como un valor
  de la línea `salida=` de la configuración (por ejemplo `salida=exclusivo`), sin tocar Ctrl+F3.

Qué se comprobó y qué no: las pruebas comprueban lo que la rocola ENTREGA a Windows (muestra a muestra, formato
y reacción a fallos simulados). Que el ruido de fondo baje en el aparato depende del motor de audio y del DAC:
una grabación en bucle (loopback) confirma lo que sale de Windows, y solo un analizador en la salida analógica
dice cómo mide el DAC. No se afirma ninguna mejora audible.

Comprobado (pruebas automáticas, Mono; pruebas/salida.cs, 40 comprobaciones en HD y 39 en Modest, con salidas
de mentira): coma flotante bit a bit; rechazo → 16 bits (la salida rechazada se suelta sin avisos colgando) y
no se vuelve a probar; XP → 16 bits con el ruido justo del dither triangular (0,502 escalones eficaces; sin
sesgo); un fallo del aparato no se confunde con un rechazo de formato; los dos formatos rechazados no dejan
salidas abiertas; 14 códigos de fallo del aparato y 11 de la canción bien separados; la espera no toca
créditos, cola ni incompatibles, espera 0,5 / 2 / 10 s, se cancela al saltar y a la sexta caída a medias se
rinde; retomar (salto exacto, fundido al final, sin aprender del trozo); el vigía (a los 10 s, no con la
salida parada, un vídeo, la medida de Ctrl+F3 o un atorón corto); el cambio de aparato de punta a punta con
`PreparaSiguienteSinPausa`; y el cableado leído del programa compilado. Diez fallos puestos a propósito (volver
al `Array.Clear`, confundir el aparato con el formato, no rendirse, no ajustar el fundido, no mirar el aparato,
todo al aparato, devolver crédito al esperar, vigía sin mirar el estado, 16 bits sin dither, aprender del
trozo) la ponen en rojo.

Tandas completas con Mono: HD 172 baterías bien y Modest 158; en las dos solo falla `disco` (espera tu
decisión; no es de esta etapa).

Cómo repetirlo: `sh pruebas/todo.sh salida` (Linux con Mono) o validar-audio-mejoras.ps1 en Windows (la
incluye). Pendiente en Windows y en el bar: desenchufar de verdad el DAC en mitad de una canción y ver que
retoma donde iba; cambiar el aparato predeterminado; comprobar en rocola_error.txt qué formato se usa; XP real.

Riesgos de migración y cómo volver atrás:
- Un controlador de Vista o posterior que acepte la coma flotante y la reproduzca mal sería raro (WaveOut pasa
  por el motor de audio), pero se oiría: se vuelve al ejecutable anterior.
- Con el aparato desenchufado la rocola ahora se queda en silencio con la cola intacta, esperando (el letrero lo
  dice); antes vaciaba la cola devolviendo créditos. Saltar sigue funcionando.
- El vigía podría tomar por aparato mudo un disco tan lento que la música no avance en 10 s: entonces retoma la
  canción donde iba.
- No cambia ningún archivo de datos ni de configuración: volver al ejecutable anterior es seguro.

### 1.20 Audio de vídeo (etapa 9 de audio): cómo funciona hoy, qué se puede compartir y la decisión

Esta etapa es sobre todo de documentación y de decisión: completar el procesamiento compartido exige cambiar la
forma en que suenan los vídeos, y eso se decide antes de hacerlo. Lo que se implementó es una batería que fija las
reglas de hoy (pruebas/videoaudio.cs). Nada de lo que suena cambió.

Cómo funciona hoy (leído del código, igual en las dos ramas):
- Los vídeos los reproduce el control del Reproductor de Windows Media (wmp.dll) dentro de la ventana de la
  rocola. Él decodifica la imagen y el sonido y los mantiene sincronizados con su propio reloj. Su sonido va por
  el camino de Windows (el mezclador de XP, o el motor de audio en Vista y posteriores), NO por la cadena de la
  rocola: a los vídeos no les llegan el DSEE, el EQ automático, el grave, el realce, el limitador de pico real ni
  la continuidad sin pausas. La sincronía imagen-sonido es enteramente del reproductor.
- Música y vídeo nunca suenan a la vez: `PlayNext` cierra la salida de la música antes de empezar un vídeo, y la
  continuidad sin pausas no encadena vídeos.
- Lo único que la rocola controla del sonido de un vídeo es el VOLUMEN del reproductor, de 0 a 100, y 100 es el
  archivo tal cual: el reproductor no sabe subir. Se calcula así (`VideoVolumeFor`):
  - base: la perilla de vídeo de Ctrl+F3 (`video_vol=`, 85 por defecto) o, en automático, el base más alto con
    el que el 85 % de los vídeos medidos llegan al objetivo de las canciones sin pasar de 100 (nunca menos de 40);
  - si el vídeo está medido, base × objetivo / sonoridad del vídeo (entre ×0,15 y ×4), topado en 100; «Subir
    TODO» no se les aplica, porque solo los empujaría contra el techo; a los no medidos sí;
  - el modo noche multiplica al final (solo baja);
  - al final, el sondeo del vídeo (cada 80 ms durante el fundido) baja el volumen hasta 0.
  La sonoridad de los vídeos la mide el pre-análisis cuando Windows puede decodificar su sonido (Media
  Foundation, de Vista en adelante; en XP no se miden y manda la perilla).
- Consecuencias: un vídeo grabado más bajo que el objetivo no se puede subir; sus picos no pasan por el
  limitador; y música y vídeos solo se emparejan en sonoridad media. Por regla, no se intenta arreglar pasando
  del 100 del reproductor.
- Lo que no está comprobado: la cuenta supone que el volumen del reproductor es lineal en amplitud (50 = −6 dB).
  Si su curva es otra, el emparejado de vídeos es aproximado. Cómo medirlo en Windows: un vídeo de prueba con un
  tono de 1 kHz a −20 dBFS, reproducido a 100, 71, 50 y 25, grabado en bucle (WASAPI loopback, por ejemplo con
  Audacity); si 71 no da −3 dB y 50 no da −6 dB, hay que corregir la cuenta.

Tres caminos para compartir procesamiento (decisión tuya):
- A) SEGUIR CON EL REPRODUCTOR (lo de hoy). Sin paridad de procesamiento: solo volumen. Mejora posible sin
  cambiar de motor: medir la curva de volumen del reproductor y corregir la cuenta. Riesgo nulo.
- B) EN HD, EL SONIDO DEL VÍDEO POR LA CADENA DE LA ROCOLA. Media Foundation decodifica la pista de sonido del
  mismo archivo, el reproductor se silencia y sigue poniendo la imagen, y la rocola reproduce el sonido por su
  cadena (WASAPI) atado al reloj del reproductor: lee su posición y corrige la deriva (saltando o repitiendo unos
  milisegundos) para quedar dentro de la sincronía labial (unos ±40 ms). Consecuencias: el archivo se decodifica
  dos veces (más CPU en el equipo de 2 núcleos); la posición que da el reproductor tiene saltos y retraso propios
  que hay que medir; solo es viable con WASAPI (con WaveOut el colchón es de 850 ms); si Media Foundation no abre
  un archivo, vuelve el sonido del reproductor; y no sirve para Modest (XP no tiene Media Foundation).
- C) OTRO MOTOR DE VÍDEO que entregue el sonido a la rocola (Media Foundation Media Engine, un grafo de
  DirectShow propio, LibVLC o FFmpeg): sincronía exacta con su reloj y la cadena completa. Es sustituir el motor
  audiovisual y, con LibVLC o FFmpeg, añadir una dependencia grande (decenas de MB), con dudas de compatibilidad
  en XP y trabajo de integración con la ventana de vídeo y el panel GPU.

Recomendación: A hoy, sin prometer paridad, y medir la curva del reproductor. Si quieres el mismo procesamiento
en los vídeos, empezar por B solo en HD, como prueba con criterios medibles (desfase por debajo de 40 ms durante
todo el vídeo, sin cortes, CPU aceptable en el equipo del bar) y con el sonido del reproductor de respaldo; C solo
si B no los cumple. No se sustituyó nada.

Comprobado (pruebas/videoaudio.cs, 10 comprobaciones, las dos ramas): 600 combinaciones de base, «Subir TODO»
(−12 a +12 dB), modo noche y vídeos de −26 a +26 dB sin pedir nunca más de 100 ni menos de 0; el emparejado
exacto dentro de los límites (×0,15 a ×4); «Subir TODO» no empuja a los medidos y sí sigue a los no medidos
(topado en 100); de noche nunca suena más que de día; el base automático deja hueco (36 de 40 vídeos llegan) y
no baja de 40; y todo el que pone volumen al reproductor lo saca de `VideoVolumeFor`. Cómo repetirlo:
`sh pruebas/todo.sh videoaudio` o validar-audio-mejoras.ps1.

## 2. Comprobaciones y cómo repetirlas

En cada repositorio, con sus herramientas de compilación .NET, Python con NumPy y FFmpeg:

    ./pruebas/validar-audio-mejoras.ps1 -Python "C:\ruta\python.exe"

Se puede indicar -OutputDirectory apuntando a una carpeta TEMPORAL de pruebas y
-Ffmpeg a su ejecutable. No usar release1/releaseHD como salida de este script.

El script:

1. Compila GPU y CPU con advertencias tratadas como errores; ejecuta las 29 baterías
   de validar-capas.ps1. Compilar CPU no significa modificar su renderizado.
2. Ejecuta diecinueve baterías adicionales: dsee, limite, entrenador, iaparidad, iaorigen, sonoridad,
   picoreal, realce, transiciones, sinpausa, salida, videoaudio, eqpreparado, iadatos, eqauto, calidad, nivel,
   bajoauto y bajo.
3. Ejecuta las pruebas Python de los entrenadores y sus protecciones (test_calidad_dsee,
   test_entrenar_eq, test_contrato_dsee, test_origen_dsee, test_estereo_dsee,
   test_seleccion_dsee y test_publicacion_dsee) y, en HD, test_remuestreo de ia/upscale (con el
   FFmpeg del PATH) y la batería remuestreo (el DMO de la salida moderna, medido de verdad en Windows).

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

Estado (22 de septiembre, seis entregas en las dos ramas): hechos 1 a 8 (ver 1.7 a 1.11). Del 7,
los transitorios solo se pueden medir con material de señal (WAV o sintético): el de la rocola
guarda cuadros sueltos. Lo que queda y por qué:
- Entrenar de verdad con validación independiente: necesita material cosechado por la rocola
  nueva (con rocola_ia_datos.idx). Con el material actual el entrenador se niega a publicar.
- Superioridad audible: A/B a volumen igualado en tu equipo; ningún número de aquí la demuestra.
- Paridad en XP real: correr pruebas/iaparidad.cs allí. Riesgo medido: entre 0 % y 1,06 % de las
  divisiones de la geometría caería en otro bin, según lo que el compilador de XP deje en registros.
- validar-audio-mejoras.ps1 completo en Windows.
- El artista solo se conoce si la canción es MP3 con etiqueta; distintas grafías del mismo
  artista («Queen» y «Queen & David Bowie») cuentan como artistas distintos. La separación por
  canción y por carpeta sí es estricta.

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

Estado (22 de septiembre, etapa 1 de audio, las dos ramas): hechos 1 a 6 (ver 1.12). Queda: medir la
tanda en el equipo del bar con MP3 reales, cotejar con el texto oficial y los casos 7-8 de Tech 3341,
y escuchar A/B a volumen igualado.

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

Estado (23 de septiembre, etapa 2 de audio, las dos ramas): hechos 1 a 6 (ver 1.13), con un límite
medido: con contenido fuerte entre 20 kHz y Nyquist la onda puede pasar hasta +0,22 dBTP (el viejo
+0,89). Queda: escucha A/B a volumen igualado, medir en XP real y en el equipo del bar, y decidir si
conviene cualquiera de las dos alternativas del límite.

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

Estado (23 de septiembre, etapas 3 y 4 de audio, las dos ramas): hechos 1 a 6 (ver 1.14 y 1.15): el
realce satura a 2× con el seco alineado; el grave, medido, no lo necesita; el grave sintético y el DSEE
ya cambian en vivo sin escalones ni saltos. Queda la escucha A/B y medir el coste en XP real y en el
equipo del bar.

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

Estado (23 de septiembre, etapa 5 de audio, HD): hechos 2 a 6 (ver 1.16): SoXR donde el FFmpeg lo
ejecuta, con alternativa probada; WAV float32 y 44,1 kHz como pide Apollo; alineación, picos,
canales, duración y final medidos; la huella cambia con el remuestreador y lo que no cambia se
reaprovecha. El punto 1 (el remuestreador DMO de la salida en vivo) es la etapa 6.

Estado del punto 1 (23 de septiembre, etapa 6, HD): medición preparada y validada (ver 1.18). El DMO solo
se puede medir en Windows; hasta tener esos números no se cambia ni el algoritmo ni la calidad.

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

Estado (23 de septiembre, etapa 8, las dos ramas): hechos 1, 2 y 4 con salidas simuladas (ver 1.19): coma
flotante en Vista o posterior, 16 bits con dither en XP y si se rechaza, fallos del aparato sin gastar
canciones, retomar, vigía y cambio de aparato. El 3 (WASAPI exclusivo) está estudiado y espera tu decisión. El 5
(loopback y medición del DAC) es del equipo del bar. NAudio sigue en 1.9.0.

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

Estado (23 de septiembre, etapa 7 de audio, las dos ramas): hechos 1 a 4 para la continuidad SIN solape
(ver 1.17): la siguiente entra por la misma salida sin cebado, con cola, créditos, historial y fallos
como antes. No se implementó fundido cruzado (solapar canciones pide una decisión tuya). Del 3, la
latencia del DSP se compensa; el retardo y el relleno del MP3 están medidos (46-51 ms por unión con LAME)
pero no se recortan hasta medir el descompresor ACM de Windows. El 5 (vídeo) es la etapa 9.

Estado del 5 (23 de septiembre, etapa 9, las dos ramas): documentado cómo se relacionan el reproductor de
Windows Media, la música y la sincronía, con tres caminos y sus consecuencias (ver 1.20). No se sustituyó el motor
ni se añadió ninguna dependencia: espera tu decisión. Lo implementado es la batería videoaudio, que fija las reglas
de hoy (nunca más de 100 en el reproductor, emparejado dentro de límites, noche solo baja).

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
