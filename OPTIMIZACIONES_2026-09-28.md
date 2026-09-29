# Optimizaciones del 28/09/2026 — más fluido en los equipos de dos núcleos

Pediste buscar mejoras de rendimiento en todo el programa y hacer las tres que salieron. Las tres son automáticas y
siempre activas: no cambian ni el sonido ni lo que se ve, así que no llevan interruptor. Van en las dos ramas (Modest y
HD). El dibujo por procesador (el viejo, congelado) no se ha tocado.

## 1. La música en su propio hilo

### Qué pasaba

La salida de sonido clásica (WaveOut) es la que usan todos los equipos de dos núcleos: Modest siempre, y HD cuando el
equipo no llega a "HD" (el modo HD pide cuatro núcleos). Esa salida pide la música desde el bucle de la pantalla: cada
50 ms la pantalla se paraba en seco a descomprimir la canción y a pasar el bloque por toda la cadena (nivelado, EQ,
realce, DSEE, grave, modo noche, limitador, fundido).

Medido en el servidor de pruebas, con todo el sonido encendido, cada bloque de 50 ms le costaba a la pantalla **1,8 ms
de media y picos de hasta 7,6 ms**, más cerca de 1 ms (picos de 9) cuando Modest descomprime con NLayer. Un cuadro de
pantalla dura 16,7 ms. En un procesador de dos núcleos de los viejos esas cifras pueden multiplicarse varias veces
(estimación, no medido): un solo bloque podía comerse un cuadro y se veía como un saltito en la estrella y en las
animaciones. Y mientras tanto el segundo núcleo estaba casi parado.

### Qué cambia

La cadena ahora la lee **un hilo propio para la música** (`Rocola/BombaSonido.cs`), con prioridad por encima de la
pantalla. Va dejando hecho un colchón corto (200 ms) y, cuando la salida pide un bloque, la pantalla solo copia lo que
ya está hecho. La salida, su colchón de 1 s y sus bloques de 50 ms siguen igual.

- **El sonido no cambia ni una muestra**: mismo orden y mismas cuentas.
- **Lo que se ve va a su tiempo**: la música se lee unos 160 ms antes que antes, y el pulso, la aurora y el relevo sin
  pausa suman ese adelanto (`RetrasoAudible`). Los cambios de Ctrl+F3 se oyen esos 160 ms más tarde; saltar o parar
  una canción sigue siendo inmediato.
- **Si el hilo se atrasa** (un disco que tarda en despertar): la pantalla lo espera hasta 600 ms, porque la salida aún
  tiene casi un segundo guardado y no se corta. Si ni así llega, entrega silencio en vez de colgarse, y no vuelve a
  esperar hasta que llegue música. Antes, en ese caso, la pantalla se quedaba congelada todo lo que tardara el disco.
- **El final y los fallos** llegan a la salida como antes: se acaba cuando se acabó lo que había, y un fallo de la
  cadena se atiende igual que siempre (aviso, crédito devuelto si toca, etc.).
- **Al parar**, el hilo se detiene y se espera antes de soltar el archivo: nadie lee un archivo cerrado, y la medida de
  la canción se guarda con el sonido de verdad quieto.
- La salida WASAPI de los equipos HD ya tenía su propio hilo: ahí no cambia nada.

### Comprobaciones automáticas

Batería nueva **`pruebas/bomba.cs`** (24 comprobaciones), verde en HD y en Modest:

- Pidiendo trozos de 1 a 9000 muestras, y a través de la vista flotante que NAudio pone sobre su memoria, sale todo,
  en orden y exacto; en mono, estéreo y 5.1, a 22, 44,1, 48 y 96 kHz. Al acabarse devuelve 0, como antes.
- Quien pide (la pantalla) nunca lee la cadena: la lee siempre el mismo hilo aparte, con prioridad por encima de lo
  normal.
