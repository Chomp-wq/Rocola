#!/usr/bin/env python3
# mide.py ESCENA CARPETA ETIQUETA: las medidas de una escena de mide.sh (rapido-spotify), una por linea: «MEDIDA etiqueta escena nombre ms».
# De la traza de la rocola (ROCOLA_TRAZA_VENTANAS: «ms evento», en ms desde que arranco): las teclas («tecla VK») y los encargos de
# Rocola OS y de XP (RocolaOs:spotify_catalogo:resultados, ...:portada, ...:disco, ...:pared, RocolaOs:pared_generos:portada; y el de XP
# que pone en pantalla las portadas decodificadas, «Form1.Portadas.cs:653», FlushCoverArrivals). Del sonido (suena): el registro de la
# tarjeta con reloj (registro.txt, hora de Unix de cada arranque) y frec.txt (frecuencias.py), y la hora en que el doble del catalogo
# atendio cada pedido («t=» de prueba-pide.log).
import sys, os, re, statistics

esc, car, etq = sys.argv[1], sys.argv[2], sys.argv[3]

def traza():
    ev = []
    try:
        for l in open(os.path.join(car, 'traza.txt'), encoding='utf-8', errors='replace'):
            p = l.rstrip('\n').split('\t', 1)
            if len(p) == 2 and p[0].isdigit(): ev.append((int(p[0]), p[1]))
    except FileNotFoundError: pass
    return ev
EV = traza()
def teclas(vk):   # las veces que llego la tecla VK (hex como la escribe la traza)
    return [t for t, x in EV if x.startswith('tecla %X ' % vk)]
def caracteres():
    return [t for t, x in EV if x.startswith('caracter ')]
def en(pat, t0=-1, t1=10**12):
    return [t for t, x in EV if t0 < t <= t1 and x.startswith('encargo ' + pat)]
def M(nombre, v):
    if v is None: print('MEDIDA %s %s %s -' % (etq, esc, nombre))
    else: print('MEDIDA %s %s %s %d' % (etq, esc, nombre, round(v)))

def peticiones(t0_unix=None):
    # tiempos.txt de la API falsa: [(llego, contesto, metodo, ruta, cod, nueva)]
    r = []
    try:
        for l in open(os.path.join(car, 'api', 'tiempos.txt')):
            p = l.split()
            if len(p) >= 5: r.append((int(p[0]), int(p[1]), p[2], p[3], p[4], len(p) > 5))
    except FileNotFoundError: pass
    return r

if esc == 'busca':
    cs = caracteres()
    # las dos busquedas: la ultima letra de cada palabra (8 y 7 letras, con 8 retrocesos entre medio)
    grupos = []
    for t in cs:
        if grupos and t - grupos[-1][-1] < 1000: grupos[-1].append(t)
        else: grupos.append([t])
    for i, g in enumerate(grupos[:2]):
        u = g[-1]; fin = grupos[i + 1][0] if i + 1 < len(grupos) else 10**12
        r = en('RocolaOs:spotify_catalogo:resultados', u, fin)
        M('busqueda%d_resultados' % (i + 1), (r[-1] - u) if r else None)
        p = en('RocolaOs:spotify_catalogo:portada', u, fin)
        M('busqueda%d_portadas_n' % (i + 1), len(p))
        M('busqueda%d_ultima_portada' % (i + 1), (p[-1] - u) if p else None)
        f = en('Form1.Portadas.cs:653', u, fin)
        M('busqueda%d_ultima_en_pantalla' % (i + 1), (f[-1] - u) if f else None)
elif esc == 'disco':
    k = [t for t in teclas(13) if t > 15000]
    if k:
        d = en('RocolaOs:spotify_catalogo:disco', k[0])
        M('abrir_disco', (d[0] - k[0]) if d else None)
        f = en('Form1.Portadas.cs:653', k[0], k[0] + 4000)
        M('disco_ultima_en_pantalla', (f[-1] - k[0]) if f else None)
elif esc == 'artista':
    k = [t for t in teclas(13) if t > 16000]
    a = [t for t in teclas(0x26) if t > 20000]
    if k:
        fin = a[0] if a else 10**12
        pa = en('RocolaOs:spotify_catalogo:pared', k[0], fin)
        M('pared_artista_bloques', len(pa))
        M('pared_artista_abierta', (pa[-1] - k[0]) if pa else None)
        po = en('RocolaOs:spotify_catalogo:portada', k[0], fin)
        M('pared_artista_ultima_portada', (po[-1] - k[0]) if po else None)
        f = en('Form1.Portadas.cs:653', k[0], fin)
        M('pared_artista_ultima_en_pantalla', (f[-1] - k[0]) if f else None)
    if a:
        pa = en('RocolaOs:spotify_catalogo:pared', a[0], a[0] + 3900)
        M('pared_artista_pag2', (pa[-1] - a[0]) if pa else None)
        po = en('RocolaOs:spotify_catalogo:portada', a[0], a[0] + 3900)
        M('pared_artista_pag2_ultima_portada', (po[-1] - a[0]) if po else None)
        f = en('Form1.Portadas.cs:653', a[0], a[0] + 3900)
        M('pared_artista_pag2_ultima_en_pantalla', (f[-1] - a[0]) if f else None)
