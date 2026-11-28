# La cadena de audio nueva (aprobada el 27-11-2026)

Peticion del dueno del 27-11-2026: «vamos a disenar una nueva cadena de audio, una nueva cadena de audio para el modo billar, y si al final vemos que
genuinamente son mejores estas nuevas que haremos, estas reemplazaran a las actuales. Las nuevas tendras como meta exprimir al maximo el formato mp3,
que suenen lo mejor posible y con volumen constante, no quiero ninguna tecnica de procesamiento si esta hace que el volumen fluctue en tiempo real».
Plan presentado (artefacto 9s1k4u7xzzC7tsdLdpb1nj) y aprobado el mismo dia: «Haz todo lo que me recomiendas en la cadena de sonido».

Reglas de esta tarea: la hacen SOLO agentes Opus (sin Sonnet); solo Rocola OS (rocola-xp con ROCOLA_OS; sin el, XP tal cual); Codigo/ solo por
ganchos de una linea (RocolaOs/GANCHOS.tsv, `python3 Pruebas/RocolaOs/ganchos.py` TODO BIEN); las cadenas nuevas CONVIVEN con las actuales y solo
las reemplazan si ganan a ciegas al mismo volumen y pasan las medidas (el dueno decide). SUSTITUIDO el 28-11-2026: la nueva y la salida nueva son
las UNICAS y la cadena vieja se borra, tambien de Codigo/ (ver «La cadena vieja borrada», al final).

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
  siempre abierta hace falta un camino suyo (agente de la salida). HECHO (28-11-2026, agente de la salida): con la salida nueva abierta va por
  `SalidaNueva.EmpiezaPrueba` (sola, en coma flotante, ganancia 1, con el volumen del dueno; se corta sola si empieza algo pedido); sin ella,
  `CreaWaveOut` como antes (CADENA_NUEVA_salida.md, «Las pruebas de sonido por el mismo flujo»).
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

- Salida nueva (g440-g445): HECHA el 27-11-2026 y juntada con la A y la B el 28-11-2026, en su rama, sin publicar (CADENA_NUEVA_salida.md): la
  funcion «salida_nueva» (apagada de fabrica; se enciende en Ctrl+F4 -> Sonido con «¿Se oye bien?» 15 s), la tarjeta por hw: y por su nombre (la
  USB primero), siempre abierta con ceros (0,5 s al abrir), S32_LE de 24 bits a 44 100 Hz fijos, el volumen del dueno digital a 1 dB por toque,
  la proteccion de 0 dBFS contada, una sola cuantizacion TPDF, los videos por la rocola (tuberia del «file» de alsa-lib, ganancia fija medida con
  ffmpeg ebur128, noche como la musica), la medida del convertidor (Ctrl+F4 y rocola-mide-dac) y las pruebas de sonido por el mismo flujo
  (SalidaNueva.EmpiezaPrueba; el ruido por bandas del billar ya va por ahi; «la musica manda»: se cortan si empieza algo pedido). LA CONVERSION DE
  FRECUENCIA ES LA DE LA SALIDA, SIN LIMITADOR: con ella XP no convierte (g441-g443: ni `frecuencia_salida=`; g445: el relevo sin pausa solo con el
  mismo formato), porque su `ConvierteSalida` pone su limitador de pico real detras; la cadena (la de hoy, la A o la B) sale a la frecuencia de la
  cancion y la fuente del motor la lleva a 44,1 kHz con el mismo sinc, con solo la proteccion detras. Guiones cambiados: rocola-tarjeta,
  rocola-sonido y rocola-mide-dac nuevo (piden la compilacion rapida del sistema). Prueba Pruebas/RocolaOs/salida-nueva (JIT y NativeAOT). Nada en
  el A4, ni con el PO100 ni con el PROZOR de verdad.

## La cadena vieja borrada (28-11-2026)

Lo que pidio el dueno el 28-11-2026: «Elimina la cadena de sonido anterior y tambien el modo billar anterior, yo se que esta cadena de sonido
nueva sera mejor» y, el mismo dia, «Sobre las cadenas de audio viejas elimina todo de ellas, que no quede rastro». Ya no es ponerlas en gris:
se BORRAN de rocola-xp (tambien de Codigo/) la cadena de XP, el modo billar viejo, su repaso de fondo, sus mandos de Ctrl+F3, sus claves, sus
datos y sus pruebas, y la salida de XP (plughw, abrir y cerrar). Se queda, como parte de la cadena nueva, lo que la nueva usa. Sin ROCOLA_OS
tampoco habra cadena de XP: la rocola usara la nueva siempre (la regla «sin ROCOLA_OS = XP tal cual» deja de valer para el sonido; lo demas de
XP no cambia). La rocola de Windows XP (Rocola/) NO se toca.

