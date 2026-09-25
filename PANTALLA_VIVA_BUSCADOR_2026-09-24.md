# La pantalla viva, tanda 6: el buscador con portadas

Cambio del 24 de septiembre de 2026, compartido por HD y Modest. Es la idea del lienzo de bocetos **"Teclado compacto
y resultados con portada"**: los resultados salen mientras se escribe, en tarjetas grandes con su portada, su tipo y
las marcas de la cola, y arriba hay atajos (lo más pedido, videos, karaoke y lo último que se buscó).

Solo cambia el dibujo por tarjeta gráfica. El renderizador por procesador sigue congelado: sin tarjeta, el buscador
es el de siempre. Con tarjeta y el interruptor apagado, también (comprobado píxel a píxel, ver abajo).

Va encima de los cambios del mismo día en las dos ramas (`NAVEGACION_Y_RELOJ_2026-09-24.md`,
`TITULOS_LEGIBLES_2026-09-24.md`, `PAGINAS_Y_ESTADO_2026-09-24.md`): la ventana del buscador que sube al abrir, los
prefijos de pista y la tarjeta del estilo con los títulos deslizantes.

El interruptor está en Ctrl+F3 → **Pantalla**, hoja **La atracción y el estilo**, tarjeta **EL BUSCADOR**
("Buscador con portadas (GPU)"), debajo de la del estilo general. Viene encendido y se guarda en `rocola_config.txt`
(`buscador_portadas=`). "Pantalla como de fábrica" lo vuelve a encender.

El código nuevo está en `Rocola/BuscadorPortadasGpu.cs`. Los demás archivos llevan solo los enganches: el buscador
(`Form1.Buscador.cs`: cada cosa pregunta primero si manda el nuevo), las firmas y las piezas (`Form1.Video.cs`), el
cuadro de la tarjeta y la ventana (`D3D9Star.cs`), el cristal de la lista del disco (`AcabadoGpu.cs`), abrir, cerrar,
contar, elegir y las portadas que llegan (`Form1.Biblioteca.cs`), de dónde sale la portada al entrar a un disco
(`Form1.Sonido.cs`), el interruptor (`Rendimiento.cs`, `Form1.Datos.cs`, `PantallaVivaGpu.cs`).

## Qué se ve

- **La casilla de lo escrito**, arriba, más grande y con su lupa. A la derecha dice cuántos resultados hay: todas las
  coincidencias ("1.234 resultados"), aunque se enseñen las 40 mejores, como siempre. Vacía, invita: "Escribe un
  artista, un disco o una canción".
- **Los atajos**, debajo. Solo salen los que tienen algo de verdad detrás, en este orden:
  - **Lo más pedido hoy**: las canciones más pedidas desde las 8 de la mañana, de más a menos (la misma cuenta que usa
    el modo atracción). La que ya no esté en la biblioteca no sale.
  - **Videos**: lleva a la sección de videos de la pared, igual que su letra.
  - **Karaoke**: los discos cuyo género es Karaoke (el mismo género que pinta las pastillas).
  - **Buscaste: …**: las dos últimas búsquedas con las que alguien eligió algo, de esta noche, la más nueva primero y
    sin repetir. Solo en memoria (al reiniciar se olvidan), nunca la clave de servicio, ni una letra suelta, ni lo que
    puso otro atajo.

  Se llega a ellos subiendo desde la fila de los números (o tocándolos). Con el verde se usan: lo más pedido y
  karaoke ponen sus tarjetas y la selección salta a la primera; "Buscaste" vuelve a buscar. Con la casilla vacía, el
  rojo (borrar) sale del atajo. Si no caben todos, se quedan fuera los últimos.
- **El teclado compacto**, a la izquierda: teclas de tamaño fijo (50 px en el boceto de 1280×720), sin estirarlas hasta
  llenar media pantalla, y **siempre a la vista**: elegir un resultado ya no lo tapa con la portada grande. BORRAR va
  en rojo. Al subir o bajar, la selección va a la tecla que queda más cerca.
