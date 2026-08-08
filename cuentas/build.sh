#!/usr/bin/env bash
#
# Compila el APK de "Cuentas Tianguis" sin Android Studio y sin Gradle,
# usando unicamente las herramientas de linea de comandos:
#
#   aapt2 ............ compila y enlaza los recursos (res/)
#   javac ............ compila el codigo Java contra android.jar
#   dx / d8 .......... convierte las clases a bytecode de Dalvik (classes.dex)
#   zipalign ......... alinea el APK
#   apksigner ........ lo firma
#
# En Debian/Ubuntu esas herramientas se instalan con:
#
#   sudo apt-get install aapt apksigner zipalign dalvik-exchange \
#                        android-sdk-build-tools android-sdk-platform-23
#
# Uso:
#   ./build.sh                 compila el APK
#   ./build.sh limpiar         borra lo generado
#
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP="$RAIZ/app/src/main"
SALIDA="$RAIZ/build"

PAQUETE="mx.tianguis.cuentas"
VERSION_CODIGO="${VERSION_CODIGO:-1}"
VERSION_NOMBRE="${VERSION_NOMBRE:-1.0}"
MIN_SDK=21
TARGET_SDK=34

# La llave de firma se conserva entre compilaciones: si cambia, el telefono
# ya no reconoce la actualizacion y obliga a desinstalar primero.
LLAVE="${CUENTAS_LLAVE:-$SALIDA/llave-firma.jks}"
LLAVE_CLAVE="${CUENTAS_LLAVE_CLAVE:-cuentas}"
LLAVE_ALIAS="${CUENTAS_LLAVE_ALIAS:-cuentas}"

# --------------------------------------------------------------------- ayudas

titulo() { printf '\n\033[1;36m▸ %s\033[0m\n' "$1"; }
aviso()  { printf '\033[1;33m  ! %s\033[0m\n' "$1"; }
ok()     { printf '\033[1;32m  ✓ %s\033[0m\n' "$1"; }
morir()  { printf '\033[1;31m  ✗ %s\033[0m\n' "$1" >&2; exit 1; }

# Silencia el aviso de JAVA_TOOL_OPTIONS que ensucia toda la salida.
sin_ruido() { grep -v "Picked up JAVA_TOOL_OPTIONS" || true; }

buscar_herramienta() {
    local nombre="$1"; shift
    local candidato
    for candidato in "$@"; do
        if command -v "$candidato" >/dev/null 2>&1; then
            command -v "$candidato"
            return 0
        fi
    done
    morir "Falta $nombre. Instalalo con apt-get (ver el encabezado de este script)."
}

# ------------------------------------------------------------------- limpiar

if [[ "${1:-}" == "limpiar" ]]; then
    rm -rf "$SALIDA/intermedios" "$SALIDA"/*.apk
    ok "Listo, se borro lo generado (la llave de firma se conservo)."
    exit 0
fi

# ---------------------------------------------------------------- validacion

titulo "Buscando las herramientas"

AAPT2="$(buscar_herramienta aapt2 aapt2)"
ZIPALIGN="$(buscar_herramienta zipalign zipalign)"
APKSIGNER="$(buscar_herramienta apksigner apksigner)"
DEXER="$(buscar_herramienta "el conversor a dex (d8 o dx)" d8 dalvik-exchange)"

if [[ -n "${ANDROID_JAR:-}" ]]; then
    :
else
    for ruta in /usr/lib/android-sdk/platforms/android-*/android.jar \
                "${ANDROID_HOME:-/nada}"/platforms/android-*/android.jar \
                "${ANDROID_SDK_ROOT:-/nada}"/platforms/android-*/android.jar; do
        [[ -f "$ruta" ]] && ANDROID_JAR="$ruta"
    done
fi
[[ -n "${ANDROID_JAR:-}" && -f "$ANDROID_JAR" ]] \
    || morir "No encontre android.jar. Instala android-sdk-platform-23 o exporta ANDROID_JAR."

ok "aapt2      $AAPT2"
ok "dex        $DEXER"
ok "zipalign   $ZIPALIGN"
ok "apksigner  $APKSIGNER"
ok "android.jar $ANDROID_JAR"

# ------------------------------------------------------------------ recursos

INTER="$SALIDA/intermedios"
rm -rf "$INTER"
mkdir -p "$INTER/res" "$INTER/gen" "$INTER/clases" "$SALIDA"

