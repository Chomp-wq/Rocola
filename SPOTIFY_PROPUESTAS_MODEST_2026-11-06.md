# Spotify en la rocola: propuestas para aprovecharlo (6-11-2026)

Pedido del dueño: «tener acceso a canciones de Spotify abre muchas oportunidades, como poder poner las letras de las canciones cuando esté
la barra de estado extendida, mayor calidad de mp3, etc. Piensa en ajustes aparte de los dos que acabo de mencionar».

**Elegidas (6-11-2026): todas menos SP7, SP10 y SP12, y nada que haga sonar música sin créditos. Hechas: ver sistema/ETAPA2.md (Spotify, segunda parte).** Las letras, «con el estilo de Apple Music»; y la versión de Spotify de tus dañadas, «que luzca igual», sin ningún cambio visible. Todo va solo con el modo Spotify encendido (reuniones familiares) y cada cosa que cambia lo
que se ve o se oye lleva su interruptor en Ctrl+F3 → Spotify. Lo que dice «depende de Spotify» no se puede asegurar hasta probarlo con
tu cuenta: Spotify cierra cosas a los programas de fuera de vez en cuando.

Lo que se comprobó en el código de librespot 0.8.0 (las piezas que usa la rocola): trae **las letras sincronizadas** de Spotify, **la
radio** a partir de una canción (lo que Spotify pone al acabar una lista) y los datos de canciones, discos y artistas. **Calidad: hasta
320 kbps (Ogg Vorbis)**; el sin pérdida (FLAC) que Spotify da en Premium **librespot aún no lo toca** (no está en sus formatos).

## Las dos que pediste

| | Qué hace | Coste en el A4 | Notas |
|---|---|---|---|
| **SP1. Letras en el estado extendido** | Con la letra de Spotify (sincronizada: cada línea a su tiempo), el estado extendido enseña la línea que se canta en grande, con la anterior y la siguiente más tenues, como un karaoke; se lee de lejos (la misma letra grande de «Letra para leer de lejos»). Si Spotify no tiene la letra de esa canción, el estado extendido queda como hoy. | Bajo (una consulta por canción y texto en pantalla) | Las letras las da Spotify en el momento (no se guardan). Con SP4, también para tus MP3. Interruptor, encendido con el modo Spotify. |
| **SP2. La versión de Spotify para tus canciones dañadas** | El repaso ya sabe qué MP3 vienen recortados o con poca calidad (el corte de agudos que mide). Con el modo encendido, si en Spotify está **la misma grabación** (mismo artista, título y duración ±2 s; con SP4), suena la de Spotify (320 kbps) en su lugar; en la cola y la pantalla sigue siendo tu canción, con el mismo crédito. Si Spotify falla, suena tu MP3. | Bajo | Con «Comparar a ciegas: tu MP3 o Spotify» para que lo decidas de oído. No se baja ni se guarda el archivo de Spotify: solo suena. Interruptor, apagado de fábrica. |

## Otras que propongo

| | Qué hace | Coste | Notas |
|---|---|---|---|
| **SP3. Que Spotify se nivele como tu música** | Hoy cada canción de Spotify va al nivel fijo de «las aún sin medir». Propuesta: medirla mientras suena (como hace el repaso con las tuyas) y apuntar la medida por su número de Spotify (solo números, nada de música): la segunda vez va **al límite**, igual que tu biblioteca, y el modo billar decide con su LRA y su EQ como con las tuyas. | Bajo | Cambia lo de hoy («de Spotify no se aprende nada»), que fue decisión mía, no tuya. Automático, sin interruptor (no cambia el aspecto). |
| **SP4. Emparejar tu biblioteca con Spotify** | En segundo plano, poco a poco, busca cada canción tuya en Spotify y apunta la pareja si es la misma (artista, título y duración). Es la base de SP1 para tus MP3, de SP2, SP5 y SP6. | Bajo (de fondo, unas pocas por minuto) | Solo apunta números de Spotify; no toca tus archivos. Automático con el modo encendido. |
| **SP5. Portadas y géneros que faltan** | Los discos tuyos sin carátula toman la de Spotify, y las canciones sin género, el del artista en Spotify: mejoran el **color del género**, lo más sonado por género y la pared. | Bajo | Las portadas se guardan en los datos de la rocola (solo imágenes, nunca en tu carpeta de música). Interruptor. |
| **SP6. Completar tus discos** | Dentro de un disco tuyo, las canciones que le faltan (las que el disco tiene en Spotify) salen al final con una marca de Spotify y se piden con su crédito. | Bajo | Necesita SP4. Interruptor. |
| **SP7. Radio cuando se acaba la cola** | Si la cola se vacía en una reunión, la rocola sigue con canciones parecidas a la última (la radio de Spotify), **más bajito y sin cobrar**, como música de fondo, hasta que alguien meta una moneda (entonces manda la cola, como siempre). | Bajo | Interruptor, apagado de fábrica. Respeta el gusto de la rocola clásica: lo pagado manda. |
| **SP8. Éxitos del momento** | En la letra SPOTIFY, además de lo tuyo, «Top 50 México», «Virales» y las listas de temporada. | Nulo | **Depende de Spotify**: desde 2024 limita esas listas a programas de fuera; se prueba con tu cuenta. |
| **SP9. Tus más escuchadas como atajo** | En el buscador, atajos «Tus más escuchadas» y «Lo que escuchaste hace poco» (de tu cuenta), junto a «Lo más pedido hoy». | Nulo | Solo lectura de tu cuenta. |
| **SP10. Discos de Spotify sin pausa** | Las pistas seguidas de un mismo disco de Spotify (en vivo, mezclas, óperas) sin el silencio de en medio, como ya hace la rocola con tus discos continuos. | Medio (cambia cómo se encadenan) | Hoy cada canción de Spotify abre la salida aparte y queda un respiro corto entre una y otra. |
| **SP11. El estado extendido más vivo** | Con lo que suena de Spotify: la foto del artista de fondo (difuminada, con el color del género), el año y el disco. | Medio (una imagen grande en la tarjeta del A4: medir la fluidez allí) | Interruptor, como las mejoras visuales. |
| **SP12. Guardar la reunión en tu Spotify** | Al acabar la noche, una lista en tu cuenta («Rocola – reunión 6-nov») con lo que sonó, para escucharla después en el celular. | Nulo | Es lo único que **escribe** en tu cuenta (hoy la rocola solo lee). Botón en Ctrl+F3, no automático. |

## Lo que no conviene (por ahora)

- **Sin pérdida (FLAC):** librespot no lo toca; si una versión futura lo trae, se sube la calidad sin cambiar nada más.
- **Karaoke quitando la voz:** separar la voz bien (con IA) no corre en tiempo real en el A4; quitar «el centro» a lo bruto suena mal.
- **Escuchar un pedazo antes de pagar:** la rocola tiene una sola salida; interrumpiría lo que suena.
- **Guardar canciones de Spotify en el disco** (para sin internet): no; es lo que Spotify prohíbe y lo que acordamos no hacer.

## Recomendación

Para empezar: **SP1, SP2 (con SP4), SP3 y SP7**. Las letras y la mejor calidad son las que pediste. SP3 hace que Spotify suene tan
parejo como tu música, y SP7 aprovecha la radio de Spotify en las reuniones sin quitarle a la rocola lo de las monedas.
