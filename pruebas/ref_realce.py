"""Referencia INDEPENDIENTE del aliasing del realce de agudos y del grave sintetico, solo con numpy.

No comparte nada con Rocola/Realce.cs: en vez de un filtro de media banda a 2x, aqui la saturacion se calcula a 16x
con interpolacion de banda limitada EXACTA (FFT con relleno de ceros) y lo que pasa de Nyquist se TIRA (no se
pliega). Lo que queda son los armonicos e intermodulacion de verdad; lo que la rocola saque de mas es aliasing.

Uso:
  python3 ref_realce.py realce entrada.wav salida.wav EXCITE RETRASO
      La salida de la rocola, adelantada RETRASO cuadros, contra  v + k*tanh(2,5h) ideal  (k = 0,2*EXCITE; h con el
      mismo paso-alto de un polo en float32). Una linea por canal:
      CANAL c PICO <dB> TERCIO <dB> DBFS <dB> DESEADO <dB> BANDA <dBFS>
        PICO:    el mayor componente del error por debajo de 16 kHz, respecto al mayor de la senal
        TERCIO:  el peor tercio de octava por debajo de 16 kHz (error/senal en esa banda), solo en bandas donde la
                 senal tiene algo (a menos de 60 dB de su banda mas fuerte)
        DBFS:    el mayor componente del error por debajo de 16 kHz, en dB de fondo de escala
        DESEADO: los armonicos e intermodulacion reales (lo no lineal de la ideal), en dB respecto a la senal
        BANDA:   el tercio de octava (por debajo de 16 kHz) con MAS error, en nivel absoluto: su potencia respecto a
                 la de un seno a fondo de escala (dBFS)
  python3 ref_realce.py grave entrada.wav salida.wav CANTIDAD
      Lo que el grave sintetico AGREGO (salida - entrada, el mismo en todos los canales) contra la ideal sin
      plegado: su parte no lineal a 16x con low y env interpolados; los filtros de salida, los mismos.
      GRAVE ALIAS <dB respecto a lo deseado> PICO <dBFS> ESPEJO <dB: rocola contra la cuenta a 1x de aqui>
"""
import sys

import numpy as np

from ref_bs1770 import lee_wav

F32 = np.float32
FINO = 16


def tanh_r(x):
    x = np.clip(x, -3.0, 3.0)
    x2 = x * x
    return x * (27.0 + x2) / (27.0 + 9.0 * x2)


def polo(fs):
    return 0.55 if fs == 44100 else 1.0 - 0.45 ** (44100.0 / fs)


def banda_alta(v, fs):
    a = F32(polo(fs))
    lp = F32(0)
    h = np.empty(len(v), dtype=F32)
    for i, x in enumerate(np.asarray(v, dtype=F32)):
        lp = F32(lp + F32(F32(x - lp) * a))
        h[i] = F32(x - lp)
    return h.astype(np.float64)


