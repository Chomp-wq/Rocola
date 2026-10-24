#!/bin/sh
# ============================================================================================
#  LAS PRUEBAS DE LA ROCOLA
# --------------------------------------------------------------------------------------------
#  Compila la rocola y corre TODAS las baterias contra el ejecutable recien hecho.
#
#    sh pruebas/todo.sh              corre la suite entera
#    sh pruebas/todo.sh disco ssd    corre solo esas
#    sh pruebas/todo.sh --lista      dice cuales hay
#
#  No hace falta preparar nada: detecta solo si esta rama es la de Windows XP (.NET 3.5) o la
#  de tarjeta grafica (.NET 4.8), compila los dos perfiles -con y sin GPU- y arranca.
#
#  Cada bateria es un programa suelto que CARGA el ejecutable de la rocola y le mira las tripas
#  por dentro: mide lo que cuesta algo de verdad, cuenta los pixeles de lo que se pinta, o lee
#  el codigo ya compilado para comprobar que una funcion sigue enchufada donde debe. Escribe
#  lineas "OK" o "PASS" y "FALLO", y devuelve el numero de fallos.
# ============================================================================================
cd "$(dirname "$0")" || exit 1
RAIZ="$(cd .. && pwd)"
NAUDIO="$RAIZ/packages/NAudio.1.9.0/lib/net35/NAudio.dll"
# NLayer: descomprime en coma flotante los MP3 que llegan al maximo digital (FlotanteNLayer.cs). Solo en esta rama.
NLAYER="$RAIZ/packages/NLayer.1.12.0/lib/net35/NLayer.dll"
# Los tres de abajo son NUEVOS y van dentro de #if ROCOLA_GPU, asi que en el perfil sin tarjeta grafica
# no compilan nada. Estan aqui porque el .csproj los incluye: si faltaran en esta lista, la tanda estaria
# probando un programa SIN el panel de reposo, sin la vineta y sin las nebulosas naturales, mientras que en
# Windows se instala uno que si los trae. Una prueba que mira otro programa no prueba nada.
FUENTES="../Rocola/Form1.cs ../Rocola/Form1.Ajustes.cs ../Rocola/Form1.Biblioteca.cs ../Rocola/Form1.Buscador.cs ../Rocola/Form1.Datos.cs ../Rocola/Form1.Fondo.cs ../Rocola/Form1.Ia.cs ../Rocola/Form1.Pared.cs ../Rocola/Form1.Portadas.cs ../Rocola/Form1.Sonido.cs ../Rocola/Form1.Video.cs ../Rocola/Form1.Vistas.cs ../Rocola/Form1.Designer.cs ../Rocola/Program.cs ../Rocola/D3D9Star.cs ../Rocola/Rendimiento.cs ../Rocola/EstadoReposoGpu.cs ../Rocola/AcabadoGpu.cs ../Rocola/CieloNaturalGpu.cs $RAIZ/Rocola/AperturaAudio.cs $RAIZ/Rocola/CargaTrabajo.cs $RAIZ/Rocola/EscaneoMemo.cs $RAIZ/Rocola/LunaGpu.cs $RAIZ/Rocola/Optimizaciones.cs $RAIZ/Rocola/RespuestaGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/EstrellaGpu.cs $RAIZ/Rocola/Estrella3dGpu.cs $RAIZ/Rocola/RelojPantallaGpu.cs $RAIZ/Rocola/AuroraFinaGpu.cs $RAIZ/Rocola/PantallaVivaGpu.cs $RAIZ/Rocola/DiscoVivoGpu.cs $RAIZ/Rocola/SuenaVivoGpu.cs $RAIZ/Rocola/AtraccionGpu.cs $RAIZ/Rocola/EstiloGpu.cs $RAIZ/Rocola/DibujoLigeroGpu.cs $RAIZ/Rocola/PerformanceGpu.cs $RAIZ/Rocola/AjustesVivos.cs $RAIZ/Rocola/UnDisparo.cs $RAIZ/Rocola/PulidoGpu.cs $RAIZ/Rocola/LoQueSuena.cs $RAIZ/Rocola/ArreglosGrabacion.cs $RAIZ/Rocola/BuscadorLigeroGpu.cs $RAIZ/Rocola/FranjaLigeraGpu.cs $RAIZ/Rocola/BuscadorPortadasGpu.cs $RAIZ/Rocola/PrecargaPaginaGpu.cs $RAIZ/Rocola/PaletaCaratulaGpu.cs $RAIZ/Rocola/CreditosServicio.cs"
FUENTES="$FUENTES $RAIZ/Rocola/TextoMovilGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/TexturasCompatiblesGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/CaratulasAdaptativas.cs"
FUENTES="$FUENTES $RAIZ/Rocola/CaratulasSeparadasGpu.cs $RAIZ/Rocola/PreviewsIndexadosGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/FluidezAnimaciones.cs $RAIZ/Rocola/FluidezGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/HojasFondo.cs"
FUENTES="$FUENTES $RAIZ/Rocola/FuentesMedidas.cs $RAIZ/Rocola/PlanBiblioteca.cs"
FUENTES="$FUENTES $RAIZ/Rocola/MedidaFluidezGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/MotorBusqueda.cs $RAIZ/Rocola/GuiaRojoGpu.cs $RAIZ/Rocola/AsistenteRocola.cs $RAIZ/Rocola/AsistenteEstrellaGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/AsistentePersonalidadGpu.cs"
# La casilla de AYUDA y la guia paso a paso de la estrella (11-10-2026, solo GPU).
FUENTES="$FUENTES $RAIZ/Rocola/AyudaCasillaGpu.cs $RAIZ/Rocola/AsistenteGuiaRocola.cs"
TMP_PRUEBAS="${TMPDIR:-/tmp}/rocola_pruebas_$$"
FUENTES="$FUENTES $RAIZ/Rocola/AudioControles.cs $RAIZ/Rocola/Sonoridad.cs $RAIZ/Rocola/RellenoMp3.cs $RAIZ/Rocola/SinSilencios.cs $RAIZ/Rocola/SubirTodo.cs $RAIZ/Rocola/NivelDisco.cs $RAIZ/Rocola/BombaSonido.cs $RAIZ/Rocola/DiarioTablas.cs $RAIZ/Rocola/CancionEnMemoria.cs $RAIZ/Rocola/MedidasBitacora.cs $RAIZ/Rocola/FlotanteNLayer.cs $RAIZ/Rocola/PicoReal.cs $RAIZ/Rocola/Realce.cs $RAIZ/Rocola/Desnormales.cs $RAIZ/Rocola/Siseo.cs $RAIZ/Rocola/Mp3Ligero.cs $RAIZ/Rocola/Pcm16Directo.cs"
FUENTES="$FUENTES $RAIZ/Rocola/ModoNoche.cs $RAIZ/Rocola/GraveMono.cs $RAIZ/Rocola/SalaLocal.cs $RAIZ/Rocola/CieloVivoGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/SinPausa.cs $RAIZ/Rocola/Form1.SinPausa.cs $RAIZ/Rocola/SalidaSonido.cs $RAIZ/Rocola/RecorridoRapido.cs"
# La presencia en sala ruidosa (22-10-2026): el compresor suave detras del local.
FUENTES="$FUENTES $RAIZ/Rocola/PresenciaSala.cs $RAIZ/Rocola/LocalAuto.cs"
# El diagnostico completo de Ctrl+F3 (23-10-2026): el motor, la toma de muestras y el informe, y las escenas con la rocola de verdad.
FUENTES="$FUENTES $RAIZ/Rocola/DiagnosticoCompleto.cs $RAIZ/Rocola/Form1.DiagnosticoCompleto.cs"
if ! mkdir -p "$TMP_PRUEBAS"; then
  echo "  No se pudo crear la carpeta temporal: $TMP_PRUEBAS"
  exit 1
