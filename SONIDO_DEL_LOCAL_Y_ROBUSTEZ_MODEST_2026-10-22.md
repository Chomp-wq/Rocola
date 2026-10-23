# Sonido del local y robustez — Modest, 22 de octubre de 2026

Destino: AMD A4-6300 (dos nucleos), 3,5 GB de RAM, SSD, Windows XP / .NET Framework 3.5. Solo la rama `claude/modest-carson-fa2v2n` (HD sigue suspendida). Punto de partida `8b12902`.
Peticion del dueño del local (22-10-2026): seis cosas que se oyen o que cuidan el sonido, elegidas de la lista de propuestas. Tres se oyen y llevan su mando; tres no cambian el sonido y son automaticas.

**Todas las cifras de este documento son sinteticas, de Linux/Mono, con tonos y ruido, y no valen para el A4.** Nada se ha probado en Windows XP ni con las bocinas del billar: lo que se oye solo se sabe en el local
(al final, que escuchar).

| # | Que es | Mando | De fabrica |
| --- | --- | --- | --- |
| A | Recopilatorios parejos | ninguno (automatico) | — |
| B | Graves del local y Agudos del local | dos deslizadores de −6 a +6 dB | 0 dB |
| C | Presencia en sala ruidosa | un interruptor | apagado |
| D | La siguiente cancion pasa antes que las caratulas cuando quedan 45 s | ninguno (automatico) | — |
| E | Caja negra del sonido | ninguno (automatico) | — |
| F | Tres robusteces (limitador, entrada con fundido=0, relleno MP3) | ninguno (automatico) | — |

Los mandos de B y C estan en **Ctrl+F3 → Sonido → El local y sus bocinas → «Tono y presencia del local...»**. La hoja del local ya estaba llena (subgrave, estereo, claridad, partes suaves, balance, prueba de bocinas), asi que
la nueva es una segunda hoja del mismo grupo: un boton abajo a la izquierda la abre y su «Volver al local y sus bocinas» regresa. Sin barras de desplazamiento y nada debajo de «Listo». «Dejar como de fabrica» los vuelve a
0 dB y apagado. Se guardan en `rocola_config.txt` (`graves_local=`, `agudos_local=`, `presencia_sala=`); una configuracion vieja sin esas claves arranca en 0 dB y apagado. Se oyen al instante en la cancion que suena
y en la siguiente ya preparada. Con los tres en su valor de fabrica, el sonido sale **bit a bit** como antes (comprobado, ver abajo). Ninguno toca el volumen de Windows ni el de la salida.

## A. Recopilatorios parejos (`NivelDisco.cs`)

**Que hacia mal.** Cuando suenan seguidas pistas de la misma carpeta, la rocola las nivela con la ganancia del disco entero para que la balada siga siendo mas bajita que el tema fuerte, como se grabo (nivel por disco, 28-9-2026).
Eso es lo correcto en un disco, pero un «Exitos 90s» o un «Varios» no es un disco: son grabaciones de epocas distintas y, con una sola ganancia para todas, una queda 5 a 8 dB mas baja que la de al lado.

**Que hace ahora.** Mira las pistas medidas de la carpeta; si la mas fuerte y la mas baja se llevan **mas de 5 dB**, no hay un volumen de disco que respetar y cada cancion se nivela por su cuenta, como si no hubiera racha. Exactamente 5 dB todavia es un
disco. Las pistas de **menos de 30 s** (intros, cortinillas) no cuentan para esa cuenta: en un disco de verdad siempre hay una muy bajita y volverian recopilatorio a cualquiera. No lleva interruptor (`NivelDisco` nunca lo tuvo).

