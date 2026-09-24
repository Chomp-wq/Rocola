# La pantalla viva, tanda 2: dentro del disco

Cambio del 24 de septiembre de 2026, compartido por HD y Modest. Son las ideas del lienzo de bocetos para el
interior de un disco. Solo cambian el dibujo por tarjeta gráfica. El renderizador por procesador sigue congelado y
sale exactamente igual: una batería lo comprueba píxel a píxel contra el programa compilado sin tarjeta gráfica.

Cada idea tiene su interruptor en Ctrl+F3 → **Pantalla**, en una tarjeta nueva, **Dentro del disco**, al lado de la
de la pared. Todas vienen encendidas y se guardan en `rocola_config.txt`. Sin tarjeta gráfica los interruptores se
guardan, pero no cambian nada de lo que se ve.

Solo se ven mientras la tarjeta gráfica dibuja el disco. Si el disco pasa a pintarlo el procesador —un vídeo que lo
tapa, Ctrl+F11 o la tarjeta apagada—, la lista vuelve en el acto a la de siempre.

El código nuevo está en `Rocola/DiscoVivoGpu.cs`. La lista del disco y la cápsula de la cola (`Form1.Vistas.cs`), el
cuadro de la tarjeta (`D3D9Star.cs`) y el reloj de la pantalla llevan solo los enganches.

## 1. Vinilo que asoma — `vinilo_disco=`

- Si suena una canción del disco que tienes abierto, un vinilo sale de detrás de la portada que gira. La portada se
  aparta un poco hacia su lado, como una funda que suelta el disco, y así se ve la etiqueta.
- La etiqueta lleva la portada de lo que suena, con un aro del color de su género. El disco lleva surcos finos y
  tres separaciones entre canciones.
- Gira a 33 ⅓ vueltas por minuto. Gira con lo que ha salido: arranca y se para como un tocadiscos.
- Sale en 650 ms y se guarda en 450 ms, arrancando y frenando suave. Si cambia de idea a medio camino, vuelve desde
  donde está.
- Nunca llega a menos de 12 px de la lista: no la tapa. En 1024×768 el cubo mide 310 px, el vinilo 205 y asoma
  103 px; la portada se aparta 50.
- Encima lleva dos brillos quietos, como la luz del local: el disco gira y el reflejo no.
- Un vídeo no saca vinilo.
- Si deja de sonar ese disco, el vinilo se guarda con su etiqueta: la de la canción nueva se pone cuando ya está
  dentro, no a medio camino.
- Se prepara al cambiar de canción, no al entrar al disco, que es cuando vuela la portada y un tropiezo se vería. Los
  surcos se dibujan una vez por tamaño; por canción solo cambia la etiqueta.

## 2. Lista grande — `lista_grande=`

- Unas nueve canciones por página, con la letra más grande. En 1024×768 las filas pasan de 30 a 50 px y la letra de
  13 a 16,7 puntos. En 1920×1080 la letra llega a 26.
- El circulito del número y el hueco de la duración crecen con la letra.
- La pastilla del género sale en todas las filas de la página o en ninguna. Sale solo si en todas el título cabe
  entero a su lado: una fila sin género entre otras que lo llevan haría pensar que esa canción es de otra cosa.
- Al encenderla, o al cambiar el tamaño de la pantalla, se llega a la página de la canción elegida sin hojear.

## 3. Ecualizador en la que suena — `eq_sonando=`

- La pastilla de SONANDO lleva cuatro barritas en lugar del triangulito.
- Cada barra va a su ritmo, como en el boceto, y la música la empuja: la primera con los graves, la última con los
  agudos. Con un golpe suben casi al instante y después bajan más despacio.
- Las dibuja la tarjeta encima de la lista, así que la página no se vuelve a hornear por ellas.
- No salen mientras la página se desliza, con el buscador encima, ni si la que suena ya es otra.

## 4. En cola, con los minutos — `cola_minutos=`