fi
limpia_tmp() { rm -rf "$TMP_PRUEBAS"; }
trap limpia_tmp 0
trap 'exit 2' 1 2 15
C_ROCOLA="$TMP_PRUEBAS/c_rocola.txt"
C_GPU="$TMP_PRUEBAS/c_rocola_gpu.txt"

# Las que miran cosas que solo existen con tarjeta grafica (la aurora, el barrido de luz) van
# contra el perfil de GPU; las demas contra el normal.
CPU="ajustes bajo eqauto bajoauto calidad eqia nivel titulo duracion prueba prueba2 prueba3 prueba4 neon fps abc barrido2 espacio busca buscaigual bolsa genero pagina busmov parcial disco ssd etiqueta sala hojea cosecha resol letrero franjas dsee noche salir carpeta carpetas miniaturas pared sobras gasto historial estrellas limite vacias videos franja colores carriles indice proyecto primera usando escribe reposo cambio apagon unasola entrar iadatos entrenador corta deriva ligera simbolos leer turno cara vuelta respira orden detalles calienta sola duerme teclea palomito moneda nebulosa pedidas tiron cielo fondocoste fugaces luna descompresor pestanas nebfuerza vigia aperturaaudio efectoslayout eqpreparado escaneomemo precarga medidas arranque apollo iaparidad iaorigen"
# 'turno' no va contra el perfil de tarjeta grafica: la cuenta de cuanto falta para tu cancion vive
# en dos funciones puras -reciben numeros y devuelven un numero-, asi que se le pueden dar casos a
# mano sin encender nada.
# 'entrar' sale en las dos: casi todo lo que mira es comun, pero la parte de que la guia se vea CON
# tarjeta grafica solo existe en ese perfil (si no, aparecia en las pruebas y no en el bar).
# 'banco' va aqui porque el medidor de Ctrl+F3 mide el camino de la tarjeta grafica: sin ese perfil ni
# existe la funcion que pone la cancion ni el bucle que la deja respirar.
# 'fugaces' y 'luna' salen en las dos por lo mismo: las dos cosas se ven en las DOS paredes, y la mitad de
# lo que vigilan -que las dos pidan la fugaz y el dibujo de la luna al mismo sitio- solo se puede mirar
# cuando el trozo de tarjeta grafica esta compilado.
GPU="textosgpu fondo anim presencia barrido tope calma entrar mover banco paginar ceremonia respira teclea palomito nebulosa pedidas fondocoste cielohondo fugaces luna mando vineta geombusca capascolor cargaadaptativa cubofila lunadetalle optimizaciones reposounido respuesta hojeoportadas cristalcola panelreposo envios revision20260921 cuboagrupado caratulasadaptativas partescaratulas"

