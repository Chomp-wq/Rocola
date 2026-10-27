# La cadena del modo billar (27 de octubre de 2026)

## Lo que pidió el dueño

> «Implementa todo con cuidado, lo que beneficie a mi rocola, cosas como cortar silencios largos y cosas por el estilo no las
> implementes. Ponlo en el modo automático para la rocola, para que no haya tantas configuraciones de sonido, quítalas todas y ponlas en
> una sub ventana al lado del interruptor del modo automático de la rocola [...] además, busca mejoras en el autoEQ [...] esos ajustes
> del autoEQ entrarán en funcionamiento al activar el modo del billar.»

Y después: el modo de la rocola es la misma cadena de audio, revisada pieza por pieza y ajustada para las bocinas del billar, con cuidado
de que las piezas no choquen entre sí.

Decisiones que tomó en la ventana de preguntas:

- **Solo lo esencial a la vista:** en la página de Sonido quedan el volumen, el modo billar con su botón «Ajustes del sonido...» al lado,
  la prueba de bocinas y la biblioteca. Todo lo demás está en la hoja de ajustes.
- **Solo las técnicas seguras:** no hay recorte suave ni se quita el eco de los vivos. Tampoco se cortan silencios ni finales.

## Qué es el modo billar ahora

Es el interruptor de siempre (`local_auto=`, apagado de fábrica). **Con el modo apagado, la cadena sale bit a bit como antes:** 10 de 10
combinaciones son iguales contra el ejecutable anterior (c061cd6), en los perfiles con y sin tarjeta gráfica.

Con el modo encendido, la rocola ajusta cada pieza de la cadena para tus bocinas: dos medios de 6,5" y dos tweeters de corneta en el
gabinete, más tres cajas de 10 a 12" repartidas por zonas. No hay subwoofer, y la nave es de lámina, ladrillo y loseta. Lo que el modo
decide queda en gris en las hojas y dice por qué. El interruptor guardado de cada pieza se conserva y vuelve al apagar el modo.

| Pieza | En el modo billar | Por qué |
|---|---|---|
| EQ automático | Siempre en automático, con reglas para estas bocinas (ver abajo) | El EQ de siempre chocaba con la claridad y el realce |
| Bajo psicoacústico | En automático | Sin subwoofer, es lo que devuelve la sensación de grave |
| Realce de brillo | Solo devuelve lo que el MP3 cortó (0 en una canción completa), con la cantidad de fábrica | Encima de 5,6 kHz chocaba con el EQ, que ahí baja |
| DSEE | No | Recrea 16 a 20 kHz: las cornetas casi no dan esa banda y el local se la come. Ahorra procesador en el A4 |
| Quitar el siseo (I2) | No | A volumen de bar, el ruido del local tapa el siseo; quitarlo se llevaría aire de la música |
| A la medida (I1) | Sí | El EQ ya usa el objetivo de cada canción; así el bajo se mide con la misma vara. Sin el modelo del EQ no cambia nada |
| Canal mudo, continua, zumbido | Sí | Solo actúan en las canciones con ese defecto |
| Subgrave, estéreo, partes suaves | Como antes (L1/L5) | — |
| Claridad | Sí, **menos en las canciones flacas** | A una canción con poco cuerpo, cortar 300 Hz la dejaba más delgada |
| Presencia | Por el LRA, como antes, **y ahora con la compensación entera** | Ver abajo |
| Suavizar las eses | Siempre, con la referencia del billar | Ver abajo |
| Retumbe dinámico y ataque más claro | Siempre | — |
| Que el bombo no hunda la voz | **No** | Ver abajo |
| Saltar silencios, balance, frecuencia de la tarjeta | No los decide el modo | Son de la instalación o del uso, no del sonido de cada canción |

### El EQ automático para estas bocinas

El EQ de siempre corrige cada canción hacia «lo bien grabado» de la biblioteca, ±4 dB por banda. Medido con la cadena entera, en el billar
chocaba con otras piezas:

- **50 Hz:** subía hasta +3,4 dB, donde ninguna bocina del billar da casi nada. En las grabaciones viejas cortadas en grave subía el
  retumbo de la cinta. Ahora solo **baja**, y no se toca si la grabación viene cortada por debajo de 60 Hz.
- **400 Hz:** junto con la claridad (−2,5 dB a 300 Hz) llegaba a −5,3 dB. Ahora baja como mucho 2,5 dB.
- **2,5 y 5,6 kHz:** con la claridad y el realce llegaba a +5 dB, justo en la franja áspera que la loseta y la lámina devuelven. Ahora
  5,6 kHz solo baja y 2,5 kHz sube como mucho 1 dB.
