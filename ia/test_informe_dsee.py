"""El informe del DSEE: errores por clase de senal, transitorios y lo que calcularia la rocola."""
import contextlib
import io
import json
import os
import struct
import tempfile
import unittest
import zlib
from unittest import mock

import numpy as np

import calidad_dsee as Q
import entrenar_ia as T
import origen_dsee as O


class Fijo:
    def __init__(self, v):
        self.v = np.asarray(v, dtype=np.float64)
    def predict(self, x):
        return np.broadcast_to(self.v, (len(x), 12)).copy()


class PeorEnTonal:
    """Acierta en todo menos en los cuadros mas tonales (entrada 26 muy negativa)."""
    def __init__(self, y_medio):
        self.y = y_medio
    def predict(self, x):
        p = np.broadcast_to(self.y, (len(x), 12)).copy()
        p[x[:, 26] < -1.0] += 0.8
        return p


def _datos(n=3000, semilla=0):
    rng = np.random.default_rng(semilla)
    x = rng.normal(0, 1, (n, 28))
    x[:, 24] = rng.uniform(-10, -5, n)
    x[:, 25] = rng.uniform(.5, .9, n)
    x[:, 26] = -np.abs(rng.normal(0, 1, n))
    return x, rng.normal(-1.0, .3, (n, 12))


class ComoLaRocola(unittest.TestCase):
    def test_float32_en_el_mismo_orden(self):
        m = T.Mlp([28, 24, 24, 12], np.random.default_rng(3))
        Q.cuantizar(m)
        x = np.random.default_rng(4).normal(0, 2, (500, 28))
        roc, malas = Q.prediccion_como_la_rocola(m, x)
        self.assertFalse(malas.any())
        self.assertLess(np.abs(roc - m.predict(x)).max(), 1e-5)
        self.assertGreater(np.abs(roc - m.predict(x)).max(), 0.0)      # de verdad es float32, no float64
        p = Q.paridad_inferencia(m, x)
        self.assertEqual((p["filas"], p["no_finitas"]), (500, 0))
        self.assertLess(p["max_abs"], Q.LIMITE_PARIDAD)

    def test_desborde_en_float32_se_detecta(self):
        m = T.Mlp([28, 12], np.random.default_rng(5))
        m.W[0][:] = 1e37                     # cabe en float32, pero 28 productos de 5e37 (1,4e39) desbordan al sumar
        m.B[0][:] = 0
        Q.cuantizar(m)
        x = np.full((10, 28), 5.0)
        _, malas = Q.prediccion_como_la_rocola(m, x)
        self.assertTrue(malas.all())
        self.assertTrue(np.isfinite(m.predict(x)).all())            # en float64 no se veria nada raro
        self.assertEqual(Q.paridad_inferencia(m, x)["no_finitas"], 10)


class PorClase(unittest.TestCase):
    def test_clases_umbrales_y_lotes(self):
        x, y = _datos()
        a = Q.errores_por_clase(Fijo(y.mean(axis=0)), Fijo(np.zeros(12)), x, y, None, lote=1)
        b = Q.errores_por_clase(Fijo(y.mean(axis=0)), Fijo(np.zeros(12)), x, y, None, lote=4096)
        self.assertEqual(json.dumps(a, sort_keys=True), json.dumps(b, sort_keys=True))
        for dim, nombres in Q.DIMENSIONES:
            self.assertEqual(sum(a[dim][n]["filas"] for n in nombres), len(x))
            self.assertEqual(sorted(a[dim]), sorted(nombres))
        self.assertEqual(a["umbrales"]["corte"], [13.0, 16.0])
        self.assertLess(abs(a["nivel"]["suave"]["filas"] - 1000), 3)   # terciles
        self.assertFalse(a["transitorios"]["disponible"])
        self.assertIn("cuadros sueltos", a["transitorios"]["motivo"])

    def test_aviso_en_la_clase_que_empeora_aunque_el_global_mejore(self):
        x, y = _datos()
        inst = Fijo(y.mean(axis=0) + .6)
        cand = PeorEnTonal(y.mean(axis=0))
        err = Q.evaluar_tres(cand, inst, x, y)
        self.assertLess(err["candidato"].mean(), err["instalado"].mean())   # en global gana
        pc = Q.errores_por_clase(cand, inst, x, y)
        avisos = Q.avisos_por_clase(pc)
        self.assertTrue(any(a.startswith("tonalidad=tonal") and "el modelo instalado" in a for a in avisos), avisos)
        self.assertFalse(any(a.startswith("tonalidad=ruidosa") for a in avisos), avisos)
        self.assertFalse(Q.avisos_por_clase(Q.errores_por_clase(Fijo(y.mean(axis=0)), inst, x, y)))

    def test_transitorios_cuando_hay_senal(self):
        x, y = _datos()
        t = np.zeros(len(x), dtype=bool)
        t[::7] = True
        pc = Q.errores_por_clase(Fijo(y.mean(axis=0)), None, x, y, t)
        self.assertTrue(pc["transitorios"]["disponible"])
        self.assertEqual(pc["transitorios"]["transitorio"]["filas"] + pc["transitorios"]["estable"]["filas"], len(x))
        self.assertEqual(pc["transitorios"]["transitorio"]["filas"], int(t.sum()))

    def test_ataques_en_la_senal(self):
        fs = T.FS
        tono = 0.2 * np.sin(2 * np.pi * 440 * np.arange(2 * fs) / fs)
        golpe = np.zeros(2 * fs)
        golpe[fs:] = np.random.default_rng(1).normal(0, .2, fs)
        self.assertEqual(sum(T.es_transitorio(e, p) for _, e, p in T.frames_of(tono, 2048, True)), 0)
        self.assertEqual(sum(T.es_transitorio(e, p) for _, e, p in T.frames_of(golpe, 2048, True)), 1)
        # El primer cuadro de un trozo no tiene anterior: no cuenta como ataque.
        self.assertFalse(T.es_transitorio(1.0, None))


