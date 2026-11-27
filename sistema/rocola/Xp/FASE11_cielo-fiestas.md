# Cielo vivo, grupo de las fiestas: FI1 a FI6

27-11-2026. Rama del grupo «fiestas» del cielo vivo, sobre `e94dcd6` (la rama principal con el nucleo del cielo: `FASE11_cielo-nucleo.md`). Es lo de
`PLAN_CIELO_VIVO.md` §6 (el calendario y sus colores) y §1 (las seis reglas), encima de los enchufes del nucleo: las seis ideas de las fiestas del
cielo de la rocola. Solo Rocola OS (`ROCOLA_OS`), solo la tarjeta (el dibujo por procesador sigue congelado), sin internet. **La funcion `fiestas`
sigue APAGADA de fabrica** (`DeFabrica = false` en `RocolaOs.Cielo.cs`, decision de la junta 4): no se tocó; se enciende con `fiestas=1` en
`rocola_os.txt` y la junta final decide cuando vuelve a encenderse. **Nada probado en el A4.**

| Idea (clave) | Que hace | Donde |
|---|---|---|
| FI1 la aurora de la fecha (`aurora_fiesta`) | los colores de la aurora de XP son los de la fecha, en bandas de izquierda a derecha, con la luz y la forma que XP le da | la aurora (g128, nucleo), en todas las escenas |
| FI2 los adornos del horizonte (`horizonte_fiesta`) | una fila baja de adornos con su base en la raya del horizonte de XP | capa `TrasAurora`, en todas las escenas |
| FI3 la fugaz de la fecha (`fugaz_fiesta`) | la fugaz dorada de XP (moneda o cancion puesta) sale con los colores de la fecha; no se anaden fugaces | g139 (nucleo) y g340-g341 |
| FI4 los fuegos (`fuegos`) | el Grito (15-9 a las 23:00), Ano Nuevo (1-1 a las 00:00, tras una cuenta de 10 s) o la hora de `rocola_fechas.txt` | capa `TrasFondo`, solo el escaparate y el reposo |
| FI5 el escaparate de la fecha (`escaparate_fiesta`) | papel picado arriba, globos, corazones o la bandera en las orillas y el mensaje de la fecha entre los de XP | capa `Escaparate` y g136 (nucleo), solo la atraccion |
| FI6 tus fechas (`fechas_propias`) | cumpleanos y aniversarios de `rocola_fechas.txt`: globos, confeti y «¡Feliz cumpleaños, NOMBRE!» | `Escaparate`, g136 y g139 |

## Que se hizo

| Archivo (todo en `RocolaOs/`, salvo lo que se dice) | Que lleva |
|---|---|
| `RocolaOs.CieloFiesta.cs` | El registro de todo lo del grupo (un `[ModuleInitializer]`, sin trabajo); los colores de hoy; FI1 (`RocolaOsFiestaAurora`, la raya del dia `TineRaya`); FI3 y FI6 la fugaz (`RocolaOsFiestaFugaz`); el mensajero de FI5 y FI6; y lo que necesitan de lo privado de XP (partes de `GpuPanel` y `GpuStarfield`) |
| `RocolaOs.CieloFiestaFormas.cs` | La geometria plana de los adornos (discos con el borde suavizado, lineas, estrellas, corazones, flores, notas, globos, banderines de papel picado, farolitos): malla de colores en los vertices, sin texturas (nada que hornear ni que soltar al reiniciar la tarjeta) |
| `RocolaOs.CieloFiestaPinta.cs` | FI2 (`RocolaOsFiestaHorizonte`) y FI5 y FI6 (`RocolaOsFiestaEscaparate`: papel picado, globos, corazones, bandera, confeti) |
| `RocolaOs.CieloFiestaFuegos.cs` | FI4 (`RocolaOsFiestaFuegos`): las rafagas, la cuenta de siete segmentos y el tope de 3 destellos por segundo |
| `Codigo/D3D9Star.cs` | dos ganchos de una linea, **g340** y **g341** (la estela de la fugaz); `RocolaOs/GANCHOS.tsv` los apunta |
| `RocolaOs.CieloDia.cs` (**archivo comun del nucleo, tocado**) | `RayaTinte` (un delegado) y su llamada en `Raya`: 4 lineas, para que FI1 tina la raya del dia (ver «DN5 y FI1») |
| `Pruebas/RocolaOs/cielo-fiestas/` | `prueba.sh` (la bateria) y `datos.sh` (la musica inventada) |