Se hace en DOS FASES para no chocar con los agentes que trabajan a la vez en la cadena nueva (fallos g464-g479, medida g510-g519, salida y nivel
g530-g539, bajo virtual g540-g544): la FASE 1 (hecha) deja la nueva como la unica con ROCOLA_OS=1 y aparta lo que ya nadie lee; la FASE 2
(cuando el coordinador junte a los demas) borra de verdad, mueve la base de los ganchos y rehace las pruebas.

### Fase 1: lo hecho (en la rama del agente, sin publicar)

| que | donde |
|---|---|
| Una funcion de Rocola OS puede ser `Siempre`: con ROCOLA_OS=1 va siempre y su clave se ignora (su descripcion lo dice); con una lista, si la nombra | `RocolaOs/RocolaOs.cs` (`RocolaOsFuncion.Siempre`, `Decide`, `Pedida`, `Registra`) |
| `cadena_nueva` Siempre (de fabrica encendida): con ROCOLA_OS=1 es la UNICA cadena (la A; con el modo billar guardado, la B); `RocolaOsCadenasNuevas.Suena` ya no lee la clave ni tiene `Forzada` | `RocolaOs.CadenaNueva.cs` |
| `salida_nueva` Siempre (de fabrica encendida); ya no hay «Encender (prueba de 15 s)» ni «¿Se oye bien?»: el boton dice «Siempre encendida» en gris; una prueba de encenderla o apagarla que quedara en `rocola_salida_prueba.txt` se borra sin preguntar; elegir la TARJETA (con su «¿Se oye bien?») se queda | `RocolaOs.SalidaNueva.cs`, `RocolaOs.SalidaNuevaHoja.cs`, `RocolaOs.Salida.cs` |
| LA SALIDA SE RESPALDA SOLA: ya no se rinde al camino de XP (`Rendida` se queda en false). Si la elegida no abre por `hw:`, prueba la misma por `plughw:` (ALSA convierte), la del lanzador (`ROCOLA_ALSA`: el plughw de rocola-tarjeta; en las pruebas la tarjeta con reloj o «null») y `default`, siempre con su motor, su sinc, el volumen del dueno y la proteccion; lo dice en la bitacora («RESPALDO: ...») y en la hoja (en ambar). Si nada abre, espera y lo vuelve a intentar cada segundo. Con una salida sin reloj (el «null» de las pruebas) el motor va al ritmo del reloj (`AlRitmo`), sin escribir sin parar | `RocolaOs.SalidaNuevaTarjetas.cs` (`AbreLaTarjeta`, `Respaldo`), `RocolaOs.SalidaNuevaMotor.cs` (`Bucle`, `AlRitmo`) |
| La hoja «Cadena» de Ctrl+F4 ya no elige: dice cual va (la A, o la B por el modo billar de Ctrl+F3) | `RocolaOs.CadenaNuevaHoja.cs` |
| La hojita «Billar» de Ctrl+F4 siempre a la vista con ROCOLA_OS (siete pestanas: van mas angostas, d33b952; los clics de las pruebas siguen cayendo en su pestana: Imagen 500, Sonido 606, Billar 818, Archivos 924) | `RocolaOs.Billar2Hoja.cs` |
| `spotify_nivela`, `spotify_eq` y `spotify_brillo` (de la cadena de XP) ya no van con ROCOLA_OS=1 (`SoloEnLaLista`: solo con una lista de pruebas que las nombre); la tabla de los niveles de Spotify se carga SOLO PARA LEER (`RocolaOsSpotifyNivela.CargaParaLeer`): la cadena nueva la usa de respaldo para la ganancia fija estimada de Spotify la 1.a vez hasta tener 5 medidas suyas | `RocolaOs.SpotifyNivela.cs`, `RocolaOs.SpotifyEq.cs` |
| Gancho g550 (`Codigo/Form1.Sonido.cs:901`, MeasureFile): con Rocola OS no se junta material para la IA (la cosecha) | `RocolaOs.CadenaVieja.cs` (`RocolaOsCosechaIa`), `GANCHOS.tsv` |
| LOS DATOS VIEJOS QUE YA NADIE LEE, A LA PAPELERA de Archivos de Ctrl+F4 (se vacia sola a los 30 dias; se pueden devolver), una vez por lista (`rocola_cadena_vieja.txt`, `papelera=N`), 3 s despues de arrancar (para no cruzarse con lo que XP aparta al arrancar); un archivo que ya no esta no cuenta como fallo. El MATERIAL DE LA IA (hasta 24 GB, en el disco de la rocola: es derivado y no cabe en la papelera de datos.img) se BORRA (decision (4) del 28-11-2026, «elimina todo de la IA»): solo `rocola_ia_datos.bin`, `.idx` y `.generos.tsv`; las miniaturas y el respaldo de esa misma carpeta (`ROCOLA_IA_DATOS`) no se tocan. Lo llama el Arranca de `cadena_nueva` | `RocolaOs.CadenaVieja.cs` |
| El importador de XP ya no trae ningun modelo de IA (IAS/ ni sueltos), ni `rocola_ia_hechas.txt`, `rocola_billar_nivel.txt` ni `rocola_billar_bitacora.txt` (la carpeta de XP del disco de Windows se sigue solo leyendo) | `Nativo/ImportaXp.cs` (`Tablas`, `NombreModelo`) |
| rocola-tarjeta y rocola-sonido: la USB primero y la tarjeta guardada por su nombre SIEMPRE (salvo con Rocola OS apagado de emergencia, `rocola_os_apagado`); `salida_nueva=` ya no se mira. Piden la compilacion rapida del sistema | `sistema/board/rocola/rootfs_overlay/usr/bin/rocola-tarjeta`, `rocola-sonido` |