GPU="$GPU tironescaratulas"
GPU="$GPU colacompacta cadenciavisual"
CPU="$CPU ajustesfluidez"
# 'hojasfondo': Ctrl+F3 -> Fondo con un solo fondo, el Legacy (12-10-2026: Espacio natural retirado, sus claves se ignoran).
CPU="$CPU hojasfondo"
GPU="$GPU hojasfondo"
# 'avisosvideo': con un video en ventana (una ventana de Windows encima de la tarjeta) los avisos y la invitacion van
# a la izquierda de el, enteros; sin video, como siempre (barrido del 12-10-2026).
CPU="$CPU avisosvideo"
GPU="$GPU avisosvideo"
# 'lineaestado': la linea de estado no se queda con avisos viejos (avance tardio del escaneo, errores que se van solos).
CPU="$CPU lineaestado"
GPU="$GPU lineaestado"
# 'segundatanda': el reloj de Windows que se atrasa, la vuelta del contador a los 24,9 dias y el motivo del fallo en corto.
CPU="$CPU segundatanda"
GPU="$GPU segundatanda"
# 'terceratanda': las canciones danadas no se vuelven a poner, el motivo del fallo primero y la biblioteca vacia.
CPU="$CPU terceratanda"
GPU="$GPU terceratanda"
# 'cuartatanda': F5/F12 ceden ante un boton de la rocola, el archivo de teclas sin botones repetidos y la cancion que
# se rompe a media cancion.
CPU="$CPU cuartatanda"
GPU="$GPU cuartatanda"
# 'quintatanda': con el rojo mantenido se encienden las teclas de flecha de BUSCAR y AYUDA, sin flechitas sueltas fuera.
CPU="$CPU quintatanda"
GPU="$GPU quintatanda"
# 'sextatanda': los textos del modo atraccion caben enteros en nueve resoluciones (mensajes, ficha, lista de la noche).
CPU="$CPU sextatanda"
GPU="$GPU sextatanda"
# 'septimatanda': AYUDA con su palabra en 1024x768, nada flotando dentro de un disco, controles del asistente, estrella
# caricaturesca, tope de ROJO + ARRIBA/ABAJO y el tubo neon.
CPU="$CPU septimatanda"
GPU="$GPU septimatanda"
# 'octavatanda': la tarjeta a una frecuencia fija (A4), letra ajustada a los pixeles y numeros de letras mas grandes
# (T1, T2).
CPU="$CPU octavatanda"
GPU="$GPU octavatanda"
# 'novenatanda': la moneda con una ventana de ajustes abierta, el toque que despierta la atraccion y el reproductor de
# video sin su menu del boton derecho.
CPU="$CPU novenatanda"
GPU="$GPU novenatanda"
# 'decimatanda': el fundido de entrada (sin golpe seco al empezar o al retomar a media cancion).
CPU="$CPU decimatanda"
GPU="$GPU decimatanda"
# 'desnormales': en el silencio exacto, los restos diminutos de los filtros a cero (Desnormales.cs): la salida queda en
# cero exacto, ningun estado se queda en desnormal y el silencio no cuesta mas que la musica (antes, unas siete veces).
CPU="$CPU desnormales"
GPU="$GPU desnormales"
# 'remuestreoligero': el convertidor de frecuencia (Tarjeta a 44,1/48 kHz y relevo entre tasas) con sus fases ya
# interpoladas y el anillo sin vueltas: los mismos bits que interpolando en cada cuadro, en muchos pares de tasas.
CPU="$CPU remuestreoligero"
GPU="$GPU remuestreoligero"
# 'mejorasia': las mejoras con IA del sonido (13-10-2026): I1 bajo y brillo a la medida de cada cancion (la red del EQ),
# I2 quitar el siseo (Siseo.cs) e I3 los modelos del DSEE por genero. Las tres con su interruptor, apagadas de fabrica.
CPU="$CPU mejorasia"
GPU="$GPU mejorasia"
# 'repasoligero': el repaso a fondo por trozos (13-10-2026): la huella del EQ banda por banda y la sonoridad K dan
# las mismas sumas y la misma memoria de los filtros que la cuenta cuadro a cuadro de antes, bit a bit.
CPU="$CPU repasoligero"
GPU="$GPU repasoligero"
# 'mp3ligero': los MP3 del repaso sin la basura de NAudio (13-10-2026; Mp3Ligero.cs): el descompresor recibe los mismos
# cuadros y salen los mismos bytes que con Mp3FileReader, con saltos, ID3, basura entre cuadros y archivos cortados.
CPU="$CPU mp3ligero"
GPU="$GPU mp3ligero"
# 'compatvieja': la actualizacion sobre una rocola vieja (13-10-2026): configuracion, lo mas sonado, monedas, botones,
# medidas, caches y material de la IA con el formato de las versiones anteriores se leen y se guardan en el nuevo.
CPU="$CPU compatvieja"
GPU="$GPU compatvieja"
# 'performance': el preset Performance de Imagen (13-10-2026; PerformanceGpu.cs): juntar ordenes (la misma imagen, menos
# ordenes), el cielo a media resolucion y el grano con la vineta en una pasada (casi identicos, comparados con un raster de
# referencia), presentar sin esperar al monitor, el medidor renovado (PlanDeAhorros) y los interruptores guardados.
CPU="$CPU performance"
GPU="$GPU performance"
# 'inventario': cuanto cuesta en procesador y memoria un cuadro de GPU capa por capa, y cuanta pantalla pinta (19-10-2026): la base
# de la ronda de optimizacion. Con la tarjeta imitada: no mide la velocidad de una tarjeta, y sin tarjeta dice que no hay nada que medir.
CPU="$CPU inventario"
GPU="$GPU inventario"
# 'huellacuadros': las huellas del cuadro de la tarjeta (19-10-2026). Una tarjeta imitada que ademas PINTA (raster en software) saca de
# cada cuadro los pixeles, el chorro de ordenes y el de ordenes sin lo invisible; compara con las huellas de antes de los ahorros
# automaticos (huellas_modest_2026-10-19.txt) para demostrar que son la misma imagen, y vigila que los ahorros sigan ahorrando.
# Compila una copia instrumentada de las fuentes (huella-compila.sh: relojes y azar de pruebas). Sin tarjeta, dice que no hay nada que medir.
CPU="$CPU huellacuadros"
GPU="$GPU huellacuadros"
# 'tironescambio': los peores cuadros al cambiar de pagina, cancion o modo (20-10-2026): el libro de los ocho peores cuadros por minuto con sus fases
# (solo con diagnostico=1, sin reservas por cuadro) y, segun se vayan haciendo, lo que llega al controlador (precalentado, PreLoad, memoria libre...).
# Tarjeta imitada que solo cuenta; todo sintetico de Linux/Mono. Sin tarjeta, dice que no hay nada que medir.
CPU="$CPU tironescambio"
GPU="$GPU tironescambio"
# 'contextoparada': lo que se apunta junto a una parada de pantalla (19-10-2026): recolecciones desde el apunte anterior, memoria del
# programa y RAM libre, solo cuando hay parada y desde el hilo del vigia.
CPU="$CPU contextoparada"
GPU="$GPU contextoparada"
# 'hojeofino': hojear rapido sin micro tirones (13-10-2026): reloj fino en la pared y el cielo, empujones que se suman, la
# rodilla de la niebla, la lectura de caratulas que reutiliza su arreglo y las paginas de colchon por perfil y JPEG.
CPU="$CPU hojeofino"
GPU="$GPU hojeofino"
# 'cielovivo': el cielo vivo de Legacy (12-10-2026): luna, lluvias, festejo, cometa, profundidad, hora y reflejo (CieloVivoGpu.cs).
CPU="$CPU cielovivo"
GPU="$GPU cielovivo"
# 'ventanaf3': la ventana Ctrl+F3 de verdad, todas sus paginas y hojas: nada fuera de la ventana, nada debajo de Listo,
# textos que caben (con la letra de Mono y con una mas ancha). No son las letras de XP.
CPU="$CPU ventanaf3"
GPU="$GPU ventanaf3 recuperagpu"
# 'ajustesvivos': Ctrl+F3 con dependencias vivas y guardado al momento (A1 y A2, 13-10-2026): lo que depende de otro ajuste
# va en gris y dice que le falta, entre paginas y al momento; sin tarjeta, lo de la tarjeta en gris con un aviso por pagina;
# lo tocado se guarda quieto medio segundo, y un guardado viejo no pisa al nuevo. Y los ajustes retirados ya no estan.
CPU="$CPU ajustesvivos"
GPU="$GPU ajustesvivos"
# 'arreglos': S1 a S5 del sonido (13-10-2026): un canal mudo por los dos, la continua fuera, el estereo estrecho solo si
# hace falta (bit a bit en las canciones sin el defecto), escuchar sin mejoras y lo que el limitador baja, y su hoja.
CPU="$CPU arreglos"
GPU="$GPU arreglos"
# 'fotos1013': las fotos del 13-10-2026: la ayuda se despide sola a los 15 s, «EN ESTA ROCOLA» entero en la ficha del
# modo atraccion y el reflejo de la aurora pegado a la raya del horizonte (el cometa retirado lo mira 'cielovivo').
CPU="$CPU fotos1013"
GPU="$GPU fotos1013"
# 'iasdisparo': 14-10-2026: los modelos de IA en la carpeta IAS (los sueltos pasan alli sin borrar ninguno; si no se
# pudo, se leen donde estan) y ROJO + ARRIBA / ROJO + ABAJO una vez por pulsacion aunque se mantengan apretados.
CPU="$CPU iasdisparo"
GPU="$GPU iasdisparo"
CPU="$CPU asistentecombo buscainteligente"
GPU="$GPU asistentecombo asistentevisual buscainteligente guiarojo"
CPU="$CPU auditoriageneral"
GPU="$GPU auditoriageneral"
# 'ayudaguia': la casilla de AYUDA con su interrogacion y lo que dibuja la guia paso a paso (11-10-2026).
GPU="$GPU ayudaguia"
CPU="$CPU ayudaguia"
# 'caratulasjpeg': interruptor "Caratulas en JPEG" de Ctrl+F3 (11-10-2026); con CPU no cambia nada.
GPU="$GPU caratulasjpeg"
CPU="$CPU caratulasjpeg"
# 'filabaja': la fila de arriba en pantallas bajas y la casilla de AYUDA desde el arranque (11-10-2026).
GPU="$GPU filabaja"
CPU="$CPU filabaja"
# Y las que miran algo que SOLO EXISTE en la rama de tarjeta grafica. La limpieza de videos que no
# suenan es una de esas: para saber si un video suena hay que descomprimir su sonido, y eso alli lo
# hace Media Foundation, que llego con Windows Vista. En la rama de XP no hay con que, asi que ni
# existe la funcion ni se prueba.
SOLO_HD="mudos"
CPU="$CPU id3selectivo tablasarranque historialarranque audiosuave audioestereo sonoridad picoreal realce transiciones sinpausa salida videoaudio gravemono"
# 'nivelpicos': el nivelado que mira los picos de cada cancion y baja un poco las muy fuertes (Sonoridad.cs), con el
# limitador de verdad sobre canciones de prueba.
CPU="$CPU nivelpicos"
# 'soltar': el limitador espera 20 ms antes de soltar (el bajo sale limpio), con el limitador de verdad.
CPU="$CPU soltar"
# 'relleno': los MP3 sin huecos entre pistas (la etiqueta LAME, la medida del descompresor con el clic y el recorte).
CPU="$CPU relleno"
# 'silencios': saltar los silencios largos (SinSilencios.cs): donde suena cada cancion (medida entera, algoritmo 3), la
# tabla, que se salta y que no, el recorte muestra a muestra, el fundido, abrir de verdad, el interruptor y lo guardado.
CPU="$CPU silencios"
# 'subgrave': las bocinas del billar (fotos del 9-10-2026): el subgrave fuera (30 Hz) y el estereo al 60 % (GraveMono.cs).
CPU="$CPU subgrave"
# 'salalocal': el local y sus bocinas (12-10-2026): claridad, partes suaves, balance y la prueba de bocinas (SalaLocal.cs).
CPU="$CPU salalocal"
# 'presenciasala': la presencia en sala ruidosa (22-10-2026; PresenciaSala.cs): el compresor suave detras del local, apagado
# de fabrica (sale bit a bit), sus 2 a 1, tiempos, ganancia de compensacion hasta el techo del limitador y sin chasquidos.
CPU="$CPU presenciasala"
# 'localauto': el local en automatico (23-10-2026; LocalAuto.cs): la tabla por tipo de local y rango de cancion, apagado = los mandos
# tal cual, encendido = decide por cada cancion con su medida, la cadena y los mandos en vivo lo leen, la configuracion y la ventana.
CPU="$CPU localauto"
# 'mp3mezclado': MP3 con frecuencia o canales mezclados (error del 17-10-2026): la reconstruccion con el formato dominante (BuildCleanMp3).
CPU="$CPU mp3mezclado"
# 'modosauto': la revision de los modos automaticos del sonido (23-10-2026; MODOS_AUTOMATICOS_SONIDO_MODEST_2026-10-23.md): la
# configuracion de videos, EQ, graves, S3 y el local (guardar = leer, fuera de rango), los grises de la ventana (S3 con el modo del
# local, la barra de los videos, "a la medida" sin modelo), «Escuchar sin mejoras» en la cancion que se arma mientras dura, el reparto
# de los rangos de la biblioteca y las medidas de las propuestas (solo informan).
CPU="$CPU modosauto"
# 'lra': el rango de sonoridad (LRA, EBU Tech 3342) en la medida del repaso (23-10-2026; Sonoridad.cs, algoritmo 4): los casos de la norma,
# leer como se lea, contra la referencia independiente ref_lra.py, el formato de rocola_sonoridad.txt (lo de antes se lee igual, lo
# posterior se conserva), que las medidas de antes siguen nivelando y el repaso las vuelve a medir, y lo que cuesta. Con
# ROCOLA_EXE_ANTERIOR=<ejecutable de antes> ademas compara el coste y comprueba que ese lee el archivo nuevo sin perder nada.
CPU="$CPU lra"
# 'todojunto': todo el sonido a la vez en la cadena de verdad (nivelado, Subir TODO, realce, DSEE, grave sintetico, grave
# en mono, des-recorte, modo noche, fundido y saltar silencios), y cambiandolo en vivo como desde Ctrl+F3: nada roto, nada
# por encima del techo, sin chasquidos ni muestras perdidas, la cadena siempre como dicen los ajustes, el limitador y el coste.
CPU="$CPU todojunto"
# 'discoparejo': el nivel por disco (NivelDisco.cs): pistas seguidas del mismo disco con la ganancia del disco entero.
CPU="$CPU discoparejo"
# 'bomba': la musica en su propio hilo (BombaSonido.cs). La salida clasica pide la musica desde la pantalla; la cadena la
# lee un hilo aparte que deja un colchon corto hecho, y la pantalla solo copia. Mismo sonido bit a bit, el final y los
# fallos como antes, cuanto espera la pantalla a un hilo atrasado y lo que le cuesta cada bloque.
CPU="$CPU bomba"
CPU="$CPU cierrebomba"
# 'tirones': lo que paraba la pantalla al poner una cancion: el primer segundo de musica lo hace el hilo de la musica y la
# salida arranca cuando esta (sin esperar la pantalla), la tabla de volumen se copia sin rehacer su indice en la pantalla,
# y el techo de "Subir TODO" con medidas nuevas se cuenta en otro hilo.
CPU="$CPU tirones"
# 'estadodetras': los creditos y la cola se escriben en un hilo de fondo, en orden: con el disco ocupado la moneda no
# espera, queda lo ultimo con los mismos bytes, un encargo viejo no resucita un estado borrado y restaurar ve lo encargado.
CPU="$CPU estadodetras"
# 'bitacoradetras': la bitacora (diagnostico, errores, incompatibles) la escribe un hilo de fondo: nadie abre el archivo por
# su cuenta, con el archivo ocupado no se espera ni se pierde nada, lo escrito no cambia y un fallo grave se escribe ya.
CPU="$CPU bitacoradetras"
# 'arranquevivo': al arrancar, la ventana no se queda parada esperando las tablas: el lector avisa al terminar, publicar no
# espera nunca, las teclas de mientras esperan su turno (salir no) y el orden sigue siendo tablas, historial y biblioteca.
CPU="$CPU arranquevivo"
# 'vigiapantalla': la rocola mide siempre cuanto tarda la pantalla en atender; mas de 150 ms a la bitacora con lo que la
# paro, mas de 500 ms siempre a los errores (sin inundarlos); las entradas grandes marcan lo que hacen.
CPU="$CPU vigiapantalla"
# 'ventanareal': la rocola entera con su ventana (Xvfb), una biblioteca y unas tablas de 40.000 canciones generadas, y un
# guion de uso; falla si la pantalla se para mas de 500 ms al arrancar o mas de 150 ms usandola. Tarda cerca de un minuto.
CPU="$CPU ventanareal"
# 'etiquetadelante': la etiqueta ID3 de la siguiente cancion se lee en la lectura por adelantado, no al empezarla; el
# mismo resultado campo por campo, sin abrir el archivo al empezar, y nunca una etiqueta vieja.
CPU="$CPU etiquetadelante"
# 'prioridad': la rocola con prioridad por encima de lo normal (Program.cs): se pone al arrancar la unica rocola, nunca
# "alta" ni "tiempo real", y en ningun otro sitio.
CPU="$CPU prioridad"
# 'diario': las tablas grandes se guardan anadiendo al final solo lo nuevo (DiarioTablas.cs). La primera vez entera y con
# los bytes de siempre; lo nuevo, al final; al leer manda la ultima linea de cada cancion; entera si se quita algo, si
# alguien toco el archivo o si desaparecio; se compacta; una ultima linea a medias o con ceros no se lee. Y cuanto se
# escribe en la primera medicion de 20.000 canciones.
CPU="$CPU diario"
# 'enmemoria': la cancion que suena y la siguiente, en memoria (CancionEnMemoria.cs). Lo mismo desde la memoria que desde
# el archivo (tambien saltando), el cargador (tope, firma, una sola vez), sin disco mientras suena y el enganche.
CPU="$CPU enmemoria"
# 'bitacora': con "diagnostico=1", lo que tardan las tablas del arranque, la biblioteca y cada cancion quedan en
# rocola_diagnostico.txt (MedidasBitacora.cs); sin eso, nada. Y enganchado donde toca.
CPU="$CPU bitacora"
# 'nofinitos': lo que no es un numero (NaN o infinito) no entra en las medidas ni en las tablas. Un WAV con muestras rotas
# se mide igual que con esas muestras en silencio; PublicaMedida, CierraHuella y CierraSonoridad no guardan NaN; y al
# leer las tablas (arranque y respaldo) una fila con NaN no entra, asi que la media del EQ de la biblioteca sale finita.
CPU="$CPU nofinitos"
# 'guardadocortado': un guardado cortado entre sus dos pasos (sin File.Replace) deja el bueno solo como .bak. Al arrancar
# vuelve de su copia (volumen, EQ, ajustes...), se lee entero y guardar despues no pierde lo de antes; el estado no entra.
CPU="$CPU guardadocortado"
# 'firmabiblioteca': una carpeta que no se puede leer ya no tira la firma de la biblioteca entera (sin firma no se guardaba
# y cada arranque recorria el disco completo). Con una carpeta cerrada de verdad (en Linux, el proceso pasa a 'nobody').
CPU="$CPU firmabiblioteca"
# 'firmarapida': la firma de la biblioteca (19-10-2026) sale IGUAL, letra por letra, que la de antes, con el hash ASCII, el orden por nombre y el
# recorrido rapido de CLR 2 (probado con un origen de datos inventado; la parte de Windows, FindFirstFile, no se puede ejecutar aqui).
CPU="$CPU firmarapida"
# 'respaldobytes': el respaldo del arranque comprueba cada copia releyendola y comparandola byte a byte con el origen (antes, dos SHA-256 por
# archivo: 2 a 8 s de procesador en .NET 3.5); una copia estropeada no se publica. De punta a punta solo donde existe FlushFileBuffers.
CPU="$CPU respaldobytes"
# 'cosechaunavez': la cosecha de la IA (19-10-2026) saca el origen una vez por cancion y anexa los canales juntos, con una confirmacion en disco por
# archivo (6 -> 3 en estereo); lo que se guarda es igual, byte a byte, a lo que dejaban los canales anexados uno por uno.
CPU="$CPU cosechaunavez"
# 'objetosmenos': menos objetos de vida larga por cancion (19-10-2026): el espejo del EQ comparte los arreglos, un registro de fuentes en vez de cuatro objetos
# (el mismo archivo rocola_fuentes.txt) y el artista de la carpeta una vez por carpeta (las mismas canciones).
CPU="$CPU objetosmenos"
# 'reproligera': la reproduccion desde la memoria abre el lector ligero de MP3 (Mp3Ligero.cs, antes solo el repaso): con el descompresor de verdad
# (NLayer) el mismo PCM muestra a muestra en cancion entera, saltos, reanudar, relevo sin pausa y con la pantalla preguntando desde otro hilo.
CPU="$CPU reproligera"
# 'lectura16': los 16 bits enteros (MP3 del sistema y WAV de 16 bits) del repaso se leen con Pcm16Directo (20-10-2026): multiplicar por 2^-15 en lugar de dividir
# entre 32768 y sin el barrido de NaN que no puede encontrar nada; los mismos floats, bit a bit, que por SampleChannel de NAudio (65.536 enteros, todos los tamanos
# de lectura, volumen, MP3 de pruebas con el lector de NAudio y el ligero, WAV de 8/16/24/32 bits por la LectorMedida de verdad). Con la rocola anterior como
# segundo argumento, ademas, MeasureFile + MideIntegrada de WAV generados dan los mismos bits.
CPU="$CPU lectura16"
GPU="$GPU lectura16"
# 'entradaligera': el aprendizaje en vivo de una cancion nueva (MideBloqueOriginal, 20-10-2026) por trozos: la huella tonal con HuellaDelTrozo y la sonoridad K de estereo con
# los filtros en variables (KCuadrosEstereo). Comparado con una copia del codigo de antes, con lecturas de 1 a 8191 muestras (casi todas parten un cuadro), de 8 a 96 kHz y de 1 a
# 6 canales, con silencios exactos: todo lo que queda despues de cada lectura es igual bit a bit.
CPU="$CPU entradaligera"
GPU="$GPU entradaligera"
# 'siseorapido': la transformada del quitasiseo (I2) con las etapas de dos en dos (22-10-2026): la misma cuenta, mariposa a mariposa, con los arreglos y el seno con
# su signo en variables. Comparada con una copia del codigo de antes, con ruido, impulsos, continua, ceros con signo, desnormales, desbordes y vectores dispersos como
# los de la vuelta, a 32, 44,1, 48 y 96 kHz (1.024 y 2.048 puntos) y en las dos direcciones: re e im iguales bit a bit; y la etapa entera troceada de cuatro maneras.
CPU="$CPU siseorapido"
GPU="$GPU siseorapido"
# 'picorapido': el detector de pico real del limitador (22-10-2026) con el filtro de 24 coeficientes desenrollado y la parabola (Afina) escrita en su sitio. Bloque tiene que dar
# EXACTAMENTE lo mismo, bit a bit, que Empuja (muestra a muestra, con el bucle de siempre): 10 senales (fuerte, bajita, alterna, a tope, el caso de libro, silencio, ceros con signo,
# desnormales, NaN e infinitos), 3 techos, mono / estereo / 6 canales, troceos de 1 a 4.410 cuadros con el estado pasando de una llamada a otra, y el vaciado del final.
CPU="$CPU picorapido"
GPU="$GPU picorapido"
# 'indicemp3': el indice de cuadros de los MP3, sin la basura de NAudio (22-10-2026; LectorMp3Ligero.Abre): al abrir, el recorrido del archivo que apunta donde empieza cada cuadro se cuenta
# aparte, en dos arreglos de enteros, repitiendo paso a paso el constructor de NAudio. Con un descompresor imitado que apunta todo lo que recibe: el mismo formato para el descompresor
# (tasa de bits media incluida), etiquetas, largo, EL MISMO INDICE (leido por dentro del de NAudio), saltos a muestras exactas y los mismos bytes en ~50 archivos (los de la carpeta,
# con caratula, basura, cortados, ID3v1, dos frecuencias, diminutos y vacios); lo raro cae al constructor de siempre; y abrir una cancion larga deja menos de la cuarta parte de la basura.
CPU="$CPU indicemp3"
GPU="$GPU indicemp3"
# 'eqporbandas': la primera pasada del nivelado (LevelTap, 22-10-2026) por bandas cuando nada cambia: ganancia y mezcla del realce como constantes y el EQ automatico (hasta 8 bandas en
# cascada) banda por banda de punta a punta de cada trozo de 512 cuadros, de dos en dos bandas, con la memoria en variables y los dos canales de un estereo a la vez; la limpieza de restos
# cae en los mismos cuadros. Comparado con el mismo LevelTap cuadro a cuadro (gancho SinBandas): 1, 2, 3 y 6 canales (LFE sin EQ), 44,1 y 48 kHz, EQ de 8, 5, 3, 1 y 0 bandas y sin curva,
# cambios de curva (cruce), de ganancia y de realce a media cancion, un silencio de ceros, -180 dB y lecturas que parten un cuadro: salida y estado interno iguales bit a bit.
CPU="$CPU eqporbandas"
GPU="$GPU eqporbandas"
# 'hilosatrapados': CoverPump (lector de caratulas) y ClavesDelIndice (ayudantes del buscador) atrapan lo inesperado: en CLR 2 una excepcion sin atrapar
# en un hilo cierra la rocola. Se provoca el fallo de verdad; con la rocola de antes el proceso muere.
CPU="$CPU hilosatrapados"
# 'otroarchivo': en la ruta de una cancion hay hoy otro archivo (la completa en lugar de una descarga cortada). Con la
# duracion apuntada del de antes, el fundido bajaba a cero alli y el resto sonaba en silencio: ahora manda el lector.
CPU="$CPU otroarchivo"
CPU="$CPU fuentes"
CPU="$CPU planbiblioteca"
# 'estadopendiente': tras un apagon, una moneda antes de restaurar el estado ya no pisa los creditos y la cola de antes:
# se conservan y se les suma lo de ahora (con la misma regla de las 7 AM).
CPU="$CPU estadopendiente"
# 'franjareloj' solo con tarjeta grafica: el reloj de la cancion repinta y sube solo su recuadro de la franja de abajo
# (FranjaLigeraGpu.cs). Identica a rehacerla entera, pixel a pixel; las cintas no se tocan; lo demas, entera; y la tarjeta
# imitada recibe solo el trozo.
GPU="$GPU franjareloj"
# 'flotantexp': los MP3 en coma flotante con NLayer (solo esta rama): el descompresor, la comparacion con el de siempre,
# los dos frenos (solo las canciones que llegan al maximo y solo si el equipo lo aguanta) y que este conectado.
CPU="$CPU flotantexp"
# 'nlayerdentro': NLayer.dll va dentro de Rocola.exe; en una carpeta sin NLayer.dll al lado se carga igual.
CPU="$CPU nlayerdentro"
GPU="$GPU espacioprecarga estelaestelar estrellaflujo estrella3d ecosgeometria pantallaviva"
GPU="$GPU relojreposo campoestelarbuffers fluidezcuadros"
# Memoria temporal de medida: rutas comunes de audio, sin cambiar dibujo CPU.
CPU="$CPU memoriaanalisis memoriaintegrada wavdirecto"
GPU="$GPU memoriaanalisis memoriaintegrada wavdirecto"
CPU="$CPU corpusacotado discoacotado prefijosguardados flotantesobrantes mp3medidaligera"
GPU="$GPU corpusacotado discoacotado prefijosguardados flotantesobrantes mp3medidaligera"
GPU="$GPU ruidomalla papeleraportadas"
GPU="$GPU fichaportada"
GPU="$GPU estadoeficiente resolucionligera trabajovisible auroraeficiente cachesgraficas cintasligeras coloresligeros perfilesligeros graficosgenerales paletaligera progresoagrupado polvoligero teselasligeras rejillaligera gestosligeros cubosligeros"
# 'pantallaviva' sale en las dos: sin tarjeta grafica solo existe la pestaña de ajustes (se comprueba que se guarda y
# que no hay nada mas); con ella, todo lo de la tanda 1 de la pantalla (PantallaVivaGpu.cs).
CPU="$CPU pantallaviva"
# 'discovivo' tambien sale en las dos: sin tarjeta grafica, la pestaña; con ella, la tanda 2 (DiscoVivoGpu.cs), y la
# lista y la capsula de la cola pintadas por el procesador se comparan pixel a pixel contra rocola_cpu.exe.
GPU="$GPU discovivo"
CPU="$CPU discovivo"
# 'suenavivo' igual: sin tarjeta grafica, los interruptores y que todo siga como antes; con ella, la tanda 3 (SuenaVivoGpu.cs),
# y la capsula de creditos y el panel de siempre pintados por el procesador se comparan contra rocola_cpu.exe.
GPU="$GPU suenavivo"
CPU="$CPU suenavivo"
# 'atraccion' tambien: sin tarjeta grafica, que la cuenta de la noche no se lleva y la pestaña; con ella, la tanda 4
# (AtraccionGpu.cs): el escaparate, lo mas pedido esta noche y su archivo.
GPU="$GPU atraccion"
CPU="$CPU atraccion"
# 'estilo' tambien: sin tarjeta grafica, la pestaña; con ella, la tanda 5 (EstiloGpu.cs): la letra de lejos en la franja
# de abajo (y que las etiquetas de verdad no se tocan) y el color del genero en las portadas y en la barra de letras.
GPU="$GPU estilo"
CPU="$CPU estilo"
# 'dibujoligero' solo con tarjeta grafica: el dibujo mas ligero (DibujoLigeroGpu.cs). Sin borrar la pantalla, sin pintar
# lo transparente y la aurora sin repetir cuentas, comprobado con una tarjeta imitada y un raster de referencia: la
# misma imagen con menos pixeles. Sin tarjeta grafica no hay nada que mirar (el dibujo por procesador no se toca).
GPU="$GPU dibujoligero"
# 'aurorafina' solo con tarjeta grafica: la aurora avanza en cada cuadro (AuroraFinaGpu.cs). La misma cancion por el
# latido de antes y por el cuadro a cuadro: cuantas veces cambia la forma a 60 cuadros por segundo, que se vea igual, que
# no dependa de los cuadros por segundo, el golpe a la hora, las medidas cada 1/60 s, la cuenta de antes bit a bit y la
# reserva. Sin tarjeta grafica no hay aurora (el dibujo por procesador no se toca).
GPU="$GPU aurorafina"
# 'buscaligero' tambien solo con tarjeta grafica: el buscador mas ligero para la tarjeta (BuscadorLigeroGpu.cs). Subir
# solo el trozo que cambia, cerrar sin copiar y abrir sin crear; con la tarjeta imitada de 'dibujoligero'.
GPU="$GPU buscaligero paginasgpu"
# 'buscaportadas', con los dos perfiles: la pantalla viva, tanda 6, el buscador con portadas (BuscadorPortadasGpu.cs). Con
# tarjeta grafica: el reparto, moverse, pulsar y tocar, que la imagen grande solo lleva lo quieto, las imagenes chicas, la
# entrada de las tarjetas, los atajos, las portadas, la tarjeta imitada (tambien con la ventana que sube y baja), lo
# quieto horneado y las cintas de la tarjeta elegida. Sin ella: que no hay nada del nuevo y la pestana.
GPU="$GPU buscaportadas"
GPU="$GPU rendercache cristalcache"
CPU="$CPU buscaportadas"
# 'bloqueos', con el perfil de tarjeta grafica, que es el programa entero (el dibujo por tarjeta incluido): lee el
# programa compilado y sigue todo lo que corre en la pantalla hasta lo que puede esperar (disco, esperas, programas,
# abrir o medir canciones); falla con cualquier sitio nuevo que no este en su lista revisada, y con los de la lista que
# ya no ocurren. Cada sitio de la lista lleva su motivo.
GPU="$GPU bloqueos"
# 'adelantoprioridad': la siguiente cancion pasa antes que las caratulas cuando a la que suena le quedan 45 s o menos
# (22-10-2026): el calentador se salta su espera de 1,5 s, la de las caratulas y la de cada trozo; con mas tiempo, igual.
CPU="$CPU adelantoprioridad"
GPU="$GPU adelantoprioridad"
# 'picomuestras': el limitador de pico real con lecturas de menos de un cuadro (22-10-2026): ya no devuelve 0 (el fin de la
# cancion para quien lo lee) pidiendole una sola muestra en estereo; sale bit a bit lo mismo que pidiendo todo junto.
CPU="$CPU picomuestras"
# 'diagnostico': el diagnostico completo de Ctrl+F3 (23-10-2026; DiagnosticoCompleto.cs): recorre todas las escenas con una tarjeta imitada, mide huecos,
# tirones por fase, procesador por hilo y del sistema, escribe el informe, no guarda ni aplica nada, lo deja todo como estaba, no reserva por cuadro y se cancela.
# Solo con tarjeta grafica; todo sintetico de Linux/Mono (tarjeta imitada), NO mide la rocola real ni Windows XP.
GPU="$GPU diagnostico"

