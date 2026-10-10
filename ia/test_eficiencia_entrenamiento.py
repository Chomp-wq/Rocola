"""Pruebas aisladas: mismo lote/Adam y presupuesto escalable sin modelos reales."""
import contextlib
import io
import os
import tempfile
import unittest
from unittest import mock

from presupuesto_entrenamiento import configura_blas, hilos_entrenamiento, procesadores_disponibles
import presupuesto_entrenamiento as P
import entrenar_ia as E
import numpy as np


class MapaContado:
    def __init__(self, datos):
        self.datos, self.lecturas = datos, 0
    def __getitem__(self, key):
        self.lecturas += 1
        return self.datos[key]


class EficienciaEntrenamiento(unittest.TestCase):
    def test_lotes_misma_lectura_orden_valores_y_disposicion(self):
        rng = np.random.default_rng(912)
        datos = rng.normal(size=(110, 40)).astype(np.float32)
        mapa = MapaContado(datos)
        filas = np.arange(97)[::-1]
        x = E.FilasMapeadas(mapa, filas, 0, 28)
        y = E.FilasMapeadas(mapa, filas.copy(), 28, 40)
        media, escala = rng.normal(size=28), rng.uniform(.4, 2, size=28)
        for entradas in (x, E.FilasNormalizadas(x, media, escala)):
            for posiciones in (slice(2, 37), slice(None, None, -2), np.array([29, 5, 29, 2]),
                               np.arange(97) % 3 == 0, 3, np.array([], dtype=np.int64)):
                anterior_x, anterior_y = entradas[posiciones], y[posiciones]
                mapa.lecturas = 0
                nuevo_x, nuevo_y = E.leer_lote(entradas, y, posiciones)
                self.assertEqual(mapa.lecturas, 1)
                np.testing.assert_array_equal(nuevo_x, anterior_x)
                np.testing.assert_array_equal(nuevo_y, anterior_y)
                self.assertTrue(nuevo_x.flags.c_contiguous)
                self.assertTrue(nuevo_y.flags.c_contiguous)

    def test_no_empareja_indices_distintos_ni_mapas_ajenos(self):
        datos = np.arange(4000, dtype=np.float32).reshape(100, 40)
        a, b = MapaContado(datos), MapaContado(datos + 1)
        x = E.FilasMapeadas(a, np.arange(90), 0, 28)
        for y in (E.FilasMapeadas(a, np.arange(90)[::-1], 28, 40),
                  E.FilasMapeadas(b, np.arange(90), 28, 40)):
            esperado = x[:12], y[:12]
            obtenido = E.leer_lote(x, y, slice(None, 12))
            for viejo, nuevo in zip(esperado, obtenido):
                np.testing.assert_array_equal(viejo, nuevo)

    def test_memmap_entrena_exactamente_con_misma_semilla_orden_y_operaciones(self):
        rng = np.random.default_rng(724)
        with tempfile.TemporaryDirectory(prefix="rocola_lote_test_") as carpeta:
            archivo = os.path.join(carpeta, "fixture.bin")
            datos = rng.normal(size=(121, 40)).astype(np.float32)
            datos.tofile(archivo)
            mapa = np.memmap(archivo, mode="r", dtype=np.float32, shape=datos.shape)
            filas = rng.permutation(121)
            x = E.FilasMapeadas(mapa, filas[:93], 0, 28)
            y = E.FilasMapeadas(mapa, filas[:93].copy(), 28, 40)
            xv = E.FilasMapeadas(mapa, filas[93:], 0, 28)
            yv = E.FilasMapeadas(mapa, filas[93:].copy(), 28, 40)
            mu, sd = E.normalizacion_dsee(x, lote=17)
            xn, xvn = E.FilasNormalizadas(x, mu, sd), E.FilasNormalizadas(xv, mu, sd)
            for objetivo in ("mse", "envolvente"):
                viejo = E.Mlp([28, 16, 12], np.random.default_rng(12))
                nuevo = E.Mlp([28, 16, 12], np.random.default_rng(12))
                with contextlib.redirect_stdout(io.StringIO()):
                    with mock.patch.object(E, "leer_lote", side_effect=lambda X, Y, p: (X[p], Y[p])):
                        E.train(viejo, xn, y, xvn, yv, epochs=4, batch=23, seed=51, objetivo=objetivo)
                    E.train(nuevo, xn, y, xvn, yv, epochs=4, batch=23, seed=51, objetivo=objetivo)
                for a, b in zip(viejo.W + viejo.B, nuevo.W + nuevo.B):
                    np.testing.assert_array_equal(a, b)
                self.assertEqual(viejo.mejor_epoca, nuevo.mejor_epoca)
                self.assertEqual(viejo.mejor_mse_validacion, nuevo.mejor_mse_validacion)
            mapa._mmap.close()  # Windows exige cerrar la vista antes de limpiar la fixture.
            del mapa

    def test_presupuesto_afinidad_escala_y_repeticion(self):
        self.assertEqual([hilos_entrenamiento(n) for n in (1, 2, 3, 4, 8, 32)], [1, 1, 2, 3, 4, 16])
        with mock.patch.object(P.os, "sched_getaffinity", return_value={2, 7}, create=True):
            self.assertEqual(procesadores_disponibles(), 2)
        with mock.patch.dict(os.environ, {}, clear=True), mock.patch.object(P, "procesadores_disponibles", return_value=32):
            self.assertEqual(configura_blas(), 16)
            self.assertEqual(os.environ["OPENBLAS_NUM_THREADS"], "16")
            os.environ["ROCOLA_HILOS_ENTRENAMIENTO"] = "1"
            self.assertEqual(configura_blas(), 1)
            os.environ["ROCOLA_HILOS_ENTRENAMIENTO"] = "32"
            self.assertEqual(configura_blas(), 32)
            os.environ["ROCOLA_HILOS_ENTRENAMIENTO"] = "33"
            with self.assertRaises(ValueError):
                configura_blas()


if __name__ == "__main__":
    unittest.main()
