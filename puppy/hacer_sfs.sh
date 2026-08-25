#!/bin/sh
# ====================================================================================================
#  ARMA EL rocola.sfs
# ----------------------------------------------------------------------------------------------------
#  Un ".sfs" es un archivo unico que Puppy abre al arrancar y deja disponible como si fuera una parte
#  mas del sistema, SIN COPIAR NADA y sin poder tocarlo. Aqui dentro va todo lo que la rocola necesita
#  para funcionar y que Puppy no trae de fabrica:
#
#      Mono          el motor que hace correr la rocola
#      libgdiplus    quien dibuja las letras y las fotos (y lo que ella necesita)
#      mpv           quien reproduce los videos, dentro de la ventana de la rocola
#      ffmpeg        quien saca el sonido de un video para poder nivelarlo
#      la rocola     el programa, sus dos bibliotecas y el modelo de IA
#
#  Y de paso deja el programa YA TRADUCIDO a lenguaje de maquina, para que arranque antes (mas abajo).
#
#  Lo que NO va dentro: la musica y todo lo que la rocola guarda. Eso va en una carpeta del disco,
#  porque esto es de solo lectura y porque un apagon tiene que encontrar los creditos en el disco.
#
#  SE CORRE EN LA MAQUINA DE PUPPY, no en otra. El motivo no es capricho: los programas de Linux se
#  compilan contra la version de las bibliotecas del sistema donde se hicieron, y uno traido de otra
#  distribucion no arranca. Copiando el Mono de la propia maquina, encaja seguro.
#
#      sh hacer_sfs.sh [carpeta-de-la-rocola] [archivo-de-salida]
# ====================================================================================================
set -e

AQUI="$(cd "$(dirname "$0")" && pwd)"
ROCOLA_DIR="${1:-$AQUI/..}"
SALIDA="${2:-$AQUI/rocola.sfs}"
TMP="${TMPDIR:-/tmp}/rocola_sfs.$$"

di() { echo "  $*"; }
muere() { echo; echo "  NO SE PUDO: $*"; echo; exit 1; }

echo
echo "=== ARMANDO EL rocola.sfs ==="
echo

# ---------- 1) que este todo lo que hay que meter dentro ----------
command -v mksquashfs >/dev/null 2>&1 || muere "falta mksquashfs (en Puppy: instala squashfs-tools)"
command -v mono       >/dev/null 2>&1 || muere "falta Mono. Instalalo en Puppy y vuelve a correr esto."

# mpv y ffmpeg NO son obligatorios: sin ellos la rocola funciona igual, solo que sin seccion de videos.
# Se avisa para que sea una decision y no una sorpresa al llegar al bar.
HAY_VIDEO=1
for v in mpv ffmpeg ffprobe; do
  command -v "$v" >/dev/null 2>&1 || { HAY_VIDEO=0; di "OJO: falta $v"; }
done
[ $HAY_VIDEO -eq 1 ] || di "     -> el .sfs saldra SIN VIDEOS. Instala mpv y ffmpeg si los quieres."

MONO_BIN="$(command -v mono)"
MONO_PREFIJO="$(cd "$(dirname "$MONO_BIN")/.." && pwd)"
[ -d "$MONO_PREFIJO/lib/mono" ] || muere "no encuentro las piezas de Mono en $MONO_PREFIJO/lib/mono"
di "Mono:       $MONO_BIN   (sus piezas en $MONO_PREFIJO/lib/mono)"

GDI="$(ldconfig -p 2>/dev/null | sed -n 's/.* => \(.*libgdiplus\.so[^ ]*\)$/\1/p' | head -1)"
[ -n "$GDI" ] && [ -f "$GDI" ] || muere "falta libgdiplus (quien dibuja las letras). Instalalo en Puppy."
di "libgdiplus: $GDI"

EXE=""
for c in "$ROCOLA_DIR/Rocola.exe" "$ROCOLA_DIR/rocola.exe" "$ROCOLA_DIR/pruebas/rocola_gpu.exe"; do
  [ -f "$c" ] && { EXE="$c"; break; }
done
[ -n "$EXE" ] || muere "no encuentro la rocola compilada. Compilala primero (mira el LEEME)."
di "la rocola:  $EXE"
echo

# ---------- 2) comprobar ANTES de empaquetar ----------
# Si a esta maquina le falta una biblioteca, meterla en un .sfs no lo arregla: mejor saberlo ahora.
if [ -f "$AQUI/../pruebas/bibliotecas.exe" ]; then
  di "comprobando que esta todo lo que la rocola le pide al sistema..."
  if ! mono "$AQUI/../pruebas/bibliotecas.exe" "$EXE" | grep -q "TODO BIEN"; then
    mono "$AQUI/../pruebas/bibliotecas.exe" "$EXE" | sed 's/^/    /'
    muere "a esta maquina le falta algo. Instalalo y vuelve a correr esto."
  fi
  di "  esta todo."
  echo
fi

# ---------- 3) el arbol ----------
rm -rf "$TMP"; mkdir -p "$TMP/opt/rocola" "$TMP/usr/lib" "$TMP/usr/bin"

