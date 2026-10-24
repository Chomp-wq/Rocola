# Revisión de la cadena de audio completa — 24 de octubre de 2026

Rama Modest (`claude/modest-carson-fa2v2n`). El dueño pidió revisar la cadena de audio entera, asegurarse de que todo funcione bien
y ver qué se puede mejorar.

Primero salieron dos defectos latentes, que se arreglaron, y tres mejoras propuestas (M1, M2, M3). Después el dueño decidió:
**«Haz lo que sea mejor para la calidad del sonido»**. Con eso se hicieron las tres mejoras y dos arreglos más que aparecieron al medir:

| | Qué era | Antes | Ahora |
|---|---|---|---|
| Defectos (sección 3) | el fundido y el des-recorte con lecturas partidas | no sonaban (la salida pide cuadros enteros) | arreglados, bit a bit igual |
| **M1** (4.1) | el grave en mono giraba la fase del grave | picos +1,5 a +2,7 dB en los masterizados | +0,06 a +0,08 dB (fase lineal) |
| **M2** (4.2) | las canciones mono | sin balance ni relevo sin pausa con las estéreo | con los dos (estéreo de canales iguales) |
| **M3** (4.3) | el P90 del nivelado al límite | 1 dB del techo | 0,5 dB, medido en la cadena de fábrica |
| **Subgrave** (4.4) | giraba la fase del grave, como M1 | billar: limitando el 21 al 70 % del tiempo | del 1 al 3 % (fase lineal) |
| **Disco continuo** (4.5) | el cambio de pista dentro de un mismo disco | **un escalón de 0,33** (música a 0,3 de pico) | 0,0002 |

Lo más importante es lo último. En la versión publicada, **cada cambio de pista de un disco continuo daba un golpe** (un escalón en
la onda de un tercio del pico). No es raro: pasa en todo disco en vivo, de mezclas o conceptual que suene entero.

Todo está medido con señales sintéticas en Mono/Linux. **Nada se oyó en el billar**, y los MP3 no se pudieron probar: en Linux no
hay descompresor del sistema (ACM).

## 1. La cadena, en orden

Lo que arma `ArmaCadena` (Form1.Sonido.cs) para cada canción:

| # | Etapa | De fábrica | Retardo |
|---|---|---|---|
| 1 | Lector (`FileAudioSource`; un archivo mono, como estéreo de dos canales iguales; sin el relleno del MP3 y sin los silencios largos) | siempre | — |
| 2 | `FuenteOriginal`: mide la original (nivelado, EQ) y limpia NaN | siempre | — |
| 3 | Arreglos de la grabación S1/S2 (canal mudo, continua) | encendidos, solo actúan con el defecto | — |
| 4 | Des-recorte | encendido, solo en canciones recortadas | 68 cuadros |
| 5 | Quitar el siseo (I2) | apagado | 11,6 ms |
| 6 | DSEE (reconstructor de agudos) | apagado (y solo en equipos que lo permiten) | ninguno: lee por delante |
| 7 | `LevelTap`: nivelado al límite, Subir TODO, realce 2×, EQ automático, medida | encendido | 31 cuadros |
| 8 | Grave en mono (120 Hz, fase lineal), estéreo estrecho, subgrave (fase lineal) | grave en mono **encendido**; los otros apagados | ninguno: lee por delante |
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

## 4. Lo que se hizo (decisión del dueño: «Haz lo que sea mejor para la calidad del sonido»)

### 4.1 M1. El grave en mono, en fase lineal

**Qué pasaba.** El grave en mono está encendido de fábrica (`grave_mono=120`, sin interruptor) para todo el estéreo. Usaba un cruce
Linkwitz-Riley:

- el **lado** perdía el grave;
- el **medio** pasaba por un paso-todo, para seguir en fase con el lado.

El paso-todo no cambia el nivel de ninguna frecuencia, pero gira la fase del grave. En una onda aplanada por el limitador de un
masterizado moderno (el bombo pegado al techo), ese giro **subía los picos**:

| Mezcla | Pico antes | Con el cruce de antes | Ahora |
|---|---|---|---|
| sin masterizar | −1,0 dBFS | −1,4 dBFS (−0,4 dB) | igual que el original |
| masterizada, 3 dB de limitador | −0,3 dBFS | +1,2 dBFS (**+1,5 dB**) | +0,06 a +0,08 dB |
| masterizada, 6 dB de limitador | −0,3 dBFS | +1,8 dBFS (**+2,1 dB**) | +0,06 a +0,08 dB |
| masterizada, 9 y 12 dB de limitador | −0,3 dBFS | +2,4 dBFS (**+2,7 dB**) | +0,06 a +0,08 dB |

El limitador de la rocola tenía que volver a quitar esos picos. En la cadena de fábrica, con el masterizado de 9 dB, quitaba más de
1 dB el 46 % del tiempo, y como mucho 3,8 dB. Con el cruce nuevo, el 26 % y 1,8 dB (y, con M3, el 2 % y 1,3 dB).

**Cómo es ahora** (`GraveMono.cs`):

