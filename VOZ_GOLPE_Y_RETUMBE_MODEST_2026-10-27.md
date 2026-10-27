# Voz, golpe y retumbe — Modest, 27 de octubre de 2026

Tres etapas nuevas de la cadena de sonido, elegidas entre las técnicas de audio que se revisaron el 27-10 (propuestas 1, 4 y 5 de esa
revisión). Pensadas para el billar: techo de lámina, ladrillo, loseta, bocinas repartidas por zonas, gente y bolas.

**Todo está probado en Linux/Mono con señales sintéticas y con canciones de prueba generadas. Nada se oyó en el billar ni se probó en el A4
con XP.** Los números de abajo son de este equipo (Mono); el A4 es unas 3 o 4 veces más lento.

| | Qué hace | Interruptor (apagado de fábrica) | Con el modo automático del local |
|---|---|---|---|
| **Que el bombo no hunda la voz** | Cuando el bajo es lo que hace pasar la señal del techo, baja solo el bajo (hasta 6 dB) en vez de que el limitador baje toda la mezcla | `bombo_sin_hundir=` | encendida |
| **Retumbe dinámico** | Baja 260 Hz (hasta 4 dB, 3 con la claridad) solo en el momento en que el grave medio se pasa de su propia media | `retumbe_dinamico=` | encendida |
| **Ataque más claro** | Quita cola detrás de cada golpe (hasta 3 dB) y, si hay sitio bajo el techo, sube el ataque hasta 1,5 dB | `ataque_claro=` | encendida |

## Cómo se gobiernan

- **Con el modo automático del local encendido** (Ctrl+F3 → Sonido → El local y sus bocinas → Tono y presencia; hecho solo para el billar) las
  tres van encendidas, en cualquier canción: ninguna necesita una regla por canción porque cada una se frena sola por lo que oye. Lo guardado
  en sus interruptores se conserva y vuelve al apagar el modo automático.
- **Con el modo automático apagado** manda cada interruptor persistente, apagado de fábrica.
- **Con todo de fábrica la cadena sale bit a bit como antes.** Comprobado contra el ejecutable del commit anterior (3437c7c) con diez canciones de
  prueba, cuatro maneras de armar la cadena y los dos perfiles: 80 de 80 iguales (ver «Pruebas»).
- **«Escuchar sin mejoras»** (S4) quita el retumbe y el ataque, y deja el bombo: es de la familia del limitador, que tampoco se quita.
- **Por ahora no tienen mando en Ctrl+F3.** La ventana de Sonido se va a reorganizar (una sub-ventana junto al interruptor del modo
  automático) y los mandos irán allí, en gris con el motivo del modo automático como el resto del local. Las claves existen, se leen, se guardan
  y «Dejar como de fábrica» las apaga; mientras tanto se pueden poner a mano en `rocola_config.txt` (`...=1`) o se encienden con el modo
  automático.
- Ninguna toca el volumen de Windows ni el de la salida.

## Qué escuchar en el billar

- **Bombo.** Canciones con bajo o bombo fuerte y voz encima (reguetón, pop actual, cumbia con bajo eléctrico): sin la etapa, cada golpe se
  lleva la voz por delante unos instantes («se hunde»). Con ella, la voz debe aguantar y el golpe sonar un poco más recogido. Si el bajo suena
  «blando» o con menos pegada, es esta etapa (baja hasta 6 dB solo en el momento justo). **Ojo:** en un bombo de ataque muy seco el primer
  golpe sigue pasando al limitador (ver «Lo que no hace»); la mejora es sobre todo en el bajo sostenido y en las notas de ataque blando.
- **Retumbe.** Un bajo o una nota grave que «se infla» de pronto y tapa la voz: debe quedar más contenido durante un segundo o dos y soltarse
  solo. Si un bajo que se queda largo suena distinto al principio que al final, es normal: la media lo alcanza en unos 6 s y el corte se va.
- **Ataque.** Voces y cajas más claras y menos «arrastradas» en el eco del local. En canciones ya al límite del volumen casi solo se oye la
  parte de la cola; el ataque sube solo cuando hay sitio. Si una voz suena «seca» o cortada al final de las frases, es esta etapa.

Para decidir con el oído: alternar «Escuchar sin mejoras» (15 s) quita el retumbe y el ataque; para el bombo, apagar el modo automático.

