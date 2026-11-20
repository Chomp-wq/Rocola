#!/usr/bin/env python3
# NOCHE Y VECINOS (20-11-2026): el analisis de lo que deja nochevecinos.cs (la salida de la cadena de verdad de la rocola de XP en cada
# propuesta). Solo numpy. Para cada cancion sintetica y cada propuesta:
#   DENTRO: bandas de tercio de octava (25 Hz a 16 kHz) de lo que dan las bocinas (cajas de 12" con puerto: paso-alto de 4.o orden a 45 Hz),
#           LAeq y LCeq (curvas A y C de la IEC 61672), el maximo en ventanas cortas (~0,19 s, parecido al «Fast») en A y en C, el grave
#           fundamental (40-100 Hz) y el de los armonicos (100-315 Hz), y la sonoridad BS.1770 (LUFS) que mide la propia rocola.
#   AFUERA: lo mismo tras un muro, con dos modelos: la ley de masa de campo (R = 20 log(m f) - 47, m = 250 kg/m2: tabique de 14 cm
#           aplanado) y la curva de referencia de la ISO 717-1 llevada a Rw 48 (por debajo de 100 Hz, 2 dB menos por tercio). Mas el nivel
#           de baja frecuencia de la guia danesa (LpA,LF: la suma en A de los tercios hasta 160 Hz) y cuanto pasa del umbral de audicion
#           (ISO 226:2003) cada banda.
#   EL VAIVEN: lo que una propuesta hace subir y bajar el grave y el volumen frente a la noche de hoy, ventana a ventana (p5 a p95):
#           un filtro fijo da un vaiven pequeno (solo por lo que trae cada instante); lo que actua en tiempo real lo da mas grande.
# El nivel absoluto se cuelga de una suposicion: de dia, dentro, junto al muro, LAeq = 85 dB (CAL_DIA). Las diferencias no dependen de eso.
#   python3 nochevecinos.py CARPETA            -> CARPETA/analisis.json (y borra los .f32 con --borra)
#   python3 nochevecinos.py --tablas CARPETA   -> las tablas del documento
import sys, os, json, glob
import numpy as np

FS = 44100
CAL_DIA = 85.0
CENTROS = [25, 31.5, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000,
           6300, 8000, 10000, 12500, 16000]
EXACTOS = [1000.0 * 10 ** (n / 10.0) for n in range(-16, 13)]
# Umbral de audicion (ISO 226:2003, campo libre, mediana), por tercio, de 25 Hz a 12,5 kHz (16 kHz sin dato: alto).
UMBRAL = [68.7, 59.5, 51.1, 44.0, 37.5, 31.5, 26.5, 22.1, 17.9, 14.4, 11.4, 8.6, 6.2, 4.4, 3.0, 2.2, 2.4, 3.5, 1.7, -1.3, -4.2, -6.0, -5.4,
          -1.5, 6.0, 12.6, 13.9, 12.3, 40.0]
ISO717 = {100: -19, 125: -16, 160: -13, 200: -10, 250: -7, 315: -4, 400: -1, 500: 0, 630: 1, 800: 2, 1000: 3, 1250: 4, 1600: 4, 2000: 4,
          2500: 4, 3150: 4}


def pesoA(f):
    f = np.maximum(f, 1e-3); f2 = f * f
    ra = (12194.0 ** 2 * f2 * f2) / ((f2 + 20.6 ** 2) * np.sqrt((f2 + 107.7 ** 2) * (f2 + 737.9 ** 2)) * (f2 + 12194.0 ** 2))
    return 20 * np.log10(ra) + 2.0


def pesoC(f):
    f = np.maximum(f, 1e-3); f2 = f * f
    rc = (12194.0 ** 2 * f2) / ((f2 + 20.6 ** 2) * (f2 + 12194.0 ** 2))
    return 20 * np.log10(rc) + 0.06


def bocinas(f):   # las cajas de 12" con puerto del billar: 4.o orden a 45 Hz (CadenaBillar.cs: sintonia de unos 45-50 Hz)
    f = np.maximum(f, 1e-3)
    return -10 * np.log10(1 + (45.0 / f) ** 8)