- Un fallo a media canción llega después de lo que ya estaba hecho, con el error de la cadena dentro.
- Un atraso de 150 ms no deja ni una muestra de silencio. Uno de 2 s: la pantalla espera 600 ms como mucho, después ya
  no espera (0,9 ms la siguiente), entrega bloques de silencio enteros (nunca 0) y, al volver, no se pierde ni se
  repite ninguna muestra.
- Al pararla el hilo se va y nadie vuelve a leer la cadena, también si estaba a media lectura lenta.
- **La cadena de verdad** (abrir la canción + armar la cadena con nivelado, realce, DSEE, grave, noche, limitador y
  fundido): por la bomba sale **bit a bit lo mismo** que leída a mano (1.058.886 muestras), también abriendo la salida
  clásica con una salida de mentira.
- Los enganches: la salida clásica lee de la bomba; al parar va primero la salida, después el hilo y solo entonces se
  suelta el archivo; el pulso, la aurora y el relevo suman el adelanto; la bomba se crea en un solo sitio, que suelta la
  anterior.

**Lo que le cuesta a la pantalla** (simulación con una cadena que tarda 2 ms por cada 25 ms de música): cada bloque de
50 ms pasa de **4,07 ms a 0,07 ms** de media (0,2 ms el peor).

**Averías metidas a propósito**: 15, y la batería las detecta todas: esperar sin tope, no esperar nada, devolver 0 en vez de silencio, olvidar que el hilo va atrasado, dejarlo con prioridad normal, no esperarlo al parar, perder el fallo, no despertarlo al copiar, no marcar el final, no soltar la bomba anterior, quitarla de la salida, soltar el archivo antes que el hilo, calcular mal el adelanto y que el pulso o el relevo no lo sumen.

### Lo que no se ha comprobado

- Escucharlo en el bar y mirar la pantalla del equipo de verdad (si desaparecen los saltitos se ve con el medidor de
  Ctrl+F3).
- Windows XP real. Modest compila como C# 3 para .NET 3.5; el hilo usa solo lo que ya había en .NET 2.0.
- Un disco duro de verdad que tarda en despertar: el atraso está simulado.

## 2. El reloj de la canción, solo su recuadro

### Qué pasaba

La franja de abajo (lo que suena, lo que sigue, el reloj, los créditos, la cola con sus portaditas y la barra) es una
imagen aparte que se rehace cuando cambia lo que dice. Con música, el reloj ("1:47 / 4:12") cambia cada segundo, así que
**cada segundo se rehacía la franja entera** (todos los textos y las pastillas) y **se subía entera a la tarjeta**: 440 KB
a 1024×768, en el mismo hilo que dibuja la pantalla.

### Qué cambia

Si lo único que cambió es el reloj o la barra, se repinta solo su recuadro y la tarjeta recibe solo ese trozo
(`Rocola/FranjaLigeraGpu.cs`). Solo en el dibujo por tarjeta gráfica; el dibujo por procesador no se toca.

- Se pinta todo como siempre y en el mismo orden, pero recortado al recuadro, así que dentro sale lo mismo que saldría
  entera. El recuadro se vacía con copia directa, sin depender de cómo trate Windows el borrado con recorte.
- Los títulos que se deslizan no se tocan: un recuadro no los puede borrar.
- Cualquier otra cosa (otro título, otros créditos, la cola, la letra de lejos, otra pantalla, el reloj que cambia de
  sitio, la atracción) rehace la franja entera, como siempre.
- La barra de progreso de la franja va con su valor exacto: antes se rehacía por tramos de 1/300 de la canción; ahora su
  recuadro se repinta cada vez que avanza (dos veces por segundo). Se ve igual, un poco más fluida.

### Comprobaciones automáticas

Batería nueva **`pruebas/franjareloj.cs`** (16 comprobaciones), verde en HD y en Modest:

- **Idéntica a rehacerla entera, píxel a píxel**, en 24 escenas (dos pantallas; letra de lejos o no; títulos que se
  deslizan o no; barra en la franja, en neón o dibujada por la tarjeta) y 9 pasos de reloj y barra en cada una
  (0:59 → 1:00, 9:59 → 10:00, 59:59 → 1:00:00...): 216 de 216, todos por recuadro.
