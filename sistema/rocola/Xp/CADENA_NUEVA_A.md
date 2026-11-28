# La cadena de sonido nueva: el marco, la medida nueva y la cadena normal (A)

Parte de la cadena nueva aprobada por el dueno el 27-11-2026 (`CADENA_NUEVA.md` §1, §2, §5 y §8; las piezas de la B, `CADENA_NUEVA_B.md`).
Solo Rocola OS (`rocola-xp` con `ROCOLA_OS`); sin el, o con la cadena nueva sin elegir, la de hoy tal cual, bit a bit. Nada oido ni probado en
el A4: lo de abajo son cuentas con senales inventadas y la rocola de verdad fuera de pantalla con la tarjeta de prueba con reloj.

**28-11-2026 (fase 1 de «La cadena vieja borrada», CADENA_NUEVA.md): la cadena nueva va SIEMPRE con ROCOLA_OS=1 (`cadena_nueva` se ignora; la
hoja Cadena ya no elige) y la cadena de XP se borra en la fase 2. Lo de abajo sobre elegirla, «la de hoy» y «apagada de fabrica» es de antes.**

## Que es y como se elige

- **`cadena_nueva`** (funcion de Rocola OS, APAGADA de fabrica): cual suena. Se elige en **Ctrl+F4 -> Cadena** («La de hoy» / «La nueva») y
  vale **desde la siguiente cancion**, sin reiniciar: la clave se lee en vivo al armar cada cancion (con `ROCOLA_OS` de lista, lo que diga la
  lista). Con el modo billar GUARDADO (`local_auto=1`, `LocalAutoAGuardar`) la nueva es la **B**; si no, la **A**. Sus claves, desde la
  siguiente cancion: `nivel_lufs=` (T, -18, de -30 a -6), `techo_dbtp=` (C, -1, de -20 a 0), `agudos_mp3=` (0) y `tono_parejo=` (0).
- **`medida_nueva`** (ENCENDIDA de fabrica: solo mide, y SOLO si la cadena nueva suena o alguien la pide con `RocolaOsMedidaTrabajo.Pedida`,
  p. ej. la comparacion ciega): la tabla, la medida al entrar a la cola y el repaso de fondo.
- **La hoja Cadena** (Ctrl+F4, entre Spotify y Archivos): cual suena; lo que suena ahora (cadena, ganancia fija y de donde salio, lo medido,
  los arreglos, como se abrio); el nivel y el techo de uno en uno; los dos opcionales; como va la medida y la DISPERSION de la sonoridad de las
  de Spotify medidas (si es menor de 1 LU, Soloist ya las entrega normalizadas). Las pruebas de la pantalla (Tab desde la primera pestana) llevan
  un Tab mas: `Pruebas/RocolaOs/pantalla/prueba.sh` y `Pruebas/Qemu/pantalla.py`.

## Los ganchos (g460-g467, `RocolaOs/GANCHOS.tsv`)