## Las reglas, una por una

1. **La noche es la de XP.** De noche el cielo no cambia (nada de DN); lo de las fiestas SI se ve de noche (la fiesta es lo que «una idea encendida
   que de noche si se ve» deja pasar): la aurora con los colores de la fecha, los adornos, los fuegos. Con la fiesta apagada (o sin ella hoy) es XP.
2. **Nada cambia de lugar.** Nada de XP se mueve: los adornos van detras de las portadas, del texto y de la interfaz (FI2 y FI4 en capas de
   antes de la interfaz) o en las orillas libres del escaparate (FI5 y FI6, abajo).
3. **Siempre se lee.** Lo claro de FI2 mide 16 px como mucho sobre la raya (en la pared acaban a 648-664 de 768, y los titulos de la ultima fila
   acaban a 652; en el reposo con la raya en 525, no pasan de 509); el papel picado cuelga en los 34 px de arriba de la atraccion (ni la cabecera
   de «Lo mas pedido» ni el cubo empiezan antes de 38); globos, corazones y confeti van en las dos orillas del escaparate (a un 3,3 % del ancho
   de cada lado), donde ni el cubo, ni la ficha, ni el mensaje, ni la lista de lo mas pedido llegan; los fuegos solo donde no hay texto (abajo). La
   luz del cielo de dia la mide la bateria (`contraste`).
4. **Lo vistoso, donde se ve.** El papel picado, los globos, los corazones, la bandera, el confeti y los fuegos solo salen en el escaparate (la
   atraccion; los fuegos tambien en el reposo con musica). En la pared, el buscador y el disco abierto solo la aurora, la fila del horizonte y la
   fugaz.
5. **Se calma al elegir.** Todo lo que se mueve (sacudida de los adornos, titileo, subida de globos, corazones y notas, confeti, la bandera, las
   rafagas de los fuegos) lleva la calma de la escena (`Escena.Calma`, 0,35 mientras alguien elige): la amplitud y la velocidad bajan; los fuegos
   tienen ademas la mitad de rafagas y mas apagadas. Lo de XP (la aurora, las estrellas, la fugaz) no cambia por esto.
6. **Interruptor y probarlo.** Cada idea con su clave (arriba); todas apagadas o sin `ROCOLA_OS` = XP identica (cintas de la tarjeta, abajo).

## FI1: la aurora de XP con los colores de la fecha

Cliente de `PonAurora` (g128): para cada columna de la aurora, despues de que XP calcule las suyas (cortina, filo, pie y suelo), se cambia el
color por el de la fecha que toca a esa columna: los colores de la fiesta de hoy (los de dos fiestas el mismo dia, como las posadas y Navidad el 24,
se juntan) en bandas parejas de izquierda a derecha con un paso suave entre una y otra (el tricolor es una bandera: verde, blanco y rojo). Cada
punto conserva la luz que XP le dio (su canal mas fuerte): la forma, el ondeo, el filo y el brillo son los de XP y siguen a la musica; solo el tono
cambia. Al cambiar de fecha o apagarse la idea, la aurora se rehace (`FirmaAuroraExtra`, g129 del nucleo). Sin fiesta hoy, XP. Un `fiesta` de
`rocola_fechas.txt` (una fiesta propia) tambien tine la aurora con sus colores.

### DN5 y FI1 (la nota que dejo el nucleo)

