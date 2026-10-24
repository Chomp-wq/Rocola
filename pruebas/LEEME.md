# Las pruebas de la rocola

Cada archivo `.cs` de esta carpeta es una **batería**: un programa suelto que carga el
ejecutable de la rocola y le mira las tripas por dentro. No son pruebas de las que solo
comprueban que una función devuelve 2+2=4; miden lo que cuesta algo **de verdad**, cuentan
los píxeles de lo que se pinta, o leen el código ya compilado para comprobar que una función
sigue enchufada donde debe.

Cada una escribe líneas `OK` y `FALLO` y devuelve cuántos fallos hubo.

## Cómo se corren

En Windows: `pruebas/validar-capas.ps1 -OutputDirectory <carpeta-temporal>`
compila GPU/CPU y ejecuta las baterías generales, incluida `espacioprecarga`
(siguiente página/letra, prioridades, memoria adaptativa, estrella y ocultación).
No reemplaza ejecutables instalados. La prueba HAL usa Direct3D real cuando está
disponible; si falta, lo indica y no demuestra el comportamiento de la tarjeta.

Para la auditoria del 11 de octubre de 2026:
`pruebas/validar-auditoria-general.ps1 -OutputDirectory <carpeta-temporal>`.
Compila con .NET 3.5 y comprueba guardado fallido/reintentos, rutas equivalentes,
buscador, fichas con datos tardios, alfa de miniaturas y reloj WinMM. Incluye un
driver artificial para texturas limitadas, uniones, relleno, direccionamiento y
referencias compartidas. No abre audio ni instala nada; no equivale a medir FPS,
probar una GPU real antigua o Windows XP.
[Resultados y limites](../AUDITORIA_GENERAL_MODEST_2026-10-11.md).

```sh
sh pruebas/todo.sh              # la suite entera (compila la rocola sola y arranca)
sh pruebas/todo.sh disco ssd    # solo esas dos
sh pruebas/todo.sh --lista      # cuáles hay
```

No hay que preparar nada. El lanzador detecta si la rama es la de Windows XP (.NET 3.5) o la
de tarjeta gráfica (.NET 4.8), compila los dos perfiles —con y sin GPU— y corre todo. Hace
falta `mcs` (Mono), `xvfb-run` y `python3`.

## Qué vigila cada una

**Sonido**

