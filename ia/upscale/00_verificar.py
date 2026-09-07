#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# PASO 0: verifica que TU maquina (la de la RTX 4070 Super) tenga todo listo, ANTES de correr nada.
#   python 00_verificar.py            -> revisa entorno
#   python 00_verificar.py --descargar -> ademas descarga el modelo Apollo (una vez, ~180MB)
import sys, os, shutil, subprocess

OK, MAL = "  [OK] ", "  [X]  "
fallas = 0
def rev(cond, bien, mal):
    global fallas
    print((OK + bien) if cond else (MAL + mal))
    if not cond: fallas += 1

print("== Verificacion del entorno de upscaling ==")
rev(sys.version_info >= (3, 9), "Python %d.%d" % sys.version_info[:2], "Se requiere Python 3.9+")

try:
    import numpy; rev(True, "numpy " + numpy.__version__, "")
except ImportError: rev(False, "", "Falta numpy:  pip install numpy")

try:
    import torch
    rev(True, "torch " + torch.__version__, "")
    cuda = torch.cuda.is_available()
    rev(cuda, ("CUDA OK: " + torch.cuda.get_device_name(0)) if cuda else "",
        "torch SIN CUDA. Reinstala:  pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu124")
except ImportError:
    rev(False, "", "Falta torch:  pip install torch torchaudio --index-url https://download.pytorch.org/whl/cu124")

try:
    import torchaudio; rev(True, "torchaudio " + torchaudio.__version__, "")
except ImportError: rev(False, "", "Falta torchaudio (se instala junto con torch, ver arriba)")

ff = shutil.which("ffmpeg")
rev(ff is not None, "ffmpeg: " + str(ff), "Falta ffmpeg:  winget install Gyan.FFmpeg  (y reabre la consola)")
fp = shutil.which("ffprobe")
if ff and not fp:
    junto = os.path.join(os.path.dirname(ff), "ffprobe.exe" if os.name == "nt" else "ffprobe")
    fp = junto if os.path.isfile(junto) else None
rev(fp is not None, "ffprobe: " + str(fp), "Falta ffprobe junto a ffmpeg")
for nombre, exe in (("ffmpeg", ff), ("ffprobe", fp)):
    if exe:
        try:
            subprocess.run([exe, "-version"], check=True, stdin=subprocess.DEVNULL,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=15)
        except Exception as e:
            rev(False, "", "%s no funciona (%s)" % (nombre, e))

apollo = os.environ.get("APOLLO_DIR", os.path.join(os.path.dirname(os.path.abspath(__file__)), "Apollo"))
rev(os.path.isdir(apollo), "Repo Apollo: " + apollo,
    "Falta el repo Apollo:  git clone https://github.com/JusperLee/Apollo  (aqui, en ia/upscale/)")
if os.path.isdir(apollo):
    sys.path.insert(0, apollo)
    try:
        import look2hear.models
        rev(True, "look2hear importa", "")
    except Exception as e:
        rev(False, "", "look2hear no importa (%s). Prueba:  pip install -r Apollo/requirements.txt" % e)
    if "--descargar" in sys.argv:
        print("  ... descargando + cargando Apollo (Hugging Face: JusperLee/Apollo). La 1a vez baja ~180 MB.")
        try:
            import modelo_apollo
            proc = modelo_apollo.cargar(apollo_dir=apollo)
            import numpy as _np
            y = proc(_np.zeros((44100, 2), dtype=_np.float64))   # prueba real: procesa 1 segundo de silencio
            rev(y is not None and y.shape[1] == 2, "Apollo cargado y probado en la GPU", "Apollo cargo pero la prueba fallo")
        except Exception as e:
            rev(False, "", "No se pudo cargar Apollo (%s)" % e)

print("== %s ==" % ("TODO LISTO: corre 01_piloto.py" if fallas == 0 else "%d pendiente(s) arriba" % fallas))
sys.exit(fallas)
