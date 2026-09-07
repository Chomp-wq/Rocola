# -*- coding: utf-8 -*-
"""Regresiones de seguridad/reanudacion sin GPU, red, ffmpeg ni checkpoint."""
import csv
import hashlib
import importlib.util
import json
import os
import sys
import tempfile
import types
import unittest
from unittest import mock

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import util_audio
import modelo_apollo


def cargar_archivo(nombre, path):
    spec = importlib.util.spec_from_file_location(nombre, path)
    modulo = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modulo)
    return modulo


procesar = cargar_archivo("procesar_todo_pruebas", os.path.join(AQUI, "05_procesar_todo.py"))
daemon = cargar_archivo("daemon_ia_pruebas", os.path.join(AQUI, "..", "tiempo_real", "daemon_ia.py"))
escanear = cargar_archivo("escanear_calidad_pruebas", os.path.join(AQUI, "escanear_calidad.py"))


class AudioTests(unittest.TestCase):
    def test_antifase_conserva_agudos(self):
        fs = 44100
        t = np.arange(fs * 3) / float(fs)
        completa = sum(np.sin(2 * np.pi * f * t + f) for f in range(300, 20800, 400)) * 0.01
        stereo = np.stack((completa, -completa), axis=1)
        self.assertGreater(util_audio.acantilado_hz(stereo, fs), 19000)

    def test_trozos_valida_solape_y_forma(self):
        x = np.arange(502, dtype=np.float64).reshape(251, 2)
        np.testing.assert_array_equal(util_audio.por_trozos(x, lambda z: z, fs=100, seg=1, sol=0), x)
        with self.assertRaises(ValueError):
            util_audio.por_trozos(x, lambda z: z, fs=100, seg=0.5, sol=0.5)
        with self.assertRaises(ValueError):
            util_audio.por_trozos(x, lambda z: z[:-1], fs=100, seg=1, sol=0.1)

    def test_audio_no_publica_nan_ni_complejos(self):
        x = np.ones((20, 2), dtype=np.float64)
        with self.assertRaises(ValueError):
            util_audio.por_trozos(x, lambda z: np.full_like(z, np.nan), fs=10, seg=3)
        with self.assertRaises(ValueError):
            util_audio.por_trozos(x, lambda z: z.astype(np.complex128), fs=10, seg=3)
        with tempfile.TemporaryDirectory() as td:
            with self.assertRaises(ValueError):
                util_audio.escribir_wav(os.path.join(td, "nan.wav"), np.full_like(x, np.nan))


