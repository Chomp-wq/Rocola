#!/bin/bash
# UN VIGILANTE FALSO para las pruebas del nucleo de Spotify (fase 11, grupo spotify-xp). Hace lo que I4 pide del de verdad
# (rocola-soloist corre, de otro grupo) solo lo justo para probar rocola-xp: abre el Soloist FALSO (SOLOIST_FALSO_BIN, la carpeta con
# soloist-falso.dll) con -n "$SPOTIFY_NOMBRE", una llave inventada, -D "$ROCOLA_SOLOIST_DATOS" y -w 127.0.0.1:0; si sale, lo vuelve a abrir
# a los 2 s (con SOLOIST_FALSO_PULSO=RUTA, el sonido por el libpulse.so.0 de RUTA, como Soloist); con SIGTERM le pasa SIGTERM y sale; si muere su padre (rocola-xp), sale. Apunta en VIGILANTE_FALSO_LOG con que lo abrieron.
[ "$1" = corre ] || { echo "uso: vigilante-falso.sh corre" >&2; exit 2; }
padre=$PPID; hijo=
log=${VIGILANTE_FALSO_LOG:-/dev/null}
echo "$(date +%s.%N) arranca padre=$padre SPOTIFY_NOMBRE=$SPOTIFY_NOMBRE ROCOLA_SPOTIFY_ANILLO=$ROCOLA_SPOTIFY_ANILLO" >> "$log"
fin() { echo "$(date +%s.%N) SIGTERM" >> "$log"; [ -n "$hijo" ] && kill -TERM "$hijo" 2>/dev/null && wait "$hijo"; exit 0; }
trap fin TERM INT
while kill -0 "$padre" 2>/dev/null; do
  dotnet "$SOLOIST_FALSO_BIN/soloist-falso.dll" -n "$SPOTIFY_NOMBRE" -k llave-inventada-de-prueba -D "$ROCOLA_SOLOIST_DATOS" \
    -C "${ROCOLA_RUN:-/tmp}/soloist-cache" -z 100 -w 127.0.0.1:0 -i 100 ${SOLOIST_FALSO_PULSO:+--por-pulso "$SOLOIST_FALSO_PULSO"} &
  hijo=$!
  while kill -0 "$hijo" 2>/dev/null; do
    kill -0 "$padre" 2>/dev/null || { kill -TERM "$hijo"; wait "$hijo"; echo "$(date +%s.%N) murio el padre" >> "$log"; exit 0; }
    sleep 0.5 & wait $!
  done
  wait "$hijo"; c=$?
  echo "$(date +%s.%N) soloist salio con $c" >> "$log"
  # (revision) con VIGILANTE_FALSO_UNA=1 no lo vuelve a abrir (como sin llave o caducado): espera a SIGTERM o a que muera el padre
  if [ "${VIGILANTE_FALSO_UNA:-0}" = 1 ]; then hijo=; while kill -0 "$padre" 2>/dev/null; do sleep 0.5 & wait $!; done; echo "$(date +%s.%N) murio el padre" >> "$log"; exit 0; fi
  # (el guion de eventos es de la primera vida: la segunda arranca limpia)
  unset SOLOIST_FALSO_GUION
  sleep "${VIGILANTE_FALSO_ESPERA:-2}" & wait $!   # (24-11-2026: VIGILANTE_FALSO_ESPERA=SEG, cuanto tarda en volver a abrirlo; 2 de fabrica)
done
