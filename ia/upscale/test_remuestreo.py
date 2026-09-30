# -*- coding: utf-8 -*-
"""Remuestreo a 44,1 kHz para Apollo: eleccion, calidad medida, identidad de cache y reaprovechamiento.

Sin FFmpeg se prueban la eleccion (con un FFmpeg simulado), la huella y el manifiesto. Con FFmpeg y
ffprobe de verdad, ademas: la calidad del remuestreador elegido, que lo que ya viene a 44,1 kHz sale
igual bit a bit, la alineacion, la duracion, el final de la cancion y el motor de punta a punta.
"""
import hashlib
import importlib.util
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock

import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
import memoria_audio
import modelo_apollo
import util_audio
from util_audio import FS, escribir_wav_float, leer_wav


def cargar(nombre, relativo):
    spec = importlib.util.spec_from_file_location(nombre, os.path.join(AQUI, relativo))
    modulo = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modulo)
    return modulo


daemon = cargar("daemon_remuestreo", os.path.join("..", "tiempo_real", "daemon_ia.py"))
offline = cargar("offline_remuestreo", "05_procesar_todo.py")
FFMPEG = shutil.which("ffmpeg")
FFPROBE = shutil.which("ffprobe")
HAY_FF = bool(FFMPEG and FFPROBE)


class FFmpegSimulado(object):
    """Sustituye a subprocess.run: acepta o rechaza cada motor y escribe una salida plausible."""

    def __init__(self, acepta=("soxr", "swr"), largo=4410, nivel=0.25):
        self.acepta, self.largo, self.nivel = acepta, largo, nivel
        self.llamadas = []

    def __call__(self, cmd, **kw):
        self.llamadas.append(list(cmd))
        filtro = cmd[cmd.index("-af") + 1] if "-af" in cmd else None
        motor = "soxr" if filtro and "soxr" in filtro else ("swr" if filtro else "defecto")
        if motor not in self.acepta:
            raise subprocess.CalledProcessError(1, cmd)
        t = np.arange(self.largo) / 44100.0
        escribir_wav_float(cmd[-1], np.stack((self.nivel * np.sin(2 * np.pi * 1000 * t),
                                              self.nivel * np.sin(2 * np.pi * 1500 * t)), axis=1), 44100)
        return subprocess.CompletedProcess(cmd, 0)