class ElEntrenadorLoUsa(unittest.TestCase):
    def _corre(self, d, extra_patches=()):
        salida = io.StringIO()
        with contextlib.ExitStack() as pila:
            pila.enter_context(mock.patch.object(T, "__file__", os.path.join(d, "entrenar_ia.py")))
            pila.enter_context(mock.patch.object(T.sys, "argv", ["entrenar_ia.py", "--clips", "60", "--epocas", "2"]))
            pila.enter_context(contextlib.redirect_stdout(salida))
            for p in extra_patches:
                pila.enter_context(p)
            codigo = T.main()
        return codigo, salida.getvalue()

    def test_informe_con_clases_transitorios_y_paridad(self):
        with tempfile.TemporaryDirectory() as d:
            codigo, _ = self._corre(d)
            candidato=next(n for n in os.listdir(d) if n.startswith("dsee-candidato-"))
            with open(os.path.join(d,candidato,"informe_dsee.json"), encoding="ascii") as f:
                inf = json.load(f)
            self.assertIn(codigo, (0, 2))
            self.assertTrue(inf["por_clase"]["transitorios"]["disponible"])        # sintetico: hay senal
            t = inf["por_clase"]["transitorios"]
            self.assertEqual(t["transitorio"]["filas"] + t["estable"]["filas"], inf["reparto"]["ejemplos"][2])
            self.assertLess(inf["paridad_inferencia"]["max_abs"], Q.LIMITE_PARIDAD)
            self.assertEqual(inf["paridad_extractor"]["extractor"], 2)
            self.assertIsInstance(inf["avisos_por_clase"], list)

    def test_si_la_rocola_no_calcularia_lo_mismo_no_se_publica(self):
        with tempfile.TemporaryDirectory() as d:
            mala = {"filas": 10, "max_abs": 0.5, "no_finitas": 0, "limite": Q.LIMITE_PARIDAD}
            codigo, texto = self._corre(d, (mock.patch.object(Q, "decide", return_value=(True, ["forzado"])),
                                            mock.patch.object(Q, "paridad_inferencia", return_value=mala)))
            self.assertEqual(codigo, 2)
            self.assertIn("no calcularia lo mismo", texto)
            self.assertFalse(os.path.exists(os.path.join(d, "rocola_ia.bin")))


class AvanceAlComprobar(unittest.TestCase):
    def test_avisa_mientras_lee_el_material(self):
        with tempfile.TemporaryDirectory() as d:
            datos, idx = os.path.join(d, "rocola_ia_datos.bin"), os.path.join(d, "rocola_ia_datos.idx")
            rng = np.random.default_rng(0)
            with open(datos, "wb") as fd, open(idx, "wb") as fx:
                fd.write(b"RIAD" + struct.pack("<iii", 1, 28, 12))
                fx.write(O.cabecera_idx())
                for i in range(50):
                    crudo = rng.normal(0, 1, (40, 40)).astype("<f4").tobytes()
                    fd.write(crudo)
                    fx.write(O.entrada_bytes(i * 40, 40, 2, 44100, 2048, 0, zlib.crc32(crudo) & 0xFFFFFFFF, i + 1, 0, 0))
            vistos = []
            ent, inf = O.lee_indice(idx, datos, avance=lambda a, b: vistos.append((a, b)), cada=64000)
            self.assertEqual(inf["validas"], 50)
            self.assertGreaterEqual(len(vistos), 4)
            self.assertTrue(all(a <= b for a, b in vistos))
            self.assertEqual([a for a, _ in vistos], sorted(a for a, _ in vistos))


if __name__ == "__main__":
    unittest.main()
