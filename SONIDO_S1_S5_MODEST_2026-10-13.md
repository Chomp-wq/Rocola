# Sonido: S1 a S5 — Modest, 13 de octubre de 2026

Petición del usuario: «Haz S1, S2, S3, S4 y S5» (de
[AUDIO_PROPUESTAS_MODEST_2026-10-13.md](AUDIO_PROPUESTAS_MODEST_2026-10-13.md)).

## Dónde está

**Ctrl+F3 → Sonido → «Lo que suena ahora...»** (el botón nuevo junto al subtítulo, arriba). Es una hoja como las del
local y la IA, con dos tarjetas:

- **Lo que suena ahora:** lo que la cadena le hace a la canción que suena (S5) y el botón «Escuchar sin mejoras» (S4).
- **Arreglos de la grabación:** los interruptores de S1, S2 y S3.

## S1. Un canal mudo, por los dos (encendido de fábrica)

Hay copias viejas con un canal vacío; con las bocinas repartidas por zonas, en media sala esa canción no se oye.

- **Cuándo actúa.** Si un canal pasa **1,5 s** más de **30 dB** por debajo del otro mientras el otro suena, los dos dan
  el que suena.
- **Mismo volumen.** Cada canal sale a media potencia: la suma de potencias no cambia y el nivelado sigue valiendo.
- **Entra y sale suave.** Entra en medio segundo. En cuanto el canal mudo vuelve a sonar (a menos de 20 dB del otro),
  se quita en medio segundo.
- **Lo que no toca:** una pausa de los dos no cambia nada, y un instrumento a un lado con su eco en el otro no se toma
  por mudo.

## S2. Quitar la corriente continua (encendido de fábrica)

Algunas grabaciones traen la onda corrida hacia arriba o hacia abajo. Eso gasta margen del limitador, que baja la
canción de más, y empuja los conos sin sonar.

- **Cuándo actúa.** Se sigue la media lenta de cada canal (2 s). Si pasa de **-50 dBFS** durante **3 s**, se resta, y
  entra en un segundo. Si baja de -56 dBFS durante 5 s, se deja de restar.
- **No se oye:** restar esa media es un filtro por debajo de 0,1 Hz.
- **Archivos de un canal:** también se les quita.

## S3. Estéreo estrecho solo si hace falta (apagado de fábrica)

- **Qué hace.** Estrecha solo las canciones con un instrumento entero a un lado, como muchas de los 60 y 70. Las
  mezclas modernas, que ya van casi todo al centro, suenan como vienen.
- **Necesita** «Estéreo al 60 %» encendido (El local y sus bocinas). Si está apagado, este interruptor va en gris y lo
  dice.
- **Cómo decide.** Mira, por encima del corte del grave, la energía de lo que va a los lados frente a la del centro
  (unos 3 s):
  - si lo de los lados pasa del 60 % de lo del centro, estrecha al ancho de «Estéreo al…», y entra en un segundo;
  - si baja del 35 %, vuelve a como viene;
  - en medio no cambia, para que no vaya y venga.

## S4. Escuchar sin mejoras

Un botón para comparar con el oído, en el bar.

- **Qué quita.** Durante **15 s**, la canción suena sin:
  - des-recorte;
  - quitasiseo;
  - DSEE;
  - realce;
  - EQ;
  - bajo;
  - compensación de noche;
  - claridad y partes suaves;
  - los arreglos de S1 y S2.
- **Qué deja.** El volumen no cambia: el nivelado, «Subir todo», la bajada de noche, el balance, las bocinas y el
  limitador siguen igual, para que el oído no se engañe con que «lo fuerte suena mejor».
- **Entra y sale suave.** Cada parte entra y sale en su rampa.
- **Cómo se quita.** Al volver a pulsar, al pasar los 15 s o al cerrar la ventana. **No se guarda.**

## S5. Lo que le hace a esta canción

Cada medio segundo, con la hoja a la vista, enseña:

- el nivel que sale y cuánto baja el limitador;
- qué está actuando de verdad en la canción que suena: des-recorte, siseo (en dB), DSEE, realce, EQ, bajo, ancho del
  estéreo, canal mudo y continua.

No cambia el sonido: el limitador solo apunta, y su salida es la misma bit a bit.

## Lo que cuesta

La etapa de S1 y S2 gasta unos **1,2 ms por segundo de música** en esta máquina (un 0,1 % de un núcleo). En el equipo
del bar será algo más; no se midió allí.

## Cómo se comprobó

- **`arreglos`** (nueva; 34 comprobaciones; pasa con y sin tarjeta). Pasa señales armadas a mano por las etapas de
  verdad.
  - **S1:**
    - el primer 1,4 s sale tal cual;
    - a los 2 s los dos canales dan lo mismo y la suma de potencias no cambia (error menor al 1 %);
    - tras una pausa de los dos sigue arreglado;
    - cuando el canal vuelve, sale otra vez tal cual;
    - un eco a -20 dB no se toca.
  - **S2:**
    - con -40 dBFS de continua, a los 6 s queda por debajo de -56 dBFS;
    - lo único que cambia es la continua;
    - con -60 dBFS no se toca;
    - también en mono.
  - **Una canción normal sale bit a bit**, y leer en trozos impares da lo mismo.
  - **El paso directo (S4)** vuelve a lo grabado bit a bit, en rampa y sin escalón.
  - **El disco continuo** sigue sin salto.
  - **S3:**
    - paneo extremo, al 60 %;
    - mezcla al centro, bit a bit como con el estrecho apagado;
    - con el automático apagado, el estrecho de siempre bit a bit;
    - también sin cruce.
  - **Lo demás:**
    - el limitador apunta lo que baja;
    - S4 sobre una cadena;
    - la configuración y la cadena.
- **`ventanaf3`:** la hoja nueva, con sus tres interruptores y el botón, cabe y no pisa nada, con dos letras.
- **`ajustesvivos`:** S3 en gris sin el estrecho.
- **Siguen pasando:** `subgrave`, `gravemono`, `salalocal`, `sinpausa`, `decimatanda` y `mejorasia`.
- **Tanda completa (`pruebas/todo.sh`):** los mismos problemas de siempre, fallo por fallo; ninguno nuevo.
- **Dibujo por CPU:** los 14 métodos, idénticos.

**No probado aquí:**

- **Cómo suena en las bocinas del bar.** Los umbrales (30 dB, -50 dBFS, 60 % / 35 %) se eligieron con señales
  armadas, no con su biblioteca; si alguno salta de más o de menos, se ajusta.
- **Cuánto gasta en el equipo del bar.**
- **Windows XP.**
