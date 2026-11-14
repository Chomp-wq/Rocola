#!/bin/bash
# prueba.sh: LA PRUEBA DE LA BATERIA DEL NIVEL 0 (nivel0.sh, alterna.sh y clasifica.py), sin correr la rocola: lo que decide clasifica.py
# con salidas de escenas y de recorridos hechas a mano (trazas, estados, capas y huellas inventadas, en el formato de las de verdad), el
# camino del nivel 1 (--ignora, con el doble compara_trazas_ignora.py) y la sintaxis de los guiones. Cada comprobacion dice BIEN o MAL en una
# linea; al final «TODO BIEN (N comprobaciones)» o cuantas MAL (y sale con 1). Unos segundos.
#   W: la carpeta de trabajo (de fabrica, una nueva en /tmp; se borra al acabar salvo con GUARDA=1)
# Lo que no prueba (lo prueba correr nivel0.sh entero, ver ../LEEME.md): las escenas, los recorridos y los demas pasos con la rocola.
set -u
G=$(cd "$(dirname "$(readlink -f "$0")")" && pwd); RO=$(cd "$G/.." && pwd); GV=$(cd "$RO/../Ventanas/guiones" && pwd)
W=${W:-$(mktemp -d /tmp/nivel0-prueba.XXXXXX)}; mkdir -p "$W"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
veredicto() { python3 "$RO/clasifica.py" "$@" | cut -f1; }
es() { local quiere="$1"; shift; local v; v=$(veredicto "$@"); [ "$v" = "$quiere" ] || { echo "      (dio $v: $(python3 "$RO/clasifica.py" "$@" | cut -f2 | cut -c1-200))"; return 1; }; }

