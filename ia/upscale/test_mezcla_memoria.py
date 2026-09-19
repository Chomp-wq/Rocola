"""Equivalencia exacta, propiedad y memoria de la mezcla optimizada (sin modelo)."""
import os
import tempfile
import tracemalloc
import unittest
import numpy as np
from util_audio import mezclar, escribir_wav_float, por_trozos


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


def _por_trozos_como_antes(x, proc, fs=44100, seg=10.0, sol=0.5):
    """EL ALGORITMO DE ANTES, escrito aparte y a proposito.

    por_trozos ya no guarda un mapa de pesos del largo de la cancion: se apunta solo donde los trozos
    se solapan. Para poder decir que eso NO cambia el sonido no vale con leerlo: hay que comparar
    contra el de antes. Esta copia es ese 'antes', con su wsum entero, tal cual estaba."""
    n = len(x)
    L = int(seg * fs)
    O = int(sol * fs)
    if n == 0:
        return x.copy()
    if n <= L:
        return np.asarray(proc(x))[:n]
    out = np.zeros_like(x)
    wsum = np.zeros((n, 1))
    p = 0
    while p < n:
        q = min(p + L, n)
        y = np.asarray(proc(x[p:q]))[: q - p]
        w = np.ones((q - p, 1))
        if O and p > 0:
            w[:min(O, q - p), 0] = np.linspace(0.0, 1.0, min(O, q - p))
        if O and q < n:
            w[-min(O, q - p):, 0] = np.minimum(w[-min(O, q - p):, 0], np.linspace(1.0, 0.0, min(O, q - p)))
        out[p:q] += y * w
        wsum[p:q] += w
        if q >= n:
            break
        p = q - O
    np.maximum(wsum, 1e-9, out=wsum)
    if out.dtype == np.float64:
        np.divide(out, wsum, out=out)
        return out
    return out / wsum


class PorTrozosAcotadoTests(unittest.TestCase):
    """El mapa de pesos del largo de la cancion se fue; el sonido tiene que ser EL MISMO."""

    @staticmethod
    def _proc(chunk):
        return chunk * 0.97 + 0.013 * np.roll(chunk, 3, axis=0)

    def test_identico_byte_a_byte_al_de_antes(self):
        rng = np.random.default_rng(11)
        for dtype in (np.float64, np.float32):
            for n in (1, 2, 1000, 44100, 44100 * 3 + 7):
                # Incluye a proposito un caso donde los trozos se pisan DE TRES EN TRES (0.6/0.5):
                # ahi la cuenta corta no vale y por_trozos tiene que volver al camino de siempre.
                for seg, sol in ((10.0, 0.5), (1.0, 0.25), (0.6, 0.5), (2.0, 0.0)):
                    x = rng.normal(0, .2, (n, 2)).astype(dtype)
                    esperado = _por_trozos_como_antes(x, self._proc, seg=seg, sol=sol)
                    obtenido = por_trozos(x, self._proc, seg=seg, sol=sol)
                    self.assertEqual(esperado.dtype, obtenido.dtype)
                    self.assertEqual(esperado.shape, obtenido.shape)
                    self.assertEqual(esperado.tobytes(), obtenido.tobytes(),
                                     "n=%d seg=%s sol=%s %s" % (n, seg, sol, dtype))

    @staticmethod
    def _pico_mb(fn, x):
        tracemalloc.start()
        base = tracemalloc.get_traced_memory()[0]
        y = fn(x, lambda c: c * 0.98)
        pico = tracemalloc.get_traced_memory()[1]
        tracemalloc.stop()
        del y
        return (pico - base) / 1048576.0

    def test_no_reserva_un_mapa_del_largo_de_la_cancion(self):
        # Se miden LOS DOS caminos con la misma cancion y se compara: asi el limite no depende de lo
        # que ocupen los trozos de turno ni de lo que este haciendo otra prueba al lado. Lo que tiene
        # que desaparecer es el mapa de pesos, que son 8 bytes por muestra.
        n = 60 * 44100
        x = np.zeros((n, 2), dtype=np.float64)
        x[:, 0] = np.sin(np.arange(n) * 0.001)
        pesos_mb = n * 8 / 1048576.0
        antes = self._pico_mb(_por_trozos_como_antes, x)
        ahora = self._pico_mb(por_trozos, x)
        self.assertLess(ahora, antes - pesos_mb * 0.8,
                        "antes %.0f MB, ahora %.0f MB: el mapa de pesos (%.0f MB) no desaparecio"
                        % (antes, ahora, pesos_mb))


if __name__ == "__main__":
    unittest.main()
