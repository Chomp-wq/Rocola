#!/usr/bin/env python3
# LOS RELAMPAGOS DE UNA CORRIDA (ROCOLA_CIELO_RELAMPAGOS=archivo: la hora de cada comienzo, en segundos, un renglon por relampago): cuenta cuantos
# hubo y mide el hueco menor entre dos comienzos. CL3 promete como mucho uno cada 6 s: sale con 0 si hubo 3 o mas y ningun hueco menor de 6 s.
#   relampagos.py ARCHIVO
import sys
try:
    t = [float(x) for x in open(sys.argv[1]).read().split()]
except Exception:
    t = []
huecos = [b - a for a, b in zip(t, t[1:])]
print('%d relampagos; el hueco menor %.2f s; los comienzos: %s' % (len(t), min(huecos) if huecos else 0, ' '.join('%.1f' % x for x in t)))
sys.exit(0 if len(t) >= 3 and (not huecos or min(huecos) >= 6.0) else 1)
