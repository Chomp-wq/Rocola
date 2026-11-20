#!/bin/bash
# nivel0.sh [ETIQUETA]: LA BATERIA DEL NIVEL 0 DE LA FASE 11 (PLAN_FASE11.md §2.6), la puerta de cada cambio: rocola-xp SIN ROCOLA_OS
# contra la copia congelada de la referencia (aef2211), como en la ultima junta del port (verifica-junta17.sh y alterna.sh, que vivian en el
# scratchpad de la sesion del port; PORT_JUNTAS.md, la junta de N7), con lo que el plan le anade: cierre_rocola_os.sh y Pruebas/Importa en
# JIT y en NativeAOT, las cintas de la tarjeta y el arnes de la frontera, Codigo/ con sus lineas y CRLF, y ganchos.py y diff-xp.py
# --sin-ganchos (si ya estan: los hace el grupo andamio). Con NIVEL=1, el nivel 1: las escenas y los recorridos con ROCOLA_OS puesto a una
# lista, una carpeta /run de prueba vacia, sin red y comparando sin los relojes y encargos «RocolaOs:».
#
# Al final, un resumen con una linea por comprobacion (BIEN, ACEPTADO con su motivo, MAL, FALTA o NO, con sus numeros y lo que tardo), en
# la salida y en $TRABAJO/resumen.txt; todo lo demas, en $TRABAJO/nivel0.log y $TRABAJO/registros/. Sale con 0 solo si todo es BIEN o
# ACEPTADO; con 1 si algo es MAL; con 2 si nada es MAL pero algo no se pudo comprobar (FALTA o NO). Lo que es cada paso, lo que tarda y como
# rehacer lo que lee de fuera del repositorio: LEEME.md (en esta carpeta).
#
# Lo pesado (compilar, publicar, los arneses, el banco, las cintas, los recorridos, cada par de escenas, las pruebas de la hoja y la
# pantalla, el cierre y el importador) toma el candado PESADO paso por paso (y los recorridos, dentro, el suyo), siempre con «flock -o»: lo
# que se abre debajo (el servidor de compilacion de dotnet, una rocola) no se queda con el candado.
#
# PARAMETROS (variables de entorno; entre parentesis, el valor de fabrica):
#   ETIQUETA  (el primer argumento, o la fecha) para no chocar con otras corridas
#   TRABAJO   la carpeta de trabajo (/root/rocola-trabajo/fase11/nivel0-ETIQUETA)
#   XP        la copia de trabajo a comprobar, la carpeta sistema/rocola/Xp (la de este guion)
#   PROG      el programa en JIT ya compilado (la carpeta con rocola-xp.dll); vacio: el paso «compila» lo compila desde cero de XP en
#             $TRABAJO/jit
#   AOT       la publicacion NativeAOT ya hecha (la carpeta con el ejecutable rocola-xp); vacio: el paso «aot» la publica de XP en $TRABAJO/aot
#   REF       la referencia con trazas, compilada (/root/rocola-trabajo/port/ventanas/ref-trazas/sistema/rocola/Xp/bin/Release/net10.0)
#   REFXP     la referencia congelada, el codigo (/root/rocola-trabajo/referencia-xp): la compilan los arneses, el banco y la frontera
#   CMPREF CMPREF2  las dos corridas de referencia de los recorridos (/root/rocola-trabajo/port/dibujo/salidas/cmp-ref y cmp-ref2)
#   LETRAS    las letras de XP falsas (/root/rocola-trabajo/port/dibujo/letras-xp-falsas)
#   RUIDO     el ruido medido de la referencia, una o varias carpetas con ESCENA.json, la primera que lo tenga
#             (/root/rocola-trabajo/port/ventanas2/ruido y /root/rocola-trabajo/port/ventanas/ruido)
#   DATOS0    los datos de prueba de las escenas (/root/rocola-trabajo/port/ventanas/datos0)
#   DH        los datos de las escenas de la hoja (/root/rocola-trabajo/port/ventanas2/datos)
#   DIBUJO    la carpeta de trabajo de los recorridos, ROCOLA_TRABAJO (/root/rocola-trabajo/port/dibujo: su corrida/ tiene que ser la misma
#             ruta que la de cmp-ref, porque la rocola ensena rutas; por eso su candado)
#   TARJETA   las cintas de la tarjeta y sus salidas de referencia (/root/rocola-trabajo/port/tarjeta)
#   PESADO    el candado de lo pesado (/root/rocola-trabajo/fase11/pesado.lock)
#   CANDADO_RECORRIDOS  el de los recorridos (/root/rocola-trabajo/port/recorridos.lock)
#   PASOS     los pasos, en este orden (de fabrica todos: previo codigo ganchos compila arneses banco cintas recorridos ventanas escenas aot
#             cierre importa; con NIVEL=1: previo compila escenas aot recorridos)
#   ESCENAS   las escenas en JIT (las 24 de la junta 17); ESCENAS_AOT, las de NativeAOT (las 7); RECORRIDOS, los recorridos (los nueve)
#   NIVEL     0 (de fabrica) o 1; ROCOLA_OS_LISTA, la lista del nivel 1 (vida,red); IGNORA, el prefijo que no se compara en el nivel 1
#             (RocolaOs:); SIN_RED (1: el programa del nivel 1 en otro espacio de red, si se puede)
#   BASE      el commit contra el que se cuentan las lineas y los CRLF de Codigo/ (ebdb272: fc7b667, la base de la fase 11, con los cambios de la rocola de
#             XP del 17 y 18-11-2026, PORT_xp-1718.md, y el modo noche de 50 a 90 % del 20-11-2026; antes 69e6547)
#   REPITE    1: una escena que sale distinta solo en cuentas (o un recorrido que no da lo de siempre) se repite una vez; 0: no
#   GUARDA_BIN  1: deja los programas compilados y publicados ($TRABAJO/jit, aot); de fabrica se borran al acabar
#   Lo que se espera (lo de la junta de N7; mas, si un grupo amplia una prueba, tambien vale): AVISOS (13), DIFFXP («5135 4868 229»: lineas
#   quitadas, puestas y sin marca de diff-xp.py en los 112 archivos; 5095 4828 229 en los 109 antes de PORT_xp-1718.md), ACABADO (6603), VISTASB (3108), AJUSTES (545), FRONTERA (106 lineas,
#   153 texturas), CINTAS (29), HOJA (86), PANTALLA (32), VISTAS (78), CIERRE (43), IMPORTA (145)
set -u
AQUI=$(cd "$(dirname "$(readlink -f "$0")")" && pwd)
XP=${XP:-$(cd "$AQUI/../.." && pwd)}; XP=$(cd "$XP" && pwd)
P=$XP/Pruebas; GV=${GUIONES:-$P/Ventanas/guiones}; RC=$P/Recorridos
ETIQUETA=${1:-${ETIQUETA:-}}; [ -n "$ETIQUETA" ] || ETIQUETA=$(date +%Y%m%d-%H%M%S)
TRABAJO=${TRABAJO:-/root/rocola-trabajo/fase11/nivel0-$ETIQUETA}
PROG=${PROG:-}; AOT=${AOT:-}
REF=${REF:-/root/rocola-trabajo/port/ventanas/ref-trazas/sistema/rocola/Xp/bin/Release/net10.0}
REFXP=${REFXP:-/root/rocola-trabajo/referencia-xp}
CMPREF=${CMPREF:-/root/rocola-trabajo/port/dibujo/salidas/cmp-ref}; CMPREF2=${CMPREF2:-/root/rocola-trabajo/port/dibujo/salidas/cmp-ref2}
LETRAS=${LETRAS:-/root/rocola-trabajo/port/dibujo/letras-xp-falsas}
RUIDO=${RUIDO:-/root/rocola-trabajo/port/ventanas2/ruido /root/rocola-trabajo/port/ventanas/ruido}
DATOS0=${DATOS0:-/root/rocola-trabajo/port/ventanas/datos0}; DH=${DH:-/root/rocola-trabajo/port/ventanas2/datos}
DIBUJO=${DIBUJO:-/root/rocola-trabajo/port/dibujo}; TARJETA=${TARJETA:-/root/rocola-trabajo/port/tarjeta}
PESADO=${PESADO:-/root/rocola-trabajo/fase11/pesado.lock}; CANDADO_RECORRIDOS=${CANDADO_RECORRIDOS:-/root/rocola-trabajo/port/recorridos.lock}
NIVEL=${NIVEL:-0}; ROCOLA_OS_LISTA=${ROCOLA_OS_LISTA:-vida,red}; IGNORA=${IGNORA:-RocolaOs:}; SIN_RED=${SIN_RED:-1}
if [ "$NIVEL" = 1 ]; then PASOS=${PASOS:-previo compila escenas aot recorridos}
else PASOS=${PASOS:-previo codigo ganchos compila arneses banco cintas recorridos ventanas escenas aot cierre importa}; fi
ESCENAS=${ESCENAS:-arranque moneda video tab buscador cierreq altf4 sigterm f3sigterm kill9 asistente captura salirq f2 f2barras f2sin aviso f3vistas f3grises f3teclas f3raton f3billar f12 f12vigia}
ESCENAS_AOT=${ESCENAS_AOT:-arranque cierreq kill9 buscador f3vistas f3teclas f12}
RECORRIDOS=${RECORRIDOS:-}   # (vacio: todos los que tienen salida de referencia en CMPREF)
BASE=${BASE:-ebdb272}; REPITE=${REPITE:-1}; GUARDA_BIN=${GUARDA_BIN:-0}
AVISOS=${AVISOS:-13}; DIFFXP=${DIFFXP:-5135 4868 229}; ACABADO=${ACABADO:-6603}; VISTASB=${VISTASB:-3108}; AJUSTES=${AJUSTES:-545}
FRONTERA=${FRONTERA:-106 153}; CINTAS=${CINTAS:-29}; HOJA=${HOJA:-86}; PANTALLA=${PANTALLA:-32}; VISTAS=${VISTAS:-78}; CIERRE=${CIERRE:-43}
IMPORTA=${IMPORTA:-145}
# las letras y la referencia que los doce arnes.sh de Vistas* leen siempre de estos sitios (fijos en sus guiones)
LETRAS_FIJAS=/root/rocola-trabajo/port/dibujo/letras-xp-falsas; REFXP_FIJA=/root/rocola-trabajo/referencia-xp

