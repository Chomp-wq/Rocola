# El fondo Legacy, más ligero sin cambiar el aspecto — Modest, 12 de octubre de 2026

Petición del usuario: «Revisa todos los elementos del fondo y busca optimizaciones de rendimiento sin afectar al
aspecto».

Solo con tarjeta gráfica. Son automáticas y sin interruptor, como pide AGENTS.md para lo que no cambia el aspecto.
El dibujo por CPU no cambia: sus 14 métodos congelados son idénticos.

## Cómo se midió

Aquí no hay Direct3D, así que hice un **simulador del fondo**:

- es la rocola de tarjeta de verdad, con su campo de estrellas, sus imágenes horneadas, su aurora y su luna;
- en lugar de la tarjeta lleva una «de mentira» que apunta todo lo que se le manda;
- de eso salen dos medidas:
  - **el procesador** que gasta cada capa por cuadro;
  - **lo que la tarjeta tiene que pintar**: el área de todos los triángulos dentro de la pantalla, contada en
    pantallas.

En una tarjeta modesta, lo segundo es lo que de verdad cuesta. **No es una medida en el equipo del bar.**

## Lo que hay en el fondo (1024×768, de fábrica)

| Capa | Pinta por cuadro | Procesador por cuadro (aquí) |
| --- | --- | --- |
| Cielo en degradado (opaco, la base) | 1,00 pantallas | casi nada |
| Nebulosas (dos pasadas: color del género y de la carátula) + vía láctea | 2,61 pantallas | 0,003 ms |
| Aurora del horizonte (relleno + brillo) | 0,32 pantallas | 0,09 ms (0,2 ms a ritmo real) |
| Reflejo de la aurora | 0,02 pantallas | 0,008 ms |
| Grano (quita las franjas del degradado) | 1,00 pantallas | casi nada |
| Estrellas (unas 460) y fugaces | 0,06 pantallas | 0,03 ms |
| Luna y cometa | 0,05 pantallas | 0,012 ms |
| Viñeta (oscurece los bordes) | 0,85 pantallas | 0,009 ms |
| **Total** | **5,89 pantallas** | **0,14 ms** |

**Ya venía muy afinado**, de rondas anteriores:

- el titileo de las estrellas va por tabla y no por seno;
- el aire de cada altura está precalculado;
- las estrellas invisibles del cruce de patrones no se mandan;
- los halos van en octágono;
- el cielo no se borra antes de pintarse;
- la vía láctea y la viñeta no pintan lo transparente;
- los colores de la aurora se guardan entre canción y canción;
- con el video a pantalla completa, el panel se apaga.

## Lo que se cambió

### 1. Las nebulosas sin detalle natural, sin pintar lo transparente

Las dos pasadas de la nebulosa se pintaban a pantalla completa. Sin el detalle natural (Ctrl+F3 → Fondo), **la mitad
de su imagen es transparente del todo**. Ahora se pinta solo lo que tiene algo, con el mismo mecanismo que ya usaban
la vía láctea y la viñeta.

- Sin detalle natural: **de 5,89 a 4,84 pantallas por cuadro (-18 %).**
- Con el detalle natural, que es lo de fábrica: casi toda la imagen tiene algo (el 98 %). Partirla no ahorraría nada
  y dejaría el redondeo del borde de los trozos. Así que va entera, **exactamente como siempre**. La regla: solo se
  usan trozos si con ellos se pinta menos del 90 %.

### 2. Trozos más finos en la vía láctea y la viñeta

Los trozos que se saltan pasan de 4 a 2 texeles (en pantalla, de 16 a 8 píxeles en la vía láctea y de 32 a 16 en
la viñeta). Se ajustan mejor al borde de lo transparente:

- la vía láctea pinta el 55 % de su imagen en lugar del 60 %;
- la viñeta, el 83 % en lugar del 85 %.

Son unas 0,05 pantallas menos por cuadro, a cambio de un centenar de triángulos más, que no pesan.

