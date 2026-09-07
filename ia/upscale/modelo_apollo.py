# -*- coding: utf-8 -*-
"""Carga segura y reproducible del modelo Apollo.

Nunca se deserializa pickle arbitrario: safetensors se carga directamente y los
checkpoints de PyTorch pasan por ``weights_only=True``. Los repos remotos siempre
se descargan desde una revision inmutable.
"""
import glob
import hashlib
import os
import sys

import numpy as np

DEFAULT_SOURCE = "JusperLee/Apollo"
DEFAULT_REVISION = "0b123a4606d5345d9082f6f633d8bfebe84c8e79"
DEFAULT_CHECKPOINT_SHA256 = "99d9af7f1ff20e63c393035513a655392818d66b4d7fc23d658175c1f15e8d76"
PIPELINE_VERSION = "apollo-safe-v2"


def _revision_inmutable(revision):
    """Hugging Face acepta ramas y tags; aqui solo aceptamos un commit Git completo."""
    return (isinstance(revision, str) and len(revision) in (40, 64) and
            all(c in "0123456789abcdefABCDEF" for c in revision))


def _repo_apollo(apollo_dir=None):
    return os.path.abspath(apollo_dir or os.environ.get("APOLLO_DIR") or
                           os.path.join(os.path.dirname(os.path.abspath(__file__)), "Apollo"))


def fuente_modelo():
    """Devuelve ``(fuente, revision)``; repos remotos exigen una revision fija."""
    fuente = os.environ.get("APOLLO_MODEL", "").strip()
    revision = os.environ.get("APOLLO_MODEL_REVISION", "").strip()
    cfg = os.path.join(os.path.dirname(os.path.abspath(__file__)), "modelo.txt")
    if not fuente and os.path.exists(cfg):
        with open(cfg, encoding="utf-8") as f:
            for linea in f:
                s = linea.strip()
                if s and not s.startswith("#"):
                    fuente = s
                    break
    fuente = fuente or DEFAULT_SOURCE
    # Una ruta relativa escrita en modelo.txt se interpreta junto al propio archivo, no segun
    # el directorio desde el que por casualidad se haya lanzado Python.
    if fuente != DEFAULT_SOURCE and not os.path.isabs(fuente):
        local = os.path.abspath(os.path.join(os.path.dirname(cfg), fuente))
        if os.path.exists(local):
            fuente = local
    if not os.path.exists(fuente) and "@" in fuente:
        fuente, revision_cfg = fuente.rsplit("@", 1)
        revision = revision or revision_cfg
    if not os.path.exists(fuente):
        if fuente == DEFAULT_SOURCE:
            revision = revision or DEFAULT_REVISION
        if not _revision_inmutable(revision):
            raise RuntimeError("El modelo remoto requiere el hash completo de un commit: usuario/repo@commit")
    return fuente, revision or None


def _sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for bloque in iter(lambda: f.read(1024 * 1024), b""):
            h.update(bloque)
    return h.hexdigest()


def descargar_checkpoint(fuente=None, revision=None):
    """Devuelve ``(checkpoint_local, fuente, revision_efectiva)``."""
    if fuente is None:
        fuente, revision_cfg = fuente_modelo()
        revision = revision or revision_cfg
    if os.path.isfile(fuente):
        return os.path.abspath(fuente), os.path.abspath(fuente), None
    if os.path.isdir(fuente):
        carpeta = os.path.abspath(fuente)
    else:
        if not _revision_inmutable(revision):
            raise RuntimeError("Se nego descargar un modelo remoto sin el hash completo de un commit")
        from huggingface_hub import snapshot_download
        carpeta = snapshot_download(repo_id=fuente, revision=revision,
                                     allow_patterns=["*.safetensors", "*.ckpt", "*.bin", "*.pt", "*.pth"])
    candidatos = []
    for ext in ("*.safetensors", "*.ckpt", "*.bin", "*.pt", "*.pth"):
        candidatos.extend(glob.glob(os.path.join(carpeta, "**", ext), recursive=True))
    if not candidatos:
        raise RuntimeError("No encontre checkpoint en: " + carpeta)
    candidatos.sort(key=lambda p: (0 if p.lower().endswith(".safetensors") else 1,
                                   0 if os.path.basename(p).lower() in ("pytorch_model.bin", "apollo.pth") else 1,
                                   -os.path.getsize(p), p.lower()))
    ruta = candidatos[0]
    digest = _sha256(ruta)
    if fuente == DEFAULT_SOURCE and revision == DEFAULT_REVISION and digest != DEFAULT_CHECKPOINT_SHA256:
        raise RuntimeError("El checkpoint oficial no coincide con su SHA-256 fijado; se nego cargarlo")
    return ruta, fuente, revision