## Cuánto cuestan (procesador)

Medido en la cadena entera con canciones de prueba (30 s, mediana de 5): **unos 24-26 ms por segundo de música con las tres apagadas, y 29-31 con las
tres encendidas** (+4 a +6 ms/s en este equipo; en el A4, del orden de 15 a 20 ms/s: 1,5-2 % de un núcleo). Apagadas no cuestan nada
(la etapa no toca la muestra). Cada una por separado, en la batería (ruido, 2 canales): bombo 2,8, retumbe 1,1–1,2, ataque 1,2–1,5 ms/s.

## Detalle de ingeniería

### El orden, y por qué

```
... EQ automático → grave en mono → local (claridad, tono, partes suaves, balance)
    → RETUMBE → presencia (compresor) → ATAQUE → eses (T9) → bajo psicoacústico → BOMBO → limitador de pico real → fundido
```

- **Retumbe justo detrás del local.** Así el detector oye lo que ya salió del EQ automático y de la claridad (no cuenta dos veces lo que ya se
  cortó) y el compresor de la presencia no aprieta por un grave que se va a bajar.
- **Ataque detrás del compresor.** El compresor aplana los golpes; el ataque los levanta después. Y delante de las eses: si el ataque sube una
  «s», T9 la baja.
- **Bombo pegado al limitador, detrás del bajo psicoacústico**, que es quien añade armónicos graves: el bombo decide cuánto del techo le toca al
  bajo con todo ya sumado. El fundido, que solo baja, y el paso a 16 bits quedan detrás.
- Ninguna añade retardo. Las tres heredan el estado de la pista anterior del mismo disco (`Sigue`, desde `SigueA`) y el cebado de delante pasa
  tal cual (`EtapaPorCuadros.Cebado`); no entran en `audioTailDelay`.

### Lo común (`EtapaPorCuadros.cs`)

Cuadros enteros como `SalaLocal`: en estéreo se pide siempre un número par; una lectura de una muestra se hace aparte y su mitad sale en la
siguiente; si el origen acaba a medio cuadro lo que queda sale tal cual. Uno o dos canales a 8 kHz o más; con seis canales o menos de 8 kHz pasa
tal cual. Cuentas en coma doble, limpieza de desnormales en cada filtro, sin reservas al leer (se mide: menos de 1 KB en 20 lecturas). Una sola ganancia para
los dos canales. «Quieta» (apagada y ya en su valor neutro): ni toca la muestra y deja su memoria para que, al encender, parta del momento.

### 1. Que el bombo no hunda la voz (`BomboSinHundir.cs`)

- **Detección.** Por canal, bajo = paso-bajo de 140 Hz (Butterworth, dos polos); resto = paso-alto de 200 Hz con su envolvente de pico (5 ms).
  Si `|bajo| + resto` pasa del techo (`TechoPicoReal`, 0,88), el bajo manda (al menos el 40 % de la suma) y el resto solo no pasa del techo, la
  ganancia del bajo es `(techo − resto) / |bajo|` con piso de 0,5 (−6 dB): el bajo recibe solo el margen que le deja el resto. Esa cuenta es
  una cota (supone que los picos coinciden), así que solo se usa si alguna muestra de los últimos 30 ms pasó **de verdad** del techo; si
  nada pasa, la salida es la entrada bit a bit, aunque la cota diga que podría pasar. Se midió: sin esa condición el bombo bajaba hasta 1,6 dB
  en tres canciones sin que el limitador fuera a hacer nada.
- **Tiempos.** Ataque 0,5 ms, espera de 20 ms con la ganancia quieta, suelta de 80 ms (la lección de `pruebas/soltar.cs`: no modular dentro de
  un ciclo del bajo). Lo más hondo que se pidió se queda como meta durante la espera y se refresca con cada petición igual o mayor.
