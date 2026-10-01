# Páginas grandes y barra compacta — 24 de septiembre de 2026

Aplicado a Modest (.NET 3.5) y HD (.NET 4.8). Solo cambia el dibujo GPU; el renderizador CPU permanece congelado.

## Cambio de página dentro del disco

El modo de capas del vidrio se desactivaba mientras la lista se desplazaba. Además, el reloj de la lista y el del indicador de páginas formaban parte de la firma del dibujo. Eso obligaba a componer y subir repetidamente una imagen grande durante los 230 ms de transición, especialmente costosa con filas grandes y pantallas anchas.

Ahora el vidrio permanece fijo y la GPU desplaza y recorta las texturas de las dos páginas. Se aprovecha el cuarto espacio de textura ya existente para la página saliente. La selección conserva su propia capa y la página base omite esa fila para no duplicar sus letras. La página saliente se libera al terminar; no se precargan todas las páginas ni se agregan trabajadores.

La página nueva aún necesita dibujarse y subirse una vez cuando cambia su contenido; no se promete coste cero ni ausencia absoluta de tirones. Se eliminó también una superficie temporal del tamaño de la página. El movimiento no invalida las texturas ni la interfaz en el camino del vidrio por capas. La ruta GPU compuesta, si se desactivan las opciones visuales del vidrio, mantiene su funcionamiento anterior.

## Apariencia

- Indicador de página con cápsula redondeada, contador central y señales de anterior/siguiente donde corresponda. El espacio superior se adapta a la altura de la ventana.
- En la barra compacta, lo siguiente pasa a una tarjeta lateral «A CONTINUACIÓN», con título legible. El título actual reserva ese espacio. En pantallas estrechas, lo siguiente ocupa su propio renglón y el artista no baja sobre él. No aparece una tarjeta vacía cuando no hay cola.
  - *1 de octubre de 2026.* A 1024 de ancho la tarjeta solo cabía con créditos y sin el letrero MEJORADA: con MEJORADA
    (el renglón del artista crece unos 120 px) o sin créditos («Inserta una moneda» empuja la cola 75 px) se veía el
    renglón de reserva «SIGUE · …». Medido con la ventana real en cinco estados: salía en 1; ahora en los 5. La tarjeta
    tiene dos formas: **alta**, del título a la barra, en el mismo sitio que antes cuando ya cabía (idéntica píxel a
    píxel); y **baja**, a la altura del título, con el artista y su letrero pasando por debajo. Si las cápsulas crecen,
    empieza algo antes del 43 % del ancho para llegar a sus 180 px, sin quitarle al título sus 240 px. El renglón de
    reserva queda solo para pantallas estrechas. De paso, el título sin tarjeta al lado ya no desaparece: su letra
    crecía justo hasta llenar el renglón y GDI+ descartaba la línea entera (sin las cintas deslizantes se notaba).
- Se retiraron los vinilos del dibujo y la preparación de sus imágenes, dentro del disco y en el estado ampliado. La carátula, el cubo, los títulos y sus otros efectos se conservan. Ya no se reserva espacio lateral para el vinilo.
- Se retiró «Vinilo que asoma» de Ctrl+F3. La antigua clave vinilo_disco se ignora y deja de guardarse. Las funciones internas antiguas permanecen por ahora para pruebas de compatibilidad, pero la aplicación ya no las conecta al dibujo.
- No se modificó la distribución de la cola ampliada ni se añadió un interruptor de rendimiento.

## Verificación

Ambas ramas compilan Release con MSBuild y advertencias tratadas como errores. También se compilaron los perfiles GPU y CPU con sus compiladores correspondientes: 3.5 para Modest y 4.0 para HD.

Pruebas de esta pasada, en ambas ramas:

- paginasgpu: 446 comprobaciones; páginas de 640, 1200 y 2300 píxeles de ancho, identidad y versiones de las texturas durante el movimiento, firma de interfaz estable, salida y retorno, invalidación de cola y liberación. Incluye dibujo real Direct3D9 HAL y comprobación de recorte/UV.
- estilo: 32 comprobaciones, incluida tarjeta compacta, textos largos, alternativa estrecha, cola vacía y conservación de las etiquetas originales.
- capascolor, cristalcola, cubofila, respuesta y dibujoligero: aprobadas. Cubren selección idéntica píxel a píxel, colores, opciones, vidrio, geometría de portada y cachés.
- Se revisaron imágenes generadas de los componentes (paginador y barra). Esto no sustituye ver la aplicación con música y navegación real.

Medición orientativa en este PC: preparar una página de 2300 × 800 rondó 23–24 ms una vez; las consultas de caché posteriores rondaron 0.003–0.018 ms por cuadro. Son mediciones de preparación/caché con reflexión, NO FPS ni tiempo completo de presentación. No se midió en un procesador de dos núcleos ni en Windows XP.

## Pruebas antiguas con problemas preexistentes

No se afirma que toda la batería general esté en verde. Se contrastaron con las versiones anteriores de GitHub (HD 52e7140 y Modest 3c75089):

- discovivo mantiene cuatro discrepancias de representación de etiquetas/géneros/títulos largos en este entorno. Las expectativas relativas a los vinilos retirados y a sus interruptores se actualizaron.
- buscaligero ya detectaba 252 píxeles de un aviso fuera del rectángulo de subida parcial, con dos comprobaciones fallidas.
- suenavivo ya fallaba en avisos/invitación sin créditos y terminaba con NullReferenceException en el montaje de la prueba Invita.

Estos problemas no se ocultaron ni se marcaron como corregidos. Quedan separados de la corrección del cambio de página y del diseño solicitado. No se tocaron música, créditos, modelos de IA ni carpetas de instalación.
