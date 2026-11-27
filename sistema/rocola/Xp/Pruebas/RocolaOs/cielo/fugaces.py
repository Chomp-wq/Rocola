#!/usr/bin/env python3
# LAS FUGACES DE UNA CINTA (ROCOLA_CINTA): de cada DrawShootStreaks (metodo 9, las fugaces al azar y la de festejo), los vertices de la CABEZA (los
# de alfa > 8: la cola va siempre en alfa 0). Da «cuantas_cabezas cuadros_con_fugaz alfa_maximo». Con DN4 de dia (g300) las al azar van a alfa 0.
#   fugaces.py CINTA          (necesita brotli: PYTHONPATH=DIR)
import sys, struct
sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
import cinta

cabezas = 0; cuadros_con = 0; mx = 0; dentro = {}; con = {}
for tipo, datos in cinta.lee(sys.argv[1]):
    if tipo != 2: continue
    disp, metodo = struct.unpack_from('<iB', datos, 0)
    args = datos[13:]
    if metodo == 5: dentro[disp] = True; con[disp] = False; continue
    if metodo == 6:
        if con.get(disp): cuadros_con += 1
        dentro[disp] = False; continue
    if metodo != 9 or not dentro.get(disp): continue
    o = 4
    if len(args) < o + 8: continue
    vl, n = struct.unpack_from('<ii', args, o); o += 8
    vl = min(vl, len(args[o:]) // 28)
    for k in range(vl):
        a = struct.unpack_from('<I', args, o + k * 28 + 16)[0] >> 24
        if a > 8: cabezas += 1; con[disp] = True; mx = max(mx, a)
print(cabezas, cuadros_con, mx)
