#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""INSTALAR UN MODELO DEL DSEE POR GENERO (I3, 13 de octubre de 2026).

entrenar_ia.py --datos rocola_ia_datos.bin --genero cumbia   deja un candidato aislado con su informe. Este programa lo
copia a la carpeta de datos de la rocola como  rocola_ia_genero_<genero>.bin  (la rocola lo usa con el interruptor
"Modelos del DSEE por genero" de Ctrl+F3, en las canciones de ese genero). El modelo general (rocola_ia.bin) no se toca.

Solo instala si:
  - el informe es del DSEE, del mismo extractor, contrato 28 -> 12, y dice "aceptado numericamente" (le gano al modelo
    instalado -el general- en la prueba de ESE genero, sin regresiones por clase ni diferencias con la rocola);
  - el informe es de ese genero, y la huella del candidato es la que dice el informe;
  - se confirma con --escuchado que ya se escucho en el A/B (como el general: nunca solo por los numeros).
Si ya habia un modelo de ese genero, se guarda al lado como .anterior-N antes de reemplazarlo. La copia es atomica.

Uso:  python instalar_dsee_genero.py --candidato dsee-genero-cumbia-candidato-XXXX/candidato.bin
                                     --destino "C:/rocola_datos" --escuchado
      (--informe va por defecto junto al candidato: informe_dsee.json)
"""
import hashlib
import json
import os
import sys

from contrato_dsee import EXTRACTOR
from entrenar_ia import slug_genero

PREFIJO = "rocola_ia_genero_"


def _sha(ruta):
    with open(ruta, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def comprueba(candidato, informe):
    """Devuelve el slug del genero si el candidato se puede instalar; si no, lanza ValueError con el motivo."""
    with open(informe, encoding="ascii") as f:
        rep = json.load(f)
    if rep.get("formato") != "ROCOLA-INFORME-DSEE 1":
        raise ValueError("no es un informe del DSEE")
    if rep.get("extractor") != EXTRACTOR:
        raise ValueError("el informe es de otro extractor (%r; se requiere %d)" % (rep.get("extractor"), EXTRACTOR))
    if rep.get("contrato_ria1") != {"entradas": 28, "salidas": 12}:
        raise ValueError("el contrato no es 28 -> 12")
    decision = rep.get("decision") or {}
    if not decision.get("aceptado_numericamente"):
        raise ValueError("el entrenador no lo acepto: " + "; ".join(decision.get("motivos", [])))
    genero = slug_genero((rep.get("datos") or {}).get("genero", ""))
    if not genero:
        raise ValueError("el informe no es de un genero (se entreno sin --genero): ese es el modelo general")
    if _sha(candidato) != rep.get("candidato_sha256"):
        raise ValueError("el candidato no es el del informe (la huella no coincide)")
    return genero


def instala(candidato, destino, informe=None, escuchado=False):
    informe = informe or os.path.join(os.path.dirname(os.path.abspath(candidato)), "informe_dsee.json")
    genero = comprueba(candidato, informe)
    if not escuchado:
        raise ValueError("falta --escuchado: hay que escucharlo en el A/B antes de instalarlo")
    if not os.path.isdir(destino):
        raise ValueError("la carpeta de datos no existe: " + destino)
    final = os.path.join(destino, PREFIJO + genero + ".bin")
    with open(candidato, "rb") as f:
        datos = f.read()
    temporal = final + ".instalando"
    with open(temporal, "wb") as f:
        f.write(datos); f.flush(); os.fsync(f.fileno())
    if os.path.exists(final):
        n = 1
        while os.path.exists("%s.anterior-%d" % (final, n)):
            n += 1
        os.replace(final, "%s.anterior-%d" % (final, n))
    os.replace(temporal, final)
    return final


def main(argv):
    def opcion(nombre):
        return argv[argv.index(nombre) + 1] if nombre in argv else None
    candidato, destino = opcion("--candidato"), opcion("--destino")
    if not candidato or not destino:
        print(__doc__)
        return 1
    try:
        final = instala(candidato, destino, opcion("--informe"), "--escuchado" in argv)
    except (ValueError, OSError) as e:
        print("No se instala:", e)
        return 2
    print("Instalado:", final)
    print("La rocola lo carga al arrancar. Ctrl+F3 -> Sonido -> Mejoras con IA -> 'Modelos del DSEE por genero'")
    print("(y el DSEE encendido).")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