# --- Que version de .NET pide este PROYECTO. No se deduce probando compiladores ni depende de que
#     exista .git: las copias entregadas y los ZIP no tienen rama, pero el .csproj siempre declara
#     el framework que se instala. ---
RAMA="$(git -C "$RAIZ" rev-parse --abbrev-ref HEAD 2>/dev/null)"
PROYECTO="$RAIZ/Rocola/Rocola.csproj"
if [ ! -f "$PROYECTO" ]; then
  echo "  FALTA el proyecto: $PROYECTO"
  exit 1
elif grep -q '<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>' "$PROYECTO"; then
  SDK=4; TIPO=hd; CPU="$CPU $SOLO_HD"
  REF_ID="hd:e3892d6:377e89e:cfdf912"
elif grep -q '<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>' "$PROYECTO"; then
  SDK=2; TIPO=xp
  REF_ID="xp:7f5b443:705c5d3:dc4a42d"
else
  echo "  No se reconoce el TargetFrameworkVersion de $PROYECTO"
  exit 1
fi

if [ "$1" = "--lista" ]; then
  echo "  sin tarjeta grafica: $CPU"
  echo "  con tarjeta grafica: $GPU"
  exit 0
fi

for herramienta in mcs mono xvfb-run; do
  if ! command -v "$herramienta" >/dev/null 2>&1; then
    echo "  FALTA la herramienta necesaria: $herramienta"
    exit 1
  fi
