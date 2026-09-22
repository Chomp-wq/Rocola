#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""CONTRATO DEL EXTRACTOR DEL DSEE: la MISMA geometria que usa la rocola al reproducir.

La red se aplica en la rocola (SpectralTap.RegenNeural) y el material que junta la propia rocola
sale de SpectralTap.MakePair. Esas dos funciones y BuildIaFeatures son, por tanto, las que
MANDAN: este modulo las transcribe paso a paso, con el mismo redondeo a float32 alli donde C#
calcula en float, para que lo que se entrena aqui sean exactamente las 28 entradas y los 12
objetivos que la rocola vera y rellenara.

Version del extractor (EXTRACTOR):
  1 = el extractor Python anterior. Se apartaba de la rocola en cinco cosas: techo de 20 kHz en
      vez del topBin de la rocola (20,5 kHz, 2*corte-2 bins, ny-4), corte sin redondear a bin,
      entrada 25 y bandas de entrada con ese corte sin redondear, tope superior del objetivo +0,5
      en vez de +0,1, y bandas vacias que se estiraban en vez de invalidar el par.
  2 = la geometria de reproduccion (este modulo). MakePair/BuildIaFeatures no han cambiado desde
      que existen (3 de agosto de 2026), asi que lo que la rocola ya junto esta en esta geometria.

Uso como programa (lo lanza la bateria pruebas/iaparidad.cs):
  python3 contrato_dsee.py --generar-paridad paridad_extractor.txt
  python3 contrato_dsee.py --comparar volcado_de_la_rocola.txt
"""
import math
import sys
from functools import lru_cache

import numpy as np

EXTRACTOR = 2
F32 = np.float32
_TOPE_ALTO = F32(0.1)      # MakePair/RegenNeural: "if (y > 0.1f) y = 0.1f"
_TOPE_BAJO = F32(-4.0)


def bin_hz(rate, frame):
    """binHz = (float)rate / Frame, en float32."""
    return F32(F32(rate) / F32(frame))


def _hz(hz, bhz, ny):
    """SpectralTap.Hz: (int)(hz / binHz), limitado a [1, ny-1]."""
    b = int(F32(F32(hz) / bhz))
    return 1 if b < 1 else (ny - 1 if b > ny - 1 else b)


def corte_de(rate, frame, cut_hz_sim):
    """MakePair: int cut = (int)(cutHzSim / binHz)."""
    return int(F32(F32(cut_hz_sim) / bin_hz(rate, frame)))


def top_bin(rate, frame, cut):
    """topBin = Min((int)(Min(20500f, ny*binHz*0.95f) / binHz), Min(2*cut-2, ny-4))."""
    ny = frame // 2
    bhz = bin_hz(rate, frame)
    techo = min(F32(20500.0), F32(F32(F32(ny) * bhz) * F32(0.95)))
    return min(int(F32(techo / bhz)), min(2 * cut - 2, ny - 4))


@lru_cache(maxsize=4096)
def geometria(rate, frame, cut):
    """Geometria completa para un corte ENTERO (en bins). Devuelve un dict inmutable en la practica:
      valido     -> False si MakePair devolveria false
      motivo     -> por que no es valido
      entrada    -> 24 pares (a, b) de BuildIaFeatures
      costura    -> (sa, cut)
      top        -> topBin
      objetivo   -> 12 pares (a, b) tal como los usa MakePair (ya recortados a [cut, topBin])
      regen      -> 12 pares (a, b) o None (banda que RegenNeural se salta con 'continue')
    """
    ny = frame // 2
    bhz = bin_hz(rate, frame)
    g = {"rate": rate, "frame": frame, "cut": cut, "valido": False, "motivo": "",
         "entrada": None, "costura": None, "top": None, "objetivo": None, "regen": None}
    if cut < 64:
        g["motivo"] = "corte < 64 bins"
        return g
    tb = top_bin(rate, frame, cut)
    g["top"] = tb
    if tb <= cut + 4:
        g["motivo"] = "topBin <= corte + 4"
        return g
    fc = F32(F32(cut) * bhz)
    if fc < F32(1200.0):
        g["motivo"] = "fc < 1200 Hz"
        return g
    fc200 = float(F32(fc / F32(200.0)))
    entrada = []
    for i in range(24):
        a = _hz(F32(200.0) * F32(math.pow(fc200, i / 24.0)), bhz, ny)
        b = _hz(F32(200.0) * F32(math.pow(fc200, (i + 1) / 24.0)), bhz, ny)
        if b <= a:
            b = a + 1
        entrada.append((a, b))
    g["entrada"] = tuple(entrada)
    sa = _hz(F32(fc * F32(0.92)), bhz, ny)
    if sa >= cut:
        sa = cut - 1
    g["costura"] = (sa, cut)
    top = F32(F32(tb) * bhz)
    razon = float(F32(top / fc))
    objetivo, regen, valido = [], [], True
    for j in range(12):
        a = _hz(fc * F32(math.pow(razon, j / 12.0)), bhz, ny)
        b = _hz(fc * F32(math.pow(razon, (j + 1) / 12.0)), bhz, ny)
        if a < cut:
            a = cut
        if b > tb:
            b = tb
        if b <= a:
            valido = False
            regen.append(None)
        else:
            regen.append((a, b))
        objetivo.append((a, b))
    g["objetivo"] = tuple(objetivo)
    g["regen"] = tuple(regen)
    g["valido"] = valido
    if not valido:
        g["motivo"] = "banda objetivo vacia"
    return g


def _potencia(re, im):
    """re[k]*re[k] + im[k]*im[k] calculado en float32, como en C#."""
    re = np.asarray(re, dtype=F32)
    im = np.asarray(im, dtype=F32)
    return re * re + im * im


