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


def seleccionar(filas, cuantas, semilla=2026):
    # Dano extremo, medio, leve y controles sanos. Distribuir sobrantes sin sesgo de orden.
    rng = random.Random(semilla)
    cubetas = [[r for hz, r in filas if condicion(hz)] for condicion in
               (lambda hz: 0 < hz <= 9000, lambda hz: 9000 < hz < 12500,
                lambda hz: 12500 <= hz < 15500, lambda hz: 15500 <= hz < 18500,
                lambda hz: hz >= 18500)]
    for c in cubetas: rng.shuffle(c)
    elegidas = []
    while len(elegidas) < cuantas and any(cubetas):
        for c in cubetas:
            if c and len(elegidas) < cuantas: elegidas.append(c.pop())
    return elegidas

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "calidad_biblioteca.csv"))
    ap.add_argument("--salida", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "muestras_danadas"))
    ap.add_argument("--cuantas", type=int, default=15)
    a = ap.parse_args()
    if not os.path.exists(a.csv):
        print("[X] No encuentro", a.csv, "- corre primero 3_escanear.bat"); return 1
    filas = cargar_csv(a.csv)
    elegidas = seleccionar(filas, max(1, a.cuantas))
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