| batería | qué comprueba |
|---|---|
| `dsee` | el reconstructor de agudos: que no se cuele un canal en el otro al meter los dos en una FFT, que apagado sea bit a bit idéntico, que una canción completa salga intacta, que el realce no empuje dos veces lo mismo y, desde la copia por bloques, lo que se oye (sección 11): un tono recreado limpio, el nivel de la curva de diseño, el ruido alrededor de un tono y entre golpes, sin temblor y con el corte quieto, también con la IA |
| `iaparidad` | que el entrenador mide lo mismo que la rocola: las 28 entradas y los 12 objetivos de `MakePair` contra `ia/paridad_extractor.txt`, todos los cortes a seis frecuencias de muestreo contrastados con `ia/contrato_dsee.py` (lanza Python; sin Python lo dice y no lo da por comprobado), las bandas de la escala que deja `RegenNeural` para la copia, y que la imitación en float32 de la red que usa el entrenador da las mismas salidas que `RedChica.Predict`, bit a bit |
| `iaorigen` | que la rocola apunta de qué canción viene cada ejemplo de la IA (`rocola_ia_datos.idx`) y que el lector del entrenador nunca lo atribuye mal: apagones a media escritura, bytes cambiados, datos recortados o borrados, índice ajeno y cuatro hilos a la vez (lanza Python) |
| `sonoridad` | la sonoridad integrada ITU-R BS.1770 de la canción entera, aparte de la estimación rápida: los coeficientes de la norma, los casos 1 a 6 de EBU Tech 3341 a seis frecuencias, lecturas de cualquier tamaño, canales y LFE sin adivinar la disposición, puertas, contra la referencia independiente `ref_bs1770.py` (lanza Python; sin él lo dice), la tabla versionada `rocola_sonoridad.txt`, que solo se publica lo medido entero, el nivelado con los mismos topes, los mínimos por duración y las lecturas que parten un cuadro |
| `picoreal` | que la onda que reconstruye el convertidor no pase del techo: el detector de pico real (filtro, casos al estilo de EBU Tech 3341, contra la referencia independiente `ref_picoreal.py`), la salida del limitador medida a cuatro frecuencias y 1/2/6 canales, lo que dejaba pasar el limitador viejo, que lo que no pasa del techo salga intacto, cola, silencio, archivos cortos, saltos, lecturas que parten un cuadro, la misma ganancia en todos los canales, sin distorsión ni bombeo, la cadena real y lo que cuesta (lanza Python; sin él lo dice) Las lecturas de menos de un cuadro (una muestra en estéreo) tienen su propia batería, `picomuestras`. |
| `realce` | que el realce de agudos no pliegue armónicos: el filtro de media banda (medido), paso directo bit a bit si la canción empieza con el realce en cero, el seco intacto y retrasado 31 cuadros si se apaga a media canción, que seco, parte lineal y saturación salgan alineados, el plegado contra la referencia independiente `ref_realce.py` (saturación a 16× exacta) en tonos, barrido, multitono y música tipo MP3 a 44,1 y 48 kHz, frente al realce de antes; seis canales sin cruces, 119 tamaños de lectura bit a bit iguales, la cola de 31 cuadros, archivos cortísimos, NaN, subirlo a media canción sin salto; el grave sintético medido aparte; cableado y coste (lanza Python; sin él lo dice) |
| `transiciones` | que el grave sintético y el DSEE cambien en vivo sin escalones ni saltos: la perilla del grave en rampa de 20 ms hasta el valor exacto (medida desde fuera), sin fundido al empezar la canción, apagado intacto bit a bit, sin estado viejo al volver, NaN ignorado, cualquier tamaño de lectura; el DSEE que al apagarse no pierde ni repite ninguna muestra, entra y sale con fundido (comparado con la misma corrida sin fundido), mide bien el corte al encenderse, aguanta alternarse cada pocos ms con lecturas que parten cuadros y acabar a media transición; y lo que cuesta |
| `atoron` | lo que la pantalla se queda parada cada vez que hay que dar de comer al sonido |
| `nivel`, `bajo`, `bajoauto` | nivelado de volumen y bajo psicoacústico |
| `nivelpicos` | el nivelado que mira los picos: el medidor de picos por bloques de 100 ms (pico y P99, silencio, trozos, último bloque), la tabla `rocola_sonoridad.txt` de algoritmo 2, la ganancia (topes de 3 y 6 dB, bajar un poco las muy fuertes, el orden) y, con el limitador de verdad, cuánto trabaja antes y después |
| `relleno` | los MP3 sin huecos entre pistas: la etiqueta LAME de MP3 de verdad (`relleno_*.mp3`, hechos con ffmpeg/LAME: estéreo, mono, 48 kHz, tasa variable, sin etiqueta, MPEG-2, con ID3 delante, con un bit cambiado), medir el descompresor con el clic que lleva la rocola (y cuándo no fiarse), el recorte de un disco continuo partido en tres MP3 (idéntico al original), que esté conectado y que la etiqueta cuadre con el archivo (un MP3 unido con `copy /b` lleva la de su primera parte y se cortaba a la mitad; uno cortado ponía el fundido después del final: ahora suenan enteros, como antes) Y desde el 22-10-2026, un fallo al medir el descompresor se reintenta una sola vez pasados 5 minutos. |
| `flotantexp` | *(solo Modest)* los MP3 en coma flotante con NLayer: cuadro a cuadro bit a bit igual que NLayer con el archivo entero, lo que pasa del máximo (`flotante_overs.mp3`), la comparación con el de siempre (imitado) y el freno del gasto, cuándo se usa (y que si no, el lector queda como antes), el lector de NAudio de verdad y que esté conectado |
| `nlayerdentro` | *(solo Modest)* `NLayer.dll` va dentro de `Rocola.exe`: el exe lleva la misma de `packages/` (y el proyecto de Visual Studio también), y en una carpeta con solo `Rocola.exe` y `NAudio.dll` el descompresor de NLayer se arma igual (sin el enganche, no); `Main` lo engancha antes que nada |
| `franjareloj` | solo con tarjeta: el reloj de la canción repinta y sube solo su recuadro de la franja de abajo (FranjaLigeraGpu.cs): idéntica píxel a píxel a rehacerla entera en 24 escenas × 9 pasos, los títulos que se deslizan no se tocan, lo demás va entera, el trozo solo para la versión de justo antes, y la tarjeta imitada recibe solo el trozo |
| `prioridad` | la rocola arranca con prioridad por encima de lo normal (Program.cs): solo la rocola que se queda (no la segunda que se cierra), antes de abrir la ventana, nunca "alta" ni "tiempo real", en un solo sitio y sin romper nada si Windows no deja |
| `diario` | las tablas grandes sin reescribirlas enteras (DiarioTablas.cs): la primera vez de la sesión entera y con los bytes de siempre, después solo lo nuevo al final, sin cambios nada; entera si se quita una canción, si alguien tocó el archivo o si desapareció; compacta cuando hay demasiadas repetidas; al leer (arranque y respaldo) manda la última línea; una última línea a medias o con ceros no se lee; lo mismo en el EQ y en la integrada (con su cabecera y los registros futuros); y lo que se escribe en la primera medición de 20.000 canciones |
| `estadopendiente` | tras un apagón, los créditos y la cola se restauran cuando la biblioteca está armada, pero la moneda funciona desde el primer instante: una moneda en ese rato ya no pisa el estado de antes. Antes de restaurar, guardar conserva lo de antes y le suma lo de ahora (créditos sumados sin contar dos veces, la cola de antes delante, la hora de antes); lo de otro día del negocio no se conserva; ya restaurado, como siempre |
| `otroarchivo` | en la ruta de una canción hay hoy otro archivo (la completa en lugar de una descarga cortada, otra versión con el mismo nombre): con la duración apuntada del de antes, el fundido de salida bajaba a cero donde acababa aquel y el resto de la canción pagada sonaba en silencio. Con la cadena de verdad (`ArmaCadena`): suena entera y el fundido cae al final de la de ahora; con la duración bien apuntada (o unas décimas distinta), como siempre; lo apuntado se sigue preguntando primero; y lo aprendido del archivo de antes (volumen, duración, EQ, integrada, repaso a fondo) se olvida, se guarda así y se vuelve a aprender oyéndola, sin tocar lo de las demás canciones (antes se quedaba para siempre) |
| `firmabiblioteca` | una carpeta de la música que no se puede leer (sin permiso, una ruta de más de 260 letras) ya no tira la firma de la biblioteca entera: antes, sin firma, la biblioteca no se guardaba y cada arranque recorría el disco completo. Con una carpeta cerrada de verdad (en Linux, siendo root, el árbol pasa a `nobody` y el proceso también): la firma sale, es estable, apunta la carpeta cerrada, la de antes y después del recorrido coinciden, y con todo abierto es la de siempre (las bibliotecas guardadas siguen valiendo); y el recorrido de la biblioteca salta esa carpeta, sigue con lo demás y lo dice en `rocola_error.txt` con el motivo (antes se la tragaba sin decir nada) |
| `firmarapida` | la firma de la biblioteca, más rápida pero la misma (19-10-2026): `RootHash` con la minúscula de ASCII a mano da el valor de `ToLowerInvariant` en los 65.536 caracteres y en miles de cadenas (acentos, I turca, eszett, Kelvin, Cirílico, pares sustitutos); `OrdenaRutasDeCarpeta` (ordenar por la parte del nombre) deja el mismo orden que `Array.Sort(OrdinalIgnoreCase)`, también con empates y sin prefijo común; árboles de verdad (nombres raros, atajo, carpeta «Windows», 3.000 archivos) con la misma firma que el código anterior copiado en la batería (y que el ejecutable anterior, si se le pasa como segundo argumento) en los dos caminos (.NET 4 y CLR 2); el recorrido rápido de CLR 2 (`RecorridoRapido.cs`) con un origen de datos inventado: mismo orden, tamaño, fecha, atajos, memoria del recorrido y errores (`!TipoDeError`), y null o excepción del origen vuelven a lo de siempre; nombres que Windows toma por aparatos, puntos finales y rutas largas dan null; `WIN32_FIND_DATA` mide 592 bytes. **La llamada de Windows (`FindFirstFileW`) no se ejecuta fuera de Windows** y esta batería no lo afirma |
| `respaldobytes` | el respaldo del arranque comprueba cada copia por bytes y no por dos SHA-256 (19-10-2026): el código ya no calcula ningún hash, confirma en disco, comprueba y solo después publica por renombrado; `CompruebaCopiaDeRespaldo` deja pasar copias iguales (vacío, un bloque, un byte más, 1,5 MB) y rechaza con «La copia del respaldo no coincide» un solo bit distinto en cualquier sitio (primero, último, borde de bloque), más corta, más larga, corrida un byte o llena de ceros; un anexo en curso se compara solo hasta lo capturado; un origen truncado da el mensaje de siempre; cede al trabajo visible entre bloque y bloque. De punta a punta (copia publicada con los mismos bytes; origen que deja de coincidir tras confirmar la copia: no se publica, no deja carpeta a medias, el respaldo anterior queda) solo donde existe `FlushFileBuffers` (Windows, o Linux con un puente de pruebas): si no, lo dice y lo salta |
| `cosechaunavez` | la cosecha de la IA, una vez por canción (19-10-2026): el origen (etiqueta, género, huellas) se saca una vez y los canales se anexan juntos con una confirmación en disco por archivo (datos, índice, géneros: de 6 a 3 en estéreo, medido con un `FlushFileBuffers` que cuenta). Lo guardado es igual, byte a byte, que anexando los canales uno por uno (16 casos: estéreo, mono, seguidas, 1.536 filas, lotes inválidos entre buenos, sin origen en uno, géneros raros, archivos nuevos que apartan el índice y los géneros de antes, cola rota de un apagon en datos, índice y géneros, índice ajeno, datos ajenos, tope del archivo con cabida para los dos, para uno o para ninguno) y, si se le pasa el ejecutable anterior como segundo argumento, igual que él; el contador de ejemplos; `IaOrigenParaCanal` contra `IaOrigenDe`; y que `MeasureFile` use el camino nuevo |
| `objetosmenos` | menos objetos de vida larga por canción (19-10-2026): el espejo de la tabla de EQ comparte el arreglo de cada canción en lugar de copiarlo (y sigue siendo una foto: una medida nueva pone otro arreglo); el registro de fuentes medidas es un objeto en vez de cuatro (el registro, su `FuenteMedida` y dos cadenas hexadecimales) y `rocola_fuentes.txt` queda igual, letra por letra (3.000 filas leídas y reescritas; las filas rotas se siguen descartando, ahora también las de 16 caracteres que no son hexadecimales; una huella en minúsculas se reescribe en mayúsculas); `Track.FromFile` saca el artista de la carpeta una vez por carpeta y las canciones de ella comparten la cadena, con las mismas canciones que el código de antes (6.030 llamadas por carpetas y revueltas, cuatro hilos a la vez sin mezclar carpetas). Con el ejecutable anterior como segundo argumento compara contra él y mide la memoria (20.000 fuentes: 328 → 210 bytes por canción; 30.000 canciones: −6 %; Mono/Linux, sintético) |
| `lectura16` | los 16 bits enteros del repaso con `Pcm16Directo.cs` (20-10-2026): un MP3 que descomprime el sistema o un WAV de 16 bits se lee ahora multiplicando por 2^-15 (exacto: el mismo float que dividir entre 32768) y sin el barrido de NaN, que en 16 bits no puede encontrar nada, en lugar de pasar por las cuatro piezas del `SampleChannel` de NAudio. Exige el MISMO float, bit a bit, que `SampleChannel`: los 65.536 enteros de 16 bits (mono y estéreo) con lecturas de 1, 2, 3, 7, 4.095, 4.096, 8.191, 8.192 y 131.072 muestras y con un origen que entrega de 3 en 3 o de 4.097 en 4.097 bytes, con volumen distinto de 1, sobre los MP3 de pruebas con el lector de NAudio y con el ligero (descompresor de 16 bits imitado), y la `LectorMedida` de verdad con WAV de 16 bits a cinco frecuencias (camino directo y mismos floats) y de 8, 24 y 32 bits en coma flotante (sin camino directo; el NaN, el infinito y lo que pasa de 4 siguen limpiándose a cero). Con la rocola anterior como segundo argumento compara además `MeasureFile` + `MideIntegrada` de los WAV generados, bit a bit. Mide, a título informativo, cuánto cuesta cada conversión |
| `entradaligera` | el aprendizaje en vivo de una canción nueva (`MideBloqueOriginal`, 20-10-2026): mientras suena una canción que el repaso aún no midió, la cadena le aprende la sonoridad K y la huella tonal; ahora los cuadros enteros van por trozos (la huella con `HuellaDelTrozo`, la del repaso, y la sonoridad K de estéreo con los coeficientes y la memoria de los dos filtros en variables, `KCuadrosEstereo`; el cuadro partido de los extremos, el mono y los canales de más siguen muestra a muestra). Lleva escrita una copia del código de antes y le da el mismo audio troceado de todas las maneras (lecturas de 1, 2, 3, 5, 7, 63, 64, 65, 127, 128, 129, 1.000, 4.096, 4.410 y 8.191 muestras con desplazamiento, a 8, 22,05, 44,1, 48 y 96 kHz, de 1 a 6 canales, con música, ceros exactos y picos): después de cada lectura exige iguales, bit a bit, `curSumSq`, `curSamples`, `kwCanal`, `kwLimpia`, `kwSt`, las 8 sumas de la huella, `eqMeasN`, `eqLimpia`, `eqMeasSt` y el cuadro partido que espera; el buffer no se toca fuera de su trozo. Control: un bit cambiado en lo de antes se descubre. Lee el programa compilado (llama a los trozos, `AccumulateEqFrame` solo para el cuadro que completa un partido) y mide cuánto cuesta |
| `siseorapido` | la transformada del quitasiseo (I2) con las etapas de dos en dos (22-10-2026; `QuitaSiseo.Transforma`, `Siseo.cs`): la misma cuenta mariposa a mariposa, con los arreglos y el seno con su signo (`senN` = -`senT`, negar es exacto) en variables locales y la etapa de tamaño L junto con la siguiente (2L) con los cuatro valores en variables y no escritos y vueltos a leer. Se compara con una copia del código de antes (la lleva escrita la batería, con las tablas del objeto): ruido, impulsos, continua, signos alternos, ceros con signo (-0), desnormales, desbordes a infinito y NaN, vectores dispersos como los de la transformada de vuelta, a 32, 44,1, 48 y 96 kHz (1.024 y 2.048 puntos: con 2.048 queda una etapa suelta) y en las dos direcciones; `re` e `im` iguales BIT A BIT. Y la etapa entera con un siseo de fondo que dispara la rebaja, troceada de cuatro maneras (4.410, de 1 en 1, 1.023 y 50.000): la misma salida bit a bit. Mutando una línea del código nuevo la batería falla (probado). |
| `picorapido` | el detector de pico real del limitador (22-10-2026; `DetectorPicoReal.Bloque`, `PicoReal.cs`): el filtro de 24 coeficientes (12 parejas simétricas) va desenrollado a mano, con los 36 coeficientes en variables y la suma `a + b` hecha una vez para `u` y `w`, y la parábola de afinado (`Afina`, cuatro veces por muestra) escrita en su sitio: las mismas sumas en el mismo orden y las mismas operaciones. La referencia es la propia rocola: `Empuja` (muestra a muestra) conserva el bucle y la llamada de siempre, y `Bloque` tiene que dar lo MISMO bit a bit en 10 señales (fuerte, bajita, que alterna, a tope en fs/2, el caso de libro con las muestras en 0,707 y la onda en 1, silencio, ceros con signo, desnormales, NaN e infinitos) × 3 techos × mono / estéreo / 6 canales × 5 troceos (de 1 a 4.410 cuadros, con el estado pasando de una llamada a la siguiente) y en el vaciado del final (datos nulos). Mutando una línea del código nuevo la batería falla (probado). |
| `indicemp3` | el índice de cuadros de los MP3, sin la basura de NAudio (22-10-2026; `LectorMp3Ligero.Abre`, `Mp3Ligero.cs`): al abrir un MP3, `Mp3FileReader` recorre el archivo entero y crea por cada cuadro un `Mp3Frame`, un arreglo de 4 bytes y un `Mp3Index` (1,3 MB de basura y ~10 ms por canción de 200 s; el repaso abre cada canción dos veces). `Abre` repite el constructor de NAudio paso a paso (etiqueta ID3v2, los dos primeros cuadros, Xing, ID3v1, el `Mp3WaveFormat` con la tasa de bits media, el descompresor) y cuenta el índice aparte en dos arreglos de enteros (dónde empieza cada cuadro y cuántas muestras había antes), validando cada cabecera con la función de NAudio. Con un descompresor imitado que apunta todo lo que recibe, ~50 archivos (los de la carpeta: mono, 48 kHz, MPEG-2, VBR...; con carátula ID3v2, basura entre cuadros con y sin 0xFF, al principio y al final, cortados, ID3v1, tres y cuarenta veces seguido, dos frecuencias, diminutos, vacíos y sin cuadros) abiertos con NAudio y con `Abre`: el mismo resultado o la misma excepción, el mismo formato dado al descompresor campo a campo, las mismas etiquetas, `Length`, `TotalTime`, EL MISMO ÍNDICE (el `tableOfContents` de NAudio leído por dentro contra los dos arreglos), 45 saltos a muestras exactas por archivo (bordes de cuadro, +1, +2, la última, el final), los mismos bytes con lecturas y saltos raros, y el descompresor recibe lo mismo en el mismo orden. Lo que no es corriente (cambia la frecuencia o los canales a mitad: NAudio lo rechaza) cae al constructor de siempre y contesta lo mismo. Abrir una canción larga deja menos de la cuarta parte de la basura. `mp3ligero`, `reproligera` y `enmemoria` miran ahora que la medida y la reproducción abran por `LectorMp3Ligero.Abre`. Mutando una línea del código nuevo la batería falla (probado con cuatro). |
| `eqporbandas` | la primera pasada del nivelado por bandas cuando nada cambia (22-10-2026; `LevelTap.PrimeraPasadaQuieta` y `BandasDelTrozo`, `Realce.cs`): `LevelTap` aplicaba la ganancia, el EQ automático (hasta 8 bandas en cascada, 7,8 ns por banda y muestra) y la mezcla del realce muestra a muestra, con cuatro cosas por cuadro (las rampas, el cruce de EQ, la limpieza de restos cada 64 cuadros). Casi siempre nada cambia; entonces el trozo de 512 cuadros se hace por bandas: la ganancia y la mezcla del realce como constantes y cada par de bandas del EQ de punta a punta del trozo, con su memoria y coeficientes en variables y los dos canales de un estéreo a la vez (la salida de una banda es la entrada de la siguiente para esa muestra, en una variable `float`: se redondea igual que al guardarla en el arreglo). La cuenta de cada muestra es la misma, en el mismo orden, y la limpieza cae en los mismos cuadros (el trozo se parte donde toca limpiar). Si algo cambia (una rampa, un cruce, otro EQ, un trozo que empieza a mitad de un cuadro) se hace cuadro a cuadro, como antes. Se compara el `LevelTap` de verdad consigo mismo con el gancho `SinBandas` (true = siempre cuadro a cuadro): 1, 2, 3 y 6 canales (el LFE sin EQ), 44,1 y 48 kHz, EQ de 8, 5, 3, 1 y 0 bandas activas y sin curva, cambios de curva (cruce de 30 ms), de ganancia (rampa de 20 ms) y de realce a media canción, un silencio de ceros exactos, un trozo de -180 dB y lecturas de todos los tamaños (muchas parten un cuadro): la salida de 6 s y TODO el estado interno (memoria del EQ y del realce, rampas, cruce, cuenta de la limpieza) iguales bit a bit; y que casi todos los trozos de una canción corriente van por bandas. Mutando una línea del código nuevo la batería falla (probado con siete). |
| `reproligera` | la reproducción desde la memoria con el lector ligero de MP3 (19-10-2026; `Mp3Ligero.cs` lo usaba solo el repaso): con el descompresor de verdad de la coma flotante (NLayer) y leyendo como la reproducción (`SampleChannel`, bloques de 4.096 muestras), el mismo PCM muestra a muestra que con el `Mp3FileReader` de NAudio en 27 MP3 de pruebas (unos 100 millones de muestras): cancion entera, saltos (a mitad, hacia atrás, al principio, casi al final, por tiempo), reanudar con otro lector, relevo sin pausa (la siguiente ya abierta y la primera cerrada) y la pantalla preguntando `Position`/`TotalTime`/`Length` desde otro hilo; mismas posiciones y duración; un MP3 roto falla igual con los dos. Mide lo asignado (con NLayer casi todo es de NLayer: el ahorro del lector, 98 %, se mide con `mp3ligero`) y vigila el enganche (ligero si `Posible`, el de siempre si no; `enmemoria` vigila el resto de `AbreMp3`) |
| `hilosatrapados` | hilos de fondo que no cierran la rocola (19-10-2026): en CLR 2 una excepción sin atrapar en un hilo cierra el programa. `CoverPump` (lector de carátulas) lleva un try/catch/finally de punta a punta: con un formulario sin armar al que le faltan sus colas el hilo termina por su cuenta, apunta el fallo, baja `coverPumpCount` (se podrá armar otro lector) y deja la carátula en curso para reintentar; `ClavesDelIndice` atrapa el fallo de un ayudante y rehace su tramo en el hilo que reparte (una entrada nula en el índice: el fallo llega a quien reparte y los tramos sanos quedan hechos). Con la rocola de antes el proceso muere con la excepción sin atrapar |
| `guardadocortado` | un guardado cortado entre sus dos pasos (donde no hay `File.Replace`: FAT32, o el archivo bloqueado por un antivirus) deja el archivo bueno solo como `.bak`: al arrancar, antes de leer nada, vuelve de su copia (volumen, EQ, ajustes, teclas, biblioteca...), la copia no se toca, lo que sí estaba tampoco, se lee entero y guardar después no pierde lo de antes; el estado (créditos y cola) no entra; queda apuntado en `rocola_error.txt` |
| `nofinitos` | lo que no es un número (NaN o infinito) no entra en las medidas ni en las tablas: un WAV en coma flotante con muestras rotas se mide igual que con esas muestras en silencio (antes salía NaN y envenenaba la media del EQ de toda la biblioteca); `PublicaMedida`, `CierraHuella` y `CierraSonoridad` no guardan NaN ni infinitos y una medida rota no cuenta como ya medida; al leer volumen, EQ y duraciones (arranque y respaldo) las filas rotas no entran y la media del EQ sale finita |
| `bitacora` | lo que tarda, en la bitácora (`MedidasBitacora.cs`): con `diagnostico=1`, las tablas del arranque (leerlas y lo que las espera la ventana), la biblioteca (y comprobar la guardada) y cada canción hasta que suena quedan en `rocola_diagnostico.txt`; sin eso no se escribe nada; y está enganchado al publicar las tablas, al acabar la biblioteca y cuando la canción ya suena |
| `enmemoria` | la canción que suena y la siguiente, en memoria (CancionEnMemoria.cs): desde la memoria el lector entrega lo mismo que desde el archivo (también saltando) y el AbreMp3 de memoria es el de siempre letra por letra y compilado igual; lo leído por adelantado se usa una sola vez y no si el archivo cambió o es otro; ni lo que no es MP3 ni lo que pasa de 32 MB; con el archivo vaciado a mitad de canción sigue entera; y el enganche (fuente, lectura por adelantado, soltar) |
| `bloqueos` | *(con el perfil de tarjeta gráfica, que es el programa entero)* lee el programa compilado y, desde todo lo que corre en la pantalla (temporizadores, teclas, dibujo, eventos y lo que se le manda con BeginInvoke), sigue las llamadas sin cruzar a hilos de fondo hasta lo que puede esperar: disco, bajar al disco, esperas, lanzar programas, imágenes, abrir o medir canciones. Cada sitio aceptado está en su lista con el motivo (53 en HD, 39 en Modest); uno nuevo hace fallar la batería enseñando el camino desde la pantalla, y uno de la lista que ya no ocurre también. Con la rocola de antes de estas mejoras señala exactamente lo que se arregló: guardar el estado, la bitácora, las incompatibles, borrar el estado y la espera del arranque |
| `etiquetadelante` | la etiqueta ID3 de la siguiente canción (título, artista, disco y género, que da el color del fondo) se lee en la lectura por adelantado, de los mismos bytes, y no al empezarla en la pantalla: el mismo resultado campo por campo en nueve tipos de etiqueta (v2.3, v2.4, v1, mezcla, sin etiqueta, con escape, truncada, un WAV y la extensión en mayúsculas); al empezar no se abre el archivo (comprobado con el archivo bloqueado); nunca una etiqueta vieja (archivo cambiado por tamaño o por fecha, canción cambiada, otra canción); la lectura por adelantado de verdad la deja lista, y lo que cuesta cada camino |
| `ventanareal` | la rocola entera con su ventana (Xvfb), una biblioteca generada (121 discos) y tablas de arranque de 40.000 canciones escritas por la propia rocola, usada con un guion: pedir canciones, pasar páginas, entrar a un disco, abrir el buscador, saltar y pedir seis seguidas, con una salida de sonido que pide como WaveOut; un hilo aparte cronometra cuánto tarda la pantalla en atender. Falla si se para más de 500 ms al arrancar o más de 150 ms usándola (medido aquí: 170-220 y 20-30 ms; con la rocola de antes del arranque sin esperar las tablas, 650 ms y falla), si no suenan al menos 5 canciones o si hay excepciones en la pantalla. Tarda cerca de un minuto |
| `vigiapantalla` | el vigía de la pantalla dentro de la rocola: con una ventana de verdad parada a propósito dice cuánto duró (420 ms) y qué la paró, y lo corto (80 ms) no lo dice; una parada sin marca se dice como «después de» lo último marcado; si no vuelve en 10 s lo dice sin esperar; parado el vigía no dice nada; la rocola apunta 150 ms o más en la bitácora del diagnóstico y 500 ms o más siempre en los errores, como mucho uno cada 10 s y diciendo cuántos se callaron; se arranca al mostrarse, se para al cerrar, y las entradas grandes (teclas, latido, cuadro de la tarjeta, empezar canción, publicar la biblioteca, arranque) marcan lo que hacen; y cuánto cuesta marcar |
| `arranquevivo` | al arrancar, la ventana no se queda parada esperando las tablas (con 40.000 canciones eran 0,8-1,1 s sin pintar ni atender): con el lector aún leyendo, mostrarse vuelve al momento y deja apuntado qué seguir; publicar las tablas no espera nunca (con la rocola de antes se queda colgada); el lector avisa una vez, desde su hilo y fuera de su candado (con una tubería como tabla lenta); las teclas de mientras se guardan y se atienden al final, en orden, y salir funciona siempre; el latido no anima nada mientras tanto; el orden sigue siendo tablas, historial y biblioteca; y cerrar mientras tanto no sigue con nada |
| `bitacoradetras` | la bitácora (`rocola_diagnostico.txt`, `rocola_error.txt`, `rocola_incompatibles.txt`) la escribe un hilo de fondo: nadie que apunte abre el archivo por su cuenta (diagnóstico, errores, incompatibles, relleno, decodificador, fallo grave); con el archivo ocupado un momento apuntar vuelve al momento y las 200 líneas llegan después y en orden (con la rocola de antes se perdían); la hora, el contexto, el sello de versión y el tope de tamaño no cambian; un fallo grave se escribe antes de volver, con lo pendiente; y cuánto cuesta apuntar en la pantalla |
| `estadodetras` | los créditos y la cola se escriben en un hilo de fondo (decisión del 29/09/2026): con el disco ocupado, guardar vuelve al momento (con la rocola de antes se queda esperando); al soltarse queda lo último, con los mismos bytes y la copia de lo anterior; un encargo viejo no resucita un estado borrado a propósito (25 vueltas con el disco ocupado); restaurar ve lo encargado aunque aún no esté en el disco; al cerrar sin borrar se espera al último encargo; y cuánto cuesta guardar en la pantalla |
| `tirones` | lo que paraba la pantalla al poner una canción, medido con 20.000 canciones en las tablas: el primer segundo de música lo hace el hilo de la música por su cuenta y avisa (pedirlo no espera, después la salida solo copia, sale lo mismo muestra a muestra, sonando el colchón vuelve a 200 ms, y avisa también si la canción se acaba o falla antes); la rocola arranca la salida cuando el aviso llega a la pantalla y no si mientras tanto se saltó la canción; lo de después, lo de siempre y en el mismo orden; guardar la tabla de volumen no rehace su índice en la pantalla; y el techo de "Subir TODO" con medidas nuevas se cuenta en otro hilo y da lo mismo que en la pantalla |
| `bomba` | la música en su propio hilo (BombaSonido.cs): sale lo mismo bit a bit (también la cadena de verdad con todo encendido), la cadena la lee otro hilo con prioridad por encima de la pantalla, el final y los fallos llegan como antes, un hilo atrasado se espera como mucho 600 ms y después silencio (nunca 0), al parar el hilo se va antes de soltar el archivo, el pulso y el relevo suman lo que lee por delante, y lo que le cuesta a la pantalla cada bloque; y con música de verdad por la medida, la bomba y el ritmo de WaveOut, el pulso que desliza y hace latir las estrellas y la luna le llega a la pantalla vuelta tras vuelta (antes, con la cola de medidas corta, no le llegaba nunca) Sección 10 (22-10-2026): la caja negra del sonido, una línea «[Cortes de sonido]» en `rocola_error.txt` al acabar o saltar una canción con cortes o avisos tardíos (y nada si no hubo), sin repetir lo ya apuntado. |
| `discoparejo` | el nivel por disco: en racha (la de antes o la de después del mismo disco) la misma ganancia para todas, la del disco entero; sin racha, la de siempre; videos y discos de otra carpeta no hacen racha; el disco por su carpeta (también desde "lo más sonado"); los picos de su pista más fuerte; sin bastantes medidas, canción por canción; enchufado en la cadena, la siguiente preparada y el ajuste en vivo Desde el 22-10-2026, los recopilatorios parejos: con más de 5 dB entre la pista más fuerte y la más baja (las de menos de 30 s no cuentan), cada canción la suya; 5 dB justos siguen siendo disco. |
| `salalocal` | el local y sus bocinas (SalaLocal.cs): todo apagado sale bit a bit; claridad, partes suaves y balance contra su teoría, sin golpe al encenderlos, disco continuo, sin desnormales ni memoria reservada; la prueba de bocinas; y, desde el 22-10-2026 (sección 8), los **graves y agudos del local** (escalones de 120 Hz y 6 kHz de −6 a +6 dB: la respuesta contra la teoría a cinco frecuencias, de 1 en 1, bit a bit a 0 dB, sin chasquido al encender / dar la vuelta / apagar, la rampa de 50 ms, disco continuo, lecturas de una muestra, «Dejar como de fábrica» y la configuración) |
| `presenciasala` | la presencia en sala ruidosa (PresenciaSala.cs, 22-10-2026): apagada sale bit a bit (1, 2 y 6 canales); el compresor suave 2 a 1 contra su teoría (el umbral a 6 dB bajo el objetivo, el Master no cuenta), ataque y suelta, una sola ganancia para los dos canales, la compensación que no pasa del techo del limitador, sin chasquido al encender y apagar, disco continuo, lecturas de cualquier tamaño, sin memoria reservada, el coste, la cadena, la configuración y la ventana; y que no llama a nada de volumen |
| `localauto` | el local en automático (LocalAuto.cs, 23-10-2026): la tabla por tipo de local (billar, sala buena, bocinas chicas) y rango de la canción (P99 − sonoridad integrada; umbrales 12 y 16 dB), apagado deja los mandos tal cual, encendido decide por cada canción con su medida real (sin medida o de algoritmo viejo: solo lo fijo del tipo), lo guardado en los mandos se conserva, la cadena, los mandos en vivo y «Escuchar sin mejoras» leen `LocalDe`, la configuración y la ventana (interruptor, tipo y siete mandos en gris con su motivo); desde el 23-10-2026 las partes suaves van siempre en el billar y las bocinas chicas (P1) y la presencia del billar y las partes suaves de la sala siguen el rango de sonoridad (LRA, 10 y 14 LU) con la regla de antes para las medidas sin LRA (P2) |
| `mp3mezclado` | MP3 con frecuencia o canales mezclados (error del 17-10-2026, «Got a frame at sample rate 32000, in an MP3 with sample rate 44100»): con dos archivos de verdad NAudio rechaza el original y la reconstrucción (`BuildCleanMp3`) deja solo el formato dominante (frecuencia **y canales**) y se abre; sin cuadros da null; y si la reconstrucción tampoco puede, se escribe por qué |
| `lra` | el rango de sonoridad (LRA, EBU Tech 3342) en la medida del repaso (`Sonoridad.cs`, algoritmo 4, 23-10-2026): los cuatro casos de la norma con sus +-1 LU, que da lo mismo se lea como se lea, contra la referencia independiente `ref_lra.py` (estéreo, mono, 5.0, 32 a 96 kHz), el formato de `rocola_sonoridad.txt` (lo de antes se lee igual, lo posterior se conserva, lo roto se salta), que las medidas de antes siguen nivelando igual y el repaso las vuelve a pedir, la medida de un archivo real (una de menos de 3 s no trae LRA) y lo que cuesta. Con `ROCOLA_EXE_ANTERIOR=<ejecutable de antes>` compara el coste y comprueba que ese ejecutable lee el archivo nuevo sin perder nada |
| `modosauto` | los modos automáticos del sonido (23-10-2026, `MODOS_AUTOMATICOS_SONIDO_MODEST_2026-10-23.md`): la configuración de videos, EQ, graves, S3 y el local (guardar = leer, valores fuera de rango), los grises de la ventana de verdad (S3 con el modo del local, la barra de los videos cuando el automático no elige nada —en XP, siempre—, «a la medida» sin el modelo del EQ), «Escuchar sin mejoras» en la canción que se arma mientras dura, el reparto de los rangos de la biblioteca (`rocola_calidad.txt`) y unas medidas sintéticas para las propuestas del documento (solo informan) |
| `adelantoprioridad` | la siguiente canción pasa antes que las carátulas con 45 s o menos (22-10-2026): la regla, el reloj de la canción, el hilo calentador de verdad con un archivo en disco (con tiempo espera 1,5 s y a las carátulas; a 45 s del final no, y rompe una espera ya empezada) y el cableado |
| `picomuestras` | el limitador de pico real con lecturas de menos de un cuadro (22-10-2026): pedir de a una muestra, a tamaños raros y con el origen partido da, bit a bit, lo mismo que pedir todo junto con 1, 2 y 6 canales, sin ceros antes del final (con la versión anterior falla) |
| `todojunto` | todo el sonido a la vez en la cadena de verdad (OpenAudio + ArmaCadena): nivelado, Subir TODO, realce, DSEE, grave sintético, grave en mono, des-recorte, modo noche, fundido y saltar silencios; y cambiándolo en vivo cada 0,4 s como desde Ctrl+F3. Nada roto ni por encima del techo, sin chasquidos ni muestras perdidas, la cadena siempre como dicen los ajustes, cuánto trabaja el limitador y lo que cuesta |
| `silencios` | saltar los silencios largos (SinSilencios.cs): dónde suena cada canción sacado de la medida entera (bloques de 100 ms, −60 dBFS, el bloque a medias del final, mono y estéreo), la tabla de algoritmo 3 (ida y vuelta y lo que se descarta), qué se salta y qué no (2 s en los extremos, 5 s en medio, márgenes y fundido), el recorte muestra a muestra (lecturas de cualquier tamaño, saltos del lector, un salto que falla, retomada), un WAV medido y abierto de verdad con el interruptor encendido y apagado, lo guardado y los enganches (también que Ctrl+F3, al cambiarlo, retire lo ya abierto para la siguiente) |
| `soltar` | que el limitador espere 20 ms antes de soltar: un bajo sostenido por encima del techo sale limpio a 40, 60, 100 y 200 Hz, un golpe suelto vuelve pronto, en canciones de prueba limita y bombea casi lo mismo que antes, lo de debajo del techo intacto y nada por encima |
| `noche` | el modo noche: el cruce de la medianoche, que los vídeos también bajen, la rampa por reloj, que de día no cambie nada y, en la sección 8, los graves y agudos que devuelve al bajar el volumen, medidos contra la norma ISO 226 calculada en la batería, sin saltos, bit a bit de día y al amanecer, disco continuo, configuración y cableado |
| `gravemono` | el grave en mono: la respuesta contra la teoría del cruce, lo mono intacto, un instrumento abierto que sigue a su lado por encima del corte, un canal al revés que no pierde el grave (y que lo normal no lo activa), lo que no se toca, cualquier forma de leer bit a bit igual, el disco continuo, la cadena de verdad, la configuración y el coste; y, desde el 23-10-2026 (P6), encender o apagar en vivo el estéreo estrecho y el subgrave sin chasquido (rampa de 25 ms; el estado estable queda bit a bit igual) |
| `limite` | que el limitador BAJE la ganancia en vez de recortar: distorsión de un tono fuerte y aplanado de la punta en un golpe seco |
| `eqauto`, `eqia`, `calidad` | el EQ automático y su objetivo por canción |
| `duracion`, `titulo`, `etiqueta`, `genero` | lo que se lee de cada archivo |
| `colores` | que ningún género musical se quede sin color: los que trae un ID3 y los que se leen del nombre de la carpeta, en español y con acentos; que el color sea el mismo se pregunte por donde se pregunte; y que no haya dos que no se puedan distinguir |
| `mudos` | *(sólo en la rama de tarjeta gráfica)* la limpieza de videos que no suenan. Lo que vigila no es que borre, es que NO BORRE DE MÁS: uno sin medir no puede aparecer nunca, y si Windows no puede mandarlos a la papelera la biblioteca se queda exactamente como estaba |
| `videos` | que los videos entren en el repaso que los mide, y que suenen todos parejo: el reproductor de Windows sólo sabe BAJAR, así que se comprueba con qué volumen quedaría cada uno y cuánta diferencia queda entre el que más suena y el que menos |
| `sinpausa` | que la canción siguiente entre por la misma salida sin hueco ni cebado, sin cobrar dos veces ni repetir o saltar ninguna, y que cola, créditos e historial queden como antes, también cuando algo falla a mitad del relevo; y que al cambiar el interruptor de saltar silencios se retire la siguiente ya armada o abierta (no la que ya entró) y se vuelva a armar igual |
| `salida` | la salida al aparato: coma flotante en Vista o posterior y 16 bits con un solo dither si no; que un fallo del APARATO (desenchufado, tomado por otro programa) no gaste canciones ni créditos y se retome la misma donde iba; el vigía de la salida muda; el cambio de aparato; y que la canción de la medida de rendimiento salga callada sin tocar ningún volumen de Windows |
| `videoaudio` | las reglas del sonido de los vídeos: nunca más de 100 en el reproductor, emparejado con las canciones dentro de sus límites, y de noche solo baja |

