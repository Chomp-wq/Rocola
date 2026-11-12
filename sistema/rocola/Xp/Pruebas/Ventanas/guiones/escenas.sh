#!/bin/bash
# escenas.sh PROGRAMA ETIQUETA [ESCENA ...]: las escenas de prueba de las ventanas con un programa (carpeta con rocola-xp.dll, JIT; o
# aot:CARPETA con el ejecutable de NativeAOT). Cada una deja $SAL/ETIQUETA-ESCENA (ver corre.sh). Sin escenas, todas.
#   arranque   arrancar y mirar (capturas con sus capas)
#   moneda     la moneda mantenida 1,5 s (la repeticion de XP), una cancion y Ctrl+F3 hasta la pestana Imagen con el raton
#   video      un video con su pantalla completa
#   tab        Ctrl+F3: 45 Tab, 12 Mayus+Tab, clic en la pestana Fondo y 25 Tab
#   buscador   el buscador: escribir, borrar, elegir
#   cierreq    Ctrl+Alt+Q tres veces (UserClosing: rocola_estado.txt debe faltar)
#   altf4      Alt+F4 (si XP no la atiende, UserClosing)
#   sigterm    SIGTERM a los 10 s (el apagado del sistema: WindowsShutDown, rocola_estado.txt debe faltar)
#   f3sigterm  SIGTERM con Ctrl+F3 abierto
#   kill9      SIGKILL a los 10 s (el apagon: rocola_estado.txt debe quedar)
# Y las de las teclas (ventanas2, paso T), que no estan en «todas» (pidalas por su nombre):
#   asistente  la ayuda con ROJO + ABAJO (ROJO mantenido 1,5 s y ABAJO: lo ya atendido, GetKeyState, y el estado fisico, GetAsyncKeyState),
#              ABAJO con la ayuda abierta (se lo queda la ayuda: la letra no cambia), ABAJO mantenido 0,8 s y ROJO (la cierra; su
#              repeticion acaba dentro del regreso animado de 0,8 s) y ABAJO (ahora si cambia la letra)
#   captura    F8 y el asistente de los botones (CaptureConfigKey): una tecla mantenida 1,2 s (su repeticion no cuenta), la misma otra vez
#              (ya usada), las otras siete; despues la moneda nueva da creditos; rocola_teclas.txt byte a byte
#   salirq     Ctrl+Alt+Q con la Q mantenida 1,2 s (su repeticion no cuenta: msg.Repetida) y dos Q mas: se cierra (UserClosing)
# Y las de la hoja (ventanas2, paso H: Ctrl+F2, la lista, los avisos), que tampoco estan en «todas»; sus datos los arma datos_h.sh en $DH:
#   f2        Ctrl+F2 con la musica de siempre (cada paso a un segundo o mas del anterior, por la carga de la maquina): la lista con el foco,
#             ABAJO, Tab y Mayus+Tab por los botones (el de Quitar apagado), la moneda
#             con la hoja abierta (MonedaTambienAqui), clics en la lista, en el boton apagado y en Listo; despues se abre y se cierra con Escape,
#             con Alt+F4 y con la «X» (el raton encima y el clic)
#   f2barras  Ctrl+F2 con 21 carpetas de mas (la lista con las dos barras): Fin, derecha, Inicio, AvPag, la rueda, clics en las flechas y el
#             canal de las barras; Quitar una carpeta (Espacio en el boton), los dos avisos de Quitar (sin eleccion y la principal: Intro y
#             Escape) y Listo: rocola_config.txt byte a byte
#   f2sin     sin la carpeta de la musica: la hoja se abre sola, Tab y Escape
#   aviso     la carpeta de datos no se puede usar: el aviso de Program.cs antes de abrir la rocola; Espacio en Aceptar (al apretarlo se
#             compone la pantalla y sale la captura del aviso; al soltarlo se cierra) y la rocola arranca
#   f2sigterm SIGTERM con Ctrl+F2 abierto (el apagado: en la referencia no; se compara con el programa del paso T, con la capa)
#   f2agrega  «Agregar carpeta...»: el selector de carpetas nativo (la referencia contesta Cancelar sin ensenar nada)
# Y las de Ctrl+F3 (ventanas2, paso C: la hoja nativa con sus piezas), que tampoco estan en «todas». Los pasos de cada una los arma
# vistas_f3.py (cada paso separado del anterior, y Ctrl+F3 abierto a los 26 s, con el repaso de la biblioteca acabado):
#   f3vistas  con el raton por sus seis paginas, las hojas de Sonido (Ajustes del sonido, el local, el tono, afinar, la voz, lo que suena
#             ahora, la IA), las tres hojas de Imagen y las cinco hojitas de Performance, las cinco de Fondo y las tres de Pantalla: una
#             captura y un estado (el de XP y el de cada pieza) de cada una de las 26 vistas
#   f3grises  el modo billar encendido (lo que decide va en gris con su motivo) en Ajustes del sonido, el local, el tono y afinar; y otra vez
#             abierta, el foco en un deslizador y en un interruptor (con Tab) y el raton encima de «Listo»
#   f3billar  el modo billar encendido en las hojas que f3grises no pisa (la voz, lo que suena ahora, la IA) y, en la pagina de Sonido,
#             «Repasar la biblioteca a fondo»: la barra de avance (BarraAvance) y su letrero, y otra vez para pararlo
#   f3teclas  Tab hasta un deslizador; Derecha, Izquierda, RePag, Fin, Inicio; la moneda con el foco en el deslizador (MonedaTambienAqui);
#             Mayus+Tab, Espacio e Intro en un interruptor, las flechas entre hermanas; Escape cierra por KeyPreview
#   f3requisitos  las dependencias vivas (AjustesVivos.cs): apagar «Estrella en 3D girando» deja en gris con su «Necesita» lo de la
#             estrella de Pulido (otra hojita); apagar y encender «Barra de progreso neon», el neon intenso de su misma hojita; al salir,
#             rocola_config.txt byte a byte
#   f3guarda  un interruptor, un deslizador y un boton tocados con el raton y la rocola muerta (SIGKILL) con Ctrl+F3 abierto: lo guardado
#             al momento (A2) en rocola_config.txt, byte a byte
#   f3medir   «Medir este equipo» (Imagen): la hoja se cierra, se mide con la pantalla libre y vuelve sola en Imagen (lo que tarda depende
#             de la maquina y de lo medido: MEDIR_ESPERA, 115 de fabrica, son los segundos que la escena espera desde que lo pulsa);
#             «Comparar perfiles» abre el historial de caratulas (otra hoja encima), la moneda con el abierto, Escape lo cierra y Escape
#             cierra Ctrl+F3. Se compara con compara_medir.py (lo medido sale distinto en dos corridas del mismo programa)
#   f3diag    «Diagnostico completo» (Imagen): la hoja se cierra, empieza el diagnostico y una tecla lo corta (la hoja no vuelve)
#   f3raton   (revision del paso C) la rueda y las teclas con Ctrl, Alt y Mayus en un deslizador; el doble clic de XP en un interruptor, en
#             un boton plano y en un boton que cambia de hoja; el raton que entra y sale; y con el modo billar, clics, Tab y el raton en lo gris
#   f3histsigterm  (revision del paso C) SIGTERM con el historial de caratulas abierto encima de Ctrl+F3 (dos modales anidados; el boton
#             solo se enciende tras medir, asi que antes «Medir este equipo», como f3medir) y una moneda con el abierto: el apagado cierra las
#             dos hojas y la rocola (WindowsShutDown) y rocola_estado.txt debe faltar; en la referencia, la diferencia aceptada
#   avisoviejo  (datos de datos_h.sh con avisos_claros=0) dos monedas: el aviso de siempre (HorneaToast y PintaToastHasta de Form1.Ajustes.cs,
#             traducidos en el paso C) en la capa de la tarjeta, a medio segundo de cada moneda (compara_aviso.py mira su pastilla)
# Y las de la ventana de prueba F12 (f12-como-hoja: GpuTestWindow, la hoja no modal con la tarjeta en su cliente), que tampoco estan en «todas»:
#   f12       (con F10 antes: la rocola deja de recuperar el foco, y la ventana, la activa, se queda con las teclas) F12 abre la ventana
#             (tarjeta: el campo estrellado en su cliente) y Escape, F12, Alt+F4 y la «X» la cierran, cada vez abierta otra; con ella abierta la
#             moneda y Ctrl+F2 no llegan a la rocola (la tecla es de la ventana activa); estado y captura de cada paso
#   f12vigia  lo mismo sin F10: el vigia del foco de la rocola (Form1.ForceFocus, cada 1,5 s) le quita el primer plano a la ventana, y su
#             Escape, su Alt+F4 y el clic en la «X» (esa si, porque el clic la activa) dependen de cuando toque el vigia; F12 la cierra siempre
#   f12sigterm  SIGTERM con la ventana F12 abierta (el apagado: FormClosing y FormClosed con WindowsShutDown, rocola_estado.txt borrado)
#   f12fallo  el dispositivo de la ventana no se crea (los sombreadores no compilan: una libGLESv2 de prueba, gl_falla.sh, mientras exista
#             su archivo): el aviso «No se pudo crear el dispositivo D3D9 para la ventana.» sale encima de la ventana, Intro lo acepta y la
#             ventana se cierra; otra vez, con Escape en el aviso
G=$(dirname "$(readlink -f "$0")")
DH=${DH:-/root/rocola-trabajo/port/ventanas2/datos}
W=${W:-/root/rocola-trabajo/port/ventanas}
prog="$1"; et="$2"; shift 2
todas="arranque moneda video tab buscador cierreq altf4 sigterm f3sigterm kill9"
esc="${*:-$todas}"
tab_pulsa=$(python3 "$G/teclas_tab.py" | sed -n 1p); tab_caps=$(python3 "$G/teclas_tab.py" | sed -n 2p)
for e in $esc; do
  case $e in
    arranque)  ESTADO="8.5:fin" "$G/corre.sh" "$prog" "$et-$e" "" "6:a6,9:a9" 12 ROCOLA_CAPTURA_CAPAS=1 ;;
    moneda)    ESTADO="9:a,14:b,16:c,17.5:d" "$G/corre.sh" "$prog" "$et-$e" "7:67~1500,10:13,11.5:13,15:17+114,17:clic:712:53" "16:f3,18:f3img" 21 ROCOLA_CAPTURA_CAPAS=1 ;;
    video)     ESTADO="13:a,15.5:b" "$G/corre.sh" "$prog" "$et-$e" "7:27+39,8.5:27+39,10:13,11:67,12.5:13" "14:v14,16:v16" 19 ROCOLA_CAPTURA_CAPAS=1 ;;
    tab)       ESTADO="14.6:tab45,17.2:shifttab12,21.8:fondo" "$G/corre.sh" "$prog" "$et-$e" "$tab_pulsa" "$tab_caps" 26 ROCOLA_CAPTURA_CAPAS=1 ;;
    buscador)  ESTADO="9:a,12:b,15:c" "$G/corre.sh" "$prog" "$et-$e" "7:67,8:27+38,9.5:72,9.7:79,9.9:76,10.1:65,11:8,12.5:40,13:13,14:27+38" "9.2:b1,12.2:b2,15.2:b3" 17 ROCOLA_CAPTURA_CAPAS=1 ;;
    cierreq)   SALE=0 ESTADO="9:antes" "$G/corre.sh" "$prog" "$et-$e" "7:67,9.5:17~900,9.55:18~800,9.65:81,9.8:81,9.95:81" "" 25 ;;
    altf4)     SALE=0 ESTADO="9:antes" "$G/corre.sh" "$prog" "$et-$e" "7:67,9.5:18+115" "" 25 ;;
    sigterm)   SALE=0 SENAL=TERM ESTADO="9:antes" "$G/corre.sh" "$prog" "$et-$e" "7:67" "" 10 ;;
    f3sigterm) SALE=0 SENAL=TERM ESTADO="9:antes" "$G/corre.sh" "$prog" "$et-$e" "7:67,8:17+114" "" 10 ;;
    kill9)     SALE=0 SENAL=KILL ESTADO="9:antes" "$G/corre.sh" "$prog" "$et-$e" "7:67" "" 10 ;;
    asistente) ESTADO="9:abierta,10.6:abajo,13:cerrada,14.6:abajo2" "$G/corre.sh" "$prog" "$et-$e" "7:27~1500,7.6:40,9.5:40,11:40~800,11.6:27,13.5:40" "15.2:fin" 18 ROCOLA_CAPTURA_CAPAS=1 ;;
    captura)   ESTADO="8:config,13.2:hecha,14.6:moneda" "$G/corre.sh" "$prog" "$et-$e" "7:119,8.5:68~1200,10:68,10.4:69,10.8:70,11.2:71,11.6:72,12:74,12.4:75,12.8:76,13.6:75" "15.2:fin" 18 ROCOLA_CAPTURA_CAPAS=1 ;;
    salirq)    SALE=0 ESTADO="9:antes,10.9:mantenida" "$G/corre.sh" "$prog" "$et-$e" "7:67,9.5:17~2600,9.55:18~2500,9.65:81~1200,11.3:81,11.6:81" "" 25 ;;
    f2)        CORRIDA=v2h-f2 ESTADO="9:abierta,16:moneda,20:cerrada,22.5:esc,25:altf4,29:x" "$G/corre.sh" "$prog" "$et-$e" "7:17+113,9.5:40,11:9,12.5:9,14:16+9,14.8:67,16.5:clic:300:276,18:clic:484:529,19:clic:770:529,20.5:17+113,21.5:27,23:17+113,24:18+115,25.5:17+113,26.5:raton:819:197,28:clic:819:197" "8.5:f2a,10.5:f2b,12:f2c,13.5:f2d,17.5:f2e,27.3:f2f,30:fin" 34 ROCOLA_CAPTURA_CAPAS=1 ;;
    f2barras)  CORRIDA=v2h-f2barras DATOS0="$DH/barras" ESTADO="8.1:abierta,14.4:quitada,15.6:aviso1,18.6:aviso2,20.6:cerrada" "$G/corre.sh" "$prog" "$et-$e" "7:17+113,8.3:35,9.1:39,9.3:39,9.5:39,10.2:36,10.5:34,11.2:raton:500:380,11.3:rueda:-1,11.5:clic:808:473,11.9:clic:808:419,12.3:clic:791:489,13:36,13.2:40,13.4:9,13.7:9,14:32,15:32,16.1:13,16.6:16+9,16.9:16+9,17.2:36,17.5:9,17.8:9,18.1:32,19.1:27,19.6:clic:770:529" "8:b1,8.8:b2,9.9:b3,10.9:b4,12.7:b5,14.6:b6,15.8:b7,18.8:b8,21.5:fin" 25 ROCOLA_CAPTURA_CAPAS=1 ;;
    f2sin)     CORRIDA=v2h-f2sin DATOS0="$DH/sin" ESTADO="9:abierta,11:cerrada" "$G/corre.sh" "$prog" "$et-$e" "9.5:9,10:27" "8.8:s1,9.8:s2,11.5:fin" 14 ROCOLA_CAPTURA_CAPAS=1 ;;
    aviso)     CORRIDA=v2h-aviso SIN_DATOS=1 ESTADO="2.5:aviso,9:rocola" "$G/corre.sh" "$prog" "$et-$e" "3:32~300" "2.95:a1,9.5:fin" 14 ROCOLA_CAPTURA_CAPAS=1 ;;
    f2sigterm) CORRIDA=v2h-f2sigterm SALE=0 SENAL=TERM ESTADO="9:antes" "$G/corre.sh" "$prog" "$et-$e" "7:67,8:17+113" "" 10 ;;
    f2agrega)  CORRIDA=v2h-f2agrega ROCOLA_CARPETAS_RAIZ="$DH/arbol" ESTADO="8.3:selector,10.3:elegida,12:hoja" "$G/corre.sh" "$prog" "$et-$e" "7:17+113,7.6:9,8:32,8.6:39,8.9:40,9.2:39,9.5:40,10.6:13,12.5:clic:770:529" "8.5:g1,10.1:g2,11.5:g3,13.5:fin" 16 ROCOLA_CAPTURA_CAPAS=1 ROCOLA_CARPETAS_RAIZ="$DH/arbol" ;;
    f12)       ESTADO="9.6:abierta,12:esc,16:f12,20:altf4,24.5:x,27.3:moneda,28.8:ctrlf2,31.5:fin" "$G/corre.sh" "$prog" "$et-$e" "7:121,8:123,11:27,13:123,15:123,17:123,19:18+115,21:123,23.3:clic:1014:33,25:123,26.5:67,28:17+113,30:123" "9.7:f12a,12.3:f12b,27.6:f12c,31.6:f12d" 33 ROCOLA_CAPTURA_CAPAS=1 ;;
    f12vigia)  ESTADO="9.6:abierta,12:esc,16:f12,20:altf4,24.5:x,31.5:fin" "$G/corre.sh" "$prog" "$et-$e" "8:123,11:27,13:123,15:123,17:123,19:18+115,21:123,23.3:clic:1014:33,25:123,30:123" "9.7:f12a,12.3:f12b,31.6:f12d" 33 ROCOLA_CAPTURA_CAPAS=1 ;;
    f12sigterm) SALE=0 SENAL=TERM ESTADO="9.6:abierta" "$G/corre.sh" "$prog" "$et-$e" "7:67,8:123" "" 11 ;;
    f12fallo)  [ -f "$W/gl-falla/libGLESv2.so.2" ] || "$G/gl_falla.sh" > /dev/null; fl="$W/gl-falla/falla.flag"; rm -f "$fl"; ( sleep 8.2; touch "$fl" ) &
               ESTADO="9.8:aviso,12.2:cerrada,15.8:aviso2,17.6:cerrada2" "$G/corre.sh" "$prog" "$et-$e" "8.7:123,11:13,14:123,16.2:27" "10:fa,12.5:fb,16:fc,18:fd" 20 ROCOLA_CAPTURA_CAPAS=1 LD_LIBRARY_PATH="$W/gl-falla" F12_FALLA_FLAG="$fl"
               wait; rm -f "$fl" ;;
    f3vistas|f3grises|f3billar|f3teclas|f3requisitos|f3diag|f3medir|f3guarda|f3raton|f3histsigterm)
               v=$(python3 "$G/vistas_f3.py" ${e#f3} ${MEDIR_ESPERA:-}); [ $e = f3guarda ] && export SALE=0 SENAL=KILL; [ $e = f3histsigterm ] && export SALE=0 SENAL=TERM
               ESTADO="$(echo "$v" | sed -n 4p)" "$G/corre.sh" "$prog" "$et-$e" "$(echo "$v" | sed -n 1p)" "$(echo "$v" | sed -n 2p)" "$(echo "$v" | sed -n 3p)" ROCOLA_CAPTURA_CAPAS=1
               unset SALE SENAL ;;
    avisoviejo) DATOS0="$DH/avisoviejo" ESTADO="8.2:a" "$G/corre.sh" "$prog" "$et-$e" "7:67,9:67" "7.55:a1,9.55:a2,11:fin" 14 ROCOLA_CAPTURA_CAPAS=1 ;;
    *) echo "escena desconocida: $e" ;;
  esac
done
