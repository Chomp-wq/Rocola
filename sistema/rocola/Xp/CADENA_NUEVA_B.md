# La cadena nueva del billar (B): sus piezas

Parte de la cadena de audio nueva (peticion del dueno del 27-11-2026, plan aprobado el mismo dia: `CADENA_NUEVA.md`, §3 y §8). Aqui: las
piezas de la B (mono, paso alto, curva del billar, bajo virtual), la afinacion de varios puntos y su hojita. Las hizo un agente Opus; el marco
(la medida, la ganancia, la cadena A, el contrato de las piezas) y la salida nueva son de los otros dos. Solo Rocola OS; **sin ganchos**.
NADA de esto se oyo ni se probo en el billar ni en el A4: todo son cuentas con senales sinteticas.

El billar: bocinas repartidas por zonas (dos medios-graves de 6,5" y dos tweeters en el gabinete; tres cajas de 12" con puertos en la nave),
sin subwoofer, varias en paralelo en el mismo amplificador, sala ruidosa. La B trata todo eso con piezas FIJAS: ninguna sigue a la musica.

## 0. Donde esta

| Archivo (RocolaOs/) | Que |
|---|---|
| `RocolaOs.Billar2.cs` | la funcion `billar_nuevo`, sus claves de `rocola_os.txt`, los parametros, la huella, el diseno de fondo y `RocolaOsCadenaB.Arma/Huella` (el contrato del marco) |
| `RocolaOs.Billar2Etapa.cs` | EL TRAMO B: una sola pieza (`IRocolaOsPieza`) con mono -> paso alto (y noche) -> curva -> bajo virtual; el disco continuo |
| `RocolaOs.Billar2Curva.cs` | la curva (objetivo, afinacion, claridad), el paso alto, el diseno del FIR (`RocolaOsBillar2Diseno`) y la afinacion de varios puntos (`RocolaOsBillar2Afinacion`) |
| `RocolaOs.Billar2Bajo.cs` | el bajo virtual fijo: |x| y sus filtros |
| `RocolaOs.Billar2Fft.cs` | la FFT real en coma flotante doble |
| `RocolaOs.Billar2Bandas.cs` | el ruido rosa por bandas EN MONO para afinar con el celular |
| `RocolaOs.Billar2Hoja.cs` | la hojita «Billar» de Ctrl+F4 |
| `RocolaOs.Billar2Prueba.cs` | la orden `rocola-xp --prueba-billar2 PARTE...` |
| `Pruebas/RocolaOs/billar-nuevo/prueba.sh` | la bateria (las cuentas, guardar y leer, y la hojita con la rocola de verdad fuera de pantalla) |

Las claves (todas con su valor de fabrica; se leen al armar cada cancion: un cambio vale desde la siguiente, que se mide otra vez):

| Clave | Fabrica | Que |
|---|---|---|
| `billar_nuevo` | 0 | la hojita «Billar» siempre a la vista (sin ella sale con `cadena_nueva=1`, la cadena nueva elegida en la hojita «Cadena» del marco). El tramo B NO depende de ella |
| `billar_paso_alto` | 80 | Hz, de 50 a 150: donde las bocinas dejan de sonar (hasta medirlo) |
| `billar_inclinacion` | -0.75 | dB por octava sobre 1 kHz, de 0 a -1.5 (la curva objetivo) |
| `billar_claridad_graves` | 2 | dB que se quitan en 250-400 Hz, de 0 a 4 |
| `billar_claridad_agudos` | 2 | dB que se ponen en 2-4 kHz, de 0 a 3 |
| `billar_afinacion` | 1 | usar lo medido con el celular (si un punto tiene las bandas de la voz) |
| `billar_afina_pond` | C | la app mide en dBA (A) o dBC (C) |
| `billar_afina_1` ... `_8` | (vacio) | lo que marco la app en cada punto: `63;125;250;500;1k;2k;4k;8k` en dB, `x` = sin anotar |
| `billar_bajo_virtual` | 0 | el bajo virtual fijo (decide la prueba ciega: regla 5 de la §1) |
| `billar_bajo_virtual_db` | -8 | su mezcla, de -14 a -4 dB |