# el nivel 0 es SIN ROCOLA_OS (y sin su /run de prueba), aunque quien lo llama lo tenga puesto
unset ROCOLA_OS ROCOLA_RUN
REG=$TRABAJO/registros; SAL=$TRABAJO/salidas; WV=$TRABAJO/ventanas; RES=$TRABAJO/resumen.txt
mkdir -p "$REG" "$SAL" "$WV" "$(dirname "$PESADO")" || exit 2
: > "$RES"
exec > >(tee -a "$TRABAJO/nivel0.log") 2>&1
# lo que leen escenas.sh, corre.sh, compara.sh, cierre_rocola_os.sh y alterna.sh
ESPERAS=$REG/esperas.txt; : > "$ESPERAS"
export W=$WV SAL DATOS0 DH PESADO GUIONES=$GV ESPERAS
if [ "$NIVEL" = 1 ]; then export ROCOLA_OS_NUEVO=$ROCOLA_OS_LISTA RUN_BASE=$TRABAJO/run SIN_RED; else unset ROCOLA_OS_NUEVO; fi
N=$NIVEL   # (las etiquetas de las corridas: R0/N0 la referencia y el programa en JIT, RA0/NA0 en NativeAOT; R1/N1... en el nivel 1)
DOTNET_BIN=$(readlink -f "$(command -v dotnet)" 2> /dev/null)