**De donde sale el 5.** Es la cifra que se pidio; en esta maquina **no hay una `rocola_sonoridad.txt` medida de un local de verdad** (la biblioteca de prueba es sintetica), asi que no hay una distribucion real que mirar. El criterio es de
oido: los discos de estudio rara vez pasan de 3 a 4 dB entre su pista mas fuerte y la mas baja (sin contar intros), y los recopilatorios si. Cuando el repaso ya haya medido el local, esta cuenta da la distribucion real (carpetas, cuantas pasan de 5 dB y cuales):

```python
# python3 -I dispersion.py rocola_sonoridad.txt [umbral_dB]
import sys, os, collections
ruta = sys.argv[1]; umbral = float(sys.argv[2]) if len(sys.argv) > 2 else 5.0
carpetas = collections.defaultdict(list)
for linea in open(ruta, encoding='utf-8', errors='replace'):
    p = linea.rstrip('\r\n').split('|')
    if len(p) < 5 or not p[0].isdigit() or p[1] == 'muda': continue
    try: lk = float(p[1]); pesos = float(p[2]); bloques = int(p[3])
    except ValueError: continue
    if bloques < 300 or pesos <= 0: continue          # menos de 30 s: no cuenta
    carpetas[os.path.dirname(p[-1].replace('\\', '/'))].append(lk)
filas = sorted(((max(v) - min(v), c, len(v)) for c, v in carpetas.items() if len(v) >= 2), reverse=True)
n = len(filas); mayores = [f for f in filas if f[0] > umbral]
print("carpetas con 2 o mas pistas medidas: %d; con mas de %.1f dB: %d (%.1f %%)" % (n, umbral, len(mayores), 100.0 * len(mayores) / max(1, n)))
for d, c, k in filas[:15]: print("  %5.1f dB  %2d pistas  %s" % (d, k, c))
```

Si un disco de verdad con una balada muy bajita (mas de 5 dB por debajo de la mas fuerte) pasa a nivelarse pista por pista, es el precio de la regla: se nota como que la balada sube a su volumen de siempre. Si molesta, el limite es la constante
`DispersionDiscoDb` (5) y `MinBloquesDispersion` (300 bloques de 400 ms = 30 s) en `NivelDisco.cs`.

**Pruebas.** `discoparejo` (21 comprobaciones): 2 dB sigue siendo disco; 5 dB justos, tambien; 5,5 y 8 dB, cada una la suya; una intro de 10 s a 18 dB por debajo no cuenta; los topes de pico se conservan (la ganancia del disco respeta los picos de su pista mas fuerte,
y en modo recopilatorio cada una respeta el suyo). Los casos que ya habia con un disco de 8 dB de dispersion (una balada a −20 entre dos temas a −12) se cambiaron a 4 dB, que sigue siendo un disco; con 8 dB ahora son un recopilatorio.
**Coste:** una cuenta por pista del disco al armar cada cancion; nada por muestra.

## B. Graves del local y Agudos del local (`SalaLocal.cs`)

**Que son.** Dos mandos de tono para afinar el sonido al local sin tocar el EQ automatico: **Graves** (un escalon de bajos alrededor de 120 Hz) y **Agudos** (un escalon de altos alrededor de 6 kHz), de **−6 a +6 dB de 1 en 1**.
Son los dos filtros de escalon (*shelf*) de las notas de Robert Bristow-Johnson (Q = 0,707), en coma doble, dentro de la etapa del local, detras de la claridad y delante de las partes suaves. Con +6 dB de graves, 40 Hz sube 5,9 dB y 120 Hz sube 3 dB; el medio y los agudos
no se tocan. Con +6 de agudos, 12 kHz sube 5,8 dB y 6 kHz 3 dB; los graves y el medio no.

**Sin chasquidos.** El valor sube o baja en una rampa de 50 ms para los 12 dB enteros (un escalon de 1 dB tarda 4 ms) y los coeficientes se recalculan en cada cuadro mientras dura; al arrancar desde 0 la memoria del filtro parte de la musica que suena, no de cero.
Pasar de −6 a +6 sonando, o encender y apagar, no da salto mayor que la pendiente del propio tono. Las pistas seguidas del mismo disco heredan su estado (`Sigue`), como el resto de la etapa del local. A 0 dB los dos, la etapa no toca la muestra (bit a bit).
«Escuchar sin mejoras» (Lo que suena ahora) los pone a 0 mientras dura y al terminar vuelven.