- El grave del lado se quita con un paso-bajo FIR simétrico (fase cero). Es un seno cardinal con ventana de Kaiser: −6 dB en el
  corte, transición de un corte de ancho y 60 dB de rechazo. Por encima de 1,6 veces el corte, el lado sale entero (0,01 dB).
- El medio no pasa por nada: sale bit a bit como entra.
- Para sacar un cuadro hacen falta los 15 ms que vienen detrás. La etapa lee por delante, como el DSEE, así que lo que sale va en
  su sitio, sin retardo. Va por bloques de 63 ms con una FFT de 4096.
- Cuesta unos 0,12 µs por cuadro (antes 0,03), en Mono/esta máquina.

Dos detalles que salieron al hacerlo:

- **El cebado, en silencio exacto** (`FadeProvider`). En fase lineal, lo que suena al empezar deja un eco previo en los cuadros de
  cebado, que el relevo tira pero que con fundido sí se oía. Ahora el cebado sale en cero exacto.
- **La vigilancia del canal al revés** lleva un piso de −70 dB. En el silencio, con restos diminutos, decidía «canal al revés».

### 4.2 M2. Las canciones mono, como estéreo de dos canales iguales

**Qué pasaba.** Un archivo mono salía mono a la tarjeta, y Windows mandaba lo mismo a las dos bocinas. Por eso:

- no llevaba el balance del local (`SalaLocal` solo lo pone con dos canales);
- entre una canción mono y una estéreo no había relevo sin pausa: se cerraba y reabría la salida.

**Ahora** el lector abre los archivos mono como estéreo con los dos canales iguales (`ComoEstereo`, `MonoComoEstereo` en
Form1.Sonido.cs). Es lo mismo que hacía Windows al mandarlos a dos bocinas, pero dentro de la rocola:

- llevan el balance;
- entran sin pausa detrás de una estéreo;
- el nivelado no cambia (se mide por canal);
- salen bit a bit las muestras del archivo, en los dos canales.

A cambio, esas canciones pasan dos canales por la cadena y cuestan el doble. No se pudo saber cuántas hay en la biblioteca: las
tablas no guardan los canales.

### 4.3 M3. El P90 del nivelado al límite, a 0,5 dB del techo

El nivelado al límite (`NIVELADO_AL_LIMITE_Y_COMPATIBILIDAD_MODEST_2026-10-24.md`) eligió sus márgenes con el limitador solo. Pero lo
que va detrás del nivelado también sube algo los picos: el realce, el bajo y (antes de M1) el grave en mono. Ahora se midieron en la
**cadena de fábrica entera**, con catorce canciones de prueba:

- golpes parejos, desiguales y sueltos;
- baladas con y sin un golpe suelto;
- una moderna aplastada, una vieja bajita y una comprimida grabada baja;
- una mezcla completa sin masterizar, y masterizada con 3, 6, 9 y 12 dB de limitador;
- y una vieja con el bajo a un lado.

El criterio es el mismo: que el limitador quite más de 1 dB como mucho el 10 % del tiempo, y más de 3 dB como mucho el 1 %.

| P90 | Resultado |
|---|---|
| 1 dB (antes) | los masterizados, del 11 al 38 % del tiempo por encima de 1 dB |
| **0,5 dB (ahora)** | como mucho el 8 % y el 0,6 %; los masterizados, el 2 % y nunca más de 1,5 dB |
| 0 dB | casi no mejora, y las de batería muy seguida y las baladas pierden 0,5 dB más |

El precio: una canción con golpes en más del 10 % de sus bloques (batería muy seguida) suena algo menos fuerte.

### 4.4 El subgrave, en fase lineal

**Qué pasaba.** «Quitar el subgrave» (apagado de fábrica) lo encienden a mano o los tipos de local «billar» y «bocinas chicas» del
modo automático. Era un paso-alto Butterworth de cuarto orden de verdad (dos bicuadradas), y gira la fase del grave como el paso-todo
de M1:

| | 40 Hz | 50 Hz | 60 Hz | 80 Hz | 120 Hz |
|---|---|---|---|---|---|
| giro de antes | 125° | 96° | 78° | 57° | 38° |
| ahora | 0,00° | 0,00° | 0,00° | 0,00° | 0,00° |

Un bombo de 50 Hz aplanado subía 7 dB de pico; ahora, −0,01 dB.

En la cadena de fábrica, el limitador (lo peor que baja; el tiempo por encima de 1 dB / por encima de 3 dB):

| | Masterizada 9 dB | Moderna aplastada |
|---|---|---|
| sin el local (de fábrica) | 1,3 dB; 2 % / 0 % | 1,5 dB; 1 % / 0 % |
| + subgrave, antes | 4,3 dB; 39 % / 1,8 % | 4,9 dB; **90 %** / 7,2 % |
| + subgrave, ahora | 1,6 dB; 3 % / 0 % | 2,7 dB; 7 % / 0 % |
| modo billar, antes | 3,6 dB; 21 % / 0,6 % | 4,4 dB; 70 % / 2,8 % |
| modo billar, ahora | 1,3 dB; 1 % / 0 % | 2,0 dB; 3 % / 0 % |
| bocinas chicas, antes | 3,9 dB; 23 % / 1 % | 4,4 dB; 72 % / 3,1 % |
| bocinas chicas, ahora | 1,9 dB; 2 % / 0 % | 2,0 dB; 3 % / 0 % |

