# Performance: más ligero con la pantalla casi igual, y un medidor más listo — Modest, 13 de octubre de 2026

Petición del usuario: «Haz las 4 cosas, pon los nuevos settings gráficos como un preset llamado performance en la ventana
de imagen, y el benchmark renuévalo para que sea más inteligente y mejor».

Las cuatro cosas eran las de la propuesta para que un equipo modesto pase de 30 a 60 cuadros por segundo:

1. medir dónde se va el cuadro;
2. pintar las nebulosas y la vía láctea a media resolución;
3. pintar el grano y la viñeta en una sola pasada;
4. juntar las órdenes de dibujo.

Todo es solo con tarjeta gráfica (`PerformanceGpu.cs`). El dibujo por CPU no cambia: se comparó con la versión anterior,
instrucción por instrucción.

## Dónde está

**Ctrl+F3 → Imagen → tarjeta EFECTOS.**

- **«Todo de golpe»** tiene ahora cuatro botones: **Bajo, Medio, Alto y Performance**.
  - **Performance:** los efectos en Alto y los dos ahorros nuevos encendidos. La pantalla queda casi igual y la tarjeta
    trabaja menos.
  - **Alto:** la pantalla de siempre, sin ahorros.
  - **Bajo y Medio:** bajan los efectos y llevan también los ahorros, porque son para ganar rendimiento.
- **Hoja nueva «Performance»**, junto a Efectos y Fluidez:
  - los dos ahorros, cada uno con su interruptor, su explicación y lo que ahorra en este equipo (después de medir);
  - lo que va siempre activo (juntar órdenes);
  - cuántas órdenes y pantallas se pintan por cuadro.
- Los dos interruptores se guardan: `cielo_media=` y `grano_vineta=`. **Vienen apagados de fábrica**, y así la
  pantalla es idéntica a siempre. «Gráficos como de fábrica» los apaga.

## Las cuatro cosas

### 1. Medir dónde se va el cuadro

Cada cuadro cuenta:

- las órdenes de dibujo que pide el programa;
- las que llegan de verdad a la tarjeta;
- los cambios de estado;
- mientras mide el medidor, las pantallas que se pintan.

Hasta ahora el medidor solo veía lo que gastaba el procesador en armar el cuadro, y nada de lo que tarda la tarjeta. Ahora
puede **presentar sin esperar al monitor** mientras mide (ver más abajo).

### 2. El cielo a media resolución (interruptor «Cielo a media resolución»)

El degradado del cielo, las nebulosas y la vía láctea se pintan en una imagen intermedia de la mitad de ancho y de alto, y
esa imagen se estira una vez, opaca, a la pantalla.

- **Por qué casi no se nota:** las nebulosas y la vía láctea ya vienen de imágenes de un cuarto de la pantalla, así que a
  media resolución les sobra detalle.
- **Medido con la tarjeta imitada**, en 640×480 y 801×601 y con la deriva en cuatro posiciones:
  - las nebulosas y la vía láctea pintan **el 25 %** de los píxeles de antes;
  - contando el cielo y el estirado, **un 49 %** del total de esas capas;
  - más de 2 niveles de 255 cambian **menos del 0,05 % de los píxeles**;
  - el que más cambia, **4 niveles** (6 con un ancho impar); de media, 0,25 niveles.
- **El borde, corregido.** En la primera versión, la última columna o fila de la pantalla salía hasta 19 niveles más
  oscura en las posiciones extremas de la deriva: el filtro leía un píxel de la imagen que ya caía fuera de la pantalla, y
  ahí no había nebulosa. Ahora el estirado va en cuatro trozos y el borde lee solo el último píxel bueno.
- Si la tarjeta no puede pintar en una imagen intermedia, se dibuja como siempre. La imagen intermedia se suelta antes de
  cada reinicio del dispositivo (al bloquear la sesión, al cambiar la resolución) y se rehace sola.
- Con las nebulosas apagadas no se usa: no habría nada que ahorrar.

### 3. Grano y viñeta en una pasada (interruptor «Grano y viñeta en una pasada»)

La tarjeta usa dos texturas a la vez, el grano y la viñeta, y mezcla las dos capas en la misma pasada: lee y escribe la
pantalla una vez en lugar de dos.

- **La cuenta es la misma** que las dos pasadas seguidas. Solo cambia el redondeo: con la tarjeta imitada, como mucho
  **1 nivel de 255**, con y sin huecos de portadas.
- **Se pinta un 55 %** de lo de antes en esas capas.
- **El grano pasa a ir encima de las estrellas**, porque la viñeta va después de ellas. El grano mueve 2 niveles de 255
  como mucho: en una estrella blanca, algún píxel queda a 253.
- **Sin las capacidades necesarias, nada cambia.** Si la tarjeta no tiene dos texturas a la vez, la mezcla necesaria o el
  segundo juego de coordenadas, se dibuja como siempre. La primera vez se le pregunta al controlador si lo hace en una
  pasada; si falla una vez, no se vuelve a intentar con ese dispositivo.
- **Lo siguiente se dibuja igual que antes.** Después de esta pasada, la segunda textura se apaga y vuelven el vértice y la
  mezcla de siempre.

### 4. Juntar órdenes de dibujo (siempre activo, sin interruptor)

Las órdenes chicas y seguidas con el mismo estado (misma textura, misma mezcla, mismo modo) se mandan juntas. Cualquier
cambio de estado, un dibujo grande o el final del cuadro mandan antes lo pendiente.

