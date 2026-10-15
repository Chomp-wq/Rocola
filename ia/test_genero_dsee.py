"""I3: el DSEE por genero. El nombre de archivo de cada genero (el mismo que calcula la rocola), el material que
entra al entrenar un genero y el instalador que solo deja pasar candidatos aceptados, escuchados y de ese genero."""
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import unittest

import numpy as np

import entrenar_ia as T
import instalar_dsee_genero as I
from contrato_dsee import EXTRACTOR

# Los mismos ejemplos que pruebas/mejorasia.cs le pasa a Form1.SlugGenero: si una de las dos cuentas cambia, falla.
EJEMPLOS = [("Cumbia", "cumbia"), ("  Regional Mexicano ", "regional_mexicano"), ("Rock & Roll", "rock_roll"),
            ("Música Tropical", "musica_tropical"), ("Baño de Pop", "bano_de_pop"), ("Unknown", ""),
            ("desconocido", ""), ("", ""), ("80's", "80_s"), ("Salsa/Merengue", "salsa_merengue"),
            ("__x__", "x"), ("a" * 60, "a" * 40)]


class Slug(unittest.TestCase):
    def test_como_la_rocola(self):
        for etiqueta, esperado in EJEMPLOS:
            self.assertEqual(T.slug_genero(etiqueta), esperado, etiqueta)


class SoloGenero(unittest.TestCase):
    def test_minimos_del_genero_no_se_saltan_con_miles_de_ejemplos(self):
        partes = [np.arange(2000), np.arange(2000, 4000), np.arange(4000, 6000)]
        informe = {"independiente": True, "grupos": [1, 1, 1]}
        self.assertIn("validacion", T.valida_material_genero(partes, informe))
        self.assertIn("minimo 3 y 200", T.valida_material_genero(partes, informe))
        # Los grupos siguen separados: rechazar por diversidad no debe llamarlo contaminacion.
        self.assertTrue(informe["independiente"])

    def test_minimos_en_cada_particion_y_limites_inclusivos(self):
        from origen_dsee import MIN_GRUPOS, MIN_EJEMPLOS
        partes = [np.arange(n) for n in MIN_EJEMPLOS]
        informe = {"independiente": True, "grupos": list(MIN_GRUPOS)}
        self.assertEqual(T.valida_material_genero(partes, informe), "")
        for p in range(3):
            poca_diversidad = dict(informe, grupos=list(MIN_GRUPOS))
            poca_diversidad["grupos"][p] -= 1
            self.assertTrue(T.valida_material_genero(partes, poca_diversidad))
            pocas_filas = list(partes)
            pocas_filas[p] = pocas_filas[p][:-1]
            self.assertTrue(T.valida_material_genero(pocas_filas, informe))
        self.assertTrue(T.valida_material_genero(partes, dict(informe, independiente=False)))

    def test_cli_rechaza_antes_de_entrenar_o_guardar(self):
        from unittest import mock
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "material.bin")
            candidato = os.path.join(d, "candidato.bin")
            partes = [np.arange(0, 2000), np.arange(2000, 4000), np.arange(4000, 6000)]
            canciones = np.repeat(np.arange(1, 4, dtype=np.uint64), 2000)
            informe = {"independiente": True, "_canciones": canciones, "_grupos": canciones.copy()}
            with mock.patch.object(sys, "argv", ["entrenar_ia.py", "--datos", ruta, "--genero", "cumbia"]), \
                    mock.patch.object(T, "rutas_candidato", return_value=("no.bin", candidato, os.path.join(d, "informe.json"))), \
                    mock.patch.object(T, "cargar_datos_rocola", return_value=(np.zeros((6000, 28)), np.zeros((6000, 12)))), \
                    mock.patch.object(T, "particiones_rocola", return_value=(partes, informe)), \
                    mock.patch.object(T, "lee_generos", return_value={1: "cumbia", 2: "cumbia", 3: "cumbia"}), \
                    mock.patch.object(T, "train") as entrena, mock.patch("builtins.print"):
                self.assertEqual(T.main(), 1)
                entrena.assert_not_called()
            self.assertFalse(os.path.exists(candidato))
            self.assertFalse(os.path.exists(os.path.join(d, "informe.json")))

    def test_deja_solo_las_canciones_del_genero_sin_moverlas_de_particion(self):
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "rocola_ia_datos.bin")
            canciones = np.array([11, 11, 22, 33, 33, 44, 55, 55, 66], dtype=np.uint64)
            with open(os.path.join(d, "rocola_ia_datos.generos.tsv"), "w", encoding="utf-8") as f:
                for c, g in ((11, "Cumbia"), (22, "rock"), (33, "cumbia "), (44, "Cumbia"), (55, "salsa"), (44, "salsa")):
                    f.write("\n%016x\t%s\n" % (c, g))
            informe = {"_canciones": canciones, "_grupos": canciones, "motivo": ""}
            partes = [np.array([0, 1, 2, 5]), np.array([3, 4]), np.array([6, 7, 8])]
            nuevas, inf = T.solo_genero(partes, informe, ruta, None, "CUMBIA")
            self.assertEqual([list(p) for p in nuevas], [[0, 1], [3, 4], []])   # 44 paso a salsa: el ultimo gana
            self.assertEqual(inf["genero"], "cumbia")
            self.assertEqual(inf["grupos"], [1, 1, 0])

    def test_sin_sidecar_o_sin_origen_no_entrena(self):
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "rocola_ia_datos.bin")
            inf = {"_canciones": np.zeros(3, dtype=np.uint64), "motivo": "x"}
            with self.assertRaises(SystemExit):
                T.solo_genero([np.arange(3)] * 3, inf, ruta, None, "cumbia")
            with self.assertRaises(SystemExit):
                T.solo_genero(None, inf, ruta, None, "cumbia")
            with self.assertRaises(SystemExit):
                T.solo_genero([np.arange(3)] * 3, inf, ruta, None, "unknown")

    def test_genero_sin_material_de_la_rocola_se_rechaza(self):
        with tempfile.TemporaryDirectory() as d:
            r = subprocess.run([sys.executable, "entrenar_ia.py", "--genero", "cumbia", "--clips", "4",
                                "--candidato", os.path.join(d, "c.bin")], capture_output=True, text=True)
            self.assertNotEqual(r.returncode, 0)
            self.assertIn("--genero solo vale con --datos", r.stderr + r.stdout)
            self.assertFalse(os.path.exists(os.path.join(d, "c.bin")))


