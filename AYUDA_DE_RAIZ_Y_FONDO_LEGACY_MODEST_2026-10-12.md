# La ayuda revisada de raíz, y un solo fondo: Legacy — Modest, 12 de octubre de 2026

Petición del usuario: «Sigue estando esa falla, después de un rato de estar la rocola, ya no se puede usar el
asistente, revisa cómo está hecho el asistente para que resuelvas todos los problemas de raíz. También quiero que
borres el fondo de espacio natural, en sí me sigue gustando más cómo se ve Legacy, y creo que deberíamos enfocar
mejoras a Legacy en vez de crear un fondo secundario».

## 1. La ayuda

Se abre con **ROJO + ABAJO** o tocando la casilla **AYUDA**, al lado de BUSCAR. Se guarda igual.

### Por qué «después de un rato» dejaba de funcionar

Se revisó el camino completo, desde la tecla hasta la estrella. Había cinco causas:

1. **Con un video sonando, la casilla AYUDA desaparecía.** BUSCAR también. Con video, letras, encabezado y discos
   se encogen a la mitad izquierda, y ahí las dos casillas no cabían: se escondían mientras durara el video. En el
   bar casi siempre suena uno. Además, a los 8 s sin tocar nada el video pasa a pantalla completa, y la primera
   tecla solo lo saca de ahí.
2. **Tres cosas la negaban sin avisar:**
   - que la ventana no tuviera el foco del teclado;
   - que se estuviera leyendo la biblioteca (al arrancar con muchos archivos, o al volver un disco);
   - que verde y rojo fueran la misma tecla, un resto del acorde viejo.

   En los tres casos ROJO + ABAJO no hacía nada y no salía ningún mensaje.
3. **El orden de los botones importaba.** Solo valía ABAJO con el ROJO ya apretado. Si al apretar los dos a la vez
   llegaba antes el ABAJO, no pasaba nada.
4. **Para saber si el otro botón seguía apretado, solo se miraba lo que la rocola ya había leído.** Si ese mensaje
   se lo quedaba otro (el video, por ejemplo), el ROJO no constaba como apretado.
5. **El regreso de la estrella le preguntaba a la tarjeta gráfica.** Desde ayer tenía un tope de 1,5 s, pero seguía
   dependiendo de ella.

### Lo que se cambió

1. **Con video, BUSCAR y AYUDA van encima del video**, centradas. Arriba del video queda libre una franja del alto
   de la fila. Así se ven siempre:
   - en 1024×768: BUSCAR de 557 a 760 px y AYUDA de 770 a 964 px;
   - en 800×600, AYUDA va compacta (el signo y sus dos botones).

   Solo con tarjeta gráfica. Sin ella (dibujo por CPU, congelado) todo sigue como antes.
2. **Solo la niegan los bloqueos de verdad:**
   - sin tarjeta gráfica;
   - cerrando la rocola;
   - arrancando, mientras se leen las tablas;
   - configurando botones;
   - en mantenimiento (F10);
   - con una ventana de ajustes abierta.
3. **Si no se puede, lo dice en la pantalla:** «La ayuda no está disponible ahora: …» y el motivo. Vale también al
   tocar la casilla. Lo apunta en `rocola_error.txt`, como mucho 20 veces por sesión.
4. **El orden ya no importa:** sirve ROJO con ABAJO apretado y ABAJO con ROJO apretado.
5. **El otro botón cuenta como apretado de dos maneras:** si la rocola ya leyó su mensaje, o si está apretado en el
   teclado en ese momento, aunque el mensaje se lo haya quedado otro. Las pistas del rojo mantenido usan la misma
   regla.
6. **El regreso lo cuenta solo la rocola, con su reloj:** 0,8 s, lo que dura el salto (0,72 s). La tarjeta solo
   anima, y se quitó el código que le preguntaba.

No cambian la estrella (sigue sin rostro), la guía paso a paso ni lo que hacen los demás botones.

## 2. Un solo fondo: Legacy