- **15,8 kHz:** no se toca. A esa frecuencia el filtro sale torcido y las cornetas ya no dan.
- **Un poco menos de agudos de fábrica:** el objetivo baja 1,5 dB a 5,6 kHz y 1 dB a 10 kHz, por el rebote del local.
- **Más prudencia:**
  - Una banda solo se toca si queda claramente por debajo del corte del codec.
  - Una banda casi muda no se toca.
  - Una canción de menos de 45 s no se ecualiza.
  - Una canción con los picos aplastados no recibe refuerzos por encima de 2 kHz.
  - Una canción rara se toca con menos fuerza.
  - Lo que se corregiría en menos de 0,4 dB se deja.

Todo está en `Rocola/CadenaBillar.cs`, en tablas fáciles de cambiar.

### Más cuerpo para las canciones flacas

A una canción le **falta cuerpo** cuando sus bandas de 158 y 400 Hz están, de media, 2,5 dB o más por debajo de su objetivo. A esas canciones:

- no se les pone la claridad;
- el EQ puede subirles 158 y 400 Hz hasta 3 y 2,5 dB.

En un disco seguido decide el disco entero, como con el LRA.

Se pensó en hacerlo generando armónicos en la zona de la voz, como el bajo psicoacústico pero una octava más arriba. **No se hizo:** en
una nave de lámina y loseta, los armónicos entre 500 Hz y 3 kHz suman aspereza. Si el cuerpo devuelto llegara a retumbar, el retumbe
dinámico lo recorta en ese instante.

### La presencia y las partes suaves miden contra la propia canción

Las dos se pensaron (22-10) cuando el nivelado llevaba todas las canciones al mismo volumen. Desde el nivelado al límite (24-10), cada
canción suena muy por encima de ese volumen, y las dos fallaban:

- La presencia apretaba casi toda la canción. Como su compensación estaba topada por los picos, la dejaba **más baja** en vez de
  acercar lo suave a lo fuerte.
- Las partes suaves casi nunca encontraban nada tan bajo.

En el billar, la referencia es el nivel al que suena **esta** canción. Además, la presencia puede dar su compensación entera (3 dB): el
limitador va detrás. Por ejemplo, en la balada la presencia baja de media 2,2 dB y devuelve 3,0.

### Las eses

La detección de la «s» comparaba su energía con la de toda la canción. Con el grave presente casi nunca se disparaba, mientras la
claridad (+1,5 dB a 3 kHz) acercaba la «s» a la voz. En el billar se compara con lo que suena por encima de 1 kHz (la voz y su brillo).

### Por qué el bombo no

Con cada canción subida hasta su límite, el limitador casi no actúa (0,2 a 2 % del tiempo en masters normales). La etapa le quitaba
~1 dB de bajo a la canción sin mejorar la voz: reacciona después del primer golpe, y ese golpe ya lo bajó el limitador. Su interruptor
sigue en la hoja «Voz, golpe y retumbe», para probarlo a mano con el modo apagado.

## La ventana

- **Página de Sonido:**
  - «Volumen al límite», igual que antes.
  - La tarjeta «El billar»: el interruptor **Modo billar**, el botón **Ajustes del sonido...** al lado, un texto que dice qué hace el
    modo, y la **prueba de bocinas** (izquierda, derecha, las dos y polaridad).
  - La biblioteca y el tipo de disco, igual que antes.
- **Ajustes del sonido...** (la hoja nueva):
  - A la izquierda, «Calidad del sonido»: EQ, graves, realce, DSEE y saltar silencios.
  - A la derecha, los botones de las demás hojas:
    - El local y sus bocinas (y desde ahí, Tono y presencia).
    - **Voz, golpe y retumbe** (la hoja nueva, con los tres interruptores que aún no tenían mando).
    - Lo que suena ahora.
    - Mejoras con IA.

  Cada hoja vuelve a esta con su botón «Volver a los ajustes del sonido».
- Con el modo encendido, lo que decide el modo va **en gris** y su nota dice «Necesita: el modo billar apagado (página de Sonido)». En
  EQ, graves y realce, la nota dice lo que hace el modo y el valor muestra lo que de verdad usa.
- La frecuencia de la tarjeta quedó en la hoja del local, en la tarjeta que antes era la de la prueba.

## Lo medido

Medido con un banco de la cadena entera: cinco canciones sintéticas de 50 s pasan por `OpenAudio` + `ArmaCadena` del ejecutable real.
El resultado es la sonoridad de salida, el cambio por zonas y el limitador. «Eses frente a voz» es cuánto queda la «s» por encima o por
debajo de la voz, comparado con la entrada; negativo es más suave.

