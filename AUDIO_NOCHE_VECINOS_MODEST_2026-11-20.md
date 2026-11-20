# El modo noche y los vecinos: qué más se puede bajar de noche — Modest, 20 de noviembre de 2026

Petición del dueño: «El modo noche ahora tendrá el slider desde 50 hasta 90% y busca si hay más cosas que se puedan bajar durante la
noche (frecuencias) que causen que el sonido moleste a los vecinos».

Este documento es **solo la búsqueda y las medidas: no cambia nada del programa.** Tú eliges. El deslizador de 50 a 90 % va aparte;
aquí se mide lo que da su 50 %. Todo lo medido es con **canciones sintéticas** pasadas por la **cadena de sonido de verdad** de la
rocola de XP (`Rocola.exe` por dentro, con el modo billar puesto, en Linux con Mono), más un modelo de muro. **Nada se oyó en el billar
ni se midió en el A4.**

## En pocas palabras

- **Lo que atraviesa el muro es el grave.** Un muro de tabique deja pasar unos 24 dB más a 63 Hz que a 1 kHz. Dentro del billar la
  música tiene unos 7 dB más en la escala C (la que cuenta el grave) que en la A (la del sonómetro normal); **en la casa del vecino
  la diferencia sube a 17-20 dB.** La OMS pide mirar la baja frecuencia aparte cuando esa diferencia pasa de 10 dB.
- **El modo noche de hoy (80 % y «Graves de noche» 4 dB) ya baja afuera unos 5 dB** de graves frente al día, con 4,2 dB menos dentro.
- **Lo que más rinde es quitar más grave con filtros fijos**, no bajar más el volumen: el «Graves de noche» más hondo (NV1) y un
  corte del grave más hondo a 80 Hz (NV2). Juntos (NV3) bajan afuera **3 dB más los graves que se oyen, 5,7 dB los dB(C) y 5,8 dB
  los golpes**, y dentro se pierde 1,4 dB de lo que se oye (de 0,6 a 3,4 según la canción). Para lograr lo mismo afuera con el
  volumen habría que bajarlo de 3 a 6 dB. Lo que se nota dentro es menos grave hondo (unos 9 dB menos en 40-100 Hz).
- **De madrugada** (NV4: 70 %, graves de noche 12 dB y el corte a 80 Hz) los graves afuera quedan como con el deslizador al 50 %,
  pero dentro se oye 3 dB más fuerte que con el 50 %.
- **No sirven** (medido): bajar el techo del limitador (no hace nada hasta bajarlo 6 dB, y entonces el volumen da brincos de 3 dB),
  el «bombo sin hundir» de noche (el grave da brincos de 6 dB), el grave en mono hasta 250 Hz (0 dB), quitar 2-5 kHz (0 dB a través
  de un muro), y subir el bajo psicoacústico de hoy (pone muy poco: lo que añade queda de 14 a 34 dB por debajo de la música).
- **Probablemente pesa más lo que no es el programa:** una puerta, ventana o rendija abierta (un hueco del 1 % de la pared deja el
  muro en 20 dB en vez de 48), las cajas pegadas al muro del vecino, en una esquina o sobre el piso (a cada superficie pegada el grave
  sube hasta 6 dB y la vibración se va por la estructura), y la perilla de graves del amplificador.
- **Recomendación:** NV3 desde las 22 h con la misma bajada de una hora, NV4 desde la 1 de la mañana, NV5 (los videos igual que la
  música), y revisar puertas y bocinas (NV8) antes que nada, porque es gratis y puede valer más que todo lo demás.

## 1. Por qué molesta el grave (lo que dicen las fuentes)

**El muro deja pasar el grave.** Una pared aísla sobre todo por su peso: la «ley de masa» dice que el aislamiento sube unos 6 dB cada
vez que se dobla la frecuencia (y 6 dB cada vez que se dobla el peso). Del grave (63 Hz) a la voz (1 kHz) hay cuatro octavas: unos 24
dB menos de aislamiento en el grave [1, 2]. Además, en los cuartos chicos el grave no se reparte parejo (modos del cuarto) y en
algunos rincones se junta; y lo que sale de una caja pegada al muro o al piso entra también como vibración por la estructura, que se
salta el aislamiento del muro [3]. Las dos curvas de muro que usé (por tercio de octava, en dB de aislamiento):

| banda (Hz) | 31,5 | 50 | 63 | 80 | 100 | 125 | 160 | 250 | 500 | 1000 | 2000 | 4000 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| muro bueno: ley de masa, 250 kg/m² (tabique de 14 cm aplanado) | 31 | 35 | 37 | 39 | 41 | 43 | 45 | 49 | 55 | 61 | 67 | 73 |
| muro flojo: la curva de referencia de la ISO 717-1 en Rw 48 [4] | 19 | 23 | 25 | 27 | 29 | 32 | 35 | 41 | 48 | 51 | 52 | 52 |

