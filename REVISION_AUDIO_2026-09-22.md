# Revisión de calidad de audio — 22 de septiembre de 2026

Actualización posterior: esta revisión se conserva como diagnóstico inicial. Las mejoras
implementadas después y el plan detallado para continuar están en
[Mejoras de audio y pendientes](MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md).

## Resultado y alcance

Sí queda margen de mejora. Esta revisión inspecciona la cadena, análisis, entrenadores, salida y herramientas; **no modifica el DSP, sus ajustes, los modelos instalados ni el sonido por defecto**. Distingue defectos observables en código de mejoras cuya ventaja audible debe medirse.

Compartido: Modest/.NET 3.5 y HD/.NET 4.8. Apollo, conversión por lotes y salida WASAPI corresponden a HD; no se incorporan esas dependencias a XP.

## Lo que ya está bien encaminado

- Procesamiento flotante; limitador con anticipación, ganancia estéreo enlazada y vaciado de cola; protección de valores no finitos en LevelTap.
- Dither TPDF al reducir a PCM16 en WaveOut. No hace falta añadir otra capa de dither antes.
- HD prepara la salida flotante para la frecuencia del mezclador WASAPI compartido y limita de nuevo después de remuestrear. El cambio de hilo del DMO y su cola ya están contemplados.
- DSEE conserva el camino sin procesar cuando no actúa; hay controles de corte espectral, límites de realce y pruebas de separación de canales.
- Apollo guarda caché WAV float32, no una segunda compresión MP3. Conserva originales y mezcla configurable; hay igualación RMS, procesamiento por fragmentos y almacenamiento adaptativo.
- AutoEQ ya restringe la corrección, aprende su referencia de temas sanos y evita intentar amplificar bandas que el códec eliminó. Su entrenador ya exige superar una referencia antes de exportar.

## Mejoras prioritarias

| Prioridad / apartado | Evidencia y propuesta | Coste y alcance |
|---|---|---|
| Alta: cambios en vivo | LevelTap.Read aplica Gain × Master de una vez por bloque y cambia al estado nuevo del EQ. Añadir rampas cortas por muestra y transición entre filtros viejo/nuevo, también al activar/desactivar. Evitar interpolar coeficientes sin probar estabilidad. | Bajo para ganancias; trabajo doble del EQ solo durante la transición. Ambas ramas. Previene discontinuidades, no altera la curva final. |
| Alta: huella estéreo del AutoEQ | MeasureFile y ReportAudioBlock suman L/R antes de medir sus bandas. Contenido en contrafase puede cancelarse y falsear la huella. Medir energía por canal y combinar potencias, versionando las medidas antiguas. | Moderado durante análisis/aprendizaje; casi nulo al reproducir temas ya medidos. Ambas. |
| Alta: publicación del modelo DSEE | entrenar_ia.py imprime la comparación con la curva fija pero exporta RIA1 sin exigir mejora. Reservar un mínimo de mejora, valores finitos y regresiones por banda; conservar el modelo anterior si no supera los criterios. | Fuera de reproducción; no penaliza equipos modestos. Ambas. No basta que gane el error numérico: validar también percepción. |
| Alta: validación de IA | DSEE divide ejemplos al azar, no por canción; fragmentos del mismo tema pueden aparecer en entrenamiento y validación. Añadir identidad de origen o un conjunto externo separado por canciones/artistas. En EQ, calcular normalización con el conjunto de entrenamiento para una evaluación estricta. | Entrenamiento/datos, no reproducción. Requiere migración compatible del conjunto, no romper RIA1. |
| Media-alta: picos reales | LookaheadLimiter solo observa máximos de muestras, techo 0,98. No detecta explícitamente máximos entre muestras. Evaluar detector true-peak con sobremuestreo, margen y enlace L/R después del último procesamiento/remuestreo. | Moderado continuo si se activa el detector en salida. Medirlo antes de fijarlo en 2C/2T. El análisis offline ayuda, pero no cubre por sí solo picos creados después por DSP. |
| Media: sonoridad | La medida actual suma energía K-ponderada, y el preanálisis normal usa tramos. No es sonoridad integrada BS.1770 completa. Añadir medida integrada de canción completa, puertas por bloques y pico real cacheados, distinguiendo estimación rápida de medida completa. | Mayor trabajo de preanálisis, consumo mínimo al reproducir. Versionar caché y preservar el nivelado anterior durante migración. |
| Media: realce de agudos | El filtro del excitador usa A = 0,55 para todas las frecuencias de muestreo. Su banda física cambia entre 44,1/48/96 kHz. Derivar el coeficiente de la frecuencia, conservando la respuesta de referencia a 44,1 kHz. | Despreciable al construir la etapa. Ambas. |
| Condicional: aliasing | El excitador produce armónicos con una no linealidad. Medir aliasing con barridos y multitonales; evaluar sobremuestreo solo en esa etapa. El grave sintético trabaja con señal previamente filtrada y armónicos bajos: no asumir que necesita el mismo tratamiento. | Potencialmente medio/alto; no sobremuestrear toda la cadena por defecto. |
| Condicional: remuestreo | En HD no se selecciona expresamente calidad del DMO desde SalidaRemuestreada. Medir su respuesta real antes de reemplazarlo. En Apollo, comparar el remuestreador actual con SoXR de alta precisión durante la preparación offline, si el FFmpeg instalado lo admite. | DMO requiere medición en vivo; Apollo puede asumir más coste fuera de reproducción. Mantener contrato 44,1 kHz del modelo. |
| Condicional: salida | Modest/WaveOut entrega PCM16 con dither. Evaluar 24 bits o flotante solo si el controlador los admite, con retorno fiable a PCM16. HD podría ofrecer salida exclusiva, pero no garantiza una mejora perceptible y cambia cómo comparte el dispositivo. | Dependiente de hardware/driver; no forzarlo en XP ni actualizar NAudio incompatiblemente. |
| Diseño: continuidad y vídeo | FadeProvider es un fundido de salida, no un solapamiento real de dos canciones. WMP controla el audio de vídeo fuera del DSP musical y su volumen no puede superar el original. Gapless/crossfade real y una cadena audiovisual común requieren diseño aparte. | Trabajo y riesgo mayores; sincronía, créditos, errores y salida deben conservarse. No es un parche ligero. |