**Pruebas.** `salalocal` seccion 8 (36 comprobaciones nuevas, 103 en total): bit a bit a 0 dB con uno y dos canales, seis canales intactos, la respuesta contra la teoria a 40 Hz, 120 Hz, 1 kHz, 6 kHz y 12 kHz, de 1 en 1, tope de 6 dB aunque se pida mas, los dos juntos sin pisarse,
sin golpe al encender / dar la vuelta / apagar, la rampa (a los 2 ms lleva 0,5 dB de 6; a los 32 ms ya esta), disco continuo, lecturas de una muestra o impares, sin desnormales tras el silencio, sin memoria reservada al leer, y la configuracion y la ventana. `gravemono` y `ventanaf3` (la hoja nueva, con letras de Mono y DejaVu) siguen en verde.
**Coste (Mono/Linux, estereo 44,1 kHz):** con los dos escalones en uso, unos **3 ms de procesador por cada segundo de musica** (5,8–6,8 ms contra 3,0–3,1 ms de la copia de la prueba); a 0 dB, nada. En el A4 sera varias veces eso: medirlo alli.

## C. Presencia en sala ruidosa (`PresenciaSala.cs`)

**Que hace.** Un compresor suave entre la etapa del local y el grave sintetico. Con gente y bolas, lo que mas se pierde de una cancion es lo que va por debajo: la voz que baja en la estrofa, la intro. «Que las partes suaves se oigan» sube hasta 4 dB lo bajito de una cancion que va baja
en conjunto; la presencia hace otra cosa, mas continua: aprieta un poco lo que pasa del volumen de siempre y le devuelve esa ganancia a todo, asi que la distancia entre lo fuerte y lo bajito se acorta unos 3 dB y el volumen general casi no cambia.

- **Umbral:** 6 dB por debajo del volumen objetivo del nivelado, medido **sin** el volumen general ni el modo noche (se divide por el Master de la cadena, como en las partes suaves): bajar el volumen de madrugada no cambia lo que hace.
- **Forma:** 2 a 1 con rodilla suave de 6 dB; ataque de unos 15 ms, suelta de unos 250 ms (medido: a los 15 ms lleva el 57 % de la reduccion, a los 80 ms el 99 %; la suelta, 63 % a los 250 ms).
- **Enlazado:** los dos canales comparten **una** ganancia (la potencia media de los dos): la imagen estereo no se mueve.
- **Ganancia de compensacion automatica:** devuelve hasta 3 dB, y **nunca mas de lo que deja el techo del limitador**: se recuerda el pico ya comprimido (2 s enteros, despues baja con 2 s de constante) y la compensacion se recorta para que ese pico no pase de `TechoPicoReal` (0,88) con un margen de 0,13 dB.
  Sube despacio (250 ms) y baja deprisa (15 ms); cerca del silencio no sube nada (no se levanta el soplido). Sin mirar hacia adelante no puede saber que viene un pico mas alto que todos los de los ultimos segundos: ese, si llega, lo recorta el limitador de pico real que va despues (que solo baja).
- **No toca el volumen de Windows ni el de la salida:** solo multiplica la señal de esa cancion (la prueba mira que la etapa no llame a nada de volumen).

Medido con tonos (rms relativo al nivel sin Master): −40 dB → +3,0 dB; en el umbral (−17,7 dB) → +2,6 dB; −8 dB → −1,9 dB; −3 dB → −4,4 dB; −62 dB → 0 dB. O sea, el abanico entre una parte bajita y una fuerte se cierra unos 5 dB en estos tonos.
Apagado (de fabrica) la etapa **no toca ni una muestra**; encenderlo o apagarlo a media cancion no da chasquido (la ganancia parte de 1 y llega con el ataque; al apagar vuelve a 1 en ~40 ms). Hereda en el disco continuo. «Escuchar sin mejoras» lo apaga mientras dura.