def _candidato(d, genero="cumbia", aceptado=True, extractor=EXTRACTOR, sha=None):
    os.makedirs(d, exist_ok=True)
    cand = os.path.join(d, "candidato.bin")
    with open(cand, "wb") as f:
        f.write(b"RIA1" + os.urandom(64))
    rep = {"formato": "ROCOLA-INFORME-DSEE 1", "extractor": extractor, "contrato_ria1": {"entradas": 28, "salidas": 12},
           "decision": {"aceptado_numericamente": aceptado, "motivos": ["prueba"]},
           "datos": {"tipo": "rocola", "genero": genero} if genero is not None else {"tipo": "rocola"},
           "candidato_sha256": sha or hashlib.sha256(open(cand, "rb").read()).hexdigest()}
    with open(os.path.join(d, "informe_dsee.json"), "w", encoding="ascii") as f:
        json.dump(rep, f)
    return cand


class Instalador(unittest.TestCase):
    def test_instala_solo_lo_aceptado_escuchado_y_de_un_genero(self):
        with tempfile.TemporaryDirectory() as d:
            destino = os.path.join(d, "datos"); os.makedirs(destino)
            general = os.path.join(destino, "rocola_ia.bin")
            with open(general, "wb") as f:
                f.write(b"general")
            casos = [(_candidato(os.path.join(d, "a"), aceptado=False), "no lo acepto"),
                     (_candidato(os.path.join(d, "b"), genero=None), "no es de un genero"),
                     (_candidato(os.path.join(d, "c"), extractor=EXTRACTOR + 1), "otro extractor"),
                     (_candidato(os.path.join(d, "e"), sha="0" * 64), "huella no coincide")]
            for cand, motivo in casos:
                with self.assertRaisesRegex(ValueError, motivo):
                    I.instala(cand, destino, escuchado=True)
            bueno = _candidato(os.path.join(d, "f"))
            with self.assertRaisesRegex(ValueError, "escuchado"):
                I.instala(bueno, destino, escuchado=False)
            self.assertEqual(sorted(os.listdir(destino)), ["rocola_ia.bin"])   # nada se escribio
            final = I.instala(bueno, destino, escuchado=True)
            self.assertEqual(os.path.basename(final), "rocola_ia_genero_cumbia.bin")
            self.assertEqual(os.path.dirname(final), os.path.join(destino, "IAS"))   # en la carpeta IAS (14-10-2026)
            self.assertEqual(open(final, "rb").read(), open(bueno, "rb").read())
            otro = _candidato(os.path.join(d, "g"))
            I.instala(otro, destino, escuchado=True)
            self.assertEqual(open(final, "rb").read(), open(otro, "rb").read())
            self.assertEqual(open(final + ".anterior-1", "rb").read(), open(bueno, "rb").read())   # el de antes, guardado
            self.assertEqual(open(general, "rb").read(), b"general")   # el general, intacto
            self.assertFalse(any(n.endswith(".instalando") for n in os.listdir(os.path.join(destino, "IAS"))))

    def test_va_a_IAS_y_aparta_el_suelto_de_antes(self):
        with tempfile.TemporaryDirectory() as d:
            destino = os.path.join(d, "datos"); os.makedirs(destino)
            suelto = os.path.join(destino, "rocola_ia_genero_cumbia.bin")   # instalado antes de la carpeta IAS
            with open(suelto, "wb") as f:
                f.write(b"de antes")
            final = I.instala(_candidato(os.path.join(d, "f")), destino, escuchado=True)
            self.assertEqual(final, os.path.join(destino, "IAS", "rocola_ia_genero_cumbia.bin"))
            self.assertFalse(os.path.exists(suelto))   # si quedara suelto, la rocola lo tomaria por el recien puesto
            self.assertEqual(open(final + ".anterior-1", "rb").read(), b"de antes")
            # dando la carpeta IAS misma, no se anida otra IAS dentro
            final2 = I.instala(_candidato(os.path.join(d, "g")), os.path.join(destino, "IAS"), escuchado=True)
            self.assertEqual(final2, final)
            self.assertTrue(os.path.exists(final + ".anterior-2"))
            self.assertFalse(os.path.exists(os.path.join(destino, "IAS", "IAS")))


