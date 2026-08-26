#!/bin/sh
# ============================================================================================
#  LAS PRUEBAS DE LA ROCOLA
# --------------------------------------------------------------------------------------------
#  Compila la rocola y corre TODAS las baterias contra el ejecutable recien hecho.
#
#    sh pruebas/todo.sh              corre la suite entera
#    sh pruebas/todo.sh disco ssd    corre solo esas
#    sh pruebas/todo.sh --lista      dice cuales hay
#
#  No hace falta preparar nada: detecta solo si esta rama es la de Windows XP (.NET 3.5) o la
#  de tarjeta grafica (.NET 4.8), compila los dos perfiles -con y sin GPU- y arranca.
#
#  Cada bateria es un programa suelto que CARGA el ejecutable de la rocola y le mira las tripas
#  por dentro: mide lo que cuesta algo de verdad, cuenta los pixeles de lo que se pinta, o lee
#  el codigo ya compilado para comprobar que una funcion sigue enchufada donde debe. Escribe
#  lineas "OK" y "FALLO" y devuelve el numero de fallos.
# ============================================================================================
cd "$(dirname "$0")" || exit 1
RAIZ="$(cd .. && pwd)"
NAUDIO="$RAIZ/packages/NAudio.1.9.0/lib/net35/NAudio.dll"
FUENTES="$RAIZ/Rocola/Form1.cs $RAIZ/Rocola/Form1.Designer.cs $RAIZ/Rocola/Program.cs $RAIZ/Rocola/D3D9Star.cs"

# Las que miran cosas que solo existen con tarjeta grafica (la aurora, el barrido de luz) van
# contra el perfil de GPU; las demas contra el normal.
CPU="ajustes bajo eqauto bajoauto calidad eqia nivel titulo duracion prueba prueba2 prueba3 prueba4 neon fps abc barrido2 espacio busca buscaigual bolsa genero pagina busmov parcial disco ssd etiqueta sala hojea cosecha resol letrero franjas dsee salir carpeta carpetas miniaturas pared sobras gasto historial estrellas limite vacias videos franja colores carriles indice proyecto primera escribe reposo cambio apagon unasola entrar iadatos entrenador corta deriva ligera simbolos leer"
# 'entrar' sale en las dos: casi todo lo que mira es comun, pero la parte de que la guia se vea CON
# tarjeta grafica solo existe en ese perfil (si no, aparecia en las pruebas y no en el bar).
GPU="fondo anim presencia barrido tope calma entrar mover"

# Y las que miran algo que SOLO EXISTE en la rama de tarjeta grafica. La limpieza de videos que no
# suenan es una de esas: para saber si un video suena hay que descomprimir su sonido, y eso alli lo
# hace Media Foundation, que llego con Windows Vista. En la rama de XP no hay con que, asi que ni
# existe la funcion ni se prueba.
SOLO_HD="mudos"

# --- Que version de .NET pide esta rama. Lo manda la RAMA, no el compilador: el codigo de la rama
#     de tarjeta grafica tambien compila con -sdk:2, asi que probar y ver si cuela elegiria mal y
#     estariamos midiendo un ejecutable que no es el que se instala. Si la rama no se reconoce
#     -por ejemplo en una copia suelta sin historial-, se prueba .NET 3.5 y se cae al 4.8. ---
RAMA="$(git -C "$RAIZ" rev-parse --abbrev-ref HEAD 2>/dev/null)"
case "$RAMA" in
  rocola-hd) SDK=4; ORDEN="4"; CPU="$CPU $SOLO_HD" ;;   # la del equipo con tarjeta grafica: .NET 4.8
  ?*)        SDK=2; ORDEN="2" ;;      # la de Windows XP: .NET 3.5
  *)         SDK=2; ORDEN="2 4" ;;    # sin rama conocida: se prueban los dos
esac

if [ "$1" = "--lista" ]; then
  echo "  sin tarjeta grafica: $CPU"
  echo "  con tarjeta grafica: $GPU"
  exit 0
fi
echo "  compilando la rocola..."
hecho=0
for k in $ORDEN; do
  if mcs -sdk:$k -langversion:5 -warn:4 -target:winexe -out:rocola_cpu.exe \
       -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$NAUDIO" $FUENTES > /tmp/c_rocola.txt 2>&1; then
    SDK=$k; hecho=1; break
  fi
done
if [ $hecho -eq 0 ]; then echo "  LA ROCOLA NO COMPILA:"; tail -12 /tmp/c_rocola.txt; exit 1; fi
if ! mcs -sdk:$SDK -langversion:5 -warn:4 -target:winexe -out:rocola_gpu.exe -d:ROCOLA_GPU \
     -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$NAUDIO" $FUENTES > /tmp/c_rocola_gpu.txt 2>&1; then
  echo "  EL PERFIL CON TARJETA GRAFICA NO COMPILA:"; tail -12 /tmp/c_rocola_gpu.txt; exit 1
