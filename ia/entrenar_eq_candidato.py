"""AutoEQ aislado: albumes separados, prueba final y comparacion con el instalado.

No instala modelos. Un resultado numerico favorable todavia requiere escucha A/B.
Limita BLAS a un hilo antes de importar numpy (solo este proceso de entrenamiento).
"""
import os
os.environ["OPENBLAS_NUM_THREADS"] = "1"
os.environ["OMP_NUM_THREADS"] = "1"
import argparse
import hashlib
import io
import json
import ntpath
import sys
import numpy as np
import entrenar_eq as E
import origen_dsee as O
import calidad_dsee as C


def leer_agrupado(texto):
    # Conservar la ultima ficha de una misma ruta. No mezclar revisiones entre partes.
    fichas = {}
    malas = 0
    for linea in io.StringIO(texto):
        ficha, sep, ruta = linea.strip().partition("|")
        ruta = ntpath.normcase(ntpath.normpath(ruta.strip()))
        if not sep or ruta in ("", ".") or not ntpath.dirname(ruta):
            malas += 1
            continue
        try:
            v = np.asarray([float(x) for x in ficha.split(";")[:9]], dtype=np.float64)
            if len(v) != 9 or not np.isfinite(v).all():
                raise ValueError("huella incompleta/no finita")
        except ValueError:
            malas += 1
            continue
        fichas[ruta] = v
    filas, claves = [], []
    for ruta, v in sorted(fichas.items()):
        if v[8] < E.SANA_HZ:
            continue
        filas.append(v[:8])
        # Carpetas de album y copias con EXACTAMENTE la misma huella se unen.
        # No se deduce un artista del nombre de la carpeta ni se promete separarlo.
        claves.append((O.huella("album:" + ntpath.dirname(ruta)), O.huella("huella:" + v[:8].tobytes().hex())))
    if not filas:
        raise ValueError("No hay fichas sanas con ruta verificable")
    grupos = O._componentes(claves)
    return np.asarray(filas), grupos, {"rutas_unicas": len(fichas), "sanas": len(filas), "invalidas": malas}


def particiones(grupos):
    partes, motivo = O.reparte(grupos, np.ones(len(grupos), dtype=np.int64),
                              semilla=20260923, min_grupos=(3, 3, 3),
                              min_ejemplos=(500, 200, 200))
    if motivo:
        raise ValueError("No hay prueba independiente por album: " + motivo)
    return [np.flatnonzero(partes == p) for p in range(3)]


def decide(errores):
    # RMSE por banda, no MSE: umbrales fijados antes de ver la prueba final.
    red = np.asarray(errores["candidato"])
    if red.shape != (3,) or not np.isfinite(red).all():
        return False
    for clave, margen in (("promedio_entrenamiento", .05), ("instalado", .02)):
        ref = np.asarray(errores[clave])
        if ref.shape != (3,) or not np.isfinite(ref).all() or ref.mean() <= 1e-12:
            return False
        if red.mean() > ref.mean() * (1 - margen) or np.any(red > ref * 1.10 + 1e-12):
            return False
    return True


