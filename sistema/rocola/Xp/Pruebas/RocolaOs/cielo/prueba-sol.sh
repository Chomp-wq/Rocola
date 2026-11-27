#!/bin/bash
# LA PRUEBA DEL GRUPO «SOL, LUNA Y ESTRELLAS» DEL CIELO VIVO (DN2, DN3, DN4: RocolaOs/RocolaOs.CieloSol.cs, CieloLuna.cs y CieloEstrellas.cs;
# FASE11_cielo-sol.md; PLAN_CIELO_VIVO.md §7). La rocola entera fuera de pantalla (1024x768, llvmpipe), sin red (ROCOLA_CLIMA_API a un puerto
# donde no escucha nadie), con datos INVENTADOS que se arman aqui (120 canciones de ffmpeg en 20 artistas, 8 teclas, sin Spotify) si no estan.
# Zona de la hora: America/Mexico_City. Aqui no esta /root/rocola-trabajo (las letras de XP, los datos de las maquetas): las letras son las
# del sistema (DejaVu) y los datos los de arriba; sirve para comparar el cielo, no para las medidas de pixel de los textos de XP.
#   bash prueba-sol.sh PROGRAMA [partes...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   BASE:     el programa de la rama principal (JIT), para «apagado» (de fabrica $T/base-jit)
#   W:        la carpeta de trabajo (de fabrica $T/prueba-sol); CAPTURAS (de fabrica $W/capturas)
#   T:        donde quedan los datos inventados, brotli (de fabrica /tmp/rocola-cielo-sol)
# partes (de fabrica todas): ganchos orden apagado escenas luna fugaz reloj
#   ganchos  ganchos.py TODO BIEN y los ganchos g300-g303 en el manifiesto
#   orden    `rocola-xp --prueba-cielo-sol` (las cuentas puras: la proyeccion, el recorte de las mallas, los umbrales de las estrellas)
#   apagado  APAGADO = XP: la cinta de la tarjeta de la pared (cinta.py) sin ROCOLA_OS, con ROCOLA_OS=1 y las cuatro funciones en 0, y con las
#            tres ideas apagadas (sol=0, luna_camino=0, estrellas_anochecer=0 con el resto del cielo tambien en 0): iguales a BASE
#   escenas  capturas a las 6:50, 9:30, 13:00, 17:40, 18:35 y 22:00 (el buzon /run/rocola-cielo-ver) del escaparate, la pared, el disco abierto y
#            el reposo con musica, con todo encendido (todo), con la luna en su camino (lunab) y con cada idea sola (sol, estrellas); y la
#            regla 3 medida en el escaparate y el disco (contraste.py)
#   luna     la luna A de dia y de noche, y las fases (ROCOLA_CIELO_LUNA=1 con ROCOLA_CIELO_FECHA a 8 fechas del mes)
#   fugaz    las fugaces al azar de XP de dia con DN4 (cuenta de DrawShootStreaks en la cinta) y de noche
#   reloj    p50/p95/p99/max del trabajo de cada cuadro (ROCOLA_CIELO_RELOJ) con todo encendido y todo apagado, en NativeAOT (AOTMIO)
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba-sol.sh PROGRAMA [partes]}"; shift
partes="${*:-ganchos orden apagado escenas luna fugaz reloj}"
# (rutas absolutas: las ordenes corren desde otras carpetas)
abs() { if [ "${1#aot:}" != "$1" ]; then echo "aot:$(cd "${1#aot:}" && pwd)"; else echo "$(cd "$1" && pwd)"; fi; }
prog=$(abs "$prog")
T=${T:-/tmp/rocola-cielo-sol}; mkdir -p "$T"
W=${W:-$T/prueba-sol}
BASE=${BASE:-$T/base-jit}
AOTMIO=${AOTMIO:-aot:$T/aot}
CAPTURAS=${CAPTURAS:-$W/capturas}
PY=${PY:-$T/py}
rm -rf "$W"; mkdir -p "$W" "$CAPTURAS"
bien=0; mal=0
si() { local d="$1"; shift; if "$@"; then echo "BIEN  $d"; bien=$((bien + 1)); else echo "MAL   $d"; mal=$((mal + 1)); fi; }
contiene() { grep -qF -- "$2" "$1" 2>/dev/null; }
rx() { if [ "${1#aot:}" != "$1" ]; then echo "$(cd "${1#aot:}" && pwd)/rocola-xp"; else echo "dotnet $(cd "$1" && pwd)/rocola-xp.dll"; fi; }
SINRED="http://127.0.0.1:9"
CIUDAD="19.43,-99.13,Ciudad de México"

# ---------------------------------------------------------------- los datos inventados
PLANTILLA=$T/plantilla; MUSICA=$T/musica
if [ ! -d "$MUSICA" ]; then
  mkdir -p "$MUSICA"
  python3 - "$MUSICA" <<'PY'
import subprocess, os, random, sys
random.seed(3); raiz = sys.argv[1]; n = 0
artistas = ["Los Tigres del Norte", "Vicente Fernandez", "Selena", "Juan Gabriel", "Luis Miguel", "Rocio Durcal", "Los Angeles Azules", "Caifanes", "Mana",
            "Cafe Tacvba", "Joan Sebastian", "Ana Gabriel", "Banda MS", "Los Bukis", "Pedro Infante", "Intocable", "Shakira", "Chayanne", "Gloria Trevi", "Alejandro Fernandez"]
for a in artistas:
    for d in range(2):
        alb = "Disco %d de %s" % (d + 1, a.split()[0]); p = os.path.join(raiz, a, alb); os.makedirs(p, exist_ok=True)
        for t in range(3):
            f = os.path.join(p, "%02d Cancion %d.mp3" % (t + 1, n)); n += 1
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "lavfi", "-i", "sine=frequency=%d:duration=240" % (200 + random.randint(0, 400)), "-ar", "44100", "-b:a", "128k",
                            "-metadata", "title=Cancion numero %d" % n, "-metadata", "artist=" + a, "-metadata", "album=" + alb, "-metadata", "track=%d" % (t + 1), f], check=True)
PY
fi
if [ ! -d "$PLANTILLA" ]; then
  mkdir -p "$PLANTILLA"
  printf 'arriba=38\r\nabajo=40\r\nizquierda=37\r\nderecha=39\r\nponer=13\r\nregresar=27\r\nmoneda=45\r\nsaltar=46\r\n' > "$PLANTILLA/rocola_teclas.txt"
  printf 'dir=%s\r\n' "$MUSICA" > "$PLANTILLA/rocola_config.txt"
fi
[ -d "$PY/brotli" ] || pip install -q --target "$PY" brotli >/dev/null 2>&1
AL=$T/alsa; mkdir -p "$AL"

# corre NOMBRE PROGRAMA SEGUNDOS ROCOLA_OS_TXT [VAR=valor...]: la rocola con datos nuevos (la plantilla y ese rocola_os.txt); en segundo plano (su PID en
# $W/NOMBRE/pid). ROCOLA_OS_TXT: «-» no escribe ninguno (el de fabrica: las funciones del cielo apagadas)
corre() {
  local n="$1" p="$2" seg="$3" os="$4"; shift 4
  local o="$W/$n"; mkdir -p "$o/run" "$o/datos"; cp -r "$PLANTILLA"/. "$o/datos"/
  [ "$os" = "-" ] || printf 'spotify=0\n%b' "$os" > "$o/datos/rocola_os.txt"
  ( cd "$o/datos" && exec env -u ROCOLA_OS -u ROCOLA_CIELO_FECHA -u ROCOLA_CLIMA -u ROCOLA_CIUDAD -u ROCOLA_CIELO_LUNA ROCOLA_DATOS="$o/datos" ROCOLA_RUN="$o/run" \
      ROCOLA_PANTALLA=fuera:1024x768 TZ=America/Mexico_City "$@" timeout -s TERM "$seg" $(rx "$p") ) > "$o/salida.txt" 2>&1 &
  echo $! > "$o/pid"
}
espera() { for n in "$@"; do wait "$(cat "$W/$n/pid")" 2>/dev/null; done; }
bit() { echo "$W/$1/datos/rocola_os_bitacora.txt"; }

# ---------------------------------------------------------------- ganchos
if [[ " $partes " == *" ganchos "* ]]; then
  echo "== ganchos"
  python3 "$XP/Pruebas/RocolaOs/ganchos.py" > "$W/ganchos.txt" 2>&1; r=$?
  si "ganchos.py: TODO BIEN" test $r = 0 -a "$(tail -1 "$W/ganchos.txt")" = "TODO BIEN"
  si "los cuatro ganchos del grupo (g300 a g303) en el manifiesto" test "$(grep -cP '^g30[0-3]\t' "$XP/RocolaOs/GANCHOS.tsv")" = 4
fi
# ---------------------------------------------------------------- orden
if [[ " $partes " == *" orden "* ]]; then
  echo "== orden"
  ( cd "$W" && env ROCOLA_OS=1 ROCOLA_DATOS="$W/orden-datos" ROCOLA_RUN="$W/orden-run" ROCOLA_PANTALLA=fuera:1024x768 $(rx "$prog") --prueba-cielo-sol ) > "$W/orden.txt" 2>&1; r=$?
  grep -E "^(MAL|BIEN)" "$W/orden.txt" | sed 's/^/   /' | grep MAL
  si "las cuentas puras: $(tail -1 "$W/orden.txt")" test $r = 0
fi
# ---------------------------------------------------------------- apagado
FECHA13="2026-11-18 13:00"
cinta() {   # NOMBRE PROGRAMA OS [VAR=valor...]: la pared sin teclas, los cuadros de los 10 a los 11,5 s
  local n="$1" p="$2" os="$3"; shift 3
  corre "$n" "$p" 14 "$os" ROCOLA_CINTA="$W/$n.cinta" ROCOLA_CINTA_CUADROS=10-11.5 "$@"
}
if [[ " $partes " == *" apagado "* ]]; then
  echo "== apagado"
  TODO0='cielo=0\nfiestas=0\nclima=0\nvidrio=0\n'
  IDEAS0='cielo=1\nfiestas=0\nclima=0\nvidrio=0\ncielo_sol=0\nsol=0\nluna_camino=0\nestrellas_anochecer=0\nhorizonte_hora=0\nnubes=0\n'
  cinta base-sin "$BASE" -; espera base-sin
  cinta mio-sin "$prog" -; espera mio-sin
  cinta mio-cero "$prog" "$TODO0" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$FECHA13" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED; espera mio-cero
  cinta mio-ideas0 "$prog" "$IDEAS0" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$FECHA13" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED; espera mio-ideas0
  # (control: DN2 sola a las 17:40, cuando asoma tambien en la pared: la cinta SI cambia)
  cinta control-sol "$prog" 'cielo=1\nfiestas=0\nclima=0\nvidrio=0\ncielo_sol=0\nluna_camino=0\nestrellas_anochecer=0\nhorizonte_hora=0\nnubes=0\n' ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 17:40" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED; espera control-sol
  for v in mio-sin mio-cero mio-ideas0; do
    PYTHONPATH="$PY" python3 "$G/cinta.py" compara "$W/base-sin.cinta" "$W/$v.cinta" > "$W/$v.compara" 2>&1; r=$?
    sed 's/^/   /' "$W/$v.compara"
    si "APAGADO = XP ($v): la cinta de la pared igual a la de la rama principal" test $r = 0
  done
  PYTHONPATH="$PY" python3 "$G/cinta.py" compara "$W/base-sin.cinta" "$W/control-sol.cinta" > "$W/control-sol.compara" 2>&1; r=$?
  sed 's/^/   /' "$W/control-sol.compara"
  si "el control (el sol solo, a las 17:40): la cinta de la pared SI cambia (el comparador ve el sol)" test $r = 1
fi

# ---------------------------------------------------------------- escenas
HORAS="06:50 09:30 13:00 17:40 18:35 22:00"
# barrido NOMBRE T0: cada 6 s una hora de HORAS por el buzon y la captura 4 s despues
caps_de() { local n="$1" t0="$2" k=0 caps=""; for h in $HORAS; do caps="$caps,$(echo "$t0 + $k * 6 + 4" | bc):$CAPTURAS/$n-${h/:/}.png"; k=$((k + 1)); done; echo "${caps#,}"; }
buzon() { local n="$1" t0="$2"; ( sleep "$t0"; for h in $HORAS; do echo "2026-11-18 $h" > "$W/$n/run/rocola-cielo-ver"; sleep 6; done ) & }
# variantes de rocola_os.txt
declare -A V
V[todo]='cielo=1\nfiestas=0\nclima=0\nvidrio=0\n'
V[lunab]='cielo=1\nfiestas=0\nclima=0\nvidrio=0\nluna_camino=1\n'
V[sol]='cielo=1\nfiestas=0\nclima=0\nvidrio=0\ncielo_sol=0\nluna_camino=0\nestrellas_anochecer=0\nhorizonte_hora=0\nnubes=0\n'
V[estrellas]='cielo=1\nfiestas=0\nclima=0\nvidrio=0\ncielo_sol=0\nsol=0\nluna_camino=0\nhorizonte_hora=0\nnubes=0\n'
PIDE="6:45,6.8:45,8:27+39,9.3:27+39,10.6:27+39,11.9:27+39,21:39,21.5:39,22:39,22.5:39,27.5:13,29.5:13"
escena() {   # VARIANTE ESCENA
  local v="$1" e="$2" n="$1-$2" t0 seg pulsa=""
  case $e in pared) t0=24; seg=70;; escaparate) t0=54; seg=100;; disco) t0=34; seg=80; pulsa="$PIDE";; reposo) t0=84; seg=130; pulsa="$PIDE,31.5:27";; esac
  corre "$n" "$prog" $seg "${V[$v]}" ROCOLA_OS=1 ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA="$(caps_de "$n" $t0)" ROCOLA_CAPTURA_SALE=1
  buzon "$n" $t0
}
contraste() { python3 "$G/contraste.py" "$@"; }
if [[ " $partes " == *" escenas "* ]]; then
  echo "== escenas"
  for v in todo lunab; do
    for e in pared escaparate disco reposo; do escena $v $e; done
    espera $v-pared $v-escaparate $v-disco $v-reposo
  done
  for v in sol estrellas; do escena $v pared; escena $v escaparate; done
  espera sol-pared sol-escaparate estrellas-pared estrellas-escaparate
  for v in todo lunab; do
    for e in pared escaparate disco reposo; do
      nc=0; for h in $HORAS; do [ -f "$CAPTURAS/$v-$e-${h/:/}.png" ] && nc=$((nc + 1)); done
      si "$v $e: las 6 capturas ($nc) y «ver como se veria» 6 veces en la bitacora" test $nc = 6 -a "$(grep -c 'ver como se veria: 2026-11-18' "$(bit $v-$e)")" = 6
    done
  done
  # la regla 3 (el cielo hasta 0,848 del alto no pasa de luminancia 0,17 donde hay texto): el escaparate bajo y a la derecha del cubo, el disco bajo la
  # portada y a la izquierda de la lista, el reposo sobre la raya (en 525) y la pared sin portadas fuera de la barra de arriba y de la luna
  for v in todo lunab; do for h in $HORAS; do
    for z in "escaparate --solo 560,560,1000,650" "disco --solo 0,560,430,650"; do
      set -- $z; e=$1; shift; f=$CAPTURAS/$v-$e-${h/:/}.png; [ -f "$f" ] || continue
      contraste "$f" "$@" > "$W/contraste-$v-$e-$h.txt" 2>&1
      si "regla 3 en $v $e a las $h: $(cat "$W/contraste-$v-$e-$h.txt")" contiene "$W/contraste-$v-$e-$h.txt" "regla 3 (<= 0,17): BIEN"
    done
    f=$CAPTURAS/$v-reposo-${h/:/}.png
    if [ -f "$f" ]; then
      contraste "$f" --hasta 0.668 --solo 0,100,1024,513 > "$W/contraste-$v-reposo-$h.txt" 2>&1
      si "regla 3 en $v reposo a las $h: $(cat "$W/contraste-$v-reposo-$h.txt")" contiene "$W/contraste-$v-reposo-$h.txt" "regla 3 (<= 0,17): BIEN"
    fi
    f=$CAPTURAS/$v-pared-${h/:/}.png
    if [ -f "$f" ]; then
      contraste "$f" 0,0,1024,100 0,90,260,290 760,100,960,270 > "$W/contraste-$v-pared-$h.txt" 2>&1
      si "regla 3 en $v pared (sin la barra de arriba, la portada ni la luna) a las $h: $(cat "$W/contraste-$v-pared-$h.txt")" contiene "$W/contraste-$v-pared-$h.txt" "regla 3 (<= 0,17): BIEN"
    fi
  done; done
