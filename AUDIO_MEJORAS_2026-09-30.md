# Audio: continuación del 30 de septiembre de 2026

Aplicado a HD y Modest, excepto la nueva alternativa de descifrado que solo faltaba
en HD. Modest conserva .NET 3.5 y destino XP. No se ha cambiado el renderizador CPU,
la música, los modelos distribuidos ni los datos/configuración del usuario.

## 1. MP3 flotante alternativo en HD

HD incluye ahora el adaptador NLayer 1.12/MIT que ya usa Modest, dentro del ejecutable.
Se mantiene primero la opción MFT de Windows. Si se rechaza, se compara NLayer con
ACM en el mismo trabajador y dentro del mismo plazo total de ocho segundos.
La decisión se conserva por frecuencia/canales durante la sesión.

No se relajó el guardián de fidelidad: mismo número de muestras, sin desplazamiento,
error respecto de ACM menor de -50 dB salvo los picos que ACM recortó. NLayer requiere
además al menos cien cuadros cronometrados después del calentamiento y un costo
máximo del 3 % de un núcleo. Es una medición del equipo actual, no una deducción
por su número de núcleos. Si falla o no se puede demostrar compatibilidad, queda ACM.
NLayer funciona también en STA; MFT mantiene su restricción de hilo. Los fallos en
vivo conservan la recuperación calentando ACM con los tres cuadros anteriores.

Resultado real en este Windows, con `pruebas/flotante_overs.mp3`: MFT entrega 528
muestras/canal por detrás de ACM; NLayer se aparta aproximadamente -30 dB. **Ambos
se rechazan** y el archivo completo permanece bit-idéntico al ACM. La nueva
alternativa no implica que esté habilitada ni que haya mejorado audiblemente este
archivo. No forzar su uso ni rebajar el umbral para obtener un PASS.

## 2. DSEE mide el original antes de Declip

La cadena mantiene Declip antes de la reconstrucción. La diferencia es que el corte
espectral se determina usando PCM original saneado, no los armónicos creados al
reparar recortes. `FuenteOriginal` conserva un anillo fijo de dos ventanas más el
retardo real de Declip. El detector consulta ventanas alineadas a ese retardo.

Sin Declip se reutiliza la FFT existente: no hay análisis duplicado. Con Declip se
hace una FFT empaquetada adicional por pareja cada cuatro ventanas, además de la
primera ventana original completa disponible. El suavizado conserva aproximadamente
su escala temporal. No hay reservas por lectura ni se relee el archivo.

Cada pareja musical conserva sus cortes de agudos/graves y la histéresis de
reconstrucción. El LFE no interviene en la detección ni se reconstruye. El resumen
global usa el mayor corte de agudos y el menor corte de graves medido, no la última
pareja procesada. Sin disposición multicanal conocida, DSEE pasa el PCM intacto.

Pruebas nuevas: lecturas partidas, cola/duración de Declip, cortes separados de
frente/central/traseros, exclusión de LFE y una etapa sintética que cambia todo el
espectro para demostrar que el detector sigue viendo el original.

## 3. Entrenamiento con masa equilibrada y objetivo experimental

El entrenador obtiene pesos SOLO de las identidades de TRAIN: igual masa por grupo
y por canción dentro del grupo, juntando canales/recosechas. Los extremos quedan
entre 0,25 y 4, con media 1; las filas sin identidad conservan peso 1. No usa valores
de Y ni estadísticas de validación/prueba para decidir estos pesos.

La pérdida MSE sigue como objetivo predeterminado. `--perdida envolvente` habilita
una hipótesis offline: pseudo-Huber sobre log-envolvente representable por el
runtime [-4; 0,1], delta 0,5, más un ancla MSE cruda del 10 %. No aprende fase ni
ataques ni constituye una métrica perceptual certificada.

En ambos objetivos el checkpoint y la semilla se eligen por MSE cruda SIN pesos de
VALIDACIÓN. Al ajustar el instalado, la época cero también compite. Los guardianes
finales de prueba, por clase y de inferencia float32 permanecen intactos. Los informes
registran objetivo y equilibrio. La arquitectura/formato y costo de inferencia de
la Rocola no cambian por estas mejoras de entrenamiento.

### Ensayo realizado: candidato rechazado, modelo conservado

Se ensayaron veinte épocas, semilla 3, arquitectura 28→96→96→12, ajuste del modelo
real distribuido, con la pérdida experimental y 171.300 ejemplos del corpus histórico:
115.665 TRAIN, 17.500 VAL, 38.135 TEST; 51/8/4 grupos. Pesos TRAIN: 130 canciones,
51 grupos, mínimo 0,25, máximo 2,574 y media 1.