def muro_masa(f, m=250.0):
    return 20 * np.log10(m * np.maximum(f, 10.0)) - 47.0


def muro_iso(f, rw=48.0):
    xs = sorted(ISO717)
    logx = np.log10(np.array(xs, float)); ys = np.array([rw + ISO717[x] for x in xs], float)
    lf = np.log10(np.maximum(f, 10.0))
    r = np.interp(lf, logx, ys)
    # por debajo de 100 Hz: 2 dB menos por tercio (la pendiente de la ley de masa); por encima de 3150, igual
    abajo = lf < logx[0]
    r[abajo] = ys[0] - 2.0 * (logx[0] - lf[abajo]) / 0.1
    return r


MUROS = {"masa": muro_masa, "iso": muro_iso}


def lee(ruta):
    x = np.fromfile(ruta, dtype=np.float32).reshape(-1, 2).astype(np.float64)
    a, b = int(1.0 * FS), x.shape[0] - int(0.5 * FS)
    return x[a:b]


def welch(x, n=32768):
    w = np.hanning(n); paso = n // 2; tot = None; k = 0
    for i in range(0, x.shape[0] - n + 1, paso):
        s = np.abs(np.fft.rfft(x[i:i + n] * w[:, None], axis=0)) ** 2
        s = s.sum(axis=1)   # potencia de L mas la de R (en la sala se suman las bocinas)
        tot = s if tot is None else tot + s; k += 1
    return np.fft.rfftfreq(n, 1.0 / FS), tot / k


def bandas(f, p):
    out = []
    for fm in EXACTOS:
        lo, hi = fm * 10 ** (-0.05), fm * 10 ** 0.05
        sel = (f >= lo) & (f < hi)
        out.append(10 * np.log10(p[sel].sum() + 1e-30))
    return np.array(out)


def suma_db(v):
    v = np.asarray(v, float)
    return 10 * np.log10(np.sum(10 ** (v / 10.0)) + 1e-30)


def ventanas(x, n=8192, paso=2048):
    """Potencia por ventana y por frecuencia (L + R), para los maximos y el vaiven."""
    w = np.hanning(n); fr = np.fft.rfftfreq(n, 1.0 / FS); filas = []
    for i in range(0, x.shape[0] - n + 1, paso):
        s = np.abs(np.fft.rfft(x[i:i + n] * w[:, None], axis=0)) ** 2
        filas.append(s.sum(axis=1))
    return fr, np.array(filas)


def kpond(f):
    """La curva K de la BS.1770 en dB: los dos filtros de la norma (dados a 48 kHz), su respuesta en magnitud. Solo para los prototipos
    (lo que pasa por la rocola lo mide su propio MedidorBs1770)."""
    z = np.exp(-1j * 2 * np.pi * np.asarray(f, float) / 48000.0)
    def h(b, a): return (b[0] + b[1] * z + b[2] * z * z) / (a[0] + a[1] * z + a[2] * z * z)
    h1 = h([1.53512485958697, -2.69169618940638, 1.19839281085285], [1.0, -1.69065929318241, 0.73248077421585])
    h2 = h([1.0, -2.0, 1.0], [1.0, -1.99004745483398, 0.99007225036621])
    return 20 * np.log10(np.abs(h1 * h2) + 1e-30)