Se borró **Espacio natural** entero:

- **El código:** 9 archivos solo de Natural y sus ramas en el motor del cielo:
  - estrellas, fugaces, nebulosas, vía láctea y aurora;
  - estrella, luna y ecos;
  - halos, cristal de la lista, grano y viñeta.

  En total, unas 2.100 líneas menos.
- **Ctrl+F3 → Fondo:**
  - se quitó el selector «Legacy / Espacio natural»;
  - se quitaron los tres interruptores de Natural: aurora con colores reales, luna con su fase real y cielo según la
    hora;
  - la hoja usa ahora el mismo marco que las demás páginas, con sus tres pestañas arriba: Cielo y aurora, Interfaz y
    Estrella.
- **La configuración:**
  - `estilo_fondo=`, `aurora_real=`, `luna_fase=` y `cielo_hora=` se ignoran y no se vuelven a escribir;
  - si la rocola estaba en Natural, arranca en Legacy;
  - los demás ajustes se conservan.
- **Las pruebas:**
  - se borraron 9 baterías de Natural;
  - `estilosconfig` pasa a ser `hojasfondo`;
  - las que probaban «Legacy y Natural» prueban ahora solo Legacy.

**Lo que se queda**, aunque lleve «natural» en el nombre: «Nebulosas con detalle natural» y «Fuerza del detalle
natural». Son de Legacy, de septiembre. Su tarjeta, antes «DETALLE NATURAL», ahora se llama «DETALLE DE LAS
NEBULOSAS». También se queda la mejora de rendimiento de los ecos de la estrella 3D, que es de Legacy.

### Legacy no cambia

- **La huella de Legacy da los mismos números que la versión publicada.** Cubre:
  - vía láctea, nebulosas y viñeta;
  - estrella con y sin detalle, luna y atlas de estrellas;
  - grano, cristal de la lista y tintes;
  - malla y brillo de la aurora.
- **Lo que Natural había reordenado vuelve al código de Legacy de antes del 1 de octubre:** la aurora, las nebulosas
  al cambiar de tamaño, el cielo, las fugaces y la luna.
- **El dibujo por CPU no cambió:** sus 14 métodos congelados son idénticos en los dos perfiles.

De aquí en adelante, las mejoras del fondo se hacen sobre Legacy (regla nueva en `AGENTS.md`).

## 3. Cómo se comprobó

- **Pruebas automáticas (Linux/Mono, sin Direct3D):**
  - `asistentecombo` (121 comprobaciones): el atajo en los dos órdenes, los motivos, el aviso y el regreso sin
    tarjeta;
  - `filabaja` (184 en GPU, 50 en CPU): BUSCAR y AYUDA encima del video en 7 resoluciones, y sin tarjeta como antes;
  - `hojasfondo` (1.360): la hoja Fondo, las claves viejas ignoradas y la ausencia de Natural;
  - siguen pasando `asistentevisual`, `guiarojo`, `ayudaguia`, `entrar`, `buscaportadas`, `cristalcola`,
    `estrella3d`, `estrellaflujo`, `ecosgeometria`, `campoestelarbuffers`, `ruidomalla`, `bloqueos`, `luna`, `fondo`
    y `espacioprecarga`;
  - tanda completa (`pruebas/todo.sh`): 26 problemas, todos de antes de estos cambios (las mismas baterías que ya
    fallaban en este Linux); ninguno nuevo. Antes eran 30: tres eran baterías de Natural, ya borradas, y
    `paginasgpu` ahora pasa. `hojasfondo`, `asistentecombo` y `filabaja` pasan en los dos perfiles.
- **Comprobación visual:** foto real de Ctrl+F3 → Fondo (Xvfb) y dibujo de la fila de arriba con video.
- **No comprobado:**
  - en el bar, con video real y los botones de la rocola;
  - en una tarjeta gráfica real.

  Si alguna vez la ayuda no se abre, el aviso en pantalla dice por qué.
