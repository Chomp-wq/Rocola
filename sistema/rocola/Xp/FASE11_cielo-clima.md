# Cielo vivo, grupo «nubes y clima»: DN6 y CL1 a CL5

27-11-2026. Rama del grupo (worktree `agent-af6efd87c7b9f1262`), sobre `b398050` (la rama principal con el nucleo del cielo y el grupo del sol, la luna y las
estrellas ya dentro). Es lo de la decision del dueno del 20-11-2026 («Me gustaron todas las ideas, haz todo!») para **DN6** (nubes pintadas con la luz del
sol o de la luna) y **CL1-CL5** (las nubes de afuera, la lluvia, los relampagos, la neblina y la pastilla del clima), sobre el nucleo
(`FASE11_cielo-nucleo.md`: el clima de Open-Meteo, la foto, la calma, los enchufes). Solo Rocola OS (`ROCOLA_OS`), solo la tarjeta (el dibujo por procesador
esta congelado). Las funciones `cielo` y `clima` siguen **APAGADAS de fabrica** (`DeFabrica = false`: no se tocaron; las enciende la junta final).
**Nada probado en el A4 ni con el Open-Meteo de verdad.**

## Que se hizo

| Archivo (todo en `RocolaOs/`) | Que lleva |
|---|---|
| `RocolaOs.CieloNubes.cs` | `RocolaOsCieloClimaVista` (lo que se quiere por clima, la luz, los colores de las nubes, la deriva; una vez por cuadro, suavizado en 1,5 s), `RocolaOsCieloNubes` (DN6: las nubes, su malla, las estrellas tras ellas con `RocolaOsCieloEstrella` y la pasada delante de la luna), `Tinte` (CL1: el cielo mas gris) |
| `RocolaOs.CieloLluvia.cs` | `RocolaOsCieloLluvia` (CL2, tambien la nieve), `RocolaOsCieloNubesRelampago` (CL3) y `RocolaOsCieloNeblina` (CL4) |
| `RocolaOs.CieloNubesEscaparate.cs` | CL5: los textos (`RocolaOsCieloClimaTextos`), el mensajero (`PonMensajero`, g136) y la pastilla horneada (`RocolaOsCieloClimaPastilla`, capa `Escaparate`) con el iconito del tiempo |
| `RocolaOs.CieloLluviaPrueba.cs` | la orden `rocola-xp --prueba-cielo-clima` (40 comprobaciones de las cuentas puras; `--pastilla=RUTA` guarda una hoja con las pastillas de cada tiempo, de dia y de noche) |
| `Pruebas/RocolaOs/cielo/` | `prueba-clima.sh` (la bateria del grupo) y `relampagos.py` (el tope de uno cada 6 s) |

**Sin ganchos propios** (el rango g320-g339 queda libre y entero) ni cambios en archivos comunes del nucleo: todo se enchufa con lo que el nucleo ya
daba (`PonPintor` en `TrasNieblas`, `ConLaLuna`, `TrasFondo` y `Escaparate`; `PonEstrella`; `PonTinte`; `PonMensajero`). `ganchos.py`: TODO BIEN.

## Lo que hace cada idea

- **La luz y el clima (`RocolaOsCieloClimaVista`).** Cada cuadro, una sola vez, de la foto del cielo: cuantas nubes (sin clima, o sin internet, o con el clima
  caducado: 0,22, un adorno; despejado 0,12; poco nublado 0,40; nublado 0,85; lluvia 0,90; aguacero y tormenta 1; la nubosidad de Open-Meteo si es mayor),
  que tan gris (0 a 0,9), cuanta lluvia, nieve y niebla, el viento y su sentido, y la luz del sol y de la luna (la altura de cada astro, la fase). Todo sube o
  baja en 1,5 s hacia lo de la foto (sin saltos al cambiar el clima ni con «Ver como se veria»). Hacia donde van las nubes y la lluvia: la direccion de
  Open-Meteo es de donde viene el viento; la rocola mira al sur y el este queda a la izquierda (como el sol): viento del oeste (270) = hacia la izquierda;
  del norte o del sur se mueven poco (0,3). Deriva de 3 a 30 px/s (km/h x 0,9; sin clima 3,5).