def bajo_virtual(x, fc=80.0, rel_db=-10.0):
    """PROTOTIPO (no esta en la rocola): quita el grave por debajo de fc (la misma curva de magnitud que el paso-alto de 4.o orden, en
    fase cero) y en su lugar pone sus armonicos 2.o y 3.o (polinomios de Chebyshev T2 y T3 del grave normalizado por su envolvente: con una
    nota sola dan solo esos armonicos, sin la fundamental), filtrados a 100-300 Hz, con una ganancia FIJA para toda la cancion: la energia de
    los armonicos queda rel_db dB frente a la del grave quitado (asi no sube ni baja con el bombo)."""
    n = x.shape[0]; N = 1
    while N < n: N <<= 1
    f = np.fft.rfftfreq(N, 1.0 / FS); f[0] = 1e-3
    X = np.fft.rfft(x, N, axis=0)
    hp = 1.0 / np.sqrt(1 + (fc / f) ** 8)
    alto = np.fft.irfft(X * hp[:, None], N, axis=0)[:n]
    bajo = x - alto
    m = bajo.mean(axis=1)
    w = np.hanning(int(0.02 * FS)); w /= w.sum()
    env = np.sqrt(2 * np.convolve(m * m, w, mode="same")) + 1e-9
    nz = np.clip(m / env, -1.5, 1.5)
    h = (0.6 * (2 * nz * nz - 1) + 0.4 * (4 * nz ** 3 - 3 * nz)) * env
    H = np.fft.rfft(h, N)
    bp = (1.0 / np.sqrt(1 + (100.0 / f) ** 8)) * (1.0 / np.sqrt(1 + (f / 300.0) ** 4))
    h = np.fft.irfft(H * bp, N)[:n]
    g = np.sqrt(np.mean(bajo ** 2) / (np.mean(h ** 2) + 1e-30)) * 10 ** (rel_db / 20.0)
    return alto + g * h[:, None]


class Medida:
    def __init__(self, cal, spk_f):
        self.cal = cal; self.fb = np.array(EXACTOS)
        frw = np.fft.rfftfreq(8192, 1.0 / FS); self.fw = frw
        self.spkw = 10 ** (bocinas(frw) / 10); self.wA = 10 ** (pesoA(frw) / 10); self.wC = 10 ** (pesoC(frw) / 10)
        self.bajo = (frw < 160).astype(float)

    def mide(self, x):
        fb = self.fb
        fx, px = welch(x); px = px * 10 ** (bocinas(fx) / 10)
        bz = bandas(fx, px) + self.cal
        r = {"bandas": bz.tolist()}
        r["LAeq"] = suma_db(bz + pesoA(fb)); r["LCeq"] = suma_db(bz + pesoC(fb))
        r["grave_40_100"] = suma_db(bz[2:6]); r["armonicos_100_315"] = suma_db(bz[6:12])
        r["LpALF"] = suma_db((bz + pesoA(fb))[:9])
        r["K"] = suma_db(bz + kpond(fb))   # la sonoridad K de lo que dan las bocinas (para comparar los prototipos, que no mide la rocola)
        fw, pw = ventanas(x)
        pw = pw * self.spkw * 10 ** (self.cal / 10)
        r["LAFmax"] = float(10 * np.log10(np.max(pw @ self.wA)))
        r["LCFmax"] = float(10 * np.log10(np.max(pw @ self.wC)))
        for nombre, muro in MUROS.items():
            R = muro(fb)
            bo = bz - R
            o = {"bandas": bo.tolist(), "LAeq": suma_db(bo + pesoA(fb)), "LCeq": suma_db(bo + pesoC(fb)), "LpALF": suma_db((bo + pesoA(fb))[:9])}
            sobre = bo - np.array(UMBRAL)
            o["sobre_umbral"] = sobre.tolist()
            o["banda_mas_audible"] = CENTROS[int(np.argmax(sobre))]
            Rw = 10 ** (-muro(fw) / 10)
            o["LAFmax"] = float(10 * np.log10(np.max(pw @ (self.wA * Rw))))
            o["LCFmax"] = float(10 * np.log10(np.max(pw @ (self.wC * Rw))))
            r["afuera_" + nombre] = o
        return r, (pw @ self.wA, pw @ self.bajo)


