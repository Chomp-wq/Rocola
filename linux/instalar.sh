#!/bin/sh
# ====================================================================================================
#  LA ROCOLA EN LINUX, DE UNA VEZ
# ----------------------------------------------------------------------------------------------------
#  Un solo guion que deja la rocola andando en Puppy (BookwormPup64) y en Pop!_OS -y de paso en
#  cualquier Debian o Ubuntu, que es lo que Pop!_OS es por debajo-.
#
#      sh linux/instalar.sh            instala
#      sh linux/instalar.sh --quitar   lo deshace, SIN tocar la musica ni lo aprendido
#      sh linux/instalar.sh --probar   dice lo que HARIA, sin tocar ni un archivo
#
#  Al final PREGUNTA si ademas quiere el arranque directo (la maquina enciende y aparece la rocola,
#  sin escritorio). Para no tener que contestar:  --modo-bar  lo pone,  --normal  no lo pone.
#
#  Antes esto eran cinco pasos: instalar Mono, libgdiplus, mpv y ffmpeg a mano adivinando los nombres
#  de los paquetes, compilar, armar un .sfs, entrar al BootManager de Puppy a marcar un archivo,
#  reiniciar, y recien ahi correr otro guion.
#
#  EL .sfs SIGUE AHI (puppy/hacer_sfs.sh) y no lo sustituye esto: para el equipo del bar es mejor,
#  porque va de solo lectura y nadie lo puede romper sin querer. Esto es para poner la rocola en una
#  maquina sin pelearse, que es otra cosa.
#
#  --probar NO ES UN ADORNO. Es lo que permite mirar antes de decidir, y lo que permite que
#  pruebas/instala.cs meta este guion en una BookwormPup de mentira sin tener una delante.
# ====================================================================================================
set -e

AQUI="$(cd "$(dirname "$0")" && pwd)"
RAIZ="$(cd "$AQUI/.." && pwd)"

MODO="instalar"
BAR=""                                  # vacio = preguntarlo al final
for a in "$@"; do
  case "$a" in
    --quitar) MODO="quitar" ;;
    --probar) PROBAR=1 ;;
    --modo-bar) BAR=1 ;;
    --normal)   BAR=0 ;;
    -h|--ayuda|--help) sed -n '2,/^[^#]/p' "$0" | sed 's/^# \{0,1\}//' | sed '$d'; exit 0 ;;
    *) echo "  no entiendo '$a'. Prueba con --quitar, --probar, --modo-bar o --normal."; exit 1 ;;
  esac
done
PROBAR="${PROBAR:-0}"

# SI NO HAY UNA TERMINAL DELANTE, NO SE PREGUNTA. Corriendo desde otro guion -o desde la bateria de
# pruebas- una pregunta que nadie va a contestar deja el instalador colgado para siempre. Sin terminal
# se hace lo prudente: instalar lo que falte, y NO tocar el arranque de la maquina.
PREGUNTA=1
[ -t 0 ] || PREGUNTA=0

di()   { echo "  $*"; }
plan() { echo "  $1: $2"; }
muere() { echo; echo "  NO SE PUDO: $*"; echo; [ "$PROBAR" = "1" ] && plan veredicto "no se puede"; exit 1; }

# ====================================================================================================
#  QUIEN ES ESTA MAQUINA
# ----------------------------------------------------------------------------------------------------
#  Puppy y Pop!_OS se parecen por dentro -las dos son Debian- pero se organizan al reves, y de ahi
#  salen casi todas las decisiones de aqui abajo. Las variables ROCOLA_FINGE_* existen para poder
#  probar este guion contra un sistema que no esta delante (ver pruebas/instala.cs).
# ====================================================================================================
SISTEMA="${ROCOLA_FINGE_SISTEMA:-}"
if [ -z "$SISTEMA" ]; then
  if grep -qi puppy /etc/DISTRO_SPECS 2>/dev/null || [ -f /etc/puppyversion ]; then
    SISTEMA="puppy"
  elif command -v apt-get >/dev/null 2>&1; then
    SISTEMA="debian"
  else
    SISTEMA="otro"
  fi