La lista 1 de la papelera (fase 1; solo lo que la cadena nueva ya no lee): `rocola_billar_nivel.txt` y `rocola_billar_nivel.antes-2026-11-18.txt`
(el volumen parejo del billar), `rocola_billar_bitacora.txt` y `.1` (la bitacora del billar), `rocola_spotify_eq.txt` (spotify_eq y
spotify_brillo), `IAS/rocola_eq_ia.bin` (la IA del EQ: «a la medida» y el EQ automatico) e `IAS/rocola_ia_genero_*.bin` (el DSEE por genero,
quitado el 18-11-2026) y, con la decision (4), TODOS los modelos de IA (`IAS/rocola_*.bin` -tambien `rocola_ia.bin`, el del DSEE, y la candidata
`rocola_ia.candidata.bin`-, sus `.anterior-N`, los sueltos de la carpeta de datos que XP aun no paso a IAS/), `rocola_ia_hechas.txt` y la carpeta
`IAS` si queda vacia. Sin `rocola_ia.bin`, el DSEE de `agudos_mp3` usa su CURVA FIJA (`EnvolventeClasica` de `SpectralTap`, Codigo/Form1.Sonido.cs:
`if (!((Red ?? NeuralEnv).Loaded && RegenNeural(...)) && !EnvolventeClasica(...))`; existe y funciona sola: es lo que hace sin modelo) desde el
siguiente arranque (en la sesion del primer arranque el modelo ya estaba cargado en memoria). BORRADO del disco: `rocola_ia_datos.bin`, `.idx` y
`.generos.tsv`. NO van todavia (la nueva aun los lee hasta que la fase 2 los porte, abajo): `rocola_volumen.txt`, `rocola_eq.txt`,
`rocola_sonoridad.txt`, `rocola_fuentes.txt`, `rocola_afondo.txt`, `rocola_calidad.txt` y `rocola_spotify_niveles.txt`.

Lo que en la fase 1 aun corre aunque es de la cadena vieja, porque la nueva todavia necesita lo que mide (se va en la fase 2): el REPASO DE XP
(`StartPreScan`: la primera tanda, `MeasureFile` -la estimacion K-RMS, la huella de tono de 8 bandas y el corte de agudos-, y la segunda,
`TandaIntegrada` -algoritmos 5 a 7: BS.1770, P90, LRA, pre-eco, el mapa de silencios, el inicio y el fin utiles y el zumbido-). Sin ROCOLA_OS (o
con una lista que no nombre `cadena_nueva`), la cadena de XP sigue armandose hasta la fase 2. Los mandos de Ctrl+F3 de la cadena vieja siguen a
la vista (con ROCOLA_OS=1 no hacen nada): se QUITAN en la fase 2 (no se ponen en gris).

### El inventario: lo que se BORRA en la fase 2

**Codigo/, archivos enteros** (la cadena de XP y el modo billar viejo; unas 5 700 lineas): `AfinarLocal.cs` (salvo `Generador`/`PruebaBanda`, que
usan `RocolaOs.Billar2Bandas.cs` y `RocolaOs.SalidaNuevaMide.cs`: pasan a RocolaOs/), `AsperezaDinamica.cs`, `AtaqueClaro.cs`,
`BitacoraBillar.cs`, `BomboSinHundir.cs`, `CadenaBillar.cs`, `CompararCiegas.cs`, `DiarioTablas.cs`, `Eses.cs`, `EtapaPorCuadros.cs`,
`LoQueSuena.cs`, `LocalAuto.cs` (salvo el campo `localAuto`/`local_auto=`, que con la nueva es «el modo billar»: elige la B), `NivelDisco.cs`
(salvo `RachaDeDisco`, que usa `RocolaOs.CadenaNuevaGanancia.cs`), `PicoReal.cs` (el limitador y el detector de XP), `PreEco.cs`,
`PresenciaSala.cs`, `Realce.cs`, `RetumbeDinamico.cs`, `Siseo.cs`, `SubirTodo.cs`, `VolumenParejo.cs`, `VolumenWindows.cs` (el 100 % lo pone la
salida nueva una vez, y el lanzador), `VozGolpeYRetumbe.cs`.

**Codigo/, archivos que se PARTEN** (se va lo de la cadena vieja; se queda lo dicho):