**Pruebas.** `presenciasala` (47 comprobaciones): apagado bit a bit con 1, 2 y 6 canales y a menos de 8 kHz; la ganancia contra la teoria a cinco niveles; el Master a la mitad da lo mismo; ataque y suelta; una sola ganancia para los dos canales; el techo con pulsos de 0,8 sobre un fondo bajo
(la salida no pasa de 0,88); sin golpe al encender y apagar; disco continuo; cualquier tamaño de lectura bit a bit; sin memoria reservada; la cadena (va detras del local y delante del grave sintetico y del limitador), la configuracion y la ventana.
**Coste (Mono/Linux):** **0,7–1,0 ms de procesador por segundo de musica** encendido (2 multiplicaciones y una comparacion por muestra y 3 o 4 funciones trascendentes cada 32 cuadros, 0,7 ms); apagado, cero.

## D. La siguiente cancion pasa antes que las caratulas (`Form1.SinPausa.cs`, `Form1.Portadas.cs`)

El calentador de la siguiente cancion espera 1,5 s al empezar, cede el paso mientras haya caratulas en camino y vuelve a mirar en cada trozo. Es lo correcto con minutos por delante. Pero si la siguiente se pidio tarde (la cola estaba vacia y entra una moneda a 40 s del final) o hay muchas caratulas
cargando, la cancion podia llegar sin estar leida ni abierta. Ahora el reloj de la cancion (cada 500 ms) apunta lo que le queda (`quedanMs`, volatil) y, **con 45 s o menos**, el calentador se salta las tres esperas y la lee y abre ya. Con mas tiempo, todo igual. Una espera de caratulas ya empezada
se rompe sola en cuanto la cancion entra en los ultimos 45 s (el hilo vuelve a mirar cada 300 ms). Lo que queda se olvida al soltar la cancion o al pasar al relevo sin pausa. No cambia el sonido: solo el orden en que se usa el disco.

**Pruebas.** Bateria nueva `adelantoprioridad` (23 comprobaciones): la regla (45 s si, 46 no, sin dato no); el reloj; el hilo de verdad con un archivo en disco (con 240 s por delante no lee en 1 s y despues si, a los 1,6 s; a 40 s del final la deja en memoria en 128 ms; a 40 s y con una caratula cargando, tambien en 128 ms; con 240 s y caratulas
cargando no lee en 3 s, y al bajar a 44 s lee a los 420 ms); el cableado. `sinpausa`, `precarga`, `calienta`, `enmemoria`, `etiquetadelante`, `bloqueos` y `tirones` siguen en verde. **No se midio un disco duro ni el A4:** la ganancia en tiempo la dicen las esperas que desaparecen (1,5 s + las de caratulas + las de cada trozo), no un cronometro de HDD.

## E. Caja negra del sonido (`SalidaSonido.cs`)

El hilo de la musica cuenta los cortes que no alcanzo a tapar (`BombaSonido.Huecos`) y el medidor de la salida ve los avisos que llegan tarde, pero nada de eso quedaba escrito: en el bar, «a veces se corta» no dejaba rastro. Ahora, **al acabar o saltar una cancion con algo que contar**, se apunta una linea en `rocola_error.txt`:

```
2026-10-22 21:14:07  [Cortes de sonido] "Cumbia del billar": 2 cortes del hilo de la musica (silencio entregado), 1 aviso tardio de la salida (el peor, 410 ms); latencia abierta 750 ms
2026-10-22 21:14:07  [Latencia de audio] sube el margen de 750 a 1000 ms (atasco de 700 ms)
```

