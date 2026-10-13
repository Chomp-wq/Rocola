# Más mejoras de audio, para elegir — Modest, 13 de octubre de 2026

Petición del usuario: «Busca más mejoras de audio, ya sea mejorando lo que ya existe o buscando algo nuevo».

Esta ronda es solo de revisar: **no cambia nada del programa.**

## Lo que ya hay, para no repetir

Recorrí la cadena de cada canción en orden (`ArmaCadena`):

1. des-recorte, solo en las canciones que vienen recortadas;
2. quitar el siseo (I2);
3. DSEE, con su modelo por género (I3);
4. nivelado BS.1770, con el nivel del disco en racha y «Subir todo»;
5. realce de agudos a medida;
6. EQ automático (y I1);
7. modo noche con su compensación;
8. grave en mono, que además devuelve el grave de un canal en contrafase;
9. subgrave y estéreo estrecho para las bocinas del billar;
10. claridad, partes suaves y balance;
11. bajo psicoacústico;
12. limitador de pico real;
13. fundido, y conversión a 16 bits con dither.

Por fuera de la cadena: entrada sin golpe, disco continuo sin corte, saltar silencios, convertidor de frecuencia propio
y estimación por tramos para que una canción nueva suene nivelada desde la primera vez.

Todo eso existe y está probado. Lo de abajo es lo que **no** encontré.

## Nuevas

| | Qué | Qué arregla en el bar | Coste |
| --- | --- | --- | --- |
| **S1 (recomendada)** | **Un canal mudo, por los dos.** Si una canción trae un canal en silencio o casi (más de 30 dB por debajo del otro durante un segundo), los dos canales dan el que suena. Entra y sale con un fundido de medio segundo. | Hay copias viejas (de cassette, de vinilo o mal convertidas) con un canal vacío. Con las bocinas repartidas por zonas, en media sala esa canción **no se oye**. Hoy el grave en mono solo rescata el grave; el estéreo estrecho deja ese lado 12 dB más bajo. | Unas sumas por muestra. Se detecta en vivo, sin volver a repasar la biblioteca. |
| **S2 (recomendada)** | **Quitar la continua (DC).** Un paso-alto muy bajo (unos 5 Hz) que solo se pone cuando la canción trae corriente continua medible. | Una grabación con continua: (1) gasta margen del limitador, que la baja de más; (2) al empezar, la entrada de 5 ms convierte esa continua en un **golpe sordo**; (3) empuja los conos sin sonar. | Mínimo. Se mide en vivo. En una canción sin continua no toca ni un bit. |
| **S3** | **Estéreo estrecho «Automático».** Una tercera posición del interruptor que ya existe: estrecha solo las canciones con paneo extremo (instrumentos enteros a un lado), no todas. | Hoy, encendido, estrecha también las mezclas modernas, que no lo necesitan. Las de los 60 y 70 con la batería a un lado y la voz al otro son las que de verdad se pierden en una zona. | Bajo. Se mide en vivo, como la contrafase del grave en mono. |
| **S4** | **«Escuchar sin mejoras»** en Ctrl+F3 → Sonido: mientras se mantiene pulsado (o 10 s), la canción suena sin DSEE, realce, bajo, EQ, siseo ni sala, **al mismo volumen** (el nivelado y el limitador siguen). | Para decidir en el bar si I1, I2, I3 y los demás valen la pena: hoy, comparar obliga a apagar y encender varios interruptores, y el oído se engaña con el volumen. | Ninguno al sonar normal. |
| **S5** | **Qué le está haciendo a esta canción**, en vivo, en la página Sonido: <br>• el volumen del momento; <br>• cuánto baja el limitador; <br>• qué etapas actúan (des-recorte, siseo en dB, DSEE, realce, bajo, EQ, canal mudo, continua). | Hoy se ve lo que está **encendido**, no lo que **hace** en la canción que suena. Sirve para ver si el limitador trabaja de más y por qué una canción suena distinta. | Un repintado por segundo con la ventana abierta. |

## Las que ya estaban propuestas y siguen pendientes

De la décima tanda ([DECIMA_TANDA_MODEST_2026-10-12.md](DECIMA_TANDA_MODEST_2026-10-12.md)); ninguna está hecha:

- **A5. Acortar los finales larguísimos (recomendada).** Canciones que acaban con 15 o 20 s de aplausos o de un
  desvanecido muy por debajo del resto: hoy «Saltar silencios» no las salta, porque no están mudas, y en un bar
  suenan a rocola apagada. Se pasaría a la siguiente con su fundido.
- **A3. Mezcla estilo DJ** de 2 a 4 s entre canciones de discos distintos.
- **A6. Igualar el volumen de los videos.** Hay que medir una vez el reproductor de Windows en el equipo del bar;
  aquí no se puede.

## Lo que miré y no propongo

- **Prioridad «Alta» para la música:** ya va por delante (ver la respuesta anterior).
- **Un ecualizador de sala con micrófono:** necesita un micrófono en el bar, y el micrófono oye a la propia rocola.
- **Saltar sin chasquido:** sigue chocando con la regla de no tocar el volumen de Windows (octava tanda). Dentro de la
  música tardaría un segundo en responder.
- **Nivelar las canciones nuevas desde la primera vez:** ya lo hace la estimación por tramos.

## Cómo se haría

- **Interruptores:** cada una con el suyo en Ctrl+F3 → Sonido, como pide AGENTS.md para lo que cambia cómo suena. Mi
  propuesta: S1 y S2 encendidas de fábrica, porque solo actúan en canciones con ese defecto; las demás, apagadas.
  Decides tú.
- **Pruebas:** con señales armadas a mano:
  - un canal mudo, continua, paneo extremo y mezclas normales;
  - comprobar que en una canción sin el defecto la salida es **idéntica bit a bit**;
  - comprobar que el disco continuo sigue sin corte.
- **No se podría probar aquí:** cómo suena en las bocinas del bar, ni Windows XP.

## Mi orden

1. **S1 y S2:** arreglan defectos que hoy se oyen, cuestan casi nada y no tocan las canciones buenas.
2. **A5:** quita los «huecos» de los finales larguísimos.
3. **S4:** ayuda a decidir todo lo demás con el oído.
4. **S3, S5 y A3:** cuando quieras afinar más.