def par_desde_espectro(rate, frame, cut_hz_sim, re, im):
    """MakePair(cutHzSim) sobre un espectro ya calculado (re/im del cuadro, sin normalizar).
    Devuelve (x28, y12) en float32, o None si la rocola devolveria false."""
    cut = corte_de(rate, frame, cut_hz_sim)
    return par_desde_potencia(rate, frame, cut, _potencia(re, im))


def par_desde_potencia(rate, frame, cut, pot):
    """El mismo par a partir de la potencia por bin |X[k]|^2 (sin dividir por N^2)."""
    g = geometria(rate, frame, cut)
    if not g["valido"]:
        return None
    n2 = F32(F32(frame) * F32(frame))
    x = caracteristicas(g, pot, n2)
    sa, _ = g["costura"]
    costura = float(np.sum(pot[sa:cut], dtype=np.float64)) / float(F32(F32(cut - sa) * n2)) + 1e-12
    y = np.empty(12, dtype=F32)
    for j, (a, b) in enumerate(g["objetivo"]):
        real = float(np.sum(pot[a:b], dtype=np.float64)) / float(F32(F32(b - a) * n2)) + 1e-12
        v = F32(math.log10(real / costura))
        y[j] = _TOPE_ALTO if v > _TOPE_ALTO else (_TOPE_BAJO if v < _TOPE_BAJO else v)
    return x, y


def caracteristicas(g, pot, n2):
    """BuildIaFeatures(cut): las 28 entradas en float32."""
    x = np.zeros(28, dtype=F32)
    suma = 0.0
    for i, (a, b) in enumerate(g["entrada"]):
        e = float(np.sum(pot[a:b], dtype=np.float64))
        x[i] = F32(math.log10(e / float(F32(F32(b - a) * n2)) + 1e-12))
        suma += float(x[i])
    media = F32(suma / 24.0)
    x[:24] = x[:24] - media
    x[24] = media
    fc = F32(F32(g["cut"]) * bin_hz(g["rate"], g["frame"]))
    x[25] = F32(fc / F32(20000.0))
    am = gm = 0.0
    for i in range(16, 24):
        v = float(F32(x[i] + media))
        am += math.pow(10.0, v)
        gm += v
    x[26] = F32(gm / 8.0 - math.log10(am / 8.0 + 1e-12))
    x[27] = F32(0.0)
    return x


# ---------------------------------------------------------------------------------------------
#  Senales reproducibles EXACTAMENTE igual en C# y en Python: generador congruente de 32 bits y
#  escalas que son potencias de dos (multiplicar por ellas no redondea).
# ---------------------------------------------------------------------------------------------
_ESCALAS = np.array([1.0, 0.25, 0.0625, 0.015625, 2.0 ** -8, 2.0 ** -10, 2.0 ** -12, 2.0 ** -14], dtype=F32)


class Lcg:
    def __init__(self, semilla):
        self.s = semilla & 0xFFFFFFFF

    def siguiente(self):
        self.s = (self.s * 1664525 + 1013904223) & 0xFFFFFFFF
        return self.s

    def valor(self):
        u = F32(F32(self.siguiente() >> 8) * F32(1.0 / 16777216.0))
        esc = _ESCALAS[self.siguiente() >> 29]
        return F32(F32(u - F32(0.5)) * esc)


def espectro_lcg(frame, semilla):
    g = Lcg(semilla)
    re = np.empty(frame, dtype=F32)
    im = np.empty(frame, dtype=F32)
    for k in range(frame):
        re[k] = g.valor()
        im[k] = g.valor()
    return re, im


