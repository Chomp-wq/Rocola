"""El contrato del extractor: que Python mide lo MISMO que la rocola, y por que antes no."""
import os
import tempfile
import unittest

import numpy as np

import contrato_dsee as C
import entrenar_ia as T

AQUI = os.path.dirname(os.path.abspath(__file__))


def _extractor_v1(mag2n, frame, fc_hz, fs=44100):
    """Copia literal del extractor Python ANTERIOR (EXTRACTOR=1), solo para demostrar la diferencia."""
    def hzb(hz):
        ny = frame // 2
        b = int(hz / (fs / frame))
        return min(max(b, 1), ny - 1)
    x = np.zeros(28)
    for i in range(24):
        a = hzb(200.0 * (fc_hz / 200.0) ** (i / 24.0)); b = hzb(200.0 * (fc_hz / 200.0) ** ((i + 1) / 24.0))
        if b <= a: b = a + 1
        x[i] = np.log10(mag2n[a:b].mean() + 1e-12)
    mean = x[:24].mean(); x[:24] -= mean; x[24] = mean; x[25] = fc_hz / 20000.0
    e = x[16:24] + mean
    x[26] = e.mean() - np.log10((10.0 ** e).mean() + 1e-12)
    ny = frame // 2
    cut = hzb(fc_hz); sa = hzb(fc_hz * 0.92)
    if sa >= cut: sa = cut - 1
    top = min(2.0 * fc_hz, 20000.0, ny * (fs / frame) * 0.95)
    seam = mag2n[sa:cut].mean() + 1e-12
    y = np.zeros(12)
    for j in range(12):
        a = hzb(fc_hz * (top / fc_hz) ** (j / 12.0)); b = hzb(fc_hz * (top / fc_hz) ** ((j + 1) / 12.0))
        if a < cut: a = cut
        if b <= a: b = a + 1
        y[j] = np.log10(mag2n[a:b].mean() + 1e-12) - np.log10(seam)
    return x, np.clip(y, -4.0, 0.5), top


