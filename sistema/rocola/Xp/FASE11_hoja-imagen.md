# La hoja «Imagen» de Ctrl+F4 (27-11-2026)

La hoja de la ventana de Rocola OS donde el dueno enciende, apaga y mira el cielo vivo (`PLAN_CIELO_VIVO.md`, punto 3 del orden de
trabajo y la decision del 20-11-2026). Solo Rocola OS (con `ROCOLA_OS`); sin el, Ctrl+F4 no hace nada. No cambia NINGUN valor de fabrica: las
cuatro funciones madre (`cielo`, `fiestas`, `clima`, `vidrio`) siguen con `DeFabrica = false`; eso se decide en la junta final.

## Donde esta

- `RocolaOs/RocolaOs.VentanaImagen.cs`: la hoja (registro en un `[ModuleInitializer]`, `RocolaOsHojitas.Registra`, **orden 12**: despues de
  Pantalla (10) y antes de Sonido (15)). Mismo aspecto que Pantalla, Sonido y Spotify: tarjetas, `Interruptor`, `Deslizador` y `BotonPlano` de
  Ctrl+F3.
- `RocolaOs/RocolaOs.VentanaImagenFechas.cs`: leer, agregar y quitar renglones de `rocola_fechas.txt` (solo texto).
- `RocolaOs/RocolaOs.VentanaImagenPrueba.cs`: `rocola-xp --prueba-imagen` (las fechas y las tablas, 31 comprobaciones).
- `Pruebas/RocolaOs/imagen/prueba.sh`: las escenas con la rocola entera fuera de pantalla.
- **Ganchos: ninguno** (no se toca `Codigo/`; el rango g470-g489 queda libre). Nada de `Codigo/` ni de la ventana (`RocolaOs.Ventana.cs`) cambio.

Con la pestana nueva, las demas se corren a la izquierda: en 1024x768 Pantalla queda en (500,54), Imagen (606,54) y Sonido, Spotify y Archivos
**siguen donde estaban** (712, 818 y 924). Por teclado, las pruebas que iban de la primera pestana a una barra llevan un Tab mas.

## Las hojitas (los cinco botones de arriba)

Se recuerda la ultima que se miro mientras la rocola siga abierta. Cada interruptor cambia su clave de `rocola_os.txt` al momento
(`RocolaOs.PonAjuste`, se guarda para siempre) y la bitacora de Rocola OS lo apunta («Imagen: cielo_sol=0»). La hoja se vuelve a leer cada medio
segundo: lo que cambie otro (el buzon, la orden «ajuste») se ve.

| Hojita | Izquierda | Derecha |
|---|---|---|
| Dia y noche | `cielo` y `cielo_sol`, `sol`, `luna_camino`, `estrellas_anochecer`, `horizonte_hora`, `nubes` (DN1-DN6) | la ciudad del bar (`ciudad_bar`) y la letra en el cielo (`letra_cielo`, K6) |
| Fiestas | `fiestas` y `aurora_fiesta`, `horizonte_fiesta`, `fugaz_fiesta`, `fuegos`, `escaparate_fiesta`, `fechas_propias` (FI1-FI6) | tus fechas |
| Clima | `clima` y `lluvia`, `tormenta`, `neblina`, `clima_escaparate` (CL2-CL5; CL1 es la funcion) | el clima de afuera de ahora |
| Vidrio | `vidrio` y `vidrio_pastillas`, `vidrio_luz`, `vidrio_ficha`, `vidrio_franja`, `vidrio_lista`, `vidrio_buscador` (VI1-VI6) | que hace y cuanto cuesta |
| Ver como se veria | hora, fecha (o fiesta) y clima | como funciona |

- **La funcion madre** (la primera de cada grupo): su interruptor se guarda al momento pero **vale desde el siguiente arranque**
  (`RocolaOs.Encendida` se decide una vez por sesion). La tarjeta lo dice arriba: «En marcha.», «Encendida: empieza en el siguiente arranque.»,
  «Apagada: se quita en el siguiente arranque.» o «Apagada (de fabrica aqui). Enciendela y reabre la rocola.».
- **Las ideas** cambian en vivo (el latido del cielo las vuelve a mirar cada segundo). Si la funcion madre esta apagada en `rocola_os.txt`, sus
  ideas van en gris con «necesita «cielo»» y su valor guardado se conserva; un clic en una en gris no guarda nada.
