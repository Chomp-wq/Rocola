# J1, J2 y los ajustes que el Performance deja sin efecto — Modest, 14 de octubre de 2026

Petición del usuario: «Haz J1 y J2 y revisa si están activados settings de performance, pon en gris los settings
gráficos normales que el performance anule, si es que anula alguno al ser activado» (de
[PERFORMANCE_PROPUESTAS_2_MODEST_2026-10-14.md](PERFORMANCE_PROPUESTAS_2_MODEST_2026-10-14.md)). J3 no se hizo.

Todo es solo con tarjeta gráfica. El dibujo por CPU no cambia: sus 14 métodos son idénticos.

## Lo que se gana

Con la tarjeta imitada en 1024×768, sin portadas (J1 solo actúa con portadas delante):

| Fondo | Pantallas pintadas por cuadro |
| --- | --- |
| De fábrica (Alto) | 5,98 |
| Performance de antes | 3,05 |
| Con G1 | 2,42 |
| Con G1 y G2 | 1,61 |
| **Con G1, G2 y J2** | **1,16** |

Con J2, volver a pintar el cielo cuesta un 25 % de lo que costaba a la mitad. Es el cuadro que más pesa, y al pasar de
página toca en todos. En la pared, J1 quita además lo que tapan las portadas: el estirado pinta entre un 83 y un 89 %
con dos portadas de prueba, y con una pared entera, alrededor del 82 %.

## J1. El estirado del cielo no se pinta debajo de las portadas (automático)

- **Qué hace.** El estirado opaco del cielo salta el interior de las portadas opacas, con los mismos huecos que ya usan
  las otras capas (P2).
- **Por qué no lleva interruptor.** Dentro del hueco la portada tapa todo con alfa 255, así que se ve igual.
- **Lo único que cambia es el redondeo.** Partir el estirado en trozos cambia en 1 nivel de 255 algún píxel, entre el
  2,6 y el 2,9 %. Es lo mismo que ya pasaba con las capas de P2, y no se ve.

## J2. «Cielo a un cuarto de resolución» (interruptor nuevo)

**Ctrl+F3 → Imagen → tarjeta EFECTOS → hoja Performance.** Viene apagado de fábrica, se guarda al momento
(`cielo_cuarto=`) y lo enciende el botón Performance de «Todo de golpe» (también Bajo y Medio, como los otros ahorros).
«Gráficos como de fábrica» lo apaga.

- **Qué hace.** La imagen del cielo (degradado, nebulosas, vía láctea y, con G2, el grano y la viñeta) se pinta a un
  cuarto de ancho y de alto, en vez de a la mitad.
- **Necesita «Cielo a media resolución»**, igual que G2.
- **Lo que cambia a la vista (ligero).** Comparado con el cielo a la mitad, como mucho 5 o 6 niveles de 255, de media
  0,3 a 0,8, y el brillo medio no se mueve (-0,03). Las nebulosas quedan algo más suaves y el grano algo más grueso.
- **El borde, bien.** La última columna y la última fila de la pantalla leen solo la última parte buena de la imagen,
  como a la mitad. Se comprobó pintándolo sobre dos fondos distintos: tapa toda la pantalla, sin basura.
- **El medidor** lo prueba encima del cielo a media resolución y, si lo propone, propone también ese.

## Revisión: qué deja sin efecto el Performance

Repasé uno por uno los ajustes gráficos normales de Ctrl+F3 (Imagen, Fondo, Pantalla, Detalles y Colores):

- viñeta suave;
- nebulosas azul / violeta y con detalle natural, y su fuerza;
- profundidad al pasar de página;
- cielo por hora;
- los niveles de efectos;
- aligerar al pasar de página;
- y los demás.

**Ninguno queda anulado por el Performance.** Lo que va dentro de la imagen del cielo (los colores, el detalle natural,
la viñeta, el empujón de la página, el cielo por hora) se sigue aplicando ahí.

Sí hay ahorros del Performance que en ciertos casos no hacen nada. Ahora van **en gris y dicen por qué**, en el
renglón de debajo de su nombre, y vuelven solos en cuanto dejan de estar sin efecto:

| Ahorro | Cuándo no hace nada | Lo que dice |
| --- | --- | --- |
| Cielo a media resolución | nebulosas en Bajo (Efectos) | «Necesita: Nieblas y nebulosas en Medio o Alto (Efectos)» |
| Grano y viñeta en una pasada | sin «Viñeta suave» (Fondo) | «Necesita: Viñeta suave (Fondo)» |
| Grano y viñeta en una pasada | con «Fondo en una sola capa» funcionando | «Sin efecto: ya va dentro del cielo (Fondo en una sola capa)» |
| Fondo en una sola capa y Cielo a un cuarto | sin «Cielo a media resolución», o con las nebulosas en Bajo | lo que falte |

Ponerlos en gris no cambia lo guardado: en cuanto vuelven a valer, hacen lo suyo.

La hoja Performance se rehízo para que quepan los cuatro, con su explicación, lo medido de cada uno y lo que va
siempre activo («juntar órdenes y no repetir»).

## Cómo se comprobó

- **`performance`:** 88 comprobaciones con tarjeta y 12 sin ella. Lo nuevo:
  - **J1, a 640×480 y 801×601:** fuera de las portadas, como mucho 1 nivel de 255; dentro, no se pinta nada; el
    estirado pinta menos.
  - **J2, a la mitad contra un cuarto, sin G2 y con G2:**
    - se vuelve a pintar al cambiar y luego se reúsa;
    - tapa toda la pantalla, también el borde;
    - cuesta el 25 %;
    - casi igual (lo de arriba).
  - **El preset**, el guardado de `cielo_cuarto=` y la propuesta del medidor con cuatro ahorros.
  - **La tabla de 1024×768** de arriba.
- **`ajustesvivos`:** 56 comprobaciones con tarjeta y 32 sin ella. Los cuatro ahorros dependen de lo suyo. Lo nuevo:
  - con todo encendido, vivos;
  - la pasada de grano y viñeta, en gris con el fondo en una capa y sin la viñeta;
  - el fondo en una capa y el cuarto, en gris sin el cielo a media resolución;
  - los del cielo, en gris con las nebulosas en Bajo;
  - y vuelven solos;
  - lo que dicen cabe con la letra ancha.
- **Siguen pasando:** `ventanaf3` (la hoja Performance con cuatro interruptores cabe y no pisa nada),
  `efectoslayout`, `ajustesfluidez` y `banco`.
- **Tanda completa (`pruebas/todo.sh`):** los mismos problemas de siempre, fallo por fallo; ninguno nuevo.
- **Dibujo por CPU:** los 14 métodos, idénticos.

**No probado aquí:** una tarjeta de verdad, cómo se ve el cielo a un cuarto en la pantalla del bar, cuánto se gana
allí y Windows XP.