- **Las tarjetas**, a la derecha, en dos columnas:
  - la portada de su disco (la del primer disco del artista que la tenga); mientras no llega, las iniciales sobre un
    degradado del color del género;
  - qué es: ARTISTA (azul), ÁLBUM (ámbar) o CANCIÓN (verde);
  - el nombre, con lo tecleado resaltado; el de una canción, sin su número de pista ("03 - Dreamer" sale
    "Dreamer"), como en el resto del dibujo por tarjeta. Si no cabe, se corta con puntos suspensivos, como las
    tarjetas del buscador de siempre; en la **tarjeta elegida**, con **Títulos largos deslizantes** encendido, el nombre
    y el renglón de debajo que no caben se deslizan (ver "Añadido el 25 de septiembre");
  - un renglón que ayuda a elegir: los discos y canciones del artista; el artista y las canciones del álbum; el artista
    y la duración de la canción (o su disco, si aún no se sabe lo que dura);
  - **SONANDO** y **EN COLA n**, con las mismas palabras y colores que dentro del disco.

  La elegida lleva un aro blanco y un resplandor del color de la sección. Desde el final de una fila del teclado, a la
  derecha, se pasa a la tarjeta que queda al lado; desde la primera columna, a la izquierda, se vuelve a la fila de
  teclas que queda al lado. Si hay más tarjetas de las que caben, se baja fila a fila y una barrita a la derecha dice por
  dónde se va. Al entrar a un disco desde el buscador, la portada sale volando de la de su tarjeta.
- **Las tarjetas que llegan entran una tras otra**: cada una sube 12 px mientras aparece, en 240 ms, con 60 ms entre una
  y la siguiente. Solo entran las que cambiaron; las que siguen iguales se quedan quietas. Al bajar de fila, las tarjetas
  ya están puestas: no se hace esperar a quien baja.

Las ayudas (Mover, Poner / Elegir, Cerrar) están abajo del todo y siguen destellando al pulsar.

Imágenes de la simulación: se enviaron por el chat (a 800×600, 1024×768, 1280×720, 1280×1024 y 1920×1080).

## Cómo trabaja

- **La imagen grande solo lleva lo quieto**: los atajos, el teclado sin elegir y las ayudas. Teclear, posar la consulta y
  moverse no la tocan (en el buscador de siempre, posar rehacía la imagen entera y subía la columna de resultados).
- **La casilla, la tecla o el atajo elegido y la tarjeta elegida** van en sus imágenes chicas, como ya pasaba con la
  casilla y la tecla.
- **Las tarjetas van en otra imagen** (la pieza 4), que se rehace al posar la consulta, al bajar de fila, al cambiar la
  cola o al llegar una portada. La tarjeta gráfica la dibuja tarjeta a tarjeta, en un solo envío: así hace la entrada y,
  de paso, no pinta el hueco transparente entre tarjetas.
- **Las portadas** se piden al disco solo para las tarjetas que se ven y cuando la vista lleva un cuarto de segundo
  quieta: quien teclea deprisa no encarga nada. Van por la cola de portadas de siempre, con sus carriles contados y por
  debajo de la interfaz. Ya escaladas se guardan las últimas 64. Si llegan varias seguidas, las tarjetas se rehacen de
  una vez (como mucho cada 120 ms).
- **Lo quieto va horneado.** El resplandor, el fondo y el borde de la tecla, el atajo o la tarjeta elegidos, los de la
  casilla con su lupa y los de cada tarjeta con la sombra de su portada solo dependen del tamaño, del color y de la
  escala: se dibujan una vez y después se pegan. Teclear y moverse ya no rehacen el resplandor capa a capa. Se guardan
  unas once imágenes (0,9 MB a 1024×768; 2,7 MB a 1920×1080) y nunca más de 16; al cambiar el tamaño de la pantalla se
  sueltan las de antes y, con el interruptor apagado, todas. Es automático, sin interruptor. La casilla, la tecla, el
  atajo y la tarjeta elegidos salen idénticos píxel a píxel a dibujarlos capa a capa (se hornean en el mismo sitio que
  ocupan en su imagen); en la imagen de todas las tarjetas cambian unos pocos píxeles del borde suavizado, entre 4 y 21
  por imagen y en 1 de 255, porque cada tarjeta se hornea en otro sitio que donde va: no se nota a la vista.
- **Con la ventana.** El buscador entra subiendo desde abajo y se va bajando (`NAVEGACION_Y_RELOJ_2026-09-24.md`).
  La casilla, lo elegido y (en el nuevo) las tarjetas van en sus imágenes chicas, no en la imagen grande, así que van
  con ella: al abrir se dibujan corridas lo mismo que la ventana; al cerrar, la ventana se queda con sus texturas ya
  subidas (sin copiar ni subir nada, como hace ya con la imagen grande) y se sueltan al acabar. Con ellas van las cintas
  de su vista previa (lo que se desliza). Sin esto subiría sin ellas y saldrían de golpe al llegar. Desde el 25 de
  septiembre vale también para el buscador de siempre.

