# Rocola

## Auditoria de Modest — 11 de octubre de 2026

Correcciones de guardado, buscador, fichas, transparencia de caratulas, bordes GPU,
reloj de audio y validacion del entrenador DSEE. Las portadas comparten texturas
sin reducir calidad; los paneles respetan los limites de la tarjeta. Solo Modest;
HD suspendida y renderizado CPU congelado.
[Cambios, pruebas y limites pendientes](AUDITORIA_GENERAL_MODEST_2026-10-11.md).

## Más rendimiento para los gráficos, segunda vuelta, para elegir — 14 de octubre de 2026

Tres propuestas más, todavía sin hacer. J1: el estirado del cielo no se pinta bajo las portadas (se ve igual). J2: el
cielo a un cuarto de resolución (casi igual; el cuadro que más pesa baja un 40 %, justo al pasar de página). J3: la
aurora a media resolución (con condiciones).
[Las propuestas, con sus cuentas](PERFORMANCE_PROPUESTAS_2_MODEST_2026-10-14.md).

## G1 y G2: el cielo sin repetir y el fondo en una sola capa — 14 de octubre de 2026

El cielo a media resolución ya no se vuelve a pintar si no cambió (G1, automático y se ve igual). «Fondo en una sola
capa» (G2, Ctrl+F3 → Imagen → Performance, apagado de fábrica, lo enciende el preset Performance) mete el grano y la
viñeta dentro de ese cielo. En 1024×768, con la tarjeta imitada: el fondo pasa de 3,05 pantallas por cuadro
(Performance de antes) a 1,61.
[Qué se cambió y cómo se comprobó](G1_G2_CIELO_EN_UNA_CAPA_MODEST_2026-10-14.md).

## Más rendimiento para los gráficos, para elegir — 14 de octubre de 2026

Tres propuestas, todavía sin hacer: no repetir el cielo cuando no cambió (G1, se ve igual); el fondo en una sola capa
(G2, casi igual); y 30 cuadros por segundo en reposo (G3). Juntas, el fondo pasaría de unas 5,9 pantallas por cuadro de
fábrica (3,2 con Performance) a unas 2,0, y en reposo a la mitad de veces por segundo.
[Las propuestas, con sus cuentas](PERFORMANCE_PROPUESTAS_MODEST_2026-10-14.md).

## Carpeta IAS y un disparo por pulsación — 14 de octubre de 2026

Los modelos de IA (DSEE, EQ automático y los de cada género) van en la carpeta **`IAS`**; al arrancar, la rocola pasa
ahí los que encuentre sueltos, sin borrar ninguno. ROJO + ARRIBA (buscador) y ROJO + ABAJO (ayuda) hacen lo suyo
**una vez por pulsación**: manteniendo los botones ya no abren y cierran solos; hay que soltar y volver a apretar.
[Qué se cambió y cómo se comprobó](IAS_Y_UN_DISPARO_MODEST_2026-10-14.md).

## Cuatro arreglos de las fotos — 13 de octubre de 2026

Fuera el cometa del cielo; la ayuda dice «¡Adiós!» y se cierra sola a los 15 s sin uso; el reflejo de la aurora nace
pegado a la raya del horizonte (ya no queda una franja oscura bajo la barra); «EN ESTA ROCOLA» sale entero en el modo
atracción.
[Qué se cambió y cómo se comprobó](FOTOS_COMETA_AYUDA_BARRA_MODEST_2026-10-13.md).

## Sonido: S1 a S5 — 13 de octubre de 2026

En Ctrl+F3 → Sonido → **Lo que suena ahora...**: un canal mudo se oye por los dos (S1) y la corriente continua se quita
(S2), los dos encendidos y solo en las canciones con ese defecto; el estéreo estrecho solo si hace falta (S3, apagado);
«Escuchar sin mejoras» 15 s al mismo volumen (S4); y lo que le hace la cadena a la canción que suena (S5).
[Qué hace cada una y cómo se comprobó](SONIDO_S1_S5_MODEST_2026-10-13.md).

## Más mejoras de audio, para elegir — 13 de octubre de 2026

Revisión con propuestas, sin cambios en el programa: un canal mudo por los dos (S1), quitar la continua (S2), estéreo
estrecho automático (S3), escuchar sin mejoras (S4) y qué le hace a la canción que suena (S5). Siguen pendientes A5,
A3 y A6.
[Las propuestas y lo que ya existe](AUDIO_PROPUESTAS_MODEST_2026-10-13.md).

## Ctrl+F3: dependencias vivas, guardar al momento y ajustes inútiles fuera — 13 de octubre de 2026

Lo que depende de otro ajuste va en gris y dice qué le falta, y vuelve solo. Sin tarjeta gráfica, lo suyo en gris con un
aviso por página; los nombres ya no llevan «(GPU)». Lo tocado se guarda al momento. Fuera la pared que respira y tres
interruptores que no eran opciones.
[Qué se cambió y cómo se comprobó](AJUSTES_VIVOS_MODEST_2026-10-13.md).

## Actualizar una rocola con una versión vieja — 13 de octubre de 2026

Compatible desde la primera versión: lo más sonado, monedas, botones, créditos, medidas y material de la IA se leen tal
cual; la configuración se convierte sola. Pasos para actualizar sin perder nada.
[Guía y lo que se revisó](ACTUALIZAR_ROCOLA_VIEJA_MODEST_2026-10-13.md).

## Performance y un medidor más listo — 13 de octubre de 2026

Imagen tiene un botón Performance (con Bajo, Medio y Alto) y una hoja con dos ahorros casi idénticos: el cielo a media
resolución y el grano con la viñeta en una pasada. Las órdenes de dibujo seguidas se juntan solas. El medidor mide sin
esperar al monitor, dice qué frena, apunta a 60 y comprueba su propuesta.
[Qué se cambió y cómo se comprobó](PERFORMANCE_MODEST_2026-10-13.md).

## Los ajustes de Ctrl+F3, revisados — 13 de octubre de 2026

Lo que confunde o está fuera de sitio en las seis páginas, y siete propuestas para elegir.
[Revisión y propuestas](AJUSTES_CTRL_F3_REVISION_MODEST_2026-10-13.md).

## Hojear rápido sin micro tirones en el fondo — 13 de octubre de 2026

El cielo se mueve con un reloj fino y los empujones de varias páginas seguidas se suman. Las nebulosas ya no chocan
contra su tope y la lectura de carátulas reutiliza su memoria. Los perfiles de carga aprovechan el JPEG: con disco
duro, dos páginas de colchón; Fluidez, una sola, para equipos débiles.
[Causas, cambios y pruebas](HOJEO_SIN_TIRONES_MODEST_2026-10-13.md).

## Los géneros para la IA, sin cargar la pantalla — 13 de octubre de 2026

La copia de los géneros deducidos se hace casi toda en segundo plano: con 30.000 canciones, el hilo de la pantalla
pasa de 41 ms a 1,3–2 ms cada vez. [Detalle](GENEROS_IA_SIN_TIRON_MODEST_2026-10-13.md).

## La IA aprende también con el género deducido — 13 de octubre de 2026

Las canciones sin etiqueta de género se apuntan para la IA con el género que la rocola deduce de su disco (la
pastilla de la pantalla), también el material ya juntado, y al sonar usan el modelo del DSEE de ese mismo género.
[Qué se cambió y cómo se comprobó](GENEROS_INTERPRETADOS_IA_MODEST_2026-10-13.md).

## El instalador del DSEE por género, con doble clic — 13 de octubre de 2026

`ia\instalar_dsee_genero.bat` busca solo los modelos entrenados, propone la carpeta de la rocola, pregunta por cada
género y no se cierra hasta apretar Enter. La rocola los encuentra sin reiniciar al abrir Ctrl+F3 → Sonido → Mejoras
con IA. Paso a paso en [ia/USO_EN_RELEASE.md](ia/USO_EN_RELEASE.md);
[qué se cambió](DSEE_POR_GENERO_INSTALAR_MODEST_2026-10-13.md).

## El repaso sin basura y el cielo sin pintar bajo las portadas — 13 de octubre de 2026

- **Los MP3 del repaso:** el mismo lector de NAudio, pero cada cuadro reutiliza su memoria. Leer una canción entera deja
  26 KB de basura en vez de 9,6 MB, con los mismos bytes para el descompresor.
- **La pared (solo tarjeta):** la nebulosa, la vía láctea, el grano y la viñeta ya no se pintan debajo de las portadas
  opacas. Cada capa pinta entre un 12 y un 18 % menos de la pantalla y la imagen es la misma.

[Qué se cambió y cómo se comprobó](REPASO_Y_PARED_LIGEROS_MODEST_2026-10-13.md).

## Lo que queda por mejorar, medido — 13 de octubre de 2026

Ronda de medir, sin cambios en el programa. La cadena de sonido, el arranque de cada canción y el fondo ya van
ligeros. Dos propuestas para elegir: leer los MP3 del repaso sin la basura de NAudio (más del 99 % de la que deja el
repaso) y no pintar el cielo debajo de las portadas.
[Medidas y propuestas](LO_QUE_QUEDA_MODEST_2026-10-13.md).

## Más mejoras: el repaso a fondo más rápido y el quitasiseo en discos seguidos — 13 de octubre de 2026

