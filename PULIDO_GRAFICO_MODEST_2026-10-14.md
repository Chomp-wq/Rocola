# El pulido de los gráficos — Modest, 14 de octubre de 2026

Petición del usuario: «Haz todo menos O2 y O3» (de
[PULIDO_GRAFICO_PROPUESTAS_MODEST_2026-10-14.md](PULIDO_GRAFICO_PROPUESTAS_MODEST_2026-10-14.md)).

Hechas:
- **Colores y degradados:** C1, C2, C3 y C4.
- **La estrella:** E1, E2, E3, E4 y E5.
- **La portada elegida:** O1.

Todo es solo con tarjeta gráfica (`PulidoGpu.cs`). El dibujo por CPU no cambia: sus 14 métodos son idénticos. La
estrella sigue sin cara.

## Dónde está

**Ctrl+F3 → Fondo → hoja nueva «Pulido»** (la quinta, junto a «Cielo vivo»). Son diez interruptores en dos tarjetas,
todos **encendidos de fábrica**. Se guardan al momento y «Fondo como de fábrica» los vuelve a encender.

| Tarjeta | Interruptor | Clave en la configuración |
| --- | --- | --- |
| Colores y degradados | Cielo de muchas paradas (C1) | `cielo_suave=` |
| | Colores con más profundidad (C2) | `colores_profundos=` |
| | Aurora más limpia (C3) | `aurora_limpia=` |
| | Interfaz sin escalones (C4) | `interfaz_tramada=` |
| La estrella y la portada | Estrella con facetas en degradado (E1) | `estrella_degradado=` |
| | La estrella refleja el cielo (E2) | `estrella_refleja=` |
| | Destello nítido en cruz (E3) | `estrella_destello=` |
| | Borde de luz en la estrella (E4) | `estrella_borde=` |
| | Reflejo de la estrella en el horizonte (E5) | `estrella_espejo=` |
| | Brillo de cristal en la portada elegida (O1) | `portada_brillo=` |

- **Los de la estrella (E1 a E5) son de la estrella en 3D**, la de fábrica. Sin ella van en gris y dicen «Necesita:
  Estrella en 3D girando (hoja Estrella)».
- **E5 cuesta algo de tarjeta**, así que los botones Performance, Medio y Bajo de «Todo de golpe» (Imagen) lo apagan.

## Colores y degradados

- **C1. Cielo de muchas paradas.**
  - **Antes:** el degradado del cielo eran dos tramos rectos, y en la parada del medio se notaba el quiebre.
  - **Ahora:** doce franjas que siguen una curva suave por las tres paradas, calculada como luz.
  - **Comprobado:** la curva pasa por los tres colores (a 1 nivel de 255). En la parada media, la pendiente cambia
    1 nivel cada 20 filas; con las dos rectas eran 10. Ninguna fila salta más de 3 niveles, y las doce franjas tapan
    toda la pantalla sin huecos.
  - **Con «Cielo a media resolución»**, el cielo de la imagen intermedia también va en doce franjas.
- **C2. Colores con más profundidad.**
  - **Mezclas como luz.** Las mezclas que hace el procesador (el tinte azul y violeta de las nebulosas, la aurora) se
    hacen como luz, no como números. Azul y rojo a medias dan un violeta claro (188), no el 128 apagado de antes.
  - **Más vida en los tonos medios.** Los colores apagados ganan vida; los grises y los ya vivos no cambian, y nada se
    quema.
  - **Un negro más hondo arriba**, hacia azul noche.
- **C3. Aurora más limpia.**
  - Sus rampas de color (del pie a la punta, de grave a agudo y con los colores de la carátula) se mezclan como luz y
    con una curva suave en los extremos.
  - Se recalculan solo cuando cambian sus colores, como antes.
- **C4. Interfaz sin escalones.**
  - **Dónde:** el panel de reposo, los globos de la ayuda y las tarjetas de las piezas. Solo los paneles que se hornean
    para la tarjeta; el dibujo por CPU no se toca.
  - **Qué hace:** sus degradados se hornean con tramado. Cada píxel redondea con un umbral distinto, así que entre dos
    niveles no queda una franja recta.
  - **Comprobado:** cada fila sigue el degradado (0,33 niveles de media) y 192 de 300 filas reparten el paso entre
    píxeles.
  - **Cuándo se nota:** desde el próximo horneado; el panel de reposo se rehornea en el acto.

