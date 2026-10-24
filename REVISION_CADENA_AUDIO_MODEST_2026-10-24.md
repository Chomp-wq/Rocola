# Revisión de la cadena de audio completa — 24 de octubre de 2026

Rama Modest (`claude/modest-carson-fa2v2n`). El dueño pidió revisar la cadena de audio entera, asegurarse de que todo funcione bien
y ver qué se puede mejorar.

Resultado: dos defectos latentes arreglados y tres mejoras propuestas, pendientes de decisión.

- Los dos defectos no sonaban con la salida de hoy, que siempre pide cuadros enteros. Están arreglados sin cambiar un bit de lo que
  suena.
- Las tres mejoras sí cambian el sonido. Por eso esperan la decisión del dueño.

Todo está medido con señales sintéticas en Mono/Linux. **Nada se oyó en el billar**, y los MP3 no se pudieron probar: en Linux no
hay descompresor del sistema (ACM).

## 1. La cadena, en orden

Lo que arma `ArmaCadena` (Form1.Sonido.cs) para cada canción:

| # | Etapa | De fábrica | Retardo |
|---|---|---|---|
| 1 | Lector (`FileAudioSource`; sin el relleno del MP3 y sin los silencios largos) | siempre | — |
| 2 | `FuenteOriginal`: mide la original (nivelado, EQ) y limpia NaN | siempre | — |
| 3 | Arreglos de la grabación S1/S2 (canal mudo, continua) | encendidos, solo actúan con el defecto | — |
| 4 | Des-recorte | encendido, solo en canciones recortadas | 68 cuadros |
| 5 | Quitar el siseo (I2) | apagado | 11,6 ms |
| 6 | DSEE (reconstructor de agudos) | apagado (y solo en equipos que lo permiten) | ninguno: lee por delante |
| 7 | `LevelTap`: nivelado al límite, Subir TODO, realce 2×, EQ automático, medida | encendido | 31 cuadros |
| 8 | Grave en mono (120 Hz), estéreo estrecho, subgrave | grave en mono **encendido**; los otros apagados | — |
| 9 | El local: claridad, partes suaves, balance, graves, agudos | apagado (balance centrado) | — |
| 10 | Presencia en sala ruidosa | apagada | — |
| 11 | Bajo psicoacústico | automático | — |
| 12 | Limitador de pico real (−1,1 dBTP, 3 ms) | siempre | 3 ms |
| 13 | Fundido: entrada de 5 ms y salida de 0,5 s | siempre | — |
| 14 | (Con `frecuencia_salida=`) conversión sinc + otro limitador | apagado | — |
| 15 | Hilo de la música (`BombaSonido`) → relevo sin pausa (`Encadenado`) → 16 bits con dither TPDF (XP) | siempre | — |

## 2. Lo que se comprobó

1. **La cadena entera** (OpenAudio + ArmaCadena), de fábrica y con TODO encendido. Formatos:
   - estéreo flotante a 44,1 kHz, 16 bits a 48 kHz y 24 bits a 96 kHz;
   - mono de 16 bits a 22,05 kHz;
   - 6 canales de 24 bits a 48 kHz.

   En cada caso se leyó de tres maneras (bloques fijos, tamaños al azar, de a una muestra) y se miró:
   - que dé los mismos bits;
   - que no salga nada roto ni por encima del techo;
   - la longitud;
   - el retardo declarado contra el de verdad, con un clic.
2. **Cada etapa sola**, con lecturas de 1 a 7 muestras, sueltas y entre bloques, en mono, estéreo y 6 canales.
3. **La salida de 16 bits**: dither TPDF de ±1 LSB, redondeo y recorte. Correcta: dos uniformes restados, independiente por canal.
4. **El conversor del relevo y de `frecuencia_salida`**: sinc de 192 coeficientes con ventana Blackman-Harris, corte al 0,47 de la
   tasa menor y lecturas por cuadros enteros. Correcto.