| | | |
|---|---|---|
| g460 | `Codigo/Form1.Sonido.cs` `ArmaCadena` | con la nueva, la cadena de la cancion la arma `RocolaOsCadenaNueva` (el arranque de siempre y el relevo sin pausa); si algo falla al armarla, el respaldo minimo (a6, abajo), nunca la de XP; null y la de XP sin la nueva |
| g461 | `Codigo/Form1.SinPausa.cs` `SigueA` | el disco continuo entre dos cadenas nuevas lo hace el marco (cada pieza con la de su tipo y orden, al PREPARAR el relevo); entre una nueva y una de XP nada se hereda |
| g462 | `Codigo/Form1.Sonido.cs` `OpenAudio` | con la nueva, un MP3 se abre en coma flotante y sin huecos (lo de abajo), con saltar los silencios de XP por fuera |
| g463 | `Codigo/SinSilencios.cs` `SiguienteConElInterruptor` | la preparada que se retira sin empezar: la que suena ya no lee su principio (el disco continuo exacto del billar) |
| g464 | `Codigo/Form1.SinPausa.cs` `ConvierteSalida` | (a5, 28-11-2026) con una cadena nueva y la salida de hoy, la conversion de frecuencia va sin el limitador de pico real de XP detras (con la salida nueva XP ya no convierte: g441, g445) |
| g465 | `Codigo/Form1.Sonido.cs` `AFrecuenciaDeLaTarjeta` | (a5) una cadena nueva convertida sin limitador no deja cebado (con XP siempre hay limitador: lo de XP) |
| g466 | `Codigo/Form1.Sonido.cs` `PublicaNoche` | (a8) el reloj de la pantalla publica tambien el volumen de la noche (`RocolaOsNocheAhora`) para el hilo del sonido |
| g467 | `Codigo/Form1.Sonido.cs` `SpectralTap.PreparaCorteGrupo` | (a7) el DSEE de `agudos_mp3` recrea desde el corte FIJO medido de la cancion; los de XP no tienen corte fijo: lo de XP |

`ganchos.py`: TODO BIEN (112 archivos con la base 8c8cba0).

## Todo el MP3 (`RocolaOs.CadenaNuevaMp3.cs`)

- **libmpg123 en coma flotante** (`MPG123_ENC_FLOAT_32` y `FORCE_FLOAT`, modo «alimentar» con los cuadros del lector de XP: el mismo indice y
  los mismos saltos); se prueba una vez con el MP3 de medir de `RellenoMp3` que de verdad da coma flotante; si no (o `ROCOLA_SIN_MPG123=1`),
  **NLayer en coma flotante** (`MotorNLayer`). Ni recorte ni redondeo: un master aplastado llega con sus picos de +3,1 a +3,6 dBFS (737 a 3925
  muestras sobre 0 dBFS en 30 s que hoy salen al tope de 16 bits). Desde la memoria del calentador como XP (`CancionEnMemoria`), y con el lector
  ligero de XP. El primer cuadro, si es una cabecera (Xing, Info o VBRI), no se le da al descompresor.
- **Sin huecos con las tres marcas**, en MPEG-1, 2 y 2.5: la etiqueta **LAME** (Xing/Info con su CRC), el **iTunSMPB** de iTunes (un COMM de la
  ID3v2.2/2.3/2.4) y la cabecera **VBRI** de Fraunhofer (dice el retraso, no el relleno: se quita el principio y el final queda como viene). El
  retraso del descompresor se MIDE con este mismo camino (529 muestras con libmpg123 y con NLayer). Se recorta con la `FuenteSinRelleno` de XP,
  asi que saltar los silencios largos, el fundido y la duracion siguen como hoy. Solo si cuadra con el archivo (dos cuadros de margen).
- **R1** (relleno de huecos, con `agudos_mp3`): el `RocolaOsRellenoHuecos` de hoy envuelve al descompresor y ya suma en coma flotante.

## La medida nueva (`RocolaOs.MedidaNueva.cs`, `RocolaOs.Medida.cs`, `RocolaOs.MedidaTabla.cs`)

- **Que**: la cancion ENTERA decodificada en coma flotante y pasada por la cadena (la A, o la B con sus piezas, `Midiendo`) a GANANCIA 1 y sin el
  ecualizador del dueno: sonoridad integrada **BS.1770-4** con sus puertas (LUFS; los coeficientes K de `MedidorBs1770`), **pico real** con
  sobremuestreo 4x (FIR de fase lineal, Kaiser beta 6, 20 coeficientes por fase, plano hasta 17,6 kHz, en vectores; un trozo que no puede pasar
  del pico ya medido no se calcula), **LRA** (EBU 3342), pico de muestra, el **espectro en 30 tercios** (para estimar lo que hace el EQ del
  dueno y el tono parejo) y, de lo que sale del decodificador, los **arreglos fijos**: canal mudo (30 dB o mas bajo que la cancion), canal
  invertido (correlacion del grave bajo 150 Hz de -0,6 o menos, con grave), continua (desde -50 dBFS) y el zumbido que midio el repaso de XP (T6).
  Si pide un arreglo, otra pasada con el arreglo puesto: lo guardado es lo que sale tal como va a sonar. Y (28-11-2026) los **picos de las
  sondas del EQ del dueno** (por cada una de sus diez bandas, el pico de muestra de lo que esa banda le suma a la musica al subirla, por dB: a1)
  y el **corte de agudos** de la cancion (en cada FFT con musica el acantilado como lo busca el DSEE de XP; el de la cancion, el percentil 90: a7).