### Lo que cuesta

Medido con Mono en este contenedor: la mediana de 15 veces, en dos corridas (por eso van de un número a otro). Sirve
para comparar entre sí; no son los milisegundos del bar ni de un procesador de dos núcleos.

| | 1024×768 | 1920×1080 |
|---|---|---|
| **Posar una consulta**, el de siempre: rehacer la imagen grande | 3,1–3,4 ms, y subir 1,26 MB | 4,9–5,0 ms, y subir 3,46 MB |
| **Posar una consulta**, el nuevo: las tarjetas y la casilla | 2,2–2,7 + 0,34–0,39 ms, y subir 1,22 MB | 2,6–2,7 + 0,47–0,49 ms, y subir 3,48 MB |
| **Cada letra** (la casilla) | 0,28–0,35 ms y 346 KB; el de siempre, 0,41–0,50 ms y 279 KB | 0,41–0,43 ms y 1.084 KB; el de siempre, 0,46 ms y 538 KB |
| **Moverse por el teclado** | 0,19–0,21 ms y 23 KB; el de siempre, 0,30–0,36 ms y 19 KB | 0,19–0,20 ms y 67 KB; el de siempre, 0,33–0,34 ms y 61 KB |
| **Moverse por las tarjetas** | 0,35–0,45 ms y 133 KB; el de siempre, 0,27–0,33 ms y 38 KB, y además el texto de debajo del cubo | 0,46–0,47 ms y 442 KB; el de siempre, 0,26–0,27 ms y 75 KB, y además el texto |
| **La imagen grande** (al abrir o con un aviso) | 3,4–3,7 ms | 4,8–4,9 ms |
| **Escalar una portada** (la primera vez; luego se guarda) | 0,54–0,56 ms | 0,55 ms |
| **Lo horneado**, guardado | 11 imágenes, 0,9 MB | 11 imágenes, 2,7 MB |

Lo que hizo hornear lo quieto, medido igual con el mismo buscador sin hornear: posar bajó de 3,8–3,9 + 0,84 ms a lo de
la tabla a 1024×768 (de 4,7 + 2,0 a 1920×1080); moverse por el teclado, de 0,54–0,81 ms (0,96–0,99); por las tarjetas,
de 0,88–0,93 ms (1,83–1,95).

En resumen: moverse por el teclado y teclear cuestan menos que en el buscador de siempre, y posar una consulta un poco
menos, subiendo lo mismo. Moverse por las tarjetas cuesta algo más, porque la tarjeta elegida es más grande y lleva su
resplandor. Y al teclear a 1920×1080 se sube más a la tarjeta gráfica (la casilla con su resplandor: 1 MB en vez de
medio); a 1024×768 la diferencia es pequeña (346 KB frente a 279).

## Qué no cambia

- **Sin tarjeta gráfica, o con el interruptor apagado, el buscador sale idéntico al de antes** (parado; lo único que
  cambia es que ahora va con la ventana al abrir y al cerrar, ver abajo). En cada rama se
  compiló la versión publicada (HD `49abb0a`, Modest `5d9fda8`) y la nueva y se compararon sus pantallas del buscador,
  con los mismos datos y los mismos pasos (abrir, moverse, pasar a los resultados, bajar, un aviso, teclear y borrar,
  volver), a 1024×768, 1280×720 y 1920×1080: 24 pantallas por procesador y 48 por tarjeta (la imagen grande y sus
  piezas), **0 píxeles distintos** en las dos ramas.
- Lo que se busca y cómo: las mismas 40 mejores, las mismas marcas, la tarjeta de servicio, el botón de cerrar.
- **En el buscador de siempre cambia una cosa, solo con la ventana moviéndose** (25 de septiembre): su casilla, la
  tecla, el resultado elegido y el texto de debajo del cubo, que van en imágenes chicas, ahora suben y bajan con la
  ventana en vez de salir de golpe al llegar. Parado sale igual que antes: se volvió a comparar con la versión publicada
  (24 pantallas por procesador y 48 por tarjeta, 0 píxeles distintos).

## Cómo se comprobó y qué falta

