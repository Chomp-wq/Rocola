# Port nativo: arreglo de la junta 14 (oleadas 5a, 5b, 6b y 6c juntas)

Sobre 8460045 (la junta de 5a interfaz-a-skbitmap, 5b vistas-disco, 6b vistas-pared y 6c vistas-buscador). La verificacion completa de esa
junta dio tres cosas que ningun grupo vio por separado. Las tres eran de las PRUEBAS, no del programa: no se toco `Codigo/` ni `Nativo/` ni
`Plataforma/`.

## Lo hecho

### 1. VistasSuena y VistasPantallaViva abortaban (y el recuento de los arnes.sh)

Desde vistas-disco (PORT_vistas-disco.md) PastillaView y SongListView ya no heredan de Control en la traduccion: son vistas de la pantalla
nativa (IVista con su CajaVista). Los dos arneses las trataban como Control:
- `Pruebas/VistasSuena/Suena.cs`, `Numero`: `pv.GetProperty("Size")` (no existe en la PastillaView traducida). Ahora, si la vista tiene
  `Size` (la referencia, un Control) se pone como antes; si no, `SetBounds(0, 0, w, h)` de la vista: lo que hace el Size de Control con la
  vista en (0,0) (Control.Size -> SetBounds(Left, Top, w, h)).
- `Pruebas/VistasPantallaViva/PantallaViva.cs`, `NuevaLista`: el cast a `Rocola.Ventanas.Control`. Ahora `Bounds`, `Font` y `BackColor` por
  reflexion (`PonProp`: los de Control en la referencia, los de la vista en la traduccion, en el mismo orden) y `Dispose` por `IDisposable`
  (Control lo es; SongListView traducida tambien).
- Es lo mismo que hizo vistas-disco en VistasVistasA (`PonSitio`, `PonProp`).
- `arnes.sh` de VistasSuena, VistasPantallaViva y VistasVideoForm1: el recuento de DISTINTAS contaba solo las lineas `>` del diff, y con la
  salida de la traduccion cortada (el arnes abortado) decia «DISTINTAS: 0 lineas». Ahora dice las dos cuentas: las de la traduccion sin su
  igual (distintas o de mas) y las de la referencia sin su igual (distintas o que faltan).

### 2. VistasVideoForm1: 24 lineas DISTINTAS (vista-*-4-*)

La vista 4 de `Vistas()` es **GeneroView** (Texto «Cumbia», Tinte verde), pintada por `DrawCtrlOverlay` en un lienzo de 440x64. Era el
**arnes**: `Vista()` le ponia la letra (`GenericSansSerif 10 negrita`) solo «a las que son Control» (`c as Control`). Desde vistas-disco
GeneroView ya no es Control, asi que en la traduccion se quedaba con la ambiental (`Control.DefaultFont`, la de la ventana) y se media
(`AnchoTexto(texto, this.Font)`, `Font.Height`) y se pintaba con otra letra que en la referencia, donde si la recibe. En el programa no pasa:
Form1.cs:1266 le pone su letra al crearla (`pastGenero.Font = ...`), igual que XP. LetterBar (5) y PastillaView (6) tambien tienen Font
pero en este camino no la leen (salian iguales). Ahora, si no es Control, la letra se le pone por reflexion a la vista que tiene una `Font`
que se puede escribir. No era el fondo de fabrica, OnPaintBackground ni el BackColor ambiental (GeneroView es SupportsTransparentBackColor y
su constructor la pone transparente; DrawCtrlOverlay no pinta fondo).

### 3. El recorrido «buscador»: texturas quietas 44-45 de 78

Repetido como lo pide la tarea (con el candado, `ROCOLA_TRABAJO=/root/rocola-trabajo/port/dibujo VOLCADO=1 CAPAS=1 HUELLAS=1`, compara.py
contra cmp-ref y cmp-ref2): 76 y 45 de 78; despues 76 y 45 otra vez (con capturas guardadas aparte). Seguia faltando: se busco la causa.

- Las capturas de los 14,5 s: en la buena la seleccion esta en la tarjeta «01 Cancion Larga»; en la mala en la tecla Ñ: una DERECHA de
  menos (de Q, nueve DERECHAS llegan a P y la ABAJO de los 14 s cae en Ñ). Las texturas lo confirman: en la mala no hay subida a los 10,2 s
  (la tecla de los 10 s) y desde ahi todo va una tecla atras.