- **La tabla** `rocola_medida_nueva.txt` (datos de rocola-xp; nunca las de XP): cabecera `ROCOLA-MEDIDA-NUEVA v1` y un renglon por cancion y
  cadena con el sello del archivo (largo, creada, escrita, como `rocola_fuentes.txt`), algoritmo, huella de la B, segundos, LUFS, dBTP, pico,
  LRA, arreglos, zumbido, los tercios, la hora y (desde el 28-11-2026) los picos de las sondas del EQ y el corte (21 campos, solo la ruta y
  numeros; un renglon de antes, de 19, se lee sin ellos). Se anaden renglones; al leerla se compacta si sobra un tercio.
- **Quien mide**: (1) LA COLA: cada segundo el latido mira lo que suena y la cola; una tuya sin medida valida se mide YA en el hilo
  `medida_nueva-cola` (por debajo de lo normal de XP: debajo de la musica y la pantalla), por delante del repaso; (2) EL REPASO DE FONDO, hilo
  `medida_nueva-fondo` a nice 19, la biblioteca entera de la mas pedida a la menos (`songCounts`, y a igualdad la mas reciente), 50 ms entre una
  y otra; (3) EN VIVO, lo que no se pudo medir antes (Spotify, o una que entro sin tiempo), si suena entera (97 % y 30 s o mas, desde el
  principio); si pide un arreglo se guardan solo los arreglos (algoritmo 0) y se vuelve a medir con ellos.
- **Cuanto cuesta** (aqui, maquina compartida): 90-220 veces el tiempo real en JIT y unas 150 en NativeAOT (unos 1,6 s una cancion de 4 min);
  decodificar en coma flotante, 1,2 ms por segundo de musica. Las diez sondas del 28-11-2026 le suman un 25 % mas o menos (JIT, con la maquina
  cargada: 60-77 veces el tiempo real frente a 66-96 del programa de antes, en la misma vuelta).

## La ganancia (`RocolaOs.CadenaNuevaGanancia.cs`)

`g = min(T - L', C - TP')`, fija toda la cancion; L' y TP' = lo medido mas lo que le hara lo que va despues sin medirse: el **EQ del dueno**
de esa memoria (L': su curva sobre el espectro de tercios con la ponderacion K, y sin espectro el tipico de la biblioteca; TP': lo que sube de
verdad su pico, con los picos de sus sondas: abajo, a1), el **tono parejo** (lo maximo de su curva) y medio dB de pico con **agudos_mp3**. Las
que no caben **quedan mas bajas** (no hay limitador). Entre -40 y +20 dB. Si el dueno mueve el EQ mientras suena, esa cancion no pasa el techo
(el tope, a1).
- **Discos**: la racha de XP (`RachaDeDisco`) con sus reglas (2 pistas medidas o mas, la mitad del disco y la propia; no en recopilatorios de
  mas de 5 dB, contando solo las de 30 s o mas): L del disco = media de energia por lo que dura cada una; TP = el mayor. El MODO lo decide la
  primera pista de la racha y las demas lo siguen (b10); la que entra SIN PAUSA detras de la anterior del mismo disco lleva su misma ganancia
  mientras quepa en su techo (a3).
