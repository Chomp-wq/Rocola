# Más rendimiento para los gráficos: propuestas — Modest, 14 de octubre de 2026

Petición del usuario: «Busca más optimizaciones del tipo performance para los gráficos de la rocola; no importa si se
ven ligeramente diferentes si dan mucho rendimiento».

Son propuestas: **no se ha cambiado nada todavía**. Todo es solo con tarjeta gráfica; el dibujo por CPU sigue
congelado.

## De dónde sale cada número

- **La unidad.** Lo que cuesta en una tarjeta modesta es cuántas veces se pinta la pantalla en cada cuadro
  («pantallas por cuadro»).
- **De dónde salen.** Las cuentas parten de lo que se midió con la tarjeta imitada el 12 y el 13 de octubre: 1024×768,
  cielo de fábrica, sin portadas delante (FONDO_LIGERO y PERFORMANCE).
- **Lo que no son.** Son cuentas, **no medidas en el equipo del bar**; allí lo diría «Medir este equipo».

| Capa del fondo | Pantallas por cuadro |
| --- | --- |
| Cielo en degradado | 1,00 |
| Nebulosas (dos pasadas) y vía láctea | 2,61 |
| Grano | 1,00 |
| Viñeta | 0,85 |
| Aurora y su reflejo | 0,34 |
| Estrellas, fugaces y luna | 0,10 |
| **De fábrica (Alto)** | **≈ 5,9** |
| **Con el preset Performance de hoy** | **≈ 3,2** |

**Lo que se descubrió al buscar:** las nebulosas y la vía láctea **solo cambian 24 veces por segundo**. Las mueve el
reloj de la animación, que va cada 42 ms. Pero se vuelven a pintar en cada cuadro, 60 veces por segundo, así que en 36
de cada 60 cuadros se pinta exactamente lo mismo que en el anterior.

## Las propuestas

### G1. No repetir el cielo cuando no cambió (automática; se ve igual)

- **Qué hace.** Con «Cielo a media resolución» encendido, la imagen intermedia del cielo y las nebulosas solo se vuelve
  a pintar cuando algo suyo cambió: los colores, el paso lento de las nebulosas, el empujón al pasar de página o el
  tamaño. En los demás cuadros se estira la que ya estaba.
- **Por qué no lleva interruptor.** Se ve **igual, píxel por píxel**: es la misma imagen. AGENTS.md pide que esto vaya
  siempre activo y sin interruptor.
- **Ganancia:** de ≈ 3,2 a **≈ 2,8 pantallas** por cuadro de media (-12 %). El cuadro más caro queda como hoy, nunca
  peor.
- **Por qué no a resolución completa.** A resolución completa, el cuadro en que sí cambia costaría una pantalla más que
  hoy. Eso empeora justo los peores cuadros, que son los que se notan como tirón.

### G2. El fondo en una sola capa (interruptor nuevo, dentro del preset Performance)

- **Qué hace.** El grano y la viñeta pasan a pintarse **dentro** de la imagen intermedia del cielo. Se pintan solo
  cuando el cielo cambia (24 veces por segundo como mucho) y a un cuarto de los píxeles.
- **Lo que queda en cada cuadro:** un solo estirado opaco de toda la pantalla, más la aurora, las estrellas y la luna.
- **Ganancia:**
  - de ≈ 3,2 a **≈ 2,0 pantallas** por cuadro de media: un 38 % menos que el Performance de hoy y un tercio de lo de
    fábrica;
  - el cuadro más caro, de 3,2 a ≈ 2,8.
- **Lo que cambia a la vista (ligero):**
  - **El grano sale en granitos de 2×2 píxeles**, en vez de 1×1. Mueve 2 niveles de 255 como mucho, así que en la
    práctica no se distingue.
  - **La viñeta quedaría debajo de las estrellas**, que hoy oscurece hasta un 43 % en las esquinas. Para que no se
    note, a las estrellas, las fugaces y la luna se les aplica la misma viñeta con una cuenta por estrella, que es
    barata.
  - **Lo que sí se notaría un poco:** las dos puntas de la aurora, abajo en las esquinas, quedarían algo más claras que
    hoy.
- **Necesita** «Cielo a media resolución». Si la tarjeta no puede pintar en una imagen aparte, se dibuja como siempre.

### G3. En reposo, 30 cuadros por segundo (interruptor nuevo, en Fluidez y en el preset Performance)

- **Cuándo entra:** si nadie toca nada en 15 segundos y no hay nada en marcha (buscador cerrado, ayuda cerrada, sin
  vuelos de portadas ni avisos).
- **Qué hace.** La pantalla se dibuja a 30 cuadros por segundo en vez de 60. Con una pantalla de 60 Hz, 30 queda
  parejo: un cuadro cada dos refrescos, sin saltos.
- **Al tocar cualquier botón**, vuelve a 60 en ese mismo cuadro.
- **Ganancia:** **la mitad del trabajo de la tarjeta y del procesador para dibujar**, justo el rato en que más tiempo
  pasa la rocola: con música sonando y nadie delante, y en el modo atracción. En un equipo de dos núcleos, ese medio
  núcleo queda para el sonido y el repaso de la biblioteca.
- **Lo que cambia a la vista (ligero).** En reposo, la deriva de las estrellas y las ondas de la aurora se mueven a 30
  cuadros. Se nota solo si alguien se queda mirando fijo el cielo. En cuanto alguien toca un botón, todo vuelve a 60.

### Juntas

| | Pantallas por cuadro | Procesador para dibujar |
| --- | --- | --- |
| De fábrica | ≈ 5,9 | 100 % |
| Performance de hoy | ≈ 3,2 | 100 % |
| Performance + G1 + G2 | **≈ 2,0** | algo menos (menos órdenes) |
| … y en reposo (G3) | **≈ 2,0, pero la mitad de veces por segundo** | **≈ 50 %** |

## Lo que miré y no propongo

- **Pintar toda la pantalla a menos resolución** (por ejemplo al 75 %) y estirarla. Ganaría mucho en pantallas
  grandes, pero las letras y las portadas se verían borrosas, que no es «ligeramente diferente». Además habría que tocar
  cada dibujo de la rocola: mucho riesgo.
- **Texturas de 16 bits** para portadas y cielo. Ahorrarían memoria de la tarjeta, pero el degradado del cielo se vería
  en escalones. Además, convertir cada portada costaría procesador justo en los equipos de dos núcleos.
- **El cielo guardado a resolución completa.** Lo explicado en G1: empeora los peores cuadros.
- **Aurora con menos puntos.** Ya se rehace solo cuando cambia y gasta unas décimas de milisegundo: lo que se ahorraría
  no se notaría.
- **Estrellas y fugaces.** Pintan 0,1 pantallas por cuadro y su cuenta ya va por tablas: no queda nada que rascar.

## Si se hacen

- **Cómo se probaría:**
  - con la tarjeta imitada y el pintor de referencia (como `performance` y `dibujoligero`): que G1 sale píxel por píxel
    igual, y cuánto cambia G2 (grano, viñeta y esquinas de la aurora);
  - G3, con el reloj a mano: cuándo entra, que sale al primer botón y que el ritmo queda parejo;
  - la tanda completa, y el dibujo por CPU idéntico.
- **En el bar**, «Medir este equipo» diría lo que se gana de verdad. Habría que mirar las esquinas de la aurora con G2 y
  el cielo en reposo con G3.