La de la ISO tiene el índice global que da la norma para ese peso (Rw 48 [2, 4]); la de masa sale con más (Rw 59), porque exagera
los agudos, donde un muro de verdad pierde por la coincidencia. En el grave la de masa es optimista y la de la ISO pesimista: lo real
queda entre las dos. **Las conclusiones salen iguales con las dos**; los números de abajo son con el muro bueno salvo
que diga otra cosa.

**El sonómetro normal no ve el grave.** La escala A (la de casi todas las leyes) le quita 26 dB a 63 Hz. La OMS (1999) pone para un
cuarto de dormir de noche 30 dB(A) de promedio y 45 dB(A) de máximo, y avisa que el ruido con mucha baja frecuencia molesta con
menos: que la escala A no sirve ahí y que si la diferencia entre dB(C) y dB(A) pasa de 10 dB hay que medir por frecuencias [5].
Leventhall [6] revisó las quejas por ruido de baja frecuencia: la escala A subestima la molestia y las autoridades se equivocan por
fiarse de ella. Dinamarca tiene un límite propio para eso: la suma en A de los tercios de 10 a 160 Hz (**LpA,LF**), 20 dB dentro de
una vivienda de las 18 a las 7 h, y 5 dB menos si es a golpes [7]. La ISO 717-1 tiene una corrección de su índice de muro
(Ctr) pensada justo para «música de discoteca» y tráfico, que carga el grave [8], y en pruebas de escucha con música a través de 20
muros, lo que mejor predijo la molestia fue el índice con una versión de esa corrección [9] (otro estudio, con sonidos de la vida de
los vecinos, encontró que las bandas de 50 a 80 Hz pesan menos de lo que dice la ISO [10]: el grave importa, pero no todo es grave).

**Lo que hacen otros.** Los «modos noche» de los receptores de cine en casa y de las barras de sonido comprimen los picos (Dolby
recomienda su control de rango para ver de noche sin molestar a los vecinos) y muchos bajan además el subwoofer [11, 12]. En
Inglaterra, las condiciones para bares y conciertos miran sobre todo las bandas de 63 y 125 Hz, y los limitadores de los locales se ponen en escala C
para que vean el grave [13, 14, 15]. En México, la NOM-081-SEMARNAT-1994 pone 50 dB(A) de 22 a 6 h en zonas de casas (medido en el
límite del predio, afuera) [16]; la de la Ciudad de México (NADF-005-AMBT-2013) pone 62 dB(A) de 20 a 6 h en la colindancia de la
fuente [17]. Las dos son en escala A: cuentan sobre todo la voz y los medios, no el grave del que se queja el vecino.

**El bajo psicoacústico** («la fundamental ausente») es dar la sensación de un grave con sus armónicos, que una bocina chica sí da,
radiando menos energía en el grave de verdad [18]. Se mide abajo si sirve aquí.

## 2. Cómo se midió

`pruebas/nochevecinos.sh` (con `nochevecinos.cs` y `nochevecinos.py`) hace todo y borra el audio al acabar:

- **Las canciones**: cinco sintéticas de 24 s (cumbia, banda con tuba, reggaetón con 808, rock y balada) y ruido rosa. Dentro y de día
  tienen de 4,7 a 10,6 dB más en C que en A (de la balada al reggaetón). Nunca canciones de verdad.
- **La cadena de verdad**: `OpenAudio` + `ArmaCadena` del `Rocola.exe` de XP con el **modo billar puesto** (subgrave a 40 Hz, estéreo
  estrecho solo si hace falta, lo demás de fábrica), el nivelado al límite con la medida del repaso, y la sonoridad medida con el
  **propio medidor BS.1770 de la rocola** (LUFS). Lo que no existe se imita con lo que sí: el «Graves de noche» con más dB (el estante
  de 120 Hz no tiene tope), el corte a 80 o 100 Hz con el subgrave de fase lineal, una curva por el ecualizador del dueño, más bajo
  psicoacústico subiendo su `Amount`, el techo del limitador más bajo subiendo el volumen antes y bajándolo después (el limitador no
  depende de la escala), y el bombo «sin hundir» encendiéndolo en la cadena ya armada.
- **Las bocinas**: lo que dan las cajas de 12" con puerto (sintonía de unos 45 Hz: nada por debajo de 40 Hz).
- **Dentro**: por tercio de octava, en dB(A), dB(C) y LUFS. **Afuera**: lo de dentro menos el muro, banda por banda (con un cuarto
  de dormir común, del tamaño de la pared, la cuenta de la absorción da casi 0 dB). El nivel absoluto supone **85 dB(A) dentro, de
  día, junto al muro**: si en el billar hay menos, todo lo de afuera baja lo mismo; las diferencias no dependen de eso.
- **Rocola-xp**: la cadena de sonido es el mismo código (en `sistema/rocola/Xp/Codigo` los archivos de la cadena solo tienen las líneas
  de traducción), y lo de Spotify pasa por la misma `ArmaCadena`, así que **los números valen para los dos programas.** No corrí el banco
  con `rocola-xp.dll`.

