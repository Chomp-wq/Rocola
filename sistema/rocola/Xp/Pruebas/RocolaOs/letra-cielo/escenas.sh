# (lo incluye prueba.sh con «source»: las escenas con la rocola entera)
# S ~ posicion + 16: la cancion empieza a sonar hacia los 16-17 s y el reposo se abre hacia los 36 s. Con el LRC de datos.json: verso 1 de 8 a 16 s,
# verso 2 de 16 a 24, verso 3 (largo) de 24 a 34, SIN VOZ de 34 a 44, verso 4 de 44 a 52...
analiza() { python3 "$G/analiza.py" "$@"; }
campo() { echo "$1" | grep -o "$2=[-0-9.]*" | head -1 | cut -d= -f2; }

# ---------------------------------------------------------------- columna
if [[ " $escenas " == *" columna "* ]]; then
  echo "== columna"
  NOMBRE=columna; falso columna; d=$(datos columna cielo=0 fiestas=0 clima=0 vidrio=0)
  # (el cambio de verso dura 3 s en esta escena, ROCOLA_LETRA_CAMBIO_S: cada captura para el cuadro casi un segundo)
  abre columna "$d" "$(pide 'rio dorado')" 100 ROCOLA_OS=1 ROCOLA_LETRA_CAMBIO_S=3 ROCOLA_CAPTURA="$(CAPS 44 46 47 48 49 50 51 52 56 60 64 68 72 76)" ROCOLA_CAPTURA_SALE=0
  espera columna; para_falso
  o="$W/columna"; L="$o/letra.log"; export GEO="$L"
  si "columna: sale con 0 y escribio su log con la geometria" bash -c "[ \"\$(cat $o/salio)\" = 0 ] && grep -q '^geo ' $L"
  si "columna: UNA peticion a LRCLIB (la letra de la que suena)" test "$(wc -l < "$o/falso/peticiones.txt")" = 1
  read -r gL gR gT gB gA glh gnom <<< "$(analiza geo)"; glh=${glh%.*}; echo "   la columna: x $gL a $gR, y $gT a $gB, el verso que se canta en y $gA (renglon $glh px)"
  # la estrella en su extremo izquierdo (centro 117 + 1,5 radios de 71) queda a la izquierda de la columna: la cuenta es la de la rocola con 1024x768
  si "columna: el borde izquierdo ($gL) esta a la derecha de la estrella en su extremo izquierdo (centro minimo 117 + 1,5 radios de 71 = 224) y el derecho cerca del borde (1024 x 0,955)" test "$gL" -ge 224 -a "$gR" -ge 960 -a "$gR" -le 990
  si "columna: vertical, sobre la raya del horizonte (el panel empieza en 523): arriba $gT, abajo $gB" test "$gT" -ge 90 -a "$gB" -le 500 -a "$gB" -ge 440
  # el resorte: los cambios de verso del log (3 s con esta escena)
  rs=$(analiza resorte "$L"); echo "   cambios de verso en el log: $rs"
  si "columna: en cada cambio el verso baja a su sitio sin retroceder y sin pasarse (resorte critico sin rebote) y llega" bash -c "echo '$rs' | grep -q 'mono=1' && echo '$rs' | awk '{ split(\$1, e, \"=\"); split(\$3, s, \"=\"); split(\$4, t, \"=\"); exit !(e[2] >= 3 && s[2] < 0.6 && t[2] != \"-\" && t[2] > 0.5 && t[2] < 3.2) }'"
  # (44: verso largo quieto; 56 y 60: tramo sin voz; 68: ya cantan el verso que vuelve)
  gr=$(analiza grupos "$CAPTURAS/columna-44.png"); echo "$gr" | sed 's/^/   44 s: grupo de tinta /'
  # (los renglones de tinta clara, uno por uno: todos arrancan en el borde izquierdo de la columna; los de poca tinta son restos del desvanecido)
  ng=$(echo "$gr" | awk -v l=$gL '$3 >= 1500 { d = $4 - l; if (d < 0) d = -d; if (d <= 16) n++ } END { print n + 0 }'); izq=$(echo "$gr" | awk -v l=$gL 'BEGIN { m = 9999 } $3 >= 150 { if ($4 < m) m = $4 } END { print m - l }')
  der=$(echo "$gr" | awk 'BEGIN { m = 0 } { if ($5 > m) m = $5 } END { print m }')
  si "columna: varios renglones de varios versos a la vista y alineados a la izquierda (renglones grandes que empiezan a menos de 16 px de $gL: $ng; ninguno a la izquierda de la columna: $izq; el borde derecho $der)" test "$ng" -ge 4 -a "$izq" -ge -16 -a "$der" -le $((gR + 12))
  nb=$(analiza blanco "$CAPTURAS/columna-44.png")
  li=$(analiza lineas "$CAPTURAS/columna-44.png"); nl=${li%% *}
  actual=$(echo "$gr" | sort -k3 -n -r | head -1); lact=$(echo "$actual" | awk '{ print $6 }')
  otros=$(echo "$gr" | awk -v a="$(echo "$li" | awk '{ split($2, p, "-"); print p[1] }')" -v z="$(echo "$li" | awk '{ split($NF, p, "-"); print p[2] }')" '($2 < a - 4 || $1 > z + 4) && $3 >= 1500 { if ($6 > m) m = $6 } END { print m + 0 }')
  si "columna: el verso largo se parte en varios renglones, alineados a la izquierda, sin achicarse ($nl renglones de blanco opaco: $li)" test "$nl" -ge 3 -a "$nl" -le 4
  si "columna: el que se canta es blanco opaco ($nb pixeles de blanco, luminancia $lact) y los demas translucidos (el mas claro de ellos $otros, menos de 0,8 del opaco)" bash -c "[ $nb -gt 1500 ] && awk 'BEGIN { exit !($lact > 0.9 && $otros < 0.8 * $lact && $otros > 0.2) }'"
  bj=$(analiza bajo "$CAPTURAS/columna-44.png")
  si "columna: se desvanece abajo (nada cortado de golpe: $bj pixeles de tinta clara entre la columna y el panel)" test "$bj" -lt 120
  c=$(analiza contraste "$CAPTURAS/columna-44.png"); echo "   contraste (S=44, cielo de XP de la hora): $c"
  si "regla 3: el verso que se canta (blanco opaco con su sombra) se lee (4,5 a 1 o mas)" bash -c "echo '$c' | grep -oP 'blanco \K[0-9.]+' | awk '{ exit !(\$1 >= 4.5) }'"
  # el cambio de verso (3 s): las capturas con el verso en camino: la tinta sube respecto a la quieta de antes
  c44=$(analiza centro "$CAPTURAS/columna-44.png"); mueve=0
  for s in 47 48 49 50 51; do
    f=$CAPTURAS/columna-$s.png; [ -f "$f" ] || continue
    c=$(analiza centro "$f"); echo "   S=$s: altura media de la tinta $c (quieta a los 44 s: $c44)"; awk "BEGIN { exit !($c < $c44 - 6) }" && mueve=$((mueve + 1))
  done
  si "columna: en el cambio de verso la columna entera sube ($mueve capturas con la tinta mas arriba que antes del cambio)" test $mueve -ge 2
  # el tramo sin voz (S=56 y 60: pos 40 y 44; el LRC tiene un vacio de 36 a 48 s): tres puntos en el sitio del verso, y nada opaco
  for s in 56 60; do
    pt=$(analiza puntos "$CAPTURAS/columna-$s.png"); nb=$(analiza blanco "$CAPTURAS/columna-$s.png"); echo "   S=$s (tramo sin voz): $pt; blanco opaco $nb"
    si "puntos ($s s): tres puntos del mismo tamano y parejamente separados, empezando en el borde izquierdo de la columna, y ningun verso opaco" bash -c "echo '$pt' | grep -q 'tres=1' && [ $nb -lt 1000 ]"
  done
  pt=$(analiza puntos "$CAPTURAS/columna-68.png"); nb=$(analiza blanco "$CAPTURAS/columna-68.png"); echo "   S=68: blanco opaco $nb; $pt"
  si "columna: pasado el tramo sin voz (68 s) el verso que vuelve es el opaco ($nb pixeles de blanco opaco, renglones $(analiza lineas "$CAPTURAS/columna-68.png"))" test $nb -gt 1200
  cp "$CAPTURAS/columna-44.png" "$CAPTURAS/LETRA-a-media-cancion.png" 2>/dev/null; cp "$CAPTURAS/columna-48.png" "$CAPTURAS/LETRA-b-cambio-de-verso.png" 2>/dev/null; cp "$CAPTURAS/columna-56.png" "$CAPTURAS/LETRA-c-sin-voz.png" 2>/dev/null
