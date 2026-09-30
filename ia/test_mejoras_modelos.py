"""Regresiones de datos y guardianes: sin tocar modelos ni biblioteca."""
import contextlib
import io
import os
import tempfile
import unittest
from unittest import mock
import numpy as np
import entrenar_ia as T
import entrenar_eq_candidato as E
import calidad_dsee as C
import origen_dsee as O

class MejorasEntrenamiento(unittest.TestCase):
    def test_informe_no_puede_pisar_modelo_datos_ni_candidato(self):
        with tempfile.TemporaryDirectory() as d:
            fuentes=[os.path.join(d,n) for n in ("instalado.bin","datos.bin","datos.idx","candidato.bin")]
            for p in fuentes:
                with open(p,"xb") as f: f.write(b"intacto")
            for p in fuentes:
                with self.assertRaises(ValueError): T.validar_ruta_informe(p,fuentes)
                with open(p,"rb") as f: self.assertEqual(f.read(),b"intacto")
            T.validar_ruta_informe(os.path.join(d,"informe.json"),fuentes)

    def test_guard_cli_pre_entrenamiento_alias_fuentes_outputs_y_existentes(self):
        with tempfile.TemporaryDirectory() as d:
            fuentes=[os.path.join(d,n) for n in ("datos.bin","datos.idx","datos.generos.tsv",
                      "actual.bin","ia_vectores_prueba.txt","paridad_extractor.txt")]
            for p in fuentes:
                with open(p,"xb") as f: f.write(b"intacto")
            candidato=os.path.join(d,"nuevo.bin"); informe=os.path.join(d,"nuevo.json")
            base=["entrenar_ia.py","--datos",fuentes[0],"--instalado",fuentes[3],
                  "--candidato",candidato,"--informe",informe]
            casos=[]
            for flag in ("--candidato","--informe"):
                for fuente in fuentes:
                    argv=base.copy(); argv[argv.index(flag)+1]=fuente; casos.append(argv)
            argv=base.copy(); argv[argv.index("--informe")+1]=candidato; casos.append(argv)
            existente=os.path.join(d,"existente.json")
            with open(existente,"xb") as f: f.write(b"informe anterior")
            argv=base.copy(); argv[argv.index("--informe")+1]=existente; casos.append(argv)
            # Hardlink: diferente nombre pero exactamente el mismo archivo fuente.
            alias=os.path.join(d,"alias.json"); os.link(fuentes[3],alias)
            argv=base.copy(); argv[argv.index("--informe")+1]=alias; casos.append(argv)
            with mock.patch.object(T,"__file__",os.path.join(d,"entrenar_ia.py")), mock.patch.object(T,"cargar_datos_rocola") as cargar:
                for argv in casos:
                    with mock.patch.object(T.sys,"argv",argv), self.assertRaises((ValueError,FileExistsError)):
                        T.main()
                cargar.assert_not_called()
            for p in fuentes:
                with open(p,"rb") as f: self.assertEqual(f.read(),b"intacto")
            with open(existente,"rb") as f: self.assertEqual(f.read(),b"informe anterior")
            self.assertFalse(os.path.exists(candidato)); self.assertFalse(os.path.exists(informe))

    def test_cinco_cortes_no_se_sincronizan_con_muestreo(self):
        for paso in (5, 10, 15):
            n, tope = paso * 10000, 10000
            indices = T.indices_estratificados(n, tope)
            np.testing.assert_array_equal(indices, T.indices_estratificados(n, tope))
            self.assertEqual(len(np.unique(indices)), tope)
            cuentas = np.bincount(indices % 5, minlength=5)
            self.assertTrue(np.all(cuentas > 1700), cuentas)
            self.assertTrue(np.all(cuentas < 2300), cuentas)
            self.assertTrue(np.all(np.diff(indices) > 0))

    def test_normalizacion_entrenamiento_por_lotes_y_capa_absorbida(self):
        x = np.random.default_rng(31).normal(0, 1, (97, 28))
        x[:, 27] = 0
        mu, sd = T.normalizacion_dsee(x, 7)
        np.testing.assert_allclose(mu, x.mean(axis=0), atol=1e-14)
        np.testing.assert_allclose(sd[:27], x.std(axis=0)[:27], atol=1e-14)
        self.assertEqual(sd[27], 1)
        m = T.Mlp([28, 6, 12], np.random.default_rng(1))
        y = m.predict((x - mu) / sd)
        m.W[0] /= sd
        m.B[0] -= m.W[0] @ mu
        np.testing.assert_allclose(m.predict(x), y, atol=1e-14)

    def test_metrica_normalizada_por_lotes_sin_corpus_materializado(self):
        x = np.arange(56 * 13, dtype=float).reshape(26, 28) / 100
        mu, sd = T.normalizacion_dsee(x[:20])
        vista = T.FilasNormalizadas(x, mu, sd)
        m = T.Mlp([28, 12], np.random.default_rng(4))
        y = np.zeros((len(x), 12))
        self.assertAlmostEqual(T.mse_modelo(m, vista, y, 3), float((m.predict((x-mu)/sd)**2).mean()), places=12)

    def test_eq_solo_version_actual_ultima_ficha_y_legacy_no_limpio(self):
        base = "0;1;2;3;4;5;6;7;20000;0;0;"
        texto = base+"1|D:/a/uno.mp3\n"+base+"2|D:/b/dos.mp3\n"+base+"3|D:/c/tres.mp3\n"
        d, g, inf = E.leer_agrupado(texto)
        self.assertEqual(len(d), 1)
        self.assertEqual(inf["excluidas_version"], 2)
        d, g, inf = E.leer_agrupado(texto, diagnostico_legacy=True)
        self.assertEqual(len(d), 1)
        self.assertEqual(inf["excluidas_version"], 2)
        with self.assertRaises(ValueError):
            E.leer_agrupado(base+"1|D:/a/uno.mp3\n")
        with self.assertRaises(ValueError):
            E.leer_agrupado(base+"2|D:/a/uno.mp3\n"+base+"1|D:/a/uno.mp3\n")

    def test_runtime_clamp_y_clases_contra_inferencia_real(self):
        x = np.zeros((600, 28)); x[:,25] = .6
        y = np.full((600, 12), .1)
        m = C.RedRia1([np.zeros((12,28))], [np.full(12, 3.)])
        raw = C.evaluar_tres(m, None, x, y)
        runtime = C.evaluar_runtime(m, None, x, y)
        self.assertGreater(raw["candidato"].mean(), 8)
        self.assertLess(runtime["candidato"].mean(), 1e-12)

    def test_genero_ultima_vacia_elimina_y_no_cambia_red(self):
        tipo = np.dtype([("cancion","<u8")])
        ent = np.array([(1,), (2,)], dtype=tipo)
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d,"datos.bin")
            with open(os.path.join(d,"datos.generos.tsv"),"w",encoding="utf-8") as f:
                f.write("0000000000000001\trock\n0000000000000001\t\n0000000000000002\tcumbia\n")
            inf = T.cobertura_generos(ruta, ent, [0,2], [10,20])
        self.assertEqual(inf["ejemplos_por_particion"][0], {"desconocido":10})
        self.assertEqual(inf["ejemplos_por_particion"][2], {"cumbia":20})

    def test_warm_start_equivalente_y_baseline_no_se_olvida(self):
        x = np.random.default_rng(40).normal(0, 1, (20,28))
        mu, sd = T.normalizacion_dsee(x)
        m = T.Mlp([28, 5, 12], np.random.default_rng(9))
        antes = m.predict(x)
        w0=m.W[0].copy(); m.W[0]*=sd; m.B[0]+=w0@mu
        np.testing.assert_allclose(m.predict((x-mu)/sd), antes, atol=1e-13)
        W, B = [w.copy() for w in m.W], [b.copy() for b in m.B]
        # Validacion empeora en todos los checkpoints: volver a epoca cero.
        with mock.patch.object(T,"mse_modelo",side_effect=[.1,.3,.4,.5,.6]), contextlib.redirect_stdout(io.StringIO()):
            T.train(m,(x-mu)/sd,np.zeros((20,12)),(x-mu)/sd,np.zeros((20,12)),epochs=2,batch=10,preservar_inicial=True)
        self.assertEqual(m.mejor_epoca,0)
        for a,b in zip(W+B,m.W+m.B): np.testing.assert_array_equal(a,b)

