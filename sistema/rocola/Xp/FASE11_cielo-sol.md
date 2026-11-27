# Cielo vivo, grupo «sol, luna y estrellas»: DN2, DN3 y DN4

27-11-2026. Rama del grupo (worktree `agent-ac19141a359782643`), sobre `e94dcd6` (la rama principal con el nucleo del cielo ya dentro). Es lo de la
decision del dueno del 20-11-2026 («Me gustaron todas las ideas, haz todo!») para las ideas **DN2** (el sol en su camino), **DN3** (la luna) y
**DN4** (las estrellas se encienden de una en una), sobre el nucleo (`FASE11_cielo-nucleo.md`: sol, luna, ciudad, calendario, calma, enchufes,
DN1 y DN5). Solo Rocola OS (`ROCOLA_OS`), solo la tarjeta (el dibujo por procesador esta congelado y no se toca). La funcion `cielo` sigue
**APAGADA de fabrica** (`DeFabrica = false` en `RocolaOs.Cielo.cs`: no se toco; la enciende la junta final). **Nada probado en el A4.**

## Que se hizo

| Archivo (todo en `RocolaOs/`) | Que lleva |
|---|---|
| `RocolaOs.CieloSol.cs` | `RocolaOsCieloAstros`: la proyeccion de un astro al cielo, el paso suave y los halos y discos de malla con recorte arriba y abajo (la tarjeta no tiene recorte: se corta triangulo por triangulo); `RocolaOsCieloSol` (DN2) |
| `RocolaOs.CieloLuna.cs` | `RocolaOsCieloLuna` (DN3 A y B, toma la luna con `TomaLaLuna`), `RocolaOsCielo.FaseLunaPara` y, en `GpuPanel`, `RocolaOsLunaPinta` (la textura de la luna de XP donde y como diga DN3) |
| `RocolaOs.CieloEstrellas.cs` | `RocolaOsCieloEstrellasDN4` (el cliente de estrellas de g127, los umbrales, el pintor que pone `EstrellasPorEstrella`) y la luz de las fugaces al azar de XP |
| `RocolaOs.CieloSolPrueba.cs` | la orden `rocola-xp --prueba-cielo-sol` (22 comprobaciones de las cuentas puras) |
| `GANCHOS.tsv` | cuatro ganchos nuevos: g300 a g303 (rango g300-g319 de este grupo; el plan decia g140-g149, que la junta cambio) |
| `Pruebas/RocolaOs/cielo/` | `prueba-sol.sh` (la bateria del grupo), `fugaces.py` (cuenta las cabezas de las fugaces de una cinta) |

**Archivos comunes del nucleo tocados: ninguno** (ni `RocolaOs.Cielo*.cs` anteriores ni los enchufes: todo entra por los registros del nucleo,
`PonPintor`, `PonEstrella` y `TomaLaLuna`, y los cuatro ganchos nuevos de abajo).

## DN2: el sol en su camino (`sol=`)

- Un disco de radio 0,028 del alto (nucleo `#fff6dc`) con dos halos `#ffc070` (r x 2,2 a 0,55 y r x 7 a 0,22, sumando luz), de §5. Se pinta en la
  capa `TrasNieblas` (g130): justo despues del degradado y las nebulosas, **antes de la aurora, las estrellas, la luna, la estrella de XP, las
  portadas y los textos: siempre detras de todo** (y el cielo de DN1 o el de XP debajo).
- **El camino real**: la altura y el acimut de la foto del cielo (el sol de la ciudad del bar). La rocola mira al sur: `x` sale del componente
  este-oeste del astro (`sen(acimut) x cos(altura)`: este a la izquierda, sin saltos por el cenit ni por el norte, que en verano el sol de
  mediodia en el tropico pasa al norte) y `y` de su altura (el horizonte de la escena abajo y el cenit a 0,12 del horizonte; bajo el horizonte
  sigue la misma curva). Se desliza hacia su sitio con una constante de 0,5 s, asi que «Ver como se veria» no da saltos.
- **El horizonte lo corta**: la raya de XP (`Escena.Horizonte`, que sube con el reposo) es el borde de abajo; el sol asoma al salir y al ponerse.
  Resplandor de los halos de `-6` a `-0,5` grados de altura; el disco, entero (el recorte lo esconde).
- **En la pared** (la rejilla de portadas a la vista y no el reposo, que la esconde: `Escena.Pared && Escena.Reposo < 0,2`): solo asoma en la franja de
  0,848 a 0,868 del horizonte (sobre la raya, bajo la ultima fila de titulos: regla 3 y 4, «nunca detras de un titulo»), con el radio x 0,7 para
  que su cupula quepa en los 16 px, los halos x 0,22 y apagandose al subir de 2 a 9 grados. A mediodia en la pared no se ve (esta detras de las
  portadas en cualquier caso).
