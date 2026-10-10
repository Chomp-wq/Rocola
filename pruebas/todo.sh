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
#  lineas "OK" y "FALLO" y devuelve el numero de fallos.
# ============================================================================================
cd "$(dirname "$0")" || exit 1
RAIZ="$(cd .. && pwd)"
NAUDIO="$RAIZ/packages/NAudio.1.9.0/lib/net35/NAudio.dll"
# NLayer: el MP3 flotante alternativo (NLayerHd.cs). Va DENTRO de Rocola.exe como recurso, igual que en el .csproj; sin
# esto la tanda no compilaba la rocola de esta rama.
NLAYER="$RAIZ/packages/NLayer.1.12.0/lib/net35/NLayer.dll"
# El dibujo por tarjeta grafica va en dos archivos: D3D9Star.cs es el dibujo (el mismo que en la rama de
# Linux) y D3D9Device.cs es el dispositivo, que es lo unico que cambia de un sistema a otro. WinSistema.cs
# lleva las dos cosas que el dibujo le pregunta a Windows (el refresco de la pantalla y si hay mensajes).
# Los tres de abajo son NUEVOS y van dentro de #if ROCOLA_GPU, asi que en el perfil sin tarjeta grafica
# no compilan nada. Estan aqui porque el .csproj los incluye: si faltaran en esta lista, la tanda estaria
# probando un programa SIN el panel de reposo, sin la vineta y sin las nebulosas naturales, mientras que en
# Windows se instala uno que si los trae. Una prueba que mira otro programa no prueba nada.
FUENTES="../Rocola/Form1.cs ../Rocola/Form1.Ajustes.cs ../Rocola/Form1.Biblioteca.cs ../Rocola/Form1.Buscador.cs ../Rocola/Form1.Datos.cs ../Rocola/Form1.Fondo.cs ../Rocola/Form1.Ia.cs ../Rocola/Form1.Pared.cs ../Rocola/Form1.Portadas.cs ../Rocola/Form1.Sonido.cs ../Rocola/Form1.Video.cs ../Rocola/Form1.Vistas.cs ../Rocola/Form1.Designer.cs ../Rocola/Program.cs ../Rocola/D3D9Star.cs ../Rocola/Rendimiento.cs ../Rocola/D3D9Device.cs ../Rocola/WinSistema.cs ../Rocola/EstadoReposoGpu.cs ../Rocola/AcabadoGpu.cs ../Rocola/CieloNaturalGpu.cs $RAIZ/Rocola/AperturaAudio.cs $RAIZ/Rocola/CargaTrabajo.cs $RAIZ/Rocola/EscaneoMemo.cs $RAIZ/Rocola/LunaGpu.cs $RAIZ/Rocola/Optimizaciones.cs $RAIZ/Rocola/RespuestaGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/EstrellaGpu.cs $RAIZ/Rocola/Estrella3dGpu.cs $RAIZ/Rocola/RelojPantallaGpu.cs $RAIZ/Rocola/AuroraFinaGpu.cs $RAIZ/Rocola/PantallaVivaGpu.cs $RAIZ/Rocola/DiscoVivoGpu.cs $RAIZ/Rocola/SuenaVivoGpu.cs $RAIZ/Rocola/AtraccionGpu.cs $RAIZ/Rocola/EstiloGpu.cs $RAIZ/Rocola/DibujoLigeroGpu.cs $RAIZ/Rocola/BuscadorLigeroGpu.cs $RAIZ/Rocola/FranjaLigeraGpu.cs $RAIZ/Rocola/BuscadorPortadasGpu.cs $RAIZ/Rocola/PrecargaPaginaGpu.cs $RAIZ/Rocola/PaletaCaratulaGpu.cs $RAIZ/Rocola/CreditosServicio.cs"
FUENTES="$FUENTES $RAIZ/Rocola/TextoMovilGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/CaratulasAdaptativas.cs"
FUENTES="$FUENTES $RAIZ/Rocola/CaratulasSeparadasGpu.cs $RAIZ/Rocola/PreviewsIndexadosGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/FluidezAnimaciones.cs $RAIZ/Rocola/FluidezGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/EstilosFondo.cs $RAIZ/Rocola/EspacioNaturalGpu.cs $RAIZ/Rocola/NebulosaNaturalGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/AuroraNaturalGpu.cs $RAIZ/Rocola/OpticaNaturalGpu.cs $RAIZ/Rocola/PreparacionFondoGpu.cs $RAIZ/Rocola/AcabadoNaturalGpu.cs"
FUENTES="$FUENTES $RAIZ/Rocola/FuentesMedidas.cs $RAIZ/Rocola/PlanBiblioteca.cs"
FUENTES="$FUENTES $RAIZ/Rocola/MedidaFluidezGpu.cs"
TMP_PRUEBAS="${TMPDIR:-/tmp}/rocola_pruebas_$$"
FUENTES="$FUENTES $RAIZ/Rocola/AudioControles.cs $RAIZ/Rocola/Sonoridad.cs $RAIZ/Rocola/RellenoMp3.cs $RAIZ/Rocola/SinSilencios.cs $RAIZ/Rocola/SubirTodo.cs $RAIZ/Rocola/NivelDisco.cs $RAIZ/Rocola/BombaSonido.cs $RAIZ/Rocola/DiarioTablas.cs $RAIZ/Rocola/CancionEnMemoria.cs $RAIZ/Rocola/MedidasBitacora.cs $RAIZ/Rocola/PicoReal.cs $RAIZ/Rocola/Realce.cs"
FUENTES="$FUENTES $RAIZ/Rocola/ModoNoche.cs $RAIZ/Rocola/GraveMono.cs"
FUENTES="$FUENTES $RAIZ/Rocola/SinPausa.cs $RAIZ/Rocola/Form1.SinPausa.cs $RAIZ/Rocola/SalidaSonido.cs"
FUENTES="$FUENTES $RAIZ/Rocola/SalidaExclusiva.cs $RAIZ/Rocola/DecodificaFlotante.cs $RAIZ/Rocola/NLayerHd.cs"
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
CPU="ajustes bajo eqauto bajoauto calidad eqia nivel titulo duracion prueba prueba2 prueba3 prueba4 neon fps abc barrido2 espacio busca buscaigual bolsa genero pagina busmov parcial disco ssd etiqueta sala hojea cosecha resol letrero franjas dsee noche respaldo ventana salir carpeta carpetas miniaturas pared sobras gasto historial estrellas limite vacias videos franja colores carriles indice proyecto primera usando escribe reposo cambio apagon unasola entrar iadatos entrenador corta deriva ligera simbolos leer turno cara vuelta respira orden detalles calienta sola duerme teclea palomito moneda nebulosa pedidas tiron cielo fondocoste fugaces luna descompresor pestanas nebfuerza vigia aperturaaudio efectoslayout eqpreparado escaneomemo precarga medidas arranque apollo iaparidad iaorigen"
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
GPU="$GPU naturalestelar nebulosanatural espacionatural espacionaturalhal"
GPU="$GPU auroranatural opticanatural preparacionnatural acabadofinalnatural"
CPU="$CPU ajustesfluidez"
CPU="$CPU estilosconfig"
GPU="$GPU estilosconfig"
# Y las que miran algo que SOLO EXISTE en la rama de tarjeta grafica. La limpieza de videos que no
# suenan es una de esas: para saber si un video suena hay que descomprimir su sonido, y eso alli lo
# hace Media Foundation, que llego con Windows Vista. En la rama de XP no hay con que, asi que ni
# existe la funcion ni se prueba. Lo mismo el remuestreador de la salida moderna (WASAPI): en esta maquina
# la bateria solo comprueba su vara de medir; el remuestreador de Windows se mide en Windows.
SOLO_HD="mudos remuestreo exclusivo flotante"
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
GPU="$GPU espacioprecarga estelaestelar estrellaflujo estrella3d ecosgeometria pantallaviva"
GPU="$GPU relojreposo campoestelarbuffers fluidezcuadros"
# Memoria temporal de medida: rutas comunes de audio, sin cambiar dibujo CPU.
CPU="$CPU memoriaanalisis memoriaintegrada wavdirecto"
GPU="$GPU memoriaanalisis memoriaintegrada wavdirecto"
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