**Pantalla**

| batería | qué comprueba |
|---|---|
| `letrero` | que ningún texto salga cortado ni se calle teniendo sitio |
| `hojea`, `pagina` | pasar de página: que lo horneado no se quede viejo y que ninguna página cueste el triple |
| `busca`, `buscaigual`, `busmov`, `parcial` | el buscador: que rinda y que se vea igual que antes |
| `mover` | que moverse entre las **teclas** del buscador NO rehaga ni vuelva a subir la pantalla entera: la tecla elegida va como pieza suelta con su letra dentro, sin mover el lienzo (eso la dejaba vacía en Windows), y no existe cuando el teclado no se ve. Y lo que SÍ tiene que seguir rehaciendo la pantalla: pasar a los resultados y moverse por ellos. Mantener pulsada una flecha tiene tope |
| `bolsa` | la primera letra tecleada, que es la única cara: que guardarla dé **exactamente** la misma lista y en el mismo orden, que se vacíe al recargar la música, y cuánto ahorra de verdad |
| `abc`, `neon`, `espacio`, `barrido2` | la barra de letras y los efectos |
| `vacias` | que las letras sin discos detrás no se dibujen, y que las que quedan salgan más grandes |
| `franja` | la franja de abajo: que al sonar una canción se vea de qué género es, y que los créditos y la cola se lean de lejos |
| `estrellas` | que el fondo tenga las cuatro siluetas de estrella, que el color fuerte se lo queden las grandes y que dibujarlo no cueste más |
| `estrella3d` | *(sólo con tarjeta gráfica)* la estrella grande en 3D: que el sólido esté cerrado y de frente sea el icono, que gire igual a cualquier velocidad de cuadros y cambie de velocidad sin tirones, la luz por facetas, la estela de copias separadas por lo recorrido (no por tiempo) que nacen y se van sin saltos, los saltitos (el bucle de dibujo acompasado al refresco frente al compositor de Windows simulado: antes, la primera idea y ahora; la cuenta de Windows y su ausencia, como en XP), el interruptor, que dibujarla no reserve memoria y que el dibujo por procesador no la use |
| `pantallaviva` | la pantalla viva, tanda 1 (sobre todo con tarjeta gráfica): la portada enfocada (crece desde su borde de abajo, se apagan las demás, el halo toma su color y sin enfocar sale bit a bit el de antes), su letrero (qué dice y que no se sale de su casilla ni tapa el nombre), la ficha rápida (cuándo sale y se va, dónde se pone, las más pedidas, el recorrido entero con una rocola armada sin encender), pasar de página deslizando (doce columnas en fila que nunca se pisan, simulado milisegundo a milisegundo, la vieja sin hornear ni pedir portadas, el empujón del cielo), la lupa de letras (y que el dibujo por procesador sale idéntico), la pestaña Pantalla de Ctrl+F3 con sus 16 combinaciones, y lo que cuesta cada cosa. Sin tarjeta gráfica, solo la pestaña |
| `discovivo` | la pantalla viva, tanda 2, dentro del disco (sobre todo con tarjeta gráfica): que la lista del disco y la cápsula de la cola pintadas por el procesador salen idénticas píxel a píxel a las del programa sin tarjeta, y vuelven a la de siempre en cuanto las pinta el procesador; la lista grande (nueve filas, su letra, el circulito y la duración, y el género en toda la página o en ninguna fila); los minutos de la cola (la misma cuenta que el aviso de tu turno, sin inventar, se rehace solo si cambia un minuto, manda el título); el ecualizador (su hueco en la pastilla, dónde van las barras y cuándo no salen); el vinilo (su dibujo, cuánto asoma sin tocar la lista, el cubo que se aparta, entrada y salida sin saltos, 33 ⅓ vueltas, preparado una vez por canción); el boleto (sale calcado encima de su fila, vuela en arco y cae dentro de la cápsula) y la cápsula (el número de antes hasta que llega, luego salta y rueda); la tarjeta de Ctrl+F3 con sus 32 combinaciones; y lo que cuesta cada cosa. Sin tarjeta gráfica, solo la pestaña |
| `suenavivo` | la pantalla viva, tanda 3, lo que suena y los créditos (sobre todo con tarjeta gráfica): que la cápsula de créditos con una moneda entrando, pintada por el procesador, sale idéntica píxel a píxel a la del programa sin tarjeta, y que el panel de reposo con la idea apagada es el de siempre; los avisos (su tipo sale del color, los dos renglones, "Pusiste «X»" y cuánto falta, cuánto duran, que entran y se van sin saltos, y que el de siempre no sale a la vez); la tarjeta de sin créditos (no pone nada en la cola, se va sola o al llegar créditos, no vuelve a entrar si se insiste, no sale en atracción ni con vídeo); la moneda (se entera sola, cae acelerando y girando, aterriza en el número cuando rueda, el "+N créditos" de verdad, también con créditos de servicio, y el letrero de arriba solo cuando la moneda no se ve); el panel en grande (el vinilo, el título en una línea que se desliza sin saltos y se hornea una vez, cuánto falta, la pastilla del género sin tapar al artista, los minutos de la cola sin inventar, la insignia corrida); la hoja nueva de Ctrl+F3 con sus 16 combinaciones y el botón de fábrica; y lo que cuesta cada cosa. Sin tarjeta gráfica, que los enganches no hacen nada y la pestaña |
| `atraccion` | la pantalla viva, tanda 4, el modo atracción (sobre todo con tarjeta gráfica): que con las ideas apagadas la atracción es la de siempre (las mismas portadas con el mismo ciclo que el cubo de siempre, el cubo en su sitio girando y sus dos textos); la cuenta de la noche (desde las 8 de la mañana, solo canciones, acotada, el orden, y su archivo rocola_noche.txt: se guarda solo si cambió, se lee igual, el de otra noche no vale, las líneas rotas se saltan y si falta se lee la copia; al pedir, cada minuto y al cerrar); el escaparate (el cubo y el texto sin salirse en cinco pantallas, se mece sin girar, cada disco el doble de tiempo, la ficha del disco que enseña el cubo y el cambio en dos tiempos, lo que lleva la ficha, los mensajes y su respiro, una cosa horneada por vuelta); lo más pedido (cada 30 s durante 15 con un paso suave, el cubo a la esquina sin cruzarse, las filas una a una y sus barras en proporción, hacen falta tres canciones que sigan en la biblioteca, la lista horneada, y sin escaparate los textos de siempre se apagan); los enganches; la tercera hoja de Ctrl+F3 con sus 4 combinaciones; y lo que cuesta. Sin tarjeta gráfica, que no se cuenta nada y la pestaña |
| `estilo` | la pantalla viva, tanda 5, el estilo general (sobre todo con tarjeta gráfica): que pintar la franja de lejos no toca las etiquetas de verdad (ni su letra, ni su sitio, ni su color) y que el acomodo de la ventana no sabe nada de ella; apagada, la franja se hornea como siempre; el título crece (entre 1,45 y 1,15, sin salirse de su renglón), el artista baja casi un renglón (lo que deja el reloj) con su pastilla y su letrero (y el brillo del letrero con él), y lo que sigue pasa a ese renglón, a la derecha, detrás de lo que lleva y en un gris claro (medido en píxeles); con textos largos, el título y lo que sigue terminan en puntos suspensivos sin salirse de su sitio; la tarjeta «A CONTINUACIÓN» a 1024 de ancho: donde ya cabía, en el mismo sitio; con el letrero MEJORADA, la tarjeta baja a la altura del título con el artista y su letrero por debajo (sin pisarla ni cortarse); sin créditos, algo más a la izquierda sin quitarle al título sus 240 px; en pantallas estrechas, su renglón de reserva con el título visible; el resplandor del género (centrado, de su color, respirando cada 5 s sin saltos, detrás de las portadas y solo con la idea encendida), que la tarjeta se entera del color y del interruptor, y el pulso de la letra con el color de su género; la tarjeta ESTILO GENERAL de Ctrl+F3 con sus 4 combinaciones; y lo que cuesta. Sin tarjeta gráfica, solo la pestaña |
| `dibujoligero` | *(sólo con tarjeta gráfica)* el dibujo más ligero (DibujoLigeroGpu.cs), con una tarjeta imitada y un raster de referencia con las reglas de Direct3D 9: que el cuadro no borra y lo primero que llega es el cielo opaco, que tapa cada píxel una vez (5 pantallas × 5 cortes del cielo); la regla de los trozos (ninguno con algo, ni a un texel, se salta; ninguno vacío se pinta; ninguno dos veces; el borde siempre si la imagen se estira) en 1.440 imágenes hechas a propósito y en las de verdad; la misma imagen entera y a trozos (la interfaz y la franja idénticas; la vía láctea y la viñeta, como mucho un nivel de 255 en unos pocos píxeles por redondeo); que la interfaz se trocea solo tras 300 ms quieta; que la aurora con y sin lo guardado manda los mismos vértices byte a byte en 360 cuadros y que lo guardado se rehace al cambiar cada una de sus 14 dependencias; que las cuentas de color por entero dan los mismos números; y un cuadro de verdad (RenderFrame): la interfaz y la franja se pintan donde siempre, igual enteras que a trozos, y no se mandan cuando no se ven. En Linux carga una copia de la rocola con un .config que lleva RtlMoveMemory a memmove, y la borra al acabar |
| `buscaligero` | *(sólo con tarjeta gráfica)* el buscador más ligero para la tarjeta (BuscadorLigeroGpu.cs), con la tarjeta imitada de `dibujoligero`: que fuera del trozo que se sube no cambia ni un píxel en 54 cambios (posar consultas, quedarse sin resultados, avisos que entran, salen o cambian, marcas de la cola, géneros que llegan, canción pedida con su aviso; con el teclado y con los resultados enfocados; tres pantallas); que lo que cambia media pantalla (enfocar los resultados, que vuelva o se vaya el cubo, otro color u otro tamaño) sube la imagen entera; que la tarjeta recibe justo ese trozo y su textura queda idéntica a la imagen, y que sin la versión de justo antes se sube entera; que al cerrar la ventana que se encoge recibe los mismos vértices y la misma imagen sin copia; y que al abrir se reutiliza la textura grande sin crear ninguna, pero nunca mientras la ventana se sigue dibujando |
| `buscaportadas` | la pantalla viva, tanda 6, el buscador con portadas (sobre todo con tarjeta gráfica): el reparto en seis pantallas, de 800×600 a 1920×1080 (todo cabe y nada se pisa, teclas del mismo tamaño y compactas, tarjetas en dos columnas, los atajos que no caben se quedan fuera); 13.500 pasos al azar entre atajos, teclado y tarjetas (la selección siempre vale, se ve y tiene su imagen chica) y los pasos concretos fila por fila; el verde y el dedo en teclas, atajos y tarjetas; que la imagen grande sale igual píxel a píxel con otra consulta, otros resultados o la selección en otro sitio; que fuera de las tarjetas su imagen es transparente (por tarjetas = entera) y que lo elegido tapa entero lo de debajo; la entrada una tras otra, las que no cambian quietas y al bajar ya puestas; los atajos con datos de verdad (lo más pedido hoy, videos, karaoke, "Buscaste" sin repetir, de esta noche y nunca la clave de servicio) y los números de pista fuera de la tarjeta; las portadas (solo con la vista quieta, solo las que se ven, una vez, juntando las que llegan); con la tarjeta imitada, las tarjetas en un envío por trozos, antes que la elegida y con su transparencia, y con la ventana del buscador (al abrir suben con ella, al cerrar bajan con sus texturas sin copiar nada y al acabar se sueltan); lo quieto horneado (pegar = dibujar píxel a píxel, moverse y teclear no hornean, tope de 16, otro color no reutiliza); la tarjeta elegida con los títulos deslizantes (lo que no cabe va en su cinta y no quieto, apagado o si cabe quieto, en la tarjeta imitada encima de la elegida y con su transparencia, y con la ventana, también con el buscador de siempre); la tarjeta de Ctrl+F3 y los enganches. Sin tarjeta gráfica, que el buscador no tiene nada del nuevo, la pestaña y la preferencia |
| `sala` | dentro de un disco: las marcas de "sonando" y "en cola" |
| `fondo`, `anim`, `presencia`, `barrido`, `tope`, `calma` | la aurora y el fondo reactivo (sólo con tarjeta gráfica) |
| `aurorafina` | *(sólo con tarjeta gráfica)* la aurora a la cadencia de la pantalla (AuroraFinaGpu.cs): la misma canción por el latido de antes (24 por segundo) y por el cuadro a cuadro, a 60 cuadros por segundo; cuántas veces cambia la forma y el salto más grande, la altura media y la forma (sube como mucho 0,08, no baja), igual a 30, 60 y 120 cuadros por segundo, el golpe a la hora, medidas cada 1/60 s con ventana de 50 ms, el latido bit a bit como antes, la cola acotada, lo mudo, la reserva y el coste |

