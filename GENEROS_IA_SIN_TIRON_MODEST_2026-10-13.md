# Los géneros para la IA, sin cargar la pantalla — Modest, 13 de octubre de 2026

Petición del usuario: «Busca más mejoras».

Es una optimización que no cambia nada de lo que se ve ni de lo que se apunta. Es automática y sin interruptor.

## Lo que se encontró

Medí la copia de los géneros deducidos para la IA, que añadí hoy
([GENEROS_INTERPRETADOS_IA_MODEST_2026-10-13.md](GENEROS_INTERPRETADOS_IA_MODEST_2026-10-13.md)). Se hacía entera en el
hilo de la pantalla. Con una biblioteca de 30.000 canciones en 2.000 discos, aquí:

| Qué | Antes | Ahora |
| --- | --- | --- |
| Cada vez que se rehace (al llegar votos del paseo, al escanear) | 41 ms | 1,3 a 2 ms |
| Justo después de un escaneo (mirar el nombre de todos los discos) | 26 ms más | incluido arriba |

En el equipo del bar, con dos núcleos lentos, eran fácilmente 100 ms o más: un tirón de la pantalla, justo al acabar
de escanear o del paseo de fondo. La primera vez de cada sesión se suman unos 30 ms de preparar el código, una sola
vez.

## Qué se cambió

En el hilo de la pantalla queda solo lo que es suyo: la etiqueta de cada disco (votos y artista, que son tablas de la
pantalla) y la lista de sus canciones. Lo demás pasa a segundo plano:

- **las carpetas** de cada canción;
- **lo que dice el nombre** del disco o de su carpeta, si aún no estaba guardado. Es la misma cuenta de la pastilla,
  ahora en una función sin estado (`NombreDice`) que usan las dos;
- **las duraciones**, leídas del archivo de medidas (`rocola_volumen.txt`) en vez de la tabla de la pantalla;
- **las huellas** de las canciones y el completado del material ya juntado.

Si llegan dos copias seguidas, se queda la más nueva.

## Cómo se comprobó

- **Medido aquí**, con una biblioteca inventada de 2.000 discos y 30.000 canciones (cinco tipos de nombre, un tercio
  con etiqueta).
- **`mejorasia`** (46 comprobaciones): los mismos resultados de antes, con la copia en segundo plano. El disco
  «Cumbias Del Recuerdo» sale «Regional», el recolector lo apunta, el DSEE lo usa y lo ya juntado se completa. Lee el
  código para comprobar que el hilo de la pantalla ya no calcula huellas ni lee el archivo de medidas ni el nombre, y
  que la pastilla usa la misma cuenta del nombre.
- **`etiqueta`**, **`genero`** y **`bloqueos`** siguen pasando.
- **Tanda completa (`pruebas/todo.sh`):** los mismos 26 problemas de siempre, ninguno nuevo. Dibujo por CPU: sin tocar.