fi

# ---------------------------------------------------------------- luna
if [[ " $partes " == *" luna "* ]]; then
  echo "== luna"
  # fases: 8 fechas de un mes (el 2026-11-09 la luna estaba nueva: 8 pasos de 3,7 dias), a las 23:30 (la noche cerrada: la luna de XP) con la fase de la hora
  # del cielo (ROCOLA_CIELO_LUNA=1); y la A de dia (13:00: sobre el horizonte o no) frente a la de noche
  k=0; for d in 2026-11-09 2026-11-13 2026-11-17 2026-11-20 2026-11-24 2026-11-28 2026-12-02 2026-12-06; do
    corre luna-$k "$prog" 36 'cielo=1\nfiestas=0\nclima=0\nvidrio=0\n' ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$d 23:30" ROCOLA_CIELO_LUNA=1 ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CAPTURA="30:$CAPTURAS/luna-fase-$k.png" ROCOLA_CAPTURA_SALE=1
    k=$((k + 1)); [ $((k % 4)) = 0 ] && espera luna-$((k - 4)) luna-$((k - 3)) luna-$((k - 2)) luna-$((k - 1))
  done
  # la luna B de dia y de noche (con la luna sobre el horizonte el 2026-11-24 a las 13:00 y 20:00)
  for h in 13:00 20:00 02:00; do
    corre lunab-${h/:/} "$prog" 36 'cielo=1\nfiestas=0\nclima=0\nvidrio=0\nluna_camino=1\n' ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-24 $h" ROCOLA_CIELO_LUNA=1 ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CAPTURA="30:$CAPTURAS/lunab-${h/:/}.png" ROCOLA_CAPTURA_SALE=1
  done
  espera luna-4 luna-5 luna-6 luna-7 lunab-1300 lunab-2000 lunab-0200
  nc=0; for i in 0 1 2 3 4 5 6 7; do [ -f "$CAPTURAS/luna-fase-$i.png" ] && nc=$((nc + 1)); done
  si "las 8 capturas de la luna en sus fases ($nc)" test $nc = 8
  PYTHONPATH="$PY" python3 - "$CAPTURAS" > "$W/luna-fases.txt" 2>&1 <<'PY'