- Los títulos que se deslizan quedan igual que rehaciéndola entera.
- Si solo avanza la barra: por su recuadro e idéntica; si la dibuja la tarjeta, la franja ni se rehace.
- Lo demás, entera; la atracción, limpia; al salir, entera e idéntica.
- El trozo solo vale para la versión de justo antes.
- **Con la tarjeta imitada**: en 9 pasos la tarjeta recibe solo el recuadro, en la misma textura, y queda igual que la
  imagen; si cambia otra cosa o la tarjeta se saltó una versión, entera.

**Medido** (servidor de pruebas): el recuadro es de media el 13 % de la franja; lo que se sube cada segundo pasa de
**440 KB a 70 KB**, y rehacerla a 1920×1080 de **2,3 ms a 0,8 ms**.

**Averías metidas a propósito**: 13, y la batería las detecta todas: pintar sin recorte, no vaciar el recuadro o vaciarlo sin copia directa, redondear la barra, tocar las cintas de verdad, no mirar la firma fija ni la de la atracción, dar el trozo a cualquier versión, que la tarjeta suba siempre entera o no pregunte el trozo, quitar el margen del reloj o de la barra, y que el reloj no mire dónde está.

### Lo que no se ha comprobado

- Una tarjeta de verdad: la de las pruebas es imitada. Cuánto se nota se ve con el medidor de Ctrl+F3.
- El dibujo de Windows: las pruebas usan el de Mono. El recorte del dibujo se mantiene al mover el origen (así trabaja
  Windows y así lo dan las pruebas), pero no se ha mirado en un Windows real.
- Windows XP real.

## 3. La rocola, por delante de las tareas de Windows

### Qué pasaba

La rocola corría con la prioridad normal de Windows. El equipo del bar es de la rocola, pero Windows sigue a lo suyo
(actualizaciones, el antivirus repasando el disco, el indexador) y, con la misma prioridad, le quita procesador justo
cuando lo necesita: en dos núcleos, eso es la música o la pantalla esperando.

### Qué cambia

Al arrancar, la rocola se pone con prioridad **"por encima de lo normal"** (`Program.cs`, `PrioridadDeLaRocola`):

- Solo la rocola que se queda: si ya había una corriendo, la segunda se cierra sin tocar nada.
- Nunca "alta" ni "tiempo real", que pueden estorbar al propio Windows (el ratón, el sonido del sistema).
- Lo de fondo de la rocola (el repaso de la biblioteca, las portadas) sigue en prioridad mínima o baja dentro de ella:
  nunca por delante de su pantalla ni de su música. Los programas que lanza (el motor de la IA en HD) no la heredan:
  Windows les da la normal.
- Si Windows no deja cambiarla, sigue como siempre.

### Comprobaciones automáticas

Batería nueva **`pruebas/prioridad.cs`**: se pone después de mirar si ya había otra rocola y antes de abrir la ventana; es
"por encima de lo normal" y nunca alta ni tiempo real; no hay otro sitio que la cambie; va protegida y llamarla de verdad
no revienta nada.

**Averías metidas a propósito**: 4 (ponerla alta, quitarle la protección, ponerla antes de mirar si ya había otra rocola,
no ponerla); la batería las detecta todas.

### Lo que no se ha comprobado

- Cuánto se nota en el equipo del bar con Windows ocupado en sus cosas: no se puede medir aquí.
- Windows XP real (la prioridad "por encima de lo normal" existe desde Windows 2000).

## 4. Las tablas grandes, sin reescribirlas enteras (SSD y disco duro)

### Qué pasaba