- La pastilla verde dice "EN COLA 3 · en ~9 min". Si falta menos de un minuto, "ya casi".
- Es la misma cuenta que el aviso de tu turno ("Tu canción es la 3.ª - en ~9 min"). La batería compara las dos en
  548 esperas distintas.
- Si no se sabe lo que dura alguna de las de delante, o suena un vídeo, no se inventan minutos: dice "EN COLA 3",
  como siempre.
- Se repasa cada medio segundo, y la lista se vuelve a hornear solo si cambió algún minuto: en la práctica, una vez
  por minuto.
- Manda el título. Los minutos salen si el título cabe entero, o si al menos le queda el 30 % de su sitio.

## 5. Ver adónde va la canción — `vuelo_cola=`

- Al pedir una canción dentro del disco, su boleto (la portadita y el título) sale calcado encima de su fila. La fila
  se pone verde y se despega.
- Después sube un poco en arco y cae dentro de la cápsula "En cola", encogiéndose.
- Si no sonaba nada, la canción no espera: vuela a la tarjeta de lo que suena.
- Mientras vuela, la cápsula enseña el número de antes. Al llegar da un saltito, destella y el número sube rodando.
- Dura 820 ms y aterriza a los 705.
- En la pared sigue volando la portada, como siempre, y el aviso de tu turno sale igual que antes.

## Lo que cuesta

Medido en este contenedor, con Mono. Sirve para comparar unas cosas con otras; los milisegundos del bar serán otros.

| Qué | Cuánto | Cuándo pasa |
|---|---|---|
| Hornear la página de la lista grande, con todo | 0,95 ms (la de siempre: 0,99 ms) | Al cambiar algo de la lista |
| Decidir el género de toda la página | 0,02 ms | Al hornear la página |
| Preparar el vinilo | 3,6 ms (la primera vez, con los surcos: 6,6 ms) | Al cambiar de canción |
| Preparar el boleto | 4,1 ms | Al pedir una canción |
| Barritas y vuelo del boleto | 1 a 2 µs | Cada cuadro |

- En un cuadro quieto no se hornea ni se sube nada a la tarjeta.
- La lista grande tiene menos filas por página, así que hornearla cuesta lo mismo que la de siempre.

## Cómo se comprobó y qué falta

- **Pruebas automáticas.** La batería nueva `pruebas/discovivo.cs` corre con los dos perfiles:
  - Con tarjeta gráfica: 128 comprobaciones. Entre ellas, la lista del disco y la cápsula de la cola pintadas por
    el procesador se comparan píxel a píxel contra el programa compilado sin tarjeta: salen idénticas.
  - Sin tarjeta gráfica: 5, solo la pestaña y que no haya nada más.
  - Se rompió a propósito cada protección de la tanda (62 mutaciones) y la batería las cazó todas. Dos
    sobrevivieron al principio: una línea que no hacía falta (se quitó) y el punto de salida del viaje al salir del
    disco, que la batería solo miraba en el código (ahora lo comprueba de verdad).
  - Tandas completas en verde en las dos ramas.
- **Simulación, no comprobación real.** Con las mismas funciones del programa se compuso el interior de un disco a
  1024×768, el vuelo del boleto y la cápsula al llegar. No es la tarjeta gráfica dibujando.
- **Sin comprobación visual real.** En este contenedor no hay Direct3D: hace falta mirarlo en el equipo del bar y en
  el de HD.
- **Sin probar en Windows XP.** En la rama Modest compila con el lenguaje de .NET 3.5 (C# 3), pero no se ha probado
  en un Windows XP real.

## Lo que viene

3. **Lo que suena y los créditos.** Hecha: ver [la guía de la tanda 3](PANTALLA_VIVA_SUENA_2026-09-24.md).
4. **Modo atracción.** El escaparate y lo más pedido esta noche.
5. **Estilo general.** Letra para leer de lejos y el color del género en todo.