def analiza_cancion(carpeta, cancion, lufs):
    rutas = sorted(glob.glob(os.path.join(carpeta, cancion + "__*.f32")))
    props = [os.path.basename(r)[len(cancion) + 2:-4] for r in rutas]
    res = {}
    # la calibracion: el dia de esta cancion a CAL_DIA dB(A) dentro
    fd, pd = welch(lee(os.path.join(carpeta, cancion + "__dia.f32")))
    pd = pd * 10 ** (bocinas(fd) / 10)
    cal = CAL_DIA - 10 * np.log10(np.sum(pd * 10 ** (pesoA(fd) / 10)))
    med = Medida(cal, None)
    ventanas_ref = {}
    for prop, ruta in zip(props, rutas):
        x = lee(ruta)
        r, v = med.mide(x)
        r["lufs"] = lufs.get((cancion, prop), {}).get("lufs")
        r["bajo_amount"] = lufs.get((cancion, prop), {}).get("bajo")
        res[prop] = r; ventanas_ref[prop] = v
        if prop == "noche80":
            # LOS PROTOTIPOS DE UN BAJO PSICOACUSTICO DE NOCHE (no estan en la rocola): sobre la salida de la noche de hoy
            for nombre, fc, rel in [("bv80_m10", 80.0, -10.0), ("bv80_m6", 80.0, -6.0), ("bv100_m6", 100.0, -6.0)]:
                y = bajo_virtual(x, fc, rel)
                ry, vy = med.mide(y)
                ry["lufs"] = None; ry["bajo_amount"] = None; ry["prototipo"] = True
                res[nombre] = ry; ventanas_ref[nombre] = vy
        del x
    # la K de los prototipos se cuenta frente a la K de la noche de hoy (los LUFS de la rocola no los vieron)
    for nombre in ["bv80_m10", "bv80_m6", "bv100_m6"]:
        if nombre in res:
            res[nombre]["lufs"] = res["noche80"]["lufs"] + (res[nombre]["K"] - res["noche80"]["K"])
    # LO QUE DE VERDAD PONE EL BAJO PSICOACUSTICO DE HOY: la resta de la salida con y sin el (todo lo demas es lineal a esta altura)
    for base in ["noche80", "pa80"]:
        r0 = os.path.join(carpeta, cancion + "__" + base + "_bajo0.f32"); r1 = os.path.join(carpeta, cancion + "__" + base + ".f32")
        r3 = os.path.join(carpeta, cancion + "__" + base + "_bajo3.f32")
        if os.path.exists(r0) and os.path.exists(r1):
            x0 = lee(r0); x1 = lee(r1)
            f, p0 = welch(x0); _, pd1 = welch(x1 - x0)
            b0 = bandas(f, p0); bd1 = bandas(f, pd1)
            o = {"x1_frente_a_musica": (bd1 - b0).tolist(), "x1_bajo_el_total": (bd1 - suma_db(b0)).tolist()}
            if os.path.exists(r3):
                x3 = lee(r3); _, pd3 = welch(x3 - x0); bd3 = bandas(f, pd3)
                o["x3_frente_a_musica"] = (bd3 - b0).tolist(); o["x3_bajo_el_total"] = (bd3 - suma_db(b0)).tolist()
            res["_bajo_" + base] = o
    # EL VAIVEN frente a la noche de hoy (noche80): ventana a ventana, lo que cambia el volumen en A y el grave (< 160 Hz)
    if "noche80" in ventanas_ref:
        ra, rb = ventanas_ref["noche80"]
        vivas = ra > np.max(ra) * 10 ** (-3.0)   # las ventanas a menos de 30 dB de lo mas fuerte
        vivasb = rb > np.max(rb) * 10 ** (-3.0)
        for prop, (a, b) in ventanas_ref.items():
            da = 10 * np.log10(a[vivas] / ra[vivas]); db = 10 * np.log10(b[vivasb] / rb[vivasb])
            res[prop]["vaiven_A"] = [float(np.percentile(da, 5)), float(np.percentile(da, 50)), float(np.percentile(da, 95))]
            res[prop]["vaiven_grave"] = [float(np.percentile(db, 5)), float(np.percentile(db, 50)), float(np.percentile(db, 95))]
    # que tan «real» es la cancion: dBC - dBA de dia, dentro
    res["_cal"] = {"cal_db": cal, "C_menos_A_dia": res["dia"]["LCeq"] - res["dia"]["LAeq"]}
    return res