class ConDobleClic(unittest.TestCase):
    """Sin argumentos (doble clic) antes imprimia la ayuda y la ventana se cerraba en el acto. Ahora busca los modelos,
    pregunta y espera a que se lea."""
    def _respuestas(self, *r):
        cola = list(r)
        return lambda texto="": cola.pop(0)

    def test_busca_los_modelos_pregunta_e_instala_el_que_se_escucho(self):
        with tempfile.TemporaryDirectory() as d:
            ent, destino = os.path.join(d, "entrenado"), os.path.join(d, "Release1")
            os.makedirs(destino)
            _candidato(os.path.join(ent, "dsee-genero-cumbia-ab12"))
            _candidato(os.path.join(ent, "dsee-genero-salsa-cd34"), genero="salsa", aceptado=False)
            _candidato(os.path.join(ent, "dsee-genero-bolero-ef56"), genero="bolero")
            os.makedirs(os.path.join(ent, "dsee-genero-rock-gh78"))   # entrenamiento cortado: sin archivos
            lista = I.candidatos_de_genero(ent)
            estado = {g: m for _, g, _, m in lista}
            self.assertIsNone(estado["cumbia"]); self.assertIsNone(estado["bolero"])
            self.assertIn("no lo acepto", estado["salsa"]); self.assertIn("no termino", estado["rock"])
            log = os.path.join(d, "log.txt")
            cod = I.interactivo(self._respuestas("", "n", "s"), ent, destino, log)   # Enter = la carpeta propuesta; bolero no, cumbia si (en orden)
            self.assertEqual(cod, 0)
            self.assertEqual(sorted(os.listdir(destino)), ["IAS"])
            self.assertEqual(sorted(os.listdir(os.path.join(destino, "IAS"))), ["rocola_ia_genero_cumbia.bin"])   # bolero: dijo que no
            self.assertIn("NO: el entrenador no lo acepto", open(log, encoding="utf-8").read())

    def test_sin_modelos_lo_explica(self):
        with tempfile.TemporaryDirectory() as d:
            cod = I.interactivo(self._respuestas(), os.path.join(d, "entrenado"), d, os.path.join(d, "log.txt"))
            self.assertEqual(cod, 1)
            self.assertIn("rocola_ia_datos.generos.tsv", open(os.path.join(d, "log.txt"), encoding="utf-8").read())

    def test_carpeta_que_no_existe_no_instala(self):
        with tempfile.TemporaryDirectory() as d:
            ent = os.path.join(d, "entrenado"); _candidato(os.path.join(ent, "dsee-genero-cumbia-ab12"))
            cod = I.interactivo(self._respuestas(os.path.join(d, "no_esta"), "s"), ent, d, os.path.join(d, "log.txt"))
            self.assertEqual(cod, 2)
            self.assertFalse(any(n.startswith("rocola_ia_genero_") for n in os.listdir(d)))

    def test_sin_argumentos_espera_antes_de_cerrar_aunque_falle(self):
        from unittest import mock
        for efecto in (lambda: 1, mock.Mock(side_effect=RuntimeError("roto"))):
            with mock.patch.object(I, "interactivo", efecto), mock.patch("builtins.input", return_value="") as espera, \
                    mock.patch("sys.stderr"), mock.patch("sys.stdout"):
                I.main(["instalar_dsee_genero.py"])
                self.assertEqual(espera.call_count, 1)

    def test_el_bat_existe_busca_python_y_no_se_cierra_solo(self):
        bat = open(os.path.join(os.path.dirname(os.path.abspath(I.__file__)), "instalar_dsee_genero.bat"), "rb").read().decode("ascii")
        self.assertIn("\r\n", bat)
        self.assertIn("instalar_dsee_genero.py", bat); self.assertIn(":prueba", bat); self.assertIn("pause", bat)


if __name__ == "__main__":
    unittest.main()