Qué quiere decir cada columna de las tablas:

- **Dentro, lo que se oye**: los LUFS, la medida que usa la rocola para nivelar. −1 es «un poco más bajito».
- **Dentro, grave hondo**: la energía de 40 a 100 Hz (el «retumbe», lo que más se nota al quitar grave).
- **Afuera, graves**: el LpA,LF de la guía danesa (lo que el oído capta de los graves, hasta 160 Hz).
- **Afuera, dB(C)**: el sonómetro con el grave casi entero (lo que hace vibrar ventanas).
- **Afuera, golpes**: el máximo en C en ventanas de 0,19 s (el «pum» del bombo).
- **Afuera, dB(A)**: el sonómetro normal, el de las leyes.
- **Rinde**: cuántos dB bajan los graves afuera por cada dB que se pierde dentro. Bajar el volumen rinde 1.

## 3. Lo de hoy, de referencia (frente al día, muro bueno, media de las 5 canciones)

| | dentro: lo que se oye | dentro: grave hondo | afuera: graves | afuera: dB(C) | afuera: golpes | afuera: dB(A) |
|---|---|---|---|---|---|---|
| noche de hoy: 80 % y graves de noche 4 dB | −4,2 (de −3,4 a −5,5) | −6,4 | −5,1 | −5,8 | −5,7 | −4,2 |
| 70 % (y graves de noche 4) | −6,2 | −8,3 | −7,0 | −7,7 | −7,6 | −6,1 |
| 60 % | −8,4 | −10,6 | −9,2 | −9,9 | −9,9 | −8,3 |
| 50 % (el mínimo del deslizador nuevo) | −11,0 | −13,2 | −11,9 | −12,5 | −12,5 | −11,0 |

El porcentaje es «de lo que se oye»: 50 % son −10 dB, la mitad de fuerte. El 50 % baja todo 6,8 dB más que el 80 %, parejo en todas
las bandas. El N1 de hoy (los 4 dB del estante de 120 Hz) ya hace que la noche baje más afuera que dentro: sin él, la noche de hoy
bajaría 2,2 dB menos los graves de afuera.

**En la casa del vecino** (suponiendo 85 dB(A) dentro de día; muro bueno / muro flojo):

| | dB(A) | dB(C) | graves LpA,LF |
|---|---|---|---|
| día | 32,5 / 42,7 | 51,0 / 62,6 | 29,5 / 40,8 |
| noche de hoy | 28,4 / 38,2 | 45,2 / 56,7 | 24,5 / 35,6 |
| 50 % | 21,6 / 31,4 | 38,4 / 49,9 | 17,7 / 28,8 |
| NV3 al 80 % (recomendada, abajo) | 27,0 / 36,3 | 39,5 / 50,3 | 21,5 / 32,3 |
| NV4 al 70 % (madrugada, abajo) | 24,5 / 33,6 | 35,9 / 46,4 | 17,9 / 28,6 |

Con el muro bueno, la noche de hoy ya queda bajo los 30 dB(A) de la OMS pero 4,5 dB sobre los 20 dB de graves de la guía danesa; con el
muro flojo, ni con el 50 % se llega a esos valores: ahí manda el NV8. En un cuarto muy callado,
con la noche de hoy, de 100 Hz a 1 kHz las bandas quedan de unos 10 a 20 dB por encima del umbral de audición con el muro bueno (de 15
a 30 con el flojo) [19]: se puede oír algo de todo; pero lo que carga (dB(C), golpes) es el grave.

## 4. Las propuestas

Todas frente a la **noche de hoy** (80 % y graves de noche 4 dB), media de las 5 canciones (entre paréntesis, de la que menos a la que
más). Muro bueno; con el muro flojo, los graves y los dB(C) de afuera bajan de 0,2 a 1 dB más.

