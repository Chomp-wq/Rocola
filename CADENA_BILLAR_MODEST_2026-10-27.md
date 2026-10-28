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

## Segunda tanda (el mismo día): lo que el dueño eligió del banco

El dueño pidió más mejoras para la cadena nueva. Tras medir con el banco, eligió las cuatro propuestas:

### 1. Presencia más llena

En una canción de mucho rango, la presencia aprieta lo fuerte más de 3 dB. Como solo devolvía 3, lo fuerte quedaba más bajo que sin
ella: la balada daba −13,1 LUFS con la presencia y −12,1 sin ella. Ahora, en el billar, devuelve hasta **5 dB**
(`PresenciaSala.CompensaBillarDb`). La balada queda en −11,5 LUFS, casi como de fábrica (−11,4), y lo suave queda más cerca de lo fuerte.
El limitador pasa de actuar el 0,2 % al 3,2 % del tiempo (de fábrica, el 11 %).

### 2. Corte de grave a 40 Hz

Las cajas de 12" llevan puerto. Por debajo de la sintonía del puerto (en cajas así, unos 45-50 Hz) el cono se queda sin freno, y lo que
llega ahí solo gasta amplificador y distorsiona. En el billar, el subgrave corta a **40 Hz** en vez de 30 (`SubgraveBillarHz`). Si
alguien puso a mano un corte más alto, manda el suyo. En el banco quita 1 dB por debajo de 45 Hz y de 0,3 a 0,6 dB entre 45 y 140 Hz.
La sensación de grave la sigue dando el bajo psicoacústico. En el reggaetón deja 0,7 dB de margen sin usar bajo el techo: es lo que se
deja de mandar a los conos.

### 3. Afinar el local con el celular

Es una hoja nueva: Ajustes del sonido → El local y sus bocinas → «Afinar el local con el celular...». El código está en `AfinarLocal.cs`.

- **El ruido:** la rocola, callada, toca ruido rosa por bandas de una octava, de 63 Hz a 8 kHz. Sale a −20 dB eficaces por canal, y
  cada canal lleva su propio ruido, para que dos bocinas no se sumen y se resten según dónde esté uno.
- **La medida:** el dueño, parado donde está la gente, mueve una barra por banda hasta lo que marca la app de decibeles, y dice si la
  app mide en A, C o Z. A la ponderación A se le devuelve lo que le quita al grave (26 dB a 63 Hz).
- **La corrección:** se compara con una curva de casa para un bar, contando desde la zona de la voz (250 Hz a 2 kHz):
  - corrige el 70 %;
  - deja lo de menos de 1 dB;
  - baja como mucho 6 dB y sube como mucho 3;
  - en 8 kHz sube como mucho 2 dB, y en 63 Hz nunca sube.
- **Cómo se aplica:** con el modo billar y el interruptor «Usar la afinación» encendidos, la corrección va dentro del EQ de cada
  canción. Usa las mismas ocho bandas, sin filtros nuevos ni procesador de más, y sustituye la inclinación de agudos estimada. Va
  también en las canciones que el EQ no toca.
- **Lo que se guarda:** `afinado_local=` (las ocho lecturas), `afinado_ponderacion=` y `afinado_usar=` (apagado de fábrica).
  «Dejar como de fábrica» apaga el interruptor y guarda lo medido.

### 4. Menos procesador

El subgrave quita lo que hay por debajo del corte con un filtro de fase lineal muy largo: 117 ms a 30 Hz, calculado por FFT. Ahora lo
que quita se calcula con la ventana diezmada 16 veces (a unos 2,8 kHz) y se devuelve a la tasa entera (`GraveMono.GraveDeDiezmado`).

- **Diferencia con el cálculo entero:** sale igual a menos de −85 dB. En la cadena entera, como mucho −98 dBFS.
- **Costo de la etapa sola:** de unos 11 a unos 3 ms por segundo de música a 30 Hz.
- **Toda la cadena del billar:** de 31-43 a 25-35 ms/s en Mono; la de fábrica gasta 24-30.

Es automático, sin interruptor: no cambia nada que se oiga.

### Lo medido después

| Canción | Fábrica | Billar (1.ª tanda) | Billar ahora |
|---|---|---|---|
| Pop | −9,5 LUFS | −10,0 | −10,2 |
| Reggaetón | −11,7 | −12,0 | −12,5 |
| Balada | −11,4 | −13,1 | **−11,5** |
| Cumbia | −11,1 | −11,6 | −11,6 |
| Pop con eses | −9,3 | −9,8 | −10,0 |

