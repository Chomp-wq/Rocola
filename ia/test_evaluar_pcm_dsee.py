"""PCM real: formato estricto, imagen estereo y smoke opcional del renderer C#."""
import json
import os
import struct
import tempfile
import unittest
from unittest import mock
import numpy as np
import evaluar_pcm_dsee as P

def pares_sinteticos(destino):
    fs=44100; n=fs*2; rng=np.random.default_rng(63)
    t=np.arange(n)/fs
    cuerpo=.04*np.sin(2*np.pi*220*t)+.03*np.sin(2*np.pi*997*t)
    ruido=rng.normal(0,.012,(n,2))
    envolvente=np.where((t%.4)<.06,1.,.25)
    referencia=(cuerpo[:,None]+ruido)*envolvente[:,None]
    degradada=[]
    f=np.fft.rfftfreq(n,1/fs)
    for c in range(2):
        s=np.fft.rfft(referencia[:,c]); s[f>15000]=0
        degradada.append(np.fft.irfft(s,n))
    degradada=np.column_stack(degradada)
    for nombre,x in (("referencia.wav",referencia),("degradada.wav",degradada)):
        P.escribir_float(os.path.join(destino,nombre),x,fs)
    manifest=os.path.join(destino,"pares.json")
    with open(manifest,"x",encoding="utf-8") as f:
        json.dump({"pares":[{"referencia":"referencia.wav","degradada":"degradada.wav",
                            "genero":"sintetico","origen":"regresion_sintetica_no_musica_real",
                            "tipo":"sintetico","degradacion_conocida":True}]},f)
    return manifest

class EvaluacionPCM(unittest.TestCase):
    def test_wav_float_metadata_finito_y_corto(self):
        with tempfile.TemporaryDirectory() as d:
            p=os.path.join(d,"x.wav")
            x=np.random.default_rng(1).normal(0,.1,(4410,2))
            P.escribir_float(p,x,44100)
            y,fs,inf=P.leer_wav(p,con_info=True)
            np.testing.assert_allclose(x,y,atol=1e-8)
            self.assertEqual(fs,44100); self.assertEqual(inf["cuadros_totales"],4410)
            self.assertFalse(inf["truncado_a_30s"])
            with self.assertRaises(ValueError): P.metricas(y[:100],y[:100],fs)
            with self.assertRaises(ValueError): P.escribir_float(os.path.join(d,"nan.wav"),np.full((100,2),np.nan),fs)

    def test_wav_rechaza_alineacion_chunks_y_no_finito(self):
        with tempfile.TemporaryDirectory() as d:
            p=os.path.join(d,"x.wav"); P.escribir_float(p,np.zeros((4410,2)),44100)
            with open(p,"rb") as f: original=f.read()
            casos=[]
            b=bytearray(original); struct.pack_into("<H",b,32,4); casos.append(b)
            b=bytearray(original); struct.pack_into("<I",b,54,0xfffffffc); casos.append(b)
            b=bytearray(original); struct.pack_into("<f",b,58,float("nan")); casos.append(b)
            b=bytearray(original); struct.pack_into("<I",b,4,len(b)+1); casos.append(b)
            for i,b in enumerate(casos):
                q=os.path.join(d,"%d.wav"%i)
                with open(q,"xb") as f: f.write(b)
                with self.assertRaises(ValueError): P.leer_wav(q)

    def test_duracion_completa_se_compara_antes_de_truncar(self):
        with tempfile.TemporaryDirectory() as d:
            for n,largo in (("r.wav",31),("d.wav",32)):
                P.escribir_float(os.path.join(d,n),np.zeros((8000*largo,1)),8000)
            manifest=os.path.join(d,"pares.json")
            with open(manifest,"x") as f: json.dump({"pares":[{"referencia":"r.wav","degradada":"d.wav"}]},f)
            with mock.patch.object(P.subprocess,"run") as renderer:
                with self.assertRaises(ValueError):
                    P.evaluar(manifest,"renderer","rocola","modelo","modelo",os.path.join(d,"salida"))
                renderer.assert_not_called()

    def test_regresion_imagen_side_y_mono_casi_nulo(self):
        rng=np.random.default_rng(8)
        ref=rng.normal(0,.1,(44100,2))
        mono=np.repeat(ref.mean(axis=1)[:,None],2,axis=1)
        installed=P.metricas(ref,ref,44100); candidato=P.metricas(mono,ref,44100)
        self.assertIn("imagen_estereo",P.regresiones(candidato,installed))
        self.assertIn("mse_side",P.regresiones(candidato,installed))
        # Referencia dual-mono no convierte side casi cero en una division infinita.
        m=P.metricas(mono,mono,44100)
        self.assertEqual(m["error_side_db"],0.)
        self.assertEqual(P.regresiones(m,m),[])

    def test_equivalencia_warm_start_float32(self):
        import entrenar_ia as T
        import calidad_dsee as C
        x=np.random.default_rng(42).normal(-1,2,(120,28)); x[:,27]=0
        mu,sd=T.normalizacion_dsee(x)
        m=T.Mlp([28,12,12],np.random.default_rng(4))
        antes=C.RedRia1([w.astype(np.float32) for w in m.W],[b.astype(np.float32) for b in m.B])
        w0=m.W[0].copy(); m.W[0]*=sd; m.B[0]+=w0@mu
        despues=C.RedRia1([w.astype(np.float32) for w in m.W],[b.astype(np.float32) for b in m.B])
        a,malas=C.prediccion_como_la_rocola(antes,x)
        b,malas2=C.prediccion_como_la_rocola(despues,(x-mu)/sd)
        self.assertFalse(malas.any() or malas2.any())
        np.testing.assert_allclose(a,b,atol=2e-5,rtol=2e-5)

    @unittest.skipUnless(all(os.environ.get(n) for n in ("ROCOLA_PCM_RENDERER","ROCOLA_PCM_ASSEMBLY","ROCOLA_PCM_MODEL")),
                         "Renderer C# opt-in, sin tocar biblioteca")
    def test_renderer_real_mismo_modelo_es_smoke_no_mejora(self):
        with tempfile.TemporaryDirectory() as d:
            manifest=pares_sinteticos(d)
            rep=P.evaluar(manifest,os.environ["ROCOLA_PCM_RENDERER"],os.environ["ROCOLA_PCM_ASSEMBLY"],
                          os.environ["ROCOLA_PCM_MODEL"],os.environ["ROCOLA_PCM_MODEL"],os.path.join(d,"ab"))
            self.assertFalse(rep["supera_politica_pcm"])
            self.assertEqual(rep["bloqueos"],[])
            self.assertEqual(rep["pares"][0]["metricas"]["candidato"],rep["pares"][0]["metricas"]["instalado"])
            for n in ("original","candidato","instalado"):
                x,fs=P.leer_wav(os.path.join(d,"ab","00-"+n+".wav"))
                self.assertEqual(len(x),fs*2); self.assertLessEqual(np.max(np.abs(x)),.951)

if __name__=="__main__": unittest.main()