def lee_tsv(carpeta):
    t = {}
    with open(os.path.join(carpeta, "medidas.tsv")) as fh:
        cab = fh.readline().strip().split("\t")
        for linea in fh:
            c = linea.rstrip("\n").split("\t")
            d = dict(zip(cab, c))
            t[(d["cancion"], d["propuesta"])] = {"lufs": float(d["lufs"]), "bajo": float(d["bajo_amount"]), "pico": float(d["pico_dbfs"])}
    return t


def main():
    if len(sys.argv) >= 3 and sys.argv[1] == "--tablas":
        return tablas(sys.argv[2])
    carpeta = sys.argv[1]; borra = "--borra" in sys.argv
    lufs = lee_tsv(carpeta)
    ruta = os.path.join(carpeta, "analisis.json")
    todo = json.load(open(ruta)) if os.path.exists(ruta) else {}
    canciones = sorted({os.path.basename(p).split("__")[0] for p in glob.glob(os.path.join(carpeta, "*.f32"))})
    for c in canciones:
        todo[c] = analiza_cancion(carpeta, c, lufs)
        print("  analizada", c, "C-A de dia = %.1f dB" % todo[c]["_cal"]["C_menos_A_dia"])
        if borra:
            for p in glob.glob(os.path.join(carpeta, c + "__*.f32")):
                os.remove(p)
    json.dump(todo, open(ruta, "w"), indent=1)


