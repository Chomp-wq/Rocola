# La pantalla viva, tanda 4: el modo atracción

Cambio del 24 de septiembre de 2026, compartido por HD y Modest. Son las dos ideas del lienzo de bocetos para el modo
atracción: el escaparate y lo más pedido esta noche. Solo cambian el dibujo por tarjeta gráfica. El renderizador por
procesador sigue congelado y no se tocó: sin tarjeta gráfica, o con las dos ideas apagadas, la atracción es exactamente
la de siempre (las mismas portadas con el mismo ritmo, el cubo girando en el centro y sus dos textos).

La atracción sale cuando la rocola se queda sola: nada sonando, nada en la cola y 40 segundos sin que nadie la toque.

Cada idea tiene su interruptor en Ctrl+F3 → **Pantalla**, en la hoja nueva **La atracción y el estilo** (la tercera),
tarjeta **MODO ATRACCIÓN**. Las dos vienen encendidas y se guardan en `rocola_config.txt` (`escaparate=` y
`top_noche=`).

El código nuevo está en `Rocola/AtraccionGpu.cs`. Los demás archivos llevan solo los enganches: la entrada y la salida
de la atracción (`Form1.Biblioteca.cs`), el cubo, los textos de siempre y su brillo (`Form1.Video.cs`), la cuenta y su
guardado (`Form1.Datos.cs`, `Form1.cs`), el cuadro de la tarjeta (`D3D9Star.cs`) y la pestaña (`PantallaVivaGpu.cs`).

## 1. El escaparate — `escaparate=`

- **El cubo gigante se va a la izquierda** y, en vez de girar, **se mece** (±16°, una vez cada 9 segundos): así la
  portada casi siempre se ve de frente.
- **A su derecha sale el disco que está enseñando:**
  - "EN ESTA ROCOLA", en letra chica;
  - el nombre del disco, grande, en hasta dos renglones (si no cabe, termina en "…");
  - el artista, en verde;
  - las pastillas: el género (de su color), "pedido 31 veces" (lo que se han pedido sus canciones desde siempre;
    solo si es más de cero) y "24 canciones". Si no caben en un renglón, pasan al segundo.
- **Cada disco se queda unos 8 segundos** (antes, casi 4) para que dé tiempo a leerlo. El texto cambia a la vez que la
  portada, en dos tiempos: la ficha vieja sube y se va, y luego llega la nueva desde abajo. Nunca las dos encima.
- **Debajo se turna un mensaje cada 5 segundos**. Entra desde abajo, se queda y se va hacia arriba, con un brillo dorado
  detrás como el de "toca un botón para elegir":
  - "Toca un botón y elige entre 12,480 canciones";
  - "Pon tu canción: una moneda = 4 canciones" (lo que da de verdad una moneda);
  - "Lo más pedido esta noche: «Volver Volver»" (solo si hay lista y esa idea está encendida);
  - "Busca cualquier canción con el botón rojo y la flecha".

  Si un mensaje no cabe, la letra se achica un poco.
- Con el escaparate, los dos textos de siempre ("12,480 canciones" y "toca un boton para elegir") no salen: los
  mensajes dicen lo mismo.
- Al entrar la atracción, la ficha aparece mientras la portada vuela desde la pared hasta el cubo (0,7 s).

## 2. Lo más pedido esta noche — `top_noche=`

- **Cada 30 segundos, y durante 15**, sale la lista de las cinco canciones más pedidas esta noche.
  - El cubo se hace chico y se va a la esquina de arriba a la derecha (0,9 s).
  - Cuando ya casi llegó, salen la cabecera y las filas, una detrás de otra desde la izquierda.
  - Al terminar, la lista se apaga primero y después el cubo vuelve. Nunca se cruzan.
- **La cabecera:** una luna, "Lo más pedido esta noche" y cuántas canciones se han pedido.
- **Cada fila:**
  - el puesto (el primero, en amarillo y más grande);
  - la portada de su disco (si aún no ha cargado, una tapa del color de su género con las iniciales del artista);
  - el título y el artista;
  - una barra que crece según las veces que se pidió (la primera, entera);
  - "14 veces".
- Si dos canciones se pidieron las mismas veces, va primero la que se pidió más tarde.
- **Hacen falta tres canciones distintas** pedidas esta noche y que sigan en la biblioteca. Si no, no sale la lista y
  el escaparate sigue.
- **Sin escaparate** (interruptor apagado), la atracción de siempre se turna con la lista: el cubo grande del centro se
  va a la esquina, y los dos textos de siempre y su brillo se apagan mientras está la lista.

### La cuenta de la noche

- **La noche va de las 8 de la mañana a las 8 de la mañana siguiente**: a las 7:59 todavía es la de ayer. A las 8
  empieza otra noche desde cero.