def entrenar(datos, instalado, destino, epocas=200):
    if epocas <= 0:
        raise ValueError("Epocas debe ser positivo")
    with open(datos, "rb") as f:
        crudo = f.read(64 * 1048576 + 1)
    if len(crudo) > 64 * 1048576:
        raise ValueError("Datos mayores que el presupuesto de 64 MiB; no se truncan")
    d, grupos, lectura = leer_agrupado(crudo.decode("utf-8-sig", errors="replace"))
    tr, va, te = particiones(grupos)
    ref = C.cargar_ria1(instalado, entradas=5, salidas=3)
    dims = [5] + [w.shape[0] for w in ref.W]
    if ref.parametros() > 20000:
        raise ValueError("La referencia supera el presupuesto de entrenamiento")
    # Destino NUEVO, nunca la carpeta instalada. Los datos personales se quedan aqui.
    os.mkdir(destino)
    with open(os.path.join(destino, "datos.txt"), "xb") as f:
        f.write(crudo)
    ref_bytes = C.bytes_ria1(ref)
    with open(os.path.join(destino, "instalado.bin"), "xb") as f:
        f.write(ref_bytes)
    x, y = d[:, E.ENTRADAS], d[:, E.SALIDAS]
    xn, mu, sd = E.normalizar_entrenamiento(x, tr)
    print("AutoEQ: %d/%d/%d canciones; %d/%d/%d grupos (entrenamiento/validacion/prueba)" %
          (len(tr), len(va), len(te), len(set(grupos[tr])), len(set(grupos[va])), len(set(grupos[te]))), flush=True)
    m = E.Mlp(dims, np.random.default_rng(20260923))
    E.entrenar(m, xn[tr], y[tr], xn[va], y[va], epocas, semilla=20260923)
    m.W[0] /= sd
    m.B[0] -= m.W[0] @ mu
    m = C.RedRia1([w.astype(np.float32).astype(np.float64) for w in m.W],
                 [b.astype(np.float32).astype(np.float64) for b in m.B])
    if any(not np.isfinite(v).all() for v in m.W + m.B):
        raise ValueError("Modelo no finito al exportar float32")
    # Prueba FINAL: nunca usada para normalizar ni elegir pesos/epoca.
    pred, malas = C.prediccion_como_la_rocola(m, x[te])
    instalado_pred, malas_ref = C.prediccion_como_la_rocola(ref, x[te])
    if malas.any() or malas_ref.any():
        raise ValueError("Inferencia no finita en la prueba final")
    media = y[tr].mean(axis=0)
    errores = {k: np.sqrt(np.mean((p - y[te]) ** 2, axis=0)).tolist() for k, p in
               (("candidato", pred), ("instalado", instalado_pred), ("promedio_entrenamiento", media))}
    paridad = float(np.max(np.abs(pred - m.predict(x[te]))))
    aceptado = decide(errores) and np.isfinite(paridad) and paridad <= 1e-4
    modelo = C.bytes_ria1(m)
    informe = {"formato": "ROCOLA-CANDIDATO-EQ 1", "semilla": 20260923, "epocas": epocas,
               "numpy": np.__version__, "python": sys.version.split()[0], "lectura": lectura,
               "datos_sha256": hashlib.sha256(crudo).hexdigest(), "dimensiones": dims,
               "parametros": m.parametros(), "instalado_parametros": ref.parametros(),
               "instalado_sha256": hashlib.sha256(ref_bytes).hexdigest(),
               "candidato_sha256": hashlib.sha256(modelo).hexdigest(),
               "filas": [len(tr), len(va), len(te)], "grupos": [len(set(grupos[p])) for p in (tr, va, te)],
               "particion_sha256": hashlib.sha256(b"".join(p.astype("<i8").tobytes() for p in (tr, va, te))).hexdigest(),
               "rmse_db_por_banda": errores, "paridad_float32_max": paridad,
               "politica": "RMSE: mejora >=5% vs promedio train y >=2% vs instalado; ninguna banda >10% peor; paridad <=1e-4",
               "aceptado_numericamente": bool(aceptado), "activado": False, "escucha_pendiente": True,
               "limites": ["Separacion por album/copia exacta, no por artista ni genero",
                           "El origen del entrenamiento del modelo instalado no se conoce",
                           "Corte >=19300 Hz es un indicador, no garantiza un master sin perdidas",
                           "Prediccion de curvas no demuestra superioridad audible"]}
    if aceptado:
        with open(os.path.join(destino, "candidato_eq.bin"), "xb") as f:
            f.write(modelo)
        vectores = np.concatenate((x[te[:64]].astype(np.float32), pred[:64]), axis=1)
        with open(os.path.join(destino, "vectores_eq.txt"), "x") as f:
            np.savetxt(f, vectores, fmt="%.9g")
    with open(os.path.join(destino, "informe_eq.json"), "x", encoding="utf-8") as f:
        json.dump(informe, f, sort_keys=True, indent=2, allow_nan=False)
    return informe


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--datos", required=True)
    ap.add_argument("--instalado", required=True)
    ap.add_argument("--destino", required=True)
    ap.add_argument("--epocas", type=int, default=200)
    args = ap.parse_args()
    result = entrenar(args.datos, args.instalado, args.destino, args.epocas)
    print(json.dumps(result, sort_keys=True))
    sys.exit(0 if result["aceptado_numericamente"] else 2)
