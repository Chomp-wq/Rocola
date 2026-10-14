# Pulir los gráficos: propuestas — Modest, 14 de octubre de 2026

Petición del usuario: «Quiero pulir los gráficos de la rocola. Por ejemplo, me gustaría que mejoraras la profundidad de
colores y degradados, y los reflejos en la estrella animada, y mejoras de ese tipo que tú también me propongas».

Son propuestas: **no se ha cambiado nada todavía**.

**Reglas que siguen todas:**
- Solo con tarjeta gráfica; el dibujo por CPU sigue congelado.
- Cada una es una opción visual con su interruptor en Ctrl+F3 (AGENTS.md).
- La estrella sigue sin cara: solo cambian su metal, sus reflejos y su luz.
- Casi todas son cuentas en el procesador al armar los vértices, o se hornean una vez. A la tarjeta le cuestan poco;
  donde no, se dice.

## Cómo está hoy (lo que se encontró al mirar)

- **La estrella 3D** pinta cada una de sus 20 caras de **un solo color plano**:
  - el reflejo es un único número por cara;
  - el destello es un halo redondo encima.

  De cerca se ve como un objeto de facetas lisas, no como metal pulido.
- **El cielo** es un degradado de **tres colores en dos tramos rectos**. En la parada del medio se nota el quiebre.
- **Los colores se mezclan como números**, no como luz. Entre un azul y un violeta, o donde se cruzan las dos
  nebulosas, el tono intermedio sale más apagado y algo sucio. Es lo que resta «profundidad».
- **Los degradados de la interfaz** que pinta Windows (la barra de estado, el panel de reposo, las tarjetas) no llevan
  tramado. En algunas pantallas se ven escalones.

## Colores y degradados (lo que pediste)

| | Qué hace | Cómo se ve | Coste |
| --- | --- | --- | --- |
| **C1. Cielo de muchas paradas** | El degradado del cielo pasa de 2 tramos a unos 12, con una curva suave, calculado como luz | Sin el quiebre del medio; el paso del negro de arriba al color del género, más natural y más hondo | Nada (unos vértices más; con G1 se pinta 24 veces por segundo como mucho) |
| **C2. Colores con más profundidad** | Los colores del cielo, de las nebulosas y de la aurora se mezclan como luz. Más vida en los tonos medios, negros más hondos arriba con un punto de azul | Los cruces de colores salen limpios y vivos, no grisáceos; más contraste sin quemar nada | Nada (se calcula al cambiar de canción) |
| **C3. Aurora más limpia** | Sus rampas de color, como luz y con más franjas cerca del pie | Transiciones más suaves; el pie brilla sin manchas | Casi nada |
| **C4. Interfaz sin escalones** | Los degradados de la barra de estado, del panel de reposo y de las tarjetas se hornean con tramado | Sin franjas en los fondos oscuros | Solo al hornear, que ya es poco frecuente |

## La estrella: reflejos (lo que pediste)

| | Qué hace | Cómo se ve |
| --- | --- | --- |
| **E1. Facetas con degradado** | Cada cara recibe la luz en cada vértice, no una sola vez | El metal tiene volumen: cada faceta pasa de clara a oscura y no se ve como una pegatina plana |
| **E2. Refleja el cielo** | Cada faceta refleja lo que tiene delante según hacia dónde mira: arriba el negro azulado, abajo el color del género y de la aurora | Metal pulido que cambia de color con cada canción y con lo que pasa en el horizonte |
| **E3. Brillo nítido y destello en cruz** | El brillo de la luz se concentra y corre por las aristas al girar. Donde más refleja, un destello de cuatro puntas, no el halo redondo | Más «joya»: el destello se mueve con el giro |
| **E4. Borde de luz** | La silueta se enciende en el color de la canción, más cuanto más de canto se ve la cara | Se despega del cielo oscuro sin necesidad de un halo grande |
| **E5. Reflejo en el horizonte** | Una copia invertida de la estrella, tenue y desvanecida, sobre la raya del horizonte, como en el agua | Le da suelo a la escena. Cuesta un poco de tarjeta: lo que mide la estrella |

Todo esto vale también para su estela de copias, más apagado, como ahora.

## Otras que propongo

| | Qué hace | Cómo se ve |
| --- | --- | --- |
| **O1. Brillo de cristal en la portada elegida** | Un reflejo diagonal suave cruza la portada enfocada cada pocos segundos | La portada elegida «brilla» como detrás de un vidrio, sin tapar la carátula |
| **O2. Sombra suave bajo las portadas** | Una sombra difusa debajo de cada portada de la pared | La pared gana profundidad: las portadas flotan sobre el cielo. Cuesta algo de tarjeta: un borde alrededor de cada portada |
| **O3. Fugaces con cola de color** | La cola va del blanco al color de la canción, con un destello pequeño en la cabeza | Más vivas y a juego con la música |

## Lo que recomiendo

- **Primero lo que pediste:** C1, C2 y C3, y E1, E2, E3 y E4. Son las que más cambian la sensación de calidad y no
  cuestan rendimiento.
- **Después**, si te gustan: C4, O1 y O3.
- **E5 y O2** dan mucho efecto, pero cuestan algo de tarjeta. Las dejaría apagadas de fábrica en el preset
  Performance.

**Cómo se probaría:**
- con la tarjeta imitada y el pintor de referencia, con capturas antes y después de la estrella, del cielo y de la
  aurora;
- comprobando que nada sale de su rango;
- y la tanda completa.

**En el bar** habría que mirarlo con la pantalla de verdad.
