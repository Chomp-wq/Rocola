"""Publicacion conjunta del DSEE: o el conjunto viejo o el nuevo, nunca una mezcla."""
import contextlib
import io
import os
import tempfile
import unittest
from unittest import mock

import numpy as np

import calidad_dsee as Q
import entrenar_ia as T
import publicar_dsee as P


class Apagon(Exception):
    pass


def _conjunto(semilla):
    m = T.Mlp([28, 6, 12], np.random.default_rng(semilla))
    Q.cuantizar(m)
    modelo = Q.bytes_ria1(m)
    x = np.random.default_rng(semilla + 100).normal(0, 1, (30, 28))
    vec = T.vectores_de_paridad(m, x, modelo)
    return {"rocola_ia.bin": modelo, "ia_vectores_prueba.txt": vec.encode("ascii"),
            "informe_dsee.json": b'{"semilla": %d}\n' % semilla}


def _lee(p):
    with open(p, "rb") as f:
        return f.read()


class Publicacion(unittest.TestCase):
    def _coherente(self, base, destinos):
        """Las copias son EXACTAMENTE la version activa, y la version esta entera."""
        version, _ = P._activo(base)
        self.assertIsNotNone(version)
        d = os.path.join(base, P.CARPETA, version)
        self.assertTrue(P.verifica_version(d)[0])
        for destino in (base,) + tuple(destinos):
            for n in P.DERIVADOS:
                self.assertEqual(_lee(os.path.join(destino, n)), _lee(os.path.join(d, n)), "%s en %s" % (n, destino))
        self.assertFalse([n for n in os.listdir(os.path.join(base, P.CARPETA)) if n.startswith(".preparando")])
        return version

    def test_apagon_en_cada_paso(self):
        # Primero, los pasos que da una publicacion normal.
        pasos = []
        with tempfile.TemporaryDirectory() as base:
            salida = os.path.join(base, "entrenado")
            va = P.publicar(base, _conjunto(1), 2, (salida,))
            with mock.patch.object(P, "_paso", side_effect=pasos.append):
                vb = P.publicar(base, _conjunto(2), 2, (salida,))
        self.assertGreater(len(pasos), 12)
        for i in ("renombra", "activa", "escribe:manifiesto.txt", "fsync:manifiesto.txt"):
            self.assertIn(i, pasos)
        vistos = {"viejo": 0, "nuevo": 0}
        for k in range(len(pasos)):
            with tempfile.TemporaryDirectory() as base:
                salida = os.path.join(base, "entrenado")
                va = P.publicar(base, _conjunto(1), 2, (salida,))
                cuenta = [0]

                def corta(nombre, k=k):
                    if cuenta[0] == k:
                        raise Apagon(nombre)
                    cuenta[0] += 1
                with mock.patch.object(P, "_paso", side_effect=corta):
                    with self.assertRaises(Apagon):
                        P.publicar(base, _conjunto(2), 2, (salida,))
                # Vuelve la luz: lo primero que hace cualquier entrenamiento o publicacion.
                P.recuperar(base, (salida,))
                v = self._coherente(base, (salida,))
                self.assertIn(v, (va, vb))
                vistos["viejo" if v == va else "nuevo"] += 1
                # Hasta que el puntero ACTIVO se reemplaza, manda el viejo; desde ese instante, el nuevo.
                decide = pasos.index("reemplaza:" + P.ACTIVO)
                self.assertEqual(v, va if k <= decide else vb, "corte en el paso %d (%s)" % (k, pasos[k]))
        self.assertGreater(vistos["viejo"], 0)
        self.assertGreater(vistos["nuevo"], 0)       # cortes durante las copias: gana el nuevo, ya activado

    def test_la_primera_vez_archiva_lo_que_habia(self):
        with tempfile.TemporaryDirectory() as base:
            with open(os.path.join(base, "rocola_ia.bin"), "wb") as f:
                f.write(b"el modelo que ya tenia el dueno")
            with open(os.path.join(base, "ia_vectores_prueba.txt"), "wb") as f:
                f.write(b"sus vectores\n")
            nueva = P.publicar(base, _conjunto(3), 2)
            previas = [n for n in os.listdir(os.path.join(base, P.CARPETA)) if n.startswith("previo-")]
            self.assertEqual(len(previas), 1)
            self.assertEqual(_lee(os.path.join(base, P.CARPETA, previas[0], "rocola_ia.bin")), b"el modelo que ya tenia el dueno")
            self.assertIn(previas[0], P._historial(base))
            self.assertEqual(self._coherente(base, ()), nueva)
            P.activar(base, previas[0])                       # volver atras
            self.assertEqual(_lee(os.path.join(base, "rocola_ia.bin")), b"el modelo que ya tenia el dueno")
            self.assertEqual(_lee(os.path.join(base, "ia_vectores_prueba.txt")), b"sus vectores\n")

    def test_version_alterada_o_truncada_no_se_usa(self):
        with tempfile.TemporaryDirectory() as base:
            va = P.publicar(base, _conjunto(4), 2)
            vb = P.publicar(base, _conjunto(5), 2)
            ruta = os.path.join(base, P.CARPETA, vb, "rocola_ia.bin")
            datos = _lee(ruta)
            with open(ruta, "wb") as f:
                f.write(datos[:-7])                            # truncado
            e = P.recuperar(base)
            self.assertIn("danada", e["aviso"])
            self.assertEqual(self._coherente(base, ()), va)    # vuelve a la ultima sana
            with self.assertRaises(ValueError):
                P.activar(base, vb)                            # y no deja activar la rota
            # Una version a medio preparar (sin manifiesto) nunca se activa y se limpia.
            prep = os.path.join(base, P.CARPETA, ".preparando-abc")
            os.makedirs(prep)
            with open(os.path.join(prep, "rocola_ia.bin"), "wb") as f:
                f.write(b"RIA1 a medias")
            self.assertEqual(P.recuperar(base)["limpiadas"], 1)
            self.assertEqual(self._coherente(base, ()), va)

    def test_manifiesto_alterado_se_detecta(self):
        man = P.manifiesto({"a": b"1", "b": b"22"}, 2)
        self.assertEqual(P.lee_manifiesto(man)[0], {"a": (P._sha(b"1"), 1), "b": (P._sha(b"22"), 2)})
        for malo in (man[:-5], man.replace(b"extractor 2", b"extractor 3"), man + b"archivo x 1 c\n"):
            with self.assertRaises(ValueError):
                P.lee_manifiesto(malo)

    def test_copia_derivada_estropeada_se_repara(self):
        with tempfile.TemporaryDirectory() as base:
            v = P.publicar(base, _conjunto(6), 2)
            with open(os.path.join(base, "ia_vectores_prueba.txt"), "wb") as f:
                f.write(b"vectores de otro modelo\n")
            os.remove(os.path.join(base, "rocola_ia.bin"))
            self.assertEqual(P.recuperar(base)["rehechas"], 2)
            self.assertEqual(self._coherente(base, ()), v)


class DesdeElEntrenador(unittest.TestCase):
    def test_publica_por_version_y_sin_rutas_en_lo_publicado(self):
        with tempfile.TemporaryDirectory() as d:
            argv = ["entrenar_ia.py", "--clips", "60", "--epocas", "2"]
            with mock.patch.object(T, "__file__", os.path.join(d, "entrenar_ia.py")), \
                 mock.patch.object(T.sys, "argv", argv), \
                 mock.patch.object(Q, "decide", return_value=(True, ["forzado por la prueba"])), \
                 contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(T.main(), 0)
            v = P._activo(d)[0]
            self.assertTrue(v and v.startswith("dsee-"))
            carpeta = os.path.join(d, P.CARPETA, v)
            self.assertEqual(sorted(os.listdir(carpeta)), ["ia_vectores_prueba.txt", "informe_dsee.json", "manifiesto.txt", "rocola_ia.bin"])
            for n in os.listdir(carpeta):
                self.assertNotIn(d.encode("utf-8"), _lee(os.path.join(carpeta, n)), n)
            self.assertEqual(_lee(os.path.join(d, "rocola_ia.bin")), _lee(os.path.join(carpeta, "rocola_ia.bin")))


if __name__ == "__main__":
    unittest.main()