apunta() {   # apunta VEREDICTO PASO DETALLE [SEGUNDOS]: con los segundos, tambien los que se espero el candado desde la anterior
  local e=""
  if [ -n "${4:-}" ] && [ -s "$ESPERAS" ]; then e=$(awk '{s+=$1} END {printf "%d", s}' "$ESPERAS"); : > "$ESPERAS"; fi
  printf '%-9s %s: %s%s\n' "$1" "$2" "$3" "${4:+  [$4 s${e:+, $e esperando el candado}]}" | tee -a "$RES"
}
cabeza() { echo; echo "== $1 ($(date +%T); disco libre $(df -h / | awk 'NR==2{print $4}'))"; }
# el candado de lo pesado, con -o (lo que abre el paso no lo hereda); lo que se espero, a $ESPERAS
pesado() { flock -o "$PESADO" bash -c 'echo "$(date +%s) - $1" | bc >> "$2"; shift 2; exec "$@"' pesado "$(date +%s)" "$ESPERAS" "$@"; }
cuenta() { grep -cE "$1" "$2" 2> /dev/null || true; }
ruido_de() { local d; for d in $RUIDO; do [ -f "$d/$1.json" ] && { echo "$d/$1.json"; return; }; done; }
# el veredicto de una escena (clasifica.py) y lo apunta; deja el veredicto en $VEREDICTO
juzga() {   # juzga ESCENA ETQ_REF ETQ_NUEVO
  local r; r=$(ruido_de "$1")
  local op=(--guiones "$GV" --informe "$REG/compara-$3-$1.txt"); [ -n "$r" ] && op+=(--ruido "$r"); [ "$NIVEL" = 1 ] && op+=(--ignora "$IGNORA")
  python3 "$AQUI/clasifica.py" escena "$1" "$SAL/$2-$1" "$SAL/$3-$1" "${op[@]}"
}
# las escenas alternadas con la referencia, su veredicto y, si solo difieren en cuentas, otra vez
escenas_con() {   # escenas_con NOMBRE_DEL_PASO PROGRAMA ETQ_REF ETQ_NUEVO ESCENA...
  local paso=$1 prog=$2 er=$3 en=$4; shift 4
  local t0=$SECONDS e v d otra=() a b
  bash "$AQUI/alterna.sh" "$REF" "$prog" "$er" "$en" "$@" >> "$REG/$paso.log" 2>&1
  declare -A V D
  for e in "$@"; do IFS=$'\t' read -r v d < <(juzga "$e" "$er" "$en"); V[$e]=$v; D[$e]=$d; [ "$v" = CUENTA ] && otra+=("$e"); done
  if [ ${#otra[@]} -gt 0 ] && [ "$REPITE" = 1 ]; then
    echo "   otra vez, por las cuentas: ${otra[*]}" >> "$REG/$paso.log"
    bash "$AQUI/alterna.sh" "$REF" "$prog" "${er}b" "${en}b" "${otra[@]}" >> "$REG/$paso.log" 2>&1
    for e in "${otra[@]}"; do
      IFS=$'\t' read -r v d < <(juzga "$e" "${er}b" "${en}b")
      case $v in BIEN|ACEPTADO) D[$e]="$d, a la segunda (la primera, ${D[$e]})"; V[$e]=$v;;
                 *) D[$e]="$d (y la primera, ${D[$e]})"; V[$e]=MAL;; esac
    done
  fi
  for e in "$@"; do a=${V[$e]}; [ "$a" = CUENTA ] && a=MAL; apunta "$a" "$paso $e ($er/$en)" "${D[$e]}"; done
  b=0; for e in "$@"; do case ${V[$e]} in BIEN|ACEPTADO) b=$((b + 1));; esac; done
  echo "   $paso: $b de $# BIEN o aceptadas en $((SECONDS - t0)) s" | tee -a "$REG/$paso.log"
}
# borra las capturas de las escenas que salieron bien o con lo aceptado (quedan la traza, los estados y lo demas; de las otras, todo)
limpia_escenas() {   # limpia_escenas PASO ETQ_REF ETQ_NUEVO
  local f e
  for f in "$SAL/$3"-*; do
    [ -d "$f" ] || continue
    e=${f##*/}; e=${e#"$3"-}
    grep -qE "^(BIEN|ACEPTADO) +$1 $e \\($2/$3\\):" "$RES" && rm -f "$SAL/$2-$e"/*.png "$SAL/$3-$e"/*.png "$SAL/${2}b-$e"/*.png "$SAL/${3}b-$e"/*.png
  done
}

# ======================================================================================================================================
paso_previo() {
  cabeza "previo: lo de fuera del repositorio y las herramientas"
  local t0=$SECONDS falta=() nota=() h
  for h in dotnet python3 git compare convert bc flock unshare ffmpeg; do command -v $h > /dev/null || falta+=("la orden $h"); done
  command -v strace > /dev/null || nota+=("sin strace: Pruebas/Importa no hace sus dos apagones")
  [ "$(id -u)" = 0 ] || nota+=("sin ser root: Pruebas/Importa no corre (unshare y mount)")
  [ -f "$REF/rocola-xp.dll" ] || falta+=("la referencia con trazas ($REF)")
  [ -f "$REFXP/sistema/rocola/Xp/Plataforma/Dibujo2D.cs" ] || falta+=("la referencia congelada ($REFXP)")
  [ -f "$CMPREF/pared/huellas.tsv" ] && [ -f "$CMPREF2/pared/huellas.tsv" ] || falta+=("las salidas de referencia de los recorridos ($CMPREF, $CMPREF2)")
  [ -f "$LETRAS/micross.ttf" ] || falta+=("las letras falsas ($LETRAS)")
  local r=0 e; for e in $ESCENAS; do [ -n "$(ruido_de "$e")" ] && r=$((r + 1)); done
  [ $r -gt 0 ] || falta+=("el ruido medido ($RUIDO)")
  [ $r -eq $(echo $ESCENAS | wc -w) ] || nota+=("ruido medido para $r de $(echo $ESCENAS | wc -w) escenas (las demas, con la holgura de 2)")
  [ -d "$DATOS0/musica" ] || falta+=("los datos de prueba ($DATOS0)")
  [ -d "$DH/barras" ] && [ -d "$DH/arbol" ] && [ -d "$DH/sin" ] || falta+=("los datos de las escenas de la hoja ($DH)")
  [ -d "$DIBUJO/datos-base/musica" ] || falta+=("los datos de los recorridos ($DIBUJO/datos-base)")
  [ -f "$TARJETA/cintas/00-sintetica.cinta" ] && [ -d "$TARJETA/salidas" ] || falta+=("las cintas de la tarjeta ($TARJETA)")
  [ "$LETRAS" = "$LETRAS_FIJAS" ] || nota+=("los doce arnes.sh leen las letras de $LETRAS_FIJAS (fijo en sus guiones), no de $LETRAS")
  [ "$REFXP" = "$REFXP_FIJA" ] || nota+=("los doce arnes.sh compilan la referencia de $REFXP_FIJA (fijo en sus guiones), no de $REFXP")
  if [ "$NIVEL" = 1 ]; then
    unshare -n true 2> /dev/null || nota+=("unshare -n no se puede en esta maquina: el nivel 1 corre CON red")
    python3 -c "import sys; sys.path.insert(0, '$AQUI'); import clasifica; sys.exit(0 if clasifica.soporta_ignora('$GV') else 1)" ||
      nota+=("compara_trazas.py aun no tiene --ignora (lo pone el grupo andamio): las escenas del nivel 1 no se comparan")
  fi
  local libre; libre=$(df -k / | awk 'NR==2{print $4}')
  [ "$libre" -gt 1500000 ] || nota+=("solo $((libre / 1024)) MB libres en el disco")
  echo "   copia: $XP ($(git -C "$XP" log --oneline -1 2> /dev/null | cut -c1-70)); trabajo: $TRABAJO; nivel $NIVEL; pasos: $PASOS"
  [ ${#nota[@]} -gt 0 ] && printf '   nota: %s\n' "${nota[@]}"
  if [ ${#falta[@]} -gt 0 ]; then apunta FALTA previo "$(IFS=';'; echo "${falta[*]}")" $((SECONDS - t0))
  else apunta BIEN previo "esta todo lo de fuera (referencia, recorridos, letras, ruido, datos, cintas) y las herramientas${nota:+; ${#nota[@]} notas arriba}" $((SECONDS - t0)); fi
}

paso_codigo() {
  cabeza "codigo: Codigo/ con las mismas lineas y CRLF que la base ($BASE)"
  local t0=$SECONDS v d
  IFS=$'\t' read -r v d < <(python3 "$AQUI/clasifica.py" codigo "$XP" "$BASE")
  apunta "$v" codigo "$d" $((SECONDS - t0))
}

paso_ganchos() {
  cabeza "ganchos: ganchos.py y diff-xp.py"
  local t0=$SECONDS g=$P/RocolaOs/ganchos.py dx=$P/Tarjeta/guiones/diff-xp.py r op=()
  if [ -f "$g" ]; then
    grep -q -- "--base" "$g" && op=(--base "$BASE")
    (cd "$XP" && python3 "$g" "${op[@]}") > "$REG/ganchos.log" 2>&1; r=$?
    if [ $r = 0 ]; then apunta BIEN "ganchos.py" "$(grep -v '^ *$' "$REG/ganchos.log" | tail -1 | cut -c1-80)$(grep -m1 -E '^ganchos: ' "$REG/ganchos.log" | sed 's/^/; /')$(grep -m1 -E '^Codigo/ devolviendo' "$REG/ganchos.log" | sed 's/^/; /' | cut -c1-140)" $((SECONDS - t0))
    else apunta MAL "ganchos.py" "salio con $r: $(grep -m3 -E '^ *MAL|rror' "$REG/ganchos.log" | cut -c1-120 | tr '\n' ';') $(tail -1 "$REG/ganchos.log" | cut -c1-120)" $((SECONDS - t0)); fi
  else
    apunta NO "ganchos.py" "aun no esta (Pruebas/RocolaOs/ganchos.py lo hace el grupo andamio)"
  fi
  t0=$SECONDS
  local sin="(sin --sin-ganchos: aun no lo tiene)"; op=()
  grep -q -- "--sin-ganchos" "$dx" && { op=(--sin-ganchos); sin="con --sin-ganchos"; }
  (cd "$XP" && python3 "$dx" "${op[@]}" $(cd Codigo && ls *.cs)) > "$REG/diff-xp.log" 2>&1; r=$?
  local tot dev; tot=$(awk '/^[^ ]+\.cs: [0-9]+ lineas quitadas, [0-9]+ puestas \([0-9]+ sin marca\)/ {n++; q+=$2; p+=$5; s+=substr($7,2)} END {print n+0, q+0, p+0, s+0}' "$REG/diff-xp.log")
  dev=$(grep -m1 "^ganchos devueltos" "$REG/diff-xp.log" | cut -c1-160); [ -n "$dev" ] && sin="$sin; $dev"
  set -- $tot
  if [ $r = 0 ] && [ "$2 $3 $4" = "$DIFFXP" ] && ! grep -q "^MAL" "$REG/diff-xp.log"; then
    apunta BIEN "diff-xp.py" "$1 archivos: $2 lineas quitadas, $3 puestas y $4 sin marca $sin (las de la base: $DIFFXP)" $((SECONDS - t0))
  else apunta MAL "diff-xp.py" "salio con $r; $1 archivos: $2 lineas quitadas, $3 puestas y $4 sin marca $sin; la base: $DIFFXP $(grep -m2 '^MAL' "$REG/diff-xp.log" | tr '\n' ' ')" $((SECONDS - t0)); fi
}

paso_compila() {
  cabeza "compila: desde cero, en JIT"
  local t0=$SECONDS
  if [ -n "$PROG" ]; then
    [ -f "$PROG/rocola-xp.dll" ] && apunta BIEN compila "no se compila: el programa ya compilado de PROG ($PROG)" || apunta FALTA compila "no esta $PROG/rocola-xp.dll"
    return
  fi
  rm -rf "$TRABAJO/jit"
  (cd "$XP" && pesado dotnet build RocolaXp.csproj -c Release --no-incremental -nologo -v q -o "$TRABAJO/jit") > "$REG/compila.log" 2>&1
  local r=$? e a v d
  e=$(grep -oE '[0-9]+ Error\(s\)' "$REG/compila.log" | tail -1 | cut -d' ' -f1); a=$(grep -oE '[0-9]+ Warning\(s\)' "$REG/compila.log" | tail -1 | cut -d' ' -f1)
  IFS=$'\t' read -r v d < <(python3 "$AQUI/clasifica.py" avisos "$REG/compila.log" "$AQUI/avisos-base.txt" "$XP")
  if [ $r = 0 ] && [ "${e:-x}" = 0 ] && [ -f "$TRABAJO/jit/rocola-xp.dll" ] && [ "$v" = BIEN ] && [ "${a:-x}" = "$AVISOS" ]; then
    PROG=$TRABAJO/jit; apunta BIEN compila "0 errores, $a avisos ($d)" $((SECONDS - t0))
  elif [ $r = 0 ] && [ "${e:-x}" = 0 ] && [ -f "$TRABAJO/jit/rocola-xp.dll" ]; then
    PROG=$TRABAJO/jit; apunta MAL compila "0 errores, ${a:-?} avisos (se esperaban $AVISOS): $d" $((SECONDS - t0))
  else
    apunta MAL compila "no compila (salio con $r, ${e:-?} errores): $(grep -m3 ' error ' "$REG/compila.log" | sed 's|.*/||' | cut -c1-160 | tr '\n' ' ')" $((SECONDS - t0))
  fi
}

paso_arneses() {
  cabeza "arneses: los de las vistas, acabado-estilo, vistas-b, Ctrl+F3 y la frontera, contra la referencia"
  local A=$TRABAJO/arneses x n var t0 lin mal v d
  mkdir -p "$A"
  for x in "VISTASA VistasVistasA" "ASISTENTE VistasAsistente" "PANTALLAVIVA VistasPantallaViva" "BUSCADOR VistasBuscadorPortadas" \
           "ATRACCION VistasAtraccion" "REPOSO VistasReposo" "SUENA VistasSuena" "PARED VistasPared" "FONDO VistasFondo" "PORTADAS VistasPortadas" \
           "BUSCADOR VistasBuscador" "VIDEOFORM1 VistasVideoForm1"; do
    set -- $x; var=$1; n=$2; t0=$SECONDS
    rm -rf "${A:?}/$n"; mkdir -p "$A/$n"
    pesado env -u GUARDA -u SOLO_NUEVO "$var=$A/$n" bash "$P/$n/arnes.sh" > "$REG/arnes-$n.log" 2>&1
    lin=$(grep -E "^(IGUALES|DISTINTAS)" "$REG/arnes-$n.log" | tr '\n' ' '); mal=$(grep -cE " error |[1-9][0-9]* Error\(s\)" "$REG/arnes-$n.log")
    if [ "$mal" != 0 ] || [ -z "$lin" ]; then
      apunta MAL "arnes $n" "no compila o no corre: $(grep -m2 -E ' error |Error\(s\)' "$REG/arnes-$n.log" | sed 's|.*/||' | cut -c1-160 | tr '\n' ' ')" $((SECONDS - t0)); continue
    fi
    if ! echo "$lin" | grep -q DISTINTAS; then apunta BIEN "arnes $n" "$lin" $((SECONDS - t0))
    elif [ $n = VistasFondo ] && [ -f "$A/$n/vistas-ref-falsas.tsv" ]; then
      IFS=$'\t' read -r v d < <(python3 "$AQUI/clasifica.py" fondo "$A/$n/vistas-ref-falsas.tsv" "$A/$n/vistas-nuevo-falsas.tsv")
      apunta "$v" "arnes $n" "$lin: $d" $((SECONDS - t0))
    else apunta MAL "arnes $n" "$lin" $((SECONDS - t0)); fi
    rm -rf "${A:?}/$n"
  done
  # los tres que comparan linea por linea con su cuenta (las letras falsas y DejaVu)
  for x in "VistasAcabadoEstilo compara.sh $ACABADO" "VistasVistasB arnes.sh $VISTASB" "VistasAjustes compara.sh $AJUSTES"; do
    set -- $x; n=$1; t0=$SECONDS
    rm -rf "${A:?}/$n"
    pesado env REF="$REFXP/sistema/rocola/Xp" LETRAS_FALSAS="$LETRAS" bash "$P/$n/$2" "$A/$n" > "$REG/arnes-$n.log" 2>&1
    local ls_; ls_=$(grep -oE "contra la referencia[^:]*: [0-9]+ distintas de [0-9]+" "$REG/arnes-$n.log" | sed -E 's/.*: //')
    local exc; exc=$(grep -oE "[0-9]+ con excepcion" "$REG/arnes-$n.log" | awk '{s+=$1} END {print s+0}')
    if [ "$(echo "$ls_" | wc -w)" -ge 8 ] && ! echo "$ls_" | grep -vqE "^0 distintas de [0-9]+$" && [ "$exc" = 0 ] &&
       [ "$(echo "$ls_" | awk '{print $4}' | sort -n | head -1)" -ge "$3" ]; then
      apunta BIEN "arnes $n" "$(echo "$ls_" | tr '\n' ',' | sed 's/,$//; s/,/, /') (falsas y DejaVu; 0 con excepcion)" $((SECONDS - t0))
    else
      apunta MAL "arnes $n" "$(echo "$ls_" | tr '\n' ',' | sed 's/,$//'); $exc con excepcion (se esperaban 0 distintas de $3)$(grep -m1 ' error ' "$REG/arnes-$n.log" | sed 's|.*/| |' | cut -c1-120)" $((SECONDS - t0))
    fi
    rm -rf "${A:?}/$n"/bin-* "${A:?}/$n"/proj-*
  done
  # la frontera con la tarjeta (VistasFrontera, compara.sh): se compila con la copia y con la referencia
  n=VistasFrontera; t0=$SECONDS; local F=$A/$n
  rm -rf "$F"; mkdir -p "$F/proj-ref"; cp "$P/$n/VistasFrontera.csproj" "$P/$n/Frontera.cs" "$F/proj-ref/"
  pesado bash -c "cd '$P/$n' && dotnet build -c Release -nologo -v q -o '$F/nuevo' 2>&1 | grep -E ' error |rror\(s\)'
                  cd '$F/proj-ref' && ROCOLA_COMUN='$P/Comun' dotnet build -c Release -nologo -v q -p:Fuente='$REFXP/sistema/rocola/Xp' -o '$F/ref' 2>&1 | grep -E ' error |rror\(s\)'
                  LETRAS='$LETRAS' bash '$P/$n/compara.sh' '$F/nuevo' '$F/ref' '$F/sal'" > "$REG/arnes-$n.log" 2>&1
  set -- $FRONTERA
  lin=$(grep -E "^sk \((xp|dejavu)\):" "$REG/arnes-$n.log")
  if [ "$(echo "$lin" | grep -cE "^sk \((xp|dejavu)\): ([0-9]+) lineas, 0 distintas de la referencia; ([0-9]+) texturas volcadas \(\3 en la referencia\), 0 lineas distintas; salida 0$")" = 2 ] &&
     [ "$(echo "$lin" | sed -E 's/^sk \([a-z]+\): ([0-9]+) lineas.*/\1/' | sort -n | head -1)" -ge "$1" ] &&
     [ "$(echo "$lin" | sed -E 's/.*; ([0-9]+) texturas volcadas.*/\1/' | sort -n | head -1)" -ge "$2" ]; then
    apunta BIEN "arnes $n" "$(echo "$lin" | sed -E 's/^sk \(([a-z]+)\): ([0-9]+) lineas, ([0-9]+) distintas[^;]*; ([0-9]+) texturas volcadas[^,]*, ([0-9]+) lineas distintas.*/\1: \3 de \2 lineas y \5 de \4 texturas distintas/' | tr '\n' ';' | sed 's/;$//; s/;/; /')" $((SECONDS - t0))
  else
    apunta MAL "arnes $n" "$(echo "$lin" | tr '\n' ' ' | cut -c1-300)$(grep -m1 ' error ' "$REG/arnes-$n.log" | sed 's|.*/| |' | cut -c1-120)" $((SECONDS - t0))
  fi
  rm -rf "$F/nuevo" "$F/ref" "$F/proj-ref"
}

paso_banco() {
  cabeza "banco: el dibujo nativo contra el congelado de la referencia (semillas 7, 11 y 5)"
  local t0=$SECONDS B=$TRABAJO/banco s L res="" mal=0
  rm -rf "$B"; mkdir -p "$B"
  pesado bash -c "cd '$P/BancoDibujo' && dotnet build -c Release Congelado -nologo -v q -p:Referencia='$REFXP' -o '$B/bin-c' 2>&1 | grep -E ' error |rror\(s\)'
                  dotnet build -c Release Nuevo -nologo -v q -o '$B/bin-n' 2>&1 | grep -E ' error |rror\(s\)'" > "$REG/banco.log" 2>&1
  for s in 7 11 5; do
    if [ $s = 5 ]; then L="SIN_LETRAS=1"; else L="ROCOLA_LETRAS_XP=$LETRAS"; fi
    pesado bash -c "env $L dotnet '$B/bin-c/BancoCongelado.dll' casos 3000 semilla $s salida '$B/c$s.tsv' > /dev/null 2>&1
                    env $L dotnet '$B/bin-n/BancoNuevo.dll' casos 3000 semilla $s salida '$B/n$s.tsv' ejecutor nativo > /dev/null 2>&1" >> "$REG/banco.log" 2>&1
    if [ -s "$B/c$s.tsv" ] && cmp -s "$B/c$s.tsv" "$B/n$s.tsv"; then res="$res semilla $s IGUAL ($(wc -l < "$B/c$s.tsv") lineas);"
    else res="$res semilla $s DISTINTO ($(diff "$B/c$s.tsv" "$B/n$s.tsv" 2> /dev/null | grep -c '^>') lineas);"; mal=1; fi
  done
  [ $mal = 0 ] && apunta BIEN banco "${res% ;}" $((SECONDS - t0)) || apunta MAL banco "$res $(grep -m2 -E ' error |[1-9] Error' "$REG/banco.log" | sed 's|.*/||' | tr '\n' ' ')" $((SECONDS - t0))
  rm -rf "$B/bin-c" "$B/bin-n"; rm -f "$B"/*.tsv
}

paso_cintas() {
  cabeza "cintas: las de la tarjeta (VistasFrontera/cintas.sh) contra sus salidas de referencia"
  local t0=$SECONDS Fr=$TRABAJO/frontera
  if [ -z "$PROG" ]; then apunta FALTA cintas "no hay programa en JIT (el paso compila o PROG)"; return; fi
  rm -rf "$Fr" "$TRABAJO/cintas"; mkdir -p "$Fr"
  (cd "$P/Tarjeta" && pesado dotnet build ReproductorCintas.csproj -c Release -nologo -v q -o "$Fr/reproductor") > "$REG/cintas.log" 2>&1
  pesado env TARJETA="$TARJETA" FRONTERA="$Fr" bash "$P/VistasFrontera/cintas.sh" "$PROG" "$TRABAJO/cintas" >> "$REG/cintas.log" 2>&1
  local r; r=$(grep "^RESUMEN:" "$REG/cintas.log")
  local ig di sin; ig=$(echo "$r" | grep -oE '[0-9]+ iguales' | cut -d' ' -f1); di=$(echo "$r" | grep -oE '[0-9]+ distintas' | cut -d' ' -f1); sin=$(cuenta "^SIN REFERENCIA" "$REG/cintas.log")
  if [ "${di:-x}" = 0 ] && [ "${ig:-0}" -ge "$CINTAS" ] && [ "$sin" = 0 ]; then apunta BIEN cintas "${r#RESUMEN: }" $((SECONDS - t0))
  else apunta MAL cintas "${r:-no corrieron} (se esperaban $CINTAS iguales; $sin sin referencia): $(grep -m3 '^DISTINTO' "$REG/cintas.log" | tr '\n' ' ' | cut -c1-200)" $((SECONDS - t0)); fi
  rm -rf "$Fr"; find "$TRABAJO/cintas" -name '*.txt' -size +200k -delete 2> /dev/null
}

recorrido_uno() {   # recorrido_uno NOMBRE: un recorrido con los dos candados (en el nivel 1, con ROCOLA_OS, su /run vacia y sin red)
  local extra=() red=()
  if [ "$NIVEL" = 1 ]; then
    local run=$TRABAJO/run/recorrido-$1; rm -rf "$run"; mkdir -p "$run"
    extra=(ROCOLA_OS="$ROCOLA_OS_LISTA" ROCOLA_RUN="$run")
    if [ "$SIN_RED" = 1 ] && unshare -n true 2> /dev/null; then red=(unshare -n); fi
  fi
  pesado flock -o "$CANDADO_RECORRIDOS" "${red[@]}" env ROCOLA_TRABAJO="$DIBUJO" VOLCADO=1 CAPAS=1 HUELLAS=1 "${extra[@]}" \
    bash "$RC/recorridos.sh" "$PROG" "$TRABAJO/recorridos" "$1" >> "$REG/recorridos-corridas.log" 2>&1
  grep -E "^$1: salio con" "$REG/recorridos-corridas.log" | tail -1 >> "$REG/recorridos-salidas.log"
}

paso_recorridos() {
  cabeza "recorridos: los nueve, contra las dos corridas de la referencia (lo quieto)"
  local t0=$SECONDS n v d lista otra=()
  if [ -z "$PROG" ]; then apunta FALTA recorridos "no hay programa en JIT (el paso compila o PROG)"; return; fi
  : > "$REG/recorridos-salidas.log"
  lista=${RECORRIDOS:-$(ls "$CMPREF")}
  for n in $lista; do recorrido_uno "$n"; done
  python3 "$AQUI/clasifica.py" recorridos "$TRABAJO/recorridos" "$CMPREF" "$CMPREF2" "$REG/recorridos-salidas.log" --solo "$(echo $lista | tr ' ' ',')" > "$REG/recorridos.txt"
  otra=($(awk -F'\t' '$1 == "REPETIR" {split($2, a, " "); sub(":", "", a[2]); print a[2]}' "$REG/recorridos.txt"))
  if [ ${#otra[@]} -gt 0 ] && [ "$REPITE" = 1 ]; then
    echo "   otra vez: ${otra[*]}" | tee -a "$REG/recorridos-corridas.log"
    cp "$REG/recorridos.txt" "$REG/recorridos-primera.txt"
    for n in "${otra[@]}"; do recorrido_uno "$n"; done
    python3 "$AQUI/clasifica.py" recorridos "$TRABAJO/recorridos" "$CMPREF" "$CMPREF2" "$REG/recorridos-salidas.log" --solo "$(echo $lista | tr ' ' ',')" > "$REG/recorridos.txt"
  fi
  while IFS=$'\t' read -r v d; do
    [ "$v" = TOTAL ] && continue
    n=$(echo "$d" | awk '{print $2}' | tr -d :)
    case " ${otra[*]:-} " in *" $n "*) d="$d (a la segunda)";; esac
    [ "$v" = REPETIR ] && v=MAL
    apunta "$v" "${d%%:*}" "${d#*: }"
  done < "$REG/recorridos.txt"
  d=$(awk -F'\t' '$1 == "TOTAL" {print $2}' "$REG/recorridos.txt")
  if grep -qE "^(MAL|REPETIR)" "$REG/recorridos.txt"; then apunta MAL recorridos "$d" $((SECONDS - t0))
  else apunta BIEN recorridos "$d" $((SECONDS - t0)); fi
}

paso_ventanas() {
  cabeza "ventanas: las reglas de la hoja, de la pantalla y la lista de la ventana (PantallaVistas)"
  local t0=$SECONDS l
  pesado env W="$WV" bash "$GV/pruebas_hoja.sh" "$ETIQUETA" > "$REG/hoja.log" 2>&1
  l=$(grep -E "^pasan [0-9]+, fallan [0-9]+" "$REG/hoja.log" | tail -1)
  if [[ "$l" =~ ^pasan\ ([0-9]+),\ fallan\ 0$ ]] && [ "${BASH_REMATCH[1]}" -ge "$HOJA" ]; then apunta BIEN "hoja (JIT)" "$l" $((SECONDS - t0))
  else apunta MAL "hoja (JIT)" "${l:-no corrio} (se esperaban $HOJA): $(grep -m2 -E '^FALLA|no compila' "$REG/hoja.log" | cut -c1-160 | tr '\n' ' ')" $((SECONDS - t0)); fi
  t0=$SECONDS
  pesado env W="$WV" AOT=1 bash "$GV/pruebas_pantalla.sh" "$ETIQUETA" > "$REG/pantalla.log" 2>&1
  local j a; j=$(grep -E "^pasan [0-9]+, fallan [0-9]+" "$REG/pantalla.log" | sed -n 1p); a=$(grep -E "^pasan [0-9]+, fallan [0-9]+" "$REG/pantalla.log" | sed -n 2p)
  for x in "JIT|$j" "NativeAOT|$a"; do
    l=${x#*|}
    if [[ "$l" =~ ^pasan\ ([0-9]+),\ fallan\ 0$ ]] && [ "${BASH_REMATCH[1]}" -ge "$PANTALLA" ]; then apunta BIEN "pantalla (${x%%|*})" "$l" $((SECONDS - t0))
    else apunta MAL "pantalla (${x%%|*})" "${l:-no corrio} (se esperaban $PANTALLA): $(grep -m2 -E '^FALLA|no compila|no publica' "$REG/pantalla.log" | cut -c1-160 | tr '\n' ' ')" $((SECONDS - t0)); fi
  done
  t0=$SECONDS
  if [ -z "$PROG" ]; then apunta FALTA PantallaVistas "no hay programa en JIT"; return; fi
  pesado env W="$WV" bash "$P/Ventanas/PantallaVistas/pruebas_vistas.sh" "$PROG" "$ETIQUETA" > "$REG/vistas.log" 2>&1
  l=$(grep -E "^pasan [0-9]+, fallan [0-9]+" "$REG/vistas.log" | tail -1)
  if [[ "$l" =~ ^pasan\ ([0-9]+),\ fallan\ 0$ ]] && [ "${BASH_REMATCH[1]}" -ge "$VISTAS" ]; then apunta BIEN PantallaVistas "$l" $((SECONDS - t0))
  else apunta MAL PantallaVistas "${l:-no corrio} (se esperaban $VISTAS): $(grep -m2 -E '^FALLA|no compila' "$REG/vistas.log" | cut -c1-160 | tr '\n' ' ')" $((SECONDS - t0)); fi
  rm -rf "$WV/hoja" "$WV/pantalla" "$WV/vistas"
}

nivel1_listo() {   # el nivel 1 compara sin los relojes y encargos de Rocola OS: hace falta --ignora en compara_trazas.py
  [ "$NIVEL" = 1 ] || return 0
  python3 -c "import sys; sys.path.insert(0, '$AQUI'); import clasifica; sys.exit(0 if clasifica.soporta_ignora('$GV') else 1)" && return 0
  apunta NO "$1" "nivel 1: compara_trazas.py aun no tiene --ignora (lo pone el grupo andamio): no se corren ni se comparan"
  return 1
}

paso_escenas() {
  cabeza "escenas: las $(echo $ESCENAS | wc -w) alternadas con la referencia (JIT)$([ "$NIVEL" = 1 ] && echo ", nivel 1: ROCOLA_OS=$ROCOLA_OS_LISTA")"
  if [ -z "$PROG" ]; then apunta FALTA escenas "no hay programa en JIT (el paso compila o PROG)"; return; fi
  nivel1_listo escenas || return
  escenas_con escenas "$PROG" "R$N" "N$N" $ESCENAS
  limpia_escenas escenas "R$N" "N$N"
}

paso_aot() {
  cabeza "aot: NativeAOT (sin avisos IL/AOT) y sus escenas"
  local t0=$SECONDS w r
  if [ -z "$AOT" ]; then
    rm -rf "$TRABAJO/aot"
    (cd "$XP" && pesado dotnet publish RocolaXp.csproj -c Release -r linux-x64 -p:PublishAot=true -nologo -v q -o "$TRABAJO/aot") > "$REG/aot.log" 2>&1
    r=$?; w=$(grep -ciE 'warning (IL|AOT)' "$REG/aot.log")
    if [ $r = 0 ] && [ -x "$TRABAJO/aot/rocola-xp" ] && [ "$w" = 0 ]; then AOT=$TRABAJO/aot; apunta BIEN "aot publica" "salida 0, 0 avisos IL/AOT ($(du -sh "$TRABAJO/aot/rocola-xp" | cut -f1) el ejecutable)" $((SECONDS - t0))
    elif [ $r = 0 ] && [ -x "$TRABAJO/aot/rocola-xp" ]; then AOT=$TRABAJO/aot; apunta MAL "aot publica" "salida 0, $w avisos IL/AOT: $(grep -m2 -iE 'warning (IL|AOT)' "$REG/aot.log" | sed 's|.*/||' | cut -c1-150 | tr '\n' ' ')" $((SECONDS - t0))
    else apunta MAL "aot publica" "no publica (salio con $r): $(grep -m2 -E ' error ' "$REG/aot.log" | sed 's|.*/||' | cut -c1-150 | tr '\n' ' ')" $((SECONDS - t0)); return; fi
  else
    # (la publicacion dada no se toca: su fecha se cambia en una copia)
    rm -rf "$TRABAJO/aot-copia"; mkdir -p "$TRABAJO/aot-copia"; cp -p "$AOT"/rocola-xp "$AOT"/*.so "$TRABAJO/aot-copia/" 2> /dev/null; AOT=$TRABAJO/aot-copia
    apunta BIEN "aot publica" "no se publica: la de AOT (copiada a $AOT)"
  fi
  # la fecha del ejecutable, la de dotnet: el titulo de Ctrl+F3 dice «Esta rocola es del ...» con la del programa que corre (con JIT, dotnet)
  [ -n "$DOTNET_BIN" ] && touch -r "$DOTNET_BIN" "$AOT/rocola-xp"
  nivel1_listo "escenas aot" || return
  escenas_con "escenas-aot" "aot:$AOT" "RA$N" "NA$N" $ESCENAS_AOT
  limpia_escenas escenas-aot "RA$N" "NA$N"
}

paso_cierre() {
  cabeza "cierre: cierre_rocola_os.sh (--version, SIGUSR1 con y sin hojas y con video) en JIT y en NativeAOT"
  local m prog t0 b ma
  for m in JIT NativeAOT; do
    t0=$SECONDS
    if [ $m = JIT ]; then prog=$PROG; else prog=${AOT:+aot:$AOT}; fi
    [ -n "$prog" ] || { apunta FALTA "cierre_rocola_os.sh ($m)" "no hay programa"; continue; }
    pesado env W="$WV" SAL="$SAL" DATOS0="$DATOS0" DH="$DH" bash "$GV/cierre_rocola_os.sh" "$prog" "cierre-$m" > "$REG/cierre-$m.log" 2>&1
    b=$(cuenta '^  BIEN ' "$REG/cierre-$m.log"); ma=$(cuenta '^  MAL ' "$REG/cierre-$m.log")
    if [ "$ma" = 0 ] && [ "$b" -ge "$CIERRE" ] && grep -q "^TODO BIEN" "$REG/cierre-$m.log"; then
      apunta BIEN "cierre_rocola_os.sh ($m)" "$b de $b (TODO BIEN; se esperaban $CIERRE)" $((SECONDS - t0))
    else apunta MAL "cierre_rocola_os.sh ($m)" "$b BIEN, $ma MAL (se esperaban $CIERRE): $(grep -m3 '^  MAL ' "$REG/cierre-$m.log" | sed 's/^  MAL *//' | cut -c1-100 | tr '\n' ';')" $((SECONDS - t0)); fi
    rm -rf "$SAL"/cierre-$m-* "$WV/corridas"/cierre-$m-*
  done
}

paso_importa() {
  cabeza "importa: Pruebas/Importa/prueba.sh (el importador de lo aprendido en XP y lo grande) en JIT y en NativeAOT"
  local m prog t0 l
  if [ "$(id -u)" != 0 ]; then apunta FALTA "Pruebas/Importa" "hace falta ser root (unshare y mount)"; return; fi
  for m in JIT NativeAOT; do
    t0=$SECONDS
    if [ $m = JIT ]; then prog=$PROG; else prog=${AOT:+aot:$AOT}; fi
    [ -n "$prog" ] || { apunta FALTA "Pruebas/Importa ($m)" "no hay programa"; continue; }
    pesado env W="$TRABAJO/importa-$m" bash "$P/Importa/prueba.sh" "$prog" > "$REG/importa-$m.log" 2>&1
    l=$(grep -E "^(TODO BIEN \([0-9]+ comprobaciones\)|[0-9]+ MAL de [0-9]+)$" "$REG/importa-$m.log" | tail -1)
    if [[ "$l" =~ ^TODO\ BIEN\ \(([0-9]+)\ comprobaciones\)$ ]] && [ "${BASH_REMATCH[1]}" -ge "$IMPORTA" ]; then
      apunta BIEN "Pruebas/Importa ($m)" "$l (se esperaban $IMPORTA)" $((SECONDS - t0))
    else apunta MAL "Pruebas/Importa ($m)" "${l:-no acabo} (se esperaban $IMPORTA): $(grep -m3 '^MAL ' "$REG/importa-$m.log" | sed 's/^MAL *//' | cut -c1-100 | tr '\n' ';')" $((SECONDS - t0)); fi
    rm -rf "$TRABAJO/importa-$m"
  done
}

# ======================================================================================================================================
inicio=$SECONDS
echo "== nivel0.sh: nivel $NIVEL, etiqueta $ETIQUETA, $(date '+%F %T'), $(nproc) nucleos, carga $(cut -d' ' -f1-3 /proc/loadavg)"
for paso in $PASOS; do
  case $paso in
    previo|codigo|ganchos|compila|arneses|banco|cintas|recorridos|ventanas|escenas|aot|cierre|importa) "paso_$paso" ;;
    *) apunta FALTA "$paso" "paso desconocido" ;;
  esac
