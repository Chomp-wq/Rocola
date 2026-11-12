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
import sys, re, json, math, difflib, collections, itertools

PROPIOS = {"Shown", "Alt+F4", "Apagado"}   # encargos que pone el hilo de la pantalla (o el apagado): van en la secuencia
CORTA = {"tecla", "keyup", "raton", "tick", "encargo", "encargo-perdido", "estado"}   # lo que empieza otro suceso


# LA TABLA DE EQUIVALENCIAS DE LAS VENTANAS (port nativo, ventanas2 H; la misma del diseno y del diff): cada pieza nativa se apunta con su
# clase (Nativo/Hoja.cs, Rotulo.cs...) y la referencia con la de WinForms de la capa. Solo donde la traza nombra una pieza: tras «a », tras
# el suceso (foco, ventana-ve, ventana-esconde, dialogo-fin, shown, closing, closed, raton EVENTO), y en los estados tras «foco=»,
# «activa=», «hoja » y al principio de cada renglon de pieza.
EQUIVALENCIAS = {"Hoja": "Form", "Rotulo": "Label", "Grupo": "Panel", "BotonXp": "Button", "ListaXp": "ListBox", "AvisoXp": "CajaAviso"}
_PIEZA = r"(Hoja|Rotulo|Grupo|BotonXp|ListaXp|AvisoXp)(?=['\s]|$)"
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
        self.enc = collections.Counter()                # (encargo, segundo) -> veces
        self.enc_total = collections.Counter()          # encargo -> veces
        i = 0
        while i < len(ev):
            ms, q = ev[i]
            p = q.split(" ")
            if p[0] == "tick":
                self.ticks[(p[1], ms // 1000)] += 1
                self.ticks_total[p[1]] += 1
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


def compara(ra, rb, ruido, hol):
    A, B = Partes(lee(ra)), Partes(lee(rb))
    dif = []
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
        if abs(A.ticks_total[k] - B.ticks_total[k]) > h:
            dif.append("reloj %s en total: %d frente a %d (holgura %d)" % (k, A.ticks_total[k], B.ticks_total[k], h))
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
    if dif:
        for d in dif[:80]:
            print("DIFERENTE " + d)
        print("%d diferencias" % len(dif))
        sys.exit(1)
    print("IGUALES (con la holgura del ruido)")