# ---- las salidas de una escena, a mano (corre.sh: traza.txt, estado-*.txt, capas, rocola_estado.txt) ----
traza_base() {   # traza_base ARCHIVO: una traza corta de una escena (los mismos sucesos que una de verdad)
  cat > "$1" <<'EOF'
144	ventana-ve Form1'Rocola'
147	foco HelpBar
151	encargo Shown
151	shown Form1'Rocola'
258	encargo MedidasBitacora.cs:129
603	tick Form1.cs:724 42
666	tick Form1.cs:724 42
701	tick Form1.cs:724 42
7012	tecla 43 a GpuPanel
7013	pck 0
7014	keydown 43 a Form1'Rocola'
7100	keyup 43 a HelpBar
9005	estado estado-antes.txt
EOF
}
escena() {   # escena DIR: una salida de escena con su estado y la traza de traza_base
  rm -rf "$1"; mkdir -p "$1"; traza_base "$1/traza.txt"
  printf 't=9005\ncreditos=4\ncola=0\nsuena=\nletra=L\nfoco=HelpBar\nactiva=Form1'"'"'Rocola'"'"'\n' > "$1/estado-antes.txt"
  printf 'b1=67\r\n' > "$1/rocola_teclas.txt"
}
E=$W/esc
echo "== escenas (clasifica.py escena)"
escena "$E/A"; escena "$E/B"
si "dos escenas iguales: BIEN" es BIEN escena arranque "$E/A" "$E/B"
# SIGTERM: el programa traza el cierre y borra el estado; la referencia no
escena "$E/A"; escena "$E/B"; echo "x" > "$E/A/rocola_estado.txt"
printf '9100\tencargo Apagado\n9101\tclosing Form1'"'"'Rocola'"'"' WindowsShutDown\n9102\tclosed Form1'"'"'Rocola'"'"' WindowsShutDown\n9103\tfoco nadie\n' >> "$E/B/traza.txt"
si "sigterm con el cierre WindowsShutDown y el estado borrado: ACEPTADO" es ACEPTADO escena sigterm "$E/A" "$E/B"
si "lo mismo en una escena que no es de apagado (arranque): MAL" es MAL escena arranque "$E/A" "$E/B"
echo "x" > "$E/B/rocola_estado.txt"
si "sigterm con el cierre pero el estado NO borrado: MAL" es MAL escena sigterm "$E/A" "$E/B"
rm -f "$E/B/rocola_estado.txt"; sed -i '/keyup 43/d' "$E/B/traza.txt"
si "sigterm con el cierre y ademas un suceso de menos: MAL" es MAL escena sigterm "$E/A" "$E/B"
escena "$E/B"; echo "x" > "$E/A/rocola_estado.txt"
printf '9100\tencargo Apagado\n9101\tclosing Form1'"'"'Rocola'"'"' UserClosing\n9102\tclosed Form1'"'"'Rocola'"'"' UserClosing\n' >> "$E/B/traza.txt"
si "sigterm que cierra con otra razon (UserClosing): MAL" es MAL escena sigterm "$E/A" "$E/B"
# con una hoja abierta (f3sigterm): la hoja con FormOwnerClosing, su dialogo acaba y la tecla que lo abrio devuelve su pck
escena "$E/A"; escena "$E/B"; echo "x" > "$E/A/rocola_estado.txt"
printf '9100\tencargo Apagado\n9101\tclosing Hoja'"'"'Rocola - Sonido'"'"' FormOwnerClosing\n9102\tclosing Form1'"'"'Rocola'"'"' WindowsShutDown\n9103\tclosed Hoja'"'"'Rocola - Sonido'"'"' FormOwnerClosing\n9104\tclosed Form1'"'"'Rocola'"'"' WindowsShutDown\n9105\tfoco HelpBar\n9106\tdialogo-fin Hoja'"'"'Rocola - Sonido'"'"' Cancel\n9107\tpck 1\n' >> "$E/B/traza.txt"
si "f3sigterm con la hoja que se cierra antes: ACEPTADO" es ACEPTADO escena f3sigterm "$E/A" "$E/B"
# solo cuentas: un reloj con el doble de vueltas
escena "$E/A"; escena "$E/B"
for i in $(seq 1 30); do printf '%d\ttick Form1.cs:724 42\n' $((1000 + 42 * i)); done >> "$E/B/traza.txt"
si "un reloj con 30 vueltas de mas (la carga): CUENTA" es CUENTA escena arranque "$E/A" "$E/B"
# el volcado del estado al otro lado de una tecla (la hora de la maquina): una cuenta; y si ademas falta algo, MAL
escena "$E/A"; escena "$E/B"
grep -v "estado estado-antes" "$E/A/traza.txt" | sed 's/^7014\tkeydown 43 a Form1.Rocola.$/&\n7050\testado estado-antes.txt/' > "$E/B/traza.txt"
si "el volcado del estado antes y no despues del keyup (la hora): CUENTA" es CUENTA escena video "$E/A" "$E/B"
sed -i '/keyup 43/d' "$E/B/traza.txt"
si "lo mismo y ademas un keyup de menos: MAL" es MAL escena video "$E/A" "$E/B"
# el reloj saturado de la ventana F12 (15 ms): con la NOTA de compara_trazas.py, lo aceptado; sin ella, una cuenta
escena "$E/A"; escena "$E/B"
for i in $(seq 1 30); do printf '%d\ttick D3D9Star.cs:1997 15\n' $((2000 + 30 * i)); done >> "$E/A/traza.txt"
for i in $(seq 1 60); do printf '%d\ttick D3D9Star.cs:1867 15\n' $((2000 + 20 * i)); done >> "$E/B/traza.txt"
si "f12vigia: el reloj de F12 saturado en las dos, 30 frente a 60: ACEPTADO" es ACEPTADO escena f12vigia "$E/A" "$E/B"
si "lo mismo en arranque (no es una escena de F12): CUENTA" es CUENTA escena arranque "$E/A" "$E/B"
escena "$E/B"; for i in $(seq 1 60); do printf '%d\ttick D3D9Star.cs:1867 15\n' $((2000 + 15 * i)); done >> "$E/B/traza.txt"
si "f12vigia con el reloj a su hora en el programa (no saturado): CUENTA" es CUENTA escena f12vigia "$E/A" "$E/B"
# un estado distinto que no es de nada aceptado
escena "$E/A"; escena "$E/B"; sed -i 's/creditos=4/creditos=5/' "$E/B/estado-antes.txt"
si "los creditos distintos en el estado: MAL" es MAL escena arranque "$E/A" "$E/B"
escena "$E/B"; printf 'b1=68\r\n' > "$E/B/rocola_teclas.txt"
si "rocola_teclas.txt distinto: MAL" es MAL escena captura "$E/A" "$E/B"

