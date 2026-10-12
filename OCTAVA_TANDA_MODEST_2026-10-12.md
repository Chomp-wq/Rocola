# Octava tanda: lo que elegiste de las propuestas — Modest, 12 de octubre de 2026

Petición del usuario: «Haz A1, A2, A4, T1 y T2, y sigue buscando fallas».

| Propuesta | Qué pasó |
| --- | --- |
| A1. Saltar sin chasquido | **No se hizo.** Choca con una regla que ya tenía el programa (abajo). |
| A2. Graves y agudos con volumen bajo | **Ya existía:** la propuse por error. No se cambió nada. |
| A4. Remuestreo de calidad | **Hecho.** En Ctrl+F3, apagado de fábrica. |
| T1. Letra más nítida en la tarjeta | **Hecho.** Solo se puede juzgar en Windows. |
| T2. Números de la barra de letras más grandes | **Hecho.** Solo con tarjeta gráfica. |

## A1. Saltar sin chasquido: no se hizo

La idea era bajar el volumen de la salida en 60 ms antes de cortar. Lo programé, pero la batería `salida` ya exigía
que **ningún sitio del programa toque el volumen de la salida**: en Windows XP ese volumen es la barra de «Onda» del
control de volumen de Windows. Si la rocola se cerrara a media bajada, esa barra se quedaría abajo para todo.

La otra forma, bajar el volumen dentro de la propia música, no sirve para saltar: la salida tiene un segundo de
música ya entregado a Windows (el colchón contra atorones), así que el fundido se oiría un segundo tarde. SALTAR
tardaría un segundo en responder.

Quité el cambio y todo quedó como estaba. Si el chasquido molesta de verdad en el bar, habría que hablarlo:
- o se acepta ese segundo de espera al saltar,
- o se reduce el colchón, con más riesgo de cortes en el equipo de dos núcleos.

## A2. Graves y agudos con volumen bajo: ya existía

Al revisarlo encontré que el **modo noche ya lo hace** (`ModoNoche.cs`): cuando baja el volumen de madrugada,
devuelve graves y agudos según la norma ISO 226, que es lo que yo proponía. Fue un error mío proponerlo como
nuevo. No se cambió nada.

## A4. La tarjeta de sonido a una frecuencia fija

**El problema:** en Windows XP, si la tarjeta trabaja a 48 kHz y la canción viene a 44,1 kHz, la conversión la hace el
mezclador de Windows, que es de calidad baja y ensucia un poco los agudos.

**Ahora:** en **Ctrl+F3 → Sonido → El local y sus bocinas**, dentro de la tarjeta «Prueba de bocinas», hay un
renglón nuevo: **«Tarjeta a:»**, con tres botones.

- **Como la canción** (de fábrica). No se convierte nada; todo queda como siempre.
- **44,1 kHz** o **48 kHz.** Cada canción que no venga a esa frecuencia se convierte dentro de la rocola. Se usa el
  mismo convertidor de calidad del relevo sin pausa, con su limitador de picos. A Windows ya le llega la frecuencia
  de la tarjeta y su mezclador no toca nada.

Detalles:

- **Se guarda** en la configuración (`frecuencia_salida=`), y «Dejar como de fábrica» lo devuelve a «Como la canción».
- **Al cambiarlo** se aplica desde la siguiente canción. La que suena sigue igual: no se corta.
- **La tabla del convertidor se prepara aparte**, al arrancar y al elegir. Si aún no está lista, esa canción sale
  tal cual y las siguientes ya convertidas.
- **El reloj de la canción** (la barra de progreso) descuenta bien el retraso del convertidor.
- Si el equipo no da (el mismo presupuesto del relevo) o la canción tiene más de dos canales sin disposición
  conocida, sale tal cual.

