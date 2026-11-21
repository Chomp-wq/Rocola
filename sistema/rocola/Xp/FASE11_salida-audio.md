# Fase 11: el selector de la salida de sonido (hoja «Sonido» de Ctrl+F4)

Petición del dueño del 21-11-2026: «agrega [...] un selector para la salida de audio, ya que como recordarás la última vez no se
escuchaba» (el 7-11-2026 el sonido salía por el HDMI de la pantalla y no por la salida verde: `rocola-sonido`, `rocola-tarjeta`). Solo
Rocola OS (`rocola-xp` con `ROCOLA_OS`); sin `ROCOLA_OS`, XP tal cual (Ctrl+F4 no hace nada). No probado en el A4.

## Lo que hay

- `RocolaOs/RocolaOs.VentanaSalida.cs`: la pestaña **Sonido** de Ctrl+F4 (orden 15, entre Pantalla y Navegador; las pestañas van pegadas
  a la derecha, así que Navegador y Archivos no se mueven). Dos tarjetas con el aspecto de Ctrl+F3:
  - **SALIDAS DE SONIDO**: una fila por salida, numerada como `rocola-sonido usar N` (el orden de `aplay -l`), con **HDMI (pantalla)** en
    ámbar o **Placa** en verde, su nombre, «(suena ahora)» con borde verde, y debajo tarjeta, salida, `plughw:T,S`, el nombre de la
    tarjeta y «la automática». Hasta 8 filas.
  - **LA QUE SE USA**: la de ahora, si es a mano o la automática, «Usar la salida N (prueba de 15 s)», «Probar un pitido por la N (3 s)» y
    «Automática (la salida N)».
- `RocolaOs/RocolaOs.Salida.cs`: lo que no es dibujo, y la función `salida_audio` (encendida de fábrica; `salida_audio=0` en
  `rocola_os.txt` deja la hoja en gris diciendo por qué y no pregunta nada al arrancar).
  - Las salidas salen de `/proc/asound` (`ROCOLA_ASOUND` en las pruebas) con las **mismas reglas que `rocola-tarjeta`** para el HDMI (la
    línea de la tarjeta en `cards`, su `id`, las dos primeras líneas de sus `codec#*`, sus `eld#*` o el nombre de la salida) y la
    automática es la misma (la primera que no es HDMI). La que suena: `ROCOLA_ALSA` (la pone el lanzador con `rocola-tarjeta`); si no
    dice una salida, la que elegiría `rocola-tarjeta`.
  - **Elegir** escribe `/rocola/datos/rocola_tarjeta.txt` (`plughw:T,S` con LF, por un temporal y con fsync; la automática lo borra;
    `ROCOLA_TARJETA_TXT` en las pruebas), como `rocola-sonido usar N` / `auto`, y lo apunta en `rocola_sonido.txt`. Antes escribe
    `rocola_salida_prueba.txt` en los datos de rocola-xp (`antes=`, `nueva=`, `arranques=`). Luego cierra la ventana, deja
    `$RUN/rocola-xp-reinicio` (el lanzador no lo cuenta como caída) y se manda **SIGUSR1** a sí misma: el cierre ordenado que **conserva
    el estado** (TaskManagerClosing: créditos, cola y lo que suena), el mismo que usan el lanzador y `rocola-sonido`. El lanzador
    (`/usr/bin/rocola`, sin cambios) sube el volumen de la tarjeta nueva (`volumen`, con `rocola-tarjeta -c`) y la abre otra vez con
    `ROCOLA_ALSA=$(rocola-tarjeta)`. Si la elegida da en la que ya suena, solo se guarda (sin reabrir ni preguntar).
  - **Al volver**, a los 3 s (desde un reloj propio, no dentro del latido), «¿Se oye bien?» con «No (vuelve en N s)» (el foco: Intro o
    Escape sin mirar = no) y «Sí, se oye bien», 15 s por el reloj. Con «Sí» se borra la prueba y queda la nueva. Con «No» o sin
    respuesta: se escribe otra vez la de antes, se borra la prueba y se reabre igual (conservando el estado). Si se cierra por otra cosa
    mientras pregunta, la prueba sigue en el archivo y pregunta al volver.
  - **Apagones y caídas**: la prueba está en el disco; cada arranque la cuenta. Un apagón a media prueba la vuelve a preguntar al prender;
    al 3.er arranque sin contestar (la rocola se cae con la salida nueva) vuelve a la de antes sin preguntar y se reabre.
  - **Probar** (opcional del encargo, hecho porque no pelea con lo que suena): solo por una salida de **otra tarjeta** que la que suena
    (la misma tarjeta no se abre dos veces); como `rocola-sonido`, le quita el silencio con `amixer` y `timeout 3 speaker-test -D plughw:T,S
    -t sine -f 440 -c 2`, en su hilo; no cambia nada. Con la de la misma tarjeta, el botón va en gris y lo dice. `ROCOLA_TONO` lo cambia
    en las pruebas.
- Ningún guion del sistema cambia (`rocola-tarjeta`, `rocola-sonido` y el lanzador ya leían y atendían todo esto). Nada de `Codigo/` ni
  ganchos nuevos (las hojitas y las funciones se registran solas).
- Pruebas que cuentan los Tab de Ctrl+F4, con una parada más por la pestaña nueva: `Pruebas/RocolaOs/pantalla/prueba.sh` (teclado) y
  `sistema/rocola/Pruebas/Qemu/pantalla.py` (esta, sin correr aquí).

## La prueba

`bash Pruebas/RocolaOs/salida-audio/prueba.sh PROGRAMA` (JIT o `aot:CARPETA`), con el KMS simulado de la hoja Pantalla, sin sonido
(`ROCOLA_ALSA=null`) y un `/proc/asound` simulado como el del A4 (tarjeta 0 «HDA ATI HDMI» con su salida 3 y su ELD; tarjeta 1
«HD-Audio Generic», Realtek ALC892, salidas 0 analógica y 1 digital, y una de captura que no cuenta). Hace de lanzador: tras cada cierre
para reabrirse, la vuelve a abrir con los mismos datos. Partes: `lista`, `elige`/`vuelta` (elegir el HDMI, reabrir conservando los
créditos, nadie contesta: a los 15 s vuelve a la automática y se reabre con los mismos créditos), `confirma` (Tab e Intro: «Sí»; el
siguiente arranque ya no pregunta), `auto`, `probar` (un pitido por `plughw:0,3`; por `plughw:1,1`, de la misma tarjeta, no), `caidas`
(3 arranques sin contestar; y un `kill -9` a media prueba) y `sin` (sin `ROCOLA_OS` nada se lee ni se toca).

## Pendiente

- Nada probado en el A4 ni con una tarjeta de verdad (ni el pitido con `speaker-test`, ni que el lanzador la reabra en el sistema).
- Si no está sonando nada al volver, «¿Se oye bien?» no se puede juzgar en 15 s: lo seguro es contestar «No» (vuelve) y elegirla con una
  canción sonando.
- `ROCOLA_ALSA` puesta desde fuera del lanzador (para probar) manda sobre lo elegido.