## 1. El mono completo: (L + R)/2

`m = (L + s·R)/2` a los dos canales (`s = -1` solo si un canal viene al reves y nadie lo volteo antes; con el marco, los arreglos fijos ya lo
voltearon y aqui `s = +1`). Desde ahi todo el tramo es UN canal (la mitad de trabajo) y L y R salen iguales bit a bit.

**La ley: -6 dB (la media), no -3 dB (entre raiz de 2).** La ganancia de la cancion se mide DESPUES de la cadena (la sonoridad integrada y el
pico real de lo que sale, §1.2): una constante delante del mono se la come la ganancia y no cambia nada de lo que se oye. Lo que cambia con el
material (mono, estereo abierto, todo a un lado) es la SUMA misma, igual con las dos leyes; la ley solo pone el nivel de dentro. Asi que se
elige la que nunca sube un pico: la media no pasa del mayor de los dos canales (|(L+R)/2| <= max(|L|, |R|)), y lo mono pasa bit a bit; la de
-3 dB sube 3 dB lo mono (y su pico real), sin ganar nada. Medido (`mono`): la proporcion pico/eficaz sale igual con las dos (12,01 a 12,31 dB
segun el material) y con -6 dB el pico nunca pasa del de la entrada:

| Material | -6 dB: eficaz / pico | -3 dB: eficaz / pico |
|---|---|---|
| mono (L = R) | 0,0 / 0,0 dB | +3,0 / +3,0 dB |
| estereo sin correlacion | -3,1 / -2,9 | -0,1 / +0,1 |
| mezcla (voz al centro y ancho) | -0,6 / -1,0 | +2,4 / +2,0 |
| todo a un lado | -3,0 / -6,0 | 0,0 / -3,0 |

Un canal al reves sin voltear se cancela entero (-6000 dB en la prueba); volteado, sale la musica entera. Un canal mudo da la mitad (los
arreglos del marco copian el que suena antes).

## 2. El paso alto: Butterworth de 24 dB/oct, en fase lineal

La magnitud de un Butterworth de cuarto orden (-3 dB en `billar_paso_alto`, -24 dB una octava abajo) con FASE CERO: el grave que queda no se
mueve de sitio (un paso alto de fase minima gira el grave y SUBE los picos de lo masterizado aplastado: medido en `GraveMono.cs`, de 1,5 a 2,7
dB), y el tramo no tiene retardo (lee por delante). Es la misma curva del subgrave y del corte de noche de hoy.

**Por que 24 y no 12 dB por octava** (`pasoalto`, con 80 Hz):

| | 12 dB/oct | 18 dB/oct | 24 dB/oct |
|---|---|---|---|
| a 40 Hz (una octava abajo) | -12,3 dB | -18,1 | -24,1 |
| a 120 Hz (1,5 veces) | -0,78 | -0,37 | -0,17 |
| a 160 Hz | -0,26 | -0,07 | -0,02 |
| a un bombo de 50 Hz le quita | 8,8 dB | 11,9 | 14,8 |
| a uno de 60 Hz | 6,3 | 8,1 | 9,8 |
| su eco previo (frente al golpe) | -23,3 dB, desde 14 ms antes | -23,2, 24 ms | -23,4, 29 ms |

Las cajas de 12" tienen puertos: debajo de la sintonia del puerto el cono se queda sin freno y lo que llega solo gasta amplificador (cuatro
bocinas en paralelo) y mueve el cono de mas. Con 24 dB/oct se le quitan 6 dB mas al grave que no se oye (el bombo de 50 Hz), y lo que las
bocinas si dan (120-160 Hz, el bajo electrico) queda entero (-0,17 dB contra -0,78). El eco previo de la fase lineal es el mismo (-23 dB del
golpe) y empieza antes (29 ms), pero es grave de 60-80 Hz, justo donde las bocinas ya caen. Es la misma curva del corte de noche: los dos
caben en una sola.

