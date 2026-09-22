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


# ---------------------------------------------------------------------------------------------
#  La inferencia EXACTAMENTE como la hace la rocola (RedChica.Predict)
# ---------------------------------------------------------------------------------------------
LIMITE_PARIDAD = 1e-3      # el mismo que exige pruebas/dsee.cs a los vectores de un modelo publicado


def prediccion_como_la_rocola(model, x):
    """RedChica.Predict en numpy: pesos y entradas float32, suma float32 en el MISMO orden
    (s = sesgo; s += w[j]*x[j] para j = 0, 1, ...), tanh de doble precision convertida a float32 en
    las capas ocultas y la ultima lineal. Devuelve (salida float64, filas no finitas): en una fila no
    finita la rocola no usa la red y se queda con la curva clasica para ese cuadro."""
    a = np.asarray(x, dtype=np.float32)
    malas = np.zeros(len(a), dtype=bool)
    with np.errstate(over="ignore", invalid="ignore"):
        for i, (w, b) in enumerate(zip(model.W, model.B)):
            w32 = np.asarray(w, dtype=np.float32)
            s = np.broadcast_to(np.asarray(b, dtype=np.float32), (len(a), w32.shape[0])).copy()
            for j in range(w32.shape[1]):
                s = (s + (a[:, j:j + 1] * w32[None, :, j]).astype(np.float32)).astype(np.float32)
            malas |= ~np.isfinite(s).all(axis=1)
            a = s if i == len(model.W) - 1 else np.tanh(s.astype(np.float64)).astype(np.float32)
    return a.astype(np.float64), malas


def paridad_inferencia(model, x, max_filas=20000):
    """Diferencia maxima entre lo que evalua el entrenador (float64) y lo que calcularia la rocola,
    sobre hasta max_filas filas repartidas por todo el conjunto (deterministas)."""
    n = len(x)
    if n == 0:
        return {"filas": 0, "max_abs": 0.0, "no_finitas": 0, "limite": LIMITE_PARIDAD}
    idx = np.unique(np.linspace(0, n - 1, min(n, max_filas)).astype(np.int64))
    xs = x[idx] if not hasattr(x, "seleccionar") else x.seleccionar(idx)[:]
    xs = np.asarray(xs, dtype=np.float64)
    ref = model.predict(xs)
    roc, malas = prediccion_como_la_rocola(model, xs)
    buenas = ~malas & np.isfinite(ref).all(axis=1)
    dif = float(np.max(np.abs(ref[buenas] - roc[buenas]))) if buenas.any() else float("inf")
    return {"filas": int(len(idx)), "max_abs": dif, "no_finitas": int(malas.sum()), "limite": LIMITE_PARIDAD}


# ---------------------------------------------------------------------------------------------
#  Errores por CLASE DE SENAL (y por transitorios cuando se sabe cuales son)
# ---------------------------------------------------------------------------------------------
# Las clases salen de las propias 28 entradas, asi que existen para todo material, tambien el de la
# rocola. Tres dimensiones por terciles del conjunto evaluado (reproducible: mismos datos, mismos
# cortes) y una por la frecuencia del corte:
#   nivel      entrada 24 (energia media, log10)                    -> suave / media / fuerte
#   tonalidad  entrada 26 (planitud de las 8 bandas bajo el corte)  -> tonal / intermedia / ruidosa
#   brillo     pendiente de las entradas 0..23                       -> oscura / media / brillante
#   corte      entrada 25 * 20 kHz                                   -> bajo 13 kHz / 13-16 kHz / desde 16 kHz
# Los TRANSITORIOS (ataques) no se pueden ver en un cuadro suelto: solo se informan cuando el material
# viene de senal (WAV o sintetico), donde se conoce el cuadro anterior.
DIMENSIONES = (("nivel", ("suave", "media", "fuerte")),
               ("tonalidad", ("tonal", "intermedia", "ruidosa")),
               ("brillo", ("oscura", "media", "brillante")),
               ("corte", ("bajo_13k", "13_16k", "desde_16k")))
_PESO_PENDIENTE = (np.arange(24) - 11.5) / np.sum((np.arange(24) - 11.5) ** 2)
MIN_FILAS_AVISO = 200


def _descriptores(xs):
    # La pendiente se suma banda a banda en un orden FIJO: con un producto de matrices el ultimo bit
    # dependeria del tamano del lote, y el informe dejaria de salir identico.
    pendiente = np.zeros(len(xs))
    for i in range(24):
        pendiente = pendiente + xs[:, i] * _PESO_PENDIENTE[i]
    return np.stack([xs[:, 24], xs[:, 26], pendiente, xs[:, 25] * 20.0], axis=1)


def _mse_filas(pred, ys):
    e = np.zeros(len(ys))
    for j in range(12):
        e = e + (pred[:, j] - ys[:, j]) ** 2
    return e / 12.0


