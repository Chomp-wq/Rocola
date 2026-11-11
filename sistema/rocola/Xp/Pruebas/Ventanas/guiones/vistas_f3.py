import sys
# (ventanas2, paso C) Los pasos de las escenas de Ctrl+F3 (escenas.sh): que se pulsa y cuando (ROCOLA_PULSA), que capturas (ROCOLA_CAPTURA)
# y en que momentos se vuelca el estado de XP y de cada pieza (ROCOLA_ESTADO_EN).
#   python3 vistas_f3.py vistas|grises|teclas|requisitos|guarda|diag|medir [SEGUNDOS_QUE_ESPERA_A_MEDIR]
#     -> cuatro lineas: PULSA, CAPTURAS, los segundos que dura y ESTADO
# Las coordenadas son de la pantalla de 1024x768: la hoja de 966x764 va en (29,2) y su cliente empieza en (32,31); cada boton por su centro,
# sacado de su SetBounds en Form1.Ajustes.cs, Rendimiento.cs, FluidezAnimaciones.cs, HojasFondo.cs y PantallaVivaGpu.cs.
# DOS REGLAS, por la maquina de pruebas (compartida: una tecla puede llegar tarde) y por la rocola:
#  - cada paso va separado del anterior, y una captura o un estado nunca cae junto al paso siguiente;
#  - Ctrl+F3 se abre a los 26 s y no a los 7: hasta los 20 s la rocola repasa la biblioteca y el letrero «faltan N archivos» de la pagina de
#    Sonido (que el volcado da aunque la pagina este escondida) cambia solo; una captura a medias cae distinta en dos corridas del MISMO
#    programa. Los tiempos de abajo se escriben como si se abriera a los 7 y al final se corren todos ESPERA segundos.
ESPERA = 19.0
PEST = {"colores": (394, 54), "detalles": (500, 54), "sonido": (606, 54), "imagen": (712, 54), "fondo": (818, 54), "pantalla": (924, 54)}
AJUSTES = (868, 184); LOCAL = (748, 186); VOZ = (748, 244); AHORA = (748, 302); IA = (748, 360); VOLVER = (748, 494)
TONO = (276, 499); AFINAR = (748, 316); BILLAR = (654, 184)
IMAGEN_HOJAS = [(604, 151), (748, 151), (892, 151)]            # Efectos, Fluidez, Performance
PERF = [(575, 247), (659, 247), (743, 247), (827, 247), (911, 247)]   # Cielo, Grano, Aurora, Estrella, Estado
FONDO = [(138, 147), (324, 147), (510, 147), (696, 147), (882, 147)]  # Cielo y aurora, Interfaz, Estrella, Cielo vivo, Pulido
PANTALLA = [(198, 149), (510, 149), (822, 149)]
MEDIR = (168, 189); DIAGNOSTICO = (384, 189); HISTORIAL = (432, 736)   # (Imagen) «Medir este equipo», «Diagnostico completo», «Comparar perfiles»
p = [(7.0, "17+114")]; c = []; e = []; t = [8.5]   # (Ctrl+F3)
cada = [False]   # (vistas) el estado de XP y de cada pieza en CADA vista, con el nombre de su captura
def paso(que, cap, sep):
    p.append((t[0], que))
    if cap: c.append((t[0] + 1.0, cap))
    if cap and cada[0]: e.append((t[0] + 1.2, cap)); sep = max(sep, 1.7)
    t[0] += sep
def clic(xy, cap=None, sep=1.2): paso("clic:%d:%d" % xy, cap, sep)
def tecla(vk, cap=None, sep=1.2): paso(vk, cap, sep)
def estado(nombre, antes=0.3): e.append((t[0] - antes, nombre))   # ANTES segundos antes del paso siguiente
def fin(): tecla("27"); estado("fin", 0.4); c.append((t[0] + 0.4, "fin"))   # Escape; el estado antes de la ultima captura, que cierra el programa
modo = sys.argv[1]
if modo == "vistas":
    c.append((8.2, "s0")); e.append((8.3, "s0")); cada[0] = True
    clic(AJUSTES, "s1aju"); clic(LOCAL, "s2local"); clic(TONO, "s3tono"); clic(VOLVER); clic(AFINAR, "s4afinar"); clic(VOLVER); clic(VOLVER)
    clic(VOZ, "s5voz"); clic(VOLVER); clic(AHORA, "s6ahora"); clic(VOLVER); clic(IA, "s7ia"); clic(VOLVER); clic(VOLVER, "s8vuelta")
    clic(PEST["imagen"], "i1efectos"); clic(IMAGEN_HOJAS[1], "i2fluidez"); clic(IMAGEN_HOJAS[2], "i3cielo")
    for k, n in enumerate(["i4grano", "i5aurora", "i6estrella", "i7estado"]): clic(PERF[k + 1], n)
    clic(PEST["detalles"], "d1"); clic(PEST["colores"], "c1"); clic(PEST["fondo"], "f1")
    for k in range(1, 5): clic(FONDO[k], "f%d" % (k + 1))
    clic(PEST["pantalla"], "p1"); clic(PANTALLA[1], "p2"); clic(PANTALLA[2], "p3")
    fin()
