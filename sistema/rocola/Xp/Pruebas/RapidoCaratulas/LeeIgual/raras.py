# raras.py CARPETA: imagenes raras para lee-igual (las que no son JPEG en color ni PNG de 8 bits)
import os, sys, io, struct, zlib
import numpy as np
from PIL import Image, ImageCms
d = sys.argv[1]; os.makedirs(d, exist_ok=True)
rng = np.random.default_rng(7)
def rgb(w, h): return Image.fromarray(rng.integers(0, 256, (h, w, 3), dtype=np.uint8), "RGB")
def rgba(w, h): return Image.fromarray(rng.integers(0, 256, (h, w, 4), dtype=np.uint8), "RGBA")
rgb(300, 200).save(f"{d}/rgb.jpg", quality=90)
rgb(300, 200).save(f"{d}/prog.jpg", quality=80, progressive=True)
rgb(301, 199).convert("L").save(f"{d}/gris.jpg")
rgb(120, 90).convert("CMYK").save(f"{d}/cmyk.jpg")
try:
    p = ImageCms.createProfile("LAB"); 
except Exception: pass
im = rgb(200, 150); im.save(f"{d}/icc.jpg", icc_profile=ImageCms.ImageCmsProfile(ImageCms.createProfile("sRGB")).tobytes())
im.save(f"{d}/exif6.jpg", exif=b"Exif\x00\x00MM\x00*\x00\x00\x00\x08\x00\x01\x01\x12\x00\x03\x00\x00\x00\x01\x00\x06\x00\x00\x00\x00\x00\x00")
b = open(f"{d}/rgb.jpg", "rb").read(); open(f"{d}/cortada.jpg", "wb").write(b[: len(b) * 2 // 3])
rgb(256, 256).save(f"{d}/rgb.png"); rgba(256, 256).save(f"{d}/rgba.png"); rgba(97, 33).save(f"{d}/rgba-impar.png")
b = open(f"{d}/rgba.png", "rb").read(); open(f"{d}/cortada.png", "wb").write(b[: len(b) // 2])
Image.fromarray((rng.integers(0, 65536, (64, 80), dtype=np.uint16)), "I;16").save(f"{d}/gris16.png")
a16 = rng.integers(0, 65536, (50, 70, 3), dtype=np.uint16)
# PNG RGB de 16 bits a mano (PIL no los escribe)
def png16(path, arr):
    h, w, c = arr.shape; raw = b"".join(b"\x00" + arr[y].astype(">u2").tobytes() for y in range(h))
    def ch(t, x): return struct.pack(">I", len(x)) + t + x + struct.pack(">I", zlib.crc32(t + x) & 0xffffffff)
    open(path, "wb").write(b"\x89PNG\r\n\x1a\n" + ch(b"IHDR", struct.pack(">IIBBBBB", w, h, 16, 2 if c == 3 else 6, 0, 0, 0)) + ch(b"IDAT", zlib.compress(raw)) + ch(b"IEND", b""))
png16(f"{d}/rgb16.png", a16); png16(f"{d}/rgba16.png", rng.integers(0, 65536, (40, 30, 4), dtype=np.uint16))
rgb(100, 100).convert("L").save(f"{d}/gris.png")
rgba(90, 60).convert("LA").save(f"{d}/grisalfa.png")
pal = rgb(80, 80).convert("P", palette=Image.ADAPTIVE, colors=64); pal.info["transparency"] = 3; pal.save(f"{d}/paleta-trns.png", transparency=3)
rgb(80, 80).convert("P", palette=Image.ADAPTIVE, colors=16).save(f"{d}/paleta.png")
rgba(120, 77).save(f"{d}/entrelazada.png", interlace=1) if False else None
rgb(64, 64).convert("1").save(f"{d}/unbit.png")
g = rgb(60, 40).convert("P", palette=Image.ADAPTIVE, colors=32); g.save(f"{d}/trans.gif", transparency=5); rgb(60, 40).convert("P").save(f"{d}/simple.gif")
fr = [rgb(40, 30).convert("P") for _ in range(3)]; fr[0].save(f"{d}/animada.gif", save_all=True, append_images=fr[1:], duration=100, loop=0)
rgb(70, 50).save(f"{d}/rgb24.bmp"); rgba(70, 50).save(f"{d}/rgba32.bmp")
rgb(90, 60).save(f"{d}/x.webp", quality=80); rgba(90, 60).save(f"{d}/alfa.webp", lossless=True)
rgb(1, 1).save(f"{d}/uno.png"); rgb(1, 1).save(f"{d}/uno.jpg")
rgb(3000, 40).save(f"{d}/ancha.jpg")
open(f"{d}/basura.jpg", "wb").write(bytes(rng.integers(0, 256, 5000, dtype=np.uint8)))
open(f"{d}/vacia.png", "wb").write(b"")
open(f"{d}/cabecera.png", "wb").write(b"\x89PNG\r\n\x1a\n")