class EleccionTests(unittest.TestCase):
    """Que remuestreador se usa: se PRUEBA el FFmpeg, no se supone que traiga SoXR."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.ff = os.path.join(self.tmp.name, "ffmpeg_simulado")
        with open(self.ff, "wb") as f:
            f.write(b"x")
        util_audio._CAPACIDAD.clear()
        self.addCleanup(util_audio._CAPACIDAD.clear)

    def elige(self, sim):
        with mock.patch.object(util_audio.subprocess, "run", sim):
            return util_audio.remuestreador(self.ff)

    def test_con_soxr_elige_soxr(self):
        self.assertEqual(self.elige(FFmpegSimulado())[0], "soxr-p28")

    def test_sin_soxr_elige_el_swr_reforzado(self):
        ident, filtro = self.elige(FFmpegSimulado(acepta=("swr",)))
        self.assertEqual(ident, "swr-f256-c092-k12")
        self.assertIn("filter_size=256", filtro)

    def test_sin_ninguno_queda_el_de_siempre(self):
        self.assertEqual(self.elige(FFmpegSimulado(acepta=())), (util_audio.REMUESTREO_HEREDADO, None))

    def test_salida_mala_no_cuenta_como_capaz(self):
        # Un motor que "funciona" pero entrega otro largo u otro nivel no se da por bueno.
        self.assertEqual(self.elige(FFmpegSimulado(largo=4000))[0], util_audio.REMUESTREO_HEREDADO)
        util_audio._CAPACIDAD.clear()
        self.assertEqual(self.elige(FFmpegSimulado(nivel=0.2))[0], util_audio.REMUESTREO_HEREDADO)

    def test_se_recuerda_por_ejecutable_y_se_reprueba_si_cambia(self):
        sim = FFmpegSimulado()
        self.assertEqual(self.elige(sim)[0], "soxr-p28")
        antes = len(sim.llamadas)
        self.assertEqual(self.elige(sim)[0], "soxr-p28")
        self.assertEqual(len(sim.llamadas), antes, "la segunda consulta no debe volver a ejecutar FFmpeg")
        with open(self.ff, "ab") as f:
            f.write(b"y")                       # otro ejecutable en la misma ruta
        self.assertEqual(self.elige(FFmpegSimulado(acepta=("swr",)))[0], "swr-f256-c092-k12")

    def test_ejecutable_inexistente(self):
        self.assertEqual(util_audio.remuestreador(os.path.join(self.tmp.name, "no_existe")),
                         (util_audio.REMUESTREO_HEREDADO, None))

    def test_argumentos(self):
        self.assertEqual(util_audio.args_decodificar(None),
                         ["-ac", "2", "-ar", "44100", "-c:a", "pcm_f32le"])   # los de siempre, exactos
        self.assertEqual(util_audio.args_decodificar("aresample=x"),
                         ["-ac", "2", "-af", "aresample=x", "-ar", "44100", "-c:a", "pcm_f32le"])

    def test_sin_remuestreo(self):
        self.assertFalse(util_audio.sin_remuestreo([]))
        self.assertFalse(util_audio.sin_remuestreo([0]))
        self.assertFalse(util_audio.sin_remuestreo([44100, 48000]))
        self.assertTrue(util_audio.sin_remuestreo([44100]))
        self.assertTrue(util_audio.sin_remuestreo([44100, 44100]))


class HuellaTests(unittest.TestCase):
    def test_confianza_corte_versiona_cache_y_no_reutiliza_v4(self):
        fp = "a" * 64
        vieja = hashlib.sha256(("apollo-safe-v4-float-decode|modelo=%s|seg=%.9g|fs=%d|cache=%s" % (
            fp, 10.0, 44100, "wav-float32-v4-sin-recompresion")).encode("utf-8")).hexdigest()
        self.assertNotEqual(daemon.huella_pipeline(fp, 10.0), vieja)
        self.assertEqual(daemon.huella_pipeline(fp, 10.0, util_audio.REMUESTREO_HEREDADO), daemon.huella_pipeline(fp, 10.0))

    def test_otro_remuestreo_otra_huella_y_vecinas(self):
        fp = "b" * 64
        huellas = {i: daemon.huella_pipeline(fp, 10.0, i) for i in
                   [util_audio.REMUESTREO_HEREDADO] + [i for i, _ in util_audio.REMUESTREOS]}
        self.assertEqual(len(set(huellas.values())), len(huellas))
        for ident in huellas:
            vecinas = daemon.huellas_vecinas(fp, 10.0, ident)
            self.assertNotIn(huellas[ident], vecinas)
            self.assertEqual(set(vecinas), set(v for k, v in huellas.items() if k != ident))

    def test_revisa_ffmpeg_detecta_otro_ejecutable(self):
        with tempfile.TemporaryDirectory() as td:
            ff = os.path.join(td, "ffmpeg")
            with open(ff, "wb") as f:
                f.write(b"1")
            util_audio._CAPACIDAD.clear()
            self.addCleanup(util_audio._CAPACIDAD.clear)
            with mock.patch.object(util_audio.subprocess, "run", FFmpegSimulado()):
                estado = daemon.estado_remuestreo(ff, "c" * 64, 10.0)
                self.assertEqual(estado["remuestreo"], "soxr-p28")
                self.assertFalse(daemon.revisa_ffmpeg(ff, estado, "c" * 64, 10.0))
            antes = estado["pipeline"]
            with open(ff, "ab") as f:
                f.write(b"2")
            with mock.patch.object(util_audio.subprocess, "run", FFmpegSimulado(acepta=("swr",))):
                self.assertTrue(daemon.revisa_ffmpeg(ff, estado, "c" * 64, 10.0))
                self.assertFalse(daemon.revisa_ffmpeg(ff, estado, "c" * 64, 10.0))
            self.assertEqual(estado["remuestreo"], "swr-f256-c092-k12")
            self.assertNotEqual(estado["pipeline"], antes)
            self.assertIn(antes, estado["vecinas"])
            os.remove(ff)   # un ejecutable que desaparece no cambia nada
            self.assertFalse(daemon.revisa_ffmpeg(ff, estado, "c" * 64, 10.0))


class ManifiestoTests(unittest.TestCase):
    """Herramienta por lotes: lo que ya estaba hecho y no cambia no se repite."""

    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.src = os.path.join(self.tmp.name, "a.wav")
        self.dst = os.path.join(self.tmp.name, "a_out.wav")
        for p in (self.src, self.dst):
            escribir_wav_float(p, np.zeros((100, 2)) + .1)
        self.base = {"formato": "wav", "modelo": "m"}

    def registro(self, params):
        return {"input": offline._identidad_entrada(self.src), "params": params,
                "output": offline._identidad_salida(self.dst)}

    def estado(self, params, remuestreo, frecuencias):
        preguntas = []
        def frec(s):
            preguntas.append(s)
            return frecuencias
        return offline.estado_entrada(self.registro(params), self.src, self.dst, self.base,
                                      remuestreo, frec), preguntas

    def test_entrada_vieja_a_44k1_sigue_valiendo_y_se_refresca(self):
        (completa, refresco), preguntas = self.estado(dict(self.base), "soxr-p28", [44100])
        self.assertTrue(completa)
        self.assertEqual(refresco, dict(self.base, remuestreo="ninguno"))
        self.assertEqual(len(preguntas), 1)

    def test_entrada_vieja_a_48k_se_rehace_con_otro_remuestreo(self):
        (completa, refresco), _ = self.estado(dict(self.base), "soxr-p28", [48000])
        self.assertFalse(completa)
        self.assertIsNone(refresco)

    def test_entrada_vieja_a_48k_vale_si_sigue_el_remuestreo_de_siempre(self):
        (completa, refresco), _ = self.estado(dict(self.base), util_audio.REMUESTREO_HEREDADO, [48000])
        self.assertTrue(completa)
        self.assertEqual(refresco["remuestreo"], util_audio.REMUESTREO_HEREDADO)

    def test_frecuencia_desconocida_no_se_da_por_44k1(self):
        (completa, _), _ = self.estado(dict(self.base), "soxr-p28", [])
        self.assertFalse(completa)

    def test_entradas_nuevas_sin_preguntar(self):
        casos = (("ninguno", "soxr-p28", True), ("soxr-p28", "soxr-p28", True),
                 ("swr-f256-c092-k12", "soxr-p28", False), ("soxr-p28", "swr-f256-c092-k12", False))
        for usado, hoy, esperado in casos:
            (completa, refresco), preguntas = self.estado(dict(self.base, remuestreo=usado), hoy, [48000])
            self.assertEqual(completa, esperado, (usado, hoy))
            self.assertIsNone(refresco)
            self.assertEqual(preguntas, [], "una entrada nueva no necesita preguntar la frecuencia")

    def test_otros_parametros_siguen_mandando(self):
        (completa, _), _ = self.estado(dict(self.base, formato="mp3"), "soxr-p28", [44100])
        self.assertFalse(completa)
        (completa, _), _ = self.estado(dict(self.base, formato="mp3", remuestreo="ninguno"), "soxr-p28", [44100])
        self.assertFalse(completa)


# ------------------------------------------------------------------------------------------------
#  CON FFMPEG DE VERDAD
# ------------------------------------------------------------------------------------------------
def convierte(carpeta, x, fs, filtro):
    src = os.path.join(carpeta, "entrada.wav")
    dst = os.path.join(carpeta, "salida.wav")
    escribir_wav_float(src, x, fs)
    subprocess.run([FFMPEG, "-nostdin", "-y", "-v", "error", "-i", src] +
                   util_audio.args_decodificar(filtro) + [dst], check=True)
    y, fo = leer_wav(dst)
    assert fo == FS
    return y


def tono(fs, f, segundos=2.0, amp=0.5):
    n = int(fs * segundos)
    t = np.arange(n) / float(fs)
    s = amp * np.sin(2 * np.pi * f * t)
    return np.stack((s, s), axis=1)


def analiza(y, f):
    """(ganancia del tono en dB, pico de lo que sobra en dB) sobre la mitad central."""
    n = len(y)
    a, b = n // 4, 3 * n // 4
    t = np.arange(a, b) / float(FS)
    s = y[a:b, 0]
    if f < FS / 2:
        M = np.stack((np.sin(2 * np.pi * f * t), np.cos(2 * np.pi * f * t)), axis=1)
        c = np.linalg.lstsq(M, s, rcond=None)[0]
        amp = float(np.hypot(c[0], c[1]))
        resto = s - M @ c
    else:
        amp, resto = 0.0, s
    w = np.hanning(len(resto))
    R = np.abs(np.fft.rfft(resto * w)) / (w.sum() / 2)
    return 20 * np.log10(max(amp / 0.5, 1e-12)), 20 * np.log10(max(float(R.max()) / 0.5, 1e-12))


@unittest.skipUnless(FFMPEG, "requiere FFmpeg")
class CalidadRealTests(unittest.TestCase):
    """El remuestreador que este FFmpeg permite elegir, medido contra la verdad analitica."""

    # (fuente, tono, que se mira): 'g' ganancia en banda; 'p' plegado/imagen en dB
    CASOS = ((48000, 19000, "g"), (48000, 20000, "g"), (48000, 22500, "p"), (48000, 23000, "p"),
             (96000, 20000, "g"), (96000, 24000, "p"), (22050, 9000, "g"), (22050, 10000, "p"),
             (32000, 14000, "g"), (32000, 15000, "p"))
    UMBRALES = {"soxr-p28": (0.05, -140.0), "swr-f256-c092-k12": (0.5, -110.0)}

    @classmethod
    def setUpClass(cls):
        cls.ident, cls.filtro = util_audio.remuestreador(FFMPEG)
        cls.tmp = tempfile.TemporaryDirectory()

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def test_calidad_del_elegido_frente_al_de_siempre(self):
        if self.ident not in self.UMBRALES:
            self.skipTest("este FFmpeg solo ejecuta el remuestreo de siempre")
        tol_g, tope_p = self.UMBRALES[self.ident]
        print("\n  remuestreador elegido: %s" % self.ident)
        for fs, f, que in self.CASOS:
            g0, p0 = analiza(convierte(self.tmp.name, tono(fs, f), fs, None), f)
            g1, p1 = analiza(convierte(self.tmp.name, tono(fs, f), fs, self.filtro), f)
            print("  %5d Hz -> 44100, tono %5d Hz: de siempre %7.2f dB / resto %7.1f dB   elegido %7.2f dB / resto %7.1f dB"
                  % (fs, f, g0, p0, g1, p1))
            if que == "g":
                self.assertLess(abs(g1), tol_g, "ganancia en banda %d Hz desde %d Hz" % (f, fs))
            self.assertLess(p1, tope_p, "lo que sobra (plegado, imagen o distorsion) con %d Hz desde %d Hz" % (f, fs))

    def test_lo_que_ya_viene_a_44k1_sale_igual_bit_a_bit(self):
        rng = np.random.default_rng(3)
        n = FS * 2
        t = np.arange(n) / float(FS)
        estereo = np.stack((0.3 * np.sin(2 * np.pi * 997 * t) + 0.05 * rng.standard_normal(n),
                            0.2 * np.sin(2 * np.pi * 4001 * t)), axis=1)
        fuentes = {"estereo float": (estereo, "f32"), "estereo 16 bits": (estereo, "s16"),
                   "mono": (estereo[:, :1], "f32"), "5.1": (np.repeat(estereo[:, :1], 6, axis=1) * [1, .5, .3, .1, .7, .2], "f32")}
        with tempfile.TemporaryDirectory() as td:
            rutas = []
            for nombre, (x, fmt) in fuentes.items():
                ruta = os.path.join(td, nombre.replace(" ", "_") + ".wav")
                if fmt == "f32":
                    escribir_wav_float(ruta, x, FS)
                else:
                    util_audio.escribir_wav(ruta, x, FS)
                rutas.append(ruta)
            mp3 = os.path.join(td, "tema.mp3")
            if subprocess.run([FFMPEG, "-nostdin", "-y", "-v", "error", "-i", rutas[0], "-c:a", "libmp3lame",
                               "-b:a", "128k", mp3]).returncode == 0:
                rutas.append(mp3)
            for ruta in rutas:
                salidas = []
                for filtro in (None, self.filtro) + tuple(f for _, f in util_audio.REMUESTREOS):
                    dst = os.path.join(td, "dec.wav")
                    r = subprocess.run([FFMPEG, "-nostdin", "-y", "-v", "error", "-i", ruta] +
                                       util_audio.args_decodificar(filtro) + [dst])
                    if r.returncode != 0:
                        continue   # un motor que este FFmpeg no trae
                    with open(dst, "rb") as f:
                        salidas.append(f.read())
                self.assertGreaterEqual(len(salidas), 2)
                for s in salidas[1:]:
                    self.assertEqual(s, salidas[0], os.path.basename(ruta))

    def test_alineacion_duracion_final_y_canales(self):
        for fs in (48000, 96000, 32000, 22050):
            for extra in (0, 7, 1234):
                n = fs * 3 + extra
                x = np.zeros((n, 2))
                k = np.arange(n) - fs            # impulso de banda limitada justo en t = 1,000 s
                bw = 0.45 * min(fs, FS) / fs
                h = np.sinc(2 * bw * k) * np.where(np.abs(k) < 400, 0.5 + 0.5 * np.cos(np.pi * k / 400), 0.0)
                x[:, 0] = 0.5 * h / h.max()
                m = int(0.008 * fs)              # rafaga de 1 kHz en los ultimos 8 ms, canal derecho
                x[n - m:, 1] = 0.3 * np.sin(2 * np.pi * 1000 * np.arange(m) / fs) * np.hanning(m)
                y = convierte(self.tmp.name, x, fs, self.filtro)
                i = int(np.argmax(y[:, 0]))
                a, b, c = y[i - 1, 0], y[i, 0], y[i + 1, 0]
                pos = i + 0.5 * (a - c) / (a - 2 * b + c)
                self.assertLess(abs(pos - FS), 0.01, "impulso corrido desde %d Hz" % fs)
                self.assertLessEqual(abs(len(y) - n * FS / float(fs)), 1.0, "duracion desde %d Hz" % fs)
                energia = (y[:, 1] ** 2).sum() / ((x[:, 1] ** 2).sum() * FS / float(fs))
                self.assertLess(abs(energia - 1.0), 1e-3, "final de la cancion desde %d Hz" % fs)
                self.assertEqual(float(np.abs(y[: len(y) // 2, 1]).max()), 0.0, "cruce de canales")

    def test_picos_finitos_y_acotados(self):
        fs = 48000
        t = np.arange(fs) / float(fs)
        cuadrada = np.sign(np.sin(2 * np.pi * 997 * t))
        x = np.stack((cuadrada, -cuadrada), axis=1)
        y = convierte(self.tmp.name, x, fs, self.filtro)
        self.assertTrue(np.isfinite(y).all())
        pico = float(np.abs(y).max())
        print("\n  pico de una cuadrada a 0 dBFS tras remuestrear (%s): %.3f (%+.2f dB); el WAV float lo "
              "guarda sin recortar y el limitador de la rocola lo controla" % (self.ident, pico, 20 * np.log10(pico)))
        self.assertLess(pico, 1.5)


@unittest.skipUnless(HAY_FF, "requiere FFmpeg/ffprobe")
class MotorTests(unittest.TestCase):
    """El motor en vivo de punta a punta, con un Apollo simulado."""

    FP = "d" * 64
    SEG = 10.0

    @classmethod
    def setUpClass(cls):
        cls.ident, cls.filtro = util_audio.remuestreador(FFMPEG)

    def setUp(self):
        if self.ident == util_audio.REMUESTREO_HEREDADO:
            self.skipTest("este FFmpeg solo ejecuta el remuestreo de siempre")
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.cache = os.path.join(self.tmp.name, "cache")
        self.pend, self.listos = daemon._dirs(self.cache)
        self.vieja = daemon.huella_pipeline(self.FP, self.SEG)
        self.nueva = daemon.huella_pipeline(self.FP, self.SEG, self.ident)
        self.vecinas = daemon.huellas_vecinas(self.FP, self.SEG, self.ident)
        self.llamadas = 0

    def fuente(self, nombre, fs):
        ruta = os.path.join(self.tmp.name, nombre)
        t = np.arange(int(fs * 2.3)) / float(fs)
        s = .1 * np.sin(2 * np.pi * 523 * t) + .08 * np.sin(2 * np.pi * 5003 * t)
        escribir_wav_float(ruta, np.stack((s, -s), axis=1), fs)
        return ruta

    def proc(self, x):
        self.llamadas += 1
        return x * .93 + .013 * np.roll(x, 3, axis=0)

    def trabajo(self, src, pipeline, mezcla="70"):
        h = daemon.hash_trabajo(src, mezcla, pipeline)
        job = os.path.join(self.pend, h + ".job")
        with open(job, "w") as f:
            json.dump(dict(src=src, mezcla=mezcla, mezcla_key=mezcla, pipeline=pipeline, cache_key=h), f)
        return h, job

    def corre(self, src, pipeline, filtro, vecinas, mezcla="70", proc=None):
        h, job = self.trabajo(src, pipeline, mezcla)
        r = daemon.procesar_uno(job, self.listos, proc or self.proc, FFMPEG, pipeline, FFPROBE,
                                seg=.75, filtro=filtro, vecinas=vecinas)
        return h, job, r

    def lee(self, ruta):
        with open(ruta, "rb") as f:
            return f.read()

    def test_48k_se_decodifica_con_el_elegido(self):
        src = self.fuente("tema48.wav", 48000)
        h, job, r = self.corre(src, self.nueva, self.filtro, self.vecinas, mezcla="0")
        self.assertTrue(isinstance(r, str) and r.endswith(".wav"))
        self.assertFalse(os.path.exists(job))
        y, fs = leer_wav(r)
        esperado = convierte(self.tmp.name, leer_wav(src)[0], 48000, self.filtro)
        self.assertEqual(fs, FS)
        np.testing.assert_array_equal(y, esperado)      # mezcla 0: es la decodificacion tal cual
        de_siempre = convierte(self.tmp.name, leer_wav(src)[0], 48000, None)
        self.assertFalse(np.array_equal(y, de_siempre), "con otro remuestreador el resultado debe cambiar")
        self.assertGreater(self.llamadas, 0, "debia pasar por Apollo")

    def test_44k1_se_reaprovecha_sin_pasar_por_apollo(self):
        src = self.fuente("tema441.wav", FS)
        h0, _, r0 = self.corre(src, self.vieja, None, ())
        antes = self.lee(r0)
        self.assertGreater(self.llamadas, 0)
        def prohibido(x):
            raise AssertionError("Apollo no debe volver a procesar una cancion que no cambia")
        h1, job1, r1 = self.corre(src, self.nueva, self.filtro, self.vecinas, proc=prohibido)
        self.assertEqual(r1, os.path.join(self.listos, h1 + ".wav"))
        self.assertEqual(self.lee(r1), antes)
        self.assertFalse(os.path.exists(r0))
        self.assertFalse(os.path.exists(os.path.join(self.listos, h0 + ".meta.json")))
        with open(os.path.join(self.listos, h1 + ".meta.json"), encoding="utf-8") as f:
            self.assertEqual(json.load(f)["pipeline"], self.nueva)
        self.assertFalse(os.path.exists(job1))

    def test_otra_frecuencia_se_vuelve_a_procesar(self):
        src = self.fuente("tema48b.wav", 48000)
        h0, _, r0 = self.corre(src, self.vieja, None, ())
        antes = self.lee(r0)
        primera = self.llamadas
        h1, _, r1 = self.corre(src, self.nueva, self.filtro, self.vecinas)
        self.assertGreater(primera, 0)
        self.assertGreater(self.llamadas, primera, "la de 48 kHz debia volver a pasar por Apollo")
        self.assertNotEqual(self.lee(r1), antes)
        self.assertEqual(self.lee(r0), antes, "el resultado viejo no se toca: lo retira la limpieza por uso")

    def test_marca_orig_se_reaprovecha(self):
        src = self.fuente("larga.wav", FS)
        h0, _ = self.trabajo(src, self.vieja)
        daemon._marca_original(self.listos, h0, "dura 17 min; el tope de la IA es 15")
        def prohibido(x):
            raise AssertionError("no debe procesarse")
        h1, _, r1 = self.corre(src, self.nueva, self.filtro, self.vecinas, proc=prohibido)
        self.assertEqual(r1, os.path.join(self.listos, h1 + ".orig"))
        with open(r1, encoding="utf-8") as f:
            self.assertIn("17 min", f.read())
        self.assertFalse(os.path.exists(os.path.join(self.listos, h0 + ".orig")))

    def test_primero_lo_que_se_reaprovecha(self):
        a48 = self.fuente("a48.wav", 48000)
        b441 = self.fuente("b441.wav", FS)
        self.corre(b441, self.vieja, None, ())          # resultado viejo de la de 44,1 kHz
        ha, job_a = self.trabajo(a48, self.nueva)
        hb, job_b = self.trabajo(b441, self.nueva)
        viejo = time.time() - 60
        os.utime(job_a, (viejo, viejo))                  # la de 48 kHz llego ANTES
        orden = []
        original = daemon.procesar_uno
        def registra(job, *args, **kw):
            orden.append(os.path.basename(job))
            return original(job, *args, **kw)
        with mock.patch.object(daemon, "procesar_uno", registra):
            hechos = daemon._un_ciclo_log(self.pend, self.listos, self.proc, FFMPEG, self.nueva, FFPROBE,
                                          20.0, self.cache, .75, self.filtro, self.vecinas)
        self.assertEqual(hechos, 2)
        self.assertEqual(orden, [hb + ".job", ha + ".job"])

    def test_sin_vecinas_el_orden_de_siempre(self):
        a = self.fuente("a.wav", FS)
        b = self.fuente("b.wav", FS)
        ha, job_a = self.trabajo(a, self.vieja)
        hb, job_b = self.trabajo(b, self.vieja)
        viejo = time.time() - 60
        os.utime(job_b, (viejo, viejo))
        orden = []
        original = daemon.procesar_uno
        def registra(job, *args, **kw):
            orden.append(os.path.basename(job))
            return original(job, *args, **kw)
        with mock.patch.object(daemon, "procesar_uno", registra):
            daemon._un_ciclo_log(self.pend, self.listos, self.proc, FFMPEG, self.vieja, FFPROBE,
                                 20.0, self.cache, .75)
        self.assertEqual(orden, [hb + ".job", ha + ".job"])


@unittest.skipUnless(HAY_FF, "requiere FFmpeg/ffprobe")
class LotesTests(unittest.TestCase):
    """05_procesar_todo: al cambiar de remuestreador solo se rehace lo que cambia."""

    def setUp(self):
        self.ident, self.filtro = util_audio.remuestreador(FFMPEG)
        if self.ident == util_audio.REMUESTREO_HEREDADO:
            self.skipTest("este FFmpeg solo ejecuta el remuestreo de siempre")
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.musica = os.path.join(self.tmp.name, "musica")
        self.salida = os.path.join(self.tmp.name, "salida")
        os.mkdir(self.musica)
        for nombre, fs in (("a441.wav", FS), ("b48.wav", 48000)):
            t = np.arange(int(fs * 1.7)) / float(fs)
            s = .1 * np.sin(2 * np.pi * 523 * t) + .08 * np.sin(2 * np.pi * 5003 * t)
            escribir_wav_float(os.path.join(self.musica, nombre), np.stack((s, -s), axis=1), fs)
        self.procesadas = []

    def corre(self, remuestreo):
        def proc(x):
            self.procesadas.append(len(x))
            return x * .95
        proc.model_fingerprint = "e" * 64
        args = ["procesar", "--musica", self.musica, "--salida", self.salida, "--formato", "wav",
                "--seg", ".75", "--mezcla", "70", "--csv", os.path.join(self.tmp.name, "sin.csv")]
        with mock.patch.object(sys, "argv", args), \
             mock.patch.object(offline, "remuestreador", return_value=remuestreo), \
             mock.patch.object(modelo_apollo, "resolver_modelo", return_value={}), \
             mock.patch.object(modelo_apollo, "identidad_modelo", return_value="e" * 64), \
             mock.patch.object(modelo_apollo, "cargar", return_value=proc), \
             mock.patch.object(memoria_audio, "memoria_disponible", return_value=16 * 1024**3):
            self.assertEqual(offline.main(), 0)

    def manifiesto(self):
        return offline.cargar_manifest(os.path.join(self.salida, "_procesadas.jsonl"))

    def test_cambio_de_remuestreador_rehace_solo_lo_que_cambia(self):
        # 1) Una corrida de ANTES: el remuestreo de siempre y un manifiesto sin el campo nuevo.
        self.corre((util_audio.REMUESTREO_HEREDADO, None))
        ruta_m = os.path.join(self.salida, "_procesadas.jsonl")
        with open(ruta_m, encoding="utf-8") as f:
            lineas = [json.loads(l) for l in f if l.strip()]
        for d in lineas:
            d["params"].pop("remuestreo", None)
        with open(ruta_m, "w", encoding="utf-8", newline="\n") as f:
            for d in lineas:
                f.write(json.dumps(d, ensure_ascii=False, sort_keys=True) + "\n")
        a_out = os.path.join(self.salida, "a441.wav")
        b_out = os.path.join(self.salida, "b48.wav")
        with open(b_out, "rb") as f:
            b_antes = f.read()
        a_antes = os.stat(a_out).st_mtime_ns
        self.assertTrue(self.procesadas)
        self.procesadas.clear()
        # 2) Con el remuestreador nuevo: la de 44,1 kHz se queda; la de 48 kHz se rehace.
        self.corre((self.ident, self.filtro))
        self.assertEqual(os.stat(a_out).st_mtime_ns, a_antes)
        with open(b_out, "rb") as f:
            self.assertNotEqual(f.read(), b_antes)
        self.assertTrue(self.procesadas, "la de 48 kHz debia pasar otra vez por Apollo")
        m = self.manifiesto()
        por_nombre = {os.path.basename(k): v["params"].get("remuestreo") for k, v in m.items()}
        self.assertEqual(por_nombre, {"a441.wav": "ninguno", "b48.wav": self.ident})
        # 3) Una tercera vez: nada pendiente.
        self.procesadas.clear()
        self.corre((self.ident, self.filtro))
        self.assertEqual(self.procesadas, [])


if __name__ == "__main__":
    unittest.main()