- **Sin medir a tiempo**: estimacion CONSERVADORA de las tablas viejas de XP: su integrada (LKFS) y su pico + 3 dB; sin integrada, su K-RMS y
  +3 dBTP; sin nada, -8 LUFS y +3 dBTP.
- **Spotify** (`spotify:track:`): la medida de la cadena nueva (en vivo, la vez que sono entera) o, la primera vez, **FIJA ESTIMADA**: la mediana
  de las de Spotify medidas por la cadena nueva (L' y TP' + 1,5 dB) con 5 o mas; si no, las de `rocola_spotify_niveles.txt` de XP (LKFS y su
  pico de muestra + 2 dB); sin nada, -8 LUFS y +1 dBTP. El **nivelado en vivo de 30 s** (`spotify_nivela`) NO se aplica en la nueva (su medida
  para las tablas de XP sigue). La **sustitucion invisible**: la medida de la version de Spotify o, sin ella, la de tu MP3.
- Nada mas mueve el nivel: ni limitador, ni preganancias, ni E3, ni el parejo del billar, ni el «al limite». Ctrl+F3 no la toca (la cadena nueva
  no tiene `LevelTap`; `curTap` es null).

## La cadena A y el marco (`RocolaOs.CadenaNueva.cs`, `RocolaOs.CadenaNuevaPiezas.cs`, el contrato en `RocolaOs.CadenaNuevaPieza.cs`)

`OpenAudio` (g462) -> **la entrada** (el pulso de las estrellas, `ReportAudioBlock`, y lo crudo si se mide en vivo) -> **el zumbido**
(`QuitaZumbido` de XP si se midio) -> **los arreglos fijos** (continua, invertido, mudo) -> [`agudos_mp3`: el **DSEE** de hoy (`SpectralTap`,
cantidad de fabrica: la banda recreada entera; su corte sigue a la cancion como hoy) y R1 en el descompresor] -> [`tono_parejo`: **8 campanas
de una octava cuyas ganancias se RESUELVEN para dar exactamente la curva pedida en el centro de cada banda** (sin el solape mal contado de hoy),
de la huella de XP frente a su objetivo, a la mitad y de +-3 dB, nada sobre el corte de la cancion] -> **graves al centro bajo 120 Hz** (el
lado pierde su grave con un FIR de fase lineal por FFT que lee por delante; sin S3, estrecho ni la vuelta dinamica de `GraveMono`; un estereo
con los dos canales iguales sale bit a bit) -> [la medida en vivo] -> **el EQ del dueno** (sus 10 bandas, memoria normal; en vivo con su rampa y
«Sin ecualizador»; SIN preganancia) -> **LA GANANCIA x el factor del modo noche** (el reloj: `FactorNoche`, alisado 20 ms) -> **el corte del grave
de noche** (`GraveMono.DeNoche`, solo la A) y **los graves de noche** (`SalaLocal` con todo lo demas apagado) -> **el fundido de XP** (el
`fundido=` del dueno y la entrada de 5 ms) -> la salida de siempre (o la nueva del otro agente, que se engancha despues). Retardo 0.

**La B** (el marco): lo mismo hasta los arreglos -> `RocolaOsCadenaB.Arma` (el tramo del billar: mono, paso alto con el corte de noche dentro,
curva, bajo virtual; mientras no este, de paso) -> el EQ del dueno con la memoria del billar -> la ganancia (medida DESPUES del tramo) -> los
graves de noche y el factor (su corte de 80 Hz NO: va dentro de su paso alto) -> el fundido -> la salida. El disco continuo: g461 llama a `Sigue`
al preparar el relevo (lo que la B usa para su modo exacto).

Lo de XP que se conserva a proposito: saltar los silencios largos y el fundido (son del dueno, no de la senal). Lo que NO se aplica: el balance
del local, «Escuchar sin mejoras», el aprendizaje de las tablas de XP mientras suena la nueva (las mide el repaso de XP como siempre).