di "copiando Mono..."
mkdir -p "$TMP/usr/lib/mono" "$TMP/usr/etc"
cp -a "$MONO_PREFIJO/lib/mono/." "$TMP/usr/lib/mono/"
cp -a "$MONO_BIN" "$TMP/usr/bin/mono"
for extra in mono-sgen mono-boehm; do
  [ -f "$MONO_PREFIJO/bin/$extra" ] && cp -a "$MONO_PREFIJO/bin/$extra" "$TMP/usr/bin/"
done
# La tabla de nombres de Mono: sin ella, la rocola pide "libc" y no encuentra nada (ver bibliotecas.cs).
for cfg in /etc/mono "$MONO_PREFIJO/etc/mono"; do
  [ -d "$cfg" ] && { mkdir -p "$TMP/etc"; cp -a "$cfg" "$TMP/etc/"; break; }
done
# Y las piezas del propio Mono (el motor, no las de .NET), que van sueltas en /usr/lib.
#
# OJO CON LAS MAYUSCULAS: unas se llaman "libmono..." y otras "libMono...", y copiando solo las de
# minusculas se quedaba fuera libMonoPosixHelper.so. La rocola arrancaba, pintaba, y se moria al
# primer evento del teclado con un error que no dice nada ("no encuentro libMonoPosixHelper"). Se
# copian las dos formas, y se comprueba que la que hace falta esta.
for so in "$MONO_PREFIJO"/lib/libmono*.so* "$MONO_PREFIJO"/lib/libMono*.so*; do
  [ -f "$so" ] && cp -a "$so" "$TMP/usr/lib/"
done
[ -f "$TMP/usr/lib/libMonoPosixHelper.so" ] || muere "no encuentro libMonoPosixHelper.so junto a Mono (sin ella la rocola se muere al primer boton)"

di "copiando libgdiplus y lo que ella necesita..."
cp -a "$GDI" "$TMP/usr/lib/"
# Se copian SOLO las que no vienen ya en Puppy seria imposible de adivinar desde aqui, asi que se
# copian todas las que libgdiplus arrastra: sobrar no rompe nada (el sistema usa la suya si es mas
# nueva) y faltar si. Las del nucleo del sistema (libc, libm, el cargador) NO se tocan: cambiarlas
# por otras seria justo lo que hace que un programa traido de otra distribucion no arranque.
ldd "$GDI" 2>/dev/null | sed -n 's/.* => \(\/[^ ]*\) .*/\1/p' | while read -r lib; do
  case "$(basename "$lib")" in
    libc.so.*|libm.so.*|libpthread.so.*|libdl.so.*|librt.so.*|ld-linux*|libgcc_s.so.*) continue ;;
  esac
  [ -f "$lib" ] && cp -a "$lib" "$TMP/usr/lib/" 2>/dev/null || true
done

if [ $HAY_VIDEO -eq 1 ]; then
  di "copiando mpv y ffmpeg (los videos)..."
  for v in mpv ffmpeg ffprobe; do
    ruta="$(command -v "$v")"
    cp -a "$ruta" "$TMP/usr/bin/"
    # Y lo que cada uno arrastra. Igual que con libgdiplus: no se tocan las del nucleo del sistema,
    # que cambiarlas es justo lo que hace que un programa traido de otra distribucion no arranque.
    ldd "$ruta" 2>/dev/null | sed -n 's/.* => \(\/[^ ]*\) .*/\1/p' | while read -r lib; do
      case "$(basename "$lib")" in
        libc.so.*|libm.so.*|libpthread.so.*|libdl.so.*|librt.so.*|ld-linux*|libgcc_s.so.*) continue ;;
      esac
      [ -f "$lib" ] && cp -a "$lib" "$TMP/usr/lib/" 2>/dev/null || true
    done
  done
fi