titulo "Compilando los recursos"
"$AAPT2" compile --dir "$APP/res" -o "$INTER/res.zip"
ok "res/ compilado"

# aapt2 necesita el atributo package en el manifiesto; el proyecto lo declara
# en build.gradle (namespace) para que Android Studio tambien lo acepte, asi
# que aqui se inyecta sobre una copia temporal.
MANIFIESTO="$INTER/AndroidManifest.xml"
sed "s|<manifest |<manifest package=\"$PAQUETE\" |" "$APP/AndroidManifest.xml" > "$MANIFIESTO"

titulo "Enlazando los recursos"
"$AAPT2" link \
    -o "$INTER/base.apk" \
    -I "$ANDROID_JAR" \
    --manifest "$MANIFIESTO" \
    --java "$INTER/gen" \
    --min-sdk-version "$MIN_SDK" \
    --target-sdk-version "$TARGET_SDK" \
    --version-code "$VERSION_CODIGO" \
    --version-name "$VERSION_NOMBRE" \
    --no-version-vectors \
    "$INTER/res.zip"
ok "base.apk enlazado"

# --------------------------------------------------------------------- java

titulo "Compilando el codigo Java"
FUENTES="$INTER/fuentes.txt"
find "$APP/java" "$INTER/gen" -name '*.java' > "$FUENTES"
printf '  %s archivos .java\n' "$(wc -l < "$FUENTES")"

javac \
    -nowarn \
    -encoding UTF-8 \
    -source 8 -target 8 \
    -bootclasspath "$ANDROID_JAR" \
    -classpath "$ANDROID_JAR" \
    -d "$INTER/clases" \
    "@$FUENTES" 2>&1 | sin_ruido | grep -v "source value 8 is obsolete\|target value 8 is obsolete\|To suppress warnings" || true

[[ -n "$(find "$INTER/clases" -name '*.class' -print -quit)" ]] \
    || morir "javac no genero ninguna clase."
ok "clases compiladas"

# ---------------------------------------------------------------------- dex

titulo "Convirtiendo a dex"
if [[ "$(basename "$DEXER")" == "d8" ]]; then
    "$DEXER" --min-api "$MIN_SDK" --lib "$ANDROID_JAR" --output "$INTER" \
        $(find "$INTER/clases" -name '*.class') 2>&1 | sin_ruido
else
    "$DEXER" --dex --min-sdk-version="$MIN_SDK" \
        --output="$INTER/classes.dex" "$INTER/clases" 2>&1 | sin_ruido
fi
[[ -f "$INTER/classes.dex" ]] || morir "No se genero classes.dex."
ok "classes.dex ($(du -h "$INTER/classes.dex" | cut -f1))"

# ---------------------------------------------------------------------- apk

titulo "Armando el APK"
cp "$INTER/base.apk" "$INTER/sin-firmar.apk"
( cd "$INTER" && zip -q -j sin-firmar.apk classes.dex )

"$ZIPALIGN" -f 4 "$INTER/sin-firmar.apk" "$INTER/alineado.apk"
ok "alineado a 4 bytes"

if [[ ! -f "$LLAVE" ]]; then
    aviso "No habia llave de firma; se crea una nueva en $LLAVE"
    aviso "Guardala: si se pierde, las siguientes versiones no podran instalarse encima."
    keytool -genkeypair \
        -keystore "$LLAVE" \
        -storepass "$LLAVE_CLAVE" -keypass "$LLAVE_CLAVE" \
        -alias "$LLAVE_ALIAS" \
        -keyalg RSA -keysize 2048 -validity 10000 \
        -dname "CN=Cuentas Tianguis, O=Artesanias, C=MX" 2>&1 | sin_ruido
fi

APK="$SALIDA/cuentas-tianguis.apk"
rm -f "$APK"
"$APKSIGNER" sign \
    --ks "$LLAVE" \
    --ks-pass "pass:$LLAVE_CLAVE" \
    --key-pass "pass:$LLAVE_CLAVE" \
    --ks-key-alias "$LLAVE_ALIAS" \
    --out "$APK" \
    "$INTER/alineado.apk" 2>&1 | sin_ruido
ok "firmado"

titulo "Comprobando el resultado"
"$APKSIGNER" verify --verbose "$APK" 2>&1 | sin_ruido | head -6

printf '\n\033[1;32m APK listo:\033[0m %s  (%s)\n\n' "$APK" "$(du -h "$APK" | cut -f1)"