# ---- tab: los rotulos del repaso en la hoja de Sonido de Ctrl+F3 ----
echo "== tab (los rotulos del repaso)"
hoja_estado() {   # hoja_estado ARCHIVO CLASE TEXTO_BIBLIOTECA PORCENTAJE TONO OTRO
  { printf 't=14630\ncreditos=0\nf3pagina=0\nfoco=BotonPlano'"'"'Las dos'"'"'\n'
    printf "hoja Hoja'Rocola - Sonido' @29,2,966x764\n"
    printf "  Grupo @16,100,928x398 -e t30\n    Tarjeta @0,0,456x398 -e t0\n"
    printf "      %s'%s' @346,42,94x20 -e t2\n" "$2" "$4"
    printf "      %s'Empareja el tono de cada cancion con el de tu biblioteca.\\\\nSolo: %s' @16,65,424x32 -e t3\n" "$2" "$5"
    printf "  Tarjeta @16,508,928x160 ve t11\n"
    printf "    %s'Calidad:   0 completas     6 aceptables     0 pobres' @16,40,896x20 ve t0\n" "$2"
    printf "    %s'La biblioteca se mide sola mientras la rocola trabaja: %s.\\\\nMaterial para la IA: 0 ejemplos (0 MB).' @16,62,896x48 ve t1\n" "$2" "$3"
    printf "    %s'%s' @16,114,300x34 ve t2\n" "$2" "$6"
  } > "$1"
}
capa() { convert -size 966x764 xc:'#101830' "$@"; }   # capa [dibujo] SALIDA: la capa de la hoja (29,2, 966x764)
T=$W/tab
for d in A B; do escena "$T/$d"; done
hoja_estado "$T/A/estado-tab45.txt" Label "ya esta al dia" "33%" "mas fuerza cuantas mas canciones medidas (van 12)." "Repasar"
hoja_estado "$T/B/estado-tab45.txt" Rotulo "faltan 4 archivos" "0%" "aun no hay bastantes medidas; de momento no toca nada." "Repasar"
capa "$T/A/tab45.png.capa4-dibujo2d-29,2-966x764.png"; cp "$T/A/tab45.png.capa4-dibujo2d-29,2-966x764.png" "$T/B/tab45.png.capa4-hoja-29,2-966x764.png"
si "tab: los rotulos del repaso distintos, la capa igual: ACEPTADO" es ACEPTADO escena tab "$T/A" "$T/B"
# la caja de «La biblioteca...» en la capa: hoja 29,2 + cliente 3,29 + tarjeta 16,508 + rotulo 16,62 - la capa 29,2 = 35,599 (896x48)
capa -fill white -draw "rectangle 40,605 300,640" "$T/B/tab45.png.capa4-hoja-29,2-966x764.png"
si "tab: y los puntos distintos de la capa dentro de ese rotulo: ACEPTADO" es ACEPTADO escena tab "$T/A" "$T/B"
capa -fill white -draw "rectangle 40,605 300,640" -draw "rectangle 600,700 620,720" "$T/B/tab45.png.capa4-hoja-29,2-966x764.png"
si "tab: con puntos distintos tambien fuera de los rotulos: MAL" es MAL escena tab "$T/A" "$T/B"
capa -fill white -draw "rectangle 40,590 300,640" "$T/B/tab45.png.capa4-hoja-29,2-966x764.png"
si "tab: con puntos que se salen del rotulo hacia el de encima (Calidad, que no cambio): MAL" es MAL escena tab "$T/A" "$T/B"
cp "$T/A/tab45.png.capa4-dibujo2d-29,2-966x764.png" "$T/B/tab45.png.capa4-hoja-29,2-966x764.png"
hoja_estado "$T/B/estado-tab45.txt" Rotulo "faltan 4 archivos" "0%" "aun no hay bastantes medidas; de momento no toca nada." "Repasar otra vez"
si "tab: ademas otro rotulo distinto (un boton que no es del repaso): MAL" es MAL escena tab "$T/A" "$T/B"
hoja_estado "$T/B/estado-tab45.txt" Rotulo "ya esta al dia" "0%" "mas fuerza cuantas mas canciones medidas (van 12)." "Repasar"
si "tab: solo el porcentaje distinto, sin su «Empareja el tono»: MAL" es MAL escena tab "$T/A" "$T/B"
hoja_estado "$T/B/estado-tab45.txt" Rotulo "faltan 4 archivos" "0%" "aun no hay bastantes medidas; de momento no toca nada." "Repasar"
si "tab: lo mismo en otra escena (f3vistas): MAL" es MAL escena f3vistas "$T/A" "$T/B"