fi

if [ -n "$ROCOLA_FINGE_ROOT" ]; then SOY_ROOT="$ROCOLA_FINGE_ROOT"; else
  [ "$(id -u)" = "0" ] && SOY_ROOT=1 || SOY_ROOT=0
fi
CASA="${ROCOLA_FINGE_HOME:-$HOME}"
[ -n "$CASA" ] || CASA="/root"

# ---- DONDE VA CADA COSA, que es lo que cambia entre sistemas ----
#
# EN PUPPY LOS DATOS VAN A UN DISCO DE VERDAD. En una instalacion frugal la carpeta del usuario vive
# DENTRO del archivo de guardado, y lo que se escribe ahi se queda en la memoria hasta que Puppy
# guarda: un apagon se llevaria los creditos y la cola de la noche. Con /mnt/home se escribe en el
# disco de verdad y al volver la luz la rocola sigue donde iba.
#
# Y EN POP!_OS AL REVES: /mnt/home no existe, asi que mandar ahi la musica seria mandarla a ninguna
# parte. Va a la carpeta del usuario, a la vista, que es donde uno espera dejar sus canciones.
if [ "$SISTEMA" = "puppy" ]; then
  DATOS="${ROCOLA_DATOS:-/mnt/home/rocola}"
else
  DATOS="${ROCOLA_DATOS:-$CASA/Rocola}"
fi

# El programa: en /opt si hay root (lo instalado para todos), y si no en la carpeta del usuario. Un
# usuario de Pop!_OS no tiene por que dar su contrasena solo para probar la rocola.
if [ "$SOY_ROOT" = "1" ]; then
  PROGRAMA="${ROCOLA_PREFIJO:-/opt/rocola}"
  MANDATO="/usr/local/bin/rocola"
  ICONOS="/usr/share/applications"
else
  PROGRAMA="${ROCOLA_PREFIJO:-$CASA/.local/opt/rocola}"
  MANDATO="$CASA/.local/bin/rocola"
  ICONOS="$CASA/.local/share/applications"
fi
MUSICA="$DATOS/musica"

# ====================================================================================================
#  DESHACER
# ----------------------------------------------------------------------------------------------------
#  Se borra el programa, el mandato y el icono. NO se toca la carpeta de datos: la musica y lo que la
#  rocola aprendio en anos -los volumenes medidos, el EQ, lo mas sonado- es lo unico de esa maquina
#  que no se recupera. El programa se vuelve a instalar en un minuto.
# ====================================================================================================
if [ "$MODO" = "quitar" ]; then
  echo
  echo "=== QUITANDO LA ROCOLA ==="
  echo
  if [ "$PROBAR" = "1" ]; then
    plan sistema "$SISTEMA"
    plan borra "$PROGRAMA $MANDATO $ICONOS/rocola.desktop"
    plan conserva "$DATOS (la musica y todo lo que la rocola aprendio)"
    plan veredicto "se puede"
    echo
    exit 0
  fi
  # El arranque directo, primero: si se quitara el programa dejandolo puesto, la maquina encenderia
  # en una pantalla negra sin escritorio al que volver.
  if [ -f /root/.xinitrc.puppy ]; then
    cp -a /root/.xinitrc.puppy /root/.xinitrc; rm -f /root/.xinitrc.puppy
    di "devuelto el escritorio de siempre"
  fi
  rm -f "$CASA/.config/autostart/rocola.desktop" 2>/dev/null || true
  rm -rf "$PROGRAMA" 2>/dev/null || true
  rm -f "$MANDATO" "$ICONOS/rocola.desktop" 2>/dev/null || true
  di "quitados el programa, el mandato y el icono."
  di "NO se ha tocado $DATOS: ahi siguen la musica y todo lo aprendido."
  echo
  exit 0
fi

