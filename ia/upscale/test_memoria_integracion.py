"""FFmpeg real y procesador determinista de prueba; no requiere pesos ni GPU."""
import importlib.util
import json
import os
import shutil
import sys
import tempfile
import unittest
from unittest import mock

import numpy as np
import memoria_audio
import modelo_apollo
from util_audio import escribir_wav_float


def cargar(name, relative):
    spec = importlib.util.spec_from_file_location(name, os.path.join(os.path.dirname(__file__), relative))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


daemon = cargar("daemon_memoria_integracion", "../tiempo_real/daemon_ia.py")
offline = cargar("offline_memoria_integracion", "05_procesar_todo.py")


@unittest.skipUnless(shutil.which("ffmpeg") and shutil.which("ffprobe"), "requiere FFmpeg/ffprobe")
class IntegracionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.music = os.path.join(self.tmp.name, "musica")
        os.mkdir(self.music)
        self.src = os.path.join(self.music, "tema.wav")
        t = np.arange(88200) / 44100.
        signal = (.1 * np.sin(t * 2 * np.pi * 523) + .08 * np.sin(t * 2 * np.pi * 5003)
                  + .08 * np.sin(t * 2 * np.pi * 7331))
        escribir_wav_float(self.src, np.stack((signal, -signal), axis=1))
        self.pipeline = "f" * 64
        def proc(x):
            return x * .93 + .013 * np.roll(x, 3, axis=0)
        proc.model_fingerprint = self.pipeline
        self.proc = proc

    def run_daemon(self, low, proc=None):
        cache = os.path.join(self.tmp.name, "cache" + str(low))
        pend, listos = daemon._dirs(cache)
        h = daemon.hash_trabajo(self.src, "70", self.pipeline)
        job = os.path.join(pend, h + ".job")
        with open(job, "w") as f:
            json.dump(dict(src=self.src, mezcla="70", mezcla_key="70", pipeline=self.pipeline, cache_key=h), f)
        with mock.patch.object(memoria_audio, "memoria_disponible", return_value=0 if low else 16 * 1024**3):
            result = daemon.procesar_uno(job, listos, proc or self.proc, shutil.which("ffmpeg"),
                                        self.pipeline, shutil.which("ffprobe"), seg=.75)
        self.assertFalse(any(name.startswith("rocola_audio_") for _, dirs, _ in os.walk(cache) for name in dirs))
        return result, job, listos

    def test_motor_ram_y_mapa_publican_wav_identico(self):
        results = []
        for low in (False, True):
            result, job, _ = self.run_daemon(low)
            self.assertIsInstance(result, str)
            self.assertTrue(result.endswith(".wav"))
            self.assertFalse(os.path.exists(job))
            with open(result, "rb") as f:
                results.append(f.read())
        self.assertEqual(results[0], results[1])

    def test_error_no_publica_audio_incompleto(self):
        def falla(x):
            raise RuntimeError("fallo del modelo simulado")
        result, job, listos = self.run_daemon(True, falla)
        self.assertIs(result, daemon.DEFERIDO)
        self.assertTrue(os.path.exists(job))
        self.assertFalse(any(name.endswith(".wav") for name in os.listdir(listos)))
        self.assertTrue(any(name.endswith(".error") for name in os.listdir(listos)))

    def test_herramienta_offline_ram_y_mapa(self):
        outputs = []
        for low in (False, True):
            dst = os.path.join(self.tmp.name, "salida" + str(low))
            args = ["procesar", "--musica", self.music, "--salida", dst, "--formato", "wav",
                    "--seg", ".75", "--mezcla", "70", "--csv", os.path.join(self.tmp.name, "sin.csv")]
            with mock.patch.object(sys, "argv", args), \
                 mock.patch.object(modelo_apollo, "resolver_modelo", return_value={}), \
                 mock.patch.object(modelo_apollo, "identidad_modelo", return_value=self.pipeline), \
                 mock.patch.object(modelo_apollo, "cargar", return_value=self.proc), \
                 mock.patch.object(memoria_audio, "memoria_disponible", return_value=0 if low else 16 * 1024**3):
                self.assertEqual(offline.main(), 0)
            with open(os.path.join(dst, "tema.wav"), "rb") as f:
                outputs.append(f.read())
            lock = offline._candado_salida(dst)
            self.assertIsNotNone(lock, "el conversor dejo la salida bloqueada")
            lock.close()
        self.assertEqual(outputs[0], outputs[1])


if __name__ == "__main__":
    unittest.main()