def senal_lcg(frame, rate, semilla):
    """Tonos + ruido: sirve para probar tambien la ventana y la FFT. El seno es de doble precision
    en los dos lenguajes; lo que difiera en el ultimo bit queda muy por debajo de la tolerancia."""
    g = Lcg(semilla)
    x = np.empty(frame, dtype=F32)
    tonos = ((220.0, 0.30), (1375.0, 0.12), (4410.0, 0.05), (9100.0, 0.02), (15300.0, 0.008))
    for n in range(frame):
        v = 0.0
        for hz, amp in tonos:
            v += amp * math.sin(2.0 * math.pi * hz * n / rate)
        x[n] = F32(F32(v) + F32(g.valor() * F32(0.125)))
    return x


def ventana(frame):
    """AudioFftPlan.Window: (float)Math.Sqrt(0.5 - 0.5 * Math.Cos(2*pi*i/N))."""
    i = np.arange(frame, dtype=np.float64)
    return np.sqrt(0.5 - 0.5 * np.cos(2.0 * np.pi * i / frame)).astype(F32)


def espectro_de_senal(x):
    """AnalyzeFrame: re[i] = mono[i] * win[i] en float y FFT. Aqui la FFT es de doble precision;
    la de la rocola es float32: de ahi la tolerancia mas amplia de estos casos."""
    frame = len(x)
    X = np.fft.fft((np.asarray(x, dtype=F32) * ventana(frame)).astype(np.float64))
    return X.real, X.imag


# ---------------------------------------------------------------------------------------------
#  Vectores de paridad (archivo pequeno que viaja en el repositorio) y comparador del volcado
# ---------------------------------------------------------------------------------------------
TASAS = (22050, 32000, 44100, 48000, 96000)
CUADROS = (2048, 4096)
TOL_ESPECTRO = 2e-5     # log10: solo difiere el orden de las sumas en doble precision
TOL_SENAL = 2e-3        # log10: FFT float32 de la rocola frente a FFT de doble precision


