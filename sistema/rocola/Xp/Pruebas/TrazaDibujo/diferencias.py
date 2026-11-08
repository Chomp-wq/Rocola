# LAS DIFERENCIAS CONOCIDAS DE LA REFERENCIA FRENTE A GDI+ (riesgos (a)-(e) de dibujo.md), medidas: (a) con la cuenta de LockBits de
# Dibujo2D.cs (todas las parejas alfa/canal) y el halo de EstrellaGpu.cs; (b)-(e) con la traza por linea de los recorridos.
import sys, os, re, math, collections
raiz = sys.argv[1]
# (a) Format32bppArgb guardado premultiplicado: se escribe c*a/255 (truncado) y se lee min(255, p*255/a) (truncado)
malos = 0; peor = 0; porAlfa = collections.Counter()
for a in range(256):
    for c in range(256):
        p = c * a // 255
        r = c if a in (0, 255) else min(255, p * 255 // a) if a else p
        if a == 0: r = p   # alfa 0: se escribe 0 y se lee 0 (el color se pierde)
        if r != c:
            malos += 1; peor = max(peor, abs(r - c)); porAlfa[a] += 1
print('(a) LockBits Format32bppArgb sobre un mapa premultiplicado: %d de 65536 parejas (alfa, canal) no vuelven igual; diferencia maxima %d' % (malos, peor))
print('    alfa 1: %d de 256 canales cambian; alfa 32: %d; alfa 128: %d; alfa 254: %d; alfa 0 pierde el color (los 255 distintos de 0)' % (porAlfa[1], porAlfa[32], porAlfa[128], porAlfa[254]))
for a, c in ((1, 0xB0), (1, 0xCF), (32, 0xB0), (32, 0xCF)):
    p = c * a // 255; print('    ejemplo: alfa %d, canal %02X -> premultiplicado %d -> se lee %02X' % (a, c, p, min(255, p * 255 // a)))
# el halo de EstrellaGpu.cs (BakeStar, lineas 15-25): a = (int)(32*exp(-r*3)*(1-r)) con el color B0CFFF
n = 256; cambian = 0; halo = 0; peor = 0; efecto = 0.0
for y in range(n):
    for x in range(n):
        dx = (x - 127.5) / 127.5; dy = (y - 127.5) / 127.5; r = dx * dx + dy * dy
        a = 0 if r >= 1 else int(32 * math.exp(-r * 3) * (1 - r))
        if a == 0: continue
        halo += 1
        for c in (0xB0, 0xCF, 0xFF):
            p = c * a // 255; rr = min(255, p * 255 // a)
            if rr != c: cambian += 1; peor = max(peor, abs(rr - c)); efecto = max(efecto, abs(rr - c) * a / 255.0)
print('    EstrellaGpu.cs (BakeStar): %d puntos del halo con alfa 1-32; %d canales salen distintos al leerlos (hasta %d niveles del canal, que con su alfa'
      ' pesan como mucho %.2f niveles en la pantalla), antes de pintar la estrella encima' % (halo, cambian, peor, efecto))
ef = 0.0
for a in range(1, 255):
    for c in range(256):
        p = c * a // 255; rr = min(255, p * 255 // a)
        ef = max(ef, abs(rr - c) * a / 255.0)
print('    en general (alfa 1-254): lo que se pierde pesa como mucho %.2f niveles en la pantalla (la diferencia por su alfa)' % ef)
# (b)-(e) con la traza
filas = []
for rec in sorted(os.listdir(raiz)):
    t = os.path.join(raiz, rec, 'traza.tsv')
    if not os.path.isfile(t): continue
    for l in open(t, encoding='utf-8'):
        p = l.rstrip('\n').split('\t')
        if len(p) >= 8: filas.append((rec,) + tuple(p[:8]))
def est(e): return dict(x.split('=', 1) for x in e.split(' ') if '=' in x)
cleartype = collections.Counter(); sistema = collections.Counter(); caps = collections.Counter(); finas = collections.Counter(); imgEsc = collections.Counter(); textoFrac = collections.Counter()
for rec, archivo, linea, op, estado, n, ini, fin, args in filas:
    e = est(estado); n = int(n); k = archivo + ':' + linea
    texto = op in ('Graphics.DrawString', 'TextRenderer.DrawText')
    if texto and e.get('t') == 'ClearTypeGridFit': cleartype[k + ' ' + op] += n
    if op == 'TextRenderer.DrawText' and e.get('t') == 'SystemDefault': sistema[k] += n
    for m in re.finditer(r'Pen\([^)]*c=(\w+)/(\w+)', args):
        if m.group(1) != m.group(2): caps[k + ' ' + m.group(1) + '/' + m.group(2)] += n
    for m in re.finditer(r'Pen\([^,]*,w=([0-9.]+)', args):
        if float(m.group(1)) < 1 and e.get('m') in ('S', 'R'): finas[k + ' w=' + m.group(1) + ' m=' + e.get('m')] += n
        elif float(m.group(1)) < 1: finas[k + ' w=' + m.group(1) + ' (sin escala: igual que GDI+)'] += 0
    if op == 'Graphics.DrawImage' and e.get('p') in ('Default', 'HighSpeed', 'None'):
        imgEsc[k + ' i=' + e.get('i', '?')] += n
    if op == 'Graphics.DrawString' and (e.get('m') in ('Tf', 'S', 'R') or re.search(r'\.\d', args.split(' ')[-2] if args else '')): textoFrac[k + ' m=' + e.get('m', '?')] += n
def lista(titulo, c):
    print(titulo + ': %d lineas, %d llamadas' % (len(c), sum(c.values())))
    for k, v in sorted(c.items()): print('    %s x%d' % (k, v))
lista('(b) texto con ClearTypeGridFit (en XP, ClearType con flecos de color; la referencia, gris)', cleartype)
lista('(b2) TextRenderer con el modo de letra del sistema (SystemDefault: en XP, el suavizado que tenga Windows)', sistema)
lista('(c) plumas con dos puntas distintas (la referencia usa una sola para las dos)', caps)
lista('(d) plumas de menos de 1 px con escala o giro (la referencia pone 1 px en el lienzo; GDI+ 1 px en la pantalla)', collections.Counter({k: v for k, v in finas.items() if v}))
lista('(e1) DrawImage con PixelOffsetMode de fabrica (la referencia corre medio punto la imagen; sin comprobar en GDI+ cuando se escala)', imgEsc)
lista('(e2) DrawString con traslacion fraccionaria, escala o giro (la referencia no corre el texto medio pixel)', textoFrac)