# ====================================================================================================
#  QUE LE FALTA A LA MAQUINA
# ----------------------------------------------------------------------------------------------------
#  La lista no esta escrita a mano en ningun sitio: sale de las llamadas del propio programa, y quien
#  la saca es pruebas/bibliotecas.cs, que ya existia. Aqui solo se comprueba lo minimo para poder
#  arrancar (Mono y el compilador no se pueden comprobar con el programa, porque hacen falta ANTES).
# ====================================================================================================
echo
echo "=== LA ROCOLA EN LINUX ==="
echo
[ "$PROBAR" = "1" ] && di "(modo prueba: no se va a tocar nada)" && echo

falta_esto() {
  case " ${ROCOLA_FINGE_FALTA:-} " in *" $1 "*) return 0 ;; esac
  return 1
}
hay_mandato() { falta_esto "$1" && return 1; command -v "$1" >/dev/null 2>&1; }
hay_biblio()  {
  falta_esto "$1" && return 1
  [ -n "${ROCOLA_FINGE_FALTA:-}" ] && return 0   # sistema fingido: lo que no se dijo que falta, esta
  ldconfig -p 2>/dev/null | grep -q "$1" && return 0
  # Casi nunca se llaman como uno las busca: lo instalado es libgdiplus.so.0, no libgdiplus.so. Por eso
  # se mira tambien con lo que lleve pegado detras.
  for d in /usr/lib /usr/lib64 /usr/lib/x86_64-linux-gnu /lib /lib64; do
    for f in "$d/$1" "$d/$1".*; do [ -f "$f" ] && return 0; done
  done
  return 1
}

FALTAN=""; FALTAN_OPC=""; PAQUETES=""
# --- lo que hace falta si o si ---
hay_mandato mono || { FALTAN="$FALTAN mono"; PAQUETES="$PAQUETES mono-complete"; }
hay_mandato mcs  || { FALTAN="$FALTAN mcs";  PAQUETES="$PAQUETES mono-complete"; }
hay_biblio libgdiplus.so    || { FALTAN="$FALTAN libgdiplus";     PAQUETES="$PAQUETES libgdiplus"; }
hay_biblio libasound.so.2   || { FALTAN="$FALTAN libasound.so.2"; PAQUETES="$PAQUETES libasound2t64"; }
hay_biblio libX11.so.6      || { FALTAN="$FALTAN libX11.so.6";    PAQUETES="$PAQUETES libx11-6"; }
hay_biblio libXrandr.so.2   || { FALTAN="$FALTAN libXrandr.so.2"; PAQUETES="$PAQUETES libxrandr2"; }
hay_biblio libGL.so.1       || { FALTAN="$FALTAN libGL.so.1";     PAQUETES="$PAQUETES libgl1"; }
# --- y lo que NO hace falta: sin esto la rocola funciona igual, solo que sin seccion de videos ---
for v in mpv ffmpeg ffprobe; do
  hay_mandato "$v" || FALTAN_OPC="$FALTAN_OPC $v"
done
SIN_VIDEOS=no
[ -n "$FALTAN_OPC" ] && SIN_VIDEOS=si

HAY_APT=1
[ "${ROCOLA_FINGE_SIN_APT:-0}" = "1" ] && HAY_APT=0
[ "$SISTEMA" = "debian" ] || [ "$SISTEMA" = "puppy" ] || HAY_APT=0
command -v apt-get >/dev/null 2>&1 || [ -n "${ROCOLA_FINGE_SISTEMA:-}" ] || HAY_APT=0

