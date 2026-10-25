# T1, T3, T5, T6, T9 y el ruido del amplificador — Modest, 25 de octubre de 2026

Decisión del dueño sobre [AUDIO_PROPUESTAS_MODEST_2026-10-25.md](AUDIO_PROPUESTAS_MODEST_2026-10-25.md): «T1, T3, T5, T6, T9 y revisa
si hay forma de que se escuche menos el ruido eléctrico del amplificador por medio de software, o si es algo solamente de hardware».

El plan de traspaso, con el estado de cada tarea, está en [PLAN_AUDIO_T1_T3_T5_T6_T9.md](PLAN_AUDIO_T1_T3_T5_T6_T9.md).

**Todo está probado en Linux/Mono con señales sintéticas y con un Windows imitado. Nada se oyó en el billar ni se probó en el A4 con XP.**

| | Qué | Interruptor | Commit |
|---|---|---|---|
| **T1** | El nivelado deja el margen que pide el tono del local | ninguno nuevo: va con los mandos de graves y agudos | fcc1304 |
| **T3** | Dónde empieza y acaba de verdad cada canción (solo se mide) | ninguno: no cambia el sonido | e270a27 |
| **T6** | Quitar el zumbido de la corriente | «Quitar el zumbido de la corriente», encendido de fábrica | e270a27 |
| **T9** | Suavizar las «eses» | «Suavizar las eses», apagado de fábrica | dbccdf9 |
| **T5** | La salida de sonido con su propio hilo | ninguno: no cambia el sonido | c1cf6f7 |
| Ruido | Lo que el software puede y no puede hacer; aviso de las entradas abiertas del mezclador | ninguno: solo mira y apunta | este |

Con todo de fábrica, la cadena sale **bit a bit** como antes: `compara-cadena` da 16 de 16 iguales contra el ejecutable anterior.

## T1. El nivelado y el tono del local

**El problema.** El nivelado al límite sube cada canción hasta que sus picos quedan a 0,5 dB del techo del limitador. Ese margen se midió con
el tono del local en 0. Al subir «Graves del local» o «Agudos del local» suben los picos, y el limitador se los comía:

- con graves +3, en una canción masterizada quitaba más de 1 dB el 96 % del tiempo;
- con graves +6, quitaba más de 3 dB el 84 % del tiempo.

**Ahora** el nivelado baja lo que pide el tono (`SalaLocal.MargenTonoDb`, aplicado en `GananciaDe`):

- 0,75 dB por cada dB de graves y 0,25 dB por cada dB de agudos;
- con los dos a la vez no se suman, porque sus picos no caen juntos: se toma la raíz de la suma de los cuadrados, que es lo que salió al
  medir (+6 y +6 piden 4,75 dB; la regla da 4,74);
- lo negativo no da nada.

Usa el tono que de verdad suena (el modo automático del local incluido), y con «Escuchar sin mejoras» no baja nada.

Medido en la cadena de verdad con seis canciones de prueba (`allimite`, sección 9):

| Tono | Baja el nivelado | Lo peor de las seis: limitador > 1 dB | > 3 dB |
|---|---|---|---|
| graves +3 | 2,25 dB | 4,8 % | 0,1 % |
| graves +6 | 4,5 dB | 3,6 % | 0 % |
| agudos +3 | 0,75 dB | 5,1 % | 0,1 % |
| agudos +6 | 1,5 dB | 7,0 % | 0,2 % |
| graves +6 y agudos +6 | 4,74 dB | 0,2 % | 0 % |
| graves +3 y agudos +6 | 2,70 dB | dentro del criterio | |
| graves +1 y agudos +1 | 0,79 dB | dentro del criterio | |

El criterio es el del nivelado al límite: más de 1 dB como mucho el 10 % del tiempo, y más de 3 dB como mucho el 1 %.

