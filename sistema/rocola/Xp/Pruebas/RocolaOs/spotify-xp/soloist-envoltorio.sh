#!/bin/sh
# EL SOLOIST FALSO VISTO COMO UN PROGRAMA (fase 11, grupo spotify-xp): para la escena «vigilante» de prueba.sh, donde rocola-xp abre el
# vigilante DE VERDAD (rocola-soloist, del grupo soloist) y este abre lo que diga SOLOIST_BIN, que es esto. Contesta --version como Soloist
# (con una fecha de compilacion de ayer: le quedan 89 dias) y lo demas lo pasa al Soloist falso de C# (SOLOIST_FALSO_BIN), con el sonido por
# el libpulse.so.0 de la carpeta que le dio el vigilante (LD_LIBRARY_PATH: la de rocola-pulso) si SOLOIST_FALSO_PULSO lo pide. NUNCA es el
# Soloist de verdad.
case "$1" in
--version|-V)
	echo "Spotify Soloist 0.0.0-falso"
	echo "Build: $(date -d @$(( $(date +%s) - 86400 )) +%Y-%m-%d) 12:00:00 UTC (falso)"
	echo "Platform: linux x86_64"
	exit 0 ;;
esac
p=
if [ -n "$SOLOIST_FALSO_PULSO" ]; then p=${LD_LIBRARY_PATH%%:*}/libpulse.so.0; fi
exec dotnet "$SOLOIST_FALSO_BIN/soloist-falso.dll" "$@" ${p:+--por-pulso "$p"}