def errores_por_clase(model, instalado, x, y, transitorio=None, lote=4096):
    """MSE medio por fila de candidato, referencia e instalado, agrupado por clase. Dos pasadas por
    lotes: memoria de unos pocos numeros por fila, nunca el conjunto entero en float64."""
    n = len(x)
    desc = np.empty((n, 4), dtype=np.float64)
    err = {"candidato": np.empty(n), "referencia": np.empty(n)}
    if instalado is not None:
        err["instalado"] = np.empty(n)
    for p in range(0, n, lote):
        xs = np.asarray(x[p:p + lote], dtype=np.float64)
        ys = np.asarray(y[p:p + lote], dtype=np.float64)
        desc[p:p + len(xs)] = _descriptores(xs)
        err["candidato"][p:p + len(xs)] = _mse_filas(model.predict(xs), ys)
        err["referencia"][p:p + len(xs)] = _mse_filas(curva_fija(xs), ys)
        if instalado is not None:
            err["instalado"][p:p + len(xs)] = _mse_filas(instalado.predict(xs), ys)
    umbrales = {}
    etiquetas = {}
    for k, (dim, nombres) in enumerate(DIMENSIONES):
        if dim == "corte":
            cortes = [13.0, 16.0]
        else:
            cortes = [float(repr(float(v))) for v in np.quantile(desc[:, k], [1.0 / 3.0, 2.0 / 3.0])]
        umbrales[dim] = cortes
        etiquetas[dim] = np.searchsorted(np.asarray(cortes), desc[:, k], side="right")
    salida = {"umbrales": umbrales}

    def resume(sel):
        r = {"filas": int(sel.sum())}
        for k in sorted(err):
            r[k] = float(repr(float(err[k][sel].mean()))) if sel.any() else None
        return r

    for dim, nombres in DIMENSIONES:
        salida[dim] = {nombre: resume(etiquetas[dim] == c) for c, nombre in enumerate(nombres)}
    if transitorio is None:
        salida["transitorios"] = {"disponible": False,
                                  "motivo": "el material de la rocola guarda cuadros sueltos, sin el anterior: "
                                            "no se puede saber cuales son ataques"}
    else:
        t = np.asarray(transitorio, dtype=bool)
        salida["transitorios"] = {"disponible": True, "transitorio": resume(t), "estable": resume(~t)}
    return salida


def avisos_por_clase(por_clase, margen=0.10, minimo=MIN_FILAS_AVISO):
    """Clases (con suficientes filas) donde el candidato queda mas de un 10 % peor que el instalado, o
    que la curva fija si no hay instalado. Es informacion para escuchar con cuidado ESE tipo de
    musica; no cambia la decision, que sigue la politica global y por banda."""
    avisos = []
    grupos = [(d, por_clase[d]) for d, _ in DIMENSIONES]
    if por_clase.get("transitorios", {}).get("disponible"):
        grupos.append(("transitorios", {k: v for k, v in por_clase["transitorios"].items() if k != "disponible"}))
    for dim, clases in grupos:
        for nombre, r in sorted(clases.items()):
            if r["filas"] < minimo or r["candidato"] is None:
                continue
            contra = "instalado" if r.get("instalado") is not None else "referencia"
            if r[contra] is not None and r["candidato"] > r[contra] * (1.0 + margen):
                avisos.append("%s=%s: el candidato es %.0f %% peor que %s (%d filas)"
                              % (dim, nombre, 100.0 * (r["candidato"] / max(r[contra], 1e-12) - 1.0),
                                 "el modelo instalado" if contra == "instalado" else "la curva fija", r["filas"]))
    return avisos


def compara_inferencia(ruta_modelo, ruta_volcado):
    """Lo que calculo la ROCOLA (volcado 'x0..x27 | y0..y11' de pruebas/iaparidad.cs) contra
    prediccion_como_la_rocola. Devuelve (filas, peor diferencia, filas identicas bit a bit)."""
    m = cargar_ria1(ruta_modelo)
    xs, ys = [], []
    with open(ruta_volcado, "r", encoding="ascii") as f:
        for linea in f:
            if "|" not in linea:
                continue
            a, b = linea.split("|")
            xs.append([float(v) for v in a.split()])
            ys.append([float(v) for v in b.split()])
    x = np.asarray(xs, dtype=np.float64)
    y = np.asarray(ys, dtype=np.float32).astype(np.float64)
    p, malas = prediccion_como_la_rocola(m, x)
    iguales = int(np.sum(np.all(p.astype(np.float32) == y.astype(np.float32), axis=1)))
    return len(x), float(np.max(np.abs(p - y))) if len(x) else 0.0, iguales


if __name__ == "__main__":
    import sys
    if len(sys.argv) == 4 and sys.argv[1] == "--paridad-inferencia":
        n, peor, iguales = compara_inferencia(sys.argv[2], sys.argv[3])
        print("FILAS %d" % n)
        print("PEOR %.9g" % peor)
        print("IGUALES %d" % iguales)
        sys.exit(0 if n > 0 else 1)
    print("uso: python3 calidad_dsee.py --paridad-inferencia modelo.bin volcado.txt")
    sys.exit(2)
