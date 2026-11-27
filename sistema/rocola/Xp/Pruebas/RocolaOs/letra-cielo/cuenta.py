#!/usr/bin/env python3
# cuenta.py CINTA: de una cinta de la tarjeta (ROCOLA_CINTA), cuantos dibujos de textura (DrawTexQuad, metodo 12) hay por cuadro, en promedio, y cuantas
# texturas se crearon (CreateTexturePow2, 17): lo que anade la letra del cielo (4 o 5 dibujos por cuadro). Necesita brotli (PYTHONPATH).
import sys, os, struct
sys.path.insert(0, os.path.join(os.path.dirname(os.path.realpath(__file__)), "../cielo"))
import cinta
cuadros = 0; quads = 0; texturas = 0
for tipo, datos in cinta.lee(sys.argv[1]):
    if tipo != 2: continue
    disp, metodo = struct.unpack_from('<iB', datos, 0)
    if metodo == 5: cuadros += 1
    elif metodo == 12: quads += 1
    elif metodo == 17: texturas += 1
print("cuadros=%d quads_por_cuadro=%.2f texturas_creadas=%d" % (cuadros, quads / max(1, cuadros), texturas))