**El corte de noche, en la misma curva.** Si `noche_corte=` (80 Hz) es MAYOR que el paso alto, el diseno trae otro filtro igual con el paso
alto en el corte de noche, y sale `(1 - m)·dia + m·noche`, con `m` = la mezcla del modo noche (`GraveMono.NocheMezcla`, su bajada de una hora
pareja en dB); con el filtro de fase cero eso es UNA curva cuyo paso alto va del uno al otro. Si no es mayor (80 y 80, lo de fabrica), no hay
nada mas: el paso alto de dia ya corta eso. Nunca dos filtros en serie. `m` se lee una vez por bloque (unos 87 ms) y va en linea recta de un
bloque al otro: con 0 a 1 en 2 s sobre un tono de 95 Hz, el paso mas grande entre dos muestras es 0,92 del del tono (sin escalones).
**El marco NO debe poner su `GraveMono.DeNoche` en la B** (ver §6).

**El diseno** (`RocolaOsBillar2Diseno`): la respuesta de fase cero `M(f) = paso alto · 10^(C/20)` muestreada en `L = 8K` puntos, su
respuesta al impulso recortada a `2K + 1` coeficientes con una ventana de TUKEY (plana en el 70 % del centro), `K = 4` periodos del paso
alto (2 205 a cada lado a 80 Hz y 44,1 kHz: 50 ms). La de Kaiser del subgrave de hoy estrecha la respuesta del grave: con ella el error era de
0,8 a 1,3 dB; con Tukey y 4 periodos, 0,004 dB (medido con numpy antes y en la prueba despues). Se aplica por FFT (solapar y guardar) de
`N = 8192` (la potencia de 2 de `2K + 1024` o mas): bloques de 3 782 cuadros y la primera lectura de 136 ms (con Spotify, que llega a su ritmo
con unos 250 ms de colchon, cabe). Se guarda por parametros y tasa (hasta 8): cancion tras cancion es el mismo; hacerlo cuesta 8 ms (aqui) y
se hace de fondo al arrancar la funcion, al abrir la hojita y al cambiar algo.

Medido (`pasoalto`, con 50, 80 y 120 Hz): contra el Butterworth, 0,003 a 0,005 dB donde esta sobre -20 dB y 0,010 sobre -40; rizo arriba de 2
veces el corte, 0,014 dB; la respuesta al impulso simetrica alrededor del impulso a 1e-16 y la fase a 1e-12 rad: retardo de grupo constante (0
muestras) de 20 Hz a 20 kHz.

## 3. La curva del billar: UNA etapa de tono, en fase lineal

`C(f) = T(f) + A(f) + V(f)`, acotada de -9 a +3 dB, y por el mismo FIR que el paso alto (un solo filtro: un solo leer por delante, el mismo
para las dos). **Por que fase lineal**: va en el mismo filtro que el paso alto (que es de fase lineal por el plan); no gira la fase, asi que no
sube los picos (la ganancia de las canciones que tope el pico real no baja); y es una curva suave, de octavas, cuyo eco previo es corto y
chico. La afinacion por octavas con un celular en varios puntos mide la tendencia del local (bocinas y nave promediadas), no los modos de un
punto, donde la de fase minima tendria ventaja.

- **T, la curva objetivo**: plana hasta 1 kHz y despues `billar_inclinacion` dB por octava (-0,75 de fabrica, en medio de lo que dice el plan:
  -0,5 a -1), con la rodilla suave (un cuarto de octava). Sin afinacion medida es todo lo del local (lo que se supone de unas bocinas planas,
  como la `InclinacionBillar` estimada de hoy).
- **A, la afinacion con el celular** (abajo).
- **V, la claridad de la voz**: una campana de -2 dB en 315 Hz (lo de 250-400, donde la voz se embarra en una nave) y otra de +2 dB en 2,8 kHz
  (2-4 kHz, las consonantes), de 0,4 octavas de sigma: a media altura en 230-430 Hz y 2-3,8 kHz. Moderadas (la investigacion: -2/-3 y
  +1,5/+3), ajustables de 0 a 4 y de 0 a 3.