Con el modo apagado y todo de fábrica, la cadena sale bit a bit como antes. Con el subgrave puesto **a mano**, la diferencia del
cálculo a menos resolución queda por debajo de −98 dBFS. `pruebas/compara-cadena.cs` ahora la cuenta como «casi igual» si está por
debajo de −80 dBFS; con el cálculo entero forzado, las 10 combinaciones salen idénticas.

Pruebas de la segunda tanda:

- `pruebas/cadenabillar.cs` suma la presencia, el corte de 40 Hz, la afinación (la corrección, la ponderación, las bandas del EQ, dentro de `EqCorrFor` y la configuración) y la ventana.
- `subgrave` se puso al día.
- Suite completa: los únicos fallos son los de antes (`sala`, `discoacotado` y `caratulasadaptativas`).
- `ventanareal`, una prueba de tiempos, falló una vez en la tanda entera (191 ms contra 150); sola pasó dos de dos.

Para la afinación, conviene medir con la app en C o Z y en lento, con el local como siempre, de pie donde se escucha y no pegado a una
bocina.

## Tercera tanda: volumen parejo, varios puntos y el bajo según lo medido

El dueño pidió más mejoras y eligió tres de las cuatro propuestas. La cuarta, afinar cada zona aparte, queda pendiente de saber cómo están
conectadas las cajas.

### 1. Volumen parejo aprendido

El nivelado al límite mide los picos de cada canción en su archivo, con márgenes calibrados para la cadena de fábrica. En el billar la
cadena cambia esos picos: el corte de 40 Hz y el EQ le quitan grave, y la presencia los mueve según la canción. Por eso a unas canciones
les sobra margen bajo el techo y se quedan más bajas que las demás. Al reggaetón del banco le sobraban 1,8 dB. El código está en
`VolumenParejo.cs`.

- **Medir:** en el billar, una etapa pegada delante del limitador apunta el pico de cada bloque de 100 ms en un histograma. No toca ni
  una muestra ni reserva memoria al sonar.
- **Aprender:** al soltar la canción, solo si sonó entera desde el principio y sin «Escuchar sin mejoras». Saca sus P90, P99 y el pico
  mayor frente al techo, y busca que queden en +0,75, +3 y +5 dB:
  - en cada vuelta se acerca el 70 % a eso;
  - nunca pasa de ±2 dB;
  - deja lo que cambiaría menos de 0,25 dB.
- **Usar:** la siguiente vez que suena en el billar. En racha con otra de su disco usa el menor del disco (una pista sin aprender cuenta
  como 0), así el disco sigue sonando parejo entre sí.
- **Guardar:** en `rocola_billar_nivel.txt`, una línea por cada cosa aprendida. Al arrancar se lee en segundo plano (no frena la pantalla) y, si creció mucho, se reescribe.

En el banco, en 2 o 3 vueltas:

| Canción | Primera vez | Aprendido | Después |
|---|---|---|---|
| Pop | −10,2 LUFS | +0,2 dB | −10,1 |
| Reggaetón | −12,5 | +1,7 dB | **−11,1** |
| Balada | −11,5 | +1,3 dB | −11,0 |
| Cumbia | −11,6 | −0,2 dB | −11,7 |
| Pop con eses | −10,0 | +0,1 dB | −10,0 |

La diferencia entre la más fuerte y la más baja pasa de 2,5 a 1,7 LU. El limitador nunca pasa del 7 % del tiempo (de fábrica, hasta 14 %).

### 2. Afinar en varios puntos

En una nave con eco, la lectura del celular cambia de 2 a 4 dB de un sitio a otro. La hoja de afinar tiene ahora **Punto 1, 2 y 3** y el
botón «Borrar este punto». La afinación usa el promedio por banda de los puntos que tengan esa banda. Se guardan en `afinado_local=` (el
de siempre), `afinado_local2=` y `afinado_local3=`.

### 3. El bajo según lo medido

Con la afinación en uso, la rocola sabe cuánto grave dan de verdad las bocinas a 63 Hz frente a la zona de la voz y a la curva de casa.

- Si dan poco, el bajo psicoacústico sube su cantidad; si dan de sobra, la baja.
- El micrófono de un celular suele leer de menos a 63 Hz, así que los primeros 4 dB que falten no cuentan, ni 2 dB de sobra.
- Va entre 0,75 y 1,4 veces lo de siempre.

Solo actúa con el modo billar y la afinación encendidos.

