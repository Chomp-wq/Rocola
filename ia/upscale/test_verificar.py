"""Extrae solo la prueba pura: no ejecutar el verificador ni importar Torch/HF."""
import ast
import os
import unittest
import numpy as np

with open(os.path.join(os.path.dirname(__file__), "00_verificar.py"), encoding="utf-8-sig") as f:
    tree = ast.parse(f.read())
node = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == "prueba_inferencia")
scope = {}
exec(compile(ast.Module(body=[node], type_ignores=[]), "00_verificar.py", "exec"), scope)
prueba = scope["prueba_inferencia"]


class InferenciaTests(unittest.TestCase):
    def test_entrada_real_no_usa_el_atajo_silencioso(self):
        seen = []
        def proc(x):
            seen.append(x.copy())
            return x
        self.assertTrue(prueba(proc))
        self.assertEqual(len(seen), 1)
        self.assertEqual(seen[0].shape, (44100, 2))
        self.assertFalse(np.all(seen[0] == 0))
        self.assertFalse(np.array_equal(seen[0][:, 0], seen[0][:, 1]))

    def test_rechaza_resultados_invalidos(self):
        for produce in (lambda x: None, lambda x: x[:-1], lambda x: x[:, :1],
                        lambda x: np.zeros_like(x), lambda x: np.full_like(x, np.nan),
                        lambda x: np.full_like(x, np.inf), lambda x: x.astype(complex)):
            with self.subTest(produce=produce):
                self.assertFalse(prueba(produce))

    def test_no_oculta_fallo_del_modelo(self):
        def fallo(x):
            raise RuntimeError("fallo real")
        with self.assertRaisesRegex(RuntimeError, "fallo real"):
            prueba(fallo)


if __name__ == "__main__":
    unittest.main()