La segunda linea sale cuando cambia el nivel de latencia del perfil de la salida (baja un escalon tras 30 s de musica sin atascos, sube por un atasco o por un fallo de la salida). Sin nada que contar no se escribe nada, ni se crea el archivo. Se escribe por la bitacora de fondo, asi que ni la musica ni la pantalla esperan al disco; los cambios de nivel se
anotan en numeros dentro del perfil (sin reservar memoria en la musica) y la pantalla los escribe en su reloj. Lleva el sello de version y el tope de 256 KB de siempre. Si una cancion tiene cortes, la cuenta es desde la ultima linea (no se repiten).
**Pruebas:** `bomba` seccion 10 (17 comprobaciones nuevas: el texto, que sin cortes no se escribe, la linea con la cancion, la cuenta y la latencia, los cortes nuevos desde la ultima, los avisos tardios, el cableado) y `latenciaadaptable` (13 nuevas: cada cambio de nivel con su motivo, el tope de la lista y el conteo de avisos tardios solo del lado de la salida).

## F. Tres robusteces

- **(a) El limitador de pico real con lecturas de menos de un cuadro (`PicoReal.cs`).** Pedirle una sola muestra en estereo devolvia 0, y quien lo lee (el encadenado sin pausa) toma 0 por el *final de la cancion*. Ahora el cuadro se lee entero aparte, se entrega la parte pedida y el resto sale en la lectura siguiente
  (como el grave en mono y el local). Las lecturas de un cuadro o mas no cambian ni un bit. Bateria nueva `picomuestras` (11): con 1, 2 y 6 canales, de a una muestra y a tamaños raros sale lo mismo que todo junto; con la version anterior falla. (El fundido de salida no tenia el problema: pasa lo que le entra.)
- **(b) Con `fundido=0` la entrada de 5 ms tampoco desaparece (`Form1.Sonido.cs`).** Sin fundido de salida tampoco habia fundido de entrada, y una cancion que empieza en su pico arrancaba de golpe (primera muestra 0,586 en la prueba). Ahora siempre hay `FadeProvider`: sin fundido de salida lleva total 0 (no baja nunca) pero conserva su entrada de 5 ms
  (0,3 s al retomar). `decimatanda` lo comprueba y falla con la version anterior. Es el unico cambio de esta lista que cambia las muestras, y solo con `fundido=0` (el de fabrica es 0,5 s y sale bit a bit igual).
- **(c) El relleno MP3 se reintenta una vez (`RellenoMp3.cs`).** La medida del descompresor del equipo (cuanto retrasa, para quitar el relleno del codificador y que no haya huecos entre pistas) se hacia una vez por sesion y, si fallaba por algo pasajero (poca memoria justo en ese momento), quedaba perdida hasta cerrar la rocola. Ahora, pasados 5 minutos del fallo, se mide **una** vez mas; si
  vuelve a fallar se queda como antes (los MP3 suenan sin recortar). `relleno` lo comprueba (el fallo no se repite de inmediato; el reintento acierta; si falla otra vez no hay un tercero).

No se hizo (d) (el escalon teorico entre pistas de un disco continuo): solo si se oyera.

## Comprobaciones de todo el conjunto

- **Salida bit a bit con las funciones nuevas apagadas** (graves y agudos a 0 dB, presencia apagada): el PCM de 16 bits de la cadena completa (`OpenAudio` + `ArmaCadena` + dither con semilla fija), 30 s de cada una de **10 canciones** (2 MP3 estereo de unos 200 s, un WAV de 230 s, y siete variantes de MP3: mono 44,1 kHz, estereo 48 kHz, mono 22 kHz, muy fuerte, con silencios,
  con un canal mudo, con continua) en cuatro modos (de fabrica y «todo encendido» —DSEE, subgrave, estereo estrecho, claridad, partes suaves, balance, I1, I2, I3, S1, S2, S3, EQ—; cancion nueva y ya analizada), contra el ejecutable de `8b12902` en otro proceso: IGUALES (40 de 40 con el perfil de tarjeta y 40 de 40 con el de CPU, 0 diferencias).
  Tambien lo aprendido en vivo (sonoridad y huella del EQ). Hecho con `igual_cadena.sh` (hash FNV del PCM y de lo aprendido).