**Se oiría:** con los graves subidos, la música queda unos dB más baja en conjunto, pero con más grave de verdad y sin el aplastado. Para más
volumen, el amplificador. Con el tono en 0, o en negativo, nada cambia.

## T3. Dónde empieza y acaba de verdad

El medidor (`MedidorBs1770.Utiles`, `Sonoridad.cs`) guarda dos tiempos, con los bloques de 400 ms que ya usaba para la sonoridad:

- **inicio útil:** el principio del primer bloque a menos de 20 LU de la sonoridad de la canción;
- **fin útil:** el final del último.

Medido (`zumbido`, sección 2):

- un principio y un final secos quedan a menos de 0,4 s de donde está la música;
- 15 s de aplausos 35 dB por debajo no cuentan como canción;
- un desvanecido acaba donde lo que suena está a unos 20 LU de la canción.

Al principio se pensó usar las ventanas de 3 s del LRA, pero con ellas un final seco salía casi 3 s tarde.

**Solo se mide**, para las propuestas T7 (acortar los finales larguísimos) y T8 (la mezcla entre discos), que no se han pedido todavía. La
medida va en la vuelta que el repaso ya está dando a toda la biblioteca.

**El formato** (`rocola_sonoridad.txt`). El algoritmo 6 es el 5 más cuatro campos antes de la ruta:

`inicio útil | fin útil | zumbido (50, 60 o «-») | cuánto sobresale (dB o «-»)`

- Las medidas de 1 a 5 se siguen leyendo y nivelan igual.
- La tanda larga pide de nuevo todo lo que no es del 6.
- **Un ejecutable anterior (algoritmo 5) lee el archivo nuevo sin romperse** y guarda las del 6 intactas: probado con `ROCOLA_EXE_ANTERIOR`
  en `lra`.

## T6. El zumbido de la corriente

**La medida** (`MedidorZumbido`, `Zumbido.cs`) va dentro del medidor de sonoridad, así que la hacen el repaso y la canción que suena entera.

- Baja la suma de los canales a unos 1400 Hz con tres medias móviles seguidas (un CIC de orden 3).
- En ventanas de 2 s con ventana de Hann (medio hercio de paso), mide con Goertzel la línea exacta de 50 y 60 Hz y de sus armónicos 2.º y 3.º,
  y la compara con lo que hay a 4 Hz de cada lado.
- Solo cuenta el 20 % más callado de cada canción: ahí el zumbido se oye y la música no lo tapa.
- Hay zumbido si la fundamental o el 2.º armónico sobresalen de media 10 dB o más, en al menos el 70 % de esas ventanas, y por encima de
  −80 dBFS.

Medido (`zumbido`, sección 3):

| Caso | Resultado |
|---|---|
| 60 Hz a −50 dBFS, con armónicos | se reconoce (sobresale 40,8 dB) |
| lo mismo a 50 Hz | se reconoce |
| solo el doble (120 Hz, el de un rectificador) | se reconoce como el de 60 |
| música sin zumbido | nada |
| una nota de bajo larga en Si (61,7 Hz) toda la canción | nada |
| un zumbido a −88 dBFS | nada |

**La etapa** (`QuitaZumbido`): cuatro muescas de 2 Hz de ancho, en la fundamental y sus tres armónicos siguientes.

- Quita la línea 64 dB en la etapa sola y 61 dB en la cadena entera.
- A 220 Hz no cambia nada (−0,014 dB). A 61,7 Hz (la nota Si más cercana), −1,3 dB.
- Va después de los arreglos S1 y S2, antes de todo lo demás, y **solo en las canciones con zumbido medido**. Las demás no la llevan: bit a
  bit igual.
- Se enciende y apaga en vivo con una mezcla de 25 ms, sin salto.
- Pasa tal cual con «sin mejoras» (S4), y S5 dice «Zumbido: 60 Hz quitado».
- En disco continuo sigue con los filtros de la pista anterior.

