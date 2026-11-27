# La cadena de sonido nueva: el marco, la medida nueva y la cadena normal (A)

Parte de la cadena nueva aprobada por el dueno el 27-11-2026 (`CADENA_NUEVA.md` §1, §2, §5 y §8; las piezas de la B, `CADENA_NUEVA_B.md`).
Solo Rocola OS (`rocola-xp` con `ROCOLA_OS`); sin el, o con la cadena nueva sin elegir, la de hoy tal cual, bit a bit. Nada oido ni probado en
el A4: lo de abajo son cuentas con senales inventadas y la rocola de verdad fuera de pantalla con la tarjeta de prueba con reloj.

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

## Los ganchos (g460-g462, `RocolaOs/GANCHOS.tsv`)

| | | |
|---|---|---|
| g460 | `Codigo/Form1.Sonido.cs` `ArmaCadena` | con la nueva, la cadena de la cancion la arma `RocolaOsCadenaNueva` (el arranque de siempre y el relevo sin pausa); null y la de XP si no, o si algo falla al armarla |
| g461 | `Codigo/Form1.SinPausa.cs` `SigueA` | el disco continuo entre dos cadenas nuevas lo hace el marco (cada pieza con la de su tipo y orden, al PREPARAR el relevo); entre una nueva y una de XP nada se hereda |
| g462 | `Codigo/Form1.Sonido.cs` `OpenAudio` | con la nueva, un MP3 se abre en coma flotante y sin huecos (lo de abajo), con saltar los silencios de XP por fuera |

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
  Si pide un arreglo, otra pasada con el arreglo puesto: lo guardado es lo que sale tal como va a sonar.
- **La tabla** `rocola_medida_nueva.txt` (datos de rocola-xp; nunca las de XP): cabecera `ROCOLA-MEDIDA-NUEVA v1` y un renglon por cancion y
  cadena con el sello del archivo (largo, creada, escrita, como `rocola_fuentes.txt`), algoritmo, huella de la B, segundos, LUFS, dBTP, pico,
  LRA, arreglos, zumbido y los tercios (19 campos, solo la ruta y numeros). Se anaden renglones; al leerla se compacta si sobra un tercio.
- **Quien mide**: (1) LA COLA: cada segundo el latido mira lo que suena y la cola; una tuya sin medida valida se mide YA en el hilo
  `medida_nueva-cola` (por debajo de lo normal de XP: debajo de la musica y la pantalla), por delante del repaso; (2) EL REPASO DE FONDO, hilo
  `medida_nueva-fondo` a nice 19, la biblioteca entera de la mas pedida a la menos (`songCounts`, y a igualdad la mas reciente), 50 ms entre una
  y otra; (3) EN VIVO, lo que no se pudo medir antes (Spotify, o una que entro sin tiempo), si suena entera (97 % y 30 s o mas, desde el
  principio); si pide un arreglo se guardan solo los arreglos (algoritmo 0) y se vuelve a medir con ellos.
- **Cuanto cuesta** (aqui, maquina compartida): 90-220 veces el tiempo real en JIT y unas 150 en NativeAOT (unos 1,6 s una cancion de 4 min);
  decodificar en coma flotante, 1,2 ms por segundo de musica.

## La ganancia (`RocolaOs.CadenaNuevaGanancia.cs`)

`g = min(T - L', C - TP')`, fija toda la cancion; L' y TP' = lo medido mas lo que le hara lo que va despues sin medirse: el **EQ del dueno**
de esa memoria (L': su curva sobre el espectro de tercios con la ponderacion K; TP': lo maximo que sube la curva), el **tono parejo** igual y
medio dB de pico con **agudos_mp3**. Las que no caben **quedan mas bajas** (no hay limitador). Entre -40 y +20 dB.
- **Discos**: la racha de XP (`RachaDeDisco`) con sus reglas (2 pistas medidas o mas, la mitad del disco y la propia; no en recopilatorios de
  mas de 5 dB, contando solo las de 30 s o mas): L del disco = media de energia por lo que dura cada una; TP = el mayor.
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

## Lo no comprobado y lo que queda

- Nada en el A4 ni oido; ni con musica de verdad, ni con Spotify ni Soloist reales.
- El DSEE de `agudos_mp3` es el de hoy con su cantidad de fabrica (la banda recreada entera); su corte de agudos se sigue alisado por ventana
  como hoy (no es «un numero por cancion»). Decide la prueba ciega.
- El empalme de los graves al centro en un disco continuo deja pasar algo del grave del lado de la pista que acaba en sus ultimos 15 ms (como
  `GraveMono` hoy), sin escalon (-33 dB del nivel con ruido rosa estereo, el peor caso).
- Si la siguiente la abrio el calentador ANTES de elegir la nueva, suena por la nueva con el lector de XP (16 bits) y no se mide en vivo.
- Cuando XP convierte de frecuencia en un relevo (una de 48 kHz detras de una de 44,1) o con `frecuencia_salida`, XP pone detras su conversion
  con su limitador: eso es de la salida (el otro agente: la salida nueva a 44,1 kHz fijos con el sinc propio).
- VBRI no dice el relleno: el final de esas canciones se queda como viene.
- R1 sigue siendo solo de MPEG-1 (su lector).
