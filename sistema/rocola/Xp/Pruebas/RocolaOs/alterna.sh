#!/bin/bash
# alterna.sh REF NUEVO ETQ_REF ETQ_NUEVO ESCENA...: cada escena de Pruebas/Ventanas/guiones/escenas.sh con la referencia y en seguida con
# el programa nuevo, para que las dos corran con la misma carga de la maquina (la de la ultima junta del port: alterna.sh del scratchpad de la
# sesion que hizo el port, PORT_JUNTAS.md). REF y NUEVO como escenas.sh: la carpeta con rocola-xp.dll (JIT) o aot:CARPETA (NativeAOT).
# Cada par (la referencia y el nuevo de una escena) toma el candado de lo pesado (PESADO) con «flock -o»: el candado lo tiene flock y no
# pasa a lo que se abre debajo (un servidor de compilacion o una rocola que quedara viva lo retendria); con ESPERAS=archivo, apunta alli los
# segundos que espero el candado cada par. Lo que dicen las corridas («salio con», «duro») va a la salida; las salidas de las escenas, a
# $SAL/ETIQUETA-ESCENA como siempre (W, SAL, DATOS0 y DH como escenas.sh y corre.sh).
#   ROCOLA_OS_NUEVO=lista  (el nivel 1 de PLAN_FASE11.md §2.6) el programa nuevo corre con ROCOLA_OS=lista, una carpeta /run de prueba
#                          vacia para cada escena (ROCOLA_RUN=$RUN_BASE/ETQ_NUEVO-ESCENA) y, si se puede, sin red (unshare -n: otro
#                          espacio de red, solo con la interfaz local apagada); SIN_RED=0 lo corre con la red de la maquina.
# La referencia corre siempre sin ROCOLA_OS ni ROCOLA_RUN; sin ROCOLA_OS_NUEVO, el nuevo tambien (el nivel 0).
set -u
AQUI=$(cd "$(dirname "$(readlink -f "$0")")" && pwd)
GV=${GUIONES:-$AQUI/../Ventanas/guiones}
PESADO=${PESADO:-/root/rocola-trabajo/fase11/pesado.lock}
if [ "${1:-}" = "--par" ]; then
  # (dentro del candado) una escena con la referencia y con el nuevo
  ref="$2"; nue="$3"; er="$4"; en="$5"; e="$6"
  env -u ROCOLA_OS -u ROCOLA_RUN bash "$GV/escenas.sh" "$ref" "$er" "$e"
  if [ -n "${ROCOLA_OS_NUEVO:-}" ]; then
    run="${RUN_BASE:?falta RUN_BASE}/$en-$e"; rm -rf "$run"; mkdir -p "$run"
    red=(); if [ "${SIN_RED:-1}" = 1 ] && unshare -n true 2> /dev/null; then red=(unshare -n); fi
    "${red[@]}" env ROCOLA_OS="$ROCOLA_OS_NUEVO" ROCOLA_RUN="$run" bash "$GV/escenas.sh" "$nue" "$en" "$e"
    echo "(nivel 1: ROCOLA_OS=$ROCOLA_OS_NUEVO, ROCOLA_RUN=$run${red:+, sin red}; en la carpeta /run quedo: $(ls -A "$run" | tr '\n' ' '))"
  else
    env -u ROCOLA_OS -u ROCOLA_RUN bash "$GV/escenas.sh" "$nue" "$en" "$e"
  fi
  exit 0
fi
[ $# -ge 5 ] || { echo "uso: alterna.sh REF NUEVO ETQ_REF ETQ_NUEVO ESCENA..."; exit 2; }
ref="$1"; nue="$2"; er="$3"; en="$4"; shift 4
mkdir -p "$(dirname "$PESADO")"
for e in "$@"; do
  echo "== $e ($(date +%T))"
  t0=$(date +%s)
  flock -o "$PESADO" bash -c '[ -n "${ESPERAS:-}" ] && echo "$(date +%s) - $1" | bc >> "$ESPERAS"; shift; exec "$@"' alterna "$t0" bash "$AQUI/alterna.sh" --par "$ref" "$nue" "$er" "$en" "$e"
done
echo FIN
