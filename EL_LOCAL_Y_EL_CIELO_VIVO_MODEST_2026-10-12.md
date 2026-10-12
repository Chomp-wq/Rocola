# El local y sus bocinas, y el cielo vivo de Legacy — Modest, 12 de octubre de 2026

Petición del usuario, después de ver las propuestas: «De sonido y fondo hazlo todo; de rendimiento, la verdad es que
ya el rendimiento es muy bueno… y en todo lo que hagas ahora asegúrate de que todo esté bien, que no haya ninguna clase
de error».

Se hicieron las cuatro propuestas de sonido (S1 a S4) y las siete del fondo (F1 a F7). Rendimiento no se tocó.

## 1. El local y sus bocinas

**Dónde:** Ctrl+F3 → Sonido → botón **«El local y sus bocinas…»**, donde antes estaban «Quitar el subgrave» y
«Estéreo al 60 %». Abre una hoja con dos tarjetas en el sitio de las dos de arriba; «Volver a las canciones» regresa.
Esos dos interruptores se mudaron a la hoja nueva: hacen lo mismo y se guardan igual.

### Los ajustes (tarjeta «El local y sus bocinas»)

| Mando | De fábrica | Qué hace | Clave |
| --- | --- | --- | --- |
| Quitar el subgrave | apagado | Igual que antes (30 Hz). | `corta_subgrave=` |
| Estéreo al 60 % | apagado | Igual que antes. | `estereo_estrecho=` |
| **Claridad para el local** | apagado | −2,5 dB alrededor de 300 Hz (el retumbe de la lámina, el ladrillo y la loseta) y +1,5 dB alrededor de 3 kHz (la voz). El bajo (60 Hz) y los agudos altos casi no cambian. | `claridad_local=` |
| **Que las partes suaves se oigan** | apagado | Lo que va por debajo del nivel de siempre sube, como mucho 4 dB, despacio. Lo fuerte no se toca, y la subida se suelta en cuanto vuelve. El silencio y lo casi mudo no suben. | `partes_suaves=` |
| **Balance** | centrado | Baja hasta 6 dB el canal que suena de más. Solo baja: nunca recorta. | `balance=` (−6 a 6) |

- **Se oyen al instante** en la canción que suena y en la siguiente ya preparada, sin chasquidos: la claridad entra y
  sale en 30 ms y el balance se mueve en 50 ms.
- **Apagados no tocan ni una muestra:** el sonido sale bit a bit como antes.
- **El volumen general y el modo noche no cuentan** para las partes suaves: bajar el volumen de madrugada no hace que
  suban más.
- **Pistas seguidas del mismo disco:** siguen con los filtros de la anterior, como el grave en mono.
- **«Dejar como de fábrica»** los apaga y centra el balance.

### La prueba de bocinas (tarjeta «Prueba de bocinas»)

- **Botones:** «Solo izquierda», «Solo derecha» y «Las dos». Suena 3 s de ruido rosa suave (−20 dB), que reparte la
  energía como la música.
  - Sirve para igualar las perillas IN A e IN B del amplificador: en las fotos no estaban a la misma altura.
  - Sirve también para revisar que cada bocina suene.
- **«Polaridad»:** 8 s de ruido grave por las dos bocinas, primero igual y después con una al revés, dos veces.
  - Párate entre dos bocinas.
  - Si «al revés» se oye **con más** grave que «igual», una de las dos está conectada al revés (rojo con negro).
  - La ventana dice en cada momento qué tramo suena.
- Sale por su propia salida, sin pasar por la cadena de la música. Solo funciona **con la rocola callada**; si algo
  suena, lo dice.
- Se para al pedir otra prueba y al cerrar Ctrl+F3.

Coste: con los tres ajustes encendidos, 5,2 ms por segundo de música en este equipo (incluida la copia de la prueba).
Leer no reserva memoria y en el silencio los filtros quedan en cero (sin números desnormales).

## 2. El cielo vivo de Legacy

**Dónde:** Ctrl+F3 → Fondo → hoja nueva **«Cielo vivo»** (la cuarta, al lado de Cielo y aurora, Interfaz y Estrella).

**De fábrica:** los siete están encendidos, como los demás adornos del fondo. «Fondo como de fábrica» los vuelve a
encender, y cada uno se apaga por separado.

**Alcance:** solo con tarjeta gráfica. El dibujo por CPU no cambia.

