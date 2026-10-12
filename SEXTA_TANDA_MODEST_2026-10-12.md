# Sexta tanda: los textos del modo atracción — Modest, 12 de octubre de 2026

Petición del usuario: «Haz otra tanda buscando más fallos y revisa que los textos del modo atracción se vean bien, ya
que ayer salían cortados unos textos».

## Qué se encontró

Se horneó lo mismo que dibuja la tarjeta en nueve resoluciones, de 640×480 a 1920×1080, con nombres reales largos:

- el escaparate: «EN ESTA ROCOLA», el nombre del disco, el artista y las pastillas;
- los mensajes que se turnan debajo;
- la lista de «Lo más pedido esta noche».

### 1. Los mensajes se cortaban al final (la causa más probable de lo de ayer)

Cuando un mensaje no cabía en su renglón, la letra se achicaba **a la medida justa**: el ancho de la caja entre lo que
medía el texto. Pero a otro tamaño las letras no miden exactamente lo proporcional, porque se ajustan a la rejilla de
píxeles. El renglón se pasaba por uno o dos píxeles y se cortaba el final:

> Toca un botón y elige entre 12,345 cancione
> Busca cualquier canción con el botón rojo y la flech

- **A 1024×768 les pasaba a los cuatro mensajes.** En las pantallas 4:3 y 5:4 (640×480, 800×600, 1024×768 y
  1280×1024) la columna del texto es angosta, y casi todos tienen que achicarse.
- **Ahora:** la letra se achica con un poco de margen y se **vuelve a medir con la letra chica**, hasta que cabe de
  verdad.

### 2. «Lo más pedido esta noche: «título largo»»

**Antes:** con un título largo el mensaje no cabía ni al 75 %, y la orilla lo cortaba por cualquier lado, sin cerrar
las comillas.

**Ahora:** se acorta el título por palabras, con los puntos dentro de las comillas:

> Lo más pedido esta noche: «Amor Eterno (En Vivo…»

### 3. El nombre largo del disco

**Antes:** se cortaba con puntos a los dos renglones («Los Tigres del Norte - Jefe de Je…»).

**Ahora**, antes de cortarlo:

- se prueba con la letra algo más chica (88 % y 78 %), que en la pantalla sigue siendo grande;
- si aun así no cabe y hay sitio arriba del mensaje, usa un tercer renglón.

### 4. La lista de la noche

**Antes:** el título tenía el 40 % del ancho y la barra, que es adorno, el resto. En pantallas 5:4 las filas son
altas y la letra grande, y «Amor Eterno (En Vivo Desde Bellas Artes)» se cortaba.

**Ahora:**

- **el título tiene la mitad del ancho;**
- la barra conserva al menos 0,8 de fila y nunca se mete debajo de «N veces»;
- la letra del título puede achicarse hasta el 62 %;
- si ni así cabe, se acorta **por palabras** con puntos suspensivos, en lugar de partir una letra o cortar a media
  palabra.

### 5. El título grande de la franja de lo que suena

Con la letra de lejos, el título crece hasta un 45 %. Se calculaba el tamaño multiplicando la medida, y en el borde
podía salir con puntos al final sin necesidad. Ahora cada tamaño se comprueba midiéndolo con la letra de verdad. Los
tamaños que ya cabían no cambian.

## Lo que no cambia

- Solo con tarjeta gráfica: el escaparate y la lista de la noche solo existen ahí. El dibujo por CPU no cambia.
- Todo esto se hornea una vez (al entrar a la atracción o al cambiar de tamaño), nunca en cada cuadro: no cuesta
  rendimiento.

## Cómo se comprobó

- **Prueba automática nueva `sextatanda`:** 124 comprobaciones con tarjeta gráfica, 1 sin ella. En las nueve
  resoluciones:
  - ningún mensaje llega a la orilla de su renglón;
  - la ficha cabe encima del mensaje;
  - «128 veces» cabe en su caja y la barra acaba antes;
  - el título largo del primer puesto cabe;
  - el acortado de la cita y por palabras;
  - la letra achicada se mide con la letra chica.

  Contra el ejecutable anterior falla.
- **Siguen igual:**
  - `ajustes`, `buscaportadas` y `carpetas` pasan;
  - `atraccion` y `estilo` fallan en lo mismo que antes de estos cambios: son de las 26 que ya fallaban en este Linux;
  - en `estilo`, la comprobación del tamaño del título pasa.
- **Tanda completa (`pruebas/todo.sh`):** 26 problemas, los mismos 26 de antes (baterías que ya fallaban en este
  Linux); ninguno nuevo. `sextatanda` y `quintatanda` pasan en los dos perfiles.
- **Comprobación visual:** imágenes antes y después de los mensajes, el escaparate y la lista, en las nueve
  resoluciones.
- **No comprobado:**
  - En la pantalla del bar. Aquí no está la letra Trebuchet MS de Windows y se usa otra parecida y algo más ancha, así
    que allá los textos deberían caber igual o con más holgura.
  - No sé exactamente cuál texto viste cortado ayer. Si fue otro, una foto ayuda a encontrarlo.
