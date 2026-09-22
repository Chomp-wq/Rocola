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


# ---------------------------------------------------------------------------------------------
#  El modelo INSTALADO: leerlo exactamente como lo leeria la rocola (RedChica.TryLoad)
# ---------------------------------------------------------------------------------------------
class RedRia1:
    """Un modelo RIA1 ya guardado: capas ocultas tanh y salida lineal, pesos float32."""
    def __init__(self, W, B):
        self.W, self.B = W, B

    def predict(self, x):
        a = np.asarray(x, dtype=np.float64)
        for i, (w, b) in enumerate(zip(self.W, self.B)):
            a = a @ w.T + b
            if i < len(self.W) - 1:
                a = np.tanh(a)
        return a

    def parametros(self):
        return int(sum(w.size + b.size for w, b in zip(self.W, self.B)))


def cargar_ria1(path, entradas=28, salidas=12):
    """Mismas reglas que RedChica.TryLoad: 12 bytes a 9 MiB, cabecera RIA1, de 1 a 8 capas, filas y
    columnas de 1 a 512, todo finito, sin bytes sobrantes y dimensiones encadenadas. Lanza ValueError
    con el motivo si la rocola NO lo cargaria (y entonces la rocola usa su curva fija)."""
    tam = os.path.getsize(path)
    if tam < 12 or tam > 9 * 1024 * 1024:
        raise ValueError("tamano fuera de lo que la rocola acepta")
    with open(path, "rb") as f:
        d = f.read()
    if d[:4] != b"RIA1":
        raise ValueError("no empieza por RIA1")
    nl = struct.unpack_from("<i", d, 4)[0]
    if not 1 <= nl <= 8:
        raise ValueError("numero de capas invalido")
    p, W, B = 8, [], []
    for _ in range(nl):
        if len(d) - p < 8:
            raise ValueError("archivo truncado")
        r, c = struct.unpack_from("<ii", d, p)
        p += 8
        if not (1 <= r <= 512 and 1 <= c <= 512) or len(d) - p < (r * c + r) * 4:
            raise ValueError("dimensiones invalidas o archivo truncado")
        w = np.frombuffer(d, dtype="<f4", count=r * c, offset=p).reshape(r, c); p += r * c * 4
        b = np.frombuffer(d, dtype="<f4", count=r, offset=p); p += r * 4
        if not (np.isfinite(w).all() and np.isfinite(b).all()):
            raise ValueError("pesos no finitos")
        W.append(w.astype(np.float64)); B.append(b.astype(np.float64))
    if p != len(d):
        raise ValueError("bytes sobrantes al final")
    if W[0].shape[1] != entradas or W[-1].shape[0] != salidas:
        raise ValueError("no es un modelo de %d entradas y %d salidas" % (entradas, salidas))
    for a, b in zip(W, W[1:]):
        if b.shape[1] != a.shape[0]:
            raise ValueError("capas que no encajan")
    return RedRia1(W, B)


def bytes_ria1(model):
    """Los bytes exactos que escribiria exportar(), sin tocar el disco (para su huella)."""
    out = [b"RIA1", struct.pack("<i", len(model.W))]
    for w, b in zip(model.W, model.B):
        out.append(struct.pack("<ii", *w.shape))
        out.append(np.asarray(w, dtype="<f4").tobytes())
        out.append(np.asarray(b, dtype="<f4").tobytes())
    return b"".join(out)


# ---------------------------------------------------------------------------------------------
#  Candidato, referencia clasica y modelo instalado, sobre EL MISMO conjunto
# ---------------------------------------------------------------------------------------------
# Politica INICIAL, escrita como tal: son margenes conservadores para no publicar regresiones, no una
# prueba de que el modelo suene mejor. Eso solo se sabe escuchando A/B a volumen igualado.
POLITICA = {
    "vs_referencia": {"mejora_global_min": 0.05, "peor_banda_max": 0.10},
    "vs_instalado": {"mejora_global_min": 0.02, "peor_banda_max": 0.10},
    "tolerancia": 1e-12,
    "nota": "politica inicial; no demuestra superioridad audible",
}


def evaluar_tres(model, instalado, x, y, lote=4096):
    """MSE por banda del candidato, de la curva fija y (si lo hay) del instalado, por lotes."""
    if lote <= 0 or len(x) == 0 or len(x) != len(y):
        raise ValueError("Conjunto vacio o incompatible")
    sumas = {"candidato": np.zeros(12), "referencia": np.zeros(12)}
    if instalado is not None:
        sumas["instalado"] = np.zeros(12)
    for p in range(0, len(x), lote):
        xs, ys = x[p:p + lote], y[p:p + lote]
        if ys.shape != (len(xs), 12):
            raise ValueError("Salidas incompatibles")
        with np.errstate(over="ignore", invalid="ignore"):
            partes = {"candidato": model.predict(xs), "referencia": curva_fija(xs)}
            if instalado is not None:
                partes["instalado"] = instalado.predict(xs)
            for k, pred in partes.items():
                if pred.shape != ys.shape:
                    raise ValueError("Salidas incompatibles")
                e = (pred - ys) ** 2
                if not np.isfinite(e).all():
                    raise ValueError("Error no finito (%s)" % k)
                sumas[k] += e.sum(axis=0)
    return {k: v / len(x) for k, v in sumas.items()}


def _cumple(err, contra, reglas, tol):
    if contra.mean() <= tol:
        return False, "la referencia ya es practicamente perfecta: no hay mejora que medir"
    if err.mean() > contra.mean() * (1.0 - reglas["mejora_global_min"]):
        return False, "mejora global %.1f %% (hace falta %.0f %%)" % (
            100.0 * (1.0 - err.mean() / contra.mean()), 100.0 * reglas["mejora_global_min"])
    peor = err / np.maximum(contra, tol) - 1.0
    malas = [j for j in range(12) if err[j] > contra[j] * (1.0 + reglas["peor_banda_max"]) + tol]
    if malas:
        return False, "banda(s) %s mas de %.0f %% peor (la peor, %.1f %%)" % (
            ",".join(str(j) for j in malas), 100.0 * reglas["peor_banda_max"], 100.0 * peor.max())
    return True, "mejora global %.1f %%" % (100.0 * (1.0 - err.mean() / contra.mean()))


def decide(errores, politica=POLITICA):
    """(publicar, motivos). Contra la curva fija SIEMPRE; contra el instalado si lo hay."""
    tol = politica["tolerancia"]
    ok, m = _cumple(errores["candidato"], errores["referencia"], politica["vs_referencia"], tol)
    motivos = ["contra la curva fija: " + m]
    if "instalado" in errores:
        ok2, m2 = _cumple(errores["candidato"], errores["instalado"], politica["vs_instalado"], tol)
        motivos.append("contra el modelo instalado: " + m2)
        ok = ok and ok2
    return bool(ok), motivos