- **El repaso a fondo de la biblioteca, un 25 % más rápido** (2,39 → 1,79 s por canción aquí). La huella del EQ y la
  sonoridad se calculan por trozos en vez de muestra a muestra, con los mismos números bit a bit.
- **El quitasiseo en un disco seguido:** la pista siguiente hereda el siseo aprendido de la anterior, y su intro ya
  sale limpia.

[Qué se cambió y cómo se comprobó](MAS_MEJORAS_MODEST_2026-10-13.md).

## Mejoras con IA en el sonido: I1, I2 e I3 — 13 de octubre de 2026

En Ctrl+F3 → Sonido → **Mejoras con IA**, cada una con su interruptor, apagadas de fábrica:

- **I1, bajo y brillo a la medida de cada canción:** el bajo y el realce se comparan con el objetivo de esa canción
  (la IA del EQ), no con el promedio. Necesita el modelo del EQ entrenado.
- **I2, quitar el siseo de grabaciones viejas:** baja hasta 6 dB el ruido fijo de los agudos cuando la música está
  cerca de él. En música limpia no toca ni un bit.
- **I3, modelos del DSEE por género:** cada canción usa el de su género, si se entrenó. El entrenamiento ya los
  saca solo para los géneros con material de sobra.

Apagadas, el sonido es idéntico byte a byte al de antes. Hay que escucharlas en el bar.

[Qué hace cada una, cómo se comprobó y lo que falta](MEJORAS_IA_SONIDO_MODEST_2026-10-13.md).

## ¿Qué partes del sonido pueden mejorar con IA? — 13 de octubre de 2026

Revisión con propuestas, sin cambios en el programa: I1 (bajo y brillo a la medida de cada canción, con la red del EQ
que ya existe; recomendada), I2 (quitar el siseo de grabaciones viejas), I3 (un DSEE más fino), I4 y I5 (finales con
aplausos y claridad que sigue a la voz; necesitan ejemplos marcados a mano).

[Las propuestas, sus riesgos y lo que no necesita IA](IA_EN_EL_SONIDO_MODEST_2026-10-13.md).

## El sonido ya no se atasca en los silencios — 12 de octubre de 2026

- **En el silencio exacto la cadena de sonido gastaba siete veces más** que con música: los filtros se apagaban en
  números diminutos que el procesador calcula muy despacio. Pasaba al final de cada canción, justo cuando se prepara
  la siguiente.
- **Ahora esos restos se ponen a cero** cada 64 cuadros: de 95 a 13 ms por segundo en una canción con silencio largo.
- **Lo que suena es lo mismo:** comparado byte a byte en 16 bits con canciones enteras. Con música gasta lo mismo.
- **Segunda pasada:** el convertidor de frecuencia («Tarjeta a 44,1/48 kHz» y el relevo entre frecuencias) gasta
  un tercio menos, con los mismos bits.
- **Tercera pasada:** el repaso a fondo de la biblioteca ya no se atasca en los silencios (60 s mudos costaban
  5,8 s; ahora, 0,6), con los mismos valores medidos.
- Medido en esta máquina; en el equipo del bar (y en XP) no se pudo medir.

[Lo que se midió, lo que se cambió y cómo se comprobó](RENDIMIENTO_SILENCIOS_MODEST_2026-10-12.md).

## El fondo Legacy, más ligero sin cambiar el aspecto — 12 de octubre de 2026

- **Medido capa por capa** con un simulador de la tarjeta: el fondo pinta 5,9 pantallas por cuadro y gasta 0,14 ms
  de procesador (aquí).
- **Nebulosas sin detalle natural:** ya no pintan su mitad transparente (de 5,9 a 4,8 pantallas, -18 %). De fábrica
  (con detalle) van enteras, exactamente como siempre.
- **Vía láctea y viñeta**, con trozos más finos: un poco menos de pintura.
- **La aurora** no rehace su malla en los cuadros en que no cambió (-23 % con ondas a 24 por segundo).
- Lo que más ahorraría (no pintar debajo de las portadas) hay que comprobarlo en la tarjeta del bar.

[Las medidas, los cambios y cómo se comprobó](FONDO_LIGERO_MODEST_2026-10-12.md).

## Décima tanda: fallos y mejoras de audio — 12 de octubre de 2026

- **Las canciones ya no arrancan con un golpe seco:** entran en una curva de 5 ms (0,3 s al retomar a media
  canción). Un disco continuo sigue empalmándose sin corte.
- **La cadena de sonido, revisada de 8 a 96 kHz**, en mono y en estéreo, con todo encendido: nada roto ni pasado de
  techo.
- **Tres ideas de audio para que elijas** (mezcla estilo DJ, acortar finales larguísimos, igualar el volumen de los
  videos).

[Qué se arregló, qué se revisó y las propuestas](DECIMA_TANDA_MODEST_2026-10-12.md).

## Novena tanda de fallos — 12 de octubre de 2026

- **La moneda ya no se pierde con una ventana de ajustes abierta** (Ctrl+F3, carpetas, carátulas): cuenta igual.
- **Tocar la pantalla despierta la atracción**, como un botón, y ese toque no abre nada debajo.
- **El reproductor de video**, sin su menú del botón derecho (Pausa, Detener, Pantalla completa).

[Qué se encontró, qué se revisó y cómo se comprobó](NOVENA_TANDA_MODEST_2026-10-12.md).

## Octava tanda: lo que elegiste de las propuestas — 12 de octubre de 2026

- **A4, la tarjeta a una frecuencia fija:** en Ctrl+F3 → Sonido → El local y sus bocinas, «Tarjeta a:» 44,1 o 48 kHz.
  La rocola convierte cada canción con su convertidor de calidad en vez del mezclador de XP. De fábrica, «Como la
  canción».
- **T1:** la letra de las texturas de la tarjeta, ajustada a los píxeles. Hay que mirarla en el bar.
- **T2:** los números de la barra de letras, más grandes en 1024 (miles abreviados: «2.3k»). Solo con tarjeta.
- **A1 no se hizo:** tocaría el volumen de Windows, algo que el programa prohíbe. **A2 ya existía** (el modo noche).

[Qué se hizo, por qué A1 no, y cómo se comprobó](OCTAVA_TANDA_MODEST_2026-10-12.md).

## Séptima tanda: lo que pediste con fotos — 12 de octubre de 2026

- **AYUDA con su palabra en 1024×768:** menos aire en las instrucciones y el encabezado cede un poco.
- **Dentro de un disco** ya no flotan el latido de BUSCAR ni la interrogación de AYUDA.
- **El menú del asistente** dibuja las flechas y el VERDE en la opción elegida, y se van con la selección. La leyenda
  de abajo lleva sus botones.
- **La estrella, más caricaturesca:** estira y aplasta, salta con anticipación, rebota y no se queda quieta. Sin cara.
- **La barra de progreso neón** ahora es un tubo con resplandor suave, núcleo y una chispa en la punta.
- **Tope a ROJO + ARRIBA/ABAJO:** cerrar siempre; abrir, no dos veces en 0,7 s ni más de seis en 30 s.
- **Los mensajes de la atracción**, mejor dichos, y uno nuevo que presenta la ayuda.

[Qué se hizo, cómo se comprobó y lo que queda para que elijas](SEPTIMA_TANDA_MODEST_2026-10-12.md).

## Sexta tanda: los textos del modo atracción — 12 de octubre de 2026

- **Los mensajes que se turnan** («Toca un botón y elige…», «Busca cualquier canción…») se cortaban al final en
  pantallas 4:3 y 5:4. La letra se achicaba a la medida justa y se pasaba un pelo. Ahora se mide de nuevo hasta que
  cabe.
- **«Lo más pedido esta noche: «…»»** acorta el título dentro de las comillas.
- **El nombre largo de un disco** prueba con la letra algo más chica y un tercer renglón antes de cortarse.
- **En la lista de la noche** el título tiene más sitio y, si no cabe, se acorta por palabras.

[Qué se encontró y cómo se comprobó](SEXTA_TANDA_MODEST_2026-10-12.md).

## Quinta tanda de fallos, y las flechas de BUSCAR y AYUDA — 12 de octubre de 2026

- **Con el rojo mantenido**, ya no salen triangulitos sueltos junto a BUSCAR y AYUDA: se enciende la tecla de flecha
  que cada casilla ya trae (roja, con la flecha en blanco), y brilla más al apretar esa dirección. Solo con tarjeta
  gráfica.
- **El reproductor de video que no se puede crear** se suelta antes de devolver el crédito. Así ya no hace caer la
  rocola al reacomodar la pantalla.
- **El buscador** aguantó 300.000 búsquedas al azar con letras raras sin un solo error.

[Qué se buscó y cómo se comprobó](QUINTA_TANDA_MODEST_2026-10-12.md).

## Cuarta tanda de fallos — 12 de octubre de 2026

- **40 cortes de luz de golpe** en momentos al azar: siempre volvieron los mismos créditos, la misma canción y la misma
  cola, y la configuración nunca se perdió.
- **Un botón de la rocola en F5** ya no vuelve a leer toda la biblioteca cada vez que se aprieta.
- **El archivo de teclas con dos funciones en el mismo botón** hace salir el asistente de botones, en vez de dejar una
  función muerta.
- **Una canción que se rompe a media canción** tampoco se puede pedir otra vez, igual que la que falla al empezar.

