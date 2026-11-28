# La cadena de audio nueva (aprobada el 27-11-2026)

Peticion del dueno del 27-11-2026: «vamos a disenar una nueva cadena de audio, una nueva cadena de audio para el modo billar, y si al final vemos que
genuinamente son mejores estas nuevas que haremos, estas reemplazaran a las actuales. Las nuevas tendras como meta exprimir al maximo el formato mp3,
que suenen lo mejor posible y con volumen constante, no quiero ninguna tecnica de procesamiento si esta hace que el volumen fluctue en tiempo real».
Plan presentado (artefacto 9s1k4u7xzzC7tsdLdpb1nj) y aprobado el mismo dia: «Haz todo lo que me recomiendas en la cadena de sonido».

Reglas de esta tarea: la hacen SOLO agentes Opus (sin Sonnet); solo Rocola OS (rocola-xp con ROCOLA_OS; sin el, XP tal cual); Codigo/ solo por
ganchos de una linea (RocolaOs/GANCHOS.tsv, `python3 Pruebas/RocolaOs/ganchos.py` TODO BIEN); las cadenas nuevas CONVIVEN con las actuales y solo
las reemplazan si ganan a ciegas al mismo volumen y pasan las medidas (el dueno decide).

Inventarios y la investigacion que sostienen esto: el plan (artefacto) y los resumenes de la sesion. Lo esencial de la cadena actual: el MP3 se
decodifica a 16 bits con recorte (Nativo/DescompresorMp3.cs, ENC_SIGNED_16); el volumen iguala PICOS (GananciaAlLimite, Sonoridad.cs) con un
limitador de pico real que trabaja (PicoReal.cs, techo 0,88); cuatro regimenes de nivelado; piezas que suben nivel sin compensar (EQ automatico,
realce, bajo psicoacustico); 8 piezas en 250-500 Hz; salida S16 por plughw a la frecuencia de cada cancion, cerrada y reabierta en Spotify, videos,
saltos y relevos fallidos.

## 1. Reglas de las dos cadenas nuevas

1. NADA MUEVE EL NIVEL MIENTRAS SUENA UNA CANCION. Una sola ganancia fija por cancion (o por disco), decidida antes de empezar. Sin compresor,
   limitador, AGC, expansor, ecualizador dinamico, detector de presencia ni envolventes que sigan a la musica. Lo unico que cambia en el tiempo es
   lo que pone el dueno o el reloj: su volumen (botones) y el modo noche (su rampa de una hora, como hoy).
2. VOLUMEN IGUAL POR SONORIDAD: g(dB) = min(T - L, C - TP), con L la sonoridad integrada (ITU-R BS.1770-4/5, con puertas, LUFS) y TP el pico real
   (sobremuestreo 4x o mas) MEDIDOS SOBRE LA SALIDA DE LA CADENA a ganancia 1. T = -18 LUFS (nivel_lufs=), C = -1 dBTP (techo_dbtp=; puede bajar si
   el convertidor recorta, ver §4). Las canciones que no caben se quedan mas bajas (sin recorte suave). Discos: si suenan seguidas pistas del mismo
   disco, una sola ganancia del disco (L del disco = media de energia de sus pistas; TP = el mayor) — como el «album gain» de ReplayGain.
3. TODO EL MP3: libmpg123 en coma flotante (ENC_FLOAT_32), sin redondear ni recortar en ningun punto antes de la salida; gapless con las tres
   marcas (LAME/Xing-Info, VBRI de Fraunhofer, iTunSMPB de iTunes) y tambien MPEG-2/2.5.
4. CADA PIEZA UNA VEZ: un solo filtro de graves, una sola curva de tono del lugar, el ecualizador del dueno (sin su preganancia: la cuenta la
   ganancia de la cancion), una sola ganancia.
5. LO DUDOSO SOLO SI GANA A CIEGAS: con interruptor propio, apagado de fabrica, hasta que la prueba ciega diga.

## 2. Cadena normal (A)

