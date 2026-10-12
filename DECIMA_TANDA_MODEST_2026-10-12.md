# Décima tanda: fallos y mejoras de audio — Modest, 12 de octubre de 2026

Petición del usuario: «Busca más fallos y mejoras de audio».

## Arreglado: las canciones ya no arrancan con un golpe seco

**Qué pasaba.** Cada canción tenía fundido de salida (0,5 s al final), pero ninguno al empezar. Si la primera
muestra de una canción no es cero, la bocina salta de cero a donde va la onda en una sola muestra y se oye un golpe
seco («clic» o «pop»). Pasa en dos casos:

- **canciones que empiezan con sonido:** temas cortados de un disco en vivo o de uno continuo, o que empiezan con un
  golpe. Medido con la cadena de verdad: una canción que empieza en su pico pasaba de 0 a 0,586 en una muestra;
- **al retomar a media canción**, cuando vuelve el aparato de sonido: la música entraba de golpe a todo volumen.

**Ahora:**

- **Toda canción entra con una curva de 5 ms.** Es lo que hace cualquier editor de audio en un corte: quita el golpe
  y no se oye en el ataque. Una canción que empieza en silencio queda exactamente igual.
- **Al retomar a media canción**, la música entra en 0,3 s, en vez de a todo volumen de golpe.
- **Las pistas seguidas de un mismo disco** que ya se empalmaban sin corte (disco continuo, en vivo) no llevan
  entrada: la onda sigue sin cortarse, como antes.
- La curva empieza donde empieza la música, no en los primeros instantes de silencio que la cadena pone antes.
- Si el fundido está apagado en la configuración (`fundido=0`, corte seco), tampoco hay entrada, como antes.

No lleva interruptor: es una corrección que no cambia cómo suena la música.

## Revisado y bien

- **La cadena de sonido entera, con todo encendido** a la vez, se pasó a todas las frecuencias en mono y en estéreo:
  - 8, 11, 16, 22, 32, 44,1, 48 y 96 kHz (los MP3 viejos a 22 kHz son comunes en una rocola);
  - todo encendido: nivelado, Subir TODO, realce, graves, des-recorte, DSEE, modo noche, grave en mono, claridad,
    partes suaves, balance, quitar subgrave y estéreo estrecho.

  En todas:
  - nada roto (NaN o infinitos);
  - nada por encima del techo del limitador;
  - los silencios quedan limpios (menos de -100 dB);
  - el nivel, parejo entre frecuencias.

  Antes solo se probaba a 44,1 kHz en estéreo.
- **El volumen de los videos** sigue al modo noche y al nivelado.
- **La prueba de bocinas** entra y sale con su rampa de 40 ms: sin golpe.
- **«Dejar como de fábrica»** en Ctrl+F3 devuelve todos los ajustes de sonido, también los nuevos.

## Para que elijas: mejoras de audio (ninguna está hecha)

Cada una cambia cómo suena la rocola, así que llevaría su interruptor en Ctrl+F3 → Sonido, apagado de fábrica:

- **A3. Mezcla entre canciones (estilo DJ).** Ya propuesta antes. Hoy una canción se apaga en 0,5 s y entra la
  siguiente. Una mezcla de 2 a 4 s entre canciones de discos distintos da ambiente de fiesta continua. Las pistas
  seguidas de un mismo disco seguirían empalmándose sin corte.
- **A5. Acortar los finales que se apagan muy despacio.** «Saltar silencios» solo salta lo que de verdad está mudo
  (por debajo de -60 dBFS). Hay canciones que acaban con 15 o 20 s de aplausos, ruido o un desvanecido larguísimo
  muy por debajo del resto de la canción: en un bar eso suena a rocola apagada. Detectarlo en la medida que ya se
  hace de cada canción y pasar a la siguiente con su fundido.
- **A6. Igualar de verdad el volumen de los videos.** El volumen de los videos se pone al reproductor de Windows en
  una escala de 0 a 100, y la rocola la trata como si fuera pareja. Si el reproductor de XP la usa en curva (es lo
  probable), los videos bajitos o fuertes quedan algo desparejos frente a las canciones. Habría que medirlo una vez
  en el equipo del bar; aquí no hay Windows Media Player.

## Cómo se comprobó

- **Prueba automática nueva `decimatanda`:** 24 comprobaciones, con tarjeta y sin ella.
  - La entrada sube en curva, sin escalón, en mono y estéreo y con bloques de 7, 64 y 4096 muestras.
  - Los primeros instantes de silencio de la cadena no se tocan.
  - Pasada la entrada, la canción sale tal cual, bit a bit.
  - Sin entrada (disco continuo), tal cual desde la primera muestra.
  - El fundido de salida, igual que antes.
  - En la cadena de verdad:
    - una canción que empieza en su pico ya no arranca de golpe: primera muestra por debajo de 0,02, cuando antes
      era 0,586;
    - retomada, entra en 0,3 s.

  Contra el ejecutable anterior falla.
- **Puesta al día:** `sinpausa` ahora arma la canción siguiente sin entrada, como el relevo de verdad en un disco
  continuo, y comprueba que el relevo se la quita. Sigue exigiendo el empalme bit a bit.
- **Siguen pasando:**
  - `transiciones`, `salida`, `todojunto`, `audiosuave`, `silencios`, `suenavivo` y `videoaudio`;
  - `gravemono`, `noche`, `salalocal`, `subgrave`, `picoreal`, `audioestereo` y `octavatanda`;
  - `wavdirecto`, `sonoridad`, `realce`, `bomba` y `cierrebomba`.
- **Tanda completa (`pruebas/todo.sh`):** 26 problemas, los mismos 26 de siempre (baterías que ya fallaban en este
  Linux); ninguno nuevo. `decimatanda` pasa en los dos perfiles.
- **Dibujo por CPU:** los 14 métodos congelados, idénticos.
- **No comprobado:** oírlo en las bocinas del bar. Son señales sintéticas: miden la cuenta, no el oído.
