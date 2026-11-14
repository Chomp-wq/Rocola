#!/usr/bin/env python3
# compara_trazas.py A B [--ruido RUIDO.json] [--holgura H]
# compara_trazas.py --mide-ruido SALIDA.json A B [C ...]
# Compara dos trazas de ROCOLA_TRAZA_VENTANAS (Plataforma/TrazaVentanas.cs) de la misma escena: la de la referencia y la de la
# traduccion (o la referencia contra si misma, para medir el ruido). Lo que llega al codigo de XP se reparte en:
#  - LA SECUENCIA (tiene que ser igual, sin los milisegundos): lo que entra por la entrada y el bucle de la pantalla en orden (teclas con
#    lo que devolvio ProcessCmdKey, KeyDown, caracteres, KeyUp, raton, foco, ventanas, dialogos, Shown, cierres, y los encargos que pone
#    el propio hilo de la pantalla: Shown, Alt+F4);
#  - LA REPETICION de una tecla mantenida (la manda otro hilo cada 33 ms, asi que donde cae entre las demas teclas depende de la hora):
#    cada «tecla ... rep» con todo lo que provoco (pck, keydown, caracter...) se saca de la secuencia y se cuenta por su firma;
#  - LOS RELOJES: cuantos Tick de cada uno en cada segundo y en total;
#  - LOS ENCARGOS DE OTROS HILOS (la biblioteca, el vigia de la pantalla, el sonido): cuantos de cada uno por segundo y en total (llegan
#    cuando su hilo acaba, no en un sitio fijo de la secuencia).
# Las cuentas se comparan con la holgura del ruido medido (la referencia corrida varias veces: --mide-ruido) mas un margen, o con
# --holgura H si no hay ruido medido.
# LOS RELOJES QUE PINTAN UN CUADRO DE LA TARJETA EN CADA TICK (RELOJES_DE_CUADRO: el de la ventana de prueba F12, GpuTestWindow.timer, de
# 15 ms). Fuera de pantalla (llvmpipe) su Tick cuesta 7 a 15 ms y entre dos suyos cabe un cuadro de la rocola (unos 20 ms): el reloj no
# llega a su plazo (va «saturado») y cuantos Tick da ya no lo dice el plazo sino la maquina de ese momento, y NO de forma monotona: con la
# regla de WM_TIMER (los vencimientos van en una rejilla fija y los que se juntan salen como uno) la cuenta sube y baja al cambiar un poco
# lo que cuesta un cuadro (reloj_saturado.py: el modelo y la medida). La referencia contra si misma dio de 203 a 286 Tick en la escena
# f12vigia segun la velocidad de la maquina (arreglo-f12vigia: PORT_arreglo-f12vigia.md), y la traduccion, que compone una capa
# transparente menos por cuadro (la que la capa de la referencia arma encima de la tarjeta de la ventana), cae en otro punto de esa curva: a una velocidad da 30 mas que la referencia y a otra 30 menos. Por eso
# el TOTAL de un reloj de cuadro que va saturado en las dos trazas (su intervalo medio pasa del 110 % del plazo) se compara con la
# holgura del ruido o con el 30 % de la cuenta mayor, lo que sea mas (lo que la referencia se separa de si misma); por segundo, como los
# demas. Si en alguna de las dos no va saturado, como cualquier reloj.
import sys, re, json, math, difflib, collections, itertools

PROPIOS = {"Shown", "Alt+F4", "Apagado"}   # encargos que pone el hilo de la pantalla (o el apagado): van en la secuencia
CORTA = {"tecla", "keyup", "raton", "tick", "encargo", "encargo-perdido", "estado"}   # lo que empieza otro suceso
RELOJES_DE_CUADRO = {"D3D9Star.cs:reloj-F12"}   # relojes cuyo Tick pinta y presenta un cuadro entero de la tarjeta (ver arriba)
SATURADO, RELATIVA = 1.10, 0.30               # intervalo medio / plazo desde el que va saturado; la holgura relativa de su total


# LA TABLA DE EQUIVALENCIAS DE LAS VENTANAS (port nativo, ventanas2 H; la misma del diseno y del diff): cada pieza nativa se apunta con su
# clase (Nativo/Hoja.cs, Rotulo.cs...) y la referencia con la de WinForms de la capa. Solo donde la traza nombra una pieza: tras «a », tras
# el suceso (foco, ventana-ve, ventana-esconde, dialogo-fin, shown, closing, closed, raton EVENTO), y en los estados tras «foco=»,
# «activa=», «hoja » y al principio de cada renglon de pieza.
# (rotulos-y-letras) RotuloVista: el rotulo de la ventana de la rocola (Nativo/RotuloVista.cs), el Label de la capa en la referencia
EQUIVALENCIAS = {"Hoja": "Form", "Rotulo": "Label", "RotuloVista": "Label", "Grupo": "Panel", "BotonXp": "Button", "ListaXp": "ListBox", "AvisoXp": "CajaAviso"}
_PIEZA = r"(Hoja|RotuloVista|Rotulo|Grupo|BotonXp|ListaXp|AvisoXp)(?=['\s]|$)"
_SITIOS = [r"( a )", r"^((?:foco|ventana-ve|ventana-esconde|dialogo-fin|shown|closing|closed) )", r"^(raton \S+ )",
           r"^((?:foco|activa)=)", r"^(hoja )", r"^(\s+)"]