## Pruebas (`Pruebas/RocolaOs/cadena-nueva/prueba.sh`; la orden `--prueba-cadena-nueva`, `RocolaOs.CadenaNuevaPrueba.cs`)

Con senales inventadas (`senales.py`), MP3 de LAME hechos con ffmpeg, iTunSMPB y VBRI armados con `marcas.py`, y la rocola fuera de pantalla
con la tarjeta con reloj; lo que sono se mide con el **ebur128 de ffmpeg** (otra implementacion de BS.1770) y `analiza.py`.

| parte | resultado |
|---|---|
| decodifica | coma flotante: +3,1/+3,4 dBFS sin recortar (hoy 737/3928 muestras al tope); sin pasar de 0 dBFS, solo el redondeo de 16 bits (-82 dB) |
| sinhuecos | LAME CBR (Info) y VBR (Xing), MPEG-2 y 2.5, iTunSMPB (MPEG-1 y 2): desfase 0 y el largo exacto; VBRI: desfase 0 y el final con el relleno menos 529 |
| medidor | seno de 997 Hz a -20 dBFS: -20,00 LUFS; seno en fs/4 a 45 grados: pico real -6,01 (debe -6,02), de muestra -9,03; 11 kHz a -1 dBFS: -0,99; ruido rosa a 44,1 y 48 kHz: igual que `MedidorBs1770` de XP (0,000 LU) y el mismo LRA |
| mide | lo que sale, medidor contra ffmpeg: a 0,05 LU y 0,05 dB de pico real |
| ganancia | fuerte (-9,1 LUFS) y media (-16,5) salen a -18,00; la de mucho rango no cabe y queda a -24,05 LUFS con -1,00 dBTP; ninguna pasa el techo |
| constante | ruido rosa con escalones de +-10 dB: la nueva 0,000 dB de vaiven por bloque de 10 ms; el limitador de hoy, 6,3 dB |
| disco | dos pistas de un disco continuo: sin hueco (el largo exacto), identico fuera de la union, en la union solo el empalme del grave del lado (-33 dB del nivel) |
| arreglos | invertido (sale con correlacion del grave 0,99), mudo (copiado: los dos canales iguales), continua (+0,0215/-0,0106 detectada, sale 0,0000) |
| costo | la cadena nueva 2,97-3,03 ms por segundo de musica en NativeAOT frente a 5,1-6,4 de lo de hoy que se arma sin la rocola (que es menos que la de hoy entera: le faltan el realce, el EQ automatico y la medida de XP); con decodificar, 4,2 frente a 6,5-7,8 |
| escena (A) | la rocola de verdad: las cuatro medidas por el repaso antes de sonar, abiertas en coma flotante y sin huecos, las dos del disco con su ganancia unica; el disco y la fuerte a -18,0 LUFS (ffmpeg) sin pasar -1 dBTP; el ruido de los escalones con 0,002 dB de vaiven a la salida de 16 bits (0,000 entre escalones); la hoja Cadena |
| billar (B) | la misma con el modo billar guardado y las piezas del billar (juntadas): las cuatro medidas con la B y la huella de sus piezas; el disco y la fuerte a -18,0 LUFS despues del tramo (ffmpeg), pico real de -4,8 a -6,1 dBTP; el MISMO trozo de ruido a 0, +10 y -10 dB sale con la misma ganancia en cada escalon (medianas a 0,025 dB; cada bloque de 10 ms varia hasta 4 dB por lo que la curva le hace a su tono) |
| hoy | la misma con la de hoy: ni tabla ni cadena A; el ruido de los escalones con 2,5 dB de vaiven |
| sinros | sin `ROCOLA_OS`: ni tabla ni bitacora |
| spotify | con el Soloist falso: la 1.a vez por la A con la ganancia fija estimada (25 medidas de XP) y SIN el nivelado en vivo (0,02 dB de salto); medida en vivo al sonar entera; la 2.a, con su medida y sin moverse |