- **Por qué no lleva interruptor:** los triángulos llegan en el mismo orden y con el mismo estado, así que la tarjeta pinta
  **exactamente lo mismo** (AGENTS.md).
- **Comprobado:** cuatro secuencias de 160 dibujos al azar dan **0 píxeles distintos** con y sin juntar, y 101 órdenes
  menos en total.
- **No medido:** cuánto se ahorra en una pantalla real con portadas. Lo dirá el medidor en el equipo del bar.

## El medidor renovado (Imagen → «Medir este equipo»)

Tarda de 20 a 45 segundos y sigue sin guardar ni aplicar nada por su cuenta.

- **Mide sin esperar al monitor.** Con la espera, un cuadro de 17 ms se enseña a los 33 y el número salta de 60 a 30 sin
  decir cuánto falta. Mientras mide las pantallas, el medidor pide presentar en cuanto el cuadro está listo. Así se ve el
  ritmo que dan de verdad el procesador y la tarjeta. Al acabar devuelve la espera, aunque algo falle por el camino.
  - Si la tarjeta no lo permite, lo dice y usa lo de antes (solo el procesador).
  - Las portadas se siguen midiendo con el monitor, como siempre, para que su comparación entre perfiles siga valiendo.
- **Una tabla en columnas**, con lo típico de cada pantalla (la mediana, que no se la lleva un cuadro raro):
  - lo que tarda un cuadro;
  - cuánto es del procesador;
  - cuánto espera a la tarjeta;
  - los cuadros por segundo que da eso.
- **Dice qué frena:**
  - «Frena la tarjeta gráfica: espera 9 ms de cada 14»;
  - o el procesador al armar el cuadro;
  - o la música y los mensajes entre cuadros.

  También dice cuántas órdenes y pantallas pinta cada cuadro.
- **Apunta a la pantalla.** La meta es el refresco del monitor (60 con una pantalla de 60 Hz), con un 8 % de margen: 15,3
  ms por cuadro. Si no se alcanza ni bajándolo todo, la meta pasa a ser la mitad, 30 (30,7 ms).
- **Prueba primero lo que casi no se nota.** Prueba los dos ahorros de Performance uno a uno, cada uno entre dos medidas
  sin él. Lo que tiembla la máquina entre esas dos es el ruido, y un ahorro más chico que el ruido no cuenta.
- **No mide de más.**
  - Si la pantalla ya llega a 60, no prueba nada más.
  - Si los ahorros bastan, no mide los efectos («no hizo falta»).
- **La propuesta, primero lo que menos se nota:**
  - los ahorros, los justos;
  - si no bastan, los escalones de efectos, con la regla de siempre (`PlanDeAhorros`).
- **La comprueba.** Pone la propuesta, mide otra vez la pantalla que peor iba y lo apunta: «Comprobado: 63 cuadros/s
  (antes 48; la cuenta daba 61)». Los ahorros no siempre se suman: si frena el procesador, aliviar la tarjeta no da nada.
- **Dos botones:** «Aplicar lo recomendado» y **«Solo Performance»**, que enciende solo los dos ahorros sin tocar los
  efectos.

## Cómo se comprobó

- **`performance`** (nueva): 56 comprobaciones con tarjeta y 11 sin ella. Lleva una tarjeta imitada con imagen
  intermedia y dos texturas, y un raster de referencia con las reglas de Direct3D 9 que usa la rocola (operaciones de etapa,
  mezclas, repetir o pegar al borde).
  - Juntar: la misma imagen, menos órdenes.
  - Cielo a media resolución: lo de arriba. Además, la imagen tapa toda la pantalla, también la columna y la fila del
    borde.
  - Grano y viñeta: lo de arriba. Lo siguiente se dibuja con la segunda etapa apagada.
  - Sin esperar al monitor:
    - se pide y se devuelve;
    - el reinicio suelta antes la imagen intermedia;
    - si la tarjeta no lo permite, no se pide.
  - El cuadro entero con los dos ahorros se presenta, y apagados sale el cuadro de siempre.
  - La propuesta (`PlanDeAhorros`) con números de mentira, y el medidor por dentro (lee su código).
  - El preset, los interruptores, su guardado y la página.
- **Siguen pasando:**
  - `banco` (la regla de los escalones no cambió);
  - `dibujoligero`;
  - `efectoslayout` y `ajustesfluidez` (ahora con 4 botones de golpe y 3 hojas);
  - `ventanaf3` (todos los textos caben, también con la letra más ancha);
  - `pestanas`, `cuboagrupado`, `buscaligero`, `ruidomalla`, `fondocoste`, `fondo`, `barrido`, `anim`, `cielovivo` y
    `hojeofino`.
- **Tanda completa (`pruebas/todo.sh`):** los mismos problemas de siempre y uno más: `ventanareal`, una prueba de
  tiempos que en esa vuelta dio 190 ms donde pide 150. Sola pasó las cuatro veces (dos con tarjeta y dos sin ella): fue
  la carga de la tanda. `performance` pasa en los dos perfiles.
- **Dibujo por CPU:** idéntico, en los 14 métodos.

**No probado aquí:**

- **Una tarjeta de verdad.** Aquí no hay Direct3D: la tarjeta es imitada.
- **Cuánto se gana en el equipo del bar.** Para saberlo hay que medir allí con «Medir este equipo».
- **Que su controlador haga bien la imagen intermedia, las dos texturas y el presentar sin esperar.** Si alguno falla, la
  rocola vuelve a dibujar como siempre.
- **Windows XP.**
