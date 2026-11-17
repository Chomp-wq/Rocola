# Rocola de XP: el modo noche y mejoras de audio (17-11-2026)

Pedido del dueño: «En la rocola XP para la rama modest, regresa el modo noche, con un slider de intensidad, desde 70 hasta 90%, y busca
mejoras de audio». Solo la rocola de Windows XP (`Rocola/`, .NET 3.5). Nada de esto está probado en el A4.

## 1. El modo noche: ya estaba de vuelta

Se había hecho el 7-11-2026 (commit `8dfa1ad`, decisión en AGENTS.md) y sigue en la rama:

- De **23 a 7 h** (`noche_desde=`, `noche_hasta=`) la rocola suena al **`noche_pct=` %** de lo que se oye: **deslizador del 70 al 90 %**,
  80 de fábrica (70 % = −5,1 dB; 80 % = −3,2 dB; 90 % = −1,5 dB).
- Interruptor `noche=`, **encendido de fábrica**; baja y sube en 20 s, sin escalón, con aviso al empezar y al acabar.
- Canciones (la que suena, la preparada y las nuevas) y videos.
- **«Probar»** en Ctrl+F3 → Sonido (Volumen al límite) lo pone 15 s al momento y vuelve solo.

Revisado hoy (`Form1.Sonido.cs`, `Form1.Ajustes.cs`, `Form1.Datos.cs`):
- el cruce de la medianoche está bien;
- la rampa la mueve el reloj de animación, que solo se la salta durante el arranque;
- el factor entra antes del limitador.

La batería `noche` pasa en verde.

## 2. Dos arreglos automáticos (sin interruptor: con todo en orden no cambian ni un bit)

### Volver de un corte sin chasquido (`SalidaPropia.cs`)

**El fallo.** Si el equipo se atasca más que el colchón de la salida, la tarjeta se queda sin música (un hueco). Cuando la música
vuelve, entraba a media onda: un chasquido encima del hueco.

**Ahora.** El primer bloque tras el corte entra con una media curva de coseno de 10 ms.
- Funciona en PCM de 16, 24 y 32 bits y en coma flotante de 32; un formato que no se reconozca no se toca.
- El hueco en sí no se puede evitar, porque ya sonó.
- Una pausa no cuenta como corte.

**Probado** (batería `salidapropia`, con el Windows imitado y un tono de 1 kHz que se atasca 900 ms con un colchón de 300 ms):
- vuelve desde 0,002 hasta entero en 10 ms, en 16 bits y en coma flotante;
- sin atasco, lo que sonó es idéntico byte a byte;
- PCM de 24 y 32 bits: escrito, pero sin prueba propia.

### Los «MP3» que no lo son (`NoEsMp3.cs`, `OpenAudio` y `LectorMedida`)

**El fallo.** Un M4A, OGG, FLAC, WMA o WAV con el nombre cambiado a `.mp3` podía sonar como **ruido casi al máximo** por las bocinas.
- El lector de MP3 busca cuadros en cualquier cosa.
- Al fallar, la rocola caía a la reconstrucción de los MP3 de frecuencias mezcladas (`BuildCleanMp3`), que copia los cuadros falsos y
  los toca.
- En la rocola de Linux se midió el golpe: 0,83 de la escala.
- Y el repaso medía basura.

**Ahora.** Antes de abrir un `.mp3` se miran sus primeros bytes, saltando la etiqueta ID3 si la hay. Si es de otro formato, se rechaza
por el camino que ya existía para las canciones que no abren (`AudioStartFailed`):
- se devuelve el crédito;
- la canción queda como dañada (no se vuelve a cobrar mientras el archivo no cambie);
- se anota en `rocola_incompatibles.txt` con el motivo, para convertirla;
- el repaso tampoco la mide.

Los MP3 de verdad y el MP3 metido en un WAV (formato 0x55, que el lector sí toca bien) no cambian.