Las demás piezas del local (estéreo estrecho, claridad, partes suaves, presencia, agudos +1) ya cumplían el criterio y no se tocaron.

**Cómo es ahora:**

- Se quita lo que la misma curva de Butterworth quitaría (1 − |Butterworth|, en fase cero): un FIR simétrico de 3,5 periodos del
  corte a cada lado (117 ms a 30 Hz), por FFT.
- La curva es la de antes con 0,3 dB desde 25 Hz: −3,3 dB a 30 Hz, −13,4 a 20 Hz y −22 a 15 Hz (antes −3,0, −14,3 y −24,1).
- Con el grave en mono puesto, el lado ya sale sin grave: basta con quitárselo al medio. Los dos canales pierden lo mismo, y lo
  mono sigue mono bit a bit.
- **Apagado** pasa tal cual y no lee por delante (solo guarda lo último que salió): la cadena de fábrica sale bit a bit como antes
  (`compara-cadena`), y cuesta 7 ns por cuadro.
- **Encendido** lee 255 ms por delante (de una vez, al encenderlo) y cuesta unos 0,2 µs por cuadro en Mono/esta máquina. Mientras
  está encendido, el tiempo que enseña la pantalla va un cuarto de segundo por delante de lo que se oye.
- Encender y apagar en vivo sigue yendo en rampa de 25 ms, sin chasquido.

### 4.5 El cambio de pista en un disco continuo

**Qué pasaba.** Cuando la siguiente es la pista que sigue del mismo disco, el relevo la encadena sin fundido, y algunas etapas
heredan el estado de la anterior para que el cambio no se oiga. Se midió con un disco partido en dos pistas, por las dos cadenas de
verdad y el relevo, contra la canción entera (`cadenaentera`, música a 0,3 de pico):

| | Escalón en el cambio, versión publicada | Ahora |
|---|---|---|
| de fábrica | **0,33** | 0,0002 |
| con la claridad | 0,33 | 0,0002 |
| modo automático: billar | 0,17 | 0,0003 |
| graves del local +3 y subgrave | 0,16 | 0,0003 |
| a tope, con el limitador trabajando en el cambio | 0,46 | 0,0024 |

Con otra música de prueba (picos de 0,25), la versión publicada daba de 0,09 a 0,18. Un escalón así es un golpe en cada cambio de
pista.

**Las causas, y lo que se hizo:**

1. **El cebado era un hueco.** La siguiente trae al empezar los cuadros de los retardos de delante: el realce (31), el des-recorte
   (68) y el siseo. El relevo los tira, pero antes pasaban por las etapas que heredan (grave en mono, local, presencia). Para ellas
   eran un hueco de 0,7 a 2,3 ms entre las dos pistas, y el paso-todo del grave en mono de antes lo volvía un golpe. Ahora, tras
   heredar, el cebado pasa tal cual (`Cebado`, lo pone `ArmaCadena`).
2. **Etapas que empezaban de cero.** El realce y el EQ (`LevelTap`), el bajo psicoacústico y el limitador no heredaban nada. El
   realce, por ejemplo, arrancaba su paso-alto en 0, y la primera muestra salía como si fuera toda agudos. Ahora heredan (`Sigue`,
   desde `SigueA`):
   - la anterior guarda su estado al acabar su música, antes de vaciar su cola;
   - la siguiente sigue con él;
   - sus primeros cuadros son la cola de la anterior otra vez, y el relevo los tira (son su cebado).
3. **Las etapas de fase lineal** (grave en mono y subgrave). La anterior acaba sin conocer la siguiente. Lo que le faltó quitar en
   su último cuadro, la siguiente lo lleva a cero en 25 ms (el empalme).

**Lo que queda:**

- Lo que la anterior no podía saber de la siguiente. En sus últimos milisegundos se aparta hasta 0,08 de la canción entera, pero
  sin escalón. Es propio de cualquier etapa que mira por delante.
- El DSEE y «Quitar el siseo» no heredan. En la prueba no dieron escalón, pero con señales sintéticas puede que no actuaran.
- Si la siguiente empieza con un golpe que hay que limitar en sus primeros 3 ms, el limitador de la anterior no lo pudo ver venir.

Fuera del disco continuo no cambia nada: la cadena de fábrica sale bit a bit igual (`compara-cadena`, 8 de 8 combinaciones sin
subgrave).

## 5. Lo que queda por ver en la rocola real

- Los MP3 reales (descompresor ACM de XP).
- El nivel de una canción mono en el mezclador de XP frente a la misma en estéreo con los dos canales iguales (para M2).
- Cómo suenan en el billar M1, M3 y el subgrave.
- **Un disco continuo entero** (un disco en vivo o de mezclas): los cambios de pista ya no deberían oírse.
- Lo que cuesta en el A4 con el subgrave encendido (aquí, unos 0,2 µs por cuadro; allí no se midió).
