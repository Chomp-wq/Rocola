# Segunda tanda de fallos para la versión final — Modest, 12 de octubre de 2026

Petición del usuario: «Haz otra tanda buscando más fallos, estamos preparando la rocola para su final release».

Esta vez se buscó lo que pasa **después de muchos días encendida** y en los **casos raros de uso**, que el barrido
anterior (pantallas y textos) no cubría.

## Lo que se arregló

### 1. El reloj de Windows que se atrasa

Las cuentas de «cuánto hace que nadie toca nada» restaban horas del reloj de Windows. Si el reloj se atrasa, la
última tecla queda «en el futuro» y la cuenta da negativo durante todo ese rato. Pasa con:

- **El cambio de horario.** Windows XP no sabe que en México se quitó en 2022: si tiene marcado el ajuste automático,
  el último domingo de octubre (el 25 este año) atrasa una hora a las 2 de la mañana.
- Una sincronización de la hora, o alguien que la corrige a mano.

Durante esa hora no llegaban:

- la atracción;
- el video a pantalla completa;
- el cierre solo del buscador al minuto;
- el reinicio del día a las 7 de la mañana.

Ahora, si el reloj quedó detrás de la última tecla, se cuenta desde ese momento, como si se acabara de tocar.

### 2. A los 24,9 días encendida, dejaban de verse el aviso y el hojeo

El contador de milisegundos de Windows pasa a negativo a los 24,9 días de estar encendido y vuelve a cero a los 49,7.
Cuatro animaciones usaban −1 como «nada en marcha», pero miraban «menor que cero», así que en esos 25 días:

- **el aviso de siempre** («Pusiste…», «Inserta una moneda», «No se pudo reproducir…») no salía nunca;
- **las páginas de la pared** cambiaban de golpe, sin deslizarse;
- **la lista de canciones del disco y su barra de páginas**, igual.

Ahora −1 sigue siendo «nada», pero se compara exacto, y la hora de arranque nunca vale −1. Con la rocola apagándose
cada noche nunca pasaba; con la máquina encendida semanas, sí.

### 3. Los fallos se explican en español y en corto

Cuando una canción no se puede tocar (archivo dañado, borrado, o el disco desconectado), la rocola ya devolvía el
crédito y pasaba a la siguiente. Pero la línea de estado mostraba el mensaje técnico, en inglés y cortado por la
orilla:

> No se pudo reproducir 01 - Basura.mp3 (Got a frame at sample rate 48000, in an MP3 with sample rate 8000. Mp3FileReader does not support sample rate

Ahora dice el motivo:

| Qué pasó | Qué dice |
| --- | --- |
| El archivo o su carpeta ya no están | no se encuentra el archivo (¿se desconectó el disco?) |
| Archivo cortado a medias | el archivo está incompleto o dañado |
| Archivo dañado o de formato raro | el archivo está dañado o en un formato que no se puede tocar |
| Windows no deja leerlo | Windows no deja leer el archivo |
| Falla el disco al leer | no se pudo leer el archivo del disco |

Vale para las canciones, para la que se corta a media reproducción («Se cortó la canción: …») y para los videos.
El mensaje completo sigue en `rocola_error.txt`; el del video, que antes solo se veía en pantalla, ahora también
queda ahí.

## Lo que se probó y está bien

Con la rocola entera, una biblioteca de prueba y una salida de sonido de mentira:

- **Archivos dañados** (basura con nombre .mp3, un .wav vacío, uno cortado), **borrados** después de leer la
  biblioteca y **un disco cuya carpeta se movió**:
  - no se cobra el crédito;
  - la cola sigue;
  - la rocola no se traba ni se cierra.
- **Quitar la carpeta de toda la música con canciones en cola:** la que sonaba termina y las siguientes fallan sin
  cobrar. Al volver la carpeta, todo sigue funcionando.
- **Biblioteca vacía:** dice que no encontró música y aguanta flechas, moneda, buscador y atracción sin errores.
- **Teclas en estados raros:** 80 repeticiones seguidas y verde/rojo alternados muy rápido, sin errores.
- **Marcha larga:**
  - sin fugas de recursos de Windows: pinceles, letras, imágenes y temporizadores se sueltan;
  - todas las listas y cachés tienen tope;
  - los archivos de errores y de diagnóstico tienen tamaño máximo;
  - las miniaturas se podan solas.
- **Versión final:** ningún gancho de pruebas está activo de fábrica; el diagnóstico viene apagado.

Para tener en cuenta (es así a propósito, no es fallo): con nada sonando y la cola vacía, **SALTAR borra los
créditos**, para cerrar el negocio.

## Lo que no se tocó

- **Faltan acentos en textos de siempre**, como «Nada reproduciendose», «Creditos» y «LO MAS SONADO». Cambiarlos toca
  el tamaño de los letreros y decenas de pruebas. Se puede hacer aparte, comprobando cada pantalla.

## Cómo se comprobó

- **Prueba automática nueva `segundatanda`:** 44 comprobaciones con tarjeta gráfica y 38 sin ella.
  - Cubre el reloj que se atrasa y las cuatro animaciones ante la vuelta del contador.
  - Cubre los motivos de fallo, sin el mensaje en inglés.
  - Contra el ejecutable anterior falla en 11 puntos: detecta los tres fallos.
- **Siguen pasando:** `lineaestado`, `suenavivo`, `avisosvideo`, `salida`, `sinpausa`, `videoaudio`, `transiciones`,
  `hojea`, `pagina`, `pestanas`, `entrar`, `asistentecombo`, `filabaja` y `respira`. `duerme` falla igual que
  antes de estos cambios.
- **El dibujo por CPU no cambió:** sus 14 métodos congelados son idénticos en los dos perfiles.
- **Tanda completa (`pruebas/todo.sh`):** 27 problemas: los 26 de antes y `indice`, una medida de velocidad con
  varios núcleos. Falló porque la tanda corrió a la vez que otras pruebas; sola pasa (dos veces seguidas).
  `segundatanda` pasa en los dos perfiles. En la tanda siguiente, ya sin otras pruebas a la vez, `indice` pasó y quedaron los 26 de
  antes.
- **No comprobado:** en Windows XP de verdad, con el cambio de horario real o con 25 días encendida. Se comprobó la
  lógica.
