# Quinta tanda de fallos, y las flechas de BUSCAR y AYUDA — Modest, 12 de octubre de 2026

Petición del usuario: «Haz otra tanda buscando más fallos, y una cosa: en los iconos de buscar y ayuda, en vez de que
aparezcan las flechitas feas al presionar el botón rojo, que mejor se resalten las que ya tienen en su contenedor».

## 1. Las flechas de BUSCAR y AYUDA con el rojo mantenido

**Antes:** al mantener el botón rojo en la pared salía un triangulito suelto:

- uno amarillo, en el hueco antes de BUSCAR;
- uno rojo, en el hueco antes de AYUDA.

**Ahora:** fuera de las casillas no sale nada. Cada casilla ya trae dibujada su tecla de flecha (arriba en BUSCAR,
abajo en AYUDA), y con el rojo mantenido **esa tecla se enciende**:

- llena de rojo vivo, con la flecha en blanco y un filo claro alrededor;
- encima, el brillo de la tarjeta gráfica, más fuerte mientras se aprieta esa dirección (arriba o abajo).

Sigue igual:

- el contorno dorado de las dos casillas;
- la barra de letras con sus flechas amarillas a los lados;
- las combinaciones del rojo.

**Alcance:** solo con tarjeta gráfica, porque sin ella la guía del rojo no existe. El dibujo por CPU no cambia.

## 2. El reproductor de video que no se puede crear

**Cómo apareció:** con el «mono» de esta tanda. La rocola se cerraba al pedir un video.

**Qué pasaba:** si el control de Windows Media Player no da su reproductor (aquí no existe; en Windows sería un XP sin
Media Player o con él registrado a medias), el control roto se quedaba puesto en la ventana. La rocola devolvía el
crédito, como debe, pero al reacomodar la pantalla volvía a mostrar u ocultar ese control fuera de cualquier
protección, y eso tronaba.

**Ahora:** si el control no da su reproductor, se quita y se suelta **antes** de devolver el crédito. El siguiente
video lo vuelve a intentar desde cero, y si tampoco puede, devuelve el crédito igual.

**No comprobado en Windows:** allí no se sabe si ese control roto llegaba a tronar. El arreglo no cambia nada cuando
el reproductor sí está.

## Cómo se buscó

- **Un «mono» más completo** que el de la tercera tanda. Además de apretar al azar, hace esto:
  - pide videos (aquí no abren);
  - vuelve a leer la biblioteca (F5) con música sonando y cola;
  - borra y repone una canción a media marcha;
  - elige resultados del buscador;
  - cambia de sección de letras.

  Así salió el fallo del video. Con el arreglo, tres vueltas de 4 minutos y semillas distintas (casi 6.000 acciones,
  diez relecturas de la biblioteca con música y cola, videos que no abren): **ninguna excepción**, y al dejarla quieta
  la música siguió y la cola avanzó.
- **El buscador,** con 300.000 búsquedas al azar:
  - acentos, eñes, comillas raras, emojis, caracteres rotos, tabuladores y espacios invisibles;
  - ninguna excepción;
  - la clave se normaliza siempre igual (aplicarla dos veces da lo mismo) y sin espacios de más.
- **Revisión del código:**
  - el gestor de carpetas (Ctrl+F2): rechaza la misma carpeta y una metida dentro de otra;
  - el modo noche, también cuando cruza la medianoche;
  - SALTAR;
  - cómo empieza la siguiente canción.

## Cómo se comprobó

- **Prueba automática nueva `quintatanda`:** 108 comprobaciones con tarjeta gráfica y 5 sin ella.
  - Las teclas de las dos casillas en seis altos, con la casilla de ayuda normal y compacta.
  - Que no se pinte nada en el hueco antes de BUSCAR ni entre las casillas.
  - Que las teclas se enciendan en rojo con la flecha blanca en su sentido.
  - Que todo quede dentro de las casillas.
  - Que una medida vieja no pinte nada, y que el brillo vaya sobre las teclas.
  - El control de video sin reproductor se suelta, y antes de devolver el crédito.
  - Contra el ejecutable anterior falla.
- **Puestas al día:** `ayudaguia` buscaba la flecha suelta de la ayuda; ahora comprueba que se enciende su tecla.
- **Siguen pasando:**
  - `guiarojo`, `ayudaguia` y `asistentecombo`;
  - `cuartatanda`, `terceratanda` y `segundatanda`.
- **El dibujo por CPU no cambió:** sus 14 métodos congelados son idénticos en los dos perfiles.
- **Tanda completa (`pruebas/todo.sh`, con la sexta tanda encima):** 26 problemas, los mismos 26 de antes; ninguno
  nuevo. `quintatanda` pasa en los dos perfiles.
- **Comprobación visual:** vista previa por software de la fila de arriba, antes y después, a dos tamaños.
  - Con la primera versión (un velo claro encima) la flecha roja quedaba deslavada; por eso la tecla encendida es
    roja con la flecha blanca.
  - No es la tarjeta gráfica: el brillo que late está aproximado.
- **No comprobado:**
  - verlo en la tarjeta gráfica del bar;
  - el video en Windows sin Media Player.
