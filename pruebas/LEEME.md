# Las pruebas de la rocola

Cada archivo `.cs` de esta carpeta es una **batería**: un programa suelto que carga el
ejecutable de la rocola y le mira las tripas por dentro. No son pruebas de las que solo
comprueban que una función devuelve 2+2=4; miden lo que cuesta algo **de verdad**, cuentan
los píxeles de lo que se pinta, o leen el código ya compilado para comprobar que una función
sigue enchufada donde debe.

Cada una escribe líneas `OK` y `FALLO` y devuelve cuántos fallos hubo.

## Cómo se corren

En Windows: `pruebas/validar-capas.ps1 -OutputDirectory <carpeta-temporal>`
compila GPU/CPU y ejecuta 27 baterías, incluida `espacioprecarga`
(siguiente página/letra, prioridades, memoria adaptativa, estrella y ocultación).
No reemplaza ejecutables instalados. La prueba HAL usa Direct3D real cuando está
disponible; si falta, lo indica y no demuestra el comportamiento de la tarjeta.

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
| `picoreal` | que la onda que reconstruye el convertidor no pase del techo: el detector de pico real (filtro, casos al estilo de EBU Tech 3341, contra la referencia independiente `ref_picoreal.py`), la salida del limitador medida a cuatro frecuencias y 1/2/6 canales, lo que dejaba pasar el limitador viejo, que lo que no pasa del techo salga intacto, cola, silencio, archivos cortos, saltos, lecturas que parten un cuadro, la misma ganancia en todos los canales, sin distorsión ni bombeo, la cadena real y lo que cuesta (lanza Python; sin él lo dice) |
| `realce` | que el realce de agudos no pliegue armónicos: el filtro de media banda (medido), paso directo bit a bit si la canción empieza con el realce en cero, el seco intacto y retrasado 31 cuadros si se apaga a media canción, que seco, parte lineal y saturación salgan alineados, el plegado contra la referencia independiente `ref_realce.py` (saturación a 16× exacta) en tonos, barrido, multitono y música tipo MP3 a 44,1 y 48 kHz, frente al realce de antes; seis canales sin cruces, 119 tamaños de lectura bit a bit iguales, la cola de 31 cuadros, archivos cortísimos, NaN, subirlo a media canción sin salto; el grave sintético medido aparte; cableado y coste (lanza Python; sin él lo dice) |
| `transiciones` | que el grave sintético y el DSEE cambien en vivo sin escalones ni saltos: la perilla del grave en rampa de 20 ms hasta el valor exacto (medida desde fuera), sin fundido al empezar la canción, apagado intacto bit a bit, sin estado viejo al volver, NaN ignorado, cualquier tamaño de lectura; el DSEE que al apagarse no pierde ni repite ninguna muestra, entra y sale con fundido (comparado con la misma corrida sin fundido), mide bien el corte al encenderse, aguanta alternarse cada pocos ms con lecturas que parten cuadros y acabar a media transición; y lo que cuesta |
| `respaldo` | *(ramas con DSEE ULTRA)* que con ULTRA encendido el DSEE normal SI atienda a las canciones que Apollo aún no ha hecho: que salga su letrero plateado, que el realce no empuje dos veces, y que encender ULTRA no te borre para siempre el interruptor del DSEE |
| `atoron` | lo que la pantalla se queda parada cada vez que hay que dar de comer al sonido |
| `nivel`, `bajo`, `bajoauto` | nivelado de volumen y bajo psicoacústico |
| `nivelpicos` | el nivelado que mira los picos: el medidor de picos por bloques de 100 ms (pico y P99, silencio, trozos, último bloque), la tabla `rocola_sonoridad.txt` de algoritmo 2, la ganancia (topes de 3 y 6 dB, bajar un poco las muy fuertes, el orden) y, con el limitador de verdad, cuánto trabaja antes y después |
| `relleno` | los MP3 sin huecos entre pistas: la etiqueta LAME de MP3 de verdad (`relleno_*.mp3`, hechos con ffmpeg/LAME: estéreo, mono, 48 kHz, tasa variable, sin etiqueta, MPEG-2, con ID3 delante, con un bit cambiado), medir el descompresor con el clic que lleva la rocola (y cuándo no fiarse), el recorte de un disco continuo partido en tres MP3 (idéntico al original), que esté conectado y que la etiqueta cuadre con el archivo (un MP3 unido con `copy /b` lleva la de su primera parte y se cortaba a la mitad; uno cortado ponía el fundido después del final: ahora suenan enteros, como antes) |
| `franjareloj` | solo con tarjeta: el reloj de la canción repinta y sube solo su recuadro de la franja de abajo (FranjaLigeraGpu.cs): idéntica píxel a píxel a rehacerla entera en 24 escenas × 9 pasos, los títulos que se deslizan no se tocan, lo demás va entera, el trozo solo para la versión de justo antes, y la tarjeta imitada recibe solo el trozo |
| `prioridad` | la rocola arranca con prioridad por encima de lo normal (Program.cs): solo la rocola que se queda (no la segunda que se cierra), antes de abrir la ventana, nunca "alta" ni "tiempo real", en un solo sitio y sin romper nada si Windows no deja |
| `diario` | las tablas grandes sin reescribirlas enteras (DiarioTablas.cs): la primera vez de la sesión entera y con los bytes de siempre, después solo lo nuevo al final, sin cambios nada; entera si se quita una canción, si alguien tocó el archivo o si desapareció; compacta cuando hay demasiadas repetidas; al leer (arranque y respaldo) manda la última línea; una última línea a medias o con ceros no se lee; lo mismo en el EQ y en la integrada (con su cabecera y los registros futuros); y lo que se escribe en la primera medición de 20.000 canciones |
| `otroarchivo` | en la ruta de una canción hay hoy otro archivo (la completa en lugar de una descarga cortada, otra versión con el mismo nombre): con la duración apuntada del de antes, el fundido de salida bajaba a cero donde acababa aquel y el resto de la canción pagada sonaba en silencio. Con la cadena de verdad (`ArmaCadena`): suena entera y el fundido cae al final de la de ahora; con la duración bien apuntada (o unas décimas distinta), como siempre; y lo apuntado se sigue preguntando primero |
| `firmabiblioteca` | una carpeta de la música que no se puede leer (sin permiso, una ruta de más de 260 letras) ya no tira la firma de la biblioteca entera: antes, sin firma, la biblioteca no se guardaba y cada arranque recorría el disco completo. Con una carpeta cerrada de verdad (en Linux, siendo root, el árbol pasa a `nobody` y el proceso también): la firma sale, es estable, apunta la carpeta cerrada, la de antes y después del recorrido coinciden, y con todo abierto es la de siempre (las bibliotecas guardadas siguen valiendo) |
| `guardadocortado` | un guardado cortado entre sus dos pasos (donde no hay `File.Replace`: FAT32, o el archivo bloqueado por un antivirus) deja el archivo bueno solo como `.bak`: al arrancar, antes de leer nada, vuelve de su copia (volumen, EQ, ajustes, teclas, biblioteca...), la copia no se toca, lo que sí estaba tampoco, se lee entero y guardar después no pierde lo de antes; el estado (créditos y cola) no entra; queda apuntado en `rocola_error.txt` |
| `nofinitos` | lo que no es un número (NaN o infinito) no entra en las medidas ni en las tablas: un WAV en coma flotante con muestras rotas se mide igual que con esas muestras en silencio (antes salía NaN y envenenaba la media del EQ de toda la biblioteca); `PublicaMedida`, `CierraHuella` y `CierraSonoridad` no guardan NaN ni infinitos y una medida rota no cuenta como ya medida; al leer volumen, EQ y duraciones (arranque y respaldo) las filas rotas no entran y la media del EQ sale finita |
| `bitacora` | lo que tarda, en la bitácora (`MedidasBitacora.cs`): con `diagnostico=1`, las tablas del arranque (leerlas y lo que las espera la ventana), la biblioteca (y comprobar la guardada) y cada canción hasta que suena quedan en `rocola_diagnostico.txt`; sin eso no se escribe nada; y está enganchado al publicar las tablas, al acabar la biblioteca y cuando la canción ya suena |
| `enmemoria` | la canción que suena y la siguiente, en memoria (CancionEnMemoria.cs): desde la memoria el lector entrega lo mismo que desde el archivo (también saltando) y el AbreMp3 de memoria es el de siempre letra por letra y compilado igual; lo leído por adelantado se usa una sola vez y no si el archivo cambió o es otro; ni lo que no es MP3 ni lo que pasa de 32 MB; con el archivo vaciado a mitad de canción sigue entera; y el enganche (fuente, lectura por adelantado, soltar) |
| `bomba` | la música en su propio hilo (BombaSonido.cs): sale lo mismo bit a bit (también la cadena de verdad con todo encendido), la cadena la lee otro hilo con prioridad por encima de la pantalla, el final y los fallos llegan como antes, un hilo atrasado se espera como mucho 600 ms y después silencio (nunca 0), al parar el hilo se va antes de soltar el archivo, el pulso y el relevo suman lo que lee por delante, y lo que le cuesta a la pantalla cada bloque |
| `discoparejo` | el nivel por disco: en racha (la de antes o la de después del mismo disco) la misma ganancia para todas, la del disco entero; sin racha, la de siempre; videos y discos de otra carpeta no hacen racha; el disco por su carpeta (también desde "lo más sonado"); los picos de su pista más fuerte; sin bastantes medidas, canción por canción; enchufado en la cadena, la siguiente preparada y el ajuste en vivo |
| `todojunto` | todo el sonido a la vez en la cadena de verdad (OpenAudio + ArmaCadena): nivelado, Subir TODO, realce, DSEE, grave sintético, grave en mono, des-recorte, modo noche, fundido y saltar silencios; y cambiándolo en vivo cada 0,4 s como desde Ctrl+F3. Nada roto ni por encima del techo, sin chasquidos ni muestras perdidas, la cadena siempre como dicen los ajustes, cuánto trabaja el limitador y lo que cuesta |
| `silencios` | saltar los silencios largos (SinSilencios.cs): dónde suena cada canción sacado de la medida entera (bloques de 100 ms, −60 dBFS, el bloque a medias del final, mono y estéreo), la tabla de algoritmo 3 (ida y vuelta y lo que se descarta), qué se salta y qué no (2 s en los extremos, 5 s en medio, márgenes y fundido), el recorte muestra a muestra (lecturas de cualquier tamaño, saltos del lector, un salto que falla, retomada), un WAV medido y abierto de verdad con el interruptor encendido y apagado, lo guardado y los enganches |
| `soltar` | que el limitador espere 20 ms antes de soltar: un bajo sostenido por encima del techo sale limpio a 40, 60, 100 y 200 Hz, un golpe suelto vuelve pronto, en canciones de prueba limita y bombea casi lo mismo que antes, lo de debajo del techo intacto y nada por encima |
| `noche` | el modo noche: el cruce de la medianoche, que los vídeos también bajen, la rampa por reloj, que de día no cambie nada y, en la sección 8, los graves y agudos que devuelve al bajar el volumen, medidos contra la norma ISO 226 calculada en la batería, sin saltos, bit a bit de día y al amanecer, disco continuo, configuración y cableado |
| `gravemono` | el grave en mono: la respuesta contra la teoría del cruce, lo mono intacto, un instrumento abierto que sigue a su lado por encima del corte, un canal al revés que no pierde el grave (y que lo normal no lo activa), lo que no se toca, cualquier forma de leer bit a bit igual, el disco continuo, la cadena de verdad, la configuración y el coste |
| `limite` | que el limitador BAJE la ganancia en vez de recortar: distorsión de un tono fuerte y aplanado de la punta en un golpe seco |
| `eqauto`, `eqia`, `calidad` | el EQ automático y su objetivo por canción |
| `duracion`, `titulo`, `etiqueta`, `genero` | lo que se lee de cada archivo |
| `colores` | que ningún género musical se quede sin color: los que trae un ID3 y los que se leen del nombre de la carpeta, en español y con acentos; que el color sea el mismo se pregunte por donde se pregunte; y que no haya dos que no se puedan distinguir |
| `mudos` | *(sólo en la rama de tarjeta gráfica)* la limpieza de videos que no suenan. Lo que vigila no es que borre, es que NO BORRE DE MÁS: uno sin medir no puede aparecer nunca, y si Windows no puede mandarlos a la papelera la biblioteca se queda exactamente como estaba |
| `videos` | que los videos entren en el repaso que los mide, y que suenen todos parejo: el reproductor de Windows sólo sabe BAJAR, así que se comprueba con qué volumen quedaría cada uno y cuánta diferencia queda entre el que más suena y el que menos |
| `sinpausa` | que la canción siguiente entre por la misma salida sin hueco ni cebado, sin cobrar dos veces ni repetir o saltar ninguna, y que cola, créditos e historial queden como antes, también cuando algo falla a mitad del relevo |
| `salida` | la salida al aparato: coma flotante en Vista o posterior y 16 bits con un solo dither si no; que un fallo del APARATO (desenchufado, tomado por otro programa) no gaste canciones ni créditos y se retome la misma donde iba; el vigía de la salida muda; el cambio de aparato; y que la canción de la medida de rendimiento salga callada sin tocar ningún volumen de Windows |
| `videoaudio` | las reglas del sonido de los vídeos: nunca más de 100 en el reproductor, emparejado con las canciones dentro de sus límites, y de noche solo baja |
| `remuestreo` | *(sólo en la rama de tarjeta gráfica)* el remuestreador de la salida moderna: en Windows mide el DMO de verdad (respuesta, plegado, retraso, coste); fuera de Windows valida el método de medida |
| `flotante` | *(sólo en la rama de tarjeta gráfica)* decodificar en coma flotante: con decodificadores de mentira, que el de siempre convertido da lo mismo bit a bit, que el de Windows solo se usa comprobado y fuera de un STA, que un fallo a media canción sigue por el de siempre en ese mismo cuadro, y los saltos y la duración por el lector de NAudio de verdad; en Windows compara el decodificador de MP3 de Windows de verdad con el de siempre sobre `flotante_overs.mp3` |
| `exclusivo` | *(sólo en la rama de tarjeta gráfica)* la salida WASAPI exclusiva con un aparato de mentira: el formato exacto que ve Windows, el orden en que se pide, la conversión (24 y 32 bits sin dither, 16 con uno), el volumen de Windows respetado, y que cualquier cosa que impida el exclusivo deje la música en compartido sin perder la canción |

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
| `estilo` | la pantalla viva, tanda 5, el estilo general (sobre todo con tarjeta gráfica): que pintar la franja de lejos no toca las etiquetas de verdad (ni su letra, ni su sitio, ni su color) y que el acomodo de la ventana no sabe nada de ella; apagada, la franja se hornea como siempre; el título crece (entre 1,45 y 1,15, sin salirse de su renglón), el artista baja casi un renglón (lo que deja el reloj) con su pastilla y su letrero (y el brillo del letrero con él), y lo que sigue pasa a ese renglón, a la derecha, detrás de lo que lleva y en un gris claro (medido en píxeles); con textos largos, el título y lo que sigue terminan en puntos suspensivos sin salirse de su sitio; el resplandor del género (centrado, de su color, respirando cada 5 s sin saltos, detrás de las portadas y solo con la idea encendida), que la tarjeta se entera del color y del interruptor, y el pulso de la letra con el color de su género; la tarjeta ESTILO GENERAL de Ctrl+F3 con sus 4 combinaciones; y lo que cuesta. Sin tarjeta gráfica, solo la pestaña |
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
| `miniaturas` | que las miniaturas de las portadas se guarden en el disco y no en la carpeta temporal (que en Puppy está en la memoria y se borra al apagar), y que el barrido no toque las recientes |
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

## Añadir una batería nueva

1. Escribe `mibateria.cs` con un `Main` que reciba la ruta del ejecutable como primer argumento.
2. Que imprima `  OK  ` / `  FALLO  ` por cada cosa que compruebe y devuelva el número de fallos.
3. Añade su nombre a la lista `CPU=` (o `GPU=`) de `todo.sh`.

**La regla que se sigue aquí:** antes de arreglar algo, la batería tiene que **fallar** con el
código de antes. Una prueba que pasa en las dos versiones no vigila nada.