# --- Que version de .NET pide este PROYECTO. No se deduce probando compiladores ni depende de que
#     exista .git: las copias entregadas y los ZIP no tienen rama, pero el .csproj siempre declara
#     el framework que se instala. ---
RAMA="$(git -C "$RAIZ" rev-parse --abbrev-ref HEAD 2>/dev/null)"
PROYECTO="$RAIZ/Rocola/Rocola.csproj"
if [ ! -f "$PROYECTO" ]; then
  echo "  FALTA el proyecto: $PROYECTO"
  exit 1
elif grep -q '<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>' "$PROYECTO"; then
  # 4.8, QUE ES LO QUE SE INSTALA, y no "4" a secas. En Mono, -sdk:4 son las bibliotecas de .NET 4.0, y
  # entre 4.0 y 4.8 hay ocho anos de cosas nuevas: el lector binario que deja el archivo abierto, sin ir
  # mas lejos, llego en 4.5. Con -sdk:4 la rocola de esta rama NO COMPILA aqui... y en Windows si, porque
  # alli el proyecto dice 4.8. O sea que la tanda de pruebas estaba mirando un programa distinto del que
  # se instala, y lo hubiera seguido haciendo hasta que alguien usara algo posterior a 4.0.
  SDK=4.8; TIPO=hd; CPU="$CPU $SOLO_HD"
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
# --- EL ARCHIVO DEL DIBUJO ES EL MISMO QUE EN LA OTRA RAMA DE TARJETA GRAFICA ---
#
# D3D9Star.cs (el dibujo) y D3D9Device.cs (el dispositivo de Direct3D) son identicos byte por byte en
# la rama de Windows potente y en la de Linux, a proposito: asi un cambio en el fondo de estrellas se
# hace UNA vez y se lleva de una copia. Esa propiedad es facil de perder sin darse cuenta -basta con
# tocar uno y olvidar el otro-, asi que se comprueba en cada tanda. La cabecera del archivo lo promete
# y aqui se cumple; un comentario que miente es peor que no tenerlo.
#
# Y SE COMPRUEBA DESDE LAS DOS RAMAS. Esto vivia solo en la de Linux, o sea que la promesa se miraba por
# un lado y por el otro no: tocando el dibujo aqui, nada avisaba.
#
# Si la otra rama no esta a mano (una copia suelta, sin historial) no se dice nada y se sigue.
#
# UNA RAMA APARCADA NO PUEDE DEJAR A LA OTRA SIN PRUEBAS. Si se decide parar el porte a una de las dos
# -aqui, el de Linux- el dibujo se separa a proposito, y entonces esta comprobacion, siendo un fallo
# duro, dejaba la rama que SI se trabaja sin correr NI UNA bateria. Eso es peor que la separacion que
# vigila: la maquina del bar se queda sin red por cuidar una promesa que nadie esta rompiendo.
#
# Asi que se aparca a mano, por nombre, y se avisa EN CADA VUELTA con un cartel que no se puede pasar
# por alto. No se afloja la vigilancia: la pareja que sigue viva sigue siendo un fallo duro, y cuando
# se retome el porte se borra esta linea y vuelve a serlo.
APARCADA="rocola-linux"

