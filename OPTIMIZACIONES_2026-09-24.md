# Optimizaciones del 24/09/2026 — el dibujo por tarjeta, más ligero

Pediste más imágenes por segundo en máquinas modestas sin perder aspecto ni funciones. Esta tanda toca **solo el dibujo
por tarjeta gráfica**, en las dos ramas (Modest y HD). Es automática y siempre activa: no hay interruptor, porque no
cambia lo que se ve. El dibujo por procesador (el viejo, congelado) no se ha tocado.

## Qué cambia

### 1. Sin borrar la pantalla

Cada cuadro empezaba borrando la pantalla entera, y justo después se dibujaba el cielo, que es opaco y la tapa
entera. Borrar era escribir todos los píxeles para nada, una vez por cuadro. Ahora el cuadro empieza directamente por
el cielo.

### 2. Sin pintar lo transparente

Varias capas grandes se dibujaban como un rectángulo a pantalla completa aunque buena parte de su imagen fuera
transparente del todo. La tarjeta leía, mezclaba con nada y volvía a escribir el mismo píxel. Ahora se miran una vez
los trozos (teselas) que tienen algo y se dibujan solo esos, en una sola llamada:

- **La vía láctea**: deja sin pintar entre el 34 % y el 48 % de su imagen, según la pantalla.
- **La viñeta**: su centro, que es transparente (el 15 %).
- **La interfaz** (la imagen grande de arriba y la franja de abajo): cuando lleva 300 ms sin cambiar. Mientras cambia
  (escribiendo en el buscador, pasando páginas) se pinta entera como siempre, así que teclear no espera a nada.
- Lo que va con **opacidad cero** (la interfaz del todo fundida, la franja con el reposo abierto) ya no se manda.

Un trozo se salta solo si él **y el texel de alrededor** son transparentes del todo: la tarjeta, al suavizar, lee como
mucho un texel de al lado, así que un píxel que cae en un trozo saltado solo podía mezclar nada.

### 3. La aurora sin repetir cuentas

El color de partida de cada franja de la aurora solo depende de los colores de la canción y de la portada, pero se
calculaba en cada cuadro (más de mil mezclas de color). Ahora se guarda y se rehace cuando cambia algo de eso. Lo que
sí cambia en cada cuadro (el color que fluye, la altura, la música) se sigue calculando igual. Además, varias cuentas de
color pasan de "sin signo" a "entero": dan los mismos números y la conversión es mucho más rápida.

## Comprobaciones automáticas

Batería nueva **`pruebas/dibujoligero.cs`** (77 comprobaciones, verde en HD y en Modest). Usa una **tarjeta imitada**
que apunta todo lo que se le manda y un **raster de referencia** con las reglas de Direct3D 9 que usa la rocola:

- El cuadro no borra, lo primero que llega es el cielo opaco y tapa **cada píxel una vez** (5 pantallas × 5 cortes del
  cielo).
- La regla de los trozos en 1.440 imágenes hechas a propósito (texeles sueltos en esquinas y fronteras, alfa 1, alfa 0
  con color) y en las imágenes de verdad: ninguno con algo se salta, ninguno vacío se pinta, ninguno dos veces.
- **La misma imagen entera y a trozos**: la interfaz y la franja, **idénticas** (0 píxeles distintos, con cuatro
  opacidades). La vía láctea y la viñeta, que van estiradas, difieren como mucho **un nivel de 255 en 1 a 41 píxeles**
  de 786.432, por el redondeo de los vértices intermedios. Es invisible y del mismo tamaño que las diferencias entre
  dos tarjetas distintas dibujando lo mismo.
- La interfaz se trocea solo tras 300 ms quieta.
- La aurora: dos paneles, uno con lo guardado y otro rehaciéndolo siempre, mandan **los mismos vértices byte a byte**
  en 360 cuadros; y lo guardado se rehace al cambiar cualquiera de sus 14 dependencias.
- Las cuentas por entero dan los mismos números en 2,6 millones de combinaciones.
- **Un cuadro de verdad** con la tarjeta imitada: la interfaz y la franja se pintan donde siempre, igual enteras que
  a trozos, y no se mandan cuando no se ven.

**Averías metidas a propósito**: 20 en cada rama; la batería detecta 19. La que no detecta (quitar el borde de
seguridad de la vía láctea) no cambia nada visible: ese borde queda siempre fuera de la pantalla.

## Simulación (banco con la tarjeta imitada, 1024×768, navegación con música)

No son FPS reales: el banco cuenta lo que se le pide a la tarjeta y lo que tarda el procesador en pedirlo.

| | Antes | Ahora |
| --- | --- | --- |
| Píxeles pintados por cuadro | 8,58 pantallas | 6,89 pantallas (−20 %) |
| Procesador por cuadro, HD | 0,64 ms (mejor 0,57) | 0,39 ms (mejor 0,33) |
| Procesador por cuadro, Modest | 0,77 ms (mejor 0,58) | 0,38 ms (mejor 0,35) |

- La aurora, en 240 cuadros fijados (sin reloj de verdad), manda exactamente los mismos vértices antes y después:
  112 MB comparados byte a byte, en las dos ramas.
- En la atracción con escaparate, la interfaz y la franja son transparentes del todo: ahora no se pintan.

