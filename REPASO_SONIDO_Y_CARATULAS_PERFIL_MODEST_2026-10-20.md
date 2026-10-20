# Repaso de la biblioteca, cadena de sonido y caratulas: perfil por etapas y ahorros — Modest, 20 de octubre de 2026

Destino: AMD A4-6300 (dos nucleos), 3,5 GB de RAM, SSD, Windows XP / .NET Framework 3.5. Solo la rama `claude/modest-carson-fa2v2n`. Punto de partida `c29b73e`.
Peticion: medir con el codigo de verdad tres caminos de procesador que compiten con la pantalla en los dos nucleos (el repaso por cancion de la biblioteca, la cadena de
sonido por segundo de musica y el camino de las caratulas), ver donde se va el tiempo y quitar lo que se pueda **sin cambiar ni un bit de lo que sale**. Nada de esto lleva
interruptor (regla del 15-10-2026): no cambia ni el sonido, ni los datos, ni el aspecto.

**Todo lo medido aqui es sintetico, de Linux/Mono (JIT de Mono sobre x64), con un descompresor de MP3 imitado, y con otro ejecutor trabajando en la misma maquina en parte de las medidas** (por eso se
alternan procesos y se dan medianas y minimos). No hay una cifra de Windows XP, ni del A4, ni del descompresor ACM de verdad, ni del GDI+ de XP. Lo que falta medir alli esta al final.

## Como se midio (para repetirlo)

- **El codigo de verdad, por reflexion.** `MeasureFile` + `MideIntegrada` (el repaso), `OpenAudio` + `ArmaCadena` + `DitherWaveProvider16` (la cadena, leida en bloques de 50 ms como la bomba), y
  `ThumbCachePath`, `DecodeCoverForPublication`, `SaveThumb`, `ColoresDePortada`, `ExtractCoverPalette`, `GpuPreviewStore` (las caratulas), sobre un `Form1` de verdad (sin encender) con datos aislados.
  Cada medida es un proceso aparte con el ejecutable que toque; las comparaciones viejo/nuevo alternan procesos.
- **El descompresor de MP3 de Windows (ACM) no existe en Linux.** Se le puso una imitacion de `Msacm32.dll` (C, 150 lineas, sobre `libmpg123`, nativo y rapido como el de Windows) para que el repaso y la
  reproduccion pasen por **su camino de verdad** (`Mp3FileReader` → lector ligero → descompresor de 16 bits → `LectorMedida`). **El tiempo de descomprimir de esta imitacion no vale para XP**: el `l3codec` de
  Windows es mucho mas lento. Por eso la etapa «decodificar» va aparte en todas las tablas.
- **Instrumentacion en una copia de las fuentes**, nunca en las entregadas: cronometros `Stopwatch` alrededor de cada etapa (relojes y contadores de bytes asignados). Los ejecutables entregados no los llevan.
- Canciones: 8 MP3 sinteticos de 185 a 240 s (musica «parecida»: bajo, acordes, ruido rosa, golpes; 192 y 320 kbps) y un WAV de 230 s de las baterias anteriores, mas siete de variantes (mono, 48 kHz, 22 kHz, con
  silencios, con continua, con un canal mudo, muy fuerte). Caratulas: 13 imagenes generadas (JPEG y PNG de 300 a 1.500 px, de 30 KB a 1,3 MB; tres con transparencia).

## 1. El repaso por cancion

Una cancion cuesta dos pasadas en segundo plano: `MeasureFile` (tres tramos de 20 s: sonoridad K aproximada, huella del EQ de 8 bandas, recortes, material y acantilado para la IA) y despues, en la segunda
tanda, `MideIntegrada` (la cancion entera: sonoridad BS.1770, picos, mapa de silencios). En total se descomprimen unos 273 s de audio por cancion de 213 s. Media de 9 canciones (8 MP3 y un WAV), cosecha de IA encendida:

