# La pantalla viva, tanda 1: la pared y las letras

Cambio del 24 de septiembre de 2026, compartido por HD y Modest. Son las primeras ideas que elegiste del lienzo de
bocetos. Solo cambian el dibujo por tarjeta gráfica. El renderizador por procesador sigue congelado y sale exactamente
igual: una batería lo comprueba píxel a píxel.

Cada idea tiene su interruptor en una pestaña nueva de Ctrl+F3, **Pantalla**. Todas vienen encendidas y se guardan en
`rocola_config.txt`. La pestaña existe también sin tarjeta gráfica: ahí los interruptores se guardan, pero no cambian
nada de lo que se ve.

El código nuevo está en `Rocola/PantallaVivaGpu.cs`. La pared (`Form1.Pared.cs`), la barra de letras
(`Form1.Vistas.cs`) y el cuadro de la tarjeta (`D3D9Star.cs`) llevan solo los enganches.

## 1. Portada enfocada — `portada_enfocada=`

- La portada elegida crece un 12 % en 170 ms, frenando al llegar.
- Crece desde el centro de su borde de abajo, hacia arriba y hacia los lados. Hacia abajo no se mueve: debajo va el
  nombre del disco y tiene que seguir leyéndose.
- El contorno y el halo crecen con ella. El halo toma el color de la propia portada: el mismo tono, con toda la luz y al
  menos un 55 % de color.
  - Se calcula una sola vez por portada, con 36 puntos, pesando más los que tienen color.
  - Una portada gris o negra deja el color de siempre, el del género.
- Las demás portadas bajan un 20 % de brillo, poco a poco. La elegida no.
- Sobre el borde de abajo de la portada, por dentro, sale un letrero:
  - Dice **"12 canciones · 38 min"**.
  - Los minutos solo salen si se sabe lo que dura cada canción; si falta alguna, no se inventan.
  - Un disco de vídeos dice cuántos vídeos trae.
  - Puede ser un poco más ancho que la portada, pero nunca se sale de su casilla. A 1024×768 la portada mide 128 px y
    el texto no cabría a una letra que se lea.
  - Si aun así no cabe, la letra baja un poco. Si ni así cabe, se quitan los minutos.
- La portada que vuela al entrar al disco sale de donde se ve, ya crecida, sin saltito.
- Apagado, todo sale como antes, bit a bit, incluido el halo.

## 2. Ficha rápida del disco — `ficha_disco=`

- Si el cursor se queda **1,1 s** en un disco, al lado sale una tarjeta. Entra en 220 ms, subiendo un poco.
- Qué dice la tarjeta:
  - El artista, el disco, cuántas canciones trae y cuánto dura.
  - Su género y cuántas suyas van ya en la cola.
  - Las tres más pedidas, con cuántas veces se pidió cada una. Si ninguna se pidió nunca, van las tres primeras y la
    tarjeta dice "ALGUNAS DE ESTE DISCO".
  - Abajo, el botón verde: "Entrar para ver las 12".
- La duración de cada canción solo aparece si al título le sigue quedando al menos media fila. En una tarjeta
  estrecha manda el título.
- Dónde se pone: a la derecha de la casilla si cabe; si no, a la izquierda. Nunca se sale de la pared.
- Cuándo se va:
  - En cuanto el cursor se mueve.
  - Con el buscador abierto, en el modo atracción, dentro de un disco o cuando se amplía el panel de reposo.
  - Sola, si nadie toca nada en 9 s. No vuelve a salir hasta cambiar de disco.
- Si alguien pide una canción de ese disco mientras se ve, se rehace para que el número de la cola cuadre.
- "Lo más sonado" no lleva ficha: ya es una lista de las más pedidas.
- Cuánto cuesta:
  - Se prepara **una vez** por parada, 250 ms antes de salir. Así el cuadro en que aparece no carga con el dibujo ni con
    la subida a la tarjeta.
  - La portadita es la que ya está en memoria: no pide nada al disco.

## 3. Pasar de página deslizando — `pagina_desliza=`

- Antes, la página nueva entraba y la vieja desaparecía de golpe. Ahora las dos van en fila: la vieja sale por un lado
  mientras la nueva entra por el otro.
- Cada columna sale un poco después que la de delante, en el sentido del movimiento: primero la vieja, columna a
  columna, y detrás la nueva.
- Todas siguen la misma curva, solo que más tarde, así que ninguna alcanza a la de delante. Las páginas nunca se pisan:
  la batería lo simula milisegundo a milisegundo en los dos sentidos.
- Dura lo mismo que antes, **230 ms**.
- Los números salieron de simular la pared a mitad de camino:
  - La primera versión, con 8 ms entre columnas y la vieja apagándose pronto, dejaba la pared cubierta menos de un
    tercio a los 100 ms. Parecía vacía.
  - Ahora van 4 ms entre columnas (44 ms de punta a punta). La nueva aparece en su primer cuarto de camino y la vieja
    se apaga solo al final.
  - La curva mezcla "arranca y frena suave" con "arranca ya". La primera columna se mueve desde el primer cuadro,
    ningún paso pasa del 1 % del camino por milisegundo, y la pared nunca queda cubierta menos de un 75 %.