DN5 (la raya del horizonte con la luz de la hora) pisaba de dia el color que FI1 pone al pie de la aurora, porque corre despues de los clientes
de la aurora y mezcla la raya hacia el color de la hora por el peso del dia. **Decision: de noche la raya es la de la aurora, o sea con los colores
de la fecha; de dia DN5 manda en la luz y FI1 en el tono**: el color de la raya del dia es 40 % el de la hora y 60 % el de la fecha de esa columna,
reescalado a la luminancia lineal que DN5 le dio menos un 10 % (sin pasar de 255 por canal): la regla 3 se midio con esa luz y no cambia.
Va por un delegado (`RocolaOsCieloDia.RayaTinte`, 4 lineas en `RocolaOs.CieloDia.cs`, el archivo del nucleo que se toco) que FI1 llena en su
registro; sin FI1, nadie lo llena y DN5 es la de antes.

## FI2: la fila baja de adornos sobre la raya del horizonte

Un pintor de la capa `TrasAurora` (delante de la aurora, su raya y su reflejo; detras del grano, las estrellas, las portadas y todo texto):
adornos de 12 a 16 px de alto con su base en la raya del horizonte de XP (la `y` de la escena: 665 en la pared, 525 en el reposo con musica),
uno cada 52 px con una sacudida al azar. Por fiesta (la columna «Ademas» de §6):

| Fiesta | Adorno |
|---|---|
| Candelaria (rojo, blanco) | farolitos de papel con su estaca y su resplandor (de noche) |
| Reyes (oro, azul) | la estrella guia (grande, con cruz y halo, a dos tercios del ancho) y estrellitas de cuatro puntas |
| Dia de la Mujer (morado) | flores moradas con su tallo |
| Dia de las Madres (rosa, blanco) | flores rosas y blancas |
| Muertos (cempasuchil, morado) | cempasuchil (8 petalos) y veladoras con su llama que titila |
| Guadalupana (azul, rosa) | rosas |
| Posadas (naranja, oro) | farolitos y bombillas |
| Navidad (rojo, verde, oro) | una guirnalda: cable que cuelga en tramos de 6 y bombillas que se encienden en cadena |
| Santa Cecilia (oro, rojo) | notas que suben desde la raya con la aurora (115 px en el escaparate y el reposo, donde el cielo esta libre; en las demas escenas apenas se despegan 3 px) |

El 24-12 (posadas y Navidad) los adornos de las dos se alternan. Fiestas sin adorno en el horizonte (las de §6 «en el escaparate») no ponen nada aqui.
Los resplandores (luz sumada) valen 0,3 de dia y 1 de noche.

## FI3: la fugaz dorada de XP con la estela de la fecha

La fugaz de festejo de XP (la que sale al poner una moneda o una cancion; `Festeja`) se queda como es (misma salida, misma vida, mismo ancho) y no se
anaden fugaces. Cambia el color: el cliente de `PonFugaz` (g139) da el color de la cabeza (uno de los de la fecha, rotando entre las fiestas de
hoy: cada fugaz, el siguiente) y, en el byte de arriba del color (que XP no lee: `rgb = s.Col & 0x00FFFFFF`), una marca que dice cual de ocho
tablas lleva los otros dos colores de su estela. Los dos ganchos nuevos, **g340** (`tailC`) y **g341** (el vertice del otro lado de la estela:
`tailD`), ponen un color en cada lado de la punta de la estela: la estela se abre de la cabeza hacia otros dos colores de la fecha, apagandose
como siempre (el tricolor: cabeza de uno, estela de los otros dos). Sin ROCOLA_OS o sin marca (cualquier fugaz de XP), `tailD` es `tailC` y los
vertices son los de XP.

## FI4: los fuegos artificiales

Solo el Grito (15-9, 23:00) y Ano Nuevo (1-1, 00:00, y desde las 23:59:50 la cuenta de 10 s con numeros de siete segmentos dorados que laten al
cambiar cada segundo), o lo que diga `rocola_fechas.txt` (`fuegos;HH:MM` o `fuegos;no`). Duran 4 minutos. Capa `TrasFondo`: detras de las
portadas, de la interfaz y de todo texto. «Nunca detras de un texto» se cumple **por donde se pintan**, no por cuidado de cada rafaga:

- **La atraccion con su ficha**: solo en la franja de arriba, entre el papel picado (34 px) y el cubo y la ficha (de 200 px abajo): las rafagas
  nacen con su centro y su radio dentro de esa franja (60 a 135 px de radio) y la chispa que cae mas alla del limite se apaga antes de llegar
  (un desvanecido de 18 px). Cuando la lista de lo mas pedido tapa la ficha (su cabecera llega hasta arriba), no hay fuegos: se desvanecen con la
  ficha (`UiAlfa`) y vuelven con ella.
