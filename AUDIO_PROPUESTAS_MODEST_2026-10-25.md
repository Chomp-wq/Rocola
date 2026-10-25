# Más mejoras de audio, para elegir — Modest, 25 de octubre de 2026

Petición del dueño: «Busca más mejoras de audio, nuevas implementaciones y posibles mejoras a lo que ya está hecho».

Esta ronda es solo de buscar y medir: **no cambia nada del programa.** Todo lo medido es con canciones sintéticas en Linux/Mono,
con la cadena de verdad. Nada se oyó en el billar ni se midió en el A4.

## Lo que ya hay, para no repetir

La cadena entera y lo hecho están en [REVISION_CADENA_AUDIO_MODEST_2026-10-24.md](REVISION_CADENA_AUDIO_MODEST_2026-10-24.md),
[MODOS_AUTOMATICOS_SONIDO_MODEST_2026-10-23.md](MODOS_AUTOMATICOS_SONIDO_MODEST_2026-10-23.md) y
[AUDIO_PROPUESTAS_MODEST_2026-10-13.md](AUDIO_PROPUESTAS_MODEST_2026-10-13.md). Ya existen, entre otras cosas:

- el nivelado al límite y el nivel por disco;
- el limitador de pico real (4x, con espera y suelta medidas);
- el grave en mono y el subgrave, en fase lineal;
- el local entero (claridad, partes suaves, graves, agudos, balance, presencia), con su modo automático por el LRA;
- S1 a S5, I1 a I3, el des-recorte, los MP3 en coma flotante para las que llegan al máximo, el sin pausa de los MP3 (LAME) y el disco
  continuo sin escalón;
- el repaso de la biblioteca, que ya mide primero lo pedido y lo más sonado.

Lo de abajo es lo que **no** encontré, o lo que se puede hacer mejor.

## 1. Mejoras a lo que ya está hecho

### T1 (RECOMENDADA). El nivelado al límite, contando con los graves y agudos del local

**Lo que encontré.** El nivelado al límite sube cada canción hasta que sus picos quedan a 0,5 dB del techo del limitador. Ese margen
se midió el 24-10 con la cadena de fábrica, y con las piezas del local que cumplían. Pero «Graves del local» y «Agudos del local»
(Ctrl+F3 → Sonido → El local y sus bocinas → Tono y presencia) no estaban en esa medida. Subirlos sube los picos, y el nivelado no se
entera: sube la canción igual que con el tono en 0.

Medido en la cadena de verdad con seis canciones de prueba: el tiempo en que el limitador quita más de 1 dB y más de 3 dB. El
criterio del nivelado al límite es como mucho el 10 % y el 1 %.

| Ajuste | Lo peor de las seis: >1 dB | >3 dB | Lo más que baja |
|---|---|---|---|
| De fábrica (tono en 0) | 7,7 % | 0,3 % | 3,9 dB |
| Graves +3 | **95,7 %** (la masterizada) | 4,6 % | 5,4 dB |
| Graves +6 | **99,9 %** | **83,9 %** | 7,0 dB |
| Agudos +3 | 16,7 % | 0,6 % | 4,2 dB |
| Agudos +6 | 45,0 % | 2,5 % | 5,1 dB |
| Graves +6 y agudos +6 | 100 % | 90,3 % | 7,2 dB |
| Graves −6 | 3,4 % | 0 % | 2,4 dB |

Con los graves a +6, una canción masterizada pasa el 84 % del tiempo con el limitador quitándole más de 3 dB. Eso es aplastarla: el
bombo «bombea» la voz y lo fuerte suena áspero. Al subir los graves para que se oigan más, se consigue sobre todo que el limitador
trabaje.

**La propuesta.** Que el nivelado le deje al tono del local el margen que necesita. Medido: lo que hay que bajar el nivelado para
volver al criterio, con las seis canciones.