- Sin la calma (regla 5): el sol no se mueve con la musica; lo que cambia en un pixel cada varios minutos no se calma. Sin memoria nueva por
  cuadro (una malla fija de 6144 vertices) y los senos y cosenos del halo en una tabla.
- Lo del plan que NO se hizo: «la tarde dorada va detras de una portada de la derecha con el halo x 0,42» (el plan lo dice para la pared; con el
  sol detras de todo ya queda detras de la portada, y el halo de la pared va a 0,22 siempre).

## DN3: la luna (`luna_camino=`)

La luna de XP (`CieloVivoGpu.cs`: `HorneaLuna`, `EnsureLunaCielo`, `DibujaLunaCielo`) ya tiene la fase real (la media, `FaseLunar`), su sitio
(arriba a la derecha junto a la estrella, `SitioLuna`) y se pinta tambien de dia. DN3 la toma con `TomaLaLuna` (g132) y usa la textura de XP:

- **A** (`luna_camino=0`, de fabrica; con DN1, que es la que pone el cielo de dia): con el peso de la noche bajo 1 la luna sigue en su sitio de XP,
  pero **solo se ve si de verdad esta sobre el horizonte** (altura de la foto, paso de -2 a 4 grados), palida (alfa 0,55; el halo, al cuadrado)
  como se ve de dia; al anochecer sube a su brillo de XP, y con la noche cerrada (la noche de XP, regla 1) la pinta XP tal cual (`TomaLaLuna` da
  falso: bit a bit). Sin DN1 el cielo de dia es el de XP (oscuro) y la luna tambien.
- **B** (`luna_camino=1`): la luna en su camino real (la misma proyeccion que el sol), **mas grande cerca del horizonte** (hasta x 1,3), el
  horizonte la corta y asoma al salir y ponerse, solo sobre el horizonte; de noche con su brillo de XP, de dia palida. En la pared, la misma franja
  de 0,848 a 0,868 que el sol. Siempre detras de todo (capa `ConLaLuna`: antes de la estrella, las portadas y los textos). Es lo unico del
  grupo que pinta algo distinto en la noche cerrada (la regla 1 lo permite: «lo que pinte la luna B»).
- **La fase**: la textura es la de XP (cambia cada ~6 h de fase). Con «Ver como se veria» o `ROCOLA_CIELO_LUNA=1` (las pruebas, con
  `ROCOLA_CIELO_FECHA`) la fase sale de la hora del cielo y no del reloj (**g302**, `faseLuna` de `ActualizaCieloVivo`); si no, la de XP, sin tocar
  (y el 20 s de XP en repintar la fase tambien aplica a «Ver como se veria»).
- El halo de A es el de XP (`DrawGlow`); el de B es una malla con recorte (no hay recorte en la tarjeta).

## DN4: las estrellas se encienden de una en una (`estrellas_anochecer=`)

- XP pinta su campo siempre a todo su brillo y DN1 (g126) lo baja todo junto por la fase del dia. Con DN4 encendida el campo deja de bajar junto
  (`RocolaOsCielo.EstrellasPorEstrella = true`, lo pone un pintor en `Prepara`, antes de armar las estrellas) y cada estrella (`PonEstrella`,
  g127; el indice de su patron, su sitio y su tamano) lleva su propio **umbral**: 0,88 x (base del tamano + 0,30 x azar + 0,15 x altura) / 1,05,
  con la base 0 las grandes, 0,30 las medianas y 0,55 el polvo. La **luz de estrellas** p es `EstrellasCuadro` (la curva de la foto, interpolada:
  0 de dia, 0,12 al ponerse el sol, 0,55 a mitad del crepusculo, 1 con la noche cerrada); la estrella se enciende en 0,1 de p, suave, cuando p
  pasa de su umbral: las grandes primero, luego las medianas y al final el polvo, y arriba antes que abajo; por la manana, al reves.