- **DN6 (`nubes=`).** 14 nubes de 8 manchas blandas cada una (un abanico de 10 triangulos con el alfa de 0,8 en el centro a 0 en el borde), sin texturas, con el
  color en los vertices: de la sombra (abajo, y al lado contrario del astro) a la luz (arriba, del lado del sol o de la luna). La luz sale de la hora del cielo
  (blanca de dia; del color de la raya de la paleta de §5 al bajar el sol, la luna azulada de noche). Una capa de atras (chicas, lentas, mas cerca del horizonte) y una de
  delante. Se pintan DETRAS de todo (capa `TrasNieblas`, detras de la aurora, las estrellas y la estrella de XP); las estrellas que quedan tras una nube se
  apagan (1 a 0,15, el cliente `RocolaOsCieloEstrella` del enchufe g127); y las nubes que pasan por el sitio de la luna de XP se pintan otra vez delante de
  ella (capa `ConLaLuna`, con un fundido segun cuanto la tapan). No bajan de 0,76 del horizonte.
- **CL1 (`clima=`).** Las nubes de afuera: con el clima de Open-Meteo sabido (y `nubes=1`: las del clima son las de DN6), cuantas segun la nubosidad y el tipo,
  grises y oscuras con la lluvia y la tormenta, el degradado del cielo mas gris (un tinte, `PonTinte`: el color mezclado hacia su gris y hasta un 20 % mas
  oscuro, nunca mas claro; nada en el suelo ni de noche cerrada; sube `TintesVersion` en pasos de 1/32 para que la imagen intermedia del cielo se repinte), y
  la deriva con el viento de verdad. Con `nubes=0` el clima solo pone lluvia, neblina y la pastilla.
- **CL2 (`lluvia=`).** Rayas en dos profundidades (un tercio delante: 22 px, 560 px/s, alfa 0,36; dos tercios atras: 11 px, 300 px/s, alfa 0,22), inclinadas con
  el viento (hasta 0,5 px de x por px de y con 25 km/h o mas), hasta 420; caen en la capa `TrasFondo` (detras de las portadas, el cubo y los textos; delante de la
  aurora y las estrellas) y se paran en el horizonte (la cabeza se recorta ahi). Llovizna 30 %, lluvia 45-80 %, aguacero 100 %. Con nieve, copos de 2,5 y 3,5 px
  que bajan despacio (55 y 95 px/s) y se mecen.
- **CL3 (`tormenta=`).** Con la tormenta, un relampago DENTRO de una nube a la vista (una mancha de luz sumada de 180/180/240 con alfa de hasta 0,26: +47 al color
  de una nube oscura, nunca un destello de pantalla): UN pulso suave (sube en 0,07 s, baja con constante de 0,2 s, acaba a los 0,9 s: nada de parpadeos), de un
  comienzo al siguiente de 6 a 16 s (nunca menos de 6), sin sonido; mientras alguien elige no empieza ninguno y los que ya van bajan con la calma.
- **CL4 (`neblina=`).** Una franja sobre el horizonte, de 0,80 del horizonte hacia la raya: alfa de 0 a 0,55 en el 0,848 del alto (el limite de la regla 3) y 0,70 en
  la franja fina de la raya (mas clara: la luz de la nube x 1,45), con un vaiven lento de 24 columnas (0,6 a 1). Detras de la aurora (`TrasNieblas`). Entera con la
  neblina de Open-Meteo (segun la visibilidad: menos de 1 km entera, 3 km o mas, 0,6); velo tenue con llovizna, lluvia y nieve.
