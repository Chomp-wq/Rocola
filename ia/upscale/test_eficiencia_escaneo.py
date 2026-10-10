import contextlib
import io
import sys
import tempfile
import unittest
from concurrent.futures import Future, wait as real_wait, ThreadPoolExecutor as real_executor
from unittest import mock
import numpy as np

import escanear_calidad as S


class EscaneoEficiencia(unittest.TestCase):
    def test_futuros_acotados_y_ninguna_cancion_perdida(self):
        recibidas, pendientes = [], set()
        maximo = [0]
        class Executor:
            def submit(self, funcion, ruta):
                f = Future()
                try:
                    f.set_result(funcion(ruta))
                except Exception as e:
                    f.set_exception(e)
                pendientes.add(f)
                maximo[0] = max(maximo[0], len(pendientes))
                return f
        def espera(fs, **kw):
            resultado = real_wait(fs, **kw)
            pendientes.difference_update(resultado[0])
            return resultado
        for hilos in (1, 4, 16):
            recibidas.clear(); maximo[0] = 0
            with mock.patch.object(S, "wait", side_effect=espera):
                S.trabajos_acotados(Executor(), recibidas.append, range(10000), hilos)
            self.assertEqual(recibidas, list(range(10000)))
            self.assertLessEqual(maximo[0], hilos * 2)
            self.assertEqual(len(pendientes), 0)
        with mock.patch.object(S, "wait", side_effect=espera):
            with self.assertRaisesRegex(ValueError, "fixture"):
                S.trabajos_acotados(Executor(), lambda _: (_ for _ in ()).throw(ValueError("fixture")), [1], 1)

    def test_ffmpeg_no_anida_hilos_y_prioridad_baja(self):
        with mock.patch.object(S, "_duracion", return_value=60), mock.patch.object(S.subprocess, "run") as run, \
                mock.patch.object(S, "leer_wav", return_value=(np.zeros(50000), 44100)), \
                mock.patch.object(S, "acantilado_hz", return_value=13000):
            self.assertEqual(S.medir("ffmpeg", "ffprobe", "fixture.mp3", None), 13000)
        comando = run.call_args.args[0]
        self.assertEqual(comando[comando.index("-threads") + 1], "1")
        self.assertEqual(comando[comando.index("-filter_threads") + 1], "1")
        self.assertEqual(run.call_args.kwargs["creationflags"], S.FONDO)

    def test_default_dos_hilos_y_equipo_potente_sin_tope_global(self):
        for disponibles, esperado in ((2, 1), (32, 16)):
            with tempfile.TemporaryDirectory(prefix="rocola_scan_test_") as carpeta:
                with mock.patch.object(sys, "argv", ["scan", "--musica", carpeta]), \
                        mock.patch.object(S, "__file__", carpeta + "/scan.py"), \
                        mock.patch.object(S, "procesadores_disponibles", return_value=disponibles), \
                        mock.patch.object(S.shutil, "which", side_effect=lambda nombre: nombre), \
                        mock.patch.object(S, "_herramienta_funciona", return_value=True), \
                        mock.patch.object(S, "_escanear", return_value=["fixture.mp3"]), \
                        mock.patch.object(S, "remuestreador", return_value=("fixture", None)), \
                        mock.patch.object(S, "medir", return_value=13000), \
                        mock.patch.object(S, "ThreadPoolExecutor", wraps=real_executor) as executor, \
                        contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(S.main(), 0)
                executor.assert_called_once_with(max_workers=esperado)


if __name__ == "__main__":
    unittest.main()
