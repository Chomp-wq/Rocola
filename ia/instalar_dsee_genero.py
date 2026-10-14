#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""INSTALAR UN MODELO DEL DSEE POR GENERO (I3, 13 de octubre de 2026).

entrenar_ia.py --datos rocola_ia_datos.bin --genero cumbia   deja un candidato aislado con su informe. Este programa lo
copia a la carpeta IAS de la carpeta de datos de la rocola como  IAS/rocola_ia_genero_<genero>.bin  (la rocola lo usa
con el interruptor "Modelos del DSEE por genero" de Ctrl+F3, en las canciones de ese genero). El modelo general
(rocola_ia.bin) no se toca. Desde el 14 de octubre de 2026 la rocola lee todas sus IAs de la carpeta IAS; si no existe,
se crea.

Solo instala si:
  - el informe es del DSEE, del mismo extractor, contrato 28 -> 12, y dice "aceptado numericamente" (le gano al modelo
    instalado -el general- en la prueba de ESE genero, sin regresiones por clase ni diferencias con la rocola);
  - el informe es de ese genero, y la huella del candidato es la que dice el informe;
  - se confirma con --escuchado que ya se escucho en el A/B (como el general: nunca solo por los numeros).
Si ya habia un modelo de ese genero (en IAS, o suelto en la carpeta de datos, donde iban antes), se guarda en IAS como
.anterior-N antes de reemplazarlo. La copia es atomica.

LO NORMAL: DOBLE CLIC en instalar_dsee_genero.bat (o en este .py). Sin argumentos, el programa pregunta:
  - busca solo los modelos por genero que dejo entrenar_dsee.bat en  ia/entrenado/dsee-genero-*  y dice de cada uno si
    se puede instalar o por que no;
  - propone la carpeta de datos de la rocola (ROCOLA_DATOS, o la de Rocola.exe, que esta encima de esta carpeta ia);
  - pregunta, genero por genero, si ya lo escuchaste;
  - y no cierra la ventana hasta que aprietes Enter. Lo que paso queda tambien en instalar_dsee_genero_log.txt.
  (Antes, con doble clic, sin argumentos, imprimia la ayuda y la ventana se cerraba en el acto.)

Uso a mano:  python instalar_dsee_genero.py --candidato dsee-genero-cumbia-XXXX/candidato.bin
                                            --destino "C:/rocola_datos" --escuchado
      (--informe va por defecto junto al candidato: informe_dsee.json)