**Recursos**

| batería | qué comprueba |
|---|---|
| `sobras` | que un cuadro en reposo no reserve ni un byte de memoria |
| `gasto` | que la caché de carátulas no crezca, que lo expulsado se libere de verdad, y lo que ocupa una biblioteca de 50.000 canciones |
| `historial` | cuántas páginas de la pared de discos se guardan, que el cajón respete su tope, y que volver a una guardada sea mucho más barato |
| `carpetas` | que se le pueda decir "la música está en ESTE disco": que el disco entero se pueda elegir, que mande la carpeta de fuera, y que el barrido no se quede dando vueltas en los atajos ni entre en las carpetas del sistema |
| `miniaturas` | que las miniaturas de las portadas se guarden en el disco y no en la carpeta temporal (que el sistema limpia cada tanto), y que el barrido no toque las recientes |
| `pared` | que el cajón de portadas se ajuste a la máquina, y que con el grande se pueda hojear la pared entera y volver **sin tocar el disco ni una vez** |
| `usando` | la rocola **con música sonando y alguien pulsando flechas**, que es el momento que aprieta: que mover la selección repinte sólo las dos casillas que cambian y no la pared entera, que salga **idéntico** píxel a píxel, y que un cuadro navegando no deje sobras para el recolector |
| `resol` | que cambiar la resolución con la rocola abierta no la deje lenta |
| `primera` | que las animaciones vayan fluidas **desde la primera vez**: mide cada pantalla —el primer cuadro contra uno normal—, comprueba que la rocola precalienta sola en segundo plano las que al encender no se ven, que no calienta lo que se midió y no gana nada, y que precalentar no cambia ni un píxel |
| `disco`, `ssd` | disco duro contra SSD |
| `cosecha` | que el entrenamiento pasivo siga juntando material |
| `proyecto` | que el archivo con el que compila Visual Studio (`Rocola.csproj`) liste **exactamente** los mismos archivos y bibliotecas que compila la tanda, y que ninguno se quede fuera sin que nadie diga por qué |

