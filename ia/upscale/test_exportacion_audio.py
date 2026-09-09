"""Regresiones del exportador: finitos, margen, dinamica, dither y silencio."""
import os
import tempfile
import unittest
import numpy as np
import util_audio as audio


class ExportacionTests(unittest.TestCase):
    def test_margen_compartido_no_rompe_rms_ab(self):
        t = np.arange(10000) * .071
        x = (.8*np.sin(t))[:, None]
        y = np.zeros_like(x); y[::10] = 1.8
        y = audio.igualar_rms(y, x)
        gain = min(audio.ganancia_wav(x), audio.ganancia_wav(y))
        with tempfile.TemporaryDirectory() as directory:
            a = os.path.join(directory, "a.wav"); b = os.path.join(directory, "b.wav")
            audio.escribir_wav(a, x*gain); audio.escribir_wav(b, y*gain)
            xa, _ = audio.leer_wav(a); xb, _ = audio.leer_wav(b)
        self.assertLess(abs(np.sqrt(np.mean(xa*xa))/np.sqrt(np.mean(xb*xb))-1), 1e-4)

    def test_margen_ganancia_comun_y_entrada_intacta(self):
        t = np.arange(10000) * .071
        x = np.stack((1.5*np.sin(t), -.75*np.sin(t)), axis=1)
        before = x.copy()
        with tempfile.TemporaryDirectory() as directory:
            path = os.path.join(directory, "audio.wav")
            gain = audio.escribir_wav(path, x)
            y, fs = audio.leer_wav(path)
        self.assertEqual(fs, 44100)
        self.assertEqual(y.shape, x.shape)
        self.assertLessEqual(np.max(np.abs(y)), .98005)
        self.assertLess(np.max(np.abs(y - x*gain)), 1.6/32768)
        self.assertLess(np.max(np.abs(y[:, 0]+2*y[:, 1])), 4.5/32768)
        self.assertLess(gain, 1)
        np.testing.assert_array_equal(before, x)

    def test_silencio_vacio_y_reproducibilidad(self):
        with tempfile.TemporaryDirectory() as directory:
            for n in (0, 1, 100, 65540):
                path = os.path.join(directory, "audio.wav")
                self.assertEqual(audio.escribir_wav(path, np.zeros((n, 2))), 1)
                y, _ = audio.leer_wav(path)
                np.testing.assert_array_equal(y, np.zeros((n, 2)))
            x = np.full((100000, 2), 1e-6)
            path2 = os.path.join(directory, "otro.wav")
            audio.escribir_wav(path, x)
            audio.escribir_wav(path2, x)
            with open(path, "rb") as a, open(path2, "rb") as b:
                self.assertEqual(a.read(), b.read())
            y, _ = audio.leer_wav(path)
            self.assertGreater(np.count_nonzero(y), 1000)
            self.assertLess(abs(float(y.mean()) - 1e-6), 2e-7)

    def test_dither_no_depende_del_bloque(self):
        x = np.full((100000, 2), .1)
        # Las parejas consecutivas del RNG no cambian al cruzar bloques.
        with tempfile.TemporaryDirectory() as directory:
            path = os.path.join(directory, "audio.wav")
            audio.escribir_wav(path, x)
            y, _ = audio.leer_wav(path)
        rng = np.random.RandomState(0x524941)
        pairs = rng.random_sample((x.size, 2))
        d = (pairs[:, 0]-pairs[:, 1]).reshape(x.shape)
        np.testing.assert_array_equal(y, np.floor(x*32768+d+.5)/32768)

    def test_invalido_no_trunca_destino(self):
        with tempfile.TemporaryDirectory() as directory:
            path = os.path.join(directory, "audio.wav")
            with open(path, "wb") as f:
                f.write(b"preservar")
            for value in (np.nan, np.inf, -np.inf):
                with self.assertRaises(ValueError):
                    audio.escribir_wav(path, np.full((100, 2), value))
                with open(path, "rb") as f:
                    self.assertEqual(f.read(), b"preservar")


if __name__ == "__main__":
    unittest.main()
