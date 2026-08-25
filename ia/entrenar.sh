#!/bin/sh
# ====================================================================================================
#  ENTRENAR LA IA DE LA ROCOLA  (en Linux)
# ----------------------------------------------------------------------------------------------------
#  La rocola tiene dos IAs y las dos aprenden DE TU BIBLIOTECA:
#
#     el RECONSTRUCTOR DE AGUDOS (el DSEE): aprende que forma tienen los agudos que le faltan a un MP3
#     el EQ AUTOMATICO: aprende como suena tu musica para saber que corregirle a cada cancion
#
#  La rocola junta el material sola mientras repasa la biblioteca. Este guion es el segundo paso:
#  coger ese material y sacar los modelos. El tercero es dejarlos en la carpeta de datos, y ya.
#
#      sh entrenar.sh                    busca el material en la carpeta de datos de la rocola
#      sh entrenar.sh /ruta/a/los/datos  o se le dice donde esta
#
#  Es el equivalente de entrenar_dsee.bat, que solo sirve en Windows. Hace lo mismo y llama a los
#  mismos programas de Python, que son los que hacen el trabajo de verdad.
#
#  Todo queda apuntado en entrenar_log.txt, al lado de este archivo: si algo sale mal, ahi esta
#  escrito hasta donde llego.
# ====================================================================================================
set -e
AQUI="$(cd "$(dirname "$0")" && pwd)"
LOG="$AQUI/entrenar_log.txt"

di()   { echo "  $*"; echo "  $*" >> "$LOG"; }
raya() { echo "  ------------------------------------------------------------------------"; }
muere() { echo; echo "  NO SE PUDO: $*"; echo "NO SE PUDO: $*" >> "$LOG"; echo; exit 1; }

echo "=== ENTRENAR LA IA DE LA ROCOLA ===  $(date '+%Y-%m-%d %H:%M:%S')" > "$LOG"
echo
echo "=========================================================================="
echo "   ENTRENAR LA IA DE LA ROCOLA"
echo "=========================================================================="
echo

# ---------- 1) Python y numpy ----------
PY=""
for c in python3 python; do command -v "$c" >/dev/null 2>&1 && { PY="$c"; break; }; done
[ -n "$PY" ] || muere "falta Python. En Puppy se instala desde el gestor de paquetes."
"$PY" -c "import numpy" 2>/dev/null || muere "falta numpy, que es lo unico que hace falta ademas de Python.
              Instalalo con:   $PY -m pip install numpy"
di "Python:  $($PY --version 2>&1)   con numpy"

# ---------- 2) donde esta el material ----------
# La rocola lo deja en su carpeta de datos, que es donde tambien estan los creditos y la biblioteca.
DATOS="$1"
if [ -z "$DATOS" ]; then
  for c in "$ROCOLA_DATOS" /mnt/home/rocola "$AQUI/.." "$AQUI"; do
    [ -n "$c" ] && [ -d "$c" ] && { [ -f "$c/rocola_eq.txt" ] || [ -f "$c/rocola_ia_datos.bin" ]; } && { DATOS="$c"; break; }
  done
fi
[ -n "$DATOS" ] || muere "no encuentro el material. Dime donde esta:   sh entrenar.sh /ruta/a/los/datos
              (es la carpeta donde la rocola guarda rocola_eq.txt y rocola_ia_datos.bin)"
DATOS="$(cd "$DATOS" && pwd)"
di "material: $DATOS"
echo

EQ="$DATOS/rocola_eq.txt"
DSEE="$DATOS/rocola_ia_datos.bin"
hechos=0

# ---------- 3) el EQ automatico ----------
raya
if [ -f "$EQ" ]; then
  n=$(wc -l < "$EQ" 2>/dev/null || echo 0)
  di "EQ AUTOMATICO: $n canciones medidas en rocola_eq.txt"
  if "$PY" "$AQUI/entrenar_eq.py" --datos "$EQ" --salida "$DATOS/rocola_eq_ia.bin" 2>&1 | tee -a "$LOG"; then
    [ -f "$DATOS/rocola_eq_ia.bin" ] && { di "  -> listo: $DATOS/rocola_eq_ia.bin"; hechos=$((hechos+1)); }
  fi
else
  di "EQ AUTOMATICO: no hay rocola_eq.txt todavia."
  di "  La rocola lo va llenando sola mientras repasa la biblioteca. Dale tiempo."
fi
echo

# ---------- 4) el reconstructor de agudos ----------
raya
if [ -f "$DSEE" ]; then
  di "RECONSTRUCTOR DE AGUDOS: $(du -h "$DSEE" | cut -f1) de material en rocola_ia_datos.bin"
  if "$PY" "$AQUI/entrenar_ia.py" --datos "$DSEE" --capas 28,96,96,12 2>&1 | tee -a "$LOG"; then
    for cual in "$AQUI/rocola_ia.bin" "./rocola_ia.bin"; do
      [ -f "$cual" ] && { cp -f "$cual" "$DATOS/rocola_ia.bin"; di "  -> listo: $DATOS/rocola_ia.bin"; hechos=$((hechos+1)); break; }
    done
  fi
else
  di "RECONSTRUCTOR DE AGUDOS: no hay rocola_ia_datos.bin todavia."
  di "  Se junta solo mientras la rocola repasa la biblioteca (se ve el avance en Ctrl+F3)."
fi

# ---------- 5) que hacer ahora ----------
echo
raya
if [ $hechos -eq 0 ]; then
  di "No se entreno nada: todavia no hay material suficiente."
  di "Deja la rocola encendida un rato mas y vuelve a intentarlo."
else
  di "LISTO: $hechos modelo(s) entrenado(s), ya puestos en $DATOS"
  di "La rocola los coge al arrancar. Cierrala con Ctrl+Alt+Q (tres veces) y vuelve a entrar."
fi
echo