- **Cómo se baja el bajo: un estante de graves dinámico** (RBJ, 140 Hz, Q 0,7071, uno por canal, coeficientes cada 4 cuadros mientras la
  ganancia cambia). **Se probó primero el reparto complementario del plan (salida = entrada − (1 − g)·paso-bajo) y no sirve:** un paso-bajo
  causal gira la fase del grave, y `entrada − paso-bajo` deja pasar mucho del propio bajo (a 60 Hz lleva el 62 % de la señal) y sube hasta 1 dB
  entre 150 y 300 Hz. Con g = 0,5 solo bajaba 3,4 dB a 60 Hz y 0,9 dB a 100 Hz (cuenta con la respuesta del filtro; incluso quitando todo el
  paso-bajo, g = 0, solo bajaría 4,1 dB a 60 Hz). El estante baja g entero por debajo de ~80 Hz, la mitad a 140 Hz y casi nada por encima de
  400 Hz, y no sube nada en ningún sitio.
- **El bajo ya es mono por debajo del cruce** (`GraveMono.cs`, 120 Hz, fase lineal; el medio sale tal cual): se comprobó. Pero no siempre: con
  `grave_mono=0`, con el balance movido o con un canal al revés los bajos de los dos canales difieren. Por eso cada canal lleva su paso-bajo y
  su estante y comparten la ganancia (la mínima de los dos).
- **Mientras g = 1** (casi siempre) no se calcula ningún estante y la salida es la entrada. Al soltar, a menos de una diezmilésima de 1 vuelve
  a 1 exacto. Los primeros 20 ms tras (re)encender no piden nada: el paso-bajo cebado con el continuo de la primera muestra parecería un bajo.

**Medido** (`pruebas/bombosinhundir.cs`, 56 comprobaciones):

| Prueba | Solo el limitador | Con la etapa delante |
|---|---|---|
| Bajo de 60 Hz a 1,2 + voz de 1 kHz a 0,2 (3 s): el 1 kHz | −4,00 dB | **0,00 dB** (el bajo cede −5,8 dB; pico tras la etapa 0,82 < 0,88) |
| Bajo sostenido 6 dB sobre el techo a 40 / 60 / 100 Hz: distorsión | −77,0 / −81,8 / −84,1 dB | **−101,0 / −94,9 / −82,3 dB** |
| Golpes de bajo de ataque blando (12 ms) cada 0,5 s con voz: la voz en el golpe | −1,81 dB | −0,91 dB |
| Golpes de bajo de ataque seco cada 0,5 s con voz | −2,39 dB | −2,18 dB (no empeora; ver abajo) |

Tiempos: llega a 0,6 en 4,5 ms; piso 0,50 (−6 dB); 20 ms quieta; a los 80 ms de soltar ha vuelto el 62 % (la constante da 63 %); 1 exacto al
cabo de ~1 s. Sin chasquidos al encender y apagar sonando (el salto mayor no pasa del de la entrada); vuelve a bit a bit en 40 ms. Un bajo suave
con la voz arriba pasada del techo: el bajo no se toca, bit a bit.

**Lo que no hace (hallazgo).** La etapa no mira hacia adelante (el plan decía «sin retardo»). El **primer pico de un bombo de ataque seco**
(1–3 ms) ya pasó cuando el estante llega a su ganancia, y ese primer pico es el que manda en el limitador: le hace bajar toda la mezcla
~3,6 dB durante ~120 ms. Por eso, con un bombo sintético seco, la voz se hunde casi igual (−2,39 → −2,18 dB), aunque los picos que siguen
bajan (de 0,88 a 0,63). Con un ataque blando o un bajo sostenido sí gana. Con una mirada de 2,5 ms hacia adelante (110 cuadros de retardo
más, que habría que sumar a `audioTailDelay` y vaciar al final) se probó un prototipo: −2,39 → −1,33 dB con el bombo seco; no se dejó porque
cambia el retardo de la cadena y el plan dice sin retardo. **Decisión pendiente del dueño**, después de oírlo: la mirada hacia adelante, o
un piso más hondo que 6 dB. En las canciones de prueba de la cadena (nivelado al límite) la etapa actúa en el 2–6 % del tiempo (hasta 6 dB), pero
el limitador no se alivia: sus bajadas vienen de los medios y agudos nivelados (el bajo es el 15–25 % de la suma), así que la etapa no las toca.
Las pruebas con canciones reales de bombo fuerte quedan para el billar.

### 2. Retumbe dinámico (`RetumbeDinamico.cs`)

