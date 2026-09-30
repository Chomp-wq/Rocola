# Audio y entrenadores: cierre del 29 de septiembre de 2026

Continuación y límites de las cuatro mejoras siguientes: [Audio del 30 de septiembre](AUDIO_MEJORAS_2026-09-30.md).

Cambios compartidos en `rocola-hd` y `claude/modest-carson-fa2v2n`, salvo Apollo y DMO,
que pertenecen a HD. Modest conserva .NET 3.5 y el destino Windows XP. No se modifica
el renderizador CPU, la biblioteca, la configuración ni los modelos instalados.

## Reproducción

- La entrada se valida **antes** del des-recorte y del DSEE. NaN, infinito y muestras
  fuera del límite defensivo de ±4 se convierten en cero antes de contaminar FFT/filtros.
  No es un limitador de ±1: los overs finitos habituales se conservan.
- La sonoridad integrada y las ocho bandas aprendidas durante la reproducción proceden
  del original limpio, no del resultado de DSEE/EQ/realce. El pulso visual sigue recibiendo
  la señal procesada. El corte en vivo aún se detecta después del des-recorte, antes de
  reconstruir; no se certifica toda la ficha de calidad como previa al DSP.
  La generación de cada cadena evita atribuir a la canción actual los datos de la siguiente.
- La huella tonal pasa a versión 2. Las versiones 0/1 no se mezclan con huellas limpias
  al calcular AutoEQ y se vuelven a medir con los presupuestos existentes. No se borra
  el corpus DSEE ni se descartan mediciones integradas válidas de sonoridad.
- Se conserva la máscara declarada de altavoces a través de los proveedores y la salida.
  EQ, realce, DSEE y grave sintético no reconstruyen el canal LFE. Una disposición
  multicanal desconocida no se interpreta como 5.1 ni se mezcla para encadenarla.
  El detector global de corte DSEE sigue analizando la primera pareja de canales.
- Realce conserva su retraso fijo de 31 cuadros al encenderse/apagarse dentro de una
  canción. Cuando está apagado mantiene el historial, pero omite el filtrado 2× costoso;
  al encenderlo inicializa el filtro con ese historial. No pierde su protección de alias
  por haberse encendido tarde. Es un retraso compensado por la continuidad, no 31 ms.
- El recorte del relleno LAME mantiene el límite final después de un seek, recuperación
  de dispositivo o salto automático de silencio. Tiempo y seeks usan coordenadas
  musicales; los mapas de silencios se traducen al quitar el retraso inicial del MP3.
  Duración, saltos y recuperación se calculan por cuadros/ticks, evitando el redondeo
  a milisegundos de algunos lectores NAudio.
- Se permite continuidad entre tasas distintas con **la misma disposición conocida**,
  sin crossfade ni mezcla de canales. Un sinc de fases acotado conserva la duración y
  aplica antialias antes de reducir tasa. La tabla se prepara fuera de la lectura de audio;
  un limitador posterior protege los picos de interpolación. Mismo formato: camino directo.
  Sin tabla lista, cambio de disposición o conversión fuera de presupuesto: reconexión
  normal, como antes, sin impedir reproducir el archivo.
- El presupuesto de conversión crece con los procesadores disponibles; no es un techo
  global del programa ni de la reproducción directa. Se conserva un presupuesto menor
  en 2C/2T. Los buffers/tablas del nuevo conversor sinc se reservan al preparar la cadena,
  no dentro de cada Read; esto no afirma que todos los proveedores antiguos carezcan
  de una reserva inicial durante la primera lectura.
  La tabla se captura antes de tomar el lector: otra precarga no puede sustituirla y
  obligar a recalcular coeficientes en la interfaz. Un miss se reintenta sin perder la canción.
  El presupuesto es 40 millones de tap×canal/s por cada pareja de procesadores lógicos
  (mínimo una), sin el antiguo techo artificial desde ocho hilos. Más núcleos no garantizan
  capacidad del único hilo de conversión; los formatos nativos no usan este presupuesto.
- Recuperación y relevo usan el reloj de salida cuando el driver lo proporciona, teniendo
  en cuenta cebado, inicio de cadena, remuestreo y saltos de silencio. Si ese reloj falla,
  es incoherente o hubo huecos de la bomba, permanece el respaldo por latencia existente.
  Los consumidores utilizan una sola instantánea del tiempo oído: una lectura concurrente
  de la bomba no puede adelantar posición/relevo por consultar otro contador de entrega.
- En HD, el DMO de salida solicita calidad 60 antes de la primera lectura; si no puede
  configurarla, reconstruye el camino de calidad predeterminada. No se exige COM nuevo en XP.

## Entrenar mejor con el mismo material

No se amplía por defecto la red desplegada ni su coste de inferencia.

### DSEE

1. Muestreo estratificado con una posición aleatoria por intervalo temporal. Evita que
   un stride fijo seleccione siempre la misma clase de corte o canal.
2. Cosecha alternada entre los dos primeros canales musicales, dentro del presupuesto
   total por canción. La calidad/corte offline de la ficha se analiza independientemente
   en 88 ventanas del canal inicial: cosechar no cambia sus resultados. Este trabajo
   adicional está acotado y se realiza fuera de la lectura de reproducción.
