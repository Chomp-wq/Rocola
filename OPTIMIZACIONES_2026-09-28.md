# Optimizaciones del 28/09/2026 — más fluido en los equipos de dos núcleos

Pediste buscar mejoras de rendimiento en todo el programa y hacer las tres que salieron. Las tres son automáticas y
siempre activas: no cambian ni el sonido ni lo que se ve, así que no llevan interruptor. Van en las dos ramas (Modest y
HD). El dibujo por procesador (el viejo, congelado) no se ha tocado.

## 1. La música en su propio hilo

### Qué pasaba

La salida de sonido clásica (WaveOut) es la que usan todos los equipos de dos núcleos: Modest siempre, y HD cuando el
equipo no llega a "HD" (el modo HD pide cuatro núcleos). Esa salida pide la música desde el bucle de la pantalla: cada
50 ms la pantalla se paraba en seco a descomprimir la canción y a pasar el bloque por toda la cadena (nivelado, EQ,
realce, DSEE, grave, modo noche, limitador, fundido).

Medido en el servidor de pruebas, con todo el sonido encendido, cada bloque de 50 ms le costaba a la pantalla **1,8 ms
de media y picos de hasta 7,6 ms**, más cerca de 1 ms (picos de 9) cuando Modest descomprime con NLayer. Un cuadro de
pantalla dura 16,7 ms. En un procesador de dos núcleos de los viejos esas cifras pueden multiplicarse varias veces
(estimación, no medido): un solo bloque podía comerse un cuadro y se veía como un saltito en la estrella y en las
animaciones. Y mientras tanto el segundo núcleo estaba casi parado.

### Qué cambia

La cadena ahora la lee **un hilo propio para la música** (`Rocola/BombaSonido.cs`), con prioridad por encima de la
pantalla. Va dejando hecho un colchón corto (200 ms) y, cuando la salida pide un bloque, la pantalla solo copia lo que
ya está hecho. La salida, su colchón de 1 s y sus bloques de 50 ms siguen igual.

- **El sonido no cambia ni una muestra**: mismo orden y mismas cuentas.
- **Lo que se ve va a su tiempo**: la música se lee unos 160 ms antes que antes, y el pulso, la aurora y el relevo sin
  pausa suman ese adelanto (`RetrasoAudible`). Los cambios de Ctrl+F3 se oyen esos 160 ms más tarde; saltar o parar
  una canción sigue siendo inmediato.
- **Si el hilo se atrasa** (un disco que tarda en despertar): la pantalla lo espera hasta 600 ms, porque la salida aún
  tiene casi un segundo guardado y no se corta. Si ni así llega, entrega silencio en vez de colgarse, y no vuelve a
  esperar hasta que llegue música. Antes, en ese caso, la pantalla se quedaba congelada todo lo que tardara el disco.
- **El final y los fallos** llegan a la salida como antes: se acaba cuando se acabó lo que había, y un fallo de la
  cadena se atiende igual que siempre (aviso, crédito devuelto si toca, etc.).
- **Al parar**, el hilo se detiene y se espera antes de soltar el archivo: nadie lee un archivo cerrado, y la medida de
  la canción se guarda con el sonido de verdad quieto.
- La salida WASAPI de los equipos HD ya tenía su propio hilo: ahí no cambia nada.

### Comprobaciones automáticas

Batería nueva **`pruebas/bomba.cs`** (24 comprobaciones), verde en HD y en Modest:

- Pidiendo trozos de 1 a 9000 muestras, y a través de la vista flotante que NAudio pone sobre su memoria, sale todo,
  en orden y exacto; en mono, estéreo y 5.1, a 22, 44,1, 48 y 96 kHz. Al acabarse devuelve 0, como antes.
- Quien pide (la pantalla) nunca lee la cadena: la lee siempre el mismo hilo aparte, con prioridad por encima de lo
  normal.
- Un fallo a media canción llega después de lo que ya estaba hecho, con el error de la cadena dentro.
- Un atraso de 150 ms no deja ni una muestra de silencio. Uno de 2 s: la pantalla espera 600 ms como mucho, después ya
  no espera (0,9 ms la siguiente), entrega bloques de silencio enteros (nunca 0) y, al volver, no se pierde ni se
  repite ninguna muestra.
- Al pararla el hilo se va y nadie vuelve a leer la cadena, también si estaba a media lectura lenta.
- **La cadena de verdad** (abrir la canción + armar la cadena con nivelado, realce, DSEE, grave, noche, limitador y
  fundido): por la bomba sale **bit a bit lo mismo** que leída a mano (1.058.886 muestras), también abriendo la salida
  clásica con una salida de mentira.
- Los enganches: la salida clásica lee de la bomba; al parar va primero la salida, después el hilo y solo entonces se
  suelta el archivo; el pulso, la aurora y el relevo suman el adelanto; la bomba se crea en un solo sitio, que suelta la
  anterior.

