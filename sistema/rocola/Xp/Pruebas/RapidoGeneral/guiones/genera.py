# Biblioteca inventada para medir lo general (rapido-general): artistas con 1-3 discos de 8-14 canciones (MP3 de 8 s enlazados de 6
# bases, sin gastar disco) y portadas generadas chicas (para que no domine la carga de las caratulas, que es de otra rama).
import os, sys, random
from PIL import Image, ImageDraw
RAIZ, NART, BASES = sys.argv[1], int(sys.argv[2]), sys.argv[3]
PAL = ["Amor","Banda","Coro","Duo","Eco","Fiesta","Grupo","Hermanos","Isla","Jarocho","Kiosko","Luna","Mariachi","Norte","Orquesta",
       "Palomas","Quinteto","Rayo","Sonora","Trio","Union","Voces","Angeles","Brisa","Cumbia","Danzon","Estrellas","Fuego","Lobos","Marea",
       "Nube","Ola","Piedra","Rio","Sol","Tigres","Valle","Ángel","Ñandú","Corazón","Canción","Último"]
PAL2 = ["de la Sierra","del Norte","Azul","Romántica","de Oro","Tropical","Sin Fin","del Mar","de Plata","Eterna","Viva","Nueva"]
PALC = ["Te quiero","Mi corazón","La noche","Bailando","Sin ti","Cumbia del sol","Amor eterno","El camino","Volver","Paloma negra",
        "La flor","Mañana","Tu mirada","Recuerdos","Mi tierra","Corrido del valle","Bésame","Llorarás","Querida","El último adiós"]
r = random.Random(7)
bases = [os.path.join(BASES, "base%d.mp3" % i) for i in range(1, 7)]
os.makedirs(RAIZ, exist_ok=True)
n = 0
for a in range(NART):
    art = "%s %s %04d" % (r.choice(PAL), r.choice(PAL2), a)
    for d in range(r.choice([1, 1, 2, 3])):
        disco = os.path.join(RAIZ, art, "%s %d" % (r.choice(PALC), d + 1))
        os.makedirs(disco, exist_ok=True)
        for k in range(r.randint(8, 14)):
            dst = os.path.join(disco, "%02d %s %d.mp3" % (k + 1, r.choice(PALC), r.randint(1, 999)))
            if not os.path.exists(dst): os.link(r.choice(bases), dst); n += 1
        if r.random() < 0.9:
            w = r.choice([300, 400, 500])
            im = Image.new("RGB", (w, w), tuple(r.randint(0, 255) for _ in range(3)))
            dr = ImageDraw.Draw(im)
            for _ in range(6):
                x, y = r.randint(0, w), r.randint(0, w)
                dr.ellipse([x, y, x + r.randint(20, w // 2), y + r.randint(20, w // 2)], fill=tuple(r.randint(0, 255) for _ in range(3)))
            im.save(os.path.join(disco, "folder.jpg"), quality=85)
print("canciones", n)