| Interruptor | Qué se ve | Clave |
| --- | --- | --- |
| **Luna con su fase real** | Arriba a la derecha, con la fase de la fecha (±1 día), los mares, el borde algo más oscuro y un halo según lo iluminada que esté. La parte sin sol es un azul gris apenas más claro que el cielo, y tapa las estrellas de detrás. Cerca de la luna nueva no se ve. Con la pared llena asoma entre las portadas; se luce en reposo y en la atracción. | `luna_del_cielo=` |
| **Lluvias de estrellas en sus fechas** | Cuadrántidas (3 ene), Líridas (22 abr), Eta Acuáridas (6 may), Perseidas (12 ago), Oriónidas (21 oct), Leónidas (17 nov) y Gemínidas (14 dic). Esos días parte de las fugaces nace a media altura, se aleja de un mismo punto y entra suave. Cerca del máximo salen algo más seguido. | `lluvias_estrellas=` |
| **Fugaz al poner una canción** | Al meter una moneda o poner una canción cruza una fugaz dorada, como mucho una cada 2,5 s. | `fugaz_festejo=` |
| **Cometa de vez en cuando** | El primero a los 6-15 minutos de encender; después, cada 35-70. Cruza el cielo en dos minutos y medio, con su cola blanca de polvo y la azul de iones, y entra y sale suave. | `cometa=` |
| **Más profundidad al pasar de página** | Las nebulosas se corren un 30 % de lo que se corren las estrellas grandes, y la vía láctea un 14 %; después vuelven despacio. Nunca salen de su margen, así que el borde no asoma y no cambian de tamaño. Va con «Pasar de página deslizando» (Pantalla). | `profundidad_pagina=` |
| **Cielo según la hora** | De madrugada, el cielo de abajo un poco más oscuro. Hacia las 19:30, un tono cálido abajo (y algo al amanecer). El cielo alto no cambia. | `cielo_por_hora=` |
| **Reflejo de la aurora** | Bajo la raya del horizonte, el brillo de los picos se refleja muy tenue (como mucho 16 px) y se apaga antes del texto de la franja. | `reflejo_aurora=` |

Las claves viejas de Espacio natural (`luna_fase=`, `cielo_hora=`…) siguen ignoradas: estas son otras.

### Coste

- **Sin pasadas nuevas de pantalla completa,** sin memoria por cuadro y sin hornear al dibujar.
- **Por cuadro:**
  - la luna: un cuadro con textura y un halo;
  - el reflejo: una llamada (288 triángulos);
  - el cometa: dos llamadas, solo mientras pasa.
- **Sin coste nuevo:** las lluvias, el festejo, la hora y la profundidad usan las mismas fugaces, el mismo cielo y las
  mismas capas.
- **La luna se hornea fuera del cuadro:** una textura de 128 px, en 4 ms, cada ~6 h. Se suelta y se rehace al
  reiniciar la tarjeta.

## Cómo se comprobó

- **Pruebas automáticas nuevas:**
  - `salalocal` (67 comprobaciones), con tonos por la etapa de verdad:
    - apagado bit a bit;
    - la curva de la claridad;
    - la subida de las partes suaves (4 dB, despacio, sin el Master, el silencio no);
    - el balance;
    - encender y mover sonando sin golpe;
    - pedir de a una muestra sin cruzar canales;
    - el disco continuo, sin desnormales ni memoria;
    - las cuatro pruebas de bocinas (nivel, lados, polaridad, sin recortar, sin chasquido), con la rocola callada,
      por su salida, y que se para;
    - la cadena, la configuración y la ventana.

    Esta prueba encontró un fallo antes de publicar: con el balance movido, la etapa se creía quieta y no lo
    aplicaba. Ya está arreglado.
  - `cielovivo` (79 comprobaciones), las cuentas sin tarjeta:
    - la fase de fechas conocidas (luna nueva el 10-10-2026, llena el 26-10-2026) y el horneado por fases;
    - las fechas de las lluvias, también la que cruza el año;
    - el radiante, el festejo, el cometa, el margen de las nebulosas, la hora y el reflejo;
    - que todo esté conectado al dibujo, a la configuración y a Ctrl+F3.
  - `ventanaf3` (3.087 comprobaciones):
    - recorre la hoja del local y la del cielo vivo;
    - ahora detecta también un letrero que pisa un mando. Así se encontró y arregló el botón «El local y sus
      bocinas…», al que la nota del DSEE le tapaba el borde.
- **Siguen pasando:**
  - de sonido: `gravemono` (ajustada para la etapa nueva en la cadena), `subgrave`, `todojunto`, `sinpausa`,
    `silencios`, `noche`, `transiciones`, `salida`, `videoaudio`, `audioestereo` y `ajustes`;
  - del fondo: `hojasfondo`, `fondo`, `fondocoste`, `cielohondo`, `fugaces`, `luna`, `nebulosa`, `vineta`,
    `aurorafina`, `dibujoligero`, `respira`, `mando`, `presencia`, `anim`, `textosgpu`, `envios`, `pedidas`,
    `reposounido`, `panelreposo`, `cielo`, `estrellas`, `pared` y `deriva`.
- **Legacy y CPU:**
  - la huella de Legacy es idéntica: con los adornos el cielo de siempre sale igual;
  - los 14 métodos congelados del dibujo por CPU son idénticos en los dos perfiles.
- **Comprobación visual:**
  - fotos de Ctrl+F3 (la hoja del local y la del cielo vivo);
  - vista previa por software de la luna en cuatro fases, el cometa, la fugaz de festejo, las Perseidas y el cielo a
    mediodía, al anochecer y de madrugada. Con ella se suavizaron los mares de la luna y su parte apagada.

  No es la tarjeta gráfica: aproxima la mezcla de colores.
- **No comprobado:**
  - oírlo en el billar;
  - verlo en la tarjeta gráfica del bar;
  - la prueba de bocinas con un amplificador de verdad.

  Para la polaridad y el balance, lo que manda es el oído en el local.