done
if [ ! -f "$NAUDIO" ]; then
  echo "  FALTA NAudio: $NAUDIO"
  exit 1
fi
for f in $FUENTES; do
  if [ ! -f "$f" ]; then
    echo "  FALTA una fuente de la rocola: $f"
    exit 1
  fi
done

echo "  compilando la rocola..."
if ! mcs -sdk:$SDK -langversion:5 -warn:4 -warnaserror+ -target:winexe -out:rocola_cpu.exe \
     -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$NAUDIO" -r:"$NLAYER" -resource:"$NLAYER",Rocola.NLayer.dll $FUENTES > "$C_ROCOLA" 2>&1; then
  echo "  LA ROCOLA NO COMPILA SIN AVISOS:"; tail -12 "$C_ROCOLA"; exit 1
fi
if ! mcs -sdk:$SDK -langversion:5 -warn:4 -warnaserror+ -target:winexe -out:rocola_gpu.exe -d:ROCOLA_GPU \
     -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$NAUDIO" -r:"$NLAYER" -resource:"$NLAYER",Rocola.NLayer.dll $FUENTES > "$C_GPU" 2>&1; then
  echo "  EL PERFIL CON TARJETA GRAFICA NO COMPILA SIN AVISOS:"; tail -12 "$C_GPU"; exit 1