- **Detector** sobre la suma mono, **detrás** del local: dos paso-banda (260 Hz y 1,4 kHz, Q 1) con su energía suavizada en 25 ms; la razón
  entre los dos (dB) se compara con su propia media lenta (6 s; los primeros 6 s, la media de lo oído hasta entonces; los primeros 150 ms no se
  mide). Debajo de unos −80 dBFS no se mide.
- **Corte** = la mitad de lo que la razón pase de (media + 3 dB), hasta 4 dB (3 dB con «Claridad para el local»). Ataque 20 ms, suelta 200 ms,
  coeficientes cada 32 cuadros como las del local. Campana de 260 Hz, **Q 1,4** (con Q 0,9 el 1 kHz bajaba 0,2 dB; con 1,4, 0,09). Sin corte
  (0 dB exacto) no filtra: la muestra sale tal cual y la memoria del filtro sigue a la señal.
- **Presupuesto (aviso del dueño sobre el EQ automático y la claridad).** El EQ automático puede cortar hasta ~3,4 dB en 400 Hz y la claridad
  −2,5 dB en 300 Hz (juntos, ~5,3–5,5 dB en 400 Hz). `CorteEstaticoDb` mide lo que cortan juntos de 250 a 500 Hz (la respuesta de los
  biquads de verdad: la curva de 8 bandas de la canción, leída del tap, y la campana de la claridad), y el corte dinámico solo puede llegar a
  lo que falte para **6 dB en total**: sin nada, 4 dB; con la claridad, 3 (medido 2,97); con 5,3 ya cortados, 0,7 (medido 0,69); con 6 o más,
  no actúa (0,00). Se recalcula cada vez que cambia algo (al armar la canción, al mover un mando, en la siguiente preparada). Un EQ que sube esa
  zona no cuenta como corte. Cuando se acote el EQ automático en el billar (400 Hz a −2,5), el presupuesto lo recoge solo.

**Medido** (`pruebas/retumbedinamico.cs`, 45 comprobaciones; fondo de ruido blanco + 1 kHz, un tono de 250 Hz que sube 8 dB la banda del detector):
corte del tono antes del salto 0,00 dB; 50 ms después 0,54; **100–200 ms 2,40**; 0,5 s 2,46; 2 s 1,50; 4 s 0,62; **6 s 0,10**; 8 s 0,02; 11 s
0,00. Fuera de banda: 1 kHz −0,09 dB, 3 kHz < 0,1 dB, 80 Hz < 0,8 dB. Fondo parejo de 12 s: 0 muestras tocadas (bit a bit). Sin
chasquidos al encender/apagar; apagada, vuelve a bit a bit en 40 ms. Disco continuo (Sigue) = una sola pieza; lecturas de a una muestra o impares
= iguales; un canal y dos, el mismo corte en los dos.

### 3. Ataque más claro (`AtaqueClaro.cs`)

- **Detector** sobre la suma mono de dos paso-altos de 120 Hz; envolvente rápida (1 ms de ataque / 10 ms de suelta) y lenta (25 / 250 ms). Las
  dos tienen la **misma razón** ataque/suelta (10): así un tono o un ruido parejo da la misma envolvente en las dos y `d = 0` (el plan decía
  20 para la rápida; con 20 un tono parejo daba un sesgo). `d = 20 log10(rápida / lenta)`; por debajo de −55 dBFS la lenta no se mide.
- **Cola:** `d < −2 dB` → `−0,35 dB` por cada dB de más, tope −3 dB. **Ataque:** `d > +2 dB` → `+0,3 dB` por cada dB de más, tope +1,5 dB y solo
  con sitio: el pico de los últimos 0,5 s (luego bajando con 1 s de constante) no puede pasar de `TechoPicoReal × 0,985`.
- **Suavizado.** 5 ms para entrar a la cola o al ataque; 60 ms para dejar la subida; **2 ms para dejar la cola** (con 5 ms el primer ataque
  del golpe siguiente pasaba por una puerta a medio cerrar, −1,6 dB en los primeros 5 ms; con 2 ms, −0,7). Una sola ganancia para los dos canales.
- **Se aplica solo a lo que pasa del paso-alto** (`salida = entrada + (G − 1)·pasoalto`): el grave por debajo de 120 Hz no se mueve, así el
  bombo no bombea.

