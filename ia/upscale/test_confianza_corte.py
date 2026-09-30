"""Confianza Apollo y cobertura; no requiere GPU ni modelos."""
import unittest
import numpy as np
import util_audio as A
import preparar_muestras as P

class ConfianzaCorte(unittest.TestCase):
    def ruido(self, corte=None, oscuro=False):
        n = A.FS * 3
        rng = np.random.default_rng(29)
        s = np.fft.rfft(rng.normal(0,.05,n))
        f = np.fft.rfftfreq(n,1/A.FS)
        if corte: s[f > corte] = 0
        if oscuro: s *= 1/(1+(f/4000)**3)
        x = np.fft.irfft(s,n)
        return np.stack((x,-x),axis=1)
    def test_corte_abrupto_y_contrafase_detectados(self):
        r = A.analizar_corte(self.ruido(12000))
        self.assertTrue(r["reconstruir"], r)
        self.assertGreaterEqual(r["confianza"],.7)
        self.assertLess(abs(r["corte_hz"]-12000),400)
        self.assertLessEqual(A.mezcla_auto(r["corte_hz"],r["confianza"]),.6)
    def test_sana_oscura_silencio_desconocido_conservados(self):
        for x in (self.ruido(),self.ruido(oscuro=True),np.zeros((44100,2)),np.zeros((10,2))):
            r = A.analizar_corte(x)
            self.assertFalse(r["reconstruir"],r)
            self.assertEqual(A.mezcla_auto(r["corte_hz"],r["confianza"]),0)
        self.assertEqual(A.mezcla_auto(12000),0)
    def test_piloto_cubre_levemente_danadas_y_sanas(self):
        filas = [(8000,"brutal"),(11000,"media"),(14000,"danada"),(17000,"leve"),(20000,"sana")]
        self.assertEqual(set(P.seleccionar(filas,5)),{x[1] for x in filas})
        self.assertEqual(P.seleccionar(filas,5),P.seleccionar(filas,5))

if __name__=="__main__": unittest.main()
