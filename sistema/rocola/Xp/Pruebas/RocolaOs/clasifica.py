#!/usr/bin/env python3
# clasifica.py: lo que la bateria del nivel 0 de la fase 11 (nivel0.sh, PLAN_FASE11.md §2.6) decide sola, para que quien la corre no tenga
# que interpretar nada. Cada orden escribe lineas «VEREDICTO<tab>detalle». Los veredictos:
#   BIEN      igual que la referencia (o que lo esperado)
#   ACEPTADO  distinto solo en lo aceptado por el dueno y las juntas del port, y lo dice (sigterm y f3sigterm: el estado se borra; los
#             rotulos del repaso en tab; el reloj saturado de F12; la linea de marca de VistasFondo; las capas y texturas de siempre de los
#             recorridos)
#   CUENTA    (escenas) distinto solo en cuentas de relojes, encargos o repeticiones, que con la maquina cargada se salen de la holgura, o
#             en el sitio de un volcado del estado en la secuencia (cae al otro lado de una tecla): la bateria repite la escena una vez
#             antes de darla por mala (PORT_JUNTAS.md)
#   REPETIR   (recorridos) una capa o una textura quieta que falta y no es de las de siempre: se repite ese recorrido una vez
#   MAL       cualquier otra diferencia
#   NO        no se pudo comparar (falta la salida, o el nivel 1 sin la opcion --ignora de compara_trazas.py)
#
#   clasifica.py escena NOMBRE DIR_A DIR_B [--ruido R.json] [--ignora PREFIJO] [--guiones DIR] [--informe ARCHIVO]
#       la escena NOMBRE de la referencia (DIR_A) contra la del programa (DIR_B), con compara_escena.py (Pruebas/Ventanas/guiones). Con
#       --ignora (el nivel 1) la traza se compara aparte con «compara_trazas.py --ignora PREFIJO» (sin los relojes y encargos de Rocola OS)
#       y lo demas con compara_escena.py sin la traza. --informe guarda lo que dijeron las comparaciones. UNA linea.
#   clasifica.py recorridos SALIDA CMP_REF CMP_REF2 REGISTRO [--tope PORCIENTO] [--solo NOMBRE,...]
#       los recorridos de SALIDA (con HUELLAS=1) contra las dos corridas de la referencia, como Recorridos/compara.py (lo QUIETO: lo que sale
#       igual en las dos); REGISTRO tiene las lineas «NOMBRE: salio con N» de recorridos.sh. Una linea por recorrido y una de TOTAL.
#   clasifica.py fondo REF.tsv NUEVO.tsv
#       VistasFondo: que lo unico distinto sea la linea de marca («inicio<tab>traducida=...»).
#   clasifica.py avisos COMPILACION.log AVISOS_BASE.txt XP
#       los avisos de la compilacion contra los de la base (archivo(linea): codigo, sin la columna: un gancho en una linea con aviso puede
#       correr la columna); UNA linea.
#   clasifica.py codigo XP BASE
#       los archivos de Codigo/ con el mismo numero de lineas, de CRLF y el mismo ultimo byte que en el commit BASE (los ganchos cambian una
#       linea en su sitio: nunca anaden, quitan ni mueven); UNA linea.
import os, re, subprocess, sys, tempfile, collections

AQUI = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------------------------------------------------------------------------------
# LAS ESCENAS
# ---------------------------------------------------------------------------------------------------------------------------------------

# SIGTERM es el apagado (regla del dueno del 8-11-2026): el programa cierra con WindowsShutDown y XP borra su estado; la referencia
# congelada no atendia SIGTERM y el estado quedaba (PORT_VENTANAS.md, V2, diferencia 1). Lo unico que puede sobrar en la secuencia del
# programa es el cierre: el encargo «Apagado», closing y closed de la rocola con WindowsShutDown (y, con una hoja abierta, los de la hoja con
# FormOwnerClosing y el fin de su dialogo, tras el que acaba la tecla que lo abrio: su «pck»), el foco que se va y las ventanas que se
# esconden. Y rocola_estado.txt esta en la referencia y falta en el programa.
ESCENAS_APAGADO = {"sigterm", "f3sigterm", "f2sigterm", "f12sigterm", "f3histsigterm"}
SOBRA_APAGADO = re.compile(r"^\+(encargo Apagado|(closing|closed) \S.* (WindowsShutDown|FormOwnerClosing)|dialogo-fin \S.*|foco \S.*|"
                           r"ventana-esconde \S.*|pck [01])$")