class Contrato(unittest.TestCase):
    def test_vectores_de_paridad_se_regeneran_identicos(self):
        with open(os.path.join(AQUI, "paridad_extractor.txt"), encoding="ascii") as f:
            guardado = f.read()
        self.assertEqual(C.genera_paridad(), guardado,
                         "paridad_extractor.txt no corresponde al contrato actual: regeneralo a proposito")
        casos = C.lee_paridad(guardado)
        self.assertGreaterEqual(len(casos), 200)
        self.assertTrue(any(c["valido"] for c in casos) and any(not c["valido"] for c in casos))
        self.assertIn("EXTRACTOR=%d" % C.EXTRACTOR, guardado.splitlines()[0])

    def test_diferencias_con_el_extractor_v1(self):
        frame, fs = 2048, 44100
        # Plano hasta 20 kHz y nada encima: el techo decide la ultima sub-banda.
        f = np.arange(frame // 2 + 1) * fs / frame
        mag2n = np.where(f < 20000.0, 1.0, 0.0)
        x1, y1, top1 = _extractor_v1(mag2n, frame, 11000.0)
        x2, y2 = T.features_and_target(mag2n, frame, 11000.0)
        g = C.geometria(fs, frame, C.corte_de(fs, frame, 11000.0))
        self.assertEqual(top1, 20000.0)
        self.assertEqual(g["top"], 952)                         # 20,5 kHz en bins de la rocola
        self.assertAlmostEqual(y1[11], 0.0, places=6)           # v1: el techo no llega a lo vacio
        self.assertLess(y2[11], -0.2)                           # v2: si, como la rocola
        # El corte se redondea a bin: la entrada 25 es corte_bin*binHz/20000, no 11000/20000.
        self.assertAlmostEqual(x1[25], 0.55, places=9)
        self.assertAlmostEqual(x2[25], g["cut"] * float(C.bin_hz(fs, frame)) / 20000.0, places=6)
        self.assertNotAlmostEqual(x2[25], 0.55, places=4)
        # Tope superior: +0,1 como la rocola, no +0,5.
        fuerte = np.where(f < 11000.0, 1.0, 50.0)
        _, yv1, _ = _extractor_v1(fuerte, frame, 11000.0)
        _, yv2 = T.features_and_target(fuerte, frame, 11000.0)
        self.assertAlmostEqual(yv1.max(), 0.5, places=6)
        self.assertAlmostEqual(yv2.max(), 0.1, places=6)

    def test_invariantes_de_toda_la_geometria(self):
        validos = 0
        for rate in (22050, 32000, 44100, 48000, 88200, 96000):
            for frame in (2048, 4096):
                ny = frame // 2
                for cut in range(60, ny):
                    g = C.geometria(rate, frame, cut)
                    if not g["valido"]:
                        continue
                    validos += 1
                    for a, b in g["entrada"]:
                        self.assertTrue(1 <= a < b <= ny)
                    sa, c = g["costura"]
                    self.assertTrue(1 <= sa < c == cut)
                    prev = cut
                    for a, b in g["objetivo"]:
                        self.assertTrue(cut <= a < b <= g["top"])
                        self.assertGreaterEqual(a, prev - 1)   # bandas en orden, solapando como mucho un bin
                        prev = b
                    self.assertLessEqual(g["top"], 2 * cut - 2)   # la fuente t>>1 siempre queda bajo el corte
                    self.assertEqual(g["regen"], g["objetivo"])  # par valido: RegenNeural usa las mismas
        self.assertGreater(validos, 5000)

    def test_entrenador_usa_el_contrato(self):
        rng = np.random.default_rng(7)
        frame, fs = 2048, 44100
        sig = rng.normal(0, 0.1, frame * 3)
        m = T.frames_of(sig, frame)[0]
        X = np.fft.fft(sig[:frame] * T._ventana_entrenamiento(frame))
        for corte in (11000.0, 13000.0, 16500.0, 18000.0, 19900.0):
            a = T.features_and_target(m, frame, corte)
            b = C.par_desde_potencia(fs, frame, C.corte_de(fs, frame, corte), np.abs(X) ** 2)
            self.assertEqual(a is None, b is None)
            if a is not None:
                np.testing.assert_allclose(a[0], b[0], atol=2e-5)
                np.testing.assert_allclose(a[1], b[1], atol=2e-5)

    def test_cuadros_mudos_se_saltan_como_en_la_cosecha(self):
        frame = 2048
        sig = np.zeros(frame * 4)
        sig[frame * 2:] = 0.05
        n = len(T.frames_of(sig, frame))
        self.assertEqual(n, 3)   # de 6 cuadros con salto de 1024, solo los que tienen senal
        self.assertEqual(len(T.frames_of(np.full(frame * 3, 5e-5), frame)), 0)   # 2,5e-9 < 1e-8

    def test_comparador_delata_un_volcado_alterado(self):
        re, im = C.espectro_lcg(2048, 5)
        lineas = []
        for cut in (400, 500, 700):
            c = float(C.F32(C.F32(cut + 0.5) * C.bin_hz(44100, 2048)))
            r = C.par_desde_espectro(44100, 2048, c, re, im)
            lineas.append("P 44100 2048 %r 5 1 %s" % (c, " ".join("%r" % float(v) for v in np.concatenate(r))))
        g = C.geometria(44100, 2048, 500)
        lineas.append("R 44100 2048 500 %d %s" % (g["top"], " ".join("%d %d" % ab for ab in g["regen"])))
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "v.txt")
            with open(ruta, "w") as f:
                f.write("\n".join(lineas) + "\n")
            vistos, malos, _ = C.compara_volcado(ruta)
            self.assertEqual((vistos, malos), (4, []))
            t = lineas[1].split(); t[20] = repr(float(t[20]) + 1e-3); lineas[1] = " ".join(t)
            t = lineas[3].split(); t[7] = str(int(t[7]) + 1); lineas[3] = " ".join(t)
            with open(ruta, "w") as f:
                f.write("\n".join(lineas) + "\n")
            _, malos, _ = C.compara_volcado(ruta)
            self.assertEqual(len(malos), 2)

    def test_margen_de_redondeo_medido(self):
        # Cuantos limites de banda cambiarian si la division se hiciera sin redondear a float32 (lo que
        # puede ocurrir con la FPU x87 de un XP de 32 bits). Es una MEDIDA del riesgo, no una prueba en XP.
        total, solo_cociente, todo, _ = C.margen_redondeo(rates=(44100,), frames=(2048,))
        self.assertGreater(total, 10000)
        self.assertLessEqual(solo_cociente, todo)
        self.assertLess(todo / float(total), 0.02)


if __name__ == "__main__":
    unittest.main()