`rocola_volumen.txt`, `rocola_eq.txt` y `rocola_sonoridad.txt` tienen una línea por canción y se guardaban **enteras**
cada 25 canciones medidas durante el repaso de la biblioteca. Había una regla para guardar menos a menudo cuanto más
grande fuera la biblioteca, pero miraba lo **ya medido**, que en la primera medición empieza en cero: ahí no servía. Con
20.000 canciones eso eran unos **3 GB** escritos para dejar 8 MB de tablas; con 100.000, unos 80 GB (calculado a partir
del código, con rutas de unos 80 caracteres). En un SSD es desgaste; en un disco duro, varios MB forzados al disco cada
minuto mientras suena la música.

### Qué cambia

Se **añade al final** solo lo nuevo o lo que cambió, con el formato de siempre (`DiarioTablas.cs`). Se puede porque la
rocola -esta y las de antes- ya lee esas tablas quedándose con la **última** línea de cada canción. Se sigue escribiendo
entera, con el guardado seguro de siempre, la primera vez de cada sesión, si se quitó alguna canción, si alguien tocó o
borró el archivo, y cuando hay demasiadas repetidas (se compacta). Qué dice ya el archivo se sabe por una huella de cada
medida (sus números bit a bit), sin volver a escribir el texto. Lo añadido se baja al disco de verdad, igual que antes.

Si se va la luz justo al añadir, lo de antes no se toca; al leer se descarta una última línea a medias y cualquier línea
con ceros. El entrenador del EQ (`ia/entrenar_eq.py`) también se queda con la última línea de cada canción.

### Comprobaciones automáticas

Batería nueva **`pruebas/diario.cs`** (27 comprobaciones): la primera vez entera y con los mismos bytes; 25 nuevas, solo
esas 25 líneas; tres cambios (también solo de duración), solo esas tres; al leer, lo que hay en memoria; sin cambios, nada;
entera al quitar una canción, al tocar el archivo desde fuera (con y sin cambios en memoria) y si desapareció; la
compactación; cada sesión empieza limpia; apagones (última línea a medias, ceros al final y en medio); lo mismo en el EQ y
en la integrada (cabecera y registros de una versión posterior). **Medido en la prueba**: la tabla de volumen de 20.000
canciones medidas de 25 en 25 escribe 1,7 MB en vez de 686 MB (lo de antes, calculado: la tabla entera en cada guardado),
399 veces menos, y queda idéntica byte a byte a la escrita entera. Y en `ia/test_entrenar_eq.py`, las repetidas.

**Averías metidas a propósito**: 17 (siempre entera, sin mirar el largo, sin mirar si se quitó alguna, sin compactar,
añadir todas, huella sin la duración, no apuntar lo añadido, no apuntar el largo nuevo, añadir sin bajar al disco, leer la
última a medias, leer líneas con ceros, el arranque, la integrada y el respaldo leyendo todo, la integrada sin cabecera o
sin los registros futuros, el EQ por fuera); la batería las detecta todas.

### Lo que no se ha comprobado

- El disco del bar: lo escrito se ha contado aquí, no medido en su SSD o disco duro.
- Windows XP real.

## 5. La canción que suena y la siguiente, en memoria

### Qué pasaba

Mientras sonaba, la canción se seguía leyendo del disco a trozos chicos: **medido** con un MP3 de 488 KB, NAudio hace
2.413 lecturas y saltos al abrirlo y 2.402 más mientras suena. Windows las junta, pero si el disco se atasca (un disco
duro con el antivirus encima, un disco que se había dormido, una memoria USB lenta), la música espera al disco. La
siguiente ya se leía entera por adelantado, pero los bytes se tiraban y se confiaba en que Windows los guardara.

### Qué cambia

Un MP3 de hasta **32 MB** (y nunca más de un octavo de la memoria libre) se abre **desde la memoria**
(`CancionEnMemoria.cs`): la lectura por adelantado deja ahí los bytes de la siguiente -con los mismos trozos y pausas de
siempre- y, si una canción llega sin ellos, se lee entera de una vez al abrirla (en trozos de 256 KB). Mientras suena, el
disco queda libre para las portadas y el repaso. Lo leído por adelantado se usa una sola vez y solo si el archivo no cambió.
Como mucho hay dos a la vez en memoria: la que suena y la siguiente. Lo que no es MP3, o pasa del tope, sigue como siempre.

