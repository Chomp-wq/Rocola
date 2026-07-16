# -*- coding: utf-8 -*-
# Prueba de las utilerias SIN GPU (se corre en cualquier maquina: python3 test_util.py)
import numpy as np, os, tempfile, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from util_audio import *

fails = 0
rng = np.random.default_rng(5)

# WAV ida y vuelta
x = rng.uniform(-0.5, 0.5, (44100, 2))
p = os.path.join(tempfile.gettempdir(), 'ut.wav')
escribir_wav(p, x); y, fs = leer_wav(p)
e = np.abs(y - x).max()
print('[1] WAV ida-vuelta: error max %.6f' % e); fails += 0 if e < 1e-3 and fs == 44100 else 1

# acantilado: senal cortada a ~12k debe medir ~12k; completa debe medir >19k
t = np.arange(44100 * 3) / 44100.0
lim = sum(np.sin(2 * np.pi * f * t + f) for f in range(300, 11800, 400)) * 0.02
ful = sum(np.sin(2 * np.pi * f * t + f) for f in range(300, 20800, 400)) * 0.02
c1 = acantilado_hz(np.stack([lim, lim], 1)); c2 = acantilado_hz(np.stack([ful, ful], 1))
print('[2] acantilado: cortada=%.0f Hz (esperado ~12000), completa=%.0f Hz (>19000)' % (c1, c2))
fails += 0 if 11000 < c1 < 13200 and c2 > 19000 else 1

# igualar RMS
a = rng.normal(0, 0.2, (44100, 2)); b = rng.normal(0, 0.05, (44100, 2))
m = igualar_rms(b, a)
r = np.sqrt((m ** 2).mean()) / np.sqrt((a ** 2).mean())
print('[3] igualar RMS: razon %.4f (esperado 1.0)' % r); fails += 0 if abs(r - 1) < 1e-6 else 1

# por_trozos: identidad debe devolver la senal EXACTA; escala x2 debe dar 2x
z = rng.uniform(-0.3, 0.3, (44100 * 7 + 123, 2))
i1 = por_trozos(z, lambda c: c); e1 = np.abs(i1 - z).max()
i2 = por_trozos(z, lambda c: c * 2.0); e2 = np.abs(i2 - 2 * z).max()
print('[4] por_trozos: identidad err %.2e, escala err %.2e' % (e1, e2))
fails += 0 if e1 < 1e-9 and e2 < 1e-9 else 1

print('TODO PASO' if fails == 0 else '%d FALLA(S)' % fails)
sys.exit(fails)
