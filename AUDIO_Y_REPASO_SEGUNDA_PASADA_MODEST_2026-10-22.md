# Audio y repaso, segunda pasada: cuatro ahorros que dejan los mismos bits — Modest, 22 de octubre de 2026

Destino: AMD A4-6300 (dos nucleos), Windows XP / .NET Framework 3.5, solo la rama `claude/modest-carson-fa2v2n`. Punto de partida `8b12902`. Continua `REPASO_SONIDO_Y_CARATULAS_PERFIL_MODEST_2026-10-20.md`.
Peticion: seguir quitando trabajo de las etapas de audio que quedaban pesadas **sin cambiar ni un bit de lo que sale** (nada lleva interruptor: regla del 15-10-2026).

**Todo lo medido es sintetico, de Linux/Mono (JIT de Mono sobre x64), con un descompresor de MP3 imitado (ACM no existe aqui) y con otro ejecutor trabajando en la misma maquina a ratos** (por eso se alterna, se da la mediana y el
ruido de las etapas que no se tocaron llega a +-10 %). Ninguna cifra es de Windows XP, del A4 ni del CLR 2 / x87. Lo que hay que mirar alli esta al final.

## Un hallazgo previo: la ronda anterior midio la cadena sin EQ

El EQ automatico esta encendido de fabrica (`eq_automatico=1`), pero el arnes de medida no le ponia curva al `LevelTap` (la media de la biblioteca no se calculaba en el arnes), asi que las tablas del 20-10 tienen
el EQ apagado. Con una curva de 8 bandas activas el EQ cuesta ~5,5 ms por segundo de musica (21 % de la cadena). Esta ronda mide tambien con EQ (`eq` = 8 bandas, `eq4` = 3 activas).

## Los cuatro ahorros (un commit cada uno, bateria propia)

| # | Que | Commit | Antes -> ahora (Mono, sintetico) | Bateria |
| --- | --- | --- | --- | --- |
| 1 | `QuitaSiseo.Transforma`: arreglos y seno con su signo (`senN`) en locales y etapas de la FFT de dos en dos | `b84d67d` | QuitaSiseo 10,1 -> 7,7 ms/s (-23 %); microprueba de la FFT de 1.024 puntos 47-54 -> 33 us | `siseorapido` |
| 2 | `DetectorPicoReal.Bloque`: filtro de 24 coeficientes desenrollado y `Afina` escrita en su sitio | `6c86baf` | LimitadorPicoReal 6,6 -> 6,0 ms/s (-9 %; -7,5 % con musica fuerte) | `picorapido` |
| 3 | `LectorMp3Ligero.Abre`: el indice de cuadros de NAudio contado aparte, en enteros | `f69c346` | abrir un MP3 15,7 -> 1,9 ms y 1.612 -> 345 KB por apertura; el repaso reserva 2.945 -> 692 KB por cancion | `indicemp3` |
| 4 | `LevelTap`: primera pasada por bandas cuando nada cambia; EQ de dos en dos bandas | `0fa3bff` | LevelTap con EQ de 8 bandas 16,2 -> 12,6 ms/s (-22 %); sin EQ 11,4 -> 10,6 | `eqporbandas` |

### 1. Transformada del quitasiseo
Una FFT compleja de 1.024 puntos cada 512 muestras (otra de vuelta donde quita algo) era tres cuartas partes de la etapa. Misma cuenta, mismas operaciones en el mismo orden; lo unico que cambia es donde viven los datos: los arreglos
`re`, `im` y las tablas en variables locales (se leian del objeto en cada acceso), `senN = -senT` (negar es exacto) y las etapas de la mariposa de dos en dos con los cuatro valores en `double` locales. Con 2.048 puntos (mas de 48 kHz) queda una etapa suelta.
Solo gana quien enciende «Quitar el siseo» (I2, apagado de fabrica).

### 2. Detector de pico real
El filtro simetrico de doce parejas (tres sumas `u`, `v`, `w`) va desenrollado con los 36 coeficientes en variables y `a + b` una vez; las cuatro llamadas a `Afina` por muestra van en su sitio. `Empuja` (muestra a muestra) conserva
el bucle y sirve de referencia. Ganancia pequena (0,5-0,6 ms de ~25) pero medida en tres series.