- Cuenta **las canciones que entran a la cola**, en el mismo momento en que cuentan para "lo más sonado". Los vídeos no
  cuentan. Una canción que ya no está en la biblioteca se salta.
- **Se guarda en `rocola_noche.txt`**, en la carpeta de datos: cada minuto si cambió algo, y al cerrar la rocola antes
  de esperar a los demás guardados. Así, si se reinicia a media noche, no se pierde.
  - Formato: arriba `noche=AAAA-MM-DD`; después una línea `veces|ruta` por canción, de la pedida hace más tiempo a la
    última.
  - Se escribe como los demás archivos de la rocola: primero una copia temporal y la anterior queda como `.bak`. Si el
    bueno falta, se lee la copia.
  - Si el archivo es de otra noche, no vale. Las líneas rotas se saltan. Como mucho 400 canciones distintas.
- **Sin tarjeta gráfica no se lleva la cuenta** ni se escribe el archivo.

## Lo que cuesta

Medido en este contenedor, con Mono. Sirve para comparar unas cosas con otras; los milisegundos del bar serán otros.

| Qué | Cuánto | Cuándo pasa |
|---|---|---|
| Hornear la ficha de un disco | 1,3 ms (1920×1080: 2,2 ms) | Cada ~8 s |
| Hornear los mensajes | 0,5 ms | Una vez, al entrar |
| Hornear la lista de la noche | 2,8 ms (1920×1080: 5,7 ms) | Una vez por atracción (si faltaban portadas, se repite hasta 8 veces) |
| Entrar: apuntar lo que se enseña y hornear lo primero | 2,5 ms | Al entrar la atracción |
| Lo que calcula la rocola en cada cuadro | menos de 5 µs (la medida no lo distingue del ruido) | Cada cuadro |

- Lo horneado se hace en el reloj de la pantalla, **una cosa por vuelta como mucho**, nunca dentro del cuadro.
- La lista va a la tarjeta en **tres envíos** (las cinco filas juntas, las cinco barras juntas y sus brillos juntos),
  no uno por fila. La batería `envios` vigila que los envíos dentro de un bucle no crezcan: la primera versión mandaba
  uno por fila y la batería lo cazó.
- La lista a 1920×1080 ocupa unos 6 MB. Se suelta al salir de la atracción, igual que lo demás.

## Cómo se comprobó y qué falta

- **Pruebas automáticas.** La batería nueva `pruebas/atraccion.cs` corre con los dos perfiles:
  - Con tarjeta gráfica: 69 comprobaciones.
  - Sin tarjeta gráfica: 9. Los enganches no hacen nada (ni cuenta ni archivo) y la pestaña funciona.

  Entre las comprobaciones con tarjeta:
  - Con las ideas apagadas, el ciclo de portadas es la misma cuenta que la del cubo de siempre, en 732 momentos.
  - La ficha dice el disco que enseña el cubo en 134 momentos seguidos.
  - La cuenta de la noche, su archivo y lo que pasa con archivos de otra noche, rotos o a medio escribir.
- **Mutaciones.** Se rompió a propósito cada protección de la tanda (78 mutaciones) y la batería las cazó todas. Tres
  sobrevivieron al principio:
  - la pastilla "pedido N veces" quitada: la prueba contaba el color ámbar con un umbral demasiado bajo;
  - las dos comprobaciones de "hacen falta tres canciones": una era redundante (se quitó del código) y la otra ahora se
    prueba con tres canciones pedidas de las que una ya no está.
- **Las baterías de antes** se pusieron al día donde cambió algo a propósito: la pestaña Pantalla tiene tres hojas
  (`suenavivo`) y `pestanas` conoce los dos interruptores nuevos. Tandas completas en verde en las dos ramas.
- **Simulación, no comprobación real.** Con las mismas funciones del programa se compuso la atracción a 1024×768:
  - el escaparate;
  - el cambio de un disco a otro;
  - la lista entrando y entera.

  No es la tarjeta gráfica dibujando: el cubo, por ejemplo, está aproximado.
- **Lo que las pruebas no ven:** el dibujo mismo de la tarjeta (los rectángulos, los brillos y las barras). Las pruebas
  comprueban lo que la rocola le pasa a la tarjeta y en qué orden lo dibuja.
- **Sin comprobación visual real.** En este contenedor no hay Direct3D: hace falta mirarlo en el equipo del bar y en
  el de HD.
- **Sin probar en Windows XP.** La rama Modest compila con el lenguaje de .NET 3.5 (C# 3), pero no se ha probado en un
  Windows XP real.

## Lo que viene

5. **Estilo general.** Hecha: letra para leer de lejos y el color del género en todo. Ver
   [la guía de la tanda 5](PANTALLA_VIVA_ESTILO_2026-09-24.md).