## La estrella

- **E1. Facetas en degradado.** La luz se calcula en cada vértice, con la normal de la cara inclinada hacia la del
  vértice. Cada faceta pasa de clara a oscura, como metal con volumen; antes cada cara era de un solo color.
- **E2. Refleja el cielo.** Cada punto refleja lo que tiene delante según hacia dónde mira:
  - hacia arriba, el cielo de arriba;
  - hacia el horizonte, el de en medio;
  - hacia abajo, el color del género.

  Refleja más cuanto más de canto se ve, como el metal pulido. Al cambiar de género, cambia el color de más de la mitad
  de los vértices.
- **E3. Destello nítido en cruz.**
  - El brillo de la luz es más concentrado y corre por las facetas al girar.
  - Donde más refleja aparece un destello de ocho puntas (cuatro largas) que gira despacio, en lugar del halo redondo.
- **E4. Borde de luz.** La silueta se enciende con el color de la canción, más cuanto más de canto. La franja del borde
  lo toma.
- **E5. Reflejo en el horizonte.**
  - Una copia invertida de la estrella bajo la raya del horizonte, como en el agua.
  - Como mucho con un tercio de su alfa, y se desvanece hacia abajo.
  - Solo cuando la estrella entera está por encima de la raya.
- **La estela de copias** toma el degradado de E1. Lo demás de la estela queda como estaba: su color de holograma.

## O1. Brillo de cristal en la portada elegida

- Cada 6,5 segundos, una franja diagonal de luz cruza la cara de delante de la portada elegida en 1,2 segundos, como
  un reflejo en un vidrio.
- Suma luz (no tapa la carátula), con un máximo de 70 de 255 en su centro y nada en sus bordes.
- Solo con la portada casi de frente: mientras gira o se bambolea, no aparece.

## Lo que cuesta

- **La estrella:** cuentas por vértice (20 caras), más 8 triángulos del destello y, con E5, la copia invertida.
- **El cielo de doce franjas:** 24 triángulos en vez de 4. Con G1 se pinta como mucho 24 veces por segundo.
- **C2 y C3:** se calculan al cambiar de color, no en cada cuadro.
- **C4:** solo al hornear.
- **O1:** unos triángulos más.
- **Lo que pinta la tarjeta** sube muy poco; lo más es E5, y por eso Performance lo apaga.
- **No está medido en el equipo del bar.**

## Cómo se comprobó

- **`performance`**, apartado 10 nuevo, con la tarjeta imitada y el pintor de referencia (110 comprobaciones con
  tarjeta, 12 sin ella). Comprueba:
  - todo lo de arriba;
  - que ningún vértice de la estrella sale negro;
  - el reflejo, entero bajo la raya y tenue, y sin reflejo si la estrella toca la raya;
  - la franja de O1, dentro de la cara de delante, como mucho 70 de 255, y sin brillo mientras gira;
  - que los diez interruptores se guardan y se releen;
  - que Performance apaga E5.
- **Capturas de la estrella** antes y después, con el pintor de referencia: sin pulir, cada cara de un azul plano;
  pulida, cada faceta con su degradado y el reflejo del color del género.
- **Pruebas ajustadas.** Las que esperaban el cielo de dos tramos aceptan ahora también el de doce (`dibujoligero` y
  `performance`); las comparaciones de la media resolución con el cielo de siempre lo apagan.
- **`ajustesvivos`:** los de la estrella, en gris sin la estrella 3D.
- **`ventanaf3`:** la hoja Pulido cabe y no pisa nada.
- **Siguen pasando:**
  - `hojasfondo`, `estrella3d`, `estrellaflujo`, `ecosgeometria` y `asistentevisual`;
  - `aurorafina`, `cielo`, `cielovivo`, `cielohondo`, `fondo`, `fondocoste`, `barrido`, `luna` y `neon`;
  - `panelreposo` y `reposo`.
- **Tanda completa (`pruebas/todo.sh`):** los mismos problemas de siempre, fallo por fallo; ninguno nuevo.
- **Dibujo por CPU:** los 14 métodos, idénticos.

**No probado aquí:**
- cómo se ve en la pantalla del bar (los colores, la estrella, el brillo de la portada);
- una tarjeta de verdad;
- cuánto cuesta en el equipo del bar;
- Windows XP.
