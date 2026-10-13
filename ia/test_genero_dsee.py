"""I3: el DSEE por genero. El nombre de archivo de cada genero (el mismo que calcula la rocola), el material que
entra al entrenar un genero y el instalador que solo deja pasar candidatos aceptados, escuchados y de ese genero."""
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import unittest

import numpy as np

import entrenar_ia as T
import instalar_dsee_genero as I
from contrato_dsee import EXTRACTOR

# Los mismos ejemplos que pruebas/mejorasia.cs le pasa a Form1.SlugGenero: si una de las dos cuentas cambia, falla.
EJEMPLOS = [("Cumbia", "cumbia"), ("  Regional Mexicano ", "regional_mexicano"), ("Rock & Roll", "rock_roll"),
            ("Música Tropical", "musica_tropical"), ("Baño de Pop", "bano_de_pop"), ("Unknown", ""),
            ("desconocido", ""), ("", ""), ("80's", "80_s"), ("Salsa/Merengue", "salsa_merengue"),
            ("__x__", "x"), ("a" * 60, "a" * 40)]


class Slug(unittest.TestCase):
    def test_como_la_rocola(self):
        for etiqueta, esperado in EJEMPLOS:
            self.assertEqual(T.slug_genero(etiqueta), esperado, etiqueta)


class SoloGenero(unittest.TestCase):
    def test_deja_solo_las_canciones_del_genero_sin_moverlas_de_particion(self):
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "rocola_ia_datos.bin")
            canciones = np.array([11, 11, 22, 33, 33, 44, 55, 55, 66], dtype=np.uint64)
            with open(os.path.join(d, "rocola_ia_datos.generos.tsv"), "w", encoding="utf-8") as f:
                for c, g in ((11, "Cumbia"), (22, "rock"), (33, "cumbia "), (44, "Cumbia"), (55, "salsa"), (44, "salsa")):
                    f.write("\n%016x\t%s\n" % (c, g))
            informe = {"_canciones": canciones, "_grupos": canciones, "motivo": ""}
            partes = [np.array([0, 1, 2, 5]), np.array([3, 4]), np.array([6, 7, 8])]
            nuevas, inf = T.solo_genero(partes, informe, ruta, None, "CUMBIA")
            self.assertEqual([list(p) for p in nuevas], [[0, 1], [3, 4], []])   # 44 paso a salsa: el ultimo gana
            self.assertEqual(inf["genero"], "cumbia")
            self.assertEqual(inf["grupos"], [1, 1, 0])

    def test_sin_sidecar_o_sin_origen_no_entrena(self):
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "rocola_ia_datos.bin")
            inf = {"_canciones": np.zeros(3, dtype=np.uint64), "motivo": "x"}
            with self.assertRaises(SystemExit):
                T.solo_genero([np.arange(3)] * 3, inf, ruta, None, "cumbia")
            with self.assertRaises(SystemExit):
                T.solo_genero(None, inf, ruta, None, "cumbia")
            with self.assertRaises(SystemExit):
                T.solo_genero([np.arange(3)] * 3, inf, ruta, None, "unknown")

    def test_genero_sin_material_de_la_rocola_se_rechaza(self):
        with tempfile.TemporaryDirectory() as d:
            r = subprocess.run([sys.executable, "entrenar_ia.py", "--genero", "cumbia", "--clips", "4",
                                "--candidato", os.path.join(d, "c.bin")], capture_output=True, text=True)
            self.assertNotEqual(r.returncode, 0)
            self.assertIn("--genero solo vale con --datos", r.stderr + r.stdout)
            self.assertFalse(os.path.exists(os.path.join(d, "c.bin")))


def _candidato(d, genero="cumbia", aceptado=True, extractor=EXTRACTOR, sha=None):
    os.makedirs(d, exist_ok=True)
    cand = os.path.join(d, "candidato.bin")
    with open(cand, "wb") as f:
        f.write(b"RIA1" + os.urandom(64))
    rep = {"formato": "ROCOLA-INFORME-DSEE 1", "extractor": extractor, "contrato_ria1": {"entradas": 28, "salidas": 12},
           "decision": {"aceptado_numericamente": aceptado, "motivos": ["prueba"]},
           "datos": {"tipo": "rocola", "genero": genero} if genero is not None else {"tipo": "rocola"},
           "candidato_sha256": sha or hashlib.sha256(open(cand, "rb").read()).hexdigest()}
    with open(os.path.join(d, "informe_dsee.json"), "w", encoding="ascii") as f:
        json.dump(rep, f)
    return cand


class Instalador(unittest.TestCase):
    def test_instala_solo_lo_aceptado_escuchado_y_de_un_genero(self):
        with tempfile.TemporaryDirectory() as d:
            destino = os.path.join(d, "datos"); os.makedirs(destino)
            general = os.path.join(destino, "rocola_ia.bin")
            with open(general, "wb") as f:
                f.write(b"general")
            casos = [(_candidato(os.path.join(d, "a"), aceptado=False), "no lo acepto"),
                     (_candidato(os.path.join(d, "b"), genero=None), "no es de un genero"),
                     (_candidato(os.path.join(d, "c"), extractor=EXTRACTOR + 1), "otro extractor"),
                     (_candidato(os.path.join(d, "e"), sha="0" * 64), "huella no coincide")]
            for cand, motivo in casos:
                with self.assertRaisesRegex(ValueError, motivo):
                    I.instala(cand, destino, escuchado=True)
            bueno = _candidato(os.path.join(d, "f"))
            with self.assertRaisesRegex(ValueError, "escuchado"):
                I.instala(bueno, destino, escuchado=False)
            self.assertEqual(sorted(os.listdir(destino)), ["rocola_ia.bin"])   # nada se escribio
            final = I.instala(bueno, destino, escuchado=True)
            self.assertEqual(os.path.basename(final), "rocola_ia_genero_cumbia.bin")
            self.assertEqual(open(final, "rb").read(), open(bueno, "rb").read())
            otro = _candidato(os.path.join(d, "g"))
            I.instala(otro, destino, escuchado=True)
            self.assertEqual(open(final, "rb").read(), open(otro, "rb").read())
            self.assertEqual(open(final + ".anterior-1", "rb").read(), open(bueno, "rb").read())   # el de antes, guardado
            self.assertEqual(open(general, "rb").read(), b"general")   # el general, intacto
            self.assertFalse(any(n.endswith(".instalando") for n in os.listdir(destino)))


if __name__ == "__main__":
    unittest.main()
