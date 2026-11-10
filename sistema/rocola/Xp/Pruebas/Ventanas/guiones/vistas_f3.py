import sys
# (ventanas2, paso C) ROCOLA_PULSA y ROCOLA_CAPTURA para recorrer Ctrl+F3 con el raton por todas sus paginas, hojas y hojitas (f3vistas) o
# con el modo billar encendido (lo que decide, en gris con su motivo), el foco en distintas piezas y el raton encima de un boton (f3grises).
# Las coordenadas son de la pantalla de 1024x768: la hoja de 966x764 va en (29,2) y su cliente empieza en (32,31); cada boton por su centro,
# sacado de su SetBounds en Form1.Ajustes.cs, Rendimiento.cs, FluidezAnimaciones.cs, HojasFondo.cs y PantallaVivaGpu.cs.
#   python3 vistas_f3.py vistas|grises  -> la linea de PULSA y la de CAPTURAS
PEST = {"colores": (394, 54), "detalles": (500, 54), "sonido": (606, 54), "imagen": (712, 54), "fondo": (818, 54), "pantalla": (924, 54)}
AJUSTES = (868, 184); LOCAL = (748, 186); VOZ = (748, 244); AHORA = (748, 302); IA = (748, 360); VOLVER = (748, 494)
TONO = (276, 499); AFINAR = (748, 316); BILLAR = (654, 184)
IMAGEN_HOJAS = [(604, 151), (748, 151), (892, 151)]            # Efectos, Fluidez, Performance
PERF = [(575, 247), (659, 247), (743, 247), (827, 247), (911, 247)]   # Cielo, Grano, Aurora, Estrella, Estado
FONDO = [(138, 147), (324, 147), (510, 147), (696, 147), (882, 147)]
PANTALLA = [(198, 149), (510, 149), (822, 149)]
p = ["7:17+114"]; c = []; t = [8.5]
def clic(xy, cap=None, paso=1.2):
    p.append("%.1f:clic:%d:%d" % (t[0], xy[0], xy[1]))
    if cap: c.append("%.1f:%s" % (t[0] + 1.0, cap))
    t[0] += paso
def tecla(vk, cap=None, paso=1.2):
    p.append("%.1f:%s" % (t[0], vk))
    if cap: c.append("%.1f:%s" % (t[0] + 1.0, cap))
    t[0] += paso
modo = sys.argv[1]
if modo == "vistas":
    c.append("8.2:s0")
    clic(AJUSTES, "s1aju"); clic(LOCAL, "s2local"); clic(TONO, "s3tono"); clic(VOLVER); clic(AFINAR, "s4afinar"); clic(VOLVER); clic(VOLVER)
    clic(VOZ, "s5voz"); clic(VOLVER); clic(AHORA, "s6ahora"); clic(VOLVER); clic(IA, "s7ia"); clic(VOLVER); clic(VOLVER, "s8vuelta")
    clic(PEST["imagen"], "i1efectos"); clic(IMAGEN_HOJAS[1], "i2fluidez"); clic(IMAGEN_HOJAS[2], "i3cielo")
    for k, n in enumerate(["i4grano", "i5aurora", "i6estrella", "i7estado"]): clic(PERF[k + 1], n)
    clic(PEST["detalles"], "d1"); clic(PEST["colores"], "c1"); clic(PEST["fondo"], "f1")
    for k in range(1, 5): clic(FONDO[k], "f%d" % (k + 1))
    clic(PEST["pantalla"], "p1"); clic(PANTALLA[1], "p2"); clic(PANTALLA[2], "p3")
    tecla("27", "fin")
else:
    c.append("8.2:g0")
    clic(BILLAR, "g1billar"); clic(AJUSTES, "g2aju"); clic(LOCAL, "g3local"); clic(TONO, "g4tono"); clic(VOLVER); clic(AFINAR, "g5afinar")
    tecla("27")
    p.append("%.1f:17+114" % t[0]); t[0] += 1.5
    for k in range(7): tecla("9", paso=0.4)   # (siete Tab: el primer deslizador, «Cuanto se sube cada cancion»)
    c.append("%.1f:g6deslizador" % (t[0] + 0.6)); t[0] += 1.2
    for k in range(4): tecla("9", paso=0.4)   # (cuatro mas: el interruptor «Modo noche»)
    c.append("%.1f:g7interruptor" % (t[0] + 0.6)); t[0] += 1.2
    p.append("%.1f:raton:%d:%d" % (t[0], 906, 730)); c.append("%.1f:g8encima" % (t[0] + 1.0)); t[0] += 1.6   # (encima de «Listo»)
    tecla("27", "fin")
print(",".join(p))
print(",".join(c))
print("%.0f" % (t[0] + 3))