**El interruptor:** Ctrl+F3 → Sonido → Lo que suena ahora → «Quitar el zumbido de la corriente» (`quita_zumbido=`), **encendido de
fábrica**. Es como S1 y S2: solo actúa en las canciones con el defecto. La nota general de esa tarjeta se repartió entre las notas de cada
interruptor, para que cupiera.

**Lo que cuesta:** la medida de cada canción cuesta 9 ns más por muestra en Mono, un 54 % más que la medida sola. Frente a descomprimir
el MP3 es poco, pero en el A4 no se ha medido.

**Lo que no hace:** solo quita el zumbido que viene **grabado en la canción**. El que mete el amplificador o la instalación está fuera de la
rocola (ver abajo).

## T9. Suavizar las «eses»

`SuavizaEses` (`Eses.cs`) va detrás del local y de la presencia (que suben los agudos), y delante del bajo psicoacústico y del limitador.

- Un paso-banda de 6,5 kHz da la banda de las eses (de 4,4 a 9,6 kHz a −3 dB).
- Dos envolventes de 5 ms comparan esa banda con todo lo que suena.
- Si la banda pasa del 45 % de la energía (y de −40 dBFS), baja (0,45 / fracción)² de golpe (1 ms), como mucho 6 dB, y suelta en 60 ms.
- Sale `x − (1 − g)·banda`: mientras no baja nada, **bit a bit lo que entra**.

Medido (`eses`):

- en una voz con eses, la banda de 5 a 9 kHz baja de 3,5 a 3,8 dB en cada «s»;
- entre una «s» y la siguiente, la voz sale bit a bit;
- ruido rosa de banda ancha y acordes: 0 dB, sin tocar ni una muestra;
- encendida cuesta 33 ns por cuadro estéreo en Mono; apagada no se calcula.

**El interruptor:** Ctrl+F3 → Sonido → El local y sus bocinas → Tono y presencia del local → «Suavizar las eses» (`suaviza_eses=`),
**apagado de fábrica**, se oye al instante. Está en el sitio de la nota que decía que graves y agudos en 0 no tocan nada.

Va con «sin mejoras», con S5 («Eses: −3,6 dB») y con el disco continuo.

**Lo que no se sabe:** con platillos muy fuertes también puede bajar un poco; las señales de prueba no los traían. Hay que oírlo en el bar.

## T5. La salida de sonido con su propio hilo

**El problema.** Cada bloque que la tarjeta acababa de sonar lo volvía a llenar **la pantalla**, en su bucle de mensajes. Si la pantalla se
paraba más que el colchón de la salida (750 a 1000 ms en XP), la música se cortaba. En el A4 se paró 2015 ms (23-10, en el Present de la
tarjeta) y 3172 ms (24-10, al arrancar).

La salida con hilo de NAudio (`WaveOutEvent`) no servía: usa un método que el .NET del A4 no tiene.

**Ahora.** `WaveOutPropio` (`SalidaPropia.cs`) habla directamente con winmm (`waveOutOpen` con aviso por evento) y tiene un hilo solo
para eso, en prioridad alta.

- Espera el aviso con `WaitOne(ms, false)`, que sí existe en el A4, y rellena en orden los bloques que ya sonaron.
- Mismos bloques de 50 ms, mismo formato, mismo reloj de la canción (`waveOutGetPosition`) y la parada avisada en el hilo de la pantalla.
- **No cambia nada de lo que suena:** va sin interruptor.

**El respaldo:**

- si no abre por algo que no es el aparato ni el formato, esa sesión sigue con el `WaveOut` de siempre;
- si falla por sí misma a media canción, pasa lo mismo y la canción se retoma desde donde iba, sin devolver el crédito;
- un fallo del aparato, o de la canción, se trata como siempre.

**Las pruebas** (`salidapropia`) usan un Windows imitado que «suena» cada bloque con un reloj de verdad:

- suena todo, en orden y entero, y el reloj da lo que sonó;
- **con el hilo que la creó parado 3 s, no se queda ni una vez sin música**;
- pausa, sigue y parada;
- el error del aparato es el de winmm;
- un fallo de Windows cuenta como de la salida, y uno de la música no;
- la parada llega por el hilo de la pantalla;
- en todo el ejecutable no hay ni un método de .NET 2.0 SP2. Como control, el lector sí encuentra el que tiene `WaveOutEvent` en NAudio.

**Hay que probarla en el A4.** Es la parte que habla con el controlador de sonido de XP: las firmas de winmm están revisadas contra la
documentación, pero solo se ve de verdad allí. La latencia se dejó como estaba (750 y 1000 ms). Bajarla, que permitiría «saltar sin
chasquido» (T10), es otro paso, después de probarla en el bar.

## El ruido eléctrico del amplificador: ¿software o hardware?

**Casi todo es hardware.** El soplido o el zumbido que se oye con la música parada nace **después** de la tarjeta de sonido: en el
amplificador, en el cable, o en la tierra entre la PC y el amplificador. El software no puede quitar lo que entra después de la tarjeta:
un filtro en la rocola solo tocaría la música, no ese ruido.

### Cómo saber de dónde viene (sin herramientas)

1. **Con la rocola callada, desenchufa el cable de audio de la entrada del amplificador.**
   - Si el ruido sigue, es del propio amplificador (su soplido). La solución es bajarle la ganancia (punto 4).
   - Si se va, viene de la PC o del cable: sigue con el paso 2.
2. **Vuelve a enchufarlo y escucha qué tipo de ruido es:**
   - un «mmm» grave y constante (60 Hz) es un **lazo de tierra** entre la PC y el amplificador;
   - un zumbido que cambia al mover el ratón, al leer el disco o con la pantalla es **ruido de la propia PC** que se cuela por la salida de
     la placa;
   - un soplido que sube y baja con el volumen de Windows viene de una **entrada abierta** del mezclador (punto 3).

### Lo que sí hace el software

1. **Dar la señal fuerte, para que el amplificador vaya más bajo.** El ruido del amplificador queda igual, y la música queda más arriba.
   Ya se hace:
   - la rocola pone el volumen de Windows al máximo al arrancar;
   - el nivelado sube cada canción hasta su límite;
   - T1 evita que eso aplaste cuando se suben los graves.
2. **Mirar las entradas del mezclador de XP que se oyen por los altavoces** (nuevo, `VolumenWindows.cs`). En los Realtek de XP, el
   micrófono, la línea de entrada, el CD y el auxiliar suelen venir abiertos en la reproducción. Su ruido, sobre todo el del micrófono con su
   refuerzo, sale por el amplificador aunque no haya nada enchufado.
   - Al arrancar, la rocola mira cuáles están abiertas (sin silencio y con volumen).
   - Si hay alguna, apunta en `rocola_error.txt` una línea «[Ruido en la salida] …» con cuáles son y cómo silenciarlas.
   - **No las toca**: AGENTS.md solo permite subir el volumen general. Si quieres que la rocola las silencie sola al arrancar, dímelo y lo
     hago.
3. **El zumbido grabado en las canciones** lo quita T6, pero solo ese.

### Lo que se arregla con hardware (de lo más barato a lo más caro)

1. **El volumen bien repartido.** Windows al máximo (ya lo hace la rocola) y la perilla del amplificador lo más baja que dé el volumen que
   quieres. Nunca al revés, Windows bajo y el amplificador alto: así se oye más el ruido.
2. **Silenciar a mano las entradas abiertas.** Control de volumen de Windows (doble clic en el altavoz de la barra de tareas): marcar
   «Silencio» en Micrófono, Entrada de línea, CD y Auxiliar. En Opciones → Controles avanzados del micrófono, quitar el «refuerzo» (Mic
   Boost).
