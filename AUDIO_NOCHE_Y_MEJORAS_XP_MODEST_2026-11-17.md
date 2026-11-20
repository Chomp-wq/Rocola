# Rocola de XP: el modo noche y mejoras de audio (17-11-2026)

Pedido del dueño: «En la rocola XP para la rama modest, regresa el modo noche, con un slider de intensidad, desde 70 hasta 90%, y busca
mejoras de audio». Solo la rocola de Windows XP (`Rocola/`, .NET 3.5). Nada de esto está probado en el A4.

## 1. El modo noche: ya estaba de vuelta

Se había hecho el 7-11-2026 (commit `8dfa1ad`, decisión en AGENTS.md) y sigue en la rama:

- De **23 a 7 h** (`noche_desde=`, `noche_hasta=`) la rocola suena al **`noche_pct=` %** de lo que se oye: **deslizador del 70 al 90 %**,
  80 de fábrica (70 % = −5,1 dB; 80 % = −3,2 dB; 90 % = −1,5 dB). Desde el 20-11-2026, de 50 a 90 % (sección 4 ter).
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

## 4. «Probar» se queda hasta otro toque, y el ecualizador del dueño (parte del sonido)

**«Probar»** (pedido: «Que se quede hasta que lo vuelva a pulsar»): un toque pone al momento (1,5 s) el nivel de la noche con su grave;
el botón dice «Quitar prueba» y la prueba sigue aunque se cierre Ctrl+F3, hasta otro toque. Por seguridad se quita sola a los 30 min y no
sobrevive a un reinicio. Batería `noche` en verde.

**Ecualizador del dueño** (pedido: «un ecualizador para poder ajustar yo por completo el sonido de la rocola [...] que se conecte
perfectamente con la cadena de sonido (normal y del modo billar)»). Está en Ctrl+F3 → Sonido → Ajustes del sonido → «Ecualizador...»: 10 deslizadores, «Todo en 0», qué memoria se está usando, cuánto baja el resto (el margen) y un dibujo con lo que ya hace la rocola (azul), tu curva y la suma (blanco). «Dejar como de fábrica» pone las dos memorias en 0.
- 10 bandas (31, 63, 125, 250, 500 Hz, 1, 2, 4, 8 y 16 kHz), de −12 a +12 dB en medios dB. Las de los extremos son estantes; las otras,
  campanas.
- Va al final de la cadena, justo antes del limitador: actúa sobre lo que la rocola ya hizo (el local, el billar, el modo noche), así que lo
  que mueve el dueño se suma a lo automático, no lo pisa.
- **Dos memorias**: una para el sonido normal y otra para el modo billar (`eq_dueno=`, `eq_dueno_billar=`). Al prender o apagar el modo
  billar cambia sola de curva, con rampa. «Escuchar sin mejoras» y la comparación a ciegas no la quitan.
- **Preganancia automática**: si se sube una banda, todo baja lo mismo para que el limitador no apriete más; por eso subir una banda se oye
  como «bajar las demás».
- Cambios en vivo con rampa (12 dB en 50 ms), sin chasquido; en un disco continuo sigue sin escalón.
- Todo en 0 (de fábrica): la cadena sale bit a bit como antes (comparada contra el ejecutable anterior: 16 de 16 iguales).
- Batería nueva `ecualizador` y 16 de la cadena en verde. Nada oído ni probado en el A4.

### E1 y E3 (elegidas por el dueño)

- **E1, «Sin ecualizador»**: botón junto a «Todo en 0». Quita tu curva 15 s para comparar (dice «Ponerlo (N s)»); vuelve con otro
  toque, al mover algo, al salir de la hoja o al cerrar Ctrl+F3. No toca lo guardado.
- **E3, el margen según la canción**: con el tono medido de cada canción (y su EQ automático) se calcula cuánto sube de verdad con tu
  curva y se baja eso, más un 25 % hacia lo máximo por seguridad; el limitador sigue detrás. Con +6 dB en 63 Hz: una canción con
  graves fuertes baja 2,6 dB, una sin graves 1,6 dB, una casi toda de graves 4,6 dB (antes, todas 6 dB). Sin tono medido, lo máximo
  como antes. En el disco continuo el cambio entre pistas va a 2 dB por segundo. Pruebas con huellas inventadas; nada oído.
  Si en el bar se oyen los bombos aplastados con el bajo subido, se puede subir ese 25 %.

## 4 bis. El modo billar sin brincos y la limpieza de Ctrl+F3 (18-11-2026)

- Con «Roundabout» el volumen daba brincos con el modo billar. El modo **ya no pone** las cinco etapas que suben o bajan el volumen
  solas: presencia, partes suaves, retumbe dinámico, ataque más claro y agudos ásperos. Van con su interruptor (apagados de fábrica) y ya
  no salen en gris. Se queda lo que no mueve el volumen: corte del bajo profundo, estéreo estrecho si hace falta, claridad, EQ del
  billar, realce, bajo, volumen parejo, limitador que respira y eses. Batería nueva `billarparejo`: con el modo billar, un tono que pasa
  de -30 a -6 dBFS sale con la misma ganancia (3,76 dB en los tres tramos). Con el modo apagado, bit a bit como antes.
