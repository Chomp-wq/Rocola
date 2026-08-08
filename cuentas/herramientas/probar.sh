#!/usr/bin/env bash
#
# Corre las pruebas de la logica de la app en la computadora, sin emulador.
#
# Se compilan solo las clases que no tocan Android (dinero, fechas, totales y
# el armado de los cortes) y se ejecutan con java normal.
#
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FUENTE="$RAIZ/app/src/main/java"
TMP="${TMPDIR:-/tmp}/cuentas-pruebas"

for ruta in /usr/lib/android-sdk/platforms/android-*/android.jar \
            "${ANDROID_HOME:-/nada}"/platforms/android-*/android.jar; do
    [[ -f "$ruta" ]] && ANDROID_JAR="${ANDROID_JAR:-$ruta}"
done
if [[ -z "${ANDROID_JAR:-}" || ! -f "${ANDROID_JAR:-}" ]]; then
    echo "No encontre android.jar (instala android-sdk-platform-23 o exporta ANDROID_JAR)" >&2
    exit 1
fi

rm -rf "$TMP"
mkdir -p "$TMP"

# Clases sin dependencias de Android en tiempo de ejecucion.
javac -nowarn -encoding UTF-8 -d "$TMP" -classpath "$ANDROID_JAR" \
    "$FUENTE/mx/tianguis/cuentas/Venta.java" \
    "$FUENTE/mx/tianguis/cuentas/Totales.java" \
    "$FUENTE/mx/tianguis/cuentas/Dinero.java" \
    "$FUENTE/mx/tianguis/cuentas/Fechas.java" \
    "$FUENTE/mx/tianguis/cuentas/Reporte.java" \
    "$FUENTE/mx/tianguis/cuentas/ExportTexto.java" \
    "$FUENTE/mx/tianguis/cuentas/Reportes.java" \
    "$FUENTE/mx/tianguis/cuentas/Prefs.java" \
    "$FUENTE/mx/tianguis/cuentas/Db.java" \
    "$RAIZ/herramientas/PruebaLogica.java" \
    2>&1 | grep -v "Picked up JAVA_TOOL_OPTIONS" || true

java -Dfile.encoding=UTF-8 -Dstdout.encoding=UTF-8 -cp "$TMP:$ANDROID_JAR" PruebaLogica \
    2>&1 | grep -v "Picked up JAVA_TOOL_OPTIONS"