def _cargar_checkpoint_seguro(ruta, digest=None):
    digest_real = _sha256(ruta)
    if digest is not None and digest_real != digest:
        raise RuntimeError("El checkpoint cambio despues de resolverlo; se nego cargarlo")
    if ruta.lower().endswith(".safetensors"):
        from safetensors.torch import load_file
        return load_file(ruta, device="cpu")
    import torch
    # El checkpoint oficial contiene metadatos OmegaConf que el cargador weights-only no admite.
    # Es la unica excepcion: se permite pickle SOLO despues de comparar todos sus bytes con el
    # SHA-256 inmutable publicado y fijado arriba. Un archivo remoto alterado no llega aqui.
    if digest_real == DEFAULT_CHECKPOINT_SHA256:
        return torch.load(ruta, map_location="cpu", weights_only=False)
    try:
        return torch.load(ruta, map_location="cpu", weights_only=True)
    except TypeError:
        raise RuntimeError("PyTorch es demasiado antiguo: se requiere torch.load(weights_only=True)")
    except Exception as e:
        raise RuntimeError("Checkpoint rechazado por el cargador seguro; conviertelo a safetensors: %s" % e)


def _state_dict(datos):
    if isinstance(datos, dict) and isinstance(datos.get("state_dict"), dict):
        datos = datos["state_dict"]
    if not isinstance(datos, dict) or not datos or not all(isinstance(k, str) for k in datos):
        raise RuntimeError("El checkpoint no contiene un state_dict")
    return datos


def _ajusta_prefijo(sd, esperadas):
    """Quita solamente un prefijo comun; nunca oculta claves incompatibles."""
    for prefijo in ("", "audio_model.", "model.", "module."):
        if prefijo and not all(k.startswith(prefijo) for k in sd):
            continue
        candidato = {(k[len(prefijo):] if prefijo else k): v for k, v in sd.items()}
        if set(candidato) == esperadas:
            return candidato
    faltan = sorted(esperadas - set(sd))[:5]
    sobran = sorted(set(sd) - esperadas)[:5]
    raise RuntimeError("Pesos incompatibles (faltan %s; sobran %s)" % (faltan, sobran))


def resolver_modelo():
    ruta, fuente, revision = descargar_checkpoint()
    digest = _sha256(ruta)
    identidad = "%s|%s|%s|%s" % (PIPELINE_VERSION, fuente, revision or "local", digest)
    return ruta, fuente, revision, digest, hashlib.sha256(identidad.encode("utf-8")).hexdigest()


def _huella_codigo(repo):
    """Incluye el codigo Apollo local en la identidad para no reutilizar audio de otra version."""
    h = hashlib.sha256()
    base = os.path.join(repo, "look2hear")
    if not os.path.isdir(base):
        raise RuntimeError("El repo Apollo no contiene look2hear: " + repo)
    rutas = []
    for carpeta, dirs, files in os.walk(base):
        dirs[:] = sorted(d for d in dirs if d != "__pycache__" and not d.startswith("."))
        rutas.extend(os.path.join(carpeta, f) for f in files if f.endswith(".py"))
    for ruta in sorted(rutas, key=lambda p: os.path.relpath(p, repo).replace("\\", "/")):
        rel = os.path.relpath(ruta, repo).replace("\\", "/")
        h.update(rel.encode("utf-8")); h.update(b"\0")
        with open(ruta, "rb") as f:
            for bloque in iter(lambda: f.read(1024 * 1024), b""):
                h.update(bloque)
        h.update(b"\0")
    return h.hexdigest()


