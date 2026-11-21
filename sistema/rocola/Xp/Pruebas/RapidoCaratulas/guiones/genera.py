# Biblioteca inventada para medir las caratulas (rapido-caratulas): carpetas de disco con una portada generada y un MP3 corto.
import os, sys, random, math
import numpy as np
from PIL import Image, ImageDraw
from multiprocessing import Pool

RAIZ = sys.argv[1]; N = int(sys.argv[2]); MP3 = sys.argv[3]
PAL = ["Amor","Banda","Coro","Duo","Eco","Fiesta","Grupo","Hermanos","Isla","Jarocho","Kiosko","Luna","Mariachi","Norte","Orquesta","Palomas","Quinteto","Rayo","Sonora","Trio","Union","Voces","Wanda","Xochitl","Yolanda","Zafiro",
       "Angeles","Brisa","Cumbia","Danzon","Estrellas","Fuego","Grillos","Huapango","Iris","Joyas","Lobos","Marea","Nube","Ola","Piedra","Rio","Sol","Tigres","Valle"]
PESO = {"A":9,"B":5,"C":8,"D":4,"E":5,"F":3,"G":4,"H":3,"I":2,"J":3,"K":1,"L":9,"M":8,"N":3,"O":3,"P":4,"Q":1,"R":4,"S":6,"T":5,"U":1,"V":3,"W":1,"X":1,"Y":1,"Z":1}

def portada(rng, w, h, gris=False, alfa=False):
    # degradado + manchas + algo de grano: comprime como una portada de verdad (ni plana ni ruido puro)
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    c0 = rng.integers(0, 255, 3); c1 = rng.integers(0, 255, 3)
    t = (x / max(1, w - 1) * rng.random() + y / max(1, h - 1) * (1 - rng.random() * 0.5)) / 1.5
    img = (c0[None, None, :] * (1 - t[..., None]) + c1[None, None, :] * t[..., None])
    for _ in range(rng.integers(3, 9)):
        cx, cy, r = rng.random() * w, rng.random() * h, (0.05 + rng.random() * 0.3) * min(w, h)
        d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2)
        m = np.clip((r - d) / (r * 0.08 + 1), 0, 1)[..., None]
        img = img * (1 - m) + rng.integers(0, 255, 3)[None, None, :] * m
    img += rng.normal(0, 6, img.shape)
    img = np.clip(img, 0, 255).astype(np.uint8)
    im = Image.fromarray(img, "RGB")
    d = ImageDraw.Draw(im)
    for _ in range(rng.integers(2, 6)):
        x0, y0 = rng.random() * w, rng.random() * h
        d.rectangle([x0, y0, x0 + rng.random() * w * 0.4, y0 + max(4, rng.random() * h * 0.08)], fill=tuple(int(v) for v in rng.integers(0, 255, 3)))
    if gris: im = im.convert("L")
    if alfa:
        a = np.full((h, w), 255, np.uint8)
        a[: h // 6, :] = 0; a[:, : w // 8] = (np.arange(w // 8)[None, :] * 255 // max(1, w // 8)).astype(np.uint8)
        im = im.convert("RGBA"); im.putalpha(Image.fromarray(a, "L"))
    return im

def uno(i):
    rng = np.random.default_rng(1000 + i); r = random.Random(1000 + i)
    letras = list(PESO.keys()); pesos = [PESO[k] for k in letras]
    L = r.choices(letras, pesos)[0]
    nombres = [p for p in PAL if p[0] == L] or [L + "nonimo"]
    nombre = "%s %s %04d" % (r.choice(nombres), r.choice(PAL), i)
    carpeta = os.path.join(RAIZ, nombre); disco = os.path.join(carpeta, "Disco %04d" % i)
    os.makedirs(disco, exist_ok=True)
    for k in range(1, 3):
        dst = os.path.join(disco, "%02d Cancion %d.mp3" % (k, k))
        if not os.path.exists(dst): os.link(MP3, dst)
    u = rng.random()
    if u < 0.04: return "sin"
    if u < 0.38: w = int(rng.integers(500, 701)); im = portada(rng, w, w); im.save(os.path.join(carpeta, "cover.jpg"), quality=90); return "jpg-chica"
    if u < 0.62: w = int(rng.integers(1000, 1401)); h = w if rng.random() < 0.8 else int(w * 0.9); im = portada(rng, w, h); im.save(os.path.join(carpeta, "folder.jpg"), quality=88); return "jpg-media"
    if u < 0.76: w = int(rng.integers(1500, 3001)); im = portada(rng, w, w); im.save(os.path.join(carpeta, "cover.jpg"), quality=85, progressive=bool(rng.random() < 0.4)); return "jpg-grande"
    if u < 0.86: w = int(rng.integers(400, 901)); im = portada(rng, w, w); im.save(os.path.join(carpeta, "cover.png"), optimize=False); return "png"
    if u < 0.91: w = int(rng.integers(300, 601)); im = portada(rng, w, w, alfa=True); im.save(os.path.join(carpeta, "cover.png")); return "png-alfa"
    if u < 0.95: w = 600; im = portada(rng, w, w, gris=True); im.save(os.path.join(carpeta, "front.jpg"), quality=90); return "jpg-gris"
    w = int(rng.integers(250, 500)); im = portada(rng, w, w); im.save(os.path.join(carpeta, "Portada del disco.jpg"), quality=92); return "jpg-otro-nombre"

if __name__ == "__main__":
    os.makedirs(RAIZ, exist_ok=True)
    with Pool(3) as p: res = p.map(uno, range(N), chunksize=16)
    from collections import Counter; print(Counter(res))
