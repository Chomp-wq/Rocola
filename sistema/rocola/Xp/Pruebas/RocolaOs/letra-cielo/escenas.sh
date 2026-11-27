# (lo incluye prueba.sh con «source»: las escenas con la rocola entera)
# S ~ posicion + 16: la cancion empieza a sonar hacia los 16-17 s y el reposo se abre hacia los 36 s. Con el LRC de datos.json: verso 1 de 8 a 16 s,
# verso 2 de 16 a 24, verso 3 (largo) de 24 a 34, SIN VOZ de 34 a 44, verso 4 de 44 a 52...
analiza() { python3 "$G/analiza.py" "$@"; }
campo() { echo "$1" | grep -o "$2=[-0-9.]*" | head -1 | cut -d= -f2; }

# ---------------------------------------------------------------- compas
if [[ " $escenas " == *" compas "* ]]; then
  echo "== compas"
  NOMBRE=compas; falso compas; d=$(datos compas)
  # (el cambio de verso dura 3 s en esta escena, ROCOLA_LETRA_CAMBIO_S: cada captura para el cuadro casi un segundo)
  abre compas "$d" "$(pide 'rio dorado')" 96 ROCOLA_OS=1 ROCOLA_LETRA_CAMBIO_S=3 ROCOLA_CAPTURA="$(CAPS 44 45 46 47 48 50 54 58 59 60 61 62 63 64 65 66 67 68 69 70 71 72 74)" ROCOLA_CAPTURA_SALE=0
  espera compas; para_falso
  o="$W/compas"; L="$o/letra.log"
  si "compas: sale con 0 y escribio su log" bash -c "[ \"\$(cat $o/salio)\" = 0 ] && [ -s $L ]"
  si "compas: UNA peticion a LRCLIB (la letra de la que suena)" test "$(wc -l < "$o/falso/peticiones.txt")" = 1
  # el ritmo: la posicion avanza al paso del reloj (log: hora de pared y posicion)
  ritmo=$(python3 - "$L" <<'EOF'
import sys
p = [l.split() for l in open(sys.argv[1]) if l.split() and l.split()[0].isdigit()]
a = [(int(x[0]) / 1000.0, float(x[1].split("=")[1])) for x in p if float(x[1].split("=")[1]) > 20]
print("%.3f" % ((a[-1][1] - a[0][1]) / (a[-1][0] - a[0][0])) if len(a) > 10 else "0")
EOF
)
  si "compas: la posicion que usa la letra avanza al paso del reloj (razon $ritmo, de 0,97 a 1,03)" bash -c "awk 'BEGIN { exit !($ritmo >= 0.97 && $ritmo <= 1.03) }'"
  # (la captura se escribe al disco con un retraso de menos de un segundo: lo que se compara es el AVANCE entre capturas y que lo llenado vaya
  # detras de la cancion por un retraso fijo, el de escribir el PNG; la posicion y el ambar salen del mismo reloj)
  tot=0; ok=0; mono=1; ant=-1; antl=-1; ofs=""
  for s in 44 45 46 47 48 50; do
    f=$CAPTURAS/compas-$s.png; [ -f "$f" ] || continue
    lg=$(linea_log "$L" "$f"); fr=$(analiza relleno "$f" | grep -o 'frac=[0-9.]*' | cut -d= -f2); ll=$(campo "$lg" llena); [ -z "$fr" -o -z "$ll" ] && continue
    [ $s = 44 ] && continue   # (la primera captura de la corrida tarda mas en escribirse: no cuenta)
    tot=$((tot + 1)); echo "   S=$s: se ve $fr de ambar; la cancion llevaba $ll en el momento de escribir el PNG ($(campo "$lg" pos) s)"
    if [ "$antl" != -1 ] && awk "BEGIN { d = ($fr - $ant) - ($ll - $antl); exit !(d < 0.05 && d > -0.05) }"; then ok=$((ok + 1)); fi
    awk "BEGIN { exit !($fr >= $ant - 0.02) }" || mono=0
    ofs="$ofs $(echo "$ll $fr" | awk '{ printf "%.3f", $1 - $2 }')"; ant=$fr; antl=$ll
  done
  si "compas: el ambar avanza al paso de la cancion (entre una captura y la siguiente, a 0,05; $ok de $((tot - 1)))" test $tot -ge 4 -a $ok = $((tot - 1))
  si "compas: el ambar solo avanza, y el retraso con el log es el mismo en todas ($ofs; el de escribir el PNG)" bash -c "[ $mono = 1 ] && echo '$ofs' | awk '{ for (i = 1; i <= NF; i++) { if (\$i < 0 || \$i > 0.15) m = 1; if (i > 1 && (\$i - \$(i-1) > 0.04 || \$(i-1) - \$i > 0.04)) m = 1 } exit m }'"
  # el cambio de verso (3 s en esta escena): con u entre 0,2 y 0,8 en el log (retrasado lo que tarda en escribirse el PNG) los renglones van subiendo: la altura
  # media de la tinta es menor que la de la captura anterior con el mismo par de renglones quietos (u 0)
  ncam=0; subio=0
  for f in $CAPTURAS/compas-5[89].png $CAPTURAS/compas-[67][0-9].png; do
    [ -f "$f" ] || continue
    lg=$(linea_log "$L" "$f" 700); u=$(campo "$lg" u); v=$(campo "$lg" verso)
    if awk "BEGIN { exit !(${u:-1} > 0.2 && ${u:-1} < 0.8 && ${v:-0} >= 4) }"; then
      ncam=$((ncam + 1)); n=$(basename "$f" .png); k=${n#compas-}; q=$CAPTURAS/compas-$((k - 1)).png
      if [ -f "$q" ]; then c1=$(analiza centroide "$q"); c2=$(analiza centroide "$f"); echo "   cambio (S=$k, u $u): altura media de la tinta $c1 -> $c2"; awk "BEGIN { exit !($c2 < $c1 - 4) }" && subio=$((subio + 1)); fi
    fi
  done
  si "compas: en el cambio de verso ($ncam capturas con u de 0,2 a 0,8) los renglones suben ($subio con la tinta mas arriba que en la captura quieta de antes)" test $ncam -ge 1 -a $subio -ge 1
  si "compas: pasado el cambio (74 s) ya nada sube (pixeles claros sobre el renglon: $(analiza claro $CAPTURAS/compas-74.png 372 408))" test "$(analiza claro $CAPTURAS/compas-74.png 372 408)" -lt 400
  # sin voz (S 54, posicion ~38): el cielo de XP tal cual (nada en la banda de la letra)
  lg=$(linea_log "$L" "$CAPTURAS/compas-54.png")
  si "sinvoz: a los 54 s (posicion ~38: el renglon vacio del LRC) no hay letra a la vista (tinta $(analiza tinta $CAPTURAS/compas-54.png 372 515); el log, voz $(campo "$lg" voz))" bash -c "[ \$(python3 $G/analiza.py tinta $CAPTURAS/compas-54.png 372 515) -lt 300 ]"
  c=$(analiza contraste "$CAPTURAS/compas-46.png"); echo "   contraste (S=46, cielo de XP de la hora): $c"
  si "regla 3: el texto blanco se lee (contraste de 4,5 a 1 o mas) con la sombra" bash -c "echo '$c' | grep -oP 'blanco \K[0-9.]+' | awk '{ exit !(\$1 >= 4.5) }'"
  si "regla 3: el ambar y el lavanda se leen (3 a 1 o mas; son de letra grande en negrita)" bash -c "echo '$c' | grep -oP '(ambar|lavanda) \K[0-9.]+' | awk '{ if (\$1 < 3) m = 1 } END { exit m }'"
  cp "$CAPTURAS/compas-46.png" "$CAPTURAS/LETRA-llenando.png" 2>/dev/null
fi

# ---------------------------------------------------------------- mp3
if [[ " $escenas " == *" mp3 "* ]]; then
  echo "== mp3"
  NOMBRE=mp3; falso mp3; d=$(datos mp3)
  abre mp3 "$d" "$(pide 'luna de papel')" 82 ROCOLA_OS=1 ROCOLA_CAPTURA="$(CAPS 44 56 68)" ROCOLA_CAPTURA_SALE=0
  espera mp3; para_falso
  o="$W/mp3"; L="$o/letra.log"
  si "mp3: la letra es la del MP3 (el LRCLIB falso dijo 404)" contiene "$o/run/rocola-xp/letras/estado" "proveedor=el MP3"
  si "mp3: sin tiempos: nada se llena (llena=-1 en el log) y los renglones cambian despacio" bash -c "grep -q 'llena=-1.000' $L && ! grep -q 'llena=0\.[1-9]' $L"
  si "mp3: se ve la letra (tinta $(analiza tinta $CAPTURAS/mp3-44.png 372 515))" bash -c "[ \$(python3 $G/analiza.py tinta $CAPTURAS/mp3-44.png 372 515) -gt 1200 ]"
  si "mp3: sin ambar (no se llena; pixeles de ambar: $(analiza ambar $CAPTURAS/mp3-56.png))" bash -c "[ \$(python3 $G/analiza.py ambar $CAPTURAS/mp3-56.png) -lt 150 ]"
  si "mp3: a los 44, 56 y 68 s los renglones son otros (la banda cambia)" bash -c "[ \$(python3 $G/analiza.py igual $CAPTURAS/mp3-44.png $CAPTURAS/mp3-68.png) -gt 500 ]"
  cp "$CAPTURAS/mp3-56.png" "$CAPTURAS/LETRA-mp3.png" 2>/dev/null
fi

# ---------------------------------------------------------------- nada
if [[ " $escenas " == *" nada "* ]]; then
  echo "== nada"
  # (a las 45 s, con el reposo abierto y la letra a la vista, se toca algo: la pared; el buscador; el disco; la estrella de la ayuda)
  for caso in pared:45:39 buscador:45:27+38 disco:45:13 ayuda:45:27+40; do
    n=${caso%%:*}; r=${caso#*:}; t=${r%%:*}; k=${r#*:}
    NOMBRE=nada-$n; falso nada-$n; d=$(datos nada-$n)
    abre nada-$n "$d" "$(pide 'rio dorado'),$t:$k" 62 ROCOLA_OS=1 ROCOLA_CAPTURA="$(CAPS 43 $(echo "$t + 7" | bc))" ROCOLA_CAPTURA_SALE=0
    espera nada-$n; para_falso
    o="$W/nada-$n"; L="$o/letra.log"
    f=$CAPTURAS/nada-$n-43.png; g=$CAPTURAS/nada-$n-$(echo "$t + 7" | bc).png
    antes=$(analiza tinta "$f" 372 515); desp=$(analiza contraste "$g" 2>&1 | head -1)
    # la letra se va con el reposo: en el cuadro de la captura de 7 s despues (el log, con el retraso de escribirse el PNG) el reposo ya no esta abierto para ella
    # (idle 0, o ni una linea del log cerca: la letra solo anota con el reposo abierto, aunque sea a medias)
    lg=$(linea_log "$L" "$g" 700); dms=${lg%% *}; idl=$(campo "$lg" idle)
    si "nada ($n): con el reposo abierto (43 s) la letra se ve (tinta $antes) y tras tocar ($k a los $t s) ya no hay letra en el cuadro de los $(echo "$t + 7" | bc) s (log a $dms ms: idle ${idl:--})" bash -c "[ $antes -gt 800 ] && { [ -z '$lg' ] || [ ${dms:-99999} -gt 1000 ] || awk 'BEGIN { exit !(${idl:-0} < 0.01) }'; }"
  done
fi

# ---------------------------------------------------------------- cielo
if [[ " $escenas " == *" cielo "* ]]; then
  echo "== cielo"
  # la regla 3 con el cielo vivo encendido (cielo, fiestas y clima): de dia con nubes, de dia en una fiesta (el 20-11, Revolucion), al anochecer y de noche
  for caso in 'dia|2026-11-18 13:00|nublado,90,0,10,90,18' 'fiesta|2026-11-20 13:00|lluvia,90,2,10,90,15' 'tarde|2026-11-18 17:40|despejado,5,0,5,90,22' 'noche|2026-11-18 23:30|nublado,70,0,5,90,12'; do
    IFS="|" read -r n fecha clima <<< "$caso"
    NOMBRE=cielo-$n; falso cielo-$n; d=$(datos cielo-$n cielo=1 fiestas=1 clima=1 vidrio=1)
    abre cielo-$n "$d" "$(pide 'rio dorado')" 62 ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$fecha" ROCOLA_CLIMA="$clima" ROCOLA_CLIMA_YA=1 ROCOLA_CAPTURA="$(CAPS 45 47)" ROCOLA_CAPTURA_SALE=0
    espera cielo-$n; para_falso
    f=$CAPTURAS/cielo-$n-47.png; c=$(analiza contraste "$f"); echo "   $n ($fecha, $clima): $c"
    si "regla 3 ($n, cielo vivo encendido): el texto blanco se lee (4,5 a 1 o mas) y el ambar y el lavanda (3 a 1 o mas)" bash -c "echo '$c' | grep -oP 'blanco \K[0-9.]+' | awk '{ exit !(\$1 >= 4.5) }' && echo '$c' | grep -oP '(ambar|lavanda) \K[0-9.]+' | awk '{ if (\$1 < 3) m = 1 } END { exit m }'"
  done
fi

# ---------------------------------------------------------------- apagado
if [[ " $escenas " == *" apagado "* ]]; then
  echo "== apagado"
  cinta() { local n="$1" p="$2" d="$3"; shift 3; NOMBRE=$n; falso $n; PROG=$p abre $n "$d" "$(pide 'rio dorado')" 56 ROCOLA_CINTA="$W/$n.cinta" ROCOLA_CINTA_CUADROS=44-45.5 "$@"; espera $n; para_falso; }
  cinta base "$BASE" "$(datos base)" ROCOLA_OS=1
  cinta cero "$prog" "$(datos cero letra_cielo=0)" ROCOLA_OS=1
  cinta uno "$prog" "$(datos uno)" ROCOLA_OS=1
  cinta sin "$prog" "$(datos sin)"
  C() { PYTHONPATH="$PY" python3 "$CIE/cinta.py" compara "$1" "$2"; }
  C "$W/base.cinta" "$W/cero.cinta" > "$W/cero.cmp" 2>&1; r=$?; sed 's/^/   /' "$W/cero.cmp" | head -5
  # (el degradado del cielo del reposo sigue el color del genero de lo que suena y a veces cae en otro paso: se compara la forma de los cuadros, no su hash)
  si "APAGADO = antes: con letra_cielo=0 la forma de los cuadros de la tarjeta del reposo con musica es la de la rama sin la letra" bash -c "grep -q 'formas solo en la primera: 0; solo en la segunda: 0' $W/cero.cmp"
  for n in base cero uno; do PYTHONPATH="$PY" python3 "$G/cuenta.py" "$W/$n.cinta" > "$W/$n.cuenta" 2>&1; echo "   $n: $(cat $W/$n.cuenta)"; done
  qb=$(grep -o 'quads_por_cuadro=[0-9.]*' "$W/base.cuenta" | cut -d= -f2); qc=$(grep -o 'quads_por_cuadro=[0-9.]*' "$W/cero.cuenta" | cut -d= -f2); qu=$(grep -o 'quads_por_cuadro=[0-9.]*' "$W/uno.cuenta" | cut -d= -f2)
  si "APAGADO = antes: con letra_cielo=0 los dibujos de textura por cuadro son los de la rama sin la letra ($qc frente a $qb)" awk "BEGIN { exit !($qc == $qb) }"
  si "control: con la letra encendida la tarjeta dibuja mas texturas por cuadro ($qu frente a $qb: los renglones y su ambar)" awk "BEGIN { exit !($qu >= $qb + 2) }"
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
