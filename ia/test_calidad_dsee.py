import contextlib
import io
import os
import tempfile
import unittest
from unittest import mock
import numpy as np
import calidad_dsee as quality
import entrenar_ia as trainer
import entrenar_eq as eq


class Constant:
    def __init__(self, value):
        self.value = np.asarray(value)
    def predict(self, x):
        return np.broadcast_to(self.value, (len(x), 12))


class CalidadDsee(unittest.TestCase):
    def data(self, n=33):
        x = np.zeros((n, 28))
        x[:, 25] = .6
        return x, np.zeros((n, 12))

    def test_guard_good_bad_tie_and_one_bad_band(self):
        x, y = self.data()
        self.assertTrue(quality.evaluar(Constant(0), x, y, 7)[0])
        self.assertFalse(quality.evaluar(Constant(20), x, y)[0])
        baseline = quality.curva_fija(x)[0]
        self.assertFalse(quality.evaluar(Constant(baseline), x, y)[0])
        bad = np.zeros(12)
        bad[0] = abs(baseline[0])*1.1
        self.assertFalse(quality.evaluar(Constant(bad), x, y)[0])
        for value in [float("nan"), float("inf"), 1e300]:
            with self.assertRaises(ValueError):
                quality.evaluar(Constant(value), x, y)
        y[:] = float("nan")
        with self.assertRaises(ValueError):
            quality.evaluar(Constant(0), x, y)

    def test_chunk_equivalence_and_reference(self):
        x, y = self.data(101)
        x[:, 25] = np.linspace(.525, .875, len(x))
        a = quality.evaluar(Constant(.25), x, y, 1)
        b = quality.evaluar(Constant(.25), x, y, 4096)
        np.testing.assert_allclose(a[1:], b[1:], rtol=1e-14)
        # Contrato EXTRACTOR=2: el techo es el topBin de la rocola (44,1 kHz, cuadro 2048), no 20 kHz.
        import contrato_dsee as contrato
        bhz = float(contrato.bin_hz(44100, 2048))
        esperado = []
        for f in x[:, 25]*20000:
            cut = int(np.floor(f / bhz + .5))
            top = contrato.top_bin(44100, 2048, cut) * bhz
            esperado.append([np.clip(-1.8*np.log2(top/f)*((j+.5)/12), -4, .1) for j in range(12)])
        np.testing.assert_allclose(quality.curva_fija(x), np.array(esperado), atol=1e-12)

    def test_invalid_shapes_cutoffs_and_empty(self):
        x, y = self.data()
        for cutoff in [0, -.1, float("nan"), 2]:
            x[:, 25] = cutoff
            with self.assertRaises(ValueError):
                quality.evaluar(Constant(0), x, y)
        with self.assertRaises(ValueError):
            quality.evaluar(Constant(0), x[:0], y[:0])
        with self.assertRaises(ValueError):
            quality.evaluar(Constant(0), x, y[:, :1])

    def test_export_atomic_and_float32_validation(self):
        m = trainer.Mlp([28, 4, 12], np.random.default_rng(5))
        with tempfile.TemporaryDirectory() as folder:
            path = os.path.join(folder, "model.bin")
            trainer.export_ria1(m, path)
            with open(path, "rb") as f:
                previous = f.read()
            self.assertTrue(previous.startswith(b"RIA1"))
            m.W[0][0, 0] = 1e100
            with self.assertRaises(ValueError):
                trainer.export_ria1(m, path)
            def broken(f):
                f.write(b"partial")
                raise OSError("simulated")
            with self.assertRaises(OSError):
                quality.escribir_atomico(path, broken)
            with open(path, "rb") as f:
                self.assertEqual(f.read(), previous)
            self.assertEqual(os.listdir(folder), ["model.bin"])
        with self.assertRaises(ValueError):
            quality.cuantizar(trainer.Mlp([5, 3], np.random.default_rng(1)))

    def test_best_checkpoint_not_last(self):
        x, y = self.data(8)
        m = trainer.Mlp([28, 4, 12], np.random.default_rng(5))
        first = []
        calls = []
        def metric(model, a, b):
            calls.append(1)
            if len(calls) == 2:
                first.extend([v.copy() for v in model.W + model.B])
                return .1
            return .9
        with mock.patch.object(trainer, "mse_modelo", side_effect=metric), contextlib.redirect_stdout(io.StringIO()):
            trainer.train(m, x, y, x, y, epochs=2, batch=4)
        self.assertEqual(len(calls), 4)  # final epoch evaluated even if not multiple of ten
        for a, b in zip(first, m.W+m.B):
            np.testing.assert_array_equal(a, b)

    @staticmethod
    def independiente(n=600):
        # Un reparto por grupos como el que sale de rocola_ia_datos.idx: 400 / 100 / 100.
        pos = np.arange(n)
        return [pos[:400], pos[400:500], pos[500:]], {"independiente": True, "motivo": "", "nivel": "cancion"}

    def test_rejected_main_preserves_model_and_vectors(self):
        x, y = self.data(600)
        m = trainer.Mlp([28, 12], np.random.default_rng(2))
        m.W[0][:] = 0
        m.B[0][:] = 20
        with tempfile.TemporaryDirectory() as folder:
            names = ["rocola_ia.bin", "ia_vectores_prueba.txt"]
            for name in names:
                with open(os.path.join(folder, name), "wb") as f:
                    f.write(b"keep")
            with mock.patch.object(trainer, "__file__", os.path.join(folder, "entrenar_ia.py")), \
                 mock.patch.object(trainer.sys, "argv", ["entrenar_ia.py", "--datos", "unused"]), \
                 mock.patch.object(trainer, "cargar_datos_rocola", return_value=(x, y)), \
                 mock.patch.object(trainer, "particiones_rocola", return_value=self.independiente()), \
                 mock.patch.object(trainer, "train", return_value=m), contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(trainer.main(), 2)
            for name in names:
                with open(os.path.join(folder, name), "rb") as f:
                    self.assertEqual(f.read(), b"keep")

    def test_accepted_main_exports_quantized_pair(self):
        x, y = self.data(600)
        m = trainer.Mlp([28, 12], np.random.default_rng(3))
        m.W[0][:] = 0
        m.B[0][:] = 0
        with tempfile.TemporaryDirectory() as folder:
            with mock.patch.object(trainer, "__file__", os.path.join(folder, "entrenar_ia.py")), \
                 mock.patch.object(trainer.sys, "argv", ["entrenar_ia.py", "--datos", "unused"]), \
                 mock.patch.object(trainer, "cargar_datos_rocola", return_value=(x, y)), \
                 mock.patch.object(trainer, "particiones_rocola", return_value=self.independiente()), \
                 mock.patch.object(trainer, "train", return_value=m), contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(trainer.main(), 4)
            candidatos = [n for n in os.listdir(folder) if n.startswith("dsee-candidato-")]
            with open(os.path.join(folder, candidatos[0], "candidato.bin"), "rb") as f:
                self.assertEqual(f.read(4), b"RIA1")
            self.assertFalse(os.path.exists(os.path.join(folder, "rocola_ia.bin")))
            lines = trainer.vectores_de_paridad(m, x[-100:], quality.bytes_ria1(m)).splitlines()[1:]
            self.assertEqual(len(lines), 24)
            for line in lines:
                inputs, outputs = line.split("|")
                self.assertEqual(len(inputs.split()), 28)
                self.assertEqual([float(v) for v in outputs.split()], [0]*12)

    def test_sin_origen_no_publica_aunque_gane(self):
        # El mismo modelo que se publica arriba, pero con un rocola_ia_datos.bin sin indice de origen:
        # no se sabe de que cancion es cada ejemplo, asi que la mejora no se puede verificar.
        x, y = self.data(600)
        m = trainer.Mlp([28, 12], np.random.default_rng(3))
        m.W[0][:] = 0
        m.B[0][:] = 0
        with tempfile.TemporaryDirectory() as folder:
            names = ["rocola_ia.bin", "ia_vectores_prueba.txt"]
            for name in names:
                with open(os.path.join(folder, name), "wb") as f:
                    f.write(b"keep")
            salida = io.StringIO()
            with mock.patch.object(trainer, "__file__", os.path.join(folder, "entrenar_ia.py")), \
                 mock.patch.object(trainer.sys, "argv", ["entrenar_ia.py", "--datos", os.path.join(folder, "no.bin")]), \
                 mock.patch.object(trainer, "cargar_datos_rocola", return_value=(x, y)), \
                 mock.patch.object(trainer, "train", return_value=m), contextlib.redirect_stdout(salida):
                self.assertEqual(trainer.main(), trainer.SALIDA_NO_VERIFICABLE)
            self.assertIn("NO INDEPENDIENTE", salida.getvalue())
            for name in names:
                with open(os.path.join(folder, name), "rb") as f:
                    self.assertEqual(f.read(), b"keep")

    def test_validation_does_not_change_normalization(self):
        x = np.arange(20, dtype=float).reshape(4, 5)
        tr = np.array([0, 1, 2])
        _, mu, sd = eq.normalizar_entrenamiento(x, tr)
        x[-1] = 1e8
        _, other_mu, other_sd = eq.normalizar_entrenamiento(x, tr)
        np.testing.assert_array_equal(mu, other_mu)
        np.testing.assert_array_equal(sd, other_sd)
        with self.assertRaises(ValueError):
            eq.normalizar_entrenamiento(x, [])


if __name__ == "__main__":
    unittest.main()
