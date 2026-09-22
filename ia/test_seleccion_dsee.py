"""Seleccion del modelo DSEE: candidato, curva clasica e instalado sobre el MISMO conjunto de prueba."""
import contextlib
import hashlib
import io
import json
import os
import struct
import tempfile
import unittest
from unittest import mock

import numpy as np

import calidad_dsee as Q
import entrenar_ia as T


class Fijo:
    """Un 'modelo' que devuelve siempre lo mismo (para fijar errores exactos)."""
    def __init__(self, v):
        self.v = np.asarray(v, dtype=np.float64)
    def predict(self, x):
        return np.broadcast_to(self.v, (len(x), 12)).copy()


def _datos(n=400, semilla=0):
    rng = np.random.default_rng(semilla)
    x = rng.normal(0, 1, (n, 28))
    x[:, 25] = rng.uniform(.55, .8, n)
    # Objetivos alrededor de -1 por banda: la curva fija se queda lejos y un modelo sencillo la mejora.
    return x, rng.normal(-1.0, .3, (n, 12)) + np.linspace(-.3, .3, 12)


def _modelo(W, B):
    m = T.Mlp([28, 12], np.random.default_rng(0))
    m.W, m.B = [np.asarray(W, dtype=np.float64)], [np.asarray(B, dtype=np.float64)]
    return m


class LeerElInstalado(unittest.TestCase):
    def test_lee_como_la_rocola_y_rechaza_lo_que_ella_rechaza(self):
        m = T.Mlp([28, 7, 12], np.random.default_rng(4))
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "m.bin")
            Q.exportar(m, ruta)
            r = Q.cargar_ria1(ruta)
            x = np.random.default_rng(1).normal(0, 1, (50, 28))
            np.testing.assert_allclose(r.predict(x), m.predict(x), atol=1e-12)   # m ya quedo en float32
            with open(ruta, "rb") as f:
                bueno = f.read()
            self.assertEqual(bueno, Q.bytes_ria1(m))
            malos = {
                "truncado": bueno[:-1], "sobrante": bueno + b"\0", "cabecera": b"RIA2" + bueno[4:],
                "nan": bueno[:20] + struct.pack("<f", float("nan")) + bueno[24:],
                "capas": bueno[:4] + struct.pack("<i", 9) + bueno[8:],
            }
            for nombre, contenido in malos.items():
                with open(ruta, "wb") as f:
                    f.write(contenido)
                with self.assertRaises(ValueError, msg=nombre):
                    Q.cargar_ria1(ruta)
            eq = T.Mlp([5, 3], np.random.default_rng(1))
            with open(ruta, "wb") as f:
                f.write(Q.bytes_ria1(eq))
            with self.assertRaises(ValueError):
                Q.cargar_ria1(ruta)                          # el del EQ no es un modelo del DSEE


class Decision(unittest.TestCase):
    def test_gana_a_la_curva_pero_pierde_contra_el_instalado(self):
        x, y = _datos()
        bueno, mejor = Fijo(y.mean(axis=0)), Fijo(y.mean(axis=0))
        # Instalado: la media exacta de cada banda. Candidato: casi igual pero un pelo peor.
        err = Q.evaluar_tres(Fijo(y.mean(axis=0) + .02), mejor, x, y)
        self.assertLess(err["candidato"].mean(), err["referencia"].mean() * .95)
        ok, motivos = Q.decide(err)
        self.assertFalse(ok)
        self.assertIn("instalado", motivos[1])
        err = Q.evaluar_tres(bueno, None, x, y)             # sin instalado, la misma red si pasaria
        self.assertTrue(Q.decide(err)[0])

    def test_gana_a_los_dos(self):
        x, y = _datos()
        err = Q.evaluar_tres(Fijo(y.mean(axis=0)), Fijo(y.mean(axis=0) + .3), x, y)
        ok, motivos = Q.decide(err)
        self.assertTrue(ok, motivos)

    def test_una_red_mas_grande_que_empata_no_gana_por_su_tamano(self):
        x, y = _datos()
        chico = Fijo(y.mean(axis=0))
        err = Q.evaluar_tres(Fijo(y.mean(axis=0) + 1e-4), chico, x, y)   # empata (0,0 % mejor)
        self.assertFalse(Q.decide(err)[0])

    def test_una_banda_mucho_peor_que_el_instalado_bloquea(self):
        x, y = _datos()
        base = y.mean(axis=0)
        cand = base.copy()
        cand[7] += .35                                        # 11 % mejor en global, pero la banda 7 mucho peor
        err = Q.evaluar_tres(Fijo(cand), Fijo(base + .15), x, y)
        ok, motivos = Q.decide(err)
        self.assertFalse(ok)
        self.assertIn("7", motivos[-1])

    def test_valores_no_finitos(self):
        x, y = _datos(40)
        with self.assertRaises(ValueError):
            Q.evaluar_tres(Fijo([float("nan")] * 12), None, x, y)
        with self.assertRaises(ValueError):
            Q.evaluar_tres(Fijo(np.zeros(12)), Fijo([float("inf")] * 12), x, y)

    def test_por_lotes_da_lo_mismo(self):
        x, y = _datos(333)
        a = Q.evaluar_tres(Fijo(y.mean(axis=0)), Fijo(np.zeros(12)), x, y, 1)
        b = Q.evaluar_tres(Fijo(y.mean(axis=0)), Fijo(np.zeros(12)), x, y, 4096)
        for k in a:
            np.testing.assert_allclose(a[k], b[k], rtol=1e-12)


