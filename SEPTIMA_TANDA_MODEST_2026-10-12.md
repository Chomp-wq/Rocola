# Séptima tanda: lo que pediste con fotos — Modest, 12 de octubre de 2026

Peticiones del usuario de esta tanda, en orden:

1. «Optimiza los espacios en resoluciones bajas para que también se vea el texto de la casilla de ayuda».
2. «En la interfaz del asistente pon los controles en su pantalla, gráficos… en cada casilla que el cliente se mueva
   los controles se moverán con cada selección».
3. «El asistente debe tener animaciones más caricaturescas y que se sientan más vivas».
4. «Al estar dentro de un disco aparece el brillo del icono del buscador y el signo de interrogación del asistente».
5. «La barra de progreso con el neón activado se ve rara».
6. «Pon un tope a la combinación de rojo arriba o abajo».
7. «Cambia los textos del modo atracción para que se entiendan mejor y que al cliente de verdad le interese».
8. Revisar mejoras de audio, si los textos pueden verse mejor en resoluciones bajas, y seguir buscando fallas.

Todo lo visual va solo con tarjeta gráfica. El dibujo por CPU no cambia: sus 14 métodos congelados son idénticos.

## 1. La palabra AYUDA en 1024×768

En tu foto de 1024×768 la casilla de AYUDA salía compacta: solo «?», el rojo y la flecha, sin la palabra. Ahora, antes
de quitársela:

- **Las instrucciones de la izquierda** («Mover», «Entrar / Poner», «Volver») van con algo menos de aire entre sí.
  Antes era solo por debajo de 1000 px de ancho; ahora hasta 1180, así que 1024 entra. Son unos 22 px.
- **El encabezado** («LO MÁS SONADO» o «LETRA A» con su página) cede hasta un 15 % de lo que pide. Su letra se achica
  sola, de medio en medio punto, hasta caber, y el número de página pasa a su forma corta («3/12»).

Si ni así cabe (pantallas de 800 o menos), la casilla va compacta como antes, sin aplastar el encabezado.

No comprobado con la letra de Windows: aquí, con otra letra, ya cabía. La prueba simula que faltan 20 y 35 px.

## 2. Nada flotando dentro de un disco

Dentro de un disco, la textura de la interfaz ya no pintaba las casillas de BUSCAR y AYUDA. Pero el latido azul de
BUSCAR y la interrogación dorada que bota (las pone la tarjeta aparte) solo miraban si la casilla estaba «visible»,
y lo sigue estando para cuando vuelves a la pared. Ahora miran también que **la pared esté a la vista**: dentro de
un disco, con el buscador abierto, en la atracción, con el video a pantalla completa o configurando botones, no
salen. Y tocar ese sitio de la pantalla dentro de un disco ya no abre la ayuda escondida.

## 3. Los controles dibujados en el menú del asistente

En la pregunta «¿Qué quieres hacer?»:

- **Dentro de la opción elegida**, a la derecha, van los controles tal cual son en la máquina:
  - las dos flechas rojas, de subir y de bajar. La que no lleva a ningún lado sale apagada: arriba en la primera
    opción, abajo en la última;
  - el botón **VERDE**.
- **Las otras opciones no llevan controles:** al moverte, se van con la selección.
- **El VERDE late,** y al cambiar de opción el latido **se desliza** de una a otra (unas dos décimas de segundo) en
  vez de saltar.
- **La leyenda de abajo** ya no es solo texto. Antes decía «FLECHAS: elegir · VERDE: aceptar · ROJO: salir»; ahora
  dibuja los botones con su palabra al lado:
  - las flechas, «elegir»;
  - el verde, «aceptar»;
  - el rojo, «salir».
- **La letra de las opciones** se ajusta para que el nombre más largo («Buscar una canción por su nombre») quepa junto
  a los controles, también a 640×480.

La guía de cada paso ya dibujaba los botones que tocan; eso no cambió.

## 4. La estrella, más caricaturesca

Sigue **sin cara**. Todo es cuerpo, brazos, manos y pies.