CIERRE_APAGADO = re.compile(r"^\+closed Form1'Rocola' WindowsShutDown$")

# El reloj de la ventana de prueba F12 (15 ms, un cuadro de la tarjeta por Tick) va saturado fuera de pantalla y su cuenta es de la maquina
# de ese momento, no monotona (PORT_arreglo-f12vigia.md): compara_trazas.py ya compara su total con el 30 % cuando va saturado en las dos
# trazas y lo dice en una NOTA; si aun asi se sale (el total o un segundo), es lo aceptado, solo con esa NOTA.
ESCENAS_F12 = {"f12", "f12vigia", "f12sigterm"}
RELOJ_F12 = "D3D9Star.cs:reloj-F12"

# Los rotulos de Ctrl+F3 que cuentan lo que el repaso de la biblioteca lleva medido (la escena tab: PORT_JUNTAS.md, PORT_VENTANAS2.md T6,
# PORT_armado-vistas.md): dependen de cuanto avanzo el repaso con la carga de la maquina (las prioridades de hilo de XP puestas de verdad),
# no del programa. Cada par de rotulos distintos tiene que ser el mismo rotulo (sangria, clase, sitio, medida y banderas iguales) con un texto
# de estos en los dos lados; el porcentaje solo junto al «Empareja el tono» de su misma tarjeta. Los puntos distintos de la capa de la hoja
# tienen que caer dentro de esos rotulos.
ESCENAS_REPASO = {"tab"}
TEXTOS_REPASO = [
    ("empareja", re.compile(r"^Empareja el tono de cada cancion con el de tu biblioteca\.\\nSolo: .+$")),
    ("videos", re.compile(r"^Los videos (suben hacia aqui\. Las canciones ya estan\\nmedidas: cada una sube hasta su limite\.|"
                          r"y las \d+ canciones? que faltan? por medir\\nsuben hacia aqui; las ya medidas, hasta su limite\.)$")),
    ("calidad", re.compile(r"^Calidad: +\d+ completas +\d+ aceptables +\d+ pobres$")),
    ("biblioteca", re.compile(r"^La biblioteca se mide sola mientras la rocola trabaja: (ya esta al dia|faltan \d+ archivos?)\.\\n.*$")),
    ("porcentaje", re.compile(r"^\d+ ?%$")),
]
PIEZA = re.compile(r"^(\s*)(\w+)'(.*)' (@-?\d+,-?\d+,\d+x\d+ .*)$")
CAJA = re.compile(r"^(\s*)(hoja )?(\w+)(?:'.*')? @(-?\d+),(-?\d+),(\d+)x(\d+)")
# el cliente de una hoja nativa empieza tras el marco y la barra (Nativo/Hoja.cs: Marco = 3, AltoBarra = 26); el de la referencia, igual
MARCO, ALTO_BARRA = 3, 26

CUENTA_RE = re.compile(r"^(repeticion «|reloj \S+ (en el segundo \d+|en total): |encargo(-perdido)? \S.*(: \d+ frente a \d+ en total| en el segundo \d+: ))")


def _texto_repaso(t):
    for nombre, rx in TEXTOS_REPASO:
        if rx.match(t):
            return nombre
    return None