- La traza de ventanas (`ROCOLA_TRAZA_VENTANAS`, exportada a recorridos.sh) muestra la causa: la captura de los 9,5 s con `CAPAS=1` deja a la
  rocola sin atender nada **de 790 a 970 ms** (guarda cada capa: huecos medidos 9476->10444, 9484->10331, ...). Las DERECHAS de los 10 y de
  los 10,4 s esperan en la cola; si la parada acaba despues de las 10,4 s, se atienden **a menos de 40 ms** una de otra (10445 y 10446 ms en
  la mala; 10340 y 10424 en una buena) y el freno de las flechas de XP (`Form1.cs:2134`, igual que `Rocola/Form1.cs:2135`:
  `if (ahora - ultimaFlecha < MsEntreFlechas) return true;`, MsEntreFlechas = 40) tira la segunda. Es lo que hace XP con la ventana ocupada:
  **no es un fallo del programa** ni de 5a ni de 6c.
- La **referencia congelada** (ref-trazas, la misma corrida): 76, **44** y **45** de 78, con las dos DERECHAS a 35 ms (10378/10413) y 12 ms
  (10389/10401). Lo mismo con la referencia: la cuenta 45 de 78 que ya salia en juntas anteriores («depende de la hora») era esto.
- Arreglo en el arnes (`Pruebas/Recorridos/recorridos.sh`, `P[buscador]`): la primera DERECHA pasa de los 10 s a los 13,8 s (entre las de
  13,6 y la ABAJO de 14 s). Son las mismas diez DERECHAS en el mismo orden; las capturas no cambian de hora (las capas se comparan por su
  nombre, que lleva la hora). Ahora solo la de los 10,4 s puede caer dentro de la parada de los 9,5 s, sola: aguanta paradas de hasta 1,3 s.
- Con el arreglo: esta copia **76, 76, 76** de 78 y la referencia **76, 76, 76** de 78, alternadas (carga 3,5-4,2); capas 22 de 22 en las
  seis. Las 2 que faltan son dos `1024x128` (los rotulos de la hora): faltan igual con la referencia. Una de las de la referencia tuvo la
  parada de los 9,5 s en 951 ms (antes habria podido salir mala).

## Costuras

Ninguna quitada ni anadida (no se toco el programa).

## Comprobaciones (12-11-2026, maquina compartida a carga 2-5, llvmpipe)

- a) `dotnet build -c Release --no-incremental`: 0 errores, 15 avisos (los de la base: SYSLIB0054 x9, CA2022 x2, CS1690 x2, SYSLIB0050,
  CA2014).
- b) diff-xp: no aplica (ningun archivo de `Codigo/` cambiado; `git diff 8460045 --stat` solo muestra Pruebas/ y este documento).
- c) NativeAOT (`dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true`, con obj de linux-x64 borrado antes): sale con 0,
  **0 avisos IL/AOT** (solo los 15 de compilar).
- e) Arneses (cada uno con su carpeta en /root/rocola-trabajo/port/arreglo-junta14):
  - VistasSuena: **IGUALES** con las letras falsas y con DejaVu (488 lineas; 487 casos, 72 llamadas directas a la traducida).
  - VistasPantallaViva: **IGUALES** con las dos letras (768 lineas; 767 casos, 291 directas).
  - VistasVideoForm1: antes del arreglo 24 y 24 distintas (las dos letras); despues **IGUALES** con las dos (735 lineas; 734, 140 directas).
- d) Recorrido buscador (lo de este grupo; los demas no cambian: solo se toco `P[buscador]`): con lo de antes 76, 45, 76, 45 (y con traza 75,
  76, 76, 45; la de 75, un rotulo de la hora de menos); con el arreglo 76, 76, 76 (esta copia) y 76, 76, 76 (referencia), capas 22 de 22 siempre.
- No se corrieron el banco ni las 29 cintas (no se toco Nativo/, Puente.cs ni lo que se sube a la tarjeta), ni las escenas f) (no se toco
  ventanas, pantalla, teclas, raton ni cierre).

## Lo que queda

- Nada de este grupo. Los arneses que ya adapto vistas-disco (VistasVistasA, VistasVistasB) no se tocaron.

## Riesgos

- El recorrido buscador tiene otras flechas medio segundo despues de una captura (21,5/22, 25,5/26 y 30,5/31 s, con capturas a los 21, 25 y
  30 s): si una de esas paradas pasara de ~1 s (se midieron hasta 1 060 ms con la maquina cargada) podria volver a tirarse una flecha. No se
  movieron porque no se vio fallar ninguna y moverlas cambia lo que muestran las capturas de esas horas. Si vuelve a salir menos de 76,
  mirar con la traza de ventanas si hay dos flechas a menos de 40 ms.
- El cambio de `P[buscador]` hace que las corridas nuevas no tengan exactamente las mismas subidas a la misma hora que cmp-ref (las mismas
  texturas, una tecla mas tarde): compara.py compara las texturas como multiconjunto, sin la hora, y las capas por el nombre de la captura,
  que no cambia.