- **Estira y aplasta**, como una pelota de goma. Se estira hacia arriba (y adelgaza) o se aplasta contra el suelo
  (y se ensancha), con los pies como ancla. Brazos, piernas y cuerpo se deforman juntos.
- **Los saltos tienen anticipación:** se agacha y se aplasta, sube estirada con los brazos al aire, cae aplastándose
  y se asienta con un rebotito.
  - **Al meter una moneda:** dos saltos de alegría, el segundo más chico, con un vaivén.
  - **Al saludar, al confirmar y al despedirse:** un saltito.
- **Al cambiar de opción** se lanza hacia ella y rebota como un resorte hasta quedarse.
- **Al explicar** asiente con todo el cuerpo. **Al pensar** se encoge y, al ofrecer, se estira.
- **Mientras espera no se queda quieta:** respira estirándose un poco y, cada 7 segundos, hace una cosa distinta, por
  turnos:
  - un saltito;
  - golpecitos con el pie;
  - un vaivén con los brazos.

Todo son cuentas suaves del tiempo, sin reservar memoria al animar. La prueba `asistentevisual` lo mide a 240
cuadros por segundo: ningún valor da un salto entre cuadro y cuadro.

## 5. La barra de progreso neón, rediseñada

**Antes:** eran cuatro franjas planas apiladas. El «resplandor» era un rectángulo de bordes duros arriba y abajo, y la
punta, cuadrada.

**Ahora** es un tubo:

- **un resplandor** de su color que se desvanece hacia arriba y hacia abajo;
- **el tubo**, de su color en los bordes y más claro en medio;
- **un núcleo** fino casi blanco;
- **en la punta**, una chispa redonda que late: ahí es donde va la canción.

Sin «Neón intenso», lo mismo pero más tenue. Sin neón, la barra de siempre. Son cuadros de color y un halo, nada se
hornea por cuadro.

## 6. Tope a ROJO + ARRIBA y ROJO + ABAJO

**Cerrar el buscador o la ayuda se puede siempre.** **Abrirlos** tiene tope:

- nunca dos veces en menos de 0,7 segundos;
- no más de seis veces en 30 segundos.

Si alguien insiste, sale «Espera un momento y vuelve a intentarlo». El mismo tope vale para tocar la casilla de AYUDA
en una pantalla táctil. Aguanta la vuelta del reloj de Windows (a los 24,9 y 49,7 días).

## 7. Los mensajes del modo atracción, mejor dichos

| Antes | Ahora |
| --- | --- |
| Toca un botón y elige entre 12,345 canciones | ¡Elige tu canción! Hay 12,345 esperándote |
| Pon tu canción: una moneda = 4 canciones | Una moneda = 4 canciones. ¡Tú pones la música! |
| Lo más pedido esta noche: «…» | La favorita de esta noche: «…» |
| Busca cualquier canción con el botón rojo y la flecha | ¿Buscas una? ROJO + ARRIBA y escríbela |
| — | **¿Primera vez? ROJO + ABAJO y te guío** (nuevo: presenta la ayuda) |

- Son cortos y cada uno da una razón para acercarse:
  - que hay mucho para elegir;
  - cuánto rinde una moneda;
  - qué suena esta noche;
  - que se puede buscar por el nombre;
  - que hay quien enseña.
- **Caben en las nueve resoluciones** que mide `sextatanda`, de 640×480 a 1920×1080. Con una favorita de nombre muy
  largo, se acorta el título dentro de las comillas.

## Cómo se comprobó

- **Prueba automática nueva `septimatanda`:** 2.106 comprobaciones con tarjeta, 10 sin ella.
  - El tope: rachas, el reloj que da la vuelta, y que abrir pasa por él y cerrar no.
  - La palabra AYUDA con 0, 20 y 35 px de menos, y compacta si falta mucho.
  - Nada dentro de un disco, y que el toque no abre.
  - Los controles en la opción elegida: el verde, las flechas apagadas o encendidas, y nada en las otras.
  - La leyenda dibujada, en tres tamaños.
  - El latido que se desliza.
  - Los saltos (anticipación, aire, aplastarse), el resorte, la espera viva y el estirar con los pies quietos.
  - Que no se reserva memoria al dibujar.
  - El tubo neón.

  Contra el ejecutable anterior falla en la palabra AYUDA, la interrogación dentro del disco, el toque escondido, los
  controles, el estirar y el tope.