[Qué se buscó y cómo se comprobó](CUARTA_TANDA_MODEST_2026-10-12.md).

## Tercera tanda de fallos — 12 de octubre de 2026

- **Una canción dañada ya no se puede pedir una y otra vez:** tras fallar una vez, al pedirla dice «Esa canción está
  dañada: elige otra», sin cobrar ni entrar en la cola. Si el archivo se cambia, o el fallo fue del disco, se puede
  volver a pedir.
- **El motivo del fallo va primero** en la línea de estado, para que un nombre largo no lo tape.
- **La biblioteca vacía** dice «No encontré música en la carpeta», no «MP3».

[Qué se buscó y cómo se comprobó](TERCERA_TANDA_MODEST_2026-10-12.md).

## El local y sus bocinas, y el cielo vivo de Legacy — 12 de octubre de 2026

- **Ctrl+F3 → Sonido → «El local y sus bocinas…»:**
  - claridad para el local;
  - que las partes suaves se oigan;
  - el balance;
  - el subgrave y el estéreo, que se mudaron aquí;
  - una prueba de bocinas (izquierda, derecha, las dos y polaridad).

  Los ajustes vienen apagados de fábrica.
- **Ctrl+F3 → Fondo → «Cielo vivo»** (solo con tarjeta gráfica), encendidos de fábrica y cada uno con su
  interruptor:
  - la luna con su fase real;
  - las lluvias de estrellas en sus fechas;
  - una fugaz dorada al poner una canción;
  - un cometa de vez en cuando;
  - más profundidad al pasar de página;
  - el cielo según la hora;
  - el reflejo de la aurora.

[Qué hace cada uno, sus claves y cómo se comprobó](EL_LOCAL_Y_EL_CIELO_VIVO_MODEST_2026-10-12.md).

## Segunda tanda de fallos para la versión final — 12 de octubre de 2026

- **El reloj de Windows que se atrasa:** con el cambio de horario de XP, la atracción y el cierre del buscador ya no
  se quedan una hora sin llegar.
- **A los 24,9 días encendida** el aviso de siempre y el hojeo de páginas dejaban de verse. Arreglado.
- **Los fallos al reproducir se explican en español y en corto,** sin el mensaje técnico en inglés.

[Qué se buscó y cómo se comprobó](SEGUNDA_TANDA_MODEST_2026-10-12.md).

## Barrido de fallos visuales y de uso — 12 de octubre de 2026

- **Con un video en ventana, los avisos ya no quedan tapados:** van enteros a la izquierda del video.
- **La línea de estado ya no se queda con avisos viejos:** el «Encontradas N canciones…» tardío se ignora, y los
  errores de reproducción se van solos.
- **El artista largo de la franja de abajo termina en «…»,** en vez de cortarse a media palabra.
- **Ctrl+F3:**
  - el texto del asistente decía el atajo viejo;
  - la fecha de la versión no se veía nunca, y ahora va en la barra de título;
  - dos notas quedaban debajo de Listo;
  - dos textos se cortaban con una letra ancha.

[Qué se buscó, qué se arregló y qué no se tocó](BARRIDO_VISUAL_MODEST_2026-10-12.md).

## La ayuda revisada de raíz, y un solo fondo: Legacy — 12 de octubre de 2026

- **La ayuda (ROJO + ABAJO o la casilla AYUDA) sigue usable después de un rato.**
  - Con un video sonando, BUSCAR y AYUDA ya no desaparecen: van encima del video.
  - Solo la niegan los bloqueos de verdad, y si no se puede, lo dice en la pantalla.
  - Los dos botones valen en cualquier orden.
- **Espacio natural se retiró; Legacy es el único fondo.**
  - En Ctrl+F3 → Fondo ya no hay selector de estilo.
  - Las claves viejas de Natural se ignoran.
  - Legacy no cambia: su huella da los mismos números.

[Causas, cambios y pruebas](AYUDA_DE_RAIZ_Y_FONDO_LEGACY_MODEST_2026-10-12.md).

## Menos disco: SSD y disco duro — 28 de septiembre de 2026

- **Las tablas grandes ya no se reescriben enteras.** El volumen, el EQ y la sonoridad de cada canción se guardaban
  enteros cada 25 canciones medidas: con 20.000 canciones, unos 3 GB escritos en la primera medición para dejar 8 MB de
  tablas (calculado). Ahora se añade al final solo lo nuevo, con el formato de siempre: en la prueba, la tabla de volumen
  de 20.000 canciones pasa de 686 MB escritos (calculado) a 1,7 MB (medido), y el archivo queda idéntico byte a byte.
- **La canción que suena y la siguiente, en memoria.** Un MP3 de hasta 32 MB se abre desde la memoria: mientras suena, el
  disco no se toca (antes, miles de lecturas pequeñas por canción) y un atasco del disco no puede cortar la música. La
  siguiente aprovecha lo que ya leía la lectura por adelantado. Se oye exactamente lo mismo.

Automático y sin interruptor, en las dos ramas. Probado con la cuenta; no se ha medido en el disco del bar ni se probó en
XP. [La guía](OPTIMIZACIONES_2026-09-28.md).

## Más fluido en los equipos de dos núcleos — 28 de septiembre de 2026

- **La música en su propio hilo.** En los equipos de dos núcleos la pantalla se paraba cada 50 ms a descomprimir la
  canción y pasarla por toda la cadena de sonido (1,8 ms de media y picos de 7,6 ms medidos en el servidor de pruebas;
  en un equipo viejo, varias veces más). Ahora lo hace un hilo aparte y la pantalla solo copia lo ya hecho: 0,07 ms por
  bloque en la simulación. El sonido sale igual bit a bit; lo que se ve sigue cayendo en el golpe.
- **El reloj de la canción, solo su recuadro.** Cada segundo se rehacía y se subía a la tarjeta la franja de abajo entera
  (440 KB). Ahora, si solo cambió el reloj o la barra, se repinta y se sube solo su recuadro (70 KB), idéntico píxel a
  píxel a rehacerla entera. Solo con tarjeta gráfica.
- **La rocola, por delante de las tareas de Windows.** Arranca con prioridad "por encima de lo normal": las
  actualizaciones, el antivirus o el indexador ya no le quitan procesador a la música ni a la pantalla. Nunca "alta".

Automático y sin interruptor, en las dos ramas. Probado con la cuenta; no se ha visto en el equipo del bar ni se probó
en XP. [La guía](OPTIMIZACIONES_2026-09-28.md).

## Nivel por disco y la espera de la cola — 28 de septiembre de 2026

- Cuando suenan seguidas canciones del mismo disco, se nivelan juntas: el disco llega al volumen de siempre y por
  dentro conserva sus diferencias (la balada sigue más bajita que el tema de antes, como en el disco). Sin racha de
  disco, todo igual que antes.
- La espera de la cola ("en ~7 min") cuenta lo que de verdad suena: sin los silencios largos que se saltan.

Las dos ramas. Probado con la cuenta; no se ha oído en el bar ni se probó en XP.

## "Subir TODO el volumen", parejo y sin aplastar — 28 de septiembre de 2026

- Ese deslizador de Ctrl+F3 sumaba sus dB encima del nivelado sin mirar los picos: con +6 dB el limitador aplastaba
  los golpes (5–6 dB), con +12 hasta 12. Ahora todas las canciones suben lo mismo (sigue parejo), pero no más de lo
  que aguanta tu biblioteca sin aplastar: si pides +6 y tu música aguanta +3, se aplican +3 y debajo de la barra lo
  dice. Para más volumen, el amplificador. Las dos ramas; probado con la cuenta, no se ha oído en el bar.

## Saltar los silencios largos — 28 de septiembre de 2026

- Las canciones con varios segundos mudos al principio, un final callado o una "pista oculta" después de un minuto
  de nada ya no dejan la rocola en silencio. **2 s o más** al principio o al final se saltan (se deja 0,3 s antes de
  que suene y 0,5 s después del último sonido); **5 s o más** en medio también (la pista oculta sí suena). Las
  pausas cortas son de la música y no se tocan. "Silencio" es por debajo de −60 dBFS.
- Interruptor en **Ctrl+F3 → Sonido → Calidad del sonido, al lado del DSEE**, encendido de fábrica y guardado en
  `rocola_config.txt` (`saltar_silencios=`). Se nota desde la siguiente canción.
- Sale de la medida entera de cada canción, que ahora apunta también dónde suena (`rocola_sonoridad.txt`, algoritmo 3):
  la biblioteca se vuelve a medir sola en reposo y, mientras una canción no esté medida, suena como siempre.
- Coste al sonar: nada que se note. El principio mudo se descarta al abrir la canción, en segundo plano; el hueco de
  en medio se salta buscando en el archivo, no descomprimiéndolo.

Las dos ramas. Probado con canciones sintéticas (`pruebas/silencios.cs`); no se ha oído en el bar ni se probó en XP.

## La aurora se mueve a 60 cuadros por segundo — 28 de septiembre de 2026

- La forma de la aurora (la altura de cada banda del espectro) se recalculaba 24 veces por segundo, con medidas del
  sonido de ~42 ms. La tarjeta dibuja 60 cuadros por segundo: el ondeo iba liso, pero la silueta cambiaba solo en 4 de
  cada 10 cuadros y avanzaba a saltos. Ahora el sonido se mide cada 1/60 s (con la misma ventana de 50 ms) y la forma
  se avanza en cada cuadro: cambia en todos, y el salto más grande de un cuadro al siguiente baja de 0,096 a 0,037.