3. Normalización calculada solamente con entrenamiento y absorbida en la primera capa
   al exportar RIA1: no añade operaciones al reproductor.
4. Regularización, mejor checkpoint por validación y semillas reproducibles. Máximo
   tres semillas y 200 épocas; la prueba final no decide pesos, época ni semilla.
5. Ajuste opcional del modelo instalado con arquitectura idéntica, aprendizaje más
   conservador y penalización de alejamiento. El checkpoint inicial también compite.
6. Evaluación float32 con el clamp real de la envolvente, comparación contra el modelo
   instalado y controles por clase; no basta que baje el error global.
7. El entrenamiento crea candidatos aislados e informes: **nunca activa ni reemplaza
   automáticamente el modelo instalado**. Un candidato rechazado no es un error técnico
   del entrenamiento ni una razón para forzar su instalación.

El índice de origen `.idx` mantiene su formato. El archivo opcional
`rocola_ia_datos.generos.tsv` registra hash de canción y género fiable de ID3, sin rutas.
Sirve para informar cobertura por género, no como entrada de la red. Género desconocido
se registra vacío, sin inventarlo; una corrección vacía invalida una etiqueta anterior.

Ejemplo, ejecutado desde `ia` con destinos nuevos:

```powershell
python entrenar_ia.py --datos "D:\material\rocola_ia_datos.bin" --capas 28,96,96,12 --max-ejemplos 60000 --epocas 30 --semillas 3 --instalado "D:\rocola\rocola_ia.bin" --ajustar-instalado --candidato "D:\candidatos\prueba1\candidato.bin" --informe "D:\candidatos\prueba1\informe_dsee.json"
```

La carpeta del candidato debe existir y los archivos de salida no existir. Para una red
con otra arquitectura, omitir `--ajustar-instalado` y usar sus dimensiones. Copiar juntos
`.bin`, `.idx` y, si existe, `.generos.tsv` del material. El arrastre al entrenador también
reconoce el índice y los metadatos asociados.

### AutoEQ

El entrenador habitual llama al evaluador estricto. Requiere huellas limpias v2;
v0/v1 se admiten solamente como diagnóstico explícito, nunca para exportar un candidato.
Separa álbumes/copias exactas, normaliza con train, selecciona por validación y compara
en prueba contra el promedio **de entrenamiento** y el modelo instalado. Comprueba
también el objetivo limitado como en runtime y regresiones por grave/brillo/aire.

```powershell
python entrenar_eq_candidato.py --datos "D:\material\rocola_eq.txt" --instalado "D:\rocola\rocola_eq_ia.bin" --destino "D:\candidatos\eq1"
```

El destino debe ser nuevo. Las huellas antiguas no se vuelven limpias por cambiarles el
número: dejar que la Rocola vuelva a medirlas. Si falta origen independiente o datos
válidos suficientes, el informe debe explicar el límite y conservar la IA instalada.

## Evaluación del audio reconstruido

`pruebas/renderdsee.cs` ejecuta el SpectralTap y RIA1 reales de cada rama, sin formulario,
dispositivo ni escritura en la instalación. Produce WAV float32 y comprueba cuadros
completos y finitos. La duración total se comprueba en el evaluador y sus pruebas Python.
`ia/evaluar_pcm_dsee.py` compara candidato/instalado/original,
incluyendo espectro, envolvente, ataques, imagen estéreo, picos y tiempo de render.
Genera A/B con RMS igualado y atenuación común, sin limitar cada versión de forma distinta.

Requiere pares WAV alineados, a la misma tasa/canales/duración; evalúa como máximo 20
pares de 30 segundos. La condición de master sin pérdidas y alineación se debe comprobar
externamente y declarar en el manifest: no se deduce por tener extensión WAV.
La evaluación es del DSEE, no una captura física del DAC ni de toda la cadena final.

```json
{"pares":[{"referencia":"master.wav","degradada":"codec.wav","genero":"rock","degradacion_conocida":true,"tipo":"musica","referencia_sin_perdidas":true,"alineacion_verificada":true}]}
```

```powershell
python evaluar_pcm_dsee.py --manifest "D:\pares\pares.json" --renderer "D:\pruebas\renderdsee.exe" --rocola "D:\pruebas\Rocola-gpu.exe" --candidato "D:\candidatos\prueba1\candidato.bin" --instalado "D:\rocola\rocola_ia.bin" --destino "D:\candidatos\pcm1"
```

Un guard numérico superado no sustituye la escucha. Un fixture sintético no cuenta como
evidencia musical ni anula un rechazo previo de envolvente.

## Apollo, solamente HD

- Detección con potencia por canal, varias ventanas distribuidas y confianza de una
  caída abrupta consistente. Material oscuro o incierto permanece original en automático;
  la ausencia natural de agudos no basta para reconstruirlo.
- Mezcla automática conservadora según corte/confianza. El porcentaje manual sigue
  siendo explícito. El detector no demuestra que el archivo haya sufrido un codec.