| | dentro: lo que se oye | dentro: grave hondo | afuera: graves | afuera: dB(C) | afuera: golpes | afuera: dB(A) | rinde |
|---|---|---|---|---|---|---|---|
| **solo volumen −3 dB** (referencia) | −3,0 | −3,0 | −3,0 | −3,0 | −3,0 | −3,0 | 1,0 |
| NV1a graves de noche 8 dB | −0,9 (−1,9 a −0,5) | −3,5 | −2,0 | −2,6 | −3,0 | −1,0 | 2,2 |
| NV1b graves de noche 12 dB | −1,6 (−3,2 a −0,8) | −6,9 | −3,9 | −5,0 | −5,7 | −1,7 | 2,4 |
| NV2 corte a 60 Hz | −0,5 (−1,6 a −0,0) | −2,3 | −0,5 | −1,8 | −1,5 | −0,3 | 1,1 |
| NV2 corte a 80 Hz | −0,8 (−2,6 a −0,1) | −6,2 | −1,2 | −3,8 | −3,6 | −0,6 | 1,5 |
| NV2 corte a 100 Hz | −1,2 (−3,3 a −0,3) | −11,1 | −2,2 | −5,5 | −5,6 | −1,0 | 1,9 |
| **NV3 graves 8 dB + corte a 80 Hz** | −1,4 (−3,4 a −0,6) | −9,4 | −3,0 | −5,7 | −5,8 | −1,3 | 2,1 |
| NV3b graves 12 dB + corte a 80 Hz | −1,9 (−4,0 a −0,9) | −12,6 | −4,6 | −7,4 | −7,8 | −1,9 | 2,5 |
| **NV4 madrugada: 70 % + graves 12 + corte 80** | −3,8 (−5,9 a −2,8) | −14,5 | −6,5 | −9,3 | −9,8 | −3,9 | 1,7 |
| NV4 con 60 % | −6,0 (−8,1 a −5,1) | −16,7 | −8,8 | −11,6 | −12,0 | −6,1 | 1,5 |
| 50 % sin nada más (referencia) | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 | 1,0 |
| NV6 prototipo de bajo psicoacústico (corte 80 + armónicos a −6 dB) | −0,5 (−1,3 a −0,1) | −6,0 | −0,5 | −3,4 | −3,1 | −0,3 | 1,2 |
| NV7 techo del limitador −3 dB | 0,0 | 0,0 | 0,0 | 0,0 | −0,1 | 0,0 | — |
| NV7 techo −6 dB | −1,2 (−2,0 a −0,4) | −1,2 | −1,3 | −1,2 | −1,5 | −1,2 | 1,1 |
| NV7 techo −6 dB y bombo «sin hundir» | −1,5 (−2,2 a −1,0) | −3,2 | −2,2 | −2,5 | −2,8 | −1,6 | 1,5 |
| NV7 grave en mono hasta 250 Hz | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | 0,0 | — |
| NV7 2 y 4 kHz −3 dB | −0,3 | 0,0 | 0,0 | 0,0 | 0,0 | −0,1 | — |
| NV7 el bajo psicoacústico de hoy al triple, con el corte a 80 | −0,9 (−2,7 a −0,2) | −5,9 | −1,4 | −3,8 | −3,5 | −0,7 | 1,6 |

**Por banda de tercio**, lo que baja afuera frente a la noche de hoy (igual con los dos muros: el muro le resta lo mismo a las dos):

| | 40 | 50 | 63 | 80 | 100 | 125 | 160 | 200 | 250 | 500 | 1k | 2k | 4k |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| NV1a graves de noche 8 dB | −3,9 | −3,8 | −3,7 | −3,2 | −2,5 | −1,9 | −1,0 | −0,5 | −0,2 | 0 | 0 | 0 | 0 |
| NV1b graves de noche 12 dB | −7,9 | −7,6 | −7,2 | −6,3 | −5,0 | −3,7 | −2,1 | −1,1 | −0,5 | 0 | 0 | 0 | 0 |
| NV2 corte a 80 Hz | −16,8 | −13,1 | −8,6 | −3,0 | −0,7 | −0,1 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| NV2 corte a 100 Hz | −22,8 | −19,9 | −15,0 | −7,5 | −2,9 | −0,8 | −0,1 | 0 | 0 | 0 | 0 | 0 | 0 |
| NV3 graves 8 + corte 80 | −20,6 | −16,9 | −12,3 | −6,2 | −3,2 | −2,0 | −1,0 | −0,5 | −0,2 | 0 | 0 | 0 | 0 |
| NV3b graves 12 + corte 80 | −24,3 | −20,7 | −15,8 | −9,2 | −5,7 | −3,9 | −2,2 | −1,1 | −0,5 | 0 | 0 | 0 | 0 |
| NV4 madrugada (70 %) | −26,2 | −22,6 | −17,7 | −11,1 | −7,6 | −5,8 | −4,1 | −3,0 | −2,5 | −2,0 | −1,9 | −1,9 | −1,9 |
| 50 % sin nada más | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 | −6,8 |
| NV6 prototipo de bajo psicoacústico | −21,5 | −14,6 | −9,0 | −2,6 | 0,0 | +0,8 | +0,7 | +0,1 | 0 | 0 | 0 | 0 | 0 |
| NV7 techo −6 y bombo | −0,9 | −3,3 | −4,2 | −3,3 | −3,0 | −2,2 | −1,5 | −1,3 | −1,1 | −1,0 | −0,8 | −0,8 | −0,7 |
| NV7 2 y 4 kHz −3 dB | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | −0,1 | −0,6 | −3,4 | −3,4 |

(La banda de 40 Hz casi no la dan las cajas: lo que se quita ahí cuenta poco.)

### NV1 — «Graves de noche» más hondo

**Qué es.** El mismo N1 de hoy (el estante de 120 Hz del local, que de noche baja con la misma bajada de una hora), con más dB: de 0 a
12 en vez de 0 a 6, y **8 dB de fábrica** en vez de 4.