| Canción | Configuración | Sonoridad | 140-500 Hz | 5-10 kHz | Limitador >1 dB | Eses |
|---|---|---|---|---|---|---|
| Balada (mucho rango) | Fábrica | −11,4 LUFS | +5,1 | +7,9 | 11,1 % | +2,5 |
| | Billar antes de hoy | −11,4 | +4,0 | +9,1 | 0,8 % | +3,8 |
| | **Billar ahora** | −13,1 | +3,3 | +3,0 | 0,2 % | **−1,1** |
| Cumbia vieja delgada | Fábrica | −11,1 | +4,1 | +3,0 | 13,9 % | +0,6 |
| | Billar antes | −12,3 | +1,9 | +4,0 | 6,2 % | +1,8 |
| | **Billar ahora** | −11,6 | **+3,0** | +1,9 | 8,8 % | **−1,2** |
| Pop con eses | Fábrica | −9,3 | −0,9 | −4,3 | 1,4 % | −3,2 |
| | **Billar ahora** | −9,8 | −2,7 | −6,6 | 1,2 % | **−4,7** |

- **Los agudos ya no se apilan.** En la balada, la franja de 5 a 10 kHz pasa de +9,1 a +3,0 dB.
- **La «s» se suaviza en todas las canciones.** Antes de hoy, en el billar la «s» quedaba de +0,5 a +3,8 dB por encima de la voz.
- **La cumbia flaca conserva el cuerpo:** sin la claridad y con el EQ, 140-500 Hz pasa de +1,9 a +3,0 dB.
- **El billar suena de 0,3 a 1,7 LU más bajo que de fábrica.** La razón es que ya no lleva los refuerzos de agudos y graves del EQ de
  fábrica. Si en el local se nota, se sube el amplificador: la cadena no aplasta más por eso.
- **Procesador:** la cadena del billar gasta unos 33 a 44 ms por segundo de música en Mono/Linux, contra 22 a 28 de fábrica. Es
  el 3 a 4 % de un núcleo; en el A4 no se ha medido.

Con el modo apagado, las 10 combinaciones de `pruebas/compara-cadena.cs` dan los mismos bits que c061cd6, en CPU y en GPU.

## Pruebas

- `pruebas/cadenabillar.cs` (nueva, 29 comprobaciones): cubre el EQ del billar banda por banda; `EqCorrFor` con una huella de verdad
  (encendido y apagado); lo que decide el modo para el realce, el DSEE, los arreglos y el bajo; el código de la cadena; y la ventana.
- Puestas al día para lo nuevo: `localauto` (el bombo ya no va en el billar; van las eses; la claridad no se pone a las flacas),
  `bombosinhundir`, `retumbedinamico` y `ataqueclaro`.
- Suite completa (`sh pruebas/todo.sh`): los únicos fallos son los que ya había antes de este cambio (`sala`, `discoacotado` y
  `caratulasadaptativas`). `ventanaf3` y `modosauto` se pusieron al día para la ventana nueva y pasan. `bombosinhundir` falló una vez
  en la tanda entera, mientras corría a la vez otra medición pesada; sola pasa siempre.

**Todo son cuentas con señales sintéticas en Linux/Mono. Nada de esto se ha oído en el billar ni se probó en el A4 con Windows XP.**

## Qué escuchar en el billar

1. **Agudos:** el modo billar debería sonar menos «chillón» que antes en las canciones modernas (platillos, «s»). Si se queda opaco,
   la tabla `InclinacionBillar` de `CadenaBillar.cs` es la primera que hay que tocar.
2. **Voz en canciones viejas:** las cumbias y boleros delgados deberían tener más cuerpo. Si alguna retumba, decirlo.
3. **Baladas:** las partes suaves y la voz en la estrofa deberían quedar más cerca de lo fuerte.
4. **Grave:** sin el bombo, el grave debería quedar más lleno. Si la voz se hunde con cada golpe, se puede volver a poner.
5. **Comparar:** «Lo que suena ahora» → «Escuchar sin mejoras» quita todo durante 15 s, para comparar con el oído.

## Lo que no se hizo

- El recorte suave y quitar el eco de los vivos: el dueño eligió solo las técnicas seguras.
- Cortar silencios largos o finales: el dueño lo pidió expresamente.
- Optimizar el filtro del grave en mono para gastar menos procesador. Funciona, pero el cambio no se puede verificar sin el A4.