- Se ve igual, con una diferencia: sale un poco más alta (de +0,01 a +0,06 de media por banda, en una escala de 0 a 1), porque antes
  se perdían los picos que caían entre latido y latido. El golpe sale ~17 ms antes de oírse (antes salía 17 ms tarde).
- Solo con tarjeta gráfica (el dibujo por procesador no se toca). No lleva interruptor: es más fluidez, no otro aspecto.
  Coste: ~1 µs por cuadro y nada medible en el hilo de audio.

Las dos ramas. Probado con una canción sintética y un reloj de mentira (`pruebas/aurorafina.cs`); no se ha visto en
la pantalla del bar ni en XP.

## Sonido: subir sin aplastar, y el bajo fuerte limpio — 28 de septiembre de 2026

- El nivelado ya no sube una canción bajita tanto que el limitador le aplaste los golpes: mide los picos de cada
  canción (el más alto y el que solo supera el 1 % del tiempo) y deja al limitador como mucho 3 y 6 dB. En canciones
  de prueba con golpes, el limitador pasó de quitar 5–8 dB y trabajar hasta el 95 % del tiempo a 3–4 dB. Esas
  canciones suenan algo más bajas y más enteras. Las muy fuertes se bajan un poco (como mucho 4 dB).
- El limitador espera 20 ms antes de soltar: un bajo fuerte sale con −76 dB de distorsión en vez de −44 (a 40 Hz).
- Hay que volver a medir la biblioteca (lo hace sola en reposo; mientras, se nivela como antes).

Las dos ramas. Probado con señales sintéticas y el limitador de verdad; no se escuchó en el bar ni se probó en XP.
- Los MP3 de un disco continuo ya no dejan un hueco de ~40 ms entre pista y pista: se lee la etiqueta LAME y se mide
  en cada equipo cuánto retrasa su descompresor (con un MP3 de prueba que la rocola lleva dentro). Si no se puede
  medir, suena como antes.

- Solo en esta rama: los MP3 que llegan al máximo digital se descomprimen en coma flotante con NLayer (así no se
  recortan sus picos), si el equipo lo aguanta. `NLayer.dll` va dentro de `Rocola.exe`: no hay que copiarla. Y un ajuste de XP sin
  código: la calidad de conversión de la frecuencia de muestreo, en la más alta (ver 1.28).

