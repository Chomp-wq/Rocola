"""Instantanea verificable y aislada para entrenar sin tocar datos/modelos instalados."""
import argparse
import hashlib
import json
import os
import shutil
import struct
import zlib

import origen_dsee as O
from contrato_dsee import EXTRACTOR


def preparar(datos, instalado, destino, max_mb=512):
    if max_mb <= 0:
        raise ValueError("El presupuesto debe ser positivo")
    # Cabecera y rangos cerrados: la rocola puede seguir agregando otros ejemplos.
    with open(datos, "rb") as f:
        cab = f.read(16)
    if cab != b"RIAD" + struct.pack("<iii", 1, 28, 12):
        raise ValueError("Cabecera RIAD incompatible")
    ent, informe = O.lee_indice(O.ruta_indice(datos), datos)
    ent = ent[ent["extractor"] == EXTRACTOR]
    grupos, nivel = O.grupos(ent)
    partes, motivo = O.reparte(grupos, ent["cuenta"])
    if motivo:
        raise ValueError("No hay validacion independiente: " + motivo)
    total = int(ent["cuenta"].sum()) * O.TAM_REGISTRO
    if total > max_mb * 1048576:
        raise ValueError("La instantanea supera el presupuesto; no se trunca por orden")
    from calidad_dsee import cargar_ria1
    cargar_ria1(instalado)  # no entrenar contra una referencia ilegible
    os.mkdir(destino)  # nunca reutilizar/sobrescribir una carpeta de modelo
    ruta = os.path.join(destino, "datos.bin")
    h = hashlib.sha256()
    with open(datos, "rb") as src, open(ruta, "xb") as dst, open(O.ruta_indice(ruta), "xb") as idx:
        dst.write(cab)
        idx.write(O.cabecera_idx())
        primero = 0
        for e in ent:
            src.seek(16 + int(e["primero"]) * O.TAM_REGISTRO)
            falta = int(e["cuenta"]) * O.TAM_REGISTRO
            crc = 0
            while falta:
                b = src.read(min(falta, 1048576))
                if not b:
                    raise ValueError("Datos cambiaron durante la copia")
                dst.write(b)
                h.update(b)
                crc = zlib.crc32(b, crc)
                falta -= len(b)
            if crc & 0xffffffff != int(e["crc"]):
                raise ValueError("CRC cambio durante la copia; candidato no preparado")
            idx.write(O.entrada_bytes(primero, int(e["cuenta"]), EXTRACTOR,
                      int(e["rate"]), int(e["frame"]), int(e["canal"]), crc & 0xffffffff,
                      int(e["cancion"]), int(e["carpeta"]), int(e["artista"])))
            primero += int(e["cuenta"])
    here = os.path.dirname(os.path.abspath(__file__))
    for nombre in ("entrenar_ia.py", "calidad_dsee.py", "contrato_dsee.py",
                   "origen_dsee.py", "publicar_dsee.py", "paridad_extractor.txt"):
        shutil.copy2(os.path.join(here, nombre), os.path.join(destino, nombre))
    shutil.copy2(instalado, os.path.join(destino, "instalado.bin"))
    with open(os.path.join(destino, "instalado.bin"), "rb") as f:
        instalado_sha = hashlib.sha256(f.read()).hexdigest()
    resumen = {"formato": "ROCOLA-CANDIDATO-DSEE 1", "extractor": EXTRACTOR,
               "ejemplos": primero, "bytes_datos": total, "sha256_registros": h.hexdigest(),
               "indice": informe, "nivel": nivel,
               "grupos": [len(set(grupos[partes == p].tolist())) for p in range(3)],
               "instalado_sha256": instalado_sha,
               "activado": False, "escucha_pendiente": True}
    with open(os.path.join(destino, "preparacion.json"), "x", encoding="utf-8") as f:
        json.dump(resumen, f, sort_keys=True, indent=2)
    return resumen


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--datos", required=True)
    ap.add_argument("--instalado", required=True)
    ap.add_argument("--destino", required=True)
    ap.add_argument("--max-mb", type=int, default=512)
    a = ap.parse_args()
    print(json.dumps(preparar(a.datos, a.instalado, a.destino, a.max_mb), sort_keys=True))