**Qué se oye dentro.** Menos retumbe y menos cuerpo: el estante baja entero hasta 80 Hz, la mitad en 120 Hz y casi nada desde 250 Hz.
Con 8 dB, 0,9 dB menos de lo que se oye (hasta 1,9 en el reggaetón); con 12 dB, 1,6 (hasta 3,2). Las voces de hombre y la tuba quedan
algo más delgadas (en 160 Hz baja 1 dB con 8 y 2 dB con 12).

**Cuánto baja afuera.** Con 8 dB: 2 dB los graves, 2,6 los dB(C), 3 los golpes. Con 12: 3,9, 5,0 y 5,7. Es de lo que mejor rinde en los
graves que se oyen (2,2 a 2,4 veces lo que rinde bajar el volumen), porque baja también 100-160 Hz, que es lo que más pasa el muro y lo
que el oído más capta de los graves.

**Coste**: casi nada (el rango del mando, su valor de fábrica y su clave `noche_graves=`). **Riesgo**: bajo; sin brincos (es un filtro
fijo que entra en una hora, como hoy). **Interruptor**: el mismo mando «Graves de noche» de Ctrl+F3 → Sonido; 8 dB de fábrica.
**Programa**: XP y rocola-xp (y Spotify en rocola-xp, que pasa por la misma cadena).

### NV2 — Corte del grave más hondo de noche (el subgrave sube de 40 a 80 Hz)

**Qué es.** El modo billar ya quita lo de menos de 40 Hz con un corte de fase lineal (no mueve los picos). De noche, ese corte subiría
a 80 Hz, con la misma bajada de una hora (por ejemplo, mezclando el corte de 40 con el de 80 según el avance de la noche).

**Qué se oye dentro.** El bombo y el 808 pierden profundidad («pum» más seco), y las notas más graves del bajo se oyen por sus
armónicos. Lo que se oye baja poco (0,8 de media) salvo en lo de mucho grave hondo (2,6 en el reggaetón), pero el grave de 40-100 Hz
baja 6 dB.

**Cuánto baja afuera.** Muy bien el retumbe y los golpes (dB(C) −3,8, golpes −3,6: rinde 4,7 en dB(C)), poco los graves que se oyen
(−1,2), porque no toca 100-160 Hz. A 100 Hz rinde más (−5,5 dB(C), −2,2 graves) pero quita 11 dB de grave hondo dentro: 80 Hz es el
punto medio.

**Coste**: medio (el corte de fase lineal está en `GraveMono`, que es delicado, y tiene que cambiar con la hora; con sus pruebas
`subgrave`, `cadenaentera` y `compara-cadena`). Medido también **detrás** del bajo psicoacústico (como un filtro al final): da lo mismo,
porque el bajo psicoacústico casi no pone nada (NV6). **Riesgo**: bajo, sin brincos. **Interruptor**: nuevo, «Cortar el grave más hondo
de noche» (`noche_corte=` en Hz, 80 de fábrica; 0 lo apaga), en Ctrl+F3 → Sonido junto al modo noche, y que «Probar» lo incluya.
**Programa**: XP y rocola-xp.

### NV3 — NV1 y NV2 juntos (la que recomiendo)

Graves de noche 8 dB y el corte a 80 Hz, los dos con la bajada de una hora desde las 22 h. Se complementan: el estante baja 100-160 Hz
(lo que se oye afuera) y el corte quita lo hondo (lo que retumba y golpea). **Dentro −1,4 de lo que se oye** (cumbia, banda y balada
−0,6; rock −1,8; reggaetón −3,4) y **9,4 dB menos de grave hondo**. **Afuera: graves −3,0, dB(C) −5,7, golpes −5,8.** Para lograrlo con
el volumen habría que bajar el deslizador del 80 al 65 % (para los graves) o al 54 % (para los dB(C) y los golpes), y perder de 3 a 6
dB dentro en vez de 1,4. Las canciones que más pierden dentro son las que más molestan afuera (reggaetón: −5,3 graves y −11,2 dB(C)
afuera), que es justo lo que se busca. La variante con 12 dB (NV3b) baja 1,6 dB más los graves de afuera y quita 0,5 más dentro.

### NV4 — Más hondo de madrugada (desde la 1)

**Qué es.** A la 1 de la mañana empieza una segunda bajada, también de una hora: el deslizador a otro porcentaje (70 % propuesto),
graves de noche 12 dB y el corte a 80 Hz.

**Qué da.** Con 70 %: dentro −3,8 de lo que se oye y −14,5 de grave hondo; afuera graves −6,5, dB(C) −9,3, golpes −9,8, dB(A) −3,9
(frente a la noche de hoy). En la casa del vecino, los graves quedan como con el 50 % (17,9 contra 17,7 con el muro bueno) pero dentro
se oye 3 dB más fuerte que con el 50 %, y los dB(C) y los golpes quedan 2,5 a 3 dB más bajos que con el 50 %. Lo que no baja tanto es
el dB(A) (la voz y los medios): 24,5 contra 21,6. Con 60 % en vez de 70 %, todo 2,2 dB más abajo.