### 3. La aurora no rehace su malla si no cambió nada

La malla de la aurora (unos 8.600 vértices) se rehacía en cada cuadro. Pero solo depende de cosas que cambian:

- cuando llega una muestra nueva de la onda;
- si se mueve el horizonte;
- si cambia el tamaño de la pantalla.

Ahora, en los cuadros en que nada de eso cambió, se manda la que ya estaba. Ayuda:

- **con las ondas a 24 cuadros por segundo** (Ctrl+F3), de 0,142 a 0,109 ms por cuadro (-23 %);
- **con pantallas de más de 60 Hz**: a 75 Hz, uno de cada cinco cuadros.

Con las ondas a 60 y una pantalla de 60 Hz (lo de fábrica) casi todos los cuadros traen muestra nueva y la ganancia
es poca.

## Cómo se comprobó que no cambia el aspecto

- **Batería `dibujoligero`**, ampliada. Tiene un pintor por software con las reglas de Direct3D 9: bordes de los
  triángulos, mezcla de 8 bits y lectura bilineal de la textura.
  - **Nebulosas sin detalle natural:** en dos resoluciones y con la deriva en cinco posiciones, se pinta la capa
    entera y la capa a trozos sobre el mismo fondo. Salen las mismas llamadas, con la misma textura y la misma
    mezcla, y la misma imagen. Cambian como mucho 3 píxeles de un millón, por 1 nivel de 255: el redondeo en el
    borde de los trozos. Es lo mismo que ya pasaba con la vía láctea antes de este cambio.
  - **Con detalle natural** se comprueba que va entera: exactamente igual que antes. Partida a la fuerza, dejaría
    unos 25 píxeles de un millón por 1 nivel, y por eso no se parte.
  - **Vía láctea y viñeta con trozos de 2:** cumplen la regla de qué trozo se salta. El redondeo queda igual que con
    los de 4 (0 a 2 píxeles de cada millón, por 1 nivel).
  - **Aurora:**
    - la malla reusada es byte a byte la misma que rehecha;
    - se reusa de verdad: con las alturas tocadas a mano, sale la de antes;
    - con el horizonte movido o con una muestra nueva de la onda, se rehace.
- **Siguen pasando:**
  - `fondocoste`, `barrido`, `fondo`, `neon`, `aurorafina`, `cielovivo`;
  - `campoestelarbuffers`, `ruidomalla`, `nebfuerza`, `ecosgeometria`;
  - `estrellas`, `fugaces`, `luna`, `cielo`, `hojasfondo`.
- **Tanda completa (`pruebas/todo.sh`):** 26 problemas, los mismos 26 de siempre (baterías que ya fallaban en este
  Linux); ninguno nuevo. `dibujoligero` y `fondocoste` pasan.
- **Dibujo por CPU:** los 14 métodos congelados, idénticos.

## Lo que queda y por qué no lo hice

- **No pintar el fondo debajo de las portadas.** Es lo que más ahorraría. En la pared, las portadas tapan buena parte
  de la pantalla (a ojo, cerca de la mitad; no lo medí), y debajo de cada una se pintan antes las casi seis capas del
  fondo para nada. Pero cada
  portada lleva su cara, sus letreros y unos bordes con partes transparentes, y las portadas se mueven al pasar de
  página y al enfocarlas. Recortar el fondo justo donde la portada es opaca sin que se note nada pide comprobarlo
  en la tarjeta del bar, y aquí no se puede. Si quieres, lo preparo para probarlo allí.
- **Juntar capas** (el grano con el cielo, las dos pasadas de la nebulosa en una) cambiaría el resultado en la
  mezcla de colores. Sería otro aspecto, aunque parecido, así que no entra en lo que pediste.
- Si un equipo no da, Ctrl+F3 ya tiene las perillas del cielo: nebulosas (sin el velo o sin nada), densidad de
  estrellas, fugaces y cada cuánto cambia el patrón. Esas sí cambian el aspecto, y por eso las elige el encargado.
