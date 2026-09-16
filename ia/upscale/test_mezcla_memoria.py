"""Equivalencia exacta, propiedad y memoria de la mezcla optimizada (sin modelo)."""
import os
import tempfile
import tracemalloc
import unittest
import numpy as np
from util_audio import mezclar, escribir_wav_float


class MezclaMemoriaTests(unittest.TestCase):
    def test_identica_sin_mutar_entradas(self):
        rng = np.random.default_rng(429)
        for dtype in (np.float32, np.float64):
            for length in (1, 31, 4096, 44101):
                original = rng.normal(0, .15, (length, 2)).astype(dtype)
                for factor in (0, 1e-12, .3, 1, 8):
                    enhanced = rng.normal(0, .2, original.shape).astype(dtype) * factor
                    before_x, before_y = original.copy(), enhanced.copy()
                    for mix in (-.1, 0, .1, .55, .7, .999, 1, 1.1):
                        old = mezclar(enhanced, original, mix, optimizar=False)
                        new = mezclar(enhanced, original, mix, optimizar=True)
                        self.assertEqual(old.dtype, new.dtype)
                        self.assertEqual(old.tobytes(), new.tobytes())
                        np.testing.assert_array_equal(original, before_x)
                        np.testing.assert_array_equal(enhanced, before_y)

    def test_alias_strides_y_solo_lectura(self):
        source = np.random.default_rng(72).normal(0, .2, (12000, 2))
        for array in (source, source[::-1], source[::2], np.asfortranarray(source)):
            array.setflags(write=False)
            for mix in (.3, .7):
                old = mezclar(array, array, mix, optimizar=False)
                new = mezclar(array, array, mix, optimizar=True)
                self.assertEqual(old.tobytes(), new.tobytes())

    def test_exporta_mismos_bytes_float(self):
        rng = np.random.default_rng(7)
        x, y = rng.normal(0, .15, (2, 44100, 2))
        with tempfile.TemporaryDirectory() as directory:
            paths = [os.path.join(directory, name) for name in ("old.wav", "new.wav")]
            for path, flag in zip(paths, (False, True)):
                escribir_wav_float(path, mezclar(y, x, .6, optimizar=flag))
            with open(paths[0], "rb") as a, open(paths[1], "rb") as b:
                self.assertEqual(a.read(), b.read())

    def test_no_oculta_muestras_invalidas(self):
        for invalid in (np.nan, np.inf, -np.inf):
            x = np.ones((100, 2), dtype=np.float64) * .1
            y = x.copy()
            y[4, 1] = invalid
            with np.errstate(invalid="ignore", divide="ignore"):
                old = mezclar(y, x, .6, optimizar=False)
                new = mezclar(y, x, .6, optimizar=True)
            self.assertEqual(old.tobytes(), new.tobytes())

    def test_automatica_por_defecto(self):
        x, y = np.random.default_rng(19).normal(0, .2, (2, 4096, 2))
        self.assertEqual(mezclar(y, x, .6).tobytes(),
                         mezclar(y, x, .6, optimizar=True).tobytes())

    def test_pico_temporales_menor(self):
        rng = np.random.default_rng(5)
        x, y = rng.normal(0, .2, (2, 44100 * 30, 2))
        peaks = []
        for flag in (False, True):
            tracemalloc.start()
            result = mezclar(y, x, .7, optimizar=flag)
            peaks.append(tracemalloc.get_traced_memory()[1])
            tracemalloc.stop()
            del result
        print("Mezcla 30 s, temporales + salida: %.2f -> %.2f MiB" %
              (peaks[0] / 2**20, peaks[1] / 2**20))
        self.assertLess(peaks[1], peaks[0] * .8)


if __name__ == "__main__":
    unittest.main()