fi
# --- LOS AVISOS SE CUENTAN, Y DE LOS DOS PERFILES ---
# Aqui la rocola se compila a CERO avisos. Antes esta linea decia "0 avisos" siempre, mirara o no: los del
# perfil con tarjeta grafica ni se leian, y por ahi se colo uno sin que nadie se enterara. Ahora se cuentan
# de verdad y, si hay alguno, se ensena cual y donde.
avisos=$(cat "$C_ROCOLA" "$C_GPU" 2>/dev/null | grep -c 'warning CS')
if [ "${avisos:-0}" -ne 0 ]; then
  echo "  FALLA: $avisos AVISO(S) al compilar la rocola (aqui se exige cero):"
  grep -h 'warning CS' "$C_ROCOLA" "$C_GPU" | sed 's/^/    /'
  exit 1
fi
echo "  rocola compilada (proyecto $TIPO, rama ${RAMA:-sin-git}, .NET $([ $SDK = 2 ] && echo 3.5 || echo 4.8), los dos perfiles, 0 avisos)"
echo

# --- Si se pidieron baterias sueltas, se reparten segun a que perfil van ---
if [ $# -gt 0 ]; then
  nc=""; ng=""
  for t in "$@"; do
    case " $GPU " in *" $t "*) ng="$ng $t";; *) nc="$nc $t";; esac
  done
  CPU="$nc"; GPU="$ng"