Leer el MP3 en float + gapless → mono a dos canales iguales → arreglos FIJOS decididos por la medida previa (zumbido: el QuitaZumbido de hoy si se
midio; canal mudo: copiar el que suena; canal invertido: voltearlo; corriente continua: restar la medida) → [opcional, ciego] agudos y huecos del
MP3 (DSEE con cantidad fija por cancion, R1) → [opcional, ciego] tono parejo por cancion (EQ automatico fijo, suave, ±3 dB, filtros mejores) →
graves al centro abajo de 120 Hz (el FIR de fase lineal de hoy, sin S3, estrecho ni polaridad dinamica) → el ecualizador del dueno (memoria normal)
→ GANANCIA DE LA CANCION (§1.2) × modo noche (FactorNoche y sus filtros de graves de hoy) → salida (§4).

## 3. Cadena del billar (B)

Igual que A en leer, arreglos, ganancia, noche y salida. En medio: MONO COMPLETO (L+R)/2 (con el canal invertido ya volteado) → UN paso alto en el
punto donde las bocinas dejan de sonar (70-90 Hz, medido; fase lineal como el subgrave de hoy) → CURVA DEL BILLAR fija (la afinacion con el
celular, mejorada para promediar varios puntos, + curva objetivo plana hasta 1 kHz y -0,5 a -1 dB/oct despues + «claridad de la voz» fija: un poco
menos en 250-400 Hz, un poco mas en 2-4 kHz; cortes de hasta -6/-9 dB, subidas de no mas de +3) → [opcional, ciego] BAJO VIRTUAL FIJO (no
linealidad homogenea, p. ej. |x| del grave filtrado, sin envolvente ni detector, mezclado de -6 a -10 dB) → el ecualizador del dueno (memoria del
billar) → ganancia medida DESPUES de todo lo anterior → noche → salida. Sin agudos inventados ni relleno de huecos.

## 4. Salida nueva (para las dos cadenas Y para la actual: decision D10)

Funcion «salida_nueva» de rocola_os.txt (apagada de fabrica hasta que el dueno la pruebe con su convertidor; con la prueba de «¿Se oye bien?» de
la hoja Sonido): el dispositivo por `hw:` (no plughw), elegido por NOMBRE/id de la tarjeta (el convertidor USB primero si esta; avisar si no esta);
24 bits (S32_LE o S24_3LE, lo que de la tarjeta), 44 100 Hz FIJOS; lo que no venga a 44,1 kHz se convierte dentro de la rocola con el sinc de
alta calidad que ya existe (SinPausa.cs RemuestreoContinuo); el flujo SIEMPRE ABIERTO (silencio entre canciones, con Spotify, con videos; 0,5 s de
ceros al abrir); el volumen del dueno DIGITAL en dB, en coma flotante, antes de cuantizar (botones de atras: 1 dB por toque; el control de la
tarjeta, si existe, una vez en 0 dB); una proteccion en 0 dBFS que no deberia actuar nunca y que, si actua, se cuenta y se apunta; cuantizacion
final una sola vez. Los VIDEOS (decision D11) salen por el mismo camino sin reabrir nada y con su ganancia fija medida al mismo nivel T (no por la
perilla de mpv). Spotify ya entra por la rocola (rocola-pulso → anillo).

Herramienta para MEDIR EL CONVERTIDOR (D12): tonos de 1 kHz a 0, -3, -6, -10 y -20 dBFS por la salida elegida, grabados por la entrada de linea de
la placa (un cable RCA a la entrada azul), con su distorsion (THD+N) y el nivel donde deja de recortar; propone el techo C. Medidas publicadas de
un PROZOR (GoldenSound): 5,4 % THD a 0 dBFS, recorta hasta unos -10 dBFS; lleva CS8416 + CS4344 (~1 Vrms).

## 5. Spotify y lo que no se puede medir antes

Spotify la primera vez (D4): ganancia FIJA estimada (de lo ya medido de Spotify: mediana de su L y TP con margen), sin el nivelado en vivo de 30 s;
desde la segunda vez, la medida exacta. Revisar si Soloist ya entrega las canciones normalizadas por Spotify (dispersion de las L medidas). Una
cancion tuya sin medir se mide al entrar a la cola (decodificar en float es ~500x tiempo real); si no da tiempo, una estimacion conservadora de las
tablas viejas, fija toda la cancion.

