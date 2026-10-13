# El repaso sin basura y el cielo sin pintar bajo las portadas — Modest, 13 de octubre de 2026

Petición del usuario: «Las 2», sobre las dos propuestas de
[LO_QUE_QUEDA_MODEST_2026-10-13.md](LO_QUE_QUEDA_MODEST_2026-10-13.md).

Las dos son optimizaciones que no cambian ni lo que suena ni lo que se ve. Por eso son automáticas y sin interruptor,
como pide AGENTS.md. El dibujo por CPU no se toca.

## 1. Los MP3 del repaso, sin basura (`Mp3Ligero.cs`)

**El problema.** El repaso lee cada MP3 con el lector de NAudio. Por cada cuadro del archivo (unos 38 por segundo de
canción) ese lector crea tres objetos nuevos y los tira enseguida. Al leer una canción entera son entre 6 y 10 MB de
basura, y obligan al recolector de memoria a pasar a menudo mientras dura el repaso.

**Qué se cambió.** El repaso abre ahora un `LectorMp3Ligero`:

- Es el mismo lector de NAudio. NAudio lo abre igual que siempre: las cabeceras, la tabla de cuadros, el formato y el
  descompresor.
- Solo cambia cómo lee cada cuadro:
  - la cabecera la valida la propia función de NAudio;
  - los cuadros se reutilizan por turnos en cuatro objetos, y los datos de cada uno van en un arreglo de su tamaño
    exacto.
- Son cuatro porque el descompresor en coma flotante recuerda los tres cuadros anteriores. El que se está leyendo
  nunca pisa a esos tres.
- La cuenta de lectura, de los saltos y de los cuadros dobles es la de NAudio, paso a paso.
- Si NAudio no fuera la 1.9 que se espera, el repaso usa el lector de siempre.
- La reproducción no cambia: sigue con el lector de NAudio tal cual.

**Medido aquí**, con un descompresor de imitación (en Linux no está el de Windows):

| Canción de prueba | NAudio | Ligero |
| --- | --- | --- |
| 215 s a 320 kbps, leerla entera | 9,6 MB de basura, 5,1 ms | 26 KB, 4,3 ms |
| 199 s (la de la batería), leerla entera | 3,9 MB | 2 KB |

Al abrir cada MP3, NAudio sigue reservando su tabla de cuadros: 1,3 a 1,4 MB por canción. Esa parte es de NAudio y no
se tocó.

**Comprobado en la batería nueva `mp3ligero`** (CPU y GPU, 50 comprobaciones). Se compara con NAudio usando un
descompresor que apunta todo lo que recibe: cada cuadro byte a byte, su posición, su tamaño y cada reinicio. Los dos
lectores tienen que:

- darle al descompresor exactamente lo mismo, en el mismo orden;
- soltar los mismos bytes, leyendo en trozos de tamaños raros y con saltos (al principio, a la mitad, al 90 % y más
  allá del final);
- marcar la misma posición después de cada paso.

Archivos probados:

- los MP3 de verdad de `pruebas/`: mono, 48 kHz, MPEG-2, VBR y sin etiqueta;
- con una carátula ID3v2 de 70 KB delante y con ID3v1 al final;
- con basura entre cuadros (la búsqueda byte a byte de la sincronía);
- cortado a mitad de un cuadro, y tres veces seguido.

También se mira que los tres cuadros anteriores sigan intactos mientras se lee el siguiente.

Para saber que la prueba sirve, estropeé el lector tres veces a propósito (un solo turno, el salto con 2 cuadros en
vez de 3, el cuadro doble sin recortar). Las tres veces falla.

**Lo que no se pudo hacer aquí:** medir el repaso completo con MP3 (necesita el descompresor de Windows). Tampoco se
puede prometer que quite tirones: que coincidieran con las recolecciones no prueba que las causen.

## 2. El cielo no se pinta debajo de las portadas (`DibujoLigeroGpu.cs`, solo tarjeta)

**El problema.** En la pared, debajo de cada portada la tarjeta pintaba antes, para nada, las capas del cielo que van
mezcladas: las dos pasadas de la nebulosa, la vía láctea, el grano y la viñeta. Donde la portada es opaca, la tarjeta
la pinta encima con alfa 255 y lo de debajo no se ve.

**Qué se cambió.** Antes de abrir cada cuadro se sacan los huecos: el interior de cada portada opaca de verdad. Una
portada deja hueco solo si cumple todo esto:

- se pinta entera: ni fundiéndose, ni al hojear a media opacidad, ni con la pared fundida;
- su textura ya está subida;
- no tiene transparencia por dentro. Se mira una vez, al subirla, sin contar los dos texeles del borde que el filtro
  deja a medias.

El hueco es solo su interior, 3 texeles y un píxel más adentro que el borde suave. Las texturas no tienen mipmaps y el
filtrado lee como mucho un texel alrededor, así que dentro del hueco la portada sale opaca en cada píxel. Las cuatro
capas se pintan en trozos alrededor de los huecos, cada trozo con las coordenadas de textura que le tocaban.

No se tocan:

- el degradado del cielo: es opaco, lo más barato de pintar, y tapa lo del cuadro anterior;
- la aurora, las estrellas y la luna.

**Cuánto ahorra.** En una pared de 6 × 3 portadas, cada capa deja de pintar más o menos:

- un 18 % de la pantalla a 1024 × 768;
- un 14 % a 1366 × 768;
- un 12 % a 800 × 600.

Son cerca de 0,8 pantallas por cuadro de las casi seis que pinta el fondo. Es una cuenta con la geometría de la pared,
no una medida en una tarjeta: aquí no hay Direct3D.

**Comprobado en `dibujoligero`**, sección 9, con su pintor de referencia (las reglas de Direct3D 9):

- **Qué portada es opaca:** con el borde a medias de dos texeles, sí; con tres, o con un solo píxel de alfa 254
  dentro, no.
- **Cuándo deja hueco una casilla:** de seis portadas, dejan hueco las cuatro opacas y enteras; no la que se está
  fundiendo ni la que tiene un píxel transparente. La que asoma por el borde queda recortada a la pantalla. Con la pared
  fundiéndose o fuera de la pared, ningún hueco.
- **Las capas, enteras y con huecos**, a 1024 × 768 y 1366 × 768, con huecos de una pared, fuera de la pantalla,
  solapados, pegados y por toda la pantalla:
  - dentro de los huecos no se pinta nada;
  - ningún píxel se pinta dos veces;
  - fuera de los huecos, como mucho 1 nivel de 255 en menos del 0,1 % de los píxeles, en el borde de los huecos. Es el
    mismo margen que ya se aceptaba para las capas estiradas a trozos. La viñeta es la que más, porque su imagen de 128
    texeles se estira ocho veces.
- **Sin huecos**, las capas se mandan exactamente como antes.
- **La prueba que importa:** la pared de verdad (`DrawNativeCover`) encima del cielo con huecos da la misma pantalla
  que encima del cielo entero: 3 píxeles de 786.432 difieren en 1 nivel de 255.

**Lo que hay que mirar en el bar:** que la pared se vea igual. La regla es la de Direct3D 9, pero cada tarjeta redondea
a su manera en el último bit.

## Cómo se comprobó

- **Baterías:** `mp3ligero` (nueva) y `dibujoligero` (ampliada).
- **Tanda completa (`pruebas/todo.sh`):** los mismos 26 problemas de siempre, ninguno nuevo. `mp3ligero` y
  `dibujoligero` pasan en los dos perfiles.
- **Dibujo por CPU:** sin tocar (los 14 métodos, idénticos instrucción por instrucción).