**Coste**: poco (otra hora y otro avance, con los mismos mecanismos). **Riesgo**: bajo; la bajada de una hora no se nota. **Interruptor**:
«Más bajo de madrugada» (`noche_madrugada=1`, `noche_madrugada_desde=1`, `noche_madrugada_pct=70`), encendido de fábrica, con su propio
deslizador de 50 a 90 %. **Programa**: XP y rocola-xp.

### NV5 — Los videos con el mismo trato

**Lo que pasa hoy.** Los videos no pasan por la cadena: de noche solo se les baja la perilla del reproductor «al pct %» de lo que iba.
Si esa perilla es lineal (es lo que supone la cuenta de la rocola, y lo que hace rocola-xp con mpv: `ReproductorVideo.cs` le pone la raíz
cúbica a mpv para que sea lineal), al 80 % el video baja 1,9 dB y la música 3,2; **al 50 %, el video baja 6 dB y la música 10.** Y a los
videos no les llega el «Graves de noche».

**La propuesta.** (a) Que la perilla del video baje los mismos dB que la música (`10^(dB de la noche × avance / 20)` en vez del %):
cuenta chica, en los dos programas. (b) Solo en rocola-xp: de noche, a mpv un filtro de audio con el mismo estante y el mismo corte
(mpv deja ponerle filtros de libavfilter, como `lowshelf` y `highpass`, por su propiedad `af`); en XP el reproductor de Windows Media
solo recibe el volumen desde la rocola. **Sin medir** (los videos no pasan por el banco); los números de la música valen como guía.
**Coste**: (a) poco, (b) medio. **Riesgo**: (a) ninguno; (b) que cambiar el filtro de mpv a media reproducción dé un chasquido: ponerlo
al empezar cada video. **Interruptor**: ninguno nuevo (va con `noche=` y los de NV1/NV2). **Programa**: (a) los dos, (b) rocola-xp.

### NV6 — Un bajo psicoacústico de noche (prototipo, no lo recomiendo por ahora)

**Lo que pasa hoy.** El bajo psicoacústico de la rocola pone muy poco: lo que añade queda **de 24 a 34 dB por debajo de la música** en
cada banda de 50 a 315 Hz (al triple, de 14 a 24 dB), así que casi no cambia nada que se pueda medir (al triple: dentro 0,0 LUFS,
afuera de 0,0 a +0,4 dB). Lo que más pone, en proporción, es por debajo de 40 Hz (su paso-alto de 95 Hz es de primer orden), pero aun
al triple eso queda de 44 a 52 dB por debajo del total de la música y las cajas no lo dan: no molesta ni ayuda. Así que **no puede
«devolver» el grave que se le quite de noche.**

**El prototipo** (en Python sobre la salida de la cadena; no está en la rocola): corte a 80 Hz y, en su lugar, los armónicos 2.º y 3.º
del grave quitado, sin su fundamental, entre 100 y 300 Hz, con una ganancia fija para toda la canción (no sube ni baja con el bombo).
Con los armónicos a −6 dB de lo quitado: dentro se recuperan 0,3 dB de lo que se oye frente al corte solo (−0,5 en vez de −0,8), y
afuera se pierde 0,7 dB del logro en los graves (−0,5 en vez de −1,2): **lo que devuelve dentro también lo devuelve afuera**, porque
los armónicos caen justo en 100-300 Hz, que el muro deja pasar más que 50 Hz y que el oído pesa más. Los golpes y los dB(C) se
quedan casi igual de bajos (−3,1 y −3,4). Si se quiere probar de oído, que sea con interruptor apagado de fábrica. **Coste**: alto
(una etapa nueva). **Riesgo**: que suene a bocina rota si se pasa. **Programa**: los dos.

### NV7 — Lo que medí y no sirve

- **El techo del limitador más bajo de noche**: con el modo noche el volumen ya baja antes del limitador, que casi no trabaja; bajar el
  techo 3 dB **no hace nada**. Con 6 dB sí baja (−1,3 graves afuera por −1,2 dentro: como el volumen) pero el volumen entero **sube y
  baja 2,9 dB** de una ventana a otra (los filtros fijos: 0,3 a 1,4, lo mismo que el N1 de hoy, y solo porque cada instante trae más o
  menos grave). Es lo que quitaste el 18-11 por los brincos. Descartado.
- **El bombo «sin hundir» con ese techo**: baja algo más el grave (−2,2 graves, −2,8 golpes afuera) pero el grave sube y baja 6 dB.
  Descartado por lo mismo.
- **El grave en mono hasta 250 Hz**: 0 dB. El grave ya va en mono hasta 120 Hz, y la energía que sale por las bocinas es la misma.
- **Menos 2-5 kHz** (−3 dB en 2 y 4 kHz): a través de un muro, 0 dB afuera (el muro ya quita 60-70 dB ahí); dentro −0,3. Solo
  ayudaría con la **puerta o ventana abierta** (−0,7 dB(A) en la calle), y por eso no compensa.