**Medido** (`pruebas/ataqueclaro.cs`, 38 comprobaciones): tonos parejos de 200 Hz, 1 kHz y 5 kHz, y ruido blanco parejo: ganancia 0,00 dB
(menos de 0,1 dB en cada ventana). Golpes de 1 kHz (40 ms, luego se apagan con 50 ms de constante) cada 0,4 s: **la cola baja hasta −2,93 dB**;
el ataque sube hasta +1,36 dB con sitio (pico 0,25 → 0,29); **a 0,86 de pico (sin sitio) el pico queda a 0,00 dB** y la cola baja igual; nunca
pasa del techo. Golpes de 80 Hz: la ganancia llega a 0,6 dB (el paso-alto de 120 Hz aún deja pasar parte). Por debajo de −55 dBFS, bit a bit.
Sin chasquidos (no pasa del salto que da encendida todo el rato). En las canciones de prueba de la cadena apenas actúa (cola de 0,1 a 0,4 dB,
sube 0,3 dB en el 0–0,1 % del tiempo): son música sostenida, sin golpes secos; **en música con golpes se verá más**.

### El modo automático

`AjusteLocal` tiene tres campos nuevos (`Bombo`, `Retumbe`, `Ataque`). `LocalAutomatico` los pone siempre a `true` (sin regla por canción: ninguna
necesita una medida; para el ataque, la única medida disponible, el rango/LRA de la canción, no dice nada sobre la cola de los golpes y no se
inventó una regla). Con el modo apagado, `LocalDe` devuelve los tres interruptores. Podrían pasar a decidirse por canción **después de oírlas en
el billar**. Lo de «Escuchar sin mejoras» se resuelve en `AjustaVozGolpe` (`VozGolpeYRetumbe.cs`), que llaman `AplicaBocinas` y
`AplicaSinMejoras` (esta última se llama tras cada cambio de ajustes, de canción y de la siguiente preparada).

### Lo que queda de la ventana

Sin hoja nueva ni mandos (decisión del dueño del 27-10: la ventana de Sonido se reorganiza después). Hechos: las tres claves
(`bombo_sin_hundir=`, `retumbe_dinamico=`, `ataque_claro=`: se leen, se guardan, «Dejar como de fábrica» las apaga), `AjusteLocal`, el
motivo en gris queda por poner con los mandos (`motivoAuto` ya existe). No hay copia de seguridad para «Cancelar» porque nada en la ventana las
cambia. La línea S5 («Lo que suena ahora») sale sola, en un renglón corto y solo con la etapa encendida: `Bombo −x,x dB   Retumbe −x,x dB   Ataque
−x,x/+y,y dB` (o «nada»).

## Pruebas

- Baterías nuevas (`pruebas/`): `bombosinhundir` (56), `retumbedinamico` (45), `ataqueclaro` (38), en `todo.sh` y `LEEME.md`: apagadas = bit a
  bit con 1, 2 y 6 canales y a menos de 8 kHz; lo que hace; tiempos; sin chasquidos; disco continuo = una sola pieza (con y sin `Sigue`);
  lecturas partidas; sin reservar; coste; enchufe en la cadena, modo automático, configuración, «Dejar como de fábrica» y S4.
- Actualizadas: `gravemono` (cada etapa lee de la anterior, con las tres nuevas) y `localauto` (los tres campos nuevos y que `ArmaCadena` pasa
  por el retumbe).
- **Cadena entera contra el ejecutable de 3437c7c** (procesos aparte, hash del PCM de 16 bits y de lo aprendido): diez canciones (MP3 de 44,1 y
  48 kHz, mono, silencios, canal mudo, continua, WAV, una fuerte), «de fábrica» y «todo encendido» (los mandos viejos), canción nueva y analizada:
  **80 de 80 iguales** (40 por perfil, CPU y GPU).
- La suite entera (`todo.sh`): igual que la de 3437c7c (que ya fallaba en `sala`, `discoacotado` —los dos perfiles— y `caratulasadaptativas`
  con la tarjeta; `subgrave` falló en la base y pasó después: es de las que dependen del tiempo). Sin fallos nuevos.
- Compila limpio (`-sdk:2 -langversion:3 -warn:4 -warnaserror+`, los dos perfiles). El renderizador por CPU congelado no se tocó.
- Nada de esto es oír la música en el billar ni medir en el A4.