[Detalles: 1.25, 1.26, 1.27 y 1.28](MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md#125-subir-sin-aplastar-el-nivelado-mira-los-picos-de-cada-canción-y-baja-un-poco-las-muy-fuertes-28-de-septiembre-las-dos-ramas).

## El letrero "+4 créditos" vuelve a salir al meter una moneda — 25 de septiembre de 2026

Con la moneda que cae (tanda 3), el aviso de arriba "+4 créditos" se quitaba mientras la moneda se veía caer, y con el
buscador abierto no se notaba que había entrado. Ahora sale siempre, además de la moneda. Solo cambia con tarjeta
gráfica (sin ella ya salía) y en las dos ramas. Comprobado con la batería `suenavivo`; no se probó en una tarjeta real
ni en XP. [Guía de la tanda 3](PANTALLA_VIVA_SUENA_2026-09-24.md#2-la-moneda-que-cae--moneda_cae).

## El buscador: la tarjeta elegida desliza lo que no cabe, y los dos buscadores van con la ventana — 25 de septiembre de 2026

En el buscador con portadas, con **Títulos largos deslizantes** encendido, el nombre y el renglón de la tarjeta elegida
que no caben se deslizan, como hace la vista previa en el buscador de siempre (usa las mismas cintas). Y ahora los dos
buscadores llevan su casilla y lo elegido con la ventana al abrir y al cerrar, sin copiar nada. Solo en el dibujo por
tarjeta gráfica y en las dos ramas; parado, el buscador de siempre sale igual que antes (comprobado píxel a píxel). No se
probó en una tarjeta real ni en XP. [En la guía de la tanda 6](PANTALLA_VIVA_BUSCADOR_2026-09-24.md#añadido-el-25-de-septiembre).

## Pantalla viva, tanda 6: el buscador con portadas — 24 de septiembre de 2026

La idea "Teclado compacto y resultados con portada", solo en el dibujo por tarjeta gráfica y en las dos ramas. El teclado
queda compacto y siempre a la vista, a la izquierda; a la derecha los resultados salen en dos columnas de tarjetas grandes
con su portada, qué son (artista, álbum o canción), lo tecleado resaltado y SONANDO / EN COLA; y arriba hay atajos, solo con
datos de verdad: lo más pedido hoy, videos, karaoke y las dos últimas búsquedas. Las tarjetas que llegan entran una tras
otra, y si no caben todas se baja fila a fila. Todo sube y baja con la ventana del buscador, y lo quieto (resplandores y
fondos) va horneado: moverse por el teclado cuesta menos que en el buscador de siempre. Interruptor en Ctrl+F3 → Pantalla
→ **La atracción y el estilo** → **EL BUSCADOR**. Sin tarjeta, o con el interruptor apagado, el buscador sale idéntico al
de antes (comprobado píxel a píxel). No se probó en una tarjeta real ni en XP.
[La guía de la tanda](PANTALLA_VIVA_BUSCADOR_2026-09-24.md).

## El buscador, más ligero para la tarjeta — 24 de septiembre de 2026

Revisados todos los apartados del buscador (buscar, teclear, moverse, posar, avisos, abrir, cerrar, quieto), solo en el
dibujo por tarjeta gráfica y en las dos ramas. La búsqueda, el tecleo y el movimiento ya estaban optimizados de antes. Lo
nuevo es lo que se le manda a la tarjeta: al posar una consulta, pedir una canción o salir un aviso se sube solo el trozo
que cambió (la columna de resultados o el aviso) en vez de la imagen entera; al cerrar, la animación usa la textura que
ya tenía la imagen en vez de copiarla y volver a subirla; y al abrir se reutiliza la textura grande de la última ventana
en vez de crear otra. La imagen se sigue haciendo exactamente igual. Automático y sin interruptor. No se probó en una
tarjeta real ni en XP. [La guía](OPTIMIZACIONES_2026-09-24.md#el-buscador-más-ligero-para-la-tarjeta).

## El dibujo por tarjeta, más ligero — 24 de septiembre de 2026

Más imágenes por segundo sin cambiar lo que se ve, solo en el dibujo por tarjeta gráfica y en las dos ramas. El cuadro ya
no borra la pantalla (el cielo, opaco, la tapa entera); la vía láctea, la viñeta y la interfaz quieta se pintan solo por los
trozos que tienen algo, y lo que va con opacidad cero no se manda; y la aurora deja de repetir en cada cuadro las cuentas
de color que solo cambian con la canción. En la simulación (1024×768, navegando con música) cada cuadro pinta 6,89
pantallas en vez de 8,58 y el procesador tarda 0,39 ms en vez de 0,64 (HD) o 0,77 (Modest). Automático y sin interruptor.
No se midieron FPS en una tarjeta real ni se probó en XP. [La guía](OPTIMIZACIONES_2026-09-24.md).

## Pantalla viva, tanda 5: el estilo general — 24 de septiembre de 2026

Las dos últimas ideas elegidas, solo en el dibujo por tarjeta gráfica y en las dos ramas. Letra para leer de lejos: en
la tarjeta de lo que suena, abajo, el título crece, el artista baja casi un renglón y lo que sigue pasa a ese mismo
renglón, a la derecha, en un gris mucho más claro; la franja mide lo mismo y nada más se mueve. El color del género en todo:
la portada de lo que suena y la de dentro de un disco llevan detrás un resplandor del color del género que respira
despacio, y el pulso de la letra elegida toma el color de su género. Cada idea tiene su interruptor en Ctrl+F3 →
Pantalla → **La atracción y el estilo**, tarjeta **ESTILO GENERAL**. Sin tarjeta, o con las ideas apagadas, todo sale
como siempre. Con esta tanda están hechas las cinco. [La guía de la tanda](PANTALLA_VIVA_ESTILO_2026-09-24.md).

## Pantalla viva, tanda 4: el modo atracción — 24 de septiembre de 2026

Las dos ideas elegidas para cuando la rocola se queda sola, solo en el dibujo por tarjeta gráfica y en las dos ramas.
El escaparate: el cubo gigante se va a la izquierda y se mece, y a su lado sale el disco que enseña (su nombre en
grande, el artista, el género, cuántas veces se ha pedido y cuántas canciones trae), con un mensaje debajo que se turna:
cómo elegir, lo que da una moneda, lo más pedido y cómo buscar. Y cada 30 segundos, la lista de las cinco canciones más
pedidas esta noche (desde las 8 de la mañana), con sus portadas y barras que crecen; la cuenta se guarda en
`rocola_noche.txt`. Cada idea tiene su interruptor en Ctrl+F3 → Pantalla, en la hoja nueva **La atracción y el
estilo**. Sin tarjeta, o con las ideas apagadas, la atracción es la de siempre.
[La guía de la tanda](PANTALLA_VIVA_ATRACCION_2026-09-24.md).

## Pantalla viva, tanda 3: lo que suena y los créditos — 24 de septiembre de 2026

Las ideas elegidas para lo que suena, las monedas y los avisos, solo en el dibujo por tarjeta gráfica y en las dos
ramas. Cuando nadie toca la rocola, el panel de "A H O R A   S U E N A" lleva un vinilo que gira detrás de la portada,
el título en una sola línea (si no cabe, se desliza despacio), cuánto le falta a la canción y en cuánto suena cada una
de la cola. Al meter una moneda cae una moneda dorada que aterriza en el número de créditos, la cápsula da un golpe y
sube "+4 créditos". Si alguien pide algo sin créditos, en vez del letrero naranja sale una tarjeta amable que le dice
qué le falta y cuánto da una moneda; no pone nada en la cola sola. Y los avisos entran por la derecha con su color, su
icono y dos renglones: qué pasó y el detalle. Cada idea tiene su interruptor en Ctrl+F3 → Pantalla, en la hoja nueva
**Lo que suena y los créditos**. El dibujo por procesador no cambia.
[La guía de la tanda](PANTALLA_VIVA_SUENA_2026-09-24.md).

## La estrella ya no baila con la música — 24 de septiembre de 2026

La estrella que cruza el cielo daba brincos porque seguía la música: con cada golpe cambiaban de golpe su velocidad, su
giro, su balanceo y su tamaño. Ahora va siempre a su ritmo de crucero, el de una canción normal. Solo se calma al
navegar y se anima en reposo, despacio. En Ctrl+F3 → Fondo el interruptor se llama ahora **Estrella viajera con órbitas**,
y la corona, **Corona estelar**. Solo cambia el dibujo por tarjeta gráfica, en las dos ramas.
[Los detalles](ESTRELLA_3D_2026-09-23.md).

## Pantalla viva, tanda 2: dentro del disco — 24 de septiembre de 2026

Las ideas elegidas para el interior de un disco, solo en el dibujo por tarjeta gráfica y en las dos ramas. Si suena
una canción del disco abierto, un vinilo sale de detrás de la portada, gira a 33 ⅓ vueltas y lleva la portada en su
etiqueta. La lista se lee de lejos: unas nueve canciones por página, con la letra más grande. La que suena lleva un
ecualizador de cuatro barritas que se mueve con la música, y las pedidas dicen cuánto falta: "EN COLA 3 · en ~9 min".
Al pedir una canción, su boleto vuela desde la fila hasta "En cola" y el número sube cuando llega. Cada idea tiene su
interruptor en Ctrl+F3 → Pantalla, en la tarjeta **Dentro del disco**. El dibujo por procesador no cambia.
[La guía de la tanda](PANTALLA_VIVA_DISCO_2026-09-24.md).

## Pantalla viva, tanda 1 — 24 de septiembre de 2026

Las primeras ideas elegidas del lienzo de bocetos, solo en el dibujo por tarjeta gráfica y en las dos ramas.
La portada elegida crece, brilla con su propio color y dice cuántas canciones trae y cuánto dura, mientras las
demás se apagan un poco. Si el cursor se queda un segundo en un disco, sale una ficha con sus canciones más
pedidas y cuántas van en la cola. Al pasar de página, la vieja sale mientras entra la nueva, columna a columna, y
el cielo se corre un poco más despacio. La barra de letras lleva lupa y una rayita que dice cuánta música hay
detrás de cada letra. Cada idea tiene su interruptor en la pestaña nueva de Ctrl+F3, **Pantalla**. El dibujo por
procesador no cambia.
[La guía de la tanda](PANTALLA_VIVA_2026-09-24.md).

## Estrella reactiva — 23 de septiembre de 2026

Estrella de cinco puntas como el icono, ahora **sólida en 3D**: gira vueltas enteras
sobre su eje mientras recorre el fondo, con luz por facetas y un brillo que corre al
girar. Su estela, tipo Sandevistan, son copias congeladas con el giro de ese momento:
una por cada radio recorrido, aparecen sin saltos y se desvanecen en 1,2 s con los
colores de la canción. Con el compositor de Windows el panel dibuja un cuadro por
refresco de la pantalla y coloca la estrella para el refresco en que se verá: eso quita
los saltitos al moverse (sin compositor, como en XP, sigue como antes). Interruptor
nuevo en Ctrl+F3 → Fondo, "Estrella en 3D girando (GPU)". El flujo de portada/género
es más rápido en el espectro; el cielo conserva su degradado nocturno azul/negro. GPU
compartida por Modest y HD.
[La estrella 3D](ESTRELLA_3D_2026-09-23.md) · [Cielo y estela musical](CIELO_ESTRELLA_2026-09-23.md).

## Audio — 23 de septiembre de 2026

Rampas de volumen/excitador, transición de EQ, huellas estéreo sin cancelación y
protecciones del entrenamiento; además, sonoridad integrada BS.1770 de la canción
entera, limitador de pico real entre muestras, realce de agudos sin plegado (la
saturación trabaja a 2× con el camino seco alineado) y cambios en vivo del grave
sintético y del DSEE sin escalones ni saltos, y continuidad sin pausas entre
canciones del mismo formato (la siguiente entra por la misma salida, sin solaparse,
con cola y créditos como siempre) y una salida que entrega coma flotante en Vista
o posterior y, si falla el aparato, espera y retoma la misma canción sin gastar la
cola, compartido por Modest y HD (el sonido de los vídeos sigue siendo del
reproductor de Windows Media: la guía explica las opciones y espera una decisión). Solo en
HD, Apollo pasa a 44,1 kHz con SoXR cuando el FFmpeg lo trae (con alternativa
probada) y reaprovecha sin reprocesar lo que no cambia.
De noche, al bajar el volumen, devuelve los graves y agudos que el oído pierde
(ISO 226), y el grave por debajo de 120 Hz sale igual por todas las bocinas (una
grabación con un canal al revés conserva su grave). Las dos ramas.
Detalles, mediciones y tareas aún pendientes (escucha A/B, Windows XP real, CPU
antiguo) en [la guía de audio](MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md).

## Respuesta y transiciones GPU (10 de septiembre de 2026)

Tres interruptores independientes, activados por defecto, en Ctrl+F3 → Fondo:

- **Respuesta inmediata al toque:** una tecla o clic solicita actualizar la UI
  en el próximo cuadro, sin esperar su intervalo de trabajo pesado (hasta 250 ms).
  También inicia el regreso desde el estado ampliado sin esperar al temporizador.
  No ejecuta el render dentro del evento, ni retrasa o duplica acciones.
- **Transiciones de vista y selección:** fundido breve de la interfaz al cambiar
  entre muro e interior (140 ms) y del énfasis de resultados del buscador (120 ms).
  Los controles mantienen sus posiciones y siguen respondiendo durante el efecto.
  Se conservan las animaciones existentes de cubos, buscador, páginas y avisos.
- **Caché de portadas recientes:** hasta ocho texturas GPU y 8 MiB adicionales
  de texeles ARGB, incluido redondeo de dimensiones. Reutiliza las texturas al
  revisitar portadas, sin reducir resolución. Se libera al apagar la opción o la GPU.
  La contabilidad no representa todo el consumo del controlador y puede retener
  referencias a los bitmaps originales mientras estén en la caché.

Las mejoras no cambian el renderizador CPU, audio, orden de cola o contenido.
Desactivarlas recupera su ruta previa. No prometen eliminar el tiempo necesario
para leer una portada que todavía no esté cargada.

Verificación: pruebas/respuesta.cs comprueba las curvas a 24–240 Hz, prioridad
de entrada y la caché con HAL local (150 cambios, identidad de texturas,
propietario único, límite y liberación). pruebas/cristalcola.cs comprueba
32 combinaciones de las cinco opciones recientes y su guardado/restauración.
Son pruebas automatizadas; no una medición de latencia/FPS de la aplicación
completa con música ni una validación de Windows XP real.

## Fondo, cristal y cola ampliada

- La pestaña Fondo permanece accesible desde todas las páginas de Ctrl+F3.
- En Ctrl+F3 → Fondo, «Interior de discos con cristal (GPU)» añade esquinas
  redondeadas, un borde iluminado y reflejos a la lista transparente. Requiere
  «Lista de canciones transparente» en Imagen; apagado conserva el velo plano.
  Es un acabado de vidrio con textura reutilizada, no desenfoque del fondo.
- «Cola ampliada de 12 canciones (GPU)» muestra hasta doce próximas canciones
  en tres columnas y cuatro filas, numeradas de izquierda a derecha. Apagado
  recupera tres canciones. En espacios estrechos se omiten miniaturas y se
  abrevian textos; la cola real y su orden no se modifican.
- En el estado expandido, el cubo llega hasta la fila inferior; a su derecha
  quedan el tiempo y el progreso. El espacio del reloj se mide con su texto real
  al actualizar el panel, sin mediciones por cuadro. Se elimina la tarjeta oscura
  detrás del cubo, sin alterar la imagen de la carátula ni sus caras sombreadas.

Las dos opciones nuevas están activadas por defecto y se guardan al cerrar
los ajustes. El renderizador CPU permanece congelado.
Prueba: pruebas/cristalcola.cs (persistencia, geometría y transparencia).
Prueba adicional: pruebas/cubofila.cs, 45 combinaciones de ancho, altura y tiempo;
comprueba alineación, espacio del progreso y ausencia de relleno tras el cubo.

## Política de renderizado (8 de septiembre de 2026)

El renderizador CPU es obsoleto y queda congelado por decisión del usuario.
Las nuevas mejoras visuales y optimizaciones de renderizado se realizan solo en GPU,
con interruptores persistentes en Ctrl+F3. No se retira el código CPU existente.
Consultar [AGENTS.md](AGENTS.md) antes de modificar el proyecto.

La opción «Nebulosas azul / violeta (solo GPU)» da a las dos capas del fondo una
base azul y violeta, conservando un 35 % de sus colores musicales. No cambia los
colores extraídos de los discos ni los de la ventana de detalles. Al desactivarla
se recuperan los tintes originales; requiere nebulosas en nivel medio o alto.

### Acabado, transparencias y buscador (9 de septiembre de 2026)

Se añaden seis interruptores independientes, activados por defecto y guardados en
Ctrl+F3 → Imagen:

- Neon de progreso intenso: halo más amplio, tubo luminoso y núcleo más brillante.
  Requiere el neón; funciona también con el suavizado del progreso desactivado.
- Estado ampliado transparente: elimina el relleno de la franja expandida y conserva
  un velo tenue en las tarjetas para mantener legibles sus textos.
- Lista de canciones transparente: aplica un velo oscuro de aproximadamente 82 %
  de opacidad al muro interior del disco (18 % de transparencia), manteniendo
  títulos, selección, insignias y desplazamiento.
- Optimizar selección del buscador: reutiliza la capa principal y actualiza por
  separado la tarjeta seleccionada y la información de su vista previa.
- Nebulosas con detalle natural: textura de nubes con varias escalas de detalle,
  calculada al crear o regenerar la textura, no en cada cuadro.
- Aurora con bandas más definidas: picos locales menos mezclados y gradación de
  color por frecuencia. Es diferenciación visual del espectro, no separación real
  de instrumentos ni procesamiento del audio.

Desactivar cada opción recupera su comportamiento anterior. No se cambia el
renderizador CPU. Las nebulosas requieren que sus capas estén habilitadas y la
transparencia ampliada requiere el estado ampliado en reposo.
Ver [verificación y despliegue](../auditoria-rendimiento/ACABADO-GPU-20260909.md).

### Paquete visual GPU

Revisión de legibilidad del 9 de septiembre: el estado ampliado utiliza tipografía
Trebuchet MS más grande y en negrita, encabezados diferenciados y numeración de cola.
El indicador MEJORADA aparece en una cápsula y su halo acompaña la expansión;
conserva la condición real de mejora y el color propio de cada rama.
El tiempo queda a la izquierda de la barra de progreso, en una fila inferior
reservada. Son ajustes de los efectos existentes, con sus mismos interruptores
en Ctrl+F3 → Imagen; el renderizado CPU no cambia.

Actualización del 9 de septiembre:

- Con música y 15 segundos sin interacción, el muro queda al 2 % de opacidad.
- «Estado y cola ampliados en reposo (GPU)» hace crecer la franja hacia arriba,
  agranda la carátula y muestra las tres próximas canciones con título, artista,
  portada, número de turno y total en cola. Las ondas ascienden con la franja.
  Al interactuar vuelve suavemente al tamaño habitual. Requiere el modo de discos
  tenues en reposo; no se activa en ajustes, buscador, lista de canciones o vídeo.
- «Ocultar carátulas al pasar de página» sustituye el horneado a baja resolución:
  durante el deslizamiento se omiten carátulas y etiquetas. Cubos discretos del
  tono del fondo ocupan su lugar hasta que cada celda está lista. Al detenerse,
  la carga completa se reparte entre cuadros. Se conserva la clave hojeo_ligero.
- «Progreso ultrafluido (solo GPU)» separa la barra de la textura de estado y la
  dibuja por cuadro, con coordenadas fraccionarias y suavizado temporal del audio.
  El interruptor del neón continúa siendo independiente.

Los nuevos efectos no modifican el renderizador CPU. La valoración del rendimiento
y la percepción de las transiciones con música corresponde al usuario.

En Ctrl+F3 → Imagen, cada efecto tiene un interruptor independiente y persistente:

- Viñeta suave: oscurece gradualmente los bordes del fondo, sin oscurecer discos ni textos.
- Halo de selección: luz del acento del fondo detrás del disco seleccionado; se atenúa junto con el muro en reposo.
- Estrellas finas: reduce las puntas grandes y conserva algunas estrellas destacadas, sin cambiar el patrón al alternar.
- Ondas suaves: reduce altura e intensidad de la aurora, conservando su reacción musical.
- Nebulosas azul/violeta, barra de progreso neón y discos tenues en reposo con música.
- El hojeo ligero existente sigue disponible por separado.

Las opciones están activadas por defecto. Desplazar la página hacia abajo para
alcanzar todos los controles. Apagarlas recupera el aspecto anterior de cada efecto.
La viñeta reutiliza una textura GPU de 128 × 128; el halo reutiliza la textura de
brillo existente. No se añadieron temporizadores. El rendimiento real será evaluado
por el usuario; las comprobaciones automatizadas no equivalen a una medición con música.

Rocola digital para Windows (WinForms + NAudio): pantalla completa, manejo por
botonera/teclado físico (sin depender de un teclado de PC), monedero con créditos,
búsqueda con teclado en pantalla, sección de videos (Windows Media Player), portadas
en cubos 3D, fondo estrellado por género, estadísticas ("Lo más sonado" + TOP por
artista) y modo atracción.

**Versión 2.5.** Esta rama es la **edición clásica (XP)**: la rocola física original.

## Ramas

- **Clásica (XP)** — *esta rama*, la de referencia: .NET Framework **3.5 sin SP1**,
  Windows XP 32-bit, CPU modesto. Los cambios comunes se mantienen en ambas versiones;
  la prueba en CLR 2 de un Windows moderno no sustituye la validación en el equipo XP real.
- **`rocola-hd`** — para PC **potente** (Windows 7+/10, 4+ núcleos, 4+ GB): retargeteada
  a **.NET Framework 4.8**, con salida WASAPI flotante, reconstructor espectral fino,
  render por GPU (Direct3D 9) y **DSEE ULTRA** (IA Apollo con selector de GPU). Mismo
  programa y misma lógica de audio; solo cambia lo que el fierro potente puede aprovechar.

## Qué hace (v2.5)

**Operación (local):**
- Se maneja con la botonera/teclado físico configurable; nada de "lenguaje de teclado de PC".
- Monedero con créditos y cola de canciones pagadas.
- Buscador con teclado en pantalla; sección de videos; portadas en cubos 3D girando.
- La lista de cada disco muestra la **duración** de cada canción. Va en su **propia pasada rápida**:
  consulta cabeceras cuando el formato lo permite; otros archivos requieren un recorrido mayor.
  El tiempo depende de los archivos y del disco, sin un factor de aceleración universal.
  Se guarda en `rocola_duracion.txt` y sobrevive a los reinicios.
- **Animaciones** (aceleradas en el perfil **GPU**, con trabajo de preparación en CPU;
  el coste y la fluidez dependen del equipo; `animacion=0` las apaga):
  - **La portada viaja entre cubos.** La rocola ya dibuja todo como cubos girando (la casilla de la
    cuadrícula, el cubo grande del disco, el cubo gigante de la atracción, el del buscador). La
    portada **sale calcada sobre el cubo de origen** —misma casilla y mismo giro, no se nota el
    relevo—, vuela girando un cuarto de vuelta, se ladea y se endereza, lleva un **destello** detrás,
    y **aterriza calcada sobre el cubo de destino**, donde se funde con él. Cuatro recorridos, el
    mismo código: cuadrícula→disco, disco→cuadrícula, cuadrícula→atracción y atracción→cuadrícula
    (~0.26 s). Desde el buscador sale del cubo grande del resultado elegido.
  - **Buscador**: se abre y se cierra **como una ventana de macOS** — entra al 86 % de su tamaño y un
    poco más abajo, se pasa un ~4 % de su tamaño final y **rebota** hasta asentarse (rebota algo más
    a lo ancho que a lo alto, así que se estira un instante: eso es lo que le da vida). Al cerrarse
    toma **impulso** —crece un pelín— y luego se encoge, se hunde y se va acelerando (~0.21 s).

  Todas son solo un dibujo **encima**: el cambio de pantalla sigue siendo instantáneo, y si algo las
  interrumpe (modo atracción, un video, otra tecla) simplemente dejan de dibujarse — nunca queda nada
  a medias. El avance lo calcula el propio render (60 fps), no el temporizador de la interfaz, así que
  se ven fluidas.
- **Aurora del horizonte** (`espectro=` en config, 0 la apaga): el visualizador de espectro, hecho como
  parte del **cielo** y no como un aparato encima. Un resplandor suave en la franja baja cuya silueta
  la dibujan **8 bandas** medidas en vivo, con el color del género. Va **detrás** de la cuadrícula y
  se atenúa junto con el fondo, así que nunca le pelea a las portadas ni al texto; se apaga sola
  cuando no hay música. Las bandas viajan por la **misma cola retrasada** que el pulso, así que la
  aurora se mueve encima del sonido que se oye y no un segundo antes.
- "Lo más sonado" y TOP por artista; modo atracción cuando nadie la usa.
- **Registro de errores de kiosco**: intenta anotar las excepciones en `rocola_error.txt`.
  El manejador de UI permite continuar en algunos casos; un error fatal de otro hilo puede
  terminar el proceso. No hay garantía de recuperación ni reinicio automático del programa.

**Sonido:**
- **Nivelado perceptual por etapas** (tipo LUFS / BS.1770, con *K-weighting*): busca emparejar
  canciones; sube las bajitas y **no** baja las fuertes en esta etapa. Seis etapas según lo bajita
  que venga la canción (**ULTRA BAJA / BAJA / MEDIA-BAJA / MEDIA / ALTA / MUY ALTA**), cada una
  con su propio tope de subida (hasta **+20 dB** en las ultra bajas, apenas +4 en las muy altas),
  interpolado entre etapas para que no haya saltos.
- **Pre-análisis de la biblioteca**: en segundo plano (prioridad mínima, solo cuando la rocola
  está en reposo) se mide el volumen, la **duración** y la **huella tonal del EQ automático** de
  cada canción legible. Si su análisis ya está disponible, llega ajustada desde la primera
  reproducción; de lo contrario se mide al sonar para aplicar lo aprendido después.
- **Subir TODO el volumen**: dB extra generales; el **limitador con pre-escucha** baja los picos
  suave y ANTES de que lleguen. No garantiza ausencia de distorsión en el archivo o las bocinas.
- **EQ automático**: corrige el tono de cada canción hacia el promedio de la biblioteca.
- **Bajo psicoacústico** (`bajo=` en config, perilla en Ctrl+F3): las bocinas de un local no bajan de
  ~80 Hz, así que el grave profundo **no sale** por ellas. Subirlo con EQ no sirve — solo gasta
  amplificador y hace toser al cono. En vez de eso se generan los **armónicos** de esa nota grave (el
  doble y el triple de su frecuencia), que la bocina sí reproduce, y **el oído reconstruye solo la
  nota que falta** (efecto de la *fundamental ausente*). El grave se toma de la mezcla mono y sale
  centrado. Verificado: genera el 2º y 3er armónico, **no toca** la nota original ni la voz ni los
  agudos en las señales de prueba; en 0 % el audio sale idéntico bit a bit.
  El coste de CPU y el resultado audible dependen del equipo y del material.
- **Grave en mono** (`grave_mono=` en config, el corte en Hz; 0 lo quita): por debajo de 120 Hz el mismo grave sale
  por los dos canales, así que con bocinas repartidas nadie se queda sin bajo por estar junto a la "equivocada". Lo
  mono no cambia de nivel; el grave sale unos 4 ms tarde, como en el cruce de una bocina de dos vías. Una grabación
  con un canal al revés no pierde su grave: se detecta y se devuelve en fase. Ver 1.24 de la guía de audio.
- **Modo noche que no apaga el sonido** (`noche_compensa=` en config, en %; 0 lo quita): mientras la noche baja el
  volumen, devuelve los graves y agudos que el oído pierde al bajar (curvas de igual sonoridad ISO 226): con los 4 dB
  de fábrica, +1,6 dB en 50 Hz y +0,7 en 12,5 kHz. De día no hace nada. Ver 1.23 de la guía de audio.
- **DSEE** (realce/reconstrucción de agudos): recrea el "aire" que el MP3 recorta. Copia por
  bloques con la fase seguida entre ventanas (como el SBR de AAC): un tono recreado sale limpio, al
  nivel de la curva de diseño y con el corte quieto (ver 1.21 de `MEJORAS_AUDIO_Y_PENDIENTES_2026-09-22.md`).
  Insignia **MEJORADA** (dorada, que brilla) cuando actúa el reconstructor o, en HD,
  se reproduce una versión procesada por Apollo. Indica procesamiento, no una garantía
  de mejor calidad subjetiva ni recuperación exacta de información perdida.
- **Des-recorte (declipper)**: repara los picos aplastados "de fábrica" de archivos mal
  masterizados (reconstruye el pico redondo perdido; `declip=` en config).
- **Fundido** suave de salida al terminar cada canción **y cada video** (`fundido=` en config, 0-3 s).
- **La interfaz sigue por GPU con video sonando** (`video_gpu=` en config, encendido por defecto).
  Antes, un video mandaba **toda** la pantalla a render por CPU (fondo estrellado incluido) mientras
  WMP decodificaba el video — de ahí el rendimiento raro con videos, y por eso las animaciones (que
  van por GPU) nunca podían verse con un video sonando: solo la del buscador, el único modo que ya
  ignoraba el video. El cuadro del video va **encima** del panel y Windows recorta uno donde está el
  otro. Con video visible, el panel baja solo a ~30 cuadros para dejarle la máquina al video. Si en
  algún equipo se viera mal, `video_gpu=0` vuelve al comportamiento anterior. El video a **pantalla
  completa** sí sigue apagando el panel.
- **Bitácora de diagnóstico** (`diagnostico=1` en config): apunta con hora los eventos clave (inicio
  y fin de video, animaciones que arrancan o el motivo por el que no, cambios de modo de render) en
  `rocola_diagnostico.txt` junto al exe. Para cazar problemas del fierro real sin adivinar; apagada
  no cuesta nada.
- **Volumen de los videos**: el video lo reproduce Windows Media Player, cuyo sonido **no pasa**
  por la cadena de audio. Una perilla en Ctrl+F3 (`video_vol=` en config) empareja su volumen con
  el de las canciones ya niveladas, y además sigue a "Subir TODO el volumen".
- **Dither TPDF** al bajar a 16 bits (salida WaveOut de XP): sin ruido de truncado.
- **Respaldo automático** de lo aprendido (volúmenes, EQ, estadísticas, config)
  a otra carpeta/disco; conserva las 3 instantáneas completas más recientes, incluso del mismo día.
  Cada archivo se copia y verifica antes de publicar la carpeta. Si falla, conserva los
  anteriores y registra el error. No es una copia de la música ni una instantánea atómica
  de todas las tablas a la vez.

### La IA aprende del material apto de la biblioteca (sin mover la música)

Para enseñarle a la red hacen falta pares *"esto es lo que queda / estos eran los agudos reales"*.
En un MP3 dañado los agudos reales **ya no existen**, así que solo sirven de maestras las canciones
que **aún los conservan** (320 kbps, FLAC, WAV).

Mientras el **pre-análisis** recorre la biblioteca en segundo plano, a esas canciones completas les
corta el agudo **a propósito** (a 11, 13, 15, 16.5 y 18 kHz) y guarda el par en
`rocola_ia_datos.bin`. Usa las **mismas funciones** que la reproducción, así que el material de
entrenamiento coincide **exactamente** con lo que la red verá en vivo (verificado: diferencia 0.0000).

Flujo completo:

1. La rocola junta el material sola. El avance se ve en **Ctrl+F3** ("Material para la IA: N ejemplos").
2. Copias `rocola_ia_datos.bin` a una **memoria USB** con espacio suficiente (hasta 24 GiB por defecto).
3. En una PC potente: `python3 ia/entrenar_ia.py --datos rocola_ia_datos.bin --capas 28,96,96,12`
   (el script viene en la carpeta `ia/`; solo necesita **Python 3 + numpy**).
4. Regresas el `rocola_ia.bin` nuevo a la carpeta `IAS`, junto al `Rocola.exe` (si lo dejas suelto junto al
   `Rocola.exe`, al arrancar la rocola lo pasa a `IAS` y guarda el de antes como `.anterior-N`).

Se apaga con `ia_datos=0` en `rocola_config.txt`.

### Entrenamiento a fondo (mantenimiento)

Botón **"ENTRENAMIENTO A FONDO"** en Ctrl+F3. Mientras está encendido, la rocola **se dedica por
completo** a dejar todo calibrado:

- Repasa **toda** la biblioteca (no solo lo que falta) y analiza cada canción **completa**, no por
  muestras — la medida más exacta posible de volumen, EQ y material para la IA.
- **Se puede parar y seguir.** Apunta en `rocola_afondo.txt` cada canción que ya repasó, así que al
  detenerlo y volverlo a encender —o si apagas la rocola, o se va la luz— **continúa donde iba**, no
  vuelve a empezar por la primera. La barra también sigue desde donde estaba. Cuando ya no queda
  nada, el panel lo dice: *"la biblioteca ya está repasada entera"*. La música que agregues después
  entra sola en la cuenta.
- **No espera** a que la rocola esté en reposo ni descansa entre archivos, y el hilo sube a
  prioridad normal.
- Una **barra de avance** y el texto muestran cuántas canciones lleva y cuántos ejemplos de IA.
- Sigue corriendo aunque cierres el panel; se detiene con el **mismo botón**.

**Los dos modos se acumulan con límites.** La cosecha pasiva tiene un presupuesto de 192 cuadros
por pasada y hasta ocho pasadas por canción. A fondo permite hasta 1.536 cuadros repartidos
por el tema (ocho veces el presupuesto de una pasada pasiva, no tres). El número aprovechado
depende de la duración, los cortes y los filtros de calidad. `rocola_ia_hechas.txt` registra
el avance para no repetir indefinidamente la aportación de una canción.

El archivo de material tiene un **tope de 24 GiB** (`ia_datos_mb=` en config; 0 lo quita). Con eso
cabe una biblioteca de ~20 000 canciones aptas cosechadas a fondo, y entra holgado en una memoria de
64 GB. Ojo: la memoria tiene que estar en **exFAT o NTFS** — en FAT32 ningún archivo puede pasar de
4 GB, y ahí habría que bajar el tope. En Ctrl+F3 se ve el tamaño actual y si llegó al tope.

**La RAM depende del tamaño de la muestra**: el entrenador mapea el archivo desde disco, pero
materializa una muestra en float64 y copias de reordenación, además de buffers de entrenamiento.
La muestra se reparte por el archivo para no tomar solo las primeras canciones. Se ajusta con
`--max-ejemplos N` (por defecto 4 millones; `0` = todos y puede requerir mucha RAM).

El modo a fondo **no** se guarda en la configuración a propósito: si se va la luz, la rocola vuelve
sola a su modo normal y nunca se queda a todo gas en horario de negocio.

## Luna con relieve, halo y polvo orbital (15-09-2026)

- Nuevo diseno exclusivamente GPU: superficie procedural con mares oscuros, crateres con bordes y sombra lateral; el relieve es aparente, no una simulacion astronomica. La cara nocturna conserva una iluminacion tenue y el exterior del disco es transparente.
- Halo del color del ambiente que respira con los graves y tres trazas pequenas de polvo orbital, dibujadas por delante/detras del disco. La superficie no rebota ni se deforma. El interruptor existente de luna musical controla el movimiento y la respuesta al audio.
- Al atenuarse el fondo durante la navegacion, la luna gana luminosidad y hasta un 22% de diametro siguiendo la misma transicion. Sigue detras de los controles. No depende de que el muro de discos este en reposo.
- Cuatro interruptores persistentes en Ctrl+F3 → Fondo: diseno con crateres, halo, polvo y realce al atenuar. Los tres ultimos requieren el nuevo diseno; al desactivarlo vuelve la luna anterior. El interruptor general de luna sigue ocultando el conjunto.
- Textura fija de 256 × 256 (256 KiB sin contar recursos del controlador), reutilizada entre cuadros y resoluciones. Sin modelos de IA, descargas, shaders nuevos ni reconstruccion por latido. CPU congelado; se conservan .NET 3.5 en Modest y .NET 4.8 en HD.

Validacion: siete baterias, incluida superficie determinista/transparencia/luz lateral, 16 configuraciones lunares, realce, reutilizacion y liberacion de textura en Direct3D 9. No equivale a medir FPS con musica real ni a probar Windows XP.

## Reposo integrado y cola compacta (15-09-2026)

- Desde 1500 px de ancho, las tarjetas de la cola ampliada tienen un máximo de 520 × 79 px y se agrupan arriba, con la carátula y las dos líneas de texto alineadas. Una o dos canciones ya no generan paneles enormes. Se conserva el reparto de las resoluciones bajas y la capacidad de 12 canciones.
- «Controles tenues en reposo (GPU)» atenúa al 2% la barra de letras, el botón Buscar, las instrucciones, el encabezado, sus halos y el contador de FPS cuando se expande el estado. Recuperan su opacidad con la animación de regreso al interactuar. La canción actual, su cola y los avisos permanecen legibles; las vistas activas del buscador y del disco no se atenúan.
- «Ondas unidas al estado (GPU)» hace que el núcleo del horizonte y el borde de la barra compartan posición durante toda la expansión y contracción. Corrige la separación anterior sin reconstruir texturas por cuadro.

Los dos interruptores están en Ctrl+F3 → Fondo, activados por defecto. Se guardan como `controles_reposo` y `ondas_unidas`; apagarlos recupera la visibilidad o separación anterior. Cambios exclusivos GPU; CPU congelado.

`pruebas/validar-capas.ps1` ejecuta seis baterías: incluye 297 disposiciones de cola, 126 posiciones del horizonte verificadas contra su malla, curvas de atenuación a 24–240 Hz y comprobaciones con Direct3D 9. No son mediciones de FPS con la biblioteca real ni pruebas en XP.

## Vidrio, cola compacta y cambios de color (14-09-2026)

Cambios compartidos con Modest/HD, exclusivamente en el renderizador GPU:

- La cola ampliada reparte hasta 12 canciones en una cuadrícula según el ancho, el alto y la cantidad real. No se ocultan las miniaturas en pantallas pequeñas; el título y artista conservan elipsis cuando falta ancho. Con «Cola ampliada de 12 canciones» apagado se conserva la distribución de tres filas.
- «Vidrio por capas y color vivo (GPU)» en Ctrl+F3 → Fondo separa el vidrio, la página y la fila seleccionada. El vidrio sigue el acento musical mediante color de vértice, sin reconstruir el bitmap por cada tono intermedio. Se conserva el hojeo y el aspecto de textos/marcas; desactivarlo recupera la composición anterior. Requiere las opciones de cristal y lista transparente.
- «Cache de transiciones de color (GPU)» hornea el buscador con el color de destino una vez por cambio. La GPU mezcla los extremos durante 420 ms; tocar un control o cambiar el contenido cancela la mezcla para no mantener textos anteriores. La retención adicional está limitada a 64 MiB y se libera al terminar, cerrar o desactivar. Si no cabe, se aplica el destino directamente.
- «Luna musical con orbitas (GPU)» añade desplazamiento, halo y tres estelas orbitales moduladas por nivel, medios y agudos suavizados. Sube con el estado ampliado para no esconderse detrás. Reutiliza la textura lunar y el atlas de halos; no genera bitmaps por cuadro. Requiere la luna habilitada en Detalles. Apagarlo recupera la luna anterior.

Claves persistentes nuevas: `glass_capas`, `colores_cache`, `luna_activa` (1 activado / 0 desactivado).

Validación reproducible en Windows: `powershell -File pruebas/validar-capas.ps1`.
Compila con advertencias como errores y ejecuta pruebas de píxeles, disposición, configuración y recursos D3D9. Los tiempos de preparación son microbenchmarks, **no FPS del programa con música**. Modest sigue apuntando a .NET 3.5/XP, pero no se ha probado en Windows XP. El renderizador CPU permanece congelado; compilarlo solo comprueba que no se rompió el proyecto.

## Colores de fondo y portadas

En Detalles, los tres botones asignan al tono del disco un peso de **20%, 50% y 100%**
en el acento; el resto corresponde al género. Con género, una nebulosa conserva ese tono.
BTS/Bangtan con género reconocido conserva su morado fijo. **Sin género reconocido**,
cualquier peso de portada mayor que cero usa solo el tono disponible del disco, también
en «Mitad y mitad» y para BTS. Apagar el efecto o poner peso cero desactiva esa sustitución.

El género se busca en canción, disco, artista y nombre/carpeta; no se limita a ID3.
El extractor ignora píxeles transparentes y representa el tono dominante/promedio y picos,
manteniendo brillo, viveza, transiciones y degradados del diseño, no el RGB literal.
Sin portada cromática disponible se usa el fondo de reserva.

## Compilar

Proyecto clásico de **.NET Framework 3.5** (Visual Studio). Abre `Rocola.slnx` y compila en
Release. El resultado (`Rocola.exe` + `NAudio.dll`) se copia junto a la carpeta `musica`, como siempre. `NLayer.dll`
(también en `packages/`, sin internet) va **dentro** de `Rocola.exe`: no hay que copiarla ni descargar nada.

- Código: `Rocola/Form1.cs` (monolítico) + `Form1.Designer.cs` + `Program.cs` + `D3D9Star.cs`.
- Dependencia: **NAudio 1.9.0** (net35): la DLL viene **incluida** en `packages/` y el proyecto
  la referencia directo — compila **sin NuGet y sin internet** (ya no hay `packages.config`).

> `bin/`, `obj/`, `.vs/` y el resto del paquete NuGet no se versionan (ver `.gitignore`);
> solo se incluye la DLL de NAudio que el `HintPath` necesita.

## Optimizaciones aplicadas (sin cambiar aspecto ni comportamiento)

1. **Estabilidad — `PlayTrack`**: si varias canciones seguidas no abren (archivos
   movidos/corruptos), antes se apilaba `PlayTrack → PlayNext → PlayTrack…` (posible
   *StackOverflow*). Ahora el `PlayNext` del `catch` va por `BeginInvoke`, que desenreda la pila.
2. **Búsqueda — `Score` / `RecomputeResults`**: `" " + q` y `q.Split(' ')` se calculaban
   dentro de `Score` (corre por **cada** entrada del índice en **cada** tecla). Ahora se calculan
   una sola vez por pulsación (puntaje idéntico), evitando decenas de miles de asignaciones.
3. **Render — `Cube.Draw`**: los tres arreglos de la proyección 3D (`ex`, `depth`, `order`)
   se reutilizan vía `[ThreadStatic]` en vez de asignarse en cada llamada.
4. **Escaneo del disco en UNA pasada** (`ScanFolders`): antes se caminaba el árbol dos
   veces (audio y video) y además se pedía la lista de cada carpeta otra vez para buscar
   la portada — **tres lecturas por carpeta**. Ahora se camina una sola vez y de esa misma
   lista salen las tres cosas. Verificado contra el método anterior: mismas canciones en el
   mismo orden, mismos videos y **la misma portada elegida** en cada carpeta.