| Etapa | Antes (ms/cancion) | % | Ahora (ms/cancion) | Que es |
| --- | ---: | ---: | ---: | --- |
| Descomprimir (ACM **imitado**; 8 de las 9 canciones) | 323,6 | 31,1 | 323,3 | NAudio + descompresor del sistema: no tocable |
| Leer: convertir a coma flotante, SampleChannel, barrido de NaN, lector ligero (tramos + entera) | 197,9 | 19,0 | 96,9 | **R1: -51 %** |
| Huella del EQ de 8 bandas (`HuellaDelTrozo`, tramos) | 203,5 | 19,5 | 207,1 | ya a mano; ver descartes |
| Sonoridad integrada (`MedidorBs1770.Estereo` + picos), cancion entera | 158,8 | 15,2 | 158,0 | doble precision, ya con el estado en variables |
| Sonoridad K aproximada y recortes (`SonoridadDelTrozo`, tramos) | 70,0 | 6,7 | 71,1 | |
| Bucle de la IA: FFT de cosecha 20,8 + calidad 6,9 + pares 6,3 + copias y cuentas | 56,2 | 5,4 | 57,4 | las FFT de 2.048 puntos son 34 ms |
| Abrir el archivo dos veces (indice de cuadros de NAudio) | 22,5 | 2,2 | 22,6 | NAudio: 1,3-1,6 MB de basura cada vez |
| Escribir la cosecha de la IA (datos, indice, generos) | 4,3 | 0,4 | 5,5 | |
| Publicar en las tablas (pantalla, `PublicaMedida`): 7 µs y 497 bytes, con tablas de 30.000 o de 100.000 canciones | < 0,01 | 0 | < 0,01 | O(1): el diario anade solo lo nuevo |
| **Total, mediana de 4 pasadas** | **1.042** | | **957** | **-8,2 %** (alternando procesos, 3 pasadas: 1.080 → 963, **-10,8 %**) |

Por minuto: una cancion media de 3,56 min cuesta 293 → 269 ms por minuto de cancion (4,55 min de audio descomprimido por cancion: 229 → 210 ms por minuto descomprimido). En 59.000-118.000 canciones pendientes,
a un solo carril: 17-34 horas de procesador en esta maquina con el descompresor rapido de la imitacion; en el A4 con el ACM de verdad la etapa de descomprimir sera la dominante.
Memoria: **2.932 KB asignados por cancion, practicamente todos del indice de cuadros de NAudio** (dos aperturas de 1,4-1,6 MB); lo demas son 64 KB. No cambia con R1.

### R1 (`d86cf29`) — los 16 bits enteros, sin el SampleChannel ni el barrido de NaN

`LectorMedida` leia el MP3 del sistema y el WAV de 16 bits por el `SampleChannel` de NAudio (cuatro piezas: el convertidor, que **divide** cada muestra entre 32768; un medidor sin nadie escuchando; un volumen a 1) y despues
barria cada muestra buscando NaN. `Pcm16Directo.cs` multiplica por 2^-15 (**exacto**: un entero de 16 bits cabe sobrado en una mantisa de 24 y multiplicar por una potencia de dos solo mueve el exponente: el mismo float que
la division, bit a bit) y no barre: un entero de 16 bits entre 32768 no puede ser NaN ni pasar de 1 (con volumen distinto de 1 multiplica y barre como siempre). La coma flotante de NLayer, los WAV de 8/24/32 bits y los demas formatos
siguen como antes. 5,3 → 2,8 ns por muestra. Es un ahorro de **trabajo quitado** (una division y una comparacion doble por muestra), asi que no depende del JIT.
Comprobado: bateria `lectura16` (139 comprobaciones, 150 con la rocola anterior): los 65.536 enteros, todos los tamanos de lectura y orígenes que entregan de 3 en 3 bytes, volumen, los MP3 de pruebas con el lector de NAudio y el
ligero, `LectorMedida` de verdad con WAV a cinco frecuencias y de 8/24/32 bits; y 16 archivos por `MeasureFile` + `MideIntegrada` en cuatro modos (con/sin cosecha, normal/a fondo): valores y **bytes de los archivos de la IA iguales**
a los del ejecutable anterior.

## 2. La cadena de sonido, por segundo de musica

Perfil con tarjeta (el de la rocola), 20 s de una cancion estereo de 44,1 kHz, bloques de 50 ms, ms de procesador por segundo de musica. «Nueva» = una cancion que el repaso aun no midio (la cadena le aprende la sonoridad K y la huella del EQ mientras suena;
con 59.000 pendientes es lo normal); «analizada» = ya medida. «De fabrica» = los ajustes de fabrica con el DSEE ofrecido (dos nucleos); «todo» = DSEE, subgrave, estereo estrecho, claridad, partes suaves, balance, I1, I2, I3, S1, S2, S3 y EQ encendidos.
Tiempo **propio** de cada etapa (el inclusivo menos el de la anterior), antes → ahora:

| Etapa | de fabrica, nueva | de fabrica, analizada | todo, nueva | todo, analizada |
| --- | ---: | ---: | ---: | ---: |
| Origen: lector, descomprimir (1,2-1,4), convertir, barrido, **aprender K y huella** | 8,94 → **6,28** | 1,90 → 1,98 | 9,96 → **6,36** | 2,28 → 2,30 |
| S1/S2 (`ArreglaGrabacion`) | 0,98 | 0,98 | 1,09 | 1,01 |
| Des-recorte | 0,70 | — | 0,80 | — |
| Quitar siseo (I2) | — | — | 9,90 | 9,62 |
| DSEE (`SpectralTap`) | 0,01 | 0,01 | 10,62 | 9,71 |
| Nivelado, EQ y realce 2x (`LevelTap`) — de ello el pulso visual y la aurora 4,6-4,9 y el realce 5,6-6,0 | 10,37 | 10,70 | 11,06 | 10,35 |
| Grave en mono / estereo estrecho | 0,95 | 0,96 | 2,57 | 2,24 |
| El local (claridad, partes suaves, balance) | 0,01 | 0,01 | 2,64 | 2,44 |
| Bajo psicoacustico | 1,33 | 1,35 | 1,43 | 1,33 |
| Limitador de pico real | 3,41 | 6,09 | 3,59 | 4,54 |
| Fundido y paso a 16 bits con dither (por diferencia) | ≈ 1,4 | ≈ 1,0 | ≈ 1,1 | ≈ 1,0 |
| **Cadena entera** (alternando procesos, 5 rondas; mediana) | 29,5 → **26,0** (**-12,0 %**) | 24,1 → 23,4 (ruido) | 56,3 → **52,1** (**-7,4 %**) | 49,2 → 47,2 (ruido) |

En «analizada» no hay cambio (la diferencia de 3-4 % es el ruido de la maquina compartida: ninguna de las dos mejoras toca ese camino). Con el perfil sin tarjeta (sin aurora) la cadena de fabrica cuesta 19,8 ms por segundo analizada y 25,4 nueva.
Memoria: **117 bytes asignados por bloque de 50 ms** (51 el origen, 22 el limitador, el resto del paso a 16 bits y la lectura): 2,3 KB por segundo de musica; no hay reservas por muestra en ninguna etapa.

### P1 (`0647c1a`) — el aprendizaje en vivo de una cancion nueva, por trozos

`MideBloqueOriginal` (en `FuenteOriginal`) aprende mientras suena: la sonoridad K muestra a muestra con los coeficientes y los estados en arreglos, y la huella tonal cuadro a cuadro (`AccumulateEqFrame`: 8 bandas por canal, con el arreglo de
coeficientes y el de estados indexados por banda, un contador y una limpieza por cuadro). Ahora los cuadros enteros del bloque van por `HuellaDelTrozoDesde` (la funcion del repaso, con desplazamiento) y la sonoridad K de estereo por
`KCuadrosEstereo` (coeficientes y memoria de los dos filtros en variables; tramos hasta el cuadro que cuenta 64 para la limpieza de restos diminutos). El cuadro partido de los extremos, el mono y los canales de mas siguen como antes.
Medido aislado (bateria `entradaligera`, 20 s de estereo): 6,6 → 4,0 ms por segundo; en la cadena entera, 2,7-3,6 ms por segundo menos en una cancion nueva.
Comprobado: `entradaligera` (20 comprobaciones; copia del codigo de antes dentro de la bateria): lecturas de 1 a 8.191 muestras (casi todas parten un cuadro), de 8 a 96 kHz, de 1 a 6 canales, con silencios exactos y picos: despues de cada lectura iguales, bit a bit,
`curSumSq`, `curSamples`, `kwCanal`, `kwLimpia`, los estados K, las 8 sumas de la huella, `eqMeasN`, `eqLimpia`, los estados de la huella y el cuadro partido pendiente; mutando una linea del codigo nuevo la bateria falla. Y la **cadena entera**: 10 canciones
(MP3 estereo y mono, 48 y 22 kHz, silencios, canal mudo, continua, muy fuerte, WAV) × 4 configuraciones: la salida (hash de todos los bytes de 16 bits con el dither fijo) y todo lo aprendido, iguales a los del ejecutable anterior.