# ---------------- el plan, para mirarlo antes ----------------
if [ "$PROBAR" = "1" ]; then
  plan sistema "$SISTEMA"
  plan root "$SOY_ROOT"
  plan programa "$PROGRAMA"
  plan datos "$DATOS"
  plan musica "$MUSICA"
  plan mandato "$MANDATO"
  plan icono "$ICONOS/rocola.desktop"
  plan falta "$(echo $FALTAN)"
  plan faltaopcional "$(echo $FALTAN_OPC)"
  plan sinvideos "$SIN_VIDEOS"
  plan arranquedirecto "$([ -z "$BAR" ] && echo pregunta || { [ "$BAR" = "1" ] && echo si || echo no; })"
  plan instalaria "$(echo $PAQUETES | tr ' ' '\n' | sort -u | tr '\n' ' ')"
  if [ -n "$FALTAN" ] && [ "$HAY_APT" = "0" ]; then
    plan veredicto "no se puede"
    echo
    di "Falta esto y aqui no hay con que instalarlo solo:$FALTAN"
    di "Instalalo a mano y vuelve a correr esto. En el gestor de paquetes se llaman:"
    di "   $(echo $PAQUETES | tr ' ' '\n' | sort -u | tr '\n' ' ')"
  else
    plan veredicto "se puede"
  fi
  echo
  exit 0
fi

# ====================================================================================================
#  INSTALAR LO QUE FALTE
# ====================================================================================================
if [ -n "$FALTAN" ]; then
  di "A esta maquina le falta:$FALTAN"
  LISTA="$(echo $PAQUETES | tr ' ' '\n' | sort -u | tr '\n' ' ')"
  if [ "$HAY_APT" = "0" ]; then
    echo
    di "Y aqui no hay con que instalarlo solo. En el gestor de paquetes busca:"
    di "   $LISTA"
    muere "instala eso y vuelve a correr esto."
  fi
  di "Se puede instalar solo. Son estos paquetes:"
  di "   $LISTA"
  echo
  if [ "$PREGUNTA" = "1" ]; then
    printf "  Lo instalo? [S/n] "
    read -r r || r="n"
    case "$r" in [Nn]*) muere "de acuerdo. Instala eso a mano y vuelve a correr esto." ;; esac
  else
    di "(no hay terminal para preguntar: se instalan)"
  fi
  echo
  SUDO=""
  [ "$SOY_ROOT" = "1" ] || SUDO="sudo"
  di "instalando (puede pedir tu contrasena)..."
  # libasound2t64 es el nombre nuevo (Ubuntu 24.04 en adelante); en los sistemas de antes es
  # libasound2. Se prueba el primero y, si no existe, el otro: mas vale eso que plantarse por un
  # cambio de nombre que no es culpa de nadie.
  $SUDO apt-get update -qq || true
  if ! $SUDO apt-get install -y $LISTA; then
    LISTA2="$(echo "$LISTA" | sed 's/libasound2t64/libasound2/')"
    di "reintentando con los nombres de antes..."
    $SUDO apt-get install -y $LISTA2 || muere "no se pudieron instalar. Mira lo de arriba."
  fi
  echo
fi

if [ -n "$FALTAN_OPC" ]; then
  di "OJO: falta$FALTAN_OPC -> la rocola va a quedar SIN SECCION DE VIDEOS."
  di "     (funciona igual; si los quieres:  apt install mpv ffmpeg  y vuelve a correr esto)"
  echo
fi

# ====================================================================================================
#  COMPILAR
# ----------------------------------------------------------------------------------------------------
#  Con las MISMAS fuentes y referencias que usa pruebas/todo.sh. No se copian a mano aqui: se sacan de
#  alli, para que no haya dos verdades sobre que entra en el programa.
# ====================================================================================================
di "compilando la rocola..."
NAUDIO="$RAIZ/packages/NAudio.1.9.0/lib/net35/NAudio.dll"
NLAYER="$RAIZ/packages/NLayer.1.12.0/lib/net35/NLayer.dll"
FUENTES="$(sed -n 's/^FUENTES="\(.*\)"$/\1/p' "$RAIZ/pruebas/todo.sh" | head -1)"
[ -n "$FUENTES" ] || muere "no pude leer la lista de fuentes de pruebas/todo.sh"
FUENTES="$(eval echo "$FUENTES")"
TMP="${TMPDIR:-/tmp}/rocola_instala.$$"
mkdir -p "$TMP"
if ! mcs -sdk:4 -langversion:5 -warn:4 -target:winexe -out:"$TMP/Rocola.exe" -d:ROCOLA_GPU \
     -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:"$NAUDIO" -r:"$NLAYER" $FUENTES > "$TMP/c.txt" 2>&1; then
  tail -15 "$TMP/c.txt" | sed 's/^/    /'
  rm -rf "$TMP"
  muere "la rocola no compila (arriba esta el motivo)."