- **Todo sale de tablas** al principio del archivo (`ImagenDia`, `ImagenFiestas`, `ImagenClima`, `ImagenVidrio`, `ImagenLetra`): anadir una
  idea es una linea (clave, nombre, que hace y su funcion). La prueba comprueba que cada clave este declarada y que las ideas de las cuatro
  funciones esten todas en la hoja.

### La letra en el cielo (K6)

El interruptor de `letra_cielo` esta en la hojita Dia y noche. La funcion `letra_cielo` ya existe en esta rama (`RocolaOs.LetraSuena.cs`: pide la
letra a LRCLIB o la lee del MP3; encendida de fabrica); el dibujo (K6, `g380-g389`) lo hace otro grupo. Si el grupo de K6 cambia el nombre de la
clave, basta cambiar la linea `ImagenLetra` (o anadir una fila a `ImagenDia`). Si la clave no esta declarada, el interruptor sale en gris con
«Esta version no trae la letra en el cielo.».

### La ciudad del bar

Un deslizador de 0 a 41: 0 es «Automatica (por internet)» (una vez por semana; mientras no se sepa, la Ciudad de Mexico) y de 1 a 41 una de las
ciudades de `RocolaOsCiudades`. Se guarda como `ciudad_bar=N` a los 0,5 s de dejar quieta la barra y al cerrar la ventana; el hilo del cielo
detecta el cambio y pide el clima de la ciudad nueva. Debajo, el lugar de ahora (nombre y coordenadas), la hora del cielo, el sol y la fase.

### Tus fechas (`rocola_fechas.txt`)

Ve todos los renglones del archivo (8 a la vez, con «Antes» y «Mas»), toca uno para elegirlo y:

- **Quitar la elegida**: quita ese renglon (cualquier tipo) y deja lo demas tal cual (comentarios, renglones vacios y los que no valen).
- **Agregar un cumpleanos / un aniversario**: pide el nombre y luego el dia y mes (`15-03`; tambien `5/3`) en una hoja chica con el teclado de
  Ctrl+F4 (sin que las monedas cuenten, como al renombrar en Archivos) y agrega `15-03;cumple;Juan` al final. El nombre: hasta 60 letras, sin `;`,
  `|` ni `#`. Se ve ese dia con las fiestas encendidas y «Tus fechas» (FI6).
- Las fiestas propias, los mensajes, los fuegos y quitar una fiesta (los otros tipos del formato, `FASE11_cielo-fiestas.md`) se escriben a mano en
  Ctrl+F4 -> Archivos; la hoja los muestra pero no los agrega.

El archivo se escribe entero y aparte (`rocola_fechas.txt.nuevo`) y se renombra encima: un apagon deja el de antes o el nuevo. El hilo del cielo lo
vuelve a leer solo cuando cambia (`RevisaFechas`).

### Ver como se veria

Elige (cada una con su interruptor «Cambiar la ...»): una **hora** (de 00:00 a 23:30 de media en media), una **fecha** (cualquier dia del ano,
con el nombre de la fiesta de fabrica de ese dia y los botones «Fiesta anterior / siguiente») y un **clima** (despejado, poco nublado, nublado,
neblina, llovizna, lluvia, aguacero, tormenta, nieve). «Verlo 30 segundos» llama a `RocolaOsCielo.VerComo(hora, clima)` (que ya existia en el
nucleo: la foto del cielo se hace con esa hora y ese clima) y **cierra la ventana** para que se vea la rocola de verdad; los modulos del cielo
leen la misma hora del cielo (`AhoraLocal`), asi que todo (sol, luna, estrellas, fiestas, clima) sigue lo elegido. No se guarda nada
(en memoria, con tope de 30 s). Vuelve solo a los 30 s, o en cuanto alguien toca un boton de la rocola (el comportamiento del nucleo), o con el boton
«Quitar lo que se ve» si se reabre la ventana. Lo que no se cambia sigue siendo de ahora (la hora corre sola).

- Hace falta el cielo vivo andando (alguna de sus cuatro funciones encendida desde el arranque): si no, los mandos van en gris con «necesita el
  cielo vivo» y el boton dice «Necesita el cielo vivo andando».
