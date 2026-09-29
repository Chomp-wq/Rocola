import unittest
from unittest import mock

import presupuesto_cpu as p


class PresupuestoCpu(unittest.TestCase):
    def test_margen_y_escalado(self):
        self.assertEqual([p.hilos_audio(n) for n in (1, 2, 3, 4, 8, 16, 32)], [1, 1, 2, 3, 4, 8, 16])
        for n in range(2, 129):
            self.assertGreaterEqual(p.hilos_audio(n), 1)
            self.assertLess(p.hilos_audio(n), n)

    def test_afinidad_antes_que_maquina(self):
        with mock.patch.object(p.os, "sched_getaffinity", return_value={3, 5}, create=True), mock.patch.object(p.os, "cpu_count", return_value=32):
            self.assertEqual(p.procesadores_disponibles(), 2)

    def test_respaldo_sin_afinidad(self):
        with mock.patch.object(p.os, "sched_getaffinity", side_effect=OSError(), create=True), mock.patch.object(p.os, "name", "posix"), mock.patch.object(p.os, "cpu_count", return_value=None):
            self.assertEqual(p.procesadores_disponibles(), 1)

    def test_publica_al_motor_una_vez(self):
        torch = mock.Mock()
        with mock.patch.object(p, "procesadores_disponibles", return_value=2):
            self.assertEqual(p.aplica_presupuesto(torch), 1)
        torch.set_num_threads.assert_called_once_with(1)
        torch.set_num_interop_threads.assert_called_once_with(1)
        self.assertEqual(len(torch.mock_calls), 2)

    def test_runtime_ya_iniciado_no_rompe_carga(self):
        torch = mock.Mock()
        torch.set_num_interop_threads.side_effect = RuntimeError("already started")
        with mock.patch.object(p, "procesadores_disponibles", return_value=16):
            self.assertEqual(p.aplica_presupuesto(torch), 8)
        torch.set_num_threads.assert_called_once_with(8)


if __name__ == "__main__":
    unittest.main()