def _trozos(diff):
    # los trozos («@@ -a,b +c,d @@») de un diff unificado sin contexto: (inicio en B, lineas quitadas, lineas puestas)
    r, actual = [], None
    for l in diff:
        m = re.match(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@", l)
        if m:
            actual = [int(m.group(1)), [], []]; r.append(actual); continue
        if actual is None or l.startswith("--- ") or l.startswith("+++ ") or l in ("---", "+++"):
            continue
        if l.startswith("-"): actual[1].append(l[1:])
        elif l.startswith("+"): actual[2].append(l[1:])
    return r


def _cajas_absolutas(lineas):
    # la caja en la pantalla de cada renglon de un estado (VolcadoEstado.cs): la hoja con su sitio y cada pieza dentro de su padre (la
    # sangria, dos espacios por nivel), desde el cliente de la hoja. Devuelve {indice de renglon: (x, y, w, h)}.
    cajas, pila = {}, []
    for i, l in enumerate(lineas):
        m = CAJA.match(l)
        if not m:
            continue
        x, y, w, h = int(m.group(4)), int(m.group(5)), int(m.group(6)), int(m.group(7))
        if m.group(2):   # «hoja ...»: el origen de sus piezas es su cliente
            pila = [(-1, (x + MARCO, y + MARCO + ALTO_BARRA))]
            cajas[i] = (x, y, w, h); continue
        if not pila:
            continue
        nivel = len(m.group(1)) // 2
        while len(pila) > 1 and pila[-1][0] >= nivel:
            pila.pop()
        ox, oy = pila[-1][1]
        cajas[i] = (ox + x, oy + y, w, h)
        pila.append((nivel, (ox + x, oy + y)))
    return cajas


class Escena:
    """Lo que dijo compara_escena.py, por partes."""
    def __init__(self, texto):
        self.texto = texto
        self.secuencia, self.cuentas, self.notas, self.otras_traza = [], [], [], []
        self.estados = collections.OrderedDict()     # estado -> lineas del diff
        self.pngs = []                               # (archivo, puntos distintos) de las capas que deben salir iguales
        self.malas = []                              # falta, sobra, config o teclas distintas
        self.estado_rocola = None                    # «esta / falta»...
        self.resultado = None
        bloque, cual = None, None
        for l in texto.splitlines():
            if l.startswith("   ") and bloque is not None:
                c = l[3:]
                if bloque == "traza": self._traza(c)
                elif bloque == "estado": self.estados[cual].append(c)
                continue
            bloque = None
            if l.startswith("traza: "):
                bloque = "traza"; self._traza(l[7:])
            elif re.match(r"^estado-\S+\.txt: DIFERENTE$", l):
                bloque, cual = "estado", l.split(":")[0]; self.estados[cual] = []
            elif re.match(r"^estado-\S+\.txt: igual$", l) or re.match(r"^rocola_(config|teclas)\.txt: igual$", l):
                pass
            elif " pixeles distintos" in l:
                m = re.match(r"^(\S+): (-?\d+) pixeles distintos(.*)$", l)
                if m and "DEBE SER 0" in m.group(3): self.pngs.append((m.group(1), int(m.group(2))))
            elif "transparente del todo en la referencia" in l:
                pass
            elif l.startswith("rocola_estado.txt: "):
                self.estado_rocola = l[len("rocola_estado.txt: "):].strip()
            elif l.startswith("RESULTADO: "):
                self.resultado = l[len("RESULTADO: "):].strip()
            elif l.strip():
                self.malas.append(l)

    def _traza(self, c):
        if c.startswith("DIFERENTE "):
            d = c[len("DIFERENTE "):]
            if d.startswith("secuencia: "): self.secuencia.append(d[len("secuencia: "):])
            elif CUENTA_RE.match(d): self.cuentas.append(d)
            else: self.otras_traza.append(d)
        elif c.startswith("NOTA "):
            self.notas.append(c[5:])
        elif re.match(r"^secuencia: \d+ sucesos", c) or re.match(r"^\d+ diferencias$", c) or c.startswith("IGUALES"):
            pass
        elif c.strip():
            self.otras_traza.append(c)


def _cambios_secuencia(sec):
    return [l for l in sec if not (l.startswith("--- ") or l.startswith("+++ ") or l.startswith("@@") or l.startswith(" "))]


def _apagado(e, quedan):
    # SIGTERM: lo que sobra en la secuencia, solo el cierre; y el estado esta en la referencia y falta en el programa
    cambios = _cambios_secuencia(e.secuencia)
    if not cambios: return None
    if not all(SOBRA_APAGADO.match(l) for l in cambios): return None
    if not any(CIERRE_APAGADO.match(l) for l in cambios): return None
    if e.estado_rocola != "esta / falta": return None
    quedan["secuencia"] = []
    quedan["estado_rocola"] = False
    return "el estado se borra (SIGTERM = apagado: closing y closed con WindowsShutDown, rocola_estado.txt borrado)"


def _repaso(e, dir_b, quedan):
    # tab: los rotulos que cuentan lo medido por el repaso; los puntos de la capa de la hoja, dentro de esos rotulos
    if not e.estados: return None
    rects = []
    for est, diff in e.estados.items():
        ruta = os.path.join(dir_b, est)
        if not os.path.exists(ruta): return None
        sys.path.insert(0, _guiones)
        from compara_escena import norm_estado   # (el mismo estado normalizado que compara: los numeros de renglon del diff)
        lineas = norm_estado(open(ruta).read()).split("\n")
        cajas = _cajas_absolutas(lineas)
        for inicio, quitadas, puestas in _trozos(diff):
            if not quitadas or len(quitadas) != len(puestas): return None
            nombres = []
            for k, (a, b) in enumerate(zip(quitadas, puestas)):
                ma, mb = PIEZA.match(a), PIEZA.match(b)
                if not ma or not mb: return None
                if ma.group(1) != mb.group(1) or ma.group(2) != mb.group(2) or ma.group(4) != mb.group(4): return None
                na, nb = _texto_repaso(ma.group(3)), _texto_repaso(mb.group(3))
                if na is None or na != nb: return None
                nombres.append(na)
                c = cajas.get(inicio - 1 + k)
                if c is None: return None
                rects.append(c)
            if "porcentaje" in nombres and "empareja" not in nombres: return None
    quedan["estados"] = False
    # los puntos distintos de las capas de las hojas: tapados esos rotulos (y 1 punto alrededor), las dos capas tienen que salir iguales
    malas = []
    for png, n in e.pngs:
        m = re.search(r"-(dibujo2d|hoja)-(-?\d+),(-?\d+)-(\d+)x(\d+)\.png$", png)
        if not m: malas.append(png); continue
        ox, oy = int(m.group(2)), int(m.group(3))
        pa, pb = os.path.join(_dir_a, png), os.path.join(dir_b, png.replace("-dibujo2d-", "-hoja-") if not os.path.exists(os.path.join(dir_b, png)) else png)
        if not (os.path.exists(pa) and os.path.exists(pb)): malas.append(png); continue
        dib = " ".join("rectangle %d,%d %d,%d" % (x - ox - 1, y - oy - 1, x - ox + w, y - oy + h) for x, y, w, h in rects)
        with tempfile.TemporaryDirectory() as d:
            ra, rb = os.path.join(d, "a.png"), os.path.join(d, "b.png")
            for o, s in ((pa, ra), (pb, rb)):
                subprocess.run(["convert", o, "-fill", "black", "-draw", dib, s], check=True)
            r = subprocess.run(["compare", "-metric", "AE", ra, rb, "null:"], capture_output=True, text=True)
            try: dist = int(float(r.stderr.split()[0]))
            except Exception: dist = -1
        if dist != 0: malas.append(png)
    if malas: return None
    quedan["pngs"] = []
    return "los rotulos del repaso (lo que el repaso de la biblioteca lleva medido al abrir Ctrl+F3: %d rotulos; sus puntos, %s)" % (
        len(rects), "%d en %d capas, todos dentro de esos rotulos" % (sum(n for _, n in e.pngs), len(e.pngs)) if e.pngs else "iguales")


def _f12(e, quedan):
    satur = any(n.startswith("reloj %s saturado en las dos" % RELOJ_F12) for n in e.notas)
    if not satur: return None
    f12 = [c for c in e.cuentas if c.startswith("reloj %s " % RELOJ_F12)]
    if not f12: return None
    quedan["cuentas"] = [c for c in e.cuentas if c not in f12]
    return "el reloj saturado de F12 (%d cuentas de %s fuera de la holgura; va saturado en las dos)" % (len(f12), RELOJ_F12)


def clasifica_escena(nombre, texto, dir_a, dir_b):
    e = Escena(texto)
    if e.resultado == "IGUAL" and not e.secuencia and not e.cuentas and not e.estados and not e.pngs and not e.malas and not e.otras_traza:
        return "BIEN", "IGUAL"
    if e.resultado is None:
        return "MAL", "la comparacion no acabo: " + (texto.strip().splitlines() or ["(nada)"])[-1][:200]
    quedan = {"secuencia": list(e.secuencia), "cuentas": list(e.cuentas), "estados": bool(e.estados), "pngs": list(e.pngs),
              "estado_rocola": e.estado_rocola is not None and e.estado_rocola.split(" / ")[0] != e.estado_rocola.split(" / ")[-1]}
    aceptado = []
    if nombre in ESCENAS_APAGADO:
        r = _apagado(e, quedan)
        if r: aceptado.append(r)
    if nombre in ESCENAS_REPASO:
        r = _repaso(e, dir_b, quedan)
        if r:
            aceptado.append(r)
            # (las cuentas de los encargos del repaso van con la misma carrera)
            rep = [c for c in quedan["cuentas"] if c.startswith("encargo Form1.Biblioteca.cs:")]
            if rep:
                quedan["cuentas"] = [c for c in quedan["cuentas"] if c not in rep]
                aceptado[-1] += "; con %d cuentas de los encargos del repaso" % len(rep)
    if nombre in ESCENAS_F12:
        r = _f12(e, quedan)
        if r: aceptado.append(r)
    # un volcado del estado (ROCOLA_ESTADO_EN, a su segundo fijo) que cae al otro lado de una tecla o un encargo: la secuencia tiene las
    # mismas lineas, solo que la del estado va en otro sitio (la hora de la maquina: PORT_form1-sin-form.md, la escena video). Es de la
    # carga, como las cuentas: se repite
    movidos = 0
    cambios = _cambios_secuencia(quedan["secuencia"])
    if cambios:
        menos = sorted(l[1:] for l in cambios if l.startswith("-")); mas = sorted(l[1:] for l in cambios if l.startswith("+"))
        if menos == mas and all(l.startswith("estado ") for l in menos):
            movidos = len(menos); quedan["secuencia"] = []
    mal = []
    if _cambios_secuencia(quedan["secuencia"]):
        mal.append("la secuencia: " + " | ".join(_cambios_secuencia(quedan["secuencia"]))[:300])
    if quedan["estados"]:
        mal.append("los estados: " + ", ".join(e.estados.keys()))
    if quedan["pngs"]:
        mal.append("las capas: " + ", ".join("%s (%d)" % p for p in quedan["pngs"]))
    if quedan["estado_rocola"]:
        mal.append("rocola_estado.txt: " + str(e.estado_rocola))
    if e.malas:
        mal.append(" | ".join(e.malas)[:300])
    if e.otras_traza:
        mal.append("la traza: " + " | ".join(e.otras_traza)[:300])
    if mal:
        return "MAL", "; ".join(mal)
    if quedan["cuentas"] or movidos:
        r = "solo cuentas (%d): %s" % (len(quedan["cuentas"]), " | ".join(quedan["cuentas"][:3])[:300]) if quedan["cuentas"] else ""
        if movidos: r += ("; y " if r else "") + "%d volcados del estado en otro sitio de la secuencia (la hora)" % movidos
        if aceptado: r += "; y lo aceptado: " + "; ".join(aceptado)
        return "CUENTA", r
    if aceptado:
        return "ACEPTADO", "; ".join(aceptado)
    return "MAL", "DIFERENTE sin detalle que se pueda clasificar"


_guiones = os.path.normpath(os.path.join(AQUI, "../Ventanas/guiones"))
_dir_a = None


def soporta_ignora(guiones):
    try:
        return "--ignora" in open(os.path.join(guiones, "compara_trazas.py"), encoding="utf-8").read()
    except OSError:
        return False


def compara(dir_a, dir_b, ruido, ignora, guiones):
    # lo que dicen compara_escena.py (y, en el nivel 1, compara_trazas.py --ignora para la traza)
    ce = [sys.executable, os.path.join(guiones, "compara_escena.py"), dir_a, dir_b] + (["--ruido", ruido] if ruido else [])
    if not ignora:
        return subprocess.run(ce, capture_output=True, text=True).stdout
    ta, tb = os.path.join(dir_a, "traza.txt"), os.path.join(dir_b, "traza.txt")
    traza = ""
    if os.path.exists(ta) and os.path.exists(tb):
        ct = [sys.executable, os.path.join(guiones, "compara_trazas.py"), ta, tb, "--ignora", ignora] + (["--ruido", ruido] if ruido else [])
        r = subprocess.run(ct, capture_output=True, text=True)
        traza = "traza: " + r.stdout.strip().replace("\n", "\n   ") + ("\n   DIFERENTE (compara_trazas.py salio con %d)" % r.returncode if r.returncode not in (0, 1) else "")
        mala_traza = r.returncode != 0
    else:
        mala_traza = False
    # lo demas, sin la traza del programa (compara_escena.py solo compara la traza si estan las dos)
    oculta = tb + ".nivel1"
    if os.path.exists(tb): os.rename(tb, oculta)
    try:
        resto = subprocess.run(ce, capture_output=True, text=True).stdout
    finally:
        if os.path.exists(oculta): os.rename(oculta, tb)
    lineas = [l for l in resto.splitlines() if not l.startswith("RESULTADO: ")]
    malo = mala_traza or "RESULTADO: DIFERENTE" in resto or "RESULTADO: " not in resto
    return "\n".join(([traza] if traza else []) + lineas + ["RESULTADO: " + ("DIFERENTE" if malo else "IGUAL")]) + "\n"


def orden_escena(args):
    global _guiones, _dir_a
    nombre, dir_a, dir_b = args[0], args[1], args[2]
    op = dict(zip(args[3::2], args[4::2]))
    _guiones = op.get("--guiones", _guiones)
    _dir_a = dir_a
    ignora = op.get("--ignora")
    if not os.path.isdir(dir_a) or not os.path.isdir(dir_b):
        print("NO\tfalta la salida de la escena (%s)" % (dir_a if not os.path.isdir(dir_a) else dir_b)); return
    if ignora and not soporta_ignora(_guiones):
        print("NO\tcompara_trazas.py aun no tiene --ignora (lo pone el grupo andamio): no se compara"); return
    texto = compara(dir_a, dir_b, op.get("--ruido"), ignora, _guiones)
    if "--informe" in op:
        open(op["--informe"], "w").write(texto)
    v, d = clasifica_escena(nombre, texto, dir_a, dir_b)
    print("%s\t%s" % (v, d))


# ---------------------------------------------------------------------------------------------------------------------------------------
# LOS RECORRIDOS
# ---------------------------------------------------------------------------------------------------------------------------------------

# Las capas de siempre (PORT_JUNTAS.md, juntas 13 a 17; PORT_borrar-capas.md): las tres de «ctrlf3» a los 46 s con F12 abierta
# (compara.py empareja por numero de capa y la referencia pone un tramo transparente por ventana: PORT_f12-como-hoja.md) y la hoja de Sonido
# de «hojas» a los 18 s y 19,5 s (lo que lleva medido el repaso: la carga de la maquina).
CAPAS_DE_SIEMPRE = [
    ("ctrlf3", re.compile(r"^c46\.png\.capa\d+-dibujo2d-"), "ctrlf3 a los 46 s con F12 abierta (la referencia pone un tramo transparente por ventana)"),
    ("hojas", re.compile(r"^c(18|19\.5)\.png\.capa\d+-dibujo2d-29,2-966x764\.png$"), "hojas a los 18 s (la hoja de Sonido con lo que lleva medido el repaso)"),
]
# Las texturas quietas que faltan y son de la hora y del ritmo (rotulos de 1024x128, 512x256 y 1024x256: el reloj de la cancion, la hora...;
# PORT_JUNTAS.md, junta 17: «las de la hora»), hasta un tope por recorrido.
TEXTURAS_DE_LA_HORA = {"1024x128", "512x256", "1024x256"}


def _lee_huellas(d):
    r = collections.defaultdict(list)
    p = os.path.join(d, "huellas.tsv")
    if os.path.isfile(p):
        for l in open(p):
            c = l.rstrip("\n").split("\t")
            if len(c) >= 3: r[c[0]].append((c[1], c[2]))
    return r


def orden_recorridos(args):
    sal, a, a2, registro = args[0], args[1], args[2], args[3]
    op = dict(zip(args[4::2], args[5::2]))
    tope = float(op.get("--tope", "25"))
    solo = set(op["--solo"].split(",")) if op.get("--solo") else None
    salio = {}
    if os.path.exists(registro):
        for l in open(registro):
            m = re.match(r"^(\w+): salio con (-?\d+)", l.strip())
            if m: salio[m.group(1)] = int(m.group(2))
    tot = collections.Counter()
    for rec in sorted(os.listdir(a)):
        if not os.path.isdir(os.path.join(a, rec)) or (solo and rec not in solo): continue
        if not os.path.isdir(os.path.join(sal, rec)):
            print("MAL\trecorrido %s: no hay salida" % rec); tot["mal"] += 1; continue
        A, A2, B = _lee_huellas(os.path.join(a, rec)), _lee_huellas(os.path.join(a2, rec)), _lee_huellas(os.path.join(sal, rec))
        ta = collections.Counter(h for _, h in A["textura"]); ta2 = collections.Counter(h for _, h in A2["textura"])
        tb = collections.Counter(h for _, h in B["textura"])
        nom = dict((h, n) for n, h in A["textura"] + A2["textura"])
        quietas = ta & ta2; enB = quietas & tb; faltan = quietas - tb
        da, da2, db = dict(A["dibujo2d"]), dict(A2["dibujo2d"]), dict(B["dibujo2d"])
        cq = [k for k in da if k in da2 and da[k] == da2[k]]; cqB = [k for k in cq if db.get(k) == da[k]]
        capas_mal = [k for k in cq if k not in cqB]
        problemas, acept = [], []
        s = salio.get(rec)
        if s != 0: problemas.append("salio con %s" % ("?" if s is None else s))
        sin_explicar = []
        for k in capas_mal:
            r = [why for (rr, rx, why) in CAPAS_DE_SIEMPRE if rr == rec and rx.match(k)]
            if r: acept.append(r[0])
            else: sin_explicar.append(k)
        fal = [nom.get(h, h) for h in faltan.elements()]
        de_la_hora = [n for n in fal if n.split("_")[0] in TEXTURAS_DE_LA_HORA]
        otras = [n for n in fal if n not in de_la_hora]
        nq = sum(quietas.values())
        detalle = "texturas quietas %d de %d" % (sum(enB.values()), nq)
        if fal:
            detalle += " (faltan %d: %s)" % (len(fal), ", ".join(sorted(set(fal)))[:200])
        detalle += "; capas quietas %d de %d" % (len(cqB), len(cq))
        if capas_mal:
            detalle += " (distintas: %s)" % ", ".join(capas_mal)[:300]
        if de_la_hora and not otras and nq and 100.0 * len(fal) / nq <= tope:
            acept.append("%d texturas de la hora y el ritmo (%s)" % (len(de_la_hora), ", ".join(sorted(TEXTURAS_DE_LA_HORA))))
        elif fal:
            sin_explicar.append("%d texturas quietas que faltan%s" % (len(fal), " (mas del %g %%)" % tope if nq and 100.0 * len(fal) / nq > tope else ""))
        tot["q"] += nq; tot["qb"] += sum(enB.values()); tot["cq"] += len(cq); tot["cqb"] += len(cqB); tot["n"] += 1
        tot["cero"] += 1 if s == 0 else 0
        if problemas:
            v = "MAL"; tot["mal"] += 1
        elif sin_explicar:
            v = "REPETIR"; tot["repetir"] += 1
            detalle += " -> sin explicar: " + "; ".join(sin_explicar)[:300]
        elif acept:
            v = "ACEPTADO"; tot["aceptado"] += 1
            detalle += " -> lo de siempre: " + "; ".join(sorted(set(acept)))
        else:
            v = "BIEN"
        print("%s\trecorrido %s: %s%s" % (v, rec, "salio con %s; " % s if s is not None else "", detalle))
    print("TOTAL\t%d de %d salieron con 0; texturas quietas %d de %d; capas quietas %d de %d; %d MAL, %d por repetir, %d con lo de siempre" % (
        tot["cero"], tot["n"], tot["qb"], tot["q"], tot["cqb"], tot["cq"], tot["mal"], tot["repetir"], tot["aceptado"]))


# ---------------------------------------------------------------------------------------------------------------------------------------
# VISTASFONDO, LOS AVISOS Y CODIGO/
# ---------------------------------------------------------------------------------------------------------------------------------------

def orden_fondo(args):
    ra, rb = open(args[0]).read().splitlines(), open(args[1]).read().splitlines()
    if ra == rb:
        print("BIEN\tIGUALES"); return
    dif = [(x, y) for x, y in zip(ra, rb) if x != y]
    if len(ra) == len(rb) and len(dif) == 1 and all(re.match(r"^inicio\ttraducida=(True|False)$", t) for t in dif[0]):
        print("ACEPTADO\tsolo la linea de marca (%s frente a %s)" % (dif[0][0].split("\t")[1], dif[0][1].split("\t")[1])); return
    print("MAL\t%d lineas distintas (de %d y %d)" % (len(dif) + abs(len(ra) - len(rb)), len(ra), len(rb)))


def _avisos(log, xp):
    r = set()
    for l in open(log, errors="replace"):
        m = re.search(r"(\S+\.cs)\((\d+),\d+\): warning (\w+)", l)
        if m:
            f = m.group(1)
            if f.startswith(xp.rstrip("/") + "/"): f = f[len(xp.rstrip("/")) + 1:]
            r.add("%s(%s): %s" % (f, m.group(2), m.group(3)))
    return r


def orden_avisos(args):
    log, base, xp = args
    ahora = _avisos(log, xp)
    antes = set(l.strip() for l in open(base) if l.strip() and not l.startswith("#"))
    nuevos, idos = sorted(ahora - antes), sorted(antes - ahora)
    if nuevos:
        print("MAL\t%d avisos, %d NUEVOS: %s" % (len(ahora), len(nuevos), "; ".join(nuevos)[:400])); return
    print("BIEN\t%d avisos, los de la base%s" % (len(ahora), (" (y %d de la base que ya no salen: %s)" % (len(idos), "; ".join(idos)[:200])) if idos else ""))


def orden_codigo(args):
    xp, base = args
    raiz = subprocess.run(["git", "-C", xp, "rev-parse", "--show-toplevel"], capture_output=True, text=True).stdout.strip()
    rel = os.path.relpath(os.path.join(xp, "Codigo"), raiz)
    r = subprocess.run(["git", "-C", raiz, "ls-tree", "--name-only", base, rel + "/"], capture_output=True, text=True)
    if r.returncode != 0:
        print("NO\tno esta la base %s en el repositorio" % base); return
    antes = sorted(os.path.basename(f) for f in r.stdout.split())
    ahora = sorted(f for f in os.listdir(os.path.join(xp, "Codigo")) if os.path.isfile(os.path.join(xp, "Codigo", f)))
    mal = []
    if antes != ahora:
        mal.append("archivos distintos (base %d, ahora %d: %s)" % (len(antes), len(ahora), ", ".join(sorted(set(antes) ^ set(ahora)))[:200]))
    for f in sorted(set(antes) & set(ahora)):
        b = subprocess.run(["git", "-C", raiz, "show", "%s:%s/%s" % (base, rel, f)], capture_output=True).stdout
        n = open(os.path.join(xp, "Codigo", f), "rb").read()
        if b.count(b"\n") != n.count(b"\n") or b.count(b"\r\n") != n.count(b"\r\n") or b[-1:] != n[-1:]:
            mal.append("%s (lineas %d/%d, CRLF %d/%d)" % (f, b.count(b"\n"), n.count(b"\n"), b.count(b"\r\n"), n.count(b"\r\n")))
    if mal:
        print("MAL\t%d archivos; distintos de la base %s: %s" % (len(ahora), base, "; ".join(mal)[:400])); return
    print("BIEN\t%d archivos con las mismas lineas, CRLF y ultimo byte que en %s" % (len(ahora), base))


if __name__ == "__main__":
    ordenes = {"escena": orden_escena, "recorridos": orden_recorridos, "fondo": orden_fondo, "avisos": orden_avisos, "codigo": orden_codigo}
    if len(sys.argv) < 2 or sys.argv[1] not in ordenes:
        print(__doc__ or open(__file__).read().split("\nimport ")[0]); sys.exit(2)
    ordenes[sys.argv[1]](sys.argv[2:])