- SHA256 instalado: `8716e86510aebff96eff0c5fe94af966736a2805ecdb2460bc87807a5fc2a0ee`.
- SHA256 candidato aislado: `c2cba8cb197a8b2e39252df6a0e20f6ca95f25b0eaec00f75b5996635f178483`.
- MSE de prueba cruda: candidato 2,4098; instalado 2,0919: aproximadamente 15,2 % peor.
- Error de paridad de inferencia máximo: 5,3e-6; decisión de calidad: rechazo, código 2.

El corpus/test ya se había utilizado antes y el origen de entrenamiento del
instalado no está certificado. Es diagnóstico histórico, **no** una nueva evaluación
musical independiente. No hay evidencia para activar este candidato. Ambos modelos
distribuidos permanecen sin cambios. No se debe convertir MP3 a WAV y tratarlo como
un máster sin pérdidas para justificar una mejora.

Para reproducir con datos propios, desde la raíz (salidas nuevas y aisladas):

```powershell
$ensayo = Join-Path $env:TEMP ('dsee-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $ensayo | Out-Null
python ia/entrenar_ia.py --datos RUTA_DATOS.bin --indice RUTA_DATOS.idx `
  --instalado ia/rocola_ia.bin --capas 28,96,96,12 --ajustar-instalado `
  --epocas 20 --semillas 3 --perdida envolvente `
  --candidato "$ensayo/candidato.bin" --informe "$ensayo/informe.json"
```

## 4. Latencia adaptable, sin reducir fidelidad

Los perfiles son de sesión, acotados a ocho combinaciones de salida/frecuencia/canales.
WASAPI diferencia además el dispositivo y exclusivo/compartido. Se miden tiempo de
lectura/DSP, atraso de callbacks y huecos de la bomba, sin tocar el PCM.

- WaveOut empieza en 1.000 ms; puede proponer 750 y luego 500 ms. Conserva bloques de
  50 ms (20/15/10 bloques) y el callback de ventana compatible con el destino XP.
- WASAPI empieza en 300 ms; puede proponer 225 y luego 150 ms. Conserva el modo de
  salida, formato negociado, conversión y protección de pico real.
- Cada reducción necesita al menos 30 segundos reales y 30 segundos de PCM
  entregado sin picos de lectura/atrasos. Solo baja un escalón por apertura efectiva.
- La precarga del productor no cuenta como reproducción estable. Un hueco, un
  productor atascado, una lectura costosa o fallo de salida devuelve el perfil al
  margen conservador para la siguiente apertura.
- El cambio solo se aplica al volver a abrir la salida de forma natural. **No se
  reinicia ni corta una canción ni un relevo sin pausa únicamente para bajar latencia.**
- La precarga inicial y espera máxima de BombaSonido siguen el búfer que realmente se
  abrió; el colchón de trabajo posterior permanece en 200 ms. La sincronización usa
  el snapshot de esa salida, no una sugerencia aún no aplicada.

No se cambian frecuencia, profundidad, número de canales, calidad de filtros ni
modelo para ahorrar trabajo. No hay interruptor de rendimiento nuevo. Los milisegundos
anteriores son solicitudes nominales, no mediciones físicas de salida del DAC.

## Verificación y límites

Los cuatro runners aíslan `ROCOLA_DATOS` en TEMP y restauran su valor. Los runners de
compilación incluyen NLayer en ambas ramas; las pruebas de reflexión reciben su DLL
en TEMP porque no ejecutan `Program.Main`.
Además se prueba una copia del ejecutable SIN `NLayer.dll` externa: el recurso
integrado se resuelve y descodifica 116 cuadros MP3. Esto valida la distribución,
no la fidelidad del codec ni su activación.

Comandos principales desde cada raíz:

```powershell
./pruebas/validar-audio-20260929.ps1
./pruebas/validar-audio-mejoras.ps1
./pruebas/validar-mejoras-20260929.ps1
```

Se mantienen pruebas de compilación GPU/CPU, regresión general, continuidad, reloj,
formatos, DSP, entrenadores y negociaciones de salida simuladas. Las pruebas nuevas
de latencia verifican histéresis, escalones, recuperación, productor/consumidor,
precarga/espera y PCM exacto. No se ha probado Windows XP, un CPU antiguo real ni
latencia física del DAC. La afinidad a dos procesadores lógicos no demuestra esos
tres puntos. La comparación auditiva musical con máster válido sigue pendiente.

Resultado final automatizado en ambas ramas: 51 verificaciones DSP original,
106 de latencia, regresiones generales y baterías de audio/entrenadores completas.
HD supera además 69 controles de descifrado flotante y 50 de salida exclusiva.
Se conservan todas las pruebas anteriores; la identificación HD/Modest de la
auditoría de bloqueos usa ahora la salida exclusiva, no la presencia de NLayer
(ya compartido), sin ampliar la lista permitida de bloqueos.
