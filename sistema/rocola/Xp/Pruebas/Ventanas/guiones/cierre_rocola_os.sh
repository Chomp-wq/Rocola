#!/bin/bash
# cierre_rocola_os.sh PROGRAMA [ETIQUETA]: lo que Rocola OS le pide a rocola-xp y no es de XP (integra-version-y-cierre):
#   --version   UNA linea `rocola-xp programa N (COMMIT) compilado SEGUNDOS`, sale con 0, sin crear la carpeta de datos, sin traza
#               (no abre la pantalla) y con otra rocola abierta en los mismos datos (va antes de la instancia unica); y lo que leen
#               de ella /usr/bin/rocola, rocola-actualiza y rocola-buzon (sus tres expresiones)
#   sigusr1     una moneda y SIGUSR1 a los 10 s (los reinicios propios de Rocola OS): se cierra por la via de XP que conserva el estado
#               (closing y closed de la rocola con TaskManagerClosing), sale con 0 y rocola_estado.txt QUEDA con los creditos; al abrirla
#               otra vez con esos datos REANUDA (los mismos creditos) y entonces SIGTERM (el apagado) la cierra con 0 y lo BORRA
#   f3sigusr1   lo mismo con Ctrl+F3 abierto: antes su hoja con FormOwnerClosing, y la hoja acaba (dialogo-fin)
#   f2sigusr1   lo mismo con Ctrl+F2 abierto
#   videosigusr1  lo mismo con un video sonando (los pasos de la escena «video»; SIGUSR1 a los 15 s): suelta el reproductor y sale con 0
# PROGRAMA = carpeta con rocola-xp.dll (JIT) o aot:CARPETA con el ejecutable de NativeAOT, como corre.sh. Deja en $SAL/ETIQUETA-ESCENA
# la salida, la traza y los estados. Dice BIEN o MAL por comprobacion y sale con 1 si alguna falla.
#   W, DATOS0, SAL: como corre.sh. ESCENAS="version sigusr1 f3sigusr1 f2sigusr1 videosigusr1" (por defecto todas).
W=${W:-/root/rocola-trabajo/port/ventanas}
DATOS0=${DATOS0:-$W/datos0}
SAL=${SAL:-$W/salidas}
prog="$1"; et="${2:-cierre}"
if [ "${prog#aot:}" != "$prog" ]; then cmd=("${prog#aot:}/rocola-xp"); else cmd=(dotnet "$prog/rocola-xp.dll"); fi
malo=0
mira() { if [ "$2" = 0 ]; then echo "  BIEN  $1"; else echo "  MAL   $1"; malo=1; fi; }

# abre NOMBRE CARPETA_DE_DATOS "PULSA" SEGUNDOS SENAL "ESTADO": abre la rocola fuera de pantalla, a los SEGUNDOS le manda la SENAL y espera
# a que salga (30 s; despues la mata). Deja $o/salio (el codigo de salida de verdad, no el de timeout) y $o/tardo (segundos en cerrarse).
abre() {
  o=$SAL/$1; d="$2"; rm -rf "$o"; mkdir -p "$o"
  e=""; IFS=',' read -ra L <<< "$6"
  for x in "${L[@]}"; do [ -z "$x" ] && continue; e="$e,${x%%:*}:$o/estado-${x#*:}.txt"; done; e="${e#,}"
  ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_PULSA="$3" ROCOLA_CAPTURA_SALE=0 \
      ROCOLA_TRAZA_VENTANAS="$o/traza.txt" ROCOLA_ESTADO_EN="$e" "${cmd[@]}" > "$o/salida.txt" 2>&1 ) &
  p=$!
  sleep "$4"; kill -"$5" $p 2>/dev/null; t0=$(date +%s.%N)
  ( sleep 30; kill -KILL $p 2>/dev/null ) & v=$!
  wait $p; echo $? > "$o/salio"; kill $v 2>/dev/null; wait $v 2>/dev/null
  echo "$(date +%s.%N) - $t0" | bc > "$o/tardo"
  [ -f "$d/rocola_estado.txt" ] && cp "$d/rocola_estado.txt" "$o/rocola_estado.txt"
  [ -f "$d/rocola_error.txt" ] && cp "$d/rocola_error.txt" "$o/rocola_error.txt"
}
creditos() { sed -n 's/^creditos=//p' "$1" 2>/dev/null | tr -d '\r' | head -1; }

