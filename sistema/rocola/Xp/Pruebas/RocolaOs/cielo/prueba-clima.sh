#!/bin/bash
# LA PRUEBA DEL GRUPO «NUBES Y CLIMA» DEL CIELO VIVO (DN6, CL1-CL5: RocolaOs/RocolaOs.CieloNubes*.cs y CieloLluvia*.cs; FASE11_cielo-clima.md;
# PLAN_CIELO_VIVO.md §7). La rocola entera fuera de pantalla (1024x768, llvmpipe), sin red de verdad (ROCOLA_CLIMA_API a un puerto donde no escucha
# nadie, o el Open-Meteo falso de clima_falso.py), con datos INVENTADOS que se arman aqui (120 canciones de ffmpeg en 20 artistas, 8 teclas, sin
# Spotify) si no estan. Zona de la hora: America/Mexico_City. Las letras son las del sistema (DejaVu): sirve para comparar el cielo, no para las
# medidas de pixel de los textos de XP. El clima: ROCOLA_CLIMA (un tipo y sus numeros), sin red, o un rocola_clima.txt (caducado).
#   bash prueba-clima.sh PROGRAMA [partes...]
#   PROGRAMA: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT)
#   BASE:     el programa de la rama principal (JIT), para «apagado» (de fabrica $T/base-jit)
#   W: la carpeta de trabajo (de fabrica $T/prueba-clima); CAPTURAS (de fabrica $W/capturas); T: donde quedan los datos inventados, brotli
# partes (de fabrica todas): ganchos orden apagado noche escenas relampagos reloj
#   ganchos     ganchos.py TODO BIEN y que esta rama no tenga ganchos propios (g320-g339: ninguno; todo va por los enchufes del nucleo)
#   orden       `rocola-xp --prueba-cielo-clima` (las cuentas puras)
#   apagado     APAGADO = XP: la cinta de la tarjeta de la pared sin ROCOLA_OS, con las funciones en 0 y con las ideas de este grupo en 0 (y un
#               clima de tormenta forzado): iguales a BASE; y el control (las nubes con tormenta a las 13:00): distinta
#   noche       DE NOCHE = XP: a las 23:30, con cielo y clima encendidos pero sin nubes (nubes=0) y sin clima, igual a BASE
#   escenas     capturas a las 13:00, 17:40 y 22:00 (el buzon /run/rocola-cielo-ver) de la pared y el escaparate con cada clima (despejado, nublado,
#               neblina, llovizna, lluvia, tormenta, nieve, sin internet, caducado y el Open-Meteo falso), el reposo con lluvia y tormenta, y la regla 3
#   relampagos  la tormenta 110 s: un relampago cada 6 s como mucho (ROCOLA_CIELO_RELAMPAGOS) y ninguno mientras alguien elige
#   reloj       p50/p95/p99/max del trabajo de cada cuadro (ROCOLA_CIELO_RELOJ) con todo encendido y todo apagado, en NativeAOT (AOTMIO)
set -u
G=$(cd "$(dirname "$0")" && pwd); XP=$(cd "$G/../../.." && pwd)
prog="${1:?uso: prueba-clima.sh PROGRAMA [partes]}"; shift
partes="${*:-ganchos orden apagado noche escenas relampagos reloj}"
# (rutas absolutas: las ordenes corren desde otras carpetas)
abs() { if [ "${1#aot:}" != "$1" ]; then echo "aot:$(cd "${1#aot:}" && pwd)"; else echo "$(cd "$1" && pwd)"; fi; }
prog=$(abs "$prog")
T=${T:-/tmp/rocola-cielo-clima}; mkdir -p "$T"
W=${W:-$T/prueba-clima}
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
# las portadas inventadas (cover.jpg: degradados de colores distintos, en la carpeta de cada ARTISTA: para la rocola el disco es la carpeta de
# primer nivel): sin ellas no hay escaparate con ficha (la atraccion lo pide), y la pastilla del clima (CL5) solo sale ahi
if [ ! -f "$MUSICA/.portadas2" ]; then
  python3 - "$MUSICA" <<'PY'
import os, sys, colorsys
from PIL import Image
raiz = sys.argv[1]; n = 0
for a in sorted(os.listdir(raiz)):
    p = os.path.join(raiz, a)
    if not os.path.isdir(p): continue
    h = (n * 0.137) % 1.0; n += 1
    c1 = tuple(int(255 * v) for v in colorsys.hsv_to_rgb(h, 0.7, 0.85)); c2 = tuple(int(255 * v) for v in colorsys.hsv_to_rgb((h + 0.15) % 1.0, 0.8, 0.35))
    im = Image.new('RGB', (300, 300))
    px = im.load()
    for y in range(300):
        for x in range(300):
            t = (x + y) / 600.0
            px[x, y] = tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3))
    im.save(os.path.join(p, 'cover.jpg'), quality=85)