### Pruebas de la tercera tanda

- `pruebas/cadenabillar.cs` suma:
  - las cuentas del volumen parejo;
  - el oído, que no toca ni una muestra y solo aprende con la canción entera;
  - el promedio de los puntos y su configuración;
  - el factor del bajo;
  - los ganchos en el código.
- Con el modo apagado, `compara-cadena` sigue dando 0 combinaciones distintas sin esperarlas.
- Suite completa: los únicos fallos son los de antes (`sala`, `discoacotado` y `caratulasadaptativas`). `bloqueos` avisó que la lectura de
  `rocola_billar_nivel.txt` frenaba la pantalla al arrancar; ahora se lee en segundo plano y pasa.

## Cuarta tanda: el volumen parejo desde la primera vez y las bandas seguidas

### 1. Volumen parejo desde la primera vez

El volumen parejo aprendido solo servía después de que una canción sonaba entera. Las nuevas, o las que siempre se saltan, se quedaban
sin nada. Ahora, con al menos 30 canciones aprendidas, la rocola ajusta una recta de lo aprendido frente a tres rasgos que ya mide el
repaso:

- cuánto grave profundo (50 Hz) trae cada canción frente a la biblioteca (de su huella del EQ);
- cuánto grave (158 Hz) trae frente a la biblioteca;
- su rango de sonoridad (LRA).

La recta usa mínimos cuadrados con un poco de freno. Cómo se aplica:

- **Cuándo se usa:** solo si la recta explica algo (R² ≥ 0,3).
- **A qué canciones:** a las que aún no tienen nada aprendido, con la mitad de lo que predice y como mucho ±1 dB.
- **Al sonar entera:** la canción aprende lo suyo, partiendo de lo que llevaba.
- **Cuándo se rehace:** al tener 5 aprendidas más que la última vez, y como mucho cada 10 minutos.
- **En racha:** con otra de su disco, las pistas sin aprender cuentan con lo estimado.

### 2. Tocar las 8 bandas seguidas

En la hoja de afinar, el botón «Tocar las 8 bandas seguidas» toca de 63 Hz a 8 kHz, 8 s cada una con 1,5 s de silencio entre ellas. La
banda que se oye aparece en grande («250 Hz   3 de 8»), descontando lo que guarda la salida, para anotar en orden sin ir y venir de la
rocola. Todas salen a −20 dB por canal: medido, entre −20,4 y −19,6.

### Pruebas de la cuarta tanda

`pruebas/cadenabillar.cs` suma:

- la recta, que encuentra lo que manda y explica casi todo;
- en la rocola, una canción sin aprender recibe la mitad de lo predicho, la aprendida usa lo suyo, y con menos de 30 aprendidas no hay recta;
- las bandas seguidas: su duración, la banda que se oye y el mismo nivel en todas.

Suite completa: los únicos fallos son los de antes. `bombosinhundir` falló una vez en su comprobación de tiempo mientras corría otra medición; sola pasó dos de dos.

## Quinta tanda: bitácora, agudos ásperos, limitador que respira y comparar a ciegas

### 1. Bitácora del billar

Hasta ahora todo se midió con canciones de prueba. Con el modo billar, la rocola apunta una línea por cada canción que sonó al menos 30 s en
`rocola_billar_bitacora.txt`, junto a los demás datos. Va separada por tabuladores, así se abre en una hoja de cálculo. Lleva:

- la hora y el archivo;
- si sonó entera, y el volumen parejo que llevaba y el que aprendió;
- sus picos frente al techo;
- qué parte del tiempo el limitador bajó más de 1 y más de 3 dB;
- lo que decidió el modo: presencia, claridad, partes suaves, cuerpo, eses y agudos ásperos;
- si iba afinada, el factor del bajo, el corte del subgrave y el EQ de la canción.

Se escribe en segundo plano, no cambia el sonido, y al pasar de 4 MB la anterior se guarda como `.1`. **Si me pasas ese archivo, ajusto la
cadena con datos del A4.**

### 2. Agudos ásperos dinámicos

Las cornetas «gritan» entre 2 y 4 kHz cuando entran de golpe platillos, trompetas o una guitarra saturada. La etapa nueva
(`AsperezaDinamica.cs`) va entre el ataque y las eses y funciona como el retumbe dinámico, pero en los agudos:

- **Qué compara:** la energía en 3,2 kHz frente a la de 1 kHz (el cuerpo de la voz), contra su propia media de la canción.
- **Cuándo corta:** si pasa 4 dB de esa media, corta la mitad de lo que pasa, como mucho 3 dB, con una campana en 3,2 kHz. Ataque de
  8 ms y suelta de 150 ms.
