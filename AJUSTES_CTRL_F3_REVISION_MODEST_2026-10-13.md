# Los ajustes de Ctrl+F3, revisados — Modest, 13 de octubre de 2026

Petición del usuario: «Revisa en la ventana de Ctrl+F3 todos los settings a ver qué cosas necesitan una renovación o
mejora de cómo funcionan algunos settings».

Esta ronda es solo de revisar: **no cambia nada del programa.** Recorrí las seis páginas (Colores, Detalles, Sonido,
Imagen, Fondo y Pantalla) y sus hojas: unos 80 ajustes. Abajo, lo que encontré y lo que propongo, para que elijas.

## Lo que funciona bien (no lo tocaría)

- **Sonido.** Cada cambio se oye al instante en la canción que suena. Lo que no manda nadie se pone gris (con el nivelado
  apagado, sus dos barras) y los «Auto» dicen qué eligieron.
- **Imagen.** Cada efecto tiene sus tres niveles, con lo que ahorra en este equipo y lo que se pierde. Y ahora están el
  preset Performance y el medidor renovado.
- **Cada página tiene su «como de fábrica»** y los textos caben (lo comprueba `ventanaf3`).

## Lo que encontré

### 1. Interruptores que a veces no hacen nada, sin avisar

Algunos dependen de otro que está en otra página. Si ese otro está apagado, se puede tocar el primero y no pasa nada.
Solo lo dice un texto:

| Este interruptor (página) | Solo funciona con (página) |
| --- | --- |
| Interior de discos con cristal (Fondo) | Lista de canciones transparente (Imagen) |
| Cola ampliada de 12 canciones (Fondo) | Estado y cola ampliados en reposo (Imagen) |
| Neón de progreso intenso (Fondo) | Barra de progreso neón (Fondo) |
| Fuerza del detalle natural (Fondo) | Nebulosas con detalle natural (Fondo) |
| Más profundidad al pasar de página (Fondo) | Pasar de página deslizando (Pantalla) |
| Modelos del DSEE por género (Sonido → IA) | DSEE encendido (Sonido) |

Además, **sin tarjeta gráfica** todos los «(GPU)» se pueden tocar, pero no hacen nada. Son la mayoría de Fondo y Pantalla.

### 2. Ajustes de lo mismo, repartidos en varias páginas

- **El color de la portada en el fondo**, en tres sitios:
  - «Que el fondo tome el color de la portada» (Detalles);
  - «Disco 20 % / Mitad / Disco 100 %» (Colores);
  - «Paleta completa de carátula» (Fondo).
- **La estrella del cielo**, en dos:
  - «Astro principal en el cielo» (Detalles);
  - superficie, corona, polvo, ecos, 3D y realce en reposo (Fondo).
- **Las carátulas**, en dos:
  - ocultarlas al hojear rápido y las miniaturas provisionales (Imagen);
  - el perfil de carga y el JPEG (Pantalla).
- **Fondo es un cajón de sastre.** Además del cielo lleva cosas que no son del fondo: la barra de progreso neón y sus dos
  variantes, el estado ampliado transparente, la cola de 12, el cristal de los discos, ocultar controles en reposo, la
  respuesta al toque y las transiciones.
- **En Sonido hay dos cosas fuera de sitio:**
  - «Tipo de disco» (Automático / SSD / Disco duro) es de rendimiento y de carátulas, no de sonido;
  - «Tarjeta a: 44,1 / 48 kHz» está dentro de la «Prueba de bocinas», y es un ajuste de la salida, no una prueba.

### 3. Cada página se navega distinto

- Imagen y Pantalla tienen **hojas con pestañas arriba**.
- Sonido abre «El local y sus bocinas...» y «Mejoras con IA...» con **botones de tres puntos** y vuelve con «Volver a las
  canciones».

Son dos maneras distintas de hacer lo mismo.

### 4. Cosas que solo se cambian en el archivo de configuración

- **Las horas del modo noche** (`noche_desde=` y `noche_hasta=`): la ventana enseña las horas, pero no deja cambiarlas.
- **El tope de cuadros por segundo** (`tope_fps=`): el botón de fábrica lo devuelve a automático, pero no se puede elegir.

### 5. No se ve qué está cambiado

No hay ninguna marca de qué ajustes ya no están como de fábrica. Para volver a fábrica hay **cinco botones, uno por
página**, y ninguno para todo.

### 6. Se guarda solo al cerrar la ventana

Si se apaga el equipo con Ctrl+F3 abierta (en el bar puede pasar), se pierde lo tocado en esa vez.

### 7. Nombres

- Casi todos llevan «(GPU)» en el nombre, cuando bastaría con decirlo una vez por página.
- «DSEE», sin más, no le dice nada al encargado.
- Los nombres viejos van sin tildes («Viñeta» sí, pero «Halo de seleccion», «Barra de progreso neon»); los nuevos sí las
  llevan.

## Lo que propongo, para que elijas

| | Qué | Por qué |
| --- | --- | --- |
| **A1 (recomendada)** | **Dependencias vivas.** El interruptor que no puede funcionar se pone gris con «Necesita: … (página)», y vuelve solo cuando su requisito se enciende. Sin tarjeta gráfica, los de tarjeta en gris con un aviso por página; se quita el «(GPU)» de cada nombre. | Arregla lo que hoy confunde: tocar algo y que no pase nada. No cambia ningún valor guardado. |
| **A2** | **Guardar al momento**, no solo al cerrar. | Un apagón con la ventana abierta ya no pierde nada. |
| **A3** | **Las horas del modo noche en la ventana** («de 1 a 7 h», con flechas). | Hoy hay que abrir el archivo con el bloc de notas. |
| **A4** | **Marca de «cambiado»** (un punto junto a lo que no está de fábrica) y **un botón «Todo como de fábrica»** con confirmación. | Se ve de un vistazo qué se tocó. |
| **A5** | **Ordenar.** Fondo solo con el cielo (y una hoja «La estrella» con todo lo suyo). Progreso, estado, cola, cristal y controles en reposo pasan a Pantalla. Lo del color de la portada, junto en Colores. Tipo de disco junto a la carga de carátulas. 44,1/48 kHz fuera de la prueba de bocinas. | Cada cosa donde uno la buscaría. Es el cambio más grande: mueve muchos interruptores (las claves guardadas no cambian). |
| **A6** | **Sonido con hojas** como Imagen y Pantalla (Canciones / El local / IA), en vez de botones «...» y «Volver». | La misma manera de moverse en toda la ventana. |
| **A7** | **Nombres con tildes y claros**, por ejemplo «DSEE (agudos que el MP3 quitó)». | Se lee mejor; solo cambia el texto. |

Mi orden: **A1 y A2 primero**, porque arreglan cómo funcionan las cosas, no solo cómo se ven. Después A3 y A4. A5, A6 y A7
cuando quieras ordenar la ventana. Las siete se pueden comprobar aquí con `ventanaf3` (textos que caben, nada fuera de
sitio) y con pruebas nuevas para las dependencias.