"""
import glob
import hashlib
import json
import os
import sys
import traceback

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)   # con doble clic la carpeta de trabajo puede ser otra: las piezas estan aqui al lado

try:
    from contrato_dsee import EXTRACTOR
    from entrenar_ia import slug_genero
except Exception:   # sin numpy, o falta una pieza al lado: con doble clic hay que verlo, no cerrar la ventana
    if __name__ == "__main__":
        traceback.print_exc()
        print("\n  No se pudo cargar el entrenador que esta al lado de este archivo (lo de arriba).")
        print("  Casi siempre es que falta numpy:  pip install numpy   (o que este archivo no esta en la carpeta ia).")
        try:
            input("\n  Aprieta Enter para cerrar esta ventana...")
        except (EOFError, KeyboardInterrupt):
            pass
        sys.exit(1)
    raise

PREFIJO = "rocola_ia_genero_"
CARPETA_IAS = "IAS"


def carpeta_ias(destino):
    """La carpeta IAS de la carpeta de datos (o la misma, si ya se dio la carpeta IAS)."""
    if os.path.basename(os.path.normpath(destino)).upper() == CARPETA_IAS:
        return destino
    return os.path.join(destino, CARPETA_IAS)


def _libre_anterior(final):
    n = 1
    while os.path.exists("%s.anterior-%d" % (final, n)):
        n += 1
    return "%s.anterior-%d" % (final, n)


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
    ias = carpeta_ias(destino)
    os.makedirs(ias, exist_ok=True)
    nombre = PREFIJO + genero + ".bin"
    final = os.path.join(ias, nombre)
    with open(candidato, "rb") as f:
        datos = f.read()
    temporal = final + ".instalando"
    with open(temporal, "wb") as f:
        f.write(datos); f.flush(); os.fsync(f.fileno())
    # El de antes se guarda: el de IAS y tambien uno suelto en la carpeta de datos (si se dejara suelto, la rocola lo
    # tomaria al arrancar por uno recien puesto y apartaria a este).
    suelto = os.path.join(destino, nombre)
    for viejo in (final, suelto):   # si destino ya era IAS, son el mismo: el segundo ya no existe
        if os.path.exists(viejo):
            os.replace(viejo, _libre_anterior(final))
    os.replace(temporal, final)
    return final


# ---------------------------------------------------------------- con doble clic: preguntando
def candidatos_de_genero(carpeta=None):
    """Las carpetas dsee-genero-* que dejo el entrenador, con lo que se sabe de cada una:
    (carpeta, genero o '', candidato.bin o None, motivo si no se puede instalar o None)."""
    carpeta = carpeta or os.path.join(AQUI, "entrenado")
    r = []
    for d in sorted(glob.glob(os.path.join(carpeta, "dsee-genero-*"))):
        if not os.path.isdir(d):
            continue
        cand, inf = os.path.join(d, "candidato.bin"), os.path.join(d, "informe_dsee.json")
        genero = os.path.basename(d)[len("dsee-genero-"):].rsplit("-", 1)[0]
        if not os.path.exists(cand) or not os.path.exists(inf):
            r.append((d, genero, None, "el entrenamiento no termino (falta el candidato o su informe)"))
            continue
        try:
            r.append((d, comprueba(cand, inf), cand, None))
        except (ValueError, OSError) as e:
            r.append((d, genero, cand, str(e)))
    return r


def carpeta_de_la_rocola():
    """Donde la rocola busca los modelos: ROCOLA_DATOS si esta puesta; si no, la carpeta de Rocola.exe."""
    d = (os.environ.get("ROCOLA_DATOS") or "").strip()
    if d and os.path.isdir(d):
        return d
    for c in (os.path.dirname(AQUI), AQUI):
        if any(os.path.exists(os.path.join(c, n)) for n in ("Rocola.exe", "rocola.exe")):
            return c
    return None


class Bitacora(object):
    def __init__(self, ruta):
        self.ruta = ruta
        try:
            self.f = open(ruta, "w", encoding="utf-8")
        except OSError:
            self.f = None

    def __call__(self, *t):
        linea = " ".join(str(x) for x in t)
        print(linea)
        if self.f:
            self.f.write(linea + "\n"); self.f.flush()


def interactivo(pregunta=input, carpeta=None, destino=None, bitacora=None):
    di = Bitacora(bitacora or os.path.join(AQUI, "instalar_dsee_genero_log.txt"))
    di("=" * 74)
    di("  INSTALAR LOS MODELOS DEL DSEE POR GENERO")
    di("=" * 74)
    lista = candidatos_de_genero(carpeta)
    if not lista:
        di("")
        di("  No hay ningun modelo por genero entrenado en:")
        di("     " + (carpeta or os.path.join(AQUI, "entrenado")))
        di("")
        di("  El entrenador solo hace modelos por genero si junto al material esta el archivo")
        di("  rocola_ia_datos.generos.tsv (la rocola lo escribe al juntar material) y si algun genero")
        di("  tiene 60 canciones o mas con material. Mira el RESUMEN de entrenar_dsee.bat: ahi dice por que.")
        return 1
    di("")
    di("  Modelos por genero encontrados:")
    for d, genero, cand, motivo in lista:
        di("     %-20s %s" % (genero or "?", "se puede instalar" if motivo is None else "NO: " + motivo))
        di("        " + d)
    buenos = [x for x in lista if x[3] is None]
    if not buenos:
        di("")
        di("  Ninguno se puede instalar. Lo normal es que no le ganara al modelo general en su genero:")
        di("  entonces la rocola sigue usando el general para ese genero, que es lo correcto.")
        return 2
    destino = destino or carpeta_de_la_rocola()
    di("")
    if destino:
        di("  Carpeta de datos de la rocola:  " + destino)
        r = pregunta("  Enter para usarla, o escribe otra carpeta: ").strip().strip('"')
        if r:
            destino = r
    else:
        destino = pregunta("  Escribe la carpeta de datos de la rocola (donde esta Rocola.exe o ROCOLA_DATOS): ").strip().strip('"')
    if not destino or not os.path.isdir(destino):
        di("  No existe esa carpeta: " + (destino or "(vacia)") + ". No se instala nada.")
        return 2
    instalados = 0
    for d, genero, cand, motivo in buenos:
        di("")
        r = pregunta("  '%s': ya lo escuchaste en el A/B y quieres instalarlo? (s/n): " % genero).strip().lower()
        di("     respuesta: " + (r or "(nada)"))
        if r not in ("s", "si", "y", "yes"):
            di("     no se instala " + genero)
            continue
        try:
            final = instala(cand, destino, None, True)
            di("     Instalado: " + final)
            instalados += 1
        except (ValueError, OSError) as e:
            di("     No se instala: %s" % e)
    di("")
    if instalados:
        di("  Listo: %d instalado(s). Reinicia la rocola (los carga al arrancar) y en Ctrl+F3 -> Sonido ->" % instalados)
        di("  Mejoras con IA enciende 'Modelos del DSEE por genero' (con el DSEE encendido).")
    else:
        di("  No se instalo ninguno.")
    return 0 if instalados else 2


def main(argv):
    if len(argv) <= 1:   # doble clic: preguntando, y sin cerrar la ventana hasta que se lea
        cod = 1
        try:
            cod = interactivo()
        except KeyboardInterrupt:
            pass
        except Exception:
            traceback.print_exc()
            print("\n  Algo fallo (lo de arriba). Mandalo tal cual si pides ayuda.")
        try:
            input("\n  Aprieta Enter para cerrar esta ventana...")
        except (EOFError, KeyboardInterrupt):
            pass
        return cod

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