- **El reposo con musica** (la franja AHORA SUENA abajo): todo el cielo de arriba hasta 14 px sobre la raya que subio.
- **La pared, el buscador y el disco abierto**: ningun fuego (no hay cielo libre de texto: la pared es una rejilla con titulos, el disco una lista).

Cada rafaga son 36 chispas en dos capas (12 cerca, 24 lejos) que frenan al salir y caen un poco; cada una deja dos colitas; el estallido pinta un
resplandor chico (de 0,28 de luz como mucho y 0,22 s) y nunca destellos de pantalla. **Tope de 3 destellos por segundo, duro**: cada rafaga nueva
es un destello, y el codigo no deja nacer una si las tres anteriores caben en un segundo; ademas el ritmo normal es una cada 1,1 a 2,2 s, con tres
seguidas (cada 0,45 s) al empezar. Con la calma, las rafagas se espacian entre 1/calma (2,9 veces mas) y valen `calma` de luz. Hay como mucho 4
rafagas a la vez (un arreglo fijo; ningun cuadro reserva memoria). Los colores son los de la fecha (el Grito: tricolor; Ano Nuevo: oro y plata).
Las pruebas: `ROCOLA_FUEGOS_LOG=archivo` apunta cada nacimiento y `ROCOLA_FUEGOS_CALMA=0.35` fija la calma (una tecla saca del escaparate y no se
puede probar de otra manera).

## FI5: el escaparate de la fecha

Un pintor de la capa `Escaparate` (despues de lo de XP) solo con la atraccion en pantalla (`Modo 4`), con entrada de 1,2 s:

- **Papel picado** (con cualquier fiesta del calendario o una `fiesta` propia): una cuerda que cuelga en tramos de 6 banderines, en los 34 px de
  arriba, con banderines de los colores de la fecha (cuerpo con dientes abajo, un rombo y dos puntos mas claros de «calado») que se mecen.
- **Segun la fiesta** (§6): globos que suben por las dos orillas (Dia del Nino, del Padre), corazones que suben y se apagan (Amor y amistad), la
  bandera de Mexico en un asta de la orilla izquierda, ondeando (Dia de la Bandera).
- **El mensaje** (por el enchufe g136, con la misma letra y la misma forma de los de XP): el de la fecha (el de fabrica de §6 o el de `rocola_fechas.txt`
  con `mensaje`) entra en el segundo lugar de los que XP turna; ya van en la lista que XP hornea al entrar la atraccion.

Las orillas: a un 3,3 % del ancho de cada lado (34 px de centro en 1024), con 12 px de ancho de globo y sacudida: ni el cubo (desde el 8 %), ni
la ficha, ni la lista de lo mas pedido (desde el 7 %), ni la estrellita del escaparate llegan ahi.

## FI6: tus fechas (rocola_fechas.txt)

`rocola_fechas.txt` (en la carpeta de datos, `/rocola/datos/xp`; lo lee el hilo del cielo cuando cambia, `RocolaOs.CieloClima.cs`): UTF-8, finales
LF o CRLF, un renglon por fecha, `DD-MM;tipo;texto`; `#` empieza un comentario; un renglon que no vale se salta (la bitacora dice cuales, una vez).
Se edita con Ctrl+F4 -> Archivos o, cuando exista, la hoja Imagen (la orden «ajuste» del buzon no lo toca: no es una clave).