fi

# El lector del programa ya compilado vive aparte y se compila JUNTO a cada bateria: antes cada una tenia
# su propia copia del recorrido y solo cuatro de quince estaban completas. Un solo sitio que arreglar.
if [ ! -f leercodigo.cs ]; then
  echo "  FALTA la fuente comun de las baterias: leercodigo.cs"
  exit 1
fi
COMUN="leercodigo.cs"

mal=0
corre() {
  for t in $1; do
    if [ ! -f "$t.cs" ]; then
      echo "  FALTA        $t.cs"
      mal=$((mal+1)); continue
    fi
    ct="$TMP_PRUEBAS/c_$t.txt"
    rt="$TMP_PRUEBAS/r_$t.txt"
    apoyo="$COMUN"
    principal=""
    if [ "$t" = "rendercache" ]; then
      apoyo="$apoyo franjareloj.cs buscaportadas.cs"
      principal="-main:RenderCache"
    fi
    if [ "$t" = "resolucionligera" ]; then
      apoyo="$apoyo performance.cs"
      principal="-main:ResolucionLigera"
    fi
    if [ "$t" = "perfilesligeros" ]; then
      apoyo="$apoyo performance.cs"
      principal="-main:PerfilesLigeros"
    fi
    if [ "$t" = "cintasligeras" ]; then
      apoyo="$apoyo performance.cs"
      principal="-main:CintasLigeras"
    fi
    if [ "$t" = "cachesgraficas" ]; then
      apoyo="$apoyo performance.cs"
      principal="-main:CachesGraficas"
    fi
    if [ "$t" = "auroraeficiente" ]; then
      apoyo="$apoyo performance.cs"
      principal="-main:AuroraEficiente"
    fi
    if [ "$t" = "trabajovisible" ]; then
      apoyo="$apoyo performance.cs"
      principal="-main:TrabajoVisible"
    fi
    if [ "$t" = "estadoeficiente" ]; then
      apoyo="$apoyo franjareloj.cs"
      principal="-main:EstadoEficiente"
    fi
    if [ "$t" = "diagnostico" ]; then
      apoyo="$apoyo franjareloj.cs"
      principal="-main:Diagnostico"
    fi
    if ! mcs -sdk:4 -warn:4 -warnaserror+ $principal -out:$t.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$NAUDIO" $t.cs $apoyo > "$ct" 2>&1; then
      echo "  COMPILA MAL  $t"; tail -5 "$ct"; mal=$((mal+1)); continue
    fi
    if grep -q 'warning CS' "$ct"; then
      echo "  CON AVISOS   $t"
      grep 'warning CS' "$ct" | head -5
      mal=$((mal+1)); continue
    fi
    # La ruta del ejecutable va como primer argumento: es la convencion de todas las baterias, y las
    # nuevas de audio y arranque (audiosuave, audioestereo, tablasarranque...) la exigen.
    letra="$LETRA_COMUN"
    case " $LETRA_DEJAVU " in *" $t "*) letra="$LETRA_VIEJA" ;; esac
    if [ -n "$letra" ]; then FONTCONFIG_FILE="$letra" xvfb-run -a mono $t.exe rocola.exe > "$rt" 2>&1
    else xvfb-run -a mono $t.exe rocola.exe > "$rt" 2>&1; fi
    rc=$?
    # Unas baterias escriben "  FALLO ..." (con sangria) y otras "FALLO ..." sin ella: se cuentan las dos.
    n=$(grep -c '^ *FALLO[ :]' "$rt")
    # Algunas baterias resumen con "OK ..." o "PASS ..." sin sangria; sigue siendo obligatorio salir sin fallos.
    ok=$(grep -c '^  OK\|^OK[ :]\|^PASS[ :]' "$rt")
    if [ "$rc" -ne 0 ] || [ "$n" -ne 0 ] || [ "$ok" -eq 0 ]; then
      echo "  FALLA        $t   (salida $rc, $n fallos, $ok comprobaciones OK)"
      grep '^ *FALLO[ :]' "$rt" | head -6
      tail -3 "$rt" | sed 's/^/        /'
      mal=$((mal+1))
    else
      echo "  bien         $t"
    fi
  done
}

# Mono busca NAudio AL LADO del programa que lo usa, asi que se trae aqui desde packages/.
# (No se guarda en el repo: es el mismo archivo que ya vive en packages/.)
if ! cp -f "$NAUDIO" ./NAudio.dll; then
  echo "  No se pudo copiar NAudio.dll junto a las baterias"
  exit 1
fi
# Y NLayer, igual (la rocola de esta rama lo carga al descomprimir en coma flotante).
if ! cp -f "$NLAYER" ./NLayer.dll; then
  echo "  No se pudo copiar NLayer.dll junto a las baterias"
  exit 1
fi

# --- Material de apoyo: se genera solo si falta (nada de esto se guarda en el repo) ---
incluye() { case " $CPU $GPU " in *" $1 "*) return 0 ;; *) return 1 ;; esac; }
# mp3 de prueba (los usan 'disco' y 'etiqueta')
if incluye disco || incluye etiqueta; then
  if [ ! -f mp3s/esperado.txt ]; then
    command -v python3 >/dev/null 2>&1 || { echo "  FALTA python3 para generar los mp3 de prueba"; exit 1; }
    echo "  generando mp3 de prueba..."
    python3 hacemp3.py mp3s || { echo "  No se pudieron generar los mp3 de prueba"; exit 1; }
  fi
  [ -f mp3s/esperado.txt ] || { echo "  Falta mp3s/esperado.txt despues de generarlo"; exit 1; }
fi
# wav de prueba (los usa 'duracion')
if incluye duracion && [ ! -d wav ]; then
  command -v python3 >/dev/null 2>&1 || { echo "  FALTA python3 para generar los wav de prueba"; exit 1; }
  echo "  generando wav de prueba..."
  mkdir -p wav || exit 1
  python3 genwav.py || { echo "  No se pudieron generar los wav de prueba"; exit 1; }
fi
# ejecutables de REFERENCIA: el "antes" con el que se comparan las mejoras del buscador ('buscaigual',
# 'busmov'). Van fijados a un commit concreto; si no estan, se sacan del historial.
necesita_refs=0
incluye buscaigual && necesita_refs=1
incluye buscaperf && necesita_refs=1
incluye busmov && necesita_refs=1
incluye disco && necesita_refs=1
if [ "$necesita_refs" -eq 1 ]; then
  ref_actual=""
  [ -f referencias.version ] && ref_actual=$(cat referencias.version)
  faltan=0
  incluye buscaigual && [ ! -s rocola_ref.exe ] && faltan=1
  incluye buscaperf && [ ! -s rocola_ref.exe ] && faltan=1
  incluye busmov && [ ! -s rocola_prev.exe ] && faltan=1
  incluye disco && [ ! -s rocola_ant.exe ] && faltan=1
  [ "$ref_actual" = "$REF_ID" ] || faltan=1
  if [ "$faltan" -ne 0 ]; then
    echo "  compilando los ejecutables historicos de referencia..."
    if ! sh refs.sh; then
      echo "  FALLA: no se pudieron obtener referencias historicas fiables"
      exit 1
    fi
  fi
  [ "$(cat referencias.version 2>/dev/null)" = "$REF_ID" ] || { echo "  Referencias de otra version del proyecto"; exit 1; }
  incluye buscaigual && [ ! -s rocola_ref.exe ] && { echo "  Falta rocola_ref.exe"; exit 1; }
  incluye buscaperf && [ ! -s rocola_ref.exe ] && { echo "  Falta rocola_ref.exe"; exit 1; }
  incluye busmov && [ ! -s rocola_prev.exe ] && { echo "  Falta rocola_prev.exe"; exit 1; }
  incluye disco && [ ! -s rocola_ant.exe ] && { echo "  Falta rocola_ant.exe"; exit 1; }