**Se oye lo mismo**: es el mismo lector de NAudio con el mismo descompresor (el `AbreMp3` de memoria es el de siempre letra
por letra), sobre los mismos bytes.

### Comprobaciones automáticas

Batería nueva **`pruebas/enmemoria.cs`** (18 comprobaciones): con un descompresor de prueba (el de Windows no existe
aquí), el lector entrega exactamente lo mismo desde la memoria que desde el archivo, también saltando a mitad de canción;
el `AbreMp3` de memoria es igual al de siempre en la fuente y compilado; el cargador (una sola vez, firma, otra canción,
soltar al cambiar la cola, formatos, tope); **con el archivo vaciado y borrado a mitad de canción, desde la memoria sigue
entera e idéntica, y leyendo del archivo se cortaba en el 33 %**; y el enganche.

**Averías metidas a propósito**: 10 (no pedirla a la memoria, que la lectura por adelantado no la deje, sin mirar la firma,
sin mirar la ruta, entregarla dos veces, sin tope, cualquier formato, otro descompresor en memoria, soltar la cola sin
soltar la memoria, cerrar sin soltar la canción); la batería las detecta todas.

### Lo que no se ha comprobado

- La decodificación real de Windows (ACM / Media Foundation) y NLayer por este camino: aquí no hay descompresor de MP3 del
  sistema; la igualdad se comprobó con un descompresor de prueba y comparando el código.
- El tope de memoria libre (aquí no se puede preguntar a Windows) y un XP real con poca memoria.
- Cuánto se nota en el disco del bar.

## 6. Los tirones de la pantalla al poner una canción (29 de septiembre)

### Qué pasaba

Al poner una canción la pantalla se paraba en seco. Se midió con la rocola de verdad y 20.000 canciones en las tablas, en
un procesador de servidor (en el equipo del bar, varias veces más):

| Qué | Antes | Ahora |
|---|---|---|
| Empezar la canción: la salida clásica llena sus 20 bloques (1 s de música) de una vez | 44-48 ms esperando al hilo de la música | 0,6 ms al pedirla y 0,3 ms al arrancar (el segundo lo hace el hilo de la música, unos 38 ms) |
| Guardar la tabla de volumen (al acabar una canción recién medida, y cada tanto mientras se mide la biblioteca) | 23 ms | 0,8 ms |
| El techo de "Subir TODO" con medidas nuevas (una vez por minuto mientras se mide la biblioteca, desde el latido de la pantalla) | 11-13 ms | 1,3 ms |

### Qué cambia

- **El primer segundo lo hace el hilo de la música** (`BombaSonido.CuandoEsteLista`): empieza en cuanto se arma la salida,
  lo deja hecho en un anillo más grande y avisa; la salida arranca entonces, en la pantalla, y solo copia
  (`ArrancaCuandoEsteLista`, `SalidaClasicaLista`: los mismos pasos de siempre y en el mismo orden). Si mientras tanto se
  salta la canción, el aviso viejo no arranca nada. Sonando, el colchón vuelve a sus 200 ms. Lo que suena no cambia ni una
  muestra. WASAPI (HD) ya hacía su primer llenado en su propio hilo.
- **La tabla de volumen** se copia como lista; el índice de las duraciones se arma al escribir, fuera de la pantalla.
- **El techo de "Subir TODO"** con medidas nuevas: la pantalla copia los valores y la cuenta va en otro hilo; hasta que
  llega vale el de antes. Con un ajuste de Ctrl+F3 se sigue contando al momento. Da exactamente lo mismo.

### Comprobaciones automáticas

Batería nueva **`pruebas/tirones.cs`** (21 comprobaciones, verde en las dos ramas; con el programa anterior, 6 fallos).
Las demás baterías de la bomba, el diario de las tablas y el nivelado siguen en verde.

### Lo que no se ha comprobado

- WaveOut de verdad (aquí no hay Windows): el arranque se probó con una salida de prueba que pide sus 20 bloques como
  WaveOut.Play.