class BibliotecaTests(unittest.TestCase):
    def test_copia_exacta_solo_si_coincide_formato(self):
        with tempfile.TemporaryDirectory() as td:
            src = os.path.join(td, "buena.MP3")
            dst = os.path.join(td, "salida.mp3")
            with open(src, "wb") as f:
                f.write(b"contenido original y etiquetas")
            with mock.patch.object(procesar, "_run") as run:
                procesar._copiar_o_convertir_buena("ffmpeg", src, dst, "mp3", 320)
            run.assert_not_called()
            with open(dst, "rb") as f:
                self.assertEqual(f.read(), b"contenido original y etiquetas")

    def test_conversion_y_portada_segun_formato(self):
        with tempfile.TemporaryDirectory() as td:
            src = os.path.join(td, "buena.flac")
            for formato in ("mp3", "wav"):
                dst = os.path.join(td, "salida." + formato)
                def codifica(cmd):
                    with open(cmd[-1], "wb") as f:
                        f.write(b"salida simulada")
                for mejorada in (False, True):
                    with mock.patch.object(procesar, "_run", side_effect=codifica) as run:
                        if mejorada:
                            procesar._encode_desde_wav("ffmpeg", "audio.wav", src, dst, formato, 320)
                        else:
                            procesar._copiar_o_convertir_buena("ffmpeg", src, dst, formato, 320)
                    cmd = run.call_args[0][0]
                    self.assertEqual("-vn" in cmd, formato == "wav")
                    self.assertEqual("-c:v" in cmd, formato == "mp3")
                    self.assertIn("-map_metadata", cmd)
                    self.assertTrue(os.path.isfile(dst))

    def test_resumen_final_distingue_fallos(self):
        for fallas in (0, 1, 27):
            with mock.patch("builtins.print") as salida:
                self.assertEqual(procesar.resumen_final("destino", fallas), 1 if fallas else 0)
            mensajes = " ".join(str(c) for c in salida.call_args_list)
            self.assertEqual("INCOMPLETO" in mensajes, bool(fallas))
            self.assertEqual("LISTO." in mensajes, not fallas)
            if fallas:
                self.assertIn("_errores.txt", mensajes)

    def test_csv_no_escapa_raiz_y_colisiones_no_pisan(self):
        with tempfile.TemporaryDirectory() as td:
            raiz = os.path.join(td, "musica")
            salida = os.path.join(td, "salida")
            album = os.path.join(raiz, "album")
            os.makedirs(album); os.makedirs(salida)
            a = os.path.join(album, "tema.mp3")
            b = os.path.join(album, "tema.flac")
            rel_a = os.path.relpath(a, raiz).replace("\\", "/")
            natural = os.path.join(album, "tema__%s.mp3" % hashlib.sha256(rel_a.encode("utf-8")).hexdigest()[:10])
            fuera = os.path.join(td, "fuera;mal.mp3")
            for p in (a, b, natural, fuera):
                with open(p, "wb") as f: f.write(b"audio")
            csv_path = os.path.join(td, "calidad.csv")
            with open(csv_path, "w", encoding="utf-8-sig", newline="") as f:
                w = csv.writer(f, delimiter=";")
                w.writerow(("acantilado_hz", "veredicto", "ruta"))
                w.writerow(("8000", "mal", fuera))
                w.writerow(("9000", "mal", a))
            rutas = procesar.orden_desde_csv(csv_path, raiz)
            self.assertEqual(set(rutas), {procesar._real(a), procesar._real(b), procesar._real(natural)})
            destinos = procesar.mapa_destinos(raiz, salida, rutas, "mp3")
            self.assertEqual(len(set(p.casefold() for p in destinos.values())), 3)
            self.assertTrue(all(procesar._dentro(p, salida) for p in destinos.values()))
            with self.assertRaises(ValueError):
                procesar.validar_raices(raiz, os.path.join(raiz, "resultado"))

    def test_manifest_detecta_entrada_salida_y_parametros_cambiados(self):
        with tempfile.TemporaryDirectory() as td:
            src = os.path.join(td, "a.mp3")
            dst = os.path.join(td, "a_salida.mp3")
            manifest = os.path.join(td, "m.jsonl")
            with open(src, "wb") as f: f.write(b"entrada")
            with open(dst, "wb") as f: f.write(b"x" * 100)
            params = {"modelo": "abc", "seg": 10.0}
            with open(manifest, "a", encoding="utf-8", newline="\n") as f:
                procesar._anota_manifest(f, src, dst, params)
            reg = procesar.cargar_manifest(manifest)[procesar._canon(src)]
            self.assertTrue(procesar.entrada_completa(reg, src, dst, params))
            st = os.stat(dst)
            os.utime(dst, ns=(st.st_atime_ns, st.st_mtime_ns + 10_000_000_000))
            self.assertFalse(procesar.entrada_completa(reg, src, dst, params))
            self.assertFalse(procesar.entrada_completa(reg, src, dst, {"modelo": "otro", "seg": 10.0}))

    def test_manifest_ignora_json_ajeno_y_repara_ultimo_registro_cortado(self):
        with tempfile.TemporaryDirectory() as td:
            manifest = os.path.join(td, "m.jsonl")
            with open(manifest, "wb") as f:
                f.write(b"[]\n{\"registro\":")
            self.assertEqual(procesar.cargar_manifest(manifest), {})
            procesar._repara_ultima_linea(manifest)
            with open(manifest, "rb") as f:
                self.assertEqual(f.read(), b"[]\n")

    def test_validacion_ffprobe_exige_stream_de_audio(self):
        with tempfile.TemporaryDirectory() as td:
            dst = os.path.join(td, "salida.mp3")
            with open(dst, "wb") as f: f.write(b"x" * 100)
            sin_audio = mock.Mock(stdout=json.dumps({"format": {"duration": "10"}, "streams": []}))
            con_audio = mock.Mock(stdout=json.dumps({"format": {"duration": "10"}, "streams": [{"index": 0}]}))
            with mock.patch.object(procesar.subprocess, "run", return_value=sin_audio):
                self.assertFalse(procesar._salida_valida("ffprobe", dst))
            with mock.patch.object(procesar.subprocess, "run", return_value=con_audio):
                self.assertTrue(procesar._salida_valida("ffprobe", dst))