| Tono del local | Bajar el nivelado | Con eso: >1 dB | >3 dB |
|---|---|---|---|
| Graves +3 | 2,0 dB | 7,1 % | 0,2 % |
| Graves +6 | 4,5 dB | 3,6 % | 0 % |
| Agudos +3 | 0,5 dB | 7,0 % | 0,3 % |
| Agudos +6 | 1,5 dB | 7,0 % | 0,2 % |

Viene a ser de 0,7 a 0,75 dB por cada dB de graves y de 0,2 a 0,25 por cada dB de agudos (medido de medio en medio dB). Con el tono en 0 o en negativo, nada cambia: de fábrica, la
cadena sale bit a bit igual.

- **Se oiría:** con los graves subidos, la música queda unos dB más baja en conjunto, pero con **más grave de verdad** y sin el
  aplastado. Si hace falta más volumen, se sube el amplificador.
- **Otra forma, más fina** (por si la primera se queda corta): unos graves «con margen». El estante de graves solo suma lo que cabe
  bajo el techo en cada momento, como un compresor solo del grave, y el resto de la canción no baja. Cuesta más y hay que afinarlo; mi
  consejo es la primera.
- **Sin interruptor nuevo:** va con los mandos de graves y agudos que ya existen. Se aplicaría al instante, como ellos.
- **Prueba:** la medida de arriba pasa a ser una batería (`allimite` con el tono del local).

### T2. El nivelado «híbrido»: un objetivo común, con el límite como tope

Es lo que propuse el 24-10. Cada canción sube hasta su límite, pero no más allá de un objetivo común de sonoridad. Las muy fuertes
bajan hasta ese objetivo. Así suenan más parejas entre sí, que es para lo que existe un nivelado.

**Hace falta tu tabla para decidir el objetivo.** El `rocola_sonoridad.txt` del 24-10 traía 229 canciones, todas medidas con una
versión anterior y casi todas de menos de un minuto. Cuando el repaso haya medido muchas canciones enteras con la medida nueva (la del
P90), mándame el `rocola_sonoridad.txt` (en zip) y el `rocola_volumen.txt`.

### T3 (RECOMENDADA, Y CONVIENE DECIDIRLA YA). Medir «dónde empieza y acaba de verdad» cada canción, aprovechando la tanda larga

El repaso está volviendo a medir **toda** la biblioteca para el nivelado al límite. El 24-10 le faltaban 47.899 archivos. Ya calcula,
cada 100 ms, la sonoridad de los últimos 3 segundos (es lo que usa para el LRA). Con eso puede guardar, casi sin costo, dos números
más por canción:

- **dónde acaba la música:** el último momento en que la canción está a menos de 20 LU de su sonoridad;
- **dónde empieza:** el primero.

Sirven para T7 y T8. **El momento es ahora.** Cambiar la medida después obligaría a otra vuelta entera por las 118.000 canciones. Hoy
casi ninguna tiene aún la medida nueva, así que entrarían en la vuelta que ya está en marcha.

- **Se oiría:** nada. Solo se mide.
- **Coste:** unos bytes por canción. Un ejecutable anterior seguiría leyendo el archivo sin romperse: así se probó cuando el LRA
  entró en el formato.

### T4. El siseo de las canciones que el nivelado sube mucho

El nivelado al límite puede subir una canción hasta **+24 dB** (el techo de seguridad, `NormMaxBoost`). En la prueba, una canción vieja
y bajita sube +11 dB, y una balada con un golpe suelto, +14 dB. El siseo de la grabación sube lo mismo, y en las partes calladas de una
grabación vieja eso se oye.

«Quitar el siseo» (I2) existe, pero está apagado de fábrica por tu decisión del 13-10. Dos opciones:

- **(a)** I2 actúa sola, aunque su interruptor esté apagado, en las canciones que el nivelado sube más de 10 dB. I2 ya reconoce
  sola si una canción trae siseo y, si no lo trae, no toca ni un bit. Las demás canciones, como ahora.
- **(b)** Bajar el techo de subida, por ejemplo a +15 dB.

Mi consejo es la (a), pero cambia tu decisión sobre I2, así que la decides tú.

### Lo pendiente de antes que conviene cerrar

