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
G=$(dirname "$(readlink -f "$0")")
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
    *) echo "escena desconocida: $e" ;;
  esac
done