**Qué frecuencia elegir:** la que tenga la tarjeta del bar en Windows: Panel de control → Sonido → la tarjeta →
Opciones avanzadas. En XP suele ser 48 kHz con las tarjetas integradas (AC'97). Si es 44,1 kHz, la mayoría de las
canciones ya vienen a esa frecuencia y no hace falta.

**Lo que cuesta:** aquí (Xeon con Mono) 48,8 ms de procesador por cada segundo de música, un 5 % de un núcleo. En el
equipo de dos núcleos puede ser algo más; por eso va apagado de fábrica. **No está medido en el bar** ni oído en
sus bocinas.

## T1. Letra ajustada a los píxeles en la tarjeta gráfica

El texto que la tarjeta pinta como imagen se suavizaba en gris sin ajustarse a la rejilla de píxeles. Ahora se ajusta
(«GridFit»), que en Windows da letras chicas más nítidas. Está en:

- la imagen de la interfaz;
- la franja de lo que suena;
- las piezas sueltas;
- las cintas de texto que se desplazan;
- lo que suena en vivo, la pantalla viva y el disco vivo.

Solo con tarjeta gráfica; el dibujo por CPU no cambia.

**No comprobado:** aquí Linux ignora el ajuste y se ve igual. Hay que mirarlo en el bar. Si en algún sitio se viera
peor (letra más flaca o apretada), dímelo y lo devuelvo en ese sitio.

## T2. Los números de la barra de letras, más grandes

Debajo de cada letra va cuántas canciones hay. Antes la letra se medía para que cupiera «8888» y salía diminuta. Ahora,
con tarjeta gráfica, los miles se abrevian («147», «2.3k», «10k»; hasta «999k») y se mide para «8.8k»:

- **a 1024×768, de 6,5 a 7,8 puntos.**

Sin tarjeta gráfica, como antes.

**Arreglado de paso:** si la tarjeta se apagaba o se encendía con la barra ya pintada, los números se quedaban como
estaban (la tira de letras se guarda hecha) hasta que algo la volviera a pintar. Ahora se rehace al momento.

## Seguir buscando fallas

En esta tanda la búsqueda se centró en lo nuevo:

- **El reloj de la canción** con la frecuencia fija: descuenta el retraso en cuadros de la canción, no de la tarjeta.
- **El relevo sin pausa:** si se cambia la frecuencia con una canción sonando, la siguiente abre la salida de nuevo a
  la elegida, en vez de seguir encadenada a la anterior.
- **La regla del volumen de Windows** (la de A1).
- **La barra de letras** al encenderse o apagarse la tarjeta (arreglado, arriba en T2).
- **La rocola entera con la frecuencia fija** (48 kHz), con 120 canciones de 32, 44,1 y 48 kHz, archivos rotos,
  discos que se borran y 1.500 teclas al azar:
  - 0 excepciones;
  - 80 veces se abrió la salida a 48 kHz;
  - la primera canción de 32 kHz salió tal cual (su tabla aún se preparaba), como está previsto;
  - las de 8 kHz salen tal cual: el convertidor no da saltos de más de 4 veces;
  - quieta 15 s, la cola siguió encadenando canciones sin abrir la salida de nuevo.
- **La configuración con valores raros** (las 121 claves, incluida la nueva, con NaN, vacíos, enormes…): carga
  entera, 0 problemas.

## Cómo se comprobó

- **Prueba automática nueva `octavatanda`:** 22 comprobaciones con tarjeta, 17 sin ella.
  - A4:
    - solo 44.100 o 48.000;
    - de fábrica no toca nada;
    - convierte a 48 kHz con su limitador;
    - el reloj descuenta lo justo;
    - suena sin pasarse;
    - una canción que ya va a 48 kHz no se toca;
    - seis canales, tal cual;
    - el relevo abre de nuevo.
  - T1: el ajuste a píxeles en las texturas de la tarjeta.
  - T2: las abreviaturas, que la letra crece y que los números cambian al momento. Esto último falla contra el
    ejecutable anterior.
- **Puesta al día:** `filabaja`, por la regla de las instrucciones de la izquierda que cambió en la séptima tanda.
- **Siguen pasando:**
  - `salida`, la regla del volumen de Windows;
  - el audio: `sinpausa`, `suenavivo`, `videoaudio`, `transiciones`, `gravemono`, `noche`, `silencios`, `todojunto`,
    `audioestereo`, `picoreal`, `subgrave` y `salalocal`;
  - Ctrl+F3: `ventanaf3` (3.117 comprobaciones, sin solapes con el renglón nuevo) y `ajustes`;
  - `textosgpu`;
  - las tandas segunda a séptima.
- **Tanda completa (`pruebas/todo.sh`):** 26 problemas, los mismos 26 de siempre (baterías que ya fallaban en este
  Linux); ninguno nuevo. `octavatanda`, `filabaja` y `septimatanda` pasan en los dos perfiles.
  Se corrió antes del arreglo de la barra de letras; después se repitieron `octavatanda` y `filabaja`, y pasan.
- **Dibujo por CPU:** los 14 métodos congelados, idénticos.
- **No comprobado:**
  - oírlo en el bar;
  - medir A4 en el equipo de dos núcleos;
  - ver T1 con la letra de Windows.