# ---- el nivel 1: --ignora con el doble, y sin la opcion ----
echo "== nivel 1 (--ignora RocolaOs:)"
D=$W/guiones; rm -rf "$D"; mkdir -p "$D"
cp "$GV/compara_escena.py" "$D/"; cp "$GV/compara_trazas.py" "$D/compara_trazas_real.py"; cp "$G/compara_trazas_ignora.py" "$D/compara_trazas.py"
escena "$E/A"; escena "$E/B"
printf '1000\ttick RocolaOs:latido 1000\n2000\ttick RocolaOs:latido 1000\n2001\tencargo RocolaOs:diagnostico\n3000\ttick RocolaOs:latido 1000\n' >> "$E/B/traza.txt"
sort -n -o "$E/B/traza.txt" "$E/B/traza.txt"
si "nivel 1: sin --ignora en compara_trazas.py: NO (no se compara)" es NO escena arranque "$E/A" "$E/B" --ignora RocolaOs: --guiones "$W/sin-ignora-$$"
mkdir -p "$W/sin-ignora-$$"; cp "$GV/compara_escena.py" "$W/sin-ignora-$$/"; printf 'import sys\nequivalentes = lambda q: q\n' > "$W/sin-ignora-$$/compara_trazas.py"
si "nivel 1: con un compara_trazas.py que no la tiene: NO" es NO escena arranque "$E/A" "$E/B" --ignora RocolaOs: --guiones "$W/sin-ignora-$$"
si "nivel 1: los relojes y encargos RocolaOs: no cuentan: BIEN" es BIEN escena arranque "$E/A" "$E/B" --ignora RocolaOs: --guiones "$D"
si "nivel 1: y la traza del programa sigue en su sitio (se aparto para comparar lo demas)" test -f "$E/B/traza.txt" -a ! -e "$E/B/traza.txt.nivel1"
si "sin --ignora (el nivel 0), esos mismos relojes y encargos SI cuentan: no sale BIEN" bash -c '[ "$(python3 "$1/clasifica.py" escena arranque "$2" "$3" | cut -f1)" != BIEN ]' sh "$RO" "$E/A" "$E/B"
sed -i 's/creditos=4/creditos=7/' "$E/B/estado-antes.txt"
si "nivel 1: con un estado distinto, ademas de lo de Rocola OS: MAL" es MAL escena arranque "$E/A" "$E/B" --ignora RocolaOs: --guiones "$D"
escena "$E/B"; printf '1000\ttick RocolaOs:latido 1000\n' >> "$E/B/traza.txt"; sed -i '/keyup 43/d' "$E/B/traza.txt"
si "nivel 1: con un suceso de menos en la secuencia: MAL" es MAL escena arranque "$E/A" "$E/B" --ignora RocolaOs: --guiones "$D"
si "una escena que falta: NO" es NO escena arranque "$E/A" "$E/no-esta"