- **Pruebas automáticas.** La batería nueva `pruebas/buscaportadas.cs` corre con los dos perfiles:
  - Con tarjeta gráfica: 88 comprobaciones, en las dos ramas. Entre ellas:
    - el reparto en seis pantallas (de 800×600 a 1920×1080): todo cabe, nada se pisa, las teclas miden lo mismo, las
      tarjetas van en dos columnas, y los atajos que no caben se quedan fuera sin salirse de su fila;
    - 13.500 pasos al azar entre atajos, teclado y tarjetas: la selección siempre vale, siempre se ve (también al
      bajar de fila) y tiene su imagen chica; y los pasos concretos, fila por fila;
    - el verde y el dedo en teclas, atajos y tarjetas;
    - la imagen grande sale igual píxel a píxel con otra consulta, otros resultados o la selección en otro sitio, y su
      firma no se mueve;
    - fuera de las tarjetas, la imagen de las tarjetas es transparente del todo, así que dibujarla tarjeta a tarjeta da
      la misma pantalla que entera; y lo elegido tapa entero lo que tiene debajo;
    - la entrada una tras otra, las que no cambian quietas, al bajar ya puestas y la elegida a la vez que la suya;
    - los atajos con datos de verdad (la cuenta de la noche, videos, un disco de karaoke, búsquedas repetidas, largas,
      de otra noche, la clave de servicio); y los números de pista fuera de la tarjeta (y los del nombre, dentro);
    - las portadas: nada con la vista moviéndose, solo las que se ven, una vez, y juntando las que llegan;
    - con una tarjeta gráfica imitada: las tarjetas van en un solo envío por trozos, antes que la tarjeta elegida, cada
      trozo en su sitio, con su transparencia; y sin ellas, todo como siempre;
    - con la ventana, en la tarjeta imitada: al abrir suben con ella (las mismas imágenes, corridas lo mismo); con el
      buscador de siempre, como siempre; al cerrar, la ventana se queda con sus texturas sin copiar nada y bajan
      juntas; al acabar se sueltan; y si la ventana no llega a salir, se vuelven a subir;
    - lo quieto horneado: pegarlo da los mismos píxeles que dibujarlo capa a capa (48 casos: teclas, ESPACIO, BORRAR,
      atajos, la casilla y las tarjetas, con dos colores y en tres pantallas) y cae en el mismo sitio de su imagen
      chica; moverse y teclear no hornean nada (1.200 imágenes chicas, 0 horneados); se guardan pocas (11, tope 16);
      otro color no reutiliza lo del anterior; al cambiar de tamaño y al apagar, se suelta;
    - la tarjeta de Ctrl+F3, su preferencia y "de fábrica"; y los 25 enganches.
  - Sin tarjeta gráfica: 6, en las dos ramas (que el buscador no tiene nada del nuevo, la pestaña y la preferencia).
- **Mutaciones.** Se rompió el código a propósito de 68 maneras (el reparto, moverse, pulsar, la imagen grande, las
  piezas, la entrada, los atajos, las portadas, la tarjeta imitada, el destello, los enganches, lo horneado, la ventana
  y los números de pista), una cada vez: la batería lo detectó las 68 en HD, sobre la base nueva. En Modest se repitieron
  las 22 de los enganches, la ventana, lo horneado y los números de pista: detectadas las 22.
- **Las baterías de antes**: `pestanas` conoce el interruptor nuevo. **Las tandas completas no están enteras en verde, y
  no por esto**: en HD fallan las mismas cinco baterías que en la versión publicada `49abb0a` sin este cambio (se
  corrieron sobre ella para compararlo): `pestanas` (`viniloDisco` ya no tiene interruptor en Ctrl+F3), `estilo` (no
  compila: una variable sin usar en la prueba), `panelreposo` (las cintas de texto nuevas no están en su lista),
  `suenavivo` (lo del vinilo retirado) y `paginasgpu` (se queda sin Direct3D aquí, como ya decía su guía).
  En Modest, lo mismo: las mismas cinco, que también fallan en su versión publicada `5d9fda8` (se corrieron sobre ella).
  La primera versión de la ventana sí rompía `envios` (dos envíos de dibujo nuevos dentro de bucles): se arregló
  dibujando las piezas de la ventana una a una, que son cinco y fijas, y `envios` volvió a pasar.
- **Simulación, no comprobación real.** Las imágenes se hicieron con las mismas funciones del programa, en Linux (Mono),
  con un cielo aproximado y la letra que hay aquí (DejaVu Sans; en Windows será Microsoft Sans Serif, algo más
  estrecha).
- **Sin comprobación visual real.** En este contenedor no hay Direct3D: hace falta mirarlo en el equipo del bar y en el
  de HD.