| archivo | se va | se queda (lo usa la nueva o no es del sonido) |
|---|---|---|
| `Form1.Sonido.cs` | el bajo psicoacustico y el DSEE de XP (`BassBaseNow`, `BassFactorFor`, `DseeDe`, `ExcitePara`, `IaMejoradaPara`...), las tablas de sonoridad (`LoudFile`, `EqFile`, `ReadLoudCache*`, `LoudTier*`, `NormGainFor`, `FinalizeLoudMeasure`, `CierraSonoridad`), `MeasureFile` entero (con la cosecha), el cuerpo de XP de `ArmaCadena` (todas sus etapas, `LevelTap`, la medida en vivo de XP, `curTap`), `FrecuenciaSalidaValida`/`AFrecuenciaDeLaTarjeta`/`CalientaFrecuenciaSalida` (la salida nueva va a 44,1 kHz fijos), `ApplyNormNow` y lo que solo la llama | el modo noche entero (`NocheDb`... `AvanzaModoNoche`, `FactorNoche`, `NocheCorteHz`, `GravesNocheDe`, `FactorVideoNoche`, `PruebaNoche*`), `PlayTrack`/`StartOpenedTrack` (sin `curTap`), `ArmaCadena` reducida a armar la nueva (o su respaldo «nueva minima» del agente de los fallos), `OpenAudio` (con g462: el MP3 en coma flotante; lo demas, `FileAudioSource`), `BuildCleanMp3`, lo de incompatibles, los bloques del espectro para lo que se ve (`EnqueueLevel`/`ConsumeLevels`/`ReportAudioBlock`), `ComoEstereo`, la aurora y el estado de la pantalla (no son del sonido) |
| `Form1.Datos.cs` | las claves de la cadena vieja (abajo) al leer y guardar `rocola_config.txt`, la carga y el guardado de `rocola_volumen/eq/sonoridad/fuentes/afondo/ia_hechas`, `DeclipVale`, el EQ automatico, el respaldo de esas tablas | lo demas de la configuracion, el estado, las estadisticas, los botones, la noche |
| `Form1.Biblioteca.cs`, `PlanBiblioteca.cs`, `Optimizaciones.cs` | el repaso (`StartPreScan`, `TandaPorMedir`, `CarrilesPara`, `RevisaCarriles`, `TurnoAnalisis`, `MeasureFileResponsive`, `TrabajoMedida`, `AudioFftPlan`, `MarkIaDone`, `MarkDeepDone`, `EscribeInformeCalidad`/`rocola_calidad.txt`) | el escaneo de la biblioteca y el de las duraciones (`StartDurScan`, `rocola_duracion.txt`: de ahi salen las duraciones, no del repaso), `LecturaBiblioteca`, las miniaturas, los tintes, el GC en baja latencia |
| `Form1.Ia.cs` | ENTERO (decision (4): «elimina todo de la IA»): la cosecha y su material (`IaDataFile`, `IaIndexFile`, `GenerosIa`, `iaDone`, `IaPasadas*`, `IaBudgetFull`, `IaInfoText`), la red (`RedChica`), el modelo del DSEE (`NeuralEnv`), la IA del EQ (`EqNet`), el DSEE por genero (`ModeloDseePara`, `dseeGeneros`, `CargaModelosPorGenero`), los generos interpretados para la IA | nada (de `SpectralTap` se quita la rama de la red: `Red`, `RegenNeural`, `iaIn/iaOut`; queda la curva fija `EnvolventeClasica`) |
| `Sonoridad.cs` | `SonoridadIntegrada`, `GananciaAlLimite`, `TandaIntegrada`, `MideIntegrada*`, `DisposicionBs1770` (mas de dos canales), la tabla `rocola_sonoridad.txt` | lo minimo de BS.1770 que usan `RocolaOs.Medida.cs` y `RocolaOs.CalidadFuente.cs` (los filtros K `Estante`/`PasoAlto`, `Potencia`, `Lkfs`, las puertas) y `PicosBloques` si la medida nueva hace el mapa de silencios con el |
| `GraveMono.cs` | el grave en mono, S3, el estrecho, el subgrave de XP | el corte del grave de noche (`GraveMono.DeNoche`, `NocheMezcla`: la A y el paso alto de la B) |
| `SalaLocal.cs` | el local (subgrave, estereo, claridad, partes suaves, balance, graves y agudos del local) | los graves de noche (el estante de 120 Hz, `GravesNocheDb`) y la prueba de bocinas (`PruebaBocinas`, `PruebaModo`) |
| `ArreglosGrabacion.cs` | `ArreglaGrabacion` (canal mudo y continua dinamicos de XP), los interruptores `canal_mudo=`/`quita_continua=`/`quita_zumbido=`, `ZumbidoDe` (lo da la medida nueva) | nada (los arreglos fijos son de `RocolaOs.CadenaNuevaPiezas.cs`) |
| `Zumbido.cs` | — | `MedidorZumbido` (pasa a la medida nueva) y `QuitaZumbido` (la nueva la usa) |
| `AudioControles.cs`, `Form1.SinPausa.cs`, `SinPausa.cs` | `FuenteOriginal`, `LevelTap`, `ConvierteSalida` y el limitador de la conversion de XP, lo de `CadenaCancion` que solo era de XP (`Margen`, sus taps) | `FormatoAudio`, `WavDirecto`, `AudioRamp`, el relevo sin pausa (`Encadenado`, `Relevo`, `Cierre`), `RemuestreoContinuo` y su tabla (los usa la salida nueva), `CadenaCancion` (lo que el marco usa) |
| `SinSilencios.cs`, `RellenoMp3.cs`, `Mp3Ligero.cs`, `FlotanteNLayer.cs`, `CancionEnMemoria.cs`, `AperturaAudio.cs`, `NoEsMp3.cs`, `Pcm16Directo.cs`, `BombaSonido.cs`, `SalidaSonido.cs`, `SalidaPropia.cs`, `EcualizadorDueno.cs`, `Form1.EcualizadorDueno.cs`, `FuentesMedidas.cs` | de `FuentesMedidas.cs`: `FuenteMedida`, `rocola_fuentes.txt`, `PublicaMedidaDeFuente`... (los sellos de las tablas de XP); de los del ecualizador: el margen E3 (`PreganCancionDb`: la nueva no tiene preganancia); de `SalidaSonido.cs`: lo de la salida de Windows (`TextoSalida`) | saltar los silencios y `FuenteSinRelleno` (sin huecos), el lector ligero, NLayer en coma flotante, el calentador, la apertura de fondo, `NoEsMp3`, la bomba del hilo de la musica (la usa Spotify), lo que XP hace cuando falla el aparato (la nueva lanza ENODEV y XP espera y retoma), `WaveOutPropio` como la cara de XP de la salida nueva, el ecualizador del dueno con sus dos memorias y «Sin ecualizador» |
| `Form1.Ajustes.cs`, `AjustesVivos.cs` | de la pagina de Sonido de Ctrl+F3 (lineas ~677-1860) todo lo de la cadena vieja (abajo); `TextoSalida` | la pagina de Sonido reducida (abajo), el asistente de botones, lo demas de Ctrl+F3 |
| `Form1.Video.cs`, `Form1.cs` | `VideoVolumeFor` y lo de la perilla de XP (`normTarget`, `videoVol`, `videoAuto`, `VideoBase`, `MasterLin`): los videos van por la rocola con su ganancia medida (si aun no se midieron, una fija conservadora); `ApplyBassAuto` (g431) | lo demas del video; mpv al 100 |