- **Subir el bajo psicoacústico de hoy**: ver NV6; al triple con el corte a 80, igual que el corte solo.
- **Una curva de noche por el ecualizador del dueño** (−12 en el estante de 31 Hz, −9 en 63, −3 en 125): rinde como NV1 (2,3) pero con
  la campana de 63 Hz no llega tan hondo como el corte, y ese ecualizador es del dueño; mejor NV1 y NV2 aparte.

### NV8 — Lo que no es del programa (y probablemente pesa más)

No se puede medir desde aquí, pero las fuentes coinciden:

- **Puertas, ventanas y rendijas.** El aislamiento de una pared con un hueco lo manda el hueco: con un 1 % de la superficie abierta (un
  hueco de 32 × 32 cm en una pared de 10 m²) un muro de 48 dB queda en **20 dB**; con un 0,1 %, en 30 dB (la cuenta de la transmisión
  compuesta). Con la puerta abierta a la calle, lo de afuera es lo de dentro menos la distancia, con todas sus frecuencias: ahí sí cuenta
  el dB(A) (la NOM-081) y lo único que lo baja es el volumen.
- **Dónde están las cajas.** Cada superficie pegada a una caja le sube el grave hasta 6 dB (muro, piso, esquina: hasta 18 dB frente a
  la caja en el aire; en la práctica menos) [3, 20]. Una caja de 12" pegada al muro del vecino o en una esquina mete más grave en el
  cuarto que da al vecino, y si toca el muro o el piso mete vibración por la estructura, que se salta el muro. Separarla del muro del
  vecino, sacarla de la esquina, subirla en un soporte o ponerle algo blando que no transmita la vibración (sin que toque el muro) son
  arreglos de grave que el programa no puede hacer.
- **El amplificador.** Si tiene perilla de graves o «loudness», de noche hace lo mismo que NV1 a mano. Si una sola caja da al vecino,
  un atenuador de bocina (L-pad, que conserva la impedancia) en esa caja baja **todo** lo que esa caja le manda (−6 dB en esa caja son
  −6 dB de esa caja afuera), cosa que ningún filtro del programa puede hacer sin bajar todas las zonas. Con varias bocinas en paralelo
  en el mismo borne, cualquier cambio de cableado hay que hacerlo cuidando la carga del amplificador.

## 5. Recomendación

1. **NV8 primero**: revisar puertas, ventanas y rendijas de noche, y dónde y cómo están las cajas que dan al vecino (sobre todo la del
   entrepiso o la que esté más cerca de su muro). Es gratis y puede valer más que todo lo del programa.
2. **NV3** desde las 22 h con la misma bajada de una hora: graves de noche a 8 dB (NV1) y el corte a 80 Hz (NV2). Afuera −3 dB los
   graves, −5,7 los dB(C) y −5,8 los golpes más que hoy, por −1,4 dentro. En los dos programas.
3. **NV4** desde la 1: 70 % (o el que elijas en el deslizador nuevo), graves 12 dB y el corte a 80. En los dos programas.
4. **NV5 (a)**: los videos bajan los mismos dB que la música. Y (b) en rocola-xp, el mismo filtro de graves a los videos.
5. No hacer NV6 ni lo de NV7.

El deslizador de 50 a 90 % sirve igual con todo esto: el porcentaje sigue siendo de lo que se oye; NV1 a NV4 solo cambian el grave.

## 6. Lo que no se pudo medir

- **Nada se oyó**: los LUFS son un modelo de lo fuerte que se oye; cuánto se nota la falta de grave dentro (−9 dB en 40-100 Hz con
  NV3) lo tienes que decidir de oído, con «Probar».
- **Canciones de verdad**: solo sintéticas. Las de mucho grave hondo (reggaetón, banda con tuba, electrónica) pierden más dentro y
  bajan más afuera; las delgadas (cumbia vieja, boleros) casi no cambian.
- **El local**: la respuesta real de las cajas y de la nave, dónde está el vecino, cómo es el muro de verdad (y si hay puentes por la
  estructura, ventanas o huecos), el nivel real dentro (supuse 85 dB(A) de día) y el ruido de fondo de la casa del vecino.
- **El A4 y el bar**: no se probó nada allí.
- **rocola-xp**: no corrí el banco con `rocola-xp.dll`; la cadena es el mismo código (solo líneas de traducción), pero no está medido.
- **Los videos**: no pasan por el banco; tampoco comprobé cómo responde la perilla del reproductor de Windows Media en XP.
- **La bajada de una hora del corte (NV2) y la de la madrugada (NV4)** no existen: se midió el punto final de cada una.
- **El prototipo de bajo psicoacústico (NV6)** está hecho fuera de la rocola, sin tiempo real.
- **Las fuentes**: desde aquí solo pude buscar (no abrir los PDF). Lo de la OMS sobre los 10 dB entre C y A lo cito del texto de
  sus guías de 1999 como lo conozco; conviene comprobarlo en el documento. Los valores del umbral de audición son los de la ISO 226:2003.