La de fabrica sin afinacion (con el paso alto): 63 Hz -8,9 · 125 -0,1 · 250 -1,4 · 315 -2,0 · 500 -0,5 · 1k -0,1 · 2k +0,2 · 2,8k +0,9 · 4k
-0,6 · 8k -2,2 · 16k -3,0 dB. Medido (`curva`): la curva sola contra lo pedido, 0,009 dB de 20 Hz a 20 kHz, simetrica; paso alto y curva en un
solo filtro, 0,036 dB donde esta sobre -20 dB; con todo al extremo, de -7,3 a +3,0 (los topes).

### La afinacion de varios puntos (`RocolaOsBillar2Afinacion`)

La de Ctrl+F3 (`AfinarLocal.cs`) tenia un punto (el dueno pidio el 27-10-2026 «lo mas simple posible»). Aqui, de 1 a 8: con uno basta.

1. La rocola, callada, toca ruido rosa por octavas (63 Hz a 8 kHz), 8 s cada una con 1,5 s de silencio, a -20 dB eficaces, **EN MONO**: la B
   toca lo mismo por todas las bocinas, y en el grave dos bocinas repartidas se suman o se restan segun donde este uno; eso es lo que hay que
   medir (el de Ctrl+F3 le da a cada canal su ruido, lo justo para la cadena estereo de hoy).
2. En cada punto, el dueno anota lo que marca la app (dBA o dBC: se le devuelve lo que la ponderacion le quita a cada banda, IEC 61672).
3. De cada punto vale su **FORMA**: cada banda menos la media de la zona de la voz (250 Hz a 2 kHz) del mismo punto; un punto sin esas cuatro
   no vale. Asi un punto pegado a una bocina (que marca todo 10 dB mas) pesa igual que uno lejano.
4. La forma del local por banda: la media de los puntos que la tienen; con 5 o mas, sin el mas alto ni el mas bajo (un punto junto al woofer
   no manda en 63 Hz). Y su dispersion (cuanto cambia de un punto a otro).
5. La correccion: `-fuerza · forma`, con la fuerza al 70 % con un punto (como la de hoy: un celular no es un microfono de medida) y al 80 % con
   tres o mas (lo de en medio, en linea recta); 1 dB de zona muerta; baja hasta 6 dB y sube hasta 3 (8 kHz hasta 2, 63 Hz nunca); y **nunca
   se sube un hueco**: una banda 3 dB o mas por debajo de su vecina mas baja, o (con 3 puntos o mas) que cambia mas de 4 dB de un punto a otro
   (un modo o una cancelacion: subirla solo gasta amplificador).
6. Entre bandas, media curva de coseno en octavas; debajo de 63 Hz, lo de 63; de 8 a 16 kHz se va a la mitad (el microfono de un celular lee de
   menos arriba).

Medido (`afinacion`): ocho puntos iguales dan la forma de uno sin dispersion; un punto con todo +10 dB no cambia la forma; seis puntos con uno
de 63 Hz +15 dB: se quita (queda en +0,00); un hueco de 7 dB en 1 kHz no sube; una banda que cambia 5 dB entre puntos no sube; los topes (-6,
+3, +2 y 0 en 63 Hz); un punto sin 250 Hz no vale; un local plano medido en dBA sale plano; el texto de `rocola_os.txt` va y vuelve.

## 4. El bajo virtual fijo (apagado: decide la prueba ciega)

Las bocinas no dan lo de debajo del paso alto; el oido reconstruye un bajo que no suena si oye sus armonicos. Aqui sin NADA que siga a la
senal (el `DeepBassProvider` de hoy tiene envolvente y por eso no entra en la cadena nueva):

- **La fuente**: lo que el paso alto quita (`1 - |paso alto|`, de fase cero, de la misma FFT de cada bloque), sin lo de debajo de 25 Hz
  (Butterworth de 2.o orden) para no hacer armonicos de un retumbe.