class ModeloTests(unittest.TestCase):
    def test_modelo_remoto_exige_hash_de_commit_completo(self):
        self.assertTrue(modelo_apollo._revision_inmutable(modelo_apollo.DEFAULT_REVISION))
        for mutable in (None, "main", "v1.0", "abc123"):
            self.assertFalse(modelo_apollo._revision_inmutable(mutable))
            with self.assertRaises(RuntimeError):
                modelo_apollo.descargar_checkpoint("usuario/modelo", mutable)

    def test_pickle_solo_se_carga_como_weights_only(self):
        with tempfile.TemporaryDirectory() as td:
            p = os.path.join(td, "custom.pt")
            with open(p, "wb") as f: f.write(b"checkpoint-local-no-confiable")
            digest = hashlib.sha256(b"checkpoint-local-no-confiable").hexdigest()
            falso = types.ModuleType("torch")
            llamadas = []
            def load(*args, **kwargs):
                llamadas.append((args, kwargs)); return {"peso": object()}
            falso.load = load
            with mock.patch.dict(sys.modules, {"torch": falso}):
                modelo_apollo._cargar_checkpoint_seguro(p, digest)
            self.assertEqual(llamadas[0][1].get("weights_only"), True)

    def test_digest_no_se_puede_suplantar(self):
        with tempfile.TemporaryDirectory() as td:
            p = os.path.join(td, "falso.bin")
            with open(p, "wb") as f: f.write(b"no-es-el-checkpoint-oficial")
            with self.assertRaises(RuntimeError):
                modelo_apollo._cargar_checkpoint_seguro(p, modelo_apollo.DEFAULT_CHECKPOINT_SHA256)

    def test_state_dict_es_estricto(self):
        self.assertEqual(set(modelo_apollo._ajusta_prefijo({"module.a": 1}, {"a"})), {"a"})
        with self.assertRaises(RuntimeError):
            modelo_apollo._ajusta_prefijo({"module.a": 1, "extra": 2}, {"a"})
        with self.assertRaises(RuntimeError):
            modelo_apollo._ajusta_prefijo({"a": 1, "module.a": 2}, {"a"})


