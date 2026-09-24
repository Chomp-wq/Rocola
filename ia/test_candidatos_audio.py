"""Candidatos aislados: no sobrescritura, particiones y barreras de publicacion."""
import contextlib
import io
import json
import os
import tempfile
import unittest
from unittest import mock
import numpy as np
import calidad_dsee as C
import origen_dsee as O
import preparar_candidato_dsee as D
import entrenar_eq_candidato as Q
from test_origen_dsee import _escribe


def guardar(ruta, b):
    with open(ruta, "wb") as f:
        f.write(b)


def leer(ruta):
    with open(ruta, "rb") as f:
        return f.read()


def modelo(ruta, ni, no):
    guardar(ruta, C.bytes_ria1(C.RedRia1([np.zeros((4, ni)), np.zeros((no, 4))],
                                      [np.zeros(4), np.zeros(no)])))


class DseeAislado(unittest.TestCase):
    def datos(self, d):
        datos, idx, _ = _escribe(d, [("%d.mp3" % n, "album/%d" % n, 100) for n in range(30)], sin_origen_tras=2)
        instalado = os.path.join(d, "instalado.bin")
        modelo(instalado, 28, 12)
        return datos, idx, instalado

    def test_instantanea_cerrada_sin_legacy_y_sin_tocar_fuentes(self):
        with tempfile.TemporaryDirectory() as d:
            datos, idx, instalado = self.datos(d)
            antes = [leer(p) for p in (datos, idx, instalado)]
            destino = os.path.join(d, "candidato")
            inf = D.preparar(datos, instalado, destino)
            self.assertFalse(inf["activado"])
            self.assertEqual(inf["ejemplos"], 3000)
            copia = os.path.join(destino, "datos.bin")
            self.assertEqual(os.path.getsize(copia), 16 + 3000 * 160)
            ent, informe = O.lee_indice(O.ruta_indice(copia), copia)
            self.assertEqual(informe["validas"], 30)
            self.assertEqual(list(ent["primero"]), list(range(0, 3000, 100)))
            self.assertEqual(antes, [leer(p) for p in (datos, idx, instalado)])
            with self.assertRaises(FileExistsError):
                D.preparar(datos, instalado, destino)
            self.assertFalse(os.path.exists(os.path.join(destino, "rocola_ia.bin")))

    def test_presupuesto_y_cabecera_no_crean_destino(self):
        with tempfile.TemporaryDirectory() as d:
            datos, idx, instalado = self.datos(d)
            destino = os.path.join(d, "candidato")
            with self.assertRaises(ValueError):
                D.preparar(datos, instalado, destino, max_mb=.1)
            self.assertFalse(os.path.exists(destino))
            guardar(datos, b"mal")
            with self.assertRaises(ValueError):
                D.preparar(datos, instalado, destino)
            self.assertFalse(os.path.exists(destino))

    def test_crc_malo_se_excluye(self):
        with tempfile.TemporaryDirectory() as d:
            datos, idx, instalado = self.datos(d)
            b = bytearray(leer(datos)); b[17] ^= 1; guardar(datos, b)
            inf = D.preparar(datos, instalado, os.path.join(d, "candidato"))
            self.assertEqual(inf["indice"]["crc_datos_malo"], 1)
            self.assertEqual(inf["ejemplos"], 2900)


class EqAislado(unittest.TestCase):
    def texto(self):
        return "".join("0;%s;%s;%s;%s;%s;0;0;20000|D:/musica/album%d/tema%d.mp3\n" %
                       (i / 1000., i / 500., i / 800., i / 900., i / 1200., i // 20, i) for i in range(2400))

    def test_albumes_no_cruzan_y_particion_es_repetible(self):
        d, grupos, _ = Q.leer_agrupado(self.texto())
        partes = Q.particiones(grupos)
        self.assertEqual(sum(map(len, partes)), len(d))
        for p, p2 in zip(partes, Q.particiones(grupos)):
            np.testing.assert_array_equal(p, p2)
        for a, b in ((0, 1), (0, 2), (1, 2)):
            self.assertFalse(set(grupos[partes[a]]) & set(grupos[partes[b]]))

    def test_copias_exactas_unen_albumes_ruta_repetida_no_duplica(self):
        linea = "0;1;2;3;4;5;6;7;20000|"
        d, g, _ = Q.leer_agrupado(linea + "D:/a/uno.mp3\n" + linea + "D:/b/dos.mp3\n" + linea + "d:/A/UNO.MP3\n")
        self.assertEqual(len(d), 2)
        self.assertEqual(g[0], g[1])
        with self.assertRaises(ValueError):
            Q.particiones(g)

    def test_invalido_y_corte_bajo_no_se_usan(self):
        for s in ("nan;1;2;3;4;5;6;7;20000|D:/a/x.mp3", "0;1;2;3;4;5;6;7;18000|D:/a/x.mp3", "0;1;2;3;4;5;6;7;20000|x.mp3"):
            with self.assertRaises(ValueError):
                Q.leer_agrupado(s)

    def test_politica_rechaza_empate_peor_banda_y_no_finito(self):
        def decide(r):
            return Q.decide({"candidato": r, "instalado": [1, 1, 1], "promedio_entrenamiento": [2, 2, 2]})
        self.assertTrue(decide([.9, .9, .9]))
        for r in ([1, 1, 1], [.1, .1, 1.2], [float("nan"), .1, .1]):
            self.assertFalse(decide(r))

    def test_entrenamiento_real_corto_rechazado_no_publica(self):
        with tempfile.TemporaryDirectory() as d:
            datos, instalado, destino = [os.path.join(d, n) for n in ("datos.txt", "modelo.bin", "candidato")]
            guardar(datos, self.texto().encode("utf-8")); modelo(instalado, 5, 3)
            antes = leer(instalado)
            with contextlib.redirect_stdout(io.StringIO()):
                inf = Q.entrenar(datos, instalado, destino, epocas=1)
            self.assertFalse(inf["aceptado_numericamente"])
            self.assertFalse(inf["activado"])
            self.assertFalse(os.path.exists(os.path.join(destino, "candidato_eq.bin")))
            self.assertEqual(leer(instalado), antes)
            with open(os.path.join(destino, "informe_eq.json")) as f:
                self.assertEqual(json.load(f)["filas"], inf["filas"])
            with self.assertRaises(FileExistsError):
                Q.entrenar(datos, instalado, destino, epocas=1)

    def test_exportacion_aislada_y_vectores_del_contrato(self):
        # Aisla la ruta de exportacion; NO afirma que esta red de juguete mejore.
        with tempfile.TemporaryDirectory() as d:
            datos, instalado, destino = [os.path.join(d, n) for n in ("datos.txt", "modelo.bin", "candidato")]
            guardar(datos, self.texto().encode("utf-8")); modelo(instalado, 5, 3)
            with contextlib.redirect_stdout(io.StringIO()), mock.patch.object(Q, "decide", return_value=True):
                inf = Q.entrenar(datos, instalado, destino, epocas=1)
            self.assertTrue(inf["aceptado_numericamente"])
            candidato = C.cargar_ria1(os.path.join(destino, "candidato_eq.bin"), 5, 3)
            v = np.loadtxt(os.path.join(destino, "vectores_eq.txt"))
            pred, malas = C.prediccion_como_la_rocola(candidato, v[:, :5])
            self.assertFalse(malas.any())
            np.testing.assert_allclose(pred, v[:, 5:], rtol=1e-6, atol=1e-6)
            self.assertFalse(inf["activado"])


if __name__ == "__main__":
    unittest.main()