def _cortes_de_prueba(rate, frame):
    """Los cortes simulados que usa la rocola, mas bordes de redondeo y de validez."""
    bhz = float(bin_hz(rate, frame))
    ny = frame // 2
    hz = [11000.0, 13000.0, 15000.0, 16500.0, 18000.0, 1100.0, 9000.0, 19300.0]
    # Donde el techo cambia de "2*corte-2" a "20,5 kHz / Nyquist": el corte cuyo doble lo cruza.
    tb_lim = top_bin(rate, frame, ny)   # con corte enorme manda el techo fijo
    for c in (tb_lim // 2, tb_lim // 2 + 1, tb_lim // 2 + 2, 64, 65, ny - 6, ny - 5):
        hz.append((c + 0.5) * bhz)
    return [float(F32(h)) for h in hz]


def _fmt(v):
    return '%.9g' % float(F32(v))


def genera_paridad():
    lineas = ["# PARIDAD DEL EXTRACTOR DSEE - contrato EXTRACTOR=%d (geometria de reproduccion de la rocola)" % EXTRACTOR,
              "# Generado por ia/contrato_dsee.py --generar-paridad. NO editar a mano.",
              "# tipo S = espectro inyectado (tolerancia %g en log10); T = senal por ventana y FFT (tolerancia %g)."
              % (TOL_ESPECTRO, TOL_SENAL),
              "# tipo tasa cuadro corteHzSim semilla valido : 28 entradas : 12 objetivos"]
    semilla = 1
    for rate in TASAS:
        for frame in CUADROS:
            for c in _cortes_de_prueba(rate, frame):
                for tipo in ("S", "T"):
                    if tipo == "S":
                        re, im = espectro_lcg(frame, semilla)
                    else:
                        re, im = espectro_de_senal(senal_lcg(frame, rate, semilla))
                    r = par_desde_espectro(rate, frame, c, re, im) if tipo == "S" else \
                        par_desde_potencia(rate, frame, corte_de(rate, frame, c), re * re + im * im)
                    cab = "%s %d %d %s %d %d" % (tipo, rate, frame, _fmt(c), semilla, 1 if r else 0)
                    if r:
                        cab += " : " + " ".join(_fmt(v) for v in r[0]) + " : " + " ".join(_fmt(v) for v in r[1])
                    lineas.append(cab)
                    semilla += 1
    return "\n".join(lineas) + "\n"


def lee_paridad(texto):
    casos = []
    for linea in texto.splitlines():
        if not linea or linea.startswith("#"):
            continue
        partes = linea.split(":")
        cab = partes[0].split()
        caso = {"tipo": cab[0], "rate": int(cab[1]), "frame": int(cab[2]), "corte": float(cab[3]),
                "semilla": int(cab[4]), "valido": cab[5] == "1", "x": None, "y": None}
        if caso["valido"]:
            caso["x"] = np.array([float(v) for v in partes[1].split()], dtype=F32)
            caso["y"] = np.array([float(v) for v in partes[2].split()], dtype=F32)
        casos.append(caso)
    return casos


def compara_volcado(ruta):
    """Lee lo que escribio la rocola y lo contrasta con este modulo. Formato por linea:
      P tasa cuadro corteHzSim semilla valido [28 entradas] [12 objetivos]   (MakePair)
      R tasa cuadro corte topBin a0 b0 ... a11 b11                          (bandas de RegenNeural; -1 -1 = saltada)
    Devuelve (lineas comprobadas, lista de discrepancias, peor diferencia)."""
    vistos, malos, peor = 0, [], 0.0
    espectros = {}
    with open(ruta, "r", encoding="ascii") as f:
        for linea in f:
            t = linea.split()
            if not t:
                continue
            vistos += 1
            if t[0] == "P":
                rate, frame, c, sem, val = int(t[1]), int(t[2]), float(t[3]), int(t[4]), t[5] == "1"
                clave = (frame, sem)
                if clave not in espectros:
                    espectros = {clave: espectro_lcg(frame, sem)}
                re, im = espectros[clave]
                r = par_desde_espectro(rate, frame, c, re, im)
                if (r is not None) != val:
                    malos.append("validez distinta: " + " ".join(t[:6]))
                    continue
                if r is None:
                    continue
                cs = np.array([float(v) for v in t[6:46]], dtype=np.float64)
                d = float(np.max(np.abs(cs - np.concatenate(r).astype(np.float64))))
                peor = max(peor, d)
                if d > TOL_ESPECTRO:
                    malos.append("valores distintos (%.3g): %s" % (d, " ".join(t[:6])))
            elif t[0] == "R":
                rate, frame, cut, tb = int(t[1]), int(t[2]), int(t[3]), int(t[4])
                g = geometria(rate, frame, cut)
                if g["top"] != tb:
                    malos.append("topBin distinto: " + " ".join(t[:5]))
                    continue
                bandas = [(int(t[5 + 2 * j]), int(t[6 + 2 * j])) for j in range(12)]
                esperado = [b if b is not None else (-1, -1) for b in g["regen"]]
                if bandas != esperado:
                    malos.append("bandas de RegenNeural distintas: " + " ".join(t[:5]))
            else:
                malos.append("linea desconocida: " + linea.strip()[:60])
    return vistos, malos, peor


def margen_redondeo(rates=(22050, 32000, 44100, 48000, 88200, 96000), frames=CUADROS):
    """Cuantas divisiones hz/binHz de TODA la geometria cambiarian de bin si se hicieran sin
    redondear a float32 (lo que puede hacer la FPU x87 de un Windows XP de 32 bits). Devuelve
    (divisiones mirando, las que cambiarian, distancia minima al entero en unidades de bin)."""
    total, cambian, minimo = 0, 0, 1.0

    def mira(hz_exacto, hz32, bhz):
        nonlocal total, cambian, minimo
        q32 = float(F32(F32(hz32) / bhz))
        qx = hz_exacto / float(bhz)
        total += 1
        if int(q32) != int(qx):
            cambian += 1
        minimo = min(minimo, abs(qx - round(qx)))

    for rate in rates:
        for frame in frames:
            ny = frame // 2
            bhz = bin_hz(rate, frame)
            for cut in range(64, ny):
                g = geometria(rate, frame, cut)
                if g["entrada"] is None:
                    continue
                fc = F32(F32(cut) * bhz)
                fc200 = float(F32(fc / F32(200.0)))
                for i in range(25):
                    p = F32(math.pow(fc200, i / 24.0))
                    mira(200.0 * float(p), F32(F32(200.0) * p), bhz)
                mira(float(fc) * 0.92, F32(fc * F32(0.92)), bhz)
                if g["objetivo"] is None:
                    continue
                top = F32(F32(g["top"]) * bhz)
                razon = float(F32(top / fc))
                for j in range(13):
                    p = F32(math.pow(razon, j / 12.0))
                    mira(float(fc) * float(p), F32(fc * p), bhz)
    return total, cambian, minimo


def main(argv):
    if len(argv) == 2 and argv[0] == "--generar-paridad":
        with open(argv[1], "w", encoding="ascii", newline="\n") as f:
            f.write(genera_paridad())
        return 0
    if len(argv) == 2 and argv[0] == "--comparar":
        vistos, malos, peor = compara_volcado(argv[1])
        print("LINEAS %d" % vistos)
        print("PEOR %.9g" % peor)
        print("DISCREPANCIAS %d" % len(malos))
        for m in malos[:12]:
            print("  " + m)
        return 0 if (vistos > 0 and not malos) else 1
    if len(argv) == 1 and argv[0] == "--margen":
        total, cambian, minimo = margen_redondeo()
        print("DIVISIONES %d" % total)
        print("CAMBIARIAN %d" % cambian)
        print("MINIMO %.9g" % minimo)
        return 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
