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
