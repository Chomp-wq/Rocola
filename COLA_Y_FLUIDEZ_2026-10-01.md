# Cola compacta y fluidez visual — 1 de octubre de 2026

## Cambios en HD y Modest

- Una o dos canciones en «A continuación» ya no estiran sus tarjetas en resoluciones bajas. El límite de 520 × 79 píxeles también se aplica allí, sin aumentar las tarjetas de las colas largas.
- Miniatura, título y artista/tiempo de espera quedan centrados como un bloque. El índice de cola en tarjetas estrechas queda dentro de la miniatura y no invade el título.
- En paneles estrechos y altos, la cola de doce utiliza dos columnas en vez de tres demasiado estrechas. Se mantienen hasta doce entradas y el desplazamiento de textos largos.
- Ctrl+F3 → Imagen → Fluidez ofrece dos ajustes independientes: **Ondas de sonido** y **Todos los cubos**, cada uno con **24, 50 o 60 FPS**. La hoja Efectos conserva su espacio; no se agrandó la ventana.
- Se corrigieron etiquetas recortadas en Efectos. Los botones deshabilitados no se activan por teclado y Enter/Espacio no producen el sonido de sistema al activarlos.

## Qué controla la fluidez

De fábrica: **60 / 60**. Las claves persistentes son `anim_ondas_fps` y `anim_cubos_fps`; configuraciones anteriores o valores inválidos utilizan 60. «Gráficos como de fábrica» restaura ambos controles y repinta sus selecciones.

Las ondas actualizan sus bandas, forma y colores con su reloj visual. Su base conserva la posición actual de la barra de estado en cada cuadro: reducir su cadencia no separa las ondas de la barra durante la expansión.

Los cubos usan un reloj GPU de alta resolución en lugar de depender del temporizador de controles, de aproximadamente 24 actualizaciones por segundo. Se cubren:

- selección y cubo en relevo del muro;
- cubo dentro de un disco;
- mini-cubo de la barra normal y extendida;
- vista previa del buscador cuando utiliza cubo;
- cubo del modo de atracción;
- vuelo de portada al entrar/salir y otras transiciones entre cubos.

La velocidad de giro no cambia con el ajuste. El relevo de portada y su pose avanzan juntos, sin intercambiar la imagen entre dos muestras. Una selección nueva responde inmediatamente. El desplazamiento de páginas y la colocación/expansión de controles siguen el reloj general para no demorar el primer toque.

Son objetivos visuales: el equipo, la carga y la frecuencia real de renderizado pueden ofrecer menos cuadros. **No son un límite general de FPS**, ni cambian la calidad de las imágenes, carga de carátulas, análisis o calidad del audio, respuesta de entrada, estrella reactiva ni fluidez del progreso.

El renderizador CPU permanece congelado; las preferencias existen en código común pero no alteran su dibujo.

## Verificación de esta pasada

- HD y Modest: compilación GPU y CPU con advertencias como errores.
- 37 baterías por rama, incluidas las pruebas de composición Direct3D HAL en este PC.
- `colacompacta.cs`: 66.932 comprobaciones; 1.430 rejillas de 0 a 12 canciones y 2.860 paneles con/sin espera. Se inspeccionaron imágenes de prueba, incluida una canción a 1024 × 220.
- `ajustesfluidez.cs`: 2.095 comprobaciones por combinación de rama/renderizador; nueve combinaciones, guardado, carga, valores inválidos, restablecimiento y controles de teclado.
- `cadenciavisual.cs`: 13.431 comprobaciones de relojes 24/50/60 a distintas frecuencias de render, cuadros perdidos, respuesta inmediata, tres ranuras de cubos, vuelos, bandas propias, unión de ondas a barra y cambio atómico de portada/pose.
- Los 14 métodos de dibujo CPU comprobados por `cpucongelado.cs` mantienen instrucciones, operandos, locales y excepciones idénticos a las compilaciones de la pasada anterior.
- Compilaciones Release: HD .NET 4.8 y Modest .NET 3.5. La compilación antigua avisa que no encuentra Windows SDK 6.0A; compila correctamente.

Estas verificaciones **no** sustituyen una medición de FPS con música y la biblioteca completa ni una prueba real en Windows XP. No se promete ausencia absoluta de tirones.

## Archivos principales

`EstadoReposoGpu.cs`, `FluidezAnimaciones.cs`, `FluidezGpu.cs`, `D3D9Star.cs`, las conexiones de `Form1`, `Rendimiento.cs`, `Form1.Datos.cs` y las tres pruebas nuevas. Ambas listas de compilación y los runners de pruebas incluyen los archivos nuevos.
