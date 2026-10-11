# La estrella te ayuda — Modest

Mantener los botones rojo y verde juntos durante tres segundos abre la ayuda. Usa las teclas configuradas de la Rocola y sus alternativas existentes; no depende de letras de teclado fijas. La estrella del fondo salta al frente, se orienta hacia el usuario y despliega brazos y piernas negros. Al cerrar, regresa al cielo sin dejar otra estrella ni ecos antiguos detrás.

La pregunta inicial es «¿Qué necesitas?»:

- «Cómo usar la Rocola»: tres pasos sobre discos, canciones, letras, búsqueda, créditos y botón rojo.
- «Ayúdame a poner una canción»: permite abrir el buscador existente o volver a explorar los discos. Si ya había una búsqueda, conserva su texto y resultados.

Las flechas eligen entre las dos opciones; verde acepta y rojo vuelve al inicio o cierra la ayuda. También se pueden pulsar las opciones con el ratón. La ayuda se cierra tras 90 segundos sin interacción. Es una guía local, sin servicios externos, voz ni un modelo conversacional nuevo.

La combinación no añade canciones ni gasta créditos. Exige liberar ambos botones antes de rearmarse; una suelta parcial o perder el foco cancela la cuenta. Los toques normales de rojo/verde tienen una breve ventana de reconocimiento de 120 ms; al soltar se ejecutan inmediatamente, sin unir dos toques rápidos. La moneda, saltar canción y los atajos de mantenimiento siguen disponibles. No se activa durante arranque, configuración de botones, mantenimiento ni reconstrucción de biblioteca.

La música continúa mientras se usa la ayuda. Con vídeo, se oculta temporalmente su cuadro, no su reproducción; se restaura cuando termina el regreso de la estrella, salvo que se haya abierto el buscador. Se evita que las decisiones de inactividad cambien de pantalla mientras la persona está leyendo la guía.

Opción visual: **Ctrl+F3 → Pantalla → La atracción y el estilo → Buscador y ayuda → Animación del asistente (GPU)**. Se guarda como `asistente_animado=1/0`, inicialmente activa. Apagarla quita el salto y los gestos; no desactiva la ayuda. La estrella de la ayuda funciona aunque la estrella del fondo esté apagada, sin modificar esa preferencia. Legacy y Natural, estrella 2D y 3D usan sus materiales existentes.

## Recursos y separación

Solo GPU. CPU congelada y HD suspendida. Se conserva .NET 3.5 y el destino XP, sin certificar ejecución física en XP.

La entrada aprovecha el temporizador existente; la animación usa el reloj fino del cuadro, no ese temporizador. No añade hilos ni relojes independientes. El mensaje se compone una vez por tamaño/página/paso/selección; la animación no rehace ni sube la imagen en cada cuadro. Los miembros se dibujan en una malla reutilizable de menos de 72 KiB. El formulario es dueño del bitmap y la GPU solo de su textura, liberada al cerrar la ayuda o perder el dispositivo.

## Verificación

- Release y compiladores nativos .NET 3.5 GPU/CPU con advertencias tratadas como errores.
- `pruebas/asistentecombo.cs`: límite de tres segundos, ambos órdenes de pulsación, repetición, liberación, foco/permiso, vuelta del contador de tiempo y distintas cadencias.
- `pruebas/asistentevisual.cs`: todos los mensajes, opciones y miembros entre 640×480 y 3840×2160; trayecto a 24/50/60/144/240 Hz, redimensionado, regreso, animación desactivada, reutilización y liberación. Prueba opcional HAL con capturas reales a 640×480 y 1024×768 en Legacy/Natural, 2D/3D, incluso con estrella de fondo desactivada.
- Persistencia de la opción en ambos perfiles y geometría de los ajustes existentes.
- Comparación de 14 métodos del renderizador CPU contra el ejecutable anterior: instrucciones y operandos resueltos idénticos.

Las 71 baterías del corredor de Windows pasan con `-SinHal`; la prueba HAL específica del asistente se ejecuta por separado y también pasa. Las capturas de GPU son una escena de prueba aislada, no una sesión de la Rocola con biblioteca y música. Estas comprobaciones no son una medición de FPS de producción ni una prueba física en XP. Queda por confirmar la combinación con los botones reales y el comportamiento del vídeo en una sesión completa.

Para repetir las baterías desde el repositorio en Windows:

```powershell
.\pruebas\validar-capas.ps1 -SinHal
```

Sin `-SinHal`, el corredor también solicita las pruebas visuales HAL. No ejecutar compilación ni pruebas HAL en paralelo con una comparación de FPS de producción.
