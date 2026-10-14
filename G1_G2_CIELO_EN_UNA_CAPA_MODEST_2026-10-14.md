# G1 y G2: el cielo sin repetir y el fondo en una sola capa — Modest, 14 de octubre de 2026

Petición del usuario: «Me interesa G1 y G2» (de
[PERFORMANCE_PROPUESTAS_MODEST_2026-10-14.md](PERFORMANCE_PROPUESTAS_MODEST_2026-10-14.md)). G3 (30 cuadros en
reposo) no se hizo.

Todo es solo con tarjeta gráfica (`PerformanceGpu.cs`). El dibujo por CPU no cambia: sus 14 métodos son idénticos.

## Lo que se gana

Medido con la tarjeta imitada, en 1024×768: 30 cuadros a 60 por segundo, con las nebulosas moviéndose 24 veces por
segundo, como en la rocola. Sin portadas delante ni aurora, que sería igual en los cuatro casos.

| Fondo | Pantallas pintadas por cuadro |
| --- | --- |
| De fábrica (Alto) | 5,98 |
| Performance de antes | 3,05 |
| Performance con G1 | **2,42** (-21 %) |
| Performance con G1 y G2 | **1,61** (-47 %; un 27 % de lo de fábrica) |

En una tarjeta modesta, pintar pantallas es lo que de verdad cuesta. Esto no es una medida en el equipo del bar:
allí lo dice «Medir este equipo».

## G1. El cielo no se repite si no cambió (automático, sin interruptor)

- **Lo que pasaba.** Las nebulosas y la vía láctea se mueven con el reloj de la animación: 24 veces por segundo. Pero
  la imagen del cielo a media resolución se pintaba 60 veces por segundo, así que en 36 de cada 60 cuadros salía lo
  mismo que en el anterior.
- **Ahora.** Se apunta con qué se pintó: los colores del cielo y de las nebulosas, su paso lento, el empujón al pasar de
  página, sus imágenes y el tamaño de la pantalla. Solo se vuelve a pintar si algo de eso cambió, o si la imagen se
  rehízo después de un reinicio del dispositivo (al bloquear la sesión o cambiar la resolución). Si no, se estira la que
  ya estaba.
- **Se ve igual, píxel por píxel.** Por eso va siempre activo y sin interruptor (AGENTS.md).
- **Cuándo actúa.** Solo con «Cielo a media resolución» encendido. A resolución completa, el cuadro que sí cambia
  costaría una pantalla más que hoy, y eso empeora justo los peores cuadros.
- **En Ctrl+F3** → Imagen → Performance, el texto de «Siempre activo» lo dice.

## G2. El fondo en una sola capa (interruptor nuevo)

**Ctrl+F3 → Imagen → tarjeta EFECTOS → hoja Performance → «Fondo en una sola capa».** Viene apagado de fábrica, se
guarda al momento (`fondo_capa=`) y lo enciende el botón **Performance** de «Todo de golpe»; también Bajo y Medio, como
los otros ahorros. «Gráficos como de fábrica» lo apaga.

- **Qué hace.** El grano y la viñeta se pintan dentro de la imagen del cielo a media resolución. Así se pintan a un
  cuarto de los píxeles y solo cuando el cielo cambia (G1). En cada cuadro queda un solo estirado opaco, más la aurora,
  las estrellas y la luna.
- **Necesita «Cielo a media resolución».** Sin él va en gris y lo dice: «Necesita: Cielo a media resolución
  (arriba)».
- **Lo que cambia a la vista (ligero):**
  - **El grano sale en granitos de 2×2 píxeles**, en vez de uno. Mueve 1 o 2 niveles de 255, arriba o abajo, así que en
    la práctica no se distingue.
  - **La viñeta, punto por punto.** Ahora la viñeta queda debajo de la aurora, las estrellas, la luna y las fugaces.
    Para que en los bordes se sigan oscureciendo como antes, a cada punto se le aplica la viñeta de su sitio. Una
    estrella sale como con la viñeta encima, con 1 nivel de diferencia como mucho. En lo grande (la aurora, la estrella
    protagonista) se aplica en cada vértice y la tarjeta la degrada entre uno y otro: casi igual.
  - **La aurora ya no lleva grano encima.** Si en algún equipo se ven franjas en su degradado, se nota ahí.
- **Comparado con el Performance de antes**, el fondo entero cambia como mucho 4 niveles de 255, de media 0,95, y el
  brillo medio se mueve +0,06 niveles.
- **Lo que gasta el procesador.** La viñeta punto por punto pasa por unos 12.000 vértices en cada cuadro. Aquí cuesta
  entre 0,15 y 0,2 ms por cuadro con una tabla de enteros (con decimales eran 0,6). En el equipo del bar será algo más;
  no se midió allí.

### El medidor (Imagen → «Medir este equipo»)

- Prueba los tres ahorros. El de una capa lo mide **encima de los otros dos**: lo que ahorra de más.
- Si lo propone, propone también el cielo a media resolución, porque va dentro de él.
- La propuesta lo nombra («fondo en una sola capa»), y «Solo Performance» enciende los tres.

## Cómo se comprobó

- **`performance`**: 68 comprobaciones con tarjeta y 12 sin ella. Usa la tarjeta imitada y el pintor de referencia de
  Direct3D 9. Lo nuevo:
  - **G1:**
    - la segunda vez sin cambios solo se estira, con la misma imagen píxel por píxel;
    - se vuelve a pintar si cambian el paso de las nebulosas, su color, el cielo o sus imágenes, y tras un reinicio;
    - en un segundo a 60 cuadros, 24 se pintan y 36 se reúsan;
    - el cielo pinta el 69 % de antes.
  - **G2:**
    - la viñeta punto por punto en el centro, el borde y las cuatro esquinas: como mucho 1 nivel de diferencia;
    - el cuadro entero va en una capa;
    - un cuadro con el cielo reusado pinta el 50 % de lo de Performance con G1;
    - casi igual que el Performance de antes (lo de arriba);
    - sin el cielo a media resolución, el cuadro de siempre;
    - la tabla de 1024×768 de arriba.
  - **El preset y el guardado:** el preset enciende y apaga los tres; `fondo_capa=` se guarda y se relee; la propuesta
    nunca pone el fondo en una capa sin el cielo a media resolución.
  - **El cuadro entero de antes** se ajustó: la imagen intermedia se pinta en el primer cuadro y se reúsa en los demás.
- **`ajustesvivos`:** «Fondo en una sola capa» va en gris sin el cielo a media resolución.
- **Siguen pasando:** `ventanaf3` (la hoja Performance con tres interruptores cabe y no pisa nada), `dibujoligero`,
  `banco` y `efectoslayout`.
- **Tanda completa (`pruebas/todo.sh`):** los mismos problemas de siempre, fallo por fallo; ninguno nuevo.
- **Dibujo por CPU:** los 14 métodos, idénticos.

**No probado aquí:**

- **Una tarjeta de verdad.** Que el controlador guarde lo pintado en la imagen intermedia entre cuadros (Direct3D 9 lo
  garantiza hasta un reinicio, y ahí se vuelve a pintar).
- **Cómo se ve en el bar:** el grano de 2×2 y las puntas de la aurora.
- **Cuánto se gana en el equipo del bar.**
- **Windows XP.**
