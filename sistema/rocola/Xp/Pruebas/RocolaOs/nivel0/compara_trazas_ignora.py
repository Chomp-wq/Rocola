#!/usr/bin/env python3
# compara_trazas_ignora.py: el DOBLE de prueba de «compara_trazas.py --ignora PREFIJO», para probar el nivel 1 de nivel0.sh (clasifica.py
# escena --ignora) sin depender de que la opcion de verdad ya este en Pruebas/Ventanas/guiones/compara_trazas.py (la pone el grupo andamio
# de la oleada 0 de la fase 11). prueba.sh lo copia como compara_trazas.py en una carpeta de guiones de prueba, junto al de verdad (como
# compara_trazas_real.py) y a compara_escena.py.
# Lo que hace: quita de las dos trazas los relojes y encargos cuyo nombre empieza por PREFIJO («tick RocolaOs:latido 1000», «encargo
# RocolaOs:diagnostico», «encargo-perdido RocolaOs:...») y llama al de verdad con lo demas. Importado (compara_escena.py importa
# «equivalentes»), da lo del de verdad.
import importlib.util, os, subprocess, sys, tempfile

REAL = os.path.join(os.path.dirname(os.path.abspath(__file__)), "compara_trazas_real.py")
_spec = importlib.util.spec_from_file_location("compara_trazas_real", REAL)
_real = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(_real)
equivalentes = _real.equivalentes


def sin_prefijo(ruta, prefijo, dir_tmp):
    out = os.path.join(dir_tmp, os.path.basename(os.path.dirname(ruta)) + "-" + os.path.basename(ruta))
    with open(ruta, encoding="utf-8") as f, open(out, "w", encoding="utf-8") as g:
        for l in f:
            q = l.rstrip("\n").split("\t", 1)
            p = q[1].split(" ") if len(q) == 2 else []
            if len(p) >= 2 and p[0] in ("tick", "encargo", "encargo-perdido") and p[1].startswith(prefijo):
                continue
            g.write(l)
    return out


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--ignora" not in args:
        sys.exit(subprocess.run([sys.executable, REAL] + args).returncode)
    i = args.index("--ignora"); prefijo = args[i + 1]; del args[i:i + 2]
    with tempfile.TemporaryDirectory() as d:
        trazas = [a for a in args if not a.startswith("--")][:2]
        nuevos = [sin_prefijo(t, prefijo, d) for t in trazas]
        args = [nuevos[trazas.index(a)] if a in trazas else a for a in args]
        sys.exit(subprocess.run([sys.executable, REAL] + args).returncode)
