"""Integracion opcional con pesos/GPU reales; nunca escribe en musica o cache instalada.

Genera una degradacion CONTROLADA a 22,05 kHz de 12 s de musica real para forzar
inferencia. Los pares no demuestran preferencia audible ni reconstruccion fiel.
No ejecutar durante un benchmark grafico o entrenamiento.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import random
import shutil
import subprocess
import sys
import tempfile
import time


def digest(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--entrada", required=True, help="cancion original, solo lectura")
    parser.add_argument("--paquete", required=True, help="carpeta ia con upscale y tiempo_real coherentes")
    parser.add_argument("--apollo", required=True, help="codigo Apollo instalado, solo lectura")
    a = parser.parse_args()
    source = str(Path(a.entrada).resolve(strict=True))
    package = Path(a.paquete).resolve(strict=True)
    if not Path(source).is_file():
        raise ValueError("La entrada debe ser un archivo")
    ff, fp = shutil.which("ffmpeg"), shutil.which("ffprobe")
    if not ff or not fp:
        raise RuntimeError("Falta FFmpeg/ffprobe")
    sys.path.insert(0, str(package / "upscale"))
    import numpy as np
    import modelo_apollo
    import util_audio as audio
    spec = importlib.util.spec_from_file_location("daemon_real_aislado", package / "tiempo_real" / "daemon_ia.py")
    daemon = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(daemon)
    before = digest(source)
    result = Path(tempfile.mkdtemp(prefix="rocola-apollo-real-"))
    original, damaged = result / "fragmento_original.wav", result / "fragmento_controlado_22k.wav"
    for path, rate in ((original, 44100), (damaged, 22050)):
        subprocess.run([ff, "-nostdin", "-v", "error", "-threads", "1", "-i", source,
                        "-ss", "20", "-t", "12", "-ac", "2", "-ar", str(rate),
                        "-c:a", "pcm_f32le", str(path)], check=True, timeout=120,
                       stdin=subprocess.DEVNULL, creationflags=audio.NOWIN)
    proc = modelo_apollo.cargar(apollo_dir=a.apollo)
    state = daemon.estado_remuestreo(ff, proc.model_fingerprint, 6.0)
    pend, ready = daemon._dirs(str(result / "cache"))
    key = daemon.hash_trabajo(str(damaged), "70", state["pipeline"])
    job = Path(pend) / (key + ".job")
    job.write_text(json.dumps(dict(src=str(damaged), mezcla="70", mezcla_key="70",
                                  pipeline=state["pipeline"], cache_key=key)), encoding="utf-8")
    calls = []
    def measured(x):
        calls.append(len(x))
        return proc(x)
    started = time.perf_counter()
    output = daemon.procesar_uno(str(job), ready, measured, ff, state["pipeline"], fp,
                                seg=6.0, filtro=state["filtro"])
    elapsed = time.perf_counter() - started
    if not isinstance(output, str) or not output.endswith(".wav") or job.exists() or not calls:
        raise RuntimeError("No se publico WAV flotante de una inferencia real: " + str(output))
    x_path = result / "fragmento_controlado_44k.wav"
    audio.decodificar_a_wav(ff, str(damaged), str(x_path))
    x, fs = audio.leer_wav(str(x_path))
    y, yfs = audio.leer_wav(output)
    if len(x) < 8 * fs or fs != 44100 or yfs != fs or y.shape != x.shape or not np.isfinite(y).all():
        raise RuntimeError("Formato, duracion o muestras de salida invalidos")
    probe = subprocess.run([fp, "-v", "error", "-select_streams", "a:0", "-show_entries",
                            "stream=codec_name", "-of", "json", output], capture_output=True,
                           text=True, check=True, timeout=30, creationflags=audio.NOWIN)
    if json.loads(probe.stdout)["streams"][0]["codec_name"] != "pcm_f32le":
        raise RuntimeError("La cache no es WAV float32")
    if np.array_equal(x, y):
        raise RuntimeError("La inferencia no cambio ninguna muestra")
    y = audio.igualar_rms(y, x)
    margin = min(audio.ganancia_wav(x), audio.ganancia_wav(y))
    blind = result / "ciego"
    blind.mkdir()
    variants = [("controlado sin Apollo", x), ("controlado Apollo 70%", y)]
    random.SystemRandom().shuffle(variants)
    for letter, (_, signal) in zip(("X", "Y"), variants):
        audio.escribir_wav_float(str(blind / (letter + ".wav")), signal * margin)
    (blind / "clave.txt").write_text("\n".join(letter + "=" + label for letter, (label, _) in
                                              zip(("X", "Y"), variants)) + "\n", encoding="utf-8")
    if digest(source) != before:
        raise RuntimeError("La entrada original cambio durante la prueba")
    report = dict(checkpoint_sha256=proc.checkpoint_sha256, modelo=proc.model_fingerprint,
                  pipeline=state["pipeline"], remuestreo=state["remuestreo"],
                  inferencias=len(calls), muestras=len(y), canales=y.shape[1], fs=yfs,
                  segundos=round(elapsed, 3), entrada_sha256=before, original_sin_cambios=True,
                  degradacion_controlada=True, preferencia_audible="no evaluada", salida=str(result))
    (result / "informe.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
