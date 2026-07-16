# -*- coding: utf-8 -*-
# Carga de Apollo, ROBUSTA. El from_pretrain de Apollo solo hace torch.load(ruta): NO baja de
# internet (por eso "JusperLee/Apollo" daba "No such file or directory"). Aqui descargamos el
# repo de Hugging Face a la cache, encontramos el checkpoint y le pasamos la RUTA LOCAL.
import os, sys, glob
import numpy as np

def _repo_apollo():
    return os.path.join(os.path.dirname(os.path.abspath(__file__)), "Apollo")

def descargar_checkpoint():
    """Baja el modelo de Hugging Face (cache; una vez) y devuelve la ruta LOCAL del checkpoint."""
    from huggingface_hub import snapshot_download
    carpeta = snapshot_download(repo_id="JusperLee/Apollo")
    import torch
    cand = []
    for ext in ("*.ckpt", "*.bin", "*.pt", "*.pth", "*.safetensors"):
        cand += glob.glob(os.path.join(carpeta, "**", ext), recursive=True)
    cand.sort(key=lambda f: -os.path.getsize(f))            # el checkpoint suele ser el archivo mas grande
    for p in cand:                                          # preferir el que traiga state_dict/model_name
        try:
            d = torch.load(p, map_location="cpu", weights_only=False)
            if isinstance(d, dict) and ("state_dict" in d or "model_name" in d):
                return p
        except Exception:
            continue
    if cand:
        return cand[0]
    raise RuntimeError("Descargue el repo pero no encontre checkpoint en: " + carpeta)

def cargar():
    """Devuelve proc(x[n,ch] float64) -> y[n,ch] float64, con Apollo listo en la GPU."""
    sys.path.insert(0, _repo_apollo())
    import torch
    import look2hear.models
    ruta = descargar_checkpoint()
    cfg = dict(sr=44100, win=20, feature_dim=256, layer=6)
    try:
        model = look2hear.models.BaseModel.from_pretrain(ruta, **cfg)     # camino documentado, ya con ruta local
    except Exception as e1:
        # Respaldo: construir el modelo Apollo y cargar los pesos a mano (por si el checkpoint es
        # un .ckpt de Lightning sin la envoltura model_name/state_dict que espera from_pretrain).
        try:
            from look2hear.models.apollo import Apollo
        except Exception:
            raise e1
        model = Apollo(**cfg)
        d = torch.load(ruta, map_location="cpu", weights_only=False)
        sd = d.get("state_dict", d) if isinstance(d, dict) else d
        sd = { (k[len("audio_model."):] if k.startswith("audio_model.") else k): v for k, v in sd.items() }
        model.load_state_dict(sd, strict=False)
    model = model.cuda().eval()

    def proc(x):
        t = torch.from_numpy(np.ascontiguousarray(x.T, dtype=np.float32)).unsqueeze(0).cuda()  # [1, ch, n]
        with torch.no_grad():
            try:
                y = model(t)
            except Exception:                               # si el modelo exige mono: canal por canal
                y = torch.cat([model(t[:, c:c+1, :]) for c in range(t.shape[1])], dim=1)
        return y.squeeze(0).detach().cpu().numpy().T.astype(np.float64)
    return proc