class ProcesoEntero(unittest.TestCase):
    def _corre(self, carpeta, argv, epocas="3"):
        salida = io.StringIO()
        with mock.patch.object(T, "__file__", os.path.join(carpeta, "entrenar_ia.py")), \
             mock.patch.object(T.sys, "argv", ["entrenar_ia.py"] + argv + ["--clips", "60", "--epocas", epocas]), \
             contextlib.redirect_stdout(salida):
            codigo = T.main()
        return codigo, salida.getvalue()

    def test_informe_identico_byte_a_byte_y_sin_rutas(self):
        with tempfile.TemporaryDirectory() as d:
            c1, _ = self._corre(d, [])
            with open(os.path.join(d, "informe_dsee.json"), "rb") as f:
                uno = f.read()
            c2, _ = self._corre(d, [])
            with open(os.path.join(d, "informe_dsee.json"), "rb") as f:
                dos = f.read()
            self.assertEqual(c1, c2)
            self.assertEqual(uno, dos)
            inf = json.loads(uno.decode("ascii"))
            for clave in ("extractor", "reparto", "prueba", "validacion", "decision", "instalado", "politica", "checkpoint"):
                self.assertIn(clave, inf)
            self.assertEqual(inf["extractor"], 2)
            self.assertNotIn(d, uno.decode("ascii"))
            self.assertNotIn(os.sep + "tmp", uno.decode("ascii"))

    def test_vectores_con_la_huella_del_modelo_y_paridad_float32(self):
        with tempfile.TemporaryDirectory() as d:
            # Un modelo instalado malisimo: cualquier candidato razonable le gana.
            malo = _modelo(np.zeros((12, 28)), np.full(12, 3.0))
            Q.exportar(malo, os.path.join(d, "rocola_ia.bin"))
            # Aqui se prueba la huella y la paridad de los vectores, no la politica: se fuerza la decision.
            with mock.patch.object(Q, "decide", return_value=(True, ["forzado por la prueba"])):
                codigo, texto = self._corre(d, [])
            self.assertEqual(codigo, 0, texto[-400:])
            with open(os.path.join(d, "rocola_ia.bin"), "rb") as f:
                modelo = f.read()
            with open(os.path.join(d, "ia_vectores_prueba.txt")) as f:
                lineas = f.read().splitlines()
            self.assertTrue(lineas[0].startswith("# "))
            self.assertIn("sha256=" + hashlib.sha256(modelo).hexdigest(), lineas[0])
            red = Q.cargar_ria1(os.path.join(d, "rocola_ia.bin"))
            filas = [l for l in lineas if "|" in l]
            self.assertEqual(len(filas), 24)
            for l in filas:
                a, b = l.split("|")
                x = np.array([float(v) for v in a.split()])
                np.testing.assert_allclose(red.predict(x[None, :])[0], [float(v) for v in b.split()], atol=2e-6)


if __name__ == "__main__":
    unittest.main()