- **La media:** baja en unos 6 s, pero sube cuatro veces más despacio. Así unos platillos que gritan cada pocos segundos no se vuelven
  «lo normal»: con la media pareja, a la tercera ráfaga ya casi no cortaba.

Medido con una «voz» continua y ráfagas de ruido en 3,2 kHz cada 4 s: con ráfagas de unos 6 dB de más corta 1,2 dB en cada una, y con
ráfagas más fuertes, 2,8 a 3 dB. Fuera de las ráfagas no toca ni una muestra.

Tiene su interruptor, «Agudos ásperos dinámicos» (`aspereza_dinamica=`), apagado de fábrica, en la hoja Voz, golpe y retumbe. Con el
modo billar va siempre. «Escuchar sin mejoras» la quita.

### 3. Limitador que respira menos

En el código constaba que el 28-09 se probó una suelta «según la música» y no ganaba en todo. Esta vez se midió el movimiento de la
ganancia del limitador cada 10 ms con la cadena del billar entera.

En el billar, el limitador lleva la cuenta de cuánto rato está bajando (más o menos la fracción del último segundo) y su suelta se alarga
con eso: de 100 ms con golpes sueltos (como siempre) hasta 400 ms cuando trabaja seguido (`RespiraSueltaLentaS`).

| Canción | Movimiento antes | Movimiento ahora | Rato limitando (>1 dB) | Lo que baja de media |
|---|---|---|---|---|
| Pop | 0,051 | 0,041 | 0,8 → 0,8 % | −0,14 → −0,17 dB |
| Balada | 0,092 | 0,071 | 3,1 → 3,7 % | −0,10 → −0,12 dB |
| Cumbia vieja | 0,099 | 0,070 | 8,6 → 11,3 % | −0,27 → −0,36 dB |

El movimiento (dB por cada 10 ms) baja entre 10 y 30 % en las cinco canciones: se oye menos el bombeo. El precio: limita un poco más de
rato y baja como 0,1 dB más de media. Sin el billar, el limitador sale bit a bit como siempre.

### 4. Comparar a ciegas

En «Lo que suena ahora», el botón «Comparar a ciegas» funciona así:

- Durante 2 minutos, la canción que suena cambia cada 15 s entre el modo billar y la cadena de fábrica, sin decir cuál es. Son 4 tramos
  de cada uno, al azar.
- Tocas «Me gusta esta» en los tramos que te suenan mejor (un voto por tramo), y al final te dice cuál elegiste más veces.
- Cada modo suena a su volumen de verdad, y lo que se fija al armar la canción (el corte de 40 Hz, la compensación de la presencia) sigue
  como estaba en esa canción.
- **No se guarda nada:** la configuración sigue con el modo de antes. Vuelve como estaba al acabar, al cambiar de canción, al tocar un
  ajuste o al cerrar la ventana. Solo funciona con el modo billar encendido.

### Pruebas de la quinta tanda

`pruebas/cadenabillar.cs` suma:

- los agudos ásperos: apagados salen bit a bit, y encendidos cortan en la ráfaga y no tocan la voz sola;
- el limitador: sin respirar sale bit a bit, y respirando se mueve menos con golpes seguidos;
- la línea de la bitácora, con tantas columnas como la cabecera;
- el orden y el resultado de la comparación a ciegas, y que no se guarda.

Con el modo apagado, `compara-cadena` no tiene distintas sin esperarlas. Al hacer las capturas apareció un fallo: la ventana de Sonido no
abría en el arnés de pruebas, porque la comparación usaba campos que solo se arman con el constructor. Se arregló antes de publicar.

## Sexta tanda: afinar con el celular, lo más simple, y el resumen de la bitácora

Primero el dueño eligió «Afinar con micrófono USB» y «Resumen de la bitácora». Después descartó el micrófono: «el cable es corto y
pues no es eficiente, lo del teléfono no era mala idea pero debes hacerlo lo más simple posible». El micrófono no se publicó.

### 1. Afinar con el celular, lo más simple

La hoja «Afinar el local con el celular» (en El local y sus bocinas) quedó así:

- **Un botón:** «Tocar las 8 bandas (de grave a agudo)». Suenan seguidas, 8 s cada una con 1,5 s de pausa. En la pantalla sale en grande
  cuál suena.