- Piloto con archivos sanos y cortes intermedios, no solamente los más deteriorados.
- Proceso masivo y caché maestra float32 sin cuantización PCM16 intermedia, WAV de salida
  predeterminado. MP3 compacto sigue como exportación explícita. La huella de caché
  identifica el detector y master nuevos. Se conservan límites y coordinación existentes.

## Resultados y límites

Dos candidatos DSEE se entrenaron en un corpus existente de 171.300 pares, con la misma
red 28→96→96→12 y partición de 51/8/4 artistas. El ensayo desde cero usó una muestra
estratificada de 60.000; el ajuste usó los 171.300. El modelo comparador era una copia
asociada al corpus (SHA-256 `f185f80fd8a41aa27b09063da03f74e44799372cf13d9e5983b14d1e62a389cb`),
no el archivo distribuido actualmente en `ia/rocola_ia.bin` (SHA-256
`8716e86510aebff96eff0c5fe94af966736a2805ecdb2460bc87807a5fc2a0ee`). Estas cifras
no son una evaluación contra ese modelo distribuido:

| Candidato | MSE de envolvente runtime | Comparador | Resultado |
| --- | ---: | ---: | --- |
| Desde cero, 20 épocas | 2,6006 | 1,5746 | Rechazado, también por clases |
| Ajuste del comparador, 30 épocas | 2,3218 | 1,5797 | Rechazado, también por clases |

Los muestreos/controles cambiaron entre esos ensayos: no comparar sus cifras como un
ranking de algoritmos. El test final es pequeño y el corpus de entrenamiento original
del modelo comparador se desconoce. **No se cambió ningún modelo instalado.**
En un fixture PCM sintético, el ajuste redujo error espectral alto ~7,2%, pero aumentó MSE
PCM ~3,8%; no demuestra mejora musical ni contradice el rechazo del guard de envolvente.
El corpus X/Y no contiene los pares PCM musicales originales/degradados verificados.

El nuevo sinc 44,1→48 kHz pasó de ~52 a ~21,6 ms por segundo estéreo en este PC durante
su optimización (incluye tabla/reserva/lectura en el fixture). Eso no mide un CPU antiguo.
En fixtures digitales: respuesta a 20 kHz dentro de 0,01 dB en 44,1↔48, imágenes de
interpolación por debajo de −143 dB en esas dos conversiones y plegados por debajo
de −112 dB al reducir 48→44,1 y 96→48. No extrapolarlo a cualquier driver, tasa o captura física.
Las conversiones multicanal extremas hacia/desde 192 kHz consumieron aproximadamente
222–487 ms por segundo de audio en este PC, según rama/canales/ejecución. No fueron
medidas aisladas comparables entre ramas: muestran coste sustancial, no rendimiento
garantizado por contar núcleos ni una recomendación de remuestrear innecesariamente.

Las regresiones específicas pasan en ambas ramas: 45 comprobaciones de medición original,
128 de continuidad/reloj/recorte/caché y 91 pruebas Python compartidas. HD añade 102 de
Apollo, 49 de negociación exclusiva simulada y 28 del DMO real de Windows. No son pruebas
de escucha ni de recuperación con un DAC físico.

La prueba HD del MP3 MFT real detectó un desfase de 528 muestras por canal frente al
ACM en este Windows. Se conserva el rechazo del programa; la prueba comprueba que
`Envuelve` vuelve a decidirlo y devuelve el ACM, con PCM entero bit-idéntico, formato y
duración iguales. Sus 65 comprobaciones pasan, pero **no** acreditan decodificación MP3
flotante real habilitada en este equipo. No se relajó el guard de alineación para usarla.

Pruebas reproducibles desde cada checkout:

```powershell
.\pruebas\validar-audio-mejoras.ps1 -OutputDirectory "$env:TEMP\rocola-audio-completo-nuevo"
.\pruebas\validar-audio-20260929.ps1 -OutputDirectory "$env:TEMP\rocola-audio29-nuevo"
```

Usar directorios temporales distintos por rama. El segundo compila GPU/CPU con warnings
como errores, verifica entrada original/LFE, metadatos y continuidad y ejecuta todas las
pruebas Python, incluida la reconstrucción PCM real con un modelo idéntico como control.
No arranca la Rocola instalada. El primero incluye formatos, controles y afinidad a dos
procesadores lógicos; esa afinidad **no** demuestra rendimiento 2C/2T antiguo ni XP real.
Para repetir solamente audio después de verificar las capas, el primero admite
`-SkipLayers`: vuelve a compilar las fuentes actuales, pero no repite las 31 baterías de capas.
Los cuatro runners de esta tanda fijan `ROCOLA_DATOS` a una subcarpeta nueva de su salida
temporal y restauran el valor anterior en `finally`, incluso ante fallo o `-BuildOnly`.
Así las pruebas que guardan preferencias/corpus/informes no heredan la instalación real.

Pendiente práctico: escucha musical A/B igualada con referencia verificada, medida en
2C/2T real, XP real para Modest y recuperación/loopback/DAC con los dispositivos del local.
No se promete recuperar el master perdido, ni superioridad audible de una IA por su MSE.