- **CL5 (`clima_escaparate=`).** La pastilla «1:01 p. m. · 16 °C · Lluvia» con un iconito del tiempo (sol, luna, nube, lluvia, tormenta, neblina o nieve, de
  Skia) en una capsula oscura translucida con la letra de los mensajes del escaparate, arriba a la derecha (0,05 del ancho del borde y 0,06 del alto), solo con
  el escaparate del disco (con la ficha: no en la atraccion de siempre) y fuera cuando sale la lista de lo mas pedido (el cubo se va a esa esquina); se hornea en
  `Prepara` solo cuando cambia el texto (cada minuto) y aparece y se va en 0,6 s. Y un mensaje del clima entre los que XP turna bajo la ficha, tercero:
  «Afuera llueve (16 °C). ¡Pon musica y que llueva!» (uno por tipo de clima; 51 letras como mucho).

## Las seis reglas

1. **La noche es la de XP.** Sin nubes (`nubes=0`) y sin clima, a las 23:30 la cinta de la tarjeta sale igual que la de la rama principal (prueba «noche»). Con
   `nubes=1` o con clima, a proposito, el cielo de noche lleva nubes por la luz de la luna (la regla 1 lo permite).
2. **Nada cambia de lugar.** Las nubes, la lluvia y la niebla van detras; la pastilla va en un sitio donde el escaparate no pone nada.
3. **Siempre se lee.** Ningun color de nube pasa de luminancia 0,15 x sol + 0,075 x luna + 0,012 (menos con mal tiempo; 805 casos recorridos), ninguna raya de lluvia
   pasa de 0,17, la niebla del cuerpo es la luz de una nube y la de la raya solo sube en la franja fina; mezclado con alfa lo que sale es siempre menor que el mayor de
   los dos. Medida en capturas de la pared (sin la barra de arriba, la portada ni la luna), del escaparate y del reposo (ver las pruebas).
4. **Lo vistoso, donde se ve.** La pastilla y el mensaje, en el escaparate.
5. **Se calma al elegir.** Las nubes: deriva x calma y alfa x (0,5 + 0,5 calma); la lluvia: rayas x (0,35 + 0,65 calma) y velocidad x (0,55 + 0,45 calma); los
   relampagos no empiezan mientras la calma baja de 0,95 y los que van se multiplican por ella (comprobado con un boton cada 5 s).
6. **Interruptor y probarlo.** Cada idea con su clave (`nubes`, `lluvia`, `tormenta`, `neblina`, `clima_escaparate` y la funcion `clima` para CL1); todas apagadas =
   XP (cintas iguales).

## Las pruebas (`Pruebas/RocolaOs/cielo/prueba-clima.sh`)

Maquina compartida y muy cargada (carga de 30 a 37 con 4 nucleos): los tiempos de pared son de llvmpipe en esas condiciones. Datos inventados (120 canciones de ffmpeg en
20 artistas, una portada de degradado por artista, 8 teclas, sin Spotify), 1024x768, zona de Mexico, sin red (`ROCOLA_CLIMA_API` a un puerto sin nadie) y un Open-Meteo
falso (`clima_falso.py`) para un caso; el clima de cada caso con `ROCOLA_CLIMA` (despejado `5 %, 10 km/h`, nublado, neblina, llovizna, lluvia `3 mm/h, 28 km/h del oeste`,
tormenta `9 mm/h, 35 km/h`, nieve), `sinred` (ninguno), `caducado` (`rocola_clima.txt` de hace 5 h) y `falso`.