3. **Usar la salida de atrás** (la verde de la placa), no la del frente de la caja. La del frente pasa por cables internos sin blindaje y
   recoge más ruido.
4. **Bajar la ganancia de entrada del amplificador**, si tiene, y subir su volumen general.
5. **Lazo de tierra («mmm» de 60 Hz):**
   - la PC y el amplificador enchufados en la **misma regleta**;
   - si sigue, un **aislador de tierra para audio** (un transformador pequeño con conectores RCA) en el cable entre la PC y el amplificador.
     Es barato y suele quitarlo del todo. **Nunca** quitar la tierra del enchufe.
6. **Cable corto y blindado**, lejos de los cables de corriente.
7. **Si el ruido es de la propia PC** (cambia con el ratón, el disco o la pantalla): una **tarjeta de sonido USB externa** o la salida
   digital (óptica) hacia el amplificador. La rocola ya funciona con cualquier tarjeta: se elige como predeterminada en Windows y listo.

## Pruebas

- Nuevas: `zumbido`, `eses` y `salidapropia`.
- Ampliadas: `allimite` (sección 9, T1), `lra` (algoritmo 6 y el ejecutable anterior), `volumenwindows` (1b, las entradas abiertas).
- En verde, entre otras: `sonoridad`, `compatvieja`, `diario`, `nivelpicos`, `localauto`, `modosauto`, `discoparejo`, `otroarchivo`,
  `flotantexp`, `cadenaentera`, `arreglos`, `ajustes`, `ajustesvivos`, `ventanaf3`, `calidad`, `todojunto`, `salalocal`, `presenciasala`,
  `sinpausa`, `salida`, `latenciaadaptable`, `arranquevivo`, `videoaudio`, `bomba`, `ventanareal`, `diagnostico`, `aperturaaudio`,
  `transiciones` y `audiosuave`.
- **La batería completa** (sobre 584175a):
  - fallan las tres de siempre (`sala`, `discoacotado` y `caratulasadaptativas`, por cosas de Linux y de la tarjeta imitada);
  - dos que esperaban la cadena y el algoritmo de antes (`gravemono`: entre la presencia y el bajo va ahora T9; `silencios`: el algoritmo
    es el 6). Se actualizaron y pasan;
  - tres de tiempos (`realce`, `subgrave` y `flotantexp`) fallaron por la carga de la máquina. Corridas solas pasan, y en ese momento
    `realce` fallaba incluso con el ejecutable anterior.
- `compara-cadena` contra el ejecutable anterior: 16 de 16 combinaciones iguales bit a bit.
- Las dos hojas de Ctrl+F3 que cambiaron se miraron en pantalla (Linux): todo cabe, nada se pisa.
- `datosdeltas` (no es de la batería) falla igual con el ejecutable anterior: no es de esto.

## Qué hacer en el bar

1. **Lo primero, T5.** Que la música suene normal un buen rato. Abre y cierra Ctrl+F3 y el buscador, y entra y sale de discos: no tiene que
   cortarse.
   - Si `rocola_error.txt` dice «La salida de sonido con hilo propio no abrio» o «fallo a media cancion», mándamelo: la rocola ya habrá
     vuelto sola a la salida de siempre.
2. **T1.** Sube «Graves del local» a +3 con una canción moderna y fuerte. Tiene que sonar con más grave y sin «bombear», algo más bajo en
   conjunto.
3. **T9.** Enciende «Suavizar las eses» con una voz con eses fuertes. Las «s» tienen que molestar menos, sin que la voz pierda brillo.
4. **T6.** Cuando el repaso vaya midiendo, S5 («Lo que suena ahora») dirá «Zumbido: 60 Hz quitado» en las canciones que lo traigan.
5. **El ruido:** mira si `rocola_error.txt` trae «[Ruido en la salida]», y haz la prueba de desenchufar el cable.
6. Repite el **Diagnóstico completo** de Ctrl+F3 y mándame `rocola_error.txt`.
