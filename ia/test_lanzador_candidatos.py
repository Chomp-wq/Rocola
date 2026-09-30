"""El lanzador solo genera candidatos, reconoce metadata y no anuncia activacion."""
import contextlib
import io
import os
import struct
import tempfile
import unittest
from unittest import mock
import entrenar_dsee as D
import entrenar_eq as E
import entrenar_eq_candidato as Q

class LanzadorCandidatos(unittest.TestCase):
    def test_eq_wrapper_siempre_delega_estricto_sin_salida_directa(self):
        argv=["entrenar_eq.py","--datos","medidas.txt","--instalado","actual.bin","--destino","aislado"]
        with mock.patch.object(E.sys,"argv",argv), mock.patch.object(Q,"entrenar",return_value={"aceptado_numericamente":True}) as q, contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(E.main(),0)
        q.assert_called_once_with("medidas.txt","actual.bin","aislado",200,False)

    def test_eq_wrapper_no_permite_forzar_publicacion(self):
        with mock.patch.object(E.sys,"argv",["entrenar_eq.py","--forzar"]), contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit): E.main()

    def test_dsee_candidato4_es_exito_metadata_aparte(self):
        with tempfile.TemporaryDirectory() as d:
            datos=os.path.join(d,"material.bin"); indice=os.path.join(d,"origen.idx")
            generos=os.path.join(d,"origen.generos.tsv")
            for p,contenido in ((datos,b"RIAD"+struct.pack("<iii",1,28,12)+b"\0"*160),
                                (indice,b"RIAX"+b"\0"*12),(generos,b"0000000000000001\trock\n")):
                with open(p,"xb") as f: f.write(contenido)
            with mock.patch.object(D,"SALIDA",os.path.join(d,"entrenado")), mock.patch.object(D,"corre",return_value=4) as c, contextlib.redirect_stdout(io.StringIO()) as out:
                self.assertEqual(D.main([datos,indice,generos]),0)
            argv=c.call_args[0][0]
            self.assertEqual(argv[argv.index("--indice")+1],indice)
            self.assertEqual(argv[argv.index("--generos")+1],generos)
            self.assertTrue(argv[argv.index("--candidato")+1].startswith(os.path.join(d,"entrenado")))
            self.assertIn("NO ACTIVADOS",out.getvalue())
            self.assertNotIn("No se genero ningun modelo",out.getvalue())
            self.assertNotIn("LO QUE NO SALIO",out.getvalue())

    def test_eq_candidato0_es_exito_no_usa_promedio_si_instalado(self):
        with tempfile.TemporaryDirectory() as d:
            datos=os.path.join(d,"rocola_eq.txt")
            with open(datos,"x",encoding="utf-8") as f: f.write("0;1;2;3;4;5;6;7;20000;0;0;2|D:/a/c.mp3\n")
            with mock.patch.object(D,"SALIDA",os.path.join(d,"entrenado")), mock.patch.object(D,"corre",return_value=0) as c, contextlib.redirect_stdout(io.StringIO()) as out:
                self.assertEqual(D.main([datos]),0)
            self.assertEqual(c.call_args[0][0][0],"entrenar_eq_candidato.py")
            self.assertIn("NO ACTIVADOS",out.getvalue())
            self.assertIn("modelo EQ instalado se conserva",out.getvalue())

    def test_rechazo_no_es_activacion_exit_no_exito(self):
        with tempfile.TemporaryDirectory() as d:
            datos=os.path.join(d,"rocola_eq.txt")
            with open(datos,"x") as f: f.write("0;1;2;3;4;5;6;7;20000;0;0;2|D:/a/c.mp3\n")
            with mock.patch.object(D,"SALIDA",os.path.join(d,"entrenado")), mock.patch.object(D,"corre",return_value=2), contextlib.redirect_stdout(io.StringIO()) as out:
                self.assertEqual(D.main([datos]),1)
            self.assertIn("instalado intacto",out.getvalue())

if __name__=="__main__": unittest.main()
