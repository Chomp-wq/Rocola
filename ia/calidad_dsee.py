"""Validacion y exportacion segura del DSEE. Sin cambios al coste de inferencia."""
import os
import struct
import tempfile
import numpy as np


def cuantizar(model):
    """Valida el contrato RIA1 y evalua exactamente los pesos float32 que se guardan."""
    if not 1 <= len(model.W) <= 8 or len(model.W) != len(model.B):
        raise ValueError("Numero de capas invalido")
    previous = 28
    weights, biases = [], []
    for w, b in zip(model.W, model.B):
        with np.errstate(over="ignore", invalid="ignore"):
            w = np.asarray(w, dtype="<f4")
            b = np.asarray(b, dtype="<f4")
        if w.ndim != 2 or w.shape[1] != previous or not 1 <= w.shape[0] <= 512 or b.shape != (w.shape[0],):
            raise ValueError("Dimensiones RIA1 invalidas")
        if not np.isfinite(w).all() or not np.isfinite(b).all():
            raise ValueError("Pesos no finitos o fuera de float32")
        weights.append(w.astype(np.float64))
        biases.append(b.astype(np.float64))
        previous = w.shape[0]
    if previous != 12:
        raise ValueError("DSEE requiere 12 salidas")
    model.W, model.B = weights, biases


# La referencia clasica: una curva FIJA que cae con la octava. No es el reconstructor clasico de la
# rocola muestra a muestra (ese depende del espectro de cada cuadro); es la linea base historica del
# entrenador. Lo que si comparte con la rocola es la GEOMETRIA: el techo de las 12 sub-bandas es el
# topBin del contrato (contrato_dsee.py) en la cosecha tipica, 44,1 kHz y cuadro de 2048. Antes usaba
# 20 kHz, que no es el techo que la rocola rellena.
_REF_RATE, _REF_FRAME = 44100, 2048


def curva_fija(x):
    from contrato_dsee import bin_hz, top_bin
    fc = x[:, 25:26] * 20000.0
    if not np.isfinite(x).all() or np.any(fc <= 0) or np.any(fc >= 20947.5):
        raise ValueError("Entradas o frecuencia de corte invalidas")
    bhz = float(bin_hz(_REF_RATE, _REF_FRAME))
    cut = np.floor(fc / bhz + 0.5)
    techo = top_bin(_REF_RATE, _REF_FRAME, _REF_FRAME)      # con corte alto manda el techo fijo (952 bins)
    top = np.minimum(techo, 2 * cut - 2) * bhz
    top = np.maximum(top, fc * (1 + 1e-6))                  # un par valido siempre tiene techo por encima
    return np.clip(-1.8 * np.log2(top / fc) * ((np.arange(12) + .5) / 12), -4, .1)


def evaluar(model, x, y, lote=4096):
    """MSE por banda; recorrido acotado tambien para los datos mapeados."""
    if lote <= 0 or len(x) == 0 or len(x) != len(y):
        raise ValueError("Validacion vacia o incompatible")
    red = np.zeros(12)
    base = np.zeros(12)
    for p in range(0, len(x), lote):
        inputs, targets = x[p:p+lote], y[p:p+lote]
        predicted = model.predict(inputs)
        if targets.shape != (len(inputs), 12) or predicted.shape != targets.shape:
            raise ValueError("Salidas incompatibles")
        with np.errstate(over="ignore", invalid="ignore"):
            er = (predicted - targets) ** 2
            eb = (curva_fija(inputs) - targets) ** 2
        if not np.isfinite(er).all() or not np.isfinite(eb).all():
            raise ValueError("Error no finito")
        red += er.sum(axis=0)
        base += eb.sum(axis=0)
    red /= len(x)
    base /= len(x)
    if not np.isfinite(red).all() or not np.isfinite(base).all():
        raise ValueError("Acumulacion no finita")
    # Politica conservadora, no garantia perceptual: ganar 5% global sin empeorar
    # ninguna banda mas del 10%. La tolerancia solo evita redondeos cerca de cero.
    accepted = (base.mean() > 1e-12 and red.mean() <= base.mean() * .95
                and np.all(red <= base * 1.10 + 1e-12))
    return bool(accepted), red, base


def escribir_atomico(path, writer):
    path = os.path.abspath(path)
    fd, temporary = tempfile.mkstemp(prefix=".ria-", suffix=".tmp", dir=os.path.dirname(path))
    try:
        with os.fdopen(fd, "wb") as f:
            writer(f)
            f.flush()
            os.fsync(f.fileno())
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def exportar(model, path):
    cuantizar(model)
    def write(f):
        f.write(b"RIA1")
        f.write(struct.pack("<i", len(model.W)))
        for w, b in zip(model.W, model.B):
            f.write(struct.pack("<ii", *w.shape))
            f.write(w.astype("<f4").tobytes())
            f.write(b.astype("<f4").tobytes())
    escribir_atomico(path, write)