**Probado** con la batería nueva `nomp3`, sobre cabeceras armadas por la prueba:
- se reconocen M4A, OGG, FLAC, WMA y WAV, con y sin etiqueta ID3;
- un MP3 de verdad, el MP3 dentro de un WAV, un archivo vacío o que no existe pasan como siempre.

Pasan también `aperturaaudio`, `medidas`, `mp3mezclado`, `mp3ligero`, `wavdirecto` y las demás de la apertura. No se probó con un M4A
real ni en Windows (el descompresor ACM no existe en Linux).

## 3. El modo noche, a las 22 h y en una hora (pedido del dueño del mismo día)

«N1 me interesa, y haz que el modo noche empiece desde las 10, pero que vaya bajando extremadamente lento el volumen hasta llegar al
objetivo, para que así los clientes no lo noten, que se vaya bajando tan lento que tarde una hora en llegar al objetivo.»

- **Empieza a las 22 h** (`noche_desde=` 22 de fábrica). Las rocolas que tenían guardado el 23 de antes pasan a 22 una sola vez (marca
  `noche_desde_22=1`); otra hora puesta a mano se respeta.
- **La bajada tarda una hora** y va **pareja en dB**: el mismo número de dB por minuto (al 80 %, −0,54 dB a los 10 min, −1,07 a los 20 y
  −3,22 a los 60), que es como el oído percibe el volumen.
  - **Ya no sale el aviso en pantalla** al empezar.
  - A las 7 sube en 20 s, como antes.
  - «Probar» sigue siendo inmediato (1,5 s).
  - Mover el deslizador o el interruptor va en 20 s, para que el dueño oiga su ajuste.
  - Una rocola que se prende ya de noche arranca en el nivel de la noche.
- **N1, de noche menos grave hacia afuera** (`noche_graves=`, 4 dB de fábrica, de 0 = apagado a 6): el escalón de graves de 120 Hz (el
  filtro de «Graves del local», `SalaLocal.cs`) baja esos dB con el mismo avance de la hora (la mitad a la media hora), con decimales y
  sin escalones.
  - Medido con 4 dB: 60 Hz baja 3,76 dB; 1 kHz, 0,00 dB.
  - De día, la etapa sale bit a bit como antes.
  - Vale con el modo billar y con «Escuchar sin mejoras» (como el volumen de la noche).
  - Mando «Graves de noche» en Ctrl+F3 → Sonido, debajo del modo noche, en gris con el modo apagado. La tarjeta queda justa (el mando
    acaba a 8 px del borde): **conviene verla en pantalla**.
- Si el equipo se detiene más de 5 s entre vueltas, la bajada se pausa ese rato, en vez de dar un salto.
- Baterías `noche` (secciones nuevas 7 y 8), `sala`, `ajustes` y otras 20 de la cadena y la ventana, en verde.

## 4. Propuestas que necesitan tu decisión (no se hizo nada)

- ~~N1~~ hecho (sección 3). **N1. «De noche, menos grave hacia afuera».** Lo que más sale del local y atraviesa paredes es el grave, no el volumen general. La
  propuesta:
  - de noche, además del porcentaje, el escalón de graves de 120 Hz (el mismo de «Graves del local») bajaría unos dB (por ejemplo 4),
    con la misma rampa de 20 s;
  - adentro casi no se nota y afuera se oye bastante menos;
  - interruptor propio, con un deslizador de cuántos dB.
- **N2. «Otro horario el fin de semana».** Una hora de empezar distinta para viernes y sábado (por ejemplo, la 1 de la mañana) y la de
  siempre entre semana.
- **Saltar sin chasquido (T10)** sigue pendiente, como se acordó: el colchón de XP es de 750 a 1000 ms y una bajada suave al saltar se
  oiría un segundo tarde. Antes de achicar ese colchón hay que ver los `[Cortes de sonido]` de `rocola_error.txt` en el A4.