- Con p = 1 (la noche cerrada) todas valen **exactamente 1** (`Brillo` da 1 sin cuentas): el cielo es el de XP, bit a bit (regla 1).
- DN4 no depende de DN1: la luz de estrellas sale de la hora del sol, que siempre se calcula.
- **Las fugaces al azar de XP de dia** (lo que decia «Lo no probado» del nucleo): `UpdateAndDrawShoots` no las atenuaba con la fase (g126 solo toca
  las estrellas) y de dia son rayas blancas sumadas sobre un cielo claro. Con DN4 se atenuan con la misma luz de estrellas. **g301** marca la
  fugaz al azar con el bit 31 de su color (XP solo usa los 24 de abajo: `s.Col & 0x00FFFFFF`; 0 sin ROCOLA_OS) y **g300** multiplica su alfa por
  esa luz; la de festejo, sin marca, se ve siempre (y la de lluvia de estrellas, que XP saca por el mismo camino de azar, se atenua igual: es
  una fugaz mas).

## Ganchos (4, en `Codigo/`; ganchos.py TODO BIEN)

| Gancho | Archivo:linea | Que hace |
|---|---|---|
| g300 | `D3D9Star.cs:1445` (`UpdateAndDrawShoots`) | `230 * k * dim` por `RocolaOsFugazAzar(s.Col)` (1f sin ROCOLA_OS, sin DN4, con la noche cerrada o con la fugaz de festejo: el mismo numero) |
| g301 | `D3D9Star.cs:1435` (al sacar una fugaz al azar) | `shoots[i].Col \|= RocolaOsMarcaAzar();` antes del `break;` |
| g303 | `D3D9Star.cs:1455` (la cabeza de la fugaz) | `la + 40u` por `la + (uint)(40f * RocolaOsFugazAzar(s.Col))` (40 exacto con 1f) |
| g302 | `CieloVivoGpu.cs:278` (`ActualizaCieloVivo`) | la fase de la luna por `RocolaOsFaseLuna(...)`: la de XP salvo «Ver como se veria» o ROCOLA_CIELO_LUNA=1 |

El resto entra por lo que ya habia: `PonPintor(TrasNieblas)` (sol), `PonPintor(ConLaLuna)` + `TomaLaLuna` (luna), `PonEstrella` y `PonPintor(TrasFondo)`
(estrellas).

## Las pruebas (`Pruebas/RocolaOs/cielo/prueba-sol.sh`)

Con `T=<carpeta de trabajo> bash Pruebas/RocolaOs/cielo/prueba-sol.sh bin/Release/net10.0` (JIT; `AOTMIO=aot:<publish>` para el reloj, NativeAOT):
la rocola entera fuera de pantalla (1024x768, llvmpipe), sin red, con datos inventados que arma la prueba (120 canciones de ffmpeg); la maquina
estaba compartida (carga de 10 a 16 con otros trabajos). Partes y resultados de esta sesion:

| Parte | Que mide | Resultado |
|---|---|---|
| `ganchos` | ganchos.py TODO BIEN (112 archivos, 29 con ganchos) y los cuatro g300-g303 en el manifiesto | 2/2 |
| `orden` | `--prueba-cielo-sol`: la proyeccion (sol al salir a la izquierda y en el horizonte, mediodia al centro, sin saltos por el norte, cenit a 0,12, la y sin saltos de 40 a -5), el recorte de las mallas (halo de 420 triangulos sin recorte; con la franja 651-667 todos los vertices dentro; disco bajo la raya o lejos de la franja: nada), los umbrales de DN4 (0 a 0,88; con p = 1 todas valen exactamente 1; con p = 0 ninguna; las grandes antes que el polvo; el brillo crece sin retrocesos) y la luz de estrellas por la altura del sol | 22/22 |
| `apagado` | **APAGADO = XP**: la cinta de la tarjeta de la pared (cinta.py, brotli) sin ROCOLA_OS, con las cuatro funciones en 0 y con `cielo=1` y las ideas del grupo y el resto del cielo en 0, iguales a la de la rama principal (un build de `e94dcd6` sin los cambios); el control (el sol solo a las 17:40) SI cambia (la raya pasa de 118/82/202 a 164/120/171 por el halo del sol en la franja del horizonte) | 4/4 |
| `escenas` | capturas a las 6:50, 9:30, 13:00, 17:40, 18:35 y 22:00 (el buzon `/run/rocola-cielo-ver`) del escaparate, la pared, el disco abierto y el reposo con musica, con todo encendido (`todo`) y con la luna en su camino (`lunab`): 48 capturas; cada idea sola (`sol`, `estrellas`: pared y escaparate, 24 capturas); y la **regla 3** medida (contraste.py, percentil 60 por fila) en el escaparate (zona 560,560,1000,650), el disco (0,560,430,650), el reposo con musica (sobre la raya, hasta 0,668 de 525) y la pared (sin la barra de arriba, la portada ni la luna): luz maxima entre 0,05 y 0,17 en todas | 8 de capturas + 48 de la regla 3, todo BIEN |
| `luna` | 8 fechas (9-11 a 6-12, 23:30, `ROCOLA_CIELO_LUNA=1`): la parte clara del disco va de 0,00 (nueva) a 0,94 (llena el 24-11) y vuelve a 0,02 (0,00 0,18 0,59 0,89 0,94 0,76 0,37 0,02) | 1/1 |
| `fugaz` | las fugaces al azar de XP (cabezas de DrawShootStreaks, 100 s): de dia sin DN4 654-693 vertices con alfa; de dia con DN4, **0**; de noche con DN4 como sin DN4 (la noche de XP) | 3/3 (tras g303, abajo) |
| `reloj` | NativeAOT (0 avisos IL/AOT en el publish), p50/p95/p99/max del trabajo de cada cuadro (ms) de la pared y la atraccion a las 17:40, dos vueltas: **encendido** (cielo=1 con luna_camino=1: DN1, sol, luna B, estrellas, DN5, nubes y todo lo del nucleo) 1,65/3,58/19,79/93,6 y 1,60/2,38/5,66/118,98; **apagado** (las cuatro funciones en 0) 1,50/2,29/5,80/101,8 y 1,48/2,21/7,16/62,6 | midio las 4 |