Tambien: NLayer en coma flotante (`ROCOLA_SIN_MPG123=1`): decodifica y sin huecos igual. NativeAOT publicado sin avisos nuevos; `dotnet build`
con los 13 avisos de siempre. La prueba entera: **70 de 70 en NativeAOT** (`aot.log`, 27-11-2026). Lo de antes con la nueva APAGADA: `andamio`
101 de 101 (JIT), `pantalla` 59 bien y los 2 «NO» de siempre de esta maquina (sin el BusyBox de QEMU ni la referencia con trazas; el teclado,
con el Tab de mas), `billar-nuevo` 86 de 86 (NativeAOT; la hoja con el candado de lo pesado, y su comprobacion de la pestana de (818,54) ya con
la Cadena del marco). Con otros agentes trabajando conviene correr las escenas con el candado (`flock -o /root/rocola-trabajo/fase11/pesado.lock`).

## Arreglos del 28-11-2026 (la revision de la cadena nueva)

Un revisor encontro siete fallos (sus cuentas, en numpy, sobre copias de los disenos). Cada uno con su prueba en `prueba.sh` (partes nuevas
`eqdueno eqvivo convierte dsee noche relevo respaldo opciones`). Sin `ROCOLA_OS` o sin la cadena nueva, nada cambia: los ganchos nuevos (g464-g467)
devuelven lo de XP.

- **a1. El EQ del dueno ya no baja de mas a las dinamicas** (`RocolaOs.CadenaNuevaPicos.cs`). Antes TP' = TP + lo maximo que sube la curva: con
  +6 dB en 63 Hz se suponian 6 dB de pico en un bolero al que de verdad le suben 0,15 (y la ganancia quedaba 5,3 dB mas baja) y en un master
  aplastado; y lo maximo tampoco era una cota (con +6 en 63 y 125, la curva sube 7,2 y el pico de la aplastada 8,3). Ahora la MEDIDA guarda, por
  cada banda del EQ, el pico de lo que esa banda le suma a la musica al subirla (su SONDA: el limite de (H - 1)/(G - 1), la mas ancha) y TP' = TP
  + lo menor de tres cotas: la de las sondas (desigualdad del triangulo: TP + suma (G_b - 1) P_b), la de la respuesta al impulso de la curva
  (suma |h|, rigurosa) y lo maximo de la curva con 1,5 dB de margen (para las curvas anchas, donde las otras dos se pasan). Sin la medida
  (Spotify la 1.a vez, sin medir, medida de antes) los picos de sondas TIPICOS de la biblioteca (la mediana, relativa al pico real) y sin nada,
  la de la respuesta al impulso; la sonoridad sin espectro, con el espectro tipico (la mediana de los tercios) o el de un ruido rosa, nunca lo
  maximo. Si el dueno mueve el EQ mientras suena, la pieza del EQ vuelve a estimar con lo de esa cancion (con lo mas de lo que va y lo pedido
  mientras la rampa del EQ corre, y otra vez al asentarse) y, si pasaria el techo, la ganancia baja (`TopeLin`, rampa de 20 ms; nunca sube de su
  ganancia fija). Prueba `eqdueno` (las dos de la revision, siete curvas): en las 14 lo estimado no se queda corto y nada pasa -1 dBTP; el
  bolero con +6 en 63 Hz sale a -1,52 dBTP y -23,2 LUFS (antes -6,85 y -28,5), con +6 en 63 y 125 a -1,97 (antes -7,88); sin espectro, +6 en
  63 Hz sube la sonoridad 0,45 LU (antes 6). Lo unico peor que antes: una curva ancha (todo +6) se supone 1,5 dB mas alta que su maximo (el bolero
  sale a -5,2 dBTP en vez de -3,7; la aplastada sube de verdad 0,2 dB mas que el maximo). Prueba `eqvivo` (el bolero, +12 dB en 2, 4 y 8 kHz a
  media cancion): con el tope la segunda mitad queda en -4,7 dBTP (sin el, +8,5) y la primera igual.