def sube(x, L):
    n = len(x)
    X = np.fft.rfft(x)
    U = np.zeros(L * n // 2 + 1, dtype=complex)
    U[: len(X)] = X
    return np.fft.irfft(U, L * n) * L


def baja(y, L, n):
    Y = np.fft.rfft(y)
    return np.fft.irfft(Y[: n // 2 + 1], n) / L


def sin_plegado(f, *senales):
    n = len(senales[0])
    return baja(f(*[sube(s, FINO) for s in senales]), FINO, n)


def db(x):
    return 10.0 * np.log10(max(float(x), 1e-30))


def metricas(err, v, fs):
    n = len(v)
    w = np.blackman(n)
    fr = np.fft.rfftfreq(n, 1.0 / fs)
    pe = np.abs(np.fft.rfft(err * w)) ** 2
    pv = np.abs(np.fft.rfft(v * w)) ** 2
    aud = fr < 16000
    ref = (np.sum(w) / 2.0) ** 2
    bandas, lim = [], 100.0
    while lim < 16000:
        m = (fr >= lim) & (fr < lim * 2 ** (1 / 3))
        if m.any():
            bandas.append((pv[m].sum(), pe[m].sum()))
        lim *= 2 ** (1 / 3)
    fuerte = max(b[0] for b in bandas)
    tercio = max(db(e / s) for s, e in bandas if s > fuerte * 1e-6) if fuerte > 0 else -300.0
    # nivel absoluto de la peor banda: su potencia respecto a la de un seno a fondo de escala medido igual
    t = np.arange(n)
    ps = (np.abs(np.fft.rfft(np.sin(2 * np.pi * 0.1 * t) * w)) ** 2).sum()
    tercio_dbfs = max(db(e / ps) for s, e in bandas)
    return db(pe[aud].max() / max(pv.max(), 1e-30)), tercio, db(pe[aud].max() / ref), tercio_dbfs


def realce(argv):
    fs, x = lee_wav(argv[2])
    fs2, y = lee_wav(argv[3])
    excite, D = float(argv[4]), int(argv[5])
    assert fs == fs2 and x.shape[1] == y.shape[1]
    k = 0.2 * excite
    n = x.shape[0]
    for c in range(x.shape[1]):
        v = x[:, c]
        h = banda_alta(v, fs)
        ideal = v + k * sin_plegado(lambda u: tanh_r(2.5 * u), h)
        r = y[D: D + n, c]
        if len(r) < n:
            r = np.concatenate([r, np.zeros(n - len(r))])
        pico, tercio, dbfs, tercio_dbfs = metricas(r - ideal, v, fs)
        deseado = k * (sin_plegado(lambda u: tanh_r(2.5 * u), h) - 2.5 * h)
        w = np.blackman(n)
        des = db(np.sum((deseado * w) ** 2) / max(np.sum((v * w) ** 2), 1e-30))
        print("CANAL %d PICO %.2f TERCIO %.2f DBFS %.2f DESEADO %.2f BANDA %.2f" % (c, pico, tercio, dbfs, des, tercio_dbfs))


def coef(hz, fs):
    a = 1.0 - np.exp(-2.0 * np.pi * hz / fs)
    return F32(min(max(a, 0.0), 1.0))


def grave(argv):
    fs, x = lee_wav(argv[2])
    _, y = lee_wav(argv[3])
    cantidad = float(argv[4])
    ch = x.shape[1]
    m = x.sum(axis=1) / ch if ch > 1 else x[:, 0]
    aLow, aHp, aLp = coef(100.0, fs), coef(95.0, fs), coef(320.0, fs)
    aAtk, aRel = coef(1.0 / (2.0 * np.pi * 0.004), fs), coef(1.0 / (2.0 * np.pi * 0.150), fs)
    mix = F32(F32(cantidad) * F32(0.85))
    lp1 = lp2 = lp3 = env = F32(0)
    low = np.empty(len(m)); en = np.empty(len(m))
    for i, s in enumerate(np.asarray(m, dtype=F32)):
        lp1 = F32(lp1 + F32(F32(s - lp1) * aLow)); lp2 = F32(lp2 + F32(F32(lp1 - lp2) * aLow))
        lp3 = F32(lp3 + F32(F32(lp2 - lp3) * aLow))
        a = -lp3 if lp3 < 0 else lp3
        env = F32(env + F32(F32(a - env) * (aAtk if a > env else aRel)))
        low[i] = lp3; en[i] = env

    def f(lo, ev):
        h = np.zeros_like(lo)
        ok = ev > 1e-4
        nz = np.clip(lo[ok] / ev[ok], -1.5, 1.5)
        n2 = nz * nz
        h[ok] = ((n2 - 0.5) * 0.8 + n2 * nz * 0.6) * ev[ok]
        return h

    def salida(h):
        hpS = lpS = F32(0); out = np.empty(len(h))
        for i, s in enumerate(np.asarray(h, dtype=F32)):
            hpS = F32(hpS + F32(F32(s - hpS) * aHp)); t = F32(s - hpS)
            lpS = F32(lpS + F32(F32(t - lpS) * aLp)); out[i] = F32(lpS * mix)
        return out

    a1 = salida(f(low, en))
    ideal = salida(sin_plegado(f, low, en))
    agregado = y[: len(m), 0] - x[:, 0]
    w = np.blackman(len(m))
    P = lambda z: np.abs(np.fft.rfft(z * w)) ** 2
    pdes = P(ideal).sum()
    ref = (np.sum(w) / 2.0) ** 2
    print("GRAVE ALIAS %.2f PICO %.2f ESPEJO %.2f" % (db(P(agregado - ideal).sum() / max(pdes, 1e-30)),
                                                     db(P(agregado - ideal).max() / ref),
                                                     db(P(agregado - a1).sum() / max(pdes, 1e-30))))


if __name__ == "__main__":
    if len(sys.argv) >= 6 and sys.argv[1] == "realce":
        realce(sys.argv)
    elif len(sys.argv) >= 5 and sys.argv[1] == "grave":
        grave(sys.argv)
    else:
        print(__doc__)
        sys.exit(2)