- **P3** (mover los cortes de 12 y 16 dB): ya no hace falta. Desde P2 el modo del local decide por el LRA.
- **P4** (los videos siguen la fuerza del nivelado): en XP los videos no se miden, así que en el A4 no haría nada.
- **P5** (devolver la sonoridad que quita la claridad): con el nivelado al límite ya no tiene sentido, porque solo daría más trabajo al
  limitador. Lo correcto es lo contrario, T1.
- **A6** (igualar el volumen de los videos): sigue sin poder medirse en XP.

## 2. Nuevas

### T5 (RECOMENDADA). La música, con su propio hilo hasta la tarjeta

**Lo que encontré.** Desde octubre, la cadena la procesa el hilo de la música (`BombaSonido`). Pero quien le entrega cada bloque a la
tarjeta sigue siendo **la pantalla**, en el bucle de mensajes. Es así porque la salida por eventos de NAudio usa un método que no existe
en el .NET del A4. Por eso hay de 750 a 1000 ms de colchón: si la pantalla se para más que eso, **la música se corta**.

Y la pantalla se para más que eso. Del `rocola_error.txt` del A4:

| | |
|---|---|
| 23-10, 13:59 | 2015 ms, dentro del Present de la tarjeta |
| 24-10, 16:41 | 3172 ms, al arrancar |
| 24-10, 21:35 y otras | de 500 a 766 ms, publicando la biblioteca |

Si alguna de esas cae con música sonando, es un corte.

**La propuesta.** Una salida propia que habla directo con Windows (`waveOutOpen` con aviso por evento) y un hilo solo para eso, en
prioridad alta. Ese hilo copia del colchón del hilo de la música a la tarjeta. Usa solo cosas que el .NET del A4 tiene (`WaitOne` con el
segundo parámetro). Si algo falla al abrirla, se usa la salida de ahora.

- **Se oiría:** nada distinto, salvo que **un atoro de la pantalla ya no corta la música**.
- **Y abre dos cosas:**
  - el colchón podría bajar (por ejemplo a unos 300 ms; hay que medirlo en el A4);
  - con eso, **saltar sin chasquido** (T10) deja de chocar con el segundo de espera (octava tanda, A1).
- **Sin interruptor**, porque no cambia el sonido (regla del 15-09). La salida de ahora queda de respaldo.
- **Riesgo:** es la parte que habla con el controlador de sonido de XP. Se prueba aquí con una salida imitada, pero **hay que probarla
  en el A4** antes de darla por buena. Es la más importante para que no haya cortes.

### T6. Quitar el zumbido de la corriente, solo donde lo hay

Hay grabaciones viejas o mal pasadas de vinilo y de cassette con zumbido de 60 Hz (o de 50 Hz si vienen de Europa) y sus armónicos. Con
«Graves del local» arriba y el bajo psicoacústico, ese zumbido se refuerza.

- **Cómo:** el repaso lo mide en las partes calladas de cada canción (la energía justo en 50, 60, 100 y 120 Hz frente a la de al lado).
  Si una canción lo trae, se le ponen unos filtros muy estrechos en esa frecuencia y sus armónicos.
- **Se oiría:** desaparece el «mmm» de fondo. En las canciones sin zumbido no toca ni un bit.
- **Interruptor** en Ctrl+F3 → Sonido → Lo que suena ahora, como S1 y S2. Mi propuesta es encendido de fábrica, porque solo actúa
  con el defecto. Lo decides tú.
- La medida iría junto con T3, en la misma vuelta del repaso.

### T7. Acortar los finales larguísimos (era A5)

Es la de la décima tanda, mejorada con T3. Hay canciones que acaban con 15 o 20 segundos de aplausos o de un desvanecido muy por debajo
del resto. «Saltar silencios» no los salta, porque no están mudos, y en el bar suenan a rocola apagada.

- **Con T3** se sabe dónde acaba la música. Si lo que viene detrás dura más de unos 8 s, se apaga en 3 s y entra la siguiente.
- **Nunca dentro de un disco continuo:** los aplausos entre pistas de un disco en vivo son parte del disco.
- **Interruptor** en Ctrl+F3 → Sonido.