## 3. Las caratulas (perfil con tarjeta), por caratula

`CoverPump` por cada caratula: `ThumbCachePath` (stat + hash; 3 veces por trabajo, a proposito: detecta que la portada se reemplazo), decodificar o leer la miniatura en cache, colores, paleta, vista previa de 32×32.
13 caratulas, 3 pasadas, GDI+ de **libgdiplus** (Mono): **los numeros absolutos no valen para el GDI+ de XP**; sirven para ver el reparto.

| Etapa | ms/caratula | KB asignados | Nota |
| --- | ---: | ---: | --- |
| Leer la portada original | 0,15 | — | un arreglo reutilizado por hilo |
| Decodificar (`Image.FromStream`, JPEG/PNG de 300-1.500 px) | 8,9 | — | GDI+ |
| Miniatura 256×256 (`Bitmap` + `DrawImage` HighQualityBilinear) | **47,9** | — | GDI+: el 84 % del camino frio |
| Copia privada para guardar (`Clone`) | 0,05 | — | |
| **Total, camino frio (miniatura que no estaba)** | **57,0** | 58 | |
| Guardar la miniatura en cache (PNG + archivo) | 21,1 (+0,1) | 6,3 | GDI+ |
| **Camino caliente** (leer 0,10 + decodificar el PNG 2,6 + copiar 0,08) | **2,8** | 17 | |
| Colores del disco / paleta | 0,25 / 0,14 | 0,9 / 5,8 | ya optimizadas (`PALETA_LIGERA`) |
| Vista previa de 32×32 (`DrawImage`) + a bytes + guardar | 3,3 + 0,01 + 0,02 | 0,2 + 4,3 + 0,3 | **mas que todo el camino caliente** |
| `ThumbCachePath` | 0,07 | 2,2 | ×3 por trabajo |

El procesador es de GDI+ en todo lo que pesa; lo gestionado (lectura, copias, colores) suma menos de 0,6 ms por caratula. **No se encontro un ahorro que deje los mismos pixeles y los mismos archivos de cache**: reducir con `DrawImage`
y guardar el PNG son llamadas al GDI+ del sistema (otro filtro o codificador cambia pixeles o bytes). La copia de la miniatura a la textura (`SubeBitmap`) es codigo de la tarjeta y de otro revisor; no se midio (necesita Direct3D).

## Lo descartado, con numeros

| Idea | Medida | Por que no |
| --- | --- | --- |
| `HuellaDelTrozo` en tramos de 64 cuadros (sin la prueba por cuadro) | 4,9 → 4,9 ns por banda y muestra | nada que ganar |
| `HuellaDelTrozo` sin el termino `k1·a0` (`k1` es 0 en el pasa-banda; bit a bit igual salvo el signo de un cero) | 4,9 → **12,3** ns | el JIT de Mono lo empeora 2,5 veces: un tiempo asi no se puede dar por bueno en CLR 2 |
| `HuellaDelTrozo` con dos bandas a la vez | 4,6-5,0 → 4,1-4,4 ns (-10…-17 %) | depende del JIT (x87 de XP con pocos registros: puede salir peor); sin forma de medirlo aqui |
| `SonoridadDelTrozo` con los estados en variables | 12,4 → 12,0 ns por muestra (-3 %) | dentro del ruido |
| `PicosBloques.Procesa` desenrollado en cuatro maximos | 2,0 → 1,5 ns con musica (7,0 → 1,5 con ruido) | ≈ 9 ms por cancion (< 1 %) |
| El bucle de la IA de `MeasureFile` por tandas (R2; iguales los bits, `DATOS` de la IA iguales) | 954 → 965 ms por cancion | no se nota (sube 1 %); revertido |
| Buffer de 64 KB para el `FileStream` del indice de NAudio | abrir 10,5 → 28 ms | peor en Mono |
| Dither de 16 bits con el estado en una local y el suelo sin `Math.Floor` | 16,2 → 14,5 ns por muestra (iguales 26 millones de muestras con extremos, NaN, infinitos) | 0,15 ms por segundo de 25 |
| `Pcm16Directo` tambien en la reproduccion (`FileAudioSource`) y sin el barrido de `FuenteOriginal` | ≈ 0,4 ms por segundo estimados (2 %) | exige un indicador «sin NaN» en las cinco fuentes (`IAudioSource`); por debajo de lo medible en la cadena entera |
| Limitador con su filtro a mano | ya descartado el 13-10 | sin cambio |
| `ArreglaGrabacion`, `DeepBass`, `GraveMono`, realce 2x | 1,0 / 1,4 / 1,0 / 5,6-6,0 ms por segundo | ya escritos a mano; sin estructura que quitar |