5. **Los cambios en vivo**: ganancia y realce en rampa de 20 ms, EQ publicado de una vez y volumen del lector en 1. Correctos.
6. **El limitador dentro de la cadena real**, con el nivelado al límite. Esto da los hallazgos de la sección 4.

Lo que salió bien, en la cadena entera y en todos los formatos:

- Leída a tamaños al azar, da los mismos bits que en bloques.
- Nada roto, y el pico de muestra no pasa del techo (−1,11 dBFS).
- Sale la canción entera más su cebado, ni una muestra de menos.
- El retardo declarado es el de verdad. Es lo que descarta el relevo sin pausa: 243 cuadros de fábrica y 755 con todo encendido a
  44,1 kHz; 255 y 767 a 48 kHz.

## 3. Defectos arreglados

Los dos con la misma causa: contaban por cuadros enteros (`n / ch`). **La salida de la rocola siempre pide cuadros enteros**: el hilo
de la música lee trozos de 25 ms, y el relevo pasa el resto de una lectura entera. Así que en el bar no sonaban.

Pero la cadena tiene que valer se lea como se lea, igual que el limitador desde el 22-10-2026. Y una lectura partida, si alguna vez
llegaba, se oía.

1. **El fundido** (`FadeProvider`). Con una lectura que dejaba un cuadro a medias, la posición no avanzaba y lo siguiente iba con los
   canales corridos una muestra. Leída de a una muestra durante 1 s:
   - la entrada de 5 ms caía **en medio de la canción**: un hueco de hasta −6 dB en la música;
   - el fundido de salida llegaba tarde.
2. **El des-recorte** (`DeclipProvider`). Las muestras sueltas de un cuadro partido salían sin retardo ni reparar, y lo siguiente iba
   con los canales corridos. Al vaciar su retardo al final, una lectura de menos de un cuadro devolvía 0, que quiere decir «se
   acabó»: **se perdían los últimos 68 cuadros** de la canción.

**El arreglo.** Las dos etapas saben de qué canal es la muestra siguiente. Con cuadros enteros siguen el camino de siempre, y lo que
sale es idéntico bit a bit (`compara-cadena` contra la versión anterior: 16 de 16 combinaciones). Si una lectura deja un cuadro a
medias, siguen muestra a muestra con las mismas cuentas. El vaciado del des-recorte va muestra a muestra: son copias, sin cuentas.

**La prueba.** Nueva batería `cadenaentera`, con 77 comprobaciones en 19 s. Con la versión anterior da 12 fallos.

## 4. Mejoras propuestas (cambian el sonido: pendientes de decisión)

### M1. El grave en mono sube los picos de las canciones masterizadas modernas

**Qué pasa.** El grave en mono está encendido de fábrica (`grave_mono=120`, sin interruptor) para todo el estéreo. Usa un cruce
Linkwitz-Riley:

- el **lado** pierde el grave;
- el **medio** pasa por un paso-todo, para seguir en fase con el lado.

El paso-todo no cambia el nivel de ninguna frecuencia, pero gira la fase del grave. En una onda aplanada por el limitador de un
masterizado moderno (el bombo pegado al techo), ese giro **sube los picos**.

Medido con una mezcla completa (bombo, bajo, caja, platillos, colchón, voz y guitarra) pasada por un limitador de masterizado:

| Mezcla | Pico antes | Después del grave en mono |
|---|---|---|
| sin masterizar | −1,0 dBFS | −1,4 dBFS (−0,4 dB) |
| masterizada, 3 dB de limitador | −0,3 dBFS | +1,2 dBFS (**+1,5 dB**) |
| masterizada, 6 dB de limitador | −0,3 dBFS | +1,8 dBFS (**+2,1 dB**) |
| masterizada, 9 y 12 dB de limitador | −0,3 dBFS | +2,4 dBFS (**+2,7 dB**) |
| vieja, con la batería y el bajo a un lado | −1,0 dBFS | −2,3 dBFS (−1,3 dB) |