| Tipo | Texto | Efecto |
|---|---|---|
| `cumple` | el nombre | ese dia, en el escaparate (FI6): globos y confeti en las orillas, y el mensaje «¡Feliz cumpleaños, NOMBRE!» **en primer lugar** de los que XP turna; la fugaz de festejo sale con los colores de feria (rosa, oro, celeste, verde, violeta) |
| `aniversario` | el nombre | lo mismo con «¡Feliz aniversario, NOMBRE!» |
| `fiesta` | el nombre, y si quieres sus colores `\|#rrggbb,#rrggbb` | una fiesta mas del calendario (sin adornos): FI1 la aurora y FI3 la fugaz con sus colores (por omision oro y azul), FI5 el papel picado y el mensaje «¡Hoy es NOMBRE!» |
| `quitar` | la clave de la fiesta (`navidad`, `muertos`...) o su nombre; sin texto, todas las de ese dia | la quita del calendario ese dia |
| `mensaje` | el texto | el mensaje del escaparate de ese dia (FI5) en lugar del de fabrica; varios se turnan |
| `fuegos` | `HH:MM` o `no` | la hora de los fuegos de ese dia (FI4), o sin fuegos |

```
# ejemplo
16-03;cumple;Juan
02-08;aniversario;El Billar
12-10;fiesta;Mi fiesta|#ff0000,#ffffff
31-10;quitar;muertos
16-09;mensaje;¡Viva México! Pon tu ranchera favorita
15-09;fuegos;22:30
```

Las claves de `quitar`: `anio_nuevo`, `reyes`, `candelaria`, `amor`, `bandera`, `mujer`, `nino`, `madres`, `padre`, `independencia`,
`muertos`, `revolucion`, `santa_cecilia`, `guadalupana`, `posadas`, `navidad`. (El formato lo implementa el nucleo, `RocolaOs.CieloFiestas.cs`;
aqui solo se lee.)

## Ganchos

| Gancho | Archivo:linea | Que da |
|---|---|---|
| **g340** | `Codigo/D3D9Star.cs:1451` | `tailC = RocolaOsColaFugaz(s.Col, rgb, 0), tailD = ...(…, 1)`: el color de la punta de la estela de la fugaz (XP: el de la cabeza) |
| **g341** | `Codigo/D3D9Star.cs:1453` | el vertice del otro lado de la estela lleva `tailD` (XP: `tailC`) |

Todo lo demas son enchufes del nucleo (g128, g129, g130-g139). `ganchos.py` dice TODO BIEN. **Archivo comun tocado:** `RocolaOs.CieloDia.cs`
(`RayaTinte`, 4 lineas); lo demas son archivos nuevos `RocolaOs.CieloFiesta*.cs`.

## Pruebas

`Pruebas/RocolaOs/cielo-fiestas/prueba.sh` (cabecera: partes y variables) con los datos de `datos.sh` (musica inventada: 16 artistas x 2 discos de
tonos de 300 s con portadas de colores, y `rocola_teclas.txt`), la rocola entera fuera de pantalla (1024x768, llvmpipe, la tarjeta de prueba con
reloj), zona America/Mexico_City, sin red:

**TODO BIEN, 85 comprobaciones** (corrida completa de `prueba.sh` sobre la version final; JIT):

- `ganchos`: ganchos.py TODO BIEN (112 archivos comparados con 8c8cba0, 28 con ganchos); g340 y g341 en el manifiesto.
- `apagado`: la cinta de la tarjeta (`cielo/cinta.py`) de la pared del 25-12 igual a la de la rama principal sin ROCOLA_OS, con `fiestas=0` y
  con `fiestas=1` y sus seis claves en 0 (IGUALES las tres); el control (todo encendido) distinta (raya de 120/83/201 a 157/146/90 y mas mallas).
- `escenas`: las 16 fechas del calendario, de noche: la pared a los 24 s y el escaparate a los 58 s; la bitacora dice la fiesta de hoy en cada
  una; el mensaje de la fecha entra entre los del escaparate (5 mensajes).
- `reposo`: las 16 fechas con musica sonando por «Ver como se veria» (16 capturas).
- `fuegos`: el Grito 8 destellos; Ano Nuevo 9 con la cuenta de 10 s y como mucho 3 en un segundo (el tope); con la calma en 0,35, 4 frente a 8
  en los primeros 14 s; con `fuegos=0` y el 15-9 a las 20:00, ninguno.
