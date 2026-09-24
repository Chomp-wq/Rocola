# Audio: puntos 2 y 4 — 23 de septiembre de 2026

Aplicado a Modest (.NET 3.5, destino XP) y HD (.NET 4.8). No cambia el renderizado
por CPU, no añade interruptores y no sustituye archivos de las instalaciones.

## 2. Margen del limitador de pico real

El techo operativo pasa de 0,98 a 0,88 (−1,110 dBFS). El margen adicional es
0,935 dB; no es bajar permanentemente toda la música. La ganancia solo cambia
cuando la señal necesita limitarse. En las pruebas de nivel moderado, la salida
sigue siendo idéntica bit a bit a la entrada, salvo el retardo existente.

Se mantiene el detector 4×, su FIR, la preescucha y la recuperación: no se añaden
coeficientes, memoria por bloque ni latencia. El umbral menor puede hacer que el
detector completo se active algo antes. El mismo valor se usa en la cadena de
canción y, en HD, después del remuestreo compartido o exclusivo.

Primero se probó 0,925: corrigió el caso conocido, pero un conjunto añadido cerca
de Nyquist llegó a +0,273 dBTP. Por eso no se publicó aquel primer ajuste.

Medición final con la referencia independiente FFT 32× de
`pruebas/ref_picoreal.py`, idéntica en ambas ramas:

| Caso | Resultado |
|---|---:|
| Regresión: misma cadena y mismo limitador, techo anterior | +0,212 dBTP |
| Misma señal, techo nuevo | −0,723 dBTP |
| Máximo de tonos/impulsos de banda limitada | −1,087 dBTP |
| Saltos y ruido fuerte hasta Nyquist del conjunto inicial | −0,917 dBTP |
| Tonos 0,47/0,49/0,499 × Fs y tres ruidos adicionales | −0,151 dBTP |

La batería usa 22,05/44,1/48/96 kHz, mono/estéreo y seis canales; verifica colas,
lecturas partidas, ausencia de alteración bajo el umbral y ganancia enlazada.
38 comprobaciones pasan en cada rama. El caso con la cadena sintética incluye
nivelado, realce y grave; no sustituye una escucha de todas las combinaciones DSP.

Coste medido en ESTE PC por segundo de música estéreo 44,1 kHz: HD 1,80 ms sin
limitar / 4,02 ms limitando; Modest 1,86 / 4,70 ms. No son mediciones en XP ni en
un procesador antiguo de dos núcleos. No hay nuevos trabajadores de reproducción.

