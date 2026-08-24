#!/bin/sh
# ====================================================================================================
#  LA SESION DE LA ROCOLA
# ----------------------------------------------------------------------------------------------------
#  Esto es lo que corre cuando se enciende la maquina: no hay escritorio, no hay iconos, no hay barra
#  de tareas. Se enciende y esta la rocola.
#
#  Cuando alguien sale de la rocola (Ctrl+Alt+Q tres veces seguidas), este guion sigue y arranca el
#  escritorio normal de Puppy, para poder meter musica o hacer lo que haga falta. O sea que salir de
#  la rocola NO deja la pantalla en negro: deja Puppy.
# ====================================================================================================

# --- Donde esta cada cosa. Lo pone el instalador; se puede cambiar a mano sin tocar nada mas. ---
ROCOLA_EXE="${ROCOLA_EXE:-/opt/rocola/Rocola.exe}"
ROCOLA_DATOS="${ROCOLA_DATOS:-/mnt/home/rocola}"
export ROCOLA_DATOS

# LA CARPETA DE DATOS ES LO PRIMERO. Ahi van los creditos, la cola, los volumenes medidos, lo mas
# sonado y la musica. Tiene que estar en un disco de verdad y tiene que poder escribirse: si no, la
# rocola arrancaria igual pero un apagon se llevaria los creditos del dia.
mkdir -p "$ROCOLA_DATOS" 2>/dev/null
if ! touch "$ROCOLA_DATOS/.escribible" 2>/dev/null; then
  echo "ROCOLA: no se puede escribir en $ROCOLA_DATOS" >&2
  echo "ROCOLA: se arranca el escritorio normal para poder arreglarlo." >&2
  exec /root/.xinitrc.puppy
fi
rm -f "$ROCOLA_DATOS/.escribible"

# SIN PROTECTOR DE PANTALLA NI APAGADO DEL MONITOR. Una rocola esta encendida toda la noche y la
# pantalla es parte del mueble: que no se apague sola a los diez minutos.
xset s off 2>/dev/null
xset -dpms 2>/dev/null
xset s noblank 2>/dev/null

# SIN GESTOR DE VENTANAS. La rocola se pone ella sola del tamano de la pantalla, asi que no hace falta
# ninguno: es un programa menos corriendo y unas decimas menos de arranque.
#
# Si en alguna maquina la ventana de ajustes (Ctrl+F3) se portara raro -que no tome el teclado, por
# ejemplo-, se quita el "#" del renglon de abajo y ya esta: JWM es el gestor de ventanas que trae
# Puppy, ocupa poco y no ensena escritorio ni barra de tareas.
# jwm &

# CUANTO TARDO PUPPY EN LLEGAR HASTA AQUI. De "se enciende la maquina y suena la primera cancion",
# esta es la mitad que no depende de la rocola. /proc/uptime dice los segundos que lleva encendida la
# maquina, asi que apuntarlo justo antes de arrancar la rocola da el numero exacto, sin cronometro.
# La otra mitad -de aqui a que suene- la mide pruebas/arranque.exe.
if [ -r /proc/uptime ]; then
  echo "$(date '+%Y-%m-%d %H:%M:%S')  Puppy tardo $(cut -d' ' -f1 /proc/uptime) s en llegar a la rocola" \
    >> "$ROCOLA_DATOS/rocola_arranques.txt" 2>/dev/null
fi

# Y LA ROCOLA. Se queda aqui todo el rato que este encendida.
mono "$ROCOLA_EXE"

# Se salio de la rocola: el escritorio normal de Puppy, para el mantenimiento.
if [ -x /root/.xinitrc.puppy ] || [ -f /root/.xinitrc.puppy ]; then
  exec /bin/sh /root/.xinitrc.puppy
fi
exec jwm