class DaemonTests(unittest.TestCase):
    def test_hash_incluye_fecha_mezcla_y_pipeline(self):
        with tempfile.TemporaryDirectory() as td:
            p = os.path.join(td, "tema.mp3")
            with open(p, "wb") as f: f.write(b"abc")
            uno = daemon.hash_trabajo(p, "auto", "modelo-a")
            self.assertNotEqual(uno, daemon.hash_trabajo(p, "70", "modelo-a"))
            self.assertNotEqual(uno, daemon.hash_trabajo(p, "auto", "modelo-b"))
            st = os.stat(p)
            os.utime(p, ns=(st.st_atime_ns, st.st_mtime_ns + 10_000_000_000))
            self.assertNotEqual(uno, daemon.hash_trabajo(p, "auto", "modelo-a"))
            with mock.patch.object(daemon.os, "stat", wraps=os.stat) as stat:
                daemon.hash_trabajo(p, "auto", "modelo-a")
                self.assertEqual(stat.call_count, 1)

    def test_job_valido_y_error_terminal_atomico(self):
        with tempfile.TemporaryDirectory() as td:
            src = os.path.join(td, "tema.mp3")
            listos = os.path.join(td, "listos")
            os.makedirs(listos)
            with open(src, "wb") as f: f.write(b"abc")
            pipeline = "f" * 64
            h = daemon.hash_trabajo(src, "auto", pipeline)
            job = {"src": src, "mezcla": "auto", "mezcla_key": "auto",
                   "pipeline": pipeline, "cache_key": h}
            self.assertEqual(daemon._valida_job(job, h, pipeline)[0], src)
            daemon._guarda_error(listos, h, 3, "rechazado", terminal=True)
            with open(os.path.join(listos, h + ".error"), encoding="utf-8") as f:
                dato = json.load(f)
            self.assertTrue(dato["terminal"])
            self.assertEqual(dato["retry_at"], 0)
            for campo in ("mezcla", "mezcla_key"):
                incompleto = dict(job); del incompleto[campo]
                with self.assertRaises(ValueError):
                    daemon._valida_job(incompleto, h, pipeline)
            distinto = dict(job); distinto["mezcla_key"] = "70"
            with self.assertRaises(ValueError):
                daemon._valida_job(distinto, h, pipeline)

    def test_hash_imita_lower_invariant_de_dotnet(self):
        # Python expande U+0130 a "i + punto"; String.ToLowerInvariant de .NET lo conserva.
        self.assertEqual(daemon._lower_invariant_dotnet("A\u0130\u1e9e\u212a\u03a3"), "a\u0130\u00dfk\u03c3")

    def test_fallo_transitorio_conserva_job_y_tercero_marca_original(self):
        with tempfile.TemporaryDirectory() as td:
            pend = os.path.join(td, "pendientes"); listos = os.path.join(td, "listos")
            os.makedirs(pend); os.makedirs(listos)
            src = os.path.join(td, "tema.mp3")
            with open(src, "wb") as f: f.write(b"abc")
            pipeline = "f" * 64
            h = daemon.hash_trabajo(src, "auto", pipeline)
            job_path = os.path.join(pend, h + ".job")
            job = {"src": src, "mezcla": "auto", "mezcla_key": "auto",
                   "pipeline": pipeline, "cache_key": h}
            with open(job_path, "w", encoding="utf-8") as f: json.dump(job, f)
            with mock.patch.object(daemon, "_duracion", return_value=10.0), \
                 mock.patch.object(daemon, "_run", side_effect=RuntimeError("decode roto")):
                for intento in (1, 2):
                    self.assertIs(daemon.procesar_uno(job_path, listos, object(), "ffmpeg",
                                                      pipeline, "ffprobe"), daemon.DEFERIDO)
                    self.assertTrue(os.path.isfile(job_path))
                    err_path = os.path.join(listos, h + ".error")
                    with open(err_path, encoding="utf-8") as f: dato = json.load(f)
                    self.assertEqual(dato["attempts"], intento)
                    self.assertFalse(dato["terminal"])
                    dato["retry_at"] = 0
                    with open(err_path, "w", encoding="utf-8") as f: json.dump(dato, f)
                resultado = daemon.procesar_uno(job_path, listos, object(), "ffmpeg",
                                                pipeline, "ffprobe")
            self.assertEqual(resultado, os.path.join(listos, h + ".orig"))
            self.assertFalse(os.path.exists(job_path))
            self.assertFalse(os.path.exists(os.path.join(listos, h + ".error")))

    def test_ffprobe_transitorio_tambien_respeta_tres_intentos(self):
        with tempfile.TemporaryDirectory() as td:
            pend = os.path.join(td, "pendientes"); listos = os.path.join(td, "listos")
            os.makedirs(pend); os.makedirs(listos)
            src = os.path.join(td, "tema.mp3")
            with open(src, "wb") as f: f.write(b"abc")
            pipeline = "f" * 64; h = daemon.hash_trabajo(src, "auto", pipeline)
            job_path = os.path.join(pend, h + ".job")
            job = {"src": src, "mezcla": "auto", "mezcla_key": "auto",
                   "pipeline": pipeline, "cache_key": h}
            with open(job_path, "w", encoding="utf-8") as f: json.dump(job, f)
            with mock.patch.object(daemon, "_duracion", return_value=None):
                for intento in (1, 2):
                    self.assertIs(daemon.procesar_uno(job_path, listos, object(), "ffmpeg",
                                                      pipeline, "ffprobe"), daemon.DEFERIDO)
                    err_path = os.path.join(listos, h + ".error")
                    with open(err_path, encoding="utf-8") as f: dato = json.load(f)
                    self.assertEqual(dato["attempts"], intento)
                    dato["retry_at"] = 0
                    with open(err_path, "w", encoding="utf-8") as f: json.dump(dato, f)
                resultado = daemon.procesar_uno(job_path, listos, object(), "ffmpeg",
                                                pipeline, "ffprobe")
            self.assertTrue(resultado.endswith(".orig"))
            self.assertFalse(os.path.exists(job_path))

    def test_publicacion_incompleta_conserva_job_hasta_reparar_meta(self):
        with tempfile.TemporaryDirectory() as td:
            pend = os.path.join(td, "pendientes"); listos = os.path.join(td, "listos")
            os.makedirs(pend); os.makedirs(listos)
            src = os.path.join(td, "tema.mp3")
            with open(src, "wb") as f: f.write(b"abc")
            pipeline = "f" * 64; h = daemon.hash_trabajo(src, "auto", pipeline)
            job_path = os.path.join(pend, h + ".job")
            job = {"src": src, "mezcla": "auto", "mezcla_key": "auto",
                   "pipeline": pipeline, "cache_key": h}
            with open(job_path, "w", encoding="utf-8") as f: json.dump(job, f)
            with open(os.path.join(listos, h + ".orig"), "w") as f: f.write("listo")
            daemon._guarda_error(listos, h, 1, "anterior")
            with mock.patch.object(daemon, "_escribe_meta", side_effect=OSError("disco")):
                self.assertIs(daemon.procesar_uno(job_path, listos, object(), "ffmpeg",
                                                  pipeline, "ffprobe"), daemon.DEFERIDO)
            self.assertTrue(os.path.exists(job_path))
            self.assertTrue(os.path.exists(os.path.join(listos, h + ".error")))
            self.assertEqual(daemon.procesar_uno(job_path, listos, object(), "ffmpeg",
                                                 pipeline, "ffprobe"), os.path.join(listos, h + ".orig"))
            self.assertFalse(os.path.exists(job_path))
            self.assertFalse(os.path.exists(os.path.join(listos, h + ".error")))
            self.assertTrue(os.path.exists(os.path.join(listos, h + ".meta.json")))

    def test_error_terminal_no_borra_job_si_no_puede_publicarse(self):
        with tempfile.TemporaryDirectory() as td:
            pend = os.path.join(td, "pendientes"); listos = os.path.join(td, "listos")
            os.makedirs(pend); os.makedirs(listos)
            job_path = os.path.join(pend, "0" * 16 + ".job")
            with open(job_path, "w", encoding="utf-8") as f: f.write("{}")
            with mock.patch.object(daemon, "_guarda_error", side_effect=OSError("disco")):
                self.assertIs(daemon.procesar_uno(job_path, listos, object(), "ffmpeg",
                                                  "f" * 64, "ffprobe"), daemon.DEFERIDO)
            self.assertTrue(os.path.exists(job_path))

    def test_ciclo_distingue_reintento_diferido(self):
        with tempfile.TemporaryDirectory() as td:
            pend = os.path.join(td, "pendientes"); listos = os.path.join(td, "listos")
            os.makedirs(pend); os.makedirs(listos)
            job = os.path.join(pend, "0" * 16 + ".job")
            with open(job, "w") as f: f.write("{}")
            original = daemon.procesar_uno
            llamadas = []
            try:
                def falso(*args):
                    llamadas.append(args); return daemon.DEFERIDO
                daemon.procesar_uno = falso
                hechos = daemon._un_ciclo_log(pend, listos, object(), "ffmpeg", "p", "ffprobe", 1, td, 10)
            finally:
                daemon.procesar_uno = original
            self.assertEqual(hechos, 0)
            self.assertEqual(len(llamadas), 1)
            self.assertEqual(llamadas[0][4:7], ("p", "ffprobe", 10))


class EscanerTests(unittest.TestCase):
    def test_escaneo_es_determinista_y_no_sigue_enlace_fuera(self):
        with tempfile.TemporaryDirectory() as td:
            raiz = os.path.join(td, "musica"); fuera = os.path.join(td, "fuera")
            os.makedirs(raiz); os.makedirs(fuera)
            for nombre in ("b.mp3", "A.WAV"):
                with open(os.path.join(raiz, nombre), "wb") as f: f.write(b"audio")
            externo = os.path.join(fuera, "secreto.mp3")
            with open(externo, "wb") as f: f.write(b"audio")
            enlace = os.path.join(raiz, "enlace.mp3")
            try:
                os.symlink(externo, enlace)
            except (OSError, NotImplementedError):
                enlace = None
            rutas = escanear._escanear(raiz)
            self.assertEqual([os.path.basename(p) for p in rutas], ["A.WAV", "b.mp3"])
            if enlace is not None:
                self.assertNotIn(escanear._canon(enlace), rutas)

if __name__ == "__main__":
    unittest.main(verbosity=2)