- **a3. El disco continuo con la misma ganancia** (`RocolaOsGananciaNueva`): la que entra SIN PAUSA detras de la anterior del mismo disco (el
  relevo con `SiguienteDelDisco`) lleva la ganancia que lleva la que suena (la del disco si la racha va por disco; si no, la de la pista
  anterior), bajada solo si no cabe en su techo; la regla de los recopilatorios (mas de 5 dB) no parte un disco continuo. Escena `relevo` (la
  rocola de verdad con la tarjeta con reloj): un recopilatorio de dos pistas seguidas de 32 s con 7 dB entre ellas (empieza cancion por cancion:
  la primera a -18,0 LUFS): la segunda entra sin pausa con la ganancia de la primera (-8,9 y -8,9 dB) y suena a -25,7 LUFS, lo que le toca en
  el disco; con el programa de antes, -8,9 y -1,3 dB: un escalon de 7,6 dB en la union.
- **a5. Sin el limitador de XP al convertir de frecuencia con la salida de hoy** (g464 en `ConvierteSalida`, g465 en `AFrecuenciaDeLaTarjeta`):
  con una cadena nueva, el convertidor solo (sin limitador, sin cebado), y en el disco continuo el convertidor sigue al de la anterior. Con la
  salida nueva XP ya no convierte (g441, g445; sus lineas no se tocan): esto queda para la salida de hoy, el respaldo de emergencia. Prueba
  `convierte`: un seno a -0,45 dBFS sale a -0,45 (con una de XP, su limitador lo deja en -1,12). Escena `opciones` (`frecuencia_salida=48000`):
  las cuatro convertidas a 48 kHz sin el limitador (vivo.txt `convertidas_sin_limitador=4`).
- **a6. El respaldo que respeta el nivel** (`RocolaOsArmaMinima`): si la nueva falla al armarse (o la fuente no es de dos canales), ya no suena la
  de XP (de 4 a 9 dB mas fuerte que -18 LUFS): la NUEVA MINIMA, leer -> el pulso -> la ganancia fija (lo medido sin lo de despues, Spotify la
  1.a vez o las tablas viejas; si ni eso, la de -8 LUFS y +3 dBTP) con la noche -> el fundido de XP, cada paso con su red; el fallo va a
  `rocola_error.txt` y la bitacora dice «RESPALDO MINIMO»; vivo.txt cuenta `respaldos=`. Es el UNICO respaldo (decision del dueno del
  28-11-2026: en Rocola OS la cadena nueva es la unica). Escena `respaldo` con `ROCOLA_CN_FALLA=armar` (falla a proposito al armar cada una): las
  cuatro por el respaldo, el disco y la fuerte a -18,0 LUFS (ffmpeg) sin pasar -1 dBTP; antes habrian sonado por la de XP, a su nivel de hoy
  (la escena `hoy`, lo mismo con la de XP: el disco a -15,0 LUFS y la fuerte a -10,5).
- **a7. El DSEE de `agudos_mp3`, fuera del alcance de XP y con su corte fijo**: va en la armada (no en `CadenaCancion.Sbr`, que XP enciende y
  apaga: `ApplyNormNow`, «Escuchar sin mejoras»); su corte es el de la cancion (el de la medida nueva; sin el, el de su ficha de XP; sin nadie
  que lo midiera, o completa, sin DSEE) y no el que sigue a la musica ventana a ventana (g467). Prueba `dsee` (ruido con agudos hasta 16 kHz la
  mitad y apagado hasta 11 kHz la otra): la medida da 16 100 Hz; con el corte fijo, en la parte apagada no se recrea nada entre 11,5 y 15,5 kHz
  (-122 dB, lo de la entrada) y siguiendo a la musica se recreaba a -28 dB de lo de 4-8 kHz; donde hay agudos, lo mismo (-11,6 y -11,6).
  Escena `opciones` (`agudos_mp3=1`): la fuerte de LAME 128 con el corte fijo medido de 16 700 Hz, las del disco con 18 800, el ruido completo sin
  DSEE; vivo.txt `dsee=1 dsee_corte=16700 dsee_xp=0` (XP no lo ve).