## 6. Comparar (decide el dueno)

Herramienta ciega en Ctrl+F4 → Sonido: alterna cadena actual / nueva por tramos sin decir cual, IGUALADAS EN NIVEL a 0,1 dB (sonoridad medida de la
salida); voto por tramo; modo ABX de 16 intentos (12 o mas = se oye). Medidas: dispersion de LUFS de salida en una lista (meta ≤ 0,5 LU), cero
picos sobre el techo, prueba de ganancia constante (ruido rosa con escalones de ±10 dB: la ganancia por bloque de 10 ms constante a ±0,05 dB), CPU
en el A4. Las nuevas reemplazan a las actuales solo si el dueno las prefiere a ciegas (en casa la A, en el billar la B) y pasan las medidas.

## 7. Decisiones del dueno (todas las recomendadas)

D1 T = -18 LUFS · D2 las que no caben quedan mas bajas · D3 una ganancia por disco en pistas seguidas · D4 Spotify 1.a vez fija estimada · D5 noche
igual (rampa de una hora) · D6 billar mono completo · D7 bajo virtual fijo: decide la prueba ciega · D8 tono parejo por cancion: decide la prueba
ciega · D9 agudos y huecos (A): decide la prueba ciega · D10 la salida nueva tambien para la cadena actual · D11 los videos por la rocola · D12 medir
el PROZOR primero · D13 botones de volumen a 1 dB por toque.

## 8. Reparto (agentes Opus) y rangos de ganchos

| Parte | Archivos nuevos (RocolaOs/) | Ganchos |
|---|---|---|
| Salida nueva, tarjeta USB por nombre, volumen digital, videos por la rocola, medir el convertidor | RocolaOs.SalidaNueva*.cs | g440-g459 |
| Medida nueva (float, BS.1770, TP 4x, arreglos fijos), ganancia, cadena A, Spotify 1.a vez, marco comun de las cadenas | RocolaOs.CadenaNueva*.cs, RocolaOs.Medida*.cs | g460-g479 |
| Piezas de la cadena B (mono, paso alto, curva del billar, bajo virtual, afinacion de varios puntos) | RocolaOs.Billar2*.cs | g480-g489 |
| Comparacion ciega, ABX y medidas (despues) | RocolaOs.Compara*.cs | g490-g499 |

### 8.1 El contrato de las piezas (marco comun; RocolaOs/RocolaOs.CadenaNuevaPieza.cs) — lo siguen las piezas de la B

- UNA PIEZA es un `ISampleProvider` de NAudio 3 (como las etapas de hoy: `Read(Span<float>)` con `Puente.Lee` y `Read(float[], int, int)`), en
  coma flotante, DOS canales, a la tasa de la cancion, armada con sus parametros FIJOS antes de sonar (nada que siga a la musica). SIN RETARDO:
  cada cuadro sale en su sitio (si necesita lo que viene, lee por delante, como el grave en mono de hoy). Y ademas `IRocolaOsPieza`:
  `void Sigue(IRocolaOsPieza anterior)` para el DISCO CONTINUO: la misma pieza (mismo tipo, mismo puesto en la receta) de la cancion que acaba
  entera; se llama en el hilo de la pantalla antes de la primera lectura y la pieza la toma en su primera lectura (hilo del sonido) solo si
  cuadra (tasa, canales, parametros), como `Sigue/Hereda` de `GraveMono` o `EcualizadorDueno`. `RocolaOsPiezaDePaso` es la identidad.
- LA RECETA `RocolaOsReceta`: `Cadena` ('A'/'B'), `Ruta`, `Tasa`, `Midiendo` (true en la medida: hilo de fondo, NADA del estado de XP),
  `Medida` (`RocolaOsMedidaNueva`: Lufs, TpDb, PicoDb, Lra, Tercios, arreglos), los arreglos fijos ya decididos, `GananciaDb`, y `Piezas`
  (cada pieza se anota en orden con `r.Anota(p)`).