- El cielo se corre un poco en el mismo sentido: un 4,5 % del ancho de la pared, mucho más despacio que las portadas.
  Eso se lee como profundidad.
  - Cada tamaño de estrella se corre según lo lejos que está: el polvo un tercio, las grandes entero.
  - Si la pared estaba tapada mientras pasaba (por ejemplo, se abrió el buscador), lo que faltaba se tira, para que el
    cielo no se corra de golpe después.
- La página que sale no hornea nada ni pide portadas: usa lo que ya estaba hecho.
- Si se pasa otra página antes de que llegue la anterior, la de a medio entrar se va de golpe, como siempre. Sacarla
  desde su sitio sería un salto.
- Apagado, es el deslizamiento de antes.

## 4. Barra de letras con lupa — `letras_lupa=`

- La letra elegida crece un 45 %, y sus vecinas un 30 % y un 9 %. A tres casillas ya no se nota, y más lejos no cambia
  nada.
  - Un 55 % también se probó, pero la letra rozaba el borde de la cápsula.
- La lupa viaja con la cápsula cuando cambias de letra.
- Una letra que ya llena su casilla no crece ("VIDEO"), para no comerse a las vecinas.
- Debajo de cada letra, una rayita dice cuánta música tiene comparada con la que más tiene: de 3 px, casi nada, a toda
  la casilla.
- Va en su propia tira: la barra de siempre, la del dibujo por procesador, ni se crea ni se toca por esto.

## Lo que cuesta

Medido en este contenedor, con Mono. Sirve para comparar unas cosas con otras; los milisegundos del bar serán otros.

| Qué | Cuánto | Cuándo pasa |
|---|---|---|
| Rehacer la barra de letras con lupa | 2,07 ms (la de siempre: 1,97 ms) | Al cambiar de letra |
| Barra de letras sin cambios | 0,27 ms | En cada reconstrucción de la interfaz |
| Hornear la ficha rápida | 1,42 ms | Una vez por disco en el que te paras |
| Hornear el letrero | 0,22 ms | Una vez por disco en el que te paras |
| Color de una portada | 10 µs | Una vez por portada |
| Rejilla por cuadro, pasando de página | 8 µs, con 36 casillas (quieta: 3 µs, con 18) | Durante los 230 ms del paso |
| Portada enfocada | 9 µs | Cada cuadro |

- Ninguna idea hornea ni sube nada a la tarjeta en un cuadro quieto.
- Pasando de página se dibujan hasta 36 casillas en vez de 18. Sus texturas ya están subidas, así que la tarjeta
  solo dibuja dos páginas de cuadrados.

## Cómo se comprobó y qué falta

- **Pruebas automáticas.** La batería nueva `pruebas/pantallaviva.cs` corre con los dos perfiles:
  - Con tarjeta gráfica: 108 comprobaciones.
  - Sin tarjeta gráfica: 10, solo la pestaña y que no haya nada más.
  - Se rompió a propósito cada protección de la tanda (39 mutaciones) y la batería las cazó; el resultado está en el
    mensaje del cambio.
  - Tandas completas en verde en las dos ramas.
- **Simulación, no comprobación real.** Con las mismas funciones del programa se compusieron imágenes del paso de
  página, cuadro a cuadro, y de la portada enfocada con su letrero y su ficha. Coloca las casillas donde las pone el
  programa, pero no es la tarjeta gráfica dibujando.
- **Sin comprobación visual real.** En este contenedor no hay Direct3D, así que nadie ha visto todavía el resultado
  en una pantalla de verdad. Hace falta mirarlo en el equipo del bar y en el de HD.
- **Sin probar en Windows XP.** En la rama Modest compila con el lenguaje de .NET 3.5 (C# 3), pero no se ha probado en
  un Windows XP real.

## Lo que viene

Las otras ideas que elegiste, por tandas, cada una con sus pruebas y en las dos ramas:

2. **Dentro del disco.** Hecha: el vinilo que asoma, la lista grande, el ecualizador, "en cola · en ~N min" y el
   vuelo desde la lista hasta "En cola". Ver [la guía de la tanda 2](PANTALLA_VIVA_DISCO_2026-09-24.md).
3. **Lo que suena y los créditos.** Hecha: lo que suena en grande, la moneda que cae, sin créditos invitar en vez de
   regañar y avisos que se entienden. Ver [la guía de la tanda 3](PANTALLA_VIVA_SUENA_2026-09-24.md).
4. **Modo atracción.** Hecha: el escaparate y lo más pedido esta noche. Ver
   [la guía de la tanda 4](PANTALLA_VIVA_ATRACCION_2026-09-24.md).
5. **Estilo general.** Hecha: letra para leer de lejos y el color del género en todo. Ver
   [la guía de la tanda 5](PANTALLA_VIVA_ESTILO_2026-09-24.md).
6. **El buscador.** Hecha después, con la idea "teclado compacto y resultados con portada": el teclado compacto, las
   tarjetas grandes con portada que entran una tras otra y los atajos. Ver
   [la guía de la tanda 6](PANTALLA_VIVA_BUSCADOR_2026-09-24.md).