# ---- los recorridos (clasifica.py recorridos) ----
echo "== recorridos (clasifica.py recorridos)"
R=$W/rec; rm -rf "$R"; mkdir -p "$R"
huellas() {   # huellas DIR RECORRIDO [lineas extra...]: unas huellas como las de huellas.py
  mkdir -p "$1/$2"
  { printf 'textura\t1024x128_en0,0_1024x128\taaaa\t0.2\ntextura\t512x256_en0,0_512x256\tbbbb\t0.2\ntextura\t256x256_en0,0_256x256\tcccc\t0.2\n'
    printf 'textura\t128x128_en0,0_128x128\tdddd\t0.2\n'
    printf 'dibujo2d\tc46.png.capa3-dibujo2d-0,18-1030x732.png\teeee\t-\ndibujo2d\tc8.png.capa1-dibujo2d-0,0-1024x768.png\tffff\t-\n'
    for l in "${@:3}"; do printf '%s\n' "$l"; done; } > "$1/$2/huellas.tsv"
}
for d in ref ref2 nuevo; do huellas "$R/$d" ctrlf3; done
echo "ctrlf3: salio con 0" > "$R/salio.log"
lin() { python3 "$RO/clasifica.py" recorridos "$R/nuevo" "$R/ref" "$R/ref2" "$R/salio.log" | head -1 | cut -f1; }
si "recorrido igual: BIEN" test "$(lin)" = BIEN
sed -i '/1024x128/d' "$R/nuevo/ctrlf3/huellas.tsv"
si "falta una textura quieta de la hora (1024x128): ACEPTADO" test "$(lin)" = ACEPTADO
huellas "$R/nuevo" ctrlf3; sed -i '/256x256/d' "$R/nuevo/ctrlf3/huellas.tsv"
si "falta una textura quieta que no es de la hora (256x256): REPETIR" test "$(lin)" = REPETIR
huellas "$R/nuevo" ctrlf3; sed -i 's/eeee/eeef/' "$R/nuevo/ctrlf3/huellas.tsv"
si "ctrlf3 c46 con F12 distinta (la de siempre): ACEPTADO" test "$(lin)" = ACEPTADO
huellas "$R/nuevo" ctrlf3; sed -i 's/ffff/fffe/' "$R/nuevo/ctrlf3/huellas.tsv"
si "otra capa quieta distinta: REPETIR" test "$(lin)" = REPETIR
for d in ref ref2 nuevo; do huellas "$R/$d" hojas 'dibujo2d	c21.png.capa4-dibujo2d-29,2-966x764.png	9999	-'; done; rm -rf "$R"/*/ctrlf3
echo "hojas: salio con 0" > "$R/salio.log"; sed -i 's/9999/9998/' "$R/nuevo/hojas/huellas.tsv"
si "hojas: la hoja de Sonido de Ctrl+F3 distinta a los 21 s (el repaso): ACEPTADO" test "$(lin)" = ACEPTADO
huellas "$R/nuevo" hojas 'dibujo2d	c21.png.capa5-dibujo2d-189,182-646x404.png	9999	-'; for d in ref ref2; do huellas "$R/$d" hojas 'dibujo2d	c21.png.capa5-dibujo2d-189,182-646x404.png	9997	-'; done
si "hojas: otra hoja distinta (no la de Sonido): REPETIR" test "$(lin)" = REPETIR
rm -rf "$R"/*/hojas; for d in ref ref2 nuevo; do huellas "$R/$d" ctrlf3; done; echo "ctrlf3: salio con 0" > "$R/salio.log"
huellas "$R/nuevo" ctrlf3; sed -i '/1024x128/d; /512x256/d' "$R/nuevo/ctrlf3/huellas.tsv"
si "faltan 2 de 4 texturas quietas de la hora (50 %, mas del tope): REPETIR" test "$(lin)" = REPETIR
huellas "$R/nuevo" ctrlf3; echo "ctrlf3: salio con 124" > "$R/salio.log"
si "un recorrido que no salio con 0: MAL" test "$(lin)" = MAL
si "la linea de TOTAL" bash -c 'python3 "$1/clasifica.py" recorridos "$2/nuevo" "$2/ref" "$2/ref2" "$2/salio.log" | grep -q "^TOTAL	0 de 1 salieron con 0; texturas quietas 4 de 4; capas quietas 2 de 2"' sh "$RO" "$R"