- **La no linealidad: |x|.** Es homogenea de grado 1: `|a·x| = a·|x|` para todo `a >= 0`. Todo lo demas del camino es lineal y fijo, asi que
  el bajo virtual entero cumple `BV(a·x) = a·BV(x)`: si la cancion sube 20 dB, los armonicos suben exactamente 20 dB, nunca cambia su
  proporcion con la musica. Es la UNICA funcion sin memoria que cumple eso y hace armonicos: `f(a x) = a f(x)` para todo `a > 0` obliga a
  `f(x) = p·x` para `x > 0` y `q·x` para `x < 0`, o sea `p'·x + q'·|x|`; lo lineal no hace armonicos. `x·|x|` (lo que sugeria el plan) es de
  grado 2: con 20 dB mas, 40 dB mas de armonicos (no vale). `|x|` hace armonicos pares: el 2.o a -7,5 dB del tono (4/3pi), el 4.o 14 dB debajo
  de el, el 6.o 21.
- **La continua y lo que se mueve despacio**: |x| lleva continua (2/pi de la amplitud) que sube y baja con el grave (una envolvente). La quita
  EXACTA un paso alto Butterworth de 4.o orden en el mismo corte de las bocinas (dos bicuadradas con su cero doble en continua; la suma de sus
  coeficientes es 0 exacto en coma flotante): a 10 Hz quita 72 dB. Arriba, un paso bajo de 2.o orden en 4 veces el corte (320 Hz): armonicos
  solo en la banda que las bocinas si dan, sin zumbido.
- **La mezcla** (`billar_bajo_virtual_db`, -8 de fabrica, de -14 a -4): donde queda el 2.o armonico de un tono de la fuente frente a ese tono.
  Medido: un tono de 45 Hz a 0 dBFS da su 2.o armonico (90 Hz) a -10,8 dB del tono (los -8 de la mezcla, -0,9 de la fuente en 45 Hz y -1,4 del
  paso alto en 90 Hz), el 4.o a -23,7 y el 6.o a -32,5.
- **Homogeneo, medido** (`bajo`): de -20 a 0 dBFS el 2.o armonico sube 20,0000 dB, y muestra a muestra lo de 0 dBFS es 10 veces lo de -20
  (1,6e-7 del pico). Sin continua (2,8e-11 de su eficaz).
- **Sin aliasing, medido**: con tonos de 35, 45 y 70 Hz de periodos justos en una ventana de 65 536 cuadros, todo lo armonico cae en sus
  multiplos y lo doblado por arriba de 22 kHz cae fuera: lo mas alto fuera de los armonicos queda de -102 a -109 dB del 2.o armonico. No hace
  falta sobremuestrear (|x| baja 12 dB por octava y la fuente casi no tiene nada encima de 2 veces el corte).
- Va despues de la curva (el orden del plan): sus armonicos no pasan por la afinacion.

## 5. La hojita «Billar» de Ctrl+F4

Sale con la cadena nueva elegida (`cadena_nueva=1`, la hojita «Cadena» del marco) o con `billar_nuevo=1`; si no, no sale y las pestanas de
siempre no se mueven (las pruebas de las demas hojas cuentan pestanas y apuntan a su sitio). Con ella son seis o siete; va pegada a la
derecha delante de «Archivos» (en 818,54 de la pantalla de 1024x768, con o sin la «Cadena» del marco).

- **AFINAR CON EL CELULAR**: los botones 1 a 8 eligen el punto (verde = vale; ambar = le faltan bandas de la voz), «Tocar las 8 bandas (de
  grave a agudo, en mono)» con la que suena en grande, una barra por banda (lo que marca la app, 40 a 120 dB, o «sin anotar»), «Tu app dice:
  dBA / dBC», «Borrar este punto», «Traer la de Ctrl+F3» (la de un punto de la cadena de hoy, al punto elegido) y cuantos puntos valen.
