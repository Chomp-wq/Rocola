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
#      la rocola     el programa, sus dos bibliotecas y el modelo de IA
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

di "copiando la rocola..."
cp -a "$EXE" "$TMP/opt/rocola/Rocola.exe"
for d in "$(dirname "$EXE")"/*.dll; do [ -f "$d" ] && cp -a "$d" "$TMP/opt/rocola/"; done
for m in rocola_ia.bin rocola_eq_ia.bin; do
  [ -f "$ROCOLA_DIR/$m" ] && cp -a "$ROCOLA_DIR/$m" "$TMP/opt/rocola/"
done
cp -a "$AQUI/arranca_rocola.sh" "$TMP/opt/rocola/"
chmod +x "$TMP/opt/rocola/arranca_rocola.sh"

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
echo "  Ahora, en Puppy:"
echo "    1. Copia $SALIDA junto a los archivos de arranque de Puppy."
echo "    2. Menu -> Sistema -> BootManager -> \"cargar SFS al arrancar\", y marca rocola.sfs."
echo "    3. Reinicia y corre  sh instalar.sh  una sola vez."
echo