elif esc == 'pared':
    ks = sorted(teclas(0x26) + teclas(0x28))
    ks = [t for t in ks if t > 13000]
    v_baja, v_pant = [], []
    for i, t in enumerate(ks):
        fin = ks[i + 1] if i + 1 < len(ks) else t + 3900
        b = en('RocolaOs:pared_generos:portada', t, fin)
        f = en('Form1.Portadas.cs:653', t, fin)
        M('pagina%d_bajadas_n' % (i + 1), len(b))
        M('pagina%d_ultima_bajada' % (i + 1), (b[-1] - t) if b else None)
        M('pagina%d_ultima_en_pantalla' % (i + 1), (f[-1] - t) if f else None)
        if f: v_pant.append(f[-1] - t)
    pe = [x for x in peticiones() if x[3].startswith('/release-group/')]
    M('caa_pedidas', len(pe))
    if v_pant: M('paginas_en_pantalla_mediana', statistics.median(v_pant))
elif esc == 'suena':
    # cuando se atendio cada pedido (hora de Unix)
    pedidos = []
    try:
        for l in open(os.path.join(car, 'run/rocola-xp/spotify/prueba-pide.log')):
            m = re.search(r' t=(\d+)', l)
            if m and ' entro=1' in l: pedidos.append(int(m.group(1)))
    except FileNotFoundError: pass
    # las sesiones de la tarjeta (cada apertura de la salida: su frecuencia y sus arranques con la hora)
    ses = []   # {rate, inicio (cuadro global), anclas [(cuadro dentro, hora_ms)], total}
    glob = 0
    try:
        for l in open(os.path.join(car, 'registro.txt')):
            p = l.split()
            if len(p) < 3: continue
            c = dict(x.split('=', 1) for x in p[2:] if '=' in x)
            if 'rate' not in c: continue
            hora = float(p[0]) * 1000.0; que = p[1]; tot = int(c['total']); rate = int(c['rate'])
            if que == 'PREPARA' and tot == 0 and (not ses or ses[-1]['total'] > 0):
                if ses: glob += ses[-1]['total']
                ses.append({'rate': rate, 'inicio': glob, 'anclas': [], 'total': 0})
            if not ses: continue
            s = ses[-1]; s['total'] = max(s['total'], tot)
            if que in ('ARRANCA', 'SIGUE'): s['anclas'].append((tot, hora))
    except FileNotFoundError: pass
    def hora_de(seg):
        # el segundo «seg» de frec.txt (lo sonado seguido, cada sesion a su frecuencia) -> la hora de Unix en que sono
        t = 0.0
        for s in ses:
            dur = s['total'] / s['rate']
            if seg <= t + dur + 1e-6:
                cuadro = (seg - t) * s['rate']
                a = [x for x in s['anclas'] if x[0] <= cuadro + 1]
                if not a: return None
                return a[-1][1] + (cuadro - a[-1][0]) * 1000.0 / s['rate']
            t += dur
        return None
    fr = []
    try:
        for l in open(os.path.join(car, 'frec.txt')):
            d, h, f = l.split(); fr.append((float(d), float(h), int(f)))
    except FileNotFoundError: pass
    def primero(hz): return next((d for d, h, f in fr if abs(f - hz) <= 15 and h - d >= 0.3), None)
    def ultimo(hz):
        u = None
        for d, h, f in fr:
            if abs(f - hz) <= 15 and h - d >= 0.3: u = h
        return u
    a0 = primero(440); a1 = ultimo(440); b0 = primero(550)
    if pedidos and a0 is not None and hora_de(a0) is not None: M('pide_a_suena', hora_de(a0) - pedidos[0])
    else: M('pide_a_suena', None)
    if a1 is not None and b0 is not None and hora_de(a1) is not None and hora_de(b0) is not None: M('hueco_entre_dos', hora_de(b0) - hora_de(a1))
    else: M('hueco_entre_dos', None)
    M('oida_440_ms', sum(h - d for d, h, f in fr if abs(f - 440) <= 15) * 1000)
    M('oida_550_ms', sum(h - d for d, h, f in fr if abs(f - 550) <= 15) * 1000)
    M('oida_990_ms', sum(h - d for d, h, f in fr if abs(f - 990) <= 15) * 1000)