- Cuánto se nota en el equipo del bar y en XP real.

## 7. El disco fuera del hilo de la pantalla: créditos, cola y bitácora (29 de septiembre)

### Qué pasaba

- **Créditos y cola.** Se guardan con cada moneda, cada canción que se pone, cada cambio de tema y cada crédito devuelto.
  Guardarlos es bajarlos al disco de verdad (archivo de paso, `FlushFileBuffers` y cambio de un tirón), y eso se hacía en
  el hilo de la pantalla. En este servidor no se nota (aquí no existe `FlushFileBuffers`), pero en un disco mecánico
  ocupado bajar un archivo al disco tarda decenas de milisegundos: un tirón en cada moneda.
- **La bitácora.** `rocola_diagnostico.txt` (con `diagnostico=1`), `rocola_error.txt` y `rocola_incompatibles.txt` se
  escribían en el hilo que apuntaba, muchas veces el de la pantalla: abrir, mirar el tamaño y pegar la línea. Con el
  diagnóstico encendido son varias líneas por canción. Y si el archivo estaba ocupado un instante (un antivirus), la línea
  se perdía.

### Qué cambia

- **Créditos y cola** (decisión del usuario del 29 de septiembre de 2026, que sustituye a la de guardarlos a la cara): la
  pantalla arma el texto y lo encarga (`EscribeEstado`); lo escribe un hilo de fondo, en el mismo archivo y de la misma
  forma (mismos bytes, copia `.bak`, bajada al disco). Si se juntan varios, se escribe el último. Cada encargo y cada
  borrado lleva un número de orden que se mira bajo el candado del archivo: un encargo viejo nunca pisa a uno nuevo ni
  resucita un estado borrado a propósito (`BorraEstado`: cierre, día nuevo, estado de otro día). Lo encargado que aún no
  llegó al disco se lee de la memoria al restaurar (`EstadoSinEscribir`). Al cerrar sin borrar se espera al último
  encargo. Queda una ventana de unos milisegundos entre la moneda y el disco; antes era la del propio `FlushFileBuffers`.
- **La bitácora** (`Bitacora`, en `MedidasBitacora.cs`): quien apunta deja la línea, con su hora de ahora, en una cola y
  sigue; un hilo de fondo las escribe en orden y de a tandas, con el mismo formato, el mismo tope de tamaño y el mismo
  sello de versión (que también sale del disco, así que lo pone el hilo que escribe). Con el archivo ocupado se reintenta
  durante un segundo en vez de perder la línea. Un fallo grave (`LogCrash`) se escribe antes de volver, y al cerrar no
  queda nada en la cola.

### Comprobaciones automáticas

- **`pruebas/estadodetras.cs`** (21 comprobaciones, verde en las dos ramas; con el programa anterior se queda esperando
  al disco y falla): con el disco ocupado, 35 guardados vuelven en 0,1 ms el peor; al soltarse queda el último con los
  mismos bytes; 25 veces encargar y borrar con el disco ocupado, ninguna resucita; restaurar ve lo encargado; unos 50 µs
  por guardado en la pantalla.
- **`pruebas/bitacoradetras.cs`** (17 comprobaciones, verde en las dos ramas; con el programa anterior 11 fallos): con el
  archivo ocupado, apuntar 200 líneas no espera y llegan las 200 en orden (antes se perdían todas); formato, sello y tope
  iguales; el fallo grave se escribe ya.
- Las baterías que leían el estado o la bitácora justo después de apuntar esperan ahora a que llegue (`Codigo.EsperaGuardado`,
  `Codigo.EsperaBitacora`); `arranque.cs` vigila la decisión nueva en lugar de la anterior.

### Lo que no se ha comprobado

- Un disco mecánico real con `FlushFileBuffers` de Windows: lo que se ahorra por moneda en el bar no está medido aquí.
- Un apagón de verdad justo en los milisegundos entre la moneda y el disco.

## 8. El arranque sin la ventana parada esperando las tablas (29 de septiembre)