- **a8. La noche sin `FactorNoche` en el hilo del sonido**: el reloj de la pantalla la publica en cada vuelta (`PublicaNoche`, g466) y la cadena
  al armarse; la pieza de la ganancia solo lee ese `volatile`. La rampa de una hora es la de XP, publicada tal cual. Prueba `noche`: lo publicado
  (1 y 0,5) es lo que aplica; escena `opciones` de noche desde el arranque (80 %): vivo.txt `noche=0.6903` y el disco y la fuerte a -21,2 LUFS
  (-18 - 3,22). (El fallo era una carrera entre hilos, no un nivel distinto: con el programa de antes la escena suena igual.)
- **b10. El modo de la racha lo decide su primera pista**: la carpeta y el modo (por disco o cancion por cancion) de la racha que va; las
  siguientes de la misma carpeta, detras de una de ella, lo siguen (antes, con la 2 pedida cuando ya sonaba la 1, la 1 iba por pista y la 2 por
  disco). Escena `relevo`: la 1 de un disco pedida sola (cancion por cancion) y la 3 pedida mientras suena la 1: la 3 va por su medida, -18,0
  LUFS como la 1; con el programa de antes, por la del disco (-20,2 LUFS).

Baterias (28-11-2026, NativeAOT publicado sin avisos IL/AOT; `dotnet build` con los 13 avisos de siempre; `ganchos.py` TODO BIEN): `cadena-nueva`
**94 de 94** (las 70 de antes con la tabla de 21 campos y las 24 nuevas), `billar-nuevo` **86 de 86**, `andamio` **101 de 101**. Las escenas
nuevas con el programa de ANTES (el mismo guion): `relevo` 4 MAL de 5 (el escalon de 7,6 dB y la 3 por la del disco) y `opciones` 3 MAL de 5
(lo que cuenta vivo.txt: sin convertir sin limitador, sin la noche publicada, el DSEE visible para XP); las partes de cuentas nuevas no existian.
La medida con las sondas cuesta un 25 % mas (88 veces el tiempo real en NativeAOT con la maquina cargada).

## Lo no comprobado y lo que queda

- Nada en el A4 ni oido; ni con musica de verdad, ni con Spotify ni Soloist reales.
- El DSEE de `agudos_mp3` es el de hoy con su cantidad de fabrica (la banda recreada entera); desde el 28-11-2026 su corte es FIJO por cancion
  (el medido, a7), pero la banda recreada sigue a la musica ventana a ventana (es lo que hace un DSEE: se empata con lo que suena bajo el corte).
- El empalme de los graves al centro en un disco continuo deja pasar algo del grave del lado de la pista que acaba en sus ultimos 15 ms (como
  `GraveMono` hoy), sin escalon (-33 dB del nivel con ruido rosa estereo, el peor caso).
- Si la siguiente la abrio el calentador ANTES de elegir la nueva, suena por la nueva con el lector de XP (16 bits) y no se mide en vivo.
- Cuando XP convierte de frecuencia en un relevo (una de 48 kHz detras de una de 44,1) o con `frecuencia_salida` y la salida de hoy (con la
  salida nueva, solo de emergencia si no pudo abrir la tarjeta), desde el 28-11-2026 convierte sin su limitador (a5).
- VBRI no dice el relleno: el final de esas canciones se queda como viene.
- R1 sigue siendo solo de MPEG-1 (su lector).
