# La estrella te ayuda — Modest

Mantener los botones rojo y verde juntos durante tres segundos abre la ayuda. Usa las teclas configuradas de la Rocola y sus alternativas existentes; no depende de letras de teclado fijas. La estrella del fondo salta al frente, se orienta hacia el usuario y despliega brazos y piernas negros. Habla mediante un globo redondeado con una cola que apunta hacia ella, no mediante una tarjeta separada. Al cerrar, regresa al cielo sin dejar otra estrella ni ecos antiguos detrás.

La pregunta inicial es «¿Qué necesitas?»:

- «Cómo usar la Rocola»: tres pasos sobre discos, canciones, letras, búsqueda, créditos y botón rojo.
- «Ayúdame a poner una canción»: permite abrir el buscador existente o volver a explorar los discos. Si ya había una búsqueda, conserva su texto y resultados.

Las flechas eligen entre las dos opciones; verde acepta y rojo vuelve al inicio o cierra la ayuda. También se pueden pulsar las opciones con el ratón. La ayuda se cierra tras 90 segundos sin interacción. Es una guía local, sin servicios externos, voz ni un modelo conversacional nuevo.

La combinación no añade canciones ni gasta créditos. Exige liberar ambos botones antes de rearmarse; una suelta parcial o perder el foco cancela la cuenta. Los toques normales de rojo/verde tienen una breve ventana de reconocimiento de 120 ms; al soltar se ejecutan inmediatamente, sin unir dos toques rápidos. La moneda, saltar canción y los atajos de mantenimiento siguen disponibles. No se activa durante arranque, configuración de botones, mantenimiento ni reconstrucción de biblioteca.

La música continúa mientras se usa la ayuda. Con vídeo, se oculta temporalmente su cuadro, no su reproducción; se restaura cuando termina el regreso de la estrella, salvo que se haya abierto el buscador. Se evita que las decisiones de inactividad cambien de pantalla mientras la persona está leyendo la guía.

Opción visual: **Ctrl+F3 → Pantalla → La atracción y el estilo → Buscador y ayuda → Animación del asistente (GPU)**. Se guarda como `asistente_animado=1/0`, inicialmente activa. Apagarla quita el salto, los gestos y el fundido del mensaje; conserva la estrella sin rostro, el globo y la ayuda. La estrella de la ayuda funciona aunque la estrella del fondo esté apagada, sin modificar esa preferencia. Legacy y Natural, estrella 2D y 3D usan sus materiales existentes.

## Globo y personalidad

Por petición del usuario, la estrella **no tiene rostro**: no lleva ojos, cejas, boca ni una placa facial. Conserva el diseño del icono y transmite expresiones exclusivamente mediante lenguaje corporal. Los antebrazos se dibujan delante del cuerpo y las piernas/brazos superiores detrás, de modo que los gestos recogidos se vean sin duplicar los miembros ni ocultar el material central.

- Saluda con una mano alta, inclina el cuerpo y hace un pequeño rebote de llegada.
- Al explicar los pasos, alterna ambas manos abiertas e inclina el cuerpo hacia el globo, sin voz ni cambios de audio.
- Al ofrecer la búsqueda, recoge los brazos, junta ligeramente los pies y se inclina con curiosidad; después abre una mano para invitar a elegir.
- Señala la opción elegida y se orienta hacia ella al mover flechas o ratón. Mantener la misma selección no reinicia el gesto.
- Confirma con brazos abiertos, rebote y un pequeño gesto de aprobación del cuerpo. Para volver, señala hacia el otro lado y mueve un pie.
- Celebra una moneda real levantando ambos brazos y dando un saltito, sin repetir la celebración ni sumar créditos por la autorrepetición del botón.
- Mientras espera, respira y se balancea suavemente; ocasionalmente mueve los pies. Se despide con la mano al cerrar.

Los cambios de gesto parten de la expresión actual y se mezclan durante 140 ms, aunque se cambie de opción rápidamente. Los mensajes siguen legibles y las opciones no se desplazan durante su fundido, también al avanzar entre pasos de la guía. El contenido se muestra completo, sin obligar a esperar una escritura letra a letra. En 4K/ultrapanorámico, el globo tiene un tamaño máximo y la estrella se coloca junto a él para evitar una caja enorme o un hueco excesivo.

## Recursos y separación

Solo GPU. CPU congelada y HD suspendida. Se conserva .NET 3.5 y el destino XP, sin certificar ejecución física en XP.

La entrada aprovecha el temporizador existente; la animación usa el reloj fino del cuadro, no ese temporizador. No añade hilos ni relojes independientes. El mensaje se compone una vez por tamaño/página/paso/selección; la animación no rehace ni sube la imagen en cada cuadro. Los miembros usan dos mallas reutilizables, menos de 72 KiB en total (1900 + 700 vértices), sin crear arreglos por cuadro. Se elimina por completo la malla facial de 2200 vértices: la ayuda reserva unos 60 KiB menos y envía menos geometría que la versión con rostro. No se añade trabajo al fondo cuando se usa normalmente la Rocola. El formulario es dueño del bitmap y la GPU solo de su textura, liberada al cerrar la ayuda o perder el dispositivo.

## Verificación

- Release y compiladores nativos .NET 3.5 GPU/CPU con advertencias tratadas como errores.
- `pruebas/asistentecombo.cs`: límite de tres segundos, ambos órdenes de pulsación, repetición, liberación, foco/permiso, vuelta del contador de tiempo y distintas cadencias.
- `pruebas/asistentevisual.cs`: todos los mensajes, opciones y miembros entre 640×480 y 3840×2160; trayecto a 24/50/60/144/240 Hz, nueve estados corporales distintos, interrupción de gestos, relojes inválidos, redimensionado, regreso, animación desactivada, reutilización y liberación. Comprueba que no exista código/buffer facial y que los gestos no recorten los miembros ni tapen opciones. Prueba opcional HAL con capturas reales a 640×480 y 1024×768 en Legacy/Natural, 2D/3D, incluso con estrella de fondo desactivada. Incluye capturas de siete acciones y compara píxeles del centro con el mismo material original para detectar rasgos añadidos.
- Persistencia de la opción en ambos perfiles y geometría de los ajustes existentes.
- Comparación de 14 métodos del renderizador CPU contra el ejecutable anterior: instrucciones y operandos resueltos idénticos.

La batería visual también informa p95/p99/máximo del coste CPU sintético del reloj fino y las dos mallas de miembros en Legacy/Natural, 2D/3D, sin reflexiones ni reservas en el bucle medido. No incluye el dibujo del fondo, el material 3D, transferencias ni Present: no equivale a FPS reales. Comprobar que no hubo colecciones GC en ese bucle no atribuye pausas externas al recolector.

La regresión de Windows ha pasado sus 71 baterías con `-SinHal`; la batería HAL específica del asistente se comprueba por separado. La comparación del renderizador CPU sigue verificando 14 métodos idénticos al ejecutable de referencia.

Las capturas de GPU son una escena de prueba aislada, no una sesión de la Rocola con biblioteca y música. Estas comprobaciones no son una medición de FPS de producción ni una prueba física en XP. Queda por confirmar la combinación con los botones reales y el comportamiento del vídeo en una sesión completa. La instalación local se mantiene pospuesta por petición del usuario.

Para repetir las baterías desde el repositorio en Windows:

```powershell
.\pruebas\validar-capas.ps1 -SinHal
```

Sin `-SinHal`, el corredor también solicita las pruebas visuales HAL. No ejecutar compilación ni pruebas HAL en paralelo con una comparación de FPS de producción.