### Qué pasaba

Las tablas del arranque (volumen, EQ, lo más sonado…) las lee un hilo aparte desde que se crea la ventana. Pero al
mostrarse, la ventana se quedaba parada esperándolas (`Monitor.Wait` en `CompleteStartupDataLoad`): sin pintar ni atender
nada. Con la ventana real y tablas de 40.000 canciones generadas por la propia rocola, en este servidor:

| | La parada más larga al arrancar |
|---|---|
| Antes | 816–1.146 ms |
| Ahora | 178 ms (preparar la tarjeta gráfica) y 265–283 ms (publicar la biblioteca recién armada) |

Las dos que quedan no son de las tablas: la tarjeta se prepara en el hilo de la ventana por obligación, y publicar la
biblioteca es un paso aparte (el vigía de la pantalla lo apunta; ver la sección siguiente).

### Qué cambia

- Al mostrarse, la ventana hace lo que no necesita las tablas (configuración, vigía del foco) y deja apuntado qué seguir
  (`EsperaLasTablas`). El lector avisa al terminar, desde su hilo y fuera de su candado, y la ventana sigue con
  `SigueArranque`: las tablas, lo más sonado y la biblioteca (que es la que restaura créditos y cola), **en el mismo orden de
  siempre**. Publicar las tablas ya no espera nunca.
- Las teclas que llegan mientras tanto **esperan su turno** y se atienden al final, en orden, como las dejaba en cola la
  ventana parada: una moneda no puede contarse antes de leer las monedas y lo más sonado guardados (los pisaría al
  publicarlos). Salir (Ctrl+Alt+Q) funciona siempre. El latido no anima nada mientras tanto: ese procesador es del lector.
- Cerrar mientras se leen las tablas no sigue con nada y no guarda tablas vacías, como antes.

### Comprobaciones automáticas

**`pruebas/arranquevivo.cs`** (18 comprobaciones, verde en las dos ramas; con el programa anterior, 9 fallos: se queda
esperando al lector). Usa una tubería como tabla lenta para ver que el lector avisa una vez y fuera de su candado.
`historialarranque.cs` mira el orden en `SigueArranque`.

### Lo que no se ha comprobado

- Un disco mecánico real con tablas grandes (ahí la espera de antes era mayor) y la tarjeta gráfica de verdad.

## 9. El vigía de la pantalla, dentro de la rocola (29 de septiembre)

### Qué pasaba

Cuánto tardaba la pantalla en atender solo se podía medir desde fuera, con un banco de pruebas. En el bar no había forma de
saber si hubo un tirón ni qué lo causó.

### Qué cambia

- Un hilo aparte (`VigiaPantalla`, en `MedidasBitacora.cs`) le da un toque a la pantalla cada 25 ms y cronometra cuánto tarda
  en atenderlo. **Siempre en marcha**, sin interruptor (no cambia nada de lo que se ve ni se oye); son unos 40 mensajes por
  segundo.
- Las entradas grandes marcan lo que hacen al empezar (una cadena y un reloj, 16 ns): una tecla, el latido, un cuadro de la
  tarjeta, el reloj de la canción, empezar una canción, arrancar la ya abierta, publicar la biblioteca, poner una portada,
  preparar la tarjeta y el arranque. El dibujo por CPU no se toca.
- **150 ms o más**: a `rocola_diagnostico.txt` (con `diagnostico=1`), diciendo qué la paró («en:») o qué fue lo último antes
  («después de:», si la paró algo sin marca). **500 ms o más**: siempre a `rocola_error.txt`, como mucho una cada 10 s y
  diciendo cuántas se callaron. Si la pantalla lleva **10 s** sin atender, se dice sin esperar a que vuelva.
- Se arranca al mostrarse la ventana y se para al cerrar (cerrar tarda lo que tenga que tardar).