PY
  touch "$MUSICA/.portadas2"
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
  [ -z "${CLIMA_ARCHIVO:-}" ] || printf '%b' "$CLIMA_ARCHIVO" > "$o/datos/rocola_clima.txt"
  ( cd "$o/datos" && exec env -u ROCOLA_OS -u ROCOLA_CIELO_FECHA -u ROCOLA_CLIMA -u ROCOLA_CIUDAD -u ROCOLA_CIELO_LUNA -u ROCOLA_CLIMA_API -u ROCOLA_CIELO_RELAMPAGOS ROCOLA_CLIMA_YA=1 ROCOLA_DATOS="$o/datos" ROCOLA_RUN="$o/run" \
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
  si "sin ganchos propios (g320 a g339): el grupo va por los enchufes del nucleo" test "$(grep -cP '^g3[23][0-9]\t' "$XP/RocolaOs/GANCHOS.tsv")" = 0
fi
# ---------------------------------------------------------------- orden
if [[ " $partes " == *" orden "* ]]; then
  echo "== orden"
  ( cd "$W" && env ROCOLA_OS=1 ROCOLA_DATOS="$W/orden-datos" ROCOLA_RUN="$W/orden-run" ROCOLA_PANTALLA=fuera:1024x768 $(rx "$prog") --prueba-cielo-clima ) > "$W/orden.txt" 2>&1; r=$?
  grep -E "^MAL" "$W/orden.txt" | sed 's/^/   /'
  si "las cuentas puras: $(tail -1 "$W/orden.txt")" test $r = 0
fi
# ---------------------------------------------------------------- apagado
FECHA13="2026-11-18 13:00"; FECHA23="2026-11-18 23:30"
cinta() {   # NOMBRE PROGRAMA OS [VAR=valor...]: la pared sin teclas, los cuadros de los 10 a los 11,5 s
  local n="$1" p="$2" os="$3"; shift 3
  corre "$n" "$p" 14 "$os" ROCOLA_CINTA="$W/$n.cinta" ROCOLA_CINTA_CUADROS=10-11.5 "$@"
}
TORMENTA="tormenta,100,9,35,240,15"
if [[ " $partes " == *" apagado "* ]]; then
  echo "== apagado"
  TODO0='cielo=0\nfiestas=0\nclima=0\nvidrio=0\n'
  IDEAS0='cielo=1\nclima=1\ncielo_sol=0\nsol=0\nluna_camino=0\nestrellas_anochecer=0\nhorizonte_hora=0\nnubes=0\nlluvia=0\ntormenta=0\nneblina=0\nclima_escaparate=0\n'
  SIN_NUBES='cielo=1\nclima=1\ncielo_sol=0\nsol=0\nluna_camino=0\nestrellas_anochecer=0\nhorizonte_hora=0\nlluvia=0\ntormenta=0\nneblina=0\nclima_escaparate=0\n'
  SIN_LLUVIA='cielo=1\nclima=1\ncielo_sol=0\nsol=0\nluna_camino=0\nestrellas_anochecer=0\nhorizonte_hora=0\nnubes=0\ntormenta=0\nneblina=0\nclima_escaparate=0\n'
  cinta base-sin "$BASE" -; espera base-sin
  cinta mio-sin "$prog" -; espera mio-sin
  cinta mio-cero "$prog" "$TODO0" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$FECHA13" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CLIMA="$TORMENTA"; espera mio-cero
  cinta mio-ideas0 "$prog" "$IDEAS0" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$FECHA13" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CLIMA="$TORMENTA"; espera mio-ideas0
  # (controles: las nubes con la tormenta a las 13:00 y la lluvia sola: la cinta SI cambia)
  cinta control-nubes "$prog" "$SIN_NUBES" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$FECHA13" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CLIMA="$TORMENTA"
  cinta control-lluvia "$prog" "$SIN_LLUVIA" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$FECHA13" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CLIMA="$TORMENTA"
  espera control-nubes control-lluvia
  for v in mio-sin mio-cero mio-ideas0; do
    PYTHONPATH="$PY" python3 "$G/cinta.py" compara "$W/base-sin.cinta" "$W/$v.cinta" > "$W/$v.compara" 2>&1; r=$?
    sed 's/^/   /' "$W/$v.compara"
    si "APAGADO = XP ($v): la cinta de la pared igual a la de la rama principal" test $r = 0
  done
  for v in control-nubes control-lluvia; do
    PYTHONPATH="$PY" python3 "$G/cinta.py" compara "$W/base-sin.cinta" "$W/$v.cinta" > "$W/$v.compara" 2>&1; r=$?
    sed 's/^/   /' "$W/$v.compara" | head -4
    si "el control ($v): la cinta de la pared SI cambia" test $r = 1
  done
fi
# ---------------------------------------------------------------- noche
if [[ " $partes " == *" noche "* ]]; then
  echo "== noche"
  [ -f "$W/base-sin.cinta" ] || { cinta base-sin "$BASE" -; espera base-sin; }
  # a las 23:30, con cielo y clima encendidos y sin clima: sin nubes (nubes=0) la noche es la de XP
  cinta noche-sin "$prog" 'cielo=1\nclima=1\nnubes=0\n' ROCOLA_OS=1 ROCOLA_CIELO_FECHA="$FECHA23" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED; espera noche-sin
  PYTHONPATH="$PY" python3 "$G/cinta.py" compara "$W/base-sin.cinta" "$W/noche-sin.cinta" > "$W/noche-sin.compara" 2>&1; r=$?
  sed 's/^/   /' "$W/noche-sin.compara" | head -6
  si "DE NOCHE = XP (23:30, cielo y clima encendidos, sin nubes, sin clima): la cinta igual a la de la rama principal" test $r = 0
fi

# ---------------------------------------------------------------- escenas
HORAS="13:00 17:40 22:00"
caps_de() { local n="$1" t0="$2" k=0 caps=""; for h in $HORAS; do caps="$caps,$(echo "$t0 + $k * 6 + 4" | bc):$CAPTURAS/$n-${h/:/}.png"; k=$((k + 1)); done; echo "${caps#,}"; }
buzon() { local n="$1" t0="$2"; ( sleep "$t0"; for h in $HORAS; do echo "2026-11-18 $h" > "$W/$n/run/rocola-cielo-ver"; sleep 6; done ) & }
declare -A CL
CL[despejado]="despejado,5,0,10,270,24"
CL[nublado]="nublado,90,0,14,270,19"
CL[neblina]="neblina,60,0,4,200,11"
CL[llovizna]="llovizna,90,0.4,12,250,15"
CL[lluvia]="lluvia,95,3,28,270,16"
CL[tormenta]="$TORMENTA"
CL[nieve]="nieve,90,1.5,12,300,-1"
TODO='cielo=1\nclima=1\n'
PIDE="6:45,6.8:45,8:27+39,9.3:27+39,10.6:27+39,11.9:27+39,21:39,21.5:39,22:39,22.5:39,27.5:13,29.5:13"
# escena CLIMA ESCENA [VAR=valor...]: CLIMA es uno de CL, «sinred», «caducado» o «falso»
escena() {
  local c="$1" e="$2"; shift 2
  local n="$c-$e" t0 seg pulsa="" extra=() clima_arch=""
  case $e in pared) t0=24; seg=70;; escaparate) t0=72; seg=120;; reposo) t0=84; seg=130; pulsa="$PIDE,31.5:27";; esac
  case $c in
    sinred) ;;
    caducado) clima_arch="# viejo\nmedida=$(date -u -d '5 hours ago' +%Y-%m-%dT%H:%M:%SZ)\ncodigo=65\nnubes=100\nlluvia=5\nviento=20\ndireccion=270\ntemperatura=14\nvisibilidad=9000\nde_dia=1\nlat=19.43\nlon=-99.13\n";;
    falso) extra=(ROCOLA_CLIMA_API="http://127.0.0.1:$PUERTO_FALSO");;
    *) extra=(ROCOLA_CLIMA="${CL[$c]}");;
  esac
  [ "$c" = falso ] || extra+=(ROCOLA_CLIMA_API=$SINRED)
  CLIMA_ARCHIVO="$clima_arch" corre "$n" "$prog" $seg "$TODO" ROCOLA_OS=1 ROCOLA_CIUDAD="$CIUDAD" "${extra[@]}" ROCOLA_PULSA="$pulsa" ROCOLA_CAPTURA="$(caps_de "$n" $t0)" ROCOLA_CAPTURA_SALE=1 "$@"
  buzon "$n" $t0
}
contraste() { python3 "$G/contraste.py" "$@"; }
if [[ " $partes " == *" escenas "* ]]; then
  echo "== escenas"
  PUERTO_FALSO=$((20000 + RANDOM % 20000))
  python3 "$G/clima_falso.py" $PUERTO_FALSO --registro "$W/falso.registro" --listo "$W/falso.listo" --codigo 63 --nubes 90 > /dev/null 2>&1 &
  FALSO=$!
  for i in 1 2 3 4 5 6 7 8 9 10; do [ -f "$W/falso.listo" ] && break; sleep 0.5; done
  for e in pared escaparate; do
    for grupo in "despejado nublado neblina" "llovizna lluvia tormenta" "nieve sinred caducado" "falso"; do
      for c in $grupo; do escena $c $e; done
      for c in $grupo; do espera $c-$e; done
    done
  done
  escena lluvia reposo; escena tormenta reposo; espera lluvia-reposo tormenta-reposo
  kill $FALSO 2>/dev/null
  for e in pared escaparate; do for c in despejado nublado neblina llovizna lluvia tormenta nieve sinred caducado falso; do
    nc=0; for h in $HORAS; do [ -f "$CAPTURAS/$c-$e-${h/:/}.png" ] && nc=$((nc + 1)); done
    si "$c $e: las 3 capturas ($nc) y «ver como se veria» 3 veces en la bitacora" test $nc = 3 -a "$(grep -c 'ver como se veria: 2026-11-18' "$(bit $c-$e)")" = 3
  done; done
  for c in lluvia tormenta; do nc=0; for h in $HORAS; do [ -f "$CAPTURAS/$c-reposo-${h/:/}.png" ] && nc=$((nc + 1)); done; si "$c reposo: las 3 capturas ($nc)" test $nc = 3; done
  # lo que se supo y lo que no
  si "sin internet: sin clima (la bitacora no habla de un clima de afuera) y las nubes de adorno" bash -c "! grep -q 'el clima de afuera' '$(bit sinred-pared)'"
  si "caducado: el clima de hace 5 h no se usa (la bitacora dice que ya es viejo)" contiene "$(bit caducado-pared)" "ya es viejo: no se usa"
  si "el Open-Meteo falso se pregunto (la ruta /v1/forecast llego al servidor)" contiene "$W/falso.registro" "/v1/forecast"
  # la regla 3: la pared (sin la barra de arriba ni las portadas) y el escaparate bajo y a la derecha del cubo, con cada clima y cada hora
  for c in despejado nublado neblina llovizna lluvia tormenta nieve sinred caducado falso; do for h in $HORAS; do
    f=$CAPTURAS/$c-escaparate-${h/:/}.png
    if [ -f "$f" ]; then
      contraste "$f" --solo 560,560,1000,650 > "$W/contraste-$c-escaparate-$h.txt" 2>&1
      si "regla 3 en $c escaparate a las $h: $(cat "$W/contraste-$c-escaparate-$h.txt")" contiene "$W/contraste-$c-escaparate-$h.txt" "regla 3 (<= 0,17): BIEN"
    fi
    f=$CAPTURAS/$c-pared-${h/:/}.png
    if [ -f "$f" ]; then
      contraste "$f" 0,0,1024,100 0,90,260,290 760,100,960,270 > "$W/contraste-$c-pared-$h.txt" 2>&1
      si "regla 3 en $c pared (sin la barra de arriba, la portada ni la luna) a las $h: $(cat "$W/contraste-$c-pared-$h.txt")" contiene "$W/contraste-$c-pared-$h.txt" "regla 3 (<= 0,17): BIEN"
    fi
  done; done
  for c in lluvia tormenta; do for h in $HORAS; do
    f=$CAPTURAS/$c-reposo-${h/:/}.png
    if [ -f "$f" ]; then
      contraste "$f" --hasta 0.668 --solo 0,100,1024,513 > "$W/contraste-$c-reposo-$h.txt" 2>&1
      si "regla 3 en $c reposo a las $h: $(cat "$W/contraste-$c-reposo-$h.txt")" contiene "$W/contraste-$c-reposo-$h.txt" "regla 3 (<= 0,17): BIEN"
    fi
  done; done
