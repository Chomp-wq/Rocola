#!/usr/bin/env python3
# compara_aviso.py DIR_A DIR_B
# La escena avisoviejo de dos corridas: la pastilla del aviso de siempre (HorneaToast, Form1.Ajustes.cs) tal como quedo en la capa de la
# tarjeta medio segundo despues de cada moneda. El aviso es un mapa de 620x48 centrado arriba (x = (1024 - 620) / 2, y = 80; a esa hora ya
# no se mueve ni se desvanece: CurvaToast) con la pastilla de 260 en su centro; lo de dentro de la pastilla tapa del todo el cielo, asi que
# tiene que salir punto por punto igual (las esquinas redondas y el borde dejan ver el cielo, que se mueve: no se miran).
import sys, os
import numpy as np
from PIL import Image
a, b = sys.argv[1], sys.argv[2]; malo = False
CAJA = (202 + 180 + 12, 80 + 3, 202 + 180 + 260 - 12, 80 + 48 - 3)
for n in ("a1", "a2"):
    f = n + ".png.capa2-tarjeta.png"
    ia = np.asarray(Image.open(os.path.join(a, f)).convert("RGBA").crop(CAJA)).astype(np.int16); ib = np.asarray(Image.open(os.path.join(b, f)).convert("RGBA").crop(CAJA)).astype(np.int16)
    d = int((ia != ib).any(axis=2).sum())
    # (que de verdad esta el aviso: verde de la moneda con letras blancas, y no el cielo)
    verde = int(((abs(ia[:, :, 0] - 40) < 3) & (abs(ia[:, :, 1] - 165) < 3) & (abs(ia[:, :, 2] - 90) < 3)).sum()); blanco = int((ia[:, :, :3].min(axis=2) > 240).sum())
    print("%s: %d puntos distintos en la pastilla (%dx%d; %d del fondo del aviso y %d de letra blanca en A)%s" % (n, d, ia.shape[1], ia.shape[0], verde, blanco, "" if d == 0 else " (DEBE SER 0)"))
    if d or verde < 1000 or blanco < 50: malo = True
print("RESULTADO: " + ("DIFERENTE" if malo else "IGUAL"))
sys.exit(1 if malo else 0)