- **Puestas al día:**
  - `atraccion` y `sextatanda`, con los mensajes nuevos;
  - `entrar` sigue exigiendo que ROJO + ARRIBA abra el buscador justo ahí.
- **Siguen pasando:** `asistentevisual` (2.470.909 comprobaciones, a 240 Hz), `ayudaguia`, `guiarojo`,
  `asistentecombo`, `entrar`, `segundatanda` a `sextatanda`, `vigia`, `vigiapantalla`, `lineaestado`, `bloqueos` y
  `arranquevivo`.
- **Siguen igual:** `atraccion`, `estilo` y `pantallaviva` fallan en lo mismo que antes; son de las 26 que ya
  fallaban en este Linux.
- **Comprobación visual** (por software, no es la tarjeta):
  - el menú del asistente con cada selección, a 1024 y 640;
  - una tira de cuadros de la estrella (saltos de alegría y espera);
  - el tubo neón antes y después;
  - los mensajes.
- **No comprobado:** verlo en la tarjeta gráfica del bar, la letra de Windows en la fila de arriba y la estrella en
  movimiento real.

## Para que elijas: audio

Revisé lo que ya hace el sonido:

- nivelado por canción y por disco;
- ecualizador automático;
- grave en mono;
- límite de picos reales;
- saltar silencios;
- relevo sin pausa con fundido de salida de 0,5 s;
- modo noche;
- el local y sus bocinas.

Quedan estas ideas; ninguna está hecha:

- **A1. Saltar sin chasquido.** Al apretar SALTAR a media canción, la salida se corta en seco, a media onda, y eso
  suele oírse como un «clic» en las bocinas. Bajar el volumen en unos 60 ms antes de cortar lo quita. Casi no se
  nota la espera.
- **A2. Graves y agudos que no se pierden con el volumen bajo.** Con poco volumen (de madrugada, el modo noche) el oído
  pierde graves y agudos antes que medios: la música suena «chiquita». Un realce suave de graves y agudos, solo
  cuanto más se baja, lo compensa. Iría con su interruptor en Ctrl+F3 → Sonido.
- **A3. Mezcla entre canciones (estilo DJ).** Hoy una canción se apaga en 0,5 s y entra la siguiente sin pausa. Una
  mezcla de 2 a 4 s, donde una baja mientras la otra sube, da ambiente de fiesta continua. Con su interruptor y
  apagada de fábrica, porque cambia el carácter de la rocola.
- **A4. Remuestreo de calidad.** En Windows XP, si la tarjeta de sonido trabaja a 48 kHz y la canción viene a 44,1 kHz,
  la conversión la hace el mezclador de Windows, que es de calidad baja: ensucia un poco los agudos. Hacerla dentro
  de la rocola, con un buen convertidor, lo evita. Cuesta algo de procesador; habría que medirlo en el equipo de dos
  núcleos.

## Para que elijas: textos en resoluciones bajas

- **T1. Letra más nítida en las texturas de la tarjeta.** El texto de la interfaz se suaviza en gris sin ajustarse a
  los píxeles. En Windows, ajustarse («GridFit») da letras chicas más nítidas. Aquí no se ve la diferencia (Linux lo
  ignora), así que habría que mirarlo en el bar antes de dejarlo.
- **T2. Los números de la barra de letras**, más grandes en 1024: hoy se miden para caber «8888» y salen diminutos. Con
  miles abreviados («2,3 mil»), la letra crecería algo.
- **T3. Los nombres de los discos de la pared.** Los dibuja un método del dibujo por CPU congelado, que comparte la
  tarjeta. Agrandarlos o pasarlos a dos renglones necesita tu permiso para hacer una versión solo de tarjeta.
