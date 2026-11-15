#!/bin/bash
# (revision «invisible-y-reglas») EL VIGILANTE FALSO, TARDE: espera VIGILANTE_TARDE segundos (12 de fabrica) y sigue como el del nucleo
# (../spotify-xp/vigilante-falso.sh, con exec: su padre sigue siendo rocola-xp). Asi Soloist tiene sesion cuando la rocola ya arranco y
# lo de la cola ya se abrio por delante como MP3 (la escena «tarde» de prueba.sh).
sleep "${VIGILANTE_TARDE:-12}"
exec "$(cd "$(dirname "$0")" && pwd)/../spotify-xp/vigilante-falso.sh" "$@"