fi

# ---------------------------------------------------------------- mp3
if [[ " $escenas " == *" mp3 "* ]]; then
  echo "== mp3"
  NOMBRE=mp3; falso mp3; d=$(datos mp3 cielo=0 fiestas=0 clima=0 vidrio=0)
  abre mp3 "$d" "$(pide 'luna de papel')" 82 ROCOLA_OS=1 ROCOLA_CAPTURA="$(CAPS 44 56 68)" ROCOLA_CAPTURA_SALE=0
  espera mp3; para_falso
  o="$W/mp3"; L="$o/letra.log"; export GEO="$L"
  si "mp3: la letra es la del MP3 (el LRCLIB falso dijo 404)" contiene "$o/run/rocola-xp/letras/estado" "proveedor=el MP3"
  read -r gL gR gT gB gA glh gnom <<< "$(analiza geo)"
  for s in 44 56 68; do
    gr=$(analiza grupos "$CAPTURAS/mp3-$s.png"); nb=$(analiza blanco "$CAPTURAS/mp3-$s.png")
    # (los versos que quedan enteros dentro del cuerpo de la columna: sin el desvanecido de arriba y de abajo)
    lums=$(echo "$gr" | awk -v t=$((gT + 30)) -v b=$((gB - 20)) '$1 >= t && $2 <= b && $3 >= 800 { print $6 }' | sort -n)
    n=$(echo "$lums" | grep -c .); mn=$(echo "$lums" | head -1); mx=$(echo "$lums" | tail -1)
    echo "   S=$s: $n grupos en el cuerpo (luminancia $mn a $mx); blanco opaco $nb"
    si "mp3 ($s s): se ve la letra, todos los versos igual de visibles, ni opacos ni translucidos (luminancia $mn a $mx, sin verso resaltado ni blanco opaco: $nb) y alineados a la izquierda" bash -c "[ $n -ge 1 ] && [ $nb -lt 400 ] && awk 'BEGIN { exit !($mn > 0.5 && $mx < 0.92) }' && echo '$gr' | awk -v l=$gL 'BEGIN { m = 9999 } \$3 >= 800 { if (\$4 < m) m = \$4 } END { exit !(m - l >= -16) }'"
  done
  si "mp3: la columna se desliza despacio y continua (la tinta de los 68 s esta mas arriba que la de los 44 s)" bash -c "awk 'BEGIN { exit !($(analiza centro "$CAPTURAS/mp3-68.png") < $(analiza centro "$CAPTURAS/mp3-44.png") - 8) }'"
  sv=$(analiza suave "$L"); echo "   el verso de referencia entre dos lineas del log (px por segundo de cancion): $sv"
  si "mp3: continua: el verso de referencia nunca se mueve a mas de 30 px por segundo de cancion (lo normal con estos versos: 6 a 19)" bash -c "echo '$sv' | awk '{ split(\$1, m, \"=\"); split(\$2, n, \"=\"); exit !(n[2] > 100 && m[2] < 30) }'"
  cp "$CAPTURAS/mp3-56.png" "$CAPTURAS/LETRA-d-del-mp3.png" 2>/dev/null