fi
di "  compilada."

# ---- y que de verdad no le falte nada al sistema, ANTES de instalar ----
# Esto es lo que evita dejar una maquina que enciende en negro: la lista sale del propio programa
# recien compilado, no de lo que este guion crea recordar.
if [ -f "$RAIZ/pruebas/bibliotecas.cs" ]; then
  di "comprobando contra el programa recien hecho..."
  if mcs -sdk:4 -warn:0 -out:"$TMP/bibliotecas.exe" "$RAIZ/pruebas/bibliotecas.cs" >/dev/null 2>&1; then
    cp -f "$NAUDIO" "$NLAYER" "$TMP/" 2>/dev/null || true
    if ! (cd "$TMP" && mono bibliotecas.exe Rocola.exe > salida.txt 2>&1) || ! grep -q "TODO BIEN" "$TMP/salida.txt"; then
      sed 's/^/    /' "$TMP/salida.txt"
      rm -rf "$TMP"
      muere "a esta maquina le sigue faltando algo de lo de arriba."
    fi
    di "  no falta nada."
  fi
fi

# ====================================================================================================
#  DEJARLO PUESTO
# ====================================================================================================
echo
di "instalando en $PROGRAMA ..."
mkdir -p "$PROGRAMA" "$(dirname "$MANDATO")" "$ICONOS"
cp -f "$TMP/Rocola.exe" "$PROGRAMA/"
cp -f "$NAUDIO" "$NLAYER" "$PROGRAMA/" 2>/dev/null || true
for m in rocola_ia.bin rocola_eq_ia.bin; do
  [ -f "$RAIZ/$m" ] && cp -f "$RAIZ/$m" "$PROGRAMA/"
done
[ -f "$RAIZ/Rocola/rocola.ico" ] && cp -f "$RAIZ/Rocola/rocola.ico" "$PROGRAMA/"
rm -rf "$TMP"

# La carpeta de datos y la musica. Se crean ya, para que no haya que adivinar donde va nada.
mkdir -p "$MUSICA"
touch "$DATOS/.escribible" 2>/dev/null || muere "no se puede escribir en $DATOS. Prueba con ROCOLA_DATOS=otra/carpeta"
rm -f "$DATOS/.escribible"

# El mandato: escribir "rocola" en una terminal y que arranque.
cat > "$MANDATO" <<FINMANDATO
#!/bin/sh
# La rocola. Lo puso linux/instalar.sh; para quitarlo:  sh linux/instalar.sh --quitar
ROCOLA_DATOS="$DATOS"
export ROCOLA_DATOS
exec mono "$PROGRAMA/Rocola.exe" "\$@"
FINMANDATO
chmod +x "$MANDATO"

# Y el icono del menu.
cat > "$ICONOS/rocola.desktop" <<FINICONO
[Desktop Entry]
Type=Application
Name=Rocola
Comment=La rocola
Exec=$MANDATO
Icon=$PROGRAMA/rocola.ico
Terminal=false
Categories=AudioVideo;Player;
FINICONO
chmod +x "$ICONOS/rocola.desktop" 2>/dev/null || true

echo
di "LISTO."
di "  el programa:  $PROGRAMA"
di "  la musica:    $MUSICA      <- copia aqui tus canciones, un disco por carpeta"
di "  para abrirla: el icono 'Rocola' del menu, o escribir  rocola  en una terminal"
# Ubuntu y Pop!_OS meten ~/.local/bin en el PATH al ENTRAR a la sesion, y solo si la carpeta ya existia.
# Recien creada, la terminal de ahora todavia no la ve, y "no se encuentra rocola" parece que fallo la
# instalacion cuando lo unico que pasa es eso.
if [ "$SOY_ROOT" != "1" ] && ! command -v rocola >/dev/null 2>&1; then
  di "                (si dice que no encuentra 'rocola', cierra la sesion y vuelve a entrar, o usa"
  di "                 el icono: $(dirname "$MANDATO") todavia no esta en el PATH de esta terminal)"
