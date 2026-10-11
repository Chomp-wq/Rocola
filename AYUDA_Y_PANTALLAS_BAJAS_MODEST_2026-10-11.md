# Casilla AYUDA, ayuda bloqueada y pantallas bajas — Modest, 11 de octubre de 2026

Petición del usuario (con captura a 1024×768): «al iniciar la rocola el icono de ayuda al lado del de búsqueda
tarda mucho en aparecer, y después de usar el asistente una vez, los controles de eso se bloquearán para siempre
por el resto de la sesión, arregla eso y también optimiza los espacios para resoluciones bajas».

Solo rama Modest y solo con tarjeta gráfica. Sin tarjeta, y en el perfil por CPU (congelado), la fila de arriba
queda exactamente como estaba:

- huella de Legacy idéntica;
- IL de los 14 métodos del dibujo por CPU idéntico al de `f2dc815`.

## 1. La casilla AYUDA tardaba en salir

**Causa:** al arrancar, la pantalla se acomoda (`LayoutControls`) **antes** de encender la tarjeta gráfica. La
casilla AYUDA solo existe con la tarjeta, así que en ese primer acomodo quedaba oculta. Nada volvía a acomodar la
pantalla hasta el primer cambio de canción o de créditos: por eso aparecía «al rato».

**Arreglo:** al encender o apagar la tarjeta (`ApplyAlbumGpu`) se acomoda la pantalla en el acto. Con eso también
quedan bien desde el principio los carriles de la barra de letras, que dependían de lo mismo.

## 2. La ayuda se quedaba bloqueada tras usarla

No se pudo reproducir aquí (Linux/Mono, sin Windows ni Direct3D). Al repasar el camino aparecieron dos
cosas que bloquean justo así, y se corrigieron las dos.

### El regreso de la estrella podía no acabar nunca

- Al guardar la ayuda, la estrella vuelve a su sitio en 0,72 s y mientras tanto la rocola no deja que un botón
  active lo de debajo.
- Ese «regresando» lo decidía la tarjeta con lo que vio en su **último cuadro**. Si los cuadros se paraban con la
  ayuda todavía abierta (panel tapado por un vídeo, ventana minimizada, dispositivo perdido…), la tarjeta seguía
  creyéndola abierta. El arreglo anterior (1,5 s sin cuadros) no la cubría, porque solo miraba ayudas ya cerradas.
- Resultado: la rocola se tragaba los botones, no salían las pistas del rojo y ROJO + ABAJO no la volvía a abrir.

**Arreglo:**

- El formulario cuenta el regreso con su propio reloj (`RegresoTopeMs` = 1,5 s desde que se guardó). Pasado ese
  tiempo no retiene nada, digan lo que digan los cuadros. La animación termina sola cuando vuelvan.
- Si alguna vez se atasca, queda apuntado en `rocola_error.txt` («el regreso de la estrella no terminó a
  tiempo»). Como mucho 20 apuntes por sesión.
- ROJO + ABAJO, o tocar AYUDA, mientras la estrella regresa la vuelve a abrir: la estrella da media vuelta desde
  donde iba. Antes ese rato no hacía nada y parecía bloqueada.

### El foco del teclado

- La ayuda y las pistas del rojo exigían que algo de la rocola tuviera el foco.
- Con la ventana delante pero el foco en nada, o fuera de ella (el vídeo, un control que se escondió), quedaban
  negadas sin remedio. El vigilante del foco solo actuaba si **otra** ventana se ponía delante.

**Arreglo:**

- Ahora basta con que la rocola sea la ventana de delante (`RocolaConTeclado`).
- El vigilante (cada 1,5 s) también le devuelve el foco a la ventana cuando está delante sin foco.
- `rocola_error.txt` sigue apuntando el motivo si ROJO + ABAJO no abre la ayuda o si las pistas del rojo no salen.

## 3. Pantallas bajas: la fila de arriba

En la captura (1024×768):

- BUSCAR iba centrada en la pantalla y dejaba 77 px vacíos detrás de las instrucciones.
- BUSCAR y AYUDA medían unos 45 px de más por dentro: el ancho se reservaba a ojo, no medido.
- El encabezado se quedaba en 208 px, la AYUDA iba sin su palabra y no cabía el número de página.

Ahora, con tarjeta gráfica:

- **BUSCAR y AYUDA miden lo que dibujan**: se mide su letra una vez por alto y les quedan 12 px de aire al
  final, como al principio.
- **El encabezado pide su sitio** (`HeaderView.AnchoPedido`): «LO MAS SONADO» a su tamaño, o «LETRA W» con su
  página corta («12/12»).
- **BUSCAR sigue en el centro si hay sitio**; si no, el par BUSCAR + AYUDA se corre a la izquierda lo justo
  para dejarle al encabezado lo que pide, sin pisar nunca las instrucciones (`ColocaParBusca`).
- **AYUDA lleva su palabra** si al encabezado le sigue quedando lo que pide. Si no, va sin ella. Si ni así le
  quedan 90 px al encabezado, no se pone.
- **Pantallas de menos de 1000 px**: las instrucciones van con menos aire entre sus tres grupos, y ese sitio se
  lo queda el encabezado.

Medido con las letras de Mono (en Windows las letras son más estrechas y sobra algo más):

| Pantalla | Antes | Ahora |
| --- | --- | --- |
| 1024×768 | BUSCAR 238, AYUDA sin palabra 149, encabezado 208 px | BUSCAR 203, AYUDA con palabra 194, encabezado 255 px (lo que pide) |
| 800×600 | AYUDA no cabía | BUSCAR 203, AYUDA sin palabra 117, encabezado 138 px |
| 1280 o más | BUSCAR centrada | BUSCAR centrada (más estrecha), encabezado más ancho |

A 800×600 «LO MAS SONADO» se encoge un poco para caber; el encabezado ya lo hacía solo.

Con un vídeo sonando, BUSCAR y AYUDA no están en la fila, como antes (la ayuda se abre igual con ROJO + ABAJO).

## Cómo se comprobó

**Pruebas automáticas (Linux/Mono, sin Direct3D):**

- compilación .NET 3.5 de los dos perfiles con cero avisos;
- batería nueva `filabaja`:
  - arma la interfaz de verdad en siete resoluciones, de 800×600 a 1920×1080;
  - comprueba orden y huecos, que nada se pise, AYUDA con palabra a partir de 1024 y lo que pide el encabezado;
  - comprueba BUSCAR centrada en 1920 y que con vídeo no salen BUSCAR ni AYUDA;
  - comprueba que sin tarjeta, y con CPU, la fila queda como antes;
  - 133 comprobaciones con GPU y 50 con CPU;
- `asistentecombo`: 84 comprobaciones, 20 nuevas. Cubren el tope del regreso y la vuelta del contador; el caso
  que bloqueaba (tarjeta parada con la ayuda abierta) recuperando los botones; el apunte en la bitácora; reabrir
  mientras regresa; y el foco;
- además pasan `guiarojo`, `ayudaguia`, `asistentevisual`, `buscaligero`, `bloqueos`, `entrar`, `titulo`,
  `buscaportadas`, `estadodetras` y `arranqueeficiente`.

**Legacy y CPU:**

- huella de Legacy idéntica;
- `cpucongelado`: el IL de los 14 métodos es idéntico al de `f2dc815`.

**No comprobado:**

- el bloqueo en la máquina real (si vuelve a pasar, `rocola_error.txt` dirá el motivo);
- el aspecto con las letras de Windows;
- Windows XP;
- FPS (no cambia nada que se dibuje por cuadro).