- **Render por CPU congelado:** un ejecutable CPU nuevo contra `head_cpu.exe` con `cpucongelado`: `PASS CPU: 14 metodos con instrucciones y operandos resueltos identicos al ejecutable previo`.
- **Suite completa** (`sh todo.sh`): fallan solo los nombres que ya fallaban en Linux/Mono (abc atraccion auditoriageneral calma caratulasadaptativas colacompacta descompresor discoacotado discovivo duerme estilo iasdisparo leer memoriaintegrada moneda mp3medidaligera paginar pantallaviva partescaratulas planbiblioteca sala tope vigiapantalla); cintasligeras, estadoeficiente, hojeoportadas, resolucionligera, trabajovisible y auroraeficiente fallan sin la configuracion auxiliar de Mono y pasan con ella. Ninguna bateria nueva falla.
- Compila sin avisos con la receta de Release (`-sdk:2 -langversion:3 -warn:4 -warnaserror+`), los dos perfiles.

## Que escuchar en el local

1. **Recopilatorios:** poner un «Exitos»/«Varios» y dejar que suenen seguidas varias pistas: ya no deben quedar una 5–8 dB mas bajita que la de al lado. Poner despues un disco de estudio completo: sigue sonando como se grabo (la balada mas bajita que el tema fuerte).
2. **Graves y agudos:** con musica sonando, mover cada deslizador de 1 en 1 dB y escuchar el cambio en el acto, sin chasquido. Empezar por ±2 o ±3: mas alla de 4 dB se come margen del limitador (un bajo sostenido con +6 puede pasar del techo y el limitador baja toda la cancion). Comparar con «Escuchar sin mejoras» para saber de donde se parte.
3. **Presencia en sala ruidosa:** con gente y bolas, encenderla en una cancion con estrofas bajitas: la voz de la estrofa debe notarse mas sin que el estribillo suene mas fuerte. Si «bombea» (el fondo sube cuando para el golpe) o se oye aplastada, apagarla. No debe haber chasquido al encenderla ni apagarla. El volumen que se oye es el mismo en promedio; no cambia el volumen de Windows.
4. **La siguiente a 45 s:** poner una cancion tarde (con la cola vacia, a menos de 45 s del final de la que suena) con muchas caratulas cargando (pasando paginas deprisa): la siguiente debe entrar sin pausa. Si entra con un hueco, mirar `rocola_error.txt`.
5. **Caja negra:** despues de un dia, abrir `rocola_error.txt` y buscar `[Cortes de sonido]` y `[Latencia de audio]`: si no hay lineas, no hubo cortes. Si las hay, anotar a que hora y con que cancion para relacionarlo con lo que se oyo.

## Archivos tocados

`NivelDisco.cs`, `SalaLocal.cs`, `PresenciaSala.cs` (nuevo), `Form1.Ajustes.cs`, `Form1.Datos.cs`, `Form1.Sonido.cs`, `Form1.SinPausa.cs`, `Form1.Video.cs`, `Form1.Portadas.cs`, `GraveMono.cs`, `LoQueSuena.cs`, `SalidaSonido.cs`, `BombaSonido.cs`, `RellenoMp3.cs`, `PicoReal.cs`, `Rocola.csproj`;
pruebas `discoparejo`, `salalocal`, `presenciasala` (nueva), `adelantoprioridad` (nueva), `picomuestras` (nueva), `gravemono`, `ventanaf3`, `bomba`, `latenciaadaptable`, `decimatanda`, `relleno`, `todo.sh` y `LEEME.md`.