for esc in ${ESCENAS:-version sigusr1 f3sigusr1 f2sigusr1 videosigusr1}; do
  echo "== $esc"
  d=$W/corridas/$et-$esc; rm -rf "$d"
  case $esc in
    version)
      o=$SAL/$et-$esc; rm -rf "$o"; mkdir -p "$o"
      t0=$(date +%s.%N)
      env ROCOLA_DATOS="$d/no-hay" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_TRAZA_VENTANAS="$o/traza.txt" \
        timeout -s KILL 20 "${cmd[@]}" --version > "$o/salida.txt" 2> "$o/errores.txt"; r=$?
      echo "  $(cat "$o/salida.txt")   (salio con $r en $(echo "$(date +%s.%N) - $t0" | bc) s)"
      mira "sale con 0" $([ $r = 0 ]; echo $?)
      mira "una sola linea, y nada por la salida de errores" $([ "$(wc -l < "$o/salida.txt")" = 1 ] && [ ! -s "$o/errores.txt" ]; echo $?)
      mira "el formato: rocola-xp programa N (COMMIT) compilado SEGUNDOS" $(grep -qE '^rocola-xp programa [0-9]+ \([^ ()]+\) compilado [0-9]+$' "$o/salida.txt"; echo $?)
      n=$(sed -n 's/.*programa \([0-9]*\).*/\1/p' "$o/salida.txt"); s=$(sed -n 's/.* compilado \([0-9]*\).*/\1/p' "$o/salida.txt")
      mira "lo que leen rocola-actualiza y rocola-buzon: programa «$n»" $([ -n "$n" ]; echo $?)
      mira "lo que leen /usr/bin/rocola y rocola-actualiza: compilado «$s» (no 0, y no en el futuro)" $([ -n "$s" ] && [ "$s" -gt 1700000000 ] && [ "$s" -le "$(date +%s)" ]; echo $?)
      mira "la comprobacion de rocola-actualiza (grep -E \"programa N( |\$)\")" $(grep -qE "programa $n( |$)" "$o/salida.txt"; echo $?)
      mira "no crea la carpeta de datos ni abre la pantalla (sin traza)" $([ ! -e "$d/no-hay" ] && [ ! -e "$o/traza.txt" ]; echo $?)
      # con otra rocola abierta en los mismos datos: contesta igual (va antes de la instancia unica) y la abierta sigue
      mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/
      ( cd "$d" && exec env ROCOLA_DATOS="$d" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 ROCOLA_CAPTURA_SALE=0 "${cmd[@]}" > "$o/abierta.txt" 2>&1 ) & p=$!
      sleep 6
      env ROCOLA_DATOS="$d" ROCOLA_ALSA=null ROCOLA_PANTALLA=fuera:1024x768 timeout -s KILL 20 "${cmd[@]}" --version > "$o/salida2.txt" 2>&1; r=$?
      mira "con otra rocola abierta en esos datos: la misma linea y sale con 0" $([ $r = 0 ] && cmp -s "$o/salida.txt" "$o/salida2.txt"; echo $?)
      mira "y la abierta sigue" $(kill -0 $p 2>/dev/null; echo $?)
      kill -KILL $p 2>/dev/null; wait $p 2>/dev/null
      ;;
    sigusr1|f3sigusr1|f2sigusr1|videosigusr1)
      mkdir -p "$d"; cp -r "$DATOS0"/. "$d"/
      cuando=10; antes=9.5
      case $esc in sigusr1) pulsa="7:67";; f3sigusr1) pulsa="7:67,8:17+114";; f2sigusr1) pulsa="7:67,8:17+113";;
        videosigusr1) pulsa="7:27+39,8.5:27+39,10:13,11:67,12.5:13"; cuando=15; antes=14.5;; esac
      abre "$et-$esc" "$d" "$pulsa" $cuando USR1 "$antes:antes"
      o1=$o; t=$o1/traza.txt
      echo "  salio con $(cat $o1/salio) en $(cat $o1/tardo) s; creditos antes $(creditos $o1/estado-antes.txt), en rocola_estado.txt $(creditos $o1/rocola_estado.txt)"
      grep -E "	(closing|closed|dialogo-fin) |encargo CierreDeFuera" "$t" | sed 's/^[0-9]*	/    /'
      mira "sale con 0" $([ "$(cat $o1/salio)" = 0 ]; echo $?)
      mira "la rocola se cierra con TaskManagerClosing (closing y closed), y con ninguna otra razon" $([ "$(grep -cE "	(closing|closed) Form1'Rocola' TaskManagerClosing$" "$t")" = 2 ] && ! grep -E "	(closing|closed) Form1" "$t" | grep -qv TaskManagerClosing; echo $?)
      [ $esc = videosigusr1 ] && mira "rocola_estado.txt lleva lo que sonaba (sonando=V|...)" $(grep -q "^sonando=V|" $o1/rocola_estado.txt; echo $?)
      mira "rocola_estado.txt se CONSERVA, con los creditos de antes de cerrar" $([ -f "$o1/rocola_estado.txt" ] && [ -n "$(creditos $o1/estado-antes.txt)" ] && [ "$(creditos $o1/rocola_estado.txt)" = "$(creditos $o1/estado-antes.txt)" ]; echo $?)
      if [ $esc = videosigusr1 ]; then
        mira "el video sonaba al cerrar (suena=...Video...)" $(grep -q "^suena=.*[Vv]ideo" $o1/estado-antes.txt; echo $?)
      elif [ $esc != sigusr1 ]; then
        mira "antes, la hoja abierta recibe closing y closed con FormOwnerClosing, y su dialogo acaba" $([ "$(grep -cE "	(closing|closed) .* FormOwnerClosing$" "$t")" = 2 ] && grep -q "	dialogo-fin " "$t" \
          && [ "$(grep -nE "	closing .* FormOwnerClosing$" "$t" | cut -d: -f1)" -lt "$(grep -nE "	closing Form1'Rocola' TaskManagerClosing$" "$t" | cut -d: -f1)" ]; echo $?)
        mira "la hoja estaba abierta al cerrar" $(grep -qE "^activa=.+" $o1/estado-antes.txt && ! grep -q "^activa=Form1'Rocola'" $o1/estado-antes.txt; echo $?)
      fi
      # otra vez con esos datos: reanuda (los creditos vuelven) y el apagado (SIGTERM) borra el estado, como siempre
      abre "$et-$esc-otra" "$d" "" 10 TERM "9:reanuda"
      echo "  otra vez: creditos $(creditos $o/estado-reanuda.txt); con SIGTERM salio con $(cat $o/salio)"
      mira "al abrirla otra vez REANUDA: los mismos creditos" $([ -n "$(creditos $o/estado-reanuda.txt)" ] && [ "$(creditos $o/estado-reanuda.txt)" = "$(creditos $o1/estado-antes.txt)" ]; echo $?)
      mira "y el apagado (SIGTERM) sigue cerrando con WindowsShutDown, sale con 0 y BORRA rocola_estado.txt" $([ "$(cat $o/salio)" = 0 ] && [ ! -f "$d/rocola_estado.txt" ] && grep -q "	closed Form1'Rocola' WindowsShutDown$" "$o/traza.txt"; echo $?)
      ;;
    *) echo "escena desconocida: $esc"; malo=1 ;;
  esac
  rm -rf "$d"
done
[ $malo = 0 ] && echo "TODO BIEN" || echo "ALGO MAL"
exit $malo
