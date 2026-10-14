# Más rendimiento para los gráficos, segunda vuelta: propuestas — Modest, 14 de octubre de 2026

Petición del usuario, después de G1 y G2: «Busca más mejoras de este tipo».

**Hechas J1 y J2** el mismo día («Haz J1 y J2»):
[J1_J2_Y_GRISES_PERFORMANCE_MODEST_2026-10-14.md](J1_J2_Y_GRISES_PERFORMANCE_MODEST_2026-10-14.md). J3 no se hizo.

Eran propuestas. Todo es solo con tarjeta gráfica; el dibujo por CPU sigue
congelado.

## Dónde está ahora el trabajo de la tarjeta

Medido con la tarjeta imitada en 1024×768, con Performance, G1 y G2: el fondo pinta **1,61 pantallas por cuadro**. Se
reparten así (cuenta a partir de esa medida):

| Pieza del fondo | Pantallas por cuadro |
| --- | --- |
| El estirado del cielo (cada cuadro) | 1,00 |
| Volver a pintar el cielo a la mitad (24 de cada 60 cuadros; cuando toca, 1,37) | 0,55 de media |
| Estrellas, luna y fugaces | 0,06 |
| La aurora y su reflejo (con música; no estaba en esa medida) | ≈ 0,34 |

**Lo de delante** (portadas, barras, halos) sale de la geometría de la pared, no está medido. En una pared de 6 × 3,
las portadas pintan unas 0,7 pantallas y el resto unas 0,4: casi todo necesario.

**Lo que miré y está bien:**
- **Las carátulas** se suben a 256 píxeles y se dibujan a unos 150. No sobra tamaño; la tarjeta no lee de más.
- **El panel de reposo** está pintado entero, sin huecos transparentes que saltar.

**El cuadro que más pesa** es el que vuelve a pintar el cielo: 1,37 pantallas de golpe. Al pasar de página pasa en
todos los cuadros, porque el empujón mueve las nebulosas, y es justo cuando un tirón se nota más.

## Las propuestas

### J1. El estirado del cielo no se pinta debajo de las portadas (automática; se ve igual)

- **Qué hace.** El estirado opaco del cielo salta el interior de las portadas opacas, con los mismos huecos que ya usan
  las otras capas (P2, del 13 de octubre). Donde hay hueco, la portada tapa todo con alfa 255.
- **Por qué no lleva interruptor.** Se ve igual, píxel por píxel.
- **Ganancia:** en la pared, ≈ 0,18 pantallas por cuadro a 1024×768 (el 18 % de la pantalla que tapan las portadas):
  de 1,61 a ≈ 1,43. Fuera de la pared no cambia nada.

### J2. El cielo a un cuarto de resolución (interruptor nuevo, en el preset Performance)

- **Qué hace.** La imagen del cielo (degradado, nebulosas, vía láctea, grano y viñeta) se pinta a un cuarto de ancho y
  de alto, no a la mitad, y se estira igual que ahora.
- **Por qué casi no se nota.** Las nebulosas y la vía láctea ya vienen de imágenes de un cuarto de la pantalla, así
  que no pierden detalle.
- **Lo que cambia a la vista (ligero):**
  - las nebulosas, un poco más suaves;
  - el grano en granitos de 4×4 (mueve 2 niveles de 255);
  - la viñeta y el degradado, igual.
- **Ganancia:**
  - volver a pintar el cielo cuesta 0,34 pantallas en vez de 1,37;
  - de media, de 1,61 a ≈ 1,2 pantallas por cuadro;
  - **el cuadro que más pesa, de ≈ 2,4 a ≈ 1,4** (-40 %), y eso es justo al pasar de página.
- **Necesita** «Cielo a media resolución», igual que G2. Si no hay imagen intermedia, se dibuja como siempre.

### J3. La aurora a media resolución (interruptor nuevo; con condiciones)

- **Qué hace.** La aurora se pinta en su propia imagen a la mitad y se pega encima del cielo.
- **Ganancia:** de ≈ 0,34 a ≈ 0,12 pantallas por cuadro con música.
- **Lo que cambia a la vista (ligero):** el borde de arriba de la aurora, un poco más suave.
- **Las condiciones:**
  - para pegarla bien hace falta que la tarjeta mezcle el color y la transparencia por separado; las muy viejas no
    pueden, y en esas se dibujaría como siempre;
  - es la más laboriosa de las tres;
  - se nota menos que J2.

### Juntas (cuenta, en 1024×768 en la pared)

| | Media por cuadro | El cuadro que más pesa |
| --- | --- | --- |
| Ahora (Performance + G1 + G2) | ≈ 1,6 + aurora 0,34 | ≈ 2,4 + 0,34 |
| + J1 | ≈ 1,4 + 0,34 | ≈ 2,2 + 0,34 |
| + J1 + J2 | ≈ 1,0 + 0,34 | ≈ 1,2 + 0,34 |
| + J1 + J2 + J3 | ≈ 1,0 + 0,12 | ≈ 1,2 + 0,12 |

## Lo que miré y no propongo

- **Carátulas de 16 bits o comprimidas.** Ocuparían menos y subirían antes, pero las fotos se verían en escalones, o
  comprimir costaría procesador justo en los equipos de dos núcleos.
- **Mover el cielo guardado en vez de volver a pintarlo al pasar de página.** El empujón mueve las nebulosas y la vía
  láctea a ritmos distintos, y la viñeta no se mueve: no sale igual.
- **Halos sin pintar debajo de la portada elegida.** Ahorraría unas 0,03 pantallas: no se notaría.
- **30 cuadros en reposo (G3).** Ya se dijo que no.

## Para saber qué frena de verdad en el bar

En Ctrl+F3 → Imagen → «Medir este equipo», la tabla dice qué frena: la tarjeta, el procesador o la música y los
mensajes. Una foto de esa tabla diría si lo siguiente que hay que aligerar es la tarjeta (estas propuestas) o el
procesador.
