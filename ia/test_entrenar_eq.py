"""El error del entrenamiento del EQ, sumado por lotes: mismo numero y mismo modelo (sin torch)."""
import tracemalloc
import unittest
import numpy as np

import entrenar_eq


def _entero(model, X, Y):
    """Como se hacia antes: el conjunto ENTERO de una vez."""
    return float(((model.predict(X) - Y) ** 2).mean())


class ErrorPorLotesTests(unittest.TestCase):
    @staticmethod
    def _modelo(rng, entradas=28, salidas=8, oculta=32):
        return entrenar_eq.Mlp([entradas, oculta, salidas], rng)

    def test_mismo_numero_que_de_una(self):
        rng = np.random.default_rng(5)
        model = self._modelo(rng)
        for n in (1, 7, 256, 257, 5000):
            X = rng.normal(0, 1, (n, 28))
            Y = rng.normal(0, 1, (n, 8))
            for lote in (1, 16, 256, 4096):
                de_una = _entero(model, X, Y)
                por_lotes = entrenar_eq.error_medio(model, X, Y, lote)
                # No se promete byte a byte: sumar en trozos no da el mismo ultimo bit. Se exige que la
                # diferencia sea del orden del redondeo, que es lo que de verdad importa.
                self.assertLess(abs(de_una - por_lotes), 1e-9 * max(1.0, abs(de_una)) + 1e-12,
                                "n=%d lote=%d: %r vs %r" % (n, lote, de_una, por_lotes))

    def test_conjunto_vacio_no_revienta(self):
        rng = np.random.default_rng(6)
        model = self._modelo(rng)
        self.assertEqual(entrenar_eq.error_medio(model, np.zeros((0, 28)), np.zeros((0, 8)), 256), 0.0)

    def test_sale_el_mismo_modelo(self):
        # Lo que de verdad importa: entrenar con la cuenta nueva tiene que dar EL MISMO modelo que
        # antes. El error solo decide cual epoca se guarda, asi que si el modelo coincide, la cuenta
        # nueva no cambio ninguna de esas decisiones.
        rng = np.random.default_rng(7)
        X = rng.normal(0, 1, (900, 28)); Y = rng.normal(0, .3, (900, 8))
        Xv = rng.normal(0, 1, (200, 28)); Yv = rng.normal(0, .3, (200, 8))

        def entrena(usar_lotes):
            real = entrenar_eq.error_medio
            if not usar_lotes:
                entrenar_eq.error_medio = lambda m, A, B, l: _entero(m, A, B)
            try:
                model = entrenar_eq.Mlp([28, 24, 8], np.random.default_rng(3))
                return entrenar_eq.entrenar(model, X, Y, Xv, Yv, epocas=25, lote=128, semilla=3)
            finally:
                entrenar_eq.error_medio = real

        a = entrena(False)
        b = entrena(True)
        for Wa, Wb in zip(a.W, b.W):
            np.testing.assert_array_equal(Wa, Wb)
        for Ba, Bb in zip(a.B, b.B):
            np.testing.assert_array_equal(Ba, Bb)

    def test_no_arma_la_pila_de_todas_las_canciones(self):
        # Se miden las dos cuentas con el mismo conjunto: la de lotes no puede pedir ni de lejos lo
        # que pide la de una sola vez, que arma entradas, capa oculta y salida de TODAS a la vez.
        rng = np.random.default_rng(8)
        model = self._modelo(rng)
        X = rng.normal(0, 1, (60000, 28)); Y = rng.normal(0, 1, (60000, 8))

        def pico(fn):
            tracemalloc.start()
            base = tracemalloc.get_traced_memory()[0]
            fn()
            p = tracemalloc.get_traced_memory()[1]
            tracemalloc.stop()
            return (p - base) / 1048576.0

        de_una = pico(lambda: _entero(model, X, Y))
        por_lotes = pico(lambda: entrenar_eq.error_medio(model, X, Y, 256))
        self.assertLess(por_lotes, de_una / 4.0,
                        "de una %.1f MB, por lotes %.1f MB" % (de_una, por_lotes))



class HuellasRepetidasTests(unittest.TestCase):
    """La rocola anade al final lo que cambia (DiarioTablas.cs): una cancion medida otra vez sale repetida en
    rocola_eq.txt. Al leerla manda la ULTIMA linea, como en la rocola, y no cuenta doble."""

    def test_manda_la_ultima_y_no_cuenta_doble(self):
        import os, tempfile
        fila = lambda v, ruta: ";".join(["%g" % v] * 12) + "|" + ruta + "\r\n"
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "rocola_eq.txt")
            with open(ruta, "w", encoding="utf-8", newline="") as f:
                f.write(fila(1, "D:\\a.mp3") + fila(2, "D:\\b.mp3") + fila(3, "D:\\A.MP3") + fila(4, "D:\\c.mp3"))
            curvas, cortes = entrenar_eq.leer_huellas(ruta)
        self.assertEqual(len(curvas), 3)                    # a (repetida, con otra caja), b y c
        self.assertEqual(list(curvas[:, 0]), [3.0, 2.0, 4.0])   # la de a es la de abajo; el orden, el de siempre
        self.assertEqual(list(cortes), [3.0, 2.0, 4.0])

    def test_sin_repetidas_sale_lo_de_siempre(self):
        import os, tempfile
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "rocola_eq.txt")
            with open(ruta, "w", encoding="utf-8", newline="") as f:
                for i in range(50):
                    f.write(";".join(["%d" % (i + b) for b in range(12)]) + "|D:\\m\\%d.mp3\r\n" % i)
            curvas, cortes = entrenar_eq.leer_huellas(ruta)
        self.assertEqual(curvas.shape, (50, 8))
        self.assertEqual([int(c[0]) for c in curvas], list(range(50)))
        self.assertEqual([int(k) for k in cortes], [i + 8 for i in range(50)])

if __name__ == "__main__":
    unittest.main()