### 3. Indice de cuadros de los MP3
`Mp3FileReader` crea por cada cuadro un `Mp3Frame`, un arreglo de 4 bytes y un `Mp3Index` (~160 bytes, 1,3 MB y ~10 ms por cancion de 200 s) y el repaso abre cada cancion **dos veces** (los tres tramos y la sonoridad entera), asi que
no se podia reutilizar el indice de la primera apertura (las dos tandas estan separadas por horas y por toda la biblioteca). En vez de eso `LectorMp3Ligero.Abre` repite el constructor de NAudio 1.9 paso a paso (ID3v2, dos primeros cuadros, Xing,
ID3v1, `Mp3WaveFormat` con la tasa de bits media, descompresor) y cuenta el indice con dos `int[]` (donde empieza cada cuadro, cuantas muestras habia antes: 8 bytes por cuadro), leyendo en trozos de 64 KB (uno por hilo) y validando cada cabecera con
la propia `Mp3Frame.IsValidHeader` de NAudio. `Position` recorre ese indice con la regla de NAudio. El objeto se crea con `FormatterServices.GetUninitializedObject` y se le ponen los campos privados (comprobados por nombre y tipo al arrancar). Lo no
corriente (cambia la frecuencia o los canales a mitad, mas de 2 GB, cualquier error al leer, NAudio que no es la 1.9) cae al constructor de siempre, que contesta lo que contestaba. Ahorro por cancion: ~27 ms y 2,2 MB de basura (2-3 % del repaso);
el tiempo del repaso entero, alternando procesos, queda dentro del ruido (-3,5 % y +0,4 % en dos tandas). Tambien la reproduccion abre por aqui.

### 4. Nivelado por bandas
`ProcesaConRealce` hacia por muestra: rampas, cruce de EQ, limpieza de restos y la cascada de hasta 8 biquads por doble indireccion de arreglos. Casi siempre nada cambia y entonces el trozo de 512 cuadros se hace por bandas: ganancia y mezcla del realce como
constantes y el EQ de dos en dos bandas de punta a punta, con memoria y coeficientes en variables y los dos canales de un estereo a la vez; la salida de una banda es la entrada de la siguiente para esa muestra, en un `float` local (se redondea igual que al guardarla).
El trozo se parte donde toca la limpieza de restos (cada 64 cuadros) y todo lo que cambia (rampa, cruce, otro EQ, trozo que empieza a mitad de un cuadro) va cuadro a cuadro como antes.

## Antes / despues por etapa (ms de procesador por segundo de musica; c1.mp3, 15 s, bloques de 50 ms, EQ de 8 bandas puesto; mediana de 4 rondas alternando procesos)

| Etapa | de fabrica, nueva | de fabrica, analizada | todo, nueva | todo, analizada |
| --- | ---: | ---: | ---: | ---: |
| Origen (lector, descomprimir, aprender K y huella) | 5,71 -> 5,61 | 1,98 -> 1,76 | 6,54 -> 6,42 | 2,42 -> 2,62 (ruido) |
| QuitaSiseo (I2) | - | - | 9,12 -> **6,77** | 8,65 -> **7,17** |
| DSEE | 0,01 | 0,01 | 9,71 -> 9,64 | 8,73 -> 9,60 (ruido; no tocado) |
| LevelTap (nivel + EQ 8 bandas + realce + aurora) | 13,42 -> **9,91** | 13,75 -> **9,30** | 14,46 -> **10,61** | 12,89 -> **10,77** |
| Limitador de pico real | 3,08 -> 3,08 | 6,08 -> **5,08** | 3,33 -> 3,23 | 4,32 -> 4,43 |
| GraveMono / SalaLocal / DeepBass / ArreglaGrabacion (no tocados) | 0,9 / 0 / 1,2 / 0,9 | igual | igual | igual (+-10 % de ruido) |
| **Cadena entera** | 28,1 -> **22,5** (-20 %) | 26,5 -> **19,9** (-25 %) | 52,2 -> **46,0** (-12 %) | 46,3 -> **42,4** (-8 %) |

Sin EQ (como se midio el 20-10): LevelTap 11,4 -> 10,6. El limitador de «nueva» no baja porque con la cancion nueva el detector corta casi siempre por el atajo del umbral. La diferencia entre la columna «analizada» del limitador y las demas es la musica
(en c1.mp3 el detector pasa mas rato por el filtro).

## Comprobacion de que sale lo mismo