**Herramientas de medida** (no son pruebas de pasa/falla; se corren a mano)

| archivo | para qué |
|---|---|
| `dseeperf.cs` | dónde se va el tiempo dentro del reconstructor de agudos, por partes |
| `modelo.cs` | lo que costaría un modelo de IA más grande, tamaño por tamaño |
| `memoria.cs` | dónde se va la memoria con una biblioteca grande |
| `cuadro.cs` | de qué se compone un cuadro de pantalla cuando no ha cambiado nada (el cielo incluido, que es la pieza más cara) |
| `vercielo.cs` | saca una imagen del cielo estrellado para poder mirarlo |
| `pantalla.cs` | dibuja la pantalla entera (1024x768) en un PNG, con los controles de verdad, para mirarla |
| `cielos.cs` | cuaderno de bocetos del fondo: dibuja nueve cielos -el de hoy y cada idea por separado- para poder elegir mirándolos, en `pruebas/cielos/` |
| `aspecto.cs` | si sacar el recuadro del resultado elegido de la imagen de pantalla completa cambiaría el aspecto: dibuja el buscador de las dos formas y compara píxel a píxel (9 casos, dos resoluciones: salen **0 píxeles** de diferencia) |
| `cachemem.cs` | lo que cuesta rehacer una carátula que se salió de la caché, y lo que costaría una caché mayor |
| `capacidad.py` | si un modelo de IA más grande predice mejor (entrena varios tamaños sobre los mismos datos) |
| `genmodelos.py` | genera modelos de varios tamaños para medirlos |