Con la ventana real, `diagnostico=1` y tablas de 40.000 canciones, el vigía de la rocola apuntó las dos paradas del arranque
que el banco de pruebas midió desde fuera: 176 ms (fuera, 190) en el primer latido y 272 ms (fuera, 275) al publicar la
biblioteca. Con un toque cada 100 ms se le escapaba la primera: por eso va cada 25.

### Comprobaciones automáticas

**`pruebas/vigiapantalla.cs`** (19 comprobaciones, verde en las dos ramas; con el programa anterior falla: no hay vigía).
Con una ventana de verdad parada a propósito: 420 ms se dicen como 416 con su marca, 80 ms no se dicen, lo que no tiene
marca sale como «después de»; los umbrales, el límite de apuntes y las marcas de las entradas.

### Lo que no se ha comprobado

- La rocola del bar con Windows y la tarjeta de verdad: ahí es donde el vigía va a contar lo que pasa.

## 10. La rocola entera, con su ventana, en la tanda (29 de septiembre)

Hasta ahora lo que se medía con la ventana real se hacía a mano, fuera de la tanda. **`pruebas/ventanareal.cs`** lo deja
dentro: arranca la rocola entera con su ventana (Xvfb), con una biblioteca generada (121 discos) y tablas de arranque de
40.000 canciones escritas por la propia rocola, todo en una carpeta temporal. La usa con un guion (pedir canciones, pasar
páginas, entrar a un disco, abrir el buscador, saltar, pedir seis seguidas) con una salida de sonido de mentira que pide la
música como WaveOut, así que la cadena de sonido trabaja de verdad. Un hilo aparte cronometra cuánto tarda la pantalla en
atender.

Falla si la pantalla se para **más de 500 ms al arrancar** o **más de 150 ms usándola**, si no suenan al menos 5 canciones o
si hay excepciones en la pantalla. Medido aquí: al arrancar 170–220 ms, usándola 20–30 ms la peor, en las dos ramas. Con la
rocola de antes del arranque sin esperar las tablas, 650 ms al arrancar: falla.

Lo que no mide: Windows, la tarjeta gráfica ni un disco mecánico (aquí la ventana se dibuja por CPU con Mono).

## 11. Lo que puede parar la pantalla, vigilado leyendo el programa (29 de septiembre)

**`pruebas/bloqueos.cs`** lee el programa compilado y, desde todo lo que corre en la pantalla (temporizadores, teclas, dibujo,
eventos y lo que se le manda con `BeginInvoke`: 212 entradas en HD), sigue las llamadas sin cruzar a lo que se encarga a un
hilo de fondo (más de 2.000 métodos) y apunta cada sitio que llega a algo que puede esperar: el disco, bajar al disco,
`Sleep`/`Join`/`WaitOne`/`Monitor.Wait`, lanzar o esperar un programa, abrir una imagen, abrir una canción o medirla.

Cada sitio aceptado está en una lista **con su motivo**, revisada a mano: 52 en HD y 38 en Modest, casi todos de una sola
vez (el arranque, restaurar tras un apagón, empezar el día), de ventanas de servicio o de la IA en vivo de HD. Un sitio
nuevo hace fallar la batería y enseña el camino desde la pantalla hasta la llamada; uno de la lista que ya no ocurre,
también. Con la rocola de antes de estas mejoras señala justo lo que se arregló: guardar el estado (`SaveState`), la
bitácora (`Diag`, `LogError`, `NoteIncompatible`), borrar el estado y la espera del arranque (`CompleteStartupDataLoad`).

Quedan anotados como pendientes, con su motivo, tres que tocan el disco o esperan al empezar o cambiar de canción:

- la etiqueta ID3 de la canción que empieza (`Track.ReadId3`);
- la IA en vivo de HD (`IaEnhancedPath` y compañía): ya está la versión mejorada;
- esperar a que el hilo de la música suelte la canción anterior (`BombaSonido.Dispose`, como mucho 1 s).

Lo que no ve: una espera dentro de otra biblioteca que no sea de las de la lista, un bucle largo sin llamadas, ni lo que
hace un delegado guardado en un campo y llamado después (como la firma de la siguiente canción).