done
# lo que queda: registros, resumenes, las trazas y los estados de las escenas (las capturas, solo de las que no salieron bien)
if [ "$GUARDA_BIN" != 1 ]; then rm -rf "$TRABAJO/jit" "$TRABAJO/aot" "$TRABAJO/aot-copia"; fi
rm -rf "$WV/corridas" "$TRABAJO/run"
echo
echo "== RESUMEN (nivel $NIVEL, $ETIQUETA, $(( (SECONDS - inicio) / 60 )) min; $(git -C "$XP" log --oneline -1 2> /dev/null | cut -c1-60); disco libre $(df -h / | awk 'NR==2{print $4}'); la carpeta de trabajo, $(du -sh "$TRABAJO" | cut -f1))"
cat "$RES"
nb=$(grep -c '^BIEN' "$RES"); na=$(grep -c '^ACEPTADO' "$RES"); nm=$(grep -c '^MAL' "$RES"); nf=$(grep -cE '^(FALTA|NO)' "$RES")
echo "== $nb BIEN, $na ACEPTADO, $nm MAL, $nf sin comprobar (FALTA o NO)"
sleep 1   # (que tee acabe de escribir el registro)
if [ "$nm" -gt 0 ]; then exit 1; elif [ "$nf" -gt 0 ]; then exit 2; else exit 0; fi
