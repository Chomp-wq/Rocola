# Botón rojo mantenido y carga de carátulas — Modest, 11 de octubre de 2026

Petición del usuario: «Necesito el mejor rendimiento posible en el programa, revisa también los modos de
carga de carátulas para ver si no pueden ser mejorados, y un bug que encontré que no sé si ya arreglaste es
que al dejar presionado el botón rojo, ya no me salían los señalamientos de combinación ni me dejaba usar el
asistente».

Solo rama Modest. Legacy y el renderizado por CPU no cambian (huella de Legacy y IL de los 14 métodos del
dibujo por CPU iguales que en `8c7ec69`).

## El botón rojo mantenido

No se pudo reproducir aquí: la prueba usa Linux/Mono, sin Windows, sin botones reales ni `GetKeyState`.
Se repasó todo el camino:

1. Tecla.
2. `ProcessCmdKey`.
3. Ayuda (`AsistenteEntrada`).
4. Condiciones de la guía (`GuiaRojoVisible`).
5. Firma de la interfaz.
6. Redibujado por la tarjeta.

En la versión actual, en la pared y sin nada abierto, no hay ninguna condición que bloquee las pistas ni el
atajo ROJO + ABAJO.

Sí hay un caso que produce exactamente los dos síntomas a la vez:

- Cuando la ayuda se cierra, la estrella vuelve a su sitio en 0,72 s.
- Ese regreso solo lo termina un cuadro de la tarjeta (`AvanzaAsistente`). Si los cuadros se paran a mitad
  (ventana minimizada, dispositivo gráfico perdido…), el estado «regresando» se queda encendido.
- Mientras tanto la rocola:
  - se traga los botones;
  - no dibuja las pistas del rojo;
  - no deja volver a abrir la ayuda.

Corregido:

- **El regreso deja de bloquear tras 1,5 s sin cuadros** (`RegresoSinCuadros`). La animación sigue cuando
  vuelven los cuadros.
- **Diagnóstico permanente en `rocola_error.txt`** (no depende de la bitácora de diagnóstico):
  - si el rojo lleva 0,7 s mantenido en la pared y las pistas no salen, se apunta el motivo exacto: sin foco
    del teclado, leyendo la biblioteca, ayuda regresando, sin barra de letras, etc.;
  - si se pulsa ROJO + ABAJO y la ayuda no se puede abrir, también se apunta por qué.
  - Como mucho 20 apuntes de cada tipo por sesión.
  - El buscador, el interior de un disco, la atracción y el vídeo a pantalla completa no se apuntan: ahí las
    pistas no salen por diseño.

Si vuelve a pasar, `rocola_error.txt` dirá el motivo.

## Modos de carga de carátulas

Se revisó el regulador (`CoverGovernor`), los lectores y los cuatro perfiles:

- **Automático:** el regulador original.
- **Fluidez:** 0,55 del presupuesto y como mucho 2 lectores.
- **Equilibrado:** 0,85 del presupuesto y la mitad de lectores.
- **Carga rápida:** 1,25 del presupuesto; sube un lector cada 375 ms en vez de cada 750 ms.

El diseño es correcto:

- un solo lector por disco duro, un trabajador en equipos de dos hilos;
- un presupuesto por cuadro que se adapta al coste medido;
- la presión reduce lectores a la mitad;
- previas de 32 px para la primera vista;
- la carga de previas se hace una vez por página.

No se cambiaron sus números a ciegas: el mejor perfil depende del disco y de la tarjeta del bar. Se compara
en la máquina real con Ctrl+F3 → Imagen → Medir, con el mismo inicio, resolución y efectos, y la tabla de
comparación.

### Carátulas en JPEG: interruptor opcional (Ctrl+F3)

Medido con el GDI+ de Mono (orientativo; en Windows los números cambian):

| Formato | Tamaño | Guardar | Leer |
| --- | --- | --- | --- |
| PNG (de fábrica, sin pérdida) | 142 KB | 23,5 ms | 2,7 ms |
| JPEG calidad 92 | 22 KB | 0,6 ms | 0,85 ms |

JPEG guarda el color a media resolución: en carátulas con letras y dibujos se nota algo en los bordes de
color (diferencia media de 7 niveles, picos de 150, incluso a calidad 100). Por eso no se aplicó por su
cuenta: los perfiles de carga no deben cambiar la calidad final.

A petición del usuario («Sí, añade las carátulas en jpeg como interruptor») queda como opción visual:

- **Dónde:** Ctrl+F3 → tarjeta «Carga de carátulas» → «Carátulas en JPEG (más rápidas)».
- **De fábrica:** apagado; «Pantalla como de fábrica» lo vuelve a apagar.
- **Clave en la configuración:** `caratulas_jpeg=`.

Encendido:

- Las portadas sin transparencia se reducen repitiendo su propio borde (ya no queda un filo semitransparente)
  y se guardan en JPEG de calidad 92 al lado de su identidad (mismo nombre, `.jpg`).
- Las que tienen transparencia, y toda imagen de paleta (GIF), siguen en PNG sin pérdida.
- Las miniaturas que ya estaban en PNG se siguen usando. El calentador de fondo las rehace en JPEG desde la
  portada original, con sus mismas pausas.
- Las transparentes se repasan una vez por sesión en segundo plano, porque no se sabe que son transparentes sin
  abrir la portada. Son pocas.

Apagado:

- Los JPEG se ignoran y cada carátula vuelve a su PNG sin pérdida, rehecho al verla o en segundo plano.
- Solo con tarjeta gráfica: con el perfil por CPU (congelado) el interruptor no cambia nada.

## Pruebas desfasadas tras `e345478` (puestas al día; el programa estaba bien)

- `estadodetras`: el guardado del estado pasó de `EscribeEstado` a `EncolaEstado`; la comprobación estática
  ahora mira los dos.
- `buscaportadas`: un toque ya no cobra una tarjeta de la consulta anterior. La prueba ahora deja posar lo
  tecleado antes de tocar las tarjetas, como hace el temporizador.
- (Ya subido en `3e756fe`) `buscaligero`: acepta el bloqueo extendido hasta el borde de la textura.

## Cómo se comprobó

- **Pruebas automáticas (Linux/Mono, sin Direct3D):**
  - compilación .NET 3.5 de los dos perfiles con cero avisos;
  - `asistentecombo` (64 comprobaciones, 7 nuevas del regreso sin cuadros y los diagnósticos);
  - batería nueva `caratulasjpeg`: 21 comprobaciones con GPU, 10 con CPU. Cubre apagado y encendido, la portada
    opaca hasta el borde, las transparentes y GIF en PNG, la conversión del PNG por el calentador, la vuelta
    atrás y la clave de configuración;
  - `guiarojo`, `ayudaguia`, `asistentevisual`, `estadodetras`, `buscaportadas`, `buscaligero`,
    `miniaturas`, `arranqueeficiente` y `bloqueos`.
- **Legacy y CPU:**
  - huella de Legacy idéntica;
  - `cpucongelado`: el IL de los 14 métodos del dibujo por CPU es idéntico.
- **No comprobado:**
  - el botón rojo en la máquina real;
  - Windows XP;
  - los tiempos de los perfiles de carga en el disco y la tarjeta del bar.
