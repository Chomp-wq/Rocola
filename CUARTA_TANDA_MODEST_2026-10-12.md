# Cuarta tanda de fallos — Modest, 12 de octubre de 2026

Petición del usuario: «Haz otra tanda buscando más fallos».

## Cómo se buscó

- **Cortes de luz de verdad, 40 seguidos.** La rocola entera, con su carpeta de datos, se mata de golpe en un
  momento al azar mientras se aprieta sin parar:
  - se meten monedas y se piden canciones;
  - se salta, se cambia de página y se entra a los discos;
  - se cambia un ajuste y se guarda.

  Al volver a arrancarla, se compara lo que restaura con lo último que tenía antes del corte. Fueron 40 cortes, entre
  1 y 9 segundos después de arrancar, con 880 cambios de créditos y cola en medio:
  - **las 39 vueltas comparables restauraron exactamente lo último:** los mismos créditos, la misma canción sonando y
    la misma cola, en el mismo orden;
  - la configuración nunca se perdió ni volvió a la de fábrica;
  - ninguna excepción.
- **Revisión del código:**
  - las teclas de servicio;
  - el archivo de teclas;
  - qué pasa cuando una canción falla a media canción;
  - las fechas y el reinicio de las 7 de la mañana;
  - lo que se guarda y se recupera tras un apagón;
  - las carpetas que no se pueden leer.

## Lo que se arregló

### 1. Un botón de la rocola en F5 volvía a leer toda la biblioteca

F8 (el asistente de botones) y F10 (mantenimiento) ya miraban si un botón de la rocola estaba en esa tecla, y en ese
caso hacían lo del botón. F5 no lo miraba. Si alguno de los ocho botones quedó en F5, cada vez que se apretaba:

- se borraba la biblioteca guardada;
- se volvía a recorrer el disco entero, minutos en un disco duro.

Ahora F5 cede ante el botón, como F8 y F10. Con tarjeta gráfica, F12 (la ventana de prueba de la tarjeta) también.

### 2. El archivo de teclas con dos funciones en el mismo botón

El asistente no deja poner dos funciones en el mismo botón («cada botón debe ser distinto»). Pero si
`rocola_teclas.txt` se editaba a mano o quedaba mal, se aceptaba igual:

- la primera función que se mira se quedaba con el botón y la otra no respondía nunca;
- con la moneda y PONER en el mismo, no se podía pedir nada.

Ahora, con dos funciones en el mismo botón, al arrancar sale el asistente de botones. Un número que no es una tecla
sola (0, negativo o con Ctrl) se ignora.

### 3. La canción que se rompe a media canción

En la tercera tanda, la canción con el archivo dañado que fallaba **al empezar** dejó de poderse pedir otra vez. Si
fallaba **a media canción** por el archivo, no se marcaba: cada vez sonaba un trozo, se cortaba y se devolvía el
crédito. Ahora queda marcada igual, con los mismos límites:

- solo en esta sesión;
- solo si es el archivo, no la salida de sonido ni el disco;
- mientras el archivo sea el mismo.

Además, un lector que la rocola ya cerró (al saltar o al cerrar) ya no cuenta como archivo dañado.

## Lo que se revisó y está bien

- **Los guardados:**
  - se escriben aparte y se cambian de un tirón;
  - si un corte deja solo la copia, se recupera al arrancar;
  - lo de antes del apagón se suma a lo que se haga mientras vuelve el disco.
- **El reinicio de las 7 de la mañana:**
  - espera a que la rocola esté ociosa;
  - un corte breve justo a esa hora no borra los créditos.
- **Las carpetas que no se pueden leer** (sin permiso, ruta de más de 260 letras): se saltan sin tirar el resto.
- **Para tener en cuenta** (es así a propósito):
  - lo más sonado y la cuenta de monedas se guardan cada minuto, así que un apagón pierde como mucho el último minuto
    de esas cuentas;
  - los créditos y la cola se guardan al momento.

## Cómo se comprobó

- **Prueba automática nueva `cuartatanda`:** 17 comprobaciones con tarjeta gráfica y 16 sin ella.
  - Que F5 (y F12 con tarjeta) cedan ante un botón.
  - El archivo de teclas: el de siempre, líneas de más, botones repetidos, ceros, negativos y teclas con Ctrl.
  - La marca a media canción, y que un lector cerrado no marque.
  - Antes de recompilar, con la versión anterior, falló en los cinco puntos de las teclas: detecta el fallo.
- **Siguen pasando**, en los dos perfiles salvo las que solo valen con tarjeta:
  - `terceratanda` y `segundatanda`;
  - `arranquevivo`, `entrar`, `guardadocortado`, `lineaestado`, `salida`, `salir`, `sinpausa`, `vigia` y
    `vigiapantalla`;
  - con tarjeta, `asistentecombo`, `asistentevisual`, `bloqueos` y `mover`.
- **El dibujo por CPU no cambió:** sus 14 métodos congelados son idénticos en los dos perfiles.
- **Tanda completa (`pruebas/todo.sh`):** 26 problemas, los mismos 26 de antes (baterías que ya fallaban en este
  Linux); ninguno nuevo. `cuartatanda` pasa en los dos perfiles.
- **No comprobado:**
  - en la máquina del bar;
  - con un botón real en F5;
  - una canción que se rompa a media canción con el sonido de Windows. Aquí los MP3 no se pueden decodificar, así que
    se comprobó el camino del código, no con el sonido real.
