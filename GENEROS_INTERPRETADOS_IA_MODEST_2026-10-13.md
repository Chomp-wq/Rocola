# La IA aprende también con el género deducido — Modest, 13 de octubre de 2026

Petición del usuario: «Haz que el recolector de datos para la IA tome los géneros de las canciones que no tienen
etiqueta de género pero que su género fue interpretado».

## Qué pasaba

El recolector apuntaba en `rocola_ia_datos.generos.tsv` solo el género de la etiqueta del archivo. Las canciones sin
etiqueta quedaban con el género vacío y no contaban para entrenar el DSEE por género. Sin embargo, la rocola ya deducía
el género de su disco para la pastilla de la pantalla (`GeneroDeCancion`), de tres formas:

- lo que vota el paseo de fondo con las etiquetas de las demás canciones del disco;
- el género que más se repite en los discos del mismo artista;
- lo que dice el nombre del disco o de la carpeta («Cumbias del Recuerdo», «Baladas Románticas»).

Si con nada de eso se sabe, no se inventa.

## Qué se cambió (`Form1.Ia.cs`)

- **El recolector.** A una canción sin etiqueta fiable (vacía, «Unknown», «Desconocido»…) le apunta el género deducido
  de su disco. Las etiquetas de verdad siguen mandando.
- **Al sonar.** El DSEE por género usa ese mismo género para esas canciones. Si no, su material entrenaría un modelo
  que luego nunca se usaría con ellas.
- **El material ya juntado.** Las canciones recolectadas antes, sin etiqueta, tenían el género vacío. Ahora la rocola
  les añade una línea con el género deducido:
  - solo a las que de verdad tienen material (su huella está en `rocola_ia_datos.idx`);
  - solo si no tenían ya un género;
  - el entrenador se queda con la última línea de cada canción;
  - no hace falta volver a recolectar ni repasar la biblioteca.

  Se hace en segundo plano al acabar de escanear la biblioteca o el paseo de fondo, aunque «Juntar material para la
  IA» esté apagado. Si no hay material, no escribe nada.
- **Sin molestar a la pantalla.** Los géneros deducidos son datos del hilo de la pantalla y el material se junta en el
  de fondo. Por eso la pantalla deja una copia fija (por carpeta, y por huella para lo ya juntado) que el recolector
  solo lee. Se rehace al escanear, al terminar el paseo y, con votos llegando, como mucho cada 10 s.

## Lo que hay que saber

- **El género deducido es la categoría de la pastilla**, más amplia que una etiqueta: «Regional», «Latino», «Rock»,
  «Pop», «Bolero», «Electronica»… Por ejemplo, un disco «Cumbias del Recuerdo» sin etiquetas cae en **«Regional»**.
  Las canciones con etiqueta siguen con su etiqueta tal cual («cumbia», «cumbia sonidera»…). Así salen grupos
  distintos: el modelo «regional» aprende con las deducidas y las que tengan esa etiqueta.
- Si prefieres que todo (también lo etiquetado) se agrupe por esas categorías amplias, es otro cambio; dímelo.
- Para que cuente lo ya juntado, abre la rocola con la versión nueva y deja que termine de escanear (o el paseo de
  fondo). Después trae otra vez `rocola_ia_datos.generos.tsv` (con el `.bin` y el `.idx`) y vuelve a entrenar.

## Cómo se comprobó

**`mejorasia`** (46 comprobaciones; 9 nuevas), con una biblioteca de prueba:

- El disco «Cumbias Del Recuerdo», sin etiquetas, sale «Regional», como su pastilla. «Coleccion 1» no se sabe y no
  se inventa. Con la carpeta «Disco 1», la rocola leyó el género Disco: el nombre sí lo dice.
- El recolector apunta «regional» a la canción sin etiqueta.
- Al sonar, esa canción usa el modelo «regional». Sin género deducido, el general.
- La copia guarda las canciones sin etiqueta fiable (vacía y «Unknown») y no la que trae «Bolero».
- El material ya juntado:
  - la canción sin género recibe el suyo;
  - la que ya tenía «rock» no se toca;
  - la que no tiene material no se apunta;
  - una segunda pasada no repite líneas.
- Enchufado: la copia se rehace al escanear y con el paseo; el recolector y el DSEE la usan; lo ya juntado se
  completa en segundo plano.

También:

- **`bloqueos`** sigue pasando: nada de esto lee archivos en el hilo de la pantalla al arrancar.
- **`genero`** sigue pasando.
- **`etiqueta`** destapó un fallo al probar: con un formulario a medio armar, la copia para la IA se caía y arrastraba
  el cálculo de los géneros de la pantalla. Ahora la copia nunca puede tumbar a quien la llama: si falla, sigue la de
  antes. `etiqueta` vuelve a pasar.
- **Tanda completa (`pruebas/todo.sh`):** los mismos 26 problemas de siempre, ninguno nuevo. Dibujo por CPU: sin tocar.

**No probado aquí:** con la biblioteca del bar y su material de verdad.