fi

# ---------------------------------------------------------------- nada
if [[ " $escenas " == *" nada "* ]]; then
  echo "== nada"
  # (a las 45 s, con el reposo abierto y la letra a la vista, se toca algo: la pared; el buscador; el disco; la estrella de la ayuda)
  for caso in pared:45:39 buscador:45:27+38 disco:45:13 ayuda:45:27+40; do
    n=${caso%%:*}; r=${caso#*:}; t=${r%%:*}; k=${r#*:}
    NOMBRE=nada-$n; falso nada-$n; d=$(datos nada-$n cielo=0 fiestas=0 clima=0 vidrio=0)
    abre nada-$n "$d" "$(pide 'rio dorado'),$t:$k" 62 ROCOLA_OS=1 ROCOLA_CAPTURA="$(CAPS 43 $(echo "$t + 7" | bc))" ROCOLA_CAPTURA_SALE=0
    espera nada-$n; para_falso
    o="$W/nada-$n"; export GEO="$o/letra.log"
    f=$CAPTURAS/nada-$n-43.png; g=$CAPTURAS/nada-$n-$(echo "$t + 7" | bc).png
    antes=$(analiza blanco "$f"); cesa=$(analiza cesa "$g")
    # la letra se va con el reposo: lo que se tocaron (la pared, el buscador, un disco, la ayuda) la quita, y su log (que solo anota con la letra a la vista)
    # se calla: ni un cuadro anotado en los 4 s antes de la captura de 7 s despues de tocar
    si "nada ($n): con el reposo abierto (43 s) la letra se ve (blanco opaco $antes) y tras tocar ($k a los $t s) deja de verse: la letra anoto $cesa cuadros en los 4 s antes de la captura de los $(echo "$t + 7" | bc) s" bash -c "[ $antes -gt 1200 ] && [ $cesa -eq 0 ]"
  done
fi

# ---------------------------------------------------------------- cielo
if [[ " $escenas " == *" cielo "* ]]; then
  echo "== cielo"
  # la regla 3 con el cielo vivo encendido (cielo, fiestas y clima): de dia con nubes, de dia en una fiesta (el 20-11, Revolucion), al anochecer y de noche
  for caso in 'dia|2026-11-18 13:00|nublado,90,0,10,90,18' 'fiesta|2026-11-20 13:00|lluvia,90,2,10,90,15' 'tarde|2026-11-18 17:40|despejado,5,0,5,90,22' 'noche|2026-11-18 23:30|nublado,70,0,5,90,12'; do
    IFS="|" read -r n fecha clima <<< "$caso"
    NOMBRE=cielo-$n; falso cielo-$n; d=$(datos cielo-$n cielo=1 fiestas=1 clima=1 vidrio=1)
    abre cielo-$n "$d" "$(pide 'rio dorado')" 62 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$fecha" ROCOLA_CLIMA="$clima" ROCOLA_CLIMA_YA=1 ROCOLA_CAPTURA="$(CAPS 44)" ROCOLA_CAPTURA_SALE=0
    espera cielo-$n; para_falso
    export GEO="$W/cielo-$n/letra.log"
    f=$CAPTURAS/cielo-$n-44.png; c=$(analiza contraste "$f"); echo "   $n ($fecha, $clima): $c"
    si "regla 3 ($n, cielo vivo encendido): el verso que se canta (blanco opaco con sombra) se lee (4,5 a 1 o mas)" bash -c "echo '$c' | grep -oP 'blanco \K[0-9.]+' | awk '{ exit !(\$1 >= 4.5) }'"
  done
  cp "$CAPTURAS/cielo-dia-44.png" "$CAPTURAS/LETRA-e-de-dia-cielo-vivo.png" 2>/dev/null
fi

# ---------------------------------------------------------------- apagado
if [[ " $escenas " == *" apagado "* ]]; then
  echo "== apagado"
  cinta() { local n="$1" p="$2" d="$3"; shift 3; NOMBRE=$n; falso $n; PROG=$p abre $n "$d" "$(pide 'rio dorado')" 56 ROCOLA_CINTA="$W/$n.cinta" ROCOLA_CINTA_CUADROS=44-45.5 "$@"; espera $n; para_falso; }
  # (27-11-2026: las cuatro funciones del cielo vienen encendidas de fabrica; aqui van en 0 en las tres para que lo unico que cambie sea la letra)
  cinta base "$BASE" "$(datos base cielo=0 fiestas=0 clima=0 vidrio=0)" ROCOLA_OS=1
  cinta cero "$prog" "$(datos cero letra_cielo=0 cielo=0 fiestas=0 clima=0 vidrio=0)" ROCOLA_OS=1
  cinta uno "$prog" "$(datos uno cielo=0 fiestas=0 clima=0 vidrio=0)" ROCOLA_OS=1
  cinta sin "$prog" "$(datos sin)"
  C() { PYTHONPATH="$PY" python3 "$CIE/cinta.py" compara "$1" "$2"; }
  C "$W/base.cinta" "$W/cero.cinta" > "$W/cero.cmp" 2>&1; r=$?; sed 's/^/   /' "$W/cero.cmp" | head -5
  # (el degradado del cielo del reposo sigue el color del genero de lo que suena y a veces cae en otro paso: se compara la forma de los cuadros, no su hash)
  si "APAGADO = antes: con letra_cielo=0 la forma de los cuadros de la tarjeta del reposo con musica es la de la rama sin la letra" bash -c "grep -q 'formas solo en la primera: 0; solo en la segunda: 0' $W/cero.cmp"
  for n in base cero uno; do PYTHONPATH="$PY" python3 "$G/cuenta.py" "$W/$n.cinta" > "$W/$n.cuenta" 2>&1; echo "   $n: $(cat $W/$n.cuenta)"; done
  qb=$(grep -o 'quads_por_cuadro=[0-9.]*' "$W/base.cuenta" | cut -d= -f2); qc=$(grep -o 'quads_por_cuadro=[0-9.]*' "$W/cero.cuenta" | cut -d= -f2)
  lb=$(grep -o 'lotes_por_cuadro=[0-9.]*' "$W/base.cuenta" | cut -d= -f2); lc=$(grep -o 'lotes_por_cuadro=[0-9.]*' "$W/cero.cuenta" | cut -d= -f2); lu=$(grep -o 'lotes_por_cuadro=[0-9.]*' "$W/uno.cuenta" | cut -d= -f2)
  si "APAGADO = antes: con letra_cielo=0 los dibujos de textura por cuadro (sueltos $qc frente a $qb; en lote $lc frente a $lb) son los de la rama sin la letra (a 0,5 por cuadro: la cinta son 1,5 s y los cuadros que caben cambian con la carga)" awk "BEGIN { d = $qc - $qb; e = $lc - $lb; exit !(d < 0.5 && d > -0.5 && e < 0.5 && e > -0.5) }"
  si "control: con la letra encendida la tarjeta dibuja mas lotes de texturas por cuadro ($lu frente a $lb: los versos, el nitido y el desenfocado)" awk "BEGIN { exit !($lu >= $lb + 2) }"
  si "sin ROCOLA_OS: ni log de la letra ni archivos de Rocola OS" bash -c "[ ! -e $W/sin/letra.log ] && [ ! -e $W/datos-sin/rocola_os_bitacora.txt ] && [ ! -e $W/sin/run/rocola-xp/letras ]"
  si "con letra_cielo=0 no hay log de la letra a la vista" bash -c "[ ! -s $W/cero/letra.log ]"
fi

# ---------------------------------------------------------------- reloj
if [[ " $escenas " == *" reloj "* ]]; then
  echo "== reloj"
  for v in con sin; do
    NOMBRE=reloj-$v; falso reloj-$v
    if [ $v = con ]; then d=$(datos reloj-$v); else d=$(datos reloj-$v letra_cielo=0); fi
    PROG=$AOTMIO abre reloj-$v "$d" "$(pide 'rio dorado')" 70 ROCOLA_OS=1 ROCOLA_CIELO_RELOJ="$W/reloj-$v.txt"
    espera reloj-$v; para_falso
    echo "   $v la letra: $(cat $W/reloj-$v.txt 2>/dev/null | tr '\n' ' ')"
  done
  si "reloj: se midieron las dos corridas" bash -c "[ -s $W/reloj-con.txt ] && [ -s $W/reloj-sin.txt ]"
fi

echo "== $bien BIEN, $mal MAL"
[ $mal = 0 ]