Los `diag*.cs` y `comp*.cs` son diagnósticos de un rato concreto: se dejan porque explican
cómo se cazó un fallo, pero no forman parte de la suite.

## Baterías nativas de capas y color (Windows)

Ejecutar `powershell -File pruebas/validar-capas.ps1` desde esta rama. Puede pasarse
`-OutputDirectory` con una carpeta temporal propia. No ejecuta la biblioteca real
ni sustituye archivos de los releases; necesita Direct3D 9 para las comprobaciones HAL.

- `capascolor`: 18 composiciones idénticas píxel a píxel; firmas de selección/datos,
  conservación del hojeo y coste de preparación; propiedad de texturas, reutilización
  durante la mezcla, cancelación al toque/cambio de contenido y limpieza al apagar.
- `cristalcola`: 1024 combinaciones de configuración y mandos sin solapamientos; 56 paneles entre 480 y 3440 px,
  miniaturas visibles y celdas sin cruces; transparencia y acceso a Fondo.
- `cubofila`, `respuesta`, `efectoslayout`: regresiones del cubo/tiempo/progreso,
  caché de portadas y mandos de Imagen.
- `reposounido`: 297 rejillas compactas/simétricas, 126 uniones entre la malla del
  horizonte y el borde de la barra; atenuación/restauración a 24–240 Hz y prueba
  HAL del alfa, posición y reutilización de texturas. Detecta el defecto de
  tarjetas gigantes al ejecutarse contra la versión anterior.

