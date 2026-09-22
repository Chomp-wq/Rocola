"""Preparacion estereo del DSEE: la contrafase no se anula y los canales no se separan de su cancion."""
import contextlib
import io
import os
import tempfile
import unittest
import wave

import numpy as np

import entrenar_ia as T
import origen_dsee as O


def _wav(ruta, izq, der):
    x = np.stack([izq, der], axis=1)
    pcm = np.clip(np.round(x * 32767.0), -32768, 32767).astype("<i2")
    with wave.open(ruta, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(T.FS)
        w.writeframes(pcm.tobytes())


def _brillante(rng, segundos):
    n = int(T.FS * segundos)
    s = rng.normal(0, 0.08, n)
    return s + 0.05 * np.sin(2 * np.pi * 330.0 * np.arange(n) / T.FS)


class Estereo(unittest.TestCase):
    def test_contrafase_no_se_anula(self):
        rng = np.random.default_rng(1)
        with tempfile.TemporaryDirectory() as d:
            s = _brillante(rng, 4.5)
            _wav(os.path.join(d, "contrafase.wav"), s, -s)
            clips, claves = T.load_wavs(d, np.random.default_rng(0), max_clips=10)
            # Lo que hacia la version anterior: promediar L y R antes de todo.
            mono = (clips[0].astype(np.float64) + clips[1]) / 2.0
            self.assertEqual(float(np.abs(mono).max()), 0.0)
            self.assertEqual(len(T.frames_of(mono, 2048)), 0)             # ni un cuadro que aprender
            X, Y = T.build_dataset(clips, np.random.default_rng(0))
            self.assertGreater(len(X), 100)                                  # por canal: la cancion SI tiene agudos
            self.assertEqual(len(clips), 4)                                  # 2 trozos x 2 canales
            self.assertEqual(clips[0].dtype, np.float32)
            np.testing.assert_array_equal(clips[0], -clips[1])
            self.assertEqual(len(set(claves)), 1)                            # todos del mismo archivo

    def test_los_canales_de_una_cancion_nunca_se_separan(self):
        rng = np.random.default_rng(2)
        with tempfile.TemporaryDirectory() as d:
            for i in range(36):
                s = _brillante(rng, 4.2)
                _wav(os.path.join(d, "ent_%03d.wav" % i), s, 0.6 * s + rng.normal(0, 0.03, len(s)))
            # preparar_entrenamiento.py deja la carpeta original de cada WAV: 12 "discos" de 3 canciones.
            with open(os.path.join(d, "origen.tsv"), "w", encoding="utf-8") as f:
                for i in range(36):
                    f.write("ent_%03d.wav\t%x\n" % (i, O.huella("/musica/disco%02d" % (i // 3))))
            clips, claves = T.load_wavs(d, np.random.default_rng(0), max_clips=1000)
            self.assertEqual(len(clips), 36 * 2 * 2)
            X, Y, origen = T.build_dataset(clips, np.random.default_rng(0), con_origen=True)
            with contextlib.redirect_stdout(io.StringIO()):
                pos, inf = T.particiones_por_grupo(claves, origen, "carpeta")
            self.assertTrue(inf["independiente"], inf["motivo"])
            parte_de = np.empty(len(X), dtype=int)
            for p in range(3):
                parte_de[pos[p]] = p
            archivo = np.array([claves[c][0] for c in origen], dtype=np.uint64)
            disco = np.array([claves[c][1] for c in origen], dtype=np.uint64)
            for a in np.unique(archivo):
                self.assertEqual(len(set(parte_de[archivo == a])), 1, "los canales de un archivo, separados")
            for k in np.unique(disco):
                self.assertEqual(len(set(parte_de[disco == k])), 1, "canciones del mismo disco, separadas")
            self.assertEqual(sum(len(p) for p in pos), len(X))

    def test_sintetico_cada_trozo_es_su_propio_grupo(self):
        rng = np.random.default_rng(3)
        clips = [T.synth_clip(rng) for _ in range(40)]
        X, Y, origen = T.build_dataset(clips, rng, con_origen=True)
        pos, inf = T.particiones_por_grupo([(i + 1, 0) for i in range(40)], origen, "artista")
        self.assertTrue(inf["independiente"], inf["motivo"])
        vistos = [set(origen[p]) for p in pos]
        self.assertFalse(vistos[0] & vistos[1] or vistos[0] & vistos[2] or vistos[1] & vistos[2])


if __name__ == "__main__":
    unittest.main()