- Se revisaron los 161 mandos de Ctrl+F3: ninguno estaba muerto. Por elección del dueño se quitaron el botón «Presencia nueva o
  antes» (ya no tenía uso) y el interruptor «Modelos del DSEE por género» (`dsee_genero=` se ignora).

- Después, cuatro mejoras más del modo billar (elegidas por el dueño):
  - tampoco pone la claridad (−2,5 dB a 300 Hz) ni suaviza las eses: esa zona la decide su ecualizador;
  - el bajo psicoacústico va con su barra y su «Auto» (ya no en gris; de fábrica suena igual);
  - el volumen parejo aprendido empieza de cero una sola vez (lo viejo se aparta a `rocola_billar_nivel.antes-2026-11-18.txt`), porque
    se había aprendido con la presencia puesta;
  - «Lo que suena ahora» dice «El limitador baja: X dB (10 s: Y dB)», lo más que apretó en los últimos 10 s.
  Batería nueva `billar18`; suite completa: 383 bien y `tironescambio`, que ya fallaba antes.

## 4 ter. El deslizador del modo noche, de 50 a 90 % (20-11-2026)

Pedido del dueño: «El modo noche ahora tendrá el slider desde 50 hasta 90%». (La otra mitad del pedido, buscar qué más se puede bajar de
noche —frecuencias— para no molestar a los vecinos, va aparte y como propuesta: no cambia nada del sonido sin que el dueño elija.)

- El deslizador de Ctrl+F3 → Sonido va de **50 a 90 %** de lo que se oye (antes de 70 a 90), con la misma cuenta, 10·log2(pct/100):
  50 % = **−10 dB**, 60 % = −7,4 dB, 70 % = −5,1 dB, 80 % = −3,2 dB, 90 % = −1,5 dB. 80 de fábrica.
- Lo guardado se conserva: un `noche_pct=` entre 50 y 90 se lee tal cual; fuera se acota (40 → 50, 95 → 90).
- **Lo que dependía del mínimo: la rampa.** La de 20 s (la subida de las 7, mover el deslizador o el interruptor) y la de «Probar»
  (1,5 s) recorrían medio volumen en ese tiempo, y de 70 a 90 % la noche baja menos que eso. Al 50 % baja de 1 a 0,316: con el paso de
  antes habrían sido 27,4 s y 2,1 s.
  - Ahora el paso es el mayor entre medio volumen y lo que baja la noche (`AvanzaRampa`).
  - De 66 a 90 % es el mismo paso de antes, bit a bit; de 50 a 65 % la noche entera sigue en 20 s y en 1,5 s.
  - La subida de las 7, al 50 %, tarda unos 23 s (al 80 %, 20 s; con el paso de antes habrían sido 28,5 s).
- Lo demás no dependía del mínimo y no cambió:
  - la bajada de las 22 h, en una hora pareja en dB: al 50 %, −1,67 dB a los 10 min, −3,33 a los 20 y −10 a los 60 (−0,17 dB por minuto);
  - los videos, con la perilla del reproductor al porcentaje: al 50 %, la perilla a la mitad (a la media hora, al 75 %);
  - los graves de noche (N1), que bajan lo mismo con cualquier porcentaje;
  - el limitador, que va después y solo trabaja menos.
- En la ventana, AvPag y RePag sobre el deslizador saltan de 10 en 10 (antes de 2 en 2): es la regla de siempre de los deslizadores de
  40 puntos o más. Con el mismo 80 % el botón del deslizador queda más a la derecha (a tres cuartos del riel): **conviene verlo en pantalla**.
- Para saber (ya era así, no cambió): la perilla del reproductor de los videos es de amplitud en rocola-xp (el volumen cúbico de mpv con la
  cuenta de XP), así que al 50 % los videos bajan unos −6 dB y la música −10 dB (al 80 %, −1,9 y −3,2). En XP depende de la perilla del
  reproductor de Windows Media, que no se midió. Igualarlos sería otro cambio.
- Batería `noche`: 86 de 86 (antes 65): el rango, la rampa al 50 % (20,02 s y 1 504 ms) y la de antes de 66 a 90 % bit a bit, la
  configuración, y una sección 9 nueva con todo al 50 %. Contra la rocola de antes, la misma batería da 12 fallos. También en verde
  `ajustes`, `ajustesvivos`, `ventanaf3`, `pestanas`, `sala`, `salalocal`, `compatvieja`, `videos`, `detalles`, `moneda`, `arreglos` y
  `cadenaentera`. Nada oído ni probado en el A4.

## 5. Propuestas que necesitan tu decisión (no se hizo nada)

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