## Lo que no se ha comprobado

- **FPS en una tarjeta de verdad.** Cuánto se nota depende de si la tarjeta del bar va justa de píxeles (entonces
  ayuda el −20 %) o de procesador (entonces ayuda el −40 %). Se ve con el medidor de Ctrl+F3.
- **Windows XP real.** Modest compila como C# 3 para .NET 3.5, pero no se ha probado en XP.
- Direct3D real: la tarjeta de las pruebas es imitada.

# El buscador, más ligero para la tarjeta

Pediste también optimizar el buscador en todos sus apartados, sin tocar su aspecto ni lo que hace. Los revisé uno por uno
(con tarjeta gráfica; el dibujo por procesador no se toca):

| Apartado | Cómo estaba | Qué cambia |
| --- | --- | --- |
| Buscar (lo que casa con lo tecleado) | Ya optimizado en tandas anteriores: cada letra solo repasa lo que quedaba, las letras sueltas y las 8 últimas consultas se guardan, y de lo que casa solo se eligen los 40 que se ven. | Nada: no queda nada que ganar sin cambiar los resultados. |
| Teclear | Lo escrito va en su propia imagen chica y los resultados se posan al parar de teclear. | Nada. |
| Moverse por el teclado y los resultados | La tecla, la tarjeta y la portada elegidas van en piezas aparte. | Nada. |
| Posar una consulta, pedir una canción, avisos | Se rehacía la imagen grande (bien) y se **subía entera** a la tarjeta. | Se sube **solo el trozo que cambió**: la columna de resultados o el aviso. |
| Cerrar | Se **copiaba** la imagen grande y se **volvía a subir** para la animación de cierre. | La animación usa la textura que ya tenía la imagen. |
| Abrir | Se **creaba** una textura grande nueva. | Se reutiliza la de la última ventana cerrada. |
| Quieto en el buscador | La imagen grande se pintaba entera en cada cuadro. | Con la primera parte de hoy: a trozos tras 300 ms quieta. |

La imagen grande se sigue haciendo entera, exactamente como antes: lo único que cambia es lo que se le manda a la tarjeta.
Cuando no se sabe con seguridad qué trozo cambió (enfocar los resultados, otro color, otro tamaño, quedarse sin
resultados con los resultados enfocados porque vuelve el teclado), se sube entera como siempre.

## Comprobaciones automáticas

Batería nueva **`pruebas/buscaligero.cs`** (29 comprobaciones), con la misma tarjeta imitada:

- **Fuera del trozo no cambia ni un píxel**: se hace la imagen antes y después de 54 cambios (9 clases: posar
  consultas, quedarse sin resultados, avisos que entran, salen o cambian, marcas de la cola, géneros que llegan, canción
  pedida con su aviso), con el teclado y con los resultados enfocados, en tres pantallas.
- Lo que cambia media pantalla pide la imagen entera.
- La tarjeta recibe exactamente ese trozo y su textura queda **idéntica** a la imagen; si no tiene la versión de justo
  antes, se sube entera.
- Cerrar: la ventana que se encoge recibe **los mismos vértices y la misma imagen** por el camino nuevo y por el de
  siempre. Abrir: se reutiliza la textura grande sin crear ninguna, pero nunca mientras la ventana se sigue dibujando.

La batería encontró un caso que la primera versión no cubría (quedarse sin resultados con los resultados enfocados
cambia media pantalla); está corregido y queda vigilado.

**Averías metidas a propósito**: 16; la batería detecta 15. La que no detecta (quitar el foco de la primera parte de la
firma) no cambia nada: esa parte ya cambia por el cubo de la portada, que depende del foco.

## Simulación (banco con la tarjeta imitada)

Lo que cuesta al procesador copiar a la textura (sin contar lo que tarde la tarjeta de verdad en recibirlo):

| | 1024×768 (imagen de 2,6 MB) | 1920×1080 (imagen de 7,1 MB) |
| --- | --- | --- |
| Posar una consulta: subir | 0,43 → 0,24 ms (la columna) | 1,24 → 0,73 ms |
| Un aviso que entra o sale: subir | 0,43 → 0,01 ms | 1,24 → 0,01 ms |
| Cerrar: copiar y subir la imagen | 0,61 → 0 ms | 1,83 → 0 ms |
| Quieto, lo que se pinta (primera parte) | 76 % con el teclado, 36 % con los resultados | 82 % y 39 % |

En la tarjeta de verdad el ahorro está sobre todo en lo que viaja hasta ella: una subida entera marca la textura
completa (a 1920×1080 son 8 MB) y un aviso ahora son unos 100 KB. Al abrir ya no se crea una textura de 8 MB, y mientras
el buscador está abierto hay una textura grande menos en la tarjeta. Rehacer la imagen al posar una consulta sigue
costando lo mismo (unos 3 ms a 1024×768, casi todo las 40 tarjetas): eso lo hace el mismo código que el dibujo por
procesador, que no se toca.

## Lo que no se ha comprobado

- Cuánto se nota en una tarjeta real (sobre todo al posar, pedir canciones, abrir y cerrar): se ve con el medidor de
  Ctrl+F3 en el bar.
- Windows XP real. Modest compila como C# 3 para .NET 3.5.