elif modo == "grises":
    # el modo billar encendido: lo que decide, en gris con su motivo; y otra vez abierta, el foco con Tab y el raton encima de «Listo»
    c.append((8.2, "g0")); cada[0] = True
    clic(BILLAR, "g1billar"); clic(AJUSTES, "g2aju"); clic(LOCAL, "g3local"); clic(TONO, "g4tono"); clic(VOLVER); clic(AFINAR, "g5afinar")
    cada[0] = False; tecla("27"); tecla("17+114", sep=1.5)
    for k in range(7): tecla("9", sep=0.4)   # (siete Tab: el primer deslizador, «Cuanto se sube cada cancion»)
    t[0] += 0.6; c.append((t[0], "g6deslizador")); e.append((t[0] + 0.2, "g6deslizador")); t[0] += 0.8
    for k in range(4): tecla("9", sep=0.4)   # (cuatro mas: el interruptor «Modo noche»)
    t[0] += 0.6; c.append((t[0], "g7interruptor")); e.append((t[0] + 0.2, "g7interruptor")); t[0] += 0.8
    p.append((t[0], "raton:%d:%d" % (906, 730))); c.append((t[0] + 1.0, "g8encima")); t[0] += 1.6   # (encima de «Listo»)
    fin()
elif modo == "teclas":
    # Tab hasta el primer deslizador; Derecha, Derecha, Izquierda, RePag, Fin, Inicio; la moneda (C) con el foco en el deslizador; Mayus+Tab,
    # Espacio e Intro en el interruptor de antes; las flechas entre hermanas; Escape (KeyPreview)
    t[0] = 8.4
    for k in range(7): tecla("9", sep=0.4)
    t[0] += 0.6; estado("deslizador", 0.0); t[0] += 0.6
    for vk in ["39", "39", "37", "33", "35", "36"]: tecla(vk, sep=0.5)
    t[0] += 0.6; tecla("67", "t1"); estado("moneda", 0.0); t[0] += 0.6
    tecla("16+9", sep=1.0); tecla("32", sep=1.0); tecla("13", sep=1.0); tecla("39", "t2"); estado("flechas", 0.0); t[0] += 0.6
    tecla("40", sep=1.0); tecla("38", sep=1.0); tecla("27"); estado("cerrada", 0.2); c.append((t[0] + 0.4, "t3"))
elif modo == "requisitos":
    # Las dependencias vivas (A1, AjustesVivos.cs: EnganchaRequisitos y RefrescaRequisitos): apagar «Estrella en 3D girando» (Fondo >
    # Estrella) deja en gris, con su «Necesita», lo de la estrella de Pulido; apagar «Barra de progreso neon» (Interfaz), el neon intenso
    # de su misma hojita, y al encenderla otra vez vuelve con lo guardado. Y lo guardado al momento, al salir.
    cada[0] = True
    clic(PEST["fondo"]); clic(FONDO[2], "r1estrella"); clic((276, 389), "r2sin3d"); clic(FONDO[4], "r3pulido")
    clic(FONDO[1]); clic((276, 309), "r4sinneon"); clic((276, 309), "r5conneon")
    fin()
elif modo == "guarda":
    # un interruptor, un deslizador y un boton de la pagina de Sonido tocados con el raton; la rocola muere (SIGKILL) con Ctrl+F3 abierto
    e.append((8.2, "abierta")); clic((214, 462)); clic((100, 496)); clic((622, 724)); c.append((t[0] + 0.4, "g1")); t[0] += 1.4; estado("tocada", 0.0); t[0] -= 1.5
elif modo == "diag":
    # «Diagnostico completo» (Imagen): la hoja se cierra, empieza el diagnostico y una tecla (Izquierda) lo corta; la hoja no vuelve
    clic(PEST["imagen"], sep=1.5); estado("imagen", 0.5); c.append((t[0] - 0.2, "d1")); clic(DIAGNOSTICO, sep=4.0); tecla("37", sep=2.0); estado("cortado", 0.0)
    t[0] += 1.0; c.append((t[0], "fin")); t[0] -= 0.4
elif modo == "medir":
    # «Medir este equipo» (Imagen): la hoja se cierra, se mide con la pantalla libre (lo que tarde: un minuto o mas, segun la maquina y lo
    # medido) y vuelve sola en Imagen. A los V segundos de pulsarlo (ya de vuelta): «Comparar perfiles» abre el historial de caratulas (otra
    # hoja, encima), la moneda con el abierto, Escape lo cierra y Escape cierra Ctrl+F3.
    v = float(sys.argv[2]) if len(sys.argv) > 2 else 90.0
    clic(PEST["imagen"], sep=1.5); estado("imagen", 0.5); c.append((t[0] - 0.2, "m1")); clic(MEDIR)
    t[0] = 10.0 + v; e.append((t[0], "vuelta")); c.append((t[0] + 0.3, "m2")); t[0] += 1.0
    clic(HISTORIAL, "m3historial", sep=1.7); estado("historial"); tecla("67", sep=1.2); estado("moneda")
    tecla("27", "m4sinhistorial", sep=1.7); estado("sinhistorial")
    fin()
def lista(l): return ",".join("%.1f:%s" % (x + ESPERA, n) for x, n in l)
print(lista(p))
print(lista(c))
print("%.0f" % (t[0] + ESPERA + 4))
print(lista(e))