Esos picos los quita después el limitador de la rocola. En la cadena de fábrica, con el nivelado al límite y el masterizado de 9 dB:

- **con** el grave en mono: quita más de 1 dB el 46 % del tiempo, y como mucho 3,8 dB;
- **sin** el grave en mono: el 32 % del tiempo, y como mucho 1,9 dB.

Es decir: el grave en mono hace que el limitador aplaste de más **justo las canciones que ya vienen aplastadas**. A las viejas, que
son para las que se hizo, no les sube nada.

**Opciones:**

- **(a) Recomendada: solo si hace falta.** La tanda larga mediría, en cada canción, cuánto grave lleva el lado frente al medio (un
  número más en `rocola_sonoridad.txt`). Una canción con el grave ya en mono (casi todas las modernas: se masterizan así) se salta el
  cruce y sale bit a bit, sin giro de fase ni picos de más. Una con el grave a un lado o al revés sigue como hoy. Mientras una canción
  no tenga esa medida, va como hoy. Cuesta otra pasada de la tanda larga en segundo plano.
- **(b) Cruce de fase lineal.** El grave del lado se quitaría con un filtro FIR, y el medio solo se retrasaría, sin girarle la fase.
  Exacto en todas las canciones. Cuesta más cálculo y unos 15–50 ms más de retardo, y hay que rehacer una etapa con muchas funciones
  (estéreo estrecho, subgrave, canal al revés, disco continuo).
- (c) Dejarlo como está.

### M2. Las canciones mono no llevan el balance ni el relevo sin pausa con las estéreo

**Qué pasa.** Un archivo mono sale mono a la tarjeta, y Windows manda lo mismo a las dos bocinas. Eso tiene dos efectos:

- **El balance del local no se aplica** a esas canciones (`SalaLocal` solo lo pone con dos canales). Si el balance compensa las
  bocinas del billar, las canciones mono (grabaciones viejas, por ejemplo) no lo llevan.
- Entre una canción mono y una estéreo **no hay relevo sin pausa**: distinta disposición, se cierra y reabre la salida.

No se pudo saber cuántas canciones mono hay en la biblioteca: las tablas no guardan los canales.

**Propuesta.** Abrir los archivos mono como estéreo con los dos canales iguales, que es lo mismo que hace Windows al mandarlos a dos
bocinas. Así llevan el balance y entran sin pausa detrás de una estéreo. El nivelado no cambia: se mide por canal. A cambio, esas
canciones pasan dos canales por la cadena en vez de uno, así que cuestan el doble.

### M3. El realce y el bajo de fábrica hacen trabajar al limitador en las canciones aplastadas

El realce al 35 % y el bajo automático suben un poco los picos. En una canción ya aplastada, cada uno lleva al limitador a quitar más
de 1 dB entre el 10 % y el 29 % del tiempo (con el masterizado de 9 dB). Son reducciones suaves: menos de 2 dB.

Esto **no lo empeoró el nivelado al límite**. Con la regla de antes la cadena de fábrica también limitaba ahí: 39 % y 38 % del tiempo
con los masterizados de 6 y 9 dB, frente a 18 % y 46 % ahora.

**Opción:** que el nivelado reserve sitio para el realce y el bajo cuando están puestos. Por ejemplo, el P90 a 0 dB del techo en vez
de a 1 dB. Así las aplastadas bajarían ~1 dB y el limitador trabajaría menos en ellas. **Recomendación:** decidir primero M1, que es lo
que más pesa, y volver a medir.

## 5. Lo que queda por ver en la rocola real

- Los MP3 reales (descompresor ACM de XP).
- El nivel de una canción mono en el mezclador de XP frente a la misma en estéreo con los dos canales iguales (para M2).
- Cómo suenan en el billar M1 y M3.