def equivalentes(que):
    for sitio in _SITIOS:
        que = re.sub(sitio + _PIEZA, lambda m: m.group(1) + EQUIVALENCIAS[m.group(2)], que, count=1, flags=re.M)
    return que


def normaliza(que):
    que = equivalentes(que)
    # la fecha del programa en el titulo de Ctrl+F3 (con JIT es la de dotnet; con NativeAOT la del ejecutable)
    que = re.sub(r"Esta rocola es del \d\d/\d\d/\d{4} \d\d:\d\d", "Esta rocola es del FECHA", que)
    que = re.sub(r"^estado .*/", "estado ", que)
    # los cuadros por segundo que la ventana de prueba F12 pone en su titulo (GpuTestWindow.Frame): los de la maquina de ese momento
    que = re.sub(r" *\[\d+ FPS, \d+ estrellas\]", "", que)   # (el titulo ya lo tiene, o aun no, segun cuando toque: depende de la maquina)
    # el reloj de la ventana F12 (GpuTestWindow.timer) se nombra por donde se crea: la linea de la referencia (1997) y la de la traduccion (1867)
    que = re.sub(r"D3D9Star\.cs:(1997|1867)(?!\d)", "D3D9Star.cs:reloj-F12", que)
    return que


def lee(ruta):
    ev = []
    with open(ruta, encoding="utf-8") as f:
        for l in f:
            l = l.rstrip("\n")
            if not l:
                continue
            ms, que = l.split("\t", 1)
            ev.append((int(ms), normaliza(que)))
    return ev


