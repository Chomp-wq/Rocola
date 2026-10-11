# Lo obsoleto, fuera, y las mejoras de rendimiento de la semana, medidas — Modest, 11 de octubre de 2026

Petición del usuario: «Busca si hay cosas obsoletas en el programa para que las elimines, y revisa las mejoras de
rendimiento hechas en la semana para ver si sí mejoran el rendimiento y no solo lo prometen».

## 1. Lo obsoleto que se quitó

Se buscó con un recorrido del código compilado: todo método que ningún otro método del programa nombra (llamada,
delegado o referencia). Se repitió hasta que no salió nada nuevo, en los dos perfiles.

### El vinilo (retirado el 24-09-2026, su código seguía dentro)

El commit `c3b284f` quitó el vinilo de la pantalla y de Ctrl+F3, pero dejó su código «por ahora para pruebas de
compatibilidad». Nada lo usaba. Se quitó entero:

- dentro del disco:
  - el estado, el horneado del disco y los surcos;
  - la textura y el giro a 33 1/3;
  - el cubo que se apartaba para dejarlo asomar (`CuboCorrido`, siempre 0);
- en el panel de reposo:
  - su vinilo, su textura y su giro;
  - el desplazamiento del texto «para dejarlo asomar» (`Corre`, `IdleCorre`, `IdleBadgeRectCorrido`, siempre 0);
- el latido vacío `SuenaVivoTic`;
- la línea que ignoraba la clave antigua `vinilo_disco=` (las claves desconocidas ya se ignoran solas);
- **un texto visible desfasado:** en Ctrl+F3 → Pantalla, «Lo que suena, en grande» seguía diciendo «En reposo, un
  vinilo gira detrás de la portada». Ahora dice lo que hace: el título en una línea que se desliza si no cabe y
  cuánto falta.

### Funciones que nadie llamaba

`AlbumFolderFor`, `KeysBySeq`, `MarcaRetardoFinalReloj`, `VerdeHeld` (resto del antiguo acorde rojo+verde),
`CajaSigueLejos`, `PintaTituloLejos`, `PintaSigueLejos`, `StepSpinGpu`, `AtajosVisibles`, `RenderTransparentGpu`,
`Track.SoloArtista`, `DrawMoonOrbits` (las órbitas de la luna antigua), `RemuestreoContinuo.TablaPreparada`,
`Encadenado.FichaActual`, una sobrecarga de `GananciaDeTabla` y tres envoltorios del panel de reposo
(`RenderIdlePanel`, `…Mode`, `…Styled`).

En total: 23 archivos, unas 640 líneas menos.

### Lo que se deja a propósito

- **El dibujo por CPU.** AGENTS.md lo declara obsoleto y congelado, pero también dice que no se retira sin petición
  expresa. Es además el respaldo si la tarjeta gráfica falla al arrancar. Si quieres quitarlo, dilo y se hace aparte.
- **Unas 20 funciones que el programa ya no llama pero usan las pruebas** como referencia de regresión. Por ejemplo,
  el atlas anterior de carátulas, la luna detallada antigua o el reloj sin caché, con los que se compara bit a bit
  la versión nueva. Quitarlas dejaría esas pruebas sin con qué comparar.

### Cómo se comprobó

- Compilación .NET 3.5 de los dos perfiles con cero avisos.
- Huella de Legacy idéntica.
- IL de los 14 métodos del dibujo por CPU idéntico.
- `suenavivo`, `discovivo`, `pestanas`, `ceremonia`, `cristalcola`, `optimizaciones` y `capascolor` puestas al día o
  pasando. `discovivo`, `estilo`, `paginasgpu` y `acabadofinalnatural` fallan igual que antes de tocar nada en este
  Linux, con los mismos motivos.
- Tanda completa: ver el commit.

## 2. Las mejoras de rendimiento de la semana, medidas

«La semana» son los commits del 10 y 11 de octubre: no hubo otros desde el 4. Se midió en este equipo (Linux/Mono,
4 núcleos), con el ejecutable de **antes de la semana** (`8933f51`) y el **actual**, el mismo trabajo y alternando los
dos para no favorecer a ninguno.

**Esto no son FPS ni tirones del bar.** Eso solo se mide en la máquina real con Ctrl+F1.

### Análisis de canciones del repaso (memoria por trabajador, sonoridad integrada reutilizada)

Es lo que más se repite: el registro real del 10-10 tenía 28.677 archivos pendientes de repaso. Se midieron 8
canciones de 3–4 min en WAV (`MeasureFile` + `MideIntegrada`), 3 vueltas:

| | Antes | Ahora |
| --- | --- | --- |
| Memoria reservada por canción, sin cosecha IA | 215 KB | 63 KB (**−71 %**) |
| Memoria reservada por canción, con cosecha IA | 915 KB | 84 KB (**−91 %**) |
| Tiempo por canción, sin cosecha | 810–827 ms | 761–776 ms (**−5 a −8 %**) |
| Tiempo por canción, con cosecha | 844–883 ms | 800–823 ms (**−5 %**) |

**Veredicto: mejora real.** Mucha en memoria, poca en tiempo.

Límite importante: en Modest el MP3 se lee para medir con el descompresor de Windows (ACM), que no existe en Linux.
Así que el MP3 no se pudo medir aquí. Tampoco la mejora de abrir el MP3 sin leer dos veces la carátula. El propio
informe del 10-10 dice que el lector MP3 de NAudio crea miles de objetos por canción, y eso no se tocó. Con MP3
reales, el alivio del recolector de memoria será proporcionalmente menor que el −71/−91 % de arriba.

### Ecos de la estrella 3D (geometría reutilizada)

Medido con su propia batería, 12 ecos:

- Legacy: de 49 a 11 µs por cuadro.
- Natural: de 52 a 20 µs por cuadro.

**Real pero pequeña:** unos 0,03–0,04 ms por cuadro aquí. En un procesador viejo de dos núcleos puede ser 3–5 veces
eso. Solo cuenta mientras la estrella 3D deja ecos.

### Grano de Espacio natural (horneado con teja de 256)

- 1024 px: de 6,9 a 6,1 ms; 4 MB → 256 KB reservados.
- 512 px: de 2,5 a 2,2 ms; 1 MB → 256 KB.

**Real pero casi irrelevante:** se hornea una sola vez por tamaño de pantalla.

### Malla del ruido del cielo (todas las tejas en un envío)

A 3440×1440 pasa de 8 envíos a 1. **A 1024×768 ya era uno solo antes y después: en la máquina del bar no cambia
nada.**

### Campo de estrellas (dos juegos de buffers) y reloj del reposo (texto en caché)

Evitan unos 235 KiB por relevo de estrellas, cada 60–180 s, y un texto corto por cuadro. Son unos pocos MB por hora
de basura menos. **Correctas pero sin efecto apreciable.** No se midieron aparte.

### Publicar solo lo que cambió (tintes de la barra de letras)

Su batería lo mide: 771 de 8395 filas enviadas (9 %). **Real**, sobre todo con bibliotecas grandes.

### Buscador (relevancia y faltas de ortografía)

No era una optimización y **hace más trabajo**:

- busca desde la primera letra (antes, desde la segunda);
- busca con faltas («corazn» encuentra «corazon»).

Con 30.000 canciones sintéticas:

| | Antes | Ahora |
| --- | --- | --- |
| Tecla típica (mediana) | 0,14–0,18 ms | 0,17–0,23 ms |
| p95 | 1,8–2,4 ms | 3,0–4,2 ms |
| Tecla más cara | 3,9–5,5 ms | 8,5–13 ms (la primera letra «c», o «corazn») |

Es el precio de buscar mejor. En un equipo viejo la tecla más cara podría rondar 30–50 ms. Se nota poco porque la
consulta se posa antes de buscar.

### Lo que no se puede medir aquí

- Subida repartida de las cintas de texto y retirada de texturas: necesitan Direct3D.
- Respaldos sin WriteThrough y corpus por bloques: el disco del bar es otro y Linux escribe distinto.
- Medida ligera del MP3: necesita ACM de Windows.

Están comprobadas como **equivalentes** (mismo resultado bit a bit), pero su ganancia de tiempo no está medida.

### Resumen

| Mejora | ¿Mejora de verdad? |
| --- | --- |
| Análisis de canciones | Sí: −71/−91 % de memoria, −5/−8 % de tiempo (WAV) |
| Ecos 3D | Sí, pequeña (~0,04 ms/cuadro aquí) |
| Grano Natural | Sí, irrelevante (una vez) |
| Malla del ruido | Solo en pantallas grandes; a 1024×768 nada |
| Buffers de estrellas, reloj | Correctas, sin efecto apreciable |
| Tintes por cambios | Sí (9 % de filas) |
| Buscador | No es optimización: un poco más lento por buscar mejor |
| Texturas, escrituras, MP3 | Sin medir aquí (Windows/Direct3D/ACM) |

Ninguna es dañina. No se revirtió nada: las que no aportan tampoco cuestan.