def tablas(carpeta):
    todo = json.load(open(os.path.join(carpeta, "analisis.json")))
    musica = [c for c in ["cumbia", "banda", "reggaeton", "rock", "balada"] if c in todo]
    props = [p for p in todo[musica[0]].keys() if not p.startswith("_")]
    ref = "noche80"
    def val(c, p, clave, muro=None):
        a = todo[c][p] if muro is None else todo[c][p]["afuera_" + muro]
        return a[clave]
    def dif(c, p, clave, muro=None): return val(c, p, clave, muro) - val(c, ref, clave, muro)
    def media(vals):
        v = np.array(vals); t = "%+.1f" % v.mean()
        return t + (" (%+.1f a %+.1f)" % (v.min(), v.max()) if v.max() - v.min() >= 0.95 else "")
    print("## 1. Las canciones sinteticas (dentro, de dia, calibradas a 85 dB(A))")
    print("cancion | LUFS | dB(A) | dB(C) | C-A | grave 40-100 Hz | armonicos 100-315 Hz")
    for c in musica + (["rosa"] if "rosa" in todo else []):
        d = todo[c]["dia"]
        print("%s | %.1f | %.1f | %.1f | %.1f | %.1f | %.1f" % (c, d["lufs"], d["LAeq"], d["LCeq"], d["LCeq"] - d["LAeq"], d["grave_40_100"], d["armonicos_100_315"]))
    for muro in ["masa", "iso"]:
        print("\n## 2%s. Frente a la noche de hoy (80 %%, N1 4 dB), media de las 5 canciones (de la que menos a la que mas) -- muro %s" % ("a" if muro == "masa" else "b", muro))
        print("propuesta | LUFS dentro | grave 40-100 dentro | armonicos 100-315 dentro | graves afuera LpA,LF | dB(C) afuera | golpe afuera LCFmax | dB(A) afuera | LAFmax afuera | rinde (graves afuera / LUFS)")
        for p in props:
            dl = [dif(c, p, "lufs") for c in musica]; dg = [dif(c, p, "LpALF", muro) for c in musica]
            rinde = (np.mean(dg) / np.mean(dl)) if abs(np.mean(dl)) > 0.05 else float("nan")
            print(" | ".join([p, media(dl), media([dif(c, p, "grave_40_100") for c in musica]), media([dif(c, p, "armonicos_100_315") for c in musica]),
                              media(dg), media([dif(c, p, "LCeq", muro) for c in musica]), media([dif(c, p, "LCFmax", muro) for c in musica]),
                              media([dif(c, p, "LAeq", muro) for c in musica]), media([dif(c, p, "LAFmax", muro) for c in musica]),
                              ("%.1f" % rinde) if rinde == rinde else "-"]))
    print("\n## 3. Por banda de tercio, afuera (igual con los dos muros: el muro resta lo mismo a todas), frente a la noche de hoy, media de las 5")
    idx = [CENTROS.index(b) for b in [40, 50, 63, 80, 100, 125, 160, 200, 250, 500, 1000, 2000, 4000]]
    print("propuesta | " + " | ".join(str(CENTROS[i]) for i in idx))
    for p in props:
        print(p + " | " + " | ".join("%+.1f" % np.mean([todo[c][p]["bandas"][i] - todo[c][ref]["bandas"][i] for c in musica]) for i in idx))
    print("\n## 4. Absoluto en la casa del vecino (si dentro, de dia, hay 85 dB(A) junto al muro), media de las 5")
    print("propuesta | masa: dB(A) | masa: dB(C) | masa: LpA,LF | iso: dB(A) | iso: dB(C) | iso: LpA,LF | banda mas sobre el umbral (masa / iso)")
    for p in props:
        m = [np.mean([val(c, p, k, w) for c in musica]) for w in ["masa", "iso"] for k in ["LAeq", "LCeq", "LpALF"]]
        bm = "/".join(str(max(set(l), key=l.count)) for l in [[val(c, p, "banda_mas_audible", w) for c in musica] for w in ["masa", "iso"]])
        print(p + " | " + " | ".join("%.1f" % v for v in m) + " | " + bm)
    print("\n## 5. Lo que pone el bajo psicoacustico de hoy (Amount 0,3 y el triple), dB frente a la musica en esa banda (media de las 5)")
    idx2 = [CENTROS.index(b) for b in [25, 31.5, 40, 50, 63, 80, 100, 125, 160, 200, 250, 315]]
    print("caso | " + " | ".join(str(CENTROS[i]) for i in idx2))
    for base in ["noche80", "pa80"]:
        for k in ["x1", "x3"]:
            vs = [todo[c]["_bajo_" + base][k + "_frente_a_musica"] for c in musica if "_bajo_" + base in todo[c]]
            if vs: print("%s %s | " % (base, k) + " | ".join("%+.1f" % np.mean([v[i] for v in vs]) for i in idx2))
    for base in ["noche80", "pa80"]:
        vs = [todo[c]["_bajo_" + base]["x3_bajo_el_total"] for c in musica if "_bajo_" + base in todo[c]]
        if vs: print("%s x3, bajo el total de la musica | " % base + " | ".join("%+.1f" % np.mean([v[i] for v in vs]) for i in idx2))
    print("\n## 6. El vaiven frente a la noche de hoy (p5 a p95 de la diferencia por ventana de 0,19 s), el peor de las 5 canciones")
    print("propuesta | volumen en A | grave (< 160 Hz)")
    for p in props:
        va = [todo[c][p]["vaiven_A"] for c in musica]; vg = [todo[c][p]["vaiven_grave"] for c in musica]
        print("%s | %.1f | %.1f" % (p, max(v[2] - v[0] for v in va), max(v[2] - v[0] for v in vg)))
    if "rosa" in todo:
        print("\n## 7. Ruido rosa: lo que cada propuesta le hace por banda (dentro), frente a la noche de hoy")
        print("propuesta | " + " | ".join(str(CENTROS[i]) for i in idx))
        for p in props:
            if p in todo["rosa"]:
                print(p + " | " + " | ".join("%+.1f" % (todo["rosa"][p]["bandas"][i] - todo["rosa"][ref]["bandas"][i]) for i in idx))
    print("\n## 8. LUFS por cancion (dia, noche de hoy, 50 %)")
    for c in musica:
        print("  %s: dia %.1f, noche80 %.1f, noche50 %.1f; pico de la salida de dia %s" % (c, todo[c]["dia"]["lufs"], todo[c]["noche80"]["lufs"], todo[c]["noche50"]["lufs"], "-"))


if __name__ == "__main__":
    main()
