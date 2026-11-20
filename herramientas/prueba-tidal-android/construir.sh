#!/bin/bash
# Arma RocolaPruebaTidal.apk desde Linux SIN el SDK de Android ni Gradle: con Java (17 o mas), curl, unzip y python3.
# Baja de Maven Central lo que hace falta (la primera vez, ~155 MB) a .herramientas/ y arma, alinea y firma el APK.
#   aapt2 (dentro de apktool)      -> manifiesto binario y assets
#   dx (dalvik-dx)                 -> classes.dex
#   apksig                         -> firma v2 (basta desde Android 7)
#   android-all (Robolectric)      -> el android.jar contra el que se compila y para resolver el manifiesto
set -euo pipefail
cd "$(dirname "$0")"
H=.herramientas; B=build; SALIDA=RocolaPruebaTidal.apk
MINSDK=26; TARGETSDK=34
C=https://repo.maven.apache.org/maven2
mkdir -p "$H" "$B"

baja() { [ -s "$H/$1" ] || { echo "Bajando $1..."; curl -fsSL -o "$H/$1.tmp" "$2" && mv "$H/$1.tmp" "$H/$1"; }; }
baja apktool-cli.jar "$C/org/apktool/apktool-cli/3.0.3/apktool-cli-3.0.3.jar"
baja dx.jar          "$C/com/jakewharton/android/repackaged/dalvik-dx/16.0.1/dalvik-dx-16.0.1.jar"
baja apksig.jar      "$C/com/android/tools/build/apksig/2.3.0/apksig-2.3.0.jar"
baja android-all.jar "$C/org/robolectric/android-all/14-robolectric-10818077/android-all-14-robolectric-10818077.jar"
[ -x "$H/aapt2" ] || { unzip -p "$H/apktool-cli.jar" prebuilt/linux/aapt2 > "$H/aapt2"; chmod +x "$H/aapt2"; }

# La llave de firma (de prueba): se hace una vez y se guarda. Para ACTUALIZAR una app ya instalada hay que firmar con la misma.
if [ ! -f "$H/rocola-prueba.p12" ]; then
  keytool -genkeypair -keystore "$H/rocola-prueba.p12" -storetype PKCS12 -alias rocola -keyalg RSA -keysize 2048 \
    -validity 10000 -storepass rocola-prueba -dname "CN=Rocola prueba, O=Rocola" >/dev/null 2>&1
fi

rm -rf "$B/clases" "$B/assets" "$B/firma"; mkdir -p "$B/clases" "$B/assets" "$B/firma"
# La pagina y el SDK de TIDAL son los de la prueba de Windows (una sola copia).
cp ../prueba-tidal-windows/src/pagina.html "$B/assets/pagina.html"
cp ../prueba-tidal-windows/src/tidal_sdk.js "$B/assets/tidal_sdk.js"

echo "Compilando..."
javac -nowarn --release 8 -cp "$H/android-all.jar" -d "$B/clases" src/com/rocola/pruebatidal/*.java 2>&1 | grep -v "^Picked up\|^warning\|^Note" || true
java -cp "$H/dx.jar" com.android.dx.command.Main --dex --min-sdk-version=$MINSDK --output="$B/classes.dex" "$B/clases"

echo "Empaquetando..."
"$H/aapt2" link -o "$B/base.apk" -I "$H/android-all.jar" --manifest AndroidManifest.xml -A "$B/assets" \
  --min-sdk-version $MINSDK --target-sdk-version $TARGETSDK --version-code 1 --version-name 1.0 --auto-add-overlay
python3 construccion/armar.py "$B/base.apk" "$B/classes.dex" "$B/sin-firmar.apk"

echo "Firmando... (apksig 2.3.0 usa clases internas de Java: de ahi las opciones --add-exports)"
javac -nowarn -cp "$H/apksig.jar" -d "$B/firma" construccion/Firma.java 2>&1 | grep -v "^Picked up\|^Note" || true
java --add-exports java.base/sun.security.x509=ALL-UNNAMED --add-exports java.base/sun.security.pkcs=ALL-UNNAMED --add-exports java.base/sun.security.util=ALL-UNNAMED -cp "$H/apksig.jar:$B/firma" Firma "$B/sin-firmar.apk" "$SALIDA" "$H/rocola-prueba.p12" rocola-prueba rocola $MINSDK 2>&1 | grep -v "^Picked up"
ls -la "$SALIDA"