## Propuestas (cambiarian datos o aspecto, o no se pueden probar aqui)

- **Usar la pasada entera de la sonoridad integrada tambien para los tres tramos** (descomprimir 273 s → 213 s por cancion, -22 % de la etapa que sera la dominante en el A4): los tramos dejarian de salir de un salto
  (NAudio descomprime 3 cuadros de calentamiento tras saltar) y saldrian de la lectura seguida; **las muestras serian casi seguro las mismas, pero no esta probado que lo sean bit a bit**, y hay que decidir el orden (hoy todas las
  estimaciones primero, la entera despues; fusionadas, la primera estimacion de toda la biblioteca tardaria 4 veces mas). Necesita una decision y su propio interruptor si cambia algo.
- **No recalcular la vista previa de 32×32 cuando ya esta en el almacen** (`GpuPreviewStore.Contains`): 3,3 ms de 6,5 en una caratula caliente. Toca la logica de las vistas previas de la tarjeta (de otro revisor).
- La aurora del hilo de la musica (`ReportAudioBlock`, escalera de 7 cortes × 3 polos por cuadro): 4,6-4,9 ms por segundo en el perfil con tarjeta, 0,97 sin ella. Es la parte grafica del hilo de sonido; no se toco.

## Verificacion

- Compila con `-sdk:2 -langversion:3 -warn:4 -warnaserror+` (receta de la entrega), con y sin tarjeta, 0 avisos.
- Baterias nuevas en `todo.sh` y `pruebas/LEEME.md`: `lectura16`, `entradaligera`. Ajustada sin quitar lo que vigilaba: `audioestereo` (acepta la cadena `HuellaDelTrozo → HuellaDelTrozoDesde → AccumulateEqFrame`).
- Tanda completa (`pruebas/todo.sh`, con el `rocola.exe.config` de Mono y el puente de `FlushFileBuffers`, locales y sin subir): fallan los mismos 22 nombres de la tanda de referencia (abc, atraccion, auditoriageneral, calma,
  caratulasadaptativas, colacompacta, descompresor, discovivo, duerme, estilo, iasdisparo, leer, memoriaintegrada, moneda, mp3medidaligera, paginar, pantallaviva, partescaratulas, planbiblioteca, sala, tope, vigiapantalla) y,
  ademas, cuatro que fallaron por la carga de la maquina (otro ejecutor corria su tanda a la vez) y **pasan sueltas**: `indice` (una medida de tiempo), `auroraeficiente`, `optimizaciones` y `vuelta`. Ningun nombre nuevo.
- `cpucongelado`: los 14 metodos del dibujo por procesador, identicos.
- Pruebas automatizadas en Linux/Mono con datos sinteticos: no prueban Windows XP, el A4, el ACM, el GDI+ de XP ni la pantalla del bar.

## Que falta medir en el A4 / XP

1. **El repaso con el ACM de verdad**: cuanto de los ~1 s por cancion es descomprimir (aqui 31 %, con un descompresor nativo muy rapido). Si domina, la propuesta de fusionar las pasadas es el siguiente ahorro.
2. **El aprendizaje en vivo (P1) y R1 en CLR 2 / x87**: las cuentas no cambian de orden ni de expresion (bit a bit iguales), pero el JIT de XP reparte distinto los registros; comprobar en `rocola_diagnostico.txt` que no sube el tiempo de la musica.
3. Las caratulas con el GDI+ de XP: si la miniatura (aqui 84 % del camino frio) y la vista previa de 32×32 siguen siendo lo que pesa.