Límite: un estimador de pico real finito no demuestra un techo para toda señal
matemáticamente posible ni para cualquier DAC. Esta entrega cubre los casos
medidos; no promete ausencia universal de sobrepicos ni certificación ITU.
Referencia del concepto de medición: [ITU-R BS.1770-5](https://www.itu.int/rec/R-REC-BS.1770-5-202311-I).

## 4. Entrenamiento real y decisión sobre los modelos

Se entrenaron candidatos nuevos, con datos reales de Release1 y la MISMA
arquitectura que cada modelo instalado. Los modelos de Release1 y ReleaseHD
tienen las mismas huellas, verificadas al finalizar.

Ninguno superó al instalado: se rechazaron los dos. No se presentan estos
entrenamientos como una mejora audible ni se instala un modelo peor.

| Modelo | Material y separación | Comparación final |
|---|---|---|
| DSEE 28→96→96→12; 13.260 parámetros | 171.300 ejemplos del extractor 2; 51/8/4 grupos de artista para entrenamiento/validación/prueba | MSE 2,3956 candidato frente a 1,5800 instalado: 51,6 % peor |
| AutoEQ 5→24→24→3; 819 parámetros | 9.407 fichas con corte ≥19.300 Hz; 7.495/967/945 canciones; 1.191/134/150 grupos de álbum/copia exacta | RMSE medio por banda 2,6681 frente a 2,5796 dB: aproximadamente 3,4 % peor |

DSEE: 60 épocas, mejor checkpoint elegido solo por validación (época 30).
AutoEQ: 200 épocas, normalización y referencia media calculadas solo con
entrenamiento. Validación elige pesos; la prueba final no los elige. Ambos se
ejecutaron con un hilo BLAS. Los informes incluyen semillas, hashes, particiones,
errores por banda y decisión:

- [Informe DSEE](ia/informes/informe_dsee_20260923.json)
- [Informe AutoEQ](ia/informes/informe_eq_20260923.json)

La fuente DSEE original tenía unos 11 GB; solo se copiaron 27,4 MB de registros
cerrados con origen y CRC válidos. Se excluyó el material antiguo sin índice:
no sería honesto declarar una prueba independiente mezclando ese material.
Los datos personales permanecen fuera del repositorio; solo se publican métricas.
Los archivos fuente y los modelos instalados no se modificaron.

Limitaciones importantes:

- Solo cuatro grupos de artista en la prueba DSEE: no cubre todos los géneros.
- AutoEQ separa álbumes y copias de huella idéntica, NO garantiza separar artistas
  ni distintas codificaciones del mismo máster. Un corte alto no prueba que la
  grabación sea un máster sin pérdidas.
- Se desconoce el conjunto con el que se entrenaron los modelos instalados;
  no puede afirmarse que estas canciones sean nuevas para ellos.
- Un error espectral menor no prueba preferencia perceptiva. Antes de activar un
  candidato aceptado haría falta escucha A/B con volumen igualado.
- No se reentrenó Apollo: no hay un corpus validado de pares originales/degradados
  ni una evaluación perceptiva para justificar cambiar sus pesos.

## Procedimiento seguro para continuar

Se añaden `ia/preparar_candidato_dsee.py` y `ia/entrenar_eq_candidato.py` en
ambas ramas. Rechazan destinos existentes; nunca escriben sobre una instalación.
La instantánea DSEE verifica CRC de nuevo al copiar, filtra extractor actual y
exige particiones independientes. Un fallo puede dejar una carpeta parcial nueva:
no usarla sin su `preparacion.json`; repetir en otra carpeta NUEVA.

Ejemplo PowerShell, sustituyendo las rutas de datos/modelos por las propias:

```powershell
$env:OPENBLAS_NUM_THREADS='1'
$env:OMP_NUM_THREADS='1'
python ia/preparar_candidato_dsee.py --datos D:/Release1/rocola_ia_datos.bin --instalado D:/Release1/rocola_ia.bin --destino C:/audio-candidato-dsee-NUEVO
Push-Location C:/audio-candidato-dsee-NUEVO
python entrenar_ia.py --datos datos.bin --max-ejemplos 0 --instalado instalado.bin --epocas 60 --capas 28,96,96,12
Pop-Location
python ia/entrenar_eq_candidato.py --datos D:/Release1/rocola_eq.txt --instalado D:/Release1/rocola_eq_ia.bin --destino C:/audio-candidato-eq-NUEVO
```

El entrenador AutoEQ nuevo no tiene opción de forzar aceptación. Compara RMSE
contra la media de entrenamiento (mínimo 5 % de mejora) y contra el instalado
(mínimo 2 %); ninguna banda puede empeorar más del 10 %. Valida pesos float32 y
paridad de inferencia. Solo exporta un candidato separado si pasa. Incluso así,
`activado` queda falso y `escucha_pendiente` verdadero.

No buscar semillas/épocas hasta ganar en este mismo conjunto de prueba: eso
convertiría la prueba en entrenamiento. Para una nueva iteración, reunir más
artistas/álbumes y reservar un conjunto final nuevo, preferentemente másteres sin
pérdidas con degradaciones conocidas. Los modelos actuales siguen siendo la
referencia a superar, no una garantía de perfección.

## Verificación y pendientes ajenos a esta entrega

- Compilaciones Release de ambas ramas con advertencias como errores: correctas.
- 38 comprobaciones de pico real por rama: correctas.
- 68 pruebas Python por rama, incluidas nueve nuevas de candidatos: correctas.
- Se corrigió en tres pruebas antiguas el cierre de referencias a archivos
  mapeados antes de eliminar sus temporales; Windows impedía limpiarlos.
- Baterías DSP: HD 22/23 correctas; Modest 20/21 correctas (sin contar pico real).
- `eqpreparado` falla en Modest por igualdad exacta de PCM (22.050 Hz mono).
- `flotante` falla tres comprobaciones del decodificador Windows real en HD:
  diferencia de 528 muestras y sin valores >1. El guardián lo rechaza y mantiene
  el decodificador clásico; no se forzó el camino que falla.
- La batería general visual se detiene en `estelaestelar` (geometría HAL).
  Los tres problemas anteriores se reprodujeron compilando el HEAD anterior
  intacto (HD 91b8a63; Modest 1b9d575); no se ocultan como pruebas superadas.
- Pendientes: revisar esos fallos por separado, escucha A/B, hardware/DAC,
  rendimiento en CPU antiguo y prueba real de XP. No se desplegó en Release1/HD.
