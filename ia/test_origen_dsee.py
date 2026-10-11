"""Identidad de origen del material del DSEE y particiones que no mezclan canciones."""
import contextlib
import io
import os
import struct
import tempfile
import tracemalloc
import unittest
import zlib

import numpy as np

import origen_dsee as O
import entrenar_ia as T


def _lee(p):
    with open(p, "rb") as f:
        return f.read()


def _pon(p, b):
    with open(p, "wb") as f:
        f.write(b)


def _escribe(carpeta, canciones, sin_origen_tras=None, extra=None):
    """Material RIAD v1 + indice como lo escribe la rocola. canciones = [(nombre, carpeta, registros)].
    Devuelve (ruta_datos, ruta_idx, lista de (primero, cuenta, cancion_huella, carpeta_huella))."""
    datos = os.path.join(carpeta, "rocola_ia_datos.bin")
    idx = os.path.join(carpeta, "rocola_ia_datos.idx")
    rng = np.random.default_rng(len(canciones))
    reg = 0
    hechas = []
    with open(datos, "wb") as d, open(idx, "wb") as x:
        d.write(b"RIAD" + struct.pack("<iii", 1, 28, 12))
        x.write(O.cabecera_idx())
        for i, (nombre, carp, n) in enumerate(canciones):
            crudo = rng.normal(0, 1, (n, 40)).astype("<f4").tobytes()
            d.write(crudo)
            hc, hk = O.huella_cancion(nombre, 200), O.huella(carp) if carp else 0
            x.write(O.entrada_bytes(reg, n, 2, 44100, 2048, 0, zlib.crc32(crudo) & 0xFFFFFFFF, hc, hk, 0))
            hechas.append((reg, n, hc, hk))
            reg += n
            if sin_origen_tras is not None and i == sin_origen_tras:
                d.write(rng.normal(0, 1, (37, 40)).astype("<f4").tobytes())   # una rocola vieja: sin entrada
                reg += 37
        if extra:
            x.write(extra)
    return datos, idx, hechas


class Huellas(unittest.TestCase):
    def test_fnv1a_conocida_y_nunca_cero(self):
        self.assertEqual(O.huella("a"), 0xAF63DC4C8601EC8C)          # FNV-1a 64 de referencia
        self.assertEqual(O.huella("A"), O.huella("a"))                 # sin mayusculas, como la rocola
        self.assertEqual(O.huella_cancion("/x/y/01 Tema.mp3", 200.4), O.huella_cancion("D:/otra/01 tema.MP3", 199.6))
        self.assertNotEqual(O.huella_cancion("01 Tema.mp3", 200), O.huella_cancion("01 Tema.mp3", 230))