### Dos ejemplos verificables de por qué hay margen

1. Una senoide de amplitud 1,2, frecuencia Fs/4 y fase pi/4 tiene muestras de magnitud aproximada 0,8485, pero su pico reconstruido ideal es 1,2. Un limitador que solo mira muestras no puede garantizar el pico entre ellas. Es un ejemplo matemático, no una medida de tu DAC.
2. Si L = s y R = -s, la suma mono vale cero aunque ambos canales tienen energía. Esa suma precede actualmente a los filtros de la huella tonal; combinar potencias evita esa cancelación.

La referencia para sonoridad integrada y pico real es [ITU-R BS.1770-5](https://www.itu.int/rec/R-REC-BS.1770-5-202311-I): contempla bloques de 400 ms con solape, puertas absoluta y relativa, y estimación de picos entre muestras. Su adopción exigiría pruebas de conformidad; no se declara que la rocola ya las cumpla.

FFmpeg documenta [los motores de remuestreo y la precisión SoXR](https://ffmpeg.org/ffmpeg-resampler.html). Su disponibilidad depende de cómo se compiló el ejecutable. Más precisión numérica no demuestra por sí sola mejora audible.

## Modelos y fidelidad

No recomiendo instalar una red más grande solo por su tamaño. La prioridad es evitar publicar regresiones, separar bien los datos y comparar a volumen igualado con temas completos, transitorios, voces y estéreo amplio. El DSEE estima contenido perdido: no puede certificar recuperación exacta del máster original. Apollo tampoco convierte automáticamente toda fuente en una referencia mejor.

Para fuentes buenas debe conservarse un camino sin reconstrucción innecesaria. Un perfil de fidelidad podría mantener protección y nivelado mientras omite coloración opcional; no se debe cambiar silenciosamente la preferencia actual. La calibración de altavoces/sala exige mediciones propias, no deducirla de la portada, del género o del promedio de canciones.

## Verificación realizada y límites

- Revisión de Form1.Sonido.cs, Form1.Video.cs, apertura/salida, entrenar_ia.py, entrenar_eq.py y utilidades Apollo de HD.
- DSEE: pasan sus comprobaciones; 24 vectores del modelo de muestra del repositorio coinciden con el entrenador con diferencia máxima aproximada 0,0000011 en ambas ramas. Esto valida carga/inferencia, **no la superioridad audible ni los modelos de tus releases**.
- Limitador: pasan pruebas existentes de transitorios, muestras y cola. No son pruebas de conformidad true-peak.
- Decodificación sintética de MP3 CBR/VBR, WMA, AAC y WAV PCM/float32: comparación PCM con la ruta de referencia y liberación de archivos, en ambas ramas.
- No se cambian ni reentrenan modelos. No se realizó inferencia Apollo con tus checkpoints, escucha A/B, medida del DAC, prueba real en XP ni medición de coste en un procesador antiguo.

Orden recomendado: rampas/EQ sin clics, huella estéreo y guardián del entrenador; después sonoridad integrada y pico real medidos; finalmente remuestreo/salidas/continuidad donde las pruebas indiquen un beneficio.