- **Sin probar en Windows XP.** La rama Modest compila con el lenguaje de .NET 3.5 (C# 3), pero no se ha probado en un
  Windows XP real.

## Añadido el 25 de septiembre

### La tarjeta elegida desliza lo que no cabe

Con **Títulos largos deslizantes** encendido (Ctrl+F3 → Pantalla → La atracción y el estilo, `TITULOS_LEGIBLES_2026-09-24.md`),
el nombre y el renglón de debajo de la **tarjeta elegida** que no caben se deslizan en bucle, con las mismas pausas que
el resto (2,2 s al principio, 1,4 s al final). En el buscador de siempre eso lo hace la vista previa, el texto de debajo
del cubo; en el nuevo no hay cubo, así que lo hace la tarjeta elegida: es donde se lee el nombre entero. Las demás
tarjetas siguen cortando con puntos suspensivos, como pide la guía de los títulos para las tarjetas de resultados.

- Usa el mismo sistema de cintas, sin grupos nuevos: el grupo 4 es "lo que se desliza de la vista previa del buscador".
  El formulario le dice al panel qué pieza es: la 3 (el texto de debajo del cubo) en el de siempre y la 2 (la tarjeta
  elegida) en el nuevo. En `TextoMovilGpu.cs` cambian tres renglones de `DibujaCintasTexto`: qué grupos van (lo mismo
  que antes, y con la ventana solo el 4), de qué pieza sale el grupo 4 y con qué transparencia.
- La cinta se prepara al hacer la imagen chica de la tarjeta elegida. Si el nombre no cabe, va en su cinta y en la imagen
  no se dibuja; si cabe, o con el interruptor apagado, sale quieto como antes, con lo tecleado resaltado. En la cinta no
  va el resaltado, como en la vista previa del de siempre.
- La cinta entra con su tarjeta (la misma transparencia) y va con la ventana al abrir y al cerrar.

### Los dos buscadores van con la ventana

Lo que ya hacía el nuevo (ver "Con la ventana") vale ahora también para el de siempre: su casilla, la tecla, el
resultado elegido y el texto de debajo del cubo suben y bajan con la ventana, con sus cintas. Sin copiar ni subir nada:
al cerrar, la ventana se queda con las texturas ya subidas y las suelta al acabar.

### Cómo se comprobó

- **Batería** `buscaportadas`: 100 comprobaciones con tarjeta gráfica y 6 sin ella, en las dos ramas. Lo nuevo:
  - la cinta del nombre y la del renglón, dentro de la imagen de la tarjeta elegida, y el nombre no dibujado quieto (0
    píxeles de letra donde va la cinta);
  - apagado, quietos y cortados (687 píxeles de letra), y cambiar el interruptor rehace la imagen;
  - lo que cabe no se desliza, tampoco lo que cabe justo (un nombre de 228 px en una columna de 229);
  - en la tarjeta imitada: encima de la elegida, en el sitio del nombre y con su transparencia; con el buscador de siempre
    esas cintas son de la pieza 3; con el vidrio de las canciones no van; entrando, con la misma transparencia que su
    tarjeta; y con la ventana al abrir y al cerrar, también con el buscador de siempre;
  - el formulario: las piezas van con la ventana con los dos buscadores y se las lleva al cerrar sin preguntar cuál.
- **Las baterías de al lado**: `textosgpu` (la de las cintas) y `envios` pasan; `panelreposo` sigue con los mismos dos
  fallos que ya tenía.
- **El buscador de siempre parado**: 0 píxeles distintos con la versión publicada, en las dos ramas.
- **Mutaciones**: 82 en HD (las de antes y las nuevas de las cintas y de la ventana con los dos buscadores). Se
  detectaron 81; la que sobrevivió quitaba una comprobación que sobraba ("si cabe, sin cinta", que ya hace la cinta
  sola). Se quitó, se ajustó el recuadro para que la cinta corte justo donde el renglón quieto y se añadió la prueba
  de un nombre que cabe justo (228 px en una columna de 229): se repitieron las 13 de las cintas, con una que corre ese
  borde, y se detectaron todas. En Modest, 22 de los enganches, la ventana y las cintas: detectadas las 22.
- **Tandas completas**: en las dos ramas fallan las mismas cinco baterías que ya fallaban en las versiones publicadas
  (`pestanas`, `estilo`, `panelreposo`, `suenavivo` y `paginasgpu`), y ninguna nueva; `textosgpu`, `envios` y
  `buscaportadas` (con los dos perfiles) pasan. En la corrida de HD, `panelreposo` salió con código 5 por un error de
  `xvfb-run` al limpiar su carpeta temporal; corrida sola, sale como siempre (los mismos dos fallos).
- **Sin comprobar**: una tarjeta gráfica real y Windows XP real, igual que el resto.