**Lo que le cuesta a la pantalla** (simulación con una cadena que tarda 2 ms por cada 25 ms de música): cada bloque de
50 ms pasa de **4,07 ms a 0,07 ms** de media (0,2 ms el peor).

**Averías metidas a propósito**: 15, y la batería las detecta todas: esperar sin tope, no esperar nada, devolver 0 en vez de silencio, olvidar que el hilo va atrasado, dejarlo con prioridad normal, no esperarlo al parar, perder el fallo, no despertarlo al copiar, no marcar el final, no soltar la bomba anterior, quitarla de la salida, soltar el archivo antes que el hilo, calcular mal el adelanto y que el pulso o el relevo no lo sumen.

### Lo que no se ha comprobado

- Escucharlo en el bar y mirar la pantalla del equipo de verdad (si desaparecen los saltitos se ve con el medidor de
  Ctrl+F3).
- Windows XP real. Modest compila como C# 3 para .NET 3.5; el hilo usa solo lo que ya había en .NET 2.0.
- Un disco duro de verdad que tarda en despertar: el atraso está simulado.

## 2. El reloj de la canción, solo su recuadro

### Qué pasaba

La franja de abajo (lo que suena, lo que sigue, el reloj, los créditos, la cola con sus portaditas y la barra) es una
imagen aparte que se rehace cuando cambia lo que dice. Con música, el reloj ("1:47 / 4:12") cambia cada segundo, así que
**cada segundo se rehacía la franja entera** (todos los textos y las pastillas) y **se subía entera a la tarjeta**: 440 KB
a 1024×768, en el mismo hilo que dibuja la pantalla.

### Qué cambia

Si lo único que cambió es el reloj o la barra, se repinta solo su recuadro y la tarjeta recibe solo ese trozo
(`Rocola/FranjaLigeraGpu.cs`). Solo en el dibujo por tarjeta gráfica; el dibujo por procesador no se toca.

- Se pinta todo como siempre y en el mismo orden, pero recortado al recuadro, así que dentro sale lo mismo que saldría
  entera. El recuadro se vacía con copia directa, sin depender de cómo trate Windows el borrado con recorte.
- Los títulos que se deslizan no se tocan: un recuadro no los puede borrar.
- Cualquier otra cosa (otro título, otros créditos, la cola, la letra de lejos, otra pantalla, el reloj que cambia de
  sitio, la atracción) rehace la franja entera, como siempre.
- La barra de progreso de la franja va con su valor exacto: antes se rehacía por tramos de 1/300 de la canción; ahora su
  recuadro se repinta cada vez que avanza (dos veces por segundo). Se ve igual, un poco más fluida.

### Comprobaciones automáticas

Batería nueva **`pruebas/franjareloj.cs`** (16 comprobaciones), verde en HD y en Modest:

- **Idéntica a rehacerla entera, píxel a píxel**, en 24 escenas (dos pantallas; letra de lejos o no; títulos que se
  deslizan o no; barra en la franja, en neón o dibujada por la tarjeta) y 9 pasos de reloj y barra en cada una
  (0:59 → 1:00, 9:59 → 10:00, 59:59 → 1:00:00...): 216 de 216, todos por recuadro.
- Los títulos que se deslizan quedan igual que rehaciéndola entera.
- Si solo avanza la barra: por su recuadro e idéntica; si la dibuja la tarjeta, la franja ni se rehace.
- Lo demás, entera; la atracción, limpia; al salir, entera e idéntica.
- El trozo solo vale para la versión de justo antes.
- **Con la tarjeta imitada**: en 9 pasos la tarjeta recibe solo el recuadro, en la misma textura, y queda igual que la
  imagen; si cambia otra cosa o la tarjeta se saltó una versión, entera.

**Medido** (servidor de pruebas): el recuadro es de media el 13 % de la franja; lo que se sube cada segundo pasa de
**440 KB a 70 KB**, y rehacerla a 1920×1080 de **2,3 ms a 0,8 ms**.

**Averías metidas a propósito**: 13, y la batería las detecta todas: pintar sin recorte, no vaciar el recuadro o vaciarlo sin copia directa, redondear la barra, tocar las cintas de verdad, no mirar la firma fija ni la de la atracción, dar el trozo a cualquier versión, que la tarjeta suba siempre entera o no pregunte el trozo, quitar el margen del reloj o de la barra, y que el reloj no mire dónde está.

### Lo que no se ha comprobado

- Una tarjeta de verdad: la de las pruebas es imitada. Cuánto se nota se ve con el medidor de Ctrl+F3.
- El dibujo de Windows: las pruebas usan el de Mono. El recorte del dibujo se mantiene al mover el origen (así trabaja
  Windows y así lo dan las pruebas), pero no se ha mirado en un Windows real.
- Windows XP real.