- LA B: el agente del billar asigna, en el `[ModuleInitializer]` de su archivo, `RocolaOsCadenaB.Arma = (receta, entrada) => salida` y
  `RocolaOsCadenaB.Huella = () => "texto corto con sus parametros"`. `entrada` es lo que sale de los arreglos fijos (canal mudo copiado,
  invertido volteado, continua y zumbido quitados); `salida` va al ecualizador del dueno (memoria del billar), la ganancia, la noche y la salida,
  que pone el marco. Sus piezas: mono (L+R)/2, el paso alto, la curva del billar, el bajo virtual fijo... cada una `IRocolaOsPieza` y anotada.
  `Arma` corre en el hilo de la pantalla o en el de la medida (`r.Midiendo`): nada pesado y, midiendo, nada de XP (lo suyo, de una foto).
  Mientras `Arma` sea null la B lleva una pieza de paso. Si `Huella` cambia, lo medido con la B se vuelve a medir. La medida de la B (L, TP,
  tercios) la hace el marco pasando la cancion por `Arma` con `Midiendo = true`.

### 8.2 Las piezas de la B (agente del billar; detalle, numeros y pruebas en CADENA_NUEVA_B.md) — HECHAS en su rama, sin oir

- `RocolaOsCadenaB.Arma` y `.Huella` los pone `RocolaOs.Billar2.cs`. `Arma` devuelve UNA pieza (`RocolaOsBillar2Tramo`, anotada): mono
  (L+R)/2 (la ley de -6 dB: la ganancia se mide despues; no sube picos) -> paso alto Butterworth de 24 dB/oct en `billar_paso_alto=` (80 Hz)
  -> curva del billar (objetivo plano hasta 1 kHz y -0,75 dB/oct, afinacion de 1 a 8 puntos, claridad -2 dB en 315 Hz y +2 en 2,8 kHz; de -9 a
  +3 dB), paso alto y curva en UN SOLO FIR DE FASE LINEAL (por FFT, lee 50 ms por delante, sin retardo) -> bajo virtual fijo (|x| del grave que
  se quita, homogeneo de grado 1; `billar_bajo_virtual=0`, apagado). Nada sigue a la senal (ganancia constante medida: 0,000000 dB). No voltea:
  los arreglos del marco ya voltearon el invertido. Midiendo: de dia, sin nada de XP. Huella: sin la noche.
- LA NOCHE EN LA B: el corte de noche (`noche_corte=`) va DENTRO del paso alto del tramo, en la misma curva (el mayor de los dos, con la mezcla
  `GraveMono.NocheMezcla`). **El marco NO debe poner `GraveMono.DeNoche` despues de la ganancia en la B** (serian dos filtros en serie); los
  graves de noche (120 Hz) y `FactorNoche` si.
- EL DISCO CONTINUO: `Sigue` llamado AL PREPARAR EL RELEVO (antes de que acabe la de antes) da el MODO EXACTO: la de antes lee del origen de la
  siguiente sus primeros 50 ms, sale con ellos y se los pasa (el cambio de pista sale cuadro a cuadro igual que las dos pegadas). Llamado tarde,
  el empalme de siempre: sin escalon, pero la cola de la de antes (50 ms) deja pasar grave y puede chasquear. Ninguna etapa de delante de la
  pieza debe leer por delante (`Cebado`); si algo mas leyera la siguiente antes de que suene, `ExactoAlFinal = false`.
- La hojita «Billar» de Ctrl+F4 (afinacion de varios puntos con el ruido por bandas EN MONO, la curva dibujada, los mandos) sale con
  `cadena_nueva=1` o `billar_nuevo=1`. El ruido por bandas suena por `CreaWaveOut` (como la prueba de bocinas de XP): con la salida nueva
  siempre abierta hace falta un camino suyo (agente de la salida).
- Pruebas: `Pruebas/RocolaOs/billar-nuevo/prueba.sh`, 86 de 86 en NativeAOT (respuesta, fase lineal, ganancia constante, bajo virtual
  homogeneo y sin aliasing, disco continuo, afinacion, guardar y leer, la hojita); 0,24-0,50 % de un nucleo aqui.

### 8.3 El marco, la medida nueva y la cadena A (agente del marco; detalle, numeros y pruebas en CADENA_NUEVA_A.md) — HECHOS, sin oir

