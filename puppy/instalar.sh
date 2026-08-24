#!/bin/sh
# ====================================================================================================
#  DEJA LA MAQUINA LISTA: SE ENCIENDE Y APARECE LA ROCOLA
# ----------------------------------------------------------------------------------------------------
#  Se corre UNA SOLA VEZ en la maquina de Puppy, despues de haber cargado el rocola.sfs.
#
#      sh instalar.sh              deja el arranque directo a la rocola
#      sh instalar.sh --quitar     lo deshace y devuelve el escritorio de siempre
#
#  No borra nada: guarda el arranque de Puppy tal como estaba y lo devuelve si se le pide.
# ====================================================================================================
set -e

AQUI="$(cd "$(dirname "$0")" && pwd)"
DATOS="${ROCOLA_DATOS:-/mnt/home/rocola}"
XINIT="/root/.xinitrc"
GUARDADO="/root/.xinitrc.puppy"

di() { echo "  $*"; }
muere() { echo; echo "  NO SE PUDO: $*"; echo; exit 1; }

# ---------------- deshacer ----------------
if [ "$1" = "--quitar" ]; then
  echo
  if [ -f "$GUARDADO" ]; then
    cp -a "$GUARDADO" "$XINIT"
    rm -f "$GUARDADO"
    di "listo: al encender vuelve a salir el escritorio normal de Puppy."
    di "la musica y todo lo que la rocola aprendio siguen en $DATOS (no se toca nada)."
  else
    di "no habia nada que deshacer."
  fi
  echo
  exit 0
fi

echo
echo "=== DEJANDO LA MAQUINA LISTA PARA LA ROCOLA ==="
echo

# ---------------- 1) que este la rocola ----------------
EXE=""
for c in /opt/rocola/Rocola.exe "$AQUI/../Rocola.exe" "$AQUI/Rocola.exe"; do
  [ -f "$c" ] && { EXE="$c"; break; }
done
[ -n "$EXE" ] || muere "no encuentro la rocola. Si va en un .sfs, cargalo primero (BootManager) y reinicia."
di "la rocola esta en $EXE"

command -v mono >/dev/null 2>&1 || muere "falta Mono."

# ---------------- 2) que no le falte nada al sistema ----------------
# Esto se comprueba ANTES de tocar el arranque. Si falta algo y se dejara el arranque directo puesto,
# la maquina encenderia en una pantalla negra sin escritorio al que volver: un ladrillo.
CHEQ=""
for c in "$AQUI/bibliotecas.exe" "$AQUI/../pruebas/bibliotecas.exe"; do [ -f "$c" ] && { CHEQ="$c"; break; }; done
if [ -n "$CHEQ" ]; then
  di "comprobando que no le falte nada al sistema..."
  if ! mono "$CHEQ" "$EXE" > /tmp/rocola_bibliotecas.txt 2>&1 || ! grep -q "TODO BIEN" /tmp/rocola_bibliotecas.txt; then
    sed 's/^/    /' /tmp/rocola_bibliotecas.txt
    muere "a esta maquina le falta algo de lo de arriba. Instalalo y vuelve a correr esto."
  fi
  di "  no falta nada."
else
  di "(no encontre bibliotecas.exe: me salto la comprobacion)"
fi

# ---------------- 3) la carpeta de datos ----------------
mkdir -p "$DATOS/musica"
touch "$DATOS/.escribible" 2>/dev/null || muere "no se puede escribir en $DATOS. Elige otra carpeta con ROCOLA_DATOS=..."
rm -f "$DATOS/.escribible"
di "los datos y la musica van en $DATOS"
di "  (la musica, en $DATOS/musica; para anadir OTRO disco de musica se usa Ctrl+F2 dentro de la rocola)"

# ---------------- 4) el arranque ----------------
[ -f "$XINIT" ] && [ ! -f "$GUARDADO" ] && cp -a "$XINIT" "$GUARDADO" && di "guardado el arranque de Puppy en $GUARDADO"
[ -f "$GUARDADO" ] || muere "no encuentro el arranque de Puppy ($XINIT). Esto no parece un Puppy."

SESION="$(dirname "$EXE")/arranca_rocola.sh"
[ -f "$SESION" ] || SESION="$AQUI/arranca_rocola.sh"
[ -f "$SESION" ] || muere "no encuentro arranca_rocola.sh"

cat > "$XINIT" <<FIN
#!/bin/sh
# Arranque directo a la rocola. Lo puso puppy/instalar.sh.
# Para volver al escritorio de siempre:  sh instalar.sh --quitar
ROCOLA_EXE="$EXE"
ROCOLA_DATOS="$DATOS"
export ROCOLA_EXE ROCOLA_DATOS
exec /bin/sh "$SESION"
FIN
chmod +x "$XINIT"
di "al encender arrancara la rocola, sin escritorio."

# ---------------- 5) un icono para volver ----------------
# Para cuando se sale de la rocola con Ctrl+Alt+Q y hay que volver a entrar sin reiniciar.
if [ -d /root/Desktop ]; then
  cat > /root/Desktop/rocola <<FIN
#!/bin/sh
ROCOLA_EXE="$EXE"
ROCOLA_DATOS="$DATOS"
export ROCOLA_EXE ROCOLA_DATOS
exec mono "\$ROCOLA_EXE"
FIN
  chmod +x /root/Desktop/rocola
  di "y hay un icono 'rocola' en el escritorio para volver a entrar sin reiniciar."
fi

echo
echo "  LISTO. Reinicia y la rocola tiene que aparecer sola."
echo
echo "  Para salir de la rocola:      Ctrl+Alt+Q  tres veces seguidas"
echo "  Para deshacer todo esto:      sh instalar.sh --quitar"
echo "  La musica va en:              $DATOS/musica"
echo