| Parte | Resultado |
|---|---|
| ganchos | `ganchos.py` TODO BIEN y ningun gancho en g320-g339 |
| orden | `rocola-xp --prueba-cielo-clima` **40/40** (lo que se quiere por clima, la deriva y el viento, la luz de las nubes en 805 casos, la lluvia y su inclinacion, el tope de 6 s, la niebla, los textos y el mensaje) |
| apagado | la cinta de la pared igual a la de la rama principal sin `ROCOLA_OS`, con las cuatro funciones en 0 y con `cielo=1 clima=1` y las ideas de este grupo en 0 con una tormenta forzada; los controles (nubes con tormenta, lluvia sola) SI cambian la cinta |
| noche | a las 23:30, con cielo y clima encendidos, `nubes=0` y sin clima: la cinta igual a la de la rama principal |
| escenas | pared y escaparate a las 13:00, 17:40 y 22:00 con despejado, nublado, neblina, llovizna, lluvia, tormenta, nieve, sin internet, caducado (5 h: la bitacora dice que no se usa) y el Open-Meteo falso (llego `/v1/forecast`), mas el reposo con lluvia y tormenta; 3 capturas por caso, «ver como se veria» 3 veces en la bitacora |
| la regla 3 | medida en cada captura (`contraste.py`): la pared (sin la barra de arriba, la portada ni la luna) 0,070 a 0,095 de luz maxima (contraste del blanco 7,2:1 o mas en lluvia y tormenta), el escaparate bajo y a la derecha del cubo 0,032 a 0,105 (6,8:1 o mas), el reposo sobre la raya 0,070 a 0,116 (6,3:1 o mas); todas por debajo de 0,17 |
| relampagos | tormenta 112 s: 9 relampagos (comienzos 18,3 24,9 41,8 57,4 69,5 77,6 86,2 93,2 104,2 s), el hueco menor 6,56 s (otra corrida: 6,51); con un boton cada 5 s (alguien elige): ninguno |
| reloj (NativeAOT, 70 s con lluvia a las 17:40, pared, ficha y escaparate; p50/p95/p99/max en ms del trabajo del cuadro) | encendido: 3,11 / 6,62 / 35,0 / 273 (vuelta 1) y 3,18 / 6,36 / 12,8 / 128 (vuelta 2); apagado: 1,66 / 2,69 / 12,0 / 167 y 1,72 / 2,98 / 11,4 / 94. Con lluvia y nubes la mediana sube unos 1,5 ms y el p95 unos 3,5 ms **en llvmpipe, en una maquina compartida con carga de 30 a 37**: los maximos de las dos columnas son de esa carga; no es el A4 |

La bateria entera son dos corridas de `prueba-clima.sh`: `ganchos orden apagado noche` (9 comprobaciones) y `escenas relampagos reloj` (94). Las capturas
(fuera del commit, en el worktree, `sistema/rocola/Xp/capturas-clima/`): la tormenta y la neblina en el escaparate con la pastilla y el mensaje, la lluvia y la tormenta
en la pared y una hoja con la pastilla de cada tiempo de dia y de noche.

## Lo no probado y lo pendiente

- **Nada en el A4**: ni como se ve ni lo que cuesta en su tarjeta. Con el Open-Meteo de verdad tampoco (solo el falso y el forzado).
- **La pastilla y el mensaje** se vieron en una captura del escaparate (`capturas-clima/a90.png` y las del barrido, `tormenta-escaparate-1740.png` y `neblina-escaparate-1300.png`).
- **La luna B** (DN3 en su camino) no queda tapada por las nubes de delante (solo la de XP en su sitio); y en el reposo con musica las nubes no cambian de sitio.
- **Los relampagos** son una mancha de luz dentro de la nube: sin rayo dibujado, a proposito (pocos destellos, regla 5 y la del 9-10 de no abusar).
- **Las capturas** de las pruebas no van en el commit (estan en el worktree: `prueba-clima*/capturas`).

> **27-11-2026, junta final:** las cuatro funciones del cielo vivo van ahora ENCENDIDAS de fabrica (`DeFabrica = true`); lo que arriba diga «apagada de fabrica» o «en esta version» era de la version anterior. Sus ideas que costaban (VI1, VI2, VI4, VI6) y `luna_camino` siguen apagadas.
