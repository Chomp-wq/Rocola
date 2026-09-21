"""Ruta RAM/mapeada: muestras exactas, seleccion adaptativa y limpieza de archivos."""
import os
import tempfile
import tracemalloc
import unittest
from unittest import mock

import numpy as np
import memoria_audio as memoria
from util_audio import leer_wav, por_trozos, mezclar, escribir_wav_float, escribir_wav, acantilado_hz


class MemoriaTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dec = os.path.join(self.tmp.name, "dec.wav")

    def forced(self):
        return mock.patch.object(memoria, "memoria_disponible", return_value=0)

    def test_seleccion_no_frena_equipo_con_ram(self):
        self.assertTrue(memoria.necesita_mapa(100 * memoria.MIB, 1024 * memoria.MIB))
        self.assertFalse(memoria.necesita_mapa(100 * memoria.MIB, 16 * 1024 * memoria.MIB))
        self.assertTrue(memoria.necesita_mapa(100 * memoria.MIB, None))
        self.assertFalse(memoria.necesita_mapa(memoria.MIB, None))
        disponible = memoria.memoria_disponible()
        self.assertTrue(disponible is None or disponible > 0)

    def test_ram_no_crea_archivos(self):
        escribir_wav_float(self.dec, np.ones((321, 2)) * .1)
        with mock.patch.object(memoria, "memoria_disponible", return_value=16 * 1024 * memoria.MIB):
            with memoria.MemoriaAudio(self.dec) as work:
                self.assertFalse(work.mapeada)
                x, _ = leer_wav(self.dec, reservar=work.crear)
                y = por_trozos(x, lambda c: c * .8, reservar=work.crear)
                self.assertEqual(work.mezclar(y, x, .6).tobytes(), mezclar(y, x, .6).tobytes())
                self.assertIsNone(work.directory)

    def test_muestras_y_exportacion_exactas(self):
        rng = np.random.default_rng(812)
        for ch in (1, 2):
            for n in (0, 1, 41, 1001, 33333, 100003):
                source = rng.normal(0, .23, (n, ch))
                if ch == 2:
                    source[:, 1] = -source[:, 0]  # contrafase
                escribir_wav_float(self.dec, source)
                original, _ = leer_wav(self.dec)
                for seg, sol in ((1., .25), (.6, .5), (2., 0.)):
                    proc = lambda c: c * .97 + .013 * np.roll(c, 3, axis=0)
                    y = por_trozos(original, proc, fs=1000, seg=seg, sol=sol)
                    for m in (0, .55, 1):
                        expected = mezclar(y, original, m) if n else original.copy()
                        with self.forced(), memoria.MemoriaAudio(self.dec) as work:
                            x, _ = leer_wav(self.dec, reservar=work.crear)
                            np.testing.assert_array_equal(x, original)
                            self.assertEqual(acantilado_hz(x), acantilado_hz(original))
                            enhanced = por_trozos(x, proc, fs=1000, seg=seg, sol=sol, reservar=work.crear)
                            self.assertEqual(enhanced.tobytes(), y.tobytes())
                            result = work.mezclar(enhanced, x, m)
                            self.assertEqual(result.tobytes(), expected.tobytes(), (ch, n, seg, sol, m))
                            self.assertEqual(x.tobytes(), original.tobytes())
                            for writer in (escribir_wav_float, escribir_wav):
                                a, b = [os.path.join(self.tmp.name, name) for name in ("a.wav", "b.wav")]
                                writer(a, expected); writer(b, result)
                                with open(a, "rb") as fa, open(b, "rb") as fb:
                                    self.assertEqual(fa.read(), fb.read())
                        self.assertFalse(any(p.startswith("rocola_audio_") for p in os.listdir(self.tmp.name)))

    def test_silencio_casi_mudo_y_trozo_fortran(self):
        x = np.random.default_rng(4).normal(0, .2, (1003, 2))
        escribir_wav_float(self.dec, x)
        for factor in (0, 1e-12, 8):
            for fortran in (False, True):
                y = x * factor
                if fortran:
                    y = np.asfortranarray(y)
                for m in (.1, .6, .999, 1):
                    with self.forced(), memoria.MemoriaAudio(self.dec) as work:
                        self.assertEqual(work.mezclar(y, x, m).tobytes(), mezclar(y, x, m).tobytes())

    def test_error_modelo_y_falta_espacio_limpian(self):
        escribir_wav_float(self.dec, np.ones((10000, 2)) * .1)
        with self.assertRaisesRegex(RuntimeError, "modelo"):
            with self.forced(), memoria.MemoriaAudio(self.dec) as work:
                x, _ = leer_wav(self.dec, reservar=work.crear)
                def falla(c):
                    raise RuntimeError("modelo")
                por_trozos(x, falla, fs=1000, seg=1, reservar=work.crear)
        self.assertFalse(any(p.startswith("rocola_audio_") for p in os.listdir(self.tmp.name)))
        with self.assertRaises(OSError), self.forced(), memoria.MemoriaAudio(self.dec) as work:
            with mock.patch.object(memoria.shutil, "disk_usage", return_value=mock.Mock(free=0)):
                work.crear((1000, 2))
        self.assertFalse(any(p.startswith("rocola_audio_") for p in os.listdir(self.tmp.name)))

    def test_pico_heap_no_crece_como_cancion(self):
        peaks = []
        for n in (500000, 5000000):
            escribir_wav_float(self.dec, np.ones((n, 2)) * .1)
            with self.forced(), memoria.MemoriaAudio(self.dec) as work:
                tracemalloc.start()
                try:
                    x, _ = leer_wav(self.dec, reservar=work.crear)
                    y = por_trozos(x, lambda c: c * .9, fs=44100, seg=10, reservar=work.crear)
                    result = work.mezclar(y, x, .7)
                    escribir_wav_float(os.path.join(self.tmp.name, "out.wav"), result)
                    peaks.append(tracemalloc.get_traced_memory()[1])
                finally:
                    tracemalloc.stop()
        print("Heap temporal (no RSS/VRAM), 10x muestras: %.2f -> %.2f MiB" % tuple(p / memoria.MIB for p in peaks))
        self.assertLess(peaks[1], peaks[0] + 2 * memoria.MIB)
        self.assertLess(peaks[1], 32 * memoria.MIB)


if __name__ == "__main__":
    unittest.main()
