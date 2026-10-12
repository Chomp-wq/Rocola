# Tercera tanda de fallos — Modest, 12 de octubre de 2026

Petición del usuario: «Busca más fallas».

## Cómo se buscó

- **Un «mono» con la rocola entera.** Aprieta al azar durante cuatro minutos, unas 2.000 acciones por vuelta:
  - las flechas, verde, rojo, moneda y saltar, también como tecla mantenida;
  - pedir canciones, pasar páginas, entrar y salir de discos;
  - escribir y borrar en el buscador;
  - la atracción;
  - los mandos del local y la prueba de bocinas;
  - el reloj que se atrasa;
  - cambiar el tamaño de la ventana (de 640×480 a 1366×768) en plena marcha.

  Cinco vueltas con semillas distintas, casi 9.500 acciones: **ninguna excepción**. Los créditos nunca bajaron de
  cero, y al dejarla quieta la música siguió y la cola avanzó.
- **La configuración con valores raros.** Cada una de las 120 claves de `rocola_config.txt` se probó con 14 valores
  raros: NaN, infinito, enormes, negativos, vacíos, letras, con coma. Ninguno corta la carga de las demás claves ni
  deja un número inválido.
- **Los archivos de datos** (volumen, duraciones, EQ, sonoridad): descartan las filas con NaN, infinitos o fuera de
  rango.
- **Revisión automática del código:**
  - textos con formato y su número de datos;
  - listas que se cambian mientras se recorren;
  - divisiones y restos con el reloj de Windows (que a los 24,9 días pasa a negativo).

  Nada roto.

## Lo que se arregló

### 1. Una canción dañada se podía pedir una y otra vez

Si el archivo de una canción está roto (cortado, con la cabecera mala o con frecuencias mezcladas), al llegarle el
turno no sonaba, se devolvía el crédito y se pasaba a la siguiente. Pero seguía en la lista: el siguiente cliente la
pedía, entraba en la cola y volvía a fallar. Quien la pidió ya se había ido de la rocola y no sabía qué pasó.

Ahora, después del primer fallo, al pedirla sale al momento **«Esa canción está dañada: elige otra»**:

- no entra en la cola ni cobra;
- avisa antes de pedir moneda: no tiene sentido pagar por algo que no va a sonar.

La marca tiene límites:

- **Solo vale en esta sesión**, y solo mientras el archivo sea el mismo. Si el operador lo cambia por uno bueno, se
  vuelve a poder pedir.
- **Un disco desconectado o que no deja leer no la marca.** Al volver el disco, suena.
- Como antes, la lista de archivos con problemas queda en `rocola_incompatibles.txt`, para reconvertirlos.

### 2. El motivo del fallo, primero

Cuando una canción no sonaba, la línea de estado decía «No se pudo reproducir 01 - …: el archivo está dañado». Con un
nombre largo, la orilla se comía justo el motivo. Ahora va primero: «No se pudo reproducir: el archivo está dañado
o en un formato que no se puede tocar  (01 - …)».

### 3. La biblioteca vacía

Decía «No encontre MP3 en esa carpeta», pero la rocola toca también WAV, WMA y otros. Ahora dice «No encontré música
en la carpeta».

## Cómo se comprobó

- **Prueba automática nueva `terceratanda`** (17 comprobaciones, en los dos perfiles):
  - qué fallos son del archivo y cuáles del disco;
  - la marca, y que se olvida al cambiar el archivo;
  - que la canción no entre ni cobre, también sin créditos;
  - los avisos.
- **Con la rocola entera:** la canción dañada, pedida dos veces, falla una sola vez y la segunda sale el aviso al
  momento, con los créditos intactos.
- **Siguen pasando:** `segundatanda` y `lineaestado`.
- **No comprobado:** en la máquina del bar con su biblioteca. El mono corre aquí con el dibujo por CPU, sin tarjeta
  gráfica.