- `lunadetalle`: superficie determinista de 256 px, borde alfa, luz lateral,
  16 combinaciones de opciones guardadas/aplicadas a GPU, geometria y brillo del
  realce, cache entre cuadros/resoluciones, regreso al diseno anterior y liberacion
  HAL. Genera una vista de la textura (no una captura de la aplicacion con musica).

Estas baterías usan el runtime de cada rama. No equivalen a una prueba con música,
una medición de FPS en el equipo de destino ni una certificación de Windows XP.

## Eficiencia compartida (30-09-2026, Windows)

`powershell -File pruebas/validar-mejoras-20260929.ps1` compila GPU y CPU con
advertencias como errores, ejecuta las regresiones y las nuevas baterias:

- `eficiencia`: 34 comprobaciones de planes reducidos, bloqueos separados,
  senales HDD, caches acotadas, muestras de color identicas y registro incremental.
  Admite `eficiencia.exe Rocola-gpu.exe --dos-hilos` para limitar solo la prueba.
- `eficiencia2`: 32 comprobaciones de guardado por pendientes, errores/reintentos,
  caratula publicada antes del guardado y pin GPU acotado dentro de la escena.
- `verticesgpu`: 83 comprobaciones HAL reales, equivalencia de superficies entre
  UP/anillo, Reset, reutilizacion, seleccion adaptativa y liberacion.
- `atlasgpu`: 480 comprobaciones de casillas compactadas, texto identico,
  redondeo de caratulas acotado a un nivel por canal, precarga y cache entre paginas,
  siete resoluciones, cambio de portada, marcas y superficies HAL fraccionarias.
  Admite `atlasgpu.exe Rocola-gpu.exe --dos-hilos`.
- `cpucongelado` (manual, recibe ejecutables CPU anterior/nuevo): compara el IL de
  14 metodos del renderizador congelado. Se compila junto a `leercodigo.cs`; los dos
  ejecutables deben compilarse con las mismas opciones. Si los bytes difieren solo porque
  se corrieron los numeros de tabla (un campo o un texto nuevo en otro sitio), compara lo
  que nombra cada instruccion y lo dice.

El lanzador tambien incluye buscaligero, dibujoligero y aurorafina. No reemplaza
releases ni datos del usuario. Afinidad reducida no equivale a un procesador
antiguo; las mediciones sinteticas no certifican XP ni FPS en una biblioteca real.
Detalles: `EFICIENCIA_2026-09-30.md`.
La paginacion a resolucion alta se documenta en
`PAGINACION_GPU_ALTA_RESOLUCION_2026-09-30.md`.

## Sesion real opcional (30-09-2026)

`sesionreal.cs` es un instrumento de integracion y medicion, no una regresion
sintetica ni parte del lanzador predeterminado. Abre Form1 completo a la resolucion
de la pantalla, espera la biblioteca y llama al medidor integrado con una cancion
real muda. Empieza en C, no en una seccion que pudiera tener una sola pagina.
Despues mide seis cambios pausados con precarga, comprobando la portada en memoria,
el bitmap compuesto y la referencia exacta de la textura GPU subida.

Recibe `sesionreal.exe <Rocola.exe> <datos COPIADOS en TEMP>`; agregar
`--solo-paginas` omite el medidor de las cinco escenas. Compilar con los mismos
ensamblados System/Core/Drawing/Windows.Forms de las otras pruebas y el compilador
de cada rama. Situar el instrumento y NAudio.dll en la copia temporal; copiar tambien
los dos modelos pequenos `rocola_ia.bin` y `rocola_eq_ia.bin` para conservar inferencia.

La carpeta debe contener copias de las tablas `rocola_*.txt` y `rocola_miniaturas`,
con `dir=` apuntando a musica real de solo lectura e `ia_datos=0` en la CONFIGURACION
COPIADA. No copiar el material de entrenamiento gigante ni apuntar a datos originales.
El instrumento rechaza carpetas fuera de TEMP y recogida IA habilitada. No reanuda
la cola o cancion de la copia; no cambia volumen de Windows. Sus guardados, medidas
y errores van exclusivamente a esa copia. Cerrar otras sesiones de pruebas primero.

`presentaciones_s` cuenta Present aceptados por el controlador, no fotones/Hz.
`input_ms` es NextPage interno, no latencia de teclado fisico; `espera_texturas_ms`
termina al estar listas las texturas, no al acabar la animacion. La copia de miniaturas
y la precarga de un segundo hacen que esto NO sea una prueba de disco frio.
Resultados y limitaciones: `DESPLIEGUE_Y_SESION_REAL_2026-09-30.md`.

## La tanda en Linux tras los cambios del 29 y 30-09 (01-10-2026)

Ocho baterías quedaron en rojo con `todo.sh` (Mono). Lo que era del programa y lo que era de la prueba:

- `ssd` (programa y prueba): Ctrl+F3 dejó de mostrar los milisegundos medidos de cada unidad. Ahora se
  recuerda la mediana de cada unidad (que ya no se vuelve a medir) y vuelve a salir. La prueba olvida
  las unidades al empezar de cero y comprueba que una unidad conocida no se mide otra vez.
- `calienta` y `sonoridad`: lo medido pasa ahora por `PublicaMedidaDeFuente` y la espera por
  `EsperaPortadas`; las pruebas siguen un salto (`Codigo.ConUnSalto`).
- `leer`: `cpucongelado` lee el IL por `leercodigo.cs` (`Codigo.BytesIL`). Y como un campo nuevo
  corre los números de tabla del ejecutable, si los bytes difieren compara lo que nombra cada
  instrucción (`Codigo.IlConNombres`): con el campo nuevo de `ssd`, los 14 métodos del dibujo por
  procesador salen iguales; cambiar un número dentro de `Cube.Draw` sí lo detecta.
- `barrido`: el Form1 a medio hacer lleva sus candados (`Codigo.PonCerrojos`).
- `cristalcache` (programa, solo tarjeta): en libgdiplus `Restore` no devuelve el modo de mezcla y
  `SourceCopy` sin recorte vaciaba la página. La fila se devuelve recortada a su renglón y con el modo
  repuesto; en Windows el resultado es el mismo.
- `rendercache`: compila sin aviso; carga la copia con RtlMoveMemory llevado a memmove, como
  `franjareloj`; y su referencia lleva un recorte rectangular, porque en Mono un recorte con forma
  se suaviza solo si va solo (en Windows nunca).
- `planbiblioteca`: fuera de Windows C:\ y D:\ son la misma unidad; ese paso se salta y la espera tiene tope.
- `iadatos` (no estaba en rojo): aparta y devuelve también `rocola_ia_datos.generos.tsv`; antes
  quedaba en la carpeta o reemplazaba la del dueño.

Comprobado con Mono en Linux; no en Windows ni en XP.

## Estela y medicion real de la estrella (10-10-2026)