Lo que se aprendio al probar: (1) con DN4 y de dia las fugaces al azar seguian dejando un **punto** (la cabeza de la fugaz suma 40 de alfa a
`la`, 0 o no): g303 lo baja con la misma luz de estrellas; (2) la pared con el reposo con musica: `Escena.Pared` sigue verdadera con el reposo
(la rejilla esta debajo), asi que la franja de «asoma» se aplica solo si `Escena.Reposo < 0,2` (`RocolaOsCieloAstros.ConPortadas`); (3) el sol
y la luna B de la pared con el radio completo quedaban con la cupula recortada plana por la franja de 16 px: radio x 0,7 en la pared.

**El reloj**: p95 y p99 de «encendido» salen algo mas altos que «apagado» en la vuelta 1 (3,58 y 19,79 contra 2,29 y 5,80) y casi igual en la
vuelta 2 (2,38 y 5,66 contra 2,21 y 7,16); el maximo (62 a 119 ms) cae en las dos configuraciones: es el arranque de la rocola (carga de la
biblioteca) y la carga de la maquina compartida, no el cielo. Con todo encendido el p50 sube unos 0,1-0,15 ms (el cielo de DN1 y las mallas);
sin medir en el A4. No afirma fluidez alli.

Capturas representativas (6 de las 80 de la corrida, en `Pruebas/RocolaOs/cielo/capturas-sol/`): `todo-escaparate-1740.png` (el sol saliendo por la
derecha, la luna A palida, DN4 a medias), `todo-pared-0650.png` (el sol asomando en la franja del horizonte en la pared),
`lunab-escaparate-1835.png` (la luna B en su camino con las estrellas encendiendose) y `luna-fase-4.png` (la luna llena del 24-11).

## Lo no probado y lo pendiente

- **Nada en el A4**: ni como se ve, ni lo que cuesta en su tarjeta.
- **Los datos de las pruebas son inventados** (120 canciones de ffmpeg en 20 artistas; sin portadas, sin las letras de XP, sin
  `/root/rocola-trabajo`, que aqui no esta): las capturas sirven para ver el cielo, no para medir los textos de XP. Las letras son las del
  sistema (DejaVu).
- Las medidas de la regla 3 se hacen en las zonas libres del escaparate y del disco (como el nucleo); un sol alto detras de una portada o del
  panel de la lista no se mide (esta detras, que es lo que pide la regla 4). La luminancia maxima de las zonas medidas pasa de 0,17 solo donde se
  dice abajo.
- **La luna A de dia sin DN1**: igual que XP (la regla es del cielo de DN1).
- **El sol y la luna no se mueven con la calma**; si el dueno quiere que el halo baje con ella (0,35), es una linea en cada pintor.
- **El sol de la pared a mediodia no se ve** (esta detras de las portadas): lo pide la regla 3, pero el dueno puede querer ver su halo asomar por
  los huecos; no se hizo.
- **Otro horizonte** (otras resoluciones): el sol y la luna usan `Escena.Horizonte` y `Escena.Alto` (se corren y escalan), pero solo se vio a
  1024x768.
- Sin el catalogo de colores del sol por la fase (el nucleo es siempre `#fff6dc` como dice §5): al ponerse se ve blanco palido sobre el naranja.