Para repetir las medidas: `sh pruebas/nochevecinos.sh /una/carpeta/de/trabajo` (unos 5 minutos; deja `analisis.json` y las tablas).

## Fuentes

1. La ley de masa del aislamiento: R = 20·log(m·f) − 47 dB (incidencia de campo). En cualquier texto de acústica de edificios, por
   ejemplo M. Long, *Architectural Acoustics* (2.ª ed., Academic Press, 2014), o D. Bies y C. Hansen, *Engineering Noise Control*.
2. EN ISO 12354-1 (predicción del aislamiento entre recintos), anexo B: Rw ≈ 37,5·log(m′) − 42 para muros macizos de más de 150 kg/m²
   (250 kg/m² → 48 dB).
3. Ruido por la estructura y bocinas pegadas a muros y pisos: «Structure-borne sound», *Designing Buildings Wiki*
   (https://www.designingbuildings.co.uk/wiki/Structure-borne_sound); y sobre el grave cerca de las paredes, «The impact of boundaries
   on loudspeaker performance», *ProSoundWeb* (https://www.prosoundweb.com/the-impact-of-boundaries-on-loudspeaker-performance/).
4. ISO 717-1 (índice global del aislamiento al ruido aéreo): su curva de referencia de 100 a 3150 Hz y el espectro ampliado desde 50 Hz.
5. OMS, *Guidelines for Community Noise* (1999): dormitorio de noche 30 dB LAeq y 45 dB LAmax; el ruido con mucha baja frecuencia
   necesita valores más bajos; la nota de los 10 dB entre dB(C) y dB(A) (https://www.who.int/publications/i/item/a68672).
6. G. Leventhall, «Low frequency noise and annoyance», *Noise & Health* 6(23), 59-72 (2004).
7. Agencia de Medio Ambiente de Dinamarca (Miljøstyrelsen), orientación n.º 9/1997 sobre ruido de baja frecuencia: 20 dB LpA,LF de las
   18 a las 7 h en viviendas (5 dB menos si es a golpes); descrita en *Laboratory Evaluation of Annoyance of Low Frequency Noise* (2002),
   https://www2.mst.dk/udgiv/publications/2002/87-7944-955-7/html/kap04_eng.htm
8. ISO 717-1, el término de adaptación Ctr: para tráfico urbano, «música de discoteca» y ruido de fábricas (citado, entre otros, en
   https://info.cype.com/en/new-feature/spectrum-adaptation-term/).
9. H. K. Park y J. S. Bradley, «Evaluating standard airborne sound insulation measures in terms of annoyance, loudness, and audibility
   ratings», *J. Acoust. Soc. Am.* 126(1) (2009), doi:10.1121/1.3147499.
10. V. Hongisto, D. Oliva y J. Keränen, «Subjective and objective rating of airborne sound insulation – living sounds», *Acta Acustica
    united with Acustica* 100, 848-863 (2014); y la tesis de D. Oliva (Universidad de Turku, 2022).
11. Control de rango de Dolby (modos «Line» y «RF»; el RF para escuchar de noche sin molestar a los vecinos): «DialNorm 101»,
    *TV Technology* (https://www.tvtechnology.com/opinions/dialnorm-101) y el manual del Dolby DP570.
12. Modos noche de receptores y barras de sonido: Sony, «Enjoying clear sound at a low volume level [Night Mode]»
    (https://helpguide.sony.net/ha/strdh59/v1/en/contents/TP0001553279.html); RTINGS, *Sound enhancement features*.
13. Institute of Acoustics (Reino Unido), *Good Practice Guide on the Control of Noise from Pubs and Clubs* (2003).
14. D. Fiumicelli y otros, «Low frequency sound at outdoor concerts – a necessary noise», *Proc. IOA* 46 pt 2 (2024): los códigos
    miran las bandas de 63 y 125 Hz.
15. NTi Audio, «Drop the bass while managing compliance» (2022): limitadores en escala C para que vean el grave.
16. NOM-081-SEMARNAT-1994, tabla 1 de su modificación (DOF, 3-12-2013): zonas de casas, 55 dB(A) de 6 a 22 h y 50 dB(A) de 22 a 6 h
    (https://dof.gob.mx/normasOficiales/5261/SEMARNAT_03122013/SEMARNAT_03122013.html).
17. NADF-005-AMBT-2013 (Ciudad de México), Gaceta Oficial del 29-12-2014: 65 dB(A) de 6 a 20 h y 62 dB(A) de 20 a 6 h en la
    colindancia de la fuente (https://paot.mx/centro/normas_a/2024/GODF_29_12_2014_NADF-005-AMBT-2013.pdf).
18. E. Larsen y R. M. Aarts, «Reproducing low-pitched signals through small loudspeakers», *J. Audio Eng. Soc.* 50(3), 147-164 (2002).
19. ISO 226:2003 (curvas de igual sonoridad; el umbral de audición de la ISO 389-7).
20. R. F. Allison, «The influence of room boundaries on loudspeaker power output», *J. Audio Eng. Soc.* 22(5) (1974).
