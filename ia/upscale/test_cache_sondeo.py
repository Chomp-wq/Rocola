"""Cache de JSON del motor: sin checkpoint ni inferencia."""
import importlib.util
import json
import os
import tempfile
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location(
    "daemon_cache_test", os.path.join(os.path.dirname(__file__), "..", "tiempo_real", "daemon_ia.py"))
daemon = importlib.util.module_from_spec(spec)
spec.loader.exec_module(daemon)


class CacheSondeoTests(unittest.TestCase):
    def setUp(self):
        daemon._JSON_MEM.clear()
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.path = os.path.join(self.tmp.name, "pedido.job")

    def write(self, value):
        with open(self.path, "w", encoding="utf-8") as f:
            json.dump(value, f)

    def test_reutiliza_sin_reabrir_y_no_comparte_objetos(self):
        self.write({"src": ["uno"]})
        value = daemon._lee_json_recordado(self.path)
        value["src"].append("ajeno")
        with mock.patch("builtins.open", side_effect=AssertionError("reabierto")):
            self.assertEqual(daemon._lee_json_recordado(self.path), {"src": ["uno"]})

    def test_reemplazo_atomico_y_borrado(self):
        self.write({"src": "uno"})
        daemon._lee_json_recordado(self.path)
        other = self.path + ".tmp"
        with open(other, "w") as f:
            json.dump({"src": "otra fuente"}, f)
        os.replace(other, self.path)
        self.assertEqual(daemon._lee_json_recordado(self.path)["src"], "otra fuente")
        os.remove(self.path)
        with self.assertRaises(FileNotFoundError):
            daemon._lee_json_recordado(self.path)
        self.assertFalse(daemon._JSON_MEM)

    def test_caducidad_y_errores_no_ocultos(self):
        self.write({"ok": 1})
        with mock.patch.object(daemon.time, "monotonic", return_value=10):
            daemon._lee_json_recordado(self.path)
        with mock.patch.object(daemon.time, "monotonic", return_value=13):
            with mock.patch("builtins.open", side_effect=PermissionError):
                with self.assertRaises(PermissionError):
                    daemon._lee_json_recordado(self.path)
        self.assertFalse(daemon._JSON_MEM)

    def test_limite_lru_y_documentos_grandes(self):
        for i in range(daemon._JSON_MEM_TOPE + 9):
            self.path = os.path.join(self.tmp.name, str(i) + ".job")
            self.write({"src": str(i)})
            daemon._lee_json_recordado(self.path)
        self.assertEqual(len(daemon._JSON_MEM), daemon._JSON_MEM_TOPE)
        self.assertFalse(any(k[0].endswith(os.sep + "0.job") for k in daemon._JSON_MEM))
        self.write({"large": "x" * (65 * 1024)})
        daemon._lee_json_recordado(self.path)
        self.assertFalse(any(k[0] == self.path for k in daemon._JSON_MEM))


if __name__ == "__main__":
    unittest.main()
