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

apollo = os.environ.get("APOLLO_DIR", os.path.join(os.path.dirname(os.path.abspath(__file__)), "Apollo"))
rev(os.path.isdir(apollo), "Repo Apollo: " + apollo,
    "Falta el repo Apollo:  git clone https://github.com/JusperLee/Apollo  (aqui, en ia/upscale/)")
if os.path.isdir(apollo):
    sys.path.insert(0, apollo)
    try:
        import look2hear.models
        rev(True, "look2hear importa", "")
        if "--descargar" in sys.argv:
            print("  ... descargando/cargando el checkpoint Apollo (Hugging Face: JusperLee/Apollo)")
            m = look2hear.models.BaseModel.from_pretrain("JusperLee/Apollo", sr=44100, win=20, feature_dim=256, layer=6)
            rev(m is not None, "Checkpoint Apollo cargado", "No se pudo cargar el checkpoint")
    except Exception as e:
        rev(False, "", "look2hear no importa (%s). Prueba:  pip install -r Apollo/requirements.txt" % e)

print("== %s ==" % ("TODO LISTO: corre 01_piloto.py" if fallas == 0 else "%d pendiente(s) arriba" % fallas))
sys.exit(fallas)