class MejorasModelos(unittest.TestCase):
    def test_gradiente_envolvente(self):
        rng=np.random.default_rng(83)
        p=rng.normal(-1,1,(4,12)); y=rng.normal(-2,3,p.shape);w=np.array([.5,1,2,.5])
        valor,grad=C.perdida_envolvente(p,y,w)
        self.assertTrue(np.isfinite(valor))
        for i,j in [(0,0),(2,4),(3,11)]:
            a,b=p.copy(),p.copy();a[i,j]+=1e-5;b[i,j]-=1e-5
            numerico=(C.perdida_envolvente(a,y,w)[0]-C.perdida_envolvente(b,y,w)[0])/2e-5
            self.assertAlmostEqual(numerico,grad[i,j],places=8)
    def test_equilibra_canciones_y_grupos(self):
        c=np.array([1]*20+[2]*10+[3]*10,dtype=np.uint64)
        g=np.array([11]*30+[22]*10,dtype=np.uint64)
        w,rep=O.pesos_equilibrados(c,g)
        self.assertAlmostEqual(w[c==1].sum(),w[c==2].sum())
        self.assertAlmostEqual(w[g==11].sum(),w[g==22].sum())
        self.assertAlmostEqual(w.mean(),1);self.assertEqual(rep['canciones'],3)
    def test_acota_extremos_y_conserva_desconocidos(self):
        c=np.array([1]*10000+[2,0],dtype=np.uint64)
        w,_=O.pesos_equilibrados(c)
        self.assertGreaterEqual(w.min(),.25);self.assertLessEqual(w.max(),4)
        self.assertAlmostEqual(w.mean(),1,places=10);self.assertEqual(w[-1],1)
        with self.assertRaises(ValueError):O.pesos_equilibrados(np.array([1,1]),np.array([2,3]))
    def test_inicial_compite_y_determinismo(self):
        rng=np.random.default_rng(4);x=rng.normal(0,.1,(40,28))
        inicial=T.Mlp([28,12],np.random.default_rng(5));y=inicial.predict(x)
        resultados=[]
        for _ in range(2):
            m=T.Mlp([28,12],np.random.default_rng(5))
            with contextlib.redirect_stdout(io.StringIO()):
                T.train(m,x,y,x,y,epochs=2,batch=13,preservar_inicial=True,objetivo='envolvente',pesos=np.ones(40))
            self.assertEqual(m.mejor_epoca,0)
            np.testing.assert_array_equal(m.W[0],inicial.W[0]);resultados.append(m.B[0])
        np.testing.assert_array_equal(*resultados)
    def test_equilibrio_no_consulta_targets(self):
        # La API solo recibe identidades TRAIN; no existe acceso a X/Y/VAL/TEST.
        c=np.array([8,8,9],dtype=np.uint64)
        w,_=O.pesos_equilibrados(c)
        np.testing.assert_array_equal(w,O.pesos_equilibrados(c.copy())[0])

if __name__=='__main__':unittest.main()