**Nativo/ y Plataforma/**: `Nativo/SalidaAlsa.cs` pierde su camino directo a ALSA (plughw, abrir y cerrar): `Abre` siempre da una fuente del
motor (los respaldos los hace el motor); `Nativo/DescompresorMp3.cs` (el MP3 de XP a 16 bits con recorte) se va si nada mas lo usa (con g462
siempre puesto solo lo usaria el repaso de XP, que se va; los WAV van por `FileAudioSource`); `Plataforma/VolumenTeclas.cs` pierde el 2 % por
amixer (los botones son el volumen digital de la salida nueva).

**RocolaOs/**: se van `RocolaOs.SpotifyNivela.cs` (con `rocola_spotify_niveles.txt`, cuando la medida nueva de Spotify o la estimacion de P10 lo
sustituya), `RocolaOs.SpotifyEq.cs`, `RocolaOs.NocheVideo.cs` (el filtro de noche de mpv: los videos van por la rocola con la noche del motor),
lo de `RocolaOs.Calidad.cs` que arma la sombra en la cadena de XP (g61, g62, g432-g437) y el nivel de la sombra con las tablas de XP; de
`RocolaOs.CadenaNueva*.cs`, `Medida*.cs` y `SalidaNueva*.cs`: lo que habla de «la de hoy», `Rendida`, `PruebaSalidaNueva`, `salida_nueva_videos=0`
(mpv directo), las claves `cadena_nueva`, `salida_nueva` y `billar_nuevo` (o se quedan declaradas e ignoradas un tiempo). `RocolaOs.CadenaVieja.cs`
se queda (la lista 2 de la papelera). Ganchos que se van con lo que tocaban: g01-g05 (la cosecha y su material), g23, g24, g61, g62, g102, g111,
g250-g254, g260/g261 (R1 en el lector de XP: R1 va en el descompresor nuevo), g430-g437, g441-g443, g445, g550; se rehacen en la base nueva o se
quedan: g13, g110, g112, g113, g235, g440, g444, g460-g463.

**TODA LA IA** (decision (4) del 28-11-2026: «elimina todo de la IA, al igual que lo de recolectar datos para entrenar IA»; se suma a lo de
arriba): en Codigo/ `Form1.Ia.cs` entero, la rama de la red de `SpectralTap` (Form1.Sonido.cs), lo de los modelos en `Form1.Datos.cs`
(`NombreCarpetaIas`, `CarpetaIas`, `OrdenaIas`, `RutaLectura` de los .bin, `PrefijoDseeGenero`), la carga en `Form1.cs` (`NeuralEnv.TryLoad`,
`EqNet`), los textos de los modelos en Ctrl+F3 (la hoja «Mejoras con IA» entera, que ya se va arriba, y «Juntar material para la IA»); el EQ
automatico con IA, I1 (a la medida), I2 (siseo), I3 (por genero, ya quitado el 18-11-2026); la cosecha (`MeasureFile`, ya en el repaso); los
ganchos g01-g05 y g550. En Nativo/: `DatosGrandes.cs` deja de hablar del material de la IA (`ReservaBytes` de la cosecha, la parada con menos
de 256 MB) y se queda solo para las miniaturas y el respaldo (la variable del lanzador `ROCOLA_IA_DATOS` y la carpeta `/rocola/disco/rocola-os/
ia-xp` conservan su nombre por las miniaturas; si se renombra, el lanzador `sistema/board/rocola/rootfs_overlay/usr/bin/rocola` y una mudanza en
el mismo disco); `ImportaXp.cs` ya no trae modelos (hecho en la fase 1). EntrenaIa, IaNueva, MaterialIa y CortesIa (la candidata y su
comparacion a ciegas) eran del programa retirado (`sistema/rocola/Motor`, rocola-consola): en rocola-xp no estan; si se quiere, `sistema/rocola/
Motor` y `sistema/rocola/Consola` (solo de referencia desde el 14-11-2026) pierden tambien esos archivos y los de su cadena (lo decide el
coordinador: rocola-xp todavia toma de ahi `Gl.cs`, `Kms.cs` y `Botones.cs`; alli estan `EntrenaIa.cs`, `IaNueva.cs`, `MaterialIa.cs`,
`CortesIa.cs`). La carpeta `ia/` de la raiz (los guiones para entrenar en otra PC) es de la rocola de Windows XP (Rocola/): NO se toca. Claves:
`ia_datos=`, `ia_datos_mb=`, `dsee_genero=`, `a_la_medida=`, `quita_siseo=`. Pruebas: las de la IA de rocola-xp que haya (se buscan por
`rocola_ia`, `NeuralEnv`, `EqNet` en `Pruebas/`).

**Claves de configuracion** que se van: de `rocola_config.txt` (XP) `normalizar=`, `nivel_objetivo=`, `nivel_fuerza=`, `video_vol=`,
`video_automatico=`, `eq_auto=`, `eq_automatico=`, `bajo=`, `bajo_automatico=`, `realce=`, `dsee=`, `dsee_forzar=`, `dsee_genero=`, `ia_datos=`,
`ia_datos_mb=`, `declip=`, `a_la_medida=`, `quita_siseo=`, `canal_mudo=`, `quita_continua=`, `estereo_auto=`, `quita_zumbido=`, `quita_preeco=`,
`suaviza_eses=`, `bombo_sin_hundir=`, `retumbe_dinamico=`, `ataque_claro=`, `aspereza_dinamica=`, `claridad_local=`, `partes_suaves=`,
`balance=`, `graves_local=`, `agudos_local=`, `presencia_sala=`, `corta_subgrave=`, `subgrave=`, `estereo_estrecho=`, `ancho_estereo=`,
`grave_mono=`, `tipo_local=`, `afinado_local=`, `afinado_ponderacion=`, `afinado_usar=`, `parejo_v2=`, `frecuencia_salida=`, y `relevo=` si solo
era de la salida de XP (se mira en la fase 2); de `rocola_os.txt`: `spotify_nivela=`, `spotify_eq=`, `spotify_brillo=`, `cadena_nueva=`,
`salida_nueva=`, `billar_nuevo=`, `salida_nueva_videos=`. Se quedan: `local_auto=` (el modo billar: la B), `eq_dueno=` y `eq_dueno_billar=`,
`noche*=`, `saltar_silencios=`, `fundido=`. La afinacion vieja de un punto (`afinado_local=`) se pasa UNA vez como punto 1 de la B si la B no tiene
ninguno (y se quita «Traer la de Ctrl+F3» de la hojita Billar). Al leer una configuracion vieja, las claves que se fueron se ignoran (y no se
vuelven a escribir).

**Ctrl+F3 -> Sonido** (se QUITAN, no van en gris): «Subir cada cancion hasta su limite», «Cuanto se sube cada cancion», «Videos y canciones aun sin
medir», «Volumen de los videos» (y su Auto), toda la tarjeta CALIDAD DEL SONIDO (EQ automatico, Graves, Realce, DSEE), las hojas «El local y sus
bocinas» (subgrave, estereo, claridad, partes suaves, balance, «Tarjeta a:» 0/44,1/48 kHz), «Tono y presencia» (graves y agudos del local, eses,
presencia), «Afinar el local con el celular» (las 8 barras, dBA/dBC, «Usar la afinacion»: la afinacion es la de Ctrl+F4 -> Billar), «Mejoras con
IA» (a la medida, siseo, los modelos), «Lo que suena ahora» («Escuchar sin mejoras», «Billar o fabrica», «Con o sin afinar», «Me gusta esta»,
canal mudo, continua, zumbido, pre-eco, estereo estrecho solo si hace falta: lo que suena ahora esta en Ctrl+F4 -> Cadena), «Voz, golpe y
retumbe», y de TU BIBLIOTECA «Repasar la biblioteca a fondo», «Juntar material para la IA» y el resumen de calidad (la ficha de calidad de la
medida nueva lo sustituye, agente de la medida). QUEDA la pagina de Sonido con: el modo noche (interruptor, cuanto se oye, «Probar» y su hoja
«Graves, corte y madrugada»), EL BILLAR (el interruptor «Modo billar», que elige la B, con su texto nuevo y «Ctrl+F4 -> Billar»; la prueba de
bocinas), «Ecualizador...» (su hoja entera: las dos memorias y «Sin ecualizador»; sin el renglon del margen), «Saltar silencios largos», el tipo de
disco, «Dejar como de fabrica» (solo lo que queda) y un renglon que manda a Ctrl+F4 -> Sonido y Cadena.

**Datos** (lista 2 de la papelera, en la fase 2, cuando la medida nueva ya de lo que la nueva leia de ellos): `rocola_volumen.txt`, `rocola_eq.txt`,
`rocola_sonoridad.txt`, `rocola_fuentes.txt`, `rocola_afondo.txt`, `rocola_ia_hechas.txt`, `rocola_calidad.txt`, `rocola_spotify_niveles.txt` y
sus copias de `respaldo/` (en el disco de la rocola: se apartan como el material de la IA). Lo que no quepa en el tope de la papelera (64 MB de
fabrica) se aparta en el disco como el material de la IA. Se queda `IAS/rocola_ia.bin`.

**Pruebas** que se van o cambian: `Pruebas/Windows/sonido.sh`, `difsonido.py` y `variantes.sh` (comparan el sonido de XP con la referencia
congelada: ya no hay sonido de XP que comparar), las escenas de `Pruebas/RocolaOs/spotify-xp` `nivela`, `speq` y las que dan por hecho el
nivelado de XP, `Pruebas/RocolaOs/noche-video` (el filtro de mpv), de `cadena-nueva` las escenas «hoy» y la comparacion con el limitador de hoy
(constante: 6,3 dB), de `salida-nueva` las partes `hoja` (encender con «¿Se oye bien?»), `rendida` (pasa a «respaldo»), `guiones` (la USB primero ya
no depende de `salida_nueva=1`), `sin` y `bitabit` (sin ROCOLA_OS tambien la salida nueva), de `andamio` y `pantalla` lo de «sin ROCOLA_OS ni
bitacora ni rocola_os.txt» para el sonido y el Tab de mas de la pestana Billar, de `billar-nuevo` la comprobacion de (818,54) «sin Billar»,
`salida-audio` (sus botones con la salida nueva siempre), `volumen` (amixer: pasa al volumen digital), `sustitucion` (la sombra en la cadena nueva)
y `huecos` (R1 en el descompresor nuevo). Las de la rocola de XP de Rocola/ (`billar18`, `billarparejo`, `compara-cadena`...) no se tocan.

### Lo que la nueva necesita antes de borrar (se porta en la fase 2, sobre lo que junte el agente de la medida)

La cadena nueva y lo que se queda leen hoy de las tablas de XP (por eso su repaso sigue en la fase 1):
1. **Saltar los silencios largos** (`SinSilencios.MapaDe`): el mapa de silencios (algoritmo 3 de `rocola_sonoridad.txt`) -> la medida nueva lo
   saca de lo crudo (bloques de 100 ms con su pico, -60 dBFS, como `PicosBloques.Mapa`) y lo guarda en un campo mas del renglon
   (`desde;hasta;hueco_desde;hueco_hasta`, despues de los de hoy: el agente de la medida tambien anade campos, se juntan al final); un gancho en
   `MapaDe` mira primero la medida nueva.
2. **El zumbido** (`ZumbidoDe`, T6): `MedidorZumbido` dentro de `RocolaOsCrudo`; si lo encuentra y no se midio con el, la segunda pasada lo lleva
   puesto (como los demas arreglos).
3. **La sustitucion invisible**: el corte de agudos medido (`EqCut` de `rocola_eq.txt`) -> el de la ficha de calidad de la medida nueva (agente de
   la medida, P2); la frontera continua de un disco (`InicioUtil`/`FinUtil`) -> el mapa de silencios nuevo (punto 1); su nivel -> el de la medida
   nueva (ya lo es con la nueva).
4. **El tono parejo** (opcional de la A) y su estimacion en la ganancia: la huella de 8 bandas de `rocola_eq.txt` -> los 30 tercios de la medida
   nueva contra la media de la biblioteca (en tercios).
5. **Lo que aun no se midio** (`CadenaNuevaGanancia`: la integrada y la K-RMS de XP) -> la estimacion de P10 del agente de la medida; Spotify la
   1.a vez -> la mediana de las de Spotify de la medida nueva (y, mientras no haya 5, una fija conservadora).
6. **Los videos sin medir** (la regla de XP, `VideoVolumeFor` con `loudCache`) -> una ganancia fija conservadora hasta que ffmpeg los mida.
7. **El DSEE de `agudos_mp3`**: solo con la CURVA FIJA (`SpectralTap` sin `Red`/`NeuralEnv`: `EnvolventeClasica`), sin ningun modelo, sin cosecha ni
   entrenamiento (decision (4)). Se comprueba en la fase 2 con la prueba de `agudos_mp3` de cadena-nueva sin ningun .bin.

### Plan para Codigo/ y los ganchos (fase 2)

Borrar codigo de XP rompe la comparacion de `ganchos.py` contra la base 8c8cba0 (punto 5: devolviendo los ganchos, Codigo/ igual byte a byte que
la base) y mueve las lineas (las trazas nombran relojes y encargos por «archivo:linea», y la referencia congelada, /root/rocola-trabajo/
referencia-xp, tiene esas lineas). Como se hizo con 8c8cba0 (la junta 4), una BASE NUEVA documentada:
1. Con todo juntado (los otros agentes ya en claude/modest-carson-fa2v2n), un commit «Codigo/ sin la cadena vieja (base nueva de los ganchos)» en
   el que Codigo/ es la base 8c8cba0 (sin ganchos) MENOS lo del inventario: archivos quitados enteros y, en los partidos, metodos, campos y
   lineas enteras quitados (nunca reescritos: lo que queda es la traduccion de XP tal cual). Lo arma un guion que parte de la base pura
   (`ganchos.py` ya sabe devolver los ganchos), quita lo de la lista y no toca nada mas: el diff contra 8c8cba0 es SOLO de lineas quitadas (se
   revisa con el diff por archivos y con `diff-xp.py --sin-ganchos`).
2. Encima, otro commit con los ganchos que quedan, cada uno en su linea nueva (`GANCHOS.tsv` con los numeros nuevos, buscados por su «ahora»); lo
   que hoy son varias lineas de XP que la nueva sustituye (el cuerpo de `ArmaCadena`) se queda en una linea de la base que llama a RocolaOs/ (la
   nueva vive en RocolaOs/; Codigo/ solo la llama).
3. `ganchos.py` pasa a esa base (su `base` de fabrica y su comentario: «la base sin la cadena vieja, 28-11-2026: Codigo/ como 8c8cba0 menos lo de
   CADENA_NUEVA.md, La cadena vieja borrada»), y la regla de AGENTS.md del 8-11-2026 («Codigo/ es Rocola/ con solo las diferencias de traduccion»)
   dice la excepcion: sin la cadena de sonido de XP.
4. Las trazas por «archivo:linea» y las cintas: lo que no es sonido no cambia de contenido (solo de numero de linea en los archivos partidos); se
   vuelven a sacar las de referencia en esta rama (las de la referencia congelada, aef2211, dejan de valer para los archivos partidos: se apunta
   en PORT_JUNTAS.md). Los relojes con nombre fijo no cambian.

### Plan para que la nueva sea la de siempre, tambien sin ROCOLA_OS (fase 2)

Hoy sin ROCOLA_OS no corre nada de Rocola OS (ni latido, ni hilos, ni rocola_os.txt). El sonido deja de depender de ese interruptor:
1. En el andamio, una funcion puede ser «del sonido» (`RocolaOsFuncion.Sonido`): va aunque no este ROCOLA_OS (o con `rocola_os_apagado`): su
   Arranca, su latido (un reloj suyo con nombre fijo, sin el resto del latido) y sus hilos; sus claves se leen y se guardan en rocola_os.txt
   tambien sin ROCOLA_OS (el volumen del dueno, el techo medido del convertidor y la curva del billar no se pierden en el modo de emergencia); su
   bitacora, la de Rocola OS. Lo son `cadena_nueva`, `medida_nueva` y `salida_nueva`.
2. Los ganchos del sonido preguntan por esas funciones, no por `RocolaOs.Activo` (lo demas de Rocola OS sigue apagado sin ROCOLA_OS: Ctrl+F4,
   Spotify, el cielo...).
3. Con el camino directo de `SalidaAlsa` borrado, si la salida nueva no abre nada la musica espera al aparato (como XP).
4. Las pruebas de «sin ROCOLA_OS = XP bit a bit» dejan de comparar el sonido (las cintas y las escenas de pantalla siguen).

### El orden de la fase 2

(1) juntar claude/modest-carson-fa2v2n (los otros agentes); (2) portar a la medida nueva lo de «lo que la nueva necesita» (con el agente de la
medida si sigue); (3) la nueva sin ROCOLA_OS (andamio); (4) borrar (los dos commits de la base nueva) y la lista 2 de la papelera; (5) la pagina
de Sonido de Ctrl+F3 reducida; (6) las pruebas; (7) AGENTS.md, este documento, CADENA_NUEVA_A/B/salida.md.