- `cumple`: cumpleanos y aniversario (con sus 2 mensajes propios), fiesta propia con colores, `quitar` (el 24-12 queda solo posadas), mensaje propio.
- `fugaz`: ocho capturas de la moneda; ademas, con la cinta, los vertices de la fugaz del 16-9 llevan el verde, el blanco y el rojo de la bandera.
- `contraste` (regla 3, 13:00): la pared, el escaparate y el reposo de las 16 fechas, todas <= 0,17 (el reposo es lo mas justo).
  Dos arreglos que salieron de aqui: la raya de dia de FI1 se reescala en luminancia LINEAL (no en sRGB) con un 10 % de margen, y las flores
  (Madres, Mujer, Guadalupana) son mas bajas y 10 % mas oscuras: sus petalos blancos pasaban de 0,17 en la fila justo sobre la raya del reposo.

Las capturas (1024x768): hojas de miniaturas en `Pruebas/RocolaOs/cielo/fiestas/` (`pared-noche.jpg`, `escaparate-noche.jpg`, `reposo-noche.jpg` y las
de dia, `fuegos.jpg`, `cumple.jpg`, `fugaz.jpg`), y a tamano completo `fuegos-grito-3.png`, `fuegos-nuevo-cuenta-2.png` y `cumple-escaparate-2.png`.
`montaje.py` las arma de las capturas de `prueba.sh`.

**Cuidado al repetirla**: la prueba es sensible a la carga (con mas de dos o tres corridas a la vez los cuadros se espacian y los fuegos nacen
menos); no se midio el reloj de los cuadros (p95/p99 de §7) ni se publico en NativeAOT: queda para la junta.

## Lo no probado y lo pendiente

- **Nada en el A4**: ni como se ve, ni lo que cuesta en su tarjeta (el reloj es de llvmpipe, en una maquina compartida). Los adornos son de 12 a
  16 px: en el A4 (1024x768) pueden verse chicos o grandes; el 1,3 x de su escala (`RocolaOsFiestaHorizonte`, `s * 1.3f`) es lo que se mueve.
- **Las letras de XP (Microsoft) no estan en este contenedor**: las capturas salen con DejaVu Sans, que es lo que usa la rocola sin las de XP;
  el mensaje y los titulos se ven con otra letra en el A4.
- **La hoja «Imagen» de Ctrl+F4 no existe** (es de despues): las fechas se editan a mano o en Archivos; las pruebas usan `ROCOLA_CIELO_FECHA` y
  «Ver como se veria» por el buzon (`/run/rocola-cielo-ver`).
- **El escaparate con su ficha de canciones («vitrina»)** no se vio en las capturas (aqui el cubo no llega a tener portadas que ciclar): el
  escaparate que se capturo es el de «N canciones / toca un boton para elegir»; las orillas y la franja de arriba estan libres en los dos y en la
  lista de lo mas pedido por su geometria (`CuboEscaparate`, `ColumnaEscaparate`, `CuboEsquina`, `GeometriaTop`), pero el mensaje de la fecha
  horneado por XP solo se comprobo por la bitacora (`escaparate: N mensajes ...`) y no pintado.
- **La lista de lo mas pedido** con los fuegos y el papel picado encima no se capturo (hace falta `rocola_semana.txt` con tres canciones).
- **NativeAOT y el reloj de los cuadros**: no se publico ni se midio (ver «Pruebas»).
- **Los fuegos solo con una calma fija** (`ROCOLA_FUEGOS_CALMA`): con una tecla de verdad la rocola sale del escaparate. En el reposo, una tecla
  tambien lo despierta; el 35 % se vio por la cuenta de nacimientos, no en una captura.
- **Santa Cecilia, «notas que suben con la aurora»**: se hizo como notas que suben desde la raya (la aurora es de XP y no se movio).
- **Las fiestas de dos dias o semanas** (Muertos del 31-10 al 2-11, posadas del 16 al 24-12) llevan el mismo adorno todos sus dias.

> **27-11-2026, junta final:** las cuatro funciones del cielo vivo van ahora ENCENDIDAS de fabrica (`DeFabrica = true`); lo que arriba diga «apagada de fabrica» o «en esta version» era de la version anterior. Sus ideas que costaban (VI1, VI2, VI4, VI6) y `luna_camino` siguen apagadas.