- Cuatro baterias nuevas en `todo.sh` y `pruebas/LEEME.md`: `siseorapido`, `picorapido`, `indicemp3`, `eqporbandas`. Cada una lleva dentro una copia del codigo de antes (o usa el camino viejo que sigue en el programa) y exige igualdad **bit a bit**; mutando
  una linea del codigo nuevo fallan (probado con 4 + 5 + 4 + 7 mutaciones; la unica que no falla es `x * 1f` -> `x * 1,0001f` en la ganancia, que es identica a proposito).
- Cadena entera con el ejecutable anterior y el nuevo (procesos aparte): hash de los floats de la salida (antes del dither) y todo lo aprendido, **iguales** en 10 canciones (MP3 estereo y mono, 48 y 22 kHz, silencios, canal mudo, continua, muy fuerte, WAV) x 4 configuraciones
  (de fabrica / todo encendido, nueva / analizada), y con EQ de 8 y de 3 bandas (10 x 4 mas).
- Repaso (`MeasureFile` + `MideIntegrada`, cosecha de la IA, normal y a fondo): valores y **bytes de los archivos de la IA iguales** (9 canciones x 4 modos).
- `mp3ligero`, `reproligera` y `enmemoria` ajustadas solo para que miren `LectorMp3Ligero.Abre` en vez del constructor; `eqpreparado`, `realce`, `desnormales`, `audiosuave`, `audioestereo`, `nivel`, `bomba`, `picoreal`, `arreglos`, `nivelpicos`, `lectura16` en verde.
- Compila con `-sdk:2 -langversion:3 -warn:4 -warnaserror+`, con y sin tarjeta, 0 avisos.

## Descartado con numeros

| Idea | Medida | Por que no |
| --- | --- | --- |
| FFT float del DSEE (`SpectralTap.Fft`) con etapas de dos en dos, y hasta con punteros `unsafe` | 75 us en las tres variantes (2.048 puntos) | en Mono no hay diferencia medible (la version double del quitasiseo si ganaba porque leia campos); no se puede decir nada del CLR 2 |
| `LimitadorPicoReal.Paso` + `AdvanceGain` con el estado en locales dentro de `Bloque` | 8,3 -> 8,2 ms/s (microprueba, bits iguales) | sin ganancia |
| `Pcm16Directo.Read` desenrollado de 4 en 4 | 2,81 -> 2,86 ns por muestra | sin ganancia |
| FFT de vuelta del quitasiseo podada (saltar los ceros) | no medido | cambiaria el signo de algun cero (-0) y por tanto bits: fuera de alcance |
| Realce 2x, `GraveMono`, `DeepBass`, `ArreglaGrabacion`, `SalaLocal`, `HuellaDelTrozo`, sonoridad integrada, `PicosBloques` | sin estructura que quitar sin reordenar cuentas, o ya descartado el 20-10 | `DeepBass` lleva sus estados en campos con redondeos explicitos para x87: pasarlos a locales puede cambiar bits en XP |
| Reutilizar el indice de la primera apertura en la segunda | - | las dos pasadas del repaso estan separadas por toda la biblioteca: guardarlo seria una cache sin cota |

## Propuestas (cambiarian bits, o no se pueden probar aqui)

- La FFT de `SpectralTap` con etapas de dos en dos (mismo orden de operaciones): en el CLR 2 puede ahorrar cargas y comprobaciones de limites como en el quitasiseo; hay que medirlo en el A4.
- Pasar `HuellaDelTrozoDesde` a dos bandas por vuelta (ya medido el 20-10: -10 a -17 % en Mono; el EQ de dos en dos de esta ronda tiene el mismo riesgo de JIT, ver abajo).

## Que falta medir en el A4 / XP

1. El EQ de dos en dos bandas (`DosBandasEstereo`) y la FFT del quitasiseo en CLR 2 / x87: las cuentas no cambian de orden ni de redondeo (cada salida intermedia es un `float`/`double` local, que el JIT de x86 guarda en memoria), pero el reparto de registros es otro. Mirar `LevelTap` en `rocola_diagnostico.txt`.
2. El indice propio con el disco y el ACM de verdad: ~10 ms menos por apertura (mas en XP) y 1,3 MB menos de basura; comprobar que la primera cancion de la noche abre igual.
3. El EQ real de la biblioteca: cuantas bandas quedan activas (aqui 8 y 3).