- **LA CURVA DEL BILLAR**: el dibujo (la `CurvaEqView` de Ctrl+F3: lo que hace la B -paso alto y curva-, la curva del dueno de la memoria del
  billar y la suma), lo que la afinacion corrige banda por banda y por que una no sube, y los mandos: paso alto (de 5 en 5 Hz), agudos por
  octava (de 0,25 en 0,25), las dos campanas de la claridad (de 0,5 en 0,5 dB), «Usar la afinacion del celular», «Bajo virtual» y su mezcla.

Cada cambio se guarda al momento en `rocola_os.txt` y vale desde la siguiente cancion (la que se vuelve a medir: cambia la huella). El ruido por
bandas suena por su propia salida (`CreaWaveOut`, como la prueba de bocinas de XP) con la rocola callada: **con la salida nueva (siempre abierta
en `hw:`) eso choca**: el agente de la salida tiene que darle un camino (ver §6).

## 6. Como se arma el tramo B (el contrato del marco, CADENA_NUEVA.md §8.1)

- En el `[ModuleInitializer]` de `RocolaOs.Billar2.cs`: `RocolaOsCadenaB.Arma = RocolaOsBillar2ArmaB` y `RocolaOsCadenaB.Huella =
  RocolaOsBillar2.Huella`.
- `Arma(receta, entrada)`: lee los ajustes (una foto de `rocola_os.txt`: nada del estado de XP), busca el diseno (hecho de fondo casi siempre;
  si no, lo hace: 8 ms aqui la primera vez despues de un cambio) y devuelve UNA pieza, `RocolaOsBillar2Tramo`, anotada con `r.Anota`. No
  voltea nada (los arreglos del marco ya voltearon el canal invertido). Con `r.Midiendo`: de dia (sin la mezcla de noche) y sin leer nada de XP;
  sonando: con el corte de noche (`NocheCorteHz()`) y la mezcla (`GraveMono.NocheMezcla`). Si algo lanza, el marco pone una pieza de paso.
- `Huella()`: el paso alto, la curva (objetivo, claridad y la correccion de la afinacion ya calculada) y el bajo virtual; SIN la noche. Si
  cambia, lo medido con la B ya no vale. Una lectura del celular que no cambia la curva (un hueco que no sube, un tope) no la cambia.
- **El disco continuo** (`Sigue(IRocolaOsPieza anterior)`, como pide el contrato). EL MODO EXACTO: si `Sigue` se llama ANTES de que acabe la
  pista de antes (al preparar el relevo), la de antes, al acabar su origen, lee los primeros K cuadros (50 ms) del origen de la siguiente (en el
  hilo del sonido), sale con ellos y se los pasa a la siguiente, que empieza por ellos: lo que sale es cuadro a cuadro lo de las dos pistas
  pegadas en una sola (0 de diferencia; 1,9e-9 con el bajo virtual). Si `Sigue` llega tarde (la de antes ya acabo): EL EMPALME, como el grave
  en mono de hoy: sin escalon en el cambio (el salto de la muestra -1 a la 0 es el de la musica), pero la cola de la de antes (sus ultimos 50
  ms) salio como si despues viniera silencio: el paso alto deja pasar ahi la mitad del grave que quita (+4,9 dB sobre el eficaz de lo que sale
  con un bajo muy cargado) y lo de arriba de 1 kHz cambia -14 dB del eficaz alrededor del cambio: un posible chasquido. Por eso EL MARCO DEBE
  LLAMAR A `Sigue` AL PREPARAR EL RELEVO, no al empezar la siguiente. El modo exacto no se hace si la siguiente lleva cebado (`Cebado`, para
  etapas de delante que lean por delante: no hay en la B), ya empezo o es de otro diseno; `ExactoAlFinal = false` lo apaga (si algo mas leyera
  el origen de la siguiente antes de que suene).
- **Lo que el marco tiene que hacer distinto en la B**: (1) NO poner su `GraveMono.DeNoche` (el corte de 80 Hz de noche) despues de la ganancia:
  ya va dentro del paso alto, en la misma curva (dos en serie es lo que el plan prohibe); los graves de noche de 120 Hz (`SalaLocal`) y el
  `FactorNoche` si son suyos; (2) llamar a `Sigue` al preparar el relevo (arriba). (3) El agente de la salida nueva: un camino para el ruido por
  bandas (o la hojita lo dice: «No se pudo abrir la salida de sonido»).