`ecosgeometria.cs` compara bit a bit caras y bordes de la estela 3D contra la receta
sin cache. Acepta un segundo argumento opcional con el ejecutable anterior. Prueba
Legacy/Natural, nacimientos, edades, transparencias, todos los campos de la clave,
reemplazos del anillo y cambios de estilo. Su coste sintetico compara cache caliente
con reconstruccion **mas copia**, no FPS reales ni el coste del binario anterior.

`estrellasesionreal.cs` es opcional. Compilar con el compilador de cada rama y las
referencias System/Core/Drawing/Windows.Forms; NAudio debe estar accesible como en
`sesionreal.cs`. La autoprueba `--autoprueba <Rocola.exe>` no abre una ventana GPU ni
reproduce musica y es la UNICA parte de este instrumento incluida en validar-capas.

Uso real: `estrellasesionreal.exe <Rocola.exe> <datos COPIADOS en TEMP> <ruta absoluta
de una pista real de esa biblioteca>`. Preparar una copia de un album pequeno, sus
tablas, miniaturas, configuracion y dos modelos pequenos. Desactivar SOLO en esa
copia `ia_datos=0`; no copiar el corpus de entrenamiento. No usar enlaces/junctions
de datos hacia la instalacion original. La musica original se lee, no se reemplaza.
La pista debe durar mas de 35 segundos. Cada escena la abre desde cero y muda al
final de la cadena, conservando el trabajo DSP; no cambia el volumen de Windows.

Rechaza otras Rocola/pruebas abiertas, sin cerrarlas. Espera escaneo, duraciones,
repaso e integrada sin pendientes NI trabajadores vivos (hasta 15 minutos). No
desactiva ni pausa el repaso para aparentar fluidez. Una biblioteca que no termine
o se reactive durante la prueba invalida la escena; ia_datos=0 por si solo no basta.
Ejecutar ambas ramas secuencialmente, sin HAL, compilacion o entrenamiento concurrentes.

Recorre Legacy/Natural x estrella 2D/3D x navegacion quieta/estado extendido. La
navegacion mantiene actividad sin pasar paginas, para aislar la animacion; los
cambios de pagina siguen midiendose con sesionreal. Usa Application.Idle y el
temporizador reales, SIN cuadros ni ticks forzados. Calienta 5 s y espera la expansion
completa; mide 25 s por escena. Informa presentaciones, fallos y rangos p95/p99 de
ventanas recientes de 512 cuadros, maximos de intervalo/fases/huecos/agenda. No son
percentiles agregados, fotones ni certificacion XP; no sumar maximos independientes.
Un cierre del usuario, cambio de pista, repaso nuevo o fallos de Present da error.

La continuacion del 10-10 agrega totales de TODOS los intervalos/episodios de la
fase, coincidencias GC (no duracion ni causa) y el mayor intervalo con sus propias
fases, hueco, agenda y deltas GC. No se pierden al salir de los anillos de 512/32.
`ventanas_max_ms` sigue siendo el mayor valor de los informes recientes;
`max_sesion_ms` es el maximo de toda la fase. Los p95/p99 siguen siendo por ventana,
no percentiles globales. Rechaza una minimizacion, una continuidad cortada o un
reinicio de medida aunque ambos contadores vuelvan a crecer. La guardia reconoce
instrumentos con sufijos HD/Modest, ademas de Rocola y sesionreal; ejecutar siempre
por separado, sin otros benchmarks/compilacion/HAL/entrenamiento. Autoprueba: 35
controles sinteticos. Ver FLUIDEZ_SESION_COMPLETA_2026-10-10.md.

La pasada actual SOLO valido la autoprueba y la geometria: no llamar medicion real
a la compilacion de este instrumento. Ver ESTELA_CACHE_Y_PRUEBAS_2026-10-10.md.

## Comparaciones reales ejecutadas y repaso activo (10-10-2026)

La continuacion corrigio la preparacion PlayItem/Song del instrumento de estrella:
la version anterior fallaba antes de medir, no demostraba un fallo del reproductor.
Ahora prepara el disco/paleta reales y comprueba Playing despues del calentamiento.
Autoprueba actual: 43 controles, repetidos en proceso nativo y x86 en ambas ramas.

Se agrega el cuarto argumento opcional `--con-repaso`. NO es un setting grafico.
Con el exige pendientes y trabajadores vivos; sin el espera repaso terminado.
Conservar ia_datos=0 SOLO en las copias; el modo activo no pausa trabajadores.
Los pendientes suman obligaciones de varias tandas, no canciones unicas. La
navegacion mantiene actividad: el propio programa aplaza el analisis mientras
alguien usa los controles. En extendido si debe disminuir el trabajo pendiente.

Se ejecutaron 32 escenas reales: ocho por rama, sin repaso y con biblioteca
completa/repaso activo. Modest aun registra pausas con el repaso activo; no
certificar fluidez por FPS medios ni por las autopruebas. Resultados y limites:
`CIERRE_REAL_Y_APOLLO_2026-10-10.md`. Sustituye SOLO el estado pendiente de los
informes anteriores; las evidencias sinteticas anteriores conservan su alcance.

## Añadir una batería nueva

1. Escribe `mibateria.cs` con un `Main` que reciba la ruta del ejecutable como primer argumento.
2. Que imprima `  OK  ` / `  FALLO  ` por cada cosa que compruebe y devuelva el número de fallos.
3. Añade su nombre a la lista `CPU=` (o `GPU=`) de `todo.sh`.

**La regla que se sigue aquí:** antes de arreglar algo, la batería tiene que **fallar** con el
código de antes. Una prueba que pasa en las dos versiones no vigila nada.

`huellacuadros` (19-10-2026) es la prueba de los ahorros automaticos del dibujo: una tarjeta imitada que ademas PINTA (raster en software: vertices a 1/256, regla de arriba-izquierda, una o dos texturas, mezcla normal o sumada, imagen intermedia) saca de cada cuadro los pixeles, el chorro de ordenes y el chorro sin lo invisible. Compila una copia de las fuentes con los relojes y el azar de pruebas (`huella-compila.sh`, `huellareloj.cs`; la rocola que se entrega no se toca) y compara con `huellas_modest_2026-10-19.txt`, las huellas de ANTES de los ahorros: los pixeles tienen que ser los mismos y, donde el ahorro no debia tocarlo, tambien cada orden y cada vertice. Con el ejecutable instrumentado como primer argumento y un segundo argumento tiene otros modos: `huella` (una linea por pantalla, 168 en total), `coste` (procesador, memoria, llamadas al controlador y pantallas pintadas), `detalle` (orden por orden: area, quien la pidio, cuanto se tapa despues), `componentes`, `panel`, `largo` (para `mono --profile=log:sample`), `vinetea` y `lista`. Es una imitacion de ESTE equipo (Linux/Mono, libgdiplus): no es una tarjeta ni el GDI+ de Windows XP.

`diagnostico` (23-10-2026) vigila el diagnostico completo de Ctrl+F3 (`DIAGNOSTICO_COMPLETO_MODEST_2026-10-23.md`; solo perfil con tarjeta, se compila con `franjareloj.cs`, `-main:Diagnostico`; `mono diagnostico.exe rocola.exe [archivo de muestra]`). Toma de muestras (un tiron inyectado de 120 ms cae en >100 ms y en su fase; fuera del dibujo; espera ociosa; fallos; un millon de cuadros sin recolecciones y sin newobj/newarr/box), motor con reloj virtual (todas las escenas, saltadas con motivo, presupuesto, cancelacion), procesador del proceso, por hilo y del sistema, informe (diez secciones, archivo nuevo, poda a 10), panel de la tarjeta imitada, `CorreDiagnosticoGpu` entero con un Form1 sin encender y lectura del programa compilado (no guarda, no reinicia la tarjeta, no toca volumenes, no escribe a disco en las escenas). Todo sintetico: no mide la rocola real ni Windows XP ni un A4-6300.

`tironescambio` (20-10-2026) vigila lo que se hizo para los peores cuadros al cambiar de pagina, de cancion o de modo (`TIRONES_AL_CAMBIAR_MODEST_2026-10-20.md`). Una tarjeta imitada que solo CUENTA (texturas creadas, bloqueos, PreLoad, memoria libre, evicciones), sin pintar. Prueba: el libro de los ocho peores cuadros de cada minuto (solo con `diagnostico=1`, sin reservas por cuadro, volcado al cerrar, encendido solo desde `diagOn`); el precalentado de lo que pinta la tarjeta (guardas, no tocar lo que se ve, primera vez fria contra precalentada en procesos nuevos, pixeles iguales); los bordes de la celda separada (24 celdas byte a byte contra `celdas_separadas_2026-10-20.txt`); el alfa de los PArgb (240 huellas contra `texturas_2026-10-20.txt`); `GetAvailableTextureMem` (3 sondeos en 180 s, al cambiar la resolucion, tras una textura que no se crea, nunca con el estado extendido); la atraccion con escaparate sin imagen de interfaz; los delegados de `Fn<>` reusados (acotados a 48); `PreLoad` solo en el adelanto; la evicion y la espera sin memoria; la vista previa 32x32 que ya esta en el almacen; y el espectro del hilo de audio (huella de 5 configuraciones contra `aurora_2026-10-20.txt`). Con un ejecutable anterior como segundo argumento compara contra el, cada uno en su PROCESO (dos ensamblados `rocola` en uno solo se confunden, y `Assembly.Load(bytes)` pierde el `dllmap` de Mono) y mide antes/despues alternando procesos. Los archivos de huellas son bytes de ESTE equipo (Linux/Mono, libgdiplus), no de GDI+ de Windows: solo se comparan en Mono. Todo sintetico: no mide la rocola real ni Windows XP ni un A4-6300.

`inventario` (19-10-2026) no vigila nada: mide, con la tarjeta imitada, cuanto cuesta en procesador y memoria un cuadro de GPU capa por capa y cuanta pantalla pinta cada orden. `ventanaf3` recorre ahora tambien las hojas Fluidez y Performance de Imagen y las hojitas de Performance.