def identidad_modelo(apollo_dir=None, modelo_resuelto=None):
    """Valida checkpoint/codigo y obtiene la misma huella sin importar Torch ni crear Apollo."""
    repo = _repo_apollo(apollo_dir)
    ruta, fuente, revision, digest, fingerprint = modelo_resuelto or resolver_modelo()
    digest_real = _sha256(ruta)
    if digest_real != digest:
        raise RuntimeError("El checkpoint cambio despues de resolverlo; se nego cargarlo")
    identidad_base = "%s|%s|%s|%s" % (PIPELINE_VERSION, fuente, revision or "local", digest_real)
    fingerprint_base = hashlib.sha256(identidad_base.encode("utf-8")).hexdigest()
    if fingerprint != fingerprint_base:
        raise RuntimeError("La identidad resuelta del modelo no es valida")
    return hashlib.sha256((fingerprint_base + "|" + _huella_codigo(repo)).encode("ascii")).hexdigest()


def lista_gpus():
    try:
        import torch
        if not torch.cuda.is_available():
            return []
        return [torch.cuda.get_device_name(i) for i in range(torch.cuda.device_count())]
    except Exception:
        return []


def _dispositivo(gpu):
    import torch
    if not torch.cuda.is_available():
        return torch.device("cpu")
    try:
        n = int(gpu)
        if 0 <= n < torch.cuda.device_count():
            return torch.device("cuda:%d" % n)
        print("   aviso: no existe la GPU %s; uso la automatica" % gpu)
    except (TypeError, ValueError):
        pass
    return torch.device("cuda")


def cargar(gpu="auto", apollo_dir=None, modelo_resuelto=None):
    """Devuelve ``proc`` y publica la identidad en ``proc.model_fingerprint``."""
    repo = _repo_apollo(apollo_dir)
    if not os.path.isdir(repo):
        raise RuntimeError("No encuentro el codigo Apollo: " + repo)
    if repo not in sys.path:
        sys.path.insert(0, repo)
    import torch
    try:
        torch.set_num_threads(2)
    except Exception:
        pass
    try:
        from look2hear.models.apollo import Apollo
    except Exception as e:
        raise RuntimeError("No pude importar Apollo desde %s: %s" % (repo, e))
    try:
        import inspect
        origen_apollo = os.path.realpath(inspect.getfile(Apollo))
        if os.path.commonpath((origen_apollo, os.path.realpath(repo))) != os.path.realpath(repo):
            raise RuntimeError("se importo Apollo desde otra carpeta: " + origen_apollo)
    except RuntimeError:
        raise
    except Exception as e:
        raise RuntimeError("No pude validar el origen del codigo Apollo: %s" % e)

    ruta, fuente, revision, digest, fingerprint = modelo_resuelto or resolver_modelo()
    fingerprint = identidad_modelo(repo, (ruta, fuente, revision, digest, fingerprint))
    print("   modelo de IA:", fuente, ("@" + revision) if revision else "")
    print("   checkpoint SHA-256:", digest)
    cfg = dict(sr=44100, win=20, feature_dim=256, layer=6)
    model = Apollo(**cfg)
    sd = _state_dict(_cargar_checkpoint_seguro(ruta, digest))
    sd = _ajusta_prefijo(sd, set(model.state_dict().keys()))
    model.load_state_dict(sd, strict=True)

    dev = _dispositivo(gpu)
    try:
        nombre = torch.cuda.get_device_name(dev) if dev.type == "cuda" else "CPU"
    except Exception:
        nombre = str(dev)
    print("   dispositivo:", dev, "-", nombre)
    model = model.to(dev).eval()

    def proc(x):
        t = torch.from_numpy(np.ascontiguousarray(x.T, dtype=np.float32)).unsqueeze(0).to(dev)
        with torch.no_grad():
            try:
                y = model(t)
            except Exception:
                y = torch.cat([model(t[:, c:c+1, :]) for c in range(t.shape[1])], dim=1)
        return y.squeeze(0).detach().cpu().numpy().T.astype(np.float64)

    proc.model_fingerprint = fingerprint
    proc.checkpoint_sha256 = digest
    return proc
