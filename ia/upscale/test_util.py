# -*- coding: utf-8 -*-
# Prueba de las utilerias SIN GPU; compatible con ejecucion directa y unittest discover.
import os
import tempfile
import sys
import unittest

import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from util_audio import escribir_wav, leer_wav, acantilado_hz, igualar_rms, por_trozos


class UtilAudioTests(unittest.TestCase):
    def setUp(self):
        self.rng = np.random.default_rng(5)

    def test_wav_ida_vuelta(self):
        x = self.rng.uniform(-0.5, 0.5, (44100, 2))
        # Un nombre exclusivo: dos suites simultaneas no comparten ni pisan ut.wav.
        with tempfile.TemporaryDirectory() as directorio:
            p = os.path.join(directorio, 'audio.wav')
            escribir_wav(p, x)
            y, fs = leer_wav(p)
        self.assertLess(np.abs(y - x).max(), 1e-3)
        self.assertEqual(fs, 44100)

    def test_acantilado(self):
        t = np.arange(44100 * 3) / 44100.0
        lim = sum(np.sin(2 * np.pi * f * t + f) for f in range(300, 11800, 400)) * 0.02
        ful = sum(np.sin(2 * np.pi * f * t + f) for f in range(300, 20800, 400)) * 0.02
        c1 = acantilado_hz(np.stack([lim, lim], 1))
        c2 = acantilado_hz(np.stack([ful, ful], 1))
        self.assertGreater(c1, 11000)
        self.assertLess(c1, 13200)
        self.assertGreater(c2, 19000)

    def test_igualar_rms(self):
        a = self.rng.normal(0, 0.2, (44100, 2))
        b = self.rng.normal(0, 0.05, (44100, 2))
        m = igualar_rms(b, a)
        razon = np.sqrt((m ** 2).mean()) / np.sqrt((a ** 2).mean())
        self.assertLess(abs(razon - 1), 1e-6)

    def test_por_trozos(self):
        z = self.rng.uniform(-0.3, 0.3, (44100 * 7 + 123, 2))
        identidad = por_trozos(z, lambda c: c)
        escala = por_trozos(z, lambda c: c * 2.0)
        self.assertLess(np.abs(identidad - z).max(), 1e-9)
        self.assertLess(np.abs(escala - 2 * z).max(), 1e-9)


if __name__ == '__main__':
    unittest.main()