import sys, numpy as np
from PIL import Image
# lo iluminado de la luna (la parte clara del disco en 850,169, radio 32): la fraccion de pixeles claros crece del 9 (nueva) al 24 de noviembre (llena)
res = []
for i in range(8):
    im = np.asarray(Image.open("%s/luna-fase-%d.png" % (sys.argv[1], i)).convert('L'), np.float64)
    y, x = np.ogrid[:im.shape[0], :im.shape[1]]
    disco = (x - 850) ** 2 + (y - 169) ** 2 <= 30 ** 2
    res.append((im[disco] > 120).mean())
print(' '.join('%.2f' % r for r in res))
ok = res[0] < 0.2 and max(res) > 0.5 and res.index(max(res)) in (3, 4, 5)
sys.exit(0 if ok else 1)
PY
  r=$?; si "la fraccion clara de la luna por fase (nueva, creciente, llena, menguante): $(cat "$W/luna-fases.txt")" test $r = 0
fi

# ---------------------------------------------------------------- fugaz
if [[ " $partes " == *" fugaz "* ]]; then
  echo "== fugaz"
  # las fugaces al azar de XP salen cada 7 a 16 s: en 100 s hay unas siete. De dia con DN4 su alfa va a 0 (g300): la cinta de DrawShootStreaks lleva
  # los vertices con su color; se cuentan los de alfa > 8 en cada corrida (13:00 sin DN4 frente a con DN4; y 23:30 con DN4 = sin DN4)
  for c in "sin 13:00 cielo=1\nfiestas=0\nclima=0\nvidrio=0\nestrellas_anochecer=0\n" "con 13:00 cielo=1\nfiestas=0\nclima=0\nvidrio=0\n" "sin 23:30 cielo=1\nfiestas=0\nclima=0\nvidrio=0\nestrellas_anochecer=0\n" "con 23:30 cielo=1\nfiestas=0\nclima=0\nvidrio=0\n"; do
    set -- $c; k=$1; h=$2; shift 2; os="$*"
    corre fugaz-$k-${h/:/} "$prog" 120 "$os" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 $h" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CINTA="$W/fugaz-$k-${h/:/}.cinta" ROCOLA_CINTA_CUADROS=10-110
  done
  espera fugaz-sin-1300 fugaz-con-1300 fugaz-sin-2330 fugaz-con-2330
  for c in sin-1300 con-1300 sin-2330 con-2330; do PYTHONPATH="$PY" python3 "$G/fugaces.py" "$W/fugaz-$c.cinta" > "$W/fugaz-$c.txt" 2>&1; echo "   $c: $(cat "$W/fugaz-$c.txt")"; done
  si "de dia sin DN4 se ven fugaces al azar (alfa > 8 en sus vertices)" bash -c "[ \"\$(cut -d' ' -f1 '$W/fugaz-sin-1300.txt')\" -gt 0 ]"
  si "de dia con DN4 no se ve ninguna" bash -c "[ \"\$(cut -d' ' -f1 '$W/fugaz-con-1300.txt')\" = 0 ]"
  si "de noche con DN4 se ven como sin DN4 (la noche es la de XP)" bash -c "[ \"\$(cut -d' ' -f1 '$W/fugaz-con-2330.txt')\" -gt 0 ] && [ \"\$(cut -d' ' -f1 '$W/fugaz-sin-2330.txt')\" -gt 0 ]"
