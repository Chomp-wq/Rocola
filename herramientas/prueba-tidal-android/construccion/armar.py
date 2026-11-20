#!/usr/bin/env python3
# Junta el APK que arma aapt2 (manifiesto binario, assets) con classes.dex y lo deja ALINEADO a 4 bytes (lo que hace zipalign):
# Android 11 o mas nuevo exige resources.arsc sin comprimir y alineado en los programas con targetSdk 30+.
# Uso: armar.py base.apk classes.dex salida.apk
import sys, zipfile, struct

base, dex, salida = sys.argv[1:4]

def agrega(zf, nombre, datos, comprime, fecha=(2026, 1, 1, 0, 0, 0)):
    zi = zipfile.ZipInfo(nombre, fecha)
    zi.create_system = 3
    zi.external_attr = 0o644 << 16
    if comprime:
        zi.compress_type = zipfile.ZIP_DEFLATED
        zf.writestr(zi, datos)
        return
    # sin comprimir: se rellena el campo extra para que los datos empiecen en un multiplo de 4
    zi.compress_type = zipfile.ZIP_STORED
    pos = zf.fp.tell()
    n = len(nombre.encode("utf-8"))
    falta = (4 - (pos + 30 + n) % 4) % 4
    if falta:
        largo = falta + 4  # un campo extra mide al menos 4 bytes (id + tamano)
        zi.extra = struct.pack("<HH", 0xD935, largo - 4) + b"\x00" * (largo - 4)
    zf.writestr(zi, datos)

with zipfile.ZipFile(base) as zb, zipfile.ZipFile(salida, "w") as zo:
    nombres = [i.filename for i in zb.infolist()]
    # el manifiesto primero, como hace aapt2/AGP
    orden = sorted(nombres, key=lambda x: (x != "AndroidManifest.xml", x != "resources.arsc"))
    for nombre in orden:
        if nombre.endswith("/"):
            continue
        datos = zb.read(nombre)
        # resources.arsc y lo que ya estaba sin comprimir va sin comprimir; lo demas, comprimido
        sin = zb.getinfo(nombre).compress_type == zipfile.ZIP_STORED or nombre == "resources.arsc"
        agrega(zo, nombre, datos, not sin)
    agrega(zo, "classes.dex", open(dex, "rb").read(), True)
print("APK armado:", salida)