fi
[ "$SIN_VIDEOS" = "si" ] && di "  (sin seccion de videos: faltaban mpv o ffmpeg)"
echo

# ====================================================================================================
#  Y SI ADEMAS QUIERE SER UNA ROCOLA DE BAR
# ----------------------------------------------------------------------------------------------------
#  Esto convierte la maquina: al encender aparece la rocola y no el escritorio. Se PREGUNTA, porque no
#  es lo que quiere quien solo esta probandola en su ordenador.
# ====================================================================================================
if [ -z "$BAR" ]; then
  if [ "$PREGUNTA" = "1" ]; then
    printf "  Quieres ademas que al ENCENDER la maquina arranque directa en la rocola? [s/N] "
    read -r r || r="n"
    case "$r" in [SsYy]*) BAR=1 ;; *) BAR=0 ;; esac
  else
    BAR=0
    di "(no hay terminal para preguntar: se queda como un programa normal;"
    di " si la quieres de bar:  sh linux/instalar.sh --modo-bar)"
  fi
fi
if [ "$BAR" != "1" ]; then di "se queda como un programa normal."; echo; exit 0; fi
echo

if [ "$SISTEMA" = "puppy" ]; then
  # En Puppy se cambia el arranque de X, que es lo que ya hace puppy/instalar.sh y esta probado en la
  # maquina del bar. Se guarda el de Puppy tal cual para poder devolverlo.
  XINIT="/root/.xinitrc"
  [ -f "$XINIT" ] || muere "no encuentro $XINIT. Esto no parece un Puppy."
  [ -f /root/.xinitrc.puppy ] || cp -a "$XINIT" /root/.xinitrc.puppy
  SESION="$RAIZ/puppy/arranca_rocola.sh"
  [ -f "$SESION" ] || muere "no encuentro puppy/arranca_rocola.sh"
  cp -f "$SESION" "$PROGRAMA/arranca_rocola.sh"; chmod +x "$PROGRAMA/arranca_rocola.sh"
  cat > "$XINIT" <<FINX
#!/bin/sh
# Arranque directo a la rocola. Lo puso linux/instalar.sh.
# Para volver al escritorio de siempre:  sh linux/instalar.sh --quitar
ROCOLA_EXE="$PROGRAMA/Rocola.exe"
ROCOLA_DATOS="$DATOS"
export ROCOLA_EXE ROCOLA_DATOS
exec /bin/sh "$PROGRAMA/arranca_rocola.sh"
FINX
  chmod +x "$XINIT"
  di "hecho: al encender arrancara la rocola, sin escritorio."
else
  # En Pop!_OS y compania se usa el arranque de sesion del escritorio. No es un kiosco de verdad -el
  # escritorio sigue debajo- pero es lo que se puede hacer sin tocar el gestor de sesiones, se deshace
  # borrando un archivo, y no deja a nadie con una maquina que no arranca.
  mkdir -p "$CASA/.config/autostart"
  cat > "$CASA/.config/autostart/rocola.desktop" <<FINAUTO
[Desktop Entry]
Type=Application
Name=Rocola
Exec=$MANDATO
Terminal=false
X-GNOME-Autostart-enabled=true
FINAUTO
  di "hecho: al entrar en el escritorio se abrira la rocola sola."
  di "(el escritorio sigue debajo: para un arranque sin escritorio hace falta Puppy)"
fi
echo
di "Para salir de la rocola:  Ctrl+Alt+Q tres veces seguidas"
di "Para deshacer todo esto:  sh linux/instalar.sh --quitar"
echo