di "copiando la rocola..."
cp -a "$EXE" "$TMP/opt/rocola/Rocola.exe"
for d in "$(dirname "$EXE")"/*.dll; do [ -f "$d" ] && cp -a "$d" "$TMP/opt/rocola/"; done
for m in rocola_ia.bin rocola_eq_ia.bin; do
  [ -f "$ROCOLA_DIR/$m" ] && cp -a "$ROCOLA_DIR/$m" "$TMP/opt/rocola/"
done
cp -a "$AQUI/arranca_rocola.sh" "$TMP/opt/rocola/"
chmod +x "$TMP/opt/rocola/arranca_rocola.sh"

# ---------- 3b) dejar el programa YA TRADUCIDO (arranca antes) ----------
#
#  Mono traduce el programa a lenguaje de maquina EN CADA ARRANQUE. Se puede dejar traducido de
#  antemano, y en Puppy encaja perfecto: el archivo traducido viaja dentro del .sfs, o sea en la
#  memoria. Medido con pruebas/arranque.exe -de arrancar a que la primera muestra llegue a la tarjeta
#  de sonido, seis arranques, la mediana-: 370 ms -> 324 ms, un 12% menos, por 1.4 MB de paquete.
#
#  Si algo de esto fallara NO se cae el guion: el .sfs sale igual, solo que sin traducir, y la rocola
#  funciona exactamente igual arrancando un pelin mas lenta. Es una mejora, no un requisito.
echo
di "dejando el programa ya traducido..."
if mono --aot "$TMP/opt/rocola/Rocola.exe" > /dev/null 2>&1 && [ -f "$TMP/opt/rocola/Rocola.exe.so" ]; then
  di "  la rocola:  hecho  ($(du -h "$TMP/opt/rocola/Rocola.exe.so" | cut -f1))"
else
  rm -f "$TMP/opt/rocola/Rocola.exe.so"
  di "  la rocola:  no se pudo (hace falta el ensamblador del sistema). El .sfs sale igual, sin esto."
fi

#  Y EL CORAZON DE .NET, que es donde de verdad esta el grueso. Medido con el registro de Mono: al
#  arrancar la rocola salen 2074 metodos ya traducidos, y SOLO 147 son codigo nuestro; los otros 1927
#  vienen del 'mscorlib'. Muchas distribuciones ya lo traen traducido de fabrica, y entonces viaja
#  dentro del .sfs con el resto de Mono y no hay nada que hacer. Si NO viene, esos 1927 metodos se
#  traducirian en cada arranque y aqui se gana bastante mas que con el programa. Cuesta unos 17 MB de
#  paquete y cuatro segundos de este guion.
#
#  Se escribe con "outfile=" DENTRO del arbol que se va a comprimir, a proposito: Mono no deja
#  traducir una COPIA de su mscorlib -exige el mismo que el tiene cargado-, y tocar el del sistema
#  seria dejar la maquina distinta de como estaba solo por armar un paquete.
YA_TRADUCIDO=""
for c in "$MONO_PREFIJO"/lib/mono/*/mscorlib.dll.so "$MONO_PREFIJO"/lib/mono/aot-cache/*/mscorlib.dll.so; do
  [ -f "$c" ] && { YA_TRADUCIDO="$c"; break; }
done
if [ -n "$YA_TRADUCIDO" ]; then
  di "  .NET:       ya venia traducido de fabrica, viaja dentro del .sfs tal cual"
else
  # CUAL ES EL mscorlib DE VERDAD. Junto a Mono hay una docena de carpetas con un archivo llamado
  # igual (2.0-api, 4.5-api, 4.8-api...), pero esas son de MENTIRA: solo dicen que funciones existen,
  # para que el compilador pueda comprobar el codigo. La de verdad -la que Mono carga al arrancar- es
  # una sola, y traducir cualquiera de las otras no serviria de nada.
  #
  # En vez de adivinar cual es, se PRUEBA: Mono se niega en redondo a traducir un mscorlib que no sea
  # el suyo ("doesn't match original file name"), asi que se le ofrecen los candidatos y el que cuele
  # es el bueno. Se descartan de entrada los "-api", que nunca lo son, y se prueba de la version mas
  # nueva a la mas vieja.
  di "  .NET:       no venia traducido; traduciendolo (tarda unos segundos)..."
  hecho_nucleo=0
  for c in $(ls -d "$MONO_PREFIJO"/lib/mono/*/mscorlib.dll 2>/dev/null | grep -v -- "-api/" | sort -Vr); do
    VER="$(basename "$(dirname "$c")")"
    mkdir -p "$TMP/usr/lib/mono/$VER"
    if mono --aot=outfile="$TMP/usr/lib/mono/$VER/mscorlib.dll.so" "$c" > /dev/null 2>&1 \
       && [ -f "$TMP/usr/lib/mono/$VER/mscorlib.dll.so" ]; then
      di "              hecho: $VER  ($(du -h "$TMP/usr/lib/mono/$VER/mscorlib.dll.so" | cut -f1))"
      hecho_nucleo=1
      break
    fi
    rm -f "$TMP/usr/lib/mono/$VER/mscorlib.dll.so"
  done
  [ $hecho_nucleo -eq 1 ] || di "              no se pudo. El .sfs sale igual, sin esto."
fi

# ---------- 4) a comprimir ----------
echo
di "comprimiendo (esto tarda un poco)..."
rm -f "$SALIDA"
mksquashfs "$TMP" "$SALIDA" -comp gzip -no-progress -noappend >/dev/null
rm -rf "$TMP"

TAM="$(du -h "$SALIDA" | cut -f1)"
echo
echo "  LISTO:  $SALIDA   ($TAM)"
echo
[ $HAY_VIDEO -eq 1 ] && echo "  (con videos: mpv y ffmpeg van dentro)" || echo "  (SIN videos: faltaban mpv o ffmpeg)"
echo
echo "  Ahora, en Puppy:"
echo "    1. Copia $SALIDA junto a los archivos de arranque de Puppy."
echo "    2. Menu -> Sistema -> BootManager -> \"cargar SFS al arrancar\", y marca rocola.sfs."
echo "    3. Reinicia y corre  sh instalar.sh  una sola vez."
echo
