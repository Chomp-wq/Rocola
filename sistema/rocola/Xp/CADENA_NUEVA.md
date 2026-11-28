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

Estado: EN CURSO desde el 27-11-2026. Nada probado en el A4 ni oido.

- Salida nueva (g440-g444): HECHA el 27-11-2026, en su rama, sin publicar (CADENA_NUEVA_salida.md): la funcion «salida_nueva» (apagada de fabrica;
  se enciende en Ctrl+F4 -> Sonido con «¿Se oye bien?» 15 s), la tarjeta por hw: y por su nombre (la USB primero), siempre abierta con ceros
  (0,5 s al abrir), S32_LE de 24 bits a 44 100 Hz fijos con el sinc de XP, el volumen del dueno digital a 1 dB por toque, la proteccion de 0 dBFS
  contada, una sola cuantizacion TPDF, los videos por la rocola (tuberia del «file» de alsa-lib, ganancia fija medida con ffmpeg ebur128, noche
  como la musica), la medida del convertidor (Ctrl+F4 y rocola-mide-dac) y las pruebas de sonido por el mismo flujo (SalidaNueva.EmpiezaPrueba,
  para el billar). Guiones cambiados: rocola-tarjeta, rocola-sonido y rocola-mide-dac nuevo (piden la compilacion rapida del sistema). Prueba
  Pruebas/RocolaOs/salida-nueva (JIT y NativeAOT). Nada en el A4, ni con el PO100 ni con el PROZOR de verdad.