case "$RAMA" in
  rocola-hd)    OTRA="rocola-linux" ;;
  rocola-linux) OTRA="rocola-hd" ;;
  *)            OTRA="" ;;
esac
if [ -n "$OTRA" ] && git -C "$RAIZ" rev-parse --verify -q "$OTRA" > /dev/null 2>&1; then
  dif=0
  for f in D3D9Star.cs D3D9Device.cs; do
    if ! git -C "$RAIZ" show "$OTRA:Rocola/$f" 2>/dev/null | diff -q - "$RAIZ/Rocola/$f" > /dev/null 2>&1; then
      echo "  OJO: Rocola/$f NO es igual que en la rama $OTRA (tienen que ser identicos)"
      dif=$((dif+1))
    fi
  done
  if [ $dif -ne 0 ]; then
    if [ "$OTRA" = "$APARCADA" ]; then
      echo "  ---------------------------------------------------------------------------"
      echo "  LA RAMA $OTRA ESTA APARCADA y el dibujo ya NO es el mismo que el de aqui."
      echo "  Se sigue para que esta rama tenga pruebas, pero la promesa de que las dos"
      echo "  comparten el dibujo esta ROTA a proposito. Al retomar el porte hay que"
      echo "  llevarle D3D9Star.cs y D3D9Device.cs y quitar APARCADA de este archivo."
      echo "  ---------------------------------------------------------------------------"
    else
      echo "  FALLA: las dos ramas ya no comparten el mismo codigo de dibujo"
      exit 1
    fi
  else
    echo "  el dibujo por tarjeta grafica es identico al de la rama $OTRA"
  fi
fi

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
echo "  rocola compilada (proyecto $TIPO, rama ${RAMA:-sin-git}, .NET $([ "$SDK" = 2 ] && echo 3.5 || echo "$SDK"), los dos perfiles, 0 avisos)"
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
    xvfb-run -a mono $t.exe rocola.exe > "$rt" 2>&1
    rc=$?
    n=$(grep -c '^  FALLO' "$rt")
    # Las baterias nuevas resumen con una linea "OK ..." sin sangria (y lanzan una excepcion si algo falla).
    ok=$(grep -c '^  OK\|^OK ' "$rt")
    if [ "$rc" -ne 0 ] || [ "$n" -ne 0 ] || [ "$ok" -eq 0 ]; then
      echo "  FALLA        $t   (salida $rc, $n fallos, $ok comprobaciones OK)"
      grep '^  FALLO' "$rt" | head -6
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
# Y NLayer, igual: la rocola lo saca de dentro de si misma solo cuando arranca de verdad (Program.cs); una bateria que
# la carga como biblioteca lo busca al lado.
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
cp -f rocola_cpu.exe rocola.exe || { echo "  No se pudo activar el perfil CPU"; exit 1; }
corre "$CPU"
cp -f rocola_gpu.exe rocola.exe || { echo "  No se pudo activar el perfil GPU"; exit 1; }
corre "$GPU"
cp -f rocola_cpu.exe rocola.exe || { echo "  No se pudo restaurar el perfil CPU"; exit 1; }

# ============================================================================================
#  LAS PRUEBAS DE LAS HERRAMIENTAS DE FUERA (Python)
#
#  Apollo, el entrenamiento del EQ y el comparador A/B traen sus propias pruebas desde hace
#  tiempo, pero vivian SUELTAS: habia que acordarse de lanzarlas a mano, y por tanto no se
#  lanzaban. Aqui se enganchan a la tanda, que es lo unico que se corre de verdad.
#
#  No piden ni torch ni el modelo de Apollo: son de numpy y de la biblioteca de Python. Si esta
#  maquina no tiene numpy se dice y se sigue -el resto de la tanda no depende de ellas-, pero no
#  se da por buena una tanda sin decir que faltaron.
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