- La regla 5 del plan (la calma al 35 % mientras alguien elige) cuenta los 15 s despues del ultimo boton; el clic de «Verlo» es el ultimo, asi que los
  efectos se ven a medio brillo los primeros 15 s y completos los 15 s siguientes. Mientras la ventana esta abierta, la hoja suma una a
  `RocolaOsCielo.HojasAbiertas` (el contador que el nucleo ya preveia para Ctrl+F4; nadie lo subia) y la quita al cerrar.
- Los fuegos del Grito y de Ano Nuevo piden ademas su hora (23:00 y 00:00): para verlos, elige fecha y hora.

## Pruebas (`Pruebas/RocolaOs/imagen/prueba.sh PROGRAMA [orden interruptores ver tecla fechas sin]`)

- **orden** (`--prueba-imagen`): leer, agregar y quitar renglones, el archivo de verdad, las tablas y que cada clave exista: 31 comprobaciones.
- **interruptores**: con las cuatro funciones apagadas, Ctrl+F4 -> Imagen; una idea en gris (sol) no guarda; el interruptor de `cielo` guarda
  `cielo=1`; `cielo_sol` apagado y encendido otra vez; la ciudad con un clic en la barra (`ciudad_bar=N` al cerrar); el vidrio.
- **ver**: «Ver como se veria» con la hora de las 20:00: la ventana se cierra, la bitacora dice «(30 s)», los 30 s pasan solos y `rocola_os.txt` queda
  igual que antes.
- **tecla**: con el clima forzado, una flecha a los 4 s lo quita («se toco un boton») sin esperar los 30 s.
- **fechas**: agregar un cumpleanos con el teclado (nombre y fecha), que lo de antes siga, quitarlo eligiendo su renglon; «Quitar» en gris sin elegir.
- **sin**: sin `ROCOLA_OS`, ninguna hoja.

Capturas de 1024x768 de la sesion: Dia y noche (apagada y con `cielo` encendido), Vidrio, Ver como se veria y Tus fechas (no se suben: ni png ni jpg).

## Lo que no se comprobo

- Nada en el A4 ni con el cielo vivo viendose de verdad por el monitor: las pruebas fuera de pantalla miran la bitacora y las capturas de las hojas.
- «Ver como se veria» se comprobo por la bitacora del cielo (hora y clima puestos, 30 s, quitado por boton); la captura de la rocola con lo elegido es
  la de `llvmpipe` fuera de pantalla, no la del monitor del A4.
- Con las funciones del cielo apagadas de fabrica (esta version), la hoja explica como encenderlas; encenderlas y reabrir hace que las cuatro
  hojitas sirvan de verdad (la escena «ver» lo hace desde `rocola_os.txt`, no desde la hoja, porque la funcion madre no vale hasta el siguiente arranque).

> **27-11-2026, junta final:** las cuatro funciones del cielo vivo van ahora ENCENDIDAS de fabrica (`DeFabrica = true`); lo que arriba diga «apagada de fabrica» o «en esta version» era de la version anterior. Sus ideas que costaban (VI1, VI2, VI4, VI6) y `luna_camino` siguen apagadas.

## Repaso del 27-11-2026

- **«Ver como se veria» desde la hoja ya no se quita solo.** Se quitaba al primer cambio de `lastInput`; el raton lo actualiza al moverse mas de 4 px (`Form1.Video.cs`), asi que al cerrar la hoja se quitaba al instante (la escena `raton` con el programa de antes: se quito al 1.er segundo). Ahora (a) el raton que solo se mueve no lo quita (gancho g438, `RocolaOsRatonMueve`: si antes del movimiento no habia otra cosa nueva, se absorbe) y (b) 2 s de gracia tras ponerlo (lo que mueva el cierre de la hoja). Una tecla o un clic (pasada la gracia) lo quita como siempre. Prueba: `imagen/prueba.sh ... raton` (4 movimientos del raton hasta el segundo 20 y una tecla en el 26: se quita a los 14 s de ponerlo; antes de este cambio, a 1 s).
- La hoja dice «Lugar sin saber: elige la ciudad» cuando el lugar es automatico y no se ha sabido (ver FASE11_cielo-clima.md).