fi
# SOLO PARA LAS PRUEBAS EN LINUX: la letra. Lo que mide texto depende de que fuente da Linux al pedir "Segoe UI",
# "Tahoma" o "sans", y no todos los equipos dan la misma. Las referencias pixel a pixel mas nuevas (huellacuadros del
# 19-10, tironescambio del 20-10) y los textos ajustados de sextatanda, filabaja y paginar se sacaron con Inter; las
# baterias mas viejas de textos (las de LETRA_DEJAVU) se calibraron con DejaVu Sans. Con una sola letra para todas,
# la mitad fallaba sin que nada estuviera roto. Asi que cada una corre con la suya, si esa fuente esta instalada (si
# no, la del sistema). Nada de esto llega a la rocola: en XP manda su propia letra, que aqui no existe.
LETRA_DEJAVU="abc moneda colacompacta estilo discovivo"
hazletra() {   # $1 = familia, $2 = archivo
  printf '%s\n' '<?xml version="1.0"?>' '<!DOCTYPE fontconfig SYSTEM "fonts.dtd">' '<fontconfig>' \
    '  <include ignore_missing="yes">/etc/fonts/fonts.conf</include>' \
    "  <match target=\"pattern\"><test qual=\"any\" name=\"family\"><string>sans-serif</string></test><edit name=\"family\" mode=\"prepend\" binding=\"strong\"><string>$1</string></edit></match>" \
    "  <match target=\"pattern\"><test qual=\"any\" name=\"family\"><string>sans</string></test><edit name=\"family\" mode=\"prepend\" binding=\"strong\"><string>$1</string></edit></match>" \
    '</fontconfig>' > "$2"
}
LETRA_COMUN=""; LETRA_VIEJA=""
if [ -z "$FONTCONFIG_FILE" ] && command -v fc-list >/dev/null 2>&1; then
  if fc-list | grep -q ': Inter[:,]'; then hazletra "Inter" "$TMP_PRUEBAS/letra_inter.conf" && LETRA_COMUN="$TMP_PRUEBAS/letra_inter.conf"; fi
  if fc-list | grep -q 'DejaVu Sans'; then hazletra "DejaVu Sans" "$TMP_PRUEBAS/letra_dejavu.conf" && LETRA_VIEJA="$TMP_PRUEBAS/letra_dejavu.conf"; fi
fi
# AVISO UNA SOLA VEZ, al empezar: que letra usa cada grupo y cual falta. Sin la fuente calibrada, la bateria corre con la letra
# del sistema y sus referencias de texto pueden salir rojas sin que nada este roto (o verdes sin comprobar lo mismo).
if [ -n "$FONTCONFIG_FILE" ]; then
  echo "  AVISO letra: FONTCONFIG_FILE ya viene puesto ($FONTCONFIG_FILE): TODAS las baterias usan esa letra, no Inter ni DejaVu Sans."
  echo "        Las calibradas con Inter o con DejaVu Sans pueden dar rojos enganosos."
elif ! command -v fc-list >/dev/null 2>&1; then
  echo "  AVISO letra: no hay fc-list; TODAS las baterias usan la letra del sistema (ni Inter ni DejaVu Sans): posibles rojos enganosos en las de texto."
else
  if [ -n "$LETRA_COMUN" ]; then echo "  letra: Inter para las baterias de texto de la tanda nueva (huellacuadros, tironescambio, sextatanda, filabaja, paginar...)"
  else echo "  AVISO letra: FALTA Inter; las baterias calibradas con Inter (huellacuadros, tironescambio, sextatanda, filabaja, paginar...) usan la letra del sistema y pueden dar rojos enganosos."; fi
  if [ -n "$LETRA_VIEJA" ]; then echo "  letra: DejaVu Sans para $LETRA_DEJAVU"
  else echo "  AVISO letra: FALTA DejaVu Sans; las baterias calibradas con ella ($LETRA_DEJAVU) usan la letra del sistema y pueden dar rojos enganosos."; fi
fi
# SOLO PARA LAS PRUEBAS EN LINUX (Mono): la rocola copia memoria con RtlMoveMemory, de kernel32.dll, que Mono no tiene.
# Sin este mapeo a memmove la tarjeta imitada no puede subir texturas y las baterias que cargan el ejecutable tal cual
# (trabajovisible, auroraeficiente, resolucionligera, cintasligeras, estadoeficiente) fallan sin que nada este roto.
# El .config va junto al rocola.exe de pruebas (ignorado por git); nunca se copia a un Release ni al programa instalado.
if command -v mono >/dev/null 2>&1; then
  printf '%s\n' '<?xml version="1.0" encoding="utf-8"?>' '<configuration>' '  <dllmap dll="kernel32.dll">' \
    '    <dllentry dll="libc.so.6" name="RtlMoveMemory" target="memmove"/>' '  </dllmap>' '</configuration>' > rocola.exe.config
fi
cp -f rocola_cpu.exe rocola.exe || { echo "  No se pudo activar el perfil CPU"; exit 1; }
corre "$CPU"
cp -f rocola_gpu.exe rocola.exe || { echo "  No se pudo activar el perfil GPU"; exit 1; }
corre "$GPU"
cp -f rocola_cpu.exe rocola.exe || { echo "  No se pudo restaurar el perfil CPU"; exit 1; }

# ============================================================================================
#  LAS PRUEBAS DE LAS HERRAMIENTAS DE FUERA (Python)
#
#  El entrenamiento del EQ trae sus propias pruebas, pero vivian SUELTAS: habia que acordarse de
#  lanzarlas a mano, y por tanto no se lanzaban. Aqui se enganchan a la tanda, que es lo unico
#  que se corre de verdad. (Apollo y su comparador A/B son solo de la rocola potente, asi que en
#  esta rama esa carpeta no existe y el bucle se la salta sola.)
#
#  No piden torch ni modelos: son de numpy y de la biblioteca de Python. Si esta maquina no tiene
#  numpy se dice y se sigue, pero no se da por buena una tanda sin decir que faltaron.
# ============================================================================================
if python3 -c "import numpy" >/dev/null 2>&1; then
  for d in "$RAIZ/ia/upscale" "$RAIZ/ia"; do
    [ -d "$d" ] || continue
    for f in "$d"/test_*.py; do
      [ -f "$f" ] || continue
      t=$(basename "$f" .py)
      rt="$TMP_PRUEBAS/py_$t.txt"
      # test_util es un script autonomo: al importarlo unittest ve SystemExit(0).
      if (cd "$d" && if [ "$t" = "test_util" ]; then python3 "$f"; else python3 -m unittest "$t"; fi) > "$rt" 2>&1; then
        echo "  bien         $t   (python)"
      else
        echo "  FALLA        $t   (python)"
        tail -6 "$rt" | sed 's/^/        /'
        mal=$((mal+1))
      fi
    done
  done
else
  echo "  SIN NUMPY: las pruebas de las herramientas de fuera no se corrieron en esta maquina"
fi

echo
if [ $mal -eq 0 ]; then echo "TODAS LAS BATERIAS EN VERDE"; else echo "BATERIAS CON PROBLEMAS: $mal"; fi
exit $mal