class Partes:
    def __init__(self, ev):
        self.sec = []                                   # la secuencia
        self.reps = collections.Counter()               # firma de la tecla repetida -> veces
        self.ticks = collections.Counter()              # (reloj, segundo) -> ticks
        self.ticks_total = collections.Counter()        # reloj -> ticks
        self.ticks_ms = collections.defaultdict(list)   # reloj -> los milisegundos de cada tick
        self.plazo = {}                                 # reloj -> su Interval
        self.enc = collections.Counter()                # (encargo, segundo) -> veces
        self.enc_total = collections.Counter()          # encargo -> veces
        i = 0
        while i < len(ev):
            ms, q = ev[i]
            p = q.split(" ")
            if p[0] == "tick":
                self.ticks[(p[1], ms // 1000)] += 1
                self.ticks_total[p[1]] += 1
                self.ticks_ms[p[1]].append(ms)
                if len(p) > 2 and p[2].isdigit():
                    self.plazo[p[1]] = int(p[2])
                i += 1
                continue
            if p[0] in ("encargo", "encargo-perdido") and (len(p) < 2 or p[1] not in PROPIOS):
                self.enc[(q, ms // 1000)] += 1
                self.enc_total[q] += 1
                i += 1
                continue
            if p[0] == "tecla" and len(p) > 2 and p[2] == "rep":
                grupo = [" ".join(p[:2] + p[3:])]
                j = i + 1
                while j < len(ev) and ev[j][1].split(" ")[0] not in CORTA:
                    grupo.append(ev[j][1])
                    j += 1
                self.reps[" | ".join(grupo)] += 1
                i = j
                continue
            self.sec.append(q)
            i += 1


    # el intervalo medio de un reloj entre su plazo (1 = a su hora), sin los huecos de mas de 10 plazos (cerrado, o una captura); None
    # si no hay bastantes Tick para decirlo
    def ritmo(self, reloj):
        plazo = max(10, self.plazo.get(reloj, 0))       # (menos de 10 ms es 10: USER_TIMER_MINIMUM, Nativo/Reloj.cs)
        t = self.ticks_ms.get(reloj, [])
        d = [y - x for x, y in zip(t, t[1:]) if y - x <= 10 * plazo]
        if len(d) < 20:
            return None
        return sum(d) / float(len(d)) / plazo


def saturado(reloj, A, B):
    if reloj not in RELOJES_DE_CUADRO:
        return False
    ra, rb = A.ritmo(reloj), B.ritmo(reloj)
    return ra is not None and rb is not None and ra > SATURADO and rb > SATURADO


def dif_cuenta(a, b):
    m = 0
    for k in set(a) | set(b):
        m = max(m, abs(a[k] - b[k]))
    return m


def mide(rutas):
    ps = [Partes(lee(r)) for r in rutas]
    r = {"tick": 0, "tick_total": 0, "encargo": 0, "encargo_total": 0, "rep": 0, "secuencia_igual": True, "corridas": len(rutas)}
    for a, b in itertools.combinations(ps, 2):
        r["tick"] = max(r["tick"], dif_cuenta(a.ticks, b.ticks))
        r["tick_total"] = max(r["tick_total"], dif_cuenta(a.ticks_total, b.ticks_total))
        r["encargo"] = max(r["encargo"], dif_cuenta(a.enc, b.enc))
        r["encargo_total"] = max(r["encargo_total"], dif_cuenta(a.enc_total, b.enc_total))
        r["rep"] = max(r["rep"], dif_cuenta(a.reps, b.reps))
        r["secuencia_igual"] &= a.sec == b.sec
    return r


def holgura(ruido, clave, defecto):
    # el ruido medido mas la mitad (al menos 2): dos corridas de la referencia no dan el peor caso
    if clave not in ruido:
        return defecto
    n = ruido[clave]
    return n + max(2, int(math.ceil(n / 2.0)))


notas = []   # lo que se dice aunque salga igual (los relojes de cuadro saturados y su holgura)


def compara(ra, rb, ruido, hol):
    A, B = Partes(lee(ra)), Partes(lee(rb))
    dif = []
    del notas[:]
    if A.sec != B.sec:
        for l in difflib.unified_diff(A.sec, B.sec, "A", "B", lineterm="", n=2):
            dif.append("secuencia: " + l)
    h = holgura(ruido, "rep", hol)
    for k in sorted(set(A.reps) | set(B.reps)):
        if abs(A.reps[k] - B.reps[k]) > h:
            dif.append("repeticion «%s»: %d frente a %d (holgura %d)" % (k, A.reps[k], B.reps[k], h))
    h = holgura(ruido, "tick", hol)
    for k in sorted(set(A.ticks) | set(B.ticks)):
        if abs(A.ticks[k] - B.ticks[k]) > h:
            dif.append("reloj %s en el segundo %d: %d frente a %d (holgura %d)" % (k[0], k[1], A.ticks[k], B.ticks[k], h))
    h = holgura(ruido, "tick_total", hol)
    for k in sorted(set(A.ticks_total) | set(B.ticks_total)):
        hk = h
        if saturado(k, A, B):   # un reloj de cuadro que no llega a su plazo en ninguna de las dos: la cuenta es de la maquina (ver arriba)
            hk = max(h, int(RELATIVA * max(A.ticks_total[k], B.ticks_total[k])))
            notas.append("reloj %s saturado en las dos (intervalo medio %.0f %% y %.0f %% del plazo): %d frente a %d en total, holgura %d"
                         % (k, 100 * A.ritmo(k), 100 * B.ritmo(k), A.ticks_total[k], B.ticks_total[k], hk))
        if abs(A.ticks_total[k] - B.ticks_total[k]) > hk:
            dif.append("reloj %s en total: %d frente a %d (holgura %d)" % (k, A.ticks_total[k], B.ticks_total[k], hk))
    h = holgura(ruido, "encargo_total", hol)
    for k in sorted(set(A.enc_total) | set(B.enc_total)):
        if abs(A.enc_total[k] - B.enc_total[k]) > h:
            dif.append("%s: %d frente a %d en total (holgura %d)" % (k, A.enc_total[k], B.enc_total[k], h))
    h = holgura(ruido, "encargo", hol)
    for k in sorted(set(A.enc) | set(B.enc)):
        if abs(A.enc[k] - B.enc[k]) > h:
            dif.append("%s en el segundo %d: %d frente a %d (holgura %d)" % (k[0], k[1], A.enc[k], B.enc[k], h))
    return dif, A, B


if __name__ == "__main__":
    args = sys.argv[1:]
    hol, ruido = 2, {}
    if "--holgura" in args:
        i = args.index("--holgura"); hol = int(args[i + 1]); del args[i:i + 2]
    if "--ruido" in args:
        i = args.index("--ruido"); ruido = json.load(open(args[i + 1])); del args[i:i + 2]
    if "--mide-ruido" in args:
        i = args.index("--mide-ruido"); salida = args[i + 1]; del args[i:i + 2]
        r = mide(args)
        print(json.dumps(r))
        if salida != "-":
            json.dump(r, open(salida, "w"))
        sys.exit(0 if r["secuencia_igual"] else 1)
    dif, A, B = compara(args[0], args[1], ruido, hol)
    print("secuencia: %d sucesos; teclas repetidas: %d; relojes: %d ticks; encargos de otros hilos: %d"
          % (len(A.sec), sum(A.reps.values()), sum(A.ticks_total.values()), sum(A.enc_total.values())))
    for n in notas:
        print("NOTA " + n)
    if dif:
        for d in dif[:80]:
            print("DIFERENTE " + d)
        print("%d diferencias" % len(dif))
        sys.exit(1)
    print("IGUALES (con la holgura del ruido)")