- Funciones `cadena_nueva` (APAGADA; cual suena, en vivo desde la siguiente cancion: Ctrl+F4 -> Cadena; con el modo billar guardado, la B) y
  `medida_nueva` (encendida: solo mide, y solo con la nueva puesta o `RocolaOsMedidaTrabajo.Pedida`). Claves `nivel_lufs=-18`, `techo_dbtp=-1`,
  `agudos_mp3=0`, `tono_parejo=0`. Ganchos g460 (ArmaCadena), g461 (SigueA: el disco continuo, al PREPARAR el relevo, pieza con pieza del mismo
  tipo y orden) y g462 (OpenAudio: el MP3 en coma flotante). `ganchos.py` TODO BIEN.
- El MP3: libmpg123 `ENC_FLOAT_32` (NLayer en coma flotante sin ella), sin recorte; sin huecos con LAME, iTunSMPB y VBRI en MPEG-1/2/2.5.
- La medida: la cancion entera por la cadena (A, o B por `RocolaOsCadenaB.Arma` con `Midiendo`) a ganancia 1 sin el EQ del dueno: BS.1770-4,
  pico real 4x, LRA, pico de muestra, 30 tercios y los arreglos fijos (mudo, invertido, continua; el zumbido de XP). `rocola_medida_nueva.txt`
  con el sello del archivo. Al entrar a la cola, de fondo (nice 19, las mas pedidas primero) y en vivo (Spotify; o la que entro sin tiempo).
- La ganancia: `min(T - L', C - TP')` con el EQ del dueno y el tono parejo estimados por el espectro; los discos en racha, una del disco; sin
  medida, la estimacion conservadora de XP; Spotify la 1.a vez fija estimada y sin el nivelado en vivo; la sustitucion con la de Spotify o la
  de tu MP3. El modo noche: `FactorNoche` en la ganancia y los graves de noche (`SalaLocal`); el corte de 80 Hz (`GraveMono.DeNoche`) solo en la
  A: en la B va dentro de su paso alto (§8.2).
- Las piezas de la A y la B juntas en esta rama (la del billar, 9526e98, juntada): escena con la rocola de verdad y la tarjeta con reloj, A y B
  a -18,0 LUFS (ebur128 de ffmpeg) sin pasar -1 dBTP; ganancia constante (escalones de +-10 dB: 0,002 dB de vaiven en la A; la de hoy, 2,5).
  `Pruebas/RocolaOs/cadena-nueva/prueba.sh`.
- Para la salida nueva: las cadenas nuevas entregan float estereo a la tasa de la cancion en `CadenaCancion.Salida` (con el fundido de XP al
  final), sin limitador: la proteccion de 0 dBFS es de la salida.

Estado: EN CURSO desde el 27-11-2026. Nada probado en el A4 ni oido.

- Salida nueva (g440-g444): HECHA el 27-11-2026, en su rama, sin publicar (CADENA_NUEVA_salida.md): la funcion «salida_nueva» (apagada de fabrica;
  se enciende en Ctrl+F4 -> Sonido con «¿Se oye bien?» 15 s), la tarjeta por hw: y por su nombre (la USB primero), siempre abierta con ceros
  (0,5 s al abrir), S32_LE de 24 bits a 44 100 Hz fijos con el sinc de XP, el volumen del dueno digital a 1 dB por toque, la proteccion de 0 dBFS
  contada, una sola cuantizacion TPDF, los videos por la rocola (tuberia del «file» de alsa-lib, ganancia fija medida con ffmpeg ebur128, noche
  como la musica), la medida del convertidor (Ctrl+F4 y rocola-mide-dac) y las pruebas de sonido por el mismo flujo (SalidaNueva.EmpiezaPrueba,
  para el billar). Guiones cambiados: rocola-tarjeta, rocola-sonido y rocola-mide-dac nuevo (piden la compilacion rapida del sistema). Prueba
  Pruebas/RocolaOs/salida-nueva (JIT y NativeAOT). Nada en el A4, ni con el PO100 ni con el PROZOR de verdad.