# ---- VistasFondo, los avisos y Codigo/ ----
echo "== fondo, avisos y codigo"
F=$W/fondo; mkdir -p "$F"
printf 'inicio\ttraducida=False\nuno\t1\n' > "$F/ref.tsv"; printf 'inicio\ttraducida=True\nuno\t1\n' > "$F/nuevo.tsv"
si "VistasFondo: solo la linea de marca: ACEPTADO" test "$(veredicto fondo "$F/ref.tsv" "$F/nuevo.tsv")" = ACEPTADO
printf 'inicio\ttraducida=True\nuno\t2\n' > "$F/nuevo.tsv"
si "VistasFondo: y otra linea: MAL" test "$(veredicto fondo "$F/ref.tsv" "$F/nuevo.tsv")" = MAL
X=$(cd "$RO/../.." && pwd)
grep -v '^#' "$RO/avisos-base.txt" | sed "s|^|$X/|; s|(\([0-9]*\)): \(.*\)$|(\1,9): warning \2: algo [x.csproj]|" > "$F/compila.log"
si "los avisos de la base: BIEN" test "$(veredicto avisos "$F/compila.log" "$RO/avisos-base.txt" "$X")" = BIEN
echo "$X/Codigo/Form1.cs(12,3): warning CS0001: nuevo [x.csproj]" >> "$F/compila.log"
si "un aviso nuevo: MAL" test "$(veredicto avisos "$F/compila.log" "$RO/avisos-base.txt" "$X")" = MAL
si "Codigo/ con las mismas lineas y CRLF que la base fc7b667 (esta copia)" test "$(veredicto codigo "$X" fc7b667)" = BIEN

# ---- los guiones ----
echo "== los guiones"
si "bash -n nivel0.sh" bash -n "$RO/nivel0.sh"
si "bash -n alterna.sh" bash -n "$RO/alterna.sh"
si "bash -n prueba.sh" bash -n "$G/prueba.sh"
si "alterna.sh sin argumentos dice su uso y sale con 2" bash -c 'bash "$1/alterna.sh" > /dev/null 2>&1; [ $? = 2 ]' sh "$RO"
# nivel0.sh con un paso que no existe y otro de nada: dice FALTA y sale con 2 (nada MAL)
si "nivel0.sh: un paso desconocido da FALTA y sale con 2" bash -c 'TRABAJO="$2/n0" PASOS="nada" bash "$1/nivel0.sh" prueba > "$2/n0.txt" 2>&1; r=$?; grep -q "^FALTA     nada: paso desconocido" "$2/n0.txt" && [ $r = 2 ]' sh "$RO" "$W"
si "nivel0.sh: el paso codigo solo, BIEN y sale con 0" bash -c 'TRABAJO="$2/n1" PASOS="codigo" bash "$1/nivel0.sh" prueba > "$2/n1.txt" 2>&1; r=$?; grep -q "^BIEN      codigo: 109 archivos" "$2/n1.txt" && [ $r = 0 ]' sh "$RO" "$W"
si "nivel0.sh corre sin ROCOLA_OS aunque quien lo llama lo tenga" bash -c 'grep -q "^unset ROCOLA_OS ROCOLA_RUN" "$1/nivel0.sh"' sh "$RO"
# el candado de lo pesado (pesado() de nivel0.sh): espera a quien lo tiene, apunta cuanto espero, y lo que corre dentro no lo hereda
L=$W/candado.lock; : > "$W/esperas.txt"
flock "$L" sleep 2 & sleep 0.3
( ESPERAS=$W/esperas.txt; PESADO=$L; eval "$(sed -n '/^pesado() {/p' "$RO/nivel0.sh")"
  pesado bash -c 'ls -l /proc/$$/fd | grep -c candado.lock' > "$W/hereda.txt" )
wait
si "pesado(): espero al que tenia el candado (2 s) y lo apunto en ESPERAS ($(cat "$W/esperas.txt") s)" test "$(cat "$W/esperas.txt")" -ge 1 -a "$(cat "$W/esperas.txt")" -le 3
si "pesado(): lo que corre dentro no hereda el candado (flock -o)" test "$(cat "$W/hereda.txt")" = 0

echo
[ "${GUARDA:-0}" = 1 ] || rm -rf "$W"
if [ $mal = 0 ]; then echo "TODO BIEN ($bien comprobaciones)"; else echo "$mal MAL de $((bien + mal))"; fi
[ $mal = 0 ]