fi
# --- LOS AVISOS SE CUENTAN, Y DE LOS DOS PERFILES ---
# Aqui la rocola se compila a CERO avisos. Antes esta linea decia "0 avisos" siempre, mirara o no: los del
# perfil con tarjeta grafica ni se leian, y por ahi se colo uno sin que nadie se enterara. Ahora se cuentan
# de verdad y, si hay alguno, se ensena cual y donde.
avisos=$(cat /tmp/c_rocola.txt /tmp/c_rocola_gpu.txt 2>/dev/null | grep -c 'warning CS')
if [ "${avisos:-0}" -ne 0 ]; then
  echo "  OJO: $avisos AVISO(S) al compilar la rocola (aqui se compila a cero):"
  grep -h 'warning CS' /tmp/c_rocola.txt /tmp/c_rocola_gpu.txt | sed 's/^/    /'
fi
echo "  rocola compilada (rama ${RAMA:-desconocida}, .NET $([ $SDK = 2 ] && echo 3.5 || echo 4.8), los dos perfiles, ${avisos:-0} avisos)"
echo

# --- Si se pidieron baterias sueltas, se reparten segun a que perfil van ---
if [ $# -gt 0 ]; then
  nc=""; ng=""
  for t in "$@"; do
    case " $GPU " in *" $t "*) ng="$ng $t";; *) nc="$nc $t";; esac
  done
  CPU="$nc"; GPU="$ng"
fi

# El lector del programa ya compilado vive aparte y se compila JUNTO a cada bateria: antes cada una tenia
# su propia copia del recorrido y solo cuatro de quince estaban completas. Un solo sitio que arreglar.
COMUN=""
[ -f leercodigo.cs ] && COMUN="leercodigo.cs"

mal=0
corre() {
  for t in $1; do
    [ -f "$t.cs" ] || { echo "  ---          $t   (no existe)"; continue; }
    if ! mcs -sdk:4 -warn:4 -out:$t.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$NAUDIO" $t.cs $COMUN > /tmp/c_$t.txt 2>&1; then
      echo "  COMPILA MAL  $t"; tail -3 /tmp/c_$t.txt; mal=$((mal+1)); continue
    fi
    xvfb-run -a mono $t.exe > /tmp/r_$t.txt 2>&1
    rc=$?
    n=$(grep -c '^  FALLO' /tmp/r_$t.txt)
    if [ "$rc" -ne 0 ] || [ "$n" -ne 0 ]; then
      echo "  FALLA        $t   (salida $rc, $n fallos)"
      grep '^  FALLO' /tmp/r_$t.txt | head -6
      tail -3 /tmp/r_$t.txt | sed 's/^/        /'
      mal=$((mal+1))
    else
      echo "  bien         $t"
    fi
  done
}

# Mono busca NAudio AL LADO del programa que lo usa, asi que se trae aqui desde packages/.
# (No se guarda en el repo: es el mismo archivo que ya vive en packages/.)
cp -f "$NAUDIO" ./NAudio.dll 2>/dev/null || true

# --- Material de apoyo: se genera solo si falta (nada de esto se guarda en el repo) ---
# mp3 de prueba (los usan 'disco' y 'etiqueta')
[ -f mp3s/esperado.txt ] || { echo "  generando mp3 de prueba..."; python3 hacemp3.py mp3s > /dev/null 2>&1; }
# wav de prueba (los usa 'duracion')
[ -d wav ] || { echo "  generando wav de prueba..."; mkdir -p wav; python3 genwav.py > /dev/null 2>&1; }
# ejecutables de REFERENCIA: el "antes" con el que se comparan las mejoras del buscador ('buscaigual',
# 'busmov'). Van fijados a un commit concreto; si no estan, se sacan del historial.
[ -f rocola_ref.exe ] && [ -f rocola_prev.exe ] || { echo "  compilando los ejecutables de referencia..."; sh refs.sh > /dev/null 2>&1; }
cp -f rocola_cpu.exe rocola.exe 2>/dev/null || true
corre "$CPU"
cp -f rocola_gpu.exe rocola.exe 2>/dev/null || true
corre "$GPU"
cp -f rocola_cpu.exe rocola.exe 2>/dev/null || true

echo
if [ $mal -eq 0 ]; then echo "TODAS LAS BATERIAS EN VERDE"; else echo "BATERIAS CON PROBLEMAS: $mal"; fi
exit $mal