fi

# ---------------------------------------------------------------- reloj
if [[ " $partes " == *" reloj "* ]]; then
  echo "== reloj (NativeAOT: $AOTMIO)"
  for vuelta in 1 2; do
    for c in encendido apagado; do
      n=reloj-$c-$vuelta
      if [ $c = apagado ]; then os='cielo=0\nfiestas=0\nclima=0\nvidrio=0\n'; else os='cielo=1\nfiestas=0\nclima=0\nvidrio=0\nluna_camino=1\n'; fi
      # la pared, la ficha y la atraccion (el escaparate), a las 17:40 (el cielo mezclando dos paletas): 70 s, se mide de los 8 s en adelante
      corre $n "$AOTMIO" 70 "$os" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 17:40" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED \
        ROCOLA_PULSA="6:27+39,7.3:27+39,8.6:27+39,9.9:27+39,19.2:39" ROCOLA_CIELO_RELOJ="$W/$n/reloj.txt"
      espera $n
      echo "   $c (vuelta $vuelta): $(cat "$W/$n/reloj.txt" 2>/dev/null || echo 'sin medida')"
    done
  done
  si "el reloj se midio en las cuatro corridas" bash -c "for n in reloj-encendido-1 reloj-apagado-1 reloj-encendido-2 reloj-apagado-2; do grep -q '^cuadros=' '$W/'\$n'/reloj.txt' || exit 1; done"
fi

echo
echo "$( [ $mal = 0 ] && echo "TODO BIEN ($bien comprobaciones)" || echo "$mal MAL de $((bien + mal))" )"
[ $mal = 0 ]
