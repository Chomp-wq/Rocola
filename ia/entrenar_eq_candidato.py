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

VERSION_HUELLA = 2  # potencia por canales medida ANTES del DSP


def leer_agrupado(texto, diagnostico_legacy=False):
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
            v = np.asarray([float(x) for x in ficha.split(";")], dtype=np.float64)
            if len(v) < 9 or not np.isfinite(v).all():
                raise ValueError("huella incompleta/no finita")
        except ValueError:
            malas += 1
            continue
        fichas[ruta] = v
    filas, claves = [], []
    versiones = {}
    excluidas = 0
    for ruta, v in sorted(fichas.items()):
        version = int(v[11]) if len(v) >= 12 and v[11] == int(v[11]) else 0
        versiones[str(version)] = versiones.get(str(version), 0) + 1
        # Diagnostico viejo separado: nunca mezclar 0/1 con 2 ni interpretar versiones futuras.
        if version not in ((0,1) if diagnostico_legacy else (VERSION_HUELLA,)):
            excluidas += 1
            continue
        if v[8] < E.SANA_HZ:
            continue
        filas.append(v[:8])
        # Carpetas de album y copias con EXACTAMENTE la misma huella se unen.
        # No se deduce un artista del nombre de la carpeta ni se promete separarlo.
        claves.append((O.huella("album:" + ntpath.dirname(ruta)), O.huella("huella:" + v[:8].tobytes().hex())))
    if not filas:
        raise ValueError("No hay fichas sanas de la version seleccionada con ruta verificable; reanalizar medidas antiguas")
    grupos = O._componentes(claves)
    return np.asarray(filas), grupos, {"rutas_unicas": len(fichas), "sanas": len(filas), "invalidas": malas,
                                      "versiones": versiones, "excluidas_version": excluidas}


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


def entrenar(datos, instalado, destino, epocas=200, diagnostico_legacy=False):
    if not 1 <= epocas <= 200:
        raise ValueError("Presupuesto de epocas: 1..200")
    with open(datos, "rb") as f:
        crudo = f.read(64 * 1048576 + 1)
    if len(crudo) > 64 * 1048576:
        raise ValueError("Datos mayores que el presupuesto de 64 MiB; no se truncan")
    d, grupos, lectura = leer_agrupado(crudo.decode("utf-8-sig", errors="replace"), diagnostico_legacy)
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
    # El objetivo runtime lleva el cinturon de +/-6 dB. No usar promedio del test.
    pred_runtime = np.clip(pred, media - 6, media + 6)
    inst_runtime = np.clip(instalado_pred, media - 6, media + 6)
    errores = {k: np.sqrt(np.mean((p - y[te]) ** 2, axis=0)).tolist() for k, p in
               (("candidato", pred), ("instalado", instalado_pred), ("promedio_entrenamiento", media))}
    paridad = float(np.max(np.abs(pred - m.predict(x[te]))))
    errores_runtime = {k: np.sqrt(np.mean((p - y[te]) ** 2, axis=0)).tolist() for k, p in
                       (("candidato", pred_runtime), ("instalado", inst_runtime), ("promedio_entrenamiento", media))}
    # Evitar ocultar regresiones por nivel de grave/brillo dentro del promedio global.
    umbrales = np.quantile(y[tr], [1/3, 2/3], axis=0)
    clases, bloqueos = {}, []
    for banda, nombre in enumerate(("grave", "brillo", "aire")):
        etiquetas = np.searchsorted(umbrales[:, banda], y[te, banda])
        for clase in range(3):
            sel = etiquetas == clase
            if not sel.any(): continue
            red = float(np.mean((pred_runtime[sel] - y[te][sel]) ** 2))
            ref_error = float(np.mean((inst_runtime[sel] - y[te][sel]) ** 2))
            clave = "%s_%d" % (nombre, clase)
            clases[clave] = {"filas": int(sel.sum()), "candidato_mse": red, "instalado_mse": ref_error}
            if sel.sum() >= 50 and red > ref_error * 1.1 + 1e-12:
                bloqueos.append(clave)
    aceptado = (decide(errores) and decide(errores_runtime) and not bloqueos and
                np.isfinite(paridad) and paridad <= 1e-4 and not diagnostico_legacy)
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
               "runtime_objetivo_rmse": errores_runtime, "runtime_por_clase": clases,
               "bloqueos_por_clase": bloqueos, "legacy_no_publicable": bool(diagnostico_legacy),
               "version_huella_requerida": VERSION_HUELLA, "pcm_completo_verificado": False,
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
    ap.add_argument("--diagnostico-legacy", action="store_true", help="Solo diagnostico aislado; nunca exporta candidato")
    args = ap.parse_args()
    result = entrenar(args.datos, args.instalado, args.destino, args.epocas, args.diagnostico_legacy)
    print(json.dumps(result, sort_keys=True))
    sys.exit(0 if result["aceptado_numericamente"] else 2)