## 7. Las pruebas (`Pruebas/RocolaOs/billar-nuevo/prueba.sh`)

`bash prueba.sh PROGRAMA [partes]` (JIT: la carpeta con `rocola-xp.dll`; NativeAOT: `aot:CARPETA`); con otros agentes trabajando, con el candado
de lo pesado (`flock -o /root/rocola-trabajo/fase11/pesado.lock`). Resultado el 27-11-2026, rama del agente (sobre `2832875`, el contrato del
marco): **86 de 86 en NativeAOT** y en JIT. `dotnet build`: 0 errores, los 13 avisos de siempre; `dotnet publish` NativeAOT: 0 avisos IL/AOT;
`ganchos.py`: TODO BIEN (no hay ganchos nuevos). La hoja con la rocola de verdad se repitio 6 veces: bien con el candado; una vez, sin el y con
la maquina a carga 10-15 (otros tres agentes), la pantalla se quedo parada mas de 30 s antes de cambiar de pestana (no se volvio a ver).

| Parte | Que | Resultado |
|---|---|---|
| fft | la FFT real contra la DFT directa e ida y vuelta | 6,6e-12 / 6,7e-16 |
| mono | lo mono igual, el canal al reves, el mudo, las dos leyes | ver §1 |
| pasoalto | respuesta, fase lineal, 24 dB/oct, el orden y el eco previo | ver §2 |
| curva | la curva sola, con el paso alto, los topes | 0,009 / 0,036 dB |
| noche | una sola curva con la mezcla en 0, 0,5 y 1 (0,036, 0,032, 0,015 dB), la rampa sin escalones | ver §2 |
| bajo | homogeneo, sin continua, sin aliasing | ver §4 |
| constante | ruido rosa con escalones de +-10 dB cada 2 s, en bloques de 10 ms contra la misma sin escalones: mono, paso alto, curva, todo, con el bajo virtual y con la noche a 0,7 | 0,000000 dB (tope 0,05); el control, un compresor de juguete: 5,0 dB (la prueba si ve la dinamica) |
| disco | dos pistas seguidas contra una sola: modo exacto y empalme, con y sin el bajo virtual | ver §6 |
| afinacion | formas, recorte, huecos, dispersion, topes, dBA, el texto | ver §3 |
| guarda | `rocola_os.txt` con `ROCOLA_OS=billar_nuevo`: guardar, lo que no vale, volver a leer, los parametros, la huella | 7 de 7 |
| cpu | ms de procesador por segundo de musica (NativeAOT, aqui) | de dia 2,4; en la bajada de la noche 3,5; con el bajo virtual 3,7; todo 5,0 (0,24 a 0,50 % de un nucleo); el diseno, 8 ms una vez |
| hoja | la rocola fuera de pantalla: Ctrl+F4, la pestana, una lectura, dBA, el paso alto, tocar las bandas; sin la cadena nueva no sale; con `cadena_nueva=1` si | 10 de 10 |

## 8. Lo no probado

- NADA se oyo. Nada en el billar ni en el A4: ni el paso alto en sus bocinas (80 Hz es un supuesto hasta medir donde dejan de sonar), ni la
  curva objetivo, ni la claridad, ni el bajo virtual (que decide la prueba ciega).
- La afinacion de varios puntos no se ha hecho con un celular de verdad; el ruido por bandas en mono no se ha oido; con la salida nueva no tiene
  por donde sonar todavia.
- El costo en el A4 (aqui, 0,24-0,50 % de un nucleo; el A4 es mas lento por ciclo).
- Juntado con el marco: la B armada por `RocolaOsCadenaNueva` de verdad (este agente solo tenia el contrato, `2832875`), el relevo llamando a
  `Sigue` a tiempo, y que el marco quite su corte de noche en la B.
- Con Spotify (llega a su ritmo: la primera lectura de 136 ms cabe en su colchon de unos 250 ms; no medido con Soloist).