class LecturaDelIndice(unittest.TestCase):
    def test_entrenador_directo_rechaza_extractor_incompatible(self):
        with tempfile.TemporaryDirectory() as d:
            datos, idx, _ = _escribe(d, [("t.mp3", "/m", 20)])
            ent, _ = O.lee_indice(idx, datos)
            e = ent[0]
            with open(idx, "r+b") as f:
                f.seek(16)
                f.write(O.entrada_bytes(int(e["primero"]), int(e["cuenta"]), 99, int(e["rate"]),
                                        int(e["frame"]), int(e["canal"]), int(e["crc"]),
                                        int(e["cancion"]), int(e["carpeta"]), int(e["artista"])))
            antes = (_lee(datos), _lee(idx))
            for permitir_sin_origen in (False, True):
                with self.assertRaisesRegex(ValueError, "extractores incompatibles.*99"):
                    T.particiones_rocola(datos, np.zeros((20, 28), dtype=np.float32),
                                         incluir_sin_origen=permitir_sin_origen)
            self.assertEqual(antes, (_lee(datos), _lee(idx)))

    def test_lee_lo_que_escribe_y_marca_lo_que_no_tiene_origen(self):
        with tempfile.TemporaryDirectory() as d:
            datos, idx, hechas = _escribe(d, [("a.mp3", "/m/A", 10), ("b.mp3", "/m/A", 20), ("c.mp3", "/m/B", 5)], sin_origen_tras=1)
            ent, inf = O.lee_indice(idx, datos)
            self.assertEqual(list(ent["primero"]), [0, 10, 67])
            self.assertEqual(inf["registros_totales"], 72)
            self.assertEqual(inf["registros_con_origen"], 35)
            self.assertEqual(list(ent["cancion"]), [h[2] for h in hechas])

    def test_nunca_atribuye_mal(self):
        with tempfile.TemporaryDirectory() as d:
            canciones = [("s%d.mp3" % i, "/m/%d" % (i // 3), 8) for i in range(9)]
            datos, idx, _ = _escribe(d, canciones, extra=b"\x01" * 30)       # entrada cortada por un apagon
            ent, inf = O.lee_indice(idx, datos)
            self.assertEqual((inf["validas"], inf["cola_incompleta"]), (9, 1))
            b = bytearray(_lee(idx))
            b[16 + 64 * 2 + 33] ^= 1                                          # bit de la huella de la tercera
            _pon(idx, bytes(b))
            ent, inf = O.lee_indice(idx, datos)
            self.assertEqual((inf["crc_entrada_malo"], inf["validas"]), (1, 8))
            b[16 + 64 * 2 + 33] ^= 1
            _pon(idx, bytes(b))
            dd = bytearray(_lee(datos))
            dd[16 + 160 * 20 + 5] ^= 1                                        # un byte de la tercera cancion
            _pon(datos, bytes(dd))
            ent, inf = O.lee_indice(idx, datos)
            self.assertEqual(inf["crc_datos_malo"], 1)
            self.assertNotIn(16, list(ent["primero"]))
            with open(datos, "r+b") as f:
                f.truncate(16 + 160 * 60)                                      # datos recortados
            ent, inf = O.lee_indice(idx, datos, verificar_datos=False)
            self.assertEqual(inf["fuera_de_rango"], 2)
            # Una entrada falsa (registros 4-11) pisa a la de 0-7 y a la de 8-15: ninguna se puede atribuir.
            with open(idx, "r+b") as f:                                          # como la rocola: tras la ultima entera
                f.seek(16 + 64 * 9)
                f.write(O.entrada_bytes(4, 8, 2, 44100, 2048, 0, 0, 99, 0, 0))
                f.truncate()
            ent, inf = O.lee_indice(idx, datos, verificar_datos=False)
            self.assertEqual(inf["solapadas"], 3)
            self.assertFalse({0, 4, 8} & set(int(v) for v in ent["primero"]))

    def test_sin_indice_o_indice_ajeno(self):
        with tempfile.TemporaryDirectory() as d:
            datos, idx, _ = _escribe(d, [("a.mp3", "/m", 10)])
            os.remove(idx)
            ent, inf = O.lee_indice(idx, datos)
            self.assertEqual((len(ent), inf["indice"], inf["registros_totales"]), (0, False, 10))
            _pon(idx, b"otra cosa cualquiera, de mas de 16 bytes")
            ent, inf = O.lee_indice(idx, datos)
            self.assertTrue(inf["cabecera_mala"])
            self.assertEqual(len(ent), 0)


class Grupos(unittest.TestCase):
    def test_lotes_sin_ejemplos_elegidos_no_cuentan_como_grupos(self):
        pesos = np.zeros(100, dtype=np.int64)
        pesos[:3] = 1000
        parte, motivo = O.reparte(np.arange(100), pesos)
        self.assertIsNone(parte)
        self.assertIn("solo hay 3 grupos", motivo)

    def test_lotes_vacios_no_cambian_el_reparto_ni_los_minimos(self):
        etiquetas = np.arange(20, dtype=np.uint64)
        pesos = np.full(20, 400, dtype=np.int64)
        antes, motivo = O.reparte(etiquetas, pesos)
        self.assertEqual(motivo, "")
        despues, motivo = O.reparte(np.concatenate((etiquetas, [999, 1000])), np.concatenate((pesos, [0, 0])))
        self.assertEqual(motivo, "")
        np.testing.assert_array_equal(antes, despues[:20])
        np.testing.assert_array_equal(despues[20:], [-1, -1])
        for p in range(3):
            self.assertGreaterEqual(len(np.unique(etiquetas[antes == p])), (1, 3, 3)[p])

    def test_reparto_rechaza_pesos_negativos_y_formas_incompatibles(self):
        for etiquetas, pesos in (([1, 2], [1, -1]), ([1, 2], [1]), ([[1]], [[1]])):
            with self.assertRaises(ValueError):
                O.reparte(etiquetas, pesos)

    def _ent(self, filas):
        t = np.dtype([("primero", "<i8"), ("cuenta", "<i4"), ("cancion", "<u8"), ("carpeta", "<u8"), ("artista", "<u8")])
        e = np.zeros(len(filas), dtype=t)
        for i, (c, k, a, n) in enumerate(filas):
            e[i] = (0, n, c, k, a)
        return e

    def test_union_de_cancion_carpeta_y_artista(self):
        # La cancion 1 cosechada en dos carpetas (se movio la biblioteca) une las dos carpetas.
        e = self._ent([(1, 10, 0, 5), (1, 20, 0, 5), (2, 10, 0, 5), (3, 20, 0, 5), (4, 30, 0, 5), (5, 40, 7, 5), (6, 50, 7, 5)] + [(100 + i, 1000 + i, 0, 5) for i in range(20)])
        g, nivel = O.grupos(e, "artista")
        self.assertEqual(nivel, "artista")
        self.assertEqual(len({g[0], g[1], g[2], g[3]}), 1)
        self.assertNotEqual(g[0], g[4])
        self.assertEqual(g[5], g[6])                       # mismo artista, carpetas distintas
        g2, _ = O.grupos(e, "cancion")
        self.assertEqual(g2[0], g2[1])
        self.assertNotEqual(g2[0], g2[2])

    def test_si_un_grupo_se_come_todo_baja_de_nivel_y_lo_dice(self):
        e = self._ent([(i + 1, 77, 0, 10) for i in range(20)])     # toda la biblioteca en una carpeta
        g, nivel = O.grupos(e, "carpeta")
        self.assertEqual(nivel, "cancion")
        self.assertEqual(len(set(g)), 20)


class Particiones(unittest.TestCase):
    def test_ningun_grupo_en_dos_particiones_en_cientos_de_repartos(self):
        rng = np.random.default_rng(11)
        for vuelta in range(300):
            ng = int(rng.integers(10, 200))
            etiquetas = rng.integers(1, 2 ** 62, ng, dtype=np.int64).astype(np.uint64)
            elementos = etiquetas[rng.integers(0, ng, int(rng.integers(ng, 4 * ng)))]
            pesos = rng.integers(1, 400, len(elementos))
            parte, motivo = O.reparte(elementos, pesos, semilla=vuelta, min_grupos=(1, 1, 1), min_ejemplos=(1, 1, 1))
            self.assertEqual(motivo, "")
            for g in np.unique(elementos):
                self.assertEqual(len(set(parte[elementos == g])), 1)
            self.assertEqual(set(parte), {0, 1, 2})
            otra, _ = O.reparte(elementos, pesos, semilla=vuelta, min_grupos=(1, 1, 1), min_ejemplos=(1, 1, 1))
            np.testing.assert_array_equal(parte, otra)                   # determinista

    def test_pocos_grupos_no_hay_validacion_independiente(self):
        parte, motivo = O.reparte(np.array([1, 2, 3, 4], dtype=np.uint64), np.array([1000] * 4))
        self.assertIsNone(parte)
        self.assertIn("grupos", motivo)

    def test_de_punta_a_punta_con_el_material_de_la_rocola(self):
        with tempfile.TemporaryDirectory() as d:
            canciones = [("t%03d.mp3" % i, "/musica/disco%02d" % (i // 4), 60) for i in range(120)]
            datos, idx, hechas = _escribe(d, canciones, sin_origen_tras=50)
            X, Y = T.cargar_datos_rocola(datos, 0, por_indices=True)
            pos, inf = T.particiones_rocola(datos, X, "carpeta")
            self.assertTrue(inf["independiente"], inf["motivo"])
            self.assertEqual(inf["sin_origen"], 37)
            # A que carpeta pertenece cada fila usada: ninguna carpeta en dos particiones.
            ent, _ = O.lee_indice(idx, datos)
            carpeta_de_fila = {}
            for e in ent:
                for r in range(int(e["primero"]), int(e["primero"] + e["cuenta"])):
                    carpeta_de_fila[r] = int(e["carpeta"])
            vistas = []
            for p in pos:
                vistas.append({carpeta_de_fila[int(X.filas[i])] for i in p})
            self.assertFalse(vistas[0] & vistas[1] or vistas[0] & vistas[2] or vistas[1] & vistas[2])
            self.assertTrue(all(vistas))
            total = sum(len(p) for p in pos)
            self.assertEqual(total, len(X) - 37)                             # las sin origen quedan fuera
            # Sumarlas al entrenamiento es posible, pero deja de ser independiente.
            pos2, inf2 = T.particiones_rocola(datos, X, "carpeta", incluir_sin_origen=True)
            self.assertFalse(inf2["independiente"])
            self.assertEqual(len(pos2[0]), len(pos[0]) + 37)
            del X, Y  # Windows no permite borrar el temporal mientras siga mapeado.

    def test_riad_antiguo_sin_indice_no_inventa_grupos(self):
        with tempfile.TemporaryDirectory() as d:
            datos, idx, _ = _escribe(d, [("t%d.mp3" % i, "/m/%d" % i, 50) for i in range(40)])
            os.remove(idx)
            X, Y = T.cargar_datos_rocola(datos, 0, por_indices=True)
            pos, inf = T.particiones_rocola(datos, X)
            self.assertIsNone(pos)
            self.assertFalse(inf["independiente"])
            self.assertIn("sin indice", inf["motivo"])
            del X, Y

    def test_memoria_acotada(self):
        # 120.000 ejemplos (19 MB) y 3.000 canciones, tomando 1 de cada 4: el reparto no debe cargar el material.
        with tempfile.TemporaryDirectory() as d:
            canciones = [("m%05d.mp3" % i, "/b/%03d" % (i // 10), 40) for i in range(3000)]
            datos, idx, _ = _escribe(d, canciones)
            tam = os.path.getsize(datos)
            with contextlib.redirect_stdout(io.StringIO()):
                X, Y = T.cargar_datos_rocola(datos, 30000, por_indices=True)
            tracemalloc.start()
            pos, inf = T.particiones_rocola(datos, X, "carpeta")
            _, pico = tracemalloc.get_traced_memory()
            tracemalloc.stop()
            self.assertTrue(inf["independiente"], inf["motivo"])
            self.assertLess(pico, tam / 4, "el reparto uso %.1f MB para un material de %.1f MB" % (pico / 1e6, tam / 1e6))
            del X, Y

    def test_huella_de_datos_reproducible(self):
        with tempfile.TemporaryDirectory() as d:
            datos, idx, _ = _escribe(d, [("a.mp3", "/m", 10), ("b.mp3", "/m", 12)])
            h1 = O.huella_datos(datos, idx)
            self.assertEqual(h1, O.huella_datos(datos, idx))
            with open(datos, "ab") as f:
                f.write(b"\0" * 160)
            self.assertNotEqual(h1, O.huella_datos(datos, idx))


if __name__ == "__main__":
    unittest.main()