- **Una barra por banda** para poner el número que marcó la app. Hasta la izquierda es «sin anotar».
- **«Tu app dice: dBA o dBC»**, dos botones. La Z guardada de antes se sigue entendiendo; en la hoja cuenta como C.
- A la derecha, como antes: la corrección, «Usar la afinación en el modo billar» y cómo medir, en pocas líneas.

Se quitó:

- los puntos 2 y 3 y el promedio: un solo punto. `afinado_local2=` y `afinado_local3=` ya no se leen ni se guardan;
- los botones de cada banda: las bandas solo suenan seguidas;
- la Z como botón.

La corrección, sus topes y el bajo según lo medido no cambian.

### 2. El resumen de la bitácora

En la tarjeta «El billar», con el modo encendido, el texto se acortó a dos líneas. Debajo van otras dos:

- cuántas canciones lleva la bitácora y cuántas en las últimas 12 h (una noche de bar);
- qué parte del tiempo el limitador bajó más de 3 dB, y la canción que más apretó.

Se arma en memoria: al arrancar se lee `rocola_billar_bitacora.txt` en segundo plano, y cada línea nueva se suma al apuntarla. Solo se
muestra.

### Pruebas de la sexta tanda

`pruebas/cadenabillar.cs` (sección 9) comprueba:

- que la hoja tiene un botón para las 8 bandas, una barra por banda y dBA/dBC, sin puntos ni botones por banda;
- que la Z de antes se sigue entendiendo;
- el resumen de la bitácora;
- que ya no se leen los puntos 2 y 3.

Con el modo apagado, `compara-cadena` da 0 distintas inesperadas.

## Séptima tanda: revisión de fallas y «Con o sin afinar» a ciegas

El dueño eligió «Revisar fallas» y «Afinación a ciegas» (28 de octubre de 2026).

### 1. Lo que se encontró y se arregló

- **La comparación a ciegas podía armar la siguiente canción sin el billar.**
  - Qué pasaba: si la canción se acababa en un tramo «de fábrica», la siguiente se preparaba en ese momento, con el modo apagado.
  - Consecuencia: toda la canción siguiente sonaba sin el corte de 40 Hz, sin la compensación de la presencia y sin su volumen parejo,
    aunque al terminar la comparación el modo volvía.
  - Arreglo: la comparación se termina antes de armar cualquier otra canción (`ArmaCadena`).
- **Se aprendía el volumen parejo de una canción comparada.** En esa vuelta sonaron dos cadenas distintas, así que lo medido no sirve.
  Ahora esa vuelta no se aprende, como con «Escuchar sin mejoras».
- **Desnormales en el limitador que respira.** Tras un minuto y medio sin limitar, su actividad bajaba a números diminutos que en
  procesadores como el A4 hacen lentísimo el hilo del sonido. Ahora se corta a cero. Sin el billar, el limitador sigue saliendo bit a bit.

Se revisó además y está bien:

- `eqCache` se lee solo en el hilo de la pantalla, donde se arma la cadena.
- La bitácora y su resumen: cerrojos y segundo plano.
- Los filtros de los agudos ásperos: sin desnormales.
- El aprendizaje y la recta del volumen parejo.

### 2. «Con o sin afinar», a ciegas

En «Lo que suena ahora» hay ahora tres botones: «Billar o fábrica», «Con o sin afinar» y «Me gusta esta».

- «Con o sin afinar» funciona como la comparación de antes: 2 minutos, 8 tramos de 15 s al azar, un voto por tramo.
- Lo que cambia es la afinación del celular. El modo billar sigue encendido todo el rato.
- Necesita las bandas de la voz anotadas.
- El EQ cambia al instante. El bajo según lo medido se fija al armar la canción, así que no cambia en la comparación.
- No se guarda: la configuración lleva la afinación de antes (`AfinadoUsarAGuardar`). Vuelve como estaba al acabar, al cambiar de
  canción, al tocar un ajuste (también en la hoja de afinar) o al cerrar la ventana.

### Pruebas de la séptima tanda

`pruebas/cadenabillar.cs` (sección 10) comprueba:

- que la actividad del limitador queda en cero exacto tras 100 s sin limitar;
- que la comparación no aprende y se acaba antes de armar la siguiente canción;
- el resultado «con o sin tu afinación», y que sin las bandas de la voz no se puede comparar;
- que durante la comparación se guarda la afinación de antes.

Con el modo apagado, `compara-cadena` da 0 distintas inesperadas.

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
- (Hecho en la segunda tanda: el subgrave a menos resolución.)