### T8. La mezcla entre canciones de discos distintos (era A3)

Hoy una canción se apaga en 0,5 s y entra la siguiente. Con T3 se puede hacer como un DJ: cuando la canción que suena **se está
desvaneciendo**, la siguiente entra encima durante 2 o 3 s, al nivel que le da el nivelado.

- Las que acaban de golpe siguen como ahora: encima de un final seco, una entrada suena mal.
- Los discos continuos, igual que ahora.
- Durante esos segundos suenan dos cadenas a la vez. Hay que medir en el A4 lo que cuesta.
- **Interruptor**, apagado de fábrica.

### T9. Suavizar las «eses» cuando los agudos están arriba

Con «Agudos del local», la presencia, el realce o el DSEE, las «s» y las «ch» de algunas voces se vuelven chillonas, sobre todo en MP3
de 128 kbps. Un «de-esser» baja solo la banda de 5 a 9 kHz en el instante de la «s» (hasta 4-6 dB, en 1 ms, y suelta en 50 ms). El resto
del brillo queda igual.

- **Interruptor**, apagado de fábrica. Otra posibilidad es que se ponga solo cuando los agudos del local están subidos.
- Prioridad media: hay que oírlo en el bar para saber si molesta.

### T10. Saltar sin chasquido (después de T5)

Hoy SALTAR corta en seco. Con T5 y un colchón de unos 300 ms, la canción se puede apagar en 150 ms dentro de la propia música, sin tocar
el volumen de Windows. SALTAR respondería en unos 0,3 s en vez de al momento, pero sin el «clac».

### En el equipo del bar, sin tocar código

Dos ajustes de Windows XP que mejoran el sonido y que la rocola no cambia sola:

1. **Panel de control → Dispositivos de sonido y audio → Volumen → Opciones avanzadas (de los altavoces) → Rendimiento:**
   - «Calidad de conversión de la frecuencia de muestreo» en **Óptima** (lo más a la derecha; las otras son «Buena» y «Mejor»).
     Es la que usa Windows cuando la tarjeta y la canción van a frecuencias distintas. Si está en «Buena», es la de menos calidad.
   - «Aceleración de hardware» en **Completa**.
2. **En el panel de Realtek:** sin efectos de ambiente y sin el ecualizador del controlador. La rocola ya hace eso, y dos
   ecualizadores seguidos se pisan.

Y que el dispositivo predeterminado sea la **salida analógica de Realtek**, no la de HDMI.

## 3. Lo que miré y no propongo

- **Dither con forma de ruido, o salida de 24 bits:** el ruido de los 16 bits queda a −96 dB de lo más fuerte. En un bar no se oye.
- **Que el limitador suelte «según la música»:** ya se probó al hacerlo (`PicoReal.cs`) y no mejoraba.
- **Compensar graves y agudos con el volumen (ISO 226):** la rocola no sabe a qué volumen está el amplificador. Lo tenía el modo noche,
  que se retiró.
- **Quitar los chasquidos de vinilo:** hay que adivinar qué es un chasquido y qué es un golpe de batería. Equivocarse estropea canciones
  buenas, y es caro para el A4. No por ahora.
- **Un ecualizador de sala con micrófono:** ya descartado. El micrófono oye a la propia rocola.

## 4. Mi orden

1. **T3**, porque solo vale la pena ahora, mientras la tanda larga mide la biblioteca entera. No cambia nada de lo que suena.
2. **T1**: lo que más se oye hoy si alguien sube los graves. Es pequeño y está medido.
3. **T5**: lo más importante para que no haya cortes. Hay que probarlo en el A4.
4. **T6** (con la medida de T3) y **T7**.
5. **T4**, cuando decidas lo de I2.
6. **T10** (después de T5), **T8** y **T9**, cuando quieras afinar más.
7. **T2**, cuando tengas la tabla nueva.

Dime cuáles hago. Los dos ajustes de Windows del equipo se pueden hacer ya, sin esperar a nada.