fi

# ---------------------------------------------------------------- relampagos
if [[ " $partes " == *" relampagos "* ]]; then
  echo "== relampagos"
  rm -f "$W/relampagos-solo.txt" "$W/relampagos-eligiendo.txt"
  # 112 s de tormenta a las 22:00: sin tocar nada (la calma vuelve a 1 a los 18 s), y otra vez con un boton cada 5 s (alguien elige: no empiezan)
  corre rel-solo "$prog" 112 "$TODO" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 22:00" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CLIMA="$TORMENTA" ROCOLA_CIELO_RELAMPAGOS="$W/relampagos-solo.txt"
  pulsa=""; for t in $(seq 6 5 106); do pulsa="$pulsa,$t:39"; done
  corre rel-elige "$prog" 112 "$TODO" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 22:00" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CLIMA="$TORMENTA" ROCOLA_CIELO_RELAMPAGOS="$W/relampagos-eligiendo.txt" ROCOLA_PULSA="${pulsa#,}"
  espera rel-solo rel-elige
  python3 "$G/relampagos.py" "$W/relampagos-solo.txt" > "$W/relampagos-solo.res" 2>&1; r=$?
  si "la tormenta da relampagos y NUNCA uno antes de 6 s del anterior: $(cat "$W/relampagos-solo.res")" test $r = 0
  n=0; [ -f "$W/relampagos-eligiendo.txt" ] && n=$(wc -l < "$W/relampagos-eligiendo.txt")
  si "mientras alguien elige (un boton cada 5 s) no empieza ninguno ($n)" test "$n" = 0
fi

# ---------------------------------------------------------------- reloj
if [[ " $partes " == *" reloj "* ]]; then
  echo "== reloj (NativeAOT: $AOTMIO)"
  for vuelta in 1 2; do
    for c in encendido apagado; do
      n=reloj-$c-$vuelta
      if [ $c = apagado ]; then os='cielo=0\nfiestas=0\nclima=0\nvidrio=0\n'; else os='cielo=1\nclima=1\n'; fi
      # la pared, la ficha y la atraccion (el escaparate), a las 17:40 con lluvia: 70 s, se mide de los 8 s en adelante
      corre $n "$AOTMIO" 70 "$os" ROCOLA_OS=1 ROCOLA_CIELO_FECHA="2026-11-18 17:40" ROCOLA_CIUDAD="$CIUDAD" ROCOLA_CLIMA_API=$SINRED ROCOLA_CLIMA="lluvia,95,3,28,270,16" \
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
