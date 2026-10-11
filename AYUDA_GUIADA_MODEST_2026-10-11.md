# Ayuda guiada de la estrella — Modest, 11 de octubre de 2026

Petición del usuario: el control de la ayuda pasa a ser el botón rojo y una flecha roja. La casilla de
la ayuda va al lado de la de BUSCAR, con un signo de interrogación dorado y animado en vez de la lupa.
La estrella no da instrucciones escritas sin más: lleva al cliente de la mano por cada paso y, cuando
logra lo que quería, se vuelve a guardar.

Solo GPU. El renderizado por CPU no cambia; en el perfil sin tarjeta gráfica no existen ni la casilla
ni la ayuda. HD sigue suspendida.

## Cómo se abre

- **ROJO + flecha ABAJO**: en la cruceta, las flechas arriba y abajo son rojas. Es la gemela de BUSCAR,
  que se abre con ROJO + ARRIBA. Con la ayuda abierta, ROJO + ABAJO la vuelve a guardar.
- **Tocando la casilla AYUDA** (pantalla táctil o ratón). Con la ayuda abierta, tocarla otra vez la guarda.
  Mientras pregunta, tocar fuera de su globo también la guarda (en la pantalla táctil no hay botón ROJO a mano);
  ese toque no llega a la pared.
- Se retira el gesto anterior (mantener rojo y verde tres segundos) y, con él, la ventana de 120 ms que
  retrasaba cada toque de rojo o verde: los botones responden al instante.
- Al mantener ROJO en la pared, la guía del botón rojo enmarca también la casilla AYUDA y dibuja a su
  izquierda una **flecha roja hacia abajo**. Se ilumina al pulsar abajo.
- Nota: el ROJO hace su acción normal al pulsarlo (dentro de un disco vuelve a la pared; en el buscador
  borra una letra), igual que antes con ROJO + ARRIBA.

## La casilla

Va a la derecha de BUSCAR y se lee igual: signo de interrogación, «AYUDA», el botón rojo, un «+» y la
flecha de abajo en rojo. En pantallas justas (1024×768) se dibuja sin la palabra, para no apretar el
encabezado. El signo lo anima la tarjeta gráfica encima de la interfaz:
- bota y se balancea;
- cada cinco segundos da media vuelta como una moneda;
- tiene un brillo dorado que respira.

Así la textura de la interfaz no se rehace por la animación. Con la ayuda abierta, la casilla queda
encendida.

## Lo que hace la estrella

1. Pregunta «¿Qué quieres hacer?» con tres opciones:
   - Poner una canción.
   - Buscar una canción por su nombre.
   - Aprender a moverme.

   Las flechas eligen, VERDE acepta y ROJO sale. También se puede tocar una opción.
2. A partir de ahí **guía y no se queda con los botones**: el cliente usa la Rocola como siempre. En cada
   latido, la estrella mira el estado real y decide el paso que toca ahora:
   - en qué pantalla está el cliente;
   - si tiene crédito;
   - si abrió el buscador o si está en los resultados;
   - si la canción ya quedó en la cola.

   Pasos:
   - **Poner una canción:** elegir un disco → (meter una moneda, si no hay crédito) → elegir la canción.
   - **Buscar por nombre:** abrir el buscador (ROJO + ARRIBA) → escribir → (moneda) → elegir el resultado.
   - **Aprender a moverme:** moverse entre discos → cambiar de página → saltar de letra (ROJO + ◄/►)
     → entrar en un disco → volver con ROJO.
3. En cada paso:
   - Un globo pequeño se coloca en la esquina contraria a lo que hay que mirar y se queda allí mientras no lo
     tape (no cruza la pantalla cada vez que el disco elegido pasa del centro). Dice «PASO 2 DE 3», un
     título corto, una frase y **los botones dibujados** como son en la máquina: rojo, verde, moneda,
     flechas rojas arriba/abajo y amarillas izquierda/derecha.
   - La estrella se pone a su lado y **señala con la mano**.
   - Un velo suave oscurece todo menos esa pieza, con un **marco dorado** que respira alrededor.
   - Una **flecha dorada** bota hacia la pieza.
4. Si el cliente acierta, la estrella dice «¡Muy bien!», lo celebra y pasa al siguiente paso. Si vuelve
   atrás o se va a otro sitio (por ejemplo, abre el buscador a mitad), la guía vuelve al paso que
   corresponde, sin regañar. Al meter una moneda lo celebra con los brazos.
5. **Cuando lo logra** (la canción quedó en la cola, o terminó el recorrido):
   - Muestra «¡Lo lograste!» y lo celebra.
   - A los 2,6 segundos se vuelve a guardar sola en el cielo.

   También se cierra tras 90 segundos sin tocar nada (tocar la pantalla cuenta como usarla).

Mientras guía, el cuadro del vídeo se oculta para no tapar el globo ni la flecha; el sonido sigue. Al
guardarse vuelve todo como estaba. La guía no pone canciones ni gasta créditos: lo hace el cliente.

## Coste

- El globo se compone una vez por paso, no por cuadro.
- Por cuadro solo se dibujan:
  - hasta 4 cuadriláteros del velo;
  - un marco de 72 vértices;
  - una flecha de 18;
  - el signo de interrogación (un cuadrilátero y un halo).
- Todo en arreglos fijos, sin reservar memoria al dibujar. La textura del signo se hornea una vez por
  tamaño.

## Cómo se comprobó

- **Pruebas automáticas (Linux/Mono, sin Direct3D):**
  - Compilación .NET 3.5 de los dos perfiles con cero avisos.
  - `asistentecombo`, reescrita: el atajo, que ya no existe el acorde, la tabla de pasos, el
    «paso X de N» y una misión completa en un formulario aislado (avanza sola, celebra y se guarda).
    57 comprobaciones tras la revisión.
  - `asistentevisual`, actualizada: menú de tres opciones y globo de la guía.
  - Batería nueva `ayudaguia`: casilla, animación del signo, posición del globo en seis pantallas y
    360 objetivos, el globo con memoria (un disco que recorre la fila y 800 objetivos al azar), marco, flecha y
    conexiones. 10.111 comprobaciones tras la revisión.
  - `guiarojo` y `bloqueos`.
  - Tanda completa: 234 baterías bien. Las que fallan también fallan igual con el código anterior en esta
    máquina (Mono sin Windows ni HAL).
- **No comprobado:**
  - Aspecto real en la tarjeta (no hay capturas HAL).
  - Los botones físicos de la máquina.
  - Una sesión real con música y vídeo.
  - Windows XP.

  Hay que probarlo en la pantalla del bar.

## Revisión del 11-10-2026 (tarde)

Petición del usuario: «Quiero que revises todo lo que hecho buscando mejoras».

- **Fallo corregido: la misión «Buscar una canción por su nombre» no terminaba nunca.** La guía se
  enteraba de la canción puesta desde `AddSong`/`AddVideoToQueue`, pero el buscador encola directamente con
  `AddItem`. Ahora el aviso está en `AddItem`, por donde pasa todo pedido (disco, vídeo o buscador): al
  elegir el resultado, la estrella dice «¡Lo lograste!» y se guarda.
- El globo de la guía recuerda su esquina: solo se muda cuando taparía lo que hay que mirar.
- Tocar la casilla AYUDA con la ayuda abierta la guarda; tocar fuera del globo de la pregunta también.
- Tocar la pantalla cuenta como actividad para el cierre por inactividad de 90 s.
- La casilla se repinta al encenderse o apagarse aunque no la componga la tarjeta.
