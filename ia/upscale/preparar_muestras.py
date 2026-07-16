# -*- coding: utf-8 -*-
# Copia un abanico REPRESENTATIVO de canciones danadas (segun el CSV del escaner) a una carpeta,
# para hacer el A/B donde SI se oye la diferencia. Reparte entre "brutal", "muy danada" y "danada".
import sys, os, csv, shutil, argparse, random

def cargar_csv(ruta):
    filas = []
    with open(ruta, encoding="utf-8") as f:
        r = csv.reader(f, delimiter=";")
        next(r, None)
        for row in r:
            if len(row) >= 3:
                try: hz = float(row[0])
                except ValueError: continue
                filas.append((hz, row[2]))
    return filas

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "calidad_biblioteca.csv"))
    ap.add_argument("--salida", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "muestras_danadas"))
    ap.add_argument("--cuantas", type=int, default=15)
    a = ap.parse_args()
    if not os.path.exists(a.csv):
        print("[X] No encuentro", a.csv, "- corre primero 3_escanear.bat"); return 1
    filas = cargar_csv(a.csv)
    rng = random.Random(2026)
    # tres cubetas por nivel de dano; se toma un tercio de cada una (variedad real)
    brutal = [r for hz, r in filas if 0 < hz <= 9000]
    muy    = [r for hz, r in filas if 9000 < hz < 12500]
    dan    = [r for hz, r in filas if 12500 <= hz < 15500]
    for c in (brutal, muy, dan): rng.shuffle(c)
    k = max(1, a.cuantas // 3)
    elegidas = brutal[:k] + muy[:k] + dan[:k]
    if not elegidas:
        print("[X] El CSV no tiene canciones danadas (<15.5 kHz). Nada que probar aqui."); return 1
    os.makedirs(a.salida, exist_ok=True)
    n = 0
    for src in elegidas:
        try:
            if not os.path.exists(src): continue
            dst = os.path.join(a.salida, "%02d_%s" % (n + 1, os.path.basename(src)))
            shutil.copyfile(src, dst); n += 1
        except Exception as e:
            print("   (no se pudo copiar %s: %s)" % (src, e))
    print("Copiadas %d canciones danadas a: %s" % (n, a.salida))
    return 0 if n > 0 else 1

if __name__ == "__main__":
    sys.exit(main())
