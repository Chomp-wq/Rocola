# Revisión de todo lo de Spotify (23-11-2026)

Petición del dueño: «Busca arreglos a todo lo relacionado con Spotify, no debe fallar jamás, pon a agentes sonnet para que encuentren más
bugs y sea todo más rápido y eficiente».

Seis revisores (modelo ligero) leyeron cada parte sin tocar nada; cada hallazgo se comprobó en el código antes de arreglarlo (la regla del
12-11-2026: toda revisión la cierra el modelo de la sesión). Partes: el núcleo (RocolaOs.Spotify.cs), la fuente y el anillo
(SpotifyFuente, SpotifyAnillo, SpotifyPide), el cliente de la API web (SpotifyCatalogoApi, CalidadApi), el buscador y sus paredes
(SpotifyCatalogo, SpotifyCatalogoParedes, BuscadorMas, ParedLetrero), el nivelado, el EQ y la sustitución (SpotifyNivela, SpotifyEq,
SpotifyRepetida, Calidad*), y el sistema (rocola-soloist, la orden spotify y rocola-pulso).

## Arreglado

**El núcleo (RocolaOs.Spotify.cs, SpotifyFuente.cs)**
- Ya no se manda «play» (ni el último intento con activate) a una pedida que acaba de darse por terminada: un play suelto podía sonar un
  instante.
- Si Soloist toca algo sin canción identificada (uri vacía) más de 5 s y la rocola no tiene ninguna pedida viva, se pausa (nada suena sin
  pedirse). Con una pedida sonando no se corta nada (la «canción vacía» del 22-11 sigue tratándose como pasajera).
- Todas las pedidas llegan al hilo de Spotify (una cola, no un solo hueco): si dos fuentes pedían mientras el hilo estaba ocupado (un connect
  de 3 s), la primera se perdía sin rematar y su crédito, si tocaba devolverlo, no volvía.
- Cerrar la fuente y su primera lectura, o cerrarla y que acabe sola a la vez: decide solo una (atómico). Antes un salto justo al fallar podía
  dejar sin devolver el crédito, y una lectura que se cruzaba con el cierre pedía una canción que nadie iba a oír.
- La salvaguarda del play adelantado ya no lo apaga para siempre por un corte normal en los últimos 5 s, una retomada o un corte seguido
  (solo con un final de verdad: TerminoReal).
- Una conexión que Soloist cierra enseguida ya no reinicia la espera (antes reconectaba cada 0,5 s para siempre y llenaba la bitácora).
- El volumen: al reconectar se olvida el de antes y, si Soloist aún no dijo el suyo, se le pide 100 una vez por conexión.

**El cliente de la API web (SpotifyCatalogoApi.cs)**
- Lo de la cuenta (favoritas, tus discos, tus listas) con una parte que no llegó por algo pasajero se vuelve a pedir entero (antes faltaban
  tus discos o tus listas hasta reiniciar), con espera creciente (30 s, 1, 2, 4… hasta 30 min; antes cada 30 s para siempre).
- La autorización renovada de la cuenta se guarda en memoria antes de escribir el archivo (si no se podía escribir, la cuenta moría a la hora).
- Un disco de más de 50 canciones con alguna que no se puede tocar entre las primeras ya no pierde las de la 51 en adelante.
- Dos pantallas esperando la misma respuesta: si una truena, la otra igual la recibe.
- El hilo de la API ya no muere con una excepción fuera de lo atrapado (la API quedaba muerta hasta reiniciar).
- Un 429 corto no acorta uno largo; un plazo leído de /run se acota a 24 h; la llave se lee una sola vez (la pantalla podía borrarla entre
  medias).

**El buscador y sus paredes**
- Con un video puesto, BUSCAR y AYUDA tampoco salen en una pared del buscador (g264, g265 en AyudaCasillaGpu.cs; lo de hoy solo cubría la
  pantalla sin video).
- Quien espera dentro de una casilla que aún no llega ya no es sacado del disco cuando un bloque de delante trae menos de 10.
- Un artista o un disco pedido que llega tarde ya no se abre encima de otra búsqueda (se compara lo escrito).
- Volver a una letra que no es la guardada empieza desde su principio; el mismo disco en dos bloques sale una vez.

**Nivelado, EQ y sustitución**
- El objetivo del nivelado en vivo copia la tabla de XP con su cerrojo (recorrerla sin él podía tronar al abrir una de Spotify) y se rehace de
  verdad cada minuto; si falla, la canción suena sin nivelado en vivo.
- La sustitución invisible: una de Spotify «terminada» habiendo sonado menos de la mitad (Soloist saltó) vuelve a tu MP3 desde donde iba;
  antes el cliente perdía su canción.
- Una medida casi muda de la sustitución no se guarda; una pareja que la forma dijo que es otra grabación no bloquea como «repetida».

**El sistema**
- rocola-soloist: si Soloist no da su pid en 30 s (antes 5 s), se cierra lo que haya arrancado y se reintenta; antes quedaba un Soloist sin
  vigilante y la vuelta siguiente abría otro con la misma carpeta.
- rocola-pulso: la salida pedida como «@DEFAULT_SINK@» o sin nombre es la de la rocola (antes «no existe»).

## Revisado y no se cambió

- Doble reembolso: no hay (Rematada; la fuente acaba limpia y XP no devuelve otra vez). Tras un apagón lo pagado de Spotify vuelve como
  crédito.
- pa_context_drain marca BADSTATE como la libpulse real; strftime y read -s existen en el BusyBox del sistema (probado en QEMU antes).
- Propuestas que no se hicieron (dudosas o sin poder probarlas con el Soloist real): esperar a que Soloist confirme la canción antes del seek
  de una retomada; no vaciar el anillo al abrir un flujo nuevo tras un cierre sin drain; topes a las cachés de la API en memoria.

## Pruebas

Fuera de pantalla, con el Soloist falso, la API falsa y datos de prueba rehechos a mano en este contenedor (los de antes se perdieron con el
contenedor): ver la sección «Resultados» abajo. rocola-pulso: 11 pasos bien (tsan, asan, valgrind). Nada probado con Soloist ni Spotify
reales ni en el A4.
