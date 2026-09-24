# La pantalla viva, tanda 5: el estilo general

Cambio del 24 de septiembre de 2026, compartido por HD y Modest. Son las dos ideas del lienzo de bocetos para el estilo
de toda la pantalla: letra para leer de lejos y el color del género en todo. Solo cambian el dibujo por tarjeta
gráfica. El renderizador por procesador sigue congelado: las etiquetas de verdad (su letra, su sitio y su color) y el
acomodo de la ventana no se tocan, así que lo que pinte el procesador sale como siempre.

Cada idea tiene su interruptor en Ctrl+F3 → **Pantalla**, hoja **La atracción y el estilo**, tarjeta **ESTILO
GENERAL** (al lado del modo atracción). Las dos vienen encendidas y se guardan en `rocola_config.txt` (`letra_lejos=` y
`color_genero=`).

El código nuevo está en `Rocola/EstiloGpu.cs`. Los demás archivos llevan solo los enganches: la franja de abajo y los
brillos de la interfaz (`Form1.Video.cs`), el cuadro de la tarjeta (`D3D9Star.cs`), el color que se le pasa en cada
latido (`Form1.cs`, `Rendimiento.cs`), la configuración (`Form1.Datos.cs`) y la pestaña (`PantallaVivaGpu.cs`).

## 1. Letra para leer de lejos — `letra_lejos=`

La tarjeta de lo que suena, abajo, es lo que se lee de pie, a dos o tres metros de la rocola. Con esta idea, en la
imagen que hornea la tarjeta gráfica:

- **El título crece un 45 %.** Se queda con casi todo el alto del renglón de lo que sigue. Si con ese tamaño no cabe,
  crece lo que quepa, de 5 en 5 % (como poco, un 15 % más que antes); si ni así cabe, termina en "…".
- **El artista baja casi un renglón** (lo que deja la caja del reloj), con su pastilla del género y el letrero
  MEJORADA. El brillo del letrero va con él.
- **Lo que sigue se va a ese mismo renglón, a la derecha**: empieza detrás del artista, su pastilla y el letrero, y
  llega hasta el borde de las cápsulas, en un gris mucho más claro (antes casi no se leía). Si no cabe, termina en
  "…".
- **El recorte con "…" lo hace la propia rocola**, no la librería de dibujo: así sale igual en cualquier equipo. En la
  simulación (con Mono) se vio que, con el texto alineado a la derecha, la librería se comía el principio
  ("ue: Cielito…") en vez del final. En Windows quizá no pase, pero así no depende de eso, y una prueba lo vigila.
- **La franja mide lo mismo.** La pared de discos, la portada, las cápsulas, la barra de avance y el reloj siguen
  donde estaban.

## 2. El color del género en todo — `color_genero=`

El color del género de lo que suena ya tenía el cielo, las nebulosas, la aurora, la barra de avance y la canción
elegida de la lista. Ahora también:

- **La portada de lo que suena** lleva detrás un resplandor de ese color. Es la chica de abajo y, cuando la rocola se
  queda en reposo y el panel crece, la grande (el resplandor va con ella).
- **La portada grande de dentro de un disco**, también.
- **El pulso de la letra elegida** en la barra de letras toma el color del género que manda en esa letra (el mismo
  que luce la sección), en vez del azul de siempre.

El resplandor respira despacio con su propio reloj (una vez cada 5 segundos), no con la música: nada da saltos.

## Lo que cuesta

Medido en este contenedor, con Mono. Sirve para comparar unas cosas con otras; los milisegundos del bar serán otros.

| Qué | Cuánto | Cuándo pasa |
|---|---|---|
| Hornear la tarjeta de lo que suena (texto) | 0,29 ms de lejos; 0,23 ms la de siempre | Solo cuando cambia lo que dice (canción, reloj, cola…) |
| Calcular el resplandor de una portada | 1,8 µs (con la llamada de la prueba incluida) | Cada cuadro, si hay portada |
| Dibujar el resplandor | un brillo más a la tarjeta, del mismo tipo que los que ya dibuja | Cada cuadro, si hay portada |

## Cómo se comprobó y qué falta

- **Pruebas automáticas.** La batería nueva `pruebas/estilo.cs` corre con los dos perfiles:
  - Con tarjeta gráfica: 29 comprobaciones. Entre ellas:
    - pintar la franja de lejos no toca las etiquetas;
    - el título se lee más grande (1,4 veces el alto de sus letras) y el artista baja justo lo que debe (medido en
      píxeles);
    - lo que sigue sale en el renglón del artista, detrás de lo último verde y pegado a la derecha, en el gris claro,
      y el gris de antes ya no aparece;
    - con textos largos, los dos terminan en "…" y nada se sale de su sitio;
    - el resplandor está centrado, es del color del género y respira sin saltos.
  - Sin tarjeta gráfica: 7 (la pestaña y que no hay nada más).
- **Mutaciones.** Se rompió a propósito cada protección de la tanda (44 mutaciones) y la batería las cazó todas. Una
  sobrevivía al principio: quitar el letrero MEJORADA de la cuenta de dónde empieza lo que sigue. La prueba ponía la
  pastilla del género detrás del letrero, al revés que el programa; se corrigió.
- **Las baterías de antes** se pusieron al día donde cambió algo a propósito: `pestanas` conoce los dos interruptores
  nuevos. Tandas completas en verde en las dos ramas.
- **Simulación, no comprobación real.** Con las mismas funciones del programa se horneó la tarjeta de lo que suena
  como estaba y con la tanda 5. El resplandor está aproximado: no es la tarjeta gráfica dibujando.
- **Lo que las pruebas no ven:** el brillo mismo que dibuja la tarjeta. Las pruebas comprueban lo que la rocola le pasa
  y en qué orden lo dibuja.
- **Sin comprobación visual real.** En este contenedor no hay Direct3D: hace falta mirarlo en el equipo del bar y en
  el de HD.
- **Sin probar en Windows XP.** La rama Modest compila con el lenguaje de .NET 3.5 (C# 3), pero no se ha probado en un
  Windows XP real.

## Con esto terminan las ideas elegidas

Las cinco tandas de la pantalla viva están hechas:

1. la pared y las letras;
2. dentro del disco;
3. lo que suena y los créditos;
4. el modo atracción;
5. el estilo general.

Cada idea tiene su interruptor en Ctrl+F3 → Pantalla.
